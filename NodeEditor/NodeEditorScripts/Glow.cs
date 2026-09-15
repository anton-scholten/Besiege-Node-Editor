using System;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Blinks the Timer Plus block's display red when a row fires, by repainting
    /// the one palette texel the display's triangles sample (notes 02). A
    /// two-by-two texture and material of its own per block, built here: nothing
    /// reads a texture back, and one block blinking leaves the rest alone.
    /// </summary>
    public class Glow
    {
        /// <summary>How long the display stays red. Simulation seconds, so it
        /// stretches with the time-scale slider the way the timers do.</summary>
        private const float Seconds = 0.1f;

        /// <summary>The model's palette colours as `tools/make-block-mesh.py`
        /// writes them; the tool fails the build if they move.</summary>
        private static readonly Color Body = Hue(28, 205, 243);
        private static readonly Color Buttons = Hue(90, 90, 90);
        private static readonly Color Display = Hue(195, 227, 147);
        private static readonly Color Spare = Hue(0, 0, 0);

        /// <summary>The red the display flashes and fades from: Besiege's
        /// own.</summary>
        private static readonly Color Lit = new Color(0.92f, 0.13f, 0.29f, 1f);

        private MeshRenderer worn;
        private Material paint;
        private Texture2D skin;
        /// <summary>The red last painted, so an unchanged frame touches nothing;
        /// negative before the first.</summary>
        private float inked = -1f;

        private float until;
        private bool complained;

        /// <summary>A row fired. Restarts the blink if one is already running, so a
        /// burst of rows reads as one long flash rather than a stuck one.</summary>
        public void Flash()
        {
            until = Time.time + Seconds;
        }

        /// <summary>How red the display is now: full the instant a row fires, none
        /// by <see cref="Seconds"/> later.</summary>
        private float Fading
        {
            get
            {
                float left = until - Time.time;
                return left <= 0f ? 0f : (left >= Seconds ? 1f : left / Seconds);
            }
        }

        /// <summary>Every frame of a run. Dresses the block if it is not wearing
        /// this yet, and paints the display the colour it should be now.</summary>
        public void Tick(MonoBehaviour block)
        {
            Dress(block);
            Ink(Fading);
        }

        /// <summary>The run ended: back to the colour the model was drawn in.</summary>
        public void Rest()
        {
            until = 0f;
            Ink(0f);
        }

        /// <summary>The material and the texture belong to this block, so they go
        /// when it does.</summary>
        public void Undress()
        {
            if (paint != null)
            {
                UnityEngine.Object.Destroy(paint);
                paint = null;
            }
            if (skin != null)
            {
                UnityEngine.Object.Destroy(skin);
                skin = null;
            }
            worn = null;
            inked = -1f;
        }

        private void Dress(MonoBehaviour block)
        {
            if (block == null)
            {
                return;
            }
            if (worn == null)
            {
                // The renderer whose material has a main texture is the mesh that
                // is seen.
                MeshRenderer[] found = block.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && found[i].sharedMaterial != null
                        && found[i].sharedMaterial.mainTexture != null)
                    {
                        worn = found[i];
                        break;
                    }
                }
                if (worn == null)
                {
                    if (!complained)
                    {
                        complained = true;
                        Log.Warn("no textured renderer on the block, so its display "
                                 + "cannot blink.");
                    }
                    return;
                }
            }

            if (worn.sharedMaterial == paint && skin != null)
            {
                return;
            }

            // Checked every time: Besiege may rebuild the visual or replace its
            // material.
            if (skin == null)
            {
                skin = new Texture2D(2, 2, TextureFormat.RGB24, false);
                // Point, or the four patches bleed into each other and every face
                // of the block ends up a blend of all of them.
                skin.filterMode = FilterMode.Point;
                skin.wrapMode = TextureWrapMode.Clamp;
                skin.hideFlags = HideFlags.HideAndDontSave;
                inked = -1f;                // so the first Ink writes
                Ink(Fading);
            }

            // Copied from whatever is on the block now, so a paint colour survives
            // this and only the picture underneath it is the block's own.
            Material next = new Material(worn.sharedMaterial);
            next.hideFlags = HideFlags.HideAndDontSave;
            next.mainTexture = skin;
            if (paint != null)
            {
                UnityEngine.Object.Destroy(paint);
            }
            paint = next;
            worn.sharedMaterial = paint;
        }

        /// <summary>Repaints the display's texel. v counts up, so the top-left body
        /// patch is texel (0,1) and the bottom-left display patch (0,0).</summary>
        private void Ink(float amount)
        {
            // Skips shades too close to tell apart, but always lands exactly on
            // zero.
            if (skin == null
                || (inked >= 0f && Mathf.Abs(amount - inked) < 0.02f
                    && (amount > 0f || inked == 0f)))
            {
                return;
            }
            skin.SetPixel(0, 1, Body);
            skin.SetPixel(1, 1, Buttons);
            skin.SetPixel(0, 0, Color.Lerp(Display, Lit, amount));
            skin.SetPixel(1, 0, Spare);
            skin.Apply();
            inked = amount;
        }

        private static Color Hue(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
