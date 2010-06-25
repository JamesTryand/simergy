using System;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;




namespace Simbiosis
{
	/// <summary>
	/// The water surface
	/// </summary>
	public class Water : Renderable
	{
		#region ------------------------ static members --------------------------------

		/// <summary> Current water level </summary>
		public const float WATERLEVEL = 80.0f;
		/// <summary> number of tiles across/down map  </summary>
		private const int NUMTILES = 16;

		/// <summary> size of one tile in world coords </summary>
		private static float tileWidth = 0;
		private static float tileHeight = 0;

		/// <summary> textures </summary>
		private const int NUMCAUSTICS = 32;												// # animation frames
		private static Texture[] texCaustic = new Texture[NUMCAUSTICS];					// texture set for caustics
		private const int NUMWAVES = 32;												// # animation frames
		private static Texture[] texNormal = new Texture[NUMCAUSTICS];					// texture set for surface normal map
		private static CubeTexture texReflection = null;								// cube map for reflections

		/// <summary> The material </summary>
		private static Material material = new Material();

		/// <summary> The global batch of tiles to be rendered this frame </summary>
		private static RenderBatch batch = new RenderBatch(60);						// lists of tiles to be rendered

		/// <summary> sea surface as grid of normal-mappable quads </summary>
		private static Water[,] tile = new Water[NUMTILES, NUMTILES];



		static Water()
		{
			// Create base material
			material.Diffuse = Color.FromArgb(128, 64, 64, 64);					// transparency shows skybox through


			// compute scale
			tileWidth = (float)Map.MapWidth / NUMTILES;
			tileHeight = (float)Map.MapHeight / NUMTILES;

			// Create and initialise the tiles 
			for (int y = 0; y < NUMTILES; y++)
			{
				for (int x = 0; x < NUMTILES; x++)
				{
					tile[x,y] = new Water(x, y);
				}
			}



		}




		/// <summary>
		/// A new D3D device has been created.
		/// if Engine.IsFirstDevice==true, create those once-only things that require a Device to be available
		/// Otherwise, rebuild all those resources that get lost when a device is destroyed during a windowing change etc.
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Water.OnDeviceCreated()");
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
			Debug.WriteLine("Water.OnDeviceLost()");
			for (int t = 0; t < NUMCAUSTICS; t++)
			{
				if (texCaustic[t] != null)
					texCaustic[t].Dispose();
			}
		}

		/// <summary>
		/// Device has been reset - rebuild unmanaged resources
		/// </summary>
		public static void OnReset()
		{
			Debug.WriteLine("Water.OnReset()");

			// Load caustic textures
			for (int i = 0; i < NUMCAUSTICS; i++)
			{
				string fsp = "caust" + i.ToString("00") + ".tga";
				texCaustic[i] = TextureLoader.FromFile(Engine.Device, FileResource.Fsp("textures", fsp));
				texCaustic[i].GenerateMipSubLevels();
			}

			// Load wave textures (normal maps)
			for (int i = 0; i < NUMWAVES; i++)
			{
				string fsp = "wave" + i.ToString("00") + "DOT3.tga";
				texNormal[i] = TextureLoader.FromFile(Engine.Device, FileResource.Fsp("textures", fsp));
				texNormal[i].GenerateMipSubLevels();
			}

			// Load the cube map for the reflections - NOTE!!!! NEED TO CHECK CAPABILITIES IN REAL LIFE - TextureLoader class can do this
			texReflection = TextureLoader.FromCubeFile(Engine.Device, FileResource.Fsp("textures", "WaterEnv.dds"));		// Longer overload needed???


		}

		/// <summary>
		/// Do any pre-rendering state changes. All objects rendered between here and 
		/// the call to PostRender will be tiles
		/// </summary>
		public static new void PreRender()
		{
			// --------------------- Set Renderstate ------------------
			Fx.SetWorldMatrix(Matrix.Identity);							// Matrix is fixed at 0,0,0
			//Engine.Device.VertexFormat = CustomVertex.PositionNormalTextured.Format;		// Textured objects
			Engine.Device.VertexDeclaration = BinormalVertex.Declaration;					// bump-mapped objects
			Fx.SetMaterial(material);
			Engine.Device.RenderState.ZBufferEnable = true;									// enabled
			Fx.SetWaterTechnique();															// technique

			// --------------------------------------------------------

			// Change the animation textures for waves & caustics
			Fx.SetTexture(null, texNormal[(int)(Scene.TotalElapsedTime * 20.0f) % NUMWAVES]);		// waves go into bump map texture
			Fx.SetCausticTexture(texCaustic[(int)(Scene.TotalElapsedTime * 20.0f) % NUMCAUSTICS]);	// caustics go into special texture
			Fx.SetEnvTexture(texReflection);														// cube map for reflections
		}

		/// <summary>
		/// Render the only/latest batch of unrendered tiles
		/// MUST BE CALLED AFTER MAP.RENDER()!!!
		/// </summary>
		public static new void Render(bool clear)
		{
			RenderBatch(clear);
		}

		/// <summary>
		/// Render the batches of visible tiles
		/// </summary>
		public static void RenderBatch(bool clear)
		{
			try
			{
				if (batch.Count > 0)
				{
					// Draw the triangles
					VertexBuffer VBBatch = new VertexBuffer(typeof(BinormalVertex),
															batch.Count * 6,								// 6 verts per tile
															Engine.Device,
															Usage.WriteOnly,
															BinormalVertex.Format,
															Pool.Default);
					using (GraphicsStream stream = VBBatch.Lock(0, 0, LockFlags.None))
					{
						batch.Reset();
						for (int t = 0; t < batch.Count; t++)
						{
							Water tile = (Water)batch.GetNext();
							// Add this tile's two triangles to the vertex buffer
							// NOTE: we want to see the quad from underneath, so the vertex order is different to get clockwise tris
							stream.Write(tile.quad[0]);
							stream.Write(tile.quad[2]);
							stream.Write(tile.quad[1]);
							stream.Write(tile.quad[3]);
							stream.Write(tile.quad[1]);
							stream.Write(tile.quad[2]);
						}
						VBBatch.Unlock();
					}
					Engine.Device.SetStreamSource(0, VBBatch, 0, BinormalVertex.StrideSize);				// make this the stream source
					Fx.DrawPrimitives(PrimitiveType.TriangleList, 0, batch.Count * 2);
					VBBatch.Dispose();
					if (clear == true)
						batch.Clear();																	// start filling from beginning next time
				}
			}
			catch (Exception e)
			{
				Debug.WriteLine("unable to render water batch");
				throw e;
			}
		}

		/// <summary>
		/// I'm visible, so render me. In the case of tiles, we don't actually render now. Instead we
		/// add ourself to a batch and render the whole batch at the end (or when it gets full). This is
		/// much more efficient
		/// </summary>
		public override void AddToRenderBatch()
		{
			// Store ourselves in the batch
			batch.AddUnsorted(this);
		}






		#endregion
		#region ------------------------------------------ instance members (quads) ------------------------------------------

		// The quad, including normals and binormals
		private BinormalVertex[] quad = new BinormalVertex[4];



		public Water(int x, int y)
		{
			// corner locations
			float wx = x * tileWidth;										// world xy of bottom-left vertex
			float wy = y * tileHeight;

			// create the two triangles for this quad
			quad[0].Position.X = wx;
			quad[0].Position.Y = WATERLEVEL;
			quad[0].Position.Z = wy + tileHeight;

			quad[1].Position.X = wx + tileWidth;
			quad[1].Position.Y = WATERLEVEL;
			quad[1].Position.Z = wy + tileHeight;

			quad[2].Position.X = wx;
			quad[2].Position.Y = WATERLEVEL;
			quad[2].Position.Z = wy;

			quad[3].Position.X = wx + tileWidth;
			quad[3].Position.Y = WATERLEVEL;
			quad[3].Position.Z = wy;

			// Normals point downwards
			for (int v = 0; v < 4; v++)
			{
				Vector3 norm = new Vector3(0f, -1f, 0f);
				quad[v].Normal = norm;
				
				// Calculate tangent and binormal
				ThreeDee.TangentFromNormal(norm, out quad[v].Tangent, out quad[v].Binormal);
			}
			
			// Each quad has a complete normal map texture on it
			quad[0].Tu = 0.0f;
			quad[0].Tv = 0.0f;
			quad[1].Tu = 1.0f;
			quad[1].Tv = 0.0f;
			quad[2].Tu = 0.0f;
			quad[2].Tv = 1.0f;
			quad[3].Tu = 1.0f;
			quad[3].Tv = 1.0f;

			// Calculate bounding sphere for culling and collision detection
			AbsSphere.Radius = Geometry.ComputeBoundingSphere(quad, BinormalVertex.StrideSize, out AbsSphere.Centre);

			// Register the tile with the map
			Map.Add(this);

		}






		#endregion



	}




}
