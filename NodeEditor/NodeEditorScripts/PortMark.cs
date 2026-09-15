using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>One end of a wire on a node. It handles its own drag: otherwise
    /// uGUI hands a drag from the port to the draggable node under it.</summary>
    public class PortMark : MonoBehaviour, IBeginDragHandler, IDragHandler,
                            IEndDragHandler, IPointerClickHandler
    {
        public int Node;
        public int Port;
        public bool Output;

        /// <summary>A wire is being pulled: where the pointer is, and whether this
        /// is the press that began it.</summary>
        public Action<PortMark, Vector2, bool> Pulling;

        /// <summary>Let go: over another port, or over nothing, and where the
        /// pointer was when it happened.</summary>
        public Action<PortMark, PortMark, Vector2> Landed;

        /// <summary>Clicked rather than dragged from.</summary>
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
            Landed(this, other, move.position);
        }
    }
}
