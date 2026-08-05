'    Petroleum Fractions Property Calculation Routines 
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

    Public Class PropertyMethods

        ''' <summary>
        ''' Calculates the Critical Temperature of a petroleum fraction with Riazi-Daubert method (1985).
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended for molecular weights between 70 and 300.</remarks>
        Public Shared Function Tc_RiaziDaubert(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Dim t1, t2 As Double
            t1 = -0.0009314 * PEMe - 0.544442 * d15 + 0.00064791 * PEMe * d15
            t2 = 9.5233 * Exp(-0.0009314 * PEMe - 0.544442 * d15 + 0.00064791 * PEMe * d15) * PEMe ^ 0.81067 * d15 ^ 0.53691
            Return t2
        End Function

        ''' <summary>
        ''' Calculates Critical Pressure of a petroleum fraction with Riazi-Daubert method (1985).
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Critical Pressure of the fraction, Pa.</returns>
        ''' <remarks>Recommended for molecular weights between 70 and 300.</remarks>
        Public Shared Function Pc_RiaziDaubert(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Dim t1, t2 As Double
            t1 = -0.008505 * PEMe - 4.8014 * d15 + 0.005749 * PEMe * d15
            t2 = 31958000000.0 * Exp(t1) * PEMe ^ -0.4844 * d15 ^ 4.0846
            Return t2
        End Function

        ''' <summary>
        ''' Calculates the Critical Temperature of a petroleum fraction with Riazi's method (2005).
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended for molecular weights higher than 300.</remarks>
        Public Shared Function Tc_Riazi(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Dim t1, t2 As Double
            t1 = -0.00069 * PEMe - 1.4442 * d15 + 0.000491 * PEMe * d15
            t2 = 35.9413 * Exp(t1) * PEMe ^ 0.7293 * d15 ^ 1.2771
            Return t2
        End Function

        ''' <summary>
        ''' Calculates the Critical Temperature of a petroleum fraction with Lee-Kesler method (1976).
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended method.</remarks>
        Public Shared Function Tc_LeeKesler(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Return 189.8 + 450.6 * d15 + (0.4244 + 0.1174 * d15) * PEMe + (0.1441 - 1.0069 * d15) * 100000.0 / PEMe
        End Function

        ''' <summary>
        ''' Calculates Critical Pressure of a petroleum fraction with Lee-Kesler's method (1976).
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Critical Pressure of the fraction, Pa.</returns>
        ''' <remarks>Recommended for molecular weights between 70 and 300.</remarks>
        Public Shared Function Pc_LeeKesler(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Return 1000000.0 * 0.986923 * Exp(5.689 - 0.0566 / d15 - (0.43639 + 4.1216 / d15 + 0.21343 / d15 ^ 2) * 0.001 * PEMe + (0.47579 + 1.182 / d15 + 0.15302 / d15 ^ 2) * 0.000001 * PEMe ^ 2 - (2.4505 + 9.9099 / d15 ^ 2) * 0.0000000001 * PEMe ^ 3)
        End Function

        ''' <summary>
        ''' Calculates the Critical Temperature of a petroleum fraction by the API-A/B method of Farah (2006)
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol (Tc = 450~900 K, Pc = 680~4100x10� Pa).</remarks>
        Public Shared Function Tc_Farah(ByVal A As Double, ByVal B As Double) As Double
            Return 731.968 + 291.952 * A - 704.998 * B
        End Function

        ''' <summary>
        ''' Calculates the Critical Temperature of a petroleum fraction by the API-A/B method of Farah (2006).
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol (Tc = 450~900 K, Pc = 680~4100x10� Pa).</remarks>
        Public Shared Function Tc_Farah(ByVal A As Double, ByVal B As Double, ByVal PEMe As Double) As Double
            Return 104.0061 + 38.75 * A - 41.6097 * B + 0.7831 * PEMe
        End Function

        ''' <summary>
        ''' Calculates the Critical Temperature of a petroleum fraction by the API-A/B method of Farah (2006).
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol (Tc = 450~900 K, Pc = 680~4100x10� Pa).</remarks>
        Public Shared Function Tc_Farah(ByVal A As Double, ByVal B As Double, ByVal d15 As Double, ByVal PEMe As Double) As Double
            Return 196.793 + 90.205 * A - 221.051 * B + 309.534 * d15 + 0.524 * PEMe
        End Function

        ''' <summary>
        ''' Calculates the Critical Pressure of a petroleum fraction by the API-A/B method of Farah (2006).
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <returns>Critical Pressure of the fraction, Pa.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol (Tc = 450~900 K, Pc = 680~4100x10� Pa).</remarks>
        Public Shared Function Pc_Farah(ByVal A As Double, ByVal B As Double) As Double
            Return Exp(20.0056 - 9.8758 * Log(A) + 12.2326 * Log(B))
        End Function

        ''' <summary>
        ''' Calculates the Critical Pressure of a petroleum fraction by the API-A/B method of Farah (2006).
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <returns>Critical Pressure of the fraction, Pa.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol (Tc = 450~900 K, Pc = 680~4100x10� Pa).</remarks>
        Public Shared Function Pc_Farah(ByVal A As Double, ByVal B As Double, ByVal PEMe As Double) As Double
            Return Exp(11.2037 - 0.5484 * A + 1.9242 * B + 510.1272 / PEMe)
        End Function

        ''' <summary>
        ''' Calculates the Critical Pressure of a petroleum fraction by the API-A/B method of Farah (2006).
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <returns>Critical Pressure of the fraction, Pa.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol (Tc = 450~900 K, Pc = 680~4100x10� Pa).</remarks>
        Public Shared Function Pc_Farah(ByVal A As Double, ByVal B As Double, ByVal PEMe As Double, ByVal d15 As Double) As Double
            Return Exp(28.7605 + 0.7158 * Log(A) - 0.2796 * Log(B) + 2.3129 * Log(d15) - 2.4027 * Log(PEMe))
        End Function

        ''' <summary>
        ''' Acentric Factor by the Lee-Kesler method (1976).
        ''' </summary>
        ''' <param name="Tc">Critical Temperature of the fraction, K.</param>
        ''' <param name="Pc">Critical Pressure of the fraction, Pa.</param>
        ''' <param name="PEMM">Molar Mean Boiling Point of the fraction, K.</param>
        ''' <returns>Acentric Factor of the fraction.</returns>
        ''' <remarks></remarks>
        Public Shared Function AcentricFactor_LeeKesler(ByVal Tc As Double, ByVal Pc As Double, ByVal PEMM As Double) As Double
            Return (-Math.Log(Pc / 101325) - 5.92714 + 6.09648 / (PEMM / Tc) + 1.28862 * Math.Log(PEMM / Tc) - 0.169347 * (PEMM / Tc) ^ 6) / (15.2518 - 15.6875 / (PEMM / Tc) - 13.4721 * Math.Log(PEMM / Tc) + 0.43577 * (PEMM / Tc) ^ 6)
        End Function

        ''' <summary>
        ''' Acentric Factor by Korsten's correlation (2000).
        ''' </summary>
        ''' <param name="Tc">Critical Temperature of the fraction, K.</param>
        ''' <param name="Pc">Critical Pressure of the fraction, Pa.</param>
        ''' <param name="PEMV">Volumetric Mean Boiling Point of the fraction, K.</param>
        ''' <returns>Acentric Factor of the fraction.</returns>
        ''' <remarks></remarks>
        Public Shared Function AcentricFactor_Korsten(ByVal Tc As Double, ByVal Pc As Double, ByVal PEMV As Double) As Double
            Return 0.5899 * ((PEMV / Tc) ^ 1.3) / (1 - (PEMV / Tc) ^ 1.3) * Log10(Pc / 101325) - 1
        End Function

        ''' <summary>
        ''' Winn's correlation (1957) for calculation of the molecular weight of petroleum fractions.
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Molecular weight of the fraction.</returns>
        ''' <remarks></remarks>
        Public Shared Function MW_Winn(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Return 0.00005805 * PEMe ^ 2.3776 / d15 ^ 0.9371
        End Function

        ''' <summary>
        ''' Riazi's correlation (1986) for calculation of the molecular weight of petroleum fractions.
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Molecular weight of the fraction.</returns>
        ''' <remarks>For light and medium fractions (PEMe 36~560 �C, d15 0.9688~0.63).</remarks>
        Public Shared Function MW_Riazi(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Dim t1 As Double
            t1 = 0.0002097 * PEMe - 7.78 * d15 + 0.00208476 * PEMe * d15
            Return 42.965 * Exp(t1) * PEMe ^ 1.26007 * d15 ^ 4.98308
        End Function

        ''' <summary>
        ''' Riazi's correlation (1986) for calculation of the molecular weight of petroleum fractions.
        ''' </summary>
        ''' <param name="v37">Kinematic viscosity of the fraction at 37.8 �C.</param>
        ''' <param name="v98">Kinematic viscosity of the fraction at 98.9 �C.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Molecular weight of the fraction.</returns>
        ''' <remarks>For heavy fractions.</remarks>
        Public Shared Function MW_Riazi(ByVal v37 As Double, ByVal v98 As Double, ByVal d15 As Double) As Double
            Return 223.56 * v37 ^ (1.2228 * d15 - 1.2435) * v98 ^ (-3.038 * d15 + 3.4758) * d15 ^ -0.6665
        End Function

        ''' <summary>
        ''' Lee-Kesler correlation (1974) for calculation of the molecular weight of petroleum fractions.
        ''' </summary>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Molecular weight of the fraction.</returns>
        ''' <remarks>For PEMe less than 750 K.</remarks>
        Public Shared Function MW_LeeKesler(ByVal PEMe As Double, ByVal d15 As Double) As Double
            Dim t1, t2, t3 As Double
            t1 = -12272.6 + 9486.4 * d15 + (8.3741 - 5.9917 * d15) * PEMe
            t2 = (1 - 0.77084 * d15 - 0.02058 * d15 ^ 2) * (0.7465 - 222.466 / PEMe) * 10000000.0 / PEMe
            t3 = (1 - 0.80882 * d15 - 0.02226 * d15 ^ 2) * (0.3228 - 17.335 / PEMe) * 1000000000000.0 / PEMe ^ 3
            Return t1 + t2 + t3
        End Function

        ''' <summary>
        ''' API-A/B method by Farah (2006) for calculation of the molecular weight of petroleum fractions.
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol.</remarks>
        Public Shared Function MW_Farah(ByVal A As Double, ByVal B As Double) As Double
            Return Exp(6.8117 + 1.3372 * A - 3.6283 * B)
        End Function

        ''' <summary>
        ''' API-A/B method by Farah (2006) for calculation of the molecular weight of petroleum fractions.
        ''' </summary>
        ''' <param name="A">A-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="B">B-Parameter of the Walther-ASTM viscosity-temperature equation.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <returns>Critical Temperature of the fraction, K.</returns>
        ''' <remarks>Recommended for MW between 72 and 500 kg/kmol.</remarks>
        Public Shared Function MW_Farah(ByVal A As Double, ByVal B As Double, ByVal d15 As Double, ByVal PEMe As Double) As Double
            Return Exp(4.0397 + 0.1362 * A - 0.3406 * B - 0.9988 * d15 + 0.0039 * PEMe)
        End Function

        ''' <summary>
        ''' 'Abbott's method (1971) for calculation of viscosity of petroleum fractions.
        ''' </summary>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <returns>Kinematic viscosity at 37.8 �C, in m2/s.</returns>
        ''' <remarks></remarks>
        Public Shared Function Visc37_Abbott(ByVal PEMe As Double, ByVal d15 As Double) As Double

            Dim Kw, API, v1 As Double
            Kw = (1.8 * PEMe) ^ (1 / 3) / d15
            API = 141.5 / d15 - 131.5
            v1 = 10 ^ (4.39371 - 1.94733 * Kw + 0.12769 * Kw ^ 2 + 0.00032629 * API ^ 2 - 0.0118246 * Kw * API + (0.171617 * Kw ^ 2 + 10.9943 * API + 0.0950663 * API ^ 2 - 0.860218 * Kw * API) / (API + 50.3642 - 4.78231 * Kw))
            'v1 = cSt
            Return v1 * 0.000001

        End Function

        ''' <summary>
        ''' 'Abbott's method (1971) for calculation of viscosity of petroleum fractions.
        ''' </summary>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <param name="PEMe">Median Boiling Point of the fraction, K.</param>
        ''' <returns>Kinematic viscosity at 98.9 �C, in m2/s.</returns>
        ''' <remarks></remarks>
        Public Shared Function Visc98_Abbott(ByVal PEMe As Double, ByVal d15 As Double) As Double

            Dim Kw, API, v1 As Double
            Kw = (1.8 * PEMe) ^ (1 / 3) / d15
            API = 141.5 / d15 - 131.5
            v1 = 10 ^ (-0.463634 - 0.166532 * API + 0.000513447 * API ^ 2 - 0.00848995 * API * Kw + (0.080325 * Kw + 1.24899 * API + 0.19768 * API ^ 2) / (API + 26.786 - 2.6296 * Kw))
            'v1 = cSt
            Return v1 * 0.000001

        End Function

        ''' <summary>
        ''' 'Beg and Amin's method (1989) for calculation of viscosity of petroleum fractions.
        ''' </summary>
        ''' <param name="T">Temperature in Kelvin.</param>
        ''' <param name="T50ASTM">Temperature relative to 50% vaporized in the ASTM D86 distillation curve.</param>
        ''' <param name="d15">Specific Gravity of the fraction at 15,6/15,6 �C.</param>
        ''' <returns>Viscosity of the petroleum fraction at specified T, in m2/s.</returns>
        ''' <remarks></remarks>
        Public Shared Function ViscT_Beg_Amin(ByVal T As Double, ByVal T50ASTM As Double, ByVal d15 As Double) As Double

            Dim A, B, API As Double
            API = 141.5 / d15 - 131.5
            B = Exp(5.471 + 0.00342 * T50ASTM)
            A = -0.0339 * API ^ 0.188 + 0.241 * T50ASTM / B
            Return A * Exp(B / T) * 0.000001

        End Function

        ''' <summary>
        ''' Calculates the A-parameter of the Walther-ASTM equation for viscosity of petroleum fractions.
        ''' </summary>
        ''' <param name="T1">Temperature relative to viscosity 1, in K.</param>
        ''' <param name="v1">Kinematic viscosity at T1, in m2/s.</param>
        ''' <param name="T2">Temperature relative to viscosity 2, in K.</param>
        ''' <param name="v2">Kinematic viscosity at T2, in m2/s.</param>
        ''' <returns>A-parameter of the Walther-ASTM equation.</returns>
        ''' <remarks></remarks>
        Public Shared Function ViscWaltherASTM_A(ByVal T1 As Double, ByVal v1 As Double, ByVal T2 As Double, ByVal v2 As Double) As Double

            Dim Vc1, Vc2, tt1, tt2 As Double
            Dim logz1, logz2 As Double
            Dim B As Double

            tt1 = Log10(T1)
            tt2 = Log10(T2)
            Vc1 = v1 * 1000000.0
            Vc2 = v2 * 1000000.0
            logz1 = CalcLogZ(Vc1)
            logz2 = CalcLogZ(Vc2)
            B = (logz2 - logz1) / (tt1 - tt2)
            Return logz2 + B * tt2
        End Function

        ''' <summary>
        ''' Calculates the B-parameter of the Walther-ASTM equation for viscosity of petroleum fractions.
        ''' </summary>
        ''' <param name="T1">Temperature relative to viscosity 1, in K.</param>
        ''' <param name="v1">Kinematic viscosity at T1, in m2/s.</param>
        ''' <param name="T2">Temperature relative to viscosity 2, in K.</param>
        ''' <param name="v2">Kinematic viscosity at T2, in m2/s.</param>
        ''' <returns>B-parameter of the Walther-ASTM equation.</returns>
        ''' <remarks></remarks>
        Public Shared Function ViscWaltherASTM_B(ByVal T1 As Double, ByVal v1 As Double, ByVal T2 As Double, ByVal v2 As Double) As Double

            Dim Vc1, Vc2, tt1, tt2 As Double
            Dim logz1, logz2 As Double

            tt1 = Log10(T1)
            tt2 = Log10(T2)
            Vc1 = v1 * 1000000.0
            Vc2 = v2 * 1000000.0
            logz1 = CalcLogZ(Vc1)
            logz2 = CalcLogZ(Vc2)
            Return (logz2 - logz1) / (tt1 - tt2)

        End Function

        Private Shared Function CalcLogZ(ByVal Vc As Double) As Double
            Dim c, d, e, f, g, h As Double
            If Vc < 2 Then
                c = Exp(-1.14883 - 2.65868 * Vc)
                If Vc < 1.65 Then
                    d = Exp(-0.0038138 - 12.5645 * Vc)
                    If Vc < 0.9 Then
                        e = Exp(5.46491 - 37.6289 * Vc)
                        If Vc < 0.3 Then
                            f = Exp(13.0458 - 74.6851 * Vc)
                            g = Exp(37.4619 - 192.643 * Vc)
                            If Vc < 0.24 Then
                                h = Exp(80.4945 - 400.468 * Vc)
                            End If
                        End If
                    End If
                End If
            End If
            Return Log10(Log10(Vc + 0.7 + c - d + e - f + g - h))
        End Function

        ''' <summary>
        ''' Twu's method for calculation of viscosity at any temperature from two known values.
        ''' </summary>
        ''' <param name="T">Temperature in K.</param>
        ''' <param name="T1">Temperature of viscosity 1, in K.</param>
        ''' <param name="T2">Temperature of viscosity 2, in K.</param>
        ''' <param name="v1">Kinematic viscosity 1, in m2/s.</param>
        ''' <param name="v2">Kinematic viscosity 2, in m2/s.</param>
        ''' <returns>Kinematic viscosity @ T, in m2/s.</returns>
        ''' <remarks></remarks>
        Public Shared Function ViscTwu(ByVal T As Double, ByVal T1 As Double, ByVal T2 As Double, ByVal v1 As Double, ByVal v2 As Double) As Double

            Dim Z, Z1, Z2, B, vk1, vk2

            vk1 = v1 * 1000000
            vk2 = v2 * 1000000

            T = 1.8 * T
            T1 = 1.8 * T1
            T2 = 1.8 * T2

            Z1 = vk1 + 0.7 + Math.Exp(-1.47 - 1.84 * vk1 - 0.51 * vk1 ^ 2)
            Z2 = vk2 + 0.7 + Math.Exp(-1.47 - 1.84 * vk2 - 0.51 * vk2 ^ 2)

            Dim var1 = (Math.Log(Math.Log(Z1)) - Math.Log(Math.Log(Z2)))
            Dim var2 = (Math.Log(T1) - Math.Log(T2))
            B = var1 / var2

            Z = Math.Exp(Math.Exp(Math.Log(Math.Log(Z1)) + B * (Math.Log(T) - Math.Log(T1))))

            Dim tmp = Z - 0.7 - Math.Exp(-0.7487 - 3.295 * (Z - 0.7) + 0.6119 * (Z - 0.7) ^ 2 - 0.3193 * (Z - 0.7) ^ 3)

            Return tmp * 0.000001

        End Function

        ''' <summary>
        ''' Specific gravity at 15,6 �C from viscosity data.
        ''' </summary>
        ''' <param name="v37">Kinematic viscosity at 37,8 �C, in m2/s.</param>
        ''' <param name="v98">Kinematic viscosity at 98,9 �C, in m2/s.</param>
        ''' <returns>Specific gravity at 15,6 �C.</returns>
        ''' <remarks>Author unknown.</remarks>
        Public Shared Function d15_v37v98(ByVal v37 As Double, ByVal v98 As Double) As Double
            Dim vc37, vc98 As Double
            vc98 = v98 * 1000000.0
            vc37 = v37 * 1000000.0
            Return 0.7717 * vc37 ^ 0.1157 * vc98 ^ -0.1616
        End Function

        ''' <summary>
        ''' Density conversion function.
        ''' </summary>
        ''' <param name="d15">d20</param>
        ''' <returns>d20</returns>
        ''' <remarks></remarks>
        Public Shared Function d20d15(ByVal d15 As Double) As Double
            If d15 < 0.934 Then
                Return -0.0166 * d15 ^ 2 + 1.0311 * d15 - 0.0182
            Else
                Return 1.2394 * d15 ^ 3 - 3.7387 * d15 ^ 2 + 4.7524 * d15 - 1.2566
            End If
        End Function

        ''' <summary>
        ''' Density conversion function.
        ''' </summary>
        ''' <param name="d20">d20</param>
        ''' <returns>d15</returns>
        ''' <remarks></remarks>
        Public Shared Function d15d20(ByVal d20 As Double) As Double
            If d20 < 0.639 Then
                Return 0.0638 * d20 ^ 2 + 0.8769 * d20 + 0.0628
            Else
                Return 0.0156 * d20 ^ 2 + 0.9706 * d20 + 0.0175
            End If
        End Function

        ''' <summary>
        ''' Specific gravity at 15,6 �C by Riazi and Al-Sahhaf's method (1996)
        ''' </summary>
        ''' <param name="MW">Molecular weight of the fraction.</param>
        ''' <returns>Specific gravity at 15,6 �C.</returns>
        ''' <remarks>For SCN groups.</remarks>
        Public Shared Function d15_Riazi(ByVal MW As Double) As Double
            Return 1.07 - Math.Exp(3.56073 - 2.93886 * MW ^ 0.1)
        End Function

        ''' <summary>
        ''' Refractive index parameter I (Lorentz-Lorenz) from Tb (K) and SG at 15 C.
        ''' Riazi & Daubert (1987), API TDB method.
        ''' Valid for MW 70-300 (light/medium petroleum fractions).
        ''' </summary>
        Public Shared Function RefractiveIndexParam_Riazi(ByVal Tb As Double, ByVal SG As Double) As Double
            Return 0.3773 * Tb ^ (-0.02269) * SG ^ 0.9182
        End Function

        ''' <summary>
        ''' Refractive index at 20 C from Tb (K) and SG. Uses Riazi I parameter
        ''' and Lorentz-Lorenz relation n = sqrt((1+2I)/(1-I)).
        ''' </summary>
        Public Shared Function RefractiveIndex_Riazi(ByVal Tb As Double, ByVal SG As Double) As Double
            Dim I As Double = RefractiveIndexParam_Riazi(Tb, SG)
            If I >= 0.5 Then I = 0.49
            Return Math.Sqrt((1.0 + 2.0 * I) / (1.0 - I))
        End Function

        ''' <summary>
        ''' Refractivity intercept Ri (Kurtz-Ward) = n20 - d20/2.
        ''' SG here is d15; d20 is approximated as SG - 0.0035 (typical shift).
        ''' </summary>
        Public Shared Function RefractivityIntercept(ByVal n20 As Double, ByVal SG As Double) As Double
            Dim d20 As Double = d20d15(SG)
            Return n20 - d20 / 2.0
        End Function

        ''' <summary>
        ''' Carbon-to-hydrogen weight ratio CH from Tb (K) and SG.
        ''' Riazi (2005) correlation, eq. 2.115 of MNL50.
        ''' </summary>
        Public Shared Function CHRatio_Riazi(ByVal Tb As Double, ByVal SG As Double) As Double
            Return 3.4707 * Math.Exp(0.01485 * Tb + 16.94 * SG - 0.012492 * Tb * SG) * Tb ^ (-2.725) * SG ^ 6.798
        End Function

        ''' <summary>
        ''' Paraffin/Naphthene/Aromatic mass fractions of a petroleum fraction.
        ''' Returns {xP, xN, xA} summing to 1. Uses Riazi (2005) correlations:
        '''   MW &lt; 200  : eq. 3.77 - uses SG and m-factor m = MW·(n20-1.475).
        '''   MW &gt;= 200 : eq. 3.78 - uses Ri (refractivity intercept) and CH ratio.
        ''' When n20 is not provided (NaN), it is estimated from (Tb, SG) via Riazi.
        ''' Fractions are clamped to [0, 1] and renormalized.
        ''' </summary>
        Public Shared Function PNA_Riazi(ByVal Tb As Double, ByVal SG As Double, ByVal MW As Double, ByVal n20 As Double) As Double()

            If Double.IsNaN(n20) OrElse n20 <= 0 Then
                n20 = RefractiveIndex_Riazi(Tb, SG)
            End If

            Dim xP, xN, xA As Double

            If MW < 200.0 Then
                Dim m As Double = MW * (n20 - 1.475)
                xP = 3.7387 - 4.0829 * SG + 0.014772 * m
                xN = -1.5027 + 2.10152 * SG - 0.02388 * m
                xA = 1.0 - xP - xN
            Else
                Dim Ri As Double = RefractivityIntercept(n20, SG)
                Dim CH As Double = CHRatio_Riazi(Tb, SG)
                xP = 1.9842 - 0.27722 * Ri - 0.15643 * CH
                xN = 0.5977 - 0.761745 * Ri + 0.068048 * CH
                xA = 1.0 - xP - xN
            End If

            If xP < 0 Then xP = 0
            If xN < 0 Then xN = 0
            If xA < 0 Then xA = 0
            Dim s As Double = xP + xN + xA
            If s > 0 Then
                xP /= s : xN /= s : xA /= s
            End If

            Return New Double() {xP, xN, xA}

        End Function

        ''' <summary>
        ''' Twu (1984) n-paraffin reference properties as a function of normal boiling point.
        ''' Input Tb in K; returns {Tc_P (K), Pc_P (Pa), Vc_P (m3/kmol), SG_P}.
        ''' All internal math in degrees Rankine and psia per the original paper.
        ''' </summary>
        Public Shared Function Twu_NpReference(ByVal Tb As Double) As Double()

            Dim TbR As Double = Tb * 1.8
            Dim TcR As Double = TbR / (0.533272 + 0.000191017 * TbR _
                                       + 0.000000779681 * TbR ^ 2 _
                                       - 0.0000000000284376 * TbR ^ 3 _
                                       + 0.959468E+28 / TbR ^ 13)

            Dim alpha As Double = 1.0 - TbR / TcR
            If alpha < 0 Then alpha = 0

            Dim PcPsia As Double = (3.83354 + 1.19629 * alpha ^ 0.5 _
                                    + 34.8888 * alpha _
                                    + 36.1952 * alpha ^ 2 _
                                    + 104.193 * alpha ^ 4) ^ 2

            Dim VcFt3Lbmol As Double = (1.0 - (0.419869 - 0.505839 * alpha _
                                               - 1.56436 * alpha ^ 3 _
                                               - 9481.7 * alpha ^ 14)) ^ (-8)

            Dim SGp As Double = 0.843593 - 0.128624 * alpha _
                                - 3.36159 * alpha ^ 3 _
                                - 13749.5 * alpha ^ 12

            Dim TcK As Double = TcR / 1.8
            Dim PcPa As Double = PcPsia * 6894.757
            Dim VcM3Kmol As Double = VcFt3Lbmol * 0.0624279606

            Return New Double() {TcK, PcPa, VcM3Kmol, SGp}

        End Function

        ''' <summary>
        ''' Critical Temperature via Twu (1984) n-paraffin perturbation.
        ''' Recommended for heavy fractions (MW &gt; 200) where Riazi-Daubert loses accuracy.
        ''' </summary>
        Public Shared Function Tc_Twu(ByVal Tb As Double, ByVal SG As Double) As Double
            Dim ref = Twu_NpReference(Tb)
            Dim TcP As Double = ref(0)
            Dim SGp As Double = ref(3)
            Dim TbR As Double = Tb * 1.8
            Dim dSG As Double = Math.Exp(5.0 * (SGp - SG)) - 1.0
            Dim fT As Double = dSG * (-0.362456 / TbR ^ 0.5 _
                                      + (0.0398285 - 0.948125 / TbR ^ 0.5) * dSG)
            Return TcP * ((1.0 + 2.0 * fT) / (1.0 - 2.0 * fT)) ^ 2
        End Function

        ''' <summary>
        ''' Critical Volume (m3/kmol) via Twu (1984) n-paraffin perturbation.
        ''' </summary>
        Public Shared Function Vc_Twu(ByVal Tb As Double, ByVal SG As Double) As Double
            Dim ref = Twu_NpReference(Tb)
            Dim VcP As Double = ref(2)
            Dim SGp As Double = ref(3)
            Dim TbR As Double = Tb * 1.8
            Dim dSG As Double = Math.Exp(4.0 * (SGp ^ 2 - SG ^ 2)) - 1.0
            Dim fV As Double = dSG * (0.46659 / TbR ^ 0.5 _
                                      + (-0.182421 + 3.01721 / TbR ^ 0.5) * dSG)
            Return VcP * ((1.0 + 2.0 * fV) / (1.0 - 2.0 * fV)) ^ 2
        End Function

        ''' <summary>
        ''' Critical Pressure (Pa) via Twu (1984) n-paraffin perturbation.
        ''' </summary>
        Public Shared Function Pc_Twu(ByVal Tb As Double, ByVal SG As Double) As Double
            Dim ref = Twu_NpReference(Tb)
            Dim TcP As Double = ref(0)
            Dim PcP As Double = ref(1)
            Dim VcP As Double = ref(2)
            Dim SGp As Double = ref(3)
            Dim TbR As Double = Tb * 1.8
            Dim Tc As Double = Tc_Twu(Tb, SG)
            Dim Vc As Double = Vc_Twu(Tb, SG)
            Dim dSG As Double = Math.Exp(0.5 * (SGp - SG)) - 1.0
            Dim fP As Double = dSG * ((2.53262 - 46.1955 / TbR ^ 0.5 - 0.00127885 * TbR) _
                                      + (-11.4277 + 252.14 / TbR ^ 0.5 + 0.00230535 * TbR) * dSG)
            Return PcP * (Tc / TcP) * (VcP / Vc) * ((1.0 + 2.0 * fP) / (1.0 - 2.0 * fP)) ^ 2
        End Function

        ''' <summary>
        ''' PNA-weighted Critical Temperature. Averages Riazi-Daubert Tc computed with
        ''' class-specific SG references (Riazi-Alsahhaf 1996) weighted by xP, xN, xA.
        ''' Falls back to regular Riazi-Daubert when xP+xN+xA is zero.
        ''' </summary>
        Public Shared Function Tc_PNAWeighted(ByVal Tb As Double, ByVal SG As Double, ByVal MW As Double, ByVal xP As Double, ByVal xN As Double, ByVal xA As Double) As Double
            Dim s = xP + xN + xA
            If s <= 0 Then Return Tc_RiaziDaubert(Tb, SG)
            xP /= s : xN /= s : xA /= s
            'Riazi-Alsahhaf 1996 class SG vs MW (eq. 2.42 family): SG_i = SG_inf,i - exp(a_i - b_i*MW^c_i)
            Dim sgP As Double = 0.85 - Math.Exp(-3.8405 + 0.6 * MW ^ 0.1)
            Dim sgN As Double = 0.9 - Math.Exp(-2.2781 + 0.3 * MW ^ 0.1)
            Dim sgA As Double = 1.05 - Math.Exp(-1.4 + 0.2 * MW ^ 0.1)
            If sgP <= 0 OrElse sgN <= 0 OrElse sgA <= 0 Then Return Tc_RiaziDaubert(Tb, SG)
            Dim tcP As Double = Tc_RiaziDaubert(Tb, sgP)
            Dim tcN As Double = Tc_RiaziDaubert(Tb, sgN)
            Dim tcA As Double = Tc_RiaziDaubert(Tb, sgA)
            Return xP * tcP + xN * tcN + xA * tcA
        End Function

        ''' <summary>
        ''' PNA-weighted Critical Pressure, class-specific Riazi-Daubert with SG per class.
        ''' </summary>
        Public Shared Function Pc_PNAWeighted(ByVal Tb As Double, ByVal SG As Double, ByVal MW As Double, ByVal xP As Double, ByVal xN As Double, ByVal xA As Double) As Double
            Dim s = xP + xN + xA
            If s <= 0 Then Return Pc_RiaziDaubert(Tb, SG)
            xP /= s : xN /= s : xA /= s
            Dim sgP As Double = 0.85 - Math.Exp(-3.8405 + 0.6 * MW ^ 0.1)
            Dim sgN As Double = 0.9 - Math.Exp(-2.2781 + 0.3 * MW ^ 0.1)
            Dim sgA As Double = 1.05 - Math.Exp(-1.4 + 0.2 * MW ^ 0.1)
            If sgP <= 0 OrElse sgN <= 0 OrElse sgA <= 0 Then Return Pc_RiaziDaubert(Tb, SG)
            Dim pcP As Double = Pc_RiaziDaubert(Tb, sgP)
            Dim pcN As Double = Pc_RiaziDaubert(Tb, sgN)
            Dim pcA As Double = Pc_RiaziDaubert(Tb, sgA)
            Return xP * pcP + xN * pcN + xA * pcA
        End Function

    End Class

End Namespace
