using System.Collections.Generic;
using Modding.Modules;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// The Timer Plus block: up to <see cref="MaxRows"/> of Besiege's timer blocks
    /// in one, each row with the same settings the game's own timer has.
    ///
    /// The state machine is Besiege's <c>TimerBlock</c>, read out of
    /// <c>Assembly-CSharp</c> and reproduced per row, so a row behaves exactly as
    /// the block it stands in for and a converted machine runs the same as the one
    /// that made it. It lives in <see cref="Clock"/>, apart from the game, because
    /// it is the part of this whose failure is silent -- see the note there.
    /// Two deliberate differences, both stated where they happen:
    ///
    /// * The vanilla timer has a fourth phase that waits out the network ping
    ///   before starting, for a client without physics. A modded block's emulation
    ///   hook does not run on such a client at all -- the handler gates it on
    ///   <c>SimPhysics</c> -- so there is nothing for that phase to do here.
    /// * Activation is read once per frame from a latch rather than tested in both
    ///   the frame update and the emulation update, which is what keeps an
    ///   emulated press from landing twice. See <see cref="KeyReader"/>.
    ///
    /// Every setting is a mapper control. That is not incidental: <c>MKey</c> is
    /// the only mapper type that carries a variable, so a row that can be
    /// automated has to *be* keys, and keys can only be registered in
    /// <c>SafeAwake</c>. Hence a fixed cap on the rows, all of them built whether
    /// or not they are used -- an unused row is at its defaults, and Besiege omits
    /// a default-valued control from the save.
    /// </summary>
    public class TimerPlusBehaviour : BlockModuleBehaviour<TimerPlusModule>
    {
        /// <summary>
        /// How many rows a block can hold. Every row's controls exist from
        /// <c>SafeAwake</c> whether the row is in use or not, so this is a real
        /// cost and not just a limit: raising it costs seven mapper controls a row
        /// on every Timer Plus block in every machine.
        ///
        /// Changing it is safe in one direction only. Lowering it orphans the
        /// controls of any row above the new cap, and a machine saved with those
        /// rows loses them silently.
        /// </summary>
        public const int MaxRows = 32;

        // ---- the block's own settings ----------------------------------------

        /// <summary>What starts every row that has not been given an activation of
        /// its own. Defaulted to the same key Besiege's timer uses, so a block
        /// dropped in beside one answers to the same press.</summary>
        private MKey masterKey;

        /// <summary>Start with the simulation instead of on the key. Applies to the
        /// rows that follow the block; a row with its own key is its own affair.
        /// </summary>
        private MToggle automatic;

        /// <summary>How many rows are in use, 0..<see cref="MaxRows"/> -- a block
        /// with none is one whose timers have all been taken off it. A control
        /// rather than a field so that it is saved, undone and sent over
        /// multiplayer with everything else.</summary>
        private MSlider rowCount;

        /// <summary>Whether converting also drops a pin inside each block it
        /// makes. On by default: a field of timers arriving loose is a field of
        /// timers that falls over, and a pin with nothing bound and its visuals
        /// hidden is the way the game itself holds a block still.</summary>
        private MToggle pinBlocks;

        private readonly List<Row> rows = new List<Row>();
        private KeyReader masterReader;

        /// <summary>Every activation key this block owns, which is what Besiege is
        /// handed to stop a row pressing a key that starts this same block. Built
        /// once: <c>KeyInputController.EmulateEntry</c> compares by reference, and
        /// the references do not change.</summary>
        private MKey[] ownKeys;

        /// <summary>The block's display, which blinks red as a row fires.</summary>
        private readonly Glow glow = new Glow();

        /// <summary>What the block last told Besiege's mapper to show, so the
        /// display flags are written when the answer changes rather than every
        /// frame. Every change to one marks the mapper dirty and costs it a
        /// rebuild of all its widgets.</summary>
        private int shownRows = -1;
        private bool shownStock;

        public override bool EmulatesAnyKeys
        {
            // Without this the modding API refuses to emulate anything and logs
            // "uses EmulateKeys but does not set EmulatesAnyKeys" once per press.
            get { return true; }
        }

        /// <summary>The rows, for the panel and the converter.</summary>
        public List<Row> Rows { get { return rows; } }

        public MKey MasterKey { get { return masterKey; } }
        public MToggle Automatic { get { return automatic; } }

        /// <summary>How many rows are in use. Setting it writes the control, which
        /// the panel then commits so the change is saved and undoable.</summary>
        public int Count
        {
            get
            {
                if (rowCount == null)
                {
                    return 0;
                }
                return Mathf.Clamp(Mathf.RoundToInt(rowCount.Value), 0, MaxRows);
            }
            set
            {
                if (rowCount != null)
                {
                    rowCount.Value = Mathf.Clamp(value, 0, MaxRows);
                }
            }
        }

        public MSlider CountControl { get { return rowCount; } }

        /// <summary>The pin switch itself, for the panel to draw, and what it says.
        /// </summary>
        public MToggle PinControl { get { return pinBlocks; } }

        public bool Pins { get { return pinBlocks != null && pinBlocks.IsActive; } }

        // ---- setting up ------------------------------------------------------

        public override void SafeAwake()
        {
            // The block wears one mesh and one texture with nothing to swap to, so
            // the skin picker is an empty choice.
            Skins.Hide(BlockBehaviour);

            masterKey = AddKey("Activate", "Activate", KeyCode.B);
            automatic = AddToggle("Automatic", "AutomaticKey", false);

            // Whole numbers, but declared as a slider: the mapper has no integer
            // control, and a slider is at least draggable in Besiege's own mapper
            // when UI Factory is not installed and the panel cannot be drawn.
            //
            // Through BlockBehaviour, like the two below, for the suffix: the
            // modding API's own AddSlider hardcodes "x", and this is a count.
            int wanted = Module == null ? 3 : Module.Rows;
            pinBlocks = AddToggle("Pin blocks", "PinKey", true);
            rowCount = BlockBehaviour.AddSlider("Rows", "RowsKey",
                Mathf.Clamp(wanted, 0, MaxRows), 0f, MaxRows, "", "");

            rows.Clear();
            for (int i = 0; i < MaxRows; i++)
            {
                rows.Add(Build(i));
            }

            masterReader = new KeyReader(masterKey);

            // What a row is not allowed to press: the key that starts this block.
            // Besiege refuses the same thing for its own timer, and for the same
            // reason -- a block that presses its own starter is a loop with no
            // frame in it.
            ownKeys = new MKey[] { masterKey };

            ShowStock(!Panel.Usable);
        }

        private Row Build(int index)
        {
            string n = index.ToString();
            Row row = new Row();

            row.Emulate = AddEmulatorKey("Emulate " + (index + 1), "Emu" + n, KeyCode.C);

            // Through BlockBehaviour rather than the modding API's own
            // AddSliderUnclamped: that one hardcodes an "x" suffix, and these are
            // seconds. It is the same call on the same object --
            // ModBlockBehaviour.BlockBehaviour *is* the handler the modding API
            // adds to -- with the prefix and suffix spelled out. Unclamped and
            // 0..60, exactly as the game's own timer declares them, so an event
            // four minutes in is one row rather than a chain of them.
            row.Wait = BlockBehaviour.AddSliderUnclamped(
                "Wait " + (index + 1), "Wait" + n, 1f, 0f, 60f, "", "s", true);
            row.Duration = BlockBehaviour.AddSliderUnclamped(
                "Duration " + (index + 1), "Dur" + n, 1f, 0f, 60f, "", "s", true);

            row.Hold = AddToggle("Hold to run " + (index + 1), "Hold" + n, false);
            row.Stop = AddToggle("Allow stop " + (index + 1), "Stop" + n, false);
            row.Loop = AddToggle("Loop " + (index + 1), "Loop" + n, false);

            return row;
        }

        /// <summary>
        /// Shows or hides the block's *table* in Besiege's own mapper.
        ///
        /// The block's own activation key and its Automatic switch are never
        /// touched. They stay in the mapper, above the panel, because Besiege's own
        /// key selector is the only thing that can bind a key properly -- it has
        /// the whole variable picker, the ignore switch and the multi-key list --
        /// and because "what starts this block" is the mapper's own question. The
        /// panel is the answer to the other one: what the block does once started.
        /// The instrument blocks in the sibling Orchestra mod are laid out the same
        /// way and for the same reason.
        ///
        /// Everything else goes where the panel can draw. Where it cannot -- no UI
        /// Factory, or a panel that gave up -- the rows in use are handed back,
        /// because otherwise there is no way to set them at all. Rows above the
        /// count stay hidden either way: two hundred controls for three rows of
        /// settings is not a mapper anybody can use.
        ///
        /// Written only when the answer changes, and the answer is "can the panel
        /// draw" rather than "is it up now". Every assignment to
        /// <c>DisplayInMapper</c> marks the mapper dirty and costs it a rebuild of
        /// all its widgets, so taking them off when the panel opens and handing
        /// them back when it closes would cost three rebuilds a visit.
        /// </summary>
        public void ShowStock(bool visible)
        {
            int count = Count;
            if (shownStock == visible && shownRows == count)
            {
                return;
            }
            shownStock = visible;
            shownRows = count;

            if (rowCount != null) rowCount.DisplayInMapper = visible;
            if (pinBlocks != null) pinBlocks.DisplayInMapper = visible;

            for (int i = 0; i < rows.Count; i++)
            {
                bool on = visible && i < count;
                Row row = rows[i];
                if (!row.Ready)
                {
                    continue;
                }
                row.Emulate.DisplayInMapper = on;
                row.Wait.DisplayInMapper = on;
                row.Duration.DisplayInMapper = on;
                row.Hold.DisplayInMapper = on;
                row.Stop.DisplayInMapper = on;
                row.Loop.DisplayInMapper = on;
            }
        }

        /// <summary>
        /// Keeps the stock mapper in step with the row count and with whether the
        /// panel can draw at all.
        ///
        /// In Unity's own Update rather than in BuildingUpdate: the latter stops
        /// the instant a run starts, and this has to keep answering while one is
        /// going on. Reconciled rather than driven by an event, because none of the
        /// events that would drive it reach a building block.
        /// </summary>
        private void Update()
        {
            ShowStock(!Panel.Usable);
        }

        // ---- running ---------------------------------------------------------

        public override void OnSimulateStart()
        {
            // The one thing a run has to be told, rather than a reset: whether the
            // rows that follow the block start now. Everything else is already at
            // its default, this object having just been made.
            if (automatic != null && automatic.IsActive)
            {
                for (int i = 0; i < Count && i < rows.Count; i++)
                {
                    if (rows[i].Ready)
                    {
                        Clock.Start(rows[i]);
                    }
                }
            }
        }

        public override void KeyEmulationUpdate()
        {
            // SafeAwake builds no controls on a simulating client without physics,
            // and this hook still runs there with every field still null. Besiege's
            // own SteeringModuleBehaviour has the same hole.
            if (masterReader == null)
            {
                return;
            }
            masterReader.ReadEmulation();
        }

        public override void SimulateUpdateAlways()
        {
            // Before the early returns below: the display has to go back to its own
            // colour at the end of a blink whatever else this frame is doing.
            glow.Tick(this);

            if (masterReader == null || Time.timeScale == 0f)
            {
                return;
            }

            masterReader.Poll();
            if (automatic != null && automatic.IsActive)
            {
                // Started with the simulation; the key does nothing.
                return;
            }

            int count = Count;
            for (int i = 0; i < count && i < rows.Count; i++)
            {
                Row row = rows[i];
                if (!row.Ready)
                {
                    continue;
                }
                // One reader for the whole table: every row is started by the
                // block's own key, so they all see the same press on the same
                // frame.
                Clock.Pressed(row, masterReader.Pressed, masterReader.Held,
                              row.HoldToRun, row.CanStop, row.Loops);
            }
        }

        /// <summary>
        /// One emulation tick. This is where the seconds are counted, at the same
        /// 50 Hz and with the same rounding as Besiege's own timer.
        /// </summary>
        public override void SendKeyEmulationUpdateHost()
        {
            if (masterReader == null || Time.timeScale == 0f)
            {
                return;
            }
            int count = Count;
            for (int i = 0; i < count && i < rows.Count; i++)
            {
                Row row = rows[i];
                if (!row.Ready)
                {
                    continue;
                }
                Clock.Tick(row, row.WaitSeconds, row.PressSeconds, row.Loops);
                // Reconciled rather than driven from inside the clock: the clock
                // says what the row wants, and this is the one place that touches a
                // key, so a press cannot be raised twice or dropped once.
                Hold(row, row.Wants);
            }
        }

        /// <summary>
        /// Holds or lets go of a row's emulated key, through Besiege's own
        /// emulation rather than by writing to the key.
        ///
        /// <c>ownKeys</c> is the block's own activation key, which is what stops
        /// a row pressing a key that would start this same block --
        /// <c>KeyInputController.EmulateEntry</c> compares the target against that
        /// array by reference and refuses a match. Besiege's own timer passes its
        /// own activation key for exactly this reason.
        /// </summary>
        private void Hold(Row row, bool down)
        {
            if (row.Held == down || row.Emulate == null || ownKeys == null)
            {
                return;
            }
            row.Held = down;
            EmulateKeys(ownKeys, row.Emulate, down);
            if (down)
            {
                // A row firing is the one thing this block does that is worth
                // seeing from outside it.
                glow.Flash();
            }
        }

        /// <summary>
        /// Lets go of anything still held.
        ///
        /// <c>OnSimulateStop</c> is <c>OnDisable</c>, which is also what the game's
        /// own timer hangs its release on: a machine taken apart mid-press would
        /// otherwise leave the emulated key down, and an emulated key is reference
        /// counted, so nothing else would ever see it come up.
        /// </summary>
        public override void OnSimulateStop()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Hold(rows[i], false);
                Clock.Stop(rows[i]);
            }
            glow.Rest();
        }

        private void OnDestroy()
        {
            // The material and the texture are this block's own.
            glow.Undress();
        }

    }
}
