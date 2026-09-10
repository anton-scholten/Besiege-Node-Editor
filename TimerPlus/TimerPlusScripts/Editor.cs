using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimerPlusMod
{
    /// <summary>
    /// The Logic Board's editor: nodes on a sheet, wires between them, and a
    /// button that turns the lot into Besiege's own logic gates.
    ///
    /// Its own window rather than a panel docked under the block mapper -- a graph
    /// wants room, and it should stay up while the player clicks about the machine.
    /// Opened by selecting the block; closed by its own cross.
    ///
    /// Written rather than borrowed: every node-editor library for Unity is an
    /// editor-time tool or wants its own assembly shipped beside it, and this mod
    /// compiles with Besiege's own compiler against the game's assemblies. What
    /// was left to write is a draggable box, a line between two of them, and the
    /// arithmetic that puts the line's ends on the right boxes.
    /// </summary>
    public class Editor : MonoBehaviour
    {
        // Over the panel's canvas, under uGUI's own Dropdown canvas at 30000.
        private const int CanvasOrder = 2500;

        /// <summary>What the window opens at, and the least it can be dragged
        /// down to. It is resized by its corners after that.</summary>
        private const float StartWidth = 894f;
        private const float StartHeight = 574f;
        private const float LeastWidth = 420f;
        private const float LeastHeight = 300f;

        /// <summary>Where it opens, from the middle of the screen: under the block
        /// mapper's own corner and to the right, so the table it belongs to and the
        /// board are both in view without either covering the other.</summary>
        private static readonly Vector2 StartAt = new Vector2(418f, -128f);

        /// <summary>How the wires are drawn. Three ways, cycled by the switch in
        /// the title bar: a straight line, a curve, or right angles.</summary>
        private const int Straight = 0;
        private const int Curved = 1;
        private const int Square = 2;

        /// <summary>Kept between openings rather than saved with the machine: how
        /// somebody likes to look at a board is about them, not about the board.
        /// </summary>
        private static int style = Curved;

        /// <summary>Whether nodes sit on the grid's intersections. Kept between
        /// openings, like the wire style and for the same reason: how somebody
        /// likes to lay a board out is about them, not about the board.</summary>
        private static bool grid = true;
        private const float BarHeight = 26f;
        private const float Margin = 8f;
        /// <summary>
        /// How big an end is drawn: four grid squares across and two down, so a node
        /// on the grid fills whole cells and a row of them lines up with the lines
        /// behind it. See <see cref="GridStep"/>.
        /// </summary>
        private const float NodeWidth = GridStep * 4f;
        private const float NodeHeight = GridStep * 2f;

        /// <summary>How tall a gate is drawn. Shorter than an end: an end holds the
        /// key or the name it stands for, and a gate holds nothing -- what it
        /// answers to is a name the board makes up and nobody needs to read.
        /// </summary>
        private const float GateHeight = GridStep;

        /// <summary>And how wide. A gate holds a picture and, sometimes, a switch;
        /// the width of an end is the width of the name in it, and a gate has no
        /// name.</summary>
        private const float GateWidth = GridStep * 3f;

        /// <summary>How big a gate's picture is drawn: in the palette, and on the
        /// node itself.</summary>
        private const float PaletteIcon = 26f;
        private const float NodeIcon = 24f;
        private const float PortSize = 11f;

        /// <summary>How big the switch on a gate that has one is drawn.</summary>
        private const float Switch = 22f;

        /// <summary>How much of the board a port answers to, as against how much of
        /// it is drawn. See <see cref="Port"/>.</summary>
        private const float PortReach = 24f;

        /// <summary>How big the cross that removes a node is. Bigger than a port:
        /// it is the one thing on a node that is aimed at rather than dragged.
        /// </summary>
        private const float CrossSize = 24f;
        private const float WireWidth = 2f;

        /// <summary>How far the wheel zooms, either way.</summary>
        private const float LeastZoom = 0.4f;
        private const float MostZoom = 2.5f;

        /// <summary>
        /// How much board there is to put nodes on.
        ///
        /// Two views across and two down at the furthest the wheel zooms out,
        /// measured off the window's own opening size: room for a circuit far
        /// larger than thirty-two gates, and an end to it. A board without one is a
        /// board somebody can drop a node into and never find again -- and ZOOM FIT
        /// would then have to draw it, which means everything else on it too small
        /// to read.
        /// </summary>
        /// <summary>
        /// In whole squares of the grid, both ways.
        ///
        /// The grid is one tiled picture stretched over the board, and a `RawImage`
        /// measures its tiling from the **bottom** left. A board that is not a whole
        /// number of squares tall therefore puts every line a fraction of a square
        /// off the top -- and since a node's place is measured from the top, a node
        /// snapped to the grid sat that fraction above the line it belonged on.
        ///
        /// Two views across and two down at the furthest the wheel zooms out comes
        /// to about 4390 by 2470, which is these two.
        /// </summary>
        private const int BoardCells = 137;
        private const int BoardRows = 77;
        private const float BoardWide = GridStep * BoardCells;
        private const float BoardTall = GridStep * BoardRows;

        /// <summary>The middle of it, which is where a board opens and where TIDY
        /// puts what it lays out. A circuit in the middle of its own board has room
        /// to grow in every direction rather than in two.</summary>
        private static Vector2 Centre
        {
            get { return new Vector2(BoardWide * 0.5f, BoardTall * 0.5f); }
        }

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Color Ink = new Color(0.10f, 0.13f, 0.17f, 0.96f);
        private static readonly Color WireInk = new Color(0.55f, 0.72f, 0.85f, 0.85f);
        private static readonly Color LiveInk = new Color(0.012f, 1f, 0.847f, 1f);

        /// <summary>Besiege's red, which is what the game paints anything that
        /// removes something.</summary>
        private static readonly Color Hot = new Color(0.92f, 0.13f, 0.29f, 1f);

        /// <summary>
        /// Every board there is.
        ///
        /// One is made when the mod loads and serves whichever block is opened.
        /// A board that has been pinned up is somebody's, though, so opening
        /// another block makes another board rather than taking that one away --
        /// up to four, after which the newest is the one that gives way. Four
        /// windows is already more than a screen holds comfortably.
        /// </summary>
        private static readonly List<Editor> all = new List<Editor>();

        private const int Most = 4;

        /// <summary>When this board was last opened on something, so the newest can
        /// be told from the rest.</summary>
        private int shown;

        private static int showings;

        private LogicGatePlusBehaviour served;
        private Canvas canvas;
        private GameObject window;
        private RectTransform windowRect;
        private RectTransform sheet;
        private Vector2 size = new Vector2(StartWidth, StartHeight);

        /// <summary>What the nodes and wires hang on: panned and scaled, while the
        /// board it sits in stays where it is and clips.</summary>
        private RectTransform content;

        /// <summary>The node being carried out of the palette, drawn under the
        /// pointer until it is let go of.</summary>
        private GameObject ghost;

        /// <summary>Which nodes are picked out, and what was copied off them.
        /// </summary>
        private readonly List<int> picked = new List<int>();
        private readonly List<Copied> clipboard = new List<Copied>();
        private readonly List<GameObject> ghosts = new List<GameObject>();
        private GameObject band;

        /// <summary>One node on the clipboard: what it is, and where it sat
        /// relative to the others.</summary>
        private class Copied
        {
            public bool IsGate;
            public int Gate;
            public bool Mode;
            public string Variable;
            public KeyCode Key = KeyCode.None;
            public int Kind;                 // for a place
            public string Words;             // what a comment says
            public string Wire;              // a gate's own answer name
            public KeyCode Answers = KeyCode.None;   // or the key it presses
            public string[] Inputs = new string[2];
            public KeyCode[] Keys = new KeyCode[2];
            public Vector2 At;
        }


        private readonly List<GameObject> parts = new List<GameObject>();
        private readonly List<RectTransform> ports = new List<RectTransform>();
        private readonly List<GameObject> wires = new List<GameObject>();

        /// <summary>The node whose answer is waiting for somewhere to go, or -1.
        /// Clicking an output arms it; clicking an input lands it.</summary>
        private int pending = -1;

        public static bool Available { get { return UIF.Available; } }

        /// <summary>
        /// Builds the window before anybody asks for it.
        ///
        /// Everything here -- the canvas, the window prefab, the fourteen palette
        /// buttons, the box in the title bar -- used to be built the first time a
        /// block was opened, on top of everything else that happens at that moment.
        /// Built once while the game is doing something else, it is a window that is
        /// already there when the block wants it.
        /// </summary>
        public static void Warm()
        {
            if (all.Count == 0 || !Available || all[0].window != null)
            {
                return;
            }
            all[0].Prepare();
        }

        private void Prepare()
        {
            if (!Build())
            {
                return;
            }
            // A switch and a key cell of the kinds a node carries, made and thrown
            // away: what costs is the first of each prefab, not the tenth.
            GameObject once = UIF.Spawn(UIF.TogglePrefab, window.transform);
            if (once != null)
            {
                Destroy(once);
            }
            KeyCell cell = KeyCell.Make(window.transform, 0f, 0f, 80f, 20f, 18f);
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
            window.SetActive(false);
        }

        /// <summary>Opens the editor on a board. One editor, whichever board was
        /// asked for last.</summary>
        public static void Open(LogicGatePlusBehaviour block)
        {
            if (block == null)
            {
                return;
            }
            Editor on = Serving(block);
            if (on != null)
            {
                // Already up on this block. Drawing it again would cost a teardown
                // and lose what is picked out, for a board that is already right --
                // it only comes to the front.
                on.shown = ++showings;
                on.Claim();
                Stack();
                Swept(block);
                return;
            }
            on = Lending() ?? Another() ?? Newest();
            if (on == null)
            {
                return;
            }
            on.Show(block);
            Stack();
            Swept(block);
        }

        /// <summary>
        /// Keeps the boards on the block whose menu is up.
        ///
        /// A board is how a logic block is read, so selecting another one should
        /// bring that one up: a board nobody has pinned follows the menu, and a
        /// pinned board is somebody's -- it keeps what it is showing, and the block
        /// just selected gets a board of its own.
        ///
        /// Asked here every so often rather than left to the menu's own opening,
        /// because a mapper changes hands in more ways than it announces: an undo
        /// reopens it, and so does clicking straight from one block to another. A
        /// board that missed one of those is a board drawing a block nobody is
        /// looking at.
        /// </summary>
        private static void Following()
        {
            if (followed == Time.frameCount)
            {
                return;
            }
            followed = Time.frameCount;
            LogicGatePlusBehaviour block = Menu();
            if (block == null)
            {
                return;
            }
            if (Serving(block) == null)
            {
                bool given = false;
                for (int i = 0; i < all.Count && !given; i++)
                {
                    if (all[i] != null && Up(all[i]) && !all[i].pinned)
                    {
                        all[i].Show(block);
                        Stack();
                        given = true;
                    }
                }
                if (!given)
                {
                    // Every board that is up is pinned, so this block gets another
                    // one -- or the newest, once there are four.
                    Open(block);
                }
            }
            Swept(block);
        }

        /// <summary>
        /// Takes down every board left behind by the block that is being edited.
        ///
        /// A board that is not pinned belongs to the menu, and the menu is on one
        /// block: a board still drawing another one is a board somebody walked away
        /// from. It stays only while the pin holds it -- including a board that was
        /// pinned and has since been let go of, which is the case that used to sit
        /// there showing a circuit nobody had asked about since.
        /// </summary>
        private static void Swept(LogicGatePlusBehaviour block)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && Up(all[i]) && !all[i].pinned
                    && all[i].served != block)
                {
                    all[i].Close();
                }
            }
        }

        private static int followed;

        /// <summary>The logic block whose menu is up, if the menu is up on one of
        /// ours at all.</summary>
        private static LogicGatePlusBehaviour Menu()
        {
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper == null || !BlockMapper.IsOpen || mapper.Block == null)
                {
                    return null;
                }
                return mapper.Block.GetComponent<LogicGatePlusBehaviour>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The board already up on this block, if there is one.</summary>
        private static Editor Serving(LogicGatePlusBehaviour block)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].served == block && Up(all[i]))
                {
                    return all[i];
                }
            }
            return null;
        }

        private static bool Up(Editor on)
        {
            return on.window != null && on.window.activeSelf;
        }

        /// <summary>
        /// A board that can be given to another block: one nobody has pinned.
        ///
        /// The open one first -- a board that is up and not pinned is the one being
        /// worked in, and it should follow the block being opened -- then any that
        /// is closed.
        /// </summary>
        private static Editor Lending()
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && Up(all[i]) && !all[i].pinned)
                {
                    return all[i];
                }
            }
            // The one used most recently, rather than the first ever made: with two
            // boards down, the window that comes back should be the one that was
            // being worked in a moment ago and not whichever happens to be first in
            // the list -- which is how a window somebody had finished with came
            // back up in front of the one they had not.
            Editor last = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && !Up(all[i])
                    && (last == null || all[i].shown > last.shown))
                {
                    last = all[i];
                }
            }
            return last;
        }

        /// <summary>Another board, while there is room for one.</summary>
        private static Editor Another()
        {
            if (all.Count >= Most)
            {
                return null;
            }
            GameObject host = new GameObject("NodeEditor " + (all.Count + 1));
            DontDestroyOnLoad(host);
            return host.AddComponent<Editor>();
        }

        /// <summary>The board opened most recently, which is the one that gives way
        /// when every board is pinned and another block is asked for.</summary>
        private static Editor Newest()
        {
            Editor best = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && (best == null || all[i].shown > best.shown))
                {
                    best = all[i];
                }
            }
            return best;
        }

        /// <summary>
        /// Puts the boards in front of each other in the order they were opened, so
        /// the one just opened is the one on top.
        /// </summary>
        private static void Stack()
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || all[i].canvas == null)
                {
                    continue;
                }
                int above = 0;
                for (int j = 0; j < all.Count; j++)
                {
                    if (all[j] != null && all[j] != all[i]
                        && all[j].shown > all[i].shown)
                    {
                        above++;
                    }
                }
                all[i].canvas.sortingOrder = CanvasOrder + (Most - 1 - above);
            }
        }

        /// <summary>Whether a board is up on this block, which is what the table's
        /// own button colours itself by.</summary>
        public static bool Showing(LogicGatePlusBehaviour block)
        {
            return block != null && Serving(block) != null;
        }

        /// <summary>Open on this block, or closed if it already is.</summary>
        public static void Toggle(LogicGatePlusBehaviour block)
        {
            Editor on = Serving(block);
            if (on != null)
            {
                on.Close();
                return;
            }
            Open(block);
        }

        private void Awake()
        {
            if (!all.Contains(this))
            {
                all.Add(this);
            }
        }

        private void OnDestroy()
        {
            all.Remove(this);
        }

        /// <summary>
        /// What the board is drawn from, as one string.
        ///
        /// The graph is the rows, and the rows can be changed by anything -- the
        /// table beside this window, an undo, another player. Rather than hear
        /// about it, the board asks every frame whether what it drew still matches
        /// what is there, which is a couple of dozen short strings and cannot be
        /// out of step.
        /// </summary>
        private string Reading()
        {
            if (served == null)
            {
                return "";
            }
            // One builder for the life of the editor: this runs every frame, and a
            // string of a few hundred characters built afresh each time is a
            // kilobyte a frame of rubbish for the collector.
            said.Length = 0;
            said.Append(served.Count).Append('|');
            said.Append(served.LayoutControl == null ? "" : served.LayoutControl.Value);
            for (int i = 0; i < served.Count && i < served.Rows.Count; i++)
            {
                LogicRow row = served.Rows[i];
                if (!row.Ready)
                {
                    continue;
                }
                said.Append(row.Gate).Append(row.Switch ? '+' : '-')
                    .Append(Bindings.Show(row.InputA, "-")).Append(',')
                    .Append(Bindings.Show(row.InputB, "-")).Append(',')
                    .Append(Bindings.Show(row.Emulate, "-")).Append(';');
            }
            return said.ToString();
        }

        private readonly System.Text.StringBuilder said =
            new System.Text.StringBuilder();

        /// <summary>When the board next asks whether the table has changed.
        /// </summary>
        private float asks;

        private string read;

        /// <summary>
        /// Whether some menu of the game's is up -- the pause menu, the save or
        /// load screens -- which is when this window should get out of the way.
        ///
        /// `StatMaster.inMenu` is a count rather than a flag, and this window puts
        /// itself in it while the pointer is over it -- see `ZoomGuard` -- so what
        /// is asked is whether anything *else* has raised it.
        /// </summary>
        private static bool Shut()
        {
            try
            {
                return StatMaster.isMainMenu
                    || StatMaster.inMenuCounter > ZoomGuard.Raised;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The board's frame.
        ///
        /// Wrapped, because everything the window does between frames is in one
        /// method and Unity stops at the first thing that throws: a single null
        /// halfway down took the board's redrawing, its keys and its auto-panning
        /// with it and left a window that drew once and then ignored the world.
        /// Said once, with the whole exception, so it is a line in the log rather
        /// than a mystery.
        /// </summary>
        private void Update()
        {
            try
            {
                Ticking();
            }
            catch (Exception e)
            {
                if (!moaned)
                {
                    moaned = true;
                    Log.Warn("the board's frame threw, and what comes after it in "
                             + "the frame did not run: " + e);
                }
            }
        }

        private static bool moaned;

        private void Ticking()
        {
            if (window == null || !window.activeSelf)
            {
                return;
            }
            if (served == null)
            {
                // The block it was drawing has been taken off the machine. A board
                // of a circuit that no longer exists is a window to close, pinned
                // or not.
                Close();
                return;
            }
            // Tab hides Besiege's own interface and this is part of it; Escape is
            // what every window is closed with.
            if (canvas != null)
            {
                bool hidden = false;
                try { hidden = StatMaster.hudHidden; }
                catch (Exception) { }
                canvas.enabled = !hidden;
            }
            if (Hotkeys.Copy)
            {
                Copy();
            }
            else if (Hotkeys.Paste)
            {
                Paste();
            }
            Carrying();
            Pointing();
            Verging();
            Fading();
            if ((Input.GetKeyDown(KeyCode.Delete)
                 || Input.GetKeyDown(KeyCode.Backspace))
                && picked.Count > 0 && !Typing())
            {
                Erase();
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Shut())
            {
                Close();
                return;
            }
            // Twenty times a second rather than every frame: this reads every row
            // of the block into a string to see whether anything has moved, and
            // nothing that moves it -- a hand on the table, an undo, another
            // player -- moves it faster than an eye can follow.
            if (Time.unscaledTime < asks)
            {
                return;
            }
            asks = Time.unscaledTime + 0.05f;

            LogicGatePlusBehaviour was = served;
            Following();
            if (served != was)
            {
                // This board has just been handed another block, which has already
                // drawn it.
                return;
            }

            if (owed)
            {
                owed = false;
                board = null;
                Redraw();
                if (middling)
                {
                    middling = false;
                    Middled();
                }
                read = Reading();
                return;
            }
            string now = Reading();
            if (now == read)
            {
                // Settled: what is here now is what the next edit undoes to.
                Settled();
                return;
            }
            read = now;
            // It moved, here or in the table or under an undo. Whatever the last
            // snapshot held is no longer what an edit made now would go back to.
            mark = null;
            board = null;
            if (Rows != counted)
            {
                // A row has arrived or gone from somewhere else -- the table's own
                // list, an undo, another player. The rows are numbered before the
                // ends, so every number the board is holding on to now means a
                // different node. Which of them meant what cannot be worked out
                // from here, so the selection is put down rather than moved to
                // whatever has taken its place.
                picked.Clear();
                naming.Clear();
                pending = -1;
            }
            // Ends are made for bindings nothing on the board accounts for. Only
            // ever here: what reaches this point is a change the board did not
            // make -- a row added or retyped in the block's own menu, an undo,
            // another player -- and that is what has nothing drawn for it.
            Adopt();
            // Whatever changed it -- the table, an undo, a redo -- the board is
            // drawn from the rows and the rows have moved.
            board = null;
            Redraw();
            // Including whatever `Adopt` has just made.
            Ours();
        }

        private void Show(LogicGatePlusBehaviour block)
        {
            served = block;
            if (!Build())
            {
                return;
            }
            shown = ++showings;
            window.SetActive(true);
            board = null;
            read = null;
            mark = null;
            // A block whose board has never been opened has nowhere written down
            // for anything to sit, and a column of nodes over the top of each other
            // is not what it means. It arrives laid out.
            bool blank = served.LayoutControl == null
                || string.IsNullOrEmpty(served.LayoutControl.Value);
            Adopt();
            if (blank)
            {
                Tidy(true);
            }
            // Drawn on the next frame rather than this one. This frame already has
            // the game's own block mapper being built in it, and the table under
            // it; a board of a dozen nodes on top of that is the difference between
            // a menu that opens and a menu that stutters.
            owed = true;
            middling = true;
            read = Reading();
            Claim();
        }

        /// <summary>Takes the shared tooltip, which belongs to whichever window was
        /// opened last.</summary>
        private void Claim()
        {
            if (canvas != null)
            {
                Tip.Tips.Home(canvas.GetComponent<RectTransform>());
            }
        }

        /// <summary>Set when the board is owed a drawing that has been put off to
        /// the next frame.</summary>
        private bool owed;

        /// <summary>And when it is owed a view of the middle of itself, which waits
        /// for the same frame: the window's own rectangles are not settled until
        /// the layout has run once, and the middle is worked out from them.
        /// </summary>
        private bool middling;

        /// <summary>
        /// Opens the view on the middle of the board -- or on the middle of what is
        /// drawn, where that is somewhere else.
        ///
        /// The board is laid out about its own middle, so for anything laid out by
        /// this version the two are the same. A board written by an older one has
        /// its circuit in a corner, and a window opening on the empty middle of the
        /// board is a window that looks broken.
        /// </summary>
        private void Middled()
        {
            Vector2 low;
            Vector2 high;
            Looking(Spread(out low, out high) ? (low + high) * 0.5f : Centre);
        }

        public void Close()
        {
            if (window != null)
            {
                window.SetActive(false);
            }
            Tip.Tips.Hide();
            Choices.Close();
            served = null;
        }

        // ---- the window ------------------------------------------------------

        private bool Build()
        {
            if (window != null)
            {
                return true;
            }
            try
            {
                if (canvas == null)
                {
                    GameObject host = new GameObject("LogicBoardCanvas");
                    host.transform.SetParent(transform, false);
                    canvas = host.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = CanvasOrder;
                    CanvasScaler scaler = host.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = Reference;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1f;
                    host.AddComponent<GraphicRaycaster>();
                }

                window = UIF.Spawn(UIF.WindowPrefab, canvas.transform);
                if (window == null)
                {
                    return false;
                }
                windowRect = window.GetComponent<RectTransform>();
                windowRect.anchorMin = new Vector2(0.5f, 0.5f);
                windowRect.anchorMax = new Vector2(0.5f, 0.5f);
                windowRect.pivot = new Vector2(0.5f, 0.5f);
                windowRect.sizeDelta = size;
                // Each board a little down and across from the last, so a second
                // one is a second window rather than a window nobody can see under
                // the first.
                int nth = all.IndexOf(this);
                windowRect.anchoredPosition = StartAt
                    + new Vector2(nth * 26f, nth * -26f);

                Strip();
                Frame();
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("the board editor could not open: " + e);
                return false;
            }
        }

        /// <summary>
        /// Takes the prefab's own furniture off: its title bar, which said "Sample
        /// Window" over ours and carried a second close cross, and its scroll view,
        /// whose bar stood down the right of a board that does not scroll.
        /// </summary>
        private void Strip()
        {
            Transform bar = Attach.Find(window.transform, "TopBar");
            if (bar != null)
            {
                bar.gameObject.SetActive(false);
            }
            ScrollRect scroll = window.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null)
            {
                if (scroll.verticalScrollbar != null)
                {
                    scroll.verticalScrollbar.gameObject.SetActive(false);
                }
                if (scroll.horizontalScrollbar != null)
                {
                    scroll.horizontalScrollbar.gameObject.SetActive(false);
                }
                scroll.enabled = false;
            }
            Transform view = Attach.Find(window.transform, "ScrollView");
            if (view != null)
            {
                view.gameObject.SetActive(false);
            }
        }

        // The pieces the window is made of, kept so that resizing moves them
        // rather than rebuilding them.
        private RectTransform barRect;
        private RectTransform shutRect;

        /// <summary>
        /// The pin, and whether it is in.
        ///
        /// Out -- white -- the board belongs to the block's menu and goes when it
        /// does. In -- red -- it stays up while the machine is worked on, which is
        /// what somebody wiring a circuit against the rest of the machine wants.
        /// Pulling it out with the menu already gone closes the board, because the
        /// only thing holding it up was the pin.
        /// </summary>
        private RawImage pinIcon;
        private GameObject pinPlate;
        private bool pinned;

        private void Pinning()
        {
            pinned = !pinned;
            Paint();
            if (!pinned && !Menued())
            {
                Close();
            }
        }

        private void Paint()
        {
            if (pinIcon != null)
            {
                pinIcon.color = Color.white;
            }
            if (pinPlate != null)
            {
                Image lit = pinPlate.GetComponent<Image>();
                if (lit != null)
                {
                    lit.color = pinned ? Hot : new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        /// <summary>Set while this board is doing something that may close the
        /// block's menu underneath it. See <see cref="Dropped"/>.</summary>
        private static bool holding;

        /// <summary>
        /// Puts the block's menu back if whatever the board just did took it down,
        /// and leaves it alone if the game has since put one up on something else.
        /// </summary>
        private void Remenu()
        {
            try
            {
                if (served == null || Menued())
                {
                    return;
                }
                if (BlockMapper.CurrentInstance != null && BlockMapper.IsOpen)
                {
                    return;
                }
                BlockBehaviour block = served.BlockBehaviour;
                if (block != null)
                {
                    BlockMapper.Open(block);
                }
            }
            catch (Exception e)
            {
                Log.Warn("could not put the block's menu back: " + e.Message);
            }
        }

        /// <summary>Whether the block's own menu is up on the block this board is
        /// drawing.</summary>
        private bool Menued()
        {
            return served != null && Menu() == served;
        }

        /// <summary>
        /// The block's menu has closed. The board goes with it unless it is pinned:
        /// a board is how the block is read, and a block nobody is looking at is not
        /// being read.
        /// </summary>
        public static void Dropped()
        {
            if (holding)
            {
                // The board is in the middle of something the game answers by
                // taking the block's menu down -- an import, which deletes blocks
                // through the game's own selection tool. The board is not part of
                // what is being closed, and the menu is put back when it is done.
                return;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && !all[i].pinned)
                {
                    all[i].Close();
                }
            }
        }
        private RectTransform boardRect;
        private RectTransform nameRect;
        private RectTransform styleRect;
        private RectTransform gridRect;
        private RectTransform tidyRect;
        private RectTransform fitRect;
        private RectTransform importRect;
        private readonly List<RectTransform> palette = new List<RectTransform>();
        private readonly List<Corner> corners = new List<Corner>();
        private Text styleLabel;
        private Text gridLabel;

        /// <summary>The furniture: a title bar to drag it by, the switches on it,
        /// the board the nodes live on, and the row along the bottom.</summary>
        private void Frame()
        {
            ZoomGuard guard = window.GetComponent<ZoomGuard>();
            if (guard == null)
            {
                guard = window.AddComponent<ZoomGuard>();
            }
            // Tells Besiege a menu is open while the pointer is over the editor, so
            // the wheel does not zoom the camera and a right-drag does not turn it.
            guard.menu = true;

            GameObject bar = Plate(window.transform, 0f, 0f, size.x, BarHeight,
                                   new Color(0f, 0f, 0f, 0.25f));
            barRect = bar.GetComponent<RectTransform>();
            Caption(bar, "LOGIC BOARD", TextAnchor.MiddleCenter);
            NodeDrag drag = bar.AddComponent<NodeDrag>();
            drag.frame = windowRect;
            drag.Moved = delegate(Vector2 by) { windowRect.anchoredPosition += by; };

            // On the left of the title: whether a gate's wire name is shown at all,
            // how the wires are drawn, and a button that lays the board out.
            GameObject wireStyle = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (wireStyle != null)
            {
                styleRect = wireStyle.GetComponent<RectTransform>();
                UIF.NoSwell(wireStyle);
                styleLabel = Caption(wireStyle, Styled(), TextAnchor.MiddleCenter);
                Grow(wireStyle, styleLabel.transform);
                Button click = wireStyle.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Styling);
                }
            }

            GameObject squares = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (squares != null)
            {
                gridRect = squares.GetComponent<RectTransform>();
                UIF.NoSwell(squares);
                gridLabel = Caption(squares, "GRID", TextAnchor.MiddleCenter);
                Grow(squares, gridLabel.transform);
                Tip.On(squares, "Sit nodes on the grid");
                Button click = squares.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Snapping);
                }
            }
            Squared();

            GameObject tidy = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (tidy != null)
            {
                tidyRect = tidy.GetComponent<RectTransform>();
                UIF.NoSwell(tidy);
                Grow(tidy, Caption(tidy, "TIDY", TextAnchor.MiddleCenter).transform);
                Button click = tidy.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Tidy);
                }
            }

            GameObject whole = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (whole != null)
            {
                fitRect = whole.GetComponent<RectTransform>();
                UIF.NoSwell(whole);
                Grow(whole, Caption(whole, "ZOOM FIT",
                                    TextAnchor.MiddleCenter).transform);
                Button click = whole.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Fitted);
                }
            }

            GameObject brought = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (brought != null)
            {
                importRect = brought.GetComponent<RectTransform>();
                UIF.NoSwell(brought);
                Grow(brought, Caption(brought, "IMPORT",
                                      TextAnchor.MiddleCenter).transform);
                Tip.On(brought, "Take the machine's own logic gates into this block");
                Button click = brought.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Import);
                }
            }

            GameObject shut = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (shut != null)
            {
                shutRect = shut.GetComponent<RectTransform>();
                UIF.NoSwell(shut);
                Caption(shut, "", TextAnchor.MiddleCenter);
                // Red behind it rather than a red pin: that is how a switch says it
                // is on everywhere else in the game, and a white pin on red reads
                // at a glance where a red pin on dark does not.
                pinPlate = Plate(shut.transform, 1f, 1f, BarHeight - 8f,
                                 BarHeight - 8f, new Color(0f, 0f, 0f, 0f));
                Image lit = pinPlate.GetComponent<Image>();
                lit.raycastTarget = false;
                // Sized off the diagonal, not the side: turned forty-five degrees
                // it is as tall as its own diagonal, and drawn at the button's
                // height it hung out over the title bar.
                pinIcon = Picture(shut.transform, Glyphs.Pin, (BarHeight - 6f) * 0.75f);
                // Turned, so it reads as a pin pushed into the corner of the window
                // rather than one standing on end.
                pinIcon.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, 45f);
                Paint();
                Grow(shut, pinIcon.transform, 1.15f);
                Tip.On(shut, "Keep the board open");
                Button click = shut.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Pinning);
                }
            }

            Add(0, Place.Input, -1, "INPUT", "", null);
            Add(0, Place.Output, -1, "OUTPUT", "", null);
            Add(0, Place.Note, -1, "", "Comment", Glyphs.Note);
            for (int g = 0; g < Gates.Count; g++)
            {
                Add(g, -1, g, "", Gates.Names[g], null);
            }

            GameObject board = Plate(window.transform, Margin, 0f, 10f, 10f,
                                     new Color(0f, 0f, 0f, 0.20f));
            board.AddComponent<RectMask2D>();
            boardRect = board.GetComponent<RectTransform>();
            sheet = boardRect;

            Sheet felt = board.AddComponent<Sheet>();
            felt.Panned = Panning;
            felt.Zoomed = Zooming;
            felt.Asked = Asked;
            felt.Boxed = Boxing;
            felt.Emptied = Nobody;

            // Everything on the board hangs on this: panning moves it, zooming
            // scales it, and the board itself stays where it is and clips.
            GameObject inside = new GameObject("Content");
            inside.transform.SetParent(board.transform, false);
            content = inside.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            // The grid, on the content so it pans and scales with what is drawn on
            // it: an empty board otherwise looks the same however far it is moved.
            GameObject paper = new GameObject("Grid");
            paper.transform.SetParent(content, false);
            RectTransform mesh = paper.AddComponent<RectTransform>();
            // Exactly the board and no more: it was a square hung about the
            // content's origin, which covered the left of the board and stopped
            // halfway across it. Drawn to the fence instead, the grid says where
            // the board ends as well as where you are on it.
            mesh.anchorMin = new Vector2(0f, 1f);
            mesh.anchorMax = new Vector2(0f, 1f);
            mesh.pivot = new Vector2(0f, 1f);
            mesh.anchoredPosition = Vector2.zero;
            mesh.sizeDelta = new Vector2(BoardWide, BoardTall);
            RawImage lines = paper.AddComponent<RawImage>();
            lines.texture = Glyphs.Grid;
            lines.color = new Color(1f, 1f, 1f, 0.5f);
            lines.raycastTarget = false;
            // uvRect counts in texture widths, so this is one square per GridStep.
            lines.uvRect = new Rect(0f, 0f, BoardWide / GridStep,
                                    BoardTall / GridStep);

            // And the edge of it: a thin solid line where the grid stops, so the
            // end of the board is something seen rather than something bumped into.
            GameObject fence = new GameObject("Fence");
            fence.transform.SetParent(content, false);
            RectTransform edge = fence.AddComponent<RectTransform>();
            edge.anchorMin = new Vector2(0f, 1f);
            edge.anchorMax = new Vector2(0f, 1f);
            edge.pivot = new Vector2(0f, 1f);
            edge.anchoredPosition = Vector2.zero;
            edge.sizeDelta = new Vector2(BoardWide, BoardTall);
            for (int side = 0; side < 4; side++)
            {
                GameObject rail = new GameObject("Rail");
                rail.transform.SetParent(fence.transform, false);
                RectTransform line = rail.AddComponent<RectTransform>();
                bool flat = side < 2;
                line.anchorMin = new Vector2(flat ? 0f : (side == 2 ? 0f : 1f),
                                             flat ? (side == 0 ? 1f : 0f) : 0f);
                line.anchorMax = new Vector2(flat ? 1f : (side == 2 ? 0f : 1f),
                                             flat ? (side == 0 ? 1f : 0f) : 1f);
                line.pivot = new Vector2(0.5f, 0.5f);
                line.sizeDelta = flat ? new Vector2(0f, 1.5f) : new Vector2(1.5f, 0f);
                line.anchoredPosition = Vector2.zero;
                // A RawImage with no texture is a plain filled rectangle, which is
                // all a line is.
                RawImage drawn = rail.AddComponent<RawImage>();
                drawn.color = new Color(1f, 1f, 1f, 0.75f);
                drawn.raycastTarget = false;
            }

            GameObject named = UIF.Spawn(UIF.InputPrefab, bar.transform);
            if (named != null)
            {
                nameRect = named.GetComponent<RectTransform>();
                prefixBox = named.GetComponent<InputField>();
                if (prefixBox != null)
                {
                    UIF.Style(prefixBox.textComponent, UIF.Ink, TextAnchor.MiddleCenter);
                    Text ghostText = prefixBox.placeholder as Text;
                    UIF.Style(ghostText, UIF.Ink, TextAnchor.MiddleCenter);
                    if (ghostText != null)
                    {
                        ghostText.text = "prefix";
                    }
                    prefixBox.onEndEdit.AddListener(Renamed);
                }
                Tip.On(named, "Variable prefix");
            }

            Handles();
            Arrange();
        }

        /// <summary>The four corners, which the window is resized by.</summary>
        private void Handles()
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject go = new GameObject("Corner");
                go.transform.SetParent(window.transform, false);
                RectTransform rect = go.AddComponent<RectTransform>();
                Image catcher = go.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);
                Corner corner = go.AddComponent<Corner>();
                corner.pull = new Vector2(i % 2 == 0 ? -1f : 1f,
                                          i < 2 ? 1f : -1f);
                corner.Pulled = Pulling;
                corners.Add(corner);
                rect.sizeDelta = new Vector2(CornerSize, CornerSize);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private const float CornerSize = 16f;

        /// <summary>How far the grid reaches from the board's own corner, and how
        /// far apart its lines are.</summary>
        private const float GridStep = 32f;

        /// <summary>
        /// Puts every piece of the window where the window's own size says it goes.
        /// Called when it is built and every time a corner is dragged.
        /// </summary>
        private void Arrange()
        {
            if (windowRect == null)
            {
                return;
            }
            windowRect.sizeDelta = size;

            float bit = BarHeight - 6f;
            if (barRect != null)
            {
                UIF.Fit(barRect, 0f, 0f, size.x, BarHeight);
            }
            if (styleRect != null)
            {
                UIF.Fit(styleRect, 3f, 3f, bit * 3.4f, bit);
            }
            if (gridRect != null)
            {
                UIF.Fit(gridRect, 3f + bit * 3.4f + 3f, 3f, bit * 2.4f, bit);
            }
            if (tidyRect != null)
            {
                UIF.Fit(tidyRect, 3f + bit * 5.8f + 6f, 3f, bit * 2.4f, bit);
            }
            if (fitRect != null)
            {
                UIF.Fit(fitRect, 3f + bit * 8.2f + 9f, 3f, bit * 3.4f, bit);
            }
            if (importRect != null)
            {
                UIF.Fit(importRect, 3f + bit * 11.6f + 12f, 3f, bit * 3.0f, bit);
            }
            if (shutRect != null)
            {
                UIF.Fit(shutRect, size.x - bit - 3f, 3f, bit, bit);
            }

            float y = BarHeight + Margin;
            float step = (size.x - Margin * 2f) / (Gates.Count + 3);
            for (int i = 0; i < palette.Count; i++)
            {
                UIF.Fit(palette[i], Margin + step * i, y, step - 3f, 30f);
            }

            float top = y + 30f + Margin;
            // The same gap under the board as beside it: what used to be reserved
            // along the bottom held the prefix box, and that sits in the title bar
            // now.
            float bottom = size.y - Margin;
            if (boardRect != null)
            {
                UIF.Fit(boardRect, Margin, top, size.x - Margin * 2f, bottom - top);
            }

            if (nameRect != null)
            {
                // Beside the cross: what generated wire names start with belongs
                // with the window's own furniture rather than in the middle of its
                // title.
                float wide = Mathf.Min(180f, size.x * 0.3f);
                UIF.Fit(nameRect, size.x - bit - 6f - wide, 3f, wide, bit);
            }
            for (int i = 0; i < corners.Count; i++)
            {
                RectTransform rect = corners[i].transform as RectTransform;
                rect.anchoredPosition = new Vector2(
                    corners[i].pull.x < 0f ? 0f : size.x,
                    corners[i].pull.y > 0f ? 0f : -size.y);
            }
        }

        /// <summary>A corner was pulled: the window grows the way it was pulled and
        /// stays put at the other end.</summary>
        private void Pulling(Vector2 by, Vector2 pull)
        {
            // Up on a top corner grows the window, down on a bottom one grows it
            // too: which way that is depends on the corner, which is what `pull`
            // says. Getting the sign wrong made the top corners behave as the
            // bottom ones.
            Vector2 wanted = new Vector2(
                size.x + by.x * pull.x,
                size.y + by.y * pull.y);
            wanted.x = Mathf.Max(LeastWidth, wanted.x);
            wanted.y = Mathf.Max(LeastHeight, wanted.y);
            Vector2 grew = wanted - size;
            size = wanted;
            // The corner opposite the one being pulled holds still, which is what
            // dragging a corner is understood to do.
            windowRect.anchoredPosition += new Vector2(grew.x * 0.5f * pull.x,
                                                       grew.y * 0.5f * pull.y);
            Arrange();
            // The wires are worked out from where the ports are, and the ports have
            // only just moved: without this they are drawn against last frame's
            // layout, which is what made them lag and look stretched.
            Canvas.ForceUpdateCanvases();
            Strings();
        }

        private void Add(int gate, int kind, int which, string words, string tip,
                         Texture drawn)
        {
            float x = 0f;
            float y = 0f;
            float w = 10f;
            GameObject go = UIF.Spawn(UIF.ButtonPrefab, window.transform);
            if (go == null)
            {
                return;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, w, 30f);
            UIF.NoSwell(go);
            palette.Add(go.GetComponent<RectTransform>());
            if (words.Length > 0)
            {
                Grow(go, Caption(go, words, TextAnchor.MiddleCenter).transform);
            }
            else
            {
                Caption(go, "", TextAnchor.MiddleCenter);
                Grow(go, Picture(go.transform,
                                 drawn != null ? drawn : Glyphs.Gate(gate),
                                 PaletteIcon).transform);
            }
            Tip.On(go, tip);
            int born = kind;
            int what = which;
            Button click = go.GetComponent<Button>();
            if (click != null)
            {
                click.onClick.AddListener(delegate { Born(born, what); });
            }

            // And dragged out of the palette onto the board, which is the other
            // way a hand reaches for a thing it wants somewhere in particular.
            NodeDrag drag = go.AddComponent<NodeDrag>();
            int dragged = what;
            drag.Carrying = delegate(Vector2 screen)
            {
                Carrying(born, dragged, screen);
            };
            drag.Carried = delegate(Vector2 screen)
            {
                Drop();
                Dropped(born, what, screen);
            };
        }

        /// <summary>
        /// The node being dragged out of the palette, drawn under the pointer.
        ///
        /// A see-through copy rather than the node itself: the node does not exist
        /// yet, and a hand that changes its mind should leave the board as it was.
        /// </summary>
        private void Carrying(int kind, int gate, Vector2 screen)
        {
            if (canvas == null)
            {
                return;
            }
            if (ghost == null)
            {
                Vector2 span = Sized(kind, gate);
                ghost = Rounded(canvas.transform, 0f, 0f, span.x, span.y,
                                new Color(Ink.r, Ink.g, Ink.b, 0.55f));
                Texture face = gate >= 0 ? Glyphs.Gate(gate)
                    : (kind == Place.Note ? Glyphs.Note : null);
                if (face != null)
                {
                    RawImage drawn = Picture(ghost.transform, face, NodeIcon);
                    drawn.color = new Color(1f, 1f, 1f, 0.7f);
                }
                RectTransform rect = ghost.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                // As big as the node will be when it lands, which is a board zoomed
                // out or in: a full-size ghost over a half-size board is a promise
                // of something twice what arrives.
                rect.localScale = Zoom();
                // It is only ever looked at.
                Graphic[] parts = ghost.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i].raycastTarget = false;
                }
            }
            Vector2 local;
            RectTransform home = canvas.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    home, screen, null, out local))
            {
                ghost.GetComponent<RectTransform>().anchoredPosition = local;
            }
        }

        /// <summary>Lets the middle button pan the board from whatever this is put
        /// on.</summary>
        private void Waving(GameObject on)
        {
            Pan waved = on.AddComponent<Pan>();
            waved.against = sheet;
            waved.Panned = Panning;
        }

        /// <summary>How far the board is zoomed, as a scale to draw something at
        /// the size it will be on it.</summary>
        private Vector3 Zoom()
        {
            float much = content == null ? 1f : content.localScale.x;
            return new Vector3(much, much, 1f);
        }

        private void Drop()
        {
            if (ghost != null)
            {
                ghost.SetActive(false);
                Destroy(ghost);
                ghost = null;
            }
        }

        /// <summary>A palette button let go of over the board: the node lands where
        /// the hand let go, rather than in the next free slot.</summary>
        private void Dropped(int kind, int gate, Vector2 screen)
        {
            if (served == null || sheet == null)
            {
                return;
            }
            Vector2 onSheet;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    sheet, screen, null, out onSheet) || !sheet.rect.Contains(onSheet))
            {
                // Outside the board is a drag that changed its mind.
                return;
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                return;
            }
            // Put down by its middle, whatever size that node is: a gate is
            // narrower than an end and a comment smaller than either, and one
            // width for all three dropped two of them beside the pointer rather
            // than under it.
            Vector2 span = Sized(kind, gate);
            Born(kind, gate, new Vector2(local.x - span.x * 0.5f,
                                         -local.y - span.y * 0.5f), true);
        }

        /// <summary>How big a node of this sort is drawn, before anything is
        /// written in it.</summary>
        private static Vector2 Sized(int kind, int gate)
        {
            if (gate >= 0)
            {
                return new Vector2(GateWidth, GateHeight);
            }
            return kind == Place.Note ? new Vector2(NoteLeast, 42f)
                                      : new Vector2(NodeWidth, NodeHeight);
        }

        /// <summary>A picture in the middle of a control.</summary>
        private static RawImage Picture(Transform host, Texture drawn, float size)
        {
            GameObject go = new GameObject("Glyph");
            go.transform.SetParent(host, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            RawImage image = go.AddComponent<RawImage>();
            image.texture = drawn;
            image.color = UIF.Ink;
            image.raycastTarget = false;
            return image;
        }

        // ---- the board -------------------------------------------------------

        /// <summary>
        /// The board as it is drawn: the rows in use, then the places that are not
        /// rows. A node's number here is its number in <see cref="ports"/> and in
        /// the wires below.
        ///
        /// The graph is not stored anywhere. A gate node *is* a row of the table,
        /// and a wire *is* a row's input carrying the name another row's answer
        /// goes out under -- so the board and the table are the same thing, and
        /// neither can be out of step with the other.
        /// </summary>
        private int Rows { get { return served == null ? 0 : served.Count; } }

        private int Nodes { get { return Rows + Board.Places.Count; } }

        private Wiring Board
        {
            get
            {
                if (board == null)
                {
                    board = Wiring.Load(served == null || served.LayoutControl == null
                                        ? "" : served.LayoutControl.Value);
                }
                return board;
            }
        }

        private Wiring board;

        /// <summary>How many rows the board was last drawn with.</summary>
        private int counted;

        /// <summary>
        /// Writes where everything sits into the control that holds it.
        ///
        /// Assigning `Value` writes the live value only, so <see cref="Apply"/>
        /// follows: a change that stops at the live value is on screen now and gone
        /// from the save, which is exactly how a board came back empty.
        ///
        /// No undo entry -- this is called from the derivation as well as from
        /// edits. <see cref="Kept"/> is the one that files a step.
        /// </summary>
        private void Keep()
        {
            if (served == null || served.LayoutControl == null)
            {
                return;
            }
            Voiced();
            served.LayoutControl.Value = Board.Save();
            LogicTable.Apply(served.LayoutControl);
        }

        /// <summary>
        /// What is written in each comment, read back off the box before the board
        /// is written down.
        ///
        /// A comment keeps its text in its own text box while somebody is typing,
        /// and it reaches the layout when the typing ends. Anything that writes the
        /// board out mid-sentence -- taking hold of the comment and dragging it is
        /// the easy way -- would otherwise write the sentence as it was before, and
        /// the redraw that follows puts that back on screen: the words vanish under
        /// the hand that typed them.
        /// </summary>
        private void Voiced()
        {
            for (int i = 0; i < notes.Count && i < Nodes; i++)
            {
                InputField box = notes[i];
                if (box == null)
                {
                    continue;
                }
                Place place = Placed(i);
                if (place == null || place.Kind != Place.Note)
                {
                    continue;
                }
                string said = box.text == null ? "" : box.text;
                if (place.Words != said)
                {
                    place.Words = said;
                }
            }
        }

        /// <summary>The same, for a hand that moved something: one undo step for
        /// the move.</summary>
        private void Kept()
        {
            Keep();
            Filed();
            Ours();
        }

        /// <summary>
        /// What the block looked like when this frame began: the whole of it, so an
        /// undo puts every row and every position back at once.
        /// </summary>
        private BlockInfo mark;
        private string marked;

        /// <summary>Takes that snapshot, on a frame where nothing has changed --
        /// which is the state any edit made in the next frame undoes to.</summary>
        private void Settled()
        {
            if (mark != null || served == null)
            {
                return;
            }
            mark = Snap();
            marked = read;
        }

        private BlockInfo Snap()
        {
            try
            {
                return LogicTable.Marked(served);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Files what just happened as one step of Besiege's undo.
        ///
        /// One entry for the whole edit, not one per control: `OnEditField` files a
        /// step per field, so moving a node -- which is a position, two keys and
        /// the layout -- took four presses of undo to put back and put it back a
        /// piece at a time. `UndoActionEdit` carries the block's entire state
        /// either side of the edit, so one press restores every row, every binding
        /// and every position exactly as they were.
        /// </summary>
        private void Filed()
        {
            BlockInfo before = mark;
            mark = null;
            // `Marked` hands back nothing where another player is listening, so a
            // null "before" is also how "not ours to file" arrives here.
            if (before == null || served == null)
            {
                return;
            }
            if (Reading() == marked)
            {
                return;                 // nothing actually moved
            }
            LogicTable.Filed(served, before);
        }

        /// <summary>
        /// Finds the ends of the board in the rows themselves.
        ///
        /// A table typed in by hand has keys and variables in it that no row
        /// produces and names no row reads -- those are what an input and an output
        /// node *are*, so they are made rather than demanded. It is what lets a
        /// table built in the list open as a circuit, and it costs nothing on a
        /// board that already has them: a node whose binding is already there is
        /// not added twice.
        /// </summary>
        private void Adopt()
        {
            if (served == null)
            {
                return;
            }
            int rows = Rows;
            for (int i = 0; i < rows; i++)
            {
                LogicRow row = Row(i);
                if (row == null || !row.Ready)
                {
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    if (port == 1 && !Gates.UsesB(row.Gate))
                    {
                        continue;
                    }
                    // One at a time: an input answers to as many names as are
                    // wired to it, and each of them is either something a node
                    // here makes or something arriving from the machine.
                    Asked(i, port, wanted, coding);
                    for (int n = 0; n < wanted.Count; n++)
                    {
                        string variable = wanted[n];
                        if (Answered(variable, KeyCode.None, i) >= 0
                            || Mine(variable))
                        {
                            // A node here makes it -- or it is one of the board's
                            // own minted names whose gate has gone, which is a
                            // loose end rather than something coming in, and
                            // drawing a node for it would be inventing one out of
                            // a deletion.
                            continue;
                        }
                        Ensure(Place.Input, variable, KeyCode.None);
                    }
                    for (int c = 0; c < coding.Count; c++)
                    {
                        if (Answered(null, coding[c], i) < 0)
                        {
                            Ensure(Place.Input, null, coding[c]);
                        }
                    }
                }

                string said = Bindings.IsVariable(row.Emulate)
                    ? Bindings.Variable(row.Emulate) : null;
                KeyCode code = said == null ? Bindings.Code(row.Emulate)
                                            : KeyCode.None;
                if (said == null && code == KeyCode.None)
                {
                    // A gate that answers to nothing stays that way. It used to be
                    // given a name of the board's own making here, which was an
                    // edit nobody asked for -- and after an undo, an edit that put
                    // back what the undo had just taken away. A wire drawn out of
                    // the gate mints the name it needs at the moment it is drawn,
                    // which is the only moment one is needed.
                    continue;
                }
                if (said == null)
                {
                    if (code != KeyCode.None && !Read(null, code))
                    {
                        Ensure(Place.Output, null, code);
                    }
                    continue;
                }
                // A key answers to several names at once; each of them that nothing
                // reads is an end of the board.
                string[] names = said.Split(';');
                for (int n = 0; n < names.Length; n++)
                {
                    string name = names[n].Trim();
                    if (name.Length == 0 || Mine(name) || Read(name, KeyCode.None))
                    {
                        // A name of the board's own making is what a gate answers
                        // to, not something the machine is listening for: an end
                        // drawn for one is an output node nobody asked for, wired
                        // to a gate that was not wired to anything.
                        continue;
                    }
                    Ensure(Place.Output, name, KeyCode.None);
                }
            }
        }

        /// <summary>Whether a name is one of this block's own, minted for a gate's
        /// answer rather than typed by anybody.</summary>
        private bool Mine(string variable)
        {
            if (variable == null || served == null)
            {
                return false;
            }
            string prefix = served.Prefix;
            return !string.IsNullOrEmpty(prefix) && variable.StartsWith(prefix);
        }

        // ---- the graph, indexed for the length of one operation ------------

        /// <summary>
        /// Which nodes answer to a name, and which answer to a keycode, in node
        /// order.
        ///
        /// <see cref="Feeding"/> asks every node what it answers to and compares
        /// `;`-joined lists of names -- a string joined and split for every
        /// comparison. Once is nothing; TIDY does it a few thousand times, four
        /// ordering passes over every node against every other, each asking
        /// <see cref="Between"/>, each asking `Feeding`. Built once at the top of
        /// an operation, the same question is a dictionary lookup and the whole
        /// thing stops being cubic.
        ///
        /// The answers are the ones the loop would have given. The lists are filled
        /// in node order, so the first entry that is not the node asking is the one
        /// the loop would have returned first; the pieces are split and trimmed
        /// exactly as `Carries` splits and trims them; a reader bound to several
        /// names asks under the joined string and finds nothing, which is what the
        /// loop does too; and an output end answers nothing, so it is left out.
        ///
        /// Held only between <see cref="Sourced"/> and <see cref="Unsourced"/>, and
        /// nothing between them writes a binding -- so it cannot go stale. Anything
        /// asking outside that pair takes the loop, unchanged.
        /// </summary>
        private readonly Dictionary<string, List<int>> named =
            new Dictionary<string, List<int>>();

        private readonly Dictionary<int, List<int>> coded =
            new Dictionary<int, List<int>>();

        /// <summary>How many operations deep the index is: a redraw inside a tidy
        /// wants the same one rather than a second.</summary>
        private int sourcing;

        private void Sourced()
        {
            if (sourcing++ > 0)
            {
                return;
            }
            named.Clear();
            coded.Clear();
            int many = Nodes;
            for (int i = 0; i < many; i++)
            {
                Place place = Placed(i);
                if (place != null && place.Kind == Place.Output)
                {
                    continue;               // an output feeds nothing
                }
                string said;
                KeyCode code;
                Answer(i, out said, out code);
                if (said != null)
                {
                    string[] all = said.Split(';');
                    for (int n = 0; n < all.Length; n++)
                    {
                        string one = all[n].Trim();
                        if (one.Length == 0)
                        {
                            continue;
                        }
                        List<int> who;
                        if (!named.TryGetValue(one, out who))
                        {
                            who = new List<int>();
                            named[one] = who;
                        }
                        if (!who.Contains(i))
                        {
                            who.Add(i);
                        }
                    }
                }
                else if (code != KeyCode.None)
                {
                    List<int> who;
                    if (!coded.TryGetValue((int)code, out who))
                    {
                        who = new List<int>();
                        coded[(int)code] = who;
                    }
                    who.Add(i);
                }
            }
        }

        private void Unsourced()
        {
            if (--sourcing > 0)
            {
                return;
            }
            sourcing = 0;
            named.Clear();
            coded.Clear();
        }

        /// <summary>Every node answering to a binding, or null.</summary>
        private List<int> Answering(string want, KeyCode key)
        {
            List<int> who = null;
            if (want != null)
            {
                named.TryGetValue(want, out who);
            }
            else if (key != KeyCode.None)
            {
                coded.TryGetValue((int)key, out who);
            }
            return who;
        }

        /// <summary>Whether any row's input is bound to this.</summary>
        private bool Read(string variable, KeyCode key)
        {
            for (int i = 0; i < Rows; i++)
            {
                LogicRow row = Row(i);
                if (row == null || !row.Ready)
                {
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    MKey input = port == 0 ? row.InputA : row.InputB;
                    if (variable != null ? Bindings.Holds(input, variable)
                                         : Bindings.Holds(input, key))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>An end of the board for this binding, made if there is not one
        /// already.</summary>
        private void Ensure(int kind, string variable, KeyCode key)
        {
            for (int i = 0; i < Board.Places.Count; i++)
            {
                Place place = Board.Places[i];
                if (place.Kind == kind && place.Same(variable, key))
                {
                    return;
                }
            }
            Place made = new Place();
            made.Kind = kind;
            made.Variable = variable;
            made.Key = key;
            // Down the side it belongs on, under whatever is there already.
            int stacked = 0;
            for (int i = 0; i < Board.Places.Count; i++)
            {
                if (Board.Places[i].Kind == kind)
                {
                    stacked++;
                }
            }
            Vector2 spot = Fenced(Snapped(new Vector2(
                Centre.x + (kind == Place.Input ? -2.5f * NodeWidth
                                                : 1.5f * NodeWidth),
                Centre.y - 60f + stacked * (NodeHeight + 18f))));
            made.X = spot.x;
            made.Y = spot.y;
            Board.Places.Add(made);
            Keep();
        }

        /// <summary>The place a node number stands for, or null when it is a
        /// row.</summary>
        private Place Placed(int node)
        {
            int at = node - Rows;
            return at >= 0 && at < Board.Places.Count ? Board.Places[at] : null;
        }

        private LogicRow Row(int node)
        {
            return node >= 0 && node < Rows && node < served.Rows.Count
                ? served.Rows[node] : null;
        }

        /// <summary>What a node puts out, as a binding.</summary>
        private void Answer(int node, out string variable, out KeyCode key)
        {
            variable = null;
            key = KeyCode.None;
            Place place = Placed(node);
            if (place != null)
            {
                variable = place.Variable;
                key = place.Key;
                return;
            }
            LogicRow row = Row(node);
            if (row == null || !row.Ready)
            {
                return;
            }
            variable = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            key = variable == null ? Bindings.Code(row.Emulate) : KeyCode.None;
        }

        /// <summary>What feeds a node's port: the node number, or -1.</summary>
        /// <summary>
        /// What a port answers to: the names, or the keycodes, as Besiege holds
        /// them.
        ///
        /// A gate's input takes as many as the game allows -- three keycodes or a
        /// hundred names, one or the other and never both -- and the game ORs them,
        /// so a wire is one of a list rather than the whole binding. The two ends
        /// of the board stand for one thing each.
        /// </summary>
        private void Asked(int node, int port, List<string> names,
                           List<KeyCode> codes)
        {
            names.Clear();
            codes.Clear();
            LogicRow row = Row(node);
            if (row != null && row.Ready)
            {
                MKey input = port == 0 ? row.InputA : row.InputB;
                if (Bindings.IsVariable(input))
                {
                    names.AddRange(Bindings.Named(Bindings.Variable(input)));
                    return;
                }
                for (int i = 0; i < input.KeysCount; i++)
                {
                    KeyCode code = input.GetKey(i);
                    if (code != KeyCode.None && !codes.Contains(code))
                    {
                        codes.Add(code);
                    }
                }
                return;
            }
            Place place = Placed(node);
            if (place == null || place.Kind != Place.Output)
            {
                return;
            }
            if (place.Variable != null)
            {
                names.Add(place.Variable);
            }
            else if (place.Key != KeyCode.None)
            {
                codes.Add(place.Key);
            }
        }

        /// <summary>Every node feeding a port, in node order and each once.
        /// </summary>
        private void Feeds(int node, int port, List<int> into)
        {
            into.Clear();
            Asked(node, port, wanted, coding);
            for (int i = 0; i < wanted.Count; i++)
            {
                int from = Answered(wanted[i], KeyCode.None, node);
                if (from >= 0 && !into.Contains(from))
                {
                    into.Add(from);
                }
            }
            for (int i = 0; i < coding.Count; i++)
            {
                int from = Answered(null, coding[i], node);
                if (from >= 0 && !into.Contains(from))
                {
                    into.Add(from);
                }
            }
        }

        /// <summary>What a port answers to while that is being worked out, and the
        /// answer to <see cref="Feeding"/>. Kept rather than made: these are asked
        /// for every port of every node whenever the board is drawn.</summary>
        private readonly List<string> wanted = new List<string>();
        private readonly List<KeyCode> coding = new List<KeyCode>();
        private readonly List<int> feeding = new List<int>();

        /// <summary>The first node feeding a port, or -1. What a single-wire
        /// question wants: whether a port has anything on it at all.</summary>
        private int Feeding(int node, int port)
        {
            Feeds(node, port, feeding);
            return feeding.Count > 0 ? feeding[0] : -1;
        }

        /// <summary>
        /// The first node answering to one binding, other than the one asking.
        /// </summary>
        private int Answered(string want, KeyCode key, int node)
        {
            if (want == null && key == KeyCode.None)
            {
                return -1;
            }
            if (sourcing > 0)
            {
                // The same answer, looked up. See `named`.
                List<int> who = Answering(want, key);
                for (int i = 0; who != null && i < who.Count; i++)
                {
                    if (who[i] != node)
                    {
                        return who[i];
                    }
                }
                return -1;
            }
            for (int i = 0; i < Nodes; i++)
            {
                if (i == node)
                {
                    continue;
                }
                Place place = Placed(i);
                if (place != null && place.Kind == Place.Output)
                {
                    continue;               // an output feeds nothing
                }
                string said;
                KeyCode code;
                Answer(i, out said, out code);
                if (Carries(said, want) || (said == null && code != KeyCode.None
                                            && code == key))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Whether an answer's names include the one being looked for. A
        /// key answers to several names at once, joined with a semicolon, which is
        /// how one gate feeds an output and another gate at the same time.</summary>
        private static bool Carries(string names, string want)
        {
            if (names == null || want == null)
            {
                return false;
            }
            string[] all = names.Split(';');
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Trim() == want)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether anything takes this node's answer, which is what fills the
        /// circle on it.
        ///
        /// An output end is asked the same way the wires into it are drawn -- every
        /// row that presses its name, not the first one found. `Feeding` answers
        /// with one node, and where three gates all raise `door` that is one of the
        /// three: the other two were drawn with a wire coming out of an empty
        /// circle, and which two changed with every undo.
        /// </summary>
        private bool Heard(int node)
        {
            for (int i = 0; i < Nodes; i++)
            {
                Place place = Placed(i);
                if (place != null && place.Kind == Place.Output)
                {
                    if (node < Rows && Presses(node, place))
                    {
                        return true;
                    }
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    // Among however many are wired to that port, not the first of
                    // them.
                    if (Wired(node, i, port))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Wires one node's answer into another's port, by writing the name.
        ///
        /// This is the whole of what wiring means here: the target's input is bound
        /// to whatever the source answers under, and a source that answers under
        /// nothing is given a name first.
        /// </summary>
        private void Join(int from, int to, int port)
        {
            Join(from, to, port, null);
        }

        private void Join(int from, int to, int port, List<MapperType> already)
        {
            if (served == null || from == to)
            {
                return;
            }
            Place landing = Placed(to);
            if (Row(from) == null && landing != null
                && landing.Kind == Place.Output)
            {
                // Both ends of this wire are ends of the board. There is no row to
                // carry it -- what an output stands for is a key some row presses,
                // and an input is a key arriving from the machine -- so the only
                // way to draw it would be to give the two of them one name, which
                // is not a wire but two nodes standing for the same thing. Put a
                // gate between them.
                //
                // Refused before anything else happens: what follows would name the
                // input end on its way past, and a refusal that leaves a mark is a
                // refusal somebody has to undo.
                Told(to, 1, "cannot directly\nconnect to output");
                Strings();
                return;
            }
            string variable;
            KeyCode key;
            Answer(from, out variable, out key);
            if (variable == null && key == KeyCode.None)
            {
                variable = Minted();
                Named(from, variable);
            }

            LogicRow row = Row(to);
            List<MapperType> touched = already != null ? already
                                                       : new List<MapperType>();
            if (row != null && row.Ready)
            {
                // Added to whatever the input answers to already rather than put in
                // its place: a gate's input takes as many wires as the game allows
                // and holds while any of them is raised.
                MKey input = port == 0 ? row.InputA : row.InputB;
                bool onNames = Bindings.IsVariable(input);
                bool onKeys = !onNames && Bindings.Code(input) != KeyCode.None;
                if ((variable != null && onKeys) || (variable == null && onNames))
                {
                    // A Besiege key answers the keyboard or a list of names and
                    // never both, so these two cannot share an input.
                    Told(to, 1 + port, "a key and a name\ncannot share");
                    Strings();
                    return;
                }
                bool room = variable != null ? Bindings.Added(input, variable)
                                             : Bindings.Added(input, key);
                if (!room)
                {
                    Told(to, 1 + port, variable != null
                         ? "that input is full\n100 names"
                         : "that input is full\n3 keys");
                    Strings();
                    return;
                }
                touched.Add(input);
            }
            else
            {
                Place place = Placed(to);
                if (place == null || place.Kind != Place.Output)
                {
                    return;
                }
                // The two have to agree on one binding, and whichever of them
                // already has one decides. The node is the more deliberate of the
                // two -- somebody typed a key into it -- so it wins where both do.
                if (!place.Bound)
                {
                    place.Variable = variable;
                    place.Key = variable == null ? key : KeyCode.None;
                    Keep();
                }
                else
                {
                    LogicRow said = Row(from);
                    if (said != null && said.Ready)
                    {
                        if (place.Variable != null)
                        {
                            Bindings.BindVariable(said.Emulate, place.Variable);
                        }
                        else
                        {
                            Bindings.Bind(said.Emulate, place.Key);
                        }
                        touched.Add(said.Emulate);
                    }
                }
            }
            Commit(touched);
            Redraw();
        }

        /// <summary>Gives a node's answer a name it did not have.</summary>
        private void Named(int node, string variable)
        {
            LogicRow row = Row(node);
            if (row != null && row.Ready)
            {
                Bindings.BindVariable(row.Emulate, variable);
                return;
            }
            Place place = Placed(node);
            if (place != null)
            {
                place.Variable = variable;
                Keep();
            }
        }

        /// <summary>Frees every input of a row that was reading an end being
        /// removed.</summary>
        private void Unread(int node, Place place, List<MapperType> touched)
        {
            LogicRow row = Row(node);
            if (row == null || !row.Ready || place.Kind != Place.Input)
            {
                return;
            }
            for (int port = 0; port < 2; port++)
            {
                MKey input = port == 0 ? row.InputA : row.InputB;
                if (place.Variable != null)
                {
                    if (!Bindings.Holds(input, place.Variable))
                    {
                        continue;
                    }
                    // Only what this end stood for: another wire on the same input
                    // is another end's, and it stays.
                    Bindings.Dropped(input, place.Variable);
                }
                else
                {
                    if (!Bindings.Holds(input, place.Key))
                    {
                        continue;
                    }
                    Bindings.Dropped(input, place.Key);
                }
                touched.Add(input);
            }
        }

        /// <summary>And takes it off again.</summary>
        private void Drops(int node, Place place, List<MapperType> touched)
        {
            LogicRow row = Row(node);
            if (row == null || !row.Ready)
            {
                return;
            }
            string had = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            if (place.Variable == null)
            {
                // The end stands for a key rather than a name. A Besiege key
                // answers either the keyboard or a list of names and never both, so
                // a row pressing this end is pressing nothing else and the wire
                // comes off by unbinding the row's answer. Without this an output
                // holding a key kept every wire that ever landed on it: the drag
                // came off in the hand and the wire stayed on the board.
                if (had != null || place.Key == KeyCode.None
                    || Bindings.Code(row.Emulate) != place.Key)
                {
                    return;
                }
                Bindings.Bind(row.Emulate, KeyCode.None);
                touched.Add(row.Emulate);
                return;
            }
            if (!Carries(had, place.Variable))
            {
                return;
            }
            string[] names = had.Split(';');
            string left = "";
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].Trim() == place.Variable)
                {
                    continue;
                }
                left += left.Length == 0 ? names[i] : ";" + names[i];
            }
            if (left.Length == 0)
            {
                Bindings.Bind(row.Emulate, KeyCode.None);
            }
            else
            {
                Bindings.BindVariable(row.Emulate, left);
            }
            touched.Add(row.Emulate);
        }

        /// <summary>
        /// A name nothing on the board answers to: the block's prefix and the
        /// lowest two-digit number that is free.
        ///
        /// One way of minting a name for the three that wanted one -- a gate being
        /// wired, a gate being pasted, an end whose own name is taken -- because
        /// three ways of spelling it is three sets of names to read on one board.
        /// </summary>
        private string Minted()
        {
            string start = served.Prefix;
            for (int n = 1; n < 100; n++)
            {
                string wanted = start + n.ToString("00");
                if (!Elsewhere(-1, wanted, KeyCode.None))
                {
                    return wanted;
                }
            }
            return start + "00";
        }

        /// <summary>
        /// What the board has just written down, taken as read.
        ///
        /// The frame's watch compares the rows against `read` and answers anything
        /// it did not do itself by deriving the ends again. An edit made here is
        /// therefore read back the moment it is made rather than left for the watch
        /// to notice: a flag saying "the next change is mine" is wrong the moment
        /// an edit changes nothing the watch can see -- an end switched between a
        /// key and a name, say -- because the flag then swallows whatever change
        /// comes next, which is how a key retyped in the table went without its
        /// input node until something else was touched.
        /// </summary>
        private void Ours()
        {
            read = Reading();
        }

        private void Commit(List<MapperType> touched)
        {
            Keep();
            // The table is the same rows seen another way, and it is probably open
            // behind this window.
            Panel.Refill();
            for (int i = 0; i < touched.Count; i++)
            {
                LogicTable.Apply(touched[i]);
            }
            Filed();
            Ours();
        }

        /// <summary>
        /// Throws away the drawn board and draws it again from the rows.
        ///
        /// A board this size redraws in nothing, and a redraw that cannot be out of
        /// step with the table is worth more than a clever one.
        /// </summary>
        private void Redraw()
        {
            // How many rows what is about to be drawn has, so that a row arriving
            // or leaving from somewhere else can be noticed for what it does to the
            // numbering. See `Ticking`.
            counted = Rows;
            Cut();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null)
                {
                    Destroy(parts[i]);
                }
            }
            parts.Clear();
            // The faint edges belong to the bodies just destroyed, and nothing is
            // under the pointer until something is drawn there again.
            marks.Clear();
            rims.Clear();
            notes.Clear();
            bins.Clear();
            // The wires belong to the nodes just cleared; `Wired` fills it again
            // at the end of this, and a board that returns early below has none.
            wired.Clear();
            binned = -1;
            under = -1;
            ports.Clear();
            if (served == null || content == null)
            {
                return;
            }
            if (prefixBox != null && !prefixBox.isFocused)
            {
                prefixBox.text = served.Prefix;
            }

            int many = Nodes;
            // Three ports a node: its answer, then its two inputs.
            for (int i = 0; i < many * 3; i++)
            {
                ports.Add(null);
            }
            for (int i = 0; i < many; i++)
            {
                Draw(i);
            }
            Wired();
            Strings();
        }

        /// <summary>One node: a row of the table, or one of the two ends.</summary>
        private void Draw(int index)
        {
            LogicRow row = Row(index);
            Place place = Placed(index);
            Vector2 at = row != null ? Board.Spot(index)
                                     : new Vector2(place.X, place.Y);

            // A comment is as big as what is written in it; everything else is
            // one size.
            bool note = place != null && place.Kind == Place.Note;
            float wide = row != null ? GateWidth : NodeWidth;
            // A gate is its picture, its switch and its ports; only the two ends
            // have anything written on them, so only they need the room for it.
            float tall = row != null ? GateHeight : NodeHeight;

            GameObject body = Rounded(content, at.x, at.y, wide, tall,
                                      note ? Paper : Ink);
            parts.Add(body);

            int me = index;
            // Room for the two dashed edges, which are made the first time they are
            // wanted rather than now: they are nine objects a node between them,
            // most nodes are never picked out or pointed at, and a board of a dozen
            // nodes is opened far more often than a node is selected.
            while (marks.Count <= index)
            {
                marks.Add(null);
                rims.Add(null);
                notes.Add(null);
            }
            marks[index] = null;
            rims[index] = null;
            notes[index] = null;
            while (bins.Count <= index)
            {
                bins.Add(null);
            }
            bins[index] = null;
            if (Picked(index))
            {
                rims[index] = Edged(index, 1f);
                if (rims[index] != null)
                {
                    rims[index].SetActive(true);
                }
            }

            Hover watch = body.AddComponent<Hover>();
            watch.Node = index;
            watch.Over = Watched;
            watch.Chose = Chosen;

            // The middle button pans the board wherever it is pressed, node or no
            // node: a hand reaching for the view should not have to find a gap
            // between the nodes first.
            Waving(body);

            NodeDrag drag = body.AddComponent<NodeDrag>();
            drag.Held = delegate { Holding(me, drag); };
            drag.Moved = delegate(Vector2 by)
            {
                Hauling(new Vector2(by.x, -by.y));
            };
            drag.Dropped = delegate
            {
                dragging = false;
                hauler = null;
                Kept();
            };

            if (note)
            {
                Comment(body, index, place, wide, tall);
                return;
            }

            // The heading: a gate's picture, or the word an end is. Drawn straight
            // onto the node rather than spawned -- a button prefab brings a plate
            // of its own, and a second dark box inside a dark box is a box nobody
            // asked for. Nothing to click either, which is what makes a click
            // anywhere on a node that is not one of its controls pick the node
            // out: uGUI hands a click to the first handler at or above what it
            // hit, and the first one above a heading is the node.
            //
            // A gate with a switch keeps the right of its plate for it, clear of
            // the answer port's reach -- which is wider than the circle drawn in it
            // and would otherwise take the clicks along the switch's own edge. What
            // is left is the picture's.
            bool switched = row != null && Gates.UsesMode(row.Gate);
            float switchAt = wide - PortSize - (PortReach - PortSize) * 0.5f
                             - Switch;

            GameObject head = new GameObject("Head");
            head.transform.SetParent(body.transform, false);
            head.AddComponent<RectTransform>();
            if (row != null)
            {
                // The picture has the gate to itself unless there is a switch, and
                // then it has what the switch has left: a picture centred in the
                // whole width sits under the switch rather than beside it.
                float room = switched ? switchAt - PortSize - 2f
                                      : wide - PortSize * 2f - 4f;
                UIF.Fit(head.GetComponent<RectTransform>(), PortSize + 2f, 3f,
                        room, tall - 6f);
                Picture(head.transform, Glyphs.Gate(row.Gate), NodeIcon);
            }
            else
            {
                // The word above and the cell below, both clear of the one side the
                // port is on: an input's answer leaves on the right, an output's
                // wire arrives on the left, and the two ends are drawn as each
                // other's mirror so a board reads left to right.
                // Across the whole node: the word belongs in the middle of the
                // thing it names, and a word centred in the room left over by the
                // port sits off to one side of it.
                UIF.Fit(head.GetComponent<RectTransform>(), 0f, 5f, wide, 24f);
                Text word = Caption(head,
                                    place.Kind == Place.Input ? "INPUT" : "OUTPUT",
                                    TextAnchor.MiddleCenter);
                if (word != null)
                {
                    // Room for a bigger word now that the node is two cells tall.
                    word.resizeTextMaxSize = 18;
                }
            }

            if (row != null)
            {
                // The switch, for the gates that have one: inverted for the edge
                // detector, toggle mode for the rest, and nothing at all for the
                // memory gates -- the same rule the table's M column follows.
                if (Gates.UsesMode(row.Gate))
                {
                    GameObject mode = UIF.Spawn(UIF.TogglePrefab, body.transform);
                    if (mode != null)
                    {
                        // Inside the gate, beside its answer port and level with
                        // the picture. Measured from the gate's own width: it was
                        // measured from an end's, which is wider, and the switch
                        // stood off the side of the node.
                        UIF.Fit(mode.GetComponent<RectTransform>(), switchAt,
                                (tall - Switch) * 0.5f, Switch, Switch);
                        UIF.NoSwell(mode);
                        Text letter = Caption(mode, Gates.ModeLetter(row.Gate),
                                              TextAnchor.MiddleCenter);
                        Grow(mode, letter.transform, 1.3f);
                        Toggle box = mode.GetComponent<Toggle>();
                        if (box != null)
                        {
                            box.isOn = row.Switch;
                            LogicRow mine = row;
                            Toggle flips = box;
                            box.onValueChanged.AddListener(delegate(bool on)
                            {
                                if (hushed)
                                {
                                    return;
                                }
                                if (Picking())
                                {
                                    // The click was for the node, not the switch,
                                    // which has already flipped itself.
                                    hushed = true;
                                    flips.isOn = !on;
                                    hushed = false;
                                    return;
                                }
                                mine.Mode.IsActive = on;
                                List<MapperType> touched = new List<MapperType>();
                                touched.Add(mine.Mode);
                                Commit(touched);
                            });
                        }
                    }
                }
            }
            else
            {
                // An end of the board: what it stands for, as a key or a variable.
                KeyCell cell = KeyCell.Make(body.transform, Beside(place), 33f,
                                            Across(wide), 26f, 22f);
                cell.ignoreCtrl = true;
                cell.Load(Shown(index, place.Variable, place.Key), place.Key);
                Place mine = place;
                cell.Changed = delegate(KeyCell edited)
                {
                    Rebind(me, mine, edited);
                };
            }

            // The ports. An answer on the right, inputs on the left -- one for a
            // gate that reads one, none at all on an input node.
            bool answers = row != null || place.Kind == Place.Input;
            float middle = tall * 0.5f - PortSize * 0.5f;
            ports[index * 3] = answers
                ? Port(body.transform, wide - PortSize, middle, true, index, 0)
                : null;
            if (row != null)
            {
                // A gate that reads two has them a quarter and three quarters down
                // its side, whatever it is drawn at; one that reads one has it in
                // the middle.
                bool two = Gates.UsesB(row.Gate);
                ports[index * 3 + 1] = Port(body.transform, 0f,
                    two ? tall * 0.25f - PortSize * 0.5f : middle, false, index, 0);
                if (two)
                {
                    ports[index * 3 + 2] = Port(body.transform, 0f,
                        tall * 0.75f - PortSize * 0.5f, false, index, 1);
                }
            }
            else if (place.Kind == Place.Output)
            {
                ports[index * 3 + 1] = Port(body.transform, 0f, middle, false,
                                            index, 0);
            }

        }

        /// <summary>
        /// Where the cell on an end begins: clear of the port, which is on the right
        /// of an input and the left of an output.
        ///
        /// The two ends are laid out the same way round -- the bubble, then what it
        /// stands for -- and only the side the port is on differs, so the margins
        /// swap and nothing else does.
        /// </summary>
        private static float Beside(Place place)
        {
            return place != null && place.Kind == Place.Input ? 6f : PortSize + 4f;
        }

        /// <summary>And how much room it has: the node less the port's side and a
        /// margin at each edge.</summary>
        private static float Across(float wide)
        {
            return wide - PortSize - 10f;
        }

        /// <summary>
        /// The red cross that removes a node, in the corner of the node the pointer
        /// is on.
        ///
        /// Only that one: a cross on every node is a row of little red crosses over
        /// a board somebody is reading, and the hand that means to remove a node is
        /// already on it. Centred on the corner, so half of it stands off the node
        /// -- and reaching for it still counts as pointing at the node, because
        /// uGUI counts a node as pointed at while the pointer is on any child of
        /// it, wherever that child happens to be drawn.
        /// </summary>
        private GameObject Cross(GameObject host, int me)
        {
            // Drawn rather than spawned: a UI Factory button brings its own dark
            // plate, and a cross wants to be a cross and nothing else.
            GameObject bin = new GameObject("Bin");
            bin.transform.SetParent(host.transform, false);
            RectTransform rect = bin.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CrossSize, CrossSize);
            rect.anchoredPosition = Vector2.zero;
            Image catcher = bin.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            Text cross = Caption(bin, "x", TextAnchor.MiddleCenter);
            cross.color = Hot;
            Button click = bin.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.targetGraphic = catcher;
            Grow(bin, cross.transform, 1.4f);
            click.onClick.AddListener(delegate
            {
                // With control held the click was for picking the node out, and a
                // node picked out and deleted at once is not what either gesture
                // meant.
                if (!Picking())
                {
                    Kill(me);
                }
            });
            return bin;
        }

        /// <summary>The paper a comment is written on: lighter than a node, so it
        /// reads as a note laid on the board rather than a thing in the
        /// circuit.</summary>
        private static readonly Color Paper = new Color(0.16f, 0.20f, 0.26f, 0.96f);

        /// <summary>The clear space round a comment's text, the same on all four
        /// sides, and the widest a comment is allowed to grow before it wraps.
        /// </summary>
        private const float NoteEdge = 5.25f;

        /// <summary>And the clear space inside the text box itself, between its own
        /// dark plate and the letters on it.</summary>
        private const float TextEdge = 8f;
        private const float NoteWide = 340f;

        /// <summary>And the narrowest. A comment of one short word should be one
        /// short word wide -- it is a note, not a node.</summary>
        private const float NoteLeast = 62f;

        /// <summary>
        /// How much room a comment wants, asked of the font rather than guessed.
        ///
        /// A guess at the width of a letter is wrong twice: the box grows in steps
        /// of a whole line where the text grew by a few pixels, and a line that the
        /// guess says fits wraps anyway, dropping its last word onto the next line.
        /// The generator that lays the text out is the only thing that knows.
        /// </summary>
        private static void Measure(Text drawn, string words, out float wide,
                                    out float tall)
        {
            string said = string.IsNullOrEmpty(words) ? "comment" : words;
            wide = NoteLeast;
            tall = 42f;
            if (drawn == null)
            {
                return;
            }
            // A trailing newline is a line of its own, and a generator asked about
            // it says nothing: it has no characters on it.
            string asked = said.EndsWith("\n") ? said + " " : said;
            TextGenerator maker = drawn.cachedTextGeneratorForLayout;
            float much = drawn.pixelsPerUnit;
            // Unconstrained: how wide the longest line would be if nothing wrapped.
            float raw = maker.GetPreferredWidth(asked,
                            drawn.GetGenerationSettings(Vector2.zero)) / much;
            // Both paddings -- the plate's and the text box's -- and a few pixels
            // of slack: a line measured to exactly the width it is laid out in
            // wraps its last word anyway.
            float pad = (NoteEdge + TextEdge) * 2f;
            wide = Mathf.Clamp(raw + pad + 6f, NoteLeast, NoteWide);
            // And how tall it is once wrapped to that width, which is the width the
            // text itself is laid out in.
            float room = wide - pad;
            float high = maker.GetPreferredHeight(asked,
                             drawn.GetGenerationSettings(
                                 new Vector2(room, 0f))) / much;
            tall = Mathf.Clamp(high + pad, 34f, 420f);
        }

        /// <summary>
        /// A comment node: a plate with a box of text in it and nothing else. No
        /// ports, because it stands for nothing the machine can read -- it is a
        /// note to whoever opens the board next.
        /// </summary>
        private void Comment(GameObject body, int index, Place place,
                             float wide, float tall)
        {
            GameObject box = UIF.Spawn(UIF.InputPrefab, body.transform);
            if (box == null)
            {
                return;
            }
            // Inset, so the edge of the plate is somewhere to take hold of it by:
            // the text box takes a drag for itself, to select what is written.
            UIF.Fit(box.GetComponent<RectTransform>(), NoteEdge, NoteEdge,
                    wide - NoteEdge * 2f, tall - NoteEdge * 2f);
            InputField field = box.GetComponent<InputField>();
            if (field == null)
            {
                return;
            }
            field.lineType = InputField.LineType.MultiLineNewline;
            Text ghostText = field.placeholder as Text;
            Inked(field.textComponent, UIF.Ink);
            Inked(ghostText, UIF.QuietInk);
            if (ghostText != null)
            {
                ghostText.text = "comment";
            }
            field.text = place.Words == null ? "" : place.Words;
            notes[index] = field;
            field.interactable = true;
            // Greyed while it is put out of reach for a drag (below), and a comment
            // flickering pale every time it is moved is not worth the tint.
            field.transition = Selectable.Transition.None;
            // A modifier-click is for the node, not for the text: the same rule the
            // key cells follow.
            Deaf guard = box.AddComponent<Deaf>();
            guard.box = field;
            // The middle button pans from the text as well as from the plate.
            Waving(box);

            Place mine = place;
            int me = index;
            GameObject plate = body;
            RectTransform inner = box.GetComponent<RectTransform>();

            // A click on the writing puts the caret in it -- that is the field's
            // own doing, and it is what a hand clicking on words expects. A *drag*
            // on the writing moves the node, which is the other thing a hand does
            // to a note on a board.
            //
            // Both have to come off the same object, because uGUI gives a drag to
            // the first handler at or above what it hit and that is the field. So
            // the node's drag is put on the field beside the field's own, and the
            // field is made non-interactable for the length of it: its handlers
            // all begin `if (!MayDrag(...)) return;`, and that asks. Without it a
            // drag would move the node and select the words at the same time.
            InputField dragged = field;
            NodeDrag carry = box.AddComponent<NodeDrag>();
            carry.frame = body.GetComponent<RectTransform>();
            carry.Held = delegate
            {
                // What is in the box is the comment's text from here on. Putting
                // the field out of reach on the next line drops the focus, and a
                // field losing focus announces its text through `onEndEdit` --
                // which draws the board again, with the node being dragged still
                // under the hand. Written down first, that announcement is old news
                // and `Written` stands down.
                mine.Words = dragged.text == null ? "" : dragged.text;
                dragged.interactable = false;
                Holding(me, carry);
            };
            carry.Moved = delegate(Vector2 by)
            {
                Hauling(new Vector2(by.x, -by.y));
            };
            carry.Dropped = delegate
            {
                dragged.interactable = true;
                dragging = false;
                hauler = null;
                Kept();
            };
            // The size it actually wants, now that there is a font to ask about it.
            Stretch(body, inner, field.textComponent, field.text);
            // Resized as it is typed rather than when the typing ends: a box that
            // only grows once the hand has moved on hides what is being written
            // into it. The layout is written when the typing ends -- one undo step
            // for a comment, not one a letter.
            Text laid = field.textComponent;
            InputField typing = field;
            field.onValueChanged.AddListener(delegate(string typed)
            {
                Stretch(plate, inner, laid, typed);
                // The caret is drawn from the last laying-out of the text, and the
                // box it is laid out in has just changed size: without this the
                // caret is a line behind where the letters are.
                typing.ForceLabelUpdate();
            });
            field.onEndEdit.AddListener(delegate(string typed)
            {
                Written(me, mine, typed);
            });
        }

        /// <summary>A comment's plate taken to the size of what is written in it,
        /// without drawing the board again -- which would take the box out from
        /// under the hand typing into it.</summary>
        private void Stretch(GameObject body, RectTransform inner,
                             Text drawn, string words)
        {
            float wide;
            float tall;
            Measure(drawn, words, out wide, out tall);
            RectTransform rect = body.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(wide, tall);
            if (inner != null)
            {
                inner.sizeDelta = new Vector2(wide - NoteEdge * 2f,
                                              tall - NoteEdge * 2f);
            }
            // Both dashed edges -- the faint one and the one that says picked out
            // -- are children of the plate and stretch with it, so what is left is
            // how many dashes they carry.
            for (int i = 0; i < body.transform.childCount; i++)
            {
                Transform kid = body.transform.GetChild(i);
                if (kid.name == "Dashes")
                {
                    Dashes(kid.gameObject, wide + 4f, tall + 4f);
                }
            }
        }

        /// <summary>
        /// One of a comment's two labels, laid out to fill its field exactly.
        ///
        /// The prefab insets its own label, and text that wraps at one width while
        /// being measured at another drops its last word onto the next line. Best
        /// fit off for the same reason: it lays the text out at a size nothing else
        /// knows about.
        /// </summary>
        private static void Inked(Text label, Color colour)
        {
            if (label == null)
            {
                return;
            }
            UIF.Style(label, colour, TextAnchor.UpperLeft);
            label.resizeTextForBestFit = false;
            label.fontSize = 14;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(TextEdge, TextEdge);
            rect.offsetMax = new Vector2(-TextEdge, -TextEdge);
        }

        private void Written(int node, Place place, string typed)
        {
            string said = typed == null ? "" : typed;
            if (said == place.Words)
            {
                return;
            }
            place.Words = said;
            // The plate is as big as what is written on it, so this is a redraw
            // rather than a repaint.
            Kept();
            Redraw();
        }

        /// <summary>An end of the board rebound by hand.</summary>
        private void Rebind(int node, Place place, KeyCell cell)
        {
            string was = place.Variable;
            KeyCode wasKey = place.Key;
            string wanted = cell.UsesVariable
                && !string.IsNullOrEmpty(cell.Variable) ? cell.Variable : null;
            KeyCode wantedKey = cell.UsesVariable ? KeyCode.None : cell.Code;
            Wants(node, cell.UsesVariable && wanted == null);
            if (wanted == null && wantedKey == KeyCode.None)
            {
                // An emptied cell is a cell waiting to be filled, not an end being
                // unbound: switching one from a key to a name clears it on the way
                // past, and pushing that through cut every wire on the end before
                // the new name had been typed. What it stands for changes when
                // something is typed, and not before.
                return;
            }
            if (Doubled(node, place.Kind, wanted, wantedKey))
            {
                Warned(cell.transform as RectTransform, "already assigned!");
                // Another end already stands for this. Two ends on one binding are
                // one end drawn twice -- every wire either appears to have is the
                // same wire -- so the name is not taken and the cell goes back to
                // saying what this end actually is.
                cell.Load(Shown(node, was, wasKey), wasKey);
                return;
            }
            place.Variable = wanted;
            place.Key = wantedKey;
            if (was == place.Variable && wasKey == place.Key)
            {
                // Which of the two it is, and nothing else. Drawing the board again
                // for that took the cell out from under the hand that had just
                // clicked it.
                return;
            }

            // Everything that was wired to it follows, or the wire silently comes
            // apart the moment the name changes -- unless the name it has been
            // given is one something else on the board already answers to. Then
            // moving the wires would land them on that node as well as this one,
            // which is a wire nobody drew: the rename is this end's own business
            // and the rest of the board is left where it is.
            bool carry = !Elsewhere(node, place.Variable, place.Key);
            List<MapperType> touched = new List<MapperType>();
            if (carry && place.Kind == Place.Output)
            {
                // The wires into an output are rows *pressing* its name, which is
                // the other half of the table from a row's inputs. Without this an
                // output renamed by hand kept its wires on screen for a frame and
                // came back with none of them.
                for (int i = 0; i < Rows; i++)
                {
                    Repoint(i, was, wasKey, place, touched);
                }
            }
            for (int i = 0; carry && i < Rows; i++)
            {
                LogicRow row = Row(i);
                if (row == null || !row.Ready)
                {
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    MKey input = port == 0 ? row.InputA : row.InputB;
                    bool mine = was != null ? Bindings.Holds(input, was)
                                            : Bindings.Holds(input, wasKey);
                    if (!mine)
                    {
                        continue;
                    }
                    // The one name this end stood for is swapped for the one it
                    // stands for now; anything else on the same input is another
                    // end's wire and stays where it is. A name cannot join an input
                    // that answers to keys, or the other way about, so where the
                    // new one will not go the old one simply comes off.
                    if (was != null)
                    {
                        Bindings.Dropped(input, was);
                    }
                    else
                    {
                        Bindings.Dropped(input, wasKey);
                    }
                    if (place.Variable != null)
                    {
                        Bindings.Added(input, place.Variable);
                    }
                    else
                    {
                        Bindings.Added(input, place.Key);
                    }
                    touched.Add(input);
                }
            }
            Commit(touched);
            Redraw();
        }

        /// <summary>
        /// Whether another node of the same sort already stands for this binding.
        ///
        /// Two ends on one name are one end drawn twice: the wires belong to
        /// whichever is found first and the other is drawn bare. A gate and an end
        /// sharing a name is a different matter -- that is the wire between them.
        /// </summary>
        /// <param name="kind">The kind of end being bound.</param>
        private bool Doubled(int node, int kind, string variable, KeyCode key)
        {
            if (variable == null && key == KeyCode.None)
            {
                return false;
            }
            for (int i = 0; i < Nodes; i++)
            {
                if (i == node)
                {
                    continue;
                }
                Place place = Placed(i);
                bool end = place != null && (place.Kind == Place.Input
                                             || place.Kind == Place.Output);
                // Two ends never share a binding, whichever kinds they are. An
                // output renamed to what an input stands for is not two nodes with
                // the same name on a drawing: the rows pressing that output really
                // do drive everything reading that input, so the board would draw a
                // wire from each of them to each of those -- wiring nobody did, out
                // of a rename.
                //
                // A gate's answer is a different matter for an output, because an
                // output holding a gate's name is exactly how that gate feeds it.
                // An input taking one is the same trap as above, so it is refused.
                bool clash = end || (place == null && kind == Place.Input);
                if (!clash)
                {
                    continue;
                }
                string other;
                KeyCode otherKey;
                Answer(i, out other, out otherKey);
                if (variable != null ? Carries(other, variable)
                                     : (other == null && otherKey == key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether anything else on the board already answers to this binding.
        ///
        /// Two nodes standing for one name are one thing drawn twice, as far as the
        /// wires are concerned -- so an edit that moves wires onto a name already in
        /// use wires them to that node too. Asked before any wire is moved.
        /// </summary>
        private bool Elsewhere(int node, string variable, KeyCode key)
        {
            if (variable == null && key == KeyCode.None)
            {
                return false;
            }
            for (int i = 0; i < Nodes; i++)
            {
                if (i == node)
                {
                    continue;
                }
                string other;
                KeyCode otherKey;
                Answer(i, out other, out otherKey);
                bool same = variable != null
                    ? Carries(other, variable)
                    : (other == null && otherKey == key);
                if (same)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A row that pressed an end presses whatever that end stands for now.
        ///
        /// A Besiege key answers either the keyboard or a list of names, never
        /// both, so an end turned from a name into a key takes the key only when
        /// the row was pressing nothing else -- a row wired to two outputs keeps
        /// the names, and the one that became a key loses that wire rather than the
        /// other one losing its.
        /// </summary>
        private void Repoint(int node, string was, KeyCode wasKey, Place place,
                             List<MapperType> touched)
        {
            LogicRow row = Row(node);
            if (row == null || !row.Ready)
            {
                return;
            }
            string had = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            if (was != null)
            {
                if (!Carries(had, was))
                {
                    return;
                }
                string[] names = had.Split(';');
                string left = "";
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].Trim() == was)
                    {
                        continue;
                    }
                    left += left.Length == 0 ? names[i] : ";" + names[i];
                }
                if (place.Variable != null)
                {
                    left = left.Length == 0 ? place.Variable
                                            : left + ";" + place.Variable;
                    Bindings.BindVariable(row.Emulate, left);
                }
                else if (left.Length == 0)
                {
                    Bindings.Bind(row.Emulate, place.Key);
                }
                else
                {
                    Bindings.BindVariable(row.Emulate, left);
                }
                touched.Add(row.Emulate);
                return;
            }
            if (wasKey == KeyCode.None || had != null
                || Bindings.Code(row.Emulate) != wasKey)
            {
                return;
            }
            if (place.Variable != null)
            {
                Bindings.BindVariable(row.Emulate, place.Variable);
            }
            else
            {
                Bindings.Bind(row.Emulate, place.Key);
            }
            touched.Add(row.Emulate);
        }

        private Vector2 Where(int node)
        {
            Place place = Placed(node);
            return place != null ? new Vector2(place.X, place.Y) : Board.Spot(node);
        }

        /// <summary>Puts a node somewhere, and takes what is drawn of it with the
        /// number that is saved.</summary>
        private void Put(int node, Vector2 now)
        {
            Move(node, now);
            // Where it actually went, which is not always where it was put: the
            // grid moves it to an intersection and the fence keeps it on the board,
            // and what is drawn should be what is written down.
            Vector2 at = Where(node);
            if (node >= 0 && node < parts.Count && parts[node] != null)
            {
                RectTransform rect = parts[node].GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(at.x, -at.y);
            }
        }

        /// <summary>A place kept inside the board: see <see cref="BoardWide"/>.
        /// The node's own corner, with room left for the node itself.</summary>
        private static Vector2 Fenced(Vector2 at)
        {
            return new Vector2(Mathf.Clamp(at.x, 0f, BoardWide - NodeWidth),
                               Mathf.Clamp(at.y, 0f, BoardTall - NodeHeight));
        }

        private void Move(int node, Vector2 to)
        {
            to = Fenced(Snapped(to));
            Place place = Placed(node);
            if (place != null)
            {
                place.X = to.x;
                place.Y = to.y;
                return;
            }
            Board.Put(node, to);
        }

        /// <summary>
        /// One port: a circle that arms a wire or lands one.
        ///
        /// Drawn rather than spawned. A UI Factory button behind the circle was a
        /// plate the eye had to look past to see the one thing that matters -- and
        /// three of them a node is three prefabs a node, which is most of what
        /// opening a board cost. What is left is a transparent square that answers
        /// the pointer, wider than the circle so a wire is something to grab.
        /// </summary>
        private RectTransform Port(Transform host, float x, float y, bool output,
                                   int node, int port)
        {
            GameObject go = new GameObject("Port");
            go.transform.SetParent(host, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            float slack = (PortReach - PortSize) * 0.5f;
            UIF.Fit(rect, x - slack, y - slack, PortReach, PortReach);
            Image catcher = go.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);

            // Empty until a wire lands on it, which is the one thing about a port
            // worth seeing from across the board.
            Place sitting = Placed(node);
            bool wired;
            if (output)
            {
                wired = Heard(node);
            }
            else if (sitting != null && sitting.Kind == Place.Output)
            {
                wired = false;
                for (int i = 0; i < Rows && !wired; i++)
                {
                    wired = Presses(i, sitting);
                }
            }
            else
            {
                wired = Feeding(node, port) >= 0;
            }
            Picture(go.transform, wired ? Glyphs.Dot : Glyphs.Ring, PortSize);

            // Both ways of wiring: press and drag from one port to another, which
            // is what a hand reaches for, and click one then the other, which is
            // what a hand that has already let go can still do.
            // A port takes the drag that starts on it, so it carries the pan as
            // well or the board is dead to the middle button over a dozen small
            // squares.
            Waving(go);

            PortMark mark = go.AddComponent<PortMark>();
            mark.Node = node;
            mark.Port = port;
            mark.Output = output;
            mark.Pulling = Pulling;
            mark.Landed = Landed;
            mark.Clicked = delegate(PortMark one)
            {
                // With control held the click was for picking the node out.
                if (!Picking())
                {
                    Touched(one.Node, one.Port, one.Output);
                }
            };
            return rect;
        }

        /// <summary>Where the wire being pulled ends now, in the sheet's own
        /// coordinates, or nowhere.</summary>
        private bool pulling;
        private Vector2 pullTo;
        private PortMark pullFrom;

        /// <summary>
        /// The wire in hand.
        ///
        /// A port that holds one wire hands it over when it is dragged: the wire
        /// comes off there and then -- as it does in any other editor -- and its
        /// loose end follows the pointer until it is let go of. <see cref="carried"/>
        /// is the node whose answer is still on the far end of it.
        ///
        /// A port that feeds several is a different question: which of them is
        /// being moved, if any. <see cref="held"/> is the one the pointer is
        /// running along, and it is not touched until the drag ends -- a drag that
        /// wandered off on its own is somebody drawing a new wire and changing
        /// their mind, and that must leave the board as it was.
        /// </summary>
        private int carried = -1;

        private int held = -1;
        private int heldPort = -1;

        /// <summary>
        /// How near the pointer has to be to an existing wire to be moving it, and
        /// how far it has to stray to be drawing a new one. Two numbers rather than
        /// one, so a hand hovering at the boundary does not flicker between them.
        ///
        /// All the reaches here are in the window's own units, as the hand sees
        /// them -- see <see cref="Near"/>. The board is zoomed and the hand is not.
        /// </summary>
        private const float Grab = 26f;
        private const float Free = 54f;

        /// <summary>
        /// How far a drag has to leave the port before any of that port's wires is
        /// taken to be the one in hand.
        ///
        /// Every wire out of an answer starts at the same point, so within a few
        /// pixels of it they are all equally near and the choice would be a
        /// toss-up changing its mind every frame. Just past the port they have
        /// fanned apart and the nearest is the one the hand set off along. Only
        /// while nothing is in hand: a wire already taken can be carried back over
        /// the port it came from without being dropped.
        /// </summary>
        private const float Decide = 16f;

        /// <summary>How near a port a wire in hand has to be let go to be put back
        /// on it: wider than the usual reach, because a wire dropped where it came
        /// from is a hand that changed its mind rather than one aiming.</summary>
        private const float Home = PortSize * 2f;

        /// <summary>
        /// A reach meant in the window's units, given in the board's.
        ///
        /// Everything a wire drag measures -- how near a wire is, how near a port
        /// is -- is worked out in the board's own coordinates, which the zoom
        /// scales. Left at that, a reach of twenty-two is twenty-two pixels at one
        /// zoom and nine at another, and putting a wire back where it came from
        /// stopped working the moment the view was pulled back. What the hand has
        /// to do is the same at every zoom.
        /// </summary>
        private float Near(float window)
        {
            float much = content == null ? 1f : content.localScale.x;
            return much > 0.001f ? window / much : window;
        }

        private void Pulling(PortMark from, Vector2 screen, bool began)
        {
            if (began)
            {
                carried = -1;
                held = -1;
                heldPort = -1;
            }
            pullFrom = from;
            pulling = true;
            Vector2 local;
            // The content's coordinates, not the board's: the wire is drawn on the
            // content, which is panned and scaled inside the board. Measuring
            // against the board put the loose end wherever the pan had got to, and
            // further out the more it was zoomed.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                pullTo = local;
            }
            if (from.Output)
            {
                Along(from);
            }
            else
            {
                Beside(from);
            }
            Strings();
        }

        /// <summary>
        /// Which wire into a port a drag off it has hold of.
        ///
        /// The mirror of <see cref="Along"/>. A gate's input holds one wire and
        /// that is the one; an output end is fed by as many rows as press its name,
        /// and since they all arrive at the same point the one in hand is whichever
        /// the pointer is running along -- asked again every frame, so leaving one
        /// for another shows while the drag is going on.
        ///
        /// Nothing is written here. The wire is taken off at the drop and not at
        /// the press, which is what lets a hand that changes its mind put it back
        /// without an edit ever having happened.
        /// </summary>
        private void Beside(PortMark from)
        {
            RectTransform sink = Held(from.Node * 3 + 1 + from.Port);
            if (sink == null)
            {
                carried = -1;
                return;
            }
            Vector2 start = Middle(sink);
            Place end = Placed(from.Node);
            bool ended = end != null && end.Kind == Place.Output;
            if (!ended)
            {
                Feeds(from.Node, from.Port, strands);
            }
            int only = -1;
            int count = 0;
            int nearest = -1;
            float best = 0f;
            for (int node = 0; node < Nodes; node++)
            {
                bool mine = ended ? node < Rows && Presses(node, end)
                                  : strands.Contains(node);
                if (!mine)
                {
                    continue;
                }
                count++;
                only = node;
                RectTransform answer = Held(node * 3);
                if (answer == null)
                {
                    continue;
                }
                float off = Aside(pullTo, start, Middle(answer));
                if (nearest < 0 || off < best)
                {
                    nearest = node;
                    best = off;
                }
            }
            if (count == 0)
            {
                carried = -1;               // nothing on it: a new wire is drawn
                return;
            }
            if (count == 1)
            {
                carried = only;             // one wire, and it is the one in hand
                return;
            }
            if (carried < 0 && (pullTo - start).magnitude < Near(Decide))
            {
                return;                     // still on the port, where they meet
            }
            if (nearest >= 0 && best <= Near(Grab))
            {
                carried = nearest;
                return;
            }
            if (carried < 0)
            {
                return;
            }
            RectTransform taken = Held(carried * 3);
            if (taken == null || Aside(pullTo, start, Middle(taken)) > Near(Free))
            {
                carried = -1;
            }
        }

        /// <summary>
        /// Whether a drag out of an answer is running along one of the wires
        /// already on it -- which is that wire being moved -- or away from all of
        /// them, which is a new wire being drawn.
        /// </summary>
        private void Along(PortMark from)
        {
            RectTransform answer = Held(from.Node * 3);
            if (answer == null)
            {
                return;
            }
            Vector2 start = Middle(answer);
            int nearest = -1;
            int port = -1;
            float best = 0f;
            // The far end the pointer is actually on, if it is on one.
            int home = -1;
            int homePort = -1;
            float homeOff = 0f;
            for (int node = 0; node < Nodes; node++)
            {
                Place end = Placed(node);
                bool ended = end != null && end.Kind == Place.Output;
                for (int i = 0; i < (ended ? 1 : 2); i++)
                {
                    // An output end may be pressed by several rows at once, and
                    // each of those is a wire of its own; `Feeding` names only the
                    // first of them.
                    bool mine = ended
                        ? from.Node < Rows && Presses(from.Node, end)
                        : Feeding(node, i) == from.Node;
                    if (!mine)
                    {
                        continue;
                    }
                    RectTransform sink = Held(node * 3 + 1 + i);
                    if (sink == null)
                    {
                        continue;
                    }
                    Vector2 landing = Middle(sink);
                    float off = Aside(pullTo, start, landing);
                    if (nearest < 0 || off < best)
                    {
                        nearest = node;
                        port = i;
                        best = off;
                    }
                    // And whether the pointer is on that wire's own far end, which
                    // is a stronger claim than being near its line.
                    float back = (landing - pullTo).magnitude;
                    if (back <= Near(Home) && (home < 0 || back < homeOff))
                    {
                        home = node;
                        homePort = i;
                        homeOff = back;
                    }
                }
            }
            if (nearest < 0)
            {
                held = -1;                  // nothing on this port to have hold of
                heldPort = -1;
                return;
            }
            if (home >= 0)
            {
                // The pointer is on the far end of one of these wires, near enough
                // to land on it. That wire is the one in hand whatever the lines
                // say: a hand that has brought a wire back to where it came from is
                // reconnecting it, and letting another wire that happens to pass
                // close by take its place is how the wrong one came off.
                held = home;
                heldPort = homePort;
                return;
            }
            if (held < 0 && (pullTo - start).magnitude < Near(Decide))
            {
                // Still on the port. Every wire out of it starts at this same
                // point, so they are all equally near and picking one would be a
                // toss-up that changed its mind every frame.
                return;
            }
            if (best <= Near(Grab))
            {
                // On a wire: that one is in hand, and it is asked again every frame
                // -- a hand that leaves one wire and finds another has changed its
                // mind, and the board should say so while the drag is going on
                // rather than at the end of it.
                held = nearest;
                heldPort = port;
                return;
            }
            if (held < 0)
            {
                return;                     // drawing a new one, and still is
            }
            // Off the one in hand: let go of it only when the hand has strayed
            // further than it took to pick it up, so hovering at the boundary does
            // not flicker -- and measured against that wire rather than whichever
            // is nearest, or a drag passing another wire keeps hold of the wrong
            // one.
            RectTransform taken = Held(held * 3 + 1 + heldPort);
            if (taken == null || Aside(pullTo, start, Middle(taken)) > Near(Free))
            {
                held = -1;
                heldPort = -1;
            }
        }

        /// <summary>How far a point lies off the line between two others.</summary>
        private static float Aside(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 span = to - from;
            float length = span.sqrMagnitude;
            if (length < 0.001f)
            {
                return (point - from).magnitude;
            }
            float t = Mathf.Clamp01(Vector2.Dot(point - from, span) / length);
            return (point - (from + span * t)).magnitude;
        }

        private void Landed(PortMark from, PortMark to, Vector2 screen)
        {
            pulling = false;
            pullFrom = null;
            pending = -1;
            // Where the pointer let go, rather than what happened to be on top
            // there: ports answer to more of the board than they draw, so two
            // nodes side by side have overlapping targets and the wire went to
            // whichever was drawn last rather than the one it was dropped on.
            // A wire in hand is put back on a port it is let go anywhere near: it
            // was taken off one, and a hand that changed its mind should not have
            // to aim to undo that.
            // Which kind of port the loose end is looking for: the opposite of
            // whatever is holding the other end of it. A wire lifted off an input
            // and one moved off an answer are both looking for an input -- and
            // without this the search took in the answer they are still attached
            // to, which sits nearer the pointer than the port they came off as soon
            // as the hand starts back towards it, so the wire landed on the end
            // that had not moved and came off instead of going back on.
            bool wants = carried < 0 && held < 0 && !from.Output;
            to = Nearest(to, Near(Home), wants);
            if (served == null)
            {
                Strings();
                return;
            }
            if (carried >= 0)
            {
                Landing(from, to);
                return;
            }
            if (from.Output && held >= 0)
            {
                Moving(from, to);
                return;
            }
            if (to != null && from.Node != to.Node && from.Output != to.Output)
            {
                // Either way round: pulled from the answer to the input, or from
                // the input back to the answer.
                int source = from.Output ? from.Node : to.Node;
                PortMark sink = from.Output ? to : from;
                if (Wired(source, sink.Node, sink.Port))
                {
                    // The two are wired together already and this drag has hold of
                    // nothing, so there is nothing to do: taking the wire off here
                    // would be cutting one the hand never picked up, which is how
                    // a wire came off while somebody was putting another one back.
                    Strings();
                    return;
                }
                // Join redraws: a port is empty or full, and which it is has just
                // changed.
                Join(source, sink.Node, sink.Port);
                return;
            }
            if (to == null)
            {
                // A wire drawn out to nowhere: the list of what could go on the
                // other end of it, where the wire was let go. Nothing is changed
                // unless something is picked from it -- an answer feeding three
                // gates does not lose one because a hand set out to draw a fourth
                // and thought better of it.
                Offering(from, screen);
                return;
            }
            if (from.Node != to.Node)
            {
                // Two ports of the same sort: an answer let go over an answer, or
                // an input over an input. A wire runs from one to the other, so
                // there is nothing to be made of this but a word about it.
                Told(to.Node, to.Output ? 0 : 1 + to.Port,
                     "has to connect\nto an input");
            }
            Strings();
        }

        /// <summary>
        /// A wire let go of over nothing: what would you like on the end of it?
        ///
        /// The same list a right-click offers, and whatever is picked is made where
        /// the wire was dropped and wired to the port it came from -- which is the
        /// gesture every other editor has, and the reason a wire pulled into space
        /// is worth anything at all.
        /// </summary>
        private void Offering(PortMark from, Vector2 screen)
        {
            if (served == null || content == null)
            {
                Strings();
                return;
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                Strings();
                return;
            }
            Vector2 at = new Vector2(local.x - NodeWidth * 0.5f,
                                     -local.y - NodeHeight * 0.5f);
            List<string> names = Offered();
            int source = from.Node;
            int port = from.Port;
            bool answering = from.Output;
            Choices.OpenAt(screen, names, delegate(string picked)
            {
                int which = names.IndexOf(picked);
                if (answering && Row(source) == null
                    && Kinded(which) == Place.Output)
                {
                    // An end at both ends of the wire, with no row to carry it --
                    // see `Join`. Said before the node is made rather than after,
                    // so a refusal does not leave one standing on the board.
                    Told(source, 0, "cannot directly\nconnect to output");
                    return;
                }
                // The rows are numbered first and the ends after them, so making
                // a gate moves every end along one -- and the port this wire came
                // out of may be one of those. Without this, a wire drawn out of an
                // input end and finished off with a new gate landed on whichever
                // end had taken over its number.
                int was = Rows;
                int born = Made(which, at);
                if (born < 0)
                {
                    return;
                }
                if (source >= was)
                {
                    source += Rows - was;
                }
                if (answering)
                {
                    // Out of an answer and into whatever was made, if it takes one.
                    Place end = Placed(born);
                    if (end == null || end.Kind == Place.Output)
                    {
                        Join(source, born, 0);
                    }
                }
                else
                {
                    Place end = Placed(born);
                    if (end == null || end.Kind == Place.Input)
                    {
                        Join(born, source, port);
                    }
                }
            });
            Strings();
        }

        /// <summary>Everything that can be put on the board, in the order the
        /// palette has them.</summary>
        private List<string> Offered()
        {
            List<string> names = new List<string>();
            names.Add("INPUT");
            names.Add("OUTPUT");
            names.Add("COMMENT");
            for (int g = 0; g < Gates.Count; g++)
            {
                names.Add(Gates.Names[g]);
            }
            return names;
        }

        /// <summary>Makes what that list's nth entry stands for, and says which
        /// node it became.</summary>
        private int Made(int which, Vector2 at)
        {
            if (which < 0)
            {
                return -1;
            }
            int kind = Kinded(which);
            return Born(kind, kind < 0 ? which - 3 : -1, at, true);
        }

        /// <summary>What that list's nth entry is: one of the two ends, a comment,
        /// or a gate -- which is -1, with the gate's own number three along.
        /// </summary>
        private static int Kinded(int which)
        {
            if (which == 0)
            {
                return Place.Input;
            }
            if (which == 1)
            {
                return Place.Output;
            }
            return which == 2 ? Place.Note : -1;
        }

        /// <summary>
        /// The port the pointer actually let go over: the nearest one to it within
        /// a port's own reach, or what the raycast found if none is near.
        ///
        /// Two ports that overlap are two answers to "what is under the pointer",
        /// and the raycast gives the one drawn last. Distance gives the one
        /// somebody aimed at.
        /// </summary>
        /// <param name="answers">Whether what is wanted is a node's answer rather
        /// than one of its inputs. A port of the other sort is no use to the wire in
        /// hand, and one of them is always nearer than it looks.</param>
        private PortMark Nearest(PortMark found, float reach, bool answers)
        {
            RectTransform closest = null;
            float best = 0f;
            for (int i = 0; i < ports.Count; i++)
            {
                // Three ports to a node, the answer first.
                if (ports[i] == null || (i % 3 == 0) != answers)
                {
                    continue;
                }
                float off = (Middle(ports[i]) - pullTo).magnitude;
                if (closest == null || off < best)
                {
                    closest = ports[i];
                    best = off;
                }
            }
            if (closest == null || best > reach)
            {
                return found;
            }
            PortMark mark = closest.GetComponent<PortMark>();
            return mark != null ? mark : found;
        }

        /// <summary>
        /// A wire that came off an input let go of: onto another input, which is
        /// where it goes now, or onto anything else, which leaves it off.
        ///
        /// The cut was written when the drag began, so this is the other half of
        /// one edit: whichever way it ends, one step of the undo.
        /// </summary>
        private void Landing(PortMark from, PortMark to)
        {
            int source = carried;
            carried = -1;
            if (source < 0 || from == null)
            {
                Strings();
                return;
            }
            if (to != null && to.Node == from.Node && to.Port == from.Port)
            {
                // Put back where it came from, which costs nothing: the wire is
                // taken off at the drop, so up to this moment it was never off at
                // all. Nothing to commit and nothing to undo.
                Strings();
                return;
            }
            List<MapperType> touched = new List<MapperType>();
            Unwire(source, from.Node, from.Port, touched);
            if (to != null && !to.Output && to.Node != source
                && !Wired(source, to.Node, to.Port))
            {
                // Onto another input: the wire moved rather than came off, and the
                // two halves of that are one edit.
                Join(source, to.Node, to.Port, touched);
                return;
            }
            if (to != null && to.Output && to.Node != from.Node)
            {
                // Onto another answer: this port reads that one now, which is the
                // same move seen from the other end of the wire.
                Join(to.Node, from.Node, from.Port, touched);
                return;
            }
            Commit(touched);
            Redraw();
        }

        /// <summary>
        /// One of an answer's wires, dragged along itself and let go of: onto
        /// another answer, which is where it comes from now, or onto nothing, which
        /// takes it off.
        /// </summary>
        private void Moving(PortMark from, PortMark to)
        {
            int node = held;
            int port = heldPort;
            held = -1;
            heldPort = -1;
            if (node < 0)
            {
                Strings();
                return;
            }
            if (to != null && to.Node == node && to.Port == port)
            {
                // Put back on the port it came off. Nothing was ever taken off --
                // the wire is cut at the drop, not at the press -- so there is
                // nothing to do but draw it where it always was.
                Strings();
                return;
            }
            if (to != null && to.Output && to.Node == from.Node)
            {
                Strings();
                return;                     // back on the answer it came out of
            }
            // From here the wire in hand comes off, and it is the only wire this
            // drag can touch: a drop is never allowed to cut something the hand was
            // not holding.
            List<MapperType> touched = new List<MapperType>();
            Unwire(from.Node, node, port, touched);
            if (to != null && !to.Output && to.Node != from.Node
                && !Wired(from.Node, to.Node, to.Port))
            {
                Join(from.Node, to.Node, to.Port, touched);
                return;                     // moved onto another input
            }
            if (to != null && to.Output && to.Node != from.Node)
            {
                Join(to.Node, node, port, touched);
                return;                     // and answered by another gate
            }
            Commit(touched);
            Redraw();
        }

        /// <summary>Whether this exact wire is on the board already: the answer of
        /// one node carrying what a port of another reads.</summary>
        private bool Wired(int from, int to, int port)
        {
            Place end = Placed(to);
            if (end != null)
            {
                return end.Kind == Place.Output && from < Rows && Presses(from, end);
            }
            Feeds(to, port, joined);
            return joined.Contains(from);
        }

        /// <summary>What feeds one port while that question is being answered --
        /// its own list, because the asker may be walking another.</summary>
        private readonly List<int> joined = new List<int>();

        /// <summary>Takes that wire off, into a list somebody else commits. An
        /// output end is fed by rows pressing its name; anything else by a row's
        /// input reading one.</summary>
        private void Unwire(int from, int to, int port, List<MapperType> touched)
        {
            Place end = Placed(to);
            if (end != null && end.Kind == Place.Output)
            {
                Drops(from, end, touched);
                return;
            }
            LogicRow row = Row(to);
            if (row == null || !row.Ready)
            {
                return;
            }
            // One wire off a port that may hold several: whatever the node at the
            // other end answers to comes off the list, and the rest of the list
            // stays where it is.
            MKey input = port == 0 ? row.InputA : row.InputB;
            string said;
            KeyCode code;
            Answer(from, out said, out code);
            if (said != null)
            {
                List<string> names = Bindings.Named(said);
                for (int i = 0; i < names.Count; i++)
                {
                    Bindings.Dropped(input, names[i]);
                }
            }
            else
            {
                Bindings.Dropped(input, code);
            }
            touched.Add(input);
        }

        /// <summary>
        /// A port was clicked: an output arms the wire, an input lands it.
        ///
        /// A click takes nothing off. Unwiring is a wire dragged off its port --
        /// which is where a hand reaches for it anyway, and a click that undid a
        /// connection was too easy to do by accident on a port the size of this
        /// one.
        /// </summary>
        private void Touched(int node, int port, bool output)
        {
            if (served == null)
            {
                return;
            }
            if (output)
            {
                pending = pending == node ? -1 : node;
                Strings();
                return;
            }
            if (pending < 0)
            {
                return;
            }
            int armed = pending;
            pending = -1;
            Join(armed, node, port);
        }

        /// <summary>
        /// Which wire is which, as three numbers each: the node that answers it,
        /// the node that reads it, and which of that node's inputs.
        ///
        /// Worked out when the board is drawn rather than when it is strung. A wire
        /// is a name, and finding the node behind a name means asking every node
        /// what it answers to and comparing lists of strings -- for every port on
        /// the board, and `Strings` runs on every frame of a drag, a pan and a
        /// zoom. The graph only changes when the board is drawn again, and that is
        /// where this is filled.
        /// </summary>
        private readonly List<int> wired = new List<int>();

        /// <summary>What feeds one port while that is being written down.</summary>
        private readonly List<int> strands = new List<int>();

        private void Wired()
        {
            wired.Clear();
            if (served == null)
            {
                return;
            }
            Sourced();
            try
            {
                Wires();
            }
            finally
            {
                Unsourced();
            }
        }

        /// <summary>The wires themselves, with the index up.</summary>
        private void Wires()
        {
            int many = Nodes;
            for (int node = 0; node < many; node++)
            {
                Place place = Placed(node);
                if (place != null && place.Kind == Place.Output)
                {
                    // An output end takes as many wires as press its name -- three
                    // gates all raising "door" is three wires into one end.
                    for (int from = 0; from < Rows; from++)
                    {
                        if (Presses(from, place))
                        {
                            wired.Add(from);
                            wired.Add(node);
                            wired.Add(0);
                        }
                    }
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    // As many as are wired to it: a gate's input answers to a list
                    // of names, and Besiege holds it while any of them is raised.
                    Feeds(node, port, strands);
                    for (int i = 0; i < strands.Count; i++)
                    {
                        wired.Add(strands[i]);
                        wired.Add(node);
                        wired.Add(port);
                    }
                }
            }
        }

        /// <summary>Draws every wire: a thin plate from one port to the other,
        /// turned to point at it.</summary>
        private void Strings()
        {
            // The pieces already made are moved rather than thrown away: this runs
            // on every frame of a drag, a pan and a zoom, and a curve is a dozen
            // pieces a wire. Building them again each time was a hundred objects a
            // frame made and destroyed.
            strung = 0;
            if (served == null || sheet == null)
            {
                Spare();
                return;
            }
            if (pulling && pullFrom != null)
            {
                // What the loose end hangs from: the node that answers a wire taken
                // off an input, the far end of one being moved off an answer, or
                // the port a new wire is being drawn out of.
                bool answering = carried >= 0 || (pullFrom.Output && held < 0);
                int fixedEnd = carried >= 0 ? carried * 3
                    : (held >= 0 ? held * 3 + 1 + heldPort
                        : (pullFrom.Output ? pullFrom.Node * 3
                                           : pullFrom.Node * 3 + 1 + pullFrom.Port));
                RectTransform end = Held(fixedEnd);
                if (end != null)
                {
                    // Always drawn from the answer end: a curve leaves one end
                    // sideways and arrives at the other sideways, so drawn the
                    // other way round it bows backwards.
                    if (answering)
                    {
                        String(Middle(end), pullTo, LiveInk);
                    }
                    else
                    {
                        String(pullTo, Middle(end), LiveInk);
                    }
                }
            }

            // Every wire there is, as it was worked out when the board was last
            // drawn.
            for (int i = 0; i + 2 < wired.Count; i += 3)
            {
                int from = wired[i];
                int node = wired[i + 1];
                int port = wired[i + 2];
                if (held == node && heldPort == port && pullFrom != null
                    && from == pullFrom.Node)
                {
                    continue;               // in hand, and drawn under the pointer
                }
                if (carried == from && pullFrom != null && node == pullFrom.Node
                    && port == pullFrom.Port)
                {
                    continue;               // the same, off the other end of it
                }
                Draw(from * 3, node * 3 + 1 + port, from == pending);
            }
            Spare();
        }

        /// <summary>Hides whatever pieces this drawing did not need.</summary>
        private void Spare()
        {
            for (int i = strung; i < wires.Count; i++)
            {
                if (wires[i] != null && wires[i].activeSelf)
                {
                    wires[i].SetActive(false);
                }
            }
        }

        /// <summary>One wire between two ports, if both are there.</summary>
        private void Draw(int from, int to, bool armed)
        {
            RectTransform start = Held(from);
            RectTransform end = Held(to);
            if (start != null && end != null)
            {
                String(Middle(start), Middle(end), armed ? LiveInk : WireInk);
            }
        }

        /// <summary>Whether a row's answer goes out under this end's name.</summary>
        private bool Presses(int row, Place place)
        {
            if (sourcing > 0)
            {
                // A row that is not ready answers to nothing and is not in the
                // index, so this is the same question asked the quick way.
                List<int> who = Answering(place.Variable, place.Key);
                return who != null && who.Contains(row);
            }
            LogicRow said = Row(row);
            if (said == null || !said.Ready)
            {
                return false;
            }
            string names = Bindings.IsVariable(said.Emulate)
                ? Bindings.Variable(said.Emulate) : null;
            if (place.Variable != null)
            {
                return Carries(names, place.Variable);
            }
            return names == null && place.Key != KeyCode.None
                && Bindings.Code(said.Emulate) == place.Key;
        }

        /// <summary>Takes down every wire there is. Only when the board itself is
        /// being drawn again -- between times they are reused.</summary>
        private void Cut()
        {
            for (int i = 0; i < wires.Count; i++)
            {
                if (wires[i] != null)
                {
                    // Switched off as well as destroyed: Destroy is deferred to the
                    // end of the frame, and a wire drawn over the one that replaced
                    // it is exactly the mess this is clearing up.
                    wires[i].SetActive(false);
                    Destroy(wires[i]);
                }
            }
            wires.Clear();
            strung = 0;
        }

        /// <summary>How many of the pieces are in use by the drawing being made.
        /// </summary>
        private int strung;

        private RectTransform Held(int at)
        {
            return at < 0 || at >= ports.Count ? null : ports[at];
        }

        /// <summary>Where a port is, in the coordinates the wires are drawn in --
        /// which are the content's, so a wire pans and scales with what it
        /// joins.</summary>
        private readonly Vector3[] edges = new Vector3[4];

        private Vector2 Middle(RectTransform port)
        {
            port.GetWorldCorners(edges);
            Vector3 middle = (edges[0] + edges[2]) * 0.5f;
            return content.InverseTransformPoint(middle);
        }

        /// <summary>
        /// One wire, in whichever of the three ways the switch says.
        ///
        /// All three are made of the same straight pieces: a curve is a handful of
        /// them along a bezier, and a square wire is three. Drawing a real curve
        /// would want a mesh of our own, and a wire is two pixels wide.
        /// </summary>
        private void String(Vector2 from, Vector2 to, Color colour)
        {
            if (style == Straight)
            {
                Piece(from, to, colour);
                return;
            }
            if (style == Square)
            {
                float middle = (from.x + to.x) * 0.5f;
                Piece(from, new Vector2(middle, from.y), colour);
                Piece(new Vector2(middle, from.y), new Vector2(middle, to.y), colour);
                Piece(new Vector2(middle, to.y), to, colour);
                return;
            }
            // A curve: the two ends leave sideways, which is what makes a bundle of
            // wires readable where straight ones would cross.
            float reach = Mathf.Max(30f, Mathf.Abs(to.x - from.x) * 0.5f);
            Vector2 outOf = from + new Vector2(reach, 0f);
            Vector2 into = to - new Vector2(reach, 0f);
            Vector2 last = from;
            for (int i = 1; i <= Curve; i++)
            {
                float t = i / (float)Curve;
                Vector2 next = Bend(from, outOf, into, to, t);
                Piece(last, next, colour);
                last = next;
            }
        }

        /// <summary>How many straight pieces a curve is drawn with.</summary>
        private const int Curve = 12;

        private static Vector2 Bend(Vector2 a, Vector2 b, Vector2 c, Vector2 d,
                                    float t)
        {
            float u = 1f - t;
            return a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t)
                 + d * (t * t * t);
        }

        private void Piece(Vector2 from, Vector2 to, Color colour)
        {
            GameObject go;
            Image line;
            if (strung < wires.Count && wires[strung] != null)
            {
                go = wires[strung];
                line = go.GetComponent<Image>();
                if (!go.activeSelf)
                {
                    go.SetActive(true);
                }
            }
            else
            {
                go = new GameObject("Wire");
                go.transform.SetParent(content, false);
                go.AddComponent<RectTransform>();
                line = go.AddComponent<Image>();
                line.raycastTarget = false;
                // Behind the nodes: a wire crossing a node should pass under it.
                go.transform.SetAsFirstSibling();
                wires.Add(go);
            }
            if (line != null)
            {
                line.color = colour;
            }

            Vector2 span = to - from;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(span.magnitude, WireWidth);
            rect.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            strung++;
        }

        // ---- what the buttons do ---------------------------------------------

        private void Born(int kind, int gate)
        {
            Born(kind, gate, Vector2.zero, false);
        }

        /// <summary>
        /// A new node.
        ///
        /// A gate is a row of the table, so it is added there and the board only
        /// remembers where it sits. The two ends are not rows and live in the
        /// layout beside them.
        /// </summary>
        private int Born(int kind, int gate, Vector2 where, bool placed)
        {
            if (served == null)
            {
                return -1;
            }
            // A node asked for rather than put somewhere lands near the middle of
            // the board, beside whatever is there already.
            Vector2 at = Fenced(Snapped(placed ? where
                : new Vector2(Centre.x - 2f * (NodeWidth + 30f)
                                  + (Nodes % 5) * (NodeWidth + 30f),
                              Centre.y - 60f
                                  + (Nodes / 5) * (NodeHeight + 20f))));
            if (gate >= 0)
            {
                int was = Rows;
                List<MapperType> touched = new List<MapperType>();
                int row = LogicTable.Add(served, touched);
                if (row < 0)
                {
                    // A gate is a row and the block holds thirty-two of them; see
                    // AGENTS.md for why that number cannot move.
                    Warned(sheet, "reached 32\ngates limit");
                    return -1;
                }
                LogicRow made = served.Rows[row];
                if (made.Ready)
                {
                    made.Kind.Value = gate;
                    touched.Add(made.Kind);
                    // A row copied from the one above it comes with that row's
                    // inputs; a node dropped on the board should arrive empty.
                    Bindings.Bind(made.InputA, KeyCode.None);
                    Bindings.Bind(made.InputB, KeyCode.None);
                    // Its answer is a name of the board's own making, given now
                    // rather than when a wire is drawn: a gate that answers to
                    // nothing is a gate nothing can be wired to.
                    Bindings.BindVariable(made.Emulate, Minted());
                    touched.Add(made.InputA);
                    touched.Add(made.InputB);
                    touched.Add(made.Emulate);
                }
                Board.Put(row, at);
                Shifted(was);
                Commit(touched);
                Rebuilt();
                return row;
            }
            Place place = new Place();
            place.Kind = kind;
            place.X = at.x;
            place.Y = at.y;
            Board.Places.Add(place);
            Kept();
            Redraw();
            return Rows + Board.Places.Count - 1;
        }

        /// <summary>Deletes a node: a row goes from the table, an end goes from the
        /// layout.</summary>
        private void Kill(int index)
        {
            if (served == null)
            {
                return;
            }
            List<MapperType> touched = new List<MapperType>();
            if (Removed(index, touched))
            {
                Shuffled(index);
                Commit(touched);
                Rebuilt();
            }
        }

        /// <summary>
        /// The selection after a row has been added.
        ///
        /// The rows are numbered first and the ends after them, so a row arriving
        /// moves every end along one and a number that stood for an end now stands
        /// for its neighbour. The same arithmetic as `Shuffled` below, the other
        /// way up.
        /// </summary>
        private void Shifted(int was)
        {
            int by = Rows - was;
            if (by == 0)
            {
                return;
            }
            for (int i = 0; i < picked.Count; i++)
            {
                if (picked[i] >= was)
                {
                    picked[i] = picked[i] + by;
                }
            }
            if (pending >= was)
            {
                pending += by;
            }
            for (int i = 0; i < naming.Count; i++)
            {
                if (naming[i] >= was)
                {
                    naming[i] = naming[i] + by;
                }
            }
        }

        /// <summary>
        /// The selection after one node has been taken off the board.
        ///
        /// A node is known by its number, and the numbers close up over the gap:
        /// the rows come first and the ends after them, so removing anything at all
        /// moves everything above it down one. Kept as they were, the numbers left
        /// in the selection picked out whatever had moved into them -- which is one
        /// node deleted and a different one selected in its place.
        /// </summary>
        private void Shuffled(int gone)
        {
            for (int i = picked.Count - 1; i >= 0; i--)
            {
                if (picked[i] == gone)
                {
                    picked.RemoveAt(i);
                }
                else if (picked[i] > gone)
                {
                    picked[i] = picked[i] - 1;
                }
            }
            // And the answer waiting for somewhere to go, which is a number of the
            // same kind: armed, then a node removed, and the wire it was going to
            // draw would have come out of whatever took its place.
            if (pending == gone)
            {
                pending = -1;
            }
            else if (pending > gone)
            {
                pending--;
            }
            for (int i = naming.Count - 1; i >= 0; i--)
            {
                if (naming[i] == gone)
                {
                    naming.RemoveAt(i);
                }
                else if (naming[i] > gone)
                {
                    naming[i] = naming[i] - 1;
                }
            }
        }

        /// <summary>One node's share of a removal, into a list somebody else
        /// commits.</summary>
        private bool Removed(int index, List<MapperType> touched)
        {
            Place place = Placed(index);
            if (place != null)
            {
                // An output's wires are rows pressing its name; an input's are rows
                // reading it. They go with the node -- a wire left bound to a node
                // that is gone is a wire nobody can see or cut -- but only if the
                // name is this node's alone. A gate reading what an output end is
                // called is wired to the *gate* that presses it, and taking that
                // name off would cut a wire the hand never touched.
                bool shared = place.Kind == Place.Input
                    ? Elsewhere(index, place.Variable, place.Key)
                    : Read(place.Variable, place.Key);
                for (int i = 0; !shared && i < Rows; i++)
                {
                    Drops(i, place, touched);
                    Unread(i, place, touched);
                }
                Board.Places.Remove(place);
                return true;
            }
            // The row and whatever was still wired to it, in one edit.
            LogicTable.Erase(served, index, touched);
            Board.Forget(index);
            return true;
        }

        /// <summary>The table changed shape, so whatever is drawing it has to be
        /// told: the panel rebuilds its rows and this redraws its board.</summary>
        private void Rebuilt()
        {
            Panel.Restack();
            Redraw();
        }

        /// <summary>The right-click menu: everything the palette holds, and the
        /// node lands where the click was.</summary>
        private void Asked(Vector2 screen)
        {
            if (served == null)
            {
                return;
            }
            List<string> names = Offered();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                local = Vector2.zero;
            }
            // The sheet's own corner is its top left and a node's y counts down
            // from it, so the y is turned over here.
            Vector2 at = new Vector2(local.x, -local.y);
            Choices.OpenAt(screen, names, delegate(string picked)
            {
                Made(names.IndexOf(picked), at);
            });
        }

        private InputField prefixBox;

        private void Renamed(string typed)
        {
            if (served == null)
            {
                return;
            }
            served.Prefix = typed;
            // The wires already named keep their names: a name is a machine-wide
            // thing and something else may be reading it. The prefix is what the
            // *next* generated one starts with.
            LogicTable.Apply(served.PrefixControl);
            Kept();
            Redraw();
        }

        /// <summary>Dragging the empty board moves what is on it.</summary>
        /// <summary>Whether one node feeds the other.</summary>
        private bool Between(int from, int to)
        {
            Place place = Placed(to);
            if (place != null && place.Kind == Place.Output)
            {
                return from < Rows && Presses(from, place);
            }
            return Wired(from, to, 0) || Wired(from, to, 1);
        }

        private void Panning(Vector2 by)
        {
            Panned(by);
        }

        /// <summary>
        /// The same, saying how far it actually went.
        ///
        /// Which is not always how far it was asked to go: the view is kept over
        /// the board, and a board being chased by a node dragged against its own
        /// edge has run out of room. Whoever is compensating for the pan -- the
        /// auto-pan holds the dragged node still against it -- has to know.
        /// </summary>
        private Vector2 Panned(Vector2 by)
        {
            if (content == null)
            {
                return Vector2.zero;
            }
            Vector2 was = content.anchoredPosition;
            content.anchoredPosition = Bounded(was + by);
            Strings();
            return content.anchoredPosition - was;
        }

        /// <summary>
        /// The view kept over the board, give or take a margin.
        ///
        /// A little way past the edge, so a node against it is not jammed against
        /// the window frame; no further, because panning into blank grid for ever
        /// is how a board gets lost.
        /// </summary>
        private Vector2 Bounded(Vector2 at)
        {
            if (sheet == null || content == null)
            {
                return at;
            }
            float much = content.localScale.x;
            Rect room = sheet.rect;
            // Half a view past the edge either way: enough that a node against the
            // fence is not jammed against the window frame, and enough for ZOOM FIT
            // to centre a small board without the next pan snapping it back.
            float overX = room.width * 0.5f;
            float overY = room.height * 0.5f;
            // The content's corner is its top left, and it moves the other way from
            // the view: to see the right of the board, its x goes negative. To see
            // the foot of it, its y goes up.
            float leftmost = room.width - BoardWide * much - overX;
            at.x = Mathf.Clamp(at.x, Mathf.Min(leftmost, overX), overX);
            float lowest = BoardTall * much - room.height + overY;
            at.y = Mathf.Clamp(at.y, -overY, Mathf.Max(lowest, -overY));
            return at;
        }

        /// <summary>
        /// The wheel zooms, about the pointer rather than about the corner: the
        /// thing under the hand is the thing somebody is looking at, and it should
        /// stay under the hand.
        /// </summary>
        private void Zooming(float wheel, Vector2 screen)
        {
            if (content == null || Mathf.Abs(wheel) < 0.01f)
            {
                return;
            }
            float was = content.localScale.x;
            float now = Mathf.Clamp(was * (wheel > 0f ? 1.1f : 1f / 1.1f),
                                    LeastZoom, MostZoom);
            if (Mathf.Abs(now - was) < 0.0001f)
            {
                return;
            }
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    sheet, screen, null, out local))
            {
                // Where the pointer is on the sheet, less where the content's own
                // corner is, is the point that has to hold still.
                Vector2 from = local - content.anchoredPosition;
                content.anchoredPosition = local - from * (now / was);
            }
            content.localScale = new Vector3(now, now, 1f);
            content.anchoredPosition = Bounded(content.anchoredPosition);
            Strings();
        }

        /// <summary>
        /// Besiege's own undo, which is the one this window's edits are in: every
        /// change here goes through a mapper control and is filed with the machine
        /// the same way a slider dragged in the mapper is.
        /// </summary>
        /// <summary>
        /// Puts the whole board in the window: as far out as it takes to see
        /// everything, and everything in the middle of what is left.
        ///
        /// A board panned somewhere else and zoomed a long way in is a board
        /// somebody has lost, and looking for it by hand is a poor use of a hand.
        /// </summary>
        private void Fitted()
        {
            if (content == null || sheet == null)
            {
                return;
            }
            Vector2 low;
            Vector2 high;
            if (!Spread(out low, out high))
            {
                content.localScale = Vector3.one;
                Middled();
                return;
            }
            Rect room = sheet.rect;
            // A margin, so the outermost node is inside the board rather than
            // against its edge.
            float edge = 24f;
            float wide = Mathf.Max(1f, high.x - low.x);
            float tall = Mathf.Max(1f, high.y - low.y);
            float much = Mathf.Clamp(
                Mathf.Min((room.width - edge * 2f) / wide,
                          (room.height - edge * 2f) / tall), LeastZoom, MostZoom);
            content.localScale = new Vector3(much, much, 1f);
            Looking((low + high) * 0.5f);
        }

        /// <summary>
        /// The box everything drawn sits in, in the board's own units. False when
        /// there is nothing drawn.
        /// </summary>
        private bool Spread(out Vector2 low, out Vector2 high)
        {
            low = Vector2.zero;
            high = Vector2.zero;
            bool any = false;
            int many = Nodes;
            for (int node = 0; node < many; node++)
            {
                Vector2 at = Where(node);
                Vector2 span = new Vector2(NodeWidth, NodeHeight);
                if (node < parts.Count && parts[node] != null)
                {
                    span = (parts[node].transform as RectTransform).sizeDelta;
                }
                if (!any)
                {
                    low = at;
                    high = at + span;
                    any = true;
                    continue;
                }
                low = new Vector2(Mathf.Min(low.x, at.x), Mathf.Min(low.y, at.y));
                high = new Vector2(Mathf.Max(high.x, at.x + span.x),
                                   Mathf.Max(high.y, at.y + span.y));
            }
            return any;
        }

        /// <summary>Puts a place on the board in the middle of the window, at
        /// whatever the board is zoomed to. A node's y counts down from the board's
        /// own top left, which is why it is the one that changes sign.</summary>
        private void Looking(Vector2 at)
        {
            if (content == null || sheet == null)
            {
                return;
            }
            Rect room = sheet.rect;
            float much = content.localScale.x;
            content.anchoredPosition =
                new Vector2(room.width * 0.5f - at.x * much,
                            at.y * much - room.height * 0.5f);
            Strings();
        }

        // ---- picking things out ----------------------------------------------

        /// <summary>The dashed edge round a node that has one now, made at the size
        /// that node is drawn at.</summary>
        private GameObject Edged(int node, float much)
        {
            if (node < 0 || node >= parts.Count || parts[node] == null)
            {
                return null;
            }
            RectTransform rect = parts[node].transform as RectTransform;
            Vector2 span = rect.sizeDelta;
            return Edge(parts[node], much, span.x, span.y);
        }

        /// <summary>A dashed edge round a node's body, at the opacity asked for.
        /// Made hidden; the caller shows it.</summary>
        private GameObject Edge(GameObject body, float much, float wide, float tall)
        {
            GameObject edge = Edging(body.transform,
                                     new Color(1f, 1f, 1f, much));
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-2f, -2f);
            rect.offsetMax = new Vector2(2f, 2f);
            Dashes(edge, wide + 4f, tall + 4f);
            return edge;
        }

        /// <summary>The faint edge on each node, and which node the pointer is on.
        /// </summary>
        private readonly List<GameObject> marks = new List<GameObject>();

        /// <summary>The edge that says a node is picked out, and the text box of a
        /// comment -- both of which follow the selection without the board being
        /// drawn again.</summary>
        private readonly List<GameObject> rims = new List<GameObject>();

        /// <summary>The text box in each comment, so that what is being typed into
        /// one can be read back before the board is written down. See
        /// <see cref="Voiced"/>.</summary>
        private readonly List<InputField> notes = new List<InputField>();

        /// <summary>The cross that removes a node, one to a node and made the first
        /// time the pointer finds that node.</summary>
        private readonly List<GameObject> bins = new List<GameObject>();

        private int under = -1;

        /// <summary>Which node is showing its cross, so the last one can be put
        /// away when the pointer moves on.</summary>
        private int binned = -1;

        /// <summary>Set while a switch is being put back after a click that was
        /// meant for the node under it, so its own handler stands down.</summary>
        private bool hushed;

        private void Watched(int node, bool on)
        {
            if (on)
            {
                under = node;
            }
            else if (under == node)
            {
                under = -1;
            }
        }

        /// <summary>Shows or hides a node's cross, making it the first time it is
        /// wanted: most nodes are never pointed at.</summary>
        private void Binned(int node, bool on)
        {
            if (node < 0 || node >= parts.Count || parts[node] == null)
            {
                return;
            }
            while (bins.Count <= node)
            {
                bins.Add(null);
            }
            if (on && bins[node] == null)
            {
                bins[node] = Cross(parts[node], node);
            }
            if (bins[node] != null && bins[node].activeSelf != on)
            {
                bins[node].SetActive(on);
            }
            if (on && bins[node] != null)
            {
                // Over everything else on the node. The dashed edge that says the
                // node is picked out is made when it is picked, which may be long
                // after the cross was, and the last child is the one drawn on top.
                bins[node].transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// A click on a node's own body picks it out.
        ///
        /// With a modifier held this stands aside and <see cref="Pointing"/> takes
        /// the click from the pointer instead, so control-clicking a node's key
        /// cell or its switch picks the node out rather than doing what the control
        /// under the pointer would have done.
        /// </summary>
        private void Chosen(int node)
        {
            if (Picking() || node < 0 || node >= Nodes)
            {
                return;
            }
            if (dragging)
            {
                // uGUI hands out the click at the end of a drag as well, whenever
                // the object pressed is the object dragged -- which a node always
                // is. Taking it would clear a selection somebody had just finished
                // moving. The drag says it is still going: it is ended a moment
                // after this, in the same release.
                return;
            }
            Pick(node, false);
        }

        /// <summary>Whether control is held, which is what turns a click into a
        /// picking-out and a drag into an axis-locked one.</summary>
        private static bool Ctrl()
        {
            return Input.GetKey(KeyCode.LeftControl)
                || Input.GetKey(KeyCode.RightControl);
        }

        /// <summary>Either modifier: both pick a node out, and a cell under either
        /// of them stands aside for that.</summary>
        private static bool Picking()
        {
            return Deaf.Aside();
        }

        /// <summary>
        /// Shows the faint edge on whatever the pointer is over while control is
        /// held, and takes the click that follows.
        ///
        /// The click is taken here rather than by the node, because every part of a
        /// node -- its heading, its key cell, its ports, its cross -- has its own
        /// idea of what a click means, and with control held none of them apply.
        /// </summary>
        private void Pointing()
        {
            bool armed = Picking();
            for (int i = 0; i < marks.Count; i++)
            {
                // While a box is being dragged it says what it has caught; the rest
                // of the time it is whatever the pointer is over with a modifier
                // down.
                bool show = banding ? banded.Contains(i) : (armed && i == under);
                if (show && marks[i] == null)
                {
                    marks[i] = Edged(i, 0.5f);
                }
                // Null where the node has never been pointed at: the faint edges
                // are made on demand, and asking one that does not exist whether it
                // is shown threw here every frame -- which took everything after
                // this in Update with it, selection and the fading message
                // included.
                if (marks[i] != null && marks[i].activeSelf != show)
                {
                    marks[i].SetActive(show);
                }
            }
            // The cross belongs to whatever the pointer is on, and follows it.
            if (binned != under)
            {
                Binned(binned, false);
                binned = under;
            }
            Binned(under, true);
            // Taken when the button comes up rather than when it goes down: with
            // control held a drag is an axis-locked move, and a press that picked
            // the node out would change the selection every time one began.
            if (armed && under >= 0 && under < Nodes
                && Input.GetMouseButtonDown(0))
            {
                aimed = under;
                aimedAt = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                float moved = ((Vector2)Input.mousePosition - aimedAt).magnitude;
                if (aimed >= 0 && aimed == under && aimed < Nodes && Picking()
                    && moved < 8f)
                {
                    Pick(aimed, true);
                }
                aimed = -1;
            }
        }

        /// <summary>The node a modifier-click went down on, and where the pointer
        /// was when it did.</summary>
        private int aimed = -1;
        private Vector2 aimedAt;

        // ---- moving what is picked out ---------------------------------------

        /// <summary>What a drag is moving, where each of them started, and how far
        /// the pointer has gone since it began. Held so that a drag with control
        /// down can be measured from where it started rather than from the step
        /// before it -- an axis lock worked out one step at a time drifts.
        /// </summary>
        private readonly List<int> moving = new List<int>();
        private readonly List<Vector2> began = new List<Vector2>();
        private Vector2 travelled;

        /// <summary>Whether a node is being dragged, which is when the board
        /// follows it off its own edge.</summary>
        private bool dragging;

        /// <summary>How near the edge a dragged node has to be before the board
        /// starts moving, and how fast it moves once it is.</summary>
        private const float Verge = 44f;
        private const float Chase = 520f;

        /// <summary>
        /// The board panned under a node being dragged past its edge.
        ///
        /// The nodes go with it: the pointer is standing still and the view is
        /// moving under it, so a node that did not move would slide out from under
        /// the hand holding it.
        /// </summary>
        private void Verging()
        {
            if (!dragging || sheet == null || content == null
                || !Input.GetMouseButton(0))
            {
                return;
            }
            Vector2 at;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    sheet, Input.mousePosition, null, out at))
            {
                return;
            }
            Rect room = sheet.rect;
            Vector2 push = Vector2.zero;
            if (at.x - room.xMin < Verge)
            {
                push.x = Verge - (at.x - room.xMin);
            }
            else if (room.xMax - at.x < Verge)
            {
                push.x = -(Verge - (room.xMax - at.x));
            }
            if (at.y - room.yMin < Verge)
            {
                push.y = Verge - (at.y - room.yMin);
            }
            else if (room.yMax - at.y < Verge)
            {
                push.y = -(Verge - (room.yMax - at.y));
            }
            if (push == Vector2.zero)
            {
                return;
            }
            push = push / Verge * Chase * Time.unscaledDeltaTime;
            // How far the view actually went, which is nothing once the board's own
            // edge is reached.
            push = Panned(push);
            // The same distance the other way, in the board's own units, so what is
            // being dragged stays under the pointer.
            float much = content.localScale.x;
            Vector2 gone = much > 0.001f ? push / much : push;
            Hauling(new Vector2(-gone.x, gone.y));
            // And the drag is told the ground moved. It measures the pointer inside
            // the board, so a board that moves reads as a pointer that moved -- and
            // the node was carried away twice, once by this and once by the drag
            // agreeing with it.
            if (hauler != null)
            {
                hauler.Shifted(new Vector2(-gone.x, -gone.y));
            }
        }

        /// <summary>The drag doing the moving, so the board can tell it when the
        /// ground has moved under it.</summary>
        private NodeDrag hauler;

        private void Holding(int node, NodeDrag drag)
        {
            dragging = true;
            hauler = drag;
            moving.Clear();
            began.Clear();
            travelled = Vector2.zero;
            // Dragging one of a picked-out set moves the set: a selection is made
            // to be moved, and a subcircuit dragged a node at a time stops being
            // the shape somebody picked out.
            if (Picked(node) && picked.Count > 1)
            {
                for (int i = 0; i < picked.Count; i++)
                {
                    moving.Add(picked[i]);
                }
            }
            else
            {
                moving.Add(node);
            }
            for (int i = 0; i < moving.Count; i++)
            {
                began.Add(Where(moving[i]));
            }
        }

        /// <summary>
        /// One frame of that drag. With control held the whole thing runs along
        /// whichever axis it has gone furthest down, from where it started -- so
        /// the lock can be taken up and let go mid-drag and the nodes end up level
        /// with where they were either way.
        /// </summary>
        private void Hauling(Vector2 step)
        {
            travelled += step;
            Vector2 gone = travelled;
            if (Ctrl())
            {
                gone = Mathf.Abs(travelled.x) >= Mathf.Abs(travelled.y)
                    ? new Vector2(travelled.x, 0f)
                    : new Vector2(0f, travelled.y);
            }
            for (int i = 0; i < moving.Count && i < began.Count; i++)
            {
                Put(moving[i], began[i] + gone);
            }
            Strings();
        }

        /// <summary>
        /// Nodes whose cell has been switched to take a name but has nothing typed
        /// into it yet.
        ///
        /// Nothing bound is nothing bound, so there is nowhere in the table or the
        /// layout for "it is going to be a name" to live -- and the board is drawn
        /// again the moment anything changes, which is what put the cell straight
        /// back to a key and made the switch look dead. Held here, where a redraw
        /// does not reach it.
        /// </summary>
        private readonly List<int> naming = new List<int>();

        private void Wants(int node, bool name)
        {
            if (name)
            {
                if (!naming.Contains(node))
                {
                    naming.Add(node);
                }
            }
            else
            {
                naming.Remove(node);
            }
        }

        /// <summary>What a node's cell shows: the name it is bound to, or an empty
        /// name where somebody has asked for one.</summary>
        private string Shown(int node, string variable, KeyCode key)
        {
            return variable == null && key == KeyCode.None && naming.Contains(node)
                ? "" : variable;
        }

        /// <summary>Whether a node is picked out.</summary>
        private bool Picked(int node)
        {
            return picked.Contains(node);
        }

        private void Pick(int node, bool add)
        {
            if (!add)
            {
                picked.Clear();
            }
            if (picked.Contains(node))
            {
                picked.Remove(node);
            }
            else
            {
                picked.Add(node);
            }
            Rims();
        }

        /// <summary>
        /// A message over the control that would not take what was typed into it.
        ///
        /// Its own small bubble rather than a tooltip: a tooltip belongs to
        /// whatever the pointer is on and goes when the pointer does, and this has
        /// to stay long enough to be read. It fades out on its own.
        /// </summary>
        private GameObject warning;
        private Text warningLabel;
        private CanvasGroup warningFade;
        private float warnAt;

        /// <summary>
        /// The message put where the hand that was refused is: over one of a node's
        /// ports, or over the whole node where the port is -1.
        ///
        /// Over the thing refused rather than in a corner of the window, because a
        /// message somewhere else is a message about nothing in particular.
        /// </summary>
        private void Told(int node, int port, string words)
        {
            RectTransform over = port >= 0 ? Held(node * 3 + port) : null;
            if (over == null && node >= 0 && node < parts.Count
                && parts[node] != null)
            {
                over = parts[node].transform as RectTransform;
            }
            Warned(over != null ? over : sheet, words);
        }

        private void Warned(RectTransform over, string words)
        {
            Warned(over, words, Hot);
        }

        /// <summary>The same, in whatever colour the news deserves: red for a
        /// gesture refused, the live colour for one that worked.</summary>
        private void Warned(RectTransform over, string words, Color ink)
        {
            if (canvas == null || over == null)
            {
                return;
            }
            if (warning == null)
            {
                warning = Rounded(canvas.transform, 0f, 0f, 10f, 10f,
                                  new Color(0.10f, 0.13f, 0.17f, 0.97f));
                warningLabel = Caption(warning, "", TextAnchor.MiddleCenter);
                warningFade = warning.AddComponent<CanvasGroup>();
                warningFade.blocksRaycasts = false;
                warningFade.interactable = false;
                RectTransform made = warning.GetComponent<RectTransform>();
                made.anchorMin = new Vector2(0f, 1f);
                made.anchorMax = new Vector2(0f, 1f);
                made.pivot = new Vector2(0f, 1f);
            }
            warningLabel.text = words.ToUpperInvariant();
            warningLabel.color = ink;
            // As many lines as it was written with: a refusal that needs a clause
            // to be understood -- what cannot be done, and what to do instead --
            // reads as two short lines and not as one long one.
            int lines = 1;
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i] == '\n')
                {
                    lines++;
                }
            }
            float wide = Mathf.Max(90f, warningLabel.preferredWidth + 18f);
            float tall = 22f + (lines - 1) * 15f;

            // Over the control that refused it, in the canvas's own coordinates.
            RectTransform home = canvas.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            over.GetWorldCorners(corners);
            Vector2 middle;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    home, RectTransformUtility.WorldToScreenPoint(
                        null, (corners[0] + corners[2]) * 0.5f),
                    null, out middle))
            {
                return;
            }
            Vector2 room = home.rect.size;
            float x = Mathf.Clamp(middle.x + room.x * 0.5f - wide * 0.5f, 2f,
                                  room.x - wide - 2f);
            float y = Mathf.Clamp(room.y * 0.5f - middle.y - tall - 18f, 2f,
                                  room.y - tall - 2f);
            UIF.Fit(warning.GetComponent<RectTransform>(), x, y, wide, tall);
            warning.transform.SetAsLastSibling();
            warning.SetActive(true);
            warningFade.alpha = 1f;
            warnAt = Time.unscaledTime + 2f;
        }

        /// <summary>Takes the message away again, fading it out at the end so it
        /// does not simply vanish mid-read.</summary>
        private void Fading()
        {
            if (warning == null || !warning.activeSelf)
            {
                return;
            }
            float left = warnAt - Time.unscaledTime;
            if (left <= 0f)
            {
                warning.SetActive(false);
                return;
            }
            if (warningFade != null)
            {
                warningFade.alpha = Mathf.Clamp01(left / 0.45f);
            }
        }

        /// <summary>Whether something is being typed into, which is when a key is
        /// a letter rather than a command.</summary>
        private static bool Typing()
        {
            if (EventSystem.current == null)
            {
                return false;
            }
            GameObject on = EventSystem.current.currentSelectedGameObject;
            return on != null && on.GetComponent<InputField>() != null;
        }

        /// <summary>
        /// Removes everything picked out, as one edit.
        ///
        /// Highest number first: a row taken out of the table shuffles the ones
        /// after it up, and an end taken out of the layout does the same -- so
        /// working down leaves every number still to be removed where it was.
        /// </summary>
        private void Erase()
        {
            List<int> going = new List<int>(picked);
            going.Sort();
            picked.Clear();
            // Whatever was armed is armed no longer: the numbers are about to move
            // under it.
            pending = -1;
            List<MapperType> touched = new List<MapperType>();
            for (int i = going.Count - 1; i >= 0; i--)
            {
                Removed(going[i], touched);
            }
            Commit(touched);
            Rebuilt();
        }

        /// <summary>A click on the empty board puts the selection down: what was
        /// picked out is what the next drag or copy acts on, and clicking away from
        /// all of it is how that is said everywhere else.</summary>
        private void Nobody()
        {
            if (picked.Count == 0)
            {
                return;
            }
            picked.Clear();
            Rims();
        }

        /// <summary>
        /// Shows the picked-out edges the selection says, and takes a picked
        /// comment's text box out of the way.
        ///
        /// This is the whole of what changes when a node is picked out, and it is
        /// why picking one is instant: the board used to be torn down and built
        /// again for it.
        /// </summary>
        private void Rims()
        {
            for (int i = 0; i < rims.Count; i++)
            {
                bool on = Picked(i);
                if (on && rims[i] == null)
                {
                    rims[i] = Edged(i, 1f);
                }
                if (rims[i] != null && rims[i].activeSelf != on)
                {
                    rims[i].SetActive(on);
                }
            }
        }

        /// <summary>
        /// A rectangle dragged over the board with shift held: everything inside it
        /// is picked out. Drawn while the drag is going on, and settled when it
        /// ends.
        /// </summary>
        private void Boxing(Vector2 from, Vector2 to, bool done)
        {
            if (band == null)
            {
                band = Edging(sheet, new Color(1f, 1f, 1f, 0.85f));
            }
            Rect box = new Rect(Mathf.Min(from.x, to.x), Mathf.Min(from.y, to.y),
                                Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            RectTransform rect = band.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(box.xMin, box.yMax);
            rect.sizeDelta = new Vector2(box.width, box.height);
            Dashes(band, box.width, box.height);
            band.SetActive(true);

            // What the box is over as it is dragged, which is what wears the faint
            // edge until it is let go: a rectangle over a board says nothing about
            // what it has caught unless the things it has caught say so.
            banded.Clear();
            for (int node = 0; node < Nodes && node < parts.Count; node++)
            {
                if (parts[node] == null)
                {
                    continue;
                }
                // The shared corner buffer: this runs for every node on every
                // frame of the drag, and a four-element array a node a frame is
                // rubbish for the collector to pick up afterwards.
                (parts[node].transform as RectTransform).GetWorldCorners(edges);
                Vector2 low = sheet.InverseTransformPoint(edges[0]);
                Vector2 high = sheet.InverseTransformPoint(edges[2]);
                if (low.x <= box.xMax && high.x >= box.xMin
                    && low.y <= box.yMax && high.y >= box.yMin)
                {
                    banded.Add(node);
                }
            }
            banding = !done;
            if (!done)
            {
                return;
            }
            band.SetActive(false);

            // A box on its own starts again with what it caught. With shift it adds
            // to what is picked out; with control it works on it -- everything in
            // the box that was out comes in, and everything in it that was in goes
            // out.
            bool toggling = Ctrl();
            bool adding = Picking() && !toggling;
            if (!toggling && !adding)
            {
                picked.Clear();
            }
            for (int i = 0; i < banded.Count; i++)
            {
                int node = banded[i];
                if (toggling && picked.Contains(node))
                {
                    picked.Remove(node);
                }
                else if (!picked.Contains(node))
                {
                    picked.Add(node);
                }
            }
            banded.Clear();
            Rims();
        }

        /// <summary>What a box being dragged is over, and whether one is being
        /// dragged at all.</summary>
        private readonly List<int> banded = new List<int>();
        private bool banding;

        /// <summary>A dashed rectangle: four edges of dashes, which is what says
        /// "picked out" rather than "has a border".</summary>
        private GameObject Edging(Transform host, Color colour)
        {
            GameObject go = new GameObject("Dashes");
            go.transform.SetParent(host, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            for (int side = 0; side < 4; side++)
            {
                GameObject edge = new GameObject("Edge");
                edge.transform.SetParent(go.transform, false);
                RectTransform line = edge.AddComponent<RectTransform>();
                bool flat = side < 2;
                line.anchorMin = new Vector2(flat ? 0f : (side == 2 ? 0f : 1f),
                                             flat ? (side == 0 ? 1f : 0f) : 0f);
                line.anchorMax = new Vector2(flat ? 1f : (side == 2 ? 0f : 1f),
                                             flat ? (side == 0 ? 1f : 0f) : 1f);
                line.pivot = new Vector2(0.5f, 0.5f);
                line.sizeDelta = flat ? new Vector2(0f, 1.5f) : new Vector2(1.5f, 0f);
                line.anchoredPosition = Vector2.zero;
                RawImage drawn = edge.AddComponent<RawImage>();
                drawn.texture = flat ? Glyphs.Dash : Glyphs.DashDown;
                drawn.color = colour;
                drawn.raycastTarget = false;
            }
            go.SetActive(false);
            return go;
        }

        /// <summary>
        /// Sets how many dashes each edge of one of those carries.
        ///
        /// One dash every <see cref="Glyphs.DashStep"/> pixels, counted from the
        /// box's own size, so the dashes stay the same length whatever the box is
        /// and a box dragged wider gets more of them rather than longer ones.
        /// </summary>
        private static void Dashes(GameObject edges, float wide, float high)
        {
            if (edges == null)
            {
                return;
            }
            for (int side = 0; side < edges.transform.childCount; side++)
            {
                RawImage drawn =
                    edges.transform.GetChild(side).GetComponent<RawImage>();
                if (drawn == null)
                {
                    continue;
                }
                float along = Mathf.Max(1f, (side < 2 ? wide : high)
                                            / Glyphs.DashStep);
                drawn.uvRect = side < 2 ? new Rect(0f, 0f, along, 1f)
                                        : new Rect(0f, 0f, 1f, along);
            }
        }

        // ---- copying ----------------------------------------------------------

        /// <summary>
        /// Copies what is picked out. The copies follow the pointer until they are
        /// put down, which is what says they are waiting to be.
        /// </summary>
        private void Copy()
        {
            clipboard.Clear();
            Vector2 corner = Vector2.zero;
            for (int i = 0; i < picked.Count; i++)
            {
                Vector2 where = Where(picked[i]);
                if (i == 0 || where.x < corner.x)
                {
                    corner.x = where.x;
                }
                if (i == 0 || where.y < corner.y)
                {
                    corner.y = where.y;
                }
            }
            for (int i = 0; i < picked.Count; i++)
            {
                int node = picked[i];
                Copied made = new Copied();
                made.At = Where(node) - corner;
                LogicRow row = Row(node);
                if (row != null && row.Ready)
                {
                    made.IsGate = true;
                    made.Gate = row.Gate;
                    made.Mode = row.Switch;
                    made.Wire = Bindings.IsVariable(row.Emulate)
                        ? Bindings.Variable(row.Emulate) : null;
                    made.Answers = made.Wire == null ? Bindings.Code(row.Emulate)
                                                     : KeyCode.None;
                    for (int port = 0; port < 2; port++)
                    {
                        MKey input = port == 0 ? row.InputA : row.InputB;
                        made.Inputs[port] = Bindings.IsVariable(input)
                            ? Bindings.Variable(input) : null;
                        made.Keys[port] = made.Inputs[port] == null
                            ? Bindings.Code(input) : KeyCode.None;
                    }
                }
                else
                {
                    Place place = Placed(node);
                    if (place == null)
                    {
                        continue;
                    }
                    made.Kind = place.Kind;
                    made.Variable = place.Variable;
                    made.Key = place.Key;
                    made.Words = place.Words;
                }
                clipboard.Add(made);
            }
            Ghosts();
        }

        /// <summary>The copies drawn under the pointer while they wait.</summary>
        private void Ghosts()
        {
            Lay();
            for (int i = 0; i < clipboard.Count; i++)
            {
                GameObject made = Rounded(canvas.transform, 0f, 0f, NodeWidth,
                                          NodeHeight,
                                          new Color(Ink.r, Ink.g, Ink.b, 0.55f));
                if (clipboard[i].IsGate)
                {
                    RawImage drawn = Picture(made.transform,
                                             Glyphs.Gate(clipboard[i].Gate), NodeIcon);
                    drawn.color = new Color(1f, 1f, 1f, 0.7f);
                }
                RectTransform rect = made.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 1f);
                rect.localScale = Zoom();
                Graphic[] bits = made.GetComponentsInChildren<Graphic>(true);
                for (int g = 0; g < bits.Length; g++)
                {
                    bits[g].raycastTarget = false;
                }
                ghosts.Add(made);
            }
        }

        /// <summary>Keeps the copies under the pointer.</summary>
        private void Carrying()
        {
            if (ghosts.Count == 0 || canvas == null)
            {
                return;
            }
            Vector2 local;
            RectTransform home = canvas.GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    home, Input.mousePosition, null, out local))
            {
                return;
            }
            float scale = content == null ? 1f : content.localScale.x;
            for (int i = 0; i < ghosts.Count && i < clipboard.Count; i++)
            {
                Vector2 at = clipboard[i].At * scale;
                ghosts[i].GetComponent<RectTransform>().anchoredPosition =
                    local + new Vector2(at.x, -at.y);
            }
        }

        /// <summary>Takes the copies down.</summary>
        private void Lay()
        {
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts[i] != null)
                {
                    ghosts[i].SetActive(false);
                    Destroy(ghosts[i]);
                }
            }
            ghosts.Clear();
        }

        /// <summary>
        /// Puts the copies down where the pointer is.
        ///
        /// Wires between copied gates are reproduced under fresh names: two copied
        /// gates keep their connection to each other, and a wire that came from
        /// outside the copy keeps the name it had, so it still reads whatever it
        /// read before.
        /// </summary>
        private void Paste()
        {
            if (served == null || clipboard.Count == 0 || content == null)
            {
                return;
            }
            Vector2 at;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, Input.mousePosition, null, out at))
            {
                return;
            }
            Vector2 corner = new Vector2(at.x, -at.y);

            List<MapperType> touched = new List<MapperType>();

            // What each copied binding became. A wire inside the copy is a name at
            // both ends of it, so reproducing the copy is a matter of reproducing
            // the names -- and any name that is already answered to on the board is
            // given a new one, or the copy would be wired into the circuit it was
            // taken from rather than to itself.
            List<string> was = new List<string>();
            List<string> now = new List<string>();
            List<KeyCode> wasKeys = new List<KeyCode>();
            List<string> nowKeys = new List<string>();

            // Copied ends whose own binding was free and is kept: a pasted gate
            // reading one of these still reads it.
            List<KeyCode> keptKeys = new List<KeyCode>();

            // One entry per thing copied, in the same order, so the input pass can
            // find the row a copy became.
            int[] made = new int[clipboard.Count];
            for (int i = 0; i < made.Length; i++)
            {
                made[i] = -1;
            }

            // The ends first, so that by the time a gate is pasted every name it
            // might read is known.
            for (int i = 0; i < clipboard.Count; i++)
            {
                Copied copy = clipboard[i];
                if (copy.IsGate)
                {
                    continue;
                }
                Place place = new Place();
                place.Kind = copy.Kind;
                place.Variable = copy.Variable;
                place.Key = copy.Key;
                place.Words = copy.Words;
                Vector2 laid = Fenced(Snapped(corner + copy.At));
                place.X = laid.x;
                place.Y = laid.y;
                if (copy.Kind != Place.Note
                    && Elsewhere(-1, copy.Variable, copy.Key))
                {
                    // Something on the board already answers to this. Pasted as it
                    // was, the copy and the original would be one end drawn twice
                    // and every wire on it would be shared; given a name of its
                    // own, the copy is a circuit of its own.
                    string name = Minted();
                    if (copy.Variable != null)
                    {
                        was.Add(copy.Variable);
                        now.Add(name);
                    }
                    else
                    {
                        wasKeys.Add(copy.Key);
                        nowKeys.Add(name);
                    }
                    place.Variable = name;
                    place.Key = KeyCode.None;
                }
                else if (copy.Variable != null)
                {
                    // Free, so it is pasted as it was and the copy reads it under
                    // the name it always had.
                    was.Add(copy.Variable);
                    now.Add(copy.Variable);
                }
                else if (copy.Key != KeyCode.None)
                {
                    keptKeys.Add(copy.Key);
                }
                Board.Places.Add(place);
            }

            for (int i = 0; i < clipboard.Count; i++)
            {
                Copied copy = clipboard[i];
                if (!copy.IsGate)
                {
                    continue;
                }
                int row = LogicTable.Add(served, touched);
                if (row < 0)
                {
                    break;                  // the block is full
                }
                LogicRow fresh = served.Rows[row];
                if (!fresh.Ready)
                {
                    continue;
                }
                fresh.Kind.Value = copy.Gate;
                fresh.Mode.IsActive = copy.Mode;
                touched.Add(fresh.Kind);
                touched.Add(fresh.Mode);
                // A name of its own, and one nothing else on the block is using:
                // two rows answering to the same name are one node as far as the
                // board is concerned, and the pasted one would never be drawn.
                // Beside it, whatever a copied output end it fed is called now, so
                // that wire is copied along with the two ends of it.
                string name = Minted();
                string said = name;
                if (copy.Wire != null)
                {
                    // A key answers to as many names as somebody gave it, and any
                    // of them may be what another copied gate reads.
                    string[] names = copy.Wire.Split(';');
                    for (int n = 0; n < names.Length; n++)
                    {
                        string one = names[n].Trim();
                        if (one.Length == 0)
                        {
                            continue;
                        }
                        string ended = Pasted(one, was, now);
                        if (ended != null)
                        {
                            said += ";" + ended;
                        }
                        was.Add(one);
                        now.Add(name);
                    }
                }
                else if (copy.Answers != KeyCode.None)
                {
                    wasKeys.Add(copy.Answers);
                    nowKeys.Add(name);
                }
                Bindings.BindVariable(fresh.Emulate, said);
                touched.Add(fresh.Emulate);
                Board.Put(row, Fenced(Snapped(corner + copy.At)));
                made[i] = row;
            }

            // Last, the inputs, once every new name is known.
            for (int i = 0; i < clipboard.Count; i++)
            {
                LogicRow fresh = made[i] >= 0 ? Row(made[i]) : null;
                if (fresh == null || !fresh.Ready)
                {
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    string wanted = clipboard[i].Inputs[port];
                    KeyCode key = clipboard[i].Keys[port];
                    MKey input = port == 0 ? fresh.InputA : fresh.InputB;
                    // Empty to start with: only the wires whose other end came
                    // along go back on, so a pasted gate arrives reading the copy
                    // rather than the circuit it was taken from.
                    Bindings.Bind(input, KeyCode.None);
                    if (wanted != null)
                    {
                        // One wire at a time -- an input may read several answers,
                        // and each of them was copied or was not.
                        List<string> names = Bindings.Named(wanted);
                        for (int n = 0; n < names.Count; n++)
                        {
                            int found = was.IndexOf(names[n]);
                            if (found >= 0)
                            {
                                Bindings.Added(input, now[found]);
                            }
                        }
                    }
                    else if (key != KeyCode.None)
                    {
                        int onKey = wasKeys.IndexOf(key);
                        if (onKey >= 0)
                        {
                            Bindings.Added(input, nowKeys[onKey]);
                        }
                        else if (keptKeys.Contains(key))
                        {
                            Bindings.Added(input, key);
                        }
                    }
                    touched.Add(input);
                }
            }

            // The ghosts are put down; what was copied stays copied, so the same
            // thing can be laid down twice.
            Lay();
            picked.Clear();
            Commit(touched);
            Rebuilt();
        }

        /// <summary>What a copied name is called in the copy, or null if it was not
        /// one of the names the copy renamed.</summary>
        private static string Pasted(string name, List<string> was, List<string> now)
        {
            int found = was.IndexOf(name);
            return found >= 0 ? now[found] : null;
        }



        /// <summary>The switches in the title bar.</summary>
        /// <summary>
        /// Takes every one of Besiege's own logic gates off the machine and into
        /// this block.
        ///
        /// The board is laid out again afterwards: a dozen gates arriving have no
        /// places of their own, and a column of nodes stacked in the corner is not
        /// the circuit somebody just imported.
        /// </summary>
        private void Import()
        {
            if (served == null)
            {
                return;
            }
            // Blocks are taken off the machine through the game's own selection
            // tool, and the game answers a deleted block by closing the block
            // mapper -- which would take this window with it, on the one gesture
            // whose whole point is to fill it.
            holding = true;
            try
            {
                // The block as it stands, so that everything this does -- the rows,
                // the ends made for what they read, where all of it is drawn, and
                // the blocks leaving the machine -- is one press of undo.
                BlockInfo before = LogicTable.Marked(served);
                int left;
                List<UndoAction> undo;
                int took = Conversion.From(served, out left, out undo);
                // The rows moved under the layout this was holding.
                board = null;
                // What the imported gates read and press, and nothing on the board
                // accounts for, is what the two ends of the board are.
                Adopt();
                Tidy(true);
                UndoAction edit = LogicTable.Edited(served, before);
                if (edit != null)
                {
                    undo.Add(edit);
                }
                Machine machine = Machine.Active();
                if (undo.Count > 0 && machine != null && machine.UndoSystem != null)
                {
                    machine.UndoSystem.AddActions(undo);
                }
                mark = null;
                Rebuilt();
                Fitted();
                // Everything above is this board's own doing, ends included.
                Ours();
                Warned(sheet, took + (took == 1 ? " gate imported" : " gates imported")
                       + (left > 0 ? "\n" + left + " left on the machine" : ""),
                       UIF.Live);
            }
            catch (Exception e)
            {
                Warned(sheet, e.Message);
                Log.Warn("import failed: " + e);
            }
            finally
            {
                holding = false;
                // And the menu, if the removal took it: the board stayed, and the
                // table under it should be there when the board is looked away
                // from.
                Remenu();
            }
        }

        private string Styled()
        {
            return style == Straight ? "LINE" : (style == Curved ? "CURVE" : "SQUARE");
        }

        /// <summary>
        /// The grid switch: on, every node sits on an intersection.
        ///
        /// Turning it on takes what is already drawn with it -- the switch is a
        /// promise about where nodes are, not only about where the next one lands
        /// -- and TIDY lays the board out and then falls on the grid like anything
        /// else, because it moves nodes the same way a hand does.
        /// </summary>
        private void Snapping()
        {
            grid = !grid;
            Squared();
            if (!grid || served == null)
            {
                return;
            }
            for (int node = 0; node < Nodes; node++)
            {
                Move(node, Where(node));
            }
            Kept();
            Redraw();
        }

        /// <summary>The switch's own lettering, lit while it is on.</summary>
        private void Squared()
        {
            if (gridLabel != null)
            {
                gridLabel.color = grid ? UIF.Live : UIF.Ink;
            }
        }

        /// <summary>A place taken to the nearest intersection, while the grid is
        /// on. A node's corner rather than its middle: the grid is drawn from the
        /// board's own corner, so that is where the lines cross.</summary>
        private static Vector2 Snapped(Vector2 at)
        {
            if (!grid)
            {
                return at;
            }
            return new Vector2(Mathf.Round(at.x / GridStep) * GridStep,
                               Mathf.Round(at.y / GridStep) * GridStep);
        }

        private void Styling()
        {
            style = (style + 1) % 3;
            if (styleLabel != null)
            {
                styleLabel.text = Styled();
            }
            Strings();
        }

        /// <summary>
        /// Lays the board out: inputs down the left, outputs down the right, and
        /// the gates in columns by how far they are from an input.
        ///
        /// The depth is the longest path back to something that feeds nothing,
        /// which is what puts a gate to the right of everything feeding it. A loop
        /// has no such path, so it is counted once and left where the walk found
        /// it.
        /// </summary>
        private void Tidy() { Tidy(false); }

        /// <summary>
        /// Lays the board out: inputs down the left, outputs down the right, gates
        /// in columns by how far they stand from an input, each column ordered to
        /// keep the wires from crossing and centred on the same line as the rest.
        ///
        /// It moves things and removes nothing. An end with no wires on it is one
        /// somebody is about to wire, not litter -- the red cross is how a node
        /// goes.
        ///
        /// <paramref name="quiet"/> is the board laying itself out the first time
        /// it is opened, which is not an edit anybody made and does not belong in
        /// the undo.
        /// </summary>
        private void Tidy(bool quiet)
        {
            if (served == null)
            {
                return;
            }
            // The graph is asked about a few thousand times below -- every node
            // against every other, four times over -- and it does not change while
            // this runs. See `named`.
            Sourced();
            try
            {
                Laid(quiet);
            }
            finally
            {
                Unsourced();
            }
        }

        private void Laid(bool quiet)
        {
            int many = Nodes;
            int[] depth = new int[many];
            for (int pass = 0; pass < many; pass++)
            {
                // Settled by repetition rather than by a walk: the graph is read
                // out of the rows every time it is asked about, and a board of a
                // few dozen nodes settles in a few dozen passes. A loop stops
                // moving once every node in it has been counted once, which is
                // what the cap is for.
                bool moved = false;
                for (int node = 0; node < many; node++)
                {
                    int deep = 0;
                    for (int port = 0; port < 2; port++)
                    {
                        Feeds(node, port, strands);
                        for (int i = 0; i < strands.Count; i++)
                        {
                            int from = strands[i];
                            if (depth[from] + 1 > deep)
                            {
                                deep = depth[from] + 1;
                            }
                        }
                    }
                    if (deep > depth[node] && deep < many)
                    {
                        depth[node] = deep;
                        moved = true;
                    }
                }
                if (!moved)
                {
                    break;
                }
            }
            int widest = 0;
            for (int node = 0; node < many; node++)
            {
                Place place = Placed(node);
                if (place != null && place.Kind == Place.Input)
                {
                    depth[node] = 0;
                }
                if (depth[node] > widest)
                {
                    widest = depth[node];
                }
            }
            for (int node = 0; node < many; node++)
            {
                Place place = Placed(node);
                if (place != null && place.Kind == Place.Output)
                {
                    depth[node] = widest + 1;
                }
            }
            // Which column each node is in, and then what order they stand in
            // within it. The order is what decides how many wires cross: a node
            // put level with the middle of whatever feeds it has its wire running
            // straight across instead of over its neighbours. Settled by repeating
            // the average a few times, which is the usual way of it.
            List<List<int>> columns = new List<List<int>>();
            for (int column = 0; column <= widest + 1; column++)
            {
                columns.Add(new List<int>());
            }
            for (int node = 0; node < many; node++)
            {
                Place noted = Placed(node);
                if (noted != null && noted.Kind == Place.Note)
                {
                    // A comment is not in the circuit and belongs where it was put:
                    // laying it out with the gates would move it away from whatever
                    // it is a comment on.
                    continue;
                }
                columns[depth[node]].Add(node);
            }

            // A column nobody is in is not a column: left in, it is a gap the width
            // of one, and the columns either side of it read as unevenly spaced.
            for (int column = columns.Count - 1; column >= 0; column--)
            {
                if (columns[column].Count == 0)
                {
                    columns.RemoveAt(column);
                }
            }

            float[] rank = new float[many];
            for (int column = 0; column < columns.Count; column++)
            {
                for (int i = 0; i < columns[column].Count; i++)
                {
                    rank[columns[column][i]] = i;
                }
            }
            for (int pass = 0; pass < 4; pass++)
            {
                for (int column = 0; column < columns.Count; column++)
                {
                    List<int> here = columns[column];
                    float[] middle = new float[here.Count];
                    for (int i = 0; i < here.Count; i++)
                    {
                        float total = 0f;
                        int seen = 0;
                        for (int other = 0; other < many; other++)
                        {
                            if (!Between(other, here[i]) && !Between(here[i], other))
                            {
                                continue;
                            }
                            total += rank[other];
                            seen++;
                        }
                        // Nothing wired keeps the place it had, so the untouched
                        // parts of a board do not shuffle every time this is asked.
                        middle[i] = seen == 0 ? rank[here[i]] : total / seen;
                    }
                    // An insertion sort on the averages, which is stable, so two
                    // nodes that tie keep the order they were in.
                    for (int i = 1; i < here.Count; i++)
                    {
                        int moving = here[i];
                        float mine = middle[i];
                        int at = i;
                        while (at > 0 && middle[at - 1] > mine)
                        {
                            here[at] = here[at - 1];
                            middle[at] = middle[at - 1];
                            at--;
                        }
                        here[at] = moving;
                        middle[at] = mine;
                    }
                    for (int i = 0; i < here.Count; i++)
                    {
                        rank[here[i]] = i;
                    }
                }
            }

            // Every column on one centreline, so a column of three and a column of
            // five read as one board rather than two stacks both hung from the top.
            float pitch = NodeHeight + 26f;
            // On the grid, a whole number of squares: the nodes are cells wide and
            // tall, so a pitch that is not costs every row a shove sideways when
            // it lands, and a column that was evenly spaced arrives ragged.
            if (grid)
            {
                pitch = Mathf.Ceil(pitch / GridStep) * GridStep;
            }
            // Middled on the board rather than tucked into its corner: see
            // `Centre`. The columns are measured first so the lot can be put down
            // with its own middle on the board's.
            float centre = Centre.y;
            float across = 0f;
            for (int column = 0; column < columns.Count; column++)
            {
                float span = 0f;
                for (int i = 0; i < columns[column].Count; i++)
                {
                    float mine = Row(columns[column][i]) != null ? GateWidth
                                                                 : NodeWidth;
                    if (mine > span)
                    {
                        span = mine;
                    }
                }
                across += span + (column > 0
                    ? (grid ? Mathf.Ceil(90f / GridStep) * GridStep : 90f) : 0f);
            }
            // The gap between columns, not the distance between their left edges:
            // a column of gates is narrower than a column of ends, so one pitch for
            // all of them leaves more clear board after the gates than after the
            // ends and the columns read as unevenly spaced. Wide enough that a wire
            // which has to pass a node passes between the columns rather than
            // behind it.
            float gap = grid ? Mathf.Ceil(90f / GridStep) * GridStep : 90f;
            float left = Centre.x - across * 0.5f;
            if (grid)
            {
                left = Mathf.Round(left / GridStep) * GridStep;
            }
            for (int column = 0; column < columns.Count; column++)
            {
                float span = 0f;
                for (int i = 0; i < columns[column].Count; i++)
                {
                    float mine = Row(columns[column][i]) != null ? GateWidth
                                                                 : NodeWidth;
                    if (mine > span)
                    {
                        span = mine;
                    }
                }
                float top = centre - (columns[column].Count - 1) * pitch * 0.5f;
                if (grid)
                {
                    top = Mathf.Round(top / GridStep) * GridStep;
                }
                for (int i = 0; i < columns[column].Count; i++)
                {
                    // Middled in the column, for the column that holds both sorts
                    // -- and against its left edge on the grid, where half a node's
                    // difference is half a square.
                    float mine = Row(columns[column][i]) != null ? GateWidth
                                                                 : NodeWidth;
                    float aside = grid ? 0f : (span - mine) * 0.5f;
                    Move(columns[column][i],
                         new Vector2(left + aside, top + i * pitch));
                }
                left += span + gap;
            }
            if (quiet)
            {
                // The board laying itself out as it opens: whoever asked for it
                // draws it, on a frame of its own.
                Keep();
                return;
            }
            Kept();
            Redraw();
        }

        // ---- small pieces ----------------------------------------------------

        /// <summary>A node's body: a rounded plate, which is what the rest of
        /// Besiege's interface is made of.</summary>
        private static GameObject Rounded(Transform host, float x, float y,
                                          float w, float h, Color colour)
        {
            GameObject go = new GameObject("Node");
            go.transform.SetParent(host, false);
            go.AddComponent<RectTransform>();
            // Nine-sliced, so a node twice the width of another has corners of the
            // same radius rather than twice the curve. A comment grows with what is
            // written in it, which is where a stretched corner shows.
            Image image = go.AddComponent<Image>();
            image.sprite = Glyphs.Plated;
            image.type = Image.Type.Sliced;
            image.color = colour;
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, w, h);
            return go;
        }

        /// <summary>Grows what it is given while the pointer is on the control,
        /// the way the tables' buttons do.</summary>
        private static void Grow(GameObject control, Transform grows)
        {
            UIF.Grow(control, grows);
        }

        /// <summary>The same, by a given amount. The two crosses want more of it:
        /// a letter that small growing by a tenth is a change nobody sees, and a
        /// button that does not answer the pointer reads as a dead one.</summary>
        private static void Grow(GameObject control, Transform grows, float by)
        {
            UIF.Grow(control, grows, by);
        }

        private static GameObject Plate(Transform host, float x, float y,
                                        float w, float h, Color colour)
        {
            GameObject go = new GameObject("Plate");
            go.transform.SetParent(host, false);
            go.AddComponent<RectTransform>();
            Image image = go.AddComponent<Image>();
            image.color = colour;
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, w, h);
            return go;
        }

        private static Text Caption(GameObject control, string words,
                                    TextAnchor align)
        {
            return UIF.Label(control, words, UIF.Ink, align, 7, true);
        }
    }
}
