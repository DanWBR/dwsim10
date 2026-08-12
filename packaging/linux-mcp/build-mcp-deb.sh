#!/usr/bin/env bash
#
# Builds a .deb around a self-contained publish of the headless MCP server.
# The package installs a systemd service (dwsim-mcp) that listens on a TCP port.
#
#   build-mcp-deb.sh <publish-dir> <version> <debian-arch> <output-dir>
#
# The Debian architecture is amd64 or arm64. Nothing here runs the payload, so the package for
# either architecture can be built on any machine that has dpkg-deb.

set -euo pipefail

publish=${1:?publish directory}
version=${2:?version}
arch=${3:?debian architecture}
output=${4:?output directory}

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

staging=$(mktemp -d)
trap 'rm -rf "$staging"' EXIT

# mktemp gives the directory to its owner alone, and dpkg carries that mode into the package
chmod 0755 "$staging"

install -d "$staging/DEBIAN"
install -d "$staging/opt/dwsim-mcp"
install -d "$staging/usr/bin"
install -d "$staging/etc/dwsim-mcp"
install -d "$staging/lib/systemd/system"

cp -a "$publish/." "$staging/opt/dwsim-mcp/"
chmod +x "$staging/opt/dwsim-mcp/dwsim-mcp"

# a launcher so the binary can be run by hand regardless of the working directory
cat > "$staging/usr/bin/dwsim-mcp" <<'LAUNCHER'
#!/bin/sh
exec /opt/dwsim-mcp/dwsim-mcp "$@"
LAUNCHER
chmod +x "$staging/usr/bin/dwsim-mcp"

cp "$here/dwsim-mcp.service" "$staging/lib/systemd/system/dwsim-mcp.service"
cp "$here/dwsim-mcp.conf" "$staging/etc/dwsim-mcp/dwsim-mcp.conf"

# preserve the admin's edits to the config across upgrades
echo "/etc/dwsim-mcp/dwsim-mcp.conf" > "$staging/DEBIAN/conffiles"

size_kb=$(du -sk "$staging" | cut -f1)

sed -e "s/@VERSION@/$version/" \
    -e "s/@ARCH@/$arch/" \
    -e "s/@SIZE@/$size_kb/" \
    "$here/control.in" > "$staging/DEBIAN/control"

cat > "$staging/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
# dedicated unprivileged system user for the service
if ! getent passwd dwsim-mcp >/dev/null 2>&1; then
    adduser --system --group --no-create-home --home /var/lib/dwsim-mcp \
            --shell /usr/sbin/nologin dwsim-mcp >/dev/null 2>&1 \
        || useradd --system --user-group --home-dir /var/lib/dwsim-mcp \
            --shell /usr/sbin/nologin dwsim-mcp >/dev/null 2>&1 \
        || true
fi
chmod +x /opt/dwsim-mcp/dwsim-mcp || true
if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload || true
    systemctl enable dwsim-mcp.service >/dev/null 2>&1 || true
    systemctl restart dwsim-mcp.service || true
fi
exit 0
POSTINST
chmod +x "$staging/DEBIAN/postinst"

cat > "$staging/DEBIAN/prerm" <<'PRERM'
#!/bin/sh
set -e
if command -v systemctl >/dev/null 2>&1; then
    systemctl stop dwsim-mcp.service || true
    systemctl disable dwsim-mcp.service >/dev/null 2>&1 || true
fi
exit 0
PRERM
chmod +x "$staging/DEBIAN/prerm"

cat > "$staging/DEBIAN/postrm" <<'POSTRM'
#!/bin/sh
set -e
if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload || true
fi
if [ "$1" = "purge" ]; then
    deluser --system dwsim-mcp >/dev/null 2>&1 || userdel dwsim-mcp >/dev/null 2>&1 || true
    rm -rf /var/lib/dwsim-mcp
fi
exit 0
POSTRM
chmod +x "$staging/DEBIAN/postrm"

mkdir -p "$output"
dpkg-deb --root-owner-group --build "$staging" "$output/dwsim-mcp_${version}_${arch}.deb"

echo "$output/dwsim-mcp_${version}_${arch}.deb"
