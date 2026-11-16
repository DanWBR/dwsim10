Imports System.Collections.Concurrent
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Xml

''' <summary>
''' What the welcome screens show about a simulation file without loading it: the name, author and
''' comments of the simulation, when and with which version it was saved, its property packages,
''' compounds and objects, and the flowsheet picture stored with it. Read with a streaming XML reader
''' that keeps only these fields, so a large file takes a fraction of a second.
''' </summary>
Public Class FlowsheetFileInfo

    ''' <summary>Name of the flowsheet picture inside a .dwxmz file.</summary>
    Public Const PreviewEntryName As String = "preview.png"

    ''' <summary>Largest size of the stored picture, in pixels.</summary>
    Public Const PreviewMaxWidth As Integer = 640
    Public Const PreviewMaxHeight As Integer = 400

    Public Property FilePath As String = ""
    Public Property SizeBytes As Long
    Public Property LastModified As Date

    Public Property Title As String = ""
    Public Property Author As String = ""
    Public Property Comments As String = ""

    ' the flowsheet metadata of the simulation settings (process type, description, key compounds)
    Public Property ProcessType As String = ""
    Public Property ProcessDescription As String = ""
    Public Property KeyCompounds As New List(Of String)
    Public Property KeyReactants As New List(Of String)
    Public Property KeyProducts As New List(Of String)
    Public Property SavedOn As Date?
    Public Property BuildVersion As String = ""
    Public Property SavedFromClassicUI As Boolean?

    Public Property PropertyPackages As New List(Of String)
    Public Property Compounds As New List(Of String)
    ''' <summary>Count of each kind of flowsheet object, by display name, most frequent first.</summary>
    Public Property ObjectCounts As New List(Of KeyValuePair(Of String, Integer))
    Public Property MaterialStreams As Integer
    Public Property EnergyStreams As Integer

    ''' <summary>The stored flowsheet picture (PNG), or Nothing.</summary>
    Public Property Preview As Byte()

    Public Property IsPasswordProtected As Boolean
    ''' <summary>Why the file couldn't be read, or empty.</summary>
    Public Property ErrorMessage As String = ""

    ''' <summary>More lines for the details panel, shown after the author (used for the Case Library).</summary>
    Public Property ExtraDetails As New List(Of KeyValuePair(Of String, String))

    ''' <summary>Components of the Patreon Edition a Case Library case uses.</summary>
    Public Property PatreonComponents As New List(Of String)

    ''' <summary>Something the user must know before opening it (a Case Library case this DWSIM can't open), or empty.</summary>
    Public Property Warning As String = ""

    Private Shared ReadOnly Cache As New ConcurrentDictionary(Of String, FlowsheetFileInfo)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>
    ''' What the Case Library says about a case, from the README and the picture next to it in the
    ''' repository (a few kilobytes; the flowsheet itself isn't downloaded). Kept for the session.
    ''' </summary>
    Public Shared Function ReadCaseLibrary(item As CaseLibraryFlowsheet) As FlowsheetFileInfo

        Dim key = "caselibrary:" & item.DownloadUrl
        Dim cached As FlowsheetFileInfo = Nothing
        If Cache.TryGetValue(key, cached) Then Return cached

        Dim info As New FlowsheetFileInfo With {.Title = item.Title}
        info.ExtraDetails.Add(New KeyValuePair(Of String, String)("Category", item.Category))

        Try
            Dim slash = item.DownloadUrl.LastIndexOf("/"c)
            Dim readme = CaseLibraryFlowsheets.DownloadString(item.DownloadUrl.Substring(0, slash + 1) & "README.md")
            ParseReadme(readme, info)
        Catch ex As Exception
            info.ErrorMessage = "The case description couldn't be downloaded (" & ex.Message & ")."
        End Try

        info.PatreonComponents.AddRange(item.PatreonComponents)
        Dim missing = item.MissingComponents
        If missing.Count > 0 Then
            info.Warning = "This case needs DWSIM Patreon Edition (" & String.Join(", ", missing) & ") and doesn't open here."
        End If

        Try
            If item.PreviewUrl <> "" Then info.Preview = CaseLibraryFlowsheets.DownloadBytes(item.PreviewUrl)
        Catch ex As Exception
        End Try

        ' a failed download is tried again the next time the case is shown
        If info.ErrorMessage = "" Then Cache(key) = info
        Return info

    End Function

    ''' <summary>What the FOSSEE list says about one of its flowsheets (nothing is downloaded).</summary>
    Public Shared Function FromFossee(item As FOSSEEFlowsheet) As FlowsheetFileInfo
        Dim info As New FlowsheetFileInfo With {.Title = item.Title, .Author = item.ProposerName}
        Dim add = Sub(k As String, v As String)
                      If Not String.IsNullOrWhiteSpace(v) Then info.ExtraDetails.Add(New KeyValuePair(Of String, String)(k, v.Trim()))
                  End Sub
        add("Institution", item.Institution)
        add("Built with DWSIM", item.DWSIMVersion)
        add("Reference", item.Reference)
        Return info
    End Function

    ''' <summary>
    ''' The title (first heading), the "**Field:** value" lines and the Summary section of a case README.
    ''' </summary>
    Private Shared Sub ParseReadme(md As String, info As FlowsheetFileInfo)

        Dim lines = md.Replace(vbCr, "").Split(ChrW(10))
        Dim inSummary = False
        Dim summary As New List(Of String)

        For Each raw In lines
            Dim line = raw.Trim()
            If line.StartsWith("# ") AndAlso Not inSummary AndAlso summary.Count = 0 Then
                info.Title = PlainText(line.Substring(2))
            ElseIf line.StartsWith("## ") Then
                If summary.Count > 0 Then Exit For
                inSummary = line.Substring(3).Trim().Equals("Summary", StringComparison.OrdinalIgnoreCase)
            ElseIf inSummary Then
                If line = "" Then
                    If summary.Count > 0 Then summary.Add("")
                ElseIf Not line.StartsWith("!") Then
                    summary.Add(PlainText(line))
                End If
            Else
                Dim m = Regex.Match(line, "^\*\*(.+?):\*\*\s*(.+)$")
                If m.Success Then
                    Dim name = m.Groups(1).Value.Trim()
                    Dim value = PlainText(m.Groups(2).Value)
                    Select Case name.ToLowerInvariant()
                        Case "contributor" : info.Author = value
                        Case "category", "language"
                        Case "dwsim version" : info.ExtraDetails.Add(New KeyValuePair(Of String, String)("Built with DWSIM", value))
                        Case Else : info.ExtraDetails.Add(New KeyValuePair(Of String, String)(name, value))
                    End Select
                End If
            End If
        Next

        info.Comments = String.Join(vbCrLf, summary).Trim()

    End Sub

    ''' <summary>Markdown emphasis, code marks and links reduced to their text.</summary>
    Private Shared Function PlainText(s As String) As String
        s = Regex.Replace(s, "!?\[([^\]]*)\]\([^)]*\)", "$1")
        s = s.Replace("**", "").Replace("`", "")
        s = Regex.Replace(s, "(?<!\w)_(.+?)_(?!\w)", "$1")
        Return s.Trim()
    End Function

    ''' <summary>
    ''' Reads the file, or returns the copy read before when the file hasn't changed since.
    ''' </summary>
    Public Shared Function Read(path As String) As FlowsheetFileInfo

        Dim fi As New FileInfo(path)
        If Not fi.Exists Then Return New FlowsheetFileInfo With {.FilePath = path, .ErrorMessage = "The file doesn't exist."}

        Dim cached As FlowsheetFileInfo = Nothing
        If Cache.TryGetValue(path, cached) AndAlso cached.SizeBytes = fi.Length AndAlso cached.LastModified = fi.LastWriteTime Then
            Return cached
        End If

        Dim info As New FlowsheetFileInfo With {.FilePath = path, .SizeBytes = fi.Length, .LastModified = fi.LastWriteTime}

        Try
            Select Case fi.Extension.ToLowerInvariant()
                Case ".dwxmz", ".zip"
                    ReadZip(path, info)
                Case ".dwxml", ".xml"
                    Using fs = File.OpenRead(path)
                        ReadXml(fs, info)
                    End Using
                Case Else
                    info.ErrorMessage = "Details aren't available for this kind of file."
            End Select
        Catch ex As Exception
            info.ErrorMessage = ex.Message
        End Try

        Cache(path) = info
        Return info

    End Function

    Private Shared Sub ReadZip(path As String, info As FlowsheetFileInfo)

        Using za = ZipFile.OpenRead(path)
            Dim xmlEntry = za.Entries.FirstOrDefault(Function(e) e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            If xmlEntry Is Nothing Then
                info.ErrorMessage = "The file doesn't contain a simulation."
                Return
            End If
            Try
                Using s = xmlEntry.Open()
                    ReadXml(s, info)
                End Using
            Catch ex As InvalidDataException
                ' entries written with a password can't be opened without it
                info.IsPasswordProtected = True
                info.ErrorMessage = "The file is protected with a password."
                Return
            End Try
            Dim png = za.Entries.FirstOrDefault(Function(e) e.Name.Equals(PreviewEntryName, StringComparison.OrdinalIgnoreCase))
            If png IsNot Nothing Then
                Using s = png.Open(), ms As New MemoryStream()
                    s.CopyTo(ms)
                    info.Preview = ms.ToArray()
                End Using
            End If
        End Using

    End Sub

    Private Shared Sub ReadXml(stream As Stream, info As FlowsheetFileInfo)

        Dim types As New Dictionary(Of String, Integer)
        Dim settings As New XmlReaderSettings With {.IgnoreComments = True, .IgnoreWhitespace = True, .DtdProcessing = DtdProcessing.Prohibit}

        Using r = XmlReader.Create(stream, settings)
            r.MoveToContent()
            If r.NodeType <> XmlNodeType.Element Then Return
            r.Read()
            While Not r.EOF
                If r.NodeType <> XmlNodeType.Element OrElse r.Depth <> 1 Then
                    r.Read()
                    Continue While
                End If
                Select Case r.LocalName
                    Case "GeneralInfo"
                        ReadFields(r, Sub(name, value)
                                          Select Case name
                                              Case "BuildVersion" : info.BuildVersion = value
                                              Case "SavedOn"
                                                  Dim d As Date
                                                  If Date.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, d) Then info.SavedOn = d.ToLocalTime()
                                              Case "SavedFromClassicUI"
                                                  Dim b As Boolean
                                                  If Boolean.TryParse(value, b) Then info.SavedFromClassicUI = b
                                          End Select
                                      End Sub, "BuildVersion", "SavedOn", "SavedFromClassicUI")
                    Case "Settings"
                        ReadSettings(r, info)
                    Case "SimulationObjects"
                        ReadChildren(r, "SimulationObject", Sub(fields)
                                                                Dim t As String = Nothing
                                                                If fields.TryGetValue("Type", t) Then
                                                                    Dim name = DisplayName(t)
                                                                    If types.ContainsKey(name) Then types(name) += 1 Else types(name) = 1
                                                                End If
                                                            End Sub, "Type")
                    Case "PropertyPackages"
                        ReadChildren(r, "PropertyPackage", Sub(fields)
                                                               Dim model As String = Nothing, tag As String = Nothing
                                                               fields.TryGetValue("ComponentName", model)
                                                               fields.TryGetValue("Tag", tag)
                                                               info.PropertyPackages.Add(PackageName(model, tag))
                                                           End Sub, "ComponentName", "Tag")
                    Case "Compounds"
                        ReadChildren(r, "Compound", Sub(fields)
                                                        Dim n As String = Nothing
                                                        If fields.TryGetValue("Name", n) AndAlso n <> "" Then info.Compounds.Add(n)
                                                    End Sub, "Name")
                    Case Else
                        r.Skip()
                End Select
            End While
        End Using

        Dim ms = "Material Stream", es = "Energy Stream"
        If types.ContainsKey(ms) Then info.MaterialStreams = types(ms) : types.Remove(ms)
        If types.ContainsKey(es) Then info.EnergyStreams = types(es) : types.Remove(es)
        info.ObjectCounts = types.OrderByDescending(Function(kv) kv.Value).ThenBy(Function(kv) kv.Key).ToList()

        If info.ProcessDescription <> "" AndAlso Not info.Comments.Contains(info.ProcessDescription) Then
            info.Comments = If(info.Comments = "", info.ProcessDescription, info.ProcessDescription & vbCrLf & vbCrLf & info.Comments)
        End If

    End Sub

    ''' <summary>The name, author and comments of the simulation and its flowsheet metadata.</summary>
    Private Shared Sub ReadSettings(r As XmlReader, info As FlowsheetFileInfo)
        If r.IsEmptyElement Then r.Read() : Return
        Dim depth = r.Depth
        r.Read()
        While Not r.EOF AndAlso r.Depth > depth
            If r.NodeType = XmlNodeType.Element AndAlso r.Depth = depth + 1 Then
                Select Case r.LocalName
                    Case "SimulationName" : info.Title = r.ReadElementContentAsStringSafe().Trim()
                    Case "SimulationAuthor" : info.Author = r.ReadElementContentAsStringSafe().Trim()
                    Case "SimulationComments" : info.Comments = r.ReadElementContentAsStringSafe().Trim()
                    Case "Metadata"
                        Dim md = TryCast(Linq.XNode.ReadFrom(r), Linq.XElement)
                        If md IsNot Nothing Then ReadMetadata(md, info)
                    Case Else
                        r.Skip()
                End Select
            Else
                r.Read()
            End If
        End While
    End Sub

    Private Shared Sub ReadMetadata(md As Linq.XElement, info As FlowsheetFileInfo)
        Dim items = Function(name As String) As List(Of String)
                        Dim el = md.Element(name)
                        If el Is Nothing Then Return New List(Of String)
                        Return el.Elements().Select(Function(e) e.Value.Trim()).Where(Function(v) v <> "").ToList()
                    End Function
        Dim pt = If(md.Element("ProcessType")?.Value, "").Trim()
        If pt <> "" AndAlso pt <> "Unspecified" Then
            info.ProcessType = Regex.Replace(pt, "(?<=[a-z])(?=[A-Z])", " ")
            info.ProcessType = info.ProcessType.Substring(0, 1) & info.ProcessType.Substring(1).ToLowerInvariant()
        End If
        info.ProcessDescription = If(md.Element("ProcessDescription")?.Value, "").Trim()
        info.KeyCompounds = items("KeyCompounds")
        info.KeyReactants = items("KeyReactants")
        info.KeyProducts = items("KeyProducts")
    End Sub

    ''' <summary>
    ''' Calls back with the text of each listed direct child of the current element; the other
    ''' children are skipped. Leaves the reader on the end of the current element.
    ''' </summary>
    Private Shared Sub ReadFields(r As XmlReader, field As Action(Of String, String), ParamArray wanted As String())
        If r.IsEmptyElement Then r.Read() : Return
        Dim depth = r.Depth
        r.Read()
        While Not r.EOF AndAlso r.Depth > depth
            If r.NodeType = XmlNodeType.Element AndAlso r.Depth = depth + 1 Then
                If Not wanted.Contains(r.LocalName) Then
                    r.Skip()
                ElseIf r.IsEmptyElement Then
                    field(r.LocalName, "")
                    r.Read()
                Else
                    Dim name = r.LocalName
                    field(name, r.ReadElementContentAsStringSafe())
                End If
            Else
                r.Read()
            End If
        End While
    End Sub

    ''' <summary>
    ''' For each child element named childName of the current element, collects the text of the
    ''' listed direct fields and calls back with them; the rest of each child is skipped.
    ''' </summary>
    Private Shared Sub ReadChildren(r As XmlReader, childName As String, item As Action(Of Dictionary(Of String, String)), ParamArray wanted As String())
        If r.IsEmptyElement Then r.Read() : Return
        Dim depth = r.Depth
        r.Read()
        While Not r.EOF AndAlso r.Depth > depth
            If r.NodeType = XmlNodeType.Element AndAlso r.Depth = depth + 1 AndAlso r.LocalName = childName Then
                Dim fields As New Dictionary(Of String, String)
                If r.IsEmptyElement Then
                    r.Read()
                Else
                    Dim cdepth = r.Depth
                    r.Read()
                    While Not r.EOF AndAlso r.Depth > cdepth
                        If r.NodeType = XmlNodeType.Element AndAlso r.Depth = cdepth + 1 Then
                            If wanted.Contains(r.LocalName) AndAlso Not fields.ContainsKey(r.LocalName) AndAlso Not r.IsEmptyElement Then
                                Dim name = r.LocalName
                                fields(name) = r.ReadElementContentAsStringSafe()
                            Else
                                r.Skip()
                            End If
                        Else
                            r.Read()
                        End If
                    End While
                End If
                item(fields)
            ElseIf r.NodeType = XmlNodeType.Element AndAlso r.Depth = depth + 1 Then
                r.Skip()
            Else
                r.Read()
            End If
        End While
    End Sub

    Private Shared Function PackageName(model As String, tag As String) As String
        model = If(model, "").Trim()
        tag = If(tag, "").Trim()
        If tag = "" Then Return model
        If model = "" OrElse tag.StartsWith(model, StringComparison.OrdinalIgnoreCase) Then Return model
        Return tag & " (" & model & ")"
    End Function

    ''' <summary>"DWSIM.UnitOperations.UnitOperations.DistillationColumn" gives "Distillation Column".</summary>
    Friend Shared Function DisplayName(typeName As String) As String
        Dim n = If(typeName, "").Trim()
        Dim plus = n.IndexOf("+"c)
        If plus >= 0 Then n = n.Substring(plus + 1)
        Dim dot = n.LastIndexOf("."c)
        If dot >= 0 Then n = n.Substring(dot + 1)
        Dim tick = n.IndexOf("`"c)
        If tick >= 0 Then n = n.Substring(0, tick)
        n = n.Replace("_", " ")
        n = Regex.Replace(n, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ")
        Return Regex.Replace(n, "\s+", " ").Trim()
    End Function

    ''' <summary>A block of the details panel: a header and either short items (shown as tags) or label/value facts.</summary>
    Public Class DetailSection
        Public Property Header As String = ""
        Public Property Items As New List(Of String)
        Public Property Facts As New List(Of KeyValuePair(Of String, String))
    End Class

    ''' <summary>Author, save date and interface, the line under the title.</summary>
    Public Function Subtitle() As String
        Dim parts As New List(Of String)
        If Not String.IsNullOrWhiteSpace(Author) Then parts.Add(Author)
        If SavedOn.HasValue Then parts.Add("saved " & FormatDate(SavedOn.Value))
        If SavedFromClassicUI.HasValue Then parts.Add(If(SavedFromClassicUI.Value, "Classic UI", "Cross-Platform UI"))
        Return String.Join(Sep, parts)
    End Function

    ''' <summary>The blocks of the panel under the description, in display order; empty ones are left out.</summary>
    Public Function GetSections() As List(Of DetailSection)

        Dim list As New List(Of DetailSection)

        If ExtraDetails.Count > 0 Then
            list.Add(New DetailSection With {.Header = "About", .Facts = ExtraDetails.Where(Function(kv) Not String.IsNullOrWhiteSpace(kv.Value)).ToList()})
        End If

        If PatreonComponents.Count > 0 Then
            list.Add(New DetailSection With {.Header = "Patreon Edition components", .Items = PatreonComponents.ToList()})
        End If

        If ProcessType <> "" Then list.Add(New DetailSection With {.Header = "Process", .Items = New List(Of String) From {ProcessType}})
        If KeyCompounds.Count > 0 Then list.Add(New DetailSection With {.Header = "Key compounds", .Items = KeyCompounds.ToList()})
        If KeyReactants.Count > 0 Then list.Add(New DetailSection With {.Header = "Reactants", .Items = KeyReactants.ToList()})
        If KeyProducts.Count > 0 Then list.Add(New DetailSection With {.Header = "Products", .Items = KeyProducts.ToList()})

        If PropertyPackages.Count > 0 Then
            list.Add(New DetailSection With {
                .Header = If(PropertyPackages.Count > 1, "Property packages", "Property package"),
                .Items = PropertyPackages.GroupBy(Function(n) n).Select(Function(g) If(g.Count() > 1, g.Key & "  " & ChrW(215) & g.Count().ToString(), g.Key)).ToList()})
        End If

        If Compounds.Count > 0 Then
            Dim shown = Compounds.Take(24).ToList()
            If Compounds.Count > shown.Count Then shown.Add("+" & (Compounds.Count - shown.Count).ToString() & " more")
            list.Add(New DetailSection With {.Header = "Compounds (" & Compounds.Count.ToString() & ")", .Items = shown})
        End If

        Dim objects As New List(Of String)
        If MaterialStreams > 0 Then objects.Add(Plural(MaterialStreams, "material stream"))
        If EnergyStreams > 0 Then objects.Add(Plural(EnergyStreams, "energy stream"))
        For Each kv In ObjectCounts
            objects.Add(If(kv.Value > 1, kv.Key & "  " & ChrW(215) & kv.Value.ToString(), kv.Key))
        Next
        If objects.Count > 0 Then list.Add(New DetailSection With {.Header = "Flowsheet", .Items = objects})

        Return list

    End Function

    ''' <summary>The small print at the bottom: file, size, dates, version and folder.</summary>
    Public Function FooterLines() As List(Of String)
        Dim lines As New List(Of String)
        If FilePath = "" Then Return lines
        lines.Add(Path.GetFileName(FilePath) & Sep & FormatSize(SizeBytes) & Sep & "modified " & FormatDate(LastModified))
        If BuildVersion <> "" Then lines.Add("Saved with DWSIM " & BuildVersion.Trim())
        lines.Add(Path.GetDirectoryName(FilePath))
        Return lines
    End Function

    ''' <summary>Two spaces, a middle dot and two spaces.</summary>
    Private Shared ReadOnly Sep As String = "  " & ChrW(183) & "  "

    Private Shared Function FormatDate(d As Date) As String
        Return d.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function Plural(n As Integer, noun As String) As String
        Return n.ToString() & " " & noun & If(n = 1, "", "s")
    End Function

    ''' <summary>The simulation name, or the file name without extension when the simulation doesn't have one.</summary>
    Public Function DisplayTitle() As String
        If Not String.IsNullOrWhiteSpace(Title) Then Return Title
        Return Path.GetFileNameWithoutExtension(FilePath)
    End Function

    Private Shared Function FormatSize(bytes As Long) As String
        If bytes < 1024 Then Return bytes.ToString() & " B"
        If bytes < 1024 * 1024 Then Return (bytes / 1024.0).ToString("0") & " kB"
        Return (bytes / 1024.0 / 1024.0).ToString("0.0") & " MB"
    End Function

End Class

Friend Module XmlReaderExtensions

    ''' <summary>The text of the current element, or empty when it holds child elements instead of text.</summary>
    <System.Runtime.CompilerServices.Extension>
    Friend Function ReadElementContentAsStringSafe(r As XmlReader) As String
        Try
            Return r.ReadElementContentAsString()
        Catch ex As XmlException
            r.Skip()
            Return ""
        End Try
    End Function

End Module
