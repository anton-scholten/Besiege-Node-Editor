using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
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

        /// <summary>
        /// Frames left before the menu hold follows the zoom hold, and whether it
        /// has.
        ///
        /// Not both at once. `MouseOrbit.Update` eases the camera's zoom towards
        /// the last wheel reading *before* it looks at `inMenu`, and takes a fresh
        /// reading -- nought while zoom is disabled -- only *after* it. A pointer
        /// scrolled onto the window raised both in the same frame, so that reading
        /// was never taken again and the camera went on zooming for as long as the
        /// pointer stayed. With the menu a few frames behind, one pass sees zoom
        /// held off and no menu, and puts the reading to nought.
        /// </summary>
        private int waiting;
        private bool menued;

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

        /// <summary>
        /// Takes that hold for the length of a drag, wherever the pointer goes.
        ///
        /// A drag that began over this mod's own window carries on when the pointer
        /// leaves it -- panning the board to its edge and carrying on is exactly
        /// how -- and the guard above lets go the moment the pointer is outside,
        /// whereupon Besiege's own camera picks the same drag up and swings the
        /// view. `MouseOrbit.Update` reads `StatMaster.inMenu` once and skips its
        /// whole pan-and-orbit block when it is set, so holding it for the drag is
        /// all it takes.
        ///
        /// The caller keeps the flag and hands it back, so a hold cannot be taken
        /// or given twice; call it from `OnDisable` as well, or a window torn down
        /// mid-drag leaves the game believing a menu is open.
        /// </summary>
        public static bool Grip(bool want, bool have)
        {
            if (want == have)
            {
                return have;
            }
            try
            {
                Menu(want);
            }
            catch (Exception)
            {
                return have;
            }
            return want;
        }

        public void OnPointerEnter(PointerEventData pointer) { Hold(true); }
        public void OnPointerExit(PointerEventData pointer) { Hold(false); }

        private void OnDisable() { Hold(false); }

        private void Update()
        {
            if (waiting <= 0)
            {
                return;
            }
            waiting--;
            if (waiting > 0 || !held || menued)
            {
                return;
            }
            try
            {
                Menu(true);
                menued = true;
            }
            catch (Exception)
            {
            }
        }

        private void Hold(bool on)
        {
            if (held == on) return;
            try
            {
                StatMaster.DisableCameraZoom(on);
                held = on;
                if (!menu)
                {
                    return;
                }
                if (on)
                {
                    // Three rather than one: which of this and the camera runs
                    // first in a frame is not ours to know.
                    waiting = 3;
                    return;
                }
                waiting = 0;
                if (menued)
                {
                    Menu(false);
                    menued = false;
                }
            }
            catch (Exception)
            {
                held = false;
            }
        }
    }
}
