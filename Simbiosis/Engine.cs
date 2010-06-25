//-----------------------------------------------------------------------------
// File: Creature.cs
// Portions copyright (c) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


//#define DEBUG_VS   // Uncomment this line to debug vertex shaders 
//#define DEBUG_PS   // Uncomment this line to debug pixel shaders 

/////#define KLUDGE_TIMER    // Define this to make game think 100mS has elapsed each frame (fix for Vista erratic QueryPerformanceCounter())


using System;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.Samples.DirectX.UtilityToolkit;


namespace Simbiosis
{
	/// <summary>
	/// Class Engine: the "form" for the application, based on the sample framework
	/// </summary>
	public class Engine : IFrameworkCallback, IDeviceCreation
	{
		#region Creation
		/// <summary>Create a new instance of the class</summary>
		public Engine(Framework f) 
		{ 
			// Store framework
			framework = f; 
		}
		#endregion

		// Static variables and properties for easy access to device etc.
		/// <summary> The sample framework object for this application </summary>
		private static Framework framework = null; 
		public static Framework Framework { get { return framework; } }
		/// <summary> The current D3D device </summary>
		public static Device Device { get { return framework.Device; } }
		/// <summary> A reference to the one-and-only Engine instance, so that static methods can reach it </summary>
		private static Engine engine = null;

		// variables
		private bool isHelpShowing = true; // If true, renders the UI help text
		

		/// <summary>
		/// Set to true after the first ever Device has been created, so that
		/// we don't try to recreate once-only objects on a second device
		/// creation (e.g. after window/fullscreen swap). Other classes can use the IsFirstDevice property
		/// to distinguish between first and subsequent device creation in their event callbacks
		/// </summary>
		private bool subsequentDevice = false;	
		public bool IsFirstDevice { get { return !subsequentDevice; } }


		/// <summary>
		/// Called during device initialization, this code checks the device for some 
		/// minimum set of capabilities, and rejects those that don't pass by returning false.
		/// </summary>
		public bool IsDeviceAcceptable(Caps caps, Format adapterFormat, Format backBufferFormat, bool windowed)
		{
			// Skip back buffer formats that don't support alpha blending
			if (!Manager.CheckDeviceFormat(caps.AdapterOrdinal, caps.DeviceType, adapterFormat, 
				Usage.QueryPostPixelShaderBlending, ResourceType.Textures, backBufferFormat))
				return false;

			return true;
		}

		/// <summary>
		/// This callback function is called immediately before a device is created to allow the 
		/// application to modify the device settings. The supplied settings parameter 
		/// contains the settings that the framework has selected for the new device, and the 
		/// application can make any desired changes directly to this structure.  Note however that 
		/// the sample framework will not correct invalid device settings so care must be taken 
		/// to return valid device settings, otherwise creating the Device will fail.  
		/// </summary>
		public void ModifyDeviceSettings(DeviceSettings settings, Caps caps)
		{
			// If device doesn't support HW T&L or doesn't support 1.1 vertex shaders in HW 
			// then switch to SWVP.
			if ( (!caps.DeviceCaps.SupportsHardwareTransformAndLight) ||
				(caps.VertexShaderVersion < new Version(1,1)) )
			{
				settings.BehaviorFlags = CreateFlags.SoftwareVertexProcessing;
			}
			else
			{
				settings.BehaviorFlags = CreateFlags.HardwareVertexProcessing;
			}

			// This application is designed to work on a pure device by not using 
			// any get methods, so create a pure device if supported and using HWVP.
			if ( (caps.DeviceCaps.SupportsPureDevice) && 
				((settings.BehaviorFlags & CreateFlags.HardwareVertexProcessing) != 0 ) )
				settings.BehaviorFlags |= CreateFlags.PureDevice;

			// Debugging vertex shaders requires either REF or software vertex processing 
			// and debugging pixel shaders requires REF.  
#if(DEBUG_VS)
            if (settings.DeviceType != DeviceType.Reference )
            {
                settings.BehaviorFlags &= ~CreateFlags.HardwareVertexProcessing;
                settings.BehaviorFlags |= CreateFlags.SoftwareVertexProcessing;
            }
#endif
#if(DEBUG_PS)
            settings.DeviceType = DeviceType.Reference;
#endif
			// For the first device created if its a REF device, optionally display a warning dialog box
			if (settings.DeviceType == DeviceType.Reference)
			{
				Utility.DisplaySwitchingToRefWarning(Framework, "Simbiosis");
			}

		}

		/// <summary>
		/// This event will be fired immediately after the Direct3D device has been 
		/// created, which will happen during application initialization and windowed/full screen 
		/// toggles. This is the best location to create Pool.Managed resources since these 
		/// resources need to be reloaded whenever the device is destroyed. Resources created  
		/// here should be released in the Disposing event. 
		/// </summary>
		private void OnCreateDevice(object sender, DeviceEventArgs e)
		{
			Debug.WriteLine("Engine.OnCreateDevice()");

			// Define DEBUG_VS and/or DEBUG_PS to debug vertex and/or pixel shaders with the 
			// shader debugger. Debugging vertex shaders requires either REF or software vertex 
			// processing, and debugging pixel shaders requires REF.  The 
			// ShaderFlags.Force*SoftwareNoOptimizations flag improves the debug experience in the 
			// shader debugger.  It enables source level debugging, prevents instruction 
			// reordering, prevents dead code elimination, and forces the compiler to compile 
			// against the next higher available software target, which ensures that the 
			// unoptimized shaders do not exceed the shader model limitations.  Setting these 
			// flags will cause slower rendering since the shaders will be unoptimized and 
			// forced into software.  See the DirectX documentation for more information about 
			// using the shader debugger.
			ShaderFlags shaderFlags = ShaderFlags.NotCloneable;
#if(DEBUG_VS)
            shaderFlags |= ShaderFlags.ForceVertexShaderSoftwareNoOptimizations;
#endif
#if(DEBUG_PS)
            shaderFlags |= ShaderFlags.ForcePixelShaderSoftwareNoOptimizations;
#endif
			// Read the D3DX effect file
			Fx.Load(e.Device, shaderFlags);			// Replaces the default framework code

			// Initialise or re-initialise the scene, which will initialise everything else
			Scene.OnDeviceCreated();

			// Finally, set the subsequentDevice flag to show that any future events follow the loss of a previous device
			subsequentDevice = true;
		}
        
		/// <summary>
		/// This event will be fired immediately after the Direct3D device has been 
		/// reset, which will happen after a lost device scenario. This is the best location to 
		/// create Pool.Default resources since these resources need to be reloaded whenever 
		/// the device is lost. Resources created here should be released in the OnLostDevice 
		/// event. 
		/// </summary>
		private void OnResetDevice(object sender, DeviceEventArgs e)
		{
			Debug.WriteLine("Engine.OnResetDevice()");
			SurfaceDescription desc = e.BackBufferDescription;
			//            // Create a sprite to help batch calls when drawing many lines of text
			//            textSprite = new Sprite(e.Device);

			// reset scene resources
			Scene.OnReset();
		}

		/// <summary>
		/// This event function will be called fired after the Direct3D device has 
		/// entered a lost state and before Device.Reset() is called. Resources created
		/// in the OnResetDevice callback should be released here, which generally includes all 
		/// Pool.Default resources. See the "Lost Devices" section of the documentation for 
		/// information about lost devices.
		/// </summary>
		private void OnDeviceLost(object sender, EventArgs e)
		{
			Debug.WriteLine("Engine.OnDeviceLost()");
			// Tell the Scene
			Scene.OnDeviceLost();
		}

		/// <summary>
		/// This callback function will be called immediately after the Direct3D device has 
		/// been destroyed, which generally happens as a result of application termination or 
		/// windowed/full screen toggles. Resources created in the OnCreateDevice callback 
		/// should be released here, which generally includes all Pool.Managed resources. 
		/// </summary>
		private void OnDestroyDevice(object sender, EventArgs e)
		{
			Debug.WriteLine("Engine.OnDestroyDevice()");
			Debug.WriteLine("(does nothing)");
		}

		/// <summary>
		/// This callback function will be called once at the beginning of every frame. This is the
		/// best location for your application to handle updates to the scene, but is not 
		/// intended to contain actual rendering calls, which should instead be placed in the 
		/// OnFrameRender callback.  
		/// </summary>
		public void OnFrameMove(Device device, double appTime, float elapsedTime)
		{
			// TODO: put scene update (but not render) here
		}

		/// <summary>
		/// This callback function will be called at the end of every frame to perform all the 
		/// rendering calls for the scene, and it will also be called if the window needs to be 
		/// repainted. After this function has returned, the sample framework will call 
		/// Device.Present to display the contents of the next buffer in the swap chain
		/// </summary>
		public void OnFrameRender(Device device, double appTime, float elapsedTime)
		{
            // HACK: Temp fix for timer problem
#if KLUDGE_TIMER
            fixedTimeKludge = fixedTimeKludge + 0.1f; 
            appTime = fixedTimeKludge;
            elapsedTime = 0.1f;
#endif 			
            Scene.Render(elapsedTime, (float)appTime);
		}
        static float fixedTimeKludge = 0;

		/// <summary>
		/// As a convenience, the sample framework inspects the incoming windows messages for
		/// keystroke messages and decodes the message parameters to pass relevant keyboard
		/// messages to the application.  The framework does not remove the underlying keystroke 
		/// messages, which are still passed to the application's MsgProc callback.
		/// </summary>
		private void OnKeyEvent(object sender, System.Windows.Forms.KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
				case System.Windows.Forms.Keys.F1:
					isHelpShowing = !isHelpShowing;
					break;
			}
		}

		/// <summary>
		/// Before handling window messages, the sample framework passes incoming windows 
		/// messages to the application through this callback function. If the application sets 
		/// noFurtherProcessing to true, the sample framework will not process the message
		/// </summary>
		public IntPtr OnMsgProc(IntPtr hWnd, NativeMethods.WindowMessage msg, IntPtr wParam, 
			IntPtr lParam, ref bool noFurtherProcessing)
		{
			// handle my own mouse and keyboard msgs
			noFurtherProcessing = UserInput.OnMsgProc(hWnd, msg, wParam, lParam);
			if (noFurtherProcessing)
				return IntPtr.Zero;

			return IntPtr.Zero;
		}

		/// <summary>
		/// Initializes the application
		/// </summary>
		public void InitializeApplication()
		{

		}


 
		/// <summary>
		/// Entry point to the program. Initializes everything and goes into a message processing 
		/// loop. Idle time is used to render the scene.
		/// </summary>
		static int Main() 
		{
			System.Windows.Forms.Application.EnableVisualStyles();
			using(framework = new Framework())
			{
				engine = new Engine(framework);
				// Set the callback functions. These functions allow the sample framework to notify
				// the application about device changes, user input, and windows messages.  The 
				// callbacks are optional so you need only set callbacks for events you're interested 
				// in. However, if you don't handle the device reset/lost callbacks then the sample 
				// framework won't be able to reset your device since the application must first 
				// release all device resources before resetting.  Likewise, if you don't handle the 
				// device created/destroyed callbacks then the sample framework won't be able to 
				// recreate your device resources.
				framework.Disposing += new EventHandler(engine.OnDestroyDevice);
				framework.DeviceLost += new EventHandler(engine.OnDeviceLost);
				framework.DeviceCreated += new DeviceEventHandler(engine.OnCreateDevice);
				framework.DeviceReset += new DeviceEventHandler(engine.OnResetDevice);

//				framework.SetKeyboardCallback(new KeyboardCallback(engine.OnKeyEvent));
				framework.SetWndProcCallback(new WndProcCallback(engine.OnMsgProc));

				framework.SetCallbackInterface(engine);
				try
				{

					// Show the cursor and clip it when in full screen
					framework.SetCursorSettings(true, true);

					// Initialize
					engine.InitializeApplication();

					// Initialize the sample framework and create the desired window and Direct3D 
					// device for the application. Calling each of these functions is optional, but they
					// allow you to set several options which control the behavior of the sampleFramework.
					framework.Initialize( true, true, true ); // Parse the command line, handle the default hotkeys, and show msgboxes
					framework.CreateWindow("Simbiosis");
					// Hook the keyboard event
					framework.Window.KeyDown += new System.Windows.Forms.KeyEventHandler(engine.OnKeyEvent);
					framework.CreateDevice( 0, true, Framework.DefaultSizeWidth, Framework.DefaultSizeHeight, 
						engine);

					// Pass control to the sample framework for handling the message pump and 
					// dispatching render calls. The sample framework will call your FrameMove 
					// and FrameRender callback when there is idle time between handling window messages.
					framework.MainLoop();

				}
#if(DEBUG)
				catch (Exception e)
				{
					// In debug mode show this error (maybe - depending on settings)
					framework.DisplayErrorMessage(e);
#else
            catch
            {
                // In release mode fail silently
#endif
					// Ignore any exceptions here, they would have been handled by other areas
					return (framework.ExitCode == 0) ? 1 : framework.ExitCode; // Return an error code here
				}

				// Perform any application-level cleanup here. Direct3D device resources are released within the
				// appropriate callback functions and therefore don't require any cleanup code here.
				return framework.ExitCode;
			}
		}
	}
}
