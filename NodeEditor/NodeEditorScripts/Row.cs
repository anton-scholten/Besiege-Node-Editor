using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// One row of the table: a whole timer, and the state it is in during a run.
    ///
    /// The settings are Besiege's own mapper controls rather than fields, which is
    /// what makes them saved with the machine, undoable, and sent over
    /// multiplayer -- and, in the case of the two keys, automatable at all.
    /// <see cref="MKey"/> is the only mapper type that carries a variable: the
    /// slider, the toggle and the menu have nothing variable-related on them, so
    /// the key a row presses has to be a key and cannot be anything else. That is
    /// also why the number of rows is capped: every row's keys are registered in
    /// <c>SafeAwake</c> and Besiege has no way to add one later.
    ///
    /// A row has no activation of its own. Every row in the table is started by
    /// the block's own key, which is the one question the block mapper above the
    /// panel answers.
    ///
    /// The phases and the frame counting are Besiege's own
    /// <c>TimerBlock</c>, read out of the game so that a row behaves exactly as
    /// the block it stands in for -- see <see cref="TimerPlusBehaviour"/> for the
    /// one place the two deliberately differ.
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

        /// <summary>Which of the three above. Reset to Idle by nothing: a sim
        /// behaviour is a fresh object every run, so its fields are already at
        /// their defaults and an OnSimulateStart that cleared them would be dead
        /// code.</summary>
        public int Phase;

        /// <summary>The phase the frame counters were set up for, so entering a
        /// phase is told apart from continuing in it.</summary>
        public int Entered;

        /// <summary>Emulation ticks spent in the current phase, and how many the
        /// phase lasts. Both counted in ticks rather than seconds because that is
        /// what Besiege's own timer counts, and a rounding that agrees with it is
        /// worth more than one that is nearer the truth.</summary>
        public int Ticks;
        public int Length;

        /// <summary>Whether the row wants its emulated key held right now, as
        /// <see cref="Clock"/> last left it. What is actually held is
        /// <see cref="Held"/>; the behaviour reconciles the two.</summary>
        public bool Wants;

        /// <summary>Whether the row is actually holding its emulated key. Kept so
        /// it can be let go exactly once, including when the machine is taken apart
        /// mid-press -- an emulated key is reference counted, so one never let go
        /// never comes up for anything else either.</summary>
        public bool Held;

        // ---- reading the settings --------------------------------------------

        /// <summary>True while every control this row needs is there. A row whose
        /// controls could not be found is skipped rather than dereferenced: the
        /// mapper is built on a simulating client without physics with every field
        /// still null, and the same guard covers both.</summary>
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
