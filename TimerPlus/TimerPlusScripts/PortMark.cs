using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TimerPlusMod
{
    /// <summary>
    /// One end of a wire: which node it belongs to, which of its inputs, and
    /// whether it is the answer coming out or a place for one to go.
    ///
    /// It handles the drag itself, which is the whole point of it being here: the
    /// node underneath is draggable too, and uGUI hands a drag to the first
    /// handler at or above the object pressed. With no handler on the port, a drag
    /// from a port moved the node instead -- which is exactly what a wire being
    /// pulled out of one is not.
    /// </summary>
    public class PortMark : MonoBehaviour, IBeginDragHandler, IDragHandler,
                            IEndDragHandler
    {
        public int Node;
        public int Port;
        public bool Output;

        /// <summary>A wire is being pulled from this port; the argument is where
        /// the pointer is, in screen coordinates.</summary>
        public Action<PortMark, Vector2> Pulling;

        /// <summary>Let go: over another port, or over nothing.</summary>
        public Action<PortMark, PortMark> Landed;

        public void OnBeginDrag(PointerEventData move)
        {
            if (Pulling != null)
            {
                Pulling(this, move.position);
            }
        }

        public void OnDrag(PointerEventData move)
        {
            if (Pulling != null)
            {
                Pulling(this, move.position);
            }
        }

        public void OnEndDrag(PointerEventData move)
        {
            if (Landed == null)
            {
                return;
            }
            // What the pointer was over when it let go. The port's own object is
            // what carries this, so a drop on a port's lettering finds it too.
            GameObject over = move.pointerCurrentRaycast.gameObject;
            PortMark other = over == null ? null : over.GetComponent<PortMark>();
            if (other == null && over != null && over.transform.parent != null)
            {
                other = over.transform.parent.GetComponent<PortMark>();
            }
            Landed(this, other);
        }
    }
}
