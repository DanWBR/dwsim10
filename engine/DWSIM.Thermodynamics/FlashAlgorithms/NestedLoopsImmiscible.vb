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
            Dim wid As Integer = 0

            If Me.FlashSettings(Interfaces.Enums.FlashSetting.NL_FastMode) = True Then

                For i = 0 To n
                    If CompoundProperties(i).Name = keycomps(0) Then
                        wid = i
                        nwm = fi(i)
                        fi(i) = 0
                    End If
                Next

                If nwm <> 0 Then
                    For i = 0 To n
                        If i <> wid And nwm <> 1.0# Then fi(i) = fi(i) / (1 - nwm)
                    Next
                End If

                Dim results As Object
                If fi.Sum() = 0.0# Then
                    If PP.AUX_PVAPi(wid, T) / P > 1.0# Then
                        L = 0.0#
                        V = 1.0#
                    Else
                        L = 1.0#
                        V = 0.0#
                    End If
                    Vx = PP.RET_NullVector()
                    Vy = PP.RET_NullVector()
                    ecount = 0.0#
                Else
                    results = _nl.Flash_PT(fi, P, T, PP, ReuseKI, PrevKi)
                    L = results(0)
                    V = results(1)
                    Vx = results(2)
                    Vy = results(3)
                    ecount = results(4)
                End If

            Else

                For i = 0 To n
                    If CompoundProperties(i).Name = keycomps(0) Then
                        wid = i
                        nwm = fi(i)
                    End If
                Next

                If nwm <> 0 Then
                    For i = 0 To n
                        If i <> wid Then fi(i) = fi(i) / (1 - nwm) Else fi(i) = 0.0#
                    Next
                End If

                Dim results As Object
                results = _nl.Flash_PT(fi, P, T, PP, ReuseKI, PrevKi)
                L = results(0)
                V = results(1)
                Vx = results(2)
                Vy = results(3)
                ecount = results(4)

            End If

            Dim xl1, xl2, Vx1(n), Vx2(n), Vn1(n), nHCy, nWy, nWx As Double

            V = V * (1 - nwm)
            xl1 = L * (1 - nwm)
            xl2 = nwm
            Vx1 = Vx.Clone
            Vx2 = PP.RET_NullVector
            Vx2(wid) = 1.0#

            Vy(wid) = PP.AUX_PVAPi(wid, T) / P

            nHCy = V

            nWy = nHCy * Vy(wid) / (1 - Vy(wid))

            nWx = nwm - nWy

            If nWx < 0.0 Then
                nWy = nwm
                nWx = 0.0
            End If

            xl2 -= nWy

            V = nHCy + nWy
            xl2 = nWx

            For i = 0 To n
                If i <> wid Then
                    If V > 0 Then
                        Vy(i) = Vy(i) * nHCy / V
                    End If
                End If
            Next

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
