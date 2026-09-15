using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>One logic row's settings as values, to sort, shift, save and
    /// export.</summary>
    public class GateData
    {
        /// <summary>Every keycode each key holds, in order; keeping only the first
        /// lost the rest on sort, delete and export.</summary>
        public KeyCode[] AKeys = { KeyCode.U };
        public string AVariable;

        public KeyCode[] BKeys = { KeyCode.I };
        public string BVariable;

        public int Gate;
        public bool Mode;

        public KeyCode[] EmulateKeys = { KeyCode.C };
        public string EmulateVariable;

        /// <summary>A timer row's own settings, carried by every row as the row
        /// carries them.</summary>
        public float Wait = 1f;
        public float Duration = 1f;
        public bool HoldRun;
        public bool CanStop;
        public bool Loops;
    }

    /// <summary>
    /// The logic table: rows read from and written to the block, and rearranged.
    /// The rows are the block's own data (see <see cref="ComputerBehaviour"/>):
    /// an edit changes a row's control objects, and <see cref="Settle"/> turns that
    /// into a commit of the block's one text control, so edits are saved,
    /// undoable and synced.
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
            data.AKeys = Bindings.Codes(row.InputA).ToArray();
            data.AVariable = Bindings.IsVariable(row.InputA)
                ? Bindings.Variable(row.InputA) : null;
            data.BKeys = Bindings.Codes(row.InputB).ToArray();
            data.BVariable = Bindings.IsVariable(row.InputB)
                ? Bindings.Variable(row.InputB) : null;
            data.Gate = row.Gate;
            data.Mode = row.Switch;
            data.EmulateKeys = Bindings.Codes(row.Emulate).ToArray();
            data.EmulateVariable = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            data.Wait = row.Wait.Value;
            data.Duration = row.Duration.Value;
            data.HoldRun = row.Hold.IsActive;
            data.CanStop = row.Stop.IsActive;
            data.Loops = row.Loop.IsActive;
            return data;
        }

        public static void Write(LogicRow row, GateData data, List<MapperType> touched)
        {
            if (row == null || !row.Ready || data == null)
            {
                return;
            }
            Bind(row.InputA, data.AVariable, data.AKeys);
            Bind(row.InputB, data.BVariable, data.BKeys);
            Bind(row.Emulate, data.EmulateVariable, data.EmulateKeys);
            row.Kind.Value = Mathf.Clamp(data.Gate, 0, Gates.Kinds - 1);
            row.Mode.IsActive = data.Mode;
            row.Wait.Value = data.Wait;
            row.Duration.Value = data.Duration;
            row.Hold.IsActive = data.HoldRun;
            row.Stop.IsActive = data.CanStop;
            row.Loop.IsActive = data.Loops;

            if (touched != null)
            {
                Table.Note(touched, row.InputA);
                Table.Note(touched, row.InputB);
                Table.Note(touched, row.Kind);
                Table.Note(touched, row.Mode);
                Table.Note(touched, row.Emulate);
                Table.Note(touched, row.Wait);
                Table.Note(touched, row.Duration);
                Table.Note(touched, row.Hold);
                Table.Note(touched, row.Stop);
                Table.Note(touched, row.Loop);
            }
        }

        private static void Bind(MKey key, string variable, KeyCode[] codes)
        {
            if (variable != null)
            {
                Bindings.BindVariable(key, variable);
            }
            else
            {
                Bindings.Bind(key, codes);
            }
        }

        /// <summary>Removes a row, and the wires into other rows that only it
        /// answered; names another row still presses are left.</summary>
        public static void Erase(ComputerBehaviour block, int index,
                                 List<MapperType> touched)
        {
            if (block == null || index < 0 || index >= block.Count)
            {
                return;
            }
            // What the row answered to, read before it is gone.
            List<string> names = new List<string>();
            List<KeyCode> codes = new List<KeyCode>();
            LogicRow going = block.Rows[index];
            if (going != null && going.Ready)
            {
                if (Bindings.IsVariable(going.Emulate))
                {
                    names = Bindings.Named(Bindings.Variable(going.Emulate));
                }
                else
                {
                    codes = Bindings.Codes(going.Emulate);
                }
            }
            Remove(block, index, touched);
            List<LogicRow> rows = block.Rows;
            // Which of those the rows left still answer to, gathered once rather
            // than asked of every row for every input.
            Dictionary<string, bool> stillNamed = new Dictionary<string, bool>();
            Dictionary<int, bool> stillCoded = new Dictionary<int, bool>();
            for (int i = 0; i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                if (row == null || !row.Ready)
                {
                    continue;
                }
                if (Bindings.IsVariable(row.Emulate))
                {
                    List<string> said = Bindings.Named(Bindings.Variable(row.Emulate));
                    for (int n = 0; n < said.Count; n++)
                    {
                        stillNamed[said[n]] = true;
                    }
                }
                else
                {
                    List<KeyCode> pressed = Bindings.Codes(row.Emulate);
                    for (int c = 0; c < pressed.Count; c++)
                    {
                        stillCoded[(int)pressed[c]] = true;
                    }
                }
            }
            for (int i = 0; i < rows.Count; i++)
            {
                LogicRow row = rows[i];
                if (row == null || !row.Ready)
                {
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    // Only this row's names and keys that nothing answers now;
                    // other wires stay.
                    MKey input = port == 0 ? row.InputA : row.InputB;
                    bool cut = false;
                    for (int n = 0; n < names.Count; n++)
                    {
                        if (!stillNamed.ContainsKey(names[n])
                            && Bindings.Holds(input, names[n]))
                        {
                            Bindings.Dropped(input, names[n]);
                            cut = true;
                        }
                    }
                    for (int c = 0; c < codes.Count; c++)
                    {
                        if (!stillCoded.ContainsKey((int)codes[c])
                            && Bindings.Holds(input, codes[c]))
                        {
                            Bindings.Dropped(input, codes[c]);
                            cut = true;
                        }
                    }
                    if (cut)
                    {
                        Table.Note(touched, input);
                    }
                }
            }
        }

        // ---- committing ------------------------------------------------------

        /// <summary>
        /// Turns a list of changed controls into what a commit is: a row's control
        /// objects are not the block's mapper controls, so they are taken off the
        /// list, and the rows' text goes on it when the rows changed. Everything
        /// that commits a Computer edit calls this first.
        /// </summary>
        public static void Settle(ComputerBehaviour block, List<MapperType> changed)
        {
            if (block == null || changed == null)
            {
                return;
            }
            List<MapperType> registered = block.BlockBehaviour == null
                ? null : block.BlockBehaviour.MapperTypes;
            for (int i = changed.Count - 1; i >= 0; i--)
            {
                if (changed[i] == null || registered == null
                    || !registered.Contains(changed[i]))
                {
                    changed.RemoveAt(i);
                }
            }
            if (block.Stored() && block.RowsControl != null
                && !changed.Contains(block.RowsControl))
            {
                changed.Add(block.RowsControl);
            }
        }

        /// <summary>The block as it stands before a multi-control edit, so one undo
        /// puts it back. Null while another player listens: Besiege's handler files
        /// steps then.
        /// </summary>
        public static BlockInfo Marked(ComputerBehaviour block)
        {
            return Marked(block == null ? null : block.BlockBehaviour);
        }

        /// <summary>The same for any block: a Timer Plus import files one.</summary>
        public static BlockInfo Marked(BlockBehaviour block)
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

        /// <summary>Settles a Computer block's changed controls, then applies
        /// them.</summary>
        public static void Applied(ComputerBehaviour block, List<MapperType> changed)
        {
            Settle(block, changed);
            Applied(changed);
        }

        /// <summary>Settles changed controls: `ApplyValue`, or Besiege's edit
        /// handler while another player listens (see <see
        /// cref="Marked"/>).</summary>
        public static void Applied(List<MapperType> changed)
        {
            if (changed == null)
            {
                return;
            }
            for (int i = 0; i < changed.Count; i++)
            {
                Apply(changed[i]);
            }
        }

        /// <summary>One of them.</summary>
        public static void Apply(MapperType changed)
        {
            if (changed == null)
            {
                return;
            }
            try
            {
                if (EditFieldHandler.Instance != null)
                {
                    BlockMapper mapper = BlockMapper.CurrentInstance;
                    if (mapper != null && BlockMapper.IsOpen
                        && mapper.Current != null)
                    {
                        BlockMapper.OnEditField(mapper.Current, changed);
                        return;
                    }
                }
                changed.ApplyValue();
            }
            catch (Exception)
            {
                // The value is written either way; this is the reconciliation.
            }
        }

        /// <summary>Files what has happened since that snapshot as one step of
        /// Besiege's undo.</summary>
        public static void Filed(ComputerBehaviour block, BlockInfo before)
        {
            if (before == null)
            {
                return;
            }
            try
            {
                Written(block);
                Machine machine = Machine.Active();
                BlockInfo after = Snap(block == null ? null : block.BlockBehaviour);
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

        /// <summary>That undo step, returned rather than filed, for an edit that is
        /// half of a bigger one: an import.</summary>
        public static UndoAction Edited(ComputerBehaviour block,
                                        BlockInfo before)
        {
            Written(block);
            return Edited(block == null ? null : block.BlockBehaviour, before);
        }

        public static UndoAction Edited(BlockBehaviour block, BlockInfo before)
        {
            if (before == null)
            {
                return null;
            }
            try
            {
                Machine machine = Machine.Active();
                BlockInfo after = Snap(block);
                if (machine == null || after == null)
                {
                    return null;
                }
                return new UndoActionEdit(machine, after, before);
            }
            catch (Exception e)
            {
                Log.Warn("could not make the undo step: " + e.Message);
                return null;
            }
        }

        /// <summary>The rows written into their text, and the text applied, before
        /// a snapshot is taken of the block.</summary>
        private static void Written(ComputerBehaviour block)
        {
            if (block != null && block.Stored() && block.RowsControl != null)
            {
                Apply(block.RowsControl);
            }
        }

        private static BlockInfo Snap(BlockBehaviour body)
        {
            if (body == null)
            {
                return null;
            }
            // `FromBlockBehaviour` returns the last saved state: save first, or it
            // is stale.
            body.OnSave(new XDataHolder());
            return BlockInfo.FromBlockBehaviour(body);
        }

        // ---- rearranging -----------------------------------------------------

        /// <summary>Every row, as values.</summary>
        public static List<GateData> Snapshot(ComputerBehaviour block)
        {
            List<GateData> all = new List<GateData>();
            if (block == null)
            {
                return all;
            }
            List<LogicRow> rows = block.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                all.Add(Read(rows[i]));
            }
            return all;
        }

        /// <summary>Makes the rows these values, as many as there are.</summary>
        public static void Restore(ComputerBehaviour block, List<GateData> all,
                                   List<MapperType> touched)
        {
            if (block == null || all == null)
            {
                return;
            }
            block.Count = all.Count;
            List<LogicRow> rows = block.Rows;
            for (int i = 0; i < all.Count && i < rows.Count; i++)
            {
                Write(rows[i], all[i], touched);
            }
        }

        /// <summary>Sorts the rows by gate. A stable insertion sort, so two rows on
        /// the same gate keep the order somebody put them in.</summary>
        public static void Sort(ComputerBehaviour block, bool ascending,
                                List<MapperType> touched)
        {
            List<GateData> all = Snapshot(block);
            Table.Stable(all, delegate(GateData a, GateData b)
            {
                return Compare(a, b, ascending);
            });
            Restore(block, all, touched);
        }

        /// <summary>Public because it is the whole of what the build can check
        /// offline. See tools/tests/TableCheck.cs.</summary>
        public static int Compare(GateData a, GateData b, bool ascending)
        {
            int order = a.Gate.CompareTo(b.Gate);
            return ascending ? order : -order;
        }

        /// <summary>Adds a row at the end, a copy of the last but answering
        /// nothing: a copied output would be two gates on one key. Returns its
        /// index, or -1 when full.</summary>
        public static int Add(ComputerBehaviour block, List<MapperType> touched)
        {
            if (block == null || block.Count >= ComputerBehaviour.MaxRows)
            {
                return -1;
            }
            int at = block.Count;
            GateData fresh = new GateData();
            if (at > 0)
            {
                GateData last = Read(block.Rows[at - 1]);
                fresh.AKeys = last.AKeys;
                fresh.AVariable = last.AVariable;
                fresh.BKeys = last.BKeys;
                fresh.BVariable = last.BVariable;
                fresh.Gate = last.Gate;
                fresh.Mode = last.Mode;
                fresh.Wait = last.Wait;
                fresh.Duration = last.Duration;
                fresh.HoldRun = last.HoldRun;
                fresh.CanStop = last.CanStop;
                fresh.Loops = last.Loops;
            }
            fresh.EmulateKeys = new KeyCode[0];
            block.Count = at + 1;
            Write(block.Rows[at], fresh, touched);
            return at;
        }

        /// <summary>Removes a row and closes the gap.</summary>
        public static void Remove(ComputerBehaviour block, int index,
                                  List<MapperType> touched)
        {
            if (block == null || index < 0 || index >= block.Count)
            {
                return;
            }
            block.Rows.RemoveAt(index);
        }

        // ---- the text --------------------------------------------------------

        /// <summary>The first line, for telling a later format from this one.
        /// </summary>
        private const string Header = "gates 1";

        /// <summary>
        /// The rows as the block saves them: a line each, fields apart by a tab --
        /// the gate, its switch (0 or 1), the wait and the duration as exact
        /// decimals, the timer's switches as `hsl` with `-` for off, then input A,
        /// input B and the output, each `-` for nothing, `k:` and keycodes apart by
        /// commas, or `v:` and the names. A name may hold a space; a tab or a line
        /// break in one is written as a space. Public for TableCheck.
        /// </summary>
        public static string Save(List<GateData> rows)
        {
            StringBuilder text = new StringBuilder(Header).Append('\n');
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                GateData row = rows[i];
                text.Append(row.Gate.ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(row.Mode ? '1' : '0')
                    .Append('\t').Append(Decimal(row.Wait))
                    .Append('\t').Append(Decimal(row.Duration))
                    .Append('\t').Append(row.HoldRun ? 'h' : '-')
                    .Append(row.CanStop ? 's' : '-')
                    .Append(row.Loops ? 'l' : '-')
                    .Append('\t').Append(Spelt(row.AVariable, row.AKeys))
                    .Append('\t').Append(Spelt(row.BVariable, row.BKeys))
                    .Append('\t').Append(Spelt(row.EmulateVariable, row.EmulateKeys))
                    .Append('\n');
            }
            return text.ToString();
        }

        /// <summary>Reads rows back, skipping any line it does not understand rather
        /// than throwing, and stopping at <see
        /// cref="ComputerBehaviour.MaxRows"/>.</summary>
        public static List<GateData> Load(string text)
        {
            List<GateData> rows = new List<GateData>();
            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length && rows.Count < ComputerBehaviour.MaxRows;
                 i++)
            {
                string[] parts = lines[i].TrimEnd('\r').Split('\t');
                int gate;
                float wait;
                float duration;
                if (parts.Length < 8 || parts[4].Length != 3
                    || !int.TryParse(parts[0], NumberStyles.Integer,
                                     CultureInfo.InvariantCulture, out gate)
                    || !Parsed(parts[2], out wait) || !Parsed(parts[3], out duration))
                {
                    continue;
                }
                GateData row = new GateData();
                row.Gate = Mathf.Clamp(gate, 0, Gates.Kinds - 1);
                row.Mode = parts[1] == "1";
                row.Wait = Mathf.Max(0f, wait);
                row.Duration = Mathf.Max(0f, duration);
                row.HoldRun = parts[4][0] == 'h';
                row.CanStop = parts[4][1] == 's';
                row.Loops = parts[4][2] == 'l';
                Unspelt(parts[5], out row.AVariable, out row.AKeys);
                Unspelt(parts[6], out row.BVariable, out row.BKeys);
                Unspelt(parts[7], out row.EmulateVariable, out row.EmulateKeys);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>One binding as a field: `-`, `k:` keycodes, or `v:` names.
        /// </summary>
        private static string Spelt(string variable, KeyCode[] codes)
        {
            if (!string.IsNullOrEmpty(variable))
            {
                return "v:" + variable.Replace('\t', ' ').Replace('\n', ' ')
                                      .Replace('\r', ' ');
            }
            StringBuilder said = new StringBuilder();
            for (int i = 0; codes != null && i < codes.Length; i++)
            {
                if (codes[i] == KeyCode.None)
                {
                    continue;
                }
                said.Append(said.Length == 0 ? "k:" : ",").Append(codes[i].ToString());
            }
            return said.Length == 0 ? "-" : said.ToString();
        }

        private static void Unspelt(string field, out string variable, out KeyCode[] codes)
        {
            variable = null;
            codes = new KeyCode[0];
            if (field.StartsWith("v:"))
            {
                string names = field.Substring(2);
                variable = names.Length > 0 ? names : null;
                return;
            }
            if (!field.StartsWith("k:"))
            {
                return;
            }
            List<KeyCode> read = new List<KeyCode>();
            string[] names2 = field.Substring(2).Split(',');
            for (int i = 0; i < names2.Length; i++)
            {
                KeyCode code = Bindings.Parse(names2[i]);
                if (code != KeyCode.None && !read.Contains(code))
                {
                    read.Add(code);
                }
            }
            codes = read.ToArray();
        }

        /// <summary>A number written so it reads back exactly.</summary>
        private static string Decimal(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool Parsed(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                                  out value);
        }
    }
}
