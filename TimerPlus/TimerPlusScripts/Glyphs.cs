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
