using System;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Blinks the block's display red when one of its rows fires.
    ///
    /// The block wears one material and Besiege's OBJ loading gives no submeshes
    /// to hang a second one on -- but a *texel* is addressable. The mesh is
    /// unwrapped onto a palette of flat patches, one per colour of the model, so
    /// the display's triangles all look at one patch and repainting that patch
    /// repaints the display and nothing else. See notes 02, "Colouring part of a
    /// block's mesh, without a second material", and the sibling Orchestra mod's
    /// `BraidsBehaviour.Dress`, which this follows.
    ///
    /// Two by two rather than the sixteen the mesh tool writes: the four colours
    /// sit at the middles of four quadrants, which is exactly what a two-by-two
    /// point-sampled texture is. Building it here rather than copying the shipped
    /// one also means nothing has to read a texture back off the GPU, which is a
    /// thing a texture is allowed to refuse.
    ///
    /// Per block: its own material and its own texture, so one block blinking does
    /// not blink every Timer Plus on the machine.
    /// </summary>
    public class Glow
    {
        /// <summary>How long the display stays red. Simulation seconds, so it
        /// stretches with the time-scale slider the way the timers do.</summary>
        private const float Seconds = 0.1f;

        /// <summary>
        /// The model's own colours, as `tools/make-block-mesh.py` writes them into
        /// `TimerPlus.png`, and where its palette puts each one.
        ///
        /// The tool checks these against the palette it writes, so a model change
        /// that moves a colour fails the build rather than lighting the wrong part
        /// of the block.
        /// </summary>
        private static readonly Color Body = Hue(28, 205, 243);
        private static readonly Color Buttons = Hue(90, 90, 90);
        private static readonly Color Display = Hue(195, 227, 147);
        private static readonly Color Spare = Hue(0, 0, 0);

        /// <summary>What the display goes the instant a row fires, and what it
        /// fades from. Besiege's own red, which is what the game paints anything
        /// urgent.</summary>
        private static readonly Color Lit = new Color(0.92f, 0.13f, 0.29f, 1f);

        private MeshRenderer worn;
        private Material paint;
        private Texture2D skin;
        /// <summary>How red the display was painted last, so a frame that would
        /// paint it the same colour again does not touch the texture at all.
        /// Negative until it has been painted once.</summary>
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
                // The renderer whose material *has* a main texture: that is the
                // mesh the block is actually seen as, and it settles in passing
                // that the shader takes its picture from where this puts one.
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

            // Checked rather than remembered. Besiege may build the visual after
            // this first looked, and a repaint replaces the material outright --
            // a block dressed once and trusted is a block quietly undone.
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

        /// <summary>
        /// Repaints the patch the display's triangles look at.
        ///
        /// The tool lays the palette out left to right and top to bottom, and a
        /// texture counts v up from the bottom: the body is the top-left patch and
        /// so is texel (0,1), the buttons (1,1), the display the bottom-left patch
        /// and so texel (0,0).
        /// </summary>
        private void Ink(float amount)
        {
            // A shade nobody could tell from the one already on the block is not
            // worth an upload; landing exactly on nothing is, or the display keeps
            // a tint of red for the rest of the run.
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
