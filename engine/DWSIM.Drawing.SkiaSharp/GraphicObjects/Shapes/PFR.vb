Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point

Namespace GraphicObjects.Shapes

    Public Class PFRGraphic

        Inherits ShapeGraphic

#Region "Constructors"

        Public Sub New()
            Me.ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.RCT_PFR
            Me.Description = "PFR"
            EmbeddedResourceIconName = "pfr.png"
            EmbeddedResourcePhotoName = "pfr_photo.png"
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
            myIC1.Position = New Point(X, Y + 0.5 * Height)
            myIC1.Type = ConType.ConIn

            Dim myIC2 As New ConnectionPoint
            myIC2.Position = New Point(X + 0.5 * Width, Y + Height)
            myIC2.Type = ConType.ConEn

            Dim myIC3 As New ConnectionPoint
            myIC3.Position = New Point(X + 0.25 * Width, Y)
            myIC3.Type = ConType.ConIn
            myIC3.Direction = ConDir.Down

            Dim myOC1 As New ConnectionPoint
            myOC1.Position = New Point(X + Width, Y + 0.5 * Height)
            myOC1.Type = ConType.ConOut

            Dim myOC2 As New ConnectionPoint
            myOC2.Position = New Point(X + 0.75 * Width, Y)
            myOC2.Type = ConType.ConOut
            myOC2.Direction = ConDir.Up

            Me.EnergyConnector.Position = New Point(X + 0.5 * Width, Y + Height)
            Me.EnergyConnector.Type = ConType.ConEn
            Me.EnergyConnector.Direction = ConDir.Up
            Me.EnergyConnector.Active = False

            With InputConnectors

                If .Count >= 3 Then
                    .Item(0).Position = New Point(X, Y + 0.5 * Height)
                    .Item(1).Position = New Point(X + 0.5 * Width, Y + Height)
                    .Item(2).Position = New Point(X + 0.25 * Width, Y)
                ElseIf .Count = 2 Then
                    .Item(0).Position = New Point(X, Y + 0.5 * Height)
                    .Item(1).Position = New Point(X + 0.5 * Width, Y + Height)
                    .Add(myIC3)
                ElseIf .Count = 0 Then
                    .Add(myIC1)
                    .Add(myIC2)
                    .Add(myIC3)
                End If

                .Item(0).ConnectorName = "Inlet"
                .Item(1).ConnectorName = "Energy Stream"
                .Item(2).ConnectorName = "Utility Fluid Inlet (Optional)"

            End With

            With OutputConnectors

                If .Count >= 2 Then
                    .Item(0).Position = New Point(X + Width, Y + 0.5 * Height)
                    .Item(1).Position = New Point(X + 0.75 * Width, Y)
                ElseIf .Count = 1 Then
                    .Item(0).Position = New Point(X + Width, Y + 0.5 * Height)
                    .Add(myOC2)
                ElseIf .Count = 0 Then
                    .Add(myOC1)
                    .Add(myOC2)
                End If

                .Item(0).ConnectorName = "Outlet"
                .Item(1).ConnectorName = "Utility Fluid Outlet (Optional)"

            End With

        End Sub

        Public Overrides Sub Draw(ByVal g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            CreateConnectors(0, 0)
            UpdateStatus()

            MyBase.Draw(g)

            Select Case DrawMode

                Case 0

                    'default

                    Using myPen2 As New SKPaint()
                    With myPen2
                        .Color = LineColor
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        .IsStroke = True
                        .StrokeWidth = LineWidth
                    End With

                    Dim rect1 As New SKRect(X + 0.1 * Width, Y, X + 0.9 * Width, Y + Height)
                    Dim rect0 As New SKRect(X, Y, X + 0.2 * Width, Y + Height)
                    Dim rect2 As New SKRect(X + 0.8 * Width, Y, X + Width, Y + Height)

                    Using gp As New SKPath()
                    gp.MoveTo(X + 0.1 * Width, Y)
                    gp.LineTo(X + 0.8 * Width, Y)
                    gp.ArcTo(rect2, -90, 180, False)
                    gp.LineTo(X + 0.1 * Width, Y + Height)
                    gp.ArcTo(rect0, 90, 180, False)

                    gp.Close()

                    Using gradPen As New SKPaint()
                    With gradPen
                        .Color = LineColor.WithAlpha(50)
                        .StrokeWidth = LineWidth
                        .IsStroke = False
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                    End With

                    canvas.DrawPath(gp, gradPen)
                    End Using

                    canvas.DrawOval(rect0, myPen2)
                    canvas.DrawPath(gp, myPen2)
                    End Using

                    For i As Integer = 1 To 15
                        Using p As New SKPath
                        Dim r As New SKRect(X + i / 20 * Width, Y, X + (0.2 + i / 20) * Width, Y + Height)
                        p.AddArc(r, -90, 180)
                        canvas.DrawPath(p, myPen2)
                        End Using
                    Next
                    End Using

                Case 1

                    'b/w
                    Using myPen2 As New SKPaint()
                    With myPen2
                        .Color = SKColors.Black
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        .IsStroke = True
                        .StrokeWidth = LineWidth
                    End With

                    Dim rect1 As New SKRect(X + 0.1 * Width, Y, X + 0.9 * Width, Y + Height)
                    Dim rect0 As New SKRect(X, Y, X + 0.2 * Width, Y + Height)
                    Dim rect2 As New SKRect(X + 0.8 * Width, Y, X + Width, Y + Height)

                    Using gp As New SKPath()
                    gp.MoveTo(X + 0.1 * Width, Y)
                    gp.LineTo(X + 0.8 * Width, Y)
                    gp.ArcTo(rect2, -90, 180, False)
                    gp.LineTo(X + 0.1 * Width, Y + Height)
                    gp.ArcTo(rect0, 90, 180, False)

                    gp.Close()

                    canvas.DrawOval(rect0, myPen2)
                    canvas.DrawPath(gp, myPen2)
                    End Using

                    For i As Integer = 1 To 15
                        Using p As New SKPath
                        Dim r As New SKRect(X + i / 20 * Width, Y, X + (0.2 + i / 20) * Width, Y + Height)
                        p.AddArc(r, -90, 180)
                        canvas.DrawPath(p, myPen2)
                        End Using
                    Next
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
