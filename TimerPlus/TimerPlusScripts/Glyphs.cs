using System;
using Modding;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// Every picture this mod draws with, loaded from the files it ships.
    ///
    /// They were drawn at runtime once -- a pixel at a time with sixteen samples
    /// in each -- and the twenty of them together were a stall you could see on
    /// the first block opened. Nothing about them changes between runs, so they
    /// are drawn by `tools/make-glyphs.py` and shipped beside the block's mesh.
    /// Change a shape there and run it; the names below are what `Mod.xml`
    /// declares.
    ///
    /// A `RawImage` takes the `Texture` the resource system hands over as it is;
    /// an `Image` would want a `Sprite` made from it and owned by somebody -- which
    /// is what <see cref="Plated"/> is, and the one place it is worth it.
    /// </summary>
    public static class Glyphs
    {
        /// <summary>The gates, in the order Besiege lists them, which is the order
        /// <see cref="Gates"/> uses.</summary>
        private static readonly string[] Named =
        {
            "not", "and", "or", "nor", "nand", "xor", "xnor", "random",
            "srlatch", "dlatch", "counter", "edge",
        };

        private static readonly Texture[] gates = new Texture[Named.Length];

        private static Texture hold;
        private static Texture stop;
        private static Texture loop;
        private static Texture bubble;
        private static Texture pinned;
        private static Texture ring;
        private static Texture dot;
        private static Texture rounded;
        private static Texture bars;
        private static Texture grid;
        private static Texture dash;
        private static Texture dashDown;
        private static Texture arrow;
        private static readonly Texture[] corner = new Texture[2];
        private static Sprite plate;

        private static bool looked;

        /// <summary>The three switch-column headings.</summary>
        public static Texture Hold { get { Look(); return hold; } }
        public static Texture Stop { get { Look(); return stop; } }
        public static Texture Loop { get { Look(); return loop; } }

        /// <summary>The node editor's two shipped icons: a speech bubble for the
        /// comment node in its palette, and the pin that holds the board open.
        /// `Note` rather than `Bubble` because a gate's inverting bubble is a
        /// picture too.</summary>
        public static Texture Note { get { Look(); return bubble; } }

        public static Texture Pin { get { Look(); return pinned; } }

        /// <summary>The mark on the heading of the column the table is sorted by,
        /// turned over for a descending sort.</summary>
        public static Texture Arrow { get { Look(); return arrow; } }

        /// <summary>A port with nothing on it, and one with a wire on it.</summary>
        public static Texture Ring { get { Look(); return ring; } }

        public static Texture Dot { get { Look(); return dot; } }

        /// <summary>A rounded square, for anything that wants a plate with the
        /// corners off -- the delete mark behind a row's number.</summary>
        public static Texture Rounded { get { Look(); return rounded; } }

        /// <summary>Diagonal bars, for a cell that is there but not read -- input B
        /// on a gate that takes one input. One tile of a stripe, drawn to repeat.
        /// </summary>
        public static Texture Bars { get { Look(); return bars; } }

        /// <summary>One square of the board's grid, drawn to repeat.</summary>
        public static Texture Grid { get { Look(); return grid; } }

        /// <summary>How long one dash and its gap are, in pixels: the two dash
        /// textures are this many texels along the way they run, so a strip
        /// repeated once per <see cref="DashStep"/> pixels has square dashes
        /// whichever way it lies.</summary>
        public const float DashStep = 8f;

        public static Texture Dash { get { Look(); return dash; } }

        /// <summary>The same dashes running the other way. A texture whose pattern
        /// runs across it is a plain line laid down an edge.</summary>
        public static Texture DashDown { get { Look(); return dashDown; } }

        /// <summary>
        /// The pointer over a corner that can be dragged: a double-headed arrow
        /// along the diagonal.
        /// </summary>
        /// <param name="other">The other diagonal. A corner at the top left or the
        /// bottom right is pulled one way and one at the top right or the bottom
        /// left the other.</param>
        public static Texture2D Resizer(bool other)
        {
            Look();
            return corner[other ? 1 : 0] as Texture2D;
        }

        public static Texture Gate(int gate)
        {
            Look();
            return gate < 0 || gate >= gates.Length ? null : gates[gate];
        }

        /// <summary>How big the plate is and how much of its half-width the corners
        /// take: the sliced sprite's border is worked out from them.</summary>
        private const int PlateSize = 64;
        private const float Corner = 0.30f;

        /// <summary>
        /// The plate as a nine-sliced sprite: the corners keep their radius
        /// whatever it is stretched to.
        ///
        /// A texture stretched across a wide node has wide oval corners, which a
        /// board of nodes of different sizes shows up at once. Sliced, the four
        /// corners are drawn at their own size and only the straight parts stretch.
        /// </summary>
        public static Sprite Plated
        {
            get
            {
                if (plate != null)
                {
                    return plate;
                }
                Texture2D drawn = Rounded as Texture2D;
                if (drawn == null)
                {
                    return null;
                }
                // The border is the corner itself, and a pixel over so the straight
                // edge starts outside the curve rather than in it.
                float edge = PlateSize * 0.5f * Corner + 1f;
                plate = Sprite.Create(drawn,
                                      new Rect(0f, 0f, drawn.width, drawn.height),
                                      new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect,
                                      new Vector4(edge, edge, edge, edge));
                plate.hideFlags = HideFlags.HideAndDontSave;
                return plate;
            }
        }

        private static void Look()
        {
            if (looked)
            {
                return;
            }
            looked = true;
            hold = Fetch("TimerPlus_hold");
            stop = Fetch("TimerPlus_stop");
            loop = Fetch("TimerPlus_loop");
            bubble = Fetch("TimerPlus_bubble");
            pinned = Fetch("TimerPlus_pin");
            for (int i = 0; i < Named.Length; i++)
            {
                gates[i] = Fetch("TimerPlus_gate_" + Named[i]);
            }
            ring = Fetch("TimerPlus_ring");
            dot = Fetch("TimerPlus_dot");
            rounded = Fetch("TimerPlus_plate");
            arrow = Fetch("TimerPlus_arrow");
            corner[0] = Fetch("TimerPlus_resize");
            corner[1] = Fetch("TimerPlus_resize2");
            // The three that are laid end to end rather than drawn once. A texture
            // loaded from a file is clamped by default, and a clamped tile drawn
            // ten times over is one tile and nine smears of its last row.
            grid = Tiled(Fetch("TimerPlus_grid"), false);
            dash = Tiled(Fetch("TimerPlus_dash"), true);
            dashDown = Tiled(Fetch("TimerPlus_dashdown"), true);
            bars = Tiled(Fetch("TimerPlus_bars"), false);
        }

        /// <summary>A texture that is repeated rather than stretched. Point
        /// sampling for the dashes: they are a few texels long, and filtered they
        /// are a smudge.</summary>
        private static Texture Tiled(Texture made, bool sharp)
        {
            if (made != null)
            {
                made.wrapMode = TextureWrapMode.Repeat;
                if (sharp)
                {
                    made.filterMode = FilterMode.Point;
                }
            }
            return made;
        }

        private static Texture Fetch(string name)
        {
            try
            {
                return ModResource.GetTexture(name);
            }
            catch (Exception e)
            {
                Log.Warn("could not load the texture '" + name + "': " + e.Message);
                return null;
            }
        }
    }
}
