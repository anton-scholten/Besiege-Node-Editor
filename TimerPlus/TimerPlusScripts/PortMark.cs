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
                            IEndDragHandler, IPointerClickHandler
    {
        public int Node;
        public int Port;
        public bool Output;

        /// <summary>A wire is being pulled from this port: where the pointer is,
        /// in screen coordinates, and whether this is the press that began it --
        /// which is when a wire already on the port is picked up rather than a new
        /// one started.</summary>
        public Action<PortMark, Vector2, bool> Pulling;

        /// <summary>Let go: over another port, or over nothing.</summary>
        public Action<PortMark, PortMark> Landed;

        /// <summary>Clicked rather than dragged from. The port draws itself and
        /// carries no button of its own -- a plate behind a circle is a second
        /// thing to look at where there is only one thing to do.</summary>
        public Action<PortMark> Clicked;

        public void OnPointerClick(PointerEventData click)
        {
            if (Clicked != null && click.button == PointerEventData.InputButton.Left)
            {
                Clicked(this);
            }
        }

        public void OnBeginDrag(PointerEventData move)
        {
            // The left button pulls a wire. The middle one pans the board, and a
            // port is not where that should turn into a wire nobody asked for.
            drawing = move.button == PointerEventData.InputButton.Left;
            if (drawing && Pulling != null)
            {
                Pulling(this, move.position, true);
            }
        }

        private bool drawing;

        public void OnDrag(PointerEventData move)
        {
            if (drawing && Pulling != null)
            {
                Pulling(this, move.position, false);
            }
        }

        public void OnEndDrag(PointerEventData move)
        {
            if (!drawing)
            {
                return;
            }
            drawing = false;
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
