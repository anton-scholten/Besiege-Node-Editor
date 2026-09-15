using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>Keeps a text box from taking a modifier-click meant for the node
    /// under it, for a few frames. Armed by the click, since shift also types
    /// capitals.
    /// </summary>
    public class Deaf : MonoBehaviour
    {
        public InputField box;

        private int frames;

        /// <summary>Whether a click now belongs to what this sits on.</summary>
        public static bool Aside()
        {
            return Input.GetKey(KeyCode.LeftControl)
                || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftShift)
                || Input.GetKey(KeyCode.RightShift);
        }

        private void Update()
        {
            if (Aside() && Input.GetMouseButtonDown(0))
            {
                frames = 3;
            }
            if (frames <= 0)
            {
                return;
            }
            frames--;
            if (box == null || !box.isFocused)
            {
                return;
            }
            box.DeactivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
