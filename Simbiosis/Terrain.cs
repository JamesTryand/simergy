using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.Samples.DirectX.UtilityToolkit;

namespace Simbiosis
{


	/// <summary>
	/// STATIC MEMBERS: Whole terrain map
	/// INSTANCE MEMBERS: One square of terrain, composed of two triangles in a triangle strip.
	/// Vertices are in the order: NW, NE, SW, SE.
	/// First triangle = 012, second = 123
	/// The triangles are stored in a strip for memory efficiency. However, they are rendered in batches
	/// and so get converted to separate triangles when being added to the batch. The first triangle is
	/// made from points 0,1,2 and the second uses points 3,2,1
	/// 
	/// GRID
	/// Origin is at bottom-left
	/// 
	/// HEIGHTFIELD
	/// Loaded from a bitmap (of arbitrary size). The red channel defines the height - 0=0, 255=MAXHEIGHT
	/// 
	/// TEXTURES
	/// Defined by a bitmap of same dimensions as heightfield. The red channel defines the texture number.
	/// 0= texture 0, 255 = texture[NUMTEXTURES].
	/// A set of texture bitmaps is loaded into a library, using filenames GTEX000.BMP to GTEX###.BMP 
	/// (where ### is NUMTEXTURES)
	/// 
	/// RENDERING
	/// Tiles are registered with the quad map. When the Map calls their Render() method, this simply adds the tile
	/// to a batch of tiles to render. These are stored in separate lists, one per texture. When a batch is full,
	/// or when Purge() is called after rendering the entire map, any non-empty batches are converted to triangle
	/// lists and rendered.
	/// 
	/// 
	/// </summary>
	public class Terrain : Renderable, IDetectable
	{
		// STATIC MEMBERS
		/// <summary> # textures available for terrain. (256 must be exactly divisible by NUMTEXTURES)</summary>
		private const int NUMTEXTURES = 4;
		/// <summary> Max terrain height (height in world coords when colour in heightmap = 255) </summary>
		private const float MAXHEIGHT = Water.WATERLEVEL + 10.0f;


		/// <summary> The global batch of tiles to be rendered this frame, arranged by texture # </summary>
		private static RenderBatch[] batch = new RenderBatch[NUMTEXTURES];		// batches of tiles to be rendered

		/// <summary> The library of terrain textures </summary>
		private static Texture[] textureLib = new Texture[NUMTEXTURES];

		/// <summary> The library of bump map textures </summary>
		private static Texture[] bumpLib = new Texture[NUMTEXTURES];

		/// <summary> The 2D array of tiles </summary>
		private static Terrain[,] tile = null;

		/// <summary> size of one tile in world coords </summary>
		private static float tileWidth = 0;
		private static float tileHeight = 0;

		/// <summary> Size of tile grid </summary>
		private static int gridWidth = 0;
		private static int gridHeight = 0;

		/// <summary> The grid of corner heights (kept for rapid altitude calculations) </summary>
		private static float[,] height = null;


		/// <summary> Material for lighting the textures </summary>
		private static Material material = new Material();


		// INSTANCE MEMBERS

		/// <summary> Triangle strip </summary>
		private BinormalVertex[] mesh = new BinormalVertex[4];

		/// <summary> Face normals for the two triangles (1=NW, 2-SE) </summary>
		private Vector3[] faceNormal = new Vector3[2];

		/// <summary> Planes representing the two triangles, for collision detection </summary>
		private Plane[] plane = new Plane[2];

		/// <summary> Texture number </summary>
		private int texture = 0;
		public int Texture { get { return texture; } set { texture = value; } }
        


		#region ----------------------- STATIC TERRAIN MEMBERS --------------------------

		static Terrain()
		{
			// Create the array of render batches
			for (int i = 0; i < NUMTEXTURES; i++)
			{
				batch[i] = new RenderBatch(20);
			}

			// Load the height map
			using (Bitmap bmp = (Bitmap)Bitmap.FromFile(FileResource.Fsp("terrain", "terrainheight.bmp")))
			{

				// compute scale
				gridWidth = bmp.Width-1;									// the grid of tiles will be 1 smaller than
				gridHeight = bmp.Height-1;									// the grid of heights (last height = RH side of last tile)
				tileWidth = (float)Map.MapWidth / gridWidth;
				tileHeight = (float)Map.MapHeight / gridHeight;

				// Create the blank tiles
				tile = new Terrain[gridWidth,gridHeight];

				// get heightmap into an array, for faster access
				// (Note: bitmap's origin is at TOP-left, so flip Y. Tile grid's origin is bottom-left)
				height = new float[bmp.Width,bmp.Height];
				for (int y = 0; y<bmp.Height; y++)
				{
					for (int x=0; x<bmp.Width; x++)
					{
						height[x,y] = (float)bmp.GetPixel(x, bmp.Height-1-y).R * MAXHEIGHT / 256;
					}
				}
			}																// dispose of the bitmap

			// Create the tiles and define their extents and heights
			for (int y = 0; y<gridHeight; y++)
			{
				for (int x=0; x<gridWidth; x++)
				{
					tile[x,y] = new Terrain(x,y,tileWidth,tileHeight,ref height);
				}
			}

			// Now that the triangles exist, define the vertex normals, by averaging the
			// surface normals of surrounding triangles
			for (int y = 1; y<gridHeight-1; y++)
			{
				for (int x=1; x<gridWidth-1; x++)
				{
					SetVertexNormal(tile,x,y);
				}
			}

			/// TODO: Load the texture map here & set the tiles' texture #s
			using (Bitmap bmp = (Bitmap)Bitmap.FromFile(FileResource.Fsp("terrain", "terraintex.bmp")))
			{
				for (int y = 0; y<gridHeight; y++)
				{
					for (int x=0; x<gridWidth; x++)
					{
						tile[x,y].texture = (int)((float)bmp.GetPixel(x,bmp.Height-1-y).R / 256.0f * NUMTEXTURES);
					}
				}
		
			}

			// Define a material for lighting the textures
			// (May want different materials for each texture later?)
			material.Ambient = Color.FromArgb(64,64,64);
			material.Diffuse = Color.FromArgb(255, 255, 255);
			material.Specular = Color.FromArgb(90, 90, 90);

		}

		/// <summary>
		/// A new D3D device has been created.
		/// if Engine.IsFirstDevice==true, create those once-only things that require a Device to be available
		/// Otherwise, rebuild all those resources that get lost when a device is destroyed during a windowing change etc.
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Terrain.OnDeviceCreated()");
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
			Debug.WriteLine("Terrain.OnDeviceLost()");
			for (int t=0; t<NUMTEXTURES; t++)
			{
				if (textureLib[t]!=null)
					textureLib[t].Dispose();
			}
		}

		/// <summary>
		/// Device has been reset - rebuild unmanaged resources
		/// </summary>
		public static void OnReset()
		{
			Debug.WriteLine("Terrain.OnReset()");
			/// Load the whole library of textures & associated bump map textures
			for (int t=0; t<NUMTEXTURES; t++)
			{
				textureLib[t] = TextureLoader.FromFile(Engine.Device, FileResource.Fsp("terrain", "gtex" + t.ToString("000") + ".png"));
				textureLib[t].GenerateMipSubLevels();

				// Load associated normal map if it exists
				try
				{
					bumpLib[t] = TextureLoader.FromFile(Engine.Device, FileResource.Fsp("terrain", "gtex" + t.ToString("000") + " Normal.png"));
					if (bumpLib[t]!=null)
						bumpLib[t].GenerateMipSubLevels();
				}
				catch { }
			}
		}

		/// <summary>
		/// Do any pre-rendering state changes. All objects rendered between here and 
		/// the call to PostRender will be tiles
		/// </summary>
		public static new void PreRender()
		{
			// --------------------- Set Renderstate ------------------
			Fx.SetWorldMatrix(Matrix.Identity);							// Matrix is fixed at 0,0,0
			//Engine.Device.VertexFormat = CustomVertex.PositionNormalTextured.Format;	// Textured objects
			Engine.Device.VertexDeclaration = BinormalVertex.Declaration;				// bump-mapped objects
			Fx.SetMaterial(material);
			Engine.Device.RenderState.ZBufferEnable = true;								// enabled
			Fx.SetMainTechnique();														// technique

			// --------------------------------------------------------
		}

		/// <summary>
		/// Render the only/latest batch of unrendered tiles
		/// MUST BE CALLED AFTER MAP.RENDER()!!!
		/// </summary>
		public static new void Render(bool clear)
		{
			try
			{
				// Render batches in texture order
				for (int tex = 0; tex < NUMTEXTURES; tex++)
				{
					if (batch[tex].Count > 0)
					{
						// Set texture
						Fx.SetTexture(textureLib[tex], bumpLib[tex]);

						// Draw the triangles
						VertexBuffer VBBatch = new VertexBuffer(typeof(BinormalVertex),
							batch[tex].Count * 6,								// 6 verts per tile
							Engine.Device,
							Usage.WriteOnly,
							BinormalVertex.Format,
							Pool.Default);
						using (GraphicsStream stream = VBBatch.Lock(0, 0, LockFlags.None))
						{
							batch[tex].Reset();
							for (int t = 0; t < batch[tex].Count; t++)
							{
								Terrain tile = (Terrain)batch[tex].GetNext();
								// Add this tile's two triangles to the vertex buffer
								stream.Write(tile.mesh[0]);
								stream.Write(tile.mesh[1]);
								stream.Write(tile.mesh[2]);
								stream.Write(tile.mesh[3]);
								stream.Write(tile.mesh[2]);
								stream.Write(tile.mesh[1]);
							}
							VBBatch.Unlock();
						}
						Engine.Device.SetStreamSource(0, VBBatch, 0, BinormalVertex.StrideSize);				// make this the stream source
						Fx.DrawPrimitives(PrimitiveType.TriangleList, 0, batch[tex].Count * 2);
						VBBatch.Dispose();
						if (clear == true)
							batch[tex].Clear();								// start filling from beginning next time
					}
				}
			}
			catch (Exception e)
			{
				Debug.WriteLine("Error: Unable to render terrain batch");
				throw e;
			}
		}


		/// <summary>
		/// Return the PRECISE height of the terrain at x,y
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static float AltitudeAt(float x, float y)
		{
			// Find the four points on the terrain map that surround the given location,
			// then interpolate between them.
			int x1 = (int)(x / tileWidth);
			int y1 = (int)(y / tileHeight);						// bottom-left grid pixel
			int x2 = x1+1;										// top-right grid pixel
			int y2=y1+1;

			float dx = (x - x1*tileWidth) / tileWidth;		// fraction of the way along and up this tile
			float dy = (y - y1*tileHeight) / tileHeight;

			// bilinear interpolation
			float ht = height[x1,y1] + dx * (height[x2,y1] - height[x1,y1])
					+ dy * (height[x1,y2] - height[x1,y1])
					+ dx * dy * (height[x1,y1] - height[x2,y1] - height[x1,y2] + height[x2,y2]);

			return ht;
		}

		/// <summary>
		/// Return the APPROXIMATE height of the terrain at x,y.
		/// Used for a quick estimate of a possible terrain collision (against an object's bounding sphere)
		/// Simply returns the highest point on the relevant tile
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static float RoughAltitudeAt(float x, float y)
		{
			int gridX = (int)(x / tileWidth);
			int gridY = (int)(y / tileHeight);

			float max = tile[gridX,gridY].mesh[0].Position.Y;
			if (tile[gridX, gridY].mesh[1].Position.Y > max)
				max = tile[gridX, gridY].mesh[1].Position.Y;
			if (tile[gridX, gridY].mesh[2].Position.Y > max)
				max = tile[gridX, gridY].mesh[2].Position.Y;
			if (tile[gridX, gridY].mesh[3].Position.Y > max)
				max = tile[gridX, gridY].mesh[3].Position.Y;
			return max;
		}

		/// <summary>
		/// Return the normal vector of the triangle under x,y.
		/// This is a measure of slope. Since it's a normalised vector we can tell steepness from the .Y member
		/// (1=flat, 0=vertical). The vector also tells us the strike direction (e.g. to make plants grow on the light side)
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static Vector3 SlopeAt(float x, float y)
		{
			// Which tile?
			int gridX = (int)(x / tileWidth);
			int gridY = (int)(y / tileHeight);

			// Which triangle are we over?
			float relX = x / tileWidth - gridX;							// Tile-relative xy as a fraction
			float relY = y / tileHeight - gridY;
			int tri = 0;
			if ((relX + (1-relY)) > 1.0f)								// too far down and to the right for tri[0]?
				tri = 1;
			
			return tile[gridX, gridY].faceNormal[tri];
		}

		/// <summary>
		/// Return the NUMBER of the texture under this x,y
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static int TextureAt(float x, float y)
		{
			int gridX = (int)(x / tileWidth);
			int gridY = (int)(y / tileHeight);
			return tile[gridX, gridY].texture;
		}

		/// <summary>
		/// Return true if point p1 can be seen from point p2 with no intervening terrain.
		/// </summary>
		/// <param name="p1"></param>
		/// <param name="p2"></param>
		/// <returns></returns>
		public static bool InLineOfSight(Vector3 p1, Vector3 p2)
		{
			// Find the start and end tile
			float sx = p1.X / tileWidth;
			float sz = p1.Z / tileHeight;
			float ex = p2.X / tileWidth;
			float ez = p2.Z / tileHeight;

			// Find the longest axis - this determines the step size
			float length = ex - sx;
			if (Math.Abs(ez-sz)>(Math.Abs(length)))
				length = ez - sz;
			float step = Math.Abs(1.0f / length);

			for (float p = 0; p < 1; p+=step)
			{
				float x = sx + p * ((ex-sx)*step);				// interpolated tile xy
				float z = sz + p * ((ez-sz)*step);
				float y = p1.Y + p * ((p2.Y-p1.Y)*step);		// interpolated height
				try 
				{
					if (height[(int)x,(int)z] > y)
						return false;
				}
				catch
				{
					Debug.WriteLine("InLineOfSight out of range ("+x+" "+y+")");
				}
			}
			return true;
		}

		#endregion


		#region ------------------------------- INSTANCE TERRAIN MEMBERS ------------------------------



		/// <summary>
		/// Construct a tile. (Normals and textures must be set separately)
		/// </summary>
		/// <param name="x">position in the heightfield grid</param>
		/// <param name="y">position in the heightfield grid</param>
		/// <param name="tileWidth">width (X) of 1 tile in world coords</param>
		/// <param name="tileHeight">height (Z) of 1 tile in world coords</param>
		/// <param name="height">The 2D array of heights</param>
		public Terrain(int x, int y, float tileWidth, float tileHeight, ref float[,] height)
		{
			// Set relevant Renderable.FlagBits


			float wx = x*tileWidth;										// world xy of bottom-left vertex
			float wy = y*tileHeight;

			// create the two triangles for this square
			mesh[0].Position.X = wx;
			mesh[0].Position.Y = height[x, y + 1];
			mesh[0].Position.Z = wy + tileHeight;

			mesh[1].Position.X = wx + tileWidth;
			mesh[1].Position.Y = height[x + 1, y + 1];
			mesh[1].Position.Z = wy + tileHeight;

			mesh[2].Position.X = wx;
			mesh[2].Position.Y = height[x, y];
			mesh[2].Position.Z = wy;

			mesh[3].Position.X = wx + tileWidth;
			mesh[3].Position.Y = height[x + 1, y];
			mesh[3].Position.Z = wy;

			// Compute the face normals for the two triangles
			faceNormal[0] = ThreeDee.FaceNormal(this.mesh[0].Position, this.mesh[1].Position, this.mesh[2].Position);
			faceNormal[1] = ThreeDee.FaceNormal(this.mesh[3].Position, this.mesh[2].Position, this.mesh[1].Position);

			// Initial vertex normals point in same direction as faces
			// I'll compute proper ones when all the tiles exist
			mesh[0].Normal = faceNormal[0];
			mesh[1].Normal = faceNormal[0];
			mesh[2].Normal = faceNormal[0];
			mesh[3].Normal = faceNormal[1];

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

			// Calculate bounding sphere for culling and collision detection
			AbsSphere.Radius = Geometry.ComputeBoundingSphere(mesh,BinormalVertex.StrideSize,out AbsSphere.Centre);

			// Create two Planes representing the two triangles. These can be used for
			// collision detection etc. (by calculating their dot product with a point)
			plane[0] = Plane.FromPoints(mesh[0].Position,mesh[1].Position,mesh[2].Position);
			plane[1] = Plane.FromPoints(mesh[3].Position,mesh[2].Position,mesh[1].Position);

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
		/// Set the vertex normals for the triangles in this tile.
		/// Each vertex normal is the average of the face normals for surrounding triangles
		/// (some of which are in different tiles)
		/// </summary>
		/// <param name="tile">the array of tiles</param>
		/// <param name="x">our location in the array</param>
		/// <param name="y"></param>
		public static void SetVertexNormal(Terrain[,] tile, int x, int y)
		{
			// NW vertex
			tile[x,y].mesh[0].Normal = Vector3.Normalize(
				tile[x-1,y+1].faceNormal[1]
				+ tile[x,y+1].faceNormal[0] 
				+ tile[x,y+1].faceNormal[1]
				+ tile[x,y].faceNormal[0]
				+ tile[x-1,y].faceNormal[1]
				+ tile[x-1,y].faceNormal[0]
				);

			// NE vertex
			tile[x,y].mesh[1].Normal = Vector3.Normalize(
				tile[x,y+1].faceNormal[1] 
				+ tile[x+1,y+1].faceNormal[0] 
				+ tile[x+1,y+1].faceNormal[1]
				+ tile[x+1,y].faceNormal[0]
				+ tile[x,y].faceNormal[1]
				+ tile[x,y].faceNormal[0]
				);


			// SW vertex
			tile[x,y].mesh[2].Normal = Vector3.Normalize(
				tile[x-1,y].faceNormal[1] 
				+ tile[x,y].faceNormal[0] 
				+ tile[x,y].faceNormal[1]
				+ tile[x,y-1].faceNormal[0]
				+ tile[x-1,y-1].faceNormal[1]
				+ tile[x-1,y-1].faceNormal[0]
				);


			// SE vertex
			tile[x,y].mesh[3].Normal = Vector3.Normalize(
				tile[x,y].faceNormal[1] 
				+ tile[x+1,y].faceNormal[0] 
				+ tile[x+1,y].faceNormal[1]
				+ tile[x+1,y-1].faceNormal[0]
				+ tile[x,y-1].faceNormal[1]
				+ tile[x,y-1].faceNormal[0]
				);

			// Add tangent and binormal vectors calculated from vertex normals
			for (int i = 0; i < 4; i++)
			{
				ThreeDee.TangentFromNormal(tile[x, y].mesh[i].Normal, out tile[x, y].mesh[i].Tangent, out tile[x, y].mesh[i].Binormal);
			}

		}

		/// <summary>
		/// I'm visible, so add me to the render batch
		/// </summary>
		public override void AddToRenderBatch()
		{
			// Store ourselves in the batch for the correct texture
			batch[texture].AddUnsorted(this);
		}


		public void DebugTile()
		{
			Debug.WriteLine("--- TILE ---");
			Debug.WriteLine("Rect S=" + mesh[2].Position.Z + " N=" + mesh[0].Position.Z + " W=" + mesh[0].Position.X + " E=" + mesh[1].Position.X);
			Debug.WriteLine("Sphere centre (X,Z) ="+AbsSphere.Centre.X+","+AbsSphere.Centre.Z+" height="+AbsSphere.Centre.Y+" radius="+AbsSphere.Radius);
			Debug.WriteLine("Tri1 = " + mesh[0].Position.X + "," + mesh[0].Position.Z + " - " + mesh[1].Position.X + "," + mesh[1].Position.Z + " - " + mesh[2].Position.X + "," + mesh[2].Position.Z);
			Debug.WriteLine("Tri2 = " + mesh[3].Position.X + "," + mesh[3].Position.Z + " - " + mesh[2].Position.X + "," + mesh[2].Position.Z + " - " + mesh[1].Position.X + "," + mesh[1].Position.Z);
			Debug.WriteLine("heights1 = " + mesh[0].Position.Y + " - " + mesh[1].Position.Y + " - " + mesh[2].Position.Y + " - ");
			Debug.WriteLine("heights2 = " + mesh[3].Position.Y + " - " + mesh[2].Position.Y + " - " + mesh[1].Position.Y + " - ");
			Debug.WriteLine("FaceNorm1 = "+faceNormal[0].X+","+faceNormal[0].Y+","+faceNormal[0].Z);
			Debug.WriteLine("FaceNorm2 = "+faceNormal[1].X+","+faceNormal[1].Y+","+faceNormal[1].Z);

		}
		public void DebugTileShort()
		{
			Debug.WriteLine("Rect S=" + mesh[2].Position.Z + " N=" + mesh[0].Position.Z + " W=" + mesh[0].Position.X + " E=" + mesh[1].Position.X);
		}


		/// <summary>
		/// Given a bounding sphere (e.g. of a cell), calculate accurately whether this sphere intersects my surface.
		/// The tile being checked lies in the same quad as the cell, but the cell may not be over it.
		/// </summary>
		/// IF there's a collision, return the bounce vector.
		/// This determines the force acting on the cell that hit me.
		/// <param name="cell">The cell that may have collided with me</param>
		/// <returns>The bounce vector (0,0,0 if there's no collision)</returns>
		public override Vector3 CollisionTest(Cell cell)
		{
			Vector3 bounce = new Vector3();
			Vector3 cellCentre = cell.AbsSphere.Centre;
			float cellRadius = cell.AbsSphere.Radius * 1.1f;		// scale up because sphere lags behind reality

			// 1. Reject if the sphere lies horizontally outside this tile's bounding BOX
			//    i.e. is beyond the opposite or adjacent sides of the two triangles
			if ((cellCentre.X + cellRadius) < mesh[0].Position.X)			// too far west
				return bounce;
			if ((cellCentre.X - cellRadius) > mesh[1].Position.X)			// too far east
				return bounce;
			if ((cellCentre.Z + cellRadius) < mesh[2].Position.Z)			// too far south
				return bounce;
			if ((cellCentre.Z - cellRadius) > mesh[0].Position.Z)			// too far north
				return bounce;

			// 2. Reject if the cell is more than one radius above the highest point on the tile
			float max = mesh[0].Position.Y;
			if (mesh[1].Position.Y > max)
				max = mesh[1].Position.Y;
			if (mesh[2].Position.Y > max)
				max = mesh[2].Position.Y;
			if (mesh[3].Position.Y > max)
				max = mesh[3].Position.Y;
			if (cellCentre.Y > max + cellRadius)
				return bounce;

			//// 3. Reject if the sphere lies outside the plane of both triangles
			float dist0 = plane[0].Dot(cellCentre);
			float dist1 = plane[1].Dot(cellCentre);
			if ((dist0 > cellRadius) && (dist1 > cellRadius))
				return bounce;

			// 4. We're close enough to justify bounding-box checks for each mesh in the cell
			if (cell.CollisionTest(this.mesh) == false)
				return bounce;

			// If we get here, at least one point is in contact with the tile
			// Construct a bounce vector - the movement required to return us to the surface...
			//float dist0 = plane[0].Dot(cellCentre);
			//float dist1 = plane[1].Dot(cellCentre);

			// First, find which facet we've collided with. This will (presumably) be the one we're NEAREST
			// the surface of
			int facet = 0;
			if (dist1 < dist0)
			{
				facet = 1;
			}

			// Our closing speed with the surface will be the difference between out present distance and
			// our previous one
			float movement = (plane[facet].Dot(cell.OldLocation) - plane[facet].Dot(cell.Location))*10.0f;
			if (movement < 0) movement = 0;

			// The bounce vector will be the surface normal scaled by our closing speed
			movement = (movement * movement + 1.0f);
			bounce = Vector3.Scale(faceNormal[facet], movement);
			return bounce;
		}




		#endregion

		#region ---------- IDetectable Members (sensor requests) -----------------

		public Vector3 ReqLocation()
		{
			return AbsSphere.Centre;
		}

		public Vector3 ReqVelocity()
		{
			return new Vector3();
		}

		public float ReqSize()
		{
			return AbsSphere.Radius;
		}

		public float ReqMass()
		{
			return 0;
		}

        /// <summary> Return the tile's colours - for now just return null, since photoreceptors only see organisms </summary>
        /// <returns></returns>
        public List<ColorValue> ReqSpectrum()
        {
            return null;
        }

        /// <summary> Return my approx depth as a fraction (0=surface, 1=deepest) </summary>
        /// <returns></returns>
        public float ReqDepth()
        {
            return (Water.WATERLEVEL - this.AbsSphere.Centre.Y) / Water.WATERLEVEL;
        }



		/// <summary> We've been sent a Stimulus. 
		/// <param name="stimulus">The stimulus information</param>
		/// <returns>Return true if the stimulus was handled</returns>
		public bool ReceiveStimulus(Stimulus stim)
		{
			switch (stim.Type)
			{
				// Stimuli that we know how to handle...

				// TODO: Add stimulus cases here, and return true
				case "dummy":
					break;
			}
			return false;
		}




		#endregion



	}


}
