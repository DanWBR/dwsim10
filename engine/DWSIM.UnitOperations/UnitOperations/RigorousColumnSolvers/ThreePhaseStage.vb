'    Three-phase (vapor-liquid-liquid) stages of the rigorous column
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
Imports DWSIM.Thermodynamics.PropertyPackages

Namespace UnitOperations.Auxiliary.SepOps.SolvingMethods

    ''' <summary>
    ''' Vapor-liquid-liquid equilibrium on a column stage. The solvers keep one liquid per stage, the total liquid; on a
    ''' three-phase stage the K-values are apparent ones, K = y/x with x the total liquid: the liquid splits into two phases
    ''' (stability test on the tangent plane distance, then successive substitution with the Rachford-Rice balance), the
    ''' vapor is in equilibrium with either of them, and K_i = K_i(x1).x1_i/x_i. The stage temperature of a bubble point
    ''' method comes from the same apparent K-values. A stage whose liquid doesn't split takes the usual K-values.
    ''' </summary>
    Public NotInheritable Class ThreePhaseStage

        Private Sub New()
        End Sub

        ''' <summary>What the last call found on a stage: the liquid it tested and, for a split, the second liquid.</summary>
        Friend NotInheritable Class StageState
            Public Z As Double()
            Public T As Double
            Public X1 As Double()   ' the two liquids of a split
            Public X2 As Double()   ' Nothing when the liquid was stable
            Public W As Double()    ' stable liquid: the stationary point of the trial nearest to instability
        End Class

        ''' <summary>
        ''' State of each stage through a solution of the column. A stage found stable keeps that answer while its
        ''' liquid stays within 1e-4 (mole fraction) and 0.05 K of the one tested, which spares the stability test in the
        ''' perturbations of a Jacobian; within 0.02 and 3 K it is tested again from the stationary point of its nearest
        ''' trial only. A stage that split starts the next split from its two liquids.
        ''' </summary>
        Public Class SplitCache
            Friend ReadOnly States As New ConcurrentDictionary(Of Integer, StageState)
        End Class

        ''' <summary>Apparent K-values of the total liquid <paramref name="x"/> of a stage.</summary>
        Public Shared Function Kvalues(pp As PropertyPackage, x As Double(), y As Double(), T As Double, P As Double,
                                       Optional stage As Integer = -1, Optional cache As SplitCache = Nothing) As Double()
            Dim x1 = LiquidSplit(pp, x, T, P, stage, cache)
            If x1 Is Nothing Then Return pp.DW_CalcKvalue(x, y, T, P)
            Dim K1 = pp.DW_CalcKvalue(x1, y, T, P)
            Dim n = x.Length
            Dim K(n - 1) As Double
            For i = 0 To n - 1
                K(i) = If(x(i) > 1.0E-30, K1(i) * x1(i) / x(i), K1(i))
                If Double.IsNaN(K(i)) OrElse Double.IsInfinity(K(i)) Then K(i) = K1(i)
            Next
            Return K
        End Function

        ''' <summary>
        ''' Bubble point temperature of the total liquid <paramref name="x"/> at <paramref name="P"/> from the apparent
        ''' K-values (sum of K.x = 1), by the secant method on ln(sum K.x) starting at <paramref name="Tguess"/>.
        ''' Returns the temperature and the apparent K-values at it.
        ''' </summary>
        Public Shared Function BubbleTemperature(pp As PropertyPackage, x As Double(), P As Double, Tguess As Double,
                                                 ByRef K As Double(), Optional stage As Integer = -1, Optional cache As SplitCache = Nothing) As Double
            Dim y = DirectCast(x.Clone(), Double())
            Dim Klast As Double() = Nothing
            Dim f = Function(T As Double) As Double
                        Dim Kt = Kvalues(pp, x, y, T, P, stage, cache)
                        Klast = Kt
                        Dim s As Double = 0.0
                        For i = 0 To x.Length - 1
                            s += Kt(i) * x(i)
                        Next
                        If s > 0.0 Then
                            For i = 0 To x.Length - 1
                                y(i) = Kt(i) * x(i) / s
                            Next
                        End If
                        Return Math.Log(Math.Max(s, 1.0E-300))
                    End Function
            Dim T0 = Tguess, f0 = f(T0)
            Dim T1 = T0 + If(f0 > 0, -1.0, 1.0), f1 = f(T1)
            For it = 1 To 60
                If Math.Abs(f1) < 1.0E-9 Then Exit For
                Dim dT As Double
                If Math.Abs(f1 - f0) < 1.0E-14 Then
                    dT = If(f1 > 0, -1.0, 1.0)
                Else
                    dT = -f1 * (T1 - T0) / (f1 - f0)
                End If
                dT = Math.Max(-20.0, Math.Min(20.0, dT))
                T0 = T1 : f0 = f1
                T1 = Math.Max(150.0, T1 + dT)
                f1 = f(T1)
                If Math.Abs(dT) < 0.00001 Then Exit For
            Next
            K = Klast
            Return T1
        End Function

        ''' <summary>
        ''' Composition of one of the two liquids that <paramref name="z"/> splits into at T and P, or Nothing when the
        ''' liquid is stable. Liquid fugacity coefficients of the package; trial phases near each pure compound of the
        ''' liquid and the last split of the stage.
        ''' </summary>
        Public Shared Function LiquidSplit(pp As PropertyPackage, z As Double(), T As Double, P As Double,
                                           Optional stage As Integer = -1, Optional cache As SplitCache = Nothing) As Double()
            Dim n = z.Length
            Dim zs = z.Sum()
            If zs <= 0.0 Then Return Nothing
            Dim zn(n - 1) As Double
            Dim present As Integer = 0
            For i = 0 To n - 1
                zn(i) = z(i) / zs
                If zn(i) > 1.0E-10 Then present += 1
            Next
            If present < 2 Then Return Nothing

            Dim lnphi = Function(x As Double()) As Double()
                            Dim phi = pp.DW_CalcFugCoeff(x, T, P, State.Liquid)
                            Dim r(n - 1) As Double
                            For i = 0 To n - 1
                                r(i) = Math.Log(Math.Max(phi(i), 1.0E-300))
                            Next
                            Return r
                        End Function

            Dim last As StageState = Nothing
            Dim dz As Double = Double.MaxValue
            If cache IsNot Nothing AndAlso stage >= 0 AndAlso cache.States.TryGetValue(stage, last) AndAlso last.Z.Length = n Then
                dz = 0.0
                For i = 0 To n - 1
                    dz = Math.Max(dz, Math.Abs(last.Z(i) - zn(i)))
                Next
                If last.X2 Is Nothing AndAlso Math.Abs(last.T - T) < 0.05 AndAlso dz <= 0.0001 Then
                    Return Nothing
                End If
            Else
                last = Nothing
            End If

            ' a stage that split: try the split again from its two liquids first
            If last IsNot Nothing AndAlso last.X2 IsNot Nothing Then
                Dim again = SplitFrom(lnphi, zn, DirectCast(last.X1.Clone(), Double()), DirectCast(last.X2.Clone(), Double()), n)
                If again IsNot Nothing Then
                    If cache IsNot Nothing Then cache.States(stage) = New StageState With {.Z = zn, .T = T, .X1 = again.Item1, .X2 = again.Item2}
                    Return again.Item1
                End If
            End If

            Dim lnphiz = lnphi(zn)
            Dim d(n - 1) As Double
            For i = 0 To n - 1
                d(i) = If(zn(i) > 0.0, Math.Log(zn(i)) + lnphiz(i), -1.0E+300)
            Next

            ' stability: a stable stage that moved a little is tested from its last stationary point only; otherwise
            ' trial phases near each pure compound
            Dim trials As New List(Of Double())
            Dim shortTest = last IsNot Nothing AndAlso last.X2 Is Nothing AndAlso last.W IsNot Nothing AndAlso dz <= 0.02 AndAlso Math.Abs(last.T - T) < 3.0
            If shortTest Then trials.Add(last.W)
            For k = 0 To n - 1
                If shortTest Then Exit For
                If zn(k) <= 1.0E-10 Then Continue For
                Dim w(n - 1) As Double
                For i = 0 To n - 1
                    w(i) = If(zn(i) > 0.0, If(i = k, 0.98, 0.02 * zn(i) / Math.Max(1.0E-30, 1.0 - zn(k))), 0.0)
                Next
                trials.Add(w)
            Next

            Dim x2 As Double() = Nothing
            Dim nearest As Double() = Nothing, nearestSum As Double = Double.MinValue
            For Each w0 In trials
                Dim W = DirectCast(w0.Clone(), Double())
                Dim unstable = False
                For it = 1 To 40
                    Dim s = W.Sum()
                    If s <= 0.0 Then Exit For
                    Dim wn(n - 1) As Double
                    For i = 0 To n - 1
                        wn(i) = W(i) / s
                    Next
                    Dim lw = lnphi(wn)
                    Dim change As Double = 0.0
                    For i = 0 To n - 1
                        If zn(i) <= 0.0 Then Continue For
                        Dim wi = Math.Exp(d(i) - lw(i))
                        change += Math.Abs(wi - W(i))
                        W(i) = wi
                    Next
                    If change < 1.0E-8 Then Exit For
                Next
                Dim sw = W.Sum()
                If sw > nearestSum AndAlso sw > 0.0 Then
                    ' the trial nearest to instability, unless it collapsed onto the feed
                    Dim dfeed As Double = 0.0
                    For i = 0 To n - 1
                        dfeed += Math.Abs(W(i) / sw - zn(i))
                    Next
                    If dfeed > 0.001 Then nearestSum = sw : nearest = W.Select(Function(v) v / sw).ToArray()
                End If
                If sw > 1.0 + 1.0E-6 Then
                    ' a trial that collapsed onto the feed is not a second phase
                    Dim dist As Double = 0.0
                    For i = 0 To n - 1
                        dist += Math.Abs(W(i) / sw - zn(i))
                    Next
                    If dist > 0.001 Then
                        unstable = True
                        x2 = W.Select(Function(v) v / sw).ToArray()
                    End If
                End If
                If unstable Then Exit For
            Next
            If x2 Is Nothing Then
                If cache IsNot Nothing AndAlso stage >= 0 Then cache.States(stage) = New StageState With {.Z = zn, .T = T, .W = nearest}
                Return Nothing
            End If

            Dim found = SplitFrom(lnphi, zn, DirectCast(zn.Clone(), Double()), x2, n)
            If cache IsNot Nothing AndAlso stage >= 0 Then
                If found Is Nothing Then
                    cache.States(stage) = New StageState With {.Z = zn, .T = T, .W = nearest}
                Else
                    cache.States(stage) = New StageState With {.Z = zn, .T = T, .X1 = found.Item1, .X2 = found.Item2}
                End If
            End If
            Return If(found Is Nothing, Nothing, found.Item1)

        End Function

        ''' <summary>
        ''' Successive substitution on the two liquids from <paramref name="x1"/> and <paramref name="x2"/>; the two liquids,
        ''' or Nothing when the split vanishes (trivial solution or a phase without amount).
        ''' </summary>
        Private Shared Function SplitFrom(lnphi As Func(Of Double(), Double()), zn As Double(), x1 As Double(), x2 As Double(), n As Integer) As Tuple(Of Double(), Double())

            ' split: K2_i = x2_i/x1_i = phi1_i/phi2_i, Rachford-Rice on the fraction of liquid 2
            Dim beta As Double = 0.5
            For it = 1 To 300
                Dim l1 = lnphi(x1), l2 = lnphi(x2)
                Dim Kl(n - 1) As Double
                For i = 0 To n - 1
                    Kl(i) = Math.Exp(l1(i) - l2(i))
                Next
                Dim ok As Boolean
                beta = RachfordRice(zn, Kl, beta, ok)
                If Not ok Then Return Nothing
                Dim change As Double = 0.0
                Dim s1 As Double = 0.0, s2 As Double = 0.0
                Dim nx1(n - 1) As Double, nx2(n - 1) As Double
                For i = 0 To n - 1
                    nx1(i) = zn(i) / (1.0 + beta * (Kl(i) - 1.0))
                    nx2(i) = Kl(i) * nx1(i)
                    s1 += nx1(i) : s2 += nx2(i)
                Next
                For i = 0 To n - 1
                    nx1(i) /= s1 : nx2(i) /= s2
                    change += Math.Abs(nx1(i) - x1(i)) + Math.Abs(nx2(i) - x2(i))
                Next
                x1 = nx1 : x2 = nx2
                If change < 1.0E-8 Then Exit For
            Next

            ' trivial solution or a phase that vanished: one liquid
            Dim diff As Double = 0.0
            For i = 0 To n - 1
                diff += Math.Abs(x1(i) - x2(i))
            Next
            If diff < 0.001 OrElse beta <= 1.0E-8 OrElse beta >= 1.0 - 1.0E-8 Then Return Nothing
            Return Tuple.Create(x1, x2)

        End Function

        ''' <summary>Fraction of the second phase for the K-values of phase 2 over phase 1; False when no root lies in (0, 1).</summary>
        ''' <summary>Vapor fraction of <paramref name="z"/> for the K-values <paramref name="K"/>; ok is False when
        ''' the K-values are all above or all below one (or the root is outside 0..1).</summary>
        Friend Shared Function RachfordRiceFraction(z As Double(), K As Double(), guess As Double, ByRef ok As Boolean) As Double
            Return RachfordRice(z, K, guess, ok)
        End Function

        ''' <summary>The second liquid of the last split found on a stage, or Nothing.</summary>
        Friend Shared Function SecondLiquid(cache As SplitCache, stage As Integer) As Double()
            Dim st As StageState = Nothing
            If cache Is Nothing OrElse Not cache.States.TryGetValue(stage, st) Then Return Nothing
            Return st.X2
        End Function

        Private Shared Function RachfordRice(z As Double(), K As Double(), guess As Double, ByRef ok As Boolean) As Double
            Dim n = z.Length
            Dim g = Function(b As Double) As Double
                        Dim s As Double = 0.0
                        For i = 0 To n - 1
                            If z(i) > 0.0 Then s += z(i) * (K(i) - 1.0) / (1.0 + b * (K(i) - 1.0))
                        Next
                        Return s
                    End Function
            Dim kmin = Double.MaxValue, kmax = Double.MinValue
            For i = 0 To n - 1
                If z(i) <= 0.0 Then Continue For
                kmin = Math.Min(kmin, K(i)) : kmax = Math.Max(kmax, K(i))
            Next
            If kmin >= 1.0 OrElse kmax <= 1.0 Then
                ok = False
                Return guess
            End If
            Dim lo = Math.Max(0.0, 1.0 / (1.0 - kmax)) + 1.0E-12, hi = Math.Min(1.0, 1.0 / (1.0 - kmin)) - 1.0E-12
            Dim glo = g(lo), ghi = g(hi)
            If glo < 0.0 OrElse ghi > 0.0 Then
                ok = False
                Return guess
            End If
            For it = 1 To 100
                Dim mid = 0.5 * (lo + hi), gm = g(mid)
                If gm > 0.0 Then lo = mid Else hi = mid
                If hi - lo < 1.0E-13 Then Exit For
            Next
            ok = True
            Return 0.5 * (lo + hi)
        End Function

    End Class

End Namespace
