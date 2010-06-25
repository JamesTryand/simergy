using System;
using System.Drawing;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{


	/// <summary>
	/// One square of terrain, composed of two triangles in a triangle strip.
	/// Vertices are in the order: NW, NE, SW, SE.
	/// First triangle = 012, second = 123
	/// The triangles are stored in a strip for memory efficiency. However, they are rendered in batches
	/// and so get converted to separate triangles when being added to the batch. The first triangle is
	/// made from points 0,1,2 and the second uses points 3,2,1
	/// </summary>
	public class Tile : Renderable
	{

		/// <summary> The global batch of triangles to be rendered this frame (in video memory) </summary>
		public const int BATCHSIZE = 3000;		// Max # verts in batch buffer
		public static int NumInBatch = 0;		// # vertices currently in buffer
		public static VertexBuffer Batch = new VertexBuffer(typeof(CustomVertex.PositionNormalTextured),
											BATCHSIZE,
											Engine.Device,
											Usage.WriteOnly,
											CustomVertex.PositionNormalTextured.Format,
											Pool.Default);

		/// <summary> Triangle strip </summary>
		private CustomVertex.PositionNormalTextured[] mesh = new CustomVertex.PositionNormalTextured[4];

		/// <summary> Face normals for the two triangles (1=NW, 2-SE) </summary>
		private Vector3 faceNormal1 = new Vector3();
		public Vector3 FaceNormal1 { get { return faceNormal1; } }
		private Vector3 faceNormal2 = new Vector3();
		public Vector3 FaceNormal2 { get { return faceNormal2; } }

		/// <summary> ref to the texture </summary>
		private Texture texture;

		/// <summary> Overrides of Renderable members - abs centre and radius for culling </summary>
		private float radius;
		private Vector3 absCentre;
		public override float Radius { get { return radius; } }
		public override Vector3 AbsCentre { get { return absCentre; } }

		/// <summary>
		/// Construct a tile. (Normals and textures must be set separately)
		/// </summary>
		/// <param name="x">position in the heightfield grid</param>
		/// <param name="y">position in the heightfield grid</param>
		/// <param name="tileWidth">width (X) of 1 tile in world coords</param>
		/// <param name="tileHeight">height (Z) of 1 tile in world coords</param>
		/// <param name="height">The 2D array of heights</param>
		public Tile(int x, int y, float tileWidth, float tileHeight, ref float[,] height)
		{
			float wx = x*tileWidth;										// world xy of bottom-left vertex
			float wy = y*tileHeight;

			// create the two triangles for this square
			mesh[0].X = wx;
			mesh[0].Y = height[x,y+1];
			mesh[0].Z = wy+tileHeight;

			mesh[1].X = wx+tileWidth;
			mesh[1].Y = height[x+1,y+1];
			mesh[1].Z = wy+tileHeight;

			mesh[2].X = wx;
			mesh[2].Y = height[x,y];
			mesh[2].Z = wy;

			mesh[3].X = wx+tileWidth;
			mesh[3].Y = height[x+1,y];
			mesh[3].Z = wy;

			// Compute the face normals for the two triangles
			faceNormal1 = ThreeDee.FaceNormal(this.mesh[0].Position, this.mesh[1].Position, this.mesh[2].Position);
			faceNormal2 = ThreeDee.FaceNormal(this.mesh[3].Position, this.mesh[2].Position, this.mesh[1].Position);

			// Initial vertex normals point in same direction as faces
			// I'll compute proper ones when all the tiles exist
			mesh[0].Normal = faceNormal1;
			mesh[1].Normal = faceNormal1;
			mesh[2].Normal = faceNormal1;
			mesh[3].Normal = faceNormal2;

			// Calculate centre and "radius" for culling operations
			float midHeight = (mesh[1].Y - mesh[2].Y) + mesh[1].Y;					// height at mid-point
			float w = tileWidth / 2.0f;
			float h = tileHeight / 2.0f;
			absCentre = new Vector3(wx+w,midHeight,wy+h);
			radius = (float)Math.Sqrt(w*w + h*h);									// dist from centre to corner

			// Register the tile with the map
			Map.Add(this);

		}

		/// <summary>
		/// IDispose interface
		/// </summary>
		public override void Dispose()
		{
			// TODO: dispose of resources
			Debug.WriteLine("Disposing of tile resources ");
		}


		/// <summary>
		/// Define the tile texture
		/// </summary>
		/// <param name="t"></param>
		public void SetTexture(Texture t)
		{
			// Set the texture to use
			texture = t;

			// define the texture coordinates
			// Assume each quad has a complete texture on it
			mesh[0].Tu = 0.0f;
			mesh[0].Tv = 0.0f;
			mesh[1].Tu = 1.0f;
			mesh[1].Tv = 0.0f;
			mesh[2].Tu = 0.0f;
			mesh[2].Tv = 1.0f;
			mesh[3].Tu = 1.0f;
			mesh[3].Tv = 1.0f;
		}

		/// <summary>
		/// Set the vertex normals for the triangles in this tile.
		/// Each vertex normal is the average of the face normals for surrounding triangles
		/// (some of which are in different tiles)
		/// </summary>
		/// <param name="tile">the array of tiles</param>
		/// <param name="x">our location in the array</param>
		/// <param name="y"></param>
		public static void SetVertexNormal(Tile[,] tile, int x, int y)
		{
			// NW vertex
			tile[x,y].mesh[0].Normal = Vector3.Normalize(
				tile[x-1,y-1].FaceNormal2 
				+ tile[x,y-1].FaceNormal1 
				+ tile[x,y-1].FaceNormal2
				+ tile[x,y].FaceNormal1
				+ tile[x-1,y].FaceNormal2
				+ tile[x-1,y].FaceNormal1
				);

			// NE vertex
			tile[x,y].mesh[1].Normal = Vector3.Normalize(
				tile[x,y-1].FaceNormal2 
				+ tile[x+1,y-1].FaceNormal1 
				+ tile[x+1,y-1].FaceNormal2
				+ tile[x+1,y].FaceNormal1
				+ tile[x,y].FaceNormal2
				+ tile[x,y].FaceNormal1
				);


			// SW vertex
			tile[x,y].mesh[2].Normal = Vector3.Normalize(
				tile[x-1,y].FaceNormal2 
				+ tile[x,y].FaceNormal1 
				+ tile[x,y].FaceNormal2
				+ tile[x,y+1].FaceNormal1
				+ tile[x-1,y+1].FaceNormal2
				+ tile[x-1,y+1].FaceNormal1
				);


			// SE vertex
			tile[x,y].mesh[3].Normal = Vector3.Normalize(
				tile[x,y].FaceNormal2 
				+ tile[x+1,y].FaceNormal1 
				+ tile[x+1,y].FaceNormal2
				+ tile[x+1,y+1].FaceNormal1
				+ tile[x,y+1].FaceNormal2
				+ tile[x,y+1].FaceNormal1
				);
			
		}

		public override void Update()
		{
			// TODO:  Add Tile.Update implementation
		}

		/// <summary>
		/// I'm visible, so render me. In the case of tiles, we don't actually render now. Instead we
		/// add our two triangles to a batch and render it at the end (or when it gets full). This is
		/// much more efficient
		/// </summary>
		public override void Render()
		{
/////			DebugTile();

			// add your two triangles (as separate triangles, not strip)
			// to the video memory buffer
			GraphicsStream stream = Batch.Lock(NumInBatch, 0, LockFlags.None);
			stream.Write(mesh[0]);
			stream.Write(mesh[1]);
			stream.Write(mesh[2]);
			stream.Write(mesh[3]);				// second triangle uses two points from first
			stream.Write(mesh[2]);
			stream.Write(mesh[1]);
			Batch.Unlock();

			NumInBatch += 6;					// 6 more vertices have been added

			// If there's no more room in the triangle batch, render the batch to the screen
			// and clear it
			if (NumInBatch>=BATCHSIZE)
			{
				RenderBatch();
			}
		}


		/// <summary>
		/// Render the current batch of visible tiles
		/// </summary>
		public static void RenderBatch()
		{
			try
			{
				if (NumInBatch!=0)											// if we haven't already sent this batch...
				{
					// Set world matrix for no transformation
					Engine.Device.Transform.World = Matrix.Identity;
					// Set expected vertex format 
					Engine.Device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
					// Create a material
					Material material = new Material();
					material.Ambient = Color.White;
					material.Diffuse = Color.White;
					Engine.Device.Material = material;
					// Set texture - THIS ISN'T GOING TO WORK WHEN I HAVE MULTIPLE TEXTURES!!!!!!!!!!!!!!!!!!!!!!!!!!!!
					Engine.Device.SetTexture(0,Terrain.texture);
					// Use linear filtering on the magnified texels in mipmap
					Engine.Device.SamplerState[0].MagFilter = TextureFilter.Linear;
					// Draw the triangles
					Engine.Device.SetStreamSource(0,Batch,0);
					Engine.Device.DrawPrimitives(PrimitiveType.TriangleList, 0, NumInBatch / 3);
				
				}
				NumInBatch = 0;												// start filling from beginning next time
			}
			catch (Exception e)
			{
				Debug.WriteLine("unable to render terrain batch");
				throw;
			}
		}

		public void DebugTile()
		{
			Debug.WriteLine("Tri1 = "+mesh[0].X+","+mesh[0].Z+" - "+mesh[1].X+","+mesh[1].Z+" - "+mesh[2].X+","+mesh[2].Z);
			Debug.WriteLine("Tri2 = "+mesh[3].X+","+mesh[3].Z+" - "+mesh[2].X+","+mesh[2].Z+" - "+mesh[1].X+","+mesh[1].Z);
			Debug.WriteLine("heights1 = "+mesh[0].Y+" - "+mesh[1].Y+" - "+mesh[2].Y+" - ");
			Debug.WriteLine("heights2 = "+mesh[3].Y+" - "+mesh[2].Y+" - "+mesh[1].Y+" - ");
			Debug.WriteLine("FaceNorm1 = "+FaceNormal1.X+","+FaceNormal1.Y+","+FaceNormal1.Z);
			Debug.WriteLine("FaceNorm2 = "+FaceNormal2.X+","+FaceNormal2.Y+","+FaceNormal2.Z);

		}

	}






	/// <summary>
	/// The terrain
	/// </summary>
	public class Terrain : IDisposable
	{
		/// <summary> Max terrain height (height in world coords when colour in heightmap = 255) </summary>
		private const float MAXHEIGHT = 8.0f;

		/// <summary> The library of terrain textures </summary>
		private Texture[] texturelib = null;
		public static Texture texture = null;								// TEMP SINGLE TEXTURE UNTIL LIBRARY IS WRITTEN!

		/// <summary> The 2D array of tiles </summary>
		private Tile[,] tile = null;

		/// <summary> size of one tile in world coords </summary>
		private float tileWidth = 0;
		private float tileHeight = 0;

		/// <summary> Size of tile grid </summary>
		private int gridWidth = 0;
		private int gridHeight = 0;

		public Terrain()
		{
			float[,] height = null;

			// Load the height map
			using (Bitmap bmp = (Bitmap)Bitmap.FromFile(FileResource.Fsp("heightfield.bmp")))
			{

				// compute scale
				gridWidth = bmp.Width;										// scale is determined by the size of the
				gridHeight = bmp.Height;									// heightfield & the size of the map
				tileWidth = Map.MapWidth / bmp.Width;
				tileHeight = Map.MapHeight / bmp.Height;
				gridWidth = bmp.Height-1;									// the grid of tiles will be 1 smaller than
				gridHeight = bmp.Height-1;									// the grid of heights (last height = RH side of last tile)

				// Create the blank tiles
				tile = new Tile[gridWidth,gridHeight];

				// get heightmap into a temp array, for faster access
				height = new float[bmp.Width,bmp.Height];
				for (int y = 0; y<bmp.Height; y++)
				{
					for (int x=0; x<bmp.Width; x++)
					{
///////////////////////						height[x,y] = (float)bmp.GetPixel(x,y).R / 256.0f * MAXHEIGHT;
						height[x,y] = 0;
					}
				}
			}																// dispose of the bitmap

			// Create the tiles and define their extents and heights
			for (int y = 0; y<gridHeight; y++)
			{
				for (int x=0; x<gridWidth; x++)
				{
					tile[x,y] = new Tile(x,y,tileWidth,tileHeight,ref height);
				}
			}

			// Now that the triangles exist, define the vertex normals, by averaging the
			// surface normals of surrounding triangles
			for (int y = 1; y<gridHeight-1; y++)
			{
				for (int x=1; x<gridWidth-1; x++)
				{
					Tile.SetVertexNormal(tile,x,y);
				}
			}

			/// TODO: Load the texture map here & create the texture library,
			/// then set the tile textures
			




			// Reload resources on reset of device
			Engine.Device.DeviceReset += new System.EventHandler(this.OnReset);
			// Load them for the first time now
			OnReset(null, null);
						
		}

		public void OnReset(object sender, EventArgs e)
		{
			Debug.WriteLine("Terrain.Reset()");
			/// TODO: Load the whole library of textures!
			texture = TextureLoader.FromFile(Engine.Device,FileResource.Fsp("ground.bmp"));
			texture.GenerateMipSubLevels();
		}

		/// <summary>
		/// IDispose interface
		/// </summary>
		public void Dispose()
		{
			// TODO: dispose of resources
			Debug.WriteLine("Disposing of terrain resources ");
		}


		/// <summary>
		/// Render the only/latest batch of unrendered tiles
		/// MUST BE CALLED AFTER MAP.RENDER()!!!
		/// </summary>
		public void Render()
		{
			Tile.RenderBatch();
		}

		/// <summary>
		/// Return the height of the terrain at x,y
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static float AltitudeAt(float x, float y)
		{
			return 0;			// TEMP!!!!!!
		}

		/// <summary>
		/// Return the height of the water surface
		/// </summary>
		/// <returns></returns>
		public static float SurfaceHeight()
		{
			return 30.0f;		// TEMP !!!!!!
		}
		}
}
