using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// The list of variable names a cell offers when it is switched to a variable.
    ///
    /// One list, on a canvas of its own, above every window this mod draws -- the
    /// same arrangement as <see cref="Tip"/> and for the same reason: the cell that
    /// opens it lives inside a scrolling view that clips its rows, and a list
    /// parented into a row would be cut off at the edge of the frame and drawn
    /// under whichever row came after it. Its own canvas rather than whichever
    /// window opened it, because two of those windows are up at once and a list
    /// drawn on the lower one disappears behind the higher.
    ///
    /// A variable is only a name typed into a key, so the box is still there to
    /// type into. This is the list of names already in use on the machine, which
    /// is what somebody wiring one block to another actually wants: the name is
    /// already decided and only has to be spelled the same.
    /// </summary>
    public static class Choices
    {
        /// <summary>How many names are shown before the list starts scrolling.</summary>
        private const int Visible = 8;

        private const float RowHeight = 22f;
        private const float Pad = 3f;
        private const float MinWidth = 120f;
        private const float Gap = 2f;

        /// <summary>The scrollbar's width, and how wide its handle is drawn inside
        /// it. Built by hand rather than borrowed: this list is not a UI Factory
        /// window, so there is no prefab bar to reuse.</summary>
        private const float BarWidth = 8f;

        /// <summary>The track behind the handle: the plate, a shade lighter.</summary>
        private static readonly Color RailInk = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color GripInk = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>Over the board editor and the docked table, under the
        /// tooltip -- a list is a thing to point at and a tip explains what is
        /// under the pointer.</summary>
        private const int CanvasOrder = 2800;

        private static RectTransform canvas;
        private static GameObject sheet;      // the catcher behind the list
        private static GameObject list;
        private static Action<string> chosen;

        /// <summary>The list's own canvas, built once and kept.</summary>
        private static bool Homed()
        {
            if (canvas != null)
            {
                return true;
            }
            try
            {
                GameObject host = new GameObject("TimerPlusChoices");
                UnityEngine.Object.DontDestroyOnLoad(host);
                Canvas drawn = host.AddComponent<Canvas>();
                drawn.renderMode = RenderMode.ScreenSpaceOverlay;
                drawn.sortingOrder = CanvasOrder;
                CanvasScaler scaler = host.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
                host.AddComponent<GraphicRaycaster>();
                canvas = host.GetComponent<RectTransform>();
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("the variable list has nowhere to draw: " + e.Message);
                return false;
            }
        }

        public static bool Open(RectTransform under, List<string> names,
                                Action<string> pick)
        {
            Close();
            if (!Homed() || under == null || names == null || names.Count == 0
                || !UIF.Available)
            {
                return false;
            }
            try
            {
                chosen = pick;
                Build(under, names, Vector2.zero, false);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("could not offer the variable list: " + e.Message);
                Close();
                return false;
            }
        }

        /// <summary>
        /// Whether the pointer is over the open list.
        ///
        /// Asked by the panel, which switches its raycaster off for a press that
        /// did not land on its window -- and this list is the one thing of the
        /// panel's that hangs outside it.
        /// </summary>
        public static bool Over(Vector2 screen)
        {
            if (list == null)
            {
                return false;
            }
            RectTransform frame = list.transform as RectTransform;
            // Null camera: the canvas is a screen-space overlay.
            return frame != null
                && RectTransformUtility.RectangleContainsScreenPoint(frame, screen, null);
        }

        public static void Close()
        {
            chosen = null;
            if (list != null)
            {
                UnityEngine.Object.Destroy(list);
            }
            if (sheet != null)
            {
                UnityEngine.Object.Destroy(sheet);
            }
            list = null;
            sheet = null;
        }

        /// <summary>The same list, opened at a point rather than under a control:
        /// a menu asked for by a right-click belongs where the click was.</summary>
        public static bool OpenAt(Vector2 screen, List<string> names,
                                  Action<string> pick)
        {
            Close();
            if (!Homed() || names == null || names.Count == 0 || !UIF.Available)
            {
                return false;
            }
            try
            {
                chosen = pick;
                Build(null, names, screen, true);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("could not offer the list: " + e.Message);
                Close();
                return false;
            }
        }

        private static void Build(RectTransform under, List<string> names,
                                  Vector2 screen, bool atPoint)
        {
            // Anything clicked outside the list closes it. A sheet over the whole
            // canvas, under the list itself: uGUI raycasts the last sibling first,
            // so the list is still clickable and everything else is not.
            sheet = new GameObject("ChoicesSheet");
            sheet.transform.SetParent(canvas, false);
            RectTransform behind = sheet.AddComponent<RectTransform>();
            behind.anchorMin = Vector2.zero;
            behind.anchorMax = Vector2.one;
            behind.offsetMin = Vector2.zero;
            behind.offsetMax = Vector2.zero;
            Image blank = sheet.AddComponent<Image>();
            blank.color = new Color(0f, 0f, 0f, 0f);
            Shut shut = sheet.AddComponent<Shut>();
            shut.enabled = true;
            // A list is over everything while it is open, so it is over whatever
            // was holding the game's camera off -- and the wheel went back to
            // zooming the machine the moment one was opened. It holds the camera
            // itself instead, for as long as it is up.
            ZoomGuard guard = sheet.AddComponent<ZoomGuard>();
            guard.menu = true;

            list = new GameObject("Choices");
            list.transform.SetParent(canvas, false);
            RectTransform frame = list.AddComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);

            Image plate = list.AddComponent<Image>();
            plate.color = UIF.Shade;

            int shown = Mathf.Min(names.Count, Visible);
            bool scrolls = names.Count > shown;
            // The bar is added to the width rather than taken out of it, so a list
            // that scrolls shows its names at the same width as one that does not.
            float wide = Mathf.Max(MinWidth, atPoint ? MinWidth : under.rect.width)
                       + (scrolls ? BarWidth + Pad : 0f);
            float tall = shown * RowHeight + Pad * 2f;
            frame.sizeDelta = new Vector2(wide, tall);

            GameObject port = new GameObject("Viewport");
            port.transform.SetParent(list.transform, false);
            RectTransform view = port.AddComponent<RectTransform>();
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = new Vector2(Pad, Pad);
            view.offsetMax = new Vector2(-(Pad + (scrolls ? BarWidth + Pad : 0f)), -Pad);
            // RectMask2D and not Mask: a stencil mask on the same canvas as the
            // window's own stencil mask cuts holes in it -- the two are at the same
            // depth. This one clips by rectangle and nothing else notices.
            port.AddComponent<RectMask2D>();

            GameObject inside = new GameObject("Names");
            inside.transform.SetParent(port.transform, false);
            RectTransform content = inside.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, names.Count * RowHeight);

            for (int i = 0; i < names.Count; i++)
            {
                Row(content, names[i], i);
            }

            if (scrolls)
            {
                ScrollRect scroll = list.AddComponent<ScrollRect>();
                scroll.viewport = view;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = RowHeight;
                scroll.verticalScrollbar = Rail(frame);
                // Permanent, and the viewport already holds its own edge clear of
                // the bar: the modes that hide the bar also resize the viewport,
                // and this viewport is placed by anchors rather than by the
                // ScrollRect.
                scroll.verticalScrollbarVisibility =
                    ScrollRect.ScrollbarVisibility.Permanent;
            }
            // The wheel over the list scrolls it and zooms the camera at the same
            // time until Besiege is told otherwise -- and the list is over the
            // sheet, so the sheet's own hold is let go of the moment the pointer
            // reaches the names.
            ZoomGuard held = list.AddComponent<ZoomGuard>();
            held.menu = true;

            if (atPoint)
            {
                Point(frame, screen, tall);
            }
            else
            {
                Place(frame, under, tall);
            }
        }

        /// <summary>
        /// The scrollbar down the right of the list: track, sliding area, handle,
        /// which is the hierarchy a <c>Scrollbar</c> drives -- it writes the
        /// handle's anchors, so the handle needs a parent stretched across the
        /// track for those anchors to mean anything.
        /// </summary>
        private static UnityEngine.UI.Scrollbar Rail(RectTransform frame)
        {
            GameObject track = new GameObject("Rail");
            track.transform.SetParent(frame, false);
            RectTransform rail = track.AddComponent<RectTransform>();
            rail.anchorMin = new Vector2(1f, 0f);
            rail.anchorMax = new Vector2(1f, 1f);
            rail.pivot = new Vector2(1f, 0.5f);
            rail.sizeDelta = new Vector2(BarWidth, -Pad * 2f);
            rail.anchoredPosition = new Vector2(-Pad, 0f);
            Image back = track.AddComponent<Image>();
            back.color = RailInk;

            GameObject area = new GameObject("Sliding Area");
            area.transform.SetParent(track.transform, false);
            RectTransform slide = area.AddComponent<RectTransform>();
            slide.anchorMin = Vector2.zero;
            slide.anchorMax = Vector2.one;
            slide.offsetMin = Vector2.zero;
            slide.offsetMax = Vector2.zero;

            GameObject held = new GameObject("Handle");
            held.transform.SetParent(area.transform, false);
            RectTransform grip = held.AddComponent<RectTransform>();
            grip.offsetMin = Vector2.zero;
            grip.offsetMax = Vector2.zero;
            Image face = held.AddComponent<Image>();
            face.color = GripInk;

            // Fully qualified: Besiege has a `Scrollbar` of its own in the global
            // namespace, and this file is compiled inside `TimerPlusMod` where the
            // unqualified name finds that one. The same collision cost `Keys` and
            // `Convert` their names -- see AGENTS.md.
            UnityEngine.UI.Scrollbar bar =
                track.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.handleRect = grip;
            bar.targetGraphic = face;
            return bar;
        }

        private static void Row(RectTransform content, string name, int index)
        {
            GameObject go = UIF.Spawn(UIF.ButtonPrefab, content);
            if (go == null)
            {
                return;
            }
            // Stretched across the list, so a name is the width of the frame
            // whatever the frame ended up being.
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(0f, -(index + 1) * RowHeight);
            rect.offsetMax = new Vector2(0f, -index * RowHeight);
            UIF.NoSwell(go);

            Text label = go.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                RectTransform words = label.rectTransform;
                words.anchorMin = Vector2.zero;
                words.anchorMax = Vector2.one;
                words.offsetMin = new Vector2(Pad * 2f, 0f);
                words.offsetMax = new Vector2(-Pad * 2f, 0f);
                label.raycastTarget = false;
                UIF.Style(label, UIF.Ink, TextAnchor.MiddleLeft);
                UIF.Shrink(label, 8);
                label.text = name;
            }

            string picked = name;
            Button click = go.GetComponent<Button>();
            if (click != null)
            {
                click.onClick.AddListener(delegate { Take(picked); });
            }
        }

        private static void Take(string name)
        {
            Action<string> answer = chosen;
            Close();
            if (answer != null)
            {
                answer(name);
            }
        }

        /// <summary>
        /// Under the cell that opened it, or over it where there is no room below.
        /// Through screen space, because the cell is several parents deep inside a
        /// scrolling view and the canvas is not an ancestor worth walking to.
        /// </summary>
        /// <summary>At the pointer, with its top-left corner on it, kept on
        /// screen.</summary>
        private static void Point(RectTransform frame, Vector2 screen, float tall)
        {
            Vector2 local;
            if (!Local(screen, out local))
            {
                return;
            }
            Vector2 room = canvas.rect.size;
            float wide = frame.sizeDelta.x;
            float x = Mathf.Clamp(local.x + wide * 0.5f,
                                  -room.x * 0.5f + wide * 0.5f,
                                  room.x * 0.5f - wide * 0.5f);
            float y = Mathf.Clamp(local.y - tall * 0.5f,
                                  -room.y * 0.5f + tall * 0.5f,
                                  room.y * 0.5f - tall * 0.5f);
            frame.anchoredPosition = new Vector2(x, y);
        }

        private static void Place(RectTransform frame, RectTransform under, float tall)
        {
            Vector3[] corners = new Vector3[4];
            under.GetWorldCorners(corners);
            Vector2 low, high;
            if (!Local(corners[0], out low) || !Local(corners[2], out high))
            {
                return;
            }
            Vector2 room = canvas.rect.size;
            float middle = (low.x + high.x) * 0.5f;
            float bottom = Mathf.Min(low.y, high.y);
            float top = Mathf.Max(low.y, high.y);

            bool below = bottom - Gap - tall > -room.y * 0.5f;
            float y = below ? bottom - Gap - tall * 0.5f : top + Gap + tall * 0.5f;
            float wide = frame.sizeDelta.x;
            float x = Mathf.Clamp(middle, -room.x * 0.5f + wide * 0.5f,
                                           room.x * 0.5f - wide * 0.5f);
            y = Mathf.Clamp(y, -room.y * 0.5f + tall * 0.5f, room.y * 0.5f - tall * 0.5f);
            frame.anchoredPosition = new Vector2(x, y);
        }

        private static bool Local(Vector3 world, out Vector2 local)
        {
            return Local(RectTransformUtility.WorldToScreenPoint(null, world),
                         out local);
        }

        private static bool Local(Vector2 screen, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas, screen, null, out local);
        }

        /// <summary>Closes the list on a click anywhere else. Its own component so
        /// the sheet has something to answer the pointer with.</summary>
        public class Shut : MonoBehaviour, IPointerClickHandler
        {
            public void OnPointerClick(PointerEventData pointer) { Close(); }
        }

        /// <summary>Opens the list. Goes on a control that already answers the
        /// pointer for its own reasons -- an InputField does -- because uGUI hands
        /// an event to every handler on the object, not only the first.</summary>
        public class Opener : MonoBehaviour, IPointerClickHandler
        {
            public Action Clicked;

            public void OnPointerClick(PointerEventData pointer)
            {
                if (Clicked != null)
                {
                    Clicked();
                }
            }
        }
    }
}
