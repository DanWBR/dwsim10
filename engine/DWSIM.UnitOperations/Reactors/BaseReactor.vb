'    Reactor Base Class
'    Copyright 2008 Daniel Wagner O. de Medeiros
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


Imports System.Collections.Generic
Imports DWSIM.Thermodynamics.BaseClasses
Imports System.Linq
Imports DWSIM.Interfaces.Enums
Imports DWSIM.SharedClasses
Imports Microsoft.Scripting.Hosting

''' <summary>
''' Implements reactor unit operations including conversion, equilibrium, CSTR, PFR, and Gibbs free energy
''' minimization reactors.
''' </summary>
Namespace Reactors

    ''' <summary>Defines the thermal operating mode of a reactor.</summary>
    Public Enum OperationMode
        ''' <summary>Isothermal operation - reactor temperature is held constant.</summary>
        Isothermic = 0
        ''' <summary>Adiabatic operation - no heat is exchanged with the surroundings.</summary>
        Adiabatic = 1
        ''' <summary>Outlet temperature is specified by the user.</summary>
        OutletTemperature = 2
        ''' <summary>Non-isothermal, non-adiabatic - both heat duty and temperature change are calculated.</summary>
        NonIsothermalNonAdiabatic = 3
        ''' <summary>Heat exchange with a utility fluid via U*A*(Tc - T) model.</summary>
        HeatExchange = 4
    End Enum

    ''' <summary>Defines the coolant flow configuration for heat exchange mode.</summary>
    Public Enum HeatExchangeCoolantMode
        ''' <summary>Coolant temperature is constant along the reactor (e.g., boiling/condensing utility).</summary>
        ConstantTemperature = 0
        ''' <summary>Coolant flows in the same direction as the process fluid.</summary>
        CoCurrent = 1
        ''' <summary>Coolant flows opposite to the process fluid.</summary>
        CounterCurrent = 2
    End Enum

    ''' <summary>Defines how the heat transfer area is determined.</summary>
    Public Enum HeatExchangeAreaMode
        ''' <summary>Area is calculated automatically from reactor geometry (PFR: pi*D*L*Ntubes).</summary>
        AutoFromGeometry = 0
        ''' <summary>Area is specified directly by the user.</summary>
        UserSpecified = 1
    End Enum

    ''' <summary>Defines the impeller type for stirred-vessel internal HTC calculation.</summary>
    Public Enum ImpellerType
        ''' <summary>Flat-blade turbine (C = 0.36).</summary>
        FlatBlade = 0
        ''' <summary>Rushton turbine (C = 0.74).</summary>
        Rushton = 1
        ''' <summary>Pitched-blade turbine (C = 0.53).</summary>
        PitchedBlade = 2
        ''' <summary>Marine propeller (C = 0.54).</summary>
        Propeller = 3
        ''' <summary>Anchor impeller (C = 1.0).</summary>
        Anchor = 4
        ''' <summary>Helical ribbon (C = 0.633).</summary>
        HelicalRibbon = 5
    End Enum

    ''' <summary>
    ''' Abstract base class for all reactor unit operations. Provides shared properties such as
    ''' reaction set selection, operating mode, pressure/temperature/heat duty changes, and
    ''' conversion tracking for each reaction and component.
    ''' </summary>
    <System.Serializable()> Public MustInherit Class Reactor

        Inherits UnitOperations.UnitOpBaseClass

        Implements IReactor

        Protected m_reactionSequence As List(Of List(Of String))
        Protected m_reactions As List(Of String)
        Protected m_conversions As Dictionary(Of String, Double)
        Protected m_componentconversions As Dictionary(Of String, Double)
        Protected m_reactionSetID As String = "DefaultSet"
        Protected m_reactionSetName As String = ""
        Protected m_opmode As OperationMode = OperationMode.Adiabatic
        Protected m_dp As Nullable(Of Double)
        Protected m_dt As Nullable(Of Double)
        Protected m_DQ As Nullable(Of Double)

        ''' <summary>Gets or sets the simulation object class category (Reactors).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Reactors

        ''' <summary>Restores the reactor state, including reaction and component conversions, from XML.</summary>
        ''' <param name="data">List of XML elements containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            MyBase.LoadData(data)
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            Try
                For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "ReactionConversions").LastOrDefault.Elements
                    m_conversions.Add(xel2.@ID, Double.Parse(xel2.Value, ci))
                Next
            Catch ex As Exception
            End Try

            Try
                For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "CompoundConversions").LastOrDefault.Elements
                    m_componentconversions.Add(xel2.@ID, Double.Parse(xel2.Value, ci))
                Next
            Catch ex As Exception
            End Try

            Return True

        End Function

        ''' <summary>Serializes the reactor state, including reaction and component conversions, to XML.</summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                .Add(New XElement("ReactionConversions"))
                For Each kvp As KeyValuePair(Of String, Double) In m_conversions
                    .Item(.Count - 1).Add(New XElement("Reaction", New XAttribute("ID", kvp.Key), kvp.Value.ToString(ci)))
                Next
                .Add(New XElement("CompoundConversions"))
                For Each kvp As KeyValuePair(Of String, Double) In m_componentconversions
                    .Item(.Count - 1).Add(New XElement("Compound", New XAttribute("ID", kvp.Key), kvp.Value.ToString(ci)))
                Next
            End With

            Return elements

        End Function

        ''' <summary>
        ''' Returns the index of the phase to read the reacting fluid's properties from.
        ''' </summary>
        ''' <remarks>
        ''' Above the critical point there is one fluid phase and each property package is free to call
        ''' it a liquid or a vapour; when the phase the reaction names is empty and the stream carries a
        ''' single phase, that phase holds the reacting fluid whatever it is called. Reading the named
        ''' phase regardless returns zero for the compressibility factor and the density, which then
        ''' propagates into the reaction rate.
        ''' </remarks>
        Protected Function RxPhaseIndex(ims As IMaterialStream, preferred As Integer) As Integer

            If ims.Phases(preferred).Properties.molarfraction.GetValueOrDefault > 0.0 Then Return preferred

            For Each pidx In New Integer() {2, 3, 4, 5, 6, 7}
                If ims.Phases(pidx).Properties.molarfraction.GetValueOrDefault > 0.9999999 Then Return pidx
            Next

            Return preferred

        End Function

        ''' <summary>
        ''' Calculates the concentration-unit conversion factors for every stoichiometric component in
        ''' the given reaction, based on the reaction basis (molar, mass, activity, fugacity, etc.).
        ''' </summary>
        ''' <param name="rxn">The reaction whose components require conversion factors.</param>
        ''' <param name="ims">The material stream providing phase properties and compositions.</param>
        ''' <returns>A dictionary mapping compound names to their respective conversion factors.</returns>
        Function GetConvFactors(rxn As Reaction, ims As IMaterialStream) As Dictionary(Of String, Double)

            Dim conv As New SystemsOfUnits.Converter

            Dim P As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim T As Double = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim amounts As New Dictionary(Of String, Double)
            Dim val1, val2, val3, Z As Double

            For Each sb As ReactionStoichBase In rxn.Components.Values

                If Not amounts.ContainsKey(sb.CompName) Then amounts.Add(sb.CompName, 0.0#)

                Select Case rxn.ReactionBasis
                    Case ReactionBasis.Activity
                        val1 = ims.Phases(RxPhaseIndex(ims, 3)).Compounds(sb.CompName).ActivityCoeff.GetValueOrDefault
                        val2 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.molecularWeight.GetValueOrDefault
                        val3 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.density.GetValueOrDefault
                        amounts(sb.CompName) = val1 * val2 / val3 / 1000.0
                    Case ReactionBasis.Fugacity
                        Select Case rxn.ReactionPhase
                            Case PhaseName.Vapor
                                val1 = ims.Phases(RxPhaseIndex(ims, 2)).Compounds(sb.CompName).FugacityCoeff.GetValueOrDefault
                                Z = ims.Phases(RxPhaseIndex(ims, 2)).Properties.compressibilityFactor.GetValueOrDefault
                                amounts(sb.CompName) = val1 * Z * 8.314 * T
                            Case PhaseName.Liquid
                                val1 = ims.Phases(RxPhaseIndex(ims, 3)).Compounds(sb.CompName).FugacityCoeff.GetValueOrDefault
                                val2 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.molecularWeight.GetValueOrDefault
                                val3 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.density.GetValueOrDefault
                                amounts(sb.CompName) = val1 * val2 / val3 * P / 1000.0
                            Case PhaseName.Mixture
                                val1 = ims.Phases(0).Compounds(sb.CompName).FugacityCoeff.GetValueOrDefault
                                val2 = ims.Phases(0).Properties.molecularWeight.GetValueOrDefault
                                val3 = ims.Phases(0).Properties.density.GetValueOrDefault
                                amounts(sb.CompName) = val1 * val2 / val3 * P / 1000.0
                        End Select
                    Case ReactionBasis.MassConc
                        Select Case rxn.ReactionPhase
                            Case PhaseName.Vapor
                                val1 = ims.Phases(RxPhaseIndex(ims, 2)).Properties.molecularWeight.GetValueOrDefault
                                amounts(sb.CompName) = 1000 / val1
                            Case PhaseName.Liquid
                                val1 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.molecularWeight.GetValueOrDefault
                                amounts(sb.CompName) = 1000 / val1
                            Case PhaseName.Mixture
                                val1 = ims.Phases(0).Properties.molecularWeight.GetValueOrDefault
                                amounts(sb.CompName) = 1000 / val1
                        End Select
                    Case ReactionBasis.MassFrac
                        Select Case rxn.ReactionPhase
                            Case PhaseName.Vapor
                                val1 = ims.Phases(RxPhaseIndex(ims, 2)).Properties.molecularWeight.GetValueOrDefault
                                Z = ims.Phases(RxPhaseIndex(ims, 2)).Properties.compressibilityFactor.GetValueOrDefault
                                amounts(sb.CompName) = Z * 8.314 * T / P * 1000 / val1
                            Case PhaseName.Liquid
                                val1 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.molecularWeight.GetValueOrDefault
                                val2 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.density.GetValueOrDefault
                                amounts(sb.CompName) = val2 * 1000 / val1
                            Case PhaseName.Mixture
                                val1 = ims.Phases(0).Properties.molecularWeight.GetValueOrDefault
                                val2 = ims.Phases(0).Properties.density.GetValueOrDefault
                                amounts(sb.CompName) = val2 * 1000 / val1
                        End Select
                    Case ReactionBasis.MolarConc
                        amounts(sb.CompName) = 1.0#
                    Case ReactionBasis.MolarFrac
                        Select Case rxn.ReactionPhase
                            Case PhaseName.Vapor
                                Z = ims.Phases(RxPhaseIndex(ims, 2)).Properties.compressibilityFactor.GetValueOrDefault
                                amounts(sb.CompName) = Z * 8.314 * T / P
                            Case PhaseName.Liquid
                                val1 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.molecularWeight.GetValueOrDefault
                                val2 = ims.Phases(RxPhaseIndex(ims, 3)).Properties.density.GetValueOrDefault
                                amounts(sb.CompName) = val1 / val2 / 1000.0
                            Case PhaseName.Mixture
                                val1 = ims.Phases(0).Properties.molecularWeight.GetValueOrDefault
                                val2 = ims.Phases(0).Properties.density.GetValueOrDefault
                                amounts(sb.CompName) = val1 / val2 / 1000.0
                        End Select
                    Case ReactionBasis.PartialPress
                        Z = ims.Phases(RxPhaseIndex(ims, 2)).Properties.compressibilityFactor.GetValueOrDefault
                        amounts(sb.CompName) = Z * 8.314 * T
                End Select

                amounts(sb.CompName) = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(rxn.ConcUnit, amounts(sb.CompName))

            Next

            Return amounts

        End Function

        Private scope As ScriptScope
        Private engine As ScriptEngine

        ''' <summary>
        ''' Evaluates a user-defined Python script that returns the reaction rate for an advanced
        ''' kinetic expression. The script receives the reactor, reaction, temperature, pressure, and
        ''' component amounts as variables and must set the variable <c>r</c> to the computed rate.
        ''' </summary>
        ''' <param name="scriptTitle">The title of the Python script registered in the flowsheet.</param>
        ''' <param name="rc">The reactor executing the reaction.</param>
        ''' <param name="rxn">The reaction whose rate is being computed.</param>
        ''' <param name="T">Temperature (K).</param>
        ''' <param name="P">Pressure (Pa).</param>
        ''' <param name="amounts">Component amounts in the reaction-basis unit.</param>
        ''' <param name="amounts2">Component amounts in alternative units.</param>
        ''' <returns>The reaction rate as calculated by the script.</returns>
        Function ProcessAdvancedKineticReactionRate(scriptTitle As String, rc As Reactor, rxn As Reaction, T As Double, P As Double, amounts As Dictionary(Of String, Double), amounts2 As Dictionary(Of String, Double)) As Double

            If scope Is Nothing Then
                Dim opts As New Dictionary(Of String, Object)()
                opts("Frames") = Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.True
                engine = IronPython.Hosting.Python.CreateEngine(opts)
                engine.Runtime.LoadAssembly(GetType(System.String).Assembly)
                engine.Runtime.LoadAssembly(GetType(Thermodynamics.BaseClasses.ConstantProperties).Assembly)
                engine.Runtime.LoadAssembly(GetType(Drawing.SkiaSharp.GraphicsSurface).Assembly)
                scope = engine.CreateScope()
            End If

            Dim script = FlowSheet.Scripts.Values.Where(Function(x) x.Title = scriptTitle).FirstOrDefault()

            If script Is Nothing Then Throw New Exception("Associated Python Script for Kinetics not found.")

            Dim scripttext = script.ScriptText

            scope = engine.CreateScope()
            scope.SetVariable("Flowsheet", rc.FlowSheet)
            scope.SetVariable("reaction", rxn)
            scope.SetVariable("reactor", rc)
            scope.SetVariable("T", T)
            scope.SetVariable("P", P)
            scope.SetVariable("Amounts", amounts2)
            For Each item In amounts
                scope.SetVariable(item.Key, item.Value)
            Next

            Dim txtcode As String = scripttext
            Dim r As Double = Double.MinValue
            Dim source As ScriptSource = engine.CreateScriptSourceFromString(txtcode, Microsoft.Scripting.SourceCodeKind.Statements)

            Try
                source.Execute(scope)
                r = scope.GetVariable("r")
            Catch ex As Exception
                Dim ops As ExceptionOperations = engine.GetService(Of ExceptionOperations)()
                FlowSheet.ShowMessage("Error running script: " & ops.FormatException(ex).ToString, IFlowsheet.MessageType.GeneralError)
            End Try

            Return r

        End Function

        ''' <summary>Initializes a new instance of the reactor base class with empty reaction collections.</summary>
        Sub New()

            MyBase.CreateNew()

            Me.m_reactionSequence = New List(Of List(Of String))
            Me.m_reactions = New List(Of String)
            Me.m_conversions = New Dictionary(Of String, Double)
            Me.m_componentconversions = New Dictionary(Of String, Double)

        End Sub

        ''' <summary>Gets or sets the specified reactor outlet temperature (K) when <see cref="OperationMode.OutletTemperature"/> is selected.</summary>
        Public Property OutletTemperature As Double = 298.15#

        ''' <summary>Gets or sets the ordered sequence of reaction groups (parallel reactions within each group are solved simultaneously).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property ReactionsSequence() As List(Of List(Of String))
            Get
                Return m_reactionSequence
            End Get
            Set(ByVal value As List(Of List(Of String)))
                m_reactionSequence = value
            End Set
        End Property

        ''' <summary>Gets the dictionary of calculated reaction conversions, keyed by reaction ID.</summary>
        <Xml.Serialization.XmlIgnore()> Public ReadOnly Property Conversions() As Dictionary(Of String, Double)
            Get
                Return Me.m_conversions
            End Get
        End Property

        ''' <summary>Gets the dictionary of calculated component conversions, keyed by compound name.</summary>
        <Xml.Serialization.XmlIgnore()> Public ReadOnly Property ComponentConversions() As Dictionary(Of String, Double) Implements IReactor.ComponentConversions
            Get
                Return Me.m_componentconversions
            End Get
        End Property

        ''' <summary>Gets or sets the list of active reaction IDs assigned to this reactor from the reaction set.</summary>
        <Xml.Serialization.XmlIgnore()> Public Property Reactions() As List(Of String)
            Get
                Return m_reactions
            End Get
            Set(ByVal value As List(Of String))
                m_reactions = value
            End Set
        End Property

        ''' <summary>Gets or sets the thermal operating mode of the reactor (isothermal, adiabatic, outlet temperature, or non-isothermal/non-adiabatic).</summary>
        Public Property ReactorOperationMode() As OperationMode
            Get
                Return Me.m_opmode
            End Get
            Set(ByVal value As OperationMode)
                Me.m_opmode = value
            End Set
        End Property

        ''' <summary>Gets or sets the ID of the reaction set used by this reactor.</summary>
        Public Property ReactionSetID() As String
            Get
                Return Me.m_reactionSetID
            End Get
            Set(ByVal value As String)
                Me.m_reactionSetID = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name of the reaction set used by this reactor.</summary>
        Public Property ReactionSetName() As String
            Get
                Return Me.m_reactionSetName
            End Get
            Set(ByVal value As String)
                Me.m_reactionSetName = value
            End Set
        End Property

        ''' <summary>Gets or sets the pressure drop across the reactor (Pa).</summary>
        Public Property DeltaP() As Nullable(Of Double)
            Get
                Return m_dp
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_dp = value
            End Set
        End Property

        ''' <summary>Gets or sets the temperature change across the reactor (K).</summary>
        Public Property DeltaT() As Nullable(Of Double)
            Get
                Return m_dt
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_dt = value
            End Set
        End Property

        ''' <summary>Gets or sets the heat duty exchanged by the reactor (kW).</summary>
        Public Property DeltaQ() As Nullable(Of Double)
            Get
                Return m_DQ
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_DQ = value
            End Set
        End Property

#Region "Heat Exchange Properties"

        ''' <summary>Gets or sets the coolant flow direction for heat exchange mode.</summary>
        Public Property HeatExchangeCoolantFlowDirection As HeatExchangeCoolantMode = HeatExchangeCoolantMode.ConstantTemperature

        ''' <summary>Gets or sets how the heat transfer area is determined.</summary>
        Public Property HeatExchangeAreaCalculationMode As HeatExchangeAreaMode = HeatExchangeAreaMode.AutoFromGeometry

        ''' <summary>Gets or sets the overall heat transfer coefficient U (W/m2.K).</summary>
        Public Property OverallHeatTransferCoefficient As Double = 500.0

        ''' <summary>Gets or sets the user-specified heat transfer area (m2).</summary>
        Public Property HeatExchangeArea As Double = 10.0

        ''' <summary>Gets or sets the coolant inlet temperature (K).</summary>
        Public Property CoolantInletTemperature As Double = 298.15

        ''' <summary>Gets or sets the calculated coolant outlet temperature (K).</summary>
        Public Property CoolantOutletTemperature As Double = 298.15

        ''' <summary>Gets or sets the coolant mass flow rate (kg/s).</summary>
        Public Property CoolantMassFlowRate As Double = 1.0

        ''' <summary>Gets or sets the coolant specific heat capacity (J/kg.K).</summary>
        Public Property CoolantSpecificHeat As Double = 4180.0

        ''' <summary>Gets or sets whether to use a connected utility material stream instead of simple coolant parameters.</summary>
        Public Property UseUtilityStream As Boolean = False

        ''' <summary>Gets or sets the calculated heat transfer area (m2) when using auto-from-geometry mode.</summary>
        Public Property CalculatedHeatExchangeArea As Double = 0.0

        ''' <summary>Gets or sets whether to calculate the overall HTC from wall properties instead of a single user-specified value.</summary>
        Public Property UseWallProperties As Boolean = False

        ''' <summary>Gets or sets the reactor wall material for thermal conductivity calculation.</summary>
        Public Property WallMaterial As String = "Stainless Steel"

        ''' <summary>Gets or sets the reactor wall thickness (m).</summary>
        Public Property WallThickness As Double = 0.005

        ''' <summary>Gets or sets the internal (process-side) heat transfer coefficient (W/m2.K).</summary>
        Public Property InternalHTC As Double = 1000.0

        ''' <summary>Gets or sets the external (coolant-side) heat transfer coefficient (W/m2.K).</summary>
        Public Property ExternalHTC As Double = 2000.0

        ''' <summary>Gets or sets the calculated overall HTC (W/m2.K) when using wall properties mode.</summary>
        Public Property CalculatedOverallHTC As Double = 0.0

        ''' <summary>Gets or sets whether to auto-calculate the internal (process-side) HTC.</summary>
        Public Property CalculateInternalHTC As Boolean = False

        ''' <summary>Gets or sets whether to auto-calculate the external (coolant-side) HTC.</summary>
        Public Property CalculateExternalHTC As Boolean = False

        ''' <summary>Gets or sets the jacket outer diameter (m) for annular jacket h_o calculation.</summary>
        Public Property JacketDiameter As Double = 0.15

        ''' <summary>Gets or sets the coolant density (kg/m3) for simple coolant mode.</summary>
        Public Property CoolantDensity As Double = 998.0

        ''' <summary>Gets or sets the coolant dynamic viscosity (Pa.s) for simple coolant mode.</summary>
        Public Property CoolantViscosity As Double = 0.001

        ''' <summary>Gets or sets the coolant thermal conductivity (W/m.K) for simple coolant mode.</summary>
        Public Property CoolantThermalConductivity As Double = 0.6

        ''' <summary>Gets or sets the calculated internal HTC (W/m2.K).</summary>
        Public Property CalculatedInternalHTC As Double = 0.0

        ''' <summary>Gets or sets the calculated external HTC (W/m2.K).</summary>
        Public Property CalculatedExternalHTC As Double = 0.0

        ''' <summary>
        ''' Returns the thermal conductivity of the reactor wall material (W/m.K) at a given temperature (K).
        ''' Same correlations as the Pipe unit operation.
        ''' </summary>
        Public Shared Function GetWallThermalConductivity(material As String, T As Double) As Double
            Select Case material
                Case "Steel"
                    Return -0.000000004 * T ^ 3 - 0.00002 * T ^ 2 + 0.021 * T + 33.743
                Case "Carbon Steel"
                    Return 0.000000007 * T ^ 3 - 0.00002 * T ^ 2 - 0.0291 * T + 70.765
                Case "Cast Iron"
                    Return -0.00000008 * T ^ 3 + 0.0002 * T ^ 2 - 0.211 * T + 127.99
                Case "Stainless Steel"
                    Return 14.6 + 0.0127 * (T - 273.15)
                Case "PVC"
                    Return 0.16
                Case "Commercial Copper"
                    Return 420.75 - 0.068493 * T
                Case Else
                    Return 16.0 ' default fallback (approx. stainless steel at 25°C)
            End Select
        End Function

        ''' <summary>
        ''' Computes the effective overall HTC from wall properties: 1/U = 1/h_i + delta_w/k_w + 1/h_o.
        ''' Uses calculated h_i / h_o when the auto-calculate flags are set.
        ''' Returns the value in W/m2.K.
        ''' </summary>
        Public Function ComputeEffectiveHTC(T As Double) As Double
            Dim k_w As Double = GetWallThermalConductivity(WallMaterial, T)
            Dim hi As Double = If(CalculateInternalHTC AndAlso CalculatedInternalHTC > 0, CalculatedInternalHTC, InternalHTC)
            Dim ho As Double = If(CalculateExternalHTC AndAlso CalculatedExternalHTC > 0, CalculatedExternalHTC, ExternalHTC)
            Dim resistance As Double = 0.0
            If hi > 0 Then resistance += 1.0 / hi
            If k_w > 0 AndAlso WallThickness > 0 Then resistance += WallThickness / k_w
            If ho > 0 Then resistance += 1.0 / ho
            If resistance > 0 Then
                Return 1.0 / resistance
            Else
                Return OverallHeatTransferCoefficient
            End If
        End Function

        ''' <summary>
        ''' Computes the Dittus-Boelter convective HTC for flow inside a tube.
        ''' Nu = 0.023 * Re^0.8 * Pr^n (n=0.4 heating, n=0.3 cooling).
        ''' Returns h in W/m2.K.
        ''' </summary>
        ''' <param name="D">Tube inner diameter (m)</param>
        ''' <param name="rho">Fluid density (kg/m3)</param>
        ''' <param name="mu">Fluid dynamic viscosity (Pa.s)</param>
        ''' <param name="Cp">Fluid specific heat (J/kg.K)</param>
        ''' <param name="k">Fluid thermal conductivity (W/m.K)</param>
        ''' <param name="massFlow">Total mass flow rate through all tubes (kg/s)</param>
        ''' <param name="nTubes">Number of parallel tubes</param>
        ''' <param name="heating">True if fluid is being heated (Tc > T)</param>
        Public Shared Function DittusBoelterHTC(D As Double, rho As Double, mu As Double,
                                                Cp As Double, k As Double,
                                                massFlow As Double, nTubes As Integer,
                                                heating As Boolean) As Double
            If D <= 0 OrElse rho <= 0 OrElse mu <= 0 OrElse k <= 0 OrElse Cp <= 0 Then Return 0.0
            Dim A_tube As Double = Math.PI / 4.0 * D * D
            Dim velocity As Double = massFlow / (rho * nTubes * A_tube)
            If velocity <= 0 Then Return 0.0
            Dim Re As Double = rho * velocity * D / mu
            Dim Pr As Double = mu * Cp / k
            If Re < 1 OrElse Pr < 0.001 Then Return 0.0
            Dim n As Double = If(heating, 0.4, 0.3)
            Dim Nu As Double = 0.023 * Re ^ 0.8 * Pr ^ n
            ' Ensure minimum for laminar flow (Nu ~ 3.66 for constant wall T)
            If Nu < 3.66 Then Nu = 3.66
            Return Nu * k / D
        End Function

        ''' <summary>
        ''' Computes the convective HTC for flow through an annular jacket using Dittus-Boelter
        ''' with the hydraulic diameter D_h = D_jacket - D_outer.
        ''' Returns h in W/m2.K.
        ''' </summary>
        ''' <param name="D_outer">Vessel/tube outer diameter (m)</param>
        ''' <param name="D_jacket">Jacket outer diameter (m)</param>
        ''' <param name="rho_c">Coolant density (kg/m3)</param>
        ''' <param name="mu_c">Coolant dynamic viscosity (Pa.s)</param>
        ''' <param name="Cp_c">Coolant specific heat (J/kg.K)</param>
        ''' <param name="k_c">Coolant thermal conductivity (W/m.K)</param>
        ''' <param name="massFlow_c">Coolant mass flow rate (kg/s)</param>
        Public Shared Function AnnularJacketHTC(D_outer As Double, D_jacket As Double,
                                                rho_c As Double, mu_c As Double,
                                                Cp_c As Double, k_c As Double,
                                                massFlow_c As Double) As Double
            If D_jacket <= D_outer OrElse rho_c <= 0 OrElse mu_c <= 0 OrElse k_c <= 0 OrElse Cp_c <= 0 Then Return 0.0
            Dim D_h As Double = D_jacket - D_outer ' hydraulic diameter for annulus
            Dim A_annulus As Double = Math.PI / 4.0 * (D_jacket * D_jacket - D_outer * D_outer)
            If A_annulus <= 0 Then Return 0.0
            Dim velocity As Double = massFlow_c / (rho_c * A_annulus)
            If velocity <= 0 Then Return 0.0
            Dim Re As Double = rho_c * velocity * D_h / mu_c
            Dim Pr As Double = mu_c * Cp_c / k_c
            If Re < 1 OrElse Pr < 0.001 Then Return 0.0
            Dim Nu As Double = 0.023 * Re ^ 0.8 * Pr ^ 0.4
            If Nu < 3.66 Then Nu = 3.66
            Return Nu * k_c / D_h
        End Function

        ''' <summary>
        ''' Computes the internal (process-side) HTC for a stirred vessel using
        ''' Nu = C * Re_imp^(2/3) * Pr^(1/3), where Re_imp = rho * N * d_imp^2 / mu.
        ''' Returns h in W/m2.K.
        ''' </summary>
        Public Shared Function GetImpellerNusseltConstant(impType As ImpellerType) As Double
            Select Case impType
                Case ImpellerType.FlatBlade : Return 0.36
                Case ImpellerType.Rushton : Return 0.74
                Case ImpellerType.PitchedBlade : Return 0.53
                Case ImpellerType.Propeller : Return 0.54
                Case ImpellerType.Anchor : Return 1.0
                Case ImpellerType.HelicalRibbon : Return 0.633
                Case Else : Return 0.36
            End Select
        End Function

        Public Shared Function StirredVesselHTC(D_vessel As Double, d_imp As Double,
                                                N_rpm As Double, C_imp As Double,
                                                rho As Double, mu As Double,
                                                Cp As Double, k As Double) As Double
            If D_vessel <= 0 OrElse d_imp <= 0 OrElse N_rpm <= 0 OrElse rho <= 0 OrElse mu <= 0 OrElse k <= 0 OrElse Cp <= 0 Then Return 0.0
            Dim N_rps As Double = N_rpm / 60.0 ' rev/s
            Dim Re_imp As Double = rho * N_rps * d_imp * d_imp / mu
            Dim Pr As Double = mu * Cp / k
            If Re_imp < 1 OrElse Pr < 0.001 Then Return 0.0
            Dim Nu As Double = C_imp * Re_imp ^ (2.0 / 3.0) * Pr ^ (1.0 / 3.0)
            Return Nu * k / D_vessel
        End Function

#End Region

#Region "    Reaction Expression Evaluation"

        ''' <summary>Contexts and compiled expressions of the reactions this reactor evaluates.</summary>
        ''' <remarks>
        ''' The cache belongs to the reactor and not to the reaction because a reaction is a
        ''' flowsheet-level object that two reactors may share, and the solver calculates unit
        ''' operations in parallel.
        ''' </remarks>
        <NonSerialized> Private _expressions As ExpressionCache

        ''' <summary>
        ''' Discards the compiled expressions. Call it at the start of a calculation so that a
        ''' reaction edited between two runs is picked up.
        ''' </summary>
        Protected Sub ResetExpressionCache()

            _expressions = Nothing

        End Sub

        ''' <summary>Returns the expression context of a reaction, creating it on first use.</summary>
        ''' <param name="reactionID">Identifier of the reaction the expressions belong to.</param>
        Protected Function GetExpressionContext(reactionID As String) As Flee.PublicTypes.ExpressionContext

            If _expressions Is Nothing Then _expressions = New ExpressionCache()

            Return _expressions.GetContext(reactionID)

        End Function

        ''' <summary>Sets a variable on an expression context, defining it if it is not there yet.</summary>
        Protected Shared Sub SetExpressionVariable(context As Flee.PublicTypes.ExpressionContext, name As String, value As Double)

            ExpressionCache.SetVariable(context, name, value)

        End Sub

        ''' <summary>
        ''' Compiles an expression against the reaction's context and keeps the compiled form for
        ''' the rest of the calculation.
        ''' </summary>
        ''' <param name="reactionID">Identifier of the reaction the expression belongs to.</param>
        ''' <param name="expression">The expression text, as the user wrote it.</param>
        Protected Function GetCompiledExpression(reactionID As String, expression As String) As Flee.PublicTypes.IGenericExpression(Of Double)

            If _expressions Is Nothing Then _expressions = New ExpressionCache()

            Return _expressions.GetCompiled(reactionID, expression)

        End Function

#End Region

    End Class

End Namespace
