'    Tank Calculation Routines 
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
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums

Namespace UnitOperations

    ''' <summary>
    ''' Represents a Tank unit operation that models a storage vessel for material accumulation.
    ''' In dynamic mode, the tank tracks liquid inventory over time, integrating inlet and outlet
    ''' mass flows to update the accumulated content. In steady-state mode, the tank passes the
    ''' inlet stream through to the outlet, applying an optional pressure drop.
    ''' </summary>
    <System.Serializable()> Public Partial Class Tank

        Inherits UnitOperations.UnitOpBaseClass
        ''' <summary>Gets or sets the simulation object class, categorized as a Separator.</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Separators

        ''' <summary>Gets a value indicating that this unit operation supports dynamic (time-stepping) mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating that this unit operation exposes properties for dynamic mode configuration.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = True

        ''' <summary>Gets the list of equipment orientation types supported by the tank (vertical, horizontal).</summary>
        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Vertical", "Horizontal"}
            End Get
        End Property

        ''' <summary>Creates the list of physical dimensions associated with the tank (volume).</summary>
        Public Overrides Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Volume, .IsUserDefined = False})

        End Sub

        ''' <summary>Updates the dimensions list values from the current tank properties (e.g., Volume).</summary>
        Public Overrides Sub UpdateDimensionsList()

            Dimensions(0).Value = Volume

        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Protected m_dp As Nullable(Of Double)
        Protected m_DQ As Nullable(Of Double)
        Protected m_vol As Double = 0
        Protected m_tRes As Double = 0

        Protected m_ignorephase As Boolean = True

        ''' <summary>
        ''' Gets or sets a value indicating whether the presence of a vapour phase in the inlet
        ''' stream should be ignored during steady-state calculation. When <c>False</c>, a vapour
        ''' phase will raise an error.
        ''' </summary>
        Public Property IgnorePhase() As Boolean
            Get
                Return m_ignorephase
            End Get
            Set(ByVal value As Boolean)
                m_ignorephase = value
            End Set
        End Property

        ''' <summary>Initializes a new instance of the <see cref="Tank"/> class with a name and description.</summary>
        ''' <param name="name">The component name assigned to this tank object.</param>
        ''' <param name="description">A short description of this tank object.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description


        End Sub

        ''' <summary>Creates a deep copy of this tank by serializing and deserializing via XML.</summary>
        ''' <returns>A new <see cref="Tank"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Tank()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this tank by serializing and deserializing via JSON.</summary>
        ''' <returns>A new <see cref="Tank"/> instance with the same property values.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Tank)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Gets or sets the tank total volume (m³).</summary>
        Public Property Volume() As Double
            Get
                Return m_vol
            End Get
            Set(ByVal value As Double)
                m_vol = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated liquid residence time (s) based on volume and throughput.</summary>
        Public Property ResidenceTime() As Double
            Get
                Return m_tRes
            End Get
            Set(ByVal value As Double)
                m_tRes = value
            End Set
        End Property

        ''' <summary>Gets or sets the pressure drop across the tank (Pa).</summary>
        Public Property DeltaP() As Nullable(Of Double)
            Get
                Return m_dp
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_dp = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated heat duty exchanged by the tank (kW).</summary>
        Public Property DeltaQ() As Nullable(Of Double)
            Get
                Return m_DQ
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_DQ = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="Tank"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Registers the dynamic properties exposed in dynamic simulation mode,
        ''' such as liquid level, height, and initialisation options.
        ''' </summary>
        Public Overrides Sub CreateDynamicProperties()

            AddDynamicProperty("Liquid Level", "Current Liquid Level", 0, UnitOfMeasure.distance, 1.0.GetType())
            AddDynamicProperty("Height", "Available Liquid Height", 2, UnitOfMeasure.distance, 1.0.GetType())
            AddDynamicProperty("Initialize using Inlet Stream", "Initializes the tank's content with information from the inlet stream, if the vessel content is null.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Reset Content", "Empties the tank's content on the next run.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Closed Tank", "Model as a closed tank with vapor space pressure calculation instead of atmospheric.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Ambient Temperature", "Ambient temperature for heat loss calculation (K).", 298.15, UnitOfMeasure.temperature, 1.0.GetType())
            AddDynamicProperty("Ambient UA Product", "Overall heat transfer coefficient times area for ambient heat loss (W/K). Set to 0 to disable.", 0.0, UnitOfMeasure.heat_transf_coeff, 1.0.GetType())
            AddDynamicProperty("Operating Pressure", "Current operating pressure (read-only in open tank mode).", 101325.0, UnitOfMeasure.pressure, 1.0.GetType())
            AddDynamicProperty("Minimum Pressure", "Minimum dynamic pressure.", 101325.0, UnitOfMeasure.pressure, 1.0.GetType())

        End Sub

        Private prevM, currentM As Double

        ''' <summary>
        ''' Executes one dynamic simulation step for the tank: integrates inlet and outlet
        ''' mass flows over the current time step, updates the accumulation stream, and
        ''' flashes the content to determine the new liquid level and outlet conditions.
        ''' </summary>
        Public Overrides Sub RunDynamicModel()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds

            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0

            Dim ims As MaterialStream = Me.GetInletMaterialStream(0)
            Dim oms1 As MaterialStream = Me.GetOutletMaterialStream(0)

            Dim s2 As Enums.Dynamics.DynamicsSpecType
            s2 = oms1.DynamicsSpec

            Dim Height As Double = GetDynamicProperty("Height")
            Dim InitializeFromInlet As Boolean = GetDynamicProperty("Initialize using Inlet Stream")
            Dim IsClosedTank As Boolean = GetDynamicProperty("Closed Tank")
            Dim Tambient As Double = GetDynamicProperty("Ambient Temperature")
            Dim UA As Double = GetDynamicProperty("Ambient UA Product")
            Dim Pmin As Double = GetDynamicProperty("Minimum Pressure")

            Dim Reset As Boolean = GetDynamicProperty("Reset Content")

            If Reset Then
                AccumulationStream = Nothing
                SetDynamicProperty("Reset Content", 0)
            End If

            If s2 = Dynamics.DynamicsSpecType.Pressure Then

                If AccumulationStream Is Nothing Then

                    If InitializeFromInlet Then

                        AccumulationStream = ims.CloneXML

                    Else

                        AccumulationStream = ims.Subtract(oms1, timestep)

                    End If

                Else

                    AccumulationStream.SetFlowsheet(FlowSheet)
                    If ims.GetMassFlow() > 0 Then AccumulationStream = AccumulationStream.Add(ims, timestep)
                    AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream
                    AccumulationStream.Calculate()
                    If oms1.GetMassFlow() > 0 Then AccumulationStream = AccumulationStream.Subtract(oms1, timestep)
                    If AccumulationStream.GetMassFlow <= 0.0 Then AccumulationStream.SetMassFlow(0.0)

                End If

                AccumulationStream.SetFlowsheet(FlowSheet)

                Dim Ha = AccumulationStream.GetMassEnthalpy()
                Dim Wa = AccumulationStream.GetMassFlow()

                Dim Qext As Double = 0.0

                If UA > 0.0 AndAlso Wa > 0.0 Then
                    Qext = UA * (Tambient - AccumulationStream.GetTemperature()) / 1000.0
                End If

                If Qext <> 0.0 AndAlso Wa > 0.0 Then
                    AccumulationStream.SetMassEnthalpy(Ha + Qext * timestep / Wa)
                End If

                AccumulationStream.PropertyPackage = PropertyPackage
                AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream

                If IsClosedTank Then

                    Dim Pressure = AccumulationStream.GetPressure()
                    Dim Temperature = AccumulationStream.GetTemperature()
                    Dim M = AccumulationStream.GetMolarFlow()

                    prevM = currentM
                    If M > 0 Then currentM = Volume / M

                    If prevM = 0.0 Or integrator.ShouldCalculateEquilibrium Then

                        If M > 0 Then
                            Dim result = PropertyPackage.CalculateEquilibrium2(FlashCalculationType.VolumeTemperature, currentM, Temperature, Pressure)
                            Pressure = result.CalculatedPressure
                            Dim Enthalpy As Double = result.CalculatedEnthalpy
                            AccumulationStream.SetMassEnthalpy(CDbl(Enthalpy))
                        End If

                    Else

                        If prevM > 0 Then Pressure = currentM / prevM * Pressure

                    End If

                    If Pressure < Pmin Then Pressure = Pmin

                    AccumulationStream.SetPressure(Pressure)
                    AccumulationStream.SpecType = StreamSpec.Pressure_and_Enthalpy

                Else

                    AccumulationStream.SetPressure(101325)
                    AccumulationStream.SpecType = StreamSpec.Pressure_and_Enthalpy

                End If

                If integrator.ShouldCalculateEquilibrium Then

                    AccumulationStream.Calculate(True, True)

                End If

                Dim LiquidVolume, RelativeLevel As Double

                LiquidVolume = AccumulationStream.Phases(3).Properties.volumetric_flow.GetValueOrDefault

                If Volume > 0 Then
                    RelativeLevel = LiquidVolume / Volume
                Else
                    RelativeLevel = 0
                End If

                SetDynamicProperty("Liquid Level", RelativeLevel * Height)
                SetDynamicProperty("Operating Pressure", AccumulationStream.GetPressure())

                Dim liqdens = AccumulationStream.Phases(3).Properties.density.GetValueOrDefault

                oms1.AssignFromPhase(PhaseLabel.Mixture, AccumulationStream, False)

                oms1.SetPressure(AccumulationStream.GetPressure + liqdens * 9.8 * RelativeLevel * Height)

            End If

        End Sub

        ''' <summary>Calculates the tank (mixing) operation.</summary>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            Dim Ti, Pi, Hi, Wi, rho_li, qli, qvi, ei, ein, P2, Q As Double

            Dim ims, oms As MaterialStream

            ims = GetInletMaterialStream(0)
            oms = GetOutletMaterialStream(0)

            qvi = ims.Phases(2).Properties.volumetric_flow.GetValueOrDefault.ToString

            If qvi > 0 And Me.IgnorePhase = False Then
                Throw New Exception(FlowSheet.GetTranslatedString("ExisteumaPhasevaporna2"))
            ElseIf Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            ElseIf Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            Me.PropertyPackage.CurrentMaterialStream = ims
            Ti = ims.Phases(0).Properties.temperature.GetValueOrDefault.ToString
            Pi = ims.Phases(0).Properties.pressure.GetValueOrDefault.ToString
            rho_li = ims.Phases(1).Properties.density.GetValueOrDefault.ToString
            qli = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault.ToString
            Hi = ims.Phases(0).Properties.enthalpy.GetValueOrDefault.ToString
            Wi = ims.Phases(0).Properties.massflow.GetValueOrDefault.ToString
            Q = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault
            ei = Hi * Wi
            ein = ei

            P2 = Pi - Me.DeltaP.GetValueOrDefault

            'Atribuir valores a corrente de materia conectada a jusante
            With oms
                .AtEquilibrium = False
                .Phases(0).Properties.temperature = Ti
                .Phases(0).Properties.pressure = P2
                .Phases(0).Properties.enthalpy = Hi
                Dim comp As BaseClasses.Compound
                Dim i As Integer = 0
                For Each comp In .Phases(0).Compounds.Values
                    comp.MoleFraction = ims.Phases(0).Compounds(comp.Name).MoleFraction
                    comp.MassFraction = ims.Phases(0).Compounds(comp.Name).MassFraction
                    i += 1
                Next
                .Phases(0).Properties.massflow = ims.Phases(0).Properties.massflow.GetValueOrDefault
                .DefinedFlow = FlowSpec.Mass
            End With

            Me.ResidenceTime = Me.Volume / Q

            IObj?.Close()

        End Sub

        ''' <summary>Clears all calculated results.</summary>
        Public Overrides Sub DeCalculate()

            If Me.GraphicObject.OutputConnectors(0).IsAttached Then

                'Zerar valores da corrente de materia conectada a jusante
                With GetOutletMaterialStream(0)
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
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.DeltaP.GetValueOrDefault)
                    Case 1
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.volume, Me.Volume)
                    Case 2
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.time, Me.ResidenceTime)
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
                Case PropertyType.RW
                    For i = 2 To 2
                        proplist.Add("PROP_TK_" + CStr(i))
                    Next
                Case PropertyType.RW
                    For i = 0 To 2
                        proplist.Add("PROP_TK_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 1
                        proplist.Add("PROP_TK_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To 2
                        proplist.Add("PROP_TK_" + CStr(i))
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
                    'PROP_TK_0	Pressure Drop
                    Me.DeltaP = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)
                Case 1
                    'PROP_TK_1	Volume
                    Me.Volume = SystemsOfUnits.Converter.ConvertToSI(su.volume, propval)
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
                        'PROP_TK_0	Pressure Drop
                        value = su.deltaP

                    Case 1
                        'PROP_TK_1	Volume
                        value = su.volume

                    Case 2
                        'PROP_TK_2	Residence Time
                        value = su.time

                End Select

                Return value
            End If
        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.tank.png")

        End Function

        ''' <summary>Returns the localised display description.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("TANK_Desc")
        End Function

        ''' <summary>Returns the localised display name.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("TANK_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace


