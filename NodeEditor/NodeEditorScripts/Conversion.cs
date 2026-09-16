using System;
using System.Collections.Generic;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Turns a table into Besiege's own blocks -- timers, logic gates, pins --
    /// written with the game's own mapper keys, so they need no mod; and imports
    /// gates back.
    /// </summary>
    public static class Conversion
    {
        /// <summary>Besiege's own timer block, and its logic gate.</summary>
        private const int TimerBlock = 66;
        private const int LogicGateBlock = 68;

        /// <summary>Its mapper keys, as a save spells them: the `bmt-` prefix and
        /// the names from `TimerBlock.Awake`.</summary>
        private const string KeyActivate = "bmt-activate";
        private const string KeyEmulate = "bmt-emulate";
        private const string KeyAutomatic = "bmt-automatic";
        private const string KeyHold = "bmt-hold-to-activate";
        private const string KeyStop = "bmt-can-stop";
        private const string KeyLoop = "bmt-loop";
        private const string KeyWait = "bmt-wait";
        private const string KeyDuration = "bmt-emulation-time";

        /// <summary>The logic gate's keys, from `LogicGate.Awake`. A row's one
        /// switch is written to whichever of the block's two its gate
        /// shows.</summary>
        private const string KeyInputA = "bmt-activate-A";
        private const string KeyInputB = "bmt-activate-B";
        private const string KeyGate = "bmt-Gate";
        private const string KeyToggleMode = "bmt-toggle-mode";
        private const string KeyInverted = "bmt-inverted";

        /// <summary>The pin's keys, from `PinBlockController.Awake`.</summary>
        private const string KeyUnpin = "bmt-unpin";
        private const string KeyHidePin = "bmt-hide-visual";

        /// <summary>Block steps: adjacent down a column (z), a quarter unit apart
        /// between columns (x).</summary>
        private const float Across = 1.25f;
        private const float Along = 1f;

        /// <summary>A quarter turn about X: stands a block upright on flat
        /// ground.</summary>
        private static readonly Quaternion Upright =
            new Quaternion(-0.7071068f, 0f, 0f, 0.7071068f);

        /// <summary>Adds a Timer Plus table's timers to the machine. Returns the
        /// rows added; throws with a message for the player.</summary>
        public static int Into(TimerPlusBehaviour block)
        {
            if (block == null)
            {
                throw new Exception("there is no block to convert");
            }
            Machine machine = Machine.Active();
            if (machine == null)
            {
                throw new Exception("there is no machine to add to");
            }
            if (!machine.CanModify)
            {
                throw new Exception("this machine cannot be changed here");
            }

            List<RowData> rows = Table.Snapshot(block);
            if (rows.Count == 0)
            {
                throw new Exception("there are no rows to convert");
            }

            bool automatic = block.Automatic != null && block.Automatic.IsActive;
            // Every row is started by the block, so every timer made from one is
            // started by what started the block.
            KeyCode key = Bindings.Code(block.MasterKey);
            string variable = Bindings.IsVariable(block.MasterKey)
                ? Bindings.Variable(block.MasterKey) : null;

            // In the table's numbering order: by wait, first to fire first.
            rows = InOrder(rows);

            Vector3 origin = Origin(machine, block.BlockBehaviour, rows.Count);
            List<BlockInfo> made = new List<BlockInfo>();

            for (int i = 0; i < rows.Count; i++)
            {
                Vector3 at = Spot(origin, i, rows.Count);
                made.Add(One(rows[i], key, variable, automatic, at));
                if (block.Pins)
                {
                    made.Add(Pinned(at));
                }
            }

            // Rows, not blocks: with pins on, every row is two blocks.
            return Drop.Into(made, machine) > 0 ? rows.Count : 0;
        }

        /// <summary>Where the <paramref name="i"/>th of <paramref name="count"/>
        /// blocks goes: a square-ish field on one horizontal plane, left to right
        /// along x, each later line at a smaller z and so lower on screen. Timers
        /// and logic gates are laid out alike, so they are laid out here.</summary>
        private static Vector3 Spot(Vector3 origin, int i, int count)
        {
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int lines = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            return origin + new Vector3(
                ((i % columns) - (columns - 1) * 0.5f) * Across,
                0f,
                ((lines - 1) * 0.5f - (i / columns)) * Along);
        }

        /// <summary>The rows by wait, ties in table order: the order the table
        /// numbers them.</summary>
        private static List<RowData> InOrder(List<RowData> rows)
        {
            List<RowData> all = new List<RowData>(rows);
            Table.Stable(all, delegate(RowData a, RowData b)
            {
                return a.Wait.CompareTo(b.Wait);
            });
            return all;
        }

        /// <summary>The field's middle: beside the block and clear of it. The
        /// blocks arrive selected under the move tool, so this need only be
        /// sensible.</summary>
        private static Vector3 Origin(Machine machine, BlockBehaviour block, int count)
        {
            Vector3 here = Vector3.zero;
            try
            {
                if (block != null && machine.BuildingMachine != null)
                {
                    here = machine.BuildingMachine.InverseTransformPoint(
                        block.transform.position);
                }
            }
            catch (Exception)
            {
                // A block with no machine around it still converts; the field just
                // starts at the machine's own origin.
            }
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            return here + new Vector3((columns * 0.5f + 1f) * Across, 0f, 0f);
        }

        /// <summary>The node editor's export: one Besiege logic gate or timer per
        /// row, laid out as the timers are.</summary>
        public static int Into(ComputerBehaviour block)
        {
            if (block == null)
            {
                throw new Exception("there is no block to convert");
            }
            Machine machine = Machine.Active();
            if (machine == null)
            {
                throw new Exception("there is no machine to add to");
            }
            if (!machine.CanModify)
            {
                throw new Exception("this machine cannot be changed here");
            }

            List<GateData> rows = LogicTable.Snapshot(block);
            if (rows.Count == 0)
            {
                throw new Exception("there are no rows to convert");
            }

            Vector3 origin = Origin(machine, block.BlockBehaviour, rows.Count);
            List<BlockInfo> made = new List<BlockInfo>();

            for (int i = 0; i < rows.Count; i++)
            {
                Vector3 at = Spot(origin, i, rows.Count);
                made.Add(One(rows[i], at));
                if (block.Pins)
                {
                    made.Add(Pinned(at));
                }
            }

            // Rows, not blocks: with pins on, every row is two blocks.
            return Drop.Into(made, machine) > 0 ? rows.Count : 0;
        }

        /// <summary>
        /// Imports Besiege's logic gates and timers as rows and takes them off the
        /// machine. A copy rather than a translation, so the wiring comes along: a
        /// wire is two blocks agreeing on a key. Stops at <see
        /// cref="ComputerBehaviour.MaxRows"/>.
        /// </summary>
        /// <param name="left">How many were left on the machine.</param>
        /// <param name="undo">The removal's undo steps, for the caller to file with
        /// its own edit as one step.</param>
        /// <returns>How many were imported.</returns>
        public static int From(ComputerBehaviour block, out int left,
                               out List<UndoAction> undo)
        {
            left = 0;
            undo = new List<UndoAction>();
            if (block == null)
            {
                throw new Exception("there is no block to import into");
            }
            Machine machine = Machine.Active();
            if (machine == null)
            {
                throw new Exception("there is no machine to import from");
            }
            if (!machine.CanModify)
            {
                throw new Exception("this machine cannot be changed here");
            }

            // Checked first: gates read in but not removable would leave the
            // machine with both.
            AdvancedBlockEditor editor = AdvancedBlockEditor.Instance;
            if (editor == null || editor.selectionController == null)
            {
                throw new Exception("the block editor is not up");
            }

            List<BlockBehaviour> gates = Gathered(machine);
            if (gates.Count == 0)
            {
                throw new Exception("there are no logic gates\nor timers on this machine");
            }

            int room = ComputerBehaviour.MaxRows - block.Count;
            if (room <= 0)
            {
                left = gates.Count;
                throw new Exception("this block has no rows left");
            }

            List<MapperType> touched = new List<MapperType>();
            List<BlockBehaviour> taken = new List<BlockBehaviour>();
            for (int i = 0; i < gates.Count && taken.Count < room; i++)
            {
                if (Read(block, gates[i], touched))
                {
                    taken.Add(gates[i]);
                }
            }
            left = gates.Count - taken.Count;
            if (taken.Count == 0)
            {
                throw new Exception("those blocks could not be read");
            }

            LogicTable.Applied(block, touched);

            // Besiege's own removal, which handles the joints and hands back the
            // undo actions rather than filing them.
            undo = Removed(taken);
            return taken.Count;
        }

        /// <summary>
        /// Imports Besiege's timers as Timer Plus rows and takes them off the
        /// machine. Every row starts on the block's own key, so a timer's own
        /// activation is not kept per row: a block with no rows takes on the one
        /// every timer taken shares.
        /// </summary>
        /// <param name="differ">Whether the timers taken did not all start the way
        /// the block now does, so some will start differently.</param>
        public static int From(TimerPlusBehaviour block, out int left,
                               out List<UndoAction> undo, out bool differ)
        {
            left = 0;
            differ = false;
            undo = new List<UndoAction>();
            if (block == null)
            {
                throw new Exception("there is no block to import into");
            }
            Machine machine = Machine.Active();
            if (machine == null || !machine.CanModify)
            {
                throw new Exception("this machine cannot be changed here");
            }
            AdvancedBlockEditor editor = AdvancedBlockEditor.Instance;
            if (editor == null || editor.selectionController == null)
            {
                throw new Exception("the block editor is not up");
            }

            List<BlockBehaviour> timers = Timers(machine);
            if (timers.Count == 0)
            {
                throw new Exception("no timers on the machine");
            }
            int room = TimerPlusBehaviour.MaxRows - block.Count;
            if (room <= 0)
            {
                left = timers.Count;
                throw new Exception("the table is full");
            }

            bool empty = block.Count == 0;
            List<MapperType> touched = new List<MapperType>();
            List<BlockBehaviour> taken = new List<BlockBehaviour>();
            List<RowData> rows = Table.Snapshot(block);
            global::TimerBlock first = null;
            for (int i = 0; i < timers.Count && taken.Count < room; i++)
            {
                global::TimerBlock timer = timers[i].GetComponent<global::TimerBlock>();
                RowData row = Timer(timer);
                if (row == null)
                {
                    continue;
                }
                rows.Add(row);
                if (first == null)
                {
                    first = timer;
                }
                else if (!Alike(first.ActivateKey, Auto(first),
                                timer.ActivateKey, Auto(timer)))
                {
                    differ = true;
                }
                taken.Add(timers[i]);
            }
            left = timers.Count - taken.Count;
            if (taken.Count == 0)
            {
                throw new Exception("those timers could not be read");
            }
            Table.Store(block, rows, touched);

            if (empty && !differ && block.MasterKey != null && block.Automatic != null)
            {
                Copy(first.ActivateKey, block.MasterKey, touched);
                block.Automatic.IsActive = Auto(first);
                touched.Add(block.Automatic);
            }
            else if (!Alike(first.ActivateKey, Auto(first), block.MasterKey,
                            block.Automatic != null && block.Automatic.IsActive))
            {
                differ = true;
            }

            LogicTable.Applied(touched);
            undo = Removed(taken);
            return taken.Count;
        }

        /// <summary>Every one of Besiege's timers on the machine, in the order the
        /// machine holds them.</summary>
        private static List<BlockBehaviour> Timers(Machine machine)
        {
            List<BlockBehaviour> found = new List<BlockBehaviour>();
            List<BlockBehaviour> all = machine.BuildingBlocks;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].GetComponent<global::TimerBlock>() != null)
                {
                    found.Add(all[i]);
                }
            }
            return found;
        }

        /// <summary>One timer as a Timer Plus row, or null, with the timer left
        /// alone, when it will not give its settings up.</summary>
        private static RowData Timer(global::TimerBlock timer)
        {
            if (timer == null || timer.EmulateKey == null || timer.WaitSlider == null
                || timer.EmulationSlider == null)
            {
                return null;
            }
            RowData data = new RowData();
            data.Wait = timer.WaitSlider.Value;
            data.Duration = timer.EmulationSlider.Value;
            data.Hold = timer.HoldToActivate != null && timer.HoldToActivate.IsActive;
            data.Stop = timer.CanStop != null && timer.CanStop.IsActive;
            data.Loop = timer.Loop != null && timer.Loop.IsActive;
            data.EmulateKeys = Bindings.Codes(timer.EmulateKey).ToArray();
            data.EmulateVariable = Bindings.IsVariable(timer.EmulateKey)
                ? Bindings.Variable(timer.EmulateKey) : null;
            return data;
        }

        private static bool Auto(global::TimerBlock timer)
        {
            return timer.Auto != null && timer.Auto.IsActive;
        }

        /// <summary>Whether two things start the same way: both with the
        /// simulation, or both on the same keys or names.</summary>
        private static bool Alike(MKey a, bool autoA, MKey b, bool autoB)
        {
            if (autoA || autoB)
            {
                return autoA == autoB;
            }
            return Bindings.Show(a, "") == Bindings.Show(b, "");
        }

        /// <summary>Every one of Besiege's own logic gates on the machine, in the
        /// order the machine holds them.</summary>
        private static List<BlockBehaviour> Gathered(Machine machine)
        {
            List<BlockBehaviour> found = new List<BlockBehaviour>();
            List<BlockBehaviour> all = machine.BuildingBlocks;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BlockBehaviour block = all[i];
                // Its logic gates and its timers: a timer is a row as well now.
                if (block == null || (block.GetComponent<LogicGate>() == null
                                      && block.GetComponent<global::TimerBlock>() == null))
                {
                    continue;
                }
                // A Computer is a block of this mod's own and has no
                // `LogicGate` on it, so nothing here can pick one up.
                found.Add(block);
            }
            return found;
        }

        /// <summary>One gate as a new row, read off its live mapper controls.
        /// False, with the block left alone, when it will not give its settings
        /// up.</summary>
        private static bool Read(ComputerBehaviour into, BlockBehaviour gate,
                                 List<MapperType> touched)
        {
            global::TimerBlock timer = gate.GetComponent<global::TimerBlock>();
            if (timer != null)
            {
                return Timed(into, timer, touched);
            }
            MMenu kind = Menued(gate, KeyGate);
            MKey a = Keyed(gate, KeyInputA);
            MKey b = Keyed(gate, KeyInputB);
            MKey emulate = Keyed(gate, KeyEmulate);
            if (kind == null || a == null || b == null || emulate == null)
            {
                return false;
            }
            int row = LogicTable.Add(into, touched);
            if (row < 0 || row >= into.Rows.Count)
            {
                return false;
            }
            LogicRow made = into.Rows[row];
            if (!made.Ready)
            {
                return false;
            }
            made.Kind.Value = kind.Value;
            touched.Add(made.Kind);

            // The block shows one of its two switches at a time and the row has
            // one; which of the block's is the live one depends on its gate.
            MToggle mode = Toggled(gate, Gates.Inverts(kind.Value)
                                         ? KeyInverted : KeyToggleMode);
            made.Mode.IsActive = mode != null && mode.IsActive;
            touched.Add(made.Mode);

            Copy(a, made.InputA, touched);
            Copy(b, made.InputB, touched);
            Copy(emulate, made.Emulate, touched);
            return true;
        }

        /// <summary>One Besiege timer as a timer row, through `TimerBlock`'s public
        /// properties. Automatic on becomes a row with nothing on its input
        /// (<see cref="LogicRow.Automatic"/>).</summary>
        private static bool Timed(ComputerBehaviour into, global::TimerBlock timer,
                                  List<MapperType> touched)
        {
            bool automatic = timer.Auto != null && timer.Auto.IsActive;
            if (timer.ActivateKey == null || timer.EmulateKey == null
                || timer.WaitSlider == null || timer.EmulationSlider == null)
            {
                return false;
            }
            int row = LogicTable.Add(into, touched);
            if (row < 0 || row >= into.Rows.Count)
            {
                return false;
            }
            LogicRow made = into.Rows[row];
            if (!made.Ready)
            {
                return false;
            }
            made.Kind.Value = Gates.Timer;
            made.Mode.IsActive = false;
            made.Wait.Value = timer.WaitSlider.Value;
            made.Duration.Value = timer.EmulationSlider.Value;
            made.Hold.IsActive = timer.HoldToActivate != null
                && timer.HoldToActivate.IsActive;
            made.Stop.IsActive = timer.CanStop != null && timer.CanStop.IsActive;
            made.Loop.IsActive = timer.Loop != null && timer.Loop.IsActive;
            touched.Add(made.Kind);
            touched.Add(made.Mode);
            touched.Add(made.Wait);
            touched.Add(made.Duration);
            touched.Add(made.Hold);
            touched.Add(made.Stop);
            touched.Add(made.Loop);

            if (automatic)
            {
                // Nothing on its input is what makes it start itself.
                Bindings.Clear(made.InputA);
                touched.Add(made.InputA);
            }
            else
            {
                Copy(timer.ActivateKey, made.InputA, touched);
            }
            // The row it was added as is a copy of the one above; a timer reads one
            // input, and whatever was copied into the other is nothing it reads.
            Bindings.Clear(made.InputB);
            touched.Add(made.InputB);
            Copy(timer.EmulateKey, made.Emulate, touched);
            return true;
        }

        /// <summary>One key's binding onto another: a name, a keycode, or nothing
        /// at all.</summary>
        private static void Copy(MKey from, MKey to, List<MapperType> touched)
        {
            if (from == null || to == null)
            {
                return;
            }
            if (Bindings.IsVariable(from))
            {
                Bindings.BindVariable(to, Bindings.Variable(from));
            }
            else
            {
                Bindings.Bind(to, Bindings.Codes(from).ToArray());
            }
            touched.Add(to);
        }

        /// <summary>A block's mapper control by key, through the public
        /// `SaveableDataHolder.MapperTypes`: no reflection needed.</summary>
        private static MapperType Control(BlockBehaviour block, string key)
        {
            if (block == null)
            {
                return null;
            }
            List<MapperType> all = block.MapperTypes;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] == null)
                {
                    continue;
                }
                // A live control's `Key` is the bare name (`activate-A`); only a
                // save adds `bmt-` (`MapperType.XDATA_PREFIX`).
                if (all[i].Key == key
                    || MapperType.XDATA_PREFIX + all[i].Key == key)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static MKey Keyed(BlockBehaviour block, string key)
        {
            return Control(block, key) as MKey;
        }

        private static MMenu Menued(BlockBehaviour block, string key)
        {
            return Control(block, key) as MMenu;
        }

        private static MToggle Toggled(BlockBehaviour block, string key)
        {
            return Control(block, key) as MToggle;
        }

        /// <summary>Takes blocks off as the delete key does, handing back the undo
        /// actions unfiled.</summary>
        private static List<UndoAction> Removed(List<BlockBehaviour> blocks)
        {
            AdvancedBlockEditor editor = AdvancedBlockEditor.Instance;
            if (editor == null || editor.selectionController == null)
            {
                Log.Warn("the gates were imported but could not be taken off the "
                         + "machine: the block editor is not up.");
                return new List<UndoAction>();
            }
            BlockSelectionTool picker = editor.selectionController;
            picker.DeselectAll(true, true);
            List<UndoAction> undo = picker.RemoveBlocks(blocks, true);
            return undo == null ? new List<UndoAction>() : undo;
        }

        /// <summary>One row as the game's own description of a logic
        /// gate.</summary>
        private static BlockInfo One(GateData row, Vector3 at)
        {
            if (row.Gate == Gates.Timer)
            {
                return Timed(row, at);
            }
            XDataHolder data = new XDataHolder();
            data.Write(new XInteger("bmt-version", 1));

            data.Write(new XInteger(KeyGate, row.Gate));
            // Only the switch this gate shows: writing both would set one nobody
            // asked for.
            data.Write(new XBoolean(
                Gates.Inverts(row.Gate) ? KeyInverted : KeyToggleMode, row.Mode));

            Binding(data, KeyInputA, row.AVariable, row.AKeys, KeyCode.U);
            // Written even if the gate ignores B: the block keeps it for a later
            // gate change.
            Binding(data, KeyInputB, row.BVariable, row.BKeys, KeyCode.I);
            Binding(data, KeyEmulate, row.EmulateVariable, row.EmulateKeys, KeyCode.C);

            return Built((BlockType)LogicGateBlock, at, data);
        }

        /// <summary>A timer row as Besiege's timer block: started by what input A
        /// reads, pressing what the row presses.</summary>
        private static BlockInfo Timed(GateData row, Vector3 at)
        {
            XDataHolder data = new XDataHolder();
            data.Write(new XInteger("bmt-version", 1));

            data.Write(new XSingle(KeyWait, row.Wait));
            data.Write(new XSingle(KeyDuration, row.Duration));
            data.Write(new XBoolean(KeyHold, row.HoldRun));
            data.Write(new XBoolean(KeyStop, row.CanStop));
            data.Write(new XBoolean(KeyLoop, row.Loops));
            bool unbound = row.AVariable == null
                ? row.AKeys.Length == 0
                : Bindings.Named(row.AVariable).Count == 0;
            if (unbound)
            {
                // Nothing to start it: a row that starts with the simulation, which
                // on Besiege's own timer is Automatic.
                data.Write(new XBoolean(KeyAutomatic, true));
            }
            else
            {
                Binding(data, KeyActivate, row.AVariable, row.AKeys, KeyCode.B);
            }
            Binding(data, KeyEmulate, row.EmulateVariable, row.EmulateKeys, KeyCode.C);

            return Built((BlockType)TimerBlock, at, data);
        }

        /// <summary>One row as the game's own description of a timer
        /// block.</summary>
        private static BlockInfo One(RowData row, KeyCode key, string variable,
                                     bool automatic, Vector3 at)
        {
            XDataHolder data = new XDataHolder();
            // Written on every block by the game itself; one less difference
            // between a block this made and one somebody placed.
            data.Write(new XInteger("bmt-version", 1));

            data.Write(new XSingle(KeyWait, row.Wait));
            data.Write(new XSingle(KeyDuration, row.Duration));
            data.Write(new XBoolean(KeyHold, row.Hold));
            data.Write(new XBoolean(KeyStop, row.Stop));
            data.Write(new XBoolean(KeyLoop, row.Loop));

            // Standalone timers follow the block's own activation, or start with
            // the simulation when it does.
            if (automatic)
            {
                data.Write(new XBoolean(KeyAutomatic, true));
            }
            else
            {
                Binding(data, KeyActivate, variable, new KeyCode[] { key }, KeyCode.B);
            }

            Binding(data, KeyEmulate, row.EmulateVariable, row.EmulateKeys, KeyCode.C);

            return Built((BlockType)TimerBlock, at, data);
        }

        /// <summary>A hidden pin with nothing on unpin, at the block's own
        /// position: Besiege rebuilds joints from positions, so it holds that block
        /// still.</summary>
        private static BlockInfo Pinned(Vector3 at)
        {
            XDataHolder data = new XDataHolder();
            data.Write(new XInteger("bmt-version", 1));
            data.Write(new XBoolean(KeyHidePin, true));
            // "None" and no keycode: the key exists and answers to nothing, which
            // is how a save spells a key nobody has bound.
            data.Write(new XStringArray(KeyUnpin, Spell(null, KeyCode.None, KeyCode.None)));

            return Built(BlockType.Pin, at, data);
        }

        /// <summary>The game's own description of one generated block. Every block
        /// this makes differs only in its type and its data: the rest -- a fresh
        /// guid, upright, unscaled -- is the same every time.</summary>
        private static BlockInfo Built(BlockType kind, Vector3 at, XDataHolder data)
        {
            BlockInfo info = new BlockInfo();
            info.Guid = Guid.NewGuid();
            info.ID = kind;
            info.Position = at;
            info.Rotation = Upright;
            info.Scale = Vector3.one;
            info.BlockData = data;
            return info;
        }

        /// <summary>Writes one key into a generated block's data.</summary>
        private static void Binding(XDataHolder data, string key, string variable,
                                    KeyCode[] codes, KeyCode spare)
        {
            data.Write(new XStringArray(key, Spell(variable, codes, spare)));
        }

        /// <summary>
        /// One key as a save spells it: an entry per keycode, then `Message=` and
        /// `Use=True` for names. A named key still needs a keycode, since
        /// registration is per keycode, so <paramref name="spare"/> fills in; an
        /// unbound key stays `None` rather than quietly pressing the spare. Public
        /// for TableCheck.
        /// </summary>
        public static string[] Spell(string variable, KeyCode code, KeyCode spare)
        {
            if (string.IsNullOrEmpty(variable))
            {
                return new string[] { code.ToString() };
            }
            return new string[]
            {
                (code == KeyCode.None ? spare : code).ToString(),
                "Message=" + variable,
                "Use=True"
            };
        }

        /// <summary>The same for several keycodes: an entry each, in
        /// order.</summary>
        public static string[] Spell(string variable, KeyCode[] codes, KeyCode spare)
        {
            if (!string.IsNullOrEmpty(variable) || codes == null || codes.Length <= 1)
            {
                return Spell(variable, codes == null || codes.Length == 0
                    ? KeyCode.None : codes[0], spare);
            }
            string[] spelt = new string[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                spelt[i] = codes[i].ToString();
            }
            return spelt;
        }
    }
}
