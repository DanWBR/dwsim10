Imports DWSIM.Interfaces
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary
Imports DWSIM.Thermodynamics.PetroleumCharacterization.Methods
Imports System.Linq

Namespace Utilities.PetroleumCharacterization.Methods

    ''' <summary>
    ''' Shared post-construction logic for petroleum pseudo-compounds.
    ''' Used by FormPCBulk (bulk properties) and DCCharacterizationWizard (distillation curve)
    ''' to avoid duplicating the Tc/Pc/omega/Vc/Z/Hf/Sf/HVap/Chao-Seader calculation block,
    ''' the EOS parameter fitting loop, and the result-grid population.
    ''' </summary>
    Partial Public Class PseudoBuilder

        ''' <summary>
        ''' Computes critical properties, acentric factor, Hf/Sf/HVap, formula and Chao-Seader parameters
        ''' on the supplied ConstantProperties. Assumes NBP, PF_SG, PF_MM, PF_vA and PF_vB are populated.
        ''' The compound name and ID are NOT assigned here (callers already do this).
        ''' </summary>
        Public Shared Sub FinalizeCompoundProperties(cprop As ConstantProperties,
                                                     tcMethod As String,
                                                     pcMethod As String,
                                                     afMethod As String)

            Dim gl As New Utilities.PetroleumCharacterization.Methods.GL
            Dim hyp As New Utilities.Hypos.Methods.HYP

            With cprop

                .OriginalDB = "DWSIM"
                .IsPF = 1
                .Normal_Boiling_Point = .NBP
                .Molar_Weight = .PF_MM

                'PNA auto-composition must occur before Tc/Pc if a PNA-weighted method is selected.
                If (tcMethod = "PNA-Weighted (Riazi)" OrElse pcMethod = "PNA-Weighted (Riazi)") Then
                    If Not .PF_n20.HasValue OrElse .PF_n20.Value <= 0 Then
                        .PF_n20 = PropertyMethods.RefractiveIndex_Riazi(.NBP, .PF_SG)
                    End If
                    If Not .PF_xP.HasValue AndAlso Not .PF_xN.HasValue AndAlso Not .PF_xA.HasValue Then
                        Dim pnaPre = PropertyMethods.PNA_Riazi(.NBP, .PF_SG, .PF_MM, .PF_n20)
                        .PF_xP = pnaPre(0) : .PF_xN = pnaPre(1) : .PF_xA = pnaPre(2)
                    End If
                End If

                Select Case tcMethod
                    Case "Riazi-Daubert (1985)"
                        .Critical_Temperature = PropertyMethods.Tc_RiaziDaubert(.NBP, .PF_SG)
                    Case "Riazi (2005)"
                        .Critical_Temperature = PropertyMethods.Tc_Riazi(.NBP, .PF_SG)
                    Case "Lee-Kesler (1976)"
                        .Critical_Temperature = PropertyMethods.Tc_LeeKesler(.NBP, .PF_SG)
                    Case "Farah (2006)"
                        .Critical_Temperature = PropertyMethods.Tc_Farah(.PF_vA, .PF_vB, .NBP, .PF_SG)
                    Case "Twu (1984)"
                        .Critical_Temperature = PropertyMethods.Tc_Twu(.NBP, .PF_SG)
                    Case "PNA-Weighted (Riazi)"
                        .Critical_Temperature = PropertyMethods.Tc_PNAWeighted(.NBP, .PF_SG, .PF_MM,
                                                                               .PF_xP.GetValueOrDefault, .PF_xN.GetValueOrDefault, .PF_xA.GetValueOrDefault)
                    Case Else
                        .Critical_Temperature = PropertyMethods.Tc_RiaziDaubert(.NBP, .PF_SG)
                End Select

                Select Case pcMethod
                    Case "Riazi-Daubert (1985)"
                        .Critical_Pressure = PropertyMethods.Pc_RiaziDaubert(.NBP, .PF_SG)
                    Case "Lee-Kesler (1976)"
                        .Critical_Pressure = PropertyMethods.Pc_LeeKesler(.NBP, .PF_SG)
                    Case "Farah (2006)"
                        .Critical_Pressure = PropertyMethods.Pc_Farah(.PF_vA, .PF_vB, .NBP, .PF_SG)
                    Case "Twu (1984)"
                        .Critical_Pressure = PropertyMethods.Pc_Twu(.NBP, .PF_SG)
                    Case "PNA-Weighted (Riazi)"
                        .Critical_Pressure = PropertyMethods.Pc_PNAWeighted(.NBP, .PF_SG, .PF_MM,
                                                                            .PF_xP.GetValueOrDefault, .PF_xN.GetValueOrDefault, .PF_xA.GetValueOrDefault)
                    Case Else
                        .Critical_Pressure = PropertyMethods.Pc_RiaziDaubert(.NBP, .PF_SG)
                End Select

                Select Case afMethod
                    Case "Lee-Kesler (1976)"
                        .Acentric_Factor = PropertyMethods.AcentricFactor_LeeKesler(.Critical_Temperature, .Critical_Pressure, .NBP)
                    Case "Korsten (2000)"
                        .Acentric_Factor = PropertyMethods.AcentricFactor_Korsten(.Critical_Temperature, .Critical_Pressure, .NBP)
                    Case Else
                        .Acentric_Factor = PropertyMethods.AcentricFactor_LeeKesler(.Critical_Temperature, .Critical_Pressure, .NBP)
                End Select

                .PF_Watson_K = (1.8 * .NBP.GetValueOrDefault) ^ (1 / 3) / .PF_SG.GetValueOrDefault

                'Refractive index and PNA composition (Riazi 2005 - MNL50 eq 3.77/3.78)
                'Only auto-compute if caller has not supplied measured values.
                If Not .PF_n20.HasValue OrElse .PF_n20.Value <= 0 Then
                    .PF_n20 = PropertyMethods.RefractiveIndex_Riazi(.NBP, .PF_SG)
                End If
                If Not .PF_Ri.HasValue OrElse .PF_Ri.Value <= 0 Then
                    .PF_Ri = PropertyMethods.RefractivityIntercept(.PF_n20, .PF_SG)
                End If
                If Not .PF_xP.HasValue AndAlso Not .PF_xN.HasValue AndAlso Not .PF_xA.HasValue Then
                    Dim pna = PropertyMethods.PNA_Riazi(.NBP, .PF_SG, .Molar_Weight, .PF_n20)
                    .PF_xP = pna(0)
                    .PF_xN = pna(1)
                    .PF_xA = pna(2)
                End If

                Dim tmp = gl.calculate_Hf_Sf(.PF_SG, .Molar_Weight, .NBP)
                .IG_Enthalpy_of_Formation_25C = tmp(0)
                .IG_Entropy_of_Formation_25C = tmp(1)
                .IG_Gibbs_Energy_of_Formation_25C = tmp(0) - 298.15 * tmp(1)
                .Formula = "C" & CDbl(tmp(2)).ToString("N2") & "H" & CDbl(tmp(3)).ToString("N2")

                .HVap_A = hyp.DHvb_Vetere(.Critical_Temperature, .Critical_Pressure, .Normal_Boiling_Point) / .Molar_Weight

                .Critical_Compressibility = PROPS.Zc1(.Acentric_Factor)
                .Critical_Volume = PROPS.Vc(.Critical_Temperature, .Critical_Pressure, .Acentric_Factor, .Critical_Compressibility)
                .Z_Rackett = PROPS.Zc1(.Acentric_Factor)
                If .Z_Rackett < 0 Then .Z_Rackett = 0.2

                .Chao_Seader_Acentricity = .Acentric_Factor
                .Chao_Seader_Solubility_Parameter = ((.HVap_A * .Molar_Weight - 8.314 * .Normal_Boiling_Point) * 238.846 * PROPS.liq_dens_rackett(.Normal_Boiling_Point, .Critical_Temperature, .Critical_Pressure, .Acentric_Factor, .Molar_Weight) / .Molar_Weight / 1000000.0) ^ 0.5
                .Chao_Seader_Liquid_Molar_Volume = 1 / PROPS.liq_dens_rackett(.Normal_Boiling_Point, .Critical_Temperature, .Critical_Pressure, .Acentric_Factor, .Molar_Weight) * .Molar_Weight / 1000 * 1000000.0

            End With

        End Sub

        ''' <summary>
        ''' Fits Acentric Factor (to NBP), Rackett Z_RA (to density), and PR/SRK volume-translation
        ''' coefficients for each compound in ccol. Caller decides whether AF and ZRA adjustments run
        ''' via the checkbox flags; PR/SRK VS fitting always runs.
        ''' </summary>
        Public Shared Sub FitPseudoParameters(ccol As Dictionary(Of String, Compound),
                                              flowsheet As IFlowsheet,
                                              adjustAF As Boolean,
                                              adjustZRA As Boolean)

            Dim dfit As New Utilities.PetroleumCharacterization.Methods.DensityFitting
            Dim prvsfit As New Utilities.PetroleumCharacterization.Methods.PRVSFitting
            Dim srkvsfit As New Utilities.PetroleumCharacterization.Methods.SRKVSFitting
            Dim nbpfit As New Utilities.PetroleumCharacterization.Methods.NBPFitting With {.Flowsheet = flowsheet}
            Dim tms As New Streams.MaterialStream("", "")
            Dim pp As PropertyPackages.PropertyPackage
            Dim fzra, fw, fprvs, fsrkvs As Double

            If flowsheet.PropertyPackages.Count > 0 Then
                pp = CType(flowsheet.PropertyPackages.Values.First(), PropertyPackages.PropertyPackage)
            Else
                pp = New PropertyPackages.PengRobinsonPropertyPackage()
            End If

            For Each c As Compound In ccol.Values
                tms.Phases(0).Compounds.Add(c.Name, c)
            Next

            Dim recalcVc As Boolean = False
            Dim i As Integer = 0

            For Each c As Compound In ccol.Values
                If adjustAF Then
                    If c.ConstantProperties.Acentric_Factor < 0 Then
                        c.ConstantProperties.Acentric_Factor = 0.5
                        recalcVc = True
                    End If
                    With nbpfit
                        ._pp = pp
                        ._ms = tms
                        ._idx = i
                        Try
                            fw = .MinimizeError()
                        Catch ex As Exception
                            flowsheet?.ShowMessage("Error fitting the acentric factor of " & c.Name & ": " & ex.Message, IFlowsheet.MessageType.GeneralError)
                        End Try
                    End With
                    With c.ConstantProperties
                        .Acentric_Factor *= fw
                        .Z_Rackett = PROPS.Zc1(.Acentric_Factor)
                        If .Z_Rackett < 0 Then
                            .Z_Rackett = 0.2
                            recalcVc = True
                        End If
                        .Critical_Compressibility = PROPS.Zc1(.Acentric_Factor)
                        .Critical_Volume = PROPS.Vc(.Critical_Temperature, .Critical_Pressure, .Acentric_Factor, .Critical_Compressibility)
                    End With
                End If
                If adjustZRA Then
                    With dfit
                        ._comp = c
                        Try
                            fzra = .MinimizeError()
                        Catch ex As Exception
                            flowsheet?.ShowMessage("Error fitting the Rackett parameter of " & c.Name & ": " & ex.Message, IFlowsheet.MessageType.GeneralError)
                        End Try
                    End With
                    With c.ConstantProperties
                        .Z_Rackett *= fzra
                        If .Critical_Compressibility < 0 Or recalcVc Then
                            .Critical_Compressibility = .Z_Rackett
                            .Critical_Volume = PROPS.Vc(.Critical_Temperature, .Critical_Pressure, .Acentric_Factor, .Critical_Compressibility)
                        End If
                    End With
                End If
                c.ConstantProperties.PR_Volume_Translation_Coefficient = 1
                prvsfit._comp = c
                fprvs = prvsfit.MinimizeError()
                With c.ConstantProperties
                    If Math.Abs(fprvs) < 99.0# Then .PR_Volume_Translation_Coefficient *= fprvs Else .PR_Volume_Translation_Coefficient = 0.0#
                End With
                c.ConstantProperties.SRK_Volume_Translation_Coefficient = 1
                srkvsfit._comp = c
                fsrkvs = srkvsfit.MinimizeError()
                With c.ConstantProperties
                    If Math.Abs(fsrkvs) < 99.0# Then .SRK_Volume_Translation_Coefficient *= fsrkvs Else .SRK_Volume_Translation_Coefficient = 0.0#
                End With
                recalcVc = False
                i += 1
            Next

        End Sub

    End Class

End Namespace
