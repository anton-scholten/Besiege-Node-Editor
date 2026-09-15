using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>Reports the pointer over a node, from the node's body: enter and
    /// exit reach every parent, so one covers all its parts.</summary>
    public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                         IPointerClickHandler
    {
        /// <summary>Which node this is, and whether the pointer is on it.</summary>
        public Action<int, bool> Over;

        /// <summary>The body was clicked, which picks the node. A click on one of
        /// its controls never arrives: uGUI gives it to the control.</summary>
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
