using System.Collections.Generic;
using Modding.Modules;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// The Timer Plus block: a table of Besiege's timers in one. The rows are the
    /// block's own data, kept as text in one control (<see cref="Table.Save"/>)
    /// rather than a pool of mapper controls: a row only presses keys, and a key
    /// made when a run starts presses as a registered one does. Each row runs
    /// <c>TimerBlock</c>'s own state machine (<see cref="Clock"/>), minus the
    /// network-ping phase (this hook never runs on a client without physics), with
    /// activation read once per frame (<see cref="KeyReader"/>).
    /// </summary>
    public class TimerPlusBehaviour : BlockModuleBehaviour<TimerPlusModule>
    {
        /// <summary>Rows per block. Nothing is built per row until a run, so this
        /// only keeps a save, and an edit sent over the network, a sensible
        /// size.</summary>
        public const int MaxRows = 1024;

        // ---- the block's own settings ----------------------------------------

        /// <summary>Starts every row. Defaults to the key Besiege's timer
        /// uses.</summary>
        private MKey masterKey;

        /// <summary>Start with the simulation instead of on the key.</summary>
        private MToggle automatic;

        /// <summary>Whether converting drops a hidden, unbound pin in each block,
        /// so the timers do not fall over. On by default.</summary>
        private MToggle pinBlocks;

        /// <summary>Every row, as text: saved, undone and synced as one control, and
        /// left out of the save while it is a new block's.</summary>
        private MText table;

        /// <summary>The rows read from <see cref="table"/>, and the text they were
        /// read from, so they are read again only when it changes -- an undo, a
        /// load, another player's edit.</summary>
        private List<RowData> data = new List<RowData>();
        private string parsed;

        private KeyReader masterReader;

        /// <summary>The block's activation key, handed to <c>EmulateKeys</c> so no
        /// row presses it; <c>KeyInputController.EmulateEntry</c> compares by
        /// reference.</summary>
        private MKey[] ownKeys;

        /// <summary>The block's display, which blinks red as a row fires.</summary>
        private readonly Glow glow = new Glow();

        /// <summary>What the mapper was last told to show: every
        /// <c>DisplayInMapper</c> write rebuilds its widgets.</summary>
        private bool shownStock;
        private bool stockShown;

        // ---- a run -----------------------------------------------------------

        /// <summary>The rows as the run started, each one's clock, and the key made
        /// for it to press with: never saved, and not a mapper control.</summary>
        private List<RowData> runs = new List<RowData>();
        private readonly List<Row> clocks = new List<Row>();
        private MKey[] pressing = new MKey[0];

        public override bool EmulatesAnyKeys
        {
            // Without this the modding API refuses to emulate anything and logs
            // "uses EmulateKeys but does not set EmulatesAnyKeys" once per press.
            get { return true; }
        }

        /// <summary>The rows, live: the panel edits these and then calls <see
        /// cref="Store"/>.</summary>
        public List<RowData> Data
        {
            get
            {
                Sync();
                return data;
            }
        }

        public int Count { get { return Data.Count; } }

        public MText TableControl { get { return table; } }

        public MKey MasterKey { get { return masterKey; } }
        public MToggle Automatic { get { return automatic; } }

        /// <summary>The pin switch itself, for the panel to draw, and what it says.
        /// </summary>
        public MToggle PinControl { get { return pinBlocks; } }

        public bool Pins { get { return pinBlocks != null && pinBlocks.IsActive; } }

        /// <summary>Makes a list the rows and writes the text. The caller commits
        /// <see cref="TableControl"/>.</summary>
        public void Store(List<RowData> rows)
        {
            data = rows == null ? new List<RowData>() : rows;
            string text = Table.Save(data);
            parsed = text;
            if (table != null)
            {
                table.Value = text;
            }
        }

        private void Sync()
        {
            string text = table == null ? null : table.Value;
            if (text == parsed)
            {
                return;
            }
            data = Table.Load(text);
            parsed = text;
        }

        // ---- setting up ------------------------------------------------------

        public override void SafeAwake()
        {
            // The block wears one mesh and one texture with nothing to swap to, so
            // the skin picker is an empty choice.
            Skins.Hide(BlockBehaviour);

            // The first of each pair is the word shown; the second is the save's
            // own key and stays English whatever the language is.
            masterKey = AddKey(Words.Of("mapper.activate"), "Activate", KeyCode.B);
            automatic = AddToggle(Words.Of("mapper.automatic"), "AutomaticKey", false);
            pinBlocks = AddToggle(Words.Of("mapper.pins"), "PinKey", true);
            if (!live.Contains(this))
            {
                live.Add(this);
            }

            // A new block's rows are the text's default, so a table left as it came
            // is not written into the save.
            int wanted = Mathf.Clamp(Module == null ? 1 : Module.Rows, 0, MaxRows);
            List<RowData> first = new List<RowData>();
            for (int i = 0; i < wanted; i++)
            {
                first.Add(new RowData());
            }
            table = BlockBehaviour.AddText("Timers", "TimersKey", Table.Save(first));

            masterReader = new KeyReader(masterKey);

            // A row may not press the key that starts its own block: a loop with no
            // frame.
            ownKeys = new MKey[] { masterKey };

            ShowStock(!Panel.Usable);
        }

        /// <summary>
        /// Shows or hides the pin switch in Besiege's mapper, which the panel draws
        /// while it can. The activation key and Automatic always stay: only the
        /// stock selector binds keys properly. The rows' text never shows: it is
        /// not a thing to type into. Written only when the answer changes, since
        /// every <c>DisplayInMapper</c> write rebuilds the mapper.
        /// </summary>
        public void ShowStock(bool visible)
        {
            if (stockShown && shownStock == visible)
            {
                return;
            }
            stockShown = true;
            shownStock = visible;
            if (pinBlocks != null) pinBlocks.DisplayInMapper = visible;
            if (table != null) table.DisplayInMapper = false;
        }

        /// <summary>Keeps the mapper in step. Unity's Update rather than
        /// BuildingUpdate, which stops when a run starts.</summary>
        private void Update()
        {
            ShowStock(!Panel.Usable);
        }

        // ---- running ---------------------------------------------------------

        /// <summary>Reads the rows once, and gives each a clock and a key to press
        /// with.</summary>
        public override void OnSimulateStart()
        {
            runs = Table.Snapshot(this);
            clocks.Clear();
            pressing = new MKey[runs.Count];
            // The one thing a run is told: whether rows started by the block start
            // now.
            bool now = automatic != null && automatic.IsActive;
            for (int i = 0; i < runs.Count; i++)
            {
                Row clock = new Row();
                clocks.Add(clock);
                pressing[i] = Pressing(runs[i], i);
                if (now)
                {
                    Clock.Start(clock);
                }
            }
        }

        /// <summary>A key for one row to press with, bound as the row is.</summary>
        private static MKey Pressing(RowData row, int index)
        {
            MKey key = new MKey("Emulate " + (index + 1), "RunEmu" + index,
                                KeyCode.None, true);
            if (!string.IsNullOrEmpty(row.EmulateVariable))
            {
                Bindings.BindVariable(key, row.EmulateVariable);
            }
            else
            {
                Bindings.Bind(key, row.EmulateKeys);
            }
            return key;
        }

        public override void KeyEmulationUpdate()
        {
            // A client without physics builds no controls, but this hook still runs
            // there.
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

            for (int i = 0; i < clocks.Count && i < runs.Count; i++)
            {
                RowData row = runs[i];
                // One reader for the table: every row sees the block's press on the
                // same frame.
                Clock.Pressed(clocks[i], masterReader.Pressed, masterReader.Held,
                              row.Hold, row.Stop, row.Loop);
            }
        }

        /// <summary>One emulation tick: seconds counted at 50 Hz, rounded as
        /// Besiege's timer rounds them.</summary>
        public override void SendKeyEmulationUpdateHost()
        {
            if (masterReader == null || Time.timeScale == 0f)
            {
                return;
            }
            for (int i = 0; i < clocks.Count && i < runs.Count; i++)
            {
                RowData row = runs[i];
                Clock.Tick(clocks[i], row.Wait, row.Duration, row.Loop);
                // The clock says what the row wants; this is the one place a key is
                // pressed.
                Hold(i, clocks[i].Wants);
            }
        }

        /// <summary>Holds or releases a row's key through Besiege's emulation.
        /// <c>ownKeys</c> stops a row pressing the block's own activation
        /// key.</summary>
        private void Hold(int index, bool down)
        {
            Row clock = clocks[index];
            if (clock.Held == down || ownKeys == null || index >= pressing.Length
                || pressing[index] == null)
            {
                return;
            }
            clock.Held = down;
            try
            {
                EmulateKeys(ownKeys, pressing[index], down);
            }
            catch (KeyNotFoundException)
            {
                // Besiege looks the keycode or name up among the keys using it and
                // throws when there are none: nothing would have heard the press.
            }
            if (down)
            {
                // A row firing is the one thing this block does that is worth
                // seeing from outside it.
                glow.Flash();
            }
        }

        /// <summary>Lets go of anything held. Emulated keys are reference counted,
        /// so a key left down would never come up.</summary>
        public override void OnSimulateStop()
        {
            for (int i = 0; i < clocks.Count; i++)
            {
                Hold(i, false);
                Clock.Stop(clocks[i]);
            }
            glow.Rest();
        }

        private void OnDestroy()
        {
            // The material and the texture are this block's own.
            glow.Undress();
            live.Remove(this);
        }

        /// <summary>Every Timer Plus on the machine, so a language change can put
        /// the new words on their mapper controls. Besiege's own mapper listens for
        /// a name changing, so an open one takes them up as they are set.</summary>
        private static readonly List<TimerPlusBehaviour> live =
            new List<TimerPlusBehaviour>();

        public static void Retitle()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i] == null)
                {
                    live.RemoveAt(i);
                    continue;
                }
                live[i].Titled();
            }
        }

        private void Titled()
        {
            if (masterKey != null)
            {
                masterKey.DisplayName = Words.Of("mapper.activate");
            }
            if (automatic != null)
            {
                automatic.DisplayName = Words.Of("mapper.automatic");
            }
            if (pinBlocks != null)
            {
                pinBlocks.DisplayName = Words.Of("mapper.pins");
            }
        }
    }
}
