using System;
using System.Collections.Generic;
using Microsoft.DirectX;

namespace Simbiosis
{

	// STANDARD BUILT-IN CELL TYPES
	// These are the specific cell types in the "starter set", compiled as CellType.dll
	// Cells of these types may be specified in the genome using just their class name,
	// whereas cell types in other dlls need a dllassemblyname.classname format


	#region ============================== new types =====================================


	/// <summary>
	/// Female reproductive organ
	/// 
	/// TEMPORARY: open/close the jaws using input 0
	/// 
	/// INPUT 0 = 
	/// 
	/// </summary>
	public class FemaleRepro : Physiology
	{

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public FemaleRepro()
		{
			// Define my properties
			Mass = 0.2f;
			Resistance = 0.2f;
			Bouyancy = 0f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
			inputTerminal = new NerveEnding[1];
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
				JointOutput[j] = Input(0);								// write one nerve signal to the muscle joint(s)
		}

	}












	#endregion

	#region =============================== old types not yet updated =======================================


	/// <summary>
	/// Standard muscle cell: a single nerve input causes flexion
	/// 
	/// INPUT 0 = muscle position (NOTE: Other muscle types might be supplied with a force rather than a position)
	/// 
	/// </summary>
	public class Muscle : Physiology
	{

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Muscle()
		{
			// Define my properties
			Mass = 0.2f;
			Resistance = 0.2f;
			Bouyancy = 0f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
			inputTerminal = new NerveEnding[1];
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
				JointOutput[j] = Input(0);								// write one nerve signal to the muscle joint(s)
		}

	}


	/// <summary>
	/// This is the core cell - it contains the reproductive organs and energy storage for the creature.
	/// All multicellular creatures must have a single core cell as the ROOT of their cell hierarchy.
	/// 
	/// TEMPORARILY, treat core as a pattern generator
	/// output0 = swim motion
	/// output1 = sinusoid
	/// output2 = fast sinusoid
	/// 
	/// </summary>
	public class Core : Physiology
	{
		PatternGenerator gen1 = new PatternGenerator();
		PatternGenerator gen2 = new PatternGenerator();
		PatternGenerator gen3 = new PatternGenerator();

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Core()
		{
			// Define my properties
			Mass = 0.8f;
			Resistance = 0.6f;
			Bouyancy = 0f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
			outputTerminal = new NerveEnding[3];

			gen1.Swim(0.4f, 1f, 2f, true);
			gen2.Sinusoid(6);
			gen3.Sinusoid(1);
		}

		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// </summary>
		public override void SlowUpdate()
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

			gen1.Update(elapsedTime);
			gen2.Update(elapsedTime);
			gen3.Update(elapsedTime);
			Output(0, gen1.state);
			Output(1, gen2.state);
			Output(2, gen3.state);

		}
	}


	/// <summary>
	/// Simple fin - does nothing - just a large water resistance
	/// 
	/// 
	/// </summary>
	public class Fin : Physiology
	{

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Fin()
		{
			// Define my properties
			Mass = 0.2f;
			Resistance = 0.8f;
			Bouyancy = 0f;

		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
		}

		/// <summary>
		/// Called every frame
		/// Since we have no sockets we'll override the base FastUpdate() to avoid wasting time trying to read/write nerves
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
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
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public SpinySucker()
		{
			// Define my properties
			Mass = 0.2f;
			Resistance = 0.2f;
			Bouyancy = -0.05f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
			inputTerminal = new NerveEnding[1];
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

	}

	/// <summary>
	/// Bend1, Bend2 etc. - various bends and branches. No functionality other than propagating signals
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
			Bouyancy = 0.0f;
		}


	}

	/// <summary>
	/// Plate1, Plate2 etc. - various heavy armoured plates. No functionality other than propagating signals
	/// </summary>
	public class Plate : Physiology
	{

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Plate()
		{
			// Define my properties
			Mass = 0.3f;
			Resistance = 0.3f;
			Bouyancy = -0.3f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
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
			Bouyancy = -0.1f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
		}

		/// <summary>
		/// Override FastUpdate because we have no nerves
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
		}

	}



	//---------------------------------------------- new types - no graphics yet -------------------------------------------------

	/// <summary>
	/// Sonar - Active sensor. Measures distance to nearest obstruction (terrain or creature).
	/// Equally sensitive to all obstructions less than given angle from the sensor axis.
	/// </summary>
	class Sonar : Physiology
	{
		/// <summary> Adjustable range - default is 20 </summary>
		private float range = 20;

		/// <summary> Adjustable acceptance angle in degrees either side of the line of sight </summary>
		private float halfAngle = (float)Math.PI / 2.0f;


		public Sonar()
		{
			// Define my properties
			Mass = 0.1f;
			Resistance = 0.1f;
			Bouyancy = 0f;
		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
			outputTerminal = new NerveEnding[1];
		}

		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// Read/write/modify your sensory/motor nerves and/or chemicals, to implement your behaviour
		/// </summary>
		public override void SlowUpdate()
		{
			// Calculate the sensor signal...
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
	/// Test sensory cell, using the ImageSensor (eye) mesh
	/// </summary>
	public class ImageSensor : Physiology
	{

		private int blink = 0;

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public ImageSensor()
		{
			// Define my properties
			Mass = 0.2f;
			Resistance = 0.2f;
			Bouyancy = 0f;

		}

		/// <summary>
		/// Called once .owner etc. have been set up by the cell. Do your main initialisation here.
		/// </summary>
		public override void Init()
		{
			outputTerminal = new NerveEnding[1];
		}

		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// </summary>
		public override void SlowUpdate()
		{
			// Get a list of all objects in range of my hotspot
			SensorItem[] item = owner.GetObjectsInRange(0, 50, true, false, true);
			// TEMP: just count those that are inside a 45 degree cone
			blink = 0;
			for (int i = 0; i < item.Length; i++)
			{
				const float cone = (float)Math.PI / 8.0f;
				float angle = item[i].Angle();
				if ((angle >= -cone) && (angle <= cone))
					blink = 1;
			}

		}

		/// <summary>
		/// Called every frame
		/// Nerves and JointOutputs should normally be updated here, although slow nerve changes can happen on a slowupdate
		/// </summary>
		public override void FastUpdate(float elapsedTime)
		{
			// Update nerve signals
			base.FastUpdate(elapsedTime);

			//			JointOutput[0] = Nerve[inputTap[0]];						// eyelids
			//			JointOutput[1] = Nerve[inputTap[1]];						// eyelids


			if ((blink > 0) && (JointOutput[0] < 1.0f))
			{
				JointOutput[0] += 0.1f;						// blink to show we've seen something
				JointOutput[1] += 0.1f;						// eyelids
			}
			else if ((blink == 0) && (JointOutput[0] > 0))
			{
				JointOutput[0] -= 0.1f;						// blink to show we've seen something
				JointOutput[1] -= 0.1f;						// eyelids
			}

		}
	}

	#endregion







}
