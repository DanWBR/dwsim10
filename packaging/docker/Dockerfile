# Runtime image for the DWSIM headless MCP server.
#
# The app is published self-contained (the .NET runtime is bundled), so the base image only needs
# the native OS dependencies. The build context is expected to hold a per-architecture publish at
# publish-mcp/amd64 and publish-mcp/arm64; buildx sets TARGETARCH to pick the matching one.

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble
ARG TARGETARCH

# SkiaSharp (the engine touches it at start-up) needs fontconfig; ICU already ships in runtime-deps.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/dwsim-mcp
COPY publish-mcp/${TARGETARCH}/ ./
RUN chmod +x /opt/dwsim-mcp/dwsim-mcp

# Bind every interface inside the container; the host maps the port with -p. Extra flags (for
# example --token secret) can be appended after the image name and are passed straight through.
EXPOSE 5901
ENTRYPOINT ["/opt/dwsim-mcp/dwsim-mcp", "--http", "--host", "0.0.0.0", "--port", "5901"]
