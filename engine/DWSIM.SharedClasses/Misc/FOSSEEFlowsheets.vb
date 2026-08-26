Imports System.IO
Imports System.Text
Imports System.Threading
Imports HtmlAgilityPack
Imports System.IO.Compression
Imports System.Net

Public Class FOSSEEFlowsheet

    Public Property Address As String = ""
    Public Property ProposerName As String = ""
    Public Property Title As String = ""
    Public Property Institution As String = ""
    Public Property DWSIMVersion As String = ""
    Public Property Reference As String = ""
    Public Property DownloadLink As String = ""
    Public Property DisplayName As String = ""

End Class

Public Class FOSSEEFlowsheets

    Shared Sub New()
        Try
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or
                                                   SecurityProtocolType.Tls12
        Catch
        End Try
    End Sub

    Private Shared Function DownloadBytes(url As String, proxy As IWebProxy) As Byte()
        Dim request = DirectCast(WebRequest.Create(url), HttpWebRequest)
        request.Method = "GET"
        request.Timeout = 30000
        request.ReadWriteTimeout = 30000
        If proxy IsNot Nothing Then request.Proxy = proxy

        Using response = DirectCast(request.GetResponse(), HttpWebResponse)
            Using stream = response.GetResponseStream()
                Using ms As New MemoryStream()
                    stream.CopyTo(ms)
                    Return ms.ToArray()
                End Using
            End Using
        End Using
    End Function


    ''' <summary>
    ''' Asked whether the abstract file that came with a downloaded flowsheet should be
    ''' deleted after the user has read it. The host assigns it; without one the file is kept.
    ''' </summary>
    Public Shared Property ConfirmAbstractFileDeletion As Func(Of String, Boolean)

    Public Shared Function GetFOSSEEFlowsheets() As List(Of FOSSEEFlowsheet)

        ' https, not http: the site 301s to it anyway, and Android refuses cleartext outright since
        ' API 28 - so on a phone the plain-http request never left the device.
        Dim website As String = "https://dwsim.fossee.in/flowsheeting-project/completed-flowsheet"

        Dim siteUri As Uri = New Uri(website)
        Dim proxyUri As Uri = Net.WebRequest.GetSystemWebProxy.GetProxy(siteUri)

        Dim bytes As Byte() = DownloadBytes(website, Nothing)

        Dim enc = Encoding.GetEncoding("utf-8")

        Dim source As [String] = enc.GetString(bytes, 0, bytes.Length - 1)
        source = WebUtility.HtmlDecode(source)

        Dim htmlpage As New HtmlDocument()

        htmlpage.LoadHtml(source)

        Dim tbody = htmlpage.DocumentNode.Descendants("tbody").FirstOrDefault

        If tbody Is Nothing Then Return New List(Of FOSSEEFlowsheet)

        Dim rows = tbody.Descendants("tr").ToList

        Dim list As New List(Of FOSSEEFlowsheet)
        Dim i As Integer = 1
        For Each r In rows

            Dim fs As New FOSSEEFlowsheet

            fs.Address = "https://dwsim.fossee.in" & r.ChildNodes(1).ChildNodes(0).Attributes(0).Value

            With fs
                .Institution = r.ChildNodes(3).ChildNodes(0).InnerText
                .ProposerName = r.ChildNodes(2).ChildNodes(0).InnerText
                .Title = r.ChildNodes(1).ChildNodes(0).InnerText
                .DownloadLink = fs.Address.Replace("dwsim-flowsheet-run", "full-download/project")
                .DisplayName = i.ToString("000") + ". " + .Title
            End With
            list.Add(fs)
            i += 1
        Next

        Return list.Where(Function(x) Not x.ProposerName.Contains("Daniel Wagner") And Not x.ProposerName.Contains("Daniel Wagner")).ToList

    End Function

    Public Shared Function GetFOSSEEFlowsheetInfo(address As String) As FOSSEEFlowsheet

        Dim website As String = "https://dwsim.fossee.in/flowsheeting-project/completed-flowsheet"

        Dim siteUri As Uri = New Uri(website)
        Dim proxyUri As Uri = Net.WebRequest.GetSystemWebProxy.GetProxy(siteUri)

        Dim fs As New FOSSEEFlowsheet

        fs.Address = address

        Dim proxy As IWebProxy = Nothing

        Try
            If Not siteUri.AbsolutePath = proxyUri.AbsolutePath Then
                Dim proxyObj2 As New WebProxy(proxyUri)
                proxyObj2.Credentials = CredentialCache.DefaultCredentials
                proxy = proxyObj2
            End If
        Catch ex As Exception
        End Try

        Dim bytes2 As Byte() = DownloadBytes(fs.Address, proxy)

        Dim source2 As [String] = Encoding.GetEncoding("utf-8").GetString(bytes2, 0, bytes2.Length - 1)
        source2 = WebUtility.HtmlDecode(source2)

        Dim htmlpage2 As New HtmlDocument()

        htmlpage2.LoadHtml(source2)

        Dim details = htmlpage2.DocumentNode.Descendants("div").Where(Function(x) x.Attributes.Contains("id") AndAlso x.Attributes("id").Value = "ajax_flowsheet_details").FirstOrDefault.ChildNodes.Descendants("li").ToList

        With fs
            .DownloadLink = "https://dwsim.fossee.in" & htmlpage2.DocumentNode.Descendants("a").Where(Function(x) x.InnerText = "Download Flowsheet").SingleOrDefault.Attributes("href").Value
            .DWSIMVersion = details(3).InnerText.Split(":")(1).Trim()
            .Institution = details(2).InnerText.Split(":")(1).Trim()
            .ProposerName = details(0).InnerText.Split(":")(1).Trim()
            .Reference = details(4).InnerText.Remove(0, 11)
            .Title = details(1).InnerText.Split(":")(1).Trim()
        End With

        Return fs

    End Function

    Public Shared Function LoadFlowsheet(fpath As String) As XDocument

        Dim fpath2 = Path.Combine(Path.GetTempPath(), "FOSSEE_DWSIM_TEMP")
        Directory.CreateDirectory(fpath2)
        Dim simname As String = ""

        Dim pdffiledir = GlobalSettings.Settings.GetConfigFileDir() + "FOSSEE"
        If Not Directory.Exists(pdffiledir) Then Directory.CreateDirectory(pdffiledir)
        Dim abstractfile As String = ""
        Dim abstractfile0 As String = ""

        ZipFile.ExtractToDirectory(fpath, fpath2)

        Dim subdirs = Directory.GetDirectories(fpath2)
        Dim rootdir = fpath2
        If subdirs.Count > 0 Then rootdir = subdirs(0)

        For Each file In Directory.GetFiles(rootdir)
            If Path.GetExtension(file).ToLower = ".dwxmz" Or Path.GetExtension(file).ToLower = ".dwxml" Then
                simname = file
            ElseIf Path.GetExtension(file).ToLower = ".pdf" Then
                abstractfile0 = file
            End If
        Next

        Dim xdoc As XDocument = Nothing

        If Path.GetExtension(simname).ToLower = ".dwxmz" Then
            xdoc = LoadZippedXML(simname)
        Else
            xdoc = XDocument.Load(simname)
        End If

        If abstractfile0 <> "" Then
            Try
                File.Copy(abstractfile0, Path.Combine(pdffiledir, Path.GetFileName(abstractfile0)), True)
                abstractfile = Path.Combine(pdffiledir, Path.GetFileName(abstractfile0))
            Catch ex As Exception
                Console.WriteLine("Error copying " & abstractfile0 & ": " & ex.ToString)
            End Try
        End If

        Try
            File.Delete(fpath)
        Catch ex As Exception
            Console.WriteLine("Error deleting " & fpath & ": " & ex.ToString)
        End Try
        Try
            Directory.Delete(fpath2, True)
        Catch ex As Exception
            Console.WriteLine("Error deleting " & fpath2 & ": " & ex.ToString)
        End Try

        If abstractfile <> "" Then
            Task.Factory.StartNew(Sub()
                                      Dim p = Process.Start(abstractfile)
                                      p?.WaitForExit()
                                      If ConfirmAbstractFileDeletion IsNot Nothing AndAlso
                                         ConfirmAbstractFileDeletion.Invoke(abstractfile) Then
                                          Try
                                              File.Delete(abstractfile)
                                          Catch ex As Exception
                                              Console.WriteLine("Error deleting " & abstractfile & ": " & ex.Message)
                                          End Try
                                      End If
                                  End Sub)
        End If

        Return xdoc

    End Function

    Public Shared Function DownloadFlowsheet(address As String, pa As Action(Of Integer)) As String

        Dim wc As New WebClient()

        Dim siteUri As Uri = New Uri(address)
        Dim proxyUri As Uri = Net.WebRequest.GetSystemWebProxy.GetProxy(siteUri)

        Try
            If Not siteUri.AbsolutePath = proxyUri.AbsolutePath Then
                Dim proxyObj As New WebProxy(proxyUri)
                proxyObj.Credentials = CredentialCache.DefaultCredentials
                wc.Proxy = proxyObj
            End If
        Catch ex As Exception
        End Try

        Dim fpath = Utility.GetTempFileName()

        AddHandler wc.DownloadProgressChanged, Sub(sender, e)
                                                   If pa IsNot Nothing Then pa.Invoke(e.ProgressPercentage)
                                               End Sub

        Dim t = wc.DownloadFileTaskAsync(New Uri(address), fpath)

        While Not t.IsCompleted
            Thread.Sleep(100)
        End While

        Return fpath

    End Function

    Private Shared Function LoadZippedXML(pathtofile As String) As XDocument

        Dim pathtosave As String = Path.Combine(Path.GetTempPath(), "FOSSEE_DWSIM_TEMP_EXTRACT")
        Directory.CreateDirectory(pathtosave)
        Dim fullname As String = ""

        ZipFile.ExtractToDirectory(pathtofile, pathtosave)

        For Each file In Directory.GetFiles(pathtosave)
            If Path.GetExtension(file).ToLower = ".xml" Or Path.GetExtension(file).ToLower = ".dwxml" Then
                fullname = file
            End If
        Next

        Dim xdoc = XDocument.Load(fullname)
        Try
            File.Delete(fullname)
        Catch ex As Exception
        End Try
        Try
            Directory.Delete(pathtosave)
        Catch ex As Exception
        End Try

        Return xdoc

    End Function

End Class
