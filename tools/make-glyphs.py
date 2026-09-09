#!/usr/bin/env python3
"""The node editor's drawn glyphs, baked into PNGs the mod loads.

Every one of these used to be drawn a pixel at a time when the game asked for
it, sixteen samples to a pixel, and the twenty of them together were a stall on
the first block opened. They do not change between runs, so they are drawn here
instead and shipped beside the block's mesh.

    python3 tools/make-glyphs.py

Writes TimerPlus/Resources/glyph-*.png, which Mod.xml declares and Glyphs.cs
loads by name. The shapes are the ones that were in Glyphs.cs, kept in the same
0..1 space with v counting up -- so a change here is a change to the picture and
nothing else has to follow it.

No PIL: the build has no Python dependencies, and a PNG writer is thirty lines.
"""

import math
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
OUT = os.path.join(REPO, "TimerPlus", "Resources")

# Coverage samples per pixel, per axis. Sixteen samples an edge is what makes a
# forty-eight pixel glyph look drawn rather than stepped.
SAMPLES = 4

GATE_SIZE = 48
PLATE_SIZE = 64
ARROW_SIZE = 32
CORNER_SIZE = 32
GRID_SIZE = 32
BAR_SIZE = 24
DASH_STEP = 8

STROKE = 0.085          # how thick a drawn outline is, as a fraction of the square
CORNER = 0.30           # how much of the plate's half-width its corners take
BAR_WIDTH = 0.34        # how much of a stripe tile the stripe covers


def write_png(path, width, height, rows):
    """rows: top-to-bottom lists of (r, g, b, a) bytes."""
    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in rows)

    def chunk(kind, body):
        return (struct.pack(">I", len(body)) + kind + body
                + struct.pack(">I", zlib.crc32(kind + body) & 0xffffffff))

    with open(path, "wb") as out:
        out.write(b"\x89PNG\r\n\x1a\n")
        out.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)))
        out.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        out.write(chunk(b"IEND", b""))


def coverage(size, inside, flip=False):
    """A white square whose alpha is how much of each pixel `inside` covers.

    `inside(u, v)` is asked in the 0..1 square with v counting **up**, which is
    how a texture is read; the rows come back top-first, which is how a PNG is
    written.
    """
    rows = []
    for row in range(size):
        y = size - 1 - row                      # PNG top row is the texture's last
        line = []
        for x in range(size):
            on = 0
            for sy in range(SAMPLES):
                for sx in range(SAMPLES):
                    u = (x + (sx + 0.5) / SAMPLES) / size
                    v = (y + (sy + 0.5) / SAMPLES) / size
                    if flip:
                        v = 1.0 - v
                    if inside(u, v):
                        on += 1
            line.append((255, 255, 255,
                         int(round(255.0 * on / (SAMPLES * SAMPLES)))))
        rows.append(line)
    return rows


def away(u, v, x, y):
    return math.hypot(u - x, v - y)


# ---- the gate symbols ------------------------------------------------------

def and_body(u, v):
    """A rectangle with a half-disc on its nose."""
    if v < 0.18 or v > 0.82:
        return False
    return (0.14 <= u <= 0.48) or away(u, v, 0.48, 0.5) <= 0.32


def or_body(u, v):
    """The same nose, hollowed at the back."""
    return and_body(u, v) and away(u, v, -0.10, 0.5) >= 0.31


def bubble(u, v):
    return away(u, v, 0.86, 0.5) <= 0.10


def box(u, v, x0, y0, x1, y1):
    outer = x0 <= u <= x1 and y0 <= v <= y1
    t = STROKE * 0.8
    inner = x0 + t <= u <= x1 - t and y0 + t <= v <= y1 - t
    return outer and not inner


def bar(u, v, x0, y0, x1, y1):
    return x0 <= u <= x1 and y0 <= v <= y1


def triangle(u, v):
    if u < 0.16 or u > 0.74:
        return False
    half = 0.34 * (0.74 - u) / 0.58
    return abs(v - 0.5) <= half


def crescent(u, v):
    """The second back curve that turns an OR into an XOR.

    The same arc as the body's own back, struck a little further left, and cut
    off short of the square's edge: an arc that runs into the edge is drawn as a
    bar down it, which is what the ends of this one used to be.
    """
    d = away(u, v, -0.18, 0.5)
    return (0.18 <= v <= 0.82 and u >= 0.03
            and 0.31 - STROKE <= d <= 0.31)


def lever(u, v):
    """The lever of the latch's switch."""
    if u < 0.26 or u > 0.82:
        return False
    line = 0.30 + (u - 0.26) * 0.75
    return abs(v - line) <= STROKE * 0.7


def notch(u, v):
    """The clock notch on the left of a flip-flop."""
    if u < 0.20 or u > 0.34:
        return False
    half = 0.14 * (0.34 - u) / 0.14
    return abs(v - 0.5) <= half


# In the order Besiege lists its gates, which is the order Gates.cs uses.
GATES = ("not", "and", "or", "nor", "nand", "xor", "xnor", "random",
         "srlatch", "dlatch", "counter", "edge")


def gate(which):
    def inside(u, v):
        if which == 0:          # NOT: a triangle with the inverting bubble
            return triangle(u, v) or bubble(u, v)
        if which == 1:          # AND
            return and_body(u, v)
        if which == 2:          # OR
            return or_body(u, v)
        if which == 3:          # NOR
            return or_body(u - 0.06, v) or bubble(u, v)
        if which == 4:          # NAND
            return and_body(u - 0.06, v) or bubble(u, v)
        if which == 5:          # XOR
            return or_body(u, v) or crescent(u, v)
        if which == 6:          # XNOR
            return (or_body(u - 0.06, v) or crescent(u - 0.06, v)
                    or bubble(u, v))
        if which == 7:          # RANDOM: a die, three pips on a square
            return (box(u, v, 0.16, 0.16, 0.84, 0.84)
                    or away(u, v, 0.32, 0.32) <= 0.07
                    or away(u, v, 0.50, 0.50) <= 0.07
                    or away(u, v, 0.68, 0.68) <= 0.07)
        if which == 8:          # SR LATCH: a switch on a pivot
            return away(u, v, 0.26, 0.30) <= 0.09 or lever(u, v)
        if which == 9:          # D LATCH: a box with a clock notch
            return box(u, v, 0.20, 0.16, 0.84, 0.84) or notch(u, v)
        if which == 10:         # COUNTER: bars climbing
            return (bar(u, v, 0.18, 0.18, 0.34, 0.42)
                    or bar(u, v, 0.42, 0.18, 0.58, 0.62)
                    or bar(u, v, 0.66, 0.18, 0.82, 0.82))
        if which == 11:         # EDGE: low, a rise, then high
            return (bar(u, v, 0.14, 0.24, 0.46, 0.34)
                    or bar(u, v, 0.46, 0.24, 0.56, 0.76)
                    or bar(u, v, 0.56, 0.66, 0.88, 0.76))
        return box(u, v, 0.18, 0.18, 0.82, 0.82)
    # The gate shapes are drawn with v counting down the square, the way they
    # were written; everything else counts up.
    return coverage(GATE_SIZE, inside, flip=True)


# ---- the rest --------------------------------------------------------------

def circle(filled):
    def inside(u, v):
        d = away(u, v, 0.5, 0.5)
        if filled:
            return d <= 0.36
        return 0.40 - STROKE * 1.6 <= d <= 0.40
    return coverage(GATE_SIZE, inside)


def plate():
    radius = PLATE_SIZE * 0.5 * CORNER
    inner = PLATE_SIZE * 0.5 - radius

    def inside(u, v):
        # In pixels from the middle, per axis, with the straight part of each
        # side taken off: what is left is the corner.
        du = max(0.0, abs(u * PLATE_SIZE - PLATE_SIZE * 0.5) - inner)
        dv = max(0.0, abs(v * PLATE_SIZE - PLATE_SIZE * 0.5) - inner)
        return du * du + dv * dv <= radius * radius
    return coverage(PLATE_SIZE, inside)


def arrow():
    """A triangle pointing up: the mark on a sorted column's heading."""
    return coverage(ARROW_SIZE,
                    lambda u, v: abs(u - 0.5) <= (1.0 - v) * 0.5)


def resizer(other):
    """The pointer over a corner that can be dragged: a double-headed arrow
    along the diagonal, with a dark edge so it shows on a pale machine."""
    rows = []
    for row in range(CORNER_SIZE):
        y = CORNER_SIZE - 1 - row
        line = []
        for x in range(CORNER_SIZE):
            u = (x + 0.5) / CORNER_SIZE
            v = (y + 0.5) / CORNER_SIZE
            if other:
                u = 1.0 - u
            shaft = abs(u - v) < 0.10 and 0.20 < u < 0.80
            head = ((u + v < 0.42 and abs(u - v) < 0.22)
                    or (u + v > 1.58 and abs(u - v) < 0.22))
            on = shaft or head
            edge = (not on
                    and ((abs(u - v) < 0.16 and 0.14 < u < 0.86)
                         or (u + v < 0.48 and abs(u - v) < 0.28)
                         or (u + v > 1.52 and abs(u - v) < 0.28)))
            if on:
                line.append((255, 255, 255, 255))
            elif edge:
                line.append((0, 0, 0, 217))
            else:
                line.append((0, 0, 0, 0))
        rows.append(line)
    return rows


def grid():
    """One square of the board's grid: two faint lines along two edges, so a
    sheet of them is a grid and not a set of boxes."""
    rows = []
    for row in range(GRID_SIZE):
        y = GRID_SIZE - 1 - row
        line = []
        for x in range(GRID_SIZE):
            on = x == 0 or y == 0
            line.append((255, 255, 255, 31 if on else 0))
        rows.append(line)
    return rows


def dashes(across):
    """A dash and a gap, drawn to repeat -- one texture each way, so the dashes
    are the same size along an edge whichever way it lies."""
    width = DASH_STEP if across else 2
    height = 2 if across else DASH_STEP
    rows = []
    for row in range(height):
        y = height - 1 - row
        line = []
        for x in range(width):
            along = x if across else y
            line.append((255, 255, 255, 255 if along < 5 else 0))
        rows.append(line)
    return rows


def stripes():
    """One tile of a diagonal stripe, for a cell that is there but not read."""
    def inside(u, v):
        along = u + v
        if along >= 1.0:
            along -= 1.0
        return along < BAR_WIDTH
    return coverage(BAR_SIZE, inside)


def main():
    if not os.path.isdir(OUT):
        raise SystemExit("no %s" % OUT)
    made = []

    def save(name, rows):
        path = os.path.join(OUT, "glyph-%s.png" % name)
        write_png(path, len(rows[0]), len(rows), rows)
        made.append(name)

    for i, name in enumerate(GATES):
        save(name, gate(i))
    save("ring", circle(False))
    save("dot", circle(True))
    save("plate", plate())
    save("arrow", arrow())
    save("resize", resizer(False))
    save("resize2", resizer(True))
    save("grid", grid())
    save("dash", dashes(True))
    save("dashdown", dashes(False))
    save("bars", stripes())
    print("%d glyphs -> %s" % (len(made), OUT))


if __name__ == "__main__":
    main()
