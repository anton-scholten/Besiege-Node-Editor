using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// A control's tooltip, in UI Factory's tooltip artwork. One panel on its own
    /// canvas, moved to whatever is hovered: a panel inside a scrolling table row
    /// would be clipped with the row and drawn under the rows after it.
    /// </summary>
    public class Tip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>No gap: the point touches the control it explains.</summary>
        private const float Gap = 0f;

        /// <summary>Below the control, unless it is this near the canvas
        /// bottom.</summary>
        private const float BottomRoom = 90f;

        private string text;

        /// <summary>What this control says; rewritten live if the tip is
        /// showing.</summary>
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

        /// <summary>Hangs a tip on a control. Null or empty text removes
        /// it.</summary>
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
            /// <summary>The tooltip's own canvas, sorted above every window of this
            /// mod.
            /// </summary>
            private static Canvas own;

            private static RectTransform canvas;
            private static RectTransform panel;
            private static Text label;
            private static RectTransform triangle;
            private static LayoutElement cap;
            private static Glide glide;

            /// <summary>How far the panel drifts as it fades up, towards the
            /// control it explains.</summary>
            private const float Drift = 7f;

            // From the sibling Clippy mod: sixteen point, capitals, padded,
            // wrapping.
            private const int FontSize = 15;

            /// <summary>Air either side of the words. Tight: a tooltip is read at a
            /// glance and a wide one covers what it explains.</summary>
            private const float Padding = 8f;
            private const float MaxWidth = 340f;

            /// <summary>Kept for callers; tips draw on their own canvas (<see
            /// cref="own"/>).
            /// </summary>
            public static void Home(RectTransform on)
            {
                Mine();
                if (canvas != null)
                {
                    return;
                }
                if (canvas == on)
                {
                    return;
                }
                canvas = on;
                // Home changed: take the old panel down, or it stays on screen for
                // good.
                if (panel != null)
                {
                    panel.gameObject.SetActive(false);
                    Destroy(panel.gameObject);
                }
                showing = null;
                panel = null;
                label = null;
                triangle = null;
                cap = null;
                glide = null;
            }

            /// <summary>Over everything else of ours: the panel is 2400 and the
            /// board editor 2500, and uGUI's own Dropdown canvas is
            /// 30000.</summary>
            private const int Order = 2900;

            private static void Mine()
            {
                if (own != null)
                {
                    return;
                }
                GameObject go = new GameObject("TipCanvas");
                UnityEngine.Object.DontDestroyOnLoad(go);
                own = go.AddComponent<Canvas>();
                own.renderMode = RenderMode.ScreenSpaceOverlay;
                own.sortingOrder = Order;
                CanvasScaler scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
                // Nothing on it answers the pointer, so it needs no raycaster.
                canvas = go.GetComponent<RectTransform>();
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

                // Capitals, as Besiege writes its interface.
                label.text = words.ToUpperInvariant();
                if (cap != null)
                {
                    // preferredWidth is the unwrapped longest line: caps long tips,
                    // leaves short ones.
                    cap.preferredWidth =
                        Mathf.Min(label.preferredWidth, MaxWidth - Padding * 2f);
                }
                // Last sibling on every showing, so a window rebuilt since is not
                // drawn over it.
                panel.SetAsLastSibling();
                panel.gameObject.SetActive(true);
                // The prefab sizes itself with a ContentSizeFitter, and the size is
                // needed now.
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

                Vector2 middle;
                float half;
                if (!Frame(over, out middle, out half))
                {
                    Hide();
                    return;
                }

                Vector2 room = canvas.rect.size;
                float top = middle.y + half;
                float bottom = middle.y - half;

                // Measured off the whole panel: the bubble and point overhang the
                // root's rect, and placing the root put the bubble over the
                // control.
                Bounds box = Extent();
                bool above = bottom - Gap - box.size.y < -room.y * 0.5f
                          && middle.y < room.y * 0.5f - BottomRoom;
                // The point moves from one edge of the panel to the other, so the
                // measurement above is only right for the side it ends up on.
                Point(above);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
                box = Extent();

                // The edge of the panel that faces the control, put on the edge of
                // the control that faces the panel.
                float y = above
                    ? top - Gap - (box.center.y - box.extents.y)
                    : bottom + Gap - (box.center.y + box.extents.y);

                // Kept on screen. The point stays over the control either way, so a
                // clamped panel still says which control it belongs to.
                float x = Mathf.Clamp(middle.x,
                    -room.x * 0.5f + box.extents.x, room.x * 0.5f - box.extents.x);
                y = Mathf.Clamp(y, -room.y * 0.5f + box.extents.y,
                                    room.y * 0.5f - box.extents.y);
                glide.Settle(new Vector2(x, y),
                             new Vector2(0f, above ? -Drift : Drift));
                Aim(middle.x - x);
            }

            /// <summary>What the panel covers, bubble and point included, in its
            /// own coordinates.</summary>
            private static Bounds Extent()
            {
                return RectTransformUtility.CalculateRelativeRectTransformBounds(
                    panel, panel);
            }

            /// <summary>A control's middle in canvas coordinates and half its
            /// height, through screen space (overlay canvas, null
            /// camera).</summary>
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
                // Anchored to the panel's bottom and flipped when above, to its top
                // when below: what Besiege.UI.Bridge.Tooltip.OnValidate does.
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

            /// <summary>Padding either side of the words, through the layout group:
            /// the prefab's ContentSizeFitter discards any width written to
            /// it.</summary>
            private static void Breathe(GameObject made)
            {
                VerticalLayoutGroup group = made.GetComponent<VerticalLayoutGroup>();
                if (group == null)
                {
                    return;
                }
                RectOffset pad = group.padding;
                int side = Mathf.RoundToInt(Padding);
                group.padding = new RectOffset(
                    Mathf.Max(pad.left, side), Mathf.Max(pad.right, side),
                    Mathf.Max(pad.top, side / 3), Mathf.Max(pad.bottom, side / 3));
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
                    label.fontSize = FontSize;
                    // A line break for an aside; wrapping for anything too long.
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    // A LayoutElement is what a ContentSizeFitter defers to: it
                    // holds long tips to a readable width.
                    cap = label.gameObject.GetComponent<LayoutElement>();
                    if (cap == null)
                    {
                        cap = label.gameObject.AddComponent<LayoutElement>();
                    }
                }
                Breathe(made);

                // Fades it up rather than switching it on, and switches the object
                // off again once it has faded away.
                glide = made.AddComponent<Glide>();

                Transform found = Attach.Find(made.transform, "Triangle");
                triangle = found == null ? null : found.GetComponent<RectTransform>();

                // The panel is over the whole window, so nothing it covers should
                // stop answering the pointer -- including the control that opened
                // it.
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
