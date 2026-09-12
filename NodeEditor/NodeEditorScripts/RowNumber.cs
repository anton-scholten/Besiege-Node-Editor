using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// The number down the left of a row, which becomes that row's delete button
    /// while the pointer is on it.
    ///
    /// The table had a column of crosses down the right for this, which cost a
    /// column of width on every row to hold something wanted about once a session.
    /// The number is already there, already the width of a cross, and already the
    /// thing somebody points at to say "this one" -- so it does both jobs, and the
    /// table is narrow enough to sit under the mapper at the mapper's own width.
    ///
    /// Red under the pointer rather than an X drawn beside the number: a control
    /// that deletes something should not look like a label until it is pressed.
    /// </summary>
    public class RowNumber : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                             IPointerClickHandler
    {
        /// <summary>Raised on a click, which is a request to delete this row.</summary>
        public Action Clicked;

        /// <summary>What answers the pointer: the whole cell, invisible.</summary>
        private Image target;

        /// <summary>What is drawn: a rounded square, square whatever shape the cell
        /// is, so the mark is a badge on the row rather than a stripe down it.</summary>
        private RawImage plate;
        private Text label;
        private string number = "";
        private bool hot;

        /// <summary>How much smaller than the cell the plate is drawn, so the
        /// badges down a table do not touch each other.</summary>
        private const float Inset = 4f;

        /// <summary>Which timer this is. Written on every fill.</summary>
        public string Number
        {
            set
            {
                number = value == null ? "" : value;
                Paint();
            }
        }

        public static RowNumber Make(Transform host, float x, float y, float w, float h)
        {
            GameObject go = new GameObject("RowNumber");
            go.transform.SetParent(host, false);
            UIF.Fit(go.AddComponent<RectTransform>(), x, y, w, h);

            RowNumber self = go.AddComponent<RowNumber>();
            // Invisible, and only there to be pointed at: a Graphic is the one
            // thing uGUI reports a pointer to.
            self.target = go.AddComponent<Image>();
            self.target.color = new Color(0f, 0f, 0f, 0f);

            GameObject badge = new GameObject("Plate");
            badge.transform.SetParent(go.transform, false);
            RectTransform square = badge.AddComponent<RectTransform>();
            square.anchorMin = new Vector2(0.5f, 0.5f);
            square.anchorMax = new Vector2(0.5f, 0.5f);
            square.pivot = new Vector2(0.5f, 0.5f);
            square.anchoredPosition = Vector2.zero;
            float side = Mathf.Min(w, h) - Inset;
            square.sizeDelta = new Vector2(side, side);
            self.plate = badge.AddComponent<RawImage>();
            self.plate.texture = Glyphs.Rounded;
            self.plate.raycastTarget = false;

            GameObject text = new GameObject("Text");
            text.transform.SetParent(go.transform, false);
            RectTransform rect = text.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            self.label = text.AddComponent<Text>();
            UIF.Style(self.label, UIF.Ink, TextAnchor.MiddleCenter);
            // The pointer belongs to the plate; a label that answered it too would
            // report an exit every time the pointer crossed a letter.
            self.label.raycastTarget = false;
            self.Paint();
            return self;
        }

        public void OnPointerEnter(PointerEventData pointer) { Lit(true); }

        public void OnPointerExit(PointerEventData pointer) { Lit(false); }

        /// <summary>Turned on by the pointer being anywhere on the row, not only on
        /// the number itself -- see <see cref="Watch"/>.</summary>
        public void Lit(bool on)
        {
            hot = on;
            Paint();
        }

        /// <summary>
        /// Lights this cell while the pointer is anywhere on the row.
        ///
        /// Pointing at a row and being shown, on that row, the one thing that can
        /// be done to it as a whole. The number alone is a twenty-two unit target
        /// somebody has to find; the row is the width of the panel.
        ///
        /// A transparent plate, because uGUI only reports a pointer to a Graphic
        /// and the row frame is a bare RectTransform. It does not take the pointer
        /// off the cells: children are raycast before their parent.
        /// </summary>
        public void Watch(GameObject row)
        {
            if (row == null)
            {
                return;
            }
            Image sheet = row.GetComponent<Image>();
            if (sheet == null)
            {
                sheet = row.AddComponent<Image>();
            }
            sheet.color = new Color(0f, 0f, 0f, 0f);
            Watcher watcher = row.GetComponent<Watcher>();
            if (watcher == null)
            {
                watcher = row.AddComponent<Watcher>();
            }
            watcher.cell = this;
        }

        /// <summary>The row's own half of that: it has nothing else to do, so it is
        /// a handful of lines rather than a file.</summary>
        public class Watcher : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public RowNumber cell;

            public void OnPointerEnter(PointerEventData pointer)
            {
                if (cell != null) cell.Lit(true);
            }

            public void OnPointerExit(PointerEventData pointer)
            {
                if (cell != null) cell.Lit(false);
            }
        }

        // The row is switched off under the pointer as it scrolls out of the frame,
        // and no exit arrives for it -- so it would come back red.
        private void OnDisable()
        {
            hot = false;
            Paint();
        }

        public void OnPointerClick(PointerEventData pointer)
        {
            if (Clicked != null)
            {
                Clicked();
            }
        }

        private void Paint()
        {
            if (plate == null || label == null)
            {
                return;
            }
            plate.enabled = hot;
            plate.color = UIF.Hot;
            label.text = hot ? "X" : number;
            label.color = UIF.Ink;
        }
    }
}
