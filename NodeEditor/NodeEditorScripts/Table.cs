using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>One Timer Plus row's settings. The block keeps these as its own
    /// data, written out as text (<see cref="Table.Save"/>), not as mapper
    /// controls.</summary>
    public class RowData
    {
        /// <summary>Every keycode the row presses, in order; empty for none. Not
        /// only the first: see <see cref="GateData"/>.</summary>
        public KeyCode[] EmulateKeys = { KeyCode.C };

        /// <summary>The names it presses instead, or null.</summary>
        public string EmulateVariable;

        public float Wait = 1f;
        public float Duration = 1f;
        public bool Hold;
        public bool Stop;
        public bool Loop;

        public RowData Copy()
        {
            RowData made = new RowData();
            made.EmulateKeys = EmulateKeys == null
                ? new KeyCode[0] : (KeyCode[])EmulateKeys.Clone();
            made.EmulateVariable = EmulateVariable;
            made.Wait = Wait;
            made.Duration = Duration;
            made.Hold = Hold;
            made.Stop = Stop;
            made.Loop = Loop;
            return made;
        }
    }

    /// <summary>The Timer Plus table: rows as values, rearranged and written back
    /// to the block as one text control, which the panel commits through
    /// `BlockMapper.OnEditField` so an edit is saved, undoable and synced.</summary>
    public static class Table
    {
        // Sort columns, as constants (an enum segfaults Besiege's compiler). Only
        // the two number columns sort.
        public const int ColWait = 0;
        public const int ColDuration = 1;

        /// <summary>Marks one control as needing a commit.</summary>
        internal static void Note(List<MapperType> into, MapperType one)
        {
            if (into != null && one != null && !into.Contains(one))
            {
                into.Add(one);
            }
        }

        /// <summary>Every row, as copies: changing one changes nothing until it is
        /// stored.</summary>
        public static List<RowData> Snapshot(TimerPlusBehaviour block)
        {
            List<RowData> all = new List<RowData>();
            if (block == null)
            {
                return all;
            }
            List<RowData> rows = block.Data;
            for (int i = 0; i < rows.Count; i++)
            {
                all.Add(rows[i].Copy());
            }
            return all;
        }

        /// <summary>Makes a list the block's rows, noting its text control for one
        /// commit.</summary>
        public static void Store(TimerPlusBehaviour block, List<RowData> all,
                                 List<MapperType> touched)
        {
            if (block == null || all == null)
            {
                return;
            }
            block.Store(all);
            Note(touched, block.TableControl);
        }

        // ---- rearranging -----------------------------------------------------

        /// <summary>Sorts the rows by a column, stably: ties keep their order.
        /// </summary>
        public static void Sort(TimerPlusBehaviour block, int column, bool ascending,
                                List<MapperType> touched)
        {
            List<RowData> all = Snapshot(block);
            Stable(all, delegate(RowData a, RowData b)
            {
                return Compare(a, b, column, ascending);
            });
            Store(block, all, touched);
        }

        /// <summary>An insertion sort, which is stable: ties keep their order.
        /// Shared by both tables and the conversion.</summary>
        internal static void Stable<T>(List<T> all, Comparison<T> order)
        {
            for (int i = 1; i < all.Count; i++)
            {
                T moving = all[i];
                int at = i;
                while (at > 0 && order(moving, all[at - 1]) < 0)
                {
                    all[at] = all[at - 1];
                    at--;
                }
                all[at] = moving;
            }
        }

        /// <summary>Which row comes first on a column; negative when <paramref
        /// name="a"/> does. Ties are 0, left to the stable sort. Public for
        /// TableCheck.</summary>
        public static int Compare(RowData a, RowData b, int column, bool ascending)
        {
            int order = column == ColDuration
                ? a.Duration.CompareTo(b.Duration)
                : a.Wait.CompareTo(b.Wait);
            return ascending ? order : -order;
        }

        /// <summary>Adds a row at the end, a copy of the last with its wait moved on
        /// by the last gap; -1 when full.</summary>
        public static int Add(TimerPlusBehaviour block, List<MapperType> touched)
        {
            if (block == null || block.Count >= TimerPlusBehaviour.MaxRows)
            {
                return -1;
            }
            List<RowData> all = Snapshot(block);
            RowData fresh = new RowData();
            if (all.Count > 0)
            {
                RowData last = all[all.Count - 1];
                float step = all.Count > 1 ? last.Wait - all[all.Count - 2].Wait : 1f;
                if (step <= 0f)
                {
                    step = 1f;
                }
                fresh = last.Copy();
                fresh.Wait = last.Wait + step;
            }
            all.Add(fresh);
            Store(block, all, touched);
            return all.Count - 1;
        }

        /// <summary>Removes a row and closes the gap.</summary>
        public static void Remove(TimerPlusBehaviour block, int index,
                                  List<MapperType> touched)
        {
            List<RowData> all = Snapshot(block);
            if (index < 0 || index >= all.Count)
            {
                return;
            }
            all.RemoveAt(index);
            Store(block, all, touched);
        }

        // ---- the text --------------------------------------------------------

        /// <summary>The first line, for telling a later format from this one.
        /// </summary>
        private const string Header = "timers 1";

        /// <summary>
        /// The rows as the block saves them: a line each, fields apart by a space --
        /// the wait and the duration as exact decimals, the three switches as
        /// `hsl` with `-` for off, then `k` and the keycodes apart by commas (`-`
        /// for none) or `v` and the names. The binding ends the line, so a name may
        /// hold a space. Public for TableCheck.
        /// </summary>
        public static string Save(List<RowData> rows)
        {
            StringBuilder text = new StringBuilder(Header).Append('\n');
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                RowData row = rows[i];
                text.Append(Decimal(row.Wait)).Append(' ')
                    .Append(Decimal(row.Duration)).Append(' ')
                    .Append(row.Hold ? 'h' : '-')
                    .Append(row.Stop ? 's' : '-')
                    .Append(row.Loop ? 'l' : '-').Append(' ');
                if (!string.IsNullOrEmpty(row.EmulateVariable))
                {
                    text.Append("v ")
                        .Append(row.EmulateVariable.Replace('\n', ' ').Replace('\r', ' '));
                }
                else
                {
                    text.Append("k ");
                    int written = 0;
                    for (int k = 0; row.EmulateKeys != null && k < row.EmulateKeys.Length; k++)
                    {
                        if (row.EmulateKeys[k] == KeyCode.None)
                        {
                            continue;
                        }
                        if (written++ > 0)
                        {
                            text.Append(',');
                        }
                        text.Append(row.EmulateKeys[k].ToString());
                    }
                    if (written == 0)
                    {
                        text.Append('-');
                    }
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        /// <summary>Reads rows back, skipping any line it does not understand rather
        /// than throwing, and stopping at <see
        /// cref="TimerPlusBehaviour.MaxRows"/>.</summary>
        public static List<RowData> Load(string text)
        {
            List<RowData> rows = new List<RowData>();
            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length && rows.Count < TimerPlusBehaviour.MaxRows;
                 i++)
            {
                string[] parts = lines[i].TrimEnd('\r').Split(' ');
                float wait;
                float duration;
                if (parts.Length < 5 || parts[2].Length != 3
                    || (parts[3] != "k" && parts[3] != "v")
                    || !Parsed(parts[0], out wait) || !Parsed(parts[1], out duration))
                {
                    continue;
                }
                RowData row = new RowData();
                row.Wait = Mathf.Max(0f, wait);
                row.Duration = Mathf.Max(0f, duration);
                row.Hold = parts[2][0] == 'h';
                row.Stop = parts[2][1] == 's';
                row.Loop = parts[2][2] == 'l';
                string said = string.Join(" ", parts, 4, parts.Length - 4);
                row.EmulateKeys = new KeyCode[0];
                if (parts[3] == "v")
                {
                    row.EmulateVariable = said.Length > 0 ? said : null;
                }
                else
                {
                    List<KeyCode> codes = new List<KeyCode>();
                    string[] names = said.Split(',');
                    for (int n = 0; n < names.Length; n++)
                    {
                        KeyCode code = Bindings.Parse(names[n]);
                        if (code != KeyCode.None && !codes.Contains(code))
                        {
                            codes.Add(code);
                        }
                    }
                    row.EmulateKeys = codes.ToArray();
                }
                rows.Add(row);
            }
            return rows;
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
