using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// Keeps a text box deaf to a click that was meant for whatever it sits on.
    ///
    /// The node editor picks nodes out with control or shift held, and a field
    /// under the pointer takes that click for itself: the caret lands in it and
    /// the next keystroke is typed rather than doing whatever it was meant to do.
    /// uGUI gives the field the focus in the frame the click lands and again as the
    /// event system settles, so this holds it off for a few frames rather than for
    /// one.
    ///
    /// Armed by the click and not by the modifier being held: shift is also how a
    /// capital letter is typed, and a field somebody is already typing in should go
    /// on taking it.
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
