#!/usr/bin/env python3
"""Builds the mod's block meshes and textures from Poly Pizza models.

    ./tools/make-block-mesh.py                     convert every block
    ./tools/make-block-mesh.py TimerPlus           just the one
    ./tools/make-block-mesh.py --preview           also render to models/preview/

The models are "Timer" by Poly by Google (https://poly.pizza/m/0J1_OKm87pR) and
"Simple computer" by Robert Schlyter (https://poly.pizza/m/doMMnviJrGi), both
CC-BY 3.0. They are *not* committed: this fetches them into tools/models/ and
converts, which keeps the shipped folder to what Besiege loads and leaves the
licence with its source. Credit is in the README.

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

import json, math, os, re, struct, sys, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
CACHE = os.path.join(HERE, "models")
OUT = os.path.join(REPO, "NodeEditor", "Resources")

# Every block the mod ships, by the name its XML and its resources carry.
#
#   source    (poly.pizza id, asset uuid, title, author, licence)
#   span      how wide the mesh is across its longest axis. Besiege's own blocks
#             are worn at half scale from z = 0.5, and 1.6 is what the sibling
#             mods use for a model that should read as one cube of the grid.
#   pose      how the model is stood up on the block, after the axis swap:
#             degrees about the block's own up, then about its front. Taste, and
#             the only knob here. The half turn puts the face on the block's +y,
#             which is the side a machine is usually looked at from -- at 0 the
#             block went into the game showing its blank back to the camera.
#   icon      the toolbar rotation, which XmlCheck holds the block XML to: the
#             preview is drawn from these numbers, so a pose judged from a
#             picture is the pose the game is given. x is what leans the block in
#             the tile and stays near -90; see notes 02.
#   recolour  material name (Poly Pizza names each one after its own hex colour)
#             to the colour the block wears. Empty means as authored.
BLOCKS = {
    "TimerPlus": {
        "source": ("0J1_OKm87pR", "e1b7ede2-ad4f-46f9-9819-051c8321d12f",
                   "Timer", "Poly by Google", "CC-BY 3.0"),
        "span": 1.6,
        "pose": (180.0, 0.0),
        "icon": (-83.0, -45.0, 0.0),
        "recolour": {},
    },
    "NodeEditor": {
        "source": ("doMMnviJrGi", "4f68868e-5e24-48ff-92a5-442c8bf3d0f7",
                   "Simple computer", "Robert Schlyter", "CC-BY 3.0"),
        "span": 1.6,
        # No turn: this model is authored facing the way the Timer needs turning
        # to. Half a turn here shows the back of the monitor.
        "pose": (0.0, 0.0),
        "icon": (-83.0, -45.0, 0.0),
        "recolour": {},
    },
}

# The block being converted. Set by main; read by the handful of steps that are
# about this block rather than about geometry in general.
CURRENT = BLOCKS["TimerPlus"]
NAME = "TimerPlus"


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
        colours.append(CURRENT["recolour"].get(mat.get("name", ""), tuple(base[:3])))
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

# The board's own colours: the card, its chips and their legs.
BOARD = ((0.05, 0.30, 0.16), (0.10, 0.10, 0.12), (0.72, 0.66, 0.30))


def box(low, high, colour):
    """A box as twelve triangles, wound so its faces point outwards."""
    x0, y0, z0 = low
    x1, y1, z1 = high
    corners = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
               (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
             (2, 3, 7, 6), (1, 2, 6, 5), (0, 4, 7, 3)]
    out = []
    for a, b, c, d in faces:
        for tri in ((a, b, c), (a, c, d)):
            ps = [corners[i] for i in tri]
            out.append((ps, [face_normal(ps)] * 3, colour))
    return out


def board():
    """A circuit card with three chips on it.

    Written in glTF's frame, like everything else this converts, so the chips go
    on -z: `stand` maps z to the block's -y, and +y is the side a machine is
    looked at from.
    """
    tris = box((-0.8, -0.8, -0.06), (0.8, 0.8, 0.06), BOARD[0])
    for x in (-0.46, 0.0, 0.46):
        tris += box((x - 0.15, -0.24, -0.18), (x + 0.15, 0.24, -0.06), BOARD[1])
        # Legs out of each end of the chip, which is what says it is a chip.
        for y in (-0.32, 0.24):
            tris += box((x - 0.13, y, -0.12), (x + 0.13, y + 0.08, -0.06), BOARD[2])
    return tris


def level(tris):
    """Squares a model that was not modelled square.

    Poly Pizza's models are built by hand and plenty of them sit a few degrees
    off their own axes -- the computer leans seven degrees back and rolls three
    to the side, which on a Besiege block reads as a block somebody knocked.

    Found rather than dialled in: the flat faces of a boxy model all point along
    its own axes, so the strongest area-weighted normal near each axis *is* that
    axis. Two of them make a frame, and the rotation that takes that frame to the
    block's is the correction. Applied only when it is small: a big one means the
    model is not boxy and the guess is wrong.
    """
    weights = {}
    for ps, ns, colour in tris:
        edge1 = [ps[1][k] - ps[0][k] for k in range(3)]
        edge2 = [ps[2][k] - ps[0][k] for k in range(3)]
        face = cross(edge1, edge2)
        area = math.sqrt(sum(c * c for c in face))
        if area <= 1e-9:
            continue
        unit = tuple(c / area for c in face)
        key = tuple(round(c, 2) for c in unit)
        weights[key] = weights.get(key, 0.0) + area

    def strongest(axis, sign):
        best, mass = None, 0.0
        for unit, area in weights.items():
            towards = sum(unit[k] * axis[k] for k in range(3)) * sign
            # Within twenty-five degrees of the axis: a face that belongs to it.
            if towards > 0.9 and area > mass:
                best, mass = unit, area
        return best

    up = strongest((0.0, 0.0, 1.0), 1.0)
    front = strongest((0.0, 1.0, 0.0), -1.0)
    if up is None or front is None:
        return tris, 0.0

    # A frame from the two: z is up, y is the front's opposite, x follows.
    z = normalise(up)
    y = normalise(tuple(-c for c in front))
    x = normalise(cross(y, z))
    y = normalise(cross(z, x))
    tilt = math.degrees(math.acos(max(-1.0, min(1.0, z[2]))))
    if tilt > 20.0:
        return tris, tilt

    def square(v):
        return (sum(v[k] * x[k] for k in range(3)),
                sum(v[k] * y[k] for k in range(3)),
                sum(v[k] * z[k] for k in range(3)))

    return ([([square(p) for p in ps], [square(n) for n in ns], colour)
             for ps, ns, colour in tris], tilt)


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
    scale = CURRENT["span"] / max(hi[k] - lo[k] for k in range(3))
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
        if CURRENT["source"] is None:
            f.write("# Drawn by the tool itself; no model behind it.\n")
        else:
            f.write("# %s by %s, %s, from https://poly.pizza/m/%s\n"
                    % (CURRENT["source"][2], CURRENT["source"][3],
                       CURRENT["source"][4], CURRENT["source"][0]))
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
    span = CURRENT["span"] * 1.25

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
    x, y, z = (math.radians(a) for a in CURRENT["icon"])

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
    path = os.path.join(CACHE, NAME + ".glb")
    if os.path.exists(path):
        return path
    url = "https://static.poly.pizza/%s.glb" % CURRENT["source"][1]
    print("fetching %s from %s" % (CURRENT["source"][2], url))
    try:
        import urllib.request
        os.makedirs(CACHE, exist_ok=True)
        # A plain urlopen is answered with 403: the CDN in front of the models
        # turns away a request with no browser about it.
        ask = urllib.request.Request(url, headers={
            "User-Agent": "Mozilla/5.0 (X11; Linux x86_64)",
            "Referer": "https://poly.pizza/",
        })
        with urllib.request.urlopen(ask, timeout=60) as r:
            open(path, "wb").write(r.read())
    except Exception as e:
        raise SystemExit("could not fetch the model (%s). Download it from\n"
                         "  https://poly.pizza/m/%s\nand save it as %s"
                         % (e, CURRENT["source"][0], path))
    return path


# The four colours Glow.cs paints, in the order the palette lays them out. It
# builds its own two-by-two version of this palette to blink the display, and a
# model change that moved a colour would light the wrong part of the block --
# silently, since nothing else reads those numbers.
GLOW = os.path.join(REPO, "NodeEditor", "NodeEditorScripts", "Glow.cs")
GLOW_NAMES = ("Body", "Buttons", "Display", "Spare")


def check_glow(colours):
    """Holds Glow.cs's colours to the palette written here."""
    try:
        source = open(GLOW).read()
    except IOError:
        return                                  # not this repo's business to make
    for i, name in enumerate(GLOW_NAMES):
        found = re.search(r"%s\s*=\s*Hue\((\d+),\s*(\d+),\s*(\d+)\)" % name, source)
        if found is None:
            raise SystemExit("Glow.cs has no colour called %s" % name)
        want = (tuple(int(round(255 * srgb(v))) for v in colours[i])
                if i < len(colours) else (0, 0, 0))
        got = tuple(int(g) for g in found.groups())
        if got != want:
            raise SystemExit(
                "Glow.cs paints %s as %s, but the palette here writes %s.\n"
                "The model's colours moved; put these numbers in Glow.cs."
                % (name, got, want))
    print("Glow.cs paints the model's own colours.")


def build(name, want_preview):
    """One block: fetch the model, convert it, write what the game loads."""
    global CURRENT, NAME
    NAME = name
    CURRENT = BLOCKS[name]

    if CURRENT["source"] is None:
        tris = board()
    else:
        tris = triangles(fetch())
    tris, tilt = level(tris)
    if tilt > 0.05:
        print("%s: model was %.1f degrees off square; squared" % (name, tilt))
    tris, extent = stand(tris, CURRENT["pose"][0], CURRENT["pose"][1])
    tris = wind(tris)
    colours = sorted(set(c for _, _, c in tris))
    rows, uv = palette(colours)

    # Only the Timer's display is repainted at runtime, so only its palette has
    # to agree with anything.
    if name == "TimerPlus":
        check_glow(colours)

    os.makedirs(OUT, exist_ok=True)
    verts, faces = write_obj(os.path.join(OUT, name + ".obj"), tris, uv)
    write_png(os.path.join(OUT, name + ".png"), rows)
    print("%s: %d verts, %d faces, %d colour(s), %.2f x %.2f x %.2f"
          % (name, verts, faces, len(colours), extent[0], extent[1], extent[2]))

    # The mod's own thumbnail, which Mod.xml names as its <Icon>: one picture for
    # the whole mod, and it is the Timer's. Drawn from the
    # same geometry as everything else, so it cannot show a block the mod does
    # not have -- and drawn every run, so it cannot go stale either.
    if name == "TimerPlus":
        preview(os.path.join(OUT, "Thumbnail.png"), tris, size=512, icon=True)

    if want_preview:
        out = os.path.join(CACHE, "preview")
        os.makedirs(out, exist_ok=True)
        preview(os.path.join(out, name + ".png"), tris)
        preview(os.path.join(out, name + "-icon.png"), tris, icon=True)
        print("previews in %s" % out)


def main():
    want_preview = "--preview" in sys.argv
    wanted = [a for a in sys.argv[1:] if not a.startswith("--")]
    for name in (wanted or sorted(BLOCKS)):
        if name not in BLOCKS:
            raise SystemExit("no block called %s. Have: %s"
                             % (name, ", ".join(sorted(BLOCKS))))
        build(name, want_preview)
    return 0


if __name__ == "__main__":
    main()
