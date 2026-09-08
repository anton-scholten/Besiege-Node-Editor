# Changelog

## 0.1.0

First version. One block, **Timer Plus**, holding a table of up to thirty-two
timers.

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
- **A number down the left** ranking the rows by wait, so the timer that fires
  first is 1 however the table is ordered. It has no heading; a column of small
  numbers beside a column of times has already said what it is. It is also the
  row's delete button: point anywhere on the row and it turns into a red X. That is a column of
  crosses' worth of width back, which is what lets the table sit under the mapper
  at the mapper's own width.
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

**Known and deliberate**

- **Thirty-two rows is a hard cap.** `MKey` is the only mapper type that carries a
  variable and a mapper control can only be registered in `SafeAwake`, so every
  row's keys are allocated up front. See [AGENTS.md](AGENTS.md).
- **Two rows pressing the same key at once raise one press.** Besiege
  reference-counts an emulated key; this is the game's behaviour and a real pair of
  timer blocks does the same.
- The stock mapper's row-count slider reads `1.00`–`32.00`. It is a slider because
  Besiege's mapper has no integer control.
