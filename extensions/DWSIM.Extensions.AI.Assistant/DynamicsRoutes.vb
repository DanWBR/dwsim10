'    DWSIM Assistant — dynamic simulation endpoints
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

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Threading
Imports System.Threading.Tasks
Imports DWSIM.Automation.DynamicRunner
Imports DWSIM.Automation.FluentAPI.Dynamics
Imports DWSIM.Interfaces
Imports Newtonsoft.Json.Linq
Imports FAPI = DWSIM.Automation.FluentAPI
Imports DynEnums = DWSIM.Interfaces.Enums.Dynamics

''' <summary>
''' Dynamic simulation over the assistant's HTTP API, on the flowsheet the user has open.
''' </summary>
''' <remarks>
''' <para>
''' The routes mirror the MCP server's dynamics tools one for one, so whoever writes the assistant's
''' prompt only has to learn one model. All the actual work happens in DWSIM.FluentAPI's dynamics
''' layer; this module is transport.
''' </para>
''' <para>
''' A run does not go on the UI thread. Integrations last minutes, and holding the HTTP connection
''' or the UI thread for that long would time the client out and freeze the window. Instead a run
''' starts on a worker, returns a run_id, and the caller polls. The UI thread is used only to
''' refresh the canvas between steps, throttled, and to create graphic objects.
''' </para>
''' </remarks>
Public Module DynamicsRoutes

    Private Const MaxListItems As Integer = 25
    Private Const DefaultPreviewPoints As Integer = 40
    Private Const MaxPreviewPoints As Integer = 400
    Private Const MaxRuns As Integer = 4

    Private ReadOnly Runs As New ConcurrentDictionary(Of String, DynamicsRun)

    ''' <summary>The status code and JSON body a route produced.</summary>
    Public Class RouteResult
        ''' <summary>HTTP status to send back; 200 unless a route says otherwise.</summary>
        Public Property StatusCode As Integer = 200
        ''' <summary>The JSON body.</summary>
        Public Property Body As String = ""
    End Class

    ''' <summary>One run in flight, or recently finished.</summary>
    Private Class DynamicsRun
        Public Property Id As String = ""
        Public Property Kind As String = "run"
        Public Property State As String = "pending"
        Public Property Steps As Integer = 0
        Public Property CurrentSeconds As Double = 0.0
        Public Property TotalSeconds As Double = 0.0
        Public Property StartedAt As Date = Date.UtcNow
        Public Property FinishedAt As Date? = Nothing
        Public Property Result As FAPI.DynamicsResult = Nothing
        Public Property Tuning As PidTuningResult = Nothing
        Public Property ErrorMessage As String = ""
        Public Property Abort As Boolean = False
        Public Property Log As New List(Of String)

        Public ReadOnly Property IsFinished As Boolean
            Get
                Return State = "completed" OrElse State = "aborted" OrElse State = "failed"
            End Get
        End Property

        Public ReadOnly Property Elapsed As TimeSpan
            Get
                Return If(FinishedAt.HasValue, FinishedAt.Value, Date.UtcNow) - StartedAt
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Handles any request under <c>/api/dynamics/</c>. Returns the JSON body to send back, and
    ''' sets the status code on <paramref name="outcome"/> when the request could not be satisfied.
    ''' </summary>
    ''' <param name="fs">The live flowsheet.</param>
    ''' <param name="method">HTTP method.</param>
    ''' <param name="path">Request path, already trimmed of its trailing slash.</param>
    ''' <param name="body">Request body, empty for GET.</param>
    ''' <param name="outcome">The response, for setting a status code.</param>
    ''' <param name="refreshCanvas">Invoked after each integration step so the host can redraw.</param>
    Public Function Handle(fs As IFlowsheet, method As String, path As String, body As String,
                           Optional refreshCanvas As Action = Nothing) As RouteResult

        Dim outcome As New RouteResult()
        outcome.Body = HandleCore(fs, method, path, body, outcome, refreshCanvas)
        Return outcome

    End Function

    ''' <summary>Routes one request, returning the JSON body and recording the status on the outcome.</summary>
    Private Function HandleCore(fs As IFlowsheet, method As String, path As String, body As String,
                                outcome As RouteResult, refreshCanvas As Action) As String

        Dim segments = path.Substring("/api/dynamics/".Length).Split("/"c)
        Dim route = segments(0).ToLowerInvariant()
        Dim argument = If(segments.Length > 1, segments(1), "")

        Dim payload As JObject = Nothing
        If Not String.IsNullOrWhiteSpace(body) Then
            Try
                payload = JObject.Parse(body)
            Catch ex As Exception
                outcome.StatusCode = 400
                Return Fail("invalid_json", ex.Message)
            End Try
        End If
        If payload Is Nothing Then payload = New JObject()

        Try
            Select Case route

                Case "inspect" : Return Inspect(fs, payload)
                Case "properties" : Return Properties(fs, payload)
                Case "check" : Return Check(fs, payload)
                Case "setup" : Return Setup(fs, payload)
                Case "monitor" : Return Monitor(fs, payload)
                Case "event" : Return [Event](fs, payload)
                Case "object" : Return ConfigureObject(fs, payload)
                Case "controller" : Return Controller(fs, payload)
                Case "state" : Return State(fs, payload)
                Case "run" : Return StartRun(fs, payload, refreshCanvas, outcome)
                Case "tune-pid" : Return StartTuning(fs, payload, outcome)
                Case "status" : Return Status(argument, outcome)
                Case "abort" : Return AbortRun(argument, outcome)
                Case "series" : Return Series(argument, payload, outcome)
                Case "analyze" : Return Analyze(argument, payload, outcome)
                Case "diagnose" : Return Diagnose(fs, argument, outcome)
                Case "export" : Return Export(payload, outcome)
                Case "chart" : Return Chart(fs, payload, outcome)
                Case "to-spreadsheet" : Return ToSpreadsheet(fs, payload, outcome)
                Case "runs" : Return ListRuns()

                Case Else
                    outcome.StatusCode = 404
                    Return Fail("unknown_route", "No dynamics route named '" & route & "'.")

            End Select

        Catch ex As Exception
            outcome.StatusCode = 400
            Return Fail("error", BaseMessage(ex))
        End Try

    End Function

    ' ----------------------------------------------------------------- Discovery

    Private Function Inspect(fs As IFlowsheet, payload As JObject) As String

        Dim inventory = DynamicsIntrospection.Inspect(fs)
        Dim detail = Str(payload, "detail", "summary").ToLowerInvariant()

        Dim result As New JObject From {
            {"success", True},
            {"dynamic_mode", inventory.DynamicModeEnabled},
            {"object_count", inventory.Objects.Count},
            {"dynamic_capable_count", inventory.DynamicCapableObjects.Count()},
            {"controller_count", inventory.Controllers.Count},
            {"current_schedule", inventory.CurrentSchedule},
            {"schedules", Arr(inventory.Schedules)},
            {"integrators", Arr(inventory.Integrators)},
            {"event_sets", Arr(inventory.EventSets)},
            {"cause_and_effect_matrices", Arr(inventory.CauseAndEffectMatrices)},
            {"stored_states", Arr(inventory.StoredStates)}
        }

        If detail = "objects" OrElse detail = "full" Then
            Dim objects As New JArray()
            For Each o In inventory.Objects.Take(MaxListItems)
                objects.Add(New JObject From {
                    {"tag", o.Tag},
                    {"type", o.Type},
                    {"supports_dynamics", o.SupportsDynamics},
                    {"dynamics_spec", o.DynamicsSpec.ToString()},
                    {"dynamic_properties", Arr(o.DynamicProperties.Select(Function(p) p.Id).ToList())}
                })
            Next
            result("objects") = objects
            If inventory.Objects.Count > MaxListItems Then result("objects_truncated") = True
        End If

        If detail = "controllers" OrElse detail = "full" Then
            result("controllers") = New JArray(inventory.Controllers.Take(MaxListItems).
                                               Select(Function(c) DirectCast(Describe(c), Object)).ToArray())
        End If

        Return result.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Properties(fs As IFlowsheet, payload As JObject) As String

        Dim tag = Str(payload, "object", Str(payload, "tag", ""))
        If tag = "" Then Throw New ArgumentException("Pass the object's tag.")

        Dim filter = Str(payload, "filter", "")
        Dim all = DynamicsIntrospection.AddressableProperties(fs, tag)

        Dim matching = all.Where(Function(p) filter = "" OrElse
                                     p.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                     p.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList()

        Dim items As New JArray()
        For Each p In matching.Take(MaxListItems)
            items.Add(New JObject From {
                {"id", p.Id},
                {"description", p.Description},
                {"units", p.Units},
                {"value", If(p.Value Is Nothing, Nothing, SeriesDecimator.Format(ToDouble(p.Value)))},
                {"dynamic", p.IsDynamic}
            })
        Next

        Return New JObject From {
            {"success", True},
            {"object", tag},
            {"properties", items},
            {"total", matching.Count}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Check(fs As IFlowsheet, payload As JObject) As String

        Dim scheduleName = Str(payload, "schedule", Nothing)
        Dim findings = DynamicsDiagnostics.CheckReady(fs, scheduleName)
        Dim blockers = findings.Where(Function(f) f.Severity = DiagnosticSeverity.Blocker).ToList()

        Dim result As New JObject From {
            {"success", True},
            {"ready", blockers.Count = 0},
            {"blockers", FindingsJson(blockers)},
            {"warnings", FindingsJson(findings.Where(Function(f) f.Severity = DiagnosticSeverity.Warning))},
            {"notes", FindingsJson(findings.Where(Function(f) f.Severity = DiagnosticSeverity.Info))}
        }

        Try
            Dim schedule = IntegratorRunner.ResolveSchedule(fs, scheduleName)
            Dim integrator = fs.DynamicsManager.IntegratorList(schedule.CurrentIntegrator)
            result("schedule") = schedule.Description
            result("integrator") = integrator.Description
            result("step_s") = integrator.IntegrationStep.TotalSeconds
            result("duration_s") = integrator.Duration.TotalSeconds
            result("estimated_steps") = CInt(integrator.Duration.TotalSeconds / integrator.IntegrationStep.TotalSeconds)
            result("monitored_variables") = Arr(integrator.MonitoredVariables.Select(Function(v) v.Description).ToList())
        Catch ex As Exception
            ' The findings already explain why the schedule could not be resolved.
        End Try

        Return result.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    ' ------------------------------------------------------------- Configuration

    Private Function Setup(fs As IFlowsheet, payload As JObject) As String

        Dim fluent = FAPI.Flowsheet.Wrap(fs)

        Dim scheduleName = Str(payload, "schedule", "")
        If scheduleName = "" Then Throw New ArgumentException("Pass a schedule name.")

        Dim integratorName = Str(payload, "integrator", scheduleName)
        Dim stepSeconds = Num(payload, "step_s", 1.0)
        Dim durationSeconds = Num(payload, "duration_s", 600.0)

        Dim integrator = fluent.Dynamics.DefineIntegrator(integratorName).
            WithIntegrationStep(TimeSpan.FromSeconds(stepSeconds)).
            WithDuration(TimeSpan.FromSeconds(durationSeconds)).
            WithCalculationRates(CInt(Num(payload, "rate_equilibrium", 1)),
                                 CInt(Num(payload, "rate_pressure_flow", 1)),
                                 CInt(Num(payload, "rate_control", 1)))

        Dim method = Str(payload, "method", "")
        If method <> "" Then
            Dim parsed As DynEnums.IntegrationMethod
            If Not [Enum].TryParse(method, True, parsed) Then
                Throw New ArgumentException("Unknown integration method '" & method & "'. Use one of: " &
                                            String.Join(", ", [Enum].GetNames(GetType(DynEnums.IntegrationMethod))) & ".")
            End If
            integrator.WithMethod(parsed)
            If parsed = DynEnums.IntegrationMethod.AdaptiveRK45 Then integrator.WithAdaptiveStep(True)
        End If

        Dim tolerance = Num(payload, "error_tolerance", 0.0)
        If tolerance > 0 Then integrator.WithErrorTolerance(tolerance)

        Dim schedule = fluent.Dynamics.DefineSchedule(scheduleName).WithIntegrator(integratorName)
        If Bool(payload, "make_current", True) Then schedule.MakeCurrent()
        If Bool(payload, "enable_dynamic_mode", True) Then fluent.Dynamics.EnableDynamicMode()

        Return New JObject From {
            {"success", True},
            {"schedule", scheduleName},
            {"integrator", integratorName},
            {"step_s", stepSeconds},
            {"duration_s", durationSeconds},
            {"estimated_steps", CInt(durationSeconds / stepSeconds)},
            {"monitored_variables", Arr(integrator.MonitoredVariableNames)}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Monitor(fs As IFlowsheet, payload As JObject) As String

        Dim fluent = FAPI.Flowsheet.Wrap(fs)
        Dim integratorName = Str(payload, "integrator", "")

        If integratorName = "" Then
            Dim schedule = IntegratorRunner.ResolveSchedule(fs, Nothing)
            If Not fs.DynamicsManager.IntegratorList.ContainsKey(schedule.CurrentIntegrator) Then
                Throw New InvalidOperationException("Schedule '" & schedule.Description &
                                                    "' has no integrator. Call /api/dynamics/setup first.")
            End If
            integratorName = fs.DynamicsManager.IntegratorList(schedule.CurrentIntegrator).Description
        End If

        Dim builder = fluent.Dynamics.Integrator(integratorName)
        Dim action = Str(payload, "action", "list").ToLowerInvariant()

        Select Case action

            Case "clear"
                builder.ClearMonitoredVariables()

            Case "set", "add"
                Dim variables = payload("variables")
                If variables Is Nothing OrElse Not variables.Any() Then
                    Throw New ArgumentException("Pass at least one variable as ""TAG.PropertyId"".")
                End If
                If action = "set" Then builder.ClearMonitoredVariables()
                For Each item In variables
                    Dim spec = item.ToString()
                    Dim split = spec.LastIndexOf("."c)
                    If split <= 0 Then Throw New ArgumentException("'" & spec & "' is not in the form ""TAG.PropertyId"".")
                    builder.Monitor(spec.Substring(0, split), spec.Substring(split + 1))
                Next

            Case "list"

            Case Else
                Throw New ArgumentException("Unknown action '" & action & "'. Use set, add, list or clear.")

        End Select

        Return New JObject From {
            {"success", True},
            {"integrator", builder.Name},
            {"monitored_variables", Arr(builder.MonitoredVariableNames)}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function [Event](fs As IFlowsheet, payload As JObject) As String

        Dim fluent = FAPI.Flowsheet.Wrap(fs)

        Dim scheduleName = Str(payload, "schedule", "")
        If scheduleName = "" Then scheduleName = IntegratorRunner.ResolveSchedule(fs, Nothing).Description

        Dim setName = Str(payload, "event_set", "")
        If setName = "" Then setName = scheduleName & " events"

        Dim eventSet = fluent.Dynamics.DefineEventSet(setName)
        Dim action = Str(payload, "action", "list").ToLowerInvariant()

        Select Case action

            Case "add"
                Dim tag = Str(payload, "object", Str(payload, "tag", ""))
                Dim propertyId = Str(payload, "property", "")
                If tag = "" OrElse propertyId = "" Then Throw New ArgumentException("An event needs an object and a property.")

                Dim value = Num(payload, "value", 0.0)
                Dim units = Str(payload, "units", Nothing)
                Dim atSeconds = Num(payload, "at_s", 0.0)
                Dim description = Str(payload, "description", Nothing)
                Dim transition = Str(payload, "transition", "step").ToLowerInvariant()

                If transition = "step" Then
                    eventSet.AddStepChange(tag, propertyId, value, FAPI.Q.Seconds(atSeconds), units, description)
                Else
                    eventSet.AddEvent(If(description, transition & " " & tag & "." & propertyId)).
                        At(FAPI.Q.Seconds(atSeconds)).
                        ChangeProperty(tag, propertyId, value, units).
                        WithTransition(ParseTransition(transition)).
                        [And]()
                End If

                ' An event set nothing runs is an event set that does nothing.
                fluent.Dynamics.Schedule(scheduleName).WithEventSet(setName)

            Case "remove"
                Dim description = Str(payload, "description", "")
                If description = "" Then Throw New ArgumentException("Pass the description of the event to remove.")
                eventSet.RemoveEvent(description)

            Case "clear"
                eventSet.ClearEvents()

            Case "list"

            Case Else
                Throw New ArgumentException("Unknown action '" & action & "'. Use add, list, remove or clear.")

        End Select

        Return New JObject From {
            {"success", True},
            {"event_set", setName},
            {"schedule", scheduleName},
            {"events", Arr(eventSet.EventDescriptions)}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function ConfigureObject(fs As IFlowsheet, payload As JObject) As String

        Dim tag = Str(payload, "object", Str(payload, "tag", ""))
        If tag = "" Then Throw New ArgumentException("Pass the object's tag.")

        Dim obj = DynamicsIntrospection.Resolve(fs, tag)
        Dim units = fs.FlowsheetOptions.SelectedUnitSystem
        Dim applied As New JArray()

        FAPI.PropertyCatalog.EnsureDynamicProperties(obj)

        Dim spec = Str(payload, "dynamics_spec", "")
        If spec <> "" Then
            Dim parsed As DynEnums.DynamicsSpecType
            If Not [Enum].TryParse(spec, True, parsed) Then
                Throw New ArgumentException("Unknown dynamics spec '" & spec & "'. Use pressure or flow.")
            End If
            obj.DynamicsSpec = parsed
            applied.Add("dynamics_spec = " & parsed.ToString())
        End If

        Dim props = TryCast(payload("properties"), JObject)
        If props IsNot Nothing Then
            For Each entry In props
                If obj.IsDynamicProperty(entry.Key) Then
                    If entry.Value.Type = JTokenType.Boolean Then
                        obj.AddDynamicProperty(entry.Key, entry.Value.Value(Of Boolean)())
                    Else
                        obj.AddDynamicProperty(entry.Key, entry.Value.Value(Of Double)())
                    End If
                    applied.Add(entry.Key & " = " & entry.Value.ToString())
                Else
                    ' A tank's volume and other ordinary properties matter to a dynamic run too.
                    Dim writable = obj.GetProperties(Enums.PropertyType.WR)
                    If writable IsNot Nothing AndAlso writable.Contains(entry.Key) Then
                        obj.SetPropertyValue(entry.Key, entry.Value.Value(Of Double)(), units)
                        applied.Add(entry.Key & " = " & entry.Value.ToString())
                    Else
                        Throw New ArgumentException("'" & tag & "' has no settable property '" & entry.Key & "'.")
                    End If
                End If
            Next
        End If

        Dim valve = TryCast(obj, DWSIM.UnitOperations.UnitOperations.Valve)

        Dim calcMode = Str(payload, "valve_calc_mode", "")
        If calcMode <> "" Then
            If valve Is Nothing Then Throw New ArgumentException("'" & tag & "' is not a valve.")
            Dim parsed As DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode
            If Not [Enum].TryParse(calcMode, True, parsed) Then
                Throw New ArgumentException("Unknown valve calculation mode '" & calcMode & "'.")
            End If
            valve.CalcMode = parsed
            applied.Add("calc_mode = " & parsed.ToString())
        End If

        Dim characteristic = Str(payload, "valve_opening_characteristic", "")
        If characteristic <> "" Then
            If valve Is Nothing Then Throw New ArgumentException("'" & tag & "' is not a valve.")
            Dim parsed As DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType
            If Not [Enum].TryParse(characteristic, True, parsed) Then
                Throw New ArgumentException("Unknown opening characteristic '" & characteristic & "'.")
            End If
            valve.EnableOpeningKvRelationship = True
            valve.DefinedOpeningKvRelationShipType = parsed
            applied.Add("opening_characteristic = " & parsed.ToString())
        End If

        Return New JObject From {
            {"success", True},
            {"object", tag},
            {"applied", applied},
            {"dynamics_spec", obj.DynamicsSpec.ToString()}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Controller(fs As IFlowsheet, payload As JObject) As String

        Dim action = Str(payload, "action", "list").ToLowerInvariant()
        Dim tag = Str(payload, "controller", Str(payload, "tag", ""))

        If action = "set" Then
            If tag = "" Then Throw New ArgumentException("Pass the controller's tag.")

            Dim pid = TryCast(DynamicsIntrospection.Resolve(fs, tag), DWSIM.UnitOperations.SpecialOps.PIDController)
            If pid Is Nothing Then Throw New ArgumentException("'" & tag & "' is not a PID controller.")

            If payload("sp") IsNot Nothing Then pid.SetPoint = payload("sp").Value(Of Double)()
            If payload("kp") IsNot Nothing Then pid.Kp = payload("kp").Value(Of Double)()
            If payload("ki") IsNot Nothing Then pid.Ki = payload("ki").Value(Of Double)()
            If payload("kd") IsNot Nothing Then pid.Kd = payload("kd").Value(Of Double)()
            If payload("out_min") IsNot Nothing Then pid.OutputMin = payload("out_min").Value(Of Double)()
            If payload("out_max") IsNot Nothing Then pid.OutputMax = payload("out_max").Value(Of Double)()
            If payload("reverse_acting") IsNot Nothing Then pid.ReverseActing = payload("reverse_acting").Value(Of Boolean)()
            If payload("active") IsNot Nothing Then pid.Active = payload("active").Value(Of Boolean)()
            If payload("manual_override") IsNot Nothing Then pid.ManualOverride = payload("manual_override").Value(Of Boolean)()
            If payload("execution_order") IsNot Nothing Then pid.ExecutionOrder = payload("execution_order").Value(Of Integer)()

            If pid.OutputMin >= pid.OutputMax Then
                Throw New ArgumentException("The output minimum (" & pid.OutputMin &
                                            ") must be below the maximum (" & pid.OutputMax & ").")
            End If
        End If

        Dim inventory = DynamicsIntrospection.Inspect(fs)
        Dim wanted = If(tag = "", inventory.Controllers,
                        inventory.Controllers.Where(Function(c) String.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase)).ToList())

        Return New JObject From {
            {"success", True},
            {"controllers", New JArray(wanted.Take(MaxListItems).Select(Function(c) DirectCast(Describe(c), Object)).ToArray())}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function State(fs As IFlowsheet, payload As JObject) As String

        Dim fluent = FAPI.Flowsheet.Wrap(fs)
        Dim action = Str(payload, "action", "list").ToLowerInvariant()
        Dim name = Str(payload, "name", "")

        Select Case action

            Case "save"
                RequireName(name)
                fluent.Dynamics.StoreCurrentStateAs(name)

            Case "restore"
                RequireName(name)
                IntegratorRunner.RestoreState(fs, name)

            Case "delete"
                RequireName(name)
                fs.StoredSolutions.Remove(name)

            Case "attach"
                RequireName(name)
                Dim scheduleName = Str(payload, "schedule", "")
                If scheduleName = "" Then scheduleName = IntegratorRunner.ResolveSchedule(fs, Nothing).Description
                fluent.Dynamics.Schedule(scheduleName).WithInitialState(name)

            Case "list"

            Case Else
                Throw New ArgumentException("Unknown action '" & action & "'. Use save, restore, list, delete or attach.")

        End Select

        Return New JObject From {
            {"success", True},
            {"stored_states", Arr(fs.StoredSolutions.Keys.Take(MaxListItems).ToList())},
            {"total", fs.StoredSolutions.Count}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    ' ------------------------------------------------------------------ Running

    Private Function StartRun(fs As IFlowsheet, payload As JObject, refreshCanvas As Action,
                              outcome As RouteResult) As String

        Dim scheduleName = Str(payload, "schedule", Nothing)

        Dim blockers = DynamicsDiagnostics.CheckReady(fs, scheduleName).
            Where(Function(f) f.Severity = DiagnosticSeverity.Blocker).ToList()

        If blockers.Count > 0 Then
            outcome.StatusCode = 400
            Return New JObject From {
                {"success", False},
                {"error", "not_ready"},
                {"blockers", FindingsJson(blockers)}
            }.ToString(Newtonsoft.Json.Formatting.None)
        End If

        ' The integrator panel's Play button and this endpoint drive the same process-wide solver
        ' state. Refusing outright beats silently queueing behind a run the user started by hand.
        If IntegratorRunner.IsRunning Then
            outcome.StatusCode = 409
            Return Fail("integrator_busy",
                        "An integration is already running. Wait for it, or stop it from the integrator panel.")
        End If

        Dim schedule = IntegratorRunner.ResolveSchedule(fs, scheduleName)
        Dim integrator = fs.DynamicsManager.IntegratorList(schedule.CurrentIntegrator)

        Dim durationOverride = Num(payload, "duration_s", 0.0)
        If durationOverride > 0 Then integrator.Duration = TimeSpan.FromSeconds(durationOverride)

        Dim realtime = Bool(payload, "realtime", False)
        Dim maxWall = CInt(Num(payload, "max_wall_time_s", 600))
        Dim maxSteps = CInt(Num(payload, "max_steps", 0))

        Dim run = NewRun("run")
        run.TotalSeconds = integrator.Duration.TotalSeconds

        Dim fluent = FAPI.Flowsheet.Wrap(fs)
        Dim pendingRefresh As Integer = 0

        Task.Run(Sub()
                     run.State = "running"
                     Try
                         Dim builder = fluent.RunDynamics(schedule.ID).
                             WithRealTime(realtime).
                             WithMaxWallTime(TimeSpan.FromSeconds(maxWall)).
                             OnProgress(Sub(p)
                                            run.CurrentSeconds = p.CurrentSeconds
                                            run.Steps = p.Step
                                        End Sub).
                             StopWhen(Function(f, t) run.Abort)

                         If maxSteps > 0 Then builder.WithMaxSteps(maxSteps)

                         ' The integrator steps far faster than the canvas redraws; without the
                         ' throttle the queue of posts starves the UI thread.
                         If refreshCanvas IsNot Nothing Then
                             builder.OnProgress(Sub(p)
                                                    If Interlocked.CompareExchange(pendingRefresh, 1, 0) <> 0 Then Return
                                                    fs.RunCodeOnUIThread(Sub()
                                                                             Try
                                                                                 refreshCanvas()
                                                                             Finally
                                                                                 Interlocked.Exchange(pendingRefresh, 0)
                                                                             End Try
                                                                         End Sub)
                                                End Sub)
                         End If

                         run.Result = builder.Execute()

                         If run.Result.Errors.Count > 0 Then
                             run.State = "failed"
                             run.ErrorMessage = BaseMessage(run.Result.Errors(0))
                         ElseIf run.Result.Aborted Then
                             run.State = "aborted"
                         Else
                             run.State = "completed"
                         End If

                     Catch ex As Exception
                         run.State = "failed"
                         run.ErrorMessage = BaseMessage(ex)
                     Finally
                         run.FinishedAt = Date.UtcNow
                     End Try
                 End Sub)

        If Bool(payload, "wait", False) Then WaitFor(run, maxWall + 30)

        Dim result As New JObject From {
            {"success", True},
            {"run_id", run.Id},
            {"schedule", schedule.Description},
            {"integrator", integrator.Description},
            {"estimated_steps", CInt(integrator.Duration.TotalSeconds / integrator.IntegrationStep.TotalSeconds)},
            {"state", run.State}
        }

        If run.IsFinished AndAlso run.Result IsNot Nothing Then result("summary") = Summarise(run)

        Return result.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function StartTuning(fs As IFlowsheet, payload As JObject, outcome As RouteResult) As String

        If IntegratorRunner.IsRunning Then
            outcome.StatusCode = 409
            Return Fail("integrator_busy", "An integration is already running. Tuning runs the schedule many times.")
        End If

        Dim objectiveName = Str(payload, "objective", "IAE")
        Dim objective As TuningObjective
        If Not [Enum].TryParse(objectiveName, True, objective) Then
            Throw New ArgumentException("Unknown objective '" & objectiveName & "'. Use one of: " &
                                        String.Join(", ", [Enum].GetNames(GetType(TuningObjective))) & ".")
        End If

        Dim controllers As List(Of String) = Nothing
        Dim tags = payload("controllers")
        If tags IsNot Nothing Then controllers = tags.Select(Function(t) t.ToString()).ToList()

        Dim scheduleName = Str(payload, "schedule", Nothing)
        Dim maxEvaluations = CInt(Num(payload, "max_evaluations", 30))
        Dim maxWallPerRun = CInt(Num(payload, "max_wall_time_per_run_s", 120))
        Dim apply = Bool(payload, "apply", True)

        Dim run = NewRun("tune")

        Task.Run(Sub()
                     run.State = "running"
                     Try
                         run.Tuning = PidTuner.Tune(fs, New PidTuningOptions With {
                             .ScheduleName = scheduleName,
                             .ControllerTags = controllers,
                             .Objective = objective,
                             .MaxEvaluations = maxEvaluations,
                             .Apply = apply,
                             .MaxWallTimePerRun = TimeSpan.FromSeconds(maxWallPerRun),
                             .AbortRequested = Function() run.Abort,
                             .OnProgress = Sub(line)
                                               SyncLock run.Log
                                                   run.Log.Add(line)
                                                   While run.Log.Count > 200 : run.Log.RemoveAt(0) : End While
                                               End SyncLock
                                               run.Steps += 1
                                           End Sub
                         })

                         If run.Tuning.Error IsNot Nothing Then
                             run.State = "failed"
                             run.ErrorMessage = BaseMessage(run.Tuning.Error)
                         Else
                             run.State = If(run.Tuning.Aborted, "aborted", "completed")
                         End If

                     Catch ex As Exception
                         run.State = "failed"
                         run.ErrorMessage = BaseMessage(ex)
                     Finally
                         run.FinishedAt = Date.UtcNow
                     End Try
                 End Sub)

        If Bool(payload, "wait", False) Then WaitFor(run, maxEvaluations * maxWallPerRun + 30)

        Dim result As New JObject From {
            {"success", True},
            {"run_id", run.Id},
            {"objective", objective.ToString()},
            {"max_evaluations", maxEvaluations},
            {"state", run.State}
        }

        If run.Tuning IsNot Nothing Then result("tuning") = Describe(run.Tuning)

        Return result.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Status(runId As String, outcome As RouteResult) As String

        Dim run = FindRun(runId, outcome)
        If run Is Nothing Then Return Fail("unknown_run", "No dynamics run with id '" & runId & "'.")

        Dim result As New JObject From {
            {"success", True},
            {"run_id", run.Id},
            {"kind", run.Kind},
            {"state", run.State},
            {"steps", run.Steps},
            {"simulated_s", SeriesDecimator.Format(run.CurrentSeconds)},
            {"elapsed_s", SeriesDecimator.Format(run.Elapsed.TotalSeconds)}
        }

        If run.TotalSeconds > 0 AndAlso run.TotalSeconds < Integer.MaxValue Then
            result("progress") = SeriesDecimator.Format(Math.Min(1.0, run.CurrentSeconds / run.TotalSeconds))
        End If

        If run.ErrorMessage <> "" Then result("error") = run.ErrorMessage
        If run.IsFinished AndAlso run.Result IsNot Nothing Then result("summary") = Summarise(run)
        If run.Tuning IsNot Nothing Then result("tuning") = Describe(run.Tuning)

        If run.Kind = "tune" Then
            SyncLock run.Log
                result("log") = Arr(run.Log.Skip(Math.Max(0, run.Log.Count - 20)).ToList())
            End SyncLock
        End If

        Return result.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function AbortRun(runId As String, outcome As RouteResult) As String

        Dim run = FindRun(runId, outcome)
        If run Is Nothing Then Return Fail("unknown_run", "No dynamics run with id '" & runId & "'.")

        run.Abort = True

        Return New JObject From {
            {"success", True},
            {"run_id", run.Id},
            {"state", run.State},
            {"steps", run.Steps}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function ListRuns() As String

        Dim items As New JArray()
        For Each run In Runs.Values.OrderByDescending(Function(r) r.StartedAt).Take(MaxRuns)
            items.Add(New JObject From {
                {"run_id", run.Id},
                {"kind", run.Kind},
                {"state", run.State},
                {"steps", run.Steps},
                {"elapsed_s", SeriesDecimator.Format(run.Elapsed.TotalSeconds)}
            })
        Next

        Return New JObject From {{"success", True}, {"runs", items}}.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    ' ------------------------------------------------------------------ Results

    Private Function Series(runId As String, payload As JObject, outcome As RouteResult) As String

        Dim failure As String = Nothing
        Dim result = RequireResult(runId, outcome, failure)
        If result Is Nothing Then Return failure

        Dim selected = SelectSeries(result, payload)
        If selected.Count = 0 Then
            Return New JObject From {
                {"success", True},
                {"run_id", runId},
                {"series", New JObject()},
                {"note", "The integrator recorded no variables. Add some with /api/dynamics/monitor and run again."}
            }.ToString(Newtonsoft.Json.Formatting.None)
        End If

        Dim maxPoints = CInt(Num(payload, "max_points", DefaultPreviewPoints))
        If maxPoints < 3 Then maxPoints = 3
        If maxPoints > MaxPreviewPoints Then maxPoints = MaxPreviewPoints

        Dim startSeconds = Num(payload, "t_start_s", 0.0)
        Dim endSeconds = Num(payload, "t_end_s", 0.0)

        Dim lo As Double? = If(startSeconds > 0, CType(startSeconds, Double?), Nothing)
        Dim hi As Double? = If(endSeconds > 0, CType(endSeconds, Double?), Nothing)

        Dim timeline = SeriesDecimator.Preview(selected(0), maxPoints, lo, hi)

        Dim bodyJson As New JObject()
        For Each s In selected
            Dim values As New JArray()
            For Each t In timeline.Times
                values.Add(SeriesDecimator.Format(s.ValueAt(t)))
            Next
            bodyJson(s.Name) = New JObject From {{"units", s.Units}, {"values", values}}
        Next

        Return New JObject From {
            {"success", True},
            {"run_id", runId},
            {"t_s", New JArray(timeline.Times.Select(Function(t) DirectCast(SeriesDecimator.Format(t), Object)).ToArray())},
            {"series", bodyJson},
            {"points", timeline.Times.Length},
            {"decimated_from", selected(0).Count},
            {"note", "Decimated preview. Call /api/dynamics/export for the full series."}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Analyze(runId As String, payload As JObject, outcome As RouteResult) As String

        Dim failure As String = Nothing
        Dim result = RequireResult(runId, outcome, failure)
        If result Is Nothing Then Return failure

        Dim selected = SelectSeries(result, payload)
        Dim band = Num(payload, "settling_band_pct", 2.0) / 100.0
        Dim requested = Num(payload, "setpoint", Double.NaN)

        Dim analyses As New JArray()
        For Each s In selected.Take(MaxListItems)
            Dim target = If(Double.IsNaN(requested), s.SteadyState(), requested)

            Dim period, decay As Double
            Dim oscillating = s.IsOscillating(period, decay)

            Dim entry As New JObject From {
                {"variable", s.Name},
                {"units", s.Units},
                {"initial", SeriesDecimator.Format(s.Initial)},
                {"final", SeriesDecimator.Format(s.Final)},
                {"min", SeriesDecimator.Format(s.Min)},
                {"max", SeriesDecimator.Format(s.Max)},
                {"steady_state", SeriesDecimator.Format(s.SteadyState())},
                {"setpoint", SeriesDecimator.Format(target)},
                {"offset", SeriesDecimator.Format(s.Offset(target))},
                {"overshoot_pct", SeriesDecimator.Format(s.Overshoot(target))},
                {"peak_time_s", SeriesDecimator.Format(s.PeakTime(target))},
                {"rise_time_s", SeriesDecimator.Format(s.RiseTime())},
                {"settling_time_s", SeriesDecimator.Format(s.SettlingTime(band))},
                {"iae", SeriesDecimator.Format(s.IAE(target))},
                {"ise", SeriesDecimator.Format(s.ISE(target))},
                {"itae", SeriesDecimator.Format(s.ITAE(target))},
                {"verdict", Verdict(s, oscillating, decay)}
            }

            If oscillating Then
                entry("oscillation_period_s") = SeriesDecimator.Format(period)
                If Not Double.IsNaN(decay) Then entry("decay_ratio") = SeriesDecimator.Format(decay)
            End If

            analyses.Add(entry)
        Next

        Return New JObject From {
            {"success", True},
            {"run_id", runId},
            {"analysis", analyses}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Diagnose(fs As IFlowsheet, runId As String, outcome As RouteResult) As String

        If runId = "" Then
            Return New JObject From {
                {"success", True},
                {"findings", FindingsJson(DynamicsDiagnostics.CheckReady(fs, Nothing))}
            }.ToString(Newtonsoft.Json.Formatting.None)
        End If

        Dim run = FindRun(runId, outcome)
        If run Is Nothing Then Return Fail("unknown_run", "No dynamics run with id '" & runId & "'.")

        If run.Result Is Nothing Then
            Return New JObject From {
                {"success", True},
                {"run_id", runId},
                {"state", run.State},
                {"findings", New JArray()},
                {"note", "The run has produced no results yet."}
            }.ToString(Newtonsoft.Json.Formatting.None)
        End If

        Return New JObject From {
            {"success", True},
            {"run_id", runId},
            {"state", run.State},
            {"findings", FindingsJson(DynamicsDiagnostics.Diagnose(fs, run.Result))}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    Private Function Export(payload As JObject, outcome As RouteResult) As String

        Dim runId = Str(payload, "run_id", "")
        Dim filePath = Str(payload, "file_path", "")
        If filePath = "" Then Throw New ArgumentException("Pass the path of the file to write.")

        Dim failure As String = Nothing
        Dim result = RequireResult(runId, outcome, failure)
        If result Is Nothing Then Return failure

        result.ToCsv(filePath)

        Return New JObject From {
            {"success", True},
            {"run_id", runId},
            {"file_path", filePath},
            {"rows", If(result.Series.Count = 0, 0, result.Series.Max(Function(s) s.Count))},
            {"variables", result.Series.Count}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    ' ------------------------------------------------------------ Visualisation

    ''' <summary>
    ''' Puts a live chart of the integrator's monitored variables on the flowsheet, and returns a
    ''' screenshot of it.
    ''' </summary>
    ''' <remarks>
    ''' The chart is a normal flowsheet graphic that redraws itself from the integrator's history,
    ''' so it keeps up during a run and is saved with the file. The two properties that make that
    ''' work are <c>OwnerID</c>, which has to be the literal "Dynamic Mode Integrators", and
    ''' <c>ModelName</c>, which has to be the integrator's description.
    ''' </remarks>
    Private Function Chart(fs As IFlowsheet, payload As JObject, outcome As RouteResult) As String

        Dim integratorName = Str(payload, "integrator", "")
        If integratorName = "" Then
            Dim schedule = IntegratorRunner.ResolveSchedule(fs, Str(payload, "schedule", Nothing))
            If Not fs.DynamicsManager.IntegratorList.ContainsKey(schedule.CurrentIntegrator) Then
                Throw New InvalidOperationException("Schedule '" & schedule.Description & "' has no integrator.")
            End If
            integratorName = fs.DynamicsManager.IntegratorList(schedule.CurrentIntegrator).Description
        End If

        Dim chartType = Type.GetType(
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts.OxyPlotGraphic, DWSIM.DrawingTools.SkiaSharp.Extended")

        If chartType Is Nothing Then
            outcome.StatusCode = 400
            Return Fail("chart_unavailable",
                        "This build has no chart graphic. Read the numbers with /api/dynamics/series instead.")
        End If

        Dim x = CInt(Num(payload, "x", 50))
        Dim y = CInt(Num(payload, "y", 50))
        Dim width = CInt(Num(payload, "width", 500))
        Dim height = CInt(Num(payload, "height", 350))

        Dim created As Drawing.SkiaSharp.GraphicObjects.GraphicObject = Nothing
        Dim failure As Exception = Nothing
        Dim finished As Boolean = False

        fs.RunCodeOnUIThread(Sub()
                                 Try
                                     Dim graphic = DirectCast(Activator.CreateInstance(chartType, New Object() {x, y}),
                                                              Drawing.SkiaSharp.GraphicObjects.GraphicObject)
                                     graphic.Flowsheet = fs
                                     graphic.Name = Guid.NewGuid().ToString()
                                     graphic.Tag = "Dynamics: " & integratorName
                                     graphic.Width = width
                                     graphic.Height = height

                                     chartType.GetProperty("OwnerID").SetValue(graphic, "Dynamic Mode Integrators")
                                     chartType.GetProperty("ModelName").SetValue(graphic, integratorName)

                                     fs.AddGraphicObject(graphic)
                                     created = graphic
                                 Catch ex As Exception
                                     failure = ex
                                 Finally
                                     finished = True
                                 End Try
                             End Sub)

        While Not finished : Threading.Thread.Sleep(50) : End While

        If failure IsNot Nothing Then
            outcome.StatusCode = 400
            Return Fail("chart_failed", BaseMessage(failure))
        End If

        Dim result As New JObject From {
            {"success", True},
            {"integrator", integratorName},
            {"graphic_name", created.Name},
            {"x", created.X},
            {"y", created.Y}
        }

        ' The same picture the user is now looking at, so the assistant can talk about it.
        If Bool(payload, "screenshot", True) Then
            Try
                Dim tmp = IO.Path.Combine(IO.Path.GetTempPath(), "dwsim_dynamics_" & Guid.NewGuid().ToString("N") & ".png")
                fs.SavePFDScreenshotToPNG(tmp)
                Dim bytes = IO.File.ReadAllBytes(tmp)
                IO.File.Delete(tmp)
                result("base64") = Convert.ToBase64String(bytes)
                result("format") = "png"
            Catch ex As Exception
                result("screenshot_error") = BaseMessage(ex)
            End Try
        End If

        Return result.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    ''' <summary>
    ''' Writes the integrator's recorded history into a spreadsheet worksheet, for the user to work
    ''' the numbers by hand.
    ''' </summary>
    Private Function ToSpreadsheet(fs As IFlowsheet, payload As JObject, outcome As RouteResult) As String

        Dim schedule = IntegratorRunner.ResolveSchedule(fs, Str(payload, "schedule", Nothing))
        If Not fs.DynamicsManager.IntegratorList.ContainsKey(schedule.CurrentIntegrator) Then
            Throw New InvalidOperationException("Schedule '" & schedule.Description & "' has no integrator.")
        End If

        Dim integrator = fs.DynamicsManager.IntegratorList(schedule.CurrentIntegrator)

        If integrator.MonitoredVariables.Count = 0 Then
            outcome.StatusCode = 409
            Return Fail("no_monitored_variables", "The integrator records no variables.")
        End If
        If integrator.MonitoredVariableValues.Count = 0 Then
            outcome.StatusCode = 409
            Return Fail("no_results", "No results stored yet. Run the integrator first.")
        End If

        Dim rows As Integer = 0
        Dim failure As Exception = Nothing
        Dim finished As Boolean = False

        fs.RunCodeOnUIThread(Sub()
                                 Try
                                     ' The grid is a ReoGrid control the host owns; reach it late so
                                     ' this module needs no reference to the spreadsheet assembly.
                                     Dim grid As Object = fs.GetSpreadsheetObject()
                                     If grid Is Nothing Then
                                         failure = New InvalidOperationException("This host has no spreadsheet.")
                                         Return
                                     End If

                                     Dim sheet As Object = CallByName(grid, "NewWorksheet", CallType.Method,
                                                                      "Integrator Results")
                                     CallByName(sheet, "RowCount", CallType.Let,
                                                integrator.MonitoredVariableValues.Count + 1)

                                     Dim cells As Object = CallByName(sheet, "Cells", CallType.Get)
                                     CallByName(CallByName(cells, "Item", CallType.Get, 0, 0), "Data", CallType.Let, "Time (ms)")

                                     Dim column As Integer = 1
                                     For Each v In integrator.MonitoredVariables
                                         Dim header = v.Description &
                                             If(String.IsNullOrEmpty(v.PropertyUnits), "", " (" & v.PropertyUnits & ")")
                                         CallByName(CallByName(cells, "Item", CallType.Get, 0, column), "Data", CallType.Let, header)
                                         column += 1
                                     Next

                                     ' Keyed by DateTime.Ticks, counted from DateTime zero.
                                     Dim row As Integer = 1
                                     For Each item In integrator.MonitoredVariableValues
                                         CallByName(CallByName(cells, "Item", CallType.Get, row, 0), "Data", CallType.Let,
                                                    item.Key / CDbl(TimeSpan.TicksPerMillisecond))
                                         column = 1
                                         For Each v In item.Value
                                             Dim parsed As Double
                                             Dim value As Object = If(
                                                 Double.TryParse(v.PropertyValue, Globalization.NumberStyles.Any,
                                                                 Globalization.CultureInfo.InvariantCulture, parsed),
                                                 CObj(parsed), CObj(v.PropertyValue))
                                             CallByName(CallByName(cells, "Item", CallType.Get, row, column), "Data", CallType.Let, value)
                                             column += 1
                                         Next
                                         row += 1
                                     Next

                                     CallByName(grid, "CurrentWorksheet", CallType.Let, sheet)
                                     rows = row - 1

                                 Catch ex As Exception
                                     failure = ex
                                 Finally
                                     finished = True
                                 End Try
                             End Sub)

        While Not finished : Threading.Thread.Sleep(50) : End While

        If failure IsNot Nothing Then
            outcome.StatusCode = 400
            Return Fail("spreadsheet_failed", BaseMessage(failure))
        End If

        Return New JObject From {
            {"success", True},
            {"worksheet", "Integrator Results"},
            {"rows", rows},
            {"variables", integrator.MonitoredVariables.Count}
        }.ToString(Newtonsoft.Json.Formatting.None)

    End Function

    ' -------------------------------------------------------------------------

    Private Function NewRun(kind As String) As DynamicsRun

        ' Keep the recent few and drop the rest, so a long session does not hold every result
        ' it ever produced.
        For Each stale In Runs.Values.Where(Function(r) r.IsFinished).
                OrderBy(Function(r) r.StartedAt).Take(Math.Max(0, Runs.Count - MaxRuns)).ToList()
            Dim removed As DynamicsRun = Nothing
            Runs.TryRemove(stale.Id, removed)
        Next

        Dim run As New DynamicsRun With {
            .Id = "dyn_" & Guid.NewGuid().ToString("N").Substring(0, 8),
            .Kind = kind
        }
        Runs(run.Id) = run
        Return run

    End Function

    Private Function FindRun(runId As String, outcome As RouteResult) As DynamicsRun
        Dim run As DynamicsRun = Nothing
        If Runs.TryGetValue(runId, run) Then Return run
        outcome.StatusCode = 404
        Return Nothing
    End Function

    ''' <summary>
    ''' Fetches a finished run's results, or records why it could not: an id nobody knows reads
    ''' differently from a run that simply has not produced anything yet.
    ''' </summary>
    Private Function RequireResult(runId As String, outcome As RouteResult, ByRef failure As String) As FAPI.DynamicsResult

        failure = Nothing

        Dim run = FindRun(runId, outcome)
        If run Is Nothing Then
            failure = Fail("unknown_run", "No dynamics run with id '" & runId & "'. It may have expired.")
            Return Nothing
        End If

        If run.Result Is Nothing Then
            outcome.StatusCode = 409
            failure = Fail("no_results", "Run '" & runId & "' is " & run.State & " and has no results yet." &
                           If(run.ErrorMessage = "", "", " It failed: " & run.ErrorMessage))
            Return Nothing
        End If

        Return run.Result

    End Function

    Private Function SelectSeries(result As FAPI.DynamicsResult, payload As JObject) As List(Of FAPI.DynamicsSeries)

        Dim wanted = payload("variables")
        If wanted Is Nothing Then
            Dim single_ = Str(payload, "variable", "")
            If single_ = "" Then Return result.Series.ToList()
            wanted = New JArray(single_)
        End If

        Dim selected As New List(Of FAPI.DynamicsSeries)
        For Each item In wanted
            Dim name = item.ToString()
            Dim series As FAPI.DynamicsSeries = Nothing
            If Not result.TryGetSeries(name, series) Then
                Throw New ArgumentException("No monitored variable named '" & name & "'. Available: " &
                                            String.Join(", ", result.Series.Select(Function(s) "'" & s.Name & "'")) & ".")
            End If
            selected.Add(series)
        Next
        Return selected

    End Function

    Private Sub WaitFor(run As DynamicsRun, timeoutSeconds As Integer)
        Dim deadline = Date.UtcNow.AddSeconds(timeoutSeconds)
        While Not run.IsFinished AndAlso Date.UtcNow < deadline
            Threading.Thread.Sleep(100)
        End While
    End Sub

    Private Function Summarise(run As DynamicsRun) As JObject

        Dim result = run.Result
        Dim variables As New JArray()

        For Each s In result.Series.Take(MaxListItems)
            variables.Add(New JObject From {
                {"variable", s.Name},
                {"units", s.Units},
                {"first", SeriesDecimator.Format(s.Initial)},
                {"last", SeriesDecimator.Format(s.Final)},
                {"min", SeriesDecimator.Format(s.Min)},
                {"max", SeriesDecimator.Format(s.Max)},
                {"settled", s.HasConverged()},
                {"diverged", s.HasDiverged}
            })
        Next

        Return New JObject From {
            {"schedule", result.ScheduleName},
            {"integrator", result.IntegratorName},
            {"completed", result.Completed},
            {"aborted", result.Aborted},
            {"steps", result.Steps},
            {"simulated_s", SeriesDecimator.Format(result.FinalTimeSeconds)},
            {"wall_clock_s", SeriesDecimator.Format(result.WallClock.TotalSeconds)},
            {"variables", variables}
        }

    End Function

    Private Function Describe(tuning As PidTuningResult) As JObject

        Dim controllers As New JArray()
        For Each c In tuning.Controllers
            controllers.Add(New JObject From {
                {"tag", c.Tag},
                {"kp", SeriesDecimator.Format(c.Kp)},
                {"ki", SeriesDecimator.Format(c.Ki)},
                {"kd", SeriesDecimator.Format(c.Kd)},
                {"original_kp", SeriesDecimator.Format(c.OriginalKp)},
                {"original_ki", SeriesDecimator.Format(c.OriginalKi)},
                {"original_kd", SeriesDecimator.Format(c.OriginalKd)}
            })
        Next

        Return New JObject From {
            {"succeeded", tuning.Succeeded},
            {"applied", tuning.Applied},
            {"aborted", tuning.Aborted},
            {"evaluations", tuning.Evaluations},
            {"initial_objective", SeriesDecimator.Format(tuning.InitialObjective)},
            {"final_objective", SeriesDecimator.Format(tuning.FinalObjective)},
            {"improvement_pct", SeriesDecimator.Format(tuning.ImprovementPercent)},
            {"controllers", controllers}
        }

    End Function

    Private Function Describe(c As ControllerInfo) As JObject
        Return New JObject From {
            {"tag", c.Tag},
            {"wired", c.IsWired},
            {"active", c.Active},
            {"manual", c.ManualOverride},
            {"reverse_acting", c.ReverseActing},
            {"execution_order", c.ExecutionOrder},
            {"kp", SeriesDecimator.Format(c.Kp)},
            {"ki", SeriesDecimator.Format(c.Ki)},
            {"kd", SeriesDecimator.Format(c.Kd)},
            {"sp", SeriesDecimator.Format(c.SetPoint)},
            {"pv", SeriesDecimator.Format(c.ProcessVariable)},
            {"mv", SeriesDecimator.Format(c.ManipulatedVariable)},
            {"out_min", SeriesDecimator.Format(c.OutputMin)},
            {"out_max", SeriesDecimator.Format(c.OutputMax)},
            {"controls", c.ControlledObjectId & "." & c.ControlledProperty},
            {"manipulates", c.ManipulatedObjectId & "." & c.ManipulatedProperty}
        }
    End Function

    Private Function Verdict(s As FAPI.DynamicsSeries, oscillating As Boolean, decay As Double) As String
        If s.HasDiverged Then Return "divergent"
        If oscillating AndAlso (Double.IsNaN(decay) OrElse decay > 0.9) Then Return "sustained_oscillation"
        If oscillating Then Return "damped_oscillation"
        If Not s.HasConverged() Then Return "still_moving"
        Return "stable"
    End Function

    Private Function FindingsJson(items As IEnumerable(Of DynamicsFinding)) As JArray
        Dim arr As New JArray()
        For Each f In items.Take(MaxListItems)
            arr.Add(New JObject From {
                {"code", f.Code},
                {"severity", f.Severity.ToString().ToLowerInvariant()},
                {"object", f.ObjectTag},
                {"message", f.Message},
                {"fix", f.Fix}
            })
        Next
        Return arr
    End Function

    Private Function ParseTransition(kind As String) As DynEnums.DynamicsEventTransitionType
        Select Case kind
            Case "linear" : Return DynEnums.DynamicsEventTransitionType.LinearChange
            Case "log" : Return DynEnums.DynamicsEventTransitionType.LogChange
            Case "inverse_log" : Return DynEnums.DynamicsEventTransitionType.InverseLogChange
            Case "random" : Return DynEnums.DynamicsEventTransitionType.RandomChange
            Case "step" : Return DynEnums.DynamicsEventTransitionType.StepChange
            Case Else
                Throw New ArgumentException("Unknown transition '" & kind &
                                            "'. Use step, linear, log, inverse_log or random.")
        End Select
    End Function

    Private Sub RequireName(name As String)
        If name = "" Then Throw New ArgumentException("This action needs a state name.")
    End Sub

    Private Function Arr(items As IEnumerable(Of String)) As JArray
        Return New JArray(items.Take(MaxListItems).Cast(Of Object).ToArray())
    End Function

    Private Function Str(payload As JObject, key As String, fallback As String) As String
        Dim token = payload(key)
        If token Is Nothing OrElse token.Type = JTokenType.Null Then Return fallback
        Return token.ToString()
    End Function

    Private Function Num(payload As JObject, key As String, fallback As Double) As Double
        Dim token = payload(key)
        If token Is Nothing OrElse token.Type = JTokenType.Null Then Return fallback
        Dim value As Double
        If Double.TryParse(token.ToString(), Globalization.NumberStyles.Any,
                           Globalization.CultureInfo.InvariantCulture, value) Then Return value
        Return fallback
    End Function

    Private Function Bool(payload As JObject, key As String, fallback As Boolean) As Boolean
        Dim token = payload(key)
        If token Is Nothing OrElse token.Type = JTokenType.Null Then Return fallback
        Dim value As Boolean
        If Boolean.TryParse(token.ToString(), value) Then Return value
        Return fallback
    End Function

    Private Function ToDouble(value As Object) As Double
        Try
            Return Convert.ToDouble(value, Globalization.CultureInfo.InvariantCulture)
        Catch
            Return Double.NaN
        End Try
    End Function

    Private Function BaseMessage(ex As Exception) As String
        Dim baseex = ex
        While baseex.InnerException IsNot Nothing
            baseex = baseex.InnerException
        End While
        Return baseex.Message
    End Function

    Private Function Fail(code As String, message As String) As String
        Return New JObject From {
            {"success", False},
            {"error", code},
            {"message", message}
        }.ToString(Newtonsoft.Json.Formatting.None)
    End Function

End Module
