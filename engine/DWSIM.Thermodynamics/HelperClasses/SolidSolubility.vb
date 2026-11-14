'    Solubility of a pure solid in a liquid mixture
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

Imports System.Math
Imports DWSIM.Interfaces

Namespace PropertyPackages.Auxiliary

    ''' <summary>A liquid saturated with a pure solid solute.</summary>
    Public Class SolidSaturationResult

        ''' <summary>Mole fraction of the solute in the saturated liquid; one at or above the melting point.</summary>
        Public MoleFraction As Double

        ''' <summary>Mole fraction an ideal solution would hold: the activity of the solute at saturation.</summary>
        Public IdealMoleFraction As Double

        ''' <summary>Activity coefficient of the solute in the saturated liquid, referred to the pure subcooled liquid.</summary>
        Public ActivityCoefficient As Double

        ''' <summary>True when the temperature is at or above the melting point of the solute, where it does not saturate.</summary>
        Public AboveMeltingPoint As Boolean

    End Class

    ''' <summary>
    ''' Solubility of a solid compound in a liquid, from a property package and the fusion data of the compound.
    ''' The solid is the pure solute. At saturation ln(x.gamma) equals the ln of the ratio between the fugacities of
    ''' the pure solid and of the pure subcooled liquid, which comes from the melting point, the enthalpy of fusion and,
    ''' when the compound has it, the difference between the heat capacities of the liquid and of the solid.
    ''' </summary>
    Public Module SolidSolubility

        Private Const R As Double = 8.31446

        ''' <summary>ln of the solid-liquid activity ratio of a pure compound; zero at or above its melting point.</summary>
        Public Function LnSaturationActivity(pp As PropertyPackage, cp As ICompoundConstantProperties, T As Double) As Double
            Dim Tf = cp.TemperatureOfFusion, Hf = cp.EnthalpyOfFusionAtTf
            If Tf <= 0.0 OrElse Hf <= 0.0 Then
                Throw New InvalidOperationException("'" & cp.Name & "' does not have a melting point and an enthalpy of fusion.")
            End If
            If T >= Tf Then Return 0.0
            Return -Hf * 1000.0 / R / T * (1.0 - T / Tf) + pp.AUX_SolidFusionCpTerm(cp, T)
        End Function

        ''' <summary>
        ''' Activity coefficients of every compound in a liquid of composition <paramref name="x"/>, referred to the
        ''' pure liquids. An activity coefficient package gives them from its model, which does not go through the vapor
        ''' pressure of the compounds; the other packages give them as the ratio between the fugacity coefficients in
        ''' the mixture and in the pure liquid.
        ''' </summary>
        Public Function ActivityCoefficients(pp As PropertyPackage, x As Double(), T As Double, P As Double) As Double()
            Dim n = x.Length - 1
            Dim g(n) As Double
            Dim act = TryCast(pp, ActivityCoefficientPropertyPackage)
            If act IsNot Nothing AndAlso act.m_act IsNot Nothing Then
                Dim res = act.m_act.CalcActivityCoefficients(T, x, act.GetArguments())
                For i = 0 To n
                    g(i) = Convert.ToDouble(res.GetValue(i))
                Next
                Return g
            End If
            Dim phi = pp.DW_CalcFugCoeff(x, T, P, State.Liquid)
            For i = 0 To n
                Dim pure(n) As Double
                pure(i) = 1.0
                g(i) = phi(i) / pp.DW_CalcFugCoeff(pure, T, P, State.Liquid)(i)
            Next
            Return g
        End Function

        ''' <summary>
        ''' The liquid saturated with the solute at T and P.
        ''' </summary>
        ''' <param name="pp">Property package with a current material stream holding the compounds.</param>
        ''' <param name="solute">Index of the solute among the compounds of the package.</param>
        ''' <param name="solvent">Composition of the liquid without the solute, in mole fractions over all the compounds
        ''' (zero at the solute).</param>
        ''' <remarks>
        ''' f(x) = ln x + ln gamma(x) - ln a(sat) is negative at infinite dilution and positive for the pure melt. The
        ''' lowest root is the saturated liquid on the solvent side; a scan in ln x brackets it even when the model
        ''' predicts two liquid phases, and bisection refines it.
        ''' </remarks>
        Public Function Saturation(pp As PropertyPackage, solute As Integer, solvent As Double(), T As Double, P As Double) As SolidSaturationResult

            Dim n = solvent.Length - 1
            Dim cp = pp.DW_GetConstantProperties()(solute)
            Dim lnasat = LnSaturationActivity(pp, cp, T)

            Dim res As New SolidSaturationResult With {.IdealMoleFraction = Exp(lnasat)}

            If T >= cp.TemperatureOfFusion Then
                res.MoleFraction = 1.0
                res.ActivityCoefficient = 1.0
                res.AboveMeltingPoint = True
                Return res
            End If

            Dim lnGamma As Func(Of Double, Double)

            Dim act = TryCast(pp, ActivityCoefficientPropertyPackage)
            If act IsNot Nothing AndAlso act.m_act IsNot Nothing Then
                Dim args = act.GetArguments()
                lnGamma = Function(x1) Log(Convert.ToDouble(act.m_act.CalcActivityCoefficients(T, Mixture(solute, solvent, x1), args).GetValue(solute)))
            Else
                Dim pure(n) As Double
                pure(solute) = 1.0
                Dim lnphiPure = Log(pp.DW_CalcFugCoeff(pure, T, P, State.Liquid)(solute))
                If Double.IsNaN(lnphiPure) OrElse Double.IsInfinity(lnphiPure) Then
                    Throw New InvalidOperationException("The property package did not return a liquid fugacity for pure '" & cp.Name & "' at " & T.ToString("0.##") & " K.")
                End If
                lnGamma = Function(x1) Log(pp.DW_CalcFugCoeff(Mixture(solute, solvent, x1), T, P, State.Liquid)(solute)) - lnphiPure
            End If

            Dim f As Func(Of Double, Double) = Function(lx) lx + lnGamma(Exp(lx)) - lnasat

            Const scan As Integer = 48
            Dim lxLow = Log(0.00000000000001), lxHigh = 0.0
            Dim a = lxLow, fa = f(a)
            If Double.IsNaN(fa) Then Throw New InvalidOperationException("The property package did not return an activity coefficient for '" & cp.Name & "'.")
            Dim b = a, fb = fa
            Dim found = fa >= 0.0
            If Not found Then
                For k = 1 To scan
                    b = lxLow + (lxHigh - lxLow) * k / scan
                    fb = If(k = scan, -lnasat, f(b))
                    If Double.IsNaN(fb) Then Throw New InvalidOperationException("The property package did not return an activity coefficient for '" & cp.Name & "'.")
                    If fb >= 0.0 Then
                        found = True
                        Exit For
                    End If
                    a = b
                    fa = fb
                Next
            End If
            If Not found Then Throw New InvalidOperationException("No saturated solution found for '" & cp.Name & "'.")

            If fa >= 0.0 Then
                'saturated below the lowest fraction scanned: practically insoluble
                b = a
            Else
                Dim it = 0
                While it < 60 AndAlso b - a > 0.0000000001
                    Dim m = 0.5 * (a + b), fm = f(m)
                    If fm >= 0.0 Then
                        b = m
                    Else
                        a = m
                    End If
                    it += 1
                End While
                b = 0.5 * (a + b)
            End If

            res.MoleFraction = Exp(b)
            res.ActivityCoefficient = Exp(lnasat - b)
            Return res

        End Function

        ''' <summary>
        ''' The temperature at which a liquid with the mole fraction <paramref name="x"/> of the solute is saturated,
        ''' between <paramref name="Tmin"/> and the melting point of the solute. Returns NaN when the liquid is not
        ''' saturated anywhere in that range.
        ''' </summary>
        Public Function SaturationTemperature(pp As PropertyPackage, solute As Integer, solvent As Double(), x As Double, P As Double, Tmin As Double) As Double
            Dim cp = pp.DW_GetConstantProperties()(solute)
            Dim Tf = cp.TemperatureOfFusion
            If Not (x > 0.0) OrElse x >= 1.0 OrElse Tmin >= Tf Then Return Double.NaN
            Dim g As Func(Of Double, Double) = Function(T) Saturation(pp, solute, solvent, T, P).MoleFraction - x
            Dim lo = Tmin, hi = Tf
            'at the lower end the liquid has to hold less than x, otherwise it is not saturated in the range
            If g(lo) >= 0.0 Then Return Double.NaN
            For it = 1 To 50
                Dim m = 0.5 * (lo + hi)
                If g(m) >= 0.0 Then hi = m Else lo = m
                If hi - lo < 0.001 Then Exit For
            Next
            Return 0.5 * (lo + hi)
        End Function

        Private Function Mixture(solute As Integer, solvent As Double(), x1 As Double) As Double()
            Dim n = solvent.Length - 1
            Dim z(n) As Double
            For i = 0 To n
                z(i) = If(i = solute, x1, (1.0 - x1) * solvent(i))
            Next
            Return z
        End Function

    End Module

End Namespace
