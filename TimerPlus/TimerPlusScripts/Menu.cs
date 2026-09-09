using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TimerPlusMod
{
    /// <summary>
    /// A right-click on whatever it is put on, with where the pointer was.
    ///
    /// Its own component because uGUI reports a click of any button through the
    /// one handler, and everything else here wants only the left one.
    /// </summary>
    public class Menu : MonoBehaviour, IPointerClickHandler
    {
        public Action<Vector2> Clicked;

        public void OnPointerClick(PointerEventData pointer)
        {
            if (pointer.button != PointerEventData.InputButton.Right
                || Clicked == null)
            {
                return;
            }
            Clicked(pointer.position);
        }
    }
}
