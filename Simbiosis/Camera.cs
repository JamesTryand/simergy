using System;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using DirectInput = Microsoft.DirectX.DirectInput;

namespace Simbiosis
{

	// There is only one camera. However, it can be attached to any cell capable of carrying it. This may be
	// a vehicle (e.g. the submarine), a static dolly or skycam, or a specialised "eye" cell on an organism.
	// The camera is carried by a hotspot on a cell, and thus the whole of the Organism, Cell and Physiology
	// functionality is available for moving the camera, collision detection, etc.
	// It also makes camera platforms visible when seen from another camera (e.g. the sub can be seen from a dolly cam).
	//
	// ATTACHING THE CAMERA TO A CELL
	// The camera is always carried on the hotspot of a cell, for example the submarine "organism"
	// or a creatures-eye-view (on a specialised cell). 
	//
	// To attach the camera to a cell, call Organism.AssignCamera(), which in turn calls Cell.AssignCamera() 
	// for each of its cells, and they call the Physiology.AssignCamera() appropriate for their cell type. 
	// If that cell type accepts the role of camera mount, Physiology.AssignCamera() returns a hotspot number, 
	// which Cell.AssignCamera() then stores in the camera using Camera.cameraMount and Camera.cemeraHotspot. 
	// The outer call to Organism.AssignCamera() then returns true to confirm that the camera has been accepted.
	// Every tick thereafter, the camera calls the appropriate Cell.GetCameraMatrix() to fetch the
	// location/orientation of the hotspot. The inverse of this matrix determines the camera location.
	//
	// Organism.AssignCamera()








	/// <summary>
	/// static Camera object
	/// </summary>
	public static class Camera
	{

		/// <summary> Scanner modes - controls how cells will be visualised </summary>
		public enum ScannerModes
		{
			Normal,						// Normal textured camera display
			Cell,						// Cell design mode - highlights the cell/creature being edited
			Channel,					// Channel-editing mode
			Chemical,					// ChemoScan: display a local/global chemical concentration
		};
		/// <summary> Current display mode </summary>
		public static ScannerModes ScannerMode = ScannerModes.Normal;

		public enum CameraMode
		{
			Dolly,					// user-controlled or stationary location and orientation
			Carried,				// object's-eye view
			Map,					// in the sky pointing down (& following a target object if selected)
			Chasing					// hovers around a target object
		}

		// The above modes as strings, for HUD info
		public static string[] modeName = 
					   {
						   "SteerableCam",	// dolly
						   "EyeCam",		// carried
						   "SkyCam",		// map
						   "ChaseCam"		// chasing
					   };


		private const float MaxZoom = (float)Math.PI/10.0f;			// min & max field-of-view for zooming
		private const float MinZoom = (float)Math.PI/3.0f;
		private const float NormalZoom = (float)Math.PI/3.0f;		// normal FOV
				
		/// <summary> Current projection & view matrices for computing frustrum clip planes </summary>
		private static Matrix viewMatrix = new Matrix();
		public static Matrix ViewMatrix { get { return viewMatrix; } }
		private static Matrix projMatrix = new Matrix();
		public static Matrix ProjMatrix { get { return projMatrix; } }

		/// <summary> Frustrum as a series of planes for clipping </summary>
		private static Plane[] frustrum = new Plane[6];
		/// <summary> Frustrum as a box (better for some kinds of clipping)</summary>
		private static Vector3[] corner = new Vector3[8];

		/// <summary> 
		/// Location of camera if Fixed or Tracking. 
		/// </summary>
		public static Vector3 position = new Vector3(1.0f,1.0f,1.0f);
		/// <summary> Yaw direction of camera (used to orient billboard sprites) </summary>
		public static float bearing = 0;

		/// <summary> Field-of-view (PI/4=45 degrees = normal)</summary>
		private static float fov = NormalZoom;

        /// <summary>
        /// The viewport to be used when rendering the 3D portion of the display. This can be smaller than the panel to clip unnecessary drawing.
        /// Its extent is computed by calling the GetSceneViewport() method of the current cameraship.
        /// </summary>
        public static Viewport SceneViewport = new Viewport();
        
        /// <summary>
        /// The viewport to be used when rendering the whole display, including panel. This may be smaller than the window,
        /// to ensure the display maintains the same aspect ratio as a 1024x768 screen.
        /// </summary>
        public static Viewport PanelViewport { get { return Camera.panelViewport; } }
        private static Viewport panelViewport = new Viewport();








		/// <summary>
		/// constr
		/// </summary>
		static Camera()
		{
		}

		/// <summary>
		/// Once the first D3D device has been created, set up those things that couldn't be done before
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Camera.OnDeviceCreated()");
		
			// Recalculate view data on resets of device
			//Engine.Device.DeviceReset += new System.EventHandler(OnReset);
			OnReset();
			
		}


        
		/// <summary>
		/// Called immediately after the D3D device has been destroyed, 
		/// which generally happens as a result of application termination or 
		/// windowed/full screen toggles. Resources created in OnSubsequentDevices() 
		/// should be released here, which generally includes all Pool.Managed resources. 
		/// </summary>
		public static void OnDeviceLost()
		{
			Debug.WriteLine("Camera.OnDeviceLost()");
			Debug.WriteLine("(does nothing)");
		}

		// Device has been reset - reinitialise renderstate variables that
		// normally don't get modified each frame
        public static void OnReset()
        {
            Debug.WriteLine("Camera.OnReset()");

            SetViewports();
        }

        /// <summary>
        /// Compute & set projection matrix for frustrum, following a reset or zoom
        /// </summary>
        public static void SetProjection()
        {
            // Size of viewport
            float width = (float)SceneViewport.Width;
            float height = (float)SceneViewport.Height;
            
            projMatrix =													// keep the result in a static field for frustrum calcs
				Matrix.PerspectiveFovLH(fov,								// field of view
				width / height,												// aspect ratio
				1f,															// near clipping plane (metres)
				Scene.Horizon);												// far clipping plane (metres)

            ///Engine.Device.SetTransform(TransformType.Projection, projMatrix);   // set the device projection matrix
            Debug.Assert(projMatrix.M34 == 1.0f, "WARNING: Projection matrix is not W-friendly - fog won't be rendered properly. See DX documentation");
			SetFrustrum();													// recompute the clip planes
		}

        /// <summary>
        /// Set the two viewports for the new window size:
        /// Camera.sceneViewport defines the screen area into which the 3D scene will be rendered.
        /// Camera.panelViewport defines the screen ares into which the cameraship panel will be rendered.
        /// Both are set to the same aspect ratio as a 1024x768 screen. I.e. if the window is wider than this aspect ratio, the display will be centred in it,
        /// with dark bands to either side. This enables the game to run fullscreen on a widescreen monitor.
        /// Call this function on a reset. Scene.Render() uses the two Viewport objects when rendering the scene and panel.
        /// </summary>
        private static void SetViewports()
        {
            const float BASEASPECT = 1024f / 768f;                                                          // The sprite graphics are drawn to this aspect ratio
            Rectangle window = Rectangle.Empty;

            // Compute the overall viewport - whole window, minus the borders needed to keep a 1024x768 aspect...

            if (Engine.Framework.IsWindowed == true)                                                        // If we're windowed, ClientRectangle contains the size
                window = Engine.Framework.ClientRectangle;
            else                                                                                            // but for fullscreen, look at the screen mode
            {                                                                                               // (note: this fixes the fullscreen bug! ClientRectangle
                window.Width = Engine.Device.DisplayMode.Width;                                             // is zero when we go fullscreen)
                window.Height = Engine.Device.DisplayMode.Height;
            }

            panelViewport.X = window.X;                                                                     // initialise viewport to full window/screen
            panelViewport.Y = window.Y;
            panelViewport.Width = window.Width;
            panelViewport.Height = window.Height;
            panelViewport.MaxZ = 1.0f;

            float currentAspect = (float)window.Width / (float)window.Height;                               // get the window aspect ratio

            if (currentAspect > BASEASPECT)                                                                 // if the window is too wide
            {
                panelViewport.Width = (int)(window.Width / currentAspect * BASEASPECT);                     // calculate a new width & store
                panelViewport.X = (int)((window.Width - panelViewport.Width) / 2f);                         // then calculate an X-offset to centre the display
            }
            else                                                                                            // if it is too tall, centre display vertically instead
            {
                panelViewport.Height = (int)(window.Height / BASEASPECT * currentAspect);
                panelViewport.Y = (int)((window.Height - panelViewport.Height) / 2f);
            }

            //Debug.WriteLine("Window: " + window.ToString());
            //Debug.WriteLine("PanelViewport: x = " + panelViewport.X + " y = " + panelViewport.Y + " width = " + panelViewport.Width + " height = " + panelViewport.Height);

        }


		/// <summary>
		/// Set up the view and projection matrices for the current camera position/angle
		/// Call this every frame.
		/// </summary>
		/// <param name="elapsedtime">number of seconds elapsed since last frame</param>
		/// <param name="simtime">time that has elapsed since simulation began</param>
		public static void Render()
		{
			// The camera matrix is the inverse of the camera mount cell's hotspot frame
			Matrix hotspot = CameraShip.CurrentShip.GetCameraMatrix();
			viewMatrix = Matrix.Invert(hotspot);

			// Update the frustrum clip planes
			SetFrustrum();

			// update Camera.position from the matrix, so that it is correct for LOD calculations etc.
			position = new Vector3(hotspot.M41,hotspot.M42,hotspot.M43);

			// Update camera bearing angle, so that it can be used by billboards, etc.
			hotspot.M41 = hotspot.M42 = hotspot.M43 = 0;								// remove the translation component
			Vector3 normal = new Vector3(0,0,1);										// vector representing the hotspot normal
			normal = Vector3.TransformCoordinate(normal,hotspot);						// rotate this into hotspot orientation
			bearing = (float)( Math.PI/2 - Math.Atan2(normal.Z, normal.X));				// and compute its bearing (yaw)

			// Send new camera data (position and shadow matrix) to the shaders
			Fx.SetCameraData();
		}


		/// <summary>
		/// Calculate the frustrum sides, ready for culling
		/// </summary>
		/// <remarks>
		/// Works by creating a unit cube in front of the camera and then transforming
		/// this by the view and projection matrices, to give a box the size and shape of the viewing
		/// frustrum. This is then used as the framework for calculating the planes of its six sides,
		/// which are used when culling
		/// </remarks>
		public static void SetFrustrum()
		{
			// set up the transformation
			Matrix m = Matrix.Multiply(viewMatrix,projMatrix);				// combined view & proj matrices
			m.Invert();														// we need to do the opposite xform

			// set up a box with unit corners, in front of the camera
			corner[0] = new Vector3(-1.0f,-1.0f, 0.0f);						// near b/l
			corner[1] = new Vector3( 1.0f,-1.0f, 0.0f);						// near b/r
			corner[2] = new Vector3(-1.0f, 1.0f, 0.0f);						// near t/l
			corner[3] = new Vector3( 1.0f, 1.0f, 0.0f);						// near t/r
			corner[4] = new Vector3(-1.0f,-1.0f, 1.0f);						// far b/l
			corner[5] = new Vector3( 1.0f,-1.0f, 1.0f);						// far b/r
			corner[6] = new Vector3(-1.0f, 1.0f, 1.0f);						// far t/l
			corner[7] = new Vector3( 1.0f, 1.0f, 1.0f);						// far t/r

			// transform each corner into the view frustrum
			for (int i=0; i<8; i++)
				corner[i] = Vector3.TransformCoordinate(corner[i],m);

			// use the transformed corners to build the six faces of the frustrum
			frustrum[0] = Plane.FromPoints(corner[7],corner[3],corner[1]);			// right face
			frustrum[1] = Plane.FromPoints(corner[2],corner[6],corner[4]);			// left face
			frustrum[2] = Plane.FromPoints(corner[6],corner[7],corner[5]);			// far face
			frustrum[3] = Plane.FromPoints(corner[0],corner[1],corner[3]);			// near face
			// Top & bottom faces are at end so can be skipped during horizontal clip tests
			frustrum[4] = Plane.FromPoints(corner[2],corner[3],corner[7]);			// top face 
			frustrum[5] = Plane.FromPoints(corner[4],corner[5],corner[1]);			// bottom face

		}


		/// <summary>
		/// Check to see whether a bounding sphere/circle intersects the viewing frustrum
		/// (e.g. for culling).
		/// </summary>
		/// <remarks>
		/// Note: for 3D objects I need to check whether they're inside ALL walls of the frustrum (including top and
		/// bottom). When checking for visible QUADS, however, I should only compare against the sides, since a quad
		/// has a bounding sphere of finite height, but should be treated as if it extends upwards to infinity. To do
		/// this, pass 4 instead of 6 in the "planes" parameter.
		/// </remarks>
		/// <param name="centre">xyz of centre of sphere</param>
		/// <param name="radius">radius of sphere</param>
		/// <param name="planes">Enter 6 to check if an object is within the entire frustrum (top/bottom as well 
		/// as sides), or 4 to check if a point on the MAP (e.g. a quad) is within the LATERAL extent
		/// of the frustrum</param>
		/// <returns>0=outside frustrum; 1=inside frustrum; -1=straddles frustrum</returns>
		public static int CanSee(Vector3 centre, float radius, int planes)
		{
			float dist;
			int behind = 0;													// counts # faces point is behind

			for (int i=0; i<planes; i++)									// for each face
			{
				dist = frustrum[i].Dot(centre);								// compute distance from the plane
				if (dist < -radius)											// if it's more than radius beyond plane
					return 0;												// object can't intersect frustrum at all
				if (dist > radius)											// if it's more than radius inside the plane
					behind++;												// it's fully enclosed by one more surface
			}
			if (behind==planes)												// if fully enclosed by all 6 planes
				return 1;													// it's fully inside frustrum

			return -1;														// otherwise it must straddle it
		}

		/// <summary>
		/// Overload of above to test whether a given 3D renderable object is within frustrum
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static int CanSee(Renderable obj)
		{
			return (CanSee(obj.AbsSphere.Centre,obj.AbsSphere.Radius, 6));
		}







		/// <summary>
		/// Adjust camera zoom
		/// </summary>
		/// <param name="amount">positive values zoom OUT (wider angle)</param>
		public static void ZoomBy(float amount)
		{
			ZoomTo(fov + amount);
		}

		/// <summary>
		/// Set camera zoom
		/// </summary>
		/// <param name="amount">PI/4 is 45 degrees (normal perspective)</param>
		public static void ZoomTo(float amount)
		{
			fov = amount;
			if (fov < MaxZoom) fov = MaxZoom;
			if (fov > MinZoom) fov = MinZoom;
            SetProjection();														// recompute projection matrix
		}

        /// <summary>
        /// Reset to default zoom
        /// </summary>
        public static void ZoomReset()
        {
            ZoomTo(NormalZoom);
        }


		#region properties


		/// <summary> Return camera location in world (works in all modes) </summary>
		public static Vector3 Position
		{
			get
			{
				return position;
			}
		}


		/// <summary>
		///  Names of the permissible camera modes (e.g. for filling panel controls)
		/// </summary>
		/// <param name="index">mode to fetch</param>
		/// <returns>name of mode, or null if index is too large</returns>
		public static string ModeName(int index)
		{
			if (index>=modeName.Length)
				return null;
			return modeName[index];
		}



	


		#endregion


		/// <summary>
		/// Given a mouse click, find the cell that has been clicked on
		/// </summary>
		/// <param name="screenX">the mouse cursor position when the click occurred</param>
		/// <param name="screenY"></param>
		/// <param name="socket">If a SOCKET on the cell was selected, its frame is returned here</param>
		/// <returns>The picked cell, or null if no cell at that point</returns>
		public static Cell MousePick(float screenX, float screenY, out JointFrame socket)
		{
			// Step 1: Convert the mouse position into an eyepoint in 3D
			Vector3 v = new Vector3();
            screenX -= Camera.SceneViewport.X;                                                                          // remove offset of viewport
            screenY -= Camera.SceneViewport.Y;
			v.X = (((2.0f * screenX) / Camera.SceneViewport.Width) - 1) / projMatrix.M11;
			v.Y = -(((2.0f * screenY) / Camera.SceneViewport.Height) - 1) / projMatrix.M22;
			v.Z = 1.0f;

			// Step 2: Create a ray emanating from the mouse cursor in the direction of the camera
			Matrix m = viewMatrix;
			m.Invert();
			Vector3 rayDirection = new Vector3(
								v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31,
								v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32,
								v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33);

			Vector3 rayPosition = new Vector3(m.M41, m.M42, m.M43);

			// Step 3: Iterate through the orgs and cells to find the nearest one to the camera that our ray intersects
			return Map.MousePick(rayPosition, rayDirection, out socket);
		}



	}
}
