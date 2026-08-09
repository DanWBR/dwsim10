'    Heat Exchanger Calculation Routines 
'    Copyright 2008-2024 Daniel Wagner O. de Medeiros
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
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports System.Globalization
Imports System.Reflection
Imports System.Threading.Tasks
Imports System.Math
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.HeatExchanger

Namespace UnitOperations

    ''' <summary>Defines the available calculation modes for the heat exchanger.</summary>
    Public Enum HeatExchangerCalcMode
        ''' <summary>Calculate the hot-side outlet temperature given other specifications.</summary>
        CalcTempHotOut = 0
        ''' <summary>Calculate the cold-side outlet temperature given other specifications.</summary>
        CalcTempColdOut = 1
        ''' <summary>Calculate both outlet temperatures from a specified heat duty.</summary>
        CalcBothTemp = 2
        ''' <summary>Calculate both outlet temperatures from a specified overall heat transfer coefficient and area (UA).</summary>
        CalcBothTemp_UA = 3
        ''' <summary>Calculate the required heat transfer area from a specified overall coefficient and outlet temperatures.</summary>
        CalcArea = 4
        ''' <summary>Perform a detailed shell-and-tube rating calculation using geometry data.</summary>
        ShellandTube_Rating = 5
        ''' <summary>Back-calculate the fouling factor from measured shell-and-tube performance data.</summary>
        ShellandTube_CalcFoulingFactor = 6
        ''' <summary>Calculate outlet temperatures using a minimum internal temperature approach (pinch point) specification.</summary>
        PinchPoint = 7
        ''' <summary>Calculate outlet temperatures using a specified thermal efficiency.</summary>
        ThermalEfficiency = 8
        ''' <summary>Calculate so the hot-side outlet reaches a specified vapour fraction.</summary>
        OutletVaporFraction1 = 9
        ''' <summary>Calculate so the cold-side outlet reaches a specified vapour fraction.</summary>
        OutletVaporFraction2 = 10
    End Enum

    ''' <summary>Indicates which fluid side has its outlet temperature specified.</summary>
    Public Enum SpecifiedTemperature
        ''' <summary>The hot-fluid outlet temperature is specified.</summary>
        Hot_Fluid
        ''' <summary>The cold-fluid outlet temperature is specified.</summary>
        Cold_Fluid
    End Enum

    ''' <summary>Defines the relative flow direction of the two fluid streams.</summary>
    Public Enum FlowDirection
        ''' <summary>Fluids flow in opposite directions (counter-current).</summary>
        CounterCurrent
        ''' <summary>Fluids flow in the same direction (co-current / parallel flow).</summary>
        CoCurrent
    End Enum

    ''' <summary>Defines the heat exchanger geometry type, used when shell-and-tube rating is selected.</summary>
    Public Enum HeatExchangerType
        ''' <summary>Double-pipe (hairpin) exchanger.</summary>
        DoublePipe
        ''' <summary>TEMA E-type shell (single-pass shell).</summary>
        ShellTubes_E
        ''' <summary>TEMA F-type shell (two-pass shell with longitudinal baffle).</summary>
        ShellTubes_F
        ''' <summary>TEMA G-type shell (split-flow shell).</summary>
        ShellTubes_G
        ''' <summary>TEMA H-type shell (double split-flow shell).</summary>
        ShellTubes_H
        ''' <summary>TEMA J-type shell (divided-flow shell).</summary>
        ShellTubes_J
        ''' <summary>TEMA K-type shell (kettle-type reboiler).</summary>
        ShellTubes_K
        ''' <summary>TEMA X-type shell (cross-flow shell).</summary>
        ShellTubes_X
    End Enum

    Public Enum TEMAFrontHeadType
        A = 0
        B = 1
        C = 2
        N = 3
        D = 4
    End Enum

    Public Enum TEMARearHeadType
        L = 0
        M = 1
        N = 2
        P = 3
        S = 4
        T = 5
        U = 6
        W = 7
    End Enum

    Public Enum TubeToTubesheetJointType
        Expanded = 0
        Welded = 1
        ExpandedAndWelded = 2
    End Enum

    ''' <summary>
    ''' Represents a heat exchanger unit operation that transfers thermal energy between
    ''' a hot-side and a cold-side material stream. Supports counter-current and co-current
    ''' flow, multiple calculation modes (outlet temperature, UA, area, rating, pinch-point,
    ''' thermal efficiency), optional shell-and-tube geometry, and dynamic simulation.
    ''' </summary>
    <System.Serializable()> Public Partial Class HeatExchanger

        Inherits UnitOperations.UnitOpBaseClass

        Implements IHeatExchanger

        ''' <summary>Gets or sets the simulation object class category (Exchangers).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Exchangers

        ''' <summary>Gets a value indicating whether this unit operation supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this unit operation exposes dedicated properties for dynamic mode.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = True

        ''' <summary>Gets the list of equipment sub-types available for this heat exchanger.</summary>
        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Shell and Tube", "Plate and Frame", "Double Pipe"}
            End Get
        End Property

        ''' <summary>Creates the dimensions list (Area) for this heat exchanger.</summary>
        Public Overrides Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Area, .IsUserDefined = False})

        End Sub

        ''' <summary>Updates the Area dimension value from the current calculated area.</summary>
        Public Overrides Sub UpdateDimensionsList()

            Dimensions(0).Value = Area

        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Protected m_Q As Nullable(Of Double) = 0
        Protected m_dp As Nullable(Of Double) = 0
        Protected m_OverallCoefficient As Nullable(Of Double) = 1000
        Protected m_Area As Nullable(Of Double) = 1.0#
        Protected TempHotOut As Nullable(Of Double) = 298.15#
        Protected TempColdOut As Nullable(Of Double) = 298.15#
        Protected m_tempdiff As Double = 0
        Protected FoulingFactor As Nullable(Of Double) = 0
        Public Property HXType As Integer
        Protected CalcMode As HeatExchangerCalcMode = HeatExchangerCalcMode.CalcBothTemp_UA
        Protected m_HotSidePressureDrop As Double = 0
        Protected m_ColdSidePressureDrop As Double = 0
        Protected m_specifiedtemperature As SpecifiedTemperature = SpecifiedTemperature.Cold_Fluid
        Protected m_flowdirection As FlowDirection = FlowDirection.CounterCurrent
        Protected m_stprops As New STHXProperties
        Protected m_f As Double = 1.0#

        ''' <summary>Gets or sets the heat duty profile along the exchanger length (kW at each discretisation point).</summary>
        Public Property HeatProfile As Double() = {}
        ''' <summary>Gets or sets the cold-side temperature profile along the exchanger length (K).</summary>
        Public Property TemperatureProfileCold As Double() = {}
        ''' <summary>Gets or sets the hot-side temperature profile along the exchanger length (K).</summary>
        Public Property TemperatureProfileHot As Double() = {}
        ''' <summary>Gets or sets whether negative LMTD (temperature cross) errors are suppressed instead of throwing.</summary>
        Public Property IgnoreLMTDError As Boolean = True
        ''' <summary>Gets or sets the LMTD correction factor applied to the log-mean temperature difference.</summary>
        Public Property CorrectionFactorLMTD As Double = 1.0
        ''' <summary>Gets or sets the fractional heat loss to the environment (kW).</summary>
        Public Property HeatLoss As Double = 0.0
        ''' <summary>Gets or sets the target vapour fraction for the hot-side outlet when using <see cref="HeatExchangerCalcMode.OutletVaporFraction1"/>.</summary>
        Public Property OutletVaporFraction1 As Double = 0.0
        ''' <summary>Gets or sets the target vapour fraction for the cold-side outlet when using <see cref="HeatExchangerCalcMode.OutletVaporFraction2"/>.</summary>
        Public Property OutletVaporFraction2 As Double = 0.0

        ''' <summary>Gets or sets whether the pinch-point specification applies at the exchanger outlets rather than internally.</summary>
        Public Property PinchPointAtOutlets As Boolean = False

        ''' <summary>Gets or sets whether shell-and-tube geometry information is used for heat-transfer coefficient estimation.</summary>
        Public Property UseShellAndTubeGeometryInformation As Boolean = False

        ''' <summary>Gets or sets whether a detailed heat-exchange profile should be calculated along the exchanger length.</summary>
        Public Property CalculateHeatExchangeProfile As Boolean = False

        ''' <summary>Gets or sets the shell-and-tube heat exchanger geometry and physical properties used when <see cref="HeatExchangerCalcMode.ShellandTube_Rating"/> is selected.</summary>
        Public Property STProperties() As STHXProperties
            Get
                If m_stprops Is Nothing Then m_stprops = New STHXProperties
                Return m_stprops
            End Get
            Set(value As STHXProperties)
                m_stprops = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated LMTD correction factor (F) for multi-pass exchangers.</summary>
        Public Property LMTD_F() As Double
            Get
                Return m_f
            End Get
            Set(ByVal value As Double)
                m_f = value
            End Set
        End Property

        ''' <summary>Gets or sets the relative flow direction of the two fluid streams (counter-current or co-current).</summary>
        Public Property FlowDir() As FlowDirection
            Get
                Return m_flowdirection
            End Get
            Set(ByVal value As FlowDirection)
                m_flowdirection = value
            End Set
        End Property

        ''' <summary>Gets or sets which fluid side has its outlet temperature explicitly specified by the user.</summary>
        Public Property DefinedTemperature() As SpecifiedTemperature
            Get
                Return m_specifiedtemperature
            End Get
            Set(ByVal value As SpecifiedTemperature)
                m_specifiedtemperature = value
            End Set
        End Property

        ''' <summary>Gets or sets the thermal efficiency (0ï¿½1) when using <see cref="HeatExchangerCalcMode.ThermalEfficiency"/> mode.</summary>
        Public Property ThermalEfficiency As Double

        ''' <summary>Gets or sets the maximum thermodynamically possible heat exchange (kW) between the two streams.</summary>
        Public Property MaxHeatExchange As Double

        ''' <summary>Gets or sets the specified minimum internal temperature approach (MITA / pinch) in K.</summary>
        Public Property MITA As Double

        ''' <summary>Gets or sets the calculated minimum internal temperature approach (K) from the last simulation run.</summary>
        Public Property CalculatedMITA As Double

        'proxy properties

        ''' <summary>Returns an array of strings describing all available calculation modes for this heat exchanger.</summary>
        ''' <returns>An array of mode descriptor strings.</returns>
        Public Overrides Function GetCalculationModes() As String()

            Dim modes As New List(Of String)

            For Each tstEnum As HeatExchangerCalcMode In System.Enum.GetValues(GetType(HeatExchangerCalcMode))
                modes.Add(String.Format("Name: {0}  ID: {1}", tstEnum.ToString, CInt(tstEnum).ToString()))
            Next

            Return modes.ToArray()

        End Function

        ''' <summary>Sets the active calculation mode for the heat exchanger using a numeric identifier.</summary>
        ''' <param name="modeID">The integer ID corresponding to a <see cref="HeatExchangerCalcMode"/> value.</param>
        ''' <returns>The string name of the newly set calculation mode.</returns>
        Public Overrides Function SetCalculationMode(modeID As Integer) As Object

            Me.CalcMode = modeID

            Return CalcMode.ToString()

        End Function

        ''' <summary>Gets or sets the thermal efficiency (interface proxy for <see cref="ThermalEfficiency"/>).</summary>
        Public Property Efficiency As Double Implements IHeatExchanger.Efficiency
            Get
                Return ThermalEfficiency
            End Get
            Set(value As Double)
                ThermalEfficiency = value
            End Set
        End Property


        ''' <summary>Gets or sets the heat duty (kW) exchanged between the two streams. Proxy for the internal Q property.</summary>
        Public Property HeatDuty As Double
            Get
                Return Q.GetValueOrDefault()
            End Get
            Set(value As Double)
                Q = value
            End Set
        End Property

        ''' <summary>Gets or sets the temperature change on the hot side (K).</summary>
        Public Property HotSideTemperatureChange As Double

        ''' <summary>Gets or sets the temperature change on the cold side (K).</summary>
        Public Property ColdSideTemperatureChange As Double

        ''' <summary>
        ''' Restores the heat exchanger state from a list of XML elements, handling backward-compatible
        ''' renames and deserialising dynamic accumulation streams.
        ''' </summary>
        ''' <param name="data">The list of XML elements containing the serialized data.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As List(Of XElement)) As Boolean
            'workaround for renaming CalcBothTemp_KA calculation type to CalcBothTemp_UA
            For Each xel In data
                If xel.Name = "CalculationMode" Then
                    xel.Value = xel.Value.Replace("CalcBothTemp_KA", "CalcBothTemp_UA")
                End If
            Next
            AccumulationStreamsHot = New List(Of Thermodynamics.Streams.MaterialStream)
            AccumulationStreamsCold = New List(Of Thermodynamics.Streams.MaterialStream)
            WallTemperatures = New List(Of Double)

            Dim aelh = (From xel As XElement In data Select xel Where xel.Name = "AccumulationStreamsHot").FirstOrDefault
            If aelh IsNot Nothing Then
                For Each xel In aelh.Elements
                    Dim as1 As New Thermodynamics.Streams.MaterialStream()
                    as1.LoadData(xel.Elements.ToList)
                    AccumulationStreamsHot.Add(as1)
                Next
            End If
            Dim aelc = (From xel As XElement In data Select xel Where xel.Name = "AccumulationStreamsCold").FirstOrDefault
            If aelc IsNot Nothing Then
                For Each xel In aelc.Elements
                    Dim as1 As New Thermodynamics.Streams.MaterialStream()
                    as1.LoadData(xel.Elements.ToList)
                    AccumulationStreamsCold.Add(as1)
                Next
            End If

            Dim aelw = (From xel As XElement In data Select xel Where xel.Name = "WallTemperatures").FirstOrDefault
            If aelw IsNot Nothing Then
                For Each xel In aelw.Elements
                    WallTemperatures.Add(Double.Parse(xel.Value, Globalization.CultureInfo.InvariantCulture))
                Next
            End If

            'Backward compatibility: old single-stream format. The per-cell reconciliation in
            'RunDynamicModel rebuilds the cell list when its count does not match Number of Cells,
            'so a single loaded stream is a valid seed.
            If AccumulationStreamsHot.Count = 0 Then
                Dim ael = (From xel As XElement In data Select xel Where xel.Name = "AccumulationStreamHot").FirstOrDefault
                If ael IsNot Nothing Then
                    Dim as1 As New Thermodynamics.Streams.MaterialStream()
                    as1.LoadData(ael.Elements.ToList)
                    AccumulationStreamsHot.Add(as1)
                End If
            End If
            If AccumulationStreamsCold.Count = 0 Then
                Dim ael2 = (From xel As XElement In data Select xel Where xel.Name = "AccumulationStreamCold").FirstOrDefault
                If ael2 IsNot Nothing Then
                    Dim as1 As New Thermodynamics.Streams.MaterialStream()
                    as1.LoadData(ael2.Elements.ToList)
                    AccumulationStreamsCold.Add(as1)
                End If
            End If

            Return MyBase.LoadData(data)
        End Function

        ''' <summary>
        ''' Serializes the heat exchanger state, including hot- and cold-side dynamic accumulation
        ''' streams, into a list of XML elements.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Overrides Function SaveData() As List(Of XElement)

            Dim elements As List(Of XElement) = MyBase.SaveData()

            If AccumulationStreamsHot IsNot Nothing AndAlso AccumulationStreamsHot.Count > 0 Then
                Dim astr As New XElement("AccumulationStreamsHot")
                elements.Add(astr)
                For Each mstream In AccumulationStreamsHot
                    astr.Add(New XElement("AccumulationStream", mstream.SaveData()))
                Next
            End If
            If AccumulationStreamsCold IsNot Nothing AndAlso AccumulationStreamsCold.Count > 0 Then
                Dim astr As New XElement("AccumulationStreamsCold")
                elements.Add(astr)
                For Each mstream In AccumulationStreamsCold
                    astr.Add(New XElement("AccumulationStream", mstream.SaveData()))
                Next
            End If
            If WallTemperatures IsNot Nothing AndAlso WallTemperatures.Count > 0 Then
                Dim wtr As New XElement("WallTemperatures")
                elements.Add(wtr)
                For Each t In WallTemperatures
                    wtr.Add(New XElement("T", t.ToString(Globalization.CultureInfo.InvariantCulture)))
                Next
            End If

            Return elements

        End Function

        ''' <summary>Initializes a new instance with a name and description.</summary>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            ComponentName = name
            ComponentDescription = description
            HXType = HeatExchangerType.DoublePipe

        End Sub

        ''' <summary>Creates a deep copy via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New HeatExchanger()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of HeatExchanger)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Gets or sets the heat-exchanger calculation mode (e.g. CalcBothTemp, CalcArea).</summary>
        Public Property CalculationMode() As HeatExchangerCalcMode
            Get
                Return Me.CalcMode
            End Get
            Set(ByVal value As HeatExchangerCalcMode)
                Me.CalcMode = value
            End Set
        End Property

        ''' <summary>Gets or sets the overall heat-transfer coefficient (W/(mï¿½ï¿½K)).</summary>
        Public Property OverallCoefficient() As Nullable(Of Double)
            Get
                Return m_OverallCoefficient
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_OverallCoefficient = value
            End Set
        End Property

        ''' <summary>Gets or sets the heat-transfer area (mï¿½) used by the IHeatExchanger interface.</summary>
        Public Property Area1 As Double Implements IHeatExchanger.Area
            Get
                Return Area.GetValueOrDefault()
            End Get
            Set(value As Double)
                Area = value
            End Set
        End Property

        ''' <summary>Gets or sets the heat-transfer area (mï¿½), nullable.</summary>
        Public Property Area As Nullable(Of Double)
            Get
                Return m_Area
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_Area = value
            End Set
        End Property

        ''' <summary>Gets or sets the overall pressure drop across the exchanger (Pa), nullable.</summary>
        Public Property DeltaP As Nullable(Of Double)
            Get
                Return m_dp
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_dp = value
            End Set
        End Property

        ''' <summary>Gets or sets the heat duty (W), nullable.</summary>
        Public Property Q As Nullable(Of Double)
            Get
                Return m_Q
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_Q = value
            End Set
        End Property

        ''' <summary>Gets or sets the pressure drop on the hot side (Pa).</summary>
        Public Property HotSidePressureDrop As Double
            Get
                Return m_HotSidePressureDrop
            End Get
            Set(ByVal value As Double)
                m_HotSidePressureDrop = value
            End Set
        End Property

        ''' <summary>Gets or sets the pressure drop on the cold side (Pa).</summary>
        Public Property ColdSidePressureDrop As Double
            Get
                Return m_ColdSidePressureDrop
            End Get
            Set(ByVal value As Double)
                m_ColdSidePressureDrop = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated hot-side outlet temperature (K).</summary>
        Public Property HotSideOutletTemperature As Double
            Get
                Return TempHotOut
            End Get
            Set(ByVal value As Double)
                TempHotOut = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated cold-side outlet temperature (K).</summary>
        Public Property ColdSideOutletTemperature As Double
            Get
                Return TempColdOut
            End Get
            Set(ByVal value As Double)
                TempColdOut = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated log-mean temperature difference (K).</summary>
        Public Property LMTD() As Double
            Get
                Return m_tempdiff
            End Get
            Set(ByVal value As Double)
                m_tempdiff = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>Validates the heat-exchanger configuration and stream connections.</summary>
        Public Overrides Sub Validate()

            MyBase.Validate()

        End Sub

        ''' <summary>Per-cell hot-side holdup streams (one per discretization cell) used in dynamic mode.</summary>
        Public AccumulationStreamsHot As New List(Of Thermodynamics.Streams.MaterialStream)

        ''' <summary>Per-cell cold-side holdup streams (one per discretization cell) used in dynamic mode.</summary>
        Public AccumulationStreamsCold As New List(Of Thermodynamics.Streams.MaterialStream)

        ''' <summary>Per-cell wall temperatures (K) used in dynamic mode when a wall thermal mass is set.</summary>
        Public WallTemperatures As New List(Of Double)

        'Per-cell molar-volume history (m3/mol) used for the incompressible pressure-ratio fallback.
        Private prevMHot, currentMHot, prevMCold, currentMCold As List(Of Double)

        ''' <summary>Creates additional properties for dynamic simulation mode.</summary>
        Public Overrides Sub CreateDynamicProperties()

            AddDynamicProperty("Cold Fluid Flow Conductance", "Flow Conductance (inverse of Resistance) for the Cold Fluid.", 1, UnitOfMeasure.conductance, 1.0.GetType())
            AddDynamicProperty("Hot Fluid Flow Conductance", "Flow Conductance (inverse of Resistance) for the Hot Fluid.", 1, UnitOfMeasure.conductance, 1.0.GetType())
            AddDynamicProperty("Volume for Cold Fluid", "Available Volume for Cold Fluid", 1, UnitOfMeasure.volume, 1.0.GetType())
            AddDynamicProperty("Volume for Hot Fluid", "Available Volume for Cold Fluid", 1, UnitOfMeasure.volume, 1.0.GetType())
            AddDynamicProperty("Cold Side Pressure", "Dynamic Pressure for the Cold Fluid side.", 101325, UnitOfMeasure.pressure, 1.0.GetType())
            AddDynamicProperty("Hot Side Pressure", "Dynamic Pressure for the Hot Fluid side.", 101325, UnitOfMeasure.pressure, 1.0.GetType())
            AddDynamicProperty("Minimum Pressure", "Minimum Dynamic Pressure for this Unit Operation.", 101325, UnitOfMeasure.pressure, 1.0.GetType())
            AddDynamicProperty("Initialize using Inlet Streams", "Initializes the volume contents with information from the inlet streams, if the content is null.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Reset Contents", "Empties the volume contents on the next run.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Fouling Rate", "Linear fouling growth rate in m2.K/kW per second. Set to 0 to disable.", 0.0, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Current Fouling Resistance", "Current total fouling resistance (m2.K/kW).", 0.0, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Wall Thermal Mass", "Product of wall mass and specific heat (J/K). Set to 0 for instantaneous heat transfer.", 0.0, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Wall Temperature", "Average wall temperature (K). In multi-cell mode this is the reported mean of the per-cell wall temperatures.", 298.15, UnitOfMeasure.temperature, 1.0.GetType())
            AddDynamicProperty("Number of Cells", "Number of spatial discretization cells along the exchanger length. 1 = lumped model.", 10, UnitOfMeasure.none, 1.GetType())
            AddDynamicProperty("Substeps", "Number of internal sub-steps the integration time step is divided into for stability.", 1, UnitOfMeasure.none, 1.GetType())

        End Sub

        ''' <summary>Whether the stream's property package rewrites any calculated property.</summary>
        Private Shared Function HasPropertyOverrides(str As MaterialStream) As Boolean

            Return str IsNot Nothing AndAlso str.PropertyPackage IsNot Nothing AndAlso
                   str.PropertyPackage.PropertyOverrides.Count > 0

        End Function

        ''' <summary>
        ''' Temperature of a stream's material at the given pressure and enthalpy, honouring the
        ''' property overrides carried by its property package.
        ''' </summary>
        ''' <param name="str">The stream whose material and property package are used.</param>
        ''' <param name="fallback">What the flash returned, which is the answer when there are no overrides.</param>
        ''' <remarks>
        ''' A property override is a script that rewrites a calculated phase property, and it runs in
        ''' MaterialStream.Calculate, not in the flash. The exchanger iterates on the flash alone, so
        ''' with an override in place it converged on the enthalpy the correlations give while the
        ''' outlet stream reported the overridden one, and the two sides of the energy balance
        ''' disagreed. Going through a stream costs a full property calculation, so it is done only
        ''' when the package actually carries an override.
        ''' </remarks>
        Private Shared Function OverriddenTemperature(str As MaterialStream, P As Double, H As Double, fallback As Double) As Double

            If str Is Nothing OrElse str.PropertyPackage Is Nothing Then Return fallback
            If str.PropertyPackage.PropertyOverrides.Count = 0 Then Return fallback

            ' An override rewrites the enthalpy after the flash has run, so a pressure-enthalpy
            ' flash cannot honour one: it solves the correlations and the answer is replaced
            ' afterwards. The mapping has to be inverted here, by looking for the temperature whose
            ' overridden enthalpy is the one being asked for. The flash result is the first guess.
            Dim T0 As Double = fallback
            Dim H0 As Double = OverriddenEnthalpy(str, P, T0, Double.NaN)

            If Double.IsNaN(H0) Then Return fallback
            If Math.Abs(H0 - H) < 0.000001 Then Return T0

            Dim T1 As Double = T0 + 1.0
            Dim H1 As Double = OverriddenEnthalpy(str, P, T1, Double.NaN)

            Dim it As Integer = 0

            While Not Double.IsNaN(H1) AndAlso Math.Abs(H1 - H) > 0.000001 AndAlso it < 50

                Dim slope As Double = (H1 - H0) / (T1 - T0)

                If Math.Abs(slope) < 0.000000000001 Then Exit While

                Dim T2 As Double = T1 - (H1 - H) / slope

                If Double.IsNaN(T2) OrElse T2 <= 0.0 Then Exit While

                T0 = T1 : H0 = H1
                T1 = T2 : H1 = OverriddenEnthalpy(str, P, T1, Double.NaN)

                it += 1

            End While

            If Double.IsNaN(H1) OrElse it >= 50 Then Return fallback

            Return T1

        End Function

        ''' <summary>
        ''' Enthalpy of a stream's material at the given pressure and temperature, honouring the
        ''' property overrides carried by its property package.
        ''' </summary>
        ''' <param name="str">The stream whose material and property package are used.</param>
        ''' <param name="fallback">What the flash returned, which is the answer when there are no overrides.</param>
        Private Shared Function OverriddenEnthalpy(str As MaterialStream, P As Double, T As Double, fallback As Double) As Double

            If str Is Nothing OrElse str.PropertyPackage Is Nothing Then Return fallback
            If str.PropertyPackage.PropertyOverrides.Count = 0 Then Return fallback

            Dim tmpstr As MaterialStream = str.Clone
            tmpstr.PropertyPackage = str.PropertyPackage
            tmpstr.SetFlowsheet(str.FlowSheet)
            tmpstr.Phases(0).Properties.pressure = P
            tmpstr.Phases(0).Properties.temperature = T
            tmpstr.SpecType = StreamSpec.Temperature_and_Pressure
            tmpstr.Calculate()

            Return tmpstr.Phases(0).Properties.enthalpy.GetValueOrDefault

        End Function

        ''' <summary>
        ''' Computes the shell-and-tube overall heat-transfer coefficient (U) and the tube/shell side
        ''' pressure drops using Tinker's method, given bulk fluid properties and throughput mass flows.
        ''' Shared by the steady-state and dynamic paths so the geometry/correlation math lives in one place.
        ''' </summary>
        Private Sub ComputeShellAndTubeU(rhoc As Double, Cpc As Double, kc As Double, muc As Double,
                                         rhoh As Double, Cph As Double, kh As Double, muh As Double,
                                         Wc As Double, Wh As Double,
                                         ByRef U As Double, ByRef A As Double,
                                         ByRef dps As Double, ByRef dpt As Double,
                                         ByRef Res As Double, ByRef Ret As Double,
                                         ByRef f1 As Double, ByRef f3 As Double, ByRef f5 As Double,
                                         ByRef vt As Double, ByRef Gsf As Double, ByRef Ssf As Double)

            Dim rs, rt, Nc, di, de, pitch, L, n, hi, nt, Prt As Double

            rs = Me.STProperties.Shell_Fouling
            rt = Me.STProperties.Tube_Fouling
            Nc = STProperties.Shell_NumberOfShellsInSeries
            de = STProperties.Tube_De / 1000
            di = STProperties.Tube_Di / 1000
            L = STProperties.Tube_Length
            pitch = STProperties.Tube_Pitch / 1000
            n = STProperties.Tube_NumberPerShell
            nt = n / STProperties.Tube_PassesPerShell
            ' the tube count is per shell, so the shells in series multiply the area, the same way
            ' the geometry report does it
            A = n * Nc * Math.PI * de * (L - 2 * de)

            If STProperties.Tube_Fluid = 0 Then
                'cold
                vt = Wc / (rhoc * nt * Math.PI * di ^ 2 / 4)
                Ret = rhoc * vt * di / muc
                Prt = muc * Cpc / kc * 1000
            Else
                'hot
                vt = Wh / (rhoh * nt * Math.PI * di ^ 2 / 4)
                Ret = rhoh * vt * di / muh
                Prt = muh * Cph / kh * 1000
            End If

            'calcular DeltaP

            'tube

            Dim fric As Double
            Dim epsilon As Double = STProperties.Tube_Roughness / 1000
            If Ret > 3250 Then
                Dim a1 = Math.Log(((epsilon / di) ^ 1.1096) / 2.8257 + (7.149 / Ret) ^ 0.8961) / Math.Log(10.0#)
                Dim b1 = -2 * Math.Log((epsilon / di) / 3.7065 - 5.0452 * a1 / Ret) / Math.Log(10.0#)
                fric = (1 / b1) ^ 2
            Else
                fric = 64 / Ret
            End If
            Dim fric_dp As Double = fric * STProperties.Tube_Scaling_FricCorrFactor

            If STProperties.Tube_Fluid = 0 Then
                'cold
                dpt = fric_dp * L * STProperties.Tube_PassesPerShell / di * vt ^ 2 / 2 * rhoc
            Else
                'hot
                dpt = fric_dp * L * STProperties.Tube_PassesPerShell / di * vt ^ 2 / 2 * rhoh
            End If

            'tube heat transfer coeff (uses uncorrected friction factor)

            If STProperties.Tube_Fluid = 0 Then
                'cold
                hi = kc / di * (fric / 8) * Ret * Prt / (1.07 + 12.7 * (fric / 8) ^ 0.5 * (Prt ^ (2 / 3) - 1))
            Else
                'hot
                hi = kh / di * (fric / 8) * Ret * Prt / (1.07 + 12.7 * (fric / 8) ^ 0.5 * (Prt ^ (2 / 3) - 1))
            End If

            'shell internal diameter

            Dim Dsi, Dsf, nsc, HDi, Nb As Double
            Select Case STProperties.Tube_Layout
                Case 0, 1
                    nsc = 1.1 * n ^ 0.5
                Case 2, 3
                    nsc = 1.19 * n ^ 0.5
            End Select
            Dsf = (nsc - 1) * pitch + de
            Dsi = STProperties.Shell_Di / 1000 'Dsf / 1.075

            HDi = STProperties.Shell_BaffleCut / 100
            Nb = Math.Max(Math.Floor(L / (STProperties.Shell_BaffleSpacing / 1000)) - 1, 1)

            'shell pressure drop

            Dim Np, Fp, Ss, fs, Cb, Ca, Prs, jh, aa, bb, cc, xx, yy, Nh, Y As Double
            xx = Dsi / (STProperties.Shell_BaffleSpacing / 1000)
            yy = pitch / de

            Select Case STProperties.Tube_Layout
                Case 0, 1
                    aa = 0.9078565328950694
                    bb = 0.66331106126564476
                    cc = -4.4329764639656482
                    Nh = aa * xx ^ bb * yy ^ cc
                    aa = 5.3718559074820611
                    bb = -0.33416765138071414
                    cc = 0.7267144209289168
                    Y = aa * xx ^ bb * yy ^ cc
                    aa = 0.53807650470841084
                    bb = 0.3761125784751041
                    cc = -3.8741224386187474
                    Np = aa * xx ^ bb * yy ^ cc
                Case 2
                    aa = 0.84134824361715088
                    bb = 0.61374520485097339
                    cc = -4.2696318466170409
                    Nh = aa * xx ^ bb * yy ^ cc
                    aa = 4.9901814007765743
                    bb = -0.32437442510328618
                    cc = 1.084850423269188
                    Y = aa * xx ^ bb * yy ^ cc
                    aa = 0.5502379008813062
                    bb = 0.36559560225434834
                    cc = -3.99041305625483
                    Np = aa * xx ^ bb * yy ^ cc
                Case 3
                    aa = 0.66738654406767639
                    bb = 0.680260033886211
                    cc = -4.522291113086232
                    Nh = aa * xx ^ bb * yy ^ cc
                    aa = 4.5749169651729105
                    bb = -0.32201759442337358
                    cc = 1.17295183743691
                    Y = aa * xx ^ bb * yy ^ cc
                    aa = 0.36869631130961067
                    bb = 0.38397859475813922
                    cc = -3.6273465996780421
                    Np = aa * xx ^ bb * yy ^ cc
            End Select
            Fp = 1 / (0.8 + Np * (Dsi / pitch) ^ 0.5)
            Select Case STProperties.Tube_Layout
                Case 0, 1, 2
                    Cb = 0.97
                Case 3
                    Cb = 1.37
            End Select
            Ca = Cb * (pitch - de) / pitch
            Ss = Ca * STProperties.Shell_BaffleSpacing / 1000 * Dsf
            Ssf = Ss / Fp
            Ssf = Math.PI / 4 * (Dsi ^ 2 - nt * de ^ 2)
            If STProperties.Shell_Fluid = 0 Then
                'cold
                Gsf = Wc / Ssf
                Res = Gsf * de / muc
                Prs = muc * Cpc / kc * 1000
            Else
                'hot
                Gsf = Wh / Ssf
                Res = Gsf * de / muh
                Prs = muh * Cph / kh * 1000
            End If

            Select Case STProperties.Tube_Layout
                Case 0, 1
                    If Res < 100 Then
                        jh = 0.497 * Res ^ 0.54
                    Else
                        jh = 0.378 * Res ^ 0.59
                    End If
                    If pitch / de <= 1.2 Then
                        If Res < 100 Then
                            fs = 276.46 * Res ^ -0.979
                        ElseIf Res < 1000 Then
                            fs = 30.26 * Res ^ -0.523
                        Else
                            fs = 2.93 * Res ^ -0.186
                        End If
                    ElseIf pitch / de <= 1.3 Then
                        If Res < 100 Then
                            fs = 208.14 * Res ^ -0.945
                        ElseIf Res < 1000 Then
                            fs = 27.6 * Res ^ -0.525
                        Else
                            fs = 2.27 * Res ^ -0.163
                        End If
                    ElseIf pitch / de <= 1.4 Then
                        If Res < 100 Then
                            fs = 122.73 * Res ^ -0.865
                        ElseIf Res < 1000 Then
                            fs = 17.82 * Res ^ -0.474
                        Else
                            fs = 1.86 * Res ^ -0.146
                        End If
                    ElseIf pitch / de <= 1.5 Then
                        If Res < 100 Then
                            fs = 104.33 * Res ^ -0.869
                        ElseIf Res < 1000 Then
                            fs = 12.69 * Res ^ -0.434
                        Else
                            fs = 1.526 * Res ^ -0.129
                        End If
                    Else
                        Throw New Exception(String.Format("The ratio between tube spacing and tube external diameter needs to be less than or equal to 1.5 (current value: {0})", pitch / de))
                    End If
                Case 2, 3
                    If Res < 100 Then
                        If STProperties.Tube_Layout = 2 Then
                            jh = 0.385 * Res ^ 0.526
                        Else
                            jh = 0.496 * Res ^ 0.54
                        End If
                    Else
                        If STProperties.Tube_Layout = 2 Then
                            jh = 0.2487 * Res ^ 0.625
                        Else
                            jh = 0.354 * Res ^ 0.61
                        End If
                    End If
                    If pitch / de <= 1.2 Then
                        If Res < 100 Then
                            fs = 230 * Res ^ -1
                        ElseIf Res < 1000 Then
                            fs = 16.23 * Res ^ -0.43
                        Else
                            fs = 2.67 * Res ^ -0.173
                        End If
                    ElseIf pitch / de <= 1.3 Then
                        If Res < 100 Then
                            fs = 142.22 * Res ^ -0.949
                        ElseIf Res < 1000 Then
                            fs = 11.93 * Res ^ -0.43
                        Else
                            fs = 1.77 * Res ^ -0.144
                        End If
                    ElseIf pitch / de <= 1.4 Then
                        If Res < 100 Then
                            fs = 110.77 * Res ^ -0.965
                        ElseIf Res < 1000 Then
                            fs = 7.524 * Res ^ -0.4
                        Else
                            fs = 1.01 * Res ^ -0.104
                        End If
                    ElseIf pitch / de <= 1.5 Then
                        If Res < 100 Then
                            fs = 58.18 * Res ^ -0.862
                        ElseIf Res < 1000 Then
                            fs = 6.76 * Res ^ -0.411
                        Else
                            fs = 0.718 * Res ^ -0.008
                        End If
                    Else
                        Throw New Exception(String.Format("The ratio between tube spacing and tube external diameter needs to be less than or equal to 1.5 (current value: {0})", pitch / de))
                    End If
            End Select

            'Cx
            Dim Cx As Double = 0
            Select Case STProperties.Tube_Layout
                Case 0, 1
                    Cx = 1.154
                Case 2
                    Cx = 1.0#
                Case 3
                    Cx = 1.414
            End Select
            Dim Gsh, Ssh, Fh, Rsh, dis As Double
            If STProperties.Shell_Fluid = 0 Then
                dps = 4 * fs * Gsf ^ 2 / (2 * rhoc) * Cx * (1 - HDi) * Dsi / pitch * Nb * (1 + Y * pitch / Dsi)
            Else
                dps = 4 * fs * Gsf ^ 2 / (2 * rhoh) * Cx * (1 - HDi) * Dsi / pitch * Nb * (1 + Y * pitch / Dsi)
            End If
            dps *= Nc

            'shell htc

            Dim M As Double = 0.96#
            dis = STProperties.Shell_Di / 1000
            Fh = 1 / (1 + Nh * (dis / pitch) ^ 0.5)
            Ssh = Ss * M / Fh
            Ssh = Math.PI / 4 * (Dsi ^ 2 - nt * de ^ 2)
            If STProperties.Shell_Fluid = 0 Then
                Gsh = Wc / Ssh
                Rsh = Gsh * de / muc
            Else
                Gsh = Wh / Ssh
                Rsh = Gsh * de / muh
            End If
            Dim Ec, lb, he As Double
            Select Case STProperties.Tube_Layout
                Case 0, 1
                    If Rsh < 100 Then
                        jh = 0.497 * Rsh ^ 0.54
                    Else
                        jh = 0.378 * Rsh ^ 0.61
                    End If
                Case 2, 3
                    If Rsh < 100 Then
                        jh = 0.385 * Rsh ^ 0.526
                    Else
                        jh = 0.2487 * Rsh ^ 0.625
                    End If
            End Select
            If STProperties.Shell_Fluid = 0 Then
                he = jh * kc * Prs ^ 0.34 / de
            Else
                he = jh * kh * Prs ^ 0.34 / de
            End If
            Dim Bs As Double = STProperties.Shell_BaffleSpacing / 1000
            lb = Bs * (Nb - 1)
            If L - lb > 0 Then
                Ec = (lb + (L - lb) * (2 * Bs / (L - lb)) ^ 0.6) / L
            Else
                Ec = 1.0
            End If
            If Double.IsNaN(Ec) OrElse Ec <= 0 OrElse Ec > 1 Then Ec = 1.0
            he *= Ec

            'global HTC (U)

            Dim kt As Double = STProperties.Tube_ThermalConductivity
            Dim f2, f4 As Double
            f1 = de / (hi * di)
            f2 = rt * de / di
            f3 = de / (2 * kt) * Math.Log(de / di)
            f4 = rs
            f5 = 1 / he

            U = f1 + f2 + f3 + f4 + f5

            STProperties.OverallFoulingFactor = f2 + f4

            U = 1 / U

            STProperties.Ft = f1 'tube side
            STProperties.Fc = f3 'heat conductivity pipe
            STProperties.Fs = f5 'shell side
            STProperties.Ff = STProperties.OverallFoulingFactor
            STProperties.ReS = Res 'Reynolds number shell side
            STProperties.ReT = Ret 'Reynolds number tube side

        End Sub

        ''' <summary>Performs the dynamic-mode calculation for the heat exchanger.</summary>
        Public Overrides Sub RunDynamicModel()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds

            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0

            Dim KrCold As Double = GetDynamicProperty("Cold Fluid Flow Conductance")
            Dim KrHot As Double = GetDynamicProperty("Hot Fluid Flow Conductance")

            Dim VolumeCold As Double = GetDynamicProperty("Volume for Cold Fluid")
            Dim VolumeHot As Double = GetDynamicProperty("Volume for Hot Fluid")

            If CalcMode = HeatExchangerCalcMode.ShellandTube_Rating Then

                Dim Vshell, Vtubes As Double

                Vshell = Math.PI * (STProperties.Shell_Di / 1000) ^ 2 / 4 * STProperties.Tube_Length

                Vtubes = Math.PI * (STProperties.Tube_Di / 1000) ^ 2 / 4 * STProperties.Tube_Length * STProperties.Tube_NumberPerShell

                If STProperties.Tube_Fluid = 0 Then
                    'cold
                    VolumeCold = Vtubes
                    VolumeHot = Vshell - Vtubes
                Else
                    'hot
                    VolumeHot = Vtubes
                    VolumeCold = Vshell - Vtubes
                End If

            End If

            Dim InitializeFromInlet As Boolean = GetDynamicProperty("Initialize using Inlet Streams")

            Dim Pmin = GetDynamicProperty("Minimum Pressure")

            Dim Reset As Boolean = GetDynamicProperty("Reset Contents")

            Dim N As Integer = CInt(GetDynamicProperty("Number of Cells"))
            If N < 1 Then N = 1
            Dim substeps As Integer = CInt(GetDynamicProperty("Substeps"))
            If substeps < 1 Then substeps = 1
            Dim dt As Double = timestep / substeps

            If Reset Then
                AccumulationStreamsHot.Clear()
                AccumulationStreamsCold.Clear()
                WallTemperatures.Clear()
                SetDynamicProperty("Reset Contents", 0)
            End If

            Dim A As Double = Area
            Dim U As Double = OverallCoefficient
            Dim StIn0, StIn1, StOut0, StOut1, StInCold, StInHot, StOutHot, StOutCold As MaterialStream

            'Validate unitop status.
            Me.Validate()

            StIn0 = Me.GetInletMaterialStream(0)
            StIn1 = Me.GetInletMaterialStream(1)
            StOut0 = Me.GetOutletMaterialStream(0)
            StOut1 = Me.GetOutletMaterialStream(1)

            'Identify cold and hot streams by inlet temperature.
            If StIn0.GetTemperature() < StIn1.GetTemperature() Then
                StInCold = StIn0 : StInHot = StIn1 : StOutCold = StOut0 : StOutHot = StOut1
            Else
                StInCold = StIn1 : StInHot = StIn0 : StOutCold = StOut1 : StOutHot = StOut0
            End If

            'Reject calculation modes that have no transient meaning.
            Select Case CalcMode
                Case HeatExchangerCalcMode.CalcArea, HeatExchangerCalcMode.CalcTempColdOut,
                     HeatExchangerCalcMode.CalcBothTemp, HeatExchangerCalcMode.CalcTempHotOut,
                     HeatExchangerCalcMode.PinchPoint, HeatExchangerCalcMode.ThermalEfficiency,
                     HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor,
                     HeatExchangerCalcMode.OutletVaporFraction1, HeatExchangerCalcMode.OutletVaporFraction2
                    Throw New Exception("This calculation mode is not supported while in Dynamic Mode.")
            End Select

            Dim ppHot = StInHot.PropertyPackage
            Dim ppCold = StInCold.PropertyPackage

            'Initialize / reconcile the per-cell holdups (mirrors the Pipe dynamic model).
            InitSideCells(AccumulationStreamsHot, StInHot, StOutHot, VolumeHot, N, timestep, InitializeFromInlet)
            InitSideCells(AccumulationStreamsCold, StInCold, StOutCold, VolumeCold, N, timestep, InitializeFromInlet)

            If WallTemperatures.Count <> N Then
                Dim tw0 As Double = GetDynamicProperty("Wall Temperature")
                WallTemperatures = New List(Of Double)
                For i As Integer = 0 To N - 1
                    WallTemperatures.Add(tw0)
                Next
            End If
            If prevMHot Is Nothing OrElse prevMHot.Count <> N Then
                prevMHot = New List(Of Double) : currentMHot = New List(Of Double)
                prevMCold = New List(Of Double) : currentMCold = New List(Of Double)
                For i As Integer = 0 To N - 1
                    prevMHot.Add(0.0) : currentMHot.Add(0.0)
                    prevMCold.Add(0.0) : currentMCold.Add(0.0)
                Next
            End If

            Dim VolCellHot As Double = VolumeHot / N
            Dim VolCellCold As Double = VolumeCold / N

            Dim coldInletIdx As Integer = If(FlowDir = FlowDirection.CounterCurrent, N - 1, 0)
            Dim coldOutletIdx As Integer = If(FlowDir = FlowDirection.CounterCurrent, 0, N - 1)

            Dim foulingRate As Double = GetDynamicProperty("Fouling Rate")
            Dim wallMCp As Double = GetDynamicProperty("Wall Thermal Mass")

            Dim QtotalEnergy As Double = 0.0   'kJ accumulated over all sub-steps
            Dim dpHotSide As Double = 0.0, dpColdSide As Double = 0.0

            Dim Th_in As Double = StInHot.GetTemperature()
            Dim Tc_in As Double = StInCold.GetTemperature()
            Dim Ph_in As Double = StInHot.GetPressure()
            Dim Pc_in As Double = StInCold.GetPressure()

            For ti As Integer = 1 To substeps

                'Advance fouling once per sub-step.
                Dim currentFouling As Double = GetDynamicProperty("Current Fouling Resistance")
                If foulingRate > 0 Then
                    currentFouling += foulingRate * dt
                    SetDynamicProperty("Current Fouling Resistance", currentFouling)
                End If

                '--- A. feed inlets ---
                If Not Double.IsNaN(StInHot.GetMassFlow()) AndAlso StInHot.GetMassFlow() > 0 Then
                    AccumulationStreamsHot(0) = AccumulationStreamsHot(0).Add(StInHot, dt)
                End If
                If Not Double.IsNaN(StInCold.GetMassFlow()) AndAlso StInCold.GetMassFlow() > 0 Then
                    AccumulationStreamsCold(coldInletIdx) = AccumulationStreamsCold(coldInletIdx).Add(StInCold, dt)
                End If

                '--- B. hot advection 0 -> N-1, C. cold advection per FlowDir (plug flow, donor-clamped) ---
                AdvectPlugFlow(AccumulationStreamsHot, StInHot.GetMassFlow(), dt, True)
                AdvectPlugFlow(AccumulationStreamsCold, StInCold.GetMassFlow(), dt, FlowDir <> FlowDirection.CounterCurrent)

                '--- U source for this sub-step (single scalar U; cell core multiplies by A/N) ---
                Dim Aexch As Double = Area
                Dim dpsST As Double = 0.0, dptST As Double = 0.0
                Select Case CalcMode
                    Case HeatExchangerCalcMode.ShellandTube_Rating
                        Dim rhoc = AccumulationStreamsCold.Average(Function(s) s.Phases(0).Properties.density.GetValueOrDefault)
                        Dim Cpc = AccumulationStreamsCold.Average(Function(s) s.Phases(0).Properties.heatCapacityCp.GetValueOrDefault)
                        Dim kc = AccumulationStreamsCold.Average(Function(s) s.Phases(0).Properties.thermalConductivity.GetValueOrDefault)
                        Dim muc = AccumulationStreamsCold.Average(Function(s) s.Phases(0).Properties.viscosity.GetValueOrDefault)
                        Dim rhoh = AccumulationStreamsHot.Average(Function(s) s.Phases(0).Properties.density.GetValueOrDefault)
                        Dim Cph = AccumulationStreamsHot.Average(Function(s) s.Phases(0).Properties.heatCapacityCp.GetValueOrDefault)
                        Dim kh = AccumulationStreamsHot.Average(Function(s) s.Phases(0).Properties.thermalConductivity.GetValueOrDefault)
                        Dim muh = AccumulationStreamsHot.Average(Function(s) s.Phases(0).Properties.viscosity.GetValueOrDefault)
                        Dim Uloc, Aloc, Res, Ret, f1, f3, f5, vt, Gsf, Ssf As Double
                        ComputeShellAndTubeU(rhoc, Cpc, kc, muc, rhoh, Cph, kh, muh,
                                             StInCold.GetMassFlow(), StInHot.GetMassFlow(),
                                             Uloc, Aloc, dpsST, dptST, Res, Ret, f1, f3, f5, vt, Gsf, Ssf)
                        U = Uloc : Aexch = Aloc : A = Aloc
                        Dim rhoShellFluid = If(STProperties.Shell_Fluid = 0, rhoc, rhoh)
                        Dim rhoTubeFluid = If(STProperties.Tube_Fluid = 0, rhoc, rhoh)
                        Dim WShellFlow = If(STProperties.Shell_Fluid = 0, StInCold.GetMassFlow(), StInHot.GetMassFlow())
                        Dim WTubeFlow = If(STProperties.Tube_Fluid = 0, StInCold.GetMassFlow(), StInHot.GetMassFlow())
                        Dim PShell = If(STProperties.Shell_Fluid = 0, Pc_in, Ph_in)
                        Dim PTube = If(STProperties.Tube_Fluid = 0, Pc_in, Ph_in)
                        Dim TShell = If(STProperties.Shell_Fluid = 0, Tc_in, Th_in)
                        Dim TTube = If(STProperties.Tube_Fluid = 0, Tc_in, Th_in)
                        STProperties.CalcDetailedResults(Ssf, vt, Gsf, rhoShellFluid, rhoTubeFluid,
                            WShellFlow, rhoShellFluid, rhoShellFluid, WTubeFlow, rhoTubeFluid, rhoTubeFluid,
                            PShell, TShell, PTube, TTube)
                    Case Else 'CalcBothTemp_UA
                        U = OverallCoefficient : Aexch = Area : A = Area
                End Select

                Dim Ueff As Double = U
                If currentFouling > 0 AndAlso U > 0 Then Ueff = 1.0 / (1.0 / U + currentFouling)

                Dim Acell As Double = Aexch / N

                '--- D. heat transfer per cell (local driving force, optional wall, anti-cross clamp) ---
                For i As Integer = 0 To N - 1

                    Dim hcell = AccumulationStreamsHot(i)
                    Dim ccell = AccumulationStreamsCold(i)

                    'Refresh cell state after advection so temperatures/Cp reflect the mixed holdup.
                    hcell.PropertyPackage = ppHot : hcell.SetFlowsheet(FlowSheet) : ppHot.CurrentMaterialStream = hcell
                    hcell.SpecType = StreamSpec.Pressure_and_Enthalpy : hcell.Calculate()
                    ccell.PropertyPackage = ppCold : ccell.SetFlowsheet(FlowSheet) : ppCold.CurrentMaterialStream = ccell
                    ccell.SpecType = StreamSpec.Pressure_and_Enthalpy : ccell.Calculate()

                    Dim mh = hcell.GetMassFlow(), mc = ccell.GetMassFlow()
                    If mh <= 0 Or mc <= 0 Then Continue For

                    Dim Th = hcell.GetTemperature(), Tc = ccell.GetTemperature()
                    Dim Hh = hcell.GetMassEnthalpy(), Hc = ccell.GetMassEnthalpy()
                    Dim CpH = hcell.Phases(0).Properties.heatCapacityCp.GetValueOrDefault
                    Dim CpC = ccell.Phases(0).Properties.heatCapacityCp.GetValueOrDefault

                    Dim Qhot As Double, Qcold As Double 'kJ over the sub-step
                    If wallMCp > 0 Then
                        Dim wallMCp_cell = wallMCp / N
                        Dim Twall = WallTemperatures(i)
                        Dim QhotToWall = Ueff / 1000.0 * Acell * (Th - Twall)  'kW
                        Dim QwallToCold = Ueff / 1000.0 * Acell * (Twall - Tc) 'kW
                        Twall += (QhotToWall - QwallToCold) * 1000.0 * dt / wallMCp_cell
                        WallTemperatures(i) = Twall
                        Qhot = QhotToWall * dt
                        Qcold = QwallToCold * dt
                    Else
                        Dim Ql = Ueff / 1000.0 * Acell * (Th - Tc) * dt 'kJ, local driving force (no LMTD)
                        Qhot = Ql : Qcold = Ql
                    End If

                    'Anti temperature-cross clamp (kJ vs kJ): limit a single explicit step.
                    Dim Qmax = 0.5 * Math.Min(mh * CpH, mc * CpC) * (Th - Tc)
                    If Th > Tc Then
                        If Qhot > Qmax Then Qhot = Qmax
                        If Qcold > Qmax Then Qcold = Qmax
                    Else
                        If Qhot < Qmax Then Qhot = Qmax
                        If Qcold < Qmax Then Qcold = Qmax
                    End If

                    hcell.SetMassEnthalpy(Hh - Qhot / mh)
                    ccell.SetMassEnthalpy(Hc + (Qcold - HeatLoss * dt / N) / mc)
                    hcell.SpecType = StreamSpec.Pressure_and_Enthalpy : hcell.Calculate()
                    ccell.SpecType = StreamSpec.Pressure_and_Enthalpy : ccell.Calculate()

                    QtotalEnergy += Qcold

                Next

                '--- E. per-cell pressure via compressibility flash ---
                For i As Integer = 0 To N - 1
                    UpdateCellPressure(AccumulationStreamsHot(i), VolCellHot, prevMHot, currentMHot, i, Pmin, integrator.ShouldCalculateEquilibrium, ppHot)
                    UpdateCellPressure(AccumulationStreamsCold(i), VolCellCold, prevMCold, currentMCold, i, Pmin, integrator.ShouldCalculateEquilibrium, ppCold)
                Next

                '--- F. side pressure drop superimposed as a linear gradient ---
                If CalcMode = HeatExchangerCalcMode.ShellandTube_Rating Then
                    If STProperties.Shell_Fluid = 0 Then
                        dpColdSide = dpsST : dpHotSide = dptST
                    Else
                        dpColdSide = dptST : dpHotSide = dpsST
                    End If
                Else
                    dpHotSide = (StInHot.GetMassFlow() / KrHot) ^ 2
                    dpColdSide = (StInCold.GetMassFlow() / KrCold) ^ 2
                End If
                ApplyPressureGradient(AccumulationStreamsHot, dpHotSide, True, N)
                ApplyPressureGradient(AccumulationStreamsCold, dpColdSide, FlowDir <> FlowDirection.CounterCurrent, N)

                '--- G. drain outlets ---
                If Not Double.IsNaN(StOutHot.GetMassFlow()) AndAlso StOutHot.GetMassFlow() > 0 Then
                    AccumulationStreamsHot(N - 1) = AccumulationStreamsHot(N - 1).Subtract(StOutHot, dt)
                End If
                If AccumulationStreamsHot(N - 1).GetMassFlow() <= 0.0 Then AccumulationStreamsHot(N - 1).SetMassFlow(0.0000000001)
                If Not Double.IsNaN(StOutCold.GetMassFlow()) AndAlso StOutCold.GetMassFlow() > 0 Then
                    AccumulationStreamsCold(coldOutletIdx) = AccumulationStreamsCold(coldOutletIdx).Subtract(StOutCold, dt)
                End If
                If AccumulationStreamsCold(coldOutletIdx).GetMassFlow() <= 0.0 Then AccumulationStreamsCold(coldOutletIdx).SetMassFlow(0.0000000001)

            Next 'ti sub-step

            '--- reporting ---

            Dim hotOutCell = AccumulationStreamsHot(N - 1)
            Dim coldOutCell = AccumulationStreamsCold(coldOutletIdx)

            Dim Th2 As Double = hotOutCell.GetTemperature()
            Dim Tc2 As Double = coldOutCell.GetTemperature()
            Dim Ph2 As Double = hotOutCell.GetPressure()
            Dim Pc2 As Double = coldOutCell.GetPressure()

            'Reported duty (kW) from the accumulated cell energy over the full integration step.
            Q = QtotalEnergy / timestep

            'LMTD is an emergent diagnostic only (it never feeds the heat-transfer calculation).
            Select Case Me.FlowDir
                Case FlowDirection.CoCurrent
                    If (Th_in - Tc_in) / (Th2 - Tc2) = 1 Then
                        LMTD = ((Th_in - Tc_in) + (Th2 - Tc2)) / 2
                    Else
                        LMTD = ((Th_in - Tc_in) - (Th2 - Tc2)) / Math.Log((Th_in - Tc_in) / (Th2 - Tc2))
                    End If
                Case FlowDirection.CounterCurrent
                    If (Th_in - Tc2) / (Th2 - Tc_in) = 1 Then
                        LMTD = ((Th_in - Tc2) + (Th2 - Tc_in)) / 2
                    Else
                        LMTD = ((Th_in - Tc2) - (Th2 - Tc_in)) / Math.Log((Th_in - Tc2) / (Th2 - Tc_in))
                    End If
            End Select
            LMTD *= CorrectionFactorLMTD
            If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then LMTD = 0.0

            Dim MaxQ As Double = ComputeMaxHeatExchange(hotOutCell, coldOutCell, StInHot, StInCold, Th_in, Tc_in)
            MaxHeatExchange = MaxQ
            If MaxQ <> 0.0 Then ThermalEfficiency = (Q - HeatLoss) / MaxQ * 100 Else ThermalEfficiency = 0.0

            ColdSideOutletTemperature = Tc2
            HotSideOutletTemperature = Th2
            ColdSidePressureDrop = Pc_in - Pc2
            HotSidePressureDrop = Ph_in - Ph2
            OverallCoefficient = U
            Area = A

            SetDynamicProperty("Cold Side Pressure", AccumulationStreamsCold.Average(Function(s) s.GetPressure()))
            SetDynamicProperty("Hot Side Pressure", AccumulationStreamsHot.Average(Function(s) s.GetPressure()))
            SetDynamicProperty("Wall Temperature", WallTemperatures.Average())

            StOutHot.AssignFromPhase(PhaseLabel.Mixture, hotOutCell, False)
            StOutCold.AssignFromPhase(PhaseLabel.Mixture, coldOutCell, False)
            StOutHot.DefinedFlow = FlowSpec.Mass
            StOutCold.DefinedFlow = FlowSpec.Mass

            StInHot.SetPressure(AccumulationStreamsHot(0).GetPressure())
            StInCold.SetPressure(AccumulationStreamsCold(coldInletIdx).GetPressure())

            If Th2 < Tc_in Or Tc2 > Th_in Then
                FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": Temperature Cross", IFlowsheet.MessageType.Warning)
            End If

        End Sub

        ''' <summary>
        ''' Initializes or reconciles the per-cell holdup streams for one side of the exchanger.
        ''' Rebuilds the list when its length does not match the requested cell count (mirrors the Pipe model).
        ''' </summary>
        Private Sub InitSideCells(streams As List(Of MaterialStream), stIn As MaterialStream, stOut As MaterialStream,
                                  sideVol As Double, N As Integer, timestep As Double, initFromInlet As Boolean)
            If streams.Count <> N Then
                streams.Clear()
                For i As Integer = 0 To N - 1
                    Dim cell As MaterialStream
                    If initFromInlet Then
                        cell = DirectCast(stIn.CloneXML(), MaterialStream)
                    Else
                        cell = stIn.Subtract(stOut, timestep)
                        cell = cell.Subtract(stOut, timestep)
                    End If
                    Dim density = cell.Phases(0).Properties.density.GetValueOrDefault
                    cell.SetMassFlow(density * sideVol / N)
                    cell.SpecType = StreamSpec.Pressure_and_Enthalpy
                    cell.PropertyPackage = stIn.PropertyPackage
                    cell.SetFlowsheet(FlowSheet)
                    cell.PropertyPackage.CurrentMaterialStream = cell
                    cell.Calculate()
                    streams.Add(cell)
                Next
            Else
                For Each cell In streams
                    If cell.GetMassFlow() <= 0.0 Then cell.SetMassFlow(0.0)
                    For Each p In cell.Phases.Values
                        For Each comp In p.Compounds.Values
                            comp.ConstantProperties = FlowSheet.SelectedCompounds(comp.Name)
                        Next
                    Next
                    cell.PropertyPackage = stIn.PropertyPackage
                    cell.SetFlowsheet(FlowSheet)
                Next
            End If
        End Sub

        ''' <summary>Moves the throughput mass cell-by-cell along a side (plug flow), clamped to the donor holdup.</summary>
        Private Sub AdvectPlugFlow(streams As List(Of MaterialStream), throughputFlow As Double, dt As Double, forward As Boolean)
            Dim N = streams.Count
            If N <= 1 Then Return
            Dim moveMass = throughputFlow * dt
            If Double.IsNaN(moveMass) OrElse moveMass <= 0.0 Then Return
            If forward Then
                For i As Integer = 0 To N - 2
                    TransferCellMass(streams, i, i + 1, moveMass, dt)
                Next
            Else
                For i As Integer = N - 1 To 1 Step -1
                    TransferCellMass(streams, i, i - 1, moveMass, dt)
                Next
            End If
        End Sub

        Private Sub TransferCellMass(streams As List(Of MaterialStream), fromIdx As Integer, toIdx As Integer, moveMass As Double, dt As Double)
            Dim donorMass = streams(fromIdx).GetMassFlow()
            Dim move = Math.Min(moveMass, donorMass)
            If move <= 0.0 Then Return
            Dim ms = DirectCast(streams(fromIdx).CloneXML(), MaterialStream)
            ms.SetMassFlow(move / dt)
            streams(fromIdx) = streams(fromIdx).Subtract(ms, dt)
            streams(toIdx) = streams(toIdx).Add(ms, dt)
        End Sub

        ''' <summary>Updates a cell's pressure from its compressibility (fixed cell volume / accumulated moles).</summary>
        Private Sub UpdateCellPressure(cell As MaterialStream, Vcell As Double, prevM As List(Of Double), currentM As List(Of Double),
                                       idx As Integer, Pmin As Double, shouldCalcEq As Boolean, pp As PropertyPackages.PropertyPackage)
            cell.PropertyPackage = pp
            cell.SetFlowsheet(FlowSheet)
            pp.CurrentMaterialStream = cell
            Dim M = cell.GetMolarFlow()
            Dim P As Double
            If M > 0 Then
                prevM(idx) = currentM(idx)
                currentM(idx) = Vcell / M
                Dim liquidcase = Math.Abs(currentM(idx) - prevM(idx)) < 0.0001 AndAlso cell.Phases(1).Properties.molarfraction.GetValueOrDefault > 0.99999
                If cell.GetPressure() > 0 Then
                    If (prevM(idx) = 0.0 OrElse shouldCalcEq) AndAlso Not liquidcase Then
                        Dim result = pp.CalculateEquilibrium2(FlashCalculationType.VolumeTemperature, currentM(idx), cell.GetTemperature(), cell.GetPressure())
                        P = result.CalculatedPressure
                    ElseIf prevM(idx) > 0.0 Then
                        P = currentM(idx) / prevM(idx) * cell.GetPressure()
                    Else
                        P = cell.GetPressure()
                    End If
                Else
                    P = Pmin
                End If
            Else
                P = Pmin
            End If
            cell.SetPressure(P)
        End Sub

        ''' <summary>Superimposes a linear pressure drop along a side so the reported drop matches the lumped value.</summary>
        Private Sub ApplyPressureGradient(streams As List(Of MaterialStream), dP As Double, inletAtZero As Boolean, N As Integer)
            If dP = 0.0 Then Return
            If N <= 1 Then
                streams(0).SetPressure(streams(0).GetPressure() - dP)
                Return
            End If
            For i As Integer = 0 To N - 1
                Dim frac = If(inletAtZero, CDbl(i), CDbl(N - 1 - i)) / CDbl(N - 1)
                streams(i).SetPressure(streams(i).GetPressure() - dP * frac)
            Next
        End Sub

        ''' <summary>Maximum theoretical heat exchange (kW) used only for the reported thermal efficiency.</summary>
        Private Function ComputeMaxHeatExchange(hotCell As MaterialStream, coldCell As MaterialStream,
                                                stInHot As MaterialStream, stInCold As MaterialStream,
                                                ThIn As Double, TcIn As Double) As Double
            Dim Hh1 = hotCell.GetMassEnthalpy(), Hc1 = coldCell.GetMassEnthalpy()
            Dim tmpstr As MaterialStream = DirectCast(hotCell.Clone(), MaterialStream)
            tmpstr.PropertyPackage = hotCell.PropertyPackage
            tmpstr.SetFlowsheet(hotCell.FlowSheet)
            tmpstr.PropertyPackage.CurrentMaterialStream = tmpstr
            tmpstr.SetTemperature(TcIn)
            tmpstr.PropertyPackage.DW_CalcEquilibrium(PropertyPackages.FlashSpec.T, PropertyPackages.FlashSpec.P)
            tmpstr.Calculate(False, True)
            Dim HHx = tmpstr.Phases(0).Properties.enthalpy.GetValueOrDefault
            Dim DeltaHh = stInHot.GetMassFlow() * (Hh1 - HHx)

            ' The cold side's half of the bound asks what the cold stream would hold at the hot
            ' inlet temperature. That is a question about a state the exchanger never reaches, and
            ' its property package may have no answer there: steam tables stop well below the
            ' flame temperature of a combustion gas. When it cannot be had, the hot side's bound
            ' stands on its own; it is the looser of the two, never the wrong one.
            Dim DeltaHc As Double = Double.MaxValue

            Try
                tmpstr = DirectCast(coldCell.Clone(), MaterialStream)
                tmpstr.PropertyPackage = coldCell.PropertyPackage
                tmpstr.SetFlowsheet(coldCell.FlowSheet)
                tmpstr.PropertyPackage.CurrentMaterialStream = tmpstr
                tmpstr.SetTemperature(ThIn)
                tmpstr.PropertyPackage.DW_CalcEquilibrium(PropertyPackages.FlashSpec.T, PropertyPackages.FlashSpec.P)
                tmpstr.Calculate(False, True)
                HHx = tmpstr.Phases(0).Properties.enthalpy.GetValueOrDefault
                DeltaHc = stInCold.GetMassFlow() * (HHx - Hc1)
                tmpstr.PropertyPackage = Nothing
                tmpstr.Dispose()
            Catch ex As Exception
            End Try

            Return Math.Min(DeltaHc, DeltaHh)
        End Function

        ''' <summary>Calculates the heat-exchanger performance for the selected calculation mode.</summary>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("DWSIM has a model for the countercurrent, two-stream heat 
                                exchanger which supports phase change and multiple phases in a 
                                stream.")

            IObj?.Paragraphs.Add("Input Parameters")

            IObj?.Paragraphs.Add("The heat exchanger in DWSIM has five calculation modes: ")

            IObj?.Paragraphs.Add("1. Calculate hot fluid outlet temperature: you must provide the 
                              cold fluid outlet temperature and the exchange area to 
                              calculate the hot fluid temperature.")

            IObj?.Paragraphs.Add("2. Calculate cold fluid outlet temperature: in this mode, DWSIM 
                                needs the hot fluid outlet temperature and the exchange area to 
                                calculate the cold fluid temperature.")

            IObj?.Paragraphs.Add("3. Calculate both temperatures: in this mode, DWSIM needs the 
                              exchange area and the heat exchanged to calculate both 
                              temperatures.")

            IObj?.Paragraphs.Add("4. Calculate area: in this mode you must provide the HTC and both temperatures to calculate the exchange area.")

            IObj?.Paragraphs.Add("5. Rate a Shell and Tube exchanger: in this mode you must provide 
                          the exchanger geometry and DWSIM will calculate output 
                          temperatures, pressure drop on the shell and tubes, overall 
                          HTC, LMTD, and exchange area. This calculation mode uses a 
                          simplified version of Tinker's method for Shell and Tube 
                          exchanger calculations. ")

            IObj?.Paragraphs.Add("You can provide the pressure drop for both fluids in the exchanger for modes 1 to 4 only.")

            IObj?.Paragraphs.Add("Calculation Mode")

            IObj?.Paragraphs.Add("The heat exchanger in DWSIM is calculated using the simple  convection heat equation:")

            IObj?.Paragraphs.Add("<m>Q=UA\Delta T_{ml},</m>")

            IObj?.Paragraphs.Add("where: Q = heat transferred, A = heat transfer area (external 
                            surface) and <mi>\Delta T_{ml}</mi> = Logarithmic Mean Temperature 
                            Difference (LMTD). We also remember that:")

            IObj?.Paragraphs.Add("<m>Q=m\Delta H,</m>")

            IObj?.Paragraphs.Add("where: <mi>Q</mi> = heat transferred from/to the fluid and <mi>\Delta H</mi> = outlet-inlet enthalpy difference.")

            IObj?.Paragraphs.Add("The calculation procedure depends on the mode selected:")

            IObj?.Paragraphs.Add("1. Calculate hot fluid outlet temperature: HTC (Heat Transfer Coefficient), hot fluid outlet temperature, heat load and LMTD.")

            IObj?.Paragraphs.Add("2. Calculate cold fluid outlet temperature: HTC, cold fluid outlet temperature, heat load and LMTD.")

            IObj?.Paragraphs.Add("3. Calculate both temperatures: HTC, cold and hot fluid outlet temperatures and LMTD.")

            IObj?.Paragraphs.Add("4. Calculate area: exchange area and LMTD.")

            IObj?.Paragraphs.Add("5. Rate Shell and Tube exchanger: exchanger geometry information.")

            IObj?.Paragraphs.Add("<h2>Inlet Streams</h2>")

            Dim Ti1, Ti2, w1, w2, A, Tc1, Th1, Wc, Wh, P1, P2, Th2, Tc2, U As Double
            Dim Pc1, Ph1, Pc2, Ph2, DeltaHc, DeltaHh, H1, H2, Hc1, Hh1, Hc2, Hh2, CPC, CPH As Double
            Dim StIn0, StIn1, StOut0, StOut1, StInCold, StInHot, StOutHot, StOutCold As MaterialStream
            Dim coldidx As Integer = 0

            'Validate unitop status.
            Me.Validate()

            HeatProfile = New Double() {}
            TemperatureProfileCold = New Double() {}
            TemperatureProfileHot = New Double() {}

            StIn0 = Me.GetInletMaterialStream(0)
            StIn1 = Me.GetInletMaterialStream(1)

            StOut0 = Me.GetOutletMaterialStream(0)
            StOut1 = Me.GetOutletMaterialStream(1)

            If DebugMode Then AppendDebugLine("Calculation mode: " & CalcMode.ToString)
            If DebugMode Then AppendDebugLine("Validating inlet stream 1...")
            StIn0.Validate()
            If DebugMode Then AppendDebugLine("Validating inlet stream 2...")
            StIn1.Validate()

            'First input stream.
            Ti1 = StIn0.Phases(0).Properties.temperature.GetValueOrDefault
            w1 = StIn0.Phases(0).Properties.massflow.GetValueOrDefault
            P1 = StIn0.Phases(0).Properties.pressure.GetValueOrDefault
            H1 = StIn0.Phases(0).Properties.enthalpy.GetValueOrDefault
            'Second input stream.
            Ti2 = StIn1.Phases(0).Properties.temperature.GetValueOrDefault
            w2 = StIn1.Phases(0).Properties.massflow.GetValueOrDefault
            P2 = StIn1.Phases(0).Properties.pressure.GetValueOrDefault
            H2 = StIn1.Phases(0).Properties.enthalpy.GetValueOrDefault

            'Let us use properties at the entrance as an initial implementation.

            If Ti1 < Ti2 Then
                'Input1 is the cold stream.
                Tc1 = Ti1
                Th1 = Ti2
                Wc = w1
                Wh = w2
                Pc1 = P1
                Ph1 = P2
                Hc1 = H1
                Hh1 = H2
                coldidx = 0
                'Identify cold and hot streams.
                StInCold = StIn0
                StInHot = StIn1
                StOutCold = StOut0
                StOutHot = StOut1
            Else
                'Input2 is the cold stream.
                Tc1 = Ti2
                Th1 = Ti1
                Wc = w2
                Wh = w1
                Pc1 = P2
                Ph1 = P1
                Hc1 = H2
                Hh1 = H1
                coldidx = 1
                'Identify cold and hot streams.
                StInCold = StIn1
                StInHot = StIn0
                StOutCold = StOut1
                StOutHot = StOut0
            End If

            IObj?.Paragraphs.Add(String.Format("<h3>Cold Stream: {0}</h3>", StInCold.GraphicObject.Tag))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", StInCold.Phases(0).Properties.temperature.GetValueOrDefault))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", StInCold.Phases(0).Properties.pressure.GetValueOrDefault))
            IObj?.Paragraphs.Add(String.Format("Mass Flow: {0} kg/s", StInCold.Phases(0).Properties.massflow.GetValueOrDefault))
            IObj?.Paragraphs.Add(String.Format("Specific Enthalpy: {0} kJ/kg", StInCold.Phases(0).Properties.enthalpy.GetValueOrDefault))


            IObj?.Paragraphs.Add(String.Format("<h3>Hot Stream: {0}</h3>", StInHot.GraphicObject.Tag))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", StInHot.Phases(0).Properties.temperature.GetValueOrDefault))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", StInHot.Phases(0).Properties.pressure.GetValueOrDefault))
            IObj?.Paragraphs.Add(String.Format("Mass Flow: {0} kg/s", StInHot.Phases(0).Properties.massflow.GetValueOrDefault))
            IObj?.Paragraphs.Add(String.Format("Specific Enthalpy: {0} kJ/kg", StInHot.Phases(0).Properties.enthalpy.GetValueOrDefault))

            IObj?.Paragraphs.Add("<h2>Maximum Heat Exchange</h2>")

            IObj?.Paragraphs.Add("Calculating maximum theoretical heat exchange...")

            IObj?.Paragraphs.Add("The maximum theoretical heat exchange is calculated as the smallest value from")

            IObj?.Paragraphs.Add("<m>Q_{max,hot}=W_{hot}(H_{hot,in}-H_{hot,c})</m>")
            IObj?.Paragraphs.Add("<m>Q_{max,cold}=W_{cold}(H_{cold,h}-H_{cold,in})</m>")

            IObj?.Paragraphs.Add("where")
            IObj?.Paragraphs.Add("<mi>H_{hot,in}</mi> is the hot stream inlet enthalpy")
            IObj?.Paragraphs.Add("<mi>H_{hot,c}</mi> is the hot stream enthalpy at cold stream inlet temperature")
            IObj?.Paragraphs.Add("<mi>H_{cold,in}</mi> is the cold stream inlet enthalpy ")
            IObj?.Paragraphs.Add("<mi>H_{cold,h}</mi> is the cold stream enthalpy at hot stream inlet temperature")

            Pc2 = Pc1 - ColdSidePressureDrop
            Ph2 = Ph1 - HotSidePressureDrop

            If DebugMode Then AppendDebugLine(StInCold.GraphicObject.Tag & " is the cold stream.")
            If DebugMode Then AppendDebugLine(StInHot.GraphicObject.Tag & " is the hot stream.")

            'calculate maximum theoretical heat exchange

            Dim HHx As Double
            Dim tmpstr As MaterialStream = StInHot.Clone

            tmpstr = StInHot.Clone
            tmpstr.PropertyPackage = StInHot.PropertyPackage
            tmpstr.SetFlowsheet(StInHot.FlowSheet)
            tmpstr.AssignSelfToPP()
            tmpstr.SetTemperature(Tc1)
            tmpstr.SetPressure(Ph2)
            tmpstr.SetFlashSpec("PT")
            IObj?.SetCurrent()
            tmpstr.Calculate()
            HHx = tmpstr.GetMassEnthalpy()
            DeltaHh = Wh * (Hh1 - HHx) 'kW

            IObj?.Paragraphs.Add("<mi>Q_{max,hot}</mi> = " & DeltaHh & " kW")

            ' What the cold stream would hold at the hot inlet temperature. That is a state the
            ' exchanger never reaches, and the cold side's property package may not cover it: the
            ' steam tables stop well below the flame temperature of a combustion gas. When it
            ' cannot be had, the hot side's bound stands alone, which is looser and never wrong.
            DeltaHc = Double.MaxValue

            Try
                tmpstr = StInCold.Clone
                tmpstr.PropertyPackage = StInCold.PropertyPackage
                tmpstr.SetFlowsheet(StInHot.FlowSheet)
                tmpstr.AssignSelfToPP()
                tmpstr.SetTemperature(Th1)
                tmpstr.SetPressure(Pc2)
                tmpstr.SetFlashSpec("PT")
                IObj?.SetCurrent()
                tmpstr.Calculate()
                HHx = tmpstr.GetMassEnthalpy()
                DeltaHc = Wc * (HHx - Hc1) 'kW
            Catch ex As Exception
                If DebugMode Then AppendDebugLine("Could not bound the cold side of the maximum heat exchange: " & ex.Message)
            End Try

            IObj?.Paragraphs.Add("<mi>Q_{max,cold}</mi> = " & DeltaHc & " kW")

            If FlowDir = FlowDirection.CounterCurrent Then

                MaxHeatExchange = Min(DeltaHc, DeltaHh) 'kW

            Else

                MaxHeatExchange = MathNet.Numerics.RootFinding.Brent.FindRoot(
                    Function(q)

                        tmpstr = StInHot.Clone
                        tmpstr.PropertyPackage = StInHot.PropertyPackage
                        tmpstr.SetFlowsheet(StInHot.FlowSheet)
                        tmpstr.AssignSelfToPP()
                        tmpstr.SetPressure(Ph1)
                        tmpstr.SetMassEnthalpy(Hh1 - q / Wh)
                        IObj?.SetCurrent()
                        tmpstr.SetFlashSpec("PH")
                        tmpstr.Calculate()

                        Dim Thx = tmpstr.GetTemperature()

                        tmpstr = StInCold.Clone
                        tmpstr.PropertyPackage = StInCold.PropertyPackage
                        tmpstr.SetFlowsheet(StInCold.FlowSheet)
                        tmpstr.AssignSelfToPP()
                        tmpstr.SetPressure(Pc1)
                        tmpstr.SetMassEnthalpy(Hc1 + q / Wc)
                        IObj?.SetCurrent()
                        tmpstr.SetFlashSpec("PH")
                        tmpstr.Calculate()

                        Dim Tcx = tmpstr.GetTemperature()

                        Return Thx - Tcx

                    End Function, 0.0, MaxHeatExchange)

            End If

            IObj?.Paragraphs.Add("<mi>Q_{max}</mi> = " & MaxHeatExchange & " kW")

            tmpstr.PropertyPackage = Nothing
            tmpstr.Dispose()
            tmpstr = Nothing

            If DebugMode Then AppendDebugLine("Maximum possible heat exchange is " & MaxHeatExchange.ToString & " kW.")

            'Copy properties from the input streams.
            StOut0.Assign(StIn0)
            StOut1.Assign(StIn1)

            CPC = StInCold.Phases(0).Properties.heatCapacityCp.GetValueOrDefault
            CPH = StInHot.Phases(0).Properties.heatCapacityCp.GetValueOrDefault

            IObj?.Paragraphs.Add("<h2>Actual Heat Exchange</h2>")

            IObj?.Paragraphs.Add("<mi>Q_{loss}</mi> = " & HeatLoss & " kW")

            IObj?.Paragraphs.Add("Calculating heat exchanged...")

            IObj?.Paragraphs.Add(String.Format("Calculation mode: {0}", [Enum].GetName(CalcMode.GetType, CalcMode)))

            Select Case CalcMode

                Case HeatExchangerCalcMode.ThermalEfficiency

                    Q = MaxHeatExchange * ThermalEfficiency / 100.0

                    If Q.GetValueOrDefault() / MaxHeatExchange > 1.001 Then
                        Throw New Exception("Defined heat exchange is invalid (higher than the theoretical maximum).")
                    End If

                    DeltaHc = Q / Wc
                    DeltaHh = -(Q + HeatLoss) / Wh
                    Hc2 = Hc1 + DeltaHc
                    Hh2 = Hh1 + DeltaHh

                    StInCold.PropertyPackage.CurrentMaterialStream = StInCold

                    If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate cold stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Pc2, Hc2))
                    IObj?.SetCurrent()
                    Dim tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Pc2, Hc2, Tc1)
                    Tc2 = OverriddenTemperature(StInCold, Pc2, Hc2, tmp.CalculatedTemperature)
                    Hh2 = Hh1 + DeltaHh
                    StInHot.PropertyPackage.CurrentMaterialStream = StInHot

                    If DebugMode Then AppendDebugLine(String.Format("Calculated cold stream outlet temperature T2 = {0} K", Tc2))
                    If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate hot stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Ph2, Hh2))

                    IObj?.SetCurrent()
                    tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, Th1)
                    Th2 = OverriddenTemperature(StInHot, Ph2, Hh2, tmp.CalculatedTemperature)

                    If DebugMode Then AppendDebugLine(String.Format("Calculated hot stream outlet temperature T2 = {0} K", Th2))

                    Select Case Me.FlowDir
                        Case FlowDirection.CoCurrent
                            LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                        Case FlowDirection.CounterCurrent
                            LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                    End Select

                    LMTD *= CorrectionFactorLMTD

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = OverallCoefficient.GetValueOrDefault

                    A = Q * 1000 / U / LMTD

                    Area = A

                Case HeatExchangerCalcMode.PinchPoint

                    Dim dhc, dhh, dq, fx As Double, nsteps As Integer

                    nsteps = 25

                    Dim tcprof, thprof, dtprof, qprof, seg_ua, seg_lmtd As New List(Of Double)

                    Dim brt As New MathOps.MathEx.BrentOpt.BrentMinimize

                    dq = brt.brentoptimize2(0, MaxHeatExchange, 0.01,
                                             Function(dqx)

                                                 dhc = dqx / Wc
                                                 dhh = dqx / Wh

                                                 'calculate profiles

                                                 tcprof.Clear()
                                                 thprof.Clear()
                                                 dtprof.Clear()
                                                 qprof.Clear()

                                                 tmpstr = StInCold.Clone
                                                 tmpstr.PropertyPackage = StInCold.PropertyPackage
                                                 tmpstr.SetFlowsheet(StInCold.FlowSheet)

                                                 For i As Integer = 0 To nsteps

                                                     tmpstr.Phases(0).Properties.enthalpy = Hc1 + Convert.ToDouble(i) / Convert.ToDouble(nsteps) * dhc
                                                     tmpstr.Phases(0).Properties.pressure = Pc1 - Convert.ToDouble(i) / Convert.ToDouble(nsteps) * ColdSidePressureDrop
                                                     tmpstr.SpecType = StreamSpec.Pressure_and_Enthalpy
                                                     IObj?.SetCurrent()
                                                     tmpstr.Calculate(True, True)

                                                     qprof.Add(i / nsteps * dqx)
                                                     tcprof.Add(tmpstr.Phases(0).Properties.temperature.GetValueOrDefault)

                                                 Next

                                                 tmpstr = StInHot.Clone
                                                 tmpstr.PropertyPackage = StInHot.PropertyPackage
                                                 tmpstr.SetFlowsheet(StInHot.FlowSheet)

                                                 For i As Integer = 0 To nsteps

                                                     tmpstr.Phases(0).Properties.enthalpy = Hh1 - Convert.ToDouble(i) / Convert.ToDouble(nsteps) * dhh
                                                     tmpstr.Phases(0).Properties.pressure = Ph1 - Convert.ToDouble(i) / Convert.ToDouble(nsteps) * HotSidePressureDrop
                                                     tmpstr.SpecType = StreamSpec.Pressure_and_Enthalpy
                                                     IObj?.SetCurrent()
                                                     tmpstr.Calculate(True, True)

                                                     thprof.Add(tmpstr.Phases(0).Properties.temperature.GetValueOrDefault)

                                                 Next

                                                 If Not PinchPointAtOutlets And FlowDir = FlowDirection.CounterCurrent Then
                                                     thprof.Reverse()
                                                 End If

                                                 seg_ua.Clear()
                                                 seg_lmtd.Clear()
                                                 For i As Integer = 0 To nsteps
                                                     dtprof.Add(Abs(thprof(i) - tcprof(i)))
                                                     If i > 0 Then
                                                         seg_lmtd.Add((dtprof(i) - dtprof(i - 1)) / Log(dtprof(i) / dtprof(i - 1)))
                                                         seg_ua.Add((qprof(i) - qprof(i - 1)) / seg_lmtd.Last)
                                                     End If
                                                 Next

                                                 fx = dtprof.Min - MITA

                                                 Return fx ^ 2

                                             End Function)

                    If Double.IsNaN(fx) Or Double.IsNaN(dhc) Then Throw New Exception("Error calculating temperature profile.")

                    Me.HeatProfile = qprof.ToArray
                    Me.TemperatureProfileCold = tcprof.ToArray
                    Me.TemperatureProfileHot = thprof.ToArray

                    dhc = dq / Wc

                    Hc2 = Hc1 + dhc
                    Q = dhc * Wc

                    Tc2 = tcprof.Last

                    Dim tmp As IFlashCalculationResult

                    DeltaHh = -(Q + HeatLoss) / Wh

                    Hh2 = Hh1 + DeltaHh
                    StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                    If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate hot stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Ph2, Hh2))
                    IObj?.SetCurrent()
                    tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, 0)
                    Th2 = OverriddenTemperature(StInHot, Ph2, Hh2, tmp.CalculatedTemperature)
                    If DebugMode Then AppendDebugLine(String.Format("Calculated hot stream outlet temperature T2 = {0} K", Th2))

                    LMTD = Q / seg_ua.Sum

                    LMTD *= CorrectionFactorLMTD

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = Me.OverallCoefficient

                    A = Q / (LMTD * U) * 1000

                    'If Double.IsNaN(A) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                Case HeatExchangerCalcMode.CalcBothTemp_UA

                    Dim Qi, Q_old, Q_older, PIc1, PIc2, PIh1, PIh2 As Double
                    Dim NTUh, NTUc, WWh, WWc, RRh, RRc, PPh, PPc As Double
                    Dim tmp As IFlashCalculationResult
                    Dim count As Integer
                    Dim alpha As Double = 0.5 'under-relaxation factor
                    Dim Qi_calc As Double 'unrelaxed Q from current iteration
                    Dim oscillating As Boolean = False
                    Dim WWc_max, WWh_max As Double 'caps for heat capacity rates during phase change
                    A = Area
                    U = OverallCoefficient
                    Qi = MaxHeatExchange * 0.5
                    Q_old = 10000000000.0
                    Q_older = 10000000000.0
                    Tc2 = Tc1 + (Th1 - Tc1) / 2 * 0.5
                    Th2 = Th1 - (Th1 - Tc1) / 2 * 0.5

                    'compute maximum heat capacity rate caps based on inlet sensible Cp
                    WWc_max = Wc * CPC * 1000 * 200
                    WWh_max = Wh * CPH * 1000 * 200
                    Dim convTol As Double = 0.001

                    If DebugMode Then AppendDebugLine(String.Format("Start with Max Heat Exchange Q = {0} KW", Qi))

                    Do

                        If DebugMode Then AppendDebugLine(String.Format("======================================================"))
                        If DebugMode Then AppendDebugLine(String.Format("Iteration loop: {0}", count))

                        Hc2 = Qi / Wc + Hc1
                        Hh2 = Hh1 - Qi / Wh - HeatLoss / Wh
                        StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                        IObj?.SetCurrent()
                        tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Pc2, Hc2, Tc2)
                        Tc2 = OverriddenTemperature(StInCold, Pc2, Hc2, tmp.CalculatedTemperature)
                        PIc2 = (1 + tmp.GetLiquidPhase1MoleFraction) * (1 + tmp.GetVaporPhaseMoleFraction * (1 + tmp.GetSolidPhaseMoleFraction)) 'phase indicator cold stream
                        If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate cold stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]  ===> Tc2 = {2} K", Pc2, Hc2, Tc2))

                        StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                        IObj?.SetCurrent()
                        tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, Th2)
                        Th2 = OverriddenTemperature(StInHot, Ph2, Hh2, tmp.CalculatedTemperature)
                        PIh2 = (1 + tmp.GetLiquidPhase1MoleFraction) * (1 + tmp.GetVaporPhaseMoleFraction * (1 + tmp.GetSolidPhaseMoleFraction)) 'phase indicator hot stream
                        If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate hot stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]  ===> Th2 = {2} K", Ph2, Hh2, Th2))

                        convTol = 0.001
                        If oscillating AndAlso count > 50 Then convTol = 0.005 'relax tolerance if oscillating for a long time
                        If Abs((Qi - Q_old) / Q_old) < convTol Or count > 300 Then Exit Do

                        'compute effective heat capacity rates with phase-change protection
                        Dim dTc As Double = Tc2 - Tc1
                        Dim dTh As Double = Th2 - Th1
                        If Abs(dTc) < 0.01 Then dTc = Math.Sign(dTc) * 0.01 'prevent division by near-zero deltaT
                        If Abs(dTh) < 0.01 Then dTh = Math.Sign(dTh) * 0.01
                        If dTc = 0 Then dTc = 0.01
                        If dTh = 0 Then dTh = -0.01
                        WWc = Wc * (Hc2 - Hc1) / dTc * 1000 'Heat Capacity Rate cold side
                        WWh = Wh * (Hh2 - Hh1) / dTh * 1000 'Heat Capacity Rate hot side
                        'clamp to prevent overflow during phase change (latent heat plateau)
                        WWc = Math.Max(Math.Abs(WWc), 1.0) * Math.Sign(WWc)
                        WWh = Math.Max(Math.Abs(WWh), 1.0) * Math.Sign(WWh)
                        If Math.Abs(WWc) > WWc_max Then WWc = Math.Sign(WWc) * WWc_max
                        If Math.Abs(WWh) > WWh_max Then WWh = Math.Sign(WWh) * WWh_max
                        NTUc = U * A / Math.Abs(WWc) 'Numbers of transfer units - cold side
                        NTUh = U * A / Math.Abs(WWh) 'Numbers of transfer units - hot side
                        RRc = Math.Abs(WWc) / Math.Abs(WWh) 'Heat capacity ratio cold side
                        RRh = Math.Abs(WWh) / Math.Abs(WWc) 'Heat capacity ratio hot side

                        If DebugMode Then AppendDebugLine(String.Format("Calculating heat exchanger"))
                        If DebugMode Then AppendDebugLine(String.Format("Number of Transfer Units - NTU_cold :{0}  NTU_hot: {1}", NTUc, NTUh))
                        If DebugMode Then AppendDebugLine(String.Format("Heat Capacity Rates - W_cold :{0}  W_hot: {1}", WWc, WWh))
                        If DebugMode Then AppendDebugLine(String.Format("Heat Capacity Ratios - R_cold :{0}  R_hot: {1}", RRc, RRh))

                        Select Case Me.FlowDir
                            Case FlowDirection.CoCurrent
                                PPc = (1 - Exp(-NTUc * (1 + RRc))) / (1 + RRc)
                                PPh = (1 - Exp(-NTUh * (1 + RRh))) / (1 + RRh)
                            Case FlowDirection.CounterCurrent
                                'special case: when R approaches 1, the general formula becomes 0/0
                                'the correct limit is P = NTU / (1 + NTU)
                                If Abs(RRc - 1) < 0.000001 Then
                                    PPc = NTUc / (1 + NTUc)
                                Else
                                    PPc = (1 - Exp((RRc - 1) * NTUc)) / (1 - RRc * Exp((RRc - 1) * NTUc))
                                End If
                                If Abs(RRh - 1) < 0.000001 Then
                                    PPh = NTUh / (1 + NTUh)
                                Else
                                    PPh = (1 - Exp((RRh - 1) * NTUh)) / (1 - RRh * Exp((RRh - 1) * NTUh))
                                End If
                        End Select
                        If DebugMode Then AppendDebugLine(String.Format("Dimensionless Temp Change - P_cold :{0}  P_hot: {1}", PPc, PPh))

                        If Double.IsNaN(PPc) Or Double.IsNaN(PPh) Then
                            Throw New Exception("failed to calculate the Number of Transfer Units (NTU) with the current input and specs")
                        End If

                        Tc2 = Tc1 + PPc * (Th1 - Tc1)
                        Th2 = Th1 - PPh * (Th1 - Tc1)
                        If DebugMode Then AppendDebugLine(String.Format("Outlet Temperatures - Tc2 :{0} K  Th2: {1} K", Tc2, Th2))

                        'LMTD calculation with improved robustness for phase-change cases
                        Dim dT1_lmtd, dT2_lmtd As Double
                        Select Case Me.FlowDir
                            Case FlowDirection.CoCurrent
                                dT1_lmtd = Th1 - Tc1
                                dT2_lmtd = Th2 - Tc2
                            Case FlowDirection.CounterCurrent
                                dT1_lmtd = Th1 - Tc2
                                dT2_lmtd = Th2 - Tc1
                        End Select

                        'guard against invalid temperature differences
                        If dT1_lmtd < 0.001 Then dT1_lmtd = 0.001
                        If dT2_lmtd < 0.001 Then dT2_lmtd = 0.001

                        Dim ratio_lmtd As Double = dT1_lmtd / dT2_lmtd
                        If Abs(ratio_lmtd - 1.0) < 0.0001 Then
                            'near-equal temperature differences: use arithmetic mean (limiting case of LMTD)
                            LMTD = (dT1_lmtd + dT2_lmtd) / 2
                        Else
                            LMTD = (dT1_lmtd - dT2_lmtd) / Math.Log(ratio_lmtd)
                        End If

                        LMTD *= CorrectionFactorLMTD

                        'detect oscillation: if Q alternates direction of change for 2+ consecutive iterations
                        If count >= 2 Then
                            If (Qi - Q_old) * (Q_old - Q_older) < 0 Then
                                oscillating = True
                                alpha = Math.Max(alpha * 0.85, 0.15) 'reduce relaxation factor progressively
                                If DebugMode Then AppendDebugLine(String.Format("Oscillation detected at iteration {0}, reducing alpha to {1}", count, alpha))
                            End If
                        End If

                        Q_older = Q_old
                        Q_old = Qi

                        If LMTD > 0 Then
                            Qi_calc = U * A * LMTD / 1000
                        Else
                            Qi_calc = Wh * (Hh1 - Hh2)
                            LMTD = Qi_calc / U / A * 1000
                        End If

                        'cap Q at maximum heat exchange
                        If Qi_calc > MaxHeatExchange Then Qi_calc = MaxHeatExchange
                        If Qi_calc < 0 Then Qi_calc = 0

                        'apply under-relaxation to prevent oscillation during phase change
                        If count > 0 Then
                            Qi = alpha * Qi_calc + (1 - alpha) * Q_old
                        Else
                            Qi = Qi_calc
                        End If

                        If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                        If DebugMode Then
                            AppendDebugLine(String.Format("Logarithmic Temperature Difference :{0} K", LMTD))
                            AppendDebugLine(String.Format("Heat Exchange Q_calc = {0} KW, Q_relaxed = {1} KW (alpha={2})", Qi_calc, Qi, alpha))
                            If oscillating Then AppendDebugLine("Note: oscillation damping is active")
                        End If

                        count += 1

                    Loop

                    ColdSideOutletTemperature = Tc2

                    Q = Qi

                    If count > 300 Then Throw New Exception("Reached maximum number of iterations! Final Q change: " & Qi - Q_old & " kW ; " & Abs((Qi - Q_old) / Q_old * 100) & " % ")

                    PIc1 = (1 + StInCold.Phases(1).Properties.molarfraction.GetValueOrDefault) * (1 + StInCold.Phases(2).Properties.molarfraction.GetValueOrDefault) * (1 + StInCold.Phases(7).Properties.molarfraction.GetValueOrDefault)
                    PIh1 = (1 + StInHot.Phases(1).Properties.molarfraction.GetValueOrDefault) * (1 + StInHot.Phases(2).Properties.molarfraction.GetValueOrDefault) * (1 + StInHot.Phases(7).Properties.molarfraction.GetValueOrDefault)

                    If (PIc1 = 2 And PIc2 > 2) Or (PIc1 > 2 And PIc2 = 2) Then FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": Phase change in cold stream detected! Heat exchange result is an aproximation.", IFlowsheet.MessageType.Warning)
                    If (PIh1 = 2 And PIh2 > 2) Or (PIh1 > 2 And PIh2 = 2) Then FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": Phase change in hot stream detected! Heat exchange result is an aproximation.", IFlowsheet.MessageType.Warning)

                Case HeatExchangerCalcMode.CalcBothTemp

                    If Q > MaxHeatExchange Then Throw New Exception("Defined heat exchange is invalid (higher than the theoretical maximum).")

                    A = Area
                    DeltaHc = Q / Wc
                    DeltaHh = -(Q + HeatLoss) / Wh
                    Hc2 = Hc1 + DeltaHc
                    Hh2 = Hh1 + DeltaHh

                    StInCold.PropertyPackage.CurrentMaterialStream = StInCold

                    If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate cold stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Pc2, Hc2))
                    IObj?.SetCurrent()
                    Dim tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Pc2, Hc2, Tc1)
                    Tc2 = OverriddenTemperature(StInCold, Pc2, Hc2, tmp.CalculatedTemperature)
                    Hh2 = Hh1 + DeltaHh
                    StInHot.PropertyPackage.CurrentMaterialStream = StInHot

                    If DebugMode Then AppendDebugLine(String.Format("Calculated cold stream outlet temperature T2 = {0} K", Tc2))
                    If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate hot stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Ph2, Hh2))

                    IObj?.SetCurrent()
                    tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, Th1)
                    Th2 = OverriddenTemperature(StInHot, Ph2, Hh2, tmp.CalculatedTemperature)

                    If DebugMode Then AppendDebugLine(String.Format("Calculated hot stream outlet temperature T2 = {0} K", Th2))

                    Select Case Me.FlowDir
                        Case FlowDirection.CoCurrent
                            LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                        Case FlowDirection.CounterCurrent
                            LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                    End Select

                    LMTD *= CorrectionFactorLMTD

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = Q / (A * LMTD) * 1000

                Case HeatExchangerCalcMode.CalcTempColdOut

                    A = Area
                    Th2 = TempHotOut

                    StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                    If DebugMode Then AppendDebugLine(String.Format("Doing a PT flash to calculate hot stream outlet enthalpy... P = {0} Pa, T = K", Ph2, Th2))
                    IObj?.SetCurrent()
                    Dim tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, Ph2, Th2, 0.0#)
                    Hh2 = OverriddenEnthalpy(StInHot, Ph2, Th2, tmp.CalculatedEnthalpy)
                    Q = -Wh * (Hh2 - Hh1)
                    If Q > MaxHeatExchange Then
                        Throw New Exception(String.Format("Invalid Outlet Temperature for Hot Fluid: {0} kW required but only {1} kW are available", Q, MaxHeatExchange))
                    End If
                    DeltaHc = (Q - HeatLoss) / Wc
                    Hc2 = Hc1 + DeltaHc
                    StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                    If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate cold stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Pc2, Hc2))
                    IObj?.SetCurrent()
                    tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Pc2, Hc2, Th2)
                    Tc2 = OverriddenTemperature(StInCold, Pc2, Hc2, tmp.CalculatedTemperature)
                    If DebugMode Then AppendDebugLine(String.Format("Calculated cold stream outlet temperature T2 = {0} K", Tc2))
                    Select Case Me.FlowDir
                        Case FlowDirection.CoCurrent
                            LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                        Case FlowDirection.CounterCurrent
                            LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                    End Select

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = Q / (A * LMTD) * 1000

                Case HeatExchangerCalcMode.CalcTempHotOut

                    A = Area
                    Tc2 = TempColdOut
                    StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                    If DebugMode Then AppendDebugLine(String.Format("Doing a PT flash to calculate cold stream outlet enthalpy... P = {0} Pa, T = K", Pc2, Tc2))
                    IObj?.SetCurrent()
                    Dim tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, Pc2, Tc2, 0)
                    Hc2 = OverriddenEnthalpy(StInCold, Pc2, Tc2, tmp.CalculatedEnthalpy)
                    Q = Wc * (Hc2 - Hc1)
                    If Q > MaxHeatExchange Then
                        Throw New Exception(String.Format("Invalid Outlet Temperature for Cold Fluid: {0} kW required but only {1} kW are available", Q, MaxHeatExchange))
                    End If
                    DeltaHh = -(Q + HeatLoss) / Wh
                    Hh2 = Hh1 + DeltaHh
                    StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                    IObj?.SetCurrent()
                    If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate hot stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Ph2, Hh2))
                    tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, Th1)
                    Th2 = OverriddenTemperature(StInHot, Ph2, Hh2, tmp.CalculatedTemperature)
                    If DebugMode Then AppendDebugLine(String.Format("Calculated hot stream outlet temperature T2 = {0} K", Th2))

                    Select Case Me.FlowDir
                        Case FlowDirection.CoCurrent
                            LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                        Case FlowDirection.CounterCurrent
                            LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                    End Select

                    LMTD *= CorrectionFactorLMTD

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = Q / (A * LMTD) * 1000

                Case HeatExchangerCalcMode.CalcArea

                    Select Case Me.DefinedTemperature
                        Case SpecifiedTemperature.Cold_Fluid
                            Tc2 = TempColdOut
                            StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                            If DebugMode Then AppendDebugLine(String.Format("Doing a PT flash to calculate cold stream outlet enthalpy... P = {0} Pa, T = {1} K", Pc2, Tc2))
                            IObj?.SetCurrent()
                            Dim tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, Pc2, Tc2, 0)
                            Hc2 = OverriddenEnthalpy(StInCold, Pc2, Tc2, tmp.CalculatedEnthalpy)
                            Q = Wc * (Hc2 - Hc1)
                            DeltaHh = -(Q + HeatLoss) / Wh
                            Hh2 = Hh1 + DeltaHh
                            StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                            If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate hot stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Ph2, Hh2))
                            IObj?.SetCurrent()
                            tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, 0)
                            Th2 = OverriddenTemperature(StInHot, Ph2, Hh2, tmp.CalculatedTemperature)
                            If DebugMode Then AppendDebugLine(String.Format("Calculated hot stream outlet temperature T2 = {0} K", Th2))
                        Case SpecifiedTemperature.Hot_Fluid
                            Th2 = TempHotOut
                            StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                            If DebugMode Then AppendDebugLine(String.Format("Doing a PT flash to calculate hot stream outlet enthalpy... P = {0} Pa, T = {1} K", Ph2, Th2))
                            IObj?.SetCurrent()
                            ' the hot outlet enthalpy comes from the hot stream; this asked the cold
                            ' stream's package for it, which answers with the cold composition
                            ' whenever the two sides do not share one package
                            Dim tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, Ph2, Th2, 0)
                            Hh2 = OverriddenEnthalpy(StInHot, Ph2, Th2, tmp.CalculatedEnthalpy)
                            Q = -Wh * (Hh2 - Hh1)
                            DeltaHc = (Q - HeatLoss) / Wc
                            Hc2 = Hc1 + DeltaHc
                            StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                            If DebugMode Then AppendDebugLine(String.Format("Doing a PH flash to calculate cold stream outlet temperature... P = {0} Pa, H = {1} kJ/[kg.K]", Pc2, Hc2))
                            IObj?.SetCurrent()
                            tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Pc2, Hc2, 0)
                            Tc2 = OverriddenTemperature(StInCold, Pc2, Hc2, tmp.CalculatedTemperature)
                            If DebugMode Then AppendDebugLine(String.Format("Calculated cold stream outlet temperature T2 = {0} K", Tc2))
                    End Select
                    Select Case Me.FlowDir
                        Case FlowDirection.CoCurrent
                            LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                        Case FlowDirection.CounterCurrent
                            LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                    End Select

                    LMTD *= CorrectionFactorLMTD

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = Me.OverallCoefficient

                    A = Q / (LMTD * U) * 1000

                Case HeatExchangerCalcMode.OutletVaporFraction1

                    Dim Q1, H20, H21, H10, H11, VF0, T10, T11, T20, T21, DP1, DP2 As Double

                    T10 = StIn0.GetTemperature()
                    T20 = StIn1.GetTemperature()

                    If T10 > T20 Then
                        P1 = Ph1
                        P2 = Pc1
                        DP1 = HotSidePressureDrop
                        DP2 = ColdSidePressureDrop
                    Else
                        P2 = Ph1
                        P1 = Pc1
                        DP2 = HotSidePressureDrop
                        DP1 = ColdSidePressureDrop
                    End If

                    A = Area

                    VF0 = StIn0.GetPhase("Vapor").Properties.molarfraction.GetValueOrDefault()
                    H10 = StIn0.GetMassEnthalpy()

                    StIn0.PropertyPackage.CurrentMaterialStream = StIn0
                    IObj?.SetCurrent()
                    Dim tmp = StIn0.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureVaporFraction, P1 - DP1, OutletVaporFraction1, T10)
                    T11 = tmp.CalculatedTemperature.GetValueOrDefault()
                    H11 = OverriddenEnthalpy(StIn0, P1 - DP1, T11, tmp.CalculatedEnthalpy)
                    Q1 = -StIn0.GetMassFlow() * (H11 - StIn0.GetMassEnthalpy())

                    Q = Math.Abs(Q1)

                    If Q > MaxHeatExchange Then

                        Q = MaxHeatExchange

                        H11 = H10 - (Math.Sign(Q1) * Q - HeatLoss) / StIn1.GetMassFlow()

                        StIn1.PropertyPackage.CurrentMaterialStream = StIn1
                        IObj?.SetCurrent()
                        tmp = StIn1.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, P1 - DP1, H11, T11)
                        T11 = OverriddenTemperature(StIn1, P1 - DP1, H11, tmp.CalculatedTemperature.GetValueOrDefault())

                    End If

                    H20 = StIn1.GetMassEnthalpy()
                    H21 = H20 + (Math.Sign(Q1) * Q - HeatLoss) / StIn1.GetMassFlow()

                    StIn1.PropertyPackage.CurrentMaterialStream = StIn1
                    IObj?.SetCurrent()
                    tmp = StIn1.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, P2 - DP2, H21, T21)
                    T21 = OverriddenTemperature(StIn1, P2 - DP2, H21, tmp.CalculatedTemperature.GetValueOrDefault())
                    'OutletVaporFraction2 = tmp.GetVaporPhaseMoleFraction()

                    If T10 > T20 Then
                        Tc1 = T20
                        Tc2 = T21
                        Th1 = T10
                        Th2 = T11
                        Ph2 = Ph1 - DP1
                        Pc2 = Pc1 - DP2
                        Hc2 = H21
                        Hh2 = H11
                    Else
                        Tc1 = T10
                        Tc2 = T11
                        Th1 = T20
                        Th2 = T21
                        Ph2 = Ph1 - DP2
                        Pc2 = Pc1 - DP1
                        Hc2 = H11
                        Hh2 = H21
                    End If

                    Select Case Me.FlowDir
                        Case FlowDirection.CoCurrent
                            LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                        Case FlowDirection.CounterCurrent
                            LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                    End Select

                    LMTD *= CorrectionFactorLMTD

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = Q / (A * LMTD) * 1000

                Case HeatExchangerCalcMode.OutletVaporFraction2

                    Dim Q1, H10, H11, H20, H21, VF0, T10, T11, T20, T21, DP1, DP2 As Double

                    If T10 > T20 Then
                        P1 = Ph1
                        P2 = Pc1
                        DP1 = HotSidePressureDrop
                        DP2 = ColdSidePressureDrop
                    Else
                        P2 = Ph1
                        P1 = Pc1
                        DP2 = HotSidePressureDrop
                        DP1 = ColdSidePressureDrop
                    End If

                    A = Area

                    VF0 = StIn1.GetPhase("Vapor").Properties.molarfraction.GetValueOrDefault()
                    T10 = StIn1.GetTemperature()
                    H10 = StIn1.GetMassEnthalpy()

                    StIn1.PropertyPackage.CurrentMaterialStream = StIn1
                    IObj?.SetCurrent()
                    Dim tmp = StIn1.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureVaporFraction, P1 - DP1, OutletVaporFraction2, T10)
                    T11 = tmp.CalculatedTemperature.GetValueOrDefault()
                    H11 = OverriddenEnthalpy(StIn1, P1 - DP1, T11, tmp.CalculatedEnthalpy)
                    Q1 = -StIn1.GetMassFlow() * (H11 - StIn1.GetMassEnthalpy())

                    Q = Math.Abs(Q1)

                    If Q > MaxHeatExchange Then

                        Q = MaxHeatExchange

                        H11 = H10 - (Math.Sign(Q1) * Q - HeatLoss) / StIn1.GetMassFlow()

                        StIn1.PropertyPackage.CurrentMaterialStream = StIn1
                        IObj?.SetCurrent()
                        tmp = StIn1.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, P1 - DP1, H11, T11)
                        T11 = OverriddenTemperature(StIn1, P1 - DP1, H11, tmp.CalculatedTemperature.GetValueOrDefault())

                    End If

                    T20 = StIn0.GetTemperature()
                    H20 = StIn0.GetMassEnthalpy()
                    H21 = H20 + (Math.Sign(Q1) * Q - HeatLoss) / StIn0.GetMassFlow()

                    StIn0.PropertyPackage.CurrentMaterialStream = StIn0
                    IObj?.SetCurrent()
                    tmp = StIn0.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, P2 - DP2, H21, T21)
                    T21 = OverriddenTemperature(StIn0, P2 - DP2, H21, tmp.CalculatedTemperature.GetValueOrDefault())
                    'OutletVaporFraction1 = tmp.GetVaporPhaseMoleFraction()

                    If T10 > T20 Then
                        Tc1 = T20
                        Tc2 = T21
                        Th1 = T10
                        Th2 = T11
                        Ph2 = Ph1 - DP1
                        Pc2 = Pc1 - DP2
                        Hc2 = H21
                        Hh2 = H11
                    Else
                        Tc1 = T10
                        Tc2 = T11
                        Th1 = T20
                        Th2 = T21
                        Ph2 = Ph1 - DP2
                        Pc2 = Pc1 - DP1
                        Hc2 = H11
                        Hh2 = H21
                    End If

                    Select Case Me.FlowDir
                        Case FlowDirection.CoCurrent
                            LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                        Case FlowDirection.CounterCurrent
                            LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                    End Select

                    LMTD *= CorrectionFactorLMTD

                    If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                    U = Q / (A * LMTD) * 1000

                Case HeatExchangerCalcMode.ShellandTube_Rating, HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor

                    'Shell and Tube HX calculation using Tinker's method.

                    IObj?.Paragraphs.Add("Shell and Tube HX calculation uses Tinker's method, more details <a href='http://essel.com.br/cursos/03_trocadores.htm'>on this link</a>, Chapter 5 (Capitulo 5).")

                    Dim Tc2_ant, Th2_ant As Double
                    Dim Ud, Ur, U_ant, Rf, fx, Fant, F As Double
                    Dim DTm, Tcm, Thm, R, Sf, P As Double

                    If CalculationMode = HeatExchangerCalcMode.ShellandTube_Rating Then

                        'initial estimates for R and P to calculate outlet temperatures

                        R = 0.4
                        P = 0.6

                        If STProperties.Tube_Fluid = 0 Then
                            'cold
                            Tc2 = P * (Th1 - Tc1) + Tc1
                            Th2 = Th1 - R * (Tc2 - Tc1)
                        Else
                            'hot
                            Th2 = P * (Tc1 - Th1) + Th1
                            Tc2 = Tc1 - R * (Th2 - Th1)
                        End If

                        If Th2 <= Tc2 Then Th2 = Tc2 * 1.2

                    Else

                        Tc2 = TempColdOut
                        Th2 = TempHotOut

                    End If

                    Pc2 = Pc1
                    Ph2 = Ph1
                    F = 1.0#
                    U = 500.0#

                    If CalculationMode = HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor Then

                        ' The duty and the outlet enthalpies follow from the outlet temperatures the
                        ' user specified. Neither was ever computed in this mode: Q kept whatever the
                        ' last run had left on the object, and the outlet enthalpies stayed at zero,
                        ' which the flash after the loop read back as a temperature. That is how a
                        ' specified outlet of 44 C came out at 1727 C.

                        StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                        IObj?.SetCurrent()
                        Dim tmpc = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, Pc2, Tc2, 0)
                        Hc2 = tmpc.CalculatedEnthalpy

                        StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                        IObj?.SetCurrent()
                        Dim tmph = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, Ph2, Th2, 0)
                        Hh2 = tmph.CalculatedEnthalpy

                        ' Same bookkeeping the rating branch does when it goes the other way.
                        If STProperties.Shell_Fluid = 0 Then
                            Q = Wc * (Hc2 - Hc1) + HeatLoss
                        Else
                            Q = Wc * (Hc2 - Hc1)
                        End If

                    End If

                    IObj?.Paragraphs.Add("<h3>Initial Estimates</h3>")

                    IObj?.Paragraphs.Add("<mi>T_{c,out}</mi> = " & Tc2 & " K")
                    IObj?.Paragraphs.Add("<mi>T_{h,out}</mi> = " & Th2 & " K")
                    IObj?.Paragraphs.Add("<mi>U</mi> = " & U & " W/[m2.K]")

                    Dim rhoc, muc, kc, rhoh, muh, kh, rs, rt, Nc, di, de, pitch, L, n, hi, nt, vt, Ret, Prt As Double

                    Dim icnt As Integer = 0

                    Do

                        IObj?.Paragraphs.Add("<h4>Convergence Loop #" & icnt & "</h4>")

                        Select Case Me.FlowDir
                            Case FlowDirection.CoCurrent
                                LMTD = ((Th1 - Tc1) - (Th2 - Tc2)) / Math.Log((Th1 - Tc1) / (Th2 - Tc2))
                            Case FlowDirection.CounterCurrent
                                LMTD = ((Th1 - Tc2) - (Th2 - Tc1)) / Math.Log((Th1 - Tc2) / (Th2 - Tc1))
                        End Select

                        LMTD *= CorrectionFactorLMTD

                        IObj?.Paragraphs.Add("<mi>\Delta T_{ml}</mi> = " & LMTD & " K")

                        If Not IgnoreLMTDError Then If Double.IsNaN(LMTD) Or Double.IsInfinity(LMTD) Then Throw New Exception(FlowSheet.GetTranslatedString("HXCalcError"))

                        If STProperties.Tube_Fluid = 0 Then
                            'cold
                            R = (Th1 - Th2) / (Tc2 - Tc1)
                            P = (Tc2 - Tc1) / (Th1 - Tc1)
                        Else
                            'hot
                            R = (Tc1 - Tc2) / (Th2 - Th1)
                            P = (Th2 - Th1) / (Tc1 - Th1)
                        End If

                        IObj?.Paragraphs.Add("<mi>R</mi> = " & R)
                        IObj?.Paragraphs.Add("<mi>P</mi> = " & P)

                        Fant = F

                        ' The correction factor is the one for N shell passes in series. Two shells
                        ' in series count as two, which is the reason for putting them in series
                        ' when the streams cross; only the passes inside one shell were counted
                        ' before, so the arrangement made no difference to F.
                        Dim Nsp As Double = Math.Max(1.0, Me.STProperties.Shell_NumberOfShellsInSeries *
                                                          Me.STProperties.Shell_NumberOfPasses)

                        If R <> 1.0# Then
                            Dim alpha As Double
                            alpha = ((1 - R * P) / (1 - P)) ^ (1 / Nsp)
                            Sf = (alpha - 1) / (alpha - R)
                            F = (R ^ 2 + 1) ^ 0.5 * Math.Log((1 - Sf) / (1 - R * Sf)) / ((R - 1) * Math.Log((2 - Sf * (R + 1 - (R ^ 2 + 1) ^ 0.5)) / (2 - Sf * (R + 1 + (R ^ 2 + 1) ^ 0.5))))
                        Else
                            Sf = P / (Nsp * (1 - P) + P)
                            F = Sf * 2 ^ 0.5 / ((1 - Sf) * Math.Log((2 * (1 - Sf) + Sf * 2 ^ 0.5) / (2 * (1 - Sf) - Sf * 2 ^ 0.5)))
                        End If
                        If Double.IsNaN(F) Then
                            F = Fant
                            'Throw New Exception("LMTD correction factor 'F'  could not be calculated. R = " & R & ", S = " & Sf)
                        End If
                        DTm = F * LMTD
                        '3
                        Tcm = (Tc2 - Tc1) / 2 + Tc1
                        Thm = (Th1 - Th2) / 2 + Th2


                        IObj?.Paragraphs.Add("<mi>F</mi> = " & F)
                        IObj?.Paragraphs.Add("<mi>\Delta T_m</mi> = " & DTm & " K")
                        IObj?.Paragraphs.Add("<mi>T_{c,m}</mi> = " & Tcm & " K")
                        IObj?.Paragraphs.Add("<mi>T_{h,m}</mi> = " & Thm & " K")

                        '4, 5

                        StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                        IObj?.SetCurrent()
                        Dim tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, Pc2, Tc2, 0)
                        Dim tms As MaterialStream = StInCold.Clone
                        tms.SetFlowsheet(StInCold.FlowSheet)
                        tms.Phases(0).Properties.temperature = Tcm
                        With tms.PropertyPackage
                            .CurrentMaterialStream = tms
                            IObj?.SetCurrent()
                            .DW_CalcEquilibrium(PropertyPackages.FlashSpec.T, PropertyPackages.FlashSpec.P)
                            If tms.Phases(3).Properties.molarfraction.GetValueOrDefault > 0 Then
                                IObj?.SetCurrent()
                                .DW_CalcPhaseProps(PropertyPackages.Phase.Liquid1)
                            Else
                                .DW_ZerarPhaseProps(PropertyPackages.Phase.Liquid1)
                            End If
                            If tms.Phases(2).Properties.molarfraction.GetValueOrDefault > 0 Then
                                IObj?.SetCurrent()
                                .DW_CalcPhaseProps(PropertyPackages.Phase.Vapor)
                            Else
                                .DW_ZerarPhaseProps(PropertyPackages.Phase.Vapor)
                            End If
                            If tms.Phases(2).Properties.molarfraction.GetValueOrDefault >= 0 And tms.Phases(2).Properties.molarfraction.GetValueOrDefault <= 1 Then
                                IObj?.SetCurrent()
                                .DW_CalcPhaseProps(PropertyPackages.Phase.Liquid)
                            Else
                                .DW_ZerarPhaseProps(PropertyPackages.Phase.Liquid)
                            End If
                            IObj?.SetCurrent()
                            tms.PropertyPackage.DW_CalcPhaseProps(PropertyPackages.Phase.Mixture)
                        End With
                        rhoc = tms.Phases(0).Properties.density.GetValueOrDefault
                        CPC = tms.Phases(0).Properties.heatCapacityCp.GetValueOrDefault
                        kc = tms.Phases(0).Properties.thermalConductivity.GetValueOrDefault
                        muc = tms.Phases(0).Properties.viscosity.GetValueOrDefault
                        tms = StInHot.Clone
                        tms.SetFlowsheet(StInHot.FlowSheet)
                        tms.Phases(0).Properties.temperature = Thm
                        tms.PropertyPackage.CurrentMaterialStream = tms
                        With tms.PropertyPackage
                            .CurrentMaterialStream = tms
                            IObj?.SetCurrent()
                            .DW_CalcEquilibrium(PropertyPackages.FlashSpec.T, PropertyPackages.FlashSpec.P)
                            If tms.Phases(3).Properties.molarfraction.GetValueOrDefault > 0 Then
                                IObj?.SetCurrent()
                                .DW_CalcPhaseProps(PropertyPackages.Phase.Liquid1)
                            Else
                                .DW_ZerarPhaseProps(PropertyPackages.Phase.Liquid1)
                            End If
                            If tms.Phases(2).Properties.molarfraction.GetValueOrDefault > 0 Then
                                IObj?.SetCurrent()
                                .DW_CalcPhaseProps(PropertyPackages.Phase.Vapor)
                            Else
                                .DW_ZerarPhaseProps(PropertyPackages.Phase.Vapor)
                            End If
                            If tms.Phases(2).Properties.molarfraction.GetValueOrDefault >= 0 And tms.Phases(2).Properties.molarfraction.GetValueOrDefault <= 1 Then
                                IObj?.SetCurrent()
                                .DW_CalcPhaseProps(PropertyPackages.Phase.Liquid)
                            Else
                                .DW_ZerarPhaseProps(PropertyPackages.Phase.Liquid)
                            End If
                            IObj?.SetCurrent()
                            tms.PropertyPackage.DW_CalcPhaseProps(PropertyPackages.Phase.Mixture)
                        End With
                        rhoh = tms.Phases(0).Properties.density.GetValueOrDefault
                        CPH = tms.Phases(0).Properties.heatCapacityCp.GetValueOrDefault
                        kh = tms.Phases(0).Properties.thermalConductivity.GetValueOrDefault
                        muh = tms.Phases(0).Properties.viscosity.GetValueOrDefault

                        '6

                        rs = Me.STProperties.Shell_Fouling
                        rt = Me.STProperties.Tube_Fouling
                        Nc = STProperties.Shell_NumberOfShellsInSeries
                        de = STProperties.Tube_De / 1000
                        di = STProperties.Tube_Di / 1000
                        L = STProperties.Tube_Length
                        pitch = STProperties.Tube_Pitch / 1000
                        n = STProperties.Tube_NumberPerShell
                        nt = n / STProperties.Tube_PassesPerShell
                        ' the tube count is per shell, so the shells in series multiply the area, the
                        ' same way the geometry report does it
                        A = n * Nc * Math.PI * de * (L - 2 * de)

                        If pitch < de Then Throw New Exception("Invalid input: tube spacing (pitch) is smaller than the tube's external diameter.")

                        If CalculationMode = HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor Then
                            Ud = Q * 1000 / (A * DTm)
                        End If
                        If STProperties.Tube_Fluid = 0 Then
                            'cold
                            vt = Wc / (rhoc * nt * Math.PI * di ^ 2 / 4)
                            Ret = rhoc * vt * di / muc
                            Prt = muc * CPC / kc * 1000
                        Else
                            'hot
                            vt = Wh / (rhoh * nt * Math.PI * di ^ 2 / 4)
                            Ret = rhoh * vt * di / muh
                            Prt = muh * CPH / kh * 1000
                        End If

                        IObj?.Paragraphs.Add("<mi>Re_{tube}</mi> = " & Ret)
                        IObj?.Paragraphs.Add("<mi>Pr_{tube}</mi> = " & Prt)

                        'calcular DeltaP

                        Dim dpt, dps As Double
                        'tube
                        dpt = 0.0#
                        Dim fric As Double = 0
                        Dim epsilon As Double = STProperties.Tube_Roughness / 1000
                        If Ret > 3250 Then
                            Dim a1 = Math.Log(((epsilon / di) ^ 1.1096) / 2.8257 + (7.149 / Ret) ^ 0.8961) / Math.Log(10.0#)
                            Dim b1 = -2 * Math.Log((epsilon / di) / 3.7065 - 5.0452 * a1 / Ret) / Math.Log(10.0#)
                            fric = (1 / b1) ^ 2
                        Else
                            fric = 64 / Ret
                        End If
                        Dim fric_dp As Double = fric * STProperties.Tube_Scaling_FricCorrFactor
                        If STProperties.Tube_Fluid = 0 Then
                            'cold
                            dpt = fric_dp * L * STProperties.Tube_PassesPerShell / di * vt ^ 2 / 2 * rhoc
                        Else
                            'hot
                            dpt = fric_dp * L * STProperties.Tube_PassesPerShell / di * vt ^ 2 / 2 * rhoh
                        End If

                        IObj?.Paragraphs.Add("<mi>\Delta P_{tube}</mi> = " & dpt & " Pa")

                        'tube heat transfer coeff (uses uncorrected friction factor)
                        If STProperties.Tube_Fluid = 0 Then
                            'cold
                            hi = kc / di * (fric / 8) * Ret * Prt / (1.07 + 12.7 * (fric / 8) ^ 0.5 * (Prt ^ (2 / 3) - 1))
                        Else
                            'hot
                            hi = kh / di * (fric / 8) * Ret * Prt / (1.07 + 12.7 * (fric / 8) ^ 0.5 * (Prt ^ (2 / 3) - 1))
                        End If

                        IObj?.Paragraphs.Add("<mi>h_{int,tube}</mi> = " & hi & " W/[m2.K]")

                        'shell internal diameter
                        Dim Dsi, Dsf, nsc, HDi, Nb As Double
                        Select Case STProperties.Tube_Layout
                            Case 0, 1
                                nsc = 1.1 * n ^ 0.5
                            Case 2, 3
                                nsc = 1.19 * n ^ 0.5
                        End Select
                        Dsf = (nsc - 1) * pitch + de
                        Dsi = STProperties.Shell_Di / 1000 'Dsf / 1.075

                        'Dsf = Dsi / 1.075 * Dsi
                        HDi = STProperties.Shell_BaffleCut / 100
                        Nb = Math.Max(Math.Floor(L / (STProperties.Shell_BaffleSpacing / 1000)) - 1, 1)

                        'shell pressure drop
                        Dim Gsf, Np, Fp, Ss, Ssf, fs, Cb, Ca, Res, Prs, jh, aa, bb, cc, xx, yy, Nh, Y As Double
                        xx = Dsi / (STProperties.Shell_BaffleSpacing / 1000)
                        yy = pitch / de
                        Select Case STProperties.Tube_Layout
                            Case 0, 1
                                aa = 0.9078565328950694
                                bb = 0.66331106126564476
                                cc = -4.4329764639656482
                                Nh = aa * xx ^ bb * yy ^ cc
                                aa = 5.3718559074820611
                                bb = -0.33416765138071414
                                cc = 0.7267144209289168
                                Y = aa * xx ^ bb * yy ^ cc
                                aa = 0.53807650470841084
                                bb = 0.3761125784751041
                                cc = -3.8741224386187474
                                Np = aa * xx ^ bb * yy ^ cc
                            Case 2
                                aa = 0.84134824361715088
                                bb = 0.61374520485097339
                                cc = -4.2696318466170409
                                Nh = aa * xx ^ bb * yy ^ cc
                                aa = 4.9901814007765743
                                bb = -0.32437442510328618
                                cc = 1.084850423269188
                                Y = aa * xx ^ bb * yy ^ cc
                                aa = 0.5502379008813062
                                bb = 0.36559560225434834
                                cc = -3.99041305625483
                                Np = aa * xx ^ bb * yy ^ cc
                            Case 3
                                aa = 0.66738654406767639
                                bb = 0.680260033886211
                                cc = -4.522291113086232
                                Nh = aa * xx ^ bb * yy ^ cc
                                aa = 4.5749169651729105
                                bb = -0.32201759442337358
                                cc = 1.17295183743691
                                Y = aa * xx ^ bb * yy ^ cc
                                aa = 0.36869631130961067
                                bb = 0.38397859475813922
                                cc = -3.6273465996780421
                                Np = aa * xx ^ bb * yy ^ cc
                        End Select
                        Fp = 1 / (0.8 + Np * (Dsi / pitch) ^ 0.5)
                        Select Case STProperties.Tube_Layout
                            Case 0, 1, 2
                                Cb = 0.97
                            Case 3
                                Cb = 1.37
                        End Select
                        Ca = Cb * (pitch - de) / pitch
                        Ss = Ca * STProperties.Shell_BaffleSpacing / 1000 * Dsf
                        Ssf = Ss / Fp
                        'Ssf = Math.PI / 4 * (Dsi ^ 2 - nt * de ^ 2)
                        If STProperties.Shell_Fluid = 0 Then
                            'cold
                            Gsf = Wc / Ssf
                            Res = Gsf * de / muc
                            Prs = muc * CPC / kc * 1000
                        Else
                            'hot
                            Gsf = Wh / Ssf
                            Res = Gsf * de / muh
                            Prs = muh * CPH / kh * 1000
                        End If

                        IObj?.Paragraphs.Add("<mi>Re_{shell}</mi> = " & Res)
                        IObj?.Paragraphs.Add("<mi>Pr_{shell}</mi> = " & Prs)

                        Select Case STProperties.Tube_Layout
                            Case 0, 1
                                If Res < 100 Then
                                    jh = 0.497 * Res ^ 0.54
                                Else
                                    jh = 0.378 * Res ^ 0.59
                                End If
                                If pitch / de <= 1.2 Then
                                    If Res < 100 Then
                                        fs = 276.46 * Res ^ -0.979
                                    ElseIf Res < 1000 Then
                                        fs = 30.26 * Res ^ -0.523
                                    Else
                                        fs = 2.93 * Res ^ -0.186
                                    End If
                                ElseIf pitch / de <= 1.3 Then
                                    If Res < 100 Then
                                        fs = 208.14 * Res ^ -0.945
                                    ElseIf Res < 1000 Then
                                        fs = 27.6 * Res ^ -0.525
                                    Else
                                        fs = 2.27 * Res ^ -0.163
                                    End If
                                ElseIf pitch / de <= 1.4 Then
                                    If Res < 100 Then
                                        fs = 122.73 * Res ^ -0.865
                                    ElseIf Res < 1000 Then
                                        fs = 17.82 * Res ^ -0.474
                                    Else
                                        fs = 1.86 * Res ^ -0.146
                                    End If
                                ElseIf pitch / de <= 1.5 Then
                                    If Res < 100 Then
                                        fs = 104.33 * Res ^ -0.869
                                    ElseIf Res < 1000 Then
                                        fs = 12.69 * Res ^ -0.434
                                    Else
                                        fs = 1.526 * Res ^ -0.129
                                    End If
                                Else
                                    Throw New Exception(String.Format("The ratio between tube spacing and tube external diameter needs to be less than or equal to 1.5 (current value: {0})", pitch / de))
                                End If
                            Case 2, 3
                                If Res < 100 Then
                                    If STProperties.Tube_Layout = 2 Then
                                        jh = 0.385 * Res ^ 0.526
                                    Else
                                        jh = 0.496 * Res ^ 0.54
                                    End If
                                Else
                                    If STProperties.Tube_Layout = 2 Then
                                        jh = 0.2487 * Res ^ 0.625
                                    Else
                                        jh = 0.354 * Res ^ 0.61
                                    End If
                                End If
                                If pitch / de <= 1.2 Then
                                    If Res < 100 Then
                                        fs = 230 * Res ^ -1
                                    ElseIf Res < 1000 Then
                                        fs = 16.23 * Res ^ -0.43
                                    Else
                                        fs = 2.67 * Res ^ -0.173
                                    End If
                                ElseIf pitch / de <= 1.3 Then
                                    If Res < 100 Then
                                        fs = 142.22 * Res ^ -0.949
                                    ElseIf Res < 1000 Then
                                        fs = 11.93 * Res ^ -0.43
                                    Else
                                        fs = 1.77 * Res ^ -0.144
                                    End If
                                ElseIf pitch / de <= 1.4 Then
                                    If Res < 100 Then
                                        fs = 110.77 * Res ^ -0.965
                                    ElseIf Res < 1000 Then
                                        fs = 7.524 * Res ^ -0.4
                                    Else
                                        fs = 1.01 * Res ^ -0.104
                                    End If
                                ElseIf pitch / de <= 1.5 Then
                                    If Res < 100 Then
                                        fs = 58.18 * Res ^ -0.862
                                    ElseIf Res < 1000 Then
                                        fs = 6.76 * Res ^ -0.411
                                    Else
                                        fs = 0.718 * Res ^ -0.008
                                    End If
                                Else
                                    Throw New Exception(String.Format("The ratio between tube spacing and tube external diameter needs to be less than or equal to 1.5 (current value: {0})", pitch / de))
                                End If
                        End Select

                        'Cx
                        Dim Cx As Double = 0
                        Select Case STProperties.Tube_Layout
                            Case 0, 1
                                Cx = 1.154
                            Case 2
                                Cx = 1.0#
                            Case 3
                                Cx = 1.414
                        End Select
                        Dim Gsh, Ssh, Fh, Rsh, dis As Double
                        If STProperties.Shell_Fluid = 0 Then
                            dps = 4 * fs * Gsf ^ 2 / (2 * rhoc) * Cx * (1 - HDi) * Dsi / pitch * Nb * (1 + Y * pitch / Dsi)
                        Else
                            dps = 4 * fs * Gsf ^ 2 / (2 * rhoh) * Cx * (1 - HDi) * Dsi / pitch * Nb * (1 + Y * pitch / Dsi)
                        End If
                        dps *= Nc

                        IObj?.Paragraphs.Add("<mi>\Delta P_{shell}</mi> = " & dps & " Pa")

                        'shell htc

                        Dim M As Double = 0.96#
                        dis = STProperties.Shell_Di / 1000
                        Fh = 1 / (1 + Nh * (dis / pitch) ^ 0.5)
                        Ssh = Ss * M / Fh
                        'Ssh = Math.PI / 4 * (Dsi ^ 2 - nt * de ^ 2)
                        If STProperties.Shell_Fluid = 0 Then
                            Gsh = Wc / Ssh
                            Rsh = Gsh * de / muc
                        Else
                            Gsh = Wh / Ssh
                            Rsh = Gsh * de / muh
                        End If
                        Dim Ec, lb, he As Double
                        Select Case STProperties.Tube_Layout
                            Case 0, 1
                                If Rsh < 100 Then
                                    jh = 0.497 * Rsh ^ 0.54
                                Else
                                    jh = 0.378 * Rsh ^ 0.61
                                End If
                            Case 2, 3
                                If Rsh < 100 Then
                                    jh = 0.385 * Rsh ^ 0.526
                                Else
                                    jh = 0.2487 * Rsh ^ 0.625
                                End If
                        End Select
                        If STProperties.Shell_Fluid = 0 Then
                            he = jh * kc * Prs ^ 0.34 / de
                        Else
                            he = jh * kh * Prs ^ 0.34 / de
                        End If
                        Dim Bs As Double = STProperties.Shell_BaffleSpacing / 1000
                        lb = Bs * (Nb - 1)
                        If L - lb > 0 Then
                            Ec = (lb + (L - lb) * (2 * Bs / (L - lb)) ^ 0.6) / L
                        Else
                            Ec = 1.0
                        End If
                        If Double.IsNaN(Ec) OrElse Ec <= 0 OrElse Ec > 1 Then Ec = 1.0
                        he *= Ec

                        IObj?.Paragraphs.Add("<mi>h_{ext,shell}</mi> = " & he & " W/[m2.K]")

                        'global HTC (U)
                        Dim kt As Double = STProperties.Tube_ThermalConductivity
                        Dim f1, f2, f3, f4, f5 As Double
                        f1 = de / (hi * di)
                        f2 = rt * de / di
                        f3 = de / (2 * kt) * Math.Log(de / di)
                        f4 = rs
                        f5 = 1 / he
                        If CalculationMode = HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor Then
                            Ur = f1 + f3 + f5
                            Ur = 1 / Ur
                            Rf = 1 / Ud - 1 / Ur
                            STProperties.OverallFoulingFactor = Rf
                            U_ant = U
                            U = 1 / Ur + Rf
                            U = 1 / U
                        Else
                            U_ant = U
                            U = f1 + f2 + f3 + f4 + f5
                            STProperties.OverallFoulingFactor = f2 + f4
                            U = 1 / U
                            Q = U * A * F * LMTD / 1000
                            If Q > MaxHeatExchange Then
                                Q = MaxHeatExchange
                                F = Q * 1000 / (U * A * LMTD)
                            End If
                            If STProperties.Shell_Fluid = 0 Then
                                'cold
                                DeltaHc = (Q - HeatLoss) / Wc
                                DeltaHh = -Q / Wh
                            Else
                                'hot
                                DeltaHc = Q / Wc
                                DeltaHh = -(Q + HeatLoss) / Wh
                            End If
                            Hc2 = Hc1 + DeltaHc
                            Hh2 = Hh1 + DeltaHh
                            StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                            IObj?.SetCurrent()
                            tmp = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Pc2, Hc2, Tc2)
                            Tc2_ant = Tc2
                            Tc2 = OverriddenTemperature(StInCold, Pc2, Hc2, tmp.CalculatedTemperature)
                            Tc2 = 0.1 * Tc2 + 0.9 * Tc2_ant
                            StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                            IObj?.SetCurrent()
                            tmp = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, Th2)
                            Th2_ant = Th2
                            Th2 = OverriddenTemperature(StInHot, Ph2, Hh2, tmp.CalculatedTemperature)
                            Th2 = 0.1 * Th2 + 0.9 * Th2_ant
                        End If

                        IObj?.Paragraphs.Add("<mi>Q</mi> = " & Q & " kW")
                        IObj?.Paragraphs.Add("<mi>U</mi> = " & U & " W/[m2.K]")

                        IObj?.Paragraphs.Add("<mi>T_{c,out}</mi> = " & Tc2 & " K")
                        IObj?.Paragraphs.Add("<mi>T_{h,out}</mi> = " & Th2 & " K")

                        STProperties.Ft = f1 'tube side
                        STProperties.Fc = f3 'heat conductivity pipe
                        STProperties.Fs = f5 'shell side
                        STProperties.Ff = STProperties.OverallFoulingFactor
                        STProperties.ReS = Res 'Reynolds number shell side
                        STProperties.ReT = Ret 'Reynolds number tube side

                        Dim rhoShellFluid As Double = If(STProperties.Shell_Fluid = 0, rhoc, rhoh)
                        Dim rhoTubeFluid As Double = If(STProperties.Tube_Fluid = 0, rhoc, rhoh)
                        Dim WShellFlow As Double = If(STProperties.Shell_Fluid = 0, Wc, Wh)
                        Dim WTubeFlow As Double = If(STProperties.Tube_Fluid = 0, Wc, Wh)
                        Dim PShell As Double = If(STProperties.Shell_Fluid = 0, Pc1, Ph1)
                        Dim PTube As Double = If(STProperties.Tube_Fluid = 0, Pc1, Ph1)
                        Dim TShell As Double = If(STProperties.Shell_Fluid = 0, Tc1, Th1)
                        Dim TTube As Double = If(STProperties.Tube_Fluid = 0, Tc1, Th1)
                        STProperties.CalcDetailedResults(Ssf, vt, Gsf, rhoShellFluid, rhoTubeFluid,
                            WShellFlow, rhoShellFluid, rhoShellFluid, WTubeFlow, rhoTubeFluid, rhoTubeFluid,
                            PShell, TShell, PTube, TTube)

                        If STProperties.Shell_Fluid = 0 Then
                            Pc2 = Pc1 - dps
                            Ph2 = Ph1 - dpt
                        Else
                            Pc2 = Pc1 - dpt
                            Ph2 = Ph1 - dps
                        End If
                        Me.LMTD_F = F
                        If CalculationMode = HeatExchangerCalcMode.ShellandTube_Rating Then
                            fx = Math.Abs((Th2 - Th2_ant) ^ 2 + (Tc2 - Tc2_ant) ^ 2)
                            IObj?.Paragraphs.Add("Temperature error = " & fx)
                        Else
                            fx = Math.Abs((U - U_ant)) ^ 2
                            IObj?.Paragraphs.Add("Overall HTC error = " & fx)
                        End If

                        FlowSheet.CheckStatus()
                        icnt += 1
                        If icnt > 100 Then
                            Throw New Exception("Calculation did not converge in 100 iteratons.")
                        End If
                    Loop Until fx < 0.001

                    StInCold.PropertyPackage.CurrentMaterialStream = StInCold
                    IObj?.SetCurrent()
                    Dim tmp2 = StInCold.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Pc2, Hc2, 0.0)
                    Tc2 = tmp2.CalculatedTemperature
                    StInHot.PropertyPackage.CurrentMaterialStream = StInHot
                    IObj?.SetCurrent()
                    tmp2 = StInHot.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureEnthalpy, Ph2, Hh2, 0.0)
                    Th2 = tmp2.CalculatedTemperature

            End Select

            CheckSpec(Tc2, True, "cold stream outlet temperature")
            CheckSpec(Th2, True, "hot stream outlet temperature")
            CheckSpec(Ph2, True, "hot stream outlet pressure")
            CheckSpec(Pc2, True, "cold stream outlet pressure")

            If CalcMode <> HeatExchangerCalcMode.PinchPoint And CalculateHeatExchangeProfile Then

                Dim dhc, dhh As Double

                Dim tcprof, thprof, dtprof, qprof As New List(Of Double)

                tcprof.Clear()
                thprof.Clear()
                qprof.Clear()

                ' The profile follows the duty the exchanger actually transfers. It used to sweep up
                ' to MaxHeatExchange, the thermodynamic limit, which draws the profile of an
                ' exchanger with infinite area and puts the hot outlet at the cold inlet
                ' temperature - so the minimum approach below came out as zero on every exchanger.
                Dim Qduty As Double = Q
                If Double.IsNaN(Qduty) OrElse Qduty <= 0.0 Then Qduty = MaxHeatExchange

                For j = 0 To 10

                    Dim dqx = j / 10.0 * Qduty

                    dhc = dqx / Wc
                    dhh = dqx / Wh

                    'calculate profiles

                    tmpstr = StInCold.Clone
                    tmpstr.PropertyPackage = StInCold.PropertyPackage
                    tmpstr.SetFlowsheet(StInCold.FlowSheet)

                    tmpstr.Phases(0).Properties.enthalpy = Hc1 + dhc
                    tmpstr.Phases(0).Properties.pressure = Pc1 - Convert.ToDouble(j) / 10.0 * ColdSidePressureDrop
                    tmpstr.SpecType = StreamSpec.Pressure_and_Enthalpy
                    IObj?.SetCurrent()
                    tmpstr.Calculate()

                    qprof.Add(dqx)
                    tcprof.Add(tmpstr.Phases(0).Properties.temperature.GetValueOrDefault)

                    tmpstr = StInHot.Clone
                    tmpstr.PropertyPackage = StInHot.PropertyPackage
                    tmpstr.SetFlowsheet(StInHot.FlowSheet)

                    tmpstr.Phases(0).Properties.enthalpy = Hh1 - dhh
                    tmpstr.Phases(0).Properties.pressure = Ph1 - Convert.ToDouble(j) / 10.0 * HotSidePressureDrop
                    tmpstr.SpecType = StreamSpec.Pressure_and_Enthalpy
                    IObj?.SetCurrent()
                    tmpstr.Calculate()

                    thprof.Add(tmpstr.Phases(0).Properties.temperature.GetValueOrDefault)

                Next

                Me.HeatProfile = qprof.ToArray
                Me.TemperatureProfileCold = tcprof.ToArray
                Me.TemperatureProfileHot = thprof.ToArray

                If Not PinchPointAtOutlets And FlowDir = FlowDirection.CounterCurrent Then
                    thprof.Reverse()
                End If

                ' Signed on purpose: where the hot stream runs colder than the cold stream the
                ' approach is negative and the arrangement is infeasible. Taking the absolute value
                ' turned a temperature cross into a small positive number that read as a tight but
                ' workable design.
                For i As Integer = 0 To 10
                    dtprof.Add(thprof(i) - tcprof(i))
                Next

                CalculatedMITA = dtprof.Min

            End If

            IObj?.Paragraphs.Add("<h2>Results</h2>")

            IObj?.Paragraphs.Add("<mi>T_{c,out}</mi> = " & Tc2 & " K")
            IObj?.Paragraphs.Add("<mi>T_{h,out}</mi> = " & Th2 & " K")
            IObj?.Paragraphs.Add("<mi>P_{c,out}</mi> = " & Pc2 & " Pa")
            IObj?.Paragraphs.Add("<mi>P_{h,out}</mi> = " & Ph2 & " Pa")

            IObj?.Paragraphs.Add("<mi>Q</mi> = " & Q & " kW")

            IObj?.Paragraphs.Add("<mi>U</mi> = " & U & " W/[m2.K]")

            IObj?.Paragraphs.Add("<mi>\Delta T_{ml}</mi> = " & LMTD & " K")

            If CalcMode <> HeatExchangerCalcMode.ThermalEfficiency Then ThermalEfficiency = (Q - HeatLoss) / MaxHeatExchange * 100

            If HeatLoss > Math.Abs(Q.GetValueOrDefault) Then Throw New Exception("Invalid Heat Loss.")

            IObj?.Paragraphs.Add("<mi>Q/Q_{max}</mi> = " & ThermalEfficiency & " %")

            If Not DebugMode Then

                Me.ColdSideOutletTemperature = Tc2
                Me.HotSideOutletTemperature = Th2
                Me.ColdSidePressureDrop = Pc1 - Pc2
                Me.HotSidePressureDrop = Ph1 - Ph2
                Me.OverallCoefficient = U
                Me.Area = A

                'Define new calculated properties.
                StOutHot.Phases(0).Properties.temperature = Th2
                StOutCold.Phases(0).Properties.temperature = Tc2
                StOutHot.Phases(0).Properties.pressure = Ph2
                StOutCold.Phases(0).Properties.pressure = Pc2
                StOutHot.Phases(0).Properties.enthalpy = Hh2
                StOutCold.Phases(0).Properties.enthalpy = Hc2

                ' With a property override in place the enthalpy the exchanger works in and the one
                ' the outlet stream reports are two different scales, because the override rewrites
                ' the value after the flash. Handing the outlet an enthalpy would make it solve the
                ' correlations again and land on a temperature that does not match the duty, and the
                ' two sides of the exchanger would not balance. The temperature is the one state
                ' both scales agree on, so it is what the outlet is specified with.
                StOutHot.SetFlashSpec(If(HasPropertyOverrides(StOutHot), "PT", "PH"))
                StOutCold.SetFlashSpec(If(HasPropertyOverrides(StOutCold), "PT", "PH"))

                StOutCold.AtEquilibrium = False
                StOutHot.AtEquilibrium = False
                StOutHot.DefinedFlow = FlowSpec.Mass
                StOutCold.DefinedFlow = FlowSpec.Mass

                If CalculationMode <> HeatExchangerCalcMode.OutletVaporFraction1 And CalculationMode <> HeatExchangerCalcMode.OutletVaporFraction2 Then
                    If Th2 < Tc1 Or Tc2 > Th1 Then
                        FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": Temperature Cross", IFlowsheet.MessageType.Warning)
                    End If
                End If

            Else

                AppendDebugLine("Calculation finished successfully.")

            End If

            IObj?.Close()

        End Sub

        ''' <summary>Clears all calculated results.</summary>
        Public Overrides Sub DeCalculate()

            If Me.GraphicObject.OutputConnectors(0).IsAttached Then

                'Zerar valores da corrente de materia conectada a jusante
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name), MaterialStream).Clear()

            End If

            If Me.GraphicObject.OutputConnectors(1).IsAttached Then

                'Zerar valores da corrente de materia conectada a jusante
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name), MaterialStream).Clear()

            End If

        End Sub

        ''' <summary>Returns the default set of properties shown in the flowsheet inspector.</summary>
        Public Overrides Function GetDefaultProperties() As String()
            Return New String() {"PROP_HX_0", "PROP_HX_1", "PROP_HX_2", "PROP_HX_3", "PROP_HX_4", "PROP_HX_25", "PROP_HX_26", "PROP_HX_27", "PROP_HX_28", "PROP_HX_32", "PROP_HX_33"}
        End Function

        ''' <summary>Returns the value of the specified property.</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else


                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As Double = 0
                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Select Case propidx

                    Case 0
                        'PROP_HX_0	Global Heat Transfer Coefficient (U)
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heat_transf_coeff, Me.OverallCoefficient.GetValueOrDefault)
                    Case 1
                        'PROP_HX_1	Heat Exchange Area (A)
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.Area.GetValueOrDefault)
                    Case 2
                        'PROP_HX_2	Heat Load
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.Q.GetValueOrDefault)
                    Case 3
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.TempColdOut)
                    Case 4
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.TempHotOut)
                    Case 5
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Shell_Di / 1000)
                    Case 6
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Shell_Fouling)
                    Case 7
                        value = Me.STProperties.Shell_BaffleCut
                    Case 8
                        value = Me.STProperties.Shell_NumberOfShellsInSeries
                    Case 9
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.thickness, Me.STProperties.Shell_BaffleSpacing / 1000)
                    Case 10
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Tube_Di / 1000)
                    Case 11
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Tube_De / 1000)
                    Case 12
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.distance, Me.STProperties.Tube_Length)
                    Case 13
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Tube_Fouling)
                    Case 14
                        value = Me.STProperties.Tube_PassesPerShell
                    Case 15
                        value = Me.STProperties.Tube_NumberPerShell
                    Case 16
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.thickness, Me.STProperties.Tube_Pitch / 1000)
                    Case 17
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.OverallFoulingFactor)
                    Case 18
                        value = Me.LMTD_F
                    Case 19
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.LMTD)
                    Case 20
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Ft)
                    Case 21
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Fc)
                    Case 22
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Fs)
                    Case 23
                        value = SystemsOfUnits.Converter.ConvertFromSI("", Me.STProperties.ReS)
                    Case 24
                        value = SystemsOfUnits.Converter.ConvertFromSI("", Me.STProperties.ReT)
                    Case 25
                        value = ThermalEfficiency
                    Case 26
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, MaxHeatExchange)
                    Case 27
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, MITA)
                    Case 28
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, HeatLoss)
                    Case 29
                        value = CorrectionFactorLMTD
                    Case 30
                        value = OutletVaporFraction1
                    Case 21
                        value = OutletVaporFraction2
                    Case 32
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, ColdSidePressureDrop)
                    Case 33
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, HotSidePressureDrop)
                    Case 34
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, CalculatedMITA)
                    Case 35
                        value = CInt(Me.STProperties.TEMA_FrontHeadType)
                    Case 36
                        value = CInt(Me.STProperties.TEMA_RearHeadType)
                    Case 37
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.STProperties.DesignPressure_Shell)
                    Case 38
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.STProperties.DesignPressure_Tube)
                    Case 39
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.STProperties.DesignTemperature_Shell)
                    Case 40
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.STProperties.DesignTemperature_Tube)
                    Case 41
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.STProperties.TestPressure_Shell)
                    Case 42
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.STProperties.TestPressure_Tube)
                    Case 43
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.thickness, Me.STProperties.CorrosionAllowance / 1000)
                    Case 44
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.thickness, Me.STProperties.Shell_WallThickness / 1000)
                    Case 45
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Nozzle_ShellInlet_Di / 1000)
                    Case 46
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Nozzle_ShellOutlet_Di / 1000)
                    Case 47
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Nozzle_TubeInlet_Di / 1000)
                    Case 48
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Nozzle_TubeOutlet_Di / 1000)
                    Case 49
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.diameter, Me.STProperties.Result_TubeBundleDiameter / 1000)
                    Case 50
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.distance, Me.STProperties.Result_EffectiveTubeLength)
                    Case 51
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.STProperties.Result_HeatTransferArea_Internal)
                    Case 52
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.STProperties.Result_HeatTransferArea_External)
                    Case 53
                        value = Me.STProperties.Result_NumberOfBaffles
                    Case 54
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.STProperties.Result_TubeSideFlowArea)
                    Case 55
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.STProperties.Result_ShellSideFlowArea)
                    Case 56
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.velocity, Me.STProperties.Result_TubeSideVelocity)
                    Case 57
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.velocity, Me.STProperties.Result_ShellSideVelocity)
                    Case 58
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.velocity, Me.STProperties.Result_Nozzle_ShellInletVelocity)
                    Case 59
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.velocity, Me.STProperties.Result_Nozzle_ShellOutletVelocity)
                    Case 60
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.velocity, Me.STProperties.Result_Nozzle_TubeInletVelocity)
                    Case 61
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.velocity, Me.STProperties.Result_Nozzle_TubeOutletVelocity)
                    Case 62
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.STProperties.Result_Nozzle_ShellInlet_RhoV2)
                    Case 63
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.STProperties.Result_Nozzle_ShellOutlet_RhoV2)
                    Case 64
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.STProperties.Result_Nozzle_TubeInlet_RhoV2)
                    Case 65
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.STProperties.Result_Nozzle_TubeOutlet_RhoV2)
                    Case 66
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.volume, Me.STProperties.Result_ShellSideVolume)
                    Case 67
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.volume, Me.STProperties.Result_TubeSideVolume)
                    Case 68
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.mass, Me.STProperties.Result_Weight_Empty)
                    Case 69
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.mass, Me.STProperties.Result_Weight_Operating)
                    Case 70
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.mass, Me.STProperties.Result_Weight_WetTest)
                End Select

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
                Case PropertyType.RO
                    For i = 2 To 4
                        proplist.Add("PROP_HX_" + CStr(i))
                    Next
                    proplist.Add("PROP_HX_25")
                    proplist.Add("PROP_HX_26")
                    For i = 17 To 24
                        proplist.Add("PROP_HX_" + CStr(i))
                    Next
                    proplist.Add("PROP_HX_34")
                    For i = 49 To 70
                        proplist.Add("PROP_HX_" + CStr(i))
                    Next
                Case PropertyType.RW
                    For i = 0 To 70
                        proplist.Add("PROP_HX_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 16
                        proplist.Add("PROP_HX_" + CStr(i))
                    Next
                    proplist.Add("PROP_HX_27")
                    proplist.Add("PROP_HX_28")
                    proplist.Add("PROP_HX_29")
                    proplist.Add("PROP_HX_30")
                    proplist.Add("PROP_HX_31")
                    proplist.Add("PROP_HX_32")
                    proplist.Add("PROP_HX_33")
                    For i = 35 To 48
                        proplist.Add("PROP_HX_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To 70
                        proplist.Add("PROP_HX_" + CStr(i))
                    Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>Sets the value of the specified property.</summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx

                Case 0
                    'PROP_HX_0	Global Heat Transfer Coefficient (U)
                    Me.OverallCoefficient = SystemsOfUnits.Converter.ConvertToSI(su.heat_transf_coeff, propval)
                Case 1
                    'PROP_HX_1	Heat Exchange Area (A)
                    Me.Area = SystemsOfUnits.Converter.ConvertToSI(su.area, propval)
                Case 2
                    'PROP_HX_1	Heat Load (Q)
                    Me.Q = SystemsOfUnits.Converter.ConvertToSI(su.heatflow, propval)
                Case 3
                    'PROP_HX_3	Cold Fluid Outlet Temperature
                    Me.TempColdOut = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval)
                Case 4
                    'PROP_HX_4	Hot Fluid Outlet Temperature
                    Me.TempHotOut = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval)
                Case 5
                    Me.STProperties.Shell_Di = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval) * 1000
                Case 6
                    Me.STProperties.Shell_Fouling = SystemsOfUnits.Converter.ConvertToSI(su.foulingfactor, propval)
                Case 7
                    Me.STProperties.Shell_BaffleCut = propval
                Case 8
                    Me.STProperties.Shell_NumberOfShellsInSeries = propval
                Case 9
                    Me.STProperties.Shell_BaffleSpacing = SystemsOfUnits.Converter.ConvertToSI(su.thickness, propval) * 1000
                Case 10
                    Me.STProperties.Tube_Di = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval) * 1000
                Case 11
                    Me.STProperties.Tube_De = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval) * 1000
                Case 12
                    Me.STProperties.Tube_Length = SystemsOfUnits.Converter.ConvertToSI(su.distance, propval)
                Case 13
                    Me.STProperties.Tube_Fouling = SystemsOfUnits.Converter.ConvertToSI(su.foulingfactor, propval)
                Case 14
                    Me.STProperties.Tube_PassesPerShell = propval
                Case 15
                    Me.STProperties.Tube_NumberPerShell = propval
                Case 16
                    Me.STProperties.Tube_Pitch = SystemsOfUnits.Converter.ConvertToSI(su.thickness, propval) * 1000
                Case 27
                    Me.MITA = SystemsOfUnits.Converter.ConvertToSI(su.deltaT, propval)
                Case 28
                    Me.HeatLoss = SystemsOfUnits.Converter.ConvertToSI(su.heatflow, propval)
                Case 29
                    CorrectionFactorLMTD = propval
                Case 30
                    OutletVaporFraction1 = propval
                Case 31
                    OutletVaporFraction2 = propval
                Case 32
                    ColdSidePressureDrop = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)
                Case 33
                    HotSidePressureDrop = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)
                Case 35
                    Me.STProperties.TEMA_FrontHeadType = CType(CInt(propval), TEMAFrontHeadType)
                Case 36
                    Me.STProperties.TEMA_RearHeadType = CType(CInt(propval), TEMARearHeadType)
                Case 37
                    Me.STProperties.DesignPressure_Shell = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
                Case 38
                    Me.STProperties.DesignPressure_Tube = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
                Case 39
                    Me.STProperties.DesignTemperature_Shell = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval)
                Case 40
                    Me.STProperties.DesignTemperature_Tube = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval)
                Case 41
                    Me.STProperties.TestPressure_Shell = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
                Case 42
                    Me.STProperties.TestPressure_Tube = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
                Case 43
                    Me.STProperties.CorrosionAllowance = SystemsOfUnits.Converter.ConvertToSI(su.thickness, propval) * 1000
                Case 44
                    Me.STProperties.Shell_WallThickness = SystemsOfUnits.Converter.ConvertToSI(su.thickness, propval) * 1000
                Case 45
                    Me.STProperties.Nozzle_ShellInlet_Di = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval) * 1000
                Case 46
                    Me.STProperties.Nozzle_ShellOutlet_Di = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval) * 1000
                Case 47
                    Me.STProperties.Nozzle_TubeInlet_Di = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval) * 1000
                Case 48
                    Me.STProperties.Nozzle_TubeOutlet_Di = SystemsOfUnits.Converter.ConvertToSI(su.diameter, propval) * 1000
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
                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Select Case propidx

                    Case 0
                        'PROP_HX_0	Global Heat Transfer Coefficient (U)
                        value = su.heat_transf_coeff
                    Case 1
                        'PROP_HX_1	Heat Exchange Area (A)
                        value = su.area
                    Case 2, 28
                        'PROP_HX_2	Heat Load
                        value = su.heatflow
                    Case 3
                        'PROP_HX_3	
                        value = su.temperature
                    Case 4
                        'PROP_HX_4
                        value = su.temperature
                    Case 5
                        value = su.diameter
                    Case 6
                        value = su.foulingfactor
                    Case 7
                        value = "%"
                    Case 8
                        value = ""
                    Case 9
                        value = su.thickness
                    Case 10
                        value = su.diameter
                    Case 11
                        value = su.diameter
                    Case 12
                        value = su.distance
                    Case 13
                        value = su.foulingfactor
                    Case 14
                        value = ""
                    Case 15
                        value = ""
                    Case 16
                        value = su.thickness
                    Case 17
                        value = su.foulingfactor
                    Case 18
                        value = ""
                    Case 19, 27, 34
                        value = su.deltaT
                    Case 20, 21, 22
                        value = su.foulingfactor
                    Case 23, 24
                        value = ""
                    Case 25
                        value = "%"
                    Case 26
                        value = su.heatflow
                    Case 32, 33
                        value = su.deltaP
                    Case 35, 36
                        value = ""
                    Case 37, 38, 41, 42
                        value = su.pressure
                    Case 39, 40
                        value = su.temperature
                    Case 43, 44
                        value = su.thickness
                    Case 45, 46, 47, 48, 49
                        value = su.diameter
                    Case 50
                        value = su.distance
                    Case 51, 52, 54, 55
                        value = su.area
                    Case 53
                        value = ""
                    Case 56, 57, 58, 59, 60, 61
                        value = su.velocity
                    Case 62, 63, 64, 65
                        value = su.deltaP
                    Case 66, 67
                        value = su.volume
                    Case 68, 69, 70
                        value = su.mass
                End Select

                Return value
            End If
        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.heat_exchanger.png")

        End Function

        ''' <summary>Returns the localised display description.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("HEXCH_Desc")
        End Function

        ''' <summary>Returns the localised display name.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("HEXCH_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property
        ''' <summary>Generates a plain-text report of the heat-exchanger results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder

            Dim istr, istr2 As MaterialStream
            istr = Me.GetInletMaterialStream(0)
            istr2 = Me.GetInletMaterialStream(1)

            str.AppendLine("Heat Exchanger: " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Inlet conditions (stream 1)")
            str.AppendLine()
            istr.PropertyPackage.CurrentMaterialStream = istr
            str.AppendLine("    Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, istr.Phases(0).Properties.temperature.GetValueOrDefault).ToString(numberformat, ci) & " " & su.temperature)
            str.AppendLine("    Pressure: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, istr.Phases(0).Properties.pressure.GetValueOrDefault).ToString(numberformat, ci) & " " & su.pressure)
            str.AppendLine("    Mass flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.massflow, istr.Phases(0).Properties.massflow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.massflow)
            str.AppendLine("    Volumetric flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.volumetricFlow, istr.Phases(0).Properties.volumetric_flow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.volumetricFlow)
            str.AppendLine("    Vapor fraction: " & istr.Phases(2).Properties.molarfraction.GetValueOrDefault.ToString(numberformat, ci))
            str.AppendLine("    Compounds: " & istr.PropertyPackage.RET_VNAMES.ToArrayString)
            str.AppendLine("    Molar composition: " & istr.PropertyPackage.RET_VMOL(PropertyPackages.Phase.Mixture).ToArrayString(ci))
            str.AppendLine()
            str.AppendLine("Inlet conditions (stream 2)")
            str.AppendLine()
            istr2.PropertyPackage.CurrentMaterialStream = istr2
            str.AppendLine("    Temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, istr2.Phases(0).Properties.temperature.GetValueOrDefault).ToString(numberformat, ci) & " " & su.temperature)
            str.AppendLine("    Pressure: " & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, istr2.Phases(0).Properties.pressure.GetValueOrDefault).ToString(numberformat, ci) & " " & su.pressure)
            str.AppendLine("    Mass flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.massflow, istr2.Phases(0).Properties.massflow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.massflow)
            str.AppendLine("    Volumetric flow: " & SystemsOfUnits.Converter.ConvertFromSI(su.volumetricFlow, istr2.Phases(0).Properties.volumetric_flow.GetValueOrDefault).ToString(numberformat, ci) & " " & su.volumetricFlow)
            str.AppendLine("    Vapor fraction: " & istr2.Phases(2).Properties.molarfraction.GetValueOrDefault.ToString(numberformat, ci))
            str.AppendLine("    Compounds: " & istr2.PropertyPackage.RET_VNAMES.ToArrayString)
            str.AppendLine("    Molar composition: " & istr2.PropertyPackage.RET_VMOL(PropertyPackages.Phase.Mixture).ToArrayString(ci))
            str.AppendLine()
            str.AppendLine("Calculation parameters")
            str.AppendLine()
            str.AppendLine("    Exchanger type: " & Me.FlowDir.ToString)
            Select Case Me.CalculationMode
                Case HeatExchangerCalcMode.CalcTempColdOut
                    str.AppendLine("    Hot fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.HotSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Exchange area: " & SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.Area).ToString(numberformat, ci) & " " & su.area)
                Case HeatExchangerCalcMode.CalcTempHotOut
                    str.AppendLine("    Cold fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.ColdSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Exchange area: " & SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.Area).ToString(numberformat, ci) & " " & su.area)
                Case HeatExchangerCalcMode.CalcBothTemp
                    str.AppendLine("    Heat exchanged: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.Q).ToString(numberformat, ci) & " " & su.heatflow)
                    str.AppendLine("    Exchange area: " & SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.Area).ToString(numberformat, ci) & " " & su.area)
                Case HeatExchangerCalcMode.CalcArea
                    str.AppendLine("    Overall heat transfer coefficient: " & SystemsOfUnits.Converter.ConvertFromSI(su.heat_transf_coeff, Me.OverallCoefficient).ToString(numberformat, ci) & " " & su.heat_transf_coeff)
                    If Me.DefinedTemperature = SpecifiedTemperature.Cold_Fluid Then
                        str.AppendLine("    Cold fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.ColdSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    Else
                        str.AppendLine("    Hot fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.HotSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    End If
                Case HeatExchangerCalcMode.OutletVaporFraction1
                    str.AppendLine("    Outlet Vapor Fraction 1: " & OutletVaporFraction1.ToString(numberformat))
                Case HeatExchangerCalcMode.OutletVaporFraction2
                    str.AppendLine("    Outlet Vapor Fraction 2: " & OutletVaporFraction2.ToString(numberformat))
            End Select
            str.AppendLine("    Hot fluid pressure drop: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.HotSidePressureDrop).ToString(numberformat, ci) & " " & su.deltaP)
            str.AppendLine("    Cold fluid pressure drop: " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.ColdSidePressureDrop).ToString(numberformat, ci) & " " & su.deltaP)
            str.AppendLine("    Heat loss: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.HeatLoss).ToString(numberformat, ci) & " " & su.heatflow)
            str.AppendLine()
            str.AppendLine("Results")
            str.AppendLine()
            Select Case Me.CalculationMode
                Case HeatExchangerCalcMode.CalcTempColdOut
                    str.AppendLine("    Cold fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.ColdSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Overall heat transfer coefficient: " & SystemsOfUnits.Converter.ConvertFromSI(su.heat_transf_coeff, Me.OverallCoefficient).ToString(numberformat, ci) & " " & su.heat_transf_coeff)
                    str.AppendLine("    Heat exchanged: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.Q).ToString(numberformat, ci) & " " & su.heatflow)
                Case HeatExchangerCalcMode.CalcTempHotOut
                    str.AppendLine("    Hot fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.HotSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Overall heat transfer coefficient: " & SystemsOfUnits.Converter.ConvertFromSI(su.heat_transf_coeff, Me.OverallCoefficient).ToString(numberformat, ci) & " " & su.heat_transf_coeff)
                    str.AppendLine("    Heat exchanged: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.Q).ToString(numberformat, ci) & " " & su.heatflow)
                Case HeatExchangerCalcMode.CalcBothTemp
                    str.AppendLine("    Cold fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.ColdSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Hot fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.HotSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Overall heat transfer coefficient: " & SystemsOfUnits.Converter.ConvertFromSI(su.heat_transf_coeff, Me.OverallCoefficient).ToString(numberformat, ci) & " " & su.heat_transf_coeff)
                Case HeatExchangerCalcMode.CalcArea
                    str.AppendLine("    Heat exchanged: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.Q).ToString(numberformat, ci) & " " & su.heatflow)
                    str.AppendLine("    Exchange area: " & SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.Area).ToString(numberformat, ci) & " " & su.area)
                Case HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor, HeatExchangerCalcMode.ShellandTube_Rating
                    str.AppendLine("    Re Shell: " & STProperties.ReS.ToString(numberformat, ci))
                    str.AppendLine("    Re Tube: " & STProperties.ReT.ToString(numberformat, ci))
                    str.AppendLine("    F Shell: " & SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Fs).ToString(numberformat, ci) & " " & su.foulingfactor)
                    str.AppendLine("    F Tube: " & SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Ft).ToString(numberformat, ci) & " " & su.foulingfactor)
                    str.AppendLine("    F Pipe: " & SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Fc).ToString(numberformat, ci) & " " & su.foulingfactor)
                    str.AppendLine("    F Fouling: " & SystemsOfUnits.Converter.ConvertFromSI(su.foulingfactor, Me.STProperties.Ff).ToString(numberformat, ci) & " " & su.foulingfactor)
                    str.AppendLine("    Heat exchanged: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.Q).ToString(numberformat, ci) & " " & su.heatflow)
                    str.AppendLine("    Exchange area: " & SystemsOfUnits.Converter.ConvertFromSI(su.area, Me.Area).ToString(numberformat, ci) & " " & su.area)
                    str.AppendLine("    Cold fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.ColdSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Hot fluid outlet temperature: " & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.HotSideOutletTemperature).ToString(numberformat, ci) & " " & su.temperature)
                    str.AppendLine("    Overall heat transfer coefficient: " & SystemsOfUnits.Converter.ConvertFromSI(su.heat_transf_coeff, Me.OverallCoefficient).ToString(numberformat, ci) & " " & su.heat_transf_coeff)
                    str.AppendLine()
                    str.AppendLine("    Shell & Tube Detailed Results")
                    str.AppendLine()
                    str.AppendLine("    TEMA Designation: " & STProperties.GetTEMADesignation(CType(Me.HXType, HeatExchangerType)))
                    str.AppendLine("    Tube Bundle Diameter: " & SystemsOfUnits.Converter.ConvertFromSI(su.diameter, STProperties.Result_TubeBundleDiameter / 1000).ToString(numberformat, ci) & " " & su.diameter)
                    str.AppendLine("    Effective Tube Length: " & SystemsOfUnits.Converter.ConvertFromSI(su.distance, STProperties.Result_EffectiveTubeLength).ToString(numberformat, ci) & " " & su.distance)
                    str.AppendLine("    Heat Transfer Area (External): " & SystemsOfUnits.Converter.ConvertFromSI(su.area, STProperties.Result_HeatTransferArea_External).ToString(numberformat, ci) & " " & su.area)
                    str.AppendLine("    Heat Transfer Area (Internal): " & SystemsOfUnits.Converter.ConvertFromSI(su.area, STProperties.Result_HeatTransferArea_Internal).ToString(numberformat, ci) & " " & su.area)
                    str.AppendLine("    Number of Baffles: " & STProperties.Result_NumberOfBaffles)
                    str.AppendLine("    Tube-Side Velocity: " & SystemsOfUnits.Converter.ConvertFromSI(su.velocity, STProperties.Result_TubeSideVelocity).ToString(numberformat, ci) & " " & su.velocity)
                    str.AppendLine("    Shell-Side Velocity: " & SystemsOfUnits.Converter.ConvertFromSI(su.velocity, STProperties.Result_ShellSideVelocity).ToString(numberformat, ci) & " " & su.velocity)
                    str.AppendLine("    Nozzle Shell Inlet: " & SystemsOfUnits.Converter.ConvertFromSI(su.diameter, STProperties.Result_Nozzle_ShellInlet_Di / 1000).ToString(numberformat, ci) & " " & su.diameter & " / " & SystemsOfUnits.Converter.ConvertFromSI(su.velocity, STProperties.Result_Nozzle_ShellInletVelocity).ToString(numberformat, ci) & " " & su.velocity & " / rho*v2 = " & STProperties.Result_Nozzle_ShellInlet_RhoV2.ToString(numberformat, ci) & " Pa")
                    str.AppendLine("    Nozzle Shell Outlet: " & SystemsOfUnits.Converter.ConvertFromSI(su.diameter, STProperties.Result_Nozzle_ShellOutlet_Di / 1000).ToString(numberformat, ci) & " " & su.diameter & " / " & SystemsOfUnits.Converter.ConvertFromSI(su.velocity, STProperties.Result_Nozzle_ShellOutletVelocity).ToString(numberformat, ci) & " " & su.velocity & " / rho*v2 = " & STProperties.Result_Nozzle_ShellOutlet_RhoV2.ToString(numberformat, ci) & " Pa")
                    str.AppendLine("    Nozzle Tube Inlet: " & SystemsOfUnits.Converter.ConvertFromSI(su.diameter, STProperties.Result_Nozzle_TubeInlet_Di / 1000).ToString(numberformat, ci) & " " & su.diameter & " / " & SystemsOfUnits.Converter.ConvertFromSI(su.velocity, STProperties.Result_Nozzle_TubeInletVelocity).ToString(numberformat, ci) & " " & su.velocity & " / rho*v2 = " & STProperties.Result_Nozzle_TubeInlet_RhoV2.ToString(numberformat, ci) & " Pa")
                    str.AppendLine("    Nozzle Tube Outlet: " & SystemsOfUnits.Converter.ConvertFromSI(su.diameter, STProperties.Result_Nozzle_TubeOutlet_Di / 1000).ToString(numberformat, ci) & " " & su.diameter & " / " & SystemsOfUnits.Converter.ConvertFromSI(su.velocity, STProperties.Result_Nozzle_TubeOutletVelocity).ToString(numberformat, ci) & " " & su.velocity & " / rho*v2 = " & STProperties.Result_Nozzle_TubeOutlet_RhoV2.ToString(numberformat, ci) & " Pa")
                    str.AppendLine("    Shell-Side Volume: " & SystemsOfUnits.Converter.ConvertFromSI(su.volume, STProperties.Result_ShellSideVolume).ToString(numberformat, ci) & " " & su.volume)
                    str.AppendLine("    Tube-Side Volume: " & SystemsOfUnits.Converter.ConvertFromSI(su.volume, STProperties.Result_TubeSideVolume).ToString(numberformat, ci) & " " & su.volume)
                    str.AppendLine("    Weight (Empty): " & SystemsOfUnits.Converter.ConvertFromSI(su.mass, STProperties.Result_Weight_Empty).ToString(numberformat, ci) & " " & su.mass)
                    str.AppendLine("    Weight (Operating): " & SystemsOfUnits.Converter.ConvertFromSI(su.mass, STProperties.Result_Weight_Operating).ToString(numberformat, ci) & " " & su.mass)
                    str.AppendLine("    Weight (Wet Test): " & SystemsOfUnits.Converter.ConvertFromSI(su.mass, STProperties.Result_Weight_WetTest).ToString(numberformat, ci) & " " & su.mass)
            End Select
            str.AppendLine("    Log mean temperature difference (LMTD): " & SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.LMTD).ToString(numberformat, ci) & " " & su.deltaT)
            str.AppendLine("    Maximum Heat Exchange: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.MaxHeatExchange).ToString(numberformat, ci) & " " & su.heatflow)
            str.AppendLine("    Thermal Efficiency (%): " & ThermalEfficiency.ToString(numberformat, ci))

            Return str.ToString

        End Function

        ''' <summary>Returns a structured (table-based) report of the heat-exchanger results.</summary>
        Public Overrides Function GetStructuredReport() As List(Of Tuple(Of ReportItemType, String()))

            Dim su As IUnitsOfMeasure = GetFlowsheet().FlowsheetOptions.SelectedUnitSystem
            Dim nf = GetFlowsheet().FlowsheetOptions.NumberFormat

            Dim list As New List(Of Tuple(Of ReportItemType, String()))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Results Report for Heat Exchanger '" & Me.GraphicObject.Tag + "'"}))
            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.SingleColumn, New String() {"Calculated successfully on " & LastUpdated.ToString}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Calculation Parameters"}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.DoubleColumn,
                    New String() {"Exchanger Mode",
                    FlowDir.ToString}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.DoubleColumn,
                    New String() {"Calculation Mode",
                    CalcMode.ToString}))

            Select Case Me.CalculationMode
                Case HeatExchangerCalcMode.CalcTempColdOut
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Hot Fluid Outlet Temperature",
                            HotSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                            su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Exchange Area",
                            Area.GetValueOrDefault.ConvertFromSI(su.area).ToString(nf),
                            su.area}))
                Case HeatExchangerCalcMode.CalcTempHotOut
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Cold Fluid Outlet Temperature",
                           ColdSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                           su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Exchange Area",
                            Area.GetValueOrDefault.ConvertFromSI(su.area).ToString(nf),
                            su.area}))
                Case HeatExchangerCalcMode.CalcBothTemp
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Heat Exchanged",
                           Q.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Exchange Area",
                            Area.GetValueOrDefault.ConvertFromSI(su.area).ToString(nf),
                            su.area}))
                Case HeatExchangerCalcMode.CalcArea
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Overall Heat Transfer Coefficient",
                            OverallCoefficient.GetValueOrDefault.ConvertFromSI(su.heat_transf_coeff).ToString(nf),
                            su.heat_transf_coeff}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Hot Fluid Outlet Temperature",
                            HotSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                            su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Cold Fluid Outlet Temperature",
                           ColdSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                           su.temperature}))
                Case HeatExchangerCalcMode.CalcBothTemp_UA
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Heat Exchanged",
                           Q.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Exchange Area",
                            Area.GetValueOrDefault.ConvertFromSI(su.area).ToString(nf),
                            su.area}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Overall Heat Transfer Coefficient",
                           OverallCoefficient.GetValueOrDefault.ConvertFromSI(su.heat_transf_coeff).ToString(nf),
                           su.heat_transf_coeff}))
                Case HeatExchangerCalcMode.OutletVaporFraction1
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Outlet Vapor Fraction 1",
                           OutletVaporFraction1.ToString(nf),
                           ""}))
                Case HeatExchangerCalcMode.OutletVaporFraction2
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Outlet Vapor Fraction 2",
                           OutletVaporFraction2.ToString(nf),
                           ""}))
            End Select

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Heat Loss",
                           HeatLoss.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.Label, New String() {"Results"}))

            Select Case Me.CalculationMode
                Case HeatExchangerCalcMode.CalcTempColdOut
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Hot Fluid Pressure Drop",
                           HotSidePressureDrop.ConvertFromSI(su.deltaP).ToString(nf),
                           su.deltaP}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Cold Fluid Pressure Drop",
                           ColdSidePressureDrop.ConvertFromSI(su.deltaP).ToString(nf),
                           su.deltaP}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Cold Fluid Outlet Temperature",
                           ColdSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                           su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Overall Heat Transfer Coefficient",
                           OverallCoefficient.GetValueOrDefault.ConvertFromSI(su.heat_transf_coeff).ToString(nf),
                           su.heat_transf_coeff}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Heat Exchanged",
                           Q.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))
                Case HeatExchangerCalcMode.CalcTempHotOut
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Hot Fluid Pressure Drop",
                           HotSidePressureDrop.ConvertFromSI(su.deltaP).ToString(nf),
                           su.deltaP}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Cold Fluid Pressure Drop",
                           ColdSidePressureDrop.ConvertFromSI(su.deltaP).ToString(nf),
                           su.deltaP}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Hot Fluid Outlet Temperature",
                            HotSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                            su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Overall Heat Transfer Coefficient",
                           OverallCoefficient.GetValueOrDefault.ConvertFromSI(su.heat_transf_coeff).ToString(nf),
                           su.heat_transf_coeff}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Heat Exchanged",
                           Q.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))
                Case HeatExchangerCalcMode.CalcBothTemp
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Hot Fluid Outlet Temperature",
                           HotSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                           su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Cold Fluid Pressure Drop",
                           ColdSidePressureDrop.ConvertFromSI(su.deltaP).ToString(nf),
                           su.deltaP}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Overall Heat Transfer Coefficient",
                           OverallCoefficient.GetValueOrDefault.ConvertFromSI(su.heat_transf_coeff).ToString(nf),
                           su.heat_transf_coeff}))
                Case HeatExchangerCalcMode.CalcArea
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Heat Exchanged",
                           Q.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Exchange Area",
                            Area.GetValueOrDefault.ConvertFromSI(su.area).ToString(nf),
                            su.area}))
                Case HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor, HeatExchangerCalcMode.ShellandTube_Rating
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Reynolds Number (Shell)",
                           STProperties.ReS.ToString(nf),
                           ""}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Reynolds Number (Tube)",
                           STProperties.ReT.ToString(nf),
                           ""}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"F (Shell)",
                           STProperties.Fs.ToString(nf),
                           ""}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"F (Tube)",
                           STProperties.Ft.ToString(nf),
                           ""}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"F (Pipe)",
                           STProperties.Fc.ToString(nf),
                           ""}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"F (Fouling)",
                           STProperties.Ff.ToString(nf),
                           ""}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Heat Exchanged",
                           Q.GetValueOrDefault.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Exchange Area",
                            Area.GetValueOrDefault.ConvertFromSI(su.area).ToString(nf),
                            su.area}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Cold Fluid Pressure Drop",
                           ColdSidePressureDrop.ConvertFromSI(su.deltaP).ToString(nf),
                           su.deltaP}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Hot Fluid Outlet Temperature",
                            HotSideOutletTemperature.ConvertFromSI(su.temperature).ToString(nf),
                            su.temperature}))
                    list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Overall Heat Transfer Coefficient",
                           OverallCoefficient.GetValueOrDefault.ConvertFromSI(su.heat_transf_coeff).ToString(nf),
                           su.heat_transf_coeff}))
            End Select

            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                            New String() {"Log Mean Temperature Difference (LMTD)",
                            LMTD.ConvertFromSI(su.deltaT).ToString(nf),
                            su.deltaT}))
            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Maximum Possible Heat Exchange",
                           MaxHeatExchange.ConvertFromSI(su.heatflow).ToString(nf),
                           su.heatflow}))
            list.Add(New Tuple(Of ReportItemType, String())(ReportItemType.TripleColumn,
                           New String() {"Thermal Efficiency (%)",
                           ThermalEfficiency.ToString(nf),
                           "%"}))

            Return list

        End Function

        ''' <summary>Returns a human-readable description of the specified property.</summary>
        Public Overrides Function GetPropertyDescription(p As String) As String
            If p.Equals("Calculation Mode") Then
                Return "Select the Heat Exchanger calculation mode."
            ElseIf p.Equals("Flow Direction") Then
                Return "Select the flow direction of the inlet streams."
            ElseIf p.Equals("Defined Temperature (for Calc Area Mode)") Then
                Return "Select which temperature you will define if you chose the 'Area' calculation mode."
            ElseIf p.Equals("Pressure Drop (Hot Fluid)") Then
                Return "Enter the pressure drop of the hot fluid. Required for all calculation modes except Shell and Tube Design/Rating."
            ElseIf p.Equals("Pressure Drop (Cold Fluid)") Then
                Return "Enter the pressure drop of the cold fluid. Required for all calculation modes except Shell and Tube Design/Rating."
            ElseIf p.Equals("Outlet Temperature (Cold Fluid)") Then
                Return "Enter the outlet temperature of the cold fluid, if required by the selected calculation mode."
            ElseIf p.Equals("Outlet Temperature (Hot Fluid)") Then
                Return "Enter the outlet temperature of the hot fluid, if required by the selected calculation mode."
            ElseIf p.Equals("Overall HTC") Then
                Return "Enter the overall Heat Exchange Coefficient, if required by the selected calculation mode."
            ElseIf p.Equals("Heat Exchange Area") Then
                Return "Enter the Heat Exchange Area, if required by the selected calculation mode."
            ElseIf p.Equals("Heat Exchanged") Then
                Return "Enter the Heat Exchanged, if required by the selected calculation mode."
            ElseIf p.Equals("MITA") Then
                Return "Enter the Mimimum Internal Temperature Approach (MITA) (for Pinch Point calculation mode only)."
            ElseIf p.Equals("Ignore LMTD Error") Then
                Return "If checked, continues solving even if the calculated LMTD is invalid."
            ElseIf p.Equals("Heat Loss") Then
                Return "Enter the total Heat Loss on this exchanger."
            Else
                Return p
            End If
        End Function
    End Class

End Namespace

''' <summary>
''' Contains auxiliary data and configuration classes for the heat exchanger unit operation,
''' including shell-and-tube and double-pipe geometry definitions.
''' </summary>
Namespace UnitOperations.Auxiliary.HeatExchanger

    <System.Serializable()> Public Class STHXProperties

        Implements Interfaces.ICustomXMLSerialization

        'number of shells in series, integer
        Public Shell_NumberOfShellsInSeries As Integer = 1
        'number of shell passes, integer
        Public Shell_NumberOfPasses As Integer = 2
        'shell internal diameter in mm
        Public Shell_Di As Double = 500.0
        'shell fouling in K.m2/W
        Public Shell_Fouling As Double = 0.0#
        'baffle type: 0 = single, 1 = double, 2 = triple, 3 = grid
        Public Shell_BaffleType As Integer = 0
        'baffle orientation: 0 = horizontal, 1 = vertical
        Public Shell_BaffleOrientation As Integer = 1
        'baffle cut in % diameter
        Public Shell_BaffleCut As Double = 20
        'baffle spacing in mm
        Public Shell_BaffleSpacing As Double = 250.0
        'fluid in shell: 0 = cold, 1 = hot
        Public Shell_Fluid As Integer = 1
        'tube internal diameter in mm
        Public Tube_Di As Double = 50.0
        'tube external diameter in mm
        Public Tube_De As Double = 60.0
        'tube length in m
        Public Tube_Length As Double = 5.0
        'tube fouling in K.m2/W
        Public Tube_Fouling As Double = 0.0#
        'number of tube passes per shell, integer
        Public Tube_PassesPerShell As Integer = 2
        'number of tubes per shell, integer
        Public Tube_NumberPerShell As Integer = 50
        'tube layout: 0 = triangular, 1 = triangular rotated, 2 = square, 2 = square rotated
        Public Tube_Layout As Integer = 0
        'tube pitch in mm
        Public Tube_Pitch As Double = 70.0
        'fluid in tubes: 0 = cold, 1 = hot
        Public Tube_Fluid As Integer = 0
        'tube material roughness in mm
        Public Tube_Roughness As Double = 0.000045 * 1000
        'shell material roughness in mm
        Public Shell_Roughness As Double = 0.000045 * 1000
        'tube scaling friction factor correction
        Public Tube_Scaling_FricCorrFactor As Double = 1.2#
        'tube thermal conductivity
        Public Tube_ThermalConductivity As Double = 70.0#
        'overall fouling factor, used only in design mode (as a calculation result)
        Public OverallFoulingFactor = 0.0#
        'partial heat exchange resistances (tube, conduction, shell, fouling), only as calculation result
        Public Ft, Fc, Fs, Ff As Double
        'Reynold numbers, only as calculation results
        Public ReT, ReS As Double

        ' TEMA designation
        Public TEMA_FrontHeadType As TEMAFrontHeadType = TEMAFrontHeadType.B
        Public TEMA_RearHeadType As TEMARearHeadType = TEMARearHeadType.M

        ' Nozzle inner diameters in mm (0 = auto-size)
        Public Nozzle_ShellInlet_Di As Double = 0.0
        Public Nozzle_ShellOutlet_Di As Double = 0.0
        Public Nozzle_TubeInlet_Di As Double = 0.0
        Public Nozzle_TubeOutlet_Di As Double = 0.0

        ' Design / datasheet
        Public DesignPressure_Shell As Double = 1000000.0
        Public DesignPressure_Tube As Double = 1000000.0
        Public DesignTemperature_Shell As Double = 473.15
        Public DesignTemperature_Tube As Double = 473.15
        Public TestPressure_Shell As Double = 1500000.0
        Public TestPressure_Tube As Double = 1500000.0
        Public CorrosionAllowance As Double = 3.0
        Public Shell_WallThickness As Double = 10.0
        Public TubeMaterial As String = "Carbon Steel (SA-516 Gr.70)"
        Public ShellMaterial As String = "Carbon Steel (SA-516 Gr.70)"
        Public TubesheetJointType As TubeToTubesheetJointType = TubeToTubesheetJointType.Expanded

        ' Calculated results - geometry
        Public Result_TubeBundleDiameter As Double = 0.0
        Public Result_EffectiveTubeLength As Double = 0.0
        Public Result_HeatTransferArea_Internal As Double = 0.0
        Public Result_HeatTransferArea_External As Double = 0.0
        Public Result_NumberOfBaffles As Integer = 0

        ' Calculated results - flow areas and velocities
        Public Result_TubeSideFlowArea As Double = 0.0
        Public Result_ShellSideFlowArea As Double = 0.0
        Public Result_TubeSideVelocity As Double = 0.0
        Public Result_ShellSideVelocity As Double = 0.0

        ' Calculated results - nozzles
        Public Result_Nozzle_ShellInlet_Di As Double = 0.0
        Public Result_Nozzle_ShellOutlet_Di As Double = 0.0
        Public Result_Nozzle_TubeInlet_Di As Double = 0.0
        Public Result_Nozzle_TubeOutlet_Di As Double = 0.0
        Public Result_Nozzle_ShellInletVelocity As Double = 0.0
        Public Result_Nozzle_ShellOutletVelocity As Double = 0.0
        Public Result_Nozzle_TubeInletVelocity As Double = 0.0
        Public Result_Nozzle_TubeOutletVelocity As Double = 0.0
        Public Result_Nozzle_ShellInlet_RhoV2 As Double = 0.0
        Public Result_Nozzle_ShellOutlet_RhoV2 As Double = 0.0
        Public Result_Nozzle_TubeInlet_RhoV2 As Double = 0.0
        Public Result_Nozzle_TubeOutlet_RhoV2 As Double = 0.0

        ' Calculated results - volumes and weights
        Public Result_ShellSideVolume As Double = 0.0
        Public Result_TubeSideVolume As Double = 0.0
        Public Result_Weight_Empty As Double = 0.0
        Public Result_Weight_Operating As Double = 0.0
        Public Result_Weight_WetTest As Double = 0.0

        Public Function GetTEMADesignation(shellType As HeatExchangerType) As String
            Dim front As String = TEMA_FrontHeadType.ToString()
            Dim shell As String
            Select Case shellType
                Case HeatExchangerType.ShellTubes_E : shell = "E"
                Case HeatExchangerType.ShellTubes_F : shell = "F"
                Case HeatExchangerType.ShellTubes_G : shell = "G"
                Case HeatExchangerType.ShellTubes_H : shell = "H"
                Case HeatExchangerType.ShellTubes_J : shell = "J"
                Case HeatExchangerType.ShellTubes_K : shell = "K"
                Case HeatExchangerType.ShellTubes_X : shell = "X"
                Case Else : shell = "E"
            End Select
            Dim rear As String = TEMA_RearHeadType.ToString()
            Return front & shell & rear
        End Function

        Private Shared ReadOnly NPS_IDs As Double() = {
            52.5, 77.9, 102.3, 154.1, 202.7, 254.5,
            303.2, 333.3, 381.0, 428.7, 477.8, 574.6
        }

        Public Shared Function SnapToNPS(diMm As Double) As Double
            For Each nps In NPS_IDs
                If nps >= diMm Then Return nps
            Next
            Return NPS_IDs(NPS_IDs.Length - 1)
        End Function

        Public Sub CalcNozzles(massFlowShell As Double, rhoShellIn As Double, rhoShellOut As Double,
                               massFlowTube As Double, rhoTubeIn As Double, rhoTubeOut As Double)
            Dim vMaxLiquid As Double = 2.0
            Dim vMaxGas As Double = 25.0
            Dim rhoV2Limit As Double = 6000.0

            CalcSingleNozzle(Nozzle_ShellInlet_Di, massFlowShell, rhoShellIn, vMaxLiquid, vMaxGas, rhoV2Limit,
                             Result_Nozzle_ShellInlet_Di, Result_Nozzle_ShellInletVelocity, Result_Nozzle_ShellInlet_RhoV2)
            CalcSingleNozzle(Nozzle_ShellOutlet_Di, massFlowShell, rhoShellOut, vMaxLiquid, vMaxGas, rhoV2Limit,
                             Result_Nozzle_ShellOutlet_Di, Result_Nozzle_ShellOutletVelocity, Result_Nozzle_ShellOutlet_RhoV2)
            CalcSingleNozzle(Nozzle_TubeInlet_Di, massFlowTube, rhoTubeIn, vMaxLiquid, vMaxGas, rhoV2Limit,
                             Result_Nozzle_TubeInlet_Di, Result_Nozzle_TubeInletVelocity, Result_Nozzle_TubeInlet_RhoV2)
            CalcSingleNozzle(Nozzle_TubeOutlet_Di, massFlowTube, rhoTubeOut, vMaxLiquid, vMaxGas, rhoV2Limit,
                             Result_Nozzle_TubeOutlet_Di, Result_Nozzle_TubeOutletVelocity, Result_Nozzle_TubeOutlet_RhoV2)
        End Sub

        Private Sub CalcSingleNozzle(inputDi As Double, massFlow As Double, rho As Double,
                                     vMaxLiq As Double, vMaxGas As Double, rhoV2Limit As Double,
                                     ByRef resultDi As Double, ByRef resultVel As Double, ByRef resultRhoV2 As Double)
            If rho <= 0 OrElse massFlow <= 0 Then
                resultDi = If(inputDi > 0, inputDi, 0)
                resultVel = 0
                resultRhoV2 = 0
                Return
            End If

            Dim vMax As Double = If(rho > 100, vMaxLiq, vMaxGas)
            Dim qVol As Double = massFlow / rho

            If inputDi <= 0 Then
                Dim diMin As Double = Math.Sqrt(4.0 * qVol / (Math.PI * vMax)) * 1000.0
                Dim diFromRhoV2 As Double = Math.Sqrt(4.0 * massFlow * massFlow / (rho * rhoV2Limit * Math.PI)) / Math.PI * 4.0
                diFromRhoV2 = Math.Sqrt(4.0 * massFlow / (Math.Sqrt(rhoV2Limit / rho) * Math.PI * rho)) * 1000.0
                diMin = Math.Max(diMin, diFromRhoV2)
                resultDi = SnapToNPS(diMin)
            Else
                resultDi = inputDi
            End If

            Dim areaM2 As Double = Math.PI * (resultDi / 1000.0) ^ 2 / 4.0
            If areaM2 > 0 Then
                resultVel = qVol / areaM2
                resultRhoV2 = rho * resultVel * resultVel
            Else
                resultVel = 0
                resultRhoV2 = 0
            End If
        End Sub

        Public Sub CalcDetailedResults(shellSideFlowArea As Double, tubeSideVelocity As Double,
                                       shellSideMassFlux As Double, rhoShell As Double,
                                       rhoTube As Double, massFlowShell As Double,
                                       rhoShellIn As Double, rhoShellOut As Double,
                                       massFlowTube As Double, rhoTubeIn As Double,
                                       rhoTubeOut As Double,
                                       Optional shellInletP As Double = 0, Optional shellInletT As Double = 0,
                                       Optional tubeInletP As Double = 0, Optional tubeInletT As Double = 0)
            Dim de_m As Double = Tube_De / 1000.0
            Dim di_m As Double = Tube_Di / 1000.0
            Dim Dsi_m As Double = Shell_Di / 1000.0
            Dim n As Integer = Tube_NumberPerShell * Shell_NumberOfShellsInSeries

            Result_TubeBundleDiameter = EstimateShellDiameter(0) - 12.0
            Result_EffectiveTubeLength = Tube_Length - 2 * de_m
            If Result_EffectiveTubeLength < 0 Then Result_EffectiveTubeLength = Tube_Length

            Result_HeatTransferArea_External = n * Math.PI * de_m * Result_EffectiveTubeLength
            Result_HeatTransferArea_Internal = n * Math.PI * di_m * Result_EffectiveTubeLength

            If Shell_BaffleSpacing > 0 Then
                Result_NumberOfBaffles = Math.Max(CInt(Math.Floor(Tube_Length / (Shell_BaffleSpacing / 1000.0))) - 1, 1)
            End If

            Dim nt_pass As Double = CDbl(Tube_NumberPerShell) / CDbl(Tube_PassesPerShell)
            Result_TubeSideFlowArea = nt_pass * Math.PI * di_m ^ 2 / 4.0
            Result_ShellSideFlowArea = shellSideFlowArea

            Result_TubeSideVelocity = tubeSideVelocity
            If rhoShell > 0 Then Result_ShellSideVelocity = shellSideMassFlux / rhoShell

            Result_TubeSideVolume = n * Math.PI * di_m ^ 2 / 4.0 * Tube_Length
            Result_ShellSideVolume = Math.PI * Dsi_m ^ 2 / 4.0 * Tube_Length - n * Math.PI * de_m ^ 2 / 4.0 * Tube_Length
            If Result_ShellSideVolume < 0 Then Result_ShellSideVolume = 0

            CalcNozzles(massFlowShell, rhoShellIn, rhoShellOut, massFlowTube, rhoTubeIn, rhoTubeOut)

            If shellInletP > 0 AndAlso tubeInletP > 0 Then
                DesignPressure_Shell = shellInletP * 1.1
                DesignPressure_Tube = tubeInletP * 1.1
                DesignTemperature_Shell = shellInletT + 25.0
                DesignTemperature_Tube = tubeInletT + 25.0
                TestPressure_Shell = DesignPressure_Shell * 1.5
                TestPressure_Tube = DesignPressure_Tube * 1.5
            End If

            Dim swt As Double = Shell_WallThickness / 1000.0
            Dim Vshell_wall As Double = Math.PI * ((Dsi_m + 2 * swt) ^ 2 - Dsi_m ^ 2) / 4.0 * Tube_Length
            Dim Vtubes_metal As Double = n * Math.PI * (de_m ^ 2 - di_m ^ 2) / 4.0 * Tube_Length
            Dim Vtubesheet As Double = 2.0 * Math.PI / 4.0 * Dsi_m ^ 2 * 0.05

            Dim matDb = HeatExchangerMaterial.GetMaterialDatabase()
            Dim shellMat = matDb.FirstOrDefault(Function(m) ShellMaterial.Contains(m.Name))
            Dim tubeMat = matDb.FirstOrDefault(Function(m) TubeMaterial.Contains(m.Name))
            Dim rhoShellMat As Double = If(shellMat IsNot Nothing, shellMat.Density, 7850.0)
            Dim rhoTubeMat As Double = If(tubeMat IsNot Nothing, tubeMat.Density, 7850.0)

            Result_Weight_Empty = Vshell_wall * rhoShellMat + Vtubes_metal * rhoTubeMat + Vtubesheet * rhoShellMat
            Result_Weight_Operating = Result_Weight_Empty + Result_TubeSideVolume * rhoTube + Result_ShellSideVolume * rhoShell
            Result_Weight_WetTest = Result_Weight_Empty + (Result_TubeSideVolume + Result_ShellSideVolume) * 998.0
        End Sub

        ''' <summary>
        ''' Estimates the minimum shell internal diameter based on tube bundle geometry
        ''' using the Coulson and Richardson empirical correlation.
        ''' </summary>
        ''' <param name="rearHeadType">
        ''' Type of rear head for bundle-to-shell clearance:
        ''' 0 = Fixed tubesheet (12 mm clearance),
        ''' 1 = U-tube (30 mm clearance),
        ''' 2 = Split-ring floating head (65 mm clearance),
        ''' 3 = Pull-through floating head (95 mm clearance)
        ''' </param>
        ''' <returns>Estimated minimum shell internal diameter in mm.</returns>
        Public Function EstimateShellDiameter(Optional rearHeadType As Integer = 0) As Double

            Dim K1 As Double
            Dim n1 As Double

            Dim passes As Integer = Tube_PassesPerShell

            If Tube_Layout = 0 OrElse Tube_Layout = 1 Then
                'triangular (30 deg) or triangular rotated (60 deg)
                Select Case passes
                    Case 1 : K1 = 0.319 : n1 = 2.142
                    Case 2 : K1 = 0.249 : n1 = 2.207
                    Case 4 : K1 = 0.175 : n1 = 2.285
                    Case 6 : K1 = 0.0743 : n1 = 2.499
                    Case 8 : K1 = 0.0365 : n1 = 2.675
                    Case Else : K1 = 0.249 : n1 = 2.207
                End Select
            Else
                'square (90 deg) or square rotated (45 deg)
                Select Case passes
                    Case 1 : K1 = 0.215 : n1 = 2.207
                    Case 2 : K1 = 0.156 : n1 = 2.291
                    Case 4 : K1 = 0.158 : n1 = 2.263
                    Case 6 : K1 = 0.0402 : n1 = 2.617
                    Case 8 : K1 = 0.0331 : n1 = 2.643
                    Case Else : K1 = 0.156 : n1 = 2.291
                End Select
            End If

            Dim Nt As Integer = Tube_NumberPerShell
            Dim de As Double = Tube_De

            Dim Db As Double = de * (Nt / K1) ^ (1.0 / n1)

            Dim clearance As Double
            Select Case rearHeadType
                Case 0 : clearance = 12.0
                Case 1 : clearance = 30.0
                Case 2 : clearance = 65.0
                Case 3 : clearance = 95.0
                Case Else : clearance = 12.0
            End Select

            Return Db + clearance

        End Function

        ''' <summary>
        ''' Estimates the maximum number of tubes that fit in the current shell diameter
        ''' using the Coulson and Richardson empirical correlation (inverse of EstimateShellDiameter).
        ''' </summary>
        ''' <param name="rearHeadType">
        ''' Type of rear head for bundle-to-shell clearance:
        ''' 0 = Fixed tubesheet (12 mm clearance),
        ''' 1 = U-tube (30 mm clearance),
        ''' 2 = Split-ring floating head (65 mm clearance),
        ''' 3 = Pull-through floating head (95 mm clearance)
        ''' </param>
        ''' <returns>Estimated maximum number of tubes that fit in the shell.</returns>
        Public Function EstimateMaxTubes(Optional rearHeadType As Integer = 0) As Integer

            Dim K1 As Double
            Dim n1 As Double

            Dim passes As Integer = Tube_PassesPerShell

            If Tube_Layout = 0 OrElse Tube_Layout = 1 Then
                Select Case passes
                    Case 1 : K1 = 0.319 : n1 = 2.142
                    Case 2 : K1 = 0.249 : n1 = 2.207
                    Case 4 : K1 = 0.175 : n1 = 2.285
                    Case 6 : K1 = 0.0743 : n1 = 2.499
                    Case 8 : K1 = 0.0365 : n1 = 2.675
                    Case Else : K1 = 0.249 : n1 = 2.207
                End Select
            Else
                Select Case passes
                    Case 1 : K1 = 0.215 : n1 = 2.207
                    Case 2 : K1 = 0.156 : n1 = 2.291
                    Case 4 : K1 = 0.158 : n1 = 2.263
                    Case 6 : K1 = 0.0402 : n1 = 2.617
                    Case 8 : K1 = 0.0331 : n1 = 2.643
                    Case Else : K1 = 0.156 : n1 = 2.291
                End Select
            End If

            Dim clearance As Double
            Select Case rearHeadType
                Case 0 : clearance = 12.0
                Case 1 : clearance = 30.0
                Case 2 : clearance = 65.0
                Case 3 : clearance = 95.0
                Case Else : clearance = 12.0
            End Select

            Dim Db As Double = Shell_Di - clearance
            Dim de As Double = Tube_De

            If Db <= 0 OrElse de <= 0 Then Return 0

            Dim Nt As Double = K1 * (Db / de) ^ n1

            Return Math.Max(CInt(Math.Floor(Nt)), 1)

        End Function

        ''' <summary>Restores the STHX profile from XML.</summary>
        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data, True)
            Return True

        End Function

        ''' <summary>Serializes the STHX profile to XML.</summary>
        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Return XMLSerializer.XMLSerializer.Serialize(Me, True)

        End Function

    End Class

    Public Class HeatExchangerMaterial

        Public Name As String
        Public CommonName As String
        Public MaxTemperatureK As Double
        Public MinTemperatureK As Double
        Public MaxPressurePa As Double
        Public CostFactor As Double
        Public ThermalConductivity As Double
        Public Density As Double
        Public CorrosionResistance As String
        Public Notes As String

        Public Sub New(name As String, commonName As String, maxTK As Double, minTK As Double,
                       maxPPa As Double, cost As Double, k As Double, rho As Double,
                       corr As String, notes As String)
            Me.Name = name
            Me.CommonName = commonName
            Me.MaxTemperatureK = maxTK
            Me.MinTemperatureK = minTK
            Me.MaxPressurePa = maxPPa
            Me.CostFactor = cost
            Me.ThermalConductivity = k
            Me.Density = rho
            Me.CorrosionResistance = corr
            Me.Notes = notes
        End Sub

        Public Shared Function GetMaterialDatabase() As List(Of HeatExchangerMaterial)
            Dim db As New List(Of HeatExchangerMaterial) From {
                New HeatExchangerMaterial("SA-516 Gr.70", "Carbon Steel", 723, 243, 200000000, 1.0, 50, 7850,
                    "General service", "Most common, economical"),
                New HeatExchangerMaterial("SA-204 Gr.B", "C-0.5Mo", 773, 243, 200000000, 1.3, 42, 7850,
                    "H2 service, moderate temperature", "Hydrogen service up to 500 C"),
                New HeatExchangerMaterial("SA-387 Gr.11", "1.25Cr-0.5Mo", 866, 243, 200000000, 1.5, 38, 7850,
                    "High temperature, H2, H2S", "Cr-Mo for high temperature H2 and sulfur"),
                New HeatExchangerMaterial("SA-387 Gr.22", "2.25Cr-1Mo", 866, 243, 200000000, 1.8, 35, 7850,
                    "High temperature, H2", "Higher Cr-Mo for severe H2 service"),
                New HeatExchangerMaterial("SA-240 304", "SS 304", 1089, 77, 200000000, 3.0, 16, 8000,
                    "Moderate corrosion, oxidizing acids", "General purpose stainless"),
                New HeatExchangerMaterial("SA-240 316", "SS 316", 1089, 77, 200000000, 3.5, 14, 8000,
                    "Chlorides, acids, pitting resistance", "Mo addition for chloride resistance"),
                New HeatExchangerMaterial("SA-240 321", "SS 321", 1089, 77, 200000000, 3.5, 16, 8000,
                    "High temperature, intergranular corrosion", "Ti-stabilized for high T welded service"),
                New HeatExchangerMaterial("SA-240 304L", "SS 304L", 700, 77, 200000000, 3.2, 16, 8000,
                    "Welded fabrication, moderate corrosion", "Low-carbon for welding"),
                New HeatExchangerMaterial("SA-240 316L", "SS 316L", 700, 77, 200000000, 3.8, 14, 8000,
                    "Welded + chlorides, pharmaceutical", "Low-carbon + Mo"),
                New HeatExchangerMaterial("SB-463", "Alloy 20", 811, 200, 200000000, 6.0, 12, 8100,
                    "Sulfuric acid, mixed acids", "Excellent H2SO4 resistance"),
                New HeatExchangerMaterial("SA-240 2205", "Duplex 2205", 588, 233, 200000000, 4.5, 19, 7800,
                    "Chlorides, high strength, SCC", "Higher strength than 316, SCC resistant"),
                New HeatExchangerMaterial("SB-443", "Inconel 625", 1255, 77, 200000000, 8.0, 10, 8440,
                    "High temperature, severe corrosion", "Ni-Cr-Mo for extreme service"),
                New HeatExchangerMaterial("SB-575", "Hastelloy C-276", 1033, 77, 200000000, 10.0, 10, 8890,
                    "Highly corrosive, HCl, wet chlorine", "Most versatile Ni alloy"),
                New HeatExchangerMaterial("SB-265 Gr.2", "Titanium Gr.2", 588, 77, 200000000, 7.0, 22, 4510,
                    "Seawater, chlorides, oxidizing media", "Lightweight, excellent chloride resistance"),
                New HeatExchangerMaterial("SB-171 C70600", "Cu-Ni 90/10", 561, 200, 100000000, 4.0, 45, 8900,
                    "Seawater, marine", "Good seawater resistance, biofouling resistant"),
                New HeatExchangerMaterial("SB-171 C71500", "Cu-Ni 70/30", 561, 200, 100000000, 5.0, 30, 8950,
                    "Seawater, higher velocity", "Better erosion resistance than 90/10")
            }
            Return db
        End Function

        Public Shared Function SuggestMaterials(tempK As Double, pressPa As Double,
                                                 hasChlorides As Boolean, hasH2S As Boolean,
                                                 hasAmines As Boolean) As List(Of Tuple(Of HeatExchangerMaterial, String))
            Dim db = GetMaterialDatabase()
            Dim results As New List(Of Tuple(Of HeatExchangerMaterial, String))

            Dim candidates = db.Where(Function(m) m.MaxTemperatureK >= tempK AndAlso
                                                   m.MinTemperatureK <= tempK AndAlso
                                                   m.MaxPressurePa >= pressPa).ToList()

            If hasChlorides Then
                Dim cl = candidates.Where(Function(m) m.CorrosionResistance.ToLower().Contains("chloride") OrElse
                                                       m.CommonName.Contains("Titanium") OrElse
                                                       m.CommonName.Contains("Duplex")).
                                   OrderBy(Function(m) m.CostFactor).Take(3)
                For Each m In cl
                    results.Add(Tuple.Create(m, "Chloride service"))
                Next
            End If

            If hasH2S Then
                Dim h2s = candidates.Where(Function(m) m.CorrosionResistance.ToLower().Contains("h2s") OrElse
                                                        m.CorrosionResistance.ToLower().Contains("h2") OrElse
                                                        m.CommonName.Contains("Cr-Mo")).
                                    OrderBy(Function(m) m.CostFactor).Take(2)
                For Each m In h2s
                    If Not results.Any(Function(r) r.Item1.Name = m.Name) Then
                        results.Add(Tuple.Create(m, "H2S/H2 service"))
                    End If
                Next
            End If

            If hasAmines Then
                Dim am = candidates.Where(Function(m) m.CommonName = "Carbon Steel" OrElse
                                                       m.CommonName = "SS 304").
                                   OrderBy(Function(m) m.CostFactor).Take(2)
                For Each m In am
                    If Not results.Any(Function(r) r.Item1.Name = m.Name) Then
                        results.Add(Tuple.Create(m, "Amine service (PWHT for CS)"))
                    End If
                Next
            End If

            If tempK > 750 Then
                Dim ht = candidates.Where(Function(m) m.MaxTemperatureK >= tempK).
                                   OrderBy(Function(m) m.CostFactor).Take(3)
                For Each m In ht
                    If Not results.Any(Function(r) r.Item1.Name = m.Name) Then
                        results.Add(Tuple.Create(m, "High temperature service"))
                    End If
                Next
            End If

            If results.Count < 3 Then
                Dim gen = candidates.OrderBy(Function(m) m.CostFactor).Take(5 - results.Count)
                For Each m In gen
                    If Not results.Any(Function(r) r.Item1.Name = m.Name) Then
                        results.Add(Tuple.Create(m, "Lowest cost for operating conditions"))
                    End If
                Next
            End If

            Return results.OrderBy(Function(r) r.Item1.CostFactor).ToList()
        End Function

    End Class

End Namespace

