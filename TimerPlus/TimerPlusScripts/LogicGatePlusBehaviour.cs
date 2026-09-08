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
        /// <summary>How many rows a block can hold. Every row's five controls
        /// exist from <c>SafeAwake</c> whether the row is in use or not. Lowering
        /// this orphans the controls of any row above the new cap, and a machine
        /// saved with those rows loses them silently.</summary>
        public const int MaxRows = 32;

        /// <summary>How many rows are in use, 1..<see cref="MaxRows"/>.</summary>
        private MSlider rowCount;

        private readonly List<LogicRow> rows = new List<LogicRow>();

        /// <summary>Every input key this block owns, which is what Besiege is
        /// handed to stop a row pressing a key that drives this same block. Built
        /// once: <c>KeyInputController.EmulateEntry</c> compares by reference, and
        /// the references do not change.</summary>
        private MKey[] ownKeys;

        public IList<LogicRow> Rows { get { return rows; } }

        public MSlider CountControl { get { return rowCount; } }

        public int Count
        {
            get
            {
                if (rowCount == null)
                {
                    return 0;
                }
                return Mathf.Clamp(Mathf.RoundToInt(rowCount.Value), 1, MaxRows);
            }
            set
            {
                if (rowCount != null)
                {
                    rowCount.Value = Mathf.Clamp(value, 1, MaxRows);
                }
            }
        }

        // ---- setting up ------------------------------------------------------

        public override void SafeAwake()
        {
            // One mesh, one texture, nothing to swap to.
            Skins.Hide(BlockBehaviour);

            int wanted = Module == null ? 3 : Module.Rows;
            rowCount = BlockBehaviour.AddSlider("Rows", "RowsKey",
                Mathf.Clamp(wanted, 1, MaxRows), 1f, MaxRows, "", "");

            rows.Clear();
            for (int i = 0; i < MaxRows; i++)
            {
                rows.Add(Build(i));
            }

            // Every input on the block, so no row can press a key that drives it.
            // Besiege's own gate passes its two for the same reason.
            ownKeys = new MKey[MaxRows * 2];
            for (int i = 0; i < MaxRows; i++)
            {
                ownKeys[i * 2] = rows[i].InputA;
                ownKeys[i * 2 + 1] = rows[i].InputB;
            }

            ShowStock(true);
        }

        private LogicRow Build(int index)
        {
            string n = index.ToString();
            LogicRow row = new LogicRow();

            // The game's own defaults for a logic gate: U and I in, C out.
            row.InputA = AddKey("Input A " + (index + 1), "A" + n, KeyCode.U);
            row.InputB = AddKey("Input B " + (index + 1), "B" + n, KeyCode.I);

            List<string> gates = new List<string>();
            for (int g = 0; g < Gates.Count; g++)
            {
                gates.Add(Gates.Names[g]);
            }
            row.Kind = BlockBehaviour.AddMenu("Gate" + n, 0, gates);

            // One switch where Besiege has two. Which of the two it is depends on
            // the gate -- see LogicRow.Mode -- and the game shows one at a time
            // for the same reason.
            row.Mode = AddToggle("Mode " + (index + 1), "Mode" + n, false);
            row.Emulate = AddEmulatorKey("Emulate " + (index + 1), "Out" + n,
                                         KeyCode.C);

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
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = visible && i < count;
                LogicRow row = rows[i];
                if (!row.Ready)
                {
                    continue;
                }
                row.InputA.DisplayInMapper = on;
                // The gates that read one input take the second off the mapper,
                // exactly as the game's own block does.
                row.InputB.DisplayInMapper = on && Gates.UsesB(row.Gate);
                row.Kind.DisplayInMapper = on;
                row.Mode.DisplayInMapper = on;
                row.Emulate.DisplayInMapper = on;
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
                Gates.Advance(row, row.Gate, row.Switch,
                              row.ReadA.RealPressed, row.ReadB.RealPressed,
                              row.ReadA.RealHeld || row.ReadA.EmulatedHeld,
                              row.ReadB.RealHeld || row.ReadB.EmulatedHeld,
                              row.ReadA.RealReleased);
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
                Gates.Advance(row, row.Gate, row.Switch, pressedA, pressedB,
                              row.ReadA.RealHeld || row.ReadA.EmulatedHeld,
                              row.ReadB.RealHeld || row.ReadB.EmulatedHeld,
                              releasedA);
                Hold(row, Gates.Answer(row, row.Gate));
            }
        }

        public override bool EmulatesAnyKeys { get { return true; } }

        /// <summary>Holds or lets go of a row's emulated key. The one place that
        /// touches a key, so a press cannot be raised twice or dropped once.
        /// </summary>
        private void Hold(LogicRow row, bool down)
        {
            if (row.Held == down || row.Emulate == null || ownKeys == null)
            {
                return;
            }
            row.Held = down;
            EmulateKeys(ownKeys, row.Emulate, down);
        }

        /// <summary>Lets go of anything still held, and forgets what every gate
        /// remembered. <c>OnSimulateStop</c> is <c>OnDisable</c>, which is where
        /// the game's own block lets go too.</summary>
        public override void OnSimulateStop()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                Hold(row, false);
                row.A = false;
                row.B = false;
                row.AToggled = false;
                row.BToggled = false;
                row.Count = 0;
                row.LastCount = 0;
            }
        }
    }
}
