Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.ExtensionMethods
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary
Imports System.IO
Imports FileHelpers
Imports System.Windows.Forms

Namespace DWSIM.Thermodynamics.AdvancedEOS

    <DelimitedRecord(vbTab)> <IgnoreFirst()> <System.Serializable()> Public Class PCSParam

        Public compound As String = ""
        Public casno As String = ""
        Public mw As Double = 0.0#
        Public m As Double = 0.0#
        Public sigma As Double = 0.0#
        Public epsilon As Double = 0.0#
        <FieldNullValue(0.0#)> Public kAiBi As Double = 0.0#
        <FieldNullValue(0.0#)> Public epsilon2 As Double = 0.0#
        ' Polymers: segment number per unit molar mass (mol/g). When > 0 the compound is a polymer and
        ' its segment number is m = m_over_M * Molar_Weight, so a single row covers any chain length.
        <FieldOptional()> <FieldNullValue(0.0#)> Public m_over_M As Double = 0.0#
        ' Association scheme (Huang-Radosz): empty/2B = one donor + one acceptor site; 4C = two donors and
        ' two acceptors (like water and the glycols); 4C/ETHER = a PEG-type chain, 4C end groups plus
        ' N_ether = 0.022*Mn - 1.409 extra ether-oxygen acceptor sites (Kontogeorgis & Folas eq. 14.9).
        ' Only unlike sites (donor-acceptor) associate. Site counts are applied as a multiplicity in InitPP.
        <FieldOptional()> <FieldNullValue("")> Public scheme As String = ""
        <FieldHidden()> Public associationparams As String = ""
        ' Copolymer definition (Gross, Spuhl, Tumakaka & Sadowski 2003). A random or alternating copolymer
        ' is defined at runtime, not shipped in pcsaft.dat, as the repeat-unit segment CAS numbers and their
        ' mass fractions: "casR:wR;casS:wS". Each segment reuses the homopolymer parameters keyed by its CAS,
        ' and the segment-segment kij (including the internal repeat-unit correction) is looked up in
        ' pcsaft_ip.dat by the segment CAS pair. Empty for an ordinary compound.
        <FieldHidden()> Public copolymer As String = ""
        ' Copolymer sequence: "" or "random" (default) applies the Table 1 random bonding fractions;
        ' "alternating" applies the strictly alternating ones.
        <FieldHidden()> Public coseq As String = ""

    End Class

    <DelimitedRecord(vbTab)> <IgnoreFirst()> <System.Serializable()> Public Class PCSIP

        Implements ICloneable

        Public compound1 As String = ""
        Public casno1 As String = ""
        Public compound2 As String = ""
        Public casno2 As String = ""
        Public kij As Double = 0.0#

        Public Function Clone() As Object Implements System.ICloneable.Clone

            Dim newclass As New PCSIP
            With newclass
                .compound1 = Me.compound1
                .compound2 = Me.compound2
                .casno1 = Me.casno1
                .casno2 = Me.casno2
                .kij = Me.kij
            End With
            Return newclass
        End Function

    End Class

    <System.Serializable> Public Partial Class PCSAFT2PropertyPackage

        Inherits PropertyPackage

        Dim pr As New PengRobinson
        Dim lk As New LeeKesler

        Public Property CompoundParameters As Dictionary(Of String, PCSParam) = New Dictionary(Of String, PCSParam)

        Public Property InteractionParameters As Dictionary(Of String, Dictionary(Of String, PCSIP)) = New Dictionary(Of String, Dictionary(Of String, PCSIP))

        Public Property UseLeeKeslerEnthalpy As Boolean = True

        Public Property UseLeeKeslerCpCv As Boolean = True

        Public Overrides ReadOnly Property DisplayDescription As String
            Get
                Return ComponentDescription
            End Get
        End Property

        Public Sub New()

            ComponentName = "PC-SAFT (with Association Support) (.NET Code)"
            ComponentDescription = "The Perturbed Chain SAFT model is a state-of-the-art, engineering-like equation of state. It is designed for modelling mixtures of all types of substances: gases, solvents and polymers."

            IsConfigurable = True

            ReadParameters()

            With PropertyMethodsInfo
                .Vapor_Fugacity = "PC-SAFT EOS"
                .Vapor_Enthalpy_Entropy_CpCv = "PC-SAFT EOS"
                .Vapor_Density = "PC-SAFT EOS"
                .Liquid_Fugacity = "PC-SAFT EOS"
                .Liquid_Enthalpy_Entropy_CpCv = "PC-SAFT EOS"
            End With

        End Sub

        Protected Sub ReadParameters()

            Dim pathsep As Char = System.IO.Path.DirectorySeparatorChar
            Dim pcsaftdatac() As PCSParam = Nothing
            Dim fh1 As FileHelperEngine(Of PCSParam) = New FileHelperEngine(Of PCSParam)

            Dim res = System.Reflection.Assembly.GetExecutingAssembly.GetManifestResourceNames
            Dim filestr As Stream = System.Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream("pcsaft.dat")

            Using t As StreamReader = New StreamReader(filestr)
                pcsaftdatac = fh1.ReadStream(t)
                For Each pcsaftdata As PCSParam In pcsaftdatac
                    Dim ci = Globalization.CultureInfo.InvariantCulture
                    Dim k As String = pcsaftdata.kAiBi.ToString(ci), e As String = pcsaftdata.epsilon2.ToString(ci)
                    ' Association is a two-site-type donor/acceptor (A-B) scheme. How many of each site type
                    ' there are - 2B: one each; 4C: two donors and two acceptors; PEG: two donors and
                    ' 2 + N_ether acceptors (the ether oxygens, Kontogeorgis & Folas eq. 14.9) - is applied
                    ' as a per-type MULTIPLICITY in InitPP from the scheme column, so the kappa and epsilon
                    ' matrices are always the 2x2 A-B form here regardless of scheme.
                    pcsaftdata.associationparams = "2" & Environment.NewLine &
                        $"[0 {k}; {k} 0]" & Environment.NewLine & $"[0 {e}; {e} 0]"
                    If Not CompoundParameters.ContainsKey(pcsaftdata.casno) Then
                        CompoundParameters.Add(pcsaftdata.casno, pcsaftdata)
                    End If
                Next
            End Using

            fh1 = Nothing

            Dim pripc() As PCSIP = Nothing

            Dim fh2 As FileHelperEngine(Of PCSIP) = New FileHelperEngine(Of PCSIP)

            filestr = System.Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream("pcsaft_ip.dat")
            Using t As StreamReader = New StreamReader(filestr)
                pripc = fh2.ReadStream(t)
                For Each ip As PCSIP In pripc
                    If InteractionParameters.ContainsKey(ip.casno1) Then
                        If InteractionParameters(ip.casno1).ContainsKey(ip.casno2) Then

                        Else
                            InteractionParameters(ip.casno1).Add(ip.casno2, CType(ip.Clone, PCSIP))
                        End If

                    Else
                        InteractionParameters.Add(ip.casno1, New Dictionary(Of String, PCSIP))
                        InteractionParameters(ip.casno1).Add(ip.casno2, CType(ip.Clone, PCSIP))
                    End If

                Next
            End Using
            For Each ip As PCSIP In pripc
                If InteractionParameters.ContainsKey(ip.casno1) Then
                    If InteractionParameters(ip.casno1).ContainsKey(ip.casno2) Then

                    Else
                        InteractionParameters(ip.casno1).Add(ip.casno2, CType(ip.Clone, PCSIP))
                    End If

                Else
                    InteractionParameters.Add(ip.casno1, New Dictionary(Of String, PCSIP))
                    InteractionParameters(ip.casno1).Add(ip.casno2, CType(ip.Clone, PCSIP))
                End If

            Next

            pripc = Nothing
            fh2 = Nothing

        End Sub

        Public Overrides Sub RunPostMaterialStreamSetRoutine()
            If Flowsheet IsNot Nothing Then
                Dim comps = RET_VCAS()
                Dim names = RET_VNAMES()
                Dim i = 0
                For Each comp In comps
                    If Not CompoundParameters.ContainsKey(comp) Then
                        Throw New Exception(String.Format("Missing PC-SAFT parameters for {0}. Calculation results will be unreliable", names(i)))
                    Else
                        ' A copolymer has no single sigma/epsilon/m of its own; its parameters come from the
                        ' repeat-unit segments, so exempt it from the empty-parameter check.
                        Dim isCopoly = Not String.IsNullOrEmpty(CompoundParameters(comp).copolymer)
                        If Not isCopoly AndAlso CompoundParameters(comp).sigma = 0.0 And CompoundParameters(comp).epsilon = 0.0 And CompoundParameters(comp).m = 0.0 Then
                            Throw New Exception(String.Format("Missing PC-SAFT parameters for {0}. Calculation results will be unreliable", names(i)))
                        End If
                    End If
                    i += 1
                Next
            End If
        End Sub

        Public Overrides Function ReturnInstance(typename As String) As Object

            Return New PCSAFT2PropertyPackage()

        End Function

        Private Function GetPRZ(Vx() As Double, T As Double, P As Double, tipo As String)

            Return pr.Z_PR(T, P, Vx, RET_VKij, RET_VTC, RET_VPC, RET_VW, tipo)

        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Sub DW_CalcProp([property] As String, phase As Phase)

            Dim result As Double = 0.0#
            Dim resultObj As Object = Nothing
            Dim phaseID As Integer = -1
            Dim state As String = "", pstate As State

            Dim T, P, MW As Double
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

            MW = Me.AUX_MMM(phase)

            Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = MW

            Dim pcs As New PCSAFT2(Me, RET_VMOL(phase))

            Select Case [property].ToLower
                Case "isothermalcompressibility", "bulkmodulus", "joulethomsoncoefficient", "speedofsound", "internalenergy", "gibbsenergy", "helmholtzenergy"
                    CalcAdditionalPhaseProperties(phaseID)
                Case "compressibilityfactor"
                    result = AUX_Z(RET_VMOL(phase), T, P, pstate)
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = result
                Case "heatcapacity", "heatcapacitycp"
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = DW_CalcCp_ISOL(phase, T, P)
                Case "heatcapacitycv"
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = DW_CalcCv_ISOL(phase, T, P)
                Case "enthalpy", "enthalpynf"
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = DW_CalcEnthalpy(RET_VMOL(phase), T, P, pstate)
                    result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result
                Case "entropy", "entropynf"
                    Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = DW_CalcEntropy(RET_VMOL(phase), T, P, pstate)
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
                        result = Me.AUX_LIQVISCm(T, P, phaseID)
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
                        result = LIQDENS(T, P, RET_VMOL(phase))
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

        Public Overrides Sub DW_CalcPhaseProps(Phase As Phase)

            Dim result As Double

            Dim dwpl As Phase, pstate As State

            Dim T, P, MW As Double
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
                    pstate = State.Vapor
                Case PropertyPackages.Phase.Liquid1
                    phaseID = 3
                    dwpl = PropertyPackages.Phase.Liquid1
                    pstate = State.Liquid
                Case PropertyPackages.Phase.Liquid2
                    phaseID = 4
                    dwpl = PropertyPackages.Phase.Liquid2
                    pstate = State.Liquid
                Case PropertyPackages.Phase.Liquid3
                    phaseID = 5
                    dwpl = PropertyPackages.Phase.Liquid3
                    pstate = State.Liquid
                Case PropertyPackages.Phase.Liquid
                    phaseID = 1
                    dwpl = PropertyPackages.Phase.Liquid
                    pstate = State.Liquid
                Case PropertyPackages.Phase.Aqueous
                    phaseID = 6
                    dwpl = PropertyPackages.Phase.Aqueous
                    pstate = State.Liquid
                Case PropertyPackages.Phase.Solid
                    phaseID = 7
                    dwpl = PropertyPackages.Phase.Solid
                    pstate = State.Solid
            End Select

            If phaseID > 0 Then
                overallmolarflow = Me.CurrentMaterialStream.Phases(0).Properties.molarflow.GetValueOrDefault
                phasemolarfrac = Me.CurrentMaterialStream.Phases(phaseID).Properties.molarfraction.GetValueOrDefault
                result = overallmolarflow * phasemolarfrac
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molarflow = result
                result = result * Me.AUX_MMM(Phase) / 1000
                Me.CurrentMaterialStream.Phases(phaseID).Properties.massflow = result
                If Me.CurrentMaterialStream.Phases(0).Properties.massflow.GetValueOrDefault > 0 Then
                    result = phasemolarfrac * overallmolarflow * Me.AUX_MMM(Phase) / 1000 / Me.CurrentMaterialStream.Phases(0).Properties.massflow.GetValueOrDefault
                Else
                    result = 0
                End If
                Me.CurrentMaterialStream.Phases(phaseID).Properties.massfraction = result
                Me.DW_CalcCompVolFlow(phaseID)
                Me.DW_CalcCompFugCoeff(Phase)
            End If

            If phaseID = 3 Or phaseID = 4 Or phaseID = 5 Or phaseID = 6 Then

                Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase))

                Dim Zest = GetPRZ(RET_VMOL(Phase), T, P, "L")

                MW = Me.AUX_MMM(Phase)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = MW

                result = LIQDENS(T, P, RET_VMOL(dwpl))

                Me.CurrentMaterialStream.Phases(phaseID).Properties.density = result

                Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = Me.DW_CalcEnthalpy(RET_VMOL(dwpl), T, P, State.Liquid)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = Me.DW_CalcEntropy(RET_VMOL(dwpl), T, P, State.Liquid)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = DW_CalcCp_ISOL(dwpl, T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = DW_CalcCv_ISOL(dwpl, T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = pcs.CalcZ(T, P, "liq", Zest)

                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault

                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result

                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropy = result

                result = Me.AUX_CONDTL(T, phaseID)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.thermalConductivity = result

                result = Me.AUX_LIQVISCm(T, P, phaseID)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.viscosity = result

                Me.CurrentMaterialStream.Phases(phaseID).Properties.kinematic_viscosity = result / Me.CurrentMaterialStream.Phases(phaseID).Properties.density.Value

            ElseIf phaseID = 2 Then

                Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase))

                Dim Zest = GetPRZ(RET_VMOL(Phase), T, P, "V")

                MW = Me.AUX_MMM(Phase)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = MW

                result = Me.AUX_VAPDENS(T, P)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.density = result

                Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = Me.DW_CalcEnthalpy(RET_VMOL(dwpl), T, P, State.Vapor)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = Me.DW_CalcEntropy(RET_VMOL(dwpl), T, P, State.Vapor)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = DW_CalcCp_ISOL(dwpl, T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = DW_CalcCv_ISOL(dwpl, T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = pcs.CalcZ(T, P, "gas", Zest)

                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result

                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropy = result

                result = Me.AUX_CONDTG(T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.thermalConductivity = result

                result = Me.AUX_VAPVISCm(T, Me.CurrentMaterialStream.Phases(phaseID).Properties.density, MW)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.viscosity = result
                Me.CurrentMaterialStream.Phases(phaseID).Properties.kinematic_viscosity = result / Me.CurrentMaterialStream.Phases(phaseID).Properties.density.Value

            ElseIf phaseID = 7 Then

                result = Me.AUX_SOLIDDENS
                Me.CurrentMaterialStream.Phases(phaseID).Properties.density = result

                Dim constprops As New List(Of Interfaces.ICompoundConstantProperties)
                For Each su As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(0).Compounds.Values
                    constprops.Add(su.ConstantProperties)
                Next

                Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy = Me.DW_CalcEnthalpy(RET_VMOL(dwpl), T, P, State.Solid)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy = Me.DW_CalcEntropy(RET_VMOL(dwpl), T, P, State.Solid)

                Me.CurrentMaterialStream.Phases(phaseID).Properties.compressibilityFactor = 0.0# 'result

                result = Me.DW_CalcSolidHeatCapacityCp(T, RET_VMOL(PropertyPackages.Phase.Solid), constprops)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCp = result
                Me.CurrentMaterialStream.Phases(phaseID).Properties.heatCapacityCv = result

                result = Me.AUX_MMM(Phase)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight = result

                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_enthalpy = result

                result = Me.CurrentMaterialStream.Phases(phaseID).Properties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Phases(phaseID).Properties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Phases(phaseID).Properties.molar_entropy = result

                result = Me.AUX_CONDTG(T, P)
                Me.CurrentMaterialStream.Phases(phaseID).Properties.thermalConductivity = 0.0# 'result
                Me.CurrentMaterialStream.Phases(phaseID).Properties.viscosity = 1.0E+20
                Me.CurrentMaterialStream.Phases(phaseID).Properties.kinematic_viscosity = 1.0E+20

            ElseIf phaseID = 1 Then

                DW_CalcLiqMixtureProps()

            Else

                DW_CalcOverallProps()

            End If

            If phaseID > 0 Then
                If Me.CurrentMaterialStream.Phases(phaseID).Properties.density.GetValueOrDefault > 0 And overallmolarflow > 0 Then
                    result = overallmolarflow * phasemolarfrac * Me.AUX_MMM(Phase) / 1000 / Me.CurrentMaterialStream.Phases(phaseID).Properties.density.GetValueOrDefault
                Else
                    result = 0
                End If
                Me.CurrentMaterialStream.Phases(phaseID).Properties.volumetric_flow = result
            End If

        End Sub

        Public Overrides Sub DW_CalcCompPartialVolume(phase As Phase, T As Double, P As Double)

            Dim pi As Integer = 0
            Select Case phase
                Case Phase.Liquid
                Case Phase.Aqueous
                    pi = 6
                Case Phase.Liquid1
                    pi = 3
                Case Phase.Liquid2
                    pi = 4
                Case Phase.Liquid3
                    pi = 5
                Case Phase.Vapor
                    Dim vapdens = AUX_VAPDENS(T, P)
                    For Each subst As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(2).Compounds.Values
                        subst.PartialVolume = subst.ConstantProperties.Molar_Weight / vapdens
                    Next
            End Select
            If pi <> 0 Then
                For Each subst As Interfaces.ICompound In Me.CurrentMaterialStream.Phases(pi).Compounds.Values
                    subst.PartialVolume = subst.ConstantProperties.Molar_Weight / AUX_LIQDENSi(subst, T)
                Next
            End If

        End Sub

        Public Overrides Function DW_CalcEnthalpy(Vx As Array, T As Double, P As Double, st As State) As Double

            If UseLeeKeslerEnthalpy AndAlso Not MixtureNeedsPCSAFTCaloric() Then
                Dim H As Double
                If st = State.Liquid Then
                    H = lk.H_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Hid(298.15, T, Vx))
                ElseIf st = State.Vapor Then
                    H = lk.H_LK_MIX("V", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Hid(298.15, T, Vx))
                ElseIf st = State.Solid Then
                    H = lk.H_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Hid(298.15, T, Vx)) - RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T)
                End If
                Return H
            Else
                Dim Hid = Me.RET_Hid(298.15, T, Vx)
                Return DW_CalcEnthalpyDeparture(Vx, T, P, st) + Hid
            End If

        End Function

        Public Overrides Function DW_CalcEnthalpyDeparture(Vx As Array, T As Double, P As Double, st As State) As Double

            Dim pcs As New PCSAFT2(Me, Vx)

            Dim H = pcs.CalcHr(T, P, If(st = State.Liquid, "liq", "gas"), GetPRZ(Vx, T, P, If(st = State.Liquid, "L", "V"))) / AUX_MMM(Vx)

            If st = State.Solid Then
                Return H - Me.RET_HFUSM(AUX_CONVERT_MOL_TO_MASS(Vx), T)
            Else
                Return H
            End If

        End Function

        Public Overrides Function DW_CalcEntropy(Vx As Array, T As Double, P As Double, st As State) As Double

            If UseLeeKeslerEnthalpy AndAlso Not MixtureNeedsPCSAFTCaloric() Then
                Dim S As Double
                If st = State.Liquid Then
                    S = lk.S_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Sid(298.15, T, P, Vx))
                ElseIf st = State.Vapor Then
                    S = lk.S_LK_MIX("V", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Sid(298.15, T, P, Vx))
                ElseIf st = State.Solid Then
                    S = lk.S_LK_MIX("L", T, P, Vx, RET_VKij(), RET_VTC, RET_VPC, RET_VW, RET_VMM, Me.RET_Sid(298.15, T, P, Vx)) - RET_HFUSM(Me.AUX_CONVERT_MOL_TO_MASS(Vx), T) / T
                End If
                Return S
            Else
                Dim Sid As Double = Me.RET_Sid(298.15, T, P, Vx)
                Return DW_CalcEntropyDeparture(Vx, T, P, st) + Sid
            End If

        End Function

        Public Overrides Function DW_CalcEntropyDeparture(Vx As Array, T As Double, P As Double, st As State) As Double

            Dim pcs As New PCSAFT2(Me, Vx)

            Dim Zest = GetPRZ(Vx, T, P, If(st = State.Liquid, "L", "V"))

            Dim S = pcs.CalcSr(T, P, If(st = State.Liquid, "liq", "gas"), Zest) / AUX_MMM(Vx)

            If st = State.Solid Then
                Return S - Me.RET_HFUSM(AUX_CONVERT_MOL_TO_MASS(Vx), T) / T
            Else
                Return S
            End If

        End Function

        Public Overrides Function DW_CalcFugCoeff(Vx As Array, T As Double, P As Double, st As State) As Double()

            If DirectCast(Vx, Double()).Sum = 0.0 Then Return RET_UnitaryVector()

            Dim pcs As New PCSAFT2(Me, Vx)

            Dim Zest = GetPRZ(Vx, T, P, If(st = State.Liquid, "L", "V"))

            Return pcs.CalcFugCoeff(T, P, If(st = State.Liquid, "liq", "gas"), Zest)

        End Function

        ''' <summary>
        ''' Log fugacity coefficients straight from the EoS, without the exponential that underflows to
        ''' zero for a high segment-number polymer. Keeps the true (large negative) chemical potential
        ''' for the stability test and phase-split estimates.
        ''' </summary>
        Public Overrides Function DW_CalcLnFugCoeff(Vx As Array, T As Double, P As Double, st As State) As Double()

            If DirectCast(Vx, Double()).Sum = 0.0 Then Return RET_NullVector()

            Dim pcs As New PCSAFT2(Me, Vx)

            Dim Zest = GetPRZ(Vx, T, P, If(st = State.Liquid, "L", "V"))

            Return pcs.CalcLnFugCoeff(T, P, If(st = State.Liquid, "liq", "gas"), Zest)

        End Function

        Public Overrides ReadOnly Property ImplementsAnalyticalDerivatives As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property UsesGibbsMinimizationForLLE As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Composition (mole-number) derivative of the log fugacity coefficients, d(lnphi_i)/dn_j at total
        ''' moles = 1. The density is solved ONCE at the base composition; the EoS is then evaluated in
        ''' closed form at that fixed density for each perturbation (no density solve per perturbation), and
        ''' the constant-pressure density response is added analytically:
        '''   d(lnphi_i)/dn_j = [d(lnphi_i)/dn_j]_rho + (d lnphi_i/d rho) * (d rho/dn_j),
        '''   d rho/dn_j = -[dP/dn_j]_rho / (dP/d rho).
        ''' This is what makes the liquid-liquid Gibbs-minimisation Newton step affordable for PC-SAFT.
        ''' </summary>
        Public Overrides Function DW_CalcdLnFugCoeffdn(Vx As Double(), T As Double, P As Double, st As State) As Double(,)

            Dim n As Integer = Vx.Length - 1
            Dim D(n, n) As Double

            Dim pcs As New PCSAFT2(Me, Vx)
            Dim phase As String = If(st = State.Liquid, "liq", "gas")
            Dim Zest As Double = GetPRZ(Vx, T, P, If(st = State.Liquid, "L", "V"))
            Dim Zstar As Double = pcs.CalcZ(T, P, phase, Zest)

            Dim kb As Double = 1.3806504E-23
            Dim densStar As Double = P / (Zstar * kb * T) / (10000000000.0) ^ 3

            ' Density response at fixed composition (central difference).
            Dim epsd As Double = densStar * 0.000001
            Dim Pp As Double = 0.0, Pm As Double = 0.0
            Dim lnfp = pcs.EvalAtDens(T, densStar + epsd, pcs.mix, Pp)
            Dim lnfm = pcs.EvalAtDens(T, densStar - epsd, pcs.mix, Pm)
            Dim dPdrho As Double = (Pp - Pm) / (2.0 * epsd)
            Dim dlnfdrho(n) As Double
            For i = 0 To n
                dlnfdrho(i) = (lnfp(i) - lnfm(i)) / (2.0 * epsd)
            Next

            ' Composition perturbations at fixed density (central where the mole fraction allows it),
            ' corrected to constant pressure. Central differencing matters where the change in one
            ' component's lnphi from another's mole number is tiny, e.g. the solvent's dependence on a
            ' trace polymer, which a one-sided difference resolves poorly.
            Dim delta As Double = 0.000001
            For j = 0 To n
                Dim useCentral As Boolean = Vx(j) > 2.0 * delta
                Dim nplus(n), nminus(n) As Double
                For k = 0 To n
                    nplus(k) = Vx(k) : nminus(k) = Vx(k)
                Next
                nplus(j) += delta
                Dim xplus = nplus.NormalizeY()
                pcs.SetComposition(xplus)
                Dim Pjp As Double = 0.0
                Dim lnfjp = pcs.EvalAtDens(T, densStar, pcs.mix, Pjp)

                Dim lnfjm As Double()
                Dim Pjm As Double = 0.0
                Dim h As Double
                If useCentral Then
                    nminus(j) -= delta
                    Dim xminus = nminus.NormalizeY()
                    pcs.SetComposition(xminus)
                    lnfjm = pcs.EvalAtDens(T, densStar, pcs.mix, Pjm)
                    h = 2.0 * delta
                Else
                    pcs.SetComposition(Vx)  ' base composition
                    lnfjm = pcs.EvalAtDens(T, densStar, pcs.mix, Pjm)
                    h = delta
                End If
                pcs.SetComposition(Vx)

                Dim dPdnj As Double = (Pjp - Pjm) / h
                Dim drhodnj As Double = If(dPdrho <> 0.0, -dPdnj / dPdrho, 0.0)
                For i = 0 To n
                    D(i, j) = (lnfjp(i) - lnfjm(i)) / h + dlnfdrho(i) * drhodnj
                Next
            Next

            Return D

        End Function

        Public Overrides Function SupportsComponent(comp As ICompoundConstantProperties) As Boolean

            Return True

        End Function

        Public Overrides Function DW_CalcMassaEspecifica_ISOL(Phase1 As Phase, T As Double, P As Double, Optional Pvp As Double = 0) As Double

            If Phase1 = Phase.Liquid Then
                ' Use the PC-SAFT equation-of-state density (physical for a polymer), not the Rackett
                ' correlation the base helper falls back to, matching DW_CalcProp and DW_CalcPhaseProps.
                Return Me.LIQDENS(T, P, RET_VMOL(Phase1))
            ElseIf Phase1 = Phase.Vapor Then
                Return Me.AUX_VAPDENS(T, P)
            Else
                Return Me.CurrentMaterialStream.Phases(1).Properties.volumetric_flow.GetValueOrDefault * Me.LIQDENS(T, P, RET_VMOL(Phase.Liquid)) / Me.CurrentMaterialStream.Phases(0).Properties.volumetric_flow.GetValueOrDefault + Me.CurrentMaterialStream.Phases(2).Properties.volumetric_flow.GetValueOrDefault * Me.AUX_VAPDENS(T, P) / Me.CurrentMaterialStream.Phases(0).Properties.volumetric_flow.GetValueOrDefault
            End If

        End Function

        Public Overrides Function DW_CalcViscosidadeDinamica_ISOL(Phase1 As Phase, T As Double, P As Double) As Double

            If Phase1 = Phase.Liquid Then
                Return Me.AUX_LIQVISCm(T, P)
            Else
                Return Me.AUX_VAPVISCm(T, Me.AUX_VAPDENS(T, P), Me.AUX_MMM(Phase.Vapor))
            End If

        End Function

        Public Overrides Function DW_CalcTensaoSuperficial_ISOL(Phase1 As Phase, T As Double, P As Double) As Double

            Return Me.AUX_SURFTM(T)

        End Function

        ''' <summary>
        ''' True when the given phase contains a polymer (a PC-SAFT compound whose segment number is scaled by
        ''' its molar mass, m_over_M > 0). The transport-property overrides below switch to mass-based mixing
        ''' only then, so every non-polymer mixture keeps the base class's behaviour exactly.
        ''' </summary>
        Private Function PhaseHasPolymer(phaseid As Integer) As Boolean
            For Each c In CurrentMaterialStream.Phases(phaseid).Compounds.Values
                If IsPolymer(c.ConstantProperties.CAS_Number) AndAlso c.MoleFraction.GetValueOrDefault > 0.0 Then
                    Return True
                End If
            Next
            Return False
        End Function

        Private Function IsPolymer(cas As String) As Boolean
            Return CompoundParameters.ContainsKey(cas) AndAlso CompoundParameters(cas).m_over_M > 0.0
        End Function

        Private Function IsAssociating(cas As String) As Boolean
            Return CompoundParameters.ContainsKey(cas) AndAlso
                   CompoundParameters(cas).kAiBi > 0.0 AndAlso CompoundParameters(cas).epsilon2 > 0.0
        End Function

        ' The Lee-Kesler caloric route uses Tc/Pc/omega corresponding states: it cannot represent the
        ' enthalpy of hydrogen bonding, and its critical constants are only placeholders for a polymer
        ' pseudo-compound. So whenever the mixture contains an associating compound or a polymer, the
        ' PC-SAFT departure (segment + association model) is used for H/S/Cp/Cv instead, regardless of the
        ' Use Lee-Kesler options. Checks the actual mixture compounds, not the parameter table (which always
        ' holds every built-in associating compound and polymer).
        Private Function MixtureNeedsPCSAFTCaloric() As Boolean
            Try
                For Each c In CurrentMaterialStream.Phases(0).Compounds.Values
                    Dim cas = c.ConstantProperties.CAS_Number
                    If IsPolymer(cas) OrElse IsAssociating(cas) Then Return True
                Next
            Catch
            End Try
            Try
                For Each c In Flowsheet.SelectedCompounds.Values
                    If IsPolymer(c.CAS_Number) OrElse IsAssociating(c.CAS_Number) Then Return True
                Next
            Catch
            End Try
            Return False
        End Function

        Private Shared Function NoUserViscosityData(cp As Interfaces.ICompoundConstantProperties) As Boolean
            If cp.LiquidViscosityEquation <> "" AndAlso cp.LiquidViscosityEquation <> "0" Then Return False
            Return cp.Liquid_Viscosity_Const_A = 0.0 AndAlso cp.Liquid_Viscosity_Const_B = 0.0 AndAlso
                   cp.Liquid_Viscosity_Const_C = 0.0 AndAlso cp.Liquid_Viscosity_Const_D = 0.0 AndAlso
                   cp.Liquid_Viscosity_Const_E = 0.0
        End Function

#Region "   Polymer transport-property estimates (used only with no user data)"

        ' Reference transport data for the built-in polymers, from the polymer literature (thermal conductivity
        ' and Tg from Van Krevelen, Properties of Polymers; surface tension at 20 C and its temperature slope
        ' from Wu, J. Phys. Chem. 74 (1970) 632). These are estimates: they let a polymer report a physical
        ' thermal conductivity and surface tension when the user has supplied no data, in place of the
        ' low-molecular-weight correlations that would use the polymer's placeholder critical constants.
        Private Structure PolymerTP
            Public lambda298 As Double  ' liquid thermal conductivity at 298 K, W/(m.K)
            Public Tg As Double         ' glass-transition temperature, K
            Public sigma293 As Double   ' surface tension at 293 K, N/m
            Public dsigmadT As Double   ' d(surface tension)/dT, N/(m.K), negative
            Public Sub New(l As Double, g As Double, s As Double, ds As Double)
                lambda298 = l : Tg = g : sigma293 = s : dsigmadT = ds
            End Sub
        End Structure

        Private Shared ReadOnly PolymerData As New Dictionary(Of String, PolymerTP) From {
            {"9002-88-4", New PolymerTP(0.46, 153.0, 0.0357, -0.000057)},    ' polyethylene HDPE
            {"9002-88-4-L", New PolymerTP(0.33, 148.0, 0.0353, -0.000056)},  ' polyethylene LDPE
            {"9003-07-0", New PolymerTP(0.19, 260.0, 0.0301, -0.000058)},    ' polypropylene
            {"9003-28-5", New PolymerTP(0.22, 249.0, 0.0336, -0.000058)},    ' polybutene
            {"9003-27-4", New PolymerTP(0.13, 200.0, 0.0336, -0.000064)},    ' polyisobutene
            {"9003-53-6", New PolymerTP(0.15, 373.0, 0.0407, -0.000072)},    ' polystyrene
            {"9003-20-7", New PolymerTP(0.159, 305.0, 0.0365, -0.000066)},   ' poly(vinyl acetate)
            {"63148-62-9", New PolymerTP(0.16, 150.0, 0.0197, -0.000048)},   ' polydimethylsiloxane
            {"9003-63-8", New PolymerTP(0.15, 293.0, 0.0310, -0.000059)},    ' poly(n-butyl methacrylate)
            {"9003-17-2", New PolymerTP(0.13, 178.0, 0.0325, -0.000060)},    ' polybutadiene
            {"25014-31-7", New PolymerTP(0.15, 441.0, 0.0400, -0.000070)},   ' poly(alpha-methylstyrene)
            {"9011-14-7", New PolymerTP(0.19, 378.0, 0.0410, -0.000076)},    ' poly(methyl methacrylate)
            {"9003-21-8", New PolymerTP(0.17, 281.0, 0.0410, -0.000070)},    ' poly(methyl acrylate)
            {"25322-68-3", New PolymerTP(0.20, 206.0, 0.0430, -0.000058)}    ' poly(ethylene glycol)
        }

        ' Typical amorphous polymer, for an injected polymer not in the table above.
        Private Shared ReadOnly PolymerDataDefault As New PolymerTP(0.20, 350.0, 0.0350, -0.000060)

        Private Shared Function PolymerRef(cas As String) As PolymerTP
            Dim p As PolymerTP = Nothing
            If PolymerData.TryGetValue(cas, p) Then Return p
            Return PolymerDataDefault
        End Function

        ' Van Krevelen's reduced thermal-conductivity curve for amorphous polymers: lambda rises weakly up to
        ' the glass transition (x = T/Tg <= 1) and falls roughly linearly above it.
        Private Shared Function VkCondShape(x As Double) As Double
            Return Math.Max(If(x <= 1.0, x ^ 0.22, 1.2 - 0.2 * x), 0.05)
        End Function

        Private Shared Function EstimatePolymerCondL(cas As String, T As Double) As Double
            Dim p As PolymerTP = PolymerRef(cas)
            Return p.lambda298 * VkCondShape(T / p.Tg) / VkCondShape(298.0 / p.Tg)
        End Function

        Private Shared Function EstimatePolymerSurfTens(cas As String, T As Double) As Double
            Dim p As PolymerTP = PolymerRef(cas)
            Return Math.Max(p.sigma293 + p.dsigmadT * (T - 293.15), 0.0001)
        End Function

#End Region

        ''' <summary>
        ''' Liquid viscosity of a phase that contains a polymer. A polymer's mole fraction is tiny, so the
        ''' base mole-average mixing nullifies its viscosity however large. Here each compound's pure viscosity
        ''' comes from AUX_LIQVISCi (the user-supplied liquid-viscosity equation when present) and they are
        ''' blended by a mass-fraction-weighted logarithm (an Arrhenius blend), so the polymer governs the
        ''' solution viscosity in proportion to its mass. With no polymer present the base mixing rule is used.
        ''' </summary>
        Public Overrides Function AUX_LIQVISCm(T As Double, P As Double, Optional phaseid As Integer = 3) As Double

            If Not PhaseHasPolymer(phaseid) Then Return MyBase.AUX_LIQVISCm(T, P, phaseid)

            Dim lnsum As Double = 0.0, wsum As Double = 0.0
            For Each c In CurrentMaterialStream.Phases(phaseid).Compounds.Values
                Dim w As Double = c.MassFraction.GetValueOrDefault
                If w <= 0.0 Then Continue For
                ' Polymer melt/solution viscosity is molar-mass and shear dependent, so there is no reliable
                ' estimate for it: a polymer with no supplied viscosity data is left out of the blend rather
                ' than filled in with the low-molecular-weight correlation's meaningless value.
                Dim cp = c.ConstantProperties
                If IsPolymer(cp.CAS_Number) AndAlso NoUserViscosityData(cp) Then Continue For
                Dim vi As Double = AUX_LIQVISCi(c.Name, T, P)
                If Double.IsNaN(vi) OrElse Double.IsInfinity(vi) OrElse vi <= 0.0 Then Continue For
                lnsum += w * Math.Log(vi)
                wsum += w
            Next

            If wsum <= 0.0 Then Return MyBase.AUX_LIQVISCm(T, P, phaseid)
            Return Math.Exp(lnsum / wsum)

        End Function

        ''' <summary>
        ''' Liquid thermal conductivity of a phase that contains a polymer. Each compound's value comes from
        ''' AUX_LIQTHERMCONDi (the user-supplied liquid thermal-conductivity equation when present), blended by
        ''' a mass-fraction average so the polymer contributes in proportion to its mass rather than its trace
        ''' mole fraction. Conductivities of solvent and polymer are of the same order, so a linear (not
        ''' logarithmic) average is appropriate. With no polymer present the base Li mixing rule is used.
        ''' </summary>
        Public Overrides Function AUX_CONDTL(T As Double, Optional phaseid As Integer = 3) As Double

            If Not PhaseHasPolymer(phaseid) Then Return MyBase.AUX_CONDTL(T, phaseid)

            Dim val As Double = 0.0, wsum As Double = 0.0
            For Each c In CurrentMaterialStream.Phases(phaseid).Compounds.Values
                Dim w As Double = c.MassFraction.GetValueOrDefault
                If w <= 0.0 Then Continue For
                Dim cp = c.ConstantProperties
                Dim ki As Double
                If IsPolymer(cp.CAS_Number) AndAlso (cp.LiquidThermalConductivityEquation = "" OrElse cp.LiquidThermalConductivityEquation = "0") Then
                    ki = EstimatePolymerCondL(cp.CAS_Number, T)   ' user gave no data: estimate rather than Latini
                Else
                    ki = AUX_LIQTHERMCONDi(cp, T)
                End If
                If Double.IsNaN(ki) OrElse Double.IsInfinity(ki) OrElse ki <= 0.0 Then Continue For
                val += w * ki
                wsum += w
            Next

            If wsum <= 0.0 Then Return MyBase.AUX_CONDTL(T, phaseid)
            Return val / wsum

        End Function

        ''' <summary>
        ''' Liquid surface tension of a phase that contains a polymer. Each compound's value comes from
        ''' AUX_SURFTi (the user-supplied surface-tension data when present), blended by a mass-fraction average
        ''' over the sub-critical compounds so the polymer is not nullified by its trace mole fraction. With no
        ''' polymer present the base molar average is used.
        ''' </summary>
        Public Overrides Function AUX_SURFTM(T As Double) As Double

            If Not PhaseHasPolymer(1) Then Return MyBase.AUX_SURFTM(T)

            Dim val As Double = 0.0, wsum As Double = 0.0
            For Each c In CurrentMaterialStream.Phases(1).Compounds.Values
                Dim cp = c.ConstantProperties
                Dim w As Double = c.MassFraction.GetValueOrDefault
                If w <= 0.0 Then Continue For
                Dim si As Double
                If IsPolymer(cp.CAS_Number) AndAlso (cp.SurfaceTensionEquation = "" OrElse cp.SurfaceTensionEquation = "0") Then
                    si = EstimatePolymerSurfTens(cp.CAS_Number, T)   ' user gave no data: estimate rather than Brock-Bird
                Else
                    If T / cp.Critical_Temperature >= 1.0 Then Continue For
                    si = AUX_SURFTi(cp, T)
                End If
                If Double.IsNaN(si) OrElse Double.IsInfinity(si) OrElse si <= 0.0 Then Continue For
                val += w * si
                wsum += w
            Next

            If wsum <= 0.0 Then Return MyBase.AUX_SURFTM(T)
            Return val / wsum

        End Function

        Public Overrides Function DW_CalcEnergyFlowMistura_ISOL(T As Double, P As Double) As Double

            Dim HM, HV, HL As Double

            HL = Me.DW_CalcEnthalpy(RET_VMOL(Phase.Liquid), T, P, State.Liquid)
            HV = Me.DW_CalcEnthalpy(RET_VMOL(Phase.Vapor), T, P, State.Vapor)
            HM = Me.CurrentMaterialStream.Phases(1).Properties.massfraction.GetValueOrDefault * HL + Me.CurrentMaterialStream.Phases(2).Properties.massfraction.GetValueOrDefault * HV

            Dim ent_massica = HM
            Dim flow = Me.CurrentMaterialStream.Phases(0).Properties.massflow
            Return ent_massica * flow

        End Function

        Public Overrides Function DW_CalcCp_ISOL(Phase1 As Phase, T As Double, P As Double) As Double

            If UseLeeKeslerCpCv AndAlso Not MixtureNeedsPCSAFTCaloric() Then
                Select Case Phase1
                    Case Phase.Vapor
                        Return lk.CpCvR_LK("V", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC, RET_VPC, RET_VCP(T), RET_VMM, RET_VW, RET_VZRa)(1)
                    Case Else
                        Return lk.CpCvR_LK("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC, RET_VPC, RET_VCP(T), RET_VMM, RET_VW, RET_VZRa)(1)
                End Select
            Else
                Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase1))
                Select Case Phase1
                    Case Phase.Vapor
                        Dim Zest = GetPRZ(RET_VMOL(Phase1), T, P, "V")
                        Dim Cp = pcs.CalcCp(T, P, "gas", Zest, Function(x) RET_Hid(298.15, x, RET_VMOL(Phase1)))
                        Return Cp
                    Case Else
                        Dim Zest = GetPRZ(RET_VMOL(Phase1), T, P, "L")
                        Dim Cp = pcs.CalcCp(T, P, "liq", Zest, Function(x) RET_Hid(298.15, x, RET_VMOL(Phase1)))
                        Return Cp
                End Select
            End If

        End Function

        Public Overrides Function DW_CalcCv_ISOL(Phase1 As Phase, T As Double, P As Double) As Double

            If UseLeeKeslerCpCv AndAlso Not MixtureNeedsPCSAFTCaloric() Then
                Select Case Phase1
                    Case Phase.Vapor
                        Return lk.CpCvR_LK("V", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC, RET_VPC, RET_VCP(T), RET_VMM, RET_VW, RET_VZRa)(2)
                    Case Else
                        Return lk.CpCvR_LK("L", T, P, RET_VMOL(Phase1), RET_VKij(), RET_VMAS(Phase1), RET_VTC, RET_VPC, RET_VCP(T), RET_VMM, RET_VW, RET_VZRa)(2)
                End Select
            Else
                Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase1))
                Select Case Phase1
                    Case Phase.Vapor
                        Dim Zest = GetPRZ(RET_VMOL(Phase1), T, P, "V")
                        Dim Cv = pcs.CalcCv(T, P, "gas", Zest, Function(x, y) RET_Sid(298.15, x, y, RET_VMOL(Phase1)))
                        Return Cv
                    Case Else
                        Dim Zest = GetPRZ(RET_VMOL(Phase1), T, P, "L")
                        Dim Cv = pcs.CalcCv(T, P, "liq", Zest, Function(x, y) RET_Sid(298.15, x, y, RET_VMOL(Phase1)))
                        Return Cv
                End Select
            End If

        End Function

        Public Overrides Function DW_CalcK_ISOL(Phase1 As Phase, T As Double, P As Double) As Double

            If Phase1 = Phase.Liquid Then
                Return Me.AUX_CONDTL(T)
            Else
                Return Me.AUX_CONDTG(T, P)
            End If

        End Function

        Public Overrides Function DW_CalcMM_ISOL(Phase1 As Phase, T As Double, P As Double) As Double

            Return Me.AUX_MMM(Phase1)

        End Function

        Public Overrides Function DW_CalcPVAP_ISOL(T As Double) As Double

            Return Auxiliary.PROPS.Pvp_leekesler(T, Me.RET_VTC(Phase.Liquid), Me.RET_VPC(Phase.Liquid), Me.RET_VW(Phase.Liquid))

        End Function

        Public Overrides Function AUX_Z(Vx() As Double, T As Double, P As Double, state As PhaseName) As Double

            Dim pcs As New PCSAFT2(Me, Vx)

            Dim Zest = GetPRZ(Vx, T, P, If(state = PhaseName.Vapor, "V", "L"))

            Return pcs.CalcZ(T, P, If(state = PhaseName.Vapor, "gas", "liq"), Zest)

        End Function

        Public Overrides Function AUX_VAPDENS(T As Double, P As Double) As Double

            Dim val As Double

            val = AUX_Z(RET_VMOL(Phase.Vapor), T, P, PhaseName.Vapor)

            val = (8.314 * val * T / P)
            val = 1 / val * Me.AUX_MMM(Phase.Vapor) / 1000

            Return val

        End Function

        Public Function LIQDENS(T As Double, P As Double, Vx() As Double) As Double

            Dim val As Double

            val = AUX_Z(Vx, T, P, PhaseName.Liquid)

            val = (8.314 * val * T / P)
            val = 1 / val * Me.AUX_MMM(Vx) / 1000

            Return val

        End Function

        Public Overrides Function CalcIsothermalCompressibility(p As IPhase) As Double

            Dim Z, P0, P1, T, Z1 As Double

            If Not p.Properties.molarfraction.HasValue Then Return 0.0

            T = CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault
            P0 = CurrentMaterialStream.Phases(0).Properties.pressure.GetValueOrDefault
            Z = p.Properties.compressibilityFactor.GetValueOrDefault

            P1 = P0 + 100

            Select Case p.Name
                Case "Mixture"
                    Return 0.0#
                Case "Vapor"
                    Z1 = AUX_Z(RET_VMOL(Phase.Vapor), T, P1, PhaseName.Vapor)
                Case "OverallLiquid"
                    Return 0.0#
                Case "Liquid1"
                    Z1 = AUX_Z(RET_VMOL(Phase.Liquid1), T, P1, PhaseName.Liquid)
                Case "Liquid2"
                    Z1 = AUX_Z(RET_VMOL(Phase.Liquid2), T, P1, PhaseName.Liquid)
                Case "Liquid3"
                    Z1 = AUX_Z(RET_VMOL(Phase.Liquid3), T, P1, PhaseName.Liquid)
                Case "Aqueous"
                    Z1 = AUX_Z(RET_VMOL(Phase.Aqueous), T, P1, PhaseName.Liquid)
                Case "Solid"
                    Return 0.0#
            End Select

            Dim K As Double = 1 / P0 - 1 / Z * (Z1 - Z) / 100

            If Double.IsNaN(K) Or Double.IsInfinity(K) Then K = 0.0#

            Return K

        End Function

        Public Overrides Function CalcSpeedOfSound(p As IPhase) As Double

            Dim K, rho As Double

            K = 1 / CalcIsothermalCompressibility(p)

            rho = p.Properties.density.GetValueOrDefault

            Return (K / rho) ^ 0.5

        End Function

        Public Overrides Function CalcJouleThomsonCoefficient(p As IPhase) As Double

            Return MyBase.CalcJouleThomsonCoefficient(p)

            'Dim Temperature, Pressure As Double
            '    Temperature = CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault
            '    Pressure = CurrentMaterialStream.Phases(0).Properties.temperature.GetValueOrDefault

            '    Select Case p.Name
            '        Case "Mixture"
            '            Return 0.0#
            '        Case "Vapor"
            '            If RET_VMOL(Phase.Vapor).Sum = 0.0 Then Return 0.0
            '            Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase.Vapor))
            '            Dim Zest = GetPRZ(RET_VMOL(Phase.Vapor), Temperature, Pressure, "V")
            '            Return pcs.CalcJT(Temperature, Pressure, "gas", Zest, p.Properties.heatCapacityCp.GetValueOrDefault).JT
            '        Case "OverallLiquid"
            '            Return 0.0#
            '        Case "Liquid1"
            '            If RET_VMOL(Phase.Liquid1).Sum = 0.0 Then Return 0.0
            '            Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase.Liquid1))
            '            Dim Zest = GetPRZ(RET_VMOL(Phase.Liquid1), Temperature, Pressure, "L")
            '            Return pcs.CalcJT(Temperature, Pressure, "liq", Zest, p.Properties.heatCapacityCp.GetValueOrDefault).JT
            '        Case "Liquid2"
            '            If RET_VMOL(Phase.Liquid2).Sum = 0.0 Then Return 0.0
            '            Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase.Liquid2))
            '            Dim Zest = GetPRZ(RET_VMOL(Phase.Liquid2), Temperature, Pressure, "L")
            '            Return pcs.CalcJT(Temperature, Pressure, "liq", Zest, p.Properties.heatCapacityCp.GetValueOrDefault).JT
            '        Case "Liquid3"
            '            If RET_VMOL(Phase.Liquid3).Sum = 0.0 Then Return 0.0
            '            Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase.Liquid3))
            '            Dim Zest = GetPRZ(RET_VMOL(Phase.Liquid3), Temperature, Pressure, "L")
            '            Return pcs.CalcJT(Temperature, Pressure, "liq", Zest, p.Properties.heatCapacityCp.GetValueOrDefault).JT
            '        Case "Aqueous"
            '            If RET_VMOL(Phase.Aqueous).Sum = 0.0 Then Return 0.0
            '            Dim pcs As New PCSAFT2(Me, RET_VMOL(Phase.Aqueous))
            '            Dim Zest = GetPRZ(RET_VMOL(Phase.Aqueous), Temperature, Pressure, "L")
            '            Return pcs.CalcJT(Temperature, Pressure, "liq", Zest, p.Properties.heatCapacityCp.GetValueOrDefault).JT
            '        Case "Solid"
            '            Return 0.0#
            '    End Select
            '    Return 0.0#

        End Function

        Public Overrides Function SaveData() As List(Of System.Xml.Linq.XElement)

            Dim data = MyBase.SaveData

            data.Add(New XElement("UseLeeKeslerEnthalpy", UseLeeKeslerEnthalpy))
            data.Add(New XElement("UseLeeKeslerCpCv", UseLeeKeslerCpCv))

            Dim casnos = New List(Of String)
            If (Not (Me.CurrentMaterialStream) Is Nothing) Then
                casnos = Me.CurrentMaterialStream.Phases(0).Compounds.Values.Select(Function(x) x.ConstantProperties.CAS_Number).ToList
            End If

            Dim ci As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture
            data.Add(New XElement("InteractionParameters"))
            For Each kvp As KeyValuePair(Of String, Dictionary(Of String, PCSIP)) In InteractionParameters
                For Each kvp2 As KeyValuePair(Of String, PCSIP) In kvp.Value
                    If (Not (Me.CurrentMaterialStream) Is Nothing) Then
                        If (casnos.Contains(kvp.Key) And casnos.Contains(kvp2.Key)) Then
                            data((data.Count - 1)).Add(New XElement("InteractionParameter", New XAttribute("Compound1", kvp2.Value.compound1), New XAttribute("Compound2", kvp2.Value.compound2), New XAttribute("CAS1", kvp.Key), New XAttribute("CAS2", kvp2.Key), New XAttribute("Value", kvp2.Value.kij.ToString(ci))))
                        End If
                    End If
                Next
            Next

            data.Add(New XElement("CompoundParameters"))
            For Each kvp As KeyValuePair(Of String, PCSParam) In CompoundParameters
                If (Not (Me.CurrentMaterialStream) Is Nothing) Then
                    If casnos.Contains(kvp.Key) Then
                        data((data.Count - 1)).Add(New XElement("CompoundParameterSet", New XAttribute("Compound", kvp.Value.compound), New XAttribute("CAS_ID", kvp.Value.casno), New XAttribute("MW", kvp.Value.mw.ToString(ci)), New XAttribute("m", kvp.Value.m.ToString(ci)), New XAttribute("sigma", kvp.Value.sigma.ToString(ci)), New XAttribute("epsilon_k", kvp.Value.epsilon.ToString(ci)), New XAttribute("assocparam", kvp.Value.associationparams.Replace(System.Environment.NewLine, "|"))))
                    End If
                End If
            Next

            Return data

        End Function

        Public Overrides Function LoadData(ByVal data As List(Of System.Xml.Linq.XElement)) As Boolean

            MyBase.LoadData(data)

            Dim ci As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture

            Try
                UseLeeKeslerEnthalpy = (From el As XElement In data Select el Where el.Name = "UseLeeKeslerEnthalpy").FirstOrDefault.Value
            Catch ex As Exception
            End Try

            Try
                UseLeeKeslerCpCv = (From el As XElement In data Select el Where el.Name = "UseLeeKeslerCpCv").FirstOrDefault.Value
            Catch ex As Exception
            End Try

            For Each xel As XElement In (From xel2 In data Where xel2.Name = "InteractionParameters" Select xel2).SingleOrDefault().Elements().ToList()

                Dim ip As PCSIP = New PCSIP()
                With ip
                    .compound1 = xel.Attribute("Compound1").Value
                    .compound2 = xel.Attribute("Compound2").Value
                    .casno1 = xel.Attribute("CAS1").Value
                    .casno2 = xel.Attribute("CAS2").Value
                    .kij = Double.Parse(xel.Attribute("Value").Value, ci)
                End With

                Dim dic As Dictionary(Of String, PCSIP) = New Dictionary(Of String, PCSIP)
                dic.Add(xel.Attribute("CAS1").Value, ip)

                If Not Me.InteractionParameters.ContainsKey(xel.Attribute("CAS1").Value) Then
                    Me.InteractionParameters.Add(xel.Attribute("CAS1").Value, dic)
                ElseIf Not Me.InteractionParameters(xel.Attribute("CAS1").Value).ContainsKey(xel.Attribute("CAS2").Value) Then
                    Me.InteractionParameters(xel.Attribute("CAS1").Value).Add(xel.Attribute("CAS2").Value, ip)
                Else
                    Me.InteractionParameters(xel.Attribute("CAS1").Value)(xel.Attribute("CAS2").Value) = ip
                End If

            Next

            For Each xel As XElement In (From xel2 In data Where xel2.Name = "CompoundParameters" Select xel2).SingleOrDefault().Elements().ToList()

                Dim param As PCSParam = New PCSParam()
                With param
                    .compound = xel.Attribute("Compound").Value
                    .casno = xel.Attribute("CAS_ID").Value
                    .mw = Double.Parse(xel.Attribute("MW").Value, ci)
                    .m = Double.Parse(xel.Attribute("m").Value, ci)
                    .sigma = Double.Parse(xel.Attribute("sigma").Value, ci)
                    .epsilon = Double.Parse(xel.Attribute("epsilon_k").Value, ci)
                    .associationparams = xel.Attribute("assocparam").Value.Replace("|", System.Environment.NewLine)
                End With

                If Not Me.CompoundParameters.ContainsKey(xel.Attribute("CAS_ID").Value) Then
                    Me.CompoundParameters.Add(xel.Attribute("CAS_ID").Value, param)
                Else
                    Me.CompoundParameters(xel.Attribute("CAS_ID").Value) = param
                End If

            Next

            Return True

        End Function

    End Class

End Namespace