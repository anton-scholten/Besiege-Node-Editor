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
- **`Row`** is one row's controls plus its phase; **`Clock`** is the phase machine.
  A row has no activation of its own: the block's key starts every row, and the
  mapper above the table is where it is set.
- **`Table`** lifts rows out of their controls as `RowData` so they can be sorted
  and deleted — a row *is* its controls, and reordering rows means moving values
  between them.
- **`Panel`** is one UI Factory window, on a `DontDestroyOnLoad` object, watching
  `BlockMapper.onMapperOpen`. It holds the *behaviour*, never a control, and reads
  the controls fresh on every fill: two blocks of the same kind do not share their
  mapper controls, and a panel that captured one would show the first block's
  values and write to it.
- **`Conversion`** and **`Drop`** build `BlockInfo`s and add them through Besiege's
  own additive-load path.
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
  too -- both are a dozen lines of coverage sampling and neither is worth a file
  in `Resources`. They are drawn into a
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
