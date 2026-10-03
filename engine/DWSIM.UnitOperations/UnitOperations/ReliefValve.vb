Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.Streams
Imports PSVSizing = DWSIM.Thermodynamics.Utilities.PSV.Sizing
Imports DWSIM.UnitOperations.UnitOperations
Imports DWSIM.UnitOperations.UnitOperations.Valve
Imports DWSIM.UI.Shared.Avalonia
Imports SkiaSharp
Imports SkiaSharp.Views.Desktop

Namespace UnitOperations

    ''' <summary>
    ''' Represents a safety relief valve (PSV / PRV) unit operation that opens progressively
    ''' between a set-point pressure and a fully-opened pressure, relieving fluid to a downstream
    ''' line. The orifice area, discharge coefficient, and back-pressure corrections follow
    ''' API 520 / ASME Section VIII methodology.
    ''' </summary>
    Public Partial Class ReliefValve

        Inherits UnitOpBaseClass

        Implements IExternalUnitOperation

        ''' <summary>Holds the compiled opening/Kv relationship expression between calculations.</summary>
        <Xml.Serialization.XmlIgnore> Private _expressions As New DWSIM.SharedClasses.ExpressionCache

        Private UOName As String = "Relief Valve"

        Private UODescription As String = "Safety Relief Valve model"

        ''' <summary>Gets or sets the simulation object class category (PressureChangers).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.PressureChangers

        ''' <summary>Gets a value indicating whether this unit operation supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this unit operation has no dedicated dynamic-mode properties.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        Private ReadOnly Property IExternalUnitOperation_Name As String = UOName Implements IExternalUnitOperation.Name

        ''' <summary>Gets the description of this external unit operation.</summary>
        Public ReadOnly Property Description As String = UODescription Implements IExternalUnitOperation.Description

        ''' <summary>Gets the default name prefix used when adding this unit operation to a flowsheet.</summary>
        Public ReadOnly Property Prefix As String = "PSV-" Implements IExternalUnitOperation.Prefix

        ''' <summary>Gets a value indicating this unit operation is not compatible with mobile/cross-platform interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean = False

        ''' <summary>Gets or sets the expression relating percentage opening to percentage Kv (e.g. "1.0*OP").</summary>
        Public Property PercentOpeningVersusPercentKvExpression As String = "1.0*OP"

        ''' <summary>Gets or sets the characteristic parameter used with the equal-percentage inherent curve.</summary>
        Public Property CharacteristicParameter As Double = 50

        ''' <summary>Gets or sets the relationship type between valve opening and effective Kv.</summary>
        Public Property DefinedOpeningKvRelationShipType As OpeningKvRelationshipType = OpeningKvRelationshipType.Linear

        ''' <summary>Gets or sets the X-axis data (opening %) for a user-defined Kv data table.</summary>
        Public Property OpeningKvRelDataTableX As New List(Of Double)

        ''' <summary>Gets or sets the Y-axis data (Kv %) for a user-defined Kv data table.</summary>
        Public Property OpeningKvRelDataTableY As New List(Of Double)

        ''' <summary>Gets or sets the set-point pressure (Pa) at which the relief valve begins to open.</summary>
        Public Property SetPointPressure As Double = 0.0

        ''' <summary>Gets or sets the pressure (Pa) at which the relief valve is fully open.</summary>
        Public Property FullyOpenedPressure As Double = 0.0

        ''' <summary>Gets or sets the viscosity correction coefficient applied to the discharge calculation.</summary>
        Public Property ViscosityCoefficient As Double = 1.0

        ''' <summary>Gets or sets the effective discharge coefficient of the orifice.</summary>
        Public Property DischargeCoefficient As Double = 1.0

        ''' <summary>Gets or sets the back-pressure correction factor applied to the relieving capacity.</summary>
        Public Property BackPressureCoefficient As Double = 1.0

        ''' <summary>Gets or sets the effective orifice area (m²). Default is API orifice designation "D".</summary>
        Public Property OrificeArea As Double = 0.71 * 0.0001  'D, m2 

        ''' <summary>Gets the list of standard API orifice letter designations with their areas.</summary>
        Public Shared Property StandardOrificeAreas = New List(Of String)({
            "D / 0.11 in² / 0.71 cm²",
            "E / 0.20 in² / 1.26 cm²",
            "F / 0.31 in² / 1.98 cm²",
            "G / 0.50 in² / 3.24 cm²",
            "H / 0.79 in² / 5.06 cm²",
            "J / 1.29 in² / 8.30 cm²",
            "K / 1.84 in² / 11.85 cm²",
            "L / 2.85 in² / 18.40 cm²",
            "M / 3.60 in² / 23.23 cm²",
            "N / 4.34 in² / 28.00 cm²",
            "P / 6.38 in² / 41.16 cm²",
            "Q / 11.05 in² / 71.29 cm²",
            "R / 16.00 in² / 103.22 cm²",
            "T / 26.00 in² / 167.74 cm²"
        })

        ''' <summary>Steady-state calculation modes of the relief valve.</summary>
        Public Enum ReliefValveCalculationMode
            ''' <summary>Relieving capacity of the orifice area in use.</summary>
            Rating = 0
            ''' <summary>Required orifice area for the inlet mass flow, and the API 526 orifice that holds it.</summary>
            Sizing = 1
        End Enum

        ''' <summary>
        ''' Gets or sets the steady-state calculation mode. Rating gives the relieving capacity of
        ''' <see cref="OrificeArea"/>; Sizing gives the area the inlet mass flow needs and the smallest
        ''' API 526 orifice that holds it. Both report the required area. Default Rating.
        ''' </summary>
        Public Property CalculationMode As ReliefValveCalculationMode = ReliefValveCalculationMode.Rating

        ''' <summary>
        ''' Gets or sets the allowable overpressure, % of the gauge set pressure. With a set pressure
        ''' above zero, the API 520 relieving pressure is P1 = Patm + (Pset - Patm) (1 + overpressure / 100);
        ''' with no set pressure, P1 is the inlet stream pressure. Default 10.
        ''' </summary>
        Public Property OverpressurePercent As Double = 10.0

        ''' <summary>Gets or sets the total back pressure (Pa, absolute) the steady-state calculation discharges to. Default atmospheric.</summary>
        Public Property BackPressure As Double = 101325.0

        ''' <summary>Gets or sets the combination correction factor Kc (rupture disk upstream of the valve). Default 1.</summary>
        Public Property CombinationCorrectionFactor As Double = 1.0

        ''' <summary>Gets or sets the liquid back pressure correction factor Kw (balanced bellows valves). Default 1.</summary>
        Public Property LiquidBackPressureCoefficient As Double = 1.0

        ''' <summary>
        ''' Gets or sets whether the steady state works out the liquid viscosity correction Kv from the
        ''' Reynolds number through the orifice (API 520 Part I) and stores it in
        ''' <see cref="ViscosityCoefficient"/>, which the dynamic model then uses. Default False: the
        ''' user value is used as it is.
        ''' </summary>
        Public Property CalculateViscosityCorrection As Boolean = False

        ''' <summary>
        ''' Gets or sets whether the steady state writes the outlet stream: the inlet mass flow and
        ''' composition at the back pressure, with the inlet enthalpy (isenthalpic letdown, as the
        ''' dynamic model writes it). Default False, which leaves the outlet stream untouched as the
        ''' steady state always did.
        ''' </summary>
        Public Property UpdateOutletStream As Boolean = False

        ''' <summary>Result: relieving pressure P1 used by the steady-state calculation (Pa, absolute).</summary>
        Public Property ResultRelievingPressure As Double = 0.0

        ''' <summary>Result: relieving capacity (kg/s) of the orifice in use (rating) or of the selected API 526 orifice (sizing).</summary>
        Public Property ResultCapacity As Double = 0.0

        ''' <summary>Result: effective orifice area (m²) the inlet mass flow needs at the relieving conditions.</summary>
        Public Property ResultRequiredArea As Double = 0.0

        ''' <summary>Result: API 526 orifice letter, the one matching the orifice area (rating) or the smallest that holds the required area (sizing).</summary>
        Public Property ResultStandardOrifice As String = ""

        ''' <summary>Result: True when the flow through the orifice is critical (choked).</summary>
        Public Property ResultChokedFlow As Boolean = False

        ''' <summary>Result: the flow regime the steady-state calculation used (Vapor, Liquid or Two-Phase).</summary>
        Public Property ResultFlowRegime As String = ""

        ''' <summary>Result: warnings of the last steady-state calculation, empty when there were none.</summary>
        Public Property ResultMessage As String = ""

        ''' <summary>API 526 orifice letters, in the order of <see cref="ApiOrificeAreas"/>.</summary>
        Private Shared ReadOnly ApiOrificeLetters As String() = {"D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "P", "Q", "R", "T"}

        ''' <summary>API 526 effective orifice areas, in², the table <c>PSV.Sizing.StandardOrifice</c> selects from.</summary>
        Private Shared ReadOnly ApiOrificeAreas As Double() = {0.11, 0.196, 0.307, 0.503, 0.785, 1.287, 1.838, 2.853, 3.6, 4.34, 6.38, 11.05, 16.0, 26.0}

        Private Const SquareMetresPerSquareInch As Double = 0.00064516


        ''' <summary>
        ''' Initializes a new instance of the <see cref="ReliefValve"/> class with a name and description.
        ''' </summary>
        ''' <param name="Name">The display name of the relief valve.</param>
        ''' <param name="Description">A brief description of the relief valve.</param>
        Public Sub New(ByVal Name As String, ByVal Description As String)

            MyBase.CreateNew()
            Me.ComponentName = Name
            Me.ComponentDescription = Description

        End Sub

        ''' <summary>Initializes a new default instance of the <see cref="ReliefValve"/> class.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>Returns the display name for this unit operation.</summary>
        ''' <returns>The unit operation name string.</returns>
        Public Overrides Function GetDisplayName() As String

            Return UOName

        End Function

        ''' <summary>Returns the display description for this unit operation.</summary>
        ''' <returns>The unit operation description string.</returns>
        Public Overrides Function GetDisplayDescription() As String

            Return UODescription

        End Function

        ''' <summary>
        ''' Creates and returns a new instance of this unit operation type for deserialization.
        ''' </summary>
        ''' <param name="typename">The fully qualified type name (not used).</param>
        ''' <returns>A new <see cref="ReliefValve"/> instance.</returns>
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New ReliefValve()
        End Function

        ''' <summary>Creates a deep copy via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object

            Dim objdata = XMLSerializer.XMLSerializer.Serialize(Me)
            Dim newrf = New ReliefValve()
            newrf.LoadData(objdata)

            Return newrf

        End Function

        ''' <summary>Creates a deep copy via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object

            Dim jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(Me)
            Dim newrf = Newtonsoft.Json.JsonConvert.DeserializeObject(Of ReliefValve)(jsonstring)

            Return newrf

        End Function


#Region "Automatic Drawing Support"

        ''' <summary>Returns the raw bytes of the icon image for this unit operation.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.relief_valve.png")

        End Function

        Private Image As SkiaSharp.SKImage

        'this function draws the object on the flowsheet
        ''' <summary>Draws the relief valve icon on the given SkiaSharp canvas.</summary>
        Public Sub Draw(g As Object) Implements Interfaces.IExternalUnitOperation.Draw

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            CreateConnectors()
            GraphicObject.UpdateStatus()

            Using myPen As New SKPaint()
                With myPen
                    .Color = GraphicObject.LineColor
                    .StrokeWidth = GraphicObject.LineWidth
                    .IsStroke = True
                    .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                End With

                Dim X = GraphicObject.X
                Dim Y = GraphicObject.Y
                Dim Height = GraphicObject.Height
                Dim Width = GraphicObject.Width

                Using gp As New SKPath()

                    gp.MoveTo(Convert.ToInt32(X + 0.2 * Width), Convert.ToInt32(Y + Height))
                    gp.LineTo(Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.5 * Height))
                    gp.LineTo(Convert.ToInt32(X + Width), Convert.ToInt32(Y + 0.2 * Height))
                    gp.LineTo(Convert.ToInt32(X + Width), Convert.ToInt32(Y + 0.8 * Height))
                    gp.LineTo(Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.5 * Height))
                    gp.LineTo(Convert.ToInt32(X + 0.8 * Width), Convert.ToInt32(Y + Height))
                    gp.LineTo(Convert.ToInt32(X + 0.2 * Width), Convert.ToInt32(Y + Height))
                    gp.Close()

                    Select Case GraphicObject.DrawMode

                        Case 0

                            'default

                            Using gradPen As New SKPaint()
                                With gradPen
                                    .Color = GraphicObject.LineColor.WithAlpha(50)
                                    .StrokeWidth = GraphicObject.LineWidth
                                    .IsStroke = False
                                    .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                                End With

                                canvas.DrawPath(gp, gradPen)
                            End Using

                            canvas.DrawPath(gp, myPen)

                            canvas.DrawLine(Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.5 * Height), Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.2 * Height), myPen)
                            canvas.DrawLine(Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.2 * Height), Convert.ToInt32(X), Convert.ToInt32(Y + 0.2 * Height), myPen)

                            canvas.DrawLine(Convert.ToInt32(X + 0.1 * Width), Convert.ToInt32(Y + 0.3 * Height), Convert.ToInt32(X + 0.2 * Width), Convert.ToInt32(Y + 0.1 * Height), myPen)
                            canvas.DrawLine(Convert.ToInt32(X + 0.2 * Width), Convert.ToInt32(Y + 0.3 * Height), Convert.ToInt32(X + 0.3 * Width), Convert.ToInt32(Y + 0.1 * Height), myPen)
                            canvas.DrawLine(Convert.ToInt32(X + 0.3 * Width), Convert.ToInt32(Y + 0.3 * Height), Convert.ToInt32(X + 0.4 * Width), Convert.ToInt32(Y + 0.1 * Height), myPen)

                        Case 1

                            'b/w

                            With myPen
                                .Color = SKColors.Black
                                .StrokeWidth = GraphicObject.LineWidth
                                .IsStroke = True
                                .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                            End With
                            canvas.DrawPath(gp, myPen)

                            canvas.DrawLine(Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.5 * Height), Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.2 * Height), myPen)
                            canvas.DrawLine(Convert.ToInt32(X + 0.5 * Width), Convert.ToInt32(Y + 0.2 * Height), Convert.ToInt32(X), Convert.ToInt32(Y + 0.2 * Height), myPen)

                            canvas.DrawLine(Convert.ToInt32(X + 0.1 * Width), Convert.ToInt32(Y + 0.3 * Height), Convert.ToInt32(X + 0.2 * Width), Convert.ToInt32(Y + 0.1 * Height), myPen)
                            canvas.DrawLine(Convert.ToInt32(X + 0.2 * Width), Convert.ToInt32(Y + 0.3 * Height), Convert.ToInt32(X + 0.3 * Width), Convert.ToInt32(Y + 0.1 * Height), myPen)
                            canvas.DrawLine(Convert.ToInt32(X + 0.3 * Width), Convert.ToInt32(Y + 0.3 * Height), Convert.ToInt32(X + 0.4 * Width), Convert.ToInt32(Y + 0.1 * Height), myPen)

                        Case 2

                    'load the photo image on memory (generated via Nano Banana)
                    If Image Is Nothing Then

                        Using stream = New IO.MemoryStream(GetBytesFromResource("DWSIM.UnitOperations.Relief_Valve_Photo.png"))
                            Using bitmap = SkiaSharp.SKBitmap.Decode(stream)
                                Image = SkiaSharp.SKImage.FromBitmap(bitmap)
                            End Using
                        End Using

                    End If

                    'draw the image into the flowsheet inside the object's reserved rectangle area
                    Using p As New SkiaSharp.SKPaint With {.FilterQuality = SkiaSharp.SKFilterQuality.High}
                        canvas.DrawImage(Image, New SkiaSharp.SKRect(GraphicObject.X, GraphicObject.Y, GraphicObject.X + GraphicObject.Width, GraphicObject.Y + GraphicObject.Height), p)
                    End Using

                    End Select

                End Using
            End Using

        End Sub

        'this function creates the connection ports in the flowsheet object
        ''' <summary>Creates the graphic connector definitions on the flowsheet.</summary>
        Public Sub CreateConnectors() Implements Interfaces.IExternalUnitOperation.CreateConnectors

            If GraphicObject.InputConnectors.Count = 0 Then

                Dim port1 As New Drawing.SkiaSharp.GraphicObjects.ConnectionPoint()

                port1.IsEnergyConnector = False
                port1.Type = Interfaces.Enums.GraphicObjects.ConType.ConIn
                port1.Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + 0.5 * GraphicObject.Width, GraphicObject.Y + GraphicObject.Height)
                port1.ConnectorName = "Inlet Port"
                port1.Direction = Enums.GraphicObjects.ConDir.Up

                GraphicObject.InputConnectors.Add(port1)

            Else

                GraphicObject.InputConnectors(0).Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + 0.5 * GraphicObject.Width, GraphicObject.Y + GraphicObject.Height)
                GraphicObject.InputConnectors(0).ConnectorName = "Inlet Port"
                GraphicObject.InputConnectors(0).Direction = Enums.GraphicObjects.ConDir.Up

            End If

            If GraphicObject.OutputConnectors.Count = 0 Then

                Dim port3 As New Drawing.SkiaSharp.GraphicObjects.ConnectionPoint()

                port3.IsEnergyConnector = False
                port3.Type = Interfaces.Enums.GraphicObjects.ConType.ConOut
                port3.Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + GraphicObject.Width, GraphicObject.Y + 0.5 * GraphicObject.Height)
                port3.ConnectorName = "Outlet Port"

                GraphicObject.OutputConnectors.Add(port3)

            Else

                GraphicObject.OutputConnectors(0).Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + GraphicObject.Width, GraphicObject.Y + 0.5 * GraphicObject.Height)
                GraphicObject.OutputConnectors(0).ConnectorName = "Outlet Port"

            End If

            GraphicObject.EnergyConnector.Active = False

        End Sub

#End Region

#Region "Classic UI and Cross-Platform UI Editor Support"

        ''' <summary>Reserved handle for an editor window; not assigned or read by the current code. Not saved with the flowsheet.</summary>
        <Xml.Serialization.XmlIgnore> Public editwindow As Object

        'display the editor on the classic user interface
        'this updates the editor window on classic ui
        'this closes the editor on classic ui
        'returns the editing form
        'this function display the properties on the cross-platform user interface
        ''' <summary>Populates the cross-platform editor panel with controls.</summary>
        Public Sub PopulateEditorPanel(ctner As Object) Implements Interfaces.IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim su = FlowSheet.FlowsheetOptions.SelectedUnitSystem
            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("Orifice Sizing")

            container.CreateAndAddDropDownRow("Standard Orifice Size",
                                              New List(Of String)({"(select to apply)"}.Concat(StandardOrificeAreas).ToArray()),
                                              0,
                                              Sub(dd, e)
                                                  If dd.SelectedIndex > 0 Then
                                                      Dim osize = dd.SelectedItem?.ToString().Substring(4, 5).Trim()
                                                      OrificeArea = osize.ToDoubleFromInvariant() * 0.00064516
                                                      FlowSheet.RequestCalculation()
                                                  End If
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Orifice Area ({0})", su.area),
                                             OrificeArea.ConvertFromSI(su.area),
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     OrificeArea = tb.Text.ParseExpressionToDouble().ConvertToSI(su.area)
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Pressure Setpoints")

            container.CreateAndAddTextBoxRow(nf, String.Format("Set-Point Pressure ({0})", su.pressure),
                                             SetPointPressure.ConvertFromSI(su.pressure),
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SetPointPressure = tb.Text.ParseExpressionToDouble().ConvertToSI(su.pressure)
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Fully-Opened Pressure ({0})", su.pressure),
                                             FullyOpenedPressure.ConvertFromSI(su.pressure),
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     FullyOpenedPressure = tb.Text.ParseExpressionToDouble().ConvertToSI(su.pressure)
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Correction Coefficients")

            container.CreateAndAddTextBoxRow(nf, "Discharge Coefficient (Kd)", DischargeCoefficient,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DischargeCoefficient = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Back-Pressure Coefficient (Kb)", BackPressureCoefficient,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     BackPressureCoefficient = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Viscosity Coefficient (Kv)", ViscosityCoefficient,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ViscosityCoefficient = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Opening vs Kv Relationship")

            container.CreateAndAddDropDownRow("Relationship Type",
                                              New List(Of String)({"Linear", "Equal Percentage", "Quick Opening", "User-Defined"}),
                                              CInt(DefinedOpeningKvRelationShipType),
                                              Sub(dd, e)
                                                  DefinedOpeningKvRelationShipType = CType(dd.SelectedIndex, OpeningKvRelationshipType)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, "Characteristic Parameter (Quick Opening)", CharacteristicParameter,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CharacteristicParameter = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddStringEditorRow("Kv Expression (User-Defined, uses OP)",
                                                  PercentOpeningVersusPercentKvExpression,
                                                  Sub(tb, e)
                                                      PercentOpeningVersusPercentKvExpression = tb.Text
                                                  End Sub)

        End Sub

#End Region

#Region "API 520 relieving flow"

        ''' <summary>
        ''' Vapour mass flux through the orifice, kg/(s m²), for unit coefficients (API 520 Part I in SI
        ''' form). Critical flow, when P2/P1 is at or below (2/(k+1))^(k/(k-1)):
        ''' W/A = sqrt(k P1/v1 (2/(k+1))^((k+1)/(k-1))). Subcritical flow (isentropic nozzle, equal to the
        ''' critical flux at the critical ratio): W/A = sqrt(2k/(k-1) P1/v1 (r^(2/k) - r^((k+1)/k))), r = P2/P1.
        ''' With v1 = Z R T / (M P1) these are the API 520 critical and subcritical gas equations.
        ''' </summary>
        ''' <param name="P1">Upstream (relieving) pressure, Pa absolute.</param>
        ''' <param name="P2">Back pressure, Pa absolute.</param>
        ''' <param name="v1">Specific volume at P1, m³/kg.</param>
        ''' <param name="k">Ideal-gas ratio of specific heats.</param>
        ''' <param name="choked">Set to True when the flow is critical.</param>
        Private Shared Function GasMassFlux(P1 As Double, P2 As Double, v1 As Double, k As Double, ByRef choked As Boolean) As Double

            choked = False

            If Not (P1 > 0.0) OrElse Not (v1 > 0.0) OrElse P2 >= P1 Then Return 0.0

            'the equations are singular at k = 1; no real gas has Cp/Cv below one
            If k < 1.001 Then k = 1.001

            If (P2 / P1) <= (2.0 / (k + 1)) ^ (k / (k - 1)) Then
                choked = True
                Return (P1 * k / v1 * (2 / (k + 1)) ^ ((k + 1) / (k - 1))) ^ 0.5
            Else
                Return (P1 / v1 * (2 * k / (k - 1)) * ((P2 / P1) ^ (2.0 / k) - (P2 / P1) ^ ((k + 1) / k))) ^ 0.5
            End If

        End Function

        ''' <summary>
        ''' Liquid mass flux through the orifice, kg/(s m²), for unit coefficients: W/A = sqrt(2 (P1 - P2) rho),
        ''' the API 520 liquid equation A = 11.78 Q / (Kd Kw Kc Kv) sqrt(G / (P1 - P2)) in SI form.
        ''' </summary>
        Private Shared Function LiquidMassFlux(P1 As Double, P2 As Double, rho As Double) As Double

            If P2 >= P1 OrElse Not (rho > 0.0) Then Return 0.0

            Return (2 * (P1 - P2) * rho) ^ 0.5

        End Function

        ''' <summary>
        ''' API 520 liquid viscosity correction Kv = 1 / (0.9935 + 2.878 / R^0.5 + 342.75 / R^1.5), with
        ''' R = 18800 Q G / (mu sqrt(A)) for Q in L/min, mu in cP and A in mm². Capped at 1.
        ''' </summary>
        ''' <param name="Q">Volumetric flow, m³/s.</param>
        ''' <param name="rho">Liquid density, kg/m³.</param>
        ''' <param name="mu">Liquid viscosity, Pa.s.</param>
        ''' <param name="A">Orifice area, m².</param>
        Private Shared Function ViscosityCorrectionFactor(Q As Double, rho As Double, mu As Double, A As Double) As Double

            If Not (Q > 0.0) OrElse Not (rho > 0.0) OrElse Not (mu > 0.0) OrElse Not (A > 0.0) Then Return 1.0

            Dim R = 18800.0 * (Q * 60000.0) * (rho / 1000.0) / (mu * 1000.0 * Math.Sqrt(A * 1000000.0))

            Return Math.Min(1.0, 1.0 / (0.9935 + 2.878 / R ^ 0.5 + 342.75 / R ^ 1.5))

        End Function

        ''' <summary>
        ''' API 526 orifice letter whose area is within 3 % of <paramref name="A"/> (m²), or "" for a
        ''' non-standard area.
        ''' </summary>
        Private Shared Function MatchingStandardOrifice(A As Double) As String

            Dim ain2 = A / SquareMetresPerSquareInch

            For i = 0 To ApiOrificeAreas.Length - 1
                If Math.Abs(ain2 - ApiOrificeAreas(i)) <= 0.03 * ApiOrificeAreas(i) Then Return ApiOrificeLetters(i)
            Next

            Return ""

        End Function

        ''' <summary>API 526 area (m²) of an orifice letter, or 0 for an unknown letter.</summary>
        Private Shared Function StandardOrificeArea(letter As String) As Double

            Dim i = Array.IndexOf(ApiOrificeLetters, If(letter, "").Trim().ToUpperInvariant())

            If i < 0 Then Return 0.0

            Return ApiOrificeAreas(i) * SquareMetresPerSquareInch

        End Function

        ''' <summary>
        ''' Steady-state relief valve calculation (API 520 Part I). The inlet stream gives the relieving
        ''' state (temperature, phase split, density, k, viscosity) and the mass flow to relieve; the set
        ''' pressure and the overpressure give the relieving pressure P1; <see cref="BackPressure"/> gives P2.
        ''' </summary>
        ''' <remarks>
        ''' <para>The flow equations are the ones <see cref="RunDynamicModel"/> integrates, with the valve
        ''' fully open: vapour W = A Kd Kb Kc G(P1, P2, v1, k), liquid W = A Kd Kw Kc Kv sqrt(2 (P1 - P2) rho).
        ''' As in the dynamic model, Kb also multiplies the subcritical vapour flow; API 520 leaves it out
        ''' there for a conventional valve, so keep Kb = 1 when the back pressure is that high. The vapour
        ''' density at P1 is the inlet density scaled by P1 / Pinlet (Z and T of the inlet, as API 520 takes
        ''' them). Two-phase flow (vapour mass fraction between 1 % and 99 %) uses the omega method of
        ''' API 520 Annex D, with the specific volumes from the inlet stream and an isentropic flash to 90 %
        ''' of its pressure.</para>
        ''' <para>Rating gives the capacity of <see cref="OrificeArea"/>; Sizing gives the required area and
        ''' the smallest API 526 orifice that holds it, and reports the capacity of that orifice. Neither
        ''' mode raises an error for a state it cannot evaluate: the results are cleared and
        ''' <see cref="ResultMessage"/> says why. The outlet stream is only written when
        ''' <see cref="UpdateOutletStream"/> is set.</para>
        ''' </remarks>
        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            ResultRelievingPressure = 0.0
            ResultCapacity = 0.0
            ResultRequiredArea = 0.0
            ResultStandardOrifice = ""
            ResultChokedFlow = False
            ResultFlowRegime = ""
            ResultMessage = ""

            If Not GraphicObject.InputConnectors(0).IsAttached Then
                If UpdateOutletStream Then Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
                Exit Sub
            End If

            If UpdateOutletStream AndAlso Not GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            Dim ims = GetInletMaterialStream(0)

            Dim messages As New List(Of String)

            Try
                EvaluateRelievingFlow(ims, messages)
            Catch ex As Exception
                ResultCapacity = 0.0
                ResultRequiredArea = 0.0
                ResultStandardOrifice = ""
                ResultChokedFlow = False
                messages.Add("The API 520 calculation failed: " & ex.Message)
            End Try

            'kept on the object for the editors and the property list; not sent to the log, so a
            'flowsheet that never used the steady-state results solves as quietly as it did
            ResultMessage = String.Join(" ", messages)

            If UpdateOutletStream Then

                Dim oms = GetOutletMaterialStream(0)

                Dim P2 = If(BackPressure > 0.0, BackPressure, PSVSizing.AtmosphericPressure)

                If P2 > ims.GetPressure() Then
                    Throw New Exception("The back pressure is above the inlet stream pressure: the relief valve cannot discharge.")
                End If

                'isenthalpic letdown to the back pressure, as the dynamic model writes the outlet
                With oms
                    .AtEquilibrium = False
                    .Phases(0).Properties.temperature = ims.GetTemperature()
                    .Phases(0).Properties.pressure = P2
                    .Phases(0).Properties.enthalpy = ims.Phases(0).Properties.enthalpy.GetValueOrDefault()
                    .Phases(0).Properties.massflow = ims.Phases(0).Properties.massflow.GetValueOrDefault()
                    .DefinedFlow = FlowSpec.Mass
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = ims.Phases(0).Compounds(comp.Name).MoleFraction
                        comp.MassFraction = ims.Phases(0).Compounds(comp.Name).MassFraction
                    Next
                    .SpecType = StreamSpec.Pressure_and_Enthalpy
                End With

            End If

        End Sub

        ''' <summary>Fills the steady-state results from the inlet stream; see <see cref="Calculate"/>.</summary>
        Private Sub EvaluateRelievingFlow(ims As MaterialStream, messages As List(Of String))

            Dim Ps = ims.GetPressure()
            Dim W = ims.GetMassFlow()
            Dim xv = ims.Phases(2).Properties.massfraction.GetValueOrDefault
            Dim rho = ims.Phases(0).Properties.density.GetValueOrDefault

            Dim P1 = If(SetPointPressure > 0.0, PSVSizing.RelievingPressure(SetPointPressure, OverpressurePercent), Ps)
            Dim P2 = If(BackPressure > 0.0, BackPressure, PSVSizing.AtmosphericPressure)

            ResultRelievingPressure = P1

            If Not (P1 > P2) Then
                messages.Add("The back pressure is at or above the relieving pressure: the valve cannot relieve.")
                Exit Sub
            End If

            Dim Kd = DischargeCoefficient
            Dim Kb = BackPressureCoefficient
            Dim Kc = CombinationCorrectionFactor

            'mass flux per unit area with every coefficient but the liquid viscosity correction, kg/(s m2)
            Dim flux As Double
            Dim isLiquid As Boolean = False
            Dim mu As Double = 0.0

            If xv > 0.99 Then

                ResultFlowRegime = "Vapor"

                Dim k = ims.Phases(2).Properties.idealGasHeatCapacityRatio.GetValueOrDefault()
                Dim z = ims.Phases(2).Properties.compressibilityFactor.GetValueOrDefault()

                'Z and T of the inlet; the density follows the pressure up to P1
                Dim v1 = If(rho > 0.0 AndAlso P1 > 0.0, Ps / (rho * P1), 0.0)

                Dim choked As Boolean
                flux = Kd * Kb * Kc * GasMassFlux(P1, P2, v1, k, choked)
                ResultChokedFlow = choked

                Dim warning = PSVSizing.IdealGasWarning(z, k)
                If warning IsNot Nothing Then messages.Add(warning)

            ElseIf xv < 0.01 Then

                ResultFlowRegime = "Liquid"
                isLiquid = True

                mu = ims.Phases(1).Properties.viscosity.GetValueOrDefault()

                flux = Kd * LiquidBackPressureCoefficient * Kc * LiquidMassFlux(P1, P2, rho)

            Else

                ResultFlowRegime = "Two-Phase"

                Dim v = PSVSizing.OmegaSpecificVolumes(ims)
                Dim res = PSVSizing.TwoPhaseArea(v(0), v(1), P1, P2, 1.0, Kd, Kb, Kc)

                If Double.IsNaN(res(0)) OrElse Not (res(0) > 0.0) Then
                    messages.Add(String.Format(Globalization.CultureInfo.InvariantCulture,
                                               "Two-phase flow with omega = {0:0.000}: the mixture does not expand, so the omega method of API 520 does not apply.", res(1)))
                    Exit Sub
                End If

                'area for 1 kg/s, in2, gives the flux
                flux = 1.0 / (res(0) * SquareMetresPerSquareInch)
                ResultChokedFlow = res(4) > 0.5

            End If

            If Not (flux > 0.0) Then
                messages.Add("The relieving flow through the orifice is zero for this inlet state.")
                Exit Sub
            End If

            'required area for the inlet mass flow

            Dim Kv As Double = ViscosityCoefficient

            If W > 0.0 Then
                If isLiquid AndAlso CalculateViscosityCorrection Then
                    'API 520: size without Kv, take the next larger API 526 orifice, correct by the Kv of
                    'the flow through it and move up one orifice while the corrected area does not fit
                    Dim AR = W / flux
                    Dim A = AR
                    For i = 1 To 50
                        Dim orifice = PSVSizing.StandardOrifice(A / SquareMetresPerSquareInch).Item2 * SquareMetresPerSquareInch
                        Dim Kvi = ViscosityCorrectionFactor(W / rho, rho, mu, If(orifice > 0.0, orifice, A))
                        Dim Anew = AR / Kvi
                        If orifice > 0.0 Then
                            If Anew <= orifice Then A = Anew : Exit For
                        ElseIf Math.Abs(Anew - A) <= 0.000001 * A Then
                            A = Anew : Exit For
                        End If
                        A = Anew
                    Next
                    ResultRequiredArea = A
                    If CalculationMode = ReliefValveCalculationMode.Sizing Then ViscosityCoefficient = AR / A
                ElseIf isLiquid Then
                    ResultRequiredArea = W / (flux * Kv)
                Else
                    ResultRequiredArea = W / flux
                End If
            Else
                messages.Add("The inlet stream has no flow: the required area is zero.")
            End If

            'the orifice in use

            Dim Ause As Double

            If CalculationMode = ReliefValveCalculationMode.Sizing Then
                If W > 0.0 Then
                    Dim orifice = PSVSizing.StandardOrifice(ResultRequiredArea / SquareMetresPerSquareInch)
                    ResultStandardOrifice = orifice.Item1
                    Ause = orifice.Item2 * SquareMetresPerSquareInch
                    If ResultStandardOrifice = "" Then
                        messages.Add("The required area is larger than the API 526 T orifice: the relief needs more than one valve.")
                    End If
                End If
            Else
                Ause = OrificeArea
                ResultStandardOrifice = MatchingStandardOrifice(OrificeArea)
            End If

            'capacity of the orifice in use

            If Ause > 0.0 Then
                If isLiquid AndAlso CalculateViscosityCorrection Then
                    'Kv depends on the flow through the orifice: iterate W = A flux Kv(W)
                    Dim Kvi = 1.0
                    For i = 1 To 100
                        Dim Kvn = ViscosityCorrectionFactor(Ause * flux * Kvi / rho, rho, mu, Ause)
                        If Math.Abs(Kvn - Kvi) <= 0.0000000001 Then Kvi = Kvn : Exit For
                        Kvi = Kvn
                    Next
                    ResultCapacity = Ause * flux * Kvi
                    If CalculationMode = ReliefValveCalculationMode.Rating Then ViscosityCoefficient = Kvi
                ElseIf isLiquid Then
                    ResultCapacity = Ause * flux * Kv
                Else
                    ResultCapacity = Ause * flux
                End If
            End If

            If CalculationMode = ReliefValveCalculationMode.Rating AndAlso W > ResultCapacity * 1.000001 Then
                messages.Add(String.Format(Globalization.CultureInfo.InvariantCulture,
                                           "The inlet mass flow ({0:G5} kg/s) is above the relieving capacity of the orifice ({1:G5} kg/s).", W, ResultCapacity))
            End If

        End Sub

        ''' <summary>
        ''' Clears the outlet stream when the steady state writes it (<see cref="UpdateOutletStream"/>);
        ''' otherwise the outlet stream is left alone, as the steady state leaves it.
        ''' </summary>
        Public Overrides Sub DeCalculate()

            If UpdateOutletStream AndAlso GraphicObject.OutputConnectors(0).IsAttached Then

                With GetOutletMaterialStream(0)
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.molarfraction = 1
                    .Phases(0).Properties.massfraction = 1
                    .Phases(0).Properties.enthalpy = Nothing
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

#End Region

        ''' <summary>Performs the dynamic-mode calculation for the relief valve.</summary>
        Public Overrides Sub RunDynamicModel()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            If Not integrator.ShouldCalculatePressureFlow Then Exit Sub

            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            ElseIf Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            Dim T1, P1, H1, W, P2, rho, CpCv, V1, xv As Double

            Dim ims, oms As MaterialStream

            ims = Me.GetInletMaterialStream(0)
            oms = Me.GetOutletMaterialStream(0)

            If ims.DynamicsSpec <> Dynamics.DynamicsSpecType.Pressure OrElse
                        oms.DynamicsSpec <> Dynamics.DynamicsSpecType.Pressure Then

                Throw New Exception("Both onlet and outlet streams must be pressure-specified in dynamic mode.")

            End If

            Dim Kvc As Double = 1.0

            P1 = ims.GetPressure()

            'lift as a fraction: shut at the set pressure, fully open at the fully opened pressure
            Dim lift As Double
            If FullyOpenedPressure > SetPointPressure Then
                lift = (P1 - SetPointPressure) / (FullyOpenedPressure - SetPointPressure)
            Else
                lift = If(P1 >= SetPointPressure, 1.0, 0.0)
            End If

            If Double.IsNaN(lift) OrElse lift < 0.0 Then lift = 0.0
            If lift > 1.0 Then lift = 1.0

            'the opening/Kv relationships below take the opening in percent, as the control valve does
            Dim OpeningPct = lift * 100.0

            Select Case DefinedOpeningKvRelationShipType
                Case OpeningKvRelationshipType.UserDefined
                    Try
                        DWSIM.SharedClasses.ExpressionCache.SetVariable(_expressions.GetContext("OP"), "OP", OpeningPct)
                        Kvc = _expressions.GetCompiled("OP", PercentOpeningVersusPercentKvExpression).Evaluate() / 100
                    Catch ex As Exception
                        Throw New Exception("Invalid expression for Kv[Cv]/Opening relationship.")
                    End Try
                Case OpeningKvRelationshipType.QuickOpening
                    Kvc = (OpeningPct / 100.0) ^ 0.5
                Case OpeningKvRelationshipType.Linear
                    Kvc = OpeningPct / 100.0
                Case OpeningKvRelationshipType.EqualPercentage
                    Kvc = CharacteristicParameter ^ (OpeningPct / 100.0 - 1.0)
                Case OpeningKvRelationshipType.DataTable
                    Try
                        Dim factor = MathNet.Numerics.Interpolate.RationalWithoutPoles(OpeningKvRelDataTableX, OpeningKvRelDataTableY).Interpolate(OpeningPct) / 100.0
                        Kvc = factor
                    Catch ex As Exception
                        Throw New Exception("Error calculating Kv from tabulated data: " + ex.Message)
                    End Try
            End Select

            'below the set pressure the valve is shut whatever the curve says at zero opening
            If lift <= 0.0 Then Kvc = 0.0

            T1 = ims.GetTemperature()
            P1 = ims.GetPressure()
            H1 = ims.GetMassEnthalpy()

            xv = ims.Phases(2).Properties.massfraction.GetValueOrDefault

            rho = ims.Phases(0).Properties.density.GetValueOrDefault

            V1 = 1.0 / rho

            P2 = oms.GetPressure()

            CpCv = ims.Phases(2).Properties.idealGasHeatCapacityRatio.GetValueOrDefault()

            Dim A = OrificeArea

            Dim Kv = ViscosityCoefficient

            Dim Kd = DischargeCoefficient

            Dim Kb = BackPressureCoefficient

            Dim Kc = CombinationCorrectionFactor

            Dim Kw = LiquidBackPressureCoefficient

            'the flow equations are shared with the steady-state calculation (GasMassFlux, LiquidMassFlux)

            If xv > 0.99 Then

                'vapor flow: API 520 critical flow below the critical pressure ratio, isentropic nozzle
                'above it. The back-pressure coefficient applies in both regimes, so the flow stays
                'continuous across the critical ratio when Kb < 1 and still falls to zero as P2 reaches P1

                Dim choked As Boolean

                W = A * Kvc * Kd * Kb * Kc * GasMassFlux(P1, P2, V1, CpCv, choked)

            ElseIf xv < 0.01 Then

                'liquid flow

                W = A * Kvc * Kd * Kv * Kw * Kc * LiquidMassFlux(P1, P2, rho)

            Else

                Throw New Exception("Two-phase flow is not supported yet.")

            End If

            ims.SetMassFlow(W)
            oms.SetMassFlow(W)

            With oms
                .Phases(0).Properties.pressure = P2
                .Phases(0).Properties.enthalpy = H1
                .SetFlashSpec("PH")
                .AtEquilibrium = False
                Dim i As Integer = 0
                For Each comp In .Phases(0).Compounds.Values
                    comp.MoleFraction = ims.Phases(0).Compounds(comp.Name).MoleFraction
                    comp.MassFraction = ims.Phases(0).Compounds(comp.Name).MassFraction
                    comp.MassFlow = comp.MassFraction * W
                    comp.MolarFlow = comp.MassFlow / comp.ConstantProperties.Molar_Weight * 1000
                    i += 1
                Next
            End With

            With ims
                Dim i As Integer = 0
                For Each comp In .Phases(0).Compounds.Values
                    comp.MassFlow = comp.MassFraction * W
                    comp.MolarFlow = comp.MassFlow / comp.ConstantProperties.Molar_Weight * 1000
                    i += 1
                Next
            End With

        End Sub

#Region "Properties"

        'PROP_RV_0   Calculation Mode (0 = Rating, 1 = Sizing)
        'PROP_RV_1   Set Pressure
        'PROP_RV_2   Overpressure (%)
        'PROP_RV_3   Back Pressure
        'PROP_RV_4   Discharge Coefficient (Kd)
        'PROP_RV_5   Back Pressure Correction Factor (Kb)
        'PROP_RV_6   Combination Correction Factor (Kc)
        'PROP_RV_7   Orifice Area
        'PROP_RV_8   Standard Orifice (API 526 letter)
        'PROP_RV_9   Relieving Pressure (result)
        'PROP_RV_10  Relieving Capacity (result)
        'PROP_RV_11  Required Orifice Area (result)
        'PROP_RV_12  Choked Flow (result, 1 or 0)
        'PROP_RV_13  Fully-Opened Pressure
        'PROP_RV_14  Viscosity Correction Factor (Kv)
        'PROP_RV_15  Liquid Back Pressure Correction Factor (Kw)
        'PROP_RV_16  Calculate Viscosity Correction (1 or 0)
        'PROP_RV_17  Update Outlet Stream (1 or 0)

        Private Const PropertyCount As Integer = 18

        ''' <summary>The PROP_RV_ indexes the active mode reads, and so accepts values for.</summary>
        Private Function WritablePropertyIndexes() As List(Of Integer)

            Dim writable As New List(Of Integer)({0, 1, 2, 3, 4, 5, 6, 7, 13, 15, 16, 17})

            'the letter is an input in rating (it writes the orifice area) and a result in sizing
            If CalculationMode = ReliefValveCalculationMode.Rating Then writable.Add(8)

            'Kv is a result when the steady state works it out
            If Not CalculateViscosityCorrection Then writable.Add(14)

            Return writable

        End Function

        ''' <summary>Returns the property identifiers of the given type (PROP_RV_0 to PROP_RV_17).</summary>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()

            Dim proplist As New List(Of String)
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)

            Dim writable = WritablePropertyIndexes()

            Select Case proptype
                Case PropertyType.RO
                    For i = 0 To PropertyCount - 1
                        If Not writable.Contains(i) Then proplist.Add("PROP_RV_" + CStr(i))
                    Next
                Case PropertyType.RW, PropertyType.WR
                    For i = 0 To PropertyCount - 1
                        If writable.Contains(i) Then proplist.Add("PROP_RV_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To PropertyCount - 1
                        proplist.Add("PROP_RV_" + CStr(i))
                    Next
            End Select

            Return proplist.ToArray()

        End Function

        ''' <summary>Returns the value of a property in the given unit system (SI when Nothing).</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If val0 IsNot Nothing Then Return val0

            If Not prop.StartsWith("PROP_RV_") Then Return Nothing

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case Convert.ToInt32(prop.Split("_")(2))
                Case 0
                    Return CDbl(CalculationMode)
                Case 1
                    Return SetPointPressure.ConvertFromSI(su.pressure)
                Case 2
                    Return OverpressurePercent
                Case 3
                    Return BackPressure.ConvertFromSI(su.pressure)
                Case 4
                    Return DischargeCoefficient
                Case 5
                    Return BackPressureCoefficient
                Case 6
                    Return CombinationCorrectionFactor
                Case 7
                    Return OrificeArea.ConvertFromSI(su.area)
                Case 8
                    If CalculationMode = ReliefValveCalculationMode.Sizing Then
                        Return ResultStandardOrifice
                    Else
                        Return MatchingStandardOrifice(OrificeArea)
                    End If
                Case 9
                    Return ResultRelievingPressure.ConvertFromSI(su.pressure)
                Case 10
                    Return ResultCapacity.ConvertFromSI(su.massflow)
                Case 11
                    Return ResultRequiredArea.ConvertFromSI(su.area)
                Case 12
                    Return If(ResultChokedFlow, 1.0, 0.0)
                Case 13
                    Return FullyOpenedPressure.ConvertFromSI(su.pressure)
                Case 14
                    Return ViscosityCoefficient
                Case 15
                    Return LiquidBackPressureCoefficient
                Case 16
                    Return If(CalculateViscosityCorrection, 1.0, 0.0)
                Case 17
                    Return If(UpdateOutletStream, 1.0, 0.0)
            End Select

            Return Nothing

        End Function

        ''' <summary>
        ''' Sets a property from a value in the given unit system (SI when Nothing). The standard orifice
        ''' (PROP_RV_8) takes an API 526 letter, D to T, and writes its area to the orifice area.
        ''' </summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If Not prop.StartsWith("PROP_RV_") Then Return False

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case Convert.ToInt32(prop.Split("_")(2))
                Case 0
                    CalculationMode = CType(Convert.ToInt32(propval), ReliefValveCalculationMode)
                Case 1
                    SetPointPressure = Convert.ToDouble(propval).ConvertToSI(su.pressure)
                Case 2
                    OverpressurePercent = Convert.ToDouble(propval)
                Case 3
                    BackPressure = Convert.ToDouble(propval).ConvertToSI(su.pressure)
                Case 4
                    DischargeCoefficient = Convert.ToDouble(propval)
                Case 5
                    BackPressureCoefficient = Convert.ToDouble(propval)
                Case 6
                    CombinationCorrectionFactor = Convert.ToDouble(propval)
                Case 7
                    OrificeArea = Convert.ToDouble(propval).ConvertToSI(su.area)
                Case 8
                    Dim area = StandardOrificeArea(Convert.ToString(propval))
                    If area <= 0.0 Then Throw New ArgumentException("Unknown API 526 orifice letter: " & Convert.ToString(propval) & ". Use D, E, F, G, H, J, K, L, M, N, P, Q, R or T.")
                    OrificeArea = area
                Case 13
                    FullyOpenedPressure = Convert.ToDouble(propval).ConvertToSI(su.pressure)
                Case 14
                    ViscosityCoefficient = Convert.ToDouble(propval)
                Case 15
                    LiquidBackPressureCoefficient = Convert.ToDouble(propval)
                Case 16
                    CalculateViscosityCorrection = Convert.ToDouble(propval) <> 0.0
                Case 17
                    UpdateOutletStream = Convert.ToDouble(propval) <> 0.0
                Case Else
                    Return False
            End Select

            Return True

        End Function

        ''' <summary>Returns the unit of a property in the given unit system (SI when Nothing).</summary>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String

            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then Return u0

            If Not prop.StartsWith("PROP_RV_") Then Return u0

            If su Is Nothing Then su = New SharedClasses.SystemsOfUnits.SI

            Select Case Convert.ToInt32(prop.Split("_")(2))
                Case 1, 3, 9, 13
                    Return su.pressure
                Case 2
                    Return "%"
                Case 7, 11
                    Return su.area
                Case 10
                    Return su.massflow
                Case Else
                    Return ""
            End Select

        End Function

#End Region

    End Class

End Namespace

