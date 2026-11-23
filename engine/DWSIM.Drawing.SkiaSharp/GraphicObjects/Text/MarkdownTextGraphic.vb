Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports Interfaces = DWSIM.Interfaces

Namespace GraphicObjects

    ''' <summary>
    ''' A text block written in Markdown. The text wraps at the block's width and the block's height
    ''' follows the text. {n:N}, {n:V} and {n:U} show the name, value and units of the n-th Watch panel item.
    ''' </summary>
    Public Class MarkdownTextGraphic

        Inherits GraphicObject

        Public Const DefaultText As String = "### Notes" & vbLf & vbLf & "Double-click to edit this text. It accepts **Markdown**: *italic*, `code`, lists and tables."

        Private _layout As MarkdownRenderer.Layout
        Private _key As String

#Region "Constructors"

        Public Sub New()
            Me.ObjectType = ObjectType.GO_MarkdownText
            Me.Width = 360
            Me.Height = 80
        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint)
            Me.New()
            Me.SetPosition(graphicPosition)
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer)
            Me.New(New SKPoint(posX, posY))
        End Sub

#End Region

        Public Overrides Property ClipboardData As String
            Get
                Return Text
            End Get
            Set(value As String)
                Text = value
            End Set
        End Property

        ''' <summary>The Markdown text.</summary>
        Public Property Text As String = DefaultText

        ''' <summary>The size of the body text; the headings scale from it.</summary>
        Public Property Size As Double = 13.0#

        ''' <summary>The color of the text.</summary>
        Public Property Color As SKColor = SKColors.Black

        ''' <summary>Fills the block with <see cref="FillColor"/>.</summary>
        Public Property Fill As Boolean = False

        Public Property FillColor As SKColor = SKColors.White

        ''' <summary>Draws a border around the block.</summary>
        Public Property Border As Boolean = False

        Public Property BorderColor As SKColor = New SKColor(208, 215, 222)

        ''' <summary>The inner margin when the block is filled or bordered.</summary>
        Public Function GetPadding() As Single
            Return If(Fill OrElse Border, CSng(Size * 0.8), 0.0F)
        End Function

        ''' <summary>The text laid out at the block's width (cached while the text, width and style stay the same).</summary>
        Public Function GetLayout() As MarkdownRenderer.Layout

            Dim md = TextVariables.Replace(Flowsheet, Text)
            Dim bw = DrawMode = 1
            ' a filled block keeps its own background, so its text keeps its color on a dark canvas
            Dim dark = GraphicsSurface.DarkCanvas AndAlso Not bw AndAlso Not Fill
            Dim tc = Color
            If dark AndAlso (CInt(tc.Red) + tc.Green + tc.Blue) < 200 Then tc = New SKColor(220, 228, 238)
            Dim w = Math.Max(40.0F, Width - 2 * GetPadding())
            Dim fonts = MarkdownRenderer.CreateFontSet(RegularTypeFace, BoldTypeFace, ItalicTypeFace, BoldItalicTypeFace)

            Dim key = String.Join(ChrW(1), md, w.ToString(), Size.ToString(), tc.ToString(), bw.ToString(), dark.ToString(),
                                  fonts.Regular.GetHashCode().ToString(), fonts.Bold.GetHashCode().ToString())
            If _layout Is Nothing OrElse key <> _key Then
                _layout = MarkdownRenderer.Build(md, w, CSng(Size), fonts, MarkdownRenderer.CreatePalette(tc, dark, bw))
                _key = key
            End If
            Return _layout

        End Function

        Public Overrides Sub Draw(ByVal g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            DrawAt(canvas, X, Y)

        End Sub

        ''' <summary>Draws the block with its top left corner at (x, y) and sets its height to the text's.</summary>
        Public Sub DrawAt(canvas As SKCanvas, x As Single, y As Single)

            Dim lay = GetLayout()
            Dim pad = GetPadding()
            Height = CInt(Math.Ceiling(Math.Max(lay.Height + 2 * pad, Size * 1.5)))

            Dim box As New SKRect(x, y, x + Width, y + Height)
            Dim radius = CSng(Size * 0.4)
            If Fill Then
                Using p As New SKPaint With {.Color = If(DrawMode = 1, SKColors.White, FillColor), .IsAntialias = True}
                    canvas.DrawRoundRect(box, radius, radius, p)
                End Using
            End If
            If Border Then
                Using p As New SKPaint With {.Color = If(DrawMode = 1, SKColors.Black, BorderColor), .IsAntialias = True, .IsStroke = True, .StrokeWidth = 1.0F}
                    canvas.DrawRoundRect(box, radius, radius, p)
                End Using
            End If

            lay.Draw(canvas, x + pad, y + pad)

        End Sub

        ''' <summary>
        ''' The block drawn on the flowsheet background at <paramref name="scale"/>, for an editor's
        ''' preview: the same drawing as on the flowsheet.
        ''' </summary>
        Public Function RenderImage(Optional scale As Single = 1.0F, Optional margin As Single = 12.0F) As SKImage

            Dim lay = GetLayout()
            Dim h = Math.Max(lay.Height + 2 * GetPadding(), CSng(Size * 1.5))
            Dim pw = CInt(Math.Ceiling((Width + 2 * margin) * scale))
            Dim ph = CInt(Math.Ceiling((h + 2 * margin) * scale))
            Using surface = SKSurface.Create(New SKImageInfo(Math.Max(1, pw), Math.Max(1, ph)))
                Dim canvas = surface.Canvas
                canvas.Clear(If(DrawMode = 1, SKColors.White, GraphicsSurface.BackgroundColor))
                canvas.Scale(scale)
                DrawAt(canvas, margin, margin)
                Return surface.Snapshot()
            End Using

        End Function

        ''' <summary>
        ''' Replaces an HTML text block of <paramref name="surface"/> with a Markdown block that holds its
        ''' text converted to Markdown, with the same name, tag, position, width, size and color.
        ''' </summary>
        Public Shared Function ReplaceHtmlBlock(surface As GraphicsSurface, html As HTMLTextGraphic) As MarkdownTextGraphic

            Dim md As New MarkdownTextGraphic With {
                .Name = html.Name, .Tag = html.Tag, .X = html.X, .Y = html.Y, .Width = Math.Max(120, html.Width),
                .Size = html.Size, .Color = html.Color, .Flowsheet = html.Flowsheet, .Rotation = html.Rotation,
                .PositionLocked = html.PositionLocked,
                .Text = MarkdownRenderer.FromHtml(html.Text)}

            Dim idx = surface.DrawingObjects.IndexOf(html)
            If idx >= 0 Then
                surface.DrawingObjects(idx) = md
            Else
                surface.DrawingObjects.Add(md)
            End If
            If surface.SelectedObject Is html Then surface.SelectedObject = md
            If surface.SelectedObjects.ContainsKey(html.Name) Then surface.SelectedObjects(html.Name) = md

            Return md

        End Function

    End Class

End Namespace
