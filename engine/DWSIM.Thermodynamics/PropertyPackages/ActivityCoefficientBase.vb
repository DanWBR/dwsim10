'    Activity Coefficient Property Package Base Class
'    Copyright 2008-2015 Daniel Wagner O. de Medeiros
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
Imports DWSIM.Interfaces.Enums

Namespace PropertyPackages

    ''' <summary>
    ''' Implemented by an activity-coefficient model that can supply its own closed-form derivatives.
    ''' </summary>
    ''' <remarks>
    ''' ActivityCoefficientPropertyPackage matches the built-in models (NRTL, UNIQUAC, Wilson, UNIFAC and
    ''' the modified UNIFACs) by type, which a model defined in another assembly cannot join. Implementing
    ''' this is how such a model opts into the analytical flash branch and the column's analytical Jacobian.
    ''' </remarks>
    Public Interface IAnalyticalGammaModel

        ''' <summary>
        ''' Returns Object(){gamma(), dln(gamma)/dT(), dln(gamma)/dn(,)} at T and composition Vx, or Nothing
        ''' when the model cannot answer and finite differences should be used instead. The mole-number
        ''' derivative is the one on a total-moles = 1 basis, i.e. with the composition constraint projected
        ''' out, matching the built-in models.
        ''' </summary>
        ''' <param name="args">Whatever the package's GetArguments returns for this model.</param>
        Function GAMMA_DERIVS(T As Double, Vx As Double(), args As Object) As Object

    End Interface

    <System.Serializable> Public MustInherit Partial Class ActivityCoefficientPropertyPackage

        Inherits PropertyPackage

        Public m_pr As New PropertyPackages.Auxiliary.PengRobinson
        Public m_lk As New PropertyPackages.Auxiliary.LeeKesler

        Public m_act As PropertyPackages.Auxiliary.IActivityCoefficientBase

#Region "Initialization"

        Public Sub New(ByVal comode As Boolean)

            MyBase.New(comode)

            EnthalpyEntropyCpCvCalculationMode = EnthalpyEntropyCpCvCalcMode.LeeKesler

            LiquidDensityCalculationMode_Subcritical = LiquidDensityCalcMode.COSTALD

            With PropertyMethodsInfo
                .Vapor_Fugacity = "Ideal / PR EOS"
                .Vapor_Enthalpy_Entropy_CpCv = "Ideal / Lee-Kesler / Excess / Experimental"
                .Vapor_Density = "Ideal / PR EOS"
                .Liquid_Fugacity = "Activity Coefficient + Poynting + Vapor Pressure / Henry's Constant"
                .Liquid_Enthalpy_Entropy_CpCv = "Ideal / Lee-Kesler / Excess / Experimental"
            End With

        End Sub

        Public Overrides Sub ConfigParameters()

        End Sub

        Public Overrides Function SupportsComponent(ByVal comp As Interfaces.ICompoundConstantProperties) As Boolean

            If Me.SupportedComponents.Contains(comp.ID) Then
                Return True
            ElseIf comp.IsHYPO = 1 Then
                Return True
            Else
                Return True
            End If

        End Function

#End Region

#Region "Functions to Calculate Isolated Properties"

        Public Overrides Function AUX_VAPDENS(ByVal T As Double, ByVal P As Double) As Double

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "AUX_VAPDENS", "Vapor Phase Density", "Vapor Phase Density Calculation Routine")

            IObj?.SetCurrent()

            Dim val As Double
            Dim Z As Double = 1.0#
            If VaporPhaseFugacityCalculationMode = VaporPhaseFugacityCalcMode.Ideal Then
                IObj?.Paragraphs.Add("Ideal Gas Vapor Phase assumption is enabled.")
                Z = 1.0#
            Else
                IObj?.Paragraphs.Add("Real Gas Vapor Phase assumption is enabled.")
                Z = m_pr.Z_PR(T, P, RET_VMOL(Phase.Vapor), RET_VKij, RET_VTC, RET_VPC, RET_VW, "V")
            End If

            IObj?.Paragraphs.Add("<h2>Intermediate Calculations</h2>")
            IObj?.Paragraphs.Add(String.Format("Vapor Phase Compressibility Factor: {0}", val))

            val = P / (Z * 8.314 * T) / 1000 * AUX_MMM(Phase.Vapor)

            IObj?.Paragraphs.Add("<h2>Results</h2>")

            IObj?.Paragraphs.Add(String.Format("Vapor Phase Density: {0} kg/m3", val))

            IObj?.Close()

            Return val

        End Function

        Public Overloads Overrides Sub DW_CalcCompPartialVolume(ByVal phase As Phase, ByVal T As Double, ByVal P As Double)

            Dim i As Integer

            For j As Integer = 1 To 7

                If j <> 2 Then

                    Dim Vx = RET_VMOL(RET_PHASECODE(j))
                    Dim n As Integer = Vx.Length - 1

                    Dim constprop = DW_GetConstantProperties()

                    Dim ativ(n), poy1(n), poy2(n), vex(n) As Double

                    ativ = m_act.CalcActivityCoefficients(T, Vx, Me.GetArguments())

                    Dim P2 As Double = P + 1

                    Dim Psati, vli As Double
                    For i = 0 To n
                        vli = 1 / AUX_LIQDENSi(constprop(i), T) * constprop(i).Molar_Weight
                        If Double.IsNaN(vli) Then
                            vli = 1 / AUX_LIQDENSi(constprop(i), constprop(i).Normal_Boiling_Point) * constprop(i).Molar_Weight
                        End If
                        Psati = AUX_PVAPi(i, T)
                        poy1(i) = Math.Exp(vli * Abs(P - Psati) / (8314.47 * T))
                        poy2(i) = Math.Exp(vli * Abs(P2 - Psati) / (8314.47 * T))
                        vex(i) = (Log(poy2(i)) - Log(poy1(i))) * 8.314 * T * 1000 'm3/kmol
                    Next

                    i = 0
                    For Each subst As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(j).Compounds.Values
                        subst.PartialVolume = vex(i)
                        i += 1
                    Next

                Else

                    If VaporPhaseFugacityCalculationMode = VaporPhaseFugacityCalcMode.Ideal Then
                        Dim vapdens = AUX_VAPDENS(T, P)
                        For Each subst As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(2).Compounds.Values
                            subst.PartialVolume = subst.ConstantProperties.Molar_Weight / vapdens
                        Next
                    Else
                        Dim partvol As New Object
                        partvol = Me.m_pr.CalcPartialVolume(T, P, RET_VMOL(2), RET_VKij(), RET_VTC(), RET_VPC(), RET_VW(), RET_VTB(), "V", 0.0001)
                        i = 0
                        For Each subst As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(2).Compounds.Values
                            subst.PartialVolume = partvol(i)
                            i += 1
                        Next
                    End If

                End If

            Next

        End Sub

        Public Function RET_KIJ(ByVal id1 As String, ByVal id2 As String) As Double
            If Me.m_pr.InteractionParameters.ContainsKey(id1) Then
                If Me.m_pr.InteractionParameters(id1).ContainsKey(id2) Then
                    Return m_pr.InteractionParameters(id1)(id2).kij
                Else
                    If Me.m_pr.InteractionParameters.ContainsKey(id2) Then
                        If Me.m_pr.InteractionParameters(id2).ContainsKey(id1) Then
                            Return m_pr.InteractionParameters(id2)(id1).kij
                        Else
                            Return 0
                        End If
                    Else
                        Return 0
                    End If
                End If
            Else
                Return 0
            End If
        End Function

        Public Overrides Function RET_VKij() As Double(,)

            Dim val(Me.CurrentMaterialStream.Phases(0).Compounds.Count - 1, Me.CurrentMaterialStream.Phases(0).Compounds.Count - 1) As Double
            Dim i As Integer = 0
            Dim l As Integer = 0

            i = 0
            For Each cp As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(0).Compounds.Values
                l = 0
                For Each cp2 As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(0).Compounds.Values
                    val(i, l) = Me.RET_KIJ(cp.Name, cp2.Name)
                    l = l + 1
                Next
                i = i + 1
            Next

            Return val

        End Function

        Public Overrides Function DW_CalcCp_ISOL(ByVal Phase1 As PropertyPackages.Phase, ByVal T As Double, ByVal P As Double) As Double
            Select Case Phase1
                Case Phase.Liquid
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(1)
                Case Phase.Aqueous
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(1)
                Case Phase.Liquid1
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(1)
                Case Phase.Liquid2
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(1)
                Case Phase.Liquid3
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(1)
                Case Phase.Vapor
                    Return Auxiliary.PROPS.CpCvR("V", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(1)
            End Select
            Return 0.0#
        End Function

        Public Overrides Function DW_CalcCv_ISOL(ByVal Phase1 As Phase, ByVal T As Double, ByVal P As Double) As Double
            Select Case Phase1
                Case Phase.Liquid
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(2)
                Case Phase.Aqueous
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(2)
                Case Phase.Liquid1
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(2)
                Case Phase.Liquid2
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(2)
                Case Phase.Liquid3
                    Return Auxiliary.PROPS.CpCvR("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(2)
                Case Phase.Vapor
                    Return Auxiliary.PROPS.CpCvR("V", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())(2)
            End Select
            Return 0.0#
        End Function

        Public Overrides Function DW_CalcEnergyFlowMistura_ISOL(ByVal T As Double, ByVal P As Double) As Double

            Dim HM, HV, HL As Double

            HL = Me.DW_CalcEnthalpy(RET_VMOL(Phase.Liquid), T, P, State.Liquid)
            HV = Me.DW_CalcEnthalpy(RET_VMOL(Phase.Vapor), T, P, State.Vapor)
            HM = Me.CurrentMaterialStream.Phases(1).Properties.massfraction.GetValueOrDefault * HL + Me.CurrentMaterialStream.Phases(2).Properties.massfraction.GetValueOrDefault * HV

            Dim ent_massica = HM
            Dim flow = Me.CurrentMaterialStream.Phases(0).Properties.massflow
            Return ent_massica * flow

        End Function

        Public Overrides Function DW_CalcK_ISOL(ByVal Phase1 As PropertyPackages.Phase, ByVal T As Double, ByVal P As Double) As Double
            If Phase1 = Phase.Liquid Then
                Return Me.AUX_CONDTL(T)
            ElseIf Phase1 = Phase.Vapor Then
                Return Me.AUX_CONDTG(T, P)
            Else
                Return 0.0#
            End If
        End Function

        Public Overrides Function DW_CalcPVAP_ISOL(ByVal T As Double) As Double
            Return Auxiliary.PROPS.Pvp_leekesler(T, Me.RET_VTC(Phase.Liquid), Me.RET_VPC(Phase.Liquid), Me.RET_VW(Phase.Liquid))
        End Function

        Public Overrides Function DW_CalcTensaoSuperficial_ISOL(ByVal Phase1 As PropertyPackages.Phase, ByVal T As Double, ByVal P As Double) As Double
            Return Me.AUX_SURFTM(T)
        End Function

        Public Overrides Function DW_CalcMassaEspecifica_ISOL(ByVal Phase1 As PropertyPackages.Phase, ByVal T As Double, ByVal P As Double, Optional ByVal pvp As Double = 0) As Double
            If Phase1 = Phase.Liquid Then
                Return Me.AUX_LIQDENS(T)
            ElseIf Phase1 = Phase.Vapor Then
                Return Me.AUX_VAPDENS(T, P)
            ElseIf Phase1 = Phase.Mixture Then
                Return Me.CurrentMaterialStream.Phases(1).Properties.volumetric_flow.GetValueOrDefault * Me.AUX_LIQDENS(T) / Me.CurrentMaterialStream.Phases(0).Properties.volumetric_flow.GetValueOrDefault + Me.CurrentMaterialStream.Phases(2).Properties.volumetric_flow.GetValueOrDefault * Me.AUX_VAPDENS(T, P) / Me.CurrentMaterialStream.Phases(0).Properties.volumetric_flow.GetValueOrDefault
            End If
        End Function

        Public Overrides Function DW_CalcMM_ISOL(ByVal Phase1 As PropertyPackages.Phase, ByVal T As Double, ByVal P As Double) As Double
            Return Me.AUX_MMM(Phase1)
        End Function

        Public Overrides Function DW_CalcViscosidadeDinamica_ISOL(ByVal Phase1 As PropertyPackages.Phase, ByVal T As Double, ByVal P As Double) As Double
            If Phase1 = Phase.Liquid Then
                Return Me.AUX_LIQVISCm(T, P)
            ElseIf Phase1 = Phase.Vapor Then
                Return Me.AUX_VAPVISCm(T, Me.AUX_VAPDENS(T, P), Me.AUX_MMM(Phase.Vapor))
            Else
                Return 0.0#
            End If
        End Function

#End Region

#Region "Main Property Routines"

        Public Overrides Sub AddDefaultCompounds(compnames() As String)

            If TypeOf Me Is SourWaterPropertyPackage Then
                MyBase.AddDefaultCompounds(compnames)
            Else
                MyBase.AddDefaultCompounds(New String() {"Water", "Ethanol"})
            End If

        End Sub

        Public Overridable Function GetArguments() As Object

            If TypeOf Me Is NRTLPropertyPackage Then
                Return DirectCast(Me, NRTLPropertyPackage).RET_VNAMES
            ElseIf TypeOf Me Is SourWaterPropertyPackage Then
                Return DirectCast(Me, SourWaterPropertyPackage).RET_VNAMES
            ElseIf TypeOf Me Is UNIQUACPropertyPackage Then
                Return New Object() {DirectCast(Me, UNIQUACPropertyPackage).RET_VNAMES, DirectCast(Me, UNIQUACPropertyPackage).RET_VQ, DirectCast(Me, UNIQUACPropertyPackage).RET_VR}
            ElseIf TypeOf Me Is MODFACPropertyPackage Then
                Return New Object() {DirectCast(Me, MODFACPropertyPackage).RET_VQ, DirectCast(Me, MODFACPropertyPackage).RET_VR, DirectCast(Me, MODFACPropertyPackage).RET_VEKI}
            ElseIf TypeOf Me Is NISTMFACPropertyPackage Then
                Return New Object() {DirectCast(Me, NISTMFACPropertyPackage).RET_VQ, DirectCast(Me, NISTMFACPropertyPackage).RET_VR, DirectCast(Me, NISTMFACPropertyPackage).RET_VEKI}
            ElseIf TypeOf Me Is UNIFACPropertyPackage Then
                Return New Object() {DirectCast(Me, UNIFACPropertyPackage).RET_VQ, DirectCast(Me, UNIFACPropertyPackage).RET_VR, DirectCast(Me, UNIFACPropertyPackage).RET_VEKI}
            ElseIf TypeOf Me Is UNIFACLLPropertyPackage Then
                Return New Object() {DirectCast(Me, UNIFACLLPropertyPackage).RET_VQ, DirectCast(Me, UNIFACLLPropertyPackage).RET_VR, DirectCast(Me, UNIFACLLPropertyPackage).RET_VEKI}
            Else
                Return Nothing
            End If

        End Function

#Region "   Analytical Derivatives"

        ' Returns Object(){gamma(), dln(gamma)/dT(), dln(gamma)/dn(,)} for the underlying activity model
        ' when it exposes a closed-form GAMMA_DERIVS (NRTL, UNIQUAC, UNIFAC, MODFAC); Nothing otherwise.
        Private Function TryGetGammaDerivs(T As Double, Vx As Double()) As Object
            Return TryGetGammaDerivs(T, Vx, GetArguments())
        End Function

        ' As above, but with the model arguments supplied by the caller. GetArguments rebuilds them from the
        ' material stream on every call - for the group-contribution models that means re-reading each
        ' compound's groups and allocating a dictionary per compound - yet they depend only on the compound
        ' list, not on T or composition. A caller sweeping composition resolves them once and passes them in.
        Private Function TryGetGammaDerivs(T As Double, Vx As Double(), args As Object) As Object
            ' An activity model outside this assembly - a Plus package, say - supplies its closed form by
            ' implementing IAnalyticalGammaModel; the built-in ones are matched by type below.
            If TypeOf m_act Is IAnalyticalGammaModel Then
                Return DirectCast(m_act, IAnalyticalGammaModel).GAMMA_DERIVS(T, Vx, args)
            End If
            If TypeOf m_act Is Auxiliary.NRTL Then
                Return DirectCast(m_act, Auxiliary.NRTL).GAMMA_DERIVS(T, Vx, DirectCast(args, String()))
            ElseIf TypeOf m_act Is Auxiliary.UNIQUAC Then
                Dim ar = DirectCast(args, Object())
                Return DirectCast(m_act, Auxiliary.UNIQUAC).GAMMA_DERIVS(T, Vx, DirectCast(ar(0), String()), DirectCast(ar(1), Double()), DirectCast(ar(2), Double()))
            ElseIf TypeOf m_act Is Auxiliary.Unifac Then
                Dim ar = DirectCast(args, Object())
                Return DirectCast(m_act, Auxiliary.Unifac).GAMMA_DERIVS(T, Vx, DirectCast(ar(0), Double()), DirectCast(ar(1), Double()), DirectCast(ar(2), List(Of Dictionary(Of Integer, Double))))
            ElseIf TypeOf m_act Is Auxiliary.Modfac Then
                Dim ar = DirectCast(args, Object())
                Return DirectCast(m_act, Auxiliary.Modfac).GAMMA_DERIVS(T, Vx, DirectCast(ar(0), Double()), DirectCast(ar(1), Double()), DirectCast(ar(2), List(Of Dictionary(Of Integer, Double))))
            ElseIf TypeOf m_act Is Auxiliary.NISTMFAC Then
                Dim ar = DirectCast(args, Object())
                Return DirectCast(m_act, Auxiliary.NISTMFAC).GAMMA_DERIVS(T, Vx, DirectCast(ar(0), Double()), DirectCast(ar(1), Double()), DirectCast(ar(2), List(Of Dictionary(Of Integer, Double))))
            ElseIf TypeOf m_act Is WilsonModel Then
                Dim ar = DirectCast(args, Object())
                Return DirectCast(m_act, WilsonModel).GAMMA_DERIVS(T, Vx, DirectCast(ar(0), String()), DirectCast(ar(1), Double()))
            End If
            Return Nothing
        End Function

        Private Function AllSubcritical(T As Double) As Boolean
            Dim Tc = RET_VTC()
            For i As Integer = 0 To Tc.Length - 1
                If T / Tc(i) >= 0.98 Then Return False
            Next
            Return True
        End Function

        ' Composition (mole-number) derivative of ln(phi) for the liquid activity model. For all-subcritical
        ' mixtures lnphi_i = ln(gamma_i*Psat_i/P)+ln(Poy_i) and Psat/P/Poy are composition-independent, so
        ' d(lnphi_i)/dn_j = d(ln gamma_i)/dn_j exactly. Falls back to finite differences otherwise.
        Public Overrides Function DW_CalcdLnFugCoeffdn(ByVal Vx As Double(), ByVal T As Double, ByVal P As Double, st As State) As Double(,)
            If AnalyticalDerivativesDisabled OrElse st <> State.Liquid OrElse Not AllSubcritical(T) Then Return MyBase.DW_CalcdLnFugCoeffdn(Vx, T, P, st)
            Dim d = TryGetGammaDerivs(T, Vx)
            If d Is Nothing Then Return MyBase.DW_CalcdLnFugCoeffdn(Vx, T, P, st)
            Return DirectCast(DirectCast(d, Object())(2), Double(,))
        End Function

        ' d(K_i)/dn_j w.r.t. liquid mole numbers = K_i * d(ln gamma_i)/dn_j (vapour fugacity is independent
        ' of the liquid composition). Vapour-side or non-subcritical requests fall back to finite differences.
        Public Overrides Function DW_CalcdKdComposition(ByVal Vx As Double(), ByVal Vy As Double(), ByVal T As Double, ByVal P As Double, ByVal withRespectTo As State, Optional ByVal type As String = "LV") As Double(,)
            If AnalyticalDerivativesDisabled OrElse type <> "LV" OrElse withRespectTo <> State.Liquid OrElse Not AllSubcritical(T) Then Return MyBase.DW_CalcdKdComposition(Vx, Vy, T, P, withRespectTo, type)
            Dim d = TryGetGammaDerivs(T, Vx)
            If d Is Nothing Then Return MyBase.DW_CalcdKdComposition(Vx, Vy, T, P, withRespectTo, type)
            Dim dlngdn = DirectCast(DirectCast(d, Object())(2), Double(,))
            Dim K = DW_CalcKvalue(Vx, Vy, T, P, type)
            Dim n As Integer = Vx.Length - 1
            Dim deriv(n, n) As Double
            For i As Integer = 0 To n
                For j As Integer = 0 To n
                    deriv(i, j) = K(i) * dlngdn(i, j)
                Next
            Next
            Return deriv
        End Function

        ' True only when the underlying model exposes a closed-form GAMMA_DERIVS (NRTL, UNIQUAC, Wilson,
        ' UNIFAC, Modified UNIFAC Dortmund/NIST and subclasses). Other activity models (sour water, ...) keep
        ' the finite-difference default so enabling the analytical flash/Jacobian branch is always safe.
        Private ReadOnly Property HasAnalyticalGammaModel As Boolean
            Get
                Return TypeOf m_act Is IAnalyticalGammaModel OrElse
                       TypeOf m_act Is Auxiliary.NRTL OrElse TypeOf m_act Is Auxiliary.UNIQUAC OrElse
                       TypeOf m_act Is Auxiliary.Unifac OrElse TypeOf m_act Is Auxiliary.Modfac OrElse
                       TypeOf m_act Is Auxiliary.NISTMFAC OrElse TypeOf m_act Is WilsonModel
            End Get
        End Property

        ''' <summary>
        ''' Advertises analytical derivatives (activating the NestedLoops PV/TV flash branch and the
        ''' column-solver analytical Jacobian) only for models with a closed-form GAMMA_DERIVS, and only
        ''' when the runtime kill-switch is not set.
        ''' </summary>
        Public Overrides ReadOnly Property ImplementsAnalyticalDerivatives As Boolean
            Get
                Return Not AnalyticalDerivativesDisabled AndAlso HasAnalyticalGammaModel
            End Get
        End Property

        ' d(ln Psat_i)/dT by tight central differences of the (smooth, single-variable) vapour-pressure
        ' correlation. Psat can come from any of a dozen per-compound correlations, so a central difference
        ' of the scalar function is effectively exact and far cheaper and smoother than differencing the
        ' full K-value. Building block for the analytical liquid-phase d(ln phi)/dT below.
        Private Function dLnPsatdT(T As Double, n As Integer) As Double()
            Dim d(n) As Double
            Const h As Double = 0.01
            For i As Integer = 0 To n
                d(i) = (Math.Log(AUX_PVAPi(i, T + h)) - Math.Log(AUX_PVAPi(i, T - h))) / (2.0 * h)
            Next
            Return d
        End Function

        ''' <summary>
        ''' Temperature derivative of ln(phi) for the activity-model packages. Liquid: lnphi_i =
        ''' ln(gamma_i) + ln(Psat_i/P) [+ ln(Poy_i)], so d(ln phi_i)/dT = d(ln gamma_i)/dT + d(ln Psat_i)/dT.
        ''' Vapour: zero in ideal mode, the PR-EOS closed form otherwise. Falls back to finite differences
        ''' for near-critical mixtures, the Poynting-correction-on case, or models without GAMMA_DERIVS.
        ''' </summary>
        Public Overrides Function DW_CalcdLnFugCoeffdT(ByVal Vx As Double(), ByVal T As Double, ByVal P As Double, st As State) As Double()
            If AnalyticalDerivativesDisabled OrElse Not AllSubcritical(T) Then Return MyBase.DW_CalcdLnFugCoeffdT(Vx, T, P, st)
            Dim n As Integer = Vx.Length - 1
            If st = State.Liquid Then
                ' With the Poynting correction on, d(ln Poy)/dT is non-negligible only at high pressure and
                ' has no closed form here; defer to the (correct) finite-difference base in that case.
                If LiquidFugacity_UsePoyntingCorrectionFactor Then Return MyBase.DW_CalcdLnFugCoeffdT(Vx, T, P, st)
                Dim d = TryGetGammaDerivs(T, Vx)
                If d Is Nothing Then Return MyBase.DW_CalcdLnFugCoeffdT(Vx, T, P, st)
                Dim dlngdT = DirectCast(DirectCast(d, Object())(1), Double())
                Dim dlnpsat = dLnPsatdT(T, n)
                Dim deriv(n) As Double
                For i As Integer = 0 To n
                    deriv(i) = dlngdT(i) + dlnpsat(i)
                Next
                Return deriv
            Else
                If VaporPhaseFugacityCalculationMode = VaporPhaseFugacityCalcMode.Ideal Then
                    Dim z(n) As Double
                    Return z
                Else
                    Dim res = ThermoPlugs.CubicEOSDerivatives.Calc(ThermoPlugs.CubicEOSDerivatives.EOS_PR, T, P, Vx, RET_VKij, RET_VTC, RET_VPC, RET_VW, 1)
                    Return DirectCast(res(1), Double())
                End If
            End If
        End Function

        ''' <summary>
        ''' Second composition derivative of the molar Gibbs energy of mixing of a BINARY,
        ''' D2 = d2(g_mix/RT)/dx1^2, evaluated analytically from the model's closed-form activity
        ''' derivatives. With g_mix/RT = sum_i x_i ln(x_i gamma_i) and Gibbs-Duhem,
        ''' D2 = 1/(x1 x2) + d ln(gamma_1)/dx1 - d ln(gamma_2)/dx1. The composition-direction derivative is
        ''' taken along dn = (+1,-1), which at one total mole is exactly dx1 = +1, so
        ''' d ln(gamma_i)/dx1 = d ln(gamma_i)/dn_1 - d ln(gamma_i)/dn_2. Returns NaN when the underlying model
        ''' exposes no closed-form derivatives.
        ''' </summary>
        Public Function GibbsMixingD2(ByVal T As Double, ByVal x1 As Double) As Double
            If x1 <= 0.0 OrElse x1 >= 1.0 Then Return Double.NaN
            Dim Vx = New Double() {x1, 1.0 - x1}
            Dim d = TryGetGammaDerivs(T, Vx)
            If d Is Nothing Then Return Double.NaN
            Dim dn = DirectCast(DirectCast(d, Object())(2), Double(,))
            Dim dg1 As Double = dn(0, 0) - dn(0, 1)
            Dim dg2 As Double = dn(1, 0) - dn(1, 1)
            Return 1.0 / (x1 * (1.0 - x1)) + dg1 - dg2
        End Function

        ''' <summary>
        ''' Returns a function (T, x1) -> D2 for a BINARY, with the activity-model arguments resolved once
        ''' up front instead of on every call as GibbsMixingD2 does. The arguments depend only on the
        ''' compound list, so rebuilding them per point is pure overhead when sweeping composition, and for
        ''' the group-contribution models that overhead dominates the D2 evaluation itself.
        ''' Use this instead of GibbsMixingD2 wherever D2 is evaluated more than a handful of times at a
        ''' fixed compound list; the returned function is only valid while that compound list stands.
        ''' </summary>
        Public Function GetGibbsMixingD2Evaluator() As Func(Of Double, Double, Double)
            Dim args = GetArguments()
            Return Function(T As Double, x1 As Double) As Double
                       If x1 <= 0.0 OrElse x1 >= 1.0 Then Return Double.NaN
                       Dim Vx = New Double() {x1, 1.0 - x1}
                       Dim d = TryGetGammaDerivs(T, Vx, args)
                       If d Is Nothing Then Return Double.NaN
                       Dim dn = DirectCast(DirectCast(d, Object())(2), Double(,))
                       Return 1.0 / (x1 * (1.0 - x1)) + (dn(0, 0) - dn(0, 1)) - (dn(1, 0) - dn(1, 1))
                   End Function
        End Function

        ' Minimum of D2 over composition at a given temperature (coarse scan + golden-section refine).
        ' D2 diverges to +infinity at both ends, so the minimum is always interior.
        ' Takes the D2 evaluator rather than calling GibbsMixingD2, so the model arguments are resolved once
        ' for the whole consolute search instead of on each of the hundreds of points it visits.
        Private Function MinGibbsMixingD2(ByVal d2 As Func(Of Double, Double, Double), ByVal T As Double,
                                          ByRef xmin As Double) As Double
            Dim best As Double = Double.MaxValue, bx As Double = 0.5
            Const m As Integer = 199
            For k As Integer = 1 To m
                Dim x As Double = k / (m + 1.0)
                Dim v As Double = d2(T, x)
                If Not Double.IsNaN(v) AndAlso v < best Then best = v : bx = x
            Next
            If best = Double.MaxValue Then xmin = 0.5 : Return Double.NaN
            Dim h As Double = 1.0 / (m + 1.0)
            Dim a As Double = Math.Max(0.0002, bx - h), b As Double = Math.Min(0.9998, bx + h)
            Dim gr As Double = (Math.Sqrt(5.0) - 1.0) / 2.0
            Dim c As Double = b - gr * (b - a), e As Double = a + gr * (b - a)
            ' Carry fc and fe across iterations. Golden section is built so that one of the next pair of
            ' interior points always coincides with one of the current pair, which is the whole point of the
            ' 0.618 ratio: re-evaluating both, as this did, doubles the cost for identical brackets.
            Dim fc As Double = d2(T, c), fe As Double = d2(T, e)
            For it As Integer = 1 To 80
                If fc < fe Then
                    b = e : e = c : fe = fc
                    c = b - gr * (b - a)
                    fc = d2(T, c)
                Else
                    a = c : c = e : fc = fe
                    e = a + gr * (b - a)
                    fe = d2(T, e)
                End If
                If Math.Abs(b - a) < 0.0000001 Then Exit For
            Next
            xmin = 0.5 * (a + b)
            Return d2(T, xmin)
        End Function

        ''' <summary>
        ''' Liquid-liquid consolute (critical solution) points of a BINARY mixture: the temperatures at which
        ''' a miscibility gap closes or opens (UCST / LCST).
        ''' <para>
        ''' Note this is a genuinely different object from a vapour-liquid critical point, which a
        ''' gamma-phi model cannot represent at all: the liquid activity model carries no PVT relation, so
        ''' there is no single thermodynamic surface spanning liquid and vapour whose Hessian could go
        ''' singular. The consolute point, by contrast, lives entirely in the Gibbs energy of mixing at fixed
        ''' temperature and pressure, which the activity model does provide.
        ''' </para>
        ''' <para>
        ''' The mixture is diffusionally stable where D2 > 0 and splits where D2 &lt; 0, so the spinodal is
        ''' D2 = 0. The consolute point is where the two spinodal roots merge, i.e. where D2 has a double root
        ''' in composition (D2 = 0 and dD2/dx1 = 0 together). This is solved in the equivalent but far more
        ''' robust form min_x D2(T,x) = 0, which both an upper and a lower critical solution temperature
        ''' satisfy; the temperature range is scanned for sign changes of that minimum and each is bracketed.
        ''' </para>
        ''' </summary>
        ''' <returns>
        ''' List of {T (K), x1} consolute points found. Empty if the binary is miscible over the whole scanned
        ''' range (no gap), if the mixture is not binary, or if the model exposes no closed-form derivatives.
        ''' </returns>
        ''' <summary>
        ''' Spinodal roots of a BINARY at a temperature: the two compositions where D2 = 0, bounding the
        ''' mechanically unstable window. Returns {xs1, xs2}, or Nothing when the mixture is miscible at
        ''' this temperature (D2 > 0 everywhere, so g_mix is strictly convex), when it is not binary, or when
        ''' the model exposes no closed-form derivatives.
        ''' </summary>
        Public Function DW_CalculateSpinodal(ByVal T As Double) As Double()
            If RET_VNAMES().Length <> 2 Then Return Nothing
            Return SpinodalRoots(GetGibbsMixingD2Evaluator(), T)
        End Function

        ' Shared by the public entry point above and the binodal trace, which resolves its own evaluator once
        ' for a whole temperature sweep rather than once per temperature.
        Private Function SpinodalRoots(ByVal d2 As Func(Of Double, Double, Double), ByVal T As Double) As Double()
            Dim xmin As Double = 0.5
            Dim d2min As Double = MinGibbsMixingD2(d2, T, xmin)
            If Double.IsNaN(d2min) OrElse d2min > 0.0 Then Return Nothing
            ' D2 is negative at the minimiser and diverges to +inf at both ends, so each side brackets a root.
            Dim xs1 As Double = BisectSpinodal(d2, T, 0.000001, xmin)
            Dim xs2 As Double = BisectSpinodal(d2, T, xmin, 0.999999)
            If xs2 <= xs1 Then Return Nothing
            Return New Double() {xs1, xs2}
        End Function

        Private Function BisectSpinodal(ByVal d2 As Func(Of Double, Double, Double), ByVal T As Double,
                                        ByVal a As Double, ByVal b As Double) As Double
            Dim fa As Double = d2(T, a)
            For it As Integer = 1 To 60
                Dim m As Double = 0.5 * (a + b)
                Dim fm As Double = d2(T, m)
                If Double.IsNaN(fm) Then Exit For
                If fa * fm <= 0.0 Then b = m Else a = m : fa = fm
                If b - a < 0.000001 Then Exit For
            Next
            Return 0.5 * (a + b)
        End Function

        ' Returns {ln gamma1, ln gamma2, dln(gamma1)/dx1, dln(gamma2)/dx1} at composition x1, or Nothing.
        ' The composition derivative along the binary line is dn(i,0) - dn(i,1), the same Gibbs-Duhem
        ' consistent combination of the mole-number projection that gives D2.
        Private Function BinaryGammaAndSlopes(ByVal T As Double, ByVal x1 As Double, ByVal args As Object) As Double()
            If x1 <= 0.0 OrElse x1 >= 1.0 Then Return Nothing
            Dim d = TryGetGammaDerivs(T, New Double() {x1, 1.0 - x1}, args)
            If d Is Nothing Then Return Nothing
            Dim g = DirectCast(DirectCast(d, Object())(0), Double())
            Dim dn = DirectCast(DirectCast(d, Object())(2), Double(,))
            If g(0) <= 0.0 OrElse g(1) <= 0.0 Then Return Nothing
            Return New Double() {Math.Log(g(0)), Math.Log(g(1)), dn(0, 0) - dn(0, 1), dn(1, 0) - dn(1, 1)}
        End Function

        ''' <summary>
        ''' Liquid-liquid binodal (coexistence curve) of a BINARY at a temperature: the two compositions that
        ''' share a common tangent to g_mix, equivalently that give both compounds equal activities.
        ''' <para>
        ''' This does NOT depend on any feed composition - the binodal is a property of the temperature and
        ''' the model alone. Tracing it by flashing one fixed feed and sweeping temperature, which is what a
        ''' phase diagram is tempted to do, silently truncates the dome: as temperature rises that feed leaves
        ''' the binodal before the consolute point is reached, the flash reports a single phase, and the curve
        ''' stops short of its own apex.
        ''' </para>
        ''' <para>
        ''' Solved as a 2x2 Newton on the equal-activity condition using the analytical composition
        ''' derivatives, seeded outside the spinodal roots - the binodal always lies outside them - offset by
        ''' a fraction of the window width, which mean-field scaling puts close to the answer at any width.
        ''' </para>
        ''' </summary>
        ''' <returns>{x1 in liquid 1, x1 in liquid 2}, the smaller x1 first; Nothing when the binary is
        ''' miscible at this temperature or the model exposes no closed-form derivatives.</returns>
        Public Function DW_CalculateLLEBinodal(ByVal T As Double) As Double()
            If RET_VNAMES().Length <> 2 Then Return Nothing
            Dim sp = SpinodalRoots(GetGibbsMixingD2Evaluator(), T)
            If sp Is Nothing Then Return Nothing
            Return BinodalFromSpinodal(T, sp, GetArguments())
        End Function

        Private Function BinodalFromSpinodal(ByVal T As Double, ByVal sp As Double(), ByVal args As Object) As Double()

            Dim wid As Double = sp(1) - sp(0)
            Dim a As Double = Math.Max(sp(0) - 0.4 * wid, 0.5 * sp(0))
            Dim b As Double = Math.Min(sp(1) + 0.4 * wid, sp(1) + 0.5 * (1.0 - sp(1)))

            For it As Integer = 1 To 60

                Dim ga = BinaryGammaAndSlopes(T, a, args)
                Dim gb = BinaryGammaAndSlopes(T, b, args)
                If ga Is Nothing OrElse gb Is Nothing Then Return Nothing

                Dim F1 As Double = Math.Log(a) + ga(0) - Math.Log(b) - gb(0)
                Dim F2 As Double = Math.Log(1.0 - a) + ga(1) - Math.Log(1.0 - b) - gb(1)
                If Double.IsNaN(F1) OrElse Double.IsNaN(F2) Then Return Nothing
                If Math.Abs(F1) + Math.Abs(F2) < 0.000000001 Then Exit For

                Dim J11 As Double = 1.0 / a + ga(2)
                Dim J12 As Double = -(1.0 / b + gb(2))
                Dim J21 As Double = -1.0 / (1.0 - a) + ga(3)
                Dim J22 As Double = 1.0 / (1.0 - b) - gb(3)

                Dim det As Double = J11 * J22 - J12 * J21
                If Math.Abs(det) < 0.000000000001 Then Return Nothing

                Dim da As Double = (-F1 * J22 + F2 * J12) / det
                Dim db As Double = (-J11 * F2 + J21 * F1) / det

                ' Damp back into the feasible interval. The trivial solution a = b satisfies these equations
                ' exactly, so a step that would cross the phases over is refused rather than followed.
                Dim taken As Boolean = False
                Dim lam As Double = 1.0
                For ls As Integer = 1 To 12
                    Dim at As Double = a + lam * da, bt As Double = b + lam * db
                    If at > 0.0 AndAlso bt < 1.0 AndAlso at < bt - 0.000001 Then
                        a = at : b = bt
                        taken = True
                        Exit For
                    End If
                    lam *= 0.5
                Next
                If Not taken Then Return Nothing

            Next

            ' collapsed onto the trivial solution: no gap at this temperature
            If b - a < 0.000001 Then Return Nothing
            Return New Double() {a, b}

        End Function

        ''' <summary>
        ''' Traces the whole liquid-liquid dome of a BINARY over a temperature range in one pass: for each
        ''' temperature, the spinodal roots and the binodal that sits outside them.
        ''' <para>
        ''' Prefer this to calling DW_CalculateSpinodal and DW_CalculateLLEBinodal per temperature. It
        ''' resolves the model arguments once for the entire sweep rather than per call, and the binodal
        ''' reuses the spinodal roots instead of recomputing them - together roughly a factor of two on any
        ''' model, and much more on the group-contribution ones, where resolving the arguments costs more
        ''' than the derivative itself.
        ''' </para>
        ''' </summary>
        ''' <returns>One {T, xs1, xs2, xb1, xb2} per temperature at which a gap exists: the two spinodal
        ''' compositions then the two binodal ones. Temperatures with no gap are omitted, so an empty list
        ''' means the binary is miscible over the whole range.</returns>
        Public Function DW_CalculateLLEDiagram(ByVal Tmin As Double, ByVal Tmax As Double,
                                               Optional ByVal nsub As Integer = 40) As List(Of Double())

            Dim res As New List(Of Double())
            If RET_VNAMES().Length <> 2 Then Return res
            If nsub < 1 OrElse Tmax <= Tmin Then Return res

            Dim d2 = GetGibbsMixingD2Evaluator()
            Dim args = GetArguments()

            For k As Integer = 0 To nsub
                Dim T As Double = Tmin + (Tmax - Tmin) / nsub * k
                Dim sp = SpinodalRoots(d2, T)
                If sp Is Nothing Then Continue For
                Dim bi = BinodalFromSpinodal(T, sp, args)
                If bi Is Nothing Then Continue For
                res.Add(New Double() {T, sp(0), sp(1), bi(0), bi(1)})
            Next

            Return res

        End Function

        ''' <summary>
        ''' Three-phase (VLLE) temperature of a BINARY at a pressure: where vapour coexists with both
        ''' liquids, i.e. the heteroazeotropic boiling point.
        ''' <para>
        ''' The binodal moves with temperature, so this is a root find on it rather than a single flash: the
        ''' three-phase temperature is the one whose binodal liquids boil at exactly this pressure. Either
        ''' liquid serves, since both are in equilibrium with the same vapour and therefore share one bubble
        ''' pressure. The bracket is found by scanning, so it works whether the gap closes upwards at an
        ''' upper consolute temperature or downwards at a lower one.
        ''' </para>
        ''' </summary>
        ''' <returns>{T3, x1 in liquid 1, x1 in liquid 2, y1 in the vapour}, or Nothing when no miscibility
        ''' gap boils at this pressure within the range.</returns>
        ' Bubble pressure at m, stepping a little aside if it happens not to converge exactly there,
        ' so that a bisection whose midpoint lands on such a point does not give up. Reports through Tused
        ' which temperature actually produced the value.
        Private Function BubblePressureNear(ByVal m As Double, ByVal span As Double, ByRef Tused As Double) As Double
            Dim gap As Boolean = False
            For Each frac As Double In New Double() {0.0, 0.05, -0.05, 0.12, -0.12}
                Dim T As Double = m + frac * span
                Dim f As Double = BubblePressureOfBinodal(T, gap)
                If gap AndAlso Not Double.IsNaN(f) Then
                    Tused = T
                    Return f
                End If
            Next
            Tused = m
            Return Double.NaN
        End Function

        Public Function DW_CalculateThreePhaseTemperature(ByVal P As Double,
                                                          Optional ByVal Tmin As Double = 200.0,
                                                          Optional ByVal Tmax As Double = 600.0,
                                                          Optional ByVal nsub As Integer = 40) As Double()

            If RET_VNAMES().Length <> 2 Then Return Nothing
            If P <= 0.0 OrElse Tmax <= Tmin Then Return Nothing

            ' Two different things make a temperature unusable here, and they must not be confused. Where
            ' there is no miscibility gap at all, there is nothing to bracket across and the chain of points
            ' genuinely ends. But the bubble-point flash also fails at isolated temperatures - it throws
            ' "maximum iterations" at 430.6 K on UNIFAC Methanol/Cyclohexane at 2 MPa while converging
            ' normally a tenth of a kelvin either side, the binodal itself being perfectly smooth through it
            ' - and treating that as the end of the chain hides the crossing entirely, which is exactly what
            ' it did: one bad point between the last negative and the first positive residual, and the whole
            ' three-phase temperature went undetected.
            Dim a As Double = Double.NaN, b As Double = Double.NaN
            Dim fa As Double = Double.NaN
            Dim prevT As Double = Double.NaN, prevF As Double = Double.NaN
            Dim gap As Boolean = False

            For k As Integer = 0 To nsub
                Dim T As Double = Tmin + (Tmax - Tmin) / nsub * k
                Dim f As Double = BubblePressureOfBinodal(T, gap)
                If Not gap Then
                    ' no gap here, so no crossing can span this point
                    prevF = Double.NaN
                    Continue For
                End If
                ' the gap is there but the flash would not converge: skip the point, keep the last good one
                If Double.IsNaN(f) Then Continue For
                f -= P
                If Not Double.IsNaN(prevF) AndAlso prevF * f <= 0.0 Then
                    a = prevT : b = T : fa = prevF
                    Exit For
                End If
                prevT = T : prevF = f
            Next

            If Double.IsNaN(a) Then Return Nothing

            For it As Integer = 1 To 60
                Dim m As Double = 0.5 * (a + b)
                Dim mu As Double = m
                Dim fm As Double = BubblePressureNear(m, b - a, mu)
                If Double.IsNaN(fm) Then Exit For
                fm -= P
                ' mu, not m: the point actually evaluated is the one whose sign is known
                If fa * fm <= 0.0 Then b = mu Else a = mu : fa = fm
                If b - a < 0.00001 Then Exit For
            Next

            Dim T3 As Double = 0.5 * (a + b)
            Dim bi = DW_CalculateLLEBinodal(T3)
            If bi Is Nothing Then Return Nothing
            Dim bp = BubblePointOfBinodal(T3, bi)
            If bp Is Nothing Then Return Nothing
            Return New Double() {T3, bi(0), bi(1), bp(1)}

        End Function

        ''' <summary>
        ''' The three-phase (VLLE) state of a BINARY at a temperature: the pressure at which vapour coexists
        ''' with both liquids, and the vapour composition it coexists with.
        ''' <para>
        ''' It is the bubble point of either binodal liquid - both give the same one, being in equilibrium
        ''' with the same vapour - and that is a vapour-liquid problem, not a three-phase one: the liquid sits
        ''' exactly on the phase boundary, so the second liquid is there in vanishing amount. It is solved
        ''' with the two-phase algorithm for precisely that reason. Handing it to a flash that first tries to
        ''' split a liquid already known to be on the boundary sets it a degenerate problem, and the general
        ''' flash does fail on it - at isolated temperatures, throwing "maximum iterations" while converging a
        ''' tenth of a kelvin either side.
        ''' </para>
        ''' </summary>
        ''' <returns>{P3, y1 in the vapour, x1 in liquid 1, x1 in liquid 2}, or Nothing where there is no gap.</returns>
        Public Function DW_CalculateThreePhasePressure(ByVal T As Double) As Double()
            Dim bi = DW_CalculateLLEBinodal(T)
            If bi Is Nothing Then Return Nothing
            Dim r = BubblePointOfBinodal(T, bi)
            If r Is Nothing Then Return Nothing
            Return New Double() {r(0), r(1), bi(0), bi(1)}
        End Function

        ' Bubble point of a binodal liquid, as {P, y1}, or Nothing if it will not converge. Deliberately the
        ' two-phase algorithm: see DW_CalculateThreePhasePressure.
        Private Function BubblePointOfBinodal(ByVal T As Double, ByVal bi As Double()) As Double()
            Try
                Dim nl As New Auxiliary.FlashAlgorithms.NestedLoops()
                nl.FlashSettings = FlashBase.FlashSettings
                Dim tv As Object = nl.Flash_TV(New Double() {bi(0), 1.0 - bi(0)}, T, 0, 0, Me)
                Dim pb As Double = Convert.ToDouble(tv(4))
                If pb <= 0.0 OrElse Double.IsNaN(pb) Then Return Nothing
                Return New Double() {pb, DirectCast(tv(3), Double())(0)}
            Catch ex As Exception
                Return Nothing
            End Try
        End Function

        ' Bubble pressure of the binodal liquid at a temperature. gapExists reports whether a miscibility
        ' gap was found at all, which the caller must tell apart from the bubble point merely failing to
        ' converge: NaN with gapExists True means the point is unusable, not that the gap has ended.
        Private Function BubblePressureOfBinodal(ByVal T As Double, ByRef gapExists As Boolean) As Double
            Dim bi = DW_CalculateLLEBinodal(T)
            gapExists = bi IsNot Nothing
            If Not gapExists Then Return Double.NaN
            Dim r = BubblePointOfBinodal(T, bi)
            If r Is Nothing Then Return Double.NaN
            Return r(0)
        End Function


        Public Function DW_CalculateConsolutePoints(Optional ByVal Tmin As Double = 250.0,
                                                    Optional ByVal Tmax As Double = 550.0,
                                                    Optional ByVal nsub As Integer = 60) As List(Of Double())

            Dim res As New List(Of Double())
            If RET_VNAMES().Length <> 2 Then Return res
            If TryGetGammaDerivs(0.5 * (Tmin + Tmax), New Double() {0.5, 0.5}) Is Nothing Then Return res

            ' One evaluator for the whole search: the compound list does not change across it, and the
            ' scan below visits D2 hundreds of times per temperature.
            Dim d2 = GetGibbsMixingD2Evaluator()

            Dim dT As Double = (Tmax - Tmin) / nsub
            Dim xd As Double = 0.5
            Dim fprev As Double = MinGibbsMixingD2(d2, Tmin, xd)

            For k As Integer = 1 To nsub
                Dim Tk As Double = Tmin + k * dT
                Dim fk As Double = MinGibbsMixingD2(d2, Tk, xd)
                If Not Double.IsNaN(fprev) AndAlso Not Double.IsNaN(fk) AndAlso fprev * fk < 0.0 Then
                    Dim a As Double = Tk - dT, b As Double = Tk, fa As Double = fprev
                    For it As Integer = 1 To 100
                        Dim mid As Double = 0.5 * (a + b)
                        Dim xm As Double = 0.5
                        Dim fm As Double = MinGibbsMixingD2(d2, mid, xm)
                        If Double.IsNaN(fm) Then Exit For
                        If fa * fm <= 0.0 Then
                            b = mid
                        Else
                            a = mid : fa = fm
                        End If
                        If b - a < 0.00005 Then Exit For
                    Next
                    Dim Tc As Double = 0.5 * (a + b)
                    Dim xc As Double = 0.5
                    MinGibbsMixingD2(d2, Tc, xc)
                    res.Add(New Double() {Tc, xc})
                End If
                fprev = fk
            Next

            Return res

        End Function

        ''' <summary>
        ''' Analytical temperature derivative of the K-values for the activity-model packages, assembled from
        ''' the liquid/vapour d(ln phi)/dT above: dK_i/dT = K_i * (d ln phi_i^L/dT - d ln phi_i^V/dT). This is
        ''' what the analytical branch of the NestedLoops PV/TV flashes consumes. Falls back to finite
        ''' differences for non-standard (non-"LV") requests or near-critical mixtures.
        ''' </summary>
        Public Overrides Function DW_CalcdKdT(ByVal Vx As Double(), ByVal Vy As Double(), ByVal T As Double, ByVal P As Double, Optional ByVal type As String = "LV") As Double()
            If AnalyticalDerivativesDisabled OrElse type <> "LV" OrElse Not AllSubcritical(T) OrElse Not HasAnalyticalGammaModel Then Return MyBase.DW_CalcdKdT(Vx, Vy, T, P, type)
            Dim n As Integer = Vx.Length - 1
            Dim dlnfugL = DW_CalcdLnFugCoeffdT(Vx, T, P, State.Liquid)
            Dim dlnfugV = DW_CalcdLnFugCoeffdT(Vy, T, P, State.Vapor)
            Dim K = DW_CalcKvalue(Vx, Vy, T, P, type)
            Dim deriv(n) As Double
            For i As Integer = 0 To n
                deriv(i) = K(i) * (dlnfugL(i) - dlnfugV(i))
            Next
            Return deriv
        End Function

#End Region

        Public Overrides Function DW_CalcEnthalpy(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double

            If OverrideEnthalpyCalculation Then

                Return EnthalpyCalculationOverride.Invoke(Vx, T, P, st, Me)

            Else

                Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj, "", "DW_CalcEnthalpy", "Enthalpy", "Property Package Enthalpy Calculation Routine")

                IObj?.SetCurrent()

                Dim H As Double

                If st = State.Liquid Then
                    Select Case EnthalpyEntropyCpCvCalculationMode
                        Case 0 'LK
                            H = Me.m_lk.H_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Hid(298.15, T, Vx))
                        Case 1 'Ideal
                            H = Me.RET_Hid(298.15, T, Vx) - Me.RET_HVAPM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P)
                        Case 2 'Excess
                            Dim Hex = Me.m_act.CalcExcessEnthalpy(T, Vx, Me.GetArguments()) / Me.AUX_MMM(Vx)
                            If Double.IsNaN(Hex) Then Hex = 0.0
                            H = Me.RET_Hid(298.15, T, Vx) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) + Hex - Me.RET_HVAPM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T)
                                Case 3 'Experimental Liquid
                            H = AUX_INT_CPDTm_L(298.15, T, Me.AUX_CONVERT_MOL_TO_MASS(Vx)) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P)
                        Case 4 'Experimental Liquid + Excess
                            Dim Hex = Me.m_act.CalcExcessEnthalpy(T, Vx, Me.GetArguments()) / Me.AUX_MMM(Vx)
                            If Double.IsNaN(Hex) Then Hex = 0.0
                            H = AUX_INT_CPDTm_L(298.15, T, Me.AUX_CONVERT_MOL_TO_MASS(Vx)) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) + Hex
                    End Select
                ElseIf st = State.Vapor Then
                    Select Case EnthalpyEntropyCpCvCalculationMode
                        Case 0 'LK
                            H = Me.m_lk.H_LK_MIX("V", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Hid(298.15, T, Vx))
                        Case 1 'Ideal
                            H = Me.RET_Hid(298.15, T, Vx)
                        Case 2 'Excess
                            H = Me.RET_Hid(298.15, T, Vx)
                        Case 3, 4 'Experimental Liquid
                            H = RET_Hid_FromLiqCp(Vx, T, P)
                    End Select
                ElseIf st = State.Solid Then
                    If SolidPhaseEnthalpy_UsesCp Then
                        H = CalcSolidEnthalpyFromCp(T, Vx, DW_GetConstantProperties)
                    Else
                        Select Case EnthalpyEntropyCpCvCalculationMode
                            Case 0 'LK
                                H = Me.m_lk.H_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Hid(298.15, T, Vx)) - RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T)
                            Case 1, 2 'Ideal
                                H = Me.RET_Hid(298.15, T, Vx) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) - Me.RET_HVAPM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) - RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T)
                            Case 3, 4 'Experimental Liquid
                                H = AUX_INT_CPDTm_L(298.15, T, Me.AUX_CONVERT_MOL_TO_MASS(Vx)) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) - RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T)
                        End Select
                    End If
                End If

                IObj?.Close()

                Return H

            End If

        End Function

        Public Overrides Function DW_CalcEnthalpyDeparture(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double
            Dim H As Double

            If st = State.Liquid Then
                Select Case EnthalpyEntropyCpCvCalculationMode
                    Case 0 'LK
                        H = Me.m_lk.H_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, 0)
                    Case 1 'Ideal
                        H = 0.0#
                    Case 2 'Excess
                        H = Me.m_act.CalcExcessEnthalpy(T, Vx, Me.GetArguments()) / Me.AUX_MMM(Vx)
                End Select
            Else
                Select Case EnthalpyEntropyCpCvCalculationMode
                    Case 0 'LK
                        H = Me.m_lk.H_LK_MIX("V", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, 0)
                    Case 1 'Ideal
                        H = 0.0#
                    Case 2 'Excess
                        H = 0.0#
                End Select
            End If

            Return H

        End Function

        Public Overrides Function DW_CalcEntropy(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double

            If OverrideEntropyCalculation Then

                Return EntropyCalculationOverride.Invoke(Vx, T, P, st, Me)

            Else

                Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj, "", "DW_CalcEntropy", "Entropy", "Entropy Calculation Routine")

                IObj?.SetCurrent()

                Dim S As Double

                If st = State.Liquid Then
                    Select Case EnthalpyEntropyCpCvCalculationMode
                        Case 0 'LK
                            S = Me.m_lk.S_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Sid(298.15, T, P, Vx))
                        Case 1 'Ideal
                            S = Me.RET_Sid(298.15, T, P, Vx) - Me.RET_HVAPM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) / T
                        Case 2 'Excess
                            Dim gammaex = Me.m_act.CalcExcessEnthalpy(T, Vx, Me.GetArguments()) / Me.AUX_MMM(Vx)
                            If Double.IsNaN(gammaex) Then gammaex = 0.0
                            S = Me.RET_Sid(298.15, T, P, Vx) + gammaex / T + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) / T - Me.RET_HVAPM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T
                        Case 3 'Experimental Liquid
                            S = AUX_INT_CPDTm_L(298.15, T, Me.AUX_CONVERT_MOL_TO_MASS(Vx)) / T + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) / T
                        Case 4 'Experimental Liquid + Excess
                            Dim gammaex = Me.m_act.CalcExcessEnthalpy(T, Vx, Me.GetArguments()) / Me.AUX_MMM(Vx)
                            If Double.IsNaN(gammaex) Then gammaex = 0.0
                            S = AUX_INT_CPDTm_L(298.15, T, Me.AUX_CONVERT_MOL_TO_MASS(Vx)) / T + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) / T + gammaex / T
                    End Select
                ElseIf st = State.Vapor Then
                    Select Case EnthalpyEntropyCpCvCalculationMode
                        Case 0 'LK
                            S = Me.m_lk.S_LK_MIX("V", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Sid(298.15, T, P, Vx))
                        Case 1 'Ideal
                            S = Me.RET_Sid(298.15, T, P, Vx)
                        Case 2 'Excess
                            S = Me.RET_Sid(298.15, T, P, Vx)
                        Case 3, 4 'Experimental Liquid
                            S = RET_Sid_FromLiqCp(Vx, T, P)
                    End Select
                ElseIf st = State.Solid Then
                    If SolidPhaseEnthalpy_UsesCp Then
                        S = CalcSolidEnthalpyFromCp(T, Vx, DW_GetConstantProperties) / T
                    Else
                        Select Case EnthalpyEntropyCpCvCalculationMode
                            Case 0 'LK
                                S = Me.m_lk.S_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Sid(298.15, T, P, Vx)) - Me.RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T
                            Case 1 'Ideal
                                S = Me.RET_Sid(298.15, T, P, Vx) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) / T - Me.RET_HVAPM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T - Me.RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T
                            Case 2 'Excess
                                S = Me.RET_Sid(298.15, T, P, Vx) + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) / T - Me.m_act.CalcExcessEnthalpy(T, Vx, Me.GetArguments()) / Me.AUX_MMM(Vx) / T - Me.RET_HVAPM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T - Me.RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T
                            Case 3, 4 'Experimental Liquid
                                S = AUX_INT_CPDTm_L(298.15, T, Me.AUX_CONVERT_MOL_TO_MASS(Vx)) / T - RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T + P / 1000 / Me.AUX_LIQDENS(T, Vx, P) / T
                        End Select
                    End If
                End If

                IObj?.Close()

                Return S

            End If

        End Function

        Public Overrides Function DW_CalcEntropyDeparture(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double
            Dim S As Double

            If st = State.Liquid Then
                Select Case EnthalpyEntropyCpCvCalculationMode
                    Case 0 'LK
                        S = Me.m_lk.S_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, 0)
                    Case 1 'Ideal
                        S = 0.0#
                    Case 2 'Excess
                        S = (Me.m_act.CalcExcessEnthalpy(T, Vx, Me.GetArguments()) / Me.AUX_MMM(Vx)) / T
                End Select
            Else
                Select Case EnthalpyEntropyCpCvCalculationMode
                    Case 0 'LK
                        S = Me.m_lk.S_LK_MIX("V", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, 0)
                    Case 1 'Ideal
                        S = 0.0#
                    Case 2 'Excess
                        S = 0.0#
                End Select
            End If

            Return S

        End Function

        Public Overrides Function DW_CalcFugCoeff(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double()

            Calculator.WriteToConsole(Me.ComponentName & " fugacity coefficient calculation for phase '" & st.ToString & "' requested at T = " & T & " K and P = " & P & " Pa.", 2)
            Calculator.WriteToConsole("Compounds: " & Me.RET_VNAMES.ToArrayString, 2)
            Calculator.WriteToConsole("Mole fractions: " & Vx.ToArrayString(), 2)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "DW_CalcFugCoeff", "Fugacity Coefficient", "Property Package Fugacity Coefficient Calculation Routine")

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", DirectCast(Vx, Double()).ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("State: {0}", [Enum].GetName(st.GetType, st)))

            If Not ActivityCoefficientModels_IgnoreMissingInteractionParameters Then
                CheckMissingInteractionParameters(Vx)
            End If

            Dim n As Integer = Vx.Length - 1
            Dim lnfug(n), ativ(n) As Double
            Dim fugcoeff(n), poy(n) As Double
            Dim i As Integer

            Dim constprop = Me.DW_GetConstantProperties

            Dim Tc As Double() = Me.RET_VTC()
            Dim Tr As Double
            If st = State.Liquid Then

                If LiquidFugacity_UsePoyntingCorrectionFactor Then
                    IObj?.Paragraphs.Add(String.Format("<h2>Poynting Correction</h2>"))
                    IObj?.Paragraphs.Add(String.Format("Poynting Correction Factor calculation is enabled."))
                    IObj?.Paragraphs.Add("The Poynting factor is a correction factor for the liquid phase vapor pressure. 
                                        Unless pressures are very high, the Poynting factor is usually small and the exponential term is near 1.")
                    IObj?.Paragraphs.Add("<m>{\ln {\frac {f}{f_{\mathrm {sat} }}}={\frac {V_{\mathrm {m} }}{RT}}\int _{P_{\mathrm {sat} }}^{P}dp={\frac {V\left(P-P_{\mathrm {sat} }\right)}{RT}}.}</m>")
                    IObj?.Paragraphs.Add("This fraction is known as the Poynting correction factor. Using <mi>f_{sat}=\phi_{sat} P_{sat}</mi>, where <mi>\phi_{sat}</mi> is the fugacity coefficient,")
                    IObj?.Paragraphs.Add("<m>f=\varphi _{\mathrm {sat} }P_{\mathrm {sat} }\exp \left({\frac {V\left(P-P_{\mathrm {sat} }\right)}{RT}}\right).</m>")
                    Dim Psati, vli As Double
                    For i = 0 To n
                        If T < 0.98 * Tc(i) Then
                            IObj?.SetCurrent()
                            IObj?.Paragraphs.Add(String.Format("<b>{0}</b>", constprop(i).Name))
                            vli = 1 / AUX_LIQDENSi(constprop(i), T) * constprop(i).Molar_Weight
                            If Double.IsNaN(vli) Then
                                IObj?.SetCurrent()
                                vli = 1 / AUX_LIQDENSi(constprop(i), constprop(i).Normal_Boiling_Point) * constprop(i).Molar_Weight
                            End If
                            IObj?.Paragraphs.Add(String.Format("Molar Volume (V) @ {0} K: {1} m3/kmol", T, vli))
                            IObj?.SetCurrent()
                            Psati = AUX_PVAPi(i, T)
                            IObj?.Paragraphs.Add(String.Format("Vapor Pressure (Psat) @ {0} K: {1} Pa", T, Psati))
                            poy(i) = Math.Exp(vli * Abs(P - Psati) / (8314.47 * T))
                            IObj?.Paragraphs.Add(String.Format("Poynting Correction Factor: {0}", poy(i)))
                        End If
                    Next
                Else
                    For i = 0 To n
                        poy(i) = 1.0#
                    Next
                End If

                IObj?.SetCurrent()
                IObj?.Paragraphs.Add(String.Format("<h2>Activity Coefficients</h2>"))
                ativ = Me.m_act.CalcActivityCoefficients(T, Vx, Me.GetArguments())

                IObj?.Paragraphs.Add(String.Format("Calculated Activity Coefficients: {0}", ativ.ToMathArrayString))

                IObj?.Paragraphs.Add(String.Format("<h2>Fugacity Coefficients</h2>"))

                For i = 0 To n
                    Tr = T / Tc(i)
                    IObj?.Paragraphs.Add(String.Format("<b>{0}</b>", constprop(i).Name))
                    IObj?.Paragraphs.Add("Reduced Temperature (<mi>T_r=T/T_c</mi>): " & Tr.ToString)
                    If Tr >= 1.02 Then
                        IObj?.SetCurrent()
                        IObj?.Paragraphs.Add("<m>f_i = H_i/P</m>")
                        If UseHenryConstants And HasHenryConstants(constprop(i).Name) Then
                            Dim hc = AUX_KHenry(constprop(i).Name, T)
                            IObj?.Paragraphs.Add(String.Format("Henry's Constant (H) @ {0} K: {1} Pa", T, hc))
                            lnfug(i) = Log(hc / P)
                        Else
                            lnfug(i) = Log(AUX_PVAPi(i, T) / (P))
                        End If
                    ElseIf Tr < 0.98 Then
                        IObj?.Paragraphs.Add("<m>f_i = \gamma_i Poy_i P_{sat_i}/P</m>")
                        IObj?.Paragraphs.Add(String.Format("Activity Coefficient: {0}", ativ(i)))
                        IObj?.Paragraphs.Add(String.Format("Vapor Pressure (Psat) @ {0} K: {1} Pa", T, Me.AUX_PVAPi(i, T)))
                        IObj?.Paragraphs.Add(String.Format("Poynting Correction Factor: {0}", poy(i)))
                        lnfug(i) = Log(ativ(i) * Me.AUX_PVAPi(i, T) / (P)) + Log(poy(i))
                        IObj?.Paragraphs.Add(String.Format("Fugacity Coefficient: {0}", Exp(lnfug(i))))
                    Else 'do interpolation at proximity of critical point
                        IObj?.SetCurrent()
                        Dim a2 As Double = AUX_KHenry(Me.RET_VNAMES(i), 1.02 * Tc(i))
                        Dim a1 As Double = ativ(i) * Me.AUX_PVAPi(i, 0.98 * Tc(i))
                        If Not Double.IsNaN(a1) Then
                            lnfug(i) = Math.Log(((Tr - 0.98) / (1.02 - 0.98) * (a2 - a1) + a1) / P)
                        Else
                            lnfug(i) = Log(a2 / P)
                        End If
                    End If
                Next

            Else

                If VaporPhaseFugacityCalculationMode = VaporPhaseFugacityCalcMode.Ideal Then
                    For i = 0 To n
                        lnfug(i) = 0.0#
                    Next
                Else
                    Dim prn As New PropertyPackages.ThermoPlugs.PR
                    IObj?.SetCurrent()
                    lnfug = prn.CalcLnFug(T, P, Vx, Me.RET_VKij, Me.RET_VTC, Me.RET_VPC, Me.RET_VW, Nothing, 1)
                End If

            End If

            For i = 0 To n
                fugcoeff(i) = Exp(lnfug(i))
            Next

            Calculator.WriteToConsole("Result: " & fugcoeff.ToArrayString(), 2)

            IObj?.Paragraphs.Add(String.Format("<h2>Results</h2>"))

            IObj?.Paragraphs.Add(String.Format("Fugacity Coefficients: {0}", fugcoeff.ToMathArrayString))

            IObj?.Close()

            Return fugcoeff

        End Function

        Public Overrides Sub DW_CalcProp(ByVal [property] As String, ByVal phase As Phase)

            Dim result As Double = 0.0#
            Dim resultObj As Object = Nothing
            Dim phaseID As Integer = -1
            Dim state As String = "", pstate As State

            Dim T, P As Double
            T = Me.CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault
            P = Me.CurrentMaterialStream.Phases(0).Properties.pressure.GetValueOrDefault

            Select Case phase
                Case Phase.Vapor
                    state = "V"
                    pstate = PropertyPackages.State.Vapor
                Case Phase.Liquid, Phase.Liquid1, Phase.Liquid2, Phase.Liquid3, Phase.Aqueous
                    state = "L"
                    pstate = PropertyPackages.State.Liquid
                Case Phase.Solid
                    state = "S"
                    pstate = PropertyPackages.State.Solid
            End Select

            Select Case phase
                Case PropertyPackages.Phase.Mixture
                    phaseID = 0
                Case PropertyPackages.Phase.Vapor
                    phaseID = 2
                Case PropertyPackages.Phase.Liquid1
                    phaseID = 3
                Case PropertyPackages.Phase.Liquid2
                    phaseID = 4
                Case PropertyPackages.Phase.Liquid3
                    phaseID = 5
                Case PropertyPackages.Phase.Liquid
                    phaseID = 1
                Case PropertyPackages.Phase.Aqueous
                    phaseID = 6
                Case PropertyPackages.Phase.Solid
                    phaseID = 7
            End Select

            Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = Me.AUX_MMM(phase)

            Select Case [property].ToLower
                Case "isothermalcompressibility", "bulkmodulus", "joulethomsoncoefficient", "speedofsound", "internalenergy", "gibbsenergy", "helmholtzenergy"
                    CalcAdditionalPhaseProperties(phaseID)
                Case "compressibilityfactor"
                    result = Me.m_lk.Z_LK(state, T / Me.AUX_TCM(phase), P / Me.AUX_PCM(phase), Me.AUX_WM(phase))(0)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = result
                Case "heatcapacity", "heatcapacitycp"
                    If state = "V" Then
                        resultObj = Me.m_lk.CpCvR_LK(state, T, P, RET_VMOL(phase), RET_VKij(), RET_VMAS(phase), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())
                        result = resultObj(1)
                    Else
                        Select Case EnthalpyEntropyCpCvCalculationMode
                            Case 0 'LK
                                resultObj = Me.m_lk.CpCvR_LK(state, T, P, RET_VMOL(phase), RET_VKij(), RET_VMAS(phase), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())
                                result = resultObj(1)
                            Case 1, 3 'Ideal/Experimental
                                result = Me.AUX_LIQCPm(T, phaseID)
                            Case 2 'Excess
                                result = Me.AUX_LIQCPm(T, phaseID) + Me.m_act.CalcExcessHeatCapacity(T, RET_VMOL(phase), Me.GetArguments()) / Me.AUX_MMM(phase)
                        End Select
                    End If
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = result
                Case "heatcapacitycv"
                    If state = "V" Then
                        resultObj = Me.m_lk.CpCvR_LK(state, T, P, RET_VMOL(phase), RET_VKij(), RET_VMAS(phase), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())
                        result = resultObj(2)
                    Else
                        Select Case EnthalpyEntropyCpCvCalculationMode
                            Case 0 'LK
                                resultObj = Me.m_lk.CpCvR_LK(state, T, P, RET_VMOL(phase), RET_VKij(), RET_VMAS(phase), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())
                                result = resultObj(2)
                            Case 1, 3 'Ideal/Experimental
                                result = Me.AUX_LIQCPm(T, phaseID)
                            Case 2 'Excess
                                result = Me.AUX_LIQCPm(T, phaseID) + Me.m_act.CalcExcessHeatCapacity(T, RET_VMOL(phase), Me.GetArguments()) / Me.AUX_MMM(phase)
                        End Select
                    End If
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = result
                Case "enthalpy", "enthalpynf"
                    result = Me.DW_CalcEnthalpy(RET_VMOL(phase), T, P, pstate)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = result
                    result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result
                Case "entropy", "entropynf"
                    result = Me.DW_CalcEntropy(RET_VMOL(phase), T, P, pstate)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = result
                    result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropy = result
                Case "excessenthalpy"
                    result = Me.DW_CalcEnthalpyDeparture(RET_VMOL(phase), T, P, pstate)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.excessEnthalpy = result
                Case "excessentropy"
                    result = Me.DW_CalcEntropyDeparture(RET_VMOL(phase), T, P, pstate)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.excessEntropy = result
                Case "enthalpyf"
                    Dim entF As Double = Me.AUX_HFm25(phase)
                    result = Me.DW_CalcEnthalpy(RET_VMOL(phase), T, P, pstate)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpyF = result + entF
                    result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpyF.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpyF = result
                Case "entropyf"
                    Dim entF As Double = Me.AUX_SFm25(phase)
                    result = Me.DW_CalcEntropy(RET_VMOL(phase), T, P, pstate)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.entropyF = result + entF
                    result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropyF.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropyF = result
                Case "viscosity"
                    If state = "L" Then
                        result = Me.AUX_LIQVISCm(T, P)
                    Else
                        result = Me.AUX_VAPVISCm(T, Me.CurrentMaterialStream.Phases(phaseID).Properties.density.GetValueOrDefault, Me.AUX_MMM(phase))
                    End If
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.viscosity = result
                Case "thermalconductivity"
                    If state = "L" Then
                        result = Me.AUX_CONDTL(T)
                    Else
                        result = Me.AUX_CONDTG(T, P)
                    End If
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.thermalConductivity = result
                Case "fugacity", "fugacitycoefficient", "logfugacitycoefficient", "activity", "activitycoefficient"
                    Me.DW_CalcCompFugCoeff(phase)
                Case "volume", "density"
                    If state = "L" Then
                        result = Me.AUX_LIQDENS(T, P, 0.0#, phaseID, False)
                    Else
                        result = Me.AUX_VAPDENS(T, P)
                    End If
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.density = result
                Case "surfacetension"
                    Me.CurrentMaterialStream.Phases(0).Properties.surfaceTension = Me.AUX_SURFTM(T)
                Case Else
                    Dim ex As Exception = New CapeOpen.CapeThrmPropertyNotAvailableException
                    ThrowCAPEException(ex, "Error", ex.Message, "ICapeThermoMaterial", ex.Source, ex.StackTrace, "CalcSinglePhaseProp/CalcTwoPhaseProp/CalcProp", ex.GetHashCode)
            End Select

        End Sub

        Public Overrides Sub DW_CalcPhaseProps(ByVal Phase As PropertyPackages.Phase)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "DW_CalcPhaseProps", ComponentName & String.Format(" (Phase Properties - {0})", [Enum].GetName(Phase.GetType, Phase)), "Property Package Phase Properties Calculation Routine")

            IObj?.Paragraphs.Add("This is the routine responsible for the calculation of phase properties of the currently associated Material Stream.")

            IObj?.Paragraphs.Add("Specified Phase: " & [Enum].GetName(Phase.GetType, Phase))

            Dim result As Double
            Dim resultObj As Object
            Dim dwpl As Phase

            Dim T, P As Double
            Dim phasemolarfrac As Double = Nothing
            Dim overallmolarflow As Double = Nothing

            Dim phaseID As Integer
            T = Me.CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault
            P = Me.CurrentMaterialStream.Phases(0).Properties.pressure.GetValueOrDefault

            Select Case Phase
                Case PropertyPackages.Phase.Mixture
                    phaseID = 0
                    dwpl = PropertyPackages.Phase.Mixture
                Case PropertyPackages.Phase.Vapor
                    phaseID = 2
                    dwpl = PropertyPackages.Phase.Vapor
                Case PropertyPackages.Phase.Liquid1
                    phaseID = 3
                    dwpl = PropertyPackages.Phase.Liquid1
                Case PropertyPackages.Phase.Liquid2
                    phaseID = 4
                    dwpl = PropertyPackages.Phase.Liquid2
                Case PropertyPackages.Phase.Liquid3
                    phaseID = 5
                    dwpl = PropertyPackages.Phase.Liquid3
                Case PropertyPackages.Phase.Liquid
                    phaseID = 1
                    dwpl = PropertyPackages.Phase.Liquid
                Case PropertyPackages.Phase.Aqueous
                    phaseID = 6
                    dwpl = PropertyPackages.Phase.Aqueous
                Case PropertyPackages.Phase.Solid
                    phaseID = 7
                    dwpl = PropertyPackages.Phase.Solid
            End Select

            IObj?.SetCurrent

            If phaseID > 0 Then
                overallmolarflow = Me.CurrentMaterialStream.Phases(0).Properties.molarflow.GetValueOrDefault
                phasemolarfrac = Me.CurrentMaterialStream.Phases(phaseID).Properties.molarfraction.GetValueOrDefault
                result = overallmolarflow * phasemolarfrac
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molarflow = result
                result = result * Me.AUX_MMM(Phase) / 1000
                Me.CurrentMaterialStream.Phases(phaseID).Properties.massflow = result
                IObj?.SetCurrent
                Me.DW_CalcCompVolFlow(phaseID)
                IObj?.SetCurrent
                Me.DW_CalcCompFugCoeff(Phase)
            End If

            If phaseID = 3 Or phaseID = 4 Or phaseID = 5 Or phaseID = 6 Then

                If TypeOf Me Is SourWaterPropertyPackage Then
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.pH = New Auxiliary.Electrolyte().pH(RET_VMOL(dwpl), T, Me.DW_GetConstantProperties)
                End If

                IObj?.SetCurrent

                result = Me.AUX_LIQDENS(T, P, 0.0#, phaseID, False)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.density = result
                IObj?.SetCurrent
                Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = Me.DW_CalcEnthalpy(RET_VMOL(dwpl), T, P, State.Liquid)
                IObj?.SetCurrent
                Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = Me.DW_CalcEntropy(RET_VMOL(dwpl), T, P, State.Liquid)
                IObj?.SetCurrent
                result = Me.m_lk.Z_LK("L", T / Me.AUX_TCM(dwpl), P / Me.AUX_PCM(dwpl), Me.AUX_WM(dwpl))(0)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = result
                IObj?.SetCurrent
                Select Case EnthalpyEntropyCpCvCalculationMode
                    Case 0 'LK
                        resultObj = Me.m_lk.CpCvR_LK("L", T, P, RET_VMOL(dwpl), RET_VKij(), RET_VMAS(dwpl), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())
                        Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = resultObj(1)
                        Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = resultObj(2)
                    Case 1, 3 'Ideal/Experimental
                        result = Me.AUX_LIQCPm(T, phaseID)
                        Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = result
                        Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = result
                    Case 2 'Excess
                        result = Me.AUX_LIQCPm(T, phaseID) + Me.m_act.CalcExcessHeatCapacity(T, RET_VMOL(dwpl), Me.GetArguments()) / Me.AUX_MMM(dwpl)
                        Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = result
                        Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = result
                End Select
                result = Me.AUX_MMM(Phase)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = result
                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result
                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropy = result
                IObj?.SetCurrent
                result = Me.AUX_CONDTL(T)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.thermalConductivity = result
                IObj?.SetCurrent
                result = Me.AUX_LIQVISCm(T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.viscosity = result
                Me.CurrentMaterialStream.Phases(phaseID).Properties.kinematic_viscosity = result / Me.CurrentMaterialStream.Phases(phaseID).Properties.density.Value

            ElseIf phaseID = 2 Then

                IObj?.SetCurrent
                result = Me.AUX_VAPDENS(T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.density = result
                IObj?.SetCurrent
                Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = Me.DW_CalcEnthalpy(RET_VMOL(dwpl), T, P, State.Vapor)
                IObj?.SetCurrent
                Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = Me.DW_CalcEntropy(RET_VMOL(dwpl), T, P, State.Vapor)
                IObj?.SetCurrent
                result = m_pr.Z_PR(T, P, RET_VMOL(Phase.Vapor), RET_VKij, RET_VTC, RET_VPC, RET_VW, "V")
                Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = result
                IObj?.SetCurrent
                result = Me.AUX_CPm(PropertyPackages.Phase.Vapor, T)
                IObj?.SetCurrent
                resultObj = Auxiliary.PROPS.CpCvR("V", T, P, RET_VMOL(PropertyPackages.Phase.Vapor), RET_VKij(), RET_VMAS(PropertyPackages.Phase.Vapor), RET_VTC(), RET_VPC(), RET_VCP(T), RET_VMM(), RET_VW(), RET_VZRa())
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = resultObj(1)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = resultObj(2)
                result = Me.AUX_MMM(Phase)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = result
                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result
                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropy = result
                IObj?.SetCurrent
                result = Me.AUX_CONDTG(T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.thermalConductivity = result
                IObj?.SetCurrent
                result = Me.AUX_VAPVISCm(T, Me.CurrentMaterialStream.Phases(phaseID).Properties.density.GetValueOrDefault, Me.AUX_MMM(Phase))
                Me.CurrentMaterialStream.Phases(phaseID).Properties.viscosity = result
                Me.CurrentMaterialStream.Phases(phaseID).Properties.kinematic_viscosity = result / Me.CurrentMaterialStream.Phases(phaseID).Properties.density.Value

            ElseIf phaseID = 7 Then

                IObj?.SetCurrent
                result = Me.AUX_SOLIDDENS
                Me.CurrentMaterialStream.Phases(phaseID).Properties.density = result
                Dim constprops As New List(Of Interfaces.ICompoundConstantProperties)
                For Each su As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(0).Compounds.Values
                    constprops.Add(su.ConstantProperties)
                Next
                IObj?.SetCurrent
                Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = Me.DW_CalcEnthalpy(RET_VMOL(dwpl), T, P, State.Solid)
                IObj?.SetCurrent
                Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = Me.DW_CalcEntropy(RET_VMOL(dwpl), T, P, State.Solid)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = 0.0# 'result
                IObj?.SetCurrent
                result = Me.DW_CalcSolidHeatCapacityCp(T, RET_VMOL(PropertyPackages.Phase.Solid), constprops)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = result
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = result
                result = Me.AUX_MMM(Phase)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = result
                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result
                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropy = result
                IObj?.SetCurrent
                result = Me.AUX_CONDTG(T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.thermalConductivity = 0.0# 'result
                Me.CurrentMaterialStream.Phases(phaseID).Properties.viscosity = 1.0E+20
                Me.CurrentMaterialStream.Phases(phaseID).Properties.kinematic_viscosity = 1.0E+20

            ElseIf phaseID = 1 Then

                IObj?.SetCurrent
                DW_CalcLiqMixtureProps()

            Else

                IObj?.SetCurrent
                DW_CalcOverallProps()

            End If

            If phaseID > 0 Then
                If Me.CurrentMaterialStream.Phases(phaseID).Properties.density.GetValueOrDefault > 0 And overallmolarflow > 0 Then
                    result = overallmolarflow * phasemolarfrac * Me.AUX_MMM(Phase) / 1000 / Me.CurrentMaterialStream.Phases(phaseID).Properties.density.GetValueOrDefault
                Else
                    result = 0
                End If
                Me.CurrentMaterialStream.Phases(phaseID).Properties.volumetric_flow = result
            Else
                'result = Me.CurrentMaterialStream.Phases(phaseID).Properties.massflow.GetValueOrDefault / Me.CurrentMaterialStream.Phases(phaseID).Properties.density.GetValueOrDefault
                'Me.CurrentMaterialStream.Phases(phaseID).Properties.volumetric_flow = result
            End If

            IObj?.Close()

        End Sub

#End Region

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides Function AUX_Z(Vx() As Double, T As Double, P As Double, state As PhaseName) As Double

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "AUX_Z", "Compressibility Factor", "Compressibility Factor Calculation Routine")

            IObj?.SetCurrent()

            Dim TCM As Double = RET_VTC().MultiplyY(Vx).Sum
            Dim PCM As Double = RET_VPC().MultiplyY(Vx).Sum
            Dim WM As Double = RET_VW().MultiplyY(Vx).Sum

            Dim val As Double
            If state = PhaseName.Liquid Then
                val = P / (Me.AUX_LIQDENS(T, Vx, P) * 8.314 * T) / 1000 * AUX_MMM(Vx)
            Else
                val = P / (Me.AUX_VAPDENS(T, P) * 8.314 * T) / 1000 * AUX_MMM(Vx)
            End If

            IObj?.Paragraphs.Add("<h2>Results</h2>")

            IObj?.Paragraphs.Add(String.Format("Compressibility Factor: {0}", val))

            IObj?.Close()

            Return val

        End Function

        Public MustOverride Function CheckMissingInteractionParameters(Vx As Double()) As Boolean

    End Class

End Namespace
