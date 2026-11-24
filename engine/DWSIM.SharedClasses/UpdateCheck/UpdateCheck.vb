Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Threading
Imports Newtonsoft.Json.Linq

''' <summary>A release on GitHub that is newer than the running version.</summary>
Public Class UpdateInfo

    ''' <summary>The version of the release, read from its tag (v10.3.1 gives 10.3.1.0).</summary>
    Public Property Version As Version

    Public Property TagName As String = ""

    ''' <summary>The release page on GitHub.</summary>
    Public Property ReleaseUrl As String = ""

    ''' <summary>The "What's new" section of the release notes.</summary>
    Public Property WhatsNew As String = ""

    ''' <summary>The installer of this installation type (empty for a portable copy, which the user
    ''' updates by hand from the release page).</summary>
    Public Property AssetName As String = ""

    Public Property AssetUrl As String = ""

    Public Property AssetSize As Long

    ''' <summary>The SHA-256 of the installer that GitHub publishes with the asset, in hex (may be empty).</summary>
    Public Property AssetSha256 As String = ""

    ''' <summary>True when DWSIM can download and start the installer of this release.</summary>
    Public ReadOnly Property CanInstall As Boolean
        Get
            Return AssetUrl <> ""
        End Get
    End Property

End Class

''' <summary>
''' Checks the GitHub releases of the edition that is running (DanWBR/dwsim10 for the open edition,
''' DanWBR/dwsim-patreon for the Patreon edition), and downloads and starts the installer of a newer
''' release. Only published, final releases count: GitHub's "latest" release skips drafts and prereleases.
''' </summary>
Public Class UpdateCheck

    Public Const OpenRepository As String = "DanWBR/dwsim10"
    Public Const PatreonRepository As String = "DanWBR/dwsim-patreon"

    ''' <summary>The installer downloaded by <see cref="DownloadUpdate"/>, which <see cref="RunPendingInstaller"/>
    ''' starts once DWSIM has closed.</summary>
    Public Shared Property PendingInstaller As String = ""

    ''' <summary>The folder of the running executable.</summary>
    Public Shared Function AppDirectory() As String
        Return AppDomain.CurrentDomain.BaseDirectory
    End Function

    ''' <summary>True when the running installation carries the Plus content of the Patreon edition (the
    ''' signed integrity manifest, or the Plus manager extension).</summary>
    Public Shared Function IsPatreonEdition() As Boolean
        Dim dir = AppDirectory()
        Return File.Exists(Path.Combine(dir, "manifest.sig")) OrElse
            File.Exists(Path.Combine(dir, "extenders", "DWSIM.Extensions.Plus.Manager.dll"))
    End Function

    ''' <summary>
    ''' Asks GitHub for the latest release of the running edition and returns it when it is newer than
    ''' <paramref name="currentVersion"/>, or Nothing when the installed version is current or the check
    ''' fails. <paramref name="classicUI"/> selects the classic (WinForms) installer, which only exists in
    ''' the Patreon edition.
    ''' </summary>
    Public Shared Function CheckForUpdate(currentVersion As String, classicUI As Boolean) As UpdateInfo
        Try
            Dim repo = If(classicUI OrElse IsPatreonEdition(), PatreonRepository, OpenRepository)
            Dim json = DownloadText("https://api.github.com/repos/" & repo & "/releases/latest")
            Dim release = JObject.Parse(json)

            Dim tag = release.Value(Of String)("tag_name")
            Dim latest = ParseVersion(tag)
            Dim current = ParseVersion(currentVersion)
            If latest Is Nothing OrElse current Is Nothing OrElse latest <= current Then Return Nothing

            Dim info As New UpdateInfo With {
                .Version = latest,
                .TagName = tag,
                .ReleaseUrl = release.Value(Of String)("html_url"),
                .WhatsNew = ExtractWhatsNew(release.Value(Of String)("body"))
            }

            Dim pattern = InstallerPattern(classicUI)
            If pattern IsNot Nothing Then
                Dim assets = TryCast(release("assets"), JArray)
                If assets IsNot Nothing Then
                    For Each a As JObject In assets
                        Dim name = a.Value(Of String)("name")
                        If name IsNot Nothing AndAlso pattern(name.ToLowerInvariant()) Then
                            info.AssetName = name
                            info.AssetUrl = a.Value(Of String)("browser_download_url")
                            info.AssetSize = a.Value(Of Long?)("size").GetValueOrDefault()
                            Dim digest = a.Value(Of String)("digest")
                            If digest IsNot Nothing AndAlso digest.StartsWith("sha256:") Then info.AssetSha256 = digest.Substring(7)
                            Exit For
                        End If
                    Next
                End If
            End If

            Return info
        Catch ex As Exception
            Console.WriteLine("Error checking the latest version: " & ex.ToString)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Selects the release asset that updates this installation in place: the Inno Setup installer on
    ''' Windows when DWSIM was installed by one, the disk image of an app bundle on macOS and the Debian
    ''' package of an /opt/dwsim installation on Linux. Returns Nothing for a portable copy.
    ''' </summary>
    Private Shared Function InstallerPattern(classicUI As Boolean) As Func(Of String, Boolean)

        Dim dir = AppDirectory()
        Dim arm = RuntimeInformation.ProcessArchitecture = Architecture.Arm64

        Select Case GlobalSettings.Settings.RunningPlatform()
            Case GlobalSettings.Settings.Platform.Windows
                If Not File.Exists(Path.Combine(dir, "unins000.exe")) Then Return Nothing
                If classicUI Then Return Function(n) n.EndsWith("-winforms-win-x64-setup.exe")
                Dim rid = If(arm, "win-arm64", "win-x64")
                Return Function(n) Not n.Contains("winforms") AndAlso
                    (n.EndsWith("-" & rid & "-setup.exe") OrElse n.EndsWith("-" & rid & "-avalonia-setup.exe"))
            Case GlobalSettings.Settings.Platform.Mac
                If Not dir.Replace("\", "/").Contains(".app/Contents/MacOS") Then Return Nothing
                Dim suffix = If(arm, "-arm64.dmg", "-x64.dmg")
                Return Function(n) n.EndsWith(suffix)
            Case Else
                If Not dir.Replace("\", "/").StartsWith("/opt/") Then Return Nothing
                Dim suffix = If(arm, "_arm64.deb", "_amd64.deb")
                Return Function(n) n.EndsWith(suffix) AndAlso Not n.Contains("-mcp")
        End Select

    End Function

    ''' <summary>
    ''' Downloads the installer of <paramref name="info"/> into the temporary folder, checks its SHA-256
    ''' against the one GitHub publishes, and returns its path. <paramref name="progress"/> receives the
    ''' bytes read and the total. A complete file from an earlier download is reused.
    ''' </summary>
    Public Shared Function DownloadUpdate(info As UpdateInfo, progress As Action(Of Long, Long),
                                          cancel As CancellationToken) As String

        Dim folder = Path.Combine(Path.GetTempPath(), "DWSIM-update")
        Directory.CreateDirectory(folder)
        Dim target = Path.Combine(folder, info.AssetName)

        If File.Exists(target) AndAlso New FileInfo(target).Length = info.AssetSize AndAlso HashMatches(target, info.AssetSha256) Then
            progress?.Invoke(info.AssetSize, info.AssetSize)
            Return target
        End If

        Dim partFile = target & ".part"
        EnableTls12()
        Dim request = DirectCast(WebRequest.Create(info.AssetUrl), HttpWebRequest)
        request.UserAgent = "DWSIM-UpdateCheck"
        request.AllowAutoRedirect = True

        Using response = request.GetResponse(), input = response.GetResponseStream(), output = File.Create(partFile)
            Dim total = If(response.ContentLength > 0, response.ContentLength, info.AssetSize)
            Dim buffer(81919) As Byte
            Dim read As Long = 0
            Dim lastReport As Long = 0
            Do
                cancel.ThrowIfCancellationRequested()
                Dim n = input.Read(buffer, 0, buffer.Length)
                If n <= 0 Then Exit Do
                output.Write(buffer, 0, n)
                read += n
                If read - lastReport >= 1048576 OrElse read = total Then
                    lastReport = read
                    progress?.Invoke(read, total)
                End If
            Loop
        End Using

        If info.AssetSize > 0 AndAlso New FileInfo(partFile).Length <> info.AssetSize Then
            File.Delete(partFile)
            Throw New IOException("The download of " & info.AssetName & " is incomplete.")
        End If
        If Not HashMatches(partFile, info.AssetSha256) Then
            File.Delete(partFile)
            Throw New IOException("The checksum of " & info.AssetName & " doesn't match the one published on GitHub.")
        End If

        If File.Exists(target) Then File.Delete(target)
        File.Move(partFile, target)
        Return target

    End Function

    ''' <summary>What happens after DWSIM closes, for the confirmation message.</summary>
    Public Shared Function InstallDescription() As String
        Select Case GlobalSettings.Settings.RunningPlatform()
            Case GlobalSettings.Settings.Platform.Windows
                Return "When DWSIM closes, the installer starts and updates this installation."
            Case GlobalSettings.Settings.Platform.Mac
                Return "When DWSIM closes, the new DWSIM.app replaces the installed one and DWSIM starts again. macOS asks for an administrator password if the application folder needs one."
            Case Else
                Return "When DWSIM closes, the system asks for your password to install the package, and DWSIM starts again."
        End Select
    End Function

    ''' <summary>Starts <see cref="PendingInstaller"/>, if there is one. Call it after DWSIM closed its
    ''' windows, so the installer can replace the program files.</summary>
    Public Shared Sub RunPendingInstaller()
        Dim installer = PendingInstaller
        PendingInstaller = ""
        If installer = "" OrElse Not File.Exists(installer) Then Return
        Try
            Select Case GlobalSettings.Settings.RunningPlatform()
                Case GlobalSettings.Settings.Platform.Windows
                    ' the installer wizard, which keeps the folder and the options of the installed copy
                    ' and offers to start DWSIM on its last page
                    Process.Start(New ProcessStartInfo(installer) With {.UseShellExecute = True})
                Case GlobalSettings.Settings.Platform.Mac
                    Dim bundle = Path.GetFullPath(Path.Combine(AppDirectory(), "..", ".."))
                    StartScript(MacInstallScript, installer, bundle.TrimEnd("/"c))
                Case Else
                    StartScript(LinuxInstallScript, installer, Path.Combine(AppDirectory(), "DWSIM.UI.Desktop.Avalonia"))
            End Select
        Catch ex As Exception
            Console.WriteLine("Error starting the installer: " & ex.ToString)
        End Try
    End Sub

    ''' <summary>Writes <paramref name="script"/> next to the downloaded package and runs it with bash,
    ''' passing the process id of DWSIM (the script waits for it to exit) and the two arguments.</summary>
    Private Shared Sub StartScript(script As String, arg1 As String, arg2 As String)
        Dim file = Path.Combine(Path.GetDirectoryName(arg1), "install-update.sh")
        IO.File.WriteAllText(file, script.Replace(vbCrLf, vbLf))
        Dim psi As New ProcessStartInfo("/bin/bash") With {.UseShellExecute = False}
        psi.Arguments = String.Join(" ", {file, Process.GetCurrentProcess().Id.ToString(), arg1, arg2}.Select(Function(a) "'" & a.Replace("'", "'\''") & "'"))
        Process.Start(psi)
    End Sub

    ' $1 = DWSIM process id, $2 = disk image, $3 = installed DWSIM.app. The bundle is replaced in place
    ' (ditto keeps the signature); an app run from the disk image itself or from a translocated path
    ' can't be replaced, and then the disk image is opened in Finder instead.
    Private Const MacInstallScript As String = "#!/bin/bash
trap '' HUP
pid=""$1""; dmg=""$2""; app=""$3""
while kill -0 ""$pid"" 2>/dev/null; do sleep 1; done
fail=
case ""$app"" in
  */AppTranslocation/*|/Volumes/*|*/DWSIM-update/*) fail=1 ;;
esac
if [ -z ""$fail"" ]; then
  mnt=$(mktemp -d /tmp/dwsim-update.XXXXXX)
  if hdiutil attach -nobrowse -readonly -noautoopen -mountpoint ""$mnt"" ""$dmg"" >/dev/null; then
    src=$(find ""$mnt"" -maxdepth 1 -name '*.app' | head -n 1)
    if [ -z ""$src"" ]; then
      fail=1
    elif [ -w ""$(dirname ""$app"")"" ] && [ -w ""$app"" ]; then
      rm -rf ""$app.old""
      if mv ""$app"" ""$app.old"" && ditto ""$src"" ""$app""; then rm -rf ""$app.old""
      else rm -rf ""$app""; mv ""$app.old"" ""$app""; fail=1; fi
    else
      osascript - ""$src"" ""$app"" <<'APPLESCRIPT' || fail=1
on run argv
  set src to quoted form of item 1 of argv
  set app to quoted form of item 2 of argv
  set tmp to quoted form of (item 2 of argv & "".new"")
  do shell script ""rm -rf "" & tmp & "" && ditto "" & src & "" "" & tmp & "" && rm -rf "" & app & "" && mv "" & tmp & "" "" & app with administrator privileges
end run
APPLESCRIPT
    fi
    hdiutil detach ""$mnt"" -quiet || hdiutil detach ""$mnt"" -force -quiet
  else
    fail=1
  fi
  rmdir ""$mnt"" 2>/dev/null
fi
if [ -z ""$fail"" ]; then open ""$app""; else open ""$dmg""; fi
"

    ' $1 = DWSIM process id, $2 = Debian package, $3 = DWSIM executable. pkexec shows the desktop's
    ' password dialog; without it (or when it fails) the package is opened in the software installer.
    Private Const LinuxInstallScript As String = "#!/bin/bash
trap '' HUP
pid=""$1""; deb=""$2""; exe=""$3""
while kill -0 ""$pid"" 2>/dev/null; do sleep 1; done
command -v notify-send >/dev/null 2>&1 && notify-send -i dwsim DWSIM 'Installing the update...'
if command -v pkexec >/dev/null 2>&1 && pkexec env DEBIAN_FRONTEND=noninteractive apt-get install -y ""$deb""; then
  command -v notify-send >/dev/null 2>&1 && notify-send -i dwsim DWSIM 'The update is installed.'
  setsid ""$exe"" >/dev/null 2>&1 < /dev/null &
else
  xdg-open ""$deb"" >/dev/null 2>&1 &
fi
"

    ''' <summary>A short text of the update for a message box: the versions and the release notes.</summary>
    Public Shared Function Describe(info As UpdateInfo, currentVersion As String) As String
        Dim text = String.Format("DWSIM {0} is available (installed: {1}).", FormatVersion(info.Version), FormatVersion(ParseVersion(currentVersion)))
        If info.WhatsNew <> "" Then text &= Environment.NewLine & Environment.NewLine & info.WhatsNew
        Return text
    End Function

    Public Shared Function FormatVersion(v As Version) As String
        If v Is Nothing Then Return ""
        Return String.Format("{0}.{1}.{2}", v.Major, v.Minor, Math.Max(v.Build, 0))
    End Function

    ''' <summary>Reads "v10.3.0", "10.3.0-rc2" or "10.3.0.0" as a four-part version (10.3.0.0).</summary>
    Public Shared Function ParseVersion(text As String) As Version
        If String.IsNullOrWhiteSpace(text) Then Return Nothing
        Dim s = text.Trim().TrimStart("v"c, "V"c)
        Dim dash = s.IndexOfAny({"-"c, "+"c, " "c})
        If dash >= 0 Then s = s.Substring(0, dash)
        Dim v As Version = Nothing
        If Not Version.TryParse(s, v) Then Return Nothing
        Return New Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0))
    End Function

    ''' <summary>The "What's new in this release" list of the release notes, or the first lines of the
    ''' notes when they don't have that section.</summary>
    Public Shared Function ExtractWhatsNew(body As String) As String
        If String.IsNullOrWhiteSpace(body) Then Return ""
        Dim lines = body.Replace(vbCrLf, vbLf).Split(ChrW(10))
        Dim start = Array.FindIndex(lines, Function(l) l.TrimStart().StartsWith("## What's new", StringComparison.OrdinalIgnoreCase))
        Dim selected As New List(Of String)
        If start >= 0 Then
            For i = start + 1 To lines.Length - 1
                If lines(i).TrimStart().StartsWith("#") Then Exit For
                selected.Add(lines(i))
            Next
        Else
            selected.AddRange(lines)
        End If
        Dim items = selected.Select(Function(l) l.TrimEnd()).SkipWhile(Function(l) l = "").ToList()
        While items.Count > 0 AndAlso items.Last() = "" : items.RemoveAt(items.Count - 1) : End While
        If items.Count > 25 Then
            Dim more = items.Count - 25
            items = items.Take(25).ToList()
            items.Add(String.Format("... and {0} more changes in the release notes.", more))
        End If
        Return String.Join(Environment.NewLine, items)
    End Function

    Private Shared Function HashMatches(file As String, sha256Hex As String) As Boolean
        If String.IsNullOrEmpty(sha256Hex) Then Return True
        Using sha = SHA256.Create(), stream = IO.File.OpenRead(file)
            Dim hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "")
            Return String.Equals(hash, sha256Hex, StringComparison.OrdinalIgnoreCase)
        End Using
    End Function

    Private Shared Function DownloadText(url As String) As String
        EnableTls12()
        Using client As New WebClient()
            ' the GitHub API refuses requests without a user agent
            client.Headers.Add(HttpRequestHeader.UserAgent, "DWSIM-UpdateCheck")
            client.Headers.Add(HttpRequestHeader.Accept, "application/vnd.github+json")
            client.Encoding = System.Text.Encoding.UTF8
            Return client.DownloadString(url)
        End Using
    End Function

    Private Shared Sub EnableTls12()
        Try
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Catch
        End Try
    End Sub

End Class
