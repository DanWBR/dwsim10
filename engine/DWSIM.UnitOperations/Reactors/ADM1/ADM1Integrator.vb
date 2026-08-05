'    Full ADM1 - Cash-Karp RK45 adaptive integrator with algebraic pH solve.
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Math

Namespace Reactors.ADM1

    ''' <summary>
    ''' Cash-Karp embedded RK45 adaptive-step ODE integrator for the ADM1 29-dim system.
    ''' The algebraic pH (charge-balance) is solved inside ComputeDerivatives at every stage.
    ''' Sampling is decoupled from the adaptive step: samples are emitted at fixed sampleInterval
    ''' via piecewise-linear interpolation between accepted steps (adequate for reporting).
    ''' </summary>
    Public Module ADM1Integrator

        ' Cash-Karp coefficients
        Private ReadOnly A2 As Double = 1.0 / 5.0
        Private ReadOnly A3 As Double = 3.0 / 10.0
        Private ReadOnly A4 As Double = 3.0 / 5.0
        Private ReadOnly A5 As Double = 1.0
        Private ReadOnly A6 As Double = 7.0 / 8.0

        Private ReadOnly B21 As Double = 1.0 / 5.0
        Private ReadOnly B31 As Double = 3.0 / 40.0, B32 As Double = 9.0 / 40.0
        Private ReadOnly B41 As Double = 3.0 / 10.0, B42 As Double = -9.0 / 10.0, B43 As Double = 6.0 / 5.0
        Private ReadOnly B51 As Double = -11.0 / 54.0, B52 As Double = 5.0 / 2.0, B53 As Double = -70.0 / 27.0, B54 As Double = 35.0 / 27.0
        Private ReadOnly B61 As Double = 1631.0 / 55296.0, B62 As Double = 175.0 / 512.0, B63 As Double = 575.0 / 13824.0, B64 As Double = 44275.0 / 110592.0, B65 As Double = 253.0 / 4096.0

        Private ReadOnly C1 As Double = 37.0 / 378.0
        Private ReadOnly C3 As Double = 250.0 / 621.0
        Private ReadOnly C4 As Double = 125.0 / 594.0
        Private ReadOnly C6 As Double = 512.0 / 1771.0

        Private ReadOnly DC1 As Double = C1 - 2825.0 / 27648.0
        Private ReadOnly DC3 As Double = C3 - 18575.0 / 48384.0
        Private ReadOnly DC4 As Double = C4 - 13525.0 / 55296.0
        Private ReadOnly DC5 As Double = -277.0 / 14336.0
        Private ReadOnly DC6 As Double = C6 - 0.25

        ''' <summary>
        ''' Integrate the ADM1 system from state0 over tSpan (days).
        ''' q: influent volumetric flow (m³/d). Sin: 29-vector of influent concentrations in state order.
        ''' </summary>
        Public Function Integrate(state0 As ADM1State,
                                  p As ADM1Parameters,
                                  q As Double,
                                  Sin As Double(),
                                  tStart As Double,
                                  tEnd As Double,
                                  Optional tolRel As Double = 0.000001,
                                  Optional tolAbs As Double = 0.00000001,
                                  Optional recordTrajectory As Boolean = True,
                                  Optional sampleInterval As Double = -1.0,
                                  Optional maxSamples As Integer = 2000,
                                  Optional progressCallback As Action(Of Double) = Nothing) As ADM1TrajectoryResult

            Dim res As New ADM1TrajectoryResult()
            res.Parameters = p

            If sampleInterval <= 0.0 Then
                sampleInterval = (tEnd - tStart) / 500.0
                Dim minInterval = (tEnd - tStart) / Max(maxSamples, 1)
                sampleInterval = Max(sampleInterval, minInterval)
            End If

            Dim s = state0.Clone()
            ADM1Equations.RefreshAlgebraicStates(s, p, q, Sin)
            Dim t = tStart
            Dim y = s.ToVector()
            Dim nvar = y.Length

            If recordTrajectory Then
                res.Times.Add(t)
                res.States.Add(s.Clone())
            End If
            Dim nextSample = tStart + sampleInterval

            Dim h = Min(sampleInterval, (tEnd - tStart) / 100.0)
            Dim hmin = 1.0E-08
            Dim hmax = (tEnd - tStart) / 20.0
            If hmax <= 0 Then hmax = 1.0

            Dim k1(nvar - 1), k2(nvar - 1), k3(nvar - 1), k4(nvar - 1), k5(nvar - 1), k6(nvar - 1) As Double
            Dim ytmp(nvar - 1), yerr(nvar - 1), ynew(nvar - 1) As Double
            Dim sTmp As ADM1State = state0.Clone()
            Dim yPrev = CDbl_Copy(y)
            Dim tPrev = t

            Dim steps = 0
            Dim forcedSteps = 0
            Dim maxSteps = Max(p.Numerics.MaxSteps, 1)
            Dim outOfBudget = False
            Do While t < tEnd - 1.0E-12
                steps += 1
                If steps > maxSteps Then
                    outOfBudget = True
                    Exit Do
                End If

                If t + h > tEnd Then h = tEnd - t

                ' k1
                sTmp.FromVector(y)
                k1 = ADM1Equations.ComputeDerivatives(sTmp, p, q, Sin)
                ' k2
                For i = 0 To nvar - 1 : ytmp(i) = y(i) + h * B21 * k1(i) : Next
                sTmp.FromVector(ytmp)
                k2 = ADM1Equations.ComputeDerivatives(sTmp, p, q, Sin)
                ' k3
                For i = 0 To nvar - 1 : ytmp(i) = y(i) + h * (B31 * k1(i) + B32 * k2(i)) : Next
                sTmp.FromVector(ytmp)
                k3 = ADM1Equations.ComputeDerivatives(sTmp, p, q, Sin)
                ' k4
                For i = 0 To nvar - 1 : ytmp(i) = y(i) + h * (B41 * k1(i) + B42 * k2(i) + B43 * k3(i)) : Next
                sTmp.FromVector(ytmp)
                k4 = ADM1Equations.ComputeDerivatives(sTmp, p, q, Sin)
                ' k5
                For i = 0 To nvar - 1 : ytmp(i) = y(i) + h * (B51 * k1(i) + B52 * k2(i) + B53 * k3(i) + B54 * k4(i)) : Next
                sTmp.FromVector(ytmp)
                k5 = ADM1Equations.ComputeDerivatives(sTmp, p, q, Sin)
                ' k6
                For i = 0 To nvar - 1 : ytmp(i) = y(i) + h * (B61 * k1(i) + B62 * k2(i) + B63 * k3(i) + B64 * k4(i) + B65 * k5(i)) : Next
                sTmp.FromVector(ytmp)
                k6 = ADM1Equations.ComputeDerivatives(sTmp, p, q, Sin)

                ' 5th-order result
                For i = 0 To nvar - 1
                    ynew(i) = y(i) + h * (C1 * k1(i) + C3 * k3(i) + C4 * k4(i) + C6 * k6(i))
                    yerr(i) = h * (DC1 * k1(i) + DC3 * k3(i) + DC4 * k4(i) + DC5 * k5(i) + DC6 * k6(i))
                Next

                ' Error norm
                Dim errMax As Double = 0.0
                For i = 0 To nvar - 1
                    Dim sc = tolAbs + tolRel * Max(Abs(y(i)), Abs(ynew(i)))
                    Dim e = Abs(yerr(i)) / Max(sc, 1.0E-30)
                    If e > errMax Then errMax = e
                Next

                If errMax <= 1.0 OrElse h <= hmin Then
                    ' Accept. A step taken at hmin failed the error test and is being forced through
                    ' anyway; that is a loss of accuracy the caller has to be told about.
                    If errMax > 1.0 Then forcedSteps += 1
                    tPrev = t
                    yPrev = CDbl_Copy(y)
                    t += h
                    y = CDbl_Copy(ynew)

                    ' Enforce non-negativity on concentration states
                    For i = 0 To nvar - 1
                        If y(i) < 0.0 Then y(i) = 0.0
                    Next

                    ' S_h2 has no ODE in algebraic mode, so the RK step leaves y(7) at its old value:
                    ' re-solve it against the state just accepted, or every sample and the next step's
                    ' initial guess carry the value from tStart.
                    If p.Numerics.AlgebraicH2 Then
                        sTmp.FromVector(y)
                        ADM1Equations.SolvePH(sTmp, p)
                        y(7) = ADM1Equations.SolveH2QSS(sTmp, p, q, Sin)
                    End If

                    ' Emit samples via linear interp between (tPrev,yPrev) and (t,y)
                    If recordTrajectory Then
                        While nextSample <= t + 1.0E-12 AndAlso nextSample <= tEnd + 1.0E-12
                            Dim alpha = If(t - tPrev > 0, (nextSample - tPrev) / (t - tPrev), 1.0)
                            Dim yi(nvar - 1) As Double
                            For i = 0 To nvar - 1 : yi(i) = yPrev(i) + alpha * (y(i) - yPrev(i)) : Next
                            Dim si As New ADM1State()
                            si.FromVector(yi)
                            ADM1Equations.RefreshAlgebraicStates(si, p, q, Sin)
                            res.Times.Add(nextSample)
                            res.States.Add(si)
                            nextSample += sampleInterval
                            If res.States.Count >= maxSamples Then
                                sampleInterval = sampleInterval * 2.0
                            End If
                        End While
                    End If

                    progressCallback?.Invoke((t - tStart) / Max(tEnd - tStart, 1.0E-09))

                    ' Adapt step up
                    If errMax = 0.0 Then
                        h = Min(h * 5.0, hmax)
                    Else
                        h = Min(h * 0.9 * Pow(errMax, -0.2), hmax)
                    End If
                Else
                    ' Reject, shrink step
                    h = Max(h * 0.9 * Pow(errMax, -0.25), hmin)
                End If
            Loop

            ' Final state
            Dim sFinal As New ADM1State()
            sFinal.FromVector(y)
            ADM1Equations.RefreshAlgebraicStates(sFinal, p, q, Sin)
            res.FinalState = sFinal

            res.ReachedTime_d = t
            res.Steps = steps
            res.Converged = Not outOfBudget AndAlso t >= tEnd - 1.0E-09

            ' How far from a standing state the answer is, which is what the digester model actually
            ' wants from a long run and is not implied by having reached tEnd.
            Dim dFinal = ADM1Equations.ComputeDerivatives(sFinal.Clone(), p, q, Sin)
            Dim ssr As Double = 0.0
            For i = 0 To nvar - 1
                Dim rel = Abs(dFinal(i)) / Max(Abs(y(i)), 0.000001)
                If rel > ssr Then ssr = rel
            Next
            res.SteadyStateResidual_perDay = ssr

            If Not res.Converged Then
                ' Note the step size rather than the tolerance: a step this small is a stability
                ' limit, not an accuracy one, and loosening the tolerance would not move it.
                res.StopReason = String.Format(CultureInfo.InvariantCulture,
                    "The ADM1 integration ran out of step budget at t = {0:G6} d of the {1:G6} d requested " &
                    "({2} steps, last step {3:G4} d). Some mode of the system is relaxing far faster than " &
                    "the explicit RK45 can step over.{4}", t, tEnd, steps, h,
                    If(p.Numerics.AlgebraicH2, "",
                       " Numerics.AlgebraicH2 is off; turning it on removes the fastest mode (dissolved H2)."))
            ElseIf forcedSteps > 0 Then
                res.StopReason = String.Format(CultureInfo.InvariantCulture,
                    "The ADM1 integration reached {0:G6} d but had to force {1} of {2} steps through at the " &
                    "minimum step size ({3:G4} d) after they failed the error test, so the trajectory is " &
                    "not accurate to the requested tolerance.", t, forcedSteps, steps, hmin)
            End If

            If recordTrajectory Then
                ' Close the trajectory at the time actually reached. Stamping tEnd here regardless of
                ' where the loop stopped - as this used to - is what let a run that died at 3.8 d of
                ' 200 d be read back as a completed one.
                If res.Times.Count = 0 OrElse res.Times(res.Times.Count - 1) < t - 1.0E-09 Then
                    res.Times.Add(t)
                    res.States.Add(sFinal.Clone())
                End If
            End If

            Return res
        End Function

        Private Function CDbl_Copy(a As Double()) As Double()
            Dim b(a.Length - 1) As Double
            Array.Copy(a, b, a.Length)
            Return b
        End Function

    End Module

End Namespace
