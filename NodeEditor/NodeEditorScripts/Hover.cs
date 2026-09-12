using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>
    /// Says when the pointer is over a node, and which node that is.
    ///
    /// On the node's body rather than on each of its parts: uGUI sends enter and
    /// exit to every object in the chain above what the pointer is actually over,
    /// so one of these covers the head, the key cell, the ports and the cross.
    ///
    /// The editor wants it for the modifier-click: holding control over a node
    /// outlines it faintly to say it can be picked out, and the click itself is
    /// taken from the pointer rather than from whichever control it landed on --
    /// those have their own ideas about a click.
    /// </summary>
    public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                         IPointerClickHandler
    {
        /// <summary>Which node this is, and whether the pointer is on it.</summary>
        public Action<int, bool> Over;

        /// <summary>
        /// The node's body was clicked, which is how a node is picked out.
        ///
        /// A click that lands on a port, a switch or a text field never arrives
        /// here: uGUI gives a click to the first handler at or above whatever was
        /// hit, and each of those carries its own. That is the whole of the rule
        /// that says clicking a node's key cell edits the key rather than picking
        /// the node out.
        /// </summary>
        public Action<int> Chose;

        public int Node;

        public void OnPointerClick(PointerEventData click)
        {
            if (Chose != null && click.button == PointerEventData.InputButton.Left)
            {
                Chose(Node);
            }
        }

        public void OnPointerEnter(PointerEventData pointer) { Say(true); }

        public void OnPointerExit(PointerEventData pointer) { Say(false); }

        private void OnDisable() { Say(false); }

        private void Say(bool on)
        {
            if (Over != null)
            {
                Over(Node, on);
            }
        }
    }
}
