using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	// DATA TYPES THAT ARE SHARED BETWEEN CELL TYPES AND THE MAIN APPLICATION
	// and hence need to be stored in this DLL



	/// <summary>
	/// Implement this interface for all objects that need to be interrogated by sensors.
	/// The contract enables the sensor to establish facts about the object's physical situation and attributes,
	/// and compute arbitrary sensory data from them
	/// </summary>
	public interface IDetectable
	{
		/// <summary> Return absolute location </summary>
		Vector3 ReqLocation();
		/// <summary> Return absolute velocity as a vector </summary>
		Vector3 ReqVelocity();
		/// <summary> Total mass </summary>
		float ReqMass();
		/// <summary> Total dimensions as a sphere radius </summary>
		float ReqSize();
        /// <summary> Object's most significant colour(s) (e.g. for light sensors) </summary>
        List<ColorValue> ReqSpectrum();
        /// <summary> Object's depth in the water as a fraction (0=shallow, 1-deep) </summary>
        /// <returns></returns>
        float ReqDepth();

		/// <summary> You've been sent a Stimulus. Handle it if possible, or hand it on to 
		/// one of your parts for handling</summary>
		/// <param name="stimulus">The stimulus information</param>
		/// <returns>Return true if the stimulus was handled</returns>
		bool ReceiveStimulus(Stimulus stimulus);
	}


	/// <summary>
	/// The Cell class is the only type that inherits this interface.
	/// It exists to allow the cell types (Physiology subclasses) to send commands back 
	/// to the Cells that own them. (All other Cell methods being unknown to this DLL)
    /// Physiology classes call these through their owner. member.
	/// </summary>
	public interface IControllable
	{

		/// <summary> 
		/// Return a list of all the objects of a given type in range of a given hotspot,
		/// as well as their relative angle and distance.
		/// Cell types use this for implementing active sensors and effectors
		/// </summary>
        /// <param name="spot">Which hotspot to use (-1 = no hotspot, e.g. entire cell is the emitter or sensor. Avoid when angle of acceptance matters)</param>
        /// <param name="range">Current range of hotspot</param>
		/// <param name="includeOrganisms">Set true to include organisms in the list</param>
		/// <param name="includeTerrain">Set true to include tiles in the list</param>
		/// <param name="lineOfSightOnly">Set true to exclude objects obscured by terrain</param>
		/// <returns>An array of SensorItems, containing the data</returns>
		SensorItem[] GetObjectsInRange(	int spot, float range,
										bool includeOrganisms, bool includeTerrain,
										bool lineOfSightOnly);

        /// <summary>
        /// Given a stimulus, find out how that object is positioned relative to a given hotspot on this cell. 
        /// Cell types use this to establish how much notice they should take of an incoming stimulus (passive sensing). 
        /// Call methods on the returned SensorItem to calculate the angle and/or distance as required.
        /// </summary>
        /// <param name="spot">Which hotspot to use</param>
        /// <param name="range">Current range of hotspot</param>
        /// <param name="stim">the stimulus we received</param>
        SensorItem TestStimulusVisibility(int spot, float range, Stimulus stim);

		/// <summary>
		/// Apply a positive or negative propulsive force in the drection of a hotspot's normal
		/// </summary>
		/// <param name="spot">Index of the hotspot acting as the effector</param>
		/// <param name="amount">Signed value of the force to apply</param>
		void JetPropulsion(int spot, float amount);

		/// <summary>
		/// Send a debug message out to the display console (can't access Engine directly from Physiology)
		/// </summary>
		/// <param name="msg"></param>
		void ConsoleMessage(string msg);

		/// <summary>
		/// Fetch the socket number on my parent to which I'm attached.
		/// Used internally for sending/receiving nerve signals. Not relevant for SDK use.
		/// </summary>
		/// <returns></returns>
		int MySocketNumber();

		/// <summary>
		/// Write cell output to a channel
		/// </summary>
		/// <param name="chan"></param>
		/// <param name="value"></param>
		void SetChannel(int chan, float value);

		/// <summary>
		/// Read cell input from a channel
		/// </summary>
		/// <param name="chan"></param>
		float GetChannel(int chan);


        /// <summary>
        /// Set the (sole) material colour of a given anim# frame. Used for colour animations such as blushing or pulsing.
        /// Colour animateable meshes must be defined as anim0... but needn't have any physical animations defined.
        /// </summary>
        /// <param name="index">index of frame in joint[] array</param>
        /// <param name="diffuse">diffuse colour</param>
        /// <param name="emissive">emissive colour</param>
        void SetAnimColour(int index, ColorValue diffuse, ColorValue emissive);

        /// <summary>
        /// Return our variant of the celltype
        /// </summary>
        int Variant { get;}

        /// <summary>
        /// The unique name of the organism owning us (for debug messages, etc.)
        /// </summary>
        /// <returns></returns>
        string OwnerOrganism();

        /// <summary>
        /// Return a unique identifier for this cell instance. Useful for debugging.
        /// </summary>
        /// <returns></returns>
        string UniqueName();


        /// <summary>
        /// If this cell is part of a cameraship organism, return the index of the currently active panel.
        /// This allows Physiology classes to select the right camera mount when a cameraship offers several panels, each with a different view into the scene
        /// (such as the front porthole and observation bubble in the sub).
        /// </summary>
        /// <returns>the currently active panel # if this cell belongs to a cameraship, or -1 if it doesn't</returns>
        int CurrentPanel();

        /// <summary>
        /// Create/update a Marker cone for showing a sensor (or stimulus-generating) celltype's receptive field. 
        /// Used for debugging, so that we know when another object is within sight.
        /// Call this during the celltype's FastUpdate() method.
        /// </summary>
        /// <param name="cone">Up to two markers can be created. Use an index of 0 or 1 here to define which is being built</param>
        /// <param name="hotspot">sensor hotspot</param>
        /// <param name="range">sensor range</param>
        /// <param name="halfAngle">divergence in radians from the hotspot normal</param>
        void ShowSensoryField(int cone, int hotspot, float range, float halfAngle);


        /// <summary>
        /// Return a matrix that represents the position and orientation of a hotspot's FACE NORMAL
        /// (i.e. the direction the hotspot is looking). Used for positioning the camera, etc.
        /// </summary>
        /// <param name="spot">Index of the hotspot to examine</param>
        /// <returns>A matrix positioned at the hotspot and oriented facing along its normal</returns>
        Matrix GetHotspotNormalMatrix(int spot);

        /// <summary>
        /// Return a matrix that represents the world position and orientation of a hotspot (NOT its normal)
        /// </summary>
        /// <param name="spot">Index of the hotspot to examine</param>
        /// <returns>The hotspot's combined matrix</returns>
        Matrix GetHotspotMatrix(int spot);

        /// <summary>
        /// Return the world coordinates of a hotspot
        /// </summary>
        /// <param name="spot"></param>
        /// <returns></returns>
        Vector3 GetHotspotLocation(int spot);

        /// <summary>
        /// Cell's current position
        /// </summary>
        Vector3 Location { get; }




    }

	/// <summary>
    /// //////////////////////////////////////////////////////////////////////////////////////////////////////////
	/// SensorItem: Used for sensing.
	/// IControllable.GetObjectsInRange() returns an ArrayList of these structs, containing information about
	/// objects within range of a hotspot, including the position of the object relative to the hotspot's frame.
	/// Cell types can use this information to determine which of the listed objects is within a cone of
	/// acceptance, or how strongly they affect a sensor, or which image pixels they stimulate
	/// </summary>
	public struct SensorItem
	{
		public IDetectable Object;					// When sending a stimulus, this is the recipient. When receiving one this is the sender.
		public Vector3 RelativePosition;			// The position of the object relative to the hotspot's coordinate frame

		public SensorItem(IDetectable obj, Vector3 relpos)
		{
			Object = obj;
			RelativePosition = relpos;
		}

		/// <summary>
		/// Calculate the angle between the hotspot's line of sight and the object.
		/// This doesn't tell us anything about the direction of the object, just how far away from our 
		/// line of sight or hearing it is.
		/// Some sensors will have a limited field of view. If an object lies outside a certain half-angle 
		/// then it isn't visible/audible.
		/// (Be careful with narrow cones, since an object's centre may fall outside the cone when the object is very close, and
		/// so it won't be visible, even though large parts of it are in the field of view.
		/// </summary>
		/// <returns>absolute angle from line-of-sight, in radians</returns>
		public float Angle()
		{
			return  (float)Math.Abs(Math.Acos(Vector3.Dot(Vector3.Normalize(RelativePosition),
												 new Vector3(0,1,0)) ));
		}

        /// <summary>
        /// Calculate the distance to the object as a fraction of the given range (1=close, 0=at limit of range).
        /// This can be used by a cell type to attenuate a stimulus in proportion to distance.
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public float Distance(float range)
        {
            float dist = Distance();                                                // get absolute distance
            dist = 1.0f - dist / range;												// convert to range 0 - 1 (1=close)
            if (dist < 0) dist = 0;
            if (dist > 1f) dist = 1f;
            return dist;
        }

        /// <summary>
        /// Calculate the distance to the object in world units
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public float Distance()
        {
            // distance = length of the hotspot-relative position minus the object's radius
            float dist = Vector3.Length(RelativePosition) - Object.ReqSize();
            if (dist < 0) dist = 0;                                                 // If we're inside the object's sphere then dist = 0
            return dist;
        }

        /// <summary>
        /// Calculate the apparent size of the object, given its actual size and its distance.
        /// </summary>
        /// <returns>HALF the angle subtended by the object, in radians. </returns>
        public float AngleSubtended()
        {
            float radius = Object.ReqSize();
            float dist = Vector3.Length(RelativePosition);
            return (float)Math.Atan(radius / dist);
        }

	}

	
	/// <summary>
	/// Represents a stimulus emitted by an effector (or other source). Stimuli can be interpreted in various different
	/// ways - visual or sonic changes picked up by passive sensors, commands to be eaten, or become injected with a poison,
	/// or whatever. Stimuli can be broadcast by 'sight', 'sound' or 'touch', from an effector.
	/// 
	/// NOTE: Stimuli are sent to Terrain tiles and Organisms, NOT cells. 
	/// If the Organism doesn't know how to handle it, it passes it on to each of its cells in turn. 
	/// Each cell must examine the TransmitterLocn and TransmissionRange information to decide whether or not the 
	/// stimulus applies to it. 
	/// If it applies but the Cell doesn't know how to process the stimulus, it passes it on to its Physiology.
	/// 
    /// BUILT-IN STIMULI:
    /// 
    /// "sound":
    ///     1st param = float pitch (0 low, 1 high) - useful for pitch-selective sensors
    ///     2nd param = string soundfile to be played if this sound is picked up by the current CameraShip (Could also be used for species-specific calls)
    /// 
	/// 
	/// </summary>
	public class Stimulus
	{
		/// <summary> The Cell that emitted the stimulus </summary>
		public IDetectable From = null;

		/// <summary> 
		/// TransmitterLocation + TransmitterRange = the sphere over which the stimulus was transmitted
		/// (for determining which cells in the receiving organism are affected).
		/// </summary>
		public Vector3 TransmitterLocn = new Vector3();
		public float TransmissionRange = 0;

		/// <summary> Arbitrary stimulus type </summary>
		public string Type = null;
		/// <summary> Up to four type-dependent parameters </summary>
		public Object Param0 = null;
		public Object Param1 = null;
		public Object Param2 = null;
		public Object Param3 = null;

		/// <summary> Set this true if replying to a stimulus </summary>
		public bool IsReply = false;

        /// <summary>
        /// Construct a stimulus for emission
        /// </summary>
        /// <param name="from">IDetectable object (e.g. Cell) emitting stimulus</param>
        /// <param name="loc">Location of the emitter (Cell or its effector hotspot)</param>
        /// <param name="range">Radius over which the stimulus can be sensed</param>
        /// <param name="type">String describing type of stimulus</param>
        /// <param name="param0">Type-dependent parameter</param>
        /// <param name="param1">Type-dependent parameter</param>
        /// <param name="param2">Type-dependent parameter</param>
        /// <param name="param3">Type-dependent parameter</param>
		public Stimulus(IDetectable from, Vector3 loc, float range, 
						string type, Object param0, Object param1, Object param2, Object param3)
		{
			From = from;
			TransmitterLocn = loc;
			TransmissionRange = range;
			Type = type;
			Param0 = param0;
			Param1 = param1;
			Param2 = param2;
			Param3 = param3;
		}

		/// <summary>
		/// Reply to a stimulus (type-dependent), supplying up to 4 objects as data.
		/// Can be used for transactions (e.g. I'd like to eat this much of your energy, how much did I actually get?)
		/// </summary>
		/// <param name="from"></param>
		/// <param name="param0"></param>
		/// <param name="param1"></param>
		/// <param name="param2"></param>
		/// <param name="param3"></param>
		public void Reply(IDetectable from, Object param0, Object param1, Object param2, Object param3)
		{
			IDetectable to = from;
			IsReply = true;
			From = from;
			Param0 = param0;
			Param1 = param1;
			Param2 = param2;
			Param3 = param3;
			to.ReceiveStimulus(this);
		}

	}

	/// <summary>
	/// Navigational commands from kbd/mouse/joystick.
	/// Used to send user data to the current camera ship's physiology
	/// </summary>
	public class TillerData
	{
		/// <summary> XY and throttle data from real or virtual joystick, in the range +/-1 </summary>
		public Vector3 Joystick = new Vector3();			

		/// <summary> Thrust data (e.g. when user presses SPACE bar or joystick button to move camera ship forward) </summary>
		public float Thrust = 0;
	}

	/// <summary>
	/// Exception type thrown when a Physiology class does something wrong, 
	/// indicating that an SDK programmer has made a mistake
	/// </summary>
	public class SDKException : Exception
	{
		public SDKException()
		{
		}
		public SDKException(string message)
			: base(message)
		{
		}
		public SDKException(string message, Exception inner)
			: base(message, inner)
		{
		}

	}

	/// <summary>
	/// Channel data element. Each Physiology subclass must return an array of these in its GetChannelData() method,
	/// to describe the plug/sockets to which each channel is connected, and what chemical it is sensitive to by default.
	/// 
    /// NOTE:
    /// -   If a default value is defined, the chemical selectivity should also normally be set to 0 when the ChannelData is defined, so that the 
    ///     channel's signal will be driven by this constant by default.
    /// 
	/// </summary>
	public class ChannelData
	{
		/// <summary>
		/// Integer describing the socket number to which one end of a channel is attached
		/// (or PL for the plug or XX for no connection at this end of channel)
		/// </summary>
		public enum Socket
		{
            /// <summary> No connection at this end </summary>
			XX = -2,
            /// <summary> Plug </summary>
			PL = -1,
			S0 = 0,
			S1,
			S2,
			S3,
			S4,
			S5,
			S6,
			S7,
			S8,
			S9
		};


		/// <summary> Source plug/skt for input or bypass channels </summary>
		public Socket Source = Socket.XX;
		/// <summary> Destination plug/skt for output or bypass channels </summary>
		public Socket Dest = Socket.XX;
		/// <summary> Default/initial chemical sensitivity </summary>
		public int Chemical = 0;
        /// <summary> Initial value to use as the user-definable constant, for channels with no chemical affinity </summary>
        public float Constant = 0f;
		/// <summary> Name of channel for UI </summary>
		public string Name = "<unknown>";

		/// <summary>
		/// Construct a channel
		/// </summary>
        /// <param name="source"> Source plug/skt for input or bypass channels </param>
        /// <param name="dest"> Destination plug/skt for output or bypass channels </param>
        /// <param name="chemical"> Default/initial chemical sensitivity </param>
        /// <param name="constant"> Initial value of user-definable constant, for unconnected channels</param>
        /// <param name="name"> Name of channel for UI </param>
		public ChannelData(Socket source, Socket dest, int chemical, float constant, string name)
		{
			Source = source;
			Dest = dest;
			Chemical = chemical;
            Constant = constant;
			Name = name;
		}

		/// <summary>
		/// Returns true if this is an input channel
		/// </summary>
		/// <returns></returns>
		public bool IsInput()
		{
			return (Dest == Socket.XX) ? true : false;
		}

		/// <summary>
		/// Returns true if this is an output channel
		/// </summary>
		/// <returns></returns>
		public bool IsOutput()
		{
			return (Source == Socket.XX) ? true : false;
		}

		/// <summary>
		/// Returns true if this is a bypass channel
		/// </summary>
		/// <returns></returns>
		public bool IsBypass()
		{
			return ((Source != Socket.XX) && (Dest != Socket.XX)) ? true : false;
		}

	}


    /// <summary> General-purpose random number generator </summary>
    public class Rnd
    {
        public static Random rnd = new Random();

        /// <summary>
        /// A random float from 0 to 1
        /// </summary>
        /// <returns></returns>
        public static float Float()
        {
            return (float)rnd.NextDouble();
        }

        /// <summary>
        /// a random float from 0 to max
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float Float(float max)
        {
            return (float)rnd.NextDouble() * max;
        }

        /// <summary>
        /// a random float from min to max
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float Float(float min, float max)
        {
            return (float)rnd.NextDouble() * (max - min) + min;
        }

        /// <summary>
        /// a random int from 0 to max
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int Int(int max)
        {
            return rnd.Next(max + 1);
        }

        /// <summary>
        /// a random float from min to max
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int Int(int min, int max)
        {
            return rnd.Next(min, max);
        }



    }



}
