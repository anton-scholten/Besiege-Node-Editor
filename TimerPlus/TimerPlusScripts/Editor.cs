using System;
using System.Collections.Generic;
using UnityEngine;
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
        private const float StartWidth = 720f;
        private const float StartHeight = 470f;
        private const float LeastWidth = 420f;
        private const float LeastHeight = 300f;

        /// <summary>How the wires are drawn. Three ways, cycled by the switch in
        /// the title bar: a straight line, a curve, or right angles.</summary>
        private const int Straight = 0;
        private const int Curved = 1;
        private const int Square = 2;

        /// <summary>Kept between openings rather than saved with the machine: how
        /// somebody likes to look at a board is about them, not about the board.
        /// </summary>
        private static int style = Curved;
        private const float BarHeight = 26f;
        private const float Margin = 8f;
        private const float NodeWidth = 124f;
        private const float NodeHeight = 56f;

        /// <summary>How big a gate's picture is drawn: in the palette, and on the
        /// node itself.</summary>
        private const float PaletteIcon = 26f;
        private const float NodeIcon = 24f;
        private const float PortSize = 14f;
        private const float WireWidth = 2f;

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Color Ink = new Color(0.10f, 0.13f, 0.17f, 0.96f);
        private static readonly Color WireInk = new Color(0.55f, 0.72f, 0.85f, 0.85f);
        private static readonly Color LiveInk = new Color(0.012f, 1f, 0.847f, 1f);

        /// <summary>Besiege's red, which is what the game paints anything that
        /// removes something.</summary>
        private static readonly Color Hot = new Color(0.92f, 0.13f, 0.29f, 1f);

        private static Editor one;

        private LogicGatePlusBehaviour served;
        private Canvas canvas;
        private GameObject window;
        private RectTransform windowRect;
        private RectTransform sheet;
        private Toggle pinBox;
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

        /// <summary>Opens the editor on a board. One editor, whichever board was
        /// asked for last.</summary>
        public static void Open(LogicGatePlusBehaviour block)
        {
            if (one == null || block == null)
            {
                return;
            }
            one.Show(block);
        }

        /// <summary>Whether the editor is up on this block, which is what the
        /// table's own button colours itself by.</summary>
        public static bool Showing(LogicGatePlusBehaviour block)
        {
            return one != null && one.served == block && block != null
                && one.window != null && one.window.activeSelf;
        }

        /// <summary>Open on this block, or closed if it already is.</summary>
        public static void Toggle(LogicGatePlusBehaviour block)
        {
            if (Showing(block))
            {
                one.Close();
                return;
            }
            Open(block);
        }

        private void Awake()
        {
            one = this;
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
            System.Text.StringBuilder said = new System.Text.StringBuilder();
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

        private void Update()
        {
            if (served == null || window == null || !window.activeSelf)
            {
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
            else if (Hotkeys.Undo)
            {
                Undo();
            }
            else if (Hotkeys.Redo)
            {
                Redo();
            }
            Carrying();
            if (Input.GetKeyDown(KeyCode.Escape) || Shut())
            {
                Close();
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
            Adopt();
            // Whatever changed it -- the table, an undo, a redo -- the board is
            // drawn from the rows and the rows have moved.
            board = null;
            Redraw();
        }

        private void Show(LogicGatePlusBehaviour block)
        {
            served = block;
            if (!Build())
            {
                return;
            }
            window.SetActive(true);
            board = null;
            read = null;
            mark = null;
            Adopt();
            // The shared tooltip and list belong to the window opened last.
            if (canvas != null)
            {
                RectTransform home = canvas.GetComponent<RectTransform>();
                Tip.Tips.Home(home);
                Choices.Home(home);
            }
            Redraw();
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
                windowRect.anchoredPosition = Vector2.zero;

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
        private RectTransform boardRect;
        private RectTransform nameRect;
        private RectTransform styleRect;
        private RectTransform tidyRect;
        private RectTransform undoRect;
        private RectTransform redoRect;
        private readonly List<RectTransform> palette = new List<RectTransform>();
        private readonly List<Corner> corners = new List<Corner>();
        private Text styleLabel;

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

            GameObject back = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (back != null)
            {
                undoRect = back.GetComponent<RectTransform>();
                UIF.NoSwell(back);
                Grow(back, Caption(back, "UNDO", TextAnchor.MiddleCenter).transform);
                Button click = back.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Undo);
                }
            }

            GameObject again = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (again != null)
            {
                redoRect = again.GetComponent<RectTransform>();
                UIF.NoSwell(again);
                Grow(again, Caption(again, "REDO", TextAnchor.MiddleCenter).transform);
                Button click = again.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Redo);
                }
            }

            GameObject shut = UIF.Spawn(UIF.ButtonPrefab, bar.transform);
            if (shut != null)
            {
                shutRect = shut.GetComponent<RectTransform>();
                UIF.NoSwell(shut);
                Text cross = Caption(shut, "X", TextAnchor.MiddleCenter);
                cross.color = Hot;
                Grow(shut, cross.transform);
                Button click = shut.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(Close);
                }
            }

            Add(0, Place.Input, -1, "INPUT", "");
            Add(0, Place.Output, -1, "OUTPUT", "");
            for (int g = 0; g < Gates.Count; g++)
            {
                Add(g, -1, g, "", Gates.Names[g]);
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
            mesh.anchorMin = new Vector2(0f, 1f);
            mesh.anchorMax = new Vector2(0f, 1f);
            mesh.pivot = new Vector2(0.5f, 0.5f);
            mesh.anchoredPosition = Vector2.zero;
            mesh.sizeDelta = new Vector2(GridReach, GridReach);
            RawImage lines = paper.AddComponent<RawImage>();
            lines.texture = Glyphs.Grid;
            lines.color = new Color(1f, 1f, 1f, 0.5f);
            lines.raycastTarget = false;
            // uvRect counts in texture widths, so this is one square per GridStep.
            lines.uvRect = new Rect(0f, 0f, GridReach / GridStep,
                                    GridReach / GridStep);

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
        private const float GridReach = 4000f;
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
            if (tidyRect != null)
            {
                UIF.Fit(tidyRect, 3f + bit * 3.4f + 3f, 3f, bit * 2.4f, bit);
            }
            if (undoRect != null)
            {
                UIF.Fit(undoRect, 3f + bit * 5.8f + 6f, 3f, bit * 2.4f, bit);
            }
            if (redoRect != null)
            {
                UIF.Fit(redoRect, 3f + bit * 8.2f + 9f, 3f, bit * 2.4f, bit);
            }
            if (shutRect != null)
            {
                UIF.Fit(shutRect, size.x - bit - 3f, 3f, bit, bit);
            }

            float y = BarHeight + Margin;
            float step = (size.x - Margin * 2f) / (Gates.Count + 2);
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

        private void Add(int gate, int kind, int which, string words, string tip)
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
                Grow(go, Picture(go.transform, Glyphs.Gate(gate), PaletteIcon)
                         .transform);
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
            int shown = what;
            drag.Carrying = delegate(Vector2 screen) { Carrying(shown, screen); };
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
        private void Carrying(int gate, Vector2 screen)
        {
            if (canvas == null)
            {
                return;
            }
            if (ghost == null)
            {
                ghost = Rounded(canvas.transform, 0f, 0f, NodeWidth, NodeHeight,
                                new Color(Ink.r, Ink.g, Ink.b, 0.55f));
                if (gate >= 0)
                {
                    RawImage drawn = Picture(ghost.transform, Glyphs.Gate(gate),
                                             NodeIcon);
                    drawn.color = new Color(1f, 1f, 1f, 0.7f);
                }
                RectTransform rect = ghost.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
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
            Born(kind, gate, new Vector2(local.x - NodeWidth * 0.5f,
                                         -local.y - NodeHeight * 0.5f), true);
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
            served.LayoutControl.Value = Board.Save();
            Apply(served.LayoutControl);
        }

        /// <summary>The same, for a hand that moved something: one undo step for
        /// the move.</summary>
        private void Kept()
        {
            Keep();
            Filed();
        }

        /// <summary>
        /// Settles one changed control.
        ///
        /// `ApplyValue` is what reconciles the live value with the one the block
        /// loads from. In a network game the edit has to go out to the other
        /// players instead, and `BlockMapper.OnEditField` is the way it does --
        /// which also files its own undo entry, so <see cref="Filed"/> stands
        /// aside there.
        /// </summary>
        private void Apply(MapperType changed)
        {
            if (changed == null)
            {
                return;
            }
            try
            {
                if (Networked())
                {
                    BlockMapper mapper = BlockMapper.CurrentInstance;
                    if (mapper != null && BlockMapper.IsOpen)
                    {
                        BlockMapper.OnEditField(mapper.Current, changed);
                        return;
                    }
                }
                changed.ApplyValue();
            }
            catch (Exception)
            {
                // The value is written either way; this is the reconciliation.
            }
        }

        /// <summary>Whether somebody else's game is listening: then every edit goes
        /// out through Besiege's own handler rather than being applied here.
        /// </summary>
        private static bool Networked()
        {
            try
            {
                return EditFieldHandler.Instance != null;
            }
            catch (Exception)
            {
                return false;
            }
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
                BlockBehaviour block = served.BlockBehaviour;
                if (block == null)
                {
                    return null;
                }
                // `BlockInfo.FromBlockBehaviour` hands back the last state the
                // block saved rather than what its controls hold now, so the block
                // is asked to save first or the snapshot is a frame stale.
                block.OnSave(new XDataHolder());
                return BlockInfo.FromBlockBehaviour(block);
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
            if (before == null || served == null || Networked())
            {
                return;
            }
            if (Reading() == marked)
            {
                return;                 // nothing actually moved
            }
            try
            {
                Machine machine = Machine.Active();
                if (machine == null || machine.UndoSystem == null)
                {
                    return;
                }
                BlockInfo after = Snap();
                if (after != null)
                {
                    machine.UndoSystem.EditBlock(after, before);
                }
            }
            catch (Exception e)
            {
                Log.Warn("could not file the edit for undo: " + e.Message);
            }
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
                    MKey input = port == 0 ? row.InputA : row.InputB;
                    string variable = Bindings.IsVariable(input)
                        ? Bindings.Variable(input) : null;
                    KeyCode key = variable == null ? Bindings.Code(input)
                                                   : KeyCode.None;
                    if ((variable == null && key == KeyCode.None)
                        || Feeding(i, port) >= 0)
                    {
                        continue;           // nothing bound, or a row makes it
                    }
                    Ensure(Place.Input, variable, key);
                }

                string said = Bindings.IsVariable(row.Emulate)
                    ? Bindings.Variable(row.Emulate) : null;
                KeyCode code = said == null ? Bindings.Code(row.Emulate)
                                            : KeyCode.None;
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
                    if (name.Length > 0 && !Read(name, KeyCode.None))
                    {
                        Ensure(Place.Output, name, KeyCode.None);
                    }
                }
            }
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
                    string had = Bindings.IsVariable(input)
                        ? Bindings.Variable(input) : null;
                    if (variable != null ? had == variable
                        : (had == null && Bindings.Code(input) == key
                           && key != KeyCode.None))
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
            made.X = kind == Place.Input ? 20f : 20f + 3f * (NodeWidth + 46f);
            made.Y = 20f + stacked * (NodeHeight + 18f);
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
        private int Feeding(int node, int port)
        {
            LogicRow row = Row(node);
            string want = null;
            KeyCode key = KeyCode.None;
            if (row != null && row.Ready)
            {
                MKey input = port == 0 ? row.InputA : row.InputB;
                want = Bindings.IsVariable(input) ? Bindings.Variable(input) : null;
                key = want == null ? Bindings.Code(input) : KeyCode.None;
            }
            else
            {
                Place place = Placed(node);
                if (place == null || place.Kind != Place.Output)
                {
                    return -1;
                }
                want = place.Variable;
                key = place.Key;
            }
            if (want == null && key == KeyCode.None)
            {
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

        /// <summary>Whether anything takes this node's answer.</summary>
        private bool Feeds(int node)
        {
            for (int i = 0; i < Nodes; i++)
            {
                for (int port = 0; port < 2; port++)
                {
                    if (Feeding(i, port) == node)
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
            if (served == null || from == to)
            {
                return;
            }
            string variable;
            KeyCode key;
            Answer(from, out variable, out key);
            if (variable == null && key == KeyCode.None)
            {
                variable = Fresh(from);
                Named(from, variable);
            }

            LogicRow row = Row(to);
            List<MapperType> touched = new List<MapperType>();
            if (row != null && row.Ready)
            {
                MKey input = port == 0 ? row.InputA : row.InputB;
                if (variable != null)
                {
                    Bindings.BindVariable(input, variable);
                }
                else
                {
                    Bindings.Bind(input, key);
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

        /// <summary>Takes the newest wire off a port.</summary>
        private void Loose(int node, int port, bool output)
        {
            List<MapperType> touched = new List<MapperType>();
            if (!output)
            {
                LogicRow row = Row(node);
                if (row != null && row.Ready)
                {
                    MKey input = port == 0 ? row.InputA : row.InputB;
                    Bindings.Bind(input, KeyCode.None);
                    touched.Add(input);
                }
                else
                {
                    Place place = Placed(node);
                    if (place != null && place.Kind == Place.Output)
                    {
                        // The newest of however many press its name -- the last row
                        // to have been wired to it is the one the hand is reaching
                        // for.
                        for (int i = Rows - 1; i >= 0; i--)
                        {
                            if (!Presses(i, place))
                            {
                                continue;
                            }
                            Drops(i, place, touched);
                            break;
                        }
                    }
                }
            }
            else
            {
                // The newest wire off an answer: the last row that reads it.
                string variable;
                KeyCode key;
                Answer(node, out variable, out key);
                for (int i = Nodes - 1; i >= 0; i--)
                {
                    bool cut = false;
                    for (int port2 = 0; port2 < 2 && !cut; port2++)
                    {
                        if (Feeding(i, port2) != node)
                        {
                            continue;
                        }
                        LogicRow row = Row(i);
                        if (row != null && row.Ready)
                        {
                            MKey input = port2 == 0 ? row.InputA : row.InputB;
                            Bindings.Bind(input, KeyCode.None);
                            touched.Add(input);
                        }
                        else
                        {
                            Place place = Placed(i);
                            if (place != null)
                            {
                                Drops(node, place, touched);
                            }
                        }
                        cut = true;
                    }
                    if (cut)
                    {
                        break;
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

        /// <summary>Adds an output node's name to what a row presses.</summary>
        private void Adds(int node, Place place, List<MapperType> touched)
        {
            LogicRow row = Row(node);
            if (row == null || !row.Ready || place.Variable == null)
            {
                return;
            }
            string had = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            string all = had == null || had.Length == 0
                ? place.Variable : had + ";" + place.Variable;
            Bindings.BindVariable(row.Emulate, all);
            touched.Add(row.Emulate);
            Keep();
        }

        /// <summary>And takes it off again.</summary>
        private void Drops(int node, Place place, List<MapperType> touched)
        {
            LogicRow row = Row(node);
            if (row == null || !row.Ready || place.Variable == null)
            {
                return;
            }
            string had = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
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

        /// <summary>A name for an answer nobody has named.</summary>
        private string Fresh(int node)
        {
            return served.Prefix + node;
        }

        private void Commit(List<MapperType> touched)
        {
            Keep();
            // The table is the same rows seen another way, and it is probably open
            // behind this window.
            Panel.Refill();
            for (int i = 0; i < touched.Count; i++)
            {
                Apply(touched[i]);
            }
            Filed();
        }

        /// <summary>
        /// Throws away the drawn board and draws it again from the rows.
        ///
        /// A board this size redraws in nothing, and a redraw that cannot be out of
        /// step with the table is worth more than a clever one.
        /// </summary>
        private void Redraw()
        {
            Cut();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null)
                {
                    Destroy(parts[i]);
                }
            }
            parts.Clear();
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
            Strings();
        }

        /// <summary>One node: a row of the table, or one of the two ends.</summary>
        private void Draw(int index)
        {
            LogicRow row = Row(index);
            Place place = Placed(index);
            Vector2 at = row != null ? Board.Spot(index)
                                     : new Vector2(place.X, place.Y);

            GameObject body = Rounded(content, at.x, at.y, NodeWidth, NodeHeight,
                                      Ink);
            parts.Add(body);

            int me = index;
            if (Picked(index))
            {
                // A dashed edge round it, which is what says picked out.
                GameObject edge = Edging(body.transform, Color.white);
                RectTransform rect = edge.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-2f, -2f);
                rect.offsetMax = new Vector2(2f, 2f);
                Dashes(edge, NodeWidth + 4f, NodeHeight + 4f);
                edge.SetActive(true);
            }

            NodeDrag drag = body.AddComponent<NodeDrag>();
            drag.Moved = delegate(Vector2 by)
            {
                Vector2 now = Where(me) + new Vector2(by.x, -by.y);
                Move(me, now);
                RectTransform rect = body.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(now.x, -now.y);
                Strings();
            };
            drag.Dropped = Kept;

            GameObject head = UIF.Spawn(UIF.ButtonPrefab, body.transform);
            if (head != null)
            {
                UIF.Fit(head.GetComponent<RectTransform>(), PortSize + 2f, 3f,
                        NodeWidth - PortSize * 2f - 4f, 26f);
                UIF.NoSwell(head);
                if (row != null)
                {
                    Caption(head, "", TextAnchor.MiddleCenter);
                    Picture(head.transform, Glyphs.Gate(row.Gate), NodeIcon);
                }
                else
                {
                    Caption(head, place.Kind == Place.Input ? "INPUT" : "OUTPUT",
                            TextAnchor.MiddleCenter);
                }
                Button click = head.GetComponent<Button>();
                if (click != null)
                {
                    // Nothing to open, but a click with a modifier picks the node
                    // out -- and the heading is the biggest part of it to hit.
                    click.onClick.AddListener(delegate
                    {
                        if (Input.GetKey(KeyCode.LeftControl)
                            || Input.GetKey(KeyCode.RightControl)
                            || Input.GetKey(KeyCode.LeftShift)
                            || Input.GetKey(KeyCode.RightShift))
                        {
                            Pick(me, true);
                        }
                    });
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
                        // Level with the gate's own picture and big enough to read
                        // the letter on: the head is 26 tall from y = 3, so this
                        // sits on the same middle.
                        float side = 22f;
                        UIF.Fit(mode.GetComponent<RectTransform>(),
                                NodeWidth - PortSize - side - 4f,
                                3f + (26f - side) * 0.5f, side, side);
                        UIF.NoSwell(mode);
                        Caption(mode, Gates.ModeLetter(row.Gate),
                                TextAnchor.MiddleCenter);
                        Toggle box = mode.GetComponent<Toggle>();
                        if (box != null)
                        {
                            box.isOn = row.Switch;
                            LogicRow mine = row;
                            box.onValueChanged.AddListener(delegate(bool on)
                            {
                                mine.Mode.IsActive = on;
                                List<MapperType> touched = new List<MapperType>();
                                touched.Add(mine.Mode);
                                Commit(touched);
                            });
                        }
                    }
                }
                Names(body, index);
            }
            else
            {
                // An end of the board: what it stands for, as a key or a variable.
                KeyCell cell = KeyCell.Make(body.transform, PortSize + 2f, 31f,
                                            NodeWidth - PortSize * 2f - 4f, 22f, 18f);
                cell.Load(place.Variable, place.Key);
                Place mine = place;
                cell.Changed = delegate(KeyCell edited)
                {
                    Rebind(mine, edited);
                };
            }

            // The ports. An answer on the right, inputs on the left -- one for a
            // gate that reads one, none at all on an input node.
            bool answers = row != null || place.Kind == Place.Input;
            ports[index * 3] = answers
                ? Port(body.transform, NodeWidth - PortSize,
                       NodeHeight * 0.5f - PortSize * 0.5f, true, index, 0)
                : null;
            if (row != null)
            {
                bool two = Gates.UsesB(row.Gate);
                ports[index * 3 + 1] = Port(body.transform, 0f,
                    two ? 10f : NodeHeight * 0.5f - PortSize * 0.5f, false, index, 0);
                if (two)
                {
                    ports[index * 3 + 2] = Port(body.transform, 0f,
                        NodeHeight - 10f - PortSize, false, index, 1);
                }
            }
            else if (place.Kind == Place.Output)
            {
                ports[index * 3 + 1] = Port(body.transform, 0f,
                    NodeHeight * 0.5f - PortSize * 0.5f, false, index, 0);
            }

            GameObject bin = UIF.Spawn(UIF.ButtonPrefab, body.transform);
            if (bin != null)
            {
                // Top right of every node, so removing one is the same reach
                // whatever kind it is.
                UIF.Fit(bin.GetComponent<RectTransform>(),
                        NodeWidth - PortSize - 1f, 1f, PortSize, PortSize);
                UIF.NoSwell(bin);
                Text cross = Caption(bin, "x", TextAnchor.MiddleCenter);
                cross.color = Hot;
                Grow(bin, cross.transform);
                Button click = bin.GetComponent<Button>();
                if (click != null)
                {
                    click.onClick.AddListener(delegate { Kill(me); });
                }
            }
        }

        /// <summary>The box holding the name a gate's answer goes out under.</summary>
        private void Names(GameObject body, int index)
        {
            GameObject box = UIF.Spawn(UIF.InputPrefab, body.transform);
            if (box == null)
            {
                return;
            }
            UIF.Fit(box.GetComponent<RectTransform>(), PortSize + 2f, 31f,
                    NodeWidth - PortSize * 2f - 4f, 22f);
            InputField field = box.GetComponent<InputField>();
            if (field == null)
            {
                return;
            }
            UIF.Style(field.textComponent, UIF.Ink, TextAnchor.MiddleCenter);
            UIF.Style(field.placeholder as Text, UIF.Ink, TextAnchor.MiddleCenter);
            if (field.textComponent != null)
            {
                field.textComponent.fontSize = 20;
            }
            string variable;
            KeyCode key;
            Answer(index, out variable, out key);
            field.text = variable != null ? variable
                : (key == KeyCode.None ? "" : Bindings.Spell(key));
            int me = index;
            field.onEndEdit.AddListener(delegate(string typed)
            {
                Renaming(me, typed);
            });
        }

        /// <summary>A gate's answer given a name by hand. Everything reading the
        /// old one is moved to the new, or the wires would come apart.</summary>
        private void Renaming(int node, string typed)
        {
            LogicRow row = Row(node);
            if (row == null || !row.Ready)
            {
                return;
            }
            string was = Bindings.IsVariable(row.Emulate)
                ? Bindings.Variable(row.Emulate) : null;
            string now = typed == null ? "" : typed.Trim();
            List<MapperType> touched = new List<MapperType>();
            if (now.Length == 0)
            {
                Bindings.Bind(row.Emulate, KeyCode.None);
            }
            else
            {
                Bindings.BindVariable(row.Emulate, now);
            }
            touched.Add(row.Emulate);

            if (was != null && now.Length > 0)
            {
                for (int i = 0; i < Rows; i++)
                {
                    LogicRow other = Row(i);
                    if (other == null || !other.Ready)
                    {
                        continue;
                    }
                    for (int port = 0; port < 2; port++)
                    {
                        MKey input = port == 0 ? other.InputA : other.InputB;
                        if (Bindings.IsVariable(input)
                            && Bindings.Variable(input) == was)
                        {
                            Bindings.BindVariable(input, now);
                            touched.Add(input);
                        }
                    }
                }
            }
            Commit(touched);
            Redraw();
        }

        /// <summary>An end of the board rebound by hand.</summary>
        private void Rebind(Place place, KeyCell cell)
        {
            string was = place.Variable;
            KeyCode wasKey = place.Key;
            place.Variable = cell.UsesVariable ? cell.Variable : null;
            place.Key = cell.UsesVariable ? KeyCode.None : cell.Code;

            // Everything that was wired to it follows, or the wire silently comes
            // apart the moment the name changes.
            List<MapperType> touched = new List<MapperType>();
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
                    string had = Bindings.IsVariable(input)
                        ? Bindings.Variable(input) : null;
                    KeyCode hadKey = had == null ? Bindings.Code(input) : KeyCode.None;
                    bool mine = was != null ? had == was
                        : (had == null && hadKey == wasKey && wasKey != KeyCode.None);
                    if (!mine)
                    {
                        continue;
                    }
                    if (place.Variable != null)
                    {
                        Bindings.BindVariable(input, place.Variable);
                    }
                    else
                    {
                        Bindings.Bind(input, place.Key);
                    }
                    touched.Add(input);
                }
            }
            Commit(touched);
            Redraw();
        }

        private Vector2 Where(int node)
        {
            Place place = Placed(node);
            return place != null ? new Vector2(place.X, place.Y) : Board.Spot(node);
        }

        private void Move(int node, Vector2 to)
        {
            Place place = Placed(node);
            if (place != null)
            {
                place.X = to.x;
                place.Y = to.y;
                return;
            }
            Board.Put(node, to);
        }

        /// <summary>One port: a small square that arms a wire or lands one.</summary>
        private RectTransform Port(Transform host, float x, float y, bool output,
                                   int node, int port)
        {
            GameObject go = UIF.Spawn(UIF.ButtonPrefab, host);
            if (go == null)
            {
                return null;
            }
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, PortSize, PortSize);
            UIF.NoSwell(go);
            Caption(go, "", TextAnchor.MiddleCenter);
            // Empty until a wire lands on it, which is the one thing about a port
            // worth seeing from across the board.
            Place sitting = Placed(node);
            bool wired;
            if (output)
            {
                wired = Feeds(node);
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
            Picture(go.transform, wired ? Glyphs.Dot : Glyphs.Ring, PortSize - 2f);

            // Both ways of wiring: press and drag from one port to another, which
            // is what a hand reaches for, and click one then the other, which is
            // what a hand that has already let go can still do.
            PortMark mark = go.AddComponent<PortMark>();
            mark.Node = node;
            mark.Port = port;
            mark.Output = output;
            mark.Pulling = Pulling;
            mark.Landed = Landed;

            int which = node;
            int side = port;
            bool source = output;
            Button click = go.GetComponent<Button>();
            if (click != null)
            {
                click.onClick.AddListener(delegate { Touched(which, side, source); });
            }
            return go.GetComponent<RectTransform>();
        }

        /// <summary>Where the wire being pulled ends now, in the sheet's own
        /// coordinates, or nowhere.</summary>
        private bool pulling;
        private Vector2 pullTo;
        private PortMark pullFrom;

        private void Pulling(PortMark from, Vector2 screen)
        {
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
            Strings();
        }

        private void Landed(PortMark from, PortMark to)
        {
            pulling = false;
            pullFrom = null;
            if (served != null && to == null)
            {
                // Pulled off and dropped on nothing: that is how a wire is taken
                // off. An input holds one, so there is no question which; an answer
                // may feed several, and the last one made is the one the hand was
                // reaching for.
                Loose(from.Node, from.Port, from.Output);
                pending = -1;
                return;
            }
            if (served != null && to != null && from.Node != to.Node
                && from.Output != to.Output)
            {
                // Either way round: pulled from the answer to the input, or from
                // the input back to the answer.
                int source = from.Output ? from.Node : to.Node;
                PortMark sink = from.Output ? to : from;
                pending = -1;
                // Join redraws: a port is empty or full, and which it is has just
                // changed.
                Join(source, sink.Node, sink.Port);
                return;
            }
            pending = -1;
            Strings();
        }

        /// <summary>
        /// A port was clicked: an output arms the wire, an input lands it. An input
        /// clicked with nothing armed cuts whatever fed it, which is the only way
        /// to unwire something and is where somebody looks for it.
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
                Loose(node, port, false);
            }
            else
            {
                int armed = pending;
                pending = -1;
                Join(armed, node, port);
            }
        }

        /// <summary>Draws every wire: a thin plate from one port to the other,
        /// turned to point at it.</summary>
        private void Strings()
        {
            Cut();
            if (served == null || sheet == null)
            {
                return;
            }
            if (pulling && pullFrom != null)
            {
                RectTransform end = Held(pullFrom.Output
                    ? pullFrom.Node * 3 : pullFrom.Node * 3 + 1 + pullFrom.Port);
                if (end != null)
                {
                    // Always drawn from the answer end: a curve leaves one end
                    // sideways and arrives at the other sideways, so drawn the
                    // other way round it bows backwards.
                    if (pullFrom.Output)
                    {
                        String(Middle(end), pullTo, LiveInk);
                    }
                    else
                    {
                        String(pullTo, Middle(end), LiveInk);
                    }
                }
            }

            // Every wire there is: for each node's ports, whatever feeds it. A
            // gate's input takes one, and an output node takes as many as press its
            // name -- three gates all raising "door" is three wires into one end.
            int many = Nodes;
            for (int node = 0; node < many; node++)
            {
                Place place = Placed(node);
                if (place != null && place.Kind == Place.Output)
                {
                    for (int from = 0; from < Rows; from++)
                    {
                        if (!Presses(from, place))
                        {
                            continue;
                        }
                        Draw(from * 3, node * 3 + 1, from == pending);
                    }
                    continue;
                }
                for (int port = 0; port < 2; port++)
                {
                    int from = Feeding(node, port);
                    if (from >= 0)
                    {
                        Draw(from * 3, node * 3 + 1 + port, from == pending);
                    }
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

        /// <summary>Takes down every wire that is drawn.</summary>
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
        }

        private RectTransform Held(int at)
        {
            return at < 0 || at >= ports.Count ? null : ports[at];
        }

        /// <summary>Where a port is, in the coordinates the wires are drawn in --
        /// which are the content's, so a wire pans and scales with what it
        /// joins.</summary>
        private Vector2 Middle(RectTransform port)
        {
            Vector3[] corners = new Vector3[4];
            port.GetWorldCorners(corners);
            Vector3 middle = (corners[0] + corners[2]) * 0.5f;
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
            GameObject go = new GameObject("Wire");
            go.transform.SetParent(content, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            Image line = go.AddComponent<Image>();
            line.color = colour;
            line.raycastTarget = false;

            Vector2 span = to - from;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(span.magnitude, WireWidth);
            rect.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            // Behind the nodes: a wire crossing a node should pass under it.
            go.transform.SetAsFirstSibling();
            wires.Add(go);
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
        private void Born(int kind, int gate, Vector2 where, bool placed)
        {
            if (served == null)
            {
                return;
            }
            Vector2 at = placed ? where
                : new Vector2(30f + (Nodes % 5) * (NodeWidth + 30f),
                              20f + (Nodes / 5) * (NodeHeight + 20f));
            if (gate >= 0)
            {
                List<MapperType> touched = new List<MapperType>();
                int row = LogicTable.Add(served, touched);
                if (row < 0)
                {
                    return;                 // the block is full
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
                    Bindings.Bind(made.Emulate, KeyCode.None);
                    touched.Add(made.InputA);
                    touched.Add(made.InputB);
                    touched.Add(made.Emulate);
                }
                Board.Put(row, at);
                Commit(touched);
                Rebuilt();
                return;
            }
            Place place = new Place();
            place.Kind = kind;
            place.X = at.x;
            place.Y = at.y;
            Board.Places.Add(place);
            Kept();
            Redraw();
        }

        /// <summary>Deletes a node: a row goes from the table, an end goes from the
        /// layout.</summary>
        private void Kill(int index)
        {
            if (served == null)
            {
                return;
            }
            Place place = Placed(index);
            if (place != null)
            {
                List<MapperType> touched = new List<MapperType>();
                for (int i = 0; i < Rows; i++)
                {
                    Drops(i, place, touched);
                }
                Board.Places.Remove(place);
                Commit(touched);
                Redraw();
                return;
            }
            if (served.Count <= 1)
            {
                return;                     // a block keeps one row
            }
            List<MapperType> gone = new List<MapperType>();
            LogicTable.Remove(served, index, gone);
            Board.Forget(index);
            Commit(gone);
            Rebuilt();
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
            List<string> names = new List<string>();
            names.Add("INPUT");
            names.Add("OUTPUT");
            for (int g = 0; g < Gates.Count; g++)
            {
                names.Add(Gates.Names[g]);
            }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, screen, null, out local))
            {
                local = Vector2.zero;
            }
            // The sheet's own corner is its top left and a node's y counts down
            // from it, so the y is turned over here.
            Vector2 at = new Vector2(local.x, -local.y);
            Choices.Home(canvas.GetComponent<RectTransform>());
            Choices.OpenAt(screen, names, delegate(string picked)
            {
                int which = names.IndexOf(picked);
                if (which == 0)
                {
                    Born(Place.Input, -1, at, true);
                }
                else if (which == 1)
                {
                    Born(Place.Output, -1, at, true);
                }
                else if (which > 1)
                {
                    Born(-1, which - 2, at, true);
                }
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
            Apply(served.PrefixControl);
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
            return Feeding(to, 0) == from || Feeding(to, 1) == from;
        }

        private void Panning(Vector2 by)
        {
            if (content == null)
            {
                return;
            }
            content.anchoredPosition += by;
            Strings();
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
            float now = Mathf.Clamp(was * (wheel > 0f ? 1.1f : 1f / 1.1f), 0.4f, 2.5f);
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
            Strings();
        }

        /// <summary>
        /// Besiege's own undo, which is the one this window's edits are in: every
        /// change here goes through a mapper control and is filed with the machine
        /// the same way a slider dragged in the mapper is.
        /// </summary>
        private void Undo() { Step(true); }

        private void Redo() { Step(false); }

        private void Step(bool back)
        {
            try
            {
                Machine machine = Machine.Active();
                if (machine == null || machine.UndoSystem == null)
                {
                    return;
                }
                if (back)
                {
                    machine.UndoSystem.Undo();
                }
                else
                {
                    machine.UndoSystem.Redo();
                }
            }
            catch (Exception e)
            {
                Log.Warn("could not step the undo: " + e.Message);
            }
            // The rows have moved under us; Update notices and redraws.
            read = null;
            mark = null;
        }

        // ---- picking things out ----------------------------------------------

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
            Redraw();
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
            if (!done)
            {
                return;
            }
            band.SetActive(false);

            picked.Clear();
            for (int node = 0; node < Nodes && node < parts.Count; node++)
            {
                if (parts[node] == null)
                {
                    continue;
                }
                Vector3[] corners = new Vector3[4];
                (parts[node].transform as RectTransform).GetWorldCorners(corners);
                Vector2 low = sheet.InverseTransformPoint(corners[0]);
                Vector2 high = sheet.InverseTransformPoint(corners[2]);
                if (low.x <= box.xMax && high.x >= box.xMin
                    && low.y <= box.yMax && high.y >= box.yMin)
                {
                    picked.Add(node);
                }
            }
            Redraw();
        }

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
            List<string> was = new List<string>();
            List<string> now = new List<string>();
            // The same, for a copied gate that answered on a key rather than a
            // name: the wire between two copied gates is that key, and without
            // this the copy reads the gate it was copied from.
            List<KeyCode> wasKeys = new List<KeyCode>();
            List<string> nowKeys = new List<string>();

            // One entry per thing copied, in the same order, so the second pass can
            // find the row a copy became. Counting only the gates is what had a
            // pasted gate take its inputs from whatever entry happened to sit at
            // that index -- an end, usually, which is no row at all.
            int[] made = new int[clipboard.Count];
            for (int i = 0; i < made.Length; i++)
            {
                made[i] = -1;
            }

            for (int i = 0; i < clipboard.Count; i++)
            {
                Copied copy = clipboard[i];
                if (!copy.IsGate)
                {
                    // An end stands for a binding, so a second one on the same
                    // binding is the same end drawn twice. The copy is dropped and
                    // the pasted gates wire to the one already there.
                    if (Standing(copy.Kind, copy.Variable, copy.Key))
                    {
                        continue;
                    }
                    Place place = new Place();
                    place.Kind = copy.Kind;
                    place.Variable = copy.Variable;
                    place.Key = copy.Key;
                    place.X = corner.x + copy.At.x;
                    place.Y = corner.y + copy.At.y;
                    Board.Places.Add(place);
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
                string name = Spare(row);
                Bindings.BindVariable(fresh.Emulate, name);
                touched.Add(fresh.Emulate);
                if (copy.Wire != null)
                {
                    was.Add(copy.Wire);
                    now.Add(name);
                }
                else if (copy.Answers != KeyCode.None)
                {
                    wasKeys.Add(copy.Answers);
                    nowKeys.Add(name);
                }
                Board.Put(row, corner + copy.At);
                made[i] = row;
            }

            // Second pass: the inputs, once every new name is known.
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
                    int found = wanted == null ? -1 : was.IndexOf(wanted);
                    int onKey = wanted != null ? -1 : wasKeys.IndexOf(key);
                    if (found >= 0)
                    {
                        Bindings.BindVariable(input, now[found]);
                    }
                    else if (onKey >= 0 && key != KeyCode.None)
                    {
                        Bindings.BindVariable(input, nowKeys[onKey]);
                    }
                    else if (wanted != null)
                    {
                        Bindings.BindVariable(input, wanted);
                    }
                    else
                    {
                        Bindings.Bind(input, key);
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

        /// <summary>Whether an end on this binding is on the board already.
        /// </summary>
        private bool Standing(int kind, string variable, KeyCode key)
        {
            if (variable == null && key == KeyCode.None)
            {
                return false;       // an end with nothing on it is its own thing
            }
            for (int i = 0; i < Board.Places.Count; i++)
            {
                if (Board.Places[i].Kind == kind
                    && Board.Places[i].Same(variable, key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A wire name for a pasted row that nothing else answers to.
        ///
        /// The prefix and the row number is the usual one, and it collides after
        /// rows have been deleted and added again -- at which point the pasted gate
        /// and the older one are the same node to everything that reads the board,
        /// and only one of them is ever drawn.
        /// </summary>
        private string Spare(int row)
        {
            string wanted = served.Prefix + row;
            for (int tries = 0; tries < 64; tries++)
            {
                bool taken = false;
                for (int i = 0; i < Rows && !taken; i++)
                {
                    LogicRow other = Row(i);
                    if (other == null || !other.Ready || i == row)
                    {
                        continue;
                    }
                    taken = Carries(Bindings.IsVariable(other.Emulate)
                                    ? Bindings.Variable(other.Emulate) : null,
                                    wanted);
                }
                if (!taken)
                {
                    return wanted;
                }
                wanted = served.Prefix + row + "_" + (tries + 2);
            }
            return wanted;
        }

        /// <summary>The switches in the title bar.</summary>
        private string Styled()
        {
            return style == Straight ? "LINE" : (style == Curved ? "CURVE" : "SQUARE");
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
        private void Tidy()
        {
            if (served == null)
            {
                return;
            }
            int folded = Board.Fold();
            if (folded > 0)
            {
                Keep();
            }
            Log.Info("tidy: " + folded + " ends folded, "
                     + Board.Places.Count + " left");
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
                        int from = Feeding(node, port);
                        if (from >= 0 && depth[from] + 1 > deep)
                        {
                            deep = depth[from] + 1;
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
                columns[depth[node]].Add(node);
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

            for (int column = 0; column < columns.Count; column++)
            {
                for (int i = 0; i < columns[column].Count; i++)
                {
                    // Wide columns: a wire that has to pass a node passes between
                    // the columns rather than behind it.
                    Move(columns[column][i],
                         new Vector2(24f + column * (NodeWidth + 90f),
                                     20f + i * (NodeHeight + 26f)));
                }
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
            RawImage image = go.AddComponent<RawImage>();
            image.texture = Glyphs.Rounded;
            image.color = colour;
            UIF.Fit(go.GetComponent<RectTransform>(), x, y, w, h);
            return go;
        }

        /// <summary>Grows what it is given while the pointer is on the control,
        /// the way the tables' buttons do.</summary>
        private static void Grow(GameObject control, Transform grows)
        {
            if (control == null || grows == null)
            {
                return;
            }
            Swell swell = control.AddComponent<Swell>();
            swell.grows = grows;
            swell.grown = 1.12f;
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
            Text label = control.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                GameObject go = new GameObject("Text");
                go.transform.SetParent(control.transform, false);
                RectTransform rect = go.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                label = go.AddComponent<Text>();
            }
            else
            {
                RectTransform rect = label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            label.raycastTarget = false;
            UIF.Style(label, UIF.Ink, align);
            UIF.Shrink(label, 7);
            label.text = words;
            return label;
        }
    }
}
