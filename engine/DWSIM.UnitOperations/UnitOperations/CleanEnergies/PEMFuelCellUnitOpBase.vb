Imports System.IO
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.UnitOperations.UnitOperations
Imports Python.Runtime
Imports SkiaSharp

Namespace UnitOperations.Auxiliary

    ''' <summary>
    ''' Represents a single named parameter (scalar or tabular) exchanged between the
    ''' flowsheet and the OPEM Python fuel-cell model.
    ''' </summary>
    Public Class PEMFuelCellModelParameter

        Implements ICustomXMLSerialization

        ''' <summary>Gets or sets the parameter name.</summary>
        Public Property Name As String = ""

        ''' <summary>Gets or sets the parameter description.</summary>
        Public Property Description As String = ""

        ''' <summary>Gets or sets the scalar value of the parameter.</summary>
        Public Property Value As Double

        ''' <summary>Gets or sets the engineering unit string for the scalar value.</summary>
        Public Property Units As String = ""

        ''' <summary>Gets or sets the X-axis title for tabular data.</summary>
        Public Property TitleX As String = ""

        ''' <summary>Gets or sets the Y-axis title for tabular data.</summary>
        Public Property TitleY As String = ""

        ''' <summary>Gets or sets the list of X-axis values for tabular data.</summary>
        Public Property ValuesX As List(Of Double)

        ''' <summary>Gets or sets the list of Y-axis values for tabular data.</summary>
        Public Property ValuesY As List(Of Double)

        ''' <summary>Gets or sets the engineering unit string for the X-axis values.</summary>
        Public Property UnitsX As String = ""

        ''' <summary>Gets or sets the engineering unit string for the Y-axis values.</summary>
        Public Property UnitsY As String = ""

        ''' <summary>Initializes a new default instance of the <see cref="PEMFuelCellModelParameter"/> class.</summary>
        Public Sub New()

        End Sub

        ''' <summary>
        ''' Initializes a new instance with the specified name, description, value, and unit.
        ''' </summary>
        Public Sub New(_name As String, _description As String, _value As Double, _units As String)

            Name = _name
            Value = _value
            Units = _units
            Description = _description

        End Sub

        ''' <summary>Serializes this parameter to a list of XML elements.</summary>
        Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
            Return XMLSerializer.XMLSerializer.Serialize(Me)
        End Function

        ''' <summary>Restores this parameter from a list of XML elements.</summary>
        Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
            Return XMLSerializer.XMLSerializer.Deserialize(Me, data)
        End Function

    End Class

End Namespace

Namespace UnitOperations

    ''' <summary>
    ''' Abstract base class for PEM Fuel Cell unit operations that use the OPEM Python library
    ''' to model different fuel-cell polarisation curves (Amphlett, Chamberline–Kim, Larminie–Dicks).
    ''' </summary>
    Public MustInherit Partial Class PEMFuelCellUnitOpBase

        Inherits DWSIM.UnitOperations.UnitOperations.UnitOpBaseClass

        Implements DWSIM.Interfaces.IExternalUnitOperation

        Private ImagePath As String = ""

        Private Image As SKImage

        <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets or sets the relative path to the embedded OPEM Python runtime.</summary>
        Public Property OPEMPath As String = "main\python-3.9.4.amd64"

        ''' <summary>Gets or sets the generated HTML report from the OPEM model.</summary>
        Public Property HTMLreport As String = ""
        ''' <summary>Gets or sets the generated CSV report from the OPEM model.</summary>
        Public Property CSVreport As String = ""
        ''' <summary>Gets or sets the generated plain-text OPEM report.</summary>
        Public Property OPEMreport As String = ""


        ''' <summary>Gets or sets the dictionary of input parameters sent to the OPEM model.</summary>
        Public Property InputParameters As Dictionary(Of String, Auxiliary.PEMFuelCellModelParameter) = New Dictionary(Of String, Auxiliary.PEMFuelCellModelParameter)()

        ''' <summary>Gets or sets the dictionary of output parameters received from the OPEM model.</summary>
        Public Property OutputParameters As Dictionary(Of String, Auxiliary.PEMFuelCellModelParameter) = New Dictionary(Of String, Auxiliary.PEMFuelCellModelParameter)()

        ''' <summary>Gets a value indicating this unit operation acts as an energy source.</summary>
        Public Overrides ReadOnly Property IsSource As Boolean
            Get
                Return True
            End Get
        End Property


        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        Private ReadOnly Property IExternalUnitOperation_Name As String = GetDisplayName() Implements IExternalUnitOperation.Name

        ''' <summary>When overridden, gets or sets the default name prefix for this unit operation.</summary>
        Public MustOverride Property Prefix As String Implements IExternalUnitOperation.Prefix

        ''' <summary>Gets the description of this external unit operation.</summary>
        Public ReadOnly Property Description As String = GetDisplayDescription() Implements IExternalUnitOperation.Description

        ''' <summary>Gets or sets the simulation object class category (CleanPowerSources).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.CleanPowerSources

        ''' <summary>Gets a value indicating this unit operation is not compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean = False

        ''' <summary>When overridden, creates and returns a new instance of this fuel-cell type for deserialization.</summary>
        Public MustOverride Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance

        ''' <summary>When overridden, populates the input parameter dictionary with default values for the specific model.</summary>
        Public MustOverride Sub AddDefaultInputParameters()

        ''' <summary>Draws the fuel-cell icon on the given SkiaSharp canvas.</summary>
        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            Dim gx = CSng(GraphicObject.X), gy = CSng(GraphicObject.Y)
            Dim w = CSng(GraphicObject.Width), h = CSng(GraphicObject.Height)

            If GraphicObject.DrawMode = 2 Then
                If DWSIM.UnitOperations.UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(
                    canvas, gx, gy, w, h, "fuelcell_photo", Image) Then Return
            End If

            DWSIM.UnitOperations.UnitOperations.CleanEnergyDrawHelper.DrawFuelCell(
                canvas, gx, gy, w, h, GraphicObject.DrawMode = 1)

        End Sub

        ''' <summary>Creates the graphic connector definitions (hydrogen inlet, oxygen inlet, inerts outlet, power outlet).</summary>
        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors

            Dim w, h, x, y As Double
            w = GraphicObject.Width
            h = GraphicObject.Height
            x = GraphicObject.X
            y = GraphicObject.Y

            Dim myIC1 As New ConnectionPoint

            myIC1.Position = New Point(x, y + h / 3)
            myIC1.Type = ConType.ConIn
            myIC1.Direction = ConDir.Right

            Dim myIC2 As New ConnectionPoint

            myIC2.Position = New Point(x, y + 2 * h / 3)
            myIC2.Type = ConType.ConIn
            myIC2.Direction = ConDir.Right

            Dim myOC1 As New ConnectionPoint
            myOC1.Position = New Point(x + w, y + h / 2)
            myOC1.Type = ConType.ConOut
            myOC1.Direction = ConDir.Right

            Dim myOC2 As New ConnectionPoint
            myOC2.Position = New Point(x + w / 2, y + h)
            myOC2.Type = ConType.ConOut
            myOC2.Direction = ConDir.Down
            myOC2.Type = ConType.ConEn

            With GraphicObject.InputConnectors
                If .Count = 1 Then
                    .Item(0).Position = New Point(x, y + h / 2)
                    .Add(myIC2)
                ElseIf .Count = 2 Then
                    .Item(0).Position = New Point(x, y + h / 3)
                    .Item(1).Position = New Point(x, y + 2 * h / 3)
                Else
                    .Add(myIC1)
                    .Add(myIC2)
                End If
                .Item(0).ConnectorName = "Hydrogen-Rich Inlet"
                .Item(1).ConnectorName = "Oxygen-Rich Inlet"
            End With

            With GraphicObject.OutputConnectors
                If .Count = 2 Then
                    .Item(0).Position = New Point(x + w, y + h / 2)
                    .Item(1).Position = New Point(x + w / 2, y + h)
                Else
                    .Add(myOC1)
                    .Add(myOC2)
                End If
                .Item(0).ConnectorName = "Inerts Outlet"
                .Item(1).ConnectorName = "Power Outlet"
            End With

            Me.GraphicObject.EnergyConnector.Active = False

        End Sub

        ''' <summary>Initializes a new instance with a name and description.</summary>
        Public Sub New(ByVal Name As String, ByVal Description As String)

            MyBase.CreateNew()
            Me.ComponentName = Name
            Me.ComponentDescription = Description

        End Sub

        ''' <summary>Initializes a new default instance.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>Performs post-calculation validation (no-op for PEM fuel cells).</summary>
        Public Overrides Sub PerformPostCalcValidation()

        End Sub

        Private Sub CallSolverIfNeeded()
            If GlobalSettings.Settings.CallSolverOnEditorPropertyChanged Then
                FlowSheet.RequestCalculation()
            End If
        End Sub

        ''' <summary>Restores the fuel-cell state, including input and output parameter dictionaries, from XML.</summary>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            Try

                InputParameters = New Dictionary(Of String, Auxiliary.PEMFuelCellModelParameter)()

                For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "InputParameters").SingleOrDefault.Elements.ToList
                    Dim par As New Auxiliary.PEMFuelCellModelParameter()
                    par.LoadData(xel.Elements.ToList())
                    InputParameters.Add(par.Name, par)
                Next

                OutputParameters = New Dictionary(Of String, Auxiliary.PEMFuelCellModelParameter)()

                For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "OutputParameters").SingleOrDefault.Elements.ToList
                    Dim par As New Auxiliary.PEMFuelCellModelParameter()
                    par.LoadData(xel.Elements.ToList())
                    OutputParameters.Add(par.Name, par)
                Next

            Catch ex As Exception

                AddDefaultInputParameters()

            End Try

            Return True

        End Function

        ''' <summary>Serializes the fuel-cell state, including input and output parameter dictionaries, to XML.</summary>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = XMLSerializer.XMLSerializer.Serialize(Me)
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                .Add(New XElement("InputParameters"))
                For Each kvp In InputParameters
                    .Item(.Count - 1).Add(New XElement("InputParameter", kvp.Value.SaveData()))
                Next
            End With

            With elements
                .Add(New XElement("OutputParameters"))
                For Each kvp In OutputParameters
                    .Item(.Count - 1).Add(New XElement("OutputParameter", kvp.Value.SaveData()))
                Next
            End With

            Return elements

        End Function

        ''' <summary>Converts a Python list object to a .NET List(Of Double).</summary>
        Public Function ToList(pythonlist As Object) As List(Of Double)

            Using Py.GIL

                Dim list As New List(Of Double)

                For i As Integer = 0 To pythonlist.Length - 1
                    list.Add(pythonlist(i).ToString().ToDoubleFromInvariant())
                Next

                Return list

            End Using

        End Function

        ''' <summary>When overridden, populates the cross-platform editor panel with controls.</summary>
        Public MustOverride Sub PopulateEditorPanel(container As Object) Implements IExternalUnitOperation.PopulateEditorPanel

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overrides Function GetProperties(proptype As PropertyType) As String()

            Select Case proptype
                Case PropertyType.ALL, PropertyType.RW, PropertyType.RO
                    Dim arr = InputParameters.Select(Function(p) p.Value.Name).ToList()
                    arr.AddRange(OutputParameters.Select(Function(p) p.Value.Name))
                    Return arr.ToArray()
                Case Else
                    Return InputParameters.Select(Function(p) p.Value.Name).ToArray()
            End Select

        End Function

        ''' <summary>Returns the value of the specified property, converted to SI.</summary>
        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object

            If InputParameters.ContainsKey(prop) Then
                Return InputParameters(prop).Value.ConvertToSI(InputParameters(prop).Units)
            ElseIf OutputParameters.ContainsKey(prop) Then
                Return OutputParameters(prop).Value.ConvertToSI(OutputParameters(prop).Units)
            Else
                Return 0.0
            End If

        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String

            If InputParameters.ContainsKey(prop) Then
                Return InputParameters(prop).Units
            ElseIf OutputParameters.ContainsKey(prop) Then
                Return OutputParameters(prop).Units
            Else
                Return ""
            End If

        End Function

        ''' <summary>Sets the value of the specified input parameter.</summary>
        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean

            If InputParameters.ContainsKey(prop) Then
                InputParameters(prop).Value = propval
                Return True
            Else
                Return False
            End If

        End Function

    End Class

End Namespace

