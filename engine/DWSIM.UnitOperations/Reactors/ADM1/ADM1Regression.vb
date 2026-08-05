'    Full ADM1 - Parameter regression engine (Nelder-Mead Simplex).
'    Fits selected ADM1 kinetic / inhibition parameters to a user-supplied
'    time-series dataset by minimising weighted sum of squared residuals
'    between the ADM1 simulated trajectory and measured observables.
'
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Math
Imports System.Reflection
Imports System.Text
Imports DotNumerics.Optimization

Namespace Reactors.ADM1

    ''' <summary>Time-series experimental dataset.</summary>
    <Serializable()> Public Class ADM1RegressionDataset

        ''' <summary>Measurement times (days).</summary>
        Public Property Times As New List(Of Double)()

        ''' <summary>Observable name → measured values, in the same order as Times.</summary>
        Public Property Observations As New Dictionary(Of String, List(Of Double))()

        Public Function SeriesNames() As String()
            Return Observations.Keys.ToArray()
        End Function

        Public Sub Clear()
            Times.Clear() : Observations.Clear()
        End Sub

        ''' <summary>
        ''' Load a CSV with the first column named "time" (or "time_d", "t", "time_s" with unit detection)
        ''' and subsequent columns treated as observable series. CSV is comma, semicolon or tab delimited.
        ''' If the first-column header ends with "_s" values are divided by 86400 to give days.
        ''' </summary>
        Public Shared Function FromCSV(path As String) As ADM1RegressionDataset
            Dim lines = File.ReadAllLines(path)
            Return FromCSVLines(lines)
        End Function

        Public Shared Function FromCSVLines(lines As String()) As ADM1RegressionDataset
            Dim ds As New ADM1RegressionDataset()
            If lines Is Nothing OrElse lines.Length < 2 Then Return ds
            Dim sep As Char = PickSeparator(lines(0))
            Dim header = lines(0).Split(sep)
            Dim timeCol As String = header(0).Trim()
            Dim toDays As Double = 1.0
            If timeCol.ToLowerInvariant().Contains("_s") OrElse timeCol.ToLowerInvariant() = "t_s" Then toDays = 1.0 / 86400.0
            If timeCol.ToLowerInvariant().Contains("_h") OrElse timeCol.ToLowerInvariant() = "t_h" Then toDays = 1.0 / 24.0
            For i = 1 To header.Length - 1
                ds.Observations(header(i).Trim()) = New List(Of Double)()
            Next
            Dim ci = CultureInfo.InvariantCulture
            For li = 1 To lines.Length - 1
                Dim raw = lines(li)
                If String.IsNullOrWhiteSpace(raw) Then Continue For
                Dim parts = raw.Split(sep)
                If parts.Length < 2 Then Continue For
                Dim t As Double
                If Not Double.TryParse(parts(0).Trim(), NumberStyles.Any, ci, t) Then Continue For
                ds.Times.Add(t * toDays)
                For i = 1 To header.Length - 1
                    Dim v As Double = Double.NaN
                    If i < parts.Length Then
                        Double.TryParse(parts(i).Trim(), NumberStyles.Any, ci, v)
                    End If
                    ds.Observations(header(i).Trim()).Add(v)
                Next
            Next
            Return ds
        End Function

        Private Shared Function PickSeparator(firstLine As String) As Char
            If firstLine.Contains(";") Then Return ";"c
            If firstLine.Contains(ChrW(9)) Then Return ChrW(9)
            Return ","c
        End Function

        Public Function ToDataTable() As DataTable
            Dim dt As New DataTable("ADM1RegressionDataset")
            dt.Columns.Add("time_d", GetType(Double))
            For Each k In Observations.Keys
                dt.Columns.Add(k, GetType(Double))
            Next
            For i = 0 To Times.Count - 1
                Dim row = dt.NewRow()
                row("time_d") = Times(i)
                For Each k In Observations.Keys
                    If i < Observations(k).Count Then row(k) = Observations(k)(i)
                Next
                dt.Rows.Add(row)
            Next
            Return dt
        End Function

    End Class

    ''' <summary>
    ''' Specification of one fit variable: a dotted path into ADM1Parameters
    ''' (e.g. "Kinetics.k_m_ac", "Inhibition.K_I_nh3"), plus bounds and initial value.
    ''' </summary>
    <Serializable()> Public Class ADM1ParameterSpec
        Public Property Path As String
        Public Property InitialValue As Double
        Public Property LowerBound As Double
        Public Property UpperBound As Double
        Public Property LogScale As Boolean = True

        Public Sub New()
        End Sub

        Public Sub New(path_ As String, init As Double, lb As Double, ub As Double, Optional logScale_ As Boolean = True)
            Path = path_ : InitialValue = init : LowerBound = lb : UpperBound = ub : LogScale = logScale_
        End Sub

        Public Overrides Function ToString() As String
            Return Path & " = " & InitialValue.ToString("G6", CultureInfo.InvariantCulture) &
                   " ∈ [" & LowerBound.ToString("G4", CultureInfo.InvariantCulture) & ", " &
                   UpperBound.ToString("G4", CultureInfo.InvariantCulture) & "]"
        End Function
    End Class

    ''' <summary>Common parameters that are typically worth fitting.</summary>
    Public Module ADM1RegressionDefaults

        Public Function SuggestParameters(baseline As ADM1Parameters) As List(Of ADM1ParameterSpec)
            Dim specs As New List(Of ADM1ParameterSpec)()
            Dim add = Sub(p As String, lbF As Double, ubF As Double)
                          Dim v = GetParamValue(baseline, p)
                          specs.Add(New ADM1ParameterSpec(p, v, v * lbF, v * ubF))
                      End Sub
            add("Kinetics.k_m_ac", 0.2, 5.0)
            add("Kinetics.K_S_ac", 0.2, 5.0)
            add("Kinetics.k_m_pro", 0.2, 5.0)
            add("Kinetics.K_S_pro", 0.2, 5.0)
            add("Kinetics.k_m_h2", 0.2, 5.0)
            add("Kinetics.k_dec_X_ac", 0.2, 5.0)
            add("Inhibition.K_I_nh3", 0.2, 5.0)
            add("Physicochemical.k_La", 0.2, 5.0)
            Return specs
        End Function

        ''' <summary>Set a dotted-path value on an ADM1Parameters object via reflection.</summary>
        Public Sub SetParamValue(p As ADM1Parameters, path As String, value As Double)
            Dim parts = path.Split("."c)
            If parts.Length < 2 Then Return
            Dim parent As Object = p
            Dim parentType = parent.GetType()
            For i = 0 To parts.Length - 2
                Dim prop = parentType.GetProperty(parts(i))
                If prop Is Nothing Then Return
                parent = prop.GetValue(parent, Nothing)
                If parent Is Nothing Then Return
                parentType = parent.GetType()
            Next
            Dim leaf = parentType.GetProperty(parts(parts.Length - 1))
            If leaf Is Nothing OrElse Not leaf.CanWrite Then Return
            leaf.SetValue(parent, value, Nothing)
        End Sub

        Public Function GetParamValue(p As ADM1Parameters, path As String) As Double
            Dim parts = path.Split("."c)
            If parts.Length < 2 Then Return Double.NaN
            Dim parent As Object = p
            Dim parentType = parent.GetType()
            For i = 0 To parts.Length - 2
                Dim prop = parentType.GetProperty(parts(i))
                If prop Is Nothing Then Return Double.NaN
                parent = prop.GetValue(parent, Nothing)
                If parent Is Nothing Then Return Double.NaN
                parentType = parent.GetType()
            Next
            Dim leaf = parentType.GetProperty(parts(parts.Length - 1))
            If leaf Is Nothing Then Return Double.NaN
            Return CDbl(leaf.GetValue(parent, Nothing))
        End Function

    End Module

    ''' <summary>Regression run result.</summary>
    <Serializable()> Public Class ADM1RegressionResult
        Public Property FittedValues As New Dictionary(Of String, Double)()
        Public Property InitialSSR As Double
        Public Property FinalSSR As Double
        Public Property Iterations As Integer
        Public Property Converged As Boolean
        Public Property Message As String = ""
        Public Property ObjectiveHistory As New List(Of Double)()
        ''' <summary>Last trajectory run with the fitted parameters.</summary>
        Public Property FittedTrajectory As ADM1TrajectoryResult
        ''' <summary>Per-observable RMSE at fitted parameters.</summary>
        Public Property RMSE As New Dictionary(Of String, Double)()
        ''' <summary>Seconds of wall-clock time.</summary>
        Public Property ElapsedSeconds As Double
    End Class

    ''' <summary>Nelder-Mead regression driver for the ADM1 model.</summary>
    Public Module ADM1Regression

        ''' <summary>
        ''' Fit the given parameter specs so that the simulated trajectory best matches
        ''' the measured dataset. The baseline ADM1Parameters is cloned internally; the
        ''' original instance is never mutated. Observables must be names accepted by
        ''' ADM1TrajectoryResult.GetSeries (ADM1State members + derived: pH, Q_gas, x_CH4,
        ''' x_CO2, x_H2, Total_VFA).
        ''' </summary>
        Public Function Fit(baseline As ADM1Parameters,
                            initial As ADM1State,
                            qInflow_m3d As Double,
                            Sin As Double(),
                            dataset As ADM1RegressionDataset,
                            specs As List(Of ADM1ParameterSpec),
                            Optional maxIterations As Integer = 400,
                            Optional tolerance As Double = 0.0000001,
                            Optional weights As Dictionary(Of String, Double) = Nothing,
                            Optional progress As Action(Of Integer, Double) = Nothing,
                            Optional cancelCheck As Func(Of Boolean) = Nothing) As ADM1RegressionResult

            Dim t0 = DateTime.Now
            Dim res As New ADM1RegressionResult()
            If specs Is Nothing OrElse specs.Count = 0 Then
                res.Message = "No parameters selected for regression."
                Return res
            End If
            If dataset Is Nothing OrElse dataset.Times Is Nothing OrElse dataset.Times.Count < 2 Then
                res.Message = "Dataset has fewer than 2 measurement points."
                Return res
            End If
            If dataset.Observations Is Nothing OrElse dataset.Observations.Count = 0 Then
                res.Message = "Dataset has no observable columns."
                Return res
            End If

            Dim tEnd = dataset.Times.Max()
            If tEnd <= 0.0 Then
                res.Message = "Dataset time horizon is non-positive."
                Return res
            End If

            ' Precompute target arrays (skip NaN rows when comparing)
            Dim targets As New Dictionary(Of String, Double())()
            For Each kv In dataset.Observations
                Dim arr = kv.Value.ToArray()
                targets(kv.Key) = arr
            Next

            ' Weights default: 1/(max-min)^2 per series to balance scale
            If weights Is Nothing Then weights = New Dictionary(Of String, Double)()
            For Each k In targets.Keys
                If Not weights.ContainsKey(k) Then
                    Dim vals = targets(k).Where(Function(v) Not Double.IsNaN(v)).ToArray()
                    If vals.Length >= 2 Then
                        Dim rng = Abs(vals.Max() - vals.Min())
                        If rng < 0.000000000001 Then rng = Max(Abs(vals.Average()), 0.000000000001)
                        weights(k) = 1.0 / (rng * rng)
                    Else
                        weights(k) = 1.0
                    End If
                End If
            Next

            ' Build bounded vars (log-transform parameters flagged LogScale)
            Dim nVar = specs.Count
            Dim vars As New List(Of OptSimplexBoundVariable)()
            Dim x0(nVar - 1) As Double
            Dim xLB(nVar - 1) As Double
            Dim xUB(nVar - 1) As Double
            For i = 0 To nVar - 1
                Dim s = specs(i)
                Dim v0 = s.InitialValue
                Dim lb = s.LowerBound
                Dim ub = s.UpperBound
                If ub <= lb Then ub = lb + Abs(lb) * 1.0 + 1.0E-09
                If s.LogScale AndAlso lb > 0 AndAlso v0 > 0 Then
                    Dim zv = Log(v0)
                    Dim zlb = Log(lb)
                    Dim zub = Log(ub)
                    x0(i) = zv : xLB(i) = zlb : xUB(i) = zub
                    vars.Add(New OptSimplexBoundVariable(zv, zlb, zub))
                Else
                    x0(i) = v0 : xLB(i) = lb : xUB(i) = ub
                    vars.Add(New OptSimplexBoundVariable(v0, lb, ub))
                End If
            Next

            Dim evals As Integer = 0
            Dim bestSSR As Double = Double.MaxValue
            Dim bestX As Double() = x0.ToArray()

            Dim evalObjective = Function(xi As Double()) As Double
                                    If cancelCheck IsNot Nothing AndAlso cancelCheck() Then Return bestSSR
                                    ' Construct a working copy of parameters with this candidate vector
                                    Dim pTrial = CloneParams(baseline)
                                    For i = 0 To nVar - 1
                                        Dim v = xi(i)
                                        If specs(i).LogScale AndAlso specs(i).LowerBound > 0 AndAlso specs(i).InitialValue > 0 Then
                                            v = Exp(v)
                                        End If
                                        ' clamp to bounds (Nelder-Mead can overshoot)
                                        If v < specs(i).LowerBound Then v = specs(i).LowerBound
                                        If v > specs(i).UpperBound Then v = specs(i).UpperBound
                                        ADM1RegressionDefaults.SetParamValue(pTrial, specs(i).Path, v)
                                    Next
                                    Dim ssr As Double
                                    Try
                                        ssr = ComputeSSR(pTrial, initial, qInflow_m3d, Sin, dataset, targets, weights, tEnd)
                                    Catch ex As Exception
                                        ssr = 1.0E+30
                                    End Try
                                    If Double.IsNaN(ssr) OrElse Double.IsInfinity(ssr) Then ssr = 1.0E+30
                                    evals += 1
                                    res.ObjectiveHistory.Add(ssr)
                                    If ssr < bestSSR Then
                                        bestSSR = ssr
                                        bestX = xi.ToArray()
                                    End If
                                    progress?.Invoke(evals, ssr)
                                    Return ssr
                                End Function

            ' Initial objective
            Dim ssr0 = evalObjective(x0)
            res.InitialSSR = ssr0

            Dim splex As New Simplex()
            splex.MaxFunEvaluations = maxIterations
            splex.Tolerance = tolerance
            Dim xsol As Double()
            Try
                xsol = splex.ComputeMin(Function(xi) evalObjective(xi), vars.ToArray())
            Catch ex As Exception
                res.Message = "Optimiser failed: " & ex.Message
                xsol = bestX
            End Try

            ' Prefer the best-seen point over the simplex-returned point (simplex can
            ' sometimes report a non-optimum when it exits on max evaluations)
            If bestX IsNot Nothing AndAlso ComputeIfLower(evalObjective, xsol, bestX) Then
                xsol = bestX
            End If

            ' Extract fitted values and run a final trajectory for plotting
            Dim pFinal = CloneParams(baseline)
            For i = 0 To nVar - 1
                Dim v = xsol(i)
                If specs(i).LogScale AndAlso specs(i).LowerBound > 0 AndAlso specs(i).InitialValue > 0 Then v = Exp(v)
                If v < specs(i).LowerBound Then v = specs(i).LowerBound
                If v > specs(i).UpperBound Then v = specs(i).UpperBound
                ADM1RegressionDefaults.SetParamValue(pFinal, specs(i).Path, v)
                res.FittedValues(specs(i).Path) = v
            Next

            Dim fitTraj As ADM1TrajectoryResult = Nothing
            Try
                fitTraj = ADM1Integrator.Integrate(initial.Clone(), pFinal, qInflow_m3d, Sin, 0.0, tEnd)
            Catch
            End Try
            res.FittedTrajectory = fitTraj

            ' RMSE per observable on the fitted trajectory
            If fitTraj IsNot Nothing Then
                For Each obs In dataset.Observations.Keys
                    Dim tgt = targets(obs)
                    Dim sim = InterpolateSeries(fitTraj, obs, dataset.Times.ToArray())
                    Dim n As Integer = 0 : Dim sse As Double = 0.0
                    For i = 0 To tgt.Length - 1
                        If Not Double.IsNaN(tgt(i)) AndAlso Not Double.IsNaN(sim(i)) Then
                            sse += (sim(i) - tgt(i)) * (sim(i) - tgt(i))
                            n += 1
                        End If
                    Next
                    res.RMSE(obs) = If(n > 0, Sqrt(sse / n), Double.NaN)
                Next
            End If

            res.FinalSSR = bestSSR
            res.Iterations = evals
            res.Converged = (bestSSR < ssr0)
            res.ElapsedSeconds = (DateTime.Now - t0).TotalSeconds
            If String.IsNullOrEmpty(res.Message) Then
                res.Message = If(res.Converged,
                                 "Regression complete: SSR reduced from " & ssr0.ToString("G6") & " to " & bestSSR.ToString("G6"),
                                 "Regression exited without improving SSR (" & bestSSR.ToString("G6") & ").")
            End If
            Return res
        End Function

        Private Function ComputeIfLower(objFun As Func(Of Double(), Double), xNew As Double(), xBest As Double()) As Boolean
            If xNew Is Nothing OrElse xBest Is Nothing Then Return False
            If xNew.Length <> xBest.Length Then Return False
            Dim valNew = objFun(xNew)
            Dim valBest = objFun(xBest)
            Return valBest < valNew
        End Function

        Private Function ComputeSSR(p As ADM1Parameters, state0 As ADM1State, q As Double, Sin As Double(),
                                    dataset As ADM1RegressionDataset,
                                    targets As Dictionary(Of String, Double()),
                                    weights As Dictionary(Of String, Double),
                                    tEnd As Double) As Double
            Dim traj = ADM1Integrator.Integrate(state0.Clone(), p, q, Sin, 0.0, tEnd,
                                                recordTrajectory:=True, sampleInterval:=-1.0,
                                                maxSamples:=1000)
            If traj Is Nothing OrElse traj.Times Is Nothing OrElse traj.Times.Count < 2 Then Return 1.0E+30
            Dim tArr = dataset.Times.ToArray()
            Dim ssr As Double = 0.0
            For Each kv In dataset.Observations
                Dim obs = kv.Key
                Dim w As Double = 1.0
                If weights.ContainsKey(obs) Then w = weights(obs)
                Dim sim = InterpolateSeries(traj, obs, tArr)
                Dim tgt = targets(obs)
                For i = 0 To tgt.Length - 1
                    If Double.IsNaN(tgt(i)) OrElse Double.IsNaN(sim(i)) Then Continue For
                    Dim d = sim(i) - tgt(i)
                    ssr += w * d * d
                Next
            Next
            Return ssr
        End Function

        ''' <summary>Linear interpolation of one series from a trajectory at specified times.</summary>
        Public Function InterpolateSeries(traj As ADM1TrajectoryResult, seriesName As String, times As Double()) As Double()
            Dim n = times.Length
            Dim y(n - 1) As Double
            If traj Is Nothing OrElse traj.Times Is Nothing OrElse traj.Times.Count = 0 Then
                For i = 0 To n - 1 : y(i) = Double.NaN : Next
                Return y
            End If
            Dim ts = traj.Times.ToArray()
            Dim ys = traj.GetSeries(seriesName)
            For i = 0 To n - 1
                Dim t = times(i)
                If t <= ts(0) Then
                    y(i) = ys(0)
                ElseIf t >= ts(ts.Length - 1) Then
                    y(i) = ys(ys.Length - 1)
                Else
                    ' binary search
                    Dim lo = 0, hi = ts.Length - 1
                    While hi - lo > 1
                        Dim mid = (lo + hi) \ 2
                        If ts(mid) <= t Then lo = mid Else hi = mid
                    End While
                    Dim alpha = (t - ts(lo)) / Max(ts(hi) - ts(lo), 0.00000000000001)
                    y(i) = ys(lo) + alpha * (ys(hi) - ys(lo))
                End If
            Next
            Return y
        End Function

        Private Function CloneParams(p As ADM1Parameters) As ADM1Parameters
            If p Is Nothing Then Return New ADM1Parameters()
            Try
                Return ADM1Parameters.FromJSON(p.ToJSON())
            Catch
                Return New ADM1Parameters()
            End Try
        End Function

    End Module

End Namespace
