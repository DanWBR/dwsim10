Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Interfaces
Imports SkiaSharp

Namespace GraphicObjects.Shapes

    Public Class MPCControllerGraphic

        Inherits ShapeGraphic

        Private Image As SKImage

#Region "Constructors"

        Public Sub New()
            Me.ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.Controller_MPC
            Me.Description = "MPC Controller"
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

            If Image Is Nothing Then
                Dim assm = Me.GetType.Assembly
                Using filestr As IO.Stream = assm.GetManifestResourceStream("DWSIM.Drawing.SkiaSharp.control_panel.png")
                    Using bitmap = SKBitmap.Decode(filestr)
                        Image = SKImage.FromBitmap(bitmap)
                    End Using
                End Using
            End If

            Using p As New SKPaint With {.IsAntialias = GlobalSettings.Settings.DrawingAntiAlias, .FilterQuality = SKFilterQuality.High}
                canvas.DrawImage(Image, New SKRect(X, Y, X + Width, Y + Height), p)
            End Using

            Dim f = Height / 50.0

            Using paint As New SKPaint With {.TextSize = 10.0 * f, .Color = GetForeColor(), .IsAntialias = True, .TextEncoding = SKTextEncoding.Utf8}
                Select Case GlobalSettings.Settings.RunningPlatform
                    Case GlobalSettings.Settings.Platform.Windows
                        paint.Typeface = SKTypeface.FromFamilyName("Consolas", SKTypefaceStyle.Bold)
                    Case GlobalSettings.Settings.Platform.Linux
                        paint.Typeface = SKTypeface.FromFamilyName("Courier New", SKTypefaceStyle.Bold)
                    Case GlobalSettings.Settings.Platform.Mac
                        paint.Typeface = SKTypeface.FromFamilyName("Menlo", SKTypefaceStyle.Bold)
                End Select
                canvas.DrawText("MPC", X + Width + 3 * f, Y + Height * 0.8, paint)
            End Using

            If Not Owner?.Active Then
                Using p As New SKPaint() With {.FilterQuality = SKFilterQuality.High}
                    p.BlendMode = SKBlendMode.Color
                    p.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.Gray, SKBlendMode.SrcIn)
                    canvas.DrawImage(Image, New SKRect(X, Y, X + Width, Y + Height), p)
                End Using
            End If

        End Sub

    End Class

End Namespace
