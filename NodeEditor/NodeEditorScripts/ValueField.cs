using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeEditorMod
{
    /// <summary>
    /// A transparent sheet over a number box. A drag that stays on the box selects
    /// text; leaving by a side changes the value, by the top or bottom reaches
    /// along the column. A click places the caret, a double-click selects all. From
    /// the sibling SpecialEffects mod. It handles pointer-down as well: without it
    /// the press resolves to the field and the click to the sheet, and no click is
    /// ever sent.
    /// </summary>
    public class ValueField : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public UnityEngine.UI.InputField field;

        /// <summary>The sheet itself: a transparent child filling the box, with one
        /// of these on it. Every number on a window is dragged this way, so the five
        /// steps live here rather than four times over.</summary>
        public static ValueField Over(GameObject host, UnityEngine.UI.InputField box)
        {
            GameObject sheet = new GameObject("Drag");
            sheet.transform.SetParent(host.transform, false);
            RectTransform over = sheet.AddComponent<RectTransform>();
            over.anchorMin = Vector2.zero;
            over.anchorMax = Vector2.one;
            over.offsetMin = Vector2.zero;
            over.offsetMax = Vector2.zero;
            // Takes the press; nothing is drawn.
            UnityEngine.UI.Image catcher = sheet.AddComponent<UnityEngine.UI.Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            ValueField drag = sheet.AddComponent<ValueField>();
            drag.field = box;
            return drag;
        }

        /// <summary>Pixels dragged sideways, once the drag has left by a
        /// side.</summary>
        public Action<float> dragged;

        /// <summary>The pointer during a drag that left by the top or bottom: a
        /// reach along the column, which the panel handles.</summary>
        public Action<Vector2> picking;

        /// <summary>That reach ended.</summary>
        public Action picked;

        // What this gesture turned out to be. Constants rather than an enum:
        // declaring an enum segfaults Besiege's own C# compiler.
        private const int Nothing = 0;
        private const int Scrubbing = 1;
        private const int Picking = 2;

        /// <summary>How far the pointer may move before a click is a drag: a pixel
        /// of wobble used to turn clicks into value nudges.</summary>
        private const float Slack = 4f;

        /// <summary>How far past the box's edge a drag stops selecting and starts
        /// changing the value.</summary>
        private const float Leeway = 4f;

        /// <summary>What the gesture is, decided once by the edge it left
        /// by.</summary>
        private int mode;

        /// <summary>Whether the ending gesture changed the value, so its click is
        /// not taken as a double-click.</summary>
        private bool scrubbed;

        public void OnPointerDown(PointerEventData press)
        {
            Pass(press, ExecuteEvents.pointerDownHandler);
        }

        public void OnPointerUp(PointerEventData press)
        {
            Pass(press, ExecuteEvents.pointerUpHandler);
        }

        public void OnBeginDrag(PointerEventData move)
        {
            mode = Nothing;
            scrubbed = false;
            // Left button only: the middle button pans (a `Pan` beside this), and
            // must not scrub the value.
            ignoring = move.button != PointerEventData.InputButton.Left;
            if (ignoring)
            {
                return;
            }
            Pass(move, ExecuteEvents.beginDragHandler);
        }

        /// <summary>Set for the length of a drag with any button but the left.
        /// </summary>
        private bool ignoring;

        public void OnDrag(PointerEventData move)
        {
            if (ignoring)
            {
                return;
            }
            if (mode == Nothing)
            {
                mode = Left(move);
            }
            if (mode == Nothing)
            {
                Pass(move, ExecuteEvents.dragHandler);
                return;
            }

            // Clear what the drag selected on its way out, every frame: the field
            // re-selects when its text changes under a drag.
            if (field != null)
            {
                field.caretPosition = field.text.Length;
            }

            if (mode == Scrubbing)
            {
                if (dragged != null)
                {
                    dragged(move.delta.x);
                }
                return;
            }
            if (picking != null)
            {
                picking(move.position);
            }
        }

        public void OnEndDrag(PointerEventData move)
        {
            if (ignoring)
            {
                ignoring = false;
                return;
            }
            if (mode == Nothing)
            {
                Pass(move, ExecuteEvents.endDragHandler);
            }
            else if (mode == Picking && picked != null)
            {
                picked();
            }
            scrubbed = mode != Nothing;
            mode = Nothing;
        }

        /// <summary>The edge the pointer left by, past <see cref="Leeway"/>: sides
        /// mean a value, top and bottom a reach. Sides are tested first, and a
        /// pointer that will not map counts as sideways.</summary>
        private int Left(PointerEventData move)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null)
            {
                return Scrubbing;
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, move.position, move.pressEventCamera, out local))
            {
                return Scrubbing;
            }
            Rect box = rect.rect;
            if (local.x < box.xMin - Leeway || local.x > box.xMax + Leeway)
            {
                return Scrubbing;
            }
            if (local.y < box.yMin - Leeway || local.y > box.yMax + Leeway)
            {
                return Picking;
            }
            return Nothing;
        }

        private void Pass<T>(PointerEventData move, ExecuteEvents.EventFunction<T> what)
            where T : IEventSystemHandler
        {
            if (field == null)
            {
                return;
            }
            ExecuteEvents.Execute(field.gameObject, move, what);
        }

        public void OnPointerClick(PointerEventData click)
        {
            // A middle click is the start of a pan, not a request to type.
            if (field == null || click.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            // Before the wander test: the second click of a double-click often
            // drifts.
            if (click.clickCount >= 2 && !scrubbed)
            {
                waiting = Patience;
                selecting = Insist;
                held = field.text;
                return;
            }

            // A drag ends with a click too; only a pointer that stayed put means
            // "type here".
            if (!Steady(click))
            {
                return;
            }

            held = field.text;

            // Not activated here: the field focuses and places its caret from the
            // passed-down press, and activating again sends the caret to the start.
            // Stepped in only if the field has not focused by itself.
            waiting = Patience;
        }

        private static bool Steady(PointerEventData pointer)
        {
            return (pointer.position - pointer.pressPosition).sqrMagnitude
                 < Slack * Slack;
        }

        /// <summary>Frames a double-click's select-all is asserted for: the field
        /// settles its caret in its own LateUpdate, in no fixed order with
        /// this.</summary>
        private const int Insist = 3;

        /// <summary>Frames a click is given to focus the field by itself before this
        /// does it.</summary>
        private const int Patience = 2;

        private int selecting;
        private int waiting;

        /// <summary>The text when a click asked for focus; once it differs the
        /// player is typing, and this stands off.</summary>
        private string held;

        private void LateUpdate()
        {
            if (field == null)
            {
                return;
            }

            // Typing cancels both: a select-all asserted over typing would eat
            // every letter.
            if ((selecting > 0 || waiting > 0) && held != null && field.text != held)
            {
                selecting = 0;
                waiting = 0;
                held = null;
            }

            if (waiting > 0)
            {
                waiting--;
                if (waiting == 0 && !field.isFocused)
                {
                    field.ActivateInputField();
                }
            }

            if (selecting <= 0 || !field.isFocused)
            {
                return;
            }

            // Anchor and focus only: `caretPosition` sets both ends and collapses
            // the selection.
            selecting--;
            field.selectionAnchorPosition = 0;
            field.selectionFocusPosition = field.text.Length;

            // Those setters do not redraw the selection highlight; this does.
            field.ForceLabelUpdate();
        }
    }
}
