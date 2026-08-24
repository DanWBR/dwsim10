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

Imports System.Text
Imports System.Text.RegularExpressions
Imports DWSIM.Interfaces
Imports DWSIM.UnitOperations.UnitOperations
Imports DWSIM.UnitOperations.Reactors
Imports DWSIM.Automation.FluentAPI
Imports DWSIM.Automation.FluentAPI.Builders
Imports FAPI = DWSIM.Automation.FluentAPI
Imports Newtonsoft.Json.Linq

''' <summary>
''' FluentAPI-powered sweep-and-rank engine for the assistant Design Mode.
''' <para>
''' Given a <see cref="DesignIntent"/> the engine enumerates the cartesian product
''' of (property package) × (per-role variant), builds each candidate as a headless
''' <see cref="FAPI.Flowsheet"/>, solves it with <c>TrySolve</c>, scores the result and
''' returns the ranked list. The winning candidate's build script can be replayed on
''' the live flowsheet via <see cref="MaterialiseToLive"/>.
''' </para>
''' <para>
''' Topology is currently restricted to a single linear chain - each role consumes
''' the previous role's main outlet stream and produces one main outlet stream. Side
''' products (column bottoms, flash liquid, splitter side cuts) are exposed as
''' product streams but are not fed forward.
''' </para>
''' </summary>
Public Class FluentSweep

    ' ── Public DTOs ────────────────────────────────────────────────────────────

    Public Class DesignIntent
        Public Property Compounds As New List(Of String)
        Public Property FeedTemperatureC As Double = 25.0
        Public Property FeedPressureKPa As Double = 101.325
        Public Property FeedFlowKgPerHour As Double = 1000.0
        Public Property FeedComposition As New Dictionary(Of String, Double)  ' molar fractions
        Public Property Objective As String = ""
        Public Property Roles As New List(Of RoleSpec)
        Public Property AllowedPropertyPackages As New List(Of String)
        Public Property MaxCandidates As Integer = 8
        Public Property ScoringCriteria As String = ""
    End Class

    Public Class RoleSpec
        Public Property Role As String = ""             ' "heater" | "cooler" | "pump" | ...
        Public Property Variants As New List(Of String) ' builder-name choices for this role
        Public Property Params As New Dictionary(Of String, Double)
    End Class

    Public Class CandidateResult
        Public Property Id As String = ""
        Public Property PropertyPackage As String = ""
        Public Property RoleChoices As New List(Of String) ' parallel to DesignIntent.Roles
        Public Property Converged As Boolean = False
        Public Property Score As Double = 0.0
        Public Property ScoreBreakdown As New Dictionary(Of String, Double)
        Public Property Diagnostics As New List(Of String)
        Public Property EquipmentInventory As New List(Of Dictionary(Of String, Object))
        Public Property TotalHeatingDutyKW As Double = 0.0
        Public Property TotalCoolingDutyKW As Double = 0.0
        Public Property TotalPowerKW As Double = 0.0
        Public Property ProductStreams As New Dictionary(Of String, Dictionary(Of String, Object))
        Public Property Intent As DesignIntent  ' kept for replay
    End Class

    ' ── Sweep cache (in-memory; sweep_id → results) ────────────────────────────

    Private Shared SweepCache As New Dictionary(Of String, List(Of CandidateResult))

    Public Shared Function CacheSweep(results As List(Of CandidateResult)) As String
        Dim id = "sw_" & DateTime.UtcNow.Ticks.ToString()
        SyncLock SweepCache
            SweepCache(id) = results
            ' bound cache size
            If SweepCache.Count > 32 Then
                Dim oldest = SweepCache.Keys.First()
                SweepCache.Remove(oldest)
            End If
        End SyncLock
        Return id
    End Function

    Public Shared Function GetSweep(sweepId As String) As List(Of CandidateResult)
        SyncLock SweepCache
            If SweepCache.ContainsKey(sweepId) Then Return SweepCache(sweepId)
        End SyncLock
        Return Nothing
    End Function

    ' ── Catalog ────────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Returns a JSON description of the FluentAPI surface the assistant can drive.
    ''' Lightweight enough to be shipped to the LLM so it grounds its variant choices.
    ''' </summary>
    Public Shared Function CatalogJson(live As IFlowsheet) As String
        Dim sb As New StringBuilder()
        sb.Append("{")

        ' Property packages (free + Plus that loaded successfully)
        Dim pps As IReadOnlyList(Of String)
        Try
            pps = FAPI.Flowsheet.Wrap(live).AvailablePropertyPackages
        Catch
            pps = New List(Of String)()
        End Try
        sb.Append("""available_property_packages"":[")
        For i = 0 To pps.Count - 1
            If i > 0 Then sb.Append(",")
            sb.Append("""").Append(EscJ2(pps(i))).Append("""")
        Next
        sb.Append("],")

        ' Plus / activated
        Dim isPlus As Boolean = False
        Try : isPlus = License.IsActivated : Catch : End Try
        sb.Append("""is_plus_activated"":").Append(If(isPlus, "true", "false")).Append(",")

        ' Roles + their variants (curated from RoleDispatch)
        sb.Append("""roles"":{")
        Dim first As Boolean = True
        For Each kv In RoleVariants
            If Not first Then sb.Append(",")
            sb.Append("""").Append(EscJ2(kv.Key)).Append(""":[")
            For i = 0 To kv.Value.Count - 1
                If i > 0 Then sb.Append(",")
                sb.Append("""").Append(EscJ2(kv.Value(i))).Append("""")
            Next
            sb.Append("]")
            first = False
        Next
        sb.Append("},")

        ' Quantity helpers (informational)
        sb.Append("""quantity_units"":[""Kelvin"",""Celsius"",""Pascal"",""KiloPascal"",""Bar"",""Atm"",""KgPerSecond"",""KgPerHour"",""MolPerSecond"",""KmolPerHour"",""Kilowatts"",""Megawatts""],")

        ' Dynamic simulation. This block is how the assistant learns that the time domain exists
        ' at all, and in what order to drive it.
        sb.Append("""dynamics"":").Append(DynamicsCatalogJson())

        sb.Append("}")
        Return sb.ToString()
    End Function

    ''' <summary>
    ''' Describes the dynamic-simulation surface: the workflow, the routes, the enumerations the
    ''' endpoints accept, the diagnostic codes they emit, and the limits on returned time series.
    ''' </summary>
    Private Shared Function DynamicsCatalogJson() As String

        Dim o As New JObject From {
            {"supported", True},
            {"workflow", New JArray("inspect", "properties", "setup", "monitor", "event",
                                    "check", "run", "status", "series|analyze", "diagnose|tune-pid")},
            {"endpoints", New JObject From {
                {"inspect", "GET /api/dynamics/inspect"},
                {"properties", "GET /api/dynamics/properties"},
                {"check", "GET /api/dynamics/check"},
                {"setup", "POST /api/dynamics/setup"},
                {"monitor", "POST /api/dynamics/monitor"},
                {"event", "POST /api/dynamics/event"},
                {"object", "POST /api/dynamics/object"},
                {"controller", "POST /api/dynamics/controller"},
                {"state", "POST /api/dynamics/state"},
                {"run", "POST /api/dynamics/run"},
                {"status", "GET /api/dynamics/status/{run_id}"},
                {"abort", "POST /api/dynamics/abort/{run_id}"},
                {"series", "GET /api/dynamics/series/{run_id}"},
                {"analyze", "GET /api/dynamics/analyze/{run_id}"},
                {"diagnose", "GET /api/dynamics/diagnose/{run_id}"},
                {"export", "POST /api/dynamics/export"},
                {"tune_pid", "POST /api/dynamics/tune-pid"},
                {"chart", "POST /api/dynamics/chart"},
                {"to_spreadsheet", "POST /api/dynamics/to-spreadsheet"}
            }},
            {"integration_methods", New JArray([Enum].GetNames(GetType(Interfaces.Enums.Dynamics.IntegrationMethod)))},
            {"event_transitions", New JArray("step", "linear", "log", "inverse_log", "random")},
            {"dynamics_specs", New JArray("pressure", "flow")},
            {"controller_types", New JArray("PIDController", "PythonController", "MPCController")},
            {"tuning_objectives", New JArray([Enum].GetNames(GetType(FAPI.Dynamics.TuningObjective)))},
            {"valve_calc_modes", New JArray([Enum].GetNames(GetType(UnitOperations.UnitOperations.Valve.CalculationMode)))},
            {"notes", New JArray(
                "A dynamic run needs the flowsheet solved at steady state first.",
                "Nothing is recorded unless it is a monitored variable.",
                "The pressure-flow network needs both kinds of spec: feeds by flow, boundaries by pressure.",
                "Property ids are not guessable; read them from /api/dynamics/properties.",
                "Only one integration runs at a time; a second request returns 409 integrator_busy.")},
            {"series_budget", New JObject From {
                {"default_max_points", 40},
                {"hard_cap", 400},
                {"full_series_via", "POST /api/dynamics/export"}
            }}
        }

        Dim codes As New JObject()
        For Each entry In FAPI.Dynamics.DiagnosticCodes.All
            codes(entry.Key) = entry.Value
        Next
        o("diagnostic_codes") = codes

        Return o.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    ''' <summary>Catalog of supported (role → variant list) pairs. Variants map 1:1 to Fluent builder method names.</summary>
    Public Shared ReadOnly RoleVariants As New Dictionary(Of String, List(Of String)) From {
        {"heater", New List(Of String)({"Heater"})},
        {"cooler", New List(Of String)({"Cooler"})},
        {"pump", New List(Of String)({"Pump"})},
        {"compressor", New List(Of String)({"Compressor"})},
        {"expander", New List(Of String)({"Expander"})},
        {"valve", New List(Of String)({"Valve"})},
        {"mixer", New List(Of String)({"Mixer"})},
        {"splitter", New List(Of String)({"Splitter"})},
        {"heat_exchanger", New List(Of String)({"HeatExchanger"})},
        {"flash", New List(Of String)({"Vessel"})},
        {"separation", New List(Of String)({"ShortcutColumn", "ComponentSeparator", "Vessel"})},
        {"reactor", New List(Of String)({"ConversionReactor", "EquilibriumReactor", "GibbsReactor", "CSTR", "PFR"})}
    }

    ' ── Sweep ──────────────────────────────────────────────────────────────────

    ''' <summary>Builds candidate flowsheets, solves and scores each, returns ranked results.</summary>
    Public Shared Function Sweep(intent As DesignIntent) As List(Of CandidateResult)

        Dim results As New List(Of CandidateResult)
        If intent Is Nothing OrElse intent.Compounds.Count = 0 Then Return results

        Dim pps = If(intent.AllowedPropertyPackages IsNot Nothing AndAlso intent.AllowedPropertyPackages.Count > 0,
                     intent.AllowedPropertyPackages, New List(Of String) From {PropertyPackages.NRTL})

        ' Build cartesian product of (PP) × (variant per role)
        Dim combos = CartesianProduct(intent.Roles)
        Dim limit = Math.Min(intent.MaxCandidates, pps.Count * Math.Max(combos.Count, 1))

        Dim idx As Integer = 0
        For Each pp In pps
            For Each combo In combos
                If idx >= limit Then Exit For
                Dim cand = BuildAndScore(intent, pp, combo, idx)
                results.Add(cand)
                idx += 1
            Next
            If idx >= limit Then Exit For
        Next

        ' Rank: converged first, then by Score desc
        results.Sort(Function(a, b)
                         If a.Converged AndAlso Not b.Converged Then Return -1
                         If b.Converged AndAlso Not a.Converged Then Return 1
                         Return b.Score.CompareTo(a.Score)
                     End Function)

        Return results
    End Function

    Private Shared Function CartesianProduct(roles As List(Of RoleSpec)) As List(Of List(Of String))
        Dim result As New List(Of List(Of String)) From {New List(Of String)()}
        If roles Is Nothing Then Return result
        For Each r In roles
            Dim variants = If(r.Variants IsNot Nothing AndAlso r.Variants.Count > 0,
                              r.Variants,
                              If(RoleVariants.ContainsKey(r.Role), RoleVariants(r.Role), New List(Of String) From {r.Role}))
            Dim next1 As New List(Of List(Of String))
            For Each prefix In result
                For Each v In variants
                    Dim ext As New List(Of String)(prefix)
                    ext.Add(v)
                    next1.Add(ext)
                Next
            Next
            result = next1
        Next
        Return result
    End Function

    Private Shared Function BuildAndScore(intent As DesignIntent, pp As String, choices As List(Of String), idx As Integer) As CandidateResult

        Dim cand As New CandidateResult With {
            .Id = "cand_" & idx.ToString(),
            .PropertyPackage = pp,
            .RoleChoices = choices,
            .Intent = intent
        }

        Dim fs As FAPI.Flowsheet
        Try
            fs = FAPI.Flowsheet.Create("sweep_" & cand.Id)
        Catch ex As Exception
            cand.Diagnostics.Add("Headless flowsheet creation failed: " & ex.Message)
            Return cand
        End Try

        Try
            For Each c In intent.Compounds
                Try : fs.WithCompound(c) : Catch ex As Exception
                    cand.Diagnostics.Add("compound '" & c & "' not in DB: " & ex.Message)
                End Try
            Next

            Try
                fs.WithPropertyPackage(pp)
            Catch ex As Exception
                cand.Diagnostics.Add("property package '" & pp & "' rejected: " & ex.Message)
                Return cand
            End Try

            ' Feed stream
            Dim feed = fs.AddMaterialStream("FEED")
            feed.At(intent.FeedTemperatureC.Celsius(), intent.FeedPressureKPa.KiloPascal())
            feed.WithMassFlow(intent.FeedFlowKgPerHour.KgPerHour())
            If intent.FeedComposition IsNot Nothing AndAlso intent.FeedComposition.Count > 0 Then
                feed.WithComposition(Sub(comp)
                                         For Each kv In intent.FeedComposition
                                             comp.Mole(kv.Key, kv.Value)
                                         Next
                                     End Sub)
            End If

            ' Build chain - each role consumes prev outlet, produces a new outlet.
            Dim prevOut As MaterialStreamBuilder = feed
            For i = 0 To intent.Roles.Count - 1
                Dim role = intent.Roles(i)
                Dim vrnt = choices(i)
                Dim tag = "U" & (i + 1).ToString()
                Try
                    prevOut = AddRole(fs, tag, role, vrnt, prevOut, cand)
                Catch ex As Exception
                    cand.Diagnostics.Add("role[" & i & "] " & role.Role & "/" & vrnt & " failed: " & ex.Message)
                    Return cand
                End Try
            Next

            ' Solve (non-throwing)
            Dim errs = fs.TrySolve()
            cand.Converged = (errs Is Nothing OrElse errs.Count = 0)
            If errs IsNot Nothing Then
                For Each ex In errs
                    cand.Diagnostics.Add(ex.Message)
                Next
            End If

            Scorer.PopulateMetrics(fs.Inner, cand)

        Catch ex As Exception
            cand.Diagnostics.Add("unexpected: " & ex.Message)
        End Try

        Return cand
    End Function

    ' ── Role dispatch ──────────────────────────────────────────────────────────
    ' Each handler attaches a unit op consuming `inlet`, configured from role.Params,
    ' and returns its main outlet stream as a MaterialStreamBuilder.

    Private Shared Function AddRole(fs As FAPI.Flowsheet,
                                    tag As String,
                                    role As RoleSpec,
                                    vrnt As String,
                                    inlet As MaterialStreamBuilder,
                                    cand As CandidateResult) As MaterialStreamBuilder

        Dim p = role.Params
        Select Case vrnt
            Case "Heater"
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim h = fs.AddHeater(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                If p.ContainsKey("outlet_temperature_C") Then h.WithOutletTemperature(p("outlet_temperature_C").Celsius())
                If p.ContainsKey("pressure_drop_kPa") Then h.WithPressureDrop(p("pressure_drop_kPa").KiloPascal())
                If p.ContainsKey("efficiency_pct") Then h.WithEfficiencyPercent(p("efficiency_pct"))
                Return h.ConnectNewProduct(tag & "_OUT")

            Case "Cooler"
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim c = fs.AddCooler(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                If p.ContainsKey("outlet_temperature_C") Then c.WithOutletTemperature(p("outlet_temperature_C").Celsius())
                If p.ContainsKey("pressure_drop_kPa") Then c.WithPressureDrop(p("pressure_drop_kPa").KiloPascal())
                If p.ContainsKey("efficiency_pct") Then c.WithEfficiencyPercent(p("efficiency_pct"))
                Return c.ConnectNewProduct(tag & "_OUT")

            Case "Pump"
                Dim ePow = fs.AddEnergyStream(tag & "_E")
                Dim pu = fs.AddPump(tag).ConnectFeed(inlet).ConnectEnergyFeed(ePow)
                If p.ContainsKey("outlet_pressure_kPa") Then pu.WithOutletPressure(p("outlet_pressure_kPa").KiloPascal())
                If p.ContainsKey("efficiency_pct") Then pu.WithEfficiencyPercent(p("efficiency_pct"))
                Return pu.ConnectNewProduct(tag & "_OUT")

            Case "Compressor"
                Dim ePow = fs.AddEnergyStream(tag & "_E")
                Dim co = fs.AddCompressor(tag).ConnectFeed(inlet).ConnectEnergyFeed(ePow)
                If p.ContainsKey("outlet_pressure_kPa") Then co.WithOutletPressure(p("outlet_pressure_kPa").KiloPascal())
                If p.ContainsKey("efficiency_pct") Then co.WithAdiabaticEfficiencyPercent(p("efficiency_pct"))
                Return co.ConnectNewProduct(tag & "_OUT")

            Case "Expander"
                Dim ePow = fs.AddEnergyStream(tag & "_E")
                Dim ex = fs.AddExpander(tag).ConnectFeed(inlet).ConnectEnergyProduct(ePow)
                If p.ContainsKey("outlet_pressure_kPa") Then ex.WithOutletPressure(p("outlet_pressure_kPa").KiloPascal())
                If p.ContainsKey("efficiency_pct") Then ex.WithAdiabaticEfficiencyPercent(p("efficiency_pct"))
                Return ex.ConnectNewProduct(tag & "_OUT")

            Case "Valve"
                Dim v = fs.AddValve(tag).ConnectFeed(inlet)
                If p.ContainsKey("outlet_pressure_kPa") Then v.WithOutletPressure(p("outlet_pressure_kPa").KiloPascal())
                If p.ContainsKey("pressure_drop_kPa") Then v.WithPressureDrop(p("pressure_drop_kPa").KiloPascal())
                Return v.ConnectNewProduct(tag & "_OUT")

            Case "Vessel"
                ' 2-phase flash: vapor + liquid; we route the vapor onward and keep liquid as side product
                Dim ves = fs.AddSeparator(tag).ConnectFeed(inlet)
                Dim vap = ves.ConnectNewProduct(tag & "_VAP", 0)
                ves.ConnectNewProduct(tag & "_LIQ", 1)
                Return vap

            Case "ComponentSeparator"
                Dim sp = fs.AddComponentSeparator(tag).ConnectFeed(inlet)
                Dim outA = sp.ConnectNewProduct(tag & "_A", 0)
                sp.ConnectNewProduct(tag & "_B", 1)
                Return outA

            Case "ShortcutColumn"
                Dim sc = fs.AddShortcutColumn(tag).ConnectFeed(inlet)
                Dim eCond = fs.AddEnergyStream(tag & "_QC")
                Dim eReb = fs.AddEnergyStream(tag & "_QR")
                sc.ConnectEnergyProduct(eCond, 0)
                sc.ConnectEnergyFeed(eReb, 0)
                Dim dist = sc.ConnectNewProduct(tag & "_DIST", 0)
                sc.ConnectNewProduct(tag & "_BOT", 1)
                Return dist

            Case "Mixer"
                ' single-inlet pass-through (mixer with one feed)
                Dim mx = fs.AddMixer(tag).ConnectFeed(inlet, 0)
                Return mx.ConnectNewProduct(tag & "_OUT")

            Case "Splitter"
                Dim sp = fs.AddSplitter(tag).ConnectFeed(inlet)
                Dim a = sp.ConnectNewProduct(tag & "_A", 0)
                sp.ConnectNewProduct(tag & "_B", 1)
                Return a

            Case "HeatExchanger"
                ' For sweep simplicity we treat HX as a hot-side heater proxy: needs a utility stream
                ' which is out-of-scope for linear chain. Fall back to Heater with same params.
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim h = fs.AddHeater(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                If p.ContainsKey("outlet_temperature_C") Then h.WithOutletTemperature(p("outlet_temperature_C").Celsius())
                Return h.ConnectNewProduct(tag & "_OUT")

            Case "ConversionReactor"
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim r = fs.AddConversionReactor(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                Return r.ConnectNewProduct(tag & "_OUT")

            Case "EquilibriumReactor"
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim r = fs.AddEquilibriumReactor(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                Return r.ConnectNewProduct(tag & "_OUT")

            Case "GibbsReactor"
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim r = fs.AddGibbsReactor(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                Return r.ConnectNewProduct(tag & "_OUT")

            Case "CSTR"
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim r = fs.AddCSTR(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                Return r.ConnectNewProduct(tag & "_OUT")

            Case "PFR"
                Dim eDuty = fs.AddEnergyStream(tag & "_E")
                Dim r = fs.AddPFR(tag).ConnectFeed(inlet).ConnectEnergyFeed(eDuty)
                Return r.ConnectNewProduct(tag & "_OUT")

            Case Else
                Throw New NotSupportedException("Unknown variant '" & vrnt & "' for role '" & role.Role & "'.")
        End Select
    End Function

    ' ── Materialise winner on live flowsheet ───────────────────────────────────

    ''' <summary>Replays a candidate's build steps on the live flowsheet (clears it first).</summary>
    Public Shared Sub MaterialiseToLive(live As IFlowsheet, winner As CandidateResult)
        If winner Is Nothing OrElse winner.Intent Is Nothing Then
            Throw New ArgumentException("Candidate has no replay intent.")
        End If

        ' Clear existing objects (mirrors /api/clear-flowsheet behavior)
        Try : live.Reset() : Catch : End Try
        Try : live.CloseOpenEditForms() : Catch : End Try

        Dim fs = FAPI.Flowsheet.Wrap(live)
        For Each c In winner.Intent.Compounds
            Try : fs.WithCompound(c) : Catch : End Try
        Next
        fs.WithPropertyPackage(winner.PropertyPackage)

        Dim feed = fs.AddMaterialStream("FEED")
        feed.At(winner.Intent.FeedTemperatureC.Celsius(), winner.Intent.FeedPressureKPa.KiloPascal())
        feed.WithMassFlow(winner.Intent.FeedFlowKgPerHour.KgPerHour())
        If winner.Intent.FeedComposition IsNot Nothing AndAlso winner.Intent.FeedComposition.Count > 0 Then
            feed.WithComposition(Sub(comp)
                                     For Each kv In winner.Intent.FeedComposition
                                         comp.Mole(kv.Key, kv.Value)
                                     Next
                                 End Sub)
        End If

        Dim prev As MaterialStreamBuilder = feed
        For i = 0 To winner.Intent.Roles.Count - 1
            prev = AddRole(fs, "U" & (i + 1).ToString(), winner.Intent.Roles(i), winner.RoleChoices(i), prev, winner)
        Next

        Try : live.AutoLayout() : Catch : End Try
    End Sub

    ' ── JSON helpers ───────────────────────────────────────────────────────────

    Private Shared Function EscJ2(s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "").Replace(vbLf, "\n")
    End Function

    ''' <summary>Serialises a candidate (or list) to JSON for the HTTP response.</summary>
    Public Shared Function CandidateJson(c As CandidateResult) As String
        Dim sb As New StringBuilder()
        sb.Append("{")
        sb.AppendFormat("""id"":""{0}"",", EscJ2(c.Id))
        sb.AppendFormat("""property_package"":""{0}"",", EscJ2(c.PropertyPackage))
        sb.Append("""role_choices"":[")
        For i = 0 To c.RoleChoices.Count - 1
            If i > 0 Then sb.Append(",")
            sb.Append("""").Append(EscJ2(c.RoleChoices(i))).Append("""")
        Next
        sb.Append("],")
        sb.AppendFormat("""converged"":{0},", If(c.Converged, "true", "false"))
        sb.AppendFormat("""score"":{0},", NumStr(c.Score))
        sb.AppendFormat("""total_heating_duty_kW"":{0},", NumStr(c.TotalHeatingDutyKW))
        sb.AppendFormat("""total_cooling_duty_kW"":{0},", NumStr(c.TotalCoolingDutyKW))
        sb.AppendFormat("""total_power_kW"":{0},", NumStr(c.TotalPowerKW))

        sb.Append("""score_breakdown"":{")
        Dim bf As Boolean = True
        For Each kv In c.ScoreBreakdown
            If Not bf Then sb.Append(",")
            sb.AppendFormat("""{0}"":{1}", EscJ2(kv.Key), NumStr(kv.Value))
            bf = False
        Next
        sb.Append("},")

        sb.Append("""diagnostics"":[")
        For i = 0 To c.Diagnostics.Count - 1
            If i > 0 Then sb.Append(",")
            sb.Append("""").Append(EscJ2(c.Diagnostics(i))).Append("""")
        Next
        sb.Append("],")

        sb.Append("""equipment_inventory"":[")
        For i = 0 To c.EquipmentInventory.Count - 1
            If i > 0 Then sb.Append(",")
            sb.Append(DictJson(c.EquipmentInventory(i)))
        Next
        sb.Append("],")

        sb.Append("""product_streams"":{")
        Dim psf As Boolean = True
        For Each kv In c.ProductStreams
            If Not psf Then sb.Append(",")
            sb.AppendFormat("""{0}"":{1}", EscJ2(kv.Key), DictJson(kv.Value))
            psf = False
        Next
        sb.Append("}")

        sb.Append("}")
        Return sb.ToString()
    End Function

    Private Shared Function DictJson(d As Dictionary(Of String, Object)) As String
        Dim sb As New StringBuilder("{")
        Dim f As Boolean = True
        For Each kv In d
            If Not f Then sb.Append(",")
            sb.AppendFormat("""{0}"":", EscJ2(kv.Key))
            If kv.Value Is Nothing Then
                sb.Append("null")
            ElseIf TypeOf kv.Value Is String Then
                sb.AppendFormat("""{0}""", EscJ2(CStr(kv.Value)))
            ElseIf TypeOf kv.Value Is Boolean Then
                sb.Append(If(CBool(kv.Value), "true", "false"))
            Else
                sb.Append(NumStr(kv.Value))
            End If
            f = False
        Next
        sb.Append("}")
        Return sb.ToString()
    End Function

    ' ── JSON parsing for DesignIntent ──────────────────────────────────────────
    ' The assistant ships JSON of the form:
    ' {
    '   "compounds": ["Ethanol","Water"],
    '   "feed": {"temperature_C": 25, "pressure_kPa": 101.325, "flow_kgph": 1000,
    '            "composition_molar": {"Ethanol": 0.5, "Water": 0.5}},
    '   "objective": "...",
    '   "scoring_criteria": "...",
    '   "max_candidates": 6,
    '   "allowed_property_packages": ["NRTL","UNIQUAC"],
    '   "roles": [
    '      {"role":"separation","variants":["ShortcutColumn","ComponentSeparator"],
    '       "params":{"outlet_temperature_C": 80}}
    '   ]
    ' }

    Public Shared Function ParseIntent(json As String) As DesignIntent
        Dim intent As New DesignIntent()
        If String.IsNullOrEmpty(json) Then Return intent

        ' compounds
        For Each c In ExtractStringArray(json, "compounds")
            intent.Compounds.Add(c)
        Next

        ' allowed property packages
        For Each pp In ExtractStringArray(json, "allowed_property_packages")
            intent.AllowedPropertyPackages.Add(pp)
        Next

        ' top-level scalars
        intent.Objective = ExtractString(json, "objective")
        intent.ScoringCriteria = ExtractString(json, "scoring_criteria")
        Dim mc = ExtractNumber(json, "max_candidates")
        If mc.HasValue Then intent.MaxCandidates = CInt(mc.Value)

        ' feed object
        Dim feedObj = ExtractObject(json, "feed")
        If feedObj.Length > 0 Then
            Dim t = ExtractNumber(feedObj, "temperature_C") : If t.HasValue Then intent.FeedTemperatureC = t.Value
            Dim pr = ExtractNumber(feedObj, "pressure_kPa") : If pr.HasValue Then intent.FeedPressureKPa = pr.Value
            Dim fl = ExtractNumber(feedObj, "flow_kgph") : If fl.HasValue Then intent.FeedFlowKgPerHour = fl.Value
            Dim comp = ExtractObject(feedObj, "composition_molar")
            If comp.Length > 0 Then
                For Each kv In ExtractNumberMap(comp)
                    intent.FeedComposition(kv.Key) = kv.Value
                Next
            End If
        End If

        ' roles array
        For Each roleObj In ExtractObjectArray(json, "roles")
            Dim r As New RoleSpec()
            r.Role = ExtractString(roleObj, "role")
            For Each v In ExtractStringArray(roleObj, "variants")
                r.Variants.Add(v)
            Next
            Dim pObj = ExtractObject(roleObj, "params")
            If pObj.Length > 0 Then
                For Each kv In ExtractNumberMap(pObj)
                    r.Params(kv.Key) = kv.Value
                Next
            End If
            intent.Roles.Add(r)
        Next

        Return intent
    End Function

    Private Shared Function ExtractString(json As String, key As String) As String
        Dim m = Regex.Match(json, """" & Regex.Escape(key) & """\s*:\s*""((?:[^""\\]|\\.)*)""")
        If m.Success Then Return m.Groups(1).Value
        Return ""
    End Function

    Private Shared Function ExtractNumber(json As String, key As String) As Double?
        Dim m = Regex.Match(json, """" & Regex.Escape(key) & """\s*:\s*(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)")
        If m.Success Then
            Dim d As Double
            If Double.TryParse(m.Groups(1).Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, d) Then Return d
        End If
        Return Nothing
    End Function

    Private Shared Function ExtractStringArray(json As String, key As String) As List(Of String)
        Dim out As New List(Of String)
        Dim m = Regex.Match(json, """" & Regex.Escape(key) & """\s*:\s*\[([^\]]*)\]")
        If Not m.Success Then Return out
        For Each im As Match In Regex.Matches(m.Groups(1).Value, """((?:[^""\\]|\\.)*)""")
            out.Add(im.Groups(1).Value)
        Next
        Return out
    End Function

    Private Shared Function ExtractObject(json As String, key As String) As String
        Dim p = Regex.Match(json, """" & Regex.Escape(key) & """\s*:\s*\{")
        If Not p.Success Then Return ""
        Dim startIdx = p.Index + p.Length - 1
        Dim depth = 0
        For i = startIdx To json.Length - 1
            If json(i) = "{"c Then depth += 1
            If json(i) = "}"c Then depth -= 1
            If depth = 0 Then Return json.Substring(startIdx, i - startIdx + 1)
        Next
        Return ""
    End Function

    Private Shared Function ExtractObjectArray(json As String, key As String) As List(Of String)
        Dim out As New List(Of String)
        Dim p = Regex.Match(json, """" & Regex.Escape(key) & """\s*:\s*\[")
        If Not p.Success Then Return out
        Dim i As Integer = p.Index + p.Length
        Dim depth As Integer = 0
        While i < json.Length
            Dim ch = json(i)
            If ch = "]"c AndAlso depth = 0 Then Exit While
            If ch = "{"c Then
                Dim s = i, d = 0
                Do
                    If json(i) = "{"c Then d += 1
                    If json(i) = "}"c Then d -= 1
                    i += 1
                Loop While i < json.Length AndAlso d > 0
                out.Add(json.Substring(s, i - s))
                Continue While
            End If
            i += 1
        End While
        Return out
    End Function

    Private Shared Function ExtractNumberMap(jsonObj As String) As Dictionary(Of String, Double)
        Dim out As New Dictionary(Of String, Double)
        For Each m As Match In Regex.Matches(jsonObj, """((?:[^""\\]|\\.)*)""\s*:\s*(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)")
            Dim d As Double
            If Double.TryParse(m.Groups(2).Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, d) Then
                out(m.Groups(1).Value) = d
            End If
        Next
        Return out
    End Function

    Private Shared Function NumStr(v As Object) As String
        If v Is Nothing Then Return "null"
        Try
            Dim d = Convert.ToDouble(v)
            If Double.IsNaN(d) OrElse Double.IsInfinity(d) Then Return "null"
            Return d.ToString("G", Globalization.CultureInfo.InvariantCulture)
        Catch
            Return "null"
        End Try
    End Function

End Class

''' <summary>
''' Computes score, equipment inventory and totals for a solved (or partially solved)
''' flowsheet. Mirrors the metric collection done by <c>/api/solve-and-score</c>.
''' </summary>
Public Class Scorer

    Public Shared Sub PopulateMetrics(flowsheet As IFlowsheet, cand As FluentSweep.CandidateResult)

        ' --- Equipment inventory + duties ------------------------------------------------
        Dim totalHeat As Double = 0, totalCool As Double = 0, totalPower As Double = 0
        Dim solvedUOs As Integer = 0, totalUOs As Integer = 0

        For Each entry In flowsheet.SimulationObjects
            Dim obj = entry.Value
            Dim otype As String = ""
            Try : otype = obj.GraphicObject.ObjectType.ToString() : Catch : End Try

            If otype = "EnergyStream" Then
                Try
                    Dim ef As Double = Convert.ToDouble(obj.GetPropertyValue("PROP_ES_0"))
                    If ef > 0 Then totalHeat += ef Else totalCool += Math.Abs(ef)
                Catch : End Try
                Continue For
            End If
            If otype = "MaterialStream" Then Continue For

            totalUOs += 1
            Dim errMsg As String = ""
            Try : errMsg = obj.ErrorMessage : Catch : End Try
            If String.IsNullOrEmpty(errMsg) Then solvedUOs += 1

            Dim eq As New Dictionary(Of String, Object)
            Dim tag As String = ""
            Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
            eq("name") = tag
            eq("type") = otype
            If Not String.IsNullOrEmpty(errMsg) Then eq("error") = errMsg

            ' Type-specific sizing readouts (best-effort, mirrors /api/solve-and-score)
            Try
                If otype.Contains("Pump") Then
                    Dim pw = DirectCast(obj, Pump).DeltaQ.GetValueOrDefault()
                    eq("power_kW") = Math.Abs(pw) : totalPower += Math.Abs(pw)
                ElseIf otype.Contains("Compressor") Then
                    Dim pw = DirectCast(obj, Compressor).DeltaQ
                    eq("power_kW") = Math.Abs(pw) : totalPower += Math.Abs(pw)
                ElseIf otype.Contains("Expander") Then
                    Dim pw = DirectCast(obj, Expander).DeltaQ
                    eq("power_kW") = Math.Abs(pw)
                ElseIf otype.Contains("Heater") Then
                    Dim duty = DirectCast(obj, Heater).HeatDuty
                    eq("duty_kW") = Math.Abs(duty)
                ElseIf otype.Contains("Cooler") Then
                    Dim duty = DirectCast(obj, Cooler).HeatDuty
                    eq("duty_kW") = Math.Abs(duty)
                ElseIf otype.Contains("HeatExchanger") Then
                    Dim area = DirectCast(obj, HeatExchanger).Area.GetValueOrDefault()
                    eq("area_m2") = area
                ElseIf otype.Contains("ShortcutColumn") Then
                    Dim ns = DirectCast(obj, ShortcutColumn).m_N
                    eq("stages") = Convert.ToInt32(ns)
                End If
            Catch : End Try

            cand.EquipmentInventory.Add(eq)
        Next

        cand.TotalHeatingDutyKW = totalHeat
        cand.TotalCoolingDutyKW = totalCool
        cand.TotalPowerKW = totalPower

        ' --- Product streams (terminal material streams) -----------------------------
        For Each entry In flowsheet.SimulationObjects
            Dim obj = entry.Value
            Dim otype As String = ""
            Try : otype = obj.GraphicObject.ObjectType.ToString() : Catch : End Try
            If otype <> "MaterialStream" Then Continue For

            Dim hasOut As Boolean = False
            Try
                For Each conn In obj.GraphicObject.OutputConnectors
                    If conn.IsAttached Then hasOut = True : Exit For
                Next
            Catch : End Try
            If hasOut Then Continue For

            Dim tag As String = ""
            Try : tag = obj.GraphicObject.Tag : Catch : tag = obj.Name : End Try
            Dim psum As New Dictionary(Of String, Object)
            Try
                psum("temperature_C") = Convert.ToDouble(obj.GetPropertyValue("PROP_MS_0")) - 273.15
                psum("pressure_kPa") = Convert.ToDouble(obj.GetPropertyValue("PROP_MS_1")) / 1000.0
                psum("flow_kgph") = Convert.ToDouble(obj.GetPropertyValue("PROP_MS_2")) * 3600.0
            Catch : End Try
            cand.ProductStreams(tag) = psum
        Next

        ' --- Score ---------------------------------------------------------------------
        Dim convFrac As Double = If(totalUOs = 0, 0.0, CDbl(solvedUOs) / CDbl(totalUOs))
        Dim score As Double = If(cand.Converged, 0.5, 0.0) + 0.5 * convFrac
        cand.ScoreBreakdown("convergence") = If(cand.Converged, 1.0, 0.0)
        cand.ScoreBreakdown("solved_unit_fraction") = convFrac
        cand.Score = score
    End Sub

End Class
