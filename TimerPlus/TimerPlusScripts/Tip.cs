using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// A word or two explaining a control, in Besiege's own tooltip panel, shown
    /// while the pointer is over it.
    ///
    /// One panel, on the canvas, moved to whatever is hovered -- rather than a
    /// panel per control, which is how UI Factory's own
    /// <c>Besiege.UI.Bridge.Tooltip</c> is arranged. Two things about this panel
    /// make that the wrong shape here:
    ///
    /// * the table **scrolls**, and its rows are shown and hidden a whole row at a
    ///   time as they leave the frame, so a tooltip parented into a row would be
    ///   clipped with it and would have to be positioned again every frame;
    /// * uGUI draws siblings in order, so a tooltip belonging to one row and
    ///   drawn over the row above it is at the mercy of which row it lives in.
    ///
    /// On the canvas, above everything, neither question arises. The artwork is
    /// still UI Factory's -- the same prefab the game's own tooltips are drawn
    /// with -- so it looks like the rest of the interface.
    /// </summary>
    public class Tip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Air between the control and the point of the tooltip.</summary>
        private const float Gap = 4f;

        /// <summary>How near the top of the canvas a control has to be before the
        /// tooltip goes below it instead.</summary>
        private const float TopRoom = 90f;

        private string text;

        /// <summary>What this control says. Assigning it while the tooltip is up
        /// rewrites it, which is what a control whose meaning changes -- the
        /// key/variable button -- wants.</summary>
        public string Text
        {
            get { return text; }
            set
            {
                text = value;
                if (showing == this)
                {
                    Tips.Show(transform as RectTransform, text);
                }
            }
        }

        private static Tip showing;

        /// <summary>Hangs a tip on a control. Null or empty text removes it.</summary>
        public static void On(GameObject control, string words)
        {
            if (control == null)
            {
                return;
            }
            Tip tip = control.GetComponent<Tip>();
            if (string.IsNullOrEmpty(words))
            {
                if (tip != null)
                {
                    Destroy(tip);
                }
                return;
            }
            if (tip == null)
            {
                tip = control.AddComponent<Tip>();
            }
            tip.Text = words;
        }

        public void OnPointerEnter(PointerEventData pointer)
        {
            showing = this;
            Tips.Show(transform as RectTransform, text);
        }

        public void OnPointerExit(PointerEventData pointer)
        {
            Hide();
        }

        // A row scrolled out of the frame is switched off under the pointer, and no
        // exit arrives for it.
        private void OnDisable() { Hide(); }

        private void OnDestroy() { Hide(); }

        private void Hide()
        {
            if (showing != this)
            {
                return;
            }
            showing = null;
            Tips.Hide();
        }

        /// <summary>Where the tooltip is drawn, and the arithmetic that puts it
        /// over the right control.</summary>
        public static class Tips
        {
            private static RectTransform canvas;
            private static RectTransform panel;
            private static Text label;
            private static RectTransform triangle;
            private static Glide glide;

            /// <summary>How far the panel drifts as it fades up, towards the
            /// control it explains.</summary>
            private const float Drift = 7f;

            /// <summary>The canvas every tooltip is drawn on. Set once, by the
            /// panel that builds it.</summary>
            public static void Home(RectTransform on)
            {
                if (canvas == on)
                {
                    return;
                }
                canvas = on;
                // Its home changed, so whatever was built for the old one is gone
                // with it.
                panel = null;
                label = null;
                triangle = null;
                glide = null;
            }

            public static void Hide()
            {
                if (glide != null)
                {
                    glide.Out();
                }
                else if (panel != null)
                {
                    panel.gameObject.SetActive(false);
                }
            }

            public static void Show(RectTransform over, string words)
            {
                if (canvas == null || over == null || string.IsNullOrEmpty(words)
                    || !UIF.TooltipsWanted)
                {
                    Hide();
                    return;
                }
                if (!Build())
                {
                    return;
                }

                label.text = words;
                // uGUI draws siblings in order, and the window is spawned fresh
                // every time the table is rebuilt -- which puts it after a tooltip
                // built before it, and so over the top of one. Last again on every
                // showing, and it is in front of whatever is there now.
                panel.SetAsLastSibling();
                panel.gameObject.SetActive(true);
                // The prefab sizes itself to its words through a ContentSizeFitter,
                // and the size is wanted now rather than at the next layout pass --
                // the placement below is measured against it.
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

                Vector2 middle;
                float half;
                if (!Frame(over, out middle, out half))
                {
                    Hide();
                    return;
                }

                Vector2 room = canvas.rect.size;
                float wide = panel.rect.width;
                float tall = panel.rect.height;

                // Above the control, unless it is too near the top of the screen for
                // the panel to fit, in which case below it and the point turned over.
                bool above = middle.y + half + Gap + tall < room.y * 0.5f
                          || middle.y > -room.y * 0.5f + TopRoom;
                float y = above ? middle.y + half + Gap + tall * 0.5f
                                : middle.y - half - Gap - tall * 0.5f;
                Point(above);

                // Kept on screen. The point stays over the control either way, so a
                // clamped panel still says which control it belongs to.
                float x = Mathf.Clamp(middle.x, -room.x * 0.5f + wide * 0.5f,
                                                 room.x * 0.5f - wide * 0.5f);
                y = Mathf.Clamp(y, -room.y * 0.5f + tall * 0.5f,
                                    room.y * 0.5f - tall * 0.5f);
                glide.Settle(new Vector2(x, y),
                             new Vector2(0f, above ? -Drift : Drift));
                Aim(middle.x - x);
            }

            /// <summary>
            /// Where a control sits in canvas coordinates, and half its height.
            ///
            /// Through screen space rather than by walking the transforms: the
            /// control is several parents deep inside a scrolling view and the
            /// canvas is not its ancestor's ancestor in any way worth relying on.
            /// The camera is null throughout because the canvas is an overlay.
            /// </summary>
            private static bool Frame(RectTransform over, out Vector2 middle, out float half)
            {
                middle = Vector2.zero;
                half = 0f;
                Vector3[] corners = new Vector3[4];
                over.GetWorldCorners(corners);

                Vector2 low, high;
                if (!Local(corners[0], out low) || !Local(corners[2], out high))
                {
                    return false;
                }
                middle = (low + high) * 0.5f;
                half = Mathf.Abs(high.y - low.y) * 0.5f;
                return true;
            }

            private static bool Local(Vector3 world, out Vector2 local)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas, screen, null, out local);
            }

            /// <summary>Puts the point on the edge facing the control.</summary>
            private static void Point(bool above)
            {
                if (triangle == null)
                {
                    return;
                }
                // Anchored to the bottom of the panel and turned over when the panel
                // is above what it explains, to the top and upright when below. The
                // same two assignments Besiege.UI.Bridge.Tooltip's own OnValidate
                // makes; done here because this panel is not driven by that handler.
                Vector2 edge = above ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
                triangle.anchorMin = edge;
                triangle.anchorMax = edge;
                triangle.localRotation = Quaternion.Euler(0f, 0f, above ? 180f : 0f);
            }

            /// <summary>Slides the point back over the control when the panel had
            /// to be pulled away from the screen edge.</summary>
            private static void Aim(float offset)
            {
                if (triangle == null)
                {
                    return;
                }
                float reach = Mathf.Max(0f, panel.rect.width * 0.5f - triangle.rect.width);
                triangle.anchoredPosition =
                    new Vector2(Mathf.Clamp(offset, -reach, reach), 0f);
            }

            private static bool Build()
            {
                if (panel != null)
                {
                    return true;
                }
                GameObject made = UIF.Spawn(UIF.TooltipPrefab, canvas);
                if (made == null)
                {
                    return false;
                }
                made.name = "Tip";
                panel = made.GetComponent<RectTransform>();
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);

                label = made.GetComponentInChildren<Text>(true);
                UIF.Style(label, UIF.Ink, TextAnchor.MiddleCenter);
                if (label != null)
                {
                    // The prefab's own wording, and the component that would put it
                    // back at the next language change, are both in the way.
                    label.text = "";
                }

                // Fades it up rather than switching it on, and switches the object
                // off again once it has faded away.
                glide = made.AddComponent<Glide>();

                Transform found = Attach.Find(made.transform, "Triangle");
                triangle = found == null ? null : found.GetComponent<RectTransform>();

                // The panel is over the whole window, so nothing it covers should
                // stop answering the pointer -- including the control that opened it.
                Graphic[] parts = made.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i].raycastTarget = false;
                }

                made.SetActive(false);
                return true;
            }
        }
    }
}
