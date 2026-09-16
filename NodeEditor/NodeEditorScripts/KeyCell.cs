using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// A table cell holding a key or a variable: a mode button, then either a plate
    /// showing the key (click to listen, Escape unbinds) or a box of `;`-joined
    /// names. Both halves are built once and swapped with `SetActive`.
    /// </summary>
    public class KeyCell : MonoBehaviour
    {
        /// <summary>How wide the mode button is. Wide enough for the mapper's own
        /// bubble icon to be read at a glance, which is what it holds.</summary>
        public const float ModeWidth = 22f;

        /// <summary>The mode bubble's width; the logic table asks for a narrower
        /// one.
        /// </summary>
        private float bubble = ModeWidth;

        private const float Gap = 2f;

        private GameObject mode;
        private Text modeLabel;
        private RawImage modeIcon;
        private GameObject plate;
        private Text plateLabel;
        private InputField box;

        /// <summary>Which half the cell shows: a choice owned by the mode button
        /// and <see cref="Show"/>, not derived from <see cref="Variable"/>.
        /// `onEndEdit` also fires on deactivation, and a derived mode switched
        /// itself straight back.</summary>
        private bool variable;

        /// <summary>Whether a modifier-click belongs to what the cell sits on: the
        /// node editor picks nodes with it.</summary>
        public bool ignoreCtrl
        {
            get { return aside; }
            set
            {
                aside = value;
                if (guard != null)
                {
                    guard.enabled = value;
                }
            }
        }

        private bool aside;
        private Deaf guard;

        /// <summary>True while the mode button is doing the switching, so the box
        /// being deactivated underneath it cannot answer for the cell.</summary>
        private bool swapping;

        private bool listening;
        private bool held;

        /// <summary>Whether the next click is already used: binding a mouse button
        /// takes its press, and its release would click the plate and listen
        /// again.</summary>
        private bool swallow;

        /// <summary>Frames with every button up while swallowing. One clear frame
        /// before the flag drops, as Update and the event system run in no fixed
        /// order.</summary>
        private int settled;

        /// <summary>The one cell listening, if any: two would bind one press to
        /// both.
        /// </summary>
        private static KeyCell waiting;

        /// <summary>What this cell shows when nothing is bound.</summary>
        private readonly string blank = Bindings.Unset;

        /// <summary>Raised when the binding changes. The panel writes it through to
        /// the key and queues the commit.</summary>
        public Action<KeyCell> Changed;

        /// <summary>Handed the name box when it is built; the node editor drags its
        /// node by it.</summary>
        public Action<InputField> Fielded;

        /// <summary>The typed name, or null. Only meaningful when
        /// <see cref="UsesVariable"/>.</summary>
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
            return Make(host, x, y, w, h, ModeWidth);
        }

        public static KeyCell Make(Transform host, float x, float y, float w, float h,
                                   float bubble)
        {
            GameObject go = new GameObject("KeyCell");
            go.transform.SetParent(host, false);
            UIF.Fit(go.AddComponent<RectTransform>(), x, y, w, h);

            KeyCell self = go.AddComponent<KeyCell>();
            self.bubble = bubble;
            self.Build(w, h);
            self.guard = go.AddComponent<Deaf>();
            self.guard.box = self.box;
            self.guard.enabled = false;
            return self;
        }


        /// <summary>Where the plate and the box begin.</summary>
        private float plateAt;

        private void Build(float w, float h)
        {
            float rest = w - bubble - Gap;
            plateAt = bubble + Gap;

            mode = UIF.Spawn(UIF.ButtonPrefab, transform);
            if (mode != null)
            {
                UIF.Fit(mode.GetComponent<RectTransform>(), 0f, 0f, bubble, h);
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
                UIF.Fit(plate.GetComponent<RectTransform>(), plateAt, 0f, rest, h);
                UIF.NoSwell(plate);
                plateLabel = Caption(plate, blank);
                Button click = plate.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Listen);
                }
            }

            wide = rest;
            high = h;
        }

        private float wide;
        private float high;

        /// <summary>The name box, built on first use: most cells are keys, and a
        /// text field is the dearest prefab.</summary>
        private void Boxed()
        {
            if (box != null)
            {
                return;
            }
            GameObject field = UIF.Spawn(UIF.InputPrefab, transform);
            if (field == null)
            {
                return;
            }
            UIF.Fit(field.GetComponent<RectTransform>(), plateAt, 0f, wide, high);
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
                // A name longer than the cell slides along to show all of it.
                Marquee.On(box);
            }
            // Clicking the box offers the machine's names. On the field itself:
            // uGUI hands a click to every handler on an object, so typing still
            // works.
            Choices.Opener opener = field.AddComponent<Choices.Opener>();
            opener.Clicked = Offer;
            if (guard != null)
            {
                guard.box = box;
            }
            if (box != null && Fielded != null)
            {
                Fielded(box);
            }
        }

        /// <summary>The mode button's picture: Besiege's key-selector bubble. Built
        /// on the first repaint that finds it, since it only exists once the mapper
        /// has built a `KeySelector`. A `RawImage`, as the artwork is a
        /// `Texture`.</summary>
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
            Text label = UIF.Label(control, text, UIF.Ink, TextAnchor.MiddleCenter,
                                   8, false);
            if (label != null)
            {
                UIF.Grow(control, label.transform);
            }
            return label;
        }

        // ---- what it is showing ----------------------------------------------

        /// <summary>Shows what a key is bound to. Called on every open and whenever
        /// the panel refills the table.</summary>
        public void Show(MKey key)
        {
            Show(key, true);
        }

        /// <param name="counted">Whether several bindings show as a count. False
        /// for an input the row does not read: the cell shows what it holds, under
        /// the bars.</param>
        public void Show(MKey key, bool counted)
        {
            // Mid-binding, or mid-typing: do not write over what somebody is doing.
            if (listening || (box != null && box.isFocused))
            {
                return;
            }
            // A key wired to several things shows a count and takes no typing:
            // lists are edited on the board.
            several = counted ? Bindings.Count(key) : 0;
            variable = Bindings.IsVariable(key);
            Variable = variable ? Bindings.Variable(key) : null;
            Code = variable ? KeyCode.None : Bindings.Code(key);
            Paint();
        }

        /// <summary>Shows a binding that is not an `MKey`: the node editor's input
        /// and output nodes.</summary>
        public void Load(string named, KeyCode code)
        {
            if (listening || (box != null && box.isFocused))
            {
                return;
            }
            several = 0;                    // an end of the board stands for one
            variable = named != null;
            Variable = named;
            Code = named != null ? KeyCode.None : code;
            Paint();
        }

        /// <summary>Shows a Timer Plus row's binding, which is the table's own data
        /// rather than an `MKey`. Several show as a count, as a key's do.</summary>
        public void Load(string named, KeyCode[] codes, bool counted)
        {
            if (listening || (box != null && box.isFocused))
            {
                return;
            }
            int keys = 0;
            KeyCode first = KeyCode.None;
            for (int i = 0; codes != null && i < codes.Length; i++)
            {
                if (codes[i] != KeyCode.None)
                {
                    if (keys == 0)
                    {
                        first = codes[i];
                    }
                    keys++;
                }
            }
            variable = named != null;
            several = counted ? (variable ? Bindings.Named(named).Count : keys) : 0;
            Variable = named;
            Code = variable ? KeyCode.None : first;
            Paint();
        }

        /// <summary>Finishes whatever the cell is doing -- listening, a name being
        /// typed -- before the table points it at another row.</summary>
        public void Let()
        {
            Give();
            if (box != null && box.isFocused)
            {
                box.DeactivateInputField();
            }
        }

        /// <summary>How many things the key answers to, when more than one; Besiege
        /// ORs them.</summary>
        private int several;

        /// <summary>Whether the cell is what a row presses rather than reads: its
        /// count says "outputs".</summary>
        public bool Answers;

        private void Paint()
        {
            if (several > 1)
            {
                if (mode != null)
                {
                    mode.SetActive(false);
                }
                if (box != null)
                {
                    box.gameObject.SetActive(false);
                }
                if (plate != null)
                {
                    plate.SetActive(true);
                }
                if (plateLabel != null)
                {
                    plateLabel.text = several + (Answers ? " outputs" : " inputs");
                    plateLabel.color = UIF.Live;
                }
                return;
            }
            if (mode != null && !mode.activeSelf)
            {
                mode.SetActive(true);
            }
            // The bubble shows where a click goes, as Besiege's selector does: dots
            // on the keyboard, a cross on a variable.
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
            if (variable)
            {
                Boxed();
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

        /// <summary>Whether a modifier-click belongs to what the cell sits on, so
        /// it does not also start listening.</summary>
        private bool Aside()
        {
            return ignoreCtrl && Deaf.Aside();
        }

        private void SwapMode()
        {
            if (Aside() || several > 1)
            {
                return;
            }
            Give();
            Hold(false);
            listening = false;

            // Guarded: deactivating a focused box fires onEndEdit, which would undo
            // the swap.
            swapping = true;
            variable = !variable;
            Variable = null;
            Code = KeyCode.None;
            Paint();
            swapping = false;

            if (variable && box != null)
            {
                box.ActivateInputField();
                Offer();
            }
            Raise();
        }

        private void Listen()
        {
            if (!live || variable || Aside() || several > 1)
            {
                return;
            }
            if (swallow)
            {
                swallow = false;
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

        /// <summary>Gives back whatever cell is listening, wherever it is. Called
        /// before a window closes: a cell still listening holds a menu count, and a
        /// window that goes without `OnDisable` reaching the cell -- its canvas
        /// switched off rather than the object -- would stand that count up for
        /// good, which stops the game's own wheel zooming.</summary>
        public static void Dropped()
        {
            if (waiting != null)
            {
                waiting.Give();
            }
        }

        /// <summary>Whether this cell can be bound at all. False for an input its
        /// gate does not read -- the one the table bars with diagonal lines -- which
        /// is drawn over rather than hidden, and used to take a click through the
        /// bars.</summary>
        public bool Live
        {
            get { return live; }
            set
            {
                if (live == value)
                {
                    return;
                }
                live = value;
                if (!live)
                {
                    // A gate can change under the pointer, so a cell may be barred
                    // while it is listening or being typed in.
                    Let();
                }
                Deadened();
            }
        }

        private bool live = true;

        /// <summary>The three things a cell is clicked on -- the bubble, the plate
        /// and the name box -- switched off together.</summary>
        private void Deadened()
        {
            if (plate != null)
            {
                Button click = plate.GetComponent<Button>();
                if (click != null)
                {
                    click.interactable = live;
                }
            }
            if (mode != null)
            {
                Button swap = mode.GetComponent<Button>();
                if (swap != null)
                {
                    swap.interactable = live;
                }
            }
            if (box != null)
            {
                box.interactable = live;
            }
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

        /// <summary>Offers the names already on the machine; nothing if there are
        /// none.
        /// </summary>
        private void Offer()
        {
            if (!live || !variable)
            {
                return;
            }
            if (Aside())
            {
                // The click was for the node under this, and the field has already
                // taken the focus off it: hand it back.
                if (box != null)
                {
                    box.DeactivateInputField();
                }
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
                return;
            }
            Choices.Open(transform as RectTransform, Variables.Known(), Picked);
        }

        private void Picked(string name)
        {
            if (!variable || string.IsNullOrEmpty(name))
            {
                return;
            }
            Variable = Bindings.Tidied(name);
            if (box != null)
            {
                box.text = Variable == null ? "" : Variable;
            }
            Paint();
            Raise();
        }

        private void Typed(string text)
        {
            // Not only Enter: this also arrives when the box loses focus and when
            // it is switched off, and neither of those is the cell being edited.
            if (swapping || !variable)
            {
                return;
            }
            // Held to Besiege's name rules. An empty box stays in variable mode:
            // falling back to key mode made the button impossible to click back.
            string tidied = Bindings.Tidied(text);
            // Nothing to raise if nothing changed: a box put out of reach for a
            // drag announces its text, and answering it redrew the node under the
            // hand.
            bool same = tidied == Variable;
            Variable = tidied;
            if (box != null && !box.isFocused)
            {
                box.text = Variable == null ? "" : Variable;
            }
            Paint();
            if (!same)
            {
                Raise();
            }
        }

        private void Update()
        {
            // The hold given back the moment it can no longer be given back by
            // hand. Switching a `Canvas` off leaves its objects active, so
            // `OnDisable` never runs and no click can ever end the listening --
            // and the menu count this holds would stay up, which stops the game's
            // own wheel zooming.
            if (held)
            {
                if (!looked)
                {
                    looked = true;
                    roof = GetComponentInParent<Canvas>();
                }
                if (!gameObject.activeInHierarchy
                    || (roof != null && !roof.isActiveAndEnabled))
                {
                    Give();
                }
            }
            // The artwork exists only once the mapper has built a key selector;
            // keep asking.
            if (modeIcon == null && MapperArt.Ready)
            {
                Paint();
            }
            if (swallow)
            {
                bool down = Input.GetMouseButton(0) || Input.GetMouseButton(1)
                         || Input.GetMouseButton(2);
                settled = down ? 0 : settled + 1;
                if (settled > 1)
                {
                    // The binding press is over and no click came: the pointer
                    // left, or a rebuild.
                    swallow = false;
                }
            }
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
            swallow = Mouse(caught);
            settled = 0;
            Paint();
            Raise();
        }

        /// <summary>Whether a keycode is one of the mouse buttons, which are
        /// bindable and are also how the plate is clicked.</summary>
        private static bool Mouse(KeyCode code)
        {
            return code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6;
        }

        private void Raise()
        {
            if (Changed != null)
            {
                Changed(this);
            }
        }

        /// <summary>Holds Besiege's keyboard off while listening, raised and
        /// dropped once, through <see cref="ZoomGuard.Menu"/> so the node editor
        /// can tell our menu count from the game's.</summary>
        private void Hold(bool on)
        {
            if (held == on)
            {
                return;
            }
            try
            {
                ZoomGuard.Menu(on);
                held = on;
            }
            catch (Exception)
            {
                held = false;
            }
        }

        private void OnDisable()
        {
            // A list offered from a row that has just been switched off would hang
            // over the panel with nothing under it.
            Choices.Close();
            listening = false;
            if (waiting == this)
            {
                waiting = null;
            }
            Hold(false);
        }

        private Canvas roof;
        private bool looked;

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
