'    Biogas Upgrader - Calculation Routines
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

    Public Enum BiogasUpgraderTech
        WaterScrubbing = 0
        Amine = 1
        PSA = 2
        MembraneSeparation = 3
    End Enum

    ''' <summary>
    ''' Biogas upgrader. Two-stage algebraic model:
    '''   Stage 1 - H2S polishing (ZnO bed / caustic wash), default 99 % removal. Only active when
    '''             <see cref="H2SCompound"/> names a compound present in the feed; it is unassigned
    '''             by default, since a desulfurized feed is the common case.
    '''   Stage 2 - CO2 bulk removal (water scrubbing / amine / PSA / membrane), user-selectable
    '''             efficiency and CH4 loss per technology.
    ''' Splits the biogas feed into an Upgraded Gas (RNG-spec) outlet and an Off-gas outlet.
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_BiogasUpgrader

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

        Public Property Technology As BiogasUpgraderTech = BiogasUpgraderTech.Amine
        Public Property H2SRemovalEfficiency As Double = 0.99
        Public Property CO2RemovalEfficiency As Double = 0.95
        Public Property CH4LossFraction As Double = 0.01 ' CH4 that ends up in off-gas
        Public Property H2ORemovalEfficiency As Double = 0.98
        Public Property TargetCH4Purity As Double = 0.96 ' for reporting only

        Public Property MethaneCompound As String = "Methane"
        Public Property CO2Compound As String = "Carbon dioxide"
        Public Property H2SCompound As String = ""
        Public Property WaterCompound As String = "Water"
        Public Property N2Compound As String = ""

        Public Property Result_FeedMass_kgs As Double = 0.0
        Public Property Result_UpgradedMass_kgs As Double = 0.0
        Public Property Result_OffgasMass_kgs As Double = 0.0
        Public Property Result_UpgradedCH4Fraction As Double = 0.0
        Public Property Result_CH4RecoveryFraction As Double = 0.0
        Public Property Result_WobbeIndex As Double = 0.0 ' crude, reported for convenience

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
            Dim obj As ICustomXMLSerialization = New UnitOp_BiogasUpgrader()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of UnitOp_BiogasUpgrader)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Apply default removal efficiencies for the selected technology.</summary>
        Public Sub ApplyTechnologyDefaults()
            Select Case Technology
                Case BiogasUpgraderTech.WaterScrubbing
                    CO2RemovalEfficiency = 0.92 : CH4LossFraction = 0.02
                Case BiogasUpgraderTech.Amine
                    CO2RemovalEfficiency = 0.99 : CH4LossFraction = 0.001
                Case BiogasUpgraderTech.PSA
                    CO2RemovalEfficiency = 0.95 : CH4LossFraction = 0.03
                Case BiogasUpgraderTech.MembraneSeparation
                    CO2RemovalEfficiency = 0.90 : CH4LossFraction = 0.02
            End Select
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("BiogasUpgrader: Biogas feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("BiogasUpgrader: Both Upgraded and Offgas outlets must be connected.")
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

            ' H2S is only routed to the offgas when the H2S compound role is assigned. Leaving it
            ' unassigned while the feed actually carries H2S sends all of it to the upgraded gas and
            ' silently ignores H2SRemovalEfficiency, so say so rather than fail quietly.
            If String.IsNullOrEmpty(H2SCompound) AndAlso FlowSheet IsNot Nothing Then
                Dim h2s = feed.Phases(0).Compounds.Values.FirstOrDefault(
                    Function(c) (c.ConstantProperties.Formula = "H2S" OrElse
                                 c.ConstantProperties.CAS_Number = "7783-06-4") AndAlso
                                feedComp(c.Name) > 0.0)
                If h2s IsNot Nothing Then
                    FlowSheet.ShowMessage(GraphicObject.Tag & ": feed contains " & h2s.Name &
                        " but no H2S compound is assigned, so the H2S removal efficiency is ignored" &
                        " and the H2S passes through to the upgraded gas. Set the H2S Compound role" &
                        " to remove it.", IFlowsheet.MessageType.Warning)
                End If
            End If

            Dim upg As New Dictionary(Of String, Double)
            Dim off As New Dictionary(Of String, Double)

            ' Per-compound split rule: user specifies a "fraction-to-offgas" for the key species,
            ' everything else defaults to upgraded stream (clean).
            For Each kv In feedComp
                Dim toOff As Double = 0.0
                Dim name = kv.Key
                If name = CO2Compound Then
                    toOff = Max(0.0, Min(1.0, CO2RemovalEfficiency))
                ElseIf name = H2SCompound AndAlso Not String.IsNullOrEmpty(H2SCompound) Then
                    toOff = Max(0.0, Min(1.0, H2SRemovalEfficiency))
                ElseIf name = MethaneCompound Then
                    toOff = Max(0.0, Min(1.0, CH4LossFraction))
                ElseIf name = WaterCompound AndAlso Not String.IsNullOrEmpty(WaterCompound) Then
                    toOff = Max(0.0, Min(1.0, H2ORemovalEfficiency))
                Else
                    ' Inerts / trace gases (N2, O2, etc.) carry through mostly to upgraded
                    toOff = 0.0
                End If
                off(kv.Key) = kv.Value * toOff
                upg(kv.Key) = kv.Value * (1.0 - toOff)
            Next

            Dim m_upg As Double = 0.0, m_off As Double = 0.0
            For Each v In upg.Values : m_upg += v : Next
            For Each v In off.Values : m_off += v : Next

            Result_FeedMass_kgs = m_total
            Result_UpgradedMass_kgs = m_upg
            Result_OffgasMass_kgs = m_off

            ' CH4 mass fraction and mole fraction in upgraded gas (if CH4 is present)
            Dim ch4_mass_upg As Double = 0.0
            If upg.ContainsKey(MethaneCompound) Then ch4_mass_upg = upg(MethaneCompound)
            If m_upg > 0 Then Result_UpgradedCH4Fraction = ch4_mass_upg / m_upg Else Result_UpgradedCH4Fraction = 0.0

            Dim ch4_feed As Double = 0.0
            If feedComp.ContainsKey(MethaneCompound) Then ch4_feed = feedComp(MethaneCompound)
            If ch4_feed > 0 Then Result_CH4RecoveryFraction = ch4_mass_upg / ch4_feed Else Result_CH4RecoveryFraction = 0.0

            ' Crude Wobbe (MJ/Nm3, based on LHV of CH4=35.9 MJ/Nm3 and a mole-average MW check)
            Result_WobbeIndex = 35.9 * Result_UpgradedCH4Fraction / Sqrt(Max(0.55, 1.0))

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        upg, m_upg, T, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        off, m_off, T, P)

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
            Return "Biogas Upgrader (H2S + CO2 removal â†’ RNG)"
        End Function
        Public Overrides Function GetDisplayName() As String
            Return "Biogas Upgrader"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("BiogasUpgrader: " & Me.GraphicObject.Tag)
            s.AppendLine("Technology:   " & Technology.ToString())
            s.AppendLine("CO2 removal:  " & (CO2RemovalEfficiency * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("CH4 loss:     " & (CH4LossFraction * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("H2S removal:  " & (H2SRemovalEfficiency * 100).ToString(numberformat, ci) & " %")
            s.AppendLine()
            s.AppendLine("Feed:          " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Upgraded:      " & Result_UpgradedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Offgas:        " & Result_OffgasMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Upgraded CH4:  " & (Result_UpgradedCH4Fraction * 100).ToString(numberformat, ci) & " % (mass)")
            s.AppendLine("CH4 recovery:  " & (Result_CH4RecoveryFraction * 100).ToString(numberformat, ci) & " %")
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Technology", "H2S Removal", "CO2 Removal", "CH4 Loss", "H2O Removal", "Target CH4 Purity",
            "Methane Compound", "CO2 Compound", "H2S Compound", "Water Compound", "N2 Compound"}
        Private Shared ReadOnly _outputProps As String() = {
            "Feed Mass", "Upgraded Mass", "Offgas Mass", "Upgraded CH4 Fraction", "CH4 Recovery"}

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
                Case "H2S Removal" : Return H2SRemovalEfficiency
                Case "CO2 Removal" : Return CO2RemovalEfficiency
                Case "CH4 Loss" : Return CH4LossFraction
                Case "H2O Removal" : Return H2ORemovalEfficiency
                Case "Target CH4 Purity" : Return TargetCH4Purity
                Case "Methane Compound" : Return MethaneCompound
                Case "CO2 Compound" : Return CO2Compound
                Case "H2S Compound" : Return H2SCompound
                Case "Water Compound" : Return WaterCompound
                Case "N2 Compound" : Return N2Compound
                Case "Feed Mass" : Return Result_FeedMass_kgs
                Case "Upgraded Mass" : Return Result_UpgradedMass_kgs
                Case "Offgas Mass" : Return Result_OffgasMass_kgs
                Case "Upgraded CH4 Fraction" : Return Result_UpgradedCH4Fraction
                Case "CH4 Recovery" : Return Result_CH4RecoveryFraction
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Feed Mass", "Upgraded Mass", "Offgas Mass" : Return "kg/s"
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
                    Dim t As BiogasUpgraderTech
                    If [Enum].TryParse(Of BiogasUpgraderTech)(propval?.ToString(), t) Then Technology = t
                    Return True
                Case "H2S Removal" : H2SRemovalEfficiency = d : Return True
                Case "CO2 Removal" : CO2RemovalEfficiency = d : Return True
                Case "CH4 Loss" : CH4LossFraction = d : Return True
                Case "H2O Removal" : H2ORemovalEfficiency = d : Return True
                Case "Target CH4 Purity" : TargetCH4Purity = d : Return True
                Case "Methane Compound" : MethaneCompound = propval?.ToString() : Return True
                Case "CO2 Compound" : CO2Compound = propval?.ToString() : Return True
                Case "H2S Compound" : H2SCompound = propval?.ToString() : Return True
                Case "Water Compound" : WaterCompound = propval?.ToString() : Return True
                Case "N2 Compound" : N2Compound = propval?.ToString() : Return True
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
                Return "BGU-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_BiogasUpgrader()
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

            container.CreateAndAddLabelRow("Upgrader Technology")

            container.CreateAndAddDropDownRow("Technology",
                                              New List(Of String)({"Water Scrubbing", "Amine", "PSA", "Membrane Separation"}),
                                              Technology,
                                              Sub(dd, e)
                                                  Technology = CType(dd.SelectedIndex, BiogasUpgraderTech)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Removal Efficiencies (0-1)")

            container.CreateAndAddTextBoxRow(nf, "Hâ‚‚S Removal Efficiency", H2SRemovalEfficiency,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     H2SRemovalEfficiency = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "COâ‚‚ Removal Efficiency", CO2RemovalEfficiency,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CO2RemovalEfficiency = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Hâ‚‚O Removal Efficiency", H2ORemovalEfficiency,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     H2ORemovalEfficiency = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "CHâ‚„ Loss to Off-gas", CH4LossFraction,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CH4LossFraction = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Target CHâ‚„ Purity (report only)", TargetCH4Purity,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     TargetCH4Purity = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Compound Mapping")

            Dim addCompoundDropdown =
                Sub(label As String, currentValue As String, setter As Action(Of String))
                    Dim idx = compIds.IndexOf(currentValue)
                    container.CreateAndAddDropDownRow(label,
                                                      New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                                      If(idx < 0, 0, idx + 1),
                                                      Sub(dd, e)
                                                          setter(If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), ""))
                                                          FlowSheet.RequestCalculation()
                                                      End Sub)
                End Sub

            addCompoundDropdown("Methane (CHâ‚„)", MethaneCompound, Sub(v) MethaneCompound = v)
            addCompoundDropdown("Carbon Dioxide (COâ‚‚)", CO2Compound, Sub(v) CO2Compound = v)
            addCompoundDropdown("Hydrogen Sulfide (Hâ‚‚S)", H2SCompound, Sub(v) H2SCompound = v)
            addCompoundDropdown("Water (Hâ‚‚O)", WaterCompound, Sub(v) WaterCompound = v)
            addCompoundDropdown("Nitrogen (Nâ‚‚)", N2Compound, Sub(v) N2Compound = v)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Biogas"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.3 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Upgraded Gas (RNG)"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + w, gy + 0.7 * h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Off-gas"
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Biogas"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.3 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Upgraded Gas (RNG)"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.7 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Off-gas"})
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
                    "biogas_upgrader_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Biogas upgrading skid: H2S polisher + CO2 absorber columns on shared skid with crossover piping.
            Dim skid As New SKRect(gx + 0.05F * w, gy + 0.85F * h, gx + 0.95F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            Dim col1 As New SKRect(gx + 0.13F * w, gy + 0.2F * h, gx + 0.4F * w, gy + 0.87F * h)
            Dim col2 As New SKRect(gx + 0.55F * w, gy + 0.2F * h, gx + 0.82F * w, gy + 0.87F * h)
            BioOpsDrawHelper.DrawVerticalTank(canvas, col1, mono)
            BioOpsDrawHelper.DrawVerticalTank(canvas, col2, mono)
            ' small vent stubs on top of each column
            Dim cx1 = (col1.Left + col1.Right) * 0.5F
            Dim cx2 = (col2.Left + col2.Right) * 0.5F
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx1, gy + 0.12F * h), New SKPoint(cx1, col1.Top), 0.025F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx2, gy + 0.12F * h), New SKPoint(cx2, col2.Top), 0.025F * w, mono)
            ' crossover pipe between upper sides
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(col1.Right, gy + 0.28F * h), New SKPoint(col2.Left, gy + 0.28F * h), 0.04F * h, mono)
            ' flanges at top
            BioOpsDrawHelper.DrawFlange(canvas, cx1, col1.Top, col1.Width * 0.8F, mono)
            BioOpsDrawHelper.DrawFlange(canvas, cx2, col2.Top, col2.Width * 0.8F, mono)
            ' inlet and outlet pipes with flanges
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.02F * w, gy + 0.4F * h), New SKPoint(col1.Left, gy + 0.4F * h), 0.035F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, col1.Left, gy + 0.4F * h, 0.08F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(col2.Right, gy + 0.7F * h), New SKPoint(gx + 0.98F * w, gy + 0.7F * h), 0.035F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, col2.Right, gy + 0.7F * h, 0.08F * w, mono)
            ' labels
            Using txt As New SKPaint With {.Color = If(mono, New SKColor(30, 30, 30), New SKColor(40, 70, 95)), .IsAntialias = True,
                                           .TextSize = 0.12F * h, .TextAlign = SKTextAlign.Center, .Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)}
                canvas.DrawText("H" & ChrW(&H2082) & "S", (col1.Left + col1.Right) * 0.5F, gy + 0.55F * h, txt)
                canvas.DrawText("CO" & ChrW(&H2082), (col2.Left + col2.Right) * 0.5F, gy + 0.55F * h, txt)
            End Using
        End Sub

    End Class

End Namespace
