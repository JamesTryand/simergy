using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.Samples.DirectX.UtilityToolkit;


namespace Simbiosis
{
	/// <summary>
	/// Static class for managing effects, shaders and rendering.
	/// Call Fx.Load() to initialise. Set the technique, materials, matrices, etc. with the SetXXX() calls,
	/// then call one of the DrawXXX() methods to draw primitives, meshes, etc. using the current effect
	/// and resources.
	/// 
	/// Note: the World matrix is set using the SetWorldMatrix() method, which picks up the latest
	/// camera and lens matrices from the camera. If these need changing on the fly, set the Camera. matrices
	/// before calling SetWorldMatrix()
	/// </summary>
	static class Fx
	{
		/// <summary>
		/// The Effect object
		/// </summary>
		private static Effect effect = null;

		/// <summary>
		/// Handles for all the global variables
		/// </summary>
		private static EffectHandle matDiffuse = null;				// diffuse material colour
		private static EffectHandle matSpecular = null;
		private static EffectHandle matEmissive = null;
		private static EffectHandle matAmbient = null;
		private static EffectHandle baseTexture = null;				// main texture
		private static EffectHandle bumpTexture = null;				// normal map texture
		private static EffectHandle envTexture = null;				// cube map texture for reflections
		private static EffectHandle world = null;					// world matrix
		private static EffectHandle worldViewProjection = null;		// world*View*Proj matrix
		private static EffectHandle eyePosition = null;				// world position vector of camera
		private static EffectHandle isTextured = null;				// true if object has a texture, false for material-only
		private static EffectHandle isBump = null;					// true if object has a normal map texture
		private static EffectHandle isShadowed = null;				// true if shadowing is switched on
		private static EffectHandle isSpotlit = null;				// true if spotlighting is required
		private static EffectHandle spotVP = null;					// Spotlight's view*proj matrix
		private static EffectHandle spotTexture = null;				// Spotlight's light map texture
		private static EffectHandle spotPosition = null;			// Spotlight's world position vector
		private static EffectHandle causticTexture = null;			// current texture for caustics
		private static EffectHandle shadowTexture = null;			// shadow map texture
		private static EffectHandle shadowMatrix = null;			// shadow projection matrix
		private static EffectHandle mainTechnique = null;			// technique for objects & terrain
		private static EffectHandle sceneryTechnique = null;		// technique for scenery sprites
		private static EffectHandle waterTechnique = null;			// technique for water
		private static EffectHandle skyboxTechnique = null;			// technique for skybox
		private static EffectHandle shadowTechnique = null;			// technique for shadow
		private static EffectHandle markerTechnique = null;			// technique for markers

		private static Matrix sunProj;								// Projection matrix for shadows

		/// <summary>
		/// Load the effect file & initialise everything
		/// </summary>
		public static void Load(Device device, ShaderFlags shaderFlags)
		{
			string err = "";										// shader compiler errors
			// Find and load the file
			try
			{
				string path = Utility.FindMediaFile("Simbiosis.fx");
				effect = ResourceCache.GetGlobalInstance().CreateEffectFromFile(device,
																				path, null, null, shaderFlags, null, out err);
				if (effect == null) throw new Exception("effect = null");
			}
			catch (Exception e)
			{
				Debug.WriteLine("=========================================================================================");
				Debug.WriteLine("Unable to compile Effect file." + e.ToString());
				Debug.WriteLine(err);
				Debug.WriteLine("=========================================================================================");
				throw e;
			}

			// Set up handles for the globals
			matDiffuse = effect.GetParameter(null, "matDiffuse");
			matSpecular = effect.GetParameter(null, "matSpecular");
			matEmissive = effect.GetParameter(null, "matEmissive");
			matAmbient = effect.GetParameter(null, "matAmbient");
			baseTexture = effect.GetParameter(null, "baseTexture");
			bumpTexture = effect.GetParameter(null, "bumpTexture");
			world = effect.GetParameter(null, "world");
			worldViewProjection = effect.GetParameter(null, "worldViewProjection");
			eyePosition = effect.GetParameter(null, "eyePosition");
			isTextured = effect.GetParameter(null, "isTextured");
			isBump = effect.GetParameter(null, "isBump");
			isShadowed = effect.GetParameter(null, "isShadowed");
			isSpotlit = effect.GetParameter(null, "isSpotlit");
			spotVP = effect.GetParameter(null, "spotVP");
			spotTexture = effect.GetParameter(null, "spotTexture");
			spotPosition = effect.GetParameter(null, "spotPosition");
			causticTexture = effect.GetParameter(null, "causticTexture");
			envTexture = effect.GetParameter(null, "envTexture");
			shadowTexture = effect.GetParameter(null, "shadowTexture");
			shadowMatrix = effect.GetParameter(null, "shadowMatrix");

			// and the techniques
			mainTechnique = effect.GetTechnique("Main");
			sceneryTechnique = effect.GetTechnique("Scenery");
			waterTechnique = effect.GetTechnique("Water");
			skyboxTechnique = effect.GetTechnique("Skybox");
			shadowTechnique = effect.GetTechnique("Shadow");
			markerTechnique = effect.GetTechnique("Marker");


			// Set once-only shader constants
			effect.SetValue("sunPosition",new Vector4(512,1000,512,0));				// sun's position (.W is ignored - float3 in shader)
            effect.SetValue("ambient", new ColorValue(255, 255, 255));					// amount/colour of ambient skylight 
            effect.SetValue("sunlight", new ColorValue(255, 255, 255));				// colour/brightness of sunlight

			// Set up the projection matrix for shadows, based on sun position 
			sunProj = Matrix.OrthoLH(100, 100, 0, 1000);							// adjust width, height if necc
			effect.SetValue("sunProj", sunProj);
			SetCameraData();														// also set up the (variable) view matrix

			// Set up the projection matrix for caustics
			Matrix caustic =
				Matrix.Invert(														// "view" matrix
				Matrix.Translation(new Vector3(0, Water.WATERLEVEL, 0))				// caustics start at water level
				* Matrix.RotationYawPitchRoll(0, (float)Math.PI/2, 0)				// looking down
				)																	// Proj matrix (orthogonal)
				* Matrix.OrthoLH(80, 80, 0, 1000);									// adjust width, height
			effect.SetValue("causticMatrix", caustic);



		}

		/// <summary>
		/// Set the current technique, given a string
		/// </summary>
		/// <param name="technique"></param>
		public static void SetTechnique(string technique)
		{
			SetTechnique(effect.GetTechnique(technique));
		}

		/// <summary>
		/// Set the current technique, given a handle
		/// </summary>
		/// <param name="technique"></param>
		public static void SetTechnique(EffectHandle tech)
		{
			try
			{
				effect.ValidateTechnique(tech);
			}
			catch
			{
				tech = effect.FindNextValidTechnique(tech);
				//Debug.WriteLine("Using fallback technique: "+effect.GetTechniqueDescription(tech).Name);
			}
			effect.Technique = tech;
		}

		/// <summary>
		/// Fast (handle-based) technique changes
		/// </summary>
		public static void SetMainTechnique()
		{
			SetTechnique(mainTechnique);
		}
		public static void SetSceneryTechnique()
		{
			SetTechnique(sceneryTechnique);
		}
		public static void SetWaterTechnique()
		{
			SetTechnique(waterTechnique);
		}
		public static void SetSkyboxTechnique()
		{
			SetTechnique(skyboxTechnique);
		}
		public static void SetShadowTechnique()
		{
			SetTechnique(shadowTechnique);
		}
		public static void SetMarkerTechnique()
		{
			SetTechnique(markerTechnique);
		}


		/// <summary>
		/// Set up a new material
		/// </summary>
		/// <param name="m"></param>
		public static void SetMaterial(Material m)
		{
			effect.SetValue(matDiffuse, m.DiffuseColor);
			effect.SetValue(matAmbient, m.AmbientColor);
			effect.SetValue(matSpecular, m.SpecularColor);
			effect.SetValue(matEmissive, m.EmissiveColor);
		}

		/// <summary>
		/// Set up a new texture (or null to disable texturing)
		/// </summary>
		/// <param name="t"></param>
		public static void SetTexture(Texture t)
		{
			effect.SetValue(baseTexture, t);
			effect.SetValue(isTextured, (t != null) ? true : false);
			effect.SetValue(isBump, false);										// if only a texture is supplied, don't do bump mapping
		}

		/// <summary>
		/// Set up a new texture AND a normal map texture (either or both can be null)
		/// </summary>
		/// <param name="t"></param>
		public static void SetTexture(BaseTexture tex, Texture bump)
		{
			effect.SetValue(baseTexture, tex);
			effect.SetValue(isTextured, (tex != null) ? true : false);

			effect.SetValue(bumpTexture, bump);
			effect.SetValue(isBump, (bump != null) ? true : false);
		}

		/// <summary>
		/// Set up a new cube map texture for reflections
		/// </summary>
		/// <param name="t"></param>
		public static void SetEnvTexture(CubeTexture t)
		{
			effect.SetValue(envTexture, t);
			// TODO: Add boolean so can switch off if not supported?
		}

		/// <summary>
		/// Set up the current shadow map (rendered onto)
		/// </summary>
		/// <param name="t"></param>
		public static void SetShadowTexture(Texture t)
		{
			effect.SetValue(shadowTexture, t);
		}


		/// <summary>
		/// Set up a spotlight texture (but don't switch it on yet)
		/// </summary>
		/// <param name="t"></param>
		public static void SetSpotlightTexture(Texture tex)
		{
			effect.SetValue(spotTexture, tex);
		}

		/// <summary>
		/// Get or set the spotlight state (on/off)
		/// </summary>
		public static bool Spotlight
		{
			get { return effect.GetValueBoolean(isSpotlit); }
			set { effect.SetValue(isSpotlit,value); }
		}

		/// <summary>
		/// Set the current spotlight location/orientation/beam angle
		/// The world matrix sets the spotlight
		/// </summary>
		/// <param name="worldViewProj">World * proj matrix for spotlight. World is spot location, proj defines projection frustrum</param>
		public static void SetSpotlightPosition(Matrix worldProj)
		{
			effect.SetValue(spotVP, worldProj);
		}

		/// <summary>
		/// Set a new World matrix (current view and projection are fetched from Camera)
		/// </summary>
		public static void SetWorldMatrix(Matrix m)
		{
			// World matrix
			effect.SetValue(world, m);

			// World * view * projection matrix
			effect.SetValue(worldViewProjection, m * Camera.ViewMatrix * Camera.ProjMatrix);
		}

		/// <summary>
		/// Update shader variables that need to change whenever the camera moves
		/// </summary>
		public static void SetCameraData()
		{
			// Camera position
			effect.SetValue(eyePosition, new Vector4(Camera.Position.X, Camera.Position.Y, Camera.Position.Z, 0));

			// Shadow-casting matrix ("sun" follows camera so that the shadowmap can be small but high resolution)
			Matrix sunView =
				Matrix.Invert(														// "view" matrix
				 Matrix.RotationYawPitchRoll(0, (float)Math.PI / 2.0f, 0)				// looking down
				* Matrix.Translation(new Vector3(Camera.Position.X, 1000.0f, Camera.Position.Z))
				);																	// Proj matrix (orthogonal)
			effect.SetValue(shadowMatrix, sunView);
		}

		/// <summary>
		/// Set the animation frame of the caustic texture
		/// </summary>
		/// <param name="tex"></param>
		public static void SetCausticTexture(Texture tex)
		{
			effect.SetValue(causticTexture, tex);
		}

		/// <summary>
		/// Get/set flag to show whether shadowing is switched on
		/// </summary>
		public static bool IsShadowed
		{
			get { return effect.GetValueBoolean(isShadowed); }

			set 
			{ 
				effect.SetValue(isShadowed, value);
				if (value==true)													// we also need to change the shader to Main2 if shadows
					mainTechnique = effect.GetTechnique("Main");					// have been switched off, so that the shadowmap isn't
				else																// rendered as a second pass
					mainTechnique = effect.GetTechnique("Main2");


			}
		}




		#region ---------------- Replacements for DrawPrimitives() calls to render different types of object through an effect -----------
		// Use these calls instead of their Device. equivalents. Each one renders all passes of the current technique
		// using a different type of primitive

		/// <summary>
		/// Use the current technique to render with a Device.DrawPrimitives() call
		/// </summary>
		/// <param name="technique"></param>
		public static void DrawPrimitives(PrimitiveType primitiveType, int startVertex, int primitiveCount)
		{
			int passes = effect.Begin(0);
			for (int pass = 0; pass < passes; pass++)
			{
				effect.BeginPass(pass);
				effect.CommitChanges();
				Engine.Device.DrawPrimitives(primitiveType, startVertex, primitiveCount);
				effect.EndPass();
			}
			effect.End();
		}

		/// <summary>
		/// Use the current technique to render with a Device.DrawUserPrimitives() call
		/// </summary>
		/// <param name="technique"></param>
		public static void DrawUserPrimitives(PrimitiveType primitiveType, int primitiveCount, Object vertexData)
		{
			int passes = effect.Begin(0);
			for (int pass = 0; pass < passes; pass++)
			{
				effect.BeginPass(pass);
				effect.CommitChanges();
				Engine.Device.DrawUserPrimitives(primitiveType, primitiveCount, vertexData);
				effect.EndPass();
			}
			effect.End();
		}

		/// <summary>
		/// Use the current technique to render with a Mesh.DrawSubset() call
		/// </summary>
		/// <param name="technique"></param>
		public static void DrawMeshSubset(BaseMesh mesh, int index)
		{
			int passes = effect.Begin(0);
			for (int pass = 0; pass < passes; pass++)
			{
				effect.BeginPass(pass);
				effect.CommitChanges();
				mesh.DrawSubset(index);
				effect.EndPass();
			}
			effect.End();
		}

		#endregion

	}
}
