using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>Passes the wheel and drags on to the panel's <see
    /// cref="ScrollRect"/>, which is not above the rows in the hierarchy (see
    /// `Panel`'s frame).</summary>
    public class Wheel : MonoBehaviour, IScrollHandler, IBeginDragHandler,
                         IDragHandler, IEndDragHandler
    {
        public ScrollRect target;

        public void OnScroll(PointerEventData pointer)
        {
            if (target != null)
            {
                target.OnScroll(pointer);
            }
        }

        // Dragging too, by the same route; a control that drags for itself never
        // sends it.
        public void OnBeginDrag(PointerEventData pointer)
        {
            if (target != null)
            {
                target.OnBeginDrag(pointer);
            }
        }

        public void OnDrag(PointerEventData pointer)
        {
            if (target != null)
            {
                target.OnDrag(pointer);
            }
        }

        public void OnEndDrag(PointerEventData pointer)
        {
            if (target != null)
            {
                target.OnEndDrag(pointer);
            }
        }
    }
}
