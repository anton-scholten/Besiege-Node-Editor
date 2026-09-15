using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>A right-click list on a control that also has a left click of its
    /// own: the editor's COLOR selector.</summary>
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
