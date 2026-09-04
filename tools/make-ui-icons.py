#!/usr/bin/env python3
"""The three switch-column headings, as textures the mod can load.

The table's H, S and L columns are a picture apiece rather than a letter. The
artwork in tools/icons is 1000 square, white on transparency, and each glyph
sits in a different part of its own square -- so drawn straight into a 20-pixel
heading the three would read as three different sizes.

This trims each one to what is actually drawn, pads it back to a square with a
constant margin, and scales it down. Trimming first is the whole point: after
it, "the same size" means the same size *of glyph*, which is what the eye
measures.

No PIL: the build has no Python dependencies and this is two hundred lines of
zlib either way.

    python3 tools/make-ui-icons.py

Writes TimerPlus/Resources/{Hold,Stop,Loop}.png, which Mod.xml declares and
Glyphs.cs loads by name.
"""

import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
SRC = os.path.join(HERE, "icons")
OUT = os.path.join(REPO, "TimerPlus", "Resources")

NAMES = ("Hold", "Stop", "Loop")

# What the game is given. Four times the ~20-pixel heading it is drawn in, so it
# still looks like a picture rather than a smudge on a 4K display.
SIZE = 80

# Clear space around the glyph, as a fraction of the finished square. The
# headings sit shoulder to shoulder in a 26-wide column; a glyph run right to
# the edge touches its neighbour.
MARGIN = 0.10


def read_png(path):
    """(width, height, RGBA rows) for an 8-bit RGBA PNG."""
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("%s is not a PNG" % path)
    at = 8
    bits = b""
    width = height = None
    while at < len(data):
        length = struct.unpack(">I", data[at:at + 4])[0]
        kind = data[at + 4:at + 8]
        chunk = data[at + 8:at + 8 + length]
        if kind == b"IHDR":
            width, height, depth, colour = struct.unpack(">IIBB", chunk[:10])
            if depth != 8 or colour != 6:
                raise SystemExit("%s is not 8-bit RGBA" % path)
        elif kind == b"IDAT":
            bits += chunk
        at += 12 + length

    raw = zlib.decompress(bits)
    stride = width * 4
    rows = []
    previous = bytearray(stride)
    at = 0
    for _ in range(height):
        filter_kind = raw[at]
        at += 1
        line = bytearray(raw[at:at + stride])
        at += stride
        for i in range(stride):
            left = line[i - 4] if i >= 4 else 0
            up = previous[i]
            corner = previous[i - 4] if i >= 4 else 0
            if filter_kind == 1:
                line[i] = (line[i] + left) & 255
            elif filter_kind == 2:
                line[i] = (line[i] + up) & 255
            elif filter_kind == 3:
                line[i] = (line[i] + (left + up) // 2) & 255
            elif filter_kind == 4:
                # Paeth.
                guess = left + up - corner
                dl, du, dc = (abs(guess - left), abs(guess - up),
                              abs(guess - corner))
                if dl <= du and dl <= dc:
                    line[i] = (line[i] + left) & 255
                elif du <= dc:
                    line[i] = (line[i] + up) & 255
                else:
                    line[i] = (line[i] + corner) & 255
        rows.append(line)
        previous = line
    return width, height, rows


def write_png(path, size, pixels):
    """An 8-bit RGBA PNG, every line written unfiltered."""
    raw = bytearray()
    for y in range(size):
        raw.append(0)
        raw += pixels[y]
    head = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)

    def chunk(kind, body):
        return (struct.pack(">I", len(body)) + kind + body
                + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF))

    open(path, "wb").write(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", head)
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + chunk(b"IEND", b""))


def bounds(width, height, rows):
    """The box the glyph actually occupies, by alpha."""
    left, right, top, bottom = width, -1, height, -1
    for y in range(height):
        line = rows[y]
        for x in range(width):
            if line[x * 4 + 3] > 8:
                if x < left:
                    left = x
                if x > right:
                    right = x
                if y < top:
                    top = y
                if y > bottom:
                    bottom = y
    if right < 0:
        raise SystemExit("nothing drawn in this icon")
    return left, top, right, bottom


def square(width, height, rows, box):
    """The glyph's box grown to a square about its own middle."""
    left, top, right, bottom = box
    side = max(right - left + 1, bottom - top + 1)
    # The margin is of the finished square, so the source square grows by it.
    side = int(round(side / (1.0 - 2.0 * MARGIN)))
    x = (left + right) // 2 - side // 2
    y = (top + bottom) // 2 - side // 2
    return x, y, side


def scale(width, height, rows, cut, size):
    """A box filter down to `size`.

    The artwork is one flat white on transparency, so only the alpha is
    actually resampled and the colour is written white throughout -- which is
    also what keeps the edges clean: averaging colour across transparent pixels
    is what fringes a scaled-down glyph with grey.
    """
    x0, y0, side = cut
    out = []
    for y in range(size):
        line = bytearray(size * 4)
        for x in range(size):
            sx0 = x0 + (x * side) // size
            sx1 = max(sx0 + 1, x0 + ((x + 1) * side) // size)
            sy0 = y0 + (y * side) // size
            sy1 = max(sy0 + 1, y0 + ((y + 1) * side) // size)
            total = 0
            count = 0
            for sy in range(sy0, sy1):
                inside = 0 <= sy < height
                row = rows[sy] if inside else None
                for sx in range(sx0, sx1):
                    count += 1
                    # Outside the source counts as clear, which is how the
                    # padding round a centred glyph stays empty.
                    if inside and 0 <= sx < width:
                        total += row[sx * 4 + 3]
            at = x * 4
            line[at] = 255
            line[at + 1] = 255
            line[at + 2] = 255
            line[at + 3] = total // max(1, count)
        out.append(line)
    return out


def main():
    if not os.path.isdir(OUT):
        raise SystemExit("no %s to write into" % OUT)
    for name in NAMES:
        source = os.path.join(SRC, name + ".png")
        width, height, rows = read_png(source)
        cut = square(width, height, rows, bounds(width, height, rows))
        write_png(os.path.join(OUT, name + ".png"), SIZE,
                  scale(width, height, rows, cut, SIZE))
        print("%s -> %d square" % (name, SIZE))
    return 0


if __name__ == "__main__":
    sys.exit(main())
