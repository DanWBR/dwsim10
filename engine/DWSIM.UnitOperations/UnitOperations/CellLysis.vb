'    Cell Lysis / High-Pressure Homogenizer
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

    Public Enum LysisTechnology
        HighPressureHomogenizer = 0
        BeadMill = 1
        Chemical = 2
        Enzymatic = 3
        Osmotic = 4
        ''' <summary>Ultrasonic / sonication lysis (acoustic cavitation). Hetherington parameters are
        ''' reinterpreted: N = number of sonication cycles (or seconds / 10 s unit), P = acoustic
        ''' power density (W/mL or similar), with k, Î± fit to the chosen basis.</summary>
        Ultrasound = 5
    End Enum

    ''' <summary>
    ''' Cell lysis / homogenizer unit. Uses the Hetherington correlation
    '''   R = 1 âˆ’ exp(âˆ’k Â· N Â· P^Î±)
    ''' to compute the release fraction R for each intracellular compound (per-compound k and Î± via
    ''' default suggestions based on MW). The resulting fraction is routed to the Lysate outlet; the
    ''' complement is routed to the Debris outlet along with the biomass compound itself.
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_CellLysis

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

        Public Property Technology As LysisTechnology = LysisTechnology.HighPressureHomogenizer
        Public Property Passes As Integer = 2
        Public Property Pressure_MPa As Double = 80.0
        Public Property HetheringtonK As Double = 0.0045
        Public Property HetheringtonAlpha As Double = 2.0
        Public Property BiomassCompound As String = ""
        Public Property DefaultReleaseFraction As Double = 0.9
        Public Property ReleaseFraction As Dictionary(Of String, Double)

        ' Ultrasound / sonication parameters. First-order kinetic model with acoustic power density:
        '   R_u = 1 - exp(-k_u * (P_a^beta) * t)
        ' where P_a is the acoustic power density (W/mL) and t the total sonication time (s). The
        ' defaults correspond to moderately tough microbial cells at bench-scale probe sonication.
        Public Property Ultrasound_PowerDensity_WmL As Double = 0.5
        Public Property Ultrasound_Time_s As Double = 300.0
        Public Property Ultrasound_k As Double = 0.008
        Public Property Ultrasound_Beta As Double = 1.2

        Public Property Result_FeedMass_kgs As Double = 0.0
        Public Property Result_LysateMass_kgs As Double = 0.0
        Public Property Result_DebrisMass_kgs As Double = 0.0
        Public Property Result_OverallRelease As Double = 0.0

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Sub New()
            MyBase.New()
            ReleaseFraction = New Dictionary(Of String, Double)()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
            ReleaseFraction = New Dictionary(Of String, Double)()
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_CellLysis()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of UnitOp_CellLysis)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Hetherington release factor for a given compound: R = 1 âˆ’ exp(âˆ’kÂ·NÂ·P^Î±).</summary>
        Public Function HetheringtonRelease() As Double
            Return 1.0 - Exp(-HetheringtonK * Passes * Pow(Pressure_MPa, HetheringtonAlpha))
        End Function

        ''' <summary>Ultrasound release factor: R_u = 1 âˆ’ exp(âˆ’k_u Â· P_a^Î² Â· t).</summary>
        Public Function UltrasoundRelease() As Double
            Dim Pa = Max(0.0, Ultrasound_PowerDensity_WmL)
            Dim t = Max(0.0, Ultrasound_Time_s)
            Return 1.0 - Exp(-Ultrasound_k * Pow(Pa, Ultrasound_Beta) * t)
        End Function

        ''' <summary>Model-agnostic intrinsic release factor for intracellular macromolecules.</summary>
        Public Function IntrinsicRelease() As Double
            If Technology = LysisTechnology.Ultrasound Then Return UltrasoundRelease()
            Return HetheringtonRelease()
        End Function

        ''' <summary>Returns the user-set release fraction for a compound, or a MW-based default capped by the active model's R.</summary>
        Public Function ReleaseFor(compName As String, mw As Double) As Double
            If ReleaseFraction IsNot Nothing AndAlso ReleaseFraction.ContainsKey(compName) Then
                Return Max(0.0, Min(1.0, ReleaseFraction(compName)))
            End If
            ' Only "intracellular-like" compounds (macromolecules, MW > 5 kDa) are subject to mechanical release
            If mw > 5000.0 Then
                Return Max(0.0, Min(1.0, IntrinsicRelease() * DefaultReleaseFraction))
            End If
            ' Small solutes diffuse freely out of lysed cells â†’ fully in lysate
            Return 1.0
        End Function

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("CellLysis: Feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("CellLysis: Both Lysate and Debris outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)

            Dim T = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim m_total = feed.Phases(0).Properties.massflow.GetValueOrDefault

            Dim lys As New Dictionary(Of String, Double)
            Dim deb As New Dictionary(Of String, Double)
            Dim m_l As Double = 0.0, m_d As Double = 0.0
            Dim macro_in As Double = 0.0, macro_rel As Double = 0.0

            For Each c In feed.Phases(0).Compounds.Values
                Dim m_in = c.MassFraction.GetValueOrDefault * m_total
                Dim mw = If(c.ConstantProperties IsNot Nothing, c.ConstantProperties.Molar_Weight, 0.0)
                Dim r As Double
                If c.Name = BiomassCompound AndAlso Not String.IsNullOrEmpty(BiomassCompound) Then
                    ' Biomass (whole/partial cells) always routes to debris
                    r = 0.0
                Else
                    r = ReleaseFor(c.Name, mw)
                End If
                lys(c.Name) = m_in * r
                deb(c.Name) = m_in * (1.0 - r)
                m_l += lys(c.Name) : m_d += deb(c.Name)
                If mw > 5000.0 AndAlso c.Name <> BiomassCompound Then
                    macro_in += m_in : macro_rel += lys(c.Name)
                End If
            Next

            Result_FeedMass_kgs = m_total
            Result_LysateMass_kgs = m_l
            Result_DebrisMass_kgs = m_d
            If macro_in > 0 Then Result_OverallRelease = macro_rel / macro_in Else Result_OverallRelease = 0.0

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        lys, m_l, T, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        deb, m_d, T, P)

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
            Return "Cell Lysis / High-Pressure Homogenizer"
        End Function
        Public Overrides Function GetDisplayName() As String
            Return "Cell Lysis"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("CellLysis: " & Me.GraphicObject.Tag)
            s.AppendLine("Technology:     " & Technology.ToString())
            If Technology = LysisTechnology.Ultrasound Then
                s.AppendLine("Power density:  " & Ultrasound_PowerDensity_WmL.ToString(numberformat, ci) & " W/mL")
                s.AppendLine("Sonication:     " & Ultrasound_Time_s.ToString(numberformat, ci) & " s")
                s.AppendLine("Ultrasound R:   " & (UltrasoundRelease() * 100).ToString(numberformat, ci) & " %")
            Else
                s.AppendLine("Passes:         " & Passes.ToString())
                s.AppendLine("Pressure:       " & Pressure_MPa.ToString(numberformat, ci) & " MPa")
                s.AppendLine("Hetherington R: " & (HetheringtonRelease() * 100).ToString(numberformat, ci) & " %")
            End If
            s.AppendLine()
            s.AppendLine("Feed:           " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Lysate:         " & Result_LysateMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Debris:         " & Result_DebrisMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Overall release (MW > 5 kDa): " & (Result_OverallRelease * 100).ToString(numberformat, ci) & " %")
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Technology", "Passes", "Pressure", "Hetherington k", "Hetherington alpha",
            "Biomass Compound", "Default Release Fraction",
            "Ultrasound Power Density", "Ultrasound Time", "Ultrasound k", "Ultrasound Beta"}
        Private Shared ReadOnly _outputProps As String() = {
            "Feed Mass", "Lysate Mass", "Debris Mass", "Overall Release"}

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
                Case "Passes" : Return Passes
                Case "Pressure" : Return Pressure_MPa
                Case "Hetherington k" : Return HetheringtonK
                Case "Hetherington alpha" : Return HetheringtonAlpha
                Case "Biomass Compound" : Return BiomassCompound
                Case "Default Release Fraction" : Return DefaultReleaseFraction
                Case "Ultrasound Power Density" : Return Ultrasound_PowerDensity_WmL
                Case "Ultrasound Time" : Return Ultrasound_Time_s
                Case "Ultrasound k" : Return Ultrasound_k
                Case "Ultrasound Beta" : Return Ultrasound_Beta
                Case "Feed Mass" : Return Result_FeedMass_kgs
                Case "Lysate Mass" : Return Result_LysateMass_kgs
                Case "Debris Mass" : Return Result_DebrisMass_kgs
                Case "Overall Release" : Return Result_OverallRelease
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Pressure" : Return "MPa"
                Case "Ultrasound Power Density" : Return "W/mL"
                Case "Ultrasound Time" : Return "s"
                Case "Feed Mass", "Lysate Mass", "Debris Mass" : Return "kg/s"
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
                    Dim t As LysisTechnology
                    If [Enum].TryParse(Of LysisTechnology)(propval?.ToString(), t) Then Technology = t
                    Return True
                Case "Passes" : Passes = CInt(Math.Max(1, d)) : Return True
                Case "Pressure" : Pressure_MPa = d : Return True
                Case "Hetherington k" : HetheringtonK = d : Return True
                Case "Hetherington alpha" : HetheringtonAlpha = d : Return True
                Case "Biomass Compound" : BiomassCompound = propval?.ToString() : Return True
                Case "Default Release Fraction" : DefaultReleaseFraction = d : Return True
                Case "Ultrasound Power Density" : Ultrasound_PowerDensity_WmL = d : Return True
                Case "Ultrasound Time" : Ultrasound_Time_s = d : Return True
                Case "Ultrasound k" : Ultrasound_k = d : Return True
                Case "Ultrasound Beta" : Ultrasound_Beta = d : Return True
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
                Return "LYS-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_CellLysis()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then
                PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel))
                Return
            End If
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("Lysis Configuration")

            container.CreateAndAddDropDownRow("Technology",
                                              New List(Of String)({"High-Pressure Homogenizer", "Bead Mill", "Chemical", "Enzymatic", "Osmotic", "Ultrasound"}),
                                              Technology,
                                              Sub(dd, e)
                                                  Technology = CType(dd.SelectedIndex, LysisTechnology)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()
            Dim bioIdx = Math.Max(0, compIds.IndexOf(BiomassCompound))
            container.CreateAndAddDropDownRow("Biomass Compound",
                                              New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                              If(String.IsNullOrEmpty(BiomassCompound), 0, bioIdx + 1),
                                              Sub(dd, e)
                                                  BiomassCompound = If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), "")
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Hetherington Parameters (R = 1 - exp(-kÂ·NÂ·P^Î±))")

            container.CreateAndAddTextBoxRow(nf, "Number of Passes (N)", Passes,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Passes = CInt(tb.Text.ParseExpressionToDouble())
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Homogenization Pressure (MPa)", Pressure_MPa,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Pressure_MPa = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Hetherington k", HetheringtonK,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HetheringtonK = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Hetherington Î±", HetheringtonAlpha,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HetheringtonAlpha = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Default Release Fraction (0-1)", DefaultReleaseFraction,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DefaultReleaseFraction = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Ultrasound Parameters (R = 1 - exp(-kÂ·P^Î²Â·t))")

            container.CreateAndAddTextBoxRow(nf, "Acoustic Power Density (W/mL)", Ultrasound_PowerDensity_WmL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Ultrasound_PowerDensity_WmL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Sonication Time (s)", Ultrasound_Time_s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Ultrasound_Time_s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Ultrasound k", Ultrasound_k,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Ultrasound_k = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Ultrasound Î²", Ultrasound_Beta,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Ultrasound_Beta = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Cell Broth"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.3 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Lysate"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + w, gy + 0.7 * h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Debris"
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Cell Broth"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.3 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Lysate"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.7 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Debris"})
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
                    "cell_lysis_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' High-pressure homogenizer: horizontal pump block + drive motor on left + feed hopper + high-pressure valve (red block) + discharge pipe.
            Dim skid As New SKRect(gx + 0.02F * w, gy + 0.82F * h, gx + 0.98F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            ' motor on left
            Dim motor As New SKRect(gx + 0.05F * w, gy + 0.35F * h, gx + 0.28F * w, gy + 0.75F * h)
            BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
            ' pump/valve block (horizontal cylinder)
            Dim pump As New SKRect(gx + 0.28F * w, gy + 0.3F * h, gx + 0.85F * w, gy + 0.75F * h)
            BioOpsDrawHelper.DrawHorizontalTank(canvas, pump, mono)
            ' red valve in middle
            Using v As New SKPaint With {.Color = If(mono, New SKColor(120, 120, 120), New SKColor(180, 60, 60)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(gx + 0.54F * w, gy + 0.35F * h, gx + 0.62F * w, gy + 0.7F * h), v)
            End Using
            Using s As New SKPaint With {.Color = If(mono, New SKColor(30, 30, 30), New SKColor(50, 65, 85)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.2F, .IsAntialias = True}
                canvas.DrawRect(New SKRect(gx + 0.54F * w, gy + 0.35F * h, gx + 0.62F * w, gy + 0.7F * h), s)
            End Using
            ' feed hopper (triangle) above
            Dim hopper As New SKPath()
            hopper.MoveTo(gx + 0.35F * w, gy + 0.1F * h)
            hopper.LineTo(gx + 0.55F * w, gy + 0.1F * h)
            hopper.LineTo(gx + 0.48F * w, gy + 0.32F * h)
            hopper.LineTo(gx + 0.42F * w, gy + 0.32F * h)
            hopper.Close()
            Using fill As New SKPaint With {.Color = If(mono, New SKColor(200, 200, 200), New SKColor(200, 215, 230)), .IsAntialias = True}
                canvas.DrawPath(hopper, fill)
            End Using
            Using stroke As New SKPaint With {.Color = If(mono, New SKColor(30, 30, 30), New SKColor(50, 65, 85)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.2F, .IsAntialias = True}
                canvas.DrawPath(hopper, stroke)
            End Using
            ' pressure gauge
            BioOpsDrawHelper.DrawGauge(canvas, gx + 0.75F * w, gy + 0.22F * h, 0.06F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.75F * w, gy + 0.3F * h), New SKPoint(gx + 0.75F * w, gy + 0.35F * h), 0.02F * w, mono)
        End Sub

    End Class

End Namespace
