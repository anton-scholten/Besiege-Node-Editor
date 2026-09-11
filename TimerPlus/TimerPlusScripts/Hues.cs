using System;
using System.Collections.Generic;
using Modding;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// How the node editor colours its nodes and wires, and the colours it has to
    /// do it with.
    ///
    /// The player's and not the block's: a machine opened on somebody else's
    /// computer should look the way that person likes a board to look, and the
    /// colours of a board are not part of the circuit. So they are kept in the
    /// mod's own data folder -- the one place `ModIO` writes to (notes 01) -- and
    /// shared by every board that is open.
    ///
    /// Nodes and wires each have a way of their own, chosen apart:
    ///
    ///   UNICOLOR  a node in the one node colour; a wire in the one wire colour --
    ///             how the board always looked.
    ///   COLOR     a node in its kind's colour; a wire shaded from the kind colour
    ///             of the node it leaves to that of the node it reaches.
    ///   RANDOM    either drawn from the row of kind colours.
    ///
    /// "Random" is chosen once and kept: a colour that changed every time the board
    /// was drawn again would flicker at every edit. What it is chosen from is a
    /// number worked out from the thing being coloured, so the same node is the
    /// same colour tomorrow.
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

        /// <summary>One colour per thing the palette offers, in the palette's own
        /// order: the two ends, a comment, and the gates as Besiege lists them.
        /// </summary>
        public const int Slots = 3 + Gates.Count;

        public const int InputSlot = 0;
        public const int OutputSlot = 1;
        public const int NoteSlot = 2;

        public static int GateSlot(int gate)
        {
            return 3 + Mathf.Clamp(gate, 0, Gates.Count - 1);
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

        /// <summary>A wire's colour where it is one colour all along -- every way
        /// but the two that shade, whose wires are worked out from the nodes at
        /// their ends.</summary>
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

        /// <summary>A name made into a number the same way every time -- not
        /// `GetHashCode`, which nothing promises is the same from one run to the
        /// next.</summary>
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

        /// <summary>
        /// Writes the colours down once nothing is being dragged: a band dragged
        /// across its hues changes the colour every frame, and a file a frame is
        /// a lot of file for one choice.
        /// </summary>
        public static void Settle()
        {
            if (dirty && !Input.GetMouseButton(0))
            {
                Save();
            }
        }

        /// <summary>The colours a board starts with: white nodes, the blue wires it
        /// always had, and every kind a different pale hue, so the ways that use
        /// them show something before anybody has picked a colour.</summary>
        private static void Defaults()
        {
            node = Color.white;
            wire = new Color(0.55f, 0.72f, 0.85f, 0.85f);
            for (int i = 0; i < Slots; i++)
            {
                kinds[i] = Color.HSVToRGB(i / (float)Slots, 0.55f, 1f);
            }
        }

        /// <summary>RESET COLORS: every colour back to how it started. The way of
        /// colouring is left as it is -- that is the selector's, not a colour.
        /// </summary>
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
                    for (int k = 0; k < each.Length && k < Slots; k++)
                    {
                        Parse(each[k], kinds[k], out kinds[k]);
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
