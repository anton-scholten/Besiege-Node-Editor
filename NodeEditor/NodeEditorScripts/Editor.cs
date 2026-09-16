using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// The node editor: a window drawing a Computer block's rows as a circuit of
    /// nodes and wires, with IMPORT and EXPORT to and from Besiege's own blocks.
    /// Written rather than borrowed: Unity's node-editor libraries are editor-time
    /// tools or ship assemblies of their own, and this compiles with Besiege's
    /// compiler.
    /// </summary>
    public class Editor : MonoBehaviour
    {
        // Over the panel's canvas, under uGUI's own Dropdown canvas at 30000.
        private const int CanvasOrder = 2500;

        /// <summary>What the window opens at, and the least it can be dragged
        /// down to. It is resized by its corners after that.</summary>
        private const float StartWidth = 894f;
        private const float StartHeight = 574f;
        /// <summary>Wide enough for every button on the title bar and a prefix box
        /// of ninety beside them.</summary>
        private const float LeastWidth = 790f;
        private const float LeastHeight = 340f;

        /// <summary>Where it opens, from the screen's middle: right of and below
        /// the block mapper, so the table and the board are both in view.</summary>
        private static readonly Vector2 StartAt = new Vector2(418f, -128f);

        /// <summary>How the wires are drawn. Three ways, cycled by the switch in
        /// the title bar: a straight line, a curve, or right angles.</summary>
        private const int Straight = 0;
        private const int Curved = 1;
        private const int Square = 2;

        /// <summary>The wire style, kept between openings rather than saved: a way
        /// of looking, not part of the board.</summary>
        private static int style = Curved;

        /// <summary>Whether nodes snap to the grid; kept like the wire
        /// style.</summary>
        private static bool grid = true;
        private const float BarHeight = 26f;
        private const float Margin = 8f;
        /// <summary>An end's or a timer's size: five grid squares by two, so a node
        /// on the grid fills whole cells (see <see cref="GridStep"/>).</summary>
        private const float NodeWidth = GridStep * 5f;
        private const float NodeHeight = GridStep * 2f;

        /// <summary>A gate's height: it holds no name, so it is shorter than an
        /// end.
        /// </summary>
        private const float GateHeight = GridStep * 2f;

        /// <summary>A gate's width: a picture, and sometimes a switch.</summary>
        private const float GateWidth = GridStep * 3f;

        /// <summary>How big a gate's picture is drawn: in the palette, and on the
        /// node itself.</summary>
        private const float PaletteIcon = 26f;
        private const float NodeIcon = 24f;

        /// <summary>A gate's picture on its node, which has two cells of height to
        /// be drawn in.</summary>
        private const float GateIcon = 36f;
        private const float PortSize = 11f;

        /// <summary>How big the switch on a gate that has one is drawn.</summary>
        private const float Switch = 22f;

        /// <summary>How much of the board a port answers to, as against how much of
        /// it is drawn. See <see cref="Port"/>.</summary>
        private const float PortReach = 24f;

        /// <summary>The cross that removes a node, bigger than a port: it is aimed
        /// at, not dragged.</summary>
        private const float CrossSize = 48f;

        /// <summary>A comment's resizing corner and its arrow, in window units at
        /// any zoom.
        /// </summary>
        private const float GripSize = 14f;
        private const float GripArrows = 9.6f;
        private const float WireWidth = 2f;

        /// <summary>How far the wheel zooms, either way.</summary>
        private const float LeastZoom = 0.4f;
        private const float MostZoom = 2.5f;

        /// <summary>
        /// The board's size, in whole grid squares: a `RawImage` tiles from the
        /// bottom left, so a height that was not a whole number of squares put
        /// every line off its nodes. The block's own, saved with its layout and set
        /// by the two boxes under the board's bottom right corner; kept here as
        /// well because the helpers that place and fence a node are static and one
        /// board is open at a time. <see cref="Boarded"/> is what puts a size on.
        /// </summary>
        private static int boardCells = Wiring.WideCells;
        private static int boardLines = Wiring.TallCells;

        private static float BoardWide { get { return GridStep * boardCells; } }
        private static float BoardTall { get { return GridStep * boardLines; } }

        /// <summary>The smallest board there is, whatever is on it: room for a node
        /// and a little around it. Smaller than the nodes need is refused in <see
        /// cref="Resized"/> rather than here.</summary>
        private const int LeastCells = 8;
        private const int LeastLines = 4;

        /// <summary>The largest. A board is one `RawImage` and one wire mesh, so
        /// the limit is what those are still drawn at rather than any count of
        /// nodes.</summary>
        private const int MostCells = 2048;

        /// <summary>The board's middle, where a board opens and TIDY lays out: room
        /// to grow every way.</summary>
        private static Vector2 Centre
        {
            get { return new Vector2(BoardWide * 0.5f, BoardTall * 0.5f); }
        }

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Color Ink = new Color(0.10f, 0.13f, 0.17f, 0.96f);
        private static readonly Color LiveInk = new Color(0.012f, 1f, 0.847f, 1f);

        /// <summary>Besiege's red, which is what the game paints anything that
        /// removes something.</summary>
        private static readonly Color Hot = new Color(0.92f, 0.13f, 0.29f, 1f);

        /// <summary>Every board. One is made at load and serves whichever block
        /// opens; a pinned board keeps its block, so another block gets a new
        /// board, up to four.
        /// </summary>
        private static readonly List<Editor> all = new List<Editor>();

        private const int Most = 4;

        /// <summary>When this board was last opened on something, so the newest can
        /// be told from the rest.</summary>
        private int shown;

        private static int showings;

        private ComputerBehaviour served;
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
            public bool IsGate;              // a row: a gate, or a timer
            public int Gate;
            public bool Mode;
            public float Wait = 1f;          // a timer's settings
            public float Duration = 1f;
            public bool Hold;
            public bool Stop;
            public bool Loop;
            public string Variable;
            public KeyCode Key = KeyCode.None;
            public int Kind;                 // for a place
            public string Words;             // what a comment says
            public int Size;                 // a comment's font size, 0 for the usual
            public string Wire;              // a gate's own answer name
            public KeyCode[] Answers = new KeyCode[0];   // or the keys it presses
            public string[] Inputs = new string[2];
            public KeyCode[][] Keys = { new KeyCode[0], new KeyCode[0] };
            public Vector2 At;
        }


        private readonly List<GameObject> parts = new List<GameObject>();
        private readonly List<RectTransform> ports = new List<RectTransform>();

        /// <summary>The node whose answer is waiting for somewhere to go, or -1.
        /// Clicking an output arms it; clicking an input lands it.</summary>
        private int pending = -1;

        /// <summary>Which port of <see cref="pending"/> is armed: -1 its answer, 0
        /// or 1 an input.</summary>
        private int pendingPort = -1;

        public static bool Available { get { return UIF.Available; } }

        /// <summary>Builds the window ahead of time, while the game is busy
        /// elsewhere, rather than on top of the first block's opening.</summary>
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
        public static void Open(ComputerBehaviour block)
        {
            if (block == null)
            {
                return;
            }
            Editor on = Serving(block);
            if (on != null)
            {
                // Already up on this block: only brought to the front, keeping the
                // selection.
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

        /// <summary>Keeps boards on the block whose menu is up: an unpinned board
        /// follows the menu, a pinned one keeps its block. Polled, as the mapper
        /// changes hands without saying so (an undo, a click from block to
        /// block).</summary>
        private static void Following()
        {
            if (followed == Time.frameCount)
            {
                return;
            }
            followed = Time.frameCount;
            ComputerBehaviour block = Menu();
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

        /// <summary>Closes unpinned boards left drawing a block other than the one
        /// being edited.</summary>
        private static void Swept(ComputerBehaviour block)
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
        private static ComputerBehaviour Menu()
        {
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                if (mapper == null || !BlockMapper.IsOpen || mapper.Block == null)
                {
                    return null;
                }
                return mapper.Block.GetComponent<ComputerBehaviour>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The board already up on this block, if there is one.</summary>
        private static Editor Serving(ComputerBehaviour block)
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

        /// <summary>A board free to take another block: the open unpinned one
        /// first, then a closed one.</summary>
        private static Editor Lending()
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && Up(all[i]) && !all[i].pinned)
                {
                    return all[i];
                }
            }
            // The one used most recently, so the window that comes back is the one
            // just worked in.
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

        /// <summary>Stacks the boards in the order they were opened, the newest on
        /// top.
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
        public static bool Showing(ComputerBehaviour block)
        {
            return block != null && Serving(block) != null;
        }

        /// <summary>Open on this block, or closed if it already is.</summary>
        public static void Toggle(ComputerBehaviour block)
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
            muffled = ZoomGuard.Grip(false, muffled);
            all.Remove(this);
        }

        /// <summary>
        /// What the board is drawn from, as one number, polled twenty times a
        /// second to see whether the rows changed (the table, an undo, another
        /// player). A hash of the row count, the layout and every row's settings,
        /// allocating nothing; the layout text is hashed again only when the
        /// control returns a different string.
        /// </summary>
        private long Reading()
        {
            if (served == null)
            {
                return 0L;
            }
            ulong hash = HashOffset;
            hash = Mixed(hash, served.Count);
            string layout = served.LayoutControl == null ? ""
                : served.LayoutControl.Value;
            if (!object.ReferenceEquals(layout, layoutRead))
            {
                layoutRead = layout;
                layoutHash = TextHash(layout);
            }
            hash = Mixed(hash, (int)layoutHash);
            hash = Mixed(hash, (int)(layoutHash >> 32));
            for (int i = 0; i < served.Count && i < served.Rows.Count; i++)
            {
                LogicRow row = served.Rows[i];
                if (!row.Ready)
                {
                    continue;
                }
                hash = Mixed(hash, i);
                if (row.IsTimer)
                {
                    // A timer's numbers and switches too, or an undo of a wait
                    // changed on the board would go unseen here and undrawn.
                    hash = Mixed(hash, row.Wait.Value.GetHashCode());
                    hash = Mixed(hash, row.Duration.Value.GetHashCode());
                    hash = Mixed(hash, (row.Hold.IsActive ? 1 : 0)
                                       | (row.Stop.IsActive ? 2 : 0)
                                       | (row.Loop.IsActive ? 4 : 0));
                }
                hash = Mixed(hash, row.Gate);
                hash = Mixed(hash, row.Switch ? 1 : 0);
                hash = KeyHash(hash, row.InputA);
                hash = KeyHash(hash, row.InputB);
                hash = KeyHash(hash, row.Emulate);
            }
            return (long)hash;
        }

        /// <summary>The layout text last hashed, and its hash.</summary>
        private string layoutRead;
        private ulong layoutHash;

        // FNV-1a, 64 bits: wide enough that a missed change is not worth thinking
        // about.
        private const ulong HashOffset = 14695981039346656037UL;
        private const ulong HashPrime = 1099511628211UL;

        private static ulong Mixed(ulong hash, int value)
        {
            for (int b = 0; b < 4; b++)
            {
                hash = (hash ^ (ulong)(value & 0xff)) * HashPrime;
                value >>= 8;
            }
            return hash;
        }

        private static ulong TextHash(string text)
        {
            ulong hash = HashOffset;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                hash = (hash ^ text[i]) * HashPrime;
            }
            return hash;
        }

        /// <summary>Everything a key answers to: whether it is on names, every name,
        /// and every keycode.</summary>
        private static ulong KeyHash(ulong hash, MKey key)
        {
            if (key == null)
            {
                return Mixed(hash, -1);
            }
            hash = Mixed(hash, key.useMessage ? 1 : 0);
            string[] names = key.message;
            int count = names == null ? 0 : names.Length;
            hash = Mixed(hash, count);
            for (int n = 0; n < count; n++)
            {
                hash = Mixed(hash, names[n] == null ? 0 : names[n].GetHashCode());
            }
            hash = Mixed(hash, key.KeysCount);
            for (int k = 0; k < key.KeysCount; k++)
            {
                hash = Mixed(hash, (int)key.GetKey(k));
            }
            return hash;
        }

        /// <summary>When the board next asks whether the table has changed.
        /// </summary>
        private float asks;

        private long read;

        /// <summary>Whether a game menu is up (pause, save, load). `inMenu` is a
        /// count this window adds to while hovered, so only other holds
        /// count.</summary>
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

        /// <summary>The board's frame, wrapped: Unity stops at the first exception,
        /// which left a window drawn once and then dead. Logged once, in
        /// full.</summary>
        private void Update()
        {
            // The game has its keyboard back a frame after the key that was held
            // off.
            if (muffled && Time.frameCount > muffledAt)
            {
                muffled = ZoomGuard.Grip(false, muffled);
            }
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

        /// <summary>Whether a simulation is running: `StatMaster.levelSimulating`
        /// (a build block's `IsSimulating` stays false, notes 08), or the machine's
        /// own flag.
        /// </summary>
        private static bool Running()
        {
            try
            {
                if (StatMaster.levelSimulating)
                {
                    return true;
                }
                Machine machine = Machine.Active();
                return machine != null && machine.isSimulating;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Ticking()
        {
            if (window == null || !window.activeSelf)
            {
                return;
            }
            if (served == null)
            {
                // The block has been taken off the machine: close, pinned or not.
                Close();
                return;
            }
            if (Running())
            {
                // A run started and the machine is hidden: every board closes,
                // pinned or held open by an import (`lingering`, which never saw
                // the menu close for the run).
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
            // None of the three while something is being typed: a 1 and a letter
            // are what a name is spelt with.
            if (Hotkeys.Copy && !Typing())
            {
                Copy();
            }
            else if (Hotkeys.Paste && !Typing())
            {
                Paste();
            }
            else if (Hotkeys.All && !Typing())
            {
                // Every node on the board picked out, as a box round all of it
                // would.
                picked.Clear();
                for (int node = 0; node < Nodes; node++)
                {
                    picked.Add(node);
                }
                Rims();
            }
            Carrying();
            Pointing();
            Verging();
            Fading();
            Hues.Settle();
            if (scrubbing.Count > 0 && !Input.GetMouseButton(0))
            {
                // A timer's number dragged: written live all the while, and one
                // step of undo now the hand has let go.
                List<MapperType> touched = new List<MapperType>(scrubbing);
                scrubbing.Clear();
                Commit(touched);
            }
            // A click outside the window drops the selection, as one on empty board
            // does.
            if (Input.GetMouseButtonDown(0) && picked.Count > 0
                && !Inside(Input.mousePosition))
            {
                picked.Clear();
                Rims();
            }
            if ((Input.GetKeyDown(KeyCode.Delete)
                 || Input.GetKeyDown(KeyCode.Backspace))
                && picked.Count > 0 && !Typing())
            {
                // With the pointer outside, the game would delete its own selection
                // -- this very block -- on the same key, so the key is held off
                // this frame.
                Muffle();
                Erase();
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Shut())
            {
                Close();
                return;
            }
            // Twenty times a second: nothing changes the rows faster than an eye
            // follows.
            if (Time.unscaledTime < asks)
            {
                return;
            }
            asks = Time.unscaledTime + 0.05f;

            Lingered();
            if (!Up(this))
            {
                return;                     // another block's menu took it
            }
            if (pinsBox != null && served != null && served.PinControl != null
                && pinsBox.isOn != served.PinControl.IsActive)
            {
                hushed = true;
                pinsBox.isOn = served.PinControl.IsActive;
                hushed = false;
            }
            if (gridBox != null && gridBox.isOn != grid)
            {
                // Flipped on another board.
                hushed = true;
                gridBox.isOn = grid;
                hushed = false;
            }
            ComputerBehaviour was = served;
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
            long now = Reading();
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
                // Rows arrived or went elsewhere, renumbering the ends: the
                // selection is dropped rather than moved to whatever took its
                // numbers.
                picked.Clear();
                naming.Clear();
                pending = -1;
            }
            if (Rows > counted)
            {
                // The table's "+": put where it can be seen.
                Unplaced(counted);
            }
            // Ends are made for bindings nothing on the board accounts for -- only
            // for changes the board did not make.
            Adopt();
            // Whatever changed it -- the table, an undo, a redo -- the board is
            // drawn from the rows and the rows have moved.
            board = null;
            Redraw();
            // Including whatever `Adopt` has just made.
            Ours();
        }

        private void Show(ComputerBehaviour block)
        {
            served = block;
            lingering = false;
            if (!Build())
            {
                return;
            }
            shown = ++showings;
            window.SetActive(true);
            board = null;
            read = 0L;
            mark = null;
            // A block never opened has no layout, so it arrives tidied.
            bool blank = served.LayoutControl == null
                || string.IsNullOrEmpty(served.LayoutControl.Value);
            Adopt();
            if (blank)
            {
                Tidy(true);
            }
            // Drawn next frame: this one is already building the mapper and the
            // table.
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

        /// <summary>And owed a view of its middle, the same frame: the window's
        /// rects settle only after a layout pass.</summary>
        private bool middling;

        /// <summary>Opens the view on the board's middle, or on what is drawn where
        /// that is elsewhere (boards laid out by older versions).</summary>
        private void Middled()
        {
            Vector2 low;
            Vector2 high;
            Looking(Spread(out low, out high) ? (low + high) * 0.5f : Centre);
        }

        public void Close()
        {
            // Before the window goes: a cell still listening for a key holds a menu
            // count, and a board closed out from under it -- an export, a run
            // starting -- would leave that count standing and the game's own wheel
            // zoom dead.
            KeyCell.Dropped();
            if (window != null)
            {
                window.SetActive(false);
            }
            Tip.Tips.Hide();
            Choices.Close();
            // The message hangs on the canvas, not the window, so it does not go
            // with the window, and `Fading` stops with the window: it is hidden
            // here.
            if (warning != null)
            {
                warning.SetActive(false);
            }
            served = null;
            lingering = false;
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
                // Each board a little down and across from the last, so none hides
                // under another.
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

        /// <summary>Takes the prefab's title bar ("Sample Window" and a second
        /// cross) and its scroll view off.</summary>
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

        /// <summary>The pin. Out (white): the board goes with the block's menu. In
        /// (red): it stays up. Pulled out with the menu already gone, the board
        /// closes.</summary>
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

        /// <summary>Set after an import or export until the block's menu is back on
        /// it or another block's menu opens: the game closes the mapper for the
        /// removal, sometimes a frame later, and the board must not go with
        /// it.</summary>
        private bool lingering;

        /// <summary>Whether Besiege's block menu is up, on any block.</summary>
        private static bool MenuUp()
        {
            try
            {
                BlockMapper mapper = BlockMapper.CurrentInstance;
                return mapper != null && BlockMapper.IsOpen && mapper.Block != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A board held up by an import stays until a menu opens: on its
        /// own block that is ordinary rules again, and on another block it closes
        /// the board unless pinned. The menu is not reopened: that
        /// flickered.</summary>
        private void Lingered()
        {
            if (!lingering || !MenuUp())
            {
                return;
            }
            lingering = false;
            if (!Menued() && !pinned)
            {
                Close();                    // the player has moved on
            }
        }

        /// <summary>Whether the block's own menu is up on the block this board is
        /// drawing.</summary>
        private bool Menued()
        {
            return served != null && Menu() == served;
        }

        /// <summary>The block's menu closed: boards go with it unless pinned or
        /// lingering.
        /// </summary>
        public static void Dropped()
        {
            for (int i = 0; i < all.Count; i++)
            {
                // Not a board that has just imported: see `lingering`.
                if (all[i] != null && !all[i].pinned && !all[i].lingering)
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
        private RectTransform pinsRect;
        private RectTransform exportRect;

        /// <summary>The block's pin-blocks setting, as a switch on the bar. Kept in
        /// step with the block in `Ticking`, since an undo can flip it.</summary>
        private Toggle pinsBox;
        private readonly List<RectTransform> palette = new List<RectTransform>();
        private readonly List<Corner> corners = new List<Corner>();
        private Text styleLabel;

        /// <summary>The grid switch. Every board shares the one setting, so each
        /// keeps its own switch in step with it in `Ticking`.</summary>
        private Toggle gridBox;

        /// <summary>The two ways-of-colouring selectors on the unicolour row, and
        /// the words in front of them.</summary>
        private RectTransform nodeModeRect;
        private Text nodeModeLabel;
        private RectTransform wireModeRect;
        private Text wireModeLabel;
        private RectTransform nodeWords;
        private RectTransform wireWords;
        private RectTransform editRect;
        private Toggle editBox;

        /// <summary>Whether this board has its colours out: the row of them over
        /// the palette and the two unicolours under it.</summary>
        private bool editing;

        /// <summary>One colour per palette button, in the palette's order.</summary>
        private readonly List<Swatch> swatches = new List<Swatch>();
        private Swatch uniNode;
        private Swatch uniWire;
        private RectTransform resetRect;

        /// <summary>The board back to its starting size, at the far end of the
        /// third row.</summary>
        private RectTransform sizeBackRect;

        /// <summary>What each palette button draws itself with -- its picture or
        /// its word -- to be coloured as the kind it offers.</summary>
        private readonly List<Graphic> inks = new List<Graphic>();

        private RawImage gridLines;

        /// <summary>The three things drawn at the board's own size: the grid, the
        /// line round its edge and the mesh every wire is drawn in. Kept so the
        /// board can be resized while it is open.</summary>
        private RectTransform paperRect;
        private RectTransform fenceRect;
        private RectTransform wiresRect;

        /// <summary>How big the board is, on the row EDIT puts out: the words, the
        /// two boxes -- squares across and squares down -- and the "x" between
        /// them.</summary>
        private InputField cellsBox;
        private InputField linesBox;
        private RectTransform cellsRect;
        private RectTransform linesRect;
        private RectTransform byRect;
        private RectTransform sizeWords;

        /// <summary>The four lines round the edge of the board, and the zoom they
        /// were last made thick enough for.</summary>
        private readonly List<RectTransform> rails = new List<RectTransform>();
        private float railed = -1f;

        /// <summary>Each node's colour and random seed, worked out once per
        /// drawing: every wire asks for both of its ends.</summary>
        private readonly List<Color> tints = new List<Color>();
        private readonly List<int> seeds = new List<int>();
        private readonly List<int> slots = new List<int>();
        private bool tinted;

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

            GameObject bar = UIF.Plate(window.transform, 0f, 0f, size.x, BarHeight,
                                   new Color(0f, 0f, 0f, 0.25f));
            barRect = bar.GetComponent<RectTransform>();
            NodeDrag drag = bar.AddComponent<NodeDrag>();
            drag.frame = windowRect;
            drag.Moved = delegate(Vector2 by) { windowRect.anchoredPosition += by; };

            // Along the bar from the left, no title: wire style, grid, tidy, view,
            // import and export.
            // The wire style and the grid switch are not on the bar: they sit on
            // the row EDIT puts out, with the board's size.
            GameObject wireStyle = UIF.Spawn(UIF.ButtonPrefab, window.transform);
            if (wireStyle != null)
            {
                styleRect = wireStyle.GetComponent<RectTransform>();
                wireStyle.SetActive(false);
                shelf.Add(styleRect);
                UIF.NoSwell(wireStyle);
                styleLabel = Caption(wireStyle, Styled(style), TextAnchor.MiddleCenter);
                UIF.Grow(wireStyle, styleLabel.transform);
                Button click = wireStyle.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Styling);
                }
                Tip.On(wireStyle, "Wires: click for the next, right-click for the list");
                Asks asks = wireStyle.AddComponent<Asks>();
                asks.Asked = Restyling;
            }

            // A switch, drawn and lit the way PIN BLOCKS and EDIT are: it
            // says whether it is on, and it is the same click either way.
            GameObject squares = UIF.Spawn(UIF.TogglePrefab, window.transform);
            if (squares != null)
            {
                gridRect = squares.GetComponent<RectTransform>();
                squares.SetActive(false);
                shelf.Add(gridRect);
                UIF.NoSwell(squares);
                UIF.Grow(squares, Caption(squares, "GRID",
                                      TextAnchor.MiddleCenter).transform);
                Tip.On(squares, "Align nodes to the grid");
                gridBox = squares.GetComponent<Toggle>();
                if (gridBox != null)
                {
                    hushed = true;
                    gridBox.isOn = grid;
                    hushed = false;
                    gridBox.onValueChanged.AddListener(Snapping);
                }
            }

            GameObject tidy = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (tidy != null)
            {
                tidyRect = tidy.GetComponent<RectTransform>();
                UIF.NoSwell(tidy);
                UIF.Grow(tidy, Caption(tidy, "TIDY", TextAnchor.MiddleCenter).transform);
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
                UIF.Grow(whole, Caption(whole, "ZOOM FIT",
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
                UIF.Grow(brought, Caption(brought, "IMPORT",
                                      TextAnchor.MiddleCenter).transform);
                Tip.On(brought, "Take all logic gates and timers of the machine into this editor");
                Button click = brought.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Import);
                }
            }

            // Whether EXPORT pins what it makes, beside the button it governs.
            GameObject stakes = UIF.Spawn(UIF.TogglePrefab, bar.transform);
            if (stakes != null)
            {
                pinsRect = stakes.GetComponent<RectTransform>();
                UIF.NoSwell(stakes);
                UIF.Grow(stakes, Caption(stakes, "PIN BLOCKS",
                                     TextAnchor.MiddleCenter).transform);
                Tip.On(stakes, "Pin logic gate blocks when exported");
                pinsBox = stakes.GetComponent<Toggle>();
                if (pinsBox != null)
                {
                    hushed = true;
                    pinsBox.isOn = served == null || served.Pins;
                    hushed = false;
                    pinsBox.onValueChanged.AddListener(Staking);
                }
            }

            GameObject sent = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (sent != null)
            {
                exportRect = sent.GetComponent<RectTransform>();
                UIF.NoSwell(sent);
                UIF.Grow(sent, Caption(sent, "EXPORT", TextAnchor.MiddleCenter).transform);
                Tip.On(sent, "Convert to logic gate blocks");
                Button click = sent.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Export);
                }
            }

            GameObject edits = UIF.Spawn(UIF.TogglePrefab, bar.transform);
            if (edits != null)
            {
                editRect = edits.GetComponent<RectTransform>();
                UIF.NoSwell(edits);
                UIF.Grow(edits, Caption(edits, "EDIT",
                                    TextAnchor.MiddleCenter).transform);
                Tip.On(edits, "Pick the colors nodes and wires are drawn in");
                editBox = edits.GetComponent<Toggle>();
                if (editBox != null)
                {
                    hushed = true;
                    editBox.isOn = false;
                    hushed = false;
                    editBox.onValueChanged.AddListener(Editing);
                }
            }

            GameObject shut = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (shut != null)
            {
                shutRect = shut.GetComponent<RectTransform>();
                UIF.NoSwell(shut);
                Caption(shut, "", TextAnchor.MiddleCenter);
                // Red behind a white pin, as a switch that is on looks elsewhere in
                // the game.
                pinPlate = UIF.Plate(shut.transform, 1f, 1f, BarHeight - 8f,
                                 BarHeight - 8f, new Color(0f, 0f, 0f, 0f));
                Image lit = pinPlate.GetComponent<Image>();
                lit.raycastTarget = false;
                // Sized off the diagonal: turned 45 degrees, it is as tall as its
                // diagonal.
                pinIcon = Picture(shut.transform, Glyphs.Pin, (BarHeight - 6f) * 0.75f);
                // Turned, so it reads as a pin pushed into the corner of the window
                // rather than one standing on end.
                pinIcon.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, 45f);
                Paint();
                UIF.Grow(shut, pinIcon.transform, 1.15f);
                Tip.On(shut, "Keep the editor open");
                Button click = shut.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Pinning);
                }
            }

            Add(0, Place.Input, -1, "INPUT", "", null);
            Add(0, Place.Output, -1, "OUTPUT", "", null);
            Add(0, Place.Note, -1, "", "Comment", Glyphs.Note);
            // A timer is a row like a gate, and made the same way, but it is not one
            // of the game's gates: it goes before them.
            Add(Gates.Timer, -1, Gates.Timer, "", "Timer", Glyphs.Timer);
            for (int g = 0; g < Gates.Count; g++)
            {
                Add(g, -1, g, "", Gates.Names[g], null);
            }
            Swatches();
            Painted();

            GameObject board = UIF.Plate(window.transform, Margin, 0f, 10f, 10f,
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
            // Exactly the board, so the grid also shows where the board ends.
            mesh.anchorMin = new Vector2(0f, 1f);
            mesh.anchorMax = new Vector2(0f, 1f);
            mesh.pivot = new Vector2(0f, 1f);
            mesh.anchoredPosition = Vector2.zero;
            mesh.sizeDelta = new Vector2(BoardWide, BoardTall);
            paperRect = mesh;
            RawImage lines = paper.AddComponent<RawImage>();
            gridLines = lines;
            lines.texture = Glyphs.Grid;
            lines.color = new Color(1f, 1f, 1f, GridInk);
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
            fenceRect = edge;
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
                // Made thicker on the board as the board is zoomed out: see
                // `Railed`, which keeps it the same on screen.
                line.sizeDelta = flat ? new Vector2(0f, RailWide) : new Vector2(RailWide, 0f);
                rails.Add(line);
                line.anchoredPosition = Vector2.zero;
                // A RawImage with no texture is a plain filled rectangle, which is
                // all a line is.
                RawImage drawn = rail.AddComponent<RawImage>();
                drawn.color = new Color(1f, 1f, 1f, 0.75f);
                drawn.raycastTarget = false;
            }

            // Every wire as one mesh (see `WireMesh`), as big as the board so it is
            // not culled, and behind everything, as the pieces it replaces were.
            GameObject threads = new GameObject("Wires");
            threads.transform.SetParent(content, false);
            RectTransform spread = threads.AddComponent<RectTransform>();
            spread.anchorMin = new Vector2(0f, 1f);
            spread.anchorMax = new Vector2(0f, 1f);
            spread.pivot = new Vector2(0f, 1f);
            spread.anchoredPosition = Vector2.zero;
            spread.sizeDelta = new Vector2(BoardWide, BoardTall);
            wiresRect = spread;
            skein = threads.AddComponent<WireMesh>();
            skein.Thickness = WireWidth;
            skein.raycastTarget = false;
            threads.transform.SetAsFirstSibling();

            // How big the board is, in squares: on the row EDIT puts out, after the
            // wire's unicolour and before RESET COLORS.
            sizeWords = Words("CANVAS SIZE");
            cellsBox = SizeBox(out cellsRect, true);
            GameObject cross = UIF.Plate(window.transform, 0f, 0f, ByWide, UniTall,
                                         new Color(0f, 0f, 0f, 0f));
            byRect = cross.GetComponent<RectTransform>();
            UIF.Label(cross, "x", UIF.QuietInk, TextAnchor.MiddleCenter, 0, true);
            cross.SetActive(false);
            linesBox = SizeBox(out linesRect, false);
            Showing(true);
            // Out and away with the rest of that row.
            shelf.Add(sizeWords);
            shelf.Add(cellsRect);
            shelf.Add(byRect);
            shelf.Add(linesRect);

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
                    Marquee.On(prefixBox);
                }
                Tip.On(named, "Variable prefix");
            }

            // The middle button on the window itself -- its bar, its rows, the air
            // round the board -- rather than only on the board and the nodes: with
            // nothing listening there, a middle press went to the game underneath.
            Waving(window);

            Handles();
            Arrange();
        }

        /// <summary>The four corners, which the window is resized by.</summary>
        private void Handles()
        {
            // The thin border beside the board moves the window as the title bar
            // does: left, right and bottom, under the corners made next.
            for (int i = 0; i < 3; i++)
            {
                GameObject edge = new GameObject("Edge");
                edge.transform.SetParent(window.transform, false);
                RectTransform rect = edge.AddComponent<RectTransform>();
                Image catcher = edge.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);
                rect.anchorMin = new Vector2(i == 1 ? 1f : 0f, 0f);
                rect.anchorMax = new Vector2(i == 0 ? 0f : 1f, i == 2 ? 0f : 1f);
                rect.offsetMin = new Vector2(i == 1 ? -Margin : 0f, 0f);
                rect.offsetMax = new Vector2(i == 0 ? Margin : 0f,
                                             i == 2 ? Margin : -BarHeight);
                NodeDrag drag = edge.AddComponent<NodeDrag>();
                drag.frame = windowRect;
                drag.Moved = delegate(Vector2 by) { windowRect.anchoredPosition += by; };
            }
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

        /// <summary>Lays the window's pieces out from its size, when built and on
        /// every corner drag.</summary>
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
            // Along the bar from the left, each after the last.
            float along = 3f;
            along = Slot(editRect, along, bit * 2.4f);
            along = Slot(tidyRect, along, bit * 2.4f);
            along = Slot(fitRect, along, bit * 3.4f);
            along = Slot(importRect, along, bit * 3.0f);
            along = Slot(pinsRect, along, bit * 4.6f);
            along = Slot(exportRect, along, bit * 3.0f);
            if (shutRect != null)
            {
                UIF.Fit(shutRect, size.x - bit - 3f, 3f, bit, bit);
            }

            float y = BarHeight + Margin;
            float step = (size.x - Margin * 2f) / Hues.Slots;
            for (int i = 0; i < palette.Count; i++)
            {
                UIF.Fit(palette[i], Margin + step * i, y, step - 3f, 30f);
            }

            float top = y + 30f + Margin;
            if (editing)
            {
                // Between the palette and the board, which gives up the room; what
                // is drawn on it stays put (see `Editing`).
                float below = y + 30f + 4f;
                // Each button's colour straight under it.
                for (int i = 0; i < swatches.Count; i++)
                {
                    swatches[i].Fit(Margin + step * i, below, step - 3f, SwatchTall);
                }
                below += SwatchTall + 4f;
                // Then nodes and wires: how each is coloured, and the one colour
                // that mode uses, in a box only as wide as the hex it holds.
                float snug = uniNode != null ? uniNode.Snug(UniTall) : UniWide;
                float at = Margin + 6f;
                UIF.Fit(nodeWords, at, below, SideWords, UniTall);
                at += SideWords + 4f;
                UIF.Fit(nodeModeRect, at, below, ModeWide, UniTall);
                at += ModeWide + 6f;
                // The one colour straight beside the mode that uses it, and no word
                // between them: the mode says what the colour is for. Its room is
                // kept whether or not it is out, so the wire half of the row does
                // not shift about as the modes are clicked through.
                if (uniNode != null)
                {
                    uniNode.Fit(at, below, snug, UniTall);
                }
                at += snug + 32f;
                UIF.Fit(wireWords, at, below, SideWords, UniTall);
                at += SideWords + 4f;
                UIF.Fit(wireModeRect, at, below, ModeWide, UniTall);
                at += ModeWide + 6f;
                if (uniWire != null)
                {
                    uniWire.Fit(at, below, snug, UniTall);
                }
                // And at the far end of that row, putting all of them back.
                UIF.Fit(resetRect, size.x - Margin - ResetWide, below, ResetWide,
                        UniTall);
                // A row of its own under that one, from the left: how wires are
                // drawn, whether the grid holds the nodes, and how big the board
                // is. The board gives up the room for it, as it does for the two
                // rows above (see `Editing`), so the window's own back is what
                // shows behind all three.
                below += UniTall + 4f;
                float third = Margin + 6f;
                UIF.Fit(sizeWords, third, below, SizeWords, UniTall);
                third += SizeWords + 4f;
                UIF.Fit(cellsRect, third, below, SizeWide, UniTall);
                third += SizeWide + 2f;
                UIF.Fit(byRect, third, below, ByWide, UniTall);
                third += ByWide + 2f;
                UIF.Fit(linesRect, third, below, SizeWide, UniTall);
                // Clear air before the wire style: the numbers are one thing and
                // the two switches beyond them another.
                third += SizeWide + 30f;
                third = Laid(styleRect, third, StyleWide, below);
                third = Laid(gridRect, third, GridWide, below);
                // And the board back to the size it started at, at the far end of
                // its own row as RESET COLORS is of the row above.
                UIF.Fit(sizeBackRect, size.x - Margin - ResetWide, below, ResetWide,
                        UniTall);
                top = below + UniTall + Margin;
            }
            boardTop = top;
            // The same gap under the board as beside it.
            float bottom = size.y - Margin;
            if (boardRect != null)
            {
                UIF.Fit(boardRect, Margin, top, size.x - Margin * 2f, bottom - top);
            }

            if (nameRect != null)
            {
                // Beside the cross, with the window's furniture: whatever the
                // buttons leave, up to a comfortable width (see `LeastWidth`).
                float wide = Mathf.Clamp(size.x - bit - 6f - along, 0f, 180f);
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
            // A top corner pulled up or a bottom one down grows the window; `pull`
            // says which.
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
            // The wires follow the ports, which have only just moved: lay out
            // first.
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
            // What is drawn on the button is kept, in the palette's order, to be
            // coloured as the kind it offers.
            if (words.Length > 0)
            {
                Text shown = Caption(go, words, TextAnchor.MiddleCenter);
                UIF.Grow(go, shown.transform);
                inks.Add(shown);
            }
            else
            {
                Caption(go, "", TextAnchor.MiddleCenter);
                RawImage shown = Picture(go.transform,
                                         drawn != null ? drawn : Glyphs.Gate(gate),
                                         PaletteIcon);
                UIF.Grow(go, shown.transform);
                inks.Add(shown);
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

        /// <summary>A see-through ghost of the node dragged off the palette;
        /// nothing exists until it is dropped.</summary>
        private void Carrying(int kind, int gate, Vector2 screen)
        {
            if (canvas == null)
            {
                return;
            }
            // A gate carried over a gate lands on it; an end or a comment does not,
            // and neither does a gate over one of its own kind.
            Lighting(gate >= 0 ? Landing(screen, -1, gate) : -1);
            if (ghost == null)
            {
                Vector2 span = Sized(kind, gate);
                ghost = Rounded(canvas.transform, 0f, 0f, span.x, span.y,
                                new Color(Ink.r, Ink.g, Ink.b, 0.55f));
                bool timer = gate == Gates.Timer;
                Texture face = timer ? Glyphs.Timer
                    : (gate >= 0 ? Glyphs.Gate(gate)
                                 : (kind == Place.Note ? Glyphs.Note : null));
                if (face != null)
                {
                    RawImage drawn = Picture(ghost.transform, face,
                                             gate >= 0 && !timer ? GateIcon : NodeIcon);
                    Color shade = Shelf(gate >= 0 ? Hues.GateSlot(gate) : Hues.NoteSlot);
                    shade.a = 0.7f;
                    drawn.color = shade;
                }
                RectTransform rect = ghost.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                // At the size it will land, zoom included.
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
            int onto = gate >= 0 ? Landing(screen, -1, gate) : -1;
            Lighting(-1);
            if (onto >= 0)
            {
                // Let go on a gate: the new one is made where that one sits and
                // picks up its wires.
                int born = Born(kind, gate, Where(onto), true);
                if (born >= 0)
                {
                    Merged(born, onto);
                }
                return;
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                return;
            }
            // Put down by its middle, whatever its size.
            Vector2 span = Sized(kind, gate);
            Born(kind, gate, new Vector2(local.x - span.x * 0.5f,
                                         -local.y - span.y * 0.5f), true);
        }

        /// <summary>How big a node of this sort is drawn, before anything is
        /// written in it.</summary>
        private static Vector2 Sized(int kind, int gate)
        {
            if (gate == Gates.Timer)
            {
                return new Vector2(NodeWidth, NodeHeight);
            }
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

        /// <summary>The board's nodes: the rows in use, then the places that are
        /// not rows. A node's number indexes <see cref="ports"/> and the wires. No
        /// graph is stored: a gate is a row, and a wire is a row's input carrying
        /// another's answer.</summary>
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
                    // This block's board is this big. The static pair is what the
                    // placing and fencing helpers read, so it follows whichever
                    // layout was loaded last.
                    boardCells = board.Cells;
                    boardLines = board.Lines;
                    Boarded();
                }
                return board;
            }
        }

        private Wiring board;

        /// <summary>How many rows the board was last drawn with.</summary>
        private int counted;

        /// <summary>Writes the layout into its control and applies it (a live value
        /// alone is lost on save). No undo entry; <see cref="Kept"/> files
        /// one.</summary>
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

        /// <summary>Reads each comment's box back into its place before the layout
        /// is written, so a comment dragged mid-typing keeps its words.</summary>
        private void Voiced()
        {
            for (int i = 0; i < notes.Count && i < noted.Count; i++)
            {
                InputField box = notes[i];
                Place place = noted[i];
                if (box == null || place == null || place.Kind != Place.Note)
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

        /// <summary>The whole block as this frame began, so an undo puts everything
        /// back at once.</summary>
        private BlockInfo mark;
        private long marked;

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

        /// <summary>Files the edit as one undo step with the whole block either
        /// side (`UndoActionEdit`), not a step per control as `OnEditField`
        /// would.</summary>
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

        /// <summary>Makes input and output nodes for bindings that no row produces
        /// or reads, so a table typed by hand opens as a circuit. An end already
        /// there is not added twice.</summary>
        private void Adopt()
        {
            if (served == null)
            {
                return;
            }
            // With the answers indexed: unindexed, every input of every row asked
            // every other row.
            Sourced();
            try
            {
                Adopting();
            }
            finally
            {
                Unsourced();
            }
        }

        private void Adopting()
        {
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
                    // One name at a time: each is made by a node here, or arrives
                    // from the machine.
                    Asked(i, port, wanted, coding);
                    for (int n = 0; n < wanted.Count; n++)
                    {
                        string variable = wanted[n];
                        if (Answered(variable, KeyCode.None, i) >= 0
                            || Mine(variable))
                        {
                            // Made here, or a minted name whose gate is gone: a
                            // loose end, not an input.
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
                List<KeyCode> presses = Bindings.Codes(row.Emulate);
                if (said == null && presses.Count == 0)
                {
                    // A gate answering nothing stays so: a wire drawn out of it
                    // mints a name when one is needed. Minting here was an edit
                    // nobody asked for, and it undid undos.
                    continue;
                }
                if (said == null)
                {
                    // Each key it presses that nothing reads is an end of the board,
                    // as each such name is.
                    for (int c = 0; c < presses.Count; c++)
                    {
                        if (!Read(null, presses[c]))
                        {
                            Ensure(Place.Output, null, presses[c]);
                        }
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
                        // A minted name is a gate's answer, not something the
                        // machine listens for.
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
        /// Which nodes answer to each name and keycode, in node order: an index
        /// that <see cref="Sourced"/> builds for one operation, so TIDY's thousands
        /// of <see cref="Feeding"/> questions are lookups rather than string
        /// splits. It gives the loop's own answers, and nothing between Sourced and
        /// Unsourced writes a binding, so it cannot go stale.
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
                else
                {
                    // Every keycode it answers to: a gate may press several key
                    // outputs at once.
                    Codes(i, keying);
                    for (int k = 0; k < keying.Count; k++)
                    {
                        List<int> who;
                        if (!coded.TryGetValue((int)keying[k], out who))
                        {
                            who = new List<int>();
                            coded[(int)keying[k]] = who;
                        }
                        who.Add(i);
                    }
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

        /// <summary>What a port answers to: its names or its keycodes, which
        /// Besiege ORs. An end stands for one thing.</summary>
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

        /// <summary>Scratch lists for <see cref="Feeding"/>, kept rather than made
        /// per port per drawing.</summary>
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

        /// <summary>The first node answering to a binding, other than the one
        /// asking.
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
                if (Bindings.Carries(said, want) || (said == null && Strikes(i, key)))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Whether anything takes this node's answer, which fills its
        /// circle. An output end asks every row pressing its name, not only the
        /// first.</summary>
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

        /// <summary>Wires one node's answer into another's port by writing the
        /// name; a source that answers nothing is given a name first.</summary>
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
                // Both ends are ends of the board: no row can carry that wire, so a
                // gate goes between. Refused before anything is written.
                Told(to, 1, "cannot directly\nconnect to output");
                Strings();
                return;
            }
            LogicRow row = Row(to);
            List<MapperType> touched = already != null ? already
                                                       : new List<MapperType>();
            if (row != null && row.Ready)
            {
                string variable;
                KeyCode key;
                bool coin;
                if (!Carried(from, out variable, out key, out coin))
                {
                    // The key cannot take a name beside it, and something else
                    // answers to it: the wire would read both.
                    Told(from, 0, KeyAndName, NewsSeconds);
                    Strings();
                    return;
                }
                // Added beside what the input already answers to: an input takes
                // many wires, ORed.
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
                         : "that input is full\n" + Bindings.MostKeys + " keys");
                    Strings();
                    return;
                }
                // Only now is the source given a new name, when nothing is left to
                // refuse the wire.
                if (coin && !Coined(from, variable, touched))
                {
                    Bindings.Dropped(input, variable);
                    Told(from, 0, "that answer is full\n100 names");
                    Strings();
                    return;
                }
                touched.Add(input);
            }
            else
            {
                Place place = landing;
                LogicRow said = Row(from);
                if (place == null || place.Kind != Place.Output
                    || said == null || !said.Ready)
                {
                    return;
                }
                // Where both have a binding, the end's wins: somebody typed it. A
                // gate's idle names go when it gets an output, so its table cell
                // shows that output.
                List<string> idle = Idle(from);
                if (!place.Bound)
                {
                    // A gate on keys gives the end its first key. A gate on names
                    // gives it an unshared name -- an idle one or a new one --
                    // never a hidden name its gate wires carry.
                    KeyCode code = Bindings.IsVariable(said.Emulate)
                        ? KeyCode.None : Bindings.Code(said.Emulate);
                    if (code != KeyCode.None && Outputs(from, place))
                    {
                        // Its key is another output's already, and this one stays
                        // blank: only a hidden name could carry the wire, and a key
                        // cannot sit beside a name.
                        Told(to, 1, KeyAndName, NewsSeconds);
                        Strings();
                        return;
                    }
                    if (code != KeyCode.None)
                    {
                        place.Key = code;
                    }
                    else if (idle.Count > 0)
                    {
                        place.Variable = idle[0];
                    }
                    else
                    {
                        string name = Minted();
                        if (!Coined(from, name, touched))
                        {
                            Told(from, 0, "that answer is full\n100 names");
                            Strings();
                            return;
                        }
                        place.Variable = name;
                    }
                    Keep();
                }
                else if (place.Variable != null)
                {
                    // Beside the gate's other outputs, keeping the hidden name its
                    // gate wires carry. A Besiege key presses keys or names, never
                    // both.
                    if (!Bindings.IsVariable(said.Emulate)
                        && Bindings.Code(said.Emulate) != KeyCode.None)
                    {
                        Told(to, 1, KeyAndName, NewsSeconds);
                        Strings();
                        return;
                    }
                    if (!Bindings.Holds(said.Emulate, place.Variable)
                        && Bindings.Count(said.Emulate) - idle.Count
                           >= Bindings.MostNames)
                    {
                        Told(from, 0, "that answer is full\n100 names");
                        Strings();
                        return;
                    }
                    Shed(said.Emulate, idle);
                    Bindings.Added(said.Emulate, place.Variable);
                    touched.Add(said.Emulate);
                }
                else
                {
                    // The same with keys. Names cannot share a key with them unless
                    // every one of them is idle, and then they go.
                    if (Bindings.IsVariable(said.Emulate)
                        && Bindings.Count(said.Emulate) > idle.Count)
                    {
                        Told(to, 1, KeyAndName, NewsSeconds);
                        Strings();
                        return;
                    }
                    if (!Bindings.Holds(said.Emulate, place.Key)
                        && Bindings.Codes(said.Emulate).Count >= Bindings.MostKeys)
                    {
                        Told(from, 0, "that answer is full\n"
                             + Bindings.MostKeys + " keys");
                        Strings();
                        return;
                    }
                    Shed(said.Emulate, idle);
                    Bindings.Added(said.Emulate, place.Key);
                    touched.Add(said.Emulate);
                }
            }
            Commit(touched);
            Redraw();
        }

        /// <summary>
        /// What a wire out of this node carries: a binding nothing else answers to.
        /// An end stands for its own. A gate's names may be shared -- every gate on
        /// one output presses its name -- and a shared one reads all of them, so
        /// the first unshared name is used, or a new one is minted.
        /// </summary>
        /// <param name="coin">Whether that name has still to be given to the node
        /// by <see cref="Coined"/>: left to the caller, so a refusal leaves
        /// nothing.</param>
        /// <returns>False for a gate on a key something else also answers to: a key
        /// cannot take a name beside it.</returns>
        private bool Carried(int node, out string variable, out KeyCode key,
                             out bool coin)
        {
            coin = false;
            Answer(node, out variable, out key);
            if (Row(node) == null)
            {
                if (variable == null && key == KeyCode.None)
                {
                    variable = Minted();
                    coin = true;
                }
                return true;
            }
            if (variable == null && key != KeyCode.None)
            {
                // The first of its keys nothing else answers to. An output standing
                // for one does not count: an output feeds nothing.
                List<KeyCode> codes = new List<KeyCode>();
                Codes(node, codes);
                for (int c = 0; c < codes.Count; c++)
                {
                    bool shared = false;
                    for (int i = 0; i < Nodes && !shared; i++)
                    {
                        Place place = Placed(i);
                        shared = i != node
                            && (place == null || place.Kind != Place.Output)
                            && Strikes(i, codes[c]);
                    }
                    if (!shared)
                    {
                        key = codes[c];
                        return true;
                    }
                }
                return false;
            }
            List<string> names = Bindings.Named(variable);
            for (int i = 0; i < names.Count; i++)
            {
                if (!Elsewhere(node, names[i], KeyCode.None))
                {
                    variable = names[i];
                    return true;
                }
            }
            variable = Minted();
            coin = true;
            return true;
        }

        /// <summary>Adds a name beside a node's others; false at the name
        /// limit.</summary>
        private bool Coined(int node, string variable, List<MapperType> touched)
        {
            LogicRow row = Row(node);
            if (row == null || !row.Ready)
            {
                Named(node, variable);
                return true;
            }
            if (Bindings.IsVariable(row.Emulate))
            {
                if (!Bindings.Added(row.Emulate, variable))
                {
                    return false;
                }
            }
            else
            {
                Bindings.BindVariable(row.Emulate, variable);
            }
            touched.Add(row.Emulate);
            return true;
        }

        /// <summary>Every keycode a node answers to: the one an end stands for, or
        /// every one a gate presses.</summary>
        private void Codes(int node, List<KeyCode> into)
        {
            into.Clear();
            Place place = Placed(node);
            if (place != null)
            {
                if (place.Variable == null && place.Key != KeyCode.None)
                {
                    into.Add(place.Key);
                }
                return;
            }
            LogicRow row = Row(node);
            if (row != null && row.Ready)
            {
                into.AddRange(Bindings.Codes(row.Emulate));
            }
        }

        /// <summary>What <see cref="Codes"/> fills while the index is put up.
        /// </summary>
        private readonly List<KeyCode> keying = new List<KeyCode>();

        /// <summary>Whether a node answers to this keycode among its others: an end
        /// stands for one, a gate may press several.</summary>
        private bool Strikes(int node, KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return false;
            }
            Place place = Placed(node);
            if (place != null)
            {
                return place.Variable == null && place.Key == key;
            }
            LogicRow row = Row(node);
            return row != null && row.Ready && Bindings.Holds(row.Emulate, key);
        }

        /// <summary>A gate's minted names that no input reads and nothing else
        /// answers to: given when it is placed, dropped once it presses an
        /// output.</summary>
        private List<string> Idle(int node)
        {
            List<string> idle = new List<string>();
            LogicRow row = Row(node);
            if (row == null || !row.Ready || !Bindings.IsVariable(row.Emulate))
            {
                return idle;
            }
            List<string> names = Bindings.Named(Bindings.Variable(row.Emulate));
            for (int i = 0; i < names.Count; i++)
            {
                if (Mine(names[i]) && !Read(names[i], KeyCode.None)
                    && !Elsewhere(node, names[i], KeyCode.None))
                {
                    idle.Add(names[i]);
                }
            }
            return idle;
        }

        /// <summary>Takes a gate's idle names off what it presses.</summary>
        private static void Shed(MKey answer, List<string> idle)
        {
            for (int i = 0; i < idle.Count; i++)
            {
                Bindings.Dropped(answer, idle[i]);
            }
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
                // A key end: a row pressing it presses only keys, so the wire comes
                // off by taking this key away, and other key outputs stay.
                if (had != null || !Bindings.Holds(row.Emulate, place.Key))
                {
                    return;
                }
                Bindings.Dropped(row.Emulate, place.Key);
                touched.Add(row.Emulate);
                return;
            }
            if (!Bindings.Carries(had, place.Variable))
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

        /// <summary>A name nothing on the board answers to or reads: the prefix and
        /// the lowest free three-letter number (`ne_001_001`). Every name the board
        /// mints.
        /// </summary>
        private string Minted()
        {
            string start = served.Prefix;
            int letters = ComputerBehaviour.WireLetters;
            int most = ComputerBehaviour.Most(letters);
            for (int n = 1; n <= most; n++)
            {
                string wanted = start + ComputerBehaviour.Counted(n, letters);
                if (!Elsewhere(-1, wanted, KeyCode.None) && !Read(wanted, KeyCode.None))
                {
                    return wanted;
                }
            }
            return start + ComputerBehaviour.Counted(0, letters);
        }

        /// <summary>Takes what the board just wrote as read, so the watch does not
        /// treat the board's own edit as somebody else's. A "the next change is
        /// mine" flag swallowed the change after an edit the watch could not
        /// see.</summary>
        private void Ours()
        {
            read = Reading();
        }

        private void Commit(List<MapperType> touched)
        {
            Keep();
            // The rows are the block's own data: what was touched becomes their
            // text.
            LogicTable.Settle(served, touched);
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
        /// Draws the board again, rebuilding only the nodes that changed: each
        /// node's <see cref="Likeness"/> is held against what it was drawn from,
        /// and an unchanged node is only moved. An end or a comment is kept only
        /// while it is the same place object, as its controls write to that object.
        /// </summary>
        private void Redraw()
        {
            // Nothing drawn is lit: the nodes are about to be made or moved, and a
            // reused one would keep the colour.
            Lighting(-1);
            // The row count drawn, so a row arriving from elsewhere is noticed (see
            // `Ticking`).
            counted = Rows;
            // The nodes may be different ones now, or numbered differently.
            tinted = false;
            Cut();
            // The wires belong to the nodes as they were; `Wired` fills this again
            // at the end, and a board that returns early below has none.
            wired.Clear();
            wiredAt++;
            if (served == null || content == null)
            {
                Razed(0);
                return;
            }
            if (drawnFor != served)
            {
                // Another block's board: nothing drawn for that one is this one's.
                Razed(0);
                drawnFor = served;
            }
            if (prefixBox != null && !prefixBox.isFocused)
            {
                prefixBox.text = served.Prefix;
            }

            int many = Nodes;
            // The cross comes down wherever it is; `Pointing` puts it back on the
            // node the pointer is on, the next frame.
            Binned(binned, false);
            binned = -1;
            Reuse(many);
            Razed(many);
            // Three ports a node: its answer, then its two inputs.
            while (ports.Count < many * 3)
            {
                ports.Add(null);
            }
            // Every node's ports are asked whether they are wired, once to compare
            // and again to draw, and nothing in here writes a binding.
            Sourced();
            try
            {
                for (int i = 0; i < many; i++)
                {
                    string like = Likeness(i);
                    object id = Identity(i);
                    if (i < parts.Count && parts[i] != null && i < likes.Count
                        && likes[i] == like
                        && object.ReferenceEquals(drawnIds[i], id))
                    {
                        Vector2 at = Where(i);
                        (parts[i].transform as RectTransform).anchoredPosition =
                            new Vector2(at.x, -at.y);
                        continue;
                    }
                    Unmade(i);
                    Draw(i);
                    while (likes.Count <= i)
                    {
                        likes.Add(null);
                    }
                    while (drawnIds.Count <= i)
                    {
                        drawnIds.Add(null);
                    }
                    likes[i] = like;
                    drawnIds[i] = id;
                }
            }
            finally
            {
                Unsourced();
            }
            // A kept node keeps its edges, and a picked-out node drawn anew was
            // given one by `Draw`; this makes both agree with the selection.
            Rims();
            Wired();
            Strings();
        }

        /// <summary>The block the drawn nodes were drawn for.</summary>
        private ComputerBehaviour drawnFor;

        /// <summary>What each drawn node was drawn from, and the place it was drawn
        /// for -- null for a gate. See <see cref="Redraw"/>.</summary>
        private readonly List<string> likes = new List<string>();
        private readonly List<object> drawnIds = new List<object>();

        /// <summary>A drawn node's number, which its handlers read when they fire:
        /// a node renumbered by a removal is moved to its new number rather than
        /// drawn again (see <see cref="Reuse"/>).</summary>
        private class Tag
        {
            public int Node;
        }

        private readonly List<Tag> tags = new List<Tag>();

        private readonly System.Text.StringBuilder likeness =
            new System.Text.StringBuilder();

        /// <summary>Everything a node is drawn from, as a string to compare:
        /// colour, gate and switch, timer settings, what an end shows, a comment's
        /// words and size, and which ports are filled. Anything `Draw` reads must
        /// be in here; the position is not.
        /// </summary>
        private string Likeness(int node)
        {
            likeness.Length = 0;
            Color32 ink = Tinted(node);
            likeness.Append(ink.r).Append(',').Append(ink.g).Append(',')
                .Append(ink.b).Append(',').Append(ink.a).Append('|');
            LogicRow row = Row(node);
            if (row != null)
            {
                likeness.Append('g').Append(row.Gate).Append(row.Switch ? '+' : '-');
                if (row.IsTimer)
                {
                    likeness.Append(UIF.Seconds(row.Wait.Value)).Append('/')
                        .Append(UIF.Seconds(row.Duration.Value))
                        .Append(row.Hold.IsActive ? 'h' : '_')
                        .Append(row.Stop.IsActive ? 's' : '_')
                        .Append(row.Loop.IsActive ? 'l' : '_');
                }
            }
            else
            {
                Place place = Placed(node);
                if (place != null)
                {
                    // Nothing and an empty name are two different cells: a key,
                    // and a name with nothing typed into it yet.
                    string shown = Shown(node, place.Variable, place.Key);
                    likeness.Append('p').Append(place.Kind).Append('|')
                        .Append(shown == null ? 'k' : 'v').Append(shown).Append('|')
                        .Append((int)place.Key).Append('|').Append(place.Size)
                        .Append('|').Append(place.Words);
                }
            }
            likeness.Append('|').Append(Filled(node, 0, true) ? '1' : '0')
                .Append(Filled(node, 0, false) ? '1' : '0')
                .Append(Filled(node, 1, false) ? '1' : '0');
            return likeness.ToString();
        }

        /// <summary>Takes down every node from <paramref name="keep"/> on, and
        /// forgets what was drawn for them.</summary>
        private void Razed(int keep)
        {
            for (int i = keep; i < parts.Count; i++)
            {
                if (parts[i] != null)
                {
                    Destroy(parts[i]);
                }
            }
            Shortened(parts, keep);
            Shortened(marks, keep);
            Shortened(rims, keep);
            Shortened(notes, keep);
            Shortened(noted, keep);
            Shortened(bins, keep);
            Shortened(grips, keep);
            Shortened(likes, keep);
            Shortened(drawnIds, keep);
            Shortened(tags, keep);
            Shortened(ports, keep * 3);
            if (under >= keep)
            {
                under = -1;
            }
        }

        /// <summary>What a node is, whatever its number: its row, or its place.
        /// </summary>
        private object Identity(int node)
        {
            LogicRow row = Row(node);
            if (row != null)
            {
                return row;
            }
            return Placed(node);
        }

        /// <summary>
        /// Moves every drawn node to the number it has now, found by what it is. A
        /// removal renumbers everything after it and a new gate every end, and
        /// drawing all of those again was most of what an add or a delete cost on a
        /// big board; a node that still looks the same is then only moved (see
        /// <see cref="Redraw"/>). Handlers read the node's <see cref="Tag"/>, the
        /// few things holding a number are given the new one, and the cross and the
        /// resizing corner, made with the old one, are made again when wanted.
        /// </summary>
        private void Reuse(int many)
        {
            Dictionary<object, int> was = new Dictionary<object, int>();
            for (int j = 0; j < parts.Count && j < drawnIds.Count; j++)
            {
                if (parts[j] != null && drawnIds[j] != null
                    && !was.ContainsKey(drawnIds[j]))
                {
                    was[drawnIds[j]] = j;
                }
            }
            int[] from = new int[many];
            bool[] kept = new bool[parts.Count];
            bool moved = parts.Count > many;
            for (int i = 0; i < many; i++)
            {
                object id = Identity(i);
                int j;
                from[i] = id != null && was.TryGetValue(id, out j) ? j : -1;
                if (from[i] >= 0)
                {
                    kept[from[i]] = true;
                }
                if (from[i] != i && (from[i] >= 0 || i < parts.Count))
                {
                    moved = true;
                }
            }
            if (!moved)
            {
                return;
            }
            for (int j = 0; j < parts.Count; j++)
            {
                if (!kept[j] && parts[j] != null)
                {
                    Destroy(parts[j]);
                }
            }
            int wasUnder = under;
            int wasSizing = sizing;
            under = -1;
            sizing = -1;
            Reorder(parts, from, 1);
            Reorder(marks, from, 1);
            Reorder(rims, from, 1);
            Reorder(notes, from, 1);
            Reorder(noted, from, 1);
            Reorder(bins, from, 1);
            Reorder(grips, from, 1);
            Reorder(likes, from, 1);
            Reorder(drawnIds, from, 1);
            Reorder(tags, from, 1);
            Reorder(ports, from, 3);
            for (int i = 0; i < many; i++)
            {
                if (from[i] < 0)
                {
                    continue;
                }
                if (from[i] == wasUnder)
                {
                    under = i;
                }
                if (from[i] == wasSizing)
                {
                    sizing = i;
                }
                if (from[i] == i)
                {
                    continue;
                }
                if (tags[i] != null)
                {
                    tags[i].Node = i;
                }
                Hover watch = parts[i] == null ? null : parts[i].GetComponent<Hover>();
                if (watch != null)
                {
                    watch.Node = i;
                }
                for (int k = 0; k < 3; k++)
                {
                    RectTransform port = ports[i * 3 + k];
                    PortMark mark = port == null ? null : port.GetComponent<PortMark>();
                    if (mark != null)
                    {
                        mark.Node = i;
                    }
                }
                if (bins[i] != null)
                {
                    Destroy(bins[i]);
                    bins[i] = null;
                }
                if (grips[i] != null)
                {
                    Destroy(grips[i]);
                    grips[i] = null;
                }
            }
        }

        /// <summary>A list of per-node things in the new order: entry <c>i</c> is
        /// what node <c>from[i]</c> had, or nothing, <paramref name="stride"/> to a
        /// node.</summary>
        private static void Reorder<T>(List<T> list, int[] from, int stride)
            where T : class
        {
            T[] old = list.ToArray();
            list.Clear();
            for (int i = 0; i < from.Length; i++)
            {
                for (int k = 0; k < stride; k++)
                {
                    int j = from[i] < 0 ? -1 : from[i] * stride + k;
                    list.Add(j >= 0 && j < old.Length ? old[j] : null);
                }
            }
        }

        /// <summary>Takes one node down to be drawn again, with everything made on
        /// it.
        /// </summary>
        private void Unmade(int node)
        {
            if (node < parts.Count && parts[node] != null)
            {
                Destroy(parts[node]);
                parts[node] = null;
            }
            Forgot(marks, node);
            Forgot(rims, node);
            Forgot(notes, node);
            Forgot(noted, node);
            Forgot(bins, node);
            Forgot(grips, node);
            for (int port = 0; port < 3; port++)
            {
                Forgot(ports, node * 3 + port);
            }
            if (under == node)
            {
                under = -1;
            }
        }

        private static void Shortened<T>(List<T> list, int keep)
        {
            if (list.Count > keep)
            {
                list.RemoveRange(keep, list.Count - keep);
            }
        }

        private static void Forgot<T>(List<T> list, int at) where T : class
        {
            if (at >= 0 && at < list.Count)
            {
                list[at] = null;
            }
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
            // A gate is its picture, its switch and its ports; the two ends and a
            // timer have things written on them, so only they need the room for it.
            bool gated = row != null && !row.IsTimer;
            float wide = gated ? GateWidth : NodeWidth;
            float tall = gated ? GateHeight : NodeHeight;

            GameObject body = Rounded(content, at.x, at.y, wide, tall,
                                      note ? Paper : Ink);
            while (parts.Count <= index)
            {
                parts.Add(null);
            }
            parts[index] = body;

            while (tags.Count <= index)
            {
                tags.Add(null);
            }
            Tag me = new Tag();
            me.Node = index;
            tags[index] = me;
            // The dashed edges are made when first wanted: nine objects a node,
            // rarely needed.
            while (marks.Count <= index)
            {
                marks.Add(null);
                rims.Add(null);
                notes.Add(null);
            }
            marks[index] = null;
            rims[index] = null;
            notes[index] = null;
            while (noted.Count <= index)
            {
                noted.Add(null);
            }
            noted[index] = null;
            while (bins.Count <= index)
            {
                bins.Add(null);
            }
            bins[index] = null;
            while (grips.Count <= index)
            {
                grips.Add(null);
            }
            grips[index] = null;
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

            // The middle button pans from anywhere, node or not.
            Waving(body);

            NodeDrag drag = body.AddComponent<NodeDrag>();
            drag.Held = delegate { Holding(me.Node, drag); };
            drag.Moved = delegate(Vector2 by)
            {
                Hauling(new Vector2(by.x, -by.y));
            };
            drag.Dropped = delegate
            {
                dragging = false;
                hauler = null;
                int onto = landing;
                Lighting(-1);
                if (onto >= 0 && me.Node < Rows && onto != me.Node)
                {
                    Merged(me.Node, onto);
                    return;
                }
                Kept();
            };

            if (note)
            {
                Comment(body, me, place, wide, tall);
                return;
            }

            // The heading is drawn, not spawned (a prefab brings its own plate),
            // and takes no clicks, so a click on the node picks it. A gate with a
            // switch keeps its plate's right clear of the answer port's reach.
            bool switched = row != null && Gates.UsesMode(row.Gate);
            float switchAt = wide - PortSize - (PortReach - PortSize) * 0.5f
                             - Switch;

            GameObject head = new GameObject("Head");
            head.transform.SetParent(body.transform, false);
            head.AddComponent<RectTransform>();
            if (row != null && row.IsTimer)
            {
                // No picture: a timer on the board is its numbers and its switches.
                Timed(body, row, index, wide, tall);
            }
            else if (row != null)
            {
                // The picture has the width, less the switch where there is one.
                float room = switched ? switchAt - PortSize - 2f
                                      : wide - PortSize * 2f - 4f;
                UIF.Fit(head.GetComponent<RectTransform>(), PortSize + 2f, 3f,
                        room, tall - 6f);
                Picture(head.transform, Glyphs.Gate(row.Gate), GateIcon).color =
                    Tinted(index);
            }
            else
            {
                // The word above and the cell below, clear of the port's side; the
                // word centred.
                UIF.Fit(head.GetComponent<RectTransform>(), 0f, 5f, wide, 24f);
                Text word = Caption(head,
                                    place.Kind == Place.Input ? "INPUT" : "OUTPUT",
                                    TextAnchor.MiddleCenter);
                if (word != null)
                {
                    // Room for a bigger word now that the node is two cells tall.
                    word.resizeTextMaxSize = 18;
                    word.color = Tinted(index);
                }
            }

            if (row != null)
            {
                // The switch, for gates with one: inverted for the edge detector,
                // toggle mode for the rest, none for memory gates.
                if (Gates.UsesMode(row.Gate))
                {
                    GameObject mode = UIF.Spawn(UIF.TogglePrefab, body.transform);
                    if (mode != null)
                    {
                        // Beside the answer port, measured from the gate's own
                        // width.
                        UIF.Fit(mode.GetComponent<RectTransform>(), switchAt,
                                (tall - Switch) * 0.5f, Switch, Switch);
                        UIF.NoSwell(mode);
                        Text letter = Caption(mode, Gates.ModeLetter(row.Gate),
                                              TextAnchor.MiddleCenter);
                        UIF.Grow(mode, letter.transform, 1.3f);
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
                // Dragged by its name box as well as by its plate, the way a comment
                // is dragged by its writing: the box is most of the node.
                cell.Fielded = delegate(InputField field)
                {
                    Haul(field, me, body, cell);
                };
                cell.Load(Shown(index, place.Variable, place.Key), place.Key);
                Place mine = place;
                cell.Changed = delegate(KeyCell edited)
                {
                    Rebind(me.Node, mine, edited);
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
                // Two inputs at a quarter and three quarters down; one in the
                // middle.
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

        /// <summary>Where an end's cell starts: clear of the port, which is on an
        /// input's right and an output's left.</summary>
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

        /// <summary>The red cross that removes a node, on the hovered node's corner
        /// only. Pointing at it still counts as pointing at the node: pointer
        /// events reach every parent.</summary>
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
            // The label only shrinks to fit, up to its own size, so the size is
            // given as well as the room.
            cross.fontSize = 28;
            cross.resizeTextMaxSize = 28;
            Button click = bin.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.targetGraphic = catcher;
            UIF.Grow(bin, cross.transform, 1.4f);
            click.onClick.AddListener(delegate
            {
                // A control-click picks the node; picking and deleting at once is
                // neither gesture.
                if (!Picking())
                {
                    Kill(me);
                }
            });
            return bin;
        }

        /// <summary>A node dragged by a text box on it: the node's drag sits beside
        /// the box's own, and the box is made non-interactable meanwhile, so it
        /// does not select.
        /// </summary>
        private void Haul(InputField field, Tag me, GameObject body, KeyCell cell)
        {
            // Greyed while out of reach otherwise, which is a flicker every move.
            field.transition = Selectable.Transition.None;
            Waving(field.gameObject);
            NodeDrag carry = field.gameObject.AddComponent<NodeDrag>();
            carry.frame = body.GetComponent<RectTransform>();
            carry.Held = delegate
            {
                // A typed name not yet taken: the drag is selecting text, not
                // moving the node.
                if (cell != null && Bindings.Tidied(field.text) != cell.Variable)
                {
                    return;
                }
                field.interactable = false;
                Holding(me.Node, carry);
            };
            carry.Moved = delegate(Vector2 by)
            {
                if (hauler == carry)
                {
                    Hauling(new Vector2(by.x, -by.y));
                }
            };
            carry.Dropped = delegate
            {
                field.interactable = true;
                if (hauler != carry)
                {
                    return;
                }
                dragging = false;
                hauler = null;
                Kept();
            };
        }

        /// <summary>How much bigger a comment is drawn than at its starting size;
        /// its margins, slack and limits scale by it.</summary>
        private static float Grown(Text drawn)
        {
            return drawn == null ? 1f : Mathf.Max(1, drawn.fontSize) / (float)NoteFont;
        }

        /// <summary>A comment's label kept inside its box by a margin that grows
        /// with the lettering.</summary>
        private static void Inset(Text label)
        {
            if (label == null)
            {
                return;
            }
            float edge = TextEdge * Grown(label);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // Down past the margin by a line's descent and a little: a multi-line
            // field drops a last line that does not fit in its label.
            float under = Leading(label) * label.fontSize + 2f;
            rect.offsetMin = new Vector2(edge, edge - under);
            rect.offsetMax = new Vector2(-edge, -edge);
        }

        /// <summary>A comment's starting font size, and the smallest its corner
        /// takes it; how large is limited by width (<see
        /// cref="NoteMostWide"/>).</summary>
        private const int NoteFont = 14;
        private const int NoteSmallest = 8;
        private const int NoteLargest = 20000;

        /// <summary>The widest a comment's corner can take it: three quarters of
        /// the board.
        /// </summary>
        private static float NoteMostWide { get { return BoardWide * 0.75f; } }

        /// <summary>The largest font a comment is set at; past it the box is scaled
        /// instead. A dynamic font's texture cannot hold letters thousands of
        /// pixels high.</summary>
        private const int NoteCrisp = 200;

        private static int Drawn(int size)
        {
            return Mathf.Min(size, NoteCrisp);
        }

        /// <summary>The scale a comment's box is drawn at to make up what its font
        /// is not set at.</summary>
        private static Vector3 Magnified(int size)
        {
            float by = size / (float)Mathf.Max(1, Drawn(size));
            return new Vector3(by, by, 1f);
        }

        private static Font leadingFont;
        private static float leadingShare;

        /// <summary>The share of a line below its lowest letter, measured once from
        /// a line of the deepest letters (the generator's last quad is the caret,
        /// and left out).
        /// </summary>
        private static float Leading(Text drawn)
        {
            if (drawn == null || drawn.font == null)
            {
                return 0f;
            }
            if (leadingFont == drawn.font)
            {
                return leadingShare;
            }
            leadingFont = drawn.font;
            leadingShare = 0f;
            try
            {
                TextGenerationSettings asked = drawn.GetGenerationSettings(Vector2.zero);
                asked.fontSize = 100;
                asked.scaleFactor = 1f;
                asked.resizeTextForBestFit = false;
                asked.textAnchor = TextAnchor.UpperLeft;
                asked.horizontalOverflow = HorizontalWrapMode.Overflow;
                asked.verticalOverflow = VerticalWrapMode.Overflow;
                TextGenerator maker = new TextGenerator();
                float line = maker.GetPreferredHeight("Hgjpqy_,;", asked);
                IList<UIVertex> corners = maker.verts;
                int count = corners.Count - 4;
                float lowest = 0f;
                for (int i = 0; i < count; i++)
                {
                    lowest = Mathf.Min(lowest, corners[i].position.y);
                }
                if (count > 0 && line > 0f)
                {
                    // Held to a sane share whatever the font says: a guess wrong by
                    // a lot would cut the writing off rather than tidy it.
                    leadingShare = Mathf.Clamp((line + lowest) / 100f, 0f, 0.4f);
                }
            }
            catch (Exception)
            {
                leadingShare = 0f;
            }
            return leadingShare;
        }

        /// <summary>That scale read back off a comment's label -- its box is its
        /// parent.</summary>
        private static float Magnification(Text drawn)
        {
            Transform box = drawn == null ? null : drawn.transform.parent;
            return box == null || box.localScale.x < 0.001f ? 1f : box.localScale.x;
        }

        /// <summary>A comment's writing at a size: the font up to <see
        /// cref="NoteCrisp"/>, the box's scale past it, and the margins that go
        /// with it.</summary>
        private static void Rescaled(Place place, InputField box, int size)
        {
            place.Size = size == NoteFont ? 0 : size;
            box.transform.localScale = Magnified(size);
            Text laid = box.textComponent;
            if (laid != null)
            {
                laid.fontSize = Drawn(size);
                Inset(laid);
            }
            Text ghostText = box.placeholder as Text;
            if (ghostText != null)
            {
                ghostText.fontSize = Drawn(size);
                Inset(ghostText);
            }
        }

        private static int Lettering(Place place)
        {
            return place != null && place.Size > 0 ? place.Size : NoteFont;
        }

        /// <summary>A comment's corner arrow, resizing it by its lettering, which
        /// the box follows. Shown with the cross, on the hovered comment.</summary>
        private GameObject Resizer(GameObject host, int me)
        {
            GameObject grip = new GameObject("Grip");
            grip.transform.SetParent(host.transform, false);
            RectTransform rect = grip.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(GripSize, GripSize);
            rect.anchoredPosition = Vector2.zero;
            Image catcher = grip.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            // The double arrow the window's own corners put on the pointer, the
            // way this corner is pulled: top left to bottom right.
            RawImage arrows = Picture(grip.transform, Glyphs.Resizer(true), GripArrows);
            arrows.raycastTarget = false;
            UIF.Grow(grip, arrows.transform, 1.3f);
            Waving(grip);

            RectTransform plate = host.GetComponent<RectTransform>();
            NodeDrag pull = grip.AddComponent<NodeDrag>();
            // Measured on the board, which is what the comment is sized in.
            pull.frame = plate;
            Place held = null;
            InputField box = null;
            Vector2 span = Vector2.one;
            Vector2 pulled = Vector2.zero;
            int from = NoteFont;
            pull.Held = delegate
            {
                held = Placed(me);
                box = me < notes.Count ? notes[me] : null;
                if (held == null || box == null)
                {
                    return;
                }
                // Whatever is being typed is the comment's from here on: the box
                // is about to change size under it.
                held.Words = box.text == null ? "" : box.text;
                sizing = me;
                from = Lettering(held);
                span = plate.sizeDelta;
                pulled = Vector2.zero;
            };
            pull.Moved = delegate(Vector2 step)
            {
                if (held == null || box == null)
                {
                    return;
                }
                // Down and to the right grows it; the board's y runs up. As much
                // bigger as the corner has been pulled, across and down together.
                pulled += new Vector2(step.x, -step.y);
                float grown = ((span.x + pulled.x) / Mathf.Max(1f, span.x)
                               + (span.y + pulled.y) / Mathf.Max(1f, span.y)) * 0.5f;
                int size = Mathf.Clamp(Mathf.RoundToInt(from * grown), NoteSmallest,
                                       NoteLargest);
                int before = Lettering(held);
                if (size == before)
                {
                    return;
                }
                RectTransform inner = box.GetComponent<RectTransform>();
                Rescaled(held, box, size);
                Stretch(host, inner, box.textComponent, box.text);
                if (size > before && (plate.sizeDelta.x >= NoteMostWide - 0.5f
                                      || plate.sizeDelta.y >= BoardTall - 0.5f))
                {
                    // At the size limit, where `Measure` holds the box, the pull
                    // stops growing it.
                    Rescaled(held, box, before);
                    Stretch(host, inner, box.textComponent, box.text);
                }
                box.ForceLabelUpdate();
                // Grown past the board's edge, it is moved back onto the board.
                Put(me, Where(me));
            };
            pull.Dropped = delegate
            {
                sizing = -1;
                Binned(me, under == me);
                // One step of undo for the whole pull.
                if (held != null && Lettering(held) != from)
                {
                    Kept();
                }
            };
            return grip;
        }

        /// <summary>A comment's paper: lighter than a node, a note laid on the
        /// board.
        /// </summary>
        private static readonly Color Paper = new Color(0.16f, 0.20f, 0.26f, 0.96f);

        /// <summary>The clear space round a comment's text, and the widest it grows
        /// before it wraps.</summary>
        private const float NoteEdge = 3f;

        /// <summary>And the clear space inside the text box itself, between its own
        /// dark plate and the letters on it.</summary>
        private const float TextEdge = 8f;
        private const float NoteWide = 340f;

        /// <summary>And the narrowest. A comment of one short word should be one
        /// short word wide -- it is a note, not a node.</summary>
        private const float NoteLeast = 62f;

        /// <summary>How much room a comment wants, asked of the font: a guessed
        /// width grows in whole lines and wraps words that fit.</summary>
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
            // Both paddings and some slack: text measured to its exact width still
            // wraps. The margin and wrap width grow with the lettering; the border
            // and slack do not. Measured in the label's units, scaled back to the
            // plate's past `NoteCrisp`.
            float by = Magnification(drawn);
            float grown = Grown(drawn) * by;
            float pad = (NoteEdge + TextEdge * grown) * 2f;
            // Never wider than a comment may be, nor taller than the board: a
            // comment is kept on the board, and one bigger than it could not be.
            wide = Mathf.Clamp(raw * by + pad + 6f * by, NoteLeast,
                               Mathf.Min(NoteWide * grown, NoteMostWide));
            // And how tall it is once wrapped to that width, which is the width the
            // text itself is laid out in.
            float room = (wide - pad) / by;
            float high = maker.GetPreferredHeight(asked,
                             drawn.GetGenerationSettings(
                                 new Vector2(room, 0f))) / much;
            // Less the space the last line keeps under its letters: a band of empty
            // box at heading sizes.
            float spare = Leading(drawn) * drawn.fontSize * by;
            tall = Mathf.Clamp(high * by + pad - spare, 34f,
                               Mathf.Min(420f * grown, BoardTall));
        }

        /// <summary>A comment node: a plate with a text box, and no
        /// ports.</summary>
        private void Comment(GameObject body, Tag tag, Place place,
                             float wide, float tall)
        {
            int index = tag.Node;
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
            Inked(field.textComponent, Tinted(index), Drawn(Lettering(place)));
            Inked(ghostText, UIF.QuietInk, Drawn(Lettering(place)));
            box.transform.localScale = Magnified(Lettering(place));
            if (ghostText != null)
            {
                ghostText.text = "comment";
            }
            field.text = place.Words == null ? "" : place.Words;
            notes[index] = field;
            noted[index] = place;
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
            Tag me = tag;
            GameObject plate = body;
            RectTransform inner = box.GetComponent<RectTransform>();

            // A click on the writing places the caret; a drag moves the node. Both
            // are on the field: the node's drag sits beside the field's own, with
            // the field made non-interactable meanwhile (`MayDrag` checks), so it
            // does not also select.
            InputField dragged = field;
            NodeDrag carry = box.AddComponent<NodeDrag>();
            carry.frame = body.GetComponent<RectTransform>();
            carry.Held = delegate
            {
                // The box's text becomes the comment's first: going
                // non-interactable drops focus, and the onEndEdit that follows
                // would otherwise redraw under the drag.
                mine.Words = dragged.text == null ? "" : dragged.text;
                dragged.interactable = false;
                Holding(me.Node, carry);
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
            // The size it actually wants, now that there is a font to ask about it
            // -- and on the board at that size, wherever it was left or pasted.
            Stretch(body, inner, field.textComponent, field.text);
            Put(me.Node, Where(me.Node));
            // Resized while typing, so the box grows under the hand; the layout is
            // written when typing ends, one undo step per comment.
            Text laid = field.textComponent;
            InputField typing = field;
            field.onValueChanged.AddListener(delegate(string typed)
            {
                Stretch(plate, inner, laid, typed);
                Put(me.Node, Where(me.Node));
                // The caret uses the last layout, and the box has just resized:
                // without this it lags a line behind.
                typing.ForceLabelUpdate();
            });
            field.onEndEdit.AddListener(delegate(string typed)
            {
                Written(me.Node, mine, typed);
            });
        }

        /// <summary>Sizes a comment's plate to its text without a redraw, which
        /// would take the box from the hand typing.</summary>
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
                // The plate's own edge is the comment's border and stays one
                // thickness whatever the lettering is.
                float edge = NoteEdge;
                // In the box's own units, which its scale -- see `NoteCrisp` --
                // makes bigger than the plate's.
                float by = Magnification(drawn);
                inner.anchoredPosition = new Vector2(edge, -edge);
                inner.sizeDelta = new Vector2((wide - edge * 2f) / by,
                                              (tall - edge * 2f) / by);
            }
            // Both dashed edges stretch with the plate; only their dash count
            // changes.
            for (int i = 0; i < body.transform.childCount; i++)
            {
                Transform kid = body.transform.GetChild(i);
                if (kid.name == "Dashes")
                {
                    Dashes(kid.gameObject, wide + 4f, tall + 4f);
                }
            }
        }

        /// <summary>A comment label laid out to fill its field exactly, with best
        /// fit off: the wrap width and the measured width must agree.</summary>
        private static void Inked(Text label, Color colour, int size)
        {
            if (label == null)
            {
                return;
            }
            UIF.Style(label, colour, TextAnchor.UpperLeft);
            label.resizeTextForBestFit = false;
            label.fontSize = size;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            Inset(label);
        }

        private void Written(int node, Place place, string typed)
        {
            string said = typed == null ? "" : typed;
            if (said == place.Words)
            {
                return;
            }
            place.Words = said;
            // No redraw: the plate kept pace while typing, and redrawing on lost
            // focus took whatever had been pressed out from under the hand.
            Kept();
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
                if (place.Kind != Place.Output || !place.Bound || Mine(was))
                {
                    // An emptied input is waiting to be typed in: switching key to
                    // name clears it, and unbinding cut every wire before the name
                    // was typed. An output already blank stays so.
                    return;
                }
                // An emptied output goes blank: its wires move onto a hidden name,
                // shown as nothing, so any gate can be wired to it again and the
                // next thing typed takes them along.
                wanted = Minted();
            }
            if (Doubled(node, place.Kind, wanted, wantedKey))
            {
                Warned(cell.transform as RectTransform, "already assigned!");
                // Another end already stands for this: refused, and the cell shows
                // what this end is.
                cell.Load(Shown(node, was, wasKey), wasKey);
                return;
            }
            place.Variable = wanted;
            place.Key = wantedKey;
            if (was == place.Variable && wasKey == place.Key)
            {
                // Only the mode changed; a redraw took the cell from under the
                // click.
                return;
            }

            // Wires follow the rename, unless something else already answers to the
            // new name: they would join that node as well.
            bool carry = !Elsewhere(node, place.Variable, place.Key,
                place.Kind == Place.Output ? Place.Input : Place.Output);
            List<MapperType> touched = new List<MapperType>();
            if (carry && place.Kind == Place.Output)
            {
                // An output's wires are the rows pressing its name; without this a
                // renamed output lost them.
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
                    // The one name swapped for the new; the input's other wires
                    // stay. Where the new one will not fit (keys against names),
                    // the old one just comes off.
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

        /// <summary>Whether another end already stands for this binding. A gate and
        /// an end sharing a name is a wire, not a clash.</summary>
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
                // Two ends of one kind never share a binding: they would be one end
                // drawn twice. An input and an output may -- the machine then feeds
                // itself through that binding, which is the player's call. An
                // output may take a gate's name -- that is how the gate feeds it --
                // but an input may not.
                bool end = place != null && place.Kind == kind;
                bool clash = end || (place == null && kind == Place.Input);
                if (!clash)
                {
                    continue;
                }
                string other;
                KeyCode otherKey;
                Answer(i, out other, out otherKey);
                if (variable != null ? Bindings.Carries(other, variable)
                                     : (other == null && otherKey == key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a gate already presses a bound output end other than
        /// this one.</summary>
        private bool Outputs(int node, Place except)
        {
            for (int i = 0; i < Board.Places.Count && node < Rows; i++)
            {
                Place place = Board.Places[i];
                if (place != except && place.Kind == Place.Output && place.Bound
                    && Presses(node, place))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether anything else on the board already answers to this
        /// binding; asked before wires are moved onto a name.</summary>
        private bool Elsewhere(int node, string variable, KeyCode key)
        {
            return Elsewhere(node, variable, key, -1);
        }

        /// <param name="ignored">A kind of end not counted: an output rebound to an
        /// input's binding still carries its wires, and the other way round.</param>
        private bool Elsewhere(int node, string variable, KeyCode key, int ignored)
        {
            if (variable == null && key == KeyCode.None)
            {
                return false;
            }
            for (int i = 0; i < Nodes; i++)
            {
                Place end = Placed(i);
                if (i == node || (end != null && end.Kind == ignored))
                {
                    continue;
                }
                string other;
                KeyCode otherKey;
                Answer(i, out other, out otherKey);
                bool same = variable != null
                    ? Bindings.Carries(other, variable)
                    : (other == null && Strikes(i, key));
                if (same)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A row that pressed an end presses what the end stands for now.
        /// A key cannot share with names, so a row pressing other names keeps them
        /// and loses this wire.</summary>
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
                if (!Bindings.Carries(had, was))
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
            if (had != null || !Bindings.Holds(row.Emulate, wasKey))
            {
                return;
            }
            if (place.Variable == null)
            {
                // One key for another, and every other key it presses stays.
                Bindings.Dropped(row.Emulate, wasKey);
                Bindings.Added(row.Emulate, place.Key);
            }
            else if (Bindings.Count(row.Emulate) == 1)
            {
                Bindings.BindVariable(row.Emulate, place.Variable);
            }
            else
            {
                // Pressing other keys too, which a name cannot share with: this one
                // wire comes off rather than every other.
                Bindings.Dropped(row.Emulate, wasKey);
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
            // Where it went after snapping and fencing, so what is drawn is what is
            // saved.
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
            return Fenced(at, new Vector2(NodeWidth, NodeHeight));
        }

        /// <summary>Fenced with room for a node that size: a big comment fenced as
        /// an end hung off the board.</summary>
        private static Vector2 Fenced(Vector2 at, Vector2 span)
        {
            return new Vector2(Mathf.Clamp(at.x, 0f, Mathf.Max(0f, BoardWide - span.x)),
                               Mathf.Clamp(at.y, 0f, Mathf.Max(0f, BoardTall - span.y)));
        }

        /// <summary>How wide a node of this sort is: a gate is narrow, and an end
        /// and a timer five squares.</summary>
        private float Wide(int node)
        {
            LogicRow row = Row(node);
            return row != null && !row.IsTimer ? GateWidth : NodeWidth;
        }

        // ---- the timer node ---------------------------------------------------

        /// <summary>A timer node's three switches down its right, and the words in
        /// front of its two numbers.</summary>
        private const float TimerSwitch = 18f;
        private const float TimerWords = 30f;

        /// <summary>Seconds per pixel of drag: a slider's range per 250 pixels, as
        /// in the timer table.</summary>
        private const float TimeDragPerPixel = 0.004f;

        /// <summary>Timer numbers dragged and not yet committed. See
        /// `Ticking`.</summary>
        private readonly List<MapperType> scrubbing = new List<MapperType>();

        /// <summary>A timer row as a node: WAIT and DUR boxes to type or drag, and
        /// hold, stop and loop switches down the right. Input A starts it; its
        /// answer leaves on the right.</summary>
        private void Timed(GameObject body, LogicRow row, int index, float wide,
                           float tall)
        {
            float left = PortSize + 4f;
            float switchAt = wide - PortSize - (PortReach - PortSize) * 0.5f
                             - TimerSwitch - 1f;
            float line = (tall - 9f) * 0.5f;
            float room = switchAt - 3f - left - TimerWords;
            for (int i = 0; i < 2; i++)
            {
                bool wait = i == 0;
                float y = 3f + i * (line + 3f);
                GameObject words = new GameObject("Words");
                words.transform.SetParent(body.transform, false);
                UIF.Fit(words.AddComponent<RectTransform>(), left, y, TimerWords, line);
                Text said = Caption(words, wait ? "WAIT" : "DUR",
                                    TextAnchor.MiddleLeft);
                if (said != null)
                {
                    said.color = UIF.QuietInk;
                }
                Number(body, wait ? row.Wait : row.Duration, left + TimerWords, y,
                       room, line);
            }

            MToggle[] switches = { row.Hold, row.Stop, row.Loop };
            Texture[] faces = { Glyphs.Hold, Glyphs.Stop, Glyphs.Loop };
            string[] letters = { "H", "S", "L" };
            string[] tips = { "Hold to run", "Allow stop", "Loop" };
            float gap = (tall - 6f - TimerSwitch * 3f) * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                GameObject flip = UIF.Spawn(UIF.TogglePrefab, body.transform);
                if (flip == null)
                {
                    continue;
                }
                UIF.Fit(flip.GetComponent<RectTransform>(), switchAt,
                        3f + k * (TimerSwitch + gap), TimerSwitch, TimerSwitch);
                UIF.NoSwell(flip);
                // The picture where there is one, and its letter where the file
                // could not be loaded.
                Text letter = Caption(flip, faces[k] == null ? letters[k] : "",
                                      TextAnchor.MiddleCenter);
                Transform grows = letter == null ? null : letter.transform;
                if (faces[k] != null)
                {
                    grows = Picture(flip.transform, faces[k], TimerSwitch - 5f)
                        .transform;
                }
                UIF.Grow(flip, grows, 1.3f);
                Tip.On(flip, tips[k]);
                Toggle box = flip.GetComponent<Toggle>();
                MToggle control = switches[k];
                if (box == null || control == null)
                {
                    continue;
                }
                hushed = true;
                box.isOn = control.IsActive;
                hushed = false;
                Toggle flips = box;
                box.onValueChanged.AddListener(delegate(bool on)
                {
                    if (hushed)
                    {
                        return;
                    }
                    if (Picking())
                    {
                        // The click was for the node, not the switch.
                        hushed = true;
                        flips.isOn = !on;
                        hushed = false;
                        return;
                    }
                    control.IsActive = on;
                    List<MapperType> touched = new List<MapperType>();
                    touched.Add(control);
                    Commit(touched);
                });
            }
        }

        /// <summary>One of a timer's two numbers: typed, or dragged sideways off the
        /// box, the way the timer table's are.</summary>
        private void Number(GameObject body, MSlider slider, float x, float y,
                           float w, float h)
        {
            GameObject go = UIF.Spawn(UIF.InputPrefab, body.transform);
            if (go == null || slider == null)
            {
                return;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, w, h);
            InputField field = go.GetComponent<InputField>();
            if (field == null)
            {
                return;
            }
            UIF.Style(field.textComponent, UIF.Ink, TextAnchor.MiddleCenter);
            Text ghostText = field.placeholder as Text;
            UIF.Style(ghostText, UIF.QuietInk, TextAnchor.MiddleCenter);
            if (ghostText != null)
            {
                ghostText.text = "";
            }
            field.contentType = InputField.ContentType.DecimalNumber;
            Digits(field, w, h);
            field.text = UIF.Seconds(slider.Value);
            // A modifier-click is for the node, as on every other box on a node.
            Deaf guard = go.AddComponent<Deaf>();
            guard.box = field;
            Waving(go);

            ValueField drag = ValueField.Over(go, field);
            // The middle button pans from the number too. On the sheet, which takes
            // the press: a `Pan` under it never heard the drag.
            Waving(drag.gameObject);
            Marquee.On(field);
            drag.dragged = delegate(float pixels)
            {
                float value = Mathf.Max(0f, slider.Value
                    + pixels * (slider.Max - slider.Min) * TimeDragPerPixel);
                slider.Value = value;
                field.text = UIF.Seconds(value);
                if (!scrubbing.Contains(slider))
                {
                    scrubbing.Add(slider);
                }
                // This board's own doing, so the watch does not read it as somebody
                // else's and draw the board again under the drag.
                Ours();
            };
            field.onEndEdit.AddListener(delegate(string typed)
            {
                float value;
                if (!float.TryParse(typed, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out value))
                {
                    field.text = UIF.Seconds(slider.Value);
                    return;
                }
                value = Mathf.Max(0f, value);
                field.text = UIF.Seconds(value);
                if (Mathf.Approximately(value, slider.Value))
                {
                    return;
                }
                slider.Value = value;
                List<MapperType> touched = new List<MapperType>();
                touched.Add(slider);
                Commit(touched);
            });
        }

        /// <summary>The clear space at each end of a timer's number.</summary>
        private const float DigitPad = 3f;

        /// <summary>Sizes a timer number's lettering so five digits and a point fit
        /// whole, asked of the font at one size and scaled.</summary>
        private static void Digits(InputField field, float w, float h)
        {
            Text label = field.textComponent;
            if (label == null || label.font == null)
            {
                return;
            }
            TextGenerationSettings asked = label.GetGenerationSettings(Vector2.zero);
            asked.fontSize = 40;
            asked.resizeTextForBestFit = false;
            float much = Mathf.Max(0.0001f, label.pixelsPerUnit);
            float needs = label.cachedTextGeneratorForLayout
                .GetPreferredWidth("888.88", asked) / much;
            // A little spare, as the colour boxes keep: a pixel too wide scrolls.
            float room = w - DigitPad * 2f - 4f;
            int wide = Mathf.FloorToInt(40f * room / Mathf.Max(1f, needs));
            int size = Mathf.Clamp(Mathf.Min(wide, Mathf.FloorToInt(h * 0.7f)), 8, 40);
            Padded(label, size);
            Padded(field.placeholder as Text, size);
        }

        private static void Padded(Text label, int size)
        {
            if (label == null)
            {
                return;
            }
            label.fontSize = size;
            label.resizeTextForBestFit = false;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(DigitPad, 0f);
            rect.offsetMax = new Vector2(-DigitPad, 0f);
        }

        /// <summary>How big a node is drawn: as it is on the board where it has
        /// been drawn, and as its sort is where it has not.</summary>
        private Vector2 Span(int node)
        {
            if (node >= 0 && node < parts.Count && parts[node] != null)
            {
                return (parts[node].transform as RectTransform).sizeDelta;
            }
            return new Vector2(Wide(node), NodeHeight);
        }

        private void Move(int node, Vector2 to)
        {
            to = Fenced(Snapped(to), Span(node));
            Place place = Placed(node);
            if (place != null)
            {
                place.X = to.x;
                place.Y = to.y;
                return;
            }
            Board.Put(node, to);
        }

        /// <summary>Whether a port has a wire on it, which is what fills its circle.
        /// Asked by <see cref="Likeness"/> as well as by the port it draws.</summary>
        private bool Filled(int node, int port, bool output)
        {
            if (output)
            {
                return Heard(node);
            }
            Place sitting = Placed(node);
            if (sitting != null && sitting.Kind == Place.Output)
            {
                for (int i = 0; i < Rows; i++)
                {
                    if (Presses(i, sitting))
                    {
                        return true;
                    }
                }
                return false;
            }
            return Feeding(node, port) >= 0;
        }

        /// <summary>One port: a transparent square, wider than the circle drawn in
        /// it, that arms or lands a wire. Drawn, not a prefab button, which cost
        /// too much a node.
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

            // Filled once a wire lands, in the node's colour.
            Picture(go.transform, Filled(node, port, output) ? Glyphs.Dot : Glyphs.Ring,
                    PortSize).color = Tinted(node);

            // Wired by dragging port to port, or clicking one then the other. The
            // port pans too, or the middle button would be dead over ports.
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
        /// The wire in hand, picked by the pointer's path: off an input,
        /// <see cref="carried"/> is the node at its far end; off an answer,
        /// <see cref="held"/> and <see cref="heldPort"/> are. Let go over nothing,
        /// it comes off; on a port, the port decides (<see cref="Decided"/>).
        /// </summary>
        private int carried = -1;

        private int held = -1;
        private int heldPort = -1;

        /// <summary>How near a wire the pointer must come to take it, and how far
        /// to let it go: two numbers, so the boundary does not flicker. In window
        /// units (<see cref="Near"/>).</summary>
        private const float Grab = 26f;
        private const float Free = 54f;

        /// <summary>How far a drag must leave the port before choosing among its
        /// wires, which all start there; not once one is in hand.</summary>
        private const float Decide = 16f;

        /// <summary>How near its port a wire in hand is put back when let go: wider
        /// than the usual reach, as that is a change of mind.</summary>
        private const float Home = PortSize * 2f;

        /// <summary>A reach in window units, given in the zoomed board's, so a
        /// gesture feels the same at every zoom.</summary>
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
            // In the content's coordinates, where the wires are drawn, not the
            // board's.
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

        /// <summary>Which wire into a port a drag off it holds: the pointer's path
        /// picks among the port's wires. Nothing is written until the drop, so a
        /// change of mind costs nothing.</summary>
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
            // One wire or several, it is picked by the path, as on an answer:
            // handing a single wire over at the press moved it when let go on
            // another answer, instead of adding one.
            if (count == 0)
            {
                carried = -1;               // nothing on it: a new wire is drawn
                return;
            }
            PortMark aimed = Aimed(from, true);
            if (aimed != null)
            {
                // On a port, which alone says what is in hand -- see `Along`.
                if (aimed.Output)
                {
                    bool feeds = ended ? aimed.Node < Rows && Presses(aimed.Node, end)
                                       : strands.Contains(aimed.Node);
                    carried = feeds ? aimed.Node : -1;
                }
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

        /// <summary>Whether a drag out of an answer runs along one of its wires,
        /// moving it, or away from all of them, drawing a new one.</summary>
        private void Along(PortMark from)
        {
            RectTransform answer = Held(from.Node * 3);
            if (answer == null)
            {
                return;
            }
            Vector2 start = Middle(answer);
            PortMark aimed = Aimed(from, false);
            if (aimed != null)
            {
                // On a port, the port decides: the far end of one of these wires is
                // that wire, pulled back to come off; any other port is a new wire.
                // Only the port under the pointer counts, not one beside a wire's
                // end.
                if (!aimed.Output)
                {
                    held = Wired(from.Node, aimed.Node, aimed.Port) ? aimed.Node : -1;
                    heldPort = held >= 0 ? aimed.Port : -1;
                }
                return;                     // or back on its own port: as it was
            }
            int nearest = -1;
            int port = -1;
            float best = 0f;
            for (int node = 0; node < Nodes; node++)
            {
                Place end = Placed(node);
                bool ended = end != null && end.Kind == Place.Output;
                for (int i = 0; i < (ended ? 1 : 2); i++)
                {
                    // An output end may be pressed by several rows, each a wire of
                    // its own; `Feeding` names only the first.
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
                    float off = Aside(pullTo, start, Middle(sink));
                    if (nearest < 0 || off < best)
                    {
                        nearest = node;
                        port = i;
                        best = off;
                    }
                }
            }
            if (nearest < 0)
            {
                held = -1;                  // nothing on this port to have hold of
                heldPort = -1;
                return;
            }
            if (held < 0 && (pullTo - start).magnitude < Near(Decide))
            {
                // Still on the port, where all its wires start: nothing to choose
                // yet.
                return;
            }
            if (best <= Near(Grab))
            {
                // On a wire: that one is in hand, asked again every frame so a
                // change shows live.
                held = nearest;
                heldPort = port;
                return;
            }
            if (held < 0)
            {
                return;                     // drawing a new one, and still is
            }
            // The wire in hand is let go only past the wider reach, measured
            // against that wire, so the boundary and passing wires do not flicker.
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
            // Where the pointer let go, not what is drawn on top: ports reach past
            // their drawing and overlap. A wire in hand goes back on its port when
            // let go anywhere near it.
            Vector2 local;
            if (content != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                pullTo = local;
            }
            // The loose end seeks the other kind of port.
            PortMark aimed = Aimed(from, !from.Output);
            if (aimed != null)
            {
                to = aimed;
            }
            if (served == null)
            {
                carried = -1;
                held = -1;
                heldPort = -1;
                Strings();
                return;
            }
            if (to == null && (carried >= 0 || held >= 0))
            {
                // A wire picked out on the way and let go over nothing comes off.
                Dropping(from);
                return;
            }
            carried = -1;
            held = -1;
            heldPort = -1;
            if (to == null)
            {
                // A wire drawn into space offers what could go on its far end;
                // nothing changes unless something is picked.
                Offering(from, screen);
                return;
            }
            Decided(from.Node, from.Port, from.Output, to.Node, to.Port, to.Output);
        }

        /// <summary>
        /// The one rule for a wire's end put on a port, dropped or clicked: two ports
        /// already wired together come apart, an answer and an input that are not
        /// get a new wire, and anything else is left as it is. The port alone
        /// decides; nothing passed on the way is touched.
        /// </summary>
        private void Decided(int node, int port, bool output,
                             int other, int otherPort, bool otherOutput)
        {
            if (served == null || node == other)
            {
                Strings();
                return;
            }
            if (output == otherOutput)
            {
                Told(other, otherOutput ? 0 : 1 + otherPort, otherOutput
                     ? "has to connect\nto an input" : "has to connect\nto an output");
                Strings();
                return;
            }
            // Either way round: from the answer to the input, or back.
            int source = output ? node : other;
            int sink = output ? other : node;
            int sinkPort = output ? otherPort : port;
            if (Wired(source, sink, sinkPort))
            {
                List<MapperType> touched = new List<MapperType>();
                Unwire(source, sink, sinkPort, touched);
                Commit(touched);
                Redraw();
                return;
            }
            // Join redraws: a port is empty or full, and which has just changed.
            Join(source, sink, sinkPort);
        }

        /// <summary>A wire let go over nothing offers the right-click list;
        /// whatever is picked is made there and wired to the wire's port.</summary>
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
                    // End to end with no row to carry it (see `Join`): said before
                    // any node is made.
                    Told(source, 0, "cannot directly\nconnect to output");
                    return;
                }
                // Making a gate renumbers every end, perhaps the wire's own port:
                // shifted here, or the wire landed on whichever end took its
                // number.
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
            names.Add(Gates.Names[Gates.Timer]);
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
            // Past the two ends and the comment, the timer and then the gates.
            int gate = which == 3 ? Gates.Timer : which - 4;
            return Born(kind, kind < 0 ? gate : -1, at, true);
        }

        /// <summary>What that list's entry is: an end kind, a comment, or -1 for a
        /// gate (the gate's own number three along).</summary>
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

        /// <summary>The one port the pointer is on: the nearest within a port's
        /// reach, or null. By distance rather than the raycast, which picks
        /// whichever overlapping port was drawn last.</summary>
        /// <param name="from">The drag's own port, which counts whatever its
        /// kind.</param>
        /// <param name="answers">Whether a node's answer is wanted rather than an
        /// input.
        /// </param>
        private PortMark Aimed(PortMark from, bool answers)
        {
            RectTransform own = from == null ? null
                : Held(from.Output ? from.Node * 3 : from.Node * 3 + 1 + from.Port);
            RectTransform closest = null;
            float best = 0f;
            for (int i = 0; i < ports.Count; i++)
            {
                // Three ports to a node, the answer first.
                if (ports[i] == null || ((i % 3 == 0) != answers && ports[i] != own))
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
            if (closest == null || best > Near(Home))
            {
                return null;
            }
            return closest.GetComponent<PortMark>();
        }

        /// <summary>One of a port's several wires, picked out by the drag and let
        /// go over nothing: that one alone comes off.</summary>
        private void Dropping(PortMark from)
        {
            List<MapperType> touched = new List<MapperType>();
            if (carried >= 0)
            {
                Unwire(carried, from.Node, from.Port, touched);
            }
            else if (held >= 0)
            {
                Unwire(from.Node, held, heldPort, touched);
            }
            carried = -1;
            held = -1;
            heldPort = -1;
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

        /// <summary>Takes a wire off, into a list the caller commits: from an
        /// output end by the row's pressing, from anything else by the input's
        /// reading.</summary>
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
            // Only what the source answers to comes off; the input's other wires
            // stay.
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
                List<KeyCode> codes = new List<KeyCode>();
                Codes(from, codes);
                for (int i = 0; i < codes.Count; i++)
                {
                    Bindings.Dropped(input, codes[i]);
                }
            }
            touched.Add(input);
        }

        /// <summary>A port clicked: the first click arms it, of either kind, and a
        /// click on a port of the other kind ends the wire there by the same rule
        /// as a drop (<see cref="Decided"/>). The armed port again disarms; another
        /// of the same kind is armed instead.</summary>
        private void Touched(int node, int port, bool output)
        {
            if (served == null)
            {
                return;
            }
            int slot = output ? -1 : port;
            bool armedOutput = pendingPort < 0;
            if (pending == node && pendingPort == slot)
            {
                pending = -1;
            }
            else if (pending < 0 || armedOutput == output)
            {
                pending = node;
                pendingPort = slot;
            }
            else
            {
                int armed = pending;
                int armedPort = pendingPort;
                pending = -1;
                Decided(armed, armedPort, armedOutput, node, port, output);
                return;
            }
            Strings();
        }

        /// <summary>The armed port as one number, for telling one drawing's from
        /// the next: -1 when none.</summary>
        private int Armed()
        {
            return pending < 0 ? -1 : pending * 3 + 1 + pendingPort;
        }

        /// <summary>Every wire as three numbers: the answering node, the reading
        /// node, and which input. Worked out when the board is drawn, not on every
        /// frame of <see cref="Strings"/>.</summary>
        private readonly List<int> wired = new List<int>();

        /// <summary>What feeds one port while that is being written down.</summary>
        private readonly List<int> strands = new List<int>();

        private void Wired()
        {
            wired.Clear();
            wiredAt++;
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

        /// <summary>Draws every wire into the one mesh: each wire is worked out
        /// into its own <see cref="Trace"/>, then all are copied into the mesh. A
        /// drag names the nodes it moves, so only their wires are worked out
        /// again.</summary>
        private void Strings()
        {
            Strings(null);
        }

        /// <param name="moved">The nodes moved since the wires were last drawn, or
        /// null where anything at all may have changed.</param>
        private void Strings(List<int> moved)
        {
            Faded();
            if (skein == null)
            {
                return;
            }
            skein.Clear();
            if (served == null || sheet == null)
            {
                skein.Shown();
                return;
            }
            loose.Clear();
            tracing = loose;
            // A wire in hand runs from its port to the pointer, down no corridor of
            // its own.
            corridor = float.NaN;
            if (pulling && pullFrom != null)
            {
                // The loose end hangs from the answer of a wire taken off an input,
                // the far end of one moved off an answer, or the port a new wire
                // leaves.
                bool answering = carried >= 0 || (pullFrom.Output && held < 0);
                int fixedEnd = carried >= 0 ? carried * 3
                    : (held >= 0 ? held * 3 + 1 + heldPort
                        : (pullFrom.Output ? pullFrom.Node * 3
                                           : pullFrom.Node * 3 + 1 + pullFrom.Port));
                RectTransform end = Held(fixedEnd);
                if (end != null)
                {
                    // Always drawn from the answer end, or a curve bows backwards.
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

            // A drag's own frames keep every wire but those on moved nodes, so long
            // as the last drawing had the same wires, the same armed one and
            // nothing in hand.
            int count = wired.Count / 3;
            // Where the corridors are is a question about every wire at once, so a
            // square board is worked out whole rather than wire by wire: one moved
            // node can move the lane of a wire that did not move.
            Laned();
            bool some = moved != null && !pulling && tracedWhole
                && tracedFor == wiredAt && tracedPending == Armed()
                && style != Square;
            while (traces.Count < count)
            {
                traces.Add(new Trace());
            }
            for (int w = 0; w < count; w++)
            {
                int from = wired[w * 3];
                int node = wired[w * 3 + 1];
                int port = wired[w * 3 + 2];
                if (some && !moved.Contains(from) && !moved.Contains(node))
                {
                    continue;
                }
                Trace trace = traces[w];
                trace.Clear();
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
                tracing = trace;
                corridor = w < lanes.Count ? lanes[w] : float.NaN;
                // The armed port's wires are lit: all out of an answer, or those
                // into one input.
                bool lit = pendingPort < 0 ? from == pending
                                           : node == pending && port == pendingPort;
                if (float.IsNaN(corridor))
                {
                    Draw(from * 3, node * 3 + 1 + port, lit);
                }
                else
                {
                    // A corridor means `Laned` has already asked both ports where
                    // they are: a square board would otherwise ask twice a frame.
                    Drawn(from * 3, node * 3 + 1 + port, lit, wireFrom[w], wireTo[w]);
                }
            }
            tracing = null;
            corridor = float.NaN;
            tracedFor = wiredAt;
            tracedPending = Armed();
            tracedWhole = !pulling;

            Poured(loose);
            for (int w = 0; w < count; w++)
            {
                Poured(traces[w]);
            }
            skein.Shown();
        }

        /// <summary>One wire as it was last worked out: its straight pieces, each a
        /// start, an end and a colour.</summary>
        private sealed class Trace
        {
            public readonly List<Vector2> From = new List<Vector2>();
            public readonly List<Vector2> To = new List<Vector2>();
            public readonly List<Color> Inks = new List<Color>();

            public void Clear()
            {
                From.Clear();
                To.Clear();
                Inks.Clear();
            }
        }

        /// <summary>The mesh every wire is drawn into.</summary>
        private WireMesh skein;

        /// <summary>Every wire in <see cref="wired"/>, by index over three; the
        /// loose one following the pointer; and the one <see cref="Piece"/> is
        /// writing.</summary>
        private readonly List<Trace> traces = new List<Trace>();
        private readonly Trace loose = new Trace();
        private Trace tracing;

        /// <summary>The corridor each square wire's middle leg runs down, worked
        /// out for every wire before any is traced (see <see cref="Laned"/>), and
        /// the one being traced now. NaN is "no corridor": the midpoint between the
        /// two ports, which is what a lone wire wants and all a curve or a straight
        /// line ever uses.</summary>
        private readonly List<float> lanes = new List<float>();
        private float corridor = float.NaN;

        /// <summary>The corridors already given out, with the stretch of board each
        /// covers and the answer it leaves: two wires off one answer may share a
        /// corridor, having a point in common already.</summary>
        private readonly List<float> laneAt = new List<float>();
        private readonly List<float> laneLow = new List<float>();
        private readonly List<float> laneHigh = new List<float>();
        private readonly List<int> laneFrom = new List<int>();

        /// <summary>Each wire's two ends, asked once: a port's middle is four world
        /// corners and a transform, which is not a question to ask twice a wire.
        /// The order lanes are handed out in, longest wire first, and what each is
        /// measured by.</summary>
        private readonly List<Vector2> wireFrom = new List<Vector2>();
        private readonly List<Vector2> wireTo = new List<Vector2>();
        private readonly List<int> order = new List<int>();
        private readonly List<float> spans = new List<float>();

        /// <summary>Bumped whenever the wire list is rebuilt, and the state the
        /// traces were last worked out in: that list, the armed wire, nothing in
        /// hand.</summary>
        private int wiredAt;
        private int tracedFor = -1;
        private int tracedPending = -1;
        private bool tracedWhole;

        private void Poured(Trace trace)
        {
            for (int i = 0; i < trace.From.Count; i++)
            {
                skein.Add(trace.From[i], trace.To[i], trace.Inks[i]);
            }
        }

        /// <summary>How many wires run side by side between two grid lines: as many
        /// as the wire's own width and a clear width of its own allow, which is
        /// five at a cell of 32 and a wire two thick.</summary>
        private static int Lanes()
        {
            return Mathf.Clamp(Mathf.FloorToInt(GridStep / (WireWidth * 3f)), 1, 12);
        }

        /// <summary>
        /// Gives every square wire the corridor its middle leg runs down, so two
        /// wires crossing the same cell are set a lane apart rather than drawn over
        /// each other. The lane nearest the midpoint is wanted, and the first free
        /// one either side of it is taken: a corridor is free where no wire already
        /// given it covers the same stretch of board, and a wire off the same answer
        /// never counts, since the two already share a point.
        /// </summary>
        private void Laned()
        {
            lanes.Clear();
            laneAt.Clear();
            laneLow.Clear();
            laneHigh.Clear();
            laneFrom.Clear();
            wireFrom.Clear();
            wireTo.Clear();
            order.Clear();
            spans.Clear();
            int count = wired.Count / 3;
            if (style != Square || content == null)
            {
                return;
            }
            float step = GridStep / Lanes();
            for (int w = 0; w < count; w++)
            {
                lanes.Add(float.NaN);
                int from = wired[w * 3];
                int node = wired[w * 3 + 1];
                int port = wired[w * 3 + 2];
                RectTransform one = Held(from * 3);
                RectTransform two = Held(node * 3 + 1 + port);
                bool both = one != null && two != null;
                Vector2 leaves = both ? Middle(one) : Vector2.zero;
                Vector2 lands = both ? Middle(two) : Vector2.zero;
                wireFrom.Add(leaves);
                wireTo.Add(lands);
                if (!both)
                {
                    continue;
                }
                order.Add(w);
                spans.Add(Mathf.Abs(lands.y - leaves.y)
                          + Mathf.Abs(lands.x - leaves.x));
            }
            // Longest first, by an insertion sort that keeps ties in the order the
            // wires are listed in: a long wire left until last finds every near lane
            // taken and has to cut across the board to reach a free one, which is
            // the crossing that shows.
            for (int i = 1; i < order.Count; i++)
            {
                int which = order[i];
                float mine = spans[i];
                int at = i;
                while (at > 0 && spans[at - 1] < mine)
                {
                    order[at] = order[at - 1];
                    spans[at] = spans[at - 1];
                    at--;
                }
                order[at] = which;
                spans[at] = mine;
            }
            for (int i = 0; i < order.Count; i++)
            {
                int w = order[i];
                int from = wired[w * 3];
                Vector2 leaves = wireFrom[w];
                Vector2 lands = wireTo[w];
                float want = (leaves.x + lands.x) * 0.5f;
                float low = Mathf.Min(leaves.y, lands.y);
                float high = Mathf.Max(leaves.y, lands.y);
                float nearest = Mathf.Round(want / step) * step;
                // Out from the lane it wants, the side it is heading first: a wire
                // going right takes the lane beyond the middle before the one behind
                // it, which is the side its far end is on.
                float toward = lands.x >= leaves.x ? 1f : -1f;
                float take = nearest;
                bool found = false;
                for (int pass = 0; pass < 2 && !found; pass++)
                {
                    for (int off = 0; off < 64; off++)
                    {
                        int side = (off + 1) / 2;
                        float at = nearest
                                 + (off % 2 == 0 ? side : -side) * step * toward;
                        if (!LaneFree(at, low, high, from, step))
                        {
                            continue;
                        }
                        // The first pass keeps the wire out from behind the nodes;
                        // the second takes the first free lane whatever stands in
                        // it, so a crowded board still draws every wire somewhere.
                        if (pass == 0 && !LaneClear(at, low, high))
                        {
                            continue;
                        }
                        take = at;
                        found = true;
                        break;
                    }
                }
                lanes[w] = take;
                laneAt.Add(take);
                laneLow.Add(low);
                laneHigh.Add(high);
                laneFrom.Add(from);
            }
        }

        /// <summary>Whether a corridor misses every node: a wire that runs behind a
        /// gate cannot be followed across it.</summary>
        private bool LaneClear(float at, float low, float high)
        {
            int many = Nodes;
            for (int node = 0; node < many; node++)
            {
                Vector2 corner = Where(node);
                Vector2 span = Span(node);
                if (at < corner.x - WireWidth || at > corner.x + span.x + WireWidth)
                {
                    continue;
                }
                if (high > corner.y - WireWidth
                    && corner.y + span.y + WireWidth > low)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Whether a corridor is clear over this stretch of the board.
        /// </summary>
        private bool LaneFree(float at, float low, float high, int from, float step)
        {
            for (int i = 0; i < laneAt.Count; i++)
            {
                if (laneFrom[i] == from)
                {
                    continue;               // the same answer: a point in common
                }
                if (Mathf.Abs(laneAt[i] - at) >= step * 0.5f)
                {
                    continue;               // a lane apart or more
                }
                // Touching end to end is not overlapping: a wire may start where
                // another stops.
                if (high > laneLow[i] + 0.01f && laneHigh[i] > low + 0.01f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>One wire between two ports, if both are there.</summary>
        private void Draw(int from, int to, bool armed)
        {
            RectTransform start = Held(from);
            RectTransform end = Held(to);
            if (start == null || end == null)
            {
                return;
            }
            Drawn(from, to, armed, Middle(start), Middle(end));
        }

        /// <summary>The same wire, with both ends already worked out. Asking a port
        /// where it is costs four world corners and a transform, and a square board
        /// has asked once already in <see cref="Laned"/>.</summary>
        private void Drawn(int from, int to, bool armed, Vector2 a, Vector2 b)
        {
            if (armed)
            {
                String(a, b, LiveInk);
                return;
            }
            // Three ports to a node, so the node is the port's number over three.
            int source = from / 3;
            int sink = to / 3;
            if (Hues.Graded)
            {
                // Between the two ends' kind colours: what the nodes wear in
                // COLORED, and what they would wear in UNI-COLOR.
                String(a, b, Hues.Wired(Hues.Kind(Slotted(source))),
                       Hues.Wired(Hues.Kind(Slotted(sink))));
                return;
            }
            // Known by both its ends and the port it lands on, so two wires
            // between the same two nodes are free to differ.
            String(a, b, Hues.WireOf(Hues.Mix(Hues.Mix(Seeded(source), Seeded(sink)),
                                              to % 3)));
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
                return Bindings.Carries(names, place.Variable);
            }
            return names == null && Bindings.Holds(said.Emulate, place.Key);
        }

        /// <summary>Takes every wire off the board, until they are next drawn.
        /// </summary>
        private void Cut()
        {
            if (skein != null)
            {
                skein.Clear();
                skein.Shown();
            }
        }

        private RectTransform Held(int at)
        {
            return at < 0 || at >= ports.Count ? null : ports[at];
        }

        /// <summary>A port's middle in the content's coordinates, where the wires
        /// are drawn.
        /// </summary>
        private readonly Vector3[] edges = new Vector3[4];

        private Vector2 Middle(RectTransform port)
        {
            port.GetWorldCorners(edges);
            Vector3 middle = (edges[0] + edges[2]) * 0.5f;
            return content.InverseTransformPoint(middle);
        }

        /// <summary>One wire in the chosen style, of straight pieces -- a handful
        /// along a bezier for a curve, three for a square -- each a quad in <see
        /// cref="WireMesh"/>.
        /// </summary>
        private void String(Vector2 from, Vector2 to, Color colour)
        {
            String(from, to, colour, colour);
        }

        /// <summary>The same, shading between two colours; a shaded wire is cut
        /// finer, as each piece is one colour.</summary>
        private void String(Vector2 from, Vector2 to, Color start, Color end)
        {
            bool even = start == end;
            if (style == Straight)
            {
                Leg(from, to, 0f, 1f, start, end, even ? 1 : Curve);
                return;
            }
            if (style == Square)
            {
                // Its own corridor where it has been given one, so wires crossing
                // the same cell run a lane apart instead of over each other.
                float middle = float.IsNaN(corridor) ? (from.x + to.x) * 0.5f
                                                     : corridor;
                Vector2 turn = new Vector2(middle, from.y);
                Vector2 back = new Vector2(middle, to.y);
                // How far along the whole wire each corner is, so the shade runs
                // evenly over all three legs rather than a third to each.
                float one = Mathf.Abs(middle - from.x);
                float two = Mathf.Abs(to.y - from.y);
                float whole = Mathf.Max(0.001f, one + two + Mathf.Abs(to.x - middle));
                float first = one / whole;
                float second = (one + two) / whole;
                Leg(from, turn, 0f, first, start, end, even ? 1 : Cuts(first));
                Leg(turn, back, first, second, start, end,
                    even ? 1 : Cuts(second - first));
                Leg(back, to, second, 1f, start, end, even ? 1 : Cuts(1f - second));
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
                Piece(last, next,
                      even ? start : Color.Lerp(start, end, (i - 0.5f) / Curve));
                last = next;
            }
        }

        /// <summary>A straight run of a wire, from `along` to `upTo` of the way
        /// along the whole of it, in as many pieces as it is given.</summary>
        private void Leg(Vector2 from, Vector2 to, float along, float upTo,
                         Color start, Color end, int pieces)
        {
            for (int i = 0; i < pieces; i++)
            {
                float near = i / (float)pieces;
                float far = (i + 1) / (float)pieces;
                Color shade = Color.Lerp(start, end,
                                         Mathf.Lerp(along, upTo, (near + far) * 0.5f));
                Piece(Vector2.Lerp(from, to, near), Vector2.Lerp(from, to, far), shade);
            }
        }

        /// <summary>How many pieces a share of a shading wire is cut into.</summary>
        private static int Cuts(float share)
        {
            return Mathf.Max(1, Mathf.CeilToInt(Curve * share));
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

        /// <summary>One straight piece of the wire being worked out.</summary>
        private void Piece(Vector2 from, Vector2 to, Color colour)
        {
            if (tracing == null)
            {
                return;
            }
            tracing.From.Add(from);
            tracing.To.Add(to);
            tracing.Inks.Add(colour);
        }

        // ---- what the buttons do ---------------------------------------------

        private void Born(int kind, int gate)
        {
            Born(kind, gate, Vector2.zero, false);
        }

        /// <summary>A new node: a gate is added as a row, with a place in the
        /// layout; an end or a comment lives in the layout alone.</summary>
        private int Born(int kind, int gate, Vector2 where, bool placed)
        {
            if (served == null)
            {
                return -1;
            }
            // A node asked for rather than put somewhere lands in the middle of
            // the view, staggered off whatever is already sitting there.
            Vector2 at = placed ? Fenced(Snapped(where))
                                : Staggered(Sized(kind, gate));
            if (gate >= 0)
            {
                int was = Rows;
                List<MapperType> touched = new List<MapperType>();
                int row = LogicTable.Add(served, touched);
                if (row < 0)
                {
                    // A gate is a row, and the block holds up to MaxRows of them.
                    Warned(sheet, "reached " + ComputerBehaviour.MaxRows
                                  + "\ngates limit");
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
                    // A gate gets a minted name now: one that answers nothing
                    // cannot be wired to.
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

        /// <summary>The selection after a row is added: rows number before ends, so
        /// every end moves along one. `Shuffled`, the other way up.</summary>
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

        /// <summary>The selection after a node is removed: the numbers above it
        /// close up, or another node would be selected in its place.</summary>
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
            // The armed answer is a node number too, and closes up the same way.
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
                // An end's wires go with it -- a wire to nothing cannot be seen or
                // cut -- but only if the name is the end's alone: a gate reading an
                // output's name is wired to the gate that presses it.
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
            // Names already given are kept, as something else may read them; the
            // prefix is for the next.
            LogicTable.Apply(served.PrefixControl);
            Kept();
            Redraw();
        }

        /// <summary>Dragging the empty board moves what is on it.</summary>
        private void Panning(Vector2 by)
        {
            Panned(by);
        }

        /// <summary>Pans, and says how far it actually went: the view is kept over
        /// the board, so an auto-pan at its edge may move less.</summary>
        private Vector2 Panned(Vector2 by)
        {
            if (content == null)
            {
                return Vector2.zero;
            }
            Vector2 was = content.anchoredPosition;
            content.anchoredPosition = Bounded(was + by);
            // The wires are drawn on the content and move with it. Only a loose end
            // following the pointer, rather than the board, has anywhere new to be.
            if (pulling)
            {
                Strings();
            }
            return content.anchoredPosition - was;
        }

        /// <summary>The view kept over the board, give or take a margin, so a board
        /// is never panned out of sight.</summary>
        private Vector2 Bounded(Vector2 at)
        {
            if (sheet == null || content == null)
            {
                return at;
            }
            float much = content.localScale.x;
            Rect room = sheet.rect;
            // Half a view past either edge: nodes at the fence clear the frame, and
            // ZOOM FIT can centre a small board.
            float overX = room.width * 0.5f;
            float overY = room.height * 0.5f;
            // The content moves against the view: to see right, x goes negative;
            // down, y up.
            float leftmost = room.width - BoardWide * much - overX;
            at.x = Mathf.Clamp(at.x, Mathf.Min(leftmost, overX), overX);
            float lowest = BoardTall * much - room.height + overY;
            at.y = Mathf.Clamp(at.y, -overY, Mathf.Max(lowest, -overY));
            return at;
        }

        /// <summary>Zooms about the pointer, so what is under the hand stays there.
        /// </summary>
        private void Zooming(float wheel, Vector2 screen)
        {
            if (content == null || Mathf.Abs(wheel) < 0.01f)
            {
                return;
            }
            float was = content.localScale.x;
            float now = Mathf.Clamp(was * (wheel > 0f ? 1.1f : 1f / 1.1f),
                                    Least(), MostZoom);
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
            // The wires scale with the content they are drawn on. What does not is
            // the grid's fade and the fence's thickness -- and a loose end.
            if (pulling)
            {
                Strings();
            }
            else
            {
                Faded();
            }
        }

        /// <summary>ZOOM FIT: as far out as it takes to see the whole board, and
        /// centred.
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
                          (room.height - edge * 2f) / tall), Least(), MostZoom);
            content.localScale = new Vector3(much, much, 1f);
            Looking((low + high) * 0.5f);
        }

        /// <summary>The box everything drawn sits in, in board units; false when
        /// nothing is.
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

        /// <summary>Rows added elsewhere -- the table's "+" -- from <paramref
        /// name="from"/> on that have no place yet are put in the middle of the
        /// view, one grid cell apart, before anything asks where they are: asked
        /// first, the layout gives them a column off to the side.</summary>
        private void Unplaced(int from)
        {
            if (served == null)
            {
                return;
            }
            Wiring layout = Board;
            int first = Mathf.Max(from, layout.Spots.Count);
            if (first >= Rows)
            {
                return;
            }
            for (int row = first; row < Rows; row++)
            {
                // One at a time: each is put before the next asks what is taken,
                // so a run of them staggers rather than stacking.
                layout.Put(row, Staggered(new Vector2(NodeWidth, NodeHeight)));
            }
            Keep();
        }

        /// <summary>Where a node nobody dropped goes: the middle of the view, and
        /// one grid cell right and down for each node already standing there, so
        /// the new one is on top of nothing and its title bar is reachable.
        /// </summary>
        private Vector2 Staggered(Vector2 span)
        {
            Vector2 at = Snapped(Viewed() - span * 0.5f);
            Vector2 step = new Vector2(GridStep, GridStep);
            // Bounded: a board packed on every cell of the diagonal would
            // otherwise walk to the edge and back for ever.
            for (int tries = 0; tries < 64 && Taken(at); tries++)
            {
                at = Snapped(at + step);
            }
            return Fenced(at, span);
        }

        /// <summary>Whether a node's corner already sits here, to within half a
        /// grid cell: what <see cref="Staggered"/> steps past.</summary>
        private bool Taken(Vector2 at)
        {
            Wiring layout = Board;
            float near = GridStep * 0.5f;
            int rows = Mathf.Min(Rows, layout.Spots.Count);
            for (int row = 0; row < rows; row++)
            {
                Vector2 other = layout.Spots[row];
                if (Mathf.Abs(other.x - at.x) < near
                    && Mathf.Abs(other.y - at.y) < near)
                {
                    return true;
                }
            }
            for (int i = 0; i < layout.Places.Count; i++)
            {
                Place place = layout.Places[i];
                if (Mathf.Abs(place.X - at.x) < near
                    && Mathf.Abs(place.Y - at.y) < near)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The board place in the middle of the window, at the current
        /// zoom and pan: <see cref="Looking"/> the other way round.</summary>
        private Vector2 Viewed()
        {
            if (content == null || sheet == null || content.localScale.x < 0.0001f)
            {
                return Centre;
            }
            Rect room = sheet.rect;
            float much = content.localScale.x;
            return new Vector2((room.width * 0.5f - content.anchoredPosition.x) / much,
                               (content.anchoredPosition.y + room.height * 0.5f) / much);
        }

        /// <summary>Centres a board place in the window at the current zoom; a
        /// node's y counts down, hence the sign.</summary>
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
            // A pan: the wires go with the content.
            Faded();
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

        /// <summary>Each node's picked-out edge, following the selection without a
        /// redraw.
        /// </summary>
        private readonly List<GameObject> rims = new List<GameObject>();

        /// <summary>Each comment's text box, read back before the layout is written
        /// (<see cref="Voiced"/>).</summary>
        private readonly List<InputField> notes = new List<InputField>();

        /// <summary>The cross that removes a node, one to a node and made the first
        /// time the pointer finds that node.</summary>
        private readonly List<GameObject> bins = new List<GameObject>();

        /// <summary>Each comment's resizing corner, made when first pointed at; and
        /// the comment being resized, or -1.</summary>
        private readonly List<GameObject> grips = new List<GameObject>();
        private int sizing = -1;

        /// <summary>The place each comment box was drawn for. Boxes are matched to
        /// places by this, not by number: renumbering once wrote one comment's box
        /// over another's.
        /// </summary>
        private readonly List<Place> noted = new List<Place>();

        /// <summary>Everything on the two rows EDIT puts out.</summary>
        private readonly List<RectTransform> shelf = new List<RectTransform>();

        /// <summary>Where the board's top edge was last laid, so showing or hiding
        /// the colour rows can move the drawing back by as much.</summary>
        private float boardTop;

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
                // Last sibling, so it is over the picked-out edge made after it.
                bins[node].transform.SetAsLastSibling();
            }

            // A comment has its resizing corner as well, shown the same way.
            Place place = Placed(node);
            if (place == null || place.Kind != Place.Note)
            {
                return;
            }
            while (grips.Count <= node)
            {
                grips.Add(null);
            }
            // Held out while it is being pulled: the pointer comes off the comment
            // as it resizes, and hiding the corner would end the drag it carries.
            bool shown = on || sizing == node;
            if (shown && grips[node] == null)
            {
                grips[node] = Resizer(parts[node], node);
            }
            if (grips[node] != null && grips[node].activeSelf != shown)
            {
                grips[node].SetActive(shown);
            }
            if (shown && grips[node] != null)
            {
                grips[node].transform.SetAsLastSibling();
                grips[node].transform.localScale = Unzoomed();
            }
        }

        /// <summary>The scale undoing the board's zoom, for a comment's corner,
        /// which should be one size on screen. Asked every frame while hovered, so
        /// a zoom is followed.
        /// </summary>
        private Vector3 Unzoomed()
        {
            float much = content == null ? 1f : content.localScale.x;
            float back = much > 0.001f ? 1f / much : 1f;
            return new Vector3(back, back, 1f);
        }

        /// <summary>A click on a node's body picks it. With a modifier held, <see
        /// cref="Pointing"/> takes the click instead, so a control-click on a
        /// node's controls picks the node.</summary>
        private void Chosen(int node)
        {
            if (Picking() || node < 0 || node >= Nodes)
            {
                return;
            }
            if (dragging)
            {
                // uGUI clicks at the end of a drag as well; taking it would clear
                // the selection just moved.
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

        /// <summary>With a modifier held, shows a faint edge on the hovered node
        /// and takes the click, since each part of a node has its own idea of
        /// one.</summary>
        private void Pointing()
        {
            // A size drag is one undo step, filed when the hand lets go.
            if (scaling && !Input.GetMouseButton(0))
            {
                scaling = false;
                cellsDrift = 0f;
                linesDrift = 0f;
                Kept();
            }
            bool armed = Picking();
            for (int i = 0; i < marks.Count; i++)
            {
                // A box being dragged shows what it caught; otherwise the hovered
                // node.
                bool show = banding ? banded.Contains(i) : (armed && i == under);
                if (show && marks[i] == null)
                {
                    marks[i] = Edged(i, 0.5f);
                }
                // Edges are made on demand and may be null: asking one threw every
                // frame.
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
            // On release, not press: with control held, a press may begin an
            // axis-locked drag.
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

        /// <summary>What a drag moves, where each started, and how far the pointer
        /// has gone: an axis lock measured step by step drifts.</summary>
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

        /// <summary>Pans the board under a node dragged past its edge, moving the
        /// node with it so it stays under the hand.</summary>
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
            // The drag is told the ground moved, or it would carry the node twice
            // as far.
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
            // Dragging one of a picked-out set moves the set.
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

        /// <summary>One frame of a node drag; with control held it locks to
        /// whichever axis it has gone furthest along since the start.</summary>
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
            // One gate dragged over another lands on it; a set being moved does
            // not, and neither does an end, a comment, or a gate over its own kind.
            int alone = moving.Count == 1 ? moving[0] : -1;
            LogicRow carried = alone >= 0 && alone < Rows ? Row(alone) : null;
            Lighting(carried != null
                     ? Landing(Input.mousePosition, alone, carried.Gate) : -1);
            Strings(moving);
        }

        /// <summary>Nodes whose cell was switched to a name with nothing typed yet:
        /// stored nowhere else, and a redraw would put the cell back to a
        /// key.</summary>
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

        /// <summary>What a node's cell shows: the name it is bound to, an empty name
        /// where somebody has asked for one, or nothing for a blank output, whose
        /// name is a hidden one (see <see cref="Rebind"/>).</summary>
        private string Shown(int node, string variable, KeyCode key)
        {
            Place place = Placed(node);
            if (place != null && place.Kind == Place.Output && Mine(variable))
            {
                return "";
            }
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

        /// <summary>A message over whatever refused an action: its own bubble
        /// rather than a tooltip, so it stays long enough to read, and then
        /// fades.</summary>
        private GameObject warning;
        private Text warningLabel;
        private CanvasGroup warningFade;
        private float warnAt;

        /// <summary>What a gate says when a key and a variable would meet on its
        /// answer: a Besiege key presses keycodes or names, never both.</summary>
        private const string KeyAndName =
            "Can't mix key and variable.\nUse an OR gate.";

        /// <summary>How long news worth reading twice stays up: an import's count,
        /// or a refusal that says what to do instead.</summary>
        private const float NewsSeconds = 6f;

        /// <summary>The message over the refused node's port, or over the whole
        /// node when the port is -1.</summary>
        private void Told(int node, int port, string words)
        {
            Told(node, port, words, 0f);
        }

        /// <param name="lasting">Seconds on screen, or 0 for the usual.</param>
        private void Told(int node, int port, string words, float lasting)
        {
            RectTransform over = port >= 0 ? Held(node * 3 + port) : null;
            if (over == null && node >= 0 && node < parts.Count
                && parts[node] != null)
            {
                over = parts[node].transform as RectTransform;
            }
            Warned(over != null ? over : sheet, words, Hot, lasting);
        }

        private void Warned(RectTransform over, string words)
        {
            Warned(over, words, Hot);
        }

        /// <summary>The same, in whatever colour the news deserves: red for a
        /// gesture refused, the live colour for one that worked.</summary>
        private void Warned(RectTransform over, string words, Color ink)
        {
            Warned(over, words, ink, 0f);
        }

        /// <param name="lasting">Seconds on screen, or 0 for the usual: two over a
        /// control, four in the corner.</param>
        private void Warned(RectTransform over, string words, Color ink, float lasting)
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
            // As many lines as it was written with: a refusal and its fix read as
            // two lines.
            int lines = 1;
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i] == '\n')
                {
                    lines++;
                }
            }
            warnWide = Mathf.Max(90f, warningLabel.preferredWidth + 18f);
            warnTall = 22f + (lines - 1) * 15f;
            warnOver = over;
            // News about the whole board goes in its top-left corner rather than
            // over its nodes, and stays twice as long.
            cornered = over == sheet;
            warning.SetActive(true);
            if (!Placing())
            {
                warning.SetActive(false);
                return;
            }
            warning.transform.SetAsLastSibling();
            warningFade.alpha = 1f;
            warnAt = Time.unscaledTime
                + (lasting > 0f ? lasting : (cornered ? 4f : 2f));
        }

        private RectTransform warnOver;
        private float warnWide;
        private float warnTall;
        private bool cornered;

        /// <summary>Places the message above its control or in the board's corner,
        /// which is followed every frame as the window moves.</summary>
        private bool Placing()
        {
            if (canvas == null || warnOver == null)
            {
                return false;
            }
            RectTransform home = canvas.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            warnOver.GetWorldCorners(corners);
            // Corner 1 is the top left; 0 and 2 across the diagonal are the middle.
            Vector3 world = cornered ? corners[1] : (corners[0] + corners[2]) * 0.5f;
            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    home, RectTransformUtility.WorldToScreenPoint(null, world),
                    null, out point))
            {
                return false;
            }
            Vector2 room = home.rect.size;
            float x = cornered ? point.x + room.x * 0.5f + 8f
                               : point.x + room.x * 0.5f - warnWide * 0.5f;
            float y = cornered ? room.y * 0.5f - point.y + 8f
                               : room.y * 0.5f - point.y - warnTall - 18f;
            x = Mathf.Clamp(x, 2f, room.x - warnWide - 2f);
            y = Mathf.Clamp(y, 2f, room.y - warnTall - 2f);
            UIF.Fit(warning.GetComponent<RectTransform>(), x, y, warnWide, warnTall);
            return true;
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
            if (cornered)
            {
                Placing();
            }
            if (warningFade != null)
            {
                warningFade.alpha = Mathf.Clamp01(left / 0.45f);
            }
        }

        /// <summary>Holds the game off for the rest of this frame:
        /// `BlockSelectionTool` skips the keyboard in its LateUpdate while `inMenu`
        /// is up.</summary>
        private void Muffle()
        {
            muffled = ZoomGuard.Grip(true, muffled);
            muffledAt = Time.frameCount;
        }

        private bool muffled;
        private int muffledAt;

        /// <summary>Whether a point on the screen is on this window, or on the list
        /// it has open.</summary>
        private bool Inside(Vector2 screen)
        {
            if (windowRect != null && RectTransformUtility.RectangleContainsScreenPoint(
                    windowRect, screen, null))
            {
                return true;
            }
            return Choices.Over(screen);
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

        /// <summary>Removes everything picked out, as one edit, highest number
        /// first so the removals do not renumber what is left.</summary>
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

        /// <summary>A click on empty board drops the selection.</summary>
        private void Nobody()
        {
            if (picked.Count == 0)
            {
                return;
            }
            picked.Clear();
            Rims();
        }

        /// <summary>Shows the picked-out edges the selection says: all a pick
        /// changes, which is why picking is instant.</summary>
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

        /// <summary>A box dragged over the board picks out what is inside: drawn
        /// during the drag, settled at its end.</summary>
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

            // What the box is over wears the faint edge until it is let go.
            banded.Clear();
            for (int node = 0; node < Nodes && node < parts.Count; node++)
            {
                if (parts[node] == null)
                {
                    continue;
                }
                // The shared corner buffer: this runs for every node on every frame
                // of the drag.
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

            // A box alone replaces the selection; with shift it adds; with control
            // it toggles.
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

        /// <summary>Sets each dashed edge's dash count from the box's size, one per
        /// <see cref="Glyphs.DashStep"/>, so dashes keep their length.</summary>
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

        /// <summary>Copies what is picked out; the copies follow the pointer until
        /// put down.
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
                    made.Wait = row.Wait.Value;
                    made.Duration = row.Duration.Value;
                    made.Hold = row.Hold.IsActive;
                    made.Stop = row.Stop.IsActive;
                    made.Loop = row.Loop.IsActive;
                    made.Wire = Bindings.IsVariable(row.Emulate)
                        ? Bindings.Variable(row.Emulate) : null;
                    made.Answers = Bindings.Codes(row.Emulate).ToArray();
                    for (int port = 0; port < 2; port++)
                    {
                        MKey input = port == 0 ? row.InputA : row.InputB;
                        made.Inputs[port] = Bindings.IsVariable(input)
                            ? Bindings.Variable(input) : null;
                        made.Keys[port] = Bindings.Codes(input).ToArray();
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
                    made.Size = place.Size;
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
                    bool timer = clipboard[i].Gate == Gates.Timer;
                    RawImage drawn = Picture(made.transform,
                                             timer ? Glyphs.Timer
                                                   : Glyphs.Gate(clipboard[i].Gate),
                                             timer ? NodeIcon : GateIcon);
                    Color shade = Shelf(Hues.GateSlot(clipboard[i].Gate));
                    shade.a = 0.7f;
                    drawn.color = shade;
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

        /// <summary>Puts the copies down at the pointer. Wires between copied gates
        /// get fresh names; a wire from outside keeps its name and still reads what
        /// it read.</summary>
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

            // What each copied name became: a name already answered on the board
            // gets a new one, or the copy would be wired into the original.
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
                place.Size = copy.Size;
                Vector2 laid = Fenced(Snapped(corner + copy.At));
                place.X = laid.x;
                place.Y = laid.y;
                if (copy.Kind != Place.Note
                    && Elsewhere(-1, copy.Variable, copy.Key))
                {
                    // Something already answers to this: a new name makes the copy
                    // an end of its own.
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
                fresh.Wait.Value = copy.Wait;
                fresh.Duration.Value = copy.Duration;
                fresh.Hold.IsActive = copy.Hold;
                fresh.Stop.IsActive = copy.Stop;
                fresh.Loop.IsActive = copy.Loop;
                touched.Add(fresh.Kind);
                touched.Add(fresh.Mode);
                touched.Add(fresh.Wait);
                touched.Add(fresh.Duration);
                touched.Add(fresh.Hold);
                touched.Add(fresh.Stop);
                touched.Add(fresh.Loop);
                // A fresh name for the pasted gate, and whatever a copied output it
                // fed is called now, so that wire comes along.
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
                for (int k = 0; k < copy.Answers.Length; k++)
                {
                    // Each key it pressed: whatever a copied end on that key is
                    // called now is pressed beside the new name, as for a name.
                    int ended = wasKeys.IndexOf(copy.Answers[k]);
                    if (ended >= 0)
                    {
                        said += ";" + nowKeys[ended];
                    }
                    wasKeys.Add(copy.Answers[k]);
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
                    KeyCode[] keys = clipboard[i].Keys[port];
                    MKey input = port == 0 ? fresh.InputA : fresh.InputB;
                    // Empty to start with: only the wires whose other end came
                    // along go back on.
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
                    else
                    {
                        for (int k = 0; k < keys.Length; k++)
                        {
                            // The last to take a key is the gate that pressed it,
                            // pasted after the ends: an output end on the same key
                            // feeds nothing.
                            int onKey = wasKeys.LastIndexOf(keys[k]);
                            if (onKey >= 0)
                            {
                                Bindings.Added(input, nowKeys[onKey]);
                            }
                            else if (keptKeys.Contains(keys[k]))
                            {
                                Bindings.Added(input, keys[k]);
                            }
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



        /// <summary>IMPORT: takes Besiege's logic gates and timers off the machine
        /// into this block, then lays the board out.</summary>
        private void Import()
        {
            if (served == null)
            {
                return;
            }
            // The game closes the block mapper for the removal, which would take
            // this window.
            lingering = true;
            try
            {
                // The whole block, so the rows, the ends, the layout and the
                // removed blocks undo as one step.
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
                Warned(sheet, took + (took == 1 ? " block imported" : " blocks imported")
                       + (left > 0 ? "\n" + left + " left on the machine" : ""),
                       UIF.Live, NewsSeconds);
            }
            catch (Exception e)
            {
                Warned(sheet, e.Message);
                Log.Warn("import failed: " + e);
            }
        }

        /// <summary>EXPORT: the rows out onto the machine as Besiege's own blocks,
        /// arriving selected under the move tool. The board closes, pinned or not,
        /// so they are in view; held up (`lingering`) while the selection closes
        /// the menu, so a failure can still be said.
        /// </summary>
        private void Export()
        {
            if (served == null)
            {
                return;
            }
            lingering = true;
            try
            {
                if (Conversion.Into(served) > 0)
                {
                    Close();
                }
                else
                {
                    Warned(sheet, "nothing to export");
                }
            }
            catch (Exception e)
            {
                Warned(sheet, e.Message);
                Log.Warn("export failed: " + e);
            }
        }

        /// <summary>The pin switch flipped: one step of undo, like any other edit
        /// the board makes to its block.</summary>
        private void Staking(bool on)
        {
            if (hushed || served == null || served.PinControl == null)
            {
                return;
            }
            served.PinControl.IsActive = on;
            List<MapperType> touched = new List<MapperType>();
            touched.Add(served.PinControl);
            Commit(touched);
        }

        // ---- colours ---------------------------------------------------------

        /// <summary>How tall a colour over a palette button is, and the pieces of
        /// the row of unicolours under the palette.</summary>
        private const float SwatchTall = 26f;
        private const float UniTall = 26f;

        /// <summary>The wire style and the grid switch on the third row, which are
        /// the only two buttons of the window not on its bar.</summary>
        private const float StyleWide = 84f;
        private const float GridWide = 64f;

        /// <summary>One thing on the third row at `at`, and where the next one
        /// goes: `Slot` for a row that is not the title bar.</summary>
        private static float Laid(RectTransform rect, float at, float wide,
                                  float below)
        {
            if (rect != null)
            {
                UIF.Fit(rect, at, below, wide, UniTall);
            }
            return at + wide + 6f;
        }
        private const float SideWords = 40f;
        private const float ModeWide = 84f;
        private const float UniWide = 96f;
        private const float ResetWide = 120f;

        /// <summary>A way-of-colouring selector: a word, a click for the next, a
        /// right-click for the list. Every open board follows, as the colours are
        /// the player's.</summary>
        private Text Selector(out RectTransform rect, bool nodes)
        {
            rect = null;
            GameObject go = UIF.Spawn(UIF.ButtonPrefab, window.transform);
            if (go == null)
            {
                return null;
            }
            rect = go.GetComponent<RectTransform>();
            UIF.NoSwell(go);
            Text label = Caption(go, Hues.Named(nodes ? Hues.NodeMode : Hues.WireMode),
                                 TextAnchor.MiddleCenter);
            UIF.Grow(go, label.transform);
            Tip.On(go, (nodes ? "Nodes" : "Wires")
                       + ": click for the next, right-click for the list");
            bool mine = nodes;
            Button click = go.GetComponent<Button>();
            if (click != null)
            {
                click.onClick.AddListener(delegate
                {
                    Moded(mine, (mine ? Hues.NodeMode : Hues.WireMode) + 1);
                });
            }
            Asks asks = go.AddComponent<Asks>();
            asks.Asked = delegate(Vector2 screen)
            {
                Listed(screen, Hues.Listed(), delegate(int which)
                {
                    Moded(mine, which);
                });
            };
            go.SetActive(false);
            return label;
        }

        private static void Moded(bool nodes, int mode)
        {
            if (nodes)
            {
                Hues.NodeMode = mode;
            }
            else
            {
                Hues.WireMode = mode;
            }
            Recoloured(null);
        }

        /// <summary>EDIT: the colours out, or put away.</summary>
        private void Editing(bool on)
        {
            if (hushed)
            {
                return;
            }
            editing = on;
            for (int i = 0; i < shelf.Count; i++)
            {
                Shown(shelf[i], on);
            }
            Unified();
            Painted();
            // The board's top edge moves with the colour rows; the drawing moves
            // back by as much, so nodes stay put on screen and the rows cover the
            // board's top.
            float was = boardTop;
            Arrange();
            if (content != null && Mathf.Abs(boardTop - was) > 0.01f)
            {
                content.anchoredPosition = Bounded(content.anchoredPosition
                                                   + new Vector2(0f, boardTop - was));
            }
            Canvas.ForceUpdateCanvases();
            Strings();
        }

        /// <summary>The one colour a unicolour mode uses is out only while that mode
        /// is chosen: COLOR and RANDOM make their own, so the box beside them would
        /// be a setting that does nothing. Its room on the row is kept either
        /// way.</summary>
        private void Unified()
        {
            Shown(uniNode, editing && Hues.NodeMode == Hues.Unicolor);
            Shown(uniWire, editing && Hues.WireMode == Hues.Unicolor);
        }

        private static void Shown(Swatch swatch, bool on)
        {
            if (swatch != null)
            {
                Shown(swatch.Root, on);
            }
        }

        private static void Shown(RectTransform rect, bool on)
        {
            if (rect != null && rect.gameObject.activeSelf != on)
            {
                rect.gameObject.SetActive(on);
            }
        }

        /// <summary>The colours, made once and put away until EDIT asks for
        /// them: one over each palette button, and the two unicolours.</summary>
        private void Swatches()
        {
            for (int slot = 0; slot < Hues.Slots; slot++)
            {
                Swatch one = Swatch.Make(window.transform);
                int mine = slot;
                one.Value = Hues.Kind(slot);
                one.Changed = delegate(Color picked)
                {
                    Hues.SetKind(mine, picked);
                    Recoloured(one);
                };
                one.Root.gameObject.SetActive(false);
                swatches.Add(one);
            }
            nodeWords = Words("NODE");
            nodeModeLabel = Selector(out nodeModeRect, true);
            uniNode = Swatch.Make(window.transform);
            uniNode.Value = Hues.Node;
            uniNode.Changed = delegate(Color picked)
            {
                Hues.Node = picked;
                Recoloured(uniNode);
            };
            uniNode.Root.gameObject.SetActive(false);
            wireWords = Words("WIRE");
            wireModeLabel = Selector(out wireModeRect, false);
            uniWire = Swatch.Make(window.transform);
            uniWire.Value = Hues.Wire;
            uniWire.Changed = delegate(Color picked)
            {
                Hues.Wire = picked;
                Recoloured(uniWire);
            };
            uniWire.Root.gameObject.SetActive(false);

            GameObject reset = UIF.Spawn(UIF.ButtonPrefab, window.transform);
            if (reset != null)
            {
                resetRect = reset.GetComponent<RectTransform>();
                UIF.NoSwell(reset);
                // The one button on the board that undoes rather than makes, so it
                // wears the game's red whatever it is doing.
                Text says = Caption(reset, "RESET COLORS", TextAnchor.MiddleCenter);
                if (says != null)
                {
                    says.color = UIF.Hot;
                    UIF.Grow(reset, says.transform);
                }
                Tip.On(reset, "Every color back to how it started");
                Button click = reset.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(delegate
                    {
                        Hues.Reset();
                        Recoloured(null);
                    });
                }
                reset.SetActive(false);
            }

            GameObject sizeBack = UIF.Spawn(UIF.ButtonPrefab, window.transform);
            if (sizeBack != null)
            {
                sizeBackRect = sizeBack.GetComponent<RectTransform>();
                UIF.NoSwell(sizeBack);
                // Red like RESET COLORS, and for the same reason: it undoes rather
                // than makes.
                Text tells = Caption(sizeBack, "RESET SIZE", TextAnchor.MiddleCenter);
                if (tells != null)
                {
                    tells.color = UIF.Hot;
                    UIF.Grow(sizeBack, tells.transform);
                }
                Tip.On(sizeBack, "The board back to the size it started at");
                Button press = sizeBack.GetComponent<Button>();
                if (press != null)
                {
                    press.onClick.AddListener(delegate
                    {
                        Resized(Wiring.WideCells, Wiring.TallCells);
                    });
                }
                sizeBack.SetActive(false);
            }

            // Everything the two rows hold, put out and away together.
            for (int i = 0; i < swatches.Count; i++)
            {
                shelf.Add(swatches[i].Root);
            }
            shelf.Add(nodeWords);
            shelf.Add(nodeModeRect);
            shelf.Add(wireWords);
            shelf.Add(wireModeRect);
            // The two unicolour boxes are not on the shelf: they come and go with
            // the mode that uses them as well as with EDIT (see `Unified`).
            shelf.Add(resetRect);
            shelf.Add(sizeBackRect);
        }

        private RectTransform Words(string said)
        {
            GameObject go = new GameObject("Words");
            go.transform.SetParent(window.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            Caption(go, said, TextAnchor.MiddleLeft);
            go.SetActive(false);
            return rect;
        }

        /// <summary>A colour or a way of colouring changed: every open board takes
        /// it up, except the swatch being dragged, whose hue would
        /// wander.</summary>
        private static void Recoloured(Swatch source)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                {
                    all[i].Repainted(source);
                }
            }
        }

        private void Repainted(Swatch source)
        {
            for (int slot = 0; slot < swatches.Count; slot++)
            {
                if (swatches[slot] != source)
                {
                    swatches[slot].Value = Hues.Kind(slot);
                }
            }
            if (uniNode != null && uniNode != source)
            {
                uniNode.Value = Hues.Node;
            }
            if (uniWire != null && uniWire != source)
            {
                uniWire.Value = Hues.Wire;
            }
            if (nodeModeLabel != null)
            {
                nodeModeLabel.text = Hues.Named(Hues.NodeMode);
            }
            if (wireModeLabel != null)
            {
                wireModeLabel.text = Hues.Named(Hues.WireMode);
            }
            Unified();
            Painted();
            tinted = false;
            if (window != null && window.activeSelf && served != null)
            {
                Redraw();
            }
        }

        /// <summary>The palette's pictures and words in the colours of what they
        /// offer.</summary>
        private void Painted()
        {
            for (int slot = 0; slot < inks.Count; slot++)
            {
                if (inks[slot] != null)
                {
                    inks[slot].color = Shelf(slot);
                }
            }
        }

        /// <summary>A palette button's colour: the colour a node of its kind is
        /// drawn in.
        /// </summary>
        private Color Shelf(int slot)
        {
            return Hues.NodeOf(slot, Hues.Mix(5, slot));
        }

        /// <summary>A node's colour, from the list worked out for this drawing of
        /// the board.</summary>
        private Color Tinted(int node)
        {
            Tints();
            return node >= 0 && node < tints.Count ? tints[node] : Hues.Node;
        }

        private int Seeded(int node)
        {
            Tints();
            return node >= 0 && node < seeds.Count ? seeds[node] : node;
        }

        /// <summary>Which palette kind a node is, for its kind colour.</summary>
        private int Slotted(int node)
        {
            Tints();
            return node >= 0 && node < slots.Count ? slots[node] : Hues.NoteSlot;
        }

        private void Tints()
        {
            if (tinted)
            {
                return;
            }
            tinted = true;
            tints.Clear();
            seeds.Clear();
            slots.Clear();
            int many = Nodes;
            for (int node = 0; node < many; node++)
            {
                int seed = Seed(node);
                seeds.Add(seed);
                LogicRow row = Row(node);
                int slot;
                if (row != null)
                {
                    slot = Hues.GateSlot(row.Gate);     // the timer's, for a timer
                }
                else
                {
                    Place place = Placed(node);
                    slot = place == null || place.Kind == Place.Note ? Hues.NoteSlot
                        : (place.Kind == Place.Input ? Hues.InputSlot : Hues.OutputSlot);
                }
                slots.Add(slot);
                tints.Add(Hues.NodeOf(slot, seed));
            }
        }

        /// <summary>A node's random-colour seed: a gate's row, an end's binding, a
        /// comment's place in the list.</summary>
        private int Seed(int node)
        {
            if (node < Rows)
            {
                // Kept in the layout beside the row's place, so a removal above it
                // does not change its colour.
                return Board.Seed(node);
            }
            Place place = Placed(node);
            if (place == null || place.Kind == Place.Note)
            {
                // Its place among the places, which a new gate does not move.
                return Hues.Mix(2, node - Rows);
            }
            string name = place.Variable != null ? place.Variable : place.Key.ToString();
            return Hues.Mix(3 + place.Kind, Hues.Hashed(name));
        }

        /// <summary>The furthest the wheel zooms out: far enough to see the whole
        /// board, or the usual limit if that is further.</summary>
        private float Least()
        {
            if (sheet == null)
            {
                return LeastZoom;
            }
            Rect room = sheet.rect;
            return Mathf.Min(LeastZoom, Mathf.Min(room.width / BoardWide,
                                                  room.height / BoardTall));
        }

        /// <summary>Fades the grid out as the view pulls back to where its lines
        /// would shimmer; the fence stays.</summary>
        private void Faded()
        {
            if (content == null)
            {
                return;
            }
            Railed(content.localScale.x);
            if (gridLines == null)
            {
                return;
            }
            float alpha = GridInk * Mathf.InverseLerp(0.12f, 0.3f, content.localScale.x);
            if (Mathf.Abs(gridLines.color.a - alpha) > 0.01f)
            {
                gridLines.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        /// <summary>How thick the board's edge is drawn, in the window's own units
        /// whatever the zoom.</summary>
        private const float RailWide = 1.5f;

        /// <summary>The grid's strength over its faint texture, at zooms that show
        /// it.
        /// </summary>
        private const float GridInk = 0.85f;

        /// <summary>Keeps the board's edge line one thickness on screen at any
        /// zoom.
        /// </summary>
        private void Railed(float much)
        {
            if (rails.Count == 0 || Mathf.Abs(much - railed) < 0.0001f)
            {
                return;
            }
            railed = much;
            float thick = RailWide / Mathf.Max(0.01f, much);
            for (int i = 0; i < rails.Count; i++)
            {
                if (rails[i] == null)
                {
                    continue;
                }
                // Laid down in the order top, bottom, left, right.
                bool flat = i % 4 < 2;
                rails[i].sizeDelta = flat ? new Vector2(0f, thick)
                                          : new Vector2(thick, 0f);
            }
        }

        // ---- how big the board is --------------------------------------------

        /// <summary>The two size boxes on the third EDIT row, the "x" between them,
        /// and the words before them.</summary>
        private const float SizeWide = 46f;
        private const float ByWide = 12f;
        private const float SizeWords = 92f;

        /// <summary>One of the two: a whole number of squares, typed or dragged
        /// sideways off the box as a timer's numbers are.</summary>
        private InputField SizeBox(out RectTransform rect, bool wide)
        {
            rect = null;
            GameObject go = UIF.Spawn(UIF.InputPrefab, window.transform);
            if (go == null)
            {
                return null;
            }
            rect = go.GetComponent<RectTransform>();
            InputField field = go.GetComponent<InputField>();
            if (field == null)
            {
                return null;
            }
            UIF.Style(field.textComponent, UIF.Ink, TextAnchor.MiddleCenter);
            Text ghostText = field.placeholder as Text;
            UIF.Style(ghostText, UIF.QuietInk, TextAnchor.MiddleCenter);
            if (ghostText != null)
            {
                ghostText.text = "";
            }
            field.contentType = InputField.ContentType.IntegerNumber;
            Digits(field, SizeWide, UniTall);
            field.onEndEdit.AddListener(Resizing);

            // Dragged as well as typed, the way a timer's wait and duration are: a
            // sheet over the box, so a drag that stays on it still selects text.
            ValueField drag = ValueField.Over(go, field);
            Waving(drag.gameObject);
            bool across = wide;
            drag.dragged = delegate(float pixels) { Scrubbing(across, pixels); };
            // No tip: the words beside it say what the pair is, as they do for the
            // unicolours on the same row.
            go.SetActive(false);
            return field;
        }

        /// <summary>Puts the board's size on everything drawn at that size, and on
        /// the boxes that say what it is.</summary>
        private void Boarded()
        {
            Vector2 span = new Vector2(BoardWide, BoardTall);
            if (paperRect != null)
            {
                paperRect.sizeDelta = span;
            }
            if (gridLines != null)
            {
                // uvRect counts in texture widths: one square per GridStep.
                gridLines.uvRect = new Rect(0f, 0f, boardCells, boardLines);
            }
            if (fenceRect != null)
            {
                fenceRect.sizeDelta = span;
            }
            if (wiresRect != null)
            {
                wiresRect.sizeDelta = span;
            }
            Showing(false);
        }

        /// <summary>The boxes say the size the board is. Not over a box being typed
        /// in, unless the size has just changed under it -- a refused size is put
        /// right in the box that asked for it.</summary>
        private void Showing(bool force)
        {
            if (cellsBox != null && (force || !cellsBox.isFocused))
            {
                cellsBox.text = boardCells.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            if (linesBox != null && (force || !linesBox.isFocused))
            {
                linesBox.text = boardLines.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Either box typed in. Both are read, so what lands is the pair
        /// as it stands rather than the one that was touched.</summary>
        private void Resizing(string typed)
        {
            if (served == null)
            {
                return;
            }
            Resized(Asked(cellsBox, boardCells), Asked(linesBox, boardLines));
        }

        /// <summary>What a box says, or the size it already had where it says
        /// nothing a number can be read out of.</summary>
        private static int Asked(InputField box, int now)
        {
            int said;
            if (box == null || !int.TryParse(box.text,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out said))
            {
                return now;
            }
            return said;
        }

        /// <summary>The board becomes this many squares. The nodes are centred in
        /// it, so the smallest it goes is the box round them, whatever size that
        /// box is standing at: a width or a height asked for under that is given
        /// the least it can have rather than refused outright.</summary>
        private void Resized(int cells, int lines)
        {
            Wiring layout = Board;
            Vector2 low;
            Vector2 high;
            bool any = Spread(out low, out high);
            int leastCells = LeastCells;
            int leastLines = LeastLines;
            if (any)
            {
                leastCells = Mathf.Max(leastCells,
                    Mathf.CeilToInt((high.x - low.x) / GridStep));
                leastLines = Mathf.Max(leastLines,
                    Mathf.CeilToInt((high.y - low.y) / GridStep));
            }
            cells = Mathf.Clamp(cells, leastCells, MostCells);
            lines = Mathf.Clamp(lines, leastLines, MostCells);
            if (cells == boardCells && lines == boardLines)
            {
                // Refused, or the same size typed again: the boxes say what the
                // board is rather than what was asked of it.
                Showing(true);
                return;
            }
            boardCells = cells;
            boardLines = lines;
            layout.Cells = cells;
            layout.Lines = lines;
            Boarded();
            if (any)
            {
                // Everything moves together, so the circuit sits in the middle of
                // whatever size the board is now and none of it is left outside.
                Vector2 by = Snapped(Centre - (low + high) * 0.5f);
                int many = Nodes;
                for (int node = 0; node < many; node++)
                {
                    Put(node, Where(node) + by);
                }
            }
            Kept();
            Redraw();
            // A smaller board may be further out than the wheel now goes, and the
            // view may be off the end of it.
            float least = Least();
            if (content != null && content.localScale.x < least)
            {
                content.localScale = new Vector3(least, least, 1f);
            }
            // The view stays on the middle of what is drawn: a board made bigger
            // grows every way at once, and the nodes would otherwise slide off to
            // one side of the window.
            Middled();
            Showing(true);
        }

        /// <summary>Makes the board at least this big, in board units, and never
        /// smaller: what TIDY asks for when what it lays out would not fit, and
        /// through it what IMPORT asks for. The nodes are not moved -- whoever
        /// asks is about to place them.</summary>
        private void Grown(float wide, float tall)
        {
            int cells = Mathf.Clamp(Mathf.CeilToInt(wide / GridStep),
                                    boardCells, MostCells);
            int lines = Mathf.Clamp(Mathf.CeilToInt(tall / GridStep),
                                    boardLines, MostCells);
            if (cells == boardCells && lines == boardLines)
            {
                return;
            }
            boardCells = cells;
            boardLines = lines;
            Wiring layout = Board;
            layout.Cells = cells;
            layout.Lines = lines;
            Boarded();
        }

        /// <summary>How many squares a pixel of drag is worth, and what is left
        /// over between frames: a slow drag would otherwise round to nothing.
        /// </summary>
        private const float CellsPerPixel = 0.5f;
        private float cellsDrift;
        private float linesDrift;

        /// <summary>Whether a size is being dragged, so the whole drag is filed as
        /// one undo step when the hand lets go rather than one per square.
        /// </summary>
        private bool scaling;

        /// <summary>A size box dragged sideways. The least it goes is the least it
        /// may be, as <see cref="Resized"/> works it out.</summary>
        private void Scrubbing(bool wide, float pixels)
        {
            if (served == null)
            {
                return;
            }
            float drift = (wide ? cellsDrift : linesDrift) + pixels * CellsPerPixel;
            int step = (int)drift;
            if (wide)
            {
                cellsDrift = drift - step;
            }
            else
            {
                linesDrift = drift - step;
            }
            if (step == 0)
            {
                return;
            }
            scaling = true;
            Resized(wide ? boardCells + step : boardCells,
                    wide ? boardLines : boardLines + step);
        }

        // ---- a node let go on another -----------------------------------------

        /// <summary>The node whose back is lit because a drag is over it, and the
        /// colour it wears while it is: a lighter <see cref="Ink"/>.</summary>
        private int landing = -1;
        private static readonly Color LandInk =
            new Color(0.20f, 0.26f, 0.34f, 0.96f);

        /// <summary>The row under this screen point, which a node let go here would
        /// land on. Rows only: an end or a comment is dropped beside, not onto.
        /// </summary>
        private int Landing(Vector2 screen, int except, int gate)
        {
            if (served == null || content == null)
            {
                return -1;
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                return -1;
            }
            // The content's corner is its top left and a node's y counts down from
            // it, so the y is turned over here.
            Vector2 at = new Vector2(local.x, -local.y);
            // Backwards: the last drawn is the one on top.
            for (int node = Rows - 1; node >= 0; node--)
            {
                if (node == except)
                {
                    continue;
                }
                LogicRow sitting = Row(node);
                if (sitting != null && gate >= 0 && sitting.Gate == gate)
                {
                    // A gate on one of its own kind would swap a gate for the same
                    // gate: nothing to show and nothing to do.
                    continue;
                }
                Vector2 corner = Where(node);
                Vector2 span = Span(node);
                if (at.x >= corner.x && at.x <= corner.x + span.x
                    && at.y >= corner.y && at.y <= corner.y + span.y)
                {
                    return node;
                }
            }
            return -1;
        }

        /// <summary>Lights the node a drag is over, and puts the last one back.
        /// </summary>
        private void Lighting(int node)
        {
            if (landing == node)
            {
                return;
            }
            Lit(landing, false);
            landing = node;
            Lit(landing, true);
        }

        private void Lit(int node, bool on)
        {
            if (node < 0 || node >= parts.Count || parts[node] == null)
            {
                return;
            }
            Image plate = parts[node].GetComponent<Image>();
            if (plate != null)
            {
                plate.color = on ? LandInk : Ink;
            }
        }

        /// <summary>Everything reading a node's answer: the port of each gate wired
        /// to it, and each output end it presses.</summary>
        private void Readers(int node, List<int> nodes, List<int> ports)
        {
            nodes.Clear();
            ports.Clear();
            for (int i = 0; i < Nodes; i++)
            {
                if (i == node)
                {
                    continue;
                }
                Place place = Placed(i);
                if (place != null)
                {
                    if (place.Kind == Place.Output && node < Rows
                        && Presses(node, place))
                    {
                        nodes.Add(i);
                        ports.Add(0);
                    }
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    if (Wired(node, i, port))
                    {
                        nodes.Add(i);
                        ports.Add(port);
                    }
                }
            }
        }

        /// <summary>A node's number once the one at <paramref name="gone"/> has been
        /// taken out: the numbers above it close up.</summary>
        private static int Shrunk(int node, int gone)
        {
            return node > gone ? node - 1 : node;
        }

        /// <summary>
        /// A row let go on another row: the one let go picks up the other's wires
        /// and takes its place, and the other goes. Input A comes from input A and
        /// input B from input B, so a gate that reads one input takes only A and a
        /// gate that reads two, landing on one that reads one, fills only A.
        /// Everything that read the other now reads this.
        /// </summary>
        private void Merged(int moved, int target)
        {
            if (served == null || moved == target || moved < 0 || target < 0)
            {
                return;
            }
            LogicRow mine = Row(moved);
            LogicRow theirs = Row(target);
            if (mine == null || !mine.Ready || theirs == null)
            {
                return;
            }
            if (mine.Gate == theirs.Gate)
            {
                // The same kind of gate: swapping one for the other would leave the
                // board exactly as it is, so the drop is a move like any other.
                return;
            }
            List<int> fromA = new List<int>();
            List<int> fromB = new List<int>();
            Feeds(target, 0, fromA);
            Feeds(target, 1, fromB);
            List<int> readers = new List<int>();
            List<int> ports = new List<int>();
            Readers(target, readers, ports);
            bool takesB = Gates.UsesB(mine.Gate);
            Vector2 at = Where(target);

            // The other goes first: its name comes off every input that read it,
            // and this one's name goes on in its place.
            Kill(target);
            moved = Shrunk(moved, target);
            for (int i = 0; i < fromA.Count; i++)
            {
                Join(Shrunk(fromA[i], target), moved, 0);
            }
            for (int i = 0; takesB && i < fromB.Count; i++)
            {
                Join(Shrunk(fromB[i], target), moved, 1);
            }
            for (int i = 0; i < readers.Count; i++)
            {
                Join(moved, Shrunk(readers[i], target), ports[i]);
            }
            Put(moved, at);
            Kept();
            Rebuilt();
        }

        /// <summary>A selector's whole list at the right-click, to go straight to a
        /// choice.
        /// </summary>
        private static void Listed(Vector2 screen, List<string> names,
                                   Action<int> picked)
        {
            Choices.OpenAt(screen, names, delegate(string chosen)
            {
                int which = names.IndexOf(chosen);
                if (which >= 0)
                {
                    picked(which);
                }
            });
        }

        /// <summary>The wire style's list.</summary>
        private void Restyling(Vector2 screen)
        {
            List<string> names = new List<string>();
            names.Add(Styled(Straight));
            names.Add(Styled(Curved));
            names.Add(Styled(Square));
            Listed(screen, names, Restyled);
        }

        /// <summary>One button on the title bar at `along`, and where the next
        /// one goes.</summary>
        private static float Slot(RectTransform rect, float along, float wide)
        {
            if (rect != null)
            {
                UIF.Fit(rect, along, 3f, wide, BarHeight - 6f);
            }
            return along + wide + 3f;
        }

        private static string Styled(int which)
        {
            return which == Straight ? "LINE" : (which == Curved ? "CURVE" : "SQUARE");
        }

        /// <summary>The grid switch. Turned on, it snaps the nodes already drawn
        /// too.
        /// </summary>
        private void Snapping(bool on)
        {
            if (hushed)
            {
                return;
            }
            grid = on;
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

        /// <summary>A place snapped to the nearest intersection while the grid is
        /// on, by the node's corner, where the lines cross.</summary>
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
            Restyled(style + 1);
        }

        /// <summary>The wires drawn another way, on every board: the style is one
        /// setting they all share.</summary>
        private static void Restyled(int which)
        {
            style = ((which % 3) + 3) % 3;
            for (int i = 0; i < all.Count; i++)
            {
                Editor board = all[i];
                if (board == null)
                {
                    continue;
                }
                if (board.styleLabel != null)
                {
                    board.styleLabel.text = Styled(style);
                }
                board.Strings();
            }
        }

        /// <summary>TIDY, as an edit.</summary>
        private void Tidy() { Tidy(false); }

        /// <summary>
        /// Lays the board out: inputs left, outputs right, gates in columns by
        /// distance from an input, ordered to cut crossings. It moves nodes and
        /// never removes one.
        /// <paramref name="quiet"/> is a first opening's layout, kept off the undo.
        /// </summary>
        private void Tidy(bool quiet)
        {
            if (served == null)
            {
                return;
            }
            // The graph is asked thousands of times and does not change: indexed
            // (`named`).
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
            if (many == 0)
            {
                return;
            }
            // Who feeds whom, asked once rather than for every pair of nodes on
            // every pass: a wire is an input reading a name, and `Feeds` knows.
            List<List<int>> ins = new List<List<int>>();
            List<List<int>> outs = new List<List<int>>();
            List<List<bool>> loops = new List<List<bool>>();
            for (int node = 0; node < many; node++)
            {
                ins.Add(new List<int>());
                outs.Add(new List<int>());
                loops.Add(new List<bool>());
            }
            for (int node = 0; node < many; node++)
            {
                for (int port = 0; port < 2; port++)
                {
                    Feeds(node, port, strands);
                    for (int i = 0; i < strands.Count; i++)
                    {
                        int from = strands[i];
                        if (from == node || ins[node].Contains(from))
                        {
                            continue;
                        }
                        ins[node].Add(from);
                        outs[from].Add(node);
                        loops[from].Add(false);
                    }
                }
            }

            // A circuit with a latch in it has a loop, and a loop cannot be laid out
            // left to right: one wire of it has to run back. Which one is chosen
            // here rather than left for the levels to stumble into. A walk forward
            // from the ends that start things marks the wire that closes each loop,
            // and only those are kept out of the levels below -- so every other wire
            // on the board runs forwards.
            int[] colour = new int[many];
            List<int> stack = new List<int>();
            List<int> step = new List<int>();
            for (int round = 0; round < 2; round++)
            {
                for (int root = 0; root < many; root++)
                {
                    // What nothing feeds first, whatever is left over second: a walk
                    // that starts where the signal starts calls the same wires
                    // backwards that a reader would.
                    bool starts = ins[root].Count == 0;
                    if (colour[root] != 0 || (round == 0) != starts)
                    {
                        continue;
                    }
                    colour[root] = 1;
                    stack.Add(root);
                    step.Add(0);
                    while (stack.Count > 0)
                    {
                        int node = stack[stack.Count - 1];
                        int i = step[step.Count - 1];
                        if (i >= outs[node].Count)
                        {
                            colour[node] = 2;
                            stack.RemoveAt(stack.Count - 1);
                            step.RemoveAt(step.Count - 1);
                            continue;
                        }
                        step[step.Count - 1] = i + 1;
                        int next = outs[node][i];
                        if (colour[next] == 1)
                        {
                            // Into something this walk is still inside: the wire
                            // that closes the loop.
                            loops[node][i] = true;
                            continue;
                        }
                        if (colour[next] == 0)
                        {
                            colour[next] = 1;
                            stack.Add(next);
                            step.Add(0);
                        }
                    }
                }
            }

            // The levels: every wire but a loop-closing one puts its far end at
            // least one column to the right of where it leaves.
            int[] depth = new int[many];
            for (int pass = 0; pass < many; pass++)
            {
                bool moved = false;
                for (int node = 0; node < many; node++)
                {
                    for (int i = 0; i < outs[node].Count; i++)
                    {
                        if (loops[node][i])
                        {
                            continue;
                        }
                        int next = outs[node][i];
                        if (depth[node] + 1 > depth[next])
                        {
                            depth[next] = depth[node] + 1;
                            moved = true;
                        }
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
            // Columns, then an order within each: a node level with the average of
            // its feeders gets straighter wires. A few passes of averaging.
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
                    // A comment stays where it was put, beside what it comments on.
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
            if (columns.Count == 0)
            {
                return;
            }
            // Which column each node ended in: the gaps below are measured by it,
            // and a comment is in none.
            int[] where = new int[many];
            for (int node = 0; node < many; node++)
            {
                where[node] = -1;
            }
            for (int column = 0; column < columns.Count; column++)
            {
                for (int i = 0; i < columns[column].Count; i++)
                {
                    where[columns[column][i]] = column;
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
            // Swept down and then up, rather than four times the one way: going
            // down a node settles level with the middle of what feeds it, going up
            // with the middle of what it feeds, and a few of each takes the
            // crossings out of a board that one direction alone leaves in.
            for (int pass = 0; pass < 6; pass++)
            {
                bool down = pass % 2 == 0;
                for (int c = 0; c < columns.Count; c++)
                {
                    int column = down ? c : columns.Count - 1 - c;
                    List<int> here = columns[column];
                    float[] middle = new float[here.Count];
                    for (int i = 0; i < here.Count; i++)
                    {
                        List<int> near = down ? ins[here[i]] : outs[here[i]];
                        float total = 0f;
                        int seen = 0;
                        for (int j = 0; j < near.Count; j++)
                        {
                            if (where[near[j]] < 0)
                            {
                                continue;
                            }
                            total += rank[near[j]];
                            seen++;
                        }
                        // Nothing wired that way keeps the place it had, so the
                        // untouched parts of a board do not shuffle every time this
                        // is asked.
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
            // A whole number of squares on the grid, or the rows land ragged.
            if (grid)
            {
                pitch = Mathf.Ceil(pitch / GridStep) * GridStep;
            }
            // A gap between columns rather than a pitch, so narrow gate columns
            // space evenly with wide end columns, and wires pass between them. One
            // layout whatever the wires are drawn like: a board that rearranged
            // itself when the wire style changed was two boards to learn.
            float gap = grid ? Mathf.Ceil(90f / GridStep) * GridStep : 90f;
            // Centred on the board's middle (`Centre`), so the columns are measured
            // first.
            float centre = Centre.y;
            float[] widths = new float[columns.Count];
            float across = 0f;
            for (int column = 0; column < columns.Count; column++)
            {
                float span = 0f;
                for (int i = 0; i < columns[column].Count; i++)
                {
                    float mine = Wide(columns[column][i]);
                    if (mine > span)
                    {
                        span = mine;
                    }
                }
                widths[column] = span;
                across += span + (column > 0 ? gap : 0f);
            }
            // The board grows to hold what is being laid out, so a tidy never has
            // to squeeze a wide circuit into a board too small for it -- which is
            // also how IMPORT sizes the board, since it tidies what it brings in.
            int tallest = 0;
            for (int column = 0; column < columns.Count; column++)
            {
                if (columns[column].Count > tallest)
                {
                    tallest = columns[column].Count;
                }
            }
            Grown(across + GridStep * 4f,
                  (tallest - 1) * pitch + NodeHeight + GridStep * 4f);
            // Measured again: the middle has moved if the board just grew.
            centre = Centre.y;
            float left = Centre.x - across * 0.5f;
            if (grid)
            {
                left = Mathf.Round(left / GridStep) * GridStep;
            }
            for (int column = 0; column < columns.Count; column++)
            {
                float top = centre - (columns[column].Count - 1) * pitch * 0.5f;
                if (grid)
                {
                    top = Mathf.Round(top / GridStep) * GridStep;
                }
                for (int i = 0; i < columns[column].Count; i++)
                {
                    // Centred in a mixed column; left-aligned on the grid, where
                    // half a node is half a square.
                    float mine = Wide(columns[column][i]);
                    float aside = grid ? 0f : (widths[column] - mine) * 0.5f;
                    Move(columns[column][i],
                         new Vector2(left + aside, top + i * pitch));
                }
                left += widths[column] + gap;
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
            // Nine-sliced, so the corners keep their radius at any width.
            Image image = go.AddComponent<Image>();
            image.sprite = Glyphs.Plated;
            image.type = Image.Type.Sliced;
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
