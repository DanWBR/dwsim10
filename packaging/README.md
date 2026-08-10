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

Notarisation is the only step that cannot run without credentials. Until these six secrets
exist the macOS job still produces a disk image, unsigned, and says so in the log; Gatekeeper
will refuse it on a machine that downloaded it. Everything else in the release is unaffected.

| Secret | What it is |
|---|---|
| `MACOS_CERTIFICATE` | a Developer ID Application certificate exported as `.p12`, base64 encoded |
| `MACOS_CERTIFICATE_PASSWORD` | the password of that `.p12` |
| `MACOS_SIGN_IDENTITY` | the certificate's common name, `Developer ID Application: Name (TEAMID)` |
| `MACOS_NOTARY_KEY` | an App Store Connect API key (`.p8`), base64 encoded |
| `MACOS_NOTARY_KEY_ID` | that key's Key ID |
| `MACOS_NOTARY_ISSUER` | the Issuer ID of the account the key belongs to |

All of them come from an Apple Developer Program membership (USD 99/year), which takes days to be
approved.

### How to obtain each one

1. **Join the Apple Developer Program** with the account and team you will ship as.

2. **Developer ID Application certificate.** In Xcode, Settings, Accounts, Manage Certificates, the
   plus button, Developer ID Application (or make a CSR and create it in the developer portal). It
   lands in the login keychain. Export it from Keychain Access as a `.p12` with a password. Then:
   - `base64 -i cert.p12 | pbcopy` gives `MACOS_CERTIFICATE`;
   - the password you set is `MACOS_CERTIFICATE_PASSWORD`.

3. **Signing identity.** `security find-identity -v -p codesigning` lists it; copy the whole
   `Developer ID Application: Name (TEAMID)` string into `MACOS_SIGN_IDENTITY`.

4. **App Store Connect API key** (this is what `notarytool` authenticates with, not an Apple ID). In
   App Store Connect, Users and Access, Integrations, Keys, generate a key with the Developer role
   and download the `.p8` (offered only once). Then:
   - `base64 -i AuthKey_XXXXXXXXXX.p8 | pbcopy` gives `MACOS_NOTARY_KEY`;
   - the Key ID shown next to it is `MACOS_NOTARY_KEY_ID`;
   - the Issuer ID at the top of the Keys page is `MACOS_NOTARY_ISSUER`.

5. **Store them** on the repository, the same way as any other secret:
   ```bash
   gh secret set MACOS_CERTIFICATE          --repo DanWBR/DWSIMCore < cert.p12.b64
   gh secret set MACOS_CERTIFICATE_PASSWORD --repo DanWBR/DWSIMCore
   gh secret set MACOS_SIGN_IDENTITY        --repo DanWBR/DWSIMCore
   gh secret set MACOS_NOTARY_KEY           --repo DanWBR/DWSIMCore < authkey.p8.b64
   gh secret set MACOS_NOTARY_KEY_ID        --repo DanWBR/DWSIMCore
   gh secret set MACOS_NOTARY_ISSUER        --repo DanWBR/DWSIMCore
   ```
   The commands with no redirection prompt for the value and hide it.

Windows code signing is optional and needs a separate certificate; the workflow does not attempt
it, so the zip is unsigned and SmartScreen will warn on first run.

## Bundling the AI Assistant server (optional)

The release workflow can fetch the proprietary assistant server from its own repo and place it under
`extenders/AIAssistantFiles/` in each payload. It runs only when this secret is set; without it the
app ships without the assistant server.

| Secret | What it is |
|---|---|
| `ASSISTANT_TOKEN` | a token that can read releases of `DanWBR/dwsim-assistant` (a fine-grained PAT with Contents:Read on that repo) |

The workflow downloads the latest release asset `dwsim-assistant-<rid>` for each target, mapping
Windows on ARM to the `win-x64` build, which it runs through emulation.

## The entitlements

`macos/entitlements.plist` asks for JIT, unsigned executable memory and library validation off.
A .NET application needs all three: it compiles methods while it runs, and it loads assemblies
that were not signed as one unit with the bundle.
