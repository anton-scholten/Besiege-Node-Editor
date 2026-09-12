# Besiege modding notes

Working notes on the parts of Besiege's mod loader and UI this mod had to work
out, recorded because they are not in the general notes at
[Besiege-Modding-AI-notes](https://github.com/anton-scholten/Besiege-Modding-AI-notes)
or because they correct something there. Each was read out of the game's own
assemblies (`Besiege_Data/Managed/`) with that repository's `tools/peek.sh`, or
settled with a probe compile; where a claim cost something to establish, that is
said.

Target: Besiege on Unity **5.4.0f3**, built-in mod loader, UI Factory 3.

## Two short type names that collide, and the rule behind them

### `Keys` is a fifth global type name in `Assembly-CSharp`

Note 01 lists four names Besiege declares in the **global** namespace that shadow
Unity's, and warns that C# checks the global namespace before `using` directives:
`Slider`, `Scrollbar`, `LOD`, `Particle`, with `UnityLogWriter` named for
completeness.

There is a fifth worth knowing, and it does not collide with Unity — it collides
with *yours*:

```
type Keys : System.Object
  field public static String Alpha0
  ...
```

`Keys` is a table of key-name strings in `Assembly-CSharp`. A mod class called
`Keys` compiles perfectly inside its own namespace and breaks the moment anything
in the **global** namespace does `using YourMod;` and names it — a build-time test,
say. The error is `'Keys' does not contain a definition for 'Spell'` with the
game's own assembly named as the location, which reads like a Besiege API that
has moved rather than like a name clash.

### `Convert` is the same trap from the other direction

The class that turns this mod's table into real timer blocks was called
`Convert`. It compiled inside `namespace NodeEditorMod` and broke the same
build-time test, this time against **`System.Convert`**:

```
error CS0117: `System.Convert' does not contain a definition for `Spell'
mscorlib.dll (Location of the symbol related to previous error)
```

Inside its own namespace the nearer name wins, so nothing complains; from the
global namespace with `using System;` in scope, it does not.

### The rule

The hazard is not only names Unity also uses, and it is not only Besiege's
globals. A short, obvious name for a **public** type is a hazard against three
sets at once: Besiege's global types, `System`, and `UnityEngine`. Two checks,
both a few seconds:

```sh
./tools/peek.sh sig Convert          # Besiege's globals and Unity's
```

and compiling one throwaway file in the *global* namespace that does
`using System; using YourMod;` and names the type. Anything a mod ships as
`public` deserves that, because the error when it goes wrong names somebody
else's assembly as the location and reads like an API that moved.

## `AddSlider` through the modding API always says `x`

Note 16 records that `MSliderDefinition.Create` hardcodes `prefix = ""` and
`suffix = "x"`, so a slider declared in **block XML** reads `1.00x` whatever it
measures. The same is true of the sliders added in **code**, which the note does
not say:

```
Modding.ModBlockBehaviour::AddSlider(5)            -> "", "x"
Modding.ModBlockBehaviour::AddSliderUnclamped(5)   -> "", "x"
```

Both forward to `SaveableDataHolder` with the two strings pushed as literals.
`MSlider`'s `Suffix` has a **private** setter, and reflection is blacklisted, so
the object it hands back cannot be corrected either.

The way out is that `ModBlockBehaviour.BlockBehaviour` *is* the handler the
modding API adds to — `get_BlockBehaviour` is `ldfld handler`, and
`ModBlockBehaviourHandler : BlockBehaviour`. So `SaveableDataHolder`'s own
overloads are reachable, public, and take the prefix and suffix:

```csharp
// seconds, and unclamped like the game's own timer declares its two
MSlider wait = BlockBehaviour.AddSliderUnclamped(
    "Wait", "Wait0", 1f, 0f, 60f, "", "s", true);
```

The last argument is `onlyPositive`; `TimerBlock.Awake` passes `true` for both of
its sliders. The same trick gives `AddSlider` a blank suffix for a plain count.

This matters more than a cosmetic usually would, because the fallback path — a
mod whose panel needs UI Factory — is exactly where the stock mapper is the only
interface, and `12.00x seconds` in it is not a small thing.

## `ModBlockBehaviour.EmulatesAnyKeys` is virtual, and returns false

Note 03 covers reading an emulated key and never covers *sending* one from a
modded block. The API is there:

```csharp
public override bool EmulatesAnyKeys { get { return true; } }
...
EmulateKeys(ownActivationKeys, emulateKey, down);
```

Three things about it:

- **The property is virtual and its base returns `false`.**
  `ModBlockBehaviourHandler.Awake` reads it and ORs it into
  `BlockPrefab.EmulatesAnyKeys`, which is what `Machine.RegisterEmulationUpdate`
  and the block tooltip go on. Without the override, `EmulateKeys` still runs but
  logs `"Block X uses EmulateKeys but does not set EmulatesAnyKeys property! This
  will not work correctly!"` as an error, once per press.
- **The first argument is the self-trigger guard.**
  `KeyInputController.EmulateEntry` compares the key it is about to press against
  that array **by reference** and refuses a match. `TimerBlock` passes its own
  single activation key; a block with several activation keys passes all of them,
  and a row then cannot press a key that would start the same block.
- **`BlockBehaviour.EmulateKeys` returns early unless `SimPhysics`,** so a
  building block calling it is a no-op rather than an error.

### `KeyInputController.Emulate` does not throw on an unregistered name

Worth recording because the IL looks as though it does. It reads

```csharp
usedMessages[name]        // indexer, not TryGetValue
usedKeys[keycode]         // likewise
```

which is a `KeyNotFoundException` waiting to happen for a variable nothing
listens to. It never fires, and the reason is in `AddMKey`: the dictionary entry
for a message is created **before** the `isEmulator` test that decides whether the
key joins the list under it. So an emulator key registers the *name* even though
it does not register itself as a listener, and by the time anything can emulate
that name the entry exists. A keycode of `KeyCode.None` is skipped earlier still.

## The emulation hooks a modded block gets, and their rates

Note 03 names `KeyEmulationUpdate` and the note on latching. Its counterpart is
not named anywhere, and a timer needs it:

| Besiege's block | a modded block's override | when |
| --- | --- | --- |
| `SendEmulationUpdateBlock` | `SendKeyEmulationUpdateHost` | first, once per emulation tick |
| `EmulationUpdateBlock` | `KeyEmulationUpdate` | after, same tick |

`Machine.FixedUpdate` drives both from inside `if (everyOther == 0)`, so with
Besiege's 100 Hz fixed step they run at **50 Hz** — which is where
`TimerBlock.TimeToFrameCount`'s `* 50` comes from, and what any reproduction of a
timer has to count in.

`ModBlockBehaviourHandler.SendEmulationUpdateBlock` gates the callback on
`isSimulating && SimPhysics`, where Besiege's own `TimerBlock` gates only on
`_parentMachine.isReady`. A modded block therefore never ticks on a client without
physics, which is why the vanilla timer's ping-compensating prephase has no
analogue worth writing: there is no such client to compensate for.

Both are reached as `ldvirtftn` through `ModdingUtil.PerformCallback`, so a caller
search for either finds nothing — the corollary note 08 already warns about,
holding for two more methods.

## Besiege omits default-valued controls from a save

`SaveableDataHolder.SaveMapperValues` opens with

```csharp
bool skip = StatMaster.SavingXML && OptionsMaster.BesiegeConfig.ExcludeDefaultSaveData;
...
if (skip && one.isDefaultValue) continue;
one.Serialize() -> holder.Write(...)
```

This is what makes a block with a *fixed pool* of controls affordable. Timer Plus
registers 227 mapper types — thirty-two rows of seven, plus three — because `MKey`
is the only mapper type that carries a variable and keys can only be registered in
`SafeAwake`, so a table that can be automated has to allocate its rows up front.
A machine using three rows pays for three: the other twenty-nine are at their
defaults and are not written.

Two consequences worth planning for:

- it is only true while the player has that option on, so the pool should still be
  a size you would accept unconditionally;
- `isDefaultValue` is what decides, so "unused" has to *be* the default. Deleting
  a row therefore resets the last row's controls rather than leaving them holding
  the values that shifted up — otherwise a deleted row reappears the moment the
  count is raised again, and it was in the save the whole time.

## `KeyCode.None` is a usable "nothing is bound here"

`MKey` arrives from `AddKey(name, key, KeyCode.None)` holding one keycode, `None`,
and both of the places that would act on it decline:

- `KeyInputController.AddMKey` returns immediately on `keycode == 0 && !useMessage`,
  so the key is never registered under anything;
- `KeyInputController.Emulate` skips a `None` in its keycode walk.

So `None` is a real third state beside "a key" and "a variable", it survives a
save, and `MKey.isDefaultValue` stays true for it. Timer Plus spells "this row
follows the block's own key" that way, which costs no extra control per row — and
seven per row was already the budget.

## `MKey.RemoveKey` takes an index, `AddOrReplaceKey` takes both

Both are easy to get backwards and neither is documented anywhere. From the IL:

```csharp
key.RemoveKey(int index);              // _keyCodes.RemoveAt(index), bounds-checked
key.AddOrReplaceKey(int index, KeyCode code);   // dedupes, appends if index >= Count
```

`RemoveKey` reads as though it takes the code to remove — it does not, and passing
a `KeyCode` where an index is wanted compiles under an explicit cast and quietly
removes the wrong entry. To leave a key holding exactly one code, remove down to
one and then `AddOrReplaceKey(0, code)`.

## A UI Factory prefab's label needs to be allowed to shrink

Note 04 covers pinning a `Text Button`'s caption to its own control so it does not
overhang its neighbour and steal the clicks. There is a second half to that in a
**table**, where the content is not yours to choose: a variable name is whatever
somebody typed, and `UIF`'s labels are set to `HorizontalWrapMode.Overflow`, so a
long one is drawn straight across the next column — where it also reads as *that
column's* value, which is worse than being clipped.

`resizeTextForBestFit` with a floor of about 8 is the honest failure: a long name
in a narrow cell is small rather than somewhere else. It wants
`horizontalOverflow = Wrap` and `verticalOverflow = Truncate` set with it, or
best-fit has nothing to shrink against.

## Besiege's own key/variable icons can be borrowed

A mod drawing its own key cells wants the two speech bubbles the block mapper puts
beside a key — three dots while the key is on the keyboard, a cross while a
variable holds it — or its cells read as something else's interface sitting inside
the game's.

They are reachable, and the namespace they are in is the reason: `Selectors` is
**not** on the loader's blacklist, only `InternalModding` is. `Selectors.KeySelector`
holds both buttons as public fields, and `ToggleVar` settles which is which:

```csharp
variableSelector.SetActive(on);
messageToggleOff.SetActive(on);                              // the cross
addButton.SetActive(!on);
messageToggleOn.SetActive(!on && config.AdvancedBuilding);   // the three dots
```

So `messageToggleOn` is the one shown while the message option is **off** — it is
named for what it does, not for when it appears, which is the opposite of the
first guess.

Two things make the borrow cheap:

- **`Resources.FindObjectsOfTypeAll<Selectors.KeySelector>()`**, not
  `FindObjectsOfType`. The mapper's selectors are pooled and most are inactive
  most of the time; an inactive one carries the artwork just the same.
- **A `RawImage`, not an `Image`.** The mapper is mesh UI, so what comes back is
  the `Texture` on a button's renderer material. `Image` wants a `Sprite`, which
  wants a `Texture2D` — an assumption there is no reason to make when uGUI ships a
  graphic that takes a bare `Texture`.

The mapper has to have built a selector before there is anything to read, so the
lookup should cache only a *yes* and the control should build its picture on a
repaint rather than once at construction. Otherwise a cell made a moment too early
keeps its fallback lettering until the whole panel is rebuilt.

## `InputField.onEndEdit` fires on losing focus, not only on Enter

This is Unity's, not Besiege's, and it is in here because it produced a bug that
read as something else entirely.

A cell that switches between "bind a key" and "type a variable" wants one flag for
which mode it is in. Deriving that flag from the data -- *this is variable mode
because a name is set* -- looks tidier and is wrong, because the box announces its
contents on the way out:

- switching **away** from variable mode deactivates the box, the deactivation
  fires `onEndEdit` with whatever was typed, and the handler puts the mode back.
  The control cannot be switched back at all.
- with the box **empty**, the deselect that Unity performs before delivering the
  click has already flipped the mode, so the click then flips it forward again --
  same symptom, different route.
- and clicking a *different* row's button deselects the first box, so **that** row
  changes mode while the row you clicked is the one that sticks.

Three symptoms, one cause. The fix is that the mode is a field the button owns,
the text only says *which* variable, and a flag suppresses `onEndEdit` while the
button is doing the switching -- `onEndEdit` is not "the user finished editing",
it is "this field stopped being focused", and those are not the same event.

## UI Factory's tooltip in a list that scrolls

`Besiege.UI.Bridge.Tooltip` is a per-control handler pointed at a per-control
panel, and it does not position the panel -- it fades and slides it from wherever
it was authored, so the caller places it. That shape is right for a row of buttons
on a title bar and wrong for a table:

- the rows scroll and are shown and hidden a whole row at a time as they leave the
  frame, so a panel parented into a row is clipped with it and has to be placed
  again every frame;
- uGUI draws siblings in order, so whether a tooltip is drawn over the row above
  it or under the row below it depends on which row it lives in.

One panel on the canvas, moved to whatever is hovered, has neither problem and is
less code. What is worth keeping from UI Factory is the **artwork** -- spawn `Text
Tooltip (Vis Only)` and drive it yourself. Three details:

- the prefab carries a `VerticalLayoutGroup` and a `ContentSizeFitter`, so it
  sizes itself to the words; call `LayoutRebuilder.ForceRebuildLayoutImmediate`
  before measuring it to place it, or you are measuring last frame's size.
- the triangle is two assignments, the same two the handler's `OnValidate` makes:
  anchor `(0.5, 0)` and a half turn for a panel *above* what it explains, `(0.5, 1)`
  and upright for one below.
- turn `raycastTarget` off on every `Graphic` in it. The panel is over the window,
  and a tooltip that eats the pointer takes the hover that is keeping it open --
  it flickers.

`Besiege.UI.Bridge.Tooltip.TooltipsActive` is a public static `Func<bool>` that
switches every tooltip in the game off. A panel of your own should read it even
though nothing makes it: a player who turned tooltips off turned them off.

## Holding the keyboard while a control listens for a key

A "click here and press a key" cell has to raise `StatMaster.SetInMenu(true)` for
exactly as long as it is listening, or the key being bound also drives the camera
and fires whatever else is bound to it. Note 08 has the counting rule.

The part that is not obvious is that **the count has to be given back when the
control goes away while still listening**, which for a panel that hides its rows
as they scroll out of the frame is a normal thing to happen — not an edge case.
`OnDisable` and `OnDestroy` both have to drop it, and a single static "who is
listening" reference is worth having, because two cells listening at once bind the
same press to both and the first goes on holding the keyboard after the pointer
has moved on.
