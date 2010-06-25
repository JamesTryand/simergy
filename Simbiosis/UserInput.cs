using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.Samples.DirectX.UtilityToolkit;
using Microsoft.DirectX.DirectInput;

namespace Simbiosis
{
	/// <summary>
	/// User input class
	/// This version only uses DirectInput for joysticks. It receives mouse & kbd Windows messages via the
	/// sample framework's OnMsgProc() method, so that the UI works. Any messages it doesn't handle are returned.
	/// </summary>
	public static class UserInput
	{
		/// <summary> Current states of all the keys </summary>
		private static bool[] KeyState = new bool[256];


		private const int joyThrust = 0;											// joystick button for thrust
		private static Device joystick = null;
		private static JoystickState joystickState = new JoystickState();

		private static Vector3 virtualJoystick = new Vector3();						// raw xyz values for mouse joystick emulation
		const float VIRTUALJOYSTICKRANGE = 300;										// how much to scale down the mouse movements

		/// <summary> The mouse location when the right button went down </summary>
		private static Vector3 dragPoint = new Vector3(0,0,0);
		/// <summary> true if the right button is down (joystick emulation) </summary>
		private static bool dragging = false;
		/// <summary> The current mouse position </summary>
		public static Vector3 Mouse = new Vector3(0,0,0);
		/// <summary> true if the left mouse button is down (emulates joystick FIRE btn) </summary>
		public static bool leftButton = false;

		static UserInput()
		{
		}

		/// <summary>
		/// Once the first D3D device has been created, set up those things that couldn't be done before
		/// </summary>
		public static void OnDeviceCreated()
		{
			// Set up a joystick if available
			foreach (DeviceInstance instance in Manager.GetDevices(DeviceClass.GameControl,EnumDevicesFlags.AttachedOnly))
			{
				joystick = new Device(instance.InstanceGuid);						// Grab the first connected joystick
				break;
			}
			if (joystick!=null)
			{
				joystick.SetCooperativeLevel(Engine.Framework.Window,
					CooperativeLevelFlags.Foreground
					| CooperativeLevelFlags.Exclusive);
				joystick.SetDataFormat(DeviceDataFormat.Joystick);
				joystick.Properties.SetRange(ParameterHow.ByDevice,0,new InputRange(-1000,1000));	// set all ranges to +/-1000
				joystick.Properties.SetDeadZone(ParameterHow.ByDevice,0,2000);						// we need a dead zone for stability
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
			if (joystick!=null)
				joystick.Unacquire();
		}

		/// <summary>
		/// Poll the devices - call every frame
		/// </summary>
		public static void Update()
		{
			// Read any joystick
			try
			{
				if (joystick!=null)
				{
					joystick.Poll();
					joystickState = joystick.CurrentJoystickState;
				}
			}
			catch 
			{
				try
				{
					joystick.Acquire();
					return;
				}
				catch
				{
					return;
				}
			}

			// If the mouse right button is down, make it emulate a joystick
			EmulateJoystick();

			// Now we farm out the input to the right recipients
			ProcessInput();
		}

		private static void ProcessInput()
		{
			// Assemble camera control commands and send them to the current camera ship
			TillerData tiller = new TillerData();
			tiller.Joystick = ReadJoystick();												// reads joystick, or mouse/kbd emulation
			if (leftButton)																	// if left mouse btn down, full thrust
				tiller.Thrust = 1.0f;
			else if ((joystick!=null)&&
					(joystickState.GetButtons())[joyThrust]!=0)								// or if main joystick button, full thrust
				tiller.Thrust = 1.0f;
			CameraShip.SteerShip(tiller);													// send data to the current camera ship



		}

		/// <summary>
		/// This method emulates a joystick using the mouse. If the right button is down, dragging
		/// the mouse works the same as the equivalent joystick movements.
		/// The scroll wheel (if present) emulates the throttle.
		/// </summary>
		private static void EmulateJoystick()
		{
			if (dragging)
			{
				virtualJoystick.X = Mouse.X - dragPoint.X;
				virtualJoystick.Y = Mouse.Y - dragPoint.Y;
//				virtualJoystick.Z = Mouse.Z - dragPoint.Z;									// control wheel, if present
			}
		}

		/// <summary>
		/// Read the virtual joystick if the right mouse button is down, or the real one if not.
		/// If the mouse button is not down and there is no joystick attached, return 0,0,0
		/// </summary>
		/// <returns>the xyz of the joystick (z=throttle (or scrollwheel if mouse))</returns>
		public static Vector3 ReadJoystick()
		{
			// if the right mouse button is down, the mouse overrides any real joystick
			if (dragging)
			{
				Vector3 virt = virtualJoystick;

				virt.X /= VIRTUALJOYSTICKRANGE;
				virt.Y /= VIRTUALJOYSTICKRANGE;
//				virt.Z /= VIRTUALJOYSTICKRANGE;

				if (virt.X<-1.0) virt.X = -1.0f;
				else if (virt.X>1.0) virt.X = 1.0f;
				if (virt.Y<-1.0) virt.Y = -1.0f;
				else if (virt.Y>1.0) virt.Y = 1.0f;
//				if (virt.Z<-1.0) virt.Z = -1.0f;
//				else if (virt.Z>1.0) virt.Z = 1.0f;

				// include a dead zone in the centre
				const float dead = 0.05f;
				if ((virt.X>-dead)&&(virt.X<dead))
					virt.X = 0;
				if ((virt.Y>-dead)&&(virt.Y<dead))
					virt.Y = 0;

				return virt;
			}
			
			// if there's no physical joystick, return 0,0,0
			if (joystick==null)
			{
				return Vector3.Empty;
			}
			
			// read the real joystick
			return new Vector3(	(float)joystickState.X / 1000, 
								(float)joystickState.Y / 1000, 
								(float)joystickState.Rz / 1000);        // HACK: my joystick has throttle on Z ROTATION. What do stick+rudder combos do???
		}

		/// <summary>
		/// Handle Windows messages relating to the mouse and keyboard. 
		/// I don't use DirectInput for this because the sample framework requires Windows mouse messages
		/// and DirectInput suppresses them.
		/// Call this from the Engine's message handler
		/// </summary>
		/// <returns>true if it handled this message</returns>
		public static bool OnMsgProc(IntPtr hWnd, NativeMethods.WindowMessage msg, IntPtr wParam, IntPtr lParam)
		{
			// Mouse movements are recorded (but the message is passed on to others)
			if (msg==NativeMethods.WindowMessage.MouseMove)
			{
				Mouse.X = NativeMethods.LoWord((uint)lParam.ToInt32());
				Mouse.Y = NativeMethods.HiWord((uint)lParam.ToInt32());
				if (dragging)
					return true;
				return false;
			}

//			// Mouse wheel movements
//			if (msg==NativeMethods.WindowMessage.MouseWheel)
//			{
//				Mouse.Z = NativeMethods.LoWord((uint)lParam.ToInt32());
//				return false;
//			}

			// If the right button has been pressed, record the mouse location
			// so that drags can be used to drive cameras
			if (msg==NativeMethods.WindowMessage.RightButtonDown)
			{
				dragPoint.X = NativeMethods.LoWord((uint)lParam.ToInt32());
				dragPoint.Y = NativeMethods.HiWord((uint)lParam.ToInt32());
				dragPoint.Z = Mouse.Z;
				dragging = true;
				System.Windows.Forms.Cursor.Hide();
				if (Engine.Framework.IsWindowed)
					NativeMethods.SetCapture(hWnd);
				return true;
			}
			// when the button is released, stop dragging the camera
			if (msg==NativeMethods.WindowMessage.RightButtonUp)
			{
				dragging = false;
				System.Windows.Forms.Cursor.Show();
				if (Engine.Framework.IsWindowed)
					NativeMethods.ReleaseCapture();
				return true;
			}

			// TODO: Left button clicks are handled here
			// (remember only return true if nobody else should have the msg)
			if (msg==NativeMethods.WindowMessage.LeftButtonDown)
			{
				// If we're not emulating a joystick, handle a click as a possible widget or 3D object selection command
				if (!dragging)
				{
					return LeftClick();
				}
				// else just record the state of the button, so that the joystick emulator can treat it as the FIRE button
				leftButton = true;
				return true;
			}
			if (msg==NativeMethods.WindowMessage.LeftButtonUp)
			{
				// If we're not emulating a joystick, handle a release as a possible widget release
				if (!dragging)
				{
					return LeftRelease();
				}
				// else just record the state of the button, so that the joystick emulator can treat it as the FIRE button
				leftButton = false;
				return true;
			}

			// Keydown message
			if (msg==NativeMethods.WindowMessage.KeyDown)
			{
				int key = wParam.ToInt32();								// System.Windows.Forms.Keys enum is wParam
				KeyState[key] = true;									// record new state for hold-down behaviours (tested during Update())
				return HandleKeypress((Keys)key);						// but trigger any keypress behaviours now
			}

			// Keyup message
			if (msg==NativeMethods.WindowMessage.KeyUp)
			{
				int key = wParam.ToInt32();								// System.Windows.Forms.Keys enum is wParam
				key &= 255;												// mask off the control key states
				KeyState[key] = false;
				return HandleKeyRelease((Keys)key);						// but trigger any keyrelease behaviours now
			}




			return false;
		}

		/// <summary>
		/// Left button has been pressed (when we're not in joystick emulation mode).
		/// Pass this to the currently active panel (might be a clicked widget or a 3D pick of a creature)
		/// </summary>
		/// <returns></returns>
		private static bool LeftClick()
		{
			if (CameraShip.CurrentShip.LeftClick(Mouse) == true)
				return true;

			// TODO: Other responses to left clicks go here

			return false;
		}

		/// <summary>
		/// Left button has been released (when we're not in joystick emulation mode).
		/// Pass this to the currently active panel (might be releasing a widget)
		/// </summary>
		/// <returns></returns>
		private static bool LeftRelease()
		{
			if (CameraShip.CurrentShip.LeftRelease(Mouse) == true)
				return true;

			// TODO: Other responses to left release go here

			return false;
		}

		/// <summary>
		/// A key has been pressed - execute a command if appropriate
		/// </summary>
		/// <param name="key">the key pressed - compare to Keys.###</param>
		/// <returns>true if the key was handled</returns>
		private static bool HandleKeypress(Keys key)
		{
			// If an EditBox currently has focus, all keypresses get directed there
			if (EditBox.Focus != null)
				return EditBox.Focus.KeyPress(key);

			// Keypresses common to all modes...
			switch (key)
			{

				// Save a screenshot to disk
				case Keys.S:
					Scene.SaveScreenshot("Screenshot.bmp");
					break;

				// Toggle fullscreen mode
				case Keys.W:
					Engine.Framework.ToggleFullscreen();
					break;

				// DirectX settings dialogue
				case Keys.X:
					Engine.Framework.ShowSettingsDialog(!Engine.Framework.IsD3DSettingsDialogShowing);
					break;

			}

			// Pass all other keys to the current camera ship cockpit
			return CameraShip.CurrentShip.KeyPress(key);
		}

		/// <summary>
		/// A key has been released - pass this through to camera ship, then panel, then widgets
		/// </summary>
		/// <param name="key">the key that was released - compare to Keys.###</param>
		/// <returns>true if the key was handled</returns>
		private static bool HandleKeyRelease(Keys key)
		{
			// Pass all keys to the current camera ship cockpit
			return CameraShip.CurrentShip.KeyRelease(key);
		}

		/// <summary>
		/// Return true if the SHIFT key is currently down
		/// </summary>
		/// <returns></returns>
		public static bool IsShift()
		{
			return KeyState[(int)Keys.ShiftKey];
		}

		/// <summary>
		/// Return true if the CTRL key is currently down
		/// </summary>
		/// <returns></returns>
		public static bool IsCtrl()
		{
			return KeyState[(int)Keys.ControlKey];
		}



	}


}
