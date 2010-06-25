using System;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace Simbiosis
{
	static class SkyBox
	{
		const float SKYLEVEL = Water.WATERLEVEL + 10.0f;	// minimum level for sky (it'll always be this much above camera)

		private static CustomVertex.PositionNormalTextured[] vert = new CustomVertex.PositionNormalTextured[4];
		private static Texture texture = null;
		private const float width = Map.MapWidth/2;
		private const float height = Map.MapHeight/2;


		static SkyBox()
		{
			vert[0].X = -width;
			vert[1].X = width;
			vert[2].X = -width;
			vert[3].X = width;

			vert[0].Y = 0;
			vert[1].Y = 0;
			vert[2].Y = 0;
			vert[3].Y = 0;

			vert[0].Z = -height;
			vert[1].Z = -height;
			vert[2].Z = height;
			vert[3].Z = height;

			vert[0].Tu = 0.0f;
			vert[0].Tv = 0.0f;
			vert[1].Tu = 1.0f;
			vert[1].Tv = 0.0f;
			vert[2].Tu = 0.0f;
			vert[2].Tv = 1.0f;
			vert[3].Tu = 1.0f;
			vert[3].Tv = 1.0f;

			// Normals point downwards
			for (int v=0; v<4; v++)
			{
				vert[v].Nx = 0;
				vert[v].Ny = -1.0f;
				vert[v].Nz = 0;
			}

					
		}

		/// <summary>
		/// A new D3D device has been created.
		/// if Engine.IsFirstDevice==true, create those once-only things that require a Device to be available
		/// Otherwise, rebuild all those resources that get lost when a device is destroyed during a windowing change etc.
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Skybox.OnDeviceCreated()");
			Debug.WriteLine("(does nothing)");
		}

		/// <summary>
		/// Called immediately after the D3D device has been destroyed, 
		/// which generally happens as a result of application termination or 
		/// windowed/full screen toggles. Resources created in OnSubsequentDevices() 
		/// should be released here, which generally includes all Pool.Managed resources. 
		/// </summary>
		public static void OnDeviceLost()
		{
			Debug.WriteLine("Skybox.OnDeviceLost()");
			if (texture != null)
				texture.Dispose();
		}

		/// <summary>
		/// Device has been reset - rebuild unmanaged resources
		/// </summary>
		public static void OnReset()
		{
			Debug.WriteLine("SkyBox.OnReset()");
			texture = TextureLoader.FromFile(Engine.Device, FileResource.Fsp("textures", "Sky.png"));
			texture.GenerateMipSubLevels();
		}





		/// <summary>
		/// Render the skybox - call this before any other scene objects have been rendered
		/// </summary>
		/// <param name="elapsedtime">time in seconds that has elapsed since last frame</param>
		/// <param name="simtime">time that has elapsed since simulation began</param>
		/// <param name="activecamera">currently active camera (for orienting sprites & skybox)</param>
		public static void Render()
		{

			//return; // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! TEMP !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

			// --------------------- Set Renderstate ------------------
			Engine.Device.VertexFormat = CustomVertex.PositionNormalTextured.Format;		// Textured objects
			// Stop z-buffering because skybox is infinitely distant
			Engine.Device.RenderState.ZBufferEnable = false;
			Fx.SetSkyboxTechnique();														// technique
			// --------------------------------------------------------

			// World transform to centre the skybox on camera
			Vector3 omphalos = Camera.Position;
			omphalos.Y += SKYLEVEL;
			Fx.SetWorldMatrix(Matrix.Translation(omphalos));

			// Set texture
			Fx.SetTexture(texture);

			// render the primitives
			Fx.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, vert);

			// re-enable z-buffering & lighting
			Engine.Device.RenderState.ZBufferEnable = true;
			////Engine.Device.RenderState.Lighting = true;
			////device.RenderState.FogEnable = true;
		}




	}
}
