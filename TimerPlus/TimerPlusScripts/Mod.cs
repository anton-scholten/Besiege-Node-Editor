using Modding;
using Modding.Modules;
using UnityEngine;

namespace TimerPlusMod
{
    /// <summary>
    /// Entry point. Two block modules, and the panel that draws their tables.
    /// </summary>
    public class Mod : ModEntryPoint
    {
        public override void OnLoad()
        {
            CustomModules.AddBlockModule<TimerPlusModule, TimerPlusBehaviour>("TimerPlus", false);
            CustomModules.AddBlockModule<LogicGatePlusModule, LogicGatePlusBehaviour>(
                "LogicGatePlus", false);

            // The panel lives on its own object rather than on a block: it watches
            // the mapper through static events, serves whichever Timer Plus block
            // is opened, and has to outlive the scene changes a block does not.
            // Without UI Factory it quietly never builds and Besiege's own mapper
            // is all there is.
            GameObject host = new GameObject("TimerPlusPanel");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Panel>();
        }
    }
}
