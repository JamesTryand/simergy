// General-purpose structures, 3D maths, etc.
// used by main application and Physiology

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;



namespace Simbiosis
{




	/// <summary>
	/// Struct used for all orientations.
	/// Yaw, pitch, roll of zero means that object is pointing along positive Z-axis (into screen)
	/// </summary>
	public struct Orientation
	{
		public float Pitch;
		public float Yaw;
		public float Roll;

		public Orientation(float p, float y, float r)
		{
			Pitch = p;
			Yaw = y;
			Roll = r;
		}

		/// <summary>
		/// Addition operator: add one orientation to another
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		/// <returns></returns>
		public static Orientation operator+(Orientation op1,Orientation op2)
		{
			Orientation or = new Orientation (op1.Pitch+op2.Pitch,op1.Yaw+op2.Yaw,op1.Roll+op2.Roll);
			or.Normalise();
			return or;
		}

		/// <summary>
		/// Addition operator: add a ROTATION to an orientation
		/// </summary>
		/// <param name="op1">orientation</param>
		/// <param name="op2">rotation</param>
		/// <returns></returns>
		public static Orientation operator+(Orientation op1,Rotation op2)
		{
			Orientation or = new Orientation (op1.Pitch+op2.Pitch,op1.Yaw+op2.Yaw,op1.Roll+op2.Roll);
			or.Normalise();
			return or;
		}

		/// <summary>
		/// Force members into the range +/- PI after a rotation
		/// </summary>
		public void Normalise()
		{
			const float PI2 = (float)Math.PI * 2.0f;
			if (Pitch > PI2)
				Pitch -= PI2;
			else if (Pitch < -PI2)
				Pitch += PI2;

			if (Yaw > PI2)
				Yaw -= PI2;
			else if (Yaw < -PI2)
				Yaw += PI2;

			if (Roll > PI2)
				Roll -= PI2;
			else if (Roll < -PI2)
				Roll += PI2;

		}

		/// <summary>
		/// Force a given angle into range (use after single-angle rotations, etc.)
		/// </summary>
		/// <param name="angle"></param>
		/// <returns></returns>
		public static float Normalise(float angle)
		{
			const float PI2 = (float)Math.PI * 2.0f;
			if (angle > PI2)
				angle -= PI2;
			else if (angle < -PI2)
				angle += PI2;
			return angle;
		}

	}




	/// <summary>
	/// Struct used for all rotations.
	/// </summary>
	public struct Rotation
	{
		public float Pitch;
		public float Yaw;
		public float Roll;

		public Rotation(float p, float y, float r)
		{
			Pitch = p;
			Yaw = y;
			Roll = r;
		}

		/// <summary>
		/// Addition operator: add one rotation to another
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		/// <returns></returns>
		public static Rotation operator+(Rotation op1,Rotation op2)
		{
			return new Rotation (op1.Pitch+op2.Pitch,op1.Yaw+op2.Yaw,op1.Roll+op2.Roll);
		}

		/// <summary>
		/// Multiplication operator: useful for scaling a rotation by a scalar, e.g. by elapsed time
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		/// <returns></returns>
		public static Rotation operator*(Rotation op1,float scalar)
		{
			return new Rotation (op1.Pitch*scalar,op1.Yaw*scalar,op1.Roll*scalar);
		}

	}


	/// <summary>
	/// Sphere class for bounding spheres
	/// </summary>
	public struct Sphere
	{
		public Vector3 Centre;
		public float Radius;

		public Sphere(Vector3 centre, float radius)
		{
			Centre = centre;
			Radius = radius;
		}

		/// <summary>
		/// Return the SQUARE of the distance between this sphere's centre and another's
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public float CentreToCentreSq(Sphere other)
		{
			return Vector3.LengthSq(this.Centre - other.Centre);
		}

		/// <summary>
		/// Return the distance between this sphere's centre and another's
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public float CentreToCentre(Sphere other)
		{
			return Vector3.Length(this.Centre - other.Centre);
		}

		/// <summary>
		/// Test whether this sphere penetrates another, and by how far.
		/// </summary>
		/// <param name="other"></param>
		/// <returns>The distance by which my sphere penetrates the other, or -1 if they don't touch</returns>
		public float PenetrationBy(Sphere other)
		{
			// dist between cell centres (squared)
			float distSq = Vector3.LengthSq(this.Centre - other.Centre);
			// sum of the two radii
			float radii = this.Radius + other.Radius;
			// If the dist from centre to centre is smaller than the sum of the radii we are in contact
			if (distSq < (radii * radii))
			{
				// return the penetration distance
				return (radii - (float)Math.Sqrt(distSq));
			}
			return -1;
		}

		/// <summary>
		/// Test whether this sphere penetrates another.
		/// Faster than PenetrationBy() but doesn't return penetration distance (use if dist not needed)
		/// </summary>
		/// <param name="other"></param>
		/// <returns>The distance by which my sphere penetrates the other, or -1 if they don't touch</returns>
		public bool IsPenetrating(Sphere other)
		{
			// dist between cell centres (squared)
			float distSq = Vector3.LengthSq(this.Centre - other.Centre);
			// sum of the two radii
			float radii = this.Radius + other.Radius;
			// If the dist from centre to centre is smaller than the sum of the radii we are in contact
			if (distSq < (radii * radii))
				return true;
			return false;
		}

		/// <summary>
		/// Return true if sphere intersects AT ALL with this horizontal rectangle
		/// (used for culling)
		/// </summary>
		/// <param name="rect"></param>
		/// <returns></returns>
		public bool IsPenetrating(RectangleF rect)
		{
			// we need to know whether the centre is more than 1 radius outside the rect
			// in any direction
			if (((Centre.X+Radius)<rect.X)||
				((Centre.X-Radius)>rect.X+rect.Width)||
				((Centre.Z+Radius)<rect.Y)||
				((Centre.Z-Radius)>rect.Y+rect.Height))
				return false;
			return true;
		}

		/// <summary>
		/// Combine two bounding spheres to create a single sphere enclosing both.
		/// NOTE: When combining mesh bounds in a boned mesh, transform
		/// the new sphere by its frame transformation matrix before passing it to this method.
		/// ALSO: Zero the combined radius before calling for the first time, so that 0,0,0 doesn't
		/// get included as a valid sphere centre.
		/// The owner of this method is the combined sphere.
		/// </summary>
		/// <param name="newCentre">centre of sphere to add</param>
		/// <param name="newRadius">radius of sphere to add</param>
		public void CombineBounds(Vector3 newCentre, float newRadius)
		{
			// If the radius is 0 we've only just started, so just copy the new sphere into the
			// combined (otherwise we'll erroneously combine the sphere with 0,0,0)
			if (Radius==0)
			{
				Radius = newRadius;
				Centre = newCentre;
				return;
			}

			// Calc the distance between the two centres (keep as dist squared to save a sqrt if it's not needed)
			Vector3 displacement = newCentre - Centre; 
			float dist = Vector3.LengthSq(displacement);
			float newRadiusSq = newRadius * newRadius;				// use these to stay with square distance
			float RadiusSq = Radius * Radius;

			// If the new sphere is completely enclosed in the combined sphere, there's nothing to do
			if ((dist+newRadiusSq)<RadiusSq)
				return;
			
			// if the combined sphere is completely enclosed in the new one, the new one becomes the combined one
			if ((dist+RadiusSq)<newRadiusSq)
			{
				Radius = newRadius;
				Centre = newCentre;
				return;
			}

			// Otherwise the two spheres only partially overlap, if at all, so we need to combine them...

			// we need the actual distance now, not the square of it
			dist = (float)Math.Sqrt(dist);
			// The new combined radius is (the distance between centres + the two radii) / 2
			float resultRadius = (dist + newRadius + Radius) / 2.0f;
			// To find the new combined centre:
			//The centre must move half the change in diameter in the direction of
			// the new sphere. To do this, scale the VECTOR between the two centres so that its 
			// length is the amount to move (normalise then scale by amount), then add this to the centre.
			float move = (dist + newRadius - Radius) / 2.0f;
			displacement.Normalize();
			displacement.Scale(move);
			Centre += displacement;
			// store the new radius now we've finished with the old one
			Radius = resultRadius;
		}

		/// <summary>
		/// Combine two bounding spheres to create a single sphere enclosing both.
		/// Overload accepting a Sphere as argument
		/// </summary>
		public void CombineBounds(Sphere add)
		{
			CombineBounds(add.Centre, add.Radius);
		}


	}




	/// <summary>
	/// 3D maths utility functions
	/// </summary>
	public static class ThreeDee
	{
	
		
		/// <summary>
		/// Extract the Euler angles from the rotation in a transformation matrix
		/// </summary>
		/// <param name="m">the transform matrix containing the rotations</param>
		/// <returns>the oriention implied by the matrix</returns>
		public static Orientation GetYPR(Matrix m)
		{
			Orientation or = new Orientation(0,0,0);
			or.Yaw = (float)Math.Asin(m.M13);								// get the yaw from the matrix
			float c = (float)Math.Cos(or.Yaw);
			if ((c<-0.005)||(c>0.005))									// if c is zero we're gimbal locked 
			{
				or.Pitch = (float)Math.Atan2( -m.M23/c, m.M33/c);
				or.Roll = (float)Math.Atan2(-m.M12/c, m.M11/c);
			}
			else
			{
				// gimbal lock has occurred - pitch and roll axes are aligned
				or.Pitch = or.Roll = (float)Math.Atan2(m.M21, m.M22);
			}
			return or;
		}

		/// <summary>
		/// Return a debugging string showing a matrix as rotation and translation values
		/// </summary>
		/// <param name="m"></param>
		public static string DecodeMatrix(Matrix m)
		{
			Orientation or = GetYPR(m);

			float degrees = 1 / (float)Math.PI * 180.0f;
			or.Yaw  *= degrees;
			or.Pitch *= degrees;
			or.Roll *= degrees;
		
			return String.Format("X: {0:0####} Y: {1:0####} Z: {2:0####} - Yaw: {3:0##} Pitch: {4:0##} Roll: {5:0##}",
									m.M41, m.M42, m.M43, or.Yaw, or.Pitch, or.Roll);

		}

		/// <summary>
		/// Convert a quaternion to an axis and angle and return as a string
		/// </summary>
		/// <param name="q"></param>
		/// <param name="axis"></param>
		/// <param name="angle"></param>
		public static string DecodeQuaternion(Quaternion q)
		{
			Vector3 axis = new Vector3();
			float angle = 0;
			Quaternion.ToAxisAngle(q, ref axis, ref angle);
			return ("Axis: "+axis.X+","+axis.Y+","+axis.Z+" Angle: "+angle);
		}

		/// <summary>
		/// Move an object or frame of an object forward in its current direction
		/// </summary>
		/// <param name="m">the transformation matrix</param>
		/// <param name="dist">distance to travel</param>
		public static void MoveForward(ref Matrix m, float dist)
		{
			// set up the distance as a vector (with movement along z)
			Vector3 v = new Vector3(0,0,dist);
			// Transform this vector by the world matrix, thus rotating it into the object's orientation and
			// then adding the object's initial location
			v.TransformCoordinate(m);
			// This is now the new location, so copy it into the matrix
			m.M41 = v.X;
			m.M42 = v.Y;
			m.M43 = v.Z;
		}

		/// <summary>
		/// Compute bearing between two 2D points
		/// </summary>
		/// <param name="x1"></param>
		/// <param name="y1"></param>
		/// <param name="x2"></param>
		/// <param name="y2"></param>
		/// <returns></returns>
		public static float Bearing(float x1, float y1, float x2, float y2)
		{
			return (float)(Math.PI/2 - Math.Atan2(y2-y1, x2-x1));
		}

		/// <summary>
		/// Return the distance between two points
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		public static float Distance(Vector3 a, Vector3 b)
		{
			return Vector3.Length(a - b);
		}


		/// <summary>
		/// Given the three points of a triangle in clockwise order, return its face normal
		/// </summary>
		/// <param name="p1"></param>
		/// <param name="p2"></param>
		/// <param name="p3"></param>
		/// <returns></returns>
		public static Vector3 FaceNormal(Vector3 p1, Vector3 p2, Vector3 p3)
		{
			// create a vector from p1 to p2, and p1 to p3
			Vector3 v1 = p2 - p1;
			Vector3 v2 = p3 - p1;

			// The normalised cross-product of these is the face normal
			Vector3 norm = Vector3.Cross(v1,v2);
			return Vector3.Normalize(norm);
		}

		/// <summary>
		/// Construct tangent and binormal vectors from a vertex normal
		/// </summary>
		/// <param name="normal"></param>
		/// <param name="tangent"></param>
		/// <param name="binormal"></param>
		public static void TangentFromNormal(Vector3 normal, out Vector3 tangent, out Vector3 binormal)
		{
			Vector3 c1 = Vector3.Cross(normal, new Vector3(0.0f, 0.0f, 1.0f));
			Vector3 c2 = Vector3.Cross(normal, new Vector3(0.0f, 1.0f, 0.0f));

			if (Vector3.Length(c1) > Vector3.Length(c2))
			{
				tangent = Vector3.Normalize(c1);
			}
			else
			{
				tangent = Vector3.Normalize(c2);
			}

			binormal = Vector3.Normalize(Vector3.Cross(normal, tangent));
		}


		/// <summary>
		/// If another object is moving, find out how much of that movement is directly towards or away from me.
		/// Uses the decomposition of two vectors (Computer Graphics p.235)
		/// </summary>
		/// <param name="him">Location of the moving object</param>
		/// <param name="me">Location of the observer</param>
		/// <param name="hisMovement">Vector describing his absolute movement (speed/dirn)</param>
		/// <returns>the component of that vector that is facing me (positive is towards me, negative away)</returns>
		public static float RelativeMotion(Vector3 him, Vector3 me, Vector3 hisMovement)
		{
			Vector3 b = me - him;							// vector from him towards me
			return Vector3.Length(Vector3.Dot(hisMovement,b) / Vector3.LengthSq(b) * b);
		}

		/// <summary>
		/// Return all 8 corners of an AXIS-ALIGNED box, given the left-bottom-front and
		/// the right-top-back coordinates
		/// </summary>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <returns></returns>
		public static Vector3[] BoxFromMinMax(Vector3 min, Vector3 max)
		{
			Vector3[] corners = new Vector3[8];
			corners[0] = min;										// lbf 0
			corners[1] = new Vector3(min.X, min.Y, max.Z);			// lbb 1
			corners[2] = new Vector3(min.X, max.Y, min.Z);			// ltf 2
			corners[3] = new Vector3(min.X, max.Y, max.Z);			// ltb 3
			corners[4] = new Vector3(max.X, min.Y, min.Z);			// rbf 4
			corners[5] = new Vector3(max.X, min.Y, max.Z);			// rbb 5
			corners[6] = new Vector3(max.X, max.Y, min.Z);			// rtf 6
			corners[7] = max;										// rtb 7
			return corners;
		}


		/// <summary>
		/// Hack method to speed up Texture.FromBitmap(), which is increadibly slow in debug mode in VS2005 and probably even in release mode.
		/// According to the forums, the current MDX method does too many GetPixel() calls, which in .NET2 do extra interops. 
		/// Bitmap.Width and .Height involve interops (for some reason) and are a lot slower.
		/// HACK: Remove this code if MDX fixes the bug!
		/// http://lab.msdn.microsoft.com/productfeedback/viewfeedback.aspx?feedbackid=0dda86da-a83f-475d-8610-d3a08a14cc7f
		/// </summary>
		/// <param name="device"></param>
		/// <param name="bitmap"></param>
		/// <param name="usage"></param>
		/// <param name="pool"></param>
		/// <returns></returns>
		public static Texture FastTextureFromBitmap(Device device, Bitmap bitmap, Usage usage, Pool pool)
		{
			int bitmapWidth = bitmap.Width;
			int bitmapHeight = bitmap.Height;

			Texture texture = new Texture(device, bitmapWidth, bitmapHeight, 1,
											usage, Format.A8R8G8B8, pool);
			BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmapWidth, bitmapHeight),
											ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			int pitch;
			GraphicsStream textureData = texture.LockRectangle(0, LockFlags.None, out pitch);

			Debug.Assert(bitmap.PixelFormat == PixelFormat.Format32bppArgb);
			Debug.Assert(sizeof(int) == 4 && (bitmapData.Stride & 3) == 0 && (pitch & 3) == 0);

			unsafe
			{
				int* texturePointer = (int*)textureData.InternalDataPointer;
				for (int y = 0; y < bitmapHeight; y++)
				{
					int* bitmapLinePointer = (int*)bitmapData.Scan0 + y * (bitmapData.Stride / sizeof(int));
					int* textureLinePointer = texturePointer + y * (pitch / sizeof(int));
					int length = bitmapWidth;
					while (--length >= 0)
						*textureLinePointer++ = *bitmapLinePointer++;
				}
			}

			bitmap.UnlockBits(bitmapData);
			texture.UnlockRectangle(0);
			return texture;
		}





	}

	/// <summary>
	/// ******************* Support for non-standard vertex types ***********************
	/// </summary>


	/// <summary>
	/// BinormalVertex declaration for normal-mapped vertices
	/// This is like CustomVertex.PositionNormalTextured, except with tangent and binormal elements added. 
	/// The vertex type supports bump and normal mapping
	/// Binormals and tangents can be set from the normals in a mesh by calling mesh.ComputeTangent()
	/// </summary>
	public struct BinormalVertex
	{
		public Vector3 Position;
		public Vector3 Normal;
		public float Tu;
		public float Tv;
		public Vector3 Tangent;
		public Vector3 Binormal;


		private static VertexDeclaration binormal = null;			// Stores declaration object for multiple calls

		private static VertexElement[] binormalElements =			// The declaration
						new VertexElement[]							
                        {
                        new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
	                    new VertexElement(0, 12, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
	                    new VertexElement(0, 24, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
	                    new VertexElement(0, 32, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Tangent, 0),
	                    new VertexElement(0, 44, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.BiNormal, 0),
	                    VertexElement.VertexDeclarationEnd,
                        };

		/// <summary>
		/// Return a VertexDeclaration - not sure what actual USE this is!
		/// </summary>
		public static VertexDeclaration Declaration
		{
			get
			{
				// If we've not defined the type yet, do it now
				if (binormal == null)
				{
					Debug.Assert(Engine.Device != null);
					binormal = new VertexDeclaration(Engine.Device, binormalElements);
				}
				return binormal;
			}
		}
		/// <summary>
		/// Return the VertexElement[] array - as required for cloning meshes
		/// </summary>
		public static VertexElement[] VertexElements { get { return binormalElements; } }

		/// <summary>
		/// Return the VertexFormats flags
		/// </summary>
		public static VertexFormats Format
		{
			get
			{
				return VertexFormats.Position |
						VertexFormats.Normal |
						VertexFormats.Texture0;
			}
		}

		/// <summary> Size of a vertex in bytes </summary>
		public const int StrideSize = 56;
	



	}



	/// <summary>
	/// Static class for handling file resources - textures, etc.
	/// </summary>
	public class FileResource
	{

		/// <summary>
		/// Given a subfolder of the current directory and a file name, return the complete filespec
		/// </summary>
		/// <param name="folder">Name of the subfolder containing the file, or NULL if the file is in the application folder</param>
		/// <param name="filename"></param>
		/// <returns></returns>
		public static string Fsp(string folder, string filename)
		{
			string s = null;

			// Application folder files...
			if (folder==null)
				s = (Directory.GetCurrentDirectory() + "\\" + filename);
			// Subfolder files...
			else
				s = (Directory.GetCurrentDirectory() + "\\" + folder + "\\" + filename);
			return (s);
		}

	}



	/// <summary>
	/// This class produces a collection of Renderable objects SORTED BY DECREASING DISTANCE FROM CAMERA.
	/// This is needed when batching transparent objects (e.g. sprites). Such objects must be rendered in
	/// decreasing distance order so that their transparent parts don't fill the z-buffer and block objects
	/// that should be visible through them.
	/// 
	/// Use this collection to build batches of objects to be rendered.
	/// 
	/// Note: SortedList or SortedDictionary would be better, but they can't support identical keys
	/// (Maybe if a key existed I could increment the depth value slightly to make it unique, though?)
	/// 
	/// </summary>
	public class RenderBatch
	{
		private List<Renderable> batch = null;
		private int iterator = 0;

		/// <summary>
		/// Create a batch collection
		/// </summary>
		/// <param name="capacity">Initial or total batch size (can grow)</param>
		public RenderBatch(int capacity)
		{
			batch = new List<Renderable>(capacity);
		}

		/// <summary>
		/// Add a Renderable object to the batch
		/// </summary>
		/// <param name="newObj"></param>
		public void Add(Renderable newObj)
		{
			for (int i=0; i<batch.Count; i++)
			{
				if (batch[i].DistSq <= newObj.DistSq)
				{
					batch.Insert(i,newObj);
					return;
				}
			}
			batch.Add(newObj);
		}

		/// <summary>
		/// Add a Renderable object to the batch with no depth sorting (for objects that don't need it)
		/// </summary>
		/// <param name="newObj"></param>
		public void AddUnsorted(Renderable newObj)
		{
			batch.Add(newObj);
		}
		
		/// <summary>
		/// Start reading from the beginning
		/// </summary>
		public void Reset()
		{
			iterator = 0;
		}

		/// <summary>
		/// Read the next object from the list
		/// </summary>
		/// <returns></returns>
		public Renderable GetNext()
		{
			if (iterator < batch.Count)
				return batch[iterator++];
			else
				return null;
		}

		/// <summary>
		/// Delete all entries from the list
		/// </summary>
		public void Clear()
		{
			batch.Clear();
		}

		/// <summary>
		/// Return the number of entries
		/// </summary>
		public int Count { get { return batch.Count; } }

	}




}