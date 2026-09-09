using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TimerPlusMod
{
    /// <summary>
    /// Drags a node, or the editor's window, about.
    ///
    /// The movement is worked out in the parent's own coordinates rather than from
    /// `PointerEventData.delta`, which is in **screen pixels**. The canvas is
    /// scaled to a 1080-tall reference, so on a 4K screen one canvas unit is two
    /// pixels and a window moved by the raw delta travels at half the speed of the
    /// hand holding it. Asking the rect where the pointer is has no such scale in
    /// it and is right at any resolution.
    ///
    /// The node's place is kept in the graph rather than in the transform, because
    /// the graph is what is saved -- so the drag reports how far it moved and the
    /// editor writes the number.
    /// </summary>
    public class NodeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler,
                            IEndDragHandler
    {
        /// <summary>How far it has moved since the press, in the parent's units.
        /// Called with the step since the last frame.</summary>
        public Action<Vector2> Moved;

        /// <summary>The drag ended, which is when the graph is written back: a
        /// commit per frame of a drag would be an undo entry per frame.</summary>
        public Action Dropped;

        /// <summary>The drag began. Whatever is being moved wants to know where it
        /// started from -- an axis-locked drag is measured from there, not from the
        /// step before it.</summary>
        public Action Held;

        /// <summary>What actually moves, when that is not this object -- the
        /// window, dragged by its title bar. The movement has to be measured in the
        /// space that thing is positioned in and which stands still while it moves:
        /// measuring inside the window itself gives a step that is cancelled by the
        /// move it caused, and the window sticks.</summary>
        public RectTransform frame;

        /// <summary>Where the pointer let go, in screen coordinates, for a drag
        /// that carries something rather than moving it -- a gate pulled out of the
        /// palette onto the board. Set instead of <see cref="Moved"/>, not as well
        /// as it.</summary>
        public Action<Vector2> Carried;

        /// <summary>Where the pointer is while it carries, so whatever is being
        /// carried can be drawn under it.</summary>
        public Action<Vector2> Carrying;

        private Vector2 last;
        private bool holding;

        public void OnBeginDrag(PointerEventData move)
        {
            // The left button moves things. The middle one pans the board -- see
            // `Pan`, which sits beside this -- and the right one is for menus and
            // moves nothing.
            holding = move.button == PointerEventData.InputButton.Left
                   && Carried == null && Local(move, out last);
            if (holding && Held != null)
            {
                Held();
            }
            if (Carried != null && Carrying != null)
            {
                Carrying(move.position);
            }
            // The game is held off for the length of the drag, wherever the pointer
            // wanders: a node dragged to the edge of the window takes the board with
            // it, and the pointer is outside by then.
            grip = ZoomGuard.Grip(holding || Carried != null, grip);
        }

        /// <summary>Whether the game is being held off for this drag.</summary>
        private bool grip;

        private void OnDisable()
        {
            holding = false;
            grip = ZoomGuard.Grip(false, grip);
        }

        public void OnDrag(PointerEventData move)
        {
            if (Carried != null)
            {
                if (Carrying != null)
                {
                    Carrying(move.position);
                }
                return;
            }
            Vector2 now;
            if (!holding || !Local(move, out now))
            {
                return;
            }
            Vector2 step = now - last;
            last = now;
            if (Moved != null)
            {
                Moved(step);
            }
        }

        public void OnEndDrag(PointerEventData move)
        {
            holding = false;
            grip = ZoomGuard.Grip(false, grip);
            if (Carried != null)
            {
                Carried(move.position);
                return;
            }
            if (Dropped != null)
            {
                Dropped();
            }
        }

        /// <summary>
        /// The ground moved under the drag.
        ///
        /// The step handed on is the difference between where the pointer is and
        /// where it was, both measured in the parent -- so when the parent itself
        /// is moved, the pointer appears to have moved that far without anybody
        /// touching it. Whoever moved it says so here, and the next step is the
        /// hand's own movement again.
        /// </summary>
        public void Shifted(Vector2 by)
        {
            last += by;
        }

        /// <summary>Where the pointer is, in the coordinates the thing being moved
        /// is positioned in. Null camera: the canvas is a screen-space overlay.
        /// </summary>
        private bool Local(PointerEventData move, out Vector2 local)
        {
            local = Vector2.zero;
            RectTransform moved = frame != null ? frame : transform as RectTransform;
            RectTransform parent = moved == null ? null
                : moved.parent as RectTransform;
            if (parent == null)
            {
                return false;
            }
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, move.position, null, out local);
        }
    }
}
