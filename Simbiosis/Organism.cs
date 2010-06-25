//#define SHOWBOUNDS		// define this to show bounding spheres as markers


using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Xml;

namespace Simbiosis 
{
	/// <summary>
	/// A complete creature, consisting of one or more Cells.
	/// </summary>
	/// <remarks>
	/// TODO: ******** THIS IS OUT OF DATE *************
	/// GENERAL
	/// An organism is made from one or more Cells. 
	/// - It has an absolute location in space & is derived from Renderable. 
	/// - Any animations are carried out by the Cells themselves, which handle the frame hierarchy for their 
	///   own component bones and meshes.
	/// - Rendering the Organism involves rendering the top-level Cell, then copying the correct joint frames'
	///   combined transformation matrices into the root bones of the child Cells. The child Cells have
	///   their own matrices computed from these root transforms and are then rendered.
	///	- An Organism is constructed from a Genome object, which can provide the filenames of its Cells, amongst
	///	  other things
	///	- As an Organism is constructed, its Cells are read from their respective X files unless they already exist
	///	  in the Cell Library. Then they are cloned and given to the Organism, so that each Organism has its own 
	///	  modifiable version (although bulky read-only data such as source meshes and textures are just references to 
	///	  the original Cell Library object's copy).
	///	- Each Organism is a unique instance (no library), since they will tend to be genetically different
	///	  
	///	PHYSICS
	///	  
	/// COLLISION DETECTION
	/// Each Organism contains a list of the map quads it currently occupies. Only tiles and other organisms in those
	/// quads need to be examined for collisions. We only need look in the lowest level quads. However, some possible
	/// collision targets will be represented several times at this level. So we scan all our bottom-level quads and
	/// build a hashlist of unique possible targets (which may be tiles or other creatures, but not sprites or particles).
	/// 
	/// We can also hit the surface, which requires its own special case (with different physics).
	/// 
	///	  
	/// </remarks>
	public class Organism : Renderable, IDetectable
	{
		#region ---------------------------- static fields -----------------------------
		/// <summary>
		/// Loaded from instanceCount++ when organism is created, to give each instance a unique identifier
		/// </summary>
 		private int instance = 0;
		public int Instance { get { return instance; } }
		private static int instanceCount = 0;

		/// <summary> The batch of organisms to be rendered this frame (separated by texture, sorted by dist) </summary>
		public static RenderBatch batch = new RenderBatch(100);

		/// <summary>
		/// Cell and hotspot that the camera is attached to (if I'm acting as a camera ship)
		/// </summary>
		protected static Cell cameraMount = null;
		protected static int cameraHotspot = 0;


		#endregion

		#region ------------------------------ instance fields -----------------------------
		/// <summary> ABSOLUTE position in world coordinates (org's CG will appear at this location)</summary>
		protected Vector3 location = new Vector3();
		/// <summary> ABSOLUTE orientation </summary>
		protected Quaternion orientation = new Quaternion();

		/// <summary> 
		/// Reference to the cell that's closest to the centre of gravity.
		/// All locations, orientations and forces appear to act around this cell rather than the
		/// root cell (e.g. so a snake writhes around its middle, rather than its head, which is probably the root)
		/// </summary>
		protected Cell CGCell = null;

		/// <summary> 
		/// Species name for this organism (as entered in lab edit box)
		/// </summary>
		protected string name = "Organism";

		/// <summary> the list of objecttree maps that I currently occupy. Used to speed up 
		/// quadtree updates whenever I move </summary>
		protected List<Map> myMaps = new List<Map>(Map.NumLevels); // most small stationary objs will only occupy NumLevels quads

		/// <summary> The root Cell in the tree of body parts </summary>
		protected Cell rootCell = null;

		/// <summary> A convenient flat list of the body Cells, in the order in which they were defined </summary>
		protected Cell[] partList = null;
		protected int numParts = 0;

		/// <summary> The creature's unique genome </summary>
		private Genome genome = null;

		/// <summary> 
		/// For scheduling slow updates. Timer is initialised to a random number on creation, so that
		/// objects don't all try to do their slow update at the same moment
		/// </summary>
		private float SlowUpdateTimer = 0;					
		private const float SLOWUPDATERATE = 0.25f;						// # seconds between slow updates

		/// <summary> 
		/// Linear and rotational velocity/acceleration.
		/// </summary>
		protected Quaternion turnRate = Quaternion.Identity;
		protected Quaternion turnForce = Quaternion.Identity;
		protected Vector3 translationRate = new Vector3(0,0,0);
		protected Vector3 translationForce = new Vector3(0,0,0);

		/// <summary>
		/// Aggregate properties, for sensor readings.
		/// These are calculated by examining all my cells on construction
		/// </summary>
		private float totalMass = 0;								// sum of all cell masses
		public float TotalMass { get { return totalMass; } }

		/// <summary> The set of global chemicals </summary>
		public Chemistry chemistry = new Chemistry();

        /// <summary> 
        /// The creature's current size, in the range 0 to 1, where 0 is a baby and 1 is fully grown. Used to scale mass, meshes, etc. 
        /// Camera ships, etc. obviously stay at scale 1.0 (although I can rescale them as needed in their genes to make the graphics suit the terrain scale, etc.)
        /// </summary>
        public float scale = 1.0f;

		#endregion

	
		/// <summary>
		/// Construct a Creature instance from a genome
		/// </summary>
		/// <param name="genotype">The name of the xml file defining this creature, or "" if the creature is being created in the editor</param>
		/// <param name="location">world location of creature (Y should be zero)</param>
		/// <param name="orientation">world orientation of creature</param>
		public Organism(string genotype, Vector3 location, Orientation orientation)
		{
			Debug.WriteLine("Creating new Creature from genome: "+genotype);

			// Set relevant Renderable.FlagBits
			Dynamic = true;									// (Most) creatures respond to forces

			// Set basic members
			this.location = location;
			this.orientation = Quaternion.RotationYawPitchRoll(orientation.Yaw,orientation.Pitch,orientation.Roll);
			instance = instanceCount++;						// get a unique instance number
			name = genotype+instance.ToString("000");	    // give the creature a unique name (valid filename)

			// Set up other members
            if (genotype == "")
                this.genome = new Genome();                 // if no genotype supplied, create a default genome (Core cell only, for putting on the clamp)
            else
			    this.genome = new Genome(genotype);	    	// otherwise, create our unique Genome object and store its ref

			// Load all the Cells from disk or the Cell Library, using the recipe in the Creature's genome
			// and establish their connection points
			partList = new Cell[1024];						// Create an initial partlist (will scrunch later)
			rootCell = GetCells(genome.Root, null);			// read the parts from the genome to create both the tree and partlist
			Cell[] shorter = new Cell[numParts];			// scrunch the partlist to the right size
			Array.Copy(partList,shorter,numParts);
			partList = shorter;

			// Now that the whole cell tree exists, allow the cells to connect up their channels
			rootCell.ClearAllChannels();
			rootCell.WireUpAllChannels();

			// Give the cells an absolute position in space so that we can calculate CG, bounds, etc.
			rootCell.Locate(Matrix.Translation(location));					// locate root cell temporarily at 0,0,0
			RecursiveUpdateFrames(rootCell);								// update the combined frames

			// Find out which cell is to act as the centre of gravity for the system
			CalculateCG();

			// Register the creature with the map and position it properly in space
			Map.Add(this);
			MoveTo(location);

			// Calculate the initial center and radius of the Creature's bounding sphere now it is located
			ComputeBoundingSphere();
			CheckMaps();													// And check maps again, because this requires sphere!

			// Calculate any aggregate properties required for sensor requests etc.
			SetAggregateProperties();

			// Set the SlowUpdate() timer to a random value, so that organisms do their slow updates
			// at different times
			SlowUpdateTimer = Rnd.Float(SLOWUPDATERATE);
		}

		/// <summary>
		/// Recursively walk the gene tree, extract the Cell filenames, clone the Cell from the library and
		/// record their interconnections (root->joint)
		/// </summary>
		/// <param name="gene">the gene being expressed</param>
		/// <param name="parent">the Cell that is the parent of this new one</param>
		/// <returns>the Cell created by this gene</returns>
		private Cell GetCells(Gene gene, Cell parent)
		{
			// Get the X filename from the gene and either load or fetch that Cell from the library,
			Cell s = Cell.Get(gene, this);

			// Add it to the flat partlist
			partList[numParts++] = s;

			// Locate this cell's SOCKET by locating the frame with the correct name in the 
			// parent (if any)
			if (parent!=null)
				s.Attach(parent, gene.Socket);

			// if the gene has any siblings, recursively attach their structures as siblings of this one
			if (gene.Sibling!=null)
				s.Sibling = GetCells(gene.Sibling, parent);					// we share a parent

			// if the gene has a child, recursively attach this as the child of this one
			if (gene.FirstChild!=null)
				s.FirstChild = GetCells(gene.FirstChild, s);				// I am the parent

			return s;														// return the new Cell to the parent/sibling
		}


		/// <summary>
		/// Calculate any aggregate properties that are required for sensor requests etc.
		/// </summary>
		private void SetAggregateProperties()
		{
			foreach (Cell c in partList)
			{
				totalMass += c.Physiology.Mass * scale;								// total mass of cells
				// TODO: Add any other aggregations here
			}
		}


		/// <summary>
		/// IDispose interface
		/// </summary>
		public override void Dispose()
		{
			// TODO: dispose of resources
			Debug.WriteLine("Disposing of the Creature "+name);
			Map.Remove(this);
		}


		/// <summary>
		/// Update the entire cell hierarchy - do all those things that need doing regardless of whether
		/// the organism is visible
		/// </summary>
		public override void Update()
		{
			if (Scene.FreezeTime!=0)			// don't update if the organisms are currently in suspended animation
				return;

			// Record positions for calculating deltas for reaction forces
			RecordPositions();

			// Set the location of the root cell's root frame to the organism's new location/orientation.
			// Child cells will pick up their location from their parent's socket.
			SetRootPosition();

			// Walk the cell hierarchy, updating each cell's local data, including joints, animations, transformation matrices
			RecursiveUpdate(rootCell);

			// Collision detection (do before applying forces)
			CheckForCollisions();
			CheckForSurfaceCollision();

			// Rotate/translate the whole organism as a result of the reaction forces produced by
			// joint movements, water resistance, bouyancy, etc.
			ApplyForces();


			// Time for a slow update?
			SlowUpdateTimer -= Scene.ElapsedTime;
			if (SlowUpdateTimer <= 0)
			{
				SlowUpdateTimer += SLOWUPDATERATE;
				SlowUpdate();
			}
			
		}

		/// <summary>
		/// Slow update operations. This method is called approximately every SLOWUPDATERATE seconds.
		/// Use it to update things that don't need to be done every frame.
		/// </summary>
		private void SlowUpdate()
		{
			// update overall bounding sphere for culling/collisions/sensing
			ComputeBoundingSphere();												

			// Pass control to the SlowUpdate() methods of all my cells
			foreach (Cell cell in partList)
			{
				cell.SlowUpdate();
			}


			/// TODO: more slow update processes here (sensing etc.)


		}




		/// <summary>
		/// Record current position, for calculating deltas and hence reaction forces
		/// </summary>
		private void RecordPositions()
		{
			foreach (Cell cell in partList)
			{
				cell.RecordLastPosition();
			}
		}


		/// <summary>
		/// Do any pre-rendering state changes. All objects rendered between here and 
		/// the call to PostRender will be organisms
		/// </summary>
		public static new void PreRender()
		{
			// --------------------- Set Renderstate ------------------
			Engine.Device.RenderState.ZBufferEnable = true;								// enabled
			Engine.Device.VertexDeclaration = BinormalVertex.Declaration;				// bump-mapped objects
			Fx.SetMainTechnique();

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
		/// Render the current batch of orgs
		/// </summary>
		public static new void Render(bool clear)
		{
			Organism org;

			// Start from the beginning
			batch.Reset();

			// render each organism in decreasing depth order
			while ((org = (Organism)batch.GetNext())!=null)
			{
				org.RecursiveRender(org.rootCell);
			}

			// wipe the batch
			if (clear)
				batch.Clear();
		}

		/// <summary>
		/// Render this organism (after it has been updated). This function should only
		/// be called if the organism is visible. All it does is add the org to the render batch.
		/// </summary>
		public override void AddToRenderBatch()
		{
			// Add this Organism to the batch to be rendered in PostRender()
			/////////////if (this != (Organism)CameraShip.CurrentShip)                       // never render the camera ship itself
    			batch.Add(this);                                                // TODO: Change if I need to see any part of my ship (eg. manipulator)
		}

		/// <summary>
		/// Actually render this organism's cells (recursively)
		/// </summary>
		/// <param name="cell"></param>
		public void RecursiveRender(Cell cell)
			{
			// Compute combined matrices and render this cell
			cell.Render((float)Math.Sqrt(DistSq));								// absolute dist from camera to obj sets LOD

			// Now propagate the new combined matrices through to my siblings and children
			if (cell.Sibling != null)											// recurse through siblings
				RecursiveRender(cell.Sibling);	

			if (cell.FirstChild != null)										// recurse through children
				RecursiveRender(cell.FirstChild);
		}


		/// <summary>
		/// Update this organism's cells
		/// To be called for ALL objects, whether visible or not, before rendering
		/// </summary>
		/// <param name="cell">The cell to update</param>
		private void RecursiveUpdate(Cell cell)
		{
			// Update this cell
			cell.Update();

			if (cell.Sibling != null)											// recurse through siblings
				RecursiveUpdate(cell.Sibling);	

			if (cell.FirstChild != null)										// recurse through children
				RecursiveUpdate(cell.FirstChild);
		}

		/// <summary>
		/// Update all the cells' Combined transformation matrices (e.g. after relocating the cell).
		/// This ensures that the cells don't think they've been pushed with extreme force when being
		/// moved artificially!
		/// </summary>
		/// <param name="cell"></param>
		private void RecursiveUpdateFrames(Cell cell)
		{
			cell.UpdateFrames();

			// Now propagate the new combined matrices through to my siblings and children
			if (cell.Sibling != null)											// recurse through siblings
				RecursiveUpdateFrames(cell.Sibling);	

			if (cell.FirstChild != null)										// recurse through children
				RecursiveUpdateFrames(cell.FirstChild);

		}


		#region ----------- forward and rotational motion --------------

		/// <summary>
		/// Use the current overall position and orientation to set the root cell
		/// Note: The root cell is not necessarily the CofG, but we want all motion to happen
		/// relative to the CofG, not the root. Otherwise a snake, in which the root was the head, would appear
		/// to writhe around a fixed point on the head, rather than the middle of the body, which is correct.
		/// To do this, I 'subtract' the abs position of the CG cell from that of the root and transform the
		/// root by this amount, so that the location and origin (and hence forces) appear to act on the CG.
		/// </summary>
		private void SetRootPosition()
		{
            // Calculate the correction to bring the CG cell back to its original position and orientation, making it
            // the "fixed point" of the system instead of the root cell
            Matrix correction = rootCell.RootFrame.CombinedMatrix * Matrix.Invert(CGCell.RootFrame.CombinedMatrix);
            Matrix transform = correction								// correct so that animation happens around CG
				* Matrix.Scaling(new Vector3(scale, scale, scale))
                * Matrix.RotationQuaternion(orientation)				// rotate
                * Matrix.Translation(location);							// then translate it into its final world position

			rootCell.Locate(transform);										// send this to the root cell
		}

		/// <summary>
		/// Move object to new location AND UPDATE combined matrices, 
		/// so that delta xyz doesn't suddenly create massive forces.
		/// All motion is zeroed - only use for placement, not movement.
		/// </summary>
		public void MoveTo(Vector3 newxyz)
		{
			location = newxyz;
			SetRootPosition();												// tell the root cell's root frame where we are now, and
			RecursiveUpdateFrames(rootCell);								// update the combined frames to prevent unwanted forces
			RecordPositions();												// last position = this position
			turnRate = Quaternion.Identity;									// zero any motion
			translationRate = Vector3.Empty;
			CheckMaps();													// update the quadtree

		}

		/// <summary>
		/// Move by an amount in XYZ
		/// </summary>
		public void MoveBy(Vector3 displacement)
		{
			location += displacement;
			CheckMaps();													// update the quadtree
		}

		/// <summary>
		/// Rotate relative on all axes
		/// </summary>
		/// <param name="amount"></param>
		public void RotateBy(Rotation amount)
		{
			orientation *= Quaternion.RotationYawPitchRoll(amount.Yaw, amount.Pitch, amount.Roll);
		}

		/// <summary>
		/// Rotate relative on all axes (individually supplied pyr)
		/// Use this when rotating by an amount proportional to the elapsed time
		/// </summary>
		/// <param name="amount"></param>
		public void RotateBy(float pitch, float yaw, float roll)
		{
			orientation = orientation * Quaternion.RotationYawPitchRoll(yaw, pitch, roll);
		}

		/// <summary>
		/// Rotate absolute on all axes (individually supplied pyr)
		/// </summary>
		public void RotateTo(float pitch, float yaw, float roll)
		{
			orientation = Quaternion.RotationYawPitchRoll(yaw, pitch, roll);
		}

		/// <summary>
		/// Rotate absolute on all axes (quaternion)
		/// </summary>
		public void RotateTo(Quaternion or)
		{
			orientation = or;
		}



		/// <summary> Get/set current location </summary>
		public Vector3 Location
		{
			get { return location; }
			set { MoveTo(value); }
		}

		/// <summary> Get current orientation </summary>
		public Orientation Orientation
		{
			get { return new Orientation(); }
		}









		#endregion


		#region --------------- camera support -----------------
		/// <summary>
		/// We've been asked to carry the camera. See if any cell in this organism will accept the role of camera mount.
		/// If so, store a reference to that cell and its hotspot for use by the camera.
		/// </summary>
		/// <returns>true if the camera was successfully assigned</returns>
		public bool AssignCamera()
		{
			foreach (Cell cell in partList)
			{
				int spot = cell.AssignCamera(cameraMount, cameraHotspot);
				if (spot!=-1)
				{
					cameraMount = cell;
					cameraHotspot = spot;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Return the matrix of the hotspot on our cell that is currently acting as the camera mount.
		/// The camera uses this to determine its present location and orientation.
		/// </summary>
		/// <returns></returns>
		public Matrix GetCameraMatrix()
		{
			if (cameraMount!=null)
				return cameraMount.GetHotspotNormalMatrix(cameraHotspot);
			return Matrix.Identity;
		}

		/// <summary>
		/// We are acting as the camera mount and therefore have been sent steering and thrust data
		/// so that the user can control our position. If we're just hosting a creatures-eye-view
		/// then we aren't steerable (although the camera itself might plausibly be). But if we're a
		/// legitimate camera ship then we must respond to the controls.
		/// Send the control data to our ROOT cell's physiology. NOTE: this is probably NOT the cell 
		/// on which the camera is actually mounted, but it makes sense for the steering data to go 
		/// to the root, which can pass signals on to its other cells via nerves as appropriate.
		/// </summary>
		/// <param name="tiller"></param>
		public void Steer(TillerData tiller)
		{
			if (cameraMount!=null)
				rootCell.Physiology.Steer(tiller, Scene.ElapsedTime);			// send the command to the ROOT cell
		}

        /// <summary>
        /// We are a camera ship and have been sent a command from a cockpit control panel button.
        /// Pass it to our root cell for processing (e.g. to steer the camera or a spotlight)
        /// </summary>
        /// <param name="tiller"></param>
        public void Command(string c, object state)
        {
            if (cameraMount != null)
                rootCell.Physiology.Command(c, state, Scene.ElapsedTime);			// send the command to the ROOT cell
        }

		#endregion

		/// <summary>
		/// Override Object.ToString() to return a helpful identifier for debugging etc.
		/// The default is "Organism", but subclasses should replace sessionid with something more useful.
		/// The ID is only guaranteed unique during a single session. The guid is permanently unique but not
		/// very helpful for messages.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return name;
		}



		#region --------- Quadtree functions --------------

		/// <summary>
		/// Add a quad that you now occupy to your list (called by quad tree)
		/// </summary>
		/// <param name="q"></param>
		public override void AddMap(Map q)
		{
			myMaps.Add(q);
		}

		/// <summary>
		/// Remove a quad that you no longer occupy from your list (called by quad tree)
		/// </summary>
		/// <param name="q"></param>
		public override void DelMap(Map q)
		{
			myMaps.Remove(q);
		}

		/// <summary>
		/// After ANY movement, check through my quad list and if I've crossed a quad
		/// boundary, remove myself from the tree and reinsert, so that the database
		/// is kept up to date.
		/// </summary>
		private void CheckMaps()
		{
			for (int q=0; q<myMaps.Count; q++)
			{
				if (!IsIn((myMaps[q]).Bounds))	        			// check each quad to see if I've left it
				{
					Map.Remove(this);								// if so, remove all references to me from quad tree
					Map.Add(this);									// and reinsert myself in new location (updating myquads)
					if (myMaps.Count==0)							// If I'm now in no quads I must have exited the map
					{
						ILeftTheRegion();
						return;
					}
					//Debug.WriteLine(String.Format("obj leaving quad, now in {0} quads",mymaps.Count));
					return;											// if I have to do it once, the job is done
				}
			}
		}

		/// <summary>
		/// This object has gone off the edge of the entire map. Do something about it
		/// TODO: Write code for what to do when an object leaves the map
		/// </summary>
		private void ILeftTheRegion()
		{
			// HACK: just wrap the object back to the other side of the map!
			//			ScreenText.AddLine("{0} left the map [wrapping it]",this);
			if (location.X<0) location.X += Map.MapWidth;
			if (location.X>=Map.MapWidth) location.X -= Map.MapWidth;
			if (location.Z<0) location.Z += Map.MapWidth;
			if (location.Z>=Map.MapWidth) location.Z -= Map.MapWidth;
			Map.Add(this);						// and reinstate it in the quadtree
			//			ScreenText.AddLine("...now in {0} quads",mymaps.Count);

		}

		#endregion

		#region ----------------- Physics --------------------------


		/// <summary>
		/// Apply the various reaction, propulsion and bouyancy forces created by the cells,
		/// to affect the entire organism's position
		/// </summary>
		private void ApplyForces()
		{
			const float BOUYANCYSCALE = 1.5f;			// scales bouyancy alone
			const float PROPSCALE = 0.05f;				// scales propulsion alone
			const float REACTSCALE = 0.5f;				// scales reaction force alone
			const float TORQUESCALE = 0.01f;			// scales torque element
			const float XLATESCALE = 1;					// scales translation element
			const float MAXFORCE = 0.5f;				// maximum allowed length of force vector
            const float ROTATIONALDRAG = 0.998f;        // rotation slows by at least this fraction, even if no draggy limbs

			// Some organisms are fixtures (e.g. the Lab) and so don't respond to forces at all
			if (Dynamic == false)
			{
				return;
			}

			foreach (Cell c in partList)				// add in each cell's reaction force
			{

				// BUOYANCY FORCE
                Vector3 bouy = new Vector3(0, c.Physiology.Buoyancy 
                                            * scale
											* BOUYANCYSCALE
											* Scene.ElapsedTime,0);

				// COLLISION/PROPULSION FORCE
				// This was calculated during collision detection, and represents the reaction force caused by
				// colliding with another object. It may also include a force produced by 'jet propelled' cells
				Vector3 prop = Vector3.Scale(c.propulsionForce,PROPSCALE);

				// REACTION FORCE
				// This is the force caused by a) animation of cells, pushing on the water
				// and b) the reaction against the resistance of the water produced by all cells as a result of
				// the organism's overall movement. Note: this automatically produces realistic friction!
				// The force is calculated from the old and new positions of the cell & water resistance of cell
				// Square forces, so that fast movements have more effect than slow ones.
				// This allows creatures to swim
				Vector3 reaction = (c.OldLocation - c.Location);
				reaction *= c.Physiology.Resistance							// scale by water resistance
                    * scale                                                 // modified by creature's size
					* REACTSCALE;											// and a scaling factor

				Vector3 total = bouy + reaction + prop;						// The sum of all the forces
				float reactionForce = total.LengthSq();						// SQUARED for nonlinear propulsion

				total.Normalize();											// The direction of the displacement

				// Limit total force
				if (reactionForce>MAXFORCE) 
				{
////					Engine.DebugHUD.WriteLine("reactionForce over limit: "+reactionForce);
					reactionForce = MAXFORCE;
				}

				// APPLY TORQUE
				// Convert force and direction back into a force vector
				Vector3 torqueVector = Vector3.Scale(total,reactionForce);

				// the 'handle of the wrench' (vector from CG to cell position)
				Vector3 wrench = c.Location - CGCell.Location;		
				// The torque created by the force is the cross product of this and the force applied to its end.
				// The vector's axis of rotation denotes the direction, and its length is the torque (angular momentum).
				Vector3 axis = Vector3.Cross(wrench,torqueVector);
				float torque = Vector3.Length(axis);							// extract torque
				if (torque!=0)
				{
					axis.Normalize();											// normalise axis of rotation
					// scale force to bring it into a reasonable range
					// and apply proportionally to frame time
					torque *= TORQUESCALE;
					// Get the torque as a rotation, stored in a quaternion
					Quaternion rot = Quaternion.RotationAxis(axis,torque);
					// Use it to accelerate the organism's rotational velocity
					turnRate *= rot;
                    // add some drag by subtracting a fraction of the rotation rate. This stops "infinitely thin" creatures from spinning forever
                   turnRate = Quaternion.Slerp(Quaternion.Identity, turnRate, ROTATIONALDRAG);
                }

				// APPLY TRANSLATION
				reactionForce *= XLATESCALE;
				total.Scale(reactionForce);
				translationRate += total;	

			}

			// APPLY OVERALL FORCE TO SPEED
			// rotate the creature at the new speed, proportional to elapsed time
			// (to scale degrees per second by the fraction of a second elapsed, slerp between a
			// zero rotation and the rate of turn)
			// NOTE: multiply elapsed time by 4 here, otherwise max speed will be 0.5 rev per second,
			// since the turnRate can't be greater than 180 degrees
			// Max speed is now 2 rps, when turnRate == PI deg
			turnRate.Normalize();
			orientation *= Quaternion.Slerp(Quaternion.Identity,turnRate,4);
			orientation.Normalize();

			// Translate the creature proportional to elapsed time
			MoveBy(translationRate);

		}

		/// <summary>
		/// Find out which cell is closest to the organism's centre of gravity.
		/// All future rotations and force calculations occur around CG cell rather than the root.
		/// NOTE: Don't call this until the cells have been given absolute locations
		/// </summary>
		private void CalculateCG()
		{
			Vector3 centre = new Vector3();
			Vector3 xyz = new Vector3();
			int n = partList.Length;

			// First, find the geographical centre of the creature (mean of all cells' locations)
			foreach (Cell c in partList)
				centre += c.Location;	
			centre.X /= n;											
			centre.Y /= n;
			centre.Z /= n;

			// find the distribution of mass around the centre
			foreach (Cell c in partList)
				xyz += (c.Location - centre) * c.Physiology.Mass;	
			xyz.X /= n;											
			xyz.Y /= n;
			xyz.Z /= n;

			// Adjust the CG to the centre of mass
			centre += xyz;

			// Find the cell nearest to this point
			Cell best = rootCell;
			float bestdist = float.PositiveInfinity;
			foreach (Cell c in partList)
			{
				float dist = Vector3.LengthSq(c.Location - centre);
				if (dist<bestdist)
				{
					bestdist = dist;
					best = c;
				}
			}

			// This is our CG cell. All rotations & forces will act around this cell
			CGCell = best;
		}


		/// <summary>
		/// Calculate an overall (absolute) bounding sphere for the whole organism, 
		/// given the cells' current dispositions. (Called on a SlowUpdate, so may be slightly inaccurate)
		/// </summary>
		private void ComputeBoundingSphere()
		{
			AbsSphere.Radius = 0;				// clear the old sphere before collating new sub-spheres
			foreach (Cell c in partList)
			{
				AbsSphere.CombineBounds(c.AbsSphere);
			}
			AbsSphere.Radius *= 1.2f;			// add on a margin to account for movement between now and next time calculated
			UpdateBoundMarker();				/// TEMP: marker to show current sphere
		}



		/// <summary>
		/// Check for collisions with terrain or other organisms
		/// Build a list of unique possible collision targets (tiles or other organisms) using the quad tree.
		/// Initially, just check quickly to see if we are in any danger of hitting any of them. Cull those that
		/// are definitely irrelevant, then look more closely.
		/// </summary>
		private void CheckForCollisions()
		{
			Renderable[] candidate = new Renderable[2000];
			int numCandidates = 0;

			// Don't bother if we're an organism with no physics (e.g. the lab) - things can collide with us but not us with them
			if (Dynamic == false)
				return;

			// Search our list of quads and extract tiles or organisms that we share a bottom-level quad with.
			// Bottom level quads are enough, because no part of us extends beyond this/these.
			foreach (Map quad in myMaps)
			{
				if (quad.Level == Map.NumLevels-1)							// Only search smallest quad or quads
				{
					foreach (Renderable target in quad.OrganismList)		// look at each ORGANISM in quad
					{
						if (target==this)									// ignore self!!!
							continue;

						bool found = false;
						for (int i=0; i<numCandidates; i++)					// if this object isn't already in our list...
						{
							if (candidate[i]==target)
							{
								found=true;
								break;
							}
						}
						if (found==false)
							candidate[numCandidates++] = target;			// ...add it
					}

					foreach (Renderable target in quad.TerrainList)			// look at each TILE in quad
					{
						bool found = false;
						for (int i=0; i<numCandidates; i++)					// if this object isn't already in our list...
						{
							if (candidate[i]==target)
							{
								found=true;
								break;
							}
						}
						if (found==false)
							candidate[numCandidates++] = target;			// ...add it
					}

				}
			}

			// Now cull those that are definitely not a risk because their bounding sphere doesn't intersect ours
			for (int i=0; i<numCandidates; i++)
			{
				// How far, if at all, do we penetrate the candidate
				if (this.AbsSphere.IsPenetrating(candidate[i].AbsSphere)==false)
					candidate[i] = null;				
			}

			// Now we're left with objects whose bounding spheres overlap ours, so examine them more closely.
			for (int i=0; i<numCandidates; i++)
			{
				if (candidate[i]!=null)
				{
					CheckForCollisions2(candidate[i]);
				}
			}

		}

		/// <summary>
		/// Second-level collision detection. The supplied object's bounding sphere overlaps with ours.
		/// Drop down to the cell level and look more closely
		/// </summary>
		/// <param name="obj"></param>
		private void CheckForCollisions2(Renderable candidate)
		{
			// For each of our cells, look to see if it collides with ANY part of the candidate.
			foreach (Cell cell in partList)
			{
				// Do we penetrate the candidate at all?
				if (cell.AbsSphere.IsPenetrating(candidate.AbsSphere))
				{
					// Call the target class's virtual method to test more precisely and return any collision force
					cell.propulsionForce += candidate.CollisionTest(cell); /// *Scene.ElapsedTime * 100f;
				}
			}
		}
		
		/// <summary>
		/// Virtual method implemented by all Renderable objects.
		/// The given cell is in collision with our bounding sphere. Test to see if it actually collides with
		/// one of my parts. If so, return a Vector describing the force we exert on the offending cell
		/// (since we're an organism we'll receive a share of the force too, unlike scenery)
		/// </summary>
		/// <param name="cell">The cell that may have collided with us</param>
		/// <returns> Any force vector acting on the cell </returns>
		public override Vector3 CollisionTest(Cell otherCell)
		{
			Vector3 bounce = new Vector3();

			// Run through our own cells looking for a collision
			foreach (Cell ourCell in partList)
			{
				// Test more deeply by looking at the cell spheres then the 
				// bounding boxes of each mesh in the cells
				if (Cell.CollisionTest(ourCell, otherCell) == true)
				{
					// The bounce direction is a vector in the direction of our centre towards theirs
					// (in other words, the two cells are treated as spheres and therefore bounce back away from
					// their centres). 
					bounce = otherCell.AbsSphere.Centre - ourCell.AbsSphere.Centre;
					bounce.Normalize();

					// The length of the force vector is proportional to the objects' closing speed
					Vector3 ourMvt = ourCell.Location - ourCell.OldLocation;			// how much we will move next frame
					Vector3 hisMvt = otherCell.Location - otherCell.OldLocation;		// how much he will move next frame
					float speed = Vector3.Length(ourMvt + hisMvt);					// combine to get closing movement
					bounce.Scale(2.0f + speed * 10.0f);											// arbitrary scaling factor

					// Also, a force acts on OUR cell, in the reverse direction.
					// Distribute the two forces inversely proportional to mass
					Vector3 ourBounce = -bounce;										// effect on us is the inverse of our effect on him
					float mass = otherCell.Owner.TotalMass;								// get the masses of the two ORGANISMS (not cells)
					float ourMass = ourCell.Owner.TotalMass;
					bounce.Scale(ourMass / (ourMass + mass));							// distribute the forces proportionately
					ourBounce.Scale(mass / (ourMass + mass));
					ourCell.propulsionForce += ourBounce;								// apply our share as propulsion

					// Once we've found a collision, we needn't look at any more of our cells
					return bounce;
				}
			}				
			
			return bounce;
		}

		/// <summary>
		/// Check to see if we're touching the water surface, and respond appropriately
		/// </summary>
		private void CheckForSurfaceCollision()
		{
			// We're quite safe if bounding sphere is below water level
			if ((AbsSphere.Centre.Y + AbsSphere.Radius) < Water.WATERLEVEL)
				return;

			// Now check each cell in turn
			foreach (Cell cell in partList)
			{
				// If the dist from centre to surface is smaller than the radius our spheres are in contact
				float aboveSurface = cell.AbsSphere.Centre.Y + cell.AbsSphere.Radius - Water.WATERLEVEL;
				if (aboveSurface >= 0)
				{
					// TODO: either look deeper at the mesh OBBs, or maybe take account of the density of the object
					// to derive an approximate flotation height

					// Our bounce vector is simply a down vector, proportional to our height above surface
					Vector3 bounce = new Vector3(0,-1,0);
					bounce.Scale(aboveSurface 
						* 0.5f);								// arbitrary scaling factor

					// Add the bounce as a force acting on our cell
					cell.propulsionForce += bounce;
					//Engine.DebugHUD.WriteLine("Surface!");
				}
			}
		}


		#endregion

		#region ------------------ debugging ---------------------
		/// <summary>
		/// Conditional methods to show bounding sphere using a marker
		/// </summary>
		/// <summary> TEMPORARY marker to show bounding sphere </summary>
		private Marker boundMarker = null;
//		private Marker cgMarker = null;
		public bool debugNow = false;			// set to true when debugging to enable output of debug info mid-run

		[Conditional("SHOWBOUNDS")]
		private void UpdateBoundMarker()
		{
			// Show bounding sphere
			if (boundMarker == null)
				boundMarker = Marker.CreateSphere(System.Drawing.Color.FromArgb(16,System.Drawing.Color.Red),
													AbsSphere.Centre, AbsSphere.Radius);
			boundMarker.Scale(AbsSphere.Radius);
			boundMarker.Goto(AbsSphere.Centre);

			// show which cell is CofG
//			if (cgMarker==null)
//				cgMarker = Marker.Create(Color.Red,CGCell.Location,2);
//			cgMarker.Goto(CGCell.Location);
	
		}

		#endregion

		#region ----------------- IDetectable Members (sensory requests) --------------------

		public Vector3 ReqLocation()
		{
			return Location;
		}

		public Vector3 ReqVelocity()
		{
			return translationRate;
		}

		public float ReqSize()
		{
			return AbsSphere.Radius;
		}

		public float ReqMass()
		{
			return TotalMass;
		}

        /// <summary> Return the organism's spectrum - i.e. the main materials for each cell </summary>
        /// <returns></returns>
        public List<ColorValue> ReqSpectrum()
        {
            List<ColorValue> colours = new List<ColorValue>();
            foreach (Cell c in partList)
            {
                List<ColorValue> cellColour = c.ReqSpectrum();
                if (cellColour != null)
                    colours.Add(cellColour[0]);
            }
            return colours;
        }

        /// <summary> Return my depth as a fraction (0=surface, 1=deepest) </summary>
        /// <returns></returns>
        public float ReqDepth()
        {
            return (Water.WATERLEVEL - Location.Y) / Water.WATERLEVEL;
        }


		/// <summary> We've been sent a Stimulus. 
		/// Handle it if possible, or hand it on to one of our cells for handling</summary>
		/// <param name="stimulus">The stimulus information</param>
		/// <returns>Return true if the stimulus was handled by one or more cells</returns>
		public bool ReceiveStimulus(Stimulus stim)
		{
			bool handled = false;
			switch (stim.Type)
			{
					// Stimuli that we know how to handle...

					// TODO: Add organism-wide stimulus cases here, and set handled=true

					// Those we don't know how to handle we send on to all our cells
					// (the cells then check whether the stimulus was intended for them, by
					// seeing if their spheres intersect the transmission range)
				default:
					foreach (Cell c in partList)
					{
						if (c.ReceiveStimulus(stim)==true)
							handled = true;
					}
					break;
			}
			return handled;
		}


		#endregion


		#region ------------------- Editing functions -------------------------

		/// <summary>
		/// Helper for mouse selection of 3D objects. Given a "finger" pointing into the screen,
		/// return the Cell that the finger points to (or null if no such cell).
		/// Also, if the finger is pointing specifically to a SOCKET on that cell, return the socket's frame in the
		/// socket variable
		/// Called by Map.MousePick()
		/// </summary>
		/// <param name="rayPosition">position of ray on screen</param>
		/// <param name="rayDirection">vector pointing into screen</param>
		/// <param name="socket">If a SOCKET on the cell was selected, its frame is returned here</param>
		/// <returns>the nearest selected cell or null</returns>
		public Cell MousePick(Vector3 rayPosition, Vector3 rayDirection, out JointFrame socket)
		{
			float bestDist = float.MaxValue;
			Cell bestCell = null;
			JointFrame bestFrame = null;

			foreach (Cell cell in partList)
			{
				// Unproject the ray and test against every triangle of every mesh
				// in the cell. The cost of this is that I have to retain the original Mesh for each Cytoplasm object, since
				// its ProgressiveMesh doesn't have an .Intersect() method.
				// Return the one (if any) closest to the camera
				JointFrame result = cell.MousePick(rayPosition, rayDirection);
				if (result != null)
				{
					float dist = Vector3.Length(cell.AbsSphere.Centre - Camera.Position);
					if (dist < bestDist)
					{
						bestCell = cell;
						bestFrame = result;
					}
				}
			}
			// If the mesh that was clicked on is a socket, pass the socket frame back to the caller
			// If it was a cell but not a socket, pass back a null, because when the cell changes
			// we must find a new socket to select
			socket = null;
			if (bestCell != null) 
			{
				if (bestFrame.Type == JointFrame.FrameType.Socket)
					socket = bestFrame;
			}

			// Return the nearest selected cell or null
			return bestCell;
		}

		/// <summary>
		/// This organism has been selected for editing. Prepare it
		/// </summary>
		public void EditOn()
		{
			// Temporarily stop responding to physical forces
			Dynamic = false;

			// Force the CofG to the root cell, so that the organism sits properly atop the clamp
			CGCell = rootCell;

			// Select this organism, its root cell and first socket (if any)
			Lab.SelectedOrg = this;
			SelectFirst();
		}

		/// <summary>
		/// This organism has been released from editing. Clean it up to account for any altered cells.
		/// </summary>
		public void EditOff()
		{
			// Make sure that the trees and lists are up-to-date
			Refresh();

			// Recalculate the CofG cell
			CalculateCG();

			// Start responding to physical forces again
			Dynamic = true;

			// Deselect us and our cell/socket
			Lab.SelectedOrg = null;
			Lab.SelectedCell = null;
			Lab.SelectedSocket = null;

			// Write out a new genome file for the creature
			WriteGenomeFile();
		}

		/// <summary>
		/// Write an edited creature's design out as a genome file
		/// </summary>
		private void WriteGenomeFile()
		{
			// We already have a default genome containing the core cell, so let it update its parameters.
			// Cell.UpdateGene() will then recurse through the entire cell tree, adding genes and updating them as it goes.
			rootCell.UpdateGene(genome.Root);

			// The genome file is named according to the species name entered in the UI
			genome.Name = this.name;

			// Now write the genome to an XML file (using the genome name as the filename)
			genome.Write();
		}

		/// <summary>
		/// Recursively create a new flat list of cells from the hierarchical one.
		/// </summary>
		/// <param name="cell"></param>
		private void BuildPartList(List<Cell> list, Cell cell)
		{
			list.Add(cell);
			if (cell.Sibling != null)
				BuildPartList(list, cell.Sibling);
			if (cell.FirstChild != null)
				BuildPartList(list, cell.FirstChild);
		}

		/// <summary>
		/// Update partlist, nerves, etc. after any change to our structure (cell addition, deletion, etc.)
		/// </summary>
		public void Refresh()
		{
			// Rebuild the flat part list
			List<Cell> list = new List<Cell>();
			BuildPartList(list, rootCell);
			partList = list.ToArray();

			// Connect up the channels to their sources/dests
			//Debug.WriteLine("Connecting channels...");
			rootCell.ClearAllChannels();
			rootCell.WireUpAllChannels();
		}

		/// <summary>
		/// Selection change: Select the root cell
		/// </summary>
		public void SelectFirst()
		{
			Lab.SelectedCell = rootCell;
			rootCell.SelectFirst();
		}

		/// <summary>
		/// Selection change: Select the next sibling
		/// </summary>
		public void SelectNext()
		{
			Cell newCell = Lab.SelectedCell.Sibling;
			if (newCell != null)
			{
				Lab.SelectedCell = newCell;
				newCell.SelectFirst();
			}
		}
		/// <summary>
		/// Selection change: Select the previous sibling
		/// </summary>
		/// <returns>true if there was a sibling (false means we're the first child and our parent can be found)</returns>
		public bool SelectPrevious()
		{
			foreach (Cell c in partList)										// search the tree for the cell who's our eldest sibling
			{
				if (c.Sibling == Lab.SelectedCell)
				{
					Lab.SelectedCell = c;
					c.SelectFirst();
					return true;
				}
			}
			return false;														// if none, we must be the eldest in the chain
		}
		/// <summary>
		/// Selection change: Select the first child
		/// </summary>
		public void SelectDown()
		{
			Cell newCell = Lab.SelectedCell.FirstChild;
			if (newCell != null)
			{
				Lab.SelectedCell = newCell;
				newCell.SelectFirst();
			}
		}
		/// <summary>
		/// Selection change: Select the parent
		/// </summary>
		public void SelectUp()
		{
			while (SelectPrevious() == true) ;							// roll back until we're the eldest sibling
			foreach (Cell c in partList)								// search the tree for the cell whose first child is us
			{
				if (c.FirstChild == Lab.SelectedCell)
				{
					Lab.SelectedCell = c;
					c.SelectFirst();
					return;
				}
			}
			SelectFirst();												// if we fail, select the root cell
		}

		/// <summary>
		/// Add a cell of the selected type to the creature at the selected socket (if any)
		/// </summary>
		public void Add(string name)
		{
			// If no socket is selected, quit
			if (Lab.SelectedSocket == null) return;

			// If the selected socket already has a cell attached to it, we mustn't add another!
			if (SelectedSocketIsOccupied())
				return;

			// OK to add
			try
			{
				Gene gene = new Gene(name);												// Create a gene for the cell type
				Cell newCell = Cell.Get(gene, Lab.SelectedOrg);							// create a cell from the gene

				if (Lab.SelectedCell.FirstChild == null)								// if the selected cell has no children
					Lab.SelectedCell.FirstChild = newCell;								// attach the new cell to it as the first child
				else																	// otherwise, drop to the first child and find its last
				{																		// sibling. The new cell becomes the youngest sibling
					Cell sib = Lab.SelectedCell.FirstChild;
					while (sib.Sibling != null)
						sib = sib.Sibling;
					sib.Sibling = newCell;
				}

				newCell.Attach(Lab.SelectedCell, Lab.SelectedSocket);					// record the cell's .parent and .parentSocket

				Refresh();																// update org's part list, channels, etc.

				Lab.SelectedCell = newCell;												// Select the cell we've just added
				newCell.SelectFirst();													// and the first socket on that cell

			}
			catch (Exception e)
			{
				throw new SDKException("Unable to create new cell of selected type: " + name, e);
			}
		}

		/// <summary>
		/// Return true if the currently selected socket already has a cell attached
		/// </summary>
		/// <returns></returns>
		public bool SelectedSocketIsOccupied()
		{
			foreach (Cell c in partList)
			{
				if (c.ParentSocket == Lab.SelectedSocket)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Delete selected cell and any children
		/// </summary>
		public static void DeleteCell()
		{
            Lab.SelectedCell.Dispose();                                         // allow cell library to clean up

			// If we're the root, kill the entire organism
			if (Lab.SelectedCell == Lab.SelectedOrg.rootCell)
			{
				Lab.SelectedOrg.Dispose();										// destroy the creature and REMOVE FROM MAP
				Lab.SelectedOrg = null;
				Lab.SelectedCell = null;
				Lab.SelectedSocket = null;
                return;
			}

			// Else search the tree for the cell who's our eldest sibling
			foreach (Cell c in Lab.SelectedOrg.partList)										
			{
				if (c.Sibling == Lab.SelectedCell)
				{
					c.Sibling = Lab.SelectedCell.Sibling;						// point the elder sibling at our younger sibling (if any)
					Lab.SelectedCell = c.Parent;							    // disconnect the cell and its children to be garbage-collected
                    Lab.SelectedOrg.Refresh();
                    Lab.SelectedCell.SelectFirst();                             // select ANY available socket on the parent
                    return;
				}
			}
			// if none, we must be the eldest in the chain, so disconnect us from our parent
			foreach (Cell c in Lab.SelectedOrg.partList)
			{
				if (c.FirstChild == Lab.SelectedCell)
				{
                    int thisSkt = c.FindSocketNumberOf(Lab.SelectedCell);       // remember the skt of the predecessor we're attached to - this will be the new selection
					c.FirstChild = Lab.SelectedCell.Sibling;					// attach our younger sib (if any) as the new first child
					Lab.SelectedCell = c;                                       // select the parent cell
                    Lab.SelectedOrg.Refresh();
                    Lab.SelectedSocket = (thisSkt == -1) ? null : c.GetSocketFrame(thisSkt);      // select the socket on the parent that we were detached from
                    return;
				}
			}
			// We should never get here 
			Debug.Fail("Organism.DeleteCell() should never get here!");
		}

		/// <summary>
		/// Return stats about the creature being edited, for display on the VDU
		/// </summary>
		public void Stats(out float radius, out float mass, out float bouyancy, out float resistance, 
						  out Vector3 balance, out string name)
		{
			// Ensure the data is accurate after recent edits
			Refresh();															// Make sure that the trees and lists are up-to-date
			CalculateCG();														// Temporarily recalculate the CofG cell
			RecursiveUpdateFrames(rootCell);									// calculate cell positions
			ComputeBoundingSphere();											// update radius and centre

			radius = this.AbsSphere.Radius;										// org radius
			name = genome.Name;													// current name


			mass = 0;
			bouyancy = 0;
			resistance = 0;
			foreach (Cell c in partList)										// sum physical properties
			{
				mass += c.Physiology.Mass * scale;
                bouyancy += c.Physiology.Buoyancy * scale;
				resistance += c.Physiology.Resistance * scale;
			}

			balance = CGCell.AbsSphere.Centre - AbsSphere.Centre;				// distance of CG from geometric centre (on each WORLD axis)
			balance.Scale(1.0f / radius);										// as a fraction of creature's size

			// Clean up
			CGCell = rootCell;													// Set the CofG back to the root cell

		}

		/// <summary>
		/// Access to organism's "species" name for editor
		/// </summary>
		public string Name
		{
			get { return genome.Name; }
			set { genome.Name = value; }
		}

        /// <summary>
        /// Be magnetically attracted to the clamp (tractor beam).
        /// </summary>
        /// <param name="clamp">The location of the clamp</param>
        /// <returns>True if the organism is close enough to the clamp to become attached to it</returns>
        public bool Tractor(Vector3 clamp, Quaternion clampOrientation)
        {
            // If we're close to the clamp, return true and we'll get attached
            Vector3 targetVector = clamp - location;
            if (targetVector.LengthSq() < 1f)
                return true;

            // Switch off physics (it will get switched on again when re-released from clamp)
            Dynamic = false;
            CGCell = rootCell;

            // If we're a long way from underneath lab, aim for a point below lab first, so we don't pass through ship's wall
            if ((Math.Abs(targetVector.X) > 10f) || (Math.Abs(targetVector.Z) > 10f))
                targetVector.Add(new Vector3(0, -8, 0));

            // Apply a movement to bring the creature into alignment with the clamp or lower target
            targetVector.Normalize();
            MoveBy(Vector3.Scale(targetVector,Scene.ElapsedTime * 5f));

            // Rotate by a fraction of the difference between current rotation and that of clamp
            RotateTo(Quaternion.Slerp(Quaternion.RotationMatrix(rootCell.GetPlugMatrix()), clampOrientation, Scene.ElapsedTime * 1f));

            return false;
        }


		#endregion


	}
}
