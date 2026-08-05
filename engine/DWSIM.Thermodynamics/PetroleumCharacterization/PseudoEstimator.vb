'    Bulk Pseudocompound Property Estimator
'    Copyright 2025 Daniel Wagner O. de Medeiros
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

Imports System.Dynamic
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.PetroleumCharacterization.Methods
Imports DWSIM.SharedClasses.Utilities.PetroleumCharacterization.Contaminants
Imports prop = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS

Namespace Utilities.PetroleumCharacterization

    ''' <summary>
    ''' One row of user data for a pseudocompound. All temperatures and pressures are in SI units.
    ''' Properties left as Nothing are estimated and written back, so the caller can tell which
    ''' values came from the correlations.
    ''' </summary>
    Public Class PseudoCompoundInput

        Public Property Name As String = ""

        Public Property MW As Double?
        Public Property NBP As Double?
        Public Property SG As Double?
        Public Property Tc As Double?
        Public Property Pc As Double?
        Public Property AF As Double?

        Public Property xP As Double?
        Public Property xN As Double?
        Public Property xA As Double?

        ''' <summary>
        ''' Contaminant values in the order sulfur, nitrogen, mercaptan sulfur, Ni, V, Fe, Na,
        ''' CCR, asphaltenes and TAN. Nothing entries are left out of the compound.
        ''' </summary>
        Public Property Contaminants As Double?() = New Double?(9) {}

    End Class

    ''' <summary>
    ''' Fills in the missing constant properties of a petroleum pseudocompound from whatever
    ''' combination of MW, NBP and SG the user supplied. Shared by the bulk pseudocompound
    ''' tools of the WinForms and the Avalonia interfaces.
    ''' </summary>
    Public Class PseudoEstimator

        Public Property MWMethod As String = "Riazi (1986)"
        Public Property TcMethod As String = "Riazi-Daubert (1985)"
        Public Property PcMethod As String = "Riazi-Daubert (1985)"
        Public Property AFMethod As String = "Lee-Kesler (1976)"

        Private Shared ReadOnly ContaminantKeys As String() = {
            CompoundContaminants.K_WtPctSulfur,
            CompoundContaminants.K_WtPctNitrogen,
            CompoundContaminants.K_MercaptanSulfurWtPct,
            CompoundContaminants.K_Ni_ppm_wt,
            CompoundContaminants.K_V_ppm_wt,
            CompoundContaminants.K_Fe_ppm_wt,
            CompoundContaminants.K_Na_ppm_wt,
            CompoundContaminants.K_CCR_wt_pct,
            CompoundContaminants.K_AsphaltenesWtPct,
            CompoundContaminants.K_TAN_mgKOH_per_g}

        ''' <summary>Normal boiling point from molar weight, inverting the Riazi MW correlation.</summary>
        Public Shared Function NBPFromMW(mw As Double) As Double
            Return 1080 - Math.Exp(6.97996 - 0.01964 * mw ^ (2 / 3))
        End Function

        ''' <summary>Molar weight from the normal boiling point, the inverse of <see cref="NBPFromMW"/>.</summary>
        Public Shared Function MWFromNBP(nbp As Double) As Double
            If nbp < 1080 Then
                Return (1.0 / 0.01964 * (6.97996 - Math.Log(1080.0 - nbp))) ^ 1.5
            Else
                Return (1.0 / 0.01964 * (6.97996 + Math.Log(-1080.0 + nbp))) ^ 1.5
            End If
        End Function

        Private Function MWFromNBPandSG(nbp As Double, sg As Double) As Double
            Select Case MWMethod
                Case "Winn (1956)"
                    Return PropertyMethods.MW_Winn(nbp, sg)
                Case "Lee-Kesler (1974)"
                    Return PropertyMethods.MW_LeeKesler(nbp, sg)
                Case Else
                    Return PropertyMethods.MW_Riazi(nbp, sg)
            End Select
        End Function

        ''' <summary>
        ''' Estimates the missing properties of a single pseudocompound. The estimated values are
        ''' written back into <paramref name="input"/>.
        ''' </summary>
        Public Function Estimate(input As PseudoCompoundInput, index As Integer) As ConstantProperties

            Dim mw, tb, sg As Double

            If input.MW.HasValue Then
                mw = input.MW.Value
                tb = If(input.NBP, NBPFromMW(mw))
                sg = If(input.SG, PropertyMethods.d15_Riazi(mw))
            ElseIf input.SG.HasValue Then
                sg = input.SG.Value
                If input.NBP.HasValue Then
                    tb = input.NBP.Value
                Else
                    'seed the molar weight from the specific gravity, then close the loop on NBP
                    mw = ((Math.Log(1.07 - sg) - 3.56073) / (-2.93886)) ^ 10
                    tb = NBPFromMW(mw)
                End If
                mw = MWFromNBPandSG(tb, sg)
            ElseIf input.NBP.HasValue Then
                tb = input.NBP.Value
                mw = MWFromNBP(tb)
                sg = PropertyMethods.d15_Riazi(mw)
                mw = MWFromNBPandSG(tb, sg)
            Else
                Throw New Exception(String.Format("Row {0} ('{1}'): provide at least one of MW, NBP or SG.", index + 1, input.Name))
            End If

            Dim comp As New ConstantProperties()

            comp.Name = input.Name
            comp.Molar_Weight = mw
            comp.NBP = tb
            comp.Normal_Boiling_Point = tb

            Dim T1 = 37.8 + 273.15
            Dim T2 = 98.9 + 273.15

            Dim v37 = PropertyMethods.Visc37_Abbott(tb, sg)
            Dim v98 = PropertyMethods.Visc98_Abbott(tb, sg)

            comp.PF_Tv1 = T1
            comp.PF_Tv2 = T2
            comp.PF_v1 = v37
            comp.PF_v2 = v98
            comp.PF_vA = PropertyMethods.ViscWaltherASTM_A(T1, v37, T2, v98)
            comp.PF_vB = PropertyMethods.ViscWaltherASTM_B(T1, v37, T2, v98)
            comp.PF_SG = sg
            comp.PF_MM = mw

            comp.IsPF = 1

            Dim tc, pc, af As Double

            If input.Tc.HasValue Then
                tc = input.Tc.Value
            Else
                Select Case TcMethod
                    Case "Riazi (2005)"
                        tc = PropertyMethods.Tc_Riazi(tb, sg)
                    Case "Lee-Kesler (1976)"
                        tc = PropertyMethods.Tc_LeeKesler(tb, sg)
                    Case "Farah (2006)"
                        tc = PropertyMethods.Tc_Farah(comp.PF_vA.GetValueOrDefault, comp.PF_vB.GetValueOrDefault, tb, sg)
                    Case Else
                        tc = PropertyMethods.Tc_RiaziDaubert(tb, sg)
                End Select
            End If

            If input.Pc.HasValue Then
                pc = input.Pc.Value
            Else
                Select Case PcMethod
                    Case "Riazi (2005)"
                        pc = PropertyMethods.Pc_Riazi(tb, sg)
                    Case "Lee-Kesler (1976)"
                        pc = PropertyMethods.Pc_LeeKesler(tb, sg)
                    Case "Farah (2006)"
                        pc = PropertyMethods.Pc_Farah(comp.PF_vA.GetValueOrDefault, comp.PF_vB.GetValueOrDefault, tb, sg)
                    Case Else
                        pc = PropertyMethods.Pc_RiaziDaubert(tb, sg)
                End Select
            End If

            If input.AF.HasValue Then
                af = input.AF.Value
            Else
                Select Case AFMethod
                    Case "Korsten (2000)"
                        af = PropertyMethods.AcentricFactor_Korsten(tc, pc, tb)
                    Case Else
                        af = PropertyMethods.AcentricFactor_LeeKesler(tc, pc, tb)
                End Select
            End If

            comp.Critical_Temperature = tc
            comp.Critical_Pressure = pc
            comp.Acentric_Factor = af

            If input.xP.HasValue OrElse input.xN.HasValue OrElse input.xA.HasValue Then
                Dim p = input.xP.GetValueOrDefault
                Dim n = input.xN.GetValueOrDefault
                Dim a = input.xA.GetValueOrDefault
                Dim sum = p + n + a
                If sum > 0.0 Then
                    comp.PF_xP = p / sum
                    comp.PF_xN = n / sum
                    comp.PF_xA = a / sum
                End If
            End If

            If input.Contaminants IsNot Nothing Then
                For ck = 0 To Math.Min(input.Contaminants.Length, ContaminantKeys.Length) - 1
                    If Not input.Contaminants(ck).HasValue Then Continue For
                    If comp.ExtraProperties Is Nothing Then comp.ExtraProperties = New ExpandoObject()
                    Dim d = DirectCast(comp.ExtraProperties, IDictionary(Of String, Object))
                    d(ContaminantKeys(ck)) = input.Contaminants(ck).Value
                Next
            End If

            comp.PF_Watson_K = (1.8 * tb) ^ (1 / 3) / sg
            comp.Critical_Compressibility = prop.Zc1(af)
            comp.Critical_Volume = 8314 * comp.Critical_Compressibility * tc / pc
            comp.Z_Rackett = prop.Zc1(af)
            If comp.Z_Rackett < 0 Then comp.Z_Rackett = 0.2

            Dim gl As New Utilities.PetroleumCharacterization.Methods.GL
            Dim tmp = gl.calculate_Hf_Sf(sg, mw, tb)

            comp.Formula = "C" & CDbl(tmp(2)).ToString("N2") & "H" & CDbl(tmp(3)).ToString("N2")
            comp.IG_Enthalpy_of_Formation_25C = tmp(0)
            comp.IG_Entropy_of_Formation_25C = tmp(1)
            comp.IG_Gibbs_Energy_of_Formation_25C = tmp(0) - 298.15 * tmp(1)

            Dim hyp As New Utilities.Hypos.Methods.HYP

            comp.HVap_A = hyp.DHvb_Vetere(tc, pc, tb) / mw

            comp.Chao_Seader_Acentricity = af
            comp.Chao_Seader_Solubility_Parameter = ((comp.HVap_A * mw - 8.314 * tb) * 238.846 * prop.liq_dens_rackett(tb, tc, pc, af, mw) / mw / 1000000.0) ^ 0.5
            comp.Chao_Seader_Liquid_Molar_Volume = 1 / prop.liq_dens_rackett(tb, tc, pc, af, mw) * mw / 1000 * 1000000.0

            comp.ID = New Random(index).Next(1000000)

            'write the estimated values back so the caller can show them

            input.MW = mw
            input.NBP = tb
            input.SG = sg
            input.Tc = tc
            input.Pc = pc
            input.AF = af

            Return comp

        End Function

    End Class

End Namespace
