using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.Samples.DirectX.UtilityToolkit;
using System.Windows.Forms;
using System.Drawing;

namespace Simbiosis
{
	/// <summary>
	/// The LabCam - side-on view in laboratory area. panel supports creature design/modification
	/// </summary>
	/// <remarks>
	/// 
	/// SELECTING THE CELL TYPE TO BE ADDED
	/// There are two ways to alter the celltype selection:
	/// 1. The user presses the UP/DOWN buttons in the SECONDARY panel. 
	///		a) The button change events update the Selector widgets to display the new selection.
	///		b) The secondary panel calls Lab.NewCelltypeSelection()
	///		c) This method stores the new celltype name in Lab.NewCellGroup and Lab.NewCellType, ready for adding a new cell to the org.
	///		d) It then calls the MAIN panel's NewCelltypeSelection() method, causing it to update its celltype text widget to reflect the change
	/// 2. The user presses UP/DOWN etc. buttons in the MAIN panel. 
	///		a) The commands are sent through to the SECONDARY panel AS IF buttons had been pressed there instead. 
	///		b) The secondary panel responds as in step 1a onwards.
	/// This way, both panels can alter the selected cell type and everyone gets to know what has happened.
	/// 
	/// </summary>

	/// 
	/// </remarks>
	class Lab : CameraShip
	{
		// --------- globally accessible static members -----------
		/// <summary> The organism being built/modified/monitored </summary>
		public static Organism SelectedOrg = null;
		/// <summary> The cell that is currently selected for editing by the LabCam </summary>
		private static Cell selectedCell = null;
		/// <summary> Property also clears Lab.SelectedChannel </summary>
		public static Cell SelectedCell
		{
			get { return Lab.selectedCell; }
			set { Lab.selectedCell = value; Lab.SelectedChannel = 0; }
		}
		/// <summary> The currently selected socket on the selected cell (where any new cell will be added) </summary>
		public static JointFrame SelectedSocket = null;
		/// <summary> Index of the selected channel on the selected cell (for editing its chemical preference) </summary>
		public static int SelectedChannel = 0;
        /// <summary> The organism most recently placed on the clamp - the one the tractor beam will recall </summary>
        public static Organism TrackedOrg = null;
        /// <summary> Any organism currently on the tractor beam </summary>
        public static Organism TractorBeam = null;
		/// <summary> the names of the cellgroup and celltype currently selected in the panels </summary>
		public static string NewCellGroup = null;
        public static string NewCellType = null;
        public static string NewCellVariant = null;

		/// <summary> The cell that is currently selected for adding to the organism </summary>
		public static Cell NewCell = null;

		/// <summary> The cone marker for showing which socket is active </summary>
		private Marker socketMarker = null;
		/// <summary> The cone marker for showing which link block has been clicked on in the patchboard </summary>
		private Marker linkMarker = null;

		/// <summary> Which socket contains any currently selected link in wiring diagram </summary>
		public static JointFrame LinkSocket = null;

        private static Texture spotTex = null;						// projection texture for the spotlight

 

		public Lab()
			: base("Lab",											// Create the Lab organism
				new Vector3(Map.MapWidth / 2.0f, Water.WATERLEVEL+1.0f, Map.MapHeight / 2.0f - 50.0f),
				new Orientation(0.0f, 0.0f, 0.0f))
		{
			// Set relevant Renderable.FlagBits
			Dynamic = false;													// The lab is a fixture and doesn't respond to forces

            // Load the spotlight texture
            spotTex = TextureLoader.FromFile(Engine.Device, FileResource.Fsp("textures", "SpotlightLab.png"));

			// Create the panels
			panel[0] = new LabPanel(this);
			panel[1] = new LabPanel2(this);

		}

		/// <summary>
		/// We have become the new camera ship. 
		/// </summary>
		protected override void Enter()
		{
            // Take over the spotlight
            Fx.SetSpotlightTexture(spotTex);
            Fx.Spotlight = true;
            
            base.Enter();														// call our base method
		}

		/// <summary>
		/// We are about to stop being the current camera ship. 
		/// </summary>
		protected override void Leave()
		{
			base.Leave();														// call base method
		}

        /// <summary>
        /// Update (called whether this is the current cameraship or not)
        /// </summary>
		public override void Update()
		{
            /// HACK: FOR TESTING ONLY - ROTATE THE SHIP
            ///RotateBy(0.03f, 0.05f, 0.02f);



            // Steer the spotlight to follow the camera
            if (CurrentShip==this)
                Fx.SetSpotlightPosition(Matrix.Invert(rootCell.GetHotspotNormalMatrix(0)) * Camera.ProjMatrix);


			// If there is a creature on the clamp, position/orient it so that it sticks to the clamp (hotspot 1)
			if (SelectedOrg != null)
			{
				// Set the selected organism's position and orientation according to the clamp's hotspot
				// (Organisms store their location/orientation in a vector/quaternion, so I need to convert
				// the hotspot matrix into this form. Luckily there's a function!)
				Matrix clampMatrix = rootCell.GetHotspotMatrix(1);
				Vector3 clampPosition = new Vector3(clampMatrix.M41, clampMatrix.M42, clampMatrix.M43);
				clampMatrix.M41 = clampMatrix.M42 = clampMatrix.M43 = 0;
                Quaternion rot = Quaternion.RotationMatrix(clampMatrix);            // NOTE: Assumes there's no scaling factor to mess up the rotation matrix
                rot.Normalize();                                                    // Normalization improves (but doesn't cure) an error in the rotation using SoftImage
				SelectedOrg.RotateTo(rot);
				SelectedOrg.MoveTo(clampPosition);
			}

			// If a socket is selected, make sure the socket marker is pointing at it
			if (SelectedSocket != null)
			{
				if (socketMarker == null)
				{
					Color c = Color.FromArgb(16, 255, 0, 255);
					socketMarker = Marker.CreateCone(c, new Vector3(0, 0, 0), new Orientation(), 0.1f, 32.0f);
				}
				else
				{
					socketMarker.Goto(SelectedSocket.CombinedMatrix);
				}
			}
			else if (socketMarker != null)
			{
				socketMarker.Delete();
				socketMarker = null;
			}

			// If a link socket is selected, make sure the link marker is pointing at it
			if (LinkSocket != null)
			{
				if (linkMarker == null)
				{
					Color c = Color.FromArgb(64, 255, 255, 0);
					linkMarker = Marker.CreateCone(c, new Vector3(0, 0, 0), new Orientation(), 0.3f, 3.5f);
				}
				else
				{
					linkMarker.Goto(LinkSocket.CombinedMatrix);
				}
			}
			else if (linkMarker != null)
			{
				linkMarker.Delete();
				linkMarker = null;
			}

            // If there's a creature being tractor beamed back to the clamp, update it
            if (TractorBeam != null)
            {
                // Magnetically attract org towards clamp position. If it is now close by, attach it ready for editing
                Matrix clampMatrix = rootCell.GetHotspotMatrix(1);
                if (TractorBeam.Tractor(new Vector3(clampMatrix.M41, clampMatrix.M42, clampMatrix.M43), Quaternion.RotationMatrix(clampMatrix)) == true)
                {
                    Attach(TractorBeam);
                    TractorBeam = null;
                }                         
            }


			// Pass control to the base method
			base.Update();
		}

		/// <summary>
		/// The cell group / type / variant selection has been changed, so record the new celltype
		/// and inform the MAIN panel to redraw its selection text.
		/// Called by the secondary panel whenever EITHER panel's buttons have changed the selection.
		/// </summary>
		/// <param name="group"></param>
		/// <param name="type"></param>
		public void NewCelltypeSelection(string group, string type, string variant)
		{
			// Store the selected type's group and name in variables ready for when the next cell is added
			NewCellGroup = group;
			NewCellType = type;
            NewCellVariant = variant;

			// Tell the main panel to update its celltype selection widget
			((LabPanel)panel[0]).NewCelltypeSelection(group, type, variant);
		}



		#region -------------------- Creature editing functions --------------------------

		/// <summary>
		/// Attach a creature to the clamp (discarding any previous occupant)
		/// </summary>
		/// <param name="org"></param>
		public void Attach(Organism org)
		{
			// detach any previous occupant
			if (SelectedOrg != null)													
				Detach();
			// suspend physical properties, reset CG, etc. and set the selected org, cell & socket
			org.EditOn();
            // Remember this organism so that it can be tractor beamed back to the clamp after release
            TrackedOrg = org;									
		}

		/// <summary>
		/// Detach the creature from the clamp
		/// </summary>
		public void Detach()
		{
			// Tell creature to clean itself up and resume normal ops, then deselect itself
			if (SelectedOrg != null) 
				SelectedOrg.EditOff();														
		}


		/// <summary>
		/// Start a new creature.
		/// Clear the clamp if necessary. Construct a new creature consisting of a single core cell
		/// then attach it to the clamp.
		/// </summary>
		public void New()
		{
			// Create an Organism from a default genome (just a Core cell) and attach it to the clamp for editing
			Attach(new Organism("", Vector3.Empty, new Orientation()));
		}

		/// <summary>
		/// Add a cell of the selected type to the creature at the selected socket (if any)
		/// </summary>
		public void Add()
		{
			Lab.SelectedOrg.Add(NewCellGroup + ":" + NewCellType + "." + NewCellVariant);
		}



		#endregion

	}



}
