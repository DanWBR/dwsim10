'    Analytical derivatives of the generalized cubic EOS fugacity coefficients (Peng-Robinson / SRK)
'    Copyright 2008-2022 Daniel Wagner O. de Medeiros
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

Imports System.Linq
Imports System.Math

Namespace PropertyPackages.ThermoPlugs

    ''' <summary>
    ''' Closed-form temperature and composition (mole-number) derivatives of the fugacity coefficients for
    ''' the generalized two-parameter cubic EOS shared by Peng-Robinson and Soave-Redlich-Kwong. Both EOS
    ''' use the same fugacity expression
    '''   ln(phi_i) = (b_i/b_m)(Z-1) - ln(Z-B) - A/((e1-e2)B) * (2*aml2_i/a_m - b_i/b_m) * ln((Z+e1*B)/(Z+e2*B))
    ''' and the same cubic Z^3 + c2 Z^2 + c1 Z + c0 = 0, differing only in the constants
    ''' (Omega_a, Omega_b, kappa(w), e1, e2). Everything is derived analytically from those; the Z-root is
    ''' obtained from the same CalcZ2 root-finder used by CalcLnFugCPU so the derivative is taken on the
    ''' identical root branch. Results are validated element-by-element against finite differences in
    ''' DWSIM.Thermodynamics.Derivatives.Tests.
    ''' </summary>
    Public Class CubicEOSDerivatives

        Public Const EOS_PR As Integer = 0
        Public Const EOS_SRK As Integer = 1
        Public Const EOS_PR78 As Integer = 2

        ''' <summary>
        ''' Computes ln(phi), d(ln phi)/dT and d(ln phi)/dn_j (on a total-moles = 1 basis) for one phase.
        ''' </summary>
        ''' <param name="eosType">EOS_PR or EOS_SRK.</param>
        ''' <param name="phase">0 = liquid (minimum Z), 1 = vapour (maximum Z), otherwise minimum-Gibbs root.</param>
        ''' <returns>Object(){ lnphi() As Double(), dlnphidT() As Double(), dlnphidn(,) As Double(,) }.</returns>
        Public Shared Function Calc(ByVal eosType As Integer, ByVal T As Double, ByVal P As Double, ByVal Vx As Double(),
                                    ByVal VKij As Double(,), ByVal Tc As Double(), ByVal Pc As Double(), ByVal w As Double(),
                                    ByVal phase As Integer) As Object

            Dim n As Integer = Vx.Length - 1
            Const R As Double = 8.314

            Dim Wa, Wb, e1, e2 As Double
            If eosType = EOS_SRK Then
                Wa = 0.42748 : Wb = 0.08664 : e1 = 1.0 : e2 = 0.0
            Else ' PR and PR78 share the same generalized cubic; only kappa(w) differs
                Wa = 0.45724 : Wb = 0.0778 : e1 = 1.0 + Sqrt(2.0) : e2 = 1.0 - Sqrt(2.0)
            End If

            ' Pure-component a_i, b_i and their T-derivatives
            Dim kappa(n), s(n), ai(n), bi(n), daidT(n) As Double
            For i As Integer = 0 To n
                If eosType = EOS_PR Then
                    kappa(i) = 0.37464 + 1.54226 * w(i) - 0.26992 * w(i) ^ 2
                ElseIf eosType = EOS_PR78 Then
                    If w(i) <= 0.491 Then
                        kappa(i) = 0.37464 + 1.5422 * w(i) - 0.26992 * w(i) ^ 2
                    Else
                        kappa(i) = 0.379642 + 1.48503 * w(i) - 0.164423 * w(i) ^ 2 + 0.016666 * w(i) ^ 3
                    End If
                Else
                    kappa(i) = 0.48 + 1.574 * w(i) - 0.176 * w(i) ^ 2
                End If
                s(i) = 1.0 + kappa(i) * (1.0 - Sqrt(T / Tc(i)))
                ai(i) = Wa * (s(i) * s(i)) * (R * Tc(i)) ^ 2 / Pc(i)
                bi(i) = Wb * R * Tc(i) / Pc(i)
                Dim dsdT As Double = -kappa(i) / (2.0 * Sqrt(T * Tc(i)))
                daidT(i) = 2.0 * ai(i) * dsdT / s(i)
            Next

            ' Cross term a_ij = sqrt(a_i a_j)(1-kij) and its T-derivative
            Dim aij(n, n), daijdT(n, n) As Double
            For i As Integer = 0 To n
                For j As Integer = 0 To n
                    aij(i, j) = Sqrt(ai(i) * ai(j)) * (1.0 - VKij(i, j))
                    daijdT(i, j) = 0.5 * aij(i, j) * (daidT(i) / ai(i) + daidT(j) / ai(j))
                Next
            Next

            ' Mixture parameters and their T-derivatives
            Dim aml As Double = 0.0, bml As Double = 0.0, damldT As Double = 0.0
            Dim aml2(n), daml2dT(n) As Double
            For i As Integer = 0 To n
                bml += Vx(i) * bi(i)
                For j As Integer = 0 To n
                    aml += Vx(i) * Vx(j) * aij(i, j)
                    aml2(i) += Vx(j) * aij(i, j)
                    daml2dT(i) += Vx(j) * daijdT(i, j)
                    damldT += Vx(i) * Vx(j) * daijdT(i, j)
                Next
            Next

            Dim A As Double = aml * P / (R * T) ^ 2
            Dim B As Double = bml * P / (R * T)
            Dim dAdT As Double = A * (damldT / aml - 2.0 / T)
            Dim dBdT As Double = -B / T

            ' Z from the same root-finder / branch selection as CalcLnFugCPU (PR and PR78 share the cubic)
            Dim zlist As List(Of Double)
            If eosType = EOS_SRK Then
                zlist = SRK.CalcZ2(A, B)
            Else
                zlist = PR.CalcZ2(A, B)
            End If
            Dim Z As Double
            If phase = 0 Then
                Z = zlist.Min
            ElseIf phase = 1 Then
                Z = zlist.Max
            Else
                Dim mg As Double()
                If eosType = EOS_SRK Then
                    mg = SRK.ZtoMinG(zlist.ToArray, T, P, Vx, VKij, Tc, Pc, w)
                Else
                    mg = PR.ZtoMinG(zlist.ToArray, T, P, Vx, VKij, Tc, Pc, w)
                End If
                Z = zlist(CInt(mg(0)))
            End If

            ' Cubic derivative denominator (holding the selected root branch)
            Dim c2 As Double = (e1 + e2 - 1.0) * B - 1.0
            Dim c1 As Double = A + (e1 * e2 - e1 - e2) * B ^ 2 - (e1 + e2) * B
            Dim denom As Double = 3.0 * Z ^ 2 + 2.0 * c2 * Z + c1
            If Abs(denom) < 1.0E-30 Then denom = Sign(denom) * 1.0E-30 + 1.0E-30

            Dim dZdT As Double = DZ(Z, A, B, dAdT, dBdT, e1, e2, denom)

            ' ln(phi), d(ln phi)/dT
            Dim lnphi(n), dlnphidT(n) As Double
            Dim eDiff As Double = e1 - e2
            For i As Integer = 0 To n
                Dim Gi As Double = 2.0 * aml2(i) / aml - bi(i) / bml
                lnphi(i) = bi(i) * (Z - 1.0) / bml - Log(Z - B) - (A / (eDiff * B)) * Gi * Log((Z + e1 * B) / (Z + e2 * B))
                dlnphidT(i) = DlnphiTerm(Z, A, B, aml, bml, aml2(i), bi(i), e1, e2,
                                         dZdT, dAdT, dBdT, damldT, daml2dT(i), 0.0)
            Next

            ' d(ln phi_i)/dn_j on total-moles = 1 basis
            Dim dlnphidn(n, n) As Double
            For j As Integer = 0 To n
                Dim dbml_j As Double = bi(j) - bml
                Dim daml_j As Double = 2.0 * aml2(j) - 2.0 * aml
                Dim dA_j As Double = A * daml_j / aml
                Dim dB_j As Double = B * dbml_j / bml
                Dim dZ_j As Double = DZ(Z, A, B, dA_j, dB_j, e1, e2, denom)
                For i As Integer = 0 To n
                    Dim daml2_ij As Double = aij(i, j) - aml2(i)
                    dlnphidn(i, j) = DlnphiTerm(Z, A, B, aml, bml, aml2(i), bi(i), e1, e2,
                                                dZ_j, dA_j, dB_j, daml_j, daml2_ij, dbml_j)
                Next
            Next

            Return New Object() {lnphi, dlnphidT, dlnphidn}

        End Function

        ''' <summary>
        ''' Derivative of the selected Z root wrt a parameter p, from implicit differentiation of the cubic
        ''' Z^3 + c2 Z^2 + c1 Z + c0 = 0 with c2,c1,c0 expressed in A,B, holding the root branch fixed.
        ''' </summary>
        Private Shared Function DZ(ByVal Z As Double, ByVal A As Double, ByVal B As Double,
                                   ByVal dA As Double, ByVal dB As Double,
                                   ByVal e1 As Double, ByVal e2 As Double, ByVal denom As Double) As Double
            Dim dc2 As Double = (e1 + e2 - 1.0) * dB
            Dim dc1 As Double = dA + (e1 * e2 - e1 - e2) * 2.0 * B * dB - (e1 + e2) * dB
            Dim dc0 As Double = -(dA * B + A * dB) - e1 * e2 * (2.0 * B * dB + 3.0 * B * B * dB)
            Return -(Z * Z * dc2 + Z * dc1 + dc0) / denom
        End Function

        ''' <summary>
        ''' Assembles d(ln phi_i)/dp from the seed partials of Z, A, B, a_m, aml2_i and b_m wrt a parameter p.
        ''' Works for both p = T (pass db_m = 0) and p = n_j.
        ''' </summary>
        Private Shared Function DlnphiTerm(ByVal Z As Double, ByVal A As Double, ByVal B As Double,
                                           ByVal aml As Double, ByVal bml As Double, ByVal aml2_i As Double, ByVal bi As Double,
                                           ByVal e1 As Double, ByVal e2 As Double,
                                           ByVal dZ As Double, ByVal dA As Double, ByVal dB As Double,
                                           ByVal daml As Double, ByVal daml2_i As Double, ByVal dbml As Double) As Double

            Dim eDiff As Double = e1 - e2
            Dim Q As Double = A / (eDiff * B)
            Dim Gi As Double = 2.0 * aml2_i / aml - bi / bml
            Dim num1 As Double = Z + e1 * B
            Dim num2 As Double = Z + e2 * B
            Dim Lg As Double = Log(num1 / num2)

            ' term 1: b_i (Z-1)/b_m
            Dim dT1 As Double = bi * (dZ * bml - (Z - 1.0) * dbml) / (bml * bml)
            ' term 2: -ln(Z-B)
            Dim dT2 As Double = -(dZ - dB) / (Z - B)
            ' term 3: -Q * Gi * Lg
            Dim dQ As Double = Q * (dA / A - dB / B)
            Dim dGi As Double = 2.0 * (daml2_i * aml - aml2_i * daml) / (aml * aml) + bi * dbml / (bml * bml)
            Dim dLg As Double = (dZ + e1 * dB) / num1 - (dZ + e2 * dB) / num2
            Dim dT3 As Double = -(dQ * Gi * Lg + Q * dGi * Lg + Q * Gi * dLg)

            Return dT1 + dT2 + dT3

        End Function

    End Class

End Namespace
