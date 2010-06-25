using System;
using System.Collections.Generic;

namespace Simbiosis
{
	/// <summary>
	/// The user's submarine (the normal camera location).
	/// 
	/// PUT THIS IN A DLL OF ITS OWN, TO ALLOW "UPGRADES"???
	/// 
	/// class Submarine is the main part, with five sockets (right jet, left jet, right spinner, left spinner, dome)
	/// and one hotspot (camera, mounted in pilot's window)
	/// 
	/// PROPULSION / STEERING
	/// The main sub is negatively bouyant (heavy enough to fall suitably quickly when the dome has zero Buoyancy).
	/// The dome is positively bouyant and normally counteracts the weight of the sub, making the whole thing neutral.
	/// The dome's Buoyancy can be increased/decreased under user control for ascent/descent.
	/// The main engines are the spinners at the back, each of which has a hotspot and produces thrust. Steering drives these
	/// independently for yaw.
	/// There are swivelling jets on the sides, which produce pitch.
	/// Any roll is a result of the dynamics (or collisions) and will be damped out by the dome Buoyancy.
	/// 
	/// Main body sends control signals to other parts using these nerves:
	/// Yang 0 (nerve 9) = right jet
	/// Yang 1 = left jet (kept separate so that they can swivel independently for roll control if appropriate
	/// Yang 2 = right spinner (propulsion)
	/// Yang 3 = left spinner
	/// Yang 4 = dome (Buoyancy)
	/// 
	/// </summary>


	/// <summary>
	/// Main part of sub
	/// </summary>
	public class Submarine : Physiology
	{
		// Propulsion hotspots
		float top = 0;
		float bot = 0;
		float left = 0;
		float right = 0;
		float rightDown = 0;
		float leftDown = 0;

        const float YAWRATE = 0.7f;         // controls maximum rate of horizontal turn

        /// <summary>
        /// Pan and tilt of the observation bubble camera
        /// </summary>
        float pan = 0.5f;
        float tilt = 0.5f;

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Submarine()
		{
			// Define my properties
			Mass = 10f;						// pretty heavy - scatters creatures it hits
			Resistance = 0.5f;				// increase resistance to reduce run-on
			Buoyancy = -1f;				// weight counterbalances Buoyancy of flotation dome

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                           
                {
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S0, 1, 0f, "Buoyancy"),
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S1, 1, 0f, "Right"),
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S2, 1, 0f, "Left"),
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S3, 1, 0f, "Top"),
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S4, 1, 0f, "Bot"),
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S5, 1, 0f, "RightDown"),
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S6, 1, 0f, "LeftDown"),
                }
			};
        }

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
            Output(0, 0.5f);                                        // initially neutral Buoyancy
		}


		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// </summary>
		public override void SlowUpdate()
		{
		}

		/// <summary>
		/// Called every frame
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
			// Update all our signals
			base.FastUpdate(elapsedTime);

            JointOutput[1] = pan;
            JointOutput[0] = tilt;
        }


		/// <summary>
		/// We've been asked if we will accept the role of camera mount.
		/// </summary>
		public override int AssignCamera(IControllable currentOwner, int currentHotspot)
		{
            if (owner.CurrentPanel() == 0) return 0;                            // if panel[0] (main control panel) is showing, our camera looks out the front
            if (owner.CurrentPanel() == 1) return 1;                            // if panel[1] (observation bubble) is showing, we look out the top
            return -1;															// else don't accept camera
        }

		/// <summary>
		/// We've been sent some steering control data because we are the ROOT cell of
		/// an organism that is currently the camera ship. 
		/// </summary>
		/// <param name="tiller">the joystick/keyboard data relevant to steering camera ships</param>
		/// <param name="elapsedTime">elapsed time this frame, in case motion/animation needs to be proportional</param>
		public override void Steer(TillerData tiller, float elapsedTime)
		{
            // in panel 1, the joystick controls the pilot's head (camera), to look around the scene from the observation bubble
            if (owner.CurrentPanel() == 1)
            {
                pan -= tiller.Joystick.Y * elapsedTime * 0.3f;
                if (pan < 0) pan = 0;
                else if (pan > 1) pan = 1;
                tilt -= tiller.Joystick.X * elapsedTime * 0.3f;
                if (tilt < 0) tilt = 0;
                else if (tilt > 1) tilt = 1;
            }

            // In panel 0, the joystick controls the thrusters
            else
            {
                // Convert the joystick xy into four steering thrust values for right, left, top, bottom fans
                bot = tiller.Joystick.Y * 0.25f;
                top = -bot;
                left = tiller.Joystick.X * 0.25f;
                right = -left;

                // add main thrust equally to all (NOTE: total of steering + thrust must be in range +/-1 at this point)
                float thrust = tiller.Thrust * 0.5f;
                top += thrust;
                bot += thrust;
                left += thrust;
                right += thrust;

                // supply a bit of the left/right thrust to the downward facing jets to add some roll to the turn
                leftDown = tiller.Joystick.X * 0.15f;
                rightDown = -leftDown;

                // pump the thrust values into the nervous system (as unsigned values, where 0.5 is neutral)
                Output(2, left / 2f + 0.5f);
                Output(1, right / 2f + 0.5f);
                Output(4, bot / 2f + 0.5f);
                Output(3, top / 2f + 0.5f);
                Output(6, leftDown / 2f + 0.5f);
                Output(5, rightDown / 2f + 0.5f);
            }
		}

        /// <summary>
        /// We've been sent a button command from a control panel because we are the ROOT cell of
        /// an organism that is currently the camera ship.
        /// Overload this method if you need to respond to the button(s), e.g. to steer the view camera or a spotlight from the cockpit
        /// by changing one or more anim# jointframes
        /// </summary>
        /// <param name="c">The name of the button or other widget that has been pressed/released/changed</param>
        /// <param name="state">Widget state (type depends on the button, e.g. bool for pushbuttons, float for knobs)</param>
        /// <param name="elapsedTime">frame time</param>
        public override void Command(string c, object state, float elapsedTime)
        {
            switch (c)
            {
                // A knob on the panel controls our Buoyancy 
                case "buoyancy":
                    Output(0, (float)state);
                    break;

                // Other panel commands go here

            }

        }

	}

	

	/// <summary>
	/// Submarine propulsion fan
	/// </summary>
	public class SubFan : Physiology
	{
        private const float RIPPLERANGE = 10.0f;    // Maximum distance over which ripples of disturbance from fan can be detected
        private float rippleCounter = 0;            // used to slow down rate of ripple stimuli to once per second

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public SubFan()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 0.4f;						// increase resistance to increase roll/pitch stability when thrust is cancelled
			Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]
                {
    				new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Speed"),		// input channel from plug - controls speed
                }
			};

		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
		}


		/// <summary>
		/// Called every frame
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
			// Update nerve signals
			base.FastUpdate(elapsedTime);

			// Use input nerve to determine thrust (convert to signed value)
			float thrust = Input(0) * 2f - 1f;
			owner.JetPropulsion(0, thrust * 100f);

			// animate the fan at a speed and direction proportional to thrust
			JointOutput[0] += thrust * elapsedTime * 16;
			if (JointOutput[0] > 1.0f)
				JointOutput[0] = 0;
			else if (JointOutput[0] < 0)
				JointOutput[0] = 1;
		}

        /// <summary>
        /// Called on a SlowUpdate tick (about 4 times a second).
        /// </summary>
        public override void SlowUpdate()
        {
            // About once a second, send out a ripple of disturbance (a stimulus) if the fan is moving, to frighten the fish
            if (++rippleCounter > 3)
            {
                rippleCounter = 0;
                if ((Input(0)>0.6f)||(Input(0)<0.4f))
                    Disturbance(Input(0) * RIPPLERANGE);
            }
        }

	}

	/// <summary>
	/// Submarine dome - Adds some variable Buoyancy at the top to keep the sub roughly upright and allow control of ascent/descent
	/// </summary>
	public class SubDome : Physiology
	{
		const float BUOYANCYNEUTRAL = 1f;						// neutral Buoyancy is when ours exactly counters that of main sub
		const float BUOYANCYRANGE = 0.5f;						// neutral +/- this amount = permissible range

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public SubDome()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 0.0f;									// increase resistance if nose pitched down when power applied
			Buoyancy = BUOYANCYNEUTRAL;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Buoyancy"),		// input channel from plug - controls weight
                }
			};

		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
		}


		/// <summary>
		/// no nerves or animation, so just return
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
			// Update nerve signals
			base.FastUpdate(elapsedTime);

			// Input nerve controls Buoyancy (cvt into range = +/-1)
			float input = Input(0) * 2f - 1f;
            if ((input > -0.1f) && (input < 0.1f)) input = 0;                                             // Create a null zone, so that knob is easier to zero
			Buoyancy = BUOYANCYNEUTRAL + input * BUOYANCYRANGE;
			//			owner.ConsoleMessage("Buoyancy : "+Buoyancy);
		}

	}



}
