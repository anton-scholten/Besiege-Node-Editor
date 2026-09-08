using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// A mapper key read from the keyboard and from Besiege's variable system at
    /// once, so automation drives a row exactly as a keypress does.
    ///
    /// The edges cannot be taken from an ordinary frame update.
    /// <c>MKey.CheckEmulation</c> keys its snapshot to <c>Time.fixedTime</c>: it
    /// advances the first time it is asked in a fixed step and answers the same
    /// thing for the rest of it. Polled per frame, a variable held for one step
    /// lands two or three times at a high frame rate and is missed entirely at a
    /// low one.
    ///
    /// So the edges are latched in <c>KeyEmulationUpdate</c>, which Besiege runs
    /// once per emulation tick, and handed to the frame update, which consumes
    /// each one once.
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

        /// <summary>
        /// The keyboard's own edges, and the emulated ones, separately.
        ///
        /// <see cref="Poll"/> merges the two, which is what a timer wants: one
        /// activation, however it arrives. A logic gate wants them apart --
        /// Besiege's own runs its state machine twice, once with the keyboard's
        /// edges in its frame update and once with the emulated ones on the
        /// emulation tick -- so these hand each set over on its own.
        /// </summary>
        public bool RealPressed { get { return key != null && key.IsPressed; } }

        public bool RealHeld { get { return key != null && key.IsHeld; } }

        /// <summary>`IsReleased` is the one key property that does not check
        /// `useMessage` for itself, so a key handed over to a variable still
        /// reports the player letting go of the keycode it used to be bound
        /// to.</summary>
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

        /// <summary>
        /// From <c>KeyEmulationUpdate</c> and nowhere else.
        ///
        /// Each key's snapshot advances only when one of *its own* edge methods is
        /// called, so every reader has to be asked every tick -- which is why the
        /// caller loops over all of them rather than combining with <c>||</c>, and
        /// why the three calls here are separate statements. Asking one key for
        /// both edges in the same tick is free: the advance is inside a
        /// <c>fixedTime</c> guard and happens once however many times it is asked.
        /// </summary>
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

            // IsReleased is the one key property that does not check useMessage
            // for itself, so a key handed over to a variable still reports the
            // player letting go of the keycode it used to be bound to.
            Released = (!key.useMessage && key.IsReleased) || emulatedRelease;

            emulatedPress = false;
            emulatedRelease = false;
        }
    }
}
