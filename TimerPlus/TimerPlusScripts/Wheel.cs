using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// Hands the mouse wheel to a <see cref="ScrollRect"/> that is not in the
    /// pointer's way up the hierarchy.
    ///
    /// uGUI delivers a scroll to the hovered object and then to its parents until
    /// something handles it, and a `ScrollRect` handles it on its own object. The
    /// panel's rows do not live under that object: the frame they scroll in is
    /// built by the panel and parented to the window, because the prefab's own
    /// frame could not be found or trusted -- see `Panel.Clip`. So the wheel
    /// travels up from a row, past the frame, past the window, and never meets the
    /// ScrollRect at all.
    ///
    /// This sits on the frame and passes it on. `ScrollRect.OnScroll` and its drag
    /// handlers are public, and are exactly what the event system would have
    /// called.
    /// </summary>
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

        // And dragging, for the same reason and by the same route: a list you can
        // wheel but not drag is a list that half works. A control that does its own
        // dragging -- an input field selecting its text -- handles the event itself
        // and it never arrives here.
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
