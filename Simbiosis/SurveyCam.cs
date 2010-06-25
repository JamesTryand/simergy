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
	/// The SurveyCam - top-down steady view for tracking and surveying
	/// </summary>
	class SurveyCam : CameraShip
	{

		public SurveyCam()
			: base("GantryCam",							// GantryCam
				new Vector3(Map.MapWidth / 2.0f, 35.0f, Map.MapHeight / 2.0f + 30.0f),
				new Orientation(0.0f, 0.0f, 0.0f))
		{
			// Set relevant Renderable.FlagBits
			Dynamic = false;										// The surveycam doesn't respond to forces - it moves on 'rails'

			// Create the panels
			panel[0] = new SurveyPanel(this);
			// TODO: add secondary panel here

		}


	}



}
