using System;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Besiege's key-selector icons for "key or variable", read off the game's
    /// mapper (`Selectors.KeySelector`, which is not blacklisted) so the cells
    /// match it: three dots while on the keyboard, a cross while on a variable.
    /// Taken as `Texture`s for `RawImage`.
    /// </summary>
    public static class MapperArt
    {
        private static bool found;
        private static Texture keyIcon;
        private static Texture variableIcon;

        /// <summary>The three-dot bubble: on the keyboard; a click moves to a
        /// variable. Null if the mapper could not be read.</summary>
        public static Texture KeyIcon { get { Look(); return keyIcon; } }

        /// <summary>The crossed bubble: this cell is on a variable, and clicking
        /// takes it back to the keyboard.</summary>
        public static Texture VariableIcon { get { Look(); return variableIcon; } }

        /// <summary>Whether both were found. A caller that gets false draws
        /// lettering instead.</summary>
        public static bool Ready { get { Look(); return found; } }

        /// <summary>When the next look is allowed: one scan a second while it finds
        /// nothing, not one per cell per frame.</summary>
        private static float next;

        private static void Look()
        {
            if (found || Time.realtimeSinceStartup < next)
            {
                return;
            }
            next = Time.realtimeSinceStartup + 1f;
            try
            {
                // FindObjectsOfTypeAll: the mapper's selectors are pooled and
                // mostly inactive.
                Selectors.KeySelector[] all =
                    Resources.FindObjectsOfTypeAll<Selectors.KeySelector>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null)
                    {
                        continue;
                    }
                    if (keyIcon == null)
                    {
                        keyIcon = Painted(all[i].messageToggleOn);
                    }
                    if (variableIcon == null)
                    {
                        variableIcon = Painted(all[i].messageToggleOff);
                    }
                    if (keyIcon != null && variableIcon != null)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("could not read the mapper's key icons: " + e.Message);
            }
            // Only ever remembered as a yes: the mapper has to have been built once
            // for its selectors to exist, and the first ask may be before that.
            found = keyIcon != null && variableIcon != null;
        }

        /// <summary>The picture a mapper button is drawn with, or null.</summary>
        private static Texture Painted(UIButton button)
        {
            if (button == null)
            {
                return null;
            }
            Renderer[] parts = button.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || parts[i].sharedMaterial == null)
                {
                    continue;
                }
                Texture worn = parts[i].sharedMaterial.mainTexture;
                if (worn != null)
                {
                    return worn;
                }
            }
            return null;
        }
    }
}
