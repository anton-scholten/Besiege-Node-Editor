using System.Collections.Generic;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// An <see cref="MKey"/> read and written as "a key or a variable". A Besiege
    /// key answers the keyboard or a `;`-joined list of names (`useMessage`), never
    /// both.
    /// </summary>
    public static class Bindings
    {
        /// <summary>What a cell shows for a key nobody has bound.</summary>
        public const string Unset = "-";

        /// <summary>The keys a cell can bind, in capture order. Built from
        /// contiguous `KeyCode` runs: `Enum.GetValues` is reflection, which the
        /// loader refuses.</summary>
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

        /// <summary>Every keycode the key answers to, each once and in the order it
        /// holds them -- none for a key bound to names.</summary>
        public static List<KeyCode> Codes(MKey key)
        {
            List<KeyCode> codes = new List<KeyCode>();
            if (key == null || IsVariable(key))
            {
                return codes;
            }
            for (int i = 0; i < key.KeysCount; i++)
            {
                KeyCode code = key.GetKey(i);
                if (code != KeyCode.None && !codes.Contains(code))
                {
                    codes.Add(code);
                }
            }
            return codes;
        }

        /// <summary>What a key reads as: its names, every keycode it holds, or
        /// <paramref name="ifEmpty"/>. Every keycode, so a second key changing is
        /// seen.</summary>
        public static string Show(MKey key, string ifEmpty)
        {
            if (IsVariable(key))
            {
                return Variable(key);
            }
            List<KeyCode> codes = Codes(key);
            if (codes.Count == 0)
            {
                return ifEmpty;
            }
            string[] spelt = new string[codes.Count];
            for (int i = 0; i < codes.Count; i++)
            {
                spelt[i] = Spell(codes[i]);
            }
            return string.Join(";", spelt);
        }

        /// <summary>A keycode from Unity's name for it, or None. Walks
        /// <see cref="Bindable"/>, since `Enum.Parse` is reflection.</summary>
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

        /// <summary>A keycode as a cell shows it: `Alpha4` is `4`, `LeftShift` is
        /// `LShift`.</summary>
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

        /// <summary>Binds the key to exactly this keycode, clearing any variable.
        /// `RemoveRedundant` alone leaves a key rebound twice answering to
        /// both.</summary>
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

        /// <summary>Binds the key to these keycodes, up to <see cref="MostKeys"/>;
        /// unbound if there are none.</summary>
        public static void Bind(MKey key, KeyCode[] codes)
        {
            if (key == null)
            {
                return;
            }
            Clear(key);
            for (int i = 0; codes != null && i < codes.Length; i++)
            {
                Added(key, codes[i]);
            }
        }

        /// <summary>
        /// Binds the key to one or more names, keeping a keycode behind them:
        /// `Machine.InitSimBlock` registers a key once per keycode, so names with
        /// no keycode hear nothing. With `useMessage` set the keycode is inert.
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
        /// Besiege's name rules: 32 characters (`KeyMapper.VariableCharLimit`),
        /// split on `;` and `,` when typed (`Selectors.TagSelector`), stored joined
        /// with `;` (`MKey.CombineVariables`).
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
        /// Caps on one key, which holds keycodes or names and never both. Five
        /// keycodes: Besiege's mapper stops adding at three
        /// (`KeySelector.MaxKeys`), but saves, registration and emulation take any
        /// number. A hundred names, the game's own cap
        /// (`KeyMapper.MaxDisplayedTags`).
        /// </summary>
        public const int MostKeys = 5;
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

        /// <summary>Whether a `;`-joined list of names holds this one.</summary>
        public static bool Carries(string names, string want)
        {
            if (names == null || want == null)
            {
                return false;
            }
            string[] all = names.Split(';');
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Trim() == want)
                {
                    return true;
                }
            }
            return false;
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

        /// <summary>Adds a name, keeping the rest. False when the key is on the
        /// keyboard or already holds <see cref="MostNames"/>.</summary>
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

        /// <summary>The key pressed this frame, or None. Escape is not bindable;
        /// the caller handles it, so listening can always be left.</summary>
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
