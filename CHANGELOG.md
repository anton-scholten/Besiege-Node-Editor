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
  mapper, which keeps the block's activation key and its Automatic switch:
  activation, wait, duration, hold to run, allow stop, loop and the emulated key,
  a row at a time, with `+` at the bottom to add another.
- **A `#` column** numbering the rows by wait, so the timer that fires first is 1
  however the table is ordered.
- **Sorting** on any column, stable, with unbound rows kept at the bottom of a key
  column whichever way it is sorted.
- **Variables everywhere a key can go**, on the block's own activation and on both
  keys of every row, switched with Besiege's own key/variable bubbles borrowed off
  the mapper.
- **Tooltips** on every icon and heading, fading in over the control they explain
  the way the game's own do, which is where the `H`, `S` and `L` columns say their
  full names.
- **Convert to timer blocks**: one of Besiege's own timers per row, laid out on a
  horizontal plane and handed over as a selection through the game's own additive
  load, so one undo takes them all back.
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
