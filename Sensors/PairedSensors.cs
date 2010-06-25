using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;

namespace Simbiosis
{



    /// <summary>
    /// Dual passive sensor: detects narrow-spectrum bioluminescence emitted from special bioluminescence cells,
    /// allowing one creature to navigate towards another (e.g. prey, mate).
    /// 
    /// The overall sensor has an acceptance angle of 270 degrees in the "horizontal" direction, and 180 degrees "vertically" (imagine 2 hemispheres at 90 degrees to each other).
    /// The horizontal receptive field is made from three 90-degree zones.
    /// If the source of the stimulus is more than 45 degrees left of centre, the direction output (chan0) will be zero.
    /// If it is more than 45 degrees right of centre it will read 1.0
    /// In the middle 90 degree zone, the value will rise from 0 to 1 as the stimulus moves from the left to the right.
    /// When the stimulus is dead ahead (or there is no perceptible source of stimuli) the value will be 0.5
    /// 
    /// As well as the direction output there is an intensity oputput (chan1). This rises from 0 towards 1 as the stimulus moves closer or gets more intense.
    /// 
    /// Parts:
    /// 
    /// anim0 - cell membrane around sensor 0 (left). Gets coloured to show intensity of signal at this sensor
    /// anim1 - cell membrane around sensor 1 (right).
    /// anim2, anim3 - the filters over each sensor. Both get recoloured to show the wavelength of light that the sensors are currently receptive to
    /// 
    /// hot0, hot1 - sensor hotspots. These should be directed 45 degrees either side of straight ahead.
    /// </summary>
    class DualSpectral : Physiology
    {

        /// <summary> Sensor range </summary>
        const float range = 60;

        /// <summary> If no new stimuli arrive after this duration, there is no longer a visible source 
        /// (mustn't be too long or creature will continue turning) </summary>
        const float LOSSOFSIGNALAFTER = 2.0f;

        /// <summary> Adjustable acceptance angle in radians either side of the line of sight </summary>
        const float halfAngle = (float)Math.PI / 2.0f;          // +/-90 degrees (180 degree cone). Hotspots must be at +/-45 degrees to give the right overlap

        /// <summary> Current filter colour </summary>
        float r = 0, g = 0, b = 0;

        /// <summary> The object with the strongest signal - our current focus of attention </summary>
        IDetectable focusObject = null;

        /// <summary> The current signal strength from the focus of attention </summary>
        float focusIntensity = 0;

        /// <summary> The current direction of the focus object (0=hot0, 0.5=straight ahead, 1=hot1)</summary>
        float focusDirection = 0.5f;

        /// <summary> countdown until we assume (from a lack of stimuli) that no valid stimulus emitter is currently in view </summary>
        float lossOfSignal = LOSSOFSIGNALAFTER;

        /// <summary> True if signal is fading out after lossOfSignal </summary>
        bool fading = false;

        /// <summary> processed signals from the two sensors as used to compute direction (kept so that cell can be coloured to show signal ratio) </summary>
        float signal0 = 0;
        float signal1 = 0;



        public DualSpectral()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.2f;
            Buoyancy = 0.0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[] 
                {                                                                                                   // SOLE VARIANT
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 1, 0.5f, "Direction"),	        // direction of source (0=left, 1=right, 0.5=ahead)
				    new ChannelData(ChannelData.Socket.XX, ChannelData.Socket.PL, 2, 0f, "Intensity"),	  	        // intensity of source
				    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.XX, 0, 0.5f, "Filter colour"),	    // colour sensitivity
                },
			};

        }

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
            ReadFilter();
            SetOutputs();
        }


        public override void FastUpdate(float elapsedTime)
        {
            base.FastUpdate(elapsedTime);

            // For debugging, show our sensory range (can't show correct angle, cos sensors are 180-degree, and cone would have infinite base
            //owner.ShowSensoryField(0, 0, range, 0.1f);
            //owner.ShowSensoryField(1, 1, range, 0.1f);

            // If we're showing a signal but haven't heard a stimulus for a while, start fading the output because nobody's talking to us
            if (lossOfSignal > 0)
            {
                lossOfSignal -= elapsedTime;
                if (lossOfSignal <= 0)
                {
                    fading = true;
                }
            }

            // If signal has been lost recently, fade it out slowly
            if (fading == true)
            {
                focusIntensity *= 1.0f - elapsedTime * 0.5f;
                SetOutputs();

                if (focusIntensity <= 0.01f)
                {
                    focusDirection = 0.5f;                                          // default direction is straight ahead
                    focusIntensity = 0;                                             // intensity of zero
                    fading = false;                                                 // stop fade
                    SetOutputs();
                }
            }


        }

        /// <summary>
        /// Set the direction & intensity outputs, and colour-animate sides of cell to show the actual sensor signals
        /// </summary>
        private void SetOutputs()
        {
            Output(0, focusDirection);                                                          // direction
            Output(1, focusIntensity);                                                          // strength
            owner.SetAnimColour(0, new ColorValue(signal0, signal0, 0), new ColorValue(0, 0, 0));     // colour left & right sensor surround 
            owner.SetAnimColour(1, new ColorValue(signal1, signal1, 0), new ColorValue(0, 0, 0));     // to show signal strengths in each sensor
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
            float freq = Input(2);
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
            this.owner.SetAnimColour(2, new ColorValue(r, g, b), new ColorValue(0, 0, 0));          // recolour the filters so user can see them
            this.owner.SetAnimColour(3, new ColorValue(r, g, b), new ColorValue(0, 0, 0));
        }

        /// <summary> We've been sent a Stimulus that our basic Cell object doesn't understand.
        /// This overload responds to the "bioluminescence" stimulus
        /// Parameter 0 will be a ColorValue containing the bioluminescent cell's current anim colour
        /// <param name="stimulus">The stimulus information</param>
        /// <returns>Return true if the stimulus was handled</returns>
        public override bool ReceiveStimulus(Stimulus stim)
        {
            float direction = 0.5f;
            float intensity = 0f;

            if (stim.Type == "bioluminescence")
            {
                // Find out if the sender is within range of our hotspots
                SensorItem cone0 = owner.TestStimulusVisibility(0, range, stim);
                float dist = cone0.Distance();                                                      // dist will be roughly the same from both hotspots
                if (dist < range)                                                                   // if object is within range...
                {
                    // Find out how visible it is from each hotspot
                    SensorItem cone1 = owner.TestStimulusVisibility(1, range, stim);                // now we know we're in range, get the other hotspot's visibility
                    float angle0 = cone0.Angle();                                                   // Get angle from each hotspot
                    float angle1 = cone1.Angle();
                    if ((angle0 < halfAngle)||(angle1 < halfAngle))                                 // if within sight of at least one hotspot...
                    {
                        // Compare the light to our filter colour
                        ColorValue light = (ColorValue)stim.Param0;                                 // Stimulus param 0 will be the bioluminescent cell's current anim colour
                        float r1 = light.Red - r;                                                   // difference in RGB between filter and cell
                        float g1 = light.Green - g;
                        float b1 = light.Blue - b;
                        float match = 1f - (float)Math.Sqrt((r1 * r1 + g1 * g1 + b1 * b1) / 3f);   // least squares measure of similarity (1=identical)

                        // calc intensity and direction, if we match spectrally
                        if (match > 0.8f)                                                           // <=1/2 is a bad match, e.g. rgB doesn't match rGb but they're still a third similar!
                        {                                                                           // only give a direction response to a good match
                            // Scale signal according to distance downrange
                            intensity = cone0.Distance(range);

                            // compute direction
                            float a0 = 1f - angle0 / halfAngle;
                            float a1 = 1f - angle1 / halfAngle;
                            direction = (a1 - a0) / 2f + 0.5f;
                            if (focusDirection < 0f) focusDirection = 0f;
                            else if (focusDirection > 1f) focusDirection = 1f;
                        }

                        // If this stimulus comes from a different source to our present focus of attention
                        // ignore it unless it is stronger (in which case, shift attention to it)
                        if ((stim.From != focusObject)                                              // Is this object different from our present focus of attention
                            && (intensity < focusIntensity))                                        // and weaker?
                            return true;                                                            // ignore it

                        focusObject = stim.From;                                                    // Shift attention if necessary (if found stronger source)
                        focusIntensity = intensity;                                                 // record the new signal strength
                        focusDirection = direction;
                        lossOfSignal = LOSSOFSIGNALAFTER;                                           // and reset the loss-of-signal timers
                        fading = false;

                        // Calculate the signal entering each sensor
                        if (angle0 < halfAngle)                                                     // if the source is visible from sensor0
                            signal0 = intensity * (1f - angle0 / halfAngle);                        // scale signal by deviation from sensor's midline
                        if (angle1 < halfAngle)                                                     // otherwise leave as zero
                            signal1 = intensity * (1f - angle1 / halfAngle);                        // Repeat for sensor1

                        SetOutputs();                                                               // Write the two cell outputs

                        }
                }
                return true;
            }
            return false;
        }


    }

}
