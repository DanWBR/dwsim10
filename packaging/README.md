# Packaging

The release workflow publishes the desktop application self-contained for six targets and wraps
each one. Nothing here has to be run by hand; the scripts are here so that a release can be
reproduced locally, and so that what the workflow does is readable.

| Target | What comes out |
|---|---|
| `win-x64`, `win-arm64` | a zip |
| `linux-x64`, `linux-arm64` | a `.tar.gz` and a `.deb` |
| `osx-x64`, `osx-arm64` | a signed, notarised `.dmg` |

Self-contained means the .NET runtime travels inside the package: the person installing DWSIM
installs nothing else. It costs about 220 MB unpacked, 70 MB in the `.deb`.

## Building one by hand

```bash
dotnet publish ui/DWSIM.UI.Desktop.Avalonia/DWSIM.UI.Desktop.Avalonia.csproj \
  -c Release -r linux-x64 --self-contained true -o publish/linux-x64
```

```bash
packaging/linux/build-deb.sh publish/linux-x64 10.0.0 amd64 artifacts
```

`dotnet publish -r <rid>` does not need a host of that architecture, so every payload except the
macOS signing is built on one Linux runner.

## What the owner of the repository has to create

Notarisation is the only step that cannot run without credentials. Until these four secrets
exist the macOS job still produces a disk image, unsigned, and says so in the log; Gatekeeper
will refuse it on a machine that downloaded it.

| Secret | What it is |
|---|---|
| `MACOS_CERTIFICATE` | a Developer ID Application certificate exported as `.p12`, base64 encoded |
| `MACOS_CERTIFICATE_PASSWORD` | the password of that `.p12` |
| `MACOS_SIGN_IDENTITY` | the certificate's common name, `Developer ID Application: Name (TEAMID)` |
| `MACOS_NOTARY_KEY`, `MACOS_NOTARY_KEY_ID`, `MACOS_NOTARY_ISSUER` | an App Store Connect API key (`.p8`, base64 encoded) and its two identifiers |

All of them come from an Apple Developer Program membership, which takes days to be approved.

Windows code signing is optional and needs a separate certificate; the workflow does not attempt
it, so the zip is unsigned and SmartScreen will warn on first run.

## The entitlements

`macos/entitlements.plist` asks for JIT, unsigned executable memory and library validation off.
A .NET application needs all three: it compiles methods while it runs, and it loads assemblies
that were not signed as one unit with the bundle.
