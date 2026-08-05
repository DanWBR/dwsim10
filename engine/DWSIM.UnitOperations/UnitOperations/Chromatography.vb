'    Chromatography Column (simplified equilibrium / Langmuir binding capacity model)
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

    Public Enum ChromatographyMode
        ''' <summary>Bind-and-elute: target compounds bind to the resin and come off in the Product.</summary>
        BindElute = 0
        ''' <summary>Flow-through: contaminants bind; product flows through.</summary>
        FlowThrough = 1
        ''' <summary>Bind-elute with a dynamic Thomas-model breakthrough curve for the loading step.</summary>
        BindElute_Dynamic = 2
    End Enum

    Public Enum ChromatographyChemistry
        IonExchange = 0
        Affinity = 1
        HIC = 2
        SizeExclusion = 3
        MixedMode = 4
    End Enum

    ''' <summary>
    ''' Chromatography column (simplified Langmuir-binding + user-specified resolution model).
    ''' For each compound, the user specifies a "RecoveryToProduct" fraction (0â€“1) - for BindElute
    ''' mode, this is the elution yield; for FlowThrough, it's the pass-through fraction. Default
    ''' values are suggested by MW (macromolecules bind; small solutes flow through).
    ''' The column's dynamic binding capacity is reported as a saturation check.
    ''' </summary>
    <System.Serializable()> Public Partial Class UnitOp_Chromatography

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

        Public Property Mode As ChromatographyMode = ChromatographyMode.BindElute
        Public Property Chemistry As ChromatographyChemistry = ChromatographyChemistry.IonExchange
        Public Property ColumnVolume_L As Double = 10.0
        Public Property DynamicBindingCapacity_gL As Double = 40.0
        Public Property DefaultRecoveryToProduct As Double = 0.05
        Public Property RecoveryToProduct As Dictionary(Of String, Double)

        Public Property Result_FeedMass_kgs As Double = 0.0
        Public Property Result_ProductMass_kgs As Double = 0.0
        Public Property Result_WasteMass_kgs As Double = 0.0
        Public Property Result_TargetRecovery As Double = 0.0
        Public Property Result_LoadRatio As Double = 0.0
        Public Property Result_Saturated As Boolean = False

        ''' <summary>Thomas rate constant k_Th (L/g/s). Typical: 1e-4 â€¦ 1e-2.</summary>
        Public Property ThomasRateConstant_Lgs As Double = 0.001

        ''' <summary>Loading time (s) used for the dynamic breakthrough simulation. 0 = auto (to â‰ˆ99% saturation).</summary>
        Public Property LoadingTime_s As Double = 0.0

        ''' <summary>Resin density (g/L column) used to convert q_max and DBC to absolute resin mass.</summary>
        Public Property ResinDensity_gL As Double = 1000.0

        ''' <summary>Last breakthrough trajectory (populated by Calculate when in BindElute_Dynamic mode). Not persisted.</summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property LastTrajectory As ChromatographyTrajectoryResult

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Sub New()
            MyBase.New()
            RecoveryToProduct = New Dictionary(Of String, Double)()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
            RecoveryToProduct = New Dictionary(Of String, Double)()
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New UnitOp_Chromatography()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of UnitOp_Chromatography)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Function RecoveryFor(compName As String) As Double
            If RecoveryToProduct IsNot Nothing AndAlso RecoveryToProduct.ContainsKey(compName) Then
                Return Max(0.0, Min(1.0, RecoveryToProduct(compName)))
            End If
            Return Max(0.0, Min(1.0, DefaultRecoveryToProduct))
        End Function

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Chromatography: Feed not connected.")
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("Chromatography: Both Product and Waste outlets must be connected.")
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

            Dim prod As New Dictionary(Of String, Double)
            Dim waste As New Dictionary(Of String, Double)
            Dim m_p As Double = 0.0, m_w As Double = 0.0
            Dim target_in As Double = 0.0, target_out As Double = 0.0

            For Each kv In feedComp
                Dim r = RecoveryFor(kv.Key)
                prod(kv.Key) = kv.Value * r
                waste(kv.Key) = kv.Value * (1.0 - r)
                m_p += prod(kv.Key) : m_w += waste(kv.Key)
                ' Treat macromolecules (MW > 5000) as "targets" for the recovery metric in BindElute mode
                Dim c = feed.Phases(0).Compounds(kv.Key)
                If c.ConstantProperties IsNot Nothing AndAlso c.ConstantProperties.Molar_Weight > 5000.0 Then
                    target_in += kv.Value
                    target_out += prod(kv.Key)
                End If
            Next

            ' Load-ratio check vs dynamic binding capacity (macromolecule mass / resin volume Ã— DBC)
            Dim dbc_kgs As Double = DynamicBindingCapacity_gL / 1000.0 * ColumnVolume_L ' kg of "bindable" per cycle
            If dbc_kgs > 0 Then
                Result_LoadRatio = target_in / dbc_kgs
            Else
                Result_LoadRatio = 0.0
            End If
            Result_Saturated = (Result_LoadRatio > 1.0)

            ' Dynamic Thomas breakthrough for the loading step, if in dynamic mode
            If Mode = ChromatographyMode.BindElute_Dynamic Then
                Dim Q_vol = feed.Phases(1).Properties.volumetric_flow.GetValueOrDefault
                If Q_vol <= 0.0 Then Q_vol = feed.Phases(0).Properties.volumetric_flow.GetValueOrDefault
                If Q_vol <= 0.0 Then Q_vol = 0.000000000001
                Dim Q_Ls = Q_vol * 1000.0          ' L/s
                Dim C0_gL = If(target_in > 0.0 AndAlso Q_vol > 0.0, target_in / Q_vol, 0.001) ' kg/s / (m3/s) = g/L
                BuildThomasBreakthrough(C0_gL, Q_Ls)
            End If

            Result_FeedMass_kgs = m_total
            Result_ProductMass_kgs = m_p
            Result_WasteMass_kgs = m_w
            If target_in > 0 Then Result_TargetRecovery = target_out / target_in Else Result_TargetRecovery = 0.0

            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name),
                        prod, m_p, T, P)
            WriteStream(FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name),
                        waste, m_w, T, P)

        End Sub

        ''' <summary>
        ''' Build a Thomas-model breakthrough curve C/C0 vs time for the loading step.
        '''   C/C0 = 1 / (1 + exp((k_Th / Q) Â· (q_max Â· m_resin âˆ’ C0 Â· Q Â· t)))
        ''' C0 in g/L, Q in L/s, q_max = DynamicBindingCapacity_gL (g/L resin),
        ''' m_resin = ColumnVolume_L Â· Ï_resin / 1000 (kg).
        ''' </summary>
        Private Sub BuildThomasBreakthrough(C0_gL As Double, Q_Ls As Double)

            Dim traj As New ChromatographyTrajectoryResult() With {.Mode = "BindElute_Dynamic"}
            LastTrajectory = traj
            If C0_gL <= 0.0 OrElse Q_Ls <= 0.0 OrElse ColumnVolume_L <= 0.0 Then Return

            Dim kTh = Max(ThomasRateConstant_Lgs, 0.000000000001)
            Dim qmax = Max(DynamicBindingCapacity_gL, 0.000000000001) ' g / L resin
            Dim m_resin_g = ColumnVolume_L * Max(ResinDensity_gL, 0.000000000001) / 1000.0 * 1000.0
            ' m_resin_g = CV(L) * rho(g/L) : g of resin. Simpler:
            m_resin_g = ColumnVolume_L * ResinDensity_gL ' g resin

            ' Time horizon: auto-compute if LoadingTime_s <= 0
            Dim t_end As Double = LoadingTime_s
            If t_end <= 0.0 Then
                ' Time to reach ~99% saturation: C/C0 = 0.99 => exp(...) = 1/99
                '   kTh/Q * (qmax*m - C0*Q*t) = -ln(99)
                '   t = (qmax*m + ln(99)*Q/kTh) / (C0*Q)
                t_end = (qmax * m_resin_g + Math.Log(99.0) * Q_Ls / kTh) / (Math.Max(C0_gL * Q_Ls, 0.000000000001))
                t_end = Max(t_end, 1.0)
            End If

            Dim N As Integer = 500
            Dim dt As Double = t_end / N
            Dim qLoaded As Double = 0.0
            For i = 0 To N
                Dim t = i * dt
                Dim expArg = (kTh / Q_Ls) * (qmax * m_resin_g - C0_gL * Q_Ls * t)
                ' clamp for numerical safety
                If expArg > 700 Then expArg = 700
                If expArg < -700 Then expArg = -700
                Dim CoverC0 = 1.0 / (1.0 + Math.Exp(expArg))
                Dim bv = Q_Ls * t / ColumnVolume_L
                ' Cumulative mass adsorbed (trap integration of (1-C/C0) * C0 * Q)
                If i > 0 Then
                    Dim tPrev = (i - 1) * dt
                    Dim eaPrev = (kTh / Q_Ls) * (qmax * m_resin_g - C0_gL * Q_Ls * tPrev)
                    If eaPrev > 700 Then eaPrev = 700
                    If eaPrev < -700 Then eaPrev = -700
                    Dim CoverPrev = 1.0 / (1.0 + Math.Exp(eaPrev))
                    Dim absRate = C0_gL * Q_Ls * 0.5 * ((1.0 - CoverPrev) + (1.0 - CoverC0))  ' g/s
                    qLoaded += absRate * dt / Max(ColumnVolume_L, 0.000000000001)             ' g/L resin
                End If
                traj.Times.Add(t)
                traj.BedVolumes.Add(bv)
                traj.C_over_C0.Add(CoverC0)
                traj.QLoaded.Add(qLoaded)
                traj.Breakthrough.Add(1.0 - CoverC0)
            Next

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
            Return "Chromatography column (bind-elute or flow-through)"
        End Function
        Public Overrides Function GetDisplayName() As String
            Return "Chromatography Column"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Chromatography: " & Me.GraphicObject.Tag)
            s.AppendLine("Mode:        " & Mode.ToString())
            s.AppendLine("Chemistry:   " & Chemistry.ToString())
            s.AppendLine("CV:          " & ColumnVolume_L.ToString(numberformat, ci) & " L")
            s.AppendLine("DBC:         " & DynamicBindingCapacity_gL.ToString(numberformat, ci) & " g/L")
            s.AppendLine()
            s.AppendLine("Feed:           " & Result_FeedMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Product:        " & Result_ProductMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Waste:          " & Result_WasteMass_kgs.ToString(numberformat, ci) & " kg/s")
            s.AppendLine("Target recovery (MW > 5 kDa): " & (Result_TargetRecovery * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("Load ratio (load/DBC):        " & Result_LoadRatio.ToString(numberformat, ci))
            If Result_Saturated Then s.AppendLine("  âš  Column is SATURATED - binding capacity exceeded.")
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {"Mode", "Chemistry", "Column Volume", "Dynamic Binding Capacity", "Default Recovery To Product"}
        Private Shared ReadOnly _outputProps As String() = {"Feed Mass", "Product Mass", "Waste Mass", "Target Recovery", "Load Ratio", "Saturated"}

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
                Case "Chemistry" : Return Chemistry.ToString()
                Case "Column Volume" : Return ColumnVolume_L
                Case "Dynamic Binding Capacity" : Return DynamicBindingCapacity_gL
                Case "Default Recovery To Product" : Return DefaultRecoveryToProduct
                Case "Feed Mass" : Return Result_FeedMass_kgs
                Case "Product Mass" : Return Result_ProductMass_kgs
                Case "Waste Mass" : Return Result_WasteMass_kgs
                Case "Target Recovery" : Return Result_TargetRecovery
                Case "Load Ratio" : Return Result_LoadRatio
                Case "Saturated" : Return Result_Saturated
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Column Volume" : Return "L"
                Case "Dynamic Binding Capacity" : Return "g/L"
                Case "Feed Mass", "Product Mass", "Waste Mass" : Return "kg/s"
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
                    Dim m As ChromatographyMode
                    If [Enum].TryParse(Of ChromatographyMode)(propval?.ToString(), m) Then Me.Mode = m
                    Return True
                Case "Chemistry"
                    Dim c As ChromatographyChemistry
                    If [Enum].TryParse(Of ChromatographyChemistry)(propval?.ToString(), c) Then Me.Chemistry = c
                    Return True
                Case "Column Volume" : ColumnVolume_L = d : Return True
                Case "Dynamic Binding Capacity" : DynamicBindingCapacity_gL = d : Return True
                Case "Default Recovery To Product" : DefaultRecoveryToProduct = d : Return True
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
                Return "CHR-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New UnitOp_Chromatography()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("Mode & Chemistry")

            container.CreateAndAddDropDownRow("Operating Mode",
                                              New List(Of String)({"Bind-Elute", "Flow-Through", "Bind-Elute (Dynamic, Thomas)"}),
                                              CInt(Mode),
                                              Sub(dd, e)
                                                  Mode = CType(dd.SelectedIndex, ChromatographyMode)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddDropDownRow("Chemistry",
                                              New List(Of String)({"Ion Exchange", "Affinity", "HIC", "Size Exclusion", "Mixed Mode"}),
                                              CInt(Chemistry),
                                              Sub(dd, e)
                                                  Chemistry = CType(dd.SelectedIndex, ChromatographyChemistry)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Column")

            container.CreateAndAddTextBoxRow(nf, "Column Volume (L)", ColumnVolume_L,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ColumnVolume_L = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Dynamic Binding Capacity (g/L)", DynamicBindingCapacity_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DynamicBindingCapacity_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Resin Density (g/L)", ResinDensity_gL,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ResinDensity_gL = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Separation")

            container.CreateAndAddTextBoxRow(nf, "Default Recovery to Product (0-1)", DefaultRecoveryToProduct,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     DefaultRecoveryToProduct = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Thomas Dynamic Model (Bind-Elute Dynamic only)")

            container.CreateAndAddTextBoxRow(nf, "Thomas Rate Constant (L/gÂ·s)", ThomasRateConstant_Lgs,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ThomasRateConstant_Lgs = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Loading Time (s)", LoadingTime_s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     LoadingTime_s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx + 0.5 * w, gy)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"
                GraphicObject.InputConnectors(0).Direction = ConDir.Down
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.7 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Product"
                GraphicObject.OutputConnectors(1).Position = New Point(gx + 0.5 * w, gy + h)
                GraphicObject.OutputConnectors(1).ConnectorName = "Waste"
                GraphicObject.OutputConnectors(1).Direction = ConDir.Up
            Else
                GraphicObject.InputConnectors.Clear() : GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.5 * w, gy), .Type = ConType.ConIn,
                    .Direction = ConDir.Down, .ConnectorName = "Feed"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.7 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Product"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.5 * w, gy + h), .Type = ConType.ConOut,
                    .Direction = ConDir.Up, .ConnectorName = "Waste"})
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
                    "chromatography_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Packed bed chromatography column: tall cylinder + top/bottom flanges + resin bed + inlet/outlet pipes.
            Dim skid As New SKRect(gx + 0.15F * w, gy + 0.9F * h, gx + 0.85F * w, gy + h)
            BioOpsDrawHelper.DrawSkid(canvas, skid, mono)
            Dim col As New SKRect(gx + 0.35F * w, gy + 0.12F * h, gx + 0.65F * w, gy + 0.9F * h)
            BioOpsDrawHelper.DrawVerticalTank(canvas, col, mono)
            ' flanges at top and bottom
            Dim cx = (col.Left + col.Right) * 0.5F
            BioOpsDrawHelper.DrawFlange(canvas, cx, col.Top + 0.01F * h, col.Width * 1.3F, mono)
            BioOpsDrawHelper.DrawFlange(canvas, cx, col.Bottom - 0.01F * h, col.Width * 1.3F, mono)
            ' resin bed (amber band)
            Dim bedTop = col.Top + col.Height * 0.22F
            Dim bedBot = col.Bottom - col.Height * 0.08F
            Using bed As New SKPaint With {.Color = If(mono, New SKColor(180, 180, 180, 230), New SKColor(210, 175, 105, 230)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(col.Left + 2.5F, bedTop, col.Right - 2.5F, bedBot), bed)
            End Using
            ' top & bottom distributor plates (thin dark bands)
            Using plate As New SKPaint With {.Color = BioOpsDrawHelper.ClrStroke(mono), .IsAntialias = True}
                canvas.DrawRect(New SKRect(col.Left + 2.5F, bedTop - 2, col.Right - 2.5F, bedTop), plate)
                canvas.DrawRect(New SKRect(col.Left + 2.5F, bedBot, col.Right - 2.5F, bedBot + 2), plate)
            End Using
            ' denser beads hint
            Dim r = 0.009F * w
            Using bead As New SKPaint With {.Color = If(mono, New SKColor(130, 130, 130), New SKColor(140, 100, 50, 255)), .IsAntialias = True}
                Dim y = bedTop + 2 * r
                Dim row = 0
                While y < bedBot - r
                    Dim xoff = If(row Mod 2 = 0, 0.0F, 1.0F * r)
                    Dim x = col.Left + 4 * r + xoff
                    While x < col.Right - 2 * r
                        canvas.DrawCircle(x, y, r, bead)
                        x += 2.0F * r
                    End While
                    y += 1.8F * r
                    row += 1
                End While
            End Using
            ' inlet & outlet pipes with flanges
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.08F * w, gy + 0.08F * h), New SKPoint(cx, gy + 0.08F * h), 0.04F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.08F * w, gy + 0.08F * h, 0.07F * w, mono)
            BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx, gy + 0.95F * h), New SKPoint(gx + 0.92F * w, gy + 0.95F * h), 0.04F * h, mono)
            BioOpsDrawHelper.DrawFlange(canvas, gx + 0.92F * w, gy + 0.95F * h, 0.07F * w, mono)
        End Sub

    End Class

End Namespace
