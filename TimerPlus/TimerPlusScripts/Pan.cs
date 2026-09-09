using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TimerPlusMod
{
    /// <summary>
    /// A drag with the middle button, wherever it starts: the board moves under
    /// the hand and whatever was pressed stays where it is.
    ///
    /// Its own component so it can sit beside the handlers that do something with
    /// the left button -- a node's drag, a port's wire -- rather than being written
    /// again in each of them. uGUI gives a drag to every handler on the object it
    /// finds, so the two share the event and each ignores the other's button.
    ///
    /// Measured in <see cref="against"/>, which is the thing that stands still
    /// while the view moves: measuring inside what is being panned gives a step the
    /// pan itself cancels out, and the board sticks.
    /// </summary>
    public class Pan : MonoBehaviour, IBeginDragHandler, IDragHandler,
                       IEndDragHandler
    {
        public Action<Vector2> Panned;

        public RectTransform against;

        private Vector2 last;
        private bool waving;

        public void OnBeginDrag(PointerEventData move)
        {
            waving = move.button == PointerEventData.InputButton.Middle
                  && Panned != null && Where(move, out last);
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
