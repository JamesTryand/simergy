using System;

namespace Simbiosis
{
	// This DLL contains some sample cell types that aren't bound to the main application and must be late-bound
	// when reading an organism's genome
	// NOTES: 
	// - DLL must reference CellTypes.dll, which contains the base class (do you need the source code to do this? Presumably not!)
	// - DLL needs to be copied to the same folder as the application
	//	 To do this in Visual Studio, go to Project:Properties:Build Events:Post-build events command line and type:
	//   copy "$(TargetPath)" "$(SolutionDir)bin\Debug"
	//   But don't forget to copy the DLL by hand in a release build!

	/// <summary>
	/// Plankton that swims randomly around
	/// </summary>
	public class Plankton1 : Physiology
	{

		/// <summary>
		/// ONLY set the physical properties in the constructor. Use Init() for initialisation.
		/// </summary>
		public Plankton1()
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
		}


		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// Read/write/modify your sensory/motor nerves and/or chemicals, to implement your behaviour
		/// </summary>
		public override void SlowUpdate()
		{
		}


	}


}
