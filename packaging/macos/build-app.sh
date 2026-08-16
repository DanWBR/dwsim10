#!/usr/bin/env bash
#
# Assembles DWSIM.app around a self-contained publish of the desktop application.
#
#   build-app.sh <publish-dir> <version> <output-dir>
#
# Only the bundle: signing and notarisation are sign-and-notarize.sh, because they need a
# certificate and a macOS host, and this does not.

set -euo pipefail

publish=${1:?publish directory}
version=${2:?version}
output=${3:?output directory}

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
assets=$here/../assets

app=$output/DWSIM.app

rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

cp -a "$publish/." "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/DWSIM.UI.Desktop.Avalonia"

# The AI assistant helper is a native executable nested under extenders/. The GitHub Actions
# artifact round-trip that carried the payload here dropped its executable bit (upload-artifact
# stores 0644), and codesign does not restore it, so the shipped app cannot launch it. The bit is
# not part of the signature, so setting it here, before signing, is safe.
assistant="$app/Contents/MacOS/extenders/AIAssistantFiles/dwsim-assistant"
[ -f "$assistant" ] && chmod +x "$assistant"

# Debug symbols do not belong in a shipped app, and codesign seals the MacOS folder and then
# trips over each .pdb as an unsigned subcomponent ("code object is not signed at all"). Drop them.
find "$app/Contents/MacOS" -name '*.pdb' -delete

sed -e "s/@VERSION@/$version/" "$here/Info.plist.in" > "$app/Contents/Info.plist"

# iconutil is the only piece that needs macOS; elsewhere the bundle simply carries the PNGs
if command -v iconutil >/dev/null 2>&1; then
    iconset=$(mktemp -d)/dwsim.iconset
    mkdir -p "$iconset"
    cp "$assets/dwsim-16.png"  "$iconset/icon_16x16.png"
    cp "$assets/dwsim-32.png"  "$iconset/icon_16x16@2x.png"
    cp "$assets/dwsim-32.png"  "$iconset/icon_32x32.png"
    cp "$assets/dwsim-64.png"  "$iconset/icon_32x32@2x.png"
    cp "$assets/dwsim-128.png" "$iconset/icon_128x128.png"
    cp "$assets/dwsim-256.png" "$iconset/icon_128x128@2x.png"
    cp "$assets/dwsim-256.png" "$iconset/icon_256x256.png"
    cp "$assets/dwsim-512.png" "$iconset/icon_256x256@2x.png"
    cp "$assets/dwsim-512.png" "$iconset/icon_512x512.png"
    iconutil -c icns "$iconset" -o "$app/Contents/Resources/dwsim.icns"
    rm -rf "$(dirname "$iconset")"
else
    echo "iconutil is not here, so the bundle carries the PNGs and no .icns" >&2
    cp "$assets/dwsim-512.png" "$app/Contents/Resources/dwsim.png"
fi

echo "$app"
