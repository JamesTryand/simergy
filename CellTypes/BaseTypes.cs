using System;
using System.Diagnostics;
using System.Collections;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

//
// These are the "system" cell types, required for various tasks but not visible to the user in the editor
//

namespace Simbiosis
{



	/// <summary>
	/// This is the core cell.
	/// All multicellular creatures built by the user must have a single core cell as the ROOT of their cell hierarchy.
    /// 
	/// The core cell is the chemical storage organ for global chemicals (really these are stored in the Organism object, but from the user's 
	/// perspective they're stored here). The Core cell must have five functional block organelles (func0 to func4).
    /// These are coloured to display the current levels of the global chemicals when in SCANNER mode. Any channels in the organism using global chems
    /// as input will also be coloured in scanner mode, but some globals, such as energy, may not have any channels associated with them and so 
    /// wouldn't be visible without this.
	/// 
    /// The core's main part can be scaled to something other than 1,1,1 in SoftImage. Since the rest of the core and all subsequent cells 
    /// will be scaled alongside this, it provides a single place to set the scale of all cells to make creatures look sensible in the lab and environment.
    /// 
	/// 
	/// </summary>
	public class Core : Physiology
	{
        /// <summary> to animate 'breathing' </summary>
        private float breathAnim = 1f;


		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Core()
		{
			// Define my properties
			Mass = 0.3f;
			Resistance = 0.4f;
			Buoyancy = 0f;

            // Define my channels
            channelData = new ChannelData[][] 
            {
                new ChannelData[]                           
                {
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 1, 0f, "Bypass"),
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 2, 0f, "Bypass"),
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 3, 0f, "Bypass"),
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 4, 0f, "Bypass"),
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 5, 0f, "Bypass"),
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 6, 0f, "Bypass"),
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 7, 0f, "Bypass"),
				    new ChannelData(ChannelData.Socket.S0, ChannelData.Socket.S1, 8, 0f, "Bypass"),
                }
			};

		}

        /// <summary>
        /// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
        /// </summary>
        public override void Init()
        {
            base.Init();
        }


		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// </summary>
		public override void SlowUpdate()
		{
            // TODO: Here's where I should adjust my organism's .scale to make the creature grow as it ages (if it has enough food)
            // Use an iControllable method


		}

		/// <summary>
		/// Called every frame
		/// Nerves and JointOutputs should normally be updated here
		/// </summary>
        public override void FastUpdate(float elapsedTime)
        {

            base.FastUpdate(elapsedTime);

            // Make a heartbeat
            // TODO: Modulate this by the energy level and/or arousal state (e.g. stop beating if dead, or beat at adrenaline level)
            JointOutput[0] = JointOutput[0] + breathAnim * elapsedTime;
            if (JointOutput[0] > 1.0f)
            {
                breathAnim = -2f;
                JointOutput[0] = 1.0f;
            }
            else if (JointOutput[0] < 0)
            {
                breathAnim = 4f;
                JointOutput[0] = 0;
            }

        }
	}



}
