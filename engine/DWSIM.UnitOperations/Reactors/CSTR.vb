'    CSTR Calculation Routines 
'    Copyright 2008-2016 Daniel Wagner O. de Medeiros; Gregor Reichert
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


Imports DWSIM.Thermodynamics.BaseClasses
Imports Flee.PublicTypes
Imports System.Math
Imports System.Linq
Imports DWSIM.Interfaces.Enums
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics
Imports DWSIM.MathOps
Imports DWSIM.UnitOperations.Streams

Namespace Reactors

    ''' <summary>
    ''' Represents a Continuous Stirred-Tank Reactor (CSTR) that models a well-mixed reactor
    ''' vessel where reaction kinetics are evaluated at the outlet composition. Supports
    ''' conversion, kinetic, and heterogeneous catalytic reactions with optional two-phase
    ''' (liquid + vapour) outlet operation.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_CSTR

        Inherits Reactor

        ''' <summary>Defines the outlet configuration of the CSTR.</summary>
        Public Enum EReactorMode
            ''' <summary>Single mixed outlet stream.</summary>
            SingleOutlet
            ''' <summary>Separate liquid and vapour outlet streams.</summary>
            TwoOutlets
        End Enum

        Protected m_vol As Double = 1.0
        Protected m_headspace As Double
        Protected m_isotemp As Double

        Dim C0 As Dictionary(Of String, Double)
        Dim C As Dictionary(Of String, Double)
        Dim Ri As Dictionary(Of String, Double)
        Dim Kf, Kr As ArrayList
        Dim N00, N0 As Dictionary(Of String, Double)
        Dim Rxi As New Dictionary(Of String, Double)
        Public RxiT As New Dictionary(Of String, Double)
        Public DHRi As New Dictionary(Of String, Double)

        Dim activeAL As Integer = 0

        ''' <summary>Gets the list of equipment sub-types available for this reactor.</summary>
        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Stirred Tank"}
            End Get
        End Property

        ''' <summary>Creates the dimensions list (Volume) for this CSTR.</summary>
        Public Overrides Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Volume, .IsUserDefined = False})

        End Sub

        ''' <summary>Updates the dimension values from the current reactor volume.</summary>
        Public Overrides Sub UpdateDimensionsList()

            Dimensions(0).Value = Volume

        End Sub

        ''' <summary>Gets a value indicating whether this reactor supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this reactor exposes dedicated dynamic-mode properties.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = True


        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets or sets the liquid/solid residence time (s).</summary>
        Public Property ResidenceTimeL As Double = 0.0#

        ''' <summary>Gets or sets the vapour residence time (s).</summary>
        Public Property ResidenceTimeV As Double = 0.0#

        ''' <summary>Gets or sets the reactor diameter (m).</summary>
        Public Property Diameter As Double = 0.1 ' m

        ''' <summary>Gets or sets the impeller diameter (m) for internal HTC calculation.</summary>
        Public Property ImpellerDiameter As Double = 0.05

        ''' <summary>Gets or sets the impeller speed (RPM) for internal HTC calculation.</summary>
        Public Property ImpellerSpeed As Double = 100.0

        ''' <summary>Gets or sets the impeller type for stirred-vessel internal HTC calculation.</summary>
        Public Property Impeller As ImpellerType = ImpellerType.FlatBlade

        ''' <summary>Gets or sets the convergence tolerance for the CSTR iteration loop.</summary>
        Public Property Tolerance As Double = 0.00001

        ''' <summary>Gets or sets the maximum number of iterations for the CSTR solver.</summary>
        Public Property MaxIterations As Integer = 1000

        ''' <summary>Gets or sets the initial number of time sub-steps used in the CSTR transient solver.</summary>
        Public Property InitialNumberOfTimeSteps As Integer = 10

        ''' <summary>Gets or sets the isothermal temperature (K) used when the reactor operates in isothermal mode.</summary>
        Public Property IsothermalTemperature() As Double
            Get
                Return m_isotemp
            End Get
            Set(ByVal value As Double)
                m_isotemp = value
            End Set
        End Property

        ''' <summary>Gets or sets the reactor vessel volume (mï¿½).</summary>
        Public Property Volume() As Double
            Get
                Return m_vol
            End Get
            Set(ByVal value As Double)
                m_vol = value
            End Set
        End Property

        ''' <summary>Gets or sets the reactor headspace volume fraction (0ï¿½1).</summary>
        Public Property Headspace() As Double
            Get
                Return m_headspace
            End Get
            Set(ByVal value As Double)
                m_headspace = value
            End Set
        End Property

        ''' <summary>Gets or sets the mass of catalyst loaded in the reactor (kg).</summary>
        Public Property CatalystAmount As Double = 0.0#

        ''' <summary>Initializes a new default instance of the <see cref="Reactor_CSTR"/> class.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Reactor_CSTR"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the CSTR.</param>
        ''' <param name="description">A brief description of the CSTR.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description

            N00 = New Dictionary(Of String, Double)
            N0 = New Dictionary(Of String, Double)
            C0 = New Dictionary(Of String, Double)
            C = New Dictionary(Of String, Double)
            Ri = New Dictionary(Of String, Double)
            Rxi = New Dictionary(Of String, Double)
            DHRi = New Dictionary(Of String, Double)
            Kf = New ArrayList
            Kr = New ArrayList

        End Sub

        ''' <summary>Creates a deep copy of this reactor via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_CSTR()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this reactor via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_CSTR)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Registers the dynamic properties for dynamic simulation mode.</summary>
        Public Overrides Sub CreateDynamicProperties()

            AddDynamicProperty("Operating Pressure", "Current Operating Pressure", 0, UnitOfMeasure.pressure, 1.0.GetType())
            AddDynamicProperty("Liquid Level", "Current Liquid Level", 0, UnitOfMeasure.distance, 1.0.GetType())
            AddDynamicProperty("Height", "Available Height for Liquid", 1, UnitOfMeasure.distance, 1.0.GetType())
            AddDynamicProperty("Minimum Pressure", "Minimum Dynamic Pressure for this Reactor.", 101325, UnitOfMeasure.pressure, 1.0.GetType())
            AddDynamicProperty("Initialize using Inlet Stream", "Initializes the CSTR contents with information from the inlet stream.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Reset Contents", "Empties the CSTR's content on the next run.", False, UnitOfMeasure.none, True.GetType())

        End Sub

        Private prevM, currentM As Double

        ''' <summary>Executes one dynamic simulation time step for the CSTR.</summary>
        Public Overrides Sub RunDynamicModel()

            Select Case Me.ReactorOperationMode

                Case OperationMode.OutletTemperature, OperationMode.Isothermic

                    Throw New Exception("This calculation mode is not supported while in Dynamic Mode.")

            End Select

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds

            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0

            Dim ims1 As MaterialStream = GetInletMaterialStream(0)

            Dim oms1 As MaterialStream = GetOutletMaterialStream(0)
            Dim oms2 As MaterialStream = GetOutletMaterialStream(1)

            Dim es As EnergyStream = GetInletEnergyStream(1)

            Dim Height As Double = GetDynamicProperty("Height")
            Dim Pressure As Double
            Dim Pmin = GetDynamicProperty("Minimum Pressure")
            Dim InitializeFromInlet As Boolean = GetDynamicProperty("Initialize using Inlet Stream")

            Dim Reset As Boolean = GetDynamicProperty("Reset Contents")

            If Reset Then
                AccumulationStream = Nothing
                SetDynamicProperty("Reset Contents", 0)
            End If

            If AccumulationStream Is Nothing Then

                If InitializeFromInlet Then

                    AccumulationStream = ims1.CloneXML

                Else

                    AccumulationStream = ims1.Subtract(oms1, timestep)
                    If oms2 IsNot Nothing Then AccumulationStream = AccumulationStream.Subtract(oms2, timestep)

                End If

                Dim density = AccumulationStream.Phases(0).Properties.density.GetValueOrDefault

                AccumulationStream.SetMassFlow(density * Volume)
                AccumulationStream.SpecType = StreamSpec.Temperature_and_Pressure
                AccumulationStream.PropertyPackage = PropertyPackage
                AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream
                AccumulationStream.Calculate()

            Else

                AccumulationStream.SetFlowsheet(FlowSheet)
                AccumulationStream = AccumulationStream.Add(ims1, timestep)
                AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream
                AccumulationStream.Calculate()
                AccumulationStream = AccumulationStream.Subtract(oms1, timestep)
                If oms2 IsNot Nothing Then AccumulationStream = AccumulationStream.Subtract(oms2, timestep)
                If AccumulationStream.GetMassFlow <= 0.0 Then AccumulationStream.SetMassFlow(0.0)

            End If

            AccumulationStream.SetFlowsheet(FlowSheet)

            ' Calculate Temperature

            Dim Qval, Ha, Wa As Double

            Ha = AccumulationStream.GetMassEnthalpy
            Wa = AccumulationStream.GetMassFlow

            If es IsNot Nothing Then Qval = es.EnergyFlow.GetValueOrDefault

            If Qval <> 0.0 Then

                If Wa > 0 Then

                    AccumulationStream.SetMassEnthalpy(Ha + Qval * timestep / Wa)

                    AccumulationStream.SpecType = StreamSpec.Pressure_and_Enthalpy

                    AccumulationStream.PropertyPackage = PropertyPackage
                    AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream

                    If integrator.ShouldCalculateEquilibrium Then

                        AccumulationStream.Calculate(True, True)

                    End If

                End If

            End If

            'calculate pressure

            Dim M = AccumulationStream.GetMolarFlow()

            Dim Temperature = AccumulationStream.GetTemperature

            Pressure = AccumulationStream.GetPressure

            'm3/mol

            prevM = currentM

            currentM = Volume / M

            PropertyPackage.CurrentMaterialStream = AccumulationStream

            Dim LiquidVolume, RelativeLevel As Double

            If AccumulationStream.GetPressure > Pmin Then

                If prevM = 0.0 Or integrator.ShouldCalculateEquilibrium Then

                    Dim result As IFlashCalculationResult

                    result = PropertyPackage.CalculateEquilibrium2(FlashCalculationType.VolumeTemperature, currentM, Temperature, Pressure)

                    Pressure = result.CalculatedPressure

                    LiquidVolume = AccumulationStream.Phases(3).Properties.volumetric_flow.GetValueOrDefault

                    RelativeLevel = LiquidVolume / Volume

                    SetDynamicProperty("Liquid Level", RelativeLevel * Height)

                Else

                    Pressure = currentM / prevM * Pressure

                End If

            Else

                Pressure = Pmin

                LiquidVolume = 0.0

                RelativeLevel = LiquidVolume / Volume

                SetDynamicProperty("Liquid Level", RelativeLevel * Height)

            End If

            AccumulationStream.SetPressure(Pressure)
            AccumulationStream.SpecType = StreamSpec.Temperature_and_Pressure

            AccumulationStream.PropertyPackage = PropertyPackage
            AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream

            If integrator.ShouldCalculateEquilibrium And Pressure > 0.0 Then

                AccumulationStream.Calculate(True, True)

            End If

            SetDynamicProperty("Operating Pressure", Pressure)

            Calculate(True)

            OutletTemperature = AccumulationStream.GetTemperature()

            DeltaT = OutletTemperature - ims1.GetTemperature()

            DeltaP = AccumulationStream.GetPressure() - ims1.GetPressure()

            DeltaQ = (AccumulationStream.GetMassEnthalpy() - ims1.GetMassEnthalpy()) * ims1.GetMassFlow()

            ' comp. conversions

            For Each sb As Compound In ims1.Phases(0).Compounds.Values
                If ComponentConversions.ContainsKey(sb.Name) > 0 Then
                    Dim n0 = ims1.Phases(0).Compounds(sb.Name).MolarFlow.GetValueOrDefault()
                    Dim nf = AccumulationStream.Phases(0).Compounds(sb.Name).MolarFlow.GetValueOrDefault()
                    ComponentConversions(sb.Name) = Abs(n0 - nf) / nf
                End If
            Next

        End Sub

        ''' <summary>
        ''' Performs the CSTR calculation, solving the mass and energy balances at the outlet
        ''' composition using the selected calculation model.
        ''' </summary>
        ''' <param name="args">Optional. If <c>True</c>, indicates a dynamic-mode call.</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Calculate_Internal_1(args)

        End Sub


        Public Sub Calculate_Internal_1(Optional ByVal args As Object = Nothing)

            Dim ims As MaterialStream

            Dim dynamics As Boolean = False

            If args IsNot Nothing Then dynamics = args

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("To run a simulation of a reactor, the user needs to define the chemical reactions which will take place in the reactor.</p>
                                This Is done through the&nbsp;<span style='font-weight bold;'>Reactions Manager, </span>accessible through <span style='font-weight: bold;'>Simulation Settings &gt; Basis &gt; Open Chemical Reactions Manager</span> or <span style='font-weight: bold;'>Tools &gt; Reactions Manager</span> menus (see separate documentation).<br><br>Reactions can be of&nbsp;<span style='font-weight: bold;'>Equilibrium</span>,<span style='font-weight: bold;'>&nbsp;Conversion</span>,<span style='font-weight: bold;'>&nbsp;Kinetic</span> or&nbsp;<span style='font-weight: bold;'>Heterogeneous Catalytic</span> types. One or more reactions can be&nbsp;combined to define
                                            a&nbsp;<span style='font-weight bold;'>Reaction Set</span>. The reactors then 'see' the reactions through the reaction sets.
                                <br><br><span style ='font-weight bold; font-style: italic;'>Equilibrium</span>
                                Reactions are defined by an equilibrium constant (K). The source Of
                                Information for the equilibrium constant can be a direct gibbs energy
                                calculation, an expression defined by the user Or a constant value.
                                Equilibrium Reactions can be used in Equilibrium And Gibbs reactors.<br><br><span style='font-weight bold; font-style: italic;'>Conversion</span>
                                            Reactions are defined by the amount of a base compound which Is
                                consumed in the reaction. This amount can be a fixed value Or a
                                Function of() the system temperature. Conversion reactions are supported
                                by the Conversion reactor.<br><br><span style='font-weight bold; font-style: italic;'>Kinetic</span> reactions are reactions defined by a kinetic expression. These reactions are supported by the PFR and CSTR reactors. <br><br><span style='font-weight: bold; font-style: italic;'>Heterogeneous Catalytic</span> reactions&nbsp;in DWSIM must obey the <span style='font-style: italic;'>Langmuir&#8211;Hinshelwood</span> 
                                            mechanism, where compounds react over a solid catalyst surface. In this 
                                model, Reaction rates are a function of catalyst amount (i.e. mol/kg 
                                cat.s). These Reactions are supported by the PFR And CSTR reactors.<p>")

            'ims-stream (internal material stream) to be used during internal calculations

            'Clone inlet stream as initial estimation

            ims = DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.DefinedFlow = FlowSpec.Mass

            If dynamics Then ims = AccumulationStream

            PropertyPackage.CurrentMaterialStream = ims

            ims.SetPropertyPackage(PropertyPackage)
            ims.SetFlowsheet(Me.FlowSheet)

            ims.PreferredFlashAlgorithmTag = Me.PreferredFlashAlgorithmTag

            N0 = New Dictionary(Of String, Double) 'inlet mole flows
            C0 = New Dictionary(Of String, Double) 'inlet mole concentrations
            C = New Dictionary(Of String, Double) 'current mole concentrations
            Ri = New Dictionary(Of String, Double) 'compound consumption rates

            DHRi = New Dictionary(Of String, Double) 'reaction heats
            Kf = New ArrayList 'K forward reaction
            Kr = New ArrayList 'K reverse reaction
            Rxi = New Dictionary(Of String, Double)
            Dim BC As String = "" 'Base Component of Reaction
            Dim ErrCode As String

            'conversion factors for different basis other than molar concentrations
            Dim convfactors As New Dictionary(Of String, Double)

            'scBC = stoichiometric coeff of the base comp
            'DHr = reaction heat (total)
            'Hr = reaction enthalpy
            'Hr0 = initial reactant enthalpy
            'Hp = products enthalpy
            Dim scBC, DHr, Hr, Hr0, Hp, T, T0, P, P0, W, Q, QL, QS, QV, Qr, Rx, IErr, OErr As Double
            Dim ReactorMode As EReactorMode = EReactorMode.SingleOutlet
            Dim dT, MaxChange As Double 'Time step in seconds; MaxChange = max relative change of a component
            Dim i, NIter As Integer
            Dim NC As Integer = ims.Phases(0).Compounds.Count 'Number of components
            Dim CompNames(NC - 1) As String 'ComponentNames
            Dim MM(NC - 1) As Double 'Mol masses of components
            Dim LC(NC - 1) As Double 'Composition of last iteration
            Dim Nin(NC - 1), Nout(NC - 1), NReac(NC - 1), Mout(NC - 1), RComp(NC - 1), dB(NC - 1), dN(NC - 1), dRT(NC - 1), Y(NC - 1), M(NC - 1) As Double
            Dim TR(NC - 1) As Double

            m_conversions = New Dictionary(Of String, Double) 'Conversion of reactions
            m_componentconversions = New Dictionary(Of String, Double) 'Conversion of components

            Dim conv As New SystemsOfUnits.Converter
            Dim rxn As Reaction

            'Check streams beeing attached
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Nohcorrentedematriac16"))
            ElseIf Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Nohcorrentedematriac15"))
            ElseIf Not Me.GraphicObject.InputConnectors(1).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Nohcorrentedeenerg17"))
            End If

            'Check Reaction type, Base components and reaction volume
            If FlowSheet.ReactionSets(Me.ReactionSetID).Reactions.Count = 0 Then Throw New Exception("No reaction defined")
            ErrCode = "No kinetic Or catalytic reaction found"
            For Each rxnsb As ReactionSetBase In FlowSheet.ReactionSets(Me.ReactionSetID).Reactions.Values
                rxn = FlowSheet.Reactions(rxnsb.ReactionID)
                If Not rxn.Components.ContainsKey(rxn.BaseReactant) Then
                    Throw New Exception("No base reactant defined for reaction '" + rxn.Name + "'.")
                End If
                If (rxn.ReactionType = ReactionType.Kinetic Or rxn.ReactionType = ReactionType.Heterogeneous_Catalytic) And rxnsb.IsActive Then
                    BC = rxn.BaseReactant
                    If BC = "" Then
                        ErrCode = "No Base Component defined for reaction " & rxn.Name
                        Exit For
                    End If

                    'Check if reaction has a volume 
                    If (rxn.ReactionPhase = PhaseName.Liquid And Volume <= 0) Then
                        ErrCode = "No reactor volume defined"
                    ElseIf (rxn.ReactionPhase = PhaseName.Vapor And Headspace <= 0) Then
                        ErrCode = "No reactor headspace defined"
                    ElseIf (rxn.ReactionPhase = PhaseName.Mixture And Volume + Headspace <= 0) Then
                        ErrCode = "No reactor volume and headspace defined"
                    Else
                        ErrCode = ""
                    End If
                    Exit For
                End If
            Next
            If ErrCode <> "" Then Throw New Exception(ErrCode)

            'Check reactor mode.
            'Mode 0 (without vapour outlet):    Complete output is leaving through Outlet Stream 1. Phase volumes for reactions depend on phase volumetric fractions.
            '                                   Residence time liquid (and vapour) is Volume (without head space) divided by total volume flow.
            'Mode 1 (with vapour outlet):       Vapour phase reactions take place in headspace volume only. Reactor volume is volume of liquid+solid phases
            '                                   Residence time is calculated for reactor volume and headspace separately
            If Me.GraphicObject.OutputConnectors(1).IsAttached Then ReactorMode = EReactorMode.TwoOutlets

            'Initialisations
            Q = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault 'Mixture
            QV = ims.Phases(2).Properties.volumetric_flow.GetValueOrDefault 'Vapour
            QL = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault 'Liquid
            QS = ims.Phases(7).Properties.volumetric_flow.GetValueOrDefault 'Solid

            If QL + QS > 0 Then
                ResidenceTimeL = Volume / (QL + QS)
            Else
                ResidenceTimeL = 0
            End If
            If ReactorMode = EReactorMode.TwoOutlets And QV > 0 Then
                ResidenceTimeV = Headspace / QV
            Else
                ResidenceTimeV = ResidenceTimeL
            End If

            dT = ResidenceTimeL / 10 'initial time step

            If dynamics Then
                Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
                Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)
                Dim timestep = integrator.IntegrationStep.TotalSeconds
                If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0
                dT = timestep
            End If

            CompNames = ims.PropertyPackage.RET_VNAMES 'Component names
            MM = ims.PropertyPackage.RET_VMM 'Component molar weights
            LC = ims.PropertyPackage.RET_VMOL(PropertyPackages.Phase.Mixture) 'Component molar fractions

            W = ims.Phases(0).Properties.massflow.GetValueOrDefault

            'Calculate initial inventory of components in reactor
            For i = 0 To NC - 1
                Nin(i) = ims.Phases(0).Compounds(CompNames(i)).MolarFlow
                Nout(i) = Nin(i)
                If ReactorMode = EReactorMode.SingleOutlet Then
                    NReac(i) = Me.Volume * ims.Phases(0).Compounds(CompNames(i)).MolarFlow.GetValueOrDefault() *
                        ims.Phases(0).Properties.density.GetValueOrDefault / W / 1000.0 'global composition; headspace is ignored
                Else
                    NReac(i) = Me.Headspace * ims.Phases(0).Compounds(CompNames(i)).MolarFlow.GetValueOrDefault() *
                        ims.Phases(0).Properties.density.GetValueOrDefault / W / 1000.0 'vapour in headspace
                    If QL + QS > 0 Then NReac(i) += Volume * QL / (QL + QS) * ims.Phases(0).Compounds(CompNames(i)).MolarFlow.GetValueOrDefault() *
                        ims.Phases(0).Properties.density.GetValueOrDefault / W / 1000.0 'liqud phase
                    If QL + QS > 0 Then NReac(i) += Volume * QS / (QL + QS) * ims.Phases(0).Compounds(CompNames(i)).MolarFlow.GetValueOrDefault() *
                        ims.Phases(0).Properties.density.GetValueOrDefault / W / 1000.0 'liqud phase
                End If
            Next

            P0 = ims.Phases(0).Properties.pressure.GetValueOrDefault
            P = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            T0 = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Select Case Me.ReactorOperationMode
                Case OperationMode.Isothermic, OperationMode.Adiabatic
                    T = ims.Phases(0).Properties.temperature.GetValueOrDefault
                Case OperationMode.OutletTemperature
                    T = Me.OutletTemperature
                Case OperationMode.HeatExchange
                    T = ims.Phases(0).Properties.temperature.GetValueOrDefault
            End Select

            ' Heat exchange area for CSTR
            If ReactorOperationMode = OperationMode.HeatExchange Then
                If HeatExchangeAreaCalculationMode = HeatExchangeAreaMode.AutoFromGeometry Then
                    ' Cylindrical vessel jacket area: A = pi * D * H, where H = V / (pi * D^2 / 4) => A = 4V / D
                    If Diameter > 0 Then
                        CalculatedHeatExchangeArea = 4.0 * Volume / Diameter
                    Else
                        CalculatedHeatExchangeArea = HeatExchangeArea
                    End If
                Else
                    CalculatedHeatExchangeArea = HeatExchangeArea
                End If
            End If

            Hr0 = ims.Phases(0).Properties.enthalpy.GetValueOrDefault * W 'reactands enthalpy

            IErr = 1

            Dim Tant As Double = T
            Dim TTol As Double = 0.01 'K, temperature convergence tolerance
            Const AdiabaticHRelax As Double = 0.5 'enthalpy under-relaxation factor for Adiabatic mode

            'Reference reactor mass (kg) captured at initial state for inventory mass-conservation rescaling
            Dim MReactorRef As Double = 0.0
            For i = 0 To NC - 1
                MReactorRef += NReac(i) * MM(i)
            Next

            '====================================================
            '==== Start of iteration loop =======================
            '====================================================
            'Assume output composition as input composition initially
            'Then do molar balances for every component for small time steps until composition becomes stable
            'Time step: Moles in reactor(t+dT)= Moles in reactor (t) + Feed - consumption (composition,temperature) - Output(composition)

            Do

                IObj?.SetCurrent

                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj2, "", "Calculate", String.Format("CSTR Convergence Loop #{0}", NIter), "", True)

                IObj2?.Paragraphs.Add("Assume output composition as input composition initially, 
                                        then do molar balances for every component for small time steps until composition becomes stable. ")
                IObj2?.Paragraphs.Add("Time step: Moles in reactor(t+dT)= Moles in reactor (t) + Feed - consumption (composition,temperature) - Output(composition)")

                'initialise component reaction rates
                Ri.Clear()
                For Each comp In ims.Phases(0).Compounds.Values
                    Ri.Add(comp.Name, 0)
                Next

                RxiT.Clear()

                Q = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault 'Mixture
                QV = ims.Phases(2).Properties.volumetric_flow.GetValueOrDefault 'Vapour
                QL = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault 'Liquid
                QS = ims.Phases(7).Properties.volumetric_flow.GetValueOrDefault 'Solid

                If ReactorMode = EReactorMode.SingleOutlet Then
                    ResidenceTimeL = Volume / Q
                    ResidenceTimeV = ResidenceTimeL
                Else
                    If QL + QS > 0 Then
                        ResidenceTimeL = Volume / (QL + QS)
                    Else
                        ResidenceTimeL = 0
                    End If

                    If QV > 0 Then
                        ResidenceTimeV = Headspace / QV
                    Else
                        ResidenceTimeV = 0
                    End If
                End If

                DHr = 0.0# 'Enthalpy change due to reactions
                DHRi.Clear()

                'Run through all reactions of Reaction Set to calculate component consumption/production rates at actual
                'composition and temperature.
                For Each rxnsb As ReactionSetBase In FlowSheet.ReactionSets(Me.ReactionSetID).Reactions.Values

                    If rxnsb.IsActive = False Then Continue For
                    rxn = FlowSheet.Reactions(rxnsb.ReactionID)
                    If rxn.ReactionType <> ReactionType.Kinetic And rxn.ReactionType <> ReactionType.Heterogeneous_Catalytic Then Continue For

                    BC = rxn.BaseReactant
                    scBC = Math.Abs(rxn.Components(BC).StoichCoeff)

                    convfactors = Me.GetConvFactors(rxn, ims)

                    'Read component concentrations in phase of reaction 

                    C.Clear()

                    'Qr = reaction volume where actual reaction takes place
                    Select Case rxn.ReactionPhase

                        Case ReactionPhase.Liquid

                            If ReactorMode = EReactorMode.SingleOutlet Then
                                Qr = QL / Q * Me.Volume
                            Else
                                Qr = QL / (QL + QS) * Me.Volume
                            End If

                            For Each comp As Compound In ims.Phases(1).Compounds.Values
                                C.Add(comp.Name, comp.Molarity) 'C: mol/mï¿½
                            Next

                        Case ReactionPhase.Vapor

                            If ReactorMode = EReactorMode.SingleOutlet Then
                                Qr = QV / Q * Me.Volume
                            Else
                                Qr = Me.Headspace
                            End If

                            For Each comp As Compound In ims.Phases(2).Compounds.Values
                                C.Add(comp.Name, comp.Molarity) 'C: mol/mï¿½
                            Next

                        Case ReactionPhase.Mixture

                            Qr = Me.Volume + Me.Headspace

                            For Each comp As Compound In ims.Phases(0).Compounds.Values
                                C.Add(comp.Name, comp.MolarFlow / Q) 'C: mol/mï¿½
                            Next

                        Case ReactionPhase.Solid

                            If ReactorMode = EReactorMode.SingleOutlet Then
                                Qr = QS / Q * Me.Volume
                            Else
                                Qr = QS / (QL + QS) * Me.Volume
                            End If

                            For Each comp As Compound In ims.Phases(7).Compounds.Values
                                C.Add(comp.Name, comp.MolarFlow / QS) 'C: mol/mï¿½
                            Next

                        Case ReactionPhase.Vapor_Solid

                            If ReactorMode = EReactorMode.SingleOutlet Then
                                Qr = QS / Q * Me.Volume
                            Else
                                Qr = QS / (QL + QS) * Me.Volume
                            End If

                            For Each comp As Compound In ims.Phases(2).Compounds.Values
                                C.Add(comp.Name, comp.MolarFlow / (QV + QS)) 'C: mol/mï¿½
                            Next

                            For Each comp As Compound In ims.Phases(7).Compounds.Values
                                C(comp.Name) += comp.MolarFlow / (QV + QS) 'C: mol/mï¿½
                            Next

                        Case ReactionPhase.Liquid_Solid

                            For Each comp As Compound In ims.Phases(1).Compounds.Values
                                C.Add(comp.Name, comp.MolarFlow / (QL + QS)) 'C: mol/mï¿½
                            Next

                            For Each comp As Compound In ims.Phases(7).Compounds.Values
                                C(comp.Name) += comp.MolarFlow / (QL + QS) 'C: mol/mï¿½
                            Next

                    End Select

                    If rxn.ReactionKinetics = ReactionKinetics.Expression Then

                        'calculate reaction constants

                        If rxn.ReactionType = ReactionType.Kinetic Then

                            Dim kxf, kxr As Double

                            If rxn.ReactionKinFwdType = ReactionKineticType.Arrhenius Then

                                kxf = rxn.A_Forward * Exp(-SystemsOfUnits.Converter.Convert(rxn.E_Forward_Unit, "J/mol", rxn.E_Forward) / (8.314 * T))

                            Else

                                rxn.ExpContext = New Flee.PublicTypes.ExpressionContext
                                rxn.ExpContext.Imports.AddType(GetType(System.Math))

                                rxn.ExpContext.Variables.Clear()
                                rxn.ExpContext.Variables.Add("T", ims.Phases(0).Properties.temperature.GetValueOrDefault)
                                rxn.ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture

                                rxn.Expr = rxn.ExpContext.CompileGeneric(Of Double)(rxn.ReactionKinFwdExpression)

                                kxf = rxn.Expr.Evaluate

                            End If

                            If rxn.ReactionKinRevType = ReactionKineticType.Arrhenius Then

                                kxr = rxn.A_Reverse * Exp(-SystemsOfUnits.Converter.Convert(rxn.E_Reverse_Unit, "J/mol", rxn.E_Reverse) / (8.314 * T))

                            Else

                                rxn.ExpContext = New Flee.PublicTypes.ExpressionContext
                                rxn.ExpContext.Imports.AddType(GetType(System.Math))

                                rxn.ExpContext.Variables.Clear()
                                rxn.ExpContext.Variables.Add("T", ims.Phases(0).Properties.temperature.GetValueOrDefault)
                                rxn.ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture

                                rxn.Expr = rxn.ExpContext.CompileGeneric(Of Double)(rxn.ReactionKinRevExpression)

                                kxr = rxn.Expr.Evaluate

                            End If

                            If T < rxn.Tmin Or T > rxn.Tmax Then
                                kxf = 0.0#
                                kxr = 0.0#
                            End If

                            Dim rxf As Double = 1.0#
                            Dim rxr As Double = 1.0#

                            'kinetic expression
                            For Each sb As ReactionStoichBase In rxn.Components.Values
                                rxf *= (C(sb.CompName) * convfactors(sb.CompName)) ^ sb.DirectOrder
                                rxr *= (C(sb.CompName) * convfactors(sb.CompName)) ^ sb.ReverseOrder
                            Next

                            'calculate total reaction rate (units of reaction definition)
                            Rx = kxf * rxf - kxr * rxr

                            'convert to internal SI units - RxiT: mol/s
                            Rxi(rxn.ID) = SystemsOfUnits.Converter.ConvertToSI(rxn.VelUnit, Rx)
                            RxiT.Add(rxn.ID, Rxi(rxn.ID) / scBC * Qr)

                        ElseIf rxn.ReactionType = ReactionType.Heterogeneous_Catalytic Then

                            If T < rxn.Tmin Or T > rxn.Tmax Then

                                Rx = 0.0

                            Else

                                Dim numval, denmval As Double

                                rxn.ExpContext = New Flee.PublicTypes.ExpressionContext
                                rxn.ExpContext.Imports.AddType(GetType(System.Math))

                                rxn.ExpContext.Variables.Clear()
                                rxn.ExpContext.Variables.Add("T", ims.Phases(0).Properties.temperature.GetValueOrDefault)
                                rxn.ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture

                                Dim ir As Integer = 1
                                Dim ip As Integer = 1

                                For Each sb As ReactionStoichBase In rxn.Components.Values
                                    If sb.StoichCoeff < 0 Then
                                        rxn.ExpContext.Variables.Add("R" & ir.ToString, C(sb.CompName) * convfactors(sb.CompName))
                                        ir += 1
                                    ElseIf sb.StoichCoeff > 0 Then
                                        rxn.ExpContext.Variables.Add("P" & ip.ToString, C(sb.CompName) * convfactors(sb.CompName))
                                        ip += 1
                                    End If
                                Next

                                rxn.Expr = rxn.ExpContext.CompileGeneric(Of Double)(rxn.RateEquationNumerator)

                                numval = rxn.Expr.Evaluate

                                rxn.Expr = rxn.ExpContext.CompileGeneric(Of Double)(rxn.RateEquationDenominator)

                                denmval = rxn.Expr.Evaluate

                                'calculate reaction rate & convert to internal SI units
                                Rx = SystemsOfUnits.Converter.ConvertToSI(rxn.VelUnit, numval / denmval)

                            End If

                            Rxi(rxn.ID) = Rx
                            RxiT.Add(rxn.ID, Rxi(rxn.ID))

                        End If

                    Else

                        ' python script
                        Dim ir As Integer = 1
                        Dim ip As Integer = 1
                        Dim ine As Integer = 1

                        Dim vars As New Dictionary(Of String, Double)
                        Dim amounts As New Dictionary(Of String, Double)

                        For Each sb As ReactionStoichBase In rxn.Components.Values
                            If sb.StoichCoeff < 0 Then
                                vars.Add("R" & ir.ToString, C(sb.CompName) * convfactors(sb.CompName))
                                ir += 1
                            ElseIf sb.StoichCoeff > 0 Then
                                vars.Add("P" & ip.ToString, C(sb.CompName) * convfactors(sb.CompName))
                                ip += 1
                            ElseIf sb.StoichCoeff = 0 Then
                                vars.Add("N" & ine.ToString, C(sb.CompName) * convfactors(sb.CompName))
                                ine += 1
                            End If
                            amounts.Add(sb.CompName, C(sb.CompName) * convfactors(sb.CompName))
                        Next

                        Dim r = ProcessAdvancedKineticReactionRate(rxn.ScriptTitle, Me, rxn, T, P, vars, amounts)

                        'calculate reaction rate & convert to internal SI units
                        Rx = SystemsOfUnits.Converter.ConvertToSI(rxn.VelUnit, r)

                        Rxi(rxn.ID) = Rx
                        RxiT.Add(rxn.ID, Rxi(rxn.ID))

                    End If

                    'calculate total compound production rates - Ri: mol/s
                    For Each sb As ReactionStoichBase In rxn.Components.Values
                        If rxn.ReactionType = ReactionType.Kinetic Then
                            Ri(sb.CompName) += Rxi(rxn.ID) * sb.StoichCoeff / scBC * Qr
                        ElseIf rxn.ReactionType = ReactionType.Heterogeneous_Catalytic Then
                            Ri(sb.CompName) += Rxi(rxn.ID) * sb.StoichCoeff / scBC * CatalystAmount
                        End If
                    Next

                    IObj2?.Paragraphs.Add(String.Format("Compounds : {0}", Ri.Keys.ToArray.ToMathArrayString))
                    IObj2?.Paragraphs.Add(String.Format("Compound Change Rates: {0} mol/s", Ri.Values.ToArray.ToMathArrayString))

                    'calculate heat of reaction
                    'Reaction Heat released (or absorbed) (kJ/s = kW) (Ideal Gas)
                    'kW (kJ/s) = kJ/kmol * mol/s * 0.001 mol/kmol
                    Hr = rxn.ReactionHeat * RxiT(rxn.ID) * 0.001 '/ scBC
                    DHRi.Add(rxn.ID, Hr)
                    DHr += Hr 'Total Heat released 

                Next

                IObj2?.Paragraphs.Add(String.Format("Total Heat of Reaction: {0} kW", DHr))

                'Calculate composition of mixture in reactor
                'Doing molar balance, calculate new component inventory of reactor after time step
                Ri.Values.CopyTo(RComp, 0)
                dB = Nin.AddY(RComp).SubtractY(Nout) 'Balance dB: mol/s

                'Limit time step for fast reactions
                Dim NtotalRef As Double = NReac.SumY
                For i = 0 To NC - 1
                    If dB(i) < 0 Then
                        'reaction step is limited to consumption of 80% of component amount in reactor
                        dRT(i) = -NReac(i) / dB(i) * 0.8
                        If dRT(i) < dT Then dT = dRT(i)
                    ElseIf dB(i) > 0 AndAlso NtotalRef > 0 Then
                        'symmetric cap on production: no single component grows beyond 20% of total reactor inventory per step
                        dRT(i) = 0.2 * NtotalRef / dB(i)
                        If dRT(i) < dT Then dT = dRT(i)
                    Else
                        dRT(i) = 0
                    End If
                Next

                'amount of component change due to reactions during time step dN: mol
                dN = dB.MultiplyConstY(dT)
                TR = dN.DivideY(NReac.AddConstY(1.0E-20)) 'Calculate relative consumption of components
                NReac = NReac.AddY(dN) 'calculate new inventory of components in reactor

                'Enforce mass conservation on reactor inventory to prevent unbounded drift
                If MReactorRef > 0 Then
                    Dim Mcurrent As Double = 0.0
                    For i = 0 To NC - 1
                        Mcurrent += NReac(i) * MM(i)
                    Next
                    If Mcurrent > 0 Then
                        NReac = NReac.MultiplyConstY(MReactorRef / Mcurrent)
                    End If
                End If

                'Time step is over! Now calculate new compositions
                Y = NReac.NormalizeY 'molar fraction
                ims.PropertyPackage.CurrentMaterialStream = ims
                M = ims.PropertyPackage.AUX_CONVERT_MOL_TO_MASS(Y) 'mass fraction
                Mout = M.MultiplyConstY(W) 'total components massflow
                Nout = Mout.DivideY(MM).MultiplyConstY(1000) 'total components molar flow

                IObj2?.Paragraphs.Add(String.Format("Previous Compound Compositions: {0}", LC.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("Updated Compound Compositions: {0}", Y.ToMathArrayString))

                'Update iteration variables
                OErr = IErr
                IErr = Y.SubtractY(LC).AbsSumY 'Calculate difference to last composition
                Y.CopyTo(LC, 0) 'Save composition as last composition (LC) for next iteration
                NIter += 1

                IObj2?.Paragraphs.Add(String.Format("Composition Error (Squared Sum): {0}", IErr))

                If Not dynamics Then

                    'Accelerate calculations by increasing time step up to residence time as maximum

                    MaxChange = -TR.Min
                    'If MaxChange < 0.3 Then
                    '    dT *= 1.2
                    'End If
                    If dT > 0.2 * ResidenceTimeL Then
                        dT = 0.2 * ResidenceTimeL
                    End If

                End If

                'Transfer calculation results to IMS-stream and recalculate stream
                i = 0
                For Each Comp In ims.Phases(0).Compounds
                    Comp.Value.MoleFraction = Y(i)
                    Comp.Value.MassFraction = M(i)
                    i += 1
                Next

                ims.SetMolarFlow(Nout.SumY)
                ims.Phases(0).Properties.temperature = T
                ims.Phases(0).Properties.pressure = P

                Select Case Me.ReactorOperationMode

                    Case OperationMode.Adiabatic

                        ims.SpecType = StreamSpec.Pressure_and_Enthalpy

                        Hp = Hr0 - DHr 'Products Enthalpy (kJ/kg * kg/s = kW)

                        Dim Hnew As Double = Hp / W
                        If NIter > 0 Then
                            Dim Hprev As Double = ims.Phases(0).Properties.enthalpy.GetValueOrDefault
                            Hnew = AdiabaticHRelax * Hnew + (1.0 - AdiabaticHRelax) * Hprev
                        End If
                        ims.Phases(0).Properties.enthalpy = Hnew

                    Case OperationMode.HeatExchange

                        ims.SpecType = StreamSpec.Pressure_and_Enthalpy

                        ' Auto-calculate internal and external HTCs if enabled
                        If CalculateInternalHTC Then
                            Dim rho_p As Double = ims.Phases(0).Properties.density.GetValueOrDefault
                            Dim mu_p As Double = ims.Phases(0).Properties.viscosity.GetValueOrDefault
                            Dim Cp_p As Double = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault * 1000
                            Dim k_p As Double = ims.Phases(0).Properties.thermalConductivity.GetValueOrDefault
                            CalculatedInternalHTC = StirredVesselHTC(Diameter, ImpellerDiameter, ImpellerSpeed, GetImpellerNusseltConstant(Impeller), rho_p, mu_p, Cp_p, k_p)
                        End If
                        If CalculateExternalHTC Then
                            Dim D_outer As Double = Diameter + 2.0 * WallThickness
                            Dim rho_c As Double = CoolantDensity
                            Dim mu_c As Double = CoolantViscosity
                            Dim Cp_c As Double = CoolantSpecificHeat
                            Dim k_c As Double = CoolantThermalConductivity
                            Dim mFlow_c As Double = CoolantMassFlowRate
                            If UseUtilityStream AndAlso Me.GraphicObject.InputConnectors.Count > 2 Then
                                Dim utilStr = GetInletMaterialStream(2)
                                If utilStr IsNot Nothing Then
                                    rho_c = utilStr.GetPhase("Mixture").Properties.density.GetValueOrDefault
                                    mu_c = utilStr.GetPhase("Mixture").Properties.viscosity.GetValueOrDefault
                                    Cp_c = utilStr.GetPhase("Mixture").Properties.heatCapacityCp.GetValueOrDefault * 1000
                                    k_c = utilStr.GetPhase("Mixture").Properties.thermalConductivity.GetValueOrDefault
                                    mFlow_c = utilStr.GetMassFlow()
                                End If
                            End If
                            CalculatedExternalHTC = AnnularJacketHTC(D_outer, JacketDiameter, rho_c, mu_c, Cp_c, k_c, mFlow_c)
                        End If

                        ' Compute effective HTC (temperature-dependent if using wall properties)
                        Dim U_eff As Double = If(UseWallProperties, ComputeEffectiveHTC(T), OverallHeatTransferCoefficient)
                        CalculatedOverallHTC = U_eff

                        ' Q = U * A * (Tc - T) in W, convert to kW
                        Dim A_hx As Double = CalculatedHeatExchangeArea
                        Dim Tc_eff As Double
                        If HeatExchangeCoolantFlowDirection = HeatExchangeCoolantMode.ConstantTemperature Then
                            Tc_eff = CoolantInletTemperature
                        Else
                            ' For variable coolant with CSTR (uniform reactor T), use log-mean or arithmetic mean
                            ' Tc_out = Tc_in - Q/(m*Cp), but Q depends on Tc_eff -> iterate
                            ' Use arithmetic mean as approximation: Tc_eff = (Tc_in + Tc_out)/2
                            ' First estimate Q with Tc_in, then refine
                            Dim mCp_hx As Double = CoolantMassFlowRate * CoolantSpecificHeat
                            If UseUtilityStream AndAlso Me.GraphicObject.InputConnectors.Count > 2 Then
                                Dim utilStream = GetInletMaterialStream(2)
                                If utilStream IsNot Nothing Then
                                    mCp_hx = utilStream.GetMassFlow() * utilStream.GetPhase("Mixture").Properties.heatCapacityCp.GetValueOrDefault * 1000
                                End If
                            End If
                            Dim Q_est As Double = U_eff * A_hx * (CoolantInletTemperature - T)
                            Dim Tc_out_est As Double = CoolantInletTemperature - Q_est / mCp_hx
                            Tc_eff = (CoolantInletTemperature + Tc_out_est) / 2.0
                        End If

                        Dim Q_hx As Double = U_eff * A_hx * (Tc_eff - T) / 1000.0 ' kW

                        Hp = Hr0 - DHr + Q_hx 'Products Enthalpy (kJ/kg * kg/s = kW)

                        ims.Phases(0).Properties.enthalpy = Hp / W

                    Case Else

                        ims.SpecType = StreamSpec.Temperature_and_Pressure

                End Select

                IObj2?.SetCurrent()

                Tant = T
                ims.Calculate(True, True)

                T = ims.Phases(0).Properties.temperature 'read temperature -> PH-Flash

                IObj2?.Close()

                If dynamics Then Exit Do

                If NIter > MaxIterations Then Throw New Exception(FlowSheet.GetTranslatedString("Nmeromximodeiteraesa3"))

                FlowSheet.CheckStatus()

            Loop Until (IErr < Tolerance Or MaxChange < Tolerance * 10) And Math.Abs(T - Tant) < TTol 'repeat until composition and temperature are stable

            '====================================================
            '==== Transfer information to output stream =========
            '====================================================

out:        Dim ms1, ms2 As MaterialStream

            If ReactorMode = EReactorMode.SingleOutlet Then 'only a single outlet

                ms1 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)

                With ms1

                    If Not dynamics Then
                        .SpecType = ims.SpecType
                        .Phases(0).Properties.massflow = ims.Phases(0).Properties.massflow.GetValueOrDefault
                        .DefinedFlow = FlowSpec.Mass
                    End If

                    .Phases(0).Properties.massfraction = 1
                    .Phases(0).Properties.temperature = T
                    .Phases(0).Properties.pressure = P
                    .Phases(0).Properties.enthalpy = ims.Phases(0).Properties.enthalpy.GetValueOrDefault

                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = ims.Phases(0).Compounds(comp.Name).MoleFraction.GetValueOrDefault
                        comp.MassFraction = ims.Phases(0).Compounds(comp.Name).MassFraction.GetValueOrDefault
                        comp.MassFlow = comp.MassFraction.GetValueOrDefault * .Phases(0).Properties.massflow.GetValueOrDefault
                        comp.MolarFlow = comp.MoleFraction.GetValueOrDefault * .Phases(0).Properties.molarflow.GetValueOrDefault
                    Next

                End With

            Else

                'two outlets: Liquid+Solid->Outlet1 / Vapour->Outlet2
                ms1 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
                ms2 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name)

                'Vapour outlet
                With ms2

                    If Not dynamics Then
                        .SpecType = ims.SpecType
                        .Phases(0).Properties.massflow = ims.Phases(2).Properties.massflow.GetValueOrDefault
                        .Phases(0).Properties.massfraction = 1
                        .DefinedFlow = FlowSpec.Mass
                    End If

                    .Phases(0).Properties.temperature = T
                    .Phases(0).Properties.pressure = P
                    .Phases(0).Properties.enthalpy = ims.Phases(2).Properties.enthalpy.GetValueOrDefault

                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = ims.Phases(2).Compounds(comp.Name).MoleFraction.GetValueOrDefault
                        comp.MassFraction = ims.Phases(2).Compounds(comp.Name).MassFraction.GetValueOrDefault
                        comp.MassFlow = comp.MassFraction.GetValueOrDefault * .Phases(2).Properties.massflow.GetValueOrDefault
                        comp.MolarFlow = comp.MoleFraction.GetValueOrDefault * .Phases(2).Properties.molarflow.GetValueOrDefault
                    Next

                End With

                'Liquid/Solid outlet

                With ms1

                    If Not dynamics Then
                        .SpecType = ims.SpecType
                        .Phases(0).Properties.massflow = ims.Phases(0).Properties.massflow.GetValueOrDefault - ims.Phases(2).Properties.massflow.GetValueOrDefault
                        .Phases(0).Properties.massfraction = 1
                    End If

                    .Phases(0).Properties.molarflow = ims.Phases(0).Properties.molarflow.GetValueOrDefault - ims.Phases(2).Properties.molarflow.GetValueOrDefault

                    .Phases(0).Properties.temperature = T
                    .Phases(0).Properties.pressure = P

                    Hp = ims.Phases(3).Properties.enthalpy.GetValueOrDefault * ims.Phases(3).Properties.massflow.GetValueOrDefault
                    Hp += ims.Phases(7).Properties.enthalpy.GetValueOrDefault * ims.Phases(7).Properties.massflow.GetValueOrDefault
                    Hp /= .Phases(0).Properties.massflow

                    .Phases(0).Properties.enthalpy = Hp

                    For Each comp In .Phases(0).Compounds.Values
                        comp.MassFlow = ims.Phases(0).Compounds(comp.Name).MassFlow - ims.Phases(2).Compounds(comp.Name).MassFlow
                        comp.MolarFlow = ims.Phases(0).Compounds(comp.Name).MolarFlow - ims.Phases(2).Compounds(comp.Name).MolarFlow
                        comp.MoleFraction = comp.MolarFlow / ms1.Phases(0).Properties.molarflow
                        comp.MassFraction = comp.MassFlow / ms1.Phases(0).Properties.massflow
                    Next

                End With

            End If

            If ReactorMode = EReactorMode.SingleOutlet Then
                ResidenceTimeL = Volume / Q
                ResidenceTimeV = ResidenceTimeL
            Else
                If QL + QS > 0 Then
                    ResidenceTimeL = Volume / (QL + QS)
                Else
                    ResidenceTimeL = 0
                End If

                If QV > 0 Then
                    ResidenceTimeV = Headspace / QV
                Else
                    ResidenceTimeV = 0
                End If
            End If

            DeltaT = T - T0

            'Calculate component conversions
            For i = 0 To NC - 1
                If Nin(i) > 0.0 Then
                    ComponentConversions(CompNames(i)) = Abs(Nin(i) - Nout(i)) / Nin(i)
                End If
            Next

            OutletTemperature = T

            If Not dynamics Then

                '====================================================
                '==== Transfer information to energy stream =========
                '====================================================

                'overall reaction heat

                Dim DHrT As Double = 0
                i = 0
                For Each sb As Compound In ims.Phases(0).Compounds.Values
                    DHrT += sb.ConstantProperties.IG_Enthalpy_of_Formation_25C * sb.ConstantProperties.Molar_Weight * (Nout(i) - Nin(i)) / 1000
                    i += 1
                Next

                If Me.ReactorOperationMode = OperationMode.Isothermic Then

                    'Products Enthalpy (kJ/kg * kg/s = kW)
                    Hp = ims.Phases(0).Properties.enthalpy.GetValueOrDefault * ims.Phases(0).Properties.massflow.GetValueOrDefault

                    Me.DeltaQ = DHrT + Hp - Hr0

                    Me.DeltaT = 0.0#

                    OutletTemperature = T0

                ElseIf Me.ReactorOperationMode = OperationMode.OutletTemperature Then

                    'Products Enthalpy (kJ/kg * kg/s = kW)
                    Hp = ims.Phases(0).Properties.enthalpy.GetValueOrDefault * ims.Phases(0).Properties.massflow.GetValueOrDefault

                    'Heat (kW)
                    Me.DeltaQ = DHrT + Hp - Hr0

                    Me.DeltaT = OutletTemperature - T0

                ElseIf Me.ReactorOperationMode = OperationMode.HeatExchange Then

                    ' Auto-calculate HTCs with converged temperature
                    If CalculateInternalHTC Then
                        Dim rho_p1 As Double = ims.Phases(0).Properties.density.GetValueOrDefault
                        Dim mu_p1 As Double = ims.Phases(0).Properties.viscosity.GetValueOrDefault
                        Dim Cp_p1 As Double = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault * 1000
                        Dim k_p1 As Double = ims.Phases(0).Properties.thermalConductivity.GetValueOrDefault
                        CalculatedInternalHTC = StirredVesselHTC(Diameter, ImpellerDiameter, ImpellerSpeed, GetImpellerNusseltConstant(Impeller), rho_p1, mu_p1, Cp_p1, k_p1)
                    End If
                    If CalculateExternalHTC Then
                        Dim D_outer1 As Double = Diameter + 2.0 * WallThickness
                        Dim rho_c1 As Double = CoolantDensity
                        Dim mu_c1 As Double = CoolantViscosity
                        Dim Cp_c1 As Double = CoolantSpecificHeat
                        Dim k_c1 As Double = CoolantThermalConductivity
                        Dim mFlow_c1 As Double = CoolantMassFlowRate
                        If UseUtilityStream AndAlso Me.GraphicObject.InputConnectors.Count > 2 Then
                            Dim utilStr1 = GetInletMaterialStream(2)
                            If utilStr1 IsNot Nothing Then
                                rho_c1 = utilStr1.GetPhase("Mixture").Properties.density.GetValueOrDefault
                                mu_c1 = utilStr1.GetPhase("Mixture").Properties.viscosity.GetValueOrDefault
                                Cp_c1 = utilStr1.GetPhase("Mixture").Properties.heatCapacityCp.GetValueOrDefault * 1000
                                k_c1 = utilStr1.GetPhase("Mixture").Properties.thermalConductivity.GetValueOrDefault
                                mFlow_c1 = utilStr1.GetMassFlow()
                            End If
                        End If
                        CalculatedExternalHTC = AnnularJacketHTC(D_outer1, JacketDiameter, rho_c1, mu_c1, Cp_c1, k_c1, mFlow_c1)
                    End If

                    ' Calculate final heat exchange with converged reactor temperature
                    Dim U_eff1 As Double = If(UseWallProperties, ComputeEffectiveHTC(T), OverallHeatTransferCoefficient)
                    CalculatedOverallHTC = U_eff1
                    Dim A_hx1 As Double = CalculatedHeatExchangeArea
                    Dim Tc_eff1 As Double
                    If HeatExchangeCoolantFlowDirection = HeatExchangeCoolantMode.ConstantTemperature Then
                        Tc_eff1 = CoolantInletTemperature
                    Else
                        Dim mCp1 As Double = CoolantMassFlowRate * CoolantSpecificHeat
                        If UseUtilityStream AndAlso Me.GraphicObject.InputConnectors.Count > 2 Then
                            Dim utilStream = GetInletMaterialStream(2)
                            If utilStream IsNot Nothing Then
                                mCp1 = utilStream.GetMassFlow() * utilStream.GetPhase("Mixture").Properties.heatCapacityCp.GetValueOrDefault * 1000
                            End If
                        End If
                        Dim Q_est1 As Double = U_eff1 * A_hx1 * (CoolantInletTemperature - T)
                        Dim Tc_out1 As Double = CoolantInletTemperature - Q_est1 / mCp1
                        Tc_eff1 = (CoolantInletTemperature + Tc_out1) / 2.0
                        CoolantOutletTemperature = Tc_out1
                    End If
                    If HeatExchangeCoolantFlowDirection = HeatExchangeCoolantMode.ConstantTemperature Then
                        CoolantOutletTemperature = CoolantInletTemperature
                    End If

                    Me.DeltaQ = U_eff1 * A_hx1 * (Tc_eff1 - T) / 1000.0

                    OutletTemperature = T
                    Me.DeltaT = OutletTemperature - T0

                    ' Update utility stream outlet if connected
                    If UseUtilityStream AndAlso Me.GraphicObject.OutputConnectors.Count > 2 AndAlso Me.GraphicObject.InputConnectors.Count > 2 Then
                        Dim utilOutCp = Me.GraphicObject.OutputConnectors(2)
                        If utilOutCp IsNot Nothing AndAlso utilOutCp.IsAttached Then
                            Dim utilOut As MaterialStream = FlowSheet.SimulationObjects(utilOutCp.AttachedConnector.AttachedTo.Name)
                            Dim utilIn = GetInletMaterialStream(2)
                            If utilIn IsNot Nothing AndAlso utilOut IsNot Nothing Then
                                utilOut.Assign(utilIn)
                                utilOut.Phases(0).Properties.temperature = CoolantOutletTemperature
                                utilOut.SpecType = StreamSpec.Temperature_and_Pressure
                                utilOut.Calculate(True, True)
                            End If
                        End If
                    End If

                Else

                    OutletTemperature = T

                    Me.DeltaT = OutletTemperature - T0

                End If

                Dim estr As Streams.EnergyStream = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(1).AttachedConnector.AttachedFrom.Name)
                If estr IsNot Nothing Then
                    With estr
                        .EnergyFlow = DeltaQ.GetValueOrDefault
                        .GraphicObject.Calculated = True
                    End With
                End If

            End If

            IObj?.Close()

        End Sub

        ''' <summary>Clears the calculated results from the outlet material streams.</summary>
        Public Overrides Sub DeCalculate()

            Dim j As Integer = 0

            Dim ms As MaterialStream
            Dim cp As IConnectionPoint

            cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.enthalpy = Nothing
                    Dim comp As BaseClasses.Compound
                    j = 0
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                        j += 1
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .Phases(0).Properties.massfraction = 1
                    .Phases(0).Properties.molarfraction = 1
                    .GraphicObject.Calculated = False
                End With
            End If

        End Sub

        ''' <summary>Returns the value of the specified property, converted to the given unit system.</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As Double = 0

                If prop.Contains("_") Then

                    Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                    Select Case propidx
                        Case 0
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.DeltaP.GetValueOrDefault)
                        Case 1
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.time, Me.ResidenceTimeL)
                        Case 2
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.volume, Me.Volume)
                        Case 3
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.DeltaT.GetValueOrDefault)
                        Case 4
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.DeltaQ.GetValueOrDefault)
                        Case 5
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.OutletTemperature)
                        Case 6
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.volume, Me.Headspace)
                        Case 7
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.time, Me.ResidenceTimeV)
                        Case 8
                            value = OverallHeatTransferCoefficient
                        Case 9
                            value = HeatExchangeArea
                        Case 10
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.temperature, CoolantInletTemperature)
                        Case 11
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.massflow, CoolantMassFlowRate)
                        Case 12
                            value = CoolantSpecificHeat
                        Case 13
                            value = CInt(HeatExchangeCoolantFlowDirection)
                        Case 14
                            value = CInt(HeatExchangeAreaCalculationMode)
                        Case 15
                            value = If(UseUtilityStream, 1, 0)
                        Case 16
                            value = WallThickness
                        Case 17
                            value = InternalHTC
                        Case 18
                            value = ExternalHTC
                        Case 19
                            value = If(UseWallProperties, 1, 0)
                        Case 20
                            value = CalculatedOverallHTC
                        Case 21
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Diameter)
                        Case 22
                            value = If(CalculateInternalHTC, 1, 0)
                        Case 23
                            value = If(CalculateExternalHTC, 1, 0)
                        Case 24
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, JacketDiameter)
                        Case 25
                            value = CoolantDensity
                        Case 26
                            value = CoolantViscosity
                        Case 27
                            value = CoolantThermalConductivity
                        Case 28
                            value = CalculatedInternalHTC
                        Case 29
                            value = CalculatedExternalHTC
                        Case 30
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, ImpellerDiameter)
                        Case 31
                            value = ImpellerSpeed
                        Case 32
                            value = CInt(Impeller)
                    End Select


                Else

                    Select Case prop
                        Case "Calculation Mode"
                            Select Case ReactorOperationMode
                                Case OperationMode.Adiabatic
                                    Return "Adiabatic"
                                Case OperationMode.Isothermic
                                    Return "Isothermic"
                                Case OperationMode.OutletTemperature
                                    Return "Defined Temperature"
                                Case OperationMode.HeatExchange
                                    Return "Heat Exchange"
                            End Select
                        Case Else
                            If prop.Contains("Conversion") Then
                                Dim comp = prop.Split(": ")(0)
                                If ComponentConversions.ContainsKey(comp) Then
                                    value = ComponentConversions(comp) * 100
                                Else
                                    value = 0.0
                                End If
                            End If
                            If prop.Contains("Extent") Then
                                Dim rx = prop.Split(": ")(0)
                                Dim rx2 = FlowSheet.Reactions.Values.Where(Function(x) x.Name = rx).FirstOrDefault
                                If rx2 IsNot Nothing AndAlso Rxi.ContainsKey(rx2.ID) Then
                                    value = SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, RxiT(rx2.ID))
                                Else
                                    value = 0.0
                                End If
                            End If
                            If prop.Contains("Rate") Then
                                Dim rx = prop.Split(": ")(0)
                                Dim rx2 = FlowSheet.Reactions.Values.Where(Function(x) x.Name = rx).FirstOrDefault
                                If rx2 IsNot Nothing AndAlso Rxi.ContainsKey(rx2.ID) Then
                                    value = SystemsOfUnits.Converter.ConvertFromSI(su.reac_rate, RxiT(rx2.ID) / Volume)
                                Else
                                    value = 0.0
                                End If
                            End If
                            If prop.Contains("Heat") Then
                                Dim rx = prop.Split(": ")(0)
                                Dim rx2 = FlowSheet.Reactions.Values.Where(Function(x) x.Name = rx).FirstOrDefault
                                If rx2 IsNot Nothing AndAlso DHRi.ContainsKey(rx2.ID) Then
                                    value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, DHRi(rx2.ID))
                                Else
                                    value = 0.0
                                End If
                            End If
                    End Select

                End If

                Return value

            End If
        End Function

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RW
                    For i = 0 To 32
                        proplist.Add("PROP_CS_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 32
                        proplist.Add("PROP_CS_" + CStr(i))
                    Next
                Case PropertyType.ALL, PropertyType.RO
                    For i = 0 To 32
                        proplist.Add("PROP_CS_" + CStr(i))
                    Next
                    proplist.Add("Calculation Mode")
                    For Each item In ComponentConversions
                        proplist.Add(item.Key + ": Conversion")
                    Next
                    For Each dbl As KeyValuePair(Of String, Double) In RxiT
                        proplist.Add(FlowSheet.Reactions(dbl.Key).Name + ": Extent")
                    Next
                    For Each dbl As KeyValuePair(Of String, Double) In RxiT
                        proplist.Add(FlowSheet.Reactions(dbl.Key).Name + ": Rate")
                    Next
                    For Each dbl As KeyValuePair(Of String, Double) In DHRi
                        proplist.Add(FlowSheet.Reactions(dbl.Key).Name + ": Heat")
                    Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>Sets the value of the specified property from the given value and unit system.</summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx
                Case 0
                    Me.DeltaP = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)
                Case 1
                    Me.ResidenceTimeL = SystemsOfUnits.Converter.ConvertToSI(su.time, propval)
                Case 2
                    Me.Volume = SystemsOfUnits.Converter.ConvertToSI(su.volume, propval)
                Case 3
                    Me.DeltaT = SystemsOfUnits.Converter.ConvertToSI(su.deltaT, propval)
                Case 5
                    Me.OutletTemperature = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval)
                Case 6
                    Me.Headspace = SystemsOfUnits.Converter.ConvertToSI(su.volume, propval)
                Case 8
                    OverallHeatTransferCoefficient = propval
                Case 9
                    HeatExchangeArea = propval
                Case 10
                    CoolantInletTemperature = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval)
                Case 11
                    CoolantMassFlowRate = SystemsOfUnits.Converter.ConvertToSI(su.massflow, propval)
                Case 12
                    CoolantSpecificHeat = propval
                Case 13
                    HeatExchangeCoolantFlowDirection = CType(Convert.ToInt32(propval), Reactors.HeatExchangeCoolantMode)
                Case 14
                    HeatExchangeAreaCalculationMode = CType(Convert.ToInt32(propval), Reactors.HeatExchangeAreaMode)
                Case 15
                    UseUtilityStream = Convert.ToBoolean(propval)
                Case 16
                    WallThickness = propval
                Case 17
                    InternalHTC = propval
                Case 18
                    ExternalHTC = propval
                Case 19
                    UseWallProperties = Convert.ToBoolean(propval)
                Case 21
                    Diameter = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval)
                Case 22
                    CalculateInternalHTC = Convert.ToBoolean(propval)
                Case 23
                    CalculateExternalHTC = Convert.ToBoolean(propval)
                Case 24
                    JacketDiameter = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval)
                Case 25
                    CoolantDensity = propval
                Case 26
                    CoolantViscosity = propval
                Case 27
                    CoolantThermalConductivity = propval
                Case 30
                    ImpellerDiameter = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval)
                Case 31
                    ImpellerSpeed = propval
                Case 32
                    Impeller = CType(Convert.ToInt32(propval), Reactors.ImpellerType)
            End Select
            Return 1
        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As String = ""

                If prop.Contains("_") Then

                    Try

                        Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                        Select Case propidx
                            Case 0
                                value = su.deltaP
                            Case 1
                                value = su.time
                            Case 2
                                value = su.volume
                            Case 3
                                value = su.deltaT
                            Case 4
                                value = su.heatflow
                            Case 5
                                value = su.temperature
                            Case 6
                                value = su.volume
                            Case 7
                                value = su.time
                            Case 8
                                value = "W/m2.K"
                            Case 9
                                value = "m2"
                            Case 10
                                value = su.temperature
                            Case 11
                                value = su.massflow
                            Case 12
                                value = "J/kg.K"
                            Case 13, 14, 15, 19
                                value = ""
                            Case 16
                                value = "m"
                            Case 17, 18, 20, 28, 29
                                value = "W/m2.K"
                            Case 21, 24, 30
                                value = su.diameter
                            Case 22, 23, 32
                                value = ""
                            Case 25
                                value = su.density
                            Case 26
                                value = "Pa.s"
                            Case 27
                                value = "W/m.K"
                            Case 31
                                value = "RPM"
                        End Select

                    Catch ex As Exception

                        Return ""

                    End Try

                Else

                    Select Case prop
                        Case "Calculation Mode"
                            Return ""
                        Case Else
                            If prop.Contains("Conversion") Then value = "%"
                            If prop.Contains("Rate") Then value = su.reac_rate
                            If prop.Contains("Extent") Then value = su.molarflow
                            If prop.Contains("Heat") Then value = su.heatflow
                    End Select

                End If

                Return value

            End If
        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.cstr.png")

        End Function

        ''' <summary>Returns the localised display description for this reactor.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("CSTR_Desc")
        End Function

        ''' <summary>Returns the localised display name for this reactor.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("CSTR_Name")
        End Function

        ''' <summary>Gets a value indicating whether this reactor is compatible with mobile/cross-platform interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>Generates a plain-text report of the reactor results using the specified units and format.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder

            str.AppendLine("Reactor:  " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Calculation Parameters")
            str.AppendLine()
            str.AppendLine("    Calculation Mode: " & ReactorOperationMode.ToString)
            str.AppendLine("    Reactor Volume: " & SystemsOfUnits.Converter.ConvertFromSI(su.volume, Me.Volume).ToString(numberformat, ci) & " " & su.volume)
            str.AppendLine("    Pressure Drop: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.DeltaP.GetValueOrDefault).ToString(numberformat, ci) & " " & su.deltaP)
            str.AppendLine()
            str.AppendLine("Results")
            str.AppendLine()
            Select Case Me.ReactorOperationMode
                Case OperationMode.Adiabatic
                    str.AppendLine("    Outlet Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.OutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                Case OperationMode.Isothermic
                    str.AppendLine("    Heat Added/Removed: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.DeltaQ.GetValueOrDefault).ToString(numberformat, ci) & " " & su.heatflow)
                Case OperationMode.OutletTemperature
                    str.AppendLine("    Outlet Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.OutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Heat Added/Removed: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.DeltaQ.GetValueOrDefault).ToString(numberformat, ci) & " " & su.heatflow)
                Case OperationMode.HeatExchange
                    str.AppendLine("    Outlet Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.OutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Heat Exchanged: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.DeltaQ.GetValueOrDefault).ToString(numberformat, ci) & " " & su.heatflow)
                    str.AppendLine("    Coolant Outlet Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.CoolantOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Heat Transfer Area: " & Me.CalculatedHeatExchangeArea.ToString(numberformat, ci) & " m2")
                    If Me.UseWallProperties Then
                        str.AppendLine("    Wall Material: " & Me.WallMaterial)
                        str.AppendLine("    Wall Thickness: " & Me.WallThickness.ToString(numberformat, ci) & " m")
                        str.AppendLine("    Internal HTC: " & Me.InternalHTC.ToString(numberformat, ci) & " W/m2.K")
                        str.AppendLine("    External HTC: " & Me.ExternalHTC.ToString(numberformat, ci) & " W/m2.K")
                        str.AppendLine("    Calculated Overall HTC: " & Me.CalculatedOverallHTC.ToString(numberformat, ci) & " W/m2.K")
                    End If
            End Select
            str.AppendLine("    Residence Time: " & SystemsOfUnits.Converter.ConvertFromSI(su.time, Me.ResidenceTimeL).ToString(numberformat, ci) & " " & su.time)
            str.AppendLine()
            str.AppendLine("Reaction Extents")
            str.AppendLine()
            For Each dbl As KeyValuePair(Of String, Double) In Me.RxiT
                str.AppendLine("    " & Me.GetFlowsheet.Reactions(dbl.Key).Name & ": " & SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, dbl.Value).ToString(numberformat, ci) & " " & su.molarflow)
            Next
            str.AppendLine()
            str.AppendLine("Reaction Rates")
            str.AppendLine()
            For Each dbl As KeyValuePair(Of String, Double) In Me.RxiT
                str.AppendLine("    " & Me.GetFlowsheet.Reactions(dbl.Key).Name & ": " & SystemsOfUnits.Converter.ConvertFromSI(su.reac_rate, (dbl.Value / Me.Volume)).ToString(numberformat, ci) & " " & su.reac_rate)
            Next
            str.AppendLine()
            str.AppendLine("Reaction Heats")
            str.AppendLine()
            For Each dbl As KeyValuePair(Of String, Double) In Me.DHRi
                str.AppendLine("    " & Me.GetFlowsheet.Reactions(dbl.Key).Name & ": " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, dbl.Value).ToString(numberformat, ci) & " " & su.heatflow)
            Next
            str.AppendLine()
            str.AppendLine("Compound Conversions")
            str.AppendLine()
            For Each dbl As KeyValuePair(Of String, Double) In Me.ComponentConversions
                If dbl.Value > 0 Then
                    str.AppendLine("    " & dbl.Key & ": " & (dbl.Value * 100).ToString(numberformat, ci) & "%")
                End If
            Next
            Return str.ToString

        End Function

        ''' <summary>Generates a structured report of the reactor results as a list of typed tuples.</summary>
        Public Overrides Function GetStructuredReport() As List(Of Tuple(Of ReportItemType, String()))

            Dim su As IUnitsOfMeasure = GetFlowsheet().FlowsheetOptions.SelectedUnitSystem
            Dim nf = GetFlowsheet().FlowsheetOptions.NumberFormat

            Dim list As New List(Of Tuple(Of ReportItemType, String()))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Results Report for CSTR '" & Me.GraphicObject.Tag + "'"}))
            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.SingleColumn, New String() {"Calculated successfully on " & LastUpdated.ToString}))


            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Calculation Parameters"}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.DoubleColumn, New String() {"Calculation Mode", ReactorOperationMode.ToString}))
            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn, New String() {"Pressure Drop", SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.DeltaP.GetValueOrDefault).ToString(nf), su.deltaP}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Results"}))

            Select Case Me.ReactorOperationMode
                Case OperationMode.Adiabatic
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Outlet Temperature",
                            Me.OutletTemperature.ConvertFromSI(su.temperature).ToString(nf), su.temperature}))
                Case OperationMode.Isothermic
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Heat Added/Removed",
                            Me.DeltaQ.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf), su.heatflow}))
                Case OperationMode.OutletTemperature
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Outlet Temperature",
                            Me.OutletTemperature.ConvertFromSI(su.temperature).ToString(nf), su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Heat Added/Removed",
                            Me.DeltaQ.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf), su.heatflow}))
                Case OperationMode.HeatExchange
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Outlet Temperature",
                            Me.OutletTemperature.ConvertFromSI(su.temperature).ToString(nf), su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Heat Exchanged",
                            Me.DeltaQ.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf), su.heatflow}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Coolant Outlet Temperature",
                            Me.CoolantOutletTemperature.ConvertFromSI(su.temperature).ToString(nf), su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Heat Transfer Area",
                            Me.CalculatedHeatExchangeArea.ToString(nf), "m2"}))
                    If Me.UseWallProperties Then
                        list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                                New String() {"Calculated Overall HTC",
                                Me.CalculatedOverallHTC.ToString(nf), "W/m2.K"}))
                    End If
            End Select

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                    New String() {"Residence Time (Liquid)",
                    ResidenceTimeL.ConvertFromSI(su.time).ToString(nf),
                    su.time}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                    New String() {"Residence Time (Vapor)",
                    ResidenceTimeV.ConvertFromSI(su.time).ToString(nf),
                    su.time}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Reaction Extents"}))
            If Not Me.Conversions Is Nothing Then
                For Each dbl As KeyValuePair(Of String, Double) In Me.RxiT
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {Me.GetFlowsheet.Reactions(dbl.Key).Name,
                            (dbl.Value).ConvertFromSI(su.molarflow).ToString(nf), su.molarflow}))
                Next
            End If

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Reaction Rates"}))
            If Not Me.Conversions Is Nothing Then
                For Each dbl As KeyValuePair(Of String, Double) In Me.RxiT
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {Me.GetFlowsheet.Reactions(dbl.Key).Name,
                            (dbl.Value / Volume).ConvertFromSI(su.reac_rate).ToString(nf), su.reac_rate}))
                Next
            End If

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Reaction Heats"}))
            If Not Me.Conversions Is Nothing Then
                For Each dbl As KeyValuePair(Of String, Double) In Me.DHRi
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {Me.GetFlowsheet.Reactions(dbl.Key).Name,
                            (dbl.Value).ConvertFromSI(su.heatflow).ToString(nf), su.heatflow}))
                Next
            End If

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Compound Conversions"}))
            For Each dbl As KeyValuePair(Of String, Double) In Me.ComponentConversions
                If dbl.Value >= 0 Then list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {dbl.Key,
                            (dbl.Value * 100).ToString(nf), "%"}))
            Next

            Return list

        End Function
        ''' <summary>Returns a human-readable description of the specified property.</summary>
        Public Overrides Function GetPropertyDescription(p As String) As String
            If p.Equals("Calculation Mode") Then
                Return "Select the calculation mode of this reactor."
            ElseIf p.Equals("Pressure Drop") Then
                Return "Enter the desired pressure drop for this reactor."
            ElseIf p.Equals("Outlet Temperature") Then
                Return "If you chose 'Outlet Temperature' as the calculation mode, enter the desired value. If you chose a different calculation mode, this parameter will be calculated."
            ElseIf p.Equals("Reactor Volume") Then
                Return "Define the active volume of this reactor."
            ElseIf p.Equals("Catalyst Amount") Then
                Return "Enter the amount of catalyst in the reactor (for HetCat reactions only)."
            Else
                Return p
            End If
        End Function

    End Class

End Namespace


