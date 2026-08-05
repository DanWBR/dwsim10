'    Solids Separator Calculation Routines 
'    Copyright 2013 Daniel Wagner O. de Medeiros
'    Copyright 2021 Gregor Reichert
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
    ''' Represents a solids separator unit operation that removes solid-phase material from a
    ''' mixed material stream. Liquid and vapour phases are sent to the first outlet while the
    ''' solid phase is directed to the second outlet, each split according to user-specified
    ''' separation efficiencies.
    ''' </summary>
    <System.Serializable()> Public Partial Class SolidsSeparator

        Inherits UnitOperations.UnitOpBaseClass

        ''' <summary>Gets or sets the simulation object class category for this unit operation (Solids).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Solids

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets or sets Base64-encoded embedded image data used when a custom icon is displayed.</summary>
        Public Property EmbeddedImageData As String = ""

        ''' <summary>Gets or sets whether an embedded image is used as the custom icon for this unit operation.</summary>
        Public Property UseEmbeddedImage As Boolean = False

        ''' <summary>Loads the object state from a list of XML elements.</summary>
        ''' <param name="data">The XML element list containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean
            Return MyBase.LoadData(data)
        End Function

        ''' <summary>Saves the object state to a list of XML elements for serialization.</summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            Return elements

        End Function

        ''' <summary>Gets or sets the solid-phase separation efficiency as a percentage (0–100).</summary>
        Public Property SeparationEfficiency() As Double = 100.0#

        ''' <summary>Gets or sets the liquid-phase separation efficiency as a percentage (0–100). Determines how much liquid leaks to the solids outlet.</summary>
        Public Property LiquidSeparationEfficiency() As Double = 100.0#

        ''' <summary>Performs post-calculation validation (no-op for this unit operation).</summary>
        Public Overrides Sub PerformPostCalcValidation()

        End Sub

        ''' <summary>Initializes a new default instance of the <see cref="SolidsSeparator"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="SolidsSeparator"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the solids separator.</param>
        ''' <param name="description">A brief description of the solids separator.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>Creates a deep copy of this solids separator via XML serialization.</summary>
        ''' <returns>A new <see cref="SolidsSeparator"/> instance with the same state.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New SolidsSeparator()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this solids separator via JSON serialization.</summary>
        ''' <returns>A new <see cref="SolidsSeparator"/> instance with the same state.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of SolidsSeparator)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = True

        Public Overrides Sub CreateDynamicProperties()

            AddDynamicProperty("Solids Holdup", "Current accumulated solids mass (kg).", 0.0, UnitOfMeasure.mass, 1.0.GetType())
            AddDynamicProperty("Maximum Solids Holdup", "Maximum solids capacity before discharge (kg). Set to 0 for continuous discharge.", 0.0, UnitOfMeasure.mass, 1.0.GetType())
            AddDynamicProperty("Discharge Active", "True when solids are being discharged.", False, UnitOfMeasure.none, True.GetType())

        End Sub

        Public Overrides Sub RunDynamicModel()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds
            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0

            Dim solidsHoldup As Double = GetDynamicProperty("Solids Holdup")
            Dim maxHoldup As Double = GetDynamicProperty("Maximum Solids Holdup")

            Dim ims As MaterialStream = Me.GetInletMaterialStream(0)
            Dim liquidOut As MaterialStream = Me.GetOutletMaterialStream(0)
            Dim solidsOut As MaterialStream = Me.GetOutletMaterialStream(1)

            Dim Wsin = ims.Phases(7).Properties.massflow.GetValueOrDefault()
            Dim capturedSolids = Wsin * SeparationEfficiency / 100.0
            solidsHoldup += capturedSolids * timestep

            Dim discharge As Boolean = False
            If maxHoldup > 0 Then
                If solidsHoldup >= maxHoldup Then
                    discharge = True
                    solidsHoldup = 0.0
                End If
            Else
                discharge = True
            End If

            SetDynamicProperty("Discharge Active", discharge)
            SetDynamicProperty("Solids Holdup", solidsHoldup)

            Calculate()

        End Sub

        ''' <summary>
        ''' Performs the steady-state calculation for the solids separator: splits inlet stream
        ''' phases to their respective outlets based on the configured separation efficiencies.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("The solids separator is used to separate solids from a liquid phase in a mixed material stream. 
                                  <br><br>Liquid and vapor phases are sent into outlet 1 and solid phase into outlet 2. 
                                  <br>The solid and liquid phases are split between both outlets according to specified efficiencies. The vapor phase is always sent to outlet 1 completely.")

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            ElseIf Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            ElseIf Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            Dim instr, outstr1, outstr2 As MaterialStream
            instr = GetInletMaterialStream(0)
            outstr1 = GetOutletMaterialStream(0)
            outstr2 = GetOutletMaterialStream(1)

            Dim W As Double = instr.Phases(0).Properties.massflow.GetValueOrDefault
            Dim Wsin As Double = instr.Phases(7).Properties.massflow.GetValueOrDefault
            Dim Wlin As Double = instr.Phases(1).Properties.massflow.GetValueOrDefault
            Dim Wvin As Double = instr.Phases(2).Properties.massflow.GetValueOrDefault
            Dim HVin As Double = instr.Phases(2).Properties.enthalpy.GetValueOrDefault
            Dim HLin As Double = instr.Phases(1).Properties.enthalpy.GetValueOrDefault
            Dim HSin As Double = instr.Phases(7).Properties.enthalpy.GetValueOrDefault

            Dim sse, lse As Double
            sse = Me.SeparationEfficiency / 100
            lse = Me.LiquidSeparationEfficiency / 100
            Dim Wsout As Double = sse * Wsin + (1 - lse) * Wlin
            Dim Wlvout As Double = (1 - sse) * Wsin + lse * Wlin + Wvin

            IObj?.Paragraphs.Add("<hr><h3>Input Variables</h3>")
            IObj?.Paragraphs.Add(String.Format("<b><i>Solid separation efficiency:</i></b> {0} <br><b><i>Liquid separation efficiency:</i></b> {1}", sse, lse))
            IObj?.Paragraphs.Add(String.Format("<b><i>Solid mass flow:</i></b> {0} Kg/s <br><b><i>Solid phase enthalpy:</i></b> {1} KJ/Kg", Wsin, HSin))
            IObj?.Paragraphs.Add(String.Format("<b><i>Liquid mass flow:</i></b> {0} Kg/s <br><b><i>Liquid phase enthalpy:</i></b> {1} KJ/Kg", Wlin, HLin))
            IObj?.Paragraphs.Add(String.Format("<b><i>Vapor mass flow:</i></b> {0} Kg/s <br><b><i>Vapor phase enthalpy:</i></b> {1} KJ/Kg", Wvin, HVin))

            Dim mw As Double

            Dim cp As IConnectionPoint

            cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                With outstr1
                    .AtEquilibrium = False
                    .ClearAllProps()
                    .Phases(0).Properties.massflow = Wlvout
                    .DefinedFlow = FlowSpec.Mass
                    Dim comp As BaseClasses.Compound
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MassFlow = (1 - sse) * instr.Phases(7).Compounds(comp.Name).MassFlow + instr.Phases(2).Compounds(comp.Name).MassFlow
                        comp.MassFlow += lse * (instr.Phases(3).Compounds(comp.Name).MassFlow + instr.Phases(4).Compounds(comp.Name).MassFlow)
                        comp.MassFraction = comp.MassFlow / Wlvout
                    Next
                    mw = 0.0#
                    For Each comp In .Phases(0).Compounds.Values
                        mw += comp.MassFraction / comp.ConstantProperties.Molar_Weight
                    Next
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = comp.MassFraction / comp.ConstantProperties.Molar_Weight / mw
                    Next
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MolarFlow = comp.MassFlow / comp.ConstantProperties.Molar_Weight / 1000
                    Next
                End With
            End If

            cp = Me.GraphicObject.OutputConnectors(1)
            If cp.IsAttached Then
                With outstr2
                    .AtEquilibrium = False
                    .ClearAllProps()
                    .Phases(0).Properties.massflow = Wsout
                    .DefinedFlow = FlowSpec.Mass
                    Dim comp As BaseClasses.Compound
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MassFlow = sse * instr.Phases(7).Compounds(comp.Name).MassFlow.GetValueOrDefault + (1 - lse) * (instr.Phases(3).Compounds(comp.Name).MassFlow + instr.Phases(4).Compounds(comp.Name).MassFlow)
                        comp.MassFraction = If(Wsout > 0.0#, comp.MassFlow / Wsout, 0.0#)
                    Next
                    mw = 0.0#
                    For Each comp In .Phases(0).Compounds.Values
                        mw += comp.MassFraction / comp.ConstantProperties.Molar_Weight
                    Next
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = If(mw > 0.0#, comp.MassFraction / comp.ConstantProperties.Molar_Weight / mw, 0.0#)
                    Next
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MolarFlow = comp.MassFlow / comp.ConstantProperties.Molar_Weight / 1000
                    Next
                End With
            End If

            'pass conditions

            outstr1.Phases(0).Properties.temperature = instr.Phases(0).Properties.temperature.GetValueOrDefault
            outstr1.Phases(0).Properties.pressure = instr.Phases(0).Properties.pressure.GetValueOrDefault
            outstr2.Phases(0).Properties.temperature = instr.Phases(0).Properties.temperature.GetValueOrDefault
            outstr2.Phases(0).Properties.pressure = instr.Phases(0).Properties.pressure.GetValueOrDefault

            outstr1.Phases(0).Properties.enthalpy = (HVin * Wvin + HLin * Wlin * lse + HSin * Wsin * (1 - sse)) / Wlvout
            outstr2.Phases(0).Properties.enthalpy = (HSin * Wsin * sse + HLin * Wlin * (1 - lse)) / Wsout
            outstr1.SpecType = StreamSpec.Pressure_and_Enthalpy
            outstr2.SpecType = StreamSpec.Pressure_and_Enthalpy

            IObj?.Paragraphs.Add("<hr><h3>Results</h3>")
            IObj?.Paragraphs.Add(String.Format("Flash specs of outlet streams are set to PH. Enthalpies are defined to maintain phase fractions."))
            IObj?.Paragraphs.Add(String.Format("<b><i>Massflow Outlet 1</i></b>: {0} Kg/s <br><b><i>Enthalpy Outlet 1</i></b>: {1} KJ/Kg", Wlvout, outstr1.Phases(0).Properties.enthalpy))
            IObj?.Paragraphs.Add(String.Format("<b><i>Massflow Outlet 2</i></b>: {0} Kg/s <br><b><i>Enthalpy Outlet 2</i></b>: {1} KJ/Kg", Wsout, outstr2.Phases(0).Properties.enthalpy))

            IObj?.Close()

        End Sub

        ''' <summary>Clears the calculated results from the outlet material streams.</summary>
        Public Overrides Sub DeCalculate()

            Dim j As Integer = 0

            Dim cp As IConnectionPoint

            cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                With GetOutletMaterialStream(0)
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
                With GetOutletMaterialStream(1)
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
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">Optional unit system for conversion.</param>
        ''' <returns>The property value.</returns>
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
                    Case 1
                        value = Me.SeparationEfficiency
                    Case 2
                        value = Me.LiquidSeparationEfficiency
                End Select

                Return value
            End If

        End Function

        ''' <summary>Returns an array of property identifier strings for the specified property type.</summary>
        ''' <param name="proptype">The category of properties to list (read-write, read-only, all).</param>
        ''' <returns>A string array of property identifiers.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RW
                    'For i = 0 To 0
                    '    proplist.Add("PROP_SS_" + CStr(i))
                    'Next
                Case PropertyType.WR
                    For i = 1 To 2
                        proplist.Add("PROP_SS_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 1 To 2
                        proplist.Add("PROP_SS_" + CStr(i))
                    Next
                Case PropertyType.RO
                    'For i = 0 To 0
                    '    proplist.Add("PROP_SS_" + CStr(i))
                    'Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>Sets the value of the specified property from the given value and unit system.</summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new value to assign.</param>
        ''' <param name="su">Optional unit system for conversion.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx
                Case 1
                    'PROP_SS_1	Solid Separation Efficiency
                    Me.SeparationEfficiency = propval
                Case 2
                    'PROP_SS_2	Liquid Separation Efficiency
                    Me.LiquidSeparationEfficiency = propval
            End Select

            Return 1

        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">Optional unit system.</param>
        ''' <returns>The unit string (e.g., "%").</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                'If su Is Nothing Then su = New SystemsOfUnits.SI
                'Dim cv As New SystemsOfUnits.Converter
                Dim value As String = "%"
                'Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Return value
            End If
        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        ''' <returns>Byte array containing the PNG image data.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.solids_separator.png")

        End Function

        ''' <summary>Returns the localised display description for this unit operation.</summary>
        ''' <returns>A localised description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("SSEP_Desc")
        End Function

        ''' <summary>Returns the localised display name for this unit operation.</summary>
        ''' <returns>A localised name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("SSEP_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile/cross-platform interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace
