'    Petroleum Distillation Curve Conversion Routines 
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

Imports System.Math

Namespace Utilities.PetroleumCharacterization.Methods

    Public Class DistillationCurveConversion

        ''' <summary>
        ''' Converts ASTM D86 points to ASTM D2892 (TBP).
        ''' </summary>
        ''' <param name="Tastmd86">Temperatures of the D86 distillation curve, in K.</param>
        ''' <returns>Temperatures of the TBP distillation curve, in K.</returns>
        ''' <remarks></remarks>
        Public Shared Function ASTMD86ToPEV_Riazi(ByVal Tastmd86() As Double) As Double()

            Dim a(6), b(6), Tpev(6) As Double
            Dim i As Integer

            a(0) = 0.9177
            a(1) = 0.5564
            a(2) = 0.7617
            a(3) = 0.9013
            a(4) = 0.8821
            a(5) = 0.9552
            a(6) = 0.8177

            b(0) = 1.0019
            b(1) = 1.09
            b(2) = 1.0425
            b(3) = 1.0176
            b(4) = 1.0226
            b(5) = 1.011
            b(6) = 1.0355

            For i = 0 To UBound(Tastmd86)
                Tpev(i) = a(i) * Tastmd86(i) ^ b(i)
            Next

            Return Tpev

        End Function

        ''' <summary>
        ''' Converts ASTM D2887 points to ASTM D2892 (TBP).
        ''' </summary>
        ''' <param name="Tastmd2887">Temperatures of the D2887 distillation curve, in K.</param>
        ''' <returns>Temperatures of the TBP distillation curve, in K.</returns>
        ''' <remarks></remarks>
        Public Shared Function ASTMD2887ToPEV_Daubert(ByVal Tastmd2887() As Double) As Double()

            Dim a(6), b(6), Tpev(7), dT(6) As Double
            Dim i, n As Integer

            n = UBound(Tastmd2887)

            Tastmd2887 = Tastmd2887.Select(Function(t) (t - 273.15) * 9 / 5 + 32).ToArray()

            a(0) = 0.15779
            a(1) = 0.011903
            a(2) = 0.05342
            a(3) = 0.19861
            a(4) = 0.31531
            a(5) = 0.97476
            a(6) = 0.02172

            b(0) = 1.4296
            b(1) = 2.0253
            b(2) = 1.6988
            b(3) = 1.3975
            b(4) = 1.2938
            b(5) = 0.8723
            b(6) = 1.9733

            For i = 0 To n - 1
                dT(i) = a(i) * (Tastmd2887(i + 1) - Tastmd2887(i)) ^ b(i)
            Next

            Tpev(0) = Tastmd2887(3) - dT(0) - dT(1) - dT(2)   'T5
            Tpev(1) = Tastmd2887(3) - dT(1) - dT(2)  'T10
            Tpev(2) = Tastmd2887(3) - dT(2)  'T30
            Tpev(3) = Tastmd2887(3)  'T50
            Tpev(4) = Tastmd2887(3) + dT(3) 'T70
            Tpev(5) = Tastmd2887(3) + dT(3) + dT(4) 'T90
            Tpev(6) = Tastmd2887(3) + dT(3) + dT(4) + dT(5) 'T95
            Tpev(7) = Tastmd2887(3) + dT(3) + dT(4) + dT(5) + dT(6) 'T100

            For i = 0 To n
                Tpev(i) = (Tpev(i) - 32) / 9 * 5 + 273.15
            Next

            Return Tpev

        End Function

        ''' <summary>
        ''' Converts sub-atmospheric TBP to atmospheric TBP curve.
        ''' </summary>
        ''' <param name="T">Temperature in K.</param>
        ''' <param name="P">Pressure in Pa.</param>
        ''' <param name="Kw">Watson K</param>
        ''' <returns>TBP curve temperatures in K.</returns>
        ''' <remarks></remarks>
        Public Shared Function PEVsubToPEV_MaxwellBonnel(ByVal T As Double, ByVal P As Double, ByVal Kw As Double) As Double

            P = P * 760 / 101325
            T = 1.8 * T

            Dim X, Tb_, Tb_ant As Double
            Dim f As Double
            Dim i As Integer

            i = 0
            Do
                Tb_ant = Tb_

                X = (-5.994296 + 0.972546 * Log10(P)) / (95.76 * Log10(P) - 2663.129)
                Tb_ = -748.1 * X / (0.0002867 - 0.2145 * X - 1 / T)

                If (Tb_ - 459.7) < 200 Then
                    f = 0
                ElseIf (Tb_ - 459.7) > 400 Then
                    f = 1
                Else
                    f = (Tb_ - 659.7) / 200
                End If

                Tb_ = Tb_ - 2.5 * f * (Kw - 12) * Log10(P / 760)

                i += 1
            Loop Until Abs(Tb_ - Tb_ant) < 0.001 Or i > 1000

            Return Tb_ / 1.8

        End Function

        ''' <summary>
        ''' Converts ASTM D86 points to ASTM D2892 (TBP).
        ''' </summary>
        ''' <param name="Tastm">Temperatures of the D1160 distillation curve, in K.</param>
        ''' <returns>Sub-atm TBP curve temperatures in K.</returns>
        ''' <remarks></remarks>
        Public Shared Function ASTMD1160ToPEVsub_Wauquier(ByVal Tastm() As Double) As Double()

            Dim j, fracv, i1, i2, i As Integer
            Dim f(0 To 1, 0 To 10) As Double
            Dim Tpev10(6), fv(6) As Double

            fv(0) = 0
            fv(1) = 10
            fv(2) = 30
            fv(3) = 50
            fv(4) = 70
            fv(5) = 90
            fv(6) = 100

            f(0, 0) = 0
            f(0, 1) = 20
            f(0, 2) = 35.5
            f(0, 3) = 47.5
            f(0, 4) = 57
            f(0, 5) = 64
            f(0, 6) = 70
            f(0, 7) = 75
            f(0, 8) = 82.5
            f(0, 9) = 91
            f(0, 10) = 100
            f(1, 0) = 0
            f(1, 1) = 13
            f(1, 2) = 24
            f(1, 3) = 34.5
            f(1, 4) = 44
            f(1, 5) = 53.5
            f(1, 6) = 63
            f(1, 7) = 72
            f(1, 8) = 81.5
            f(1, 9) = 90.5
            f(1, 10) = 100

            For i = 0 To 6
                fracv = Convert.ToInt32(fv(i) / 20 + 0.5)
                If fracv >= 3 Then
                    Tpev10(i) = Tastm(i)
                Else
                    Tpev10(i) = Tastm(3)
                    i1 = 0
                    For j = 3 To fracv + 1 Step -1
                        If j = 2 Then i1 = 1
                        i2 = Int(Abs(Tastm(j) - Tastm(j - 1)) / 10) - 1
                        If i2 > 9 Then i2 = 9
                        Tpev10(i) = Tpev10(i) - (Abs(Tastm(j) - Tastm(j - 1)) / 10 - i2) * (f(i1, i2 + 1) - f(i1, i2)) - f(i1, i2)
                    Next j
                End If
            Next

            Return Tpev10

        End Function

        ' API Technical Data Book, procedure 3A1.1 (Daubert, 1994), segments 0-10, 10-30, 30-50,
        ' 50-70, 70-90 and 90-100 vol%: TBP difference = A (D86 difference)^B, in F
        Private Shared ReadOnly D86TBP_A As Double() = {7.4012, 4.9004, 3.0305, 2.5282, 3.0419, 0.11798}
        Private Shared ReadOnly D86TBP_B As Double() = {0.60244, 0.71644, 0.80076, 0.82002, 0.75497, 1.6606}

        ' procedure 3A3.1, segments 5-10, 10-30, 30-50, 50-70, 70-90, 90-95 and 95-100 %:
        ' TBP difference = C (D2887 difference)^D, in F
        Private Shared ReadOnly SDTBP_C As Double() = {0.15779, 0.011903, 0.05342, 0.19861, 0.31531, 0.97476, 0.02172}
        Private Shared ReadOnly SDTBP_D As Double() = {1.4296, 2.0253, 1.6988, 1.3975, 1.2938, 0.8723, 1.9733}

        ' Riazi and Daubert, D86 to EFV at 0, 10, 30, 50, 70, 90 and 100 vol% (Riazi, MNL50, Eq. 3.16)
        Private Shared ReadOnly D86EFV_a As Double() = {2.9747, 1.4459, 0.8506, 3.268, 8.2873, 10.6266, 7.9952}
        Private Shared ReadOnly D86EFV_b As Double() = {0.8466, 0.9511, 1.0315, 0.8274, 0.6871, 0.6529, 0.6949}
        Private Shared ReadOnly D86EFV_c As Double() = {0.4209, 0.1287, 0.0817, 0.6214, 0.934, 1.1025, 1.0737}

        ' Edmister and Okamoto, API figure 3A2.1: ASTM D1160 difference against TBP difference at 10 mmHg, in F,
        ' read from the figure. The 10-30 and 30-50 % segments share a curve, the 0-10 % segment has its own
        ' and the segments above 50 % are equal on both curves.
        Private Shared ReadOnly D1160_X As Double() = {0, 9, 15, 21, 27, 33, 39, 45, 51, 57, 63, 69, 75, 81, 87, 93, 99, 105, 111, 117, 123, 129, 135, 141, 147, 153, 159, 165, 171}
        Private Shared ReadOnly D1160_Y1050 As Double() = {0, 11.7, 18.6, 25.9, 32.5, 39.7, 46.1, 52.5, 58.9, 65.1, 71.1, 76.8, 82.3, 87.8, 93.5, 98.8, 104.3, 109.8, 115.5, 121.5, 127.0, 132.5, 138.3, 143.8, 149.5, 155.0, 160.4, 165.6, 171}
        Private Shared ReadOnly D1160_X010 As Double() = {0, 3, 9, 15, 21, 27, 33, 39, 45, 51, 57, 63, 69, 75, 81, 87, 93, 99, 105, 111, 117}
        Private Shared ReadOnly D1160_Y010 As Double() = {0, 6.5, 18.8, 30.2, 41.5, 51.3, 60.0, 68.1, 75.9, 83.2, 89.7, 94.8, 100.0, 104.6, 109.3, 113.8, 117.9, 121.4, 125.0, 128.2, 130.8}

        Private Shared Function KToF(T As Double) As Double
            Return (T - 273.15) * 9 / 5 + 32
        End Function

        Private Shared Function FToK(T As Double) As Double
            Return (T - 32) * 5 / 9 + 273.15
        End Function

        ''' <summary>
        ''' Converts an ASTM D86 curve to a TBP curve at atmospheric pressure (API procedure 3A1.1, Daubert 1994).
        ''' </summary>
        ''' <param name="Td86">D86 temperatures at 0, 10, 30, 50, 70, 90 and 100 vol% distilled, in K.</param>
        ''' <returns>TBP temperatures at the same points, in K.</returns>
        Public Shared Function ASTMD86ToTBP_Daubert(Td86 As Double()) As Double()

            Dim d = Td86.Select(Function(t) KToF(t)).ToArray()
            Dim tbp(6) As Double

            tbp(3) = 0.8718 * d(3) ^ 1.0258
            For i = 2 To 0 Step -1
                tbp(i) = tbp(i + 1) - D86TBP_A(i) * Max(0.0, d(i + 1) - d(i)) ^ D86TBP_B(i)
            Next
            For i = 4 To 6
                tbp(i) = tbp(i - 1) + D86TBP_A(i - 1) * Max(0.0, d(i) - d(i - 1)) ^ D86TBP_B(i - 1)
            Next

            Return tbp.Select(Function(t) FToK(t)).ToArray()

        End Function

        ''' <summary>
        ''' Converts a TBP curve at atmospheric pressure to an ASTM D86 curve (API procedure 3A1.1 in reverse).
        ''' </summary>
        ''' <param name="Ttbp">TBP temperatures at 0, 10, 30, 50, 70, 90 and 100 vol% distilled, in K.</param>
        ''' <returns>D86 temperatures at the same points, in K.</returns>
        Public Shared Function TBPToASTMD86_Daubert(Ttbp As Double()) As Double()

            Dim t = Ttbp.Select(Function(x) KToF(x)).ToArray()
            Dim d(6) As Double

            ' a D86 segment is never wider than its TBP segment: past that the inverted power law runs away
            ' (a few light ends in a side draw put the TBP IBP 200 F below its 10 % point)
            Dim seg = Function(dt As Double, k As Integer) Min(dt, (dt / D86TBP_A(k)) ^ (1 / D86TBP_B(k)))
            d(3) = Exp(Log(t(3) / 0.8718) / 1.0258)
            For i = 2 To 0 Step -1
                d(i) = d(i + 1) - seg(Max(0.0, t(i + 1) - t(i)), i)
            Next
            For i = 4 To 6
                d(i) = d(i - 1) + seg(Max(0.0, t(i) - t(i - 1)), i - 1)
            Next

            Return d.Select(Function(x) FToK(x)).ToArray()

        End Function

        ''' <summary>
        ''' Converts a TBP curve at atmospheric pressure to an ASTM D2887 (simulated distillation) curve
        ''' (API procedure 3A3.1 in reverse). The TBP points are in vol% distilled, the D2887 points in wt%.
        ''' </summary>
        ''' <param name="Ttbp">TBP temperatures at 5, 10, 30, 50, 70, 90, 95 and 100 % distilled, in K.</param>
        ''' <returns>D2887 temperatures at the same points, in K.</returns>
        Public Shared Function TBPToASTMD2887_Daubert(Ttbp As Double()) As Double()

            Dim t = Ttbp.Select(Function(x) KToF(x)).ToArray()
            Dim sd(7) As Double

            sd(3) = t(3)
            For i = 2 To 0 Step -1
                sd(i) = sd(i + 1) - (Max(0.0, t(i + 1) - t(i)) / SDTBP_C(i)) ^ (1 / SDTBP_D(i))
            Next
            For i = 4 To 7
                sd(i) = sd(i - 1) + (Max(0.0, t(i) - t(i - 1)) / SDTBP_C(i - 1)) ^ (1 / SDTBP_D(i - 1))
            Next

            Return sd.Select(Function(x) FToK(x)).ToArray()

        End Function

        ''' <summary>
        ''' Converts an ASTM D86 curve to an equilibrium flash vaporization curve at atmospheric pressure
        ''' (Riazi and Daubert).
        ''' </summary>
        ''' <param name="Td86">D86 temperatures at 0, 10, 30, 50, 70, 90 and 100 vol% distilled, in K.</param>
        ''' <param name="SG">Specific gravity 60F/60F.</param>
        ''' <returns>EFV temperatures at the same points, in K.</returns>
        Public Shared Function ASTMD86ToEFV_RiaziDaubert(Td86 As Double(), SG As Double) As Double()

            Dim efv(6) As Double
            For i = 0 To 6
                efv(i) = D86EFV_a(i) * Td86(i) ^ D86EFV_b(i) * SG ^ D86EFV_c(i)
            Next
            Return efv

        End Function

        ''' <summary>
        ''' Converts an ASTM D1160 curve at 10 mmHg to a TBP curve at 10 mmHg (Edmister and Okamoto, API figure 3A2.1).
        ''' </summary>
        ''' <param name="Td1160">D1160 temperatures at 0, 10, 30, 50, 70, 90 and 100 vol% distilled, in K.</param>
        ''' <returns>TBP temperatures at 10 mmHg at the same points, in K.</returns>
        Public Shared Function ASTMD1160ToTBP10mmHg_EdmisterOkamoto(Td1160 As Double()) As Double()

            Dim d = Td1160.Select(Function(t) KToF(t)).ToArray()
            Dim tbp(6) As Double

            tbp(3) = d(3)
            tbp(2) = tbp(3) - Interpolate(D1160_X, D1160_Y1050, Max(0.0, d(3) - d(2)))
            tbp(1) = tbp(2) - Interpolate(D1160_X, D1160_Y1050, Max(0.0, d(2) - d(1)))
            tbp(0) = tbp(1) - Interpolate(D1160_X010, D1160_Y010, Max(0.0, d(1) - d(0)))
            For i = 4 To 6
                tbp(i) = tbp(i - 1) + Max(0.0, d(i) - d(i - 1))
            Next

            Return tbp.Select(Function(t) FToK(t)).ToArray()

        End Function

        ''' <summary>
        ''' Converts a TBP curve at 10 mmHg to an ASTM D1160 curve at 10 mmHg (API figure 3A2.1 in reverse).
        ''' </summary>
        ''' <param name="Ttbp">TBP temperatures at 10 mmHg at 0, 10, 30, 50, 70, 90 and 100 vol% distilled, in K.</param>
        ''' <returns>D1160 temperatures at 10 mmHg at the same points, in K.</returns>
        Public Shared Function TBP10mmHgToASTMD1160_EdmisterOkamoto(Ttbp As Double()) As Double()

            Dim t = Ttbp.Select(Function(x) KToF(x)).ToArray()
            Dim d(6) As Double

            d(3) = t(3)
            d(2) = d(3) - Interpolate(D1160_Y1050, D1160_X, Max(0.0, t(3) - t(2)))
            d(1) = d(2) - Interpolate(D1160_Y1050, D1160_X, Max(0.0, t(2) - t(1)))
            d(0) = d(1) - Interpolate(D1160_Y010, D1160_X010, Max(0.0, t(1) - t(0)))
            For i = 4 To 6
                d(i) = d(i - 1) + Max(0.0, t(i) - t(i - 1))
            Next

            Return d.Select(Function(x) FToK(x)).ToArray()

        End Function

        ''' <summary>
        ''' Linear interpolation on an increasing table, extrapolated with the slope of the last two points.
        ''' </summary>
        Private Shared Function Interpolate(x As Double(), y As Double(), xi As Double) As Double

            Dim n = x.Length
            If xi <= x(0) Then Return y(0)
            For i = 1 To n - 1
                If xi <= x(i) Then
                    Return y(i - 1) + (y(i) - y(i - 1)) * (xi - x(i - 1)) / (x(i) - x(i - 1))
                End If
            Next
            Return y(n - 1) + (y(n - 1) - y(n - 2)) * (xi - x(n - 1)) / (x(n - 1) - x(n - 2))

        End Function

        ''' <summary>
        ''' Converts a normal (atmospheric) boiling temperature to the boiling temperature at another pressure
        ''' (Maxwell and Bonnell, API procedure 5A1.19). This is the reverse of <see cref="PEVsubToPEV_MaxwellBonnel"/>.
        ''' </summary>
        ''' <param name="Tb">Normal boiling temperature, in K.</param>
        ''' <param name="P">Pressure, in Pa.</param>
        ''' <param name="Kw">Watson K. Use 12 for no correction.</param>
        ''' <returns>Boiling temperature at P, in K.</returns>
        Public Shared Function PEVToPEVsub_MaxwellBonnel(Tb As Double, P As Double, Kw As Double) As Double

            Dim Pmm = P * 760 / 101325
            Dim TbR = 1.8 * Tb

            Dim f As Double
            If (TbR - 459.7) < 200 Then
                f = 0
            ElseIf (TbR - 459.7) > 400 Then
                f = 1
            Else
                f = (TbR - 659.7) / 200
            End If

            Dim Tb_ = TbR + 2.5 * f * (Kw - 12) * Log10(Pmm / 760)

            Dim X As Double
            If Pmm < 2 Then
                X = (6.76156 - 0.987672 * Log10(Pmm)) / (3000.538 - 43 * Log10(Pmm))
            ElseIf Pmm > 760 Then
                X = (6.412631 - 0.989679 * Log10(Pmm)) / (2770.085 - 36 * Log10(Pmm))
            Else
                X = (5.994296 - 0.972546 * Log10(Pmm)) / (2663.129 - 95.76 * Log10(Pmm))
            End If

            Dim TR = 1 / (0.0002867 - 0.2145 * X + 748.1 * X / Tb_)

            Return TR / 1.8

        End Function

        Class TBPFit

            Private _x, _y As Double()
            Private sum As Double
            Private its As Integer = 0

            Public Function GetCoeffs(ByVal x As Double(), ByVal y As Double(), ByVal inest() As Double, ByVal epsg As Double, ByVal epsf As Double, ByVal epsx As Double, ByVal maxits As Integer) As Object

                Dim lmsolve As New MathEx.LM.levenbergmarquardt
                lmsolve.DefineFuncGradDelegate(AddressOf fv)

                Dim newc(UBound(inest) + 1) As Double
                Dim i As Integer = 1
                Do
                    newc(i) = inest(i - 1)
                    i = i + 1
                Loop Until i = UBound(inest) + 2

                Me._x = x
                Me._y = y

                Dim info As Integer = 56

                its = 0
                lmsolve.levenbergmarquardtminimize(inest.Length, _x.Length, newc, epsg, epsf, epsx, maxits, info)

                Dim coeffs(UBound(inest)) As Double

                i = 0
                Do
                    coeffs(i) = newc(i + 1)
                    i = i + 1
                Loop Until i = UBound(inest) + 1

                Return New Object() {coeffs, info, sum, its}

            End Function

            Public Sub fv(ByRef x As Double(), ByRef fvec As Double(), ByRef fjac As Double(,), ByRef iflag As Integer)

                If Double.IsNaN(x(1)) Or Double.IsNegativeInfinity(x(1)) Or Double.IsPositiveInfinity(x(1)) Then iflag = -1
                If Double.IsNaN(fvec(1)) Or Double.IsNegativeInfinity(fvec(1)) Or Double.IsPositiveInfinity(fvec(1)) Then iflag = -1

                'A + B*FV + C*FV^2 + D*FV^3 + E*FV^4 + F*FV^5 + G*FV^6
                sum = 0
                Dim i As Integer
                If iflag = 1 Then
                    i = 1
                    Do
                        fvec(i) = -_y(i - 1) + (x(1) + x(2) * _x(i - 1) + x(3) * _x(i - 1) ^ 2 + x(4) * _x(i - 1) ^ 3 + x(5) * _x(i - 1) ^ 4 + x(6) * _x(i - 1) ^ 5 + x(7) * _x(i - 1) ^ 6)
                        sum += (fvec(i)) ^ 2
                        i = i + 1
                    Loop Until i = UBound(_y) + 2
                ElseIf iflag = 2 Then
                    i = 1
                    Do
                        'A + B*FV + C*FV^2 + D*FV^3 + E*FV^4 + F*FV^5 + G*FV^6
                        fjac(i, 1) = 0
                        fjac(i, 2) = _x(i - 1)
                        fjac(i, 3) = _x(i - 1) ^ 2
                        fjac(i, 4) = _x(i - 1) ^ 3
                        fjac(i, 5) = _x(i - 1) ^ 4
                        fjac(i, 6) = _x(i - 1) ^ 5
                        fjac(i, 7) = _x(i - 1) ^ 6
                        i = i + 1
                    Loop Until i = UBound(_y) + 2
                End If

                its += 1

            End Sub

        End Class

    End Class

End Namespace
