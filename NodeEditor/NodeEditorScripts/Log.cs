using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>One prefix for this mod's log lines. Grep for the tag: the loader's
    /// messages name files, never the mod.</summary>
    public static class Log
    {
        private const string Prefix = "[NodeEditor] ";

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
