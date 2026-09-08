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
        /// <summary>Besiege's own timer block.</summary>
        private const int TimerBlock = 66;

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

            Vector3 origin = Origin(machine, block, rows.Count);
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
        private static Vector3 Origin(Machine machine, TimerPlusBehaviour block, int count)
        {
            Vector3 here = Vector3.zero;
            try
            {
                if (block.BlockBehaviour != null && machine.BuildingMachine != null)
                {
                    here = machine.BuildingMachine.InverseTransformPoint(
                        block.BlockBehaviour.transform.position);
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
