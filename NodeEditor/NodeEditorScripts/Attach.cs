using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>Finding a named object inside a spawned prefab.</summary>
    public static class Attach
    {
        /// <summary>
        /// The named descendant, or null. Inactive ones count: UI Factory's Window
        /// prefab arrives with parts switched off, and the panel has to reach them
        /// to size the viewport and hide the title bar.
        /// </summary>
        public static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }
    }
}
