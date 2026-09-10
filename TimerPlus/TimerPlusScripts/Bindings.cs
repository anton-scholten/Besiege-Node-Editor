using System.Collections.Generic;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// Reading and writing an <see cref="MKey"/> as "a key or a variable", which
    /// is what a cell of the table shows and what the converter writes into a real
    /// timer block.
    ///
    /// A Besiege key can answer the keyboard or a *message* -- one or more
    /// variable names, joined with a semicolon -- and `useMessage` is the flag
    /// that says which. There is no third state and no way to do both.
    /// </summary>
    public static class Bindings
    {
        /// <summary>What a cell shows for a key nobody has bound.</summary>
        public const string Unset = "-";

        /// <summary>
        /// The keys a cell will let somebody bind, in the order the capture scans
        /// them.
        ///
        /// Built rather than listed, because `KeyCode`'s values are contiguous
        /// through each of these runs. Enumerating the enum itself would be
        /// tidier and is not available: `Enum.GetValues` is reflection, and one
        /// reference to `System.Reflection` has the mod loader refuse the whole
        /// assembly.
        /// </summary>
        private static readonly KeyCode[] Bindable = Build();

        private static KeyCode[] Build()
        {
            List<KeyCode> all = new List<KeyCode>();
            Run(all, KeyCode.A, KeyCode.Z);
            Run(all, KeyCode.Alpha0, KeyCode.Alpha9);
            Run(all, KeyCode.F1, KeyCode.F15);
            Run(all, KeyCode.Keypad0, KeyCode.Keypad9);
            Run(all, KeyCode.UpArrow, KeyCode.LeftArrow);
            all.AddRange(new KeyCode[]
            {
                KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Tab,
                KeyCode.Backspace, KeyCode.Delete, KeyCode.Insert,
                KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
                KeyCode.LeftShift, KeyCode.RightShift,
                KeyCode.LeftControl, KeyCode.RightControl,
                KeyCode.LeftAlt, KeyCode.RightAlt,
                KeyCode.Minus, KeyCode.Equals, KeyCode.Comma, KeyCode.Period,
                KeyCode.Slash, KeyCode.Backslash, KeyCode.Semicolon, KeyCode.Quote,
                KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.BackQuote,
                KeyCode.KeypadPeriod, KeyCode.KeypadDivide, KeyCode.KeypadMultiply,
                KeyCode.KeypadMinus, KeyCode.KeypadPlus,
                KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2,
                KeyCode.Mouse3, KeyCode.Mouse4,
            });
            return all.ToArray();
        }

        private static void Run(List<KeyCode> into, KeyCode from, KeyCode to)
        {
            for (int c = (int)from; c <= (int)to; c++)
            {
                into.Add((KeyCode)c);
            }
        }

        /// <summary>Whether the key is bound to a variable rather than the
        /// keyboard.</summary>
        public static bool IsVariable(MKey key)
        {
            return key != null && key.useMessage
                && !string.IsNullOrEmpty(Variable(key));
        }

        /// <summary>The variable names this key answers to, joined as a save spells
        /// them, or null.</summary>
        public static string Variable(MKey key)
        {
            if (key == null || key.message == null)
            {
                return null;
            }
            string joined = MKey.CombineVariables(key.message);
            return string.IsNullOrEmpty(joined) ? null : joined;
        }

        /// <summary>The first keycode the key holds, or None.</summary>
        public static KeyCode Code(MKey key)
        {
            if (key == null)
            {
                return KeyCode.None;
            }
            for (int i = 0; i < key.KeysCount; i++)
            {
                if (key.GetKey(i) != KeyCode.None)
                {
                    return key.GetKey(i);
                }
            }
            return KeyCode.None;
        }

        /// <summary>
        /// What a cell shows: the variable names, or the keycode as Unity spells
        /// it, or <paramref name="ifEmpty"/>.
        /// </summary>
        public static string Show(MKey key, string ifEmpty)
        {
            if (IsVariable(key))
            {
                return Variable(key);
            }
            KeyCode code = Code(key);
            return code == KeyCode.None ? ifEmpty : Spell(code);
        }

        /// <summary>
        /// A keycode back from the name Unity gives it, or None.
        ///
        /// `Enum.Parse` is reflection and one mention of `System.Reflection` has
        /// the loader refuse the whole assembly, so the list this mod already
        /// builds for capture is walked instead. It holds every key a cell will
        /// bind, which is every key this can be asked about.
        /// </summary>
        public static KeyCode Parse(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return KeyCode.None;
            }
            for (int i = 0; i < Bindable.Length; i++)
            {
                if (Bindable[i].ToString() == name)
                {
                    return Bindable[i];
                }
            }
            return KeyCode.None;
        }

        /// <summary>
        /// A keycode as a cell shows it: Unity's own name, shortened where its
        /// spelling is longer than the column. `Alpha4` is `4` and `LeftShift` is
        /// `LShift`, which is what a player calls them.
        /// </summary>
        public static string Spell(KeyCode code)
        {
            string name = code.ToString();
            if (name.StartsWith("Alpha"))
            {
                return name.Substring(5);
            }
            if (name.StartsWith("Keypad"))
            {
                return "#" + name.Substring(6);
            }
            if (name.StartsWith("Left"))
            {
                return "L" + name.Substring(4);
            }
            if (name.StartsWith("Right"))
            {
                return "R" + name.Substring(5);
            }
            if (name.StartsWith("Mouse"))
            {
                return "M" + name.Substring(5);
            }
            if (name.EndsWith("Arrow"))
            {
                return name.Substring(0, name.Length - 5);
            }
            return name;
        }

        /// <summary>
        /// Binds the key to the keyboard, clearing any variable.
        ///
        /// `RemoveRedundant` is not enough on its own: the key has to end up
        /// holding exactly this one code, or a key rebound twice answers to both.
        /// </summary>
        public static void Bind(MKey key, KeyCode code)
        {
            if (key == null)
            {
                return;
            }
            Only(key, code);
            key.useMessage = false;
            key.message = new string[0];
        }

        /// <summary>
        /// Binds the key to one or more variable names.
        ///
        /// The keycode left behind is not decoration. `Machine.InitSimBlock` files
        /// a block's keys with `KeyInputController` from inside
        /// `for (i = 0; i &lt; key.KeysCount; i++)`, and it is `AddMKey` that puts
        /// a key into the table variable names are looked up in -- so a key with a
        /// name and no keycodes is never registered, hears nothing, and looks
        /// exactly like a block that does not support automation. `AddMKey` files a
        /// key under its name *or* its keys and never both, so with `useMessage`
        /// set the keycode kept here stays inert. It is there to be counted.
        /// </summary>
        public static void BindVariable(MKey key, string names)
        {
            if (key == null)
            {
                return;
            }
            List<string> kept = Named(names);
            if (kept.Count == 0)
            {
                Clear(key);
                return;
            }
            if (Code(key) == KeyCode.None)
            {
                Only(key, KeyCode.C);
            }
            key.message = kept.ToArray();
            key.useMessage = true;
        }

        /// <summary>
        /// How long a variable name may be, and the two characters that separate
        /// one from the next as somebody types them.
        ///
        /// Besiege's own: `StatMaster.KeyMapper.VariableCharLimit` is 32, and its
        /// tag editor splits what is typed on `Selectors.TagSelector`'s separators,
        /// a semicolon and a comma. A name longer than the limit cannot be edited
        /// in the game's own mapper afterwards, and a comma left in one would be
        /// two names there and one here -- so both rules are kept to.
        ///
        /// Stored names are joined with a semicolon alone; that is what
        /// `MKey.CombineVariables` writes and `MKey.SplitVariable` reads.
        /// </summary>
        public const int NameLimit = 32;

        private static readonly char[] Apart = { ';', ',' };

        /// <summary>What somebody typed, as the names Besiege would make of it:
        /// split, trimmed, cut to the limit, and the empties dropped.</summary>
        public static List<string> Named(string typed)
        {
            List<string> kept = new List<string>();
            if (typed == null)
            {
                return kept;
            }
            string[] split = typed.Split(Apart);
            for (int i = 0; i < split.Length; i++)
            {
                string one = split[i] == null ? "" : split[i].Trim();
                if (one.Length > NameLimit)
                {
                    one = one.Substring(0, NameLimit);
                }
                if (one.Length > 0 && !kept.Contains(one))
                {
                    kept.Add(one);
                }
            }
            return kept;
        }

        /// <summary>The same, as the one string a cell shows and a binding holds.
        /// Null where nothing is left of it.</summary>
        public static string Tidied(string typed)
        {
            List<string> kept = Named(typed);
            if (kept.Count == 0)
            {
                return null;
            }
            return string.Join(";", kept.ToArray());
        }

        /// <summary>
        /// How many keycodes one key may answer to, and how many names.
        ///
        /// Besiege's own: `Selectors.KeySelector.MaxKeys` is three, which is what
        /// its mapper lets a hand bind, and `StatMaster.KeyMapper.MaxDisplayedTags`
        /// is a hundred, past which its tag editor stops splitting what is typed.
        /// A key answers to keycodes or to names and never both, so these are two
        /// caps on the same key rather than a total.
        /// </summary>
        public const int MostKeys = 3;
        public const int MostNames = 100;

        /// <summary>How many things a key answers to. Nought when it is bound to
        /// nothing at all.</summary>
        public static int Count(MKey key)
        {
            if (key == null)
            {
                return 0;
            }
            if (IsVariable(key))
            {
                return Named(Variable(key)).Count;
            }
            int codes = 0;
            for (int i = 0; i < key.KeysCount; i++)
            {
                if (key.GetKey(i) != KeyCode.None)
                {
                    codes++;
                }
            }
            return codes;
        }

        /// <summary>Whether a key already answers to this name.</summary>
        public static bool Holds(MKey key, string name)
        {
            return IsVariable(key) && name != null
                && Named(Variable(key)).Contains(name);
        }

        /// <summary>Whether a key already answers to this keycode.</summary>
        public static bool Holds(MKey key, KeyCode code)
        {
            return key != null && !IsVariable(key) && code != KeyCode.None
                && key.HasKey(code);
        }

        /// <summary>
        /// Adds a name to what a key answers to, keeping whatever it answers to
        /// already.
        ///
        /// False where it cannot: the key is on the keyboard rather than on names
        /// -- a Besiege key is one or the other and never both -- or it is holding
        /// as many names as the game allows.
        /// </summary>
        public static bool Added(MKey key, string name)
        {
            if (key == null || string.IsNullOrEmpty(name))
            {
                return false;
            }
            List<string> kept = IsVariable(key) ? Named(Variable(key))
                                                : new List<string>();
            if (!IsVariable(key) && Code(key) != KeyCode.None)
            {
                return false;               // it is on the keyboard
            }
            if (kept.Contains(name))
            {
                return true;                // already there, and once is enough
            }
            if (kept.Count >= MostNames)
            {
                return false;
            }
            kept.Add(name);
            BindVariable(key, string.Join(";", kept.ToArray()));
            return true;
        }

        /// <summary>The same for a keycode: added beside whatever codes are there,
        /// up to the three Besiege's own mapper allows.</summary>
        public static bool Added(MKey key, KeyCode code)
        {
            if (key == null || code == KeyCode.None)
            {
                return false;
            }
            if (IsVariable(key))
            {
                return false;               // it is on names
            }
            if (key.HasKey(code))
            {
                return true;
            }
            int codes = Count(key);
            if (codes >= MostKeys)
            {
                return false;
            }
            if (codes == 0)
            {
                Only(key, code);
                key.useMessage = false;
                return true;
            }
            key.AddKey(code);
            key.useMessage = false;
            return true;
        }

        /// <summary>Takes one name off a key, leaving the rest. The key is unbound
        /// where it was the last one.</summary>
        public static void Dropped(MKey key, string name)
        {
            if (!IsVariable(key) || string.IsNullOrEmpty(name))
            {
                return;
            }
            List<string> kept = Named(Variable(key));
            if (!kept.Remove(name))
            {
                return;
            }
            if (kept.Count == 0)
            {
                Clear(key);
                return;
            }
            BindVariable(key, string.Join(";", kept.ToArray()));
        }

        /// <summary>And one keycode.</summary>
        public static void Dropped(MKey key, KeyCode code)
        {
            if (key == null || IsVariable(key) || code == KeyCode.None)
            {
                return;
            }
            for (int i = key.KeysCount - 1; i >= 0; i--)
            {
                if (key.GetKey(i) == code)
                {
                    key.RemoveKey(i);
                }
            }
            if (Count(key) == 0)
            {
                Clear(key);
            }
        }

        /// <summary>Unbinds the key entirely.</summary>
        public static void Clear(MKey key)
        {
            if (key == null)
            {
                return;
            }
            Only(key, KeyCode.None);
            key.useMessage = false;
            key.message = new string[0];
        }

        /// <summary>Leaves the key holding exactly one code.</summary>
        private static void Only(MKey key, KeyCode code)
        {
            while (key.KeysCount > 1)
            {
                key.RemoveKey(key.KeysCount - 1);
            }
            if (key.KeysCount == 0)
            {
                key.AddKey(code);
            }
            else
            {
                key.AddOrReplaceKey(0, code);
            }
        }

        /// <summary>
        /// The key being pressed now, or None. Used by a cell that is listening for
        /// one to bind.
        ///
        /// Escape is not in <see cref="Bindable"/> and is answered separately by
        /// the caller, so there is always a way out of listening.
        /// </summary>
        public static KeyCode Captured()
        {
            if (!Input.anyKeyDown)
            {
                return KeyCode.None;
            }
            for (int i = 0; i < Bindable.Length; i++)
            {
                if (Input.GetKeyDown(Bindable[i]))
                {
                    return Bindable[i];
                }
            }
            return KeyCode.None;
        }
    }
}
