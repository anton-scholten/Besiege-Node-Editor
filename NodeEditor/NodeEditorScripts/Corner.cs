using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>
    /// A corner of the editor's window, dragged to resize it, and the pointer that
    /// says so while it is over one.
    ///
    /// The cursor is put back on exit rather than left: Unity's cursor is one
    /// global thing, and a mod that takes it and does not give it back leaves the
    /// game wearing a resize arrow over the machine.
    /// </summary>
    public class Corner : MonoBehaviour, IBeginDragHandler, IDragHandler,
                          IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Which way it pulls: -1 or +1 on each axis, in canvas units
        /// where y counts up.</summary>
        public Vector2 pull = Vector2.one;

        /// <summary>Dragged by this much, in canvas units.</summary>
        public Action<Vector2, Vector2> Pulled;

        /// <summary>The drag ended, which is when the graph's window size is worth
        /// writing down.</summary>
        public Action Dropped;

        private Vector2 last;
        private bool holding;
        private static bool wearing;

        public void OnPointerEnter(PointerEventData pointer) { Wear(true, pull); }

        public void OnPointerExit(PointerEventData pointer)
        {
            if (!holding)
            {
                Wear(false, pull);
            }
        }

        private void OnDisable() { Wear(false, pull); }

        private static void Wear(bool on, Vector2 pull)
        {
            if (wearing == on)
            {
                return;
            }
            wearing = on;
            try
            {
                // The two diagonals: a corner whose pulls agree in sign lies along
                // one, and one whose pulls disagree along the other.
                Texture2D arrows = on ? Glyphs.Resizer(pull.x * pull.y < 0f) : null;
                Cursor.SetCursor(arrows,
                    on ? new Vector2(arrows.width * 0.5f, arrows.height * 0.5f)
                       : Vector2.zero,
                    CursorMode.Auto);
            }
            catch (Exception)
            {
                wearing = false;
            }
        }

        public void OnBeginDrag(PointerEventData move)
        {
            holding = Local(move, out last);
        }

        public void OnDrag(PointerEventData move)
        {
            Vector2 now;
            if (!holding || !Local(move, out now))
            {
                return;
            }
            Vector2 step = now - last;
            last = now;
            if (Pulled != null)
            {
                Pulled(step, pull);
            }
        }

        public void OnEndDrag(PointerEventData move)
        {
            holding = false;
            Wear(false, pull);
            if (Dropped != null)
            {
                Dropped();
            }
        }

        private bool Local(PointerEventData move, out Vector2 local)
        {
            local = Vector2.zero;
            RectTransform parent = transform.parent == null ? null
                : transform.parent.parent as RectTransform;
            return parent != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                       parent, move.position, null, out local);
        }
    }
}
