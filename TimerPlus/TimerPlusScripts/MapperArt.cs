using System;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// The two icons Besiege's own key selector uses for "key or variable",
    /// borrowed off the game's mapper so the table's cells read the same as the
    /// row above them.
    ///
    /// They are the speech bubbles beside a key's name in the block mapper: one
    /// with three dots, which is offered while the key answers the keyboard and
    /// switches it to a variable, and one with a cross, which is offered while a
    /// variable is bound and takes it back. `Selectors.KeySelector` holds both as
    /// public fields, and `ToggleVar` settles which is which --
    /// `messageToggleOff.SetActive(on)` against
    /// `messageToggleOn.SetActive(!on &amp;&amp; AdvancedBuilding)`, so *On* is the
    /// one shown while the variable is off.
    ///
    /// The mapper is mesh UI, not uGUI, so what is taken is the `Texture` off the
    /// button's own renderer. That is enough for a `RawImage`, which takes a
    /// `Texture` rather than the `Sprite` an `Image` wants -- and so needs nothing
    /// converted and nothing assumed about the texture's type.
    ///
    /// `Selectors` is not on the mod loader's namespace blacklist; `InternalModding`,
    /// where the rest of the mapper lives, is.
    /// </summary>
    public static class MapperArt
    {
        private static bool found;
        private static Texture keyIcon;
        private static Texture variableIcon;

        /// <summary>The three-dot bubble: this cell is on the keyboard, and
        /// clicking hands it to a variable. Null if the mapper could not be read.
        /// </summary>
        public static Texture KeyIcon { get { Look(); return keyIcon; } }

        /// <summary>The crossed bubble: this cell is on a variable, and clicking
        /// takes it back to the keyboard.</summary>
        public static Texture VariableIcon { get { Look(); return variableIcon; } }

        /// <summary>Whether both were found. A caller that gets false draws
        /// lettering instead.</summary>
        public static bool Ready { get { Look(); return found; } }

        private static void Look()
        {
            if (found)
            {
                return;
            }
            try
            {
                // FindObjectsOfTypeAll rather than FindObjectsOfType: the mapper's
                // selectors are pooled and most of them are inactive most of the
                // time, and an inactive one has the artwork just the same.
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
