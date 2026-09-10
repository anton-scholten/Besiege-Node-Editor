using System;
using UnityEngine;
using UnityEngine.EventSystems;
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

        /// <summary>What this cell's bubble is actually drawn at. The default is
        /// <see cref="ModeWidth"/>; the logic table asks for a narrower one,
        /// because it has three of these columns to fit where the timer has two and
        /// the bubble is a picture that reads at either size.</summary>
        private float bubble = ModeWidth;

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

        /// <summary>Whether a click with a modifier held belongs to whatever this
        /// cell sits on rather than to the cell. Set on the node editor's cells,
        /// where a modifier picks the node out.</summary>
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

        /// <summary>
        /// Whether the click that is about to arrive has already been used.
        ///
        /// A mouse button is a bindable key, and binding one takes the button
        /// *down*: the matching *up* is what raises the plate's own click, which
        /// would put the cell straight back to listening and lose what it had just
        /// caught. So a mouse binding swallows the click that made it, and the
        /// next one -- a fresh press, meaning what it says -- goes through.
        /// </summary>
        private bool swallow;

        /// <summary>Frames since every mouse button came up, counted only while a
        /// click is being swallowed. One clear frame is given to the click event
        /// before the flag is dropped, because Update and the event system do not
        /// agree on an order within a frame.</summary>
        private int settled;

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

        /// <summary>
        /// The box a name is typed into, built the first time the cell is in
        /// variable mode.
        ///
        /// Most cells are keys and never see it, and a text field is the most
        /// expensive prefab of the three a cell can hold -- a board of a dozen
        /// nodes was building a dozen of them to keep them switched off.
        /// </summary>
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
            }
            // Clicking the box offers the names already on the machine. On the
            // field itself rather than a control beside it: the box is the whole
            // cell in variable mode, and uGUI hands a click to every handler on an
            // object, so the field goes on taking typing.
            Choices.Opener opener = field.AddComponent<Choices.Opener>();
            opener.Clicked = Offer;
            if (guard != null)
            {
                guard.box = box;
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
            // Mid-binding, or mid-typing: do not write over what somebody is doing.
            if (listening || (box != null && box.isFocused))
            {
                return;
            }
            // A key wired to several things at once is not a cell to type in: what
            // it answers to is a list, the board is where a list is edited, and the
            // one thing worth saying here is how many. See `several`.
            several = Bindings.Count(key);
            variable = Bindings.IsVariable(key);
            Variable = variable ? Bindings.Variable(key) : null;
            Code = variable ? KeyCode.None : Bindings.Code(key);
            Paint();
        }

        /// <summary>
        /// Shows a binding that is not a mapper key at all.
        ///
        /// The node editor's ends of the board are a name or a keycode written into
        /// the block's layout rather than into an `MKey`, and a cell is how anybody
        /// would want to edit one.
        /// </summary>
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

        public bool Listening { get { return listening; } }

        /// <summary>
        /// How many things this cell's key answers to, when that is more than one.
        ///
        /// Besiege ORs them -- the key is held while any of them is raised -- so a
        /// gate's input wired to five answers is one input reading five names.
        /// There is nothing useful to show in a cell that wide and nothing safe to
        /// type into it: the count is what it says, in the game's own live colour,
        /// and the board is where the wires are.
        /// </summary>
        private int several;

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
                    plateLabel.text = several + " inputs";
                    plateLabel.color = UIF.Live;
                }
                return;
            }
            if (mode != null && !mode.activeSelf)
            {
                mode.SetActive(true);
            }
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

        /// <summary>
        /// Whether this click belongs to whatever the cell is sitting on rather
        /// than to the cell.
        ///
        /// The node editor picks nodes out with control held, and a cell that took
        /// that click for itself started listening for a key at the same time --
        /// two things from one click, one of them unasked for.
        /// </summary>
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
                Offer();
            }
            Raise();
        }

        private void Listen()
        {
            if (variable || Aside() || several > 1)
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

        /// <summary>
        /// Offers the names already in use on the machine.
        ///
        /// Nothing is offered when there are none: an empty list under the box
        /// says less than the box itself does, and the box is still the way a name
        /// nobody has used yet gets typed.
        /// </summary>
        private void Offer()
        {
            if (!variable)
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
            // Held to Besiege's own rules for a name as it is taken in: cut to
            // its character limit, and split where the game's tag editor splits.
            // A name this could not spell is a name the stock mapper could not
            // edit afterwards.
            // An empty box stays variable mode with nothing bound. Falling back to
            // key mode here is what made the button impossible to click back.
            Variable = Bindings.Tidied(text);
            if (box != null && !box.isFocused)
            {
                box.text = Variable == null ? "" : Variable;
            }
            Paint();
            Raise();
        }

        private void Update()
        {
            // The mapper's own key selector has to have been built once for its
            // artwork to exist, and a cell made before that keeps its lettering
            // until something repaints it. Nothing does, in a window that is not
            // the mapper -- so it is asked for again here until it turns up.
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
                    // The press that bound the button is long over and no click
                    // came of it -- the pointer was dragged off the plate, or the
                    // panel was rebuilt under it.
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

        /// <summary>
        /// Holds Besiege's own keyboard off while this cell is listening, or the
        /// key being bound also drives the camera and fires whatever else is bound
        /// to it. `SetInMenu` is counted on Besiege's side, so it is raised and
        /// dropped exactly once -- including when the panel is torn down with a
        /// cell still listening, which is the usual way to leave the game believing
        /// a menu is open.
        ///
        /// Through <see cref="ZoomGuard.Menu"/> rather than straight to
        /// `StatMaster`, so the count stays this mod's own: the node editor closes
        /// when a menu that is not ours goes up, and a cell listening for a key is
        /// ours.
        /// </summary>
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
