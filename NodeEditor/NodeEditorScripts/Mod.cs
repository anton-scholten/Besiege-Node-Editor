using Modding;
using Modding.Modules;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>Entry point: the two block modules, the panel and the node editor.
    /// </summary>
    public class Mod : ModEntryPoint
    {
        public override void OnLoad()
        {
            // Before anything is drawn: the words come from the language Besiege is
            // set to, and change with it.
            Words.Watching();

            CustomModules.AddBlockModule<TimerPlusModule, TimerPlusBehaviour>("TimerPlus", false);
            CustomModules.AddBlockModule<ComputerModule, ComputerBehaviour>(
                "Computer", false);

            // The panel watches the mapper from an object of its own that outlives
            // scene changes.
            GameObject host = new GameObject("NodeEditorPanel");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Panel>();

            // The node editor is a window on an object of its own that outlives
            // deselection.
            GameObject board = new GameObject("NodeEditor");
            Object.DontDestroyOnLoad(board);
            board.AddComponent<Editor>();
        }
    }
}
