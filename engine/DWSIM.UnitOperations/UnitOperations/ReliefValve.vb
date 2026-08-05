Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.Streams
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

        ''' <summary>Calculates the relief valve pressure drop and flow.</summary>
        Public Overrides Sub Calculate(Optional args As Object = Nothing)

        End Sub

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

            Dim OpeningPct = (P1 - SetPointPressure) / (FullyOpenedPressure - SetPointPressure)

            If OpeningPct < 0.0 Then OpeningPct = 0.0
            If OpeningPct > 1.0 Then OpeningPct = 1.0

            If Double.IsInfinity(OpeningPct) Then OpeningPct = 1.0

            Select Case DefinedOpeningKvRelationShipType
                Case OpeningKvRelationshipType.UserDefined
                    Try
                        Dim ExpContext As New Flee.PublicTypes.ExpressionContext()
                        ExpContext.Imports.AddType(GetType(System.Math))
                        ExpContext.Variables.Clear()
                        ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture
                        ExpContext.Variables.Add("OP", OpeningPct)
                        Dim Expr = ExpContext.CompileGeneric(Of Double)(PercentOpeningVersusPercentKvExpression)
                        Kvc = Expr.Evaluate() / 100
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
                        Dim factor = MathNet.Numerics.Interpolate.RationalWithoutPoles(OpeningKvRelDataTableX, OpeningKvRelDataTableX).Interpolate(OpeningPct) / 100.0
                        Kvc = factor
                    Catch ex As Exception
                        Throw New Exception("Error calculating Kv from tabulated data: " + ex.Message)
                    End Try
            End Select

            T1 = ims.GetTemperature()
            P1 = ims.GetPressure()
            H1 = ims.GetMassEnthalpy()

            xv = ims.Phases(2).Properties.massfraction.GetValueOrDefault

            rho = ims.Phases(0).Properties.density.GetValueOrDefault

            V1 = 1.0 / rho

            P2 = oms.GetPressure()

            CpCv = ims.Phases(2).Properties.idealGasHeatCapacityRatio.GetValueOrDefault()

            Dim choked_factor = (2.0 / (CpCv + 1)) ^ (CpCv / (CpCv - 1))

            Dim A = OrificeArea

            Dim Kv = ViscosityCoefficient

            Dim Kd = DischargeCoefficient

            Dim Kb = BackPressureCoefficient

            If xv > 0.99 Then

                'vapor flow

                If (P2 / P1) >= choked_factor Then

                    'choked flow

                    W = A * Kvc * Kd * Kb * (P1 * CpCv / V1 * (2 / (CpCv + 1)) ^ ((CpCv - 1) / (CpCv + 1))) ^ 0.5

                Else

                    'non-choked flow

                    W = A * Kvc * Kd * (P1 / V1 * (2 * CpCv / (CpCv + 1)) * ((P2 / P1) ^ (2.0 / CpCv) - (P2 / P1) ^ ((CpCv + 1) / CpCv))) ^ 0.5

                End If

            ElseIf xv < 0.01 Then

                'liquid flow

                W = A * Kvc * Kd * Kv * (2 * (P1 - P2) * rho) ^ 0.5

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

    End Class

End Namespace

