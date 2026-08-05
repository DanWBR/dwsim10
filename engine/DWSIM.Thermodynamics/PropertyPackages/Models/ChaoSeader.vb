'    Grayson-Streed Property Package 
'    Copyright 2009 Daniel Wagner O. de Medeiros
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
Imports filehelpers
Imports DWSIM.MathOps.MathEx.PolySolve

''' <summary>
''' Contains auxiliary model classes for individual property package calculations, including EOS mixing rules,
''' activity coefficient models, and fluid property correlations.
''' </summary>
Namespace PropertyPackages.Auxiliary

    <System.Serializable()> Public Class CS

        Sub New()

        End Sub

        Public Function CalcLiqActCoeff(ByVal Vx As Object, ByVal VVL() As Double, ByVal VSP() As Double, ByVal T As Double) As Double()

            Dim n As Integer = UBound(VVL)

            Dim i As Integer
            Dim sumVS, sumV, S_ As Double

            Dim R As Double = 8314470.0

            sumV = 0
            sumVS = 0
            For i = 0 To n
                sumV += Vx(i) * VVL(i)
                sumVS += Vx(i) * VVL(i) * VSP(i)
            Next

            S_ = sumVS / sumV

            Dim lnac(n), ac(n) As Double

            For i = 0 To n
                lnac(i) = VVL(i) * (VSP(i) - S_) ^ 2 / (R * T)
                ac(i) = Math.Exp(lnac(i))
            Next

            Return ac

        End Function

        Public Function CalcNu(ByVal P As Double, ByVal T As Double, ByVal VMW() As Double, ByVal VPc() As Double, ByVal VTc() As Double, ByVal Vw() As Double) As Double()

            Dim n As Integer = UBound(VPc)
            Dim i As Integer

            Dim A0, A1, A2, A3, A4, A5, A6, A7, A8, A9 As Double

            Dim v(n), logv(n), logv0(n), logv1(n) As Double
            Dim Tr, Pr As Double

            For i = 0 To n
                Dim Acf = NuCoefficients(VMW(i))
                A0 = Acf(0) : A1 = Acf(1) : A2 = Acf(2) : A3 = Acf(3) : A4 = Acf(4)
                A5 = Acf(5) : A6 = Acf(6) : A7 = Acf(7) : A8 = Acf(8) : A9 = Acf(9)
                Tr = T / VTc(i)
                Pr = P / VPc(i)
                logv0(i) = A0 + A1 / Tr + A2 * Tr + A3 * Tr ^ 2 + A4 * Tr ^ 3 + (A5 + A6 * Tr + A7 * Tr ^ 2) * Pr + (A8 + A9 * Tr) * Pr ^ 2 - Math.Log10(Pr)
                logv1(i) = -4.23893 + 8.65808 * Tr - 1.2206 / Tr - 3.15224 * Tr ^ 3 - 0.025 * (Pr - 0.6)
                logv(i) = logv0(i) + Vw(i) * logv1(i)
                v(i) = 10 ^ logv(i)
            Next

            Return v

        End Function

        Function CalcVapFugCoeff(ByVal T, ByVal P, ByVal Vx, ByVal VTc, ByVal VPc, ByVal Vw) As Double()

            Dim n, R, coeff(3) As Double
            Dim Vant(0, 4) As Double
            Dim criterioOK As Boolean = False
            Dim ZV As Double
            Dim AG, BG, aml, bml As Double
            Dim t1, t2, t3, t4 As Double

            n = Vx.Length - 1

            Dim ai(n), bi(n), tmp(n + 1), a(n, n), b(n, n)
            Dim LN_CF(n), PHI(n) As Double
            Dim Tc(n), Pc(n), alpha(n), m(n), Tr(n) As Double

            R = 8.314

            Dim i, j As Integer
            i = 0
            Do
                Tc(i) = VTc(i)
                Tr(i) = T / Tc(i)
                Pc(i) = VPc(i)
                i = i + 1
            Loop Until i = n + 1

            i = 0
            Do
                ai(i) = 0.42748 * R ^ 2 * Tc(i) ^ 2.5 / (Pc(i) * T ^ 0.5)
                bi(i) = 0.08664 * R * Tc(i) / Pc(i)
                i = i + 1
            Loop Until i = n + 1

            i = 0
            Do
                j = 0
                Do
                    a(i, j) = (ai(i) * ai(j)) ^ 0.5
                    j = j + 1
                Loop Until j = n + 1
                i = i + 1
            Loop Until i = n + 1


            i = 0
            aml = 0
            Do
                j = 0
                Do
                    aml = aml + Vx(i) * Vx(j) * a(i, j)
                    j = j + 1
                Loop Until j = n + 1
                i = i + 1
            Loop Until i = n + 1

            i = 0
            bml = 0
            Do
                bml = bml + Vx(i) * bi(i)
                i = i + 1
            Loop Until i = n + 1

            AG = aml * P / (R * T) ^ 2
            BG = bml * P / (R * T)

            Dim u, w As Integer
            u = 1
            w = 0

            coeff(0) = (-AG * BG - w * BG ^ 2 - w * BG ^ 3)
            coeff(1) = AG + w * BG ^ 2 - u * BG - u * BG ^ 2
            coeff(2) = -(1 + BG - u * BG)
            coeff(3) = 1

            Dim temp1 = Poly_Roots(coeff)
            Dim tv = 0.0#
            Dim tv2

            Try

                If temp1(0, 0) > temp1(1, 0) Then
                    tv = temp1(1, 0)
                    temp1(1, 0) = temp1(0, 0)
                    temp1(0, 0) = tv
                    tv2 = temp1(1, 1)
                    temp1(1, 1) = temp1(0, 1)
                    temp1(0, 1) = tv2
                End If
                If temp1(0, 0) > temp1(2, 0) Then
                    tv = temp1(2, 0)
                    temp1(2, 0) = temp1(0, 0)
                    temp1(0, 0) = tv
                    tv2 = temp1(2, 1)
                    temp1(2, 1) = temp1(0, 1)
                    temp1(0, 1) = tv2
                End If
                If temp1(1, 0) > temp1(2, 0) Then
                    tv = temp1(2, 0)
                    temp1(2, 0) = temp1(1, 0)
                    temp1(1, 0) = tv
                    tv2 = temp1(2, 1)
                    temp1(2, 1) = temp1(1, 1)
                    temp1(1, 1) = tv2
                End If

                ZV = temp1(2, 0)
                If temp1(2, 1) <> 0 Then
                    ZV = temp1(1, 0)
                    If temp1(1, 1) <> 0 Then
                        ZV = temp1(0, 0)
                    End If
                End If

                ZV = temp1(2, 0)
 
            Catch
                Dim findZV
                ZV = 1
                Do
                    findZV = coeff(3) * ZV ^ 3 + coeff(2) * ZV ^ 2 + coeff(1) * ZV + coeff(0)
                    ZV -= 0.00001
                Loop Until Math.Abs(findZV) < 0.0001 Or ZV < 0
            End Try

                i = 0
            Do
                t1 = bi(i) * (ZV - 1) / bml
                t2 = -Math.Log(ZV - BG)
                t3 = AG / (BG * (u ^ 2 - 4 * w) ^ 0.5) * (bi(i) / bml - 2 * (ai(i) / aml) ^ 0.5)
                t4 = Math.Log((2 * ZV + BG * (u + (u ^ 2 - 4 * w) ^ 0.5)) / (2 * ZV + BG * (u - (u ^ 2 - 4 * w) ^ 0.5)))
                PHI(i) = Math.Exp(t1 + t2 + t3 * t4)
                i = i + 1
            Loop Until i = n + 1

            Return PHI
            
        End Function


        ''' <summary>
        ''' The ten correlation coefficients for a compound's liquid fugacity,
        ''' chosen by molar mass: hydrogen, methane, or everything else.
        ''' </summary>
        ''' <remarks>
        ''' Shared by CalcNu and by the temperature derivative, so the two cannot
        ''' drift apart.
        ''' </remarks>
        Private Shared Function NuCoefficients(ByVal mw As Double) As Double()
            If Convert.ToInt32(mw) = 2 Then
                Return New Double() {1.96718, 1.02972, -0.054009, 0.0005288, 0, 0.008585, 0, 0, 0, 0}
            ElseIf Convert.ToInt32(mw) = 16 Then
                Return New Double() {2.4384, -2.2455, -0.34084, 0.00212, -0.00223, 0.10486, -0.03691, 0, 0, 0}
            Else
                Return New Double() {5.75748, -3.01761, -4.985, 2.02299, 0, 0.08427, 0.26667, -0.31138, -0.02655, 0.02883}
            End If
        End Function

        ''' <summary>
        ''' d(ln phi_i)/dT at fixed composition and pressure, in closed form.
        ''' </summary>
        ''' <remarks>
        ''' This correlation is explicit, so there is nothing to solve and no
        ''' iteration to differentiate through.
        '''
        ''' Liquid: phi_i = nu_i * gamma_i.
        '''   log10(nu_i) is a polynomial in Tr with a pressure-only tail, so its
        '''   temperature derivative is that polynomial differentiated, over Tc.
        '''   ln(gamma_i) = V_i (delta_i - Sbar)^2 / (R T) with V and delta taken as
        '''   constants, so d ln(gamma_i)/dT is simply -ln(gamma_i)/T.
        '''
        ''' Vapour: Redlich-Kwong, where a_i is proportional to T^-1/2 and b_i is
        '''   constant. Two consequences worth naming, because they do most of the
        '''   work here: d(aml)/dT = -aml/(2T), and the ratio a_i/aml that appears
        '''   under the square root has no temperature dependence at all, so the
        '''   whole of t3 moves only through AG/BG.
        ''' </remarks>
        Public Function CalcLnFugDT(ByVal T As Double, ByVal P As Double, ByVal Vx() As Double,
                                    ByVal VMM() As Double, ByVal VVL() As Double, ByVal VSP() As Double,
                                    ByVal VTc() As Double, ByVal VPc() As Double, ByVal Vw() As Double,
                                    ByVal VCSAc() As Double, ByVal TIPO As String) As Double()

            Dim n As Integer = Vx.Length - 1
            Dim i, j As Integer
            Dim deriv(n) As Double
            Dim ln10 As Double = Math.Log(10.0)

            If TIPO = "L" Then

                ' ── nu: the correlation, differentiated in Tr ────────────
                For i = 0 To n
                    Dim A = NuCoefficients(VMM(i))
                    Dim Tc As Double = VTc(i)
                    Dim Tr As Double = T / Tc
                    Dim Pr As Double = P / VPc(i)

                    Dim dlogv0 As Double = -A(1) / Tr ^ 2 + A(2) + 2 * A(3) * Tr + 3 * A(4) * Tr ^ 2 +
                                           (A(6) + 2 * A(7) * Tr) * Pr + A(9) * Pr ^ 2
                    Dim dlogv1 As Double = 8.65808 + 1.2206 / Tr ^ 2 - 3 * 3.15224 * Tr ^ 2
                    Dim dlogv As Double = (dlogv0 + VCSAc(i) * dlogv1) / Tc

                    deriv(i) = ln10 * dlogv
                Next

                ' ── gamma: regular solution, all of T in the 1/T ─────────
                Dim R As Double = 8314470.0
                Dim sumV As Double = 0.0, sumVS As Double = 0.0
                For i = 0 To n
                    sumV += Vx(i) * VVL(i)
                    sumVS += Vx(i) * VVL(i) * VSP(i)
                Next
                If sumV > 0.0 Then
                    Dim Sbar As Double = sumVS / sumV
                    For i = 0 To n
                        Dim lnac As Double = VVL(i) * (VSP(i) - Sbar) ^ 2 / (R * T)
                        deriv(i) += -lnac / T
                    Next
                End If

                For i = 0 To n
                    If Double.IsNaN(deriv(i)) Or Double.IsInfinity(deriv(i)) Then Return Nothing
                Next
                Return deriv

            End If

            ' ── vapour: Redlich-Kwong ────────────────────────────────────
            Const R2 As Double = 8.314
            Dim ai(n), bi(n) As Double
            For i = 0 To n
                ai(i) = 0.42748 * R2 ^ 2 * VTc(i) ^ 2.5 / (VPc(i) * T ^ 0.5)
                bi(i) = 0.08664 * R2 * VTc(i) / VPc(i)
            Next

            Dim aml As Double = 0.0, bml As Double = 0.0
            For i = 0 To n
                For j = 0 To n
                    aml += Vx(i) * Vx(j) * (ai(i) * ai(j)) ^ 0.5
                Next
                bml += Vx(i) * bi(i)
            Next
            If aml <= 0.0 Or bml <= 0.0 Then Return Nothing

            Dim AG As Double = aml * P / (R2 * T) ^ 2
            Dim BG As Double = bml * P / (R2 * T)
            ' a_i goes as T^-1/2, so aml does too: dAG/dT = AG*(-1/(2T) - 2/T).
            Dim dAG As Double = -2.5 * AG / T
            Dim dBG As Double = -BG / T

            Dim c2 As Double = -1.0
            Dim c1 As Double = AG - BG - BG ^ 2
            Dim c0 As Double = -AG * BG

            Dim Z As Double = SolveRKRoot(c2, c1, c0)
            If Double.IsNaN(Z) OrElse Z <= BG Then Return Nothing

            Dim denom As Double = 3 * Z ^ 2 + 2 * c2 * Z + c1
            If Math.Abs(denom) < 1.0E-30 Then Return Nothing

            Dim dc1 As Double = dAG - dBG - 2 * BG * dBG
            Dim dc0 As Double = -(dAG * BG + AG * dBG)
            Dim dZ As Double = -(Z * dc1 + dc0) / denom

            Dim t4 As Double = Math.Log((Z + BG) / Z)
            Dim dt4 As Double = (dZ + dBG) / (Z + BG) - dZ / Z

            For i = 0 To n
                Dim t3 As Double = AG / BG * (bi(i) / bml - 2 * (ai(i) / aml) ^ 0.5)
                ' AG/BG carries the only temperature dependence of t3: the bracket
                ' is composition, and a_i/aml cancels its T^-1/2 exactly.
                Dim dt3 As Double = -1.5 * t3 / T
                Dim dt1 As Double = bi(i) / bml * dZ
                Dim dt2 As Double = -(dZ - dBG) / (Z - BG)
                deriv(i) = dt1 + dt2 + dt3 * t4 + t3 * dt4
                If Double.IsNaN(deriv(i)) Or Double.IsInfinity(deriv(i)) Then Return Nothing
            Next

            Return deriv

        End Function

        ''' <summary>
        ''' Largest real root of Z^3 + c2 Z^2 + c1 Z + c0, which is the vapour root
        ''' the fugacity routine picks. NaN when there is none to be had.
        ''' </summary>
        Private Shared Function SolveRKRoot(c2 As Double, c1 As Double, c0 As Double) As Double
            Dim Z As Double = 1.0
            For k = 1 To 100
                Dim f As Double = Z ^ 3 + c2 * Z ^ 2 + c1 * Z + c0
                Dim df As Double = 3 * Z ^ 2 + 2 * c2 * Z + c1
                If Math.Abs(df) < 1.0E-30 Then Return Double.NaN
                Dim dz As Double = f / df
                Z -= dz
                If Z < 0.0 Then Z = 1.0E-06
                If Math.Abs(dz) < 1.0E-12 Then Exit For
            Next
            If Double.IsNaN(Z) Or Double.IsInfinity(Z) Then Return Double.NaN
            Return Z
        End Function
    End Class


End Namespace

