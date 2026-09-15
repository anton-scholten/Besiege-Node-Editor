using System;
using System.Collections.Generic;
using Modding;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// How the node editor colours nodes and wires, and the colours it uses. The
    /// player's rather than the block's: kept in the mod's data folder through
    /// `ModIO` and shared by every open board. Nodes and wires each go UNICOLOR
    /// (one colour), COLOR (by kind; a wire shades between its ends' kinds) or
    /// RANDOM (from the kind row, seeded by what is coloured, so it stays put).
    /// </summary>
    public static class Hues
    {
        // Constants rather than an enum: declaring an enum segfaults Besiege's own
        // C# compiler.
        public const int Unicolor = 0;
        public const int Colored = 1;
        public const int Random = 2;
        private const int Modes = 3;

        private static readonly string[] names = { "UNICOLOR", "COLOR", "RANDOM" };

        /// <summary>Every way, in the order the selectors step through them, for
        /// the list a right-click on one opens.</summary>
        public static List<string> Listed()
        {
            return new List<string>(names);
        }

        /// <summary>One colour per palette entry, in palette order: the two ends, a
        /// comment, a timer, then the gates in Besiege's order.</summary>
        public const int Slots = 4 + Gates.Count;

        public const int InputSlot = 0;
        public const int OutputSlot = 1;
        public const int NoteSlot = 2;
        public const int TimerSlot = 3;

        /// <summary>A row's slot: the timer's, or its gate's.</summary>
        public static int GateSlot(int gate)
        {
            if (gate == Gates.Timer)
            {
                return TimerSlot;
            }
            return 4 + Mathf.Clamp(gate, 0, Gates.Count - 1);
        }

        /// <summary>Where they are kept, in the mod's data folder.</summary>
        private const string File = "board-colours.txt";

        private static int nodeMode = Unicolor;
        private static int wireMode = Unicolor;

        /// <summary>The colour the nodes were always drawn in: their pictures and
        /// ports are white, tinted by nothing.</summary>
        private static Color node = Color.white;

        /// <summary>And the wires', see-through a little so a crossing reads as
        /// two wires rather than a knot.</summary>
        private static Color wire = new Color(0.55f, 0.72f, 0.85f, 0.85f);

        private static readonly Color[] kinds = new Color[Slots];

        private static bool loaded;
        private static bool dirty;

        /// <summary>How the nodes are coloured.</summary>
        public static int NodeMode
        {
            get { Load(); return nodeMode; }
            set { Load(); nodeMode = Wrapped(value); dirty = true; }
        }

        /// <summary>How the wires are coloured.</summary>
        public static int WireMode
        {
            get { Load(); return wireMode; }
            set { Load(); wireMode = Wrapped(value); dirty = true; }
        }

        /// <summary>What a selector set to that way says.</summary>
        public static string Named(int mode)
        {
            return names[Wrapped(mode)];
        }

        private static int Wrapped(int mode)
        {
            return ((mode % Modes) + Modes) % Modes;
        }

        public static Color Node
        {
            get { Load(); return node; }
            set { Load(); node = value; dirty = true; }
        }

        public static Color Wire
        {
            get { Load(); return wire; }
            set { Load(); wire = value; dirty = true; }
        }

        public static Color Kind(int slot)
        {
            Load();
            return slot >= 0 && slot < Slots ? kinds[slot] : node;
        }

        public static void SetKind(int slot, Color colour)
        {
            Load();
            if (slot >= 0 && slot < Slots)
            {
                kinds[slot] = colour;
                dirty = true;
            }
        }

        /// <summary>Whether the nodes are drawn in the row's colours rather than
        /// in the one.</summary>
        public static bool Kinded
        {
            get
            {
                return NodeMode != Unicolor;
            }
        }

        /// <summary>Whether a wire shades from the kind colour of the node it
        /// leaves to that of the node it reaches.</summary>
        public static bool Graded
        {
            get
            {
                return WireMode == Colored;
            }
        }

        /// <summary>A node's colour: its kind's slot, and the number it is known
        /// by for a random choice.</summary>
        public static Color NodeOf(int slot, int seed)
        {
            int now = NodeMode;
            if (now == Colored)
            {
                return Kind(slot);
            }
            if (now == Random)
            {
                return kinds[Pick(seed)];
            }
            return node;
        }

        /// <summary>A wire's colour where it is one colour along its
        /// length.</summary>
        public static Color WireOf(int seed)
        {
            if (WireMode == Random)
            {
                return Wired(kinds[Pick(seed)]);
            }
            return wire;
        }

        /// <summary>A node's colour as a wire wears it: as see-through as the
        /// unicolour wire is.</summary>
        public static Color Wired(Color colour)
        {
            colour.a = Wire.a;
            return colour;
        }

        /// <summary>Two numbers made into one, for a wire known by its two ends.
        /// </summary>
        public static int Mix(int a, int b)
        {
            unchecked
            {
                return (a * 486187739) ^ (b + 668265263 + (a << 6) + (a >> 2));
            }
        }

        /// <summary>A name as a number that is the same every run, as `GetHashCode`
        /// need not be.</summary>
        public static int Hashed(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        hash ^= text[i];
                        hash *= 16777619u;
                    }
                }
                return (int)hash;
            }
        }

        private static int Pick(int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % (uint)Slots);
            }
        }

        /// <summary>Six hex characters, the way Besiege writes a colour.</summary>
        public static string Hex(Color colour)
        {
            return Two(colour.r) + Two(colour.g) + Two(colour.b);
        }

        private static string Two(float channel)
        {
            return Mathf.Clamp(Mathf.RoundToInt(channel * 255f), 0, 255).ToString("X2");
        }

        /// <summary>Six (or three) hex characters read back, keeping the alpha
        /// the colour had: nothing here types an alpha.</summary>
        public static bool Parse(string typed, Color keep, out Color parsed)
        {
            if (typed == null || !ColorUtility.TryParseHtmlString("#" + typed.Trim(),
                                                                  out parsed))
            {
                parsed = keep;
                return false;
            }
            parsed.a = keep.a;
            return true;
        }

        /// <summary>Writes the colours down once nothing is being dragged, not
        /// every frame of a drag.</summary>
        public static void Settle()
        {
            if (dirty && !Input.GetMouseButton(0))
            {
                Save();
            }
        }

        /// <summary>The starting colours: white nodes, blue wires, and a different
        /// pale hue per kind.</summary>
        private static void Defaults()
        {
            node = Color.white;
            wire = new Color(0.55f, 0.72f, 0.85f, 0.85f);
            for (int i = 0; i < Slots; i++)
            {
                kinds[i] = Color.HSVToRGB(i / (float)Slots, 0.55f, 1f);
            }
        }

        /// <summary>RESET COLORS: every colour back to its default. The ways of
        /// colouring are left alone.</summary>
        public static void Reset()
        {
            Load();
            Defaults();
            dirty = true;
        }

        private static void Load()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            Defaults();
            string text;
            try
            {
                // Spelt out: the game has a namespace called `ModIO` too, and a
                // bare `ModIO` inside `using Modding` finds that one first.
                text = Modding.ModIO.ReadAllText(File, true);
            }
            catch (Exception)
            {
                return;                     // never saved: the defaults stand
            }
            if (text == null)
            {
                return;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, equals);
                string said = line.Substring(equals + 1);
                if (key == "nodes" || key == "wires")
                {
                    // By name, so a way added between two others does not turn a
                    // saved choice into its neighbour.
                    int found = Array.IndexOf(names, said.ToUpperInvariant());
                    if (found >= 0 && key == "nodes")
                    {
                        nodeMode = found;
                    }
                    else if (found >= 0)
                    {
                        wireMode = found;
                    }
                }
                else if (key == "node")
                {
                    Parse(said, node, out node);
                }
                else if (key == "wire")
                {
                    Parse(said, wire, out wire);
                }
                else if (key == "kinds")
                {
                    string[] each = said.Split(';');
                    // Written before the timer had a slot: everything from its
                    // place on is one along, and the timer keeps its default.
                    bool older = each.Length == Slots - 1;
                    for (int k = 0; k < each.Length; k++)
                    {
                        int slot = older && k >= TimerSlot ? k + 1 : k;
                        if (slot < Slots)
                        {
                            Parse(each[k], kinds[slot], out kinds[slot]);
                        }
                    }
                }
            }
        }

        private static bool moaned;

        private static void Save()
        {
            dirty = false;
            string[] each = new string[Slots];
            for (int i = 0; i < Slots; i++)
            {
                each[i] = Hex(kinds[i]);
            }
            string text = "nodes=" + names[nodeMode] + "\n"
                        + "wires=" + names[wireMode] + "\n"
                        + "node=" + Hex(node) + "\n"
                        + "wire=" + Hex(wire) + "\n"
                        + "kinds=" + string.Join(";", each) + "\n";
            try
            {
                Modding.ModIO.WriteAllText(File, text, true);
            }
            catch (Exception e)
            {
                if (!moaned)
                {
                    moaned = true;
                    Log.Warn("could not keep the board's colours: " + e.Message);
                }
            }
        }
    }
}
