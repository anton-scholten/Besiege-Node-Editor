using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// One row of the table: a whole timer, and the state it is in during a run.
    ///
    /// The settings are Besiege's own mapper controls rather than fields, which is
    /// what makes them saved with the machine, undoable, and sent over
    /// multiplayer -- and, in the case of the two keys, automatable at all.
    /// <see cref="MKey"/> is the only mapper type that carries a variable: the
    /// slider, the toggle and the menu have nothing variable-related on them, so a
    /// row's activation and the key it presses have to be keys and cannot be
    /// anything else. That is also why the number of rows is capped: every row's
    /// keys are registered in <c>SafeAwake</c> and Besiege has no way to add one
    /// later.
    ///
    /// The phases and the frame counting are Besiege's own
    /// <c>TimerBlock</c>, read out of the game so that a row behaves exactly as
    /// the block it stands in for -- see <see cref="TimerPlusBehaviour"/> for the
    /// one place the two deliberately differ.
    /// </summary>
    public class Row
    {
        // ---- what it is set to -----------------------------------------------

        /// <summary>What starts this row. A key with no keycode and no variable --
        /// which is how one arrives -- means "whatever starts the block", so the
        /// key at the top of the panel drives every row that has not been given
        /// one of its own.</summary>
        public MKey Activate;

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

        /// <summary>The row's own activation, read through
        /// <see cref="KeyReader"/> so an emulated press is caught on the emulation
        /// tick rather than missed or counted twice by a frame update.</summary>
        public KeyReader Key;

        // ---- reading the settings --------------------------------------------

        /// <summary>True while every control this row needs is there. A row whose
        /// controls could not be found is skipped rather than dereferenced: the
        /// mapper is built on a simulating client without physics with every field
        /// still null, and the same guard covers both.</summary>
        public bool Ready
        {
            get
            {
                return Activate != null && Emulate != null && Wait != null
                    && Duration != null && Hold != null && Stop != null && Loop != null;
            }
        }

        /// <summary>
        /// Whether this row has an activation of its own, rather than following the
        /// block's.
        ///
        /// A variable counts, and so does any keycode but None -- Besiege writes
        /// <c>KeyCode.None</c> for a key nobody has bound, and
        /// <c>KeyInputController</c> never registers or answers one, so it is the
        /// natural spelling of "not set".
        /// </summary>
        public bool Owns
        {
            get
            {
                if (Activate == null)
                {
                    return false;
                }
                if (Activate.useMessage)
                {
                    return true;
                }
                for (int i = 0; i < Activate.KeysCount; i++)
                {
                    if (Activate.GetKey(i) != KeyCode.None)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public float WaitSeconds { get { return Wait == null ? 0f : Wait.Value; } }
        public float PressSeconds { get { return Duration == null ? 0f : Duration.Value; } }
        public bool HoldToRun { get { return Hold != null && Hold.IsActive; } }
        public bool CanStop { get { return Stop != null && Stop.IsActive; } }
        public bool Loops { get { return Loop != null && Loop.IsActive; } }

    }
}
