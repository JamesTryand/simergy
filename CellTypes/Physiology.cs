using System;
using System.Diagnostics;
using System.Collections;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Simbiosis
{
	/// <summary>
	/// Base class for all cell types, whether located in this DLL or as plug-ins.
	/// Could also act as a default cell type for simple structural cells with trivial properties (e.g. neutral bouyancy)
	/// 
	/// The Cell class interrogates the appropriate Physiology subclass on initialisation to find out 
	/// what its physical properties are and how to attach sensors and moveable joints.
	/// 
	/// The Cell handles all the necessary animation, sensor updating etc. 
	/// On both fast and slow update ticks, it passes control to the Physiology object, 
	/// which reads and writes channels, chemistry and joint positions according to its function.
	///  
	/// </summary>
	/// <remarks>
	/// HOW TO IMPLEMENT NEW CELL TYPES
	/// 
	/// 1. Create a DLL to contain one or more types.
	/// 2. Reference *this* DLL from the new one.
	/// 3. Derive one or more classes from Physiology or one of the generic subclasses defined in BaseType.cs
	///		- The classes must have exactly the same names as their corresponding X file folder and gene entry
	/// 4. Override the default constructor and use it to:
	///		- Set appropriate cell properties (mass, etc)
    ///     - Initialise the array of ChannelData arrays, defining the channels (for each variant)
	/// 5. Override Init() and use it to set up any subclass members that need access to Cell members (not all of which are set up by the time the ctor is called)
    /// 6. Override FastUpdate()
	///		- Carry out any fast processing (usually things that create channel waveforms or alter joint positions
	///			- Fetch any input signals from channels using Input(n)
	///			- Write any output signals to channels using Output(n)
	///			- Write any animation signals to joints by filling in JointOutput[]
	///	7. Override SlowUpdate()
	///		- Use this to monitor sensors or respond to slow events such as chemical changes
    /// 8. Override IndexVariants() if the celltype has more than one variant, to return a list of their names
    /// 9. Override ReceiveStimulus() to handle any incoming stimuli.
    /// 10. Override AssignCamera() if you need to accept the role of camera mount (usually only camera ships, but possibly also cells that allow user to see what creature sees)
    /// 11. Override Steer() if you need to accept joystick/mouse commands (only applies to camera ships)
    /// 12. Override Command() to respond to control panel events (camera ships only)
    /// 13. Call Sound(), EmitStimulus(), Disturbance(), etc. to alert other creatures to changes, as appropriate.
    ///		  
	///
	/// 
	/// SENSORS AND EFFECTORS
	/// Sensors and effectors are generally based on HOTSPOTS in the cell (frames/meshes called "hot0" etc.). These define
	/// the location and orientation of sensitive areas. A cell type may have zero or more hotspots.
	/// 
	/// Two kinds of sensor can be implemented: active and passive.
	/// 
	///		TO IMPLEMENT AN ACTIVE SENSOR
	///		- In your SlowUpdate() method, call owner.GetObjectsInRange(hotspotnumber, range). This returns a list of selected 
	///		  IDetectable objects within the given range. They are supplied as an array of SensorItem structs, which contain
	///		  a reference to the object and the object's position relative to the position and orientation of the hotspot.
	///		- If the sensor is directional, call SensorItem methods to establish how far each object is from the line of sight
	///		  of the hotspot, and therefore whether it is actually visible.
	///		- The list of objects can be filtered as required, by interrogating them through their IDetectable interface.
	///		- The intensity of any output can be determined as appropriate, but possibly taking into account the relative
	///		  angle of the object and its distance downrange (both established by calling SensorItem methods).
	///		  
	///		TO IMPLEMENT A PASSIVE SENSOR
	///		Active sensors are necessary for proximity detection etc. but they can be expensive to run because a scan of the map
	///		has to be performed every slowupdate, whether there's anything to see or not. A more effient alternative is an
	///		active sensor, in which an EFFECTOR on one organism emits a stimulus to all objects within range, ONLY WHEN IT DOES SOMETHING.
	///		The effector has a range and possibly some directionality, and sends messages to all organisms within that range. 
    ///     The receiving cell can then either respond to the stimulus immediately, or use its own hotspot to do a visibility check before 
    ///     deciding whether to respond.
	///		NOTE: Only cell types that need directonality or control of their own range need have a hotspot. ANY cell is free to respond
	///		to incoming stimuli.
    ///		- Any received stimuli not handled by the Organism or Cell will be sent to us via the ReceiveStimulus() virtual function.
    ///     - The argument to this is a Stimulus object, which contains a stimulus type string, a set of simulus-dependent operands
    ///       and some information about who sent it, plus their location/distance.
	///		- Optionally, call owner.TestVisibility() method to determine whether the stimulus source is in view.
	///		  
	///		TO IMPLEMENT AN EFFECTOR
	///		Effectors emit stimuli to all ORGANISMS in a given range, optionally within a cone of influence. Stimuli can be emitted
	///		for many reasons, for instance:
	///		* a cell can choose to emit a "sound" when it moves, and other cells can "hear" this sound;
	///		* a cell can emit a "visual" signal, e.g. to denote a mating display or simply a movement, to all objects in visible range
	///		* one cell can eat another nearby cell, or inject poison into it
	///		* a cell can fire a gun or other "beam" at all objects in a given direction
	///		- In your SlowUpdate() or FastUpdate() method, decide if a stimulus should be emitted
	///		- If so, call owner.GetObjectsInRange() to fetch a list of all objects within range
	///		- filter out any objects that aren't organisms (unless you want the terrain to respond to the stimulus)
	///		- Optionally, call SensorItem.Angle() to filter out recipients that are in the wrong direction
	///		- for each object left in the list, send it a Stimulus
    ///     Effectors can be hotspots (for directional, neurally triggered or precise contact effectors) or they can be 
    ///     the Cell itself (when emitting simple sounds and movements).
    ///		The recipients process the stimuli as in the passive sensor description above.
	///
    ///     COMPULSORY STIMULI
    ///     Many cells ought to generate stimuli without necessarily being effectors - anything that would normally produce
    ///     a noise or sudden movement, for example. This prevents creatures from being stealthy by default.
    ///     There are helper functions in the Physiology class for emitting common stimuli such as sounds and movements.
    ///		  
	/// 
	/// </remarks>
	public class Physiology
	{

		// ---------- Properties (subclasses should set these in constr; defaults are suitable for simple non-functional cells) ---------
        // The value is the one to be used when the creature's scale is 1.0
		/// <summary> mass (used to calculate centre of gravity) </summary>
		public float Mass = 0.1f;
		/// <summary> Water resistance of cell (fraction of the reaction force that's applied to the root) </summary>
		public float Resistance = 0.1f;
		/// <summary> Current bouyancy (0=neutral in water. -ve sinks, +ve floats </summary>
		public float Buoyancy = 0.0f;

		// ------------ sensor and motor data ---------------

		/// <summary> Data to be written out to the cell's animatable joints, after each FastUpdate() 
		/// (Size of array is determined by the number of joint frames defined in the X file)</summary>
		public float[] JointOutput = null;


		// ------------- data about our Cell and its neighbours -----------

		/// <summary> 
		/// The Cell that owns me, represented as an IControllable object. This allows cell types to interrogate and control the Cell. 
		/// .owner can also be cast to an IDetectable, allowing me to extract information about my cell's properties.
		/// For example, a distance sensor might substract the ReqLocation() of the sensed object from owner.ReqLocation()
		/// to establish distance.
		/// </summary>
		protected IControllable owner = null;

		/// <summary> group.name of this cell type (including any variant#) </summary>
		protected string name = null;

 		/// <summary> Number of sockets on this cell (needed for e.g. routing nerves) </summary>
		protected int numSockets = 0;

        /// <summary> 
        /// The celltype's channel data. Each column represents one channel.
        /// If this cell has multiple variants, each row holds the channel data for one variant
        /// </summary>
        protected ChannelData[][] channelData = null;



		/// <summary>
		/// Create and return an object derived from Physiology, containing the functionality of the cell.
		/// The name of the appropriate DLL is supplied in the Genome: <type> DLLname.celltype </type>
		/// If no explicit DLL name is supplied, then the cell type is presumed to exist in this DLL
        /// NOTE: If the name ends in a number, this is removed before searching for a Physiology subclass.
        /// That way, different variants can share the same code. E.g. Muscle1 and Muscle2 both find their 
        /// functionality in the Muscle class.
		/// </summary>
		/// <param name="group">The name of the DLL containing the class (no extension)</param>
		/// <param name="type">The name of the Physiology-derived class</param>
        /// <param name="variantName">The name of the .X file</param>
		/// <param name="owner">The Cell that will own the resulting object</param>
		/// <param name="numJoints">The number of animatable joints in this cell</param>
		/// <param name="numSockets">The number of sockets in this cell</param>
		/// <returns></returns>
		public static Physiology LoadPhysiology(string group, string type, string variantName,
												IControllable owner,
												int numJoints, int numSockets)
		{
			Physiology phys = null;

			// Get full name so that we can store it in celltype and use when writing genes
			string name = group + ":" + type + "." + variantName;

			// Attempt to construct the specified object from its named DLL
			try
			{
				System.Runtime.Remoting.ObjectHandle obj = null;
				obj = Activator.CreateInstance(group, "Simbiosis." + type);
				phys = (Physiology)obj.Unwrap();							// convert the handle into an actual object
			}
			// If we fail, report the error and construct an instance of the base class instead. This will simply do nothing.
			// That way, trivial structural cells needn't have any associated code. Note that the default properties such as
			// mass, water resistance etc. may not be appropriate though.
			catch (Exception e)
			{
				Trace.WriteLine("WARNING: Physiology.LoadPhysiology() was unable to find/create a cell type of " + type + " in " + group + ".dll");
				Trace.WriteLine("         Using default Physiology class instead.");
				phys = new Physiology();
				// HACK: For now, throw an exception in case I meant to have such a cell type - remove this line later
				throw new SDKException("No Physiology class exists for cell type: " + group + "." + type,e);
			}
			finally
			{
				// Store the full name for ToString() to use.
				phys.name = name;
				// Initialise the new Physiology object's base and subclass members.
				// (can't do this in constr because I'm using the overload of CreateInstance that needs a parameterless constr)
				phys.Construct(owner, numJoints, numSockets);
			}
			return phys;
		}

		#region ----------------------- public methods --------------------------



		/// <summary>
		/// Return the DLL.class[variant] name of this cell type, as used in genes (e.g. "Default:Muscle.Variant3").
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return name;
		}

        /// <summary>
        /// Return an array of ChannelData objects describing this cell type's channels.
        /// A cell type that contains no channels must return null.
        /// Cell types that have upstream and downstream variants must return one of two arrays, depending on the value of .isUpstream.
        /// Each variant is identical except for its plug/skt assignments for input and output
        /// </summary>
        /// <returns>Null or an array of ChannelData objects</returns>
        public ChannelData[] GetChannelData()
        {
            try
            {
                return channelData[owner.Variant];                                      // return channel data for this specific variant
            }
            catch
            {
                try
                {
                    return channelData[0];                                              // if there isn't one, all variants are the same, so return row 0
                }
                catch
                {
                    return null;                                                        // if no channels are defined, return null
                }
            }
        }


		#endregion


		#region ----------------------- housekeeping -----------------------------


		/// <summary>
		/// Since the class is instantiated using reflection it can only have a default constructor (or at least, it's easier and more
		/// typesafe that way). So, immediately after creation, call this method to set up the base class members (owner, etc.).
		/// </summary>
		/// <param name="owner">An IControllable interface to our owning Cell</param>
		/// <param name="numJoints">Number of joint frames in the cell</param>
		/// <param name="numSockets">Number of sockets in the cell</param>
		private void Construct(IControllable owner, int numJoints, int numSockets)
		{
			this.owner = owner;										// reference to our cell, so that we can send it commands/requests
			JointOutput = new float[numJoints];						// size of our JointOutput array depends on # joint frames in cell

			this.numSockets = numSockets;							// store # sockets (needed for some operations on nerves etc.)

		}




		#endregion

		#region ------------------ utility functions callable from subclasses ------------------------


		/// <summary>
		/// Read an input channel from the nervous system
		/// This is how a cell type gets its behavioural parameters
		/// </summary>
		/// <param name="channel"></param>
		/// <returns></returns>
		protected float Input(int channel)
		{
			return owner.GetChannel(channel);
		}

 		/// <summary>
		/// Write an output channel to the nervous system
		/// This is how a cell type sends output to other cells
		/// </summary>
		/// <param name="chan">channel number</param>
		/// <param name="value">value to write</param>
		protected void Output(int chan, float value)
		{
			try
			{
                //Debug.WriteLine("Organism " + owner.OwnerOrganism() + " cell " + this.name + " output " + value + " to chan " + chan);
				owner.SetChannel(chan, value);
			}
			catch
			{
				throw new SDKException("Attempt to Output() to a channel that doesn't exist: " + chan + " in cell type " + this.name);
			}
		}

        /// <summary>
        /// Emit a specialised broadcast stimulus (other than sounds, disturbances, etc.)
        /// </summary>
        /// <param name="type">Stimulus type, e.g. "sound"</param>
        /// <param name="hotspot">hotspot emitting stimulus, or -1 to emit from the cell's axis</param>
        /// <param name="range">distance over which stimulus travels</param>
        /// <param name="angle">cone of transmission - half-angle from hotspot or cell's normal</param>
        /// <param name="param0">first stimulus param (type-dependent)</param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        protected void EmitStimulus(string type, int hotspot, float range, float angle, Object param0, Object param1, Object param2, Object param3)
        {
            // Create the stimulus
            Stimulus stimulus = new Stimulus((IDetectable)owner, ((IDetectable)owner).ReqLocation(), range, type, param0, param1, param2, param3);

            // Send it to each organism in range along the cone of transmission
            SensorItem[] recipients = owner.GetObjectsInRange(hotspot, range, true, false, false);
            foreach (SensorItem recipient in recipients)
            {
                if (recipient.Angle() <= angle)
                    recipient.Object.ReceiveStimulus(stimulus);
            }
        }


        /// <summary>
        /// Common stimulus: Emit a transient sound. (Call repeatedly for continuous noise)
        /// Any cell can call this - we don't need an effector hotspot.
        /// Recipients ("ear" celltypes) should combine .TransmissionRange with distance from source to compute perceived loudness
        /// First parameter of Stimulus is the pitch
        /// NOTE: CameraShips' root cell types will receive the stimuli and can use the parameters to emit a real (DirectSound) sound to the user
        /// </summary>
        /// <param name="range">Distance sound travels</param>
        /// <param name="pitch">Frequency of sound, for pitch-sensitive or pitch-selective sensors: 0 = low, 1.0 = high</param>
        /// <param name="timbre">Optional string for the DirectSound file that should be played if this sound is picked up by the current CameraShip</param>
        protected void Sound(float range, float pitch, string timbre)
        {
            // Create the stimulus
            Stimulus stimulus = new Stimulus((IDetectable)owner, ((IDetectable)owner).ReqLocation(), range, "sound", pitch, timbre, null, null);

            // Send it to each organism in range
            SensorItem[] recipients = owner.GetObjectsInRange(-1, range, true, false, false);
            foreach (SensorItem recipient in recipients)
            {
                recipient.Object.ReceiveStimulus(stimulus);
            }
        }

        /// <summary>
        /// Common stimulus: Emit an omnidirectional ripple of water disturbance, to show that a cell has radically changed shape.
        /// Use when a muscle twitches rapidly, jaws clench, etc. Whole-body movements should be detected passively; this is just a shockwave in the water
        /// that can be picked up by a hairy cell, like the lateral line in fish.
        /// Usually best called from a SlowUpdate() whenever the cell (e.g. its JointOutput) has changed more than a certain amount since the last update.
        /// Note: the sub engines could emit disturbances, which could scare away creatures
        /// </summary>
        /// <param name="range">The intensity of the stimulus and hence its range</param>
        protected void Disturbance(float range)
        {
            Stimulus stimulus = new Stimulus((IDetectable)owner, ((IDetectable)owner).ReqLocation(), range, "disturbance", null, null, null, null);

            SensorItem[] recipients = owner.GetObjectsInRange(-1, range, true, false, false);
            foreach (SensorItem recipient in recipients)
            {
                recipient.Object.ReceiveStimulus(stimulus);
            }
        }



		#endregion

		#region ------------------- overridable members ----------------------------

		/// <summary>
		/// Base constr. ONLY set the physical properties & channelData array(s) in the constructor. Use Init() for initialisation.
		/// </summary>
		public Physiology()
		{
		}

		/// <summary>
		/// Called once, after the cell and its joints, channels, etc. have been set up. Do your main initialisation here.
        /// E.g. you may need to read Input() channels to fetch initial values, etc.
		/// 
		/// </summary>
		public virtual void Init()
		{
		}


		/// <summary>
		/// Called every frame
		/// Functional block and JointOutputs should normally be updated here.
		/// </summary>
		public virtual void FastUpdate(float elapsedTime)
		{
		}



		/// <summary>
		/// Called on a SlowUpdate tick (about 4 times a second).
		/// Read/write/modify your sensory/motor nerves and/or chemicals, to implement your behaviour.
        /// Also a good place to emit a Disturbance() ripple if you've moved more than a certain amount
		/// </summary>
		public virtual void SlowUpdate()
		{
		}

		/// <summary> We've been sent a Stimulus that our basic Cell object doesn't understand.
		/// Override this method to handle any stimuli that our particular cell type understands. 
		/// This mechanism allows new cell types to declare new stimulus types. Note however that 
		/// existing cell types won't have any handlers for these new stimuli. (On the other hand
		/// it's possible to define handlers in advance of any cell types that know how to emit a 
		/// particular stimulus, making them "stimulus-ready"!)
		/// <param name="stimulus">The stimulus information</param>
		/// <returns>Return true if the stimulus was handled</returns>
		public virtual bool ReceiveStimulus(Stimulus stim)
		{
			return false;
		}

 
		/// <summary>
		/// We've been asked if we will accept the role of camera mount.
		/// Most cell types won't need to override this, but any cells that can play the part
		/// of a creatures-eye view, plus of course the main submarine or other camera objects,
		/// should override this and return a valid hotspot number that will be used to carry the camera.
		/// The parameters to this method show which cell and hotspot is currently carrying the camera. 
		/// When a command to cycle through the views is issued, the current camera ship will be asked AGAIN
		/// whether it will accept the camera. This gives it the opportunity to offer a second or subsequent
		/// hotspots (e.g. side views, or an outside view). If this cell type has only the one hotspot it
		/// should return -1 if this is the hotspot already in use, thus passing the opportunity along to
		/// other cells in this organism or other organisms entirely.
		/// </summary>
		/// <param name="currentOwner">The cell that currently has the camera</param>
		/// <param name="currentHotspot">The hotspot that currently has the camera</param>
		/// <returns>The index of the hotspot to use, or -1 if we aren't a valid camera mount</returns>
		public virtual int AssignCamera(IControllable currentOwner, int currentHotspot)
		{
			// Sample code for a valid camera mount with only one hotspot...
			//	if (owner==currentOwner)											// if I'm being asked a second time
			//		return -1;														// say no
			//	return 0;															// otherwise, accept the camera

			// Sample code for a cell type with two camera hotspots...
			//	if ((owner==currentOwner)&&(currentHotspot==0))						// if my hotspot 0 is already in use
			//		return 1;														// transfer camera to hotspot 1
			//	if ((owner==currentOwner)&&(currentHotspot==1))						// if my hotspot 1 is already in use
			//		return -1;														// let someone else have a go
			//	return 0;															// else offer hotspot 0

			// the default is never to accept a camera
			return -1;
		}

		/// <summary>
		/// We've been sent some steering control data because we are the ROOT cell of
		/// an organism that is currently the camera ship. 
		/// Overload this method if you are a camera ship and move/steer accordingly.
		/// The default is to ignore this data - we may have been sent it because we are
		/// acting as a creatures-eye-view, and proper creatures can't be steered by the user.
		/// </summary> 
		/// <param name="tiller">the joystick/keyboard data relavent to steering camera ships</param>
		/// <param name="elapsedTime">elapsed time this frame, in case motion/animation needs to be proportional</param>
		public virtual void Steer(TillerData tiller, float elapsedTime)
		{
		}

        /// <summary>
        /// We've been sent a button command from a control panel because we are the ROOT cell of
        /// an organism that is currently the camera ship.
        /// Overload this method if you need to respond to the button(s), e.g. to steer the view camera or a spotlight from the cockpit
        /// by changing one or more anim# jointframes
        /// </summary>
        /// <param name="c">The name of the button or other widget that has been pressed/released/changed</param>
        /// <param name="state">Widget state (type depends on the button, e.g. bool for pushbuttons, float for knobs)</param>
        /// <param name="elapsedTime">frame time</param>
        public virtual void Command(string c, object state, float elapsedTime)
        {
        }

        /// <summary>
        /// If the celltype has more than one variant, override this method to return a list of the variant names.
        /// The cell uses this to convert variant names (.X files) into indices for fast access to the channel data, etc.
        /// </summary>
        /// <param name="variantName"></param>
        public virtual string[] IndexVariants(string variantName)
        {
            if ((channelData!=null)&&(channelData.GetLength(0) > 1))
                throw new SDKException("Celltypes with multiple variants must override Physiology.IndexVariants()!");
            return new string[] { "default" };
        }


		#endregion



	}









}
