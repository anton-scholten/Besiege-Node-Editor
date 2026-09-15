using System;

namespace NodeEditorMod
{
    /// <summary>The editor's copy, paste and select-all hotkeys, as the player
    /// bound them (declared under `&lt;Keys&gt;` in `Mod.xml`). Looked up
    /// once.</summary>
    public static class Hotkeys
    {
        private static bool asked;
        private static Modding.ModKey copy;
        private static Modding.ModKey paste;
        private static Modding.ModKey all;

        public static bool Copy { get { Look(); return Down(copy); } }

        public static bool Paste { get { Look(); return Down(paste); } }

        /// <summary>Every node on the board picked out.</summary>
        public static bool All { get { Look(); return Down(all); } }

        private static bool Down(Modding.ModKey key)
        {
            try
            {
                return key != null && key.IsPressed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Look()
        {
            if (asked)
            {
                return;
            }
            asked = true;
            try
            {
                copy = Modding.ModKeys.GetKey("board-copy");
                paste = Modding.ModKeys.GetKey("board-paste");
                all = Modding.ModKeys.GetKey("board-select-all");
            }
            catch (Exception e)
            {
                Log.Warn("the editor's hotkeys are unavailable: " + e.Message);
            }
        }
    }
}
