Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

''' <summary>
''' A single simulation published in the DWSIM Case Library
''' (github.com/DanWBR/dwsim-case-library).
''' </summary>
Public Class CaseLibraryFlowsheet

    Public Property Title As String = ""
    Public Property Category As String = ""
    Public Property Path As String = ""
    Public Property DownloadUrl As String = ""
    Public Property PreviewUrl As String = ""
    Public Property DisplayName As String = ""

    ''' <summary>
    ''' The components of the DWSIM Patreon Edition the case uses (a unit operation, property package
    ''' or extension the open-source DWSIM does not have), by name, as the case index lists them.
    ''' </summary>
    Public Property PatreonComponents As New List(Of String)

    ''' <summary>The assemblies of those components, in the same order.</summary>
    Public Property PatreonAssemblies As New List(Of String)

    ''' <summary>
    ''' The Patreon Edition components the case uses that this DWSIM does not have: the case does
    ''' not open here. Empty in the Patreon Edition and for the cases that need none.
    ''' </summary>
    Public ReadOnly Property MissingComponents As List(Of String)
        Get
            Dim missing As New List(Of String)
            For i = 0 To PatreonAssemblies.Count - 1
                If Not CaseLibraryFlowsheets.HasAssembly(PatreonAssemblies(i)) Then missing.Add(PatreonComponents(i))
            Next
            Return missing
        End Get
    End Property

End Class

''' <summary>
''' Lists and downloads the flowsheets kept in the DWSIM Case Library GitHub repository. The list
''' comes from cases/index.json, which a workflow of the repository keeps up to date, read off
''' raw.githubusercontent.com; the git tree through the GitHub API is the fall-back (it allows 60
''' requests an hour per network, and some proxies block it), and the list read last time is kept
''' for when neither answers. Every .dwxmz / .dwxml under cases/ becomes an entry, downloaded from
''' its raw URL. Mirrors FOSSEEFlowsheets.
''' </summary>
Public Class CaseLibraryFlowsheets

    Private Const Owner As String = "DanWBR"
    Private Const Repo As String = "dwsim-case-library"
    Private Const Branch As String = "main"

    Shared Sub New()
        Try
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or
                                                   SecurityProtocolType.Tls12
        Catch
        End Try
    End Sub

    Private Shared Sub ApplySystemProxy(request As HttpWebRequest, target As Uri)
        Try
            Dim proxyUri As Uri = WebRequest.GetSystemWebProxy().GetProxy(target)
            If Not target.AbsolutePath = proxyUri.AbsolutePath Then
                Dim proxyObj As New WebProxy(proxyUri)
                proxyObj.Credentials = CredentialCache.DefaultCredentials
                request.Proxy = proxyObj
            End If
        Catch
        End Try
    End Sub

    Private Shared Function DownloadString(url As String, Optional accept As String = Nothing) As String
        Dim target As New Uri(url)
        Dim request = DirectCast(WebRequest.Create(target), HttpWebRequest)
        request.Method = "GET"
        request.Timeout = 30000
        request.ReadWriteTimeout = 30000
        request.UserAgent = "DWSIM"
        If accept IsNot Nothing Then request.Accept = accept
        ApplySystemProxy(request, target)

        Using response = DirectCast(request.GetResponse(), HttpWebResponse)
            Using stream = response.GetResponseStream()
                Using sr As New StreamReader(stream, Encoding.UTF8)
                    Return sr.ReadToEnd()
                End Using
            End Using
        End Using
    End Function

    ''' <summary>The list read last time, for when the Case Library cannot be reached.</summary>
    Private Shared ReadOnly Property CacheFile As String
        Get
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DWSIM", "caselibrary-index.json")
        End Get
    End Property

    ''' <summary>
    ''' Reads the list of the case library. It is a web request, so callers run it on a background
    ''' thread and fill the list when it lands. Throws, with the reason, when nothing can be read.
    ''' </summary>
    Public Shared Function GetCaseLibraryFlowsheets() As List(Of CaseLibraryFlowsheet)
        Return GetCaseLibraryFlowsheets(Nothing)
    End Function

    ''' <summary>
    ''' Reads the list of the case library: cases/index.json, else the git tree through the GitHub
    ''' API, else the list read last time. <paramref name="note"/> says when the list is that last copy,
    ''' and why; it is Nothing for a list just read. Throws, with the reason, when nothing can be read.
    ''' </summary>
    Public Shared Function GetCaseLibraryFlowsheets(ByRef note As String) As List(Of CaseLibraryFlowsheet)

        note = Nothing
        Dim errors As New List(Of String)
        Dim entries As List(Of JObject) = Nothing

        Try
            entries = EntriesFromIndex(DownloadString(RawUrl("cases/index.json")))
        Catch ex As Exception
            errors.Add("the case index: " & Describe(ex))
        End Try

        If entries Is Nothing OrElse entries.Count = 0 Then
            Try
                entries = EntriesFromTree(DownloadString($"https://api.github.com/repos/{Owner}/{Repo}/git/trees/{Branch}?recursive=1", "application/vnd.github+json"))
            Catch ex As Exception
                errors.Add("the GitHub API: " & Describe(ex))
            End Try
        End If

        If entries IsNot Nothing AndAlso entries.Count > 0 Then
            Try
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFile))
                File.WriteAllText(CacheFile, New JObject From {{"version", 1}, {"cases", New JArray(entries)}}.ToString(Formatting.None), New UTF8Encoding(False))
            Catch
            End Try
            Return Build(entries)
        End If

        Try
            If File.Exists(CacheFile) Then
                Dim cached = EntriesFromIndex(File.ReadAllText(CacheFile))
                If cached IsNot Nothing AndAlso cached.Count > 0 Then
                    note = "The Case Library could not be reached (" & String.Join("; ", errors) & "): this is the list from " &
                        File.GetLastWriteTime(CacheFile).ToString("yyyy-MM-dd") & "."
                    Return Build(cached)
                End If
            End If
        Catch
        End Try

        Throw New Exception("The Case Library could not be reached: " & String.Join("; ", errors) & ".")

    End Function

    ''' <summary>The cases of cases/index.json: path, preview and the Patreon Edition components they use.</summary>
    Private Shared Function EntriesFromIndex(json As String) As List(Of JObject)
        Dim cases = TryCast(JObject.Parse(json)("cases"), JArray)
        If cases Is Nothing Then Return New List(Of JObject)
        Return cases.OfType(Of JObject).Where(Function(c) Not String.IsNullOrEmpty(c("path")?.ToString())).ToList()
    End Function

    ''' <summary>The files of the git tree the GitHub API returns, as index entries without components.</summary>
    Private Shared Function EntriesFromTree(json As String) As List(Of JObject)
        Dim tree = TryCast(JObject.Parse(json)("tree"), JArray)
        If tree Is Nothing Then Return New List(Of JObject)
        Return tree.Where(Function(n) n("type")?.ToString() = "blob").Select(Function(n) n("path")?.ToString()).
            Where(Function(p) Not String.IsNullOrEmpty(p)).Select(Function(p) New JObject From {{"path", p}}).ToList()
    End Function

    Private Shared ReadOnly Assemblies As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>
    ''' True when this DWSIM has the assembly: loaded, or in its folder or the folders it loads
    ''' unit operations, property packages and extensions from.
    ''' </summary>
    Friend Shared Function HasAssembly(name As String) As Boolean
        SyncLock Assemblies
            Dim known As Boolean
            If Assemblies.TryGetValue(name, known) Then Return known
            Dim found = AppDomain.CurrentDomain.GetAssemblies().Any(Function(a) String.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
            If Not found Then
                Dim root = AppDomain.CurrentDomain.BaseDirectory
                For Each folder In {"", "unitops", "ppacks", "extenders", "plugins"}
                    If File.Exists(Path.Combine(root, folder, name & ".dll")) Then found = True : Exit For
                Next
            End If
            Assemblies(name) = found
            Return found
        End SyncLock
    End Function

    ''' <summary>What went wrong with a request, in words a user can act on.</summary>
    Private Shared Function Describe(ex As Exception) As String
        Dim we = TryCast(ex, WebException)
        If we Is Nothing Then Return ex.Message
        Dim response = TryCast(we.Response, HttpWebResponse)
        If response IsNot Nothing Then
            Dim code = CInt(response.StatusCode)
            If (code = 403 OrElse code = 429) AndAlso response.Headers("X-RateLimit-Remaining") = "0" Then
                Return "GitHub refused it, the 60 requests an hour allowed to this network are used up"
            End If
            Return "the server answered " & code & " " & response.StatusDescription
        End If
        Select Case we.Status
            Case WebExceptionStatus.NameResolutionFailure
                Return "the address could not be found (no internet, or a proxy or firewall blocks it)"
            Case WebExceptionStatus.Timeout
                Return "no answer in 30 s"
            Case WebExceptionStatus.TrustFailure, WebExceptionStatus.SecureChannelFailure
                Return "the secure connection failed (" & we.Message & "), often a proxy or antivirus that inspects HTTPS"
            Case Else
                Return we.Message
        End Select
    End Function

    ''' <summary>The flowsheets among <paramref name="entries"/>, by category and title.</summary>
    Private Shared Function Build(entries As IEnumerable(Of JObject)) As List(Of CaseLibraryFlowsheet)

        Dim list As New List(Of CaseLibraryFlowsheet)

        For Each entry In entries

            Dim p As String = entry("path").ToString()

            Dim lower = p.ToLowerInvariant()
            If Not lower.StartsWith("cases/") Then Continue For
            If Not (lower.EndsWith(".dwxmz") OrElse lower.EndsWith(".dwxml")) Then Continue For

            Dim parts = p.Split("/"c)
            Dim category As String = If(parts.Length > 1, Prettify(parts(1)), "")
            ' the flowsheet lives in its own folder, so the folder name is the case title
            Dim folder As String = If(parts.Length > 2, parts(parts.Length - 2), Path.GetFileNameWithoutExtension(p))

            Dim fs As New CaseLibraryFlowsheet With {
                .Path = p,
                .Category = category,
                .Title = Prettify(folder),
                .DownloadUrl = RawUrl(p)
            }

            ' a preview image sits next to the flowsheet in the same folder, same base name
            Dim pngPath As String = p.Substring(0, p.Length - Path.GetExtension(p).Length) & ".png"
            fs.PreviewUrl = RawUrl(pngPath)
            fs.DisplayName = If(category = "", fs.Title, category & " - " & fs.Title)

            Dim requires = TryCast(entry("requires"), JArray)
            If requires IsNot Nothing Then
                For Each r In requires.OfType(Of JObject)
                    Dim asm = r("assembly")?.ToString()
                    If String.IsNullOrEmpty(asm) Then Continue For
                    fs.PatreonAssemblies.Add(asm)
                    fs.PatreonComponents.Add(If(r("name")?.ToString(), asm))
                Next
            End If
            ' this DWSIM lacks what the case needs: say so in the list
            If fs.MissingComponents.Count > 0 Then fs.DisplayName &= "  [needs DWSIM Patreon Edition]"

            list.Add(fs)

        Next

        Return list.OrderBy(Function(x) x.Category).ThenBy(Function(x) x.Title).ToList()

    End Function

    Private Shared Function RawUrl(path As String) As String
        Return $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/{path}"
    End Function

    ''' <summary>
    ''' Turns a repository slug ("green-hydrogen-solar-electrolysis") into a display title
    ''' ("Green Hydrogen Solar Electrolysis").
    ''' </summary>
    Private Shared Function Prettify(slug As String) As String
        If String.IsNullOrEmpty(slug) Then Return ""
        Dim words = slug.Replace("-"c, " "c).Replace("_"c, " "c).Split(" "c)
        Dim sb As New StringBuilder
        For Each w In words
            If w.Length = 0 Then Continue For
            sb.Append(Char.ToUpperInvariant(w(0)))
            If w.Length > 1 Then sb.Append(w.Substring(1))
            sb.Append(" "c)
        Next
        Return sb.ToString().Trim()
    End Function

    ''' <summary>
    ''' Downloads a case-library flowsheet to a temporary file and returns its path. The raw URL
    ''' already carries the .dwxmz / .dwxml extension, so the loader picks the reader from it.
    ''' </summary>
    Public Shared Function DownloadFlowsheet(downloadUrl As String, pa As Action(Of Integer)) As String

        Dim target As New Uri(downloadUrl)
        Dim ext = Path.GetExtension(downloadUrl)
        If String.IsNullOrEmpty(ext) Then ext = ".dwxmz"

        Dim fpath As String = Path.Combine(Path.GetTempPath(),
                                           "DWSIM_CaseLibrary_" & Guid.NewGuid().ToString("N") & ext)

        Dim request = DirectCast(WebRequest.Create(target), HttpWebRequest)
        request.Method = "GET"
        request.Timeout = 60000
        request.ReadWriteTimeout = 60000
        request.UserAgent = "DWSIM"
        ApplySystemProxy(request, target)

        Using response = DirectCast(request.GetResponse(), HttpWebResponse)
            Dim total As Long = response.ContentLength
            Dim read As Long = 0
            Using stream = response.GetResponseStream()
                Using fout = File.Create(fpath)
                    Dim buffer(81919) As Byte
                    Dim n As Integer
                    Do
                        n = stream.Read(buffer, 0, buffer.Length)
                        If n <= 0 Then Exit Do
                        fout.Write(buffer, 0, n)
                        read += n
                        If pa IsNot Nothing AndAlso total > 0 Then
                            pa.Invoke(CInt(Math.Min(100, read * 100 \ total)))
                        End If
                    Loop
                End Using
            End Using
        End Using

        Return fpath

    End Function

End Class
