Imports System.IO
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.SharedClasses.DWSIM.Flowsheet
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.Streams

Namespace CompoundEditing

    ''' <summary>
    ''' Edits a compound that is loaded in a simulation without breaking the references every material
    ''' stream and property package hold to it. The rule: the editors work on a clone, and on OK the clone
    ''' is copied INTO the live instance. Nothing in the flowsheet ever gets a new compound object.
    ''' Also the JSON side: load, save, link, and "import this JSON into the simulation".
    ''' Call these only while the solver is idle.
    ''' </summary>
    Public Module CompoundEditor

        Private ReadOnly IdentityKeys As String() = {"Name", "ID", "OriginalDB", "CurrentDB"}

        ''' <summary>An independent copy for an editing session.</summary>
        Public Function BeginEdit(live As ICompoundConstantProperties) As ConstantProperties
            Dim cp = TryCast(live, ConstantProperties)
            If cp Is Nothing Then Throw New ArgumentException("Only ConstantProperties compounds can be edited.")
            Return DirectCast(cp.Clone(), ConstantProperties)
        End Function

        ''' <summary>
        ''' Field-wise copy driven by the descriptor table. Exact: nullable fields stay Nothing, the element
        ''' and group lists and the tabular data are cloned, the extra properties are copied. Identity
        ''' (name, id, databases) is left alone unless asked for, because the target is normally the live
        ''' object that the simulation keys by name.
        ''' </summary>
        Public Sub CopyInto(source As ICompoundConstantProperties, target As ICompoundConstantProperties, Optional copyIdentity As Boolean = False)

            ' scalars first: the Formula setter rebuilds Elements, which the collection pass then overrides
            For Each d In CompoundPropertyDescriptors.Scalars
                If d.Kind = CompoundPropertyKind.Elements OrElse d.Kind = CompoundPropertyKind.Groups Then Continue For
                If Not copyIdentity AndAlso IdentityKeys.Contains(d.Key) Then Continue For
                d.SetValue(target, d.GetValue(source))
            Next

            For Each b In CompoundPropertyDescriptors.Blocks
                For Each k In b.Keys()
                    Dim d = CompoundPropertyDescriptors.ByKey(k)
                    d.SetValue(target, d.GetValue(source))
                Next
            Next

            For Each d In CompoundPropertyDescriptors.Scalars
                If d.Kind = CompoundPropertyKind.Elements OrElse d.Kind = CompoundPropertyKind.Groups Then
                    d.SetValue(target, d.GetValue(source))
                End If
            Next

            target.LinkedJsonFile = If(source.LinkedJsonFile, "")
            target.CompCreatorStudyFile = If(source.CompCreatorStudyFile, "")

            Dim src = TryCast(source.ExtraProperties, IDictionary(Of String, Object))
            Dim dst = TryCast(target.ExtraProperties, IDictionary(Of String, Object))
            If src IsNot Nothing AndAlso dst IsNot Nothing Then
                dst.Clear()
                For Each kv In src
                    dst(kv.Key) = kv.Value
                Next
            End If

        End Sub

        ''' <summary>
        ''' The compound instance the simulation actually uses. Always through Options.SelectedComponents:
        ''' IFlowsheet.SelectedCompounds hands back a re-ordered COPY of the dictionary for every ordering
        ''' mode except the default, so writing into that copy changes nothing.
        ''' </summary>
        Public Function FindLive(flowsheet As IFlowsheet, name As String) As ICompoundConstantProperties
            Dim fv = TryCast(flowsheet.FlowsheetOptions, FlowsheetVariables)
            If fv IsNot Nothing AndAlso fv.SelectedComponents IsNot Nothing Then
                Dim cp As ICompoundConstantProperties = Nothing
                If fv.SelectedComponents.TryGetValue(name, cp) Then Return cp
                Return Nothing
            End If
            Dim cp2 As ICompoundConstantProperties = Nothing
            If flowsheet.SelectedCompounds.TryGetValue(name, cp2) Then Return cp2
            Return Nothing
        End Function

        ''' <summary>
        ''' Copies the edited values into the compound the simulation holds, keeping the object identity so
        ''' every stream and property package sees the change. Registers an undo snapshot first and marks
        ''' every object as needing recalculation afterwards.
        ''' </summary>
        Public Sub ApplyInPlace(flowsheet As IFlowsheet, name As String, edited As ICompoundConstantProperties)

            Dim live = FindLive(flowsheet, name)
            If live Is Nothing Then Throw New KeyNotFoundException("The compound '" & name & "' is not in the simulation.")
            If edited.Name <> name Then Throw New ArgumentException("The edited compound is named '" & edited.Name & "', not '" & name & "'. Renaming is not supported here.")

            flowsheet.RegisterSnapshot(SnapshotType.Compounds)

            CopyInto(edited, live)
            live.IsModified = True

            RepointStreams(flowsheet, live)
            Invalidate(flowsheet)

        End Sub

        ''' <summary>Safety net: any stream phase that lost the shared reference gets it back.</summary>
        Private Sub RepointStreams(flowsheet As IFlowsheet, live As ICompoundConstantProperties)
            For Each stream In flowsheet.SimulationObjects.Values.OfType(Of MaterialStream)()
                For Each ph In stream.Phases.Values
                    Dim c As ICompound = Nothing
                    If ph.Compounds.TryGetValue(live.Name, c) AndAlso Not ReferenceEquals(c.ConstantProperties, live) Then
                        c.ConstantProperties = live
                    End If
                Next
            Next
        End Sub

        Private Sub Invalidate(flowsheet As IFlowsheet)
            flowsheet.ResetCalculationStatus()
            Try
                flowsheet.UpdateOpenEditForms()
            Catch
                ' no interface attached (automation, tests)
            End Try
        End Sub

        ''' <summary>
        ''' Reads a compound JSON file. A file with no source database becomes a User compound; a file
        ''' exported from another database keeps its OriginalDB, because that flag decides in which units
        ''' its equation coefficients are read. The link to the file is recorded on the compound.
        ''' </summary>
        Public Function LoadJson(jsonPath As String) As ConstantProperties
            Dim text = File.ReadAllText(jsonPath)
            Dim cp = Newtonsoft.Json.JsonConvert.DeserializeObject(Of ConstantProperties)(text)
            If cp Is Nothing Then Throw New InvalidDataException("The file does not contain a DWSIM compound.")
            If String.IsNullOrWhiteSpace(cp.Name) Then Throw New InvalidDataException("The compound in the file has no name.")
            If String.IsNullOrWhiteSpace(cp.OriginalDB) Then cp.OriginalDB = "User"
            cp.CurrentDB = "User"
            cp.LinkedJsonFile = System.IO.Path.GetFullPath(jsonPath)
            Return cp
        End Function

        ''' <summary>Writes the compound as JSON (the link path itself is not written) and links it to the file.</summary>
        Public Sub SaveToJson(cp As ICompoundConstantProperties, jsonPath As String)
            File.WriteAllText(jsonPath,cp.ExportToJSON())
            cp.LinkedJsonFile = System.IO.Path.GetFullPath(jsonPath)
        End Sub

        ''' <summary>
        ''' The linked file if it exists; otherwise a file with the same name next to the simulation file,
        ''' so a simulation moved together with its JSON still finds it. Nothing when neither exists.
        ''' </summary>
        Public Function ResolveLinkedJson(flowsheet As IFlowsheet, cp As ICompoundConstantProperties) As String
            Dim linked = If(cp.LinkedJsonFile, "").Trim()
            If linked = "" Then Return Nothing
            If File.Exists(linked) Then Return linked
            Dim simPath = ""
            Try
                If flowsheet IsNot Nothing Then simPath = If(flowsheet.FilePath, "")
            Catch
            End Try
            If simPath <> "" Then
                Dim candidate = Path.Combine(Path.GetDirectoryName(simPath), Path.GetFileName(linked))
                If File.Exists(candidate) Then Return candidate
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' Brings a JSON compound into the simulation. If a compound with that name is already selected,
        ''' its live instance is updated in place (streams keep their reference); otherwise the compound is
        ''' added to the available list, selected, and given to every material stream, as the compound
        ''' selection in the settings does.
        ''' </summary>
        Public Function ImportJsonCompound(flowsheet As IFlowsheet, jsonPath As String) As ICompoundConstantProperties

            Dim cp = LoadJson(jsonPath)

            Dim live = FindLive(flowsheet, cp.Name)
            If live IsNot Nothing Then
                ApplyInPlace(flowsheet, cp.Name, cp)
                Return live
            End If

            flowsheet.RegisterSnapshot(SnapshotType.Compounds)

            If flowsheet.AvailableCompounds.ContainsKey(cp.Name) Then
                flowsheet.AvailableCompounds(cp.Name) = cp
            Else
                flowsheet.AvailableCompounds.Add(cp.Name, cp)
            End If

            Dim fv = TryCast(flowsheet.FlowsheetOptions, FlowsheetVariables)
            If fv IsNot Nothing Then
                If fv.NotSelectedComponents IsNot Nothing AndAlso fv.NotSelectedComponents.ContainsKey(cp.Name) Then fv.NotSelectedComponents.Remove(cp.Name)
                fv.SelectedComponents.Add(cp.Name, cp)
            Else
                flowsheet.SelectedCompounds.Add(cp.Name, cp)
            End If

            For Each stream In flowsheet.SimulationObjects.Values.OfType(Of MaterialStream)()
                For Each ph In stream.Phases.Values
                    If Not ph.Compounds.ContainsKey(cp.Name) Then
                        ph.Compounds.Add(cp.Name, New Compound(cp.Name, ""))
                    End If
                    ph.Compounds(cp.Name).ConstantProperties = cp
                Next
            Next

            Invalidate(flowsheet)

            Return cp

        End Function

    End Module

End Namespace
