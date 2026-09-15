using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>
    /// Drags a node or the editor's window. Measured in the parent's coordinates,
    /// not by `PointerEventData.delta`, which is screen pixels and wrong on a
    /// scaled canvas. It reports the movement; the editor writes positions into the
    /// graph.
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

        /// <summary>The drag began, so an axis-locked drag can measure from its
        /// start.
        /// </summary>
        public Action Held;

        /// <summary>What moves when it is not this object (the window, by its title
        /// bar), measured in its parent, which stands still.</summary>
        public RectTransform frame;

        /// <summary>Where a carrying drag let go, in screen coordinates: a palette
        /// gate dropped on the board. Set instead of <see cref="Moved"/>.</summary>
        public Action<Vector2> Carried;

        /// <summary>Where the pointer is while it carries, so whatever is being
        /// carried can be drawn under it.</summary>
        public Action<Vector2> Carrying;

        private Vector2 last;
        private bool holding;

        public void OnBeginDrag(PointerEventData move)
        {
            // Left moves, middle pans (a `Pan` beside this), right is for menus.
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
            // The game is held off for the whole drag, even with the pointer
            // outside.
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

        /// <summary>The parent moved under the drag, which would otherwise read as
        /// the pointer moving by as much.</summary>
        public void Shifted(Vector2 by)
        {
            last += by;
        }

        /// <summary>The pointer in the moved thing's parent coordinates (overlay
        /// canvas, null camera).</summary>
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
