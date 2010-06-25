using System;
using System.Diagnostics;
using System.IO;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;


namespace Simbiosis
{
	/// <summary>
	/// Static class: 
	/// Loads cell data from an X file.
	/// </summary>
	public class CellLoader
	{
		private static XFileManager manager = null;							// The current XFile manager object
		private static XFile xfile = null;									// The XFile currently being loaded
		private static string filespec = null;								// its filespec
		private static string folder = null;					    		// the subfolder in which to look for [vaiant].X files and textures (Cells\group\type)
		private static JointFrame rootFrame = null;							// The root of the frame hierarchy being built
		private static MotionController motion = null;						// the motion controller being built
		private static bool rmin = false;									// flags used to track which motion controller
		private static bool tmin = false;									// members have been filled from an AnimationKey
		private static bool smin = false;									// object



		private static Guid propertiesGuid = Guid.NewGuid();
		// add other GUIDs here and include as {1} {2} etc. in string
		private static string templatesString = @"xof 0302txt 0064
				template properties {{
					<{0}>
					FLOAT mass;
					FLOAT resistance;
					FLOAT bouyancy;
				}}
				// add more templates here
				";


		static CellLoader()
		{
		}


		/// <summary>
		/// Register templates for my additional cell data
		/// </summary>
		private static void RegisterCustomTemplates()
		{
			string tString = null;
			byte[] tBytes = null;

			try
			{
				tString = String.Format(templatesString,
										propertiesGuid.ToString());							// add the GUIDs
				tBytes = System.Text.Encoding.ASCII.GetBytes(tString);						// cvt to byte array
				manager.RegisterTemplates(tBytes);
			}
			catch (GraphicsException e)
			{
				Debug.WriteLine("Unable to create custom template");
				throw e;
			}
		}


		/// <summary>
		/// Read an XFile and create the objects defined in it,
		/// i.e. the frame hierarchy and Cytoplasm meshes.
		/// The animationSet objects in the X file are used to set joint limits in the frames
		/// </summary>
		/// <param name="group">Group that cell belongs to (i.e. DLL and folder name)</param>
		/// <param name="name">Cell type (i.e. filename minus extension, and CellType-derived class name)</param>
        /// <param name="variantName">NAME of the variant (i.e. name of the .X file)</param>
		/// <returns>The root frame of the loaded hierarchy</returns>
		public static JointFrame Load(string group, string type, string variantName)
		{

			CellLoader.folder = "cells" + "\\" + group + "\\" + type;				    // store subfolder name so that textures can be found

            string fsp = FileResource.Fsp(CellLoader.folder, variantName + ".X");	    // .X file will be ../cells/group/type/variant.x
//			Debug.WriteLine("Loading XFile: "+fsp);
			filespec = fsp;

			manager = new XFileManager();
			manager.RegisterDefaultTemplates();							                // register standard templates BEFORE opening file

			try
			{
				xfile = manager.FromFile(filespec);						                // create an XFile object from file
			}
			catch (GraphicsException e)
			{
				if (e.ErrorCode == (int)XFileErrorCodes.ParseError)
					throw new SDKException("ERROR in CellLoader.Load() - Couldn't parse Xfile "+filespec);
				else
                    throw new SDKException("ERROR in CellLoader.Load() - Failed to load " + filespec);
			}

			manager.RegisterXFileTemplates(xfile);						// register any custom templates in the file
////////			RegisterCustomTemplates();									// register any templates in this code

			for (int i=0; i<xfile.NumberChildren; i++)					// iterate through the root-level objects
			{
				using (XFileData rootObject = xfile.GetChild(i))
				{
					LoadRootObject(rootObject);
				}
			}

			// Dispose of everything
			xfile.Dispose();
			manager.Dispose();

			// return the completed hierarchy
			return rootFrame;
		}

		/// <summary>
		/// Process a first-level node from an XFileData object
		/// </summary>
		/// <param name="node"></param>
		/// <param name="depth"></param>
		/// <param name="parentFrame"></param>
		/// <param name="thisFrame"></param>
		private static void LoadRootObject(XFileData node)
		{
			// **** Frame
			if (node.Type==XFileGuid.Frame)
			{
				LoadFrame(node, 0, null);
			}

			// **** Animation set
			else if (node.Type==XFileGuid.AnimationSet)
			{
				LoadAnimationSet(node);
			}


			// TODO: Other root-level objects here

		}


		/// <summary>
		/// Recursively process frame node(s) and their subnodes from an XFileData object
		/// </summary>
		/// <param name="node"></param>
		/// <param name="depth"></param>
		/// <param name="parentFrame"></param>
		private static void LoadFrame(XFileData node, int depth, JointFrame parentFrame)
		{

			JointFrame newFrame = new JointFrame(node.Name);				// Create a new frame
//			Debug.Write(Depth(depth) + "Creating frame "+newFrame.Name+" ("+newFrame.Type.ToString()+":"+newFrame.Index+") ");
			if (parentFrame==null)											// if there's no parent frame
			{																// the new frame is the root
//				Debug.WriteLine("as root");
				rootFrame = newFrame;
			}
			else if (parentFrame.FirstChild==null)							// or, if we're the first child
			{
//				Debug.WriteLine("as child of "+parentFrame.Name);
				parentFrame.FirstChild = newFrame;							// attach as first child of parent
			}
			else															// else we're a sibling
			{
				JointFrame bigSister = parentFrame.FirstChild;
				while (bigSister.Sibling!=null)								// so scan to the end of this row
					bigSister = bigSister.Sibling;
//				Debug.WriteLine("as sibling of "+bigSister.Name);
				bigSister.Sibling = newFrame;								// attach as a sibling
			}

			// --------- Recurse through this node's children to construct frame's contents ----------
			depth++;
			for (int i=0; i<node.NumberChildren; i++)
			{
				using (XFileData child = node.GetChild(i))
				{
					// **** Frame nested inside this frame
					if (child.Type==XFileGuid.Frame)
					{
						LoadFrame(child, depth, newFrame);
					}

					// **** FrameTransformationMatrix
					else if (child.Type==XFileGuid.FrameTransformMatrix)
					{
						LoadTransformationMatrix(child, depth, newFrame);
					}

					// **** Mesh
					else if (child.Type==XFileGuid.Mesh)
					{
                        string name = child.Name;                                       // use mesh named in xfile if available
                        if (name == "") name = node.Name;                               // or name it after the frame

						LoadMesh(child, name, depth, newFrame);
					}

				}
			}

		}


		/// <summary>
		/// Return padding to align debug messages according to depth into node tree
		/// </summary>
		/// <param name="depth"></param>
		/// <returns></returns>
		private static string Depth(int depth)
		{
			return "\t\t\t\t\t\t\t\t\t\t\t".Substring(0,depth);
		}


		/// <summary>
		/// Read a transformation matrix and attach to the current frame
		/// </summary>
		/// <param name="node"></param>
		/// <param name="depth"></param>
		/// <param name="thisFrame"></param>
		private static void LoadTransformationMatrix(XFileData node, int depth, JointFrame thisFrame)
		{
			try
			{
//				Debug.WriteLine(Depth(depth) + "Creating Matrix for "+thisFrame.Name);
				GraphicsStream stream = node.Lock();							// access the data stream
				thisFrame.TransformationMatrix = MatrixFromXFile(stream);		// read in the matrix
				node.Unlock();
			}
			catch (Exception e)
			{
				Debug.WriteLine("Error reading transformation matrix: "+e.ToString());
				throw;
			}
		}

		/// <summary>
		/// Read a mesh and create a Cytoplasm object from it, then attach this to the current frame
		/// </summary>
		/// <param name="node"></param>
		/// <param name="depth"></param>
		/// <param name="thisFrame"></param>
		private static void LoadMesh(XFileData node, string name, int depth, JointFrame thisFrame)
		{
//			Debug.WriteLine(Depth(depth) + "Creating Mesh "+name+" for "+thisFrame.Name);

			Mesh mesh = null;
			ExtendedMaterial[] materials = null;
			GraphicsStream adjacency = null;
			EffectInstance[] effects = null;

			try
			{
				mesh = Mesh.FromX(node,MeshFlags.Managed,Engine.Device,out adjacency, out materials, out effects);
				//Debug.WriteLine(Depth(depth) + "Mesh "+node.Name+" has "+mesh.NumberVertices+" verts and "+materials.Length+" materials");
			}
			catch (Direct3DXException e)
			{
				Debug.WriteLine("Error reading mesh: "+e.ToString());
				throw;
			}

			// Create a Cytoplasm (meshcontainer) object from mesh, materials, etc.
			// Give it the right name (node.Name)
			// Link it into the tree of Cytoplasm objects
			// Link it to this mesh
			Cytoplasm cyt = new Cytoplasm(folder,name,mesh,materials,effects,adjacency,null);
			thisFrame.MeshContainer = cyt;

		}

		/// <summary>
		/// Read a Matrix from an XFile node
		/// </summary>
		/// <param name="stream">GraphicsStream obtained from locking the Xfile data object</param>
		public static Matrix MatrixFromXFile(GraphicsStream stream)
		{
			float v = 0;													// elements are floats
			Matrix m = Matrix.Identity;
            Type tv = v.GetType();                                          // type of float

			m.M11 = (float)stream.Read(tv);		                            // read the matrix
			m.M12 = (float)stream.Read(tv);
			m.M13 = (float)stream.Read(tv);
			m.M14 = (float)stream.Read(tv);
			m.M21 = (float)stream.Read(tv);
			m.M22 = (float)stream.Read(tv);
			m.M23 = (float)stream.Read(tv);
			m.M24 = (float)stream.Read(tv);
			m.M31 = (float)stream.Read(tv);
			m.M32 = (float)stream.Read(tv);
			m.M33 = (float)stream.Read(tv);
			m.M34 = (float)stream.Read(tv);
			m.M41 = (float)stream.Read(tv);
			m.M42 = (float)stream.Read(tv);
			m.M43 = (float)stream.Read(tv);
			m.M44 = (float)stream.Read(tv);

			return m;
		}


		/// <summary>
		/// Process AnimationSet node(s).
		/// Each X file should have a SINGLE AnimationSet, containing Animation nodes
		/// that each contain TWO keyframes (0,1). These define the limits for the
		/// joint.
		/// </summary>
		/// <param name="node"></param>
		/// <param name="depth"></param>
		/// <param name="parentFrame"></param>
		private static void LoadAnimationSet(XFileData node)
		{
//			Debug.WriteLine("Reading AnimationSet "+node.Name);
			// Recurse through the child Animation nodes for this set
			for (int i=0; i<node.NumberChildren; i++)
			{
				using (XFileData child = node.GetChild(i))
				{
					// **** Frame nested inside this frame
					if (child.Type==XFileGuid.Animation)
					{
						LoadAnimation(child);
					}
				}
			}
	
		}

		/// <summary>
		/// Load an Animation node
		/// </summary>
		/// <param name="node"></param>
		private static void LoadAnimation(XFileData node)
		{
			JointFrame frame = null;
            int f = 0;

//			Debug.WriteLine("\tReading Animation "+node.Name);

            // Find the subnode that's a REFERENCE to a frame, then find the frame with that name
            XFileData frameRef = null;
            for (f = 0; f < node.NumberChildren; f++)
            {
                if (node.GetChild(f).Type == XFileGuid.Frame)
                {
                    frameRef = node.GetChild(f);
                    break;
                }
            }
            if (frameRef==null)
                throw new Exception("XFile animation node " + node.Name + "doesn't refer to a frame");

            frame = JointFrame.Find(rootFrame, frameRef.Name);
            if (frame == null)
                throw new Exception("XFile Animation node " + node.Name + " refers to unknown frame " + frameRef.Name);

 			// If this frame is animatable, create a motion controller and load in the limits.
			// Non-animatable frames have no motion controller.
			if (frame.Type==JointFrame.FrameType.Animating)
			{

				// Create a motion controller in which to store the limit data
				motion = new MotionController();
				rmin = tmin = smin = false;							// We haven't filled any motion members yet

				// Now iterate through the remaining AnimationKey and AnimationOptions nodes...
				for (int i=0; i<node.NumberChildren; i++)
				{
					using (XFileData child = node.GetChild(i))
					{
						// **** Frame nested inside this frame
						if (child.Type==XFileGuid.AnimationKey)
						{
							LoadKeyframes(child, frame);
						}
						// We don't care about AnimationOptions & we skip the frame reference
					}
				}

				// Store the new motion controller in the frame
				frame.Motion = motion;								
//				Debug.WriteLine("\t\tFrame "+frame.Name+" is animated");
			}

		}

		/// <summary>
		/// Read an AnimationKey node and load its transformation into the given frame
		/// </summary>
		/// <param name="node"></param>
		/// <param name="frame"></param>
		private static void LoadKeyframes(XFileData node, JointFrame frame)
		{
			int keyType = 0, keyFrames = 0;
			int keyTime = 0, keyValues = 0;
			Quaternion q = new Quaternion();
			Vector3 v = new Vector3();

			GraphicsStream stream = node.Lock();							// Lock the node and obtain a stream
			keyType = (int)stream.Read(keyType.GetType());					// get the keyframe type (rotation, etc.)
			keyFrames = (int)stream.Read(keyFrames.GetType());				// get the number of keyframes (should be 2)
			if (keyFrames>2) throw new Exception("XFile "+filespec+" should have only two keyframes per animation");

			for (int i=0; i<keyFrames; i++)
			{
				keyTime = (int)stream.Read(keyTime.GetType());					// time of key (ignored)
				keyValues = (int)stream.Read(keyValues.GetType());				// number of values in key (ignored)

				switch (keyType)
				{
						// Rotation
					case 0:
						q = ReadQuaternion(stream);								// get the transformation
						if (rmin == false)										// store it in min or max
						{
							motion.RotationMin = q;								// (min first, then max if min is full)
							motion.RotationMax = q;								// (fill max too in case no 2nd key)
							rmin = true;
						}
						else
							motion.RotationMax = q;
						break;

						// Translation
					case 1:
						v = ReadVector(stream);
						if (smin == false)
						{
							motion.ScaleMin = v;
							motion.ScaleMax = v;
							smin = true;
						}
						else
							motion.ScaleMax = v;
						break;
						// Scale
					case 2:
						v = ReadVector(stream);
						if (tmin == false)
						{
							motion.TranslationMin = v;
							motion.TranslationMax = v;
							tmin = true;
						}
						else
							motion.TranslationMax = v;
						break;

				}
			}

			node.Unlock();														// release the node
		}


		/// <summary>
		/// Read a vector from open xfile stream
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		private static Vector3 ReadVector(GraphicsStream stream)
		{
			Vector3 v = new Vector3();
			//			v = (Vector3)stream.Read(v.GetType());	
			v.X = (float)stream.Read(v.X.GetType());	
			v.Y = (float)stream.Read(v.X.GetType());	
			v.Z = (float)stream.Read(v.X.GetType());	
			return v;
		}

		/// <summary>
		/// Read a quaternion from open xfile stream
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		private static Quaternion ReadQuaternion(GraphicsStream stream)
		{
			Quaternion q = new Quaternion();
			q.W = (float)stream.Read(q.W.GetType());				// must read element by element to get WXYZ order
			q.X = (float)stream.Read(q.X.GetType());	
			q.Y = (float)stream.Read(q.Y.GetType());	
			q.Z = (float)stream.Read(q.Z.GetType());	
			q.Normalize();
			//Debug.WriteLine(ThreeDee.DecodeQuaternion(q));
			return q;
		}





	}


}
