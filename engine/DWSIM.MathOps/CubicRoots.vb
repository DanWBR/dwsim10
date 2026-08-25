'    Property Package Auxiliary Calculations Base Classes 
'    Copyright 2008-2014 Daniel Wagner O. de Medeiros
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

Imports System.Numerics
Imports MathNet.Numerics

Namespace MathEx

    Public Class PolySolve

        Shared Function Poly_Roots(ByVal Coeff As Double()) As Double(,)

            Return CalcRoots(Coeff(3), Coeff(2), Coeff(1), Coeff(0))

        End Function

        Shared Function Poly_Roots3(ByVal Coeff As Double()) As Double()

            Return CalcRoots3(Coeff(3), Coeff(2), Coeff(1), Coeff(0))

        End Function

        ''' <summary>
        ''' Solves the monic cubic in Z for a cubic equation of state and returns the physical
        ''' compressibility factor: the smallest real root above the covolume B for a liquid, the
        ''' largest for a vapour. Roots at or below B (molar volume &lt;= b, non-physical) and the
        ''' complex roots are discarded, so the liquid branch can no longer take an unphysical root
        ''' (which showed up as liquid densities above the M/b hard limit near the critical point).
        ''' The stable MathNet cubic solver is used, avoiding the stalled values the multi-start
        ''' Newton path could return. If no root above B exists the largest real root is returned,
        ''' never below B.
        ''' </summary>
        ''' <param name="Coeff">Cubic coefficients, Coeff(0) constant term through Coeff(3) the z^3 term.</param>
        ''' <param name="B">Dimensionless covolume (bP/RT) of the phase.</param>
        ''' <param name="liquid">True for the liquid root (smallest valid), False for the vapour root (largest valid).</param>
        Shared Function SelectZ(ByVal Coeff As Double(), ByVal B As Double, ByVal liquid As Boolean) As Double

            Dim valid = ValidZRoots(Coeff, B)

            If valid.Count > 0 Then
                If liquid Then Return valid(0) Else Return valid(valid.Count - 1)
            End If

            'no root above the covolume: fall back to the largest real root, but never below B
            Dim best As Double = B
            For Each root In RealCubicRoots(Coeff)
                If root > best Then best = root
            Next
            Return best

        End Function

        ''' <summary>
        ''' Real roots of the monic EOS cubic that lie strictly above the covolume B, in ascending
        ''' order. Roots at or below B (non-physical, molar volume &lt;= b) are dropped. May be empty
        ''' when the pressure is past the point where the EOS has a physical solution (B &gt;= 1).
        ''' </summary>
        Shared Function ValidZRoots(ByVal Coeff As Double(), ByVal B As Double) As List(Of Double)

            Dim result As New List(Of Double)
            For Each root In RealCubicRoots(Coeff)
                If root > B Then result.Add(root)
            Next
            result.Sort()
            Return result

        End Function

        ''' <summary>
        ''' Real roots of a cubic Coeff(3) z^3 + Coeff(2) z^2 + Coeff(1) z + Coeff(0), solved
        ''' analytically (Cardano / trigonometric). The equation-of-state work only needs the real
        ''' roots, and doing it here keeps the routine independent of any external solver's version.
        ''' </summary>
        Shared Function RealCubicRoots(ByVal Coeff As Double()) As List(Of Double)

            Dim roots As New List(Of Double)
            Dim a3 = Coeff(3)
            If a3 = 0.0# Then Return roots

            Dim a = Coeff(2) / a3
            Dim b = Coeff(1) / a3
            Dim c = Coeff(0) / a3

            'depressed cubic t^3 + p t + q, with z = t - a/3
            Dim p = b - a * a / 3.0#
            Dim q = 2.0# * a * a * a / 27.0# - a * b / 3.0# + c
            Dim shift = a / 3.0#
            Dim disc = q * q / 4.0# + p * p * p / 27.0#

            If disc > 0.000000000001 Then
                'one real root (Cardano)
                Dim s = Math.Sqrt(disc)
                roots.Add(Cbrt(-q / 2.0# + s) + Cbrt(-q / 2.0# - s) - shift)
            ElseIf Math.Abs(p) < 0.00000000000001 Then
                'triple root
                roots.Add(-shift)
            Else
                'three real roots (disc <= 0): trigonometric form
                Dim m = 2.0# * Math.Sqrt(-p / 3.0#)
                Dim arg = 3.0# * q / (p * m)
                If arg > 1.0# Then arg = 1.0#
                If arg < -1.0# Then arg = -1.0#
                Dim theta = Math.Acos(arg) / 3.0#
                For k = 0 To 2
                    roots.Add(m * Math.Cos(theta - 2.0# * Math.PI * k / 3.0#) - shift)
                Next
            End If

            Return roots

        End Function

        Private Shared Function Cbrt(ByVal x As Double) As Double
            Return If(x < 0.0#, -Math.Pow(-x, 1.0# / 3.0#), Math.Pow(x, 1.0# / 3.0#))
        End Function

        Shared Function CalcRoots2(ByVal a As Double, ByVal b As Double, ByVal c As Double, ByVal d As Double) As Double(,)

            Dim roots0 = FindRoots.Cubic(d, c, b, a)
            Dim root1 = roots0.Item1
            Dim root2 = roots0.Item2
            Dim root3 = roots0.Item3

            Dim roots(2, 1), real1, im1 As Double

            roots(0, 0) = root1.Real
            If Math.Abs(root1.Imaginary) > 0.0000000001 Then
                roots(0, 1) = root1.Imaginary
            Else
                real1 = root1.Real
                im1 = root1.Imaginary
            End If
            roots(1, 0) = root2.Real
            If Math.Abs(root2.Imaginary) > 0.0000000001 Then
                roots(1, 1) = root2.Imaginary
            Else
                real1 = root2.Real
                im1 = root2.Imaginary
            End If
            roots(2, 0) = root3.Real
            If Math.Abs(root3.Imaginary) > 0.0000000001 Then
                roots(2, 1) = root3.Imaginary
            Else
                real1 = root3.Real
                im1 = root3.Imaginary
            End If

            If roots(0, 0) < 0.0000000001 Then
                roots(0, 0) = real1
                roots(0, 1) = im1
            End If
            If roots(1, 0) < 0.0000000001 Then
                roots(1, 0) = real1
                roots(1, 1) = im1
            End If
            If roots(2, 0) < 0.0000000001 Then
                roots(2, 0) = real1
                roots(2, 1) = im1
            End If

            Return roots

        End Function

        Shared Function CalcRoots3(ByVal a As Double, ByVal b As Double, ByVal c As Double, ByVal d As Double) As Double()

            Dim roots0 = FindRoots.Cubic(d, c, b, a)
            Dim root1 = roots0.Item1
            Dim root2 = roots0.Item2
            Dim root3 = roots0.Item3

            Dim roots(2) As Double
            Dim real1 As Double

            If Math.Abs(root1.Imaginary) < 0.00000001 Then
                roots(0) = root1.Real
                real1 = roots(0)
            End If
            If Math.Abs(root2.Imaginary) < 0.00000001 Then
                roots(1) = root2.Real
                real1 = roots(1)
            End If
            If Math.Abs(root3.Imaginary) < 0.00000001 Then
                roots(2) = root3.Real
                real1 = roots(2)
            End If

            If Math.Abs(roots(0)) < 0.0000000001 Then roots(0) = real1
            If Math.Abs(roots(1)) < 0.0000000001 Then roots(1) = real1
            If Math.Abs(roots(2)) < 0.0000000001 Then roots(2) = real1

            Array.Sort(roots)

            Return roots

        End Function

        Shared Function CalcRoots(ByVal a As Double, ByVal b As Double, ByVal c As Double, ByVal d As Double) As Double(,)

            Dim cnt As Integer = 0
            Dim r, rant, rant2, fi, fi_ant, fi_ant2, dfidr As Double

            fi_ant = 0.0#
            fi = 0.0#

            r = 0.01
            rant = r
            Do
                fi_ant2 = fi_ant
                fi_ant = fi
                fi = a * r * r * r + b * r * r + c * r + d
                dfidr = 3 * a * r * r + 2 * b * r + c
                rant = r
                r = r - fi / dfidr
                If Math.Abs(fi - fi_ant2) = 0.0# Then r = rant * 1.01
                cnt += 1
            Loop Until Math.Abs(fi) < 0.00000001 Or cnt >= 1000

            Dim r1, i1, r2, i2, r3, i3 As Double

            If cnt >= 1000 Then
                r1 = r
                i1 = -1
            Else
                r1 = r
                i1 = 0
            End If

            fi_ant = 0
            fi = 0

            cnt = 0

            r = 0.99999999
            rant = r
            Do
                fi_ant2 = fi_ant
                fi_ant = fi
                fi = a * r * r * r + b * r * r + c * r + d
                dfidr = 3 * a * r * r + 2 * b * r + c
                rant = r
                r = r - fi / dfidr
                If Math.Abs(fi - fi_ant2) = 0 Then r = rant * 0.999
                cnt += 1
            Loop Until Math.Abs(fi) < 0.00000001 Or cnt >= 1000

            If cnt >= 1000 Then
                r2 = r
                i2 = -1
            Else
                r2 = r
                i2 = 0
            End If

            fi_ant = 0
            fi = 0

            cnt = 0

            r = 0.5
            rant = r
            Do
                fi_ant2 = fi_ant
                fi_ant = fi
                fi = a * r * r * r + b * r * r + c * r + d
                dfidr = 3 * a * r * r + 2 * b * r + c
                rant = r
                r = r - fi / dfidr
                If Math.Abs(fi - fi_ant2) = 0 Then r = rant * 0.999
                cnt += 1
            Loop Until Math.Abs(fi) < 0.00000001 Or cnt >= 1000

            If cnt >= 1000 Then
                r3 = r
                i3 = -1
            Else
                r3 = r
                i3 = 0
            End If

            Dim roots(2, 1) As Double

            roots(0, 0) = r1
            roots(0, 1) = i1
            roots(1, 0) = r2
            roots(1, 1) = i2
            roots(2, 0) = r3
            roots(2, 1) = i3

            Return roots

        End Function

    End Class

End Namespace