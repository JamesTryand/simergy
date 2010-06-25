using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	/// <summary>
	/// MeshContainer-derived class representing (a portion of) a cell's skin (possibly a socket or effector)
	/// </summary>
	/// <remarks>
	/// - A Cytoplasm object is a MeshContainer, containing a single mesh (albeit in two versions). 
	/// - This mesh may represent all or only a section of a Cell. 
	/// - NOTE: In tutorials most people make a new copy of the original mesh when calling ConvertToBlendedMesh() - 
	///   but it is not clear why! It *could* be that we need individual instances of that mesh, although I see no evidence that
	///   it ever gets modified. For now, I replace the original loaded mesh with its progressive equivalent,
	///   but if necessary I could save the modified mesh off to a new member later.
	/// - Cytoplasm objects are NOT instanced: cloned Cells all refer to the same Cytoplasm objects and frames. 
	///   This is not a problem, because the WorkingMesh is rendered as soon as it has been animated, 
	///   so no stored state is required.
	/// - Cytoplasm objects are created by the HierarchyAllocator when loading Xfiles
	/// </remarks>
	public class Cytoplasm : MeshContainer
	{

		/// <summary>
		/// Visual styles for designer mode - specify which of the above materials (or the defaults) should be used
		/// </summary>
		public enum DesignStyle
		{
			Selected,												// the currently selected cell
			Unselected,												// an unselected cell in the selected organism
			Other,													// cells in unselected organisms or the cell being added (normal view)
		}

		// Base properties:
		// EffectInstance		- any effects associated with the mesh
		// MeshData				- original mesh and derived progressive mesh
		// Name					- mesh name
		// NextContainer		- next link in the mesh list for this Cell / Xfile

		/// <summary> Materials for each subset </summary>
		public Material[] materials;
		/// <summary> Textures for each subset </summary>
		public Texture[] textures;
		/// <summary> Normal maps for each subset </summary>
		private Texture[] bumps;

		/// <summary> a copy of the non-progressive mesh, needed for mouse picking tests </summary>
		private Mesh originalMesh = null;
		public Mesh OriginalMesh { get { return originalMesh; } }


		/// <summary> Approximate bounding sphere </summary>
		protected float boundRadius = 1.0f;
		public float BoundRadius { get { return boundRadius; } }
		protected Vector3 boundCentre = new Vector3();
		public Vector3 BoundCentre { get { return boundCentre; } }

		/// <summary>
		/// Bounding box
		/// </summary>	
		private Vector3 minOBB, maxOBB;


		/// <summary>
		/// Set this to false to prevent a mesh from being rendered (e.g. to switch off control meshes
		/// such as the meshes that mark sockets and effectors)
		/// </summary>
		public bool Visible = true;






		/// <summary>
		/// Constructor, called by HierarchyAllocator
		/// </summary>
		/// <param name="subfolder">textures are found in ../Cells/subfolder, where subfolder is the cell group name</param>
		/// <param name="name">mesh name (read from the X file)</param>
		/// <param name="mesh">the source mesh (as a standard mesh)</param>
		/// <param name="extmaterials">Materials and texture filenames</param>
		/// <param name="effectInstances">Effects</param>
		/// <param name="adjacency">Mesh adjacency data</param>
		/// <param name="skinInfo">Skin information for the mesh</param>
		public Cytoplasm(
			string subfolder,
			string name,
			Mesh mesh,
			ExtendedMaterial[] extmaterials,
			EffectInstance[] effectInstances,
			GraphicsStream adjacency,
			SkinInformation skinInfo)
		{

			// Store the name
			Name = name;

			// Keep the original mesh because this is needed for MousePick()
			// (ProgressiveMeshes don't have a .Intersect() method)
			originalMesh = mesh;

			// Store the materials
            int matlength = 0;
            if (extmaterials != null)
                matlength = extmaterials.Length;
			materials = new Material[matlength];
			textures = new Texture[matlength];
			bumps = new Texture[matlength];
			for (int i = 0; i < matlength; i++)
			{
				materials[i] = extmaterials[i].Material3D;
				// TRUESPACE HACK: Truespace doesn't allow me to set any ambient values in materials, 
				// which means that everything comes out black if it isn't lit by the diffuse light.
				// So add a default ambient value here for any black cells, to simulate reflection from terrain
				if ((materials[i].Ambient.R + materials[i].Ambient.G + materials[i].Ambient.B) == 0)
                    materials[i].Ambient = Color.FromArgb(68, 68, 68);

				if ((extmaterials[i].TextureFilename != null) && (extmaterials[i].TextureFilename !=
					string.Empty))
				{
					try
					{
						// We have a texture file, rather than an inline texture, so try to load it from ./cells/group/type
						textures[i] = TextureLoader.FromFile(Engine.Device,
							FileResource.Fsp(subfolder, extmaterials[i].TextureFilename));
						// Also attempt to load a normal map, if any
                        try
						{
							string filename = System.IO.Path.GetFileNameWithoutExtension(extmaterials[i].TextureFilename) + " Normal.PNG";
							bumps[i] = TextureLoader.FromFile(Engine.Device,
								FileResource.Fsp(subfolder, filename));
						}
						catch { }
					}
					catch
					{
						throw new SDKException("Failed to load texture " + FileResource.Fsp(subfolder, extmaterials[i].TextureFilename));
					}
				}
			}

			// Get access to the vertices to allow various operations
			// get the input vertices as an array (in case I want to modify them)
			VertexBuffer vb = mesh.VertexBuffer;						// Retrieve the vertex buffer data
            System.Array vert = null;                                   // We don't know if array will be PositionNormal or PositionNormalTextured, so use generic type

            if (mesh.VertexFormat == VertexFormats.PositionNormal)
            {
                vert = vb.Lock(0,
                    typeof(CustomVertex.PositionNormal),
                    LockFlags.ReadOnly,
                    mesh.NumberVertices);
            }
            else
            {
                vert = vb.Lock(0,
                    typeof(CustomVertex.PositionNormalTextured),
                    LockFlags.ReadOnly,
                    mesh.NumberVertices);
            }

            // compute the bounding sphere radius & centre from the vertices
            // NOTE: THIS VALUE IS FOR THE UNSCALED VERTICES and needs to be transformed by the combined transformation matrix
            boundRadius = Geometry.ComputeBoundingSphere(vert,
															mesh.VertexFormat,
															out boundCentre);

			// Calculate a bounding box for fine collision detection
            // NOTE: THIS VALUE IS FOR THE UNSCALED VERTICES and needs to be transformed by the combined transformation matrix
            Geometry.ComputeBoundingBox(vert, mesh.VertexFormat, out minOBB, out maxOBB);

			// gather useful debug info while we have the vertices
			//			Debug.WriteLine(String.Format("		Loaded mesh [{0}] from disk. {1} vertices, {2} textures, {3} materials",
			//											name,md.Mesh.NumberVertices,textures.Length, materials.Length));	
			//						Debug.WriteLine(String.Format("Mesh is centred on {0} with bound radius {1}; OBB is {2}:{3}",
			//											boundcentre, boundradius, OBBmin, OBBmax));

			vb.Unlock();
			vb.Dispose();														// vertices no longer needed

			// create a cleaned progressive mesh from the input
			using (Mesh clean = Mesh.Clean(CleanType.Optimization, mesh, adjacency, adjacency))
			{
				// From the cleaned mesh, create one that has binormals and tangents as well as normals
				// (ThreeDee.BinormalVertex). These are needed for normal mapping in the shader
				using (Mesh clone = clean.Clone(MeshFlags.Managed, BinormalVertex.VertexElements, Engine.Device))
				{
					// Add tangents and binormals
					clone.ComputeTangent(0, 0, 0, 0);
                    //clone.ComputeTangentFrame(0);

					// Create a new progressive mesh from the clean version
					MeshData md = new MeshData();
					md.ProgressiveMesh = new ProgressiveMesh(clone,
																adjacency,
																null,
																12,					// min acceptable # faces
																MeshFlags.SimplifyFace);
					this.MeshData = md;
				}

			}

			// Add a device reset handler to reload resources (if required)
			/////Engine.Device.DeviceReset += new System.EventHandler(OnReset);



		}

		/// <summary>
		/// // device reset event - rebuild all resources
		/// </summary>
		/// <param name="sender">ignored</param>
		/// <param name="e">ignored</param>
		public void OnReset(object sender, EventArgs e)
		{
			// NOTE: My meshes are flagged as MANAGED, so D3D keeps a system memory copy that persists
			// between device resets. 
		}

		/// <summary>
		/// Render the contents of this object's mesh in NORMAL textured mode
		/// </summary>
		/// <param name="frame">the frame of reference</param>
		/// <param name="lod">level of detail (as dist from camera), for progressive meshes</param>
		public void Render(JointFrame frame, float lod)
		{
			// This class of mesh may not currently be visible
			if (Visible == false)
				return;

			// set the LOD for the progressive mesh using the dist from camera to obj
			// (calculated in Map.RenderQuad())
			const float MAXDIST = 180.0f;				// use min LOD beyond this dist
			const float MINDIST = 15.0f;					// use max LOD nearer than this
			if (lod > MAXDIST)
				MeshData.ProgressiveMesh.NumberFaces = MeshData.ProgressiveMesh.MinFaces;
			else
			{
				int faces = (int)((float)MeshData.ProgressiveMesh.MaxFaces *
							(1 - ((lod - MINDIST) / (MAXDIST - MINDIST))));
				MeshData.ProgressiveMesh.NumberFaces = faces;
				/////Engine.DebugHUD.WriteLine(faces.ToString());
			}

			// Set up the world matrix
			Fx.SetWorldMatrix(frame.CombinedMatrix);

			// Render the subsets
			for (int i = 0; i < materials.Length; i++)
			{
				Fx.SetMaterial(materials[i]);
				Fx.SetTexture(textures[i], bumps[i]);
				Fx.DrawMeshSubset(MeshData.ProgressiveMesh, i);
			}

		}


		/// <summary>
		/// Render the contents of this object's mesh in DESIGN mode
		/// in which the material depends on the status of the cell (selected, part of selected org, etc.).
		/// Hotspots are also rendered differently depending on whether they are selected, part of the selected cell, etc.
		/// </summary>
		/// <param name="frame">the frame of reference</param>
		/// <param name="lod">level of detail (as dist from camera), for progressive meshes</param>
		/// <param name="diffuse">Overridden colour for mesh, or Color.Black to use the mesh's own colour</param>
		/// <param name="emissive">Overridden emissive colour for mesh</param>
		public void Render(JointFrame frame, float lod, Color diffuse, Color emissive)
		{
			// If the natural mesh colour is being overridden, set up a material of the given colour
			if (diffuse != Color.Black)
			{
				Material mat = new Material();
				mat.Ambient = Color.Black;
				mat.Diffuse = diffuse;
				mat.Emissive = emissive;
                mat.Specular = Color.Black;
				Fx.SetMaterial(mat);
			}

			// set the LOD for the progressive mesh using the dist from camera to obj
			// (calculated in Map.RenderQuad())
			const float MAXDIST = 180.0f;				// use min LOD beyond this dist
			const float MINDIST = 40.0f;				// use max LOD nearer than this  
			if (lod > MAXDIST)							// TODO: USE LARGE ENOUGH DIST TO ENSURE LAB IS FULLY RENDERED!!!!
				MeshData.ProgressiveMesh.NumberFaces = MeshData.ProgressiveMesh.MinFaces;
			else
			{
				int faces = (int)((float)MeshData.ProgressiveMesh.MaxFaces *
							(1 - ((lod - MINDIST) / (MAXDIST - MINDIST))));
				MeshData.ProgressiveMesh.NumberFaces = faces;
				/////Engine.DebugHUD.WriteLine(faces.ToString());
			}

			// Set up the world matrix
			Fx.SetWorldMatrix(frame.CombinedMatrix);

			// Render the subsets
			for (int i = 0; i < materials.Length; i++)
			{
				// If this is a selected cell or an unselected cell on the selected org, we already
				// have a special material set up. Otherwise, we should use the mesh subset's own material
				// so the object is rendered normally
				if (diffuse == Color.Black)
				{
					Fx.SetMaterial(materials[i]);
				}

				// Use mesh's texture
				Fx.SetTexture(textures[i]);

				Fx.DrawMeshSubset(MeshData.ProgressiveMesh, i);
			}
		}




		/// <summary>
		/// Helper for Cell.HitTest(). Test to see whether these two meshes currently intersect.
		/// Method: If all points on one OBB lie in front of any of the planes marking the faces of the other,
		/// then the two boxes don't overlap.
		/// </summary>
		/// <param name="ourFrame">The frame containing one mesh</param>
		/// <param name="hisFrame">Same data for other mesh</param>
		/// <returns>True if there will be a collision</returns>
		public static bool CollisionTest(JointFrame ourFrame, JointFrame hisFrame)
		{
			// Get the transformed corners for each mesh
			Vector3[] ourCorners = ((Cytoplasm)ourFrame.MeshContainer).GetTransformedOBB(ourFrame.CombinedMatrix);
			Vector3[] hisCorners = ((Cytoplasm)hisFrame.MeshContainer).GetTransformedOBB(hisFrame.CombinedMatrix);

			// Test our points against his planes. If we definitely don't intersect, return false
			if (CollisionTest2(ourCorners, hisCorners) == false)
				return false;
			// If we're still here, the boxes still might intersect, so test his points against our planes
			return CollisionTest2(hisCorners, ourCorners);
		}

		/// <summary>
		/// Helper for CollisionTest() - check one box's points against the other's planes
		/// </summary>
		/// <param name="pointsMin">OBB min for the box whose points we're testing</param>
		/// <param name="pointsMax">OBB max</param>
		/// <param name="lbf">OBB min for the box whose planes we're testing against</param>
		/// <param name="rtb">OBB max</param>
		/// <returns>False if the boxes definitely DON'T intersect; true if they MIGHT</returns>
		public static bool CollisionTest2(Vector3[] points, Vector3[] planeCorners)
		{
			// Construct six planes from one box
			Plane[] planes = new Plane[6];
			planes[0] = Plane.FromPoints(planeCorners[0], planeCorners[2], planeCorners[6]);	// front
			planes[1] = Plane.FromPoints(planeCorners[2], planeCorners[0], planeCorners[1]);	// left
			planes[2] = Plane.FromPoints(planeCorners[7], planeCorners[6], planeCorners[2]);	// top
			planes[3] = Plane.FromPoints(planeCorners[1], planeCorners[5], planeCorners[7]);	// back
			planes[4] = Plane.FromPoints(planeCorners[6], planeCorners[7], planeCorners[5]);	// right
			planes[5] = Plane.FromPoints(planeCorners[5], planeCorners[1], planeCorners[0]);	// bottom

			// For each plane, test all points against it. 
			// If ALL points lie in front of the plane the boxes DON'T intersect
			// and we can go home. 
			foreach (Plane plane in planes)
			{
				bool behind = false;
				foreach (Vector3 point in points)
				{
					if (plane.Dot(point) < 0)											// if any point lies behind this plane
					{																	// the plane is no good
						behind = true;
						break;
					}
				}
				if (behind == false)													// if NONE of the points lie behind this plane
				{																		// then the boxes definitely don't intersect
					return false;														// (signified by false)
				}
			}																			// otherwise, try another plane
			return true;																// true means we can't say for sure - try other way round
		}

		/// <summary>
		/// Return an array of 8 bounding box corners, transformed into world coordinates 
		/// </summary>
		/// <returns>True if there is a collision</returns>
		public Vector3[] GetTransformedOBB(Matrix frameMatrix)
		{
			// Get the axis-aligned corners for our mesh
			Vector3[] corners = ThreeDee.BoxFromMinMax(minOBB, maxOBB);

			// Transform points into world coordinates
			for (int i = 0; i < 8; i++)
			{
				corners[i] = Vector3.TransformCoordinate(corners[i], frameMatrix);
			}
			return corners;
		}

 

	}




}
