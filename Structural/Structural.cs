using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;

namespace Simbiosis
{
	/// <summary>
	/// Simple fin - does nothing - just a large water resistance
	/// 
	/// 
	/// </summary>
	public class Fin : Physiology
	{
        private Vector3 lastLocation = Vector3.Empty;
        private float movingAverage = 0;

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Fin()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 2.0f;
            Buoyancy = 0f;

		}

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "Pectoral", "Tail" };
        }

        public override void FastUpdate(float elapsedTime)
        {
            switch (owner.Variant)
            {
                case 0: // Pectoral fin - Curves in one direction only. Propulsion comes from reaction force. Hot0 points "forward"
                    {
                        const float GAIN = 0.25f;                                               // bigger numbers cause bigger movements for a given speed
                        const float SMOOTH = 0.3f;                                              // time in seconds over which smoothing works (affects relaxation and response rates)

                        Vector3 mvt = lastLocation - owner.Location;                            // get this cell root's movement vector
                        lastLocation = owner.Location;
                        Matrix frame = owner.GetHotspotNormalMatrix(0);                         // rotate it into hot0's frame (which faces the direction in which the fin bends)
                        frame.Invert();
                        frame.M41 = frame.M42 = frame.M43 = 0;
                        mvt.TransformCoordinate(frame);
                        float speed = mvt.Z * GAIN / elapsedTime;                               // convert to a speed
                        movingAverage = (movingAverage * SMOOTH + speed * elapsedTime) / (SMOOTH + elapsedTime); // smooth it with a MA
                        if (movingAverage < 0) movingAverage = 0;
                        else if (movingAverage > 1f) movingAverage = 1f;
                        JointOutput[0] = JointOutput[1] = movingAverage;
                        // Set a lower resistance when the fin is flexed
                        //Resistance = (1f-movingAverage) * 2f;
                        if (mvt.Y < 0)
                            Resistance = 2.0f;
                        else
                            Resistance = 0.2f;
                    }
                    break;

                case 1: // Tail fin - curves in both directions. Propulsion is explicit, driven by amplitude of flap
                    {
                        const float GAIN = 0.25f;                                               // bigger numbers cause bigger flapping movements for a given speed
                        const float FORCE = 200f;                                               // GAIN * FORCE controls amount of force per flap speed
                        const float SMOOTH = 0.2f;                                              // time in seconds over which smoothing works (affects relaxation and response rates)

                        Matrix frame = owner.GetHotspotNormalMatrix(0);                              
                        Vector3 locn = new Vector3(frame.M41, frame.M42, frame.M43);            // get hotspot's position
                        Vector3 mvt = lastLocation - locn;                                      // cvt to a movement vector
                        lastLocation = locn;

                        frame.M41 = frame.M42 = frame.M43 = 0;                                  // rotate it into hotpot's frame             
                        frame.Invert();
                        mvt.TransformCoordinate(frame);    
                                     
                        float dist = mvt.Z * GAIN / elapsedTime;                                // how fast we've moved sideways
                        movingAverage = (movingAverage * SMOOTH + dist * elapsedTime) / (SMOOTH + elapsedTime); // smooth it with a MA
                        JointOutput[0] = JointOutput[1] = movingAverage / 2f + 0.5f;            // control flex

                        float force = Math.Abs(movingAverage) * FORCE;                          // Jet propel in current mean direction of tail, in proportion
                        owner.JetPropulsion(1, -force);                                         // to how fast it is flapping

                        //Debug.WriteLine(" z=" + mvt.Z.ToString("0.00") + " dist=" + dist.ToString("0.00") + " ma=" + movingAverage.ToString("0.00") + " force=" + force.ToString("0.00"));
                    }
                    break;
            }

        }
	}


	/// <summary>
	/// Bend1, Bend2 etc. - various bends. No functionality other than propagating signals. There are 3 channels from the plug to the socket.
	/// </summary>
	public class Bend : Physiology
	{

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Bend()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 0.1f;
            Buoyancy = 0.0f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]                                                               // ALL VARIANTS ARE THE SAME
                {
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "chan1"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "chan2"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "chan3"),
                }
            };

		}


	}

    /// <summary>
    /// Branch1, Branch2 etc. - various Y-pieces. No functionality other than propagating signals. 
    /// There are 2 channels from the plug to each socket and one channel connecting the two sockets.
    /// </summary>
    public class Branch : Physiology
    {

        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Branch()
        {
            // Define my properties
            Mass = 0.1f;
            Resistance = 0.1f;
            Buoyancy = 0.0f;

            // Define my channels
            channelData = new ChannelData[][]
            {
                new ChannelData[]                                                                   // ALL VARIANTS ARE CURRENTLY THE SAME
                {
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "plug-socket0"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S1, 0, 0f, "plug-socket1"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "plug-socket0"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S1, 0, 0f, "plug-socket1"),
                    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 0, 0f, "socket-socket"),
                }
            };

        }

 
    }



    /// <summary>
    /// Plexus 0 - n. Various branching body segments. No functionality other than propagating signals. 
    /// Each variant has different wiring and # sockets (e.g. pentaradial, bilateral, etc.)
    /// </summary>
    public class Plexus : Physiology
    {
        /// <summary>
        /// Properties for each variant
        /// </summary>
        private static float[] Masses =         { 0.5f, 0.5f, 0.5f, 0.5f };
        private static float[] Resistances =    { 0.5f, 0.5f, 0.5f, 0.5f };
        private static float[] Buoyancies =     { 0.0f, 0.0f, 0.0f, 0.0f };

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public override string[] IndexVariants(string variantName)
        {
            return new string[] { "small" };
        }

        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Plexus()
        {
            // Define my channels
            channelData = new ChannelData[][]
            {
                // Variant 0 - standard spinal plexus
                new ChannelData[] 
                {
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "spine"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "spine"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "spine"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S0, 0, 0f, "spine"),

                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S2, 0, 0f, "plug-arm"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S2, 0, 0f, "plug-arm"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S1, 0, 0f, "plug-arm"),
                    new ChannelData(ChannelData.Socket.PL, ChannelData.Socket.S1, 0, 0f, "plug-arm"),

                    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S2, 0, 0f, "arm-skt"),
                    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S2, 0, 0f, "arm-skt"),
                    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 0, 0f, "arm-skt"),
                    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 0, 0f, "arm-skt"),

                    new ChannelData(ChannelData.Socket.S1, ChannelData.Socket.S2, 0, 0f, "arm-arm"),
               },

                // Other variants here

            };

            // Can't set mass, etc. here because we don't know owner.variant until Init() is called
        }

        public override void Init()
        {
            base.Init();

            // Define my properties now we know our variant
            Mass = Masses[owner.Variant];
            Resistance = Resistances[owner.Variant];
            Buoyancy = Buoyancies[owner.Variant];



        }


    }

    /// <summary>
    /// Plate1, Plate2 etc. - various heavy armoured plates. No functionality.
    /// </summary>
    public class Plate : Physiology
    {

        /// <summary>
        /// ONLY set the physical properties in the constructor. Use Init() for initialisation.
        /// </summary>
        public Plate()
        {
            // Define my properties
            Mass = 0.5f;
            Resistance = 1.5f;
            Buoyancy = -0.8f;
        }




    }




	/// <summary>
	/// Foot1, Foot2 etc. - Slightly heavy feet. No functionality at all
	/// </summary>
	public class Foot : Physiology
	{

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Foot()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 0.1f;
            Buoyancy = 0.5f;
		}



	}





}
