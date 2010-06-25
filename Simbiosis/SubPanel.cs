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
	/// This is the panel of the submarine
	/// </summary>
	class SubPanel : Panel
	{
		SubCam owner = null;											// The camera ship that owns me

		// Resources for writing to the radar display on the SubBot widget
		private SolidBrush navBrush = null;								// foreground brush for fonts etc.
		private SolidBrush navFill = null;								// background brush for fills
		private Pen navPen = null;										// foreground pen for lines etc. (same colour as .brush)
		private System.Drawing.Font navFont = null;						// body text font

        private Meter depthMeter = null;                                // sub's depth
        private Meter hdgMeter = null;                                  // sub's bearing
        private Meter trkMeter = null;                                  // tracked object's bearing
        private Meter labMeter = null;                                  // Research ship's bearing
        private Knob bouyancy = null;                                   // controls bouyancy
        private SafetyButton hatch = null;                              // "leave ship"

		public SubPanel(SubCam owner)
			: base()
		{
			// Store the camera ship for callbacks etc.
			this.owner = owner;

			// Create the backdrop and widgets
			widgetList.Add(new Widget("SubPanel", false, 1024, 768, 0, 0));						// top panel
            //widgetList.Add(new Widget("SubBot", true, 1024, 168, 0, 768 - 168));				// bottom panel (and radar)
            //widgetList.Add(new Widget("SubLeft", false, 128, 500, 0, 100));						// left panel
            //widgetList.Add(new Widget("SubRight", false, 50, 500, 1024 - 50, 100));				// right panel

			// Spotlight switch
			Switch switch1 = new Switch("toggleswitch", 48, 67, 860, 30, Keys.L);
			widgetList.Add(switch1);
			switch1.OnChange += new ChangeEventHandler(owner.Switch1Changed);

            // Depth meter
            depthMeter = new Meter("LabNeedle", 8, 48, 90, 58, (float)Math.PI * 0.75f, 4, 40, -4, 4, false);
            widgetList.Add(depthMeter);

            // Compass bearing
            hdgMeter = new Meter("CompassRose", 104, 104, 42, 646, (float)Math.PI, 52, 52, 0, 0, false); // no shadow; don't slew, or it'll spin when wrapping
            widgetList.Add(hdgMeter);

            // Bearing to tracked object
            trkMeter = new Meter("LabNeedle", 8, 48, 90, 674, (float)Math.PI, 4, 40, -4, 4, false);
            widgetList.Add(trkMeter);

            // Bearing to lab ship
            labMeter = new Meter("SmallNeedle", 8, 32, 90, 682, (float)Math.PI, 4, 25, -4, 4, false);
            widgetList.Add(labMeter);

            bouyancy = new Knob("KnobMedium", 64, 75, 950, 170, 32F, 32F, Keys.PageUp, Keys.PageDown, (float)(Math.PI * 0.5));
            bouyancy.Value = 0.5f;
            bouyancy.OnChange += new ChangeEventHandler(bouyancy_OnChange);
            widgetList.Add(bouyancy);

            hatch = new SafetyButton("SafetySwitch", 80, 92, 940, 265, Keys.None, 0.3f);
            hatch.OnChange += new ChangeEventHandler(hatch_OnChange);
            widgetList.Add(hatch);

            // Set up the Display widget for the navigation display
			SetupNavDisplay();

		}

 
        /// <summary>
        /// Return the dimensions of the porthole through which the scene will be visible.
        /// </summary>
        /// <returns>Porthole dimensions in BASE coordinates</returns>
        public override Rectangle GetPorthole()
        {
            return new Rectangle(180, 50, 670, 670);
        }


		/// <summary>
		/// Refresh navigation display and other slow-changing widgets
		/// </summary>
		public override void SlowUpdate()
		{
			DrawNavDisplay();													// draw the navigation display
		}

        /// <summary>
        /// Refresh fast-changing widgets
        /// </summary>
        public override void FastUpdate()
        {
            float bearing = 0;

            // Set the depth meter
            float depth = (Water.WATERLEVEL - Camera.Position.Y) / Water.WATERLEVEL;
            if (depth < 0) depth = 0;
            else if (depth > 1.0f) depth = 1.0f;
            depthMeter.Value = depth;

            // Set the compass
            hdgMeter.Value = 1.0f - ((Camera.bearing / (float)Math.PI / 2.0f) % 1.0f);

            // Show the bearing of the tracked object, if any
            if (Lab.TrackedOrg != null)
            {
                bearing = (float)Math.Atan2(Lab.TrackedOrg.Location.X - owner.Location.X, Lab.TrackedOrg.Location.Z - owner.Location.Z);
                bearing -= Camera.bearing;                                  // show relative to current heading
                bearing = bearing / (float)Math.PI / 2f + 0.5f;
                trkMeter.Value = bearing;
            }

            // Show the bearing of the research vessel
            Organism lab = (Organism)CameraShip.GetLab();
            bearing = (float)Math.Atan2(lab.Location.X - owner.Location.X, lab.Location.Z - owner.Location.Z);
            bearing -= Camera.bearing;                                  // show relative to current heading
            bearing = bearing / (float)Math.PI / 2f + 0.5f;
            labMeter.Value = bearing;

        }

		/// <summary>
		/// Create the resources for drawing onto the navigation display (subbot)
		/// </summary>
		private void SetupNavDisplay()
		{
			// Create the fonts, brushes and pens
			navFont = new System.Drawing.Font(FontFamily.GenericMonospace, 12, FontStyle.Bold);
			navBrush = new SolidBrush(Color.FromArgb(150, Color.LightGreen));
			navFill = new SolidBrush(Color.FromArgb(64, Color.DarkGreen));
			navPen = new Pen(navBrush, 2);
		}

		/// <summary>
		/// Draw text/graphics onto the navigation display inside the SubBot widget
		/// </summary>
		private void DrawNavDisplay()
		{
            //const float left = 580;										// top-left edge of radar disp
            //const float top = 120;

            //Graphics g = widgetList[1].BeginDrawing();

            //// TEMP: ----------- Draw debug info ----------------
            //g.DrawString(Map.HUDInfo + " " + Engine.Framework.FPS.ToString("###") + "fps", navFont, Brushes.Red, 480, 48);

            //float hdg = Camera.bearing / (float)Math.PI * 180.0f;
            //if (hdg < 0) hdg += 360;

            //// Draw the navigation text
            //g.DrawString(String.Format("locn: {0:0000}E           hdg: {1:000}deg",
            //            Camera.Position.X, hdg),
            //            navFont, navBrush, left + 48, top + 8);

            //g.DrawString(String.Format("      {0:0000}N         depth: {1:000}m",
            //            Camera.Position.Z, Water.WATERLEVEL - Camera.Position.Y),
            //            navFont, navBrush, left + 48, top + 24);

            //widgetList[1].EndDrawing();
		}

        /// <summary>
        /// Bouyancy knob has been moved. Send a command through to root cell's physiology
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        void bouyancy_OnChange(Widget sender, object value)
        {
            owner.Command("buoyancy", value);
        }

        /// <summary>
        /// Hatch button has been pressed - jump to survey ship / lab
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        void hatch_OnChange(Widget sender, object value)
        {
            hatch.Frame = 0;                                        // reset switch
            CameraShip.SwitchCameraShip();                          // change to next (specific???) camera ship
        }


        /// <summary>
        /// Left mouse click - may be a 3D pick.
        /// Picking is used to select an organism to track.
        /// </summary>
        /// <param name="screenX">mouse position when click occurred</param>
        /// <returns>true if the click was handled</returns>
        public override bool LeftClick(float screenX, float screenY)
        {
            // First check to see if it is a 3D pick of a cell
            JointFrame socket;
            Cell cell = Camera.MousePick(screenX, screenY, out socket);						// get the selected cell and/or socket
            if (cell != null)
            {
                if (!(cell.Owner is CameraShip))                                            // if it isn't a camera ship
                    Lab.TrackedOrg = cell.Owner;                                          // set it to the source for tracking and any future tractor beam
                return true;
            }
            // If not, pass it back to the base class to distribute to our widgets
            return base.LeftClick(screenX, screenY);
        }



	}

	/// <summary>
	/// Secondary panel: Clear view of scene without any backdrop or instruments
	/// </summary>
	class SubPanel2 : Panel
	{
		SubCam owner = null;													// The camera ship that owns me

		public SubPanel2(SubCam owner)
			: base()
		{
			// Store the camera ship for callbacks etc.
			this.owner = owner;
		}

		/// <summary>
		/// Refresh any HUD Display widgets, etc.
		/// </summary>
		public override void SlowUpdate()
		{
		}

  
	}


}
