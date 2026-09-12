using Modding;
using Modding.Modules;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Entry point. Two block modules, the panel that draws their tables, and the
    /// node editor the logic table opens.
    /// </summary>
    public class Mod : ModEntryPoint
    {
        public override void OnLoad()
        {
            CustomModules.AddBlockModule<TimerPlusModule, TimerPlusBehaviour>("TimerPlus", false);
            CustomModules.AddBlockModule<NodeEditorModule, NodeEditorBehaviour>(
                "NodeEditor", false);

            // The panel lives on its own object rather than on a block: it watches
            // the mapper through static events, serves whichever Timer Plus block
            // is opened, and has to outlive the scene changes a block does not.
            // Without UI Factory it quietly never builds and Besiege's own mapper
            // is all there is.
            GameObject host = new GameObject("NodeEditorPanel");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Panel>();

            // The node editor is a window of its own rather than a panel docked
            // under the mapper, so it lives on its own object and outlives the
            // block being deselected. It draws the logic table's own rows as a
            // circuit; the table's NODE EDITOR button opens it.
            GameObject board = new GameObject("NodeEditor");
            Object.DontDestroyOnLoad(board);
            board.AddComponent<Editor>();
        }
    }
}
