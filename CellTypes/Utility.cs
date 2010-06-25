using System;

namespace Simbiosis
{
	// Utility classes for use by cell types

	
	/// <summary>
	/// A generalised attack/sustain/decay/repeat pattern generator.
	/// Can be used to create various kinds of nerve impulse, including servoing, sinusoidal oscillation and ramps.
	/// </summary>
	public class PatternGenerator
	{
		/// <summary>
		/// Phases of the movement
		/// </summary>
		public enum Phase
		{
			Transition,										// moving to the start point before beginning a new motion profile
			Attack,											// changing from .begin to .end via a sigmoid
			Sustain,										// waiting at .end
			Decay,											// changing from .end to .begin via a sigmoid
			Refractory,										// waiting at .begin before possible repeat
			Finished										// one-shot pattern is completed
		};

		public float state = 0;								// current state of the joint (0-1)
		public Phase phase = Phase.Finished;				// current movement phase


		private float begin = 0;							// state before attack and after decay (set .begin and .end to same value to stop joint)
		private float end = 0;								// state after attack and before decay
		private float attackPeriod = 1;						// how long it takes to attack from .begin to .end
		private float sustainPeriod = 0;					// how long to stay at .end before starting decay
		private float decayPeriod = 1;						// how long it takes to decay from .end to .begin
		private float refractoryPeriod = 0;					// how long to wait after decay before starting a new cycle (float.MaxValue = don't repeat)

		private float clock = 0;							// used for timing the various phases

		public PatternGenerator()
		{
		}

		/// <summary>
		/// Called by owner every frame.
		/// </summary>
		/// <param name="elapsedTime">time elapsed since last call</param>
		/// <returns>The new state of the joint</returns>
		public float Update(float elapsedTime)
		{
			float frac;

			switch (phase)
			{
					// Transit smoothly from where you are to the .begin position, ready to start a newly defined movement profile
				case Phase.Transition:
					if (state>begin)
					{
						state -= elapsedTime;							// reach .begin within one second
						if (state<=begin)
						{
							state = begin;
							phase = Phase.Attack;
						}
					}
					else
						state += elapsedTime;
					if (state>=begin)
					{
						state = begin;
						phase = Phase.Attack;
					}
					break;

					// Attack
				case Phase.Attack:
					clock+=elapsedTime;									// total time elapsed this phase
					frac = clock / attackPeriod;						// total elapsed time as a fraction of the period
					frac = (float)Math.Sin(frac * (float)Math.PI/2);	// convert this to a sigmoidal curve
					state = (end-begin)*frac + begin;					// move joint to the correct position
					if (frac>0.99)										// if we've reached end of phase, move on
					{
						clock = 0;
						phase = Phase.Sustain;
					}
					break;

					// Sustain
				case Phase.Sustain:
					clock += elapsedTime;
					if (clock>sustainPeriod)
					{
						clock = 0;
						phase = Phase.Decay;
					}
					break;

					// Decay
				case Phase.Decay:
					clock+=elapsedTime;									// total time elapsed this phase
					frac = clock / decayPeriod;							// total elapsed time as a fraction of the period
					frac = (float)Math.Sin(frac * (float)Math.PI/2);	// convert this to a sigmoidal curve
					state = (begin-end)*frac + end;						// move joint to the correct position
					if (frac>0.99)										// if we've reached end of phase, move on
					{
						clock = 0;
						phase = Phase.Refractory;
					}
					break;

					// Refractory period after decay (will last forever if cycle is not to be repeated)
				case Phase.Refractory:
					clock += elapsedTime;
					if (clock>refractoryPeriod)
					{
						clock = 0;
						phase = Phase.Attack;
					}
					break;

			}

			return state;
		}


		/// <summary>
		/// General-purpose joint motion definition. 
		/// More specific types are available (e.g. ServoWithinPeriod).
		/// </summary>
		/// <param name="begin">state before attack and after decay</param>
		/// <param name="attackPeriod">how long it takes to attack from .begin to .end</param>
		/// <param name="end">state after attack and before decay</param>
		/// <param name="sustainPeriod">how long to stay at .end before starting decay (float.MaxValue = stop here; no decay)</param>
		/// <param name="decayPeriod">how long it takes to decay from .end to .begin</param>
		/// <param name="refractoryPeriod">how long to wait after decay, before starting a new cycle (float.MaxValue = do not repeat cycle)</param>
		public void Set(float begin, float attackPeriod, 
			float end, float sustainPeriod, 
			float decayPeriod, 
			float refractoryPeriod)
		{
			// Attack and decay must take a finite time
			if (attackPeriod<=0)
				attackPeriod = 0.1f;
			if (decayPeriod<=0)
				decayPeriod = 0.1f;

			this.begin = begin;
			this.attackPeriod = attackPeriod;
			this.end = end;
			this.sustainPeriod = sustainPeriod;
			this.decayPeriod = decayPeriod;
			this.refractoryPeriod = refractoryPeriod;
			phase = Phase.Transition;						// don't start this new movement until you've moved smoothly to .begin
			Random rnd = new Random();						// randomise the state so that instances aren't synchronised
			state = (float)rnd.NextDouble();
		}

		/// <summary>
		/// Stop the joint immediately
		/// </summary>
		public void Stop()
		{
			begin = end = state;
			phase = Phase.Finished;
		}

		/// <summary>
		/// Servo smoothly from the joint's present position to a new target position and stay there.
		/// Do this in a constant period (i.e. large movements happen more quickly).
		/// This is a good way to move a head towards a target of interest.
		/// </summary>
		/// <param name="target">Target position of joint</param>
		/// <param name="period">Time over which the movement should take place</param>
		public void ServoWithinPeriod(float target, float period)
		{
			begin = state;						// start from where you are now
			end = target;						// end at target
			attackPeriod = period;				// in this many seconds' time
			sustainPeriod = float.MaxValue;		// stay there indefinitely
			phase = Phase.Transition;
		}

		/// <summary>
		/// Servo smoothly from the joint's present position to a new target position and stay there.
		/// Do this at a roughly constant rate (i.e. large movements take longer than small ones).
		/// This is good for heavy, ponderous heads.
		/// </summary>
		/// <param name="target">Target position of joint</param>
		/// <param name="rate">Speed (e.g. 0.5 will move through the whole range of the joint in 0.5 seconds
		/// or half the range in 0.25 sec) </param>
		public void ServoAtRate(float target, float rate)
		{
			begin = state;						// start from where you are now
			end = target;						// end at target
			sustainPeriod = float.MaxValue;		// stay there indefinitely
			attackPeriod = (end-begin) * rate;	// move in appropriate fraction of the total period
			phase = Phase.Transition;
		}

		/// <summary>
		/// Make a swimming movement like a pair of flippers - move briskly backwards, pause, then slowly forwards.
		/// </summary>
		/// <param name="strokePeriod"></param>
		/// <param name="waitPeriod"></param>
		/// <param name="returnPeriod"></param>
		/// <param name="repeat">true to repeat</param>
		public void Swim(float strokePeriod, float waitPeriod, float returnPeriod, bool repeat)
		{
			begin = 0;
			end = 1;
			attackPeriod = strokePeriod;
			sustainPeriod = waitPeriod;
			decayPeriod = returnPeriod;
			this.refractoryPeriod = 0;
			phase = Phase.Transition;
		}

		/// <summary>
		/// Oscillate sinusoidally
		/// </summary>
		/// <param name="period"></param>
		public void Sinusoid(float period)
		{
			begin = 0;
			end = 1;
			attackPeriod = period/2.0f;
			decayPeriod = period/2.0f;
			sustainPeriod = 0;
			this.refractoryPeriod = 0;
			phase = Phase.Transition;
		}

		/// TODO: Other useful profiles here
		/// (don't forget to set phase = Phase.Transition;)

	}


}
