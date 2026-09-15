using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    // Stops the camera zooming while the pointer is over this. DisableCameraZoom is
    // a counter, so each hold is given back exactly once, even if destroyed while
    // hovered. One on a window covers every child.
    public class ZoomGuard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Whether to tell Besiege a menu is open as well: the editor's
        /// window does; the docked panel is already under a menu.</summary>
        public bool menu;

        private bool held;

        /// <summary>Frames before the menu hold follows the zoom hold. Both in one
        /// frame left `MouseOrbit` easing toward a stale wheel reading, zooming for
        /// as long as the pointer stayed.</summary>
        private int waiting;
        private bool menued;

        /// <summary>How many of this mod's pieces hold a menu open, so ours can be
        /// told from the game's.</summary>
        public static int Raised;

        /// <summary>Tells Besiege a menu is open, counted. Everything here that
        /// raises `SetInMenu` goes through this, or the editor takes its own for
        /// the pause menu.
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

        /// <summary>Holds the menu flag for a drag wherever the pointer goes, so
        /// Besiege's camera does not take the drag up. The caller keeps the flag;
        /// call it from `OnDisable` too.</summary>
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
