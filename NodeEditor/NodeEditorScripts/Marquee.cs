using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// Slides a text box's text to its end and back, resting at each end, while it
    /// is longer than the box and nobody is typing. An unfocused `InputField`'s
    /// label holds only the part that fits (notes 04), so a clipped second label is
    /// shown instead.
    /// </summary>
    public class Marquee : MonoBehaviour
    {
        /// <summary>The box this scrolls for.</summary>
        public InputField field;

        /// <summary>How fast the text slides, in the box's own units a second: slow
        /// enough to read while it moves.</summary>
        private const float Speed = 22f;

        /// <summary>How long it rests at either end.</summary>
        private const float Rest = 1.25f;

        // Where in the round it is. Constants rather than an enum: declaring an enum
        // segfaults Besiege's own C# compiler.
        private const int AtStart = 0;
        private const int Going = 1;
        private const int AtEnd = 2;
        private const int Coming = 3;

        private RectTransform window;
        private RectTransform strip;
        private Text rolling;

        /// <summary>What the text was laid out for, so it is measured again only
        /// when one of these changes.</summary>
        private string measured;
        private float measuredWide = -1f;
        private int measuredSize = -1;
        private Font measuredFont;

        /// <summary>How far the whole text runs past the box.</summary>
        private float over;
        private float along;
        private int phase;
        private float until;
        private bool rolls;

        /// <summary>Puts one on a field, or hands back the one already there.
        /// </summary>
        public static Marquee On(InputField field)
        {
            if (field == null)
            {
                return null;
            }
            Marquee roll = field.GetComponent<Marquee>();
            if (roll == null)
            {
                roll = field.gameObject.AddComponent<Marquee>();
            }
            roll.field = field;
            return roll;
        }

        private void OnDisable()
        {
            // A box switched off part-way round starts the round again when it is
            // back, with its own label showing until then.
            Stopped();
        }

        private void LateUpdate()
        {
            Text label = field == null ? null : field.textComponent;
            if (label == null)
            {
                return;
            }
            if (Typing() || !Overflows(label))
            {
                Stopped();
                return;
            }
            if (!rolls)
            {
                Started(label);
            }
            if (rolls)
            {
                Rolled(label);
            }
        }

        /// <summary>Whether the box is being typed in -- or has just been clicked
        /// into and is a frame from saying so.</summary>
        private bool Typing()
        {
            if (field.isFocused)
            {
                return true;
            }
            EventSystem events = EventSystem.current;
            return events != null && events.currentSelectedGameObject == field.gameObject;
        }

        /// <summary>Whether the field shows less than its whole text: asked of the
        /// field's own label, since measuring disagreed at the edges. The distance
        /// to slide is measured when the text, the room or the lettering
        /// change.</summary>
        private bool Overflows(Text label)
        {
            string text = field.text;
            if (string.IsNullOrEmpty(text) || label.font == null || label.text == text)
            {
                return false;
            }
            float wide = label.rectTransform.rect.width;
            if (!object.ReferenceEquals(text, measured)
                || Mathf.Abs(wide - measuredWide) > 0.5f
                || label.fontSize != measuredSize || label.font != measuredFont)
            {
                measured = text;
                measuredWide = wide;
                measuredSize = label.fontSize;
                measuredFont = label.font;
                // Never nothing: the field has said it does not fit.
                over = Mathf.Max(2f, Needs(label, text) - wide);
                // Laid out afresh: a round under way was for the text as it was.
                rolls = false;
            }
            return true;
        }

        /// <summary>The text's width in the label's lettering.</summary>
        private static float Needs(Text label, string text)
        {
            TextGenerationSettings asked = label.GetGenerationSettings(Vector2.zero);
            asked.resizeTextForBestFit = false;
            asked.horizontalOverflow = HorizontalWrapMode.Overflow;
            float much = Mathf.Max(0.0001f, label.pixelsPerUnit);
            return label.cachedTextGeneratorForLayout.GetPreferredWidth(text, asked) / much;
        }

        /// <summary>The whole text put in the field's label's place, at the start
        /// of its round.</summary>
        private void Started(Text label)
        {
            Built();
            if (window == null)
            {
                return;
            }
            // Clipped across only: the field's label is shorter than its letters,
            // and a window its height cut their tops off. The strip is put back to
            // the label's height.
            float room = label.fontSize * 2f;
            RectTransform from = label.rectTransform;
            window.anchorMin = from.anchorMin;
            window.anchorMax = from.anchorMax;
            window.pivot = from.pivot;
            window.offsetMin = from.offsetMin - new Vector2(0f, room);
            window.offsetMax = from.offsetMax + new Vector2(0f, room);

            rolling.font = label.font;
            rolling.fontSize = label.fontSize;
            rolling.fontStyle = label.fontStyle;
            rolling.lineSpacing = label.lineSpacing;
            rolling.color = label.color;
            // From the left whatever the box is set to: text that is being slid
            // along its own length starts at its start.
            rolling.alignment = (TextAnchor)(((int)label.alignment / 3) * 3);
            rolling.text = measured;
            strip.offsetMin = new Vector2(0f, room);
            strip.offsetMax = new Vector2(measuredWide + over + 2f, -room);

            along = 0f;
            phase = AtStart;
            until = Time.unscaledTime + Rest;
            strip.anchoredPosition = Vector2.zero;
            window.gameObject.SetActive(true);
            label.enabled = false;
            rolls = true;
        }

        /// <summary>One frame of the round: rest, slide to the end, rest, slide
        /// back.</summary>
        private void Rolled(Text label)
        {
            if (rolling.color != label.color)
            {
                rolling.color = label.color;
            }
            float now = Time.unscaledTime;
            float step = Speed * Time.unscaledDeltaTime;
            if (phase == AtStart)
            {
                if (now >= until)
                {
                    phase = Going;
                }
            }
            else if (phase == Going)
            {
                along += step;
                if (along >= over)
                {
                    along = over;
                    phase = AtEnd;
                    until = now + Rest;
                }
            }
            else if (phase == AtEnd)
            {
                if (now >= until)
                {
                    phase = Coming;
                }
            }
            else
            {
                along -= step;
                if (along <= 0f)
                {
                    along = 0f;
                    phase = AtStart;
                    until = now + Rest;
                }
            }
            Vector2 at = new Vector2(-along, 0f);
            if (strip.anchoredPosition != at)
            {
                strip.anchoredPosition = at;
            }
        }

        /// <summary>The field's own label back, and this one put away.</summary>
        private void Stopped()
        {
            Text label = field == null ? null : field.textComponent;
            if (label != null && !label.enabled)
            {
                label.enabled = true;
            }
            if (window != null && window.gameObject.activeSelf)
            {
                window.gameObject.SetActive(false);
            }
            rolls = false;
        }

        /// <summary>The clipped window and the label that slides in it, made the
        /// first time a text is too long -- most boxes never need one.</summary>
        private void Built()
        {
            if (window != null || field == null)
            {
                return;
            }
            GameObject box = new GameObject("Marquee");
            box.transform.SetParent(field.transform, false);
            window = box.AddComponent<RectTransform>();
            box.AddComponent<RectMask2D>();

            GameObject words = new GameObject("Rolling");
            words.transform.SetParent(box.transform, false);
            strip = words.AddComponent<RectTransform>();
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = Vector2.zero;
            rolling = words.AddComponent<Text>();
            rolling.raycastTarget = false;
            rolling.supportRichText = false;
            rolling.resizeTextForBestFit = false;
            rolling.horizontalOverflow = HorizontalWrapMode.Overflow;
            rolling.verticalOverflow = VerticalWrapMode.Overflow;
            box.SetActive(false);
        }
    }
}
