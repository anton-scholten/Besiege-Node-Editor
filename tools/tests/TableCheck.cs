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
        Spelling();
        Variables();
        Ticks();
        Firing();
        Looping();
        Instant();
        Stopping();
        Holding();
        Converting();
        Combinational();
        ToggleMode();
        Latches();
        Counting();
        Edges();
        Arguments();
        Names();
        GateSorting();
        Boards();

        if (bad > 0)
        {
            Console.Error.WriteLine(bad + " check(s) failed.");
            return 1;
        }
        Console.WriteLine("Offline check: timer ticks, phases, sort order, ties, key\n"
            + "               names and every logic gate as the game has them.");
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
    /// keep the order they were already in, so sorting by Duration after sorting
    /// by Wait gives Wait within Duration rather than a reshuffle.
    /// </summary>
    static void Stability()
    {
        List<RowData> rows = Rows(1f, 2f, 3f, 4f);
        rows[0].Duration = 1f;
        rows[1].Duration = 2f;
        rows[2].Duration = 1f;
        rows[3].Duration = 2f;
        Sort(rows, Table.ColDuration, true);
        Same("a tie keeps the order it was in", "1,3,2,4", Waits(rows));
    }

    static void Descending()
    {
        List<RowData> rows = Rows(1f, 3f, 2f);
        Sort(rows, Table.ColWait, false);
        Same("waits sort descending", "3,2,1", Waits(rows));
    }

    // ---- the logic gates -------------------------------------------------

    /// <summary>One tick of a gate: the state machine, then the answer.</summary>
    static bool Gate(LogicRow row, int gate, bool mode, bool pressedA, bool pressedB,
                     bool heldA, bool heldB, bool releasedA)
    {
        Gates.Advance(row, gate, mode, pressedA, pressedB, heldA, heldB, releasedA);
        return Gates.Answer(row, gate);
    }

    /// <summary>A gate held at A and B, with no edges: the plain truth table.</summary>
    static bool Held(int gate, bool a, bool b)
    {
        return Gate(new LogicRow(), gate, false, false, false, a, b, false);
    }

    /// <summary>
    /// The seven gates that are only their inputs. Read straight off Besiege's
    /// own `EvaluateEmulation`, so this is the check that the reading was right.
    /// </summary>
    static void Combinational()
    {
        Same("NOT", "10", Bits(Gates.Not));
        Same("AND", "0001", Bits4(Gates.And));
        Same("OR", "0111", Bits4(Gates.Or));
        Same("NOR", "1000", Bits4(Gates.Nor));
        Same("NAND", "1110", Bits4(Gates.Nand));
        Same("XOR", "0110", Bits4(Gates.Xor));
        Same("XNOR", "1001", Bits4(Gates.Xnor));
    }

    /// <summary>A one-input gate over A false, true.</summary>
    static string Bits(int gate)
    {
        return (Held(gate, false, false) ? "1" : "0")
             + (Held(gate, true, false) ? "1" : "0");
    }

    /// <summary>A two-input gate over 00, 01, 10, 11.</summary>
    static string Bits4(int gate)
    {
        string all = "";
        for (int i = 0; i < 4; i++)
        {
            all += Held(gate, (i & 2) != 0, (i & 1) != 0) ? "1" : "0";
        }
        return all;
    }

    /// <summary>
    /// Toggle mode: a press flips an input and it stays flipped, which is what
    /// makes a gate usable from a key somebody taps rather than holds.
    /// </summary>
    static void ToggleMode()
    {
        LogicRow row = new LogicRow();
        Same("a tap turns AND's A on", false,
             Gate(row, Gates.And, true, true, false, false, false, false));
        Same("and then B makes it true", true,
             Gate(row, Gates.And, true, false, true, false, false, false));
        Same("tapping A again turns it off", false,
             Gate(row, Gates.And, true, true, false, false, false, false));
    }

    static void Latches()
    {
        LogicRow sr = new LogicRow();
        Same("SR latch sets on A", true,
             Gate(sr, Gates.SRLatch, false, true, false, false, false, false));
        Same("SR latch holds", true,
             Gate(sr, Gates.SRLatch, false, false, false, false, false, false));
        Same("SR latch clears on B", false,
             Gate(sr, Gates.SRLatch, false, false, true, false, false, false));

        LogicRow d = new LogicRow();
        Same("D latch takes A while B is held", true,
             Gate(d, Gates.DLatch, false, false, false, true, true, false));
        Same("D latch holds when B lets go", true,
             Gate(d, Gates.DLatch, false, false, false, false, false, false));
        Same("D latch follows A again on B", false,
             Gate(d, Gates.DLatch, false, false, false, false, true, false));
    }

    /// <summary>
    /// The counter answers on the wrap and nowhere else: four presses in, one
    /// press out. That is what makes a chain of them divide.
    /// </summary>
    static void Counting()
    {
        LogicRow row = new LogicRow();
        string got = "";
        for (int i = 0; i < 8; i++)
        {
            got += Gate(row, Gates.Counter, false, true, false, false, false, false)
                 ? "1" : "0";
        }
        Same("counter divides by four", "00010001", got);

        LogicRow reset = new LogicRow();
        Gate(reset, Gates.Counter, false, true, false, false, false, false);
        Gate(reset, Gates.Counter, false, true, false, false, false, false);
        Same("B resets the count", false,
             Gate(reset, Gates.Counter, false, false, true, false, false, false));
    }

    /// <summary>
    /// The edge detector answers for exactly one tick, and its switch means
    /// "inverted": the edge it answers to is the letting go rather than the
    /// press.
    /// </summary>
    static void Edges()
    {
        LogicRow row = new LogicRow();
        Same("an edge is one tick", true,
             Gate(row, Gates.EdgeDetect, false, true, false, true, false, false));
        Same("and no more", false,
             Gate(row, Gates.EdgeDetect, false, false, false, true, false, false));

        LogicRow back = new LogicRow();
        Same("inverted ignores the press", false,
             Gate(back, Gates.EdgeDetect, true, true, false, true, false, false));
        Same("and answers the release", true,
             Gate(back, Gates.EdgeDetect, true, false, false, false, false, true));
    }

    /// <summary>
    /// What the block hands the state machine, which is the other half of being
    /// the game's own gate: `LogicGate.UpdateBlock` counts a press as a hold, ORs
    /// the emulated hold in, and takes the release only where the key is not held.
    /// </summary>
    static void Arguments()
    {
        Same("a press counts as held", true, Gates.Holding(true, false, false));
        Same("so does an emulated hold", true, Gates.Holding(false, false, true));
        Same("nothing held is nothing", false, Gates.Holding(false, false, false));
        Same("a release counts", true, Gates.Letting(false, false, true));
        Same("but not while still held", false, Gates.Letting(false, true, true));
        Same("nor on the press itself", false, Gates.Letting(true, false, true));
    }

    /// <summary>
    /// A variable name as Besiege would take it: split on its own two separators,
    /// trimmed, and cut to `StatMaster.KeyMapper.VariableCharLimit`.
    /// </summary>
    static void Names()
    {
        Same("a name is left alone", "door", Bindings.Tidied(" door "));
        Same("a comma separates, as the game's own editor has it", "a;b",
             Bindings.Tidied("a, b"));
        Same("and a semicolon", "a;b", Bindings.Tidied("a;b"));
        Same("empties are dropped", "a", Bindings.Tidied("a;;"));
        Same("nothing is nothing", null, Bindings.Tidied("  "));
        Same("a name is cut to the game's limit", true,
             Bindings.Tidied(new string('x', 40)).Length == Bindings.NameLimit);
        Same("one name each", "a", Bindings.Tidied("a;a"));
    }

    static void GateSorting()
    {
        GateData a = new GateData();
        GateData b = new GateData();
        a.Gate = Gates.Xor;
        b.Gate = Gates.And;
        Same("gates sort by the game's own order", true,
             LogicTable.Compare(b, a, true) < 0);
        Same("and reverse", true, LogicTable.Compare(b, a, false) > 0);
    }

    /// <summary>
    /// A board's layout saves and loads: where the rows sit, and the two ends that
    /// are not rows. The wires themselves are not in it -- they are the rows' own
    /// bindings -- which is the point of the format.
    /// </summary>
    static void Boards()
    {
        Wiring board = new Wiring();
        board.Put(0, new UnityEngine.Vector2(40f, 60f));
        board.Put(2, new UnityEngine.Vector2(300f, 20f));
        Place input = new Place();
        input.Kind = Place.Input;
        input.X = 10f;
        input.Y = 10f;
        input.Variable = "door";
        board.Places.Add(input);
        Place output = new Place();
        output.Kind = Place.Output;
        output.X = 500f;
        output.Y = 30f;
        output.Key = KeyCode.M;
        board.Places.Add(output);

        Wiring back = Wiring.Load(board.Save());
        Same("a row keeps its place", "40,60",
             (int)back.Spot(0).x + "," + (int)back.Spot(0).y);
        Same("and a row after a gap", "300,20",
             (int)back.Spot(2).x + "," + (int)back.Spot(2).y);
        Same("both ends come back", "2", back.Places.Count.ToString());
        Same("a named end", "door", back.Places[0].Variable);
        Same("and a keyed one", "M", back.Places[1].Key.ToString());
        Same("an end knows what it stands for", true,
             back.Places[0].Same("door", KeyCode.None));

        // A name with a space in it is one name, not the first word of one: the
        // derivation makes a fresh end for any binding it cannot find, so a name
        // that came back short bred a duplicate on every load.
        Wiring spaced = new Wiring();
        spaced.Places.Add(End(Place.Input, "door open", KeyCode.None));
        Same("a name with a space survives a save",
             "door open", Wiring.Load(spaced.Save()).Places[0].Variable);

        // A comment is a line of the layout like anything else, and what it says
        // may run over several lines of its own.
        Wiring noted = new Wiring();
        Place note = new Place();
        note.Kind = Place.Note;
        note.X = 12f;
        note.Y = 34f;
        note.Words = "two lines\nand a \\ in one";
        noted.Places.Add(note);
        Place read = Wiring.Load(noted.Save()).Places[0];
        Same("a comment comes back whole", "two lines\nand a \\ in one", read.Words);
        Same("and knows what kind it is", Place.Note.ToString(),
             read.Kind.ToString());
        Same("an empty comment is still a comment", "",
             Wiring.Load(Wiring.Load("n 1 2 w -").Save()).Places[0].Words);

        // A row nobody has placed still has somewhere to be drawn.
        Same("an unplaced row is placed", true, back.Spot(7).y > 0f);
    }

    static Place End(int kind, string variable, KeyCode key)
    {
        Place place = new Place();
        place.Kind = kind;
        place.Variable = variable;
        place.Key = key;
        return place;
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

    /// <summary>The same for a yes or a no, which most of the gate checks are.</summary>
    static void Same(string what, bool want, bool got)
    {
        Same(what, want ? "yes" : "no", got ? "yes" : "no");
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
