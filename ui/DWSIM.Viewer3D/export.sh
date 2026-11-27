#!/usr/bin/env bash
#
# Exports the DWSIM 3D window for one or more DWSIM runtime ids into <out>/<rid>/viewer3d, the
# folder DWSIM looks in (see ViewerLauncher.cs):
#
#   export.sh <godot> <out> [rid ...]     rids: win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64
#
# <godot> is a Godot 4.7 editor (the Linux one runs with --headless) with the export templates of
# the same version installed. Both macOS ids get the same universal app.

set -euo pipefail

godot=${1:?godot executable}
out=${2:?output folder}
shift 2
rids=("$@")
[ ${#rids[@]} -gt 0 ] || rids=(win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64)

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
mkdir -p "$out"
out=$(cd "$out" && pwd)

# the first run imports the resources; an export before that misses them
"$godot" --headless --path "$here" --import >/dev/null 2>&1 || true

mac_done=""
for rid in "${rids[@]}"; do
    dest="$out/$rid/viewer3d"
    rm -rf "$dest"
    mkdir -p "$dest"
    case "$rid" in
        osx-*)
            if [ -z "$mac_done" ]; then
                mkdir -p "$here/export/macos"
                "$godot" --headless --path "$here" --export-release macos "$here/export/macos/DWSIM 3D.zip"
                mac_done=1
            fi
            unzip -qo "$here/export/macos/DWSIM 3D.zip" -d "$dest"
            chmod +x "$dest/DWSIM 3D.app/Contents/MacOS/"* || true
            ;;
        win-*)
            "$godot" --headless --path "$here" --export-release "$rid" "$dest/DWSIM3D.exe"
            ;;
        linux-x64)
            "$godot" --headless --path "$here" --export-release "$rid" "$dest/DWSIM3D.x86_64"
            chmod +x "$dest/DWSIM3D.x86_64"
            ;;
        linux-arm64)
            "$godot" --headless --path "$here" --export-release "$rid" "$dest/DWSIM3D.arm64"
            chmod +x "$dest/DWSIM3D.arm64"
            ;;
        *)
            echo "unknown runtime id $rid" >&2
            exit 1
            ;;
    esac
    cp "$here/LICENSE-NOTICE.txt" "$here/licenses/GODOT-LICENSE.txt" "$here/licenses/GODOT-COPYRIGHT.txt" "$dest/"
    echo "$rid: $(du -sh "$dest" | cut -f1)"
done
