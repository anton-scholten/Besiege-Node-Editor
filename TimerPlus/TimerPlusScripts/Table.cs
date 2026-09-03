using System.Collections.Generic;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// One row's settings, lifted out of its mapper controls so they can be moved
    /// about: sorted into another order, shuffled up when a row is deleted, copied
    /// into a real timer block.
    ///
    /// A row *is* its controls, and the controls belong to the block rather than to
    /// the row -- there is a fixed set of them and row three is whichever values
    /// happen to be in the third set. So reordering rows means moving values
    /// between controls, and this is the value.
    /// </summary>
    public class RowData
    {
        public KeyCode ActivateKey = KeyCode.None;
        public string ActivateVariable;

        public KeyCode EmulateKey = KeyCode.C;
        public string EmulateVariable;

        public float Wait = 1f;
        public float Duration = 1f;
        public bool Hold;
        public bool Stop;
        public bool Loop;
    }

    /// <summary>
    /// The table as a whole: reading the rows out of the block, writing them back,
    /// and the three things that rearrange them.
    ///
    /// Everything here goes through the mapper controls, which is what makes an
    /// edit saved with the machine, undoable and sent over multiplayer. Writing
    /// only puts the live value in place; the panel commits afterwards, through
    /// `BlockMapper.OnEditField`, which is what reserialises the block.
    /// </summary>
    public static class Table
    {
        // Which column a sort is on. Constants rather than an enum: declaring an
        // enum segfaults Besiege's own C# compiler.
        public const int ColActivate = 0;
        public const int ColWait = 1;
        public const int ColDuration = 2;
        public const int ColHold = 3;
        public const int ColStop = 4;
        public const int ColLoop = 5;
        public const int ColEmulate = 6;

        public static RowData Read(Row row)
        {
            RowData data = new RowData();
            if (row == null || !row.Ready)
            {
                return data;
            }
            data.ActivateKey = Bindings.Code(row.Activate);
            data.ActivateVariable = Bindings.IsVariable(row.Activate)
                ? Bindings.Variable(row.Activate) : null;
            data.EmulateKey = Bindings.Code(row.Emulate);
            data.EmulateVariable = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            data.Wait = row.Wait.Value;
            data.Duration = row.Duration.Value;
            data.Hold = row.Hold.IsActive;
            data.Stop = row.Stop.IsActive;
            data.Loop = row.Loop.IsActive;
            return data;
        }

        /// <summary>
        /// Writes one row's settings back, and adds every control it touched to
        /// <paramref name="touched"/> so the caller can commit them together.
        /// Assigning `MapperType.Value` writes the live value only: a change that
        /// stops there is heard now and forgotten on save.
        /// </summary>
        public static void Write(Row row, RowData data, List<MapperType> touched)
        {
            if (row == null || !row.Ready || data == null)
            {
                return;
            }

            if (data.ActivateVariable != null)
            {
                Bindings.BindVariable(row.Activate, data.ActivateVariable);
            }
            else
            {
                Bindings.Bind(row.Activate, data.ActivateKey);
            }
            if (data.EmulateVariable != null)
            {
                Bindings.BindVariable(row.Emulate, data.EmulateVariable);
            }
            else
            {
                Bindings.Bind(row.Emulate, data.EmulateKey);
            }

            row.Wait.Value = data.Wait;
            row.Duration.Value = data.Duration;
            row.Hold.IsActive = data.Hold;
            row.Stop.IsActive = data.Stop;
            row.Loop.IsActive = data.Loop;

            if (touched != null)
            {
                Note(touched, row.Activate);
                Note(touched, row.Emulate);
                Note(touched, row.Wait);
                Note(touched, row.Duration);
                Note(touched, row.Hold);
                Note(touched, row.Stop);
                Note(touched, row.Loop);
            }
        }

        /// <summary>Marks one control as needing a commit.</summary>
        private static void Note(List<MapperType> into, MapperType one)
        {
            if (into != null && one != null && !into.Contains(one))
            {
                into.Add(one);
            }
        }

        /// <summary>Every row in use, as values.</summary>
        public static List<RowData> Snapshot(TimerPlusBehaviour block)
        {
            List<RowData> all = new List<RowData>();
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

        /// <summary>Puts a list of values back into the rows, in order.</summary>
        public static void Restore(TimerPlusBehaviour block, List<RowData> all,
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

        // ---- rearranging -----------------------------------------------------

        /// <summary>
        /// Sorts the rows in use by one column.
        ///
        /// An insertion sort, which is what a list this short wants and which is
        /// **stable** -- so sorting by Loop and then by Wait gives Wait within
        /// Loop, and two rows that tie keep the order somebody put them in. A
        /// quicksort would scramble the ties, and on a table this is the difference
        /// between a sort and a shuffle.
        /// </summary>
        public static void Sort(TimerPlusBehaviour block, int column, bool ascending,
                                List<MapperType> touched)
        {
            List<RowData> all = Snapshot(block);
            for (int i = 1; i < all.Count; i++)
            {
                RowData moving = all[i];
                int at = i;
                while (at > 0 && Compare(moving, all[at - 1], column, ascending) < 0)
                {
                    all[at] = all[at - 1];
                    at--;
                }
                all[at] = moving;
            }
            Restore(block, all, touched);
        }

        /// <summary>
        /// Which of two rows comes first on a column. Negative if
        /// <paramref name="a"/> does.
        ///
        /// A switch column sorts the rows that are on to the top when ascending,
        /// which is what somebody clicking "Loop" is asking to see. Ties are
        /// answered 0 rather than broken by another column, so the sort's own
        /// stability decides them.
        ///
        /// Public because it is the whole of what the build can check offline:
        /// everything else here needs a live block. See tools/tests/TableCheck.cs.
        /// </summary>
        public static int Compare(RowData a, RowData b, int column, bool ascending)
        {
            switch (column)
            {
                case ColWait:
                    return Turn(a.Wait.CompareTo(b.Wait), ascending);
                case ColDuration:
                    return Turn(a.Duration.CompareTo(b.Duration), ascending);
                case ColHold:
                    return Turn(Flag(a.Hold, b.Hold), ascending);
                case ColStop:
                    return Turn(Flag(a.Stop, b.Stop), ascending);
                case ColLoop:
                    return Turn(Flag(a.Loop, b.Loop), ascending);
                case ColEmulate:
                    return Text(a.EmulateVariable, a.EmulateKey,
                                b.EmulateVariable, b.EmulateKey, ascending);
                default:
                    return Text(a.ActivateVariable, a.ActivateKey,
                                b.ActivateVariable, b.ActivateKey, ascending);
            }
        }

        private static int Turn(int order, bool ascending)
        {
            return ascending ? order : -order;
        }

        private static int Flag(bool a, bool b)
        {
            if (a == b)
            {
                return 0;
            }
            return a ? -1 : 1;
        }

        /// <summary>
        /// A key column's order: unbound rows last **however the column is
        /// sorted**, then variables and keycodes by name.
        ///
        /// An unbound row is not a small value on the column, it is a row the
        /// column says nothing about, so letting it float to whichever end is being
        /// looked at is the more useful answer. That is why the direction is applied
        /// here rather than by the caller: negating the whole comparison would take
        /// the unbound rule with it and put the blanks at the top on every reverse.
        /// </summary>
        private static int Text(string variableA, KeyCode keyA,
                                string variableB, KeyCode keyB, bool ascending)
        {
            string a = variableA != null ? variableA
                : (keyA == KeyCode.None ? "" : keyA.ToString());
            string b = variableB != null ? variableB
                : (keyB == KeyCode.None ? "" : keyB.ToString());
            if (a.Length == 0 || b.Length == 0)
            {
                return a.Length == b.Length ? 0 : (a.Length == 0 ? 1 : -1);
            }
            return Turn(string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase),
                        ascending);
        }

        /// <summary>
        /// Adds a row after the last one in use and returns its index, or -1 when
        /// the block is full. The new row is a copy of the one above it with its
        /// wait pushed on by the same gap as the last two -- so a sequence being
        /// typed in carries on rather than starting again from a default.
        /// </summary>
        public static int Add(TimerPlusBehaviour block, List<MapperType> touched)
        {
            if (block == null || block.Count >= TimerPlusBehaviour.MaxRows)
            {
                return -1;
            }
            int at = block.Count;
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
                fresh.ActivateKey = last.ActivateKey;
                fresh.ActivateVariable = last.ActivateVariable;
                fresh.EmulateKey = last.EmulateKey;
                fresh.EmulateVariable = last.EmulateVariable;
                fresh.Duration = last.Duration;
                fresh.Hold = last.Hold;
                fresh.Stop = last.Stop;
                fresh.Loop = last.Loop;
                fresh.Wait = last.Wait + step;
            }
            Write(block.Rows[at], fresh, touched);
            block.Count = at + 1;
            Note(touched, block.CountControl);
            return at;
        }

        /// <summary>
        /// Removes one row, closing the gap. The last row's controls are reset to
        /// their defaults rather than left holding the values that have moved up --
        /// a control at its default is one Besiege leaves out of the save, and one
        /// still holding a copy of row 31 is a row that reappears if the count is
        /// raised again.
        /// </summary>
        public static void Remove(TimerPlusBehaviour block, int index,
                                  List<MapperType> touched)
        {
            if (block == null || block.Count <= 1)
            {
                return;
            }
            List<RowData> all = Snapshot(block);
            if (index < 0 || index >= all.Count)
            {
                return;
            }
            all.RemoveAt(index);
            all.Add(new RowData());
            Restore(block, all, touched);
            block.Count = all.Count - 1;
            Note(touched, block.CountControl);
        }
    }
}
