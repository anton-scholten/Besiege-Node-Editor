using System;
using Modding;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Every picture this mod draws, loaded from shipped files that
    /// `tools/make-glyphs.py` makes (drawing them at runtime stalled the first
    /// open). Names match `Mod.xml`. `Texture`s for `RawImage`, except the one
    /// sprite, <see cref="Plated"/>.
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
        private static Texture timer;
        private static Texture ring;
        private static Texture dot;
        private static Texture rounded;
        private static Texture bars;
        private static Texture grid;
        private static Texture dash;
        private static Texture dashDown;
        private static Texture arrow;
        private static readonly Texture2D[] corner = new Texture2D[2];
        private static Sprite plate;

        private static bool looked;

        /// <summary>The three switch-column headings.</summary>
        public static Texture Hold { get { Look(); return hold; } }
        public static Texture Stop { get { Look(); return stop; } }
        public static Texture Loop { get { Look(); return loop; } }

        /// <summary>The comment node's palette icon and the board's pin. `Note`,
        /// not `Bubble`: a gate's inverting bubble is a picture too.</summary>
        public static Texture Note { get { Look(); return bubble; } }

        public static Texture Pin { get { Look(); return pinned; } }

        /// <summary>The timer's palette icon, and the ghost carried off
        /// it.</summary>
        public static Texture Timer { get { Look(); return timer; } }

        /// <summary>The mark on the heading of the column the table is sorted by,
        /// turned over for a descending sort.</summary>
        public static Texture Arrow { get { Look(); return arrow; } }

        /// <summary>A port with nothing on it, and one with a wire on it.</summary>
        public static Texture Ring { get { Look(); return ring; } }

        public static Texture Dot { get { Look(); return dot; } }

        /// <summary>A rounded square, for anything that wants a plate with the
        /// corners off -- the delete mark behind a row's number.</summary>
        public static Texture Rounded { get { Look(); return rounded; } }

        /// <summary>Diagonal bars for a cell that is not read, drawn to
        /// repeat.</summary>
        public static Texture Bars { get { Look(); return bars; } }

        /// <summary>One square of the board's grid, drawn to repeat.</summary>
        public static Texture Grid { get { Look(); return grid; } }

        /// <summary>One dash and its gap, in pixels: the dash textures' length, so
        /// a dash is square whichever way it runs.</summary>
        public const float DashStep = 8f;

        public static Texture Dash { get { Look(); return dash; } }

        /// <summary>The same dashes running the other way. A texture whose pattern
        /// runs across it is a plain line laid down an edge.</summary>
        public static Texture DashDown { get { Look(); return dashDown; } }

        /// <summary>The double-headed arrow cursor for a corner that can be
        /// dragged.
        /// </summary>
        /// <param name="other">The other diagonal: the top-right and bottom-left
        /// corners.
        /// </param>
        public static Texture2D Resizer(bool other)
        {
            int which = other ? 1 : 0;
            if (corner[which] == null)
            {
                corner[which] = Arrows(other);
            }
            return corner[which] as Texture2D;
        }

        private const int CornerSize = 32;

        /// <summary>Drawn here rather than shipped: `Cursor.SetCursor` wants a
        /// readable, uncompressed texture with no mipmaps.</summary>
        private static Texture2D Arrows(bool other)
        {
            Texture2D made = new Texture2D(CornerSize, CornerSize,
                                           TextureFormat.ARGB32, false);
            made.wrapMode = TextureWrapMode.Clamp;
            made.hideFlags = HideFlags.HideAndDontSave;
            Color[] pixels = new Color[CornerSize * CornerSize];
            for (int y = 0; y < CornerSize; y++)
            {
                for (int x = 0; x < CornerSize; x++)
                {
                    float u = (x + 0.5f) / CornerSize;
                    float v = (y + 0.5f) / CornerSize;
                    if (other)
                    {
                        // Mirrored, which is the same arrow turned a quarter turn.
                        u = 1f - u;
                    }
                    // The shaft: a band along the leading diagonal. The heads: two
                    // triangles, one at each end of it.
                    bool shaft = Mathf.Abs(u - v) < 0.10f && u > 0.20f && u < 0.80f;
                    bool head = (u + v < 0.42f && Mathf.Abs(u - v) < 0.22f)
                             || (u + v > 1.58f && Mathf.Abs(u - v) < 0.22f);
                    bool on = shaft || head;
                    // A dark edge round it, or a white arrow is invisible on a
                    // white machine.
                    bool edge = !on
                        && (Mathf.Abs(u - v) < 0.16f && u > 0.14f && u < 0.86f
                            || u + v < 0.48f && Mathf.Abs(u - v) < 0.28f
                            || u + v > 1.52f && Mathf.Abs(u - v) < 0.28f);
                    pixels[y * CornerSize + x] = on ? Color.white
                        : (edge ? new Color(0f, 0f, 0f, 0.85f)
                                : new Color(0f, 0f, 0f, 0f));
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
        }

        public static Texture Gate(int gate)
        {
            Look();
            return gate < 0 || gate >= gates.Length ? null : gates[gate];
        }

        /// <summary>The plate's size and corner share, which the sliced border
        /// comes from. Drawn at twice display size, so a scaled-up interface still
        /// shows a curve.
        /// </summary>
        private const int PlateSize = 128;

        /// <summary>How many times over the plate is drawn, against the size a
        /// corner takes on the board.</summary>
        private const float PlateScale = PlateSize / 64f;
        private const float Corner = 0.30f;

        /// <summary>The plate as a nine-sliced sprite: corners keep their radius at
        /// any node size.</summary>
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
                // The border is the corner and a pixel, in picture pixels, read at
                // the picture's own pixels per unit so a corner keeps its size on
                // the board.
                float edge = PlateSize * 0.5f * Corner + PlateScale;
                plate = Sprite.Create(drawn,
                                      new Rect(0f, 0f, drawn.width, drawn.height),
                                      new Vector2(0.5f, 0.5f), 100f * PlateScale, 0,
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
            hold = Fetch("NodeEditor_hold");
            stop = Fetch("NodeEditor_stop");
            loop = Fetch("NodeEditor_loop");
            bubble = Fetch("NodeEditor_bubble");
            pinned = Fetch("NodeEditor_pin");
            timer = Fetch("NodeEditor_timer");
            for (int i = 0; i < Named.Length; i++)
            {
                gates[i] = Fetch("NodeEditor_gate_" + Named[i]);
            }
            ring = Fetch("NodeEditor_ring");
            dot = Fetch("NodeEditor_dot");
            rounded = Fetch("NodeEditor_plate");
            arrow = Fetch("NodeEditor_arrow");
            // Repeated, so wrapped: a clamped tile repeats as smears of its last
            // row.
            grid = Tiled(Fetch("NodeEditor_grid"), false);
            dash = Tiled(Fetch("NodeEditor_dash"), true);
            dashDown = Tiled(Fetch("NodeEditor_dashdown"), true);
            bars = Tiled(Fetch("NodeEditor_bars"), false);
        }

        /// <summary>A repeating texture, point-sampled for the short
        /// dashes.</summary>
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
