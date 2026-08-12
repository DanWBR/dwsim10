# DWSIM headless MCP server (.deb)

Packages the [MCP server](../../tools/DWSIM.MCPServer) as a Debian package that installs a
systemd service listening on a TCP port, so DWSIM can run on a mini PC or SBC and answer
Model Context Protocol calls over the network.

## Contents of the package

| Path | Purpose |
|------|---------|
| `/opt/dwsim-mcp/` | the self-contained publish (the .NET runtime is bundled) |
| `/usr/bin/dwsim-mcp` | launcher, to run the server by hand |
| `/lib/systemd/system/dwsim-mcp.service` | the service unit |
| `/etc/dwsim-mcp/dwsim-mcp.conf` | options (bind address, port, token) - a conffile, kept across upgrades |

The service runs as the unprivileged `dwsim-mcp` system user created on install, and is enabled
and started automatically.

## Install and use

```sh
sudo apt install ./dwsim-mcp_<version>_<arch>.deb
# edit /etc/dwsim-mcp/dwsim-mcp.conf to set the port/token, then
sudo systemctl restart dwsim-mcp
curl http://<host>:5901/health
```

By default it listens on every interface on port 5901 with no token. On an untrusted network,
add `--token <secret>` to `DWSIM_MCP_OPTS` in the conf file.

Endpoints: `POST /mcp` (JSON-RPC), `GET /sse` (server-sent events), `GET /health`.

DWSIM's engine loads ICU for globalization. Standard Debian, Ubuntu and Raspberry Pi OS images
already carry it; a stripped-down image may not, in which case install the ICU runtime for the
release (for example `sudo apt install libicu-dev`, which pulls the matching `libicuNN`).

## Building the package

```sh
dotnet publish tools/DWSIM.MCPServer/DWSIM.MCPServer.csproj \
  -c Release -r linux-x64 --self-contained true -p:CoolPropRid=linux-x64 -o publish-mcp/linux-x64
packaging/linux-mcp/build-mcp-deb.sh publish-mcp/linux-x64 10.2.0 amd64 artifacts
```

Use `-r linux-arm64` / `arm64` for the ARM package. Building needs only `dpkg-deb`; nothing runs
the payload, so either architecture can be built on any host. The `release` workflow builds both.
