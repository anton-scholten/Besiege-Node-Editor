namespace TimerPlusMod
{
    /// <summary>
    /// One row of the Logic Gate Plus table: a whole logic gate, and the state it
    /// is in during a run.
    ///
    /// The settings are Besiege's own mapper controls rather than fields, which is
    /// what makes them saved with the machine, undoable, and sent over
    /// multiplayer -- and, in the case of the three keys, automatable at all. See
    /// <see cref="Row"/> for why that caps the number of rows.
    ///
    /// The state is <see cref="Gates"/>', which is Besiege's own: A and B as the
    /// gate last saw them, the latched flag the memory gates answer with, and the
    /// counter's two numbers.
    /// </summary>
    public class LogicRow
    {
        // ---- what it is set to -----------------------------------------------

        /// <summary>The row's two inputs. B is not read by every gate -- see
        /// <see cref="Gates.UsesB"/>.</summary>
        public MKey InputA;
        public MKey InputB;

        /// <summary>Which gate, as one of <see cref="Gates"/>' constants. A menu
        /// rather than a number, so a save carries the choice the same way the
        /// game's own block does.</summary>
        public MMenu Kind;

        /// <summary>The one switch: "inverted" for the edge detector, "toggle
        /// mode" for everything else. Besiege has two controls and shows one at a
        /// time; a table with a column each would be a column of blanks.</summary>
        public MToggle Mode;

        /// <summary>What the row presses while its gate says yes.</summary>
        public MKey Emulate;

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

        /// <summary>Whether the row is holding its emulated key. Kept so it can be
        /// let go exactly once -- an emulated key is reference counted, so one
        /// never let go never comes up for anything else either.</summary>
        public bool Held;

        /// <summary>The two inputs, read through <see cref="KeyReader"/> so an
        /// emulated press is caught on the emulation tick rather than missed or
        /// counted twice by a frame update.</summary>
        public KeyReader ReadA;
        public KeyReader ReadB;

        // ---- reading the settings --------------------------------------------

        /// <summary>True while every control this row needs is there. A row whose
        /// controls could not be found is skipped rather than dereferenced: the
        /// mapper is built on a simulating client without physics with every field
        /// still null.</summary>
        public bool Ready
        {
            get
            {
                return InputA != null && InputB != null && Kind != null
                    && Mode != null && Emulate != null;
            }
        }

        /// <summary>Which gate this row is, clamped to one that exists: a save
        /// from a later game with more gates in it should not throw.</summary>
        public int Gate
        {
            get
            {
                if (Kind == null)
                {
                    return Gates.Not;
                }
                int gate = Kind.Value;
                return gate < 0 || gate >= Gates.Count ? Gates.Not : gate;
            }
        }

        /// <summary>The row's one switch, whatever it means for this gate.</summary>
        public bool Switch { get { return Mode != null && Mode.IsActive; } }
    }
}
