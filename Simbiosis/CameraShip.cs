using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.Samples.DirectX.UtilityToolkit;
using System.Windows.Forms;

namespace Simbiosis
{

	/// <summary>
	/// CameraShips are organisms that represent the different user views.
	/// They are Organisms so that they can have a physical presence in the world, and thus be visible to
	/// creatures, able to move using the physics engine, visible to the user from other camera ships, etc.
	/// 
	/// A CameraShip contains cockpit panels to hold the UI controls for that station.
	/// 
	/// This is an abstract class - each specific camera ship is a subclass
	/// 
	/// Camera ships can override the following virtual functions:
	/// 
	/// From Organism:
	/// - Update()
	/// - Render()
	/// - Dispose()
	/// 
	/// From CameraShip:
	/// - Enter()
	/// - Leave()
	/// 
	/// 
	/// </summary>
	public abstract class CameraShip : Organism
	{


		#region --------------------------- Static members ----------------------------------

		/// <summary> How often to do a SlowUpdate to refresh HUD Displays etc. </summary>
		const float SLOWUPDATERATE = 0.5f;
		/// <summary> STATIC timer for triggering slow updates </summary>
		private static float slowTimer = SLOWUPDATERATE;

		/// <summary> The list of camera ships </summary>
		private static CameraShip[] cameraShip = null;
		/// <summary> The ship currently carrying the user and camera </summary>
		private static int currentShip = 0;



		/// <summary>
		/// Create the list of camera ships, once the Device has been set up
		/// </summary>
		public static void OnDeviceCreated()
		{
			// Create the organisms that will act as camera platforms
			cameraShip = new CameraShip[2];													// EXTEND THIS when more platforms are added
			cameraShip[0] = new SubCam();
			cameraShip[1] = new Lab();
            //cameraShip[2] = new SurveyCam();

			SwitchCameraShip(0);															// start with camera on the sub

		}

        /// <summary>
        /// Return a reference to the laboratory research vessel, so e.g. the sub can track its location
        /// </summary>
        /// <returns></returns>
        public static CameraShip GetLab()
        {
            return cameraShip[1];
        }


        public static void OnReset()
        {
            Debug.WriteLine("CameraShip.OnReset()");

            // Set the viewport and camera for the 3D portion of the scene
            SetViewport();
        }

        /// <summary>
        /// Camera.SceneViewport is our responsibility to set (to a suitable window in the current panel). 
        /// We also need to reset the camera's aspect ratio...
        /// </summary>
        public static void SetViewport()
        {
            Rectangle porthole = cameraShip[currentShip].Panel[cameraShip[currentShip].currentPanel].GetPorthole();             // get base coords for curr panel's porthole                                               // Get the porthole dimensions (default is whole panel viewport)
            // Convert porthole from base coords to screen coords
            Camera.SceneViewport.X = (int)((float)porthole.X * (float)Camera.PanelViewport.Width / 1024f + Camera.PanelViewport.X);   // equiv to Widget.ToScreenCoords()
            Camera.SceneViewport.Y = (int)((float)porthole.Y * (float)Camera.PanelViewport.Height / 768f + Camera.PanelViewport.Y);
            Camera.SceneViewport.Width = (int)((float)porthole.Width * (float)Camera.PanelViewport.Width / 1024f);                    // equiv to Widget.ToScreenOffset()
            Camera.SceneViewport.Height = (int)((float)porthole.Height * (float)Camera.PanelViewport.Height / 768f);
            Camera.SceneViewport.MinZ = 0.0f;
            Camera.SceneViewport.MaxZ = 1.0f;
            //Debug.WriteLine("PanelViewport: x = " + Camera.PanelViewport.X + " y = " + Camera.PanelViewport.Y + " width = " + Camera.PanelViewport.Width + " height = " + Camera.PanelViewport.Height);
            //Debug.WriteLine("SceneViewport: x = " + Camera.SceneViewport.X + " y = " + Camera.SceneViewport.Y + " width = " + Camera.SceneViewport.Width + " height = " + Camera.SceneViewport.Height);
            Camera.SetProjection();                                                                             // rebuild the matrix for porthole aspect ratio
        }

		/// <summary>
		/// Render the current camera ship's cockpit panel
		/// </summary>
		/// <param name="tiller"></param>
		public static void RenderCockpit()
		{
			cameraShip[currentShip].RenderPanel();
		}


		/// <summary>
		/// Pass user control inputs to the current camera ship.
		/// The command ripples through to the correct cell and from there to the cell's physiology,
		/// where it is responded to
		/// </summary>
		/// <param name="tiller">Navigational commands</param>
		public static void SteerShip(TillerData tiller)
		{
			cameraShip[currentShip].Steer(tiller);
		}

		/// <summary>
		/// Respond to the C key by toggling through the available camera ship views
		/// (The chosen ship will receive joystick commands)
		/// </summary>
		public static void SwitchCameraShip()
		{
			cameraShip[currentShip].Leave();							// give current ship a chance to clean up
			if (++currentShip >= cameraShip.Length)							// roll to next ship in the list
				currentShip = 0;
			cameraShip[currentShip].Enter();							// new ship grabs the camera etc.
		}

		/// <summary>
		/// Switch to a specific camera ship
		/// </summary>
		public static void SwitchCameraShip(int newShip)
		{
			cameraShip[currentShip].Leave();							// give current ship a chance to clean up
			currentShip = newShip;
			cameraShip[currentShip].Enter();							// new ship grabs the camera etc.
		}

		/// <summary>
		/// Return the current camera ship
		/// </summary>
		public static CameraShip CurrentShip
		{
			get { return cameraShip[currentShip]; }
		}


		#endregion




		#region ----------------------------------------- Instance members -----------------------------------------

        /// <summary> number of display panels in this cameraship </summary>
        protected const int MAXPANELS = 4;
		/// <summary> The cockpit panels for this ship </summary>
		protected Panel[] panel = new Panel[MAXPANELS];
		/// <summary> Which panel is currently visible </summary>
        public int CurrentPanel { get { return currentPanel; } }
		protected int currentPanel = 0;

		/// <summary>
		/// Public access to the panels, so that one panel can get to its counterpart
		/// </summary>
		public Panel[] Panel
		{
			get { return panel; }
		}
	

		
		/// <summary>
		/// Construct the underlying Organism
		/// </summary>
		/// <param name="genotype"></param>
		/// <param name="location"></param>
		/// <param name="orientation"></param>
		public CameraShip(string genotype, Vector3 location, Orientation orientation)
			: base(genotype, location, orientation)
		{

		}

		/// <summary>
		/// We have become the new camera ship. 
		/// Attach the camera to our main view hotspot. Allow all the widgets to wake up.
		/// Subclasses can override to perform any other initialisation
		/// </summary>
		protected virtual void Enter()
		{
			// Attach the camera to our primary camera mount hotspot
			AssignCamera();

            // Update the viewports
            SetViewport();

			// Wake up our current panel and widgets
			panel[currentPanel].Enter();
		}

		/// <summary>
		/// We are about to stop being the current camera ship. Dispose of any unwanted resources.
		/// </summary>
		protected virtual void Leave()
		{
			// Allow our current panel and widgets to dispose of resources
			panel[currentPanel].Leave();
		}

		/// <summary>
		/// Toggle between the panels (where present)
		/// </summary>
		public void SwitchPanels()
		{
			Leave();																// leave current panel
			do
			{
				currentPanel = (currentPanel + 1) % MAXPANELS;						// change panel, wrapping as needed
			} while (panel[currentPanel] == null);									// and skipping over unused panels
            SetViewport();                                                          // update the viewports
			Enter();																// then reload panel resources
		}

		/// <summary>
		/// Switch to a specified panel
		/// </summary>
		/// <param name="newPanel"></param>
		public void SwitchPanels(int newPanel)
		{
			Leave();
			if (panel[newPanel]!=null)
				currentPanel = newPanel;
			Enter();
		}

		/// <summary>
		/// We are the current camera ship, so render our currently active panel
		/// </summary>
		public void RenderPanel()
		{
            // Do any rapid updates
            panel[currentPanel].FastUpdate();

			// If roughly half a second has elapsed, do a SlowUpdate to refresh HUD Displays etc.
			slowTimer -= Scene.ElapsedTime;
			if (slowTimer <= 0)
			{
				slowTimer += SLOWUPDATERATE;
				panel[currentPanel].SlowUpdate();
			}

			// Now render the panel
			panel[currentPanel].Render();
		}

		/// <summary>
		/// Process a (non-joystick) left mouse click, by passing it to the current panel (if any)
		/// </summary>
		/// <param name="mouse">xy or mouse (as a vector, to differentiate from instance method with same name)</param>
		/// <returns></returns>
		public bool LeftClick(Vector3 mouse)
		{
			if (panel[currentPanel] != null)
				return panel[currentPanel].LeftClick(mouse.X, mouse.Y);
			return false;
		}

		/// <summary>
		/// Process a (non-joystick) left mouse release, by passing it to the current panel (if any)
		/// </summary>
		/// <returns></returns>
		public bool LeftRelease(Vector3 mouse)
		{
			if (panel[currentPanel] != null)
				return panel[currentPanel].LeftRelease();
			return false;
		}

		/// <summary>
		/// A key has been pressed that the UI doesn't know how to handle.
		/// If we can handle it, or our current panel can handle it, return true.
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public bool KeyPress(Keys key)
		{
			// First, look to see if we know what to do with it. Return true if we handled it
			switch (key)
			{
				// switch camera ship
				case Keys.C:
					SwitchCameraShip();
					return true;

				// Toggle between primary and secondary panels
				case Keys.P:
					SwitchPanels();
					break;

				// TODO: Add generic camera ship commands here

					
			}

			// If we didn't handle it, pass it on to the currently active Panel
			return panel[currentPanel].KeyPress(key);
		}

		/// <summary>
		/// A key has been released.
		/// If we can handle it, or our current panel can handle it, return true.
		/// </summary>
		/// <param name="key">The Forms.Keys key that was pressed</param>
		/// <returns>true if the key was handled</returns>
		public bool KeyRelease(Keys key)
		{
			// In moset cases, pass it on to the currently active Panel
			// for sending to the widgets - these are the most likely to need it
			return panel[currentPanel].KeyRelease(key);
		}


		#endregion

	}











}
