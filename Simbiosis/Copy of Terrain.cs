using System;
using System.Drawing;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{


	/// <summary>
	/// One square of terrain, composed of two triangles.
	/// Vertices are in the order: left triangle - NW, NE, SW;  right triangle - SE, SW, NE.
	/// Note: Triangle strips would take up less space, but I want to batch them and render them all
	/// at once, so I need to use discrete triangles
	/// </summary>
	public class Tile : Renderable
	{
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
			float wx = x*tileWidth;										// world xy of top-left vertex
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

			// Compute the face normals for the tow triangles
			faceNormal1 = ThreeDee.FaceNormal(this.mesh[0].Position, this.mesh[1].Position, this.mesh[2].Position);
			faceNormal2 = ThreeDee.FaceNormal(this.mesh[3].Position, this.mesh[2].Position, this.mesh[1].Position);

			// Initial vertex normals point in same direction as faces
			// I'll compute proper ones when all the tiles exist
			mesh[0].Normal = faceNormal1;
			mesh[1].Normal = faceNormal1;
			mesh[2].Normal = faceNormal1;
			mesh[3].Normal = faceNormal2;

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

		public override void Render()
		{
			// TODO:  Add Tile.Render implementation
		}

	}






	/// <summary>
	/// The terrain
	/// </summary>
	public class Terrain : IDisposable
	{


		/// <summary> The library of terrain textures </summary>
		private Texture[] texturelib = null;
		private Texture texture = null;								// TEMP SINGLE TEXTURE UNTIL LIBRARY IS WRITTEN!

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
						height[x,y] = (float)bmp.GetPixel(x,y).R / 256.0f;
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
		/// Render the mesh
		/// </summary>
		public void Render()
		{
			Engine.Device.Transform.World = Matrix.Translation(0,0,0);
			Engine.Device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
			Engine.Device.SetTexture(0,texture);
			// Use linear filtering on the magnified texels in mipmap
			Engine.Device.SamplerState[0].MagFilter = TextureFilter.Linear;
			Engine.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, mesh);

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
