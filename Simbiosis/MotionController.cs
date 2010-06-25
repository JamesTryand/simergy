using System;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	/// <summary>
	/// Holds animation data for an animateable frame and performs the interpolation to animate the frame
	/// to an angle/scale/position in the range 0-1
	/// </summary>
	public class MotionController
	{
		// Min and max values for rotation, translation and scale
		public Quaternion RotationMin = Quaternion.Identity;
		public Quaternion RotationMax = Quaternion.Identity;
		public Vector3 TranslationMin = Vector3.Empty;
		public Vector3 TranslationMax = Vector3.Empty;
		public Vector3 ScaleMin = new Vector3(1.0f,1.0f,1.0f);
		public Vector3 ScaleMax = new Vector3(1.0f,1.0f,1.0f);


		public MotionController()
		{
		}

		/// <summary>
		/// Produce a clone of this motion controller
		/// </summary>
		/// <returns></returns>
		public MotionController Clone()
		{
			return (MotionController)this.MemberwiseClone();
		}



		/// <summary>
		/// Use a value in the range 0 to 1.0 to interpolate between min and max rotation, translation and/or scale,
		/// then apply this transformation to the given frame's transformation matrix to animate the frame.
		/// Record the amount of the change for calculating reaction forces
        /// NOTE: Rotation range must be less than 180 degrees, or the slerp will interpolate through the shortest distance,
        /// which is the opposite way to the one intended.
		/// </summary>
		/// <param name="frame">The frame to animate</param>
		/// <param name="amount">A value from 0 (min) to 1.0 (max)</param>
		public void Transform(JointFrame frame, float amount)
		{
			Quaternion Rotation = Quaternion.Identity;
			Vector3 Translation = Vector3.Empty;
			Vector3 Scale = Vector3.Empty;

			// Cap the value into range
			if (amount>1.0f)
				amount = 1.0f;
			else if (amount < 0)
				amount = 0;

			// Record the translation and rotation values before the change, for calculating relative mvt later
			Vector3 oldTrans = Translation;
			Quaternion oldRot = Rotation;

			// Interpolate Translation & store
            Translation = (TranslationMax - TranslationMin) * amount + TranslationMin;

			// Interpolate scale & store
			//Scale = (ScaleMax - ScaleMin) * amount + new Vector3(1,1,1);
			Scale = (ScaleMax - ScaleMin) * amount + ScaleMin;

			// Interpolate rotation & store
			Rotation = Quaternion.Slerp(RotationMin,RotationMax,amount);
			Rotation.Invert();

			// Do the transform
            frame.TransformationMatrix = Matrix.Scaling(Scale) 
                * Matrix.RotationQuaternion(Rotation) 
                * Matrix.Translation(Translation);

		}
 

	}
}
