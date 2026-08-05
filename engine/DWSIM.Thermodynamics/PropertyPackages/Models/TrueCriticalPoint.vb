'    Critical Point Calculation Routines (PR & SRK) 
'    Copyright 2008-2026 Daniel Wagner O. de Medeiros
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

Imports DWSIM.MathOps.MathEx.Common
Imports DWSIM.MathOps.MathEx

''' <summary>
''' Contains algorithms for calculating true (mixture) critical properties from component data.
''' </summary>
Namespace Utilities.TCP

    ''' <summary>
    ''' Unified Heidemann-Khalil true-critical-point solver for the generalized cubic EOS. The EOS is
    ''' selected by the constructor argument (CubicCP.EOS_PR / EOS_SRK / EOS_PR78); the root-finding logic
    ''' (multi-root volume scan, Brent, LU null vector) is EOS-independent, and the critical matrix, cubic
    ''' form and pressure come from CubicCP. Replaces the former per-EOS Methods_SRK / Methods_PR78 classes.
    ''' </summary>
    <System.Serializable()> Public Class Methods

        Private ReadOnly eosType As Integer
        ' EOS-specific root-finder tuning (preserved from the former per-EOS classes: SRK was tuned
        ' differently from PR/PR78 and a couple of cases only bracket with its grid).
        Private ReadOnly s_nsubV As Integer     ' CRITPT_PR critical-volume scan subdivisions
        Private ReadOnly s_nsubT As Integer     ' TRIPLESUM critical-temperature scan subdivisions
        Private ReadOnly s_itmaxT As Integer    ' TRIPLESUM temperature Brent max iterations
        Private ReadOnly s_tolTC2 As Double     ' TCRIT2 temperature Brent tolerance

        Sub New()
            Me.New(CubicCP.EOS_PR)
        End Sub

        Sub New(ByVal eos As Integer)
            eosType = eos
            If eos = CubicCP.EOS_SRK Then
                s_nsubV = 25 : s_nsubT = 50 : s_itmaxT = 100 : s_tolTC2 = 0.000000000001
            Else
                s_nsubV = 100 : s_nsubT = 20 : s_itmaxT = 1000 : s_tolTC2 = 0.00000001
            End If
        End Sub

        Function CRITPT_PR(ByVal Vz, ByVal VTc, ByVal VPc, ByVal VVc, ByVal Vw, ByVal VKIj, Optional ByVal Vinf = 0) As ArrayList

            Dim res As New ArrayList

            Dim V, Vc_sup, Vc_inf, Tcm, Pcm As Double

            Dim stmp(2)
            Dim n, R As Double
            Dim i As Integer

            n = Vz.Length - 1

            Dim Tc(n), Pc(n) As Double
            Dim b As Double

            'estimar temperatura e pressao criticas iniciais

            R = 8.314

            i = 0
            Do
                Tc(i) = VTc(i)
                Pc(i) = VPc(i)
                Tcm += Vz(i) * VTc(i)
                Pcm += Vz(i) * VPc(i)
                i = i + 1
            Loop Until i = n + 1

            i = 0
            b = 0
            Do
                b += Vz(i) * 0.0778 * R * Tc(i) / Pc(i)
                i = i + 1
            Loop Until i = n + 1

            'estimar temperatura e pressao criticas iniciais

            Vc_inf = 4 * b
            If Vinf <> 0 Then Vc_inf = Vinf
            Vc_sup = b

            Dim fV, fV_inf, nsub, delta_Vc As Double

            nsub = s_nsubV

            delta_Vc = (Vc_sup - Vc_inf) / nsub

            Do
restart:        fV = TRIPLESUM(Vc_inf, Vz, VTc, VPc, VVc, Vw, VKIj)
                Vc_inf = Vc_inf + delta_Vc
                fV_inf = TRIPLESUM(Vc_inf, Vz, VTc, VPc, VVc, Vw, VKIj)
            Loop Until fV * fV_inf < 0 Or Vc_inf <= b
            Vc_sup = Vc_inf - delta_Vc
            'Vc_inf = Vc_inf - delta_Vc

            If Vc_inf <= b Then GoTo Final2

            'metodo de Brent para encontrar Vc

            Dim aaa, bbb, ccc, ddd, eee, min11, min22, faa, fbb, fcc, ppp, qqq, rrr, sss, tol11, xmm As Double
            Dim ITMAX2 As Integer = 100
            Dim iter2 As Integer

            aaa = Vc_inf
            bbb = Vc_sup
            ccc = Vc_sup

            faa = TRIPLESUM(Vc_inf, Vz, VTc, VPc, VVc, Vw, VKIj)
            fbb = TRIPLESUM(Vc_sup, Vz, VTc, VPc, VVc, Vw, VKIj)
            fcc = fbb

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
                tol11 = 0.000001
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
                fbb = TRIPLESUM(bbb, Vz, VTc, VPc, VVc, Vw, VKIj)
                iter2 += 1
            Loop Until iter2 = ITMAX2

Final3:

            V = bbb

            Dim T, P

            T = TCRIT2(V, Vz, VTc, VPc, VVc, Vw, VKIj)

            P = PCRIT(T, V, Vz, VTc, VPc, Vw, VKIj)

            If P < 0 Then
                Vc_inf += 2 * delta_Vc
                GoTo restart
            End If

            stmp(0) = T
            stmp(1) = P
            stmp(2) = V

            If T > 0.5 * Tcm And T < 2 * Tcm And P > 0.3 * Pcm And P < 4 * Pcm Then
                res.Add(stmp.Clone)
            End If

            If Vc_inf <= b Then
                GoTo Final2
            Else
                Vc_inf += 2 * delta_Vc
                GoTo restart
            End If

Final2:

            ' If the coarse volume scan bracketed no critical point (it can miss on borderline mixtures),
            ' fall back to the robust analytical generic solver rather than returning nothing.
            If res.Count = 0 Then
                Dim vzd(n) As Double, vtcd(n) As Double, vpcd(n) As Double, vwd(n) As Double
                Dim vkd(n, n) As Double
                Dim ii As Integer, jj As Integer
                For ii = 0 To n
                    vzd(ii) = Vz(ii) : vtcd(ii) = VTc(ii) : vpcd(ii) = VPc(ii) : vwd(ii) = Vw(ii)
                    For jj = 0 To n
                        vkd(ii, jj) = VKIj(ii, jj)
                    Next
                Next
                Dim gm As New GenericMethod
                gm.QMatrixAnalytic = Function(Tx, Vx, Vzi) CubicCP.QMatrix(eosType, Tx, Vx, Vzi, vtcd, vpcd, vwd, vkd)
                gm.CubicFormAnalytic = Function(Dnx, Tx, Vx, Vzi) CubicCP.CubicForm(eosType, Dnx, Tx, Vx, Vzi, vtcd, vpcd, vwd, vkd)
                gm.CalcP = Function(Tx, Vx, Vzi) CubicCP.PressureCubic(eosType, Tx, Vx, Vzi, vtcd, vpcd, vwd, vkd)
                Dim V0 As Double = 0, T0 As Double = 0
                For ii = 0 To n
                    V0 += 0.08664 * 8.314 * vzd(ii) * vtcd(ii) / vpcd(ii)
                    T0 += vzd(ii) * vtcd(ii)
                Next
                For Each item In gm.CriticalPoint(vzd, V0, T0)
                    Dim st(2) As Object
                    st(0) = item(0) : st(1) = item(1) : st(2) = item(2)
                    res.Add(st)
                Next
            End If

            CRITPT_PR = res

        End Function

        Function QIJ_HES_MAT(ByVal T, ByVal V, ByVal Vz, ByVal VTc, ByVal VPc, ByVal VVc, ByVal Vw, ByVal VKIj) As Mapack.Matrix
            Return CubicCP.QMatrix(eosType, T, V, Vz, VTc, VPc, Vw, VKIj)
        End Function

        Function TRIPLESUM(ByVal V, ByVal Vz, ByVal VTc, ByVal VPc, ByVal VVc, ByVal Vw, ByVal VKIj) As Double

            Dim T, Tc_sup, Tc_inf As Double

            Dim i, j As Integer
            Dim n As Double

            Dim am, bm, R As Double

            n = Vz.Length - 1

            Dim Dn(n) As Double

            Dim ai(n), b(n), c(n), tmp(2, n + 1), a(n, n), am2(n) As Double
            Dim Tc(n), Pc(n), Vc(n), Zc(n), w(n), alpha(n), Tr(n) As Double

            Tc_inf = MathEx.Common.Min(VTc) * 0.5
            Tc_sup = MathEx.Common.Max(VTc) * 1.5

            Dim fT, fT_inf, nsub, delta_Tc As Double

            nsub = s_nsubT

            delta_Tc = (Tc_sup - Tc_inf) / nsub

            Do
                fT = QIJ_HES_MAT(Tc_inf, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
                Tc_inf = Tc_inf + delta_Tc
                fT_inf = QIJ_HES_MAT(Tc_inf, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            Loop Until fT * fT_inf < 0 Or Tc_inf > Tc_sup
            Tc_sup = Tc_inf
            Tc_inf = Tc_inf - delta_Tc

            'metodo de Brent para encontrar Tc

            Dim aa, bb, cc, dd, ee, min1, min2, fa, fb, fc, pp, qq, rr, ss, tol1, xm As Double
            Dim ITMAX As Integer = s_itmaxT
            Dim iter As Integer

            aa = Tc_inf
            bb = Tc_sup
            cc = Tc_sup

            fa = QIJ_HES_MAT(aa, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            fb = QIJ_HES_MAT(bb, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            fc = fb

            iter = 0
            Do
                If (fb > 0 And fc > 0) Or (fb < 0 And fc < 0) Then
                    cc = aa
                    fc = fa
                    dd = bb - aa
                    ee = dd
                End If
                If Math.Abs(fc) < Math.Abs(fb) Then
                    aa = bb
                    bb = cc
                    cc = aa
                    fa = fb
                    fb = fc
                    fc = fa
                End If
                tol1 = 0.00000001
                xm = 0.5 * (cc - bb)
                If (Math.Abs(xm) <= tol1) Or (fb = 0) Then GoTo Final
                If Math.Abs(fb) < tol1 Then GoTo Final
                If (Math.Abs(ee) >= tol1) And (Math.Abs(fa) > Math.Abs(fb)) Then
                    ss = fb / fa
                    If aa = cc Then
                        pp = 2 * xm * ss
                        qq = 1 - ss
                    Else
                        qq = fa / fc
                        rr = fb / fc
                        pp = ss * (2 * xm * qq * (qq - rr) - (bb - aa) * (rr - 1))
                        qq = (qq - 1) * (rr - 1) * (ss - 1)
                    End If
                    If pp > 0 Then qq = -qq
                    pp = Math.Abs(pp)
                    min1 = 3 * xm * qq - Math.Abs(tol1 * qq)
                    min2 = Math.Abs(ee * qq)
                    Dim tvar As Double
                    If min1 < min2 Then tvar = min1
                    If min1 > min2 Then tvar = min2
                    If 2 * pp < tvar Then
                        ee = dd
                        dd = pp / qq
                    Else
                        dd = xm
                        ee = dd
                    End If
                Else
                    dd = xm
                    ee = dd
                End If
                aa = bb
                fa = fb
                If (Math.Abs(dd) > tol1) Then
                    bb += dd
                Else
                    bb += Math.Sign(xm) * tol1
                End If
                fb = QIJ_HES_MAT(bb, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
                iter += 1
            Loop Until iter = ITMAX
Final:
            T = bb
            If iter = ITMAX Then GoTo Final2

            Dim MA As Mapack.Matrix, Dn0(n) As Double
            Dim MA_(n, n), MB_(n), Dn0_(n) As Double

            'Dim MP As New DLLXnumbers.Xnumbers
            MA = QIJ_HES_MAT(T, V, Vz, VTc, VPc, VVc, Vw, VKIj)

            Dim m2 As Mapack.Matrix = New Mapack.Matrix(MA.Rows, 1)

            For i = 0 To n
                For j = 0 To n
                    MA_(i, j) = MA(i, j)
                Next
                MB_(i) = Double.Epsilon
            Next

            Try
                Dim trg As New Mapack.LuDecomposition(MA)
                i = 0
                Do
                    m2(i, 0) = 0
                    i = i + 1
                Loop Until i = n + 1
                m2(n, 0) = trg.UpperTriangularFactor(n, n)
                Dim m3 As Mapack.Matrix = trg.UpperTriangularFactor.Solve(m2)
                i = 0
                Do
                    Dn0(i) = m3(i, 0)
                    i = i + 1
                Loop Until i = n + 1
            Catch ex As Exception
                i = 0
                Do
                    Dn0(i) = 0
                    i = i + 1
                Loop Until i = n + 1
            End Try

            Dim soma_Dn = 0.0#
            i = 0
            Do
                soma_Dn += Math.Abs(Dn0(i))
                i = i + 1
            Loop Until i = n + 1

            i = 0
            Do
                Dn(i) = Dn0(i) / soma_Dn
                i = i + 1
            Loop Until i = n + 1

            Dim TS As Double = CubicCP.CubicForm(eosType, Dn, T, V, Vz, VTc, VPc, Vw, VKIj)

Final2:

            TRIPLESUM = TS

        End Function

        Function TCRIT(ByVal V, ByVal Vz, ByVal VTc, ByVal VPc, ByVal VVc, ByVal Vw, ByVal VKIj)

            'Dim MP As New DLLXnumbers.Xnumbers
            'MP.DigitsMax = 20

            Dim T, Tc_sup, Tc_inf As Double

            Dim n As Double

            n = Vz.Length - 1

            Dim Dn(n)

            Dim ai(n), b(n), c(n), tmp(2, n + 1), a(n, n), am2(n)
            Dim Tc(n), Pc(n), Vc(n), Zc(n), w(n), alpha(n), Tr(n)

            Tc_inf = MathEx.Common.Min(VTc) * 0.5
            Tc_sup = MathEx.Common.Max(VTc) * 1.5

            Dim fT, fT_inf, nsub, delta_Tc As Double

            nsub = 50

            delta_Tc = (Tc_sup - Tc_inf) / nsub

            Do
                fT = QIJ_HES_MAT(Tc_inf, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
                Tc_inf = Tc_inf + delta_Tc
                fT_inf = QIJ_HES_MAT(Tc_inf, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            Loop Until fT * fT_inf < 0 Or Tc_inf > Tc_sup
            Tc_sup = Tc_inf
            Tc_inf = Tc_inf - delta_Tc

            'metodo de Brent para encontrar Tc

            Dim aa, bb, cc, dd, ee, min1, min2, fa, fb, fc, pp, qq, rr, ss, tol1, xm As Double
            Dim ITMAX As Integer = 100
            Dim iter As Integer

            aa = Tc_inf
            bb = Tc_sup
            cc = Tc_sup

            fa = QIJ_HES_MAT(aa, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            fb = QIJ_HES_MAT(bb, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            fc = fb

            iter = 0
            Do
                If (fb > 0 And fc > 0) Or (fb < 0 And fc < 0) Then
                    cc = aa
                    fc = fa
                    dd = bb - aa
                    ee = dd
                End If
                If Math.Abs(fc) < Math.Abs(fb) Then
                    aa = bb
                    bb = cc
                    cc = aa
                    fa = fb
                    fb = fc
                    fc = fa
                End If
                tol1 = 0.000000000001
                xm = 0.5 * (cc - bb)
                If (Math.Abs(xm) <= tol1) Or (fb = 0) Then GoTo Final
                If (Math.Abs(ee) >= tol1) And (Math.Abs(fa) > Math.Abs(fb)) Then
                    ss = fb / fa
                    If aa = cc Then
                        pp = 2 * xm * ss
                        qq = 1 - ss
                    Else
                        qq = fa / fc
                        rr = fb / fc
                        pp = ss * (2 * xm * qq * (qq - rr) - (bb - aa) * (rr - 1))
                        qq = (qq - 1) * (rr - 1) * (ss - 1)
                    End If
                    If pp > 0 Then qq = -qq
                    pp = Math.Abs(pp)
                    min1 = 3 * xm * qq - Math.Abs(tol1 * qq)
                    min2 = Math.Abs(ee * qq)
                    Dim tvar As Double
                    If min1 < min2 Then tvar = min1
                    If min1 > min2 Then tvar = min2
                    If 2 * pp < tvar Then
                        ee = dd
                        dd = pp / qq
                    Else
                        dd = xm
                        ee = dd
                    End If
                Else
                    dd = xm
                    ee = dd
                End If
                aa = bb
                fa = fb
                If (Math.Abs(dd) > tol1) Then
                    bb += dd
                Else
                    bb += Math.Sign(xm) * tol1
                End If
                fb = QIJ_HES_MAT(bb, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
                iter += 1
            Loop Until iter = ITMAX
Final:
            T = bb
            If iter = ITMAX Then GoTo Final2

Final2:

            TCRIT = T

        End Function

        Function PCRIT(ByVal T, ByVal V, ByVal Vx, ByVal VTc, ByVal VPc, ByVal Vw, ByVal VKIj)
            Return CubicCP.PressureCubic(eosType, T, V, Vx, VTc, VPc, Vw, VKIj)
        End Function

        Function STABILITY_CURVE(ByVal Vz As Object, ByVal VTc As Object, ByVal VPc As Object, ByVal VVc As Object, ByVal Vw As Object, ByVal VKIj As Object, Optional ByVal Vmax As Double = 0, Optional ByVal delta As Double = 40, Optional ByVal multipl As Integer = 15) As ArrayList

            'Dim MP As New DLLXnumbers.Xnumbers

            Dim V, Vmin, deltaV As Double

            Dim stmp(2)
            Dim n, R, P, T As Double
            Dim i As Integer

            n = Vz.Length - 1

            Dim Tc(n), Pc(n)
            Dim b As Double

            'estimar temperatura e pressao criticas iniciais

            R = 8.314

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Tc(i) = VTc(i)
                    Pc(i) = VPc(i)
                End If
                i = i + 1
            Loop Until i = n + 1

            i = 0
            b = 0
            Do
                b += Vz(i) * 0.0778 * R * Tc(i) / Pc(i)
                i = i + 1
            Loop Until i = n + 1

            'estimar temperatura e pressao criticas iniciais

            If Vmax = 0 Then Vmax = b * multipl
            Vmin = b * 1.05

            deltaV = (Vmax - Vmin) / 100 ' delta

            Dim result As ArrayList = New ArrayList()

            V = Vmax
            Do
                T = TCRIT2(V, Vz, VTc, VPc, VVc, Vw, VKIj)
                'P = 0.307 * 8.314 * T / V
                P = PCRIT(T, V, Vz, VTc, VPc, Vw, VKIj)
                If P < 0 Then Exit Do
                result.Add(New Object() {T, P})
                V -= deltaV
            Loop Until V <= Vmin

            STABILITY_CURVE = result

        End Function

        Function TCRIT2(ByVal V, ByVal Vz, ByVal VTc, ByVal VPc, ByVal VVc, ByVal Vw, ByVal VKIj)

            'Dim MP As New DLLXnumbers.Xnumbers
            'MP.DigitsMax = 20

            Dim T, Tc_sup, Tc_inf As Double

            Dim n As Double

            n = Vz.Length - 1

            Dim Dn(n)

            Dim ai(n), b(n), c(n), tmp(2, n + 1), a(n, n), am2(n)
            Dim Tc(n), Pc(n), Vc(n), Zc(n), w(n), alpha(n), Tr(n)

            Tc_inf = Min(VTc) * 0.5
            Tc_sup = Max(VTc) * 1.5

            Dim fT, fT_inf, nsub, delta_Tc As Double

            nsub = 50

            delta_Tc = (Tc_sup - Tc_inf) / nsub

            Do
                fT = QIJ_HES_MAT(Tc_inf, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
                Tc_inf = Tc_inf + delta_Tc
                fT_inf = QIJ_HES_MAT(Tc_inf, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            Loop Until fT * fT_inf < 0 Or Tc_inf > Tc_sup
            Tc_sup = Tc_inf
            Tc_inf = Tc_inf - delta_Tc

            'metodo de Brent para encontrar Tc

            Dim aa, bb, cc, dd, ee, min1, min2, fa, fb, fc, pp, qq, rr, ss, tol1, xm As Double
            Dim ITMAX As Integer = 100
            Dim iter As Integer

            aa = Tc_inf
            bb = Tc_sup
            cc = Tc_sup

            fa = QIJ_HES_MAT(aa, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            fb = QIJ_HES_MAT(bb, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
            fc = fb

            iter = 0
            Do
                If (fb > 0 And fc > 0) Or (fb < 0 And fc < 0) Then
                    cc = aa
                    fc = fa
                    dd = bb - aa
                    ee = dd
                End If
                If Math.Abs(fc) < Math.Abs(fb) Then
                    aa = bb
                    bb = cc
                    cc = aa
                    fa = fb
                    fb = fc
                    fc = fa
                End If
                tol1 = s_tolTC2
                xm = 0.5 * (cc - bb)
                If (Math.Abs(xm) <= tol1) Or (fb = 0) Then GoTo Final
                If Math.Abs(fb) < tol1 Then GoTo Final
                If (Math.Abs(ee) >= tol1) And (Math.Abs(fa) > Math.Abs(fb)) Then
                    ss = fb / fa
                    If aa = cc Then
                        pp = 2 * xm * ss
                        qq = 1 - ss
                    Else
                        qq = fa / fc
                        rr = fb / fc
                        pp = ss * (2 * xm * qq * (qq - rr) - (bb - aa) * (rr - 1))
                        qq = (qq - 1) * (rr - 1) * (ss - 1)
                    End If
                    If pp > 0 Then qq = -qq
                    pp = Math.Abs(pp)
                    min1 = 3 * xm * qq - Math.Abs(tol1 * qq)
                    min2 = Math.Abs(ee * qq)
                    Dim tvar As Double
                    If min1 < min2 Then tvar = min1
                    If min1 > min2 Then tvar = min2
                    If 2 * pp < tvar Then
                        ee = dd
                        dd = pp / qq
                    Else
                        dd = xm
                        ee = dd
                    End If
                Else
                    dd = xm
                    ee = dd
                End If
                aa = bb
                fa = fb
                If (Math.Abs(dd) > tol1) Then
                    bb += dd
                Else
                    bb += Math.Sign(xm) * tol1
                End If
                fb = QIJ_HES_MAT(bb, V, Vz, VTc, VPc, VVc, Vw, VKIj).Determinant
                iter += 1
            Loop Until iter = ITMAX
Final:
            T = bb
            If iter = ITMAX Then GoTo Final2

Final2:

            TCRIT2 = T

        End Function

    End Class

    ''' <summary>
    ''' Analytical (closed-form) building blocks of the Heidemann-Khalil true critical point for a
    ''' generalized cubic EOS, parameterized by <paramref name="eosType"/> (0 = Peng-Robinson,
    ''' 1 = SRK, 2 = Peng-Robinson 1978). QMatrix returns the critical Hessian
    ''' Q_ij = RT * d(ln phi_i)/d(n_j) at constant T and V; CubicForm returns the third-order cubic
    ''' form evaluated along the critical direction Dn. These generalize the per-EOS QIJ_HES_MAT /
    ''' TRIPLESUM routines and are consumed by GenericMethod to replace its finite differences.
    ''' </summary>
    Public Class CubicCP

        Public Const EOS_PR As Integer = 0
        Public Const EOS_SRK As Integer = 1
        Public Const EOS_PR78 As Integer = 2

        ' Returns { Omega_a, Omega_b, delta1, delta2 } for the EOS.
        Private Shared Function Consts(ByVal eosType As Integer) As Double()
            If eosType = EOS_SRK Then
                Return New Double() {0.42748, 0.08664, 1.0, 0.0}
            Else
                ' PR and PR78 share the volume translation
                Return New Double() {0.45724, 0.0778, 1.0 + Math.Sqrt(2.0), 1.0 - Math.Sqrt(2.0)}
            End If
        End Function

        Private Shared Function Kappa(ByVal eosType As Integer, ByVal w As Double) As Double
            Select Case eosType
                Case EOS_SRK
                    Return 0.48 + 1.574 * w - 0.176 * w ^ 2
                Case EOS_PR78
                    If w <= 0.491 Then
                        Return 0.37464 + 1.5422 * w - 0.26992 * w ^ 2
                    Else
                        Return 0.379642 + 1.48503 * w - 0.164423 * w ^ 2 + 0.016666 * w ^ 3
                    End If
                Case Else ' EOS_PR
                    Return 0.37464 + 1.54226 * w - 0.26992 * w ^ 2
            End Select
        End Function

        ' Builds the per-component and mixture cubic parameters and the F-functions shared by both routines.
        Private Shared Sub Setup(ByVal eosType As Integer, ByVal T As Double, ByVal V As Double, ByVal Vz As Double(),
                                 ByVal VTc As Double(), ByVal VPc As Double(), ByVal Vw As Double(), ByVal VKIj As Double(,),
                                 ByRef a As Double(,), ByRef b As Double(), ByRef am As Double, ByRef bm As Double,
                                 ByRef beta_ As Double(), ByRef alfa_ As Double(),
                                 ByRef F1 As Double, ByRef F3 As Double, ByRef F4 As Double, ByRef F5 As Double, ByRef F6 As Double)

            Dim R As Double = 8.314
            Dim n As Integer = Vz.Length - 1
            Dim cc = Consts(eosType)
            Dim Oa As Double = cc(0), Ob As Double = cc(1), delta1 As Double = cc(2), delta2 As Double = cc(3)

            Dim ai(n) As Double
            ReDim a(n, n)
            ReDim b(n)

            For i As Integer = 0 To n
                If Vz(i) <> 0 Then
                    Dim alpha = (1 + Kappa(eosType, Vw(i)) * (1 - (T / VTc(i)) ^ 0.5)) ^ 2
                    ai(i) = Oa * alpha * R ^ 2 * VTc(i) ^ 2 / VPc(i)
                    b(i) = Ob * R * VTc(i) / VPc(i)
                End If
            Next
            For i As Integer = 0 To n
                For j As Integer = 0 To n
                    a(i, j) = (ai(i) * ai(j)) ^ 0.5 * (1 - VKIj(i, j))
                Next
            Next

            am = 0 : bm = 0
            Dim sum_xa(n) As Double
            For i As Integer = 0 To n
                For j As Integer = 0 To n
                    am += Vz(i) * Vz(j) * a(i, j)
                    sum_xa(i) += Vz(j) * a(i, j)
                Next
                bm += Vz(i) * b(i)
            Next

            ReDim beta_(n)
            ReDim alfa_(n)
            For i As Integer = 0 To n
                beta_(i) = b(i) / bm
                alfa_(i) = sum_xa(i) / am
            Next

            Dim K As Double = V / bm
            F1 = 1 / (K - 1)
            F3 = 1 / (delta1 - delta2) * ((delta1 / (K + delta1)) ^ 2 - (delta2 / (K + delta2)) ^ 2)
            F4 = 1 / (delta1 - delta2) * ((delta1 / (K + delta1)) ^ 3 - (delta2 / (K + delta2)) ^ 3)
            F5 = 2 / (delta1 - delta2) * Math.Log((K + delta1) / (K + delta2))
            Dim F2 = 2 / (delta1 - delta2) * (delta1 / (K + delta1) - delta2 / (K + delta2))
            F6 = F2 - F5
        End Sub

        ''' <summary>Critical Hessian Q_ij = RT * d(ln phi_i)/d(n_j) at constant T, V.</summary>
        Public Shared Function QMatrix(ByVal eosType As Integer, ByVal T As Double, ByVal V As Double, ByVal Vz As Double(),
                                       ByVal VTc As Double(), ByVal VPc As Double(), ByVal Vw As Double(), ByVal VKIj As Double(,)) As Mapack.Matrix

            Dim R As Double = 8.314
            Dim n As Integer = Vz.Length - 1
            Dim a(,) As Double, b() As Double, am, bm As Double, beta_() As Double, alfa_() As Double
            Dim F1, F3, F4, F5, F6 As Double
            Setup(eosType, T, V, Vz, VTc, VPc, Vw, VKIj, a, b, am, bm, beta_, alfa_, F1, F3, F4, F5, F6)

            Dim Q As Mapack.Matrix = New Mapack.Matrix(n + 1, n + 1)
            For i As Integer = 0 To n
                For j As Integer = 0 To n
                    Dim dta As Double = If(i = j, 1.0, 0.0)
                    Q(i, j) = R * T * (dta / Vz(i) + (beta_(i) + beta_(j)) * F1 + beta_(i) * beta_(j) * F1 ^ 2) +
                              am / bm * (beta_(i) * beta_(j) * F3 - a(i, j) * F5 / am + (beta_(i) * beta_(j) - alfa_(i) * beta_(j) - alfa_(j) * beta_(i)) * F6)
                Next
            Next
            Return Q
        End Function

        ''' <summary>Pressure from the generalized cubic EOS at (T, molar volume V, composition).</summary>
        Public Shared Function PressureCubic(ByVal eosType As Integer, ByVal T As Double, ByVal V As Double, ByVal Vz As Double(),
                                             ByVal VTc As Double(), ByVal VPc As Double(), ByVal Vw As Double(), ByVal VKIj As Double(,)) As Double

            Dim R As Double = 8.314
            Dim a(,) As Double, b() As Double, am, bm As Double, beta_() As Double, alfa_() As Double
            Dim F1, F3, F4, F5, F6 As Double
            Setup(eosType, T, V, Vz, VTc, VPc, Vw, VKIj, a, b, am, bm, beta_, alfa_, F1, F3, F4, F5, F6)
            Dim cc = Consts(eosType)
            Dim delta1 As Double = cc(2), delta2 As Double = cc(3)
            Return R * T / (V - bm) - am / ((V + delta1 * bm) * (V + delta2 * bm))
        End Function

        ''' <summary>Third-order cubic form of the Heidemann-Khalil criterion along the critical direction Dn.</summary>
        Public Shared Function CubicForm(ByVal eosType As Integer, ByVal Dn As Double(), ByVal T As Double, ByVal V As Double, ByVal Vz As Double(),
                                         ByVal VTc As Double(), ByVal VPc As Double(), ByVal Vw As Double(), ByVal VKIj As Double(,)) As Double

            Dim R As Double = 8.314
            Dim n As Integer = Vz.Length - 1
            Dim a(,) As Double, b() As Double, am, bm As Double, beta_() As Double, alfa_() As Double
            Dim F1, F3, F4, F5, F6 As Double
            Setup(eosType, T, V, Vz, VTc, VPc, Vw, VKIj, a, b, am, bm, beta_, alfa_, F1, F3, F4, F5, F6)

            Dim a_ As Double = 0, b_ As Double = 0, af_ As Double = 0, n_ As Double = 0, sum_Dn3 As Double = 0
            For i As Integer = 0 To n
                For j As Integer = 0 To n
                    a_ += a(i, j) * Dn(i) * Dn(j) / am
                Next
                b_ += beta_(i) * Dn(i)
                af_ += alfa_(i) * Dn(i)
                n_ += Dn(i)
                sum_Dn3 += Dn(i) ^ 3 / Vz(i) ^ 2
            Next

            Return R * T * (-sum_Dn3 + 3 * n_ * (b_ * F1) ^ 2 + 2 * (b_ * F1) ^ 3) +
                   am / bm * (3 * b_ ^ 2 * (2 * af_ - b_) * (F3 + F6) - 2 * b_ ^ 3 * F4 - 3 * b_ * a_ * F6)
        End Function

    End Class

    ''' <summary>
    ''' Analytical building blocks of the Heidemann-Khalil true critical point for PRSV2. PRSV2 is a
    ''' Peng-Robinson cubic with a temperature-dependent kappa(Tr) and, crucially, the composition-dependent
    ''' Stryjek-Vera / Margules mixing rule a_ij = sqrt(a_i a_j)(1 - x_i k_ij - x_j k_ij2). That makes the
    ''' mixture "a" cubic in composition and breaks the closed-form partial-molar expressions CubicCP relies
    ''' on. Rather than hand-deriving the second and third composition derivatives of that mixing rule, the
    ''' critical matrix and the cubic form are obtained by forward-mode automatic differentiation
    ''' (second-order dual numbers) of the exact ln-fugacity, so the Margules rule is handled exactly and to
    ''' machine precision.
    ''' </summary>
    Public Class PRSV2CP

        ''' <summary>
        ''' Second-order forward-mode AD scalar along one seeded direction: value, first and second
        ''' directional derivative. Only the operations used by the cubic ln-fugacity are provided.
        ''' </summary>
        Private Structure Jet
            Public V As Double, D1 As Double, D2 As Double
            Public Sub New(v_ As Double)
                V = v_ : D1 = 0.0 : D2 = 0.0
            End Sub
            Public Sub New(v_ As Double, d1_ As Double, d2_ As Double)
                V = v_ : D1 = d1_ : D2 = d2_
            End Sub
            Public Shared Widening Operator CType(c As Double) As Jet
                Return New Jet(c)
            End Operator
            Public Shared Operator +(a As Jet, b As Jet) As Jet
                Return New Jet(a.V + b.V, a.D1 + b.D1, a.D2 + b.D2)
            End Operator
            Public Shared Operator -(a As Jet, b As Jet) As Jet
                Return New Jet(a.V - b.V, a.D1 - b.D1, a.D2 - b.D2)
            End Operator
            Public Shared Operator -(a As Jet) As Jet
                Return New Jet(-a.V, -a.D1, -a.D2)
            End Operator
            Public Shared Operator *(a As Jet, b As Jet) As Jet
                Return New Jet(a.V * b.V, a.D1 * b.V + a.V * b.D1,
                               a.D2 * b.V + 2.0 * a.D1 * b.D1 + a.V * b.D2)
            End Operator
            Public Shared Operator /(a As Jet, b As Jet) As Jet
                Dim q = a.V / b.V
                Dim q1 = (a.D1 - q * b.D1) / b.V
                Dim q2 = (a.D2 - 2.0 * q1 * b.D1 - q * b.D2) / b.V
                Return New Jet(q, q1, q2)
            End Operator
            Public Shared Function Sqrt(a As Jet) As Jet
                Dim s = Math.Sqrt(a.V)
                Return New Jet(s, a.D1 / (2.0 * s), a.D2 / (2.0 * s) - a.D1 * a.D1 / (4.0 * s * a.V))
            End Function
            Public Shared Function Log(a As Jet) As Jet
                Return New Jet(Math.Log(a.V), a.D1 / a.V, (a.D2 * a.V - a.D1 * a.D1) / (a.V * a.V))
            End Function
        End Structure

        Private Const Rg As Double = 8.314

        ' Pure-component attractive/repulsive parameters at T (composition-independent), PRSV2 alpha.
        Private Shared Sub PureParams(T As Double, VTc As Double(), VPc As Double(), Vw As Double(),
                                      Vk1 As Double(), Vk2 As Double(), Vk3 As Double(),
                                      ByRef ai As Double(), ByRef bi As Double())
            Dim nc As Integer = VTc.Length - 1
            ReDim ai(nc) : ReDim bi(nc)
            For i As Integer = 0 To nc
                Dim Tr As Double = T / VTc(i)
                Dim ci As Double
                If Vk1(i) * Vk2(i) * Vk3(i) <> 0.0 Then
                    ci = (0.378893 + 1.4897153 * Vw(i) - 0.17131848 * Vw(i) ^ 2 + 0.0196544 * Vw(i) ^ 3) +
                         (Vk1(i) + Vk2(i) * (Vk3(i) - Tr) * (1 - Tr ^ 0.5)) * (1 + Tr ^ 0.5) * (0.7 - Tr)
                Else
                    If Vw(i) <= 0.491 Then
                        ci = 0.37464 + 1.5422 * Vw(i) - 0.26992 * Vw(i) ^ 2
                    Else
                        ci = 0.379642 + 1.48503 * Vw(i) - 0.164423 * Vw(i) ^ 2 + 0.016666 * Vw(i) ^ 3
                    End If
                End If
                Dim alpha As Double = (1 + ci * (1 - Tr ^ 0.5)) ^ 2
                ai(i) = 0.45724 * alpha * Rg ^ 2 * VTc(i) ^ 2 / VPc(i)
                bi(i) = 0.0778 * Rg * VTc(i) / VPc(i)
            Next
        End Sub

        ''' <summary>
        ''' ln f_i = ln(phi_i * x_i * P) at constant temperature and total volume, together with its first and
        ''' second derivatives along the seeded mole-number direction dN. Mirrors PRSV2.Z_PR: the Margules
        ''' mixing rule and its partial molar (sum1 + sum2 + sum3) are evaluated in Jet arithmetic, so the AD
        ''' picks up the full composition dependence.
        ''' </summary>
        Private Shared Function LnFugTV(T As Double, Vtot As Double, N As Double(), dN As Double(),
                                        VTc As Double(), VPc As Double(), Vw As Double(),
                                        Vk1 As Double(), Vk2 As Double(), Vk3 As Double(),
                                        VKij As Double(,), VKij2 As Double(,)) As Jet()

            Dim nc As Integer = N.Length - 1
            Dim e1 As Double = 1.0 + Math.Sqrt(2.0), e2 As Double = 1.0 - Math.Sqrt(2.0)
            Dim dlt As Double = e1 - e2

            Dim ai() As Double = Nothing, bi() As Double = Nothing
            PureParams(T, VTc, VPc, Vw, Vk1, Vk2, Vk3, ai, bi)

            Dim Nd(nc) As Jet
            For i As Integer = 0 To nc : Nd(i) = New Jet(N(i), dN(i), 0.0) : Next

            Dim ntot As Jet = New Jet(0.0)
            For i As Integer = 0 To nc : ntot = ntot + Nd(i) : Next

            Dim x(nc) As Jet
            For i As Integer = 0 To nc : x(i) = Nd(i) / ntot : Next
            Dim v As Jet = New Jet(Vtot) / ntot

            ' Margules mixing rule
            Dim a(nc, nc) As Jet
            For i As Integer = 0 To nc
                For j As Integer = 0 To nc
                    Dim g As Jet = New Jet(Math.Sqrt(ai(i) * ai(j)))
                    a(i, j) = g * (New Jet(1.0) - x(i) * New Jet(VKij(i, j)) - x(j) * New Jet(VKij2(j, i)))
                Next
            Next

            Dim am As Jet = New Jet(0.0), bm As Jet = New Jet(0.0)
            For i As Integer = 0 To nc
                For j As Integer = 0 To nc
                    am = am + x(i) * x(j) * a(i, j)
                Next
                bm = bm + x(i) * New Jet(bi(i))
            Next

            ' Margules partial molar (mirrors PRSV2.Z_PR sum1/sum2/sum3)
            Dim aml2(nc) As Jet
            For i As Integer = 0 To nc
                Dim s1 As Jet = New Jet(0.0), s2 As Jet = New Jet(0.0), s3 As Jet = New Jet(0.0)
                For j As Integer = 0 To nc
                    If i <> j Then
                        s2 = s2 + x(i) * x(j) * Jet.Sqrt(a(i, i) * a(j, j)) *
                             (x(j) * New Jet(VKij2(j, i)) - (New Jet(1.0) - x(i)) * New Jet(VKij(i, j)))
                    End If
                    For k As Integer = 0 To nc
                        If i <> j AndAlso k > j AndAlso k <> i Then
                            s3 = s3 + x(j) * x(k) * Jet.Sqrt(a(j, j) * a(k, k)) *
                                 (x(j) * New Jet(VKij(j, k)) + x(k) * New Jet(VKij2(k, j)))
                        End If
                    Next
                    s1 = s1 + x(j) * a(i, j)
                Next
                aml2(i) = s1 + s2 + s3
            Next

            Dim P As Jet = New Jet(Rg * T) / (v - bm) - am / ((v + New Jet(e1) * bm) * (v + New Jet(e2) * bm))
            Dim Z As Jet = P * v / New Jet(Rg * T)
            Dim AG As Jet = am * P / New Jet((Rg * T) ^ 2)
            Dim BG As Jet = bm * P / New Jet(Rg * T)
            Dim L As Jet = Jet.Log((Z + New Jet(e1) * BG) / (Z + New Jet(e2) * BG))

            Dim res(nc) As Jet
            For i As Integer = 0 To nc
                Dim bib As Jet = New Jet(bi(i)) / bm
                Dim lnphi As Jet = bib * (Z - New Jet(1.0)) - Jet.Log(Z - BG) -
                                   (AG / (BG * New Jet(dlt))) * (New Jet(2.0) * aml2(i) / am - bib) * L
                res(i) = lnphi + Jet.Log(x(i) * P)
            Next
            Return res
        End Function

        ''' <summary>
        ''' ln f_i = ln(phi_i * x_i * P) at constant T and total volume (values only). PRSV2 has no other
        ''' temperature-volume fugacity entry point; this is also what the finite-difference validation of the
        ''' automatic derivatives differentiates.
        ''' </summary>
        Public Shared Function LnFugTVValues(T As Double, V As Double, N As Double(),
                                             VTc As Double(), VPc As Double(), Vw As Double(),
                                             Vk1 As Double(), Vk2 As Double(), Vk3 As Double(),
                                             VKij As Double(,), VKij2 As Double(,)) As Double()
            Dim nc As Integer = N.Length - 1
            Dim dN(nc) As Double
            Dim r = LnFugTV(T, V, N, dN, VTc, VPc, Vw, Vk1, Vk2, Vk3, VKij, VKij2)
            Dim res(nc) As Double
            For i As Integer = 0 To nc : res(i) = r(i).V : Next
            Return res
        End Function

        ''' <summary>Critical Hessian Q_ij = RT * d(ln f_i)/d(n_j) at constant T, V, by first-order AD.</summary>
        Public Shared Function QMatrix(T As Double, V As Double, Vz As Double(),
                                       VTc As Double(), VPc As Double(), Vw As Double(),
                                       Vk1 As Double(), Vk2 As Double(), Vk3 As Double(),
                                       VKij As Double(,), VKij2 As Double(,)) As Mapack.Matrix
            Dim nc As Integer = Vz.Length - 1
            Dim Q As Mapack.Matrix = New Mapack.Matrix(nc + 1, nc + 1)
            Dim dN(nc) As Double
            For j As Integer = 0 To nc
                For k As Integer = 0 To nc : dN(k) = 0.0 : Next
                dN(j) = 1.0
                Dim r = LnFugTV(T, V, Vz, dN, VTc, VPc, Vw, Vk1, Vk2, Vk3, VKij, VKij2)
                For i As Integer = 0 To nc
                    Q(i, j) = Rg * T * r(i).D1
                Next
            Next
            Return Q
        End Function

        ''' <summary>
        ''' Heidemann-Khalil cubic form along the critical direction Dn, by second-order AD: seeding the
        ''' direction Dn gives the second directional derivative of every ln f_i in a single pass, so the
        ''' triple contraction reduces to sum_i Dn_i * d2(ln f_i).
        ''' </summary>
        Public Shared Function CubicForm(Dn As Double(), T As Double, V As Double, Vz As Double(),
                                         VTc As Double(), VPc As Double(), Vw As Double(),
                                         Vk1 As Double(), Vk2 As Double(), Vk3 As Double(),
                                         VKij As Double(,), VKij2 As Double(,)) As Double
            Dim nc As Integer = Vz.Length - 1
            Dim r = LnFugTV(T, V, Vz, Dn, VTc, VPc, Vw, Vk1, Vk2, Vk3, VKij, VKij2)
            Dim ts As Double = 0.0
            For i As Integer = 0 To nc
                ts += Dn(i) * r(i).D2
            Next
            Return ts * Rg * T
        End Function

        ''' <summary>Pressure from the PRSV2 EOS (Margules mixing) at (T, molar volume V, composition).</summary>
        Public Shared Function PressureTV(T As Double, V As Double, Vz As Double(),
                                          VTc As Double(), VPc As Double(), Vw As Double(),
                                          Vk1 As Double(), Vk2 As Double(), Vk3 As Double(),
                                          VKij As Double(,), VKij2 As Double(,)) As Double
            Dim nc As Integer = Vz.Length - 1
            Dim e1 As Double = 1.0 + Math.Sqrt(2.0), e2 As Double = 1.0 - Math.Sqrt(2.0)
            Dim ai() As Double = Nothing, bi() As Double = Nothing
            PureParams(T, VTc, VPc, Vw, Vk1, Vk2, Vk3, ai, bi)
            Dim am As Double = 0, bm As Double = 0
            For i As Integer = 0 To nc
                For j As Integer = 0 To nc
                    Dim aij As Double = Math.Sqrt(ai(i) * ai(j)) * (1 - Vz(i) * VKij(i, j) - Vz(j) * VKij2(j, i))
                    am += Vz(i) * Vz(j) * aij
                Next
                bm += Vz(i) * bi(i)
            Next
            Return Rg * T / (V - bm) - am / ((V + e1 * bm) * (V + e2 * bm))
        End Function

    End Class

    Public Class GenericMethod

        ''' <summary>
        ''' Argument order: T (K), V (m3/mol), molar composition
        ''' </summary>
        Public FugacityTV As Func(Of Double, Double, Double(), Double())

        ''' <summary>
        ''' T (K), V (m3/mol)
        ''' </summary>
        Public CalcP As Func(Of Double, Double, Double(), Double)

        ''' <summary>
        ''' Optional analytical critical Hessian Q_ij = RT * d(ln phi_i)/d(n_j) at (T, V, composition). When
        ''' set, it replaces the finite-difference build of the critical matrix (see CubicCP.QMatrix).
        ''' </summary>
        Public QMatrixAnalytic As Func(Of Double, Double, Double(), Mapack.Matrix)

        ''' <summary>
        ''' Optional analytical Heidemann-Khalil cubic form evaluated along the critical direction Dn at
        ''' (T, V, composition). When set, it replaces the finite-difference-of-finite-differences build of
        ''' the third-order term (see CubicCP.CubicForm).
        ''' </summary>
        Public CubicFormAnalytic As Func(Of Double(), Double, Double, Double(), Double)

        Private Tit, Vmin, Vmax, Tmin, Tmax As Double

        Private Tcalc As Double

#Region "        Critical Point General Calculation Routines (EXPERIMENTAL)"

        Public Function dlnfug_i_dn_j(ByVal jidx As Integer, ByVal T As Double, ByVal V As Double, ByVal Vz As Double()) As Double()

            Dim n As Integer = Vz.Length - 1

            Dim Vz0 As Double() = Vz.Clone()

            Dim i As Integer

            For i = 0 To n
                If Vz0(i) = 0.0 Then Vz0(i) = 0.0000001
            Next

            Dim mres(n) As Double

            Dim d1(n), d2(n), d3(n), d4(n) As Double

            Dim h As Double = 0.0001

            Dim n2 = 1.0 - Vz0(jidx) * h
            Dim n3 = 1.0 + Vz0(jidx) * h

            Dim Vz2 = perturb_n(jidx, -h, Vz0)
            Dim P2 = CalcP.Invoke(T, V / n2, Vz2)

            Dim Vz3 = perturb_n(jidx, h, Vz0)
            Dim P3 = CalcP.Invoke(T, V / n3, Vz3)

            d2 = FugacityTV.Invoke(T, V / n2, Vz2).MultiplyConstY(P2).MultiplyY(Vz2).LogY()
            d3 = FugacityTV.Invoke(T, V / n3, Vz3).MultiplyConstY(P3).MultiplyY(Vz3).LogY()

            i = 0
            Do
                mres(i) = (d3(i) - d2(i)) / (2 * h)
                If Double.IsNaN(mres(i)) Then mres(i) = 0.0
                i = i + 1
            Loop Until i = n + 1

            Return mres

        End Function

        Public Function d2lnfug_i_dn_j_dn_k(ByVal jidx As Integer, ByVal kidx As Integer, ByVal T As Double, ByVal V As Double, ByVal Vz As Double()) As Double()

            Dim n As Integer = Vz.Length - 1

            Dim i As Integer

            Dim mres(n) As Double

            Dim Vz0 As Double() = Vz.Clone()

            For i = 0 To n
                If Vz0(i) = 0.0 Then Vz0(i) = 0.0000001
            Next

            Dim points1(n), points2(n) As Double

            Dim h As Double = 0.0001

            Dim n1 = 1.0 - Vz0(kidx) * h
            Dim n2 = 1.0 + Vz0(kidx) * h

            points1 = dlnfug_i_dn_j(jidx, T, V / n1, perturb_n(kidx, -h, Vz0))
            points2 = dlnfug_i_dn_j(jidx, T, V / n2, perturb_n(kidx, h, Vz0))

            i = 0
            Do
                mres(i) = (points2(i) - points1(i)) / (2 * h)
                i = i + 1
            Loop Until i = n + 1

            Return mres

        End Function

        Private Function perturb_n(ByVal i As Integer, ByVal dn As Double, ByVal Vx As Double()) As Double()

            Dim n As Integer = Vx.Length - 1
            Dim j As Integer = 0

            Dim ntot As Double = 1.0

            Dim Vn(n), Vn2(n) As Double

            j = 0
            Do
                Vn(j) = Vx(j) * ntot
                j = j + 1
            Loop Until j = n + 1

            Vn(i) = Vn(i) * (1.0 + dn)

            Return Vn.NormalizeY()

        End Function

        Public Function Qij(ByVal T As Double, ByVal V As Double, ByVal Vz As Double()) As Mapack.Matrix

            If QMatrixAnalytic IsNot Nothing Then Return QMatrixAnalytic.Invoke(T, V, Vz)

            Dim n As Integer = Vz.Length - 1

            Dim mat As Mapack.Matrix = New Mapack.Matrix(n + 1, n + 1)
            Dim el(n) As Object

            Dim i, j As Integer

            i = 0
            Do
                el(i) = dlnfug_i_dn_j(i, T, V, Vz)
                i = i + 1
            Loop Until i = n + 1

            i = 0
            Do
                j = 0
                Do
                    mat(i, j) = el(i)(j) * 8.314 * T
                    j = j + 1
                Loop Until j = n + 1
                i = i + 1
            Loop Until i = n + 1

            Return mat

        End Function

        Private Function QijDetBrent(ByVal T As Double, V As Double, Vz As Double()) As Double

            Dim mat As Mapack.Matrix = Qij(T, V, Vz)

            Return mat.Determinant

        End Function

        Private Function TripleSum(ByVal Dn As Double(), ByVal T As Double, ByVal V As Double, ByVal Vz As Double()) As Double

            If CubicFormAnalytic IsNot Nothing Then Return CubicFormAnalytic.Invoke(Dn, T, V, Vz)

            Dim n As Integer = Vz.Length - 1

            Dim mat(n, n) As Object
            Dim el(n) As Object

            Dim i, j, k As Integer

            i = 0
            Do
                j = 0
                Do
                    mat(i, j) = d2lnfug_i_dn_j_dn_k(i, j, T, V, Vz)
                    j = j + 1
                Loop Until j = n + 1
                i = i + 1
            Loop Until i = n + 1

            Dim ts As Double = 0.0

            i = 0
            Do
                j = 0
                Do
                    k = 0
                    Do
                        ts += mat(i, j)(k) * Dn(i) * Dn(j) * Dn(k) * 8.314 * T / 100.0
                        k = k + 1
                    Loop Until k = n + 1
                    j = j + 1
                Loop Until j = n + 1
                i = i + 1
            Loop Until i = n + 1

            Return ts

        End Function

        Private Function TripleSum2(ByVal V As Double, ByVal Vz As Double()) As Double

            Dim T As Double

            Dim i As Integer
            Dim n As Double

            n = Vz.Length - 1

            Dim Dn(n) As Double

            Dim brent As New BrentOpt.Brent

            T = brent.BrentOpt2(Tmin, Tmax, 10, 0.001, 1000,
                                Function(Tx)
                                    Return QijDetBrent(Tx, V, Vz)
                                End Function)

            Tcalc = T

            Dim MA As Mapack.Matrix, Dn0(n) As Double

            MA = Qij(T, V, Vz)

            Dim m2 As Mapack.Matrix = New Mapack.Matrix(MA.Rows, 1)

            Try
                Dim trg As New Mapack.LuDecomposition(MA)
                i = 0
                Do
                    m2(i, 0) = 0
                    i = i + 1
                Loop Until i = n + 1
                m2(n, 0) = trg.UpperTriangularFactor(n, n)
                Dim m3 As Mapack.Matrix = trg.UpperTriangularFactor.Solve(m2)
                i = 0
                Do
                    Dn0(i) = m3(i, 0)
                    i = i + 1
                Loop Until i = n + 1
            Catch ex As Exception
                i = 0
                Do
                    Dn0(i) = 0
                    i = i + 1
                Loop Until i = n + 1
            End Try

            Dim soma_Dn As Double = 0
            i = 0
            Do
                soma_Dn += Math.Abs(Dn0(i))
                i = i + 1
            Loop Until i = n + 1

            i = 0
            Do
                Dn(i) = Dn0(i) / soma_Dn
                i = i + 1
            Loop Until i = n + 1

            Tit = T

            Return TripleSum(Dn, T, V, Vz)

        End Function

        Function CriticalPoint(ByVal Vz As Double(), V0 As Double, T0 As Double) As List(Of Double())

            Dim res As New List(Of Double())

            Dim V As Double

            Tmin = T0 * 0.5
            Tmax = T0 * 2
            Vmin = 0.5 * V0
            Vmax = 4.0 * V0

            Tit = T0

            Dim brent As New BrentOpt.Brent

            Dim fV, fV2, delta_Vc, Viter As Double

            delta_Vc = (Vmax - Vmin) / 10

            Viter = Vmax

            Do
                fV = TripleSum2(Viter, Vz)
                Viter -= delta_Vc
                fV2 = TripleSum2(Viter, Vz)
            Loop Until fV * fV2 < 0.0 Or Viter <= Vmin

            If fV * fV2 >= 0.0 Then
                Return res
            End If

            V = brent.BrentOpt2(Viter, Viter + delta_Vc, 2, 0.0000000001, 100,
                                Function(Vi)
                                    Return TripleSum2(Vi, Vz)
                                End Function)

            'V = 0.0892478 / 1000.0

            Dim P = CalcP.Invoke(Tcalc, V, Vz)

            res.Add({Tcalc, P, V})

            Return res

        End Function

#End Region

    End Class

End Namespace