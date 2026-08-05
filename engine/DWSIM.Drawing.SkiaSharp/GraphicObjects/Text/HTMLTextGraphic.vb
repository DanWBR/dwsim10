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

        Public Overrides Sub Draw(ByVal g As Object)

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            Using tpaint As New SKPaint()

                If DrawMode = 0 Then
                    With tpaint
                        .TextSize = Size
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        .Color = If(GlobalSettings.Settings.DarkMode, SKColors.LightSteelBlue, Color)
                        .IsStroke = False
                        .Typeface = GetFont()
                    End With
                Else
                    With tpaint
                        .TextSize = Size
                        .IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        .Color = SKColors.Black
                        .IsStroke = False
                        .Typeface = GetFont()
                    End With
                End If

                Dim newtext = ReplaceVars(Text)

                ' Strip HTML tags for a cross-platform fallback. The original implementation used
                ' TheArtOfDev.HtmlRenderer.WinForms which pulled System.Windows.Forms into the
                ' engine's transitive dependencies, breaking .NET 8 / Linux / macOS hosts. SkiaSharp
                ' does its own text layout, so we render the plain text line-by-line and let the
                ' user style via Size / Color properties.
                Dim plain = StripHtml(newtext)
                Dim lineHeight As Single = CSng(Size * 1.3)
                Dim curY As Single = CSng(Y + lineHeight)
                For Each line In plain.Split({vbCrLf, vbLf}, StringSplitOptions.None)
                    canvas.DrawText(line, X, curY, tpaint)
                    curY += lineHeight
                Next

            End Using

        End Sub

        Private Shared Function StripHtml(input As String) As String
            If String.IsNullOrEmpty(input) Then Return ""
            ' Normalize <br> / <p> / </p> to newlines, then drop the remaining tags. A real HTML
            ' renderer would be overkill for what is effectively a labelled annotation.
            Dim t = input.Replace("<br>", vbLf).Replace("<br/>", vbLf).Replace("<br />", vbLf)
            t = t.Replace("</p>", vbLf).Replace("<p>", "").Replace("<P>", "").Replace("</P>", vbLf)
            Return System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "").Trim()
        End Function

        Private Function ReplaceVars(oldtext As String) As String

            Dim newtext As String = oldtext
            Dim i As Integer = 0

            If Flowsheet IsNot Nothing Then
                If Text.Contains("{") And Text.Contains("}") Then
                    For i = 1 To Flowsheet.WatchItems.Count
                        Dim objID = Flowsheet.WatchItems(i - 1).ObjID
                        Dim propID = Flowsheet.WatchItems(i - 1).PropID
                        If Flowsheet.SimulationObjects.ContainsKey(objID) Then
                            Dim units = Flowsheet.SimulationObjects(objID).GetPropertyUnit(propID)
                            Dim name = Flowsheet.GetTranslatedString(propID)
                            newtext = newtext.Replace("{" + i.ToString() + ":N}", name)
                            newtext = newtext.Replace("{" + i.ToString() + ":U}", units)
                            Dim value = Flowsheet.SimulationObjects(objID).GetPropertyValue(propID).ToString()
                            If Double.TryParse(value, New Double) Then
                                Dim dval = Double.Parse(value).ToString(Flowsheet.FlowsheetOptions.NumberFormat)
                                newtext = newtext.Replace("{" + i.ToString() + ":V}", dval)
                            Else
                                newtext = newtext.Replace("{" + i.ToString() + ":V}", value)
                            End If
                        End If
                    Next
                End If
            End If

            Return newtext

        End Function

    End Class

End Namespace