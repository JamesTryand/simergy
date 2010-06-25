using System;
using System.Collections.Generic;

namespace Simbiosis
{
	/// <summary>
	/// GantryCam - camera on a gantry that by default looks straight down
	/// anim0 = pan, anim1 = tilt
	/// </summary>
	public class GantryCam : Physiology
	{
		float pan = 0.5f;
		float tilt = 0.5f;

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public GantryCam()
		{
			// Define my properties
			Mass = 1000f;														// an immovable mass that doesn't suffer in collisions
			Resistance = 0f;
			Buoyancy = 0f;
		}


		/// <summary>
		/// no nerves or animation, so just return
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
			JointOutput[0] = pan;
			JointOutput[1] = tilt;
		}

		/// <summary>
		/// We've been sent some steering control data because we are the ROOT cell of
		/// an organism that is currently the camera ship. 
		/// </summary>
		/// <param name="tiller">the joystick/keyboard data relevent to steering camera ships</param>
		/// <param name="elapsedTime">elapsed time this frame, in case motion/animation needs to be proportional</param>
		public override void Steer(TillerData tiller, float elapsedTime)
		{
			// pan and tilt change at a speed determined by joystick position
			pan -= tiller.Joystick.X * elapsedTime * 0.1f;
			if (pan < 0) pan = 0;
			else if (pan > 1) pan = 1;
			tilt -= tiller.Joystick.Y * elapsedTime * 0.1f;
			if (tilt < 0) tilt = 0;
			else if (tilt > 1) tilt = 1;
			//owner.ConsoleMessage("pan rate:"+tiller.Joystick.X);
		}

		/// <summary>
		/// We've been asked if we will accept the role of camera mount.
		/// </summary>
		/// <returns>The index of the hotspot to use, or -1 if we aren't a valid camera mount
		/// OR if we are already the camera mount (since we only have one site available)</returns>
		public override int AssignCamera(IControllable currentOwner, int currentHotspot)
		{
			if (owner == currentOwner)											// if I'm being asked a second time
				return -1;														// say no
			return 0;															// otherwise, accept the camera
		}

	}

}
