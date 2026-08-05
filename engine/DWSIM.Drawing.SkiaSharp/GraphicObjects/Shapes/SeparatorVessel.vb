Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Interfaces

Namespace GraphicObjects.Shapes

    Public Class VesselGraphic

        Inherits ShapeGraphic

#Region "Constructors"

        Public Sub New()
            Me.ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.Vessel
            Me.Description = "Vapor-Liquid Separator"
            EmbeddedResourceIconName = "separator.png"
            EmbeddedResourcePhotoName = "separator_vessel_photo.png"
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

            Dim myIC1 As New ConnectionPoint
            myIC1.Position = New Point(X + 0.25 * Width, Y + 1 / 7 * Height)
            myIC1.Type = ConType.ConIn

            Dim myIC2 As New ConnectionPoint
            myIC2.Position = New Point(X + 0.25 * Width, Y + 2 / 7 * Height)
            myIC2.Type = ConType.ConIn

            Dim myIC3 As New ConnectionPoint
            myIC3.Position = New Point(X + 0.25 * Width, Y + 3 / 7 * Height)
            myIC3.Type = ConType.ConIn

            Dim myIC4 As New ConnectionPoint
            myIC4.Position = New Point(X + 0.25 * Width, Y + 4 / 7 * Height)
            myIC4.Type = ConType.ConIn

            Dim myIC5 As New ConnectionPoint
            myIC5.Position = New Point(X + 0.25 * Width, Y + 5 / 7 * Height)
            myIC5.Type = ConType.ConIn

            Dim myIC6 As New ConnectionPoint
            myIC6.Position = New Point(X + 0.25 * Width, Y + 6 / 7 * Height)
            myIC6.Type = ConType.ConIn

            Dim myOC1 As New ConnectionPoint
            myOC1.Position = New Point(X + 0.827 * Width, Y + (1 / 7) * Height)
            myOC1.Type = ConType.ConOut

            Dim myOC2 As New ConnectionPoint
            myOC2.Position = New Point(X + 0.827 * Width, Y + (6 / 7) * Height)
            myOC2.Type = ConType.ConOut

            Dim myOC3 As New ConnectionPoint
            myOC3.Position = New Point(X + 0.5 * Width, Y + Height)
            myOC3.Type = ConType.ConOut
            myOC3.Direction = ConDir.Down

            Dim myOC4 As New ConnectionPoint
            myOC4.Position = New Point(X + 0.4 * Width, Y + 0.05 * Height)
            myOC4.Type = ConType.ConOut
            myOC4.Direction = ConDir.Up

            Dim myOC5 As New ConnectionPoint
            myOC5.Position = New Point(X + 0.4 * Width, Y + 0.5 * Height)
            myOC5.Type = ConType.ConOut
            myOC5.Type = ConType.ConEn

            Dim myIC7 As New ConnectionPoint
            myIC7.Position = New Point(X + 0.25 * Width, Y + 1 * Height)
            myIC7.Type = ConType.ConEn
            myIC7.Direction = ConDir.Up

            With InputConnectors

                If .Count <> 0 Then
                    If .Count = 1 Then
                        .Add(myIC2)
                        .Add(myIC3)
                        .Add(myIC4)
                        .Add(myIC5)
                        .Add(myIC6)
                        .Add(myIC7)
                    End If
                    If .Count = 6 Then
                        .Add(myIC7)
                    End If
                    If DrawMode = 2 Then
                        .Item(0).Position = New Point(X + 0.453 * Width, Y + 0.2979 * Height)
                        .Item(1).Position = New Point(X + 0.453 * Width, Y + 0.2979 * Height)
                        .Item(2).Position = New Point(X + 0.453 * Width, Y + 0.2979 * Height)
                        .Item(3).Position = New Point(X + 0.453 * Width, Y + 0.2979 * Height)
                        .Item(4).Position = New Point(X + 0.453 * Width, Y + 0.2979 * Height)
                        .Item(5).Position = New Point(X + 0.453 * Width, Y + 0.2979 * Height)
                        .Item(6).Position = New Point(X + 0.453 * Width, Y + 0.2979 * Height)
                        For i = 0 To 6
                            .Item(i).Direction = ConDir.Down
                        Next
                    Else
                        .Item(0).Position = New Point(X + 0.25 * Width, Y + 2 / 10 * Height)
                        .Item(1).Position = New Point(X + 0.25 * Width, Y + 3 / 10 * Height)
                        .Item(2).Position = New Point(X + 0.25 * Width, Y + 4 / 10 * Height)
                        .Item(3).Position = New Point(X + 0.25 * Width, Y + 5 / 10 * Height)
                        .Item(4).Position = New Point(X + 0.25 * Width, Y + 6 / 10 * Height)
                        .Item(5).Position = New Point(X + 0.25 * Width, Y + 7 / 10 * Height)
                        .Item(6).Position = New Point(X + 0.25 * Width, Y + 0.5 * Height)
                        For i = 0 To 6
                            .Item(i).Direction = ConDir.Right
                        Next
                    End If
                Else
                    .Add(myIC1)
                    .Add(myIC2)
                    .Add(myIC3)
                    .Add(myIC4)
                    .Add(myIC5)
                    .Add(myIC6)
                    .Add(myIC7)
                End If

            End With

            For Each c In InputConnectors
                c.ConnectorName = "Inlet Stream #" & InputConnectors.IndexOf(c)
                If c.Type = ConType.ConEn Then c.ConnectorName = "Heat In"
            Next

            With OutputConnectors

                If .Count = 2 Then .Add(myOC3)
                If .Count = 3 Then .Add(myOC4)
                If .Count = 4 Then .Add(myOC5)

                If .Count <> 0 Then
                    If DrawMode = 2 Then
                        .Item(0).Position = New Point(X + 0.84 * Width, Y + 0.32 * Height)
                        .Item(1).Position = New Point(X + Width, Y + 0.58 * Height)
                        .Item(2).Position = New Point(X + Width, Y + 0.7 * Height)
                        .Item(3).Position = New Point(X + 0.29 * Width, Y + 0.32 * Height)
                        .Item(4).Position = New Point(X + 0.29 * Width, Y + 0.5 * Height)
                        .Item(0).Direction = ConDir.Up
                        .Item(1).Direction = ConDir.Right
                        .Item(2).Direction = ConDir.Right
                        .Item(3).Direction = ConDir.Up
                        .Item(4).Direction = ConDir.Right
                    Else
                        .Item(0).Position = New Point(X + 0.5 * Width, Y)
                        .Item(1).Position = New Point(X + 0.75 * Width, Y + 5 / 7 * Height)
                        .Item(2).Position = New Point(X + 0.5 * Width, Y + Height)
                        .Item(3).Position = New Point(X + 0.65 * Width, Y + 0.045 * Height)
                        .Item(4).Position = New Point(X + 0.75 * Width, Y + 0.5 * Height)
                        .Item(0).Direction = ConDir.Up
                        .Item(1).Direction = ConDir.Right
                        .Item(2).Direction = ConDir.Down
                        .Item(3).Direction = ConDir.Up
                        .Item(4).Direction = ConDir.Right
                    End If
                Else
                    .Add(myOC1)
                    .Add(myOC2)
                    .Add(myOC3)
                    .Add(myOC4)
                    .Add(myOC5)
                End If

                .Item(0).ConnectorName = "Vapor Outlet"
                .Item(1).ConnectorName = "Light Liquid Outlet"
                .Item(2).ConnectorName = "Heavy Liquid Outlet"
                .Item(3).ConnectorName = "Relief Valve Outlet"
                .Item(4).ConnectorName = "Heat Out"

            End With

            Me.EnergyConnector.Active = False

        End Sub

        Public Overrides Sub Draw(ByVal g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            CreateConnectors(0, 0)
            UpdateStatus()

            MyBase.Draw(g)

            Select Case DrawMode

                Case 0

                    'default
                    Using myPen As New SKPaint()
                        With myPen
                            .Color = LineColor
                            .StrokeWidth = LineWidth
                            .IsStroke = True
                            .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        End With

                        Dim rect As New SKRect(X + 0.25 * Width, Y + 0.2 * Height, X + 0.75 * Width, Y + 0.8 * Height)
                        Dim rect1 As New SKRect(X + 0.25 * Width, Y, X + 0.75 * Width, Y + 0.4 * Height)
                        Dim rect2 As New SKRect(X + 0.25 * Width, Y + 0.6 * Height, X + 0.75 * Width, Y + Height)

                        Using gradPen As New SKPaint()
                            With gradPen
                                .Color = LineColor.WithAlpha(50)
                                .StrokeWidth = LineWidth
                                .IsStroke = False
                                .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                            End With

                            canvas.DrawRect(rect, gradPen)
                            canvas.DrawRect(rect, myPen)

                            canvas.DrawArc(rect1, -180, 180, False, gradPen)
                            canvas.DrawArc(rect2, 180, -180, False, gradPen)
                            canvas.DrawArc(rect1, -180, 180, False, myPen)
                            canvas.DrawArc(rect2, 180, -180, False, myPen)
                        End Using
                    End Using

                Case 1

                    'b/w
                    Using myPen As New SKPaint()
                        With myPen
                            .Color = SKColors.Black
                            .StrokeWidth = LineWidth
                            .IsStroke = True
                            .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        End With

                        Dim rect As New SKRect(X + 0.25 * Width, Y + 0.2 * Height, X + 0.75 * Width, Y + 0.8 * Height)
                        Dim rect1 As New SKRect(X + 0.25 * Width, Y, X + 0.75 * Width, Y + 0.4 * Height)
                        Dim rect2 As New SKRect(X + 0.25 * Width, Y + 0.6 * Height, X + 0.75 * Width, Y + Height)

                        canvas.DrawRect(rect, myPen)

                        canvas.DrawArc(rect1, -180, 180, False, myPen)
                        canvas.DrawArc(rect2, 180, -180, False, myPen)
                    End Using

                Case 2

                    DrawPhoto(canvas)

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