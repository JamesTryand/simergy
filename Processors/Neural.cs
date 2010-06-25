using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;


namespace Simbiosis
{
    /// <summary>
    /// Implements a learning servo, which learns to produce (DC) outputs that reduce the difference between
    /// a DESIRE signal and an ACTUAL signal.
    /// 
    /// The output is calculated from a surface defined by four points at the ends of each axis: 
    /// 
    /// Z defines the value of an output parameter for a given X and Y, where X is the desire and Y is the actual.
    /// 
    /// 
    /// </summary>
    public class LinearServo : Physiology
    {
        private float desire = 0.5f;                    // current inputs
        private float actual = 0.5f;
        private float lastDesire = -1f;                 // check for changes, so we don't waste time calculating surface levels unnecessarily
        private float lastActual = -1f;

        /// <summary>
        /// The four corner points describing the surface. 
        /// The corners are stored in the order x0,y0; x1;y0; x0,y1; x1,y1
        /// </summary>
        private float[] surface = new float[4];

        /// <summary>
        /// x,y coordinates of the four corners, in order
        /// </summary>
        private float[] x = { 0, 1, 0, 1 };
        private float[] y = { 0, 0, 1, 1 };

        /// <summary>
        /// current rate of change for each corner due to adaptation
        /// </summary>
        private float[] velocity = new float[4];



        public LinearServo()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                                                                           // DOWNSTREAM
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0f, "Desired"),	   	// input channel - desired state
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Actual"),		    // input channel - actual state
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S0, 0, 0f, "Output"),	    	// output channel - response
 				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
               },
			    new ChannelData[]                                                                           // UPSTREAM
                {
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 0, 0f, "Desired"),	   	// input channel - desired state
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 1, 0f, "Actual"),		    // input channel - actual state
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 0, 0f, "Output"),	    	// output channel - response
  				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
              }
			};
        }

                /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
            // initialise the response curve. Start with just a DC signal whose output tries to negate the difference between
            // desire and actual, as in a conventional servo. Treat an o/p of 0.5 as neutral.
            // In other words, when desire is less than actual, o/p is a low value (<0.5)
            // and when desire > actual, o/p is higher than 0.5
            surface[0] = 0.5f;
            surface[1] = 1.0f;
            surface[2] = 0.0f;
            surface[3] = 0.5f;          
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            desire = Input(0);
            actual = Input(1);

            if ((desire != lastDesire) || (actual != lastActual))
            {
                // Store the present set
                lastActual = actual;
                lastDesire = desire;

                // Get new output from surface
                Output(0, ReadSurface(desire, actual));
            }
            
        }

        /// <summary>
        /// Slow update: adjust surface through learning.
        /// The more A differs from D, the less well the servo is doing in this region of the surface. 
        /// But we don't know the direction of the error, so calculate whether we're getting worse/better faster or slower.
        /// If we're getting worse faster, we need to shift the surface in the other direction.
        /// Each corner is given a rate of change. This rate is adjusted in proportion to how close the current A,D is to the corner
        /// (i.e. how much influence this corner has on the relevant part of the surface).
        /// 
        /// If the error is not zero, add a small corner velocity.
        /// if we're getting better, increase the velocity in the current direction.
        /// If we're getting worse, add a force in the opposite direction, to slow the velocity and cross zero.
        /// The faster we're getting better or worse, the more force should be applied.
        /// So, force = de.
        /// 
        /// 
        /// </summary>
        public override void SlowUpdate()
        {
            base.SlowUpdate();

            // We only need to adapt servo every few seconds or so
            if (--adaptCount < 0)
            {
                adaptCount = 40;

                // How are we doing?
                float e = (float)Math.Abs(desire - actual);                                     // magnitude of error
                float de = laste - e;                                                           // rate of change of error (<0 if we're getting worse)
                laste = e;

                Debug.WriteLine("Desire=" + desire + " Actual=" + actual + " Error=" + e + " DeltaError=" + de);

                // for each corner, adjust its rate of change in proportion to how much it influences the current D,A region of the surface.
                for (int i = 0; i < 4; i++)
                {
                    // Calc D,A distance from this corner (keep it as the square for speed)
                    float dist = (desire - x[i]) * (desire - x[i]) + (actual - y[i]) * (actual - y[i]);
                    // Calculate new force to accelerate/reverse motion
                    float force = de * (1.415f-dist) * 0.01f;                                    // 1.415 because the largest distance is root 2
                    // Add current force to velocity
                    velocity[i] += force;
                    // add velocity to corner height
                    surface[i] += velocity[i];
                    if (surface[i] < 0) surface[i] = 0;
                    else if (surface[i] > 1) surface[i] = 1;

                    Debug.WriteLine("corner=" + x[i] + "'" + y[i] + " force=" + force + " velocity=" + velocity[i] + " height=" + surface[i]);
                }

            }
        }

        private float laste = 0;                                                            // last error magnitude for calculating delta
        private int adaptCount = 0;                                                         // countdown to next learning update

        /// <summary>
        /// Read the height of a given surface by interpolating the four corners
        /// Simplified bilinear interpolation is:
        /// f(x,y) = f(0,0) * (1-x) * (1-y) + f(1,0) * x * (1-y) + f(0,1) * (1-x) * y + f(1,1) * x * y
        /// </summary>
        /// <param name="desire">current x value</param>
        /// <param name="actual">current y value</param>
        /// <returns>z value at x,y</returns>
        private float ReadSurface(float desire, float actual)
        {
            return surface[0] * (1 - desire) * (1 - actual)
                + surface[1] * desire * (1 - actual)
                + surface[2] * (1 - desire) * actual
                + surface[3] * desire * actual;
        }

    }

    /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ Finish later? ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    /// <summary>
    /// Implements a learning, nonlinear "servo", which learns to produce outputs that reduce the difference between
    /// a DESIRE signal and an ACTUAL signal.
    /// 
    /// The output is calculated from a series of 3D graphs, each defining the value of a parameter.
    /// Each graph is a surface defined by four points at the ends of each axis: 
    /// 
    /// Z defines the value of an output parameter for a given X and Y, where X is the desire and Y is the actual.
    /// 
    /// Each parameter controls the nature of the output signal, which has an attack/decay envelope.
    /// The parameters are:
    /// - Attack period
    /// - Sustain period. If this is zero, the decay starts immediately after the attack
    /// - Sustain level
    /// - Decay period
    /// - Pause period. If this is zero, the sustain is infinite, giving us a DC output at the sustain level
    /// - pause level (level after decay and before next attack)
    /// 
    /// The envelope is integrated (moving average) to smooth it a little.
    /// 
    /// Whenever the desire or actual change, the new value for the present phase of the cycle replaces the old, so an infinite sustain might 
    /// turn into an oscillation, etc.
    /// 
    /// 
    /// 
    /// 
    /// 
    /// </summary>
    public class NonlinearServo : Physiology
    {
        /// <summary>
        /// Phases of the cycle
        /// </summary>
        private enum State
        {
            Pause,
            Attack,
            Sustain,
            Decay
        };

        /// <summary>
        /// Surfaces
        /// </summary>
        private enum Sfc
        {
            AttackPeriod,
            SustainPeriod,
            SustainLevel,
            DecayPeriod,
            PausePeriod,
            PauseLevel,

            NUMSURFACES
        };

        /// <summary>
        /// Scaling factors for the periods. 
        /// A factor of 1 means that a surface value of 1 will create a period of 1 second.
        /// A factor of 10 means the maximum period will last 10 seconds
        /// </summary>
        private float[] scale = {
            5f,                                         // max attack
            5f,                                         // max sustain
            1f,                                         // dummy - this is a level, not a period
            5f,                                         // max decay
            5f,                                         // max pause
            1f                                          // dummy - this is a level, not a period
        };

        private float lastDesire = -1f;                 // check for changes, so we don't waste time calculating surface levels unnecessarily
        private float lastActual = -1f;

        private float signal = 0f;                      // current output
        private State state = State.Pause;              // state of the system
        private float time = 0f;                        // time until next phase starts, in seconds

        /// <summary>
        /// The four corner points describing each of the six surfaces. 
        /// The corners are stored in the order x0,y0; x1;y0; x0,y1; x1,y1
        /// </summary>
        private float[,] surface = new float[(int)Sfc.NUMSURFACES, 4];

        public NonlinearServo()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Bouyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                                                                           // DOWNSTREAM
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0f, "Desired"),	   	// input channel - desired state
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Actual"),		    // input channel - actual state
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S0, 0, 0f, "Output"),	    	// output channel - response
                },
			    new ChannelData[]                                                                           // UPSTREAM
                {
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 0, 0f, "Desired"),	   	// input channel - desired state
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 1, 0f, "Actual"),		    // input channel - actual state
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 0, 0f, "Output"),	    	// output channel - response
                }
			};
        }

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
            // initialise the response curves. Start with just a DC signal whose output tries to negate the difference between
            // desire and actual, as in a linear servo. Treat an o/p of 0.5 as neutral.
            // In other words, when desire is less than actual, o/p is a low value (<0.5)
            // and when desire > actual, o/p is higher than 0.5

            // Attack period: inverted servo, so that if actual < desire, attack gets faster (this will be ignored until pause!=0)
            SetSurface(Sfc.AttackPeriod, 0.5f, 0f, 1f, 0.5f);

            // Sustain period: start with one second (this will be ignored until pause!=0)
            float v = 1f / scale[(int)Sfc.SustainPeriod] * 1f;
            SetSurface(Sfc.SustainPeriod, v, v, v, v);

            // Sustain level: standard servo pattern - 0.5 when desire==actual, <0.5 when desire < actual, >0.5 when desire > actual
            SetSurface(Sfc.SustainLevel, 0.5f, 1f, 0f, 0.5f);

            // Decay period: start with one second (this will be ignored until pause!=0)
            v = 1f / scale[(int)Sfc.DecayPeriod] * 1f;
            SetSurface(Sfc.DecayPeriod, v, v, v, v);

            // Pause period: start with flat 0, so that there is no AC component and only the servoed sustain level counts
            SetSurface(Sfc.PausePeriod, 0, 0, 0, 0);

            // Pause level: start with flat zero
            SetSurface(Sfc.PauseLevel, 0, 0, 0, 0);

        }

        private void SetSurface(Sfc sfc, float x0y0, float x1y0, float x0y1, float x1y1)
        {
            surface[(int)sfc, 0] = x0y0;
            surface[(int)sfc, 1] = x1y0;
            surface[(int)sfc, 2] = x0y1;
            surface[(int)sfc, 3] = x1y1;
        }


        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            float desire = Input(0);
            float actual = Input(1);

            if ((desire != lastDesire) || (actual != lastActual))
            {
                // Store the present set
                lastActual = actual;
                lastDesire = desire;

                // Load




            }

        }

        /// <summary>
        /// Read the height of a given surface by interpolating the four corners
        /// Simplified bilinear interpolation is:
        /// f(x,y) = f(0,0) * (1-x) * (1-y) + f(1,0) * x * (1-y) + f(0,1) * (1-x) * y + f(1,1) * x * y
        /// </summary>
        /// <param name="sfc">index of the surface to be read</param>
        /// <param name="desire">current x value</param>
        /// <param name="actual">current y value</param>
        /// <returns>z value at x,y</returns>
        private float ReadSurface(int sfc, float desire, float actual)
        {
            return surface[sfc, 0] * (1 - desire) * (1 - actual)
            + surface[sfc, 1] * desire * (1 - actual)
            + surface[sfc, 2] * (1 - desire) * actual
            + surface[sfc, 3] * desire * actual;
        }

    }

    ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/

}
