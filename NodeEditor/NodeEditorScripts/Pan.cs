using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>A middle-button drag pans the board wherever it starts, beside the
    /// left-button handlers. Measured in <see cref="against"/>, which stands still.
    /// </summary>
    public class Pan : MonoBehaviour, IBeginDragHandler, IDragHandler,
                       IEndDragHandler
    {
        public Action<Vector2> Panned;

        public RectTransform against;

        private Vector2 last;
        private bool waving;

        /// <summary>Whether the game is being held off for this drag. See
        /// <see cref="ZoomGuard.Grip"/>.</summary>
        private bool grip;

        public void OnBeginDrag(PointerEventData move)
        {
            waving = move.button == PointerEventData.InputButton.Middle
                  && Panned != null && Where(move, out last);
            grip = ZoomGuard.Grip(waving, grip);
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
