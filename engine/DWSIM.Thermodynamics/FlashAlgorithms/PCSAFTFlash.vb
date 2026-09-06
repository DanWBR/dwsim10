'    PC-SAFT Flash Algorithm
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

Imports DWSIM.MathOps.MathEx.BrentOpt

Namespace PropertyPackages.Auxiliary.FlashAlgorithms

    ''' <summary>
    ''' Flash algorithm for PC-SAFT mixtures that carry a non-volatile polymer. It owns the two phase
    ''' behaviours a polymer solution shows that the general vapour-liquid flash cannot: a liquid-liquid
    ''' split (cloud point), solved by SimpleLLE from the equation-of-state spinodal; and a vapour-liquid
    ''' flash where the polymer stays in the liquid (devolatilization), solved by a direct one-dimensional
    ''' root find on the vapour fraction with the solvent K-value recomputed at each trial composition. A
    ''' mixture with no non-volatile falls through to the universal flash unchanged.
    ''' </summary>
    Public Class PCSAFTFlash

        Inherits UniversalFlash

        Public Sub New()
            MyBase.New()
        End Sub

        Public Overrides ReadOnly Property Name As String
            Get
                Return "PC-SAFT Flash"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "PC-SAFT Equilibrium Flash (polymer-aware)"
            End Get
        End Property

        Public Overrides Function Flash_PT(Vz() As Double, P As Double, T As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = False, Optional PrevKi() As Double = Nothing) As Object

            Dim n As Integer = Vz.Length - 1

            ' Non-volatile (polymer) components cannot enter the vapour. Count the volatiles too.
            Dim nonvol = PP.RET_VNONVOLATILE()
            Dim hasNonVol As Boolean = False
            Dim sumNonVol As Double = 0.0
            Dim volIdx As Integer = -1
            Dim nVol As Integer = 0
            For i As Integer = 0 To n
                If i <= nonvol.GetUpperBound(0) AndAlso nonvol(i) Then
                    hasNonVol = True
                    sumNonVol += Vz(i)
                Else
                    nVol += 1
                    volIdx = i
                End If
            Next

            ' No polymer: an ordinary PC-SAFT mixture. Use the universal flash.
            If Not hasNonVol Then
                Return MyBase.Flash_PT(Vz, P, T, PP, ReuseKI, PrevKi)
            End If

            ' 1) Vapour-liquid flash first (it is cheap - a one-dimensional root find). If the solvent
            '    vaporises, that is the whole answer, and there is no need to look for a second liquid. This
            '    is what a devolatilization or a dewatering flash does, and it avoids the expensive
            '    liquid-liquid search for the common case that carries vapour.
            Dim vleRes As Object = Nothing
            If nVol = 1 Then
                vleRes = NonVolatileVLE(Vz, P, T, PP, nonvol, volIdx, sumNonVol)
                If Convert.ToDouble(vleRes(1)) > 0.00000001 Then Return vleRes
            End If

            ' 2) No vapour forms. Look for a liquid-liquid split (cloud point) when the package does polymer
            '    LLE (a homopolymer; PC-SAFT declines it for a copolymer, whose composition derivative the LLE
            '    Newton cannot use). SimpleLLE seeds itself from the spinodal; a genuine split below the boiling
            '    pressure is the answer.
            If PP.UsesGibbsMinimizationForLLE Then
                Dim rL As Object = New SimpleLLE().Flash_PT(Vz, P, T, PP)
                Dim L2s As Double = Convert.ToDouble(rL(5))
                If L2s > 0.0001 Then
                    Dim Vx1s = DirectCast(rL(2), Double())
                    Dim g1 = DirectCast(rL(9), Double())
                    Dim PVs = PP.RET_VPVAP(T)
                    Dim Pbub As Double = 0.0
                    For i As Integer = 0 To n
                        Pbub += Vx1s(i) * g1(i) * PVs(i)
                    Next
                    If P > Pbub Then
                        Return New Object() {Convert.ToDouble(rL(0)), 0.0, Vx1s, PP.RET_NullVector, T,
                                             L2s, DirectCast(rL(6), Double()), 0.0, PP.RET_NullVector}
                    End If
                End If
            End If

            ' 3) Neither vapour nor a second liquid: a single liquid (the all-liquid vapour-liquid result),
            '    or - for several volatiles - hand it to the universal flash.
            If vleRes IsNot Nothing Then Return vleRes
            Return MyBase.Flash_PT(Vz, P, T, PP, ReuseKI, PrevKi)

        End Function

        ''' <summary>
        ''' Vapour-liquid flash for a single volatile solvent plus one or more non-volatiles. The vapour is
        ''' pure solvent, so equilibrium is one equation in the vapour fraction V: K_solvent(x(V)) * x_solvent(V)
        ''' = 1, with every non-volatile held in the liquid (x_j = z_j / (1 - V)). The K-value is recomputed at
        ''' each trial composition instead of being frozen - a frozen K is what makes a successive-substitution
        ''' flash oscillate for a polymer solution, whose solvent K-value swings by orders of magnitude across
        ''' the composition (steeply and near unity for an associating polymer such as PEG in water). The vapour
        ''' fugacity is constant (pure solvent) and computed once. The residual is monotonic in V, so a bracket
        ''' converges it, and the composition is set explicitly so the whole polymer feed is conserved.
        ''' </summary>
        Private Function NonVolatileVLE(Vz() As Double, P As Double, T As Double, PP As PropertyPackage,
                                        nonvol As Boolean(), volIdx As Integer, sumNonVol As Double) As Object

            Dim n As Integer = Vz.Length - 1
            Dim Vcap As Double = 1.0 - sumNonVol

            Dim yPure(n) As Double
            yPure(volIdx) = 1.0
            Dim lnPhiVap As Double = PP.DW_CalcLnFugCoeff(yPure, T, P, State.Vapor)(volIdx)

            ' Whether a genuine vapour can form at all. Above the solvent's critical pressure the equation of
            ' state has a single fluid root, so its vapour-state and liquid-state fugacity coefficients collapse
            ' onto the same value; any vaporisation the root find would then report comes from a spurious dense
            ' "vapour" root, and the real second phase is a liquid (the cloud point). When the two coincide, hold
            ' the vapour fraction at zero so the flash defers to the liquid-liquid search.
            Dim lnPhiLiqPure As Double = PP.DW_CalcLnFugCoeff(yPure, T, P, State.Liquid)(volIdx)
            Dim vapourCanForm As Boolean = Math.Abs(lnPhiVap - lnPhiLiqPure) > 0.001

            ' Liquid composition at a trial vapour fraction: the non-volatiles hold their whole feed, the
            ' solvent fills the rest.
            Dim LiquidComp As Func(Of Double, Double()) =
                Function(vv)
                    Dim Ll As Double = 1.0 - vv
                    Dim xx(n) As Double
                    Dim xs As Double = 1.0
                    For ix As Integer = 0 To n
                        If nonvol(ix) Then
                            xx(ix) = Vz(ix) / Ll
                            xs -= xx(ix)
                        End If
                    Next
                    xx(volIdx) = xs
                    Return xx
                End Function

            Dim gRes As Func(Of Double, Double) =
                Function(vv)
                    Dim xx = LiquidComp(vv)
                    If xx(volIdx) <= 1.0E-8 Then Return -1.0    'essentially all solvent vaporised
                    Dim lnPhiLiq As Double = PP.DW_CalcLnFugCoeff(xx, T, P, State.Liquid)(volIdx)
                    Return Math.Exp(lnPhiLiq - lnPhiVap) * xx(volIdx) - 1.0
                End Function

            Dim V As Double
            If Not vapourCanForm Then
                'no vapour root distinct from the liquid: the solvent is above its critical pressure
                V = 0.0
            ElseIf gRes(0.0) <= 0.0 Then
                'the solvent will not vaporise even from the feed: a single liquid
                V = 0.0
            ElseIf gRes(Vcap) >= 0.0 Then
                'every volatile mole vaporises; the liquid is the non-volatiles only
                V = Vcap
            Else
                V = Brent.BrentOpt3(0.0, Vcap, 12, 0.0000001, 100, gRes)
            End If

            Dim L As Double = 1.0 - V
            Dim Vx() As Double
            Dim Vy(n) As Double
            Vy(volIdx) = 1.0
            If V <= 0.0 Then
                V = 0.0
                L = 1.0
                Vx = DirectCast(Vz.Clone(), Double())
            Else
                Vx = LiquidComp(V)
            End If

            Dim Ki(n) As Double
            For i As Integer = 0 To n
                If Vx(i) > 0.0 Then Ki(i) = Vy(i) / Vx(i)
            Next

            Return New Object() {L, V, Vx, Vy, 0, 0.0, PP.RET_NullVector(), 0.0, PP.RET_NullVector(), Ki}

        End Function

    End Class

End Namespace
