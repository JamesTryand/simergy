using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;


namespace Simbiosis
{
	/// <summary>
	/// The "map": A quadtree of 3D objects, with facilities for visibiity checks, etc.
	/// Static members represent the one-and-only tree, instance members are nodes (sub-maps).
	/// Note: the root node contains all 3D objects in the system, so can be used as the primary database
	/// of active objects, i.e. it is THE map.
	/// </summary>
	/// <remarks>
	/// WARNING: The number of maps in the tree = 4^levels + 4^(levels-1) ... 4^0
	/// which is a LOT of maps. 6 levels = 1365 maps; 7 levels = 5461; 8 levels = 21,845
	/// On the other hand, if the number of levels is too low for the size of the map, then a large number
	/// of objects will be in bottom-level maps that straddle the camera, causing a lot of extra culling
	/// </remarks>
	public class Map
	{

		/// <summary> Size of the whole terrain map, in metres </summary>
		public const int MapWidth = 1024;
		public const int MapHeight = 1024;
		/// <summary> number of levels in tree </summary>
		public const int NumLevels = 5;

		/// <summary> Root node of tree </summary>
		private static Map root = null;
		/// <summary> for tracking number of quads in database - rises VERY quickly with NumLevels! </summary>
		private static int numQuads = 0;
		/// <summary> for tracking number of renderable objects </summary>
		private static int tilesInView = 0;
		private static int orgsInView = 0;
		private static int spritesInView = 0;
		private static int wavesInView = 0;

		/// <summary> Shadow map texture and render surface  </summary>
		private static Texture shadowMap = null;
		private static Surface shadowMapSfc = null;





		/// <summary> My children (null if bottom level) </summary>
		private Map[] child = new Map[4];
		/// <summary> my parent </summary>
		private Map parent = null;
		/// <summary> level in tree (0=root) </summary>
		private int level = 0;
		public int Level { get { return level; } }
		/// <summary> boundary in world coords </summary>
		private RectangleF bounds;
		/// <summary> centre of region </summary>
		private Vector3 centre;
		/// <summary> approx radius of quad (for quick hit-testing) </summary>
		private float radius;
		/// <summary> list of all Organisms within quad</summary>
		private List<Organism> organismList = new List<Organism>();
        public List<Organism> OrganismList { get { return organismList; } }
		/// <summary> list of all tiles within quad</summary>
		private List<Terrain> terrainList = new List<Terrain>();
		public List<Terrain> TerrainList { get { return terrainList; } }
		/// <summary> list of all water tiles within quad</summary>
		private List<Water> waterList = new List<Water>();
		public List<Water> WaterList { get { return waterList; } }
		/// <summary> list of all scenery within quad</summary>
		private List<Scenery> sceneryList = new List<Scenery>();
		public List<Scenery> SceneryList { get { return sceneryList; } }




		/// <summary>
		/// Static constr - build tree
		/// </summary>
		static Map()
		{
			// Recursively create the entire tree
			root = new Map(null,new Rectangle(0,0,MapWidth,MapHeight),0);
			Debug.WriteLine(String.Format("{0} quads in quadtree",numQuads));
		}

		/// <summary>
		/// Instance constr - called recursively by static constr
		/// </summary>
		/// <param name="parent">map above me</param>
		/// <param name="bounds">rectangle of world that I represent</param>
		/// <param name="level">current level into tree</param>
		public Map(Map parent, RectangleF bounds, int level)
		{
			// record level & parent
			this.level = level;
			this.parent = parent;
			numQuads++;																// track number of quads during development

			// set up my bounding box and bounding circle
			this.bounds = bounds;
			centre = new Vector3(bounds.X + (bounds.Width / 2.0f),					// use Vector3 for easy comparison with
								 0,													// object x,y,z, but y is always 0
								 bounds.Y + (bounds.Height / 2.0f));
			double diagx = centre.X - bounds.X;										// xy offset from centre to a corner
			double diagy = centre.Z - bounds.Y;
			radius = (float)Math.Sqrt((diagx*diagx)+(diagy*diagy));					// distance from centre to corners

			// recursively create my children, if any
			if (++level < NumLevels)
			{
				float w = bounds.Width / 2.0f;										// size of sub-quads
				float h = bounds.Height / 2.0f;
				child[0] = new Map(this,											// NE
					new RectangleF(centre.X,centre.Z,w,h),
					level);
				child[1] = new Map(this,											// SE
					new RectangleF(centre.X,bounds.Y,w,h),
					level);
				child[2] = new Map(this,											// SW
					new RectangleF(bounds.X,bounds.Y,w,h),
					level);
				child[3] = new Map(this,											// NW
					new RectangleF(bounds.X,centre.Z,w,h),
					level);
			}

		}

		/// <summary>
		/// Set up resources
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Map.OnDeviceCreated()");

		}

		public static void OnDeviceLost()
		{
			Debug.WriteLine("Map.OnDeviceLost()");
			if (shadowMapSfc != null)
			{
				shadowMapSfc.Dispose();
				shadowMapSfc = null;
			}
			if (shadowMap != null)
			{
				shadowMap.Dispose();
				shadowMap = null;
			}
		}

		/// <summary>
		/// Device has been reset
		/// </summary>
		public static void OnReset()
		{
			Debug.WriteLine("Map.OnReset()");

			// (re)create the texture that will hold the shadow map
			int hresult;
			Format buffmt = Engine.Device.PresentationParameters.BackBufferFormat;
			Manager.CheckDeviceFormat(0, DeviceType.Hardware, buffmt, Usage.RenderTarget, ResourceType.Textures, Format.R32F, out hresult);
			if (hresult != (int)ResultCode.NotAvailable)
			{
				shadowMap = new Texture(Engine.Device, 512, 512, 1, Usage.RenderTarget, Format.R32F, Pool.Default);

			}

			//// (re)create the texture that will hold the shadow map
			//if (Manager.CheckDeviceFormat(0, DeviceType.Hardware, Format.X8B8G8R8, Usage.RenderTarget, ResourceType.Textures, Format.R32F))
			//{
			//    shadowMap = new Texture(Engine.Device, 512, 512, 1, Usage.RenderTarget, Format.R32F, Pool.Default);

			//}
			else // Switch off shadowing if the hardware doesn't support this format
			{
				Fx.IsShadowed = false;
				Debug.WriteLine("WARNING: Floating point textures are not supported by h/w - switching off shadows");
			}
		}


		/// <summary>
		/// Add an object to the quadtree
		/// </summary>
		/// <remarks>Object gets added to every node in the tree that bounds it</remarks>
		public static void Add(Renderable obj)
		{
			root.RecursiveAdd(obj);	
			//Engine.EventHUD.WriteLine(String.Format("Obj {0} added to quadtree",root.objects.Count));
		}

		/// <summary>
		/// Remove an object completely from the quadtree
		/// </summary>
		/// <remarks>
		/// Object should be Disposed of after this call, if no longer needed
		/// (while a reference to it still exists)
		/// </remarks>
		public static void Remove(Renderable obj)
		{
			root.RecursiveDel(obj);													// remove from all relevant nodes
			//Engine.EventHUD.WriteLine("Obj removed from quadtree: "+obj);
		}


		/// <summary>
		/// Recursively add an object to a node and its children (if it lies within bounds)
		/// </summary>
		/// <param name="obj"></param>
		private void RecursiveAdd(Renderable obj)
		{
			if (obj.IsIn(bounds))													// if the obj is within my boundary
			{
				if (obj is Organism)
				{
					organismList.Add((Organism)obj);								// add the object to my Organism list
					obj.AddMap(this);												// and add me to the object's own list
				}
				else if (obj is Terrain)
				{
					terrainList.Add((Terrain)obj);									// add the object to my Terrain list
				}
				else if (obj is Water)
				{
					waterList.Add((Water)obj);										// add the object to my Water list
				}
				else
				{
					sceneryList.Add((Scenery)obj);									// add the object to my Scenery list
				}

				if (level < NumLevels-1)
				{
					child[0].RecursiveAdd(obj);										// and recursively try my children
					child[1].RecursiveAdd(obj);										// (if it isn't within my boundary
					child[2].RecursiveAdd(obj);										// none of my children will contain
					child[3].RecursiveAdd(obj);										// it either)
				}
			}
		}


		/// <summary>
		/// Recursively remove the object from this node and, if it was present, all relevant subnodes
		/// </summary>
		/// <param name="obj">reference to the object to remove</param>
		private void RecursiveDel(Renderable obj)
		{
			int found=-1;

			if (obj is Organism)
				found = organismList.IndexOf((Organism)obj);						// search correct array for object
			else if (obj is Terrain)
				found = terrainList.IndexOf((Terrain)obj);
			else if (obj is Water)
				found = waterList.IndexOf((Water)obj);
			else
				found = sceneryList.IndexOf((Scenery)obj);

			if (found>=0)															// if present at this node
			{
				if (level < NumLevels-1)
				{
					child[0].RecursiveDel(obj);										// remove it from any child nodes
					child[1].RecursiveDel(obj);
					child[2].RecursiveDel(obj);
					child[3].RecursiveDel(obj);
				}
				if (obj is Organism)
				{
					organismList.RemoveAt(found);									// delete the object from my list
					obj.DelMap(this);												// and remove me to the object's own list
				}
				else if (obj is Terrain)
				{
					terrainList.RemoveAt(found);									// delete the object from my list
				}
				else if (obj is Water)
				{
					waterList.RemoveAt(found);										// delete the object from my list
				}
				else
				{
					sceneryList.RemoveAt(found);									// delete the object from my list
				}

			}
		}

		/// <summary>
		/// Overrides Object.ToString() to return a helpful identifier for debugging etc.
		/// </summary>
		public override string ToString()
		{
			return string.Format("[Quad L{0}:Rect{1}]",level,bounds);
		}

		/// <summary>
		/// Get this quad's boundary rectangle
		/// Used by Organism to test for boundary transgressions when object moves
		/// </summary>
		public RectangleF Bounds
		{ get { return bounds; } }

		/// <summary> Get the total number of objects in the database </summary>
		public static int NumObjects
		{
			get { return root.organismList.Count + root.terrainList.Count + root.waterList.Count + root.sceneryList.Count; }
		}


		/// <summary>
		///Establish which objects are currently visible, and add them to their class's render batch
		/// prior to rendering shadows and scene.
		/// </summary>
		/// <remarks>
		/// Since objects might straddle several quads, I have to flag those that need
		/// rendering then render them afterwards, to prevent objects being rendered several times
		/// </remarks>
		public static void CullScene()
		{
			// no need to check the root node, since that is the whole map & HAS to be straddling the camera
			root.child[0].RenderQuad();
			root.child[1].RenderQuad();
			root.child[2].RenderQuad();
			root.child[3].RenderQuad();

			// Objects that need rendering now have their RenderMe flag set, so update them and add them to their respective
			// class's render batch.
			// Update() is called for ALL objects on map that require updating, whether visible or not.
			// Handle each type of Renerable object separately, so that renderstate changes need only be done once.

			// --- Terrain ---
			tilesInView = 0;															// track # rendered for console
			Terrain.PreRender();														// set renderstate
			for (int o = 0; o < root.terrainList.Count; o++)
			{
				Terrain obj = (Terrain)root.terrainList[o];
				//////obj.Update();														// Update ALL
				if (obj.RenderMe)														// if it's visible...
				{
					obj.AddToRenderBatch();												// add the object to the render batch
					tilesInView++;														// track # actually rendered
					obj.RenderMe = false;												// reset the flag for next time
				}
			}

			// --- Water ---
			wavesInView = 0;															// track # rendered for console
			Water.PreRender();															// set renderstate & do caustics
			for (int o = 0; o < root.waterList.Count; o++)
			{
				Water obj = (Water)root.waterList[o];
				//////obj.Update();														// Update ALL
				if (obj.RenderMe)														// if it's visible...
				{
					obj.AddToRenderBatch();												// add the object to the render batch
					wavesInView++;														// track # actually rendered
					obj.RenderMe = false;												// reset the flag for next time
				}
			}

			// --- Organisms ---
			orgsInView = 0;																// track # rendered for console
			Organism.PreRender();														// set renderstate
			for (int o=0; o<root.organismList.Count; o++)
			{
				Organism obj = (Organism)root.organismList[o];
				obj.Update();															// Update ALL organisms
				if (obj.RenderMe)														// if it's visible...
				{
					obj.AddToRenderBatch();												// add the object to the render batch
					orgsInView++;														// track # actually rendered
					obj.RenderMe = false;												// reset the flag for next time
				}
			}

			// --- Scenery ---
			spritesInView = 0;															// track # rendered for console
			Scenery.PreRender();														// set renderstate
			for (int o=0; o<root.sceneryList.Count; o++)
			{
				Scenery obj = (Scenery)root.sceneryList[o];
				if (obj.RenderMe)														// if it's visible...
				{
					obj.AddToRenderBatch();												// add the object to the render batch
					spritesInView++;													// track # actually rendered
					obj.RenderMe = false;												// reset the flag for next time
				}
			}

			/// TODO: Add any other Renderable subclasses here (include call to Update() if required)

		}

		private void RenderQuad()
		{
			// check how visible this quad is (check against SIDES of frustrum only)
			int vis = Camera.CanSee(centre,radius,4);							// 0=outside, 1=inside, -1=straddling

			// if invisible, then ignore this quad and its children
			if (vis==0)
				return;

			// if completely inside frustrum, all my objects MIGHT need rendering.
			// However I need to check them all individually, since a quad is infinitely high and
			// many of the objects may be above or below the camera. On the other hand, I only need check the
			// top and bottom faces of the frustrum.
			if (vis==1)
			{
				for (int o=0; o<organismList.Count; o++)
				{
					Renderable obj = (Renderable)organismList[o];
					// HACK: Organisms should still be visible if above camera, so that their shadows will display. Cost is minimal.
					////if (Camera.CanSee(obj)!=0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre,Camera.Position).LengthSq();
					}
				}
				for (int o = 0; o < terrainList.Count; o++)
				{
					Renderable obj = (Renderable)terrainList[o];
					if (Camera.CanSee(obj) != 0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre, Camera.Position).LengthSq();
					}
				}
				for (int o = 0; o < waterList.Count; o++)
				{
					Renderable obj = (Renderable)waterList[o];
					if (Camera.CanSee(obj) != 0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre, Camera.Position).LengthSq();
					}
				}
				for (int o = 0; o < sceneryList.Count; o++)
				{
					Renderable obj = (Renderable)sceneryList[o];
					if (Camera.CanSee(obj)!=0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre,Camera.Position).LengthSq();
					}
				}					
				return;
			}

			// otherwise, if quad straddles the frustrum, only some of my objects need rendering,
			// so check my children recursively
			if (level < NumLevels-1)
			{
				child[0].RenderQuad();
				child[1].RenderQuad();
				child[2].RenderQuad();
				child[3].RenderQuad();
			}
			// unless if I'm already at the lowest level, when this quad is never going to fit completely inside 
			// the frustrum, so check the objects individually using their bounding spheres
			else
			{
				for (int o=0; o<organismList.Count; o++)
				{
					Renderable obj = (Renderable)organismList[o];
					if (Camera.CanSee(obj)!=0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre,Camera.Position).LengthSq();
					}
				}
				for (int o = 0; o < terrainList.Count; o++)
				{
					Renderable obj = (Renderable)terrainList[o];
					if (Camera.CanSee(obj) != 0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre, Camera.Position).LengthSq();
					}
				}
				for (int o = 0; o < waterList.Count; o++)
				{
					Renderable obj = (Renderable)waterList[o];
					if (Camera.CanSee(obj) != 0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre, Camera.Position).LengthSq();
					}
				}
				for (int o = 0; o < sceneryList.Count; o++)
				{
					Renderable obj = (Renderable)sceneryList[o];
					if (Camera.CanSee(obj)!=0)
					{
						obj.RenderMe = true;													// this obj will be rendered next frame
						// compute the SQUARED dist from camera to obj, for LOD calculation and render ordering
						obj.DistSq = Vector3.Subtract(obj.AbsSphere.Centre,Camera.Position).LengthSq();
					}
				}					
			}
		}

		/// <summary>
		/// Render all map-based objects that have been added to their respective render batches
		/// </summary>
		public static void Render()
		{
            Terrain.PreRender();
            Terrain.Render(true);

			Organism.PreRender();
			Organism.Render(true);

            Scenery.PreRender();
            Scenery.Render(true);

			Water.PreRender();
			Water.Render(true);	
		}


		/// <summary>
		/// Render only the creatures and terrain onto the shadow map texture.
		/// Leaves the RenderMe flag set, ready for the main scene render.
		/// Call this AFTER the RenderMe flags have been set using RenderQuad()
		/// </summary>
		public static void RenderShadow()
		{
			Surface oldRenderTarget = null;
			Viewport oldView = new Viewport();

			Viewport view = new Viewport();
			view.Width = 512;
			view.Height = 512;
			view.MaxZ = 1.0f;
			view.MinZ = 0;
			view.X = 0;
			view.Y = 0;

			try
			{
				// Set up the render target and renderstate
				// If the device is capable of shadowing and shadows are enabled, set up the shadow map texture etc.
				/// TODO: check caps here!!!
				//Save the current render target so that we can reset it after use.
				oldRenderTarget = Engine.Device.GetRenderTarget(0);
				oldView = Engine.Device.Viewport;
				//Set the render target to the surface we created
				using (shadowMapSfc = shadowMap.GetSurfaceLevel(0))
				{
					Engine.Device.SetRenderTarget(0, shadowMapSfc);
					//Engine.Device.CreateDepthStencilSurface(512, 512, DepthFormat.D32, MultiSampleType.None, 0, false);
					// BUG: Shadows stop working if the viewport goes smaller than 512 in either direction.
					// This probably means I need to ensure the zbuffer is always at least as big as the
					// shadowmap surface. But rather than waste time creating a new zbuffer here, I'll just
					// force the window to have a minimum size of 512x512! Change this if it's a problem.
				}
				Engine.Device.Viewport = view;
				//Clear the render target (our surface and our depth buffer of the surface) 
				Engine.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.White, 1.0f, 0);

				Engine.Device.BeginScene();

				// Set the technique and renderstate
				//Engine.Device.RenderState.ZBufferEnable = true;								// enabled
				Engine.Device.VertexDeclaration = BinormalVertex.Declaration;				// bump-mapped objects
				//Engine.Device.RenderState.AlphaBlendEnable = false;							// enable/disable transparency)
				//Engine.Device.RenderState.ZBufferFunction = Compare.Less;
				Fx.SetShadowTechnique();

				// Render the shadowing objects
				//////Terrain.Render(false);			// only worth doing if the sun can go low in the sky!
				Organism.Render(false);

				Engine.Device.EndScene();

				// Return to the normal render target
				Engine.Device.SetRenderTarget(0, oldRenderTarget);
				oldRenderTarget.Dispose();													// MUST dispose of this resource!
				Engine.Device.Viewport = oldView;
				//Engine.Device.RenderState.ZBufferFunction = Compare.Greater;

				// Store the latest texture into the shader
				Fx.SetShadowTexture(shadowMap);

			}
			catch (Exception e)
			{
				Debug.WriteLine("Map.RenderShadow() failed, with "+e.Message);
			}
		}






		// Map state information for HUD displays
		public static string HUDInfo
		{ 
			get 
			{
				return String.Format("{0} total, {1} terr, {2} water, {3} orgs, {4} scen",
									  NumObjects, tilesInView, wavesInView, orgsInView, spritesInView); 
			}
		}
	
		/// <summary>
		/// Return a list of all the IDetectable objects within a given radius of a point
		/// (for sensors). IDetectable objects include Organisms and tiles but not scenery.
		/// Only return objects of the specified class(es). Optionally perform a line-of-sight
		/// test on the candidates too.
		/// </summary>
		/// <param name="loc">location of sensor</param>
		/// <param name="radius">range of sensor</param>
		/// <param name="includeOrganisms">Set true to include organisms in the list</param>
		/// <param name="includeTerrain">Set true to include tiles in the list</param>
		/// <param name="lineOfSightOnly">Set true to exclude objects obscured by terrain.</param>
		/// <param name="ignoreMe">The Organism object issuing the call (or null) - obviously we don't want to detect ourselves!</param>
		/// <returns>A list of IDetectable objects</returns>
        public static List<IDetectable> GetObjectsWithinRange(Vector3 loc, float radius, 
			bool includeOrganisms, bool includeTerrain,
			bool lineOfSightOnly, Organism ignoreMe)
		{
			// Create a list to store the winners in
            List<IDetectable> result = new List<IDetectable>();

			// Run down the cell hierarchy, following only the thread that contains the given location,
			// Until we reach a depth at which the given range exceeds the boundary of the quad.
			// Back up one level. We now have the smallest quad that completely encloses the circle under consideration...

			// First, clip the circle to the edges of the map, otherwise no quad will be fully within circle
			float minx = loc.X-radius;
			if (minx<0) minx=0;
			float maxx = loc.X+radius;
			if (maxx>MapWidth) maxx=MapWidth;
			float minz = loc.Z-radius;
			if (minz<0) minz=0;
			float maxz = loc.Z+radius;
			if (maxz>MapHeight) maxz=MapHeight;
			// then walk the hierarchy
			Map map = root.RecursiveFindSmallestContainer(minx, maxx, minz, maxz);

			// normally we can just search the one quad
			if (map.level!=0)						
			{
				GetObjectsFromQuad(map, result, loc, radius, includeOrganisms, includeTerrain, lineOfSightOnly, ignoreMe);
			}
			// BUT, for a sensor close to the mid-point of the map, the quad to search will be the WHOLE map!!!
			// This is sufficiently wasteful that it's worth treating as a special case (reduces workload by factor of 4)...
			else
			{
				if ((map.child[0].child[2].bounds.Right >= maxx)
					&&(map.child[1].child[3].bounds.Y <= minz)
					&&(map.child[2].child[0].bounds.X <= minx)
					&&(map.child[3].child[1].bounds.Bottom >= maxz))
				{
					// only scan the grandchildren that surround the mid-point
					GetObjectsFromQuad(map.child[0].child[2], result, loc, radius, includeOrganisms, includeTerrain, lineOfSightOnly, ignoreMe);
					GetObjectsFromQuad(map.child[1].child[3], result, loc, radius, includeOrganisms, includeTerrain, lineOfSightOnly, ignoreMe);
					GetObjectsFromQuad(map.child[2].child[0], result, loc, radius, includeOrganisms, includeTerrain, lineOfSightOnly, ignoreMe);
					GetObjectsFromQuad(map.child[3].child[1], result, loc, radius, includeOrganisms, includeTerrain, lineOfSightOnly, ignoreMe);
				}
				else
				{
					// here if there was some other reason for having to scan the whole map - e.g. very large range effectors
					GetObjectsFromQuad(map.child[0].child[2], result, loc, radius, includeOrganisms, includeTerrain, lineOfSightOnly, ignoreMe);
				}
			}
			return result;
		}
		// Helper for GetObjectsWithinRange()
		// Run down the cell hierarchy, following only the branch that contains the given location,
		// Until we reach a depth at which the given range exceeds the boundary of the quad.
		// Back up one level. We now have the smallest quad that completely encloses the circle under consideration
		private Map RecursiveFindSmallestContainer(float minx, float maxx, float minz, float maxz)
		{
			if (child[0] == null)											// if we're already as deep as we can go,
				return this;												// we are it

			for (int i = 0; i < 4; i++)										// for each child of this level
			{
				if ((child[i].bounds.Left <= minx)							// if it completely encloses the circle
					&&(child[i].bounds.Right >= maxx)
					&&(child[i].bounds.Top <= minz)
					&&(child[i].bounds.Bottom >= maxz))	
				{
					return child[i].RecursiveFindSmallestContainer(minx, maxx, minz, maxz);	// look deeper down this branch
				}
			}
			// If we've looked in all four children and none of them enclose the whole circle,
			// WE must be the smallest map containing it all, so bubble our name to the top of the call stack
			return this;
		}
		// Helper for GetObjectsWithinRange()
		// Run through each object in the given quad, keeping only those that are within the radius
		// and are of the right type(s). Optionally also filter those not in direct line of sight.
        private static void GetObjectsFromQuad(Map map, List<IDetectable> result, Vector3 loc, float radius, 
														bool includeOrganisms, bool includeTerrain,	
														bool lineOfSightOnly, Organism ignoreMe)
		{
			// Organisms...
			if (includeOrganisms==true)
			{
				foreach (Renderable obj in map.OrganismList)
				{
					if (obj != ignoreMe)														// don't include the owner of the sensor!
					{
						float radii = obj.AbsSphere.Radius + radius;
						if (Vector3.LengthSq(loc - obj.AbsSphere.Centre) <= (radii * radii))	// include it if spheres intersect
						{
							if (lineOfSightOnly == true)										// and optionally if unobstructed by terrain etc.
							{
								if ((Terrain.InLineOfSight(loc, obj.AbsSphere.Centre))
                                    && (!result.Contains((IDetectable)obj)))                    // Object may be in several quads, so only add if unique
                                        result.Add((IDetectable)obj);
							}
							else
							{
                                if (!result.Contains((IDetectable)obj))                         // Object may be in several quads, so only add if unique
								    result.Add((IDetectable)obj);
							}
						}
					}
				}
			}

			// Tiles...
			if (includeTerrain==true)
			{
				foreach (Renderable obj in map.TerrainList)
				{
					float radii = obj.AbsSphere.Radius + radius;
					if (Vector3.LengthSq(loc - obj.AbsSphere.Centre) <= (radii * radii))	// include it if obj sphere intersects tile SPHERE
					{
						if (lineOfSightOnly==true)
						{
							if (Terrain.InLineOfSight(loc, obj.AbsSphere.Centre))
								result.Add((IDetectable)obj);
						}
						else
						{
                            if (!result.Contains((IDetectable)obj))                         // Object may be in several quads, so only add if unique
                                result.Add((IDetectable)obj);
						}
					}
				}
			}


		}

		/// <summary>
		/// Return true if the given point is within this map
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		private bool Contains(float x, float y)
		{
			return bounds.Contains(x,y);
		}

		/// <summary>
		/// Helper for mouse selection of 3D objects. Given a "finger" pointing into the screen,
		/// return the Cell that the finger points to (or null if no such cell).
		/// Called by Camera.MousePick()
		/// </summary>
		/// <param name="rayPosition">position of ray on screen</param>
		/// <param name="rayDirection">vector pointing into screen</param>
		/// <param name="socket">If a SOCKET on the cell was selected, its frame is returned here</param>
		/// <returns>the Cell that the finger points to (or null)</returns>
		public static Cell MousePick(Vector3 rayPosition, Vector3 rayDirection, out JointFrame socket)
		{
			float bestDist = float.MaxValue;
			Cell bestCell = null;
			socket = null;

			// For every organism on the map
			foreach (Organism org in root.organismList)
			{
				// Skip if this is the camera ship - we're inside that so we'd be certain to click on it!
				if (org == (Organism)CameraShip.CurrentShip)
					continue;

				// if finger points somewhere within bounds of this organism
				if (Geometry.SphereBoundProbe(org.AbsSphere.Centre, org.AbsSphere.Radius, rayPosition, rayDirection))
				{
					// Ask the organism to check its cells
					Cell cell = org.MousePick(rayPosition, rayDirection, out socket);
					// if the ray intersects this cell and it is closer than any previous cell, store it
					if (cell != null)
					{
						float dist = Vector3.Length(cell.AbsSphere.Centre - Camera.Position);
						if (dist < bestDist)
							bestCell = cell;
					}
				}
			}
			// return best candidate cell or null if none found
			return bestCell;
		}



	}

}
