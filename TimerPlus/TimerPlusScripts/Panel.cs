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

        /// <summary>What the table is drawn at until the mapper has been measured.
        /// After that it is the mapper's own width, so the two read as one window
        /// with a seam rather than a panel wider than what it hangs from.</summary>
        private const float DefaultWidth = 434f;

        /// <summary>
        /// The narrowest the logic table is drawn.
        ///
        /// It has three columns holding a key or a variable name where the timer's
        /// has two, and a name shrunk to fit is a name read by leaning in. This is
        /// what leaves each of them room for seven characters at the size the rest
        /// of the panel is lettered in -- as near the mapper's own width as that
        /// allows, and the rest of the way is taken out of the number and switch
        /// columns below rather than off a name. The panel is docked by its left
        /// edge, so what is over is all on the right.
        /// </summary>
        private const float GatesWidth = 434f;

        /// <summary>The key/variable bubble in the logic table, narrower than the
        /// timer's: three columns wear one where the timer has two, and the four
        /// units are worth more to the name beside it than to the picture.</summary>
        private const float GateBubble = 18f;
        private const float Margin = 10f;
        private const float RowHeight = 24f;
        private const float RowGap = 3f;
        private const float HeadHeight = 20f;
        private const float RuleGap = 6f;

        /// <summary>The strip down the right the scrollbar lives in. The rows stop
        /// short of it, or the bar is drawn over the end of every one of them.</summary>
        private const float BarGutter = 18f;

        /// <summary>The scrollbar drawn in that gutter, and its inset from the
        /// window's edge.</summary>
        private const float BarWidth = 8f;
        private const float BarInset = 5f;

        private static readonly Color RailInk = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color GripInk = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>
        /// The height of the strip along the bottom of the window: the rule, the
        /// "+", the convert button and the line anything has to be said on.
        ///
        /// Those are not rows of the table and do not scroll with it -- a table
        /// with thirty rows in it is exactly when somebody wants the convert button,
        /// and it was at the bottom of a list they had to scroll to the end of.
        /// A constant rather than a measurement, so the viewport can be held clear
        /// of the strip before the strip is built.
        ///
        /// The strip has no plate of its own. It had one for a while, to stop rows
        /// showing through it, and a plate over the window's own plate is the
        /// window's colour laid down twice -- which reads as a dark band under the
        /// table rather than part of it. What actually keeps the rows out is the
        /// clipping: a `RectMask2D` on the viewport, and <see cref="Curtain"/>
        /// measuring against the viewport rather than the window.
        /// </summary>
        private const float StripHeight = RuleGap + 2f + RowGap
                                        + RowHeight + RowGap
                                        + RowHeight + 4f + Margin;

        /// <summary>How many rows are shown before the table starts scrolling.
        /// Ten is about as many as the eye takes in at once, and a window taller
        /// than that under the mapper is a window in the way of the machine.</summary>
        private const int RowsShown = 10;

        /// <summary>The tallest the window is drawn: the heading, ten rows, and the
        /// strip along the bottom.</summary>
        private const float MaxHeight = Margin + HeadHeight + RowGap
                                      + RowsShown * (RowHeight + RowGap)
                                      + Margin + StripHeight;
        /// <summary>The shortest the window is drawn: the heading, one row and the
        /// strip. Anything less is empty space under a table of one.</summary>
        private const float MinHeight = Margin + HeadHeight + RowGap
                                      + RowHeight + Margin + StripHeight;

        /// <summary>Fixed column widths. The number and the three switches do not
        /// get wider on a wider mapper; the three that hold values do.</summary>
        private const float SwitchWidth = 24f;
        private const float NumberWidth = 22f;
        private const float ColGap = 3f;

        /// <summary>How big a switch column's picture is drawn. A little taller
        /// than the lettering beside it: a glyph is thin strokes where a letter is
        /// solid, and the two only read as one weight if the glyph is bigger.</summary>
        private const float GlyphSize = 18f;

        /// <summary>The sort mark's size, and how far its point sits in from the
        /// right-hand end of the heading.</summary>
        private const float MarkSize = 9f;
        private const float MarkInset = 4f;

        // Which column is which, left to right. The number is not a setting and
        // has no heading -- it is also the row's delete button, see RowNumber.
        private const int CNumber = 0;
        private const int CWait = 1;
        private const int CDuration = 2;
        private const int CHold = 3;
        private const int CStop = 4;
        private const int CLoop = 5;
        private const int CEmulate = 6;

        // And the logic table's, which shares only the number column.
        private const int LInputA = 1;
        private const int LInputB = 2;
        private const int LGate = 3;
        private const int LMode = 4;
        private const int LEmulate = 5;

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

        /// <summary>
        /// Whether the panel draws this block's table, which is what tells a block
        /// to take its controls off Besiege's own mapper.
        ///
        /// Timer Plus only, for now: the logic table's columns are a different
        /// table, and until the panel can draw one the Logic Gate Plus block keeps
        /// its controls in the mapper where they can at least be reached.
        /// </summary>
        public static bool Serving(MonoBehaviour block)
        {
            return Usable;
        }

        /// <summary>Set once the panel has given up -- no UI Factory, or a build
        /// that threw -- so the stock mapper is the only way to set a block and
        /// keeps its controls for good.</summary>
        public static bool Failed;

        private float width = DefaultWidth;
        private Canvas canvas;

        /// <summary>What makes the panel answer the pointer, kept so it can be
        /// switched off for the length of a drag that began somewhere else -- see
        /// <see cref="StandOff"/>.</summary>
        private GraphicRaycaster caster;
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

        /// <summary>The other block this panel draws a table for. Exactly one of
        /// the two is ever set: the chrome -- the window, the docking, the frame,
        /// the strip, the scrollbar -- is the same for both, and only the columns
        /// differ.</summary>
        private LogicGatePlusBehaviour logic;

        /// <summary>Whether the open block is the logic table. Not called
        /// `Logic`: Besiege has types of that name in the global namespace, and
        /// this file is compiled inside `TimerPlusMod`.</summary>
        private bool Gated { get { return logic != null; } }

        /// <summary>How many rows the open block has, whichever it is.</summary>
        private int Rows
        {
            get
            {
                if (Gated)
                {
                    return logic.Count;
                }
                return served == null ? 0 : served.Count;
            }
        }
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
            public RowNumber Number;
            public InputField Wait;
            public InputField Duration;

            /// <summary>What the two boxes are painted when nothing is selected.
            /// Read off the prefab as they are built: a colour written back from a
            /// constant is a guess, and the guess was white.</summary>
            public Color WaitPlain;
            public Color DurationPlain;
            public Toggle Hold;
            public Toggle Stop;
            public Toggle Loop;
            public KeyCell Emulate;

            // ---- the logic table's own cells ---------------------------------

            public KeyCell InA;
            public KeyCell InB;
            public GameObject Gate;
            public Text GateName;
            public Toggle ModeBox;

            /// <summary>The bars drawn over input B for a gate that does not read
            /// it, and over the switch for a gate that has neither of the two the
            /// switch stands for.</summary>
            public RawImage Barred;
            public RawImage ModeBarred;

            /// <summary>The letter on the switch: T for toggle mode, I for
            /// inverted.</summary>
            public Text ModeName;
        }

        private readonly List<Cells> table = new List<Cells>();
        /// <summary>The two buttons' own lettering, which is also where anything
        /// the panel has to say is said -- see <see cref="Flash"/>.</summary>
        private Text plusLabel;
        private Text convertLabel;

        /// <summary>Where the "+" and the convert button live: pinned to the bottom
        /// of the window, outside the scrolling view.</summary>
        private Transform fixedStrip;
        /// <summary>The sort mark on each sortable heading: one triangle, shown on
        /// the column the table is sorted by and turned over for a descending
        /// sort.</summary>
        private readonly RawImage[] marks = new RawImage[HeadNames.Length];

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
            LogicGatePlusBehaviour gates = null;
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper != null && mapper.Block != null)
                {
                    block = mapper.Block.GetComponent<TimerPlusBehaviour>();
                    gates = mapper.Block.GetComponent<LogicGatePlusBehaviour>();
                }
            }
            catch (Exception) { }

            if (block == null && gates == null)
            {
                Hide();
                return;
            }

            try
            {
                if (gates != null)
                {
                    Show(gates);
                }
                else
                {
                    Show(block);
                }
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

        /// <summary>The logic table's own open. The same shape as the timer's
        /// below it: a rebuild when the block or its size changed, then a fill.
        /// </summary>
        private void Show(LogicGatePlusBehaviour block)
        {
            // Which table it is, before the width is worked out: the logic table
            // has a minimum the timer's does not, and Widen reads it.
            bool other = logic != block || served != null;
            logic = block;
            served = null;

            Rect frame;
            if (MapperFrame(out frame))
            {
                Widen(frame);
            }

            if (other)
            {
                sortColumn = -1;
                sortAscending = true;
            }
            if (other || builtRows != block.Count
                || Mathf.Abs(builtWidth - width) > 0.5f || window == null)
            {
                if (!Build())
                {
                    Hide();
                    return;
                }
            }

            Usable = true;
            logic.ShowStock(false);
            window.SetActive(true);
            Unflash();
            Fill();
            Canvas.ForceUpdateCanvases();
            Dock();
            Curtain(true);
        }

        private void Show(TimerPlusBehaviour block)
        {
            bool other = served != block || logic != null;
            served = block;
            logic = null;

            Rect frame;
            if (MapperFrame(out frame))
            {
                Widen(frame);
            }

            if (other)
            {
                // The sort marker says which column *this* block was last sorted
                // by, and a different block was not sorted at all.
                sortColumn = -1;
                sortAscending = true;
            }
            if (other || builtRows != block.Count
                || Mathf.Abs(builtWidth - width) > 0.5f || window == null)
            {
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
            Unflash();
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
                Choices.Home(canvas.GetComponent<RectTransform>());

                Fixed();
                float height = Layout();
                builtRows = Rows;
                builtWidth = width;
                FitContent(height);
                windowRect.sizeDelta =
                    new Vector2(width, Mathf.Min(height + StripHeight, MaxHeight));
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

            caster = go.AddComponent<GraphicRaycaster>();
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
        /// <summary>
        /// Holds the scrolling view inside the window and above the strip, and
        /// clips whatever hangs out of it.
        ///
        /// The viewport is found rather than assumed. `ScrollRect.viewport` is
        /// allowed to be null -- uGUI then scrolls inside the ScrollRect's own rect
        /// -- and the prefab's need not be called "Viewport". Getting this wrong is
        /// invisible until something has to be kept out of a part of the window:
        /// with no viewport there is nothing to clip against, nothing to shorten,
        /// and <see cref="Curtain"/> falls back to the whole window, so rows scroll
        /// straight over the buttons along the bottom.
        /// </summary>
        /// <summary>
        /// The frame the rows scroll inside: ours, made here, with the prefab's
        /// content moved into it.
        ///
        /// Asking the ScrollRect for its viewport does not work. It is allowed to
        /// be null -- uGUI then scrolls inside the ScrollRect's own rect -- and the
        /// prefab's need not be named anything in particular; every attempt to find
        /// it and shorten it left the ScrollRect believing its frame was the whole
        /// window, which is how rows ended up over the strip and how the last rows
        /// could not be scrolled to at all.
        ///
        /// A frame of our own is the end of that. It is the window less the strip
        /// and less the bar's gutter, so how far the list scrolls is exactly how
        /// much of it does not fit, and the bar hides itself when all of it does.
        /// </summary>
        private void Clip()
        {
            GameObject go = new GameObject("Frame");
            go.transform.SetParent(window.transform, false);
            RectTransform view = go.AddComponent<RectTransform>();
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.pivot = new Vector2(0f, 1f);
            view.offsetMin = new Vector2(0f, StripHeight);
            view.offsetMax = new Vector2(-BarGutter, 0f);
            // Clips by rectangle. A stencil Mask would do it too and would be one
            // more thing sharing a stencil buffer with the window's own.
            go.AddComponent<RectMask2D>();

            // The wheel. It reaches the hovered object and then its parents, and
            // the ScrollRect is not among them -- this frame is the panel's own and
            // hangs off the window. A plate to be hovered at all, invisible, and
            // behind everything in the frame because children are raycast first.
            Image sheet = go.AddComponent<Image>();
            sheet.color = new Color(0f, 0f, 0f, 0f);
            Wheel wheel = go.AddComponent<Wheel>();
            wheel.target = scroll;

            scroll.viewport = view;
            RectTransform rows = scroll.content;
            rows.SetParent(view, false);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.anchoredPosition = Vector2.zero;
            rows.sizeDelta = new Vector2(0f, rows.sizeDelta.y);
        }

        /// <summary>
        /// The bar down the right of the table.
        ///
        /// Built rather than borrowed. The Window prefab brings one, but it is
        /// anchored to the whole window and the strip along the bottom is not part
        /// of the list -- and a prefab's rect, reshaped from outside, is a thing
        /// that keeps coming back. This one is ours, it stops where the list stops,
        /// and it is the only bar the ScrollRect knows about.
        /// </summary>
        private void Rail()
        {
            if (scroll.verticalScrollbar != null)
            {
                scroll.verticalScrollbar.gameObject.SetActive(false);
                scroll.verticalScrollbar = null;
            }

            GameObject track = new GameObject("Rail");
            track.transform.SetParent(window.transform, false);
            RectTransform rail = track.AddComponent<RectTransform>();
            rail.anchorMin = new Vector2(1f, 0f);
            rail.anchorMax = new Vector2(1f, 1f);
            rail.pivot = new Vector2(1f, 1f);
            // Down the right, from the top of the window to the top of the strip.
            rail.offsetMin = new Vector2(-(BarWidth + BarInset), StripHeight);
            rail.offsetMax = new Vector2(-BarInset, 0f);
            Image back = track.AddComponent<Image>();
            back.color = RailInk;

            GameObject area = new GameObject("Sliding Area");
            area.transform.SetParent(track.transform, false);
            RectTransform slide = area.AddComponent<RectTransform>();
            slide.anchorMin = Vector2.zero;
            slide.anchorMax = Vector2.one;
            slide.offsetMin = Vector2.zero;
            slide.offsetMax = Vector2.zero;

            GameObject held = new GameObject("Handle");
            held.transform.SetParent(area.transform, false);
            RectTransform grip = held.AddComponent<RectTransform>();
            grip.offsetMin = Vector2.zero;
            grip.offsetMax = Vector2.zero;
            Image face = held.AddComponent<Image>();
            face.color = GripInk;

            // Fully qualified: Besiege has a `Scrollbar` of its own in the global
            // namespace, and this file is compiled inside `TimerPlusMod`.
            UnityEngine.UI.Scrollbar bar =
                track.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.handleRect = grip;
            bar.targetGraphic = face;

            scroll.verticalScrollbar = bar;
            // AutoHide and not AutoHideAndExpandViewport: the second resizes the
            // viewport, and the viewport here is whatever the prefab decided.
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void Teardown()
        {
            // Whatever a cell was offering, and whatever was selected, belongs to
            // rows that are going away.
            Choices.Close();
            pickFrom = -1;
            pickTo = -1;
            reaching = false;
            pickBox = null;
            pickFocus = 0;
            table.Clear();
            plusLabel = null;
            convertLabel = null;
            pinBox = null;
            flashing = null;
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i] = null;
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
            fixedStrip = null;
            scroll = null;
            content = null;
            builtRows = -1;
            builtWidth = -1f;
        }

        // ---- geometry --------------------------------------------------------

        private float Full { get { return width - Margin * 2f - BarGutter; } }

        /// <summary>
        /// Where the table starts and how wide it is.
        ///
        /// The logic table is drawn tighter than the timer's: it has six columns
        /// where the timer has seven narrower ones, and every unit of margin is a
        /// unit a variable name does not get. The left inset is a hair rather than
        /// a margin, and the right one is the scrollbar and no more -- the bar is
        /// eight wide, inset five, and three clear of the last column.
        /// </summary>
        private float Edge { get { return Gated ? 4f : Margin; } }

        private float Wide
        {
            get
            {
                return Gated ? width - Edge - (BarWidth + BarInset + 3f) : Full;
            }
        }

        /// <summary>
        /// Where each column starts and how wide it is. The number and the three
        /// switches are fixed; what is left is shared between the two times and the
        /// key -- a variable name needs the room and a time needs four characters
        /// and no more.
        /// </summary>
        private void Columns(out float[] x, out float[] w)
        {
            if (Gated)
            {
                // The number, one switch, and four columns that hold a name: two
                // inputs, the gate and the key. The gate takes a little less than
                // the keys -- "SR LATCH" is the longest thing it ever says, and a
                // variable name has no length this can choose.
                // Tighter than the timer's number and switch columns: a number of
                // two digits and one tick box need less than they were given, and
                // every unit taken off them is a unit a variable name keeps.
                float number = NumberWidth - 2f;
                float mode = SwitchWidth - 2f;
                float room = Mathf.Max(200f,
                    Wide - (number + mode + ColGap * 5f));
                float key = room * 0.275f;
                w = new float[] { number, key, key, room - key * 3f, mode, key };
                x = new float[w.Length];
                float at2 = Edge;
                for (int i = 0; i < w.Length; i++)
                {
                    x[i] = at2;
                    at2 += w[i] + ColGap;
                }
                return;
            }

            float fixedWidth = NumberWidth + SwitchWidth * 3f + ColGap * 6f;
            float rest = Mathf.Max(160f, Full - fixedWidth);
            // The two times take more than the key does. A heading grows under the
            // pointer, and "DURATION" grown by a tenth has to stay inside its own
            // column. What is left goes to the key, which needs enough of it for a
            // variable name -- a name a character longer than the column is a name
            // shrunk until it is not read at a glance.
            float num = rest * 0.31f;
            float emu = rest - num * 2f;

            w = new float[] { NumberWidth, num, num,
                              SwitchWidth, SwitchWidth, SwitchWidth, emu };
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

            y = Gated ? LogicHeader(y, x, w) : Header(y, x, w);

            table.Clear();
            int count = Rows;
            for (int i = 0; i < count; i++)
            {
                table.Add(Gated ? LogicRowCells(i, ref y, x, w)
                                : BuildRow(i, ref y, x, w));
            }

            return y + Margin;
        }

        /// <summary>
        /// What each column is headed with. The number column is headed with
        /// nothing: a column of small numbers down the left of a table of times
        /// has already said what it is, and a heading on it would be one more
        /// thing to read.
        ///
        /// The three switch columns are headed with a picture instead -- see
        /// <see cref="Glyphs"/>. The name is still here because it is what the
        /// heading falls back to when the pictures could not be loaded.
        /// </summary>
        private static readonly string[] HeadNames =
        {
            "", "WAIT", "DURATION", "H", "S", "L", "EMULATE"
        };

        /// <summary>
        /// What each heading says on hover. This is where the three switch columns
        /// give their names, a picture in a twenty-six unit column having no room
        /// for a word beside it.
        /// </summary>
        private static readonly string[] HeadTips =
        {
            "",
            "Seconds from the start to the press",
            "How long the press is held",
            "Hold to run",
            "Allow stop",
            "Loop",
            "The key or variable this row presses"
        };


        /// <summary>
        /// The column headings.
        ///
        /// Only the two times sort. A column of tick boxes is already an answer to
        /// "which of these loop" -- three ticks in a column of thirty-two rows are
        /// read off faster than a sort is clicked -- and a table in keycode-name
        /// order answers a question nobody asks. So the other headings are labels,
        /// with the button's own click feedback taken off them so they do not offer
        /// something that does not happen.
        /// </summary>
        private float Header(float y, float[] x, float[] w)
        {
            // From one: the number column is headed with nothing at all.
            for (int c = CWait; c < HeadNames.Length; c++)
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

                bool sorts = c == CWait || c == CDuration;
                Texture picture = Picture(c);
                if (picture != null)
                {
                    Draw(go, picture);
                }
                else
                {
                    Text label = Pin(go, HeadNames[c], UIF.Ink);
                    if (sorts)
                    {
                        Grow(go, label);
                        marks[c] = Mark(go);
                    }
                }

                Tip.On(go, HeadTips[c]);

                Button click = go.GetComponent<Button>();
                if (click == null)
                {
                    continue;
                }
                if (sorts)
                {
                    int column = c == CWait ? Table.ColWait : Table.ColDuration;
                    click.onClick.AddListener(delegate { SortBy(column); });
                }
                else
                {
                    // Left in place rather than destroyed, so the heading keeps the
                    // plate the others are drawn on; it just no longer lights up
                    // under the pointer.
                    click.enabled = false;
                }
            }
            y += HeadHeight + RowGap;
            return y;
        }

        /// <summary>
        /// The sort mark on a sortable heading: a triangle at the right-hand end,
        /// hidden until the table is sorted by that column.
        ///
        /// Its own object rather than a character on the end of the heading, so
        /// ascending and descending are one picture two ways up rather than two
        /// letters of different sizes -- and so the heading's own words do not
        /// shift sideways when the mark appears.
        /// </summary>
        private static RawImage Mark(GameObject heading)
        {
            Texture picture = Glyphs.Arrow;
            if (picture == null)
            {
                return null;
            }
            GameObject go = new GameObject("Sort");
            go.transform.SetParent(heading.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            // Pivoted in its middle, not on its edge: the turn for a descending
            // sort is about the pivot, and a mark pivoted on its edge swings out
            // to one side instead of turning over where it stands.
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-(MarkInset + MarkSize * 0.5f), 0f);
            rect.sizeDelta = new Vector2(MarkSize, MarkSize);

            RawImage drawn = go.AddComponent<RawImage>();
            drawn.texture = picture;
            drawn.color = UIF.Ink;
            drawn.raycastTarget = false;
            go.SetActive(false);
            return drawn;
        }

        /// <summary>The picture a heading is drawn with, or null for one drawn with
        /// its name.</summary>
        private static Texture Picture(int column)
        {
            if (column == CHold) return Glyphs.Hold;
            if (column == CStop) return Glyphs.Stop;
            if (column == CLoop) return Glyphs.Loop;
            return null;
        }

        /// <summary>
        /// Puts a picture in a heading, in place of its lettering.
        ///
        /// A <c>RawImage</c> rather than an <c>Image</c>: the resource system hands
        /// over a <c>Texture</c>, and a Sprite would have to be made from it and
        /// owned by somebody. Square and inset, so three of them read as one size
        /// whatever shape the artwork is, and tinted like the lettering it stands
        /// in for -- the artwork is white, so the tint is the colour.
        /// </summary>
        private static void Draw(GameObject control, Texture picture)
        {
            Text label = control.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "";
            }
            GameObject go = new GameObject("Glyph");
            go.transform.SetParent(control.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(GlyphSize, GlyphSize);

            RawImage drawn = go.AddComponent<RawImage>();
            drawn.texture = picture;
            // Ink rather than the QuietInk the lettering wears, for the same
            // reason it is drawn bigger.
            drawn.color = UIF.Ink;
            drawn.raycastTarget = false;
        }

        /// <summary>What the logic table's columns are headed with, and what each
        /// says on hover. The number column is headed with nothing, as the timer's
        /// is.</summary>
        private static readonly string[] GateHeads =
        {
            "", "INPUT A", "INPUT B", "GATE", "M", "OUTPUT"
        };

        private static readonly string[] GateHeadTips =
        {
            "",
            "",
            "",
            "",
            "Edge detector: invert\nOther gates: toggle",
            ""
        };

        private float LogicHeader(float y, float[] x, float[] w)
        {
            for (int c = LInputA; c < GateHeads.Length; c++)
            {
                GameObject go = UIF.Spawn(UIF.ButtonPrefab, content);
                if (go == null)
                {
                    continue;
                }
                UIF.Fit(go.GetComponent<RectTransform>(), x[c], y, w[c], HeadHeight);
                UIF.NoSwell(go);

                bool sorts = c == LGate;
                Text label = Pin(go, GateHeads[c], UIF.Ink);
                if (sorts)
                {
                    Grow(go, label);
                    marks[c] = Mark(go);
                }
                Tip.On(go, GateHeadTips[c]);

                Button click = go.GetComponent<Button>();
                if (click == null)
                {
                    continue;
                }
                if (sorts)
                {
                    click.onClick.AddListener(delegate { SortBy(LogicTable.ColGate); });
                }
                else
                {
                    click.enabled = false;
                }
            }
            y += HeadHeight + RowGap;
            return y;
        }

        /// <summary>
        /// One row of the logic table: two inputs, the gate, its one switch and the
        /// key it presses.
        /// </summary>
        private Cells LogicRowCells(int index, ref float y, float[] x, float[] w)
        {
            Cells cells = new Cells();

            GameObject frame = new GameObject("Row" + index);
            frame.transform.SetParent(content, false);
            frame.AddComponent<RectTransform>();
            UIF.Fit(frame.GetComponent<RectTransform>(), 0f, y, width, RowHeight);
            cells.Frame = frame;
            Transform host = frame.transform;

            cells.Number = RowNumber.Make(host, x[CNumber], 0f, w[CNumber], RowHeight);
            int which = index;
            cells.Number.Clicked = delegate { Delete(which); };
            cells.Number.Watch(frame);

            cells.InA = KeyCell.Make(host, x[LInputA], 0f, w[LInputA], RowHeight,
                                     GateBubble);
            cells.InA.Row = index;
            cells.InA.Changed = GateKeyChanged;

            cells.InB = KeyCell.Make(host, x[LInputB], 0f, w[LInputB], RowHeight,
                                     GateBubble);
            cells.InB.Row = index;
            cells.InB.Changed = GateKeyChanged;
            // Drawn over input B for a gate that reads only one, and switched off
            // for one that reads both. Built here rather than when it is first
            // wanted: a row's gate changes under the pointer.
            cells.Barred = Bars(cells.InB.gameObject);

            cells.Gate = UIF.Spawn(UIF.ButtonPrefab, host);
            if (cells.Gate != null)
            {
                UIF.Fit(cells.Gate.GetComponent<RectTransform>(), x[LGate], 0f,
                        w[LGate], RowHeight);
                UIF.NoSwell(cells.Gate);
                cells.GateName = Pin(cells.Gate, "", UIF.Ink);
                Grow(cells.Gate, cells.GateName);
                int at = index;
                Button click = cells.Gate.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(delegate { PickGate(at); });
                }
            }

            cells.ModeBox = Box(host, x[LMode], w[LMode], index);
            if (cells.ModeBox != null)
            {
                cells.ModeName = cells.ModeBox.GetComponentInChildren<Text>(true);
                cells.ModeBarred = Bars(cells.ModeBox.gameObject);
            }

            cells.Emulate = KeyCell.Make(host, x[LEmulate], 0f, w[LEmulate], RowHeight,
                                         GateBubble);
            cells.Emulate.Row = index;
            cells.Emulate.Changed = GateKeyChanged;

            y += RowHeight + RowGap;
            return cells;
        }

        /// <summary>
        /// The diagonal bars that say a cell is not read.
        ///
        /// Drawn over the cell rather than by hiding it: the key is still there and
        /// still saved, and a gate switched back to one that reads B should find
        /// what it was given. An empty space would say the setting was lost.
        /// </summary>
        private static RawImage Bars(GameObject over)
        {
            if (over == null)
            {
                return null;
            }
            GameObject go = new GameObject("Barred");
            go.transform.SetParent(over.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage drawn = go.AddComponent<RawImage>();
            drawn.texture = Glyphs.Bars;
            drawn.color = new Color(1f, 1f, 1f, 0.5f);
            // Tiled: the picture is one diagonal stripe, and a cell is however wide
            // the mapper is. uvRect counts in texture widths.
            drawn.uvRect = new Rect(0f, 0f, 6f, 1f);
            // The cell underneath still takes the pointer, so a barred key can be
            // read and even rebound; it is drawn over, not switched off.
            drawn.raycastTarget = false;
            go.SetActive(false);
            return drawn;
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
            cells.Number = RowNumber.Make(host, x[CNumber], 0f, w[CNumber], RowHeight);
            int which = index;
            cells.Number.Clicked = delegate { Delete(which); };
            cells.Number.Watch(frame);


            cells.Wait = Number(host, x[CWait], w[CWait], index, true);
            cells.Duration = Number(host, x[CDuration], w[CDuration], index, false);
            cells.WaitPlain = Plain(cells.Wait);
            cells.DurationPlain = Plain(cells.Duration);
            cells.Hold = Box(host, x[CHold], w[CHold], index);
            cells.Stop = Box(host, x[CStop], w[CStop], index);
            cells.Loop = Box(host, x[CLoop], w[CLoop], index);

            cells.Emulate = KeyCell.Make(host, x[CEmulate], 0f, w[CEmulate], RowHeight);
            cells.Emulate.Row = index;
            cells.Emulate.Changed = RowKeyChanged;

            y += RowHeight + RowGap;
            return cells;
        }

        /// <summary>
        /// The strip along the bottom of the window, outside the scrolling view:
        /// the rule that closes the table off, the "+" that adds a row, the convert
        /// button and the message line.
        ///
        /// Parented to the window rather than to the scrolling content, and the
        /// viewport is held clear of it in <see cref="Clip"/>, so the two never
        /// overlap however many rows there are.
        /// </summary>
        private void Fixed()
        {
            GameObject go = new GameObject("Fixed");
            go.transform.SetParent(window.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, StripHeight);
            fixedStrip = go.transform;

            float y = 0f;
            y = Rule(y);
            y = Plus(y);
            Footer(y);
        }

        /// <summary>The button that adds a row, across the strip.</summary>
        private float Plus(float y)
        {
            GameObject go = UIF.Spawn(UIF.ButtonPrefab, fixedStrip);
            if (go != null)
            {
                UIF.Fit(go.GetComponent<RectTransform>(), Edge, y, Wide, RowHeight);
                UIF.NoSwell(go);
                plusLabel = Pin(go, "+", UIF.Ink);
                Grow(go, plusLabel);
                Button click = go.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(AddRow);
                }
            }
            return y + RowHeight + RowGap;
        }

        /// <summary>How much of the bottom row the pin switch takes, the convert
        /// button having the rest.</summary>
        private const float PinShare = 0.25f;

        private float Footer(float y)
        {
            float tall = RowHeight + 4f;
            float pinWide = Wide * PinShare;

            // The switch first, on the left, because it is a thing the convert
            // button does rather than a thing of its own.
            GameObject pin = UIF.Spawn(UIF.TogglePrefab, fixedStrip);
            if (pin != null)
            {
                UIF.Fit(pin.GetComponent<RectTransform>(), Edge, y,
                        pinWide - ColGap, tall);
                UIF.NoSwell(pin);
                Text label = Pin(pin, "PIN BLOCKS", UIF.Ink);
                Grow(pin, label);
                pinBox = pin.GetComponent<Toggle>();
                if (pinBox != null)
                {
                    pinBox.onValueChanged.AddListener(Pinning);
                }
            }

            GameObject go = UIF.Spawn(UIF.ButtonPrefab, fixedStrip);
            if (go != null)
            {
                UIF.Fit(go.GetComponent<RectTransform>(), Edge + pinWide, y,
                        Wide - pinWide, tall);
                UIF.NoSwell(go);
                convertLabel = Pin(go, Gated ? "CONVERT TO LOGIC GATES"
                                             : "CONVERT TO TIMER BLOCKS", UIF.Ink);
                Grow(go, convertLabel);
                Button click = go.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(DoConvert);
                }
            }
            return y + tall;
        }

        /// <summary>The pin switch, and what it is set to now.</summary>
        private Toggle pinBox;

        private void Pinning(bool on)
        {
            if (filling)
            {
                return;
            }
            MToggle control = Gated
                ? (logic == null ? null : logic.PinControl)
                : (served == null ? null : served.PinControl);
            if (control == null)
            {
                return;
            }
            control.IsActive = on;
            Queue(control);
        }

        // ---- the small pieces ------------------------------------------------

        private Text Label(Transform host, string text, float x, float y,
                           float w, float h, Color colour, TextAnchor align)
        {
            GameObject go = UIF.Spawn(UIF.TextPrefab, host);
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

        private Toggle Box(Transform host, float x, float w, int index)
        {
            GameObject go = UIF.Spawn(UIF.TogglePrefab, host);
            if (go == null)
            {
                return null;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, 0f, w, RowHeight);
            UIF.NoSwell(go);
            // No caption: the column heading is a picture for want of room, and
            // the heading's own tooltip is where the switch says what it is.
            Pin(go, "", UIF.Ink);
            Toggle toggle = go.GetComponent<Toggle>();
            if (toggle != null)
            {
                int at = index;
                float where = x;
                toggle.onValueChanged.AddListener(
                    delegate(bool on) { Flipped(at, where, on); });
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
                UIF.Style(field.placeholder as Text, UIF.Ink, TextAnchor.MiddleRight);
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

                // A sheet over the box, so the number can be dragged as well as
                // typed -- the sibling SpecialEffects mod's value fields, and the
                // whole of why it is a sheet is in ValueField.
                GameObject drag = new GameObject("Drag");
                drag.transform.SetParent(field.transform, false);
                RectTransform sheet = drag.AddComponent<RectTransform>();
                sheet.anchorMin = Vector2.zero;
                sheet.anchorMax = Vector2.one;
                sheet.offsetMin = Vector2.zero;
                sheet.offsetMax = Vector2.zero;
                Image catcher = drag.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);

                ValueField value = drag.AddComponent<ValueField>();
                value.field = field;
                value.dragged = delegate(float pixels) { Scrub(at, which, pixels); };
                value.picking = delegate(Vector2 screen) { Pick(at, which, screen); };
                value.picked = delegate { Picked(); };
            }
            return field;
        }

        // ---- reaching down a column ------------------------------------------

        /// <summary>Which column a standing selection is in, and the first and last
        /// row of it. <c>from</c> is -1 when there is none.</summary>
        private bool pickWait;
        private int pickFrom = -1;
        private int pickTo = -1;

        /// <summary>True while the pointer is still down and reaching; the panel
        /// scrolls itself under it while that is so.</summary>
        private bool reaching;
        private Vector2 reachAt;

        /// <summary>The box the reach started from, which is the one that takes the
        /// keyboard when it ends: a selection is made to be typed into, and asking
        /// for a click on a box that is already lit would be asking twice.</summary>
        private InputField pickBox;

        /// <summary>Frames left to insist on that. A field settles its own caret in
        /// its LateUpdate, and which of the two runs first is not ours to
        /// decide.</summary>
        private int pickFocus;

        /// <summary>How near the top or bottom of the frame the pointer has to be
        /// for the list to start moving under it, and how fast it then moves.</summary>
        private const float EdgeBand = 26f;
        private const float EdgeSpeed = 220f;

        /// <summary>The colour a box is painted when nothing is selected: whatever
        /// the prefab gave it.</summary>
        private static Color Plain(InputField box)
        {
            Graphic plate = box == null ? null : box.targetGraphic;
            return plate == null ? Color.white : plate.color;
        }

        /// <summary>
        /// What a selected box is tinted: the colour the field itself highlights
        /// selected text with, which is the grey somebody already knows means
        /// "this, and what you type next replaces it".
        ///
        /// Its own alpha is for text drawn over a box; the box keeps the alpha it
        /// was drawn with.
        /// </summary>
        private static Color Chosen(InputField box, Color plain)
        {
            if (box == null)
            {
                return plain;
            }
            Color grey = box.selectionColor;
            return new Color(grey.r, grey.g, grey.b, plain.a);
        }

        /// <summary>
        /// The pointer has reached out of a box, up or down its own column. Selects
        /// every row between the one the drag started on and the one it is over.
        /// </summary>
        private void Pick(int index, bool wait, Vector2 screen)
        {
            if (served == null || table.Count == 0)
            {
                return;
            }
            reachAt = screen;
            if (!reaching)
            {
                // A new reach, not the one that ended a moment ago: whatever was
                // selected is not what this gesture is about.
                reaching = true;
                pickWait = wait;
                pickFrom = index;
                pickTo = index;
                pickBox = wait ? table[index].Wait : table[index].Duration;
            }
            int over = Under(screen);
            pickTo = over < 0 ? pickTo : over;
            if (pickTo < 0)
            {
                pickTo = index;
            }
            Highlight();
        }

        /// <summary>The reach ended. The selection stands: the next value typed or
        /// dragged into any of it goes to all of it.</summary>
        private void Picked()
        {
            reaching = false;
            if (pickFrom >= 0 && pickTo == pickFrom)
            {
                // A reach that never left its own row selected one row, which is
                // what a plain edit already is.
                Unpick();
                return;
            }
            if (pickBox != null)
            {
                pickBox.ActivateInputField();
                pickFocus = Insisting;
            }
        }

        /// <summary>Frames the selected box is given the keyboard for.</summary>
        private const int Insisting = 3;

        /// <summary>Holds the whole of the box's value selected for a few frames
        /// after a reach, so the first thing typed replaces it rather than landing
        /// beside it.</summary>
        private void Keyboard()
        {
            if (pickFocus <= 0 || pickBox == null)
            {
                return;
            }
            pickFocus--;
            if (!pickBox.isFocused)
            {
                return;
            }
            pickBox.selectionAnchorPosition = 0;
            pickBox.selectionFocusPosition = pickBox.text.Length;
            // Those two setters move the caret and the anchor and nothing else --
            // they do not mark the caret graphic dirty, so the highlight that shows
            // a selection is never rebuilt without this.
            pickBox.ForceLabelUpdate();
        }

        /// <summary>Drops the selection and puts the boxes back to their own
        /// colour.</summary>
        private void Unpick()
        {
            pickFrom = -1;
            pickTo = -1;
            reaching = false;
            pickBox = null;
            pickFocus = 0;
            Highlight();
        }

        /// <summary>Whether a cell is one of the selected ones.</summary>
        private bool InPick(int index, bool wait)
        {
            return pickFrom >= 0 && wait == pickWait
                && index >= Mathf.Min(pickFrom, pickTo)
                && index <= Mathf.Max(pickFrom, pickTo);
        }

        /// <summary>Tints the boxes that are selected and clears the ones that are
        /// not. Every box every time: a row that has just left the selection has to
        /// be put back, and there are thirty-two of them at the most.</summary>
        private void Highlight()
        {
            for (int i = 0; i < table.Count; i++)
            {
                Cells row = table[i];
                Tint(row.Wait, InPick(i, true) ? Chosen(row.Wait, row.WaitPlain)
                                               : row.WaitPlain);
                Tint(row.Duration,
                     InPick(i, false) ? Chosen(row.Duration, row.DurationPlain)
                                      : row.DurationPlain);
            }
        }

        private static void Tint(InputField box, Color colour)
        {
            Graphic plate = box == null ? null : box.targetGraphic;
            if (plate != null)
            {
                plate.color = colour;
            }
        }

        /// <summary>
        /// Which row the pointer is over, or -1 if it is over none of them.
        ///
        /// Through the window's own rect, the same way <see cref="Curtain"/>
        /// measures: the rows are several parents deep inside a frame that scrolls,
        /// and their anchored positions say nothing about where they are on screen.
        /// </summary>
        private int Under(Vector2 screen)
        {
            if (windowRect == null)
            {
                return -1;
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    windowRect, screen, null, out local))
            {
                return -1;
            }
            int nearest = -1;
            float best = 0f;
            Vector3[] corners = new Vector3[4];
            for (int i = 0; i < table.Count; i++)
            {
                RectTransform row = table[i].Frame == null
                    ? null : table[i].Frame.transform as RectTransform;
                if (row == null)
                {
                    continue;
                }
                row.GetWorldCorners(corners);
                float bottom = windowRect.InverseTransformPoint(corners[0]).y;
                float top = windowRect.InverseTransformPoint(corners[1]).y;
                if (local.y <= top && local.y >= bottom)
                {
                    return i;
                }
                // Past the end of the list in either direction: the nearest row is
                // what the pointer means, which is what lets a reach run off the
                // top or the bottom and take everything with it.
                float away = local.y > top ? local.y - top : bottom - local.y;
                if (nearest < 0 || away < best)
                {
                    nearest = i;
                    best = away;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Scrolls the list while a reach is held near the top or the bottom of the
        /// frame, so a selection can be longer than the window.
        ///
        /// Driven from LateUpdate rather than from the drag: a pointer held still at
        /// the edge sends no drag events, and holding still at the edge is exactly
        /// how somebody asks for this.
        /// </summary>
        private void Reach()
        {
            if (!reaching || scroll == null || scroll.content == null
                || scroll.viewport == null)
            {
                return;
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    scroll.viewport, reachAt, null, out local))
            {
                return;
            }
            Rect frame = scroll.viewport.rect;
            float step = 0f;
            if (local.y > frame.yMax - EdgeBand)
            {
                step = -EdgeSpeed * Time.unscaledDeltaTime;
            }
            else if (local.y < frame.yMin + EdgeBand)
            {
                step = EdgeSpeed * Time.unscaledDeltaTime;
            }
            if (step == 0f)
            {
                return;
            }
            float span = Mathf.Max(0f, scroll.content.sizeDelta.y - frame.height);
            Vector2 at = scroll.content.anchoredPosition;
            scroll.content.anchoredPosition =
                new Vector2(at.x, Mathf.Clamp(at.y + step, 0f, span));
            Curtain(false);
            int over = Under(reachAt);
            if (over >= 0)
            {
                pickTo = over;
                Highlight();
            }
        }

        /// <summary>How much of a second a pixel of drag is worth. A slider's whole
        /// range per two hundred and fifty pixels, which is the rate the sibling
        /// SpecialEffects mod's value fields use.</summary>
        private const float DragPerPixel = 0.004f;

        /// <summary>
        /// A number dragged rather than typed.
        ///
        /// Written straight to the control and shown at once, and queued for the
        /// commit that happens when the mouse comes up -- a drag writes live every
        /// frame and reserialises the block once, the same as a slider being
        /// dragged in Besiege's own mapper.
        /// </summary>
        private void Scrub(int index, bool wait, float pixels)
        {
            if (served == null || index >= served.Rows.Count)
            {
                return;
            }
            Row row = served.Rows[index];
            if (!row.Ready)
            {
                return;
            }
            MSlider slider = wait ? row.Wait : row.Duration;
            float step = pixels * (slider.Max - slider.Min) * DragPerPixel;

            // A drag over a selection moves every row of it by the same amount
            // rather than to the same value: the rows were given their spacing on
            // purpose, and a drag is an adjustment where a typed number is a
            // decision.
            bool many = InPick(index, wait);
            int first = many ? Mathf.Min(pickFrom, pickTo) : index;
            int last = many ? Mathf.Max(pickFrom, pickTo) : index;
            if (!many)
            {
                // Touching a cell outside the selection is leaving it.
                Unpick();
            }
            for (int i = first; i <= last && i < served.Rows.Count; i++)
            {
                Row one = served.Rows[i];
                if (!one.Ready)
                {
                    continue;
                }
                MSlider each = wait ? one.Wait : one.Duration;
                float value = each.Value + step;
                // Never below the slider's own floor: a wait or a duration below
                // zero is not a thing, whatever the slider will take above its top.
                each.Value = value < each.Min ? each.Min : value;
                Queue(each);
                InputField shown = wait ? table[i].Wait : table[i].Duration;
                if (shown != null)
                {
                    // Written even while the box has focus, which is the one place
                    // that is right: the value is moving under the pointer and the
                    // box is what shows it.
                    shown.text = Spell(each.Value);
                }
            }
            // The numbers down the left are the wait order, and a wait being
            // dragged is that order changing under the hand.
            Numbers();
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
            Plate(fixedStrip, Edge, y + RuleGap * 0.5f, Wide, 2f, RuleInk);
            return y + RuleGap + 2f + RowGap;
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
            if (Gated)
            {
                GateFill();
                return;
            }
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
                    cells.Emulate.Show(row.Emulate);
                    Set(cells.Wait, row.Wait.Value);
                    Set(cells.Duration, row.Duration.Value);
                    if (cells.Hold != null) cells.Hold.isOn = row.Hold.IsActive;
                    if (cells.Stop != null) cells.Stop.isOn = row.Stop.IsActive;
                    if (cells.Loop != null) cells.Loop.isOn = row.Loop.IsActive;
                }
                Numbers();
                Heads();
                ShowPin();
            }
            finally
            {
                filling = false;
            }
        }

        /// <summary>
        /// The logic table's fill. Every cell every time, for the reason the
        /// timer's does it: a window kept for the next block of the same shape
        /// would otherwise show the last one's values.
        /// </summary>
        private void GateFill()
        {
            filling = true;
            try
            {
                for (int i = 0; i < table.Count && i < logic.Rows.Count; i++)
                {
                    LogicRow row = logic.Rows[i];
                    Cells cells = table[i];
                    if (!row.Ready)
                    {
                        continue;
                    }
                    int gate = row.Gate;
                    cells.InA.Show(row.InputA);
                    cells.InB.Show(row.InputB);
                    cells.Emulate.Show(row.Emulate);
                    if (cells.GateName != null)
                    {
                        cells.GateName.text = Gates.Names[gate];
                    }
                    if (cells.ModeBox != null)
                    {
                        cells.ModeBox.isOn = row.Switch;
                    }
                    if (cells.Barred != null)
                    {
                        cells.Barred.gameObject.SetActive(!Gates.UsesB(gate));
                    }
                    if (cells.ModeName != null)
                    {
                        cells.ModeName.text = Gates.ModeLetter(gate);
                    }
                    if (cells.ModeBarred != null)
                    {
                        // Barred *and* switched off, not merely barred: a switch
                        // this gate never reads is one a click should not move.
                        bool has = Gates.UsesMode(gate);
                        cells.ModeBarred.gameObject.SetActive(!has);
                        cells.ModeBox.interactable = has;
                    }
                    if (cells.Number != null)
                    {
                        // Row order, not a rank: a gate has no order of its own,
                        // and the number is here to be pointed at.
                        cells.Number.Number = (i + 1).ToString();
                    }
                }
                Heads();
                ShowPin();
            }
            finally
            {
                filling = false;
            }
        }

        /// <summary>Puts the block's pin setting on the switch that shows it.
        /// Inside a fill, so writing it back does not answer for the player.
        /// </summary>
        private void ShowPin()
        {
            if (pinBox == null)
            {
                return;
            }
            MToggle control = Gated
                ? (logic == null ? null : logic.PinControl)
                : (served == null ? null : served.PinControl);
            if (control != null)
            {
                pinBox.isOn = control.IsActive;
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
                RowNumber cell = table[order[rank]].Number;
                if (cell != null)
                {
                    cell.Number = (rank + 1).ToString();
                }
            }
        }

        private float Wait(int row)
        {
            Row one = served.Rows[row];
            return one.Ready ? one.Wait.Value : 0f;
        }

        /// <summary>Marks the heading of the column the table was last sorted
        /// by. Only the two that sort have a heading to mark.</summary>
        private void Heads()
        {
            if (Gated)
            {
                Mark(LGate, LogicTable.ColGate);
                return;
            }
            Mark(CWait, Table.ColWait);
            Mark(CDuration, Table.ColDuration);
        }

        private void Mark(int head, int column)
        {
            bool on = column == sortColumn;
            RawImage arrow = marks[head];
            if (arrow != null)
            {
                arrow.gameObject.SetActive(on);
                // Up for ascending, the same triangle turned over for descending.
                arrow.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, sortAscending ? 0f : 180f);
            }
        }

        // ---- what the controls do --------------------------------------------

        // ---- what the logic table's controls do -------------------------------

        /// <summary>One of a logic row's three keys changed. Which one it is comes
        /// from the cell the panel built it into, so a row does not have to carry a
        /// column number about with it.</summary>
        private void GateKeyChanged(KeyCell cell)
        {
            if (filling || logic == null || cell.Row < 0
                || cell.Row >= logic.Rows.Count || cell.Row >= table.Count)
            {
                return;
            }
            LogicRow row = logic.Rows[cell.Row];
            if (!row.Ready)
            {
                return;
            }
            Cells shown = table[cell.Row];
            MKey key = cell == shown.InA ? row.InputA
                     : (cell == shown.InB ? row.InputB : row.Emulate);
            Apply(key, cell);
            Queue(key);
        }

        /// <summary>Offers the twelve gates, in the game's own order.</summary>
        private void PickGate(int index)
        {
            if (logic == null || index >= logic.Rows.Count || index >= table.Count)
            {
                return;
            }
            List<string> names = new List<string>();
            for (int g = 0; g < Gates.Count; g++)
            {
                names.Add(Gates.Names[g]);
            }
            RectTransform under = table[index].Gate == null
                ? null : table[index].Gate.transform as RectTransform;
            int at = index;
            Choices.Open(under, names, delegate(string picked)
            {
                Chose(at, names.IndexOf(picked));
            });
        }

        private void Chose(int index, int gate)
        {
            if (logic == null || gate < 0 || index >= logic.Rows.Count)
            {
                return;
            }
            LogicRow row = logic.Rows[index];
            if (!row.Ready)
            {
                return;
            }
            row.Kind.Value = gate;
            Queue(row.Kind);
            // The gate decides whether input B is read at all, and whether the
            // switch means "inverted" -- both of which the row shows.
            Fill();
        }

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
            Apply(row.Emulate, cell);
            Queue(row.Emulate);
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

        /// <param name="where">Which switch column this box is in, as the x it was
        /// built at. The three are told apart by position because the columns they
        /// stand for are no longer anything <see cref="Table"/> knows about: they
        /// do not sort, so they have no column number.</param>
        private void Flipped(int index, float where, bool on)
        {
            if (filling)
            {
                return;
            }
            if (Gated)
            {
                // The logic table has one switch a row, so there is nothing to tell
                // apart: which of the game's two controls it stands for is the
                // gate's business, not the panel's.
                if (index >= logic.Rows.Count || !logic.Rows[index].Ready)
                {
                    return;
                }
                logic.Rows[index].Mode.IsActive = on;
                Queue(logic.Rows[index].Mode);
                return;
            }
            if (served == null || index >= served.Rows.Count)
            {
                return;
            }
            Row row = served.Rows[index];
            if (!row.Ready)
            {
                return;
            }
            float[] x, w;
            Columns(out x, out w);
            MToggle toggle = where <= x[CHold] ? row.Hold
                : (where <= x[CStop] ? row.Stop : row.Loop);
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

            // One value into every row of a standing selection. That is what the
            // selection is for: reaching down a column and typing once is the whole
            // of "give these eight the same wait".
            int first = InPick(index, wait) ? Mathf.Min(pickFrom, pickTo) : index;
            int last = InPick(index, wait) ? Mathf.Max(pickFrom, pickTo) : index;
            for (int i = first; i <= last && i < served.Rows.Count; i++)
            {
                Row one = served.Rows[i];
                if (!one.Ready)
                {
                    continue;
                }
                MSlider each = wait ? one.Wait : one.Duration;
                each.Value = value;
                Queue(each);
                // Written back, so a rejected value changes in front of whoever
                // typed it rather than silently.
                Set(wait ? table[i].Wait : table[i].Duration, value);
            }
            Unpick();
            if (wait)
            {
                // A wait decides which timer this is.
                Numbers();
            }
        }

        private void SortBy(int column)
        {
            if (Gated)
            {
                sortAscending = column == sortColumn ? !sortAscending : true;
                sortColumn = column;
                List<MapperType> gateTouched = new List<MapperType>();
                LogicTable.Sort(logic, sortAscending, gateTouched);
                Commit(gateTouched);
                Fill();
                return;
            }
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
            if (Gated)
            {
                List<MapperType> gateTouched = new List<MapperType>();
                if (LogicTable.Add(logic, gateTouched) < 0)
                {
                    Flash(plusLabel, "REACHED THE "
                          + LogicGatePlusBehaviour.MaxRows + " GATES LIMIT",
                          UIF.Hot);
                    return;
                }
                Commit(gateTouched);
                Rebuild();
                return;
            }
            if (served == null)
            {
                return;
            }
            List<MapperType> touched = new List<MapperType>();
            int made = Table.Add(served, touched);
            if (made < 0)
            {
                Flash(plusLabel, "REACHED THE " + TimerPlusBehaviour.MaxRows
                      + " TIMERS LIMIT", UIF.Hot);
                return;
            }
            Commit(touched);
            Rebuild();
        }

        private void Delete(int index)
        {
            if (Gated)
            {
                if (logic.Count <= 1)
                {
                    Flash(plusLabel, "A BLOCK KEEPS ONE ROW", UIF.Hot);
                    return;
                }
                List<MapperType> gateTouched = new List<MapperType>();
                LogicTable.Remove(logic, index, gateTouched);
                Commit(gateTouched);
                Rebuild();
                return;
            }
            if (served == null)
            {
                return;
            }
            if (served.Count <= 1)
            {
                Flash(plusLabel, "A BLOCK KEEPS ONE ROW", UIF.Hot);
                return;
            }
            List<MapperType> touched = new List<MapperType>();
            Table.Remove(served, index, touched);
            Commit(touched);
            Rebuild();
        }

        private void DoConvert()
        {
            if (Gated)
            {
                try
                {
                    int gatesMade = Conversion.Into(logic);
                    Flash(convertLabel,
                          gatesMade + (gatesMade == 1 ? " GATE ADDED" : " GATES ADDED"),
                          UIF.Live);
                }
                catch (Exception e)
                {
                    Flash(convertLabel, "COULD NOT CONVERT", UIF.Hot);
                    Log.Warn("convert failed: " + e);
                }
                return;
            }
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
                Flash(convertLabel,
                      made + (made == 1 ? " TIMER ADDED" : " TIMERS ADDED"), UIF.Live);
            }
            catch (Exception e)
            {
                Flash(convertLabel, "COULD NOT CONVERT", UIF.Hot);
                Log.Warn("convert failed: " + e);
            }
        }

        /// <summary>
        /// Says something on the button it is about, for a few seconds, and then
        /// puts the button's own word back.
        ///
        /// It was a line of its own along the bottom of the panel, which cost a
        /// row of height on every block for something shown a few seconds a
        /// session. On the button, the message is where the click that caused it
        /// was, and the panel is a row shorter.
        /// </summary>
        private void Flash(Text label, string words, Color colour)
        {
            if (label == null)
            {
                return;
            }
            Unflash();
            flashing = label;
            flashWas = label.text;
            flashUntil = Time.unscaledTime + FlashSeconds;
            label.text = words;
            label.color = colour;
        }

        /// <summary>How long a message holds the button before its own word comes
        /// back. Long enough to read twice.</summary>
        private const float FlashSeconds = 4f;

        private Text flashing;
        private string flashWas;
        private float flashUntil;

        private void Unflash()
        {
            if (flashing == null)
            {
                return;
            }
            flashing.text = flashWas;
            flashing.color = UIF.Ink;
            flashing = null;
        }

        /// <summary>
        /// The row count changed, so the table's geometry has. Rebuilt rather than
        /// grown: the rows are laid out against a count and there is no cheaper
        /// honest way to add one in the middle of that.
        /// </summary>
        private void Rebuild()
        {
            TimerPlusBehaviour block = served;
            LogicGatePlusBehaviour gates = logic;
            // Where the list was looked at. A row added or deleted somewhere above
            // the fold should not carry the view back to the top -- the row being
            // worked on is the one on screen.
            float keep = scroll != null && scroll.content != null
                ? scroll.content.anchoredPosition.y : 0f;
            Teardown();
            served = block;
            logic = gates;
            if (!Build())
            {
                Hide();
                return;
            }
            window.SetActive(true);
            Fill();
            Canvas.ForceUpdateCanvases();
            Dock();
            Look(keep);
            Curtain(true);
        }

        /// <summary>
        /// Puts the view back where it was, as far as the list still goes.
        ///
        /// Clamped rather than remembered exactly: deleting the last rows makes the
        /// list shorter than the offset it was scrolled to, and a ScrollRect left
        /// past its own end shows empty space until something nudges it.
        /// </summary>
        private void Look(float keep)
        {
            if (scroll == null || scroll.content == null || windowRect == null)
            {
                return;
            }
            float span = Mathf.Max(0f, scroll.content.sizeDelta.y
                - (scroll.viewport != null ? scroll.viewport.rect.height
                                           : windowRect.rect.height));
            Vector2 at = scroll.content.anchoredPosition;
            scroll.content.anchoredPosition =
                new Vector2(at.x, Mathf.Clamp(keep, 0f, span));
            Canvas.ForceUpdateCanvases();
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
            StandOff();
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
        /// <summary>
        /// Keeps the panel out of a drag that started somewhere else.
        ///
        /// Besiege drags its own mapper by the mouse, and its handling stops the
        /// moment the pointer is over another interface -- so dragging the window
        /// down, over this panel, took the pointer off the game's own window and
        /// the drag stopped dead with the window part-way. The block mapper alone
        /// does not show it, because the mapper is tall enough that the pointer
        /// stays on it.
        ///
        /// A press that does not land on this window means the gesture is somebody
        /// else's for as long as the button is down, so the raycaster goes off and
        /// nothing here is under the pointer at all. A press that lands on the
        /// window is ours and is left alone.
        /// </summary>
        private void StandOff()
        {
            if (caster == null)
            {
                return;
            }
            if (Input.GetMouseButtonDown(0) && !Under())
            {
                caster.enabled = false;
            }
            else if (!caster.enabled && !Input.GetMouseButton(0)
                     && !Input.GetMouseButton(1))
            {
                caster.enabled = true;
            }
        }

        /// <summary>
        /// Whether the pointer is over anything of the panel's.
        ///
        /// The window, and the list a cell opens -- which hangs outside the window
        /// by design, and which a raycaster switched off for the length of a press
        /// would otherwise make unclickable below the window's own bottom edge.
        /// </summary>
        private bool Under()
        {
            if (window == null || !window.activeSelf || windowRect == null)
            {
                return false;
            }
            Vector2 at = Input.mousePosition;
            // Null camera: the canvas is a screen-space overlay.
            return RectTransformUtility.RectangleContainsScreenPoint(
                       windowRect, at, null)
                || Choices.Over(at);
        }

        private void LateUpdate()
        {
            // Tab hides Besiege's own interface, and this panel is the lower half
            // of the block mapper -- half a mapper left hanging over a hidden HUD
            // is exactly what Tab is pressed to get rid of.
            //
            // The canvas, not the window: switching the window off is the path that
            // hands every control back to the stock mapper, and coming out of Tab
            // would find the panel gone and the mapper full of rows. Disabling the
            // canvas draws nothing and changes nothing else -- the tooltip and the
            // variable list are on it too, so they go with it.
            if (canvas != null)
            {
                canvas.enabled = !Hidden;
            }

            if (window == null || !window.activeSelf)
            {
                return;
            }
            if (!StillOpen())
            {
                Hide();
                return;
            }
            if (flashing != null && Time.unscaledTime >= flashUntil)
            {
                Unflash();
            }
            // A pointer held still at the edge of the frame sends no drag events,
            // and holding still at the edge is exactly how a reach past the end of
            // the list is asked for.
            Reach();
            Keyboard();
            Dock();
            Curtain(false);
        }

        /// <summary>Whether the player has hidden the game's interface.</summary>
        private static bool Hidden
        {
            get
            {
                try
                {
                    return StatMaster.hudHidden;
                }
                catch (Exception)
                {
                    return false;
                }
            }
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
                if (Gated)
                {
                    return mapper.Block.GetComponent<LogicGatePlusBehaviour>() == logic;
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
            float tall = Mathf.Max(MinHeight,
                Mathf.Min(contentHeight + StripHeight, MaxHeight, room));
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
            float wide = frame.width * Scale();
            if (Gated && wide < GatesWidth)
            {
                wide = GatesWidth;
            }
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
            if (scroll == null || scroll.content == null || content == null
                || windowRect == null)
            {
                return;
            }
            float at = scroll.content.anchoredPosition.y;
            float tall = windowRect.rect.height;
            if (!force && Mathf.Abs(at - curtainAt) < Slop
                       && Mathf.Abs(tall - curtainTall) < Slop)
            {
                return;
            }
            curtainAt = at;
            curtainTall = tall;

            // Measured against the window, through the transforms, rather than
            // against the ScrollRect's viewport. A viewport may be missing, may be
            // named anything, and may be the ScrollRect's own rect -- and each of
            // those was a way for a row to be counted as on screen while it was
            // over the strip along the bottom. The window is none of those things,
            // and the strip is measured from its bottom edge.
            float half = tall * 0.5f;
            float floor = -half + StripHeight;
            Vector3[] corners = new Vector3[4];

            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform row = content.GetChild(i) as RectTransform;
                if (row == null)
                {
                    continue;
                }
                row.GetWorldCorners(corners);
                // 0 is the bottom-left corner and 1 the top-left.
                float bottom = windowRect.InverseTransformPoint(corners[0]).y;
                float top = windowRect.InverseTransformPoint(corners[1]).y;
                bool inside = bottom >= floor - Slop && top <= half + Slop;
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
