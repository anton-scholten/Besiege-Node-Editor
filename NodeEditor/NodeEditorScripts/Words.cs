using System;
using System.Collections.Generic;

namespace NodeEditorMod
{
    /// <summary>
    /// Every word the mod puts on screen, by key, so translating it is editing one
    /// file rather than hunting through the source.
    ///
    /// Besiege's own localisation cannot carry these. `LocalisationManager` looks a
    /// string up by a number (`GetTranslation(int)`) out of the game's own table --
    /// a language file replaces the game's strings by id, and there is no id a mod
    /// may claim without colliding with Besiege's or another mod's.
    /// `ExternalLocalisations` looks like the hook for it and is never read by
    /// anything in the game. So the mod keeps its own catalogue, which is what mods
    /// in this position usually do.
    ///
    /// What the game does give is the player's chosen language and a say in when it
    /// changes, and both are used: the file loaded is the language Besiege is set
    /// to.
    ///
    /// English lives in the table below, compiled in, so a missing or broken file
    /// costs nothing but English. A translation is `lang/French.txt` beside the mod
    /// -- named as Besiege names its own language files, which is Unity's
    /// `SystemLanguage` -- or the same name in the mod's data folder to override one
    /// without touching the install:
    ///
    /// <code>
    /// // Lines from // are ignored. \n in a value is a line break.
    /// bar.import = Importer
    /// tip.import = Prend toutes les portes logiques de la machine
    /// </code>
    ///
    /// A key nobody translated keeps its English, so a part-finished translation is
    /// still worth shipping.
    /// </summary>
    public static class Words
    {
        /// <summary>Where a translation is looked for, under the mod and under its
        /// data folder.</summary>
        private const string Folder = "lang/";

        /// <summary>The words as written here: English, and the fallback for every
        /// key a translation leaves out.</summary>
        private static readonly string[] English =
        {
            // ---- the node editor's title bar and rows ------------------------
            "bar.edit", "EDIT",
            "bar.tidy", "TIDY",
            "bar.fit", "ZOOM FIT",
            "bar.import", "IMPORT",
            "bar.pins", "PIN BLOCKS",
            "bar.export", "EXPORT",
            "bar.grid", "GRID",
            "bar.empty", "START EMPTY",
            "bar.reset-colours", "RESET COLORS",
            "bar.reset-size", "RESET SIZE",
            "bar.canvas-size", "CANVAS SIZE",
            "bar.node", "NODE",
            "bar.wire", "WIRE",
            "bar.by", "x",

            // ---- what a tip says ---------------------------------------------
            "tip.style", "Wire style",
            "tip.grid", "Align nodes to the grid",
            "tip.empty", "Start with empty board",
            "tip.edit", "Edit colors and settings",
            "tip.import", "Take all logic gates and timers of the machine into this editor",
            "tip.pins", "Add pin blocks when exporting",
            "tip.export", "Convert to logic gate blocks",
            "tip.prefix", "Prefix for variables created when connecting nodes",
            "tip.keep-open", "Keep the editor open",

            // ---- what a box says before anything is typed in it ---------------
            "ghost.name", "name",
            "ghost.prefix", "prefix",
            "ghost.comment", "comment",
            "ghost.zero", "0",
            "ghost.hash", "#",

            // ---- how nodes and wires are coloured -------------------------------
            // Shown only. What the file holds is `Hues.names`, which stays English.
            "mode.unicolor", "UNICOLOR",
            "mode.color", "COLOR",
            "mode.random", "RANDOM",

            // ---- the wire styles ----------------------------------------------
            "style.line", "LINE",
            "style.curve", "CURVE",
            "style.square", "SQUARE",

            // ---- what the board says when it refuses ---------------------------
            "board.key-and-name", "Can't mix key and variable.\nUse an OR gate.",
            "board.input-full-names", "that input is full\n100 names",
            "board.input-full-keys", "that input is full\n{0} keys",
            "board.answer-full-names", "that answer is full\n100 names",
            "board.answer-full-keys", "that answer is full\n{0} keys",
            "board.key-name-share", "a key and a name\ncannot share",
            "board.needs-input", "has to connect\nto an input",
            "board.needs-output", "has to connect\nto an output",
            "board.no-direct-output", "cannot directly\nconnect to output",
            "board.assigned", "already assigned!",
            "board.gates-limit", "reached {0}\ngates limit",
            "board.imported-one", "{0} block imported",
            "board.imported-many", "{0} blocks imported",
            "board.left-behind", "\n{0} left on the machine",
            "board.nothing-to-export", "nothing to export",

            // ---- the table under the mapper -------------------------------------
            "table.open", "NODE EDITOR",
            "table.add", "+",
            "table.import", "IMPORT",
            "table.pins", "PIN BLOCKS",
            "table.export", "EXPORT",
            "table.wait", "WAIT",
            "table.duration", "DURATION",
            "table.hold", "H",
            "table.stop", "S",
            "table.loop", "L",
            "table.emulate", "EMULATE",
            "table.input-a", "INPUT A",
            "table.input-b", "INPUT B",
            "table.gate", "GATE",
            "table.mode", "M",
            "table.output", "OUTPUT",

            "tip.wait", "Seconds from the start to the press",
            "tip.duration", "How long the press is held",
            "tip.hold", "Hold to run",
            "tip.stop", "Allow stop",
            "tip.loop", "Loop",
            "tip.emulate", "The key or variable this row presses",
            "tip.mode", "Edge detector: invert\nOther gates: toggle",

            // ---- what the table says back ---------------------------------------
            "table.gates-limit", "REACHED THE {0} GATES LIMIT",
            "table.timers-limit", "REACHED THE {0} TIMERS LIMIT",
            "table.no-timers", "NO TIMERS\nTO EXPORT",
            "table.export-failed", "COULD NOT EXPORT",
            "table.timer-added", "{0} TIMER ADDED",
            "table.timers-added", "{0} TIMERS ADDED",
            "table.imported", "{0} IMPORTED",
            "table.imported-differ", "{0} IN, CHECK ACTIVATE",

            // ---- the mapper's own controls ---------------------------------------
            "mapper.activate", "Activate",
            "mapper.automatic", "Automatic",
            "mapper.pins", "Pin blocks"
        };

        private static readonly Dictionary<string, string> said =
            new Dictionary<string, string>();

        private static bool loaded;

        /// <summary>The words for <paramref name="key"/>: the translation where
        /// there is one, English where there is not, and the key itself where
        /// somebody asks for one that does not exist -- visible, rather than an
        /// empty label nobody can trace.</summary>
        public static string Of(string key)
        {
            Load();
            string found;
            if (said.TryGetValue(key, out found))
            {
                return found;
            }
            return key;
        }

        /// <summary>The same, with a number or a name filled into it. The whole
        /// sentence is one key, so a translation can put the number where its own
        /// grammar wants it.</summary>
        public static string Of(string key, object filled)
        {
            return Of(key).Replace("{0}", filled == null ? "" : filled.ToString());
        }

        /// <summary>Reads the table again: the words are wanted in the language
        /// Besiege is set to now.</summary>
        public static void Reload()
        {
            loaded = false;
            Load();
        }

        /// <summary>Listens, once, for the player changing Besiege's language. The
        /// game raises this as it swaps translation files -- its own tooltips
        /// listen to the same thing -- so everything the mod has already drawn is
        /// made again in the new words, with nothing to restart.</summary>
        public static void Watching()
        {
            if (watching)
            {
                return;
            }
            watching = true;
            try
            {
                Localisation.LocalisationManager.LanguageChanged += Changed;
            }
            catch (Exception)
            {
                // Nothing to listen to: the words are still right for the language
                // the session started in, which is all this costs.
                watching = false;
            }
        }

        private static bool watching;

        /// <summary>The language changed under everything that is open.</summary>
        private static void Changed()
        {
            try
            {
                Reload();
                // The gates are named by the game, not by the table above, and
                // those names are kept once asked for.
                Gates.Forget();
                Editor.Retranslate();
                Panel.Retranslate();
                ComputerBehaviour.Retitle();
                TimerPlusBehaviour.Retitle();
            }
            catch (Exception e)
            {
                // Half-translated is better than a thrown exception inside the
                // game's own event, which would stop whatever listens after this.
                Log.Warn("could not take up the new language: " + e);
            }
        }

        private static void Load()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            said.Clear();
            for (int i = 0; i + 1 < English.Length; i += 2)
            {
                said[English[i]] = English[i + 1];
            }
            string code = Language();
            if (string.IsNullOrEmpty(code) || code == "English")
            {
                return;
            }
            // Beside the mod first, then the data folder, so a player can put a
            // translation of their own over the one that shipped.
            Read(Folder + code + ".txt", false);
            Read(Folder + code + ".txt", true);
        }

        private static void Read(string path, bool data)
        {
            string text;
            try
            {
                text = Modding.ModIO.ReadAllText(path, data);
            }
            catch (Exception)
            {
                return;                         // no such translation: English
            }
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//"))
                {
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();
                if (key.Length == 0 || value.Length == 0)
                {
                    continue;
                }
                said[key] = value.Replace("\\n", "\n");
            }
        }

        /// <summary>Which language Besiege is set to, named as Besiege names it --
        /// `French`, `ChineseSimplified` -- which is Unity's `SystemLanguage` and
        /// what its own language files are called, whatever `currLangISO` suggests.
        /// Empty where the game has not said, which is English.</summary>
        private static string Language()
        {
            try
            {
                if (!Localisation.LocalisationManager.hasInstance())
                {
                    return "";
                }
                return Localisation.LocalisationManager.Instance.currLangISO;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
