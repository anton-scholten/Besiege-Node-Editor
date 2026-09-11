using System;
using UnityEngine;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// One colour to choose: six hex characters, typed or dragged sideways through
    /// the hues, with the colour itself as a stroke round the box. The box of the
    /// SpecialEffects spot light's colour row, made to fit over a palette button.
    ///
    /// UI Factory has no colour picker and Besiege's own is behind the mapper
    /// (notes 04). A plain class rather than a component: it owns a few objects
    /// under whatever it is made on, and nothing about it needs a frame.
    /// </summary>
    public class Swatch
    {
        /// <summary>What it is laid out by: the box itself.</summary>
        public RectTransform Root;

        /// <summary>A colour picked, typed or dragged.</summary>
        public Action<Color> Changed;

        private InputField box;
        private Text hash;
        private Image stroke;
        private Color colour = Color.white;

        /// <summary>Set while this writes to its own box, so the box does not hear
        /// that as the player typing (notes 04, "Committing a typed value").
        /// </summary>
        private bool echo;

        /// <summary>The clear space at each end of the box.</summary>
        private const float Pad = 3f;

        /// <summary>
        /// A little more room than the lettering is measured to need.
        ///
        /// A field whose text is even a pixel wider than its box scrolls the text
        /// to keep the caret in view, and a selection dragged across a scrolling
        /// field trails behind the pointer.
        /// </summary>
        private const float Spare = 4f;

        /// <summary>The size the lettering is measured at before it is scaled to
        /// fill the box: big enough that rounding does not show.</summary>
        private const int Probe = 40;

        /// <summary>What the widest colour is measured as. A colour of narrower
        /// characters is a little short of the edge; a wider one never spills.
        /// </summary>
        private const string Widest = "DDDDDD";

        /// <summary>The same drag through the hues the spot light's boxes have.
        /// </summary>
        private const float HuePerPixel = 0.003f;

        public static Swatch Make(Transform host)
        {
            Swatch made = new Swatch();
            GameObject go = UIF.Spawn(UIF.InputPrefab, host);
            if (go == null)
            {
                // Something to lay out and switch on and off, even so.
                go = new GameObject("Swatch");
                go.transform.SetParent(host, false);
                made.Root = go.AddComponent<RectTransform>();
                return made;
            }
            made.Root = go.GetComponent<RectTransform>();
            made.Build(go);
            made.Show(true);
            return made;
        }

        public Color Value
        {
            get { return colour; }
            set
            {
                if (value == colour)
                {
                    return;
                }
                colour = value;
                Show(false);
            }
        }

        /// <summary>Puts it where it goes, with its lettering as big as the box
        /// lets it be.</summary>
        public void Fit(float x, float y, float w, float h)
        {
            UIF.Fit(Root, x, y, w, h);
            Letter(w, h);
        }

        private void Build(GameObject go)
        {
            box = go.GetComponent<InputField>();
            if (box == null)
            {
                return;
            }
            Lettered(box.textComponent, UIF.Ink);
            Text ghost = box.placeholder as Text;
            Lettered(ghost, UIF.QuietInk);
            if (ghost != null)
            {
                ghost.text = "";
            }
            // Six hex characters and nothing else, and the hash a label of its own
            // so it cannot be typed over -- the way Besiege's colour boxes are.
            box.characterLimit = 6;
            box.onValidateInput = Hexed;
            box.onEndEdit.AddListener(Typed);
            stroke = Stroke(box);

            GameObject mark = new GameObject("Hash");
            mark.transform.SetParent(go.transform, false);
            RectTransform rect = mark.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(Pad, 0f);
            hash = mark.AddComponent<Text>();
            UIF.Style(hash, UIF.QuietInk, TextAnchor.MiddleLeft);
            hash.text = "#";
            hash.raycastTarget = false;

            // A sheet over the box that tells a click from a sideways drag: a click
            // types, a drag leaving by a side runs through the hues.
            GameObject sheet = new GameObject("Drag");
            sheet.transform.SetParent(go.transform, false);
            RectTransform over = sheet.AddComponent<RectTransform>();
            over.anchorMin = Vector2.zero;
            over.anchorMax = Vector2.one;
            over.offsetMin = Vector2.zero;
            over.offsetMax = Vector2.zero;
            Image catcher = sheet.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            ValueField drag = sheet.AddComponent<ValueField>();
            drag.field = box;
            drag.dragged = Nudged;
        }

        private static void Lettered(Text label, Color ink)
        {
            if (label == null)
            {
                return;
            }
            UIF.Style(label, ink, TextAnchor.MiddleLeft);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
        }

        /// <summary>
        /// The lettering sized to fill the box: as wide as `#` and six characters
        /// can be in what the box leaves, and no taller than the box.
        ///
        /// Asked of the font rather than guessed -- the generator that lays the
        /// text out is the only thing that knows how wide a letter is -- at one big
        /// size, and scaled, since a width is proportional to the size it is set
        /// at.
        /// </summary>
        /// <summary>How wide the box has to be, at a height, for its lettering to
        /// be as tall as that height lets it and still reach just to both ends --
        /// for a box with room to spare to be only as wide as what it says.
        /// </summary>
        public float Snug(float h)
        {
            float marked;
            float written;
            if (!Widths(out marked, out written))
            {
                return h * 4f;
            }
            int size = Mathf.Clamp(Tallest(h), 8, 40);
            return (marked + written) * size / Probe + Pad * 2f + Spare + 2f;
        }

        /// <summary>The biggest lettering a box this tall takes. A line of text is
        /// taller than its size, so a little under the box.</summary>
        private static int Tallest(float h)
        {
            return Mathf.FloorToInt(h * 0.72f);
        }

        /// <summary>How wide the hash and the widest colour are, set at
        /// <see cref="Probe"/>.</summary>
        private bool Widths(out float marked, out float written)
        {
            marked = 0f;
            written = 0f;
            Text label = box == null ? null : box.textComponent;
            if (label == null || label.font == null)
            {
                return false;
            }
            TextGenerationSettings asked = label.GetGenerationSettings(Vector2.zero);
            asked.fontSize = Probe;
            asked.resizeTextForBestFit = false;
            TextGenerator maker = label.cachedTextGeneratorForLayout;
            float much = Mathf.Max(0.0001f, label.pixelsPerUnit);
            marked = maker.GetPreferredWidth("#", asked) / much;
            written = maker.GetPreferredWidth(Widest, asked) / much;
            return true;
        }

        private void Letter(float w, float h)
        {
            Text label = box == null ? null : box.textComponent;
            float marked;
            float written;
            if (hash == null || !Widths(out marked, out written))
            {
                return;
            }
            float room = w - Pad * 2f - Spare;
            int wide = Mathf.FloorToInt(Probe * room / Mathf.Max(1f, marked + written));
            int tall = Tallest(h);
            int size = Mathf.Clamp(Mathf.Min(wide, tall), 8, 40);
            // And a little below the middle: the font sits its capitals high in
            // the line, and at a size that nearly fills the box their tops went
            // under the coloured stroke along its top edge.
            float drop = Mathf.Round(size * 0.12f);

            label.fontSize = size;
            Text ghost = box.placeholder as Text;
            if (ghost != null)
            {
                ghost.fontSize = size;
            }
            hash.fontSize = size;
            float hashed = marked * size / Probe + 1f;
            hash.rectTransform.sizeDelta = new Vector2(hashed, 0f);
            hash.rectTransform.anchoredPosition = new Vector2(Pad, -drop);
            Offset(label, Pad + hashed, drop);
            Offset(ghost, Pad + hashed, drop);
            box.ForceLabelUpdate();
        }

        private static void Offset(Text label, float left, float drop)
        {
            if (label == null)
            {
                return;
            }
            label.rectTransform.offsetMin = new Vector2(left, -drop);
            label.rectTransform.offsetMax = new Vector2(-Pad, -drop);
        }

        /// <summary>The colour as a stroke round the inside of the box: the box's
        /// own sliced sprite with its middle left out, so the corners are the
        /// box's and the value reads through (notes 04, "Borrowing a prefab's own
        /// corners").</summary>
        private static Image Stroke(InputField field)
        {
            GameObject go = new GameObject("Stroke");
            go.transform.SetParent(field.transform, false);
            go.transform.SetAsFirstSibling();
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            Image back = field.GetComponent<Image>();
            if (back != null)
            {
                image.sprite = back.sprite;
                image.type = back.type;
            }
            image.fillCenter = false;
            image.raycastTarget = false;
            return image;
        }

        private static char Hexed(string text, int index, char typed)
        {
            char up = char.ToUpper(typed);
            bool known = (up >= '0' && up <= '9') || (up >= 'A' && up <= 'F');
            return known ? up : '\0';
        }

        private void Typed(string typed)
        {
            if (echo)
            {
                return;
            }
            Color parsed;
            if (Hues.Parse(typed, colour, out parsed))
            {
                Set(parsed);
            }
            // Written back either way: a short answer read as a long one shows as
            // what it was read as, and something that is not a colour shows the
            // colour it did not replace.
            Show(true);
        }

        private void Nudged(float pixels)
        {
            float hue;
            float saturation;
            float brightness;
            Color.RGBToHSV(colour, out hue, out saturation, out brightness);
            Color picked = Color.HSVToRGB(Mathf.Repeat(hue + pixels * HuePerPixel, 1f),
                                          1f, 1f);
            picked.a = colour.a;
            Set(picked);
        }

        private void Set(Color picked)
        {
            bool same = picked == colour;
            colour = picked;
            // Written even with the caret in the box: a drag begins with a press,
            // which gives the box the caret, and the colour it made went unwritten.
            Show(true);
            if (!same && Changed != null)
            {
                Changed(colour);
            }
        }

        /// <param name="always">Whether the box is written even while it has the
        /// caret: only once what was typed has been read.</param>
        private void Show(bool always)
        {
            echo = true;
            if (box != null && (always || !box.isFocused))
            {
                box.text = Hues.Hex(colour);
            }
            if (stroke != null)
            {
                Color solid = colour;
                solid.a = 1f;
                stroke.color = solid;
            }
            echo = false;
        }
    }
}
