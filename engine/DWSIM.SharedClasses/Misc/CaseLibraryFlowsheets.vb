Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
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

End Class

''' <summary>
''' Lists and downloads the flowsheets kept in the DWSIM Case Library GitHub repository. The
''' repository has no manifest, so the index is read straight off the git tree and every .dwxmz /
''' .dwxml under cases/ becomes an entry, downloaded from its raw URL. Mirrors FOSSEEFlowsheets.
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

    Private Shared Function DownloadString(url As String) As String
        Dim target As New Uri(url)
        Dim request = DirectCast(WebRequest.Create(target), HttpWebRequest)
        request.Method = "GET"
        request.Timeout = 30000
        request.ReadWriteTimeout = 30000
        request.UserAgent = "DWSIM"
        request.Accept = "application/vnd.github+json"
        ApplySystemProxy(request, target)

        Using response = DirectCast(request.GetResponse(), HttpWebResponse)
            Using stream = response.GetResponseStream()
                Using sr As New StreamReader(stream, Encoding.UTF8)
                    Return sr.ReadToEnd()
                End Using
            End Using
        End Using
    End Function

    ''' <summary>
    ''' Reads the flowsheet index off the case-library repository. It is a web request, so callers
    ''' run it on a background thread and fill the list when it lands.
    ''' </summary>
    Public Shared Function GetCaseLibraryFlowsheets() As List(Of CaseLibraryFlowsheet)

        Dim apiUrl As String = $"https://api.github.com/repos/{Owner}/{Repo}/git/trees/{Branch}?recursive=1"

        Dim json As String = DownloadString(apiUrl)
        Dim root As JObject = JObject.Parse(json)
        Dim tree = TryCast(root("tree"), JArray)

        Dim list As New List(Of CaseLibraryFlowsheet)
        If tree Is Nothing Then Return list

        For Each node In tree

            If node("type") Is Nothing OrElse node("type").ToString() <> "blob" Then Continue For

            Dim p As String = If(node("path")?.ToString(), "")
            If p = "" Then Continue For

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
