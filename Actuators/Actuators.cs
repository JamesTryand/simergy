using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;

namespace Simbiosis
{
    /// <summary>
    /// Bioluminescent cells of various shapes.
    /// Each one emits pulses of "narrow band" light that can be detected by specialised bioluminescence detectors.
    /// </summary>
    public class Bioluminescence : Physiology
    {
        private float pulseTimer = 0;                   // countdown timer
        private float fader = 0;                        // fades light level after pulse
        private float r = 0, g = 0, b = 0;              // current pulse's colour

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "default" };
        }

        public Bioluminescence()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 0.2f;
            Buoyancy = 1.0f; ////////// TEMP!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Speed"),            // pulse rate
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Colour"),           // wavelength
                }
			};

		}

        public override void Init()
        {
            pulseTimer = Rnd.Float(2f);
        }

        public override void FastUpdate(float elapsedTime)
        {
            base.FastUpdate(elapsedTime);

            // If it's time for a pulse, emit it now (along with the stimulus)
            pulseTimer -= elapsedTime;
            if (pulseTimer <= 0)
            {
                pulseTimer += (1.1f - Input(0)) * 5f;                                                           // Reset timer to current channel rate: 0=slow, 1=fast
                ReadFilter();                                                                                   // Find out what colour to use
                fader = 1;                                                                                      // start the pulse
                EmitStimulus("bioluminescence", -1, 60, (float)Math.PI * 2, new ColorValue(r, g, b), null, null, null);  // Emit a stimulus in all directions
                // TODO: May need to change range and/or direction for different variants
            }

            // Pulse appears to decay over time
            if (fader > 0)
            {
                owner.SetAnimColour(0, new ColorValue(r / 4f, g / 4f, b / 4f), new ColorValue(r * fader, g * fader, b * fader));
                fader -= elapsedTime;
            }
        }

        /// <summary>
        /// Get the filter's RGB from the signal on input 1.
        /// The colours run smoothly from red, through yellow to green and cyan to blue (no magenta)
        /// THIS FUNCTION MATCHES THAT IN THE BIOLUMINESCENCE SENSOR
        /// </summary>
        private void ReadFilter()
        {
            float freq = Input(1);
            if (freq < 0.5f)
            {
                r = (0.5f - freq) * 2f;
                g = 1f - r;
                b = 0;
            }
            else
            {
                r = 0;
                b = (freq - 0.5f) * 2f;
                g = 1f - b;
            }
        }


   }



	/// <summary>
	/// Sexual reproductive organ
    /// 
    /// The cell has two "labia" (doors) that can open. The labia are also colour-animateable.
    /// The sex of the creature is decided randomly (?) at birth, i.e. when the reproductive organ is constructed.
    /// 
    /// In males, the labia are red and normally shut. When channel 0 is triggered, the labia open and a "sperm" (organism) is released (consumes energy).
    /// 
    /// In females, the labia are pink and normally open. If a sperm comes close by, the creature is fertilised (assuming an egg is present - requires energy).
    /// A baby creature is produced (with a small core scaling factor). The labia open and the baby is released.
    /// Female organ glows pinker when channel 0 is triggered. This could be used as a signal that the creature is ready to mate.
	/// 
	/// 
	/// </summary>
	public class SexOrgan : Physiology
	{
        private static bool nextSex = false;                                            // used so that births alternate between sexes, to keep population even

        private bool Female = true;                                                     // true if female; false if male
        private float animTimer = 0;                                                    // used to smooth opening/closing or colour changes

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
        public SexOrgan()
		{
			// Define my properties
			Mass = 0.2f;
			Resistance = 0.2f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Activate"),		// input from plug - males open labia; females glow
                }
			};

		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
            // Decide the sex of the cell, and hence the creature that owns me
            Female = nextSex;
            nextSex = !nextSex;

            // Set the initial colour of the "labia"
            if (Female)
            {
                owner.SetAnimColour(0, new ColorValue(0.5f, 0.6f, 0.6f), new ColorValue(0, 0, 0));
                owner.SetAnimColour(1, new ColorValue(0.5f, 0.6f, 0.6f), new ColorValue(0, 0, 0));
            }
            else
            {
                owner.SetAnimColour(0, new ColorValue(0.1f, 0f, 0.9f), new ColorValue(0, 0, 0));
                owner.SetAnimColour(1, new ColorValue(0.1f, 0f, 0.9f), new ColorValue(0, 0, 0));
            }

        }

		/// <summary>
        /// Slow update:
        /// Set colour or animation state according to sex and signal on channel 0.
        /// Decide if/when to release sperm, release baby, etc.
		/// </summary>
		public override void SlowUpdate()
		{
			base.SlowUpdate();

            // Female labia glow pink when there's a nerve signal
            if (Female)
            {
                animTimer = (animTimer * 15f + Input(0)) / 16f;
                owner.SetAnimColour(0, new ColorValue(animTimer / 2f + 0.5f, 0.6f, 0.6f), new ColorValue(animTimer / 2f, 0, 0));
                owner.SetAnimColour(1, new ColorValue(animTimer / 2f + 0.5f, 0.6f, 0.6f), new ColorValue(animTimer / 2f, 0, 0));
            }

            // Male labia open when there's a nerve signal
            else
            {
                animTimer = (animTimer * 15f + Input(0)) / 16f;
                JointOutput[0] = animTimer;
                JointOutput[1] = animTimer;
            }

            // TODO: If male animTimer gets close to 1, emit a sperm...
            // TODO: If female is ready to give birth (fertile egg and enough energy) do it now...


		}

	}





    /// <summary>
    /// Muscular joints of various shapes and (single) axes
    /// Inputs set muscle position (through a moving average for smoothness)
    /// Min and max angle can be defined chemically. If the minimum is greater than the maximum, the muscle flexes in the 
    /// opposite sense (e.g. a contractor gets longer instead of smaller as the signal rises)
    /// </summary>
    public class Joint : Physiology
    {
        /// <summary>
        /// Used for creating ripples of disturbance when the cell moves
        /// </summary>
        private float lastState = 0;
        private const float RIPPLEMINDELTA = 0.1f;          // how much a muscle signal must have changed between slow updates in order to create a ripple
        private const float RIPPLEMAGNITUDE = 10.0f;        // how much to magnify the delta by when calculating the range of the ripple

        private float smooth = 0;                           // moving average to smooth movements
        private const float SLEWRATE = 0.1f;                // speed of moving average

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "Flex", "Contract", "Expand", "Twist" };
        }

        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Joint()
        {
            // Define my properties
            Mass = 0.2f;
            Resistance = 0.2f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]       // All variants are chemically identical
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Angle"),		// input channel from plug - controls angle
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0f, "Minimum"),    // minimum rotation
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 1f, "Maximum"),	// maximum rotation
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		// bypass channel from plug to skt0
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		// bypass channel from plug to skt0
                },
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
        /// Nerves and JointOutputs should normally be updated here
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            smooth += (Input(0) - smooth) * elapsedTime * 10f;

            float angle = (Input(2) - Input(1)) * smooth + Input(1);
            if (angle < 0f) angle = 0f;
            if (angle > 1f) angle = 1f;

            for (int j = 0; j < JointOutput.Length; j++)
                JointOutput[j] = angle;								// write one nerve signal to the muscle joint(s)
        }

        /// <summary>
        /// Called on a SlowUpdate tick (about 4 times a second).
        /// </summary>
        public override void SlowUpdate()
        {
            // If we've twitched rapidly enough, send out a ripple of disturbance as a stimulus
            float delta = Math.Abs(Input(0) - lastState);
            if (delta > RIPPLEMINDELTA)
            {
                Disturbance(RIPPLEMAGNITUDE * delta);
                lastState = Input(0);
                //Debug.WriteLine("                Organism " + owner.OwnerOrganism() + " twitched a muscle");
            }
        }
    }

    /// <summary>
    /// Muscular joint with three degees of freedom
    /// </summary>
    public class Joint3Axis : Physiology
    {
        /// <summary>
        /// Used for creating ripples of disturbance when the cell moves
        /// </summary>
        private float[] lastState = new float[3];
        private const float RIPPLEMINDELTA = 0.1f;          // how much a muscle signal must have changed between slow updates in order to create a ripple
        private const float RIPPLEMAGNITUDE = 10.0f;        // how much to magnify the delta by when calculating the range of the ripple

        private float[] smooth = new float[3];              // moving average to smooth movements
        private const float SLEWRATE = 0.1f;                // speed of moving average


        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Joint3Axis()
        {
            // Define my properties
            Mass = 0.2f;
            Resistance = 0.2f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]       // All variants are chemically identical
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Yaw"),		// axis1
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Pitch"),    // axis2
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Roll"),	    // axis3 (twist)
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		// bypass channel from plug to skt0
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		// bypass channel from plug to skt0
                },
			};

        }

        /// <summary>
        /// Called every frame
        /// Nerves and JointOutputs should normally be updated here
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            for (int i = 0; i < 3; i++)
            {
                smooth[i] += (Input(i) - smooth[i]) * elapsedTime * 10f;
                JointOutput[i] = smooth[i];	
            }
        }

        /// <summary>
        /// Called on a SlowUpdate tick (about 4 times a second).
        /// </summary>
        public override void SlowUpdate()
        {
            // If we've twitched rapidly enough, send out a ripple of disturbance as a stimulus
            for (int i = 0; i < 3; i++)
            {
                float delta = Math.Abs(Input(i) - lastState[i]);
                if (delta > RIPPLEMINDELTA)
                {
                    Disturbance(RIPPLEMAGNITUDE * delta);
                    lastState[i] = Input(i);
                    //Debug.WriteLine("                Organism " + owner.OwnerOrganism() + " twitched a muscle");
                }
            }
        }
    }




	/// <summary>
	/// SpinySucker - toothed mouth for sucking energy
	/// 
	/// INPUT 0 = teeth position (0=relaxed, 1=gripping)
	/// 
	/// </summary>
	public class SpinySucker : Physiology
	{

        /// <summary>
        /// Used for creating ripples of disturbance when the cell moves
        /// </summary>
        private float lastState = 0;
        private const float RIPPLEMINDELTA = 0.1f;          // how much a muscle signal must have changed between slow updates in order to create a ripple
        private const float RIPPLEMAGNITUDE = 10.0f;        // how much to magnify the delta by when calculating the range of the ripple


		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public SpinySucker()
		{
			// Define my properties
			Mass = 0.2f;
			Resistance = 0.2f;
            Buoyancy = -0.05f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Gripper"),		// input channel from plug - controls grip
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
		/// Nerves and JointOutputs should normally be updated here
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
			// Update nerve signals
			base.FastUpdate(elapsedTime);

			for (int j = 0; j < JointOutput.Length; j++)
				JointOutput[j] = Input(0);						    		// write one nerve signal to all the tooth joints
		}

        /// <summary>
        /// Called on a SlowUpdate tick (about 4 times a second).
        /// </summary>
        public override void SlowUpdate()
        {
            base.SlowUpdate();

            // If we've twitched rapidly enough, send out a ripple of disturbance as a stimulus
            float delta = Math.Abs(Input(0) - lastState);
            if (delta > RIPPLEMINDELTA)
            {
                Disturbance(RIPPLEMAGNITUDE * delta);
                lastState = Input(0);
            }
        }


	}

    /// <summary>
    /// Jaw: two moving parts, normally open. They snap shut as soon as the input is non-zero. If the input is permanently non-zero, they'll snap repeatedly.
    /// The snapping action stops as soon as the animation reaches 1.0 or both jaw-tip hotspots are touching an organism.
    /// If there's a bite then we try to control the other creature like the lab clamp does, by altering its position (but not orientation).
    /// The other creature can free itself by separating enough from one of the hotspots, or at random, or because our jaws have exhausted their local energy store.
    /// 
    /// </summary>
    class Jaw : Physiology
    {
        private enum State
        {
            Relaxed,                                    // Normal, open state
            Closing,                                    // jaws are closing
            Biting,                                     // jaws have succeeded in gripping something
            Missed,                                     // jaws got all the way closed without biting anything
            Opening,                                    // Jaws are opening
        };

        private const float CONTACTDIST = 0.1f;         // radius within which prey is counted as in contact with the jaw hotspots
        
        private State state = State.Relaxed;            // Current state
        private float angle = 0;                        // Jaw position
        private IDetectable prey = null;                // any creature currently trapped in jaw

        public Jaw()
        {
            // Define my properties
            Mass = 0.6f;
            Resistance = 0.6f;
            Buoyancy = -0.05f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Bite"),           // Input: start closing the jaws (when non-zero)
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 0, 0f, "Eating"),         // Output: 1 if we've caught something
                }
			};
        }

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "1" };
        }

        public override void FastUpdate(float elapsedTime)
        {
            // Get our current local energy level
            // TODO: ADD CODE FOR READING THE LOCAL ENERGY STORE HERE. FOR NOW ASSUME LIMITLESS ENERGY.
            float energy = 1f;

            switch (state)
            {
                // Relaxed: If input > 0 and we have enough energy, start closing
                case State.Relaxed:
                    if ((Input(0) > 0.02f) && (energy > 0f))
                    {
                        angle = 0;
                        Output(1, 0);
                        state = State.Closing;
                        Disturbance(30);                                        // emit a vibration disturbance
                    }
                    break;

                // In the act of closing: animate and check for hit or miss
                case State.Closing:
                    angle += elapsedTime * 1.5f;                                // Close a bit more
                    if (angle > 1.0f)                                           // fully closed? We didn't catch anything
                    {
                        state = State.Missed;
                    }
                    else if (Caught() == true)                                  // caught something?
                    {
                        Output(1, 1f);                                          // send an output to that effect
                        state = State.Biting;
                    }
                    else
                    {
                        JointOutput[0] = JointOutput[1] = angle;
                    }
                    break;

                // We've closed over nothing: open if input goes zero or we run out of energy
                case State.Missed:
                    if ((energy == 0f)||(Input(0) < 0.01f))
                    {
                        Output(1, 0);
                        state = State.Opening;
                    }
                    break;

                // We've closed over something. Suck energy while we can, until we're told to let go, the critter escapes, or we run out of energy
                case State.Biting:
                    if ((energy == 0f) || (Input(0) < 0.01f))                   // let go if told to or out of energy
                    {
                        Output(1, 0);
                        state = State.Opening;
                    }
                    else if (Escaped() == true)
                    {
                        Output(1, 0);
                        state = State.Closing;                                  // if the critter escapes, close jaws uselessly
                    }
                    else
                    {
                        Suck(elapsedTime);                                      // else extract some energy
                    }
                    break;

                // We're opening after releasing or running out of energy
                case State.Opening:
                    angle -= elapsedTime * 0.6f;                                // Open a bit more
                    if (angle < 0f)                                             // fully open?
                    {
                        state = State.Relaxed;
                    }
                    else
                    {
                        JointOutput[0] = JointOutput[1] = angle;
                    }
                    break;

            }
        }

        /// <summary>
        /// If the hotspots have contacted a critter, store a ref to it, note its relative position and return true
        /// </summary>
        /// <returns></returns>
        private bool Caught()
        {
            SensorItem[] nearHot0 = owner.GetObjectsInRange(0, CONTACTDIST, true, false, false);                    // if hotspot0 is near a creature
            if (nearHot0.Length > 0)
            {
                SensorItem[] nearHot1 = owner.GetObjectsInRange(0, CONTACTDIST, true, false, false);                // and hotspot1 is also near it
                for (int i = 0; i < nearHot0.Length; i++)
                {
                    for (int j = 0; j < nearHot1.Length; j++)
                    {
                        if (nearHot0[i].Object == nearHot1[j].Object)
                        {
                            prey = nearHot0[i].Object;                                                              // grab it
                            return true;
                        }
                    }
                }
            }
            prey = null;
            return false;
        }

        /// <summary>
        /// If critter has escaped our clutches for some reason, return true
        /// </summary>
        /// <returns></returns>
        private bool Escaped()
        {
            SensorItem[] nearHot0 = owner.GetObjectsInRange(0, CONTACTDIST, true, false, false);
            for (int i = 0; i < nearHot0.Length; i++)
            {
                if (nearHot0[i].Object == prey)
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Extract some energy from the currently bit critter while we can.
        /// </summary>
        /// <param name="elapsedTime">how much energy to suck this frame</param>
        private void Suck(float elapsedTime)
        {

        }

    }



}
