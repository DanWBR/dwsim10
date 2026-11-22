Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Namespace ProcessCatalog

    ''' <summary>One catalog file: the processes of an industry.</summary>
    Public Class CatalogFile
        Public Property Industry As String = ""
        Public Property Processes As New List(Of ProcessDefinition)
    End Class

    ''' <summary>
    ''' A typical process that the simulation wizard offers: the compounds, the property package, the
    ''' reactions and the units it needs, and the skeleton of a starting flowsheet.
    ''' </summary>
    Public Class ProcessDefinition

        Public Property Id As String = ""
        Public Property Name As String = ""
        Public Property Industry As String = ""
        Public Property Description As String = ""

        ''' <summary>A <see cref="Enums.ProcessType"/> name, saved in the simulation metadata.</summary>
        Public Property ProcessType As String = "Unspecified"

        ''' <summary>The Patreon access level the process needs. When it is not given, the loader takes
        ''' the highest level of the external unit operations of the skeleton.</summary>
        Public Property RequiredLevel As Integer? = Nothing

        Public Property Compounds As New List(Of String)
        Public Property KeyCompounds As New List(Of String)
        Public Property Reactants As New List(Of String)
        Public Property Products As New List(Of String)

        Public Property PropertyPackage As New PackageSpec

        ''' <summary>The name of a system of units (SI, CGS, ENG, C1...), or empty to keep the current one.</summary>
        Public Property UnitSystem As String = ""

        Public Property Reactions As New List(Of ReactionSpec)

        Public Property Skeleton As SkeletonSpec

        ''' <summary>Ready-made simulations of this process in the samples and in the Case Library, which the
        ''' wizard offers to open in place of the new simulation.</summary>
        Public Property Examples As New List(Of ExampleSpec)

        ''' <summary>True when the process only points to ready-made simulations: it doesn't set up the
        ''' compounds and the property package, and doesn't have a skeleton.</summary>
        <JsonIgnore> Public ReadOnly Property IsExampleOnly As Boolean
            Get
                Return Compounds.Count = 0 AndAlso String.IsNullOrWhiteSpace(PropertyPackage?.Name) AndAlso
                       (Skeleton Is Nothing OrElse Skeleton.Objects.Count = 0)
            End Get
        End Property

        ''' <summary>Where the definition came from (a resource name or a file path), for messages.</summary>
        <JsonIgnore> Public Property Source As String = ""

    End Class

    ''' <summary>The property package: its name as listed by the flowsheet, an optional package of the open
    ''' edition to use when the first one isn't available, and properties or methods to set on it.</summary>
    Public Class PackageSpec
        Public Property Name As String = ""
        Public Property Fallback As String = ""
        Public Property Properties As New Dictionary(Of String, JToken)
        Public Property Calls As New List(Of CallSpec)
    End Class

    ''' <summary>A reaction for the default reaction set. Type is Conversion, Equilibrium or Kinetic.</summary>
    Public Class ReactionSpec
        Public Property Type As String = "Conversion"
        Public Property Name As String = ""
        Public Property Description As String = ""
        Public Property Stoichiometry As New Dictionary(Of String, Double)
        Public Property BaseCompound As String = ""
        Public Property Phase As String = "Mixture"
        ' conversion
        Public Property Conversion As String = ""
        ' equilibrium
        Public Property Basis As String = "Fugacity"
        Public Property Units As String = "Pa"
        Public Property TApproach As Double = 0.0
        Public Property LnKeq As String = ""
        ' kinetic
        Public Property DirectOrders As New Dictionary(Of String, Double)
        Public Property ReverseOrders As New Dictionary(Of String, Double)
        Public Property AmountUnits As String = "mol/m3"
        Public Property RateUnits As String = "mol/[m3.s]"
        Public Property AForward As Double = 0.0
        Public Property EForward As Double = 0.0
        Public Property AReverse As Double = 0.0
        Public Property EReverse As Double = 0.0
        Public Property ExprForward As String = ""
        Public Property ExprReverse As String = ""
    End Class

    ''' <summary>The starting flowsheet: its objects, how they connect, and the layout to apply.</summary>
    Public Class SkeletonSpec
        Public Property Objects As New List(Of ObjectSpec)
        Public Property Connections As New List(Of ConnectionSpec)
        ''' <summary>"orthogonal" (default), "auto" or "none".</summary>
        Public Property Layout As String = "orthogonal"
    End Class

    ''' <summary>
    ''' An object of the skeleton. Type is an <see cref="ObjectType"/> name (MaterialStream, Heater,
    ''' RCT_Equilibrium...); External is the display name of an external unit operation and replaces Type.
    ''' Streams take a state in SI units (K, Pa, kg/s, mol/s) and a composition. Properties are set by
    ''' name on the object, and Calls are methods run after the connections (a string argument that
    ''' starts with @ is the object with that tag).
    ''' </summary>
    Public Class ObjectSpec
        Public Property Tag As String = ""
        Public Property Type As String = ""
        Public Property External As String = ""
        Public Property X As Integer? = Nothing
        Public Property Y As Integer? = Nothing
        Public Property Temperature As Double? = Nothing
        Public Property Pressure As Double? = Nothing
        Public Property MassFlow As Double? = Nothing
        Public Property MolarFlow As Double? = Nothing
        Public Property Composition As New Dictionary(Of String, Double)
        ''' <summary>"mole" (default) or "mass".</summary>
        Public Property CompositionBasis As String = "mole"
        Public Property Properties As New Dictionary(Of String, JToken)
        Public Property Calls As New List(Of CallSpec)
    End Class

    ''' <summary>A connection between two objects of the skeleton. A port of -1 takes the first free one.</summary>
    Public Class ConnectionSpec
        Public Property From As String = ""
        Public Property [To] As String = ""
        Public Property FromPort As Integer = -1
        Public Property ToPort As Integer = -1
    End Class

    ''' <summary>A method to call on an object, with its arguments.</summary>
    Public Class CallSpec
        Public Property Method As String = ""
        Public Property Args As New List(Of JToken)
    End Class

    ''' <summary>
    ''' A ready-made simulation of a process: a file of the samples folder (Sample, the file name) or a case of
    ''' the Case Library (Case, the folder of the case under 'cases', such as "other/raw-sugar-mill").
    ''' </summary>
    Public Class ExampleSpec
        Public Property Title As String = ""
        Public Property Sample As String = ""
        Public Property [Case] As String = ""

        <JsonIgnore> Public ReadOnly Property IsCase As Boolean
            Get
                Return Not String.IsNullOrWhiteSpace([Case])
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return If(IsCase, "Case Library: ", "Sample: ") & Title
        End Function
    End Class

    ''' <summary>Finds and fetches the ready-made simulations that the catalog points to.</summary>
    Public NotInheritable Class ProcessExamples

        Private Sub New()
        End Sub

        ''' <summary>
        ''' The samples folder: next to the program, in the DWSIM install folder, or in the repository
        ''' (PlatformFiles\Common\samples) when DWSIM runs from a build. Nothing when none is found.
        ''' </summary>
        Public Shared Function FindSamplesDirectory() As String
            Dim baseDir = AppDomain.CurrentDomain.BaseDirectory
            Dim candidates As New List(Of String) From {Path.Combine(baseDir, "samples")}
            Dim local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            If Not String.IsNullOrEmpty(local) Then candidates.Add(Path.Combine(local, "DWSIM", "samples"))
            Dim dir = New DirectoryInfo(baseDir)
            While dir IsNot Nothing
                candidates.Add(Path.Combine(dir.FullName, "PlatformFiles", "Common", "samples"))
                dir = dir.Parent
            End While
            Return candidates.FirstOrDefault(Function(d) Directory.Exists(d))
        End Function

        ''' <summary>The full path of a sample file, or Nothing when it isn't in the samples folder.</summary>
        Public Shared Function SamplePath(sample As String) As String
            Dim dir = FindSamplesDirectory()
            If dir Is Nothing OrElse String.IsNullOrWhiteSpace(sample) Then Return Nothing
            Dim p = Path.Combine(dir, sample)
            Return If(File.Exists(p), p, Nothing)
        End Function

        ''' <summary>The examples of a process that this installation can open: samples that are present and
        ''' every Case Library case (those are downloaded when opened).</summary>
        Public Shared Function Available(p As ProcessDefinition) As List(Of ExampleSpec)
            Return p.Examples.Where(Function(e) e.IsCase OrElse SamplePath(e.Sample) IsNot Nothing).ToList()
        End Function

        ''' <summary>
        ''' Returns the local file of an example: the sample itself, or the Case Library case downloaded to a
        ''' temporary file. Downloading needs the network; it throws with a message when the case isn't found.
        ''' </summary>
        Public Shared Function GetFile(e As ExampleSpec, Optional progress As Action(Of Integer) = Nothing) As String
            If Not e.IsCase Then
                Dim sp = SamplePath(e.Sample)
                If sp Is Nothing Then Throw New FileNotFoundException("The sample isn't in the samples folder: " & e.Sample)
                Return sp
            End If
            Dim note As String = Nothing
            Dim cases = CaseLibraryFlowsheets.GetCaseLibraryFlowsheets(note)
            Dim key = "/" & e.Case.Trim("/"c) & "/"
            Dim found = cases?.FirstOrDefault(Function(c) ("/" & c.Path.Replace("\", "/")).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
            If found Is Nothing Then Throw New Exception("The case '" & e.Case & "' wasn't found in the Case Library" & If(String.IsNullOrEmpty(note), ".", " (" & note & ")."))
            Return CaseLibraryFlowsheets.DownloadFlowsheet(found.DownloadUrl, If(progress, Sub(x)
                                                                                               End Sub))
        End Function

    End Class

    ''' <summary>Reads the process catalog: the files built into DWSIM and, optionally, a folder of the user's.</summary>
    Public NotInheritable Class ProcessCatalogLoader

        Private Const ResourcePrefix As String = "DWSIM.SharedClasses.ProcessCatalog."

        Private Sub New()
        End Sub

        ''' <summary>The processes of every catalog file built into DWSIM, in the order of the industries.</summary>
        Public Shared Function LoadBuiltIn(Optional errors As List(Of String) = Nothing) As List(Of ProcessDefinition)
            Dim list As New List(Of ProcessDefinition)
            Dim asm = GetType(ProcessCatalogLoader).Assembly
            For Each res In asm.GetManifestResourceNames().Where(Function(n) n.StartsWith(ResourcePrefix, StringComparison.Ordinal) AndAlso
                                                                     n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).OrderBy(Function(n) n)
                Try
                    Using s = asm.GetManifestResourceStream(res), r As New StreamReader(s)
                        list.AddRange(Parse(r.ReadToEnd(), res))
                    End Using
                Catch ex As Exception
                    errors?.Add(res & ": " & ex.Message)
                End Try
            Next
            Return Sort(list)
        End Function

        ''' <summary>The processes of the .json files in a folder. A process with the id of a built-in one replaces it.</summary>
        Public Shared Function LoadFolder(folder As String, Optional errors As List(Of String) = Nothing) As List(Of ProcessDefinition)
            Dim list As New List(Of ProcessDefinition)
            If String.IsNullOrEmpty(folder) OrElse Not Directory.Exists(folder) Then Return list
            For Each f In Directory.GetFiles(folder, "*.json")
                Try
                    list.AddRange(Parse(File.ReadAllText(f), f))
                Catch ex As Exception
                    errors?.Add(f & ": " & ex.Message)
                End Try
            Next
            Return list
        End Function

        ''' <summary>The built-in processes, with those of the user's folder added or replacing them by id.</summary>
        Public Shared Function LoadAll(Optional userFolder As String = Nothing, Optional errors As List(Of String) = Nothing) As List(Of ProcessDefinition)
            Dim all = LoadBuiltIn(errors)
            For Each p In LoadFolder(userFolder, errors)
                all.RemoveAll(Function(x) String.Equals(x.Id, p.Id, StringComparison.OrdinalIgnoreCase))
                all.Add(p)
            Next
            Return Sort(all)
        End Function

        ''' <summary>Reads one catalog file. A process without an industry takes the one of the file.</summary>
        Public Shared Function Parse(json As String, source As String) As List(Of ProcessDefinition)
            Dim cf = JsonConvert.DeserializeObject(Of CatalogFile)(json)
            Dim list As New List(Of ProcessDefinition)
            If cf Is Nothing Then Return list
            For Each p In cf.Processes
                If String.IsNullOrWhiteSpace(p.Industry) Then p.Industry = cf.Industry
                p.Source = source
                list.Add(p)
            Next
            Return list
        End Function

        Private Shared Function Sort(list As List(Of ProcessDefinition)) As List(Of ProcessDefinition)
            Dim order = PaletteIndustry.Sort(list.Select(Function(p) p.Industry))
            Return list.OrderBy(Function(p) order.IndexOf(p.Industry)).ThenBy(Function(p) p.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
        End Function

    End Class

    ''' <summary>
    ''' Applies a catalog process to a flowsheet: the compounds, the property package, the reactions, the
    ''' units and the metadata, and then, on request, the skeleton of the starting flowsheet. It works on any
    ''' <see cref="IFlowsheet"/> (the Classic and cross-platform interfaces and the headless flowsheets),
    ''' and reports a problem as a message instead of stopping.
    ''' </summary>
    Public NotInheritable Class ProcessApplier

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Adds an external unit operation (a template, a position and a tag) to the flowsheet and returns
        ''' the new object. Each host adds it to its own drawing surface.
        ''' </summary>
        Public Delegate Function AddExternalHandler(template As IExternalUnitOperation, x As Integer, y As Integer, tag As String) As ISimulationObject

        ''' <summary>Checks a process against a flowsheet: the compounds, the package, the object types and the
        ''' external unit operations it needs. An empty list means the process can be applied as it is.</summary>
        Public Shared Function Validate(fs As IFlowsheet, p As ProcessDefinition) As List(Of String)
            Dim problems As New List(Of String)
            'a process that only points to ready-made simulations doesn't set anything up
            If p.IsExampleOnly Then Return problems
            For Each c In p.Compounds
                If FindCompoundName(fs, c) Is Nothing Then problems.Add("Compound not found: " & c)
            Next
            If ResolvePackageName(fs, p.PropertyPackage) Is Nothing Then
                problems.Add("Property package not available: " & p.PropertyPackage.Name &
                             If(String.IsNullOrEmpty(p.PropertyPackage.Fallback), "", " (nor " & p.PropertyPackage.Fallback & ")"))
            End If
            If p.Skeleton IsNot Nothing Then
                Dim tags As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                For Each o In p.Skeleton.Objects
                    If Not tags.Add(o.Tag) Then problems.Add("Duplicate tag: " & o.Tag)
                    If Not String.IsNullOrEmpty(o.External) Then
                        If FindExternal(fs, o.External) Is Nothing Then problems.Add("Unit operation not available: " & o.External)
                    Else
                        Dim t As ObjectType
                        If Not [Enum].TryParse(o.Type, True, t) Then problems.Add("Unknown object type: " & o.Type)
                    End If
                    For Each c In o.Composition.Keys
                        If Not p.Compounds.Any(Function(x) String.Equals(x, c, StringComparison.OrdinalIgnoreCase)) Then
                            problems.Add(o.Tag & ": compound not in the process list: " & c)
                        End If
                    Next
                Next
                For Each cn In p.Skeleton.Connections
                    If Not tags.Contains(cn.From) Then problems.Add("Connection from an unknown tag: " & cn.From)
                    If Not tags.Contains(cn.To) Then problems.Add("Connection to an unknown tag: " & cn.To)
                Next
            End If
            Return problems
        End Function

        ''' <summary>The Patreon level a process needs: the one it states, or the highest level of the external
        ''' unit operations of its skeleton.</summary>
        Public Shared Function GetRequiredLevel(fs As IFlowsheet, p As ProcessDefinition) As Integer
            If p.RequiredLevel.HasValue Then Return p.RequiredLevel.Value
            Dim level = 0
            If p.Skeleton IsNot Nothing Then
                For Each o In p.Skeleton.Objects.Where(Function(x) Not String.IsNullOrEmpty(x.External))
                    Dim t = If(fs IsNot Nothing, FindExternal(fs, o.External), Nothing)
                    If t IsNot Nothing Then level = Math.Max(level, Utility.GetRequiredAccessLevel(t))
                Next
            End If
            Return level
        End Function

        ''' <summary>Adds the compounds, the property package, the reactions, the units and the metadata.
        ''' Returns the messages about what couldn't be applied.</summary>
        Public Shared Function ApplySetup(fs As IFlowsheet, p As ProcessDefinition) As List(Of String)

            Dim report As New List(Of String)
            If p.IsExampleOnly Then Return report

            For Each c In p.Compounds
                Dim name = FindCompoundName(fs, c)
                If name Is Nothing Then
                    report.Add("Compound not found: " & c)
                Else
                    Try
                        fs.AddCompound(name)
                    Catch ex As Exception
                        report.Add("Compound " & c & ": " & ex.Message)
                    End Try
                End If
            Next

            Dim ppname = ResolvePackageName(fs, p.PropertyPackage)
            If ppname Is Nothing Then
                report.Add("Property package not available: " & p.PropertyPackage.Name)
            ElseIf Not fs.PropertyPackages.Values.Any(Function(x) x.ComponentName = ppname) Then
                Try
                    Dim pp = fs.CreateAndAddPropertyPackage(ppname)
                    If ppname <> p.PropertyPackage.Name Then report.Add("Using " & ppname & " in place of " & p.PropertyPackage.Name)
                    For Each kv In p.PropertyPackage.Properties
                        SetMember(pp, kv.Key, kv.Value, report, pp.ComponentName)
                    Next
                    For Each c In p.PropertyPackage.Calls
                        Invoke(pp, c, Nothing, report, pp.ComponentName)
                    Next
                Catch ex As Exception
                    report.Add("Property package " & ppname & ": " & ex.Message)
                End Try
            End If

            For Each r In p.Reactions
                Try
                    AddReaction(fs, r, p)
                Catch ex As Exception
                    report.Add("Reaction " & r.Name & ": " & ex.Message)
                End Try
            Next

            If Not String.IsNullOrWhiteSpace(p.UnitSystem) Then
                Dim su = fs.AvailableSystemsOfUnits?.FirstOrDefault(Function(u) String.Equals(u.Name, p.UnitSystem, StringComparison.OrdinalIgnoreCase))
                If su IsNot Nothing Then
                    fs.FlowsheetOptions.SelectedUnitSystem = su
                Else
                    report.Add("System of units not found: " & p.UnitSystem)
                End If
            End If

            Try
                Dim md = fs.FlowsheetOptions.Metadata
                If md IsNot Nothing Then
                    Dim pt As Enums.ProcessType
                    If [Enum].TryParse(p.ProcessType, True, pt) Then md.ProcessType = pt
                    md.ProcessDescription = p.Description
                    md.KeyCompounds = New List(Of String)(p.KeyCompounds)
                    md.KeyReactants = New List(Of String)(p.Reactants)
                    md.KeyProducts = New List(Of String)(p.Products)
                End If
            Catch ex As Exception
                report.Add("Metadata: " & ex.Message)
            End Try

            Return report

        End Function

        ''' <summary>
        ''' Builds the skeleton of the starting flowsheet: the objects, the stream states, the properties,
        ''' the connections, the method calls and the layout. Call it after <see cref="ApplySetup"/>.
        ''' Returns the messages about what couldn't be built.
        ''' </summary>
        Public Shared Function BuildSkeleton(fs As IFlowsheet, p As ProcessDefinition, addExternal As AddExternalHandler) As List(Of String)

            Dim report As New List(Of String)
            If p.Skeleton Is Nothing OrElse p.Skeleton.Objects.Count = 0 Then Return report

            Dim objs As New Dictionary(Of String, ISimulationObject)(StringComparer.OrdinalIgnoreCase)
            Dim i = 0

            For Each o In p.Skeleton.Objects
                Dim x = If(o.X, 100 + 150 * (i Mod 8))
                Dim y = If(o.Y, 100 + 150 * (i \ 8))
                i += 1
                Try
                    Dim so As ISimulationObject = Nothing
                    If Not String.IsNullOrEmpty(o.External) Then
                        Dim t = FindExternal(fs, o.External)
                        If t Is Nothing Then
                            report.Add("Unit operation not available: " & o.External)
                        ElseIf addExternal Is Nothing Then
                            report.Add("This host can't add external unit operations: " & o.External)
                        Else
                            so = addExternal(t, x, y, o.Tag)
                            'an external unit operation lays out its ports itself
                            Dim ext = TryCast(so, IExternalUnitOperation)
                            If ext IsNot Nothing AndAlso so.GraphicObject IsNot Nothing AndAlso
                               so.GraphicObject.InputConnectors.Count = 0 AndAlso so.GraphicObject.OutputConnectors.Count = 0 Then
                                ext.CreateConnectors()
                            End If
                        End If
                    Else
                        so = fs.AddObject(CType([Enum].Parse(GetType(ObjectType), o.Type, True), ObjectType), x, y, o.Tag)
                    End If
                    If so Is Nothing Then Continue For
                    objs(o.Tag) = so
                    Dim ms = TryCast(so, IMaterialStream)
                    If ms IsNot Nothing Then SetStreamState(fs, ms, o, report)
                    For Each kv In o.Properties
                        SetMember(so, kv.Key, kv.Value, report, o.Tag)
                    Next
                Catch ex As Exception
                    report.Add(o.Tag & ": " & ex.Message)
                End Try
            Next

            For Each cn In p.Skeleton.Connections
                Dim a As ISimulationObject = Nothing, b As ISimulationObject = Nothing
                If Not objs.TryGetValue(cn.From, a) OrElse Not objs.TryGetValue(cn.To, b) Then
                    report.Add("Connection " & cn.From & " -> " & cn.To & ": an object is missing")
                    Continue For
                End If
                Try
                    fs.ConnectObjects(a.GraphicObject, b.GraphicObject, cn.FromPort, cn.ToPort)
                Catch ex As Exception
                    report.Add("Connection " & cn.From & " -> " & cn.To & ": " & ex.Message)
                End Try
            Next

            For Each o In p.Skeleton.Objects
                Dim so As ISimulationObject = Nothing
                If Not objs.TryGetValue(o.Tag, so) Then Continue For
                For Each c In o.Calls
                    Invoke(so, c, objs, report, o.Tag)
                Next
            Next

            Try
                Select Case p.Skeleton.Layout?.ToLowerInvariant()
                    Case "none"
                    Case "auto"
                        fs.AutoLayout()
                    Case Else
                        Dim surface = fs.GetSurface()
                        Dim m = surface?.GetType().GetMethod("OrthogonalArrange")
                        If m IsNot Nothing Then m.Invoke(surface, New Object() {False}) Else fs.AutoLayout()
                End Select
            Catch ex As Exception
                report.Add("Layout: " & ex.Message)
            End Try

            Return report

        End Function

        ' ---------------------------------------------------------------------------------------------

        Private Shared Sub SetStreamState(fs As IFlowsheet, ms As IMaterialStream, o As ObjectSpec, report As List(Of String))
            If o.Composition.Count > 0 Then
                Dim names = fs.SelectedCompounds.Keys.ToList()
                Dim x(names.Count - 1) As Double
                For Each kv In o.Composition
                    Dim k = names.FindIndex(Function(n) String.Equals(n, kv.Key, StringComparison.OrdinalIgnoreCase))
                    If k < 0 Then
                        Dim resolved = FindCompoundName(fs, kv.Key)
                        If resolved IsNot Nothing Then k = names.IndexOf(resolved)
                    End If
                    If k >= 0 Then x(k) = kv.Value Else report.Add(o.Tag & ": compound not in the simulation: " & kv.Key)
                Next
                Dim sum = x.Sum()
                If sum > 0 Then
                    For k = 0 To x.Length - 1
                        x(k) /= sum
                    Next
                    If String.Equals(o.CompositionBasis, "mass", StringComparison.OrdinalIgnoreCase) Then
                        ms.SetOverallMassComposition(x)
                    Else
                        ms.SetOverallComposition(x)
                    End If
                End If
            End If
            If o.Temperature.HasValue Then ms.SetTemperature(o.Temperature.Value)
            If o.Pressure.HasValue Then ms.SetPressure(o.Pressure.Value)
            If o.MassFlow.HasValue Then ms.SetMassFlow(o.MassFlow.Value)
            If o.MolarFlow.HasValue Then ms.SetMolarFlow(o.MolarFlow.Value)
        End Sub

        Private Shared Sub AddReaction(fs As IFlowsheet, r As ReactionSpec, p As ProcessDefinition)

            Dim stoich As New Dictionary(Of String, Double)
            For Each kv In r.Stoichiometry
                stoich(If(FindCompoundName(fs, kv.Key), kv.Key)) = kv.Value
            Next
            Dim baseName = If(FindCompoundName(fs, r.BaseCompound), r.BaseCompound)
            Dim FixNames = Function(d As Dictionary(Of String, Double))
                          Dim o As New Dictionary(Of String, Double)
                          For Each kv In d
                              o(If(FindCompoundName(fs, kv.Key), kv.Key)) = kv.Value
                          Next
                          Return o
                      End Function

            Dim rx As IReaction
            Select Case r.Type.ToLowerInvariant()
                Case "equilibrium"
                    rx = fs.CreateEquilibriumReaction(r.Name, r.Description, stoich, baseName, r.Phase, r.Basis, r.Units, r.TApproach, r.LnKeq)
                Case "kinetic"
                    rx = fs.CreateKineticReaction(r.Name, r.Description, stoich, FixNames(r.DirectOrders), FixNames(r.ReverseOrders), baseName, r.Phase, r.Basis,
                                                  r.AmountUnits, r.RateUnits, r.AForward, r.EForward, r.AReverse, r.EReverse, r.ExprForward, r.ExprReverse)
                Case Else
                    rx = fs.CreateConversionReaction(r.Name, r.Description, stoich, baseName, r.Phase, If(String.IsNullOrEmpty(r.Conversion), "100", r.Conversion))
            End Select

            If Not fs.Reactions.ContainsKey(rx.ID) Then fs.AddReaction(rx)
            If Not fs.ReactionSets.ContainsKey("DefaultSet") Then fs.AddReactionSet(fs.CreateReactionSet("DefaultSet", "Default Set"))
            If Not fs.ReactionSets("DefaultSet").Reactions.ContainsKey(rx.ID) Then
                fs.AddReactionToSet(rx.ID, "DefaultSet", True, fs.ReactionSets("DefaultSet").Reactions.Count)
            End If

        End Sub

        ''' <summary>The name of the compound as the flowsheet knows it (case and the CAS number tolerated).</summary>
        Public Shared Function FindCompoundName(fs As IFlowsheet, name As String) As String
            If String.IsNullOrWhiteSpace(name) Then Return Nothing
            If fs.SelectedCompounds.ContainsKey(name) OrElse fs.AvailableCompounds.ContainsKey(name) Then Return name
            For Each k In fs.SelectedCompounds.Keys.Concat(fs.AvailableCompounds.Keys)
                If String.Equals(k, name, StringComparison.OrdinalIgnoreCase) Then Return k
            Next
            For Each c In fs.AvailableCompounds.Values
                If String.Equals(c.CAS_Number, name, StringComparison.OrdinalIgnoreCase) Then Return c.Name
            Next
            Return Nothing
        End Function

        Private Shared Function ResolvePackageName(fs As IFlowsheet, spec As PackageSpec) As String
            Dim names = fs.AvailablePropertyPackages.Keys.ToList()
            For Each candidate In {spec.Name, spec.Fallback}
                If String.IsNullOrWhiteSpace(candidate) Then Continue For
                Dim hit = names.FirstOrDefault(Function(n) String.Equals(n, candidate, StringComparison.OrdinalIgnoreCase))
                If hit IsNot Nothing Then Return hit
            Next
            Return Nothing
        End Function

        ''' <summary>The template of an external unit operation by its display name.</summary>
        Public Shared Function FindExternal(fs As IFlowsheet, displayName As String) As IExternalUnitOperation
            Dim so As ISimulationObject = Nothing
            If fs.AvailableSimulationObjects IsNot Nothing AndAlso fs.AvailableSimulationObjects.TryGetValue(displayName, so) Then
                Dim e = TryCast(so, IExternalUnitOperation)
                If e IsNot Nothing Then Return e
            End If
            If fs.AvailableExternalUnitOperations IsNot Nothing Then
                For Each e In fs.AvailableExternalUnitOperations.Values
                    Dim s = TryCast(e, ISimulationObject)
                    If s IsNot Nothing AndAlso (String.Equals(s.GetDisplayName(), displayName, StringComparison.OrdinalIgnoreCase) OrElse
                                                String.Equals(e.Name, displayName, StringComparison.OrdinalIgnoreCase)) Then Return e
                Next
            End If
            Return Nothing
        End Function

        Private Shared Sub SetMember(target As Object, name As String, value As JToken, report As List(Of String), owner As String)
            Try
                ' "Config.MembraneArea" sets a property of an object the unit operation holds
                Dim dot = name.IndexOf("."c)
                If dot > 0 Then
                    Dim head = target.GetType().GetProperty(name.Substring(0, dot), BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.IgnoreCase)
                    Dim inner = head?.GetValue(target)
                    If inner Is Nothing Then
                        report.Add(owner & ": property not found: " & name.Substring(0, dot))
                    Else
                        SetMember(inner, name.Substring(dot + 1), value, report, owner)
                    End If
                    Return
                End If
                Dim t = target.GetType()
                ' "Ratios[0]" sets an item of a list property (the split ratios of a splitter)
                Dim m = Text.RegularExpressions.Regex.Match(name, "^(\w+)\[(\d+)\]$")
                If m.Success Then
                    Dim lp = t.GetProperty(m.Groups(1).Value, BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.IgnoreCase)
                    Dim list = TryCast(lp?.GetValue(target), IList)
                    If list Is Nothing Then
                        report.Add(owner & ": list property not found: " & m.Groups(1).Value)
                        Return
                    End If
                    Dim k = Integer.Parse(m.Groups(2).Value, CultureInfo.InvariantCulture)
                    Dim et = If(list.GetType().IsGenericType, list.GetType().GetGenericArguments()(0), GetType(Object))
                    Dim v = If(et Is GetType(Object), value.ToObject(Of Object)(), ConvertToken(value, et, Nothing))
                    If TypeOf v Is Long Then v = CDbl(CLng(v))
                    While list.Count <= k
                        list.Add(If(et.IsValueType, Activator.CreateInstance(et), If(et Is GetType(Object), CObj(0.0), Nothing)))
                    End While
                    list(k) = v
                    Return
                End If
                Dim p = t.GetProperty(name, BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.IgnoreCase)
                If p IsNot Nothing AndAlso p.CanWrite Then
                    p.SetValue(target, ConvertToken(value, p.PropertyType, Nothing))
                    Return
                End If
                Dim f = t.GetField(name, BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.IgnoreCase)
                If f IsNot Nothing Then
                    f.SetValue(target, ConvertToken(value, f.FieldType, Nothing))
                    Return
                End If
                report.Add(owner & ": property not found: " & name)
            Catch ex As Exception
                report.Add(owner & "." & name & ": " & If(ex.InnerException?.Message, ex.Message))
            End Try
        End Sub

        Private Shared Sub Invoke(target As Object, c As CallSpec, objs As Dictionary(Of String, ISimulationObject), report As List(Of String), owner As String)
            Try
                Dim candidates = target.GetType().GetMethods(BindingFlags.Public Or BindingFlags.Instance).
                    Where(Function(m) String.Equals(m.Name, c.Method, StringComparison.OrdinalIgnoreCase) AndAlso
                                      m.GetParameters().Length >= c.Args.Count AndAlso
                                      m.GetParameters().Skip(c.Args.Count).All(Function(pp) pp.IsOptional)).ToList()
                For Each m In candidates.OrderBy(Function(x) x.GetParameters().Length)
                    Dim pars = m.GetParameters()
                    Dim args(pars.Length - 1) As Object
                    Dim ok = True
                    For k = 0 To pars.Length - 1
                        If k < c.Args.Count Then
                            Try
                                args(k) = ConvertToken(c.Args(k), pars(k).ParameterType, objs)
                            Catch
                                ok = False
                                Exit For
                            End Try
                        Else
                            args(k) = pars(k).DefaultValue
                        End If
                    Next
                    If Not ok Then Continue For
                    m.Invoke(target, args)
                    Return
                Next
                report.Add(owner & ": method not found or arguments don't match: " & c.Method)
            Catch ex As Exception
                report.Add(owner & "." & c.Method & ": " & If(ex.InnerException?.Message, ex.Message))
            End Try
        End Sub

        Private Shared Function ConvertToken(value As JToken, target As Type, objs As Dictionary(Of String, ISimulationObject)) As Object
            Dim nt = If(Nullable.GetUnderlyingType(target), target)
            If value Is Nothing OrElse value.Type = JTokenType.Null Then Return Nothing
            If value.Type = JTokenType.String Then
                Dim s = value.Value(Of String)()
                If s.StartsWith("@") AndAlso objs IsNot Nothing Then
                    Dim so As ISimulationObject = Nothing
                    If Not objs.TryGetValue(s.Substring(1), so) Then Throw New ArgumentException("unknown tag " & s)
                    If target.IsInstanceOfType(so) Then Return so
                    If target.IsInstanceOfType(so.GraphicObject) Then Return so.GraphicObject
                    If target Is GetType(String) Then Return so.Name
                    Throw New ArgumentException("type mismatch for " & s)
                End If
                If nt.IsEnum Then Return [Enum].Parse(nt, s, True)
                If nt Is GetType(String) Then Return s
                Return Convert.ChangeType(s, nt, CultureInfo.InvariantCulture)
            End If
            ' a number or a flag is never turned into a string, so an overload that takes a number wins
            If nt Is GetType(String) Then Throw New ArgumentException("a string is expected")
            If nt.IsEnum Then Return [Enum].ToObject(nt, value.Value(Of Integer)())
            If nt Is GetType(Object) Then Return value.ToObject(Of Object)()
            Return value.ToObject(nt)
        End Function

    End Class

End Namespace
