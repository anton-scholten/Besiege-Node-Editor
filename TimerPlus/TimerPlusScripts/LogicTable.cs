using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>One logic row's settings, lifted out of its mapper controls so
    /// they can be moved about: sorted, shuffled up when a row is deleted, copied
    /// into a real logic gate block.</summary>
    public class GateData
    {
        public KeyCode AKey = KeyCode.U;
        public string AVariable;

        public KeyCode BKey = KeyCode.I;
        public string BVariable;

        public int Gate;
        public bool Mode;

        public KeyCode EmulateKey = KeyCode.C;
        public string EmulateVariable;
    }

    /// <summary>
    /// The logic table as a whole: reading the rows out of the block, writing them
    /// back, and the things that rearrange them. <see cref="Table"/> for the timer
    /// table, and the same rules -- everything goes through the mapper controls,
    /// which is what makes an edit saved, undoable and sent over multiplayer.
    /// </summary>
    public static class LogicTable
    {
        // Which column a sort is on. Only the gate sorts: a column of keys sorted
        // by name answers a question nobody asks, and the switch is one bit.
        public const int ColGate = 0;

        public static GateData Read(LogicRow row)
        {
            GateData data = new GateData();
            if (row == null || !row.Ready)
            {
                return data;
            }
            data.AKey = Bindings.Code(row.InputA);
            data.AVariable = Bindings.IsVariable(row.InputA)
                ? Bindings.Variable(row.InputA) : null;
            data.BKey = Bindings.Code(row.InputB);
            data.BVariable = Bindings.IsVariable(row.InputB)
                ? Bindings.Variable(row.InputB) : null;
            data.Gate = row.Gate;
            data.Mode = row.Switch;
            data.EmulateKey = Bindings.Code(row.Emulate);
            data.EmulateVariable = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            return data;
        }

        public static void Write(LogicRow row, GateData data, List<MapperType> touched)
        {
            if (row == null || !row.Ready || data == null)
            {
                return;
            }
            Bind(row.InputA, data.AVariable, data.AKey);
            Bind(row.InputB, data.BVariable, data.BKey);
            Bind(row.Emulate, data.EmulateVariable, data.EmulateKey);
            row.Kind.Value = Mathf.Clamp(data.Gate, 0, Gates.Count - 1);
            row.Mode.IsActive = data.Mode;

            if (touched != null)
            {
                Note(touched, row.InputA);
                Note(touched, row.InputB);
                Note(touched, row.Kind);
                Note(touched, row.Mode);
                Note(touched, row.Emulate);
            }
        }

        private static void Bind(MKey key, string variable, KeyCode code)
        {
            if (variable != null)
            {
                Bindings.BindVariable(key, variable);
            }
            else
            {
                Bindings.Bind(key, code);
            }
        }

        /// <summary>
        /// Takes a row off the block and takes its wires with it.
        ///
        /// A wire is a name: a row's input bound to whatever another row's answer
        /// goes out under. Take the row away and nothing answers that name any
        /// more, and every input still bound to it is a wire hanging from nothing
        /// -- which the board reads as a key arriving from the machine and draws an
        /// input node for. So the loose ends are cleared here, in the same edit as
        /// the removal, and only the loose ones: a name some other row still
        /// presses is a wire that still works and is left alone.
        /// </summary>
        public static void Erase(LogicGatePlusBehaviour block, int index,
                                 List<MapperType> touched)
        {
            if (block == null || block.Count <= 1
                || index < 0 || index >= block.Count)
            {
                return;
            }
            // What the row answered to, read before it is gone.
            List<string> names = new List<string>();
            KeyCode code = KeyCode.None;
            LogicRow going = index < block.Rows.Count ? block.Rows[index] : null;
            if (going != null && going.Ready)
            {
                if (Bindings.IsVariable(going.Emulate))
                {
                    Spread(Bindings.Variable(going.Emulate), names);
                }
                else
                {
                    code = Bindings.Code(going.Emulate);
                }
            }
            Remove(block, index, touched);
            for (int i = 0; i < block.Count && i < block.Rows.Count; i++)
            {
                LogicRow row = block.Rows[i];
                if (row == null || !row.Ready)
                {
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    MKey input = port == 0 ? row.InputA : row.InputB;
                    string had = Bindings.IsVariable(input)
                        ? Bindings.Variable(input) : null;
                    bool mine = had != null
                        ? Any(had, names)
                        : (code != KeyCode.None && Bindings.Code(input) == code);
                    if (!mine || Answered(block, had, had == null ? code
                                                                 : KeyCode.None))
                    {
                        continue;
                    }
                    Bindings.Clear(input);
                    Note(touched, input);
                }
            }
        }

        /// <summary>Whether any row still answers to this binding.</summary>
        private static bool Answered(LogicGatePlusBehaviour block, string variable,
                                     KeyCode code)
        {
            for (int i = 0; i < block.Count && i < block.Rows.Count; i++)
            {
                LogicRow row = block.Rows[i];
                if (row == null || !row.Ready)
                {
                    continue;
                }
                if (variable != null)
                {
                    if (Bindings.IsVariable(row.Emulate)
                        && Held(Bindings.Variable(row.Emulate), variable))
                    {
                        return true;
                    }
                    continue;
                }
                if (code != KeyCode.None && !Bindings.IsVariable(row.Emulate)
                    && Bindings.Code(row.Emulate) == code)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A key answers to a `;`-joined list of names; these are the two
        /// questions worth asking of one.</summary>
        private static void Spread(string list, List<string> into)
        {
            string[] said = (list == null ? "" : list).Split(';');
            for (int i = 0; i < said.Length; i++)
            {
                string one = said[i].Trim();
                if (one.Length > 0 && !into.Contains(one))
                {
                    into.Add(one);
                }
            }
        }

        private static bool Held(string list, string want)
        {
            List<string> said = new List<string>();
            Spread(list, said);
            return said.Contains(want);
        }

        private static bool Any(string list, List<string> wanted)
        {
            List<string> said = new List<string>();
            Spread(list, said);
            for (int i = 0; i < said.Count; i++)
            {
                if (wanted.Contains(said[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The block as it stands, for an edit about to change several of its
        /// controls at once -- a row removed shifts every row after it, and one
        /// undo should put all of that back rather than a field of it.
        ///
        /// Null when somebody else's game is listening: there the edit goes out
        /// through Besiege's own handler, which files its own steps.
        /// </summary>
        public static BlockInfo Marked(LogicGatePlusBehaviour block)
        {
            try
            {
                if (EditFieldHandler.Instance != null)
                {
                    return null;
                }
                return Snap(block);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Files what has happened since that snapshot as one step of
        /// Besiege's undo.</summary>
        public static void Filed(LogicGatePlusBehaviour block, BlockInfo before)
        {
            if (before == null)
            {
                return;
            }
            try
            {
                Machine machine = Machine.Active();
                BlockInfo after = Snap(block);
                if (machine == null || machine.UndoSystem == null || after == null)
                {
                    return;
                }
                machine.UndoSystem.EditBlock(after, before);
            }
            catch (Exception e)
            {
                Log.Warn("could not file the edit for undo: " + e.Message);
            }
        }

        private static BlockInfo Snap(LogicGatePlusBehaviour block)
        {
            BlockBehaviour body = block == null ? null : block.BlockBehaviour;
            if (body == null)
            {
                return null;
            }
            // `BlockInfo.FromBlockBehaviour` hands back the block's last saved
            // state rather than what its controls hold now, so the block is asked
            // to save first or the snapshot is a frame stale.
            body.OnSave(new XDataHolder());
            return BlockInfo.FromBlockBehaviour(body);
        }

        private static void Note(List<MapperType> into, MapperType one)
        {
            if (into != null && one != null && !into.Contains(one))
            {
                into.Add(one);
            }
        }

        /// <summary>Every row in use, as values.</summary>
        public static List<GateData> Snapshot(LogicGatePlusBehaviour block)
        {
            List<GateData> all = new List<GateData>();
            if (block == null)
            {
                return all;
            }
            int count = block.Count;
            for (int i = 0; i < count && i < block.Rows.Count; i++)
            {
                all.Add(Read(block.Rows[i]));
            }
            return all;
        }

        public static void Restore(LogicGatePlusBehaviour block, List<GateData> all,
                                   List<MapperType> touched)
        {
            if (block == null || all == null)
            {
                return;
            }
            for (int i = 0; i < all.Count && i < block.Rows.Count; i++)
            {
                Write(block.Rows[i], all[i], touched);
            }
        }

        /// <summary>Sorts the rows in use by gate. A stable insertion sort, so two
        /// rows on the same gate keep the order somebody put them in.</summary>
        public static void Sort(LogicGatePlusBehaviour block, bool ascending,
                                List<MapperType> touched)
        {
            List<GateData> all = Snapshot(block);
            for (int i = 1; i < all.Count; i++)
            {
                GateData moving = all[i];
                int at = i;
                while (at > 0 && Compare(moving, all[at - 1], ascending) < 0)
                {
                    all[at] = all[at - 1];
                    at--;
                }
                all[at] = moving;
            }
            Restore(block, all, touched);
        }

        /// <summary>Public because it is the whole of what the build can check
        /// offline. See tools/tests/TableCheck.cs.</summary>
        public static int Compare(GateData a, GateData b, bool ascending)
        {
            int order = a.Gate.CompareTo(b.Gate);
            return ascending ? order : -order;
        }

        /// <summary>Adds a row after the last one in use and returns its index, or
        /// -1 when the block is full. A copy of the one above it: a table of gates
        /// is usually a row of the same gate on different keys.</summary>
        public static int Add(LogicGatePlusBehaviour block, List<MapperType> touched)
        {
            if (block == null || block.Count >= LogicGatePlusBehaviour.MaxRows)
            {
                return -1;
            }
            int at = block.Count;
            List<GateData> all = Snapshot(block);
            GateData fresh = new GateData();
            if (all.Count > 0)
            {
                GateData last = all[all.Count - 1];
                fresh.AKey = last.AKey;
                fresh.AVariable = last.AVariable;
                fresh.BKey = last.BKey;
                fresh.BVariable = last.BVariable;
                fresh.Gate = last.Gate;
                fresh.Mode = last.Mode;
                fresh.EmulateKey = last.EmulateKey;
                fresh.EmulateVariable = last.EmulateVariable;
            }
            Write(block.Rows[at], fresh, touched);
            block.Count = at + 1;
            Note(touched, block.CountControl);
            return at;
        }

        /// <summary>Removes one row, closing the gap. The last row's controls go
        /// back to their defaults rather than keeping the values that moved up: a
        /// control at its default is one Besiege leaves out of the save.</summary>
        public static void Remove(LogicGatePlusBehaviour block, int index,
                                  List<MapperType> touched)
        {
            if (block == null || block.Count <= 1)
            {
                return;
            }
            List<GateData> all = Snapshot(block);
            if (index < 0 || index >= all.Count)
            {
                return;
            }
            all.RemoveAt(index);
            all.Add(new GateData());
            Restore(block, all, touched);
            block.Count = all.Count - 1;
            Note(touched, block.CountControl);
        }
    }
}
