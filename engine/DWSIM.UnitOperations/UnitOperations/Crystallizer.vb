'    Crystallizer (cooling / evaporative / antisolvent)
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

    Public Enum CrystallizerMode
        Cooling = 0
        Evaporative = 1
        Antisolvent = 2
    End Enum

    ''' <summary>
    ''' Crystallizer (cooling / evaporative / antisolvent). Splits the inlet between a Crystals outlet
    ''' and a Mother-Liquor outlet. Solubility of the target solute in the solvent is described by a
    ''' modified Apelblat/Van't-Hoff form:
    '''   C_sat(T)  [g solute / g solvent]  =  A + BÂ·(T âˆ’ 298) + CÂ·(T âˆ’ 298)^2
    ''' At steady state, mass crystallized = max(0, m_solute_in âˆ’ C_sat Â· m_solvent_in).
    ''' In Evaporative mode the solvent mass is reduced by EvaporationFraction before the check.
    ''' In Antisolvent mode, an additional stream adds solvent that "dilutes" the solubility by a
    ''' user-set factor (SolubilityReductionByAntisolvent âˆˆ [0, 1]).
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_Crystallizer

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

        Public Property Mode As CrystallizerMode = CrystallizerMode.Cooling
        Public Property SoluteCompound As String = ""
        Public Property SolventCompound As String = "Water"
        Public Property OperatingT_K As Double = 278.15 ' 5 Â°C for cooling
        Public Property Sol_A As Double = 0.35
        Public Property Sol_B As Double = 0.005
        Public Property Sol_C As Double = 0.0
        Public Property EvaporationFraction As Double = 0.30
        Public Property SolubilityReductionByAntisolvent As Double = 0.7
        Public Property MeanCrystalSize_um As Double = 200.0 ' reported only

        Public Property Result_SoluteInFeed_kgs As Double = 0.0
        Public Property Result_Cryst_kgs As Double = 0.0
        Public Property Result_MotherLiquor_kgs As Double = 0.0
        Public Property Result_Yield As Double = 0.0
        Public Property Result_Csat_gg As Double = 0.0

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_Crystallizer()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of UnitOp_Crystallizer)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Function SolubilityAt(T_K As Double) As Double
            Dim x = T_K - 298.15
            Return Max(0.0, Sol_A + Sol_B * x + Sol_C * x * x)
        End Function

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If String.IsNullOrEmpty(SoluteCompound) Then _
                Throw New Exception("Crystallizer: Solute compound not selected.")
            If String.IsNullOrEmpty(SolventCompound) Then _
                Throw New Exception("Crystallizer: Solvent compound not selected.")
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Crystallizer: Feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("Crystallizer: Both Crystals and Mother Liquor outlets must be connected.")
            End If

            Dim feed As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream)
            Dim antisolvent As MaterialStream = Nothing
            If Me.GraphicObject.InputConnectors.Count > 1 AndAlso Me.GraphicObject.InputConnectors(1).IsAttached Then
                antisolvent = DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(1).AttachedConnector.AttachedFrom.Name), MaterialStream)
            End If

            Dim T_in = feed.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = feed.Phases(0).Properties.pressure.GetValueOrDefault
            Dim m_total = feed.Phases(0).Properties.massflow.GetValueOrDefault

            Dim feedComp As New Dictionary(Of String, Double)
            For Each c In feed.Phases(0).Compounds.Values
                feedComp(c.Name) = c.MassFraction.GetValueOrDefault * m_total
            Next
            If antisolvent IsNot Nothing Then
                Dim m_as = antisolvent.Phases(0).Properties.massflow.GetValueOrDefault
                For Each c In antisolvent.Phases(0).Compounds.Values
                    Dim mf = c.MassFraction.GetValueOrDefault * m_as
                    If feedComp.ContainsKey(c.Name) Then
                        feedComp(c.Name) += mf
                    Else
                        feedComp(c.Name) = mf
                    End If
                Next
                m_total += m_as
            End If

            Dim m_solute As Double = 0.0, m_solvent As Double = 0.0
            If feedComp.ContainsKey(SoluteCompound) Then m_solute = feedComp(SoluteCompound)
            If feedComp.ContainsKey(SolventCompound) Then m_solvent = feedComp(SolventCompound)

            Dim T_op As Double = T_in
            Dim solvent_effective As Double = m_solvent

            Select Case Mode
                Case CrystallizerMode.Cooling
                    T_op = OperatingT_K
                Case CrystallizerMode.Evaporative
                    T_op = OperatingT_K ' may be elevated to boiling
                    solvent_effective = m_solvent * Max(0.0, 1.0 - Max(0.0, Min(1.0, EvaporationFraction)))
                Case CrystallizerMode.Antisolvent
                    ' Keep inlet T; antisolvent already added. Effective solubility dropped by user factor.
                    T_op = T_in
            End Select

            Dim Csat = SolubilityAt(T_op)
            If Mode = CrystallizerMode.Antisolvent Then
                Csat *= Max(0.0, Min(1.0, 1.0 - SolubilityReductionByAntisolvent))
            End If
            Result_Csat_gg = Csat

            Dim max_dissolved = Csat * solvent_effective
            Dim m_cryst = Max(0.0, m_solute - max_dissolved)
            Dim m_solute_in_liquor = m_solute - m_cryst

            ' Build outlet streams. Crystals outlet: essentially pure solute (crystallized). Mother
            ' liquor: everything else (remaining solute + all solvent + all impurities + evaporated
            ' solvent is reported in the solvent-loss but we return only two streams, so we keep the
            ' un-evaporated solvent in mother liquor).
            Dim cryst As New Dictionary(Of String, Double)
            Dim liquor As New Dictionary(Of String, Double)
            For Each kv In feedComp
                If kv.Key = SoluteCompound Then
                    cryst(kv.Key) = m_cryst
                    liquor(kv.Key) = m_solute_in_liquor
                ElseIf kv.Key = SolventCompound Then
                    cryst(kv.Key) = 0.0
                    ' mother liquor keeps effective solvent (post-evaporation in Evaporative mode)
                    liquor(kv.Key) = solvent_effective
                Else
                    cryst(kv.Key) = 0.0
                    liquor(kv.Key) = kv.Value
                End If
            Next

            Dim m_c As Double = 0.0, m_l As Double = 0.0
            For Each v In cryst.Values : m_c += v : Next
            For Each v In liquor.Values : m_l += v : Next

            Result_SoluteInFeed_kgs = m_solute
            Result_Cryst_kgs = m_c
            Result_MotherLiquor_kgs = m_l
            If m_solute > 0 Then Result_Yield = m_c / m_solute Else Result_Yield = 0.0

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        cryst, m_c, T_op, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        liquor, m_l, T_op, P)

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
            Return "Crystallizer (cooling / evaporative / antisolvent)"
        End Function
        Public Overrides Function GetDisplayName() As String
            Return "Crystallizer"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Crystallizer: " & Me.GraphicObject.Tag)
            s.AppendLine("Mode:        " & Mode.ToString())
            s.AppendLine("Solute:      " & SoluteCompound)
            s.AppendLine("Solvent:     " & SolventCompound)
            s.AppendLine("Operating T: " & OperatingT_K.ToString(numberformat, ci) & " K")
            s.AppendLine("Csat:        " & Result_Csat_gg.ToString(numberformat, ci) & " g/g solvent")
            s.AppendLine()
            s.AppendLine("Solute in feed:      " & Result_SoluteInFeed_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Crystallized:        " & Result_Cryst_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Mother liquor:       " & Result_MotherLiquor_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Crystallization yield: " & (Result_Yield * 100).ToString(numberformat, ci) & " %")
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Mode", "Solute Compound", "Solvent Compound", "Operating T",
            "Solubility A", "Solubility B", "Solubility C",
            "Evaporation Fraction", "Solubility Reduction By Antisolvent", "Mean Crystal Size"}
        Private Shared ReadOnly _outputProps As String() = {
            "Solute In Feed", "Crystallized Mass", "Mother Liquor Mass", "Crystallization Yield", "Saturation Concentration"}

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
                Case "Mode" : Return Mode.ToString()
                Case "Solute Compound" : Return SoluteCompound
                Case "Solvent Compound" : Return SolventCompound
                Case "Operating T" : Return OperatingT_K
                Case "Solubility A" : Return Sol_A
                Case "Solubility B" : Return Sol_B
                Case "Solubility C" : Return Sol_C
                Case "Evaporation Fraction" : Return EvaporationFraction
                Case "Solubility Reduction By Antisolvent" : Return SolubilityReductionByAntisolvent
                Case "Mean Crystal Size" : Return MeanCrystalSize_um
                Case "Solute In Feed" : Return Result_SoluteInFeed_kgs
                Case "Crystallized Mass" : Return Result_Cryst_kgs
                Case "Mother Liquor Mass" : Return Result_MotherLiquor_kgs
                Case "Crystallization Yield" : Return Result_Yield
                Case "Saturation Concentration" : Return Result_Csat_gg
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Operating T" : Return "K"
                Case "Mean Crystal Size" : Return "um"
                Case "Solute In Feed", "Crystallized Mass", "Mother Liquor Mass" : Return "kg/s"
                Case "Saturation Concentration" : Return "g/g"
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
                Case "Mode"
                    Dim m As CrystallizerMode
                    If [Enum].TryParse(Of CrystallizerMode)(propval?.ToString(), m) Then Me.Mode = m
                    Return True
                Case "Solute Compound" : SoluteCompound = propval?.ToString() : Return True
                Case "Solvent Compound" : SolventCompound = propval?.ToString() : Return True
                Case "Operating T" : OperatingT_K = d : Return True
                Case "Solubility A" : Sol_A = d : Return True
                Case "Solubility B" : Sol_B = d : Return True
                Case "Solubility C" : Sol_C = d : Return True
                Case "Evaporation Fraction" : EvaporationFraction = d : Return True
                Case "Solubility Reduction By Antisolvent" : SolubilityReductionByAntisolvent = d : Return True
                Case "Mean Crystal Size" : MeanCrystalSize_um = d : Return True
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
                Return "CRY-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_Crystallizer()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then
                PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel))
                Return
            End If
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat
            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()

            container.CreateAndAddLabelRow("Crystallizer Mode")

            container.CreateAndAddDropDownRow("Mode",
                                              New List(Of String)({"Cooling", "Evaporative", "Antisolvent"}),
                                              Mode,
                                              Sub(dd, e)
                                                  Mode = CType(dd.SelectedIndex, CrystallizerMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            Dim solIdx = Math.Max(0, compIds.IndexOf(SoluteCompound))
            container.CreateAndAddDropDownRow("Solute Compound",
                                              New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                              If(String.IsNullOrEmpty(SoluteCompound), 0, solIdx + 1),
                                              Sub(dd, e)
                                                  SoluteCompound = If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), "")
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            Dim solvIdx = Math.Max(0, compIds.IndexOf(SolventCompound))
            container.CreateAndAddDropDownRow("Solvent Compound",
                                              New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                              If(String.IsNullOrEmpty(SolventCompound), 0, solvIdx + 1),
                                              Sub(dd, e)
                                                  SolventCompound = If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), "")
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Operating Conditions")

            container.CreateAndAddTextBoxRow(nf, "Operating Temperature (K)", OperatingT_K,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     OperatingT_K = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Evaporation Fraction (0-1)", EvaporationFraction,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     EvaporationFraction = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Solubility Reduction by Antisolvent (0-1)", SolubilityReductionByAntisolvent,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SolubilityReductionByAntisolvent = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Solubility: Csat(T) = A + B*(T-298) + C*(T-298)^2 [g solute / g solvent]")

            container.CreateAndAddTextBoxRow(nf, "Coefficient A", Sol_A,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Sol_A = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Coefficient B", Sol_B,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Sol_B = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Coefficient C", Sol_C,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     Sol_C = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Mean Crystal Size (Î¼m, reported only)", MeanCrystalSize_um,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     MeanCrystalSize_um = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 2 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.4 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"
                GraphicObject.InputConnectors(1).Position = New Point(gx + 0.3 * w, gy)
                GraphicObject.InputConnectors(1).ConnectorName = "Antisolvent (Optional)"
                GraphicObject.InputConnectors(1).Direction = ConDir.Down
                GraphicObject.OutputConnectors(0).Position = New Point(gx + 0.7 * w, gy + h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Crystals"
                GraphicObject.OutputConnectors(0).Direction = ConDir.Up
                GraphicObject.OutputConnectors(1).Position = New Point(gx + w, gy + 0.4 * h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Mother Liquor"
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.4 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Feed"})
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.3 * w, gy), .Type = ConType.ConIn,
                    .Direction = ConDir.Down, .ConnectorName = "Antisolvent (Optional)"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.7 * w, gy + h), .Type = ConType.ConOut,
                    .Direction = ConDir.Up, .ConnectorName = "Crystals"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.4 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Mother Liquor"})
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
                    "crystallizer_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Draft-tube crystallizer: vertical tank w/ cone bottom, top agitator motor, sparkle crystals overlay.
            Dim vessel As New SKRect(gx + 0.2F * w, gy + 0.18F * h, gx + 0.8F * w, gy + 0.95F * h)
            BioOpsDrawHelper.DrawConeBottomTank(canvas, vessel, mono)
            Dim cx = (vessel.Left + vessel.Right) * 0.5F
            ' top motor and shaft
            Dim motor As New SKRect(cx - 0.08F * w, gy + 0.02F * h, cx + 0.08F * w, gy + 0.15F * h)
            BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
            BioOpsDrawHelper.DrawAgitator(canvas, cx, gy + 0.18F * h, gy + 0.62F * h, 0.22F * w, mono)
            ' draft tube hint (inner cylinder)
            Using dt As New SKPaint With {.Color = If(mono, New SKColor(120, 120, 120), New SKColor(130, 150, 175)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawRect(New SKRect(cx - 0.12F * w, gy + 0.28F * h, cx + 0.12F * w, gy + 0.7F * h), dt)
            End Using
            ' crystal sparkles inside
            Using stroke As New SKPaint With {.Color = If(mono, New SKColor(60, 60, 60), New SKColor(60, 90, 130)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                Dim pts = New Single(,) {{0.3F, 0.75F}, {0.52F, 0.82F}, {0.7F, 0.72F}, {0.42F, 0.6F}, {0.62F, 0.5F}}
                For i = 0 To pts.GetLength(0) - 1
                    Dim rx = gx + pts(i, 0) * w
                    Dim ry = gy + pts(i, 1) * h
                    Dim sz = 0.025F * w
                    canvas.DrawLine(rx - sz, ry, rx + sz, ry, stroke)
                    canvas.DrawLine(rx, ry - sz, rx, ry + sz, stroke)
                Next
            End Using
        End Sub

    End Class

End Namespace
