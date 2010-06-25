using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	class SurveyPanel : Panel
	{

		SurveyCam owner = null;													// The camera ship that owns me


		public SurveyPanel(SurveyCam owner)
			: base()
		{
			// Store the camera ship for callbacks etc.
			this.owner = owner;

			// Create the backdrop and widgets
			//widgetList.Add(new Widget("SubTop", false, 1024, 100, 0, 0));
			//widgetList.Add(new Widget("SubBot", false, 1024, 168, 0, 768 - 168));



		}

		/// <summary>
		/// Refresh any HUD Display widgets, etc.
		/// </summary>
		public override void SlowUpdate()
		{
		}


	}
}
