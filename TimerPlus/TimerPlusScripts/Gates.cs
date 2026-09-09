using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// Besiege's own logic gate, as a state machine with no Unity object in it.
    ///
    /// Read out of `LogicGate` with `peek.sh dump`, the way <see cref="Clock"/>
    /// was read out of `TimerBlock`, and for the same reason: the convert button
    /// only means anything if a row behaves exactly like the block it turns into.
    ///
    /// The game calls its `UpdateState` twice a frame -- once with the keyboard's
    /// edges from `UpdateBlock`, once with the emulated ones from
    /// `EmulationUpdateBlock` -- and then asks `EvaluateEmulation` on the
    /// emulation tick for the one answer that goes out of the block.
    /// <see cref="Advance"/> and <see cref="Answer"/> are those two.
    /// </summary>
    public static class Gates
    {
        // The gates, in the order Besiege's own `GateType` declares them, which is
        // also the order of its menu: the block casts the menu's value straight to
        // the enum. Constants rather than an enum because declaring one segfaults
        // Besiege's C# compiler.
        public const int Not = 0;
        public const int And = 1;
        public const int Or = 2;
        public const int Nor = 3;
        public const int Nand = 4;
        public const int Xor = 5;
        public const int Xnor = 6;
        public const int Random = 7;
        public const int SRLatch = 8;
        public const int DLatch = 9;
        public const int Counter = 10;
        public const int EdgeDetect = 11;

        public const int Count = 12;

        /// <summary>
        /// What Besiege calls each gate, in the order its own menu lists them --
        /// which is the order of `GateType`, because the block casts the menu's
        /// value straight to the enum.
        ///
        /// The words are the game's, looked up by the same translation ids
        /// `LogicGate.Awake` builds its menu from, so a table and the block it
        /// stands in for say the same thing in the same language. The list below is
        /// the fallback for when that lookup is not available.
        /// </summary>
        private static readonly int[] Ids =
        {
            3811, 3810, 3812, 3813, 3816, 3814, 3815, 4253, 4246, 4247, 4248, 4595
        };

        private static readonly string[] Spelled =
        {
            "NOT", "AND", "OR", "NOR", "NAND", "XOR", "XNOR",
            "RANDOM", "SR LATCH", "D LATCH", "COUNTER", "EDGE"
        };

        private static string[] names;

        public static string[] Names
        {
            get
            {
                if (names != null)
                {
                    return names;
                }
                names = new string[Ids.Length];
                for (int i = 0; i < Ids.Length; i++)
                {
                    names[i] = Spelled[i];
                    try
                    {
                        string said = Localisation.LocalisationManager
                            .GetTranslation(Ids[i]);
                        if (!string.IsNullOrEmpty(said))
                        {
                            names[i] = said.ToUpperInvariant();
                        }
                    }
                    catch (System.Exception)
                    {
                        // The game's own words are a nicety; the list above is
                        // what the gates are called either way.
                    }
                }
                return names;
            }
        }

        /// <summary>
        /// Whether a gate reads its second input at all.
        ///
        /// Besiege's own `UpdateHidden` takes the B key out of the mapper for
        /// exactly these three, which is where this list comes from rather than
        /// from reasoning about what a gate ought to need.
        /// </summary>
        public static bool UsesB(int gate)
        {
            return gate != Not && gate != Random && gate != EdgeDetect;
        }

        /// <summary>Whether the row's one switch means "inverted" -- the edge
        /// detector's -- rather than "toggle mode".</summary>
        public static bool Inverts(int gate)
        {
            return gate == EdgeDetect;
        }

        /// <summary>
        /// Whether the switch does anything at all for this gate.
        ///
        /// Toggle mode is read only by the combinational gates and inverted only
        /// by the edge detector, so the memory gates -- random, the two latches
        /// and the counter -- have neither, and Besiege shows neither control for
        /// them.
        /// </summary>
        public static bool UsesMode(int gate)
        {
            return gate <= Xnor || gate == EdgeDetect;
        }

        /// <summary>The letter the switch wears: what it would be called if there
        /// were room for a word.</summary>
        public static string ModeLetter(int gate)
        {
            if (!UsesMode(gate))
            {
                return "";
            }
            return Inverts(gate) ? "I" : "T";
        }

        /// <summary>
        /// One pass of the game's `UpdateState`, which is where a gate's memory
        /// lives: the latches, the counter and the toggled inputs.
        ///
        /// The arguments are the game's own, in its order: the two press edges,
        /// the two held states, and A's release -- which only the edge detector
        /// looks at, and only when it is inverted.
        /// </summary>
        /// <param name="gate">Which gate, and <paramref name="mode"/> its one
        /// switch. Passed rather than read off the row's controls, so the state
        /// machine holds no mapper control and the build can check it without a
        /// live block -- the same reason <see cref="Clock"/> takes its wait and
        /// its duration as numbers.</param>
        public static void Advance(LogicRow row, int gate, bool mode,
                                   bool pressedA, bool pressedB,
                                   bool heldA, bool heldB, bool releasedA)
        {
            row.A = heldA;
            row.B = heldB;

            if (gate == Counter)
            {
                if (pressedB)
                {
                    // B is the reset, and it wins over a count arriving with it.
                    row.Count = 0;
                    row.LastCount = 0;
                }
                else if (pressedA)
                {
                    row.LastCount = row.Count;
                    row.Count++;
                }
                row.Count %= 4;
                // Not the output -- these two are what the block's own display
                // shows, and they are kept because the block keeps them.
                row.A = row.Count % 2 == 1;
                row.B = row.Count > 1;
                return;
            }

            if (gate == Random)
            {
                if (pressedA)
                {
                    row.AToggled = !(UnityEngine.Random.Range(0f, 1f) < 0.5f);
                }
                row.A = row.B = heldA && !pressedA;
                return;
            }

            if (gate == SRLatch)
            {
                bool set = row.AToggled;
                if (!set && (pressedA || heldA))
                {
                    set = true;
                }
                if (set && (pressedB || heldB))
                {
                    set = false;
                }
                row.AToggled = set;
                return;
            }

            if (gate == DLatch)
            {
                if (pressedB || heldB)
                {
                    row.AToggled = pressedA || heldA;
                }
                return;
            }

            if (gate == EdgeDetect)
            {
                row.B = row.A;
                if (mode)
                {
                    // Inverted: the edge it answers to is the letting go.
                    if (releasedA)
                    {
                        row.AToggled = true;
                    }
                }
                else if (pressedA)
                {
                    row.AToggled = true;
                }
                return;
            }

            // Everything else is combinational, and the switch means "toggle
            // mode": each press flips its input rather than holding it.
            if (mode)
            {
                if (pressedA)
                {
                    row.AToggled = !row.AToggled;
                }
                if (pressedB)
                {
                    row.BToggled = !row.BToggled;
                }
                row.A = row.AToggled;
                row.B = row.BToggled;
            }
            if (gate == Not)
            {
                row.B = row.A;
            }
        }

        /// <summary>
        /// What the row presses, once per emulation tick: the game's
        /// `EvaluateEmulation`.
        ///
        /// The edge detector's answer clears the flag as it is read, which is what
        /// makes it a pulse of exactly one tick.
        /// </summary>
        public static bool Answer(LogicRow row, int gate)
        {
            switch (gate)
            {
                case Not: return !row.A;
                case And: return row.A && row.B;
                case Or: return row.A || row.B;
                case Nor: return !(row.A || row.B);
                case Nand: return !(row.A && row.B);
                case Xor: return row.A != row.B;
                case Xnor: return row.A == row.B;
                case Random:
                case SRLatch:
                case DLatch: return row.AToggled;
                case Counter: return row.Count == 0 && row.LastCount == 3;
                case EdgeDetect:
                    bool edge = row.AToggled;
                    row.AToggled = false;
                    return edge;
                default: return false;
            }
        }
    }
}
