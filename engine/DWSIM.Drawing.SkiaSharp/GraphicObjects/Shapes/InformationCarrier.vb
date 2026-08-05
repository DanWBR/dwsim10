Namespace GraphicObjects.Shapes

    Public Class InformationCarrierGraphic

        Inherits ShapeGraphic

        Protected m_svPT, m_tvPT, m_tvPT2, m_tvPT3 As GraphicObject

        Public Property ConnectedToSv As GraphicObject
            Get
                Return m_svPT
            End Get
            Set(ByVal value As GraphicObject)
                m_svPT = value
            End Set
        End Property

        Public Property ConnectedToTv As GraphicObject
            Get
                Return m_tvPT
            End Get
            Set(ByVal value As GraphicObject)
                m_tvPT = value
            End Set
        End Property

        Public Property ConnectedToTv2 As GraphicObject
            Get
                Return m_tvPT2
            End Get
            Set(ByVal value As GraphicObject)
                m_tvPT2 = value
            End Set
        End Property

        Public Property ConnectedToTv3 As GraphicObject
            Get
                Return m_tvPT3
            End Get
            Set(ByVal value As GraphicObject)
                m_tvPT3 = value
            End Set
        End Property

#Region "Constructors"

        Public Sub New()
            Me.ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.OT_InformationCarrier
            Me.Description = "Information Carrier Logical Op"
        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint)
            Me.New()
            Me.SetPosition(graphicPosition)
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer)
            Me.New(New SKPoint(posX, posY))
        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint, ByVal graphicSize As SKSize)
            Me.New(graphicPosition)
            Me.SetSize(graphicSize)
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer, ByVal graphicSize As SKSize)
            Me.New(New SKPoint(posX, posY), graphicSize)
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer, ByVal width As Integer, ByVal height As Integer)
            Me.New(New SKPoint(posX, posY), New SKSize(width, height))
        End Sub

#End Region

        Public Overrides Sub PositionConnectors()

            CreateConnectors(0, 0)

        End Sub

        Public Overrides Sub CreateConnectors(InCount As Integer, OutCount As Integer)

            Me.EnergyConnector.Active = False

        End Sub

        Public Overrides Sub Draw(ByVal g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            CreateConnectors(0, 0)

            UpdateStatus()

            MyBase.Draw(g)

            Select Case DrawMode

                Case 0, 2

                    'default
                    Using aPen As New SKPaint()
                    With aPen
                        If Active Then
                            .Color = SKColors.Violet
                        Else
                            .Color = SKColors.Gray
                        End If
                        .StrokeWidth = 1
                        .IsStroke = True
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        .PathEffect = SKPathEffect.CreateDash(New Single() {2.0F, 3.0F, 2.0F, 3.0F}, 2.0F)
                    End With

                    If Not Me.ConnectedToSv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_svPT.X + Me.m_svPT.Width / 2, Me.Y + Me.Height / 2), Me.m_svPT.GetCenterPosition}, aPen)
                    End If
                    If Not Me.ConnectedToTv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_tvPT.X + Me.m_tvPT.Height / 2, Me.Y + Me.Height / 2), Me.m_tvPT.GetCenterPosition}, aPen)
                    End If
                    If Not Me.ConnectedToTv2 Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_tvPT2.X + Me.m_tvPT2.Height / 2, Me.Y + Me.Height / 2), Me.m_tvPT2.GetCenterPosition}, aPen)
                    End If
                    If Not Me.ConnectedToTv3 Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_tvPT3.X + Me.m_tvPT3.Height / 2, Me.Y + Me.Height / 2), Me.m_tvPT3.GetCenterPosition}, aPen)
                    End If
                    End Using

                    Dim centerC As SKColor, edgeC As SKColor, outlineC As SKColor, textC As SKColor
                    If Active Then
                        centerC = New SKColor(240, 220, 255)
                        edgeC = New SKColor(150, 70, 180)
                        outlineC = New SKColor(95, 40, 120)
                        textC = New SKColor(95, 40, 120)
                    Else
                        centerC = New SKColor(235, 235, 235)
                        edgeC = New SKColor(120, 120, 120)
                        outlineC = New SKColor(80, 80, 80)
                        textC = New SKColor(60, 60, 60)
                    End If
                    Using New SKAutoCanvasRestore(canvas)
                        StraightCanvas(canvas)
                        LogicalOpSphereHelper.DrawSphere(canvas, X, Y, Width, Height,
                                                        centerC, edgeC, outlineC, textC,
                                                        "I", 0.55F,
                                                        GlobalSettings.Settings.DrawingAntiAlias,
                                                        BoldTypeFace)
                    End Using

                Case 1

                    'b/w
                    Using aPen As New SKPaint()
                    With aPen
                        .Color = SKColors.Black
                        .StrokeWidth = LineWidth
                        .IsStroke = True
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        .PathEffect = SKPathEffect.CreateDash(New Single() {2, 2}, 4)
                    End With

                    If Not Me.ConnectedToSv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_svPT.X, Me.Y + Me.Height / 2), Me.m_svPT.GetPosition}, aPen)
                    End If
                    If Not Me.ConnectedToTv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_tvPT.X, Me.Y + Me.Height / 2), Me.m_tvPT.GetPosition}, aPen)
                    End If
                    If Not Me.ConnectedToTv2 Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_tvPT2.X + Me.m_tvPT2.Height / 2, Me.Y + Me.Height / 2), Me.m_tvPT2.GetCenterPosition}, aPen)
                    End If
                    If Not Me.ConnectedToTv3 Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_tvPT3.X + Me.m_tvPT3.Height / 2, Me.Y + Me.Height / 2), Me.m_tvPT3.GetCenterPosition}, aPen)
                    End If
                    End Using

                    Using New SKAutoCanvasRestore(canvas)
                        StraightCanvas(canvas)
                        LogicalOpSphereHelper.DrawSphereMono(canvas, X, Y, Width, Height,
                                                            "I", 0.55F,
                                                            GlobalSettings.Settings.DrawingAntiAlias,
                                                            BoldTypeFace)
                    End Using

                Case 3

                    'Temperature Gradients

                Case 4

                    'Pressure Gradients

                Case 5

                    'Temperature/Pressure Gradients

            End Select

        End Sub

    End Class

End Namespace