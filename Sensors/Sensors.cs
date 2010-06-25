using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;

namespace Simbiosis
{
    /// <summary>
    /// Optical Flow: measures the relative movement of objects and tiles in the vicinity (+/-90 degrees).
    /// Detects moving objects but also triggered when the creature itself moves.
    /// </summary>
    class OpticalFlow : Physiology
    {
        private const float RANGE = 30f;                // visibility distance
        private SensorItem[] memories = null;           // List of objects found at last search (positions tell us whether they've moved between updates)
        private int updateRate = 4;                     // only update once every second (efficient and gives any o/p time to be noticed)

        public OpticalFlow()
        {
            // Define my properties
            Mass = 0.2f;
            Resistance = 0.2f;
            Buoyancy = 0.0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[] 
                { 
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Motion"),	        // output
                },
			};
        }

        public override void SlowUpdate()
        {
            if (--updateRate <= 0)
            {
                updateRate = 4;
                float signal = 0;
                SensorItem[] items = owner.GetObjectsInRange(0, RANGE, true, true, true);           // Get a list of all visible creatures and tiles within range
                foreach (SensorItem item in items)
                {
                    float angle = item.Angle();                                                     // angle of obj from line of sight
                    if (angle < (float)Math.PI / 2f)                                                // if object is within sensor cone
                    {
                        foreach (SensorItem memory in memories)                                     // search for same object in memory
                        {
                            if (item.Object == memory.Object)
                            {
                                float mvt = Vector3.LengthSq(memory.RelativePosition - item.RelativePosition);      // how much object has moved relative to us in past 0.25 secs
                                if (mvt != 0)                                                       // if moved, add abs distance to signal, modulated by how far downrange it is
                                {
                                    signal += (float)Math.Sqrt(mvt) * item.Distance(RANGE);
                                }
                                break;
                            }
                        }
                    }
                }
                memories = items;                                                                   // store new list for next time
                Output(0, signal / 10f);                                                            // output a fraction of the accumulated motion
            }
        }

    }



    /// <summary>
    /// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Speed sensor. Measures speed in the direction of its nose (away from the plug).
    /// Cell has 4 hairs, which stick out when still but flow backwards when cell is moving.
    /// A differentiator cell could be used to turn speed into acceleration.
    /// </summary>
    class Speed : Physiology
    {
        /// <summary> Last position of hair tip (hot4-7) </summary>
        private Vector3 lastPosn = new Vector3();

        /// <summary> Moving average speed </summary>
        private float hairSpeed = 0;


        public Speed()
        {
            // Define my properties
            Mass = 0.2f;
            Resistance = 0.2f;
            Buoyancy = 0.0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[] 
                { 
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Speed"),	        // output
                },
			};
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            // Make hairs flow
            Matrix hotMat = owner.GetHotspotNormalMatrix(0);                                                // position and direction of hotspot
            hotMat.Invert();
            Vector3 oldRelPos = Vector3.TransformCoordinate(lastPosn, hotMat);                              // transform previous position into new posn's frame
            float dist = -oldRelPos.Z;                                                                      // distance hotspot has travelled along its normal axis
            lastPosn = owner.GetHotspotLocation(0);                                                         // remember latest position

            float speed = dist / elapsedTime / 4f;                                                          // Speed is distance / time
            hairSpeed = (hairSpeed * 31f + speed) / 32f;                                                    // keep a moving average for smooth animation
            if (hairSpeed < 0) hairSpeed = 0;
            JointOutput[0] = JointOutput[1] = JointOutput[2] = JointOutput[3] = hairSpeed;                  // drive hairs
            Output(0, hairSpeed);                                                                           // output signal
        }
        



    }





    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Passive sensor: detects narrow-spectrum bioluminescence emitted from special bioluminescence cells,
    /// allowing one creature to recognize another or detect the mood of another.
    /// Output is a pulse that decays over about a second
    /// </summary>
    class SpectralSensor : Physiology
    {

        /// <summary> Sensor range </summary>
        const float range = 60;

        /// <summary> Adjustable acceptance angle in radians either side of the line of sight </summary>
        const float halfAngle = (float)Math.PI / 4.0f;

        /// <summary> Current filter colour </summary>
        float r=0, g=0, b=0;

        /// <summary> instantaneous signal strength (received from stimuli) </summary>
        float incoming = 0;

        /// <summary> mean signal strength integrated over time </summary>
        float output = 0;


        public SpectralSensor()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0.0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[] 
                {                                                                                                   // SOLE VARIANT
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Signal"),	    	        // output
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0f, "Filter colour"),		    // colour sensitivity
                },
			};

        }

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
            ReadFilter();
        }


        public override void FastUpdate(float elapsedTime)
        {
            base.FastUpdate(elapsedTime);

            // If we have recently received a signal via a stimulus, add it to the output
            output += incoming;
            incoming = 0;
            output /= 1f + elapsedTime/1f;                                                                             // output decays over time
            Output(0, output);
        }


        /// <summary>
        /// Called on a SlowUpdate tick (about 4 times a second).
        /// Read/write/modify your sensory/motor nerves and/or chemicals, to implement your behaviour
        /// </summary>
        public override void SlowUpdate()
        {
            ReadFilter();
        }

        /// <summary>
        /// Get the filter's RGB from the signal on input 1.
        /// The colours run smoothly from red, through yellow to green and cyan to blue (no magenta)
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
            if (freq >= 0.5f)
            {
                r = 0;
                b = (freq - 0.5f) * 2f;
                g = 1f - b;
            }
            this.owner.SetAnimColour(0,new ColorValue(r, g, b), new ColorValue(0,0,0));
        }

        /// <summary> We've been sent a Stimulus that our basic Cell object doesn't understand.
        /// This overload responds to the "bioluminescence" stimulus
        /// Parameter 0 will be a ColorValue containing the bioluminescent cell's current anim colour
        /// <param name="stimulus">The stimulus information</param>
        /// <returns>Return true if the stimulus was handled</returns>
        public override bool ReceiveStimulus(Stimulus stim)
        {
            if (stim.Type == "bioluminescence")
            {
                // Find out if the sender is within range/sight of our hotspot
                SensorItem sender = owner.TestStimulusVisibility(0, range, stim);
                float dist = sender.Distance();
                if (dist<range)
                {
                    float angle = sender.Angle();
                    if (angle < halfAngle)
                    {
                        // Parameter 0 will be a ColorValue containing the bioluminescent cell's current anim colour
                        ColorValue light = (ColorValue)stim.Param0;

                        // Compare the light to our filter colour
                        float r1 = light.Red - r;                                                   // difference in RGB between filter and cell
                        float g1 = light.Green - g;
                        float b1 = light.Blue - b;
                        float signal = 1f - (float)Math.Sqrt((r1 * r1 + g1 * g1 + b1 * b1) / 3f);   // least squares measure of similarity (1=identical)
                        signal *= 1f - angle / halfAngle;                                           // scale by deviation from mid-line (so objects straight ahead have more effect)

                        // NOTE: Removed this because bioluminescent cells are obviously tiny at long range
                        //float apparentSize = sender.AngleSubtended();                               // scale by angle subtended (depends on size and distance)
                        //if (apparentSize < 0) apparentSize = 0;
                        //signal *= apparentSize;

                        // Add this signal to that waiting for inclusion in the output
                        incoming += signal;
                    }
                }
                return true;
            }
            else return false;
        }


    }

    /// <summary>
    /// ///////////////////////////////////////////////////////////////////////////////////////
    /// Colour-specific light sensor - Active sensor. Measures amount of a given colour (not counting terrain and water) within the field of view.
    /// Signal is a product of:
    ///     - the closeness of the object
    ///     - the size of the object
    ///     - the similarity of the colour
    ///     - the nearness to the centre of the field of view
    /// 
    /// Channel 0 = output. Amount of current colour
    /// Channel 1 = optional input. Defines the colour of the filter (RGB values close to this will be detected)
    /// Channel 2 = optional input. Defines the selectivity of the filter (high values are broad; 0 is highly selective)
    /// 
    /// </summary>
    class ColorSensitive : Physiology
    {
        /// <summary> Sensor range </summary>
        const float range = 40;

        /// <summary> Adjustable acceptance angle in radians either side of the line of sight </summary>
        const float halfAngle = (float)Math.PI / 4.0f;

        /// <summary> Current filter colour </summary>
        float r=0, g=0, b=0;

        public ColorSensitive()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[] 
                {                                                                                                   // SOLE VARIANT
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Signal"),	    	        // output
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0f, "Filter colour"),		    // colour sensitivity
                },
			};

        }

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
        }


        //public override void FastUpdate(float elapsedTime)
        //{
        //    base.FastUpdate(elapsedTime);
        //}


        /// <summary>
        /// Called on a SlowUpdate tick (about 4 times a second).
        /// Read/write/modify your sensory/motor nerves and/or chemicals, to implement your behaviour
        /// </summary>
        public override void SlowUpdate()
        {
            ReadFilter();
            ReadSignal();
        }

        /// <summary>
        /// Get the filter's RGB from the signal on input 1.
        /// The colours run smoothly from red, through yellow to green and cyan to blue (no magenta)
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
            if (freq >= 0.5f)
            {
                r = 0;
                b = (freq - 0.5f) * 2f;
                g = 1f - b;
            }
            this.owner.SetAnimColour(0,new ColorValue(r, g, b), new ColorValue(0,0,0));
        }

        /// <summary>
        /// Read the sensor and output the signal
        /// </summary>
        private void ReadSignal()
        {
            float signal = 0;
            float total = 0;
            SensorItem[] items = owner.GetObjectsInRange(0, range, true, false, true);  // Get a list of all visible creatures (but NOT tiles) within range
            foreach (SensorItem item in items)
            {
                float angle = item.Angle();                                             // angle of obj from line of sight
                if (angle < halfAngle)                                                  // if object is within sensor cone
                {
                    List<ColorValue> colours = item.Object.ReqSpectrum();               // get the spectral response of the organism (its cell colours)
                    
                    // Measure the amount of light from each cell that will pass through the filter
                    foreach (ColorValue c in colours)
                    {
                        float r1 = c.Red-r;                                             // difference in RGB between filter and cell
                        float g1 = c.Green-g;
                        float b1 = c.Blue-b;

                        signal += 1f - (float)Math.Sqrt((r1*r1 + g1*g1 + b1*b1)/3f);    // least squares measure of similarity
                    }
                    signal /= colours.Count;                                            // average by # colours

                    signal *= 1f - angle/halfAngle;                                     // scale by deviation from mid-line (so objects straight ahead have more effect)

                    float apparentSize = item.AngleSubtended();                         // scale by angle subtended (depends on size and distance)
                    if (apparentSize < 0) apparentSize = 0;
                    signal *= apparentSize;

                    total++;                                                            // tally # objects found
                }
            }
            if (total > 0)                                                              // If we found one or more objects...
            {
                signal *= 1f - ((IDetectable)owner).ReqDepth();                         // scale overall signal by the depth of the sensor (i.e. light level)     
                signal *= 10f;                                                          // scale into a useful range
                if (signal < 0f) signal = 0f;                                           // clamp output
                if (signal > 1f) signal = 1f;

                Output(0, signal);				                                        // signal is our primary output
            }
            else
                Output(0, 0);                                                           // ...else if no objects in sight, o/p will be zero
        }



    }







	/// <summary>
    /// ///////////////////////////////////////////////////////////////////////////////////////
	/// Sonar - Active sensor. Measures distance to nearest obstruction (terrain or creature).
	/// Equally sensitive to all obstructions less than given angle from the sensor axis.
    /// 
    /// Channel 0 = output. Distance of nearest object
    /// Channel 1 = optional input. If connected, triggers a ping. If unconnected, pings are continuous but use more energy.
    /// 
	/// </summary>
	class Sonar : Physiology
	{
		/// <summary> Adjustable range - default is 20 </summary>
		private float range = 20;

		/// <summary> Adjustable acceptance angle in radians either side of the line of sight </summary>
		private float halfAngle = (float)Math.PI / 4.0f;

        /// <summary> trigger signal must exceed this level to start a ping </summary>
        private const float TRIGGERTHRESHOLD = 0.5f;

        /// <summary> how many cycles of animation for a ping </summary>
        private const int PINGCYCLES = 2;

        /// <summary> state machine for handling pings & echoes </summary>
        private States state = States.Standby;
        private enum States
        {
            Standby,
            Pinging,
        };

        /// <summary> Animated transducer </summary>
        private float animPosn = 0;                         // current angle
        private float animDelta = 0.9f;                     // rate of increase/decrease
        private int animCount = 0;                          // number of cycles to go


		public Sonar()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[] 
                {                                                                                                   // SOLE VARIANT
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Distance"),	    	    // output - distance of nearest echo
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 1f, "Trigger (optional)"),		// triggers a "ping"
                },
			};

		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
		}


        public override void FastUpdate(float elapsedTime)
        {
            base.FastUpdate(elapsedTime);

            // Update any ping animation
            if (state == States.Pinging)                                                                    
            {
                animPosn += elapsedTime * animDelta;                                                        // if we're pinging, move the transducer
                JointOutput[0] = animPosn;

                if ((animPosn < 0) || (animPosn > 1.0f))                                                    // if it has reached the limit
                {
                    animDelta = -animDelta;                                                                 // turn it around and bring back into range
                    animPosn = (animPosn < 0) ? 0 : 1.0f;
                    if (--animCount <= 0)                                                                   // if this was the last cycle
                    {
                        ReadEcho();                                                                         // read the sensor
                        state = States.Standby;                                                             // and go to standby
                    }
                }
            }
        }


		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// Read/write/modify your sensory/motor nerves and/or chemicals, to implement your behaviour
		/// </summary>
		public override void SlowUpdate()
		{
            // If we're in standby, decide if we should start a new ping (on trigger, or repeatedly if no trigger connection)
            if (state == States.Standby)
            {
                // if there's a trigger signal (constant or driven), start a ping
                if (Input(1) > TRIGGERTHRESHOLD)
                {
                    state = States.Pinging;
                    animCount = PINGCYCLES;
                }
            }
		}

        /// <summary>
        /// Read the sensor and output the distance
        /// </summary>
        private void ReadEcho()
        {
            float signal = 0;                                                           // largest 'sonar echo' so far found
            SensorItem[] items = owner.GetObjectsInRange(0, range, true, true, false);  // Get a list of all creatures and tiles within range
            foreach (SensorItem item in items)
            {
                float angle = item.Angle();                                             // angle of obj from line of sight
                if (angle < halfAngle)                                                  // if object is within sensor cone
                {
                    float dist = item.Distance(range);                                  // get range-relative distance to obj
                    if (dist > signal)
                        signal = dist;                                                  // if this is closest so far, keep it
                }
            }
            Output(0, signal);				                                            // sonar signal is our primary output

        }



	}








    /// <summary>
    /// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Vibration sensor. Passive. Detects ripples of disturbance from actuators (including the submarine fans)
    /// Has an acceptance angle of 180 degrees, so putting one on each side of a creature gives it an idea of the source direction.
    /// </summary>
    class Vibration : Physiology
    {
        /// <summary> Adjustable range - default is 20 </summary>
        private float range = 20;

        /// <summary> 
        /// Acceptance angle in radians either side of the line of sight. 
        /// (less than 180 degrees total, to shadow own body) 
        /// </summary>
        private const float HALFANGLE = (float)Math.PI / 2.0f * 0.8f;


        public Vibration()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                                                                                   // SOLE VARIANT
                    {
    				new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Signal strength"),		// output channel to plug - distance
                    },
			};

        }

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {

        }

 
        public override bool ReceiveStimulus(Stimulus stim)
        {
            switch (stim.Type)
            {
                // Potentially picked up a disturbance
                case "disturbance":
                    SensorItem result = owner.TestStimulusVisibility(0, range, stim);                       // see if the source is within our acceptance angle
                    if (result.Angle()<HALFANGLE)
                    {
                        Output(0,result.Distance(range));                                                   // if so, output a signal as a fraction of the source range
                    }
                    return true;
            }

            Output(0,0);                                                                                    // if no valid sensation, clear the signal
            return false;
        }
 

    }

 





}
