using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{

	/// <summary>
	/// This struct holds data about one type of Scenery (weed, say)
	/// </summary>
	class SceneryInfo
	{
		/// <summary> number of animation frames (columns) in the texture </summary>
		public int numFrames;
		/// <summary> number of animation frames per second (if 0, frames are single-pose variants of the type) </summary>
		public int frameRate;
		/// <summary> pixel Y at which this type/row can be found </summary>
		public int top;
		/// <summary> size of each frame in pixels </summary>
		public int width, height;
		/// <summary> position of the "anchor" of the sprite (0,0 = top-left of FRAME) </summary>
		public int anchorX, anchorY;
		/// <summary> Real world size = pixel size * scale </summary>
		public float scale;
		/// <summary> minimum & maximum acceptable terrain altitude
		/// (if minAlt==-1, instances are suspended in mid-water) </summary>
		public float minAlt;
		public float maxAlt; 
		/// <summary> range of acceptable slopes (face normal.Y, where 0=vertical, 1=flat) </summary>
		public float minSlope; 
		public float maxSlope;
		/// <summary> list of terrain tile type numbers that can support this type of object
		/// (null means they can go anywhere) </summary>
		public int[] validTileTypes;
        /// <summary> Number of instances on the map </summary>
        public int numberToCreate;

		/// <summary> Triangle strip to use as the basis for all sprites of this type </summary>
		public CustomVertex.PositionNormalTextured[] quad = new CustomVertex.PositionNormalTextured[4];


		/// <summary>
		/// Initialise
		/// </summary>
		/// <param name="type">The index number identifying which type (sprite row) we are in the list</param>
		/// <param name="numFrames">number of animation frames (columns) in the texture</param>
		/// <param name="frameRate">number of animation frames per second (if 0, frames are single-pose variants of the type)</param>
		/// <param name="top">Pixel Y at which this row of frame starts</param>
		/// <param name="width">size of each frame in pixels</param>
		/// <param name="height">size of each frame in pixels</param>
		/// <param name="anchorX">position of the "centre" of the sprite (0,0 = top-left of FRAME)</param>
		/// <param name="anchorY">position of the "centre" of the sprite (0,0 = top-left of FRAME)</param>
		/// <param name="scale">real-world scale</param>
		/// <param name="minAlt">minimum & maximum acceptable terrain altitude (or -1)</param>
		/// <param name="maxAlt">minimum & maximum acceptable terrain altitude</param>
		/// <param name="minSlope">range of acceptable slopes (face normal.Y, where 0=vertical, 1=flat)</param>
		/// <param name="maxSlope">range of acceptable slopes (face normal.Y, where 0=vertical, 1=flat)</param>
		/// <param name="validTileTypes">list of tile type numbers that can support this type of scenery (or null)</param>
        /// <param name="numberToCreate">how many instances of this type to create</param>
		public SceneryInfo(
					int type,
					int numFrames, 
					int frameRate,
					int top,
					int width, int height, 
					int anchorX, int anchorY,
					float scale,
					float minAlt, float maxAlt,
					float minSlope, float maxSlope,
					int[] validTileTypes,
                    int numberToCreate)
		{
			this.numFrames = numFrames;
			this.frameRate = frameRate;
			this.top = top;
			this.width = width;
			this.height = height;
			this.anchorX = anchorX;
			this.anchorY = anchorY;
			this.scale = scale;
			this.minAlt = minAlt;
			this.maxAlt = maxAlt;
			this.minSlope = minSlope;
			this.maxSlope = maxSlope;
			this.validTileTypes = validTileTypes;
            this.numberToCreate = numberToCreate;

			// set up the quad template according to scale, aspect ratio, etc.
			SetTemplateQuad();
		}

		/// <summary>
		/// Set up an untransformed quad (triangle strip) for use by sprites of this type.
		/// The quad will have normals and be set up with corners at the right local coordinates for the
		/// scale and aspect ratio of the sprite. To render an instance, take a local copy of this quad,
		/// set its u,v coordinates to the right part of the texture for the animation frame and variant,
		/// then transform the quad's vertices into the camera orientation and world location.
		/// </summary>
		private void SetTemplateQuad()
		{
			// Build the untransformed triangle strip...

			// Use scaling factor and bitmap aspect ratio to compute scale
			float hscale = ((float)width / (float)height) * scale;	// scaled width in world coords
			float vscale = scale;

			float afx = ((float)anchorX / (float)width);			// anchor as a fraction of width/ht
			float afy = ((float)anchorY / (float)height);			// can mult hscale by afx or 1-afx to get left/right

			// Corners
			quad[0].Position = new Vector3(-hscale*afx,		 vscale*afy, 0);			// NW
			quad[1].Position = new Vector3( hscale*(1-afx),	 vscale*afy, 0);			// NE
			quad[2].Position = new Vector3(-hscale*afx,		-vscale*(1-afy), 0);		// SW
			quad[3].Position = new Vector3( hscale*(1-afx),	-vscale*(1-afy), 0);		// SE

			// splay the normals outwards, so that the sprite is lit as if it were cylindrical
			quad[0].Normal = new Vector3(-0.5f,0,-0.5f);
			quad[1].Normal = new Vector3( 0.5f,0,-0.5f);
			quad[2].Normal = new Vector3(-0.5f,0,-0.5f);
			quad[3].Normal = new Vector3( 0.5f,0,-0.5f);

		}
	}




	/// <summary>
	/// Sprite-based scenery (weeds, rocks etc.).
	/// Scenery may or may not be collidable; it may or may not receive messages (weed can be eaten, for instance).
	/// </summary>
	/// <remarks>
	/// BASIC STRUCTURE
	/// A static library of Scenery objects is maintained, and each Scenery object is registered with the Map.
	/// The map calls the visible objects' Render() methods, each of which batches another copy of the static Sprite
	/// at a particular location and with a particular appearance.
	/// 
	/// APPEARANCE
	/// All Scenery sprites are billboarded.
	/// A SINGLE texture contains all types of Scenery sprite and all animation frames.
	/// Every ROW of images in the texture represents one type of scenery object.
	/// Each COLUMN in the row represents one frame in the animation for that variant.
	/// IMPORTANT: THE TEXTURE MUST BE A POWER OF TWO IN HEIGHT
	/// 
	/// NOTE: I CAN PROBABLY IMPROVE THE APPEARANCE BY REPLACING MORE THAN JUST LIME GREEN WITH PARTIAL ALPHAS
	/// AFTER I LOAD THE TEXTURE - LOOK FOR GREENS THAT ARE CLOSE TO LIME GREEN, AND REPLACE ALPHA BY BRIGHTNESS
	/// 
	/// NOTE2: MIP-MAPPING is currently turned off. Can't get it to work properly. When I set 
	/// Engine.Device.SamplerState[0].MipFilter = TextureFilter.xxx to anything other than .None I get weird effects,
	/// and none of the options to generate mipmaps in the TextureLoader function seem to make the texture visible.
	/// Worth playing around with some time (but possibly it won't work because I'm displaying PART of the texture)
	/// 
	/// </remarks>
	public class Scenery : Renderable
	{
		//------------- static fields ------------------

		/// <summary> Number of different scenery types </summary>
		private const int NUMTYPES = 3;

		/// <summary> facts about each type of Scenery </summary>
 		private static SceneryInfo[] typeList = {
			new SceneryInfo(0,10, 5,    0, 96,256,  48,250, 8.0f, 0,20, 0.5f,1, null, 200),		// weed0
			new SceneryInfo(1, 7, 0,  256, 128,128, 64,127, 6.0f, 0,20, 0.9f,1, null, 200),		// rocks
            new SceneryInfo(2, 8, 9,  384,  64,512, 32,510, 24.0f,0,20, 0.2f,1, null,  50),      // bubbles
		};

		/// <summary> The list of individual Scenery instances </summary>
		private static List<Scenery> sceneryList = new List<Scenery>();
		/// <summary> The batch of instances to be rendered this frame (separated by texture, sorted by dist) </summary>
		public static RenderBatch batch = new RenderBatch(100);

		/// <summary> the texture containing all types (rows) and frames (cols) </summary>
		public static Texture texture = null;
		/// <summary> Material for lighting the texture </summary>
		private static Material material = new Material();

		/// <summary> The total size of the texture bitmap (for calculating texture coords of frames) </summary>
		private static float texWidth = 0;
		private static float texHeight = 0;

		/// <summary> A matrix that will face all visible sprites towards the camera, calculated once per frame </summary>
		private static Matrix facingMatrix = Matrix.Identity;

		// ------------ instance fields -------------

		/// <summary> Info about my type </summary>
		private SceneryInfo info = null;
		/// <summary> My location in world coordinates </summary>
		private Vector3 location = new Vector3();
		/// <summary> My current/initial frame number (randomise to desynchronise anims)	
		/// (float so that it can accumulate fractions of a frame) /// </summary>
		private float frame = 0;



		// ------------ static methods ----------------


		/// <summary>
		/// Create the initial list of Scenery objects
		/// </summary>
		static Scenery()
		{
			// Define a material for lighting the textures
			// (May want different materials for each texture later?)
			material.Ambient = Color.FromArgb(255,255,255);
			material.Diffuse = Color.FromArgb(255,255,255);

			// Find the total width and height of the texture bitmap (for converting sizes to fractions)
			using (Bitmap bmp = (Bitmap)Bitmap.FromFile(FileResource.Fsp("textures", "scenery.png")))
			{
				texWidth = bmp.Width;
				texHeight = bmp.Height;
			}

			// Temp - create 100 of each type
			for (int t = 0; t<NUMTYPES; t++)
			{
				Create(t);
			}

		}


		/// <summary>
		/// Device has been created - load the textures.
		/// Call this from Scene.OnDeviceCreated();
		/// (Our textures are marked Managed, so we don't need to recreate them on a Reset)
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Scenery.OnReset()");

			// Load/reload the texture
			texture = TextureLoader.FromFile(	Engine.Device, 
												FileResource.Fsp("textures","scenery.png"),
												0,0,							// width/height
												1,								// mip levels
												Usage.None,						// usage
												Format.A8R8G8B8,				// argb
												Pool.Managed,					// pool
												Filter.None,					// filter
												Filter.None,					// mip filter
												unchecked((int)0xFF00FF00));	// chroma color (lime green)
		}


		/// <summary>
		/// Create some new instances, placing them on the ground in random locations 
		/// as filtered by the placement criteria stored in their SceneryInfo
		/// (Note: the new Props don't have their frameRate or phase values set, just the location)
		/// </summary>
        /// <param name="type">type of scenery to create</param>
		public static void Create(int type)
		{
			float x,y;
			Vector3 slope;
			float alt;
			int tileType;
			bool validTexture = false;

			SceneryInfo inf = typeList[type];

			for (int n=0; n<inf.numberToCreate; n++)
			{
				do
				{
					// Pick a location
					x = Rnd.Float(Map.MapWidth-1);
					y = Rnd.Float(Map.MapHeight-1);
				
					// Establish facts about it
					alt = Terrain.AltitudeAt(x,y);
					slope = Terrain.SlopeAt(x,y);
					tileType = Terrain.TextureAt(x,y);

					// check validity of tile type
					if (inf.validTileTypes == null)
					{
						validTexture = true;
					}
					else
					{
						for (int i=0; i<inf.validTileTypes.Length; i++)
						{
							if (tileType == inf.validTileTypes[i])
							{
								validTexture = true;
								break;
							}
						}
					}
				}
					// repeat until all criteria are satisfied
				while ((alt <= inf.minAlt) || (alt >= inf.maxAlt)
					|| (slope.Y <= inf.minSlope) || (slope.Y >= inf.maxSlope)
					|| (validTexture == false));

				// Add a Prop at the successful location, touching the ground
				sceneryList.Add(new Scenery(typeList[type], new Vector3(x,alt,y)));

				/// HACK: REMOVE THIS!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
			/////	Marker.Create(Color.FromArgb(32,System.Drawing.Color.Red),1,new Vector3(x,alt,y));
			}
		}

		/// <summary>
		/// Call this before rendering the map, to prepare.
		/// </summary>
		public static new void PreRender()
		{

			// --------------------- Set Renderstate ------------------
			Fx.SetWorldMatrix(Matrix.Identity);							// Matrix is fixed at 0,0,0
			Engine.Device.VertexFormat = CustomVertex.PositionNormalTextured.Format;	// Textured objects
			Fx.SetMaterial(material);
			Fx.SetTexture(texture);							// Use this texture for all primitives
			Fx.SetSceneryTechnique();												// technique




			//*** Transparency settings ***
			Engine.Device.RenderState.AlphaBlendEnable = true;							// enable/disable transparency)
			// Vector alpha...
			//Engine.Device.RenderState.DiffuseMaterialSource = ColorSource.Color1;
			// Material alpha...
			//			Engine.Device.RenderState.DiffuseMaterialSource = ColorSource.Material;	
			Engine.Device.RenderState.SourceBlend = Blend.SourceAlpha;					// Source blend
			Engine.Device.RenderState.DestinationBlend = Blend.InvSourceAlpha;			// Dest blend
			// Texture alpha...
			//Engine.Device.TextureState[0].ColorOperation = TextureOperation.Modulate;	// Use the following for texture transp
			//Engine.Device.TextureState[0].ColorArgument1 = TextureArgument.TextureColor;
			//Engine.Device.TextureState[0].ColorArgument2 = TextureArgument.Diffuse;
			//Engine.Device.TextureState[0].AlphaOperation = TextureOperation.Modulate;
			//Engine.Device.TextureState[0].AlphaArgument1 = TextureArgument.TextureColor;
			//Engine.Device.TextureState[0].AlphaArgument2 = TextureArgument.Diffuse;
			// --------------------------------------------------------

		}

		/// <summary>
		/// Render the batch of sprites to the display
		/// </summary>
		public static new void Render(bool clear)
		{
			if (batch.Count == 0)
				return;

			// Create a matrix that will rotate all the sprites in view roughly towards the camera
			facingMatrix = Matrix.RotationY(Camera.bearing);

			try
			{
				// Create the vertex buffer
				VertexBuffer VBBatch = new VertexBuffer(typeof(CustomVertex.PositionNormalTextured),
					batch.Count * 6,								// 6 verts per tile
					Engine.Device,
					Usage.WriteOnly,
					CustomVertex.PositionNormalTextured.Format,
					Pool.Default);

				// Send the triangles to it
				using (GraphicsStream stream = VBBatch.Lock(0, 0, LockFlags.None))
				{
					// For each object
					batch.Reset();
					Scenery obj;
					while ((obj = (Scenery)batch.GetNext()) != null)
						obj.Write(stream);
					VBBatch.Unlock();
				}
				Engine.Device.SetStreamSource(0, VBBatch, 0);							// make this the stream source
				Fx.DrawPrimitives(PrimitiveType.TriangleList, 0, batch.Count * 2);	// render the primitives via an effect
				VBBatch.Dispose();
				if (clear == true)
					batch.Clear();														// start filling from beginning next time
			}
			catch (Exception e)
			{
				Debug.WriteLine("unable to render terrain batch");
				throw e;
			}
		}





		//----------------- instance methods ------------------

		/// <summary>
		/// Instance constr
		/// </summary>
		private Scenery(SceneryInfo info, Vector3 loc)
		{
			this.info = info;
			location = loc;

			// Set up the sphere. The diameter of this is the size of the sprite converted to world coords
			float scale = ((float)info.width / (float)info.height) * info.scale;	// scaled width in world coords
			if (info.scale > scale)
				scale = info.scale;
			AbsSphere.Centre = loc;
			AbsSphere.Radius = scale;

			Map.Add(this);

			// set a random start frame
			frame = Rnd.Float(info.numFrames);

		}

		/// <summary>
		/// Dispose of resources
		/// </summary>
		public override void Dispose()
		{
		}

		/// <summary>
		/// Render this object. Like tiles, Props are just batched by their Render method.
		/// They actually get rendered by Scenery.PostRender()
		/// </summary>
		public override void AddToRenderBatch()
		{
			batch.Add(this);
		}

		/// <summary>
		/// The given cell is in collision with our bounding sphere. Test to see if it actually collides with
		/// one of my parts. If so, return a Vector describing the force we exert on the offending cell.
		/// The default is to return nothing, but Scenery classes that are .Collidable will need to implement this.
		/// </summary>
		/// <param name="cell">The cell that may have collided with us</param>
		/// <returns> Any force vector acting on the cell </returns>
		public override Vector3 CollisionTest(Cell otherCell)
		{
			/// TODO: behaviour should depend on type
			return Vector3.Empty;
		}

		/// <summary>
		/// Write myself to a vertex buffer as a pair of triangles
		/// </summary>
		/// <param name="stream"></param>
		private void Write(GraphicsStream stream)
		{

			// Get a local copy of the untransformed quad
			CustomVertex.PositionNormalTextured[] quad = new CustomVertex.PositionNormalTextured[4];
			for (int v=0; v<4; v++)
			{
				quad[v] = info.quad[v];
			}

			// calculate frame number
			if (info.frameRate!=0)							// if no frame rate, frame represents an unanimated variant
			{
				frame += info.frameRate * Scene.ElapsedTime;
				frame %= info.numFrames;
			}
			float wholeFrame = (int)frame;
								
			// define the part of the texture to display this frame, in texture coordinates
			float left = wholeFrame * info.width / texWidth;		// fraction of the texture at which this frame starts
			float right = (wholeFrame+1.0f) * info.width / texWidth;
			float top = info.top / texHeight;						// fraction of the texture at which this row starts
			float bot = (info.top + info.height) / texHeight;

			const float border = 0.001f;					// close in a little, to prevent overrunning sprite edges
			left += border;
			right -= border;
			top += border;
			bot -= border;

			quad[0].Tu = left;
			quad[0].Tv = top;
			quad[1].Tu = right;
			quad[1].Tv = top;
			quad[2].Tu = left;
			quad[2].Tv = bot;
			quad[3].Tu = right;
			quad[3].Tv = bot;

			// Get a matrix that will rotate to face camera (around yaw axis only) and then translate into world coordinates
			Matrix trans = facingMatrix * Matrix.Translation(location);

			// We have to transform the vertices ourselves, since there are many sprites in each vertex buffer 
			for (int v=0; v<4; v++)
			{
				// Transform the vertex
				quad[v].Position = Vector3.TransformCoordinate(quad[v].Position,trans);
				// Transform the normal
				quad[v].Normal = Vector3.TransformNormal(quad[v].Normal,trans);
			}
	
			// Add this quad's two triangles to the vertex buffer
			stream.Write(quad[0]);
			stream.Write(quad[1]);
			stream.Write(quad[2]);
			stream.Write(quad[3]);
			stream.Write(quad[2]);
			stream.Write(quad[1]);

		}


	}

}
