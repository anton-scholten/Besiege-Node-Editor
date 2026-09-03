using System;
using System.Collections.Generic;
using TimerPlusMod;
using UnityEngine;

/// <summary>
/// Exercises the parts of the mod that touch no Unity object: the timer state
/// machine and the table's ordering.
///
/// Those are worth a check precisely because their failure is silent in game: a
/// sort that scrambles ties, or a delete that shuffles the wrong row, does not
/// throw and does not log -- it looks like a mod that lost your settings. The
/// rest of the mod cannot be run outside Besiege at all, so this is the whole of
/// what a build can prove.
///
/// The comparison and the row arithmetic are reached through a stand-in for the
/// block's controls, because the real ones are `MapperType`s that only exist on a
/// live block. That is why <see cref="Table"/> keeps its sorting on `RowData`
/// rather than on the controls themselves.
/// </summary>
static class TableCheck
{
    static int bad;

    public static int Main()
    {
        Sorting();
        Stability();
        Descending();
        Unbound();
        Spelling();
        Variables();
        Ticks();
        Firing();
        Looping();
        Instant();
        Stopping();
        Holding();
        Converting();

        if (bad > 0)
        {
            Console.Error.WriteLine(bad + " check(s) failed.");
            return 1;
        }
        Console.WriteLine("Offline check: timer ticks, phases, sort order, ties and key\n"
            + "               names all as the game's own timer has them.");
        return 0;
    }

    // ---- the checks ------------------------------------------------------

    static void Sorting()
    {
        List<RowData> rows = Rows(3f, 1f, 2f);
        Sort(rows, Table.ColWait, true);
        Same("waits sort ascending", "1,2,3", Waits(rows));
    }

    /// <summary>
    /// The property that makes a second sort useful: rows that tie on the column
    /// keep the order they were already in, so sorting by Loop after sorting by
    /// Wait gives Wait within Loop rather than a reshuffle.
    /// </summary>
    static void Stability()
    {
        List<RowData> rows = Rows(1f, 2f, 3f, 4f);
        rows[0].Loop = true;
        rows[2].Loop = true;
        Sort(rows, Table.ColLoop, true);
        Same("a tie keeps the order it was in", "1,3,2,4", Waits(rows));
    }

    static void Descending()
    {
        List<RowData> rows = Rows(1f, 3f, 2f);
        Sort(rows, Table.ColWait, false);
        Same("waits sort descending", "3,2,1", Waits(rows));
    }

    /// <summary>
    /// An unbound key is not a small value on its column, it is a row the column
    /// says nothing about -- so it goes last whichever way the column is sorted,
    /// rather than heading the list every time it is reversed.
    /// </summary>
    static void Unbound()
    {
        List<RowData> rows = Rows(1f, 2f, 3f);
        rows[0].EmulateKey = KeyCode.None;
        rows[1].EmulateKey = KeyCode.Z;
        rows[2].EmulateKey = KeyCode.A;

        Sort(rows, Table.ColEmulate, true);
        Same("unbound last, ascending", "3,2,1", Waits(rows));

        rows = Rows(1f, 2f, 3f);
        rows[0].EmulateKey = KeyCode.None;
        rows[1].EmulateKey = KeyCode.Z;
        rows[2].EmulateKey = KeyCode.A;
        Sort(rows, Table.ColEmulate, false);
        Same("unbound last, descending", "2,3,1", Waits(rows));
    }

    /// <summary>What a cell shows for a keycode. These are read at a glance in a
    /// column sixty units wide, so the shortening is part of the feature.</summary>
    static void Spelling()
    {
        Same("a letter", "B", Bindings.Spell(KeyCode.B));
        Same("a digit", "4", Bindings.Spell(KeyCode.Alpha4));
        Same("a keypad digit", "#4", Bindings.Spell(KeyCode.Keypad4));
        Same("a modifier", "LShift", Bindings.Spell(KeyCode.LeftShift));
        Same("an arrow", "Up", Bindings.Spell(KeyCode.UpArrow));
        Same("a mouse button", "M0", Bindings.Spell(KeyCode.Mouse0));
        Same("something plain", "Space", Bindings.Spell(KeyCode.Space));
    }

    /// <summary>
    /// A variable name reaches a save as `Message=a;b`, so a name carrying a
    /// semicolon would be read back as two. The split and the join are Besiege's
    /// own, and this is here to catch a change in them.
    /// </summary>
    static void Variables()
    {
        Same("one name", "fire", MKey.CombineVariables(MKey.SplitVariable("fire")));
        Same("two names", "a;b", MKey.CombineVariables(MKey.SplitVariable("a;b")));
    }

    /// <summary>
    /// How a generated timer block's keys reach the save.
    ///
    /// The format is Besiege's: an entry per keycode, then `Message=` and
    /// `Use=True` where the key answers a variable. Wrong here and the timer loads
    /// with the wrong binding rather than failing, which is why it is worth a
    /// check the build runs.
    /// </summary>
    static void Converting()
    {
        Same("a plain key", "B",
             string.Join("|", Conversion.Spell(null, KeyCode.B, KeyCode.C)));

        // Not the spare: an unbound row must not become a timer quietly pressing
        // whatever the spare happens to be.
        Same("nothing bound stays nothing", "None",
             string.Join("|", Conversion.Spell(null, KeyCode.None, KeyCode.C)));

        // A keycode goes in behind the name so InitSimBlock counts the key and
        // registers it; with Use=True it is never answered to.
        Same("a variable, with a keycode to be counted", "C|Message=fire|Use=True",
             string.Join("|", Conversion.Spell("fire", KeyCode.None, KeyCode.C)));
        Same("a variable keeps the row's own keycode", "T|Message=fire|Use=True",
             string.Join("|", Conversion.Spell("fire", KeyCode.T, KeyCode.C)));
        Same("two names", "C|Message=a;b|Use=True",
             string.Join("|", Conversion.Spell("a;b", KeyCode.None, KeyCode.C)));
    }

    // ---- the clock -------------------------------------------------------

    /// <summary>
    /// Seconds to emulation ticks. `TimerBlock.TimeToFrameCount` is
    /// `max(1, ceil(seconds * 50))`, and these are the numbers that decide whether
    /// a row fires at the same moment as the real timer beside it.
    /// </summary>
    static void Ticks()
    {
        Count("a second", 50, Clock.Ticks(1f));
        Count("a fifth of a second", 10, Clock.Ticks(0.2f));
        Count("nought seconds still takes a tick", 1, Clock.Ticks(0f));
        Count("a tick and a bit rounds up", 2, Clock.Ticks(0.021f));
        Count("four minutes", 12000, Clock.Ticks(240f));
    }

    /// <summary>
    /// When the key goes down and when it comes up. A wait of n ticks is spent
    /// counting, and the press begins on the tick after -- which is the tick the
    /// game's own timer presses on, because `ElapseDelay` falls straight into
    /// `ElapseEmulation` rather than waiting for the next one.
    /// </summary>
    static void Firing()
    {
        Row row = new Row();
        Clock.Start(row);
        Count("a 0.1s wait presses on tick 6", 6, Down(row, 0.1f, 0.1f, false, 50));

        row = new Row();
        Clock.Start(row);
        Run(row, 0.1f, 0.1f, false, 5);
        Same("nothing is pressed while it waits", "False", row.Wants.ToString());
        Run(row, 0.1f, 0.1f, false, 1);
        Same("pressed on the sixth", "True", row.Wants.ToString());
        Run(row, 0.1f, 0.1f, false, 4);
        Same("still pressed four ticks later", "True", row.Wants.ToString());
        Run(row, 0.1f, 0.1f, false, 1);
        Same("released on the eleventh", "False", row.Wants.ToString());
        Same("and it is done", "0", row.Phase.ToString());
    }

    /// <summary>
    /// A looping row costs exactly the same per cycle as the first one. This is
    /// the check the clock was pulled out of the behaviour for: handing the tick
    /// on rather than returning at the end of a phase is what stops a loop losing
    /// one every time round, and the drift that would cause is invisible until a
    /// machine has been running a while.
    /// </summary>
    static void Looping()
    {
        Row row = new Row();
        Clock.Start(row);
        int first = Down(row, 0.1f, 0.1f, true, 60);
        Count("the first press", 6, first);
        // Five ticks of wait and five of press, and not one more: a cycle that
        // cost eleven would be the lost-tick bug, and would put a looping row a
        // second behind a real timer every fifty cycles.
        Count("a cycle is the wait plus the press", 10, Gap(row, 0.1f, 0.1f, 60));
        Count("and the one after it is the same", 10, Gap(row, 0.1f, 0.1f, 60));
    }

    /// <summary>Ticks from the press the row is in now to the next one.</summary>
    static int Gap(Row row, float wait, float press, int budget)
    {
        bool down = row.Wants;
        for (int i = 1; i <= budget; i++)
        {
            Clock.Tick(row, wait, press, true);
            if (row.Wants && !down)
            {
                return i;
            }
            down = row.Wants;
        }
        return -1;
    }

    /// <summary>A row set to nought both ways still presses, and still lets
    /// go.</summary>
    static void Instant()
    {
        Row row = new Row();
        Clock.Start(row);
        Count("nought seconds presses on the second tick", 2,
              Down(row, 0f, 0f, false, 20));
        Run(row, 0f, 0f, false, 1);
        Same("and lets go the tick after", "False", row.Wants.ToString());
    }

    /// <summary>
    /// A second press stops a running row only where the row allows it, and a
    /// stopped row lets its key go -- an emulated key is reference counted, so one
    /// left down never comes up for anything else either.
    /// </summary>
    static void Stopping()
    {
        Row row = new Row();
        Clock.Start(row);
        Run(row, 0.1f, 0.1f, false, 8);
        Same("running and pressed", "True", row.Wants.ToString());

        Clock.Pressed(row, true, true, false, false, false);
        Same("a press does not stop a row that may not be stopped", "True",
             row.Wants.ToString());

        Clock.Pressed(row, true, true, false, true, false);
        Same("a press stops one that may", "False", row.Wants.ToString());
        Same("and it is idle", "0", row.Phase.ToString());
    }

    /// <summary>Hold to run: the activation is a switch, and letting go stops the
    /// row wherever it had got to.</summary>
    static void Holding()
    {
        Row row = new Row();
        Clock.Pressed(row, true, true, true, false, false);
        Same("a press while held starts it", "1", row.Phase.ToString());
        Run(row, 0.1f, 0.1f, false, 8);
        Same("it presses", "True", row.Wants.ToString());

        Clock.Pressed(row, false, false, true, false, false);
        Same("letting go stops it", "False", row.Wants.ToString());
        Same("and it is idle", "0", row.Phase.ToString());
    }

    /// <summary>Runs the clock for so many ticks.</summary>
    static void Run(Row row, float wait, float press, bool loop, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            Clock.Tick(row, wait, press, loop);
        }
    }

    /// <summary>Which tick the key first goes down on, counting from one, or -1
    /// within the budget given.</summary>
    static int Down(Row row, float wait, float press, bool loop, int budget)
    {
        for (int i = 1; i <= budget; i++)
        {
            Clock.Tick(row, wait, press, loop);
            if (row.Wants)
            {
                return i;
            }
        }
        return -1;
    }

    static void Count(string what, int want, int got)
    {
        Same(what, want.ToString(), got.ToString());
    }

    // ---- the stand-in ----------------------------------------------------

    static List<RowData> Rows(params float[] waits)
    {
        List<RowData> rows = new List<RowData>();
        for (int i = 0; i < waits.Length; i++)
        {
            RowData row = new RowData();
            row.Wait = waits[i];
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// The same insertion sort <see cref="Table.Sort"/> runs, over a list rather
    /// than over a block's controls. `Table.Ranks` is private and the block it
    /// would need is a Unity object, so the ordering is reached through
    /// <see cref="Table.Compare"/>, which exists for this.
    /// </summary>
    static void Sort(List<RowData> rows, int column, bool ascending)
    {
        for (int i = 1; i < rows.Count; i++)
        {
            RowData moving = rows[i];
            int at = i;
            while (at > 0 && Table.Compare(moving, rows[at - 1], column, ascending) < 0)
            {
                rows[at] = rows[at - 1];
                at--;
            }
            rows[at] = moving;
        }
    }

    static string Waits(List<RowData> rows)
    {
        string[] out_ = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            out_[i] = ((int)rows[i].Wait).ToString();
        }
        return string.Join(",", out_);
    }

    static void Same(string what, string want, string got)
    {
        if (want == got)
        {
            return;
        }
        Console.Error.WriteLine("  " + what + ": wanted " + want + ", got " + got);
        bad++;
    }
}
