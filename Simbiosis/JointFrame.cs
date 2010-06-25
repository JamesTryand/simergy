using System;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	/// <summary>
	/// Substitute for MDX's Frame class, so that I can clone frame hierarchies
	/// </summary>
	public class JointFrame
	{
		private string name = null;
		public string Name { get { return name; } set { name = value; } }

		/// <summary> Type of frame and index number. E.g. "skt1" has type=FrameType.Socket and index=1 </summary>
		public enum FrameType 
		{
			General,										// unspecialised frames
			Socket,											// Socket frames, labelled "skt#" in truespace
			Hotspot,										// Hotspot (effector/sensor) frames, labelled "hot#" in truespace
			Animating,										// Animating frames (joints etc.), labelled "anim#"
			Channel,										// Signal channels inside cell (non-flippable cells, or currently active channels in flippable ones)
			Function,										// non-channel functional blocks ("func#"), visible inside cell along with channels
		};

        /// <summary>
        /// Enum describing the directionality of a cell (for those with upstream and downstream variants).
        /// </summary>
        public enum Direction
        {
            Both,
            Up,
            Down
        };

		private FrameType type = FrameType.General;			// this frame's type
		public FrameType Type { get { return type; } }
		private int index = 0;								// this frame's index - skt0, skt1 or whatever
		public int Index { get { return index; } }
        public Direction direction = Direction.Both;   // for channel frames or functional blocks, this designates their directionality, if any
        
		public JointFrame FirstChild = null;
		public JointFrame Sibling = null;

        public Matrix TransformationMatrix = Matrix.Identity;   // Transformation included in the .X file (neutral pose)
        public Matrix CombinedMatrix = Matrix.Identity;         // total transformation (parent + xfile + anim)
        
		public MeshContainer MeshContainer = null;

        /// <summary> Values and limits for animation, or NULL if this frame doesn't animate </summary>
		public MotionController Motion = null;

        /// <summary> If this frame has colour animation, this member will hold the material to be used for this instance of the mesh</summary>
        public Material[] animColour = null;


		/// <summary>
		/// Create a new blank JointFrame
		/// </summary>
		/// <param name="name"></param>
		public JointFrame(string name)
		{
            this.name = name;

			// Establish frame's type and index (and direction too, if cell has upstream and downstream variants)
			try
			{
                // Sockets
				if (name.StartsWith("skt"))
				{
					type = FrameType.Socket;
                    index = Convert.ToInt32(Strip(name).Substring(3));
				}
                    // Hotspots
				else if (name.StartsWith("hot"))
				{
					type = FrameType.Hotspot;
                    index = Convert.ToInt32(Strip(name).Substring(3));
				}
                // Animating frames
				else if (name.StartsWith("anim"))
				{
					type = FrameType.Animating;
                    index = Convert.ToInt32(Strip(name).Substring(4));
                    // Create a default motion controller in case this anim# frame is only colour-animated (no keyframes exist). Otherwise it'll crash when I try to update its joints
                    Motion = new MotionController();
				}
                // ordinary channels (or channels that are used in both upstream and downstream variants)
                else if (name.StartsWith("chan"))
                {
                    type = FrameType.Channel;
                    index = Convert.ToInt32(Strip(name).Substring(4));
                    direction = JointFrame.Direction.Both;
                }
                // upstream channels
                else if (name.StartsWith("up"))                                         // upstream channels are like ordinary channels, except their direction is .Up
                {
                    type = FrameType.Channel;
                    index = Convert.ToInt32(Strip(name).Substring(2));
                    direction = JointFrame.Direction.Up;
                }
                // downstream channels
                else if (name.StartsWith("down"))
                {
                    type = FrameType.Channel;
                    index = Convert.ToInt32(Strip(name).Substring(4));
                    direction = JointFrame.Direction.Down;
                }
                // functional blocks
                else if (name.StartsWith("func"))
				{
					type = FrameType.Function;
                    if (name.Length>4)                                                  // legacy: some cells use "func" instead of "func0". Indices are needed for Core to display global chems
                        index = Convert.ToInt32(Strip(name).Substring(4));
                    direction = JointFrame.Direction.Both;
                }
                // upstream functional blocks
                else if (name.StartsWith("fup"))
                {
                    type = FrameType.Function;
                    if (name.Length > 2)                                                // legacy
                        index = Convert.ToInt32(Strip(name).Substring(3));
                    direction = JointFrame.Direction.Up;
                }
                // downstream functional blocks
                else if (name.StartsWith("fdown"))
                {
                    type = FrameType.Function;
                    if (name.Length > 5)                                                // legacy
                        index = Convert.ToInt32(Strip(name).Substring(5));
                    direction = JointFrame.Direction.Down;
                }

			}
			catch(Exception)
			{
				Debug.WriteLine("Unable to extract index number from frame type: "+name);
				throw;
			}
		}

        /// <summary>
        /// Strip out any junk added to a frame's name by the graphics editor. Softimage doesn't do this but Truespace converts, e.g. "chan0" to "chan0-0".
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string Strip(string name)
        {
            int index = name.IndexOfAny(new char[] { '-', '.', '_' });
            if (index == -1) return name;
            return name.Substring(0,index);
        }


		/// <summary>
		/// Find the frame in this hierarchy with this EXACT name
		/// </summary>
		/// <param name="root">the root to start searching from</param>
		/// <param name="name">the EXACT name of the frame</param>
		/// <returns>the frame of that name, or null if not found</returns>
		public static JointFrame Find(JointFrame root, string name)
		{
			// get the current frame's name. If we match then return
			if (root.Name == name)	
			{							
					return root;
			}
			// else continue with rest of hierarchy
			if (root.Sibling!=null)
			{
				JointFrame result = Find(root.Sibling,name);
				if (result!=null)
					return result;
			}
			if (root.FirstChild!=null)
			{
				JointFrame result = Find(root.FirstChild,name);
				if (result!=null)
					return result;
			}
			return null;							
		}


		/// <summary>
		/// Find the frame in this hierarchy with this BASE name
		/// E.g. if name = "JointHead", the first frame called "JointHead*" will be returned.
		/// This copes with the fact that TrueSpace and presumably other packages add a number to
		/// the end of frame names. So if I call a mesh "Eric", Eric's frame will be called "Eric-0"
		/// or some other number, depending on when it was created.
		/// In a skinned mesh the bones are called Bone-# by default. MUST rename them to prevent them all having
		/// a base name of "Bone"!
		/// This method is to allow a frame to be found (e.g. a joint) using a standardised name, e.g. from
		/// a Cell's X file.
		/// </summary>
		/// <param name="root">the root to start searching from</param>
		/// <param name="basename">the ROOT name of the frame</param>
		/// <returns>the frame of that name, or null if not found</returns>
		public static JointFrame FindBaseName(JointFrame root, string basename)
		{
			// convert string to a valid base name if necessary, then recurse through the hierarchy
			return RecursiveFindBaseName(root, BaseName(basename));							
		}

		/// <summary>
		/// Recursive part of FindBaseName()
		/// </summary>
		/// <param name="root"></param>
		/// <param name="basename"></param>
		/// <returns></returns>
		private static JointFrame RecursiveFindBaseName(JointFrame root, string basename)
		{
			// get the current frame's base name. If we match then return
			if (root.Name!=null)
			{
				string rootbase = BaseName(root.Name);									
				if (rootbase == basename)												
					return root;
			}
			// else continue with rest of hierarchy
			if (root.Sibling!=null)
			{
				JointFrame result = RecursiveFindBaseName(root.Sibling,basename);
				if (result!=null)
					return result;
			}
			if (root.FirstChild!=null)
			{
				JointFrame result = RecursiveFindBaseName(root.FirstChild,basename);
				if (result!=null)
					return result;
			}
			return null;							
		}

		/// <summary>
		/// Convert a full bone name into its base name, for use in searching
		/// The base name is the name up to the first hyphen, so "Bone-1" becomes "Bone"
		/// </summary>
		/// <param name="name"></param>
		private static string BaseName(string name)
		{
            int hyphen = name.IndexOf('-');
            if (hyphen > 0)
                return name.Substring(0, hyphen);
            return name;                                    // return whole string if it doesn't contain a hyphen
		}

		/// <summary>
		/// Print an entire frame hierarchy to the debug output stream
		/// </summary>
		/// <param name="root">root of hierarchy</param>
		/// <param name="level">current depth into hierarchy (set this to 0 when calling externally)</param>
		/// <returns></returns>
		public static void DebugHierarchy(JointFrame root, int level)
		{
			// Display info about this frame
			root.DebugFrame(level);

			// continue walking the tree, depth-first
			if (root.FirstChild!=null)
			{
				DebugHierarchy(root.FirstChild, level+1);
			}
			if (root.Sibling!=null)
			{
				DebugHierarchy(root.Sibling, level);
			}
		}

		/// <summary>
		/// Print a single frame to the debug stream
		/// </summary>
		/// <param name="level"></param>
		public void DebugFrame(int level)
		{
			string indent = "\t\t\t\t\t\t\t\t\t\t\t".Substring(0,level);

			Debug.WriteLine(indent+"*** FRAME ["+name+"], Type:index = ["+type.ToString()+":"+index+"] ***");
			if (MeshContainer!=null)
			{
				Debug.WriteLine(indent+"    MESHCONTAINER: ["+MeshContainer.Name+"]");
			}
			Debug.WriteLine(indent+"    Transformation Matrix: "+ThreeDee.DecodeMatrix(TransformationMatrix));
			Debug.WriteLine(indent+"          Combined Matrix: "+ThreeDee.DecodeMatrix(CombinedMatrix));
		}

		/// <summary>
		/// Recursively clone an entire frame hierarchy
		/// </summary>
		/// <param name="master">the root frame of the master hierarchy</param>
		/// <returns>the root frame of the clone hierarchy</returns>
		public static JointFrame CloneHierarchy(JointFrame master)
		{
			JointFrame newFrame = (JointFrame)master.MemberwiseClone();		// create the new frame as a shallow copy of the master

			// Replace any members that should be unique to this instance
			if (master.Motion!=null)										// our unique motion controller (if any)
			{
				newFrame.Motion = master.Motion.Clone();
			}

			if (master.FirstChild!=null)									// recurse through children/siblings
			{
				newFrame.FirstChild = CloneHierarchy(master.FirstChild);
			}
			if (master.Sibling!=null)
			{
				newFrame.Sibling = CloneHierarchy(master.Sibling);
			}

			return newFrame;
		}

		/// <summary>
		/// Search the given frame hierarchy for all frames of a given type (sockets, effectors, etc.).
		/// Return an array containing references to these frames, sorted in order, so that
		/// "skt0" is in array[0], etc. Used to give cells rapid access to their joints and effectors 
		/// for physics & animation. 
		/// Note: array will be no larger than is needed to contain all the named frames. Array may be of zero size but will never be null.
		/// If any indices are missing (e.g. anim0 and anim2 exist but not anim1) then the unused array entries will be null
		/// and a trace warning will be emitted.
		/// </summary>
		/// <param name="root">The root frame for the hierarchy</param>
		/// <param name="type">The type of frame to return</param>
		/// <returns>The array of JointFrames</returns>
		public static JointFrame[] IndexFrameType(JointFrame root, FrameType type)
		{
			int num = 0;
			JointFrame[] array = new JointFrame[50];							// A big enough array
			RecurseFrameType(type, array, root);								// fill the elements
			
			for (num=array.Length; (num>0)&&(array[num-1]==null); num--);		// find out how many entries were filled
			JointFrame[] array2 = new JointFrame[num];							// copy elements into a correctly-sized array
			Array.Copy(array,0,array2,0,num);									// (Possibly zero-length)
			for (int i=0; i<num; i++)											// scan the array for any missing elements and
			{																	// emit a trace warning - cell designer might have
				if (array2[i]==null)											// made a mistake!
				{
					Trace.WriteLine("WARNING: Unable to find a frame of type "+type.ToString()+" with index "+i);
				}
			}
			return array2;														// any unused entries will be null
		}
		private static void RecurseFrameType(FrameType type, JointFrame[] array, JointFrame frame)
		{
			if (frame.type==type)												// if frame is correct type
			{
				array[frame.index] = frame;										// store a ref to it in correct index
			}
			if (frame.FirstChild!=null)											// then recurse through children/siblings
				RecurseFrameType(type, array, frame.FirstChild);
			if (frame.Sibling!=null)
				RecurseFrameType(type, array, frame.Sibling);
		}




	}



}
