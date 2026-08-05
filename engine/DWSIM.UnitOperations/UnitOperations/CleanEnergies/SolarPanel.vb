Imports System.IO
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.UnitOperations.UnitOperations
Imports SkiaSharp
Imports DWSIM.UI.Shared.Avalonia
Imports System.Globalization
Imports DWSIM.SharedClasses

Namespace UnitOperations

    ''' <summary>
    ''' Represents a Solar Panel unit operation that calculates electrical power output from
    ''' solar irradiation, panel area, efficiency, and the number of panels.
    ''' </summary>
    Public Partial Class SolarPanel

        Inherits CleanEnergyUnitOpBase

        Private ImagePath As String = ""

        Private Image As SKImage

        <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets the list of equipment sub-types (Monocrystalline, Polycrystalline, Thin Film).</summary>
        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Monocrystalline", "Polycrystalline", "Thin Film"}
            End Get
        End Property

        ''' <summary>Creates the dimensions list (Area, Efficiency) for this solar panel.</summary>
        Public Overrides Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Area, .IsUserDefined = False})
            Dimensions.Add(New Dimension With {.Name = DimensionName.Efficiency, .IsUserDefined = False})

        End Sub

        ''' <summary>Updates the dimension values from the current panel properties.</summary>
        Public Overrides Sub UpdateDimensionsList()

            Dimensions(0).Value = PanelArea
            Dimensions(1).Value = PanelEfficiency

        End Sub

        ''' <summary>Gets or sets the default name prefix for this unit operation.</summary>
        Public Overrides Property Prefix As String = "SP-"

        ''' <summary>Gets or sets the area of a single panel (m²).</summary>
        Public Property PanelArea As Double = 1

        ''' <summary>Gets or sets the panel conversion efficiency (%).</summary>
        Public Property PanelEfficiency As Double = 15

        ''' <summary>Gets or sets the number of panels in the array.</summary>
        Public Property NumberOfPanels As Integer = 1

        ''' <summary>Gets or sets the calculated generated electrical power (kW).</summary>
        Public Property GeneratedPower As Double = 0.0

        ''' <summary>Gets or sets the user-specified solar irradiation (kW/m²).</summary>
        Public Property SolarIrradiation_kW_m2 As Double = 1.0

        ''' <summary>Gets or sets the actual (weather-adjusted) solar irradiation used in the calculation (kW/m²).</summary>
        Public Property ActualSolarIrradiation_kW_m2 As Double = 1.0

        ''' <summary>Returns the display name for this unit operation.</summary>
        Public Overrides Function GetDisplayName() As String
            Return "Solar Panel"
        End Function

        ''' <summary>Returns the display description for this unit operation.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return "Solar Panel"
        End Function

        ''' <summary>Initializes a new default instance of the <see cref="SolarPanel"/> class.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>Draws the solar panel icon on the given SkiaSharp canvas.</summary>
        Public Overrides Sub Draw(g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            Dim gx = CSng(GraphicObject.X), gy = CSng(GraphicObject.Y)
            Dim w = CSng(GraphicObject.Width), h = CSng(GraphicObject.Height)

            If GraphicObject.DrawMode = 2 Then
                If UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(canvas, gx, gy, w, h,
                    "solarpanel_photo", Image) Then Return
            End If

            UnitOperations.CleanEnergyDrawHelper.DrawSolarPanel(canvas, gx, gy, w, h,
                GraphicObject.DrawMode = 1)

        End Sub

        ''' <summary>Creates the graphic connector definitions (energy outlet) on the flowsheet.</summary>
        Public Overrides Sub CreateConnectors()

            Dim w, h, x, y As Double
            w = GraphicObject.Width
            h = GraphicObject.Height
            x = GraphicObject.X
            y = GraphicObject.Y

            Dim myOC1 As New ConnectionPoint
            myOC1.Position = New Point(x + w, y + w / 2.0)
            myOC1.Type = ConType.ConOut
            myOC1.Direction = ConDir.Right
            myOC1.Type = ConType.ConEn

            With GraphicObject.OutputConnectors
                If .Count = 1 Then
                    .Item(0).Position = New Point(x + w, y + w / 2.0)
                Else
                    .Add(myOC1)
                End If
                .Item(0).ConnectorName = "Power Outlet"
            End With

            Me.GraphicObject.EnergyConnector.Active = False

        End Sub


        ''' <summary>Populates the cross-platform editor panel with controls for this solar panel.</summary>
        Public Overrides Sub PopulateEditorPanel(ctner As Object)

            If TypeOf ctner Is AvaloniaEditorPanel Then
                PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel))
                Return
            End If
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim su = GetFlowsheet().FlowsheetOptions.SelectedUnitSystem
            Dim nf = GetFlowsheet().FlowsheetOptions.NumberFormat

            container.CreateAndAddCheckBoxRow("Use Global Weather Conditions", Not UseUserDefinedWeather,
                                        Sub(chk, e)
                                            UseUserDefinedWeather = Not chk.IsChecked.GetValueOrDefault()
                                        End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Solar Irradiation ({0})", "kW/m2"), SolarIrradiation_kW_m2,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     SolarIrradiation_kW_m2 = tb.Text.ToDoubleFromInvariant()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Panel Area ({0})", su.area), PanelArea,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     PanelArea = tb.Text.ToDoubleFromInvariant().ConvertToSI(su.area)
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Efficiency ({0})", "%"), PanelEfficiency,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     PanelEfficiency = tb.Text.ToDoubleFromInvariant()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Number of Units", NumberOfPanels,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     NumberOfPanels = tb.Text.ToDoubleFromInvariant()
                                                 End If
                                             End Sub)

        End Sub

        ''' <summary>Generates a plain-text report of the solar panel results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As CultureInfo, nf As String) As String

            Dim sb As New Text.StringBuilder()

            sb.AppendLine(String.Format("Number of Units: {0}", NumberOfPanels))

            sb.AppendLine()
            sb.AppendLine(String.Format("Using Global Weather: {0}", Not UseUserDefinedWeather))
            sb.AppendLine(String.Format("Solar Irradiation: {0} kW/m2", SolarIrradiation_kW_m2.ToString(nf)))

            sb.AppendLine()
            sb.AppendLine(String.Format("Panel Area: {0} {1}", PanelArea.ConvertFromSI(su.area).ToString(nf), su.area))
            sb.AppendLine(String.Format("Efficiency: {0}", PanelEfficiency.ToString(nf)))
            sb.AppendLine()
            sb.AppendLine(String.Format("Generated Power: {0} {1}", GeneratedPower.ConvertFromSI(su.heatflow).ToString(nf), su.heatflow))

            Return sb.ToString()

        End Function

        ''' <summary>Creates and returns a new instance for deserialization.</summary>
        Public Overrides Function ReturnInstance(typename As String) As Object

            Return New SolarPanel

        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.icons8_solar_panel.png")

        End Function

        ''' <summary>Creates a deep copy via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object

            Dim obj As ICustomXMLSerialization = New SolarPanel()
            obj.LoadData(Me.SaveData)
            Return obj

        End Function

        ''' <summary>Creates a deep copy via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object

            Throw New NotImplementedException()

        End Function

        ''' <summary>Restores the solar panel state from XML.</summary>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            Return True

        End Function

        ''' <summary>Serializes the solar panel state to XML.</summary>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = XMLSerializer.XMLSerializer.Serialize(Me)
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            Return elements

        End Function

        ''' <summary>Calculates the generated power from solar irradiation, panel area, efficiency, and count.</summary>
        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            Dim esout = GetOutletEnergyStream(0)

            Dim si As Double = 0.0

            If UseUserDefinedWeather Then

                si = SolarIrradiation_kW_m2

            Else

                si = FlowSheet.FlowsheetOptions.CurrentWeather.SolarIrradiation_kWh_m2

            End If

            ActualSolarIrradiation_kW_m2 = si

            GeneratedPower = si * PanelArea * NumberOfPanels * PanelEfficiency / 100.0

            esout.EnergyFlow = GeneratedPower

        End Sub

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overrides Function GetProperties(proptype As PropertyType) As String()

            Select Case proptype
                Case PropertyType.ALL, PropertyType.RW, PropertyType.RO
                    Return New String() {"Efficiency", "User-Defined Solar Irradiation", "Actual Solar Irradiation", "Panel Area", "Number of Panels", "Generated Power"}
                Case PropertyType.WR
                    Return New String() {"Efficiency", "User-Defined Solar Irradiation", "Panel Area", "Number of Panels"}
            End Select

        End Function

        ''' <summary>Returns the value of the specified property.</summary>
        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case prop
                Case "Efficiency"
                    Return PanelEfficiency
                Case "User-Defined Solar Irradiation"
                    Return SolarIrradiation_kW_m2
                Case "Actual Solar Irradiation"
                    Return ActualSolarIrradiation_kW_m2
                Case "Panel Area"
                    Return PanelArea.ConvertFromSI(su.area)
                Case "Number of Panels"
                    Return NumberOfPanels
                Case "Generated Power"
                    Return GeneratedPower.ConvertFromSI(su.heatflow)
            End Select

        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case prop
                Case "Efficiency"
                    Return "%"
                Case "User-Defined Solar Irradiation", "Actual Solar Irradiation"
                    Return "kW/m2"
                Case "Panel Area"
                    Return (su.area)
                Case "Number of Panels"
                    Return ""
                Case "Generated Power"
                    Return (su.heatflow)
            End Select

        End Function

        ''' <summary>Sets the value of the specified property.</summary>
        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case prop
                Case "Efficiency"
                    PanelEfficiency = propval
                Case "User-Defined Solar Irradiation"
                    SolarIrradiation_kW_m2 = propval
                Case "Panel Area"
                    PanelArea = Convert.ToDouble(propval).ConvertToSI(su.area)
                Case "Number of Panels"
                    NumberOfPanels = propval
            End Select

            Return True

        End Function

    End Class

End Namespace