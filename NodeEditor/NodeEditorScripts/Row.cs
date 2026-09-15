using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// A timer's run state, which <see cref="Clock"/> moves: phases and tick counts
    /// are <c>TimerBlock</c>'s. A Computer timer row fills the settings with its
    /// mapper controls (<see cref="LogicRow.Timing"/>); a Timer Plus run uses the
    /// state alone and hands the clock its settings from <see cref="RowData"/>.
    /// </summary>
    public class Row
    {
        // ---- what it is set to -----------------------------------------------

        /// <summary>What the row presses while it is running.</summary>
        public MKey Emulate;

        /// <summary>Seconds from the start to the press.</summary>
        public MSlider Wait;

        /// <summary>Seconds the press is held for.</summary>
        public MSlider Duration;

        /// <summary>Run only while the activation is held, rather than until it is
        /// done.</summary>
        public MToggle Hold;

        /// <summary>A second press stops a run in progress.</summary>
        public MToggle Stop;

        /// <summary>Start again as soon as a cycle ends.</summary>
        public MToggle Loop;

        // ---- what it is doing ------------------------------------------------

        public const int Idle = 0;
        public const int Waiting = 1;
        public const int Pressing = 2;

        /// <summary>The phase. Nothing resets it: the simulation's copy is a fresh
        /// object.
        /// </summary>
        public int Phase;

        /// <summary>The phase the frame counters were set up for, so entering a
        /// phase is told apart from continuing in it.</summary>
        public int Entered;

        /// <summary>Ticks spent in the phase and its length, counted in ticks as
        /// Besiege's timer counts them.</summary>
        public int Ticks;
        public int Length;

        /// <summary>Whether the clock wants the key held; <see cref="Held"/> is
        /// what is.
        /// </summary>
        public bool Wants;

        /// <summary>Whether the key is held, so it is let go exactly once: emulated
        /// keys are reference counted.</summary>
        public bool Held;

        // ---- reading the settings --------------------------------------------

        /// <summary>Whether every control exists; a client without physics builds
        /// none.
        /// </summary>
        public bool Ready
        {
            get
            {
                return Emulate != null && Wait != null && Duration != null
                    && Hold != null && Stop != null && Loop != null;
            }
        }

        public float WaitSeconds { get { return Wait == null ? 0f : Wait.Value; } }
        public float PressSeconds { get { return Duration == null ? 0f : Duration.Value; } }
        public bool HoldToRun { get { return Hold != null && Hold.IsActive; } }
        public bool CanStop { get { return Stop != null && Stop.IsActive; } }
        public bool Loops { get { return Loop != null && Loop.IsActive; } }

    }
}
