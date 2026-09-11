using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// One thing on the board that is not a row: an input coming in, or an output
    /// going out.
    ///
    /// A gate is a row of the table and carries its own settings; these two are
    /// bindings with a place on the board and nothing else. What they are wired to
    /// is not stored either -- a wire *is* a row's input carrying this node's
    /// binding, which is what makes the board and the table the same thing seen two
    /// ways.
    /// </summary>
    public class Place
    {
        public const int Input = 0;
        public const int Output = 1;

        /// <summary>A comment: a note on the board, wired to nothing. It stands for
        /// no binding at all, which is why everything that walks the graph passes
        /// over it.</summary>
        public const int Note = 2;

        public int Kind;
        public float X;
        public float Y;

        /// <summary>What it stands for: a variable name, or a keycode when
        /// <see cref="Variable"/> is null.</summary>
        public string Variable;
        public KeyCode Key = KeyCode.None;

        /// <summary>What a comment says. Empty on everything else.</summary>
        public string Words;

        /// <summary>A comment's font size, where its corner has been pulled; 0 for
        /// the size every comment starts at.</summary>
        public int Size;

        public bool Bound
        {
            get { return Variable != null || Key != KeyCode.None; }
        }

        /// <summary>What a row's key has to carry for a wire to exist.
        ///
        /// Names are compared trimmed: a name typed with a space on the end binds
        /// the same key as one without, so two ends carrying it are two drawings of
        /// one end and should fold into each other.</summary>
        public bool Same(string variable, KeyCode key)
        {
            if (Variable != null)
            {
                return variable != null && Tidied(variable) == Tidied(Variable);
            }
            return variable == null && key == Key && key != KeyCode.None;
        }

        private static string Tidied(string name)
        {
            return name == null ? null : name.Trim();
        }

        public string Say()
        {
            if (Variable != null)
            {
                return Variable;
            }
            return Key == KeyCode.None ? "-" : Bindings.Spell(Key);
        }
    }

    /// <summary>
    /// Where everything sits on the board, and nothing else.
    ///
    /// The graph itself is the table: a gate node is a row, and a wire is a row's
    /// input carrying the name another row's answer goes out under. So this holds
    /// only what the table has nowhere to put -- the places -- and is saved as text
    /// in one mapper control beside the rows.
    ///
    /// One line per thing, fields separated by spaces, which is enough for a board
    /// of a few dozen nodes and can be read in a save. A format somebody can fix by
    /// hand is worth more here than a compact one.
    /// </summary>
    public class Wiring
    {
        /// <summary>Where row <c>i</c> sits, for as many rows as have been placed.
        /// </summary>
        public readonly List<Vector2> Spots = new List<Vector2>();

        public readonly List<Place> Places = new List<Place>();

        public Vector2 Spot(int row)
        {
            while (Spots.Count <= row)
            {
                // A row nobody has placed lands in a column, out of the way of the
                // ones that have been.
                Spots.Add(new Vector2(240f, 20f + Spots.Count * 74f));
            }
            return Spots[row];
        }

        public void Put(int row, Vector2 at)
        {
            Spot(row);
            Spots[row] = at;
        }

        /// <summary>Takes a row's place out and shuffles the rest up, so the places
        /// keep following the rows they belong to.</summary>
        public void Forget(int row)
        {
            if (row >= 0 && row < Spots.Count)
            {
                Spots.RemoveAt(row);
            }
        }

        public string Save()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < Spots.Count; i++)
            {
                text.Append("g ").Append(i).Append(' ')
                    .Append(Whole(Spots[i].x)).Append(' ')
                    .Append(Whole(Spots[i].y)).Append('\n');
            }
            for (int i = 0; i < Places.Count; i++)
            {
                Place place = Places[i];
                if (place.Kind == Place.Note)
                {
                    // A comment is a line like the rest, with what it says at the
                    // end of it -- newlines and all, written as `\n` so one comment
                    // stays one line of the layout.
                    // The field before the words is `w`, with the font size run on
                    // where the comment has been resized -- `w22`. A version that
                    // knows nothing of sizes skips that field whatever it says.
                    text.Append("n ").Append(Whole(place.X)).Append(' ')
                        .Append(Whole(place.Y)).Append(" w")
                        .Append(place.Size > 0
                                ? place.Size.ToString(CultureInfo.InvariantCulture)
                                : "")
                        .Append(' ').Append(Folded(place.Words)).Append('\n');
                    continue;
                }
                text.Append(place.Kind == Place.Input ? "i " : "o ")
                    .Append(Whole(place.X)).Append(' ')
                    .Append(Whole(place.Y)).Append(' ')
                    .Append(place.Variable != null ? "v" : "k").Append(' ')
                    .Append(place.Variable != null
                            ? place.Variable : place.Key.ToString()).Append('\n');
            }
            return text.ToString();
        }

        private static string Whole(float value)
        {
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads one back. A line this does not understand is skipped rather than
        /// thrown over: half a board is worth more than none, and an unknown line
        /// is one a later version of the mod wrote.
        /// </summary>
        public static Wiring Load(string text)
        {
            Wiring made = new Wiring();
            if (string.IsNullOrEmpty(text))
            {
                return made;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Trim().Split(' ');
                if (parts.Length < 4)
                {
                    continue;
                }
                if (parts[0] == "g")
                {
                    int row = Number(parts[1], -1);
                    if (row < 0)
                    {
                        continue;
                    }
                    while (made.Spots.Count <= row)
                    {
                        made.Spots.Add(Vector2.zero);
                    }
                    made.Spots[row] = new Vector2(Number(parts[2], 0),
                                                  Number(parts[3], 0));
                }
                else if (parts[0] == "n" && parts.Length >= 5)
                {
                    Place note = new Place();
                    note.Kind = Place.Note;
                    note.X = Number(parts[1], 0);
                    note.Y = Number(parts[2], 0);
                    if (parts[3].Length > 1 && parts[3][0] == 'w')
                    {
                        note.Size = Mathf.Max(0, Number(parts[3].Substring(1), 0));
                    }
                    note.Words = Unfolded(Rest(parts, 4));
                    made.Places.Add(note);
                }
                else if ((parts[0] == "i" || parts[0] == "o") && parts.Length >= 5)
                {
                    Place place = new Place();
                    place.Kind = parts[0] == "i" ? Place.Input : Place.Output;
                    place.X = Number(parts[1], 0);
                    place.Y = Number(parts[2], 0);
                    // What it stands for is the rest of the line, spaces and all.
                    // Reading only the next word turned `door open` into `door` on
                    // every load, and the derivation then made a second end for the
                    // name the rows still carried -- a duplicate that came back
                    // however many times it was folded away.
                    string said = Rest(parts, 4);
                    if (said != "-")
                    {
                        if (parts[3] == "v")
                        {
                            place.Variable = said;
                        }
                        else
                        {
                            place.Key = Bindings.Parse(said);
                        }
                    }
                    made.Places.Add(place);
                }
            }
            return made;
        }

        /// <summary>A comment as one line: its own backslashes doubled, its
        /// newlines written as `\n`, and an empty one written as a single dash so
        /// the line still has a field there to read.</summary>
        private static string Folded(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return "-";
            }
            return words.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "");
        }

        private static string Unfolded(string line)
        {
            if (string.IsNullOrEmpty(line) || line == "-")
            {
                return "";
            }
            System.Text.StringBuilder said = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\\' && i + 1 < line.Length)
                {
                    i++;
                    said.Append(line[i] == 'n' ? '\n' : line[i]);
                    continue;
                }
                said.Append(line[i]);
            }
            return said.ToString();
        }

        /// <summary>Everything from one field to the end of the line, joined back
        /// up as it was written.</summary>
        private static string Rest(string[] parts, int from)
        {
            System.Text.StringBuilder said = new System.Text.StringBuilder();
            for (int i = from; i < parts.Length; i++)
            {
                if (i > from)
                {
                    said.Append(' ');
                }
                said.Append(parts[i]);
            }
            return said.ToString();
        }

        private static int Number(string text, int ifBad)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out value)
                ? value : ifBad;
        }
    }
}
