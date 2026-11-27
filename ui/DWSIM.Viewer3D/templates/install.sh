#!/usr/bin/env bash
#
# Puts the size-optimized DWSIM 3D export templates (built by .github/workflows/viewer3d-templates.yml
# with custom.py) over the official ones in a Godot export template folder:
#
#   install.sh <template folder> [tag]
#
# The files come from the assets of the release <tag> of DanWBR/dwsim10 and are checked against its
# SHA256SUMS.txt. If the release cannot be reached, the official templates stay, and the script
# says so, because the export still works with them (only larger).

set -uo pipefail

dest=${1:?template folder}
tag=${2:-viewer3d-templates-4.7.2}
base="https://github.com/DanWBR/dwsim10/releases/download/$tag"
files=(windows_release_x86_64.exe windows_release_arm64.exe linux_release.x86_64 linux_release.arm64 macos.zip)

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

if ! curl -Lfs --retry 3 -o "$tmp/SHA256SUMS.txt" "$base/SHA256SUMS.txt"; then
    echo "::warning::DWSIM 3D templates ($tag) not found; exporting with the official Godot templates"
    exit 0
fi
for f in "${files[@]}"; do
    if ! curl -Lfs --retry 3 -o "$tmp/$f" "$base/$f"; then
        echo "::warning::DWSIM 3D template $f ($tag) could not be downloaded; exporting with the official Godot templates"
        exit 0
    fi
done
names=$(IFS='|'; echo "${files[*]}")
(cd "$tmp" && grep -E " \*?($names)\$" SHA256SUMS.txt | sha256sum -c -) || { echo "checksum mismatch in the DWSIM 3D templates" >&2; exit 1; }

for f in "${files[@]}"; do
    cp "$tmp/$f" "$dest/$f"
done
chmod +x "$dest"/linux_release.* || true
echo "DWSIM 3D templates ($tag) installed in $dest"
