#!/usr/bin/env python3
"""Builds the Timer Plus block's mesh and texture from a Poly Pizza model.

    ./tools/make-block-mesh.py              convert (downloading if missing)
    ./tools/make-block-mesh.py --preview    also render it to tools/models/preview/

The model is "Timer" by Poly by Google, CC-BY 3.0, from
https://poly.pizza/m/0J1_OKm87pR. It is *not* committed: this fetches it into
tools/models/ and converts, which keeps the shipped folder to what Besiege loads
and leaves the licence with its source. Credit is in the README.

What conversion means here:

  * glTF is Y-up and right-handed; a Besiege block mesh is Z-up, and Unity is
    left-handed, so the map between them has to *flip* the handedness. A swap
    that preserves it -- the obvious (x, -z, y) -- gives a model that measures
    correctly and is mirrored, which on a clock face is a mirrored clock face.

  * The model carries no texture, only a flat colour per material. A block needs
    one, so the colours are collected into a palette a few pixels across and
    every triangle is pointed at its own patch. Besiege point-samples these, so
    nothing bleeds between patches.

  * The model's own colours are kept. It was recoloured yellow at first, on the
    idea that the block should read as a variant of Besiege's timer; it read as a
    yellow blob instead, and the authored blue reads as a timer perfectly well.
"""

import json, math, os, struct, sys, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
CACHE = os.path.join(HERE, "models")
OUT = os.path.join(REPO, "TimerPlus", "Resources")

# (poly.pizza id, asset uuid, title, author, licence)
SOURCE = ("0J1_OKm87pR", "e1b7ede2-ad4f-46f9-9819-051c8321d12f",
          "Timer", "Poly by Google", "CC-BY 3.0")

# How wide the block's mesh is across its longest axis. Besiege's own blocks are
# worn at half scale from z = 0.5, and 1.6 is what the sibling mods use for a
# model that should read as one cube of the grid.
SPAN = 1.6

# How the model is stood up on the block, after the axis swap: degrees about the
# block's own up, then about its front. Taste, and the only knob here.
#
# The half turn puts the display on the block's +y. That is the side a machine is
# usually looked at from, and at 0 the block went into the game showing its blank
# back to the camera -- see the sibling Orchestra mod, whose instruments face the
# same way for the same reason.
POSE = (180.0, 0.0)

# The block's toolbar icon rotation, which XmlCheck holds TimerPlus.xml to: the
# preview is drawn from these numbers, so a pose judged from a picture is the
# pose the game is given.
ICON = (-83.0, -45.0, 0.0)

# The model's palette, should it ever want repainting: material name (Poly Pizza
# names each one after its own hex colour) to the colour the block wears. Empty,
# which means the model is used as authored.
RECOLOUR = {}


# ---- glTF -----------------------------------------------------------------

def read_glb(path):
    d = open(path, "rb").read()
    if d[:4] != b"glTF":
        raise SystemExit("%s is not a GLB" % path)
    length = struct.unpack_from("<I", d, 12)[0]
    js = json.loads(d[20:20 + length])
    at, blob = 20 + length, b""
    while at < len(d):
        size, kind = struct.unpack_from("<II", d, at)
        if kind == 0x004E4942:
            blob = d[at + 8:at + 8 + size]
        at += 8 + size + (-size % 4)
    return js, blob


COMPONENT = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2),
             5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
COUNT = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def accessor(js, blob, index):
    acc = js["accessors"][index]
    fmt, size = COMPONENT[acc["componentType"]]
    n = COUNT[acc["type"]]
    view = js["bufferViews"][acc["bufferView"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = view.get("byteStride") or size * n
    return [struct.unpack_from("<" + fmt * n, blob, start + i * stride)
            for i in range(acc["count"])]


def node_matrix(node):
    if "matrix" in node:
        m = node["matrix"]
        return [m[0:4], m[4:8], m[8:12], m[12:16]]     # column-major
    t = node.get("translation", [0, 0, 0])
    x, y, z, w = node.get("rotation", [0, 0, 0, 1])
    s = node.get("scale", [1, 1, 1])
    rot = [[1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w)],
           [2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w)],
           [2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y)]]
    cols = [[rot[c][r] * s[c] for r in range(3)] + [0.0] for c in range(3)]
    return cols + [[t[0], t[1], t[2], 1.0]]


def multiply(a, b):
    """Column-major 4x4, a applied after b."""
    return [[sum(a[k][r] * b[c][k] for k in range(4)) for r in range(4)]
            for c in range(4)]


def apply(m, v, point=True):
    w = 1.0 if point else 0.0
    return tuple(sum(m[k][r] * (list(v) + [w])[k] for k in range(4)) for r in range(3))


def triangles(path):
    """Every triangle in the file, as (three positions, three normals, colour)."""
    js, blob = read_glb(path)
    colours = []
    for mat in js.get("materials", [{}]):
        base = mat.get("pbrMetallicRoughness", {}).get("baseColorFactor", [0.8, 0.8, 0.8, 1])
        colours.append(RECOLOUR.get(mat.get("name", ""), tuple(base[:3])))
    if not colours:
        colours = [(0.8, 0.8, 0.8)]

    out = []

    def walk(index, parent):
        node = js["nodes"][index]
        world = multiply(parent, node_matrix(node))
        if "mesh" in node:
            for prim in js["meshes"][node["mesh"]]["primitives"]:
                if prim.get("mode", 4) != 4:
                    continue
                pos = accessor(js, blob, prim["attributes"]["POSITION"])
                nrm = (accessor(js, blob, prim["attributes"]["NORMAL"])
                       if "NORMAL" in prim["attributes"] else None)
                idx = ([i[0] for i in accessor(js, blob, prim["indices"])]
                       if "indices" in prim else list(range(len(pos))))
                colour = colours[prim.get("material", 0) % len(colours)]
                for t in range(0, len(idx) - 2, 3):
                    tri = [idx[t], idx[t + 1], idx[t + 2]]
                    ps = [apply(world, pos[i]) for i in tri]
                    ns = ([apply(world, nrm[i], False) for i in tri] if nrm
                          else [face_normal(ps)] * 3)
                    out.append((ps, ns, colour))
        for child in node.get("children", []):
            walk(child, world)

    unit = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
    scene = js.get("scenes", [{}])[js.get("scene", 0)]
    for root in scene.get("nodes", range(len(js.get("nodes", [])))):
        walk(root, unit)
    return out


def face_normal(p):
    u = [p[1][i] - p[0][i] for i in range(3)]
    v = [p[2][i] - p[0][i] for i in range(3)]
    return normalise((u[1] * v[2] - u[2] * v[1],
                      u[2] * v[0] - u[0] * v[2],
                      u[0] * v[1] - u[1] * v[0]))


def normalise(v):
    d = math.sqrt(sum(c * c for c in v)) or 1.0
    return (v[0] / d, v[1] / d, v[2] / d)


# ---- placing it on the block ----------------------------------------------

def stand(tris, yaw, roll):
    """glTF's frame to the block's: Z up, centred, scaled, then posed."""
    # The negated x as well as z is what makes this a reflection rather than a
    # rotation, which is what turns a right-handed model into a left-handed one.
    # `wind` puts the triangle winding back from the shading normals afterwards.
    placed = [([(-p[0], -p[2], p[1]) for p in ps],
               [(-n[0], -n[2], n[1]) for n in ns], c) for ps, ns, c in tris]

    for angle, axis in ((math.radians(yaw), 2), (math.radians(roll), 0)):
        if abs(angle) < 1e-9:
            continue
        co, si = math.cos(angle), math.sin(angle)
        i, j = (0, 1) if axis == 2 else (1, 2)

        def turn(v, i=i, j=j, co=co, si=si):
            out = list(v)
            out[i] = v[i] * co - v[j] * si
            out[j] = v[i] * si + v[j] * co
            return tuple(out)

        placed = [([turn(p) for p in ps], [turn(n) for n in ns], c)
                  for ps, ns, c in placed]

    lo = [min(p[k] for ps, _, _ in placed for p in ps) for k in range(3)]
    hi = [max(p[k] for ps, _, _ in placed for p in ps) for k in range(3)]
    mid = [(lo[k] + hi[k]) / 2.0 for k in range(3)]
    scale = SPAN / max(hi[k] - lo[k] for k in range(3))
    return ([([tuple((p[k] - mid[k]) * scale for k in range(3)) for p in ps], ns, c)
             for ps, ns, c in placed],
            [(hi[k] - lo[k]) * scale for k in range(3)])


def wind(tris):
    """Turns any triangle whose face disagrees with its own shading normal."""
    out = []
    for ps, ns, colour in tris:
        avg = [sum(n[k] for n in ns) / 3.0 for k in range(3)]
        f = face_normal(ps)
        if sum(f[k] * avg[k] for k in range(3)) < 0.0:
            ps, ns = [ps[0], ps[2], ps[1]], [ns[0], ns[2], ns[1]]
        out.append((ps, ns, colour))
    return out


# ---- the palette ----------------------------------------------------------

CELL = 8            # pixels per colour, so point sampling cannot reach a neighbour


def palette(colours):
    """A square of flat patches, and where the middle of each one is in uv."""
    side = 1
    while side * side < len(colours):
        side += 1
    size = side * CELL
    rows = [[(0, 0, 0)] * size for _ in range(size)]
    uv = {}
    for i, c in enumerate(colours):
        cx, cy = i % side, i // side
        rgb = tuple(int(round(255 * srgb(v))) for v in c)
        for y in range(cy * CELL, (cy + 1) * CELL):
            for x in range(cx * CELL, (cx + 1) * CELL):
                rows[y][x] = rgb
        # v counts up from the bottom in a texture, down in the image.
        uv[c] = ((cx + 0.5) * CELL / size, 1.0 - (cy + 0.5) * CELL / size)
    return rows, uv


def srgb(value):
    """glTF factors are linear; a texture is read as sRGB."""
    if value <= 0.0031308:
        return max(0.0, 12.92 * value)
    return min(1.0, 1.055 * (value ** (1 / 2.4)) - 0.055)


def write_png(path, rows):
    height, width = len(rows), len(rows[0])
    raw = b"".join(b"\0" + b"".join(struct.pack("BBB", *px) for px in row) for row in rows)

    def chunk(kind, body):
        c = kind + body
        return struct.pack(">I", len(body)) + c + struct.pack(">I", zlib.crc32(c) & 0xffffffff)

    open(path, "wb").write(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b""))


# ---- output ---------------------------------------------------------------

def write_obj(path, tris, uv):
    verts, norms, faces = [], [], []
    seen_v, seen_n = {}, {}

    def index(store, seen, key):
        if key not in seen:
            store.append(key)
            seen[key] = len(store)
        return seen[key]

    texels = sorted(set(uv.values()))
    texel_index = dict((t, i + 1) for i, t in enumerate(texels))
    for ps, ns, colour in tris:
        faces.append([(index(verts, seen_v, tuple(round(c, 5) for c in p)),
                       texel_index[uv[colour]],
                       index(norms, seen_n, tuple(round(c, 4) for c in n)))
                      for p, n in zip(ps, ns)])

    with open(path, "w") as f:
        f.write("# Generated by tools/make-block-mesh.py -- edit that, not this.\n")
        f.write("# %s by %s, %s, from https://poly.pizza/m/%s\n"
                % (SOURCE[2], SOURCE[3], SOURCE[4], SOURCE[0]))
        for v in verts:
            f.write("v %.5f %.5f %.5f\n" % v)
        for t in texels:
            f.write("vt %.5f %.5f\n" % t)
        for n in norms:
            f.write("vn %.4f %.4f %.4f\n" % n)
        for face in faces:
            f.write("f " + " ".join("%d/%d/%d" % v for v in face) + "\n")
    return len(verts), len(faces)


# ---- a look at what came out ----------------------------------------------

def preview(path, tris, size=340, icon=False):
    """A flat-shaded render, so the pose can be judged without starting the game.
    Nothing here ships.

    Two views: three-quarters from the front, which is how a block is seen while
    it is being built with, and the toolbar's, worked out from the same rotation
    the block XML gives its icon. Both the camera and the projection are the
    sibling Orchestra mod's, whose numbers were checked against the game's own
    toolbar -- see the comments, which record which way round each cross product
    goes and what shipped wrong before it was settled.
    """
    if icon:
        eye, up = icon_camera()
    else:
        # From the block's +y, which is the face the display is on and the side a
        # machine is usually looked at from.
        eye = normalise((0.85, 1.0, 0.65))
        right = normalise((eye[1], -eye[0], 0.0))
        # right x eye, not eye x right: the other way round puts the block's up at
        # the bottom of the picture, which is a preview that lies about the one
        # thing it is for.
        up = (right[1] * eye[2] - right[2] * eye[1],
              right[2] * eye[0] - right[0] * eye[2],
              right[0] * eye[1] - right[1] * eye[0])
    # cross(up, eye) is the screen's right in a right-handed frame, and these
    # coordinates are Unity's, which is left-handed -- so it points left, and a
    # render drawn that way is a mirror of the game.
    right = normalise(cross(eye, up))
    up = normalise(cross(right, eye))
    # Beside the camera and to the right, which is where the toolbar's own light is.
    light = normalise((eye[0] + right[0] * 0.9 + 0.1,
                       eye[1] + right[1] * 0.9 + 0.1,
                       eye[2] + right[2] * 0.9 + 0.5))

    pixels = [[(24, 24, 28)] * size for _ in range(size)]
    depth = [[1e9] * size for _ in range(size)]
    span = SPAN * 1.25

    def project(p):
        x = sum(p[k] * right[k] for k in range(3))
        y = sum(p[k] * up[k] for k in range(3))
        z = sum(p[k] * eye[k] for k in range(3))
        return ((x / span + 0.5) * size, (0.5 - y / span) * size, -z)

    for ps, ns, colour in tris:
        pts = [project(p) for p in ps]
        n = face_normal(ps)
        shade = 0.35 + 0.65 * max(0.0, sum(n[k] * light[k] for k in range(3)))
        rgb = tuple(min(255, int(255 * srgb(c) * shade)) for c in colour)
        raster(pixels, depth, pts, rgb, size)
    write_png(path, pixels)


def raster(pixels, depth, pts, rgb, size):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1 = max(0, int(min(xs))), min(size - 1, int(max(xs)) + 1)
    y0, y1 = max(0, int(min(ys))), min(size - 1, int(max(ys)) + 1)
    ax, ay, az = pts[0]
    bx, by, bz = pts[1]
    cx, cy, cz = pts[2]
    area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)
    if abs(area) < 1e-9:
        return
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px, py = x + 0.5, y + 0.5
            w0 = ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) / area
            w1 = ((px - ax) * (cy - ay) - (py - ay) * (cx - ax)) / area
            if w0 < 0 or w1 < 0 or w0 + w1 > 1:
                continue
            z = az + w1 * (bz - az) + w0 * (cz - az)
            if z < depth[y][x]:
                depth[y][x] = z
                pixels[y][x] = rgb


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def icon_camera():
    """Where the toolbar looks at the block from, in the block's own frame.

    The toolbar camera is fixed and the block is turned in front of it by the
    icon rotation, so the camera in the block's frame is that rotation undone.
    Unity turns Z, then X, then Y, so undoing it is Y, then X, then Z.

    The sign on the view direction is not a guess: the sibling Orchestra mod
    settled it against the game's own toolbar after nine blocks shipped drawn
    from behind and below. Judge a change to ICON against a preview, and the
    preview against the game.
    """
    x, y, z = (math.radians(a) for a in ICON)

    def unturn(v):
        cy, sy = math.cos(-y), math.sin(-y)
        v = (v[0] * cy + v[2] * sy, v[1], -v[0] * sy + v[2] * cy)
        cx, sx = math.cos(-x), math.sin(-x)
        v = (v[0], v[1] * cx - v[2] * sx, v[1] * sx + v[2] * cx)
        cz, sz = math.cos(-z), math.sin(-z)
        return (v[0] * cz - v[1] * sz, v[0] * sz + v[1] * cz, v[2])

    return normalise(unturn((0.0, 0.0, -1.0))), normalise(unturn((0.0, 1.0, 0.0)))


# ---- fetching -------------------------------------------------------------

def fetch():
    path = os.path.join(CACHE, "Timer.glb")
    if os.path.exists(path):
        return path
    url = "https://static.poly.pizza/%s.glb" % SOURCE[1]
    print("fetching %s from %s" % (SOURCE[2], url))
    try:
        import urllib.request
        os.makedirs(CACHE, exist_ok=True)
        with urllib.request.urlopen(url, timeout=60) as r:
            open(path, "wb").write(r.read())
    except Exception as e:
        raise SystemExit("could not fetch the model (%s). Download it from\n"
                         "  https://poly.pizza/m/%s\nand save it as %s"
                         % (e, SOURCE[0], path))
    return path


def main():
    want_preview = "--preview" in sys.argv
    source = fetch()

    tris, extent = stand(triangles(source), POSE[0], POSE[1])
    tris = wind(tris)
    colours = sorted(set(c for _, _, c in tris))
    rows, uv = palette(colours)

    os.makedirs(OUT, exist_ok=True)
    verts, faces = write_obj(os.path.join(OUT, "TimerPlus.obj"), tris, uv)
    write_png(os.path.join(OUT, "TimerPlus.png"), rows)
    print("TimerPlus: %d verts, %d faces, %d colour(s), %.2f x %.2f x %.2f"
          % (verts, faces, len(colours), extent[0], extent[1], extent[2]))

    # The mod's own thumbnail, which Mod.xml names as its <Icon>. Drawn from the
    # same geometry as everything else, so it cannot show a block the mod does
    # not have -- and drawn every run, so it cannot go stale either.
    preview(os.path.join(OUT, "Thumbnail.png"), tris, size=512, icon=True)

    if want_preview:
        out = os.path.join(CACHE, "preview")
        os.makedirs(out, exist_ok=True)
        preview(os.path.join(out, "TimerPlus.png"), tris)
        preview(os.path.join(out, "TimerPlus-icon.png"), tris, icon=True)
        print("previews in %s" % out)


if __name__ == "__main__":
    main()
