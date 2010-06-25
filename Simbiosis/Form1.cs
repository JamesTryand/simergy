using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using DirectInput = Microsoft.DirectX.DirectInput;

// Note: add Microsoft.DirectX.xxx references to project if reusing this code

namespace Simbiosis3D
{
	/// <summary>
	/// Render Target Form
	/// </summary>
	public class D3DForm : System.Windows.Forms.Form
	{

		#region Application options
		/// <summary> True if this app should start up in windowed mode </summary>
		const bool STARTWINDOWED = true;
		/// <summary> If true, we'd like to use w-buffering if available (else z-buffering) </summary>
		const bool USEWBUFFER = true;
		#endregion

		#region Static variables, to enable all objects to access their D3D form and device easily
		/// <summary> Our one-and-only D3D form </summary>
		public static D3DForm form = null;
		public static D3DForm Form	{ get { return form; } }
		/// <summary> Our one-and-only D3D device </summary>
		public static Device device = null;
		public static Device Device	{ get { return device; } }
		#endregion


		/// <summary> are we running/starting up in a window? </summary>
		private bool iswindowed = STARTWINDOWED;	
		/// <summary> Current viewport size (window client or screen size if full-screen)</summary>
		private int viewportwidth;
		private int viewportheight;
		/// <summary> Performance counters for monitoring resource use </summary>
		public PerformanceCounter PCmem = null;
		
		#region useful device capabilities or choices based on those capabilities
		/// <summary> if false, we have chosen to do our vertex processing in software </summary>
		public bool CapsVertexHardware = true;	
		/// <summary> # lights we can show simultaneously </summary>
		public int CapsMaxActiveLights;	
		/// <summary> if true, we have chosen to use a w-buffer not a z-buffer </summary>
		public bool CapsWBuffer = false;				
		#endregion
		
		
		
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public D3DForm()
		{
			// Required for Windows Form Designer support
			InitializeComponent();

			// tell Windows not to do any form repainting of
			// its own (to prevent backgnd repaint causing flicker)
			this.SetStyle(ControlStyles.AllPaintingInWmPaint |		
						  ControlStyles.Opaque, true);

			// prevent a zero client size causing an exception in OnPaint()
			Size s = new Size(80,80);								
			this.MinimumSize = s;

			// Create performance counters for monitoring resources
			PCmem = new PerformanceCounter("Memory","Available KBytes");			// heap remaining
			

		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(D3DForm));
			// 
			// D3DForm
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(808, 589);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "D3DForm";
			this.Text = "Sim-biosis engine";

		}
		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			// VStudio default code = Application.Run(new D3DForm());
			// but we need to show the form to create its window handle before
			// we can create the D3D device
			using (D3DForm form = new D3DForm())
			{
				// Show our form and initialize our graphics engine
				form.Show();
				form.InitGraphics();								// initialise D3D device
				Input.Init();										// set up the input devices
				Scene.Setup();										// Set up the scene
				Application.Run(form);								// Start the message loop
			}

		}


		/// <summary>
		/// OnPaint override - redraw scene
		/// </summary>
		/// <param name="e">event args</param>
		protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
		{
			// clear the render target & the z-buffer
			device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, 
						 System.Drawing.Color.White, 1.0f, 0);

			// render the scene
			device.BeginScene();
			Scene.Render();
			device.EndScene();
			device.Present();

			// cause an immediate repaint, so that scene is constantly redrawn!
			this.Invalidate();
		}


	
		/// <summary>
		/// Set up the D3D device
		/// </summary>
		public void InitGraphics()
		{

			// check caps of default device
			Caps caps = Manager.GetDeviceCaps(0,DeviceType.Hardware);
			AdapterInformation adapterInfo = Manager.Adapters[0];
			PresentParameters pp = new PresentParameters();

			// we'll use hw T&L if available unless the h/w is really crap (e.g. doesn't support decent lighting)
			// TODO: add any other caps-driven requirements
			CapsVertexHardware = caps.DeviceCaps.SupportsHardwareTransformAndLight;		
			Debug.WriteLine(String.Format("CAPS: Hardware vertex processing = {0}",CapsVertexHardware));
			if ((caps.VertexProcessingCaps.SupportsDirectionalLights==false)||			
				(caps.VertexProcessingCaps.SupportsPositionalLights==false))
			{
				CapsVertexHardware = false;
				Debug.WriteLine("Vertex h/w turned off because no positional lights support!");
			}
			// We'll use a W-buffer if possible, since that causes fewer hidden surface artifacts when the ratio
			// between the near and far clip planes is large (as it is for terrain models)
			if (caps.RasterCaps.SupportsWBuffer == true)
				CapsWBuffer = true;
			Debug.WriteLine(String.Format("CAPS: Using W-buffering = {0}",CapsWBuffer));

			// record other capabilities that might influence processing or scene parameters
			CapsMaxActiveLights = caps.MaxActiveLights;

			// Set our presentation parameters according to whether we are starting in windowed or full-screen mode
			if (iswindowed)
			{
				pp.Windowed = true;								// Start in windowed mode
				pp.SwapEffect = SwapEffect.Discard;				// Let D3D decide best swapchain method
				// HACK: PresentInterval.Immediate = MUCH FASTER WITH SMALL SCENES BUT SOME GLITCHES!!!
				// Probably good for testing frame rate while debugging, but ceases to be much use once normal
				// frame rate approaches screen refresh rate. PresentInterval.Immediate essentially takes off the
				// minimum wait between frames.
				pp.PresentationInterval = PresentInterval.Immediate;	
			}
			else
			{
				pp.BackBufferFormat = adapterInfo.CurrentDisplayMode.Format;
				pp.BackBufferHeight = adapterInfo.CurrentDisplayMode.Height;
				pp.BackBufferWidth = adapterInfo.CurrentDisplayMode.Width;
				pp.DeviceWindow = this;
				// PresentInterval.Immediate is necessary for full-screen use
				pp.PresentationInterval = PresentInterval.Immediate;
				pp.SwapEffect = SwapEffect.Discard;
				pp.Windowed = false;
			}

			pp.EnableAutoDepthStencil = true;
			pp.AutoDepthStencilFormat = DepthFormat.D16;


			// Create our device
			device = new Device(0,										// default adapter
								DeviceType.Hardware,					// must have h/w rendering
								this,									// window handle of render target
								(CapsVertexHardware==true)?
								CreateFlags.HardwareVertexProcessing :	// Vertex processing type depends on caps
								CreateFlags.SoftwareVertexProcessing,
								pp);

			// Set up event handlers
			device.DeviceReset += new System.EventHandler(this.DeviceResetEventHandler);

			// if we are using w-buffering, switch that on (Scene.OnReset() will re-enable it after any device reset
			if (CapsWBuffer==true)
				device.RenderState.UseWBuffer = true;

			// Set up initial window/screen size so that projection matrix will calculate correct aspect ratio
			SetViewportSize();

			// Store references to the form and device in static variables to make it easy for other objects to
			// obtain them
			D3DForm.form = this;
			D3DForm.device = device;

		}

		/// <summary>
		/// Device has been reset (window change etc.)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void DeviceResetEventHandler(object sender, EventArgs e)
		{

			ScreenText.AddLine("Device reset");

			// Update window/screen size so that projection matrix will calculate correct aspect ratio
			SetViewportSize();

			Scene.OnReset();
			// TODO: CHANGE SO EACH CLASS ADDS ITS OWN HANDLER TO DEVICE!!!
		}

		/// <summary>
		/// Calculate initial or post-reset size of viewport (screen area or window client area)
		/// </summary>
		private void SetViewportSize()
		{
			if (iswindowed)
			{
				viewportwidth = this.Width;
				viewportheight = this.Height;
			}
			else
			{
				AdapterInformation adapterInfo = Manager.Adapters[0];
				viewportwidth = adapterInfo.CurrentDisplayMode.Width;
				viewportheight = adapterInfo.CurrentDisplayMode.Height;
			}
		}


		/// <summary> Get screen or window client width </summary>
		public int ViewportWidth
		{
			get { return viewportwidth; } 
		}

		/// <summary> Get screen or window client height </summary>
		public int ViewportHeight
		{
			get { return viewportheight; } 
		}
	
	
	
	
	}
}

