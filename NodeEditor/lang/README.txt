Translating this mod
====================

Every word the mod puts on screen is a key in a file like the one beside this,
`example.txt`. To translate the mod:

Every language Besiege ships in is already here. To correct one, edit its file.
To add one:

  1. Copy `example.txt` to the name Besiege uses for the language, in this same
     folder -- `French.txt`, `German.txt`, `ChineseSimplified.txt`. They are the
     same names as the game's own `Localisation Files` folder, which is where to
     look if you are unsure.
  2. Translate the right-hand side of each line. Leave the key on the left of
     the `=` exactly as it is: that is how the mod finds the words.
  3. Start Besiege with that language chosen.

The translations that ship were made without a native speaker's eye. If one
reads badly, it probably is bad -- corrections are the most useful thing you can
send.

The file is read from this folder, and then from the mod's own data folder, so a
player can put a translation of their own over one that shipped without touching
the install.

Rules, all of them short:

  * Lines beginning with `//` are ignored, and so are empty ones.
  * `\n` in a value is a line break. Several messages want two short lines
    rather than one long one -- the board is not wide.
  * `{0}` is a number or a name the mod fills in. Keep it, and put it where your
    own grammar wants it.
  * A key you leave out, or a line you get wrong, falls back to English. A
    part-finished translation is still worth having.
  * The words in CAPITALS are buttons and column headings, and the board has
    little room: short is better than exact.

Two things are deliberately not here. The gate names -- AND, OR, XOR and the
rest -- come from Besiege's own translations, so they are already in your
language. And the words written into save files are never translated, or a
machine saved in one language would not load in another.
