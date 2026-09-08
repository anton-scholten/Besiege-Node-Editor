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

**Known and deliberate**

- **Thirty-two rows is a hard cap.** `MKey` is the only mapper type that carries a
  variable and a mapper control can only be registered in `SafeAwake`, so every
  row's keys are allocated up front. See [AGENTS.md](AGENTS.md).
- **Two rows pressing the same key at once raise one press.** Besiege
  reference-counts an emulated key; this is the game's behaviour and a real pair of
  timer blocks does the same.
- **Logic Gate Plus does not burn out.** Besiege can burn a gate out for pulsing
  too fast; a row of a table is not a thing on the machine that can be broken, and
  thirty-two rows sharing one block's burnout would be a rule nobody could see the
  shape of.
- The stock mapper's row-count slider reads `1.00`–`32.00`. It is a slider because
  Besiege's mapper has no integer control.
