using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>The empty board behind the nodes: drag to box-select or pan, wheel
    /// to zoom, right-click for the palette.</summary>
    public class Sheet : MonoBehaviour, IBeginDragHandler, IDragHandler,
                         IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        /// <summary>Panned by this much, in the board's own units.</summary>
        public Action<Vector2> Panned;

        /// <summary>Wheeled: how much, and where the pointer was.</summary>
        public Action<float, Vector2> Zoomed;

        /// <summary>Right-clicked, with where.</summary>
        public Action<Vector2> Asked;

        /// <summary>Clicked on the empty board, which is what says "none of
        /// them": a selection is a thing to put down as well as to make.</summary>
        public Action Emptied;

        /// <summary>Dragged: a rectangle from where the press was to where the
        /// pointer is, and then the same again when it is let go.</summary>
        public Action<Vector2, Vector2, bool> Boxed;

        private Vector2 pressed;
        private bool boxing;

        private Vector2 last;
        private bool holding;

        public void OnBeginDrag(PointerEventData move)
        {
            // Left draws a selection box and middle pans, as in other editors.
            boxing = move.button == PointerEventData.InputButton.Left;
            holding = move.button == PointerEventData.InputButton.Middle
                   && Local(move, out last);
            if (boxing)
            {
                Local(move, out last);
            }
            pressed = last;
            // Held for the whole drag, even past the window's edge.
            grip = ZoomGuard.Grip(boxing || holding, grip);
        }

        /// <summary>Whether the game is being held off for this drag.</summary>
        private bool grip;

        public void OnDrag(PointerEventData move)
        {
            Vector2 now;
            if (!Local(move, out now))
            {
                return;
            }
            if (boxing)
            {
                if (Boxed != null)
                {
                    Boxed(pressed, now, false);
                }
                return;
            }
            if (!holding)
            {
                return;
            }
            Vector2 step = now - last;
            last = now;
            if (Panned != null)
            {
                Panned(step);
            }
        }

        public void OnEndDrag(PointerEventData move)
        {
            Vector2 now;
            if (boxing && Local(move, out now) && Boxed != null)
            {
                Boxed(pressed, now, true);
            }
            boxing = false;
            holding = false;
            grip = ZoomGuard.Grip(false, grip);
        }

        private void OnDisable()
        {
            boxing = false;
            holding = false;
            grip = ZoomGuard.Grip(false, grip);
        }

        public void OnScroll(PointerEventData move)
        {
            if (Zoomed != null)
            {
                Zoomed(move.scrollDelta.y, move.position);
            }
        }

        public void OnPointerClick(PointerEventData click)
        {
            if (click.button == PointerEventData.InputButton.Right && Asked != null)
            {
                Asked(click.position);
                return;
            }
            if (click.button == PointerEventData.InputButton.Left && Emptied != null)
            {
                Emptied();
            }
        }

        /// <summary>Where the pointer is, in this rect's own coordinates. Null
        /// camera: the canvas is a screen-space overlay.</summary>
        private bool Local(PointerEventData move, out Vector2 local)
        {
            local = Vector2.zero;
            RectTransform rect = transform as RectTransform;
            return rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, move.position, null, out local);
        }
    }
}
