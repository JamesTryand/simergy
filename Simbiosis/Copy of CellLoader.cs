using System;
using System.Diagnostics;
using System.IO;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;


namespace Simbiosis
{
	/// <summary>
	/// Loads cell data from an X file
	/// </summary>
	public class CellLoader
	{
		XFileManager manager = null;
		XFile xfile = null;

		SimFrame rootFrame = null;										// The root of the frame hierarchy being built

		public CellLoader()
		{
		}

		/// <summary>
		/// Read an XFile and create the objects defined in it
		/// </summary>
		/// <param name="filespec"></param>
		public void Load(string filespec)
		{
			Debug.WriteLine("Loading XFile: "+filespec);

			manager = new XFileManager();
			manager.RegisterDefaultTemplates();							// register standard templates BEFORE opening file
			try
			{
				xfile = manager.FromFile(filespec);						// create an XFile object from file
			}
			catch (GraphicsException e)
			{
				if (e.ErrorCode == (int)XFileErrorCodes.ParseError)
					Debug.WriteLine("ERROR in CellLoader.Load() - Couldn't parse Xfile "+filespec);
				else
					Debug.WriteLine("ERROR in CellLoader.Load() - Failed to load "+filespec);
				throw;
			}

			manager.RegisterXFileTemplates(xfile);						// register any custom templates in the file
			//manager.RegisterTemplates(data);							// register any templates defined in my code

			for (int i=0; i<xfile.NumberChildren; i++)					// iterate through the root-level objects
			{
				using (XFileData rootObject = xfile.GetChild(i))
				{
					LoadDataObject(rootObject,0,null,null);
				}
			}

			// Dispose of everything
			xfile.Dispose();
			manager.Dispose();
		}

		/// <summary>
		/// Recursively create frames and their contents from an XFileData object
		/// </summary>
		/// <param name="node"></param>
		/// <param name="depth"></param>
		/// <param name="parentFrame"></param>
		/// <param name="thisFrame"></param>
		private void LoadDataObject(XFileData node, int depth, SimFrame parentFrame, SimFrame thisFrame)
		{
			// --------- handle the instance nodes (references are handled later) ------------
			if (node.IsReference==false)
			{
				// **** Frame
				if (node.Type==XFileGuid.Frame)
				{
					ReadFrame(node, depth, ref thisFrame);
				}

					// **** FrameTransformationMatrix
				else if (node.Type==XFileGuid.FrameTransformMatrix)
				{
					ReadTransformationMatrix(node, depth, thisFrame);
				}

					// **** Mesh
				else if (node.Type==XFileGuid.Mesh)
				{
					ReadMesh(node, depth, thisFrame);
				}
					// Ignore mesh subset members (materials, normals, etc.) - they're already handled by ReadMesh()

					// **** Animation set
				else if (node.Type==XFileGuid.AnimationSet)
				{
					ReadAnimationSet(node, depth, thisFrame);
				}
					// **** Animation
				else if (node.Type==XFileGuid.Animation)
				{
					
				}
					// **** AnimationKey
				else if (node.Type==XFileGuid.AnimationKey)
				{
					
				}
					// **** AnimationOptions
				else if (node.Type==XFileGuid.AnimationOptions)
				{
					
				}


				// --------- Recurse through this node's children ----------
				// Note: we mustn't touch the children of a REFERENCE node or we'll create a duplicate
				// set of frames, etc. So only run this loop if we've just handled an INSTANCE node
				for (int i=0; i<node.NumberChildren; i++)
				{
					using (XFileData child = node.GetChild(i))
					{
						LoadDataObject(child, depth++, parentFrame, thisFrame);
					}
				}


			}

				// --------- deal with REFERENCES to existing instances (e.g inside animation nodes) ----------
			else
			{
				// **** Frame reference
				if (node.Type==XFileGuid.Frame)
				{
					SimFrame refFrame = null; /////rootFrame.Find(node.Name);
				}
				

			}



		}

		/// <summary>
		/// Return padding to align debug messages according to depth into node tree
		/// </summary>
		/// <param name="depth"></param>
		/// <returns></returns>
		private string Depth(int depth)
		{
			return "\t\t\t\t\t\t\t\t\t\t\t".Substring(0,depth);
		}

		/// <summary>
		/// Create a new frame from this node
		/// </summary>
		/// <param name="node">the XFileData node</param>
		/// <param name="depth">depth in hierarchy</param>
		/// <param name="thisFrame">the previous / new current frame</param>
		private void ReadFrame(XFileData node, int depth, ref SimFrame thisFrame)
		{
			SimFrame newFrame = new SimFrame(node.Name);					// Create a new frame
			if (thisFrame==null)											// if there's no current frame
			{																// the new frame is the root
				Debug.WriteLine(Depth(depth) + "Creating frame "+newFrame.Name+" as root");
				thisFrame = newFrame;
				rootFrame = thisFrame;
			}
			else if (thisFrame.FirstChild==null)							// or, if we're in a new level
			{
				Debug.WriteLine(Depth(depth) + "Creating frame "+newFrame.Name+" as child of "+thisFrame.Name);
				thisFrame.FirstChild = newFrame;							// attach as first child of parent
				thisFrame = newFrame;										// and move down a generation
			}
			else															// else we're a sibling
			{
				SimFrame bigSister = thisFrame.FirstChild;
				while (bigSister.Sibling!=null)								// so scan to the end of this row
					bigSister = bigSister.Sibling;
				Debug.WriteLine(Depth(depth) + "Creating frame "+newFrame.Name+" as sibling of "+bigSister.Name);
				bigSister.Sibling = newFrame;								// attach as a sibling
				thisFrame = newFrame;										// and start work on the new one
			}
		}

		/// <summary>
		/// Read a transformation matrix and attach to the current frame
		/// </summary>
		/// <param name="node"></param>
		/// <param name="depth"></param>
		/// <param name="thisFrame"></param>
		private void ReadTransformationMatrix(XFileData node, int depth, SimFrame thisFrame)
		{
			try
			{
				Debug.WriteLine(Depth(depth) + "Creating Matrix for "+thisFrame.Name);
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
		private void ReadMesh(XFileData node, int depth, SimFrame thisFrame)
		{
			Debug.WriteLine(Depth(depth) + "Creating Mesh "+node.Name+" for "+thisFrame.Name);

			Mesh mesh = null;
			ExtendedMaterial[] materials = null;
			GraphicsStream adjacency = null;
			EffectInstance[] effects = null;

			try
			{
				mesh = Mesh.FromX(node,MeshFlags.Managed,Engine.Device,out adjacency, out materials, out effects);
				Debug.WriteLine(Depth(depth) + "Mesh "+node.Name+" has "+mesh.NumberVertices+" verts and "+materials.Length+" materials");
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
			// TODO: thisFrame.MeshContainer = CreateCytoplasm( ---stuff---);

			MeshData md = new MeshData();									// Cytoplasm currently expects a MeshData
			md.Mesh = mesh;													// rather than a simple mesh

			thisFrame.MeshContainer = new Cytoplasm(node.Name,md,materials,effects,adjacency,null);

		}

		/// <summary>
		/// Read a Matrix from an XFile node
		/// </summary>
		/// <param name="stream">GraphicsStream obtained from locking the Xfile data object</param>
		public Matrix MatrixFromXFile(GraphicsStream stream)
		{
			float v = 0;													// elements are floats
			Matrix m = Matrix.Identity;

			m.M11 = (float)stream.Read(v.GetType());		// read the matrix
			m.M12 = (float)stream.Read(v.GetType());
			m.M13 = (float)stream.Read(v.GetType());
			m.M14 = (float)stream.Read(v.GetType());
			m.M21 = (float)stream.Read(v.GetType());
			m.M22 = (float)stream.Read(v.GetType());
			m.M23 = (float)stream.Read(v.GetType());
			m.M24 = (float)stream.Read(v.GetType());
			m.M31 = (float)stream.Read(v.GetType());
			m.M32 = (float)stream.Read(v.GetType());
			m.M33 = (float)stream.Read(v.GetType());
			m.M34 = (float)stream.Read(v.GetType());
			m.M41 = (float)stream.Read(v.GetType());
			m.M42 = (float)stream.Read(v.GetType());
			m.M43 = (float)stream.Read(v.GetType());
			m.M44 = (float)stream.Read(v.GetType());

			return m;
		}

		private void ReadAnimationSet(XFileData node, int depth, SimFrame thisFrame)
		{
			Debug.WriteLine(Depth(depth) + "AnimationSet " + node.Name);
		}





	}

	class SimFrame
	{
		private string name = null;
		public string Name { get { return name; } set { name = value; } }
        
		private SimFrame firstChild = null;
		public SimFrame FirstChild { get { return firstChild; } set { firstChild = value; } }

		private SimFrame sibling = null;
		public SimFrame Sibling { get { return sibling; } set { sibling = value; } }

		private Matrix transformationMatrix = Matrix.Identity;
		public Matrix TransformationMatrix { get { return transformationMatrix; } set { transformationMatrix = value; } }

		private MeshContainer meshContainer = null;
		public MeshContainer MeshContainer { get { return meshContainer; } set { meshContainer = value; } }

		public SimFrame(string name)
		{
			this.name = name;
		}



		// WARNING: this is a class, so I need a Clone() function

	}


}
