using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// The block's table, drawn with UI Factory and docked under Besiege's block
    /// mapper as its lower half. One instance, serving whichever block is open;
    /// nothing holds a control across opens, as blocks do not share their mapper
    /// controls. Without UI Factory it never builds, and blocks keep their stock
    /// controls.
    /// </summary>
    public class Panel : MonoBehaviour
    {
        // Under uGUI's own Dropdown canvas at 30000, so nothing of ours can end up
        // over a list Besiege spawns.
        private const int CanvasOrder = 2400;

        /// <summary>The width until the mapper has been measured; after that, the
        /// mapper's own.</summary>
        private const float DefaultWidth = 434f;

        /// <summary>The logic table's narrowest width: room for seven characters in
        /// each name column, taken out of the number and switch columns.</summary>
        private const float GatesWidth = 434f;

        /// <summary>The logic table's narrower key/variable bubble, which leaves
        /// the names more room.</summary>
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
        /// The fixed strip along the bottom -- rule, "+", buttons -- outside the
        /// scrolling rows, so it is always in reach. A constant, so the viewport is
        /// kept clear of it before it is built. No plate of its own: clipping keeps
        /// the rows out (<see cref="Curtain"/>).
        /// </summary>
        private const float StripHeight = RuleGap + 2f + RowGap
                                        + RowHeight + RowGap
                                        + RowHeight + 4f + Margin;

        /// <summary>Rows shown before the table scrolls.</summary>
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

        /// <summary>A switch column's picture size, a little over the lettering so
        /// thin strokes read at the same weight.</summary>
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

        /// <summary>Whether the panel has drawn a block, which tells blocks to hide
        /// their stock controls. Latched: every <c>DisplayInMapper</c> change
        /// rebuilds the mapper.
        /// </summary>
        public static bool Usable;

        /// <summary>The table changed shape elsewhere -- the node editor added or
        /// deleted a row -- so it is rebuilt.</summary>
        public static void Restack()
        {
            if (only != null)
            {
                only.Refresh();
            }
        }

        private static Panel only;

        /// <summary>Whether the first open has been reported. See
        /// <see cref="MapperOpened"/>.</summary>
        private static bool timed;

        private void Refresh()
        {
            if (window == null || !window.activeSelf)
            {
                return;
            }
            if (!Reshaped())
            {
                Rebuild();
            }
        }

        /// <summary>A table that changed length but still builds as many rows --
        /// a window's worth -- is resized and pointed again rather than built
        /// again: rebuilding the window was most of what an add or a delete cost.
        /// False when it has to be rebuilt.</summary>
        private bool Reshaped()
        {
            int count = Rows;
            if (scroll == null || scroll.content == null || windowRect == null
                || table.Count != Mathf.Min(count, RowsShown + 2)
                || Mathf.Abs(builtWidth - width) > 0.5f)
            {
                return false;
            }
            float keep = scroll.content.anchoredPosition.y;
            builtRows = count;
            FitContent(rowsTop + count * (RowHeight + RowGap) + Margin);
            Fill();
            Canvas.ForceUpdateCanvases();
            Dock();
            Look(keep);
            Pool(false);
            Curtain(true);
            return true;
        }

        /// <summary>Values changed elsewhere (the node editor, an undo) but not the
        /// shape: the cells are written again.</summary>
        public static void Refill()
        {
            if (only != null && only.window != null && only.window.activeSelf)
            {
                only.Fill();
            }
        }

        public static bool Serving(MonoBehaviour block)
        {
            return Usable;
        }

        /// <summary>Set once the panel gives up -- no UI Factory, or a build that
        /// threw -- after which blocks keep their stock controls.</summary>
        public static bool Failed;

        private float width = DefaultWidth;
        private Canvas canvas;

        /// <summary>The panel's raycaster, switched off during a drag that began
        /// elsewhere (<see cref="StandOff"/>).</summary>
        private GraphicRaycaster caster;
        private GameObject window;
        private RectTransform windowRect;
        private ScrollRect scroll;
        private Transform content;
        private Camera mapperEye;

        private bool hooked;
        private bool ready;
        private bool said;

        /// <summary>The Timer Plus block the table was built for, and its row
        /// count: either changing means a rebuild.</summary>
        private TimerPlusBehaviour served;

        /// <summary>The Computer block shown instead; only one of the two is
        /// set.
        /// </summary>
        private ComputerBehaviour logic;

        /// <summary>Whether the open block is a logic table. Not `Logic`: Besiege
        /// has global types of that name.</summary>
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

        /// <summary>Where the rows start in the content, under the headings, and
        /// the scroll the timer table's built rows were last pointed for.</summary>
        private float rowsTop;
        private float pooledAt = float.NaN;

        /// <summary>True while the panel is writing its controls from the block, so
        /// their own change events do not echo back into it.</summary>
        private bool filling;

        /// <summary>Controls changed but not committed yet: a drag writes live
        /// values every frame and commits once, when the button comes up.</summary>
        private readonly List<MapperType> pending = new List<MapperType>();

        // ---- the rows, as built ----------------------------------------------

        private class Cells
        {
            public GameObject Frame;

            /// <summary>Which data row this shows. Fixed in the logic table; in the
            /// timer table a built row is pointed at whichever row the scroll has
            /// under it (see `Pool`), so every handler asks this, never a
            /// captured number.</summary>
            public int Index = -1;
            public RowNumber Number;
            public InputField Wait;
            public InputField Duration;

            /// <summary>The boxes' plain colour, read off the prefab.</summary>
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

            /// <summary>Bars over input B, or the switch, when the gate does not
            /// read it.
            /// </summary>
            public RawImage Barred;
            public RawImage ModeBarred;

            /// <summary>The letter on the switch: T for toggle mode, I for
            /// inverted.</summary>
            public Text ModeName;
        }

        private readonly List<Cells> table = new List<Cells>();
        /// <summary>The buttons' own lettering, which is also where anything the
        /// panel has to say is said -- see <see cref="Flash"/>.</summary>
        private Text plusLabel;
        private Text importLabel;
        private Text convertLabel;

        /// <summary>Where the "+" and the convert button live: pinned to the bottom
        /// of the window, outside the scrolling view.</summary>
        private Transform fixedStrip;
        /// <summary>The sort triangle on each sortable heading, turned over for
        /// descending.
        /// </summary>
        private readonly RawImage[] marks = new RawImage[HeadNames.Length];

        private int sortColumn = -1;
        private bool sortAscending = true;

        // ---- lifecycle -------------------------------------------------------

        private void Awake()
        {
            only = this;
        }

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
            ComputerBehaviour gates = null;
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper != null && mapper.Block != null)
                {
                    block = mapper.Block.GetComponent<TimerPlusBehaviour>();
                    gates = mapper.Block.GetComponent<ComputerBehaviour>();
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
                float began = Time.realtimeSinceStartup;
                int spawned = UIF.Spawned;
                if (gates != null)
                {
                    Show(gates);
                }
                else
                {
                    Show(block);
                }
                if (!timed)
                {
                    // Logged once a session, to tell this mod's share of a slow
                    // open from the mapper's.
                    timed = true;
                    Log.Info("first open: "
                             + Mathf.RoundToInt((Time.realtimeSinceStartup - began)
                                                * 1000f)
                             + " ms in the panel and the board, "
                             + (UIF.Spawned - spawned) + " prefabs built");
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

        /// <summary>Opens the logic table: a rebuild when the block or its size
        /// changed, then a fill.</summary>
        private void Show(ComputerBehaviour block)
        {
            // Which table it is, before the width is worked out: the logic table
            // has a minimum the timer's does not, and Widen reads it.
            bool other = logic != block || served != null;
            logic = block;
            served = null;

            // The block takes its prefix the first time it is opened -- `ne_001_`,
            // the lowest free -- and it is saved with the machine.
            MText claimed = block.Claim();
            if (claimed != null)
            {
                // Applied, not committed: `OnEditField` would reserialise the block
                // and file an undo step for a value nobody typed.
                try { claimed.ApplyValue(); }
                catch (Exception) { }
            }

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
            Claim();
            logic.ShowStock(false);
            window.SetActive(true);
            Unflash();
            Fill();
            Canvas.ForceUpdateCanvases();
            Dock();
            Curtain(true);

            // The board opens with the table: both are the same circuit. Last, so
            // it claims the shared tooltip and list.
            if (Editor.Available)
            {
                Editor.Open(logic);
                // And the switch that says whether it is up follows it.
                Watching();
            }
        }

        private void Show(TimerPlusBehaviour block)
        {
            // Another block's menu is this one's menu closing.
            Editor.Dropped();
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
            Claim();
            served.ShowStock(false);
            window.SetActive(true);
            // Whatever the last visit was told is not news about this one.
            Unflash();
            Fill();
            Canvas.ForceUpdateCanvases();
            Dock();
            Curtain(true);
        }

        /// <summary>Takes the shared tooltip and list, which belong to the window
        /// opened last. On opening only: two windows claiming every frame would
        /// keep rebuilding them.</summary>
        private void Claim()
        {
            if (canvas == null)
            {
                return;
            }
            RectTransform home = canvas.GetComponent<RectTransform>();
            Tip.Tips.Home(home);
        }

        private void Hide()
        {
            // The board is part of the block's menu unless it has been pinned up.
            Editor.Dropped();
            // A cell still listening holds a menu count; closing out from under it
            // would leave that count up and the game's wheel zoom dead.
            KeyCell.Dropped();
            if (window != null)
            {
                window.SetActive(false);
            }
            // A tip belongs to what it explains, and what it explained is gone.
            Tip.Tips.Hide();
            Choices.Close();
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
                // Anchored to the screen's middle, which the docking measures from.
                windowRect.anchorMin = new Vector2(0.5f, 0.5f);
                windowRect.anchorMax = new Vector2(0.5f, 0.5f);
                windowRect.pivot = new Vector2(0.5f, 0.5f);

                Untitle();
                content = ScrollContent();
                // The tooltip lives on the canvas: inside the scrolling content it
                // would be clipped.
                Tip.Tips.Home(canvas.GetComponent<RectTransform>());

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
            // A canvas of its own: two stencil masks at one depth (the viewport's,
            // the chat window's) cut holes in each other, and a bare parent has no
            // size to clamp to.
            GameObject go = new GameObject("NodeEditorCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // UI Factory is authored for 1920x1080, matched on height.
            scaler.matchWidthOrHeight = 1f;

            caster = go.AddComponent<GraphicRaycaster>();
            return true;
        }

        /// <summary>Removes the Window prefab's title bar -- the panel is the
        /// mapper's lower half -- and stretches the scroll view over the
        /// space.</summary>
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

        /// <summary>The Window prefab's scroll content: rows must go in there, or
        /// they are drawn behind the scroll view.</summary>
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

            // One guard on the window stops the wheel zooming the camera over any
            // row in it.
            if (window.GetComponent<ZoomGuard>() == null)
            {
                window.AddComponent<ZoomGuard>();
            }
            return scroll.content;
        }

        /// <summary>The frame the rows scroll inside, made here with the prefab's
        /// content moved in: the prefab's viewport may be null or named anything,
        /// which let rows run over the strip. This one is the window less the strip
        /// and the scrollbar.</summary>
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

            // An invisible plate so the frame gets the wheel; behind everything in
            // it, since children are raycast first.
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

        /// <summary>The scrollbar, built rather than borrowed: the prefab's spans
        /// the whole window, strip and all.</summary>
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
            // namespace, and this file is compiled inside `NodeEditorMod`.
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
            importLabel = null;
            convertLabel = null;
            pinBox = null;
            boardBox = null;
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
            pooledAt = float.NaN;
        }

        // ---- geometry --------------------------------------------------------

        private float Full { get { return width - Margin * 2f - BarGutter; } }

        /// <summary>Where the table starts and how wide it is. The logic table is
        /// drawn tighter, since every unit of margin comes out of a name.</summary>
        private float Edge { get { return Gated ? 4f : Margin; } }

        private float Wide
        {
            get
            {
                return Gated ? width - Edge - (BarWidth + BarInset + 3f) : Full;
            }
        }

        /// <summary>Where each column starts and how wide it is: the number and
        /// switches are fixed, the rest shared by the times and the key.</summary>
        private void Columns(out float[] x, out float[] w)
        {
            if (Gated)
            {
                // The number, one switch and four name columns (two inputs, gate,
                // key); the gate a little narrower, "SR LATCH" being its longest.
                // Number and switch kept tight.
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
            // The times get more than the key, since "DURATION" grows on hover and
            // must stay in its column. The rest goes to the key.
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
            rowsTop = y;

            table.Clear();
            int count = Rows;
            // A window's worth of rows and two to spare, however long the table:
            // which rows they show follows the scroll (see `Pool`). The content is
            // as tall as every row would be, so the scrollbar is the table's.
            int built = Mathf.Min(count, RowsShown + 2);
            float at = y;
            for (int i = 0; i < built; i++)
            {
                table.Add(Gated ? LogicRowCells(i, ref at, x, w)
                                : BuildRow(i, ref at, x, w));
            }
            return y + count * (RowHeight + RowGap) + Margin;
        }

        /// <summary>Column headings; the number column has none. The switch columns
        /// show a picture, with these names as the fallback.</summary>
        private static readonly string[] HeadNames =
        {
            "", "WAIT", "DURATION", "H", "S", "L", "EMULATE"
        };

        /// <summary>Heading tooltips, which is where the pictured switch columns
        /// are named.
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


        /// <summary>The column headings. Only the two times sort; the rest are
        /// labels, with their click feedback taken off.</summary>
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
                // The prefab's swell grows the whole heading into its neighbour,
                // and its press animation shrinks the target mid-click; ours grows
                // the lettering in place.
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
                        UIF.Grow(go, label);
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
                    // Kept rather than destroyed, so the heading keeps its plate
                    // but does not light up.
                    click.enabled = false;
                }
            }
            y += HeadHeight + RowGap;
            return y;
        }

        /// <summary>A heading's sort mark: a triangle at its right end, hidden
        /// until sorted by. One picture turned over, so the words do not
        /// shift.</summary>
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
            // Pivoted in its middle, so turning it over leaves it where it stands.
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

        /// <summary>A picture in place of a heading's lettering: square, inset, and
        /// tinted as the lettering was.</summary>
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

        /// <summary>The logic table's headings and their tooltips.</summary>
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
                    UIF.Grow(go, label);
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

        /// <summary>One logic row: two inputs, the gate, its switch and the key it
        /// presses.
        /// </summary>
        private Cells LogicRowCells(int index, ref float y, float[] x, float[] w)
        {
            Cells cells = new Cells();

            GameObject frame = new GameObject("Row" + index);
            frame.transform.SetParent(content, false);
            frame.AddComponent<RectTransform>();
            UIF.Fit(frame.GetComponent<RectTransform>(), 0f, y, width, RowHeight);
            cells.Frame = frame;
            cells.Index = index;
            Transform host = frame.transform;

            cells.Number = RowNumber.Make(host, x[CNumber], 0f, w[CNumber], RowHeight);
            cells.Number.Clicked = delegate { Delete(cells.Index); };
            cells.Number.Watch(frame);

            cells.InA = KeyCell.Make(host, x[LInputA], 0f, w[LInputA], RowHeight,
                                     GateBubble);
            cells.InA.Row = index;
            cells.InA.Changed = GateKeyChanged;

            cells.InB = KeyCell.Make(host, x[LInputB], 0f, w[LInputB], RowHeight,
                                     GateBubble);
            cells.InB.Row = index;
            cells.InB.Changed = GateKeyChanged;
            // Built with the row: a row's gate can change under the pointer.
            cells.Barred = Bars(cells.InB.gameObject);

            cells.Gate = UIF.Spawn(UIF.ButtonPrefab, host);
            if (cells.Gate != null)
            {
                UIF.Fit(cells.Gate.GetComponent<RectTransform>(), x[LGate], 0f,
                        w[LGate], RowHeight);
                UIF.NoSwell(cells.Gate);
                cells.GateName = Pin(cells.Gate, "", UIF.Ink);
                UIF.Grow(cells.Gate, cells.GateName);
                Button click = cells.Gate.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(delegate { PickGate(cells.Index); });
                }
            }

            cells.ModeBox = Box(host, x[LMode], w[LMode], cells);
            if (cells.ModeBox != null)
            {
                cells.ModeName = cells.ModeBox.GetComponentInChildren<Text>(true);
                cells.ModeBarred = Bars(cells.ModeBox.gameObject);
            }

            cells.Emulate = KeyCell.Make(host, x[LEmulate], 0f, w[LEmulate], RowHeight,
                                         GateBubble);
            cells.Emulate.Row = index;
            cells.Emulate.Changed = GateKeyChanged;
            cells.Emulate.Answers = true;

            y += RowHeight + RowGap;
            return cells;
        }

        /// <summary>Bars over a cell that is not read. Drawn over rather than
        /// hidden: the value is still saved, and a gate that reads it again finds
        /// it.</summary>
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

            // One object per row, so clipping decides once for the whole row.
            GameObject frame = new GameObject("Row" + index);
            frame.transform.SetParent(content, false);
            frame.AddComponent<RectTransform>();
            UIF.Fit(frame.GetComponent<RectTransform>(), 0f, y, width, RowHeight);
            cells.Frame = frame;

            Transform host = frame.transform;

            // Written by Fill, which is where the wait order is worked out.
            cells.Index = index;
            cells.Number = RowNumber.Make(host, x[CNumber], 0f, w[CNumber], RowHeight);
            cells.Number.Clicked = delegate { Delete(cells.Index); };
            cells.Number.Watch(frame);

            cells.Wait = Number(host, x[CWait], w[CWait], cells, true);
            cells.Duration = Number(host, x[CDuration], w[CDuration], cells, false);
            cells.WaitPlain = Plain(cells.Wait);
            cells.DurationPlain = Plain(cells.Duration);
            cells.Hold = Box(host, x[CHold], w[CHold], cells);
            cells.Stop = Box(host, x[CStop], w[CStop], cells);
            cells.Loop = Box(host, x[CLoop], w[CLoop], cells);

            cells.Emulate = KeyCell.Make(host, x[CEmulate], 0f, w[CEmulate], RowHeight);
            cells.Emulate.Row = index;
            cells.Emulate.Changed = RowKeyChanged;
            cells.Emulate.Answers = true;

            y += RowHeight + RowGap;
            return cells;
        }

        /// <summary>The strip along the bottom, outside the scrolling view: the
        /// rule, "+", the convert button and messages.</summary>
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
                UIF.Grow(go, plusLabel);
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
            float tall = RowHeight + 4f;
            if (Gated)
            {
                // The logic table's pin switch and export are on the node editor's
                // title bar; here is only the switch that opens the editor.
                GameObject board = UIF.Spawn(UIF.TogglePrefab, fixedStrip);
                if (board != null)
                {
                    UIF.Fit(board.GetComponent<RectTransform>(), Edge, y, Wide, tall);
                    UIF.NoSwell(board);
                    UIF.Grow(board, Pin(board, "NODE EDITOR", UIF.Ink));
                    boardBox = board.GetComponent<Toggle>();
                    if (boardBox != null)
                    {
                        boardBox.onValueChanged.AddListener(OpenBoard);
                    }
                }
                return y + tall;
            }
            // Three of a size: import, the pin switch, export -- the switch
            // between the two, as it is about what export makes.
            float third = (Wide - ColGap * 2f) / 3f;

            GameObject import = UIF.Spawn(UIF.ButtonPrefab, fixedStrip);
            if (import != null)
            {
                UIF.Fit(import.GetComponent<RectTransform>(), Edge, y, third, tall);
                UIF.NoSwell(import);
                importLabel = Pin(import, "IMPORT", UIF.Ink);
                UIF.Grow(import, importLabel);
                Button click = import.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(DoImport);
                }
            }

            GameObject pin = UIF.Spawn(UIF.TogglePrefab, fixedStrip);
            if (pin != null)
            {
                UIF.Fit(pin.GetComponent<RectTransform>(), Edge + third + ColGap, y,
                        third, tall);
                UIF.NoSwell(pin);
                Text label = Pin(pin, "PIN BLOCKS", UIF.Ink);
                UIF.Grow(pin, label);
                pinBox = pin.GetComponent<Toggle>();
                if (pinBox != null)
                {
                    pinBox.onValueChanged.AddListener(Pinning);
                }
            }

            GameObject go = UIF.Spawn(UIF.ButtonPrefab, fixedStrip);
            if (go != null)
            {
                UIF.Fit(go.GetComponent<RectTransform>(), Edge + (third + ColGap) * 2f,
                        y, third, tall);
                UIF.NoSwell(go);
                convertLabel = Pin(go, "EXPORT", UIF.Ink);
                UIF.Grow(go, convertLabel);
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

        /// <summary>The switch that opens the node editor on the table being
        /// drawn: the same rows, seen as a circuit.</summary>
        private Toggle boardBox;

        private void OpenBoard(bool on)
        {
            if (filling || logic == null)
            {
                return;
            }
            Editor.Toggle(logic);
        }

        /// <summary>Keeps the switch in step with the editor, which can also close
        /// by its own cross or by Escape.</summary>
        private void Watching()
        {
            if (boardBox == null || logic == null)
            {
                return;
            }
            bool up = Editor.Showing(logic);
            if (boardBox.isOn == up)
            {
                return;
            }
            filling = true;
            boardBox.isOn = up;
            filling = false;
        }

        private void Pinning(bool on)
        {
            if (filling)
            {
                return;
            }
            // The timer table's alone: the logic table's is on the node editor.
            MToggle control = served == null ? null : served.PinControl;
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

        /// <summary>A prefab's caption, pinned to its own control and deaf: the
        /// fixed-width label overhangs a narrow control and steals its neighbour's
        /// clicks.</summary>
        private static Text Pin(GameObject control, string text, Color colour)
        {
            return UIF.Label(control, text, colour, TextAnchor.MiddleCenter, 8,
                             false);
        }

        private Toggle Box(Transform host, float x, float w, Cells cells)
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
                float where = x;
                toggle.onValueChanged.AddListener(
                    delegate(bool on) { Flipped(cells.Index, where, on); });
            }
            return toggle;
        }

        private InputField Number(Transform host, float x, float w, Cells cells, bool wait)
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
                Marquee.On(field);
                bool which = wait;
                // onEndEdit, not onValueChanged: the latter would apply the 2 of a
                // 25 while it is still being typed.
                field.onEndEdit.AddListener(
                    delegate(string typed) { Typed(cells.Index, which, typed); });

                // A sheet over the box, so the number can be dragged too (see
                // ValueField).
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
                value.dragged = delegate(float pixels) { Scrub(cells.Index, which, pixels); };
                value.picking = delegate(Vector2 screen) { Pick(cells.Index, which, screen); };
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

        /// <summary>The box a reach started from, which takes the keyboard when it
        /// ends.
        /// </summary>
        private InputField pickBox;

        /// <summary>Frames left to insist on that selection: the field settles its
        /// caret in its own LateUpdate.</summary>
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

        /// <summary>A selected box's tint: the field's own text-selection colour,
        /// at the box's alpha.</summary>
        private static Color Chosen(InputField box, Color plain)
        {
            if (box == null)
            {
                return plain;
            }
            Color grey = box.selectionColor;
            return new Color(grey.r, grey.g, grey.b, plain.a);
        }

        /// <summary>A drag reaching up or down a column: selects every row between
        /// where it started and the pointer.</summary>
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
                Cells from = CellFor(index);
                pickBox = from == null ? null : (wait ? from.Wait : from.Duration);
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

        /// <summary>Holds a reached box fully selected for a few frames, so typing
        /// replaces its value.</summary>
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
            // Those setters do not redraw the selection highlight; this does.
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

        /// <summary>Tints the selected boxes and clears the rest, every box every
        /// time.
        /// </summary>
        private void Highlight()
        {
            for (int i = 0; i < table.Count; i++)
            {
                Cells row = table[i];
                Tint(row.Wait, InPick(row.Index, true) ? Chosen(row.Wait, row.WaitPlain)
                                                       : row.WaitPlain);
                Tint(row.Duration,
                     InPick(row.Index, false) ? Chosen(row.Duration, row.DurationPlain)
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

        /// <summary>The row under the pointer, or -1, measured through the window's
        /// rect.
        /// </summary>
        private int Under(Vector2 screen)
        {
            if (windowRect == null)
            {
                return -1;
            }
            return RowAt(screen);
        }

        /// <summary>Scrolls while a reach is held near the frame's top or bottom.
        /// From LateUpdate: a pointer held still sends no drag events.</summary>
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
            Pool(false);
            Curtain(false);
            int over = Under(reachAt);
            if (over >= 0)
            {
                pickTo = over;
                Highlight();
            }
        }

        /// <summary>Seconds per pixel of drag: a slider's range per 250 pixels, as
        /// the SpecialEffects mod's fields use.</summary>
        private const float DragPerPixel = 0.004f;

        /// <summary>A number dragged: written live, committed when the button comes
        /// up.
        /// </summary>
        private void Scrub(int index, bool wait, float pixels)
        {
            if (served == null)
            {
                return;
            }
            List<RowData> rows = served.Data;
            if (index < 0 || index >= rows.Count)
            {
                return;
            }
            float step = pixels * SliderSpan * DragPerPixel;

            // Over a selection, every row moves by the same amount and keeps its
            // spacing.
            bool many = InPick(index, wait);
            int first = many ? Mathf.Min(pickFrom, pickTo) : index;
            int last = many ? Mathf.Max(pickFrom, pickTo) : index;
            if (!many)
            {
                // Touching a cell outside the selection is leaving it.
                Unpick();
            }
            for (int i = first; i <= last && i < rows.Count; i++)
            {
                RowData one = rows[i];
                // Never below nought: a wait or a duration below zero is not a
                // thing, and nothing above sixty stops one.
                float value = Mathf.Max(0f, (wait ? one.Wait : one.Duration) + step);
                if (wait)
                {
                    one.Wait = value;
                }
                else
                {
                    one.Duration = value;
                }
                Cells cells = CellFor(i);
                InputField shown = cells == null ? null
                    : (wait ? cells.Wait : cells.Duration);
                if (shown != null)
                {
                    // Written even while focused: the value is moving under the
                    // pointer.
                    shown.text = UIF.Seconds(value);
                }
            }
            served.Store(rows);
            Queue(served.TableControl);
            // The numbers down the left are the wait order, and a wait being
            // dragged is that order changing under the hand.
            Numbers();
        }

        /// <summary>The seconds a drag is measured against: the range of Besiege's
        /// own timer slider.</summary>
        private const float SliderSpan = 60f;

        // ---- the timer table's built rows -----------------------------------

        /// <summary>
        /// Points the timer table's built rows at the data rows the scroll has in
        /// view, and writes them: a window's worth of rows are built whatever the
        /// table's length, so a long table opens and scrolls as fast as a short one.
        /// Only when the scroll moved, unless forced.
        /// </summary>
        private void Pool(bool force)
        {
            if ((Gated ? logic == null : served == null) || scroll == null
                || scroll.content == null
                || table.Count == 0)
            {
                return;
            }
            float at = scroll.content.anchoredPosition.y;
            if (!force && Mathf.Abs(at - pooledAt) < Slop)
            {
                return;
            }
            pooledAt = at;
            float step = RowHeight + RowGap;
            int first = Mathf.Clamp(Mathf.FloorToInt((at - rowsTop) / step), 0,
                                    Mathf.Max(0, Rows - table.Count));
            bool moving = false;
            for (int k = 0; k < table.Count; k++)
            {
                if (table[k].Index != first + k)
                {
                    // Whatever the row is in the middle of -- a number typed, a key
                    // being bound -- is finished for the row it was showing.
                    Let(table[k]);
                    moving = true;
                }
            }
            if (!moving && !force)
            {
                return;
            }
            filling = true;
            try
            {
                for (int k = 0; k < table.Count; k++)
                {
                    Cells cells = table[k];
                    int index = first + k;
                    if (cells.Index == index && !force)
                    {
                        continue;
                    }
                    cells.Index = index;
                    if (cells.Emulate != null)
                    {
                        cells.Emulate.Row = index;
                    }
                    if (cells.InA != null)
                    {
                        cells.InA.Row = index;
                    }
                    if (cells.InB != null)
                    {
                        cells.InB.Row = index;
                    }
                    UIF.Fit(cells.Frame.transform as RectTransform, 0f,
                            rowsTop + index * step, width, RowHeight);
                    if (Gated)
                    {
                        List<LogicRow> gates = logic.Rows;
                        if (index < gates.Count)
                        {
                            GateRow(cells, gates[index]);
                        }
                    }
                    else
                    {
                        List<RowData> rows = served.Data;
                        if (index < rows.Count)
                        {
                            FillRow(cells, rows[index]);
                        }
                    }
                }
            }
            finally
            {
                filling = false;
            }
            Numbers();
            Highlight();
        }

        /// <summary>Writes one built row from its data row.</summary>
        private static void FillRow(Cells cells, RowData row)
        {
            if (cells.Emulate != null)
            {
                cells.Emulate.Load(row.EmulateVariable, row.EmulateKeys, true);
            }
            Set(cells.Wait, row.Wait);
            Set(cells.Duration, row.Duration);
            if (cells.Hold != null) cells.Hold.isOn = row.Hold;
            if (cells.Stop != null) cells.Stop.isOn = row.Stop;
            if (cells.Loop != null) cells.Loop.isOn = row.Loop;
        }

        /// <summary>The built row showing a data row, or null when it is scrolled
        /// out of view.</summary>
        private Cells CellFor(int index)
        {
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].Index == index)
                {
                    return table[i];
                }
            }
            return null;
        }

        /// <summary>Ends a typing or a binding in a built row, which writes it into
        /// the row it was for.</summary>
        private static void Let(Cells cells)
        {
            if (cells.Emulate != null)
            {
                cells.Emulate.Let();
            }
            if (cells.InA != null)
            {
                cells.InA.Let();
            }
            if (cells.InB != null)
            {
                cells.InB.Let();
            }
            if (cells.Wait != null && cells.Wait.isFocused)
            {
                cells.Wait.DeactivateInputField();
            }
            if (cells.Duration != null && cells.Duration.isFocused)
            {
                cells.Duration.DeactivateInputField();
            }
        }

        /// <summary>The timer row under the pointer, or the nearest end of the
        /// table, from the content's own offsets: most rows are not built.</summary>
        private int RowAt(Vector2 screen)
        {
            RectTransform rows = content as RectTransform;
            Vector2 point;
            if (rows == null || Rows == 0
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                       rows, screen, null, out point))
            {
                return -1;
            }
            // Down from the content's top edge, which is its pivot.
            int index = Mathf.FloorToInt((-point.y - rowsTop) / (RowHeight + RowGap));
            return Mathf.Clamp(index, 0, Rows - 1);
        }

        private float Rule(float y)
        {
            UIF.Plate(fixedStrip, Edge, y + RuleGap * 0.5f, Wide, 2f, RuleInk)
                .GetComponent<Image>().raycastTarget = false;
            return y + RuleGap + 2f + RowGap;
        }

        // ---- reading the block -----------------------------------------------

        /// <summary>Writes every cell from the block, every time: a window reused
        /// for another block would otherwise show the last one's values.</summary>
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
            // Every built row pointed and written, then the rest.
            Pool(true);
            filling = true;
            try
            {
                Numbers();
                Heads();
                ShowPin();
            }
            finally
            {
                filling = false;
            }
        }

        /// <summary>The logic table's fill: every cell every time, for the same
        /// reason.
        /// </summary>
        private void GateFill()
        {
            // Every built row pointed and written, then the rest.
            Pool(true);
            filling = true;
            try
            {
                Heads();
                ShowPin();
            }
            finally
            {
                filling = false;
            }
        }

        /// <summary>Writes one built logic row from its row.</summary>
        private static void GateRow(Cells cells, LogicRow row)
        {
            if (!row.Ready)
            {
                return;
            }
            int gate = row.Gate;
            cells.InA.Show(row.InputA);
            // No count on an input the gate does not read: see KeyCell.Show.
            cells.InB.Show(row.InputB, Gates.UsesB(gate));
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
            // Barred and dead, not merely barred: an input this gate never reads is
            // one no key or name should be put on, as the switch beside it is one no
            // click should move. What it already holds stays held, and comes back
            // when a gate that reads it is chosen.
            cells.InB.Live = Gates.UsesB(gate);
            if (cells.ModeName != null)
            {
                cells.ModeName.text = Gates.ModeLetter(gate);
            }
            if (cells.ModeBarred != null)
            {
                // Barred *and* switched off, not merely barred: a switch this gate
                // never reads is one a click should not move.
                bool has = Gates.UsesMode(gate);
                cells.ModeBarred.gameObject.SetActive(!has);
                cells.ModeBox.interactable = has;
            }
            if (cells.Number != null)
            {
                // Row order, not a rank: a gate has no order of its own, and the
                // number is here to be pointed at.
                cells.Number.Number = (cells.Index + 1).ToString();
            }
        }

        /// <summary>Shows the block's pin setting on its switch, inside a fill so
        /// it is not taken for the player's.</summary>
        private void ShowPin()
        {
            if (pinBox == null)
            {
                return;
            }
            MToggle control = served == null ? null : served.PinControl;
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
            field.text = UIF.Seconds(value);
        }

        /// <summary>Numbers the rows by firing order -- the shortest wait is 1 --
        /// with ties in row order.</summary>
        private void Numbers()
        {
            if (served == null)
            {
                return;
            }
            List<RowData> rows = served.Data;
            int count = rows.Count;
            if (ranks.Length != count)
            {
                ranks = new int[count];
            }
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            // Ties in row order, so the sort itself need not be stable.
            Array.Sort(order, delegate(int a, int b)
            {
                int by = rows[a].Wait.CompareTo(rows[b].Wait);
                return by != 0 ? by : a.CompareTo(b);
            });
            for (int rank = 0; rank < count; rank++)
            {
                ranks[order[rank]] = rank + 1;
            }
            for (int i = 0; i < table.Count; i++)
            {
                Cells cells = table[i];
                if (cells.Number != null && cells.Index >= 0 && cells.Index < count)
                {
                    cells.Number.Number = ranks[cells.Index].ToString();
                }
            }
        }

        /// <summary>Each row's place in firing order, over the whole table: the
        /// rows built are only some of it.</summary>
        private int[] ranks = new int[0];

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

        /// <summary>One of a logic row's three keys changed; the cell says
        /// which.</summary>
        private void GateKeyChanged(KeyCell cell)
        {
            if (filling || logic == null || cell.Row < 0
                || cell.Row >= logic.Rows.Count)
            {
                return;
            }
            LogicRow row = logic.Rows[cell.Row];
            Cells shown = CellFor(cell.Row);
            if (!row.Ready || shown == null)
            {
                return;
            }
            MKey key = cell == shown.InA ? row.InputA
                     : (cell == shown.InB ? row.InputB : row.Emulate);
            Apply(key, cell);
            Queue(key);
        }

        /// <summary>Offers the twelve gates in the game's order, then the
        /// timer.</summary>
        private void PickGate(int index)
        {
            Cells cells = CellFor(index);
            if (logic == null || index < 0 || index >= logic.Rows.Count || cells == null)
            {
                return;
            }
            List<string> names = new List<string>();
            for (int g = 0; g < Gates.Kinds; g++)
            {
                names.Add(Gates.Names[g]);
            }
            RectTransform under = cells.Gate == null
                ? null : cells.Gate.transform as RectTransform;
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
            if (filling || served == null)
            {
                return;
            }
            List<RowData> rows = served.Data;
            if (cell.Row < 0 || cell.Row >= rows.Count)
            {
                return;
            }
            RowData row = rows[cell.Row];
            // The cell's mode decides: a variable cell with nothing typed is
            // unbound, not its old keycode.
            if (cell.UsesVariable)
            {
                row.EmulateVariable = Bindings.Tidied(cell.Variable);
                row.EmulateKeys = new KeyCode[0];
            }
            else
            {
                row.EmulateVariable = null;
                row.EmulateKeys = cell.Code != KeyCode.None
                    ? new KeyCode[] { cell.Code } : new KeyCode[0];
            }
            served.Store(rows);
            Queue(served.TableControl);
        }

        /// <summary>Writes a cell through to its key. The cell's mode decides: a
        /// variable cell with nothing typed is unbound, not its old
        /// keycode.</summary>
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

        /// <param name="where">Which switch column, as the x it was built at: these
        /// columns do not sort, so they have no column number.</param>
        private void Flipped(int index, float where, bool on)
        {
            if (filling)
            {
                return;
            }
            if (Gated)
            {
                // The logic table has one switch a row; which game control it means
                // is up to the gate.
                if (index >= logic.Rows.Count || !logic.Rows[index].Ready)
                {
                    return;
                }
                logic.Rows[index].Mode.IsActive = on;
                Queue(logic.Rows[index].Mode);
                return;
            }
            if (served == null)
            {
                return;
            }
            List<RowData> rows = served.Data;
            if (index < 0 || index >= rows.Count)
            {
                return;
            }
            RowData row = rows[index];
            float[] x, w;
            Columns(out x, out w);
            if (where <= x[CHold])
            {
                row.Hold = on;
            }
            else if (where <= x[CStop])
            {
                row.Stop = on;
            }
            else
            {
                row.Loop = on;
            }
            served.Store(rows);
            Queue(served.TableControl);
        }

        private void Typed(int index, bool wait, string text)
        {
            if (filling || served == null)
            {
                return;
            }
            List<RowData> rows = served.Data;
            if (index < 0 || index >= rows.Count)
            {
                return;
            }

            float value;
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out value))
            {
                // Not a number: put the real value back, so the box never keeps
                // something the block does not hold.
                Cells own = CellFor(index);
                if (own != null)
                {
                    Set(wait ? own.Wait : own.Duration,
                        wait ? rows[index].Wait : rows[index].Duration);
                }
                return;
            }
            // No negative seconds, and no upper bound, as Besiege's timer. Rounded
            // to what the box shows -- half a tick at most -- so what is stored is
            // what is shown.
            value = Mathf.Max(0f, Mathf.Round(value * 100f) / 100f);

            // One value into every row of a standing selection.
            int first = InPick(index, wait) ? Mathf.Min(pickFrom, pickTo) : index;
            int last = InPick(index, wait) ? Mathf.Max(pickFrom, pickTo) : index;
            for (int i = first; i <= last && i < rows.Count; i++)
            {
                if (wait)
                {
                    rows[i].Wait = value;
                }
                else
                {
                    rows[i].Duration = value;
                }
                // Written back, so a rejected value changes in front of whoever
                // typed it rather than silently.
                Cells cells = CellFor(i);
                if (cells != null)
                {
                    Set(wait ? cells.Wait : cells.Duration, value);
                }
            }
            served.Store(rows);
            Queue(served.TableControl);
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
                          + ComputerBehaviour.MaxRows + " GATES LIMIT",
                          UIF.Hot);
                    return;
                }
                Commit(gateTouched);
                Refresh();
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
            Refresh();
        }

        private void Delete(int index)
        {
            if (Gated)
            {
                List<MapperType> gateTouched = new List<MapperType>();
                // The whole block, so one undo restores the row, its wires and its
                // board place.
                BlockInfo before = LogicTable.Marked(logic);
                LogicTable.Erase(logic, index, gateTouched);
                Shifted(index, gateTouched);
                if (before != null)
                {
                    // Written without filing anything: this edit's undo step is
                    // filed whole below, and `Commit` would file one per control.
                    LogicTable.Applied(logic, gateTouched);
                    LogicTable.Filed(logic, before);
                }
                else
                {
                    Commit(gateTouched);
                }
                Refresh();
                return;
            }
            if (served == null)
            {
                return;
            }
            List<MapperType> touched = new List<MapperType>();
            Table.Remove(served, index, touched);
            Commit(touched);
            Refresh();
        }

        /// <summary>Moves board positions along with the rows when one is deleted,
        /// or every later gate is drawn where its neighbour was.</summary>
        private void Shifted(int index, List<MapperType> touched)
        {
            if (logic == null || logic.LayoutControl == null)
            {
                return;
            }
            Wiring board = Wiring.Load(logic.LayoutControl.Value);
            board.Forget(index);
            logic.LayoutControl.Value = board.Save();
            touched.Add(logic.LayoutControl);
        }

        /// <summary>The timer table's IMPORT: every timer on the machine as rows,
        /// taken off the machine in the same undo step as the rows' edit.</summary>
        private void DoImport()
        {
            if (served == null)
            {
                return;
            }
            TimerPlusBehaviour block = served;
            try
            {
                BlockInfo before = LogicTable.Marked(block.BlockBehaviour);
                int left;
                bool differ;
                List<UndoAction> undo;
                int took = Conversion.From(block, out left, out undo, out differ);
                UndoAction edit = LogicTable.Edited(block.BlockBehaviour, before);
                if (edit != null)
                {
                    undo.Add(edit);
                }
                Machine machine = Machine.Active();
                if (undo.Count > 0 && machine != null && machine.UndoSystem != null)
                {
                    machine.UndoSystem.AddActions(undo);
                }
                // Logged as well: taking the timers off closes the mapper, and this
                // panel with it.
                Log.Info(took + " timer(s) imported" + (left > 0 ? ", " + left
                         + " left on the machine" : "") + (differ
                         ? "; they did not all start the way the block does" : "."));
                Flash(importLabel, took + (differ ? " IN, CHECK ACTIVATE" : " IMPORTED"),
                      differ ? UIF.Hot : UIF.Live);
                Rebuild();
            }
            catch (Exception e)
            {
                Flash(importLabel, e.Message.ToUpperInvariant(), UIF.Hot);
                Log.Warn("import failed: " + e);
            }
        }

        /// <summary>The timer table's EXPORT. The logic table's is the node
        /// editor's.</summary>
        private void DoConvert()
        {
            if (served == null)
            {
                return;
            }
            if (served.Count == 0)
            {
                // Nothing to make blocks of is the table being empty, not the
                // export going wrong: say which.
                Flash(convertLabel, "NO TIMERS\nTO EXPORT", UIF.Hot);
                return;
            }
            try
            {
                int made = Conversion.Into(served);
                // Logged as well: selecting the new blocks closes the mapper, and
                // this panel with it.
                Log.Info(made + " timer block(s) added from the table.");
                Flash(convertLabel,
                      made + (made == 1 ? " TIMER ADDED" : " TIMERS ADDED"), UIF.Live);
            }
            catch (Exception e)
            {
                Flash(convertLabel, "COULD NOT EXPORT", UIF.Hot);
                Log.Warn("convert failed: " + e);
            }
        }

        /// <summary>Says something on the button it is about for a few seconds,
        /// then puts the button's own word back.</summary>
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

        /// <summary>The row count changed, so the table's geometry is
        /// rebuilt.</summary>
        private void Rebuild()
        {
            TimerPlusBehaviour block = served;
            ComputerBehaviour gates = logic;
            // Keep the scroll position: an edit above the fold should not jump to
            // the top.
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
            Pool(false);
            Curtain(true);
        }

        /// <summary>Restores the scroll position, clamped to the list's new length.
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

        private void Commit(List<MapperType> changed)
        {
            if (changed == null)
            {
                return;
            }
            // A logic row's controls are the block's own data, committed as its
            // text (see `LogicTable.Settle`).
            if (logic != null)
            {
                LogicTable.Settle(logic, changed);
            }
            for (int i = 0; i < changed.Count; i++)
            {
                Commit(changed[i]);
            }
        }

        /// <summary>Writes a setting through the mapper, so it is saved and
        /// undoable; `ApplyValue` where that is not up.</summary>
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

        /// <summary>Whether the board's window has been built ahead of time, while
        /// the game is still in its menus.</summary>
        private bool warmedBoard;

        private void Update()
        {
            if (!warmedBoard && UIF.Available)
            {
                // Last of the warming: the board's window, which is the biggest
                // single thing this mod builds.
                warmedBoard = true;
                // The list's canvas too: the first list of a session was placed
                // against a canvas not laid out yet.
                Choices.Ready();
                Editor.Warm();
            }
            if (!ready)
            {
                // UI Factory loads a moment after the mod. Asked once a second:
                // while it is absent, each ask costs a caught TypeLoadException.
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

        /// <summary>Keeps the panel out of a drag that started elsewhere --
        /// Besiege's mapper drag stops over another interface -- by switching the
        /// raycaster off for a press that misses this window, until the button
        /// comes up.</summary>
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

        /// <summary>Whether the pointer is over the window, or the list a cell
        /// opened, which hangs outside it.</summary>
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
            // Tab hides Besiege's interface, and this with it. The canvas, not the
            // window: switching the window off hands controls back to the stock
            // mapper.
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
            // A pointer held still at the frame's edge sends no drag events, so
            // reach from here.
            Reach();
            Keyboard();
            Watching();
            Dock();
            Pool(false);
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
                    return mapper.Block.GetComponent<ComputerBehaviour>() == logic;
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

        /// <summary>Hides rows rather than clipping them: a row shows only while it
        /// is wholly inside the frame, as the prefab's mask does not
        /// clip.</summary>
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

            // Measured against the window: a viewport may be missing or be the
            // ScrollRect's own rect, which counted rows over the strip as on
            // screen.
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

        /// <summary>Besiege's mapper window in screen pixels: the tallest renderer
        /// named "Background" -- not "WideShadow", nor the button
        /// "Visual".</summary>
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

        /// <summary>The topmost camera drawing the mapper's layer: the mapper is
        /// drawn in the world.</summary>
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
