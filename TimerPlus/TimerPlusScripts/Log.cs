using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// One prefix in one place, so this mod's lines can be found in Player.log and
    /// in the in-game console with `show_logs true`. Grep the log for the tag
    /// rather than for the mod's name: the loader's own messages name the file and
    /// the element and never the mod.
    /// </summary>
    public static class Log
    {
        private const string Prefix = "[TimerPlus] ";

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }
    }
}
