using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>
    /// A right-click on a control that has a list to offer as well as a click of
    /// its own -- the node editor's COLOR selector.
    ///
    /// Beside the control's own `Button` rather than instead of it: uGUI hands a
    /// click to every click handler on the object it lands on, and a `Button`
    /// answers the left button only, so each of the two hears its own button.
    /// </summary>
    public class Asks : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Where the right-click was, in screen coordinates.</summary>
        public Action<Vector2> Asked;

        public void OnPointerClick(PointerEventData click)
        {
            if (click.button == PointerEventData.InputButton.Right && Asked != null)
            {
                Asked(click.position);
            }
        }
    }
}
