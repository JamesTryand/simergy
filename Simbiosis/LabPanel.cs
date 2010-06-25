using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.IO;
using System.Windows.Forms;

namespace Simbiosis
{

	#region -------------------------------------- ANATOMY PANEL ----------------------------------------------------------

	/// <summary>
	/// Main panel class for the LabCam - supports creature design/modification/debug commands
	/// This panel is the view into the lab, showing any creature that is being built or examined.
	/// LabPanel2 is the VDU on which cell data can be displayed and cells can be selected/wired (no 3D view)
	/// </summary>
	class LabPanel : Panel
	{
		#region ------------------------------- Panel display handling -----------------------------

		private Lab owner = null;											// The camera ship that owns me

		private Widget osd = null;											// topmost backdrop panel, with LCD display
		private Widget labReflection = null;								// LCD glass reflection
		private Widget dispCellGroup = null;								// panel for displaying selected cell group
        private Widget dispCellType = null;									// panel for displaying selected cell type
        private Widget dispCellVariant = null;								// panel for displaying selected cell variant
        private Wheel wheelRotate = null;									// wheel for rotating a cell's plug
        private Wheel wheelConst = null;									// wheel for adjusting a channel constant
        private Meter meterConst = null;                                    // meter for showing value of current channel constant
        private Lamp lampConst = null;                                      // LED shows whether the selected channel is set to a constant rather than a chemical
        private EditBox editSpecies = null;									// Edit box for typing creature's "name"
		private MultiSwitch modeSwitch = null;								// display mode multiswitch
        private Lamp lampTractor = null;                                    // show tractor beam is active
        private System.Drawing.Font msgFont = null;							// font for celltype selector text
		private System.Drawing.Font lcdFont = null;							// font for celltype info on LCD display
        private float zoomRate = 0;                                         // current rate of zoom

		public LabPanel(Lab owner)
			: base()
		{
			// Record my owner for callbacks etc.
			this.owner = owner;

			// Create fonts
			// KLUDGE: When lcdFont is different from msgFont, there's an odd bug:
			// if I create a creature, anything using lcdFont fails to draw (the EditBox and the LCD text)
			// but if I type into the EditBox before creating a creature, everything is ok.
			// Equally, if I make the cell selector use lcdFont instead of msgFont then all objects using lcdFont work ok.
			// So there's something in the EditBox and cell selector code that "fixes" lcdFont. Can't figure this out,
			// so for now I'll just use the one font for everthing and wait until inspiration strikes!!!!
			msgFont = new System.Drawing.Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold, GraphicsUnit.Pixel);
			//lcdFont = new System.Drawing.Font(FontFamily.GenericSansSerif, 14, FontStyle.Regular, GraphicsUnit.Pixel);
			lcdFont = msgFont;	// Temp: let lcdFont share msgFont

            // writeable onscreen display (behind screen so that raster lines show)
            widgetList.Add(osd = new Widget("LabOSD", true, 256, 64, 210, 64));

            // Edit box for typing species name
            editSpecies = new EditBox("LCDEditBox", 128, 16, 210, 48, 54, 0, lcdFont, Brushes.Green, "Species:");
            widgetList.Add(editSpecies);
            editSpecies.OnChange += new ChangeEventHandler(editSpeciesChanged);

            // Backdrop
            widgetList.Add(new Widget("LabBkgnd", false, 1024, 768, 0, 0));						
            
			// Display panels for showing the currently selected cell group & type
			dispCellGroup = new Widget("LCDLine", true, 100, 20, 416, 675);
			widgetList.Add(dispCellGroup);
            dispCellType = new Widget("LCDLine", true, 100, 20, 416, 695);
            widgetList.Add(dispCellType);
            dispCellVariant = new Widget("LCDLine", true, 100, 20, 416, 715);
            widgetList.Add(dispCellVariant);

			// Button: Start building a new creature
			SafetyButton btnNew = new SafetyButton("SafetySwitch", 80, 92, 18, 16, Keys.None, 0.3f);
			widgetList.Add(btnNew);
			btnNew.OnChange += new ChangeEventHandler(btnNewChanged);

			// Button: Release creature from clamp
			SafetyButton btnRelease = new SafetyButton("SafetySwitch", 80, 92, 18, 154, Keys.None, 0.3f);
			widgetList.Add(btnRelease);
			btnRelease.OnChange += new ChangeEventHandler(btnReleaseChanged);

            // Button: Activate the tractor beam
            Button btnTractor = new Button("PushButtonSmall", 40, 40, 37, 320, Keys.T, false);
            widgetList.Add(btnTractor);
            btnTractor.OnChange += new ChangeEventHandler(btnTractorChanged);
            // LED to show when tractor beam is active
            lampTractor = new Lamp("LED", 24, 24, 45, 292);
            widgetList.Add(lampTractor);

			// Button: Delete selected cells
			Button btnDelete = new Button("PushButtonSmall", 40, 40, 300, 686, Keys.Back, false);
			widgetList.Add(btnDelete);
			btnDelete.OnChange += new ChangeEventHandler(btnDeleteChanged);


            // Button: Add a new cell (INSERT)
            Button btnAdd = new Button("PushButtonSmall", 40, 40, 593, 698, Keys.Insert, false);
            widgetList.Add(btnAdd);
            btnAdd.OnChange += new ChangeEventHandler(btnAddChanged);

            // Button: Preview a potential new cell (SPACE)
            Button btnPreview = new Button("BtnRect", 64, 24, 580,670, Keys.Space, false);
            widgetList.Add(btnPreview);
            btnPreview.OnChange += new ChangeEventHandler(btnPreviewChanged);


			// These buttons change the selected celltype by mimicking those in the secondary panel
			// Button: cell group selection up
			Button btnGroupUp = new Button("BtnRectSmallLeft", 32, 24, 363, 670, Keys.Insert, true);
			widgetList.Add(btnGroupUp);
			btnGroupUp.OnChange += new ChangeEventHandler(btnGroupUpChanged);
			// Button: cell group selection down
			Button btnGroupDn = new Button("BtnRectSmallRight", 32, 24, 536, 670, Keys.Delete, true);
			widgetList.Add(btnGroupDn);
			btnGroupDn.OnChange += new ChangeEventHandler(btnGroupDnChanged);
            // Button: cell type selection up
            Button btnTypeUp = new Button("BtnRectSmallLeft", 32, 24, 363, 694, Keys.Home, true);
            widgetList.Add(btnTypeUp);
            btnTypeUp.OnChange += new ChangeEventHandler(btnTypeUpChanged);
            // Button: cell type selection down
            Button btnTypeDn = new Button("BtnRectSmallRight", 32, 24, 536, 694, Keys.End, true);
            widgetList.Add(btnTypeDn);
            btnTypeDn.OnChange += new ChangeEventHandler(btnTypeDnChanged);
            // Button: cell variant selection up
            Button btnVariantUp = new Button("BtnRectSmallLeft", 32, 24, 363, 718, Keys.PageUp, true);
            widgetList.Add(btnVariantUp);
            btnVariantUp.OnChange += new ChangeEventHandler(btnVariantUpChanged);
            // Button: cell variant selection down
            Button btnVariantDn = new Button("BtnRectSmallRight", 32, 24, 536, 718, Keys.PageDown, true);
            widgetList.Add(btnVariantDn);
            btnVariantDn.OnChange += new ChangeEventHandler(btnVariantDnChanged);

			// Buttons for navigating/selecting cells and sockets
			// Button: up in cell tree
			Button btnCellUp = new Button("BtnRectUp", 64, 24, 16, 664, Keys.Up, false);
			widgetList.Add(btnCellUp);
			btnCellUp.OnChange += new ChangeEventHandler(btnCellUpChanged);
			// Button: down in cell tree
			Button btnCellDn = new Button("BtnRectDn", 64, 24, 16, 712, Keys.Down, false);
			widgetList.Add(btnCellDn);
			btnCellDn.OnChange += new ChangeEventHandler(btnCellDnChanged);
			// Button: next sibling in cell tree
            Button btnCellNext = new Button("BtnRectSmallRight", 32, 24, 48, 688, Keys.Right, false);
			widgetList.Add(btnCellNext);
			btnCellNext.OnChange += new ChangeEventHandler(btnCellNextChanged);
			// Button: prev sibling in cell tree
            Button btnCellPrev = new Button("BtnRectSmallLeft", 32, 24, 16, 688, Keys.Left, false);
			widgetList.Add(btnCellPrev);
			btnCellPrev.OnChange += new ChangeEventHandler(btnCellPrevChanged);
			// Button: next socket
			Button btnSktNext = new Button("PushbuttonSmall", 40, 40, 123, 686, Keys.Enter, false);
			widgetList.Add(btnSktNext);
			btnSktNext.OnChange += new ChangeEventHandler(btnSktNextChanged);

			// Knob: rotates selected cell
			wheelRotate = new Wheel("Knob", 48, 56, 34, 578, 24f, 24f, Keys.None, Keys.None);
			widgetList.Add(wheelRotate);
			wheelRotate.OnChange += new ChangeEventHandler(knobRotateChanged);
			// Button: rotates selected cell in 45-degree increments
            Button btnRotate = new Button("PushbuttonSmall", 40, 40, 37, 511, Keys.R, true);
			widgetList.Add(btnRotate);
			btnRotate.OnChange += new ChangeEventHandler(btnRotateChanged);

			// Multiswitch selects mode: cell-edit, channel-edit or chemoscan
			modeSwitch = new MultiSwitch("knobMedium", 64, 75, 930, 317, 32f, 32f, Keys.None, Keys.None, 4, (float)(Math.PI * 0.5));
			widgetList.Add(modeSwitch);
			modeSwitch.OnChange += new ChangeEventHandler(ModeSwitchChanged);
			modeSwitch.Value = (int)Camera.ScannerModes.Cell;									// default mode is cell-edit mode

			// Buttons for navigating/modifying chemical channels
			// Button: previous channel
			Button btnChannelUp = new Button("BtnRectUp", 64, 24, 205, 684, Keys.None, false);
			widgetList.Add(btnChannelUp);
			btnChannelUp.OnChange += new ChangeEventHandler(btnChannelUpChanged);
			// Button: next channel
			Button btnChannelDn = new Button("BtnRectDn", 64, 24, 205, 708, Keys.None, false);
			widgetList.Add(btnChannelDn);
			btnChannelDn.OnChange += new ChangeEventHandler(btnChannelDnChanged);
			// Button: change channel to use previous chemical
            Button btnChemUp = new Button("BtnRectUp", 64, 24, 677, 684, Keys.None, false);
			widgetList.Add(btnChemUp);
			btnChemUp.OnChange += new ChangeEventHandler(btnChemUpChanged);
			// Button: change channel to use next chemical
            Button btnChemDn = new Button("BtnRectDn", 64, 24, 677, 708, Keys.None, false);
			widgetList.Add(btnChemDn);
			btnChemDn.OnChange += new ChangeEventHandler(btnChemDnChanged);

            // Buttons for steering & zooming camera
            Button btnCamUp = new Button("BtnRectUp", 64, 24, 930, 440, Keys.NumPad8, false);
            widgetList.Add(btnCamUp);
            btnCamUp.OnChange += new ChangeEventHandler(btnCamUpChanged);
            Button btnCamDn = new Button("BtnRectDn", 64, 24, 930, 488, Keys.NumPad2, false);
            widgetList.Add(btnCamDn);
            btnCamDn.OnChange += new ChangeEventHandler(btnCamDnChanged);
            Button btnCamLeft = new Button("BtnRectSmallRight", 32, 24, 962, 464, Keys.NumPad6, false);
            widgetList.Add(btnCamLeft);
            btnCamLeft.OnChange += new ChangeEventHandler(btnCamLeftChanged);
            Button btnCamRight = new Button("BtnRectSmallLeft", 32, 24, 930, 464, Keys.NumPad4, false);
            widgetList.Add(btnCamRight);
            btnCamRight.OnChange += new ChangeEventHandler(btnCamRightChanged);
            Button btnCamZoomIn = new Button("BtnRectUp", 64, 24, 930, 548, Keys.NumPad9, true);
            widgetList.Add(btnCamZoomIn);
            btnCamZoomIn.OnChange += new ChangeEventHandler(btnCamZoomInChanged);
            Button btnCamZoomOut = new Button("BtnRectDn", 64, 24, 930, 572, Keys.NumPad3, true);
            widgetList.Add(btnCamZoomOut);
            btnCamZoomOut.OnChange += new ChangeEventHandler(btnCamZoomOutChanged);

            // Knob: adjusts channel constants
            wheelConst = new Wheel("Knob", 48, 56, 785, 689, 24f, 24f, Keys.None, Keys.None);
            widgetList.Add(wheelConst);
            wheelConst.OnChange += new ChangeEventHandler(wheelConstChanged);
            // Meter: shows channel constant
            meterConst = new Meter("LabNeedle", 8, 48, 898, 710, 0.87f, 4, 40, -3, 3, true);
            widgetList.Add(meterConst);
            // LED to show when a constant is applicable (i.e. chemical selectivity is set to none)
            lampConst = new Lamp("LED", 24, 24, 796, 662);
            widgetList.Add(lampConst);
            
			// transparent reflection on LCD glass (printed on top of LCD contents)
			widgetList.Add(labReflection = new Widget("LabReflection", false, 620, 112, 416, 675));		

		}

        /// <summary>
        /// Return the dimensions of the porthole through which the scene will be visible.
        /// </summary>
        /// <returns>Porthole dimensions in BASE coordinates</returns>
        public override Rectangle GetPorthole()
        {
            return new Rectangle(192, 24, 640, 570);
        }

        /// <summary>
        /// Override this to refresh stuff that changes every frame
        /// </summary>
        public override void FastUpdate()
        {
            // If a zoom button is being held down, continue zooming
            if (zoomRate != 0)
                Camera.ZoomBy(Scene.ElapsedTime * zoomRate);

            // If tractor beam is operating, flash the LED
            lampTractor.Value = (Lab.TractorBeam!=null)&&((Scene.TotalElapsedTime % 1f)>0.5) ? 1 : 0;
        }

		/// <summary>
		/// Draw the currently selected cell name etc. on the onscreen display
		/// </summary>
		public void DrawCellData()
		{
			Graphics g = osd.BeginDrawing();
			//g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;		// MUST antialias or text leaves black marks!

			// update creature name, in case we've started a new one
			if (Lab.SelectedOrg != null)
				editSpecies.Text = Lab.SelectedOrg.Name;
			else
				editSpecies.Text = "";

			// Display the cell type being edited (not the one being added)
			if (Lab.SelectedCell != null)
			{
				DrawOSDString(g, "Celltype: "+Lab.SelectedCell.group + ":" + Lab.SelectedCell.name + "." + Lab.SelectedCell.variantName, 0, 0);

				// Display the channel being edited
				DrawOSDString(g, "Channel: " + Lab.SelectedCell.GetChannelInfo(Lab.SelectedChannel), 0, 16);

                // TEMP: Debug info
                DrawOSDString(g, Engine.Framework.FPS.ToString("###") + "fps", 0, 32);
			}

			osd.EndDrawing();

            UpdateConstantMeter();                                                  // refresh meter with current channel constant, while we're here

		}

		/// <summary>
		/// Draw a string on the onscreen display, with shadow
		/// </summary>
		/// <param name="g">graphics object</param>
		/// <param name="s">string</param>
		/// <param name="x">posn</param>
		/// <param name="y">posn</param>
		private void DrawOSDString(Graphics g, string s, int x, int y)
		{
			///////g.DrawString(s, lcdFont, Brushes.Gray, x + 1, y - 1);
			g.DrawString(s, lcdFont, Brushes.Red, x, y);
		}

		/// <summary>
		/// Button: Start a new creature
		/// </summary>
		/// <param name="sender"></param>
		public void btnNewChanged(Widget sender, Object value)
		{
			if (((bool)value == true)												// Only do it if the button has been pressed
				&& (Lab.SelectedOrg == null))										// and no creature is on the clamp
			{
				owner.New();														// lab creates the creature
				DrawCellData();														// update the LCD
			}
		}

		/// <summary>
		/// Button: detach the creature from the clamp
		/// </summary>
		/// <param name="sender"></param>
		public void btnReleaseChanged(Widget sender, Object value)
		{
			if (((bool)value == true)												// Only do it if the button has been pressed
				&& (Lab.SelectedOrg != null))										// and there is a creature on the clamp
			{
				owner.Detach();														// detach the creature from the clamp
				DrawCellData();														// update the LCD
			}
		}

        /// <summary>
        /// Button: Activate the tractor beam
        /// </summary>
        /// <param name="sender"></param>
        public void btnTractorChanged(Widget sender, Object value)
        {
            if (((bool)value == true)												// Only do it if the button has been pressed
                && (Lab.TrackedOrg != null))										// and there has been a creature on the clamp or selected for tracking
            {
                if (Lab.SelectedOrg != null)                                        // detach any existing creature from the clamp
                {                                                                   // in case another creature was clicked on from the sub, whilst editing
                    owner.Detach();														
                }

                Lab.TractorBeam = Lab.TrackedOrg;                                   // attach this org to the tractor beam
                DrawCellData();														// update the LCD
            }
        }

        /// <summary>
        /// Button: delete the selected cell(s)
        /// </summary>
        /// <param name="sender"></param>
        public void btnDeleteChanged(Widget sender, Object value)
        {
            if (((bool)value == true)												// Only do it if the button has been pressed
                && (Lab.SelectedCell != null))										// and there is a selected cell
            {
                Organism.DeleteCell();												// delete the selected cell
                DrawCellData();														// update the LCD
            }
        }




		/// <summary>
		/// A cell group or type selector button has changed. Pass the event through to the secondary panel,
		/// which will handle the reselection as if the button was pressed there. This will eventually ripple
		/// back to us via our NewCelltypeSelection() method, which redraws our copy of the selection text.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="value"></param>
		public void btnGroupUpChanged(Widget sender, Object value)
		{
			((LabPanel2)owner.Panel[1]).btnGroupUpChanged(sender, value);
		}
		public void btnGroupDnChanged(Widget sender, Object value)
		{
			((LabPanel2)owner.Panel[1]).btnGroupDnChanged(sender, value);
		}
        public void btnTypeUpChanged(Widget sender, Object value)
        {
            ((LabPanel2)owner.Panel[1]).btnTypeUpChanged(sender, value);
        }
        public void btnTypeDnChanged(Widget sender, Object value)
        {
            ((LabPanel2)owner.Panel[1]).btnTypeDnChanged(sender, value);
        }
        public void btnVariantUpChanged(Widget sender, Object value)
        {
            ((LabPanel2)owner.Panel[1]).btnVariantUpChanged(sender, value);
        }
        public void btnVariantDnChanged(Widget sender, Object value)
        {
            ((LabPanel2)owner.Panel[1]).btnVariantDnChanged(sender, value);
        }

		/// <summary>
		/// A channel selector or chemical assignment button has changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="value"></param>
		public void btnChannelUpChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedCell != null))
			{
				Lab.SelectedCell.NextChannel();
				DrawCellData();
			}
		}
		public void btnChannelDnChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedCell != null))
			{
				Lab.SelectedCell.PrevChannel();
				DrawCellData();
			}
		}
		public void btnChemUpChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedCell != null))
			{
				Lab.SelectedCell.NextChemical();
				DrawCellData();
			}
		}
        public void btnChemDnChanged(Widget sender, Object value)
        {
            if (((bool)value == true) && (Lab.SelectedCell != null))
            {
                Lab.SelectedCell.PrevChemical();
                DrawCellData();
            }
        }


        /// <summary>
        /// A camera steering button has changed. Pass this to the cell (via the organism) to animate the camera joints
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        public void btnCamUpChanged(Widget sender, Object value)
        {
            owner.Command("up", value);
        }
        public void btnCamDnChanged(Widget sender, Object value)
        {
            owner.Command("down", value);
        }
        public void btnCamLeftChanged(Widget sender, Object value)
        {
            owner.Command("left", value);
        }
        public void btnCamRightChanged(Widget sender, Object value)
        {
            owner.Command("right", value);
        }

        public void btnCamZoomInChanged(Widget sender, Object value)
        {
            zoomRate = ((bool)value)? -0.3f : 0f;
        }
        public void btnCamZoomOutChanged(Widget sender, Object value)
        {
            zoomRate = ((bool)value) ? 0.3f : 0f;
        }


		/// <summary>
		/// The organism's species name has been changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="value"></param>
		public void editSpeciesChanged(Widget sender, Object value)
		{
			Organism org = Lab.SelectedOrg;
			if (org != null)
			{
				org.Name = (string)value;
			}
		}

		/// <summary>
		/// The cell group / type / variant selector has been changed, so update our selection widgets.
		/// Called by the Lab whenever we or the secondary panel have changed the selection.
		/// </summary>
		/// <param name="group"></param>
		/// <param name="type"></param>
		public void NewCelltypeSelection(string group, string type, string variant)
		{
			StringFormat format = new StringFormat();
			format.Alignment = StringAlignment.Center;

			// Redraw our own cell group selection box
			Graphics graphics = dispCellGroup.BeginDrawing();
			graphics.DrawString(group, msgFont, Brushes.Black, 50, 2,format);
			dispCellGroup.EndDrawing();

            // Redraw our own cell type selection box
            graphics = dispCellType.BeginDrawing();
            graphics.DrawString(type, msgFont, Brushes.Black, 50, 2, format);
            dispCellType.EndDrawing();

            // Redraw our own cell variant selection box
            graphics = dispCellVariant.BeginDrawing();
            graphics.DrawString(variant, msgFont, Brushes.Black, 50, 2, format);
            dispCellVariant.EndDrawing();
        }

		/// <summary>
		/// A cell navigation button was pressed. Move up/down/left/right in the tree and select a new cell
		/// </summary>
		/// <param name="sender"></param>
		public void btnCellUpChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedOrg != null))
			{
				Lab.SelectedOrg.SelectUp();
				DrawCellData();
			}
		}
		public void btnCellDnChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedOrg != null))
			{
				Lab.SelectedOrg.SelectDown();
				DrawCellData();
			}
		}
		public void btnCellNextChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedOrg != null))
			{
				Lab.SelectedOrg.SelectNext();
				DrawCellData();
			}
		}
		public void btnCellPrevChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedOrg != null))
			{
				Lab.SelectedOrg.SelectPrevious();
				DrawCellData();
			}
		}

		/// <summary>
		/// A socket navigation button has been pressed - select next socket (if any - may be null)
		/// </summary>
		/// <param name="sender"></param>
		public void btnSktNextChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedCell != null))
				Lab.SelectedCell.SelectNext();
		}

        /// <summary>
        /// ADD button pressed - add a new cell
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        public void btnAddChanged(Widget sender, Object value)
        {
            if (((bool)value == true) && (Lab.SelectedCell != null))
            {
                owner.Add();
                DrawCellData();
            }
        }

        /// <summary>
        /// PREVIEW button pressed - temporarily add a new cell or delete it again when btn released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        public void btnPreviewChanged(Widget sender, Object value)
        {
            if (((bool)value == true) && (Lab.SelectedCell != null) && (Lab.SelectedSocket != null))
            {
                owner.Add();
                DrawCellData();
                previewed = Lab.SelectedCell;
            }
            else if (((bool)value == false) && (Lab.SelectedCell != null) && (Lab.SelectedCell == previewed))
            {
                Organism.DeleteCell();												// delete the selected cell
                DrawCellData();														// update the LCD
                previewed = null;
            }

        }
        private Cell previewed = null;

		/// <summary>
		/// Rotation wheel has moved - rotate the selected cell
		/// </summary>
		/// <param name="sender"></param>
		public void knobRotateChanged(Widget sender, Object value)
		{
			if (Lab.SelectedCell != null)
			{
				// Get a delta angle from the wheel & add it to the plug's yaw
				Lab.SelectedCell.PlugOrientation *= Matrix.RotationYawPitchRoll((float)value, 0, 0);
			}
		}

        /// <summary>
        /// Constant wheel has moved - adjust the current channel's constant value
        /// </summary>
        /// <param name="sender"></param>
        private void wheelConstChanged(Widget sender, Object value)
        {
            if ((Lab.SelectedCell != null)&&(Lab.SelectedCell.NumChannels>Lab.SelectedChannel))
            {
                // Get a delta angle from the wheel & add it to the constant
                float c = Lab.SelectedCell.GetChannelConstant(Lab.SelectedChannel);
                c += (float)value / (float)Math.PI;
                if (c<0f) c=0f;
                else if (c>1f) c=1f;
                Lab.SelectedCell.SetChannelConstant(Lab.SelectedChannel, c);
                
                // Set meter to reflect change
                UpdateConstantMeter();
            }
        }

        /// <summary>
        /// Update the meter to reflect the current selected channel's constant value, if appropriate. Call this whenever a selection changes.
        /// </summary>
        private void UpdateConstantMeter()
        {
            ChannelData[] chanType = null;

            if (Lab.SelectedCell != null)
                chanType = Lab.SelectedCell.Physiology.GetChannelData();

            if ((Lab.SelectedCell != null)                                                              // if there's a selected cell
                && (Lab.SelectedCell.NumChannels > Lab.SelectedChannel)                                 // and one or more channels in it
                && (chanType!=null)                                                                     
                && (chanType[Lab.SelectedChannel].IsInput())                                            // and that channel is an input channel
                && (Lab.SelectedCell.Channel(Lab.SelectedChannel).chemical==0))                         // whose chemical selectivity is set to NONE
            {
                meterConst.Value = Lab.SelectedCell.GetChannelConstant(Lab.SelectedChannel);            // show the constant value
                lampConst.Value = 1f;                                                                   // and light the LED
            }
            else                                                                                        // for all other cases, we'll hide the constant b/c it isn't useful
            {
                meterConst.Value = 0;
                lampConst.Value = 0f;                                                                   // and extinguish the LED
            }
        }

		/// <summary>
		/// Rotation button has changed - rotate the selected cell in neat 90-degree increments
		/// </summary>
		/// <param name="sender"></param>
		public void btnRotateChanged(Widget sender, Object value)
		{
			if (((bool)value == true) && (Lab.SelectedCell != null))
			{
				// get the current angle from the matrix (varies from 0 at top to +/- PI at bottom)
				float angle = -(float)Math.Atan2(Lab.SelectedCell.PlugOrientation.M13, Lab.SelectedCell.PlugOrientation.M33);

				// Convert range from +/-PI to +/-1
				const float PI = (float)Math.PI;
				float octant = angle / PI;

				// From there into range +/-4 (current octant). Round to nearest integer so that we go to 
				// our nearest octant, then increment to the next octant
				octant = (float)Math.Round(octant * 4) + 1;

				// Return into the range +/-PI
				octant = octant / 4 * PI;

				// This is the new rotation
				Lab.SelectedCell.PlugOrientation = Matrix.RotationYawPitchRoll(octant, 0, 0);
			}
		}


		/// <summary>
		/// User has selected a new disply mode (normal, cell-editing, channel-editing, chemoscan)
		/// </summary>
		public void ModeSwitchChanged(Widget sender, Object value)
		{
			Camera.ScannerMode = (Camera.ScannerModes)((int)value);
		}


		#endregion

		#region ---------------------------------- Creature design code --------------------------------------




		/// <summary>
		/// Left mouse click - may be a 3D pick.
		/// Picking is only used to select cells or sockets on the *currently selected* creature.
		/// </summary>
		/// <param name="screenX">mouse position when click occurred</param>
		/// <returns>true if the click was handled</returns>
		public override bool LeftClick(float screenX, float screenY)
		{
			// First check to see if it is a 3D pick of a cell (and possibly a socket on that cell)
            RectangleF porthole = GetPorthole();
            PointF mouse = Widget.ToBaseCoords(new PointF(screenX, screenY));
            if (porthole.Contains((int)mouse.X, (int)mouse.Y))                                  // only check for picking if the mouse is within the porthole
            {
                JointFrame socket;
                Cell cell = Camera.MousePick(screenX, screenY, out socket);						// get the selected cell and/or socket
                if ((cell != null) && (cell.Owner == Lab.SelectedOrg))							// IF it belongs to the creature on the clamp
                {
                    Lab.SelectedCell = cell;													// select this cell
                    if (socket != null)															// and possibly a socket if one was clicked on
                        Lab.SelectedSocket = socket;
                    else
                        Lab.SelectedCell.SelectFirst();											// if no skt was clicked, select the first one in cell
                    DrawCellData();
                    return true;
                }
            }
			// If not, pass it back to the base class to distribute to our widgets
			return base.LeftClick(screenX, screenY);
		}

		/// <summary>
		/// Handle any special design mode keypresses
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			//switch (key)
			//{
			//}

			// If we didn't handle the key, offer to the base Panel class for generic key commands
			return base.KeyPress(key);
		}

		/// <summary>
		/// We have become visible. 
		/// </summary>
		public override void Enter()
		{
			base.Enter();
			DrawCellData();
			Camera.ScannerMode = (Camera.ScannerModes)modeSwitch.Value;		// Pick up display mode from Mode switch
//			Scene.FreezeTime = -1;											// Stop the creatures from moving!
            Camera.ZoomReset();                                             // lose any zoom
            zoomRate = 0;
		}

		/// <summary>
		/// We are about to become invisible. Allow our widgets to dispose of any unwanted resources.
		/// </summary>
		public override void Leave()
		{
			base.Leave();
//			Scene.FreezeTime = 1;											// set the world back in motion

			Camera.ScannerMode = Camera.ScannerModes.Normal;				// Exit design mode and return to normal rendering
            Camera.ZoomReset();                                             // lose any zoom
        }



	


		#endregion







	}

	#endregion
	#region -------------------------------------------------- VDU PANEL --------------------------------------------------
	/// <summary>
	/// Secondary panel - the VDU on which cell data can be displayed and cells can be selected/wired (no 3D view)
	/// </summary>
	class LabPanel2 : Panel
	{

		private Lab owner = null;									// The cameraship that owns me

		private const int VDUX = 64;									// left edge of VDU text region
		private const int VDUY = 64;									// top edge of VDU text region

		private Selector groupSelector = null;							// VDU section for listing cell groups
        private Selector typeSelector = null;							// VDU section for listing cell types
        private Selector variantSelector = null;						// VDU section for listing cell type variants
        private Carousel infoPanel = null;								// VDU section for displaying cell type info
		private Widget totalsPanel = null;								// VDU section for displaying info about the organism being built

		private System.Drawing.Font vduFont = null;						// VDU font
		private SolidBrush vduBrush = null;								// foreground brush for fonts etc.
		private SolidBrush vduFill = null;								// background brush for fills
		private Pen vduPen = null;										// foreground pen for lines etc. (same colour as .brush)

		private int vduLine = 0;										// Number of pixels per display line




		public LabPanel2(Lab owner)
			: base()
		{
			// Store the camera ship for callbacks etc.
			this.owner = owner;

			// Create the fonts, brushes and pens
			vduFont = new System.Drawing.Font(FontFamily.GenericMonospace, 12, FontStyle.Bold);
			vduBrush = new SolidBrush(Color.FromArgb(150, Color.LightGreen));
			vduFill = new SolidBrush(Color.FromArgb(64, Color.LightGreen));
			vduPen = new Pen(vduBrush, 2);

			// Depth of one line of text
			vduLine = vduFont.Height;
			
			// Create the backdrop and widgets...
			// For now, use a single-piece backdrop and superimpose smaller bitmaps for the writeable widgets
			// such as selectors. Later it might be sensible to cut up the backdrop so as not to duplicate
			// these regions
			widgetList.Add(new Widget("LabPanel2", false, 1024, 768, 0, 0));

            // cell group list on VDU
            groupSelector = new Selector("LabVduGroup", 160, 128, 192, 98, vduFont, vduBrush, vduFill);
            widgetList.Add(groupSelector);
            groupSelector.OnChange += new ChangeEventHandler(groupSelectorChanged);
            // cell type list on VDU
			typeSelector = new Selector("LabVduType", 160, 128, 192, 256, vduFont, vduBrush, vduFill);
			widgetList.Add(typeSelector);
			typeSelector.OnChange += new ChangeEventHandler(typeSelectorChanged);
            // cell variant list on VDU
            variantSelector = new Selector("LabVduVariant", 160, 96, 192, 415, vduFont, vduBrush, vduFill);
            widgetList.Add(variantSelector);
            variantSelector.OnChange += new ChangeEventHandler(variantSelectorChanged);


			// Button: cell group selection up
            Button btnGroupUp = new Button("BtnRectUp", 64, 24, 8, 64, Keys.None, true);
			widgetList.Add(btnGroupUp);
			btnGroupUp.OnChange += new ChangeEventHandler(btnGroupUpChanged);
			// Button: cell group selection down
            Button btnGroupDn = new Button("BtnRectDn", 64, 24, 8, 128, Keys.None, true);
			widgetList.Add(btnGroupDn);
			btnGroupDn.OnChange += new ChangeEventHandler(btnGroupDnChanged);
            // Button: cell type selection up
            Button btnTypeUp = new Button("BtnRectUp", 64, 24, 8, 192, Keys.None, true);
            widgetList.Add(btnTypeUp);
            btnTypeUp.OnChange += new ChangeEventHandler(btnTypeUpChanged);
            // Button: cell type selection down
            Button btnTypeDn = new Button("BtnRectDn", 64, 24, 8, 256, Keys.None, true);
            widgetList.Add(btnTypeDn);
            btnTypeDn.OnChange += new ChangeEventHandler(btnTypeDnChanged);
            // Button: cell variant selection up
            Button btnVariantUp = new Button("BtnRectUp", 64, 24, 8, 320, Keys.None, true);
            widgetList.Add(btnVariantUp);
            btnVariantUp.OnChange += new ChangeEventHandler(btnVariantUpChanged);
            // Button: cell variant selection down
            Button btnVariantDn = new Button("BtnRectDn", 64, 24, 8, 384, Keys.None, true);
            widgetList.Add(btnVariantDn);
            btnVariantDn.OnChange += new ChangeEventHandler(btnVariantDnChanged);


			// Carousel for displaying information about the selected cell type
			infoPanel = new Carousel("LabVduInfoPanel", 512, 400, 400, 105);

			// Space for writing information about the creature being built (total mass, etc.)
			totalsPanel = new Widget("LabVduTotalsPanel", true, 740, 64, 192, 530);
			widgetList.Add(totalsPanel);

            // fill the selectors with the cell groups, types and variants available on this system
            groupSelector.SetList(GroupTypeVariant.GetGroups());
            typeSelector.SetList(GroupTypeVariant.GetTypes(0));
            variantSelector.SetList(GroupTypeVariant.GetVariants(0,0));



		}

		/// <summary>
		/// We have become visible. 
		/// </summary>
		public override void Enter()
		{
			base.Enter();

			// Compile global stats about the creature being edited, and display on bottom of VDU
			DrawStats();

		}

        /// <summary>
        /// Return the dimensions of the porthole through which the scene will be visible.
        /// </summary>
        /// <returns>Porthole dimensions in BASE coordinates</returns>
        public override Rectangle GetPorthole()
        {
            return new Rectangle(0, 0, 32, 32);         // minimal scene because we don't actually see it in this panel
        }

		/// <summary>
		/// Compile global stats about the creature being edited, and display on bottom of VDU
		/// </summary>
		private void DrawStats()
		{
			if (Lab.SelectedOrg != null)
			{
				// get the stats
				float radius, mass, bouyancy, resistance;
				Vector3 balance;
				string name;
				Lab.SelectedOrg.Stats(out radius, out mass, out bouyancy, out resistance, out balance, out name);					

				// Draw the data
				Graphics g = totalsPanel.BeginDrawing();

				string line1 = String.Format("Size:     {0:##0}\tMass: {1:#0.#}", radius, mass);
				string line2 = String.Format("Bouyancy: {0:#0.#}\tDrag: {1:#0.#}", bouyancy, resistance);
				string line3 = String.Format("Name:     " + name);
				g.DrawString(line1, vduFont, vduBrush, 0, 0);
				g.DrawString(line2, vduFont, vduBrush, 0, 16);
				g.DrawString(line3, vduFont, vduBrush, 0, 32);

				// Draw a balance diagram
				int bx = 640, by = 0;									// position of balance indicator
				int bw = 64, bh = 64;									// size of outer rectangle
				int mx = bx+(bw/2), my = by+(bh/2);						// mid-point
				g.DrawEllipse(vduPen, bx, by, bw, bh);
				g.DrawLine(vduPen, bx, my, bx + bw, my);				// crosshairs
				g.DrawLine(vduPen, mx, by, mx, by + bh);
				float balx = mx + (balance.X * bw / 2);					// CG position
				float baly = my + (balance.Z * bh / 2);
				g.DrawEllipse(vduPen, balx-4, baly-4, 8, 8);
				g.DrawString("CG", vduFont, vduBrush, bx-16, by);

				totalsPanel.EndDrawing();


			}
		}


		/// <summary>
		/// Refresh any HUD Display widgets, etc.
		/// </summary>
		public override void SlowUpdate()
		{
		}


		/// <summary>
		/// Handle any special design mode keypresses
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public override bool KeyPress(Keys key)
		{
			//switch (key)
			//{

			//}

			// If we didn't handle the key, offer to the base Panel class for generic key commands
			return base.KeyPress(key);
		}

		/// <summary>
		/// Cell group selector has changed
		/// </summary>
		/// <param name="sender"></param>
		public void groupSelectorChanged(Widget sender, Object value)
		{
			// Rebuild the type selection list
			typeSelector.SetList(GroupTypeVariant.GetTypes(groupSelector.Index));
		}

        /// <summary>
        /// Cell type selector has changed
        /// </summary>
        /// <param name="sender"></param>
        public void typeSelectorChanged(Widget sender, Object value)
        {
            // Rebuild the type selection list
            variantSelector.SetList(GroupTypeVariant.GetVariants(groupSelector.Index,typeSelector.Index));
        }

        /// <summary>
        /// Cell variant selector has changed
        /// </summary>
        /// <param name="sender"></param>
        public void variantSelectorChanged(Widget sender, Object value)
        {
            // Tell the Lab that the selected cell variant has changed. 
            // The Lab will then tell the main panel to update its own cell type selection text
            ((Lab)owner).NewCelltypeSelection(groupSelector.Selection, typeSelector.Selection, variantSelector.Selection);
        }

        /// <summary>
		/// Cell group up button changed
		/// </summary>
		/// <param name="sender"></param>
		public void btnGroupUpChanged(Widget sender, Object value)
		{
			if ((bool)value == true)
				groupSelector.MoveUp();
		}

		/// <summary>
		/// Cell group down button changed
		/// </summary>
		/// <param name="sender"></param>
		public void btnGroupDnChanged(Widget sender, Object value)
		{
			if ((bool)value == true)
				groupSelector.MoveDown();
		}

        /// <summary>
        /// Cell type up button changed
        /// </summary>
        /// <param name="sender"></param>
        public void btnTypeUpChanged(Widget sender, Object value)
        {
            if ((bool)value == true)
                typeSelector.MoveUp();
        }

        /// <summary>
        /// Cell type down button changed
        /// </summary>
        /// <param name="sender"></param>
        public void btnTypeDnChanged(Widget sender, Object value)
        {
            if ((bool)value == true)
                typeSelector.MoveDown();
        }

        /// <summary>
        /// Cell variant up button changed
        /// </summary>
        /// <param name="sender"></param>
        public void btnVariantUpChanged(Widget sender, Object value)
        {
            if ((bool)value == true)
                variantSelector.MoveUp();
        }

        /// <summary>
        /// Cell variant down button changed
        /// </summary>
        /// <param name="sender"></param>
        public void btnVariantDnChanged(Widget sender, Object value)
        {
            if ((bool)value == true)
                variantSelector.MoveDown();
        }



	}


    /// <summary>
    /// Class used to hold information about the available cell types, as listed in the Lab control panels
    /// Each instance holds information about a single cell group found on the disk.
    /// The static OnReset() method scans the disk structure as follows:
    /// ..\Cells contains a list of folders, the names of which are cell groups
    /// ..\Cells\[group name] contains a list of folders, the names of which are cell types in that group
    /// ..\Cells\[group name]\[type name] contains a list of .X files (plus textures, etc.), the names of which 
    /// are the variants of that type: e.g. [type].x, downstream.x 1.x, etc.
    /// For consistency, cells that only have one variant should be called by the same name as the folder, e.g. ..\Cells\Actuators\LightSensor\LightSensor.x
    /// Processing cells that have upstream and downstream versions can be called, e.g. ..\Cells\Processors\Trigger\Upstream.x
    /// The code automatically excludes cells in the CellTypes and Cameras folders, so that system cells like the submarine are not listed in the Lab panel.
    /// </summary>
    public class GroupTypeVariant
    {
        // --------------------------- static members ----------------------------------

        /// <summary>
        /// The library of cell groups, each of which contains information about the cell types and variants in that group
        /// </summary>
        static GroupTypeVariant[] library = null;


        // --------------------------- instance members --------------------------------
        /// <summary> Group name (Default, Cameras, etc.) </summary>
        public string groupName = null;
        /// <summary> List of cell types in this group </summary>
        public TypeVariant[] cellType = null;

        /// <summary>
        /// Return the list of known groups
        /// </summary>
        /// <returns></returns>
        public static string[] GetGroups()
        {
            string[] groups = new string[library.Length];
            for (int i = 0; i < library.Length; i++)
            {
                groups[i] = library[i].groupName;
            }
            return groups;
        }
        
        /// <summary>
        /// Return the list of types for a given group
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public static string[] GetTypes(int group)
        {
            string[] types = new string[library[group].cellType.Length];
            for (int i = 0; i < library[group].cellType.Length; i++)
            {
                types[i] = library[group].cellType[i].name;
            }
            return types;
        }

        /// <summary>
        /// Return the list of variants for a given type
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public static string[] GetVariants(int group, int type)
        {
            string[] variants = new string[library[group].cellType[type].variant.Length];
            for (int i = 0; i < library[group].cellType[type].variant.Length; i++)
            {
                variants[i] = library[group].cellType[type].variant[i];
            }
            return variants;
        }
   

        /// <summary>
        /// Static ctor. Scan the directory structure for cell groups and types.
        /// Groups are the names of subdirectories inside .\cells
        /// Cell types are the names of subdirectories inside each group folder
        /// Variants are the .x files found there - 0.x, 1.x, etc.
        /// </summary>
        static GroupTypeVariant()
        {
            string folder = Directory.GetCurrentDirectory() + "\\cells";			// assume all cell groups are found in .\cells
            string[] path = Directory.GetDirectories(folder);						// get the list of subdirectories (cell groups)

            // Get the list of group names
            List<string> group = new List<string>();
            for (int p = 0; p < path.Length; p++)	    							// remove everything except the subfolder name
            {
                group.Add(Path.GetFileName(path[p]));
            }

            // Ignore system cell types
            group.Remove("CellTypes");
            group.Remove("Cameras");

            group.Sort();

            // Initialise the library elements (GroupTypeVariants)
            library = new GroupTypeVariant[group.Count];                            // Create the library of groups
            for (int g = 0; g < group.Count; g++)			    					// for each group, create the cell type information
            {
                library[g] = new GroupTypeVariant(folder,group[g]);
            }
        }

        /// <summary>
        /// Construct a single group - a list of cell types and variants. Only accessible via static ctor
        /// </summary>
        private GroupTypeVariant(string libPath, string groupName)
        {
            // Store the group name
            this.groupName = groupName;

            // Store the list of cell type names in this group
            string groupPath = libPath + "\\" + groupName;		                            // cell types are subdirectories inside the group directory
            string[] types = Directory.GetDirectories(groupPath);		   		            // get the list of subdirectories (cell types)
            for (int p = 0; p < types.Length; p++)					    		        	// remove everything except the subfolder name
            {
                types[p] = Path.GetFileName(types[p]);
            }
            Array.Sort(types);

            // Create an array of CellType instances, holding the names of the variants
            cellType = new TypeVariant[types.Length];
            for (int t = 0; t < cellType.Length; t++)
            {
                cellType[t] = new TypeVariant(groupPath, types[t]);                         // create each TypeVariant object and let it enumberate the variants
            }


        }

        /// <summary>
        /// Contains information about one cell type: its name and list of variants
        /// </summary>
        public struct TypeVariant
        {
            public string name;
            /// <summary> Named variants for this type (downstream, upstream, etc.) </summary>
            public string[] variant;

            /// <summary>
            /// Constr
            /// </summary>
            /// <param name="typePath">The path containing the list of cell types for this group</param>
            /// <param name="typeName">The name of the cell type</param>
            public TypeVariant(string groupPath, string typeName)
            {
                name = typeName;                                                                // Store the name of the cell type

                // Store the list of variant names (X files) in this type
                string typePath = groupPath + "\\" + typeName;		                            // cell types are subdirectories inside the group directory
                string[] xfiles = Directory.GetFiles(typePath,"*.X");	                        // get the list of X files, the names of which are variants
                Array.Sort(xfiles);
                variant = new string[xfiles.Length];                                            // where to store the variant names
                for (int p = 0; p < xfiles.Length; p++)					    		        	// remove everything except the file name
                {
                    variant[p] = Path.GetFileNameWithoutExtension(xfiles[p]);
                }
            }

        }


    }



	#endregion


}
