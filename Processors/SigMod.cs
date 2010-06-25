using System;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;

// ************************** Signal modifiers *****************************
namespace Simbiosis
{

    /// <summary>
    /// Leaky Integrator: Accumulates input values linearly, and leaks them exponentially. Leakage rate can be set using a const on 2nd input.
    /// </summary>
    class Integrator : Physiology
    {
        private float accumulator = 0;
        

        public Integrator()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]           // All variants look the same (but could use different data for each variant to make the labels more explicit (e.g. Y -> "threshold")
                {
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Output"),	
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 2, 0f, "Input"),
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Leakage rate"),
                },
            };
        }

        public override void FastUpdate(float elapsedTime)
        {
            accumulator += Input(1) * 1f * elapsedTime;
            accumulator *= 1f - (elapsedTime * (Input(2) + 0.1f) * 1f);
            Output(0, accumulator);
        }





    }


    /// <summary>
    /// Generic computing surface.
    /// Constructed from 25 (5x5) points. Given an X,Y in the range 0,0 to 1,1, the output is the interpolated height of the surface at this point.
    /// Surfaces can be used to implement a wide range of static and adaptive functions of two variables, for instance:
    /// - servos
    /// - thresholders
    /// - scaling, adding, inverting, differencing
    /// - AND, OR, XOR (analogue equivalents)
    /// - complex waveforms
    /// - randomness
    /// 
    /// Variants are used to set the surface up for different tasks. Each variant is electrically similar but functionally different.
    /// The surface data are written to the genome and saved with the creature, so that mutations and leraning can occur.
    /// 
    /// 
    /// </summary>
    public class Function : Physiology
    {
        /// <summary> heightfield for this cell. Order is [y,x] </summary>
        private byte[,] heightfield = new byte[5, 5];

        /// <summary> 
        /// memory: records how recently each square has been traversed, for recency effects in learning.
        /// An incrementing (and wrapping) number is stored in each square visited. 
        /// </summary>
        private byte[,] memory = new byte[5, 5];                                // memory of when last visited
        private byte sequencer = 0;                                             // next number in sequence
        private int lastSquareX = -1, lastSquareY = -1;                         // last square visited
        private float happiness = 1.0f;                                         // moving average of success input, for learning

        /// <summary>
        /// Heightfield initialisers for each variant
        /// </summary>
        private static byte[,,] function = {

            {   // SUM: o/p is proportional to x + y (combine two signals, or bias a signal by a constant)
                {000,012,025,037,050},
                {012,025,037,050,062},     
                {025,037,050,062,075},     
                {037,050,062,075,087},     
                {050,062,075,087,100} 
            },
            {   // SCALE: o/p is X * Y (X attenuated by Y or a constant)
                {000,000,000,000,000},
                {000,006,012,019,025},     
                {000,012,025,038,050},     
                {000,019,038,056,075},     
                {000,025,050,075,100} 
            },
            {   // DIFFERENCE: o/p reflects how different X is to Y (abs(y-x))
                {000,025,050,075,100},
                {025,000,025,050,075},     
                {050,025,000,025,050},     
                {075,050,025,000,025},     
                {100,075,050,025,000} 
            },
            {   // EXCESS: o/p reflects how much greater X is than Y
                {000,025,050,075,100},
                {000,000,025,050,075},     
                {000,000,000,025,050},     
                {000,000,000,000,025},     
                {000,000,000,000,000} 
            },
            {   // CLOSENESS: o/p rises the closer X gets to Y
                {100,075,050,025,000},
                {075,100,075,050,025},     
                {050,075,100,075,050},     
                {025,050,075,100,075},     
                {000,025,050,075,100} 
            },
             {  // ISEQUAL: o/p rises towards 1 only when X is almost equal to Y
                {100,000,000,000,000},
                {000,100,000,000,000},     
                {000,000,100,000,000},     
                {000,000,000,100,000},     
                {000,000,000,000,100} 
            },
            {   // ISGREATER: o/p is 1 when X is greater than threshold Y
                // Alternatively, o/p is 1 when Y is less than threshold X
                {000,100,100,100,100},
                {000,000,100,100,100},     
                {000,000,000,100,100},     
                {000,000,000,000,100},     
                {000,000,000,000,000} 
            },
            {   // SERVO: o/p is the likely value needed to bring Y (actual) into line with X (desire).
                // Output is a pseudo-signed number, where 0.5 counts as neutral and numbers less than 0.5 represent "negative" values.
                // SERVO cells can be combined with Navigator cells, which understand pseudo-signed numbers and convert them to a push/pull pair
                {050,062,075,087,100},
                {038,050,062,075,087},     
                {025,038,050,062,087},     
                {012,025,038,050,062},     
                {000,012,025,038,050} 
            },
            {   // AND: o/p is 1 if both X AND Y are 0.5 or higher
                {000,000,000,000,000},
                {000,000,000,000,000},     
                {000,000,100,100,100},     
                {000,000,100,100,100},     
                {000,000,100,100,100} 
            },
            {   // OR: o/p is 1 if either X OR Y are 0.5 or higher
                {000,000,100,100,100},
                {000,000,100,100,100},     
                {100,100,100,100,100},     
                {100,100,100,100,100},     
                {100,100,100,100,100} 
            },
            {   // XOR: o/p is 1 if X but not Y, or Y but not X is 0.5 or higher
                {000,000,100,100,100},
                {000,000,100,100,100},     
                {100,100,000,000,000},     
                {100,100,000,000,000},     
                {100,100,000,000,000} 
            },
           //{   // ABS: When Y is <0.5, o/p is positive when a pseudo-signed number on X is "positive" (above 0.5).
            //    // When Y is >=0.5, o/p is positive when a pseudo-signed number on X is "negative" (below 0.5). 
            //    // Can be used in positive and negative pairs with SERVO cells to decode the pseudo-signed output into a push-pull pair of signals.
            //    {000,000,000,050,100},
            //    {000,000,000,050,100},     
            //    {100,050,000,000,000},
            //    {100,050,000,000,000},     
            //    {100,050,000,000,000} 
            //},
            { 
                {000,000,000,000,000},
                {000,000,000,000,000},     
                {000,000,000,000,000},     
                {000,000,000,000,000},     
                {000,000,000,000,000} 
            },
        };

        // Latest x,y for detecting changes
        float xx = -1, yy = -1;

        public Function()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]           // All variants look the same (but could use different data for each variant to make the labels more explicit (e.g. Y -> "threshold")
                {
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 3, 0f, "Output"),	
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "X"),
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 2, 0f, "Y"),
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 1f, "Learning"),
                },
            };
        
        }

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "sum", "scale", "difference", "excess", "closeness", "isequal", "isgreater", "servo", "AND", "OR", "XOR" };
        }

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
            // Define heightfield according to variant (genes can overwrite this)
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    heightfield[y,x] = function[owner.Variant,y,x];
                }
            }
        }

 
        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            float x = Input(1);
            float y = Input(2);

            // Don't waste time if there's been no change
            if ((xx == x) && (yy == y))
                return;

            xx = x; yy = y;

            x *= 4f; y *= 4f;                                               // cvt point into range 0-4
            int x1 = (int)x; int y1 = (int)y;                               // integer part of x,y (bot-left corner of square)
            int x2 = x1 + 1; int y2 = y1 + 1;                               // top-right corner of square
            if (x2 > 4) x2 = 4;                                             // if point is along top or right edge, don't overflow array
            if (y2 > 4) y2 = 4;
            float dx = x - x1; float dy = y - y1;                           // fractional position within square

            // bilinear interpolation
            float ht = heightfield[y1, x1] 
                + dx * (heightfield[y1, x2] - heightfield[y1, x1])
                + dy * (heightfield[y2, x1] - heightfield[y1, x1])
                + dx * dy * (heightfield[y1, x1] - heightfield[y1, x2] - heightfield[y2, x1] + heightfield[y2, x2]);

            ht /= 100;                                                      // table values range 0-100, but we want output to range 0-1
            Output(0, ht);                                                                                             // send interpolated value to output channel
            owner.SetAnimColour(0, new ColorValue(ht / 2.0f + 0.5f, 0.5f, 0.5f), new ColorValue(0, 0, 0));             // Make cell blush red in proportion to signal level

            // Update memory if necessary
            if ((x1 != lastSquareX) || (y1 != lastSquareY))                 // if we've traversed into a new square
            {
                lastSquareX = x1; lastSquareY = y1;                         // remember it
                memory[y1, x1] = sequencer++;                               // store position in sequence and increment counter (wrapping)
            }

        }

        /// <summary>
        /// Slow update: make any learning modifications to heightfield
        /// </summary>
        public override void SlowUpdate()
        {
            happiness = (happiness * 7f + Input(3)) / 8f;                         // get the happiness/success level as a moving average
            if (happiness == 1.0f)                                                      // if we're at the default setting (or perfect), no learning takes place
                return;

            if (--updateTimer <= 0)
            {
                updateTimer = UPDATERATE;

                for (int y = 0; y < 5; y++)
                {
                    for (int x = 0; x < 5; x++)
                    {
                        float time = TraversalTime(x, y);                                   // how recently the square was traversed
                        float noise = (Rnd.Float(-0.01f, 0.01f)                             // create some Gaussian noise, centred on zero
                            + Rnd.Float(-0.01f, 0.01f) 
                            + Rnd.Float(-0.01f, 0.01f)) / 3f;
                        noise *= time;                                                      // modulate noise by recency (most recent is most noisy)
                        noise *= (1f - happiness);                                          // modulate by how well we're doing (lots of noise if we're doing badly)
                        float height = heightfield[y, x] / 100f;                            // get height as a float 0-1
                        height += noise;                                                    // add noise
                        if (height < 0f) height = 0f;                                       // keep in limits
                        else if (height > 1f) height = 1f;
                        //Debug.WriteLine("Cell="+this.name+" Happiness=" + happiness + " (instantaneous=" + Input(3) + ") old height=" + heightfield[y, x] + " new height=" + (byte)(height * 100f) + " noise=" + noise + " traversed=" + time);
                        heightfield[y, x] = (byte)(height * 100f);                          // return height as a byte 0-100
                    }
                }


            }
        }
        const int UPDATERATE = 4;
        private int updateTimer = UPDATERATE;

        /// <summary>
        /// Return a value representing how long ago (approximately) square x,y was traversed.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>1 = most recent, 0 = least recent</returns>
        private float TraversalTime(int x, int y)
        {
            float trav = 0;
            float time = memory[y, x];
            if (time <= sequencer)
                trav = 1f - ((sequencer - time) / 25f);
            else
                trav = 1f - ((sequencer + 256f - time) / 25f);
            if (trav < 0) trav = 0;
            return trav;
        }


    }



    







    /// <summary>
    /// Latch: a positive signal on channel0 switches; a positive signal on chan1 switches it off again
    /// </summary>
    public class Latch : Physiology
    {
        private const float THRESHOLD = 0.5f;                       // set/reset threshold
        private float sig = 0;                                      // output signal


        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "up", "down" };
        }


        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Latch()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                                                                       // DOWNSTREAM
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Set"),	    	// set input from plug
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0f, "Reset"),    	    // reset input from plug
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S0, 1, 0f, "Output"),	    	// emitter to skt0
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
                },
                new ChannelData[]                                                                       // UPSTREAM
                {
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 1, 0f, "Set"),	    	// collector from skt0
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 0, 0f, "Reset"),    	    // base from skt0
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Output"),	    	// emitter to plug
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
                }
			};
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            if ((Input(0) > THRESHOLD) && (sig == 0f))
            {
                sig = 1f;
                Output(2, sig);
            }
            if ((Input(1) > THRESHOLD) && (sig > 0f))
            {
                sig = 0f;
                Output(2, sig);
            }
        }

        public override void SlowUpdate()
        {
            // Make cell blush red in proportion to output
            owner.SetAnimColour(0, new ColorValue(sig / 2.0f + 0.5f, 0.5f, 0.5f), new ColorValue(0, 0, 0));
        }

    }

    /// <summary>
    /// Monostable: a positive signal on channel0 triggers output for a given length of time; channel1 determines the delay period
    /// </summary>
    public class Monostable : Physiology
    {
        private const float THRESHOLD = 0.5f;                       // trigger threshold
        private const float MAXDELAY = 10.0f;                       // max delay in seconds (when delay i/p = 1.0f)
        private float sig = 0;                                      // output signal
        private float timer = 0;                                    // countdown timer


        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "up", "down" };
        }


        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Monostable()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                                                                       // DOWNSTREAM
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Trigger"),	   	// trigger input from plug
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "DelayTime"),      // delay input from plug
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S0, 1, 0f, "Output"),	    	// emitter to skt0
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
                },
                new ChannelData[]                                                                       // UPSTREAM
                {
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 1, 0f, "Trigger"),	   	// trigger from skt0
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 0, 0.5f,  "DelayTime"), 	    // delay from skt0
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Output"),	    	// emitter to plug
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
                }
			};
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            if (timer > 0)
            {
                timer -= elapsedTime;
                if (timer < 0)
                {
                    timer = 0;
                    Output(2, 0);
                }
            }
            else if ((Input(0) > THRESHOLD) && (sig == 0f))
            {
                Output(2, 1f);
                timer = Input(0) * MAXDELAY;
            }
        }

        public override void SlowUpdate()
        {
            // Make cell blush red in proportion to output
            owner.SetAnimColour(0, new ColorValue(sig / 2.0f + 0.5f, 0.5f, 0.5f), new ColorValue(0, 0, 0));
        }

    }

    /// <summary>
    /// Inverts the input (so 0 in = 1 out)
    /// </summary>
    public class Inverter : Physiology
    {
        private float signal = 0;

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "up", "down" };
        }

        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Inverter()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                                                                       // DOWNSTREAM
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0f, "Input"),	    	// collector from plug
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S0, 1, 0f, "Output"),	    	// emitter to skt0
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
                },
                new ChannelData[]                                                                       // UPSTREAM
                {
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 1, 0f, "Input"),	    	// collector from skt0
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Output"),	    	// emitter to plug
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "Bypass"),		    // bypass channel from plug to skt0
                }
			};

        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            Output(1, signal = 1f - Input(0));
        }

        public override void SlowUpdate()
        {
            // Make cell blush red in proportion to output
            owner.SetAnimColour(0, new ColorValue(signal / 2.0f + 0.5f, 0.5f, 0.5f), new ColorValue(0, 0, 0));
        }

    }


 


    /// <summary>
    /// Navigator: converts signals from dual sensors into swimming or other motion.
    /// 
    /// INPUTS:
    ///     Direction - A value in which 0 means left, 1 means right and 0.5 means straight ahead
    ///     Intensity - An optional input controlling the amplitude of the output signal, from Min to 1.0. By default this is set to a constant 1.0 and has no effect.
    ///                 Setting it to a lower constant reduces the amplitude of the output. Feeding it from the Intensity input of a dual sensor makes the output
    ///                 stronger when the stimulus source is strong/close. Inverting the intensity before feeding it to this input makes the swimming movements
    ///                 get stronger when the stimulus gets weaker.
    ///     Waveform  - An optional input for modulating the output signal. With the default constant 1.0 the output will be a DC voltage that varies with the
    ///                 direction/intensity. Adding a waveform makes the output oscillate with an amplitude determined by direction/intensity, 
    ///                 e.g. to drive flippers.
    ///     Minimum   - An optional value defining the minimum amplitude, so that a creature will still keep swimming even in the absence of a stimulus
    /// 
    /// OUTPUTS
    ///     Left, Right - motor signals for driving flippers, jet propulsion cells, etc. The modulation waveform is modulated according to the direction
    ///                 and intensity of the stimulus. The end result is to propel the creature forwards and steer it towards or away from the stimulus.
    /// 
    /// NOTES:
    /// - This cell is convenient for driving flippers or legs. A rudder/tail or a neck for orienting a head can be driven directly by the dual sensor itself, 
    ///   using a muscle cell whose mid-way position is straight.
    /// - Generally, a Navigator cell will be driven by several sources, according to the current behaviour, routed using a Selector cell.
    /// - Physically rotate the cell to make the creature avoid instead of seeking, 
    /// 
    /// 
    /// </summary>
    public class Navigator : Physiology
    {
        private float totalTime = 0;                // total elapsed time for generating sinewaves
        private float smoothDirection = 0.5f;       // Moving average for smoothing incoming direction values

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "taildriver", "two-way" };
        }

        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Navigator()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                                                                           // TAILDRIVER - drives one muscle to control a tail fin
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0.5f, "Direction"),  	// direction input from sensor
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 2, 1f, "Intensity"),  	// optional intensity signal from sensor
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.3f, "Minimum"),	    // optional minimum
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S0, 0, 0f, "Tail"),		    // motor driver - connect to muscle
                },
                new ChannelData[]                                                                           // TWO-WAY - drives two muscles from a dualsensor
                {
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 1, 0.5f, "Direction"),  	// direction input from sensor
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 2, 1f, "Intensity"),  	// optional intensity signal from sensor
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0f, "Minimum"),	    // optional minimum
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.XX, 0, 1f, "Waveform"),	  	// optional modulating waveform (enters via socket)
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S1, 0, 0f, "Left"),		    // left motor driver
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.S2, 0, 0f, "Right"),		    // right motor driver
                },
			};

        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            switch (owner.Variant)
            {
                case 0: // TAILDRIVER
                    {
                        float direction = Input(0);                                                             // get the inputs
                        float intensity = Input(1);
                        float minimum = Input(2);
                        smoothDirection = (smoothDirection * 1.0f + direction * elapsedTime) / (1.0f + elapsedTime);    // smooth changes in direction
                        totalTime += elapsedTime;                                                               // tally the time
                        float sine = (float)Math.Sin(totalTime * 4.0f) * 0.5f;                                  // create sinewave +/-0.5
                        intensity = intensity * (1f - minimum) + minimum;                                       // cvt intensity into range minimum to 1
                        sine *= intensity;                                                                      // scale flapping by sensor intensity
                        sine += (smoothDirection - 0.5f);                                                       // bias tail according to sensor direction
                        Output(3, sine / 2f + 0.5f);
                        //Debug.WriteLine("dir=" + direction.ToString("0.00") + " smooth = " + smoothDirection.ToString("0.00") + " sine=" + sine.ToString("0.00"));
                    }
                    break;

                case 1: // TWO-WAY
                    {
                        float direction = Input(0);                                                             
                        float intensity = Input(1);
                        float minimum = Input(2);
                        float waveform = Input(3);
                        Output(4, (direction * intensity + minimum) * (1f - minimum) * waveform);
                        Output(5, ((1.0f - direction) * intensity + minimum) * (1f - minimum) * waveform);
                    }
                    break;
            }
        }

    }


}
