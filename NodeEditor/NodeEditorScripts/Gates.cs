using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Besiege's logic gate as a state machine with no Unity object in it, read out
    /// of `LogicGate` so a row behaves as the block it exports to. The game runs
    /// `UpdateState` twice a frame (keyboard, then emulated edges) and
    /// `EvaluateEmulation` once a tick: <see cref="Advance"/> and <see
    /// cref="Answer"/>.
    /// </summary>
    public static class Gates
    {
        // The gates in `GateType` order, which is the menu's: the block casts the
        // menu value to the enum. Constants, as an enum segfaults Besiege's
        // compiler.
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

        /// <summary>A timer rather than a gate, run by <see cref="Clock"/>.
        /// Numbered after the gates, so a saved gate number keeps its meaning. <see
        /// cref="Count"/> counts gates; <see cref="Kinds"/> counts menu
        /// entries.</summary>
        public const int Timer = 12;

        public const int Kinds = 13;

        /// <summary>Each gate's translation id, in menu order, as `LogicGate.Awake`
        /// builds its menu; the names below are the fallback.</summary>
        private static readonly int[] Ids =
        {
            3811, 3810, 3812, 3813, 3816, 3814, 3815, 4253, 4246, 4247, 4248, 4595
        };

        private static readonly string[] Spelled =
        {
            "NOT", "AND", "OR", "NOR", "NAND", "XOR", "XNOR",
            "RANDOM", "SR LATCH", "D LATCH", "COUNTER", "EDGE", "TIMER"
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
                // The timer as well, which has no translation of the gates' own to
                // look up and keeps its spelling.
                names = new string[Spelled.Length];
                for (int i = 0; i < Spelled.Length; i++)
                {
                    names[i] = Spelled[i];
                    if (i >= Ids.Length)
                    {
                        continue;
                    }
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

        /// <summary>Whether a gate reads input B. The three it does not are the
        /// three Besiege's `UpdateHidden` hides B for.</summary>
        public static bool UsesB(int gate)
        {
            // A timer has one key that starts it, and that is input A.
            return gate != Not && gate != Random && gate != EdgeDetect && gate != Timer;
        }

        /// <summary>Whether the row's one switch means "inverted" -- the edge
        /// detector's -- rather than "toggle mode".</summary>
        public static bool Inverts(int gate)
        {
            return gate == EdgeDetect;
        }

        /// <summary>Whether the switch does anything: combinational gates use
        /// toggle mode, the edge detector inverted, memory gates neither.</summary>
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

        /// <summary>"Held" as the game hands it over: a fresh press counts, and
        /// emulation is ORed in on both passes.</summary>
        public static bool Holding(bool pressed, bool held, bool emulated)
        {
            return pressed || held || emulated;
        }

        /// <summary>"Released" on the keyboard pass: only when no code is held, or
        /// a key on two codes would fire an edge detector early.</summary>
        public static bool Letting(bool pressed, bool held, bool released)
        {
            return released && !pressed && !held;
        }

        /// <summary>One pass of the game's `UpdateState`, where latches, counters
        /// and toggles live. Arguments in the game's order.</summary>
        /// <param name="gate">The gate, and <paramref name="mode"/> its switch:
        /// passed in, so the build can test this without a live block.</param>
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

        /// <summary>What the row presses this tick: the game's `EvaluateEmulation`.
        /// Reading the edge detector clears it, so its pulse lasts one
        /// tick.</summary>
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
