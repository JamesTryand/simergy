using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Windows.Forms;

namespace Simbiosis
{
	/// <summary>
	/// 2D panel bitmaps and widgets.
	/// NOTE: Base all panel bitmaps on a 1024x768 screen. This is the "base resolution", as used in the Widget.CalculateSize()
	/// function. Use the origin of each widget in the base resolution to position it. The code will modify these base sizes and
	/// positions to adapt to different screen/window sizes.
	/// 
	/// To add a Widget to a panel...
	/// 
	///		Switch lightSwitch = null;
	///		lightSwitch = new Switch("toggleswitch", 48, 96, 16, 425);			// create the Widget
	///		widgetList.Add(lightSwitch);										// add it to widgetList so that it'll be rendered etc.
	///		lightSwitch.OnChange += new ChangeEventHandler(Switch1Changed);		// optionally, create an OnChange handler for it
	/// 
	/// 
	/// </summary>
	public class Panel
	{

		/// <summary> The Widgets for the panel (including the sections of backdrop) </summary>
		protected List<Widget> widgetList = new List<Widget>();

		/// <summary> The widget that just received a mouse down event and should get the next mouse up event too </summary>
		protected Widget captureWidget = null;


		/// <summary>
		/// Base constructor. 
		/// </summary>
		public Panel()
		{
			// Hook reset event to recalculate extents
			Engine.Device.DeviceReset += new System.EventHandler(OnReset);
            OnReset(null, null);
		}


		/// <summary>
		/// Device has been reset - calculate the sprite coordinates for the new screen size
		/// </summary>
		public void OnReset(object sender, EventArgs e)
		{

            // Camera.PanelViewport will have been rebuilt by now, so use it to compute the new widget positions
            foreach (Widget w in widgetList)
			{
				w.CalculateSize();
			}
		}

        /// <summary>
        /// Return the dimensions of the porthole through which the scene will be visible.
        /// This default implementation returns the whole of the panel, but panel subclasses should
        /// override this to return a smaller region where possible. 
        /// Note that the porthole will contain the whole camera view, so smaller portholes are like wider-angle cameras.
        /// </summary>
        /// <returns>Porthole dimensions in BASE coordinates</returns>
        public virtual Rectangle GetPorthole()
        {
            return new Rectangle(0, 0, 1024, 768);
        }

		/// <summary>
		/// Render the panel. This base class method renders all the widgets
		/// (using their current frame). Subclasses may override this
		/// to add code for writing text and other UI components as well.
		/// </summary>
		public void Render()
		{

			// --------------------- Set Renderstate ------------------
			// Set ALL that we care about and rem out the others. 
			// If I add any here, add them to ALL renderable classes

			//*** world matrix ***
			//Engine.Device.Transform.World = Matrix.Identity;							// Matrix is fixed at 0,0,0

			//*** expected vertex format ***
			//Engine.Device.VertexFormat = CustomVertex.PositionNormalTextured.Format;	// Textured objects
			//Engine.Device.VertexFormat = CustomVertex.PositionNormalColored.Format;	// Vertex-coloured objects

			//*** material ***
			//Engine.Device.Material = material;

			//*** Once-only texture settings ***
			//Engine.Device.SetTexture(0,texture);							// Use this texture for all primitives
			//Engine.Device.SetTexture(0,null);								// Use no texture at all (material-only objects)
			//Engine.Device.SamplerState[0].MagFilter = TextureFilter.None;				// (texel smoothing option)
			//Engine.Device.SamplerState[0].MagFilter = TextureFilter.Linear;			// (texel smoothing option)
			//Engine.Device.SamplerState[0].AddressU = TextureAddress.Mirror;			// Texture mirroring required
			//Engine.Device.SamplerState[0].AddressV = TextureAddress.Mirror;			// ditto in V direction

			//*** Transparency settings ***
			//Engine.Device.RenderState.AlphaBlendEnable = true;							// enable/disable transparency)
			// Vector alpha...
			//Engine.Device.RenderState.DiffuseMaterialSource = ColorSource.Color1;
			// Material alpha...
			//			Engine.Device.RenderState.DiffuseMaterialSource = ColorSource.Material;	
			//			Engine.Device.RenderState.SourceBlend = Blend.SourceAlpha;					// Source blend
			//			Engine.Device.RenderState.DestinationBlend = Blend.InvSourceAlpha;			// Dest blend
			// Texture alpha...
			//Engine.Device.TextureState[0].ColorOperation = TextureOperation.Modulate;	// Use the following for texture transp
			//Engine.Device.TextureState[0].ColorArgument1 = TextureArgument.TextureColor;
			//Engine.Device.TextureState[0].ColorArgument2 = TextureArgument.Diffuse;
			//Engine.Device.TextureState[0].AlphaOperation = TextureOperation.Modulate;
			//Engine.Device.TextureState[0].AlphaArgument1 = TextureArgument.TextureColor;
			//Engine.Device.TextureState[0].AlphaArgument2 = TextureArgument.Diffuse;

			//*** depth buffering ***
			Engine.Device.RenderState.ZBufferEnable = false;							// ZBuffer

			//*** lighting ***
			Engine.Device.RenderState.Lighting = false;									// enabled
			Engine.Device.RenderState.SpecularEnable = false;							// Specular lighting
			// --------------------------------------------------------



			// Draw all the widgets...
			Sprite sprite;														// we only need one sprite

			using (sprite=new Sprite(Engine.Device))
			{
				sprite.Begin(SpriteFlags.AlphaBlend);

				for (int s=0; s<widgetList.Count; s++)
				{
					widgetList[s].Draw(sprite);									// draw the current anim frame
				}

				sprite.End();
			}


		}

        /// <summary>
        /// Override this to refresh HUD Display text and graphics, etc.
        /// </summary>
        public virtual void SlowUpdate()
        {
        }

        /// <summary>
        /// Override this to refresh stuff that changes every frame
        /// </summary>
        public virtual void FastUpdate()
        {
        }

        /// <summary>
		/// Process a (non-joystick) left mouse click, by passing it to the appropriate Widget.
		/// Subclasses can override, e.g. so that LabCam can detect 3D pick commands
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public virtual bool LeftClick(float x, float y)
		{
			for (int w = widgetList.Count - 1; w >= 0; w--)						// start at the top of the list (nearest the camera)
			{
				if (widgetList[w].HitTest(x, y) == true)						// if we're over this widget
				{
					if (widgetList[w].MouseClick(x, y) == true)
					{
						captureWidget = widgetList[w];							// let the widget capture the mouse so it also gets the release event
						if (widgetList[w] != EditBox.Focus)						// if the widget we clicked is not an EditBox with focus, clear the focus
							EditBox.Focus = null;								// so that future keypresses will be treated as hotkeys, not be sent to an EditBox
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Process a (non-joystick) left mouse release, by passing it to the Widget that received the most recent click
		/// so that it can stop any action that was started by the mouse going down
		/// </summary>
		/// <returns></returns>
		public virtual bool LeftRelease()
		{
			bool result = false;
			if (captureWidget!=null)
				result = captureWidget.MouseRelease();
			captureWidget = null;
			return result;
		}

		/// <summary>
		/// A key has been pressed that neither the UI nor the camera ship itself knows how to handle.
		/// If we can handle it, return true. If we don't know how, pass it on to each of our widgets
		/// in turn (allows hotkeys for buttons etc. with animation)
		/// Subclasses can override this to trap their own special keypresses, but should end with 
		/// return base.KeyPress();
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public virtual bool KeyPress(Keys key)
		{
			//switch (key)
			//{
			//    // TODO: Add generic panel commands here
			//}

			// If we didn't handle it, pass it on to each widget in turn
			foreach (Widget w in widgetList)
			{
				if (w.KeyPress(key) == true)
					return true;
			}

			return false;
		}

		/// <summary>
		/// A key has been released.
		/// If we can handle it, return true. If we don't know how, pass it on to each of our widgets
		/// in turn - they might need it (e.g. to let go of a button)
		/// </summary>
		/// <param name="key">The Forms.Keys key that was released</param>
		/// <returns>true if the key was handled</returns>
		public virtual bool KeyRelease(Keys key)
		{
			// If we didn't handle it, pass it on to each widget in turn
			foreach (Widget w in widgetList)
			{
				if (w.KeyRelease(key) == true)
					return true;
			}

			return false;
		}


		/// <summary>
		/// We have become visible. 
		/// Wake up any widgets that have dumped their resources.
		/// Subclasses can override to perform any other initialisation
		/// </summary>
		public virtual void Enter()
		{
			for (int i = 0; i < widgetList.Count; i++)
			{
				widgetList[i].Enter();
			}
		}

		/// <summary>
		/// We are about to become invisible. Allow our widgets to dispose of any unwanted resources.
		/// </summary>
		public virtual void Leave()
		{
			for (int i = 0; i < widgetList.Count; i++)
			{
				widgetList[i].Leave();
			}
			// Any EditBox that had focus must lose it now
			EditBox.Focus = null;
		}


	}











}
