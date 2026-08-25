'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports DWSIM.Automation.FluentAPI.Diagnostics
Imports System.Net
Imports System.IO
Imports System.Text
Imports DWSIM.Interfaces
Imports DWSIM.UnitOperations.UnitOperations
Imports DWSIM.UnitOperations.Reactors
Imports DWSIM.Automation.FluentAPI

''' <summary>
''' Embedded HTTP server that exposes the active DWSIM flowsheet as a JSON REST API
''' consumed by the AI assistant front-end running in the WebView2 browser control.
''' <para>
''' The server listens on <c>http://localhost:5002/</c> and handles requests on a
''' background thread, dispatching each request to <see cref="HandleCtx"/>.
''' </para>
''' <para>
''' <b>Read endpoints</b> (GET):
''' <c>/api/check</c>, <c>/api/objects</c>, <c>/api/summary</c>, <c>/api/streams</c>,
''' <c>/api/all-objects</c>, <c>/api/unit-system</c>, <c>/api/property-packages</c>,
''' <c>/api/screenshot</c>, <c>/api/flowsheet-xml</c>, <c>/api/diagnostics</c>,
''' <c>/api/list-sections</c>, <c>/api/object/{name}/property/{prop}</c>,
''' <c>/api/fluent/catalog</c>.
''' </para>
''' <para>
''' <b>Write/action endpoints</b> (POST):
''' <c>/api/solve</c>, <c>/api/solve-and-score</c>, <c>/api/clear-flowsheet</c>,
''' <c>/api/add-compounds</c>, <c>/api/add-reaction</c>, <c>/api/set-property-package</c>,
''' <c>/api/add-section</c>, <c>/api/connect-sections</c>, <c>/api/set-feed-conditions</c>,
''' <c>/api/modify-unit</c>, <c>/api/remove-section</c>, <c>/api/auto-layout</c>,
''' <c>/api/object/{name}/property/{prop}</c>,
''' <c>/api/fluent/sweep</c>, <c>/api/fluent/materialise</c>, <c>/api/fluent/build</c>.
''' </para>
''' </summary>
Public Class Server

    Private Property Server As HttpListener = Nothing

    ''' <summary>The DWSIM flowsheet this server instance is bound to.</summary>
    Public Property Flowsheet As IFlowsheet

    Private ListeningTask As Threading.Thread

    ' ── Section Registry ─────────────────────────────────────────────────────────
    ' Tracks sections created by /api/add-section.
    ' Key   = sectionId (String)
    ' Value = Dictionary with keys:
    '   "type"    → String (section_type)
    '   "objects" → List(Of String) (DWSIM object tags created for this section)
    '   "ports"   → Dictionary(Of String, String) (port_name → stream tag)
    Private sectionRegistry As New Dictionary(Of String, Dictionary(Of String, Object))

    ''' <summary>
    ''' Starts the HTTP listener on <c>http://localhost:5002/</c> and launches the
    ''' background listening thread. No-op if the server is already running.
    ''' </summary>
    Sub StartServer()

        If Server Is Nothing Then

            Try

                Server = New HttpListener()
                Server.Prefixes.Add("http://localhost:5002/")
                Server.Start()
                Console.WriteLine("[DWSIM HTTP] Listening at http://localhost:5002/")

                ListeningTask = New System.Threading.Thread(Sub() ListenLoop(Server))
                ListeningTask.IsBackground = True
                ListeningTask.Start()

            Catch ex As Exception

                Flowsheet?.ShowMessage("Failed to start AI Assistant Server: " & ex.Message, IFlowsheet.MessageType.GeneralError)

            End Try


        End If

    End Sub

    ''' <summary>Aborts the HTTP listener and terminates the background listening thread.</summary>
    Sub StopServer()

        Server.Abort()
        Server = Nothing
        ListeningTask?.Abort()
        ListeningTask = Nothing

    End Sub

    ''' <summary>
    ''' Blocking loop that accepts incoming HTTP connections and dispatches each one
    ''' to <see cref="HandleCtx"/> on a thread-pool thread.
    ''' Runs on the dedicated background thread started by <see cref="StartServer"/>.
    ''' </summary>
    ''' <param name="lst">The active <see cref="HttpListener"/>.</param>
    Sub ListenLoop(lst As HttpListener)

        While lst.IsListening
            Try
                Dim ctx = lst.GetContext()
                Task.Run(Sub() HandleCtx(ctx))
            Catch : End Try
        End While

    End Sub

    ''' <summary>
    ''' Routes a single HTTP request to the appropriate handler block and writes the
    ''' JSON response back to the client.
    ''' Returns HTTP 404 for unknown paths and HTTP 500 when an unhandled exception occurs.
    ''' </summary>
    ''' <param name="ctx">The <see cref="HttpListenerContext"/> for the incoming request.</param>
    Sub HandleCtx(ctx As HttpListenerContext)

        Dim req = ctx.Request
        Dim resp = ctx.Response

        resp.ContentType = "application/json; charset=utf-8"

        ' ── Auth gate ────────────────────────────────────────────────────────
        ' The listener binds 127.0.0.1, but on a multi-user workstation any
        ' other process running as the same user can otherwise drive DWSIM
        ' through this API. Require the per-launch token that
        ' ReportExportHelper minted at startup; the same token is exported
        ' to dwsim-assistant.exe via the DWSIM_ASSISTANT_TOKEN env var.
        Dim expectedToken As String = ReportExportHelper.AssistantToken
        If Not String.IsNullOrEmpty(expectedToken) Then
            Dim sentToken As String = req.Headers("X-DWSIM-Token")
            If String.IsNullOrEmpty(sentToken) OrElse sentToken <> expectedToken Then
                resp.StatusCode = 401
                Dim msg = System.Text.Encoding.UTF8.GetBytes("{""error"":""unauthorized""}")
                resp.ContentLength64 = msg.Length
                resp.OutputStream.Write(msg, 0, msg.Length)
                resp.OutputStream.Close()
                Exit Sub
            End If
        End If

        Dim path = req.Url.AbsolutePath.TrimEnd("/"c)
        Dim method = req.HttpMethod
        Dim body = ""

        If req.HasEntityBody Then
            Using r = New StreamReader(req.InputStream)
                body = r.ReadToEnd()
            End Using
        End If

        Try

            Dim units = Flowsheet.FlowsheetOptions.SelectedUnitSystem
            Dim nf = Flowsheet.FlowsheetOptions.NumberFormat

            ' ── GET /api/screenshot ──────────────────────────────────────────────
            If req.HttpMethod = "GET" AndAlso path = "/api/screenshot" Then

                ' Ask DWSIM to render the flowsheet canvas to a temporary PNG file.
                Dim tmpFile As String = IO.Path.Combine(IO.Path.GetTempPath(), "dwsim_screenshot.png")

                Flowsheet.SavePFDScreenshotToPNG(tmpFile)

                ' Read the PNG bytes and stream them back.
                Dim pngBytes As Byte() = File.ReadAllBytes(tmpFile)
                File.Delete(tmpFile)   ' clean up

                resp.ContentType = "image/png"
                resp.ContentLength64 = pngBytes.Length
                resp.Headers.Add("Content-Disposition", "attachment; filename=""flowsheet.png""")
                resp.OutputStream.Write(pngBytes, 0, pngBytes.Length)
                resp.OutputStream.Close()

                Exit Sub

                ' ── GET /api/flowsheet-xml ───────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/flowsheet-xml" Then

                Dim xmlText As String = Flowsheet.SaveToXML().ToString()
                Dim xmlBytes As Byte() = Encoding.UTF8.GetBytes(xmlText)

                resp.ContentType = "application/xml; charset=utf-8"
                resp.ContentLength64 = xmlBytes.Length
                resp.OutputStream.Write(xmlBytes, 0, xmlBytes.Length)
                resp.OutputStream.Close()

                Exit Sub

            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/check" Then

                body = "{""objects"":[]}"

                ' ── GET /api/objects ─────────────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/objects" Then

                Dim sb As New StringBuilder("[")
                Dim first As Boolean = True
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    Dim tag As String = ""
                    Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
                    Dim otype As String = obj.GraphicObject.ObjectType.ToString()
                    If Not first Then sb.Append(",")
                    sb.AppendFormat("{{""name"":""{0}"",""type"":""{1}""}}", EscJ(tag), EscJ(otype))
                    first = False
                Next
                sb.Append("]")
                body = String.Format("{{""objects"":{0}}}", sb.ToString())

                ' ── GET /api/summary ─────────────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/summary" Then

                Dim sb As New StringBuilder("[")
                Dim first As Boolean = True
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    Dim tag As String = ""
                    Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
                    Dim otype As String = obj.GraphicObject.ObjectType.ToString()
                    If Not first Then sb.Append(",")
                    sb.AppendFormat("{{""name"":""{0}"",""type"":""{1}"",""properties"":[", EscJ(tag), EscJ(otype))
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
                'Try
                '    Dim efirst As Boolean = True
                '    For Each e In Flowsheet.MessagesLog
                '        If Not efirst Then errList.Append(",")
                '        errList.AppendFormat("""{0}""", EscJ(e.ToString()))
                '        efirst = False
                '    Next
                'Catch
                'End Try
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

                ' ── Assemble final JSON ─────────────────────────────────────────
                body = String.Format(
                    "{{""flowsheet"":""{0}""," &
                    """description"":""{1}""," &
                    """process_type"":""{2}""," &
                    """unit_system"":{3}," &
                    """key_compounds"":{4}," &
                    """unit_operation_types"":{5}," &
                    """key_reactants"":{6}," &
                    """key_products"":{7}," &
                    """objects"":{8}," &
                    """errors"":{9}}}",
                    EscJ(Flowsheet.FlowsheetOptions.SimulationName),
                    EscJ(procDesc),
                    EscJ(processType),
                    unitSysSb.ToString(),
                    compList.ToString(),
                    uotSb.ToString(),
                    reactantsSb.ToString(),
                    productsSb.ToString(),
                    sb.ToString(),
                    errList.ToString())

                ' ── GET /api/unit-system ─────────────────────────────────────────────
                ' Returns the active unit system name and all property-category → unit mappings.
                ' Consumers (Python LLM prompt, report generator) use this to know the units
                ' in which every GetPropertyValue / GetPropertyUnit call will return values.
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/unit-system" Then

                Try
                    Dim usSb2 As New StringBuilder("{")
                    Dim usName3 As String = "SI"
                    Try : usName3 = units.Name : Catch : End Try
                    usSb2.AppendFormat("""name"":""{0}""", EscJ(usName3))
                    Dim catNames2() As String = {
                        "temperature", "pressure", "massflow", "molarflow", "volumetricflow",
                        "enthalpy", "entropy", "heatflow", "mass", "moles", "density",
                        "viscosity", "thermalConductivity", "heatTransferCoefficient",
                        "area", "volume", "length", "time", "velocity",
                        "molarenthalpy", "molarentropy", "molarvolume",
                        "heatcapacity", "molarconcentration", "surfaceTension",
                        "kinematic_viscosity", "acceleration", "force"
                    }
                    If units IsNot Nothing Then
                        For Each cat2 In catNames2
                            Try
                                Dim pinfo2 = units.GetType().GetProperty(cat2,
                                    Reflection.BindingFlags.IgnoreCase Or
                                    Reflection.BindingFlags.Public Or
                                    Reflection.BindingFlags.Instance)
                                If pinfo2 IsNot Nothing Then
                                    Dim pval3 As String = pinfo2.GetValue(units)?.ToString()
                                    If pval3 IsNot Nothing Then
                                        usSb2.AppendFormat(",""{0}"":""{1}""", EscJ(cat2), EscJ(pval3))
                                    End If
                                End If
                            Catch
                            End Try
                        Next
                    End If
                    usSb2.Append("}")
                    body = usSb2.ToString()
                Catch ex As Exception
                    body = String.Format("{{""error"":""{0}""}}", EscJ(ex.Message))
                End Try

                ' ── GET /api/streams ─────────────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/streams" Then

                Dim sb As New StringBuilder("[")
                Dim first As Boolean = True
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    If obj.GraphicObject.ObjectType <> Enums.GraphicObjects.ObjectType.MaterialStream Then Continue For
                    Dim tag As String = ""
                    Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
                    If Not first Then sb.Append(",")
                    sb.AppendFormat("{{""name"":""{0}"",""properties"":[", EscJ(tag))
                    Try
                        Dim names = obj.GetDefaultProperties()
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
                body = sb.ToString()

            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/all-objects" Then

                Dim sb As New StringBuilder("[")
                Dim first As Boolean = True
                Dim count As Integer = 0
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    Dim tag As String = ""
                    Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
                    Dim otype As String = obj.GraphicObject.ObjectType.ToString()
                    If Not first Then sb.Append(",")
                    sb.AppendFormat("{{""name"":""{0}"",""type"":""{1}"",""properties"":[", EscJ(tag), EscJ(otype))
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
                    Catch
                    End Try
                    sb.Append("]}")
                    first = False
                    count += 1
                Next
                sb.Append("]")
                body = String.Format("{{""object_count"":{0},""objects"":{1}}}", count, sb.ToString())

                ' ── GET /api/object/{name}/property/{prop} ────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path.StartsWith("/api/object/") _
               AndAlso path.Contains("/property/") Then

                Dim parts = path.Split("/")  ' ["","api","object",name,"property",prop]
                Dim objName = Uri.UnescapeDataString(parts(3))
                Dim propName = Uri.UnescapeDataString(parts(5))
                Dim obj = FindObj(Flowsheet, objName)
                Dim val = obj.GetPropertyValue(propName, units)
                Dim unit = obj.GetPropertyUnit(propName, units)
                body = String.Format("{{""object"":""{0}"",""property"":""{1}"",""value"":{2},""unit"":""{3}""}}",
                EscJ(objName), EscJ(Flowsheet.GetTranslatedString(propName)), SafeNum(val), EscJ(unit))

                ' ── POST /api/object/{name}/property/{prop} ───────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path.StartsWith("/api/object/") _
               AndAlso path.Contains("/property/") Then

                Dim parts = path.Split("/")
                Dim objName = Uri.UnescapeDataString(parts(3))
                Dim propName = Uri.UnescapeDataString(parts(5))
                Dim payload = body
                Dim value = ExtractJsonDouble(payload, "value")
                Dim unitStr = ExtractJsonString(payload, "unit")
                Dim obj = FindObj(Flowsheet, objName)
                If TypeOf obj Is IMaterialStream Then
                    Select Case propName.ToLower()
                        Case "temperature"
                            obj.SetPropertyValue2("Temperature", Nothing, unitStr, value)
                        Case "pressure"
                            obj.SetPropertyValue2("Pressure", Nothing, unitStr, value)
                        Case "mass_flow", "mass flow"
                            obj.SetPropertyValue2("Mass Flow", Nothing, unitStr, value)
                    End Select
                Else
                    obj.SetPropertyValue2(propName, "", unitStr, value)
                End If
                body = String.Format("{{""success"":true,""object"":""{0}"",""property"":""{1}"",""value"":{2},""unit"":""{3}""}}",
                EscJ(objName), EscJ(Flowsheet.GetTranslatedString(propName)), value.ToString("G", System.Globalization.CultureInfo.InvariantCulture), EscJ(unitStr))

                ' ── POST /api/solve ──────────────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/flowsheet/check" Then

                ' What is wrong with the flowsheet before anything is solved. Cheap, and it
                ' turns most failed solves into a fix applied beforehand.
                body = FlowsheetChecks.Check(Flowsheet).ToString(Formatting.None)

            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/solve" Then

                Dim t0 = Environment.TickCount
                Dim errors = Flowsheet.RequestCalculationAndWait()
                Dim elapsed = Environment.TickCount - t0
                If errors IsNot Nothing AndAlso errors.Count > 0 Then
                    Dim errArr As New StringBuilder("[")
                    Dim efirst As Boolean = True
                    For Each e In errors
                        If Not efirst Then errArr.Append(",")
                        errArr.AppendFormat("""{0}""", EscJ(e.ToString()))
                        efirst = False
                    Next
                    errArr.Append("]")
                    Dim findings = FlowsheetChecks.FindingsArray(
                        FlowsheetDiagnostics.Diagnose(Flowsheet, errors)).ToString(Formatting.None)
                    body = String.Format("{{""success"":false,""converged"":false,""time_ms"":{0},""errors"":{1},""findings"":{2}}}",
                                         elapsed, errArr.ToString(), findings)
                Else
                    body = String.Format("{{""success"":true,""converged"":true,""time_ms"":{0}}}", elapsed)
                End If

                ' ══════════════════════════════════════════════════════════════════════
                ' FLOWSHEET DESIGN AGENT ENDPOINTS
                ' ══════════════════════════════════════════════════════════════════════

                ' ── POST /api/clear-flowsheet ──────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/clear-flowsheet" Then

                Dim objcount = Flowsheet.SimulationObjects.Count
                Flowsheet.Reset()
                Flowsheet.CloseOpenEditForms()
                sectionRegistry.Clear()
                body = String.Format("{{""success"":true,""message"":""Flowsheet cleared"",""objects_removed"":{0}}}", objcount)

                ' ── POST /api/add-compounds ────────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/add-compounds" Then

                Dim payload = body
                Dim names = ExtractJsonArray(payload, "compounds")
                Dim addedSb As New StringBuilder("[")
                Dim notFoundSb As New StringBuilder("[")
                Dim aFirst As Boolean = True
                Dim nFirst As Boolean = True
                For Each cname In names
                    Try
                        Flowsheet.RunCodeOnUIThread(Sub() Flowsheet.AddCompound(cname))
                        If Not aFirst Then addedSb.Append(",")
                        addedSb.AppendFormat("""{0}""", EscJ(cname))
                        aFirst = False
                    Catch
                        If Not nFirst Then notFoundSb.Append(",")
                        notFoundSb.AppendFormat("""{0}""", EscJ(cname))
                        nFirst = False
                    End Try
                Next
                addedSb.Append("]")
                notFoundSb.Append("]")
                body = String.Format("{{""success"":true,""added"":{0},""not_found"":{1}}}", addedSb.ToString(), notFoundSb.ToString())

            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/add-reaction" Then

                Dim payload = body
                Dim rType = ExtractJsonString(payload, "reaction_type").ToLower()   ' conversion, equilibrium, kinetic, hetcat
                Dim rName = ExtractJsonString(payload, "name")
                Dim rDesc = ExtractJsonString(payload, "description")
                Dim baseComp = ExtractJsonString(payload, "base_compound")
                Dim rPhase = ExtractJsonString(payload, "phase")                     ' mixture, vapor, liquid, solid
                If rPhase = "" Then rPhase = "vapor"

                Try

                    ' Parse stoichiometry: {"Methane": -1, "Water": -2, "CO2": 1, "H2": 4}
                    Dim stoich As New Dictionary(Of String, Double)
                    Dim stoichJson = ExtractJsonObject(payload, "stoichiometry")
                    If stoichJson <> "" Then
                        ' Simple parser: remove braces, split by comma, parse "key": value
                        Dim inner = stoichJson.Trim().TrimStart("{"c).TrimEnd("}"c)
                        For Each pair In inner.Split(","c)
                            Dim kv = pair.Split(":"c)
                            If kv.Length >= 2 Then
                                Dim k = kv(0).Trim().Trim(""""c)
                                Dim v = Convert.ToDouble(kv(1).Trim(), System.Globalization.CultureInfo.InvariantCulture)
                                stoich(k) = v
                            End If
                        Next
                    End If

                    For Each k In stoich.Keys
                        If Not Flowsheet.SelectedCompounds.ContainsKey(k) Then Flowsheet.AddCompound(k)
                    Next

                    Dim rxn As Object = Nothing

                    Select Case rType

                        Case "conversion"

                            Dim convExpr = ExtractJsonString(payload, "conversion_expression")
                            If convExpr = "" Then convExpr = "50"
                            rxn = Flowsheet.CreateConversionReaction(rName, rDesc, stoich, baseComp, rPhase, convExpr)

                        Case "equilibrium"

                            Dim basis = ExtractJsonString(payload, "basis")
                            If basis = "" Then basis = "fugacity"
                            Dim unitsr = ExtractJsonString(payload, "units")
                            Dim tApproach As Double = 0
                            Try : tApproach = Convert.ToDouble(ExtractJsonString(payload, "temperature_approach"), System.Globalization.CultureInfo.InvariantCulture) : Catch : End Try
                            Dim keqExpr = ExtractJsonString(payload, "keq_expression")
                            rxn = Flowsheet.CreateEquilibriumReaction(rName, rDesc, stoich, baseComp, rPhase, basis, unitsr, tApproach, keqExpr)

                        Case "kinetic"

                            Dim basis = ExtractJsonString(payload, "basis")
                            If basis = "" Then basis = "molar concentration"
                            Dim amtUnits = ExtractJsonString(payload, "amount_units")
                            If amtUnits = "" Then amtUnits = "mol/m3"
                            Dim rateUnits = ExtractJsonString(payload, "rate_units")
                            If rateUnits = "" Then rateUnits = "mol/[m3.s]"
                            Dim Af As Double = 0 : Try : Af = Convert.ToDouble(ExtractJsonString(payload, "A_forward"), System.Globalization.CultureInfo.InvariantCulture) : Catch : End Try
                            Dim Ef As Double = 0 : Try : Ef = Convert.ToDouble(ExtractJsonString(payload, "E_forward"), System.Globalization.CultureInfo.InvariantCulture) : Catch : End Try
                            Dim Ar As Double = 0 : Try : Ar = Convert.ToDouble(ExtractJsonString(payload, "A_reverse"), System.Globalization.CultureInfo.InvariantCulture) : Catch : End Try
                            Dim Er As Double = 0 : Try : Er = Convert.ToDouble(ExtractJsonString(payload, "E_reverse"), System.Globalization.CultureInfo.InvariantCulture) : Catch : End Try
                            Dim exprFwd = ExtractJsonString(payload, "expression_forward")
                            Dim exprRev = ExtractJsonString(payload, "expression_reverse")

                            ' Parse direct/reverse orders: same format as stoichiometry
                            Dim directOrd As New Dictionary(Of String, Double)
                            Dim reverseOrd As New Dictionary(Of String, Double)
                            For Each k In stoich.Keys
                                directOrd(k) = 0.0
                                reverseOrd(k) = 0.0
                            Next
                            Dim doJson = ExtractJsonObject(payload, "direct_orders")
                            If doJson <> "" Then
                                Dim inner2 = doJson.Trim().TrimStart("{"c).TrimEnd("}"c)
                                For Each pair In inner2.Split(","c)
                                    Dim kv = pair.Split(":"c)
                                    If kv.Length >= 2 Then
                                        Dim k = kv(0).Trim().Trim(""""c)
                                        Dim v = Convert.ToDouble(kv(1).Trim(), System.Globalization.CultureInfo.InvariantCulture)
                                        directOrd(k) = v
                                    End If
                                Next
                            End If
                            Dim roJson = ExtractJsonObject(payload, "reverse_orders")
                            If roJson <> "" Then
                                Dim inner3 = roJson.Trim().TrimStart("{"c).TrimEnd("}"c)
                                For Each pair In inner3.Split(","c)
                                    Dim kv = pair.Split(":"c)
                                    If kv.Length >= 2 Then
                                        Dim k = kv(0).Trim().Trim(""""c)
                                        Dim v = Convert.ToDouble(kv(1).Trim(), System.Globalization.CultureInfo.InvariantCulture)
                                        reverseOrd(k) = v
                                    End If
                                Next
                            End If

                            rxn = Flowsheet.CreateKineticReaction(rName, rDesc, stoich, directOrd, reverseOrd,
                                                             baseComp, rPhase, basis, amtUnits, rateUnits,
                                                             Af, Ef, Ar, Er, exprFwd, exprRev)

                        Case "hetcat", "heterogeneous_catalytic"

                            Dim basis = ExtractJsonString(payload, "basis")
                            If basis = "" Then basis = "molar concentration"
                            Dim amtUnits = ExtractJsonString(payload, "amount_units")
                            If amtUnits = "" Then amtUnits = "mol/m3"
                            Dim rateUnits = ExtractJsonString(payload, "rate_units")
                            If rateUnits = "" Then rateUnits = "mol/[m3.s]"
                            Dim numExpr = ExtractJsonString(payload, "numerator_expression")
                            Dim denExpr = ExtractJsonString(payload, "denominator_expression")

                            rxn = Flowsheet.CreateHetCatReaction(rName, rDesc, stoich, baseComp, rPhase,
                                                            basis, amtUnits, rateUnits, numExpr, denExpr)

                        Case Else

                            rxn = Nothing
                            body = String.Format("{{""success"":false,""error"":""Unknown reaction_type: {0}. Use: conversion, equilibrium, kinetic, hetcat""}}", EscJ(rType))

                    End Select

                    ' Add reaction to flowsheet and to the default reaction set
                    If rxn IsNot Nothing Then
                        Flowsheet.AddReaction(rxn)
                        Flowsheet.AddReactionToSet(rxn.ID, "DefaultSet", True, 0)
                        body = String.Format("{{""success"":true,""reaction_id"":""{0}"",""reaction_type"":""{1}"",""name"":""{2}""}}",
                            EscJ(rxn.ID), EscJ(rType), EscJ(rName))
                    End If
                Catch ex As Exception
                    body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(ex.Message))
                End Try

                ' ── GET /api/property-packages ─────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/property-packages" Then

                Try
                    Dim names As New List(Of String)
                    Dim finished As Boolean = False, busy As Boolean = False
                    While Not finished
                        If Not busy Then
                            busy = True
                            Flowsheet.RunCodeOnUIThread(Sub()
                                                            Try
                                                                names = Flowsheet.GetAvailablePropertyPackages()
                                                            Catch ex As Exception
                                                            Finally
                                                                finished = True
                                                            End Try
                                                        End Sub)
                        End If
                        Threading.Thread.Sleep(500)
                    End While
                    Dim sb As New Text.StringBuilder()
                    sb.Append("{""property_packages"":[")
                    For i = 0 To names.Count - 1
                        If i > 0 Then sb.Append(",")
                        sb.Append("""")
                        sb.Append(EscJ(names(i)))
                        sb.Append("""")
                    Next
                    sb.Append("]}")
                    body = sb.ToString()
                Catch ex As Exception
                    body = String.Format("{{""error"":""{0}""}}", EscJ(ex.Message))
                End Try

                ' ── POST /api/set-property-package ─────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/set-property-package" Then

                Dim payload = body
                Dim ppName = ExtractJsonString(payload, "property_package")
                If ppName = "" Then ppName = "Raoult's Law"
                Try
                    Flowsheet.PropertyPackages.Clear()
                    Flowsheet.RunCodeOnUIThread(Sub()
                                                    Flowsheet.CreateAndAddPropertyPackage(ppName)
                                                End Sub)
                    body = String.Format("{{""success"":true,""property_package"":""{0}""}}", EscJ(ppName))
                Catch ex As Exception
                    body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(ex.Message))
                End Try

                ' ── POST /api/add-section ──────────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/add-section" Then

                Dim payload = body
                Dim secType = ExtractJsonString(payload, "section_type")
                Dim secId = ExtractJsonString(payload, "section_id")
                Dim paramsJson = ExtractJsonObject(payload, "params")

                Dim info As New Dictionary(Of String, Object)

                Dim finished As Boolean = False, busy As Boolean = False
                Dim exc1 As Exception = Nothing
                While Not finished
                    If Not busy Then
                        busy = True
                        Flowsheet.RunCodeOnUIThread(Sub()
                                                        Try
                                                            info = CreateSection(Flowsheet, secType, secId, paramsJson)
                                                            Flowsheet.AutoLayout()
                                                            Flowsheet.NaturalLayout()
                                                            Flowsheet.UpdateInterface()
                                                            Flowsheet.UpdateOpenEditForms()
                                                        Catch ex As Exception
                                                            exc1 = ex
                                                        Finally
                                                            finished = True
                                                        End Try
                                                    End Sub)
                    End If
                    Threading.Thread.Sleep(500)
                End While

                ' Register section
                sectionRegistry(secId) = info

                ' Build response
                Dim objsSb As New StringBuilder("[")
                Dim oFirst As Boolean = True
                Dim objList = DirectCast(info("objects"), List(Of String))
                For Each oname In objList
                    If Not oFirst Then objsSb.Append(",")
                    objsSb.AppendFormat("""{0}""", EscJ(oname))
                    oFirst = False
                Next
                objsSb.Append("]")

                Dim portsSb As New StringBuilder("{")
                Dim pFirst As Boolean = True
                Dim portDict = DirectCast(info("ports"), Dictionary(Of String, String))
                For Each kvp In portDict
                    If Not pFirst Then portsSb.Append(",")
                    portsSb.AppendFormat("""{0}"":""{1}""", EscJ(kvp.Key), EscJ(kvp.Value))
                    pFirst = False
                Next
                portsSb.Append("}")

                If exc1 Is Nothing Then
                    body = String.Format(
                    "{{""success"":true,""section_id"":""{0}"",""section_type"":""{1}""," &
                    """objects_created"":{2},""exposed_ports"":{3}}}",
                    EscJ(secId), EscJ(secType), objsSb.ToString(), portsSb.ToString())
                Else
                    body = String.Format(
                    "{{""success"":false,""section_id"":""{0}"",""section_type"":""{1}""," &
                    """objects_created"":{2},""exposed_ports"":{3}}}",
                    EscJ(secId), EscJ(secType), objsSb.ToString(), portsSb.ToString())
                End If

                ' ── POST /api/connect-sections ─────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/connect-sections" Then

                Dim payload = body
                Dim fromSec = ExtractJsonString(payload, "from_section")
                Dim fromPort = ExtractJsonString(payload, "from_port")
                Dim toSec = ExtractJsonString(payload, "to_section")
                Dim toPort = ExtractJsonString(payload, "to_port")

                ' Look up stream tags from section ports
                If Not sectionRegistry.ContainsKey(fromSec) Then
                    Throw New Exception("Section '" & fromSec & "' not found in registry")
                End If
                If Not sectionRegistry.ContainsKey(toSec) Then
                    Throw New Exception("Section '" & toSec & "' not found in registry")
                End If
                Dim fromPorts = DirectCast(sectionRegistry(fromSec)("ports"), Dictionary(Of String, String))
                Dim toPorts = DirectCast(sectionRegistry(toSec)("ports"), Dictionary(Of String, String))
                If Not fromPorts.ContainsKey(fromPort) Then
                    If fromPort.ToLower().Contains("outlet") Then fromPort = fromPorts.Keys.Where(Function(p) p.Contains("out")).FirstOrDefault()
                    If Not fromPorts.ContainsKey(fromPort) Then
                        Throw New Exception("Port '" & fromPort & "' not found in section '" & fromSec & "'")
                    End If
                End If
                If Not toPorts.ContainsKey(toPort) Then
                    If toPort.ToLower().Contains("in") Then toPort = toPorts.Keys.Where(Function(p) p.Contains("in")).FirstOrDefault()
                    If Not toPorts.ContainsKey(toPort) Then
                        Throw New Exception("Port '" & toPort & "' not found in section '" & toSec & "'")
                    End If
                End If

                Dim fromStream = fromPorts(fromPort)
                Dim toStream = toPorts(toPort)

                ' Connect: the output port's stream becomes the input port's stream.
                ' We connect the from-section's output object to the to-section's input object
                ' by creating a connecting material stream.
                Dim connName As String = "S_" & fromSec & "_to_" & toSec
                Dim fromObj = FindObj(Flowsheet, fromStream)
                Dim toObj = FindObj(Flowsheet, toStream)

                Try
                    Dim valve = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Valve, 100, 100, "valve_01")
                    Flowsheet.Connect(fromObj, valve, 0, 0)
                    Flowsheet.Connect(valve, toObj, 0, 0)
                Catch
                    ' If direct connection fails, try via the unit operation ports
                    ' This is a simplified approach - DWSIM may need specific port indices
                End Try

                Flowsheet.RunCodeOnUIThread(Sub()
                                                Flowsheet.NaturalLayout()
                                                Flowsheet.UpdateOpenEditForms()
                                                Flowsheet.UpdateInterface()
                                            End Sub)
                body = String.Format(
                "{{""success"":true,""stream_name"":""{0}"",""from"":""{1}.{2}"",""to"":""{3}.{4}""}}",
                EscJ(connName), EscJ(fromSec), EscJ(fromPort), EscJ(toSec), EscJ(toPort))

                ' ── POST /api/set-feed-conditions ──────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/set-feed-conditions" Then

                Dim payload = body
                Dim secId = ExtractJsonString(payload, "section_id")
                Dim port = ExtractJsonString(payload, "port")
                Dim sName = ExtractJsonString(payload, "stream_name")

                ' Determine which stream to configure
                Dim streamTag As String = ""
                If sName <> "" Then
                    streamTag = sName
                ElseIf secId <> "" AndAlso port <> "" Then
                    If sectionRegistry.ContainsKey(secId) Then
                        Dim ports = DirectCast(sectionRegistry(secId)("ports"), Dictionary(Of String, String))
                        If ports.ContainsKey(port) Then
                            streamTag = ports(port)
                        ElseIf ports.Keys.Where(Function(p) p.ToLower().Contains("in")).Count > 0 Then
                            port = ports.Keys.Where(Function(p) p.ToLower().Contains("in")).FirstOrDefault()
                            streamTag = ports(port)
                        End If
                    End If
                End If
                If streamTag = "" Then
                    Throw New Exception("Could not resolve stream from section_id/port or stream_name")
                End If

                Dim obj = FindObj(Flowsheet, streamTag)

                ' Extract conditions from JSON (accept both nested and flat)
                Dim condJson = ExtractJsonObject(payload, "conditions")
                If condJson = "" Then condJson = payload  ' fallback: conditions are flat in payload
                Dim tempC = ExtractJsonDouble(condJson, "temperature_C")
                If tempC = 0 Then tempC = ExtractJsonDouble(condJson, "T_C")
                Dim presKPa = ExtractJsonDouble(condJson, "pressure_kPa")
                If presKPa = 0 Then presKPa = ExtractJsonDouble(condJson, "P_kPa")
                Dim flowKgph = ExtractJsonDouble(condJson, "total_flow_kgph")
                If flowKgph = 0 Then flowKgph = ExtractJsonDouble(condJson, "flow_kgph")

                DirectCast(obj, IMaterialStream).SetPropertyPackageObject(Flowsheet.PropertyPackages.Values.First())

                ' Set temperature (convert C → K)
                If tempC <> 0 Then obj.SetPropertyValue("PROP_MS_0", tempC + 273.15)
                ' Set pressure (convert kPa → Pa)
                If presKPa <> 0 Then obj.SetPropertyValue("PROP_MS_1", presKPa * 1000)
                ' Set mass flow (convert kg/h → kg/s)
                If flowKgph <> 0 Then obj.SetPropertyValue("PROP_MS_2", flowKgph / 3600.0)

                ' Set molar composition
                Dim compJson = ExtractJsonObject(condJson, "composition_molar")
                If compJson <> "" Then
                    ' Parse each compound and its mole fraction
                    Dim compPattern As String = """([^""]+)""\s*:\s*(-?[\d.eE+\-]+)"
                    Dim matches = System.Text.RegularExpressions.Regex.Matches(compJson, compPattern)
                    Dim vx As New List(Of Double)
                    For Each m As System.Text.RegularExpressions.Match In matches
                        Dim compName = m.Groups(1).Value
                        Dim moleFrac = Double.Parse(m.Groups(2).Value, System.Globalization.CultureInfo.InvariantCulture)
                        vx.Add(moleFrac)
                    Next
                    DirectCast(obj, IMaterialStream).SetOverallMolarComposition(vx.ToArray())
                End If

                body = String.Format(
                "{{""success"":true,""stream"":""{0}"",""temperature_C"":{1},""pressure_kPa"":{2},""total_flow_kgph"":{3}}}",
                EscJ(streamTag), tempC.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                presKPa.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                flowKgph.ToString("G", System.Globalization.CultureInfo.InvariantCulture))

                ' ── POST /api/solve-and-score ──────────────────────────────────────

            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/solve-and-score" Then

                Dim t0 = Environment.TickCount
                Dim errors = Flowsheet.RequestCalculationAndWait()

                If errors.Where(Function(ex1) ex1.Message.Contains("Infinite loop detected")).Count > 0 Then
                    Dim finished As Boolean = False, busy As Boolean = False
                    Dim exc1 As Exception = Nothing
                    While Not finished
                        If Not busy Then
                            busy = True
                            Flowsheet.RunCodeOnUIThread(Sub()
                                                            'process flowsheet layout
                                                            Dim fprocessor As New FlowsheetAnalyzer() With {.Diagram = Flowsheet}
                                                            Try
                                                                fprocessor.ProcessInfiniteLoops()
                                                                Flowsheet.NaturalLayout()
                                                                Flowsheet.UpdateInterface()
                                                            Catch ex As Exception
                                                                exc1 = ex
                                                            Finally
                                                                fprocessor.Diagram = Nothing
                                                                fprocessor = Nothing
                                                                finished = True
                                                            End Try
                                                        End Sub)
                        End If
                        Threading.Thread.Sleep(500)
                    End While
                    errors = Flowsheet.RequestCalculationAndWait()
                End If

                Dim elapsed = Environment.TickCount - t0
                Dim converged As Boolean = (errors Is Nothing OrElse errors.Count = 0)

                ' Build error array
                Dim errArr As New StringBuilder("[")
                If Not converged Then
                    Dim efirst As Boolean = True
                    For Each e In errors
                        If Not efirst Then errArr.Append(",")
                        errArr.AppendFormat("""{0}""", EscJ(e.ToString()))
                        efirst = False
                    Next
                End If
                errArr.Append("]")

                ' Collect unconverged units
                Dim uncSb As New StringBuilder("[")
                Dim uFirst As Boolean = True
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    Try
                        If obj.GraphicObject.ObjectType.ToString() = "MaterialStream" Then Continue For
                        If obj.GraphicObject.ObjectType.ToString() = "EnergyStream" Then Continue For
                        ' Check if the object has calculation errors
                        Dim errMsg As String = ""
                        Try : errMsg = obj.ErrorMessage : Catch : End Try
                        If errMsg IsNot Nothing AndAlso errMsg.Length > 0 Then
                            If Not uFirst Then uncSb.Append(",")
                            Dim tag As String = ""
                            Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
                            uncSb.AppendFormat("""{0}""", EscJ(tag))
                            uFirst = False
                        End If
                    Catch
                    End Try
                Next
                uncSb.Append("]")

                ' Collect product stream results
                Dim prodSb As New StringBuilder("{")
                Dim prFirst As Boolean = True
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    If obj.GraphicObject.ObjectType.ToString() <> "MaterialStream" Then Continue For
                    ' Check if it's a product stream (no output connections)
                    Dim hasOut As Boolean = False
                    Try
                        For Each conn In obj.GraphicObject.OutputConnectors
                            If conn.IsAttached Then hasOut = True : Exit For
                        Next
                    Catch
                    End Try
                    If hasOut Then Continue For
                    ' This is a product stream
                    Dim tag As String = ""
                    Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
                    If Not prFirst Then prodSb.Append(",")
                    prodSb.AppendFormat("""{0}"":{{", EscJ(tag))
                    Try
                        Dim tempK = Convert.ToDouble(obj.GetPropertyValue("PROP_MS_0"))
                        Dim presPa = Convert.ToDouble(obj.GetPropertyValue("PROP_MS_1"))
                        Dim flowKgs = Convert.ToDouble(obj.GetPropertyValue("PROP_MS_2"))
                        Dim vapFrac = 0.0
                        Try : vapFrac = DirectCast(obj, IMaterialStream).Phases(2).Properties.molarfraction.GetValueOrDefault() : Catch : End Try
                        prodSb.AppendFormat("""temperature_C"":{0},""pressure_kPa"":{1},""flow_kgph"":{2},""vapor_fraction"":{3}",
                        SafeNum(tempK - 273.15), SafeNum(presPa / 1000.0), SafeNum(flowKgs * 3600.0), SafeNum(vapFrac))
                    Catch
                    End Try
                    ' Molar composition
                    Try
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
                        prodSb.Append(compSb.ToString())
                    Catch
                        ' Composition not available - skip
                    End Try
                    prodSb.Append("}")
                    prFirst = False
                Next
                prodSb.Append("}")

                ' Calculate total duties
                Dim totalHeat As Double = 0
                Dim totalCool As Double = 0
                Dim totalPower As Double = 0
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    Dim otype = obj.GraphicObject.ObjectType.ToString()
                    If otype = "EnergyStream" Then
                        Try
                            Dim ef = Convert.ToDouble(obj.GetPropertyValue("PROP_ES_0"))
                            If ef > 0 Then
                                totalHeat += ef
                            Else
                                If obj.GraphicObject.OutputConnectors(0).IsAttached AndAlso
                                    obj.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.ObjectType =
                                    Enums.GraphicObjects.ObjectType.DistillationColumn Then
                                    'reboiler duty
                                    totalHeat += -ef
                                Else
                                    totalCool += Math.Abs(ef)
                                End If
                            End If
                        Catch
                        End Try
                    End If
                    If otype.Contains("Pump") Then
                        Try
                            Dim pw = DirectCast(obj, Pump).DeltaQ.GetValueOrDefault()  ' power
                            totalPower += Math.Abs(pw)
                        Catch
                        End Try
                    End If
                    If otype.Contains("Compressor") Then
                        Try
                            Dim pw = DirectCast(obj, Compressor).DeltaQ  ' power
                            totalPower += Math.Abs(pw)
                        Catch
                        End Try
                    End If
                Next

                ' Collect equipment inventory for cost estimation
                Dim eqSb As New StringBuilder("[")
                Dim eqFirst As Boolean = True
                For Each entry In Flowsheet.SimulationObjects
                    Dim obj = entry.Value
                    Dim otype = obj.GraphicObject.ObjectType.ToString()
                    ' Skip streams (material + energy)
                    If otype = "MaterialStream" OrElse otype = "EnergyStream" Then Continue For
                    Dim eqTag As String = ""
                    Try : eqTag = obj.GraphicObject.Tag : Catch : eqTag = obj.Name : End Try
                    If Not eqFirst Then eqSb.Append(",")
                    eqSb.AppendFormat("{{""name"":""{0}"",""type"":""{1}""", EscJ(eqTag), EscJ(otype))
                    ' Try to extract sizing properties based on type
                    Try
                        If otype.Contains("ShortcutColumn") Then
                            ' Number of stages
                            Try
                                Dim ns = DirectCast(obj, ShortcutColumn).m_N
                                eqSb.AppendFormat(",""stages"":{0}", Convert.ToInt32(ns))
                            Catch : End Try
                        End If
                        If otype.Contains("Distillation") Then
                            ' Number of stages
                            Try
                                Dim ns = DirectCast(obj, Column).Stages.Count
                                eqSb.AppendFormat(",""stages"":{0}", Convert.ToInt32(ns))
                            Catch : End Try
                        End If
                        If otype.Contains("Absorption") Then
                            ' Number of stages
                            Try
                                Dim ns = DirectCast(obj, Column).Stages.Count
                                eqSb.AppendFormat(",""stages"":{0}", Convert.ToInt32(ns))
                            Catch : End Try
                        End If
                        If otype.Contains("HeatExchanger") Then
                            Try
                                Dim area = DirectCast(obj, HeatExchanger).Area.GetValueOrDefault()
                                eqSb.AppendFormat(",""area_m2"":{0}", SafeNum(area))
                            Catch : End Try
                        End If
                        If otype.Contains("Pump") Then
                            Try
                                Dim pw = DirectCast(obj, Pump).DeltaQ.GetValueOrDefault()
                                eqSb.AppendFormat(",""power_kW"":{0}", SafeNum(Math.Abs(pw)))
                            Catch : End Try
                        End If
                        If otype.Contains("Compressor") Then
                            Try
                                Dim pw = DirectCast(obj, Compressor).DeltaQ
                                eqSb.AppendFormat(",""power_kW"":{0}", SafeNum(Math.Abs(pw)))
                            Catch : End Try
                        End If
                        If otype.Contains("Expander") Then
                            Try
                                Dim pw = DirectCast(obj, Expander).DeltaQ
                                eqSb.AppendFormat(",""power_kW"":{0}", SafeNum(Math.Abs(pw)))
                            Catch : End Try
                        End If
                        If otype.Contains("Vessel") Then
                            Try
                                Dim vol = DirectCast(obj, Vessel).CalculateVolume()
                                eqSb.AppendFormat(",""volume_m3"":{0}", SafeNum(vol))
                            Catch : End Try
                        End If
                        If otype.Contains("Tank") Then
                            Try
                                Dim vol = DirectCast(obj, Tank).Volume
                                eqSb.AppendFormat(",""volume_m3"":{0}", SafeNum(vol))
                            Catch : End Try
                        End If
                        If otype.Contains("CSTR") Then
                            Try
                                Dim vol = DirectCast(obj, Reactor_CSTR).Volume
                                eqSb.AppendFormat(",""volume_m3"":{0}", SafeNum(vol))
                            Catch : End Try
                        End If
                        If otype.Contains("Heater") Then
                            Try
                                Dim duty = DirectCast(obj, Heater).HeatDuty
                                eqSb.AppendFormat(",""duty_kW"":{0}", SafeNum(Math.Abs(duty)))
                            Catch : End Try
                        End If
                        If otype.Contains("Cooler") Then
                            Try
                                Dim duty = DirectCast(obj, Cooler).HeatDuty
                                eqSb.AppendFormat(",""duty_kW"":{0}", SafeNum(Math.Abs(duty)))
                            Catch : End Try
                        End If
                        If otype.Contains("PFR") Then
                            Try
                                Dim vol2 = DirectCast(obj, Reactor_PFR).Volume
                                eqSb.AppendFormat(",""volume_m3"":{0}", SafeNum(vol2))
                            Catch : End Try
                        End If
                    Catch
                    End Try
                    eqSb.Append("}")
                    eqFirst = False
                Next
                eqSb.Append("]")

                ' Score: call DWSIM's built-in scoring if available, otherwise return 0
                Dim score As Double = 0.0
                Dim scoreBreakdown As String = "{}"
                Try
                    ' Attempt to call the DWSIM scoring algorithm
                    ' This assumes sim has a method or property for scoring
                    ' Adjust to match your DWSIM scoring implementation
                    score = Flowsheet.FlowsheetOptions.Metadata.Score
                    scoreBreakdown = String.Format(
                    "{{""convergence"":{0}}}",
                    If(converged, "1.0", "0.0"))
                Catch
                    ' Scoring not available - return basic metrics
                    If converged Then score = 0.5 Else score = 0.0
                    scoreBreakdown = String.Format(
                    "{{""convergence"":{0}}}",
                    If(converged, "1.0", "0.0"))
                End Try

                body = String.Format(
                "{{""converged"":{0},""time_ms"":{1},""score"":{2},""score_breakdown"":{3}," &
                """diagnostics"":{{""unconverged_units"":{4},""errors"":{5}}}," &
                """key_results"":{{""product_streams"":{6}," &
                """total_heating_duty_kW"":{7},""total_cooling_duty_kW"":{8},""total_power_kW"":{9}," &
                """equipment_inventory"":{10}}}}}",
                If(converged, "true", "false"), elapsed,
                SafeNum(score), scoreBreakdown,
                uncSb.ToString(), errArr.ToString(),
                prodSb.ToString(),
                SafeNum(totalHeat), SafeNum(totalCool), SafeNum(totalPower),
                eqSb.ToString())

                ' ── GET /api/diagnostics ───────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/diagnostics" Then

                ' Sections info
                Dim secSb As New StringBuilder("[")
                Dim sFirst As Boolean = True
                For Each kvp In sectionRegistry
                    Dim secId = kvp.Key
                    Dim info = kvp.Value
                    Dim secType = CStr(info("type"))
                    Dim ports = DirectCast(info("ports"), Dictionary(Of String, String))

                    If Not sFirst Then secSb.Append(",")
                    secSb.AppendFormat("{{""section_id"":""{0}"",""type"":""{1}"",""ports"":{{", EscJ(secId), EscJ(secType))

                    Dim ppFirst As Boolean = True
                    For Each pk In ports
                        If Not ppFirst Then secSb.Append(",")
                        ' Check if the port stream is connected
                        Dim portStatus As String = "unconnected"
                        Try
                            Dim obj = FindObj(Flowsheet, pk.Value)
                            Dim hasIn As Boolean = False
                            Dim hasOut As Boolean = False
                            For Each conn In obj.GraphicObject.InputConnectors
                                If conn.IsAttached Then hasIn = True : Exit For
                            Next
                            For Each conn In obj.GraphicObject.OutputConnectors
                                If conn.IsAttached Then hasOut = True : Exit For
                            Next
                            If hasIn OrElse hasOut Then portStatus = "connected"
                        Catch
                        End Try
                        secSb.AppendFormat("""{0}"":""{1}""", EscJ(pk.Key), portStatus)
                        ppFirst = False
                    Next
                    secSb.Append("}}")
                    sFirst = False
                Next
                secSb.Append("]")

                ' Compound count
                Dim compCount As Integer = 0
                Try : compCount = Flowsheet.SelectedCompounds.Count : Catch : End Try

                ' Property package
                Dim ppNameDiag As String = ""
                Try
                    For Each pp In Flowsheet.PropertyPackages
                        ppNameDiag = pp.Value.ComponentName
                        Exit For
                    Next
                Catch
                End Try

                body = String.Format(
                "{{""sections"":{0},""compound_count"":{1},""property_package"":""{2}""}}",
                secSb.ToString(), compCount, EscJ(ppNameDiag))

                ' ── POST /api/modify-unit ──────────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/modify-unit" Then

                Dim objName = ExtractJsonString(body, "object_name")
                Dim obj = FindObj(Flowsheet, objName)

                ' Values go through the same setter the MCP tools use, so a name is matched
                ' against the property system, the dynamic properties and the model's own,
                ' and a calculation mode can be given by name. The previous version read the
                ' JSON with a regex, dropped anything quoted and swallowed every failure.
                Dim requested As JObject = Nothing
                Try
                    Dim parsed = JObject.Parse(body)
                    requested = TryCast(parsed("properties"), JObject)
                Catch ex As Exception
                    resp.StatusCode = 400
                    body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(ex.Message))
                    Return
                End Try

                If requested Is Nothing Then
                    resp.StatusCode = 400
                    body = "{""success"":false,""error"":""no properties given""}"
                    Return
                End If

                Dim modified As New List(Of String)
                Dim failures As New List(Of String)
                Dim system = Flowsheet.FlowsheetOptions.SelectedUnitSystem

                For Each entry In requested
                    Dim value As Object
                    Select Case entry.Value.Type
                        Case JTokenType.Boolean : value = entry.Value.Value(Of Boolean)()
                        Case JTokenType.Integer : value = entry.Value.Value(Of Long)()
                        Case JTokenType.Float : value = entry.Value.Value(Of Double)()
                        Case Else : value = entry.Value.ToString()
                    End Select

                    Try
                        If PropertySetter.TrySet(obj, entry.Key, value, system) Then
                            modified.Add(entry.Key)
                        Else
                            failures.Add(entry.Key & ": no such settable property")
                        End If
                    Catch ex As Exception
                        ' A rejected value carries the ones that would have worked, and that is
                        ' the whole point of reporting it rather than swallowing it.
                        failures.Add(entry.Key & ": " & ex.Message)
                    End Try
                Next

                Dim modifiedJson = "[" & String.Join(",", modified.Select(Function(m) """" & EscJ(m) & """")) & "]"
                Dim failedJson = "[" & String.Join(",", failures.Select(Function(f) """" & EscJ(f) & """")) & "]"

                body = String.Format("{{""success"":{0},""object"":""{1}"",""modified_properties"":{2},""failed"":{3}}}",
                                     If(failures.Count = 0, "true", "false"), EscJ(objName), modifiedJson, failedJson)

                ' ── POST /api/remove-section ───────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/remove-section" Then

                Dim payload = body
                Dim secId = ExtractJsonString(payload, "section_id")

                If Not sectionRegistry.ContainsKey(secId) Then
                    Throw New Exception("Section '" & secId & "' not found in registry")
                End If

                Dim info = sectionRegistry(secId)
                Dim objList = DirectCast(info("objects"), List(Of String))
                Dim removedSb As New StringBuilder("[")
                Dim rFirst As Boolean = True

                For Each oname In objList
                    Try
                        Dim obj = FindObj(Flowsheet, oname)
                        Flowsheet.DeleteSelectedObject(Me, New EventArgs(), obj, False, False)
                        If Not rFirst Then removedSb.Append(",")
                        removedSb.AppendFormat("""{0}""", EscJ(oname))
                        rFirst = False
                    Catch
                    End Try
                Next
                removedSb.Append("]")
                sectionRegistry.Remove(secId)

                body = String.Format("{{""success"":true,""section_id"":""{0}"",""removed_objects"":{1}}}",
                EscJ(secId), removedSb.ToString())

                ' ── GET /api/list-sections ─────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/list-sections" Then

                Dim secSb As New StringBuilder("[")
                Dim sFirst As Boolean = True
                For Each kvp In sectionRegistry
                    If Not sFirst Then secSb.Append(",")
                    Dim info = kvp.Value
                    Dim ports = DirectCast(info("ports"), Dictionary(Of String, String))
                    secSb.AppendFormat("{{""section_id"":""{0}"",""type"":""{1}"",""ports"":{{",
                    EscJ(kvp.Key), EscJ(CStr(info("type"))))
                    Dim ppFirst As Boolean = True
                    For Each pk In ports
                        If Not ppFirst Then secSb.Append(",")
                        Dim portStatus As String = "unconnected"
                        Try
                            Dim obj = FindObj(Flowsheet, pk.Value)
                            For Each conn In obj.GraphicObject.InputConnectors
                                If conn.IsAttached Then portStatus = "connected" : Exit For
                            Next
                            If portStatus = "unconnected" Then
                                For Each conn In obj.GraphicObject.OutputConnectors
                                    If conn.IsAttached Then portStatus = "connected" : Exit For
                                Next
                            End If
                        Catch
                        End Try
                        secSb.AppendFormat("""{0}"":""{1}""", EscJ(pk.Key), portStatus)
                        ppFirst = False
                    Next
                    secSb.Append("}}")
                    sFirst = False
                Next
                secSb.Append("]")
                body = String.Format("{{""sections"":{0}}}", secSb.ToString())

                ' ── POST /api/auto-layout ─────────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/auto-layout" Then

                Try
                    Flowsheet.RunCodeOnUIThread(Sub()
                                                    Flowsheet.NaturalLayout()
                                                    Flowsheet.UpdateInterface()
                                                End Sub)
                    body = "{""success"":true,""message"":""Flowsheet layout optimized.""}"
                Catch ex As Exception
                    body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(ex.Message))
                End Try

                ' ── GET /api/fluent/catalog ────────────────────────────────────────
                ' Discoverable FluentAPI surface: property packages, roles+variants,
                ' Plus-license status, supported quantity-unit names. Call this BEFORE
                ' /api/fluent/sweep so variant choices reference real installed builders.
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/fluent/catalog" Then

                Try
                    body = FluentSweep.CatalogJson(Flowsheet)
                Catch ex As Exception
                    body = String.Format("{{""error"":""{0}""}}", EscJ(ex.Message))
                End Try

                ' ── POST /api/fluent/sweep ─────────────────────────────────────────
                ' Builds the cartesian product of (property package) × (per-role variant)
                ' as headless flowsheets via the FluentAPI, solves & scores each, returns
                ' the ranked candidate list. Does NOT touch the live flowsheet - call
                ' /api/fluent/materialise with the chosen sweep_id+candidate_id to commit.
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/fluent/sweep" Then

                Dim intent = FluentSweep.ParseIntent(body)
                Dim results = FluentSweep.Sweep(intent)
                Dim sweepId = FluentSweep.CacheSweep(results)

                Dim sb As New StringBuilder()
                sb.Append("{""sweep_id"":""").Append(EscJ(sweepId)).Append(""",""candidates"":[")
                For i = 0 To results.Count - 1
                    If i > 0 Then sb.Append(",")
                    sb.Append(FluentSweep.CandidateJson(results(i)))
                Next
                sb.Append("]}")
                body = sb.ToString()

                ' ── POST /api/fluent/materialise ───────────────────────────────────
                ' Body: { "sweep_id": "...", "candidate_id": "cand_N" } - clears the
                ' live flowsheet and replays the chosen candidate's build script on it.
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/fluent/materialise" Then

                Dim sweepId = ExtractJsonString(body, "sweep_id")
                Dim candId = ExtractJsonString(body, "candidate_id")
                Dim results = FluentSweep.GetSweep(sweepId)
                If results Is Nothing Then
                    body = String.Format("{{""success"":false,""error"":""sweep_id '{0}' not found or expired""}}", EscJ(sweepId))
                Else
                    Dim winner = results.FirstOrDefault(Function(c) c.Id = candId)
                    If winner Is Nothing Then
                        body = String.Format("{{""success"":false,""error"":""candidate_id '{0}' not found in sweep""}}", EscJ(candId))
                    Else
                        Dim finished As Boolean = False, busy As Boolean = False
                        Dim mErr As Exception = Nothing
                        While Not finished
                            If Not busy Then
                                busy = True
                                Flowsheet.RunCodeOnUIThread(Sub()
                                                                Try
                                                                    FluentSweep.MaterialiseToLive(Flowsheet, winner)
                                                                    Flowsheet.NaturalLayout()
                                                                    Flowsheet.UpdateInterface()
                                                                Catch ex As Exception
                                                                    mErr = ex
                                                                Finally
                                                                    finished = True
                                                                End Try
                                                            End Sub)
                            End If
                            Threading.Thread.Sleep(200)
                        End While
                        If mErr Is Nothing Then
                            body = String.Format("{{""success"":true,""candidate_id"":""{0}"",""property_package"":""{1}""}}",
                                                 EscJ(candId), EscJ(winner.PropertyPackage))
                        Else
                            body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(mErr.Message))
                        End If
                    End If
                End If

                ' ── POST /api/fluent/build ─────────────────────────────────────────
                ' Single-shot: builds ONE flowsheet directly on the live flowsheet using
                ' the FluentAPI from the same DesignIntent shape as /api/fluent/sweep
                ' (max_candidates is forced to 1, the first PP and first variant per role
                '  win). Useful for follow-up tweaks that don't need a sweep.
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/fluent/build" Then

                Dim intent = FluentSweep.ParseIntent(body)
                intent.MaxCandidates = 1
                ' Pin variants to first choice each so a single candidate is generated.
                For Each r In intent.Roles
                    If r.Variants.Count > 1 Then
                        Dim first = r.Variants(0)
                        r.Variants.Clear() : r.Variants.Add(first)
                    End If
                Next
                Dim results = FluentSweep.Sweep(intent)
                If results.Count = 0 Then
                    body = "{""success"":false,""error"":""sweep produced no candidates""}"
                Else
                    Dim winner = results(0)
                    Dim finished As Boolean = False, busy As Boolean = False
                    Dim mErr As Exception = Nothing
                    While Not finished
                        If Not busy Then
                            busy = True
                            Flowsheet.RunCodeOnUIThread(Sub()
                                                            Try
                                                                FluentSweep.MaterialiseToLive(Flowsheet, winner)
                                                                Flowsheet.NaturalLayout()
                                                                Flowsheet.UpdateInterface()
                                                            Catch ex As Exception
                                                                mErr = ex
                                                            Finally
                                                                finished = True
                                                            End Try
                                                        End Sub)
                        End If
                        Threading.Thread.Sleep(200)
                    End While
                    If mErr Is Nothing Then
                        body = "{""success"":true,""candidate"":" & FluentSweep.CandidateJson(winner) & "}"
                    Else
                        body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(mErr.Message))
                    End If
                End If

                ' ── POST /api/add-graphic-object ────────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/add-graphic-object" Then

                Dim objType = ExtractJsonString(body, "object_type").ToLower()
                Dim x = CInt(ExtractJsonDouble(body, "x", 50))
                Dim y = CInt(ExtractJsonDouble(body, "y", 50))
                Dim text = ExtractJsonString(body, "text")
                Dim fontSize = ExtractJsonDouble(body, "font_size", 12)
                Dim tag = ExtractJsonString(body, "tag")

                Dim gobj As Drawing.SkiaSharp.GraphicObjects.GraphicObject = Nothing
                Dim mErr As Exception = Nothing
                Dim finished As Boolean = False

                Flowsheet.RunCodeOnUIThread(Sub()
                                                Try
                                                    Select Case objType
                                                        Case "text"
                                                            Dim t As New Drawing.SkiaSharp.GraphicObjects.TextGraphic(x, y, If(text = "", "Text", text))
                                                            t.Size = fontSize
                                                            gobj = t
                                                        Case "htmltext"
                                                            Dim t As New Drawing.SkiaSharp.GraphicObjects.TextGraphic(x, y, If(text = "", "<b>HTML Text</b>", text))
                                                            t.Size = fontSize
                                                            gobj = t
                                                            gobj.ObjectType = Enums.GraphicObjects.ObjectType.GO_HTMLText
                                                        Case "button"
                                                            Dim b As New Drawing.SkiaSharp.GraphicObjects.Shapes.ButtonGraphic()
                                                            b.X = x : b.Y = y
                                                            b.Text = If(text = "", "Button", text)
                                                            gobj = b
                                                        Case "rectangle"
                                                            Dim r As New Drawing.SkiaSharp.GraphicObjects.Shapes.RectangleGraphic()
                                                            r.X = x : r.Y = y
                                                            r.Text = If(text = "", "", text)
                                                            gobj = r
                                                        Case "table"
                                                            Dim t As New Drawing.SkiaSharp.GraphicObjects.Tables.TableGraphic(x, y)
                                                            t.Flowsheet = Flowsheet
                                                            gobj = t
                                                        Case "mastertable"
                                                            Dim t As New Drawing.SkiaSharp.GraphicObjects.Tables.MasterTableGraphic(x, y)
                                                            t.Flowsheet = Flowsheet
                                                            gobj = t
                                                        Case "spreadsheettable"
                                                            Dim t As New Drawing.SkiaSharp.GraphicObjects.Tables.SpreadsheetTableGraphic(x, y)
                                                            t.Flowsheet = Flowsheet
                                                            gobj = t
                                                        Case "chart"
                                                            ' OxyPlotGraphic lives in the Extended assembly — use reflection
                                                            Dim chartType = Type.GetType("DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts.OxyPlotGraphic, DWSIM.DrawingTools.SkiaSharp.Extended")
                                                            If chartType Is Nothing Then
                                                                mErr = New InvalidOperationException("Chart graphic type not available in this build.")
                                                            Else
                                                                Dim c = DirectCast(Activator.CreateInstance(chartType, New Object() {x, y}), Drawing.SkiaSharp.GraphicObjects.GraphicObject)
                                                                c.Flowsheet = Flowsheet
                                                                gobj = c
                                                            End If
                                                        Case Else
                                                            mErr = New ArgumentException("Unknown graphic object type: " & objType)
                                                    End Select

                                                    If gobj IsNot Nothing Then
                                                        gobj.Name = Guid.NewGuid().ToString()
                                                        If tag <> "" Then gobj.Tag = tag
                                                        Flowsheet.AddGraphicObject(gobj)
                                                    End If
                                                Catch ex As Exception
                                                    mErr = ex
                                                Finally
                                                    finished = True
                                                End Try
                                            End Sub)
                While Not finished : Threading.Thread.Sleep(50) : End While

                If mErr IsNot Nothing Then
                    resp.StatusCode = 400
                    body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(mErr.Message))
                Else
                    body = String.Format("{{""success"":true,""name"":""{0}"",""object_type"":""{1}"",""x"":{2},""y"":{3}}}",
                                         EscJ(gobj.Name), EscJ(objType), gobj.X, gobj.Y)
                End If

                ' ── POST /api/edit-graphic-object ───────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/edit-graphic-object" Then

                Dim objName = ExtractJsonString(body, "name")
                Dim mErr As Exception = Nothing
                Dim finished As Boolean = False
                Dim resultJson As String = ""

                Flowsheet.RunCodeOnUIThread(Sub()
                                                Try
                                                    Dim surface = DirectCast(Flowsheet.GetSurface(), Drawing.SkiaSharp.GraphicsSurface)
                                                    Dim gobj = surface.DrawingObjects.FirstOrDefault(Function(o) o.Name = objName)
                                                    If gobj Is Nothing Then
                                                        mErr = New ArgumentException("Graphic object not found: " & objName)
                                                        finished = True
                                                        Return
                                                    End If

                                                    ' Position
                                                    Dim nx = ExtractJsonDouble(body, "x", Double.NaN)
                                                    Dim ny = ExtractJsonDouble(body, "y", Double.NaN)
                                                    If Not Double.IsNaN(nx) Then gobj.X = CInt(nx)
                                                    If Not Double.IsNaN(ny) Then gobj.Y = CInt(ny)

                                                    ' Size
                                                    Dim nw = ExtractJsonDouble(body, "width", Double.NaN)
                                                    Dim nh = ExtractJsonDouble(body, "height", Double.NaN)
                                                    If Not Double.IsNaN(nw) Then gobj.Width = CInt(nw)
                                                    If Not Double.IsNaN(nh) Then gobj.Height = CInt(nh)

                                                    ' Tag
                                                    Dim newTag = ExtractJsonString(body, "tag")
                                                    If newTag <> "" Then gobj.Tag = newTag

                                                    ' Text (for TextGraphic, ButtonGraphic, RectangleGraphic)
                                                    Dim newText = ExtractJsonString(body, "text")
                                                    If newText <> "" Then
                                                        If TypeOf gobj Is Drawing.SkiaSharp.GraphicObjects.TextGraphic Then
                                                            DirectCast(gobj, Drawing.SkiaSharp.GraphicObjects.TextGraphic).Text = newText
                                                        ElseIf TypeOf gobj Is Drawing.SkiaSharp.GraphicObjects.Shapes.ButtonGraphic Then
                                                            DirectCast(gobj, Drawing.SkiaSharp.GraphicObjects.Shapes.ButtonGraphic).Text = newText
                                                        ElseIf TypeOf gobj Is Drawing.SkiaSharp.GraphicObjects.Shapes.RectangleGraphic Then
                                                            DirectCast(gobj, Drawing.SkiaSharp.GraphicObjects.Shapes.RectangleGraphic).Text = newText
                                                        End If
                                                    End If

                                                    ' Font size (for TextGraphic)
                                                    Dim fs = ExtractJsonDouble(body, "font_size", Double.NaN)
                                                    If Not Double.IsNaN(fs) AndAlso TypeOf gobj Is Drawing.SkiaSharp.GraphicObjects.TextGraphic Then
                                                        DirectCast(gobj, Drawing.SkiaSharp.GraphicObjects.TextGraphic).Size = fs
                                                    End If

                                                    resultJson = String.Format("{{""success"":true,""name"":""{0}"",""x"":{1},""y"":{2},""width"":{3},""height"":{4}}}",
                                                                               EscJ(gobj.Name), gobj.X, gobj.Y, gobj.Width, gobj.Height)
                                                Catch ex As Exception
                                                    mErr = ex
                                                Finally
                                                    finished = True
                                                End Try
                                            End Sub)
                While Not finished : Threading.Thread.Sleep(50) : End While

                If mErr IsNot Nothing Then
                    resp.StatusCode = 400
                    body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(mErr.Message))
                Else
                    body = resultJson
                End If

                ' ── POST /api/remove-graphic-object ─────────────────────────────────
            ElseIf req.HttpMethod = "POST" AndAlso path = "/api/remove-graphic-object" Then

                Dim objName = ExtractJsonString(body, "name")
                Dim mErr As Exception = Nothing
                Dim finished As Boolean = False

                Flowsheet.RunCodeOnUIThread(Sub()
                                                Try
                                                    Dim surface = DirectCast(Flowsheet.GetSurface(), Drawing.SkiaSharp.GraphicsSurface)
                                                    Dim gobj = surface.DrawingObjects.FirstOrDefault(Function(o) o.Name = objName)
                                                    If gobj Is Nothing Then
                                                        mErr = New ArgumentException("Graphic object not found: " & objName)
                                                    Else
                                                        surface.DeleteSelectedObject(DirectCast(gobj, Drawing.SkiaSharp.GraphicObjects.GraphicObject))
                                                    End If
                                                Catch ex As Exception
                                                    mErr = ex
                                                Finally
                                                    finished = True
                                                End Try
                                            End Sub)
                While Not finished : Threading.Thread.Sleep(50) : End While

                If mErr IsNot Nothing Then
                    resp.StatusCode = 400
                    body = String.Format("{{""success"":false,""error"":""{0}""}}", EscJ(mErr.Message))
                Else
                    body = "{""success"":true}"
                End If

                ' ── GET /api/graphic-objects ─────────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/graphic-objects" Then

                Dim surface = DirectCast(Flowsheet.GetSurface(), Drawing.SkiaSharp.GraphicsSurface)
                Dim sb As New StringBuilder("[")
                Dim first As Boolean = True
                For Each gobj In surface.DrawingObjects
                    Dim ot = gobj.ObjectType
                    ' Only include annotation/display objects, not unit operations
                    If ot = Enums.GraphicObjects.ObjectType.GO_Text OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_HTMLText OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_Table OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_MasterTable OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_SpreadsheetTable OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_Chart OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_Button OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_Rectangle OrElse
                       ot = Enums.GraphicObjects.ObjectType.GO_Image Then
                        If Not first Then sb.Append(",")
                        sb.AppendFormat("{{""name"":""{0}"",""tag"":""{1}"",""type"":""{2}"",""x"":{3},""y"":{4},""width"":{5},""height"":{6}}}",
                                        EscJ(gobj.Name), EscJ(If(gobj.Tag, "")), ot.ToString(), gobj.X, gobj.Y, gobj.Width, gobj.Height)
                        first = False
                    End If
                Next
                sb.Append("]")
                body = sb.ToString()

                ' ── GET /api/screenshot (base64) ────────────────────────────────────
            ElseIf req.HttpMethod = "GET" AndAlso path = "/api/screenshot-base64" Then

                Dim tmpFile As String = IO.Path.Combine(IO.Path.GetTempPath(), "dwsim_screenshot_" & Guid.NewGuid().ToString("N") & ".png")
                Flowsheet.SavePFDScreenshotToPNG(tmpFile)
                Dim pngBytes As Byte() = File.ReadAllBytes(tmpFile)
                File.Delete(tmpFile)
                Dim b64 = Convert.ToBase64String(pngBytes)
                body = String.Format("{{""success"":true,""format"":""png"",""base64"":""{0}""}}", b64)

                ' ── /api/dynamics/* ─────────────────────────────────────────────────
                ' Every dynamic-simulation route lives in DynamicsRoutes; this chain is long
                ' enough already, and the whole surface shares one shape.
            ElseIf path.StartsWith("/api/dynamics/") Then

                Dim dynamicsResult = DynamicsRoutes.Handle(Flowsheet, method, path, body,
                                                           Sub() Flowsheet.UpdateInterface())
                resp.StatusCode = dynamicsResult.StatusCode
                body = dynamicsResult.Body

            Else

                resp.StatusCode = 404
                body = """error"":""endpoint not found"""
                body = "{" & body & "}"

            End If

        Catch ex As Exception

            resp.StatusCode = 500
            body = String.Format("{{""error"":""{0}""}}", EscJ(ex.Message))

        End Try

        Dim bytes As Byte() = Encoding.UTF8.GetBytes(body)
        resp.ContentLength64 = bytes.Length
        resp.OutputStream.Write(bytes, 0, bytes.Length)
        resp.OutputStream.Close()

    End Sub

    ''' <summary>
    ''' Escapes a string for safe embedding as a JSON string value:
    ''' backslashes, double-quotes, and newline characters are escaped.
    ''' Returns an empty string when <paramref name="s"/> is <see langword="Nothing"/>.
    ''' </summary>
    ''' <param name="s">The raw string to escape.</param>
    ''' <returns>A JSON-safe string (without surrounding quotes).</returns>
    Function EscJ(s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "").Replace(vbLf, "\n")
    End Function

    ''' <summary>
    ''' Converts an object value to a JSON-compatible numeric literal.
    ''' Returns <c>"null"</c> for <see langword="Nothing"/>, NaN, or Infinity;
    ''' returns a quoted string for values that cannot be parsed as a <see cref="Double"/>.
    ''' </summary>
    ''' <param name="v">The value to convert.</param>
    ''' <returns>A JSON number literal, <c>"null"</c>, or a quoted string.</returns>
    Function SafeNum(v As Object) As String
        If v Is Nothing Then Return "null"
        Try
            Dim d As Double = Convert.ToDouble(v)
            If Double.IsNaN(d) OrElse Double.IsInfinity(d) Then Return "null"
            Return d.ToString("G", System.Globalization.CultureInfo.InvariantCulture)
        Catch
            Return String.Format("""{0}""", EscJ(v.ToString()))
        End Try
    End Function

    ''' <summary>
    ''' Looks up a simulation object by tag or GUID in the active flowsheet.
    ''' </summary>
    ''' <param name="sim">The flowsheet to search.</param>
    ''' <param name="name">Object tag or internal GUID.</param>
    ''' <returns>The matching <see cref="ISimulationObject"/>.</returns>
    ''' <exception cref="KeyNotFoundException">Thrown when no object with the given name is found.</exception>
    Function FindObj(sim As IFlowsheet, name As String) As ISimulationObject
        ' Try direct GUID lookup first
        Dim obj = Flowsheet.GetObject(name)
        If obj IsNot Nothing Then Return obj
        Throw New KeyNotFoundException(String.Format("Object '{0}' not found", name))
    End Function

    ''' <summary>Extracts a numeric value for <paramref name="key"/> from a JSON string without an external library.</summary>
    ''' <param name="json">Raw JSON text.</param>
    ''' <param name="key">The property key to look up.</param>
    ''' <returns>The parsed <see cref="Double"/>, or <c>0.0</c> if the key is absent.</returns>
    Function ExtractJsonDouble(json As String, key As String) As Double
        Return ExtractJsonDouble(json, key, 0.0)
    End Function

    Function ExtractJsonDouble(json As String, key As String, defaultValue As Double) As Double
        ' Minimal JSON double extractor (no external library needed)
        Dim pattern As String = """" & key & """" & "\s*:\s*(-?[\d.eE+\-]+)"
        Dim m = System.Text.RegularExpressions.Regex.Match(json, pattern)
        If m.Success Then
            Return Double.Parse(m.Groups(1).Value, System.Globalization.CultureInfo.InvariantCulture)
        End If
        Return defaultValue
    End Function

    ''' <summary>Extracts a string value for <paramref name="key"/> from a JSON string.</summary>
    ''' <param name="json">Raw JSON text.</param>
    ''' <param name="key">The property key to look up.</param>
    ''' <returns>The unescaped string value, or an empty string if the key is absent.</returns>
    Function ExtractJsonString(json As String, key As String) As String
        Dim pattern As String = """" & key & """" & "\s*:\s*""([^""]*)"""
        Dim m = System.Text.RegularExpressions.Regex.Match(json, pattern)
        If m.Success Then Return m.Groups(1).Value
        Return ""
    End Function

    ''' <summary>Extracts a JSON string array (<c>"key":["a","b","c"]</c>) as a <see cref="List(Of String)"/>.</summary>
    ''' <param name="json">Raw JSON text.</param>
    ''' <param name="key">The property key whose value is the array.</param>
    ''' <returns>A list of string items, or an empty list if the key is absent.</returns>
    Function ExtractJsonArray(json As String, key As String) As List(Of String)
        ' Extract an array of strings from JSON: "key":["a","b","c"]
        Dim result As New List(Of String)
        Dim arrPattern As String = """" & key & """" & "\s*:\s*\[([^\]]*)\]"
        Dim m = System.Text.RegularExpressions.Regex.Match(json, arrPattern)
        If m.Success Then
            Dim inner = m.Groups(1).Value
            Dim itemPattern As String = """([^""]*)"""
            Dim matches = System.Text.RegularExpressions.Regex.Matches(inner, itemPattern)
            For Each im As System.Text.RegularExpressions.Match In matches
                result.Add(im.Groups(1).Value)
            Next
        End If
        Return result
    End Function

    ''' <summary>
    ''' Extracts a nested JSON object (<c>"key":{...}</c>) as a raw substring,
    ''' correctly handling arbitrarily nested braces.
    ''' </summary>
    ''' <param name="json">Raw JSON text.</param>
    ''' <param name="key">The property key whose value is an object.</param>
    ''' <returns>The raw JSON object string including surrounding braces, or an empty string if absent.</returns>
    Function ExtractJsonObject(json As String, key As String) As String
        ' Extract a nested JSON object as a raw string: "key":{...}
        ' Uses brace counting to handle nested objects.
        Dim keyPattern As String = """" & key & """" & "\s*:\s*\{"
        Dim m = System.Text.RegularExpressions.Regex.Match(json, keyPattern)
        If Not m.Success Then Return ""
        Dim startIdx As Integer = m.Index + m.Length - 1  ' points to the opening {
        Dim depth As Integer = 0
        For i As Integer = startIdx To json.Length - 1
            If json(i) = "{"c Then depth += 1
            If json(i) = "}"c Then depth -= 1
            If depth = 0 Then
                Return json.Substring(startIdx, i - startIdx + 1)
            End If
        Next
        Return ""
    End Function

    ' ══════════════════════════════════════════════════════════════════════════════
    ' SECTION FACTORY - Creates pre-wired groups of unit operations
    ' ══════════════════════════════════════════════════════════════════════════════

    ''' <summary>
    ''' Factory that creates a pre-wired group of DWSIM unit operations ("section") of the
    ''' specified type and registers it in the section registry.
    ''' </summary>
    ''' <param name="sim">The target flowsheet.</param>
    ''' <param name="sectionType">
    ''' One of: <c>"separation_flash"</c>, <c>"distillation"</c>, <c>"absorption"</c>,
    ''' <c>"heat_exchanger"</c>, <c>"reactor_cstr"</c>, <c>"reactor_pfr"</c>,
    ''' <c>"pump"</c>, <c>"compressor"</c>, <c>"mixer_splitter"</c>.
    ''' </param>
    ''' <param name="sectionId">Unique identifier used as a name prefix for all created objects.</param>
    ''' <param name="paramsJson">JSON object string with section-specific configuration parameters.</param>
    ''' <returns>
    ''' A dictionary with three entries:
    ''' <list type="bullet">
    ''' <item><c>"type"</c> - the <paramref name="sectionType"/> string.</item>
    ''' <item><c>"objects"</c> - <c>List(Of String)</c> tags of every created object.</item>
    ''' <item><c>"ports"</c> - <c>Dictionary(Of String, String)</c> mapping logical port names to stream tags.</item>
    ''' </list>
    ''' </returns>
    Function CreateSection(sim As IFlowsheet, sectionType As String, sectionId As String, paramsJson As String) As Dictionary(Of String, Object)

        ' Returns a dict with keys: "type", "objects" (List(Of String)), "ports" (Dict(Of String, String))

        ' ── FluentAPI dispatcher first ──────────────────────────────────────────
        ' Covers all typed FluentAPI builders not already in the legacy table:
        ' valve, pipe, tank, shortcut_column, gibbs_reactor, clean-energy,
        ' bioprocess externals, plus refining / electrolyte / advanced /
        ' ExtensionPack (license-gated). When the type isn't handled by the
        ' FluentSections registry, fall through to the legacy Select Case below
        ' so existing section_types keep working byte-for-byte.
        If FluentSections.IsHandled(sectionType) Then
            Return FluentSections.BuildSection(sim, sectionType, sectionId, paramsJson)
        End If

        Dim objects As New List(Of String)
        Dim ports As New Dictionary(Of String, String)
        Dim info As New Dictionary(Of String, Object)
        info("type") = sectionType
        info("objects") = objects
        info("ports") = ports

        Select Case sectionType

            Case "separation_flash"

                Dim feedName = sectionId & "_feed"
                Dim vapName = sectionId & "_vapor"
                Dim dutyName = sectionId & "_duty"
                Dim liqName = sectionId & "_liquid"
                Dim flashName = sectionId & "_flash"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 100, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 300, 100, vapName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 300, 300, liqName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 300, 300, dutyName)
                Dim vessel = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Vessel, 200, 200, flashName), Vessel)

                ' Connect internally
                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, flashName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, flashName).GraphicObject, FindObj(sim, vapName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, flashName).GraphicObject, FindObj(sim, liqName).GraphicObject, 1, 0)
                Flowsheet.ConnectObjects(FindObj(sim, dutyName).GraphicObject, FindObj(sim, flashName).GraphicObject, 0, -1)

                ' Set parameters
                vessel.CalculationMode = Vessel.CalculationModes.Legacy
                vessel.OverrideP = True
                vessel.OverrideT = True

                Dim tempC = ExtractJsonDouble(paramsJson, "temperature_C")
                Dim presKPa = ExtractJsonDouble(paramsJson, "pressure_kPa")
                If tempC <> 0 Then
                    vessel.FlashTemperature = tempC + 273.15
                End If
                If presKPa <> 0 Then
                    vessel.FlashPressure = presKPa * 1000
                End If

                objects.AddRange({feedName, vapName, liqName, flashName})
                ports("feed_in") = feedName
                ports("vapor_out") = vapName
                ports("liquid_out") = liqName

            Case "distillation"

                Dim feedName = sectionId & "_feed"
                Dim distName = sectionId & "_distillate"
                Dim botName = sectionId & "_bottoms"
                Dim colName = sectionId & "_column"
                Dim condEn = sectionId & "_condenser_energy"
                Dim rebEn = sectionId & "_reboiler_energy"

                Dim feed = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Dim dist = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 100, distName)
                Dim bottoms = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 300, botName)
                Dim column = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.DistillationColumn, 200, 200, colName), DistillationColumn)
                Dim condduty = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 350, 50, condEn)
                Dim rebduty = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 350, 350, rebEn)

                ' Connect: feed → column, column → distillate/bottoms, energy streams
                column.ConnectFeed(feed, 4)
                column.ConnectDistillate(dist)
                column.ConnectBottoms(bottoms)
                column.ConnectCondenserDuty(condduty)
                column.ConnectReboilerDuty(rebduty)

                column.ExternalLoopTolerance = 0.01
                column.InternalLoopTolerance = 0.01

                ' --- Column pressure and pressure drop ---
                Dim colPressKPa = ExtractJsonDouble(paramsJson, "column_pressure_kPa")
                Dim colDpKPa = ExtractJsonDouble(paramsJson, "total_pressure_drop_kPa")
                If colPressKPa > 0 Then
                    Try : column.SetTopPressure(colPressKPa * 1000) : Catch : End Try  ' kPa → Pa
                Else
                    column.SetTopPressure(101325)
                End If
                If colDpKPa > 0 Then
                    Try : column.ColumnPressureDrop = colDpKPa * 1000 : Catch : End Try     ' kPa → Pa
                Else
                    column.ColumnPressureDrop = 1000
                End If

                ' --- Condenser spec ---
                Dim condSpec = ExtractJsonString(paramsJson, "condenser_spec")
                Dim condValue = ExtractJsonDouble(paramsJson, "condenser_value")
                If condSpec <> "" Then
                    Try
                        Dim cSpec = column.Specs("C")
                        Select Case condSpec.ToLower()
                            Case "reflux_ratio"
                                cSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Stream_Ratio
                                cSpec.SpecValue = condValue
                            Case "distillate_rate"
                                cSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Product_Molar_Flow_Rate
                                cSpec.SpecValue = condValue
                            Case "temperature"
                                cSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Temperature
                                cSpec.SpecValue = condValue + 273.15  ' C → K
                            Case "heat_duty"
                                cSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Heat_Duty
                                cSpec.SpecValue = condValue
                            Case "feed_recovery"
                                cSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Component_Mass_Flow_Rate
                                cSpec.SpecValue = condValue
                        End Select
                    Catch ex As Exception
                        ' Log but don't fail - column may still solve with defaults
                    End Try
                End If

                ' --- Reboiler spec ---
                Dim rebSpec = ExtractJsonString(paramsJson, "reboiler_spec")
                Dim rebValue = ExtractJsonDouble(paramsJson, "reboiler_value")
                If rebSpec <> "" Then
                    Try
                        Dim rSpec = column.Specs("R")
                        Select Case rebSpec.ToLower()
                            Case "feed_recovery"
                                rSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Feed_Recovery
                                rSpec.SpecValue = rebValue
                            Case "bottoms_rate"
                                rSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Product_Molar_Flow_Rate
                                rSpec.SpecValue = rebValue
                            Case "temperature"
                                rSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Temperature
                                rSpec.SpecValue = rebValue + 273.15  ' C → K
                            Case "heat_duty"
                                rSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Heat_Duty
                                rSpec.SpecValue = rebValue
                            Case "boilup_ratio"
                                rSpec.SType = Auxiliary.SepOps.ColumnSpec.SpecType.Stream_Ratio
                                rSpec.SpecValue = rebValue
                        End Select
                    Catch ex As Exception
                        ' Log but don't fail
                    End Try
                End If

                ' Set column parameters
                Dim numStages = ExtractJsonDouble(paramsJson, "num_stages")
                Dim feedStage = ExtractJsonDouble(paramsJson, "feed_stage")
                Dim col = FindObj(sim, colName)
                If numStages > 0 Then column.SetNumberOfStages(numStages)
                If feedStage > 0 Then column.SetStreamFeedStage(feed.Name, Convert.ToInt32(feedStage))

                objects.AddRange({feedName, distName, botName, colName, condEn, rebEn})
                ports("feed_in") = feedName
                ports("distillate_out") = distName
                ports("bottoms_out") = botName

            Case "absorption"

                Dim gasInName = sectionId & "_gas_in"
                Dim solvInName = sectionId & "_solvent_in"
                Dim gasOutName = sectionId & "_gas_out"
                Dim liqOutName = sectionId & "_liquid_out"
                Dim colName = sectionId & "_column"

                Dim gasIn = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 300, gasInName)
                Dim solvIn = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 100, solvInName)
                Dim gasOut = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 100, gasOutName)
                Dim liqOut = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 300, liqOutName)
                Dim column = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.AbsorptionColumn, 200, 200, colName), AbsorptionColumn)

                column.ConnectFeed(gasIn, column.Stages.Count - 1)
                column.ConnectFeed(solvIn, 0)
                column.ConnectTopProduct(gasOut)
                column.ConnectBottoms(liqOut)

                objects.AddRange({gasInName, solvInName, gasOutName, liqOutName, colName})
                ports("gas_in") = gasInName
                ports("solvent_in") = solvInName
                ports("gas_out") = gasOutName
                ports("liquid_out") = liqOutName

            Case "reaction_cstr"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim rxName = sectionId & "_reactor"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.RCT_CSTR, 200, 200, rxName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, rxName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, enName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, -1) : Catch : End Try

                objects.AddRange({feedName, prodName, rxName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "reaction_pfr"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim rxName = sectionId & "_reactor"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.RCT_PFR, 200, 200, rxName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, rxName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, enName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, -1) : Catch : End Try

                objects.AddRange({feedName, prodName, rxName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "reaction_conversion"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim rxName = sectionId & "_reactor"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.RCT_Conversion, 200, 200, rxName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, rxName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, enName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, -1) : Catch : End Try

                objects.AddRange({feedName, prodName, rxName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "reaction_equilibrium"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim rxName = sectionId & "_reactor"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.RCT_Equilibrium, 200, 200, rxName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, rxName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, enName).GraphicObject, FindObj(sim, rxName).GraphicObject, 0, -1) : Catch : End Try

                objects.AddRange({feedName, prodName, rxName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "pump"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim pumpName = sectionId & "_pump"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Dim pump = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Pump, 200, 200, pumpName), Pump)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, pumpName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, pumpName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, enName).GraphicObject, FindObj(sim, pumpName).GraphicObject, 0, -1) : Catch : End Try


                Dim outPres = ExtractJsonDouble(paramsJson, "outlet_pressure_kPa")
                Dim eff = ExtractJsonDouble(paramsJson, "efficiency")
                If outPres > 0 Then
                    pump.CalcMode = Pump.CalculationMode.OutletPressure
                    Try : pump.Pout = outPres * 1000 : Catch : End Try
                Else
                    pump.CalcMode = Pump.CalculationMode.Delta_P
                    pump.DeltaP = 500000
                End If
                If eff > 0 Then
                    If eff < 1.0 Then eff *= 100
                    Try : pump.Efficiency = eff : Catch : End Try
                Else
                    pump.Efficiency = 75
                End If

                objects.AddRange({feedName, prodName, pumpName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "compressor"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim compName = sectionId & "_compressor"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Dim comp = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Compressor, 200, 200, compName), Compressor)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, compName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, compName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, enName).GraphicObject, FindObj(sim, compName).GraphicObject, 0, -1) : Catch : End Try

                Dim outPres = ExtractJsonDouble(paramsJson, "outlet_pressure_kPa")
                Dim eff = ExtractJsonDouble(paramsJson, "efficiency")
                If outPres > 0 Then
                    comp.CalcMode = Compressor.CalculationMode.OutletPressure
                    Try : comp.POut = outPres * 1000 : Catch : End Try
                Else
                    comp.CalcMode = Compressor.CalculationMode.PressureRatio
                    comp.PressureRatio = 10
                End If
                If eff > 0 Then
                    If eff < 1.0 Then eff *= 100
                    Try : comp.AdiabaticEfficiency = eff : Catch : End Try
                Else
                    comp.AdiabaticEfficiency = 75
                End If

                objects.AddRange({feedName, prodName, compName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "expander"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim expName = sectionId & "_expander"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Dim expander = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Expander, 200, 200, expName), Expander)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, expName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, expName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, expName).GraphicObject, FindObj(sim, enName).GraphicObject, -1, 0) : Catch : End Try


                Dim outPres = ExtractJsonDouble(paramsJson, "outlet_pressure_kPa")
                Dim eff = ExtractJsonDouble(paramsJson, "efficiency")
                If outPres > 0 Then
                    expander.CalcMode = Expander.CalculationMode.OutletPressure
                    Try : expander.POut = outPres * 1000 : Catch : End Try
                Else
                    expander.CalcMode = Expander.CalculationMode.PressureRatio
                    expander.PressureRatio = 10
                End If
                If eff > 0 Then
                    If eff < 1.0 Then eff *= 100
                    Try : expander.AdiabaticEfficiency = eff : Catch : End Try
                Else
                    expander.AdiabaticEfficiency = 75
                End If

                objects.AddRange({feedName, prodName, expName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "heat_exchanger"

                Dim hotIn = sectionId & "_hot_in"
                Dim hotOut = sectionId & "_hot_out"
                Dim coldIn = sectionId & "_cold_in"
                Dim coldOut = sectionId & "_cold_out"
                Dim hxName = sectionId & "_hx"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 150, hotIn)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 150, hotOut)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 250, coldIn)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 250, coldOut)
                Dim hx = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.HeatExchanger, 200, 200, hxName), HeatExchanger)

                hx.CalculationMode = HeatExchangerCalcMode.ThermalEfficiency
                hx.Efficiency = 75

                Flowsheet.ConnectObjects(FindObj(sim, hotIn).GraphicObject, FindObj(sim, hxName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, hxName).GraphicObject, FindObj(sim, hotOut).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, coldIn).GraphicObject, FindObj(sim, hxName).GraphicObject, 0, 1)
                Flowsheet.ConnectObjects(FindObj(sim, hxName).GraphicObject, FindObj(sim, coldOut).GraphicObject, 1, 0)

                objects.AddRange({hotIn, hotOut, coldIn, coldOut, hxName})
                ports("hot_in") = hotIn
                ports("hot_out") = hotOut
                ports("cold_in") = coldIn
                ports("cold_out") = coldOut

            Case "heater"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim htrName = sectionId & "_heater"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Dim heater = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Heater, 200, 200, htrName), Heater)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, htrName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, htrName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, enName).GraphicObject, FindObj(sim, htrName).GraphicObject, 0, -1) : Catch : End Try


                Dim outT = ExtractJsonDouble(paramsJson, "outlet_temperature_C")
                If outT <> 0 Then
                    heater.CalcMode = Heater.CalculationMode.OutletTemperature
                    Try : heater.OutletTemperature = outT + 273.15 : Catch : End Try
                Else
                    heater.CalcMode = Heater.CalculationMode.TemperatureChange
                    heater.DeltaT = 30
                End If

                objects.AddRange({feedName, prodName, htrName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "cooler"

                Dim feedName = sectionId & "_feed"
                Dim prodName = sectionId & "_product"
                Dim clrName = sectionId & "_cooler"
                Dim enName = sectionId & "_energy"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, feedName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, prodName)
                Dim cooler = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Cooler, 200, 200, clrName), Cooler)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.EnergyStream, 200, 350, enName)

                Flowsheet.ConnectObjects(FindObj(sim, feedName).GraphicObject, FindObj(sim, clrName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, clrName).GraphicObject, FindObj(sim, prodName).GraphicObject, 0, 0)
                Try : Flowsheet.ConnectObjects(FindObj(sim, clrName).GraphicObject, FindObj(sim, enName).GraphicObject, -1, 0) : Catch : End Try


                Dim outT = ExtractJsonDouble(paramsJson, "outlet_temperature_C")
                If outT <> 0 Then
                    cooler.CalcMode = Cooler.CalculationMode.OutletTemperature
                    Try : cooler.OutletTemperature = outT + 273.15 : Catch : End Try
                Else
                    cooler.CalcMode = Cooler.CalculationMode.TemperatureChange
                    cooler.TemperatureChange = 30
                End If

                objects.AddRange({feedName, prodName, clrName, enName})
                ports("feed_in") = feedName
                ports("product_out") = prodName

            Case "mixer"

                Dim in1Name = sectionId & "_in1"
                Dim in2Name = sectionId & "_in2"
                Dim outName = sectionId & "_product"
                Dim mixName = sectionId & "_mixer"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 150, in1Name)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 250, in2Name)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 200, outName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Mixer, 200, 200, mixName)

                Flowsheet.ConnectObjects(FindObj(sim, in1Name).GraphicObject, FindObj(sim, mixName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, in2Name).GraphicObject, FindObj(sim, mixName).GraphicObject, 0, 1)
                Flowsheet.ConnectObjects(FindObj(sim, mixName).GraphicObject, FindObj(sim, outName).GraphicObject, 0, 0)

                objects.AddRange({in1Name, in2Name, outName, mixName})
                ports("feed_in_1") = in1Name
                ports("feed_in_2") = in2Name
                ports("product_out") = outName

            Case "splitter"

                Dim inName = sectionId & "_feed"
                Dim out1Name = sectionId & "_out1"
                Dim out2Name = sectionId & "_out2"
                Dim splName = sectionId & "_splitter"

                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, 200, inName)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 150, out1Name)
                Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, 250, out2Name)
                Dim splitter = DirectCast(Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.Splitter, 200, 200, splName), Splitter)

                splitter.OperationMode = Splitter.OpMode.SplitRatios
                splitter.Ratios(0) = 0.5

                Flowsheet.ConnectObjects(FindObj(sim, inName).GraphicObject, FindObj(sim, splName).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, splName).GraphicObject, FindObj(sim, out1Name).GraphicObject, 0, 0)
                Flowsheet.ConnectObjects(FindObj(sim, splName).GraphicObject, FindObj(sim, out2Name).GraphicObject, 1, 0)

                objects.AddRange({inName, out1Name, out2Name, splName})
                ports("feed_in") = inName
                ports("product_out_1") = out1Name
                ports("product_out_2") = out2Name

            'generic
            Case "generic_script"

                ' Material connector indices on CustomUO: 0,1,2,4,5,6  (index 3 = energy - skip)
                Dim matConnIdx() As Integer = {0, 1, 2, 4, 5, 6}

                ' Number of inlets / outlets - default 1, max 6
                Dim rawIn = ExtractJsonDouble(paramsJson, "num_inlets")
                Dim numIn = If(rawIn > 0, CInt(Math.Min(6, rawIn)), 1)
                Dim rawOut = ExtractJsonDouble(paramsJson, "num_outlets")
                Dim numOut = If(rawOut > 0, CInt(Math.Min(6, rawOut)), 1)

                Dim uoName = sectionId & "_scriptuo"
                Dim scriptUO = sim.AddObject(Enums.GraphicObjects.ObjectType.CustomUO, 200, 200, uoName)

                ' Create inlet streams and connect each to the corresponding CustomUO input connector
                For i As Integer = 0 To numIn - 1
                    Dim sName = sectionId & "_feed_" & i
                    Dim ypos = CInt(200 + (i - (numIn - 1) / 2.0) * 80)
                    Dim stream = Flowsheet.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 50, ypos, sName)
                    sim.Connect(stream, scriptUO, 0, matConnIdx(i))
                    objects.Add(sName)
                    ports("feed_in_" & i) = sName
                Next
                If numIn = 1 Then ports("feed_in") = sectionId & "_feed_0"

                ' Create outlet streams and connect from the corresponding CustomUO output connector
                For i As Integer = 0 To numOut - 1
                    Dim sName = sectionId & "_product_" & i
                    Dim ypos = CInt(200 + (i - (numOut - 1) / 2.0) * 80)
                    Dim stream = sim.AddObject(Enums.GraphicObjects.ObjectType.MaterialStream, 350, ypos, sName)
                    sim.Connect(scriptUO, stream, matConnIdx(i), 0)
                    objects.Add(sName)
                    ports("product_out_" & i) = sName
                Next
                If numOut = 1 Then ports("product_out") = sectionId & "_product_0"

                objects.Add(uoName)

                ' Inject the IronPython script text provided by the LLM
                Dim scriptCode = ExtractJsonString(paramsJson, "script").Replace("\n", vbCrLf)
                If scriptCode <> "" Then scriptUO.ScriptText = scriptCode

            Case Else

                Throw New Exception("Unknown section type: " & sectionType)

        End Select

        Return info

    End Function


End Class
