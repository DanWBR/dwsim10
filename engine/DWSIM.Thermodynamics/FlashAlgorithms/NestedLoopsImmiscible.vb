'    DWSIM Nested Loops Flash Algorithms for Simplified (Immiscible) VLLE
'    Copyright 2013-2024 Daniel Wagner O. de Medeiros
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

Namespace PropertyPackages.Auxiliary.FlashAlgorithms

    ''' <summary>
    ''' The Flash algorithms in this class are based on the Nested Loops approach to solve equilibrium calculations.
    ''' </summary>
    ''' <remarks></remarks>
    <System.Serializable()> Public Class NestedLoopsImmiscible

        Inherits FlashAlgorithm

        Dim etol As Double = 0.000001
        Dim itol As Double = 0.000001
        Dim maxit_i As Integer = 100
        Dim maxit_e As Integer = 100
        Dim Hv0, Hvid, Hlid, Hf, Hv, Hl, Hlid2, Hl2 As Double
        Dim Sv0, Svid, Slid, Sf, Sv, Sl, Slid2, Sl2 As Double

        Private _nl As New NestedLoops

        Sub New()
            MyBase.New()
            Order = 3
        End Sub

        Public Overrides ReadOnly Property AlgoType As Interfaces.Enums.FlashMethod
            Get
                Return Interfaces.Enums.FlashMethod.Nested_Loops_Immiscible_VLLE
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                If GlobalSettings.Settings.CurrentCulture = "pt-BR" Then
                    Return "Algoritmo Nested Loops VLLE simplificado para cálculos de equilíbrio entre Água e Hidrocarbonetos."
                Else
                    Return "Simplified Nested Loops VLLE Flash Algorithm for Hydrocarbon-Water equilibrium calculations."
                End If
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "Nested Loops (Immiscible VLLE)"
            End Get
        End Property

        Public Property CompoundProperties As List(Of Interfaces.ICompoundConstantProperties)

        Public Overrides Function Flash_PT(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            If CompoundProperties Is Nothing Then CompoundProperties = PP.DW_GetConstantProperties

            Dim keycomps As String() = New String() {"Water"}

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, V As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            Dim Vn(n) As String, Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), Ki2(n), Ki_ant(n), fi(n) As Double

            Dim Vprops = PP.DW_GetConstantProperties()

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim nwm As Double = 0
            Dim wid As Integer = -1

            For i = 0 To n
                If CompoundProperties(i).Name = keycomps(0) Then
                    wid = i
                    nwm = fi(i)
                End If
            Next

            'the organic part of the feed, per mole of organics
            If wid >= 0 Then fi(wid) = 0.0#
            If nwm > 0.0# And nwm < 1.0# Then
                For i = 0 To n
                    If i <> wid Then fi(i) = fi(i) / (1 - nwm)
                Next
            End If

            Dim xl1, xl2, Vx1(n), Vx2(n), Vn1(n), nHCy, nWy, nWx As Double

            Vx2 = PP.RET_NullVector
            If wid >= 0 Then Vx2(wid) = 1.0#

            If fi.Sum() = 0.0# Then

                'water alone
                If wid >= 0 AndAlso PP.AUX_PVAPi(wid, T) / P > 1.0# Then
                    nWy = nwm
                    nWx = 0.0#
                Else
                    nWy = 0.0#
                    nWx = nwm
                End If
                nHCy = 0.0#
                xl1 = 0.0#
                Vx1 = PP.RET_NullVector()
                Vy = PP.RET_NullVector()
                If nWy > 0.0# Then Vy(wid) = 1.0#
                ecount = 0

            Else

                'The vapor is an ideal mixture of the organics and of the water. While free water is left, water
                'has its vapor pressure in the vapor, so the organics are in equilibrium at the rest of the
                'pressure, P - Psat(water). When the vapor takes all the water, the organic partial pressure
                'Porg solves Porg = P nHC/(nHC + nW), with nHC the organic vapor of a flash at Porg.
                Dim no As Double = 1.0# - nwm
                Dim Psw As Double = If(wid >= 0 AndAlso nwm > 0.0#, PP.AUX_PVAPi(wid, T), 0.0#)

                Dim orgflash = Function(pOrganic As Double) As Object
                                   Return _nl.Flash_PT(fi, pOrganic, T, PP, ReuseKI, PrevKi)
                               End Function

                Dim results As Object = Nothing
                Dim saturated As Boolean = False

                If nwm = 0.0# Then
                    results = orgflash(P)
                    nWy = 0.0#
                    saturated = True
                ElseIf Psw < P Then
                    results = orgflash(P - Psw)
                    nHCy = results(1) * no
                    nWy = nHCy * Psw / (P - Psw)
                    saturated = nWy <= nwm
                End If

                If Not saturated Then
                    'all the water is vapor: find Porg
                    Dim gfun = Function(pOrganic As Double) As Double
                                   Dim r As Object = orgflash(pOrganic)
                                   Dim nv As Double = r(1) * no
                                   Return pOrganic - P * nv / (nv + nwm)
                               End Function
                    Dim lo As Double = P * 0.000001, hi As Double = P
                    Dim glo As Double = gfun(lo), ghi As Double = gfun(hi)
                    Dim Porg As Double = hi
                    If glo < 0.0# And ghi > 0.0# Then
                        For k As Integer = 0 To 100
                            'regula falsi with the Illinois step, then bisection when it stalls
                            Dim pm As Double = (lo * ghi - hi * glo) / (ghi - glo)
                            If Not (pm > lo And pm < hi) Then pm = 0.5 * (lo + hi)
                            Dim gm As Double = gfun(pm)
                            Porg = pm
                            If Math.Abs(gm) < 0.000001 * P OrElse (hi - lo) < 0.000000001 * P Then Exit For
                            If gm < 0.0# Then
                                lo = pm : glo = gm : ghi *= 0.5
                            Else
                                hi = pm : ghi = gm : glo *= 0.5
                            End If
                        Next
                    End If
                    results = orgflash(Porg)
                    nWy = nwm
                    'A single organic compound vaporizes all at once at its vapor pressure, so the root sits on that
                    'step and the flash at Porg lands on one side of it. The organic vapor is then the amount that the
                    'partial pressures need, nHC = nW Porg/(P - Porg), and the rest of the organic stays liquid.
                    Dim nvneed As Double = If(P > Porg, nwm * Porg / (P - Porg) / no, 1.0#)
                    If fi.Where(Function(fz) fz > 0.0#).Count() = 1 AndAlso nvneed > 0.0# AndAlso nvneed < 1.0# AndAlso
                        Math.Abs(results(1) - nvneed) > 0.000001 Then
                        results = New Object() {1.0# - nvneed, nvneed, fi.Clone(), fi.Clone(), results(4)}
                    End If
                End If

                L = results(0)
                V = results(1)
                Vx = results(2)
                Vy = results(3)
                ecount = results(4)

                nHCy = V * no
                xl1 = L * no
                Vx1 = Vx.Clone

                If saturated Then
                    nWx = nwm - nWy
                Else
                    nWx = 0.0#
                End If

                Dim Vtot As Double = nHCy + nWy
                Dim Vy2(n) As Double
                If Vtot > 0.0# Then
                    For i = 0 To n
                        If i <> wid Then Vy2(i) = Vy(i) * nHCy / Vtot
                    Next
                    If wid >= 0 Then Vy2(wid) = nWy / Vtot
                End If
                Vy = Vy2

            End If

            V = nHCy + nWy
            xl2 = nWx

            'supercritical gases solubility

            Dim VTc = PP.RET_VTC()

            For i = 0 To n
                If xl2 > 0 And i <> wid And T / VTc(i) > 1.0 Then
                    Vx2(i) = Vy(i) * P / PP.AUX_KHenry(Vn(i), T)
                End If
            Next

            'hydrocarbon solubility

            Dim isPF As Boolean

            For i = 0 To n
                If Vprops(i).IsPF Then
                    isPF = True
                Else
                    If Vprops(i).Elements.Count = 2 And Vprops(i).Elements.ContainsKey("C") And Vprops(i).Elements.ContainsKey("H") Then
                        isPF = True
                    Else
                        isPF = False
                    End If
                End If
                Dim nC = 0
                If Vprops(i).Elements.ContainsKey("C") Then
                    nC = Vprops(i).Elements("C")
                End If
                If nC > 0 And fi(i) > 0.0 And xl2 > 0 And i <> wid And Vx2(i) = 0.0 And isPF Then
                    Dim sol = Math.Exp(-1.6708 - 0.6386 * nC - 0.5538 * nC ^ 2)
                    Dim sT1 = Math.Exp(19.76 - 30125 / (1.8 * T) + 8649917 / (1.8 * T) ^ 2)
                    Dim sT2 = Math.Exp(19.76 - 30125 / (1.8 * 298.15) + 8649917 / (1.8 * 298.15) ^ 2)
                    Dim dSdT = (sT1 - sT2) / (298.15 - T) / sT1
                    If T = 298.15 Then dSdT = 0.0
                    Vx2(i) = sol + sol * dSdT * (T - 298.15)
                    If Vx2(i) < 0.0 Then Vx2(i) = sol
                    Vn1(i) = fi(i) - Vx2(i) * xl2 - Vy(i) * V
                    If Vn1(i) < 0.0 Then Vx2(i) = 0.0
                End If
            Next

            Vx2 = Vx2.NormalizeY()

            Ki = PP.DW_CalcKvalue(Vx1, Vy, T, P)
            Ki2 = PP.DW_CalcKvalue(Vx2, Vy, T, P)

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PT Flash [NL-I]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

out:        Return New Object() {xl1, V, Vx1, Vy, ecount, xl2, Vx2, 0.0#, PP.RET_NullVector, Ki, Ki2}

        End Function

        Public Overrides Function Flash_PH(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim nl = New NestedLoops
            nl.FlashSettings = FlashSettings
            nl.PTFlashFunction = AddressOf Flash_PT
            nl.DisableParallelCalcs = True
            Return nl.Flash_PH(Vz, P, H, Tref, PP, ReuseKI, PrevKi)

        End Function

        Public Overrides Function Flash_PS(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim nl = New NestedLoops
            nl.FlashSettings = FlashSettings
            nl.PTFlashFunction = AddressOf Flash_PT
            nl.DisableParallelCalcs = True
            Return nl.Flash_PS(Vz, P, S, Tref, PP, ReuseKI, PrevKi)

        End Function

        Public Overrides Function Flash_TV(ByVal Vz As Double(), ByVal T As Double, ByVal V As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim nl = New NestedLoops

            Return nl.Flash_TV(Vz, T, V, Pref, PP, ReuseKI, PrevKi)

        End Function

        Public Overrides Function Flash_PV(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim nl = New NestedLoops

            Return nl.Flash_PV(Vz, P, V, Tref, PP, ReuseKI, PrevKi)

        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace
