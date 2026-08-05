'    Component Separator Calculation Routines 
'    Copyright 2010 Daniel Wagner O. de Medeiros
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

''' <summary>
''' Contains auxiliary helper classes, enumerations, and data structures that support unit operation implementations.
''' </summary>
Namespace UnitOperations.Auxiliary

    ''' <summary>
    ''' Defines the type of separation specification used for a component in the Component Separator.
    ''' </summary>
    Public Enum SeparationSpec
        ''' <summary>Specify the component separation as an absolute mass flow rate.</summary>
        MassFlow
        ''' <summary>Specify the component separation as an absolute molar flow rate.</summary>
        MolarFlow
        ''' <summary>Specify the component separation as a percentage of the inlet mass flow rate.</summary>
        PercentInletMassFlow
        ''' <summary>Specify the component separation as a percentage of the inlet molar flow rate.</summary>
        PercentInletMolarFlow
    End Enum

    ''' <summary>
    ''' Stores the separation specification for a single compound in the Component Separator,
    ''' including the compound identifier, the type of specification, its numeric value, and unit.
    ''' </summary>
    <System.Serializable()> Public Class ComponentSeparationSpec

        Private _compID As String
        Private _sepspec As SeparationSpec = SeparationSpec.PercentInletMassFlow
        Private _specvalue As Double = 0
        Private _specunit As String = ""


        ''' <summary>
        ''' Gets or sets the identifier of the compound this specification applies to.
        ''' </summary>
        Public Property ComponentID() As String
            Get
                Return _compID
            End Get
            Set(ByVal value As String)
                _compID = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the type of separation specification (mass flow, molar flow, or percentage of inlet flow).
        ''' </summary>
        Public Property SepSpec() As SeparationSpec
            Get
                Return _sepspec
            End Get
            Set(ByVal value As SeparationSpec)
                _sepspec = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the numeric value of the separation specification.
        ''' </summary>
        Public Property SpecValue() As Double
            Get
                Return _specvalue
            End Get
            Set(ByVal value As Double)
                _specvalue = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the unit string associated with the separation specification value.
        ''' </summary>
        Public Property SpecUnit() As String
            Get
                Return _specunit
            End Get
            Set(ByVal value As String)
                _specunit = value
            End Set
        End Property

        ''' <summary>
        ''' Initializes a new instance of <see cref="ComponentSeparationSpec"/> with default values.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of <see cref="ComponentSeparationSpec"/> with the specified parameters.
        ''' </summary>
        ''' <param name="id">The compound identifier.</param>
        ''' <param name="spectype">The type of separation specification.</param>
        ''' <param name="specvalue">The numeric value of the specification.</param>
        ''' <param name="specunit">The unit of the specification value.</param>
        Sub New(ByVal id As String, ByVal spectype As Auxiliary.SeparationSpec, ByVal specvalue As Double, ByVal specunit As String)
            Me.ComponentID = id
            Me.SepSpec = spectype
            Me.SpecValue = specvalue
            Me.SpecUnit = specunit
        End Sub

    End Class

End Namespace

Namespace UnitOperations

    ''' <summary>
    ''' Represents a Component Separator unit operation that splits an inlet material stream into
    ''' two outlet streams based on user-defined per-compound separation specifications.
    ''' Specifications can be given as absolute mass flow, absolute molar flow, or as a percentage
    ''' of the inlet mass or molar flow for each compound. The energy imbalance is computed after
    ''' the mass balance and passed to an optional connected energy stream.
    ''' </summary>
    <System.Serializable()> Public Partial Class ComponentSeparator

        Inherits UnitOperations.UnitOpBaseClass

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>
        ''' Gets or sets the Base64-encoded embedded image data used for the object icon.
        ''' </summary>
        Public Property EmbeddedImageData As String = ""

        ''' <summary>
        ''' Gets or sets a value indicating whether the embedded image should be used as the object icon.
        ''' </summary>
        Public Property UseEmbeddedImage As Boolean = False

        ''' <summary>
        ''' Gets or sets the simulation object class, identifying this object as a Separator.
        ''' </summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Separators

        Protected m_ei As Double
        Protected _compsepspeccollection As New Dictionary(Of String, ComponentSeparationSpec)
        Protected _streamindex As Byte = 0

        ''' <summary>
        ''' Creates a deep copy of this <see cref="ComponentSeparator"/> by serializing and deserializing via XML.
        ''' </summary>
        ''' <returns>A new <see cref="ComponentSeparator"/> instance with the same state.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New ComponentSeparator()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Creates a deep copy of this <see cref="ComponentSeparator"/> by serializing and deserializing via JSON.
        ''' </summary>
        ''' <returns>A new <see cref="ComponentSeparator"/> instance with the same state.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of ComponentSeparator)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = True

        Public Overrides Sub CreateDynamicProperties()

            AddDynamicProperty("Volume", "Internal holdup volume (m3).", 0.1, UnitOfMeasure.volume, 1.0.GetType())
            AddDynamicProperty("Minimum Pressure", "Minimum dynamic pressure.", 101325.0, UnitOfMeasure.pressure, 1.0.GetType())
            AddDynamicProperty("Initialize using Inlet Stream", "Initializes volume content from the inlet stream.", True, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Reset Content", "Empties the volume content on the next run.", False, UnitOfMeasure.none, True.GetType())

        End Sub

        Public Overrides Sub RunDynamicModel()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds
            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0

            Dim Vol As Double = GetDynamicProperty("Volume")
            Dim Pmin As Double = GetDynamicProperty("Minimum Pressure")
            Dim InitializeFromInlet As Boolean = GetDynamicProperty("Initialize using Inlet Stream")
            Dim Reset As Boolean = GetDynamicProperty("Reset Content")

            If Reset Then
                AccumulationStream = Nothing
                SetDynamicProperty("Reset Content", 0)
            End If

            Dim ims As MaterialStream = Me.GetInletMaterialStream(0)
            Dim oms1 As MaterialStream = Me.GetOutletMaterialStream(0)
            Dim oms2 As MaterialStream = Me.GetOutletMaterialStream(1)

            If AccumulationStream Is Nothing Then
                If InitializeFromInlet Then
                    AccumulationStream = ims.CloneXML
                Else
                    AccumulationStream = ims.Subtract(oms1, timestep)
                End If
                Dim density = AccumulationStream.Phases(0).Properties.density.GetValueOrDefault
                AccumulationStream.SetMassFlow(density * Vol)
                AccumulationStream.SpecType = StreamSpec.Temperature_and_Pressure
                AccumulationStream.PropertyPackage = PropertyPackage
                AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream
                AccumulationStream.Calculate()
            Else
                AccumulationStream.SetFlowsheet(FlowSheet)
                If ims.GetMassFlow() > 0 Then AccumulationStream = AccumulationStream.Add(ims, timestep)
                AccumulationStream.PropertyPackage.CurrentMaterialStream = AccumulationStream
                AccumulationStream.Calculate()
                If oms1.GetMassFlow() > 0 Then AccumulationStream = AccumulationStream.Subtract(oms1, timestep)
                If oms2 IsNot Nothing AndAlso oms2.GetMassFlow() > 0 Then AccumulationStream = AccumulationStream.Subtract(oms2, timestep)
                If AccumulationStream.GetMassFlow <= 0.0 Then AccumulationStream.SetMassFlow(0.0)
            End If

            AccumulationStream.SetFlowsheet(FlowSheet)
            AccumulationStream.SetPressure(Math.Max(AccumulationStream.GetPressure(), Pmin))

            oms1.AssignFromPhase(PhaseLabel.Mixture, AccumulationStream, False)
            oms1.SetTemperature(AccumulationStream.GetTemperature)
            oms1.SetMassEnthalpy(AccumulationStream.GetMassEnthalpy)
            oms1.SetPressure(AccumulationStream.GetPressure())

            If oms2 IsNot Nothing Then
                oms2.AssignFromPhase(PhaseLabel.Mixture, AccumulationStream, False)
                oms2.SetTemperature(AccumulationStream.GetTemperature)
                oms2.SetMassEnthalpy(AccumulationStream.GetMassEnthalpy)
                oms2.SetPressure(AccumulationStream.GetPressure())
            End If

        End Sub

        ''' <summary>
        ''' Restores the object state from a list of XML elements previously produced by <see cref="SaveData"/>.
        ''' </summary>
        ''' <param name="data">The list of XML elements containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            MyBase.LoadData(data)

            Me.ComponentSepSpecs = New Dictionary(Of String, ComponentSeparationSpec)

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "SeparationSpecs").SingleOrDefault.Elements.ToList
                Dim spec As New ComponentSeparationSpec With {.ComponentID = xel.@CompID, .SepSpec = [Enum].Parse(New ComponentSeparationSpec().SepSpec.GetType, xel.@SepSpec), .SpecUnit = xel.@SpecUnit, .SpecValue = xel.@SpecValue.ToDoubleFromInvariant}
                _compsepspeccollection.Add(xel.@ID, spec)
            Next
            Return True
        End Function

        ''' <summary>Serializes the component separator state, including separation specs, to XML.</summary>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                .Add(New XElement("SeparationSpecs"))
                For Each kvp As KeyValuePair(Of String, ComponentSeparationSpec) In _compsepspeccollection
                    .Item(.Count - 1).Add(New XElement("SeparationSpec", New XAttribute("ID", kvp.Key),
                                                       New XAttribute("CompID", kvp.Value.ComponentID),
                                                       New XAttribute("SepSpec", kvp.Value.SepSpec),
                                                       New XAttribute("SpecUnit", kvp.Value.SpecUnit),
                                                       New XAttribute("SpecValue", kvp.Value.SpecValue.ToString(ci))))
                Next
            End With

            Return elements

        End Function

        ''' <summary>Gets or sets the index of the outlet stream whose composition is specified.</summary>
        Public Property SpecifiedStreamIndex() As Byte
            Get
                Return _streamindex
            End Get
            Set(ByVal value As Byte)
                _streamindex = value
            End Set
        End Property

        ''' <summary>Gets or sets the dictionary of per-compound separation specifications.</summary>
        Public Property ComponentSepSpecs() As Dictionary(Of String, ComponentSeparationSpec)
            Get
                Return _compsepspeccollection
            End Get
            Set(ByVal value As Dictionary(Of String, ComponentSeparationSpec))
                _compsepspeccollection = value
            End Set
        End Property

        ''' <summary>Gets or sets the calculated energy imbalance (W).</summary>
        Public Property EnergyImb() As Double
            Get
                Return m_ei
            End Get
            Set(ByVal value As Double)
                m_ei = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>Initializes a new instance with a name and description.</summary>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description
            Me.ComponentSepSpecs = New Dictionary(Of String, ComponentSeparationSpec)

        End Sub

        ''' <summary>Calculates the component separator using the specified separation specs.</summary>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("The Component Separator is a mass balance unit operation. The 
                                components are separated between two streams, specified as 
                                fractions or absolute flow rates. The energy balance is then 
                                calculated after the separation.")

            Dim su As SystemsOfUnits.Units = FlowSheet.FlowsheetOptions.SelectedUnitSystem
            Dim cv As New SystemsOfUnits.Converter

            Dim instr, outstr1, outstr2, specstr, otherstr As MaterialStream
            instr = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)
            outstr1 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
            outstr2 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name)

            'get component ids

            Dim namesS, namesC, toremove As New ArrayList

            For Each sb As Compound In instr.Phases(0).Compounds.Values
                namesS.Add(sb.Name)
            Next
            For Each cs As ComponentSeparationSpec In Me.ComponentSepSpecs.Values
                namesC.Add(cs.ComponentID)
            Next

            If namesC.Count > namesS.Count Then

            ElseIf namesC.Count < namesS.Count Then

            End If

            If Me.SpecifiedStreamIndex = 0 Then
                specstr = outstr1
                otherstr = outstr2
            Else
                specstr = outstr2
                otherstr = outstr1
            End If

            'separate components according to specifications

            For Each cs As ComponentSeparationSpec In Me.ComponentSepSpecs.Values
                If specstr.Phases(0).Compounds.ContainsKey(cs.ComponentID) Then
                    With specstr.Phases(0).Compounds(cs.ComponentID)
                        Select Case cs.SepSpec
                            Case SeparationSpec.MassFlow
                                .MassFlow = SystemsOfUnits.Converter.ConvertToSI(cs.SpecUnit, cs.SpecValue)
                                .MolarFlow = .MassFlow / .ConstantProperties.Molar_Weight * 1000
                            Case SeparationSpec.MolarFlow
                                .MolarFlow = SystemsOfUnits.Converter.ConvertToSI(cs.SpecUnit, cs.SpecValue)
                                .MassFlow = .MolarFlow * .ConstantProperties.Molar_Weight / 1000
                            Case SeparationSpec.PercentInletMassFlow
                                Dim mf As Double = instr.Phases(0).Compounds(cs.ComponentID).MassFlow.GetValueOrDefault
                                If mf <> 0.0# Then
                                    .MassFlow = cs.SpecValue / 100 * mf
                                Else
                                    .MassFlow = 0.0#
                                End If
                                .MolarFlow = .MassFlow / .ConstantProperties.Molar_Weight * 1000
                            Case SeparationSpec.PercentInletMolarFlow
                                Dim mf As Double = instr.Phases(0).Compounds(cs.ComponentID).MolarFlow.GetValueOrDefault
                                If mf <> 0.0# Then
                                    .MolarFlow = cs.SpecValue / 100 * mf
                                Else
                                    .MolarFlow = 0.0#
                                End If
                                .MassFlow = .MolarFlow * .ConstantProperties.Molar_Weight / 1000
                        End Select
                        CheckSpec(.MolarFlow, False, "component molar flow: " & .ConstantProperties.Name.ToString.ToLower)
                        CheckSpec(.MassFlow, False, "component mass flow: " & .ConstantProperties.Name.ToString.ToLower)
                    End With
                    With otherstr.Phases(0).Compounds(cs.ComponentID)
                        .MassFlow = instr.Phases(0).Compounds(cs.ComponentID).MassFlow.GetValueOrDefault - specstr.Phases(0).Compounds(cs.ComponentID).MassFlow.GetValueOrDefault
                        If .MassFlow < -0.000001 Then
                            Throw New Exception(String.Format("Calculated negative mass flow for stream {0}, compound {1} [{2} kg/s].", otherstr.GraphicObject.Tag, cs.ComponentID, .MassFlow))
                        ElseIf .MassFlow < 0.0 Then
                            .MassFlow = 0.0
                        End If
                        .MolarFlow = instr.Phases(0).Compounds(cs.ComponentID).MolarFlow.GetValueOrDefault - specstr.Phases(0).Compounds(cs.ComponentID).MolarFlow.GetValueOrDefault
                        If .MolarFlow < -0.000001 Then
                            Throw New Exception(String.Format("Calculated negative molar flow for stream {0}, compound {1} [{2} mol/s].", otherstr.GraphicObject.Tag, cs.ComponentID, .MolarFlow))
                        ElseIf .MolarFlow < 0.0 Then
                            .MolarFlow = 0.0
                        End If
                    End With
                Else
                    toremove.Add(cs.ComponentID)
                End If
            Next

            For Each cs As String In toremove
                If Me.ComponentSepSpecs.ContainsKey(cs) Then Me.ComponentSepSpecs.Remove(cs)
            Next

            Dim summ, sumw As Double

            summ = 0
            sumw = 0
            For Each sb As Compound In specstr.Phases(0).Compounds.Values
                summ += sb.MolarFlow.GetValueOrDefault
                sumw += sb.MassFlow.GetValueOrDefault
            Next
            specstr.Phases(0).Properties.molarflow = summ
            specstr.Phases(0).Properties.massflow = sumw
            specstr.DefinedFlow = FlowSpec.Mass
            For Each sb As Compound In specstr.Phases(0).Compounds.Values
                If summ <> 0.0# Then sb.MoleFraction = sb.MolarFlow.GetValueOrDefault / summ Else sb.MoleFraction = 0.0#
                If sumw <> 0.0# Then sb.MassFraction = sb.MassFlow.GetValueOrDefault / sumw Else sb.MassFraction = 0.0#
            Next
            summ = 0
            sumw = 0
            For Each sb As Compound In otherstr.Phases(0).Compounds.Values
                summ += sb.MolarFlow.GetValueOrDefault
                sumw += sb.MassFlow.GetValueOrDefault
            Next
            otherstr.Phases(0).Properties.molarflow = summ
            otherstr.Phases(0).Properties.massflow = sumw
            otherstr.DefinedFlow = FlowSpec.Mass
            For Each sb As Compound In otherstr.Phases(0).Compounds.Values
                If summ <> 0.0# Then sb.MoleFraction = sb.MolarFlow.GetValueOrDefault / summ Else sb.MoleFraction = 0.0#
                If sumw <> 0.0# Then sb.MassFraction = sb.MassFlow.GetValueOrDefault / sumw Else sb.MassFraction = 0.0#
            Next

            'pass conditions

            outstr1.Phases(0).Properties.temperature = instr.Phases(0).Properties.temperature.GetValueOrDefault
            outstr1.Phases(0).Properties.pressure = instr.Phases(0).Properties.pressure.GetValueOrDefault
            outstr2.Phases(0).Properties.temperature = instr.Phases(0).Properties.temperature.GetValueOrDefault
            outstr2.Phases(0).Properties.pressure = instr.Phases(0).Properties.pressure.GetValueOrDefault

            Dim Hi, Ho1, Ho2, Wi, Wo1, Wo2 As Double

            Hi = instr.Phases(0).Properties.enthalpy.GetValueOrDefault
            Wi = instr.Phases(0).Properties.massflow.GetValueOrDefault

            Wo1 = outstr1.Phases(0).Properties.massflow.GetValueOrDefault
            Wo2 = outstr2.Phases(0).Properties.massflow.GetValueOrDefault

            CheckSpec(Hi, False, "inlet enthalpy")
            CheckSpec(Wi, True, "inlet mass flow")
            CheckSpec(Wo1, True, "outlet mass flow")
            CheckSpec(Wo1, True, "outlet mass flow")

            'do a flash calculation on streams to calculate energy imbalance

            If Wo1 <> 0.0# Then
                outstr1.PropertyPackage.CurrentMaterialStream = outstr1
                outstr1.PropertyPackage.DW_CalcEquilibrium(PropertyPackages.FlashSpec.T, PropertyPackages.FlashSpec.P)
                IObj?.SetCurrent()
                outstr1.AtEquilibrium = False
                outstr1.Calculate(True, True)
                Ho1 = outstr1.Phases(0).Properties.enthalpy.GetValueOrDefault
            End If

            If Wo2 <> 0.0# Then
                outstr2.PropertyPackage.CurrentMaterialStream = outstr2
                outstr2.PropertyPackage.DW_CalcEquilibrium(PropertyPackages.FlashSpec.T, PropertyPackages.FlashSpec.P)
                IObj?.SetCurrent()
                outstr2.AtEquilibrium = False
                outstr2.Calculate(True, True)
                Ho2 = outstr2.Phases(0).Properties.enthalpy.GetValueOrDefault
            End If

            'calculate imbalance

            EnergyImb = Hi * Wi - Ho1 * Wo1 - Ho2 * Wo2

            CheckSpec(EnergyImb, False, "energy balance")

            'update energy stream power value

            If GetEnergyStream() IsNot Nothing Then
                With GetEnergyStream()
                    .EnergyFlow = EnergyImb
                    .GraphicObject.Calculated = True
                End With
            End If

            IObj?.Close()

        End Sub

        ''' <summary>Clears all calculated results.</summary>
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

            cp = Me.GraphicObject.OutputConnectors(1)
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

            'energy stream - update energy flow value (kW)
            If Me.GraphicObject.EnergyConnector.IsAttached Then
                With DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.EnergyConnector.AttachedConnector.AttachedTo.Name), Streams.EnergyStream)
                    .EnergyFlow = Nothing
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

                If prop.StartsWith("SepSpecValue_") Then

                    Dim compound As String = prop.Split("_")(1)

                    If ComponentSepSpecs.ContainsKey(compound) Then
                        Return SystemsOfUnits.Converter.ConvertToSI(ComponentSepSpecs(compound).SpecUnit, ComponentSepSpecs(compound).SpecValue)
                    End If

                ElseIf prop.Equals("SpecifiedStreamIndex") Then

                    Return SpecifiedStreamIndex

                Else

                    Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                    Select Case propidx

                        Case 0

                            value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.EnergyImb)

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
                Case PropertyType.WR
                Case PropertyType.ALL
                    For i = 0 To 0
                        proplist.Add("PROP_CP_" + CStr(i))
                    Next
                Case PropertyType.RO
                    For i = 0 To 0
                        proplist.Add("PROP_CP_" + CStr(i))
                    Next
            End Select
            proplist.Add("SpecifiedStreamIndex")
            For Each item In ComponentSepSpecs.Values
                proplist.Add("SepSpecValue_" & item.ComponentID)
            Next
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>Sets the value of the specified property.</summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI

            If prop.StartsWith("SepSpecValue_") Then

                Dim compound As String = prop.Split("_")(1)

                If ComponentSepSpecs.ContainsKey(compound) Then
                    ComponentSepSpecs(compound).SpecValue = SystemsOfUnits.Converter.ConvertFromSI(ComponentSepSpecs(compound).SpecUnit, propval)
                End If

            ElseIf prop.Equals("SpecifiedStreamIndex") Then

                SpecifiedStreamIndex = propval

            End If


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

                If prop.StartsWith("SepSpecValue_") Then

                    Dim compound As String = prop.Split("_")(1)

                    If ComponentSepSpecs.ContainsKey(compound) Then
                        Return ComponentSepSpecs(compound).SpecUnit
                    Else
                        Return ""
                    End If

                ElseIf prop.Equals("SpecifiedStreamIndex") Then

                Else

                    Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                    Select Case propidx
                        Case 0
                            value = su.heatflow
                    End Select

                End If

                Return value

            End If

        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.component_separator.png")

        End Function

        ''' <summary>Returns the localised display description.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("CSEP_Desc")
        End Function

        ''' <summary>Returns the localised display name.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("CSEP_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>Generates a plain-text report of the component separator results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Return ""
        End Function

        ''' <summary>Returns a human-readable description of the specified property.</summary>
        Public Overrides Function GetPropertyDescription(p As String) As String
            If p.Equals("Specified Stream") Then
                Return "Select the outlet stream to which the specified separation values will be applied to."
            Else
                Return p
            End If
        End Function

    End Class

End Namespace