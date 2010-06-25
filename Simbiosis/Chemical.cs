using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Diagnostics;

namespace Simbiosis
{
	/// <summary>
	/// One set of (global) chemical concentrations and other chemistry parameters.
	/// Each Organism owns one instance of Chemistry.
	/// Static members define half-lives, etc. Instances are the concentrations
	/// </summary>
	public class Chemistry
	{
		public const int NUMSIGNALS = 8;											// Number of chemicals that can be sent through channels
		public const int NUMGLOBALS = 4;											// Number of hormones/energetic chemicals
		public const int NUMCHEMICALS = NUMSIGNALS + NUMGLOBALS;					// Number of chemicals of all types

		// Colours of the chemicals (how to colour each channel in design mode)
		public static Color[] Colour = {
			Color.White,															// 0 represents NO chemical (i.e. the channel is unused and the signal is read from the default/user-defined constant)
			
			Color.Red,
			Color.Green,
			Color.Blue,
			Color.Magenta,
			Color.DarkCyan,
			Color.Yellow,
            Color.Orange,
            Color.DarkSalmon,

            Color.Pink,
			Color.PaleGreen,
			Color.LightBlue,
			Color.Lavender,

		};

		public static string[] Name = {
			"<none/const>",

			"Signal1",
			"Signal2",
			"Signal3",
			"Signal4",
			"Signal5",
			"Signal6",
			"Signal7",
			"Signal8",

			"Global1",
			"Global2",
			"Global3",
			"Global4",
		};



		/// <summary> Global chemical concentrations </summary>
		private float[] concentration = new float[NUMGLOBALS];


		/// <summary>
		/// Read the concentration of a global chemical
		/// </summary>
		/// <param name="chem">Chemical number (INDEX IS >= NUMSIGNALS - Global0 is not chemical zero)</param>
		/// <returns></returns>
		public float Read(int chem)
		{
            Debug.Assert(chem >= NUMSIGNALS, "ERROR in Chemistry.Read(). Index for a global chemical must be >= NUMSIGNALS");
			return concentration[chem-NUMSIGNALS];
		}

		/// <summary>
		/// Add an amount of a global chemical to the present stock
		/// </summary>
        /// <param name="chem">Chemical number (INDEX IS >= NUMSIGNALS - Global0 is not chemical zero)</param>
		/// <param name="value"></param>
		public void Add(int chem, float value)
		{
            Debug.Assert(chem >= NUMSIGNALS, "ERROR in Chemistry.Add(). Index for a global chemical must be >= NUMSIGNALS");
            chem -= NUMSIGNALS;
            concentration[chem] += value;
			if (concentration[chem] > 1.0f)
				concentration[chem] = 1.0f;
		}
	}
}
