using System;
using System.Diagnostics;
using System.Collections.Generic;
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

		private static List<Marker> list = new List<Marker>();

		public enum Type
		{
			Sphere,
			Cone
		};

		/// <summary>
		/// Create a new SPHERE marker
		/// </summary>
		/// <param name="color">ARGB value (use A for transparency)</param>
		/// <param name="posn">World coordinates for position</param>
		/// <param name="radius">Radius of sphere</param>
		/// <returns></returns>
		public static Marker CreateSphere(Color color, Vector3 posn, float radius)
		{
			Marker m = new Marker(color, posn, radius);
			list.Add(m);
			return m;
		}

		/// <summary>
		/// Create a new SPHERE marker
		/// </summary>
		/// <param name="color">ARGB value (use A for transparency)</param>
		/// <param name="posn">World coordinates of TIP</param>
		/// <param name="size">Radius of base</param>
		/// <returns></returns>
		public static Marker CreateCone(Color color, Vector3 posn, Orientation orientation, float angle, float height)
		{
			Marker m = new Marker(color, posn, orientation, angle, height);
			list.Add(m);
			return m;
		}

		/// <summary>
		/// A new D3D device has been created.
		/// if Engine.IsFirstDevice==true, create those once-only things that require a Device to be available
		/// Otherwise, rebuild all those resources that get lost when a device is destroyed during a windowing change etc.
		/// </summary>
		public static void OnDeviceCreated()
		{
			Debug.WriteLine("Marker.OnDeviceCreated()");
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
			Debug.WriteLine("Marker.OnDeviceLost()");
			Debug.WriteLine("(does nothing)");
		}

		/// <summary>
		/// Device has been reset - rebuild unmanaged resources
		/// </summary>
		public static void OnReset()
		{
			Debug.WriteLine("Marker.OnReset()");
			Debug.WriteLine("(does nothing)");
		}


		/// <summary>
		/// Render all markers
		/// </summary>
		public static void Render()
		{

			// --------------------- Set Renderstate ------------------
			Fx.SetTexture(null);								// Use no texture at all (material-only objects)
			Engine.Device.RenderState.ZBufferEnable = true;								// enabled
			Fx.SetMarkerTechnique();
			// --------------------------------------------------------


			foreach (Marker m in list)
			{
				// Set world matrix
				Fx.SetWorldMatrix(m.matrix);
				// Define the material for lighting
				Fx.SetMaterial(m.material);

				// draw the mesh
				Fx.DrawMeshSubset(m.mesh, 0);
			}

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
		private Orientation orientation = new Orientation(0,0,0);
		private Matrix matrix = Matrix.Identity;


		/// <summary>
		/// Create a SPHERE marker
		/// </summary>
		/// <param name="color"></param>
		/// <param name="posn"></param>
		/// <param name="radius"></param>
		private Marker(Color color, Vector3 posn, float radius)
		{
			// Create the mesh
			mesh = Mesh.Sphere(Engine.Device,1.0f,16,16);

			// create the material
//			material.Ambient = color;
			material.Diffuse = color;
//			material.Specular = color;
			material.Emissive = color;

			Goto(posn);
			Scale(radius);
		}

		/// <summary>
		/// Create a CONE marker, with the tip at posn
		/// </summary>
		/// <param name="color"></param>
		/// <param name="posn"></param>
		/// <param name="angle"></param>
		/// <param name="height"></param>
		/// <param name="orientation"></param>
		private Marker(Color color, Vector3 posn, Orientation orientation, float angle, float height)
		{
			// Work out the width of the base from the angle
			float radius = (float)Math.Tan(angle) * height;

			// Create the mesh, centred at 0,0,0
			mesh = Mesh.Cylinder(Engine.Device,0,radius,height,16,1);

			// Shift the cone so that the tip is at 0,0,0
			// and orient it to be aligned with the Y axis (which is how Truespace objects come out when at a YPR of 0,0,0)
			Matrix mat = Matrix.Translation(0,0,height/2)
					   * Matrix.RotationYawPitchRoll(0,-(float)Math.PI/2,0);
						
			VertexBuffer vb = mesh.VertexBuffer;						// Retrieve the vertex buffer data
			CustomVertex.PositionNormal[] vert;
			// get the input vertices as an array (in case I want to modify them)
			vert = (CustomVertex.PositionNormal[])vb.Lock(0,
				typeof(CustomVertex.PositionNormal),
				0,
				mesh.NumberVertices);		

			for (int i=0; i<mesh.NumberVertices; i++)
			{
				//vert[i].Z = vert[i].Z + height / 2;
				vert[i].Position = Vector3.TransformCoordinate(vert[i].Position,mat);
				vert[i].Normal = Vector3.TransformNormal(vert[i].Normal, mat);
			}

			vb.Unlock();

			// create the material
			material.Ambient = color;
			material.Diffuse = color;
			material.Specular = color;
			material.Emissive = color;

			Goto(posn, orientation);
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

		/// <summary>
		/// Move and orient this marker
		/// </summary>
		/// <param name="locn"></param>
		/// <param name="orient"></param>
		public void Goto(Vector3 locn, Orientation orient)
		{
			location = locn;
			orientation = orient;
			SetMatrix();
		}

		/// <summary>
		/// Move and orient this marker using a matrix (e.g. for showing sensor Cones)
		/// </summary>
		/// <param name="locn"></param>
		/// <param name="orient"></param>
		public void Goto(Matrix mat)
		{
			matrix = mat;
		}

		public void Scale(float size)
		{
			scale = size;
			SetMatrix();
		}

		private void SetMatrix()
		{
			matrix = Matrix.Scaling(new Vector3(scale,scale,scale))
				* Matrix.RotationYawPitchRoll(orientation.Yaw, orientation.Pitch, orientation.Roll)
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
