# DWSIM headless MCP server (Docker)

A container image of the [MCP server](../../tools/DWSIM.MCPServer), for running DWSIM as a network
service on a PC or an SBC without installing anything on the host. It is the same self-contained
build the `.deb` uses, in a different envelope.

The image is published multi-architecture (`linux/amd64` and `linux/arm64`) to GitHub Container
Registry by the `release` workflow on a tag.

## Run it

```sh
docker run -d --name dwsim-mcp -p 5901:5901 ghcr.io/danwbr/dwsim-mcp:10.2
curl http://<host>:5901/health
```

`-p 5901:5901` publishes the container's port on the host (`host:container`). The server binds
`0.0.0.0` inside the container; Docker handles the network bridge.

Require a token on an untrusted network by appending it:

```sh
docker run -d --name dwsim-mcp -p 5901:5901 ghcr.io/danwbr/dwsim-mcp:10.2 --token change-me
```

Or use the [docker-compose.yml](docker-compose.yml): `docker compose up -d`.

Endpoints: `POST /mcp` (JSON-RPC), `GET /sse` (server-sent events), `GET /health`.

The server speaks plain HTTP. For HTTPS, put a reverse proxy (nginx, Caddy, Traefik) in front.

## Build it locally

```sh
# stage a publish per architecture, then build with buildx
dotnet publish tools/DWSIM.MCPServer/DWSIM.MCPServer.csproj -c Release -r linux-x64 \
  --self-contained true -p:CoolPropRid=linux-x64 -o build/publish-mcp/amd64
docker build -f packaging/docker/Dockerfile -t dwsim-mcp:local build
docker run --rm -p 5901:5901 dwsim-mcp:local
```

For a multi-arch build and push, `docker buildx build --platform linux/amd64,linux/arm64 ...`
with both `publish-mcp/amd64` and `publish-mcp/arm64` staged in the context. The `release`
workflow does exactly this.
