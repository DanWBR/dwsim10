Imports System.Text

Public Class FlowsheetSummary

    Public Shared Function GetSummary(Flowsheet As IFlowsheet)

        Dim units = Flowsheet.FlowsheetOptions.SelectedUnitSystem

        Dim sb As New StringBuilder("[")
        Dim first As Boolean = True
        For Each entry In Flowsheet.SimulationObjects
            Dim obj = entry.Value
            Dim tag As String = ""
            Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
            Dim otype As String = obj.GetDisplayName()
            Dim odesc = obj.GetDisplayDescription()
            If Not first Then sb.Append(",")
            sb.AppendFormat("{{""name"":""{0}"",""type"":""{1}"",""description"":""{2}"", ""properties"":[", EscJ(tag), EscJ(otype), EscJ(odesc))
            Try
                Dim names = Flowsheet.FlowsheetOptions.VisibleProperties(obj.GetType().Name)
                Dim pfirst As Boolean = True
                For Each pname In names
                    Try
                        Dim pval = obj.GetPropertyValue(pname, units)
                        Dim punit = obj.GetPropertyUnit(pname, units)
                        If Not pfirst Then sb.Append(",")
                        sb.AppendFormat("{{""name"":""{0}"",""value"":{1},""unit"":""{2}""}}",
                        EscJ(Flowsheet.GetTranslatedString(pname)), SafeNum(pval), EscJ(punit))
                        pfirst = False
                    Catch
                    End Try
                Next
            Catch ex As Exception
            End Try
            sb.Append("]}")
            first = False
        Next
        sb.Append("]")
        Dim errList As New StringBuilder("[")
        Try
            If Flowsheet.ErrorMessages IsNot Nothing Then
                Dim efirst As Boolean = True
                For Each e In Flowsheet.ErrorMessages
                    If Not efirst Then errList.Append(",")
                    errList.AppendFormat("""{0}""", EscJ(e.ToString()))
                    efirst = False
                Next
            End If
        Catch
        End Try
        errList.Append("]")

        ' ── Process description ─────────────────────────────────────────
        Dim procDesc As String = ""
        Try : procDesc = Flowsheet.FlowsheetOptions.Metadata.ProcessDescription : Catch : End Try
        If procDesc Is Nothing Then procDesc = Flowsheet.FlowsheetOptions.SimulationComments

        ' ── Key compounds (critical substances for efficiency evaluation) ─
        Dim compList As New StringBuilder("[")
        If Flowsheet.FlowsheetOptions.Metadata.KeyCompounds.Count = 0 Then
            Try
                Dim cfirst As Boolean = True
                For Each comp In Flowsheet.SelectedCompounds.Values
                    If Not cfirst Then compList.Append(",")
                    compList.AppendFormat("""{0}""", EscJ(comp.Name))
                    cfirst = False
                Next
            Catch
            End Try
        Else
            Try
                Dim cfirst As Boolean = True
                For Each comp In Flowsheet.FlowsheetOptions.Metadata.KeyCompounds
                    If Not cfirst Then compList.Append(",")
                    compList.AppendFormat("""{0}""", EscJ(comp))
                    cfirst = False
                Next
            Catch ex As Exception
            End Try
        End If
        compList.Append("]")

        ' ── Key reactants & key products ─────────────────────────────────
        ' Reactants  = main reagents from feed streams (no upstream connection).
        ' Products   = main products from product streams (no downstream connection).
        ' Used by the LLM to calculate reagent-to-product conversion in reaction processes.

        Dim reactantSet As New List(Of String)
        Dim productSet As New List(Of String)

        Dim reactantsSb As New StringBuilder("[")
        Dim productsSb As New StringBuilder("[")

        If Flowsheet.FlowsheetOptions.Metadata.KeyReactants.Count = 0 And Flowsheet.FlowsheetOptions.Metadata.KeyProducts.Count = 0 Then

            For Each entry In Flowsheet.SimulationObjects
                Dim obj = entry.Value
                Dim otype As String = obj.GraphicObject.ObjectType.ToString()
                If otype <> "MaterialStream" Then Continue For

                ' Check input connectors
                Dim hasInput As Boolean = False
                Try
                    For Each conn In obj.GraphicObject.InputConnectors
                        If conn.IsAttached Then
                            hasInput = True
                            Exit For
                        End If
                    Next
                Catch
                End Try

                ' Check output connectors
                Dim hasOutput As Boolean = False
                Try
                    For Each conn In obj.GraphicObject.OutputConnectors
                        If conn.IsAttached Then
                            hasOutput = True
                            Exit For
                        End If
                    Next
                Catch
                End Try

                ' Only process feed or product streams
                If hasInput AndAlso hasOutput Then Continue For

                ' Extract compound names with significant mole fractions
                Dim compNames As New List(Of String)
                Try
                    Dim propNames = obj.GetProperties(Enums.PropertyType.ALL)
                    For Each pname In propNames
                        If pname.Contains("MoleFraction") OrElse pname.Contains("Molar Fraction") Then
                            Try
                                Dim pval = obj.GetPropertyValue(pname)
                                If pval IsNot Nothing Then
                                    Dim dval As Double = Convert.ToDouble(pval)
                                    If dval > 0.0001 Then  ' skip trace amounts
                                        ' Extract compound name from property name
                                        ' Typical format: "Mixture Compounds Water Molar Fraction"
                                        ' or similar - take the part between "Compounds " and " Molar"/"MoleFraction"
                                        Dim cname As String = pname
                                        Dim idx1 As Integer = cname.IndexOf("Compounds ")
                                        If idx1 >= 0 Then
                                            cname = cname.Substring(idx1 + 10)
                                            Dim idx2 As Integer = cname.IndexOf(" Molar")
                                            If idx2 < 0 Then idx2 = cname.IndexOf(" MoleFraction")
                                            If idx2 >= 0 Then cname = cname.Substring(0, idx2)
                                        End If
                                        cname = cname.Trim()
                                        If cname.Length > 0 AndAlso Not compNames.Contains(cname) Then
                                            compNames.Add(cname)
                                        End If
                                    End If
                                End If
                            Catch
                            End Try
                        End If
                    Next
                Catch
                End Try

                If Not hasInput Then
                    ' Feed stream → reactants
                    For Each cn In compNames
                        If Not reactantSet.Contains(cn) Then reactantSet.Add(cn)
                    Next
                End If

                If Not hasOutput Then
                    ' Product stream → products
                    For Each cn In compNames
                        If Not productSet.Contains(cn) Then productSet.Add(cn)
                    Next
                End If
            Next

            Dim rfirst As Boolean = True
            For Each rn In reactantSet
                If Not rfirst Then reactantsSb.Append(",")
                reactantsSb.AppendFormat("""{0}""", EscJ(rn))
                rfirst = False
            Next
            reactantsSb.Append("]")

            Dim prfirst As Boolean = True
            For Each pn In productSet
                If Not prfirst Then productsSb.Append(",")
                productsSb.AppendFormat("""{0}""", EscJ(pn))
                prfirst = False
            Next
            productsSb.Append("]")

        Else

            ' Build JSON arrays of substance names
            Dim rfirst As Boolean = True
            For Each rn In Flowsheet.FlowsheetOptions.Metadata.KeyReactants
                If Not rfirst Then reactantsSb.Append(",")
                reactantsSb.AppendFormat("""{0}""", EscJ(rn))
                rfirst = False
            Next
            reactantsSb.Append("]")

            Dim prfirst As Boolean = True
            For Each pn In Flowsheet.FlowsheetOptions.Metadata.KeyProducts
                If Not prfirst Then productsSb.Append(",")
                productsSb.AppendFormat("""{0}""", EscJ(pn))
                prfirst = False
            Next
            productsSb.Append("]")

        End If

        ' ── Unit System ─────────────────────────────────────────────────
        ' Reads the active unit system from FlowsheetOptions and exposes
        ' each quantity category (temperature, pressure, massflow, …) with
        ' the unit string that DWSIM will use for GetPropertyValue / GetPropertyUnit.
        Dim unitSysSb As New StringBuilder("{")
        Try
            Dim usName2 As String = "SI"
            Try : usName2 = units.Name : Catch : End Try
            unitSysSb.AppendFormat("""name"":""{0}""", EscJ(usName2))
            ' Property category names that IUnitsOfMeasure exposes as string fields
            Dim catNames() As String = {
            "temperature", "pressure", "massflow", "molarflow", "volumetricflow",
            "enthalpy", "entropy", "heatflow", "mass", "moles", "density",
            "viscosity", "thermalConductivity", "heatTransferCoefficient",
            "area", "volume", "length", "time", "velocity",
            "molarenthalpy", "molarentropy", "molarvolume",
            "heatcapacity", "molarconcentration", "surfaceTension",
            "kinematic_viscosity", "acceleration", "force"
        }
            If units IsNot Nothing Then
                For Each cat In catNames
                    Try
                        Dim pinfo = units.GetType().GetProperty(cat,
                        Reflection.BindingFlags.IgnoreCase Or
                        Reflection.BindingFlags.Public Or
                        Reflection.BindingFlags.Instance)
                        If pinfo IsNot Nothing Then
                            Dim pval2 As String = pinfo.GetValue(units)?.ToString()
                            If pval2 IsNot Nothing Then
                                unitSysSb.AppendFormat(",""{0}"":""{1}""", EscJ(cat), EscJ(pval2))
                            End If
                        End If
                    Catch
                    End Try
                Next
            End If
        Catch ex2 As Exception
            unitSysSb.AppendFormat(",""error"":""{0}""", EscJ(ex2.Message))
        End Try
        unitSysSb.Append("}")

        ' ── Detect process type from unit operations present ────────────
        Dim hasReactor As Boolean = False
        Dim hasColumn As Boolean = False
        Dim hasHX As Boolean = False
        Dim hasPump As Boolean = False
        Dim hasCompressor As Boolean = False
        Dim hasMixer As Boolean = False
        Dim hasSeparator As Boolean = False
        Dim unitOpTypes As New List(Of String)

        For Each entry In Flowsheet.SimulationObjects
            Dim otype As String = entry.Value.GraphicObject.ObjectType.ToString()
            If Not unitOpTypes.Contains(otype) Then unitOpTypes.Add(otype)
            If otype.Contains("Reactor") Then hasReactor = True
            If otype.Contains("Column") OrElse otype.Contains("Distillation") _
               OrElse otype.Contains("Absorption") Then hasColumn = True
            If otype.Contains("HeatExchanger") OrElse otype.Contains("Heater") _
               OrElse otype.Contains("Cooler") Then hasHX = True
            If otype.Contains("Pump") Then hasPump = True
            If otype.Contains("Compressor") Then hasCompressor = True
            If otype.Contains("Mixer") Then hasMixer = True
            If otype.Contains("Flash") OrElse otype.Contains("Separator") _
               OrElse otype.Contains("ComponentSeparator") Then hasSeparator = True
        Next

        Dim processType As String = "General"
        If hasReactor AndAlso hasColumn Then
            processType = "ChemicalSeparation"
        ElseIf hasReactor Then
            processType = "Transformation"
        ElseIf hasColumn Then
            processType = "PhysicalSeparation"
        ElseIf hasSeparator Then
            processType = "PhysicalSeparation"
        ElseIf hasHX AndAlso (hasPump OrElse hasCompressor) Then
            processType = "Transportation"
        End If

        If Flowsheet.FlowsheetOptions.Metadata.ProcessType <> Enums.ProcessType.Unspecified Then

            processType = Flowsheet.FlowsheetOptions.Metadata.ProcessType.ToString().ToLower()

        End If

        ' ── Unit operation types list ───────────────────────────────────
        Dim uotSb As New StringBuilder("[")
        Dim uotFirst As Boolean = True
        For Each uot In unitOpTypes
            If uot = "MaterialStream" OrElse uot = "EnergyStream" Then Continue For
            If Not uotFirst Then uotSb.Append(",")
            uotSb.AppendFormat("""{0}""", EscJ(uot))
            uotFirst = False
        Next
        uotSb.Append("]")

        Dim sbUO As New StringBuilder("[")
        first = True
        Dim count As Integer = 0
        For Each obj In Flowsheet.SimulationObjects.Values.Where(Function(o1) TypeOf o1 Is IUnitOperation)
            Dim tag As String = ""
            Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
            Dim otype As String = obj.GraphicObject.ObjectType.ToString()
            If Not first Then sbUO.Append(",")
            Dim inlets As String = "", outlets As String = ""
            For Each ip In obj.GraphicObject.InputConnectors
                If ip.IsAttached Then
                    inlets += ip.AttachedConnector.AttachedFrom.Tag + ", "
                End If
            Next
            inlets = inlets.TrimEnd(", ")
            For Each ip In obj.GraphicObject.OutputConnectors
                If ip.IsAttached Then
                    outlets += ip.AttachedConnector.AttachedTo.Tag + ", "
                End If
            Next
            outlets = outlets.TrimEnd(", ")
            sbUO.AppendFormat("{{""name"":""{0}"",""type"":""{1}"",""connected from"":""{2}"",""connected to"":""{3}"",""properties"":[", EscJ(tag), EscJ(otype), EscJ(inlets), EscJ(outlets))
            Try
                Dim names = Flowsheet.FlowsheetOptions.VisibleProperties(obj.GetType().Name)
                Dim pfirst As Boolean = True
                For Each pname In names
                    Try
                        Dim pval = obj.GetPropertyValue(pname, units)
                        Dim punit = obj.GetPropertyUnit(pname, units)
                        If Not pfirst Then sbUO.Append(",")
                        sbUO.AppendFormat("{{""name"":""{0}"",""value"":{1},""unit"":""{2}""}}",
                                    EscJ(Flowsheet.GetTranslatedString(pname)), SafeNum(pval), EscJ(punit))
                        pfirst = False
                    Catch
                    End Try
                Next
            Catch
            End Try
            sbUO.Append("]}")
            first = False
            count += 1
        Next
        sbUO.Append("]")

        Dim sbStreams As New StringBuilder("[")
        first = True
        For Each entry In Flowsheet.SimulationObjects
            Dim obj = entry.Value
            If obj.GraphicObject.ObjectType <> Enums.GraphicObjects.ObjectType.MaterialStream Then Continue For
            Dim tag As String = ""
            Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
            If Not first Then sbStreams.Append(",")
            Dim inlets As String = "", outlets As String = ""
            For Each ip In obj.GraphicObject.InputConnectors
                If ip.IsAttached Then
                    inlets += ip.AttachedConnector.AttachedFrom.Tag + ", "
                End If
            Next
            inlets = inlets.TrimEnd().TrimEnd(",")
            For Each ip In obj.GraphicObject.OutputConnectors
                If ip.IsAttached Then
                    outlets += ip.AttachedConnector.AttachedTo.Tag + ", "
                End If
            Next
            outlets = outlets.TrimEnd().TrimEnd(",")
            Dim isFeed = inlets = "" AndAlso outlets <> ""
            Dim isProduct = inlets <> "" AndAlso outlets = ""

            ' Molar composition
            Dim ms = DirectCast(obj, IMaterialStream)
            Dim compSb As New StringBuilder(",""composition_molar"":{")
            Dim cFirst As Boolean = True
            For Each compName In ms.Phases(0).Compounds.Keys
                Dim molFrac = ms.Phases(0).Compounds(compName).MoleFraction.GetValueOrDefault(0)
                If molFrac < 0.0000001 Then Continue For  ' skip trace amounts
                If Not cFirst Then compSb.Append(",")
                compSb.AppendFormat("""{0}"":{1}", EscJ(compName), SafeNum(molFrac))
                cFirst = False
            Next
            compSb.Append("}")

            sbStreams.AppendFormat("{{""name"":""{0}"",""is_feed"":""{1}"",""is_product"":""{2}"",""connected_from"":""{3}"",""connected_to"":""{4}"",""properties"":[", EscJ(tag), EscJ(isFeed), EscJ(isProduct), EscJ(inlets), EscJ(outlets))
            Try
                Dim names = obj.GetDefaultProperties()
                Dim pfirst As Boolean = True
                For Each pname In names
                    Try
                        Dim pval = obj.GetPropertyValue(pname, units)
                        Dim punit = obj.GetPropertyUnit(pname, units)
                        If Not pfirst Then sbStreams.Append(",")
                        sbStreams.AppendFormat("{{""name"":""{0}"",""value"":{1},""unit"":""{2}""}}",
                                EscJ(Flowsheet.GetTranslatedString(pname)), SafeNum(pval), EscJ(punit))
                        pfirst = False
                    Catch
                    End Try
                Next
            Catch ex As Exception
            End Try
            sbStreams.Append(compSb.ToString())
            sbStreams.Append("]}")
            first = False
        Next
        sbStreams.Append("]")

        ' ── Assemble final JSON ─────────────────────────────────────────
        Dim summary = String.Format(
            "{{""flowsheet"":""{0}""," &
            """description"":""{1}""," &
            """process_type"":""{2}""," &
            """unit_system"":{3}," &
            """key_compounds"":{4}," &
            """unit_operation_types"":{5}," &
            """streams"":{6}," &
            """unit_operations"":{7}," &
            """key_reactants"":{8}," &
            """key_products"":{9}," &
            """objects"":{10}," &
            """solved"":{11}," &
            """errors"":{12}}}",
            EscJ(Flowsheet.FlowsheetOptions.SimulationName),
            EscJ(procDesc),
            EscJ(processType),
            unitSysSb.ToString(),
            compList.ToString(),
            uotSb.ToString(),
            sbStreams.ToString(),
            sbUO.ToString(),
            reactantsSb.ToString(),
            productsSb.ToString(),
            sb.ToString(),
            Flowsheet.Solved,
            errList.ToString())

        Return summary

    End Function

    Private Shared Function EscJ(s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "").Replace(vbLf, "\n")
    End Function

    Private Shared Function SafeNum(v As Object) As String
        If v Is Nothing Then Return "null"
        Try
            Dim d As Double = Convert.ToDouble(v)
            If Double.IsNaN(d) OrElse Double.IsInfinity(d) Then Return "null"
            Return d.ToString("G", System.Globalization.CultureInfo.InvariantCulture)
        Catch
            Return String.Format("""{0}""", EscJ(v.ToString()))
        End Try
    End Function

End Class
