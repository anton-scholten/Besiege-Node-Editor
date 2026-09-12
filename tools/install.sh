#!/usr/bin/env bash
#
# Installs the mod into Besiege.
#
#   ./tools/install.sh              build, then symlink the mod (best for
#                                   development -- a rebuild is picked up by the
#                                   next game start, with no reinstall)
#   ./tools/install.sh --copy       build, then copy instead (for handing someone
#                                   a folder, or if symlinks are awkward)
#   ./tools/install.sh --uninstall  remove it again
#   ./tools/install.sh --no-build   skip the build step
#
# Besiege reads mods once at startup, so restart the game afterwards.
# Set BESIEGE_DIR if the install is not auto-detected.
#
# The folder Besiege loads is NodeEditor/, not the repository root: that subfolder
# is the whole of what gets uploaded to the Workshop, and everything beside it --
# sources, tools, docs -- is not part of the mod.

set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MOD_NAME="NodeEditor"
SRC="$REPO_DIR/$MOD_NAME"

MODE="link"
BUILD=1
for arg in "$@"; do
    case "$arg" in
        --uninstall) MODE="uninstall"; BUILD=0 ;;
        --copy)      MODE="copy" ;;
        --no-build)  BUILD=0 ;;
        *) echo "Unknown option: $arg" >&2; exit 1 ;;
    esac
done

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
        [[ -d "$dir/Besiege_Data" ]] && { echo "$dir"; return; }
    done
    return 1
}

if ! BESIEGE="$(find_besiege)"; then
    echo "Could not find Besiege. Set BESIEGE_DIR to your install directory." >&2
    exit 1
fi

MODS="$BESIEGE/Besiege_Data/Mods"
DEST="$MODS/$MOD_NAME"

# The mod folder used to be TimerPlus/. A symlink left under that name points at
# a folder that is gone, and a copy left under it is a second mod with the same
# <ID>; take away the link, and name the copy rather than delete it unasked.
OLD="$MODS/TimerPlus"
if [[ -L "$OLD" ]]; then
    rm "$OLD"
    echo "Removed the old symlink $OLD"
elif [[ -d "$OLD" ]]; then
    echo "Note: $OLD is this mod under its old name; delete it." >&2
fi

if [[ "$MODE" == "uninstall" ]]; then
    if [[ -L "$DEST" ]]; then
        rm "$DEST"
        echo "Removed symlink $DEST"
    elif [[ -d "$DEST" ]]; then
        rm -rf "$DEST"
        echo "Removed $DEST"
    else
        echo "Nothing installed at $DEST"
    fi
    exit 0
fi

if [[ $BUILD -eq 1 ]]; then
    "$REPO_DIR/tools/build.sh"
fi

if [[ ! -f "$SRC/NodeEditor.dll" ]]; then
    echo "NodeEditor/NodeEditor.dll is missing; run ./tools/build.sh first." >&2
    exit 1
fi

mkdir -p "$MODS"
# Replace whatever is there, whichever kind it is, then install.
[[ -L "$DEST" ]] && rm "$DEST"
[[ -d "$DEST" ]] && rm -rf "$DEST"

if [[ "$MODE" == "copy" ]]; then
    cp -r "$SRC" "$DEST"
    # The sources are not part of the mod: Besiege reads only what Mod.xml names.
    rm -rf "$DEST/NodeEditorScripts"
    echo "Copied mod to $DEST"
else
    ln -s "$SRC" "$DEST"
    echo "Linked $DEST -> $SRC"
fi

if pgrep -x Besiege >/dev/null 2>&1 || pgrep -f 'Besiege\.x86' >/dev/null 2>&1; then
    echo "Besiege is running; restart it to pick this up."
fi

if ! grep -q '<ID>' "$SRC/Mod.xml"; then
    cat <<'EOF'

Note: Mod.xml has no <ID> yet. The game writes one the first time it loads the
mod, and with a symlink that write lands in your working copy, which is what you
want -- the ID must stay stable for the life of the mod, so commit it once it is
there. Saved machines refer to this block by it.
EOF
fi

if [[ ! -d "$BESIEGE/../../workshop/content/346010/2913469777" \
      && ! -d "$MODS/UIFactory" ]]; then
    cat <<'EOF'

Note: UI Factory does not look installed. The block still works -- its settings
appear in Besiege's own block mapper -- but the table, the sorting and the
convert button are the panel, and the panel needs it. Workshop item 2913469777.
EOF
fi
