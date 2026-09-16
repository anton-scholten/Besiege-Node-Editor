using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>A middle-button drag pans the board wherever it starts, beside the
    /// left-button handlers. Measured in <see cref="against"/>, which stands still.
    /// </summary>
    public class Pan : MonoBehaviour, IBeginDragHandler, IDragHandler,
                       IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Action<Vector2> Panned;

        public RectTransform against;

        private Vector2 last;
        private bool waving;

        /// <summary>Whether the game is being held off for this drag. See
        /// <see cref="ZoomGuard.Grip"/>.</summary>
        private bool grip;

        /// <summary>The press, not the drag. A middle click that never moves sends
        /// no drag events at all, and the frames between a press and the drag
        /// threshold send none either -- so the game saw a middle press over the
        /// window and panned its camera with it. `MouseOrbit` skips its whole
        /// pan-and-orbit block only while the menu count is up, so the count has to
        /// be up from the press itself.</summary>
        public void OnPointerDown(PointerEventData press)
        {
            if (press.button == PointerEventData.InputButton.Middle)
            {
                grip = ZoomGuard.Grip(true, grip);
            }
        }

        /// <summary>Given back on the release, unless a drag of its own is running:
        /// that gives it back when it ends. Either order uGUI sends these in is
        /// safe, as a hold is idempotent.</summary>
        public void OnPointerUp(PointerEventData press)
        {
            if (!waving)
            {
                grip = ZoomGuard.Grip(false, grip);
            }
        }

        public void OnBeginDrag(PointerEventData move)
        {
            waving = move.button == PointerEventData.InputButton.Middle
                  && Panned != null && Where(move, out last);
            grip = ZoomGuard.Grip(waving || grip, grip);
        }

        public void OnDrag(PointerEventData move)
        {
            Vector2 now;
            if (!waving || !Where(move, out now))
            {
                return;
            }
            Vector2 step = now - last;
            last = now;
            Panned(step);
        }

        public void OnEndDrag(PointerEventData move)
        {
            waving = false;
            grip = ZoomGuard.Grip(false, grip);
        }

        private void OnDisable()
        {
            OnEndDrag(null);
        }

        private bool Where(PointerEventData move, out Vector2 local)
        {
            local = Vector2.zero;
            // Null camera: the canvas is a screen-space overlay.
            return against != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    against, move.position, null, out local);
        }
    }
}
