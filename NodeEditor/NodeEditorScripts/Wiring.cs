using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>A board node that is not a row: an input, an output or a comment.
    /// Its wires are not stored; a wire is a row's input carrying this
    /// binding.</summary>
    public class Place
    {
        public const int Input = 0;
        public const int Output = 1;

        /// <summary>A comment, wired to nothing; walks of the graph skip
        /// it.</summary>
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

        /// <summary>Whether this node stands for that binding. Names compare
        /// trimmed: a trailing space binds the same key.</summary>
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
    /// Where everything sits on the board, saved as text in one mapper control. The
    /// graph is the table, so only places live here: a line per node, readable and
    /// fixable by hand in a save.
    /// </summary>
    public class Wiring
    {
        /// <summary>Where row <c>i</c> sits, for as many rows as have been placed.
        /// </summary>
        public readonly List<Vector2> Spots = new List<Vector2>();

        public readonly List<Place> Places = new List<Place>();

        /// <summary>Row <c>i</c>'s colour seed, kept beside its place so it moves
        /// with the row: a removal above it does not change its colour.</summary>
        public readonly List<int> Seeds = new List<int>();

        public Vector2 Spot(int row)
        {
            while (Spots.Count <= row)
            {
                // A row nobody has placed lands in a column, out of the way of the
                // ones that have been.
                Spots.Add(new Vector2(240f, 20f + Spots.Count * 74f));
            }
            Seeded();
            return Spots[row];
        }

        public int Seed(int row)
        {
            Spot(row);
            return Seeds[row];
        }

        /// <summary>A seed for every place: what a row's number gave it before seeds
        /// were kept.</summary>
        private void Seeded()
        {
            while (Seeds.Count < Spots.Count)
            {
                Seeds.Add(Hues.Mix(1, Seeds.Count));
            }
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
            Seeded();
            if (row >= 0 && row < Spots.Count)
            {
                Spots.RemoveAt(row);
                Seeds.RemoveAt(row);
            }
        }

        public string Save()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            Seeded();
            for (int i = 0; i < Spots.Count; i++)
            {
                // The seed last: a layout from before seeds has four fields.
                text.Append("g ").Append(i).Append(' ')
                    .Append(Whole(Spots[i].x)).Append(' ')
                    .Append(Whole(Spots[i].y)).Append(' ')
                    .Append(Seeds[i].ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }
            for (int i = 0; i < Places.Count; i++)
            {
                Place place = Places[i];
                if (place.Kind == Place.Note)
                {
                    // A comment's words end its line, newlines written `\n`. The
                    // field before them is `w`, with the font size run on once
                    // resized (`w22`).
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

        /// <summary>Reads a layout back, skipping lines it does not understand (a
        /// later version's) rather than throwing.</summary>
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
                    made.Seeded();
                    made.Spots[row] = new Vector2(Number(parts[2], 0),
                                                  Number(parts[3], 0));
                    if (parts.Length >= 5)
                    {
                        made.Seeds[row] = Number(parts[4], made.Seeds[row]);
                    }
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
                    // The binding is the rest of the line: reading one word turned
                    // `door open` into `door`, and grew a duplicate end on every
                    // load.
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

        /// <summary>A comment as one line: backslashes doubled, newlines as `\n`,
        /// empty as `-`.</summary>
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
