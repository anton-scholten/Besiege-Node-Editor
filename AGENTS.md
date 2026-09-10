# Working on this repository

Notes for anyone — human or AI — changing this mod. The [README](README.md) is for
people who just want to use it; nothing here needs repeating there.

What this mod established about Besiege that was not already written down is in
[docs/MODDING-NOTES.md](docs/MODDING-NOTES.md).

## Layout

The folder Besiege loads is `TimerPlus/`, because that subfolder is the whole of
what gets uploaded to the Workshop. Everything beside it is not part of the mod.

```
TimerPlus/Mod.xml                    manifest: assembly, resources, block list
TimerPlus/TimerPlus.xml              the block: mesh, colliders, module, icon
TimerPlus/TimerPlus.dll              built by tools/build.sh (checked in, the game loads it)
TimerPlus/Resources/                 the mesh, its texture, the thumbnail, the UI icons
TimerPlus/TimerPlusScripts/*.cs      mod source; not read by the game
tools/build.sh                       compiles with Besiege's own compiler, and checks
tools/verify-build.sh                the check to run after editing any .cs
tools/install.sh                     builds and installs into the game
tools/make-block-mesh.py             fetches and converts the block model
tools/make-ui-icons.py               trims and scales the table's switch icons
tools/icons/                         the artwork it reads (not loaded by the game)
tools/tests/                         the checks the build runs (XML, blacklist, timer + table)
docs/                                notes; not loaded by anything
```

`TimerPlus/TimerPlus.dll` is committed on purpose. `Mod.xml` names it as an
`<Assembly>`, so a checkout has to carry a built one or the mod does not load.
`TimerPlusScripts/` sits inside `TimerPlus/` so the sources travel with the mod
folder; Besiege reads only what `Mod.xml` names, and `install.sh --copy` strips
them out of the copy it makes.

## Build

```sh
./tools/verify-build.sh          # compile check; leaves the shipped assembly alone
./tools/build.sh                 # compile, run every check, install into TimerPlus/
./tools/install.sh               # build, then symlink into Besiege_Data/Mods
./tools/install.sh --no-build    # symlink only
./tools/install.sh --uninstall
./tools/make-block-mesh.py --preview   # rebuild the block mesh and texture, render previews
python3 tools/make-ui-icons.py         # rebuild the H/S/L heading textures from tools/icons
```

No .NET toolchain: the build drives Besiege's own `mcs.dll` through `libmono.so`.
`BESIEGE_DIR` and `UIFACTORY_DIR` override the auto-detection, which otherwise
walks Steam's `libraryfolders.vdf`. UI Factory is needed to *compile* even though
it is optional at runtime — the panel's source names its types.

`build.sh` is also the whole test suite, and each of its three checks has caught
something real:

1. **`XmlCheck`** over the block XML plus `TimerPlusModule.cs` and
   `make-block-mesh.py` — the elements Besiege insists on, mesh and texture names
   against `Mod.xml`, the module's required attributes against the
   `[DefaultValue]` markers in the source, and the block's `<Icon><Rotation>`
   against the mesh tool's `ICON`. Besiege reports none of these: the block is
   simply absent from the toolbar.
2. **`BlacklistCheck`** over the built assembly — the loader's namespace blacklist
   and its P/Invoke test, so a refusal arrives here rather than as a mod that
   silently does not load.
3. **`TableCheck`** — the offline unit tests, below.

There is no way to run one of those tests alone; `TableCheck.Main` calls each in
turn and they take milliseconds. To add one, write a `static void` and call it at
the top of `Main`.

The assembly is built into a scratch *directory* rather than under a scratch
*name*: an assembly is identified by its name once loaded, so building to
`TimerPlus.<pid>.dll` would make it impossible for the table check to reference.

Iterating means restarting Besiege — mods are read once at startup. The symlink
means a rebuild needs no reinstall.

## The shape of it

One block, and the design is dictated by one Besiege constraint (the next section).
What depends on what:

- **`TimerPlusBehaviour`** owns the controls and the run loop. It reads activation
  through `KeyReader` (latch in `KeyEmulationUpdate`, consume in
  `SimulateUpdateAlways`), advances each row through `Clock` in
  `SendKeyEmulationUpdateHost` at 50 Hz, and is the only thing that calls
  `EmulateKeys`. It reconciles `row.Wants` against `row.Held` rather than letting
  the clock press anything.
- **`ValueField`** is the transparent sheet over a number box that makes it
  draggable, taken from the sibling SpecialEffects mod. It has to be a sheet and it
  has to handle the pointer-down as well -- both reasons are written out at the top
  of the file, and both cost a day somewhere else. Which edge the drag leaves by
  settles what it is: the sides are a value, the top and bottom are a reach down
  the column, and `Panel.Pick`/`Reach` turn that into a selection and scroll the
  list under it.
- **`Glow`** blinks the block's display when a row fires. The mesh is unwrapped
  onto a palette of flat patches, one per colour of the model, so the display's
  triangles all look at one patch: the block gets its own material and its own
  2x2 point-sampled texture, and that one texel is repainted. `Dress` re-checks
  the renderer rather than remembering it, because Besiege builds the visual after
  `SafeAwake` and a repaint replaces the material outright. The colours are held
  to the palette `tools/make-block-mesh.py` writes -- run it and it says so.
- **`Row`** is one row's controls plus its phase; **`Clock`** is the phase machine.
- **`LogicGatePlusBehaviour`**, **`LogicRow`**, **`Gates`**, **`LogicTable`** are the
  same four things for the second block. `Gates` is Besiege's `LogicGate` read out
  with `peek.sh`: `Advance` is its `UpdateState` (the latches, the counter, the
  toggled inputs) and `Answer` is its `EvaluateEmulation`. Both take the gate and
  the row's switch as arguments rather than reading the mapper, so the build can
  check them without a live block -- `tools/tests/TableCheck.cs` holds every gate
  to the game's own truth table, and to the arguments the game hands it
  (`Gates.Holding`, `Gates.Letting`). The block has no activation key: a gate has
  inputs, not a start.
  **Each row emulates against its own two input keys and no others.**
  `KeyInputController.Emulate` skips every key handed to `EmulateKeys` -- that is
  how the game stops a gate driving itself -- so passing the whole block's inputs
  told it to skip them all, and no row could drive another row in the same block:
  every wire the board drew between two of its own gates was dead in a simulation
  and nowhere else. See `ownKeys` in `LogicGatePlusBehaviour`, and
  notes/03-keys-and-automation.md.
- **A port holds a list, not a wire.** A gate's input answers to as many names (or
  keycodes) as are wired to it and Besiege ORs them -- `MKey.IsHeld` returns on the
  first code held, and the names share the key's emulation count -- so `Asked` reads
  what a port answers to, `Feeds` turns that into the nodes behind it, and `Feeding`
  is only "the first of them, if any". Adding a wire is `Bindings.Added` and taking
  one off is `Bindings.Dropped`; nothing rebinds a whole input any more, or the
  other wires on it would go with it. The game's caps are `Bindings.MostKeys` (3)
  and `Bindings.MostNames` (100), and a key answers to codes or to names and never
  both -- both refusals are said over the port they were dropped on.
- **The board's graph is derived, and indexed for the length of one operation.**
  `Feeding` answers "what feeds this port" by asking every node what it answers to
  and comparing `;`-joined name lists -- a string joined and split per comparison.
  Fine once; TIDY does it for every node against every other, four times over, so
  it was cubic and allocated in the millions on a full table. `Sourced`/`Unsourced`
  put a name-to-node index up for the length of `Wired` and of a tidy, and
  `Feeding`/`Presses` consult it when it is up. Nothing between those two calls
  writes a binding, so it cannot go stale, and anything asking outside them takes
  the original loop -- which is still there, and is the definition of the answer.
- **`Table`** lifts rows out of their controls as `RowData` so they can be sorted
  and deleted — a row *is* its controls, and reordering rows means moving values
  between them.
- **`Editor`** and **`Wiring`** are the node editor: the logic table's own rows,
  drawn as a circuit. There may be up to four `Editor` instances -- one is made at
  load and the rest on demand, because a board somebody has pinned is theirs and
  opening another block must not take it away. The statics that used to speak for
  the single instance (`Open`, `Showing`, `Toggle`, `Dropped`) work over the list:
  an unpinned board is lent to whichever block is opened, a pinned one is left
  alone, and past four the newest gives way. **There is no graph stored anywhere** -- a gate node is a row,
  and a wire is a row's input bound to the variable another row's answer goes out
  under, so the board is derived from the table on every redraw and an edit on
  either side is an edit to the same thing. A gate's answer is a **generated name**
  (`ne01_04`) given to it the moment the row exists; nothing on the board types it
  and nothing shows it. Keys and variables belong to the two ends -- which is why a
  wire out of an *input* end carries that end's own binding rather than a name of
  its own: nothing on a machine raises a variable except a block emulating a key,
  and an end is not a block. `Wiring` holds only what the table has
  nowhere to put: where each row sits, and the input and output nodes, saved as
  text in the block's `LayoutKey`. That text is the whole of the layout format, so
  the value of a line is everything to the end of it -- a name with a space in it
  read as one word bred a duplicate end on every load. An edit is applied with
  `MapperType.ApplyValue` and filed as **one** step of Besiege's undo with
  `UndoSystem.EditBlock`, which carries the block's entire state either side: one
  press puts every row, binding and position back. `BlockMapper.OnEditField` is
  kept for the multiplayer path only -- it files a step per control, and a gesture
  here touches several. `BlockInfo.FromBlockBehaviour` hands back the block's last
  saved state, so `OnSave` runs before each snapshot or the pair is stale. The
  The board's two hotkeys -- copy and paste, and nothing else: undo is Besiege's
  own -- are declared in `Mod.xml` under `<Keys>` and read through
  `Modding.ModKeys`, which is what puts them in Besiege's own controls screen;
  their modifier is the 1 key, because the game's own copy and paste are on
  Control and it hears the keyboard at the same time this does.
  The gate names and their order are Besiege's own: `LogicGate.Awake` builds its
  menu from twelve translation ids and casts the menu's value straight to
  `GateType`, so index *is* the enum, and `Gates.Names` looks those ids up through
  `LocalisationManager.GetTranslation`. The gate symbols and the port circles are
  drawn in `Glyphs`, like the arrow and the bars. The editor's window is laid out
  by `Arrange` from one `size` field, so the corners can resize it; the nodes and
  wires hang on a `content` rect that is panned and scaled, and the board itself
  stays put and clips.
- **`Panel`** serves *both* table blocks: `served` or `logic` is set, never both, and
  only the columns differ -- the window, docking, frame, strip, scrollbar, curtain
  and tooltips are shared. `StandOff` switches the panel's `GraphicRaycaster` off
  for the length of any drag that did not begin on the window: Besiege drags its
  own mapper by the mouse and gives up the moment the pointer is over another
  interface, so a window dragged down over this panel used to stop dead. It builds two things: the rows, in the Window prefab's own ScrollRect,
  and a fixed strip along the bottom -- rule, `+`, convert, message line -- parented
  to the window with the viewport held clear of it by `StripHeight`. The strip has
  no plate: what keeps the rows out from under it is `Curtain`, which measures each
  row against the **window's** own rect through `GetWorldCorners` +
  `InverseTransformPoint` and switches off anything not wholly above the strip. Not
  against the ScrollRect's viewport: that may be null, may be named anything, and
  may be the ScrollRect's own rect, and each of those let rows draw over the strip.
  The `RectMask2D` on the viewport is a second line and not the one to rely on.
  The scrolling frame is the panel's own too -- `Clip` makes a `Frame` object the
  size of the window less the strip and the bar's gutter, moves the prefab's
  content into it and points `ScrollRect.viewport` at it. Asking the prefab where
  its viewport was failed three ways (null, differently named, the ScrollRect's own
  rect), and each left the ScrollRect believing its frame was the whole window: rows
  over the strip, last rows unreachable, and a bar that never hid itself. Same for
  the bar: built in `Rail`, stopping where the list stops. There is no message
  line: `Flash` puts a refusal or a result on the button it is about, for four
  seconds, and puts the button's own word back afterwards. The strip has no plate of
  its own: a plate there is the window's own colour laid down twice, and reads as a
  dark band. `LateUpdate` follows `StatMaster.hudHidden`, so Tab takes the panel
  with the rest of the interface -- the **canvas** is disabled, never the window,
  because switching the window off is the path that hands every control back to the
  stock mapper. It is one UI Factory window, on a `DontDestroyOnLoad` object,
  watching
  `BlockMapper.onMapperOpen`. It holds the *behaviour*, never a control, and reads
  the controls fresh on every fill: two blocks of the same kind do not share their
  mapper controls, and a panel that captured one would show the first block's
  values and write to it.
- **`Conversion`** and **`Drop`** build `BlockInfo`s and add them through Besiege's
  own additive-load path, in wait order rather than row order. With the block's pin
  switch on, each one gets a `BlockType.Pin` at the same position, `bmt-hide-visual`
  true and `bmt-unpin` bound to nothing -- Besiege rebuilds joints from where blocks
  are, so a pin at the block's own position is a pin inside it.
- **`Conversion.From`** is the same thing backwards, for the board's IMPORT button:
  the machine's own logic gates read into rows and then taken off the machine. A
  gate's settings are read off its **live mapper controls** -- `LogicGate`'s own
  fields are private, but `SaveableDataHolder.MapperTypes` is public and every
  control in it carries the name its block registered, which is the way in that
  needs no reflection. Mind that a live `MapperType.Key` is the **bare** name
  (`activate-A`): `bmt-` is `MapperType.XDATA_PREFIX` and goes on only in a save,
  so `Conversion.Control` matches either spelling -- asking with the save's alone
  matched nothing and read every gate on the machine as unreadable. The blocks go through `BlockSelectionTool.RemoveBlocks`,
  which hands its undo actions back rather than filing them, so the removal and the
  block's own edit (`LogicTable.Edited`) are filed together as one step. Wiring
  needs no work: a wire is two blocks bound to one key, and copying both blocks
  copies the wire.
- **`Variables`** lists the names in use on the machine by walking
  `Machine.BuildingBlocks` and reading every `MKey.message`. Not
  `KeyInputController.usedMessages`: that is filled by `Machine.InitSimBlock` at
  the start of a run and is empty in the build area, which is where the list is
  wanted. **`Choices`** is the menu it is shown in -- one list on the canvas, like
  the tooltip and for the same clipping reason, with a `RectMask2D` because a
  stencil `Mask` on the same canvas as the window's own cuts holes in it, and a
  hand-built scrollbar past eight names (there is no prefab bar outside a UI
  Factory window). `UnityEngine.UI.Scrollbar` is spelled out in full there:
  Besiege has a `Scrollbar` of its own in the global namespace, the same collision
  that cost `Keys` and `Convert` their names.
- **`UIF`** is the only file that names `Besiege.UI` — see the soft-dependency rule
  below.
- **`MapperArt`** borrows the game's own key/variable bubble icons off a live
  `Selectors.KeySelector`. `Selectors` is not blacklisted; `InternalModding` is.
- **`RowNumber`** is the number down the left of a row, which turns into a red X
  while the pointer is anywhere on that row -- a transparent plate on the row frame
  reports it, since uGUI only tells a Graphic about a pointer -- and deletes the
  row when clicked. It replaced a column of
  crosses down the right; the width that freed is what lets the panel sit at the
  mapper's own width.
- **`Glyphs`** loads the three switch-column headings — hold, stop, loop — through
  `ModResource.GetTexture`, by the names `Mod.xml` declares, and draws the sort
  mark itself: one triangle in a generated texture, turned over for a descending
  sort, because a `^` and a `v` are two letters of different weights rather than
  one mark two ways up. The rounded plate behind a row's delete X is drawn there
  too. Every other picture the mod draws with -- the twelve gate symbols, the port
  circles, the plate, the grid, the dashes, the stripes, the resize cursors -- is
  **generated by `tools/make-glyphs.py` and shipped as a PNG**. They were drawn at
  runtime, a pixel at a time with sixteen samples in each, and the twenty of them
  together were a stall on the first block opened. Change a shape in that script
  and run it; `Mod.xml` declares the files and `Glyphs` loads them by name. They are drawn into a
  `RawImage`, which takes the `Texture` the resource system hands over as it is; an
  `Image` would want a `Sprite` made from it and owned by somebody. Missing
  textures fall back to the letters `H`, `S`, `L`.
- **`Tip`** is one tooltip panel on the canvas, moved to whatever is hovered,
  rather than UI Factory's per-control `Besiege.UI.Bridge.Tooltip` — the table
  scrolls and hides whole rows, which a panel parented into a row cannot survive.
  **`Glide`** is the one part of that handler worth keeping: it fades the shared
  panel up and drifts it into place, so a pointer crossing a row of icons does not
  flash a hard-edged box on and off at each one. The panel is made the last child
  of the canvas on every showing, because the window is respawned on every rebuild
  and uGUI draws siblings in order. Its shape — capitals, sixteen point, sixteen
  units of padding, the point resting on the control's edge, below where there is
  room — is the sibling Clippy mod's, so the two mods' tips read as one interface.

## Hard rules

**Never change `<ID>` in `Mod.xml`.** The game generates it on first load and
saved machines refer to this block by it. The same goes for `<ID>1</ID>` in
`TimerPlus.xml`, and for the module name `TimerPlus`, which has to be spelled the
same in the `[XmlRoot]` on `TimerPlusModule`, in the `AddBlockModule` call in
`Mod.OnLoad`, and in the `<TimerPlus>` element inside `<Modules>` in the block XML.

**The block's `Activate` key and `Automatic` switch stay in Besiege's own
mapper.** `ShowStock` never touches their `DisplayInMapper`. Besiege's key
selector is the only thing that can bind a key properly — the variable picker, the
ignore switch, the multi-key list — and the panel is docked under it rather than
replacing it. The sibling Orchestra mod's instrument blocks are laid out the same
way; `InstrumentBehaviour.ShowInMapper` says so in a comment at the bottom.

**Do not rename a mapper key.** `"Activate"`, `"AutomaticKey"`, `"RowsKey"`, and
the per-row `"Emu<n>"`, `"Wait<n>"`, `"Dur<n>"`, `"Hold<n>"`, `"Stop<n>"`,
`"Loop<n>"` are what a saved machine stores its settings under.
Renaming one silently resets that setting on every existing machine. The display
names beside them are only labels and are free to change.

**Do not rename the logic block's mapper keys either** -- `"A<n>"`, `"B<n>"`,
`"Gate<n>"`, `"Mode<n>"`, `"Out<n>"`, or either block's `"PinKey"` -- or change `<ID>2</ID>` in
`LogicGatePlus.xml`, for the same reasons.

**Do not lower `TimerPlusBehaviour.MaxRows`.** Raising it is safe. Lowering it
orphans the controls of every row above the new cap, and a machine saved with
those rows loses them with no error anywhere.

**Do not change the `bmt-` names in `Conversion.cs`.** They are Besiege's own, read
out of `TimerBlock.Awake`, and a typo produces a timer that loads with that
setting at its default rather than one that fails.

## Why the rows are a fixed number

This is the constraint the whole design is built round, so it is worth stating
plainly before anyone tries to make the table grow.

`MKey` is the **only** mapper type that carries a variable. `MSlider`, `MToggle`,
`MMenu` and `MValue` have nothing variable-related on them at all — no message, no
emulation, no selector. So a row that can press a variable has to *be* an `MKey`,
and a mapper control can only be registered in `SafeAwake`. There is no API for
adding one to a block that already exists.

Hence: every row's six controls are built for every block, whether the row is in
use or not, and the row count is a control of its own that says how many of them
mean anything. An unused row sits at its defaults, and Besiege leaves a
default-valued control out of the save when `ExcludeDefaultSaveData` is on, so
thirty-two rows cost nothing in a machine that uses three.

The alternative — keeping the rows in one `MText` and doing the key registration
by hand — does not work: `Machine.InitSimBlock` files a block's keys with
`KeyInputController` by walking `MapperTypes`, so a key that is not one is never
registered and hears nothing.

## Where the behaviour came from

`Clock.cs` is `TimerBlock`'s state machine, read out of `Assembly-CSharp` with
`peek.sh dump TimerBlock`. Anything that changes it has to keep agreeing with the
real block, because the whole point of the convert button is that the two run the
same. The numbers, for checking against a future build:

| | |
| --- | --- |
| `BlockType.Timer` | 66 |
| mapper keys | `activate`, `emulate`, `automatic`, `hold-to-activate`, `can-stop`, `loop`, `wait`, `emulation-time` |
| default keys | activate `B` (98), emulate `C` (99) |
| both sliders | `AddSliderUnclamped`, default 1, 0..60, suffix `s`, onlyPositive |
| tick rate | 50 a second — `Machine.FixedUpdate` runs the emulation pass on every *other* 100 Hz step |
| `TimeToFrameCount` | `max(1, ceil(seconds * 50))` |

Two deliberate differences from the original, both commented where they happen:

- **No prephase.** The vanilla timer has a fourth phase that waits out the network
  ping before starting, for a client without physics. `ModBlockBehaviourHandler`
  gates a modded block's emulation hook on `SimPhysics`, so it never runs on such
  a client and there is nothing for that phase to do.
- **Activation is latched, not double-tested.** Vanilla calls `CheckKeys` from
  both the frame update and the emulation update, each with its own press flag.
  This reads the edges once per emulation tick in `KeyEmulationUpdate` and
  consumes them once per frame — the split the modding notes settled, and what
  keeps a variable press from landing two or three times at a high frame rate.

## What can and cannot be checked offline

Almost nothing here runs outside the game. Three things deliberately do, and they
are the ones whose failure is *silent* rather than loud:

- **`Clock`** is the timer state machine, kept free of Unity objects. A row that
  fires a tick late, or loses one every time it loops, does not throw and does not
  log — it drifts away from the real timer beside it on the same machine, which is
  the one thing this block must not do.
- **`Table.Compare`** is public for the same reason: a sort that scrambles its ties
  looks like a mod that lost your settings.
- **`Conversion.Spell`** likewise: a key written into a generated block in the wrong
  format gives a timer that loads with the wrong binding rather than one that fails.

`tools/tests/TableCheck.cs` runs all three against the tick counts above. It is
worth confirming a change to it can still fail: setting `Clock.Tick`'s loop bound
to 1 reproduces the lost-tick bug and five checks go red.

Everything else — the panel, the block lifecycle, the additive load — needs Besiege
running. Verify those by installing and launching, and read the log:

```sh
grep -a 'TimerPlus\]\|Mods\]' ~/.config/unity3d/Spiderling\ Games/Besiege/Player.log
```

`-a` matters: the log picks up bytes that make grep treat it as binary. Grep for
the `[Mods]` tag rather than the mod's name — the loader's own messages name the
file and the element and never the mod.

## Reading the game

Everything above was read with the tools in the sibling
[Besiege-Modding-AI-notes](https://github.com/anton-scholten/Besiege-Modding-AI-notes)
repository, which needs nothing installed:

```sh
./tools/peek.sh sig TimerBlock            # fields, properties, methods
./tools/peek.sh dump TimerBlock           # ...with the IL
./tools/peek.sh calls EmulatesAnyKeys     # who reads it
```

Guessing an API is not worth it when a probe compile settles it in ten seconds:
write the call into a scratch `.cs`, compile it against `Besiege_Data/Managed`
with `tools/besiegecc`, and if it compiles the member exists with that signature.
That is how `BlockBehaviour.AddSliderUnclamped`'s eight-argument overload and
`ModBlockBehaviour.EmulatesAnyKeys` being virtual were both established.

## Things that will bite

**A short public type name collides with something.** `Keys` is one of Besiege's
own global types (so are `Slider`, `Scrollbar`, `LOD` and `Particle`), and
`Convert` is `System.Convert`. Both compiled fine inside `namespace TimerPlusMod`
and both broke `tools/tests/TableCheck.cs`, which is in the global namespace —
with the error naming Besiege's or Microsoft's assembly as the location, so it
reads like an API that moved. Hence `Bindings` and `Conversion`. Check a new
public name against all three sets before using it; see
[docs/MODDING-NOTES.md](docs/MODDING-NOTES.md).

**`ModBlockBehaviour.AddSlider` hardcodes an `"x"` suffix**, so a slider added
through the modding API reads `1.00x` whatever it measures.
`ModBlockBehaviour.BlockBehaviour` *is* the handler the modding API adds to, so
`BlockBehaviour.AddSliderUnclamped(name, key, value, min, max, prefix, suffix,
onlyPositive)` is the same call with the suffix spelled out. `MSlider.Suffix`'s
setter is private and reflection is blacklisted, so there is no other way.

**Do not churn `DisplayInMapper`.** Every assignment marks the mapper dirty and it
answers by rebuilding *all* of its widgets — on a block with two hundred controls
that is a visible stall. `TimerPlusBehaviour.ShowStock` therefore keys off
"can the panel draw at all" (`Panel.Usable`, latched) rather than "is the panel up
now", and returns early unless the answer changed.

**The toolbar icon is cached on disk and nothing invalidates it.** It is
`Besiege_Data/Mods/Thumbnails/Blocks/<mod guid>_1.png`, rendered once. Changing
the mesh, the texture or the `<Icon>` pose updates the block in the world and not
its icon, which reads as the icon having the wrong model. Delete that file after
touching any of the three. A mod cannot do it for itself — `ModIO` only reaches
its own folders and this is not one of them.

**`InputField.onEndEdit` fires when the field loses focus, not only on Enter.**
`KeyCell`'s mode is therefore a field the button owns rather than something
derived from whether a variable name is set — deriving it let the box answer for
the cell on its way out and made the key/variable button impossible to click back.
See [docs/MODDING-NOTES.md](docs/MODDING-NOTES.md); the comment on
`KeyCell.variable` is the short version.

**A key with no keycodes is never registered.** `Machine.InitSimBlock` files a
key with `KeyInputController` from inside `for (i = 0; i < key.KeysCount; i++)`,
so a key carrying a variable name and no keycode joins no table and hears nothing
— which looks exactly like a block that ignores variables. `Bindings.BindVariable`
and `Conversion.Spell` both plant a keycode for that reason; with `useMessage` set
it is never answered to, and it is there to be counted.

**`MKey.IsReleased` is the one key property that does not check `useMessage`.** A
key handed over to a variable still reports the player letting go of the keycode it
used to be bound to. `KeyReader` guards it.

**UI Factory is a soft dependency and must stay one.** Every mention of
`Besiege.UI` lives in `UIF.cs`; a type that cannot be resolved fails as the method
mentioning it is compiled, so one guarded call site decides whether the panel can
exist. If you name `Besiege.UI` anywhere else, the mod stops loading for everyone
who has not subscribed to it.

## Style

C# 4, and Besiege's compiler is old: no interpolated strings, no `?.`, no
`nameof`, no expression-bodied members, and **any `enum` declaration segfaults
it** — the column constants in `Table` are `int`s for that reason.

`x.GetType().Name` compiles to a `System.Reflection` call and one reference to it
has the loader refuse the whole assembly. Test with `is`.

Comments say why, not what. If a line is the way it is because the obvious way was
wrong, the comment is the record of that and is worth more than the line.

The sibling repositories are the reference and are worth reading rather than
guessing: `../Besiege-Music` and `../Besiege-SpecialEffects` for the house style of
a docked UI Factory panel, and `../Besiege-clippy` for tooltips.
