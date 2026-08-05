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
    ''' Represents a Wind Turbine unit operation that calculates electrical power output from
    ''' wind speed, air density, rotor disk area, and turbine efficiency.
    ''' </summary>
    Public Partial Class WindTurbine

        Inherits CleanEnergyUnitOpBase

        <Xml.Serialization.XmlIgnore> Public f As Object

        Private ImagePath As String = ""

        Private Image As SKImage

        Private calc As DWSIM.Thermodynamics.CalculatorInterface.Calculator

        Private rpp As DWSIM.Thermodynamics.PropertyPackages.RaoultPropertyPackage

        ''' <summary>Gets the list of equipment sub-types (Onshore, Offshore).</summary>
        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Onshore", "Offshore"}
            End Get
        End Property

        ''' <summary>Creates the dimensions list (Power, Diameter) for this wind turbine.</summary>
        Public Overrides Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Power, .IsUserDefined = False})
            Dimensions.Add(New Dimension With {.Name = DimensionName.Diameter, .IsUserDefined = False})

        End Sub

        ''' <summary>Updates the dimension values from the current turbine properties.</summary>
        Public Overrides Sub UpdateDimensionsList()

            Dimensions(0).Value = GeneratedPower
            Dimensions(1).Value = RotorDiameter * 1000.0

        End Sub

        ''' <summary>Gets or sets the default name prefix for this unit operation.</summary>
        Public Overrides Property Prefix As String = "WT-"

        ''' <summary>User-defined wind speed (m/s).</summary>
        Public UserDefinedWindSpeed As Double = 10.0

        ''' <summary>User-defined air temperature (K).</summary>
        Public UserDefinedAirTemperature As Double = 298.15

        ''' <summary>User-defined atmospheric pressure (Pa).</summary>
        Public UserDefinedAirPressure As Double = 101325.0

        ''' <summary>User-defined relative humidity (%).</summary>
        Public UserDefinedRelativeHumidity As Double = 30.0

        ''' <summary>Actual wind speed used in the calculation (m/s).</summary>
        Public ActualWindSpeed As Double = 10.0

        ''' <summary>Actual air temperature used in the calculation (K).</summary>
        Public ActualAirTemperature As Double = 298.15

        ''' <summary>Actual atmospheric pressure used in the calculation (Pa).</summary>
        Public ActualAirPressure As Double = 101325.0

        ''' <summary>Actual relative humidity used in the calculation (%).</summary>
        Public ActualRelativeHumidity As Double = 30.0

        ''' <summary>Gets or sets the rotor disk area (m²).</summary>
        Public Property DiskArea As Double = 10.0

        ''' <summary>Gets or sets the rotor diameter (m).</summary>
        Public Property RotorDiameter As Double = 0.0

        ''' <summary>Gets or sets the turbine efficiency (%).</summary>
        Public Property Efficiency As Double = 80.0

        ''' <summary>Gets or sets the number of turbines in the farm.</summary>
        Public Property NumberOfTurbines As Integer = 1

        ''' <summary>Gets or sets the calculated air density (kg/m³).</summary>
        Public Property AirDensity As Double = 0.0

        ''' <summary>Gets or sets the calculated generated electrical power (kW).</summary>
        Public Property GeneratedPower As Double = 0.0

        ''' <summary>Gets or sets the calculated maximum theoretical (Betz limit) power (kW).</summary>
        Public Property MaximumTheoreticalPower As Double = 0.0

        ''' <summary>Returns the display name for this unit operation.</summary>
        Public Overrides Function GetDisplayName() As String
            Return "Wind Turbine"
        End Function

        ''' <summary>Returns the display description for this unit operation.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return "Wind Turbine"
        End Function

        ''' <summary>Initializes a new default instance of the <see cref="WindTurbine"/> class.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>Draws the wind turbine icon on the given SkiaSharp canvas.</summary>
        Public Overrides Sub Draw(g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            Dim gx = CSng(GraphicObject.X), gy = CSng(GraphicObject.Y)
            Dim w = CSng(GraphicObject.Width), h = CSng(GraphicObject.Height)

            If GraphicObject.DrawMode = 2 Then
                If UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(canvas, gx, gy, w, h,
                    "windturbine_photo", Image) Then Return
            End If

            UnitOperations.CleanEnergyDrawHelper.DrawWindTurbine(canvas, gx, gy, w, h,
                GraphicObject.DrawMode = 1)

        End Sub

        ''' <summary>Creates the graphic connector definitions on the flowsheet.</summary>
        Public Overrides Sub CreateConnectors()

            Dim w, h, x, y As Double
            w = GraphicObject.Width
            h = GraphicObject.Height
            x = GraphicObject.X
            y = GraphicObject.Y

            Dim myOC1 As New ConnectionPoint
            myOC1.Position = New Point(x + w, y + h / 2.0)
            myOC1.Type = ConType.ConOut
            myOC1.Direction = ConDir.Right
            myOC1.Type = ConType.ConEn

            With GraphicObject.OutputConnectors
                If .Count = 1 Then
                    .Item(0).Position = New Point(x + w, y + h / 2.0)
                Else
                    .Add(myOC1)
                End If
                .Item(0).ConnectorName = "Power Outlet"
            End With

            Me.GraphicObject.EnergyConnector.Active = False

        End Sub

        ''' <summary>Populates the cross-platform editor panel with controls for this wind turbine.</summary>
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

            container.CreateAndAddTextBoxRow(nf, String.Format("Wind Speed ({0})", su.velocity), UserDefinedWindSpeed,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     UserDefinedWindSpeed = tb.Text.ToDoubleFromInvariant().ConvertToSI(su.velocity)
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Air Temperature ({0})", su.temperature), UserDefinedAirTemperature,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     UserDefinedAirTemperature = tb.Text.ToDoubleFromInvariant().ConvertToSI(su.temperature)
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Air Pressure ({0})", su.pressure), UserDefinedAirPressure,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     UserDefinedAirPressure = tb.Text.ToDoubleFromInvariant().ConvertToSI(su.pressure)
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Relative Humidity ({0})", "%"), UserDefinedRelativeHumidity,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     UserDefinedRelativeHumidity = tb.Text.ToDoubleFromInvariant()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Disk Area ({0})", su.area), DiskArea,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     DiskArea = tb.Text.ToDoubleFromInvariant().ConvertToSI(su.area)
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Efficiency ({0})", "%"), Efficiency,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     Efficiency = tb.Text.ToDoubleFromInvariant()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Number of Units", NumberOfTurbines,
                                             Sub(tb, e)
                                                 If tb.Text.ToDoubleFromInvariant().IsValidDouble() Then
                                                     NumberOfTurbines = tb.Text.ToDoubleFromInvariant()
                                                 End If
                                             End Sub)

        End Sub

        ''' <summary>Generates a plain-text report of the wind turbine results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As CultureInfo, nf As String) As String

            Dim sb As New Text.StringBuilder()

            sb.AppendLine(String.Format("Number of Units: {0}", NumberOfTurbines))

            sb.AppendLine()
            sb.AppendLine(String.Format("Using Global Weather: {0}", Not UseUserDefinedWeather))
            sb.AppendLine(String.Format("Air Temperature: {0} {1}", ActualAirTemperature.ConvertFromSI(su.temperature).ToString(nf), su.temperature))
            sb.AppendLine(String.Format("Air Pressure: {0} {1}", ActualAirPressure.ConvertFromSI(su.pressure).ToString(nf), su.pressure))
            sb.AppendLine(String.Format("Relative Humidity (%): {0}", ActualRelativeHumidity.ToString(nf)))

            sb.AppendLine()
            sb.AppendLine(String.Format("Disk Area: {0} {1}", DiskArea.ConvertFromSI(su.area).ToString(nf), su.area))
            sb.AppendLine(String.Format("Efficiency: {0}", Efficiency.ToString(nf)))
            sb.AppendLine()
            sb.AppendLine(String.Format("Calculated Air Density: {0} {1}", AirDensity.ConvertFromSI(su.density).ToString(nf), su.density))
            sb.AppendLine(String.Format("Maximum Theoretical Power: {0} {1}", MaximumTheoreticalPower.ConvertFromSI(su.heatflow).ToString(nf), su.heatflow))
            sb.AppendLine(String.Format("Generated Power: {0} {1}", GeneratedPower.ConvertFromSI(su.heatflow).ToString(nf), su.heatflow))

            Return sb.ToString()

        End Function

        ''' <summary>Creates and returns a new instance for deserialization.</summary>
        Public Overrides Function ReturnInstance(typename As String) As Object

            Return New WindTurbine

        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.icons8_wind_turbine.png")

        End Function

        ''' <summary>Creates a deep copy via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object

            Dim obj As ICustomXMLSerialization = New WindTurbine()
            obj.LoadData(Me.SaveData)
            Return obj

        End Function

        ''' <summary>Creates a deep copy via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object

            Throw New NotImplementedException()

        End Function

        ''' <summary>Restores the wind turbine state from XML.</summary>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            Return True

        End Function

        ''' <summary>Serializes the wind turbine state to XML.</summary>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = XMLSerializer.XMLSerializer.Serialize(Me)
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            Return elements

        End Function

        ''' <summary>Calculates the generated power from wind speed, air density, disk area, and efficiency.</summary>
        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            Dim esout = GetOutletEnergyStream(0)

            Dim ws, at, ap, rh As Double

            If UseUserDefinedWeather Then

                ws = UserDefinedWindSpeed
                at = UserDefinedAirTemperature
                rh = UserDefinedRelativeHumidity
                ap = UserDefinedAirPressure

            Else

                ws = FlowSheet.FlowsheetOptions.CurrentWeather.WindSpeed_km_h / 3.6
                at = FlowSheet.FlowsheetOptions.CurrentWeather.Temperature_C + 273.15
                rh = FlowSheet.FlowsheetOptions.CurrentWeather.RelativeHumidity_pct
                ap = FlowSheet.FlowsheetOptions.CurrentWeather.AtmosphericPressure_Pa

            End If

            ActualAirPressure = ap
            ActualAirTemperature = at
            ActualRelativeHumidity = rh
            ActualWindSpeed = ws

            'calculate air density

            If calc Is Nothing Then
                calc = New Thermodynamics.CalculatorInterface.Calculator()
                calc.Initialize()
                rpp = New Thermodynamics.PropertyPackages.RaoultPropertyPackage()
            End If

            Dim airstr = calc.CreateMaterialStream({"Air", "Water"}, {1.0, 1.0})
            airstr.SetMassFlow(1.0)
            airstr.SetTemperature(at)
            airstr.SetPressure(ap)
            airstr.SetFlowsheet(FlowSheet)
            airstr.PropertyPackage = rpp
            rpp.CurrentMaterialStream = airstr

            airstr.Calculate()

            Dim wc = airstr.Phases(2).Compounds("Water").MoleFraction.GetValueOrDefault()

            'add relative humidity
            wc = wc * rh / 100.0

            airstr = calc.CreateMaterialStream({"Air", "Water"}, {1.0 - wc, wc})
            airstr.SetMassFlow(1.0)
            airstr.SetTemperature(at)
            airstr.SetPressure(ap)
            airstr.SetFlowsheet(FlowSheet)
            airstr.PropertyPackage = rpp
            rpp.CurrentMaterialStream = airstr

            airstr.Calculate()

            AirDensity = airstr.Phases(2).Properties.density.GetValueOrDefault()

            airstr.Dispose()
            airstr = Nothing

            If RotorDiameter <> 0.0 Then
                DiskArea = Math.PI * RotorDiameter ^ 2 / 4
            Else
                RotorDiameter = (DiskArea * 4 / Math.PI) ^ 0.5
            End If

            MaximumTheoreticalPower = NumberOfTurbines * 8.0 / 27.0 * AirDensity * ws ^ 3 * DiskArea / 1000.0 ' kW

            GeneratedPower = MaximumTheoreticalPower * Efficiency / 100.0

            esout.EnergyFlow = GeneratedPower

        End Sub

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overrides Function GetProperties(proptype As PropertyType) As String()

            Select Case proptype
                Case PropertyType.ALL, PropertyType.RW, PropertyType.RO
                    Return New String() {"Efficiency", "User-Defined Wind Speed", "Actual Wind Speed", "User-Defined Air Temperature", "Actual Air Temperature",
                        "User-Defined Air Pressure", "Actual Air Pressure", "User-Defined Relative Humidity", "Actual Relative Humidity",
                        "Disk Area", "Rotor Diameter", "Number of Units", "Generated Power", "Maximum Theoretical Power", "Calculated Air Density"}
                Case PropertyType.WR
                    Return New String() {"Efficiency", "User-Defined Wind Speed", "User-Defined Air Temperature",
                        "User-Defined Air Pressure", "User-Defined Relative Humidity", "Rotor Diameter", "Number of Units"}
            End Select

        End Function

        ''' <summary>Returns the value of the specified property.</summary>
        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case prop
                Case "Efficiency"
                    Return Efficiency
                Case "User-Defined Wind Speed"
                    Return UserDefinedWindSpeed.ConvertFromSI(su.velocity)
                Case "Actual Wind Speed"
                    Return ActualWindSpeed.ConvertFromSI(su.velocity)
                Case "User-Defined Air Temperature"
                    Return UserDefinedAirTemperature.ConvertFromSI(su.temperature)
                Case "Actual Air Temperature"
                    Return ActualAirTemperature.ConvertFromSI(su.temperature)
                Case "User-Defined Air Pressure"
                    Return UserDefinedAirPressure.ConvertFromSI(su.pressure)
                Case "Actual Air Pressure"
                    Return ActualAirPressure.ConvertFromSI(su.pressure)
                Case "User-Defined Relative Humidity"
                    Return UserDefinedRelativeHumidity
                Case "Actual Relative Humidity"
                    Return ActualRelativeHumidity
                Case "Disk Area"
                    Return DiskArea.ConvertFromSI(su.area)
                Case "Rotor Diameter"
                    Return RotorDiameter.ConvertFromSI(su.distance)
                Case "Number of Units"
                    Return NumberOfTurbines
                Case "Generated Power"
                    Return GeneratedPower.ConvertFromSI(su.heatflow)
                Case "Maximum Theoretical Power"
                    Return MaximumTheoreticalPower.ConvertFromSI(su.heatflow)
                Case "Calculated Air Density"
                    Return AirDensity.ConvertFromSI(su.density)
            End Select

        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case prop
                Case "Efficiency"
                    Return "%"
                Case "User-Defined Wind Speed"
                    Return (su.velocity)
                Case "Actual Wind Speed"
                    Return (su.velocity)
                Case "User-Defined Air Temperature"
                    Return (su.temperature)
                Case "Actual Air Temperature"
                    Return (su.temperature)
                Case "User-Defined Air Pressure"
                    Return (su.pressure)
                Case "Actual Air Pressure"
                    Return (su.pressure)
                Case "User-Defined Relative Humidity"
                    Return "%"
                Case "Actual Relative Humidity"
                    Return "%"
                Case "Disk Area"
                    Return (su.area)
                Case "Rotor Diameter"
                    Return (su.distance)
                Case "Number of Units"
                    Return ""
                Case "Generated Power"
                    Return (su.heatflow)
                Case "Maximum Theoretical Power"
                    Return (su.heatflow)
                Case "Calculated Air Density"
                    Return (su.density)
            End Select

        End Function

        ''' <summary>Sets the value of the specified property.</summary>
        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI
            Select Case prop
                Case "Efficiency"
                    Efficiency = Convert.ToDouble(propval)
                Case "User-Defined Wind Speed"
                    UserDefinedWindSpeed = Convert.ToDouble(propval).ConvertFromSI(su.velocity)
                Case "User-Defined Air Temperature"
                    UserDefinedAirTemperature = Convert.ToDouble(propval).ConvertFromSI(su.temperature)
                Case "User-Defined Air Pressure"
                    UserDefinedAirPressure = Convert.ToDouble(propval).ConvertFromSI(su.pressure)
                Case "User-Defined Relative Humidity"
                    UserDefinedRelativeHumidity = Convert.ToDouble(propval)
                Case "Disk Area"
                    DiskArea = Convert.ToDouble(propval).ConvertFromSI(su.area)
                    RotorDiameter = (DiskArea * 4 / Math.PI) ^ 0.5
                Case "Rotor Diameter"
                    RotorDiameter = Convert.ToDouble(propval).ConvertFromSI(su.distance)
                    DiskArea = Math.PI * RotorDiameter ^ 2 / 4
                Case "Number of Units"
                    NumberOfTurbines = Convert.ToDouble(propval)
            End Select

            Return True

        End Function

    End Class

End Namespace