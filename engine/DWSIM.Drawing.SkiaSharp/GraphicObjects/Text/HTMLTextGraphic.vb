Imports Interfaces = DWSIM.Interfaces
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums.GraphicObjects

Namespace GraphicObjects

    Public Class HTMLTextGraphic

        Inherits GraphicObject

#Region "Constructors"

        Public Sub New()

            Me.ObjectType = Interfaces.Enums.GraphicObjects.ObjectType.GO_HTMLText

            Me.Height = 300
            Me.Width = 400

        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint)
            Me.New()
            Me.SetPosition(graphicPosition)
            Me.Text = Text
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

        Public Property Text As String = "<html><body><p>Double-click to edit this text.</p></body></html>"

        Public Property Size As Double = 14.0#

        Public Property Color As SKColor = SKColors.Black

        Private _layout As MarkdownRenderer.Layout
        Private _key As String

        Public Overrides Sub Draw(ByVal g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            ' the HTML is converted to Markdown and drawn by the Markdown renderer: paragraphs, headings,
            ' bold, italic, lists, tables and links keep their formatting; fonts, sizes and colors of the
            ' HTML give way to the block's size and color
            Dim md = MarkdownRenderer.FromHtml(TextVariables.Replace(Flowsheet, Text))
            Dim bw = DrawMode = 1
            Dim dark = GraphicsSurface.DarkCanvas AndAlso Not bw
            Dim tc = If(dark, SKColors.LightSteelBlue, If(bw, SKColors.Black, Color))
            Dim w = Math.Max(40.0F, CSng(Width))
            Dim fonts = MarkdownRenderer.CreateFontSet(RegularTypeFace, BoldTypeFace, ItalicTypeFace, BoldItalicTypeFace)
            Dim key = String.Join(ChrW(1), md, w.ToString(), Size.ToString(), tc.ToString(), bw.ToString(), dark.ToString(),
                                  fonts.Regular.GetHashCode().ToString())
            If _layout Is Nothing OrElse key <> _key Then
                _layout = MarkdownRenderer.Build(md, w, CSng(Size), fonts, MarkdownRenderer.CreatePalette(tc, dark, bw))
                _key = key
            End If
            Height = CInt(Math.Ceiling(Math.Max(_layout.Height, Size * 1.5)))
            _layout.Draw(canvas, X, Y)

        End Sub

    End Class

End Namespace