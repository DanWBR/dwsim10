'    Splitter Calculation Routines 
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

Namespace UnitOperations

    ''' <summary>
    ''' Represents a stream splitter unit operation that divides one inlet material stream
    ''' into up to three outlet streams. The split can be specified using fractional ratios,
    ''' absolute mass flow rates, or absolute molar flow rates. The outlet streams share the
    ''' same temperature, pressure, enthalpy, and composition as the inlet stream.
    ''' </summary>
    <System.Serializable()> Public Partial Class Splitter

        Inherits UnitOperations.UnitOpBaseClass
        ''' <summary>
        ''' Gets or sets the simulation object class category for this unit operation.
        ''' </summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.MixersSplitters

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>
        ''' Gets a value indicating whether this unit operation supports dynamic simulation mode.
        ''' </summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>
        ''' Gets a value indicating whether this unit operation exposes dedicated properties for dynamic mode configuration.
        ''' </summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>
        ''' Defines the calculation mode used by the splitter to determine how outlet stream flows are specified.
        ''' </summary>
        Public Enum OpMode
            ''' <summary>
            ''' Outlet flows are determined by fractional split ratios that must sum to 1.
            ''' </summary>
            SplitRatios = 0
            ''' <summary>
            ''' Outlet flows are specified as absolute mass flow rates (kg/s).
            ''' </summary>
            StreamMassFlowSpec = 1
            ''' <summary>
            ''' Outlet flows are specified as absolute molar flow rates (mol/s).
            ''' </summary>
            StreamMoleFlowSpec = 2
            ''' <summary>
            ''' Outlet flows are specified as absolute volumetric flow rates (m3/s).
            ''' </summary>
            StreamVolumetricFlowSpec = 3
        End Enum

        Protected m_ratios As New System.Collections.ArrayList(3)

        ''' <summary>
        ''' Gets or sets the number of currently connected outlet streams.
        ''' </summary>
        Public OutCount As Integer = 0

        ''' <summary>
        ''' Gets or sets the flow specification for the first outlet stream when using mass or molar flow specification modes.
        ''' </summary>
        Public Property StreamFlowSpec As Double = 0.0#
        ''' <summary>
        ''' Gets or sets the flow specification for the second outlet stream when using mass or molar flow specification modes and three outlets are connected.
        ''' </summary>
        Public Property Stream2FlowSpec As Double = 0.0#

        ''' <summary>
        ''' Gets or sets the current calculation mode that determines how outlet stream flows are specified.
        ''' </summary>
        Public Property OperationMode As OpMode = OpMode.SplitRatios

        ''' <summary>
        ''' Returns an array of strings describing all available calculation modes for this splitter.
        ''' </summary>
        ''' <returns>An array of mode descriptor strings in the format "Name: &lt;name&gt;  ID: &lt;id&gt;".</returns>
        Public Overrides Function GetCalculationModes() As String()

            Dim modes As New List(Of String)

            For Each tstEnum As OpMode In System.Enum.GetValues(GetType(OpMode))
                modes.Add(String.Format("Name: {0}  ID: {1}", tstEnum.ToString, CInt(tstEnum).ToString()))
            Next

            Return modes.ToArray()

        End Function

        ''' <summary>
        ''' Sets the active calculation mode for the splitter using a numeric identifier.
        ''' </summary>
        ''' <param name="modeID">The integer ID corresponding to an <see cref="OpMode"/> value.</param>
        ''' <returns>The string name of the newly set operation mode.</returns>
        Public Overrides Function SetCalculationMode(modeID As Integer) As Object

            Me.OperationMode = modeID

            Return OperationMode.ToString()

        End Function

        ''' <summary>
        ''' Creates a deep copy of this splitter instance by serializing and deserializing through XML.
        ''' </summary>
        ''' <returns>A new <see cref="Splitter"/> instance with identical state.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Splitter()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>
        ''' Creates a deep copy of this splitter instance by serializing and deserializing through JSON.
        ''' </summary>
        ''' <returns>A new <see cref="Splitter"/> instance with identical state.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Splitter)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Restores the splitter state from a list of XML elements previously produced by <see cref="SaveData"/>.
        ''' </summary>
        ''' <param name="data">A list of <see cref="XElement"/> objects containing the serialized splitter data.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            MyBase.LoadData(data)

            Me.m_ratios = New ArrayList

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "SplitRatios").SingleOrDefault.Elements.ToList
                m_ratios.Add(Double.Parse(xel.Value, ci))
            Next

            If Not GraphicObject Is Nothing Then
                OutCount = 0
                For Each cp In GraphicObject.OutputConnectors
                    If cp.IsAttached Then OutCount += 1
                Next
            End If

            Return True

        End Function

        ''' <summary>
        ''' Serializes the splitter state, including split ratios, into a list of XML elements.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current splitter configuration.</returns>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                .Add(New XElement("SplitRatios"))
                For Each d As Double In m_ratios
                    .Item(.Count - 1).Add(New XElement("SplitRatio", d.ToString(ci)))
                Next
            End With

            Return elements

        End Function

        ''' <summary>
        ''' Gets the collection of fractional split ratios assigned to each outlet stream.
        ''' Each value represents the fraction of the inlet mass flow directed to the corresponding outlet.
        ''' </summary>
        Public ReadOnly Property Ratios() As System.Collections.ArrayList
            Get
                Return Me.m_ratios
            End Get
        End Property

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Splitter"/> class with a specified name and description.
        ''' Default split ratios are set so that the first outlet receives 100% of the inlet flow.
        ''' </summary>
        ''' <param name="Name">The display name of this splitter object.</param>
        ''' <param name="Description">A short description of this splitter object.</param>
        Public Sub New(ByVal Name As String, ByVal Description As String)

            MyBase.CreateNew()
            Me.ComponentName = Name
            Me.ComponentDescription = Description
            Me.m_ratios.Add(1.0#)
            Me.m_ratios.Add(0.0#)
            Me.m_ratios.Add(0.0#)


        End Sub

        ''' <summary>
        ''' Initializes a new default instance of the <see cref="Splitter"/> class.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Performs post-calculation validation checks to verify the splitter results are physically consistent.
        ''' </summary>
        Public Overrides Sub PerformPostCalcValidation()
            MyBase.PerformPostCalcValidation()
        End Sub

        ''' <summary>
        ''' Executes the dynamic simulation model for the splitter, back-calculating the inlet stream
        ''' mass flow as the sum of the connected outlet stream mass flows and propagating inlet
        ''' thermodynamic state to all outlets.
        ''' </summary>
        Public Overrides Sub RunDynamicModel()

            OutCount = 0
            For Each cp In GraphicObject.OutputConnectors
                If cp.IsAttached Then OutCount += 1
            Next

            If OutCount = 1 Then

                GetInletMaterialStream(0).SetMassFlow(GetOutletMaterialStream(0).GetMassFlow)

                GetOutletMaterialStream(0).SetPressure(GetInletMaterialStream(0).GetPressure)
                GetOutletMaterialStream(0).SetTemperature(GetInletMaterialStream(0).GetTemperature)
                GetOutletMaterialStream(0).SetMassEnthalpy(GetInletMaterialStream(0).GetMassEnthalpy)
                GetOutletMaterialStream(0).AssignFromPhase(PhaseLabel.Mixture, GetInletMaterialStream(0), False)

            ElseIf OutCount = 2 Then

                GetInletMaterialStream(0).SetMassFlow(GetOutletMaterialStream(0).GetMassFlow + GetOutletMaterialStream(1).GetMassFlow)

                GetOutletMaterialStream(0).SetPressure(GetInletMaterialStream(0).GetPressure)
                GetOutletMaterialStream(0).SetTemperature(GetInletMaterialStream(0).GetTemperature)
                GetOutletMaterialStream(0).SetMassEnthalpy(GetInletMaterialStream(0).GetMassEnthalpy)
                GetOutletMaterialStream(0).AssignFromPhase(PhaseLabel.Mixture, GetInletMaterialStream(0), False)

                GetOutletMaterialStream(1).SetPressure(GetInletMaterialStream(0).GetPressure)
                GetOutletMaterialStream(1).SetTemperature(GetInletMaterialStream(0).GetTemperature)
                GetOutletMaterialStream(1).SetMassEnthalpy(GetInletMaterialStream(0).GetMassEnthalpy)
                GetOutletMaterialStream(1).AssignFromPhase(PhaseLabel.Mixture, GetInletMaterialStream(0), False)

            ElseIf OutCount = 3 Then

                GetInletMaterialStream(0).SetMassFlow(GetOutletMaterialStream(0).GetMassFlow + GetOutletMaterialStream(1).GetMassFlow + GetOutletMaterialStream(2).GetMassFlow)

                GetOutletMaterialStream(0).SetPressure(GetInletMaterialStream(0).GetPressure)
                GetOutletMaterialStream(0).SetTemperature(GetInletMaterialStream(0).GetTemperature)
                GetOutletMaterialStream(0).SetMassEnthalpy(GetInletMaterialStream(0).GetMassEnthalpy)
                GetOutletMaterialStream(0).AssignFromPhase(PhaseLabel.Mixture, GetInletMaterialStream(0), False)

                GetOutletMaterialStream(1).SetPressure(GetInletMaterialStream(0).GetPressure)
                GetOutletMaterialStream(1).SetTemperature(GetInletMaterialStream(0).GetTemperature)
                GetOutletMaterialStream(1).SetMassEnthalpy(GetInletMaterialStream(0).GetMassEnthalpy)
                GetOutletMaterialStream(1).AssignFromPhase(PhaseLabel.Mixture, GetInletMaterialStream(0), False)

                GetOutletMaterialStream(2).SetPressure(GetInletMaterialStream(0).GetPressure)
                GetOutletMaterialStream(2).SetTemperature(GetInletMaterialStream(0).GetTemperature)
                GetOutletMaterialStream(2).SetMassEnthalpy(GetInletMaterialStream(0).GetMassEnthalpy)
                GetOutletMaterialStream(2).AssignFromPhase(PhaseLabel.Mixture, GetInletMaterialStream(0), False)

            End If

        End Sub

        ''' <summary>
        ''' Executes the steady-state mass balance calculation for the splitter.
        ''' Distributes the inlet stream to the connected outlet streams according to the
        ''' active <see cref="OperationMode"/> (split ratios, mass flow spec, or mole flow spec).
        ''' Outlet streams inherit the inlet temperature, pressure, enthalpy, and composition.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used by this operation).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("The splitter is a mass balance unit operation - splits a 
                                    material stream into two or three other streams with different overall flow rates but with the same composition.")

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            OutCount = 0
            For Each cp In GraphicObject.OutputConnectors
                If cp.IsAttached Then OutCount += 1
            Next

            If OutCount > 0 And GetOutletMaterialStream(0) Is Nothing Or
            (OutCount > 1 And GetOutletMaterialStream(0) Is Nothing) Or
            (OutCount > 1 And GetOutletMaterialStream(1) Is Nothing) Then
                Throw New Exception("Outlet streams must be connected sequentially (first one to the first port, second one to the second port and so on)")
            End If

            Dim ems As Thermodynamics.Streams.MaterialStream = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)
            ems.Validate()
            Dim W As Double = ems.Phases(0).Properties.massflow.GetValueOrDefault
            Dim M As Double = ems.Phases(0).Properties.molarflow.GetValueOrDefault
            Dim V As Double = ems.Phases(0).Properties.volumetric_flow.GetValueOrDefault

            Dim i As Integer = 0
            Dim j As Integer = 0

            Dim ms As MaterialStream

            Select Case Me.OperationMode

                Case OpMode.SplitRatios

                    Select Case OutCount
                        Case 1
                            Ratios(0) = 1.0
                            Ratios(1) = 0.0
                            Ratios(2) = 0.0
                        Case 2
                            Ratios(1) = 1.0 - Ratios(0)
                            Ratios(2) = 0.0
                        Case 3
                            Ratios(2) = 1.0 - Ratios(0) - Ratios(1)
                    End Select

                    Dim cp As IConnectionPoint
                    For Each cp In Me.GraphicObject.OutputConnectors
                        If cp.IsAttached Then
                            ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                            With ms
                                .Phases(0).Properties.temperature = ems.Phases(0).Properties.temperature
                                .Phases(0).Properties.pressure = ems.Phases(0).Properties.pressure
                                .Phases(0).Properties.enthalpy = ems.Phases(0).Properties.enthalpy
                                Dim comp As BaseClasses.Compound
                                j = 0
                                For Each comp In .Phases(0).Compounds.Values
                                    comp.MoleFraction = ems.Phases(0).Compounds(comp.Name).MoleFraction.GetValueOrDefault
                                    comp.MassFraction = ems.Phases(0).Compounds(comp.Name).MassFraction.GetValueOrDefault
                                    j += 1
                                Next
                                .Phases(0).Properties.massflow = W * Me.Ratios(i)
                                .DefinedFlow = FlowSpec.Mass
                                .Phases(0).Properties.massfraction = 1
                                .Phases(0).Properties.molarfraction = 1
                                .SpecType = Interfaces.Enums.StreamSpec.Pressure_and_Enthalpy
                            End With
                        End If
                        i += 1
                    Next

                Case OpMode.StreamMassFlowSpec

                    Dim cp As IConnectionPoint
                    Dim w1, w2 As Double

                    Dim wn(OutCount) As Double

                    Select Case OutCount
                        Case 1
                            w1 = Me.StreamFlowSpec
                            wn(0) = w1
                        Case 2
                            If W >= Me.StreamFlowSpec Then
                                w1 = Me.StreamFlowSpec
                                wn(0) = w1
                                wn(1) = W - w1
                            Else
                                Throw New Exception(FlowSheet.GetTranslatedString("Ovalorinformadonovli"))
                            End If
                        Case 3
                            If W >= Me.StreamFlowSpec + Me.Stream2FlowSpec Then
                                w1 = Me.StreamFlowSpec
                                w2 = Me.Stream2FlowSpec
                                wn(0) = w1
                                wn(1) = w2
                                wn(2) = W - w1 - w2
                            Else
                                Throw New Exception(FlowSheet.GetTranslatedString("Ovalorinformadonovli"))
                            End If
                    End Select

                    i = 0
                    For Each cp In Me.GraphicObject.OutputConnectors
                        If cp.IsAttached Then
                            ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                            With ms
                                .Phases(0).Properties.temperature = ems.Phases(0).Properties.temperature
                                .Phases(0).Properties.pressure = ems.Phases(0).Properties.pressure
                                .Phases(0).Properties.enthalpy = ems.Phases(0).Properties.enthalpy
                                Dim comp As BaseClasses.Compound
                                j = 0
                                For Each comp In .Phases(0).Compounds.Values
                                    comp.MoleFraction = ems.Phases(0).Compounds(comp.Name).MoleFraction.GetValueOrDefault
                                    comp.MassFraction = ems.Phases(0).Compounds(comp.Name).MassFraction.GetValueOrDefault
                                    j += 1
                                Next
                                .Phases(0).Properties.massflow = wn(i)
                                .DefinedFlow = FlowSpec.Mass
                                .Phases(0).Properties.massfraction = 1.0
                                .Phases(0).Properties.molarfraction = 1.0
                                .SpecType = Interfaces.Enums.StreamSpec.Pressure_and_Enthalpy
                            End With
                        End If
                        i += 1
                    Next

                Case OpMode.StreamMoleFlowSpec

                    Dim cp As IConnectionPoint
                    Dim m1, m2 As Double

                    Dim mn(OutCount) As Double

                    Select Case OutCount
                        Case 1
                            m1 = m1
                            mn(0) = m1
                        Case 2
                            If M >= Me.StreamFlowSpec Then
                                m1 = Me.StreamFlowSpec
                                mn(0) = m1
                                mn(1) = M - m1
                            Else
                                Throw New Exception(FlowSheet.GetTranslatedString("Ovalorinformadonovli"))
                            End If
                        Case 3
                            If M >= Me.StreamFlowSpec + Me.Stream2FlowSpec Then
                                m1 = Me.StreamFlowSpec
                                m2 = Me.Stream2FlowSpec
                                mn(0) = m1
                                mn(1) = m2
                                mn(2) = M - m1 - m2
                            Else
                                Throw New Exception(FlowSheet.GetTranslatedString("Ovalorinformadonovli"))
                            End If

                    End Select

                    i = 0
                    For Each cp In Me.GraphicObject.OutputConnectors
                        If cp.IsAttached Then
                            ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                            With ms
                                .Phases(0).Properties.temperature = ems.Phases(0).Properties.temperature
                                .Phases(0).Properties.pressure = ems.Phases(0).Properties.pressure
                                .Phases(0).Properties.enthalpy = ems.Phases(0).Properties.enthalpy
                                Dim comp As BaseClasses.Compound
                                j = 0
                                For Each comp In .Phases(0).Compounds.Values
                                    comp.MoleFraction = ems.Phases(0).Compounds(comp.Name).MoleFraction.GetValueOrDefault
                                    comp.MassFraction = ems.Phases(0).Compounds(comp.Name).MassFraction.GetValueOrDefault
                                    j += 1
                                Next
                                .Phases(0).Properties.molarflow = mn(i)
                                .DefinedFlow = FlowSpec.Mole
                                .Phases(0).Properties.massfraction = 1.0
                                .Phases(0).Properties.molarfraction = 1.0
                                .SpecType = Interfaces.Enums.StreamSpec.Pressure_and_Enthalpy
                            End With
                        End If
                        i += 1
                    Next

                Case OpMode.StreamVolumetricFlowSpec

                    Dim cp As IConnectionPoint
                    Dim v1, v2 As Double

                    Dim vn(OutCount) As Double

                    Select Case OutCount
                        Case 1
                            v1 = v1
                            vn(0) = v1
                        Case 2
                            If V >= Me.StreamFlowSpec Then
                                v1 = Me.StreamFlowSpec
                                vn(0) = v1
                                vn(1) = V - v1
                            Else
                                Throw New Exception(FlowSheet.GetTranslatedString("Ovalorinformadonovli"))
                            End If
                        Case 3
                            If V >= Me.StreamFlowSpec + Me.Stream2FlowSpec Then
                                v1 = Me.StreamFlowSpec
                                v2 = Me.Stream2FlowSpec
                                vn(0) = v1
                                vn(1) = v2
                                vn(2) = V - v1 - v2
                            Else
                                Throw New Exception(FlowSheet.GetTranslatedString("Ovalorinformadonovli"))
                            End If

                    End Select

                    i = 0
                    For Each cp In Me.GraphicObject.OutputConnectors
                        If cp.IsAttached Then
                            ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                            With ms
                                .Phases(0).Properties.temperature = ems.Phases(0).Properties.temperature
                                .Phases(0).Properties.pressure = ems.Phases(0).Properties.pressure
                                .Phases(0).Properties.enthalpy = ems.Phases(0).Properties.enthalpy
                                Dim comp As BaseClasses.Compound
                                j = 0
                                For Each comp In .Phases(0).Compounds.Values
                                    comp.MoleFraction = ems.Phases(0).Compounds(comp.Name).MoleFraction.GetValueOrDefault
                                    comp.MassFraction = ems.Phases(0).Compounds(comp.Name).MassFraction.GetValueOrDefault
                                    j += 1
                                Next
                                .SetVolumetricFlow(vn(i))
                                .DefinedFlow = FlowSpec.Volumetric
                                .Phases(0).Properties.massfraction = 1.0
                                .Phases(0).Properties.molarfraction = 1.0
                                .SpecType = Interfaces.Enums.StreamSpec.Pressure_and_Enthalpy
                            End With
                        End If
                        i += 1
                    Next

            End Select

            IObj?.Close()

        End Sub

        ''' <summary>
        ''' Resets all outlet stream properties to their default (uncalculated) state,
        ''' clearing temperatures, pressures, enthalpies, flow rates, and compositions.
        ''' </summary>
        Public Overrides Sub DeCalculate()

            Dim i As Integer = 0
            Dim j As Integer = 0

            Dim ms As MaterialStream
            Dim cp As IConnectionPoint
            For Each cp In Me.GraphicObject.OutputConnectors
                If cp.IsAttached Then
                    ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                    j = 0
                    With ms
                        .Phases(0).Properties.temperature = Nothing
                        .Phases(0).Properties.pressure = Nothing
                        .Phases(0).Properties.enthalpy = Nothing
                        Dim comp As BaseClasses.Compound
                        For Each comp In .Phases(0).Compounds.Values
                            comp.MoleFraction = 0
                            comp.MassFraction = 0
                            j += 1
                        Next
                        .Phases(0).Properties.massflow = Nothing
                        .Phases(0).Properties.massfraction = 1
                        .Phases(0).Properties.molarfraction = 1
                    End With
                End If
                i += 1
            Next

        End Sub

        ''' <summary>
        ''' Returns the current value of a named splitter property, converting from SI units to the supplied unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string (e.g. "PROP_SP_1", "SR1").</param>
        ''' <param name="su">The target unit system for the returned value. Defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>, or the base-class value if the property is not handled here.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As Double = 0
                Select Case prop
                    Case "PROP_SP_1"
                        If Me.OperationMode = OpMode.StreamMassFlowSpec Then
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.massflow, Me.StreamFlowSpec)
                        ElseIf Me.OperationMode = OpMode.StreamMoleFlowSpec Then
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, Me.StreamFlowSpec)
                        Else
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.volumetricFlow, Me.StreamFlowSpec)
                        End If
                    Case "PROP_SP_2"
                        If Me.OperationMode = OpMode.StreamMassFlowSpec Then
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.massflow, Me.Stream2FlowSpec)
                        ElseIf Me.OperationMode = OpMode.StreamMoleFlowSpec Then
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, Me.Stream2FlowSpec)
                        Else
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.volumetricFlow, Me.Stream2FlowSpec)
                        End If
                    Case "SR1"
                        If Me.Ratios.Count > 0 Then value = Me.Ratios(0)
                    Case "SR2"
                        If Me.Ratios.Count > 1 Then value = Me.Ratios(1)
                    Case "SR3"
                        If Me.Ratios.Count > 2 Then value = Me.Ratios(2)
                End Select
                Return value
            End If
        End Function

        ''' <summary>
        ''' Returns the list of property identifiers available for this splitter, filtered by the requested access type.
        ''' Split ratio properties (SR1, SR2, SR3) are included according to the number of connected outlets.
        ''' </summary>
        ''' <param name="proptype">The access-type filter (<see cref="Interfaces.Enums.PropertyType"/>) such as read-write, write-only, or all.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)

            proplist.Add("PROP_SP_1")
            proplist.Add("PROP_SP_2")

            If GraphicObject IsNot Nothing Then
                OutCount = 0
                For Each cp In GraphicObject.OutputConnectors
                    If cp.IsAttached Then OutCount += 1
                Next
            End If

            Select Case proptype
                Case PropertyType.RW
                    For i = 1 To OutCount - 1
                        proplist.Add("SR" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 1 To OutCount - 1
                        proplist.Add("SR" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 1 To OutCount
                        proplist.Add("SR" + CStr(i))
                    Next
                Case PropertyType.RO
                    proplist.Add("SR" + CStr(OutCount))
            End Select

            Return proplist.ToArray(GetType(System.String))
        End Function

        ''' <summary>
        ''' Sets the value of a named splitter property, converting from the supplied unit system to SI units.
        ''' </summary>
        ''' <param name="prop">The property identifier string (e.g. "PROP_SP_1", "SR1", "SR2").</param>
        ''' <param name="propval">The new property value in the units of <paramref name="su"/>.</param>
        ''' <param name="su">The unit system of the supplied value. Defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If GraphicObject IsNot Nothing Then
                OutCount = 0
                For Each cp In GraphicObject.OutputConnectors
                    If cp.IsAttached Then OutCount += 1
                Next
            End If

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Select Case prop
                Case "PROP_SP_1"
                    If Me.OperationMode = OpMode.StreamMassFlowSpec Then
                        Me.StreamFlowSpec = SystemsOfUnits.Converter.ConvertToSI(su.massflow, propval)
                    ElseIf Me.OperationMode = OpMode.StreamMoleFlowSpec Then
                        Me.StreamFlowSpec = SystemsOfUnits.Converter.ConvertToSI(su.molarflow, propval)
                    Else
                        Me.StreamFlowSpec = SystemsOfUnits.Converter.ConvertToSI(su.volumetricFlow, propval)
                    End If
                Case "PROP_SP_2"
                    If Me.OperationMode = OpMode.StreamMassFlowSpec Then
                        Me.Stream2FlowSpec = SystemsOfUnits.Converter.ConvertToSI(su.massflow, propval)
                    ElseIf Me.OperationMode = OpMode.StreamMoleFlowSpec Then
                        Me.Stream2FlowSpec = SystemsOfUnits.Converter.ConvertToSI(su.molarflow, propval)
                    Else
                        Me.Stream2FlowSpec = SystemsOfUnits.Converter.ConvertToSI(su.volumetricFlow, propval)
                    End If
                Case "SR1"
                    If propval >= 0 And propval <= 1 Then
                        Me.Ratios(0) = propval
                        If OutCount = 2 Then Me.Ratios(1) = 1 - propval
                        If OutCount = 3 And Ratios(0) + Ratios(1) <= 1 Then Me.Ratios(2) = 1 - Me.Ratios(0) - Me.Ratios(1)
                    End If
                Case "SR2"
                    If propval >= 0 And propval <= 1 And Me.Ratios(0) + Me.Ratios(1) + propval <= 1 And OutCount = 3 Then
                        Me.Ratios(1) = propval
                        Me.Ratios(2) = 1 - Me.Ratios(0) - Me.Ratios(1)
                    End If
            End Select
            Return 1
        End Function

        ''' <summary>
        ''' Returns the unit string for a named splitter property under the supplied unit system.
        ''' Flow specification properties return mass or molar flow units depending on the active operation mode.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use. Defaults to SI if not provided.</param>
        ''' <returns>A unit string, or an empty string for dimensionless properties such as split ratios.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim value As String = ""
                If prop.StartsWith("P") Then
                    Select Case Me.OperationMode
                        Case OpMode.StreamMassFlowSpec
                            value = su.massflow
                        Case OpMode.StreamMoleFlowSpec
                            value = su.molarflow
                        Case OpMode.StreamVolumetricFlowSpec
                            value = su.volumetricFlow
                    End Select
                Else
                    value = ""
                End If
                Return value
            End If
        End Function

        ''' <summary>
        ''' Returns the raw bytes of the splitter icon image, suitable for cross-platform rendering.
        ''' </summary>
        ''' <returns>A byte array containing the PNG image data for the splitter icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.splitter.png")

        End Function

        ''' <summary>
        ''' Returns the localized description string for the splitter unit operation.
        ''' </summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("SPLIT_Desc")
        End Function

        ''' <summary>
        ''' Returns the localized display name for the splitter unit operation.
        ''' </summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("SPLIT_Name")
        End Function

        ''' <summary>
        ''' Gets a value indicating whether this unit operation is compatible with the DWSIM mobile platform.
        ''' </summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Returns a human-readable description for a named splitter property, suitable for display in the property editor.
        ''' </summary>
        ''' <param name="p">The property display name (e.g. "Specification", "Split Ratio Stream 1").</param>
        ''' <returns>A descriptive string explaining the purpose and usage of the property.</returns>
        Public Overrides Function GetPropertyDescription(p As String) As String
            If p.Equals("Specification") Then
                Return "Define how you will specify this splitter block."
            ElseIf p.Equals("Split Ratio Stream 1") Then
                Return "If you chose 'Split Ratios' as the specification mode, enter the fraction of the inlet mass flow that will be directed to the outlet stream 1."
            ElseIf p.Equals("Split Ratio Stream 2") Then
                Return "If you chose 'Split Ratios' as the specification mode, enter the fraction of the inlet mass flow that will be directed to the outlet stream 2."
            ElseIf p.Equals("Split Ratio Stream 3") Then
                Return "If you chose 'Split Ratios' as the specification mode and have 3 outlet streams connected to this splitter, enter the fraction of the inlet mass flow that will be directed to the outlet stream 3."
            ElseIf p.Equals("Stream 1 Mass/Mole Flow Spec") Then
                Return "If you chose a Flow Spec as the specification mode, enter the flow amount of the stream 1. If only two outlet streams are connected, you don't need to specify a flow amount for the stream 2 as it will be calculated to close the mass balance."
            ElseIf p.Equals("Stream 2 Mass/Mole Flow Spec") Then
                Return "If you chose a Flow Spec as the specification mode, enter the flow amount of the stream 2. This is required only if you have 3 outlet streams connected to this splitter."
            Else
                Return p
            End If
        End Function

    End Class

End Namespace
