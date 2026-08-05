'    Petroleum Cold Flow Properties
'    Copyright 2009-2025 Daniel Wagner O. de Medeiros
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
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.Streams

Namespace Utilities.PetroleumProperties

    ''' <summary>Cold flow properties of a petroleum stream. All values in SI units.</summary>
    Public Class ColdFlowResults

        Public Property TrueVaporPressure As Double
        Public Property ReidVaporPressure As Double
        Public Property Viscosity37C As Double
        Public Property Viscosity98C As Double
        Public Property FlashPoint As Double
        Public Property PourPoint As Double
        Public Property CloudPoint As Double
        Public Property FreezingPoint As Double
        Public Property RefractionIndex As Double
        Public Property CetaneIndex As Double

        Public Property MeanAverageBoilingPoint As Double
        Public Property SpecificGravity As Double
        Public Property WatsonK As Double
        Public Property API As Double

    End Class

    ''' <summary>
    ''' API cold flow correlations (procedures 2B5.1, 2B7.1, 2B8.1, 2B11.1, 2B12.1 and 2B13.1)
    ''' for a petroleum material stream. Shared by the WinForms and the Avalonia utilities.
    ''' </summary>
    Public Class ColdFlowProperties

        ''' <summary>
        ''' Calculates the cold flow properties of a copy of the given stream. The stream is
        ''' modified during the calculation, so pass a clone.
        ''' </summary>
        Public Shared Function Calculate(mat As MaterialStream) As ColdFlowResults

            Dim res As New ColdFlowResults

            Dim pp As PropertyPackages.PropertyPackage = mat.PropertyPackage
            Dim MABP, CABP, MeABP, K, SG, API As Double
            Dim TVP, v1, v2, kv1, t10ASTM, t10TBP, bt, dp As Double

            pp.CurrentMaterialStream = mat

            If TypeOf pp Is PropertyPackages.BlackOilPropertyPackage Then

                Dim bopp As PropertyPackages.BlackOilPropertyPackage = DirectCast(pp, PropertyPackages.BlackOilPropertyPackage)

                Dim bof = bopp.CalcBOFluid(bopp.RET_VMOL(PropertyPackages.Phase.Mixture), bopp.DW_GetConstantProperties)

                Dim bop As New PropertyPackages.Auxiliary.BlackOilProperties

                MeABP = bop.LiquidNormalBoilingPoint(bof.SGO, bof.BSW)
                SG = bof.SGO
                K = (1.8 * MeABP) ^ (1 / 3) / SG
                API = 141.5 / SG - 131.5

                TVP = bopp.DW_CalcBubP(bopp.RET_VMOL(PropertyPackages.Phase.Mixture), 310.95, 101325)(0)

                v1 = bopp.DW_CalcViscosidadeDinamica_ISOL(PropertyPackages.Phase.Liquid, 310.95, 101325)
                kv1 = v1 / bopp.DW_CalcMassaEspecifica_ISOL(PropertyPackages.Phase.Liquid, 310.95, 101325)
                v2 = bopp.DW_CalcViscosidadeDinamica_ISOL(PropertyPackages.Phase.Liquid, 372.05, 101325)

                t10ASTM = MeABP * 0.9

            Else

                Dim ppi As New PropertyPackages.RaoultPropertyPackage
                ppi.CurrentMaterialStream = mat

                MABP = 0
                CABP = 0

                Dim i As Integer = 0
                Dim Vx(mat.Phases(0).Compounds.Count - 1) As Double
                For Each subst As Compound In mat.Phases(0).Compounds.Values
                    MABP += subst.MoleFraction.GetValueOrDefault * subst.ConstantProperties.Normal_Boiling_Point
                    CABP += subst.MoleFraction.GetValueOrDefault * subst.ConstantProperties.Normal_Boiling_Point ^ (1 / 3)
                    Vx(i) = subst.MoleFraction.GetValueOrDefault
                    i = i + 1
                Next
                CABP = CABP ^ 3
                MeABP = (MABP + CABP) / 2

                SG = pp.DW_CalcMassaEspecifica_ISOL(PropertyPackages.Phase.Liquid, 288.706, 101325) / 999
                K = (1.8 * MeABP) ^ (1 / 3) / SG
                API = 141.5 / SG - 131.5

                TVP = pp.DW_CalcBubP(Vx, 310.95, 101325)(4)
                v1 = pp.DW_CalcViscosidadeDinamica_ISOL(PropertyPackages.Phase.Liquid, 310.95, 101325)
                kv1 = v1 / pp.DW_CalcMassaEspecifica_ISOL(PropertyPackages.Phase.Liquid, 310.95, 101325)
                v2 = pp.DW_CalcViscosidadeDinamica_ISOL(PropertyPackages.Phase.Liquid, 372.05, 101325)

                Try
                    bt = pp.DW_CalcBubT(Vx, 101325)(4)
                Catch ex As Exception
                    bt = ppi.DW_CalcBubT(Vx, 101325)(4)
                End Try
                Try
                    dp = pp.DW_CalcDewP(Vx, 310.95)(4)
                Catch ex As Exception
                    dp = ppi.DW_CalcDewP(Vx, 310.95)(4)
                End Try

                If dp < 0 Or Double.IsNaN(dp) Or Double.IsInfinity(dp) Then
                    dp = ppi.DW_CalcDewP(Vx, 310.95)(4)
                End If

                t10TBP = Calc10PercentTBP(mat, pp, ppi, bt)

                t10ASTM = (t10TBP / 0.5564) ^ (1 / 1.09)

            End If

            'API Procedure 2B5.1
            Dim Huang_I = 0.02266 * Exp(0.0003905 * (1.8 * MeABP) + 2.468 * SG - 0.0005704 * (1.8 * MeABP) * SG) * (1.8 * MeABP) ^ 0.0572 * SG ^ -0.72
            res.RefractionIndex = ((1 + 2 * Huang_I) / (1 - Huang_I)) ^ 0.5

            'API Procedure 2B7.1 (Pensky-Martens Closed Cup - ASTM D93)
            Dim fp = 0.69 * ((t10ASTM - 273.15) * 9 / 5 + 32) - 118.2
            res.FlashPoint = (fp - 32) * 5 / 9 + 273.15

            'API Procedure 2B8.1
            res.PourPoint = (753 + 136 * (1 - Exp(-0.15 * kv1 * 1000000.0)) - 572 * SG + 0.0512 * kv1 * 1000000.0 + 0.139 * (MeABP * 1.8)) / 1.8

            'API Procedure 2B11.1
            res.FreezingPoint = (-2390.42 + 1826 * SG + 122.49 * K - 0.135 * 1.8 * MeABP) / 1.8

            'API Procedure 2B12.1
            res.CloudPoint = (10 ^ (-7.41 + 5.49 * Log10(MeABP * 1.8) - 0.712 * (1.8 * MeABP) ^ 0.315 - 0.133 * SG)) / 1.8

            'API Procedure 2B13.1
            res.CetaneIndex = 415.26 - 7.673 * API + 0.186 * (MeABP * 1.8 - 458.67) + 3.503 * API * Log10(1.8 * MeABP - 458.67) - 193.816 * Log10(MeABP * 1.8 - 458.67)

            'Reid Vapor Pressure
            'reference: https://www.epa.gov/air-emissions-factors-and-quantification/ap-42-fifth-edition-volume-i-chapter-7-liquid-storage-0 , page 7.1-82
            res.ReidVaporPressure = (10 ^ ((Log(TVP / 6894.76) + 7261 / (310.95 * 1.8) - 12.82) / (2799 / (310.95 * 1.8) - 2.227))) * 6894.76

            res.TrueVaporPressure = TVP
            res.Viscosity37C = v1
            res.Viscosity98C = v2
            res.MeanAverageBoilingPoint = MeABP
            res.SpecificGravity = SG
            res.WatsonK = K
            res.API = API

            Return res

        End Function

        ''' <summary>
        ''' Temperature at which 10 % of the liquid has vaporized at 1 atm, found by a secant
        ''' iteration on the volumetric vapor fraction.
        ''' </summary>
        Private Shared Function Calc10PercentTBP(mat As MaterialStream,
                                                 pp As PropertyPackages.PropertyPackage,
                                                 ppi As PropertyPackages.PropertyPackage,
                                                 bt As Double) As Double

            Dim tmp, vv, vl, dv, dl As Object
            Dim t, t_ant, t_ant2, ft, ft_ant, ft_ant2, v As Double
            Dim i As Integer = 0, j As Integer

            t = bt + 15

            Do
                ft_ant2 = ft_ant
                ft_ant = ft
                Try
                    tmp = pp.FlashBase.Flash_PT(pp.RET_VMOL(PropertyPackages.Phase.Mixture), 101325, t, pp)
                Catch ex As Exception
                    tmp = ppi.FlashBase.Flash_PT(pp.RET_VMOL(PropertyPackages.Phase.Mixture), 101325, t, pp)
                End Try
                v = tmp(1)
                vv = tmp(3)
                vl = tmp(2)

                mat.Phases(0).Properties.temperature = t
                mat.Phases(0).Properties.pressure = 101325
                j = 0
                For Each subst As Compound In mat.Phases(1).Compounds.Values
                    subst.MoleFraction = vl(j)
                    j += 1
                Next
                pp.DW_CalcProp("density", PropertyPackages.Phase.Liquid)
                dl = mat.Phases(1).Properties.density.GetValueOrDefault
                j = 0
                For Each subst As Compound In mat.Phases(1).Compounds.Values
                    subst.MoleFraction = vv(j)
                    j += 1
                Next
                pp.DW_CalcProp("density", PropertyPackages.Phase.Liquid)
                dv = mat.Phases(1).Properties.density.GetValueOrDefault

                If v = 0 Then v = i * 0.0001

                ft = v - (0.1 / dv) / ((0.1 / dv) + (0.9 / dl))

                t_ant2 = t_ant
                t_ant = t
                If i > 2 Then
                    If ft <> ft_ant2 Then
                        t = t - 0.3 * ft * (t - t_ant2) / (ft - ft_ant2)
                    End If
                Else
                    t = t - 1
                End If
                i = i + 1
            Loop Until Abs(ft) < 0.001 Or t < 0 Or Double.IsNaN(t) Or Double.IsInfinity(t) Or i > 200

            Return t

        End Function

    End Class

End Namespace
