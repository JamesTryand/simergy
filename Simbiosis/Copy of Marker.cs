using System;
using System.Diagnostics;
using System.Collections;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	/// <summary>
	/// A 3D sphere for debugging
	/// </summary>
	public class Marker
	{

		//---------- STATIC MEMBERS = list of markers -------------

		private static ArrayList list = new ArrayList();

		/// <summary>
		/// Create a new marker
		/// </summary>
		/// <param name="color">ARGB value (use A for transparency)</param>
		/// <param name="size">Radius of sphere</param>
		/// <param name="posn">World coordinates for position</param>
		/// <returns></returns>
		public static Marker Create(Color color, float size, Vector3 posn)
		{
			Marker m = new Marker(color, size, posn);
			list.Add(m);
			return m;
		}

		/// <summary>
		/// Render all markers
		/// </summary>
		public static void Render()
		{
			// enable transparency
			Engine.Device.RenderState.DiffuseMaterialSource = ColorSource.Material;
			Engine.Device.RenderState.SourceBlend = Blend.SourceColor;
			Engine.Device.RenderState.DestinationBlend = Blend.InvSourceAlpha;
			Engine.Device.RenderState.BlendOperation = BlendOperation.Add;
			Engine.Device.RenderState.AlphaBlendEnable = true;

			foreach (Marker m in list)
			{
				// Set world matrix
				Engine.Device.Transform.World = m.matrix;
				// Define the material for lighting
				Engine.Device.Material = m.material;

				// draw the mesh
				m.mesh.DrawSubset(0);
			}

			// Reset transparency
			/// HACK: This is wrong - if I don't do it, all the organisms are transparent, even though their
			/// materials and textures aren't. (Note: Truespace doesn't save alpha values in materials anyway!)
			Engine.Device.RenderState.SourceBlend = Blend.SourceAlpha;
			Engine.Device.RenderState.DestinationBlend = Blend.InvSourceAlpha;
			Engine.Device.RenderState.AlphaBlendEnable = false;
		}

		/// <summary>
		/// Move a marker by index number
		/// </summary>
		/// <param name="index"></param>
		/// <param name="posn"></param>
		public static void Goto(int index, Vector3 posn)
		{
			((Marker)list[index]).Goto(posn);
		}

		/// <summary>
		/// Delete a marker by index (will change all other indices!!!)
		/// </summary>
		/// <param name="index"></param>
		public static void Delete(int index)
		{
			list.RemoveAt(index);
		}


		// ---------- DYNAMIC MEMBERS

		private Mesh mesh = null;
		private Material material = new Material();
		private Vector3 location = new Vector3();
		private float scale = 1.0f;
		private Matrix matrix = Matrix.Identity;


		private Marker(Color color, float size, Vector3 posn)
		{
			// Creat the mesh
			mesh = Mesh.Sphere(Engine.Device,1.0f,16,16);

//			VertexBuffer vb = mesh.VertexBuffer;						// Retrieve the vertex buffer data
//			CustomVertex.PositionNormal[] vert;
//			// get the input vertices as an array (in case I want to modify them)
//			vert = (CustomVertex.PositionNormal[])vb.Lock(0,
//				typeof(CustomVertex.PositionNormal),
//				0,
//				mesh.NumberVertices);		
//
//			vb.Unlock();


			// create the material
//			material.Ambient = color;
			material.Diffuse = color;
//			material.Specular = color;
//			material.Emissive = color;

			Goto(posn);
			Scale(size);
		}

		/// <summary>
		/// Move this marker
		/// </summary>
		/// <param name="locn"></param>
		public void Goto(Vector3 locn)
		{
			location = locn;
			SetMatrix();
		}

		public void Scale(float size)
		{
			scale = size;
			SetMatrix();
		}

		private void SetMatrix()
		{
			matrix = Matrix.Scaling(new Vector3(scale,scale,scale))
				* Matrix.Translation(location);
		}

		/// <summary>
		/// Remove this marker from the list
		/// </summary>
		public void Delete()
		{
			mesh.Dispose();
			list.Remove(this);
		}


	}
}
