#!/usr/bin/env bash
#
# Signs, notarises and staples DWSIM.app, then wraps it in a disk image. Needs macOS and a
# Developer ID Application certificate already in the keychain.
#
#   sign-and-notarize.sh <app-bundle> <version> <output-dir>
#
# Reads from the environment:
#
#   MACOS_SIGN_IDENTITY   the certificate, e.g. "Developer ID Application: Name (TEAMID)"
#   MACOS_NOTARY_PROFILE  a notarytool keychain profile, or
#   MACOS_NOTARY_KEY      path to the App Store Connect .p8 private key
#   MACOS_NOTARY_KEY_ID   its key id
#   MACOS_NOTARY_ISSUER   the issuer id of the App Store Connect account
#
# Without MACOS_SIGN_IDENTITY the disk image is still built, unsigned: Gatekeeper will refuse it
# on a machine that downloaded it, which is the honest outcome of not having a certificate.

set -euo pipefail

app=${1:?app bundle}
version=${2:?version}
output=${3:?output directory}
# The architecture the payload is for. It is not $(uname -m): the runner is Apple Silicon and
# builds both the x64 and the arm64 image, so the target has to be told, not read off the host.
arch=${4:-$(uname -m)}

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

mkdir -p "$output"
dmg=$output/DWSIM-$version-$arch.dmg

if [ -n "${MACOS_SIGN_IDENTITY:-}" ]; then

    # Everything codesign counts as code is signed before the bundle itself, deepest first, which
    # is the order codesign requires: signing the bundle does not reach what it carries. That is
    # not only the native Mach-O libraries (*.dylib, *.so, and executables with no extension like
    # createdump) but also the managed assemblies (*.dll): a self-contained .NET bundle carries
    # both, and an unsigned Microsoft.CSharp.dll makes the apphost signature fail with "code object
    # is not signed at all". The apphost is skipped here and signed on its own, with entitlements.
    find "$app/Contents/MacOS" -type f ! -name 'DWSIM.UI.Desktop.Avalonia' -print0 |
        while IFS= read -r -d '' lib; do
            case "$lib" in
                *.dll) : ;;
                *) file -b "$lib" | grep -q 'Mach-O' || continue ;;
            esac
            # The bundled AI assistant is a PyInstaller onefile: under the hardened
            # runtime it unpacks and dlopens its own libraries at run time, so it
            # needs the same entitlements as the apphost or library validation
            # kills it on launch. It sits in a nonstandard location that the
            # bundle's --deep pass does not reach, so the signature (and its
            # entitlements) applied here is the one that ships.
            case "$lib" in
                */extenders/AIAssistantFiles/dwsim-assistant)
                    codesign --force --timestamp --options runtime \
                             --entitlements "$here/entitlements.plist" \
                             --sign "$MACOS_SIGN_IDENTITY" "$lib" ;;
                *)
                    codesign --force --timestamp --options runtime \
                             --sign "$MACOS_SIGN_IDENTITY" "$lib" ;;
            esac
        done

    # The bundle is signed in one pass with --deep. The apphost shares its base name with data
    # files next to it (DWSIM.UI.Desktop.Avalonia.dll, .runtimeconfig.json, .deps.json), which
    # makes codesign read the executable and its siblings as a loose bundle and demand the .json be
    # signed code; --deep lets it seal those subcomponents. --entitlements still applies only to
    # the main executable, which is the one that needs them.
    codesign --force --deep --timestamp --options runtime \
             --entitlements "$here/entitlements.plist" \
             --sign "$MACOS_SIGN_IDENTITY" "$app"

    codesign --verify --deep --strict --verbose=2 "$app"

else
    echo "MACOS_SIGN_IDENTITY is not set: building an unsigned disk image" >&2
fi

# hdiutil intermittently fails with "Resource busy" on the runner when a device from a previous
# attempt lingers; a short retry clears it.
for attempt in 1 2 3; do
    if hdiutil create -volname "DWSIM $version" -srcfolder "$app" -ov -format UDZO "$dmg"; then
        break
    fi
    if [ "$attempt" = 3 ]; then
        echo "hdiutil create failed after $attempt attempts" >&2
        exit 1
    fi
    echo "hdiutil create failed, retrying ($attempt)" >&2
    sleep 5
done

if [ -n "${MACOS_SIGN_IDENTITY:-}" ]; then

    codesign --force --timestamp --sign "$MACOS_SIGN_IDENTITY" "$dmg"

    if [ -n "${MACOS_NOTARY_PROFILE:-}" ]; then
        xcrun notarytool submit "$dmg" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
        xcrun stapler staple "$dmg"
    elif [ -n "${MACOS_NOTARY_KEY:-}" ]; then
        xcrun notarytool submit "$dmg" \
              --key "$MACOS_NOTARY_KEY" \
              --key-id "$MACOS_NOTARY_KEY_ID" \
              --issuer "$MACOS_NOTARY_ISSUER" --wait
        xcrun stapler staple "$dmg"
    else
        echo "no notarytool credentials: the disk image is signed but not notarised" >&2
    fi

fi

echo "$dmg"
