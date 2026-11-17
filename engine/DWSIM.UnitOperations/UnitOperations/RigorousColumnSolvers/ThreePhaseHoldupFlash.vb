'    Vapor-liquid-liquid flash of the holdup of a three-phase column stage (dynamic model)
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

Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms

Namespace UnitOperations.Auxiliary.SepOps.SolvingMethods

    ''' <summary>
    ''' Flash of the holdup of a three-phase column stage: the flash of the property package gives the vapor and a
    ''' single liquid; when that liquid is unstable, the vapor is put in equilibrium with the total liquid through the
    ''' apparent K-values of the steady-state solvers (K of the first liquid times x1/x) and the liquid is reported as
    ''' its two phases. The pressure-enthalpy flash finds the temperature at which the enthalpy of the vapor and of
    ''' the two liquids matches the one asked for. Stable holdups get the result of the package's flash unchanged.
    ''' </summary>
    Friend NotInheritable Class ThreePhaseHoldupFlash

        Inherits FlashAlgorithm

        Private ReadOnly _base As FlashAlgorithm
        Private ReadOnly _stage As Integer
        Private ReadOnly _cache As ThreePhaseStage.SplitCache

        Sub New(baseFlash As FlashAlgorithm, stage As Integer, cache As ThreePhaseStage.SplitCache)
            MyBase.New()
            _base = baseFlash
            _stage = stage
            _cache = cache
            FlashSettings = baseFlash.FlashSettings
        End Sub

        Public Overrides ReadOnly Property AlgoType As Interfaces.Enums.FlashMethod
            Get
                Return Interfaces.Enums.FlashMethod.Custom
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String = "Vapor-liquid-liquid flash of a three-phase column stage"

        Public Overrides ReadOnly Property Name As String = "Three-Phase Stage Holdup Flash"

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function Flash_PT(Vz As Double(), P As Double, T As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = False, Optional PrevKi As Double() = Nothing) As Object
            Dim r = DirectCast(_base.Flash_PT(Vz, P, T, PP, ReuseKI, PrevKi), Object())
            Dim three = Split(r, Vz, T, P, PP)
            Return If(three, r)
        End Function

        Public Overrides Function Flash_PH(Vz As Double(), P As Double, H As Double, Tref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = False, Optional PrevKi As Double() = Nothing) As Object

            ' the package's flash first: a stable liquid keeps its answer, and its temperature starts the search
            Dim r = DirectCast(_base.Flash_PH(Vz, P, H, Tref, PP, ReuseKI, PrevKi), Object())
            Dim T0 As Double = Convert.ToDouble(r(4))
            If Not T0 > 0.0 OrElse Double.IsNaN(T0) Then T0 = If(Tref > 0, Tref, 350.0)
            Dim first = Split(DirectCast(_base.Flash_PT(Vz, P, T0, PP), Object()), Vz, T0, P, PP)
            If first Is Nothing Then Return r

            ' secant on the temperature, bracketed once the error changes sign
            Dim res0 As Object() = first
            Dim f0 = MixtureEnthalpy(first, T0, P, PP) - H
            Dim res1 As Object() = Nothing
            Dim T1 = T0 + If(f0 > 0, -0.5, 0.5)
            Dim f1 = EnthalpyError(Vz, P, H, T1, PP, res1)
            Dim lo = Double.NaN, hi = Double.NaN
            If f0 * f1 < 0 Then lo = Math.Min(T0, T1) : hi = Math.Max(T0, T1)
            Dim tol = 0.0001 * Math.Max(1.0, Math.Abs(H))
            For it = 1 To 50
                If Math.Abs(f1) < tol Then Exit For
                Dim Tn As Double
                If Math.Abs(f1 - f0) > 1.0E-14 Then
                    Tn = T1 - f1 * (T1 - T0) / (f1 - f0)
                Else
                    Tn = T1 + If(f1 > 0, -1.0, 1.0)
                End If
                Tn = Math.Max(T1 - 10.0, Math.Min(T1 + 10.0, Tn))
                If Not Double.IsNaN(lo) AndAlso (Tn <= lo OrElse Tn >= hi) Then Tn = 0.5 * (lo + hi)
                Dim resn As Object() = Nothing
                Dim fn = EnthalpyError(Vz, P, H, Tn, PP, resn)
                If Not Double.IsNaN(lo) Then
                    If fn > 0 Then hi = Tn Else lo = Tn
                ElseIf fn * f1 < 0 Then
                    lo = Math.Min(Tn, T1) : hi = Math.Max(Tn, T1)
                End If
                T0 = T1 : f0 = f1
                T1 = Tn : f1 = fn : res1 = resn
                If Math.Abs(T1 - T0) < 0.00001 Then Exit For
            Next

            Dim n = Vz.Length
            Dim K(n - 1) As Double
            Dim x1 = DirectCast(res1(2), Double()), y = DirectCast(res1(3), Double())
            For i = 0 To n - 1
                K(i) = If(x1(i) > 0, y(i) / x1(i), 1.0)
            Next
            Return New Object() {res1(0), res1(1), res1(2), res1(3), T1, res1(4), K, res1(5), res1(6), 0.0, PP.RET_NullVector()}

        End Function

        ''' <summary>Enthalpy of the flash at T minus the one asked for; the flash result goes to <paramref name="res"/>.</summary>
        Private Function EnthalpyError(Vz As Double(), P As Double, H As Double, T As Double, PP As PropertyPackage, ByRef res As Object()) As Double
            Dim rb = DirectCast(_base.Flash_PT(Vz, P, T, PP), Object())
            res = If(Split(rb, Vz, T, P, PP), rb)
            Return MixtureEnthalpy(res, T, P, PP) - H
        End Function

        ''' <summary>
        ''' The vapor-liquid-liquid result at T and P from a vapor-liquid one, in the layout of a pressure-temperature
        ''' flash, or Nothing when the liquid does not split (or there is not any liquid).
        ''' </summary>
        Private Function Split(r As Object(), Vz As Double(), T As Double, P As Double, PP As PropertyPackage) As Object()

            Dim n = Vz.Length
            Dim L = Convert.ToDouble(r(0)) + Convert.ToDouble(r(5))
            Dim V = Convert.ToDouble(r(1))
            If L <= 1.0E-10 Then Return Nothing
            Dim x(n - 1) As Double, y = DirectCast(DirectCast(r(3), Double()).Clone(), Double())
            Dim xa = DirectCast(r(2), Double()), xb = DirectCast(r(6), Double())
            Dim la = Convert.ToDouble(r(0)), lb = Convert.ToDouble(r(5))
            For i = 0 To n - 1
                x(i) = (la * xa(i) + lb * If(xb IsNot Nothing AndAlso xb.Length = n, xb(i), 0.0)) / L
            Next

            If ThreePhaseStage.LiquidSplit(PP, x, T, P, _stage, _cache) Is Nothing Then Return Nothing

            ' the vapor in equilibrium with the total liquid through the apparent K-values
            Dim Kapp As Double() = Nothing
            For it = 1 To 50
                If V <= 0.0 Then
                    For i = 0 To n - 1
                        y(i) = x(i)
                    Next
                End If
                Kapp = ThreePhaseStage.Kvalues(PP, x, y, T, P, _stage, _cache)
                Dim ok As Boolean
                Dim Vn = ThreePhaseStage.RachfordRiceFraction(Vz, Kapp, V, ok)
                If Not ok Then
                    Dim sk As Double = 0.0
                    For i = 0 To n - 1
                        sk += Vz(i) * Kapp(i)
                    Next
                    If sk <= 1.0 Then Vn = 0.0 Else Return Nothing ' above the dew point: the package's vapor stands
                End If
                Dim change As Double = 0.0
                Dim xs As Double = 0.0, ys As Double = 0.0
                Dim xn(n - 1) As Double, yn(n - 1) As Double
                For i = 0 To n - 1
                    xn(i) = Vz(i) / (1.0 + Vn * (Kapp(i) - 1.0))
                    yn(i) = Kapp(i) * xn(i)
                    xs += xn(i) : ys += yn(i)
                Next
                For i = 0 To n - 1
                    xn(i) /= xs
                    If ys > 0 Then yn(i) /= ys
                    change += Math.Abs(xn(i) - x(i))
                Next
                x = xn : y = yn
                change += Math.Abs(Vn - V)
                V = Vn
                If change < 1.0E-8 Then Exit For
            Next

            ' the two liquids of the total liquid
            Dim x1 = ThreePhaseStage.LiquidSplit(PP, x, T, P, _stage, _cache)
            If x1 Is Nothing Then Return Nothing
            Dim x2 = ThreePhaseStage.SecondLiquid(_cache, _stage)
            If x2 Is Nothing OrElse x2.Length <> n Then Return Nothing
            ' fraction of the liquid in the second phase from the compound that differs the most between the two
            Dim jmax = 0
            For i = 1 To n - 1
                If Math.Abs(x2(i) - x1(i)) > Math.Abs(x2(jmax) - x1(jmax)) Then jmax = i
            Next
            Dim b = (x(jmax) - x1(jmax)) / (x2(jmax) - x1(jmax))
            b = Math.Max(0.0, Math.Min(1.0, b))
            Dim Ltot = 1.0 - V
            Dim L1 = Ltot * (1.0 - b), L2 = Ltot * b
            If L2 > L1 Then
                Dim tmp = x1 : x1 = x2 : x2 = tmp
                Dim tl = L1 : L1 = L2 : L2 = tl
            End If
            Return New Object() {L1, V, x1, y, 0, L2, x2, 0.0, PP.RET_NullVector(), Kapp}

        End Function

        ''' <summary>Mass enthalpy of the phases of a result (vapor, first and second liquid), kJ/kg.</summary>
        Private Shared Function MixtureEnthalpy(r As Object(), T As Double, P As Double, PP As PropertyPackage) As Double
            Dim parts = {Tuple.Create(Convert.ToDouble(r(1)), DirectCast(r(3), Double()), State.Vapor),
                         Tuple.Create(Convert.ToDouble(r(0)), DirectCast(r(2), Double()), State.Liquid),
                         Tuple.Create(Convert.ToDouble(r(5)), DirectCast(r(6), Double()), State.Liquid)}
            Dim mass As Double = 0.0, h As Double = 0.0
            For Each part In parts
                If part.Item1 <= 0.0 OrElse part.Item2 Is Nothing OrElse part.Item2.Sum() <= 0.0 Then Continue For
                Dim m = part.Item1 * PP.AUX_MMM(part.Item2)
                mass += m
                h += m * PP.DW_CalcEnthalpy(part.Item2, T, P, part.Item3)
            Next
            Return If(mass > 0.0, h / mass, 0.0)
        End Function

        Public Overrides Function Flash_PS(Vz As Double(), P As Double, S As Double, Tref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = False, Optional PrevKi As Double() = Nothing) As Object
            Return _base.Flash_PS(Vz, P, S, Tref, PP, ReuseKI, PrevKi)
        End Function

        Public Overrides Function Flash_PV(Vz As Double(), P As Double, V As Double, Tref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = False, Optional PrevKi As Double() = Nothing) As Object
            Return _base.Flash_PV(Vz, P, V, Tref, PP, ReuseKI, PrevKi)
        End Function

        Public Overrides Function Flash_TV(Vz As Double(), T As Double, V As Double, Pref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = False, Optional PrevKi As Double() = Nothing) As Object
            Return _base.Flash_TV(Vz, T, V, Pref, PP, ReuseKI, PrevKi)
        End Function

    End Class

End Namespace
