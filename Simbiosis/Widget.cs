using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Windows.Forms;
using System.Xml;
using System.IO;

namespace Simbiosis
{
	// Widget classes for panel decorations, gauges and controls
	// Add new instances of these classes to the panel.widgetList to build a panel for a given camera ship
	//
	// Widget classes:
	// ---------------
	// Widget		- this is the base class for more advanced widgets, and the class to use for panel backdrop sprites.
	// Switch		- a toggle switch. Each click toggles between off and on.
	// Button		- a simple pushbutton.
	// CycleButton	- a button that visibly cycles to the next state (frame) each time it is pressed.
	// SafetyButton - a pushbutton with a safety cover. One click opens the cover and another presses the button.
	// MultiSwitch	- a rotary switch. Clicking on the right increments the switch, clicking the left decrements it.
	// Knob			- an analogue knob. Holding down button on left or right side rotates knob until released.
	// Lamp			- simple two-state or multi-state indicator. Setting Lamp.Value defines the displayed frame.
	// Meter		- rotating needle.
	// Selector		- scrolling list.
	// Carousel		- widget that can load different bitmaps from disk.
	// MagicEye		- progress bar (currently only horizontal and unsigned)
	// Container	- abstract base class for multi-widget widgets (like those used for editing terminal blocks)
	// TerminalUI	- Multi-widget control for editing terminal blocks (nerve wiring)
	//
	// The base widget class (and optionally some subclasses) can have scaled text and graphics written to them
	// (at a cost of efficiency).
	//
	// Widget subclasses optionally define an OnChange event to tell their owning panel when they've altered state




	/// <summary>
	/// Delegate for supporting OnChange events. 
	/// 
	/// Any Widget subclass can implement the event using:
	/// 	public event ChangeEventHandler OnChange;
	///		private void SomeMethod()
	///		{
	///			if (somethingIsTrue)
	///				if (OnChange!=null)				// IF there's at least one handler hooked in
	///					OnChange(this, value);		// trigger the event (value is passed as an Object and must be recast by handler)
	///		}
	/// 
	/// Panels hook into this using:
	///		Widget w = new Widget();
	///		w.OnChange += new ChangeEventHandler(MyEventHandler)
	/// 
	/// And implement the handler as:
	///		public void MyEventHandler(Widget sender, Object value)
	///		{
	///			float thisWidgetTypeHasAFloatValue = (float)value;
	///		}
	/// The handler could be a member of the panel class or a member of the owning camera ship
	///		w.OnChange += new ChangeEventHandler(owner.MyEventHandler)
	/// 
	/// </summary>
	public delegate void ChangeEventHandler(Widget sender, Object value);





	/// <summary>
	/// Base class for all panel widgets and sections of backdrop.
	/// Backdrop elements can be made from this class. Other widgets are subclassed.
	/// </summary>
	public class Widget
	{
		protected string textureName = null;						// texture filename
		protected Texture texture = null;							// the texture (possibly multi frame)

		protected Bitmap bitmap = null;								// if writeable, this is the bitmap to write to
		protected Bitmap backdrop = null;							// if writeable, this is the original bitmap

		protected Graphics graphics = null;							// The graphics object for this drawing session

		protected bool writeable = false;							// true if the widget can be written/drawn to
		protected bool enabled = true;								// display status

		protected int baseWidth, baseHeight;						// size of ONE FRAME in the original 1024x768 coordinate space
		protected int baseX, baseY;									// screen position of the widget in original coordinates

		protected Rectangle srcRect;								// rectangle within the bitmap of the current frame
		protected SizeF destSize;									// size of this rectangle in destination coords
		protected PointF destPosn;									// position of the widget in destination coords

		private float frame;										// current frame# (as a float so that we can add a fractional amount of animation)

		/// <summary>
		/// When we modify frame we must update the displayed rectangle
		/// </summary>
		public float Frame
		{
			get { return frame; }
			set
			{
				frame = value;
				// Recalculate the source rectangle (assumes all frames are in a row)
				srcRect = new Rectangle((int)frame * baseWidth, 0, baseWidth, baseHeight);
			}
		}

		/// <summary>
		/// Base ctor
		/// </summary>
		public Widget()
		{
		}

		/// <summary>
		/// Construct a basic widget
		/// </summary>
		/// <param name="textureName">texture file (no suffix)</param>
		/// <param name="sourceWidth">width of ONE FRAME in original (1024x768) coordinates</param>
		/// <param name="sourceHeight">height of ONE FRAME in original (1024x768) coordinates</param>
		/// <param name="originalX">Screen position in original (1024x768) coordinates</param>
		/// <param name="originalY">Screen position in original (1024x768) coordinates</param>
		public Widget(string textureName, bool writeable,
					  int sourceWidth, int sourceHeight,
					  int originalX, int originalY)
		{
			// store extents etc.
			this.textureName = textureName;
			this.baseWidth = sourceWidth;
			this.baseHeight = sourceHeight;
			this.baseX = originalX;
			this.baseY = originalY;
			this.writeable = writeable;

			// Calculate display extents
			CalculateSize();

			// Set initial frame # and rectangle
			Frame = 0;

			// if we're writeable, we must load the backdrop and create a texture from it now.
			// Otherwise the texture will get loaded later during the call to Enter()
			if (writeable == true)
			{
				backdrop = (Bitmap)Bitmap.FromFile(FileResource.Fsp("textures", textureName + ".png"));
				BeginDrawing();
				EndDrawing();
			}

		}

		/// <summary>
		/// Load a texture from the main textures folder
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		protected Texture LoadTexture(string name)
		{
            try
            {
                return LoadTextureFromPath(FileResource.Fsp("textures", name + ".png"));
            }
            catch (Exception e)
            {
                throw new SDKException("Failed to locate widget texture: " + name, e);
            }
		}

		/// <summary>
		/// Load a texture given a FULL pathname (Carousel widget might want to load images from e.g. celltype folders)
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		protected Texture LoadTextureFromPath(string path)
		{
			return TextureLoader.FromFile(Engine.Device,
											path,
											0, 0,							// width/height
											1,								// mip levels
											Usage.None,						// usage
											Format.A8R8G8B8,				// argb
											Pool.Managed,					// pool
											Filter.None,					// filter
											Filter.None,    		    	// mip filter
			                            	unchecked((int)0xFFFF00FF));	// chroma color (purple)
		}

		/// <summary>
		/// We have become visible. 
		/// Subclasses can override this to reload any resources they dumped while asleep.
        /// They should also trigger any event handler to inform the system of their current state.
		/// </summary>
		/// <remarks>
		/// The default behaviour is to dump/reload the .texture, 
		/// since base class Widgets are likely to be part of the backdrop and hence quite large. 
		/// If a subclass mustn't lose its .texture (e.g. because it has been written onto OR MIGHT GET written onto while invisible), it 
		/// should override this method and not call the base method.
		/// </remarks>
		public virtual void Enter()
		{
			// If we're not writeable, load the texture
			if (writeable == false)
			{
				texture = LoadTexture(textureName);
			}
		}

		/// <summary>
		/// We are about to become invisible - dump resources without losing state.
		/// Subclasses can override this to dispose of any memory-hogging resources such as bitmaps
		/// that they don't need and which can be reloaded next time our panel becomes visible.
		/// </summary>
		public virtual void Leave()
		{
			// If we're not a writeable widget we can safely dump the .texture
			if (writeable == false)
			{
				if (texture != null)
				{
					texture.Dispose();
					texture = null;
				}
			}
		}

		/// <summary>
		/// Draw this gauge as a sprite (using current animation frame).
		/// Called every video frame
		/// Subclasses should override this to do any animation, then call this base method to draw the result
		/// </summary>
		/// <param name="sprite"></param>
		public virtual void Draw(Sprite sprite)
		{
			// By default we use an UNROTATED sprite draw command. Widget classes should use a different 
			// form of Draw2D() if they need a certain rotation (e.g. rotating knobs) 
			// NOTE: Could change the colour here for FX such as flashes
			if (enabled)
				sprite.Draw2D(texture, srcRect, destSize, destPosn, Color.White);
		}

		/// <summary>
		/// (Re)calculate the display size and location of a gauge after a screen size change.
        /// Override to add any subclass adjustments
		/// </summary>
		public virtual void CalculateSize()
		{
			// Size of viewport
			float screenWidth = (float)Camera.PanelViewport.Width;
			float screenHeight = (float)Camera.PanelViewport.Height;



			destSize = new SizeF(baseWidth * screenWidth / 1024f,			// size of a frame in dest coords
								baseHeight * screenHeight / 768f);
			destPosn = new PointF(baseX * screenWidth / 1024f + Camera.PanelViewport.X,				// position of the sprite in dest coords
									baseY * screenHeight / 768f + Camera.PanelViewport.Y);
		}

        /// <summary>
        /// Convert a point in base 1024x768 coordinates into the current display coordinate space
        /// </summary>
        /// <param name="orig">The point in the original bitmap's 1024x768 frame</param>
        /// <returns>The point in screen coordinates</returns>
        public static PointF ToScreenCoords(PointF orig)
        {
            return new PointF(orig.X * (float)Camera.PanelViewport.Width / 1024f + Camera.PanelViewport.X,
                              orig.Y * (float)Camera.PanelViewport.Height / 768f + Camera.PanelViewport.Y);
        }

        /// <summary>
        /// Convert a point in base 1024x768 coordinates into the current display coordinate space
        /// WITHOUT adding the panel's offset XY. This allows me to compute a position RELATIVE to another
        /// in destination coordinates, given an offset in base coordinates.
        /// E.g. if the centre of a knob is 16 pixels to the right of and below its origin in base coordinates,
        /// calling this method with ToScreenOffset(new PointF(16f,16f)) will tell me how far the centre is from the
        /// origin in screen coordinates.
        /// </summary>
        /// <param name="orig">The point in the original bitmap's 1024x768 frame</param>
        /// <returns>The point in screen coordinates</returns>
        public static PointF ToScreenOffset(PointF orig)
        {
            return new PointF(orig.X * (float)Camera.PanelViewport.Width / 1024f,
                              orig.Y * (float)Camera.PanelViewport.Height / 768f);
        }

        /// <summary>
		/// Convert a point in display coordinates into the base 1024x768 coordinate space
		/// </summary>
		/// <param name="orig">The point in screen coordinates</param>
		/// <returns>The point in base coordinates</returns>
		public static PointF ToBaseCoords(PointF screen)
		{
            return new PointF((screen.X - Camera.PanelViewport.X) / (float)Camera.PanelViewport.Width * 1024f,
                              (screen.Y - Camera.PanelViewport.Y) / (float)Camera.PanelViewport.Height * 768f);
		}

		/// <summary>
		/// Handle a left button mouse click.
		/// In the base Widget class this does nothing but is present so that clicks can be passed to any widget.
		/// Override this in subclasses to detect hotspot clicks
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public virtual bool MouseClick(float x, float y)
		{
			return false;
		}

		/// <summary>
		/// Handle a left button mouse release.
		/// In the base Widget class this does nothing but is present so that releases can be passed to any widget
		/// that has just processed a left click. Allows e.g. analogue knobs to turn while the mouse is down
		/// </summary>
		public virtual bool MouseRelease()
		{
			return false;
		}

		/// <summary>
		/// Return true if the given mouse cursor is over this widget (used to establish which widget should process the click)
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public bool HitTest(float x, float y)
		{
			if ((x >= destPosn.X) && (x <= destPosn.X + destSize.Width)
				&& (y >= destPosn.Y) && (y <= destPosn.Y + destSize.Height))
				return true;

			return false;
		}

		/// <summary>
		/// A key has been pressed that neither the UI nor the camera ship nor the panel knows how to handle.
		/// Subclasses can override this to support one or more hotkeys, complete with animation as if the widget was clicked.
		/// If we can handle it, return true. 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public virtual bool KeyPress(Keys key)
		{
			return false;
		}

		/// <summary>
		/// A key has been released.
		/// Subclasses can override this to support one or more hotkeys, e.g. to release a button or stop turning a knob.
		/// If we can handle it, return true. 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public virtual bool KeyRelease(Keys key)
		{
			return false;
		}


		/// <summary>
		/// Call this before starting to draw new text/graphics onto a writeable widget
		/// </summary>
		/// <returns>The graphics object to be written to</returns>
		public Graphics BeginDrawing()
		{
			bitmap = (Bitmap)backdrop.Clone();													// get a 'clean' copy of the bitmap

			graphics = Graphics.FromImage(bitmap);												// create a Graphics object for drawing
			graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;		// MUST antialias or text leaves black marks!

			return graphics;																	// return the graphics obj so that caller can draw
		}

		/// <summary>
		/// Call this after updating the graphics on a writeable widget
		/// </summary>
		public void EndDrawing()
		{
			if (texture != null)
				texture.Dispose();
			////texture = Texture.FromBitmap(Engine.Device, bitmap, Usage.None, Pool.Managed);		// create a new texture from the bitmap for sprite
			texture = ThreeDee.FastTextureFromBitmap(Engine.Device, bitmap, Usage.None, Pool.Managed);		// create a new texture from the bitmap for sprite
			graphics.Dispose();
			bitmap.Dispose();
		}











	}



	




    // Remmed out because I probably don't need it...

    ///// <summary>
    ///// A Widget that acts as a slowly animating switch.
    ///// A click anywhere in the widget's bitmap will toggle the switch's state.
    ///// There are 5 animation frames - off, nearly off, mid-way, nearly on, on.
    ///// (Note, switch can be toggle or rotary, and could have an ON light)
    ///// The OnChange event supplies a BOOL value for off/on
    ///// </summary>
    //class SlowSwitch : Widget
    //{
    //    // We implement an OnChange event when the switch changes state
    //    public event ChangeEventHandler OnChange;

    //    private const int NUMFRAMES = 5;					// # frames
    //    private bool state = false;							// current state
    //    private bool target = false;						// target state
    //    private Keys hotkey = Keys.None;					// optional hotkey - toggles state


    //    public SlowSwitch(string textureName,
    //                  int sourceWidth, int sourceHeight,
    //                  int originalX, int originalY, Keys hotkey)
    //        : base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
    //    {
    //        this.hotkey = hotkey;
    //    }

    //    /// <summary>
    //    /// Animate and draw
    //    /// </summary>
    //    /// <param name="sprite"></param>
    //    public override void Draw(Sprite sprite)
    //    {
    //        // Animate the switch
    //        if (state != target)														// if we're in the process of moving...
    //        {
    //            if (target == true)														// switching on?
    //            {
    //                Frame += Scene.ElapsedTime * 20f;									// increase fractional frame#
    //                if (Frame >= NUMFRAMES - 1)											// flip state if we've got to last frame
    //                {
    //                    Frame = NUMFRAMES - 1;
    //                    state = true;
    //                    if (OnChange!=null)												// IF there's at least one handler hooked in
    //                        OnChange(this, state);										// trigger an OnChange event
    //                }
    //            }
    //            else
    //            {
    //                Frame -= Scene.ElapsedTime * 20f;									// switching off? Decrease frame#
    //                if (Frame <= 0)														// flip state if we've got to last frame
    //                {
    //                    Frame = 0;
    //                    state = false;
    //                    if (OnChange != null)											// IF there's at least one handler hooked in
    //                        OnChange(this, state);										// trigger an OnChange event
    //                }
    //            }
    //        }

    //        // call base method to render the sprite
    //        base.Draw(sprite);
    //    }

    //    /// <summary>
    //    /// Read or set explicit value (with animation)
    //    /// </summary>
    //    public bool Value
    //    {
    //        get { return this.state; }
    //        set { this.target = value; }											// only set target, so that we get an animation
    //    }

    //    /// <summary>
    //    /// Toggle the switch (an OnChange event will be thrown later, when the animation completes)
    //    /// </summary>
    //    public void Toggle()
    //    {
    //        if (target == true)
    //            target = false;
    //        else
    //            target = true;
    //    }

    //    /// <summary>
    //    /// Handle a left button mouse click.
    //    /// If we're ANYWHERE inside the widget, toggle the switch
    //    /// </summary>
    //    /// <param name="x"></param>
    //    /// <param name="y"></param>
    //    public override bool MouseClick(float x, float y)
    //    {
    //        Toggle();
    //        return true;
    //    }

    //    /// <summary>
    //    /// Hotkey support 
    //    /// </summary>
    //    /// <param name="key">The Forms.Keys key that was pressed</param>
    //    /// <returns>true if the key was handled</returns>
    //    public override bool KeyPress(Keys key)
    //    {
    //        if (key == hotkey)
    //        {
    //            Toggle();
    //            return true;
    //        }
    //        return false;
    //    }


    //}

    /// <summary>
    /// A Widget that acts as a switch.
    /// A click anywhere in the widget's bitmap will toggle the switch's state.
    /// There are 5 animation frames - off, nearly off, mid-way, nearly on, on.
    /// (Note, switch can be toggle or rotary, and could have an ON light)
    /// The OnChange event supplies a BOOL value for off/on
    /// </summary>
    class Switch : Widget
    {
        // We implement an OnChange event when the switch changes state
        public event ChangeEventHandler OnChange;

        private bool state = false;							// current state
        private Keys hotkey = Keys.None;					// optional hotkey - toggles state


        public Switch(string textureName,
                      int sourceWidth, int sourceHeight,
                      int originalX, int originalY, Keys hotkey)
            : base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
        {
            this.hotkey = hotkey;
        }

        /// <summary>
        /// We have become visible. 
        /// Subclasses can override this to reload any resources they dumped while asleep.
        /// They should also trigger any event handler to inform the system of their current state.
        /// </summary>
        public override void Enter()
        {
            base.Enter();                                                   // We're not writeable, so call our base method to reload resources

            if (OnChange != null)										    // If there's at least one handler hooked in
                OnChange(this, state);										// trigger an OnChange event to inform the system of our initial state
        }


        /// <summary>
        /// Animate and draw
        /// </summary>
        /// <param name="sprite"></param>
        public override void Draw(Sprite sprite)
        {
            // call base method to render the sprite
            base.Draw(sprite);
        }

        /// <summary>
        /// Read or set explicit value (with animation)
        /// </summary>
        public bool Value
        {
            get { return this.state; }
            set { this.state = value; }	
        }

        /// <summary>
        /// Toggle the switch (an OnChange event will be thrown later, when the animation completes)
        /// </summary>
        public void Toggle()
        {
            state = !state;
            Frame = (state) ? 1 : 0;                                        // set correct frame
            if (OnChange != null)										    // IF there's at least one handler hooked in
                OnChange(this, state);										// trigger an OnChange event
        }

        /// <summary>
        /// Handle a left button mouse click.
        /// If we're ANYWHERE inside the widget, toggle the switch
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public override bool MouseClick(float x, float y)
        {
            Toggle();
            return true;
        }

        /// <summary>
        /// Hotkey support 
        /// </summary>
        /// <param name="key">The Forms.Keys key that was pressed</param>
        /// <returns>true if the key was handled</returns>
        public override bool KeyPress(Keys key)
        {
            if (key == hotkey)
            {
                Toggle();
                return true;
            }
            return false;
        }


    }




	/// <summary>
	/// A Widget that acts as a rotary switch.
	/// A click on the right hand side increments the switch's state.
	/// The number of states is defined on creation
	/// The OnChange event supplies an INT value for the switch state
    /// 
    /// See Wheel class for info on the graphics requirements
    /// 
    /// </summary>
	class MultiSwitch : Widget
	{
		// We implement an OnChange event when the switch changes state
		public event ChangeEventHandler OnChange;

		private int state = 0;								// switch position
		private int numPositions = 0;						// number of positions the switch can be in
		private float midState = 0;							// what state the switch would be in when vertical (#.5 if an even # states)
		private float halfAngle = 1;						// the maximum angle the knob can be either side of vertical
		private float increment = 0.1f;						// the angle to move with each click
		private PointF centre;								// the rotation centre (centre of sprite)
		private Keys hotkeyDecrease = Keys.None;			// hotkey to decrease value
		private Keys hotkeyIncrease = Keys.None;			// hotkey to increase value


		public MultiSwitch(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY,
                          float centreX, float centreY,
                          Keys hotkeyIncrease, Keys hotkeyDecrease,
						  int numPositions, float halfAngle)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.hotkeyIncrease = hotkeyIncrease;
			this.hotkeyDecrease = hotkeyDecrease;
			this.numPositions = numPositions;						// record number of positions
			this.halfAngle = halfAngle;								// and angular range either side of mid-point

			midState = (int)(((float)numPositions - 1.0f) / 2.0f);	// this is the state we'd be in at the mid-point
			increment = halfAngle / midState;						// so this is the angular change for each click

            centre.X = centreX;
            centre.Y = centreY;
        }

        /// <summary>
        /// We have become visible. 
        /// Subclasses can override this to reload any resources they dumped while asleep.
        /// They should also trigger any event handler to inform the system of their current state.
        /// </summary>
        public override void Enter()
        {
            base.Enter();                                                   // We're not writeable, so call our base method to reload resources

            if (OnChange != null)										    // If there's at least one handler hooked in
                OnChange(this, state);										// trigger an OnChange event to inform the system of our initial state
        }

		/// <summary>
		/// Read or set value
		/// </summary>
		public int Value
		{
			get { return state; }
			set { state = value; }
		}

		/// <summary>
		/// Animate and draw
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
			// calc angle from switch position
			float angle = ((float)state - midState) * increment;

            // Draw the rotated DECAL (frame0)...
            // rotating form of Draw2D() positions the CENTRE at xy instead of the top-left pixel,
            // So I must calculate the centre position from the top-left posn (in destination coordinates)
            PointF destCentre = ToScreenOffset(centre);
            PointF posn = new PointF(destPosn.X + destCentre.X, destPosn.Y + destCentre.Y);
            sprite.Draw2D(texture, srcRect, destSize, centre, angle, posn, Color.White);

            // Draw the LUMINOSITY image (frame 1)...
            Rectangle lumRect = new Rectangle(baseWidth, 0, baseWidth, baseHeight);
            sprite.Draw2D(texture, lumRect, destSize, destPosn, Color.White);
        }

		/// <summary>
		/// Handle a left button mouse click in left or right side of bitmap
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override bool MouseClick(float x, float y)
		{
			if ((x >= destPosn.X) && (x <= destPosn.X + destSize.Width / 2f)
				&& (y >= destPosn.Y) && (y <= destPosn.Y + destSize.Height))
			{
				if (--state < 0)
					state = 0;
			}
			else
			{
				if (++state >= numPositions)
					state = numPositions - 1;
			}
			if (OnChange != null)												// IF there's at least one handler hooked in
				OnChange(this, state);											// trigger an OnChange event
			return true;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			if (key == hotkeyIncrease)
			{
				return MouseClick(destPosn.X, destPosn.Y);						// emulate a click in left side
			}
			else if (key == hotkeyDecrease)
			{
				return MouseClick(destPosn.X + destSize.Width, destPosn.Y);		// emulate a click in right side
			}
			return false;
		}


	}



	/// <summary>
	/// A Widget that acts as a rotary analogue wheel. This is a Knob that can turn more than 360 degrees,
	/// like the 'digital' potentiometers you find on radios.
	/// It's useful for rotating cells, etc. because it doesn't need to be set to a fixed orientation.
	/// HOLDING DOWN the mouse on the right hand side turns the knob clockwise.
	/// It returns a value that shows how far the wheel has rotated since the last OnChange event
	/// (i.e. it returns RELATIVE motion not absolute).
	/// The OnChange event supplies a FLOAT value for delta
    /// 
    /// Graphically the sprite consists of two frames. Frame 0 is the decal - just the colours, with no shading. This frame will contain any colour data that
    /// needs to rotate with the knob. Frame 1 is a plain black image with a mask. The mask's transparency levels all more or less black to show through
    /// and form the luminosity image - the shading, shadows and highlights that should stay static as the knob turns. This image will be drawn over the top
    /// of the decal to produce the full 3D effect. It is created from a 3D render that has been turned to greyscale (the colours will be in the decal).
    /// 
    /// To create the image:
    ///    • Render the knob complete with shadows (avoid vital highlights)
    ///    • Desaturate the bitmap 
    ///    • Mask out the fully transparent background by painting it white
    ///    • Double the width of the image, placing the bitmap in the right half (frame(1))
    ///    • Create a mask of black on the left half, through which the visible part of the decal will show
    ///    • Save to disk as the "luminosity layer"

    ///    • Draw a "decal layer" containing just the colours of the knob - no shading
    ///    • Add a pointer mark to the decal to show which way the knob is pointing. Add any ribbing or other rotating parts.

    ///    • Expand the decal image to twice the width, with a black background
    ///    • Press ctrl-K to edit the mask
    ///    • Use flood fill in bitmap mode to fill the mask using the luminosity layer image
    ///    • The image should now have the decal on the left,  showing through the hole in the mask, and the luminosity image on the right as mask transparency levels.
    ///    • Save to disk as .PNG with "use mask" for transparency

    /// 
	/// </summary>
	class Wheel : Widget
	{
		// We implement an OnChange event every frame while the switch is changing state
		public event ChangeEventHandler OnChange;

		protected float state = 0;								// wheel rotation from +/-PI
		protected float delta = 0;								// amount it has changed (+/-radians)
        protected PointF centre;								// the rotation centre (centre of sprite) in source coords
        protected int movement = 0;								// -1 the knob is going anti-clockwise, 1 = clockwise, 0 = not moving
		protected Keys hotkeyInc = Keys.None;					// optional hotkey - turn clockwise
		protected Keys hotkeyDec = Keys.None;					// optional hotkey - turn anticlockwise

		/// <summary>
		/// 
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
        /// <param name="centreX">Centre of rotation</param>
        /// <param name="centreY">Centre of rotation</param>
        /// <param name="halfAngle">the maximum angle the knob can be either side of vertical</param>
		public Wheel(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY,
                          float centreX, float centreY, 
						  Keys hotkeyInc, Keys hotkeyDec)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.hotkeyInc = hotkeyInc;
			this.hotkeyDec = hotkeyDec;

            centre.X = centreX;
            centre.Y = centreY;
		}


		/// <summary>
		/// Read latest change of angle
		/// </summary>
		public float Value
		{
			get { return delta; }
		}

		/// <summary>
		/// Animate and draw
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
			// move knob if the mouse is down
			if (movement != 0)
			{
				float oldState = state;
				state += movement * Scene.ElapsedTime * 1f;
				delta = state - oldState;										// we return the delta
				state %= (float)Math.PI * 2;									// keep orientation in range +/-2PI
				if (OnChange != null)											// IF there's at least one handler hooked in
					OnChange(this, delta);										// trigger an OnChange event
			}

            // Draw the rotated DECAL (frame0)...
			// rotating form of Draw2D() positions the CENTRE at xy instead of the top-left pixel,
			// So I must calculate the centre position from the top-left posn (in destination coordinates)
            PointF destCentre = ToScreenOffset(centre);
			PointF posn = new PointF(destPosn.X + destCentre.X, destPosn.Y + destCentre.Y);
			sprite.Draw2D(texture, srcRect, destSize, centre, state, posn, Color.White);

            // Draw the LUMINOSITY image (frame 1)...
            Rectangle lumRect = new Rectangle(baseWidth, 0, baseWidth, baseHeight);
            sprite.Draw2D(texture, lumRect, destSize, destPosn, Color.White);

		}

		/// <summary>
		/// Handle a left button mouse click in left or right side of bitmap
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override bool MouseClick(float x, float y)
		{
			if ((x >= destPosn.X) && (x <= destPosn.X + destSize.Width / 2f)
				&& (y >= destPosn.Y) && (y <= destPosn.Y + destSize.Height))
			{
				movement = -1;
			}
			else
			{
				movement = 1;
			}

			return true;
		}

		/// <summary>
		/// Handle a left button mouse release - stop any rotation
		/// </summary>
		public override bool MouseRelease()
		{
			movement = 0;
			return true;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			if (key == hotkeyInc)
			{
				movement = 1;
				return true;
			}
			else if (key == hotkeyDec)
			{
				movement = -1;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyRelease(Keys key)
		{
			if ((key == hotkeyInc) || (key == hotkeyDec))
			{
				movement = 0;
				return true;
			}
			return false;
		}


	}

	/// <summary>
	/// A Widget that acts as a rotary ANALOGUE knob.
	/// HOLDING DOWN the mouse on the right hand side turns the knob clockwise.
	/// The OnChange event supplies a FLOAT value for knob position
    /// 
    /// See Wheel class for info on the graphics requirements
    /// 
	/// </summary>
	class Knob : Wheel		// NOTE: derived from WHEEL
	{
		// We implement an OnChange event every frame while the switch is changing state
		public new event ChangeEventHandler OnChange;

		private float halfAngle = 1;						// the maximum angle the knob can be either side of vertical

		/// <summary>
		/// 
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		/// <param name="halfAngle">the maximum angle the knob can be either side of vertical</param>
		public Knob(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY,
                          float centreX, float centreY,
                          Keys hotkeyInc, Keys hotkeyDec, float halfAngle)
			: base(textureName, sourceWidth, sourceHeight, originalX, originalY, centreX, centreY, hotkeyInc, hotkeyDec)
		{
			this.halfAngle = halfAngle;								// record angular range either side of mid-point
		}

        /// <summary>
        /// We have become visible. 
        /// Subclasses can override this to reload any resources they dumped while asleep.
        /// They should also trigger any event handler to inform the system of their current state.
        /// </summary>
        public override void Enter()
        {
            base.Enter();                                                   // We're not writeable, so call our base method to reload resources

            if (OnChange != null)										    // If there's at least one handler hooked in
                OnChange(this, state);										// trigger an OnChange event to inform the system of our initial state
        }


		/// <summary>
		/// Read or set value (0 to 1)
		/// </summary>
		public new float Value
		{
			get { return state; }
			set { state = value; }
		}

		/// <summary>
		/// Animate and draw
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
			// move knob if the mouse is down
			if (movement != 0)
			{
				state += movement * Scene.ElapsedTime * 0.5f;
				if (state < 0)
					state = 0;
				else if (state > 1)
					state = 1;
				if (OnChange != null)											// IF there's at least one handler hooked in
					OnChange(this, state);										// trigger an OnChange event
			}

			// calc angle from switch position
			float angle = ((float)state - 0.5f) * (halfAngle * 2.0f);

            // Draw the rotated DECAL (frame0)...
            // rotating form of Draw2D() positions the CENTRE at xy instead of the top-left pixel,
            // So I must calculate the centre position from the top-left posn (in destination coordinates)
            PointF destCentre = ToScreenOffset(centre);
            PointF posn = new PointF(destPosn.X + destCentre.X, destPosn.Y + destCentre.Y);
            sprite.Draw2D(texture, srcRect, destSize, centre, angle, posn, Color.White);

            // Draw the LUMINOSITY image (frame 1)...
            Rectangle lumRect = new Rectangle(baseWidth, 0, baseWidth, baseHeight);
            sprite.Draw2D(texture, lumRect, destSize, destPosn, Color.White);
        }


	}






	/// <summary>
	/// A Widget that acts as a pushbutton.
	/// Pressing or releasing the button causes an event
	/// Sprite has two frames: up/down. Value is true or false.
	/// Optionally, the button can be set to auto-repeat. It does this by firing OnChange events (with Value==true) at intervals
	/// The OnChange event supplies a BOOL value for off/on
	/// </summary>
	class Button : Widget
	{
		// We implement an OnChange event when the button is pressed / released
		public event ChangeEventHandler OnChange;

		private bool repeat = true;									// true if this button auto-repeats
		private float timer = 0;									// controls timing of repeats
		private Keys hotkey = Keys.None;							// optional hotkey - press/release the button

		/// <summary>
		/// 
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		/// <param name="repeat">true if the button should auto-repeat</param>
		public Button(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY, Keys hotkey, bool repeat)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.hotkey = hotkey;
			this.repeat = repeat;
		}

		/// <summary>
		/// Read the button state that triggered the event
		/// </summary>
		public bool Value
		{
			get { return (Frame == 1) ? true : false; }
		}

		/// <summary>
		/// Draw and do any auto-repeat
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
			// If we auto-repeat and the button is down, trigger an event at intervals
			if ((Frame==1)&&(repeat == true))
			{
				timer -= Scene.ElapsedTime;
				if (timer <= 0)
				{
					timer = 0.25f;											// subsequent repeat delay
					if (OnChange != null)									// Repeat the OnChange event
						OnChange(this, true);	
				}
			}

			// call base method to render the sprite
			base.Draw(sprite);
		}


		/// <summary>
		/// Handle a left button mouse click
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override bool MouseClick(float x, float y)
		{
			Frame = 1;
			if (OnChange != null)											// IF there's at least one handler hooked in
				OnChange(this, true);										// trigger an OnChange event
			if (repeat == true)												// if we auto-repeat, set an initial delay
				timer = 0.8f;
			return true;
		}

		/// <summary>
		/// Handle a left button mouse release
		/// </summary>
		public override bool MouseRelease()
		{
			Frame = 0;
			timer = 0;
			if (OnChange != null)											// IF there's at least one handler hooked in
				OnChange(this, false);										// trigger an OnChange event
			return true;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			if (key == hotkey)
			{
				return MouseClick(0, 0);
			}
			return false;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyRelease(Keys key)
		{
			if (key == hotkey)
			{
				return MouseRelease();
			}
			return false;
		}

	}

	/// <summary>
	/// A pushbutton that changes state with each push, wrapping back to state zero after passing through
	/// the other states. The state is made visible by cycling through the animation frames
	/// Pressing or releasing the button causes an event and returns the state
	/// </summary>
	class CycleButton : Widget
	{
		// We implement an OnChange event when the button is pressed / released
		public event ChangeEventHandler OnChange;

		private int numStates = 2;									// set this to the number of frames in the bitmap
		private Keys hotkey = Keys.None;							// optional hotkey - press/release the button

		/// <summary>
		/// 
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		/// <param name="repeat">true if the button should auto-repeat</param>
		public CycleButton(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY, Keys hotkey, int numStates)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.hotkey = hotkey;
			this.numStates = numStates;
		}

		/// <summary>
		/// Read the button state that triggered the event
		/// </summary>
		public int Value
		{
			get { return (int)Frame; }
			set { Frame = (float)value; }
		}

		/// <summary>
		/// Handle a left button mouse click
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override bool MouseClick(float x, float y)
		{
			Frame++;
			Frame %= numStates;
			if (OnChange != null)											// IF there's at least one handler hooked in
				OnChange(this, (int)Frame);									// trigger an OnChange event
			return true;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			if (key == hotkey)
			{
				return MouseClick(0, 0);
			}
			return false;
		}

	}



	/// <summary>
	/// A Widget that acts as a pushbutton with a safety cover.
	/// Clicking anywhere on a covered button raises the cover.
	/// Clicking on the top n pixels of the uncovered button covers it again (or it will close after a couple of seconds)
	/// Clicking below this point will press the button. The cover will close again after a few seconds.
	/// Pressing or releasing the button causes an event
	/// Sprite has three frames: covered/up/down. Value is true or false.
	/// The OnChange event supplies a BOOL value for off/on
	/// </summary>
	class SafetyButton : Widget
	{
		// We implement an OnChange event when the button is pressed/released
		public event ChangeEventHandler OnChange;

		private float buttonTop = 0;						// marks the line between the cover (close) and the button (press)
		private float timer = 0;							// causes the cover to close after button released
		private Keys hotkey = Keys.None;					// optional hotkey (open, press/release)

		/// <summary>
		/// Constr
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		/// <param name="buttonTop">Clicking within this top FRACTION of the sprite closes the lid. Clicking below presses the button</param>
		public SafetyButton(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY, Keys hotkey, float buttonTop)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.hotkey = hotkey;
			this.buttonTop = buttonTop;
		}

		/// <summary>
		/// Read the button state that triggered the event
		/// </summary>
		public bool Value
		{
			get { return (Frame == 2) ? true : false; }
		}

		/// <summary>
		/// Animate and draw
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
			// If the cover is open, close it when the timer times out
			if (Frame == 1)
			{
				timer -= Scene.ElapsedTime;
				if (timer <= 0)
				{
					timer = 0;
					Frame = 0;
				}
			}

			// call base method to render the sprite
			base.Draw(sprite);
		}

		/// <summary>
		/// Handle a left button mouse click
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override bool MouseClick(float x, float y)
		{
			// If the cover is closed, open it
			if (Frame == 0)
			{
				Frame = 1;
				timer = 2.0f;												// cover closing time
				return true;
			}

			// If the cover is open and the click is above the line, close the cover again
			if (y <= destPosn.Y + destSize.Height * buttonTop)
			{
				Frame = 0;
				timer = 0;
				return true;
			}

			// Otherwise, press the button
			Frame = 2;
			if (OnChange != null)											// IF there's at least one handler hooked in
				OnChange(this, true);										// trigger an OnChange event
			return true;
		}

		/// <summary>
		/// Handle a left button mouse release
		/// </summary>
		public override bool MouseRelease()
		{
			// Only applies if we were pressing the button, not the cover
			if (Frame == 2)
			{
				Frame = 1;
				timer = 2.0f;													// cover will automatically close after a delay (or can be clicked)
				if (OnChange != null)											// IF there's at least one handler hooked in
					OnChange(this, false);										// trigger an OnChange event
			}
			return true;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			if (key == hotkey)
			{
				return MouseClick(0, destPosn.Y + destSize.Height);				// 1st press opens cover, 2nd presses btn
			}
			return false;
		}

		/// <summary>
		/// Hotkey support 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyRelease(Keys key)
		{
			if (key == hotkey)
			{
				return MouseRelease();											// releases btn
			}
			return false;
		}


	}


	/// <summary>
	/// A Widget that acts as a simple lamp or other multi-state indicator.
	/// No mouse response. Program can set .Value and the correct frame (e.g. off or on) will be displayed.
	/// Value can be more than just 0 or 1
	/// </summary>
	class Lamp : Widget
	{

		public Lamp(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
		}

		/// <summary>
		/// Read or set value
		/// </summary>
		public float Value
		{
			get { return Frame; }
			set { Frame = value; }				// display rectangle will be automatically updated
		}
		
	}


	/// <summary>
	/// A Widget that creates a rotary ANALOGUE needle for meters.
    /// Frame 0 is the needle; frame 1 is the shadow
	/// No mouse action. Just set .Value to a float from 0 to 1
	/// </summary>
	class Meter : Widget
	{
		private float state = 0;							// needle position from 0 to 1 (0.5 is straight up)
        private float target = 0;                           // target needle position (.state slews to this)
        private bool slew = false;                          // true if needle should slew smoothly between values
		private float halfAngle = 1;						// the maximum angle the needle can be either side of vertical
		private PointF centre;								// the rotation centre
        private Rectangle shadowSrcRect = new Rectangle();  // frame containing the shadow
        private PointF shadowOffsetBase = new PointF();     // offset of shadow in base coords
        private PointF shadowOffsetDest = new PointF();     // offset of shadow in dest coords

		/// <summary>
		/// 
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		/// <param name="halfAngle"></param>
		/// <param name="centreX">Location of the pivot in source coordinates</param>
		/// <param name="centreY">Location of the pivot in source coordinates</param>
        /// <param name="shadowOffsetX">Location of shadow's centre relative to needle centre in base coords (0 if there's no shadow required)</param>
        /// <param name="shadowOffsetY">Location of shadow's centre relative to needle centre</param>
        /// <param name="slew">True if the needle should slew smoothly to new values</param>
		public Meter(string textureName,
						  int sourceWidth, int sourceHeight,
                          int originalX, int originalY, float halfAngle, int centreX, int centreY, int shadowOffsetX, int shadowOffsetY, bool slew)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.halfAngle = halfAngle;								        // record angular range either side of mid-point

            shadowOffsetBase = new PointF(shadowOffsetX, shadowOffsetY);    // record shadow offset in base coords
            shadowOffsetDest = ToScreenOffset(shadowOffsetBase);            // & dest coords (recalculated on window size change)

			centre.X = centreX;										        // sprite's centre of rotation
			centre.Y = centreY;

            this.slew = slew;

            // pre-calculate the frame rectangle for frame 1 (the shadow)
            shadowSrcRect = new Rectangle(baseWidth, 0, baseWidth, baseHeight);

		}

        /// <summary>
		/// (Re)calculate the display size and location of a gauge after a screen size change.
        /// Override to add any subclass adjustments
		/// </summary>
        public override void CalculateSize()
        {
            base.CalculateSize();

            // Recalc offset of shadow in dest coords
            shadowOffsetDest = ToScreenOffset(shadowOffsetBase);

        }


		/// <summary>
		/// Read or set value
		/// </summary>
		public float Value
		{
			get { return target; }
			set { target = value; }
		}

		/// <summary>
		/// Animate and draw
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
            // update state towards target
            if (slew)
                state = (state * 7 + target) / 8;
            else
                state = target;

			// calc angle from state
			float angle = ((float)state - 0.5f) * (halfAngle * 2.0f);

            // If there's a shadow, draw that first
            if (shadowOffsetBase.X != 0)      
            {
                PointF shadow = new PointF(destPosn.X + shadowOffsetDest.X + destSize.Width / 2, destPosn.Y + shadowOffsetDest.Y + destSize.Height / 2);
                sprite.Draw2D(texture, shadowSrcRect, destSize, centre, angle, shadow, Color.White);
            }

            // rotating form of Draw2D() positions the CENTRE at xy instead of the top-left pixel,
			// So I must calculate the centre position from the top-left posn (in destination coordinates)
            PointF posn = new PointF(destPosn.X + destSize.Width / 2, destPosn.Y + destSize.Height / 2);

            // Need to call a different Draw2D() so that we can rotate the sprite
            sprite.Draw2D(texture, srcRect, destSize, centre, angle, posn, Color.White);
        }



	}


	/// <summary>
	/// A Widget that displays a list and selection cursor.
	/// No mouse response - use separate buttons to move index. 
	/// The OnChange event supplies a STRING value representing the selected item (may be null)
	/// </summary>
	class Selector : Widget
	{
		// We implement an OnChange event when the selection changes
		public event ChangeEventHandler OnChange;

		private List<string> list = new List<string>();						// list of items
		private int index = 0;												// selection index
		private int start = 0;												// first displayed item
		private int numLines = 0;											// length of display in lines

		System.Drawing.Font font = null;									// text font
		SolidBrush brush = null;											// font colour
		SolidBrush fill = null;												// highlighter / fill

		/// <summary>
		/// Constr
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		/// <param name="font">The display font</param>
		/// <param name="brush">The brush for the font</param>
		/// <param name="fill">The brush for highlighting the selection</param>
		public Selector(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY,
						System.Drawing.Font font,
						SolidBrush brush, SolidBrush fill)
			: base(textureName, true, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.font = font;
			this.brush = brush;
			this.fill = fill;

			// Calculate how many items to display, given font and bitmap size
			numLines = sourceHeight / font.Height;


		}

		/// <summary>
		/// redraw the list after a list or selection change
		/// </summary>
		private void Refresh()
		{
			Graphics graphics = BeginDrawing();

			// Draw a cursor rectangle behind the selected item
			graphics.FillRectangle(fill, 8, (index - start) * font.Height + 4, baseWidth - 16, font.Height);

			// draw the text list
			for (int i = 0; i < numLines; i++)
			{
				string item = "";
				if (start + i < list.Count)
					item = list[start + i];

				graphics.DrawString(item, font, brush, 12, i * font.Height + 4);
			}

			EndDrawing();

			if (OnChange != null)											// IF there's at least one handler hooked in
				OnChange(this, Selection);									// trigger an OnChange event

		}


		/// <summary>
		/// Add an item to the list
		/// </summary>
		/// <param name="item"></param>
		public void Add(string item)
		{
			list.Add(item);
			Refresh();
		}

		/// <summary>
		/// Create the WHOLE list of items in one go from a list
		/// </summary>
		/// <param name="newList"></param>
		public void SetList(List<string> newList)
		{
			list.Clear();
			list.AddRange(newList);
			index = 0;
			start = 0;
			Refresh();
		}

		/// <summary>
		/// Create the WHOLE list of items in one go from an array
		/// </summary>
		/// <param name="items"></param>
		public void SetList(string[] items)
		{
			list.Clear();
			list.AddRange(items);
			index = 0;
			start = 0;
			Refresh();
		}

		/// <summary>
		/// Remove all entries from the list
		/// </summary>
		public void Clear()
		{
			list.Clear();
			index = 0;
			start = 0;
			Refresh();
		}

		/// <summary>
		/// Reset selection to start
		/// </summary>
		public void Reset()
		{
			index = 0;
			start = 0;
			Refresh();
		}

		/// <summary>
		/// Move down the list
		/// </summary>
		public void MoveDown()
		{
			if (++index >= list.Count)
				index--;
			if (index >= start + numLines)
				start++;
			Refresh();
		}

		/// <summary>
		/// Move up the list
		/// </summary>
		public void MoveUp()
		{
			if (--index < 0)
				index = 0;
			if (index < start)
				start--;
			Refresh();
		}

		/// <summary>
		/// Current selection index
		/// </summary>
		public int Index
		{ get { return index; } }

		/// <summary>
		/// Currently selected item text
		/// </summary>
		public string Selection
		{
			get
			{
				try
				{
					return list[index];
				}
				catch
				{
					return null;
				}
			}
		}



	}



	/// <summary>
	/// A Widget that displays one of several bitmaps loaded from disk
	/// (e.g. for displaying cell help text and diagrams). Widget is NOT writeable.
	/// All bitmaps should be the same size. the INITIAL texture must be in the textures folder, like all Widgets,
	/// but subsequent textures are loaded from a fully defined path. This is so that, for example, the Lab VDU
	/// can load and display bitmaps containing help on selected cell types, each of which will be stored in the
	/// relevant celltype subfolder.
	/// </summary>
	class Carousel : Widget
	{
		private bool blank = true;											// set false once we have a non-default texture

		/// <summary>
		/// Constr
		/// </summary>
		/// <param name="textureName">Initial image to display</param>
		public Carousel(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY)
			: base(textureName, false, sourceWidth, sourceHeight, originalX, originalY)
		{
		}

		/// <summary>
		/// Load a different bitmap, disposing of the previous one
		/// </summary>
		/// <param name="textureName">Name of the new texture to load</param>
		public void Load(string textureName)
		{
			blank = false;													// we no longer have the default blank texture
			this.textureName = textureName;									// ensure this one is reloaded next time panel is opened
			texture.Dispose();
			texture = LoadTextureFromPath(textureName);
		}

		/// <summary>
		/// We have become visible. We need to override the default because our texture
		/// might come from a different folder
		/// </summary>
		public override void Enter()
		{
			if (blank==true)												// if we still have the default texture, reload from \textures
				texture = LoadTexture(textureName);
			else
				texture = LoadTextureFromPath(textureName);					// otherwise load according to full path
		}




	}





	/// <summary>
	/// A Widget that acts as a progress bar or magic eye meter.
	/// No mouse response. Program can set .Value in range 0-1.
	/// </summary>
	class magicEye : Widget
	{
		private float level = 0;
		private int sideMargin, topMargin;
		private Brush barBrush;
		private bool vertical;

		/// <summary>
		/// Constr
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		/// <param name="sideMargin"># pixels from left & right for bar (in original coordinates)</param>
		/// <param name="topMargin"># pixels from top & bottom for bar</param>
		/// <param name="barColour">colour of bar</param>
		public magicEye(string textureName,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY,
						  int sideMargin, int topMargin,
						  Brush barBrush, bool vertical)
			: base(textureName, true, sourceWidth, sourceHeight, originalX, originalY)
		{
			this.sideMargin = sideMargin;
			this.topMargin = topMargin;
			this.barBrush = barBrush;
			this.vertical = vertical;
		}

		/// <summary>
		/// Read or set value
		/// </summary>
		public float Value
		{
			get { return level; }

			set
			{
				level = value;

				// Draw the changed bar onto the sprite
				PointF topLeft = new PointF(sideMargin, topMargin);
				PointF wh = new PointF(baseWidth - sideMargin - sideMargin, baseHeight - topMargin - topMargin);

				if (vertical == true)	// cvt bar width or height to fractional value (depending on orientation)
				{
					float max = wh.Y;
					wh.Y *= level;
					topLeft.Y += max - wh.Y;
				}
				else
				{
					wh.X *= level;
				}
				
				RectangleF bar = new RectangleF(topLeft, new SizeF(wh));

				Graphics g = BeginDrawing();
				g.FillRectangle(barBrush, bar);
				EndDrawing();
			}
		}


	}

	





	/// <summary>
	/// Abstract base class for multi-widget objects. Subclass this and add Widgets to the widgetList and
	/// they'll be handled like normal widgets but as a unit. The subclass can hold all the event functions
	/// etc.
	/// Used to create multiple small panels for editing wiring parameters, where the number of mini-panels
	/// varies with the cell type being edited
	/// 
	/// NOTE: When creating widgets, add their relative XY to the originalX and originalY variables supplied
	/// to the constructor, to give the widgets an abs location
	/// 
	/// </summary>
	abstract class Container : Widget
	{

		/// <summary> The Widgets held inside this container (created by subclasses of Container) </summary>
		protected List<Widget> widgetList = new List<Widget>();

		/// <summary> The widget that just received a mouse down event and should get the next mouse up event too </summary>
		protected Widget captureWidget = null;

		/// <summary>
		/// Constructor - texture is the background for the container
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="writeable"></param>
		/// <param name="sourceWidth"></param>
		/// <param name="sourceHeight"></param>
		/// <param name="originalX"></param>
		/// <param name="originalY"></param>
		public Container(string textureName, 
						  bool writeable,
						  int sourceWidth, int sourceHeight,
						  int originalX, int originalY)
			: base(textureName, writeable, sourceWidth, sourceHeight, originalX, originalY)
		{
			// Hook reset event to recalculate child widget extents
			Engine.Device.DeviceReset += new System.EventHandler(OnReset);
		}

		/// <summary>
		/// Device has been reset - calculate the sprite coordinates for the new screen size
		/// </summary>
		public void OnReset(object sender, EventArgs e)
		{
			foreach (Widget w in widgetList)
			{
				w.CalculateSize();
			}
		}

		/// <summary>
		/// We have become visible. 
		/// Tell our children.
		/// </summary>
		public override void Enter()
		{
			base.Enter();
			foreach (Widget w in widgetList)
			{
				w.Enter();
			}
		}

		/// <summary>
		/// We are about to become invisible - dump resources without losing state.
		/// Subclasses can override this to dispose of any memory-hogging resources such as bitmaps
		/// that they don't need and which can be reloaded next time our panel becomes visible.
		/// </summary>
		public override void Leave()
		{
			foreach (Widget w in widgetList)
			{
				w.Leave();
			}
			base.Leave();
		}

		/// <summary>
		/// Draw this gauge as a sprite (using current animation frame).
		/// Called every video frame
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
			if (enabled)
			{
				// Draw the background texture
				sprite.Draw2D(texture, srcRect, destSize, destPosn, Color.White);

				// Draw our children on top
				foreach (Widget w in widgetList)
				{
					w.Draw(sprite);
				}

			}
		}

		/// <summary>
		/// Handle a left button mouse click.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override bool MouseClick(float x, float y)
		{
			foreach (Widget w in widgetList)
			{
				if ((w.HitTest(x,y) == true) && (w.MouseClick(x, y) == true))
				{
					captureWidget = w;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Handle a left button mouse release.
		/// </summary>
		public override bool MouseRelease()
		{
			bool result = false;
			if (captureWidget != null)
				result = captureWidget.MouseRelease();
			captureWidget = null;
			return result;
		}

		/// <summary>
		/// A key has been pressed that neither the UI nor the camera ship nor the panel knows how to handle.
		/// Subclasses can override this to support one or more hotkeys, complete with animation as if the widget was clicked.
		/// If we can handle it, return true. 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			foreach (Widget w in widgetList)
			{
				if (w.KeyPress(key) == true)
					return true;
			}
			return false;
		}

		/// <summary>
		/// A key has been released.
		/// Subclasses can override this to support one or more hotkeys, e.g. to release a button or stop turning a knob.
		/// If we can handle it, return true. 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyRelease(Keys key)
		{
			foreach (Widget w in widgetList)
			{
				if (w.KeyRelease(key) == true)
					return true;
			}
			return false;
		}

	}



	/// <summary>
	/// Text entry/edit box. Click on box to enable cursor. All keypresses entering panel get sent here until Enter is
	/// pressed or another control is selected. Editbox.focus is set to the editing control that has received focus (or null)
	/// </summary>
	public class EditBox : Widget
	{
		// We implement an OnChange event when the ENTER key is pressed
		public event ChangeEventHandler OnChange;

		private static EditBox focus = null;									// The edit box (if any) that currently captures keypresses

		private string text = "";												// The current text string
        private string prompt = "";                                             // prompt string at start of box
		private System.Drawing.Brush brush = null;								// font colour
		private System.Drawing.Font font = null;								// text font
		private int textX, textY;												// position of text on backdrop (in 1024x768 coords)
		private static float cursorTime = 0;									// time at which we last flipped cursor
		private static bool cursorState = false;								// current cursor state

		public string Text
		{
			get { return text; }
			set { text = value; DrawText(); }
		}


		public EditBox(string textureName,
					  int sourceWidth, int sourceHeight,
					  int originalX, int originalY,
					  int textX, int textY,
					  System.Drawing.Font font,
					  System.Drawing.Brush brush,
                      string prompt)
			: base(textureName, true, sourceWidth, sourceHeight, originalX, originalY)

		{
			this.textX = textX;
			this.textY = textY;
			this.font = font;
			this.brush = brush;
            this.prompt = prompt;
		}

		/// <summary>
		/// Render current string onto backdrop, plus cursor if visible
		/// </summary>
		private void DrawText()
		{
			Graphics g = BeginDrawing();
            g.DrawString(prompt, font, brush, 0, textY);
            g.DrawString(text + ((cursorState == true) ? "|" : ""), font, brush, textX, textY);
            EndDrawing();
		}

		/// <summary>
		/// Set or test which EditBox has the focus (null to release)
		/// </summary>
		public static EditBox Focus
		{
			get { return EditBox.focus; }
			set 
			{
				cursorState = false;													// make sure cursor is off
				if (Focus != null)														// and redraw the old edit box with no cursor showing
					Focus.DrawText();
				EditBox.focus = value;
			}
		}


		/// <summary>
		/// Handle a left button mouse click.
		/// This gives the control focus, thus diverting all keypresses here until another control is clicked
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override bool MouseClick(float x, float y)
		{
			Focus = this;																// tell the UI we are to capture all keypresses
			return true;
		}

		/// <summary>
		/// A key has been pressed that neither the UI nor the camera ship nor the panel knows how to handle.
		/// Subclasses can override this to support one or more hotkeys, complete with animation as if the widget was clicked.
		/// If we can handle it, return true. 
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			// If we're receiving a keypress and we DON'T have the focus, discard it - we're being asked to respond to a hotkey, not typing
			if (Focus != this)
				return false;

			switch (key)
			{
				case Keys.Enter:														// On enter we lose focus and trigger any event to return string
					Focus = null;
					if (OnChange != null)
						OnChange(this, text);
					return true;

				case Keys.Back:
					if (text.Length > 0)
						text = text.Substring(0, text.Length - 1);
					DrawText();
					return true;

				default:																// printable characters get added to the string
					if ((key >= Keys.A) && (key <= Keys.Z))
					{
						byte b = (byte)key;
						if (!UserInput.IsShift())										// If shift is off, add 32 to get lower case from keycode
							b += 32;
						text += Convert.ToChar(b);
						DrawText();
					}
					else if ((key >= Keys.D0) && (key <= Keys.D9))						// accept digits as well as upper/lower case alpha
					{
						text += Convert.ToChar((byte)key);
						DrawText();
					}
					return true;
			}
		}

		/// <summary>
		/// Draw with blinking cursor
		/// </summary>
		/// <param name="sprite"></param>
		public override void Draw(Sprite sprite)
		{
			// If we have the focus, every half second, turn the cursor on or off by redrawing the string
			if (Focus == this)
			{
				if (Scene.TotalElapsedTime > cursorTime + 0.5f)
				{
					cursorTime = Scene.TotalElapsedTime;
					cursorState = !cursorState;
					DrawText();
				}
			}


			base.Draw(sprite);
		}


	}

}



