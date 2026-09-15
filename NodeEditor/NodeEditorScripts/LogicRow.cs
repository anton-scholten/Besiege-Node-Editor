namespace NodeEditorMod
{
    /// <summary>
    /// One Computer row: a logic gate or a timer, and its run state. The
    /// settings are Besiege control objects, made rather than registered: the rows
    /// are the block's own data, saved as text (<see cref="LogicTable.Save"/>),
    /// and a run files the inputs by hand. The state is <see cref="Gates"/>' copy
    /// of the game's.
    /// </summary>
    public class LogicRow
    {
        // ---- what it is set to -----------------------------------------------

        /// <summary>The row's two inputs. B is not read by every gate -- see
        /// <see cref="Gates.UsesB"/>.</summary>
        public MKey InputA;
        public MKey InputB;

        /// <summary>Which gate, as <see cref="Gates"/>' constants; a menu, as the
        /// game's own block saves it.</summary>
        public MMenu Kind;

        /// <summary>The one switch: "inverted" for the edge detector, "toggle mode"
        /// for the rest. Besiege has both and shows one.</summary>
        public MToggle Mode;

        /// <summary>What the row presses while its gate says yes.</summary>
        public MKey Emulate;

        /// <summary>Timer settings (<see cref="Gates.Timer"/>). Every row has them,
        /// and a row switched back to a gate keeps them.</summary>
        public MSlider Wait;
        public MSlider Duration;
        public MToggle Hold;
        public MToggle Stop;
        public MToggle Loop;

        /// <summary>The same controls as a timer row, and the phase it is in:
        /// what <see cref="Clock"/> runs, the same as a Timer Plus row.</summary>
        public Row Timing;

        // ---- what it is doing ------------------------------------------------

        /// <summary>The two inputs as the gate last saw them.</summary>
        public bool A;
        public bool B;

        /// <summary>The latch the memory gates answer with, and the second one
        /// toggle mode keeps for input B.</summary>
        public bool AToggled;
        public bool BToggled;

        /// <summary>The counter gate's two numbers.</summary>
        public int Count;
        public int LastCount;

        /// <summary>Whether the row holds its emulated key, so it is let go exactly
        /// once: emulated keys are reference counted.</summary>
        public bool Held;

        /// <summary>The inputs, read through <see cref="KeyReader"/> so an emulated
        /// press is neither missed nor counted twice.</summary>
        public KeyReader ReadA;
        public KeyReader ReadB;

        // ---- reading the settings --------------------------------------------

        /// <summary>Whether every control exists; a client without physics builds
        /// none.
        /// </summary>
        public bool Ready
        {
            get
            {
                return InputA != null && InputB != null && Kind != null
                    && Mode != null && Emulate != null && Wait != null
                    && Duration != null && Hold != null && Stop != null
                    && Loop != null && Timing != null;
            }
        }

        /// <summary>The gate, clamped to one that exists, so a save from a later
        /// game does not throw.</summary>
        public int Gate
        {
            get
            {
                if (Kind == null)
                {
                    return Gates.Not;
                }
                int gate = Kind.Value;
                return gate < 0 || gate >= Gates.Kinds ? Gates.Not : gate;
            }
        }

        /// <summary>Whether this row is a timer rather than a gate.</summary>
        public bool IsTimer { get { return Kind != null && Kind.Value == Gates.Timer; } }

        /// <summary>Whether this is a timer with nothing on its input, which starts
        /// with the simulation: Besiege's timer with Automatic on, read and written
        /// as one.
        /// </summary>
        public bool Automatic
        {
            get { return IsTimer && Bindings.Count(InputA) == 0; }
        }

        /// <summary>The row's one switch, whatever it means for this gate.</summary>
        public bool Switch { get { return Mode != null && Mode.IsActive; } }
    }
}
