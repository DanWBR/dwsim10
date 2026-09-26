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

            ' Every root, real or complex, within |a/3|/2 of the inflection point -a/3 (Fujiwara's bound on the
            ' depressed cubic): the EOS cubic next to the critical point. There p and q above are differences of
            ' numbers far larger than themselves, the absolute threshold on disc below misclassifies one real root as
            ' three (Z off by up to 6 %), and Newton on f(z) evaluated in plain precision cannot resolve the cluster.
            If (Math.Abs(p) <= shift * shift / 16.0# AndAlso Math.Abs(q) <= Math.Abs(shift * shift * shift) / 32.0#) OrElse
                Math.Abs(p) < 0.00000000000001 Then
                Return ClusterRoots(a, b, c)
            End If

            ' disc against the size of its own terms: an absolute 1E-12 sent a cubic with a pair of roots 1E-8 apart
            ' (relative) at a scale of 1E3 to Cardano, which returns one root. A disc within rounding of zero goes to
            ' the trigonometric form, and SplitClosePair decides whether the pair is real.
            If disc > 0.000000000001 * (q * q / 4.0# + Math.Abs(p * p * p) / 27.0#) Then
                'one real root (Cardano)
                Dim s = Math.Sqrt(disc)
                roots.Add(Cbrt(-q / 2.0# + s) + Cbrt(-q / 2.0# - s) - shift)
            ElseIf p > 0.0# Then
                'p > 0 has one real root, and the trigonometric form below would take Sqrt(-p) (NaN, no root at all).
                'Reached when 0 < disc <= 1E-12, next to a pure compound's critical point.
                Dim s = Math.Sqrt(disc)
                roots.Add(Cbrt(-q / 2.0# + s) + Cbrt(-q / 2.0# - s) - shift)
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
                ' Near a double root (|arg| close to 1) theta carries an error of about sqrt(eps), so the two close roots
                ' come out with an absolute error far larger than their spacing; at |arg| = 1 they coincide at the local
                ' extremum of the cubic, which is no root, and the polish below cannot leave it. The liquid pair of an EOS
                ' at very low pressure (Z of the order of B, 1E-11 for C2/C3 at 85 K and 1E-4 Pa) is such a pair.
                If Math.Abs(arg) > 0.999999 Then Return SplitClosePair(a, b, c, roots)
            End If

            ' The closed forms carry an absolute error of about 1E-16 x the largest root. The liquid root of an
            ' EOS at low pressure is of the order of B (1E-8 for C2/C3 at 86 K and 0.15 Pa), so Z - B, and with
            ' it ln(Z - B) in the fugacity coefficient, had no correct digit left below about 1 Pa. Newton on the
            ' cubic itself restores full relative precision.
            For ir = 0 To roots.Count - 1
                roots(ir) = PolishCubicRoot(a, b, c, roots(ir))
            Next

            Return roots

        End Function

        ''' <summary>
        ''' Newton steps on the monic cubic z^3 + a z^2 + b z + c from an approximate root, each kept only while
        ''' it lowers |f|: a root already exact to rounding, or a spurious one from the trigonometric form, is
        ''' returned unchanged. f is evaluated by compensated Horner: in plain precision its rounding error,
        ''' about 1E-16 x the largest term, divided by the small f' of a root with a close neighbour, left such a
        ''' root off by up to 1E-8 (a pair next to the spinodal, or the cluster next to the critical point).
        ''' </summary>
        Private Shared Function PolishCubicRoot(ByVal a As Double, ByVal b As Double, ByVal c As Double, ByVal r As Double) As Double
            Dim co = New Double() {1.0#, a, b, c}
            Dim f = CompHorner(r, co)
            For it = 1 To 8
                If f = 0.0# Then Exit For
                Dim df = (3.0# * r + 2.0# * a) * r + b
                If df = 0.0# OrElse Double.IsNaN(df) OrElse Double.IsInfinity(df) Then Exit For
                Dim rn = r - f / df
                Dim fn = CompHorner(rn, co)
                If Not Math.Abs(fn) < Math.Abs(f) Then Exit For
                r = rn
                f = fn
            Next
            Return r
        End Function

        ''' <summary>
        ''' Value at x of the polynomial co(0) x^n + ... + co(n) by compensated Horner (Graillat, Langlois and Louvet,
        ''' 2005): the rounding error of every step is recovered exactly (Dekker's product with Veltkamp's split,
        ''' Knuth's sum) and added back, so the result is as accurate as Horner in twice the working precision.
        ''' </summary>
        Private Shared Function CompHorner(ByVal x As Double, ByVal co As Double()) As Double
            Dim r = co(0)
            Dim e = 0.0#
            Dim t = 134217729.0# * x
            Dim xh = t - (t - x)
            Dim xl = x - xh
            For k = 1 To co.Length - 1
                Dim pr = r * x
                t = 134217729.0# * r
                Dim rh = t - (t - r)
                Dim rl = r - rh
                Dim pe = rl * xl - (((pr - rh * xh) - rl * xh) - rh * xl)
                Dim s = pr + co(k)
                Dim z = s - pr
                Dim se = (pr - (s - z)) + (co(k) - z)
                r = s
                e = e * x + (pe + se)
            Next
            Return r + e
        End Function

        ''' <summary>
        ''' Roots of a cubic whose three roots lie within |a/3|/2 of the inflection point s = -a/3 (next to the
        ''' critical point of an EOS). With z = s + t, the t-cubic t^3 + f''(s)/2 t^2 + f'(s) t + f(s) has its
        ''' coefficients computed by compensated Horner, so its depressed form is known to full relative precision
        ''' however small the cluster is; its roots are classified by the sign of its discriminant alone and taken
        ''' from the cancellation-free Cardano form or the trigonometric form, then polished on the original cubic.
        ''' </summary>
        Private Shared Function ClusterRoots(ByVal a As Double, ByVal b As Double, ByVal c As Double) As List(Of Double)
            Dim s = -a / 3.0#
            Dim ca = CompHorner(s, New Double() {3.0#, a})
            Dim cb = CompHorner(s, New Double() {3.0#, 2.0# * a, b})
            Dim cc = CompHorner(s, New Double() {1.0#, a, b, c})
            Dim p = cb - ca * ca / 3.0#
            Dim q = 2.0# * ca * ca * ca / 27.0# - ca * cb / 3.0# + cc
            Dim disc = q * q / 4.0# + p * p * p / 27.0#
            ' error bounds of f(s) and f'(s) from compensated Horner (about 4.4E-31 x the sum of the |terms|): a disc
            ' within them may hide a close pair, which the trigonometric branch and SplitClosePair classify
            Dim absS = Math.Abs(s)
            Dim e3 = 1.0E-30 * (((absS + Math.Abs(a)) * absS + Math.Abs(b)) * absS + Math.Abs(c))
            Dim e2 = 1.0E-30 * ((3.0# * absS + 2.0# * Math.Abs(a)) * absS + Math.Abs(b))
            If p >= 0.0# OrElse disc > 0.000000000001 * (q * q / 4.0# + Math.Abs(p * p * p) / 27.0#) + Math.Abs(q) / 2.0# * e3 + p * p / 9.0# * e2 Then
                'one real root: u^3 = -q/2 - sign(q) sqrt(disc) has no cancellation, t = u - p/(3u)
                Dim w = -q / 2.0# - If(q >= 0.0#, Math.Sqrt(disc), -Math.Sqrt(disc))
                Dim t = 0.0#
                If w <> 0.0# Then
                    Dim u = Cbrt(w)
                    t = u - p / (3.0# * u)
                End If
                Return New List(Of Double) From {PolishCubicRoot(a, b, c, s + (t - ca / 3.0#))}
            End If
            Dim m = 2.0# * Math.Sqrt(-p / 3.0#)
            Dim arg = 3.0# * q / (p * m)
            If arg > 1.0# Then arg = 1.0#
            If arg < -1.0# Then arg = -1.0#
            Dim theta = Math.Acos(arg) / 3.0#
            Dim roots As New List(Of Double)
            For k = 0 To 2
                roots.Add(s + (m * Math.Cos(theta - 2.0# * Math.PI * k / 3.0#) - ca / 3.0#))
            Next
            If Math.Abs(arg) > 0.999999 Then Return SplitClosePair(a, b, c, roots)
            For k = 0 To 2
                roots(k) = PolishCubicRoot(a, b, c, roots(k))
            Next
            Return roots
        End Function

        ''' <summary>
        ''' Recomputes the two close roots of a three-real-root cubic from the isolated one, which is well conditioned:
        ''' it is polished, and the pair follows from RefinePair around the midpoint of its two estimates.
        ''' </summary>
        Private Shared Function SplitClosePair(ByVal a As Double, ByVal b As Double, ByVal c As Double, ByVal roots As List(Of Double)) As List(Of Double)
            Dim i1 As Integer = 0
            Dim gap1 As Double = -1.0#
            For k = 0 To 2
                Dim g = Math.Min(Math.Abs(roots(k) - roots((k + 1) Mod 3)), Math.Abs(roots(k) - roots((k + 2) Mod 3)))
                If g > gap1 Then
                    gap1 = g
                    i1 = k
                End If
            Next
            Dim r1 = PolishCubicRoot(a, b, c, roots(i1))
            If r1 <> 0.0# Then
                Dim pair = RefinePair(a, b, c, r1, (roots((i1 + 1) Mod 3) + roots((i1 + 2) Mod 3)) / 2.0#)
                If pair IsNot Nothing Then Return pair
            End If
            For k = 0 To 2
                roots(k) = PolishCubicRoot(a, b, c, roots(k))
            Next
            Return roots
        End Function

        ''' <summary>
        ''' The close pair of a cubic whose third root r1 is known. With z = m + t (m any point next to the pair), the
        ''' t-cubic has f'(m) and f(m) from compensated Horner and its third root t1 = r1 - m; the pair follows from
        ''' Vieta's relations (product -f(m)/t1, sum (f'(m) - product)/t1), exact relative to the pair's own spread.
        ''' A pair whose discriminant is negative beyond rounding is complex (the trigonometric form invented it by
        ''' clamping arg) and is dropped. The pair member farther from zero is m + t, the other the product -c/r1 over
        ''' it, which keeps full relative precision for the liquid pair of an EOS at very low pressure (Z ~ B ~ 1E-11).
        ''' Returns Nothing when t1 is zero.
        ''' </summary>
        Private Shared Function RefinePair(ByVal a As Double, ByVal b As Double, ByVal c As Double, ByVal r1 As Double, ByVal m As Double) As List(Of Double)
            Dim ca = CompHorner(m, New Double() {3.0#, a})
            Dim cb = CompHorner(m, New Double() {3.0#, 2.0# * a, b})
            Dim cc = CompHorner(m, New Double() {1.0#, a, b, c})
            Dim t1 = PolishCubicRoot(ca, cb, cc, r1 - m)
            If t1 = 0.0# Then Return Nothing
            Dim pt = -cc / t1
            Dim st = (cb - pt) / t1
            Dim d = st * st - 4.0# * pt
            ' rounding of d, plus the error bounds of f(m) and f'(m) from compensated Horner (about 4.4E-31 x the sum
            ' of the |terms|): an exact double root one ulp away from m is still a double root
            Dim am = Math.Abs(m)
            Dim ec = 1.0E-30 * (((am + Math.Abs(a)) * am + Math.Abs(b)) * am + Math.Abs(c))
            Dim eb = 1.0E-30 * ((3.0# * am + 2.0# * Math.Abs(a)) * am + Math.Abs(b))
            Dim tol = 0.00000000000001 * (st * st + 4.0# * Math.Abs(pt)) + (4.0# * ec + 2.0# * Math.Abs(st) * eb) / Math.Abs(t1)
            If d < 0.0# Then
                If d < -tol Then Return New List(Of Double) From {r1}
                Dim zd = m + st / 2.0#
                Return New List(Of Double) From {r1, zd, zd}
            End If
            Dim h = (st + If(st < 0.0#, -Math.Sqrt(d), Math.Sqrt(d))) / 2.0#
            If h = 0.0# Then Return New List(Of Double) From {r1, m, m}
            Dim t3 = pt / h
            Dim zb = If(Math.Abs(m + h) >= Math.Abs(m + t3), m + h, m + t3)
            If zb = 0.0# Then Return New List(Of Double) From {r1, 0.0#, 0.0#}
            Return New List(Of Double) From {r1, zb, (-c / r1) / zb}
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

        ''' <summary>
        ''' Newton steps on a converged start of CalcRoots until the step is below 1E-12 relative. The
        ''' starts stop on an absolute residual of 1E-8, which leaves a liquid root with no correct digit
        ''' when the root itself is that small (a liquid at low pressure, B below about 1E-4). Returns
        ''' False when the point does not polish into a root (a residual below 1E-8 away from any root).
        ''' A multiple root (the triple root at a pure compound's critical point) converges only linearly
        ''' and stalls on rounding noise before the step gets that small; it is accepted when its residual
        ''' is at rounding level.
        ''' </summary>
        Private Shared Function PolishRoot(ByVal a As Double, ByVal b As Double, ByVal c As Double, ByVal d As Double, ByRef r As Double) As Boolean
            For k = 1 To 50
                Dim fi = a * r * r * r + b * r * r + c * r + d
                Dim dfidr = 3 * a * r * r + 2 * b * r + c
                If Double.IsNaN(dfidr) OrElse Double.IsInfinity(dfidr) Then Return False
                If dfidr = 0.0# Then Exit For
                Dim stp = fi / dfidr
                r -= stp
                If Double.IsNaN(r) OrElse Double.IsInfinity(r) Then Return False
                If Math.Abs(stp) <= 0.000000000001 * Math.Max(Math.Abs(r), Double.Epsilon) Then Return True
            Next
            Dim fr = a * r * r * r + b * r * r + c * r + d
            Dim scale = Math.Abs(a * r * r * r) + Math.Abs(b * r * r) + Math.Abs(c * r) + Math.Abs(d)
            Return Math.Abs(fr) <= 0.00000000000001 * scale
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

            If cnt >= 1000 OrElse Not PolishRoot(a, b, c, d, r) Then
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

            If cnt >= 1000 OrElse Not PolishRoot(a, b, c, d, r) Then
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

            If cnt >= 1000 OrElse Not PolishRoot(a, b, c, d, r) Then
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