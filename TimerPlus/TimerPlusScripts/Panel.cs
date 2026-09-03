using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// The block's table, drawn in Besiege's own interface through UI Factory and
    /// docked under the block mapper so the two read as one window with a seam.
    ///
    /// UI Factory is a soft dependency: without it none of this runs, and the block
    /// hands its controls back to Besiege's own mapper -- see
    /// <see cref="TimerPlusBehaviour.ShowStock"/>. Every mention of `Besiege.UI` is
    /// in <see cref="UIF"/> for that reason.
    ///
    /// One instance, on a DontDestroyOnLoad object, watching the mapper. It serves
    /// whichever Timer Plus block is open, so nothing here holds a control by
    /// reference across an open: two blocks of the same kind do not share their
    /// mapper controls, and a row that captured one would show the first block's
    /// values and write to it.
    /// </summary>
    public class Panel : MonoBehaviour
    {
        // Under uGUI's own Dropdown canvas at 30000, so nothing of ours can end up
        // over a list Besiege spawns.
        private const int CanvasOrder = 2400;

        /// <summary>
        /// The narrowest the table is drawn, whatever the mapper above it does.
        ///
        /// The panel otherwise takes the mapper's width, so the two read as one
        /// window with a seam -- but the mapper is narrower than two full column
        /// headings need, and "ACTIVATE" and "DURATION" shrunk to fit are worse
        /// than a panel a little wider than what it hangs from. The seam still
        /// lines up: the panel is docked by its left edge, so the extra is all on
        /// the right.
        /// </summary>
        private const float MinWidth = 540f;

        private const float DefaultWidth = MinWidth;
        private const float Margin = 10f;
        private const float RowHeight = 24f;
        private const float RowGap = 3f;
        private const float HeadHeight = 20f;
        private const float RuleGap = 6f;

        /// <summary>The strip down the right the scrollbar lives in. The rows stop
        /// short of it, or the bar is drawn over the end of every one of them.</summary>
        private const float BarGutter = 18f;

        private const float MaxHeight = 660f;
        private const float MinHeight = 180f;

        /// <summary>Fixed column widths. The three switches and the delete cross do
        /// not get wider on a wider mapper; the four that hold values do.</summary>
        private const float SwitchWidth = 26f;
        private const float CrossWidth = 20f;
        private const float NumberWidth = 22f;
        private const float ColGap = 3f;

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Color RuleInk = new Color(0.55f, 0.62f, 0.72f, 0.30f);

        /// <summary>
        /// True once the panel has drawn a block successfully, which is what tells
        /// every Timer Plus block to take its controls off Besiege's own mapper --
        /// and to leave them off. Latched rather than following the panel up and
        /// down: each change to a `DisplayInMapper` marks the mapper dirty and costs
        /// it a rebuild of all its widgets, so a flag that went off on every close
        /// would cost three rebuilds a visit on a block with two hundred controls.
        ///
        /// Static because the block asks and there is only ever one panel.
        /// </summary>
        public static bool Usable;

        /// <summary>Set once the panel has given up -- no UI Factory, or a build
        /// that threw -- so the stock mapper is the only way to set a block and
        /// keeps its controls for good.</summary>
        public static bool Failed;

        private float width = DefaultWidth;
        private Canvas canvas;
        private GameObject window;
        private RectTransform windowRect;
        private ScrollRect scroll;
        private Transform content;
        private Camera mapperEye;

        private bool hooked;
        private bool ready;
        private bool said;

        /// <summary>The block the table was built for and how many rows it had.
        /// Both are why a rebuild happens: the geometry is per row count, and the
        /// bindings are per block.</summary>
        private TimerPlusBehaviour served;
        private int builtRows = -1;
        private float builtWidth = -1f;

        /// <summary>True while the panel is writing its controls from the block, so
        /// their own change events do not echo back into it.</summary>
        private bool filling;

        /// <summary>Settings changed here that Besiege has not been told about yet.
        /// Assigning `MapperType.Value` writes the live value only, so a panel that
        /// stopped there would be heard now and forgotten on save. Committing
        /// reserialises the block and adds an undo entry, so a drag writes live
        /// every frame and commits once, when the mouse comes up.</summary>
        private readonly List<MapperType> pending = new List<MapperType>();

        // ---- the rows, as built ----------------------------------------------

        private class Cells
        {
            public GameObject Frame;
            public Text Number;
            public KeyCell Activate;
            public InputField Wait;
            public InputField Duration;
            public Toggle Hold;
            public Toggle Stop;
            public Toggle Loop;
            public KeyCell Emulate;
        }

        private readonly List<Cells> table = new List<Cells>();
        private Text status;
        private readonly Text[] heads = new Text[HeadNames.Length];

        private int sortColumn = -1;
        private bool sortAscending = true;

        // ---- lifecycle -------------------------------------------------------

        private void Start()
        {
            if (!UIF.Available)
            {
                // Not "not installed": UI Factory loads its bundle a moment after
                // the mod does. Asked again on a slow tick below.
                return;
            }
            Ready();
        }

        private float askAt;

        private void Ready()
        {
            if (ready)
            {
                return;
            }
            ready = true;
            Hook();
        }

        private void Hook()
        {
            if (hooked)
            {
                return;
            }
            try
            {
                BlockMapper.onMapperOpen += MapperOpened;
                BlockMapper.onMapperClose += MapperClosed;
                hooked = true;
            }
            catch (Exception e)
            {
                Log.Warn("no panel, could not watch the mapper: " + e.Message);
                Failed = true;
            }
        }

        private void OnDestroy()
        {
            if (!hooked)
            {
                return;
            }
            try
            {
                BlockMapper.onMapperOpen -= MapperOpened;
                BlockMapper.onMapperClose -= MapperClosed;
            }
            catch (Exception) { }
            hooked = false;
        }

        private void MapperOpened()
        {
            if (Failed || !ready)
            {
                return;
            }
            TimerPlusBehaviour block = null;
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper != null && mapper.Block != null)
                {
                    block = mapper.Block.GetComponent<TimerPlusBehaviour>();
                }
            }
            catch (Exception) { }

            if (block == null)
            {
                Hide();
                return;
            }

            try
            {
                Show(block);
            }
            catch (Exception e)
            {
                Log.Warn("the panel could not open, so the stock mapper stands: " + e);
                Hide();
                Failed = true;
                // Whatever it was, it is not coming back this session, and the
                // block has to be settable somehow.
                Usable = false;
                block.ShowStock(true);
            }
        }

        private void MapperClosed()
        {
            // onMapperClose is a plain Action, so anything thrown here stops the
            // rest of Besiege's own close handling and the menu stays up.
            try { Hide(); }
            catch (Exception e) { Log.Warn("the panel could not close: " + e.Message); }
        }

        private void Show(TimerPlusBehaviour block)
        {
            Rect frame;
            if (MapperFrame(out frame))
            {
                Widen(frame);
            }

            if (served != block)
            {
                // The sort marker says which column *this* block was last sorted
                // by, and a different block was not sorted at all.
                sortColumn = -1;
                sortAscending = true;
            }
            if (served != block || builtRows != block.Count
                || Mathf.Abs(builtWidth - width) > 0.5f || window == null)
            {
                served = block;
                if (!Build())
                {
                    Hide();
                    return;
                }
            }

            Usable = true;
            served.ShowStock(false);
            window.SetActive(true);
            // Whatever the last visit was told is not news about this one.
            Say("", UIF.QuietInk);
            Fill();
            Canvas.ForceUpdateCanvases();
            Dock();
            Curtain(true);
        }

        private void Hide()
        {
            if (window != null)
            {
                window.SetActive(false);
            }
            CommitPending();
        }

        // ---- building --------------------------------------------------------

        private bool Build()
        {
            try
            {
                Teardown();
                if (!BuildCanvas())
                {
                    return false;
                }
                window = UIF.Spawn(UIF.WindowPrefab, canvas.transform);
                if (window == null)
                {
                    return false;
                }
                windowRect = window.GetComponent<RectTransform>();
                // Owned rather than inherited: a prefab's rect can be anchored any
                // way its author liked, and the docking below measures from the
                // middle of the screen.
                windowRect.anchorMin = new Vector2(0.5f, 0.5f);
                windowRect.anchorMax = new Vector2(0.5f, 0.5f);
                windowRect.pivot = new Vector2(0.5f, 0.5f);

                Untitle();
                content = ScrollContent();
                // Above the window, on the canvas: a tooltip parented into the
                // scrolling content would be clipped with the row it explains and
                // drawn under whichever row came after it.
                Tip.Tips.Home(canvas.GetComponent<RectTransform>());

                float height = Layout();
                builtRows = served.Count;
                builtWidth = width;
                FitContent(height);
                windowRect.sizeDelta = new Vector2(width, Mathf.Min(height, MaxHeight));
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("could not build the panel: " + e);
                // Or a half-built frame is left on screen with nothing in it.
                Teardown();
                return false;
            }
        }

        private bool BuildCanvas()
        {
            if (canvas != null)
            {
                return true;
            }
            // Its own canvas, not UI Factory's shared one and not a bare
            // GameObject. The Window's viewport clips with a stencil Mask and so
            // does Besiege's chat window; two masks at the same depth cut holes in
            // each other. A bare parent would have a zero-sized rect, which an
            // on-screen clamp reads as "this can never fit".
            GameObject go = new GameObject("TimerPlusCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // UI Factory authors against 1920x1080 matching on height. Anything else
            // draws the game's own widgets at the wrong size beside the game's own
            // interface, which is the one thing borrowing them was to prevent.
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();
            return true;
        }

        /// <summary>
        /// The panel has no title of its own -- it is the mapper's lower half -- so
        /// the Window prefab's TopBar goes, and with it the drag handle and the
        /// close cross that would shut only this half. The ScrollView is anchored
        /// below the bar, so hiding the bar alone leaves a bar's worth of empty
        /// frame at the top and it has to be stretched over the whole window.
        /// </summary>
        private void Untitle()
        {
            Transform bar = Attach.Find(window.transform, "TopBar");
            if (bar != null)
            {
                bar.gameObject.SetActive(false);
            }
            Transform view = Attach.Find(window.transform, "ScrollView");
            if (view == null)
            {
                return;
            }
            RectTransform rect = view.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The Window prefab is a frame plus a full ScrollRect over
        /// Viewport/Content. Rows put on the window itself are drawn behind the
        /// scroll view and never seen, and the scroll view is left holding the
        /// prefab's own 500-unit placeholder -- an empty window with a scrollbar
        /// beside it.
        /// </summary>
        private Transform ScrollContent()
        {
            scroll = window.GetComponentInChildren<ScrollRect>(true);
            if (scroll == null || scroll.content == null)
            {
                Log.Warn("the UI Factory Window has no ScrollRect; rows go on the frame.");
                return window.transform;
            }
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = RowHeight;
            if (scroll.horizontalScrollbar != null)
            {
                scroll.horizontalScrollbar.gameObject.SetActive(false);
            }
            Clip();
            Rail();

            // The wheel over the panel scrolls this and zooms the camera at the
            // same time, because nothing told Besiege not to. Enter and exit reach
            // the whole chain of parents, so one guard on the window covers every
            // row in it.
            if (window.GetComponent<ZoomGuard>() == null)
            {
                window.AddComponent<ZoomGuard>();
            }
            return scroll.content;
        }

        /// <summary>
        /// The prefab's Viewport carries the Mask that should clip the rows, and it
        /// arrives anchored to a corner at zero size, so it clips nothing at all.
        /// Sizing it is still not enough on its own -- see <see cref="Curtain"/>,
        /// which is what actually keeps a row inside the frame -- but a viewport
        /// with a real rect is what everything else measures against.
        /// </summary>
        private void Clip()
        {
            RectTransform view = scroll.viewport;
            if (view == null)
            {
                Transform found = Attach.Find(window.transform, "Viewport");
                if (found != null)
                {
                    view = found.GetComponent<RectTransform>();
                }
                scroll.viewport = view;
            }
            if (view == null)
            {
                return;
            }
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.pivot = new Vector2(0f, 1f);
            view.offsetMin = Vector2.zero;
            view.offsetMax = new Vector2(-BarGutter, 0f);

            Mask mask = view.GetComponent<Mask>();
            if (mask != null)
            {
                mask.enabled = true;
                // The window already has a background; this would be a second plate
                // over it.
                mask.showMaskGraphic = false;
            }
            else if (view.GetComponent<RectMask2D>() == null)
            {
                view.gameObject.AddComponent<RectMask2D>();
            }
        }

        /// <summary>The scrollbar into the gutter the rows leave for it.</summary>
        private void Rail()
        {
            UnityEngine.UI.Scrollbar bar = scroll.verticalScrollbar;
            if (bar == null)
            {
                return;
            }
            RectTransform rect = bar.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(BarGutter, 0f);
                rect.anchoredPosition = Vector2.zero;
            }
            bar.gameObject.SetActive(true);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void Teardown()
        {
            table.Clear();
            status = null;
            for (int i = 0; i < heads.Length; i++)
            {
                heads[i] = null;
            }
            if (window != null)
            {
                // Destroy is deferred to the end of the frame, so the old window
                // would otherwise be drawn over the one that replaced it.
                window.SetActive(false);
                Destroy(window);
            }
            window = null;
            windowRect = null;
            scroll = null;
            content = null;
            builtRows = -1;
            builtWidth = -1f;
        }

        // ---- geometry --------------------------------------------------------

        private float Full { get { return width - Margin * 2f - BarGutter; } }

        /// <summary>
        /// Where each column starts and how wide it is. The three switches and the
        /// delete cross are fixed; what is left is shared between the two key
        /// columns and the two numbers, in that proportion -- a key or a variable
        /// name needs the room and a number does not.
        /// </summary>
        private void Columns(out float[] x, out float[] w)
        {
            float fixedWidth = NumberWidth + SwitchWidth * 3f + CrossWidth + ColGap * 8f;
            float rest = Mathf.Max(160f, Full - fixedWidth);
            // The activation column has to hold a keycode name as well as "blk",
            // and the emulation column a variable name, so the two of them get most
            // of what is going; a time needs four characters and no more.
            float act = rest * 0.24f;
            float num = rest * 0.185f;
            float emu = rest - act - num * 2f;

            w = new float[] { NumberWidth, act, num, num,
                              SwitchWidth, SwitchWidth, SwitchWidth,
                              emu, CrossWidth };
            x = new float[w.Length];
            float at = Margin;
            for (int i = 0; i < w.Length; i++)
            {
                x[i] = at;
                at += w[i] + ColGap;
            }
        }

        private float Layout()
        {
            float y = Margin;
            float[] x, w;
            Columns(out x, out w);

            y = Header(y, x, w);

            table.Clear();
            int count = served.Count;
            for (int i = 0; i < count; i++)
            {
                table.Add(BuildRow(i, ref y, x, w));
            }

            y = Plus(y);
            y = Rule(y);
            y = Footer(y);
            return y + Margin;
        }

        /// <summary>
        /// The column headings. The first is the timer's number, which is not a
        /// setting and not one of <see cref="Table"/>'s columns -- clicking it
        /// sorts by wait, because the number *is* the wait order and putting the
        /// numbers in order is the only thing sorting by them could mean.
        /// </summary>
        private static readonly string[] HeadNames =
        {
            "#", "ACTIVATE", "WAIT", "DURATION", "H", "S", "L", "EMULATE"
        };

        /// <summary>
        /// What each heading says on hover. The three switches are a letter apiece
        /// because a column holding a tick box has no room for a word, so this is
        /// where their names live. The number column says nothing: a column of
        /// numbers headed "#" has already said it.
        /// </summary>
        private static readonly string[] HeadTips =
        {
            "",
            "What starts this row\nblk follows the block's own key",
            "Seconds from the start to the press",
            "How long the press is held",
            "Hold to run",
            "Allow stop",
            "Loop",
            "The key or variable this row presses"
        };


        /// <summary>
        /// The column headings, each a button that sorts by its column. A second
        /// click on the same one reverses it.
        /// </summary>
        private float Header(float y, float[] x, float[] w)
        {
            for (int c = 0; c < HeadNames.Length; c++)
            {
                GameObject go = UIF.Spawn(UIF.ButtonPrefab, content);
                if (go == null)
                {
                    continue;
                }
                UIF.Fit(go.GetComponent<RectTransform>(), x[c], y, w[c], HeadHeight);
                // The prefab's own swell grows the whole heading, which on a
                // column this narrow carries the word into its neighbour; ours
                // grows the lettering in place. It also stops the press animation
                // shrinking the click target out from under the pointer, which on
                // a small control eats the click outright.
                UIF.NoSwell(go);
                Text label = Pin(go, HeadNames[c], UIF.QuietInk);
                Grow(go, label);
                heads[c] = label;
                Tip.On(go, HeadTips[c].Length == 0
                           ? "" : HeadTips[c] + "\n(click to sort)");
                // The number column sorts by wait: heading 0 is not a column of
                // Table's, and every heading after it is one along.
                int column = c == 0 ? Table.ColWait : c - 1;
                Button click = go.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(delegate { SortBy(column); });
                }
            }
            y += HeadHeight + RowGap;
            return y;
        }

        private Cells BuildRow(int index, ref float y, float[] x, float[] w)
        {
            Cells cells = new Cells();

            // One object holding the whole row, so the panel's own clipping has a
            // single thing to decide about and the cells inside it never argue with
            // it over who is showing.
            GameObject frame = new GameObject("Row" + index);
            frame.transform.SetParent(content, false);
            frame.AddComponent<RectTransform>();
            UIF.Fit(frame.GetComponent<RectTransform>(), 0f, y, width, RowHeight);
            cells.Frame = frame;

            Transform host = frame.transform;

            // Written by Fill, which is where the wait order is worked out.
            cells.Number = Cell(host, "", x[0], w[0], UIF.QuietInk, TextAnchor.MiddleCenter);

            cells.Activate = KeyCell.Make(host, x[1], 0f, w[1], RowHeight, Bindings.Inherited);
            cells.Activate.Row = index;
            cells.Activate.IsActivation = true;
            cells.Activate.What = "What starts this timer";
            cells.Activate.Changed = RowKeyChanged;

            cells.Wait = Number(host, x[2], w[2], index, true);
            cells.Duration = Number(host, x[3], w[3], index, false);
            Tip.On(cells.Wait == null ? null : cells.Wait.gameObject,
                   "Seconds from the start to the press");
            Tip.On(cells.Duration == null ? null : cells.Duration.gameObject,
                   "How long the press is held");

            cells.Hold = Box(host, x[4], w[4], index, Table.ColHold, "Hold to run");
            cells.Stop = Box(host, x[5], w[5], index, Table.ColStop, "Allow stop");
            cells.Loop = Box(host, x[6], w[6], index, Table.ColLoop, "Loop");

            cells.Emulate = KeyCell.Make(host, x[7], 0f, w[7], RowHeight, Bindings.Unset);
            cells.Emulate.Row = index;
            cells.Emulate.IsActivation = false;
            cells.Emulate.What = "What this timer presses";
            cells.Emulate.Changed = RowKeyChanged;

            GameObject cross = UIF.Spawn(UIF.ButtonPrefab, host);
            if (cross != null)
            {
                UIF.Fit(cross.GetComponent<RectTransform>(), x[8], 0f, w[8], RowHeight);
                UIF.NoSwell(cross);
                Grow(cross, Pin(cross, "x", UIF.QuietInk));
                Tip.On(cross, "Delete this timer");
                int at = index;
                Button click = cross.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(delegate { Delete(at); });
                }
            }

            y += RowHeight + RowGap;
            return cells;
        }

        /// <summary>The last row of the table: one button across it that adds
        /// another.</summary>
        private float Plus(float y)
        {
            GameObject go = UIF.Spawn(UIF.ButtonPrefab, content);
            if (go != null)
            {
                UIF.Fit(go.GetComponent<RectTransform>(), Margin, y, Full, RowHeight);
                UIF.NoSwell(go);
                Grow(go, Pin(go, "+", UIF.QuietInk));
                Tip.On(go, "Add a timer\nIt copies the row above and carries the wait on");
                Button click = go.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(AddRow);
                }
            }
            return y + RowHeight + RowGap;
        }

        private float Footer(float y)
        {
            GameObject go = UIF.Spawn(UIF.ButtonPrefab, content);
            if (go != null)
            {
                UIF.Fit(go.GetComponent<RectTransform>(), Margin, y, Full, RowHeight + 4f);
                UIF.NoSwell(go);
                Grow(go, Pin(go, "CONVERT TO TIMER BLOCKS", UIF.Ink));
                Tip.On(go, "Build one of Besiege's own timer blocks per row,\n"
                         + "beside this one and ready to move.\nOne undo takes them back");
                Button click = go.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(DoConvert);
                }
            }
            y += RowHeight + 4f + RowGap;

            // Empty unless something has to be said. It used to carry a running
            // "N rows -> N timer blocks", which said nothing the table above it did
            // not already show; what is left is the place a refusal or a
            // confirmation goes.
            status = Label("", Margin, y, Full, RowHeight, UIF.QuietInk,
                           TextAnchor.MiddleLeft);
            return y + RowHeight;
        }

        // ---- the small pieces ------------------------------------------------

        private Text Label(string text, float x, float y, float w, float h,
                           Color colour, TextAnchor align)
        {
            GameObject go = UIF.Spawn(UIF.TextPrefab, content);
            if (go == null)
            {
                return null;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, w, h);
            Text label = go.GetComponent<Text>();
            if (label != null)
            {
                UIF.Style(label, colour, align);
                label.text = text;
                // A caption is never clicked, and one wider than its slot would take
                // the click meant for whatever it overhangs.
                label.raycastTarget = false;
            }
            return label;
        }

        /// <summary>
        /// A prefab's caption, pinned to its own control and made deaf. The Text
        /// Button's label is a child authored at a fixed width for the prefab's own
        /// size, so on a narrow control it overhangs its neighbour and wins the
        /// clicks meant for it.
        /// </summary>
        private static Text Pin(GameObject control, string text, Color colour)
        {
            Text label = control.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                return null;
            }
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label.raycastTarget = false;
            UIF.Style(label, colour, TextAnchor.MiddleCenter);
            UIF.Shrink(label, 8);
            label.text = text;
            return label;
        }

        /// <summary>
        /// Puts the hover swell on a control's lettering rather than on the control.
        /// A hovered control grows about 15%, which is right for a button and wrong
        /// for a table cell: it carries the word into the next column, and the press
        /// animation shrinks the click target out from under the pointer, so the
        /// outer few percent of the control never fires at all.
        /// </summary>
        private static void Grow(GameObject control, Text label)
        {
            if (control == null || label == null)
            {
                return;
            }
            Swell swell = control.AddComponent<Swell>();
            swell.grows = label.transform;
            swell.grown = 1.12f;
        }

        private Toggle Box(Transform host, float x, float w, int index, int column,
                           string tip)
        {
            GameObject go = UIF.Spawn(UIF.TogglePrefab, host);
            if (go == null)
            {
                return null;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, 0f, w, RowHeight);
            UIF.NoSwell(go);
            // No caption: the column heading is a single letter for want of room,
            // and the tooltip is where the switch says what it is.
            Pin(go, "", UIF.Ink);
            Tip.On(go, tip);
            Toggle toggle = go.GetComponent<Toggle>();
            if (toggle != null)
            {
                int at = index;
                int which = column;
                toggle.onValueChanged.AddListener(delegate(bool on) { Flipped(at, which, on); });
            }
            return toggle;
        }

        private InputField Number(Transform host, float x, float w, int index, bool wait)
        {
            GameObject go = UIF.Spawn(UIF.InputPrefab, host);
            if (go == null)
            {
                return null;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, 0f, w, RowHeight);
            InputField field = go.GetComponent<InputField>();
            if (field != null)
            {
                UIF.Style(field.textComponent, UIF.Ink, TextAnchor.MiddleRight);
                UIF.Style(field.placeholder as Text, UIF.QuietInk, TextAnchor.MiddleRight);
                Text ghost = field.placeholder as Text;
                if (ghost != null)
                {
                    ghost.text = "0";
                }
                field.contentType = InputField.ContentType.DecimalNumber;
                int at = index;
                bool which = wait;
                // onEndEdit, not onValueChanged: the latter would apply the 2 of a
                // 25 while it is still being typed.
                field.onEndEdit.AddListener(delegate(string typed) { Typed(at, which, typed); });
            }
            return field;
        }

        /// <summary>A label inside a row, as opposed to one on the content.</summary>
        private Text Cell(Transform host, string text, float x, float w,
                          Color colour, TextAnchor align)
        {
            GameObject go = UIF.Spawn(UIF.TextPrefab, host);
            if (go == null)
            {
                return null;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, 0f, w, RowHeight);
            Text label = go.GetComponent<Text>();
            if (label != null)
            {
                UIF.Style(label, colour, align);
                label.text = text;
                label.raycastTarget = false;
            }
            return label;
        }

        private Image Plate(Transform host, float x, float y, float w, float h, Color colour)
        {
            GameObject go = new GameObject("Plate");
            go.transform.SetParent(host, false);
            Image image = go.AddComponent<Image>();
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, w, h);
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private float Rule(float y)
        {
            Plate(content, Margin, y + RuleGap * 0.5f, Full, 2f, RuleInk);
            return y + RuleGap + 2f;
        }

        // ---- reading the block -----------------------------------------------

        /// <summary>
        /// Writes every cell from the block. On every open, and after any edit that
        /// rearranges the rows.
        ///
        /// Every cell is written every time, not only the ones that might have
        /// changed. A window kept and reused for the next block of the same shape
        /// would otherwise show the previous block's values, which is a bug that
        /// looks like a save fault and is an identity fault -- two blocks of the
        /// same kind do not share their mapper controls.
        /// </summary>
        private void Fill()
        {
            if (served == null)
            {
                return;
            }
            filling = true;
            try
            {
                for (int i = 0; i < table.Count && i < served.Rows.Count; i++)
                {
                    Row row = served.Rows[i];
                    Cells cells = table[i];
                    if (!row.Ready)
                    {
                        continue;
                    }
                    cells.Activate.Show(row.Activate);
                    cells.Emulate.Show(row.Emulate);
                    Set(cells.Wait, row.Wait.Value);
                    Set(cells.Duration, row.Duration.Value);
                    if (cells.Hold != null) cells.Hold.isOn = row.Hold.IsActive;
                    if (cells.Stop != null) cells.Stop.isOn = row.Stop.IsActive;
                    if (cells.Loop != null) cells.Loop.isOn = row.Loop.IsActive;
                }
                Numbers();
                Heads();
            }
            finally
            {
                filling = false;
            }
        }

        private static void Set(InputField field, float value)
        {
            if (field == null || field.isFocused)
            {
                // Never while it has focus, or the caret jumps out from under
                // whoever is typing.
                return;
            }
            field.text = Spell(value);
        }

        /// <summary>A time as a cell shows it: two decimals, and no trailing
        /// zeroes past what a wait is ever set to.</summary>
        private static string Spell(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Numbers the rows by when they fire: the shortest wait is timer 1,
        /// whatever order the rows happen to be in.
        ///
        /// A rank rather than a row index, so the number answers the question
        /// somebody reading the table is actually asking -- which of these goes
        /// first. Sorting by WAIT therefore puts the numbers in order and every
        /// other sort scatters them, which is the point: the number is the one
        /// column that does not move when the rows do.
        ///
        /// Ties keep the row order, so two rows on the same wait are numbered top
        /// to bottom rather than arbitrarily. An insertion sort over the indices,
        /// which is stable and is as much machinery as thirty-two rows deserve.
        /// </summary>
        private void Numbers()
        {
            int count = Mathf.Min(table.Count, served.Rows.Count);
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            for (int i = 1; i < count; i++)
            {
                int moving = order[i];
                float wait = Wait(moving);
                int at = i;
                while (at > 0 && Wait(order[at - 1]) > wait)
                {
                    order[at] = order[at - 1];
                    at--;
                }
                order[at] = moving;
            }
            for (int rank = 0; rank < count; rank++)
            {
                Text label = table[order[rank]].Number;
                if (label != null)
                {
                    label.text = (rank + 1).ToString();
                }
            }
        }

        private float Wait(int row)
        {
            Row one = served.Rows[row];
            return one.Ready ? one.Wait.Value : 0f;
        }

        /// <summary>Marks the heading of the column the table was last sorted
        /// by.</summary>
        private void Heads()
        {
            for (int c = 0; c < heads.Length; c++)
            {
                if (heads[c] == null)
                {
                    continue;
                }
                // Heading 0 is the number column and sorts nothing, so the sort
                // marker is one along from the column it belongs to.
                bool on = c > 0 && c - 1 == sortColumn;
                heads[c].text = HeadNames[c] + (on ? (sortAscending ? " ^" : " v") : "");
                heads[c].color = on ? UIF.Ink : UIF.QuietInk;
            }
        }

        // ---- what the controls do --------------------------------------------

        private void RowKeyChanged(KeyCell cell)
        {
            if (filling || served == null || cell.Row < 0 || cell.Row >= served.Rows.Count)
            {
                return;
            }
            Row row = served.Rows[cell.Row];
            if (!row.Ready)
            {
                return;
            }
            MKey key = cell.IsActivation ? row.Activate : row.Emulate;
            Apply(key, cell);
            Queue(key);
        }

        /// <summary>
        /// Writes a cell through to the key it stands for.
        ///
        /// The cell's *mode* decides, not which of its two values happens to be
        /// set: a cell switched to a variable with nothing typed into it yet is an
        /// unbound key, and must not fall back to whatever keycode it was showing
        /// a moment ago.
        /// </summary>
        private static void Apply(MKey key, KeyCell cell)
        {
            if (cell.UsesVariable)
            {
                if (string.IsNullOrEmpty(cell.Variable))
                {
                    Bindings.Clear(key);
                }
                else
                {
                    Bindings.BindVariable(key, cell.Variable);
                }
                return;
            }
            if (cell.Code != KeyCode.None)
            {
                Bindings.Bind(key, cell.Code);
            }
            else
            {
                Bindings.Clear(key);
            }
        }

        private void Flipped(int index, int column, bool on)
        {
            if (filling || served == null || index >= served.Rows.Count)
            {
                return;
            }
            Row row = served.Rows[index];
            if (!row.Ready)
            {
                return;
            }
            MToggle toggle = column == Table.ColHold ? row.Hold
                : (column == Table.ColStop ? row.Stop : row.Loop);
            toggle.IsActive = on;
            Queue(toggle);
        }

        private void Typed(int index, bool wait, string text)
        {
            if (filling || served == null || index >= served.Rows.Count)
            {
                return;
            }
            Row row = served.Rows[index];
            if (!row.Ready)
            {
                return;
            }
            MSlider slider = wait ? row.Wait : row.Duration;

            float value;
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out value))
            {
                // Not a number: put the real value back, so the box never keeps
                // something the block does not hold.
                Set(wait ? table[index].Wait : table[index].Duration, slider.Value);
                return;
            }
            // Negative seconds are not a time. The upper end is deliberately open:
            // the slider is declared unclamped, exactly as Besiege's own timer
            // declares its two, so an event four minutes in is one row rather than
            // a chain of them.
            //
            // Rounded to what the cell can show, so that what is stored is what is
            // written back below -- otherwise typing 0.055 leaves a box reading
            // 0.06 over a block holding 0.055, and the next person to touch it
            // stores the 0.06 they were shown. Ten milliseconds is half a tick, so
            // nothing the timer can resolve is lost.
            value = Mathf.Max(0f, Mathf.Round(value * 100f) / 100f);
            slider.Value = value;
            Queue(slider);
            // Written back, so a rejected value changes in front of whoever typed
            // it rather than silently.
            Set(wait ? table[index].Wait : table[index].Duration, value);
            if (wait)
            {
                // A wait decides which timer this is.
                Numbers();
            }
        }

        private void SortBy(int column)
        {
            if (served == null)
            {
                return;
            }
            sortAscending = column == sortColumn ? !sortAscending : true;
            sortColumn = column;
            List<MapperType> touched = new List<MapperType>();
            Table.Sort(served, column, sortAscending, touched);
            Commit(touched);
            Fill();
        }

        private void AddRow()
        {
            if (served == null)
            {
                return;
            }
            List<MapperType> touched = new List<MapperType>();
            int made = Table.Add(served, touched);
            if (made < 0)
            {
                Say("this block already holds " + TimerPlusBehaviour.MaxRows
                    + " rows, which is as many as it can.", UIF.Hot);
                return;
            }
            Commit(touched);
            Rebuild();
        }

        private void Delete(int index)
        {
            if (served == null)
            {
                return;
            }
            if (served.Count <= 1)
            {
                Say("a block keeps at least one row.", UIF.Hot);
                return;
            }
            List<MapperType> touched = new List<MapperType>();
            Table.Remove(served, index, touched);
            Commit(touched);
            Rebuild();
        }

        private void DoConvert()
        {
            if (served == null)
            {
                return;
            }
            try
            {
                int made = Conversion.Into(served);
                // Logged as well as said: the new blocks become the selection,
                // which closes the block mapper, which takes this panel with it --
                // so the line under the button is gone before it can be read. What
                // the player actually sees is the timers arriving under the move
                // tool, which is the same answer the load screen gives.
                Log.Info(made + " timer block(s) added from the table.");
                Say(made + (made == 1 ? " timer block added, " : " timer blocks added, ")
                    + "selected and ready to move. One undo takes them back.", UIF.Live);
            }
            catch (Exception e)
            {
                Say("could not convert: " + e.Message, UIF.Hot);
                Log.Warn("convert failed: " + e);
            }
        }

        /// <summary>A line under the button, until the next thing that has
        /// something to say. Not a log: the log is where the detail goes.</summary>
        private void Say(string text, Color colour)
        {
            if (status == null)
            {
                return;
            }
            status.text = text;
            status.color = colour;
        }

        /// <summary>
        /// The row count changed, so the table's geometry has. Rebuilt rather than
        /// grown: the rows are laid out against a count and there is no cheaper
        /// honest way to add one in the middle of that.
        /// </summary>
        private void Rebuild()
        {
            TimerPlusBehaviour block = served;
            Teardown();
            served = block;
            if (!Build())
            {
                Hide();
                return;
            }
            window.SetActive(true);
            Fill();
            Canvas.ForceUpdateCanvases();
            Dock();
            Curtain(true);
        }

        // ---- writing back ----------------------------------------------------

        /// <summary>Queues a setting for the commit that happens when the mouse
        /// comes up.</summary>
        private void Queue(MapperType changed)
        {
            if (filling || changed == null || pending.Contains(changed))
            {
                return;
            }
            pending.Add(changed);
        }

        private void CommitPending()
        {
            Commit(pending);
            pending.Clear();
        }

        private static void Commit(List<MapperType> changed)
        {
            if (changed == null)
            {
                return;
            }
            for (int i = 0; i < changed.Count; i++)
            {
                Commit(changed[i]);
            }
        }

        /// <summary>
        /// Writes a setting through the mapper, so it survives a save and can be
        /// undone. `ApplyValue` is the fallback where that machinery is not up.
        /// </summary>
        private static void Commit(MapperType changed)
        {
            if (changed == null)
            {
                return;
            }
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper != null && mapper.Current != null)
                {
                    BlockMapper.OnEditField(mapper.Current, changed);
                    return;
                }
            }
            catch (Exception) { }
            try { changed.ApplyValue(); }
            catch (Exception) { }
        }

        // ---- every frame -----------------------------------------------------

        private void Update()
        {
            if (!ready)
            {
                // UI Factory loads its bundle a moment after the mod does, so "not
                // yet" is not "not installed". Asked on a slow tick rather than
                // every frame: while it is genuinely absent, each ask costs a caught
                // TypeLoadException.
                if (Failed || Time.unscaledTime < askAt)
                {
                    return;
                }
                askAt = Time.unscaledTime + 1f;
                if (UIF.Available)
                {
                    Ready();
                }
                return;
            }
            if (pending.Count > 0 && !Input.GetMouseButton(0))
            {
                CommitPending();
            }
        }

        /// <summary>
        /// In LateUpdate: the mapper is dragged by its own behaviour, so a panel
        /// placed in Update is placed against where the mapper was. Reconciled every
        /// frame rather than left to onMapperClose, which does not fire for every
        /// way a mapper goes away -- clicking off the block, or the block being
        /// deselected -- and the panel was then left hanging over the world.
        /// </summary>
        private void LateUpdate()
        {
            if (window == null || !window.activeSelf)
            {
                return;
            }
            if (!StillOpen())
            {
                Hide();
                return;
            }
            Dock();
            Curtain(false);
        }

        private bool StillOpen()
        {
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper == null || !BlockMapper.IsOpen || mapper.Block == null)
                {
                    return false;
                }
                return mapper.Block.GetComponent<TimerPlusBehaviour>() == served
                    && served != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void FitContent(float height)
        {
            if (scroll == null || scroll.content == null)
            {
                return;
            }
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, height);
            contentHeight = height;
        }

        private float contentHeight;

        private void Dock()
        {
            if (windowRect == null || window == null || !window.activeSelf)
            {
                return;
            }
            Rect frame;
            if (!MapperFrame(out frame))
            {
                return;
            }
            if (Widen(frame))
            {
                Rebuild();
                return;
            }

            float scale = Scale();
            float left = (frame.xMin - Screen.width * 0.5f) * scale;
            float bottom = (frame.yMin - Screen.height * 0.5f) * scale;

            // What is left between the bottom of the mapper and the bottom of the
            // screen. The window takes that and scrolls the rest.
            float room = frame.yMin * scale - Margin;
            float tall = Mathf.Max(MinHeight, Mathf.Min(contentHeight, MaxHeight, room));
            if (Mathf.Abs(windowRect.sizeDelta.y - tall) > 0.5f)
            {
                windowRect.sizeDelta = new Vector2(width, tall);
            }

            Vector2 size = windowRect.sizeDelta;
            windowRect.anchoredPosition =
                new Vector2(left + size.x * 0.5f, bottom - size.y * 0.5f);
        }

        /// <summary>Takes the panel's width from the mapper's. True if it changed,
        /// in which case the rows -- laid out to it -- no longer fit.</summary>
        private bool Widen(Rect frame)
        {
            float wide = Mathf.Max(MinWidth, frame.width * Scale());
            if (Mathf.Abs(wide - width) <= 0.5f)
            {
                return false;
            }
            width = wide;
            return true;
        }

        /// <summary>Canvas units per screen pixel. The scaler matches on height
        /// against a 1080-tall reference, so one unit is one pixel at 1080p.</summary>
        private float Scale()
        {
            return Screen.height > 0 ? Reference.y / Screen.height : 1f;
        }

        // ---- clipping --------------------------------------------------------

        private float curtainAt = float.NaN;
        private float curtainTall = float.NaN;
        private const float Slop = 0.5f;

        /// <summary>
        /// The rows are hidden rather than clipped.
        ///
        /// The Window's Viewport carries a Mask and sizing it does not make it clip:
        /// rows scrolled past the top are still drawn over the mapper above, and the
        /// last one hangs below the frame. Rather than keep guessing at prefab
        /// internals the panel clips itself -- a row is drawn only while all of it is
        /// inside the frame, so nothing is ever cut in half and nothing reaches
        /// outside, which is what a list of whole rows should look like anyway.
        /// </summary>
        private void Curtain(bool force)
        {
            if (scroll == null || scroll.content == null || content == null)
            {
                return;
            }
            float at = scroll.content.anchoredPosition.y;
            float tall = windowRect != null ? windowRect.sizeDelta.y : 0f;
            if (!force && Mathf.Abs(at - curtainAt) < Slop
                       && Mathf.Abs(tall - curtainTall) < Slop)
            {
                return;
            }
            curtainAt = at;
            curtainTall = tall;

            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform row = content.GetChild(i) as RectTransform;
                if (row == null)
                {
                    continue;
                }
                float top = at + row.anchoredPosition.y;
                float bottom = top - row.sizeDelta.y;
                bool inside = top <= Slop && bottom >= -tall - Slop;
                if (row.gameObject.activeSelf != inside)
                {
                    row.gameObject.SetActive(inside);
                }
            }
        }

        // ---- where the mapper is ---------------------------------------------

        /// <summary>
        /// Besiege's mapper window in screen pixels.
        ///
        /// The window is the TALLEST renderer named "Background" -- they all share
        /// its width, and only the frame reaches the bottom edge. Two plausible
        /// alternatives are wrong and both have been shipped by other mods:
        /// "WideShadow" is an eleventh wider and sits higher, and "Visual" is a
        /// 93-pixel button.
        /// </summary>
        private bool MapperFrame(out Rect frame)
        {
            frame = new Rect();
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper == null)
                {
                    return false;
                }
                Renderer[] parts = mapper.GetComponentsInChildren<Renderer>(false);
                Renderer best = null;
                float tallest = 0f;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null || parts[i].gameObject.name != "Background")
                    {
                        continue;
                    }
                    float height = parts[i].bounds.size.y;
                    if (height > tallest)
                    {
                        tallest = height;
                        best = parts[i];
                    }
                }
                if (best == null)
                {
                    return false;
                }
                Camera eye = MapperCamera(best.gameObject.layer);
                if (eye == null)
                {
                    return false;
                }
                frame = ScreenRect(best, eye);
                if (!said)
                {
                    said = true;
                    Log.Info("docking to '" + best.gameObject.name + "' at " + frame
                             + " via camera '" + eye.name + "'");
                }
                return frame.width > 1f && frame.height > 1f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Rect ScreenRect(Renderer part, Camera eye)
        {
            Bounds box = part.bounds;
            Vector3 a = eye.WorldToScreenPoint(new Vector3(box.min.x, box.min.y, box.center.z));
            Vector3 b = eye.WorldToScreenPoint(new Vector3(box.max.x, box.max.y, box.center.z));
            return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                            Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        /// <summary>
        /// The mapper is drawn in the world, so only the camera whose culling mask
        /// includes its layer knows where on screen it lands. The topmost such
        /// camera: the interface is drawn last.
        /// </summary>
        private Camera MapperCamera(int layer)
        {
            if (mapperEye != null && (mapperEye.cullingMask & (1 << layer)) != 0)
            {
                return mapperEye;
            }
            Camera best = null;
            Camera[] all = Camera.allCameras;
            for (int i = 0; i < all.Length; i++)
            {
                if ((all[i].cullingMask & (1 << layer)) == 0)
                {
                    continue;
                }
                if (best == null || all[i].depth > best.depth)
                {
                    best = all[i];
                }
            }
            mapperEye = best;
            return best;
        }
    }
}
