//#define SHOWBOUNDS		// define this to show bounding spheres as markers


using System;
using System.Diagnostics;
//using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Xml;


namespace Simbiosis
{
	/// <summary>
	/// A whole X file's worth of meshes, animations, etc. representing a single cell
	/// Cells are stored in a library (mananged by the static members of this class) and can be 
	/// cloned to create modifiable instances.
	/// 
	///  
	/// </summary>
	/// <remarks>
	///	- A Cell is a whole X file's worth of meshes, animations, frames, etc.
	/// - Cells are created by reading in X files from disk, as required during the loading of an Organism etc.
	///	- Cells are stored in a library when first accessed. When an Organism needs to obtain its Cells, 
	///	  these are CLONED from the library, and so each instance has its own state data.
	///	  
	///	JOINTS
	///	- Each cell has a single PLUG, which is represented by the origin of the scene when the object is
	///	  saved in Truespace. Cells should have their primary axis oriented along the Y-axis (blue
	///	  line). NEGATIVE Y (into the screen) is forwards.
	///	- Each cell can have zero or more SOCKET joints, marked by a small mesh (e.g. a plane) in Truespace.
	///	  - The child mesh is attached to the main cell by a BONE, which may be zero-DOF or animated.
	///	  - The mesh (or rather the bone that connects it) is called "SKT0", "SKT1", etc. 
	///	  - The normal of the mesh (plane) defines the socket axis.
	///	- When a child cell is attached to a socket it is oriented relative to the socket in a given ROLL
	///	  direction (and possibly also a pitch and yaw). This is unique to each instance
	///	  of a cell and is specified when the cell is first attached to its parent. This PLUG ANGLE is stored
	///	  in the genome (as a matrix) and gets placed into the cell instance during cloning. 
	///	  The actual orientation of the child cell is therefore a combination of the socket and plug orientations
	///	  (the socket being generic and defined when the cells are designed, while the plug is defined uniquely 
	///	  for each cell instance), allowing the user to rotate child cells freely within their parent sockets.
	///	- Bones are used to support animation. SKINNING is not supported because of computational cost 
	///	  and problems with orienting joints in Truespace. A joint can rotate a socket mesh directly if required
	///	  or can animate other non-socket child meshes.
	///	- Other child meshes are used to specify HOTSPOT locations and orientations in the same way
	///	  as for a socket. The bones/frames supporting these are labelled "hot0" onwards.
	///	
	///	FRAME HIERARCHY
	///	- The frame hierarchy is CLONED for new Cell instances (I had to use own frame class because MDX doesn't support cloning)
	/// - Frames in the hierarchy may represent:
	///   - The cell's absolute position and attitude (the root frame)
	///   - The abs location and orientation of valid SOCKETS, to which child cells can be attached 
	///     Socket frames are named "skt0", "skt1", etc. in Truespace. (Note: the socket is created by adding
	///     a child mesh, but it's the BONE that needs to be named "skt#" using the scene editor)
	///   - The abs location and orientation of EFFECTORS. These points can be used to detect 
	///     active points of contact between one creature and another, e.g. to sting or extract energy, or define the
	///     location and orientation of nozzles for propulsion, "squirting", etc. 
	///   - The abs location and orientation of SENSORS (providing line-of-sight or touch data).
	///     Both effectors AND sensors are regarded as HOTSPOTS and named "hot0", "hot1", etc.
	///   - Internal joints for flexing or contracting the cell, or animating parts of it. These are used by the
	///     animator. ANIMATABLE JOINTS are to be named "anim0", etc.
	///   - Other odd frames that Truespace feels the need to include, such as child mesh frames. 
	///	  
	///	- A cloned Cell has unique instances of the following contained resources:
	///		> its animation controller
	///		> its joint angles and velocities for physical animation
	///		> the frame hierarchy
	///		> other state data
	///	- The following are NOT cloned and therefore are shared between all cell instances of a given type:
	///		- the Cytoplasm (meshes)
	///	  
	///	  
	/// </remarks>
	/// 
	public class Cell : IDisposable, IDetectable, IControllable
	{

		#region ---- Static members (Cell library) ----

		/// <summary> ***** The Cell library, keyed by filename (minus the .X) ***** </summary>
		private static SortedList<string, Cell> library = new SortedList<string, Cell>();
		public static int LibrarySize { get { return library.Count; } }

		#endregion


		#region ---- Instance members (Cell) ----


		// -------- shared between all instances ------

        /// <summary> full name as used in the library key </summary>
        private string fullname = null;
		/// <summary>Cell name for defining X file, printing messages, etc. </summary>
		public string name = null;
		/// <summary> Name of the DLL containing the CellType class for this cell (or null if the cell is a default type) </summary>
		public string group = null;
        /// <summary> Variant of the cell type as a string (the filename of the .X file) </summary>
        public string variantName = "default";
        /// <summary> Variant of the cell type as an integer (for fast access to channel data) </summary>
        public int variant = 0;
        /// <summary> The approximate colour of this cell </summary>
        public ColorValue colour = ColorValue.FromColor(Color.Gray);

        // -------- cloned from master ----------

		/// <summary> 
		/// Root frame of the hierarchy (bones and sockets)
		/// </summary>
		private JointFrame rootFrame = null;
		public JointFrame RootFrame { get { return rootFrame; } }

		// -------- unique to each instance ----------
        // WARNING: If I create any arrays or other references here, DON'T do "thing[] n = new thing[]", because Clone() will shallow copy and 
        // therefore every cell instance will point to the SAME thing[]. Instead, set the object to null here and create a new object for each 
        // instance inside the Clone() method

		/// <summary> The cell to which I'm attached </summary>
		private Cell parent = null;
        public Cell Parent { get { return parent; } }

		/// <summary> The socket frame on my parent to which I'm attached </summary>
		private JointFrame parentSocket = null;
		public JointFrame ParentSocket { get { return parentSocket; } set { parentSocket = value; } }

		/// <summary> The Organism I belong to </summary>
		private Organism owner = null;
		public Organism Owner { get { return owner; } }

		/// <summary> 
		/// A list of the frames to use for collision detection 
		/// (i.e those containing meshes that aren't merely sockets or hotspots)
		/// </summary>
		private List<JointFrame> collisionFrames = null;

        /// <summary>
        /// Markers for debugging sensors - see ShowSensoryField()
        /// </summary>
        private Marker[] sensoryField = null;


		/// <summary> 
		/// The orientation of this cell within its socket.
		/// The user can rotate cells in their sockets during design, and this is later stored in the genome.
		/// When an organism is loaded from a genome, this matrix is supplied by the gene. It's multiplied with
		/// the combined transformation matrix of the parent socket before computing the combined matrices for
		/// this cell.
		/// TODO: Can I integrate this into the frame hierarchy (e.g. multiply root matrix by it), now that each instance has its own hierarchy
		/// </summary>
		public Matrix PlugOrientation = Matrix.Identity;

		/// <summary> Cells are arranged in a tree form inside Organisms. Store my location in the tree here </summary>
		private Cell sibling = null;
		public Cell Sibling { get { return sibling; } set { sibling = value; } }
		private Cell firstChild = null;
		public Cell FirstChild { get { return firstChild; } set { firstChild = value; } }

		/// <summary> For master cells, this increments with every new instance created. 
		/// For instances, this is their unique instance number </summary>
		public int instance = 0;

		/// <summary> An array of references to my animatable frames, stored in index order ("anim0"...) </summary>
		private JointFrame[] joint = null;
		/// <summary> An array of references to my hotspot (effector/sensor) frames, stored in order ("hot0"...) </summary>
		private JointFrame[] hotspot = null;
        /// <summary> An array of references to my socket frames, stored in order ("skt0"...) </summary>
        private JointFrame[] socket = null;
        /// <summary> The chemical channels (extracted from mesh ("chan0"...) </summary>
		private Channel[] channel = null;

		/// <summary> Returns number of hotspots in cell </summary>
		public int NumHotspots { get { return hotspot.Length; } }
		/// <summary> Returns number of sockets in cell </summary>
		public int NumSockets { get { return socket.Length; } }
		/// <summary> Returns number of channels in cell </summary>
		public int NumChannels { get { return channel.Length; } }

		/// <summary> World coordinates at previous frame </summary>
		private Vector3 oldLocation = new Vector3();
		public Vector3 OldLocation { get { return oldLocation; } }
		/// <summary> Current world coordinates </summary>
		public Vector3 Location { get { return new Vector3(	rootFrame.CombinedMatrix.M41,
															rootFrame.CombinedMatrix.M42,
															rootFrame.CombinedMatrix.M43); } }
		/// <summary> 
		/// Approximate bounding sphere (only approximate because animation may make the cell bigger).
		/// Computed from the initial bounding spheres of all the meshes in the cell. Centre is relative to the root frame
		/// Shared by all clones
		/// </summary>
		public Sphere RelSphere;
		/// <summary> ABSOLUTE bounding sphere of cell, for collision detection </summary>
		public Sphere AbsSphere = new Sphere();			// sphere in world coordinates

		/// <summary> 
		/// Force vector produced by a collision with another object and/or the 'jet propulsion'
		/// force produced by certain types of cell
		/// </summary>
		public Vector3 propulsionForce = new Vector3();		

		/// <summary> Our Physiology class, containing the cell's functionality </summary>
		private Physiology physiology = null;
		public Physiology Physiology { get { return physiology; } }



		#endregion

		#region -------- constructors & initialisation -------------


		/// <summary>
		/// Construct a new Cell FOR USE IN THE LIBRARY as an archetype
		/// - Use Get() to get instances for actual animation and rendering
		/// </summary>
		/// <param name="name">name of Cell type, in the form "DLLname:celltype.variant" (where .variant is optional)</param>
		private Cell(string fullname)
		{
            // Store the full name so that we can identify our archetype in the library (to remove it when all instances have been disposed)
            this.fullname = fullname;

			// Parse the group:name.variant into group, name & variant
            int colon = fullname.IndexOf(":");							            // if the celltype has no : it is in the default DLL 
            if (colon >= 0)
            {
                group = fullname.Substring(0, colon);				  			    // the first part is the DLL assembly name
                name = fullname.Substring(colon + 1);	        	    		    // the second is the class name, plus possible variant
            }
            else
            {
                group = "CellTypes";
                name = fullname;
            }
            int dot = name.IndexOf(".");                                            // find start of any variant
            if (dot >= 0)                                                           // If the name ends in a variant
            {
                variantName = name;
                name = name.Substring(0, dot);                                      // remove it from name
                variantName = variantName.Substring(dot+1);                         // and store it in variant
            }
            else
            {
                variantName = "default";                                            // if no variant specified, use "default.x"
            }
                        
			// Load the frame hierarchy from disk
			rootFrame = CellLoader.Load(group, name, variantName);

			// Compute the overall bounding sphere for the cell, now that the frame hierarchy and meshes exist
			ComputeBoundingSphere();

            // Compute an approximate colour for this cell, for use in optical sensors
            ComputeColour();

			// Add this Cell to the library (key is complete dll.type name)
			library.Add(fullname, this);
		}

		/// <summary>
		/// Clone this cell
		/// </summary>
		/// <param name="gene">The gene specifying this cell instance's orientation, wiring, etc.</param>
		/// <param name="owner">The organism that owns me</param>
		private Cell Clone(Gene gene, Organism owner)
		{
			// Make a shallow copy of all members
			Cell newCell = (Cell)this.MemberwiseClone();

			// Replace members that are CLONED copies of objects in the master...
			newCell.rootFrame = JointFrame.CloneHierarchy(rootFrame);	// my unique copy of the frame hierarchy
			// TODO: Clone any other members here

			// Replace or create members that are UNIQUE to this instance...
			newCell.instance = instance++;													// my instance number (tally in master)
			newCell.PlugOrientation = gene.Orientation;										// my orientation in the parent socket
			newCell.owner = owner;															// my owning organism

			newCell.joint = JointFrame.IndexFrameType(
							newCell.rootFrame,JointFrame.FrameType.Animating);				// lists of my animation, hotspot & skt frames
			newCell.hotspot = JointFrame.IndexFrameType(
							newCell.rootFrame, JointFrame.FrameType.Hotspot);
			newCell.socket = JointFrame.IndexFrameType(
							newCell.rootFrame, JointFrame.FrameType.Socket);

			newCell.physiology = Physiology.LoadPhysiology(newCell.group, newCell.name, newCell.variantName,		// my Physiology class containing functionality
															newCell,
															newCell.joint.Length,
															newCell.socket.Length);

            newCell.GetMyVariantIndex();                                                    // Convert variant name into an index before trying to access channel data

			newCell.channel = newCell.LoadChannels(gene);									// lists of channel info from gene AND mesh frames

			newCell.collisionFrames = new List<JointFrame>();
			newCell.GetMeshFrames(newCell.rootFrame, newCell.collisionFrames, false);		// a list of mesh-containing frames for collision tests


			// Set all the internal (organelle) frames to invisible, because they aren't normally rendered
			newCell.HideOrganelles();

			// TODO: Initialise other instance-specific members here
           

            // Now that everything is set up, call the Physiology subclass's Init() method to allow the physiology to initialise itself
            newCell.physiology.Init();

			// Add a device reset handler to rebuild this cell's resources
			Engine.Device.DeviceReset += new System.EventHandler(newCell.OnReset);

			return newCell;
		}


		/// <summary>
		/// Get a clone of this Cell from the library, loading it first if necessary
		/// </summary>
		/// <returns>A modifiable clone of the named Cell</returns>
		/// <param name="gene">The gene containing the cell's properties, orientation and wiring</param>
		/// <param name="owner">The organism that owns me</param>
		public static Cell Get(Gene gene, Organism owner)
		{
			Cell cell = null;

			// Get name of the cell from the gene
			string fullname = gene.XFile();

			// if the library already contains a Cell of that name, that's what we'll clone from
			if (library.ContainsKey(fullname))
			{
				cell = (Cell)library[fullname];
//				Debug.WriteLine("	Cell: Created new Cell instance: "+name+":"+cell.instance);
			}
			// otherwise, create a new entry in the library by loading a whole X file
			else
			{
				cell = new Cell(fullname);
//				Debug.WriteLine("	Cell: Created new Cell archetype: "+name+":"+cell.instance);
			}

			// Return a cloned instance of this Cell
			Cell newCell = cell.Clone(gene, owner);	

			newCell.CreateBoundMarker();								// temp: create marker to show bounding sphere

			return newCell;
		}



		/// <summary>
		/// The absolute location/orientation of this Cell is determined by its SOCKET on the parent Cell.
		/// This method locates the frame in the parent with the given (genetically defined) name
		/// and stores a reference to it in .socket
		/// Also stores a reference to the parent cell, to make it easier to walk up the tree
		/// </summary>
		/// <param name="parent">the Cell to which I'm connected</param>
		/// <param name="skt">the name of the socket to which I'm attached</param>
		public void Attach(Cell parent, string sktname)
		{
			try
			{
				parentSocket = JointFrame.FindBaseName(parent.RootFrame, sktname);
				this.parent = parent;
			}
			catch { }
			if (parentSocket == null)
			{
                throw new SDKException("Cell.FindSocket() was unable to connect cell [" + this.name + "] to parent socket [" + parent.name + "-" + sktname + "]");
			}
			//			Debug.WriteLine("	Connected cell ["+this.name+"] to parent socket ["+parent.name+"-"+sktname+"]");
		}

		/// <summary>
		/// Overload of Attach() to attach a cell to its parent given the actual attachment socket, rather than its name
		/// </summary>
		/// <param name="parent">the Cell to which I'm connected</param>
		/// <param name="skt">the actual socket to which I'm attached</param>
		public void Attach(Cell parent, JointFrame skt)
		{
			parentSocket = skt;
			this.parent = parent;
		}

        /// <summary>
        /// Determine our variant number from the variant name
        /// </summary>
        private void GetMyVariantIndex()
        {
            string[] variants = Physiology.IndexVariants(variantName);
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i].ToLower() == variantName.ToLower())
                {
                    variant = i;
                    return;
                }
            }
            throw new SDKException("Celltype " + fullname + "doesn't have a variant called " + variantName + ". Did you fail to override Physiology.IndexVariants()?");
        }

		/// <summary>
		/// Clear all the channel connections prior to (re-)wiring them.
		/// Called on the root cell of the Organism before calling WireUpAllChannels()
		/// </summary>
		public void ClearAllChannels()
		{
			for (int c = 0; c < channel.Length; c++)
			{
				channel[c].signal = 0;
				channel[c].source = null;
			}
			if (sibling != null)
				sibling.ClearAllChannels();
			if (firstChild != null)
				firstChild.ClearAllChannels();
		}

		/// <summary>
		/// Called by the Organism once the entire Cell tree exists. This recursive fn connects up the channels, 
		/// allowing cells to read chemical signals from their neighbours.
		/// The method should be called on the ROOT cell of the organism. It will then recurse through the rest of the tree.
		/// </summary>
		/// <remarks>
		/// Recurse through the tree and iterate through the channels, looking for output channels.
		/// For each output channel, work outwards, finding all the bypass/input channels in the adjacent cell that should
		/// connect to it, and set their .source pointers to the correct plug/skt (1st or 2nd element in the channelData array).
		/// Stop after you reach an input channel or a bypass channel with no connection.
		/// </remarks>
		public void WireUpAllChannels()
		{
			// Iterate through all my channels...
            //Debug.WriteLine("--------- wiring cell "+name+instance+" ---------");

			// call our cell type to get a list of socket#s for each channel
			ChannelData[] ourSocket = this.Physiology.GetChannelData();

            if (ourSocket != null)                                                          // we may not have any channels
			{
				for (int c = 0; c < channel.Length; c++)									// For each channel...
				{
					if (ourSocket[c].IsOutput())											// if it is an output channel
					{																		// start with the o/p channel as the source
						channel[c].signal = 0;												// don't be fooled into thinking this has been flipped
						//Debug.WriteLine("From output channel " + c+"...");
						while (this.ConnectToMe(c, ref ourSocket));							// recursively link subsequent channels to it
					}
				}
			}
			
			// Finally, recurse down the cell tree
			if (sibling != null)
				sibling.WireUpAllChannels();
			if (firstChild != null)
				firstChild.WireUpAllChannels();
		}

		/// <summary>
 		/// Recursively attempt to connect up a chain or tree of channels.
		/// This cell's channel[c] is the channel we should try to connect to
		/// ourSocket[c,#] contains the assignments for that channel
		/// Return false if:
		///		- we have just connected an input channel to the end of the chain
		///		- we couldn't find a channel to connect to the end of the chain
		/// </summary>
		/// <param name="c">index into this cell's channel array</param>
		/// <param name="ourSocket">assignment table for all the channels in this cell</param>
		/// <returns></returns>
		private bool ConnectToMe(int c, ref ChannelData[] ourSocket)
		{
			Cell otherCell;
			int otherSkt;
			ChannelData.Socket skt;
			bool stillGoing = true;
			bool foundOne = false;

			// if this channel is an input channel we've finished
			if (ourSocket[c].IsInput())
				return false;

			// If this is a bypass channel that has been flipped, the dest cell is on ourSocket[c,0]
			// otherwise it's on ourSocket[c,1]
			if (this.channel[c].signal == 0)
				skt = ourSocket[c].Dest;
			else
				skt = ourSocket[c].Source;

			// if the output end of this channel is on the plug, the dest cell is the parent
			// And we are on one of its sockets
			if (skt==ChannelData.Socket.PL)
			{
				otherCell = this.parent;
				//otherSkt = FindSocketNumberOf(otherCell);
                otherSkt = otherCell.FindSocketNumberOf(this);
			}
			// otherwise, find the cell that is on the correct output socket
			// and we are on that cell's plug
			else
			{
				otherCell = FindCellAtSocket((int)skt);
				otherSkt = -1;
			}

			// If there's no cell here, we've reached the end of the chain, so back up
			if (otherCell == null)
				return false;

			// Scan through other cell, looking for ALL channels connected to the right socket
			// with the right chemistry
			ChannelData[] otherChannel = otherCell.physiology.GetChannelData();
			for (int o = 0; o < otherCell.channel.Length; o++)
			{
				// if the input side of the other channel is on the correct skt and has the correct chemistry
				if (((int)otherChannel[o].Source == otherSkt) && (otherCell.channel[o].chemical == this.channel[c].chemical))
				{
					// This channel is connected to us, so store a ref to us in its .source
					otherCell.channel[o].source = channel[c];
					otherCell.channel[o].signal = 0;
					//Debug.WriteLine("Connected channel " + o + " of " + otherCell.OwnerOrganism() + otherCell.name + otherCell.instance + " to channel " + c + " of " + OwnerOrganism() + name + instance + " using chem " + channel[c].chemical);
					foundOne = true;

					// Then move onto that channel and recurse down the chain.
					// If we've reached the end, remember that but continue looking at rest of channels at this level
					if (!otherCell.ConnectToMe(o, ref otherChannel))
						stillGoing = false;
				}

				// This channel might be a bypass channel pointing the wrong way, so we have to check both ends
				// and set .signal if we have to swap it round
				if ((otherChannel[o].IsBypass()) && ((int)otherChannel[o].Dest == otherSkt) && (otherCell.channel[o].chemical == this.channel[c].chemical))
				{
					// This channel is connected to us, so store a ref to us in its .source
					// And record the fact that the channel has been flipped
					otherCell.channel[o].source = channel[c];
					otherCell.channel[o].signal = 1;
                    //Debug.WriteLine("Flipped channel " + o + " of " + otherCell.OwnerOrganism() + otherCell.name + otherCell.instance + " to channel " + c + " of " + OwnerOrganism() + name + instance + " using chem " + channel[c].chemical);
                    foundOne = true;

					// Then move onto that channel and recurse down the chain.
					// If we've reached the end, remember that but continue looking at rest of channels at this level
					if (!otherCell.ConnectToMe(o, ref otherChannel))
						stillGoing = false;
				}

			}
			// if we went through the whole loop without finding a way forward, we've finished
			if (foundOne == false)
				stillGoing = false;

			return stillGoing;
		}


		/// <summary>
		/// Return the socket number to which this other cell is connected
		/// </summary>
		/// <param name="other">the cell we're looking for</param>
		/// <returns>-2 if not found; -1 if the cell is on our plug; 0-n if the cell is on one of our sockets</returns>
		public int FindSocketNumberOf(Cell other)
		{
			// Are we the other's parent? If so, return the skt# to which it is attached
			if (this == other.parent)
				return other.parentSocket.Index;

			// Is other our parent? If so, return -1 because it is attached to our plug
			if (this.parent == other)
				return -1;
			
			// other isn't connected to us at all
			return -2;
		}

		/// <summary>
		/// Return the cell that is attached to a given numbered socket
		/// </summary>
		/// <param name="skt">socket number</param>
		/// <returns>null if the socket is empty</returns>
		public Cell FindCellAtSocket(int skt)
		{
			// Child cells aren't necc in skt order, so search them
			Cell child = this.firstChild;
			while (child != null)
			{
				if (child.parentSocket.Index == skt)
					return child;
				child = child.sibling;
			}

			return null;
		}

		/// <summary>
		/// Propagate the signals through the channels (called every frame)
		/// </summary>
		private void UpdateChannels()
		{
			foreach (Channel c in channel)
			{
                if (c.source != null)
                {
                    c.signal = c.source.signal;
                    //Debug.Assert(c.signal != float.NaN);
                }
			}
		}

 
		/// <summary>
		/// // device reset event - rebuild all resources
		/// </summary>
		/// <param name="sender">ignored</param>
		/// <param name="e">ignored</param>
		public void OnReset(object sender, EventArgs e) 
		{
		}

        /// <summary>
        /// If the cell being disposed of is the last remaining instance, remove the archetype from the library
        /// </summary>
		public void Dispose()
		{
            if (library.ContainsKey(fullname)) 
            {
                Cell arch = library[fullname];
                if (--arch.instance <= 0)
                {
                    Debug.WriteLine("Disposing of cell type " + fullname + " from library");
                    library.Remove(fullname);
                }
            }
		}

		/// <summary>
		/// Recurse through my frame hierarchy and construct an overall bounding sphere containing all
		/// the (transformed) meshes in this cell. Do this once, when all meshes have been loaded.
		/// This cell-wide sphere can then be used to construct a dynamic organism-wide sphere for culling
		/// and collisions.
		/// The result of this method is a RELATIVE sphere. The absolute centre is calculated from this every frame
		/// </summary>
		public void ComputeBoundingSphere()
		{
			RelSphere.Radius = 0;							// clear the old sphere
			ComputeBoundingSphere(rootFrame,Matrix.Identity);
			AbsSphere.Radius = RelSphere.Radius;			// Local copy of radius can now be set (never changes)
		}
		private void ComputeBoundingSphere(JointFrame frame, Matrix parentMatrix)
		{
			// Give the meshes their proper relative positions within the cell
			Matrix combinedMatrix = frame.TransformationMatrix * parentMatrix;

			// If this frame contains a mesh, transform its bounding sphere and
			// combine it with the overall bounds for the cell
			if (frame.MeshContainer!=null)
			{
				Cytoplasm cyt = (Cytoplasm)frame.MeshContainer;
				float radius = cyt.BoundRadius;	
				Vector3 centre = cyt.BoundCentre;
                // transform the sphere's centre
				centre.TransformCoordinate(combinedMatrix);
                // Transform the sphere's radius (to scale it - the original vertices are probably not at their final scale
                Vector3 radiusVector = new Vector3(radius, 0, 0);               // create a vector of size radius
                radiusVector.TransformCoordinate(combinedMatrix);               // transform it to rescale it
                radius = radiusVector.Length();                                 // scaled radius is the length of the transformed vector
                // Combine this sphere with the others in the cell
				RelSphere.CombineBounds(centre, radius);
			}

			// Now propagate the new combined matrix through to my siblings and children
			if (frame.Sibling != null)											// recurse through siblings
			{
				ComputeBoundingSphere(frame.Sibling, parentMatrix);
			}
			if (frame.FirstChild != null)										// recurse through children
			{
				ComputeBoundingSphere(frame.FirstChild, combinedMatrix);
			}
		}

        /// <summary>
        /// Compute an approximate colour for this cell, for use in optical sensors, etc.
        /// Colour is the diffuse colour of the material of the first mesh found,
        /// or the mean colour of its texture.
        /// Called at LOAD time (because it is compute-intensive)
        /// anim# frames may change colour later, so sensors (via ReqSpectrum()) may wish to check for this dynamically
        /// </summary>
        private void ComputeColour()
        {
            List<JointFrame> meshFrames = new List<JointFrame>();

            GetMeshFrames(rootFrame, meshFrames, false);                                            // get a list of frames containing significant meshes
            if (meshFrames.Count == 0)
                return;

            Cytoplasm cyt = (Cytoplasm)(meshFrames[0].MeshContainer);                               // get first mesh

            Texture tex = null;                                                                     // get first texture, if any
            for (int i = 0; i < cyt.textures.Length; i++)
            {
                if (cyt.textures[i] != null)
                {
                    tex = cyt.textures[i];
                    break;
                }
            }

            if (cyt.materials.Length > 0)                                                           // if the mesh has a material...
            {
                float r = 0, g = 0, b = 0;
                int count = 0;
                for (int i = 0; i < cyt.materials.Length; i++)                                      // average their (non-zero) diffuse colours
                {
                    float rr = cyt.materials[i].DiffuseColor.Red;
                    float gg = cyt.materials[i].DiffuseColor.Green;
                    float bb = cyt.materials[i].DiffuseColor.Blue;

                    if (rr + gg + bb != 0)
                    {
                        r += rr;
                        g += gg;
                        b += bb;
                        count++;
                    }
                }
                colour = new ColorValue(r / count, g / count, b / count);
            }

            if (tex !=null)                                                                         // but if the mesh has a texture...
            {
                float r = 0, g = 0, b = 0;
                int pitch = 0;
                SurfaceDescription sfc = tex.GetLevelDescription(0);
                int texSize = sfc.Width * sfc.Height;                                               // size of texture in pixels
                Debug.Assert((sfc.Format == Format.A8R8G8B8)||(sfc.Format == Format.X8R8G8B8));     // check texture is valid ARGB or XRGB
                byte[,] RGBs = (byte[,])tex.LockRectangle(typeof(byte), 0, LockFlags.ReadOnly, out pitch, new int[] { texSize, 4 }); // lock it to read the pixels as array of ARGBs
                for (long i = 0; i < texSize; i++)
                {
                    r += RGBs[i,1];
                    g += RGBs[i,2];
                    b += RGBs[i,3];
                }
                r /= texSize;                                                                       // calculate the average of R, G and B
                g /= texSize;                                                                       // (This is potentially troublesome, since half red pixels and half green
                b /= texSize;                                                                       // would average yellow, but the mode is harder to compute and may not be better)
                tex.UnlockRectangle(0);                                                             // release the texture
                colour = new ColorValue(r / 255f, g / 255f, b / 255f);                              // store as the colour
            }
        }
 

		/// <summary>
		/// Create a list of Channels, using the mesh hierarchy to get the visible frames representing the channels,
		/// and the gene or the physiology to get the species-specific parameters such as chemical selectivity
		/// </summary>
		/// <param name="gene"></param>
		/// <returns></returns>
		private Channel[] LoadChannels(Gene gene)
		{
			// Read any channels defined by the gene
			Channel[] channel = gene.GetChannels();

			// If none are defined, create some and load with the default chemical#s from the cell type
			if (channel.Length == 0)
			{
				ChannelData[] chanData = physiology.GetChannelData();					// read the default src, dst, chem
				if (chanData != null)
				{
					channel = new Channel[chanData.GetLength(0)];						// create enough channels
					for (int c = 0; c < channel.Length; c++)							// copy in the default chemical selectivity
					{
						channel[c] = new Channel();
						channel[c].chemical = chanData[c].Chemical;
                        channel[c].constant = chanData[c].Constant;                     // and default for user-definable constant
					}
				}
			}

			//// Get a list of the "chan#" frames (visible organelles), in order, then copy to the channels
			//JointFrame[] channelFrame = null;
			//try
			//{
			//    channelFrame = JointFrame.IndexFrameType(rootFrame, JointFrame.FrameType.Channel);
			//    for (int c = 0; c < channel.Length; c++)
			//    {
			//        channel[c].organelle = channelFrame[c];
			//    }
			//}
			//catch
			//{
			//    throw new SDKException(this.name + " has different numbers of channels in the cell type (" 
			//                            + channel.Length + ") and the mesh (" + channelFrame.Length + ")");
			//}

			return channel;
		}


		#endregion



		#region ------- updating & rendering ---------

		/// <summary>
		/// Store our previous location, for calculating reaction forces
		/// </summary>
		public void RecordLastPosition()
		{
			oldLocation.X =	rootFrame.CombinedMatrix.M41;
			oldLocation.Y = rootFrame.CombinedMatrix.M42;
			oldLocation.Z = rootFrame.CombinedMatrix.M43; 
		}

		/// <summary>
		/// Render this (visible) cell
		/// </summary>
		/// <param name="lod">level of detail</param>
		public void Render(float lod)
		{
			// Render code depends on whether we're in design mode or a scanner mode at the moment
			switch (Camera.ScannerMode)
			{
				case Camera.ScannerModes.Normal:
					RecursiveDraw(rootFrame, lod);				// normal display mode
					break;

				case Camera.ScannerModes.Cell:					// cell editing mode
					DrawCellMode(lod);
					break;

				case Camera.ScannerModes.Channel:				// channel editing mode
					DrawChannelMode(lod);
					break;
				
				case Camera.ScannerModes.Chemical:				// chemoscan mode
					DrawScannerMode(lod);
					break;
			}

			UpdateBoundMarker();						/// TEMP: show bounds
		}



		/// <summary>
		/// Update combined matrices, and do all other
		/// updating that should occur for ALL cells, whether visible or not
		/// </summary>
		public void Update()
		{
            try
            {
                // wipe any propulsion or collision forces produced during the previous frame
                propulsionForce = Vector3.Empty;

                // Propagate signals through the channels
                UpdateChannels();

                // Update my physiology, nerve signals, etc.
                physiology.FastUpdate(Scene.ElapsedTime);
                
                // Update the joints and animate the cell's parts as a result
                UpdateJoints();

                // Update all our frames to the correct abs locations
                UpdateFrames();
            }
            catch (Exception e)
            {
                throw new SDKException("Failed updating cell " + name + ". Error: ", e);
            }
		}

		/// <summary>
		/// Run the cell's type-dependent behaviours
		/// </summary>
		public void SlowUpdate()
		{
            try
            {
                // Allow my physiology to do any less frequent updates
                physiology.SlowUpdate();
            }
            catch (Exception e)
            {
                throw new SDKException("Failed slow updating cell " + name + ". Error: ", e);
            }

		}



		/// <summary>
		/// Position this cell's root frame at a specified abs location/orientation/scale
		/// (because we are the root cell and the organism has moved)
		/// </summary>
		/// <param name="locationMatrix">The matrix describing the location/orientation/scale</param>
		public void Locate(Matrix locationMatrix)
		{
			RootFrame.CombinedMatrix = RootFrame.TransformationMatrix * locationMatrix;
			// We also need to reposition the bounding sphere
			AbsSphere.Centre = Vector3.TransformCoordinate(RelSphere.Centre,locationMatrix);
		}

		/// <summary>
		/// Update the combined transformation matrices for all frames in this cell.
		/// At the same time, calculate how much the cell has moved, for physics
		/// </summary>
		public void UpdateFrames()
		{

			// If we're the root cell then our combined matrix will have been set up by the organism (via Locate()).
			// Otherwise, we need to fetch it from our parent's socket (and apply our plug orientation)
			if (parentSocket!=null)
			{
                rootFrame.CombinedMatrix = rootFrame.TransformationMatrix * PlugOrientation * parentSocket.CombinedMatrix;	// + local offset

                // Update our bounding sphere too, while we have the information about its location
                AbsSphere.Centre = Vector3.TransformCoordinate(RelSphere.Centre, parentSocket.CombinedMatrix);
            }

			// calculate the combined transformation matrices of any child frames
			if (rootFrame.FirstChild!=null)
				RecurseFrameMatrices(rootFrame.FirstChild,rootFrame.CombinedMatrix);	

		}


		/// <summary>
		/// Called by UpdateFrames() to recursively calculate all combined matrices.
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="parentMatrix"></param>
		private void RecurseFrameMatrices(JointFrame frame, Matrix parentMatrix)
		{
			// My combined transformation matrix is computed by combining my frame orientation
			// with my parent's combined matrix
			frame.CombinedMatrix = frame.TransformationMatrix * parentMatrix;	// combine my matrix with total so far

			// Now propagate the new combined matrix through to my siblings and children
			if (frame.Sibling != null)											// recurse through siblings
			{
				RecurseFrameMatrices(frame.Sibling, parentMatrix);
			}
			if (frame.FirstChild != null)										// recurse through children
			{
				RecurseFrameMatrices(frame.FirstChild, frame.CombinedMatrix);
			}
		}

		/// <summary>
		/// Pass any newly calculated joint positions from the Physiology through to the 
		/// appropriate frame's motion controller for visualisation.
		/// NOTE: If updating joints every frame is too expensive, do it less often (but faster than a slowupdate) and
		/// use some kind of moving average here to smooth things out
		/// </summary>
		private void UpdateJoints()
		{
            int j=0;

            try
            {
                for (j = 0; j < physiology.JointOutput.Length; j++)
                {
                    joint[j].Motion.Transform(joint[j], physiology.JointOutput[j]);
                }
            }
            catch (Exception e)
            {
                throw new SDKException("Reference to a joint (anim"+j+") that doesn't exist in cell "+name);
            }
		}

		/// <summary>
		/// Draw a frame and all its child and sibling frames in NORMAL display mode
		/// </summary>
		/// <param name="frame">Frame to draw</param>
		/// <param name="lod"> dist from camera </param>
		private void RecursiveDraw(JointFrame frame, float lod)
		{
			// Render this frame's mesh
			Cytoplasm cytoplasm = (Cytoplasm)frame.MeshContainer;							// get first Cytoplasm mesh in this frame
			while(cytoplasm != null)
			{
                // if this frame has colour animation, copy the instanced material from the frame to the cytoplasm
                if (frame.animColour != null)
                    cytoplasm.materials = frame.animColour;

				cytoplasm.Render(frame, lod);												// draw it (textured)
				cytoplasm = (Cytoplasm)cytoplasm.NextContainer;								// and repeat for any other meshes in frame
			}

			// Render any siblings
			if (frame.Sibling != null)
			{
				RecursiveDraw(frame.Sibling,lod);
			}

			// Render children and their siblings
			if (frame.FirstChild != null)
			{
				RecursiveDraw(frame.FirstChild,lod);
			}
		}

		/// <summary>
		/// Draw a frame and all its child and sibling frames in SCANNER mode,
		/// in which the material luminance (or channel luminance) depends on the level of a chemical
		/// </summary>
		/// <param name="lod">Dist from camera</param>
		private void DrawScannerMode(float lod)
		{
			// only the selected organism is displayed in scanner mode
            if (Lab.SelectedOrg == this.owner)
            {
                RecursiveDrawScannerMode(rootFrame, lod);
            }
            else
            {
                RecursiveDraw(rootFrame, lod);
            }
		}


		/// <summary>
		/// Recursively draw the local chemical concentration (in channels with the right affinity)
        /// For the Core cell, show the global chemical concs in the func0 to func5 organelles
		/// </summary>
		/// <param name="frame">frame to draw</param>
		/// <param name="lod">level of detail</param>
		private void RecursiveDrawScannerMode(JointFrame frame, float lod)
		{
            int chem = 0;
            float brightness = 0;
            Color col = Color.White;

            try
            {

                // Render this frame's mesh
                Cytoplasm cytoplasm = (Cytoplasm)frame.MeshContainer;							// get first Cytoplasm mesh in this frame
                while (cytoplasm != null)														// for each mesh...
                {
                    switch (frame.Type)
                    {
                        // If this is a channel, render it in a colour determined by its chemical preference
                        // and concentration
                        case JointFrame.FrameType.Channel:
                            chem = channel[frame.Index].chemical;		    				// the chemical number for this channel
                            col = Chemistry.Colour[chem];		    						// the base colour of that chemical
                            brightness = GetChannel(frame.Index);	        				// modulate by signal in that channel
                            if (brightness == 0) brightness = 0.01f;						// can't use black, or we'll render using texture
                            Debug.Assert(brightness >= 0f, "Chemical conc in cell " + fullname + " channel " + frame.Index + " is < 0");
                            Debug.Assert(brightness <= 1.0f, "Chemical conc in cell " + fullname + " channel " + frame.Index + " is > 1");
                            col = Color.FromArgb((int)(col.R * brightness),
                                              (int)(col.G * brightness),
                                              (int)(col.B * brightness));		    		// adjust intensity
                            cytoplasm.Render(frame, lod, col, col);
                            break;

                        // If this is a functional block, render it in yellow
                        // UNLESS this is the Core cell, in which case we should colour its six func# meshes
                        // to show the levels of the global chemicals
                        case JointFrame.FrameType.Function:
                            if (name == "Core")
                            {
                                chem = frame.Index + Chemistry.NUMSIGNALS;		    			// the global chemical number for this func block
                                col = Chemistry.Colour[chem];	    							// the base colour of that chemical
                                brightness = owner.chemistry.Read(chem);	        		    // modulate by signal in that channel
                                if (brightness == 0) brightness = 0.01f;						// can't use black, or we'll render using texture
                                col = Color.FromArgb((int)(col.R * brightness),
                                                  (int)(col.G * brightness),
                                                  (int)(col.B * brightness));					// adjust intensity
                                cytoplasm.Render(frame, lod, col, col);
                            }
                            // For non-Core cells, render the functional blocks in yellow
                            else
                            {
                                cytoplasm.Render(frame, lod, Color.Yellow, Color.Yellow);
                            }
                            break;

                        // Cell membrane is rendered in wireframe, so that we can see organelles
                        case JointFrame.FrameType.General:
                        case JointFrame.FrameType.Animating:
                            Engine.Device.RenderState.FillMode = FillMode.WireFrame;
                            cytoplasm.Render(frame, lod, Color.Gray, Color.Gray);
                            Engine.Device.RenderState.FillMode = FillMode.Solid;
                            break;
                    }

                    // and repeat for any other meshes in frame
                    cytoplasm = (Cytoplasm)cytoplasm.NextContainer;
                }
            }
            catch (Exception e)
            {
                throw new SDKException("Error: Unable to render chemical concentrations for celltype " + fullname + " chemical# " + chem + " concentration " + brightness,e);
            }

			// Render any siblings
			if (frame.Sibling != null)
			{
				RecursiveDrawScannerMode(frame.Sibling, lod);
			}

			// Render children and their siblings
			if (frame.FirstChild != null)
			{
				RecursiveDrawScannerMode(frame.FirstChild, lod);
			}
		}

		/// <summary>
		/// Draw a frame and all its child and sibling frames in CELL DESIGN mode,
		/// in which the material luminance depends on whether the cell is the one being edited
		/// </summary>
		/// <param name="lod"> dist from camera </param>
		private void DrawCellMode(float lod)
		{
			// draw using materials according to whether this cell is the selected cell,
			// is a cell in an unselected organism, or whatever
			if (Lab.SelectedCell == this)													// the cell that is selected for editing
			{
				RecursiveDrawCellMode(rootFrame, lod, Cytoplasm.DesignStyle.Selected);
			}
			else if ((Lab.SelectedCell != null) && (Lab.SelectedOrg == this.owner))			// a non-edit cell in the selected organism
			{
				RecursiveDrawCellMode(rootFrame, lod, Cytoplasm.DesignStyle.Unselected);
			}
			else																			// any cell in a different organism (draw normally)
			{
				RecursiveDraw(rootFrame, lod);
			}
		}

		/// <summary>
		/// Recursively draw in cell edit mode
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="lod"></param>
		/// <param name="style"></param>
		private void RecursiveDrawCellMode(JointFrame frame, float lod, Cytoplasm.DesignStyle style)
		{
			// Render this frame's mesh
			Cytoplasm cytoplasm = (Cytoplasm)frame.MeshContainer;							// get first Cytoplasm mesh in this frame
			while (cytoplasm != null)														// for each mesh...
			{
				switch (frame.Type)
				{
					// If this is the cell membrane, render it in a colour determined by whether it is selected or not
					case JointFrame.FrameType.General:
					case JointFrame.FrameType.Animating:
						if (style == Cytoplasm.DesignStyle.Selected)
							cytoplasm.Render(frame, lod, Color.Blue, Color.Black);
						else
							cytoplasm.Render(frame, lod, Color.White, Color.Black);
						break;

					// If this is a socket, render it in a colour determined by whether it is selected or not
					case JointFrame.FrameType.Socket:
						if (frame == Lab.SelectedSocket)
							cytoplasm.Render(frame, lod, Color.Red, Color.Red);
						else
							cytoplasm.Render(frame, lod, Color.White, Color.White);
						break;

					// All other parts are unrendered
				}

				// and repeat for any other meshes in frame
				cytoplasm = (Cytoplasm)cytoplasm.NextContainer;
			}

			// Render any siblings
			if (frame.Sibling != null)
			{
				RecursiveDrawCellMode(frame.Sibling, lod, style);
			}

			// Render children and their siblings
			if (frame.FirstChild != null)
			{
				RecursiveDrawCellMode(frame.FirstChild, lod, style);
			}
		}

		/// <summary>
		/// Draw a frame and all its child and sibling frames in CHANNEL DESIGN mode,
		/// in which the cell walls are wireframes and the channels are visible
		/// </summary>
		/// <param name="lod"> dist from camera </param>
		private void DrawChannelMode(float lod)
		{
			// draw using materials according to whether this cell is the selected cell,
			// is a cell in an unselected organism, or whatever
			if (Lab.SelectedCell == this)													// the cell that is selected for editing (red wireframe)
			{
				RecursiveDrawChannelMode(rootFrame, lod, Cytoplasm.DesignStyle.Selected);
			}
			else if ((Lab.SelectedCell != null) && (Lab.SelectedOrg == this.owner))			// a non-edit cell in the selected organism (green wireframe)
			{
				RecursiveDrawChannelMode(rootFrame, lod, Cytoplasm.DesignStyle.Unselected);
			}
			else																			// any cell in a different organism (draw normally)
			{
				RecursiveDraw(rootFrame, lod);
			}
		}
		
		/// <summary>
		/// Recursively draw in channel-edit mode
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="lod"></param>
		/// <param name="style"></param>
		private void RecursiveDrawChannelMode(JointFrame frame, float lod, Cytoplasm.DesignStyle style)
		{
            try
            {
                // Render this frame's mesh
                Cytoplasm cytoplasm = (Cytoplasm)frame.MeshContainer;							// get first Cytoplasm mesh in this frame
                while (cytoplasm != null)														// for each mesh...
                {
                    switch (frame.Type)
                    {
                        // If this is a socket, render it in a colour determined by whether it is selected or not
                        case JointFrame.FrameType.Socket:
                            if (frame == Lab.SelectedSocket)
                                cytoplasm.Render(frame, lod, Color.Red, Color.Red);				// selected socket
                            else if (style != Cytoplasm.DesignStyle.Other)
                                cytoplasm.Render(frame, lod, Color.White, Color.White);			// unselected sockets on selected organism
                            break;

                        // If this is a channel, render it in a colour determined by its chemical preference
                        // And flash it if it is being edited
                        case JointFrame.FrameType.Channel:

                            int chem = channel[frame.Index].chemical;							// the chemical number for this channel
                            Color c = Chemistry.Colour[chem];									// the colour of that chemical
                            if ((this == Lab.SelectedCell) && (Lab.SelectedChannel == frame.Index))	// if we are the channel selected for editing
                            {
                                float time = Scene.TotalElapsedTime % 0.3f;						// get current fraction of a second
                                if (time > 0.15f)												// flash 1:1
                                {
                                    //c = Color.DarkGray;
                                    c = Color.FromArgb(c.R / 3, c.G / 3, c.B / 3);				// flash between bright and dark versions
                                }
                            }
                            cytoplasm.Render(frame, lod, Color.FromArgb(1,1,1), c);             // Use only emissive for clarity. Diffuse is dark (mustn't be black - means ignore)
                            break;

                        // If this is a functional block, render it in yellow
                        case JointFrame.FrameType.Function:
                            cytoplasm.Render(frame, lod, Color.Yellow, Color.Yellow);
                            break;

                        // Cell membrane is rendered in wireframe on a selected creature, so that we can see organelles
                        case JointFrame.FrameType.General:
                        case JointFrame.FrameType.Animating:
                            Engine.Device.RenderState.FillMode = FillMode.WireFrame;
                            if (style == Cytoplasm.DesignStyle.Selected)
                                cytoplasm.Render(frame, lod, Color.DarkRed, Color.DarkRed);
                            else
                                cytoplasm.Render(frame, lod, Color.DarkGreen, Color.DarkGreen);
                            Engine.Device.RenderState.FillMode = FillMode.Solid;
                            break;
                    }

                    // and repeat for any other meshes in frame
                    cytoplasm = (Cytoplasm)cytoplasm.NextContainer;
                }
            }            
            catch (Exception e)
            {
                throw new SDKException("ERROR: Unable to draw organelle " + frame.Name + " in cell " + name, e);
            }

            // Render any siblings
            if (frame.Sibling != null)
            {
                RecursiveDrawChannelMode(frame.Sibling, lod, style);
            }

            // Render children and their siblings
            if (frame.FirstChild != null)
            {
                RecursiveDrawChannelMode(frame.FirstChild, lod, style);
            }
            
		}



		

		#endregion


		#region ------- misc ----------

		/// <summary>
		/// Send the tree of frame matrices out to the debug stream
		/// </summary>
		public void DebugFrameMatrices()
		{
			JointFrame.DebugHierarchy(this.rootFrame, 0);
		}

		/// <summary>
		/// Set the visibility level of all meshes of a given type ("SKT", "EFF", etc.)
		/// for ALL cells in the system
		/// </summary>
		/// <param name="meshname">root part of the FRAME name used to designate a part as being an effector/socket/etc</param>
		/// <param name="vis">true to make parts of this type visible</param>
		public static void SetVisibility(JointFrame.FrameType type, bool vis)
		{
			for (int i=0; i<Cell.library.Count; i++)							// apply to every cell in the library
			{
				Cell c = (Cell)library.Values[i];
				c.RecursiveSetVisibility(c.rootFrame, type, vis);
			}
		}
		private void RecursiveSetVisibility(JointFrame frame, JointFrame.FrameType type, bool vis)
		{
			if (frame.Type==type)												// if we're the right type of frame
				if (frame.MeshContainer!=null)									// and there's a mesh
					((Cytoplasm)frame.MeshContainer).Visible = vis;				// set its visibility

			if (frame.Sibling != null)											// recurse through siblings
				RecursiveSetVisibility(frame.Sibling, type, vis);
			if (frame.FirstChild != null)										// recurse through children
				RecursiveSetVisibility(frame.FirstChild, type, vis);
		}

		/// <summary>
		/// Normally all organelles are invisible, so we don't want to waste time rendering them.
		/// This method sets their .Visible flag to false
		/// </summary>
		private void HideOrganelles()
		{
            RecursiveSetVisibility(rootFrame, JointFrame.FrameType.Channel, false);
            RecursiveSetVisibility(rootFrame, JointFrame.FrameType.Function, false);
			RecursiveSetVisibility(rootFrame, JointFrame.FrameType.Hotspot, false);
			RecursiveSetVisibility(rootFrame, JointFrame.FrameType.Socket, false);
		}


		/// <summary>
		/// See if our cell type will accept the role of camera mount. (See Physiology.AssignCamera() for info).
		/// </summary>
		/// <param name="currentOwner">The cell that currently has the camera</param>
		/// <param name="currentHotspot">The hotspot that currently has the camera</param>
		/// <returns>The index of the hotspot to use, or -1 if our cell type isn't a valid camera mount</returns>
		public int AssignCamera(IControllable currentOwner, int currentHotspot)
		{
			return physiology.AssignCamera(currentOwner, currentHotspot);
		}

		/// <summary>
		/// Helper for Organism.CollisionTest(). Looks for a collision between two cells,
		/// starting at the cell level then descending to the mesh level if required
		/// </summary>
		/// <param name="our"></param>
		/// <param name="his"></param>
		/// <returns></returns>
		public static bool CollisionTest(Cell our, Cell his)
		{
			// Do our spheres overlap? If not, we can't possibly collide
			if (Vector3.LengthSq(his.Location - our.Location) >
				(his.AbsSphere.Radius + our.AbsSphere.Radius) *
				(his.AbsSphere.Radius + our.AbsSphere.Radius))
				return false;

			// Our spheres WILL intersect. So, for each mesh in both cells, test to see if they will actually collide

			foreach (JointFrame ourMesh in our.collisionFrames)
			{
				foreach (JointFrame hisMesh in his.collisionFrames)
				{
					bool result = Cytoplasm.CollisionTest(ourMesh, hisMesh);
					if (result == true)
						return true;
				}
				
			}
			return false;
		}

		/// <summary>
		/// Helper for Terrain.CollisionTest(). Looks for a collision between this cell
		/// and a terrain tile
		/// </summary>
		/// <param name="tile">The triangle strip containing the tile's vertices</param>
		/// <returns>true if there's a collision</returns>
		public bool CollisionTest(BinormalVertex[] tile)
		{
			// Compare the bounding box of each collidable mesh with the tile
			foreach (JointFrame mesh in collisionFrames)
			{
				// Get the transformed bounding box for that mesh
				Vector3[] points = ((Cytoplasm)mesh.MeshContainer).GetTransformedOBB(mesh.CombinedMatrix);

				// test each point
				foreach (Vector3 point in points)
				{
					// if the point is above the tile...
					if ((point.X >= tile[0].Position.X) && (point.X <= tile[1].Position.X)
						&& (point.Z >= tile[2].Position.Z) && (point.Z <= tile[0].Position.Z))
					{
						// interpolate the height of the tile at that point (see AltitudeAt() for info)
						float dx = (point.X - tile[2].Position.X) / (tile[1].Position.X - tile[0].Position.X);
						float dy = (point.Z - tile[2].Position.Z) / (tile[0].Position.Z - tile[2].Position.Z);

						float height = tile[2].Position.Y + dx * (tile[3].Position.Y - tile[2].Position.Y)
								+ dy * (tile[0].Position.Y - tile[2].Position.Y)
								+ dx * dy * (tile[2].Position.Y - tile[3].Position.Y - tile[0].Position.Y + tile[1].Position.Y);

						// If the point is below this height, it is inside the tile
						if (point.Y < height)
							return true;					
					}
				}
			}
			return false;
		}




		#endregion


		#region ------------------ debugging ---------------------
		/// <summary>
		/// Conditional methods to show bounding sphere using a marker
		/// </summary>
		/// <summary> TEMPORARY marker to show bounding sphere </summary>
		private Marker boundMarker = null;

		[Conditional("SHOWBOUNDS")]
		private void CreateBoundMarker()
		{
			boundMarker = Marker.CreateSphere(System.Drawing.Color.FromArgb(32,System.Drawing.Color.Blue),
												AbsSphere.Centre, AbsSphere.Radius);
		}

		[Conditional("SHOWBOUNDS")]
		private void UpdateBoundMarker()
		{
			boundMarker.Goto(AbsSphere.Centre);
		}

		#endregion
	

		
		#region ----------- IDetectable members (sensory requests) -------------------
	
		/// <summary> Return absolute location </summary>
		public Vector3 ReqLocation()
		{
			return Location;
		}

		/// <summary> Return absolute velocity as a vector </summary>
		public Vector3 ReqVelocity()
		{
			return Location - OldLocation;
		}

		/// <summary> Total mass </summary>
		public float ReqMass()
		{
            return physiology.Mass * owner.scale;
		}

		/// <summary> Total dimensions as a sphere radius </summary>
		public float ReqSize()
		{
			return RelSphere.Radius;
		}

        /// <summary> Return the cell's dominant colour - i.e. the first material found in the mesh hierarchy </summary>
        /// <returns></returns>
        public List<ColorValue> ReqSpectrum()
        {
            // TODO: if first mesh frame is anim# then load current animation colour instead
            List<ColorValue> colours = new List<ColorValue>();
            colours.Add(colour);
            return colours;
        }

 

        /// <summary> Return my depth as a fraction (0=surface, 1=deepest) </summary>
        /// <returns></returns>
        public float ReqDepth()
        {
            return (Water.WATERLEVEL - Location.Y) / Water.WATERLEVEL;
        }


		/// <summary> We've been sent a Stimulus. 
		/// Handle it if possible, or hand it on to our physiology for handling</summary>
		/// <param name="stimulus">The stimulus information</param>
		/// <returns>Return true if the stimulus was handled</returns>
		public bool ReceiveStimulus(Stimulus stim)
		{
			// Stimuli that should be handled by ALL cells in a receiving organism go here...
			switch (stim.Type)
			{
					// TODO: Add universal stimulus cases here, then return true
				case "dummy":
					return true;

			}

			// If that doesn't work, test to see if this cell is actually in range of the sender,
			// and attempt to handle such stimuli (e.g. being stung or eaten)
			if (AbsSphere.IsPenetrating(new Sphere(stim.TransmitterLocn, stim.TransmissionRange))==false)
				return false;

			// Stimuli that should be handled by cells directly affected go here...
			switch (stim.Type)
			{
					// TODO: Add stimulus cases here, then return true
				case "dummy":
					return true;

			}

			// If that doesn't work, pass the stimulus on to our Physiology to see if our cell type
			// can handle it. This allows the stimulus types to be extended
			return (physiology.ReceiveStimulus(stim));
		}

		#endregion

		#region ----------- IControllable Members (enables Physiology to control Cell without whole cell class being public) --------


		/// <summary> 
		/// Return a list of all the objects of a given type in range of a given hotspot,
		/// as well as their relative angle and distance.
		/// Cell types use this for implementing active sensors and also effectors (Stimulus sources)
		/// </summary>
		/// <param name="spot">Which hotspot to use (-1 = no hotspot, e.g. entire cell is the emitter or sensor. Avoid when angle of acceptance matters)</param>
		/// <param name="range">Current range of hotspot</param>
		/// <param name="includeOrganisms">Set true to include organisms in the list</param>
		/// <param name="includeTerrain">Set true to include tiles in the list</param>
		/// <param name="lineOfSightOnly">Set true to exclude objects obscured by terrain</param>
		/// <returns>An array of SensorItems, containing the data</returns>
		public SensorItem[] GetObjectsInRange(int spot, float range,
											   bool includeOrganisms, bool includeTerrain,
											   bool lineOfSightOnly)
		{
            // if an effector hotspot was specified, use its location
            JointFrame sensor = null;
            if (spot >= 0)
            {
                // get the hotspot frame
                try
                {
                    sensor = hotspot[spot];
                }
                catch
                {
                    // Any error is probably because we forgot to define this hotspot in Truespace
                    throw new SDKException("Cell.GetObjectsInRange() error: Cell " + this.fullname + " is trying to access missing hotspot " + spot);
                }
            }
            // if a hotspot wasn't specified, use the centre of the cell (orientation of the cell is presumed to be the orientation of the effector)
            else
            {
                sensor = rootFrame;
            }

			// Get the location from its matrix
			Vector3 centre = new Vector3(sensor.CombinedMatrix.M41,
									sensor.CombinedMatrix.M42,
									sensor.CombinedMatrix.M43);

			// Create a matrix that will rotate objects into a sensor-relative frame
			Matrix transform = sensor.CombinedMatrix;
			transform.Invert();

			// Get a list of IDetectable objects that fall anywhere within range of the hotspot
			List<IDetectable> candidate = Map.GetObjectsWithinRange(centre, range, includeOrganisms,
															includeTerrain, lineOfSightOnly, this.owner);

			// From this, create an array of SensorItems, including relative bearing etc..
			SensorItem[] item = new SensorItem[candidate.Count];					// where to store the results

			// Process each target
			for (int i = 0; i < candidate.Count; i++)
			{
				IDetectable target = (IDetectable)candidate[i];						// get the target object
				item[i].Object = target;											// and store it

				// convert the target into sensor-relative coordinates, in case the cell type wants to 
				// know its angle or orientation (don't waste time calculating these now)
				//item[i].RelativePosition = target.ReqLocation() - centre;
				item[i].RelativePosition = Vector3.TransformCoordinate(target.ReqLocation(), transform);
			}

			return item;
		}

        /// <summary>
        /// Given a stimulus, find out how that object is positioned relative to a given hotspot on this cell. 
        /// Cell types use this to establish how much notice they should take of an incoming stimulus (passive sensing). 
        /// Call methods on the returned SensorItem to calculate the angle and/or distance as required.
        /// </summary>
        /// <param name="spot">Which hotspot to use</param>
        /// <param name="range">Current range of hotspot</param>
        /// <param name="stim">the stimulus we received</param>
        public SensorItem TestStimulusVisibility(int spot, float range, Stimulus stim)
		{
			// Get a matrix that will convert target location into hotspot-relative coordinates
			// Create a matrix that will rotate objects into a sensor-relative frame
			Matrix transform = hotspot[spot].CombinedMatrix;
			transform.Invert();

			// transform the source position into sensor-relative coordinates
			Vector3 relpos = Vector3.TransformCoordinate(stim.TransmitterLocn, transform);

            // Return a SensorItem with this info (the SensorItem.Object refers to the sender)
			return new SensorItem(stim.From, relpos);
		}

		/// <summary>
		/// Apply a positive or negative propulsive force in the direction of a hotspot's normal
		/// </summary>
		/// <param name="spot">Index of the hotspot acting as the effector</param>
		/// <param name="amount">Signed value of the force to apply</param>
		public void JetPropulsion(int spot, float amount)
		{
            amount *= Scene.ElapsedTime;                                            // modulate force by the time it has been acting
            if (amount > 10f) amount = 10f;                                         // keep within sensible limits
            else if (amount < -10f) amount = -10f;
			Vector3 force = new Vector3(0,0,-amount);		                        // vector of the force 
            Matrix transform = this.GetHotspotNormalMatrix(spot);   				// we want to rotate it to same orientation as hotspot
			transform.M41 = transform.M42 = transform.M43 = 0;						// but we don't want to translate it
			propulsionForce += Vector3.TransformCoordinate(force,transform);
		}

		/// <summary>
		/// Send a debug message out to the display console (can't access Engine directly from Physiology)
		/// </summary>
		/// <param name="msg"></param>
		public void ConsoleMessage(string msg)
		{
			Debug.WriteLine(msg);
		}

		/// <summary>
		/// Fetch the socket number on my parent to which I'm attached.
		/// Used internally for sending/receiving nerve signals. Not relevant for SDK use.
		/// </summary>
		/// <returns></returns>
		public int MySocketNumber()
		{
			return parentSocket.Index;
		}

        /// <summary>
        /// Read cell input from a channel (chemical concentration or user-definable constant)
        /// </summary>
        /// <param name="chan">absolute channel number (must be an input channel)</param>
        public float GetChannel(int chan)
        {
            try
            {
                if (channel[chan].chemical == 0)                                            // if no chemical affinity, return the user-defined constant
                    return channel[chan].constant;
                return channel[chan].signal;
            }
            catch
            {
                throw new SDKException("Unable to find channel " + chan + " in mesh for Cell " + name);
            }
        }

        /// <summary>
		/// Write cell output to a channel
		/// </summary>
		/// <param name="chan">absolute channel number (must be an output channel)</param>
		/// <param name="value"></param>
		public void SetChannel(int chan, float value)
		{
			if (channel[chan].chemical == 0)											// if the channel is unused, be sure to write a zero to it
				value = 0;
            if (value < 0f) value = 0;                                                  // clamp to range
            else if (value > 1f) value = 1f;
			channel[chan].signal = value;
		}

        /// <summary>
        /// Get a channel's constant value
        /// </summary>
        /// <param name="chan">absolute channel number</param>
        /// <param name="value"></param>
        public float GetChannelConstant(int chan)
        {
            return channel[chan].constant;
        }

        /// <summary>
        /// Set a channel's constant value
        /// </summary>
        /// <param name="chan">absolute channel number</param>
        /// <param name="value"></param>
        public void SetChannelConstant(int chan, float value)
        {
            channel[chan].constant = value;
        }

        public Channel Channel(int chan)
        {
            return channel[chan];
        }
        
		/// <summary>
		/// Read a channel's name (for LCD display in lab)
		/// </summary>
		/// <param name="chan"></param>
		public string GetChannelInfo(int chan)
		{
			ChannelData[] data = Physiology.GetChannelData();
			try
			{
				string direction = " (bypass) = ";
				if (data[chan].IsInput())
					direction = " (input) = ";
				else if (data[chan].IsOutput())
					direction = " (output) = ";
				string chem = Chemistry.Name[channel[chan].chemical];
				return data[chan].Name + direction + chem;
			}
			catch		// if no such channel exists, report this sensibly
			{
				return "<none>";
			}
		}

 

        /// <summary>
        /// Set the (sole) material colour of a given anim# frame. Used for colour animations such as blushing or pulsing.
        /// Colour animateable meshes must be defined as anim0... but needn't physically animate.
        /// HOWEVER, any anim# frames in the object MUST still have two keyframes, even if they don't move. 
        /// Otherwise the motioncontroller will move them to 0,0,0.
        /// </summary>
        /// <param name="index">index of frame in joint[] array</param>
        /// <param name="diffuse">diffuse colour</param>
        /// <param name="emissive">emissive colour</param>
        public void SetAnimColour(int index, ColorValue diffuse, ColorValue emissive)
        {
            try
            {
                if (joint[index].animColour == null)
                    joint[index].animColour = new Material[1];
                joint[index].animColour[0].EmissiveColor = emissive;
                joint[index].animColour[0].DiffuseColor = diffuse;
                joint[index].animColour[0].AmbientColor = new ColorValue(0.2f,0.2f,0.2f);
                joint[index].animColour[0].SpecularColor = new ColorValue(1f, 1f, 1f); ;

            }
            catch (Exception e)
            {
                throw new SDKException("Error: Attempting to animate the colour of a non-existent anim mesh (anim" + index + ") in cell "+name, e);
            }
        }

        /// <summary>
        /// Return our variant of the celltype
        /// </summary>
        public int Variant
        {
            get { return variant; }
        }

        /// <summary>
        /// The unique name of the organism owning us (for debug messages, etc.)
        /// </summary>
        /// <returns></returns>
        public string OwnerOrganism()
        {
            return owner.Name + ":"+ owner.Instance.ToString();
        }

        /// <summary>
        /// Return a unique identifier for this cell instance. Useful for debugging.
        /// </summary>
        /// <returns></returns>
        public string UniqueName()
        {
            return fullname + ":" + instance;
        }

        /// <summary>
        /// If this cell is part of a cameraship organism, return the index of the currently active panel.
        /// This allows Physiology classes to select the right camera mount when a cameraship offers several panels, each with a different view into the scene
        /// (such as the front porthole and observation bubble in the sub).
        /// </summary>
        /// <returns>the currently active panel # if this cell belongs to a cameraship, or -1 if it doesn't</returns>
        public int CurrentPanel()
        {
            if (owner is CameraShip)
                return ((CameraShip)owner).CurrentPanel;
            return -1;
        }

        /// <summary>
        /// Create a Marker cone for showing a sensor (or stimulus-generating) celltype's receptive field. 
        /// Used for debugging, so that we know when another object is within sight.
        /// Call this during the celltype's FastUpdate() method.
        /// </summary>
        /// <param name="cone">Up to two markers can be created. Use an index of 0 or 1 here to define which is being built</param>
        /// <param name="spot">sensor hotspot #</param>
        /// <param name="range">sensor range</param>
        /// <param name="halfAngle">divergence in radians from the hotspot normal</param>
        public void ShowSensoryField(int cone, int spot, float range, float halfAngle)
        {
            Debug.Assert(halfAngle < (float)Math.PI / 2, "Can't display cones with >=90-degree half angles, cos base would be infinite!");
            Debug.Assert(cone < 2, "Only two cones allowed");

            if (sensoryField == null)                                                   // Only create a Marker list for cells that actually want one, to save space
                sensoryField = new Marker[2];
            if (sensoryField[cone] == null)                                             // if this marker doesn't exist, create it
            {
                sensoryField[cone] = Marker.CreateCone(Color.DarkBlue, new Vector3(), new Orientation(), halfAngle, range);
            }
            else                                                                        // if it already exists, update its potition/orientation
            {
                sensoryField[cone].Goto(this.hotspot[spot].CombinedMatrix);
            }

        }

        /// <summary>
        /// Return a matrix that represents the position and orientation of a hotspot's FACE NORMAL
        /// (i.e. the direction the hotspot is looking). Used for positioning the camera, etc.
        /// DirectX treats Z as the normal direction, but most art packages use Y
        /// </summary>
        /// <param name="spot">Index of the hotspot to examine</param>
        /// <returns>A matrix positioned at the hotspot and oriented facing along its normal</returns>
        public Matrix GetHotspotNormalMatrix(int spot)
        {
            // We need to rotate the hotspot's frame so that we're looking along the normal, not the y-axis
            return Matrix.RotationYawPitchRoll(0, -(float)Math.PI / 2.0f, (float)Math.PI)
                                * hotspot[spot].CombinedMatrix;
        }

        /// <summary>
        /// Return a matrix that represents the world position and orientation of a hotspot (NOT its normal)
        /// </summary>
        /// <param name="spot">Index of the hotspot to examine</param>
        /// <returns>The hotspot's combined matrix</returns>
        public Matrix GetHotspotMatrix(int spot)
        {
            return hotspot[spot].CombinedMatrix;
        }

        /// <summary>
        /// Return the world coordinates of a hotspot
        /// </summary>
        /// <param name="spot"></param>
        /// <returns></returns>
        public Vector3 GetHotspotLocation(int spot)
        {
            return new Vector3(hotspot[spot].CombinedMatrix.M41,
                hotspot[spot].CombinedMatrix.M42,
                hotspot[spot].CombinedMatrix.M43);
        }






		#endregion // IControllable members







		/// <summary>
		/// Return a matrix that represents the position and orientation of a socket (NOT its normal)
		/// </summary>
		/// <param name="spot">Index of the hotspot to examine</param>
		/// <returns>The hotspot's combined matrix</returns>
		public Matrix GetSocketMatrix(int skt)
		{
			return socket[skt].CombinedMatrix;
		}

        /// <summary>
        /// Return a matrix that represents the position and orientation of the plug (NOT its normal)
        /// </summary>
        /// <returns>The root frame's combined matrix</returns>
        public Matrix GetPlugMatrix()
        {
            return rootFrame.CombinedMatrix;
        }

		/// <summary>
		/// Return a frame that represents the position and orientation of a socket (NOT its normal)
		/// </summary>
		/// <param name="spot">Index of the hotspot to examine</param>
		/// <returns>The hotspot's combined matrix</returns>
		public JointFrame GetSocketFrame(int skt)
		{
			return socket[skt];
		}

		/// <summary>
		/// Recursively convert the frame hierarchy into a flat list of frames for 
		/// easier or more powerful searching, e.g. during 3D picking.
		/// </summary>
		/// <param name="list"></param>
		/// <returns></returns>
		private List<JointFrame> FlattenFrames(JointFrame frame, List<JointFrame> list)
		{
			list.Add(frame);
			if (frame.Sibling!=null)
				FlattenFrames(frame.Sibling, list);
			if (frame.FirstChild!=null)
				FlattenFrames(frame.FirstChild, list);
			return list;
		}

		/// <summary>
		/// Recursively create a list of frames that contain meshes. 
		/// Useful for e.g. collision detection.
		/// </summary>
		/// <param name="frame">Current frame in the recursion</param>
		/// <param name="list">Where to store the results</param>
		/// <param name="includeSpots">False if we only want major meshes, not hotspots, sockets or channels</param>
		/// <returns></returns>
		private List<JointFrame> GetMeshFrames(JointFrame frame, List<JointFrame> list, bool includeSpots)
		{
			if (frame.MeshContainer != null)											// if we have a mesh
			{
				if ((includeSpots==true)
					||(frame.Type==JointFrame.FrameType.General)
					||(frame.Type==JointFrame.FrameType.Animating))						// optionally exclude hotspots & sockets
				{
					list.Add(frame);										
				}
			}
			if (frame.Sibling != null)
				GetMeshFrames(frame.Sibling, list, includeSpots);
			if (frame.FirstChild != null)
				GetMeshFrames(frame.FirstChild, list, includeSpots);
			return list;
		}



		/// <summary>
		/// Helper for mouse selection of 3D objects. Given a "finger" pointing into the screen,
		/// return non-null if the finger points to this cell. The method favours sockets over
		/// other meshes in the cell and the returned JointFrame can be used
		/// to set the Cell.SelectedSocket variable if the user has clicked over a socket.
		/// Called by Organism.MousePick()
		/// </summary>
		/// <param name="rayPosition"></param>
		/// <param name="rayDirection"></param>
		/// <returns>The frame containing the mesh that was pointed at, or null if this cell isn't the one.
		///			 The frame can later be used to detemine whether the user clicked on a socket or other named type</returns>
		public JointFrame MousePick(Vector3 rayPosition, Vector3 rayDirection)
		{
			// Get a flat list of the frames, so that I can preferentially search for sockets
			List<JointFrame> list = new List<JointFrame>();
			FlattenFrames(rootFrame, list);

			// scan the list for any picked SOCKET first
			foreach (JointFrame frame in list)
			{
				if (frame.Type == JointFrame.FrameType.Socket)
				{
					bool result = MousePick(frame, rayPosition, rayDirection);					// Check for mesh intersection
					if (result == true)															// if this mesh is pointed at
					{
						return frame;															// return the frame that was clicked
					}
				}
			}

			// if that didn't work, scan for any other kind of mesh
			foreach (JointFrame frame in list)
			{
				if (frame.Type != JointFrame.FrameType.Socket)
				{
					bool result = MousePick(frame, rayPosition, rayDirection);					// Check for mesh intersection
					if (result == true)															// if this mesh is pointed at
					{
						return frame;															// return the frame that was clicked
					}
				}
			}

			// No luck - this cell isn't being selected
			return null;
		}
		// Helper for above
		private bool MousePick(JointFrame frame, Vector3 rayPosition, Vector3 rayDirection)
		{
			// Skip frames that don't have meshes
			if (frame.MeshContainer == null)
				return false;

			// Unproject the ray and its normal into model space
			Matrix unproject = Matrix.Invert(frame.CombinedMatrix);
			Vector3 localPosn = Vector3.TransformCoordinate(rayPosition, unproject);
			Vector3 localDirn = Vector3.TransformNormal(rayDirection, unproject);
			localDirn.Normalize();

			// Do the intersection test
			return ((Cytoplasm)frame.MeshContainer).OriginalMesh.Intersect(localPosn, localDirn);
		}




		#region ------------------ editing functions ---------------------------

		/// <summary>
		/// Selection change: Select my first (free) socket (or null if none)
		/// </summary>
		public void SelectFirst()
		{
			if (socket.Length > 0)														// if the cell has some sockets...
			{
				SelectFirstFreeSocket(0);												// select socket 0 or the first free one after that
			}
			else
				Lab.SelectedSocket = null;												// if no sockets, we can't select any
		}

		/// <summary>
		/// Selection change: Select the next (free) socket
		/// </summary>
		public void SelectNext()
		{
			if (socket.Length > 0)
			{
				int s;
				for (s = 0; s < socket.Length; s++)										// find the current socket #
				{
					if (socket[s] == Lab.SelectedSocket)
						break;
				}
				if (s >= socket.Length)													// if we didn't find it (might be null)
					return;																// give up
				SelectFirstFreeSocket(s + 1);											// else find the next free socket after the current one
			}
		}

		/// <summary>
		/// Select the first socket to which no cell is attached, starting with the given skt and wrapping if necessary
		/// </summary>
		/// <param name="index">The first socket to try</param>
		private void SelectFirstFreeSocket(int index)
		{
			if (socket.Length == 0)														// if there are no sockets, we can't find one
			{
				Lab.SelectedSocket = null;
				return;
			}

			if (index >= socket.Length)													// wrap if we've gone off the end of the list
				index = 0;

			for (int i = 0; i < socket.Length; i++)										// we only want to look at each socket once
			{
				Lab.SelectedSocket = socket[index];										// try the given socket
				if (owner.SelectedSocketIsOccupied() == false)							// if it was free, we're done
					return;
				if (++index >= socket.Length)											// if not free, move to the next socket
					index = 0;															// wrapping if necessary and keep on trying 
			}																			// until one is free or we've tried them all
			Lab.SelectedSocket = null;													// if they're all occupied, none can be selected
		}

		/// <summary>
		/// Given the gene that has/would be used to create this cell, update its parameters
		/// to reflect the current wiring setup, etc. Then recurse through children and siblings
		/// until the whole tree has been constructed
		/// </summary>
		/// <param name="gene"></param>
		public void UpdateGene(Gene gene)
		{
			// The gene type is our cell type, which we get from ToString()
			gene.Type = physiology.ToString();

			// Create a new list of channels from the user-defined wiring
			gene.Channels = new List<Channel>();
			gene.Channels.AddRange(channel);

			// Get our current plug orientation matrix
			gene.Orientation = PlugOrientation;

			// Get the name of our parent socket (if we're not the root)
			if (parentSocket != null)
			{
				gene.Socket = parentSocket.Name.Substring(0,4);					// convert "skt3-0" to skt3
			}
			// Recursively build up the rest of the genome...
			// If we have a child, create a new gene for it, link it into the genome tree and ask the child to update this gene
			if (this.firstChild != null)
			{
				gene.FirstChild = new Gene();
				this.firstChild.UpdateGene(gene.FirstChild);
			}
			// If we have a sibling, blah blah blah
			if (this.sibling != null)
			{
				gene.Sibling = new Gene();
				this.sibling.UpdateGene(gene.Sibling);
			}
		}

		/// <summary>
		/// Select the next channel for editing
		/// </summary>
		public void NextChannel()
		{
			if (++Lab.SelectedChannel >= channel.Length)
				Lab.SelectedChannel = 0;
		}
		/// <summary>
		/// Select the previous channel for editing
		/// </summary>
		public void PrevChannel()
		{
			if (--Lab.SelectedChannel < 0)
				Lab.SelectedChannel = channel.Length - 1;
		}

		/// <summary>
		/// Select the next valid chemical that the selected channel can support.
		/// </summary>
		public void NextChemical()
		{
            try
            {
			    int chem = (channel[Lab.SelectedChannel].chemical + 1) % (Chemistry.NUMCHEMICALS+1);
			    while (IsValidChemical(chem) == false)
			    {
				    chem = (++chem) % (Chemistry.NUMCHEMICALS+1);						// keep incrementing and wrap until safe chem found
			    }
			    channel[Lab.SelectedChannel].chemical = chem;
			    Lab.SelectedOrg.Refresh();												// reconnect all the channels following the change
            }
            catch (Exception e)
            {
                throw new SDKException("Unable to select channel " + Lab.SelectedChannel, e);
            }
    }

		/// <summary>
		/// Return the previous valid chemical that the selected channel can support.
		/// </summary>
		public void PrevChemical()
		{
            try
            {
                int chem = channel[Lab.SelectedChannel].chemical - 1;
                if (chem < 0)
                    chem = Chemistry.NUMCHEMICALS;
                while (IsValidChemical(chem) == false)
                {
                    if (--chem < 0)
                        chem = Chemistry.NUMCHEMICALS;
                }
                channel[Lab.SelectedChannel].chemical = chem;
                Lab.SelectedOrg.Refresh();												// reconnect all the channels following the change
            }
            catch (Exception e)
            {
                throw new SDKException("Unable to select channel " + Lab.SelectedChannel, e);
            }
		}

		/// <summary>
		/// Check whether the selected channel can support this chemical.
		/// The rules are as follows:
		/// - If this is an input channel, it can be any chemical (because chemicals can fan out), including any global chemical
		/// - if this is an output channel, it can't be global because that would allow users to create energy from nothing.
        ///   Also, it can't use the same chemical as any other output or bypass channel leading to the same socket. 
        ///   If it was, the next cell in the chain wouldn't know which one to get its inputs from.
        /// 
		/// </summary>
		/// <param name="chemical">The prospective chemical to be checked</param>
		/// <returns></returns>
		private bool IsValidChemical(int chemical)
		{
			ChannelData[] data = physiology.GetChannelData();

			// If this is chemical 0 (i.e. channel is being set to unused/constant), we're ok
			if (chemical == 0)
				return true;

			// If this is an input channel, we're ok for all chemicals, including globals
			if (data[Lab.SelectedChannel].IsInput())
				return true;

			// If this is an output or bypass channel, we can't allow any global chemicals
			if (chemical > Chemistry.NUMSIGNALS)
				return false;

			// For output channels, compare to all other o/p and bypass channels
			// and only accept chemical if nothing else has that affinity
            if (data[Lab.SelectedChannel].IsOutput())
            {
                for (int c = 0; c < channel.Length; c++)
                {
                    if ((c != Lab.SelectedChannel)										// if this is not me
                        && (!data[c].IsInput())											// and this is an output or bypass channel
                        && (channel[c].chemical == chemical)							// and it has the same chemical
                        && (data[c].Dest == data[Lab.SelectedChannel].Dest))            // and that channel connects to the same output socket as me
                        return false;													// we fail
                }
                return true;
            }

            // Else, for bypass channels, only accept if no other channel between the same TWO sockets has the same affinity.
            // This permits collisions but it's necessary because the user might be trying to fan out a signal to two sockets in a plexus,
            // and since we don't yet know which direction the signal will travel in, we don't know which is the output end.
            for (int c = 0; c < channel.Length; c++)
            {
                if ((c != Lab.SelectedChannel)										// if this is not me
                    && (!data[c].IsInput())											// and this is an output or bypass channel
                    && (channel[c].chemical == chemical))							// and it has the same chemical
                {
                    if ((data[c].Dest == data[Lab.SelectedChannel].Dest)            // if we're connected to the same pair of sockets
                    && (data[c].Source == data[Lab.SelectedChannel].Source))
                        return false;
                    if ((data[c].Source == data[Lab.SelectedChannel].Dest)
                    && (data[c].Dest == data[Lab.SelectedChannel].Source))          // ...even if the other way around
                        return false;											    // we fail
                }
            }

            return true;

		}













		#endregion

	}




	/// <summary>
	/// An input, output or throughput channel, carrying chemicals into and out of a cell.
	/// An array of channels is stored in the Cell itself (not the physiology). The cell type can access channels
	/// to fetch functional block input and distribute output, using methods in IControllable.
	/// 
	/// Channels chain backwards, i.e:
	/// - each output channel stores its output in its own .signal member
	/// - each input/bypass channel holds a reference to the output channel it sources from
	/// 
	/// Channels are connected by virtue of their chemical selectivity. This means that no two channels with output 
	/// (i.e. output or bypass channels) attached to the same socket can share the same chemical, since this would cause ambiguity when linking.
	/// However, two channels with input (input or bypass channels) can share the same chemical and will link to
	/// the same source, allowing the user to send a chemical into a cell and also through to the subsequent cells.
    /// Two output/bypass channels CAN share the same chemical if they connect to different sockets.
	/// 
	/// Input and output channels have implicit directionality, but bypass channels might need to transmit signals
	/// in one direction or the other, according to which side of the channel the user has connected to a signal source
	/// (an output channel or the output of another bypass channel). Since the channels are backward-chained we need to
	/// ensure that every bypass channel is pointing so that its .source is in the direction of the signal source.
	/// This is like aligning a bunch of magnets, and requires several recursive passes over the cell tree, aligning
	/// any bypass channels that are directly connected to either an output channel or an already-aligned bypass
	/// channel. This needs to be done whenever the organism is edited. Thereafter, all input and bypass channels can 
	/// fetch their input from the .signal member of their .source
    /// 
    /// Default channel chemical selectivity is defined in the ChannelData object, but genes store the user-defined selectivity
    /// 
    /// Each channel can be set to have no chemical selectivity (i.e. chemical == 0), whereupon it will fetch its signal from a user-definable constant
    /// value. This can be adjusted in the lab, and will be initialised with a default value in the ChannelData definition. This allows control inputs
    /// (speed, threshold, etc.) to be driven by chemical signals, set to a suitable constant by the user or kept at a default value.
	/// 
    /// CREATING CHANNELS IN THE MESH:
    /// - Add a cuboid or pyramid to represent the functional block. Colour this RED and name it "func"
    /// - Pyramids show the direction of flow for processor cells. Cubes are better for sensors/actuators.
    /// - Add cuboidal filaments to represent each channel, starting and ending at a plug, a socket or the functional block.
    /// - Name them "chan0" etc. They can be any colour because the code recolours them.
    /// - Bypass channels will tend to form diamond shapes, starting at the plug, avoiding the functional block and ending at the socket.
    /// - If a cell has several sockets, each must have its own channel.
	/// 
	/// 
	/// </summary>
	public class Channel
	{

		// Genetically defined members
		public int chemical = 0;									// chemical carried by this channel
		public float signal = 0;									// output signal (for output / bypass channels)
        public float constant = 0;                                  // user-defined constant, to be used as signal if an i/p channel has no chemical affinity

		// Members defined when the cell is created (cloned from library)
		public Channel source = null;								// signal source for inputs & bypass channels (null for o/p chans). 

		public Channel()
		{
		}


		/// <summary>
		/// Read ONE Channel member from genome (not all members are defined in genome, e.g. .organelle)
		/// </summary>
		/// <param name="xml">The xml stream</param>
		/// <param name="tag">The name of the node</param>
		public void ReadXml(XmlTextReader xml, string tag)
		{
			switch (tag)											// action depends on the tag this text is part of
			{
				//case "type":										// <type> ["input", "output", "bypass"] </type>
				//    type = (Channel.Types)Enum.Parse(typeof(Channel.Types), xml.Value);	// convert string to enum value
				//    break;

                // <chemical>
                case "chemical":
                    chemical = Convert.ToInt32(xml.Value);
                    break;

                // <signal>
                case "constant":
                    constant = Convert.ToSingle(xml.Value);
                    break;

                // TODO: Other <channel> tags here

				default:
					throw new XmlException("Unexpected tag in <channel> node: " + tag);
			}

		}

		/// <summary>
		/// Write this channel to the genome
		/// </summary>
		/// <param name="xml">the XmlWriter stream (currently inside the gene node)</param>
		public void WriteXml(XmlWriter xml)
		{
			xml.WriteStartElement("channel");
			//xml.WriteElementString("type", type.ToString());
            xml.WriteElementString("chemical", chemical.ToString());
            xml.WriteElementString("constant", constant.ToString());
            xml.WriteEndElement();
		}


	}









}
