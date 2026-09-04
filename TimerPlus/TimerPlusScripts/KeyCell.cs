using System;
using UnityEngine;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// One cell of the table that holds a key or a variable.
    ///
    /// Besiege's own mapper gives a key a whole row and a modal selector; a table
    /// has a hundred units of width for the same thing, so this is the compact
    /// form: a mode button and, beside it, either a plate showing the key or a box
    /// holding the variable name.
    ///
    /// * **key mode** -- the plate shows what is bound. Clicking it listens for the
    ///   next press and binds that; Escape unbinds; clicking away gives up.
    /// * **variable mode** -- the box holds one or more names, joined with `;` the
    ///   way a save spells them.
    ///
    /// The two are built once and swapped with `SetActive`, each owning its own
    /// flag, so the row's own clipping can hide the whole cell without the two
    /// arguing about which of them should be showing -- see notes/04, "one owner
    /// per SetActive".
    /// </summary>
    public class KeyCell : MonoBehaviour
    {
        /// <summary>How wide the mode button is. Wide enough for the mapper's own
        /// bubble icon to be read at a glance, which is what it holds.</summary>
        public const float ModeWidth = 22f;

        private const float Gap = 2f;

        private GameObject mode;
        private Text modeLabel;
        private RawImage modeIcon;
        private GameObject plate;
        private Text plateLabel;
        private InputField box;

        /// <summary>
        /// Which of the two the cell is showing.
        ///
        /// A field the mode button and <see cref="Show"/> own, **not** something
        /// derived from whether <see cref="Variable"/> is null. It was derived, and
        /// that was the bug: `InputField.onEndEdit` fires when a field loses focus
        /// or is deactivated, not only when somebody presses Enter -- so switching
        /// away from variable mode deactivated the box, the box announced its text
        /// on the way out, and the announcement put the mode straight back. The
        /// cell could be switched to a variable and never switched back, and
        /// clicking another row's button knocked this one over instead. Mode is a
        /// choice; the text only says which variable.
        /// </summary>
        private bool variable;

        /// <summary>True while the mode button is doing the switching, so the box
        /// being deactivated underneath it cannot answer for the cell.</summary>
        private bool swapping;

        private bool listening;
        private bool held;

        /// <summary>
        /// The one cell listening for a key, if any.
        ///
        /// Static because two cells listening at once bind the same press to both,
        /// and because the first would go on holding Besiege's keyboard off after
        /// the pointer had moved on.
        /// </summary>
        private static KeyCell waiting;

        /// <summary>What this cell shows when nothing is bound.</summary>
        private readonly string blank = Bindings.Unset;

        /// <summary>Raised when the binding changes. The panel writes it through to
        /// the key and queues the commit.</summary>
        public Action<KeyCell> Changed;

        /// <summary>The name typed into the box, or null when there is none. Only
        /// meaningful in variable mode -- read <see cref="UsesVariable"/> first.
        /// </summary>
        public string Variable;

        /// <summary>Whether the cell is showing a variable rather than a key.
        /// What the panel writes through to the block goes on this.</summary>
        public bool UsesVariable { get { return variable; } }

        /// <summary>The keycode the cell is showing. None when nothing is bound or
        /// when it is showing a variable.</summary>
        public KeyCode Code = KeyCode.None;

        /// <summary>Which row this cell belongs to, for the panel's own
        /// bookkeeping. Not used here.</summary>
        public int Row;

        public static KeyCell Make(Transform host, float x, float y, float w, float h)
        {
            GameObject go = new GameObject("KeyCell");
            go.transform.SetParent(host, false);
            UIF.Fit(go.AddComponent<RectTransform>(), x, y, w, h);

            KeyCell self = go.AddComponent<KeyCell>();
            self.Build(w, h);
            return self;
        }

        private void Build(float w, float h)
        {
            float rest = w - ModeWidth - Gap;

            mode = UIF.Spawn(UIF.ButtonPrefab, transform);
            if (mode != null)
            {
                UIF.Fit(mode.GetComponent<RectTransform>(), 0f, 0f, ModeWidth, h);
                UIF.NoSwell(mode);
                modeLabel = Caption(mode, "K");
                Button click = mode.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(SwapMode);
                }
            }

            plate = UIF.Spawn(UIF.ButtonPrefab, transform);
            if (plate != null)
            {
                UIF.Fit(plate.GetComponent<RectTransform>(), ModeWidth + Gap, 0f, rest, h);
                UIF.NoSwell(plate);
                plateLabel = Caption(plate, blank);
                Button click = plate.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Listen);
                }
            }

            GameObject field = UIF.Spawn(UIF.InputPrefab, transform);
            if (field != null)
            {
                UIF.Fit(field.GetComponent<RectTransform>(), ModeWidth + Gap, 0f, rest, h);
                box = field.GetComponent<InputField>();
                if (box != null)
                {
                    UIF.Style(box.textComponent, UIF.Ink, TextAnchor.MiddleLeft);
                    UIF.Style(box.placeholder as Text, UIF.Ink, TextAnchor.MiddleLeft);
                    Text ghost = box.placeholder as Text;
                    if (ghost != null)
                    {
                        ghost.text = "name";
                    }
                    box.onEndEdit.AddListener(Typed);
                }
                field.SetActive(false);
            }
        }

        /// <summary>
        /// The picture on the mode button: Besiege's own key-selector bubble, laid
        /// over the UI Factory button rather than replacing its graphic.
        ///
        /// Built on the first repaint that finds the artwork rather than while the
        /// cell is being made: the icons are read off a live `KeySelector`, and the
        /// mapper has to have built one before there is anything to read. A cell
        /// made a moment too early would otherwise keep its lettering until the
        /// whole panel was rebuilt.
        ///
        /// A `RawImage` and not an `Image`, because what the mapper hands over is a
        /// `Texture` off a mesh-UI material -- a `Sprite` would need it to be a
        /// `Texture2D`, which is an assumption there is no reason to make.
        /// </summary>
        private void Bubble()
        {
            if (modeIcon != null || mode == null || !MapperArt.Ready)
            {
                return;
            }
            GameObject go = new GameObject("Bubble");
            go.transform.SetParent(mode.transform, false);
            RawImage image = go.AddComponent<RawImage>();
            RectTransform rect = go.GetComponent<RectTransform>();
            // Inset, so the bubble sits inside the button's rounded plate rather
            // than running to its edge.
            rect.anchorMin = new Vector2(0.12f, 0.12f);
            rect.anchorMax = new Vector2(0.88f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // The plate behind it takes the click; a graphic that answered the ray
            // itself would be a second, smaller target inside the first.
            image.raycastTarget = false;
            modeIcon = image;
        }

        private static Text Caption(GameObject control, string text)
        {
            Text label = control.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                return null;
            }
            // The prefab's caption is a fixed width and is the last child, so on a
            // control narrower than that it overhangs its neighbour and wins the
            // clicks meant for it. Pinned to its own control, and deaf: the plate
            // behind it is what the click is for.
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label.raycastTarget = false;
            UIF.Style(label, UIF.Ink, TextAnchor.MiddleCenter);
            UIF.Shrink(label, 8);
            label.text = text;

            Swell swell = control.AddComponent<Swell>();
            swell.grows = label.transform;
            swell.grown = 1.12f;
            return label;
        }

        // ---- what it is showing ----------------------------------------------

        /// <summary>Shows what a key is bound to. Called on every open and whenever
        /// the panel refills the table.</summary>
        public void Show(MKey key)
        {
            // Mid-binding, or mid-typing: do not write over what somebody is doing.
            if (listening || (box != null && box.isFocused))
            {
                return;
            }
            variable = Bindings.IsVariable(key);
            Variable = variable ? Bindings.Variable(key) : null;
            Code = variable ? KeyCode.None : Bindings.Code(key);
            Paint();
        }

        public bool Listening { get { return listening; } }

        private void Paint()
        {
            // The bubble says which way the cell will go if it is clicked, which
            // is the way round Besiege's own selector uses them: three dots while
            // the key is on the keyboard, a cross while a variable holds it.
            Bubble();
            if (modeIcon != null)
            {
                modeIcon.texture = variable ? MapperArt.VariableIcon : MapperArt.KeyIcon;
            }
            if (modeLabel != null)
            {
                // Only the fallback for a mapper whose icons could not be read.
                modeLabel.text = modeIcon != null ? "" : (variable ? "V" : "K");
                modeLabel.color = variable ? UIF.Live : UIF.Ink;
            }
            if (plate != null)
            {
                plate.SetActive(!variable);
            }
            if (box != null)
            {
                box.gameObject.SetActive(variable);
                // Never while it has focus, or the caret jumps out from under
                // whoever is typing.
                if (!box.isFocused)
                {
                    box.text = Variable == null ? "" : Variable;
                }
            }
            if (plateLabel != null)
            {
                bool bound = Code != KeyCode.None;
                plateLabel.text = listening ? "press key"
                    : (bound ? Bindings.Spell(Code) : blank);
                plateLabel.color = listening ? UIF.Hot : UIF.Ink;
            }
        }

        // ---- editing ---------------------------------------------------------

        private void SwapMode()
        {
            Give();
            Hold(false);
            listening = false;

            // Guarded, because Paint below deactivates whichever half is going
            // away -- and deactivating a focused InputField makes it announce its
            // text through onEndEdit, which would answer for the cell and undo the
            // switch that is happening.
            swapping = true;
            variable = !variable;
            Variable = null;
            Code = KeyCode.None;
            Paint();
            swapping = false;

            if (variable && box != null)
            {
                box.ActivateInputField();
            }
            Raise();
        }

        private void Listen()
        {
            if (variable)
            {
                return;
            }
            if (!listening && waiting != null && waiting != this)
            {
                waiting.Give();
            }
            listening = !listening;
            waiting = listening ? this : null;
            Hold(listening);
            Paint();
        }

        /// <summary>Stops listening without changing what is bound.</summary>
        private void Give()
        {
            if (!listening)
            {
                return;
            }
            listening = false;
            if (waiting == this)
            {
                waiting = null;
            }
            Hold(false);
            Paint();
        }

        private void Typed(string text)
        {
            // Not only Enter: this also arrives when the box loses focus and when
            // it is switched off, and neither of those is the cell being edited.
            if (swapping || !variable)
            {
                return;
            }
            string wanted = text == null ? "" : text.Trim();
            // An empty box stays variable mode with nothing bound. Falling back to
            // key mode here is what made the button impossible to click back.
            Variable = wanted.Length == 0 ? null : wanted;
            Paint();
            Raise();
        }

        private void Update()
        {
            if (!listening)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                listening = false;
                waiting = null;
                Hold(false);
                Code = KeyCode.None;
                Paint();
                Raise();
                return;
            }
            KeyCode caught = Bindings.Captured();
            if (caught == KeyCode.None)
            {
                return;
            }
            listening = false;
            waiting = null;
            Hold(false);
            Code = caught;
            Paint();
            Raise();
        }

        private void Raise()
        {
            if (Changed != null)
            {
                Changed(this);
            }
        }

        /// <summary>
        /// Holds Besiege's own keyboard off while this cell is listening, or the
        /// key being bound also drives the camera and fires whatever else is bound
        /// to it. `SetInMenu` is counted on Besiege's side, so it is raised and
        /// dropped exactly once -- including when the panel is torn down with a
        /// cell still listening, which is the usual way to leave the game believing
        /// a menu is open.
        /// </summary>
        private void Hold(bool on)
        {
            if (held == on)
            {
                return;
            }
            try
            {
                StatMaster.SetInMenu(on);
                held = on;
            }
            catch (Exception)
            {
                held = false;
            }
        }

        private void OnDisable()
        {
            listening = false;
            if (waiting == this)
            {
                waiting = null;
            }
            Hold(false);
        }

        private void OnDestroy()
        {
            if (waiting == this)
            {
                waiting = null;
            }
            Hold(false);
        }
    }
}
