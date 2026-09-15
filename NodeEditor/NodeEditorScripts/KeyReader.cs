using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// A mapper key read from the keyboard and from variables together. Emulated
    /// edges are latched in <c>KeyEmulationUpdate</c> once a tick and consumed by
    /// the frame update: <c>MKey.CheckEmulation</c> snapshots per fixed step, so
    /// polling each frame doubled or missed presses.
    /// </summary>
    public class KeyReader
    {
        private readonly MKey key;

        private bool emulatedPress;
        private bool emulatedRelease;
        private bool emulatedHeld;

        /// <summary>What the key is doing, as of the last <see cref="Poll"/>.</summary>
        public bool Pressed;
        public bool Held;
        public bool Released;

        public KeyReader(MKey key)
        {
            this.key = key;
        }

        public MKey Mapper { get { return key; } }

        /// <summary>Keyboard and emulated edges kept apart. <see cref="Poll"/>
        /// merges them for a timer; a logic gate reads them separately, as
        /// Besiege's does.</summary>
        public bool RealPressed { get { return key != null && key.IsPressed; } }

        public bool RealHeld { get { return key != null && key.IsHeld; } }

        /// <summary>The keyboard's release. `IsReleased` ignores `useMessage`, so a
        /// key moved to a variable would still report its old keycode.</summary>
        public bool RealReleased
        {
            get { return key != null && !key.useMessage && key.IsReleased; }
        }

        public bool EmulatedHeld { get { return emulatedHeld; } }

        /// <summary>The latched emulated press, handed out once.</summary>
        public bool TakePress()
        {
            bool had = emulatedPress;
            emulatedPress = false;
            return had;
        }

        /// <summary>The latched emulated release, handed out once.</summary>
        public bool TakeRelease()
        {
            bool had = emulatedRelease;
            emulatedRelease = false;
            return had;
        }

        /// <summary>From <c>KeyEmulationUpdate</c> only. A key's snapshot advances
        /// only when its own edge methods are asked, so every reader is asked every
        /// tick, in separate statements.</summary>
        public void ReadEmulation()
        {
            if (key == null)
            {
                return;
            }
            emulatedPress |= key.EmulationPressed();
            emulatedRelease |= key.EmulationReleased();
            emulatedHeld = key.EmulationHeld(true);
        }

        /// <summary>From the per-frame update. Each emulated edge is handed out
        /// once.</summary>
        public void Poll()
        {
            if (key == null)
            {
                Pressed = false;
                Held = false;
                Released = false;
                return;
            }
            Pressed = key.IsPressed || emulatedPress;
            Held = key.IsHeld || emulatedHeld;

            // IsReleased ignores useMessage, so that is checked here.
            Released = (!key.useMessage && key.IsReleased) || emulatedRelease;

            emulatedPress = false;
            emulatedRelease = false;
        }
    }
}
