using System;
using System.Collections.Generic;

namespace NodeEditorMod
{
    /// <summary>
    /// Every variable name the machine already uses.
    ///
    /// A variable is not declared anywhere -- it is a name written into an
    /// <see cref="MKey"/>, and a key answers to it if it is spelled the same. So
    /// "the variables available" is exactly "the names somebody has already typed
    /// somewhere on this machine", and the way to find them is to read the keys.
    ///
    /// <c>KeyInputController</c> keeps a table of them, but only of keys
    /// registered for a run: it is filled by <c>Machine.InitSimBlock</c> at the
    /// start of a simulation and is empty in the build area, which is the one
    /// place this list is wanted. So the blocks are read directly.
    /// </summary>
    public static class Variables
    {
        /// <summary>
        /// The names, in alphabetical order, without duplicates.
        ///
        /// Not cached: a name appears the moment somebody types it into another
        /// block, and a list built once would be stale for the rest of the
        /// session. It is a walk of a few hundred controls, done when a menu opens.
        /// </summary>
        public static List<string> Known()
        {
            List<string> found = new List<string>();
            try
            {
                Machine machine = Machine.Active();
                if (machine == null || machine.BuildingBlocks == null)
                {
                    return found;
                }
                for (int b = 0; b < machine.BuildingBlocks.Count; b++)
                {
                    BlockBehaviour block = machine.BuildingBlocks[b];
                    if (block == null || block.MapperTypes == null)
                    {
                        continue;
                    }
                    for (int m = 0; m < block.MapperTypes.Count; m++)
                    {
                        Gather(block.MapperTypes[m] as MKey, found);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("could not read the machine's variables: " + e.Message);
            }
            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        /// <summary>
        /// The names on one key. Read off `message` -- a `string[]` -- rather than
        /// through `CombineVariables`, because this wants them one at a time and
        /// that joins them with the separator a save uses.
        ///
        /// A key that is *not* on a variable is read too: its name is still there
        /// -- Besiege leaves it behind when a key goes back to the keyboard -- and
        /// a name typed and then switched away from is a name the player has in
        /// mind for this machine.
        /// </summary>
        private static void Gather(MKey key, List<string> into)
        {
            if (key == null || key.message == null)
            {
                return;
            }
            for (int i = 0; i < key.message.Length; i++)
            {
                string name = key.message[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                name = name.Trim();
                if (name.Length == 0 || into.Contains(name))
                {
                    continue;
                }
                into.Add(name);
            }
        }
    }
}
