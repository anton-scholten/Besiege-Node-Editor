using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// Turns the table into that many of Besiege's own timer blocks, laid out on
    /// one horizontal plane and added to the machine as a selection the player can
    /// then move.
    ///
    /// The settings are written as the game's own mapper keys, which is why this
    /// is worth doing at all: the result is not a copy of a Timer Plus block, it
    /// is a field of real timers, editable one at a time, savable in a machine
    /// that does not need this mod, and behaving exactly as the rows did because
    /// the rows are a reproduction of that block in the first place.
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

        /// <summary>The logic gate's own, from `LogicGate.Awake`. The gate itself
        /// is an integer menu, and the two switches are the two the block shows one
        /// of at a time -- which is why a row's single switch is written to
        /// whichever of them its gate would have shown.</summary>
        private const string KeyInputA = "bmt-activate-A";
        private const string KeyInputB = "bmt-activate-B";
        private const string KeyGate = "bmt-Gate";
        private const string KeyToggleMode = "bmt-toggle-mode";
        private const string KeyInverted = "bmt-inverted";

        /// <summary>And the pin's, from `PinBlockController.Awake`. A pin with
        /// nothing bound to `unpin` never lets go, and `hide-visual` is the
        /// block's own way of taking its picture off the machine.</summary>
        private const string KeyUnpin = "bmt-unpin";
        private const string KeyHidePin = "bmt-hide-visual";

        /// <summary>One grid step. Besiege builds on a one-unit grid, so timers a
        /// unit apart land on it and can be attached to a machine without being
        /// nudged first.</summary>
        private const float Spacing = 1f;

        /// <summary>
        /// A quarter turn about X, which stands a block up on flat ground -- the
        /// same rotation a machine generated from a score uses. Every timer gets
        /// it, so they lie in one plane facing the same way.
        /// </summary>
        private static readonly Quaternion Upright =
            new Quaternion(-0.7071068f, 0f, 0f, 0.7071068f);

        /// <summary>
        /// Converts a block's table and adds the timers to the machine. Returns how
        /// many were added; throws with something worth showing the player.
        /// </summary>
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

            // In the order the table numbers them -- by wait, first to fire first
            // -- rather than in whatever order the rows happen to sit in. The
            // field is then read the way the table is.
            rows = InOrder(rows);

            Vector3 origin = Origin(machine, block.BlockBehaviour, rows.Count);
            List<BlockInfo> made = new List<BlockInfo>();
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(rows.Count)));
            int lines = Mathf.Max(1, Mathf.CeilToInt(rows.Count / (float)columns));

            for (int i = 0; i < rows.Count; i++)
            {
                // Left to right along x, and each line after the first nearer the
                // camera: the build view looks along +z, so a line laid at a
                // smaller z is the next one down the screen.
                Vector3 at = origin + new Vector3(
                    ((i % columns) - (columns - 1) * 0.5f) * Spacing,
                    0f,
                    ((lines - 1) * 0.5f - (i / columns)) * Spacing);
                made.Add(One(rows[i], key, variable, automatic, at));
                if (block.Pins)
                {
                    made.Add(Pinned(at));
                }
            }

            return Drop.Into(made, machine);
        }

        /// <summary>
        /// The rows by wait, ties keeping the order they are in -- the same rank
        /// the table's number column shows, worked out the same way, so timer 1 in
        /// the panel is the first block in the field.
        ///
        /// An insertion sort, stable and as much machinery as thirty-two rows
        /// deserve. <see cref="Table.Sort"/> is not reused because that one writes
        /// through to the block's controls; this is a copy of the values, and the
        /// table itself is left in whatever order the player put it.
        /// </summary>
        private static List<RowData> InOrder(List<RowData> rows)
        {
            List<RowData> all = new List<RowData>(rows);
            for (int i = 1; i < all.Count; i++)
            {
                RowData moving = all[i];
                int at = i;
                while (at > 0 && all[at - 1].Wait > moving.Wait)
                {
                    all[at] = all[at - 1];
                    at--;
                }
                all[at] = moving;
            }
            return all;
        }

        /// <summary>
        /// Where the middle of the field goes: beside the Timer Plus block in the
        /// machine's own space, clear of it, so the timers do not arrive inside the
        /// block that made them. They land selected with the move tool up, so this
        /// only has to be somewhere sensible rather than somewhere final.
        /// </summary>
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
            return here + new Vector3((columns * 0.5f + 1f) * Spacing, 0f, 0f);
        }

        /// <summary>
        /// The logic table's own conversion: one of Besiege's logic gates per row,
        /// laid out the same way and for the same reasons as the timers above.
        /// </summary>
        public static int Into(LogicGatePlusBehaviour block)
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
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(rows.Count)));
            int lines = Mathf.Max(1, Mathf.CeilToInt(rows.Count / (float)columns));

            for (int i = 0; i < rows.Count; i++)
            {
                // Left to right along x, and each line after the first nearer the
                // camera, exactly as the timers are laid out.
                Vector3 at = origin + new Vector3(
                    ((i % columns) - (columns - 1) * 0.5f) * Spacing,
                    0f,
                    ((lines - 1) * 0.5f - (i / columns)) * Spacing);
                made.Add(One(rows[i], at));
                if (block.Pins)
                {
                    made.Add(Pinned(at));
                }
            }

            return Drop.Into(made, machine);
        }

        /// <summary>
        /// The other direction: Besiege's own logic gates taken off the machine and
        /// read into this block's table.
        ///
        /// Nothing is translated. A gate's settings are the same six things a row
        /// holds -- which gate, its switch, two inputs and what it answers to -- so
        /// importing is a copy, and the wiring comes with it for free: a wire is
        /// two blocks agreeing on a key, and copying both halves of that agreement
        /// copies the wire. The board draws the circuit that was on the machine
        /// because it *is* the circuit that was on the machine.
        ///
        /// Up to the block's own thirty-two rows. What is left over stays on the
        /// machine, so a second Logic Gate Plus can import the rest.
        /// </summary>
        /// <param name="left">How many gates were left on the machine.</param>
        /// <param name="undo">The steps the removal wants filed. Handed back rather
        /// than filed here so that whoever called this can put its own edit in the
        /// same list: the rows arriving, the places they are drawn in and the blocks
        /// they came from all belong to one press of undo.</param>
        /// <returns>How many were imported.</returns>
        public static int From(LogicGatePlusBehaviour block, out int left,
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

            // Asked before anything is imported rather than after: the blocks are
            // taken off through the game's own selection tool, and a run that read
            // the gates into rows and then could not remove them would leave the
            // machine holding both.
            AdvancedBlockEditor editor = AdvancedBlockEditor.Instance;
            if (editor == null || editor.selectionController == null)
            {
                throw new Exception("the block editor is not up");
            }

            List<BlockBehaviour> gates = Gathered(machine);
            if (gates.Count == 0)
            {
                throw new Exception("there are no logic gates on this machine");
            }

            int room = LogicGatePlusBehaviour.MaxRows - block.Count;
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
                throw new Exception("those gates could not be read");
            }

            LogicTable.Applied(touched);

            // Besiege's own removal, which handles the joints and hands back the
            // undo actions rather than filing them.
            undo = Removed(taken);
            return taken.Count;
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
                if (block == null || block.GetComponent<LogicGate>() == null)
                {
                    continue;
                }
                // A Logic Gate Plus is a block of this mod's own and has no
                // `LogicGate` on it, so nothing here can pick one up.
                found.Add(block);
            }
            return found;
        }

        /// <summary>
        /// One gate as a new row of the table. False when the block would not give
        /// its settings up, in which case it is left where it is.
        ///
        /// Read off the live mapper controls rather than out of a save: they are
        /// public, they are the same three kinds the rows are made of, and the
        /// binding helpers this mod already has do the rest.
        /// </summary>
        private static bool Read(LogicGatePlusBehaviour into, BlockBehaviour gate,
                                 List<MapperType> touched)
        {
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
                Bindings.Bind(to, Bindings.Code(from));
            }
            touched.Add(to);
        }

        /// <summary>
        /// A block's mapper control by the name a save spells it with.
        ///
        /// `SaveableDataHolder.MapperTypes` is public and every control in it
        /// carries its own `Key`; the gate's own fields are private, so this is the
        /// way in that does not need reflection.
        /// </summary>
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
                // A live control's `Key` is the bare name its block registered --
                // `activate-A` -- and `bmt-` is only what a *save* puts in front of
                // it (`MapperType.XDATA_PREFIX`). Asked with the save's spelling,
                // which is what this mod names everywhere else, nothing matched and
                // every gate on the machine read as unreadable.
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

        /// <summary>
        /// Takes the blocks off the machine the way the delete key does, and hands
        /// back the undo actions rather than filing them.
        /// </summary>
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

        /// <summary>One row as the game's own description of a logic gate.</summary>
        private static BlockInfo One(GateData row, Vector3 at)
        {
            XDataHolder data = new XDataHolder();
            data.Write(new XInteger("bmt-version", 1));

            data.Write(new XInteger(KeyGate, row.Gate));
            // The block has two switches and shows one at a time; the row has one
            // switch and means whichever of them this gate would show. Writing it
            // to both would set a thing the player never asked for the moment they
            // changed the gate on the block afterwards.
            data.Write(new XBoolean(
                Gates.Inverts(row.Gate) ? KeyInverted : KeyToggleMode, row.Mode));

            Binding(data, KeyInputA, row.AVariable, row.AKey, KeyCode.U);
            // Written even for a gate that does not read it: the block keeps its B
            // key whatever the gate is, and a row switched to AND afterwards should
            // find the input it was given.
            Binding(data, KeyInputB, row.BVariable, row.BKey, KeyCode.I);
            Binding(data, KeyEmulate, row.EmulateVariable, row.EmulateKey, KeyCode.C);

            BlockInfo info = new BlockInfo();
            info.Guid = Guid.NewGuid();
            info.ID = (BlockType)LogicGateBlock;
            info.Position = at;
            info.Rotation = Upright;
            info.Scale = Vector3.one;
            info.BlockData = data;
            return info;
        }

        /// <summary>One row as the game's own description of a timer block.</summary>
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

            // The timer takes the block's own activation, since a standalone
            // timer has nothing to follow -- and if the block starts with the
            // simulation, so does the timer.
            if (automatic)
            {
                data.Write(new XBoolean(KeyAutomatic, true));
            }
            else
            {
                Binding(data, KeyActivate, variable, key, KeyCode.B);
            }

            Binding(data, KeyEmulate, row.EmulateVariable, row.EmulateKey, KeyCode.C);

            BlockInfo info = new BlockInfo();
            info.Guid = Guid.NewGuid();
            info.ID = (BlockType)TimerBlock;
            info.Position = at;
            info.Rotation = Upright;
            info.Scale = Vector3.one;
            info.BlockData = data;
            return info;
        }

        /// <summary>
        /// A pin, in the same place as the block it holds still.
        ///
        /// Nothing bound to its unpin key and its visuals hidden, which is what
        /// makes it furniture rather than another block to look at: the field of
        /// timers reads as a field of timers, and none of them wanders off when the
        /// machine is run. Besiege rebuilds a machine's joints from where its
        /// blocks are, so a pin at the block's own position is a pin inside it.
        /// </summary>
        private static BlockInfo Pinned(Vector3 at)
        {
            XDataHolder data = new XDataHolder();
            data.Write(new XInteger("bmt-version", 1));
            data.Write(new XBoolean(KeyHidePin, true));
            // "None" and no keycode: the key exists and answers to nothing, which
            // is how a save spells a key nobody has bound.
            data.Write(new XStringArray(KeyUnpin, Spell(null, KeyCode.None, KeyCode.None)));

            BlockInfo info = new BlockInfo();
            info.Guid = Guid.NewGuid();
            info.ID = BlockType.Pin;
            info.Position = at;
            info.Rotation = Upright;
            info.Scale = Vector3.one;
            info.BlockData = data;
            return info;
        }

        /// <summary>Writes one key into a generated block's data.</summary>
        private static void Binding(XDataHolder data, string key, string variable,
                                    KeyCode code, KeyCode spare)
        {
            data.Write(new XStringArray(key, Spell(variable, code, spare)));
        }

        /// <summary>
        /// One key as a save spells it: an entry per keycode, then `Message=` and
        /// `Use=True` for a key bound to a variable.
        ///
        /// Two things decided here, both of which are wrong in the obvious way.
        ///
        /// **A variable needs a keycode behind it.** `Machine.InitSimBlock` files a
        /// block's keys with `KeyInputController` once per keycode the key holds,
        /// so a key with a name and no keycodes is registered under no name and
        /// hears nothing -- which looks exactly like a timer that ignores the
        /// variable it was given. With `Use=True` that keycode is never answered
        /// to; it is there to be counted, and <paramref name="spare"/> is what goes
        /// in when the row had none of its own.
        ///
        /// **An unbound key stays unbound.** `KeyCode.None` is what a key nobody
        /// has bound holds, and `AddMKey` declines to register it -- so writing
        /// `None` gives a timer that does nothing, which is what a row with nothing
        /// bound did. Substituting the spare here instead would hand every such row
        /// a timer quietly pressing `C`.
        ///
        /// Public so tools/tests/TableCheck.cs can hold it to that, the save format
        /// being the one thing a wrong answer here would be silent about.
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
    }
}
