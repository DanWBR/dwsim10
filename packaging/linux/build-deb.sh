#!/usr/bin/env bash
#
# Builds a .deb around a self-contained publish of the desktop application.
#
#   build-deb.sh <publish-dir> <version> <debian-arch> <output-dir>
#
# The Debian architecture is amd64 or arm64. Nothing here runs the payload, so the package for
# either architecture can be built on any machine that has dpkg-deb.

set -euo pipefail

publish=${1:?publish directory}
version=${2:?version}
arch=${3:?debian architecture}
output=${4:?output directory}

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
assets=$here/../assets

staging=$(mktemp -d)
trap 'rm -rf "$staging"' EXIT

# mktemp gives the directory to its owner alone, and dpkg carries that mode into the package
chmod 0755 "$staging"

install -d "$staging/DEBIAN"
install -d "$staging/opt/dwsim"
install -d "$staging/usr/bin"
install -d "$staging/usr/share/applications"
install -d "$staging/usr/share/mime/packages"

cp -a "$publish/." "$staging/opt/dwsim/"
chmod +x "$staging/opt/dwsim/DWSIM.UI.Desktop.Avalonia"

# the launcher, so that the working directory does not decide whether the app finds its own files
cat > "$staging/usr/bin/dwsim" <<'LAUNCHER'
#!/bin/sh
exec /opt/dwsim/DWSIM.UI.Desktop.Avalonia "$@"
LAUNCHER
chmod +x "$staging/usr/bin/dwsim"

for size in 16 32 64 128 256 512; do
    install -d "$staging/usr/share/icons/hicolor/${size}x${size}/apps"
    cp "$assets/dwsim-$size.png" "$staging/usr/share/icons/hicolor/${size}x${size}/apps/dwsim.png"
done

cp "$here/dwsim.desktop" "$staging/usr/share/applications/dwsim.desktop"
cp "$here/dwsim-mime.xml" "$staging/usr/share/mime/packages/dwsim.xml"

size_kb=$(du -sk "$staging" | cut -f1)

sed -e "s/@VERSION@/$version/" \
    -e "s/@ARCH@/$arch/" \
    -e "s/@SIZE@/$size_kb/" \
    "$here/control.in" > "$staging/DEBIAN/control"

# libfontconfig and libice are what SkiaSharp and Avalonia's X11 backend reach for; the rest of
# the runtime is inside the package
cat > "$staging/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v update-mime-database >/dev/null 2>&1; then
    update-mime-database /usr/share/mime || true
fi
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database /usr/share/applications || true
fi
POSTINST
chmod +x "$staging/DEBIAN/postinst"

cp "$staging/DEBIAN/postinst" "$staging/DEBIAN/postrm"

mkdir -p "$output"
dpkg-deb --root-owner-group --build "$staging" "$output/dwsim_${version}_${arch}.deb"

echo "$output/dwsim_${version}_${arch}.deb"
