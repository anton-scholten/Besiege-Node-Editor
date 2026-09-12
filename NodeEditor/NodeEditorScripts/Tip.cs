using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
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
        /// <summary>Air between the control and the point of the tooltip. None:
        /// the point rests against the edge of what it explains, which is what
        /// says which control that is -- the same as the sibling Clippy mod.</summary>
        private const float Gap = 0f;

        /// <summary>How near the bottom of the canvas a control has to be before
        /// the tooltip goes above it instead. Below is the side it is drawn on
        /// where there is room, which is Besiege's own habit.</summary>
        private const float BottomRoom = 90f;

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
            /// <summary>
            /// The tooltip's own canvas, above every window this mod draws.
            ///
            /// It used to live on whichever window claimed it last, which put it
            /// under any window drawn over that one -- a tip from the block panel
            /// appearing behind the board editor. A canvas of its own, sorted above
            /// both, cannot be behind anything of ours.
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

            // The tooltip's own shape, taken from the sibling Clippy mod so the two
            // read as the same interface: sixteen point, capitals, air either side,
            // and a line that wraps rather than a panel that runs off the screen.
            private const int FontSize = 15;

            /// <summary>Air either side of the words. Tight: a tooltip is read at a
            /// glance and a wide one covers what it explains.</summary>
            private const float Padding = 8f;
            private const float MaxWidth = 340f;

            /// <summary>The canvas every tooltip is drawn on. Set once, by the
            /// panel that builds it.</summary>
            /// <summary>
            /// The canvas tooltips are drawn on. Made here rather than borrowed:
            /// see <see cref="own"/>. The argument is kept for the caller that
            /// wants its own home, and ignored while ours exists.
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
                // Its home changed, so the panel built on the old one is taken
                // down rather than dropped: the old canvas is still there, and a
                // tooltip left showing on it stays on screen for good.
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
            /// board editor 2500, and uGUI's own Dropdown canvas is 30000.</summary>
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

                // Besiege writes its interface in capitals, and matching that is
                // most of what makes a panel read as the game's own -- the sibling
                // Clippy mod puts every tip through the same call.
                label.text = words.ToUpperInvariant();
                if (cap != null)
                {
                    // preferredWidth is the width of the longest line laid out
                    // without wrapping, so this holds a long tip to the measure and
                    // leaves a short one alone.
                    cap.preferredWidth =
                        Mathf.Min(label.preferredWidth, MaxWidth - Padding * 2f);
                }
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
                float top = middle.y + half;
                float bottom = middle.y - half;

                // Measured off the whole panel rather than its own rect. The
                // prefab's bubble is a child stretched past the root -- twenty
                // units each side and nine above and below -- and the point is a
                // child of that, hung outside it again. Placing the root's edge
                // against the control therefore puts the bubble over the control
                // by whatever those two overhang, which is what had a heading's
                // tooltip covering the heading.
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

            /// <summary>What the panel covers, point and bubble included, in its
            /// own coordinates -- so the middle of it is not the middle of the rect
            /// its position is written in.</summary>
            private static Bounds Extent()
            {
                return RectTransformUtility.CalculateRelativeRectTransformBounds(
                    panel, panel);
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

            /// <summary>
            /// Air either side of the words.
            ///
            /// Through the layout group's padding, because the prefab carries a
            /// <c>VerticalLayoutGroup</c> and a <c>ContentSizeFitter</c> and sizes
            /// itself to its contents -- a width written onto it is discarded at
            /// the next layout pass, silently. Padding is the only instruction a
            /// self-sizing panel takes.
            /// </summary>
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
                    // Two ways to a second line: a break written into the tip, for
                    // an aside that reads better under what it qualifies, and
                    // wrapping, for anything too long for one line at all.
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    // The label sizes itself through a ContentSizeFitter of its
                    // own, so a width written onto its rect is discarded; a
                    // LayoutElement is the one thing a fitter defers to, and it is
                    // what holds a long tip to a readable measure.
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
