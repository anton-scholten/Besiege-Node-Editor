using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>Finding a named object inside a spawned prefab.</summary>
    public static class Attach
    {
        /// <summary>The named descendant, inactive ones included, or
        /// null.</summary>
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
