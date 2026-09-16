using System.Collections.Generic;
using Modding.Modules;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// The Computer block: up to <see cref="MaxRows"/> of Besiege's logic gates
    /// and timers in one, each row the game's own gate (<see cref="Gates"/>) with
    /// the same settings. No activation of its own, and no burnout -- that belongs
    /// to a gate on the machine, not a row of a table.
    ///
    /// The rows are the block's own data, kept as text in one control (<see
    /// cref="LogicTable.Save"/>). A row still holds Besiege's control objects --
    /// keys, a menu, toggles, sliders -- so whatever edits a row edits it as it
    /// always did, but none is registered with the block: a run files each row's
    /// inputs with the machine's key controller by hand (<see cref="Listen"/>), and
    /// a row presses through its own key.
    /// </summary>
    public class ComputerBehaviour : BlockModuleBehaviour<ComputerModule>
    {
        /// <summary>Rows per block. Nothing is registered per row, so this only
        /// keeps a save, a network edit and the board a sensible size.</summary>
        public const int MaxRows = 1024;

        /// <summary>Whether converting also drops a pin inside each gate it makes.
        /// See <see cref="TimerPlusBehaviour.PinControl"/>.</summary>
        private MToggle pinBlocks;

        /// <summary>The node editor's layout: row positions and the input, output
        /// and comment nodes. A control, so the board saves with the
        /// machine.</summary>
        private MText layout;

        /// <summary>What generated wire names start with.</summary>
        private MText prefix;

        /// <summary>Every row, as text: saved, undone and synced as one control, and
        /// left out of the save while it is a new block's.</summary>
        private MText gates;

        private readonly List<LogicRow> rows = new List<LogicRow>();

        /// <summary>The text <see cref="rows"/> were read from or last written as,
        /// so they are read again only when it changes: an undo, a load, another
        /// player's edit.</summary>
        private string parsed;

        /// <summary>
        /// Each row's own two inputs, handed to Besiege when that row presses:
        /// <c>KeyInputController.Emulate</c> skips them (by reference), which stops
        /// a gate driving itself. Only the row's own: passing every row's inputs
        /// made every wire between two gates of this block dead in a run.
        /// </summary>
        private MKey[][] ownKeys = new MKey[0][];

        /// <summary>Whether this run's inputs are filed with a key controller: a key
        /// without one cannot be read.</summary>
        private bool listened;

        private bool shownStock;
        private bool stockShown;

        /// <summary>The rows, live. Whatever changes one commits through <see
        /// cref="LogicTable.Settle"/>, which writes them back.</summary>
        public List<LogicRow> Rows
        {
            get
            {
                Sync();
                return rows;
            }
        }

        public MText RowsControl { get { return gates; } }

        public MToggle PinControl { get { return pinBlocks; } }

        public MText LayoutControl { get { return layout; } }

        public MText PrefixControl { get { return prefix; } }

        /// <summary>What generated wire names start with: whatever was typed, or
        /// `ne_` and the block's number.</summary>
        public string Prefix
        {
            get
            {
                string typed = prefix == null ? "" : prefix.Value;
                if (!string.IsNullOrEmpty(typed))
                {
                    return typed;
                }
                // Not claimed yet: worked out once and kept, so names minted before
                // the block is opened match the prefix it is then given.
                if (unclaimed == null)
                {
                    unclaimed = Numbered(FindObjectsOfType<ComputerBehaviour>());
                }
                return unclaimed;
            }
            set
            {
                if (prefix == null)
                {
                    return;
                }
                string typed = value == null ? "" : value.Trim();
                // Room for the wire number inside Besiege's limit on a name's
                // length.
                int room = Bindings.NameLimit - WireLetters;
                if (typed.Length > room)
                {
                    typed = typed.Substring(0, room);
                }
                prefix.Value = typed;
            }
        }

        /// <summary>The prefix this block will be given, worked out before it has
        /// been opened and written down. See <see cref="Prefix"/>.</summary>
        private string unclaimed;

        /// <summary>
        /// The digits of generated numbers: base 36, lower case; three count to
        /// 46,655. Lower case only: Besiege matches names exactly
        /// (`KeyInputController.usedMessages` is a plain string `Dictionary`), and
        /// mixed case gets retyped wrong.
        /// </summary>
        public const string NameLetters = "0123456789abcdefghijklmnopqrstuvwxyz";

        /// <summary>How many of those a block's number carries in its prefix, and a
        /// wire's number after the prefix.</summary>
        public const int PrefixLetters = 3;
        public const int WireLetters = 3;

        /// <summary>The largest number that many letters spell.</summary>
        public static int Most(int letters)
        {
            int most = 1;
            for (int i = 0; i < letters; i++)
            {
                most *= NameLetters.Length;
            }
            return most - 1;
        }

        /// <summary>A number spelt in <see cref="NameLetters"/>, that many letters
        /// long: 1 is `001`, 10 is `00a`, 36 is `010`.</summary>
        public static string Counted(int value, int letters)
        {
            char[] spelt = new char[letters];
            int radix = NameLetters.Length;
            for (int i = letters - 1; i >= 0; i--)
            {
                spelt[i] = NameLetters[value % radix];
                value /= radix;
            }
            return new string(spelt);
        }

        /// <summary>The default prefix: `ne_`, the lowest block number on the
        /// machine not in use, then `_` -- `ne_001_`. Names are machine-wide, so no
        /// two blocks may share one.</summary>
        private string Numbered(ComputerBehaviour[] all)
        {
            int most = Most(PrefixLetters);
            for (int n = 1; n <= most; n++)
            {
                string wanted = "ne_" + Counted(n, PrefixLetters) + "_";
                if (!Elsewhere(all, wanted))
                {
                    return wanted;
                }
            }
            return "ne_" + Counted(0, PrefixLetters) + "_";
        }

        /// <summary>Whether another Computer block on the machine is already
        /// using this prefix.</summary>
        private bool Elsewhere(ComputerBehaviour[] all, string wanted)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i] == this || all[i].prefix == null)
                {
                    continue;
                }
                if (all[i].prefix.Value == wanted)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Writes the prefix down when the block is opened, so it stays:
        /// an unwritten one can change, and a copied block would share its
        /// original's.
        /// </summary>
        public MText Claim()
        {
            if (prefix == null)
            {
                return null;
            }
            ComputerBehaviour[] all = FindObjectsOfType<ComputerBehaviour>();
            string mine = prefix.Value;
            if (!string.IsNullOrEmpty(mine) && !Elsewhere(all, mine))
            {
                return null;                // typed or claimed, and nobody else's
            }
            // The one already worked out, if nobody has taken it since: names may
            // have been minted with it.
            prefix.Value = unclaimed != null && !Elsewhere(all, unclaimed)
                ? unclaimed : Numbered(all);
            unclaimed = null;
            return prefix;
        }

        public bool Pins { get { return pinBlocks != null && pinBlocks.IsActive; } }

        /// <summary>How many rows there are. Setting it adds default rows at the end
        /// or takes rows off it.</summary>
        public int Count
        {
            get
            {
                return Rows.Count;
            }
            set
            {
                List<LogicRow> all = Rows;
                int wanted = Mathf.Clamp(value, 0, MaxRows);
                if (all.Count > wanted)
                {
                    all.RemoveRange(wanted, all.Count - wanted);
                }
                while (all.Count < wanted)
                {
                    all.Add(Made(new GateData()));
                }
            }
        }

        /// <summary>Writes the rows into their text if they changed since it was
        /// read or last written; true if they did.</summary>
        public bool Stored()
        {
            if (gates == null)
            {
                return false;
            }
            string text = LogicTable.Save(LogicTable.Snapshot(this));
            if (text == parsed)
            {
                return false;
            }
            parsed = text;
            gates.Value = text;
            return true;
        }

        private void Sync()
        {
            string text = gates == null ? null : gates.Value;
            if (text == parsed)
            {
                return;
            }
            parsed = text;
            rows.Clear();
            List<GateData> read = LogicTable.Load(text);
            for (int i = 0; i < read.Count; i++)
            {
                rows.Add(Made(read[i]));
            }
        }

        // ---- setting up ------------------------------------------------------

        public override void SafeAwake()
        {
            // One mesh, one texture, nothing to swap to.
            Skins.Hide(BlockBehaviour);

            // The word shown; "PinKey" is the save's own and stays English.
            pinBlocks = AddToggle(Words.Of("mapper.pins"), "PinKey", true);

            // Edited in the node editor rather than the mapper: none of these is
            // something to type into a text box.
            layout = BlockBehaviour.AddText("Layout", "LayoutKey", "");
            layout.DisplayInMapper = false;
            prefix = BlockBehaviour.AddText("Prefix", "PrefixKey", "");
            prefix.DisplayInMapper = false;

            // With START EMPTY on in the node editor, a block arrives with nothing
            // on its board: no rows, and so no ends either, since the ends are made
            // from what the rows read and press.
            int wanted = Editor.StartEmpty
                ? 0 : Mathf.Clamp(Module == null ? 3 : Module.Rows, 0, MaxRows);
            gates = BlockBehaviour.AddText("Gates", "GatesKey",
                                           LogicTable.Save(FirstRows(wanted)));
            gates.DisplayInMapper = false;

            ShowStock(!Panel.Serving(this));
            if (!live.Contains(this))
            {
                live.Add(this);
            }
        }

        /// <summary>Every Computer on the machine, so a language change can put the
        /// new words on their mapper controls. Besiege's own mapper listens for a
        /// name changing, so an open one takes them up as they are set.</summary>
        private static readonly List<ComputerBehaviour> live =
            new List<ComputerBehaviour>();

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
            if (pinBlocks != null)
            {
                pinBlocks.DisplayName = Words.Of("mapper.pins");
            }
        }

        private void OnDestroy()
        {
            live.Remove(this);
        }

        /// <summary>A fresh block's first three rows: two gates and a counter, one
        /// answering on a name and one on a key. Later rows take the game's
        /// defaults: U and I in, C out.</summary>
        private static readonly KeyCode[] FirstA =
            { KeyCode.J, KeyCode.K, KeyCode.L };
        private static readonly KeyCode[] FirstB =
            { KeyCode.I, KeyCode.O, KeyCode.M };
        private static readonly int[] FirstGate =
            { Gates.And, Gates.Xor, Gates.Counter };
        private static readonly bool[] FirstMode = { false, true, false };
        private static readonly KeyCode[] FirstOut =
            { KeyCode.C, KeyCode.P, KeyCode.C };

        /// <summary>Which first rows answer on a name.</summary>
        private static readonly string[] FirstNamed = { "var_1", null, "var_2" };

        private static List<GateData> FirstRows(int wanted)
        {
            List<GateData> all = new List<GateData>();
            for (int i = 0; i < wanted; i++)
            {
                GateData row = new GateData();
                row.Gate = Gates.Not;
                if (i < FirstA.Length)
                {
                    row.AKeys = new KeyCode[] { FirstA[i] };
                    row.BKeys = new KeyCode[] { FirstB[i] };
                    row.Gate = FirstGate[i];
                    row.Mode = FirstMode[i];
                    row.EmulateKeys = new KeyCode[] { FirstOut[i] };
                    if (FirstNamed[i] != null)
                    {
                        row.EmulateVariable = FirstNamed[i];
                        row.EmulateKeys = new KeyCode[0];
                    }
                }
                all.Add(row);
            }
            return all;
        }

        /// <summary>The gate menu's entries, shared by every row's menu.</summary>
        private static List<string> gateNames;

        /// <summary>A row with its control objects -- made, not registered: see the
        /// class summary -- holding these settings.</summary>
        private static LogicRow Made(GateData data)
        {
            if (gateNames == null)
            {
                gateNames = new List<string>();
                for (int g = 0; g < Gates.Kinds; g++)
                {
                    gateNames.Add(Gates.Names[g]);
                }
            }
            LogicRow row = new LogicRow();
            row.InputA = Key("Input A", "A", false);
            row.InputB = Key("Input B", "B", false);
            row.Kind = new MMenu("Gate", 0, gateNames, false);
            row.Mode = new MToggle("Mode", "Mode", false);
            row.Emulate = Key("Emulate", "Out", true);
            row.Wait = Seconds("Wait");
            row.Duration = Seconds("Duration");
            row.Hold = new MToggle("Hold to run", "Hold", false);
            row.Stop = new MToggle("Allow stop", "Stop", false);
            row.Loop = new MToggle("Loop", "Loop", false);
            row.Timing = new Row();
            row.Timing.Emulate = row.Emulate;
            row.Timing.Wait = row.Wait;
            row.Timing.Duration = row.Duration;
            row.Timing.Hold = row.Hold;
            row.Timing.Stop = row.Stop;
            row.Timing.Loop = row.Loop;
            LogicTable.Write(row, data, null);
            row.ReadA = new KeyReader(row.InputA);
            row.ReadB = new KeyReader(row.InputB);
            return row;
        }

        private static MKey Key(string name, string key, bool emulator)
        {
            MKey made = new MKey(name, key, KeyCode.None, emulator);
            made.isEmulator = emulator;
            return made;
        }

        /// <summary>Seconds from nought, unclamped above sixty, as the game's
        /// timer.</summary>
        private static MSlider Seconds(string name)
        {
            return new MSlider(name, name, 1f, 0f, 60f, "", "s", true, false, false);
        }

        /// <summary>Shows or hides the pin switch in Besiege's mapper, which the
        /// panel draws while it can; the rows, the layout and the prefix never
        /// show. Written only when the answer changes, since every
        /// <c>DisplayInMapper</c> write rebuilds the mapper.</summary>
        public void ShowStock(bool visible)
        {
            if (stockShown && shownStock == visible)
            {
                return;
            }
            stockShown = true;
            shownStock = visible;
            if (pinBlocks != null)
            {
                pinBlocks.DisplayInMapper = visible;
            }
        }

        private void Update()
        {
            ShowStock(!Panel.Serving(this));
        }

        // ---- running ---------------------------------------------------------

        /// <summary>Starts timer rows with nothing on their input, as Besiege's
        /// timer with Automatic does, and files every row's inputs. Everything
        /// else is fresh on the simulation clone.</summary>
        public override void OnSimulateStart()
        {
            List<LogicRow> all = Rows;
            ownKeys = new MKey[all.Count][];
            for (int i = 0; i < all.Count; i++)
            {
                ownKeys[i] = new MKey[] { all[i].InputA, all[i].InputB };
                if (all[i].Automatic)
                {
                    Clock.Start(all[i].Timing);
                }
            }
            Listen();
        }

        /// <summary>Files every row's two inputs with the machine's key controller,
        /// as `Machine.InitSimBlock` files a block's registered keys, which these
        /// are not.</summary>
        private void Listen()
        {
            listened = false;
            KeyInputController controller = null;
            try
            {
                Machine machine = BlockBehaviour.ParentMachine;
                if (machine != null)
                {
                    // `Machine.Awake` adds one; a networked player's is added after
                    // it, so the last is the one in use.
                    KeyInputController[] all =
                        machine.GetComponents<KeyInputController>();
                    controller = all.Length > 0 ? all[all.Length - 1] : null;
                }
            }
            catch (System.Exception e)
            {
                Log.Warn("could not find the machine's key controller: " + e.Message);
            }
            if (controller == null)
            {
                Log.Warn("no key controller on the machine, so no row can read its "
                         + "inputs this run.");
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                File(rows[i].InputA, controller);
                File(rows[i].InputB, controller);
            }
            listened = true;
        }

        /// <summary>One key as a listener: its controller set, then `AddMKey` and
        /// `Add` for each keycode it holds.</summary>
        private void File(MKey key, KeyInputController controller)
        {
            key.SetInputController(controller);
            for (int k = 0; k < key.KeysCount; k++)
            {
                controller.AddMKey(BlockBehaviour, key, key.GetKey(k));
                controller.Add(key.GetKey(k));
            }
        }

        /// <summary>Keyboard edges once a frame, as Besiege's gate reads them, so a
        /// press and release inside one tick are not lost.</summary>
        public override void SimulateUpdateAlways()
        {
            if (!listened || Time.timeScale == 0f)
            {
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                if (row.IsTimer)
                {
                    if (row.Automatic)
                    {
                        // Started with the simulation: its key does nothing, and a
                        // hold-to-run would stop it at once.
                        continue;
                    }
                    // A timer row starts on input A's merged edges, taken here once
                    // a frame.
                    row.ReadA.Poll();
                    Clock.Pressed(row.Timing, row.ReadA.Pressed, row.ReadA.Held,
                                  row.Timing.HoldToRun, row.Timing.CanStop,
                                  row.Timing.Loops);
                    continue;
                }
                // The game's own `UpdateBlock`: held is pressed or held, emulation
                // ORed in later, and a release only when no code is held.
                bool pressedA = row.ReadA.RealPressed;
                bool pressedB = row.ReadB.RealPressed;
                Gates.Advance(row, row.Gate, row.Switch, pressedA, pressedB,
                              Gates.Holding(pressedA, row.ReadA.RealHeld,
                                            row.ReadA.EmulatedHeld),
                              Gates.Holding(pressedB, row.ReadB.RealHeld,
                                            row.ReadB.EmulatedHeld),
                              Gates.Letting(pressedA, row.ReadA.RealHeld,
                                            row.ReadA.RealReleased));
            }
        }

        public override void KeyEmulationUpdate()
        {
            // A client without physics has no rows filed, and this hook still runs
            // there.
            if (!listened)
            {
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].ReadA.ReadEmulation();
                rows[i].ReadB.ReadEmulation();
            }
        }

        /// <summary>One emulation tick: emulated edges, then the answer. The game's
        /// `EmulationUpdateBlock`, then `SendEmulationUpdateBlock`.</summary>
        public override void SendKeyEmulationUpdateHost()
        {
            if (!listened || Time.timeScale == 0f)
            {
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                if (row.IsTimer)
                {
                    // Its seconds are counted here, at Besiege's 50 Hz and
                    // rounding.
                    Clock.Tick(row.Timing, row.Timing.WaitSeconds,
                               row.Timing.PressSeconds, row.Timing.Loops);
                    Hold(row, i, row.Timing.Wants);
                    continue;
                }
                bool pressedA = row.ReadA.TakePress();
                bool pressedB = row.ReadB.TakePress();
                bool releasedA = row.ReadA.TakeRelease();
                row.ReadB.TakeRelease();
                // The game's `EmulationUpdateBlock`: emulated edges, keyboard held
                // ORed in, no release filter.
                Gates.Advance(row, row.Gate, row.Switch, pressedA, pressedB,
                              Gates.Holding(row.ReadA.RealPressed,
                                            row.ReadA.RealHeld,
                                            row.ReadA.EmulatedHeld),
                              Gates.Holding(row.ReadB.RealPressed,
                                            row.ReadB.RealHeld,
                                            row.ReadB.EmulatedHeld),
                              releasedA);
                Hold(row, i, Gates.Answer(row, row.Gate));
            }
        }

        public override bool EmulatesAnyKeys { get { return true; } }

        /// <summary>Holds or releases a row's emulated key: the one place a key is
        /// touched.</summary>
        private void Hold(LogicRow row, int index, bool down)
        {
            if (row.Held == down || row.Emulate == null || index < 0
                || index >= ownKeys.Length)
            {
                return;
            }
            row.Held = down;
            try
            {
                EmulateKeys(ownKeys[index], row.Emulate, down);
            }
            catch (KeyNotFoundException)
            {
                // Besiege looks the keycode or name up among the keys using it and
                // throws when there are none: nothing would have heard the press.
            }
        }

        /// <summary>Lets go of anything held and resets gate memory. OnSimulateStop
        /// is OnDisable, where the game's own block lets go too.</summary>
        public override void OnSimulateStop()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                Hold(row, i, false);
                row.A = false;
                row.B = false;
                row.AToggled = false;
                row.BToggled = false;
                row.Count = 0;
                row.LastCount = 0;
                if (row.Timing != null)
                {
                    Clock.Stop(row.Timing);
                }
            }
        }
    }
}
