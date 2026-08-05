Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Interfaces.Enums.GraphicObjects

Namespace GraphicObjects.Shapes

    Public Class AdjustGraphic

        Inherits ShapeGraphic

        Protected m_mvPT, m_cvPT, m_rvPT As GraphicObject

        Public Property ConnectedToMv() As GraphicObject
            Get
                Return m_mvPT
            End Get
            Set(ByVal value As GraphicObject)
                m_mvPT = value
            End Set
        End Property

        Public Property ConnectedToRv() As GraphicObject
            Get
                Return m_rvPT
            End Get
            Set(ByVal value As GraphicObject)
                m_rvPT = value
            End Set
        End Property

        Public Property ConnectedToCv() As GraphicObject
            Get
                Return m_cvPT
            End Get
            Set(ByVal value As GraphicObject)
                m_cvPT = value
            End Set
        End Property

#Region "Constructors"

        Public Sub New()
            Me.ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.OT_Adjust
            Me.Description = "Steady-State Controller Block"
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

        Public Overrides Sub CreateConnectors(InCount As Integer, OutCount As Integer)

            Me.EnergyConnector.Active = False

        End Sub

        Public Overrides Sub PositionConnectors()

            CreateConnectors(0, 0)

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
                            .Color = SKColors.SandyBrown
                        Else
                            .Color = SKColors.Gray
                        End If
                        .StrokeWidth = 1
                        .IsStroke = True
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        .PathEffect = SKPathEffect.CreateDash(New Single() {2.0F, 3.0F, 2.0F, 3.0F}, 2.0F)
                    End With

                    If Not Me.ConnectedToMv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_mvPT.X + Me.m_mvPT.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_mvPT.X + Me.m_mvPT.Width / 2, Me.m_mvPT.Y + Me.m_mvPT.Height / 2)}, aPen)
                    End If
                    If Not Me.ConnectedToCv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_cvPT.X + Me.m_cvPT.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_cvPT.X + Me.m_cvPT.Width / 2, Me.m_cvPT.Y + Me.m_cvPT.Height / 2)}, aPen)
                    End If
                    If Not Me.ConnectedToRv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_rvPT.X + Me.m_rvPT.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_rvPT.X + Me.m_rvPT.Width / 2, Me.m_rvPT.Y + Me.m_rvPT.Height / 2)}, aPen)
                    End If
                    End Using

                    Dim centerC As SKColor, edgeC As SKColor, outlineC As SKColor, textC As SKColor
                    If Active Then
                        centerC = New SKColor(255, 220, 200) ' soft salmon highlight
                        edgeC = New SKColor(180, 60, 50)     ' deep red edge
                        outlineC = New SKColor(140, 30, 25)
                        textC = New SKColor(140, 30, 25)
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
                                                        "C", 0.55F,
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
                        .PathEffect = SKPathEffect.CreateDash(New Single() {2.0F, 3.0F, 2.0F, 3.0F}, 2.0F)
                    End With

                    If Not Me.ConnectedToMv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_mvPT.X + Me.m_mvPT.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_mvPT.X + Me.m_mvPT.Width / 2, Me.m_mvPT.Y + Me.m_mvPT.Height / 2)}, aPen)
                    End If
                    If Not Me.ConnectedToCv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_cvPT.X + Me.m_cvPT.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_cvPT.X + Me.m_cvPT.Width / 2, Me.m_cvPT.Y + Me.m_cvPT.Height / 2)}, aPen)
                    End If
                    If Not Me.ConnectedToRv Is Nothing Then
                        canvas.DrawPoints(SKPointMode.Polygon, New SKPoint() {New SKPoint(Me.X + Me.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_rvPT.X + Me.m_rvPT.Width / 2, Me.Y + Me.Height / 2), New SKPoint(Me.m_rvPT.X + Me.m_rvPT.Width / 2, Me.m_rvPT.Y + Me.m_rvPT.Height / 2)}, aPen)
                    End If
                    End Using

                    Using New SKAutoCanvasRestore(canvas)
                        StraightCanvas(canvas)
                        LogicalOpSphereHelper.DrawSphereMono(canvas, X, Y, Width, Height,
                                                            "C", 0.55F,
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