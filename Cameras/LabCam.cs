using System;
using System.Collections.Generic;
using System.Text;

namespace Simbiosis
{
	/// <summary>
	/// Laboratory main part - the shell of the ship, including the camera mount in one wall and the specimen clamp
	/// </summary>
	class LabStructure : Physiology
	{
        /// <summary>
        /// Pan and tilt of the clamp
        /// </summary>
		float pan = 0.5f;
		float tilt = 0.5f;

        /// <summary>
        /// Pan and tilt RATE of the lab camera
        /// </summary>
        float cameraPanRate = 0;
        float cameraTiltRate = 0;

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public LabStructure()
		{
			// Define my properties
			Mass = 1000f;
			Resistance = 0f;
			Buoyancy = 0f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
		}



		/// <summary>
		/// We've been asked if we will accept the role of camera mount.
		/// </summary>
		/// <returns>The index of the hotspot to use, or -1 if we aren't a valid camera mount
		/// OR if we are already the camera mount (since we only have one site available)</returns>
		public override int AssignCamera(IControllable currentOwner, int currentHotspot)
		{
            JointOutput[3] = 0.5f;                                              // start with camera pointing straight ahead
            JointOutput[4] = 0.5f;

            if (owner == currentOwner)											// if I'm being asked a second time
				return -1;														// say no
			return 0;															// otherwise, accept the camera
		}


		/// <summary>
		/// set clamp joints
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
            JointOutput[2] = pan;
            JointOutput[1] = tilt;

            JointOutput[3] += cameraPanRate;
            if (JointOutput[3] > 1) JointOutput[3] = 1;
            else if (JointOutput[3] < 0) JointOutput[3] = 0;
            JointOutput[4] += cameraTiltRate;
            if (JointOutput[4] > 1) JointOutput[4] = 1;
            else if (JointOutput[4] < 0) JointOutput[4] = 0;

        }

		/// <summary>
		/// We've been sent some steering control data because we are the ROOT cell of
		/// an organism that is currently the camera ship. 
		/// </summary>
		/// <param name="tiller">the joystick/keyboard data relavent to steering camera ships</param>
		/// <param name="elapsedTime">elapsed time this frame, in case motion/animation needs to be proportional</param>
		public override void Steer(TillerData tiller, float elapsedTime)
		{
			// Clamp pan and tilt change at a speed determined by joystick position
			pan += tiller.Joystick.Y * elapsedTime * 0.3f;
			if (pan < 0) pan = 0;
			else if (pan > 1) pan = 1;
			tilt += tiller.Joystick.X * elapsedTime * 0.3f;
			if (tilt < 0) tilt = 0;
			else if (tilt > 1) tilt = 1;

		}

        /// <summary>
        /// We've been sent a button command from a control panel to steer the lab camera
        /// </summary>
        /// <param name="c">One of the Physiology.Command tokens</param>
        /// <param name="c">The name of the button or other widget that has been pressed/released/changed</param>
        /// <param name="state">Widget state (type depends on the button, e.g. bool for pushbuttons, float for knobs)</param>
        /// <param name="elapsedTime">frame time</param>
        public override void Command(string c, object state, float elapsedTime)
        {
            float rate = elapsedTime * 0.3f;
            bool btnState = (bool)state;                        // our param is a bool

            switch (c)
            {
                case "down":
                    cameraTiltRate = (btnState) ? -rate : 0;
                    break;
                case "up":
                    cameraTiltRate = (btnState) ? rate : 0;
                    break;
                case "left":
                    cameraPanRate = (btnState) ? rate : 0;
                    break;
                case "right":
                    cameraPanRate = (btnState) ? -rate : 0;
                    break;

            }
        }








	}
}
