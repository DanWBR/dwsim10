'    Valve Calculation Routines 
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

Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.Interfaces.Enums
Imports DotNumerics.Optimization.TN
Imports NetOffice.ExcelApi

Namespace UnitOperations

    ''' <summary>
    ''' Represents a valve unit operation that models a pressure letdown device.
    ''' The valve reduces stream pressure via an isenthalpic (adiabatic) or isentropic flash,
    ''' optionally using flow coefficient (Kv/Cv) equations for liquid, gas, steam, or two-phase service.
    ''' </summary>
    <System.Serializable()> Public Partial Class Valve

        Inherits UnitOperations.UnitOpBaseClass
        ''' <summary>
        ''' Gets the simulation object class category for this valve (PressureChangers).
        ''' </summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.PressureChangers

        ''' <summary>
        ''' Defines the relationship type between valve stem opening percentage and the flow coefficient (Kv/Cv).
        ''' </summary>
        Public Enum OpeningKvRelationshipType
            ''' <summary>Flow coefficient increases linearly with valve opening.</summary>
            Linear = 0
            ''' <summary>Flow coefficient increases by an equal percentage of the previous value for each percent of stem travel.</summary>
            EqualPercentage = 1
            ''' <summary>Provides large flow with small opening; flow coefficient increases rapidly at low opening percentages.</summary>
            QuickOpening = 2
            ''' <summary>Flow coefficient is defined by a user-supplied mathematical expression of opening percentage.</summary>
            UserDefined = 3
            ''' <summary>Flow coefficient is determined by interpolation from a user-supplied data table of opening vs. Kv values.</summary>
            DataTable = 4
        End Enum

        ''' <summary>
        ''' Gets the list of equipment sub-types supported by this valve object.
        ''' </summary>
        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Ball", "Gate", "Butterfly"}
            End Get
        End Property

        ''' <summary>
        ''' Creates the list of physical dimensions associated with this valve (diameter).
        ''' </summary>
        Public Overrides Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Diameter, .IsUserDefined = False})

        End Sub

        ''' <summary>
        ''' Updates the valve's estimated diameter dimension based on the current volumetric flow rate and Kv coefficient.
        ''' </summary>
        Public Overrides Sub UpdateDimensionsList()

            Dimensions(0).Value = GetInletMaterialStream(0).GetVolumetricFlow() * 15850.323140625 / Kv * 10.67 * 0.0254 'm

        End Sub

        ''' <summary>
        ''' Gets a value indicating whether this valve supports dynamic simulation mode.
        ''' </summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>
        ''' Gets a value indicating whether this valve exposes properties specific to dynamic mode.
        ''' </summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = True

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Protected m_dp As Double?
        Protected m_dt As Double?
        Protected m_DQ As Double?
        Protected m_Pout As Double? = 101325.0#
        Protected m_cmode As CalculationMode = CalculationMode.DeltaP
        ''' <summary>
        ''' Gets or sets the specific enthalpy of the inlet stream in kJ/kg.
        ''' </summary>
        Public Property Hinlet As Double
        ''' <summary>
        ''' Gets or sets the specific enthalpy of the outlet stream in kJ/kg.
        ''' </summary>
        Public Property Houtlet As Double

        ''' <summary>
        ''' Gets or sets the maximum flow coefficient (Kv or Cv depending on <see cref="FlowCoefficient"/>) for the valve at fully open position.
        ''' </summary>
        Public Property Kv As Double = 100.0#

        ''' <summary>
        ''' Gets or sets the actual (effective) flow coefficient calculated during the last simulation step, accounting for the current opening percentage.
        ''' </summary>
        Public Property ActualKv As Double = 0.0

        Private _opening As Double = 50.0

        ''' <summary>
        ''' Gets or sets the valve stem opening as a percentage (0–100).
        ''' In dynamic mode with actuator delay configured, setting this value enqueues the new opening
        ''' for deferred application rather than applying it immediately.
        ''' </summary>
        Public Property OpeningPct As Double
            Get
                Return _opening
            End Get
            Set(value As Double)
                If FlowSheet IsNot Nothing Then
                    If FlowSheet.DynamicMode AndAlso DelayedOpenings IsNot Nothing Then
                        Dim AD As Double = GetDynamicProperty("Actuator Delay")
                        If AD > 0.0 Then
                            DelayedOpenings.Enqueue(value)
                        Else
                            _opening = value
                        End If
                    Else
                        _opening = value
                    End If
                Else
                    _opening = value
                End If
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the pressure differential ratio factor (xT) used in gas/two-phase Kv calculations. Default is 0.75.
        ''' </summary>
        Public Property xT As Double = 0.75

        ''' <summary>
        ''' Gets or sets the liquid pressure recovery factor (FL) of the valve. Default is 0.9.
        ''' </summary>
        Public Property FL As Double = 0.9

        ''' <summary>
        ''' Gets or sets the piping geometry factor (FP) accounting for inlet/outlet fittings. Default is 1.0.
        ''' </summary>
        Public Property FP As Double = 1.0

        ''' <summary>
        ''' Gets or sets the valve style modifier (Fs). Default is 1.0.
        ''' </summary>
        Public Property Fs As Double = 1.0

        ''' <summary>
        ''' Gets or sets the incipient cavitation factor (Fi). Default is 0.9.
        ''' </summary>
        Public Property Fi As Double = 0.9

        ''' <summary>
        ''' Gets or sets the numerical constant N6 used in the gas Kv sizing equation (SI units). Default is 31.6.
        ''' </summary>
        Public Property N6 As Double = 31.6

        ''' <summary>
        ''' Gets or sets the mathematical expression that relates valve stem opening percentage (variable OP) to the
        ''' effective Kv as a percentage of the maximum Kv. Used when <see cref="EnableOpeningKvRelationship"/> is True
        ''' and <see cref="DefinedOpeningKvRelationShipType"/> is UserDefined.
        ''' </summary>
        Public Property PercentOpeningVersusPercentKvExpression As String = "1.0*OP"

        ''' <summary>
        ''' Gets or sets a value indicating whether the opening/Kv relationship feature is active.
        ''' When True, the effective Kv is calculated from the stem opening using the selected characteristic curve.
        ''' </summary>
        Public Property EnableOpeningKvRelationship As Boolean = False

        ''' <summary>
        ''' Gets or sets the rangeability (characteristic) parameter used in the equal-percentage flow characteristic calculation.
        ''' Default is 50.
        ''' </summary>
        Public Property CharacteristicParameter As Double = 50

        ''' <summary>
        ''' Gets or sets the type of opening/Kv characteristic curve to apply when <see cref="EnableOpeningKvRelationship"/> is True.
        ''' </summary>
        Public Property DefinedOpeningKvRelationShipType As OpeningKvRelationshipType = OpeningKvRelationshipType.UserDefined

        ''' <summary>
        ''' Gets or sets the X-axis data points (opening percentage values) for the tabulated opening/Kv relationship.
        ''' </summary>
        Public Property OpeningKvRelDataTableX As New List(Of Double)

        ''' <summary>
        ''' Gets or sets the Y-axis data points (Kv percentage values) for the tabulated opening/Kv relationship.
        ''' </summary>
        Public Property OpeningKvRelDataTableY As New List(Of Double)

        ''' <summary>
        ''' Gets or sets whether the flow coefficient is expressed as Kv (metric, m³/h at 1 bar drop) or Cv (imperial).
        ''' </summary>
        Public Property FlowCoefficient As FlowCoefficientType = FlowCoefficientType.Kv

        ''' <summary>
        ''' Gets or sets the estimated valve body diameter in metres, calculated from flow and Kv.
        ''' </summary>
        Public Property EstimatedDiameter As Double

        Private ActuatorTimeToNext As New DateTime


        Private DelayedOpenings As New Queue(Of Double)

        'proxy properties

        ''' <summary>
        ''' Returns an array of strings describing all available calculation modes for this valve.
        ''' </summary>
        ''' <returns>An array of formatted strings listing each <see cref="CalculationMode"/> name and its integer ID.</returns>
        Public Overrides Function GetCalculationModes() As String()

            Dim modes As New List(Of String)

            For Each tstEnum As CalculationMode In System.Enum.GetValues(GetType(CalculationMode))
                modes.Add(String.Format("Name: {0}  ID: {1}", tstEnum.ToString, CInt(tstEnum).ToString()))
            Next

            Return modes.ToArray()

        End Function

        ''' <summary>
        ''' Sets the valve calculation mode by its integer identifier and returns the name of the new mode.
        ''' </summary>
        ''' <param name="modeID">Integer identifier corresponding to a <see cref="CalculationMode"/> value.</param>
        ''' <returns>The string name of the newly applied <see cref="CalculationMode"/>.</returns>
        Public Overrides Function SetCalculationMode(modeID As Integer) As Object

            Me.CalcMode = modeID

            Return CalcMode.ToString()

        End Function

        ''' <summary>
        ''' Gets or sets the heat duty (kJ/s) associated with the valve. For an adiabatic valve this is always zero.
        ''' Proxies <see cref="DeltaQ"/>.
        ''' </summary>
        Public Property HeatDuty As Double
            Get
                Return DeltaQ.GetValueOrDefault()
            End Get
            Set(value As Double)
                DeltaQ = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the pressure drop (Pa) across the valve.
        ''' Proxies <see cref="DeltaP"/>.
        ''' </summary>
        Public Property PressureDrop As Double
            Get
                Return DeltaP.GetValueOrDefault()
            End Get
            Set(value As Double)
                DeltaP = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the temperature change (K) across the valve as a result of the pressure letdown.
        ''' Proxies <see cref="DeltaT"/>.
        ''' </summary>
        Public Property TemperatureChange As Double
            Get
                Return DeltaT.GetValueOrDefault()
            End Get
            Set(value As Double)
                DeltaT = value
            End Set
        End Property

        ''' <summary>
        ''' Specifies the unit system used to express the valve flow coefficient.
        ''' </summary>
        Public Enum FlowCoefficientType
            ''' <summary>Metric flow coefficient Kv (m³/h at 1 bar differential pressure with water at 20 °C).</summary>
            Kv = 0
            ''' <summary>Imperial flow coefficient Cv (US gal/min at 1 psi differential pressure with water at 60 °F). Cv = 1.16 × Kv.</summary>
            Cv = 1
        End Enum

        ''' <summary>
        ''' Defines how the valve outlet condition is determined during steady-state calculation.
        ''' </summary>
        Public Enum CalculationMode
            ''' <summary>Outlet pressure is calculated from a user-specified pressure drop (ΔP).</summary>
            DeltaP = 0
            ''' <summary>Pressure drop is calculated from a user-specified outlet pressure.</summary>
            OutletPressure = 1
            ''' <summary>Outlet pressure is calculated from the flow coefficient (Kv/Cv) equation for liquid service.</summary>
            Kv_Liquid = 2
            ''' <summary>Outlet pressure is calculated from the flow coefficient (Kv/Cv) equation for gas service.</summary>
            Kv_Gas = 3
            ''' <summary>Outlet pressure is calculated from a simplified steam sizing equation.</summary>
            Kv_Steam = 4
            ''' <summary>Outlet pressure is calculated using the appropriate Kv equation selected automatically based on stream phase.</summary>
            Kv_General = 5
        End Enum

        ''' <summary>
        ''' Initializes a new default instance of the <see cref="Valve"/> class.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Valve"/> class with the specified name and description.
        ''' </summary>
        ''' <param name="name">The display name assigned to this valve object.</param>
        ''' <param name="description">A short description of this valve object.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>
        ''' Creates a deep copy of this valve by serialising and deserialising via the custom XML mechanism.
        ''' </summary>
        ''' <returns>A new <see cref="Valve"/> instance with identical property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Valve()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Creates a deep copy of this valve by serialising and deserialising via JSON.
        ''' </summary>
        ''' <returns>A new <see cref="Valve"/> instance with identical property values.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Valve)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Gets or sets the specified or calculated outlet pressure (Pa).
        ''' </summary>
        Public Property OutletPressure() As Nullable(Of Double)
            Get
                Return m_Pout
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_Pout = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the active calculation mode that determines how the valve outlet condition is computed.
        ''' </summary>
        Public Property CalcMode() As CalculationMode
            Get
                Return m_cmode
            End Get
            Set(ByVal value As CalculationMode)
                m_cmode = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the pressure drop (Pa) across the valve (P_inlet − P_outlet).
        ''' </summary>
        Public Property DeltaP() As Nullable(Of Double)
            Get
                Return m_dp
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_dp = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the temperature change (K) across the valve (T_outlet − T_inlet).
        ''' </summary>
        Public Property DeltaT() As Nullable(Of Double)
            Get
                Return m_dt
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_dt = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the heat duty (kJ/s) of the valve. Always zero for an adiabatic valve.
        ''' </summary>
        Public Property DeltaQ() As Nullable(Of Double)
            Get
                Return m_DQ
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_DQ = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the calculated outlet stream temperature (K).
        ''' </summary>
        Property OutletTemperature As Double

        ''' <summary>
        ''' Registers dynamic simulation properties for this valve, specifically the actuator delay.
        ''' </summary>
        Public Overrides Sub CreateDynamicProperties()

            AddDynamicProperty("Actuator Delay", "Valve Actuator Delay (dead time in seconds). Set to 0 to disable.", 0, UnitOfMeasure.time, 1.0.GetType())
            AddDynamicProperty("Actuator Time Constant", "First-order lag time constant for actuator response (s). Set to 0 for instantaneous.", 0.0, UnitOfMeasure.time, 1.0.GetType())
            AddDynamicProperty("Opening Setpoint", "Target valve opening (%). Actuator moves towards this value.", 100.0, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Cavitation Alarm", "True if cavitation is detected (dP > FL^2*(P1-Pv)).", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Liquid Pressure Recovery Factor FL", "Liquid pressure recovery factor (typically 0.8-0.95).", 0.9, UnitOfMeasure.none, 1.0.GetType())

        End Sub

        ''' <summary>
        ''' Executes the valve model for one dynamic simulation time step, updating mass flow rates and
        ''' pressures on connected streams based on the current opening and flow coefficient.
        ''' Handles actuator delay by deferring opening changes when configured.
        ''' </summary>
        Public Overrides Sub RunDynamicModel()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            If Not integrator.ShouldCalculatePressureFlow Then Exit Sub

            Dim timestep = integrator.IntegrationStep.TotalSeconds
            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0

            Dim AD As Double = GetDynamicProperty("Actuator Delay")
            Dim tau As Double = GetDynamicProperty("Actuator Time Constant")

            If tau > 0.0 Then
                Dim targetOP As Double = GetDynamicProperty("Opening Setpoint")
                OpeningPct = OpeningPct + (targetOP - OpeningPct) * (1.0 - Math.Exp(-timestep / tau))
            End If

            If AD > 0.0 Then

                Dim DT0 = (integrator.CurrentTime - New Date).TotalSeconds

                If DT0 = 0.0 Then
                    ActuatorTimeToNext = New Date()
                    DelayedOpenings = New Queue(Of Double)
                Else
                    ActuatorTimeToNext = ActuatorTimeToNext.Add(integrator.IntegrationStep)
                End If

                Dim DT = (ActuatorTimeToNext - New Date).TotalSeconds

                If DT >= AD AndAlso DelayedOpenings.Count > 0 Then
                    ActuatorTimeToNext = New Date()
                    OpeningPct = DelayedOpenings.Dequeue()
                End If

            End If

            Dim ims As MaterialStream = Me.GetInletMaterialStream(0)
            Dim oms As MaterialStream = Me.GetOutletMaterialStream(0)

            Dim Ti, P1, Hi, Wi, ei, ein, P2, H2, rho, volf, rhog20, P2ant, v2, Kvc, Pv, Pc, rhol, rhog, k, Cp_ig As Double
            Dim massfrac_gas, massfrac_liq As Double
            Dim icount As Integer

            Me.PropertyPackage.CurrentMaterialStream = ims

            Ti = ims.Phases(0).Properties.temperature.GetValueOrDefault
            P1 = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Hi = ims.Phases(0).Properties.enthalpy.GetValueOrDefault
            Wi = ims.Phases(0).Properties.massflow.GetValueOrDefault
            ei = Hi * Wi
            ein = ei
            rho = ims.Phases(0).Properties.density.GetValueOrDefault
            volf = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault

            H2 = Hi

            Select Case CalcMode

                Case CalculationMode.OutletPressure, CalculationMode.DeltaP

                    If ims.DynamicsSpec = Dynamics.DynamicsSpecType.Flow And
                        oms.DynamicsSpec = Dynamics.DynamicsSpecType.Flow Then

                        Throw New Exception("Inlet and Outlet Streams cannot be both Flow-spec'd at the same time.")

                    ElseIf ims.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure And
                         oms.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure Then

                        Throw New Exception("Inlet and Outlet Streams cannot be both Pressure-spec'd at the same time.")

                    ElseIf ims.DynamicsSpec = Dynamics.DynamicsSpecType.Flow And
                                oms.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure Then

                        Throw New Exception("Inlet Flow + Outlet Pressure specifications not supported by this calculation mode.")

                    ElseIf ims.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure And
                                oms.DynamicsSpec = Dynamics.DynamicsSpecType.Flow Then

                        If CalcMode = CalculationMode.OutletPressure Then
                            P2 = OutletPressure.GetValueOrDefault()
                        Else
                            P2 = P1 - DeltaP.GetValueOrDefault
                        End If

                    End If

                    DeltaP = P1 - P2
                    OutletPressure = P2

                    With oms
                        .AtEquilibrium = False
                        .Phases(0).Properties.temperature = Ti
                        .Phases(0).Properties.pressure = P2
                        .Phases(0).Properties.enthalpy = H2
                        Dim comp As BaseClasses.Compound
                        Dim i As Integer = 0
                        For Each comp In .Phases(0).Compounds.Values
                            comp.MoleFraction = ims.Phases(0).Compounds(comp.Name).MoleFraction
                            comp.MassFraction = ims.Phases(0).Compounds(comp.Name).MassFraction
                            comp.MassFlow = comp.MassFraction * Wi
                            comp.MolarFlow = comp.MassFlow / comp.ConstantProperties.Molar_Weight * 1000
                            i += 1
                        Next
                    End With

                    Wi = oms.GetMassFlow
                    If Double.IsNaN(Wi) Or Double.IsInfinity(Wi) Or Wi < 0.0 Then Wi = 1.0E-20

                    If ims.MaximumAllowableDynamicMassFlowRate.HasValue Then
                        Dim WiMax = ims.MaximumAllowableDynamicMassFlowRate.Value
                        If Wi > WiMax Then
                            ims.SetMassFlow(WiMax)
                            oms.SetMassFlow(WiMax)
                        Else
                            ims.SetMassFlow(Wi)
                            oms.SetMassFlow(Wi)
                        End If
                    Else
                        ims.SetMassFlow(Wi)
                        oms.SetMassFlow(Wi)
                    End If

                    ims.SetMassFlow(Wi)

                Case Else

                    Dim FC As Double 'flow coefficient

                    If FlowCoefficient = FlowCoefficientType.Cv Then
                        'Cv = 1.16 Kv
                        'Kv = Cv / 1.16
                        FC = Kv / 1.16
                    Else
                        FC = Kv
                    End If

                    If EnableOpeningKvRelationship Then
                        Select Case DefinedOpeningKvRelationShipType
                            Case OpeningKvRelationshipType.UserDefined
                                Try
                                    Dim ExpContext As New Flee.PublicTypes.ExpressionContext
                                    ExpContext.Imports.AddType(GetType(System.Math))
                                    ExpContext.Variables.Clear()
                                    ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture
                                    ExpContext.Variables.Add("OP", OpeningPct)
                                    Dim Expr = ExpContext.CompileGeneric(Of Double)(PercentOpeningVersusPercentKvExpression)
                                    Kvc = FC * Expr.Evaluate() / 100
                                Catch ex As Exception
                                    Throw New Exception("Invalid expression for Kv[Cv]/Opening relationship.")
                                End Try
                            Case OpeningKvRelationshipType.QuickOpening
                                Kvc = (OpeningPct / 100.0) ^ 0.5 * FC
                            Case OpeningKvRelationshipType.Linear
                                Kvc = OpeningPct / 100.0 * FC
                            Case OpeningKvRelationshipType.EqualPercentage
                                Kvc = CharacteristicParameter ^ (OpeningPct / 100.0 - 1.0) * FC
                            Case OpeningKvRelationshipType.DataTable
                                Try
                                    Dim factor = MathNet.Numerics.Interpolate.RationalWithoutPoles(OpeningKvRelDataTableX, OpeningKvRelDataTableY).Interpolate(OpeningPct) / 100.0
                                    Kvc = factor * FC
                                Catch ex As Exception
                                    Throw New Exception("Error calculating Kv from tabulated data: " + ex.Message)
                                End Try
                        End Select
                    Else
                        Kvc = FC
                    End If

                    If ims.DynamicsSpec = Dynamics.DynamicsSpecType.Flow And
                        oms.DynamicsSpec = Dynamics.DynamicsSpecType.Flow Then

                        'not supported

                        Throw New Exception("Inlet and Outlet Streams cannot be both Flow-spec'd at the same time.")

                    ElseIf ims.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure And
                         oms.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure Then

                        'valid! calculate flow

                        P2 = oms.GetPressure

                        If CalcMode = CalculationMode.Kv_General Or CalcMode = CalculationMode.Kv_Gas Or CalcMode = CalculationMode.Kv_Liquid Then
                            If ims.Phases(1).Properties.molarfraction > 0.99 Or CalcMode = CalculationMode.Kv_Liquid Then
                                Wi = Kvc * (1000.0 * rho * (P1 - P2) / 100000.0) ^ 0.5 / 3600
                            ElseIf ims.Phases(2).Properties.molarfraction > 0.99 Or CalcMode = CalculationMode.Kv_Gas Then
                                ims.PropertyPackage.CurrentMaterialStream = ims
                                rhog20 = ims.PropertyPackage.AUX_VAPDENS(273.15, 101325)
                                If P2 > P1 / 2 Then
                                    Wi = 519 * Kvc / (Ti / (rhog20 * (P1 - P2) / 100000.0 * P1 / 100000.0)) ^ 0.5 / 3600
                                Else
                                    Wi = 259.5 * Kvc * P1 / 100000.0 / (Ti / rhog20) ^ 0.5 / 3600
                                End If
                            Else
                                ims.PropertyPackage.CurrentMaterialStream = ims
                                rhog = ims.Phases(2).Properties.density.GetValueOrDefault
                                Cp_ig = ims.PropertyPackage.AUX_CPm(PropertyPackages.Phase.Vapor, Ti) * ims.Phases(2).Properties.molecularWeight.GetValueOrDefault
                                k = Cp_ig / (Cp_ig - 8.314)
                                rhol = ims.Phases(1).Properties.density.GetValueOrDefault
                                Pc = ims.PropertyPackage.AUX_PCM(PropertyPackages.Phase.Liquid)
                                Pv = ims.PropertyPackage.AUX_PVAPM(PropertyPackages.Phase.Liquid, Ti)

                                massfrac_gas = ims.Phases(2).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                                massfrac_liq = ims.Phases(1).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault

                                If Double.IsNaN(massfrac_gas) Or Double.IsNaN(massfrac_liq) Then
                                    Wi = 0.0
                                Else
                                    Wi = WTwoPhase(Kvc, P1 / 100000.0, P2 / 100000.0, rhog, rhol, k, Pv / 100000.0, Pc / 100000.0, massfrac_gas, massfrac_liq)
                                End If

                            End If
                        ElseIf CalcMode = CalculationMode.Kv_Steam Then
                            If P2 > P1 / 2 Then
                                v2 = 1 / ims.PropertyPackage.AUX_VAPDENS(Ti, P2)
                                Wi = Kvc * 31.62 / (v2 / ((P1 - P2) / 100000.0)) ^ 0.5 / 3600
                            Else
                                v2 = 1 / ims.PropertyPackage.AUX_VAPDENS(Ti, P1 / 2)
                                Wi = Kvc * 31.62 / (2 * v2 / (P1 / 100000.0)) ^ 0.5 / 3600
                            End If
                        End If

                        If Double.IsNaN(Wi) Or Double.IsInfinity(Wi) Or Wi < 0.0 Then Wi = 0.0

                        If ims.MaximumAllowableDynamicMassFlowRate.HasValue Then
                            Dim WiMax = ims.MaximumAllowableDynamicMassFlowRate.Value
                            If Wi > WiMax Then
                                ims.SetMassFlow(WiMax)
                                oms.SetMassFlow(WiMax)
                            Else
                                ims.SetMassFlow(Wi)
                                oms.SetMassFlow(Wi)
                            End If
                        Else
                            ims.SetMassFlow(Wi)
                            oms.SetMassFlow(Wi)
                        End If

                    ElseIf ims.DynamicsSpec = Dynamics.DynamicsSpecType.Flow And
                                oms.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure Then

                        'valid! calculate P1

                        If Double.IsNaN(Wi) Or Double.IsInfinity(Wi) Or Wi < 0.0 Then Wi = 0.0

                        oms.SetMassFlow(Wi)

                        P2 = oms.GetPressure()

                        If CalcMode = CalculationMode.Kv_General Or CalcMode = CalculationMode.Kv_Gas Or CalcMode = CalculationMode.Kv_Liquid Then
                            If ims.Phases(1).Properties.molarfraction = 1 Or CalcMode = CalculationMode.Kv_Liquid Then
                                P1 = P2 / 100000.0 + 1 / (1000.0 * rho) * (Wi * 3600 / Kvc) ^ 2
                            ElseIf ims.Phases(2).Properties.molarfraction = 1 Or CalcMode = CalculationMode.Kv_Gas Then
                                ims.PropertyPackage.CurrentMaterialStream = ims
                                rhog20 = ims.PropertyPackage.AUX_VAPDENS(273.15, 101325)
                                P1 = P2 / 100000.0 + Ti / rhog20 / (P2 / 100000) * (519 * Kvc / (Wi * 3600)) ^ -2
                            Else
                                ims.PropertyPackage.CurrentMaterialStream = ims
                                rhog20 = ims.PropertyPackage.AUX_VAPDENS(273.15, 101325)
                                rhol = ims.Phases(1).Properties.density.GetValueOrDefault
                                massfrac_gas = ims.Phases(2).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                                massfrac_liq = ims.Phases(1).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                                P1 = P1TwoPhase(Wi * 3600, Kvc, P2 / 100000.0, Ti, rhog20, rhol, massfrac_gas, massfrac_liq)
                            End If
                        ElseIf CalcMode = CalculationMode.Kv_Steam Then
                            v2 = 1 / ims.PropertyPackage.AUX_VAPDENS(Ti, P2)
                            P1 = P2 / 100000.0 + v2 * (31.62 * Kvc / (Wi * 3600)) ^ -2
                        End If
                        P1 = P1 * 100000.0
                        ims.SetPressure(P1)

                    ElseIf ims.DynamicsSpec = Dynamics.DynamicsSpecType.Pressure And
                                oms.DynamicsSpec = Dynamics.DynamicsSpecType.Flow Then

                        Wi = oms.GetMassFlow

                        If Double.IsNaN(Wi) Or Double.IsInfinity(Wi) Or Wi < 0.0 Then Wi = 1.0E-20

                        ims.SetMassFlow(Wi)

                        'valid! Calculate P2

                        If CalcMode = CalculationMode.Kv_General Or CalcMode = CalculationMode.Kv_Gas Or CalcMode = CalculationMode.Kv_Liquid Then
                            If ims.Phases(1).Properties.molarfraction = 1 Or CalcMode = CalculationMode.Kv_Liquid Then
                                P2 = P1 / 100000.0 - 1 / (1000.0 * rho) * (Wi * 3600 / Kvc) ^ 2
                                P2 = P2 * 100000.0
                            ElseIf ims.Phases(2).Properties.molarfraction = 1 Or CalcMode = CalculationMode.Kv_Gas Then
                                ims.PropertyPackage.CurrentMaterialStream = ims
                                rhog20 = ims.PropertyPackage.AUX_VAPDENS(273.15, 101325)
                                Dim roots = MathOps.Quadratic.quadForm(-rhog20, rhog20 * P1 / 100000, -Ti * (519 * Kvc / (Wi * 3600)) ^ -2)
                                If roots.Item1 > 0 And roots.Item1 > P1 / 100000 / 2 Then
                                    P2 = roots.Item1 * 100000.0
                                ElseIf roots.Item2 > 0 And roots.Item2 > P1 / 100000 / 2 Then
                                    P2 = roots.Item2 * 100000.0
                                Else
                                    Throw New Exception("Unable to calculate the outlet pressure.")
                                End If
                            Else
                                ims.PropertyPackage.CurrentMaterialStream = ims
                                rhog = ims.Phases(2).Properties.density.GetValueOrDefault
                                Cp_ig = ims.PropertyPackage.AUX_CPm(PropertyPackages.Phase.Vapor, Ti) * ims.Phases(2).Properties.molecularWeight()
                                k = Cp_ig / (Cp_ig - 8.314)
                                rhol = ims.Phases(1).Properties.density.GetValueOrDefault
                                Pc = ims.PropertyPackage.AUX_PCM(PropertyPackages.Phase.Liquid)
                                Pv = P1 'ims.PropertyPackage.AUX_PVAPM(PropertyPackages.Phase.Liquid, Ti)

                                massfrac_gas = ims.Phases(2).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                                massfrac_liq = ims.Phases(1).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                                P2 = 100000.0 * P2TwoPhase(Wi * 3600, Kvc, P1 / 100000.0, rhog, rhol, k, Pv / 100000.0, Pc / 100000.0, massfrac_gas, massfrac_liq)
                            End If
                        ElseIf CalcMode = CalculationMode.Kv_Steam Then
                            P2 = P1 * 0.7 / 100000.0
                            icount = 0
                            Do
                                v2 = 1 / ims.PropertyPackage.AUX_VAPDENS(Ti, P2)
                                P2ant = P2
                                P2 = P1 / 100000.0 - v2 * (31.62 * Kvc / (Wi * 3600)) ^ -2
                                icount += 1
                                If icount > 10000 Then Throw New Exception("P2 did not converge in 10000 iterations.")
                            Loop Until Math.Abs(P2 - P2ant) < 0.0001
                            P2 = P2 * 100000.0
                        End If
                    End If

                    DeltaP = P1 - P2
                    OutletPressure = P2

                    ActualKv = Kvc

                    With oms
                        .Phases(0).Properties.temperature = Ti
                        .Phases(0).Properties.pressure = P2
                        .Phases(0).Properties.enthalpy = H2
                        .SetFlashSpec("PH")
                        Dim comp As BaseClasses.Compound
                        Dim i As Integer = 0
                        For Each comp In .Phases(0).Compounds.Values
                            comp.MoleFraction = ims.Phases(0).Compounds(comp.Name).MoleFraction
                            comp.MassFraction = ims.Phases(0).Compounds(comp.Name).MassFraction
                            comp.MassFlow = comp.MassFraction * Wi
                            comp.MolarFlow = comp.MassFlow / comp.ConstantProperties.Molar_Weight * 1000
                            i += 1
                        Next
                        .SetMassFlow(Wi)
                    End With

                    With ims
                        Dim comp As BaseClasses.Compound
                        Dim i As Integer = 0
                        For Each comp In .Phases(0).Compounds.Values
                            comp.MassFlow = comp.MassFraction * Wi
                            comp.MolarFlow = comp.MassFlow / comp.ConstantProperties.Molar_Weight * 1000
                            i += 1
                        Next
                        .SetMassFlow(Wi)
                    End With

            End Select

            Dim FL As Double = GetDynamicProperty("Liquid Pressure Recovery Factor FL")
            Dim cavitating As Boolean = False

            If FL > 0 AndAlso P1 > 0 Then
                Dim PvCalc As Double = 0.0
                Try
                    Me.PropertyPackage.CurrentMaterialStream = ims
                    PvCalc = ims.PropertyPackage.AUX_PVAPM(PropertyPackages.Phase.Liquid, Ti)
                Catch
                End Try
                If PvCalc > 0 AndAlso (P1 - P2) > FL * FL * (P1 - PvCalc) Then
                    cavitating = True
                End If
            End If

            SetDynamicProperty("Cavitation Alarm", cavitating)

        End Sub

        ''' <summary>
        ''' Calculates the valve flow coefficient (Kv) for single-phase liquid service using the simplified ISA equation.
        ''' </summary>
        ''' <param name="Wi">Mass flow rate in kg/h.</param>
        ''' <param name="rho">Liquid density in kg/m³.</param>
        ''' <param name="P1">Inlet pressure in Pa.</param>
        ''' <param name="P2">Outlet pressure in Pa.</param>
        ''' <returns>The calculated Kv value (m³/h at 1 bar drop).</returns>
        Public Function SimpleKvLiquid(Wi As Double, rho As Double, P1 As Double, P2 As Double) As Double

            SimpleKvLiquid = Wi * 3600 / (1000.0 * rho * (P1 - P2) / 100000.0) ^ 0.5
        End Function

        ''' <summary>
        ''' Calculates the valve flow coefficient (Kv) for single-phase gas service using the simplified ISA equation.
        ''' Applies the choked-flow correction when the downstream pressure falls below half the upstream pressure.
        ''' </summary>
        ''' <param name="Wi">Mass flow rate in kg/h.</param>
        ''' <param name="rhog20">Gas density at standard conditions (273.15 K, 101325 Pa) in kg/m³.</param>
        ''' <param name="P1">Inlet pressure in Pa.</param>
        ''' <param name="P2">Outlet pressure in Pa.</param>
        ''' <param name="Ti">Inlet temperature in K.</param>
        ''' <returns>The calculated Kv value (m³/h at 1 bar drop).</returns>
        Public Function SimpleKvGas(Wi As Double, rhog20 As Double, P1 As Double, P2 As Double, Ti As Double) As Double
            If P2 > P1 / 2 Then
                SimpleKvGas = Wi * 3600 / 519 * (Ti / (rhog20 * (P1 - P2) / 100000.0 * P2 / 100000.0)) ^ 0.5
            Else
                SimpleKvGas = Wi * 3600 / 259.5 / P1 * (Ti / rhog20) ^ 0.5
            End If
        End Function

        ''' <summary>
        ''' Calculates the ratio of specific heats factor F_k = k / 1.4, used in gas Kv sizing equations.
        ''' </summary>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the gas.</param>
        ''' <returns>The dimensionless factor F_k.</returns>
        Public Function F_k(k As Double) As Double

            F_k = k / 1.4

        End Function

        ''' <summary>
        ''' Calculates the expansion factor Y, which accounts for the change in gas density as it flows
        ''' through the valve restriction (per ANSI/ISA-75.01.01).
        ''' </summary>
        ''' <param name="x">Pressure differential ratio (P1 − P2) / P1.</param>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the gas.</param>
        ''' <param name="xT">Pressure differential ratio factor at choked flow for the valve style.</param>
        ''' <returns>The dimensionless expansion factor Y (1 at no pressure drop, decreasing toward choke).</returns>
        Public Function Y_factor(x As Double, k As Double, xT As Double) As Double
            Y_factor = 1 - x / (3 * x_choked(k, xT))

        End Function

        ''' <summary>
        ''' Calculates the pressure differential ratio x = (P1 − P2) / P1.
        ''' </summary>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <returns>The dimensionless pressure differential ratio x.</returns>
        Public Function x_ratio(P1 As Double, P2 As Double) As Double

            x_ratio = (P1 - P2) / P1

        End Function

        ''' <summary>
        ''' Calculates the choked-flow pressure differential ratio x_choked = F_k × xT.
        ''' Flow is choked when the actual ratio x exceeds this value.
        ''' </summary>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the gas.</param>
        ''' <param name="xT">Pressure differential ratio factor at choked flow for the valve style.</param>
        ''' <returns>The dimensionless choked-flow pressure differential ratio.</returns>
        Public Function x_choked(k As Double, xT As Double) As Double
            x_choked = F_k(k) * xT
        End Function


        ''' <summary>
        ''' Calculates the liquid critical pressure ratio factor F_F = 0.96 − 0.28 × √(Pv/Pc),
        ''' used to determine the effective pressure at which choked liquid flow begins.
        ''' </summary>
        ''' <param name="Pv">Liquid vapour pressure at inlet temperature in bar.</param>
        ''' <param name="Pc">Mixture critical pressure in bar.</param>
        ''' <returns>The dimensionless liquid critical pressure ratio factor F_F.</returns>
        Public Function F_F(Pv As Double, Pc As Double) As Double

            F_F = 0.96 - 0.28 * (Pv / Pc) ^ 0.5

        End Function

        ''' <summary>
        ''' Calculates the combined Kv for two-phase (gas/liquid) flow using the Masoneilan blending method.
        ''' </summary>
        ''' <param name="Wi">Total mass flow rate in kg/h.</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <param name="rhog">Vapour phase density at inlet in kg/m³.</param>
        ''' <param name="rhol">Liquid phase density at inlet in kg/m³.</param>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the vapour phase.</param>
        ''' <param name="Pv">Liquid vapour pressure at inlet temperature in bar.</param>
        ''' <param name="Pc">Mixture critical pressure in bar.</param>
        ''' <param name="massfrac_gas">Mass fraction of the vapour phase.</param>
        ''' <param name="massfrac_liq">Mass fraction of the liquid phase.</param>
        ''' <returns>The effective two-phase Kv value (m³/h at 1 bar drop).</returns>
        Public Function KvTwoPhase(Wi As Double, P1 As Double, P2 As Double, rhog As Double, rhol As Double, k As Double, Pv As Double, Pc As Double, massfrac_gas As Double, massfrac_liq As Double) As Double

            KvTwoPhase = (massfrac_gas * KvGas(Wi, P1, P2, k, rhog) ^ 2 + massfrac_liq * KvLiquid(Wi, P1, P2, rhol, Pv, Pc) ^ 2) ^ 0.5
        End Function

        ''' <summary>
        ''' Calculates the total mass flow rate through the valve for two-phase service given a known Kv.
        ''' Uses the Masoneilan blending method to combine liquid and gas contributions.
        ''' </summary>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <param name="rhog">Vapour phase density at inlet in kg/m³.</param>
        ''' <param name="rhol">Liquid phase density at inlet in kg/m³.</param>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the vapour phase.</param>
        ''' <param name="Pv">Liquid vapour pressure at inlet temperature in bar.</param>
        ''' <param name="Pc">Mixture critical pressure in bar.</param>
        ''' <param name="massfrac_gas">Mass fraction of the vapour phase.</param>
        ''' <param name="massfrac_liq">Mass fraction of the liquid phase.</param>
        ''' <returns>The total mass flow rate in kg/h.</returns>
        Public Function WTwoPhase(Kv As Double, P1 As Double, P2 As Double, rhog As Double, rhol As Double, k As Double, Pv As Double, Pc As Double, massfrac_gas As Double, massfrac_liq As Double) As Double
            WTwoPhase = 1 / (massfrac_liq / WLiquid(Kv, P1, P2, rhol, Pv, Pc) ^ 2 + massfrac_gas / WGas(Kv, P1, P2, k, rhog) ^ 2) ^ 0.5
        End Function

        ''' <summary>
        ''' Calculates the total mass flow rate through the valve for two-phase service using simplified
        ''' (non-ISA) liquid and gas equations, suitable for iterative pressure calculations.
        ''' </summary>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P1">Inlet pressure in Pa.</param>
        ''' <param name="P2">Outlet pressure in Pa.</param>
        ''' <param name="Ti">Inlet temperature in K.</param>
        ''' <param name="rhog20">Gas density at standard conditions (273.15 K, 101325 Pa) in kg/m³.</param>
        ''' <param name="rhol">Liquid phase density at inlet in kg/m³.</param>
        ''' <param name="massfrac_gas">Mass fraction of the vapour phase.</param>
        ''' <param name="massfrac_liq">Mass fraction of the liquid phase.</param>
        ''' <returns>The total mass flow rate in kg/h.</returns>
        Public Function SimpleWTwoPhase(Kv As Double, P1 As Double, P2 As Double, Ti As Double, rhog20 As Double, rhol As Double, massfrac_gas As Double, massfrac_liq As Double) As Double
            Dim Wliquid, Wgas As Double

            Wliquid = Kv * (1000.0 * rhol * (P1 - P2)) ^ 0.5
            If P2 > P1 / 2 Then
                Wgas = 519 * Kv / (Ti / (rhog20 * (P1 - P2) * P1)) ^ 0.5
            Else
                Wgas = 259.5 * Kv * P1 / (Ti / rhog20) ^ 0.5
            End If

            SimpleWTwoPhase = 1 / (massfrac_liq / Wliquid ^ 2 + massfrac_gas / Wgas ^ 2) ^ 0.5
        End Function

        ''' <summary>
        ''' Calculates the valve flow coefficient (Kv) for single-phase liquid service per ANSI/ISA-75.01.01,
        ''' applying the choked-flow limit based on F_L and F_F.
        ''' </summary>
        ''' <param name="Wi">Mass flow rate in kg/h.</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <param name="rho">Liquid relative density (specific gravity referenced to water at 15 °C).</param>
        ''' <param name="Pv">Liquid vapour pressure at inlet temperature in bar.</param>
        ''' <param name="Pc">Mixture critical pressure in bar.</param>
        ''' <returns>The calculated Kv value (m³/h at 1 bar drop).</returns>
        Public Function KvLiquid(Wi As Double, P1 As Double, P2 As Double, rho As Double, Pv As Double, Pc As Double) As Double
            Dim dP_choke

            dP_choke = FL ^ 2 * (P1 - F_F(Pv, Pc) * Pv)
            If dP_choke > 0 And dP_choke < (P1 - P2) Then
                P2 = P1 - dP_choke
            End If

            KvLiquid = Wi / FP / (rho * 999.1 * (P1 - P2)) ^ 0.5
        End Function

        ''' <summary>
        ''' Calculates the mass flow rate through the valve for single-phase liquid service given a known Kv,
        ''' applying the choked-flow limit based on F_L and F_F.
        ''' </summary>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <param name="rho">Liquid relative density (specific gravity referenced to water at 15 °C).</param>
        ''' <param name="Pv">Liquid vapour pressure at inlet temperature in bar.</param>
        ''' <param name="Pc">Mixture critical pressure in bar.</param>
        ''' <returns>The liquid mass flow rate in kg/h.</returns>
        Public Function WLiquid(Kv As Double, P1 As Double, P2 As Double, rho As Double, Pv As Double, Pc As Double) As Double
            Dim dP_choke

            dP_choke = FL ^ 2 * (P1 - F_F(Pv, Pc) * Pv)
            If dP_choke > 0 And dP_choke < (P1 - P2) Then
                P2 = P1 - dP_choke
            End If
            WLiquid = Kv * FP * (rho * 999.1 * (P1 - P2)) ^ 0.5

        End Function

        ''' <summary>
        ''' Iteratively calculates the inlet pressure P1 for two-phase service given a known mass flow rate,
        ''' outlet pressure and flow coefficient, using Newton's method.
        ''' </summary>
        ''' <param name="Wi">Total mass flow rate in kg/h.</param>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <param name="Ti">Inlet temperature in K.</param>
        ''' <param name="rhog20">Gas density at standard conditions (273.15 K, 101325 Pa) in kg/m³.</param>
        ''' <param name="rhol">Liquid phase density in kg/m³.</param>
        ''' <param name="massfrac_gas">Mass fraction of the vapour phase.</param>
        ''' <param name="massfrac_liq">Mass fraction of the liquid phase.</param>
        ''' <returns>The calculated inlet pressure P1 in bar.</returns>
        Public Function P1TwoPhase(Wi As Double, Kv As Double, P2 As Double, Ti As Double, rhog20 As Double, rhol As Double, massfrac_gas As Double, massfrac_liq As Double) As Double

            Dim x_c, Wtemp, Werror, P1, dP1, dW As Double
            Dim icount As Integer

            P1 = P2 * 1.1
            dP1 = 0.001
            Wtemp = SimpleWTwoPhase(Kv, P1, P2, Ti, rhog20, rhol, massfrac_gas, massfrac_liq)

            icount = 0
            Do While (Math.Abs(Wi - Wtemp) > 0.1)
                Werror = Wi - Wtemp
                dW = SimpleWTwoPhase(Kv, P1 + dP1, P2, Ti, rhog20, rhol, massfrac_gas, massfrac_liq) - Wtemp
                P1 = P1 + 0.5 * dP1 / dW * (Werror)
                If P1 < P2 Then P1 = P2 + 0.0001
                Wtemp = SimpleWTwoPhase(Kv, P1, P2, Ti, rhog20, rhol, massfrac_gas, massfrac_liq)

                If icount > 1000 Then Throw New Exception("P1 did not converge in 1000 iterations.")
                icount += 1
            Loop

            P1TwoPhase = P1

        End Function

        ''' <summary>
        ''' Calculates the outlet pressure P2 for two-phase service given a known mass flow rate,
        ''' inlet conditions and flow coefficient using bisection search.
        ''' </summary>
        ''' <param name="Wi">Total mass flow rate in kg/h.</param>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="rhog">Vapour phase density at inlet in kg/m³.</param>
        ''' <param name="rhol">Liquid phase density at inlet in kg/m³.</param>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the vapour phase.</param>
        ''' <param name="Pv">Liquid vapour pressure at inlet temperature in bar.</param>
        ''' <param name="Pc">Mixture critical pressure in bar.</param>
        ''' <param name="massfrac_gas">Mass fraction of the vapour phase.</param>
        ''' <param name="massfrac_liq">Mass fraction of the liquid phase.</param>
        ''' <returns>The calculated outlet pressure P2 in bar.</returns>
        Public Function P2TwoPhase(Wi As Double, Kv As Double, P1 As Double, rhog As Double, rhol As Double, k As Double, Pv As Double, Pc As Double, massfrac_gas As Double, massfrac_liq As Double) As Double
            Dim P2_high, P2_low, P2_mid, x_c As Double
            Dim icount As Integer

            x_c = x_choked(k, xT)
            P2_high = P1
            P2_low = P2_high - P2_high * x_c

            If P2_low > (P1 - FL ^ 2 * (P1 - F_F(Pv, Pc) * Pv)) Then
                P2_low = P1 - FL ^ 2 * (P1 - F_F(Pv, Pc) * Pv)
            End If

            If WTwoPhase(Kv, P1, P2_low, rhog, rhol, k, Pv, Pc, massfrac_gas, massfrac_liq) < Wi Then
                Throw New Exception("Valve capacity too small, increase Kv")
            Else
                Do While Math.Abs(P2_high - P2_low) > 0.001
                    P2_mid = (P2_high + P2_low) / 2
                    If WTwoPhase(Kv, P1, P2_mid, rhog, rhol, k, Pv, Pc, massfrac_gas, massfrac_liq) > Wi Then
                        P2_low = P2_mid
                    Else
                        P2_high = P2_mid
                    End If
                    If icount > 1000 Then Throw New Exception("P2 did not converge in 1000 iterations.")
                    icount += 1
                Loop
            End If
            P2TwoPhase = (P2_high + P2_low) / 2
        End Function

        ''' <summary>
        ''' Calculates the outlet pressure P2 for single-phase liquid service given a known mass flow rate
        ''' and flow coefficient, applying the choked-flow limit.
        ''' </summary>
        ''' <param name="Wi">Mass flow rate in kg/h.</param>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="rho">Liquid relative density (specific gravity referenced to water at 15 °C).</param>
        ''' <param name="Pv">Liquid vapour pressure at inlet temperature in bar.</param>
        ''' <param name="Pc">Mixture critical pressure in bar.</param>
        ''' <returns>The calculated outlet pressure P2 in bar.</returns>
        Public Function P2Liquid(Wi As Double, Kv As Double, P1 As Double, rho As Double, Pv As Double, Pc As Double) As Double

            Dim P2_high, P2_low As Double

            P2_high = P1
            P2_low = P1 - FL ^ 2 * (P1 - F_F(Pv, Pc) * Pv)

            Dim Wic = Kv * FP * (rho * 999.1 * (P1 - P2_low)) ^ 0.5

            If Wic < Wi Then
                Throw New Exception("Valve capacity too small, increase Kv")
            Else
                P2Liquid = P1 - 1 / (999.1 * rho) * (Wi / (Kv * FP)) ^ 2
            End If

        End Function

        ''' <summary>
        ''' Calculates the valve flow coefficient (Kv) for single-phase gas service per ANSI/ISA-75.01.01,
        ''' applying the choked-flow correction via Y and x_choked.
        ''' </summary>
        ''' <param name="Wi">Mass flow rate in kg/h.</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the gas.</param>
        ''' <param name="rho">Gas density at inlet conditions in kg/m³.</param>
        ''' <returns>The calculated Kv value (m³/h at 1 bar drop).</returns>
        Public Function KvGas(Wi As Double, P1 As Double, P2 As Double, k As Double, rho As Double)
            Dim Y, x, x_c As Double

            x = x_ratio(P1, P2)
            x_c = x_choked(k, xT)

            If x > x_c Then
                x = x_c
            End If

            Y = Y_factor(x, k, xT)

            KvGas = Wi * 1 / (N6 * FP * Y) / (x * P1 * rho) ^ 0.5
        End Function

        ''' <summary>
        ''' Calculates the gas mass flow rate through the valve given a known Kv per ANSI/ISA-75.01.01,
        ''' applying the choked-flow correction via Y and x_choked.
        ''' </summary>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="P2">Outlet pressure in bar.</param>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the gas.</param>
        ''' <param name="rho">Gas density at inlet conditions in kg/m³.</param>
        ''' <returns>The gas mass flow rate in kg/h.</returns>
        Public Function WGas(Kv As Double, P1 As Double, P2 As Double, k As Double, rho As Double)
            Dim Y, x, x_c As Double

            x = x_ratio(P1, P2)
            x_c = x_choked(k, xT)

            If x > x_c Then
                x = x_c
            End If

            Y = Y_factor(x, k, xT)

            WGas = Kv * (N6 * FP * Y) * (x * P1 * rho) ^ 0.5
        End Function


        ''' <summary>
        ''' Calculates the outlet pressure P2 for single-phase gas service given a known mass flow rate
        ''' and flow coefficient, using bisection search within the non-choked pressure range.
        ''' </summary>
        ''' <param name="Wi">Gas mass flow rate in kg/h.</param>
        ''' <param name="Kv">Effective flow coefficient (m³/h at 1 bar drop).</param>
        ''' <param name="P1">Inlet pressure in bar.</param>
        ''' <param name="k">Ratio of specific heats (Cp/Cv) of the gas.</param>
        ''' <param name="rho">Gas density at inlet conditions in kg/m³.</param>
        ''' <returns>The calculated outlet pressure P2 in bar.</returns>
        Public Function P2_Gas(Wi As Double, Kv As Double, P1 As Double, k As Double, rho As Double)
            Dim P2_high, P2_low, P2_mid, x_c As Double
            Dim icount As Integer

            x_c = x_choked(k, xT)
            P2_high = P1
            P2_low = P2_high - P2_high * x_c

            icount = 0
            If (Kv * N6 * FP * Y_factor(x_c, k, xT) * (x_c * P1 * rho) ^ 0.5) < Wi Then
                Throw New Exception("Valve capacity too small, increase Kv")
            Else
                Do While Math.Abs(P2_high - P2_low) > 0.001
                    P2_mid = (P2_high + P2_low) / 2
                    If WGas(Kv, P1, P2_mid, k, rho) > Wi Then
                        P2_low = P2_mid
                    Else
                        P2_high = P2_mid
                    End If
                    If icount > 1000 Then Throw New Exception("P2 did not converge in 1000 iterations.")
                    icount += 1
                Loop
            End If
            P2_Gas = (P2_high + P2_low) / 2
        End Function

        ''' <summary>
        ''' Calculates and updates the valve's maximum flow coefficient (Kv or Cv) from the current inlet and
        ''' outlet stream conditions.  The appropriate sizing equation is selected based on the current
        ''' <see cref="CalcMode"/> and the phase state of the inlet stream.
        ''' Also back-calculates the full-open Kv from the opening/Kv relationship when enabled.
        ''' </summary>
        Public Sub CalculateKv()

            Dim Ti, P1, Hi, Wi, ei, P2, rho, rhog20, rhog, rhol, volf, k, v2, Cp_ig, Pv, Pc As Double
            Dim massfrac_liq, massfrac_gas As Double

            Dim ims As MaterialStream = Me.GetInletMaterialStream(0)
            Dim oms As MaterialStream = Me.GetOutletMaterialStream(0)

            Me.PropertyPackage.CurrentMaterialStream = ims
            Me.PropertyPackage.CurrentMaterialStream.Validate()

            Ti = ims.Phases(0).Properties.temperature.GetValueOrDefault
            P1 = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Hi = ims.Phases(0).Properties.enthalpy.GetValueOrDefault
            Wi = ims.Phases(0).Properties.massflow.GetValueOrDefault
            ei = Hi * Wi
            rho = ims.Phases(0).Properties.density.GetValueOrDefault
            volf = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault

            P2 = oms.Phases(0).Properties.pressure.GetValueOrDefault

            If Me.CalcMode = CalculationMode.DeltaP Then
                P2 = P1 - Me.DeltaP.GetValueOrDefault
            ElseIf CalcMode = CalculationMode.OutletPressure Then
                P2 = Me.OutletPressure.GetValueOrDefault
            Else
                P2 = oms.Phases(0).Properties.pressure.GetValueOrDefault
            End If


            If CalcMode = CalculationMode.Kv_Steam Then
                If P2 > P1 / 2 Then
                    v2 = 1 / ims.PropertyPackage.AUX_VAPDENS(Ti, P2)
                    Kv = Wi * 3600 / 31.62 * (v2 / ((P1 - P2) / 100000.0)) ^ 0.5
                Else
                    v2 = 1 / ims.PropertyPackage.AUX_VAPDENS(Ti, P1 / 2)
                    Kv = Wi * 3600 / 31.62 * (2 * v2 / (P1 / 100000.0)) ^ 0.5
                End If
            Else
                If ims.Phases(2).Properties.molarfraction = 1 Or CalcMode = CalculationMode.Kv_Gas Then
                    ims.PropertyPackage.CurrentMaterialStream = ims
                    rho = ims.PropertyPackage.AUX_VAPDENS(Ti, P1)

                    Cp_ig = ims.PropertyPackage.AUX_CPm(PropertyPackages.Phase.Vapor, Ti) * ims.Phases(2).Properties.molecularWeight()
                    k = Cp_ig / (Cp_ig - 8.314)
                    Kv = KvGas(Wi * 3600, P1 / 100000.0, P2 / 100000.0, k, rho)
                ElseIf ims.Phases(1).Properties.molarfraction = 1 Or CalcMode = CalculationMode.Kv_Liquid Then
                    Pv = ims.PropertyPackage.AUX_PVAPM(Ti)
                    Pc = ims.PropertyPackage.AUX_PCM(PropertyPackages.Phase.Liquid)
                    rho = ims.Phases(1).Properties.density.GetValueOrDefault
                    Kv = KvLiquid(Wi * 3600, P1 / 100000.0, P2 / 100000.0, rho, Pv / 100000.0, Pc / 100000.0)
                ElseIf ims.Phases(2).Properties.molarfraction > 0 And ims.Phases(1).Properties.molarfraction > 0 Then
                    ims.PropertyPackage.CurrentMaterialStream = ims
                    rhog = ims.Phases(2).Properties.density.GetValueOrDefault
                    Cp_ig = ims.PropertyPackage.AUX_CPm(PropertyPackages.Phase.Vapor, Ti) * ims.Phases(2).Properties.molecularWeight()
                    k = Cp_ig / (Cp_ig - 8.314)
                    rhol = ims.Phases(1).Properties.density.GetValueOrDefault
                    Pc = ims.PropertyPackage.AUX_PCM(PropertyPackages.Phase.Liquid)
                    Pv = P1 'ims.PropertyPackage.AUX_PVAPM(PropertyPackages.Phase.Liquid, Ti)

                    massfrac_gas = ims.Phases(2).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                    massfrac_liq = ims.Phases(1).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault

                    Kv = KvTwoPhase(Wi * 3600, P1 / 100000.0, P2 / 100000.0, rhog, rhol, k, Pv / 100000.0, Pc / 100000.0, massfrac_gas, massfrac_liq)
                End If
            End If

            If EnableOpeningKvRelationship Then
                Try
                    Dim ExpContext As New Flee.PublicTypes.ExpressionContext
                    ExpContext.Imports.AddType(GetType(System.Math))
                    ExpContext.Variables.Clear()
                    ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture
                    ExpContext.Variables.Add("OP", OpeningPct)

                    Dim Expr = ExpContext.CompileGeneric(Of Double)(PercentOpeningVersusPercentKvExpression)
                    Kv = Kv / (Expr.Evaluate() / 100)

                Catch ex As Exception
                    Throw New Exception("Invalid expression for Kv/Opening relationship.")
                End Try
            End If

            If FlowCoefficient = FlowCoefficientType.Cv Then
                'Cv = 1.16 Kv
                Kv = 1.16 * Kv
            End If

        End Sub

        ''' <summary>
        ''' Performs the steady-state valve calculation.  Determines the outlet pressure from the selected
        ''' <see cref="CalcMode"/> (DeltaP, OutletPressure, or Kv-based), then performs an isenthalpic
        ''' (PH) flash to find the outlet temperature and phase state.
        ''' </summary>
        ''' <param name="args">
        ''' Optional two-element array: args(0) is the inlet <see cref="MaterialStream"/>,
        ''' args(1) is the outlet <see cref="MaterialStream"/>. When Nothing, connected streams are used.
        ''' </param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("The Valve works like a fixed pressure drop for the process, where 
                                the outlet material stream properties are calculated beginning 
                                from the principle that the expansion is an isenthalpic process.")

            IObj?.Paragraphs.Add("The outlet stream pressure is calculated from the inlet pressure 
                                and the pressure drop. The outlet stream temperature is found by 
                                doing a PH Flash. This way, in the majority of cases, the outlet 
                                temperature will be less than or equal to the inlet one.")

            If args Is Nothing Then
                If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                    Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
                ElseIf Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                    Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
                End If
            End If

            Dim Ti, Pi, Hi, Wi, ei, ein, T2, P2, H2, H2c, rho, volf, rhog20, P2ant, v2, Kvc, T2est As Double
            Dim Cp_ig, k, Pv, Pc, rhog, rhol, massfrac_gas, massfrac_liq As Double
            Dim icount As Integer

            Dim ims, oms As MaterialStream

            If args IsNot Nothing Then
                ims = args(0)
                oms = args(1)
            Else
                ims = Me.GetInletMaterialStream(0)
                oms = Me.GetOutletMaterialStream(0)
            End If

            Me.PropertyPackage.CurrentMaterialStream = ims
            Me.PropertyPackage.CurrentMaterialStream.Validate()
            Ti = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Pi = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Hi = ims.Phases(0).Properties.enthalpy.GetValueOrDefault
            Wi = ims.Phases(0).Properties.massflow.GetValueOrDefault
            ei = Hi * Wi
            ein = ei
            rho = ims.Phases(0).Properties.density.GetValueOrDefault
            volf = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault

            If oms.GetTemperature() < ims.GetTemperature() Then
                T2est = oms.GetTemperature()
            Else
                T2est = ims.GetTemperature()
            End If

            H2 = Hi '- Me.DeltaP.GetValueOrDefault / (rho_li * 1000)

            If DebugMode Then AppendDebugLine(String.Format("Property Package: {0}", Me.PropertyPackage.Name))
            If DebugMode Then AppendDebugLine(String.Format("Input variables: T = {0} K, P = {1} Pa, H = {2} kJ/kg, W = {3} kg/s", Ti, Pi, Hi, Wi))

            Dim FC As Double 'flow coefficient

            If FlowCoefficient = FlowCoefficientType.Cv Then
                'Cv = 1.16 Kv
                'Kv = Cv / 1.16
                FC = Kv / 1.16
            Else
                FC = Kv
            End If

            If EnableOpeningKvRelationship Then
                IObj?.Paragraphs.Add("<h2>Opening/Kv[Cv] relationship</h2>")
                IObj?.Paragraphs.Add("When this feature is enabled, you can enter an expression that relates the valve stem opening with the maximum flow value (Kvmax).")
                IObj?.Paragraphs.Add("The relationship between control valve capacity and valve stem travel is known as the Flow Characteristic of the 
                                    Control Valve. Trim design of the valve affects how the control valve capacity changes as the valve moves through 
                                    its complete travel. Because of the variation in trim design, many valves are not linear in nature. Valve trims 
                                    are instead designed, or characterized, in order to meet the large variety of control application needs. Many 
                                    control loops have inherent non linearity's, which may be possible to compensate selecting the control valve trim.")
                IObj?.Paragraphs.Add("<img src='https://www.engineeringtoolbox.com/docs/documents/485/Control_Valve_Flow_Characteristics.gif'></img>")
                Select Case DefinedOpeningKvRelationShipType
                    Case OpeningKvRelationshipType.UserDefined
                        Try
                            Dim ExpContext As New Flee.PublicTypes.ExpressionContext()
                            ExpContext.Imports.AddType(GetType(System.Math))
                            ExpContext.Variables.Clear()
                            ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture
                            ExpContext.Variables.Add("OP", OpeningPct)
                            IObj?.Paragraphs.Add("Current Opening (%): " & OpeningPct)
                            IObj?.Paragraphs.Add("Opening/Kv[Cv]max relationship expression: " & PercentOpeningVersusPercentKvExpression)
                            Dim Expr = ExpContext.CompileGeneric(Of Double)(PercentOpeningVersusPercentKvExpression)
                            Kvc = FC * Expr.Evaluate() / 100
                            IObj?.Paragraphs.Add("Calculated Kv[Cv]/Kv[Cv]max (%): " & Kvc / FC * 100)
                            IObj?.Paragraphs.Add("Calculated Kv: " & Kvc)
                        Catch ex As Exception
                            Throw New Exception("Invalid expression for Kv[Cv]/Opening relationship.")
                        End Try
                    Case OpeningKvRelationshipType.QuickOpening
                        IObj?.Paragraphs.Add("Current Opening (%): " & OpeningPct)
                        Kvc = (OpeningPct / 100.0) ^ 0.5 * FC
                        IObj?.Paragraphs.Add("Calculated Kv[Cv]/Kv[Cv]max (%): " & Kvc / FC * 100)
                        IObj?.Paragraphs.Add("Calculated Kv: " & Kvc)
                    Case OpeningKvRelationshipType.Linear
                        IObj?.Paragraphs.Add("Current Opening (%): " & OpeningPct)
                        Kvc = OpeningPct / 100.0 * FC
                        IObj?.Paragraphs.Add("Calculated Kv[Cv]/Kv[Cv]max (%): " & Kvc / FC * 100)
                        IObj?.Paragraphs.Add("Calculated Kv: " & Kvc)
                    Case OpeningKvRelationshipType.EqualPercentage
                        IObj?.Paragraphs.Add("Current Opening (%): " & OpeningPct)
                        Kvc = CharacteristicParameter ^ (OpeningPct / 100.0 - 1.0) * FC
                        IObj?.Paragraphs.Add("Calculated Kv[Cv]/Kv[Cv]max (%): " & Kvc / FC * 100)
                        IObj?.Paragraphs.Add("Calculated Kv: " & Kvc)
                    Case OpeningKvRelationshipType.DataTable
                        IObj?.Paragraphs.Add("Current Opening (%): " & OpeningPct)
                        Try
                            Dim factor = MathNet.Numerics.Interpolate.RationalWithoutPoles(OpeningKvRelDataTableX, OpeningKvRelDataTableY).Interpolate(OpeningPct) / 100.0
                            Kvc = factor * FC
                            IObj?.Paragraphs.Add("Calculated Kv[Cv]/Kv[Cv]max (%): " & Kvc / FC * 100)
                            IObj?.Paragraphs.Add("Calculated Kv: " & Kvc)
                        Catch ex As Exception
                            Throw New Exception("Error calculating Kv from tabulated data: " + ex.Message)
                        End Try
                End Select
            Else
                Kvc = FC
            End If

            'reference: https://www.samson.de/document/t00050en.pdf

            If CalcMode = CalculationMode.Kv_General Or CalcMode = CalculationMode.Kv_Steam Then
                IObj?.Paragraphs.Add("<h2>Kv Calculation Mode</h2>")
                IObj?.Paragraphs.Add("Kv flow equations in DWSIM are implemented as per ANSI/ISA-75.01.01 and IEC 60534-2-1 for turbulent flow.")
                IObj?.Paragraphs.Add("Kv for two-phase service is adapted from Masoneilan and is eqivalent to other vendor equations e.g. Valtek, Parcol and Warren Controls.")
                IObj?.Paragraphs.Add("See <a href='https://dam.bakerhughes.com/m/47616eb160214a1d/original/MN-Valve-Sizing-Handbook-GEA19540A-English-pdf.pdf' > Masoneilan Control Valve Sizing Handbook</a> for more information on general valve sizing.")
                IObj?.Paragraphs.Add("For more information on simplified steam service sizing, see <a href='https://www.samson.de/document/t00050en.pdf'>this document</a>.")
                IObj?.Paragraphs.Add("The folowwing default valve style modifiers are applied:")
                IObj?.Paragraphs.Add("<mi>x_T= 0.75</mi>")
                IObj?.Paragraphs.Add("<mi>F_L = 0.9</mi>")
                IObj?.Paragraphs.Add("<mi>F_P = 1.0</mi>")
                IObj?.Paragraphs.Add("<mi>F_s = 1.0</mi>")
                IObj?.Paragraphs.Add("<mi>F_i = 0.9</mi>")

                IObj?.Paragraphs.Add(String.Format("Kv = {0}", Kvc))
            End If

            If CalcMode = CalculationMode.Kv_Gas Then
                ims.PropertyPackage.CurrentMaterialStream = ims
                rhog = ims.PropertyPackage.AUX_VAPDENS(Ti, Pi)
                Cp_ig = ims.PropertyPackage.AUX_CPm(PropertyPackages.Phase.Vapor, Ti) * ims.Phases(2).Properties.molecularWeight.GetValueOrDefault()
                k = Cp_ig / (Cp_ig - 8.314)
                P2 = P2_Gas(Wi * 3600, Kvc, Pi / 100000.0, k, rhog) * 100000.0
                IObj?.Paragraphs.Add(String.Format("Calculated Outlet Pressure P2 = {0} Pa", P2))
            ElseIf CalcMode = CalculationMode.Kv_Liquid Then
                Pv = ims.PropertyPackage.AUX_PVAPM(Ti)
                Pc = ims.PropertyPackage.AUX_PCM(PropertyPackages.Phase.Liquid)
                rhol = ims.Phases(1).Properties.density.GetValueOrDefault
                P2 = 100000.0 * P2Liquid(Wi * 3600, Kvc, Pi / 100000.0, rhol, Pv / 100000.0, Pc / 100000.0)
                IObj?.Paragraphs.Add(String.Format("Calculated Outlet Pressure P2 = {0} Pa", P2))
            ElseIf CalcMode = CalculationMode.Kv_General Then
                ims.PropertyPackage.CurrentMaterialStream = ims
                rhog = ims.Phases(2).Properties.density.GetValueOrDefault
                Cp_ig = ims.PropertyPackage.AUX_CPm(PropertyPackages.Phase.Vapor, Ti) * ims.Phases(2).Properties.molecularWeight.GetValueOrDefault()
                k = Cp_ig / (Cp_ig - 8.314)
                rhol = ims.Phases(1).Properties.density.GetValueOrDefault
                Pc = ims.PropertyPackage.AUX_PCM(PropertyPackages.Phase.Liquid)
                Pv = Pi 'ims.PropertyPackage.AUX_PVAPM(PropertyPackages.Phase.Liquid, Ti)
                massfrac_gas = ims.Phases(2).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                massfrac_liq = ims.Phases(1).Properties.massflow.GetValueOrDefault / ims.Phases(0).Properties.massflow.GetValueOrDefault
                If massfrac_gas > 0.01 And massfrac_liq > 0.01 Then
                    P2 = 100000.0 * P2TwoPhase(Wi * 3600, Kvc, Pi / 100000.0, rhog, rhol, k, Pv / 100000.0, Pc / 100000.0, massfrac_gas, massfrac_liq)
                ElseIf massfrac_liq <= 0.01 Then
                    ims.PropertyPackage.CurrentMaterialStream = ims
                    rhog = ims.PropertyPackage.AUX_VAPDENS(Ti, Pi)
                    Cp_ig = ims.PropertyPackage.AUX_CPm(PropertyPackages.Phase.Vapor, Ti) * ims.Phases(2).Properties.molecularWeight.GetValueOrDefault()
                    k = Cp_ig / (Cp_ig - 8.314)
                    P2 = P2_Gas(Wi * 3600, Kvc, Pi / 100000.0, k, rhog) * 100000.0
                ElseIf massfrac_gas <= 0.01 Then
                    Pv = ims.PropertyPackage.AUX_PVAPM(Ti)
                    Pc = ims.PropertyPackage.AUX_PCM(PropertyPackages.Phase.Liquid)
                    rhol = ims.Phases(1).Properties.density.GetValueOrDefault
                    P2 = 100000.0 * P2Liquid(Wi * 3600, Kvc, Pi / 100000.0, rhol, Pv / 100000.0, Pc / 100000.0)
                End If
                IObj?.Paragraphs.Add(String.Format("Calculated Outlet Pressure P2 = {0} Pa", P2))
            ElseIf CalcMode = CalculationMode.Kv_Steam Then
                P2 = Pi * 0.7 / 100000.0
                icount = 0
                Do
                    v2 = 1 / ims.PropertyPackage.AUX_VAPDENS(Ti, P2)
                    P2ant = P2
                    P2 = Pi / 100000.0 - v2 * (31.62 * Kvc / (Wi * 3600)) ^ -2
                    icount += 1
                    If icount > 10000 Then Throw New Exception("P2 did not converge in 10000 iterations.")
                Loop Until Math.Abs(P2 - P2ant) < 0.0001
                P2 = P2 * 100000.0
                IObj?.Paragraphs.Add(String.Format("Calculated Outlet Pressure P2 = {0} Pa", P2))
            End If

            If Me.CalcMode = CalculationMode.DeltaP Then
                P2 = Pi - Me.DeltaP.GetValueOrDefault
                OutletPressure = P2
            ElseIf CalcMode = CalculationMode.OutletPressure Then
                P2 = Me.OutletPressure.GetValueOrDefault
                Me.DeltaP = Pi - P2
            Else
                DeltaP = Pi - P2
                OutletPressure = P2
            End If

            ActualKv = Kvc

            CheckSpec(P2, True, "outlet pressure")

            If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", P2, H2))

            IObj?.Paragraphs.Add(String.Format("Doing a PH flash to calculate outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", P2, H2))

            IObj?.Paragraphs.Add(String.Format("Inlet Stream Enthalpy = {0} kJ/kg", Hi))

            If Not ims.IsSingleCompound() Then

                'build quadratic curve

                Dim msc As MaterialStream = ims.Clone()
                msc.SetPressure(P2)
                msc.SetFlashSpec("PT")

                Dim Tvec = New Double() {Ti, Ti * 0.98, Ti * 0.97, Ti * 0.96, Ti * 0.95}
                Dim Hvec = New Double() {0.0, 0.0, 0.0, 0.0, 0.0}

                For i = 0 To Tvec.Count - 1
                    msc.PropertyPackage = PropertyPackage
                    PropertyPackage.CurrentMaterialStream = msc
                    msc.SetTemperature(Tvec(i))
                    msc.Calculate()
                    Hvec(i) = msc.GetMassEnthalpy()
                Next

                ims.PropertyPackage = PropertyPackage
                PropertyPackage.CurrentMaterialStream = ims

                T2est = MathNet.Numerics.Interpolation.CubicSpline.InterpolateAkima(Hvec, Tvec).Interpolate(H2)

                IObj?.SetCurrent()
                Dim tmp = Me.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, P2, H2, T2est)
                T2 = tmp.CalculatedTemperature
                CheckSpec(T2, True, "outlet temperature")
                H2c = tmp.CalculatedEnthalpy
                CheckSpec(H2c, False, "outlet enthalpy")

            Else

                IObj?.SetCurrent()
                Dim tmp = Me.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, P2, H2, T2est)
                T2 = tmp.CalculatedTemperature
                CheckSpec(T2, True, "outlet temperature")
                H2c = tmp.CalculatedEnthalpy
                CheckSpec(H2c, False, "outlet enthalpy")

            End If

            If DebugMode Then AppendDebugLine(String.Format("Calculated outlet temperature T2 = {0} K", T2))

            IObj?.Paragraphs.Add(String.Format("Outlet Stream Enthalpy = {0} kJ/kg", H2c))
            IObj?.Paragraphs.Add(String.Format("Calculated Outlet Temperature T2 = {0} K", T2))

            Houtlet = H2c
            Hinlet = Hi

            'Dim htol As Double = Me.PropertyPackage.Parameters("PP_PHFELT")
            'Dim herr As Double = Math.Abs((H2c - H2) / H2)

            'If herr > 0.01 Then Throw New Exception("The enthalpy of inlet and outlet streams doesn't match. Result is invalid.")

            Me.DeltaT = T2 - Ti
            Me.DeltaQ = 0

            OutletTemperature = T2

            If Not DebugMode Then

                With oms
                    .AtEquilibrium = False
                    .Phases(0).Properties.temperature = T2
                    .Phases(0).Properties.pressure = P2
                    .Phases(0).Properties.enthalpy = H2
                    .Phases(0).Properties.massflow = ims.Phases(0).Properties.massflow.GetValueOrDefault
                    .DefinedFlow = FlowSpec.Mass
                    Dim comp As BaseClasses.Compound
                    Dim i As Integer = 0
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = ims.Phases(0).Compounds(comp.Name).MoleFraction
                        comp.MassFraction = ims.Phases(0).Compounds(comp.Name).MassFraction
                        i += 1
                    Next
                    .SpecType = StreamSpec.Pressure_and_Enthalpy
                End With

            Else

                AppendDebugLine("Calculation finished successfully.")

            End If

            IObj?.Close()

        End Sub

        ''' <summary>
        ''' Clears the results on the outlet stream, resetting temperatures, pressures, enthalpies, and
        ''' compositions to indeterminate values. Called when the calculation is invalidated.
        ''' </summary>
        Public Overrides Sub DeCalculate()

            If Me.GraphicObject.OutputConnectors(0).IsAttached Then

                'Zerar valores da corrente de materia conectada a jusante
                With Me.GetOutletMaterialStream(0)
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.molarfraction = 1
                    .Phases(0).Properties.massfraction = 1
                    .Phases(0).Properties.enthalpy = Nothing
                    Dim comp As BaseClasses.Compound
                    Dim i As Integer = 0
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                        i += 1
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .Phases(0).Properties.molarflow = Nothing
                    .GraphicObject.Calculated = False
                End With

            End If


        End Sub

        ''' <summary>
        ''' Returns the value of the specified property, converted to the requested unit system.
        ''' </summary>
        ''' <param name="prop">Property identifier string (e.g., "PROP_VA_0").</param>
        ''' <param name="su">Unit system to convert the value to; defaults to SI when Nothing.</param>
        ''' <returns>The property value as an Object, or Nothing if the property is not found.</returns>
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
                            'PROP_VA_0	Calculation Mode
                            value = Me.CalcMode
                        Case 1
                            'PROP_VA_1	Pressure Drop
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.DeltaP.GetValueOrDefault)
                        Case 2
                            'PROP_VA_2	Outlet Pressure
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.OutletPressure.GetValueOrDefault)
                        Case 3
                            'PROP_VA_3	Temperature Drop
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.DeltaT.GetValueOrDefault)
                        Case 4
                            value = Kv
                        Case 5
                            value = OpeningPct
                        Case 6
                            value = CharacteristicParameter
                    End Select

                    Return value

                Else

                    If prop.Equals("Actual Flow Coefficient") Then
                        Return ActualKv
                    End If

                End If

            End If

        End Function

        ''' <summary>
        ''' Returns an array of property identifier strings for the valve, filtered by the requested property access type.
        ''' </summary>
        ''' <param name="proptype">Specifies whether to return read-only, read-write, write-only, or all properties.</param>
        ''' <returns>An array of property name strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RO
                    For i = 3 To 3
                        proplist.Add("PROP_VA_" + CStr(i))
                    Next
                    proplist.Add("Actual Flow Coefficient")
                Case PropertyType.RW
                    For i = 0 To 6
                        proplist.Add("PROP_VA_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 6
                        proplist.Add("PROP_VA_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To 6
                        proplist.Add("PROP_VA_" + CStr(i))
                    Next
                    proplist.Add("Actual Flow Coefficient")
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>
        ''' Sets the value of the specified property, converting from the supplied unit system to SI.
        ''' </summary>
        ''' <param name="prop">Property identifier string (e.g., "PROP_VA_1").</param>
        ''' <param name="propval">The new value to assign.</param>
        ''' <param name="su">Unit system of the supplied value; defaults to SI when Nothing.</param>
        ''' <returns>True if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter

            If prop.Contains("_") Then

                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Select Case propidx
                    Case 0
                        'PROP_VA_0	Calculation Mode
                        Me.CalcMode = propval
                    Case 1
                        'PROP_VA_1	Pressure Drop
                        Me.DeltaP = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)
                    Case 2
                        'PROP_VA_2	Outlet Pressure
                        Me.OutletPressure = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
                    Case 4
                        Me.Kv = propval
                    Case 5
                        If propval >= 0 And propval <= 100 Then
                            Me.OpeningPct = propval
                        ElseIf propval < 0 Then
                            OpeningPct = 0
                        Else
                            OpeningPct = 100
                        End If
                    Case 6
                        CharacteristicParameter = propval
                End Select

            End If

            Return 1

        End Function

        ''' <summary>
        ''' Returns the display unit string for the specified property in the given unit system.
        ''' </summary>
        ''' <param name="prop">Property identifier string.</param>
        ''' <param name="su">Unit system to use for the unit label; defaults to SI when Nothing.</param>
        ''' <returns>A string representing the unit (e.g., "bar", "K"), or an empty string for dimensionless properties.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String

            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 = "NF" Then

                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim value As String = ""

                If prop.Contains("_") Then

                    Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                    Select Case propidx

                        Case 0, 4, 5
                            'PROP_VA_0	Calculation Mode
                            value = ""
                        Case 1
                            'PROP_VA_1	Pressure Drop
                            value = su.deltaP
                        Case 2
                            'PROP_VA_2	Outlet Pressure
                            value = su.pressure
                        Case 3
                            'PROP_VA_3	Temperature Drop
                            value = su.deltaT

                    End Select

                    Return value

                Else

                    Return ""

                End If


            Else

                Return u0

            End If

        End Function

        ''' <summary>
        ''' Returns the raw bytes of the valve icon PNG resource, used for cross-platform icon loading.
        ''' </summary>
        ''' <returns>A Byte array containing the PNG image data.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.valve.png")

        End Function

        ''' <summary>
        ''' Returns the localised description string for this valve, shown in the object palette and tooltips.
        ''' </summary>
        ''' <returns>A localised description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("VALVE_Desc")
        End Function

        ''' <summary>
        ''' Returns the localised display name for this valve, shown in the flowsheet and property grid.
        ''' </summary>
        ''' <returns>A localised name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("VALVE_Name")
        End Function

        ''' <summary>
        ''' Gets a value indicating that this valve is compatible with the DWSIM mobile platform.
        ''' </summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Generates a plain-text results report for this valve, listing inlet conditions, calculation parameters, and results.
        ''' </summary>
        ''' <param name="su">Unit system used to convert values for display.</param>
        ''' <param name="ci">Culture info controlling number formatting.</param>
        ''' <param name="numberformat">Format string applied to numeric values (e.g., "G6").</param>
        ''' <returns>A multi-line string containing the formatted report.</returns>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder

            Dim istr, ostr As MaterialStream
            istr = Me.GetInletMaterialStream(0)
            ostr = Me.GetOutletMaterialStream(0)

            istr.PropertyPackage.CurrentMaterialStream = istr

            str.AppendLine("Adiabatic Valve: " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Inlet conditions")
            str.AppendLine()
            str.AppendLine("    Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, istr.Phases(0).Properties.temperature.GetValueOrDefault).ToString(numberformat, ci) & " " & su.temperature)
            str.AppendLine("    Pressure: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, istr.Phases(0).Properties.pressure.GetValueOrDefault).ToString(numberformat, ci) & " " & su.pressure)
            str.AppendLine("    Mass flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.massflow, istr.Phases(0).Properties.massflow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.massflow)
            str.AppendLine("    Mole flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, istr.Phases(0).Properties.molarflow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.molarflow)
            str.AppendLine("    Volumetric flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.volumetricFlow, istr.Phases(0).Properties.volumetric_flow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.volumetricFlow)
            str.AppendLine("    Vapor fraction: " & istr.Phases(2).Properties.molarfraction.GetValueOrDefault.ToString(numberformat, ci))
            str.AppendLine("    Compounds: " & istr.PropertyPackage.RET_VNAMES.ToArrayString)
            str.AppendLine("    Molar composition: " & istr.PropertyPackage.RET_VMOL(PropertyPackages.Phase.Mixture).ToArrayString(ci))
            str.AppendLine()
            str.AppendLine("Calculation parameters")
            str.AppendLine()
            str.AppendLine("    Calculation mode: " & CalcMode.ToString)
            Select Case Me.CalcMode
                Case CalculationMode.DeltaP
                    str.AppendLine("    Pressure decrease: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.DeltaP).ToString(numberformat, ci) & " " & su.deltaP)
                Case CalculationMode.OutletPressure
                    str.AppendLine("    Outlet pressure: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.OutletPressure).ToString(numberformat, ci) & " " & su.pressure)
                Case Else
                    str.AppendLine("    Kv(max): " & Kv)
            End Select
            str.AppendLine()
            str.AppendLine("Results")
            str.AppendLine()
            Select Case Me.CalcMode
                Case CalculationMode.DeltaP
                    str.AppendLine("    Outlet pressure: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.OutletPressure).ToString(numberformat, ci) & " " & su.pressure)
                Case CalculationMode.OutletPressure
                    str.AppendLine("    Pressure decrease: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.DeltaP).ToString(numberformat, ci) & " " & su.deltaP)
                Case Else
                    str.AppendLine("    Outlet pressure: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.OutletPressure).ToString(numberformat, ci) & " " & su.pressure)
                    str.AppendLine("    Pressure decrease: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.DeltaP).ToString(numberformat, ci) & " " & su.deltaP)
            End Select
            str.AppendLine("    Inlet enthalpy: " & SystemsOfUnits.Converter.ConvertFromSI(su.enthalpy, Me.Hinlet).ToString(numberformat, ci) & " " & su.enthalpy)
            str.AppendLine("    Outlet enthalpy: " & SystemsOfUnits.Converter.ConvertFromSI(su.enthalpy, Me.Houtlet).ToString(numberformat, ci) & " " & su.enthalpy)
            str.AppendLine("    Temperature decrease: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.DeltaT).ToString(numberformat, ci) & " " & su.deltaT)

            Return str.ToString

        End Function

        ''' <summary>
        ''' Generates a structured results report as a list of typed tuples, used by the UI report viewer.
        ''' Each tuple contains a <see cref="ReportItemType"/> tag and an array of display strings.
        ''' </summary>
        ''' <returns>A list of report-item tuples covering calculation parameters and results.</returns>
        Public Overrides Function GetStructuredReport() As List(Of Tuple(Of ReportItemType, String()))

            Dim su As IUnitsOfMeasure = GetFlowsheet().FlowsheetOptions.SelectedUnitSystem
            Dim nf = GetFlowsheet().FlowsheetOptions.NumberFormat

            Dim list As New List(Of Tuple(Of ReportItemType, String()))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Results Report for Adiabatic Valve '" & Me.GraphicObject?.Tag + "'"}))
            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.SingleColumn, New String() {"Calculated successfully on " & LastUpdated.ToString}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Calculation Parameters"}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.DoubleColumn,
                    New String() {"Calculation Mode",
                    CalcMode.ToString}))

            Select Case CalcMode
                Case CalculationMode.DeltaP
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Pressure Drop",
                            Me.DeltaP.GetValueOrDefault.ConvertFromSI(su.deltaP).ToString(nf),
                            su.deltaP}))
                Case CalculationMode.OutletPressure
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Outlet Pressure",
                            Me.OutletPressure.GetValueOrDefault.ConvertFromSI(su.pressure).ToString(nf),
                            su.pressure}))
                Case Else
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Kv (max)",
                            Me.Kv.ToString(nf),
                            ""}))
            End Select

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Results"}))

            Select Case CalcMode
                Case CalculationMode.DeltaP
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Outlet Pressure",
                            Me.OutletPressure.GetValueOrDefault.ConvertFromSI(su.pressure).ToString(nf),
                            su.pressure}))
                Case Else
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Pressure Drop",
                            Me.DeltaP.GetValueOrDefault.ConvertFromSI(su.deltaP).ToString(nf),
                            su.deltaP}))
            End Select

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Temperature Change",
                            Me.DeltaT.GetValueOrDefault.ConvertFromSI(su.deltaT).ToString(nf),
                            su.deltaT}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Inlet Enthalpy",
                            Me.Hinlet.ConvertFromSI(su.enthalpy).ToString(nf),
                            su.enthalpy}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Outlet Enthalpy",
                            Me.Houtlet.ConvertFromSI(su.enthalpy).ToString(nf),
                            su.enthalpy}))

            Return list

        End Function

        ''' <summary>
        ''' Returns a user-facing description for the specified property name, displayed in the property grid tooltip.
        ''' </summary>
        ''' <param name="p">The property display name (e.g., "Pressure Drop").</param>
        ''' <returns>A descriptive string explaining the property's purpose and usage.</returns>
        Public Overrides Function GetPropertyDescription(p As String) As String
            If p.Equals("Calculation Mode") Then
                Return "Select the calculation mode of this valve."
            ElseIf p.Equals("Pressure Drop") Then
                Return "If you chose 'Pressure Drop' as the calculation mode, enter the desired value. If you chose a different calculation mode, this parameter will be calculated."
            ElseIf p.Equals("Outlet Pressure") Then
                Return "If you chose 'Outlet Pressure' as the calculation mode, enter the desired value. If you chose a different calculation mode, this parameter will be calculated."
            Else
                Return p
            End If
        End Function

    End Class

End Namespace

