using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TimerPlusMod
{
    // Stops the camera zooming while the pointer is over this, so the wheel
    // scrolls a list rather than pulling the camera in.
    //
    // DisableCameraZoom is a counter, not a flag -- Besiege's own scrollbars hold
    // it the same way -- so every hold must be given back exactly once, including
    // when the object goes away while still hovered. Enter and exit reach the whole
    // chain of parents, so one of these on a window covers every row in it.
    public class ZoomGuard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Whether to tell Besiege a menu is open as well as holding the
        /// zoom off. The editor wants it -- a right-click over its window should
        /// open its own menu rather than swing the camera round the machine -- and
        /// the docked panel does not, because the mapper it hangs under is already
        /// a menu as far as the game is concerned.</summary>
        public bool menu;

        private bool held;

        /// <summary>How many of this mod's own pieces are telling Besiege a menu
        /// is open, so that anything asking "is a menu up" can tell ours from the
        /// game's.</summary>
        public static int Raised;

        /// <summary>
        /// Tells Besiege a menu is open, and counts that it was us who said so.
        ///
        /// Anything in this mod that raises `SetInMenu` has to come through here.
        /// A key cell listening for a keypress raises it too, and while it did so
        /// on its own the editor read its own key-binding cell as the pause menu
        /// and shut itself -- which is what clicking a node's key did.
        /// </summary>
        public static void Menu(bool on)
        {
            StatMaster.SetInMenu(on);
            Raised += on ? 1 : -1;
            if (Raised < 0)
            {
                Raised = 0;
            }
        }

        public void OnPointerEnter(PointerEventData pointer) { Hold(true); }
        public void OnPointerExit(PointerEventData pointer) { Hold(false); }

        private void OnDisable() { Hold(false); }

        private void Hold(bool on)
        {
            if (held == on) return;
            try
            {
                StatMaster.DisableCameraZoom(on);
                if (menu)
                {
                    Menu(on);
                }
                held = on;
            }
            catch (Exception)
            {
                held = false;
            }
        }
    }
}
