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

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

mkdir -p "$output"
dmg=$output/DWSIM-$version-$(uname -m).dmg

if [ -n "${MACOS_SIGN_IDENTITY:-}" ]; then

    # Every Mach-O inside the bundle is signed before the bundle itself, deepest first, which is
    # the order codesign requires: signing the bundle does not reach the libraries it carries.
    find "$app/Contents/MacOS" \( -name '*.dylib' -o -name '*.so' \) -print0 |
        while IFS= read -r -d '' lib; do
            codesign --force --timestamp --options runtime \
                     --sign "$MACOS_SIGN_IDENTITY" "$lib"
        done

    codesign --force --timestamp --options runtime \
             --entitlements "$here/entitlements.plist" \
             --sign "$MACOS_SIGN_IDENTITY" \
             "$app/Contents/MacOS/DWSIM.UI.Desktop.Avalonia"

    codesign --force --timestamp --options runtime \
             --entitlements "$here/entitlements.plist" \
             --sign "$MACOS_SIGN_IDENTITY" "$app"

    codesign --verify --deep --strict --verbose=2 "$app"

else
    echo "MACOS_SIGN_IDENTITY is not set: building an unsigned disk image" >&2
fi

hdiutil create -volname "DWSIM $version" -srcfolder "$app" -ov -format UDZO "$dmg"

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
