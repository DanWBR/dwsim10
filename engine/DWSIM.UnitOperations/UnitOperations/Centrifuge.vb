'    Centrifuge (disk-stack / decanter / tubular) - Calculation Routines
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports DWSIM.Thermodynamics.BaseClasses
Imports System.Math
Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports SkiaSharp
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics
Imports DWSIM.UnitOperations.Streams
Imports System.Collections.Generic
Imports DWSIM.UI.Shared.Avalonia

Namespace UnitOperations

    Public Enum CentrifugeType
        DiskStack = 0
        Decanter = 1
        Tubular = 2
    End Enum

    ''' <summary>
    ''' Solids / liquid centrifuge (disk-stack / decanter / tubular). Splits the inlet between a
    ''' Heavy (concentrate / cake) outlet and a Light (clarified) outlet using per-compound
    ''' recovery-to-heavy fractions r_i âˆˆ [0, 1]. Default r_i is suggested from compound MW
    ''' (macromolecules â†’ heavy, small solutes â†’ light).
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_Centrifuge

        Inherits UnitOperations.UnitOpBaseClass

        Implements IExternalUnitOperation
        Public ReadOnly Property IsBio As Boolean = True

        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Separators
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        Public Property Technology As CentrifugeType = CentrifugeType.DiskStack
        Public Property BowlSpeed_rpm As Double = 6000.0
        Public Property SigmaFactor_m2 As Double = 1000.0
        Public Property DefaultRecoveryToHeavy As Double = 0.05
        Public Property RecoveryToHeavy As Dictionary(Of String, Double)

        Public Property Result_FeedMass_kgs As Double = 0.0
        Public Property Result_HeavyMass_kgs As Double = 0.0
        Public Property Result_LightMass_kgs As Double = 0.0
        Public Property Result_SolidsRecovery As Double = 0.0

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Sub New()
            MyBase.New()
            RecoveryToHeavy = New Dictionary(Of String, Double)()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
            RecoveryToHeavy = New Dictionary(Of String, Double)()
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_Centrifuge()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of UnitOp_Centrifuge)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Function RecoveryFor(compName As String) As Double
            If RecoveryToHeavy IsNot Nothing AndAlso RecoveryToHeavy.ContainsKey(compName) Then
                Return Max(0.0, Min(1.0, RecoveryToHeavy(compName)))
            End If
            Return Max(0.0, Min(1.0, DefaultRecoveryToHeavy))
        End Function

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Centrifuge: Feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("Centrifuge: Both Heavy and Light outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)

            Dim T = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim m_total = feed.Phases(0).Properties.massflow.GetValueOrDefault

            Dim feedComp As New Dictionary(Of String, Double)
            For Each c In feed.Phases(0).Compounds.Values
                feedComp(c.Name) = c.MassFraction.GetValueOrDefault * m_total
            Next

            Dim heavy As New Dictionary(Of String, Double)
            Dim light As New Dictionary(Of String, Double)
            Dim m_h As Double = 0.0, m_l As Double = 0.0
            For Each kv In feedComp
                Dim r = RecoveryFor(kv.Key)
                heavy(kv.Key) = kv.Value * r
                light(kv.Key) = kv.Value * (1.0 - r)
                m_h += heavy(kv.Key) : m_l += light(kv.Key)
            Next

            Result_FeedMass_kgs = m_total
            Result_HeavyMass_kgs = m_h
            Result_LightMass_kgs = m_l
            ' Solids recovery: if a "biomass-like" compound (MW > 10000) is present, report its recovery
            Dim macro_in As Double = 0.0, macro_h As Double = 0.0
            For Each c In feed.Phases(0).Compounds.Values
                If c.ConstantProperties IsNot Nothing AndAlso c.ConstantProperties.Molar_Weight > 10000.0 Then
                    macro_in += feedComp(c.Name) : macro_h += heavy(c.Name)
                End If
            Next
            If macro_in > 0 Then Result_SolidsRecovery = macro_h / macro_in Else Result_SolidsRecovery = 0.0

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        heavy, m_h, T, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        light, m_l, T, P)

        End Sub

        Private Shared Sub WriteStream(ms As MaterialStream, m As Dictionary(Of String, Double), total As Double, T As Double, P As Double)
            With ms
                .ClearAllProps()
                .Phases(0).Properties.temperature = T
                .Phases(0).Properties.pressure = P
                If total > 0 Then
                    For Each c In .Phases(0).Compounds.Values
                        c.MassFraction = If(m.ContainsKey(c.Name), m(c.Name), 0.0) / total
                    Next
                    Dim invMW As Double = 0.0
                    For Each c In .Phases(0).Compounds.Values
                        invMW += c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight
                    Next
                    If invMW > 0 Then
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = (c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight) / invMW
                        Next
                    End If
                End If
                .Phases(0).Properties.massflow = total
                .DefinedFlow = FlowSpec.Mass
                .SpecType = StreamSpec.Temperature_and_Pressure
            End With
        End Sub

        Public Overrides Sub DeCalculate()
            For i = 0 To Math.Min(1, Me.GraphicObject.OutputConnectors.Count - 1)
                Dim cp = Me.GraphicObject.OutputConnectors(i)
                If cp.IsAttached Then
                    Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                    With ms
                        .Phases(0).Properties.temperature = Nothing
                        .Phases(0).Properties.pressure = Nothing
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = 0 : c.MassFraction = 0
                        Next
                        .Phases(0).Properties.massflow = Nothing
                        .GraphicObject.Calculated = False
                    End With
                End If
            Next
        End Sub

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function
        Public Overrides Function GetDisplayDescription() As String
            Return "Centrifuge (disk-stack / decanter / tubular)"
        End Function
        Public Overrides Function GetDisplayName() As String
            Return "Centrifuge"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Centrifuge: " & Me.GraphicObject.Tag)
            s.AppendLine("Technology:    " & Technology.ToString())
            s.AppendLine("Bowl Speed:    " & BowlSpeed_rpm.ToString(numberformat, ci) & " rpm")
            s.AppendLine("Sigma factor:  " & SigmaFactor_m2.ToString(numberformat, ci) & " m2")
            s.AppendLine()
            s.AppendLine("Feed:     " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Heavy:    " & Result_HeavyMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Light:    " & Result_LightMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Solids recovery (macro MW>10 kDa): " & (Result_SolidsRecovery * 100).ToString(numberformat, ci) & " %")
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {"Technology", "Bowl Speed", "Sigma Factor", "Default Recovery To Heavy"}
        Private Shared ReadOnly _outputProps As String() = {"Feed Mass Flow", "Heavy Mass Flow", "Light Mass Flow", "Solids Recovery"}

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR : Return _inputProps
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Technology" : Return Technology.ToString()
                Case "Bowl Speed" : Return BowlSpeed_rpm
                Case "Sigma Factor" : Return SigmaFactor_m2
                Case "Default Recovery To Heavy" : Return DefaultRecoveryToHeavy
                Case "Feed Mass Flow" : Return Result_FeedMass_kgs
                Case "Heavy Mass Flow" : Return Result_HeavyMass_kgs
                Case "Light Mass Flow" : Return Result_LightMass_kgs
                Case "Solids Recovery" : Return Result_SolidsRecovery
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Bowl Speed" : Return "rpm"
                Case "Sigma Factor" : Return "m2"
                Case "Feed Mass Flow", "Heavy Mass Flow", "Light Mass Flow" : Return "kg/s"
                Case Else : Return "-"
            End Select
        End Function

        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean
            Dim d As Double = 0.0
            If TypeOf propval Is Double Then
                d = CDbl(propval)
            ElseIf TypeOf propval Is String Then
                Double.TryParse(CStr(propval), Globalization.NumberStyles.Any, Globalization.CultureInfo.CurrentCulture, d)
            End If
            Select Case prop
                Case "Technology"
                    Dim t As CentrifugeType
                    If [Enum].TryParse(Of CentrifugeType)(propval?.ToString(), t) Then Technology = t
                    Return True
                Case "Bowl Speed" : BowlSpeed_rpm = d : Return True
                Case "Sigma Factor" : SigmaFactor_m2 = d : Return True
                Case "Default Recovery To Heavy" : DefaultRecoveryToHeavy = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' IExternalUnitOperation
        Private ReadOnly Property IEUO_Name As String Implements IExternalUnitOperation.Name
            Get
                Return GetDisplayName()
            End Get
        End Property
        Private ReadOnly Property IEUO_Description As String Implements IExternalUnitOperation.Description
            Get
                Return GetDisplayDescription()
            End Get
        End Property
        Public ReadOnly Property Prefix As String Implements IExternalUnitOperation.Prefix
            Get
                Return "CF-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_Centrifuge()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then
                PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel))
                Return
            End If
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("Centrifuge Configuration")

            container.CreateAndAddDropDownRow("Centrifuge Type",
                                              New List(Of String)({"Disk-Stack", "Decanter", "Tubular"}),
                                              Technology,
                                              Sub(dd, e)
                                                  Technology = CType(dd.SelectedIndex, CentrifugeType)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddTextBoxRow(nf, "Bowl Speed (rpm)", BowlSpeed_rpm,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     BowlSpeed_rpm = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Sigma Factor (mÂ²)", SigmaFactor_m2,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SigmaFactor_m2 = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Separation")

            container.CreateAndAddTextBoxRow(nf, "Default Recovery to Heavy (0-1)", DefaultRecoveryToHeavy,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DefaultRecoveryToHeavy = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddDescriptionRow("Per-compound recovery-to-heavy fractions are set to the default above. Override individual compounds via the Windows editing form.")

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.8 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Heavy (Concentrate)"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + w, gy + 0.2 * h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Light (Clarified)"
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Feed"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.8 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Heavy (Concentrate)"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.2 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Light (Clarified)"})
            End If
            GraphicObject.EnergyConnector.Active = False
        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw
            If GraphicObject Is Nothing Then Return
            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            If GraphicObject.DrawMode = 2 Then
                If BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "centrifuge_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Disc-stack centrifuge: motor on top, rounded body on a skid, feed on top-left, two discharges on right.
            Dim skid As New SKRect(gx + 0.05F * w, gy + 0.8F * h, gx + 0.95F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            Dim bodyRect As New SKRect(gx + 0.2F * w, gy + 0.3F * h, gx + 0.78F * w, gy + 0.82F * h)
            BioOpsDrawHelper.DrawVerticalTank(canvas, bodyRect, mono)
            ' disc-stack hint: three horizontal stripes inside the bowl
            Using band As New SKPaint With {.Color = BioOpsDrawHelper.ClrStrokeLight(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.7F, .IsAntialias = True}
                For i = 0 To 2
                    Dim yy = bodyRect.Top + bodyRect.Height * (0.25F + i * 0.18F)
                    canvas.DrawLine(bodyRect.Left + 3, yy, bodyRect.Right - 3, yy, band)
                Next
            End Using
            ' mounting flange between motor and bowl
            Dim cxB = (bodyRect.Left + bodyRect.Right) * 0.5F
            BioOpsDrawHelper.DrawFlange(canvas, cxB, gy + 0.3F * h, 0.32F * w, mono)
            ' motor on top centered
            Dim motor As New SKRect(gx + 0.4F * w, gy + 0.08F * h, gx + 0.58F * w, gy + 0.28F * h)
            BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
            ' small pressure gauge on the bowl
            BioOpsDrawHelper.DrawGauge(canvas, gx + 0.7F * w, gy + 0.36F * h, 0.045F * w, mono)
            ' feed pipe from top-left with flange at motor
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.05F * w, gy + 0.2F * h), New SKPoint(gx + 0.4F * w, gy + 0.2F * h), 0.05F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.4F * w, gy + 0.2F * h, 0.09F * w, mono)
            ' two discharge nozzles on right with flanges
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.78F * w, gy + 0.5F * h), New SKPoint(gx + w, gy + 0.5F * h), 0.045F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.78F * w, gy + 0.5F * h, 0.08F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.78F * w, gy + 0.72F * h), New SKPoint(gx + w, gy + 0.72F * h), 0.045F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.78F * w, gy + 0.72F * h, 0.08F * w, mono)
        End Sub

    End Class

End Namespace
