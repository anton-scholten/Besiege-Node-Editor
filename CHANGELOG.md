# Changelog

## 0.1.0

First version. Two blocks, each holding a table of up to thirty-two rows:
**Timer Plus** for Besiege's timers and **Logic Gate Plus** for its logic gates.

**Added**

- **The block.** Every row is a reproduction of Besiege's own `TimerBlock` state
  machine — same phases, same 50 Hz tick, same rounding — so a row fires on the
  tick a real timer beside it would. Rows run during a simulation; converting them
  is a convenience, not the point of the block.
- **The table**, drawn through UI Factory and docked under Besiege's own block
  mapper, which keeps the block's activation key and its Automatic switch: wait,
  duration, hold to run, allow stop, loop and the emulated key, a row at a time,
  with `+` to add another. Ten rows are shown at once and the rest scroll; `+` and
  the convert button are pinned along the bottom of the window rather than
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
- **Pin blocks**, a switch beside the convert button and on by default: each block
  conversion makes gets one of the game's pins inside it, with nothing bound and
  its visuals hidden, so a field of them stays where it was put.
- **Convert to timer blocks**: one of Besiege's own timers per row, laid out on a
  horizontal plane in the table's own numbering — first to fire at the top left,
  then along and down — and handed over as a selection through the game's own
  additive load, so one undo takes them all back.
- Without UI Factory the block still works: its controls appear in Besiege's own
  block mapper and the panel quietly never builds.

- **Logic Gate Plus.** The same table for Besiege's logic gate: two inputs, the
  gate, its one switch and the key it presses. Every one of the twelve gates is the
  game's own, read out of `LogicGate` and checked against its truth tables, its
  latches, its counter and its one-tick edge — so a converted machine runs the same
  as the one that made it. Input B is barred with diagonal stripes for a gate that
  reads one input, and the switch is barred and dead for a gate that has neither of
  the two it stands for; what is barred is still bound underneath. The switch wears
  **T** or **I** for which of the two it is. Convert builds real logic gate
  blocks.

- **A node editor** for the logic table, opened by a button between its pin switch
  and its convert button. A gate node is a row and a wire is a row's input carrying
  another row's answer name, so the board and the table are the same thing seen two
  ways -- neither can be out of step with the other, and the block runs and converts
  exactly as it did. Written rather than borrowed -- every node-editor library for Unity is an editor-time tool or wants
  its own assembly, and this mod compiles with the game's compiler against the
  game's assemblies. The palette is a row of drawn gate symbols, ports are circles
  that fill when something lands on them, wires come off by being pulled away, and
  a right-click on the board offers the same list where the pointer is. Nodes can
  be picked out by a click, a modifier-click or a dragged box, moved as a set and
  copied as one; **TIDY** lays the board out and folds ends standing for the same
  key or variable into one, and **GRID** -- on by default -- sits every node on the
  grid's intersections. The nodes are whole numbers of squares for it: an end is
  four cells across and two down, a gate three by two, and TIDY lays out in cells
  as well, so a board on the grid fills them rather than straddling them. The board is two zoomed-out views across and two down, and nodes and the
  view both stop at its edge.
  Every edit is one step of Besiege's own undo, carrying the whole block, so one
  press puts every gate back where it was with the wires it had -- the game's undo
  and no other, since an edit on the board and an edit in the table are the same
  edit. A gate removed, here or in the table, takes its own wires with it and comes
  back with them on that one press. A gesture the board cannot carry out says so in
  a word over the thing refused: a wire from an end straight into an output, a name
  a second end already stands for, a gate on a block whose thirty-two rows are
  used, a wire that would put a key and a name on one
  input, and one that would fill an input past the game's own three keys or hundred
  names -- an input takes as many wires as an answer does and Besiege ORs them, so a
  gate reading four answers is held while any of the four is up. A cell wired to
  several shows the count in the live colour and takes no typing; the board is where
  those wires are. **IMPORT** runs the conversion backwards: every
  one of Besiege's own logic gates on the machine is read into the block as a row
  and taken off the machine, wiring and all -- a wire is two gates agreeing on a
  key, so copying the gates copies the circuit -- up to the block's thirty-two
  rows, with anything over left on the machine for another block to take. One press
  of undo puts the blocks back and the rows away. Its two hotkeys are in Besiege's controls
  screen and default to 1+C and 1+V -- the game itself owns the Control versions
  of both.

**Known and deliberate**

- **Thirty-two rows is a hard cap.** `MKey` is the only mapper type that carries a
  variable and a mapper control can only be registered in `SafeAwake`, so every
  row's keys are allocated up front. See [AGENTS.md](AGENTS.md).
- **Two rows pressing the same key at once raise one press.** Besiege
  reference-counts an emulated key; this is the game's behaviour and a real pair of
  timer blocks does the same.
- **Logic Gate Plus does not burn out.** Besiege can burn a gate out for pulsing
  too fast -- five changes of output on consecutive ticks, and only for a gate
  whose answer can reach its own inputs -- and a row does not. Everything else
  about a row is the game's own gate, argument for argument: the state machine,
  the two passes it runs each tick, and the one fixed step a signal takes to cross
  a gate, so a chain of rows settles at the same rate a row of blocks would.
- The stock mapper's row-count slider reads `0.00`–`32.00`. It is a slider because
  Besiege's mapper has no integer control.
