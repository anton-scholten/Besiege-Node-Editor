using System;

namespace TimerPlusMod
{
    /// <summary>
    /// The node editor's own hotkeys -- copy and paste -- as the player has them
    /// bound.
    ///
    /// Declared in `Mod.xml` under `&lt;Keys&gt;`, which is what puts them in
    /// Besiege's own controls screen -- so they can be rebound like anything else,
    /// and this must ask rather than test `Ctrl+C` itself.
    ///
    /// Looked up once and kept: the lookup is a dictionary in the mod loader, and
    /// rebinding changes the key object rather than replacing it.
    /// </summary>
    public static class Hotkeys
    {
        private static bool asked;
        private static Modding.ModKey copy;
        private static Modding.ModKey paste;

        public static bool Copy { get { Look(); return Down(copy); } }

        public static bool Paste { get { Look(); return Down(paste); } }

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
            }
            catch (Exception e)
            {
                Log.Warn("the editor's hotkeys are unavailable: " + e.Message);
            }
        }
    }
}
