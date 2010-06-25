using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using DirectInput = Microsoft.DirectX.DirectInput;

namespace Simbiosis
{
	/// <summary>
	/// PURE STATIC Class: the one-and-only scene
	/// </summary>
	public static class Scene
	{

		// WEATHER
		/// <summary> Colour for fog and also background when wiping render target at start of frame</summary>
        //public static Color FogColor = Color.FromArgb(110, 180, 160);
		//public static Color FogColor = Color.FromArgb(64, 100, 140);
		public static Color FogColor = Color.FromArgb(140, 200, 200);
		// Farthest visible distance (used to set clipping planes, fog and skybox)
		public const float Horizon = 200.0f;
		/// <summary> The time since last frame, for use by objects in the scene </summary>
		private static float elapsedTime = 0;
		public static float ElapsedTime { get { return elapsedTime; } }
		/// <summary> Time since application started (useful for creating rythmic motion etc.) </summary>
		private static float totalElapsedTime = 0;
		public static float TotalElapsedTime { get { return totalElapsedTime; } }

		/// <summary> Time left until game is unfrozen (-1=indefinite freeze, 0=unfrozen, n=pause for n secs) </summary>
		private static float freezeTime = 0f;
		public static float FreezeTime { get { return freezeTime; } set { freezeTime = value; } }

        /// <summary>
        /// TEMP: Opens a text file that can be used for logging debug info (e.g. in releas version)
        /// </summary>
       ////// public static StreamWriter Log = new StreamWriter("Log.txt");


		// TEMP: somewhere to store some creatures
		public static Organism[] Creature = null;


		#region construction and initialisation

		/// <summary>
		/// Constructor. Note: This gets called before the form, device, etc. have been created.
		/// </summary>
		static Scene()
		{
			Debug.WriteLine("Constructing scene");

		}

		/// <summary>
		/// Once the first D3D device has been created, set up those things that couldn't be done before
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Scene.OnDeviceCreated()");
            
            // Pause the time while everything is created, else the first frame will appear to be a long elapsedTime
            //Engine.Framework.Pause(true, true);

			// Ripple through to other classes' STATIC event handlers. Instances will hook their own events
			UserInput.OnDeviceCreated();
			Terrain.OnDeviceCreated();
			SkyBox.OnDeviceCreated();
			Water.OnDeviceCreated();
			Marker.OnDeviceCreated();
			Scenery.OnDeviceCreated();
			Camera.OnDeviceCreated();
			CameraShip.OnDeviceCreated();
			SkyBox.OnDeviceCreated();
			Map.OnDeviceCreated();

			// HACK: TEMP TEST FOR BODY & Cell CLASSES
			CreateSomeCreatures();


           //Engine.Framework.Pause(false, false);
           Microsoft.Samples.DirectX.UtilityToolkit.FrameworkTimer.Reset();
           
		}


		/// <summary>
		/// TEMP: Create one or more creatures for testing
		/// </summary>
		private static void CreateSomeCreatures()
		{
			const int NUMCREATURES = 50;

			Creature = new Organism[NUMCREATURES];

            string[] genotype = {
                "testTail",
                "testJawRed",
            };

			// Creature 0 is guaranteed to be in front of the camera - use this alone when testing
            Creature[0] = new Organism(genotype[0], new Vector3(512.0f, 25.0f, 475.0f), new Orientation(0, 0, 0));
            Creature[1] = new Organism(genotype[1], new Vector3(512.0f, 25.0f, 480.0f), new Orientation(0, 0, 0));

			for (int c = 2; c < NUMCREATURES; c++)
			{
				float x, z;
				do
				{
					x = Rnd.Float(400,600);
					z = Rnd.Float(400,600);
				} while (Terrain.AltitudeAt(x, z) > Water.WATERLEVEL - 10);

				Vector3 loc = new Vector3(
					x,
					Rnd.Float(Terrain.AltitudeAt(x, z) + 5, Water.WATERLEVEL - 5),
					z
					);
				Orientation or = new Orientation(Rnd.Float(3.14f), Rnd.Float(3.14f), Rnd.Float(3.14f));
				int genome = Rnd.Int(genotype.Length - 1);
				Creature[c] = new Organism(genotype[genome], loc, or);
			}

		}

		/// <summary>
		/// Called immediately after the D3D device has been destroyed, 
		/// which generally happens as a result of application termination or 
		/// windowed/full screen toggles. Resources created in OnSubsequentDevices() 
		/// should be released here, which generally includes all Pool.Managed resources. 
		/// </summary>
		public static void OnDeviceLost()
		{
			Debug.WriteLine("Scene.OnDeviceLost()");
			// Ripple through to other classes' STATIC event handlers. Instances will hook their own events
			SkyBox.OnDeviceLost();
			Terrain.OnDeviceLost();
			Water.OnDeviceLost();
			Marker.OnDeviceLost();
			Camera.OnDeviceLost();
			UserInput.OnDeviceLost();
			Map.OnDeviceLost();
		}

		/// <summary>
		/// Device has been reset - reinitialise renderstate variables that
		/// normally don't get modified each frame. Also call the OnReset()
		/// methods for all scene objects
		/// </summary>
		public static void OnReset()
		{
			Debug.WriteLine("Scene.OnReset()");
			// Ripple through to other classes' STATIC event handlers. Instances will hook their own events
            Camera.OnReset();
            CameraShip.OnReset();
            SkyBox.OnReset();
			Terrain.OnReset();
			Water.OnReset();
			Marker.OnReset();
			Map.OnReset();
            
			// enable fog
			Engine.Device.RenderState.FogEnable = true;
			Engine.Device.RenderState.FogColor = FogColor;


			// If we can, use h/w alpha testing. This is faster and prevents any depth-buffering issues
			// TODO: But remember to test it without!!!
			if (Engine.Device.DeviceCaps.AlphaCompareCaps.SupportsNotEqual==true)
			{
			    Engine.Device.RenderState.AlphaTestEnable = true;
			    Engine.Device.RenderState.AlphaFunction = Compare.NotEqual;
			    Engine.Device.RenderState.ReferenceAlpha = unchecked((int)0x00000000);
			}


		}

		#endregion


		/// <summary>
		/// Render the whole scene
		/// </summary>
		public static void Render(float elapsedTime, float appTime)
		{
           ///// Scene.Log.WriteLine("e:" + elapsedTime + " t:" + appTime);

			// If the game is in suspended animation, count down
			if (FreezeTime > 0)
			{
				FreezeTime -= elapsedTime;
				if (freezeTime < 0)
					freezeTime = 0;
			}

			// Store the elapsed time in a global property
			Scene.elapsedTime = elapsedTime;
            ////Debug.WriteLine(elapsedTime.ToString());
			Scene.totalElapsedTime = appTime; //////+= elapsedTime;

			// Handle any pre-render stuff
			UserInput.Update();														// Poll the input devices (move camera, etc.)
			SetWaterColour();														// Adjust the fog colour according to depth
			Camera.Render();														// Set up the camera matrices
			Map.CullScene();														// Set up the render batches for objects on map

			// Render the shadow map texture
			if (Fx.IsShadowed == true)												// if we're using shadows,
				Map.RenderShadow();													// Render the shadow map

            // Set the viewport for the 3D scene
            Engine.Device.Viewport = Camera.SceneViewport;
            
            // Render the scene from the camera's point of view
			bool beginSceneCalled = false;
			Engine.Device.Clear(ClearFlags.ZBuffer | ClearFlags.Target, Scene.FogColor, 1.0f, 0);				// Clear the render target and the zbuffer 
            try
            {
                Engine.Device.BeginScene();
                beginSceneCalled = true;

                // Render the skybox FIRST
                SkyBox.Render();

                // Render all non-culled objects in the quadtree, including terrain tiles & water
                Map.Render();

                // Draw any debugging 3D markers
                Marker.Render();

                // Set the viewport for the cameraship panel
                Engine.Device.Viewport = Camera.PanelViewport;

                // Render the cockpit panel of the current camera ship
                CameraShip.RenderCockpit();

            }
            catch (Exception e)
            {
                throw new SDKException("Unknown error rendering scene: ", e);
            }
			finally
			{
				if (beginSceneCalled)
					Engine.Device.EndScene();
			}

		}

		/// <summary>
		/// Adjust the fog colour according to depth, so that surface waters are brighter and bluer than deep waters
		/// </summary>
		private static void SetWaterColour()
		{
			Color deep = Color.FromArgb(35, 80, 50);												// deepest water colour
			Color shallow = Color.FromArgb(190, 223, 223);											// shallowest water colour
			float interp = Camera.Position.Y / Water.WATERLEVEL;
			FogColor = Color.FromArgb((int)((shallow.R - deep.R) * interp + deep.R),
										(int)((shallow.G - deep.G) * interp + deep.G),
										(int)((shallow.B - deep.B) * interp + deep.B));
			Engine.Device.RenderState.FogColor = FogColor;
		}

		/// <summary>
		/// Write out a screenshot as a BMP file
		/// </summary>
		/// <param name="filespec"></param>
		public static void SaveScreenshot(string filespec)
		{
			Surface backbuffer = Engine.Device.GetBackBuffer(0, 0, BackBufferType.Mono);
			SurfaceLoader.Save(filespec, ImageFileFormat.Bmp, backbuffer);
			backbuffer.Dispose();
		}

		/// <summary>
		/// Put the creatures (but not camera etc.) into suspended animation for n seconds.
		/// </summary>
		/// <param name="seconds">0=unfreeze, n=freeze n seconds, -1=freeze indefinitely</param>
		public static void Freeze(float seconds)
		{
			freezeTime = seconds;
		}


	
	
	}
}
