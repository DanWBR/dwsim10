'    Miscelaneous Math Functions for DWSIM
'    Copyright 2008 Daniel Wagner O. de Medeiros
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

Namespace MathEx.BrentOpt

    Public Class Brent

        Public fc As funcdelegate
        Delegate Function funcdelegate(ByVal x As Double, ByVal otherargs As Object) As Double

        Sub New()

        End Sub

        Sub DefineFuncDelegate(ByVal fg As funcdelegate)
            Me.fc = fg
        End Sub

        Function func(ByVal x As Double, ByVal otherargs As Object) As Double
            Return fc.Invoke(x, otherargs)
        End Function

        Function BrentOpt(ByVal minval As Double, ByVal maxval As Double, ByVal n As Integer, ByVal tol As Double, ByVal itmax As Integer, ByVal otherargs As Object) As Double

            Dim x_inf, x_sup, y_inf, y, delta_x As Double

            If minval < maxval Then
                x_inf = minval
                x_sup = maxval
            Else
                x_inf = maxval
                x_sup = minval
            End If

            delta_x = (x_sup - x_inf) / n

            ' The scan walks the bracket looking for a sign change. It used to step past x_sup and
            ' evaluate there before noticing, so a function defined only on [minval, maxval] was asked
            ' for a point outside it - the steam tables, scanned over 273.15..1073.15 K in 100 steps,
            ' were asked for the enthalpy at 1081.15 K on the last step, and the range check they now
            ' carry turned that into an error on a perfectly ordinary flash. The last point is clamped
            ' to x_sup instead, which also keeps the interval handed to the refinement below inside the
            ' function's domain when no sign change was found at all.
            Dim exhausted As Boolean = False
            Do
                y = func(x_inf, otherargs)
                x_inf = x_inf + delta_x
                If x_inf > x_sup Then
                    x_inf = x_sup
                    exhausted = True
                End If
                y_inf = func(x_inf, otherargs)
                ' <= rather than <, so a root sitting exactly on a scan point is not walked past.
                ' It is not a rare case: an enthalpy that came out of the same correlation lands on
                ' one exactly. A heater set to 80 C hands the outlet stream h(353.15 K), the scan
                ' steps 8 K from 273.15 and its eleventh point IS 353.15, the residual there is 0.0
                ' bit for bit, and the strict test read that as "no sign change" and ran to the end
                ' of the range. The stream then came back at 1065 K instead of 353.15.
                If y * y_inf <= 0 Then Exit Do
            Loop Until exhausted
            x_sup = x_inf - delta_x

            Dim aaa, bbb, ccc, ddd, eee, min11, min22, faa, fbb, fcc, ppp, qqq, rrr, sss, tol11, xmm As Double
            Dim ITMAX2 As Integer = itmax
            Dim iter2 As Integer

            aaa = x_inf
            bbb = x_sup
            ccc = x_sup

            faa = func(x_inf, otherargs)
            fbb = func(x_sup, otherargs)
            fcc = func(x_sup, otherargs)

            iter2 = 0
            Do
                If (fbb > 0 And fcc > 0) Or (fbb < 0 And fcc < 0) Then
                    ccc = aaa
                    fcc = faa
                    ddd = bbb - aaa
                    eee = ddd
                End If
                If Math.Abs(fcc) < Math.Abs(fbb) Then
                    aaa = bbb
                    bbb = ccc
                    ccc = aaa
                    faa = fbb
                    fbb = fcc
                    fcc = faa
                End If
                tol11 = tol
                xmm = 0.5 * (ccc - bbb)
                If (Math.Abs(xmm) <= tol11) Or (fbb = 0) Then GoTo Final3
                If Math.Abs(fbb) < tol11 Then GoTo Final3
                If (Math.Abs(eee) >= tol11) And (Math.Abs(faa) > Math.Abs(fbb)) Then
                    sss = fbb / faa
                    If aaa = ccc Then
                        ppp = 2 * xmm * sss
                        qqq = 1 - sss
                    Else
                        qqq = faa / fcc
                        rrr = fbb / fcc
                        ppp = sss * (2 * xmm * qqq * (qqq - rrr) - (bbb - aaa) * (rrr - 1))
                        qqq = (qqq - 1) * (rrr - 1) * (sss - 1)
                    End If
                    If ppp > 0 Then qqq = -qqq
                    ppp = Math.Abs(ppp)
                    min11 = 3 * xmm * qqq - Math.Abs(tol11 * qqq)
                    min22 = Math.Abs(eee * qqq)
                    Dim tvar2 As Double
                    If min11 < min22 Then tvar2 = min11
                    If min11 > min22 Then tvar2 = min22
                    If 2 * ppp < tvar2 Then
                        eee = ddd
                        ddd = ppp / qqq
                    Else
                        ddd = xmm
                        eee = ddd
                    End If
                Else
                    ddd = xmm
                    eee = ddd
                End If
                aaa = bbb
                faa = fbb
                If (Math.Abs(ddd) > tol11) Then
                    bbb += ddd
                Else
                    bbb += Math.Sign(xmm) * tol11
                End If
                fbb = func(bbb, otherargs)
                iter2 += 1
            Loop Until iter2 = ITMAX2

            Return bbb

Final3:

            Return bbb

        End Function

        Function BrentOpt2(ByVal minval As Double, ByVal maxval As Double, ByVal n As Integer, ByVal tol As Double, ByVal itmax As Integer, ByVal fx As Func(Of Double, Double)) As Double

            Dim x_inf, x_sup, y_inf, y, delta_x As Double

            If minval < maxval Then
                x_inf = minval
                x_sup = maxval
            Else
                x_inf = maxval
                x_sup = minval
            End If

            delta_x = (x_sup - x_inf) / n

            Do
                y = fx(x_inf)
                x_inf = x_inf + delta_x
                y_inf = fx(x_inf)
            Loop Until y * y_inf < 0 Or x_inf >= x_sup
            x_sup = x_inf - delta_x

            Dim aaa, bbb, ccc, ddd, eee, min11, min22, faa, fbb, fcc, ppp, qqq, rrr, sss, tol11, xmm As Double
            Dim ITMAX2 As Integer = itmax
            Dim iter2 As Integer

            aaa = x_inf
            bbb = x_sup
            ccc = x_sup

            faa = fx(x_inf)
            fbb = fx(x_sup)
            fcc = fx(x_sup)

            iter2 = 0
            Do
                If (fbb > 0 And fcc > 0) Or (fbb < 0 And fcc < 0) Then
                    ccc = aaa
                    fcc = faa
                    ddd = bbb - aaa
                    eee = ddd
                End If
                If Math.Abs(fcc) < Math.Abs(fbb) Then
                    aaa = bbb
                    bbb = ccc
                    ccc = aaa
                    faa = fbb
                    fbb = fcc
                    fcc = faa
                End If
                tol11 = tol
                xmm = 0.5 * (ccc - bbb)
                If (Math.Abs(xmm) <= tol11) Or (fbb = 0) Then GoTo Final3
                If Math.Abs(fbb) < tol11 Then GoTo Final3
                If (Math.Abs(eee) >= tol11) And (Math.Abs(faa) > Math.Abs(fbb)) Then
                    sss = fbb / faa
                    If aaa = ccc Then
                        ppp = 2 * xmm * sss
                        qqq = 1 - sss
                    Else
                        qqq = faa / fcc
                        rrr = fbb / fcc
                        ppp = sss * (2 * xmm * qqq * (qqq - rrr) - (bbb - aaa) * (rrr - 1))
                        qqq = (qqq - 1) * (rrr - 1) * (sss - 1)
                    End If
                    If ppp > 0 Then qqq = -qqq
                    ppp = Math.Abs(ppp)
                    min11 = 3 * xmm * qqq - Math.Abs(tol11 * qqq)
                    min22 = Math.Abs(eee * qqq)
                    Dim tvar2 As Double
                    If min11 < min22 Then tvar2 = min11
                    If min11 > min22 Then tvar2 = min22
                    If 2 * ppp < tvar2 Then
                        eee = ddd
                        ddd = ppp / qqq
                    Else
                        ddd = xmm
                        eee = ddd
                    End If
                Else
                    ddd = xmm
                    eee = ddd
                End If
                aaa = bbb
                faa = fbb
                If (Math.Abs(ddd) > tol11) Then
                    bbb += ddd
                Else
                    bbb += Math.Sign(xmm) * tol11
                End If
                fbb = fx(bbb)
                iter2 += 1
            Loop Until iter2 = ITMAX2

            Return bbb

Final3:

            Return bbb

        End Function

        Shared Function BrentOpt3(ByVal minval As Double, ByVal maxval As Double, ByVal n As Integer, ByVal tol As Double, ByVal itmax As Integer, ByVal fx As Func(Of Double, Double)) As Double

            Dim x_inf, x_sup, y_inf, y, delta_x As Double

            If minval < maxval Then
                x_inf = minval
                x_sup = maxval
            Else
                x_inf = maxval
                x_sup = minval
            End If

            delta_x = (x_sup - x_inf) / n

            Do
                y = fx(x_inf)
                x_inf = x_inf + delta_x
                y_inf = fx(x_inf)
            Loop Until y * y_inf < 0 Or x_inf >= x_sup
            x_sup = x_inf - delta_x

            Dim aaa, bbb, ccc, ddd, eee, min11, min22, faa, fbb, fcc, ppp, qqq, rrr, sss, tol11, xmm As Double
            Dim ITMAX2 As Integer = itmax
            Dim iter2 As Integer

            aaa = x_inf
            bbb = x_sup
            ccc = x_sup

            faa = fx(x_inf)
            fbb = fx(x_sup)
            fcc = fx(x_sup)

            iter2 = 0
            Do
                If (fbb > 0 And fcc > 0) Or (fbb < 0 And fcc < 0) Then
                    ccc = aaa
                    fcc = faa
                    ddd = bbb - aaa
                    eee = ddd
                End If
                If Math.Abs(fcc) < Math.Abs(fbb) Then
                    aaa = bbb
                    bbb = ccc
                    ccc = aaa
                    faa = fbb
                    fbb = fcc
                    fcc = faa
                End If
                tol11 = tol
                xmm = 0.5 * (ccc - bbb)
                If (Math.Abs(xmm) <= tol11) Or (fbb = 0) Then GoTo Final3
                If Math.Abs(fbb) < tol11 Then GoTo Final3
                If (Math.Abs(eee) >= tol11) And (Math.Abs(faa) > Math.Abs(fbb)) Then
                    sss = fbb / faa
                    If aaa = ccc Then
                        ppp = 2 * xmm * sss
                        qqq = 1 - sss
                    Else
                        qqq = faa / fcc
                        rrr = fbb / fcc
                        ppp = sss * (2 * xmm * qqq * (qqq - rrr) - (bbb - aaa) * (rrr - 1))
                        qqq = (qqq - 1) * (rrr - 1) * (sss - 1)
                    End If
                    If ppp > 0 Then qqq = -qqq
                    ppp = Math.Abs(ppp)
                    min11 = 3 * xmm * qqq - Math.Abs(tol11 * qqq)
                    min22 = Math.Abs(eee * qqq)
                    Dim tvar2 As Double
                    If min11 < min22 Then tvar2 = min11
                    If min11 > min22 Then tvar2 = min22
                    If 2 * ppp < tvar2 Then
                        eee = ddd
                        ddd = ppp / qqq
                    Else
                        ddd = xmm
                        eee = ddd
                    End If
                Else
                    ddd = xmm
                    eee = ddd
                End If
                aaa = bbb
                faa = fbb
                If (Math.Abs(ddd) > tol11) Then
                    bbb += ddd
                Else
                    bbb += Math.Sign(xmm) * tol11
                End If
                fbb = fx(bbb)
                iter2 += 1
            Loop Until iter2 = ITMAX2

            Return bbb

Final3:

            Return bbb

        End Function

    End Class

End Namespace
