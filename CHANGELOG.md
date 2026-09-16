# Changelog

## 0.1.0

First version, named **Node Editor** in Besiege's mods menu. Two blocks, each
holding a table of rows: **Timer Plus**, up to 1024 of Besiege's timers and starting
with one, and **Computer**, whose node editor the mod is named for, up to 1024 logic
gates and timers.

**Added**

- **The block.** Every row is a reproduction of Besiege's own `TimerBlock` state
  machine — same phases, same 50 Hz tick, same rounding — so a row fires on the
  tick a real timer beside it would. Rows run during a simulation; converting them
  is a convenience, not the point of the block.
- **The table**, drawn through UI Factory and docked under Besiege's own block
  mapper, which keeps the block's activation key and its Automatic switch: wait,
  duration, hold to run, allow stop, loop and the emulated key, a row at a time,
  with `+` to add another. Ten rows are shown at once and the rest scroll; `+` and
  the **IMPORT**, **PIN BLOCKS** and **EXPORT** buttons, three of a size, are pinned
  along the bottom of the window rather than
  scrolling with them. Every row is started by the block's own
  key — the mapper above the table is the one place activation is set.
- **The display flashes red** whenever a row fires — full on the tick, faded out a
  tenth of a second later — so what the block is doing can be seen from the machine
  rather than only heard in what it presses, and a burst of rows reads as a burst.
- **A number down the left** ranking the rows by wait, so the timer that fires
  first is 1 however the table is ordered. It has no heading; a column of small
  numbers beside a column of times has already said what it is. It is also the
  row's delete button: point anywhere on the row and it turns into a red X. That is a column of
  crosses' worth of width back, which is what lets the table sit under the mapper
  at the mapper's own width.
- **Wait and duration drag**, as the sibling SpecialEffects mod's value fields do:
  pull sideways off the box and the number follows the pointer; stay on it and the
  drag selects text.
- **Reaching up or down a column** selects every row between where the drag started
  and where it is, scrolling the list when it runs past the end of it. One number
  typed then goes to all of them, and one sideways drag moves all of them by the
  same amount.
- **Sorting** on wait and on duration, stable, so a second sort refines the first.
  Nothing else sorts: three ticks in a column are read off faster than a sort is
  clicked.
- **Variables everywhere a key can go**, on the block's own activation and on the
  key every row presses, switched with Besiege's own key/variable bubbles borrowed
  off the mapper. A cell on a variable offers the names already in use on the
  machine — read off its blocks' own keys, since nothing else knows them in the
  build area — eight at a time, with a scrollbar past that.
- **Tooltips** on the column headings, fading in under the heading they explain
  and shaped like the sibling Clippy mod's — capitals, sixteen point, air either
  side — which is where the three switch columns, headed with a picture apiece, say
  what they are in words.
- **Pin blocks**, a switch between import and export and on by default: each block
  export makes gets one of the game's pins inside it, with nothing bound and its
  visuals hidden, so a field of them stays where it was put.
- **Import**: every one of Besiege's timers on the machine as rows, taken off the
  machine in the same undo step. A block with no rows takes on the activation the
  timers share; where they differ the button says so.
- **Export**: one of Besiege's own timers per row, laid out on a
  horizontal plane in the table's own numbering — first to fire at the top left,
  then along and down — and handed over as a selection through the game's own
  additive load, so one undo takes them all back.
- Without UI Factory the block still works: its controls appear in Besiege's own
  block mapper and the panel quietly never builds.

- **Computer.** The same table for Besiege's logic gate: two inputs, the
  gate, its one switch and the key it presses. Every one of the twelve gates is the
  game's own, read out of `LogicGate` and checked against its truth tables, its
  latches, its counter and its one-tick edge — so a converted machine runs the same
  as the one that made it. Input B is barred with diagonal stripes for a gate that
  reads one input, and the switch is barred and dead for a gate that has neither of
  the two it stands for; what is barred is still bound underneath. The switch wears
  **T** or **I** for which of the two it is. **EXPORT**, on the node editor's
  title bar beside **PIN BLOCKS**, builds real logic gate blocks.

- **A node editor** for the logic table, opened by the button along the bottom of
  the table. Besides the gates it has a **timer**: a row run as Besiege's own timer
  block, started by its one input, with its wait and duration as numbers to type or
  drag and its hold, stop and loop switches on the node. The table shows it as a
  TIMER row, and EXPORT makes a real timer block of it. With nothing on its input
  it starts with the simulation, its wait counted from there, and is exported as a
  timer with Automatic on. A gate node is a row and a wire is a row's input carrying
  another row's answer name, so the board and the table are the same thing seen two
  ways -- neither can be out of step with the other, and the block runs and converts
  exactly as it did. Written rather than borrowed -- every node-editor library for Unity is an editor-time tool or wants
  its own assembly, and this mod compiles with the game's compiler against the
  game's assemblies. The palette is a row of drawn gate symbols, ports are circles
  that fill when something lands on them, wires come off by being pulled away -- a
  wire let go on a port is asked of that one port alone, so a second wire drawn into
  the input beside a wire's far end is added rather than moving the first, a wire
  let go on its own far end comes off, and only the port it left puts it back; an
  output end holding one wire is dragged from like one holding several, so a second
  wire can still be drawn out of it; and a wire from one gate into another carries a
  name only that gate answers to, so two gates on one output stay two wires -- and
  a right-click on the board offers the same list where the pointer is. Nodes can
  be picked out by a click, a modifier-click or a dragged box, moved as a set and
  copied as one; **TIDY** lays the board out and folds ends standing for the same
  key or variable into one, and **GRID** -- on by default -- sits every node on the
  grid's intersections. The nodes are whole numbers of squares for it: an end is
  five cells across and two down, as is a timer, a gate three by two, and TIDY lays out in cells
  as well, so a board on the grid fills them rather than straddling them. The board starts four zoomed-out views across and four down, the wheel pulls back
  far enough to see all of it, and nodes and the view both stop at its edge. How
  big it is, in grid squares, is **CANVAS SIZE** on the row **EDIT** puts out,
  saved with the layout: the nodes are centred in the size asked for, so the least
  a side goes is the box round them and a smaller one asked for is given that
  instead. Either number is typed or dragged sideways off its box as a timer's
  numbers are, a whole drag being one step of undo.
  **EDIT** lays three rows over the top of the board, which stays put: a colour
  under each palette button; under those, a row of how nodes are coloured and
  how wires are -- UNICOLOR, COLOR or
  RANDOM each, clicked for the next or right-clicked for the list, as the wire style
  is too -- with the unicolour node and wire colours beside them and **RESET
  COLORS**, always in the game's red, at the far end; and under that, from the left,
  **CANVAS SIZE** and its numbers, the wire style, **GRID** and **START EMPTY** --
  off to begin with, and while it is on a newly placed block arrives with nothing on
  its board rather than with the starter circuit -- with **RESET SIZE**
  -- red, and the board back to the size it started at -- at that row's far end:
  the things that are about the board rather than about one node, together on a row
  of their own. A board made bigger keeps the view on the middle of what is drawn,
  so the nodes do not slide off to one side as it grows. Each unicolour box sits
  beside the mode that uses it, unlabelled, and is out only while that mode is
  UNICOLOR; its room is kept either way. **TIDY** grows the board to hold what it
  lays out, up to the limit, and **IMPORT** with it, since it tidies what it
  brings in. **TIDY lays the logic out to run left to right.** A loop -- a latch,
  a counter fed by its own answer -- has to have one wire running back, and which
  one is now chosen by a walk forward from the ends that start things, rather than
  left for the levels to stumble into; every wire that is not closing a loop runs
  forwards. The order within a column is swept down and then up, so a node settles
  level with the middle of what feeds it and of what it feeds, which takes out
  crossings a single direction leaves in. One layout whatever the wires are drawn
  like: a board that rearranged itself when the wire style changed would be two
  boards to learn. Asking which nodes are wired together is now
  asked once rather than for every pair of nodes on every pass.
- **Square wires run in lanes.** No two are drawn down the same line: the cell a
  wire's middle leg falls in is divided into as many lanes as the wire's width
  leaves room for -- five at two thick -- and each wire takes the lane nearest its
  two ports' middle, or the first free one either side. Two wires off one answer
  never count against each other, having a point in common already, so a bundle
  crossing the same cells reads as parallel lines rather than as one thick one. A
  lane that would run behind a node is passed over, and lanes go out longest wire
  first, each trying the side its far end is on -- which cuts crossings rather than
  promising none. A board crowded enough to block every lane still draws the wire.
  Each colour is a hex box like the Special Effects spot light's.
- **A gate let go on another gate takes its place**, wires and all: what fed the old
  one feeds the new one, everything that read the old one reads the new one, and the
  old one goes, in one step of undo. The gate under the pointer lights a shade while
  a gate is over it. Off the palette or already on the board, either way; inputs map
  A to A and B to B, so a gate reading one input takes only A and a gate reading two,
  landing on one that reads one, fills only A. Ends and comments neither land on a
  node nor are landed on, a node is never dropped on itself, and a gate over one of
  its own kind neither lights nor swaps: the board would be left exactly as it is.
  Colours are kept per player, in the mod's data folder, and so are the board's
  three switches: the wire style, **GRID** and **START EMPTY** stand between
  sessions. RESET COLORS puts the colours back and leaves the switches alone. The board's edge stays the
  same thickness on screen at any zoom, and a comment is resized by the double
  arrow in its corner, which scales its writing up to three quarters of the board's
  width and keeps it on the board however big it is; that arrow stays one size on screen at any zoom.
  Every edit is one step of Besiege's own undo, carrying the whole block, so one
  press puts every gate back where it was with the wires it had -- the game's undo
  and no other, since an edit on the board and an edit in the table are the same
  edit. A gate removed, here or in the table, takes its own wires with it and comes
  back with them on that one press. A gesture the board cannot carry out says so in
  a word over the thing refused: a wire from an end straight into an output, a name
  a second end already stands for, a gate on a block whose rows are all
  used, a wire that would put a key and a name on one
  input, and one that would fill an input past five keys or the game's own hundred
  names -- an input takes as many wires as an answer does and Besiege ORs them, so a
  gate reading four answers is held while any of the four is up, and a gate or timer
  presses as many outputs as it is wired to, under the same caps. News about the
  board as a whole, an import or a full block, sits in the board's top-left corner
  instead, for four seconds or six for an import. A cell wired to
  several shows the count in the live colour and takes no typing; the board is where
  those wires are. **IMPORT** runs the conversion backwards: every
  one of Besiege's own logic gates and timers on the machine -- an Automatic timer as a
  timer row with nothing on its input -- is read into the block as a row
  and taken off the machine, wiring and all -- a wire is two gates agreeing on a
  key, so copying the gates copies the circuit -- up to the block's
  1024 rows, with anything over left on the machine for another block to take. One press
  of undo puts the blocks back and the rows away. Its three hotkeys -- copy, paste and select
  all -- are in Besiege's controls screen and default to 1+C, 1+V and 1+A -- the
  game itself owns the Control versions.
- **The board's window moves by its thin border** as well as its title bar.
  **EXPORT** closes the board. A board left up by **IMPORT** closes when another
  block's menu opens, unless pinned. **Besiege's own wheel zoom no longer sticks
  off.** A window holds the camera's zoom while the pointer is over it, and gives
  it back on the pointer leaving or the object going off; but hiding the game's
  interface switches the *canvas* off, which leaves the objects active -- so no
  such event ever arrives, and the hold outlived every route that returns it.
  `ZoomGuard` now gives it back the moment it cannot be given back by hand.
  **The middle button no longer passes through the board.** A middle click that
  never moves sends no drag events at all, so nothing raised the menu count and
  Besiege panned its camera under the window; the press itself now takes the hold,
  and the window's own bar, rows and margins listen for it as the board already
  did. **A board closed while a cell was listening for a key** left that cell's
  menu count standing -- and the game reads the wheel below its own in-menu
  check, so the camera stopped zooming; closing now gives the cell back first,
  and a cell whose canvas goes out from under it gives itself back.
- **A barred input takes no key and no name.** The input a gate does not read is
  drawn over with diagonal lines, and the cell underneath went on taking clicks
  through them: its bubble, its plate and its name box are all dead now, as the
  barred switch beside them already was. What it holds is still held, and comes
  back when a gate that reads it is chosen. The default AND and counter answer `var_1` and
  `var_2` for real: their names were never applied and fell back to `C`, so the two
  shared a key and a wire out of either was refused. A row added with `+` no longer
  copies the output of the row above. Every wire gesture follows one rule -- the port
  a wire ends on decides, for a drop and a second click alike: a gate input holding
  one wire no longer hands it over, which moved the wire instead of adding one, and
  a click can start from an input and can take a wire off. A wire that would
  put a key and a variable on one gate's answer -- a gate on keys into a named output,
  a gate carrying names into a key output, or a wire out of a gate on a shared key --
  is refused with *Can't mix key and variable. Use an OR gate.*, for six seconds. An
  emptied output goes blank -- its wires kept on a hidden name -- instead of keeping
  its old binding and refusing gates, and a new output off a gate already feeding
  another starts blank. An input and an output may share a key or variable.
  Adding or removing a node on a board of hundreds no longer draws every node after
  it again, nor rebuilds the table's window, and a gate keeps its colour when one
  above it goes. A node nobody dropped -- the table's `+`, or a palette button
  clicked rather than dragged -- lands in the middle of the board's view rather
  than in a column off its edge, one grid cell right and down for each node
  already standing there, so a handful added in a row staggers instead of
  stacking.

- **The mod speaks every language Besiege does** -- French, German, Spanish,
  Italian, Portuguese, Polish, Russian, Turkish, Japanese, Korean and the three
  Chinese variants -- and follows the language chosen in Besiege's own options.
  Every word it shows is a key in `lang/example.txt`; a translation is that file
  under the name Besiege uses for the language (`French.txt`, `ChineseSimplified.txt`
  -- the same names as the game's own `Localisation Files`), beside the mod.
  **Changing language takes effect where you stand**, with nothing to restart: the
  mod listens for the game's own language change, reads its words again, and makes
  the table and every open board over in them -- the table keeping its scroll, a
  board its nodes, zoom and window position -- while the mapper's own controls are
  renamed in place. The same name in the mod's data folder
  overrides one that shipped, so a player can retranslate without touching the
  install. A key left out stays English, so a part-finished translation still
  works. Numbers in a message are `{0}` rather than glued on, so a translation can
  put them where its own grammar wants them. The gate names are not in the file:
  they come from Besiege's own translations already. Nor is anything written into a
  save, or a machine saved in one language would not load in another.
- **The words are drawn in a font that can draw them.** Besiege's interface font
  carries Latin and Cyrillic but no CJK at all, so the mod asks the game which font
  to use (`LocalisationManager.GetFont`) rather than deciding for itself, and
  Japanese, Korean and Chinese come out in Besiege's own CJK lettering instead of
  empty boxes. Every label in the mod takes its font from that one place, and a
  language change re-runs it. The French for *nodes* is spelled `NOEUDS`, as
  Besiege's own French spells it, rather than `NŒUDS`.

**Known and deliberate**

- **Both tables' rows are the block's own data, not mapper controls.** Each block
  keeps its rows as one text control, a run makes its keys -- Timer Plus's to press,
  Computer's to press and to listen, filed with the machine's key controller by
  hand -- and each panel draws only the rows in view, so 1024 rows cost what one
  does. Without UI Factory neither table's rows can be edited. See
  [AGENTS.md](AGENTS.md).
- **Two rows pressing the same key at once raise one press.** Besiege
  reference-counts an emulated key; this is the game's behaviour and a real pair of
  timer blocks does the same.
- **Computer does not burn out.** Besiege can burn a gate out for pulsing
  too fast -- five changes of output on consecutive ticks, and only for a gate
  whose answer can reach its own inputs -- and a row does not. Everything else
  about a row is the game's own gate, argument for argument: the state machine,
  the two passes it runs each tick, and the one fixed step a signal takes to cross
  a gate, so a chain of rows settles at the same rate a row of blocks would.
