using System;
using System.Collections.Generic;

namespace NodeEditorMod
{
    /// <summary>Every variable name on the machine, read off the blocks' keys:
    /// <c>KeyInputController</c>'s table is only filled during a run.</summary>
    public static class Variables
    {
        /// <summary>The names, sorted and without duplicates. Not cached: one typed
        /// elsewhere has to show at once.</summary>
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

        /// <summary>The names on one key, off `message`. A key back on the keyboard
        /// keeps its names, and they count.</summary>
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
