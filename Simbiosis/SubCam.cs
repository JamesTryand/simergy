using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.Samples.DirectX.UtilityToolkit;
using System.Windows.Forms;

namespace Simbiosis
{
	/// <summary>
	/// The submarine
	/// </summary>
	class SubCam : CameraShip
	{
		private static Texture spotTex = null;						// projection texture for the spotlight
		private static bool spotState = false;						// true if the spotlight is switched on


		public SubCam()
			: base("Submarine",
				new Vector3(Map.MapWidth / 2.0f, 25.0f, Map.MapHeight / 2.0f),
				new Orientation(0.0f, 0.0f, 0.0f))
		{
			// Create the panels
			panel[0] = new SubPanel(this);
            panel[1] = new SubPanel2(this);

			// Load the spotlight texture
			spotTex = TextureLoader.FromFile(Engine.Device, FileResource.Fsp("textures", "SpotlightSub.png"));
			Fx.SetSpotlightTexture(spotTex);
			Fx.Spotlight = spotState;		
		}

		/// <summary>
		/// We have become the new camera ship. 
		/// </summary>
		protected override void Enter()
		{
			base.Enter();

            // Take over the spotlight
            Fx.SetSpotlightTexture(spotTex);
            Fx.Spotlight = spotState;
        }

		/// <summary>
		/// We are about to stop being the current camera ship. 
		/// </summary>
		protected override void Leave()
		{
			base.Leave();

		}

		public override void Update()
		{
			base.Update();

			if (spotState == true)
			{
				Fx.SetSpotlightPosition(Matrix.Invert(rootCell.GetHotspotNormalMatrix(0)) * Camera.ProjMatrix);
			}
			

		}

		/// <summary>
		/// Switch spotlight on/off
		/// </summary>
		public void Switch1Changed(Widget sender, Object value)
		{
			spotState = (bool)value;
			Fx.Spotlight = spotState;
		}

		


	}
}
