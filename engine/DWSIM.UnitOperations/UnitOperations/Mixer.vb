'    Mixer Calculation Routines 
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
    ''' Represents a mixer unit operation that combines up to six material inlet streams into one outlet
    ''' stream by performing mass and energy balances. Outlet pressure is determined by the configured
    ''' <see cref="PressureBehavior"/> (minimum, maximum, or average of inlet pressures).
    ''' </summary>
    <System.Serializable()> Public Partial Class Mixer

        Inherits UnitOperations.UnitOpBaseClass

        ''' <summary>Gets or sets the simulation object class category (MixersSplitters).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.MixersSplitters

        ''' <summary>Gets a value indicating whether this unit operation supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this unit operation exposes properties for dynamic mode.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Defines how the outlet pressure is computed from the inlet stream pressures.</summary>
        Public Enum PressureBehavior
            ''' <summary>Outlet pressure is the arithmetic average of all inlet pressures.</summary>
            Average
            ''' <summary>Outlet pressure equals the highest inlet pressure.</summary>
            Maximum
            ''' <summary>Outlet pressure equals the lowest inlet pressure.</summary>
            Minimum
        End Enum

        Protected m_pressurebehavior As PressureBehavior = PressureBehavior.Minimum

        ''' <summary>Gets or sets the rule used to determine the outlet stream pressure from the inlet stream pressures.</summary>
        Public Property PressureCalculation() As PressureBehavior
            Get
                Return Me.m_pressurebehavior
            End Get
            Set(ByVal value As PressureBehavior)
                Me.m_pressurebehavior = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="Mixer"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Mixer"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the mixer.</param>
        ''' <param name="description">A brief description of the mixer.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>Creates a deep copy of this mixer via XML serialization.</summary>
        ''' <returns>A new <see cref="Mixer"/> instance with the same data.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Mixer()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this mixer via JSON serialization.</summary>
        ''' <returns>A new <see cref="Mixer"/> instance with the same data.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Mixer)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Executes the mixer calculation in dynamic simulation mode.</summary>
        Public Overrides Sub RunDynamicModel()

            Calculate()

        End Sub

        ''' <summary>
        ''' Performs the mixer mass and energy balance: combines all connected inlet streams into the
        ''' single outlet stream using the selected pressure rule and a PH flash to determine outlet temperature.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("The Mixer is used to mix up to six material streams into one, while executing all the mass and energy balances.")

            IObj?.Paragraphs.Add("The mixer does the mass balance in the equipment and determines 
                                the mass flow and the composition of the outlet stream. Pressure 
                                is calculated according to the parameter defined by the user. 
                                Temperature is calculated by doing a PH Flash in the outlet 
                                stream, with the enthalpy calculated from the inlet streams 
                                (energy balance).")

            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Nohcorrentedematriac6"))
            End If

            Me.PropertyPackage.CurrentMaterialStream = Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)

            Dim H, Hs, T, W, We, P As Double
            H = 0
            Hs = 0
            T = 0
            W = 0
            We = 0
            P = 0
            Dim i As Integer = 1
            Dim ms As MaterialStream
            Dim cp As IConnectionPoint
            For Each cp In Me.GraphicObject.InputConnectors
                If cp.IsAttached Then
                    IObj?.Paragraphs.Add(String.Format("<h3>Inlet Stream #{0}</h3>", i))
                    If cp.AttachedConnector.AttachedFrom.Calculated = False Then Throw New Exception(FlowSheet.GetTranslatedString("Umaoumaiscorrentesna"))
                    ms = Me.FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedFrom.Name)
                    ms.Validate()
                    If Me.PressureCalculation = PressureBehavior.Minimum Then
                        If ms.Phases(0).Properties.pressure.GetValueOrDefault < P Then
                            P = ms.Phases(0).Properties.pressure
                        ElseIf P = 0 Then
                            P = ms.Phases(0).Properties.pressure
                        End If
                    ElseIf Me.PressureCalculation = PressureBehavior.Maximum Then
                        If ms.Phases(0).Properties.pressure.GetValueOrDefault > P Then
                            P = ms.Phases(0).Properties.pressure
                        ElseIf P = 0 Then
                            P = ms.Phases(0).Properties.pressure
                        End If
                    Else
                        P = P + ms.Phases(0).Properties.pressure.GetValueOrDefault
                        i += 1
                    End If
                    IObj?.Paragraphs.Add(String.Format("Mass Flow: {0} kg/s", ms.Phases(0).Properties.massflow.GetValueOrDefault))
                    IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", ms.Phases(0).Properties.pressure.GetValueOrDefault))
                    IObj?.Paragraphs.Add(String.Format("Enthalpy: {0} kJ/kg", ms.Phases(0).Properties.enthalpy.GetValueOrDefault))
                    We = ms.Phases(0).Properties.massflow.GetValueOrDefault
                    W += We
                    If Not Double.IsNaN(ms.Phases(0).Properties.enthalpy.GetValueOrDefault) Then H += We * ms.Phases(0).Properties.enthalpy.GetValueOrDefault
                End If
            Next

            Dim SS As MaterialStream, isSS As Boolean

            isSS = GraphicObject.InputConnectors.Where(Function(c) c.IsAttached).
                Where(Function(c2) DirectCast(c2.AttachedConnector.AttachedFrom.Owner, MaterialStream).GetMassFlow() > 0).Count = 1

            If isSS Then

                SS = GraphicObject.InputConnectors.Where(Function(c) c.IsAttached).
                Select(Function(c2) DirectCast(c2.AttachedConnector.AttachedFrom.Owner, MaterialStream)).
                Where(Function(c3) c3.GetMassFlow() > 0).SingleOrDefault()

                GetOutletMaterialStream(0).AssignFromPhase(PhaseLabel.Mixture, SS, True)
                GetOutletMaterialStream(0).AtEquilibrium = False

            Else

                If W <> 0.0# Then Hs = H / W Else Hs = 0.0#

                If Me.PressureCalculation = PressureBehavior.Average Then P = P / (i - 1)

                'propagate pressure backwards in dynamic mode

                If FlowSheet.DynamicMode Then
                    P = GetOutletMaterialStream(0).GetPressure()
                    For Each cp In Me.GraphicObject.InputConnectors
                        If cp.IsAttached Then
                            ms = Me.FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedFrom.Name)
                            ms.SetPressure(P)
                        End If
                    Next
                End If

                T = 0.0

                Dim n As Integer = DirectCast(Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name), MaterialStream).Phases(0).Compounds.Count
                Dim Vw As New Dictionary(Of String, Double)
                For Each cp In Me.GraphicObject.InputConnectors
                    If cp.IsAttached Then
                        ms = Me.FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedFrom.Name)
                        Dim comp As BaseClasses.Compound
                        For Each comp In ms.Phases(0).Compounds.Values
                            If Not Vw.ContainsKey(comp.Name) Then
                                Vw.Add(comp.Name, 0)
                            End If
                            Vw(comp.Name) += comp.MassFraction.GetValueOrDefault * ms.Phases(0).Properties.massflow.GetValueOrDefault
                        Next
                        If W <> 0.0# Then T += ms.Phases(0).Properties.massflow.GetValueOrDefault / W * ms.Phases(0).Properties.temperature.GetValueOrDefault
                    End If
                Next

                If W = 0.0# Then T = 273.15

                IObj?.Paragraphs.Add("Outlet Mixed Stream</h3>")

                IObj?.Paragraphs.Add(String.Format("Mass Flow: {0} kg/s", W))
                IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
                IObj?.Paragraphs.Add(String.Format("Enthalpy: {0} kJ/kg", Hs))

                Dim omstr As MaterialStream = Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
                With omstr
                    .Clear()
                    .ClearAllProps()
                    If W <> 0.0# Then .Phases(0).Properties.enthalpy = Hs
                    .Phases(0).Properties.pressure = P
                    .Phases(0).Properties.massflow = W
                    .DefinedFlow = FlowSpec.Mass
                    .Phases(0).Properties.molarfraction = 1
                    .Phases(0).Properties.massfraction = 1
                    Dim comp As BaseClasses.Compound
                    For Each comp In .Phases(0).Compounds.Values
                        If W <> 0.0# Then comp.MassFraction = Vw(comp.Name) / W
                    Next
                    Dim mass_div_mm As Double = 0
                    Dim sub1 As BaseClasses.Compound
                    For Each sub1 In .Phases(0).Compounds.Values
                        mass_div_mm += sub1.MassFraction.GetValueOrDefault / sub1.ConstantProperties.Molar_Weight
                    Next
                    For Each sub1 In .Phases(0).Compounds.Values
                        If W <> 0.0# Then
                            sub1.MoleFraction = sub1.MassFraction.GetValueOrDefault / sub1.ConstantProperties.Molar_Weight / mass_div_mm
                        Else
                            sub1.MoleFraction = 0.0#
                        End If
                    Next
                    .Phases(0).Properties.temperature = T
                    .SpecType = Interfaces.Enums.StreamSpec.Pressure_and_Enthalpy
                End With

                IObj?.Close()

            End If

        End Sub

        ''' <summary>Resets the outlet material stream to an uncalculated state.</summary>
        Public Overrides Sub DeCalculate()

            If Me.GraphicObject.OutputConnectors(0).IsAttached Then
                With DirectCast(Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name), Thermodynamics.Streams.MaterialStream)
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.molarfraction = 1
                    Dim comp As BaseClasses.Compound
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .Phases(0).Properties.molarflow = Nothing
                    .GraphicObject.Calculated = False
                End With
            End If

        End Sub

        ''' <summary>Returns the value of the specified property.</summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                Return 0
            End If

        End Function

        ''' <summary>Returns the list of property identifiers available for this mixer.</summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>Sets the value of the specified property; delegates to the base implementation.</summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new property value.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> always.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Return 0

        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>A unit string, or an empty string if the property has no units.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                Return 0
            End If
        End Function

        ''' <summary>Returns the raw bytes of the mixer icon image resource.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.mixer.png")

        End Function

        ''' <summary>Returns the localized display description for the mixer type.</summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("MIX_Desc")
        End Function

        ''' <summary>Returns the localized display name for the mixer type.</summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("MIX_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with the DWSIM mobile interface.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Namespace
