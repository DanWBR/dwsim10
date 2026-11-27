#!/usr/bin/env bash
#
# Assembles DWSIM.app around a self-contained publish of the desktop application.
#
#   build-app.sh <publish-dir> <version> <output-dir> [arch]
#
# arch (x64 or arm64) thins the DWSIM 3D window, which Godot exports as a universal app, to the
# architecture of this disk image.
#
# Only the bundle: signing and notarisation are sign-and-notarize.sh, because they need a
# certificate and a macOS host, and this does not.

set -euo pipefail

publish=${1:?publish directory}
version=${2:?version}
output=${3:?output directory}
arch=${4:-}

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

# The DWSIM 3D window is an app of its own (Godot). Nested code belongs in a standard place of the
# bundle, so it moves from the publish folder to Contents/Helpers, where the launcher looks for it
# (ViewerLauncher.cs); the artifact round trip dropped its executable bit too.
if [ -d "$app/Contents/MacOS/viewer3d/DWSIM 3D.app" ]; then
    mkdir -p "$app/Contents/Helpers"
    mv "$app/Contents/MacOS/viewer3d/DWSIM 3D.app" "$app/Contents/Helpers/"
    mv "$app/Contents/MacOS/viewer3d/"*.txt "$app/Contents/Helpers/DWSIM 3D.app/Contents/Resources/" 2>/dev/null || true
    rm -rf "$app/Contents/MacOS/viewer3d"
    chmod +x "$app/Contents/Helpers/DWSIM 3D.app/Contents/MacOS/"*
    if [ -n "$arch" ] && command -v lipo >/dev/null 2>&1; then
        case "$arch" in x64) slice=x86_64 ;; *) slice=$arch ;; esac
        for bin in "$app/Contents/Helpers/DWSIM 3D.app/Contents/MacOS/"*; do
            lipo "$bin" -thin "$slice" -output "$bin.thin" && mv "$bin.thin" "$bin"
        done
        # thinning breaks Godot's ad hoc signature, and Apple silicon runs nothing unsigned;
        # sign-and-notarize.sh signs it again with the certificate when there is one
        codesign --force --deep --sign - "$app/Contents/Helpers/DWSIM 3D.app"
    fi
fi

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
