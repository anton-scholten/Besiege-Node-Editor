using System.Collections.Generic;
using Modding.Modules;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// The Logic Gate Plus block: up to <see cref="MaxRows"/> of Besiege's logic
    /// gates in one, each row with the same settings the game's own gate has.
    ///
    /// The gate itself is Besiege's, read out of <c>Assembly-CSharp</c> and kept
    /// in <see cref="Gates"/> apart from the game, so a row behaves exactly as the
    /// block it stands in for and a converted machine runs the same as the one
    /// that made it.
    ///
    /// The block has no activation of its own -- a gate has inputs, not a start --
    /// so the mapper above the panel holds nothing and every row names its own A
    /// and B.
    ///
    /// One deliberate difference from the game's block: no burnout. Besiege can
    /// burn a gate out for pulsing too fast, with sparks and a sound, because a
    /// gate is a thing on the machine that can be broken. A row of a table is not
    /// one, and thirty-two of them sharing one block's burnout would be a rule
    /// nobody could see the shape of.
    /// </summary>
    public class LogicGatePlusBehaviour : BlockModuleBehaviour<LogicGatePlusModule>
    {
        /// <summary>How many rows a block can hold. Every row's ten controls
        /// exist from <c>SafeAwake</c> whether the row is in use or not. Lowering
        /// this orphans the controls of any row above the new cap, and a machine
        /// saved with those rows loses them silently.</summary>
        public const int MaxRows = 32;

        /// <summary>How many rows are in use, 0..<see cref="MaxRows"/>. A block
        /// with none is a block whose gates have all been taken off it -- there is
        /// nothing a row has to be kept for, and one kept back is a gate somebody
        /// has to notice and delete somewhere else.</summary>
        private MSlider rowCount;

        /// <summary>Whether converting also drops a pin inside each gate it makes.
        /// See <see cref="TimerPlusBehaviour.PinControl"/>.</summary>
        private MToggle pinBlocks;

        /// <summary>Where the node editor puts things: the rows' places on the
        /// board, and the input and output nodes, which are not rows. A control
        /// like everything else, so a board is saved with the machine.</summary>
        private MText layout;

        /// <summary>What generated wire names start with.</summary>
        private MText prefix;

        private readonly List<LogicRow> rows = new List<LogicRow>();

        /// <summary>
        /// Each row's own two input keys, which is what Besiege is handed when that
        /// row presses its answer.
        ///
        /// <c>KeyInputController.Emulate</c> skips every key in this array --
        /// <c>EmulateEntry</c> returns false for anything it finds by reference --
        /// so it is what stops a gate driving itself. Besiege's own gate passes its
        /// own two and nothing else, and so must a row: handing over *every* row's
        /// inputs, which is what this block used to do, told the game to skip them
        /// all, and a row could not drive another row in the same block. Every wire
        /// the board drew between two of its own gates was dead the moment the
        /// machine ran.
        ///
        /// Built once and kept: the comparison is by reference, and the references
        /// do not change.
        /// </summary>
        private MKey[][] ownKeys;

        public IList<LogicRow> Rows { get { return rows; } }

        public MSlider CountControl { get { return rowCount; } }

        public MToggle PinControl { get { return pinBlocks; } }

        public MText LayoutControl { get { return layout; } }

        public MText PrefixControl { get { return prefix; } }

        /// <summary>
        /// What generated wire names start with: whatever was typed, or something
        /// made from the block's own number.
        ///
        /// A wire name is a machine-wide thing -- anything on the machine can read
        /// it -- so a name that says what the block is for beats one that says
        /// which object it happens to be.
        /// </summary>
        public string Prefix
        {
            get
            {
                string typed = prefix == null ? "" : prefix.Value;
                return string.IsNullOrEmpty(typed) ? Numbered() : typed;
            }
            set
            {
                if (prefix == null)
                {
                    return;
                }
                string typed = value == null ? "" : value.Trim();
                // Room for the two digits a generated name ends with, inside
                // Besiege's own limit on the length of one: a name the game cannot
                // spell is a name its own mapper cannot edit.
                int room = Bindings.NameLimit - 2;
                if (typed.Length > room)
                {
                    typed = typed.Substring(0, room);
                }
                prefix.Value = typed;
            }
        }

        /// <summary>
        /// What this block's generated names start with when nobody has typed
        /// anything: `ne` and the block's own number on the machine, two digits and
        /// an underscore -- `ne01_` for the first one placed.
        ///
        /// The number is the lowest nothing else is using, so a second block gets
        /// `ne02_` and a third that fills a gap left by a deleted one takes the
        /// gap. Names are machine-wide, and two blocks generating `gate3_0` between
        /// them is two circuits quietly driving each other.
        /// </summary>
        private string Numbered()
        {
            // One look at the machine, not one per number tried: finding every
            // block of a kind walks everything loaded, and doing it a hundred times
            // to answer one question is a hundred times too many.
            LogicGatePlusBehaviour[] all =
                FindObjectsOfType<LogicGatePlusBehaviour>();
            for (int n = 1; n < 100; n++)
            {
                string wanted = "ne" + n.ToString("00") + "_";
                if (!Elsewhere(all, wanted))
                {
                    return wanted;
                }
            }
            return "ne00_";
        }

        /// <summary>Whether another Logic Gate Plus block on the machine is already
        /// using this prefix.</summary>
        private bool Elsewhere(LogicGatePlusBehaviour[] all, string wanted)
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

        /// <summary>
        /// Writes the block's number into its prefix, so it stays that number.
        ///
        /// Called when the block is opened. Until it is written the prefix is
        /// worked out afresh every time it is asked for, which means a block whose
        /// neighbour is deleted quietly changes what its next name will be -- and a
        /// block copied from another arrives with the same prefix, which is two
        /// blocks generating the same names.
        /// </summary>
        public MText Claim()
        {
            if (prefix == null)
            {
                return null;
            }
            string mine = prefix.Value;
            if (!string.IsNullOrEmpty(mine)
                && !Elsewhere(FindObjectsOfType<LogicGatePlusBehaviour>(), mine))
            {
                return null;                // typed or claimed, and nobody else's
            }
            prefix.Value = Numbered();
            return prefix;
        }

        public bool Pins { get { return pinBlocks != null && pinBlocks.IsActive; } }

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

        // ---- setting up ------------------------------------------------------

        public override void SafeAwake()
        {
            // One mesh, one texture, nothing to swap to.
            Skins.Hide(BlockBehaviour);

            pinBlocks = AddToggle("Pin blocks", "PinKey", true);

            // Both are edited in the node editor rather than the mapper: a board is
            // not something to type into a text box.
            layout = BlockBehaviour.AddText("Layout", "LayoutKey", "");
            layout.DisplayInMapper = false;
            prefix = BlockBehaviour.AddText("Prefix", "PrefixKey", "");
            prefix.DisplayInMapper = false;

            int wanted = Module == null ? 3 : Module.Rows;
            rowCount = BlockBehaviour.AddSlider("Rows", "RowsKey",
                Mathf.Clamp(wanted, 0, MaxRows), 0f, MaxRows, "", "");

            rows.Clear();
            for (int i = 0; i < MaxRows; i++)
            {
                rows.Add(Build(i));
            }

            // A row's own two inputs, so that row cannot press a key that drives
            // itself -- and every other row can be driven by it, which is what a
            // wire between two gates on the board is.
            ownKeys = new MKey[MaxRows][];
            for (int i = 0; i < MaxRows; i++)
            {
                ownKeys[i] = new MKey[] { rows[i].InputA, rows[i].InputB };
            }

            ShowStock(true);
        }

        /// <summary>
        /// What the three rows a fresh block starts with are set to.
        ///
        /// A block that arrives holding a circuit says what it is for in one look
        /// -- two gates and a counter, each on its own keys, one answering on a
        /// name and one on a key. Row four onwards is the game's own default for a
        /// logic gate: U and I in, C out, and the gate its menu opens on.
        /// </summary>
        private static readonly KeyCode[] FirstA =
            { KeyCode.J, KeyCode.K, KeyCode.L };
        private static readonly KeyCode[] FirstB =
            { KeyCode.I, KeyCode.O, KeyCode.M };
        private static readonly int[] FirstGate =
            { Gates.And, Gates.Xor, Gates.Counter };
        private static readonly bool[] FirstMode = { false, true, false };
        private static readonly KeyCode[] FirstOut =
            { KeyCode.C, KeyCode.P, KeyCode.C };

        /// <summary>And the ones answering on a name rather than a key. Set after
        /// the key is made rather than being its default: a Besiege key's default
        /// is a keycode, and a name is a value it is put to.</summary>
        private static readonly string[] FirstNamed = { "var_1", null, "var_2" };

        private LogicRow Build(int index)
        {
            string n = index.ToString();
            bool first = index < FirstA.Length;
            LogicRow row = new LogicRow();

            row.InputA = AddKey("Input A " + (index + 1), "A" + n,
                                first ? FirstA[index] : KeyCode.U);
            row.InputB = AddKey("Input B " + (index + 1), "B" + n,
                                first ? FirstB[index] : KeyCode.I);

            List<string> gates = new List<string>();
            // The gates, and the timer after them.
            for (int g = 0; g < Gates.Kinds; g++)
            {
                gates.Add(Gates.Names[g]);
            }
            row.Kind = BlockBehaviour.AddMenu("Gate" + n,
                                              first ? FirstGate[index] : Gates.Not,
                                              gates);

            // One switch where Besiege has two. Which of the two it is depends on
            // the gate -- see LogicRow.Mode -- and the game shows one at a time
            // for the same reason.
            row.Mode = AddToggle("Mode " + (index + 1), "Mode" + n,
                                 first && FirstMode[index]);
            row.Emulate = AddEmulatorKey("Emulate " + (index + 1), "Out" + n,
                                         first ? FirstOut[index] : KeyCode.C);
            if (first && FirstNamed[index] != null)
            {
                Bindings.BindVariable(row.Emulate, FirstNamed[index]);
            }

            // What the row is set to as a timer, declared exactly as the Timer Plus
            // block declares its own -- unclamped seconds, 0..60 -- and under keys
            // of their own: new ones, so no save that exists means anything by them.
            row.Wait = BlockBehaviour.AddSliderUnclamped(
                "Wait " + (index + 1), "Wait" + n, 1f, 0f, 60f, "", "s", true);
            row.Duration = BlockBehaviour.AddSliderUnclamped(
                "Duration " + (index + 1), "Dur" + n, 1f, 0f, 60f, "", "s", true);
            row.Hold = AddToggle("Hold to run " + (index + 1), "Hold" + n, false);
            row.Stop = AddToggle("Allow stop " + (index + 1), "Stop" + n, false);
            row.Loop = AddToggle("Loop " + (index + 1), "Loop" + n, false);
            row.Timing = new Row();
            row.Timing.Emulate = row.Emulate;
            row.Timing.Wait = row.Wait;
            row.Timing.Duration = row.Duration;
            row.Timing.Hold = row.Hold;
            row.Timing.Stop = row.Stop;
            row.Timing.Loop = row.Loop;

            row.ReadA = new KeyReader(row.InputA);
            row.ReadB = new KeyReader(row.InputB);
            return row;
        }

        /// <summary>
        /// Shows or hides the rows in Besiege's own mapper.
        ///
        /// Rows above the count stay hidden either way: a hundred and sixty
        /// controls for three rows of settings is not a mapper anybody can use.
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

            if (rowCount != null)
            {
                rowCount.DisplayInMapper = visible;
            }
            if (pinBlocks != null)
            {
                pinBlocks.DisplayInMapper = visible;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = visible && i < count;
                LogicRow row = rows[i];
                if (!row.Ready)
                {
                    continue;
                }
                bool timed = row.IsTimer;
                row.InputA.DisplayInMapper = on;
                // The gates that read one input take the second off the mapper,
                // exactly as the game's own block does -- and a timer reads one.
                row.InputB.DisplayInMapper = on && Gates.UsesB(row.Gate);
                row.Kind.DisplayInMapper = on;
                row.Mode.DisplayInMapper = on && !timed;
                row.Emulate.DisplayInMapper = on;
                row.Wait.DisplayInMapper = on && timed;
                row.Duration.DisplayInMapper = on && timed;
                row.Hold.DisplayInMapper = on && timed;
                row.Stop.DisplayInMapper = on && timed;
                row.Loop.DisplayInMapper = on && timed;
            }
        }

        private bool shownStock;
        private int shownRows = -1;

        /// <summary>Reconciled rather than driven by an event: none of the events
        /// that would drive it reach a building block, and the gate a row is set
        /// to decides whether its B key is shown at all.</summary>
        private void Update()
        {
            shownRows = -1;              // the gate may have changed under us
            ShowStock(!Panel.Serving(this));
        }

        // ---- running ---------------------------------------------------------

        /// <summary>
        /// Starts every timer row that starts itself -- nothing on its input -- as
        /// Besiege's own timer with Automatic on starts. The one thing a
        /// run has to be told rather than reset: this object is made afresh for it,
        /// so everything else is already at its default.
        /// </summary>
        public override void OnSimulateStart()
        {
            int count = Count;
            for (int i = 0; i < count && i < rows.Count; i++)
            {
                if (rows[i].Ready && rows[i].Automatic)
                {
                    Clock.Start(rows[i].Timing);
                }
            }
        }

        /// <summary>
        /// The keyboard's own edges, once a frame. Besiege's gate runs its state
        /// machine here as well as on the emulation tick, so a press and a release
        /// inside one tick are not lost.
        /// </summary>
        public override void SimulateUpdateAlways()
        {
            if (Time.timeScale == 0f)
            {
                return;
            }
            int count = Count;
            for (int i = 0; i < count && i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                if (!row.Ready || row.ReadA == null || row.ReadB == null)
                {
                    continue;
                }
                if (row.IsTimer)
                {
                    if (row.Automatic)
                    {
                        // Started with the simulation, as Besiege's automatic timer
                        // is: its key does nothing -- and asked, a hold-to-run would
                        // stop it at once for want of a key held.
                        continue;
                    }
                    // A timer row is started by its input A the way the Timer Plus
                    // block is started by its key: one activation however it
                    // arrives, so the reader's merged edges, taken here once a
                    // frame -- which is why the emulation tick leaves them alone.
                    row.ReadA.Poll();
                    Clock.Pressed(row.Timing, row.ReadA.Pressed, row.ReadA.Held,
                                  row.Timing.HoldToRun, row.Timing.CanStop,
                                  row.Timing.Loops);
                    continue;
                }
                // The game's own `UpdateBlock`, argument for argument:
                // `aHeld = aPressed || aKey.IsHeld`, then `|| emuAHeld` where it
                // is handed to the state machine -- and the release only where
                // the key is not held at all, which is what keeps a key bound to
                // two codes from reporting a release while the other is down.
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
            // SafeAwake builds no controls on a simulating client without physics,
            // and this hook still runs there with every field still null.
            if (rows.Count == 0)
            {
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].ReadA != null)
                {
                    rows[i].ReadA.ReadEmulation();
                }
                if (rows[i].ReadB != null)
                {
                    rows[i].ReadB.ReadEmulation();
                }
            }
        }

        /// <summary>
        /// One emulation tick: the emulated edges, then the one answer that leaves
        /// the block. The game's `EmulationUpdateBlock` and
        /// `SendEmulationUpdateBlock`, in that order.
        /// </summary>
        public override void SendKeyEmulationUpdateHost()
        {
            if (rows.Count == 0 || Time.timeScale == 0f)
            {
                return;
            }
            int count = Count;
            for (int i = 0; i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                if (row.ReadA == null || row.ReadB == null)
                {
                    continue;
                }
                if (i < count && row.Ready && row.IsTimer)
                {
                    // Its edges are the frame update's to take. This is where its
                    // seconds are counted, at the same 50 Hz and with the same
                    // rounding as Besiege's own timer.
                    Clock.Tick(row.Timing, row.Timing.WaitSeconds,
                               row.Timing.PressSeconds, row.Timing.Loops);
                    Hold(row, i, row.Timing.Wants);
                    continue;
                }
                // Taken for every row, in use or not: an edge that is never
                // consumed keeps a stale latch, and a row switched back on would
                // act on a press from minutes ago.
                bool pressedA = row.ReadA.TakePress();
                bool pressedB = row.ReadB.TakePress();
                bool releasedA = row.ReadA.TakeRelease();
                row.ReadB.TakeRelease();
                if (i >= count || !row.Ready)
                {
                    continue;
                }
                // And the game's own `EmulationUpdateBlock`: the emulated edges,
                // with the keyboard's held state ORed in and no filter on the
                // release -- the block does not apply one on this pass.
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

        /// <summary>Holds or lets go of a row's emulated key. The one place that
        /// touches a key, so a press cannot be raised twice or dropped once.
        /// </summary>
        private void Hold(LogicRow row, int index, bool down)
        {
            if (row.Held == down || row.Emulate == null || ownKeys == null
                || index < 0 || index >= ownKeys.Length)
            {
                return;
            }
            row.Held = down;
            EmulateKeys(ownKeys[index], row.Emulate, down);
        }

        /// <summary>Lets go of anything still held, and forgets what every gate
        /// remembered. <c>OnSimulateStop</c> is <c>OnDisable</c>, which is where
        /// the game's own block lets go too.</summary>
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
