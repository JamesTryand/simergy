using System;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;

// Signal generators

namespace Simbiosis
{

    /// <summary>
    /// Oscillator: Produces various regular waveforms, according to variant:
    /// Sine
    /// Square
    /// Flapping (reverse ramp)
    /// 
    /// </summary>
    public class Oscillator : Physiology
    {
        static float[,] waveform = new float[4, 100];                                   // output values for 1/100ths of a cycle in each type of wave
        const int FL = 0, SI = 1, SQ = 2, PU = 3;                                       // uncluttered enum

        private float accumulator = 0;													// we calc a scaled increment each frame and add it to this
        private float currSpeed = 0;                                                    // current speed (only re-sampled at end of each phase, to avoid glitches)

        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Oscillator()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]   // All variants look the same
                {
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0f, "Output"),		    // output channel to plug - signal
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Speed"),	   	// input channel from plug - controls speed
                },
			};

            // Define the waveforms...

            // Square (soften the shoulders to prevent infinite rates of change)
            for (int i = 0; i < 50; i++)
            {
                waveform[SQ, i] = 1.0f;
            }
            waveform[SQ, 0] = 0.25f; waveform[SQ, 1] = 0.50f; waveform[SQ, 2] = 0.75f;
            waveform[SQ, 50] = 0.75f; waveform[SQ, 51] = 0.50f; waveform[SQ, 52] = 0.25f; 

            // Pulse (low MK/SPC square wave with soft shoulders; not a true pulse because this might not get noticed by downstream cells)
            for (int i = 0; i < 10; i++)
            {
                waveform[PU, i] = 1.0f;
            }
            waveform[PU, 0] = 0.25f; waveform[PU, 1] = 0.50f; waveform[PU, 2] = 0.75f;
            waveform[PU, 10] = 0.75f; waveform[PU, 11] = 0.50f; waveform[PU, 12] = 0.25f; 

            // Sine (unsigned)
            for (int i = 0; i < 100; i++)
            {
                waveform[SI,i] = (float)(Math.Sin(((double)i) / 100.0 * Math.PI * 2.0) / 2.0 + 0.5);
            }

            // 1:5 ramp (for swimming/flapping motion)
            for (int i = 0; i < 20; i++)
            {
                waveform[FL, i] = i / 20.0f;
            }
            for (int i = 20; i < 100; i++)
            {
                waveform[FL, i] = 1.0f - (i-20) / 80.0f;
            }

        }

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
            accumulator = Rnd.Float(99f);                                                         // start each instance off at a different phase
            GetSpeed();
        }

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "flap", "sine", "square", "pulse" };
        }

        /// <summary>
        /// Read & scale speed input (do this only at the start of each phase, to avoid glitches caused by sudden acceleration mid-flap)
        /// </summary>
        private void GetSpeed()
        {
            currSpeed = Input(1) * 200f + 10f;
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        public override void FastUpdate(float elapsedTime)
        {
            accumulator += elapsedTime * currSpeed;
            if (accumulator >= 100f)
            {
                accumulator = 0f;
                GetSpeed();
            }

            float val = waveform[owner.Variant, (int)accumulator];
            Output(0, val);                                                                                             // send wave table entry to output channel

            owner.SetAnimColour(0, new ColorValue(val / 2.0f + 0.5f, 0.5f, 0.5f), new ColorValue(0, 0, 0));             // Make cell blush red in proportion to signal level
        }

    }

















}
