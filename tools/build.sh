#!/usr/bin/env bash
#
# Compiles the mod into NodeEditor.dll, using Besiege's OWN C# compiler rather
# than an installed toolchain.
#
#   ./tools/build.sh            build the mod's assembly
#   ./tools/build.sh --check    compile to a temp file only, do not install
#
# Mod.xml loads NodeEditor.dll directly, so the mod ships as a prebuilt assembly
# rather than a <ScriptAssembly> the game compiles at load. It has to: a
# ScriptAssembly is compiled during the mod LOAD phase, before any other mod's
# assembly is in the AppDomain, so it cannot reference UI Factory at all. It is
# also compiled once ever and never invalidated, so editing the sources would
# leave the game running the first build, silently.
#
# No C# toolchain is needed. This loads the game's own libmono.so and calls
# Mono.CSharp.CompilerCallableEntryPoint.InvokeCompiler in its mcs.dll, which is
# the exact code path the game uses. gcc is needed once, to build the host.
#
# Set BESIEGE_DIR if the install is not auto-detected.

set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC_DIR="$REPO_DIR/NodeEditor/NodeEditorScripts"
BUILD_DIR="${TMPDIR:-/tmp}/besiege-nodeeditor-build"
OUT="$REPO_DIR/NodeEditor/NodeEditor.dll"

CHECK_ONLY=0
if [[ "${1:-}" == "--check" ]]; then
    CHECK_ONLY=1
fi

# Always compile to a scratch file and move it into place afterwards. Writing
# straight to the shipped path means a failed compile can leave a truncated
# assembly behind, and -- if Besiege is open with the mod loaded -- the target
# may be mapped by the running game and refuse to be overwritten. A rename
# replaces the directory entry instead, which always works.
#
# The scratch directory carries the pid so two builds cannot collide; the file
# inside keeps the assembly's own name, which is what an assembly is identified
# by once it is loaded.
mkdir -p "$BUILD_DIR/tmp.$$"
TMP_OUT="$BUILD_DIR/tmp.$$/NodeEditor.dll"
trap 'rm -rf "$BUILD_DIR/tmp.$$"' EXIT

find_besiege() {
    if [[ -n "${BESIEGE_DIR:-}" ]]; then echo "$BESIEGE_DIR"; return; fi
    local candidates=(
        "$HOME/.steam/steam/steamapps/common/Besiege"
        "$HOME/.local/share/Steam/steamapps/common/Besiege"
    )
    local vdf
    for vdf in "$HOME/.steam/steam/steamapps/libraryfolders.vdf" \
               "$HOME/.local/share/Steam/steamapps/libraryfolders.vdf"; do
        [[ -f "$vdf" ]] || continue
        while read -r lib; do candidates+=("$lib/steamapps/common/Besiege"); done \
            < <(grep -oE '"path"[[:space:]]+"[^"]+"' "$vdf" | sed -E 's/.*"([^"]+)"$/\1/')
    done
    local dir
    for dir in "${candidates[@]}"; do
        [[ -f "$dir/Besiege_Data/Managed/mcs.dll" ]] && { echo "$dir"; return; }
    done
    return 1
}

if ! BESIEGE="$(find_besiege)"; then
    echo "Could not find Besiege. Set BESIEGE_DIR to your install directory." >&2
    exit 1
fi

DATA="$BESIEGE/Besiege_Data"
export LIBMONO="$DATA/Mono/x86_64/libmono.so"
export MANAGED="$DATA/Managed"
export MONOETC="$DATA/Mono/etc"

# UI Factory is needed to *build*, but not to run: the panel is a soft dependency
# and the block falls back to Besiege's own mapper when it is absent. The
# compiler still has to resolve the types, so its assemblies go on the reference
# path -- see NodeEditor/NodeEditorScripts/UIF.cs for how the fallback is arranged.
find_uifactory() {
    if [[ -n "${UIFACTORY_DIR:-}" ]]; then echo "$UIFACTORY_DIR"; return; fi
    local roots=("$BESIEGE/../../workshop/content/346010/2913469777"
                 "$BESIEGE/Besiege_Data/Mods/UIFactory")
    local root hit
    for root in "${roots[@]}"; do
        hit="$(find "$root" -name Besiege.UI.dll -print -quit 2>/dev/null || true)"
        [[ -n "$hit" ]] && { dirname "$hit"; return; }
    done
    return 1
}

if ! UIFACTORY="$(find_uifactory)"; then
    cat >&2 <<'NOUIF'
Could not find UI Factory 3 (Besiege.UI.dll), which is needed to compile.

The mod does not need it to run -- without it the block uses Besiege's own block
mapper and the table simply does not appear -- but the panel's source names its
types, so the compiler has to be able to resolve them.

Subscribe to Workshop item 2913469777 ("UI Factory"), or set UIFACTORY_DIR to
the folder holding Besiege.UI.dll.
NOUIF
    exit 1
fi

echo "Besiege:    $BESIEGE"
echo "UI Factory: $UIFACTORY"

HOST="$BUILD_DIR/besiegecc"
for tool in besiegecc monohost; do
    if [[ ! -x "$BUILD_DIR/$tool" || "$REPO_DIR/tools/$tool.c" -nt "$BUILD_DIR/$tool" ]]; then
        echo "Building $tool host..."
        gcc -O1 -o "$BUILD_DIR/$tool" "$REPO_DIR/tools/$tool.c" -ldl
    fi
done

if pgrep -x Besiege >/dev/null 2>&1 || pgrep -f 'Besiege\.x86' >/dev/null 2>&1; then
    echo "Note: Besiege appears to be running. The build itself is fine, but the"
    echo "      game will not pick up the new assembly until you restart it."
fi

# Besiege does not report an XML it cannot parse: the block is simply not in the
# toolbar, which looks like a dozen other faults. Catch it here, before the build
# has a chance to look like it worked.
XMLCHECK="$BUILD_DIR/xmlcheck.exe"
if [[ ! -f "$XMLCHECK" || "$REPO_DIR/tools/tests/XmlCheck.cs" -nt "$XMLCHECK" ]]; then
    rm -f "$XMLCHECK"
    if ! "$HOST" -target:exe -out:"$XMLCHECK" -lib:"$MANAGED" -r:System.dll -r:System.Xml.dll \
            "$REPO_DIR/tools/tests/XmlCheck.cs"; then
        echo "The XML checker itself failed to compile (above)." >&2
        exit 1
    fi
fi
set +e
TARGET_ASM="$XMLCHECK" "$BUILD_DIR/monohost" "$REPO_DIR/NodeEditor"/*.xml \
    "$SRC_DIR/TimerPlusModule.cs" "$SRC_DIR/NodeEditorModule.cs" \
    "$REPO_DIR/tools/make-block-mesh.py"
xml_rc=$?
set -e
if [[ $xml_rc -ne 0 ]]; then
    cat >&2 <<'BADXML'

Besiege would not show the block at all.
If it is a parse error: an XML comment may not contain two hyphens in a row,
which prose written with a dash produces easily.
If an element is missing: a block needs BasePoint, Colliders and AddingPoints as
well as the obvious ones. Copy the geometry from a block that works rather than
writing it out.
BADXML
    exit 1
fi

# System.Xml is referenced for the [Xml*] attributes on the block module. That is
# not a blacklist violation: the loader's AssemblyScanner walks field types,
# locals and IL operands, and never enumerates custom attributes -- and the
# module system has no other way to name the XML elements it deserialises.
echo "Compiling $(ls "$SRC_DIR"/*.cs | wc -l) source files with Besiege's compiler..."
set +e
"$HOST" -target:library -out:"$TMP_OUT" \
    -lib:"$MANAGED" -lib:"$UIFACTORY" \
    -r:UnityEngine.dll -r:UnityEngine.UI.dll \
    -r:Assembly-CSharp.dll -r:Assembly-CSharp-firstpass.dll \
    -r:System.dll -r:System.Core.dll -r:System.Xml.dll \
    -r:Besiege.UI.dll -r:Besiege.UI.Bridge.dll \
    "$SRC_DIR"/*.cs
rc=$?
set -e

if [[ $rc -ne 0 ]]; then
    cat >&2 <<'EOF'

Build FAILED. The previously built assembly, if any, was left untouched.

If the output above is a list of CS#### errors, that is an ordinary compile
error -- fix the source. Otherwise:

  "the compiler threw a managed exception"
      The exception text is printed above it. Compiling holds the game's whole
      assembly set in memory, so if it is an OutOfMemoryException, close Besiege
      (or anything else large) and try again.

  a SIGSEGV inside Mono.CSharp
      A construct this ancient compiler cannot handle. The known case is any
      `enum` declaration; use int constants instead.
EOF
    exit $rc
fi

# Compiling cleanly says nothing about whether the mod loader will accept the
# result: it also scans for blacklisted namespaces and refuses the whole assembly
# over a single reference. Catch that here, not at launch.
BLACKLIST="$BUILD_DIR/blacklist.exe"
if [[ ! -f "$BLACKLIST" || "$REPO_DIR/tools/tests/BlacklistCheck.cs" -nt "$BLACKLIST" ]]; then
    rm -f "$BLACKLIST"
    if ! "$HOST" -target:exe -out:"$BLACKLIST" -lib:"$MANAGED" -r:System.dll \
            "$REPO_DIR/tools/tests/BlacklistCheck.cs"; then
        echo "The blacklist checker itself failed to compile (above)." >&2
        exit 1
    fi
fi
set +e
TARGET_ASM="$BLACKLIST" "$BUILD_DIR/monohost" "$TMP_OUT" \
    "$UIFACTORY/Besiege.UI.dll" "$UIFACTORY/Besiege.UI.Bridge.dll"
scan_rc=$?
set -e
if [[ $scan_rc -ne 0 ]]; then
    echo >&2
    echo >&2 "Refusing to install an assembly Besiege would reject."
    exit 1
fi

# The two parts of this mod that touch no Unity object -- the timer's own state
# machine and the table's ordering -- are exercised here, because they are the
# parts whose failure is silent in game. A row that fires a tick late, or loses
# one every time it loops, drifts away from the real timer beside it without
# throwing or logging anything; so does a sort that scrambles its ties.
TABLECHECK="$BUILD_DIR/tmp.$$/tablecheck.exe"
set +e
"$HOST" -target:exe -out:"$TABLECHECK" -lib:"$MANAGED" -lib:"$BUILD_DIR/tmp.$$" \
    -r:System.dll -r:UnityEngine.dll -r:Assembly-CSharp.dll -r:NodeEditor.dll \
    "$REPO_DIR/tools/tests/TableCheck.cs" >/dev/null
table_rc=$?
set -e
if [[ $table_rc -eq 0 ]]; then
    set +e
    TARGET_ASM="$TABLECHECK" "$BUILD_DIR/monohost"
    table_rc=$?
    set -e
    if [[ $table_rc -ne 0 ]]; then
        echo >&2
        echo >&2 "The timer does not keep time, or the table does not sort, the"
        echo >&2 "way the game's own timer block does."
        exit 1
    fi
else
    echo "(table check would not compile; skipping it)" >&2
fi

if [[ $CHECK_ONLY -eq 1 ]]; then
    echo "Build OK (check only; $(stat -c%s "$TMP_OUT") bytes, not installed)"
else
    mv -f "$TMP_OUT" "$OUT"
    echo "Build OK -> $OUT"
fi
