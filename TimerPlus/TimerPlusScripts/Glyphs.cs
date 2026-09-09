using System;
using Modding;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// The three pictures the switch columns are headed with: hold to run, allow
    /// stop, loop.
    ///
    /// A letter apiece is what a column twenty-six units wide has room for, and
    /// "H", "S" and "L" say nothing to somebody who has not read the tooltip. A
    /// picture says it at a glance and fits in the same box.
    ///
    /// They are declared in <c>Mod.xml</c> and fetched by name, which is the only
    /// way the resource system hands anything over -- see notes 10. Asked for
    /// once and remembered, because a heading is rebuilt every time the table is,
    /// and a lookup that failed is remembered as a failure too: a mod whose
    /// resources did not load is not going to start loading them mid-session, and
    /// the headings fall back to lettering.
    /// </summary>
    public static class Glyphs
    {
        private static bool looked;
        private static Texture hold;
        private static Texture stop;
        private static Texture loop;
        private static Texture arrow;

        public static Texture Hold { get { Look(); return hold; } }
        public static Texture Stop { get { Look(); return stop; } }
        public static Texture Loop { get { Look(); return loop; } }

        /// <summary>
        /// The mark on the heading of the column the table is sorted by: a
        /// triangle, drawn here rather than shipped, and turned over for a
        /// descending sort.
        ///
        /// It was a "^" and a "v" -- two letters of different weight and different
        /// height, which read as two different marks rather than one mark two ways
        /// up. One picture turned over cannot have that problem.
        /// </summary>
        public static Texture Arrow
        {
            get
            {
                if (arrow == null)
                {
                    arrow = Point();
                }
                return arrow;
            }
        }

        private static Texture rounded;

        /// <summary>
        /// A rounded square, white on transparency, for anything that wants a
        /// plate with the corners off -- the delete mark behind a row's number.
        /// Drawn rather than shipped, for the same reason the arrow is.
        /// </summary>
        public static Texture Rounded
        {
            get
            {
                if (rounded == null)
                {
                    rounded = Plate();
                }
                return rounded;
            }
        }

        private static Texture bars;

        /// <summary>
        /// Diagonal bars, for a cell that is there but not read -- input B on a
        /// gate that takes one input.
        ///
        /// One tile of a stripe, drawn to repeat: the picture is a square with the
        /// stripe crossing it corner to corner, so laid end to end it is one
        /// unbroken diagonal. Wrapped rather than clamped for the same reason.
        /// </summary>
        public static Texture Bars
        {
            get
            {
                if (bars == null)
                {
                    bars = Striped();
                }
                return bars;
            }
        }

        private const int BarSize = 24;

        /// <summary>How much of a tile the stripe covers.</summary>
        private const float BarWidth = 0.34f;

        private static Texture2D Striped()
        {
            Texture2D made = new Texture2D(BarSize, BarSize, TextureFormat.ARGB32,
                                           false);
            made.wrapMode = TextureWrapMode.Repeat;
            Color[] pixels = new Color[BarSize * BarSize];
            for (int y = 0; y < BarSize; y++)
            {
                for (int x = 0; x < BarSize; x++)
                {
                    int on = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 4f) / BarSize;
                            float v = (y + (sy + 0.5f) / 4f) / BarSize;
                            // Distance along the diagonal, wrapped: one stripe per
                            // tile, and the wrap is what makes the tiles join.
                            float along = u + v;
                            if (along >= 1f)
                            {
                                along -= 1f;
                            }
                            if (along < BarWidth)
                            {
                                on++;
                            }
                        }
                    }
                    pixels[y * BarSize + x] = new Color(1f, 1f, 1f, on / 16f);
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
        }

        private static readonly Texture[] gates = new Texture[Gates.Count];
        private static Texture ring;
        private static Texture dot;

        /// <summary>
        /// The picture for a gate: the drawn shape everyone knows for the seven
        /// that have one, and an invented mark for the five that do not.
        ///
        /// Drawn here rather than shipped, like the arrow and the bars: a shape
        /// described in code is a shape that scales, and twelve small PNGs in
        /// Resources would be twelve more things to keep in step with this list.
        /// </summary>
        public static Texture Gate(int gate)
        {
            if (gate < 0 || gate >= gates.Length)
            {
                return null;
            }
            if (gates[gate] == null)
            {
                gates[gate] = Shape(gate);
            }
            return gates[gate];
        }

        /// <summary>A port with nothing in it, and one with a wire on it.</summary>
        public static Texture Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Circle(false);
                }
                return ring;
            }
        }

        public static Texture Dot
        {
            get
            {
                if (dot == null)
                {
                    dot = Circle(true);
                }
                return dot;
            }
        }

        private static Texture dash;
        private static Texture dashDown;

        /// <summary>How long one dash and its gap are, in pixels. The two dash
        /// textures are this many texels along the way they run, so a strip
        /// repeated once per <see cref="DashStep"/> pixels has square dashes
        /// whichever way it lies.</summary>
        public const float DashStep = 8f;

        /// <summary>
        /// A dash and a gap, drawn to repeat: laid along an edge it is a dashed
        /// line, which is how a selection says it is a selection rather than a
        /// thing with a border.
        /// </summary>
        public static Texture Dash
        {
            get
            {
                if (dash == null)
                {
                    dash = Dashed(true);
                }
                return dash;
            }
        }

        /// <summary>The same dashes running the other way.
        ///
        /// A texture whose pattern runs across it is a plain line when it is laid
        /// down an edge -- repeating it vertically repeats rows that are all the
        /// same. So the upright edges of a selection get their own.</summary>
        public static Texture DashDown
        {
            get
            {
                if (dashDown == null)
                {
                    dashDown = Dashed(false);
                }
                return dashDown;
            }
        }

        private static Texture2D Dashed(bool across)
        {
            int run = (int)DashStep;
            Texture2D made = new Texture2D(across ? run : 2, across ? 2 : run,
                                           TextureFormat.ARGB32, false);
            made.wrapMode = TextureWrapMode.Repeat;
            made.filterMode = FilterMode.Point;
            made.hideFlags = HideFlags.HideAndDontSave;
            Color[] pixels = new Color[run * 2];
            for (int along = 0; along < run; along++)
            {
                // Five on, three off.
                Color ink = new Color(1f, 1f, 1f, along < 5 ? 1f : 0f);
                if (across)
                {
                    pixels[along] = ink;
                    pixels[run + along] = ink;
                }
                else
                {
                    pixels[along * 2] = ink;
                    pixels[along * 2 + 1] = ink;
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
        }

        private static Texture grid;

        /// <summary>
        /// One square of the board's grid, drawn to repeat: two faint lines along
        /// two edges, so a sheet of them is a grid and not a set of boxes.
        ///
        /// It is what makes panning and zooming visible -- without it an empty
        /// board looks the same however far it has been moved.
        /// </summary>
        public static Texture Grid
        {
            get
            {
                if (grid == null)
                {
                    grid = Squared();
                }
                return grid;
            }
        }

        private const int GridSize = 32;

        private static Texture2D Squared()
        {
            Texture2D made = new Texture2D(GridSize, GridSize, TextureFormat.ARGB32,
                                           false);
            made.wrapMode = TextureWrapMode.Repeat;
            made.hideFlags = HideFlags.HideAndDontSave;
            Color[] pixels = new Color[GridSize * GridSize];
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    bool line = x == 0 || y == 0;
                    pixels[y * GridSize + x] = new Color(1f, 1f, 1f,
                                                         line ? 0.12f : 0f);
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
        }

        private static readonly Texture2D[] corner = new Texture2D[2];

        /// <summary>
        /// The pointer over a corner that can be dragged: a double-headed arrow
        /// lying along the diagonal, which is what every interface uses to say
        /// "this resizes".
        /// </summary>
        /// <param name="other">The other diagonal. A corner at the top left or
        /// the bottom right is pulled one way and one at the top right or the
        /// bottom left the other, and an arrow pointing the wrong way says the
        /// window resizes in a direction it does not.</param>
        public static Texture2D Resizer(bool other)
        {
            int which = other ? 1 : 0;
            if (corner[which] == null)
            {
                corner[which] = Arrows(other);
            }
            return corner[which];
        }

        private const int CornerSize = 32;

        private static Texture2D Arrows(bool other)
        {
            Texture2D made = New(CornerSize);
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

        private const int GateSize = 64;

        /// <summary>How thick a drawn outline is, as a fraction of the square.</summary>
        private const float Stroke = 0.085f;

        private static Texture2D Circle(bool filled)
        {
            Texture2D made = New(GateSize);
            Color[] pixels = new Color[GateSize * GateSize];
            for (int y = 0; y < GateSize; y++)
            {
                for (int x = 0; x < GateSize; x++)
                {
                    int on = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 4f) / GateSize;
                            float v = (y + (sy + 0.5f) / 4f) / GateSize;
                            float d = Away(u, v, 0.5f, 0.5f);
                            if (filled ? d <= 0.36f : (d <= 0.40f && d >= 0.40f - Stroke * 1.6f))
                            {
                                on++;
                            }
                        }
                    }
                    pixels[y * GateSize + x] = new Color(1f, 1f, 1f, on / 16f);
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
        }

        private static Texture2D Shape(int gate)
        {
            Texture2D made = New(GateSize);
            Color[] pixels = new Color[GateSize * GateSize];
            for (int y = 0; y < GateSize; y++)
            {
                for (int x = 0; x < GateSize; x++)
                {
                    int on = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 4f) / GateSize;
                            // v counts up from the bottom in a texture.
                            float v = 1f - (y + (sy + 0.5f) / 4f) / GateSize;
                            if (Inside(gate, u, v))
                            {
                                on++;
                            }
                        }
                    }
                    pixels[y * GateSize + x] = new Color(1f, 1f, 1f, on / 16f);
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
        }

        private static Texture2D New(int side)
        {
            Texture2D made = new Texture2D(side, side, TextureFormat.ARGB32, false);
            made.wrapMode = TextureWrapMode.Clamp;
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        private static float Away(float u, float v, float x, float y)
        {
            float dx = u - x;
            float dy = v - y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The body of an AND: a rectangle with a half-disc on its nose.
        /// </summary>
        private static bool AndBody(float u, float v)
        {
            if (v < 0.18f || v > 0.82f)
            {
                return false;
            }
            return (u >= 0.14f && u <= 0.48f) || Away(u, v, 0.48f, 0.5f) <= 0.32f;
        }

        /// <summary>The body of an OR: the same nose, hollowed at the back.</summary>
        private static bool OrBody(float u, float v)
        {
            return AndBody(u, v) && Away(u, v, -0.10f, 0.5f) >= 0.31f;
        }

        private static bool Bubble(float u, float v)
        {
            return Away(u, v, 0.86f, 0.5f) <= 0.10f;
        }

        /// <summary>A rectangle's outline, for the gates with no drawn shape of
        /// their own.</summary>
        private static bool Box(float u, float v, float x0, float y0,
                                float x1, float y1)
        {
            bool outer = u >= x0 && u <= x1 && v >= y0 && v <= y1;
            float t = Stroke * 0.8f;
            bool inner = u >= x0 + t && u <= x1 - t && v >= y0 + t && v <= y1 - t;
            return outer && !inner;
        }

        private static bool Bar(float u, float v, float x0, float y0,
                                float x1, float y1)
        {
            return u >= x0 && u <= x1 && v >= y0 && v <= y1;
        }

        /// <summary>Whether a point is in the gate's mark.</summary>
        private static bool Inside(int gate, float u, float v)
        {
            switch (gate)
            {
                case Gates.Not:
                    // A triangle pointing right, with the bubble that makes it a
                    // NOT rather than a buffer.
                    return Triangle(u, v) || Bubble(u, v);
                case Gates.And:
                    return AndBody(u, v);
                case Gates.Nand:
                    return AndBody(u - 0.06f, v) || Bubble(u, v);
                case Gates.Or:
                    return OrBody(u, v);
                case Gates.Nor:
                    return OrBody(u - 0.06f, v) || Bubble(u, v);
                case Gates.Xor:
                    return OrBody(u, v) || Crescent(u, v);
                case Gates.Xnor:
                    return OrBody(u - 0.06f, v) || Crescent(u - 0.06f, v)
                        || Bubble(u, v);
                case Gates.Random:
                    // A die: three pips on a square.
                    return Box(u, v, 0.16f, 0.16f, 0.84f, 0.84f)
                        || Away(u, v, 0.32f, 0.32f) <= 0.07f
                        || Away(u, v, 0.50f, 0.50f) <= 0.07f
                        || Away(u, v, 0.68f, 0.68f) <= 0.07f;
                case Gates.SRLatch:
                    // A switch: a lever off a pivot, which is what a latch does.
                    return Away(u, v, 0.26f, 0.30f) <= 0.09f
                        || Lever(u, v);
                case Gates.DLatch:
                    // A box with the notch a flip-flop's clock input is drawn as.
                    return Box(u, v, 0.20f, 0.16f, 0.84f, 0.84f) || Notch(u, v);
                case Gates.Counter:
                    // Bars climbing, which is what a counter does until it wraps.
                    return Bar(u, v, 0.18f, 0.18f, 0.34f, 0.42f)
                        || Bar(u, v, 0.42f, 0.18f, 0.58f, 0.62f)
                        || Bar(u, v, 0.66f, 0.18f, 0.82f, 0.82f);
                case Gates.EdgeDetect:
                    // A step: low, a rise, then high -- and the rise is the edge.
                    return Bar(u, v, 0.14f, 0.24f, 0.46f, 0.34f)
                        || Bar(u, v, 0.46f, 0.24f, 0.56f, 0.76f)
                        || Bar(u, v, 0.56f, 0.66f, 0.88f, 0.76f);
                default:
                    return Box(u, v, 0.18f, 0.18f, 0.82f, 0.82f);
            }
        }

        private static bool Triangle(float u, float v)
        {
            if (u < 0.16f || u > 0.74f)
            {
                return false;
            }
            float half = 0.34f * (0.74f - u) / 0.58f;
            return Mathf.Abs(v - 0.5f) <= half;
        }

        /// <summary>The second back curve that turns an OR into an XOR.</summary>
        private static bool Crescent(float u, float v)
        {
            float d = Away(u, v, -0.22f, 0.5f);
            return v >= 0.18f && v <= 0.82f && d <= 0.31f && d >= 0.31f - Stroke;
        }

        /// <summary>The lever of the latch's switch.</summary>
        private static bool Lever(float u, float v)
        {
            if (u < 0.26f || u > 0.82f)
            {
                return false;
            }
            float line = 0.30f + (u - 0.26f) * 0.75f;
            return Mathf.Abs(v - line) <= Stroke * 0.7f;
        }

        /// <summary>The clock notch on the left of a flip-flop.</summary>
        private static bool Notch(float u, float v)
        {
            if (u < 0.20f || u > 0.34f)
            {
                return false;
            }
            float half = 0.14f * (0.34f - u) / 0.14f;
            return Mathf.Abs(v - 0.5f) <= half;
        }

        private const int ArrowSize = 32;
        private const int PlateSize = 64;

        /// <summary>How much of the plate's half-width its corners take.</summary>
        private const float Corner = 0.30f;

        private static Texture2D Plate()
        {
            Texture2D made = new Texture2D(PlateSize, PlateSize, TextureFormat.ARGB32,
                                           false);
            made.wrapMode = TextureWrapMode.Clamp;
            float radius = PlateSize * 0.5f * Corner;
            float inner = PlateSize * 0.5f - radius;
            Color[] pixels = new Color[PlateSize * PlateSize];
            for (int y = 0; y < PlateSize; y++)
            {
                for (int x = 0; x < PlateSize; x++)
                {
                    int on = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            // Distance from the middle, per axis, with the straight
                            // part of each side taken off: what is left is the
                            // corner, and a corner is round where that is inside
                            // the radius.
                            float u = Mathf.Abs(x + (sx + 0.5f) / 4f - PlateSize * 0.5f);
                            float v = Mathf.Abs(y + (sy + 0.5f) / 4f - PlateSize * 0.5f);
                            float du = Mathf.Max(0f, u - inner);
                            float dv = Mathf.Max(0f, v - inner);
                            if (u <= PlateSize * 0.5f && v <= PlateSize * 0.5f
                                && du * du + dv * dv <= radius * radius)
                            {
                                on++;
                            }
                        }
                    }
                    pixels[y * PlateSize + x] = new Color(1f, 1f, 1f, on / 16f);
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
        }

        /// <summary>
        /// A white triangle on transparency, pointing up.
        ///
        /// Coverage is sampled four by four per pixel rather than tested once at
        /// the centre: at this size an unsampled edge is a staircase, and the mark
        /// sits next to text that Unity has antialiased properly.
        /// </summary>
        private static Texture2D Point()
        {
            Texture2D made = new Texture2D(ArrowSize, ArrowSize, TextureFormat.ARGB32,
                                           false);
            made.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[ArrowSize * ArrowSize];
            for (int y = 0; y < ArrowSize; y++)
            {
                for (int x = 0; x < ArrowSize; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            // 0..1 across the texture, with y up.
                            float u = (x + (sx + 0.5f) / 4f) / ArrowSize;
                            float v = (y + (sy + 0.5f) / 4f) / ArrowSize;
                            // The triangle: apex at the top middle, base along the
                            // bottom, sides meeting it at the corners.
                            if (v <= 1f && Mathf.Abs(u - 0.5f) <= (1f - v) * 0.5f)
                            {
                                inside++;
                            }
                        }
                    }
                    pixels[y * ArrowSize + x] = new Color(1f, 1f, 1f, inside / 16f);
                }
            }
            made.SetPixels(pixels);
            made.Apply();
            return made;
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
