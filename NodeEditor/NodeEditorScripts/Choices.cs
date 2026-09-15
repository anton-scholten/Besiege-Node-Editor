using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// The list of variable names a cell offers: the names already on the machine;
    /// a new one can still be typed. One list, on its own canvas above every window
    /// of this mod, since a list inside a scrolling row would be clipped and drawn
    /// under later rows.
    /// </summary>
    public static class Choices
    {
        /// <summary>How many names are shown before the list starts scrolling.</summary>
        private const int Visible = 8;

        private const float RowHeight = 22f;
        private const float Pad = 3f;
        private const float MinWidth = 120f;
        private const float Gap = 2f;

        /// <summary>The scrollbar's width and its handle's. Built by hand: this
        /// list is no UI Factory window.</summary>
        private const float BarWidth = 8f;

        /// <summary>The track behind the handle: the plate, a shade lighter.</summary>
        private static readonly Color RailInk = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color GripInk = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>Above the board and the table, below the tooltip.</summary>
        private const int CanvasOrder = 2800;

        private static RectTransform canvas;
        private static GameObject sheet;      // the catcher behind the list
        private static GameObject list;
        private static Action<string> chosen;

        /// <summary>Builds the canvas before anything needs it, so the first list
        /// opened is placed against a canvas Unity has already laid out.</summary>
        public static void Ready()
        {
            Homed();
        }

        /// <summary>The list's own canvas, built once and kept.</summary>
        private static bool Homed()
        {
            if (canvas != null)
            {
                return true;
            }
            try
            {
                GameObject host = new GameObject("NodeEditorChoices");
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
                // A canvas made this frame has no size until it is laid out; placed
                // against it, the first list of a session landed nowhere near its
                // cell.
                Canvas.ForceUpdateCanvases();
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

        /// <summary>Whether the pointer is over the open list. The panel asks: the
        /// list hangs outside the panel's window.</summary>
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
            // A sheet over the canvas, under the list, closes it on any click
            // outside.
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
            // Holds the camera's zoom off while the list is up, as it covers
            // whatever held it.
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
            // RectMask2D, not Mask: a stencil mask at the same depth as the
            // window's own cuts holes in it.
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
                // Permanent: the auto-hiding modes resize the viewport, which
                // anchors place here.
                scroll.verticalScrollbarVisibility =
                    ScrollRect.ScrollbarVisibility.Permanent;
            }
            // The wheel over the names scrolls the list; this stops it zooming the
            // camera too.
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

        /// <summary>The scrollbar: track, sliding area and handle, the hierarchy a
        /// <c>Scrollbar</c> drives by writing the handle's anchors.</summary>
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

            // Fully qualified: Besiege's global `Scrollbar` would be found first.
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

            // A highlight under the pointer, as a plate of its own over the
            // button's skin.
            GameObject lit = new GameObject("Lit");
            lit.transform.SetParent(go.transform, false);
            RectTransform glow = lit.AddComponent<RectTransform>();
            glow.anchorMin = Vector2.zero;
            glow.anchorMax = Vector2.one;
            glow.offsetMin = Vector2.zero;
            glow.offsetMax = Vector2.zero;
            Image wash = lit.AddComponent<Image>();
            wash.color = new Color(1f, 1f, 1f, 0.16f);
            wash.raycastTarget = false;
            lit.transform.SetAsFirstSibling();
            lit.SetActive(false);
            GameObject shown = lit;
            Hover watch = go.AddComponent<Hover>();
            watch.Over = delegate(int who, bool on)
            {
                if (shown != null)
                {
                    shown.SetActive(on);
                }
            };

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

        /// <summary>Under the cell that opened it, or above without room below,
        /// placed through screen space.</summary>
        private static void Point(RectTransform frame, Vector2 screen, float tall)
        {
            Vector2 local;
            if (!Local(screen, out local))
            {
                return;
            }
            Vector2 room = Room();
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
            Vector2 room = Room();
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

        /// <summary>The room a list has: the canvas's, or the screen's while a new
        /// canvas reports nothing.</summary>
        private static Vector2 Room()
        {
            Vector2 room = canvas == null ? Vector2.zero : canvas.rect.size;
            if (room.x > 1f && room.y > 1f)
            {
                return room;
            }
            Canvas drawn = canvas == null ? null : canvas.GetComponent<Canvas>();
            float much = drawn == null || drawn.scaleFactor <= 0f
                ? 1f : drawn.scaleFactor;
            return new Vector2(Screen.width / much, Screen.height / much);
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

        /// <summary>Opens the list from a control that handles the pointer already
        /// (an InputField): uGUI hands an event to every handler on an
        /// object.</summary>
        public class Opener : MonoBehaviour, IPointerClickHandler
        {
            public Action Clicked;

            public void OnPointerClick(PointerEventData pointer)
            {
                // Not the click ending a drag, or a node dragged by its name box
                // would open a list.
                if (Clicked != null && !pointer.dragging)
                {
                    Clicked();
                }
            }
        }
    }
}
