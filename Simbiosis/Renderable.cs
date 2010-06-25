using System;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	/// <summary>
	/// Base class for all objects stored in map - organisms, tiles, sprites, etc.
	/// </summary>
	/// <remarks>
	/// TODO FOR SUBCLASSES:
	/// - define appropriate flag bits on creation
	/// - set absSphere from relSphere each frame or so (e.g. via appropriate transformation matrix)
	/// </remarks>
	public abstract class Renderable : IDisposable
	{
		/// <summary> Bounding sphere of whole object, for scene culling and approximate collision detection </summary>
		protected Sphere RelSphere = new Sphere();		// sphere relative to object
		public Sphere AbsSphere = new Sphere();			// sphere in world coordinates (subclass must set each frame)

		/// <summary>
		/// Various status and property flags. Inherited classes must set these as appropriate.
		/// </summary>
		public bool	RenderMe = false;					// ...must be rendered THIS FRAME
		public bool Dynamic = false;					// ...responds to forces (on for creatures, off for fixed objects such as the Lab)


		/// <summary> 
		/// dist from camera SQUARED (valid only for RenderMe==true objects)
		/// Used to compute LOD for progressive meshes, and to sort renderable tiles etc. in front-back order.
		/// </summary>
		public float DistSq = 0;	



		public Renderable()
		{
		}

		/// <summary>
		/// do all those things that need doing regardless of whether
		/// the object is visible
		/// </summary>
		public virtual void Update()
		{
		}
		
		/// <summary>
		/// Do any pre-rendering stuff, e.g. renderstate changes. 
		/// All objects rendered between here and the call to Render 
		/// are guaranteed to be of the same class
		/// </summary>
		public static void PreRender()
		{
		}

		/// <summary>
		/// Call once to render the batch of objects
		/// </summary>
		/// <param name="clear">True if the batch should be cleared after rendering (i.e. false when rendering shadow map)</param>
		public static void Render(bool clear)
		{
		}

		/// <summary>
		/// Add this object to the batch to be rendered
		/// </summary>
		public virtual void AddToRenderBatch()
		{
		}

		/// <summary>
		/// Dispose of resources
		/// </summary>
		public virtual void Dispose()
		{
		}

		/// <summary>
		/// Add a quad that you now occupy to your list, IF YOU HAVE ONE (called by quad tree)
		/// Base method does nothing - only mobile objects need implement a list of
		/// their own, so that they can check whether they've crossed a quad boundary
		/// </summary>
		/// <param name="q"></param>
		public virtual void AddMap(Map q)
		{
		}

		/// <summary>
		/// Remove a quad that you no longer occupy from your list (called by quad tree)
		/// </summary>
		/// <param name="q"></param>
		public virtual void DelMap(Map q)
		{
		}

		/// <summary>
		/// Return true if my bounding sphere intersects at all with this horizontal rectangle
		/// (used for culling)
		/// </summary>
		/// <param name="rect"></param>
		/// <returns></returns>
		public bool IsIn(RectangleF rect)
		{
			return AbsSphere.IsPenetrating(rect);
		}

		/// <summary>
		/// The given cell is in collision with our bounding sphere. Test to see if it actually collides with
		/// one of my parts. If so, return a Vector describing the force we exert on the offending cell
		/// </summary>
		/// <param name="cell">The cell that may have collided with us</param>
		/// <returns> Any force vector acting on the cell </returns>
		public virtual Vector3 CollisionTest(Cell otherCell)
		{
			// default is no force
			return Vector3.Empty;
		}



	}
}
