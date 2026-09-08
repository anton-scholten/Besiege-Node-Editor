# Besiege Timer Plus

<img src="TimerPlus/Resources/Thumbnail.png" alt="thumbnail" width="200" align="right">

One block that is up to thirty-two timer blocks, in
[Besiege](https://store.steampowered.com/app/346010/Besiege/).

A sequence of anything — a bomb run, a launch, a set of doors — is normally a row
of timer blocks on the machine and a row of separate mappers to set them in. This
is one block with a **table** in it. Each row is a whole timer — wait, duration,
hold to run, allow stop, loop, and the key it presses — started by the block's own
key, and a button at the bottom turns the table into that many real timer blocks
when you want them.

Every row is a reproduction of Besiege's own timer, read out of the game: same
phases, same 50 Hz tick, same rounding. A row fires on the tick a real timer
beside it would, and the block's display flashes red each time one does — full red
on the tick it fires, faded out a tenth of a second later — so a sequence can be
watched as well as timed, and rows firing one after another read as separate
flashes.

**[UI Factory](https://steamcommunity.com/sharedfiles/filedetails/?id=2913469777)**
(another Besiege mod which enables the nice UI, see workshop item `2913469777`) is
optional here. With it the block gets the table docked under the block mapper;
without it the rows appear in Besiege's ordinary mapper and everything still
works.

<br clear="right">

## Install

Either subscribe to the mod on Steam, or if you don't use Steam you can clone the repo then:

```sh
./tools/install.sh              # symlink into Besiege_Data/Mods
./tools/install.sh --copy       # copy instead
./tools/install.sh --uninstall
```

Set `BESIEGE_DIR` if your install isn't found automatically. Start Besiege, enable
**Timer Plus** in the mods menu, and the block appears in the toolbar — search
`timer`. No C# toolchain is needed; the build uses Besiege's own compiler.

## The table

Click the block. Besiege's own mapper holds the block's activation key and its
Automatic switch, the way any block's does, and the table is docked underneath.

```
  ┌ Besiege's mapper ─────────────────────────────┐
  │                 ACTIVATE (...)                │
  │                       B                       │
  │                   AUTOMATIC                   │
  └───────────────────────────────────────────────┘
  ┌ the table ────────────────────────────────────┐
  │   │ WAIT │ DURATION │ ↧ │ ⏸ │ ↻ │   EMULATE   │
  │ 1 │ 0.5  │   0.2    │ · │ · │ · │   (...)  a  │
  │ X │ 1.2  │    1     │ · │ · │ ● │  (x)  fire  │
  │ 3 │  3   │   0.5    │ ● │ · │ · │   (...)  b  │
  │ ───────────────────────────────────────────── │
  │                       +                       │
  │ ───────────────────────────────────────────── │
  │            CONVERT TO TIMER BLOCKS            │
  └───────────────────────────────────────────────┘
```

`(...)` and `(x)` are the two speech bubbles Besiege's own key selector uses; see
[Keys and variables](#keys-and-variables) below. The three switch columns are
headed with a picture apiece — `↧` `⏸` `↻` above stand in for them. Row 2 is drawn
with the pointer on its number, which is what deleting a row looks like.

**ACTIVATE**, in the mapper above, is the block's own key, and it starts every row
in the table. **Automatic** starts them with the simulation instead.

| Column | What it does |
| --- | --- |
| (the number) | Which timer this is, numbered by wait — 1 fires first, whatever order the rows are in. Point anywhere on the row and it turns into a red **X**: click to delete that row |
| **WAIT** | Seconds from the start to the press |
| **DURATION** | How long the press is held |
| ↧ | Hold to run: the activation becomes a switch rather than a trigger |
| ⏸ | Allow stop: a second press stops a run in progress |
| ↻ | Loop: start again as soon as a cycle ends |
| **EMULATE** | The key or variable this row presses |

Column headings say what they are on hover, fading in under the heading the way
the game's own tooltips do — which is where the three switch columns give their
names in words. Nothing else carries a tip: a table whose every cell explains
itself is a table you cannot read across.

**+** adds a row. It copies the one above and pushes the wait on by the same gap
as the last two, so a sequence being typed in carries on. A block keeps at least
one row, so the last one will not delete.

Ten rows are shown at once; past that the table scrolls, and adding or deleting a
row leaves the view where it was. **+** and **CONVERT TO TIMER BLOCKS** do not
scroll with the rows — they sit along the bottom of the window and stay there,
because a table long enough to scroll is exactly when they are wanted. Anything the
panel has to say — a block already holding all thirty-two rows, a convert that
failed — is said on the button it is about, in red, for a few seconds.

The table is drawn at the mapper's own width, so the two are one window with a
seam across it.

**WAIT** and **DURATION** are dragged as well as typed: press on the number and
pull sideways, and the value follows the pointer once the drag leaves the box —
a drag that stays inside selects text, the way any box does. Click for a caret,
double-click for the lot.

Sliders are clipped but out-of-range values can be typed.

| Setting | Slider | Typed |
| --- | --- | --- |
| Wait | 0–60 s | any |
| Duration | 0–60 s | any |

An event four minutes in is one row with `240`, exactly as it is one timer block
with `wait=240`.

## Keys and variables

The bubble beside a key cell is the same one Besiege's own key selector uses: a
bubble with **three dots** while the cell is on the keyboard, and clicking it hands
the cell to a variable; a bubble with a **cross** while a variable holds it, and
clicking takes it back.

In key mode, click the plate and press the key you want — a mouse button counts as
a key — or `Escape` to unbind it.

In variable mode the cell offers every variable name already in use anywhere on the
machine, eight at a time with a scrollbar past that, so wiring one block to another
is picking the name rather than spelling it. A name nobody has used yet is typed
straight into the box. Several names separated by `;` all work.

## Sorting

Click **WAIT** or **DURATION** to sort by it, and again to reverse; a triangle on
the heading says which way. The sort is
stable, so sorting by duration and then by wait gives wait order within duration,
and two rows that tie keep the order you put them in.

Those two are the only columns that sort. Three ticks in a column of thirty-two
rows are read off faster than a sort is clicked, and a table in keycode-name order
answers a question nobody asks.

## Converting

**CONVERT TO TIMER BLOCKS** builds one of Besiege's own timer blocks per row, lays
them out on a horizontal plane beside this block, and hands them over as a
selection with the move tool up. One undo takes them all back.

They are laid out in the table's own numbering — first to fire at the top left,
then along and down — so the field reads the way the table does, whatever order the
rows happen to be sitting in.

The Timer Plus block stays where it is, so you can convert again or keep editing.
The panel closes as the timers arrive, because they become the selection and that
closes the block mapper it is docked to.

Each timer is set exactly as its row was, and every one gets the block's own
activation key, since a timer standing on its own has nothing to follow. If the
block was on **Automatic**, they get Besiege's own `automatic` instead.

This is Besiege's own additive load — the same path the load screen's "add to
machine" button runs — so joints, undo and the selection tool behave exactly as
they do for a machine loaded from a file.

## Notes

Two of Besiege's own rules apply to a row of this block exactly as they do to a
timer block, and are worth knowing:

**Two rows pressing the same key at the same time raise one press.** Besiege
reference-counts an emulated key, so the second to arrive is not an edge and the
key does not come up until the last one lets go. Leave a gap — sixty milliseconds
is below what anyone notices and above a fixed step.

**A row cannot press a key that starts this same block.** Besiege refuses that for
its own timer too, and for the same reason.

A block holds thirty-two rows and no more. That is not a round number picked for
neatness — see [AGENTS.md](AGENTS.md).

In multiplayer the rows run on the machine's owner and the presses reach everyone
else the way any block's emulation does.

Runtime behaviour hasn't been fully confirmed yet — if something misbehaves, the
details land in `Player.log` and in the in-game console with `show_logs true`.

AI agent? see [AGENTS.md](AGENTS.md) for layout, build, and any relevant info.
[docs/MODDING-NOTES.md](docs/MODDING-NOTES.md) has what this mod had to work out
about Besiege's modding API — including borrowing the mapper's own icons and
docking a UI Factory window under it. The general notes, for a mod that is not
this one, are collected in
[Besiege-Modding-AI-notes](https://github.com/anton-scholten/Besiege-Modding-AI-notes).

## Credits

The block model is Creative Commons Attribution (CC-BY 3.0), from Poly Pizza:

| Block | Model | By |
| --- | --- | --- |
| Timer Plus | [Timer](https://poly.pizza/m/0J1_OKm87pR) | Poly by Google |

It is fetched and converted by `tools/make-block-mesh.py` rather than committed,
so the licence stays with its source.

## Licence

GPL-3.0. Besiege is Spiderling Studios'; nothing of theirs is redistributed here.
