Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports SkiaSharp

Namespace GraphicObjects

    ''' <summary>
    ''' Lays out and draws Markdown with SkiaSharp: headings, paragraphs, bold, italic, strikethrough,
    ''' inline code, links, bullet, numbered and task lists (nested), block quotes, fenced code blocks,
    ''' horizontal rules and tables (GitHub style). The Markdown text block draws with it and the
    ''' editors draw their preview with it, so the preview and the flowsheet look the same. The HTML
    ''' text block converts its HTML to Markdown (<see cref="FromHtml"/>) and draws the same way.
    ''' </summary>
    Public Module MarkdownRenderer

#Region "Styles"

        ''' <summary>The colors of a layout.</summary>
        Public Class Palette
            Public Text As SKColor = SKColors.Black
            Public Muted As SKColor = New SKColor(101, 109, 118)
            Public Link As SKColor = New SKColor(9, 105, 218)
            Public Border As SKColor = New SKColor(208, 215, 222)
            Public CodeBack As SKColor = New SKColor(239, 241, 243)
            Public HeaderBack As SKColor = New SKColor(246, 248, 250)
        End Class

        ''' <summary>
        ''' The palette for a text color: the light theme, the dark one when the text sits on a dark
        ''' canvas, or black and grey only for the black and white drawing mode.
        ''' </summary>
        Public Function CreatePalette(text As SKColor, dark As Boolean, blackAndWhite As Boolean) As Palette
            If blackAndWhite Then
                Return New Palette With {.Text = SKColors.Black, .Muted = New SKColor(80, 80, 80), .Link = SKColors.Black,
                    .Border = New SKColor(150, 150, 150), .CodeBack = New SKColor(238, 238, 238), .HeaderBack = New SKColor(245, 245, 245)}
            ElseIf dark Then
                Return New Palette With {.Text = text, .Muted = New SKColor(145, 155, 166), .Link = New SKColor(88, 166, 255),
                    .Border = New SKColor(68, 76, 86), .CodeBack = New SKColor(48, 54, 61), .HeaderBack = New SKColor(36, 41, 47)}
            Else
                Return New Palette With {.Text = text}
            End If
        End Function

        ''' <summary>The typefaces of a layout; missing ones fall back to the embedded Asap and JetBrains Mono.</summary>
        Public Class FontSet
            Public Regular, Bold, Italic, BoldItalic, Mono As SKTypeface

            Friend Function Pick(isBold As Boolean, isItalic As Boolean, isCode As Boolean) As SKTypeface
                If isCode Then Return Mono
                If isBold AndAlso isItalic Then Return BoldItalic
                If isBold Then Return Bold
                If isItalic Then Return Italic
                Return Regular
            End Function
        End Class

        Private ReadOnly FontCache As New Dictionary(Of String, SKTypeface)
        Private ReadOnly FontLock As New Object

        ''' <summary>An embedded font of the drawing library by file name (without .ttf).</summary>
        Public Function EmbeddedFont(name As String) As SKTypeface
            SyncLock FontLock
                Dim tf As SKTypeface = Nothing
                If FontCache.TryGetValue(name, tf) Then Return tf
                Using s = GetType(MarkdownRenderer).Assembly.GetManifestResourceStream("DWSIM.Drawing.SkiaSharp." & name & ".ttf")
                    If s IsNot Nothing Then tf = SKTypeface.FromStream(s)
                End Using
                If tf Is Nothing Then tf = SKTypeface.Default
                FontCache(name) = tf
                Return tf
            End SyncLock
        End Function

        ''' <summary>The fonts of the flowsheet (the drawing surface's typefaces) with the monospaced font for code.</summary>
        Public Function CreateFontSet(regular As SKTypeface, bold As SKTypeface, italic As SKTypeface, boldItalic As SKTypeface) As FontSet
            Return New FontSet With {
                .Regular = If(regular, EmbeddedFont("Asap-Regular")),
                .Bold = If(bold, EmbeddedFont("Asap-SemiBold")),
                .Italic = If(italic, EmbeddedFont("Asap-Italic")),
                .BoldItalic = If(boldItalic, EmbeddedFont("Asap-SemiBoldItalic")),
                .Mono = EmbeddedFont("JetBrainsMono-Regular")}
        End Function

#End Region

#Region "Blocks"

        Friend Enum MdKind
            Paragraph
            Heading
            ListItem
            Code
            Quote
            Rule
            Table
        End Enum

        Friend Class MdBlock
            Public Kind As MdKind
            ''' <summary>The heading level (1 to 6) or the nesting depth of a list item (from 0).</summary>
            Public Level As Integer
            Public Ordered As Boolean
            Public Number As Integer
            ''' <summary>-1 for an ordinary list item, 0 for an open task, 1 for a done one.</summary>
            Public Task As Integer = -1
            Public QuoteDepth As Integer
            Public Text As New StringBuilder
            Public Lines As New List(Of String)
            Public Header As List(Of String)
            Public Aligns As List(Of Integer)
            Public Rows As New List(Of List(Of String))
        End Class

        Private ReadOnly FenceRx As New Regex("^( {0,3})(`{3,}|~{3,})(.*)$")
        Private ReadOnly HeadingRx As New Regex("^ {0,3}(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$")
        Private ReadOnly SetextRx As New Regex("^ {0,3}(=+|-+)[ \t]*$")
        Private ReadOnly RuleRx As New Regex("^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$")
        Private ReadOnly SeparatorRx As New Regex("^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$")
        Private ReadOnly ListRx As New Regex("^( *)([-*+]|(\d{1,9})[.)])(?:[ \t]+(.*))?$")
        Private ReadOnly TaskRx As New Regex("^\[([ xX])\][ \t]+(.*)$")

        ''' <summary>Splits Markdown into blocks.</summary>
        Friend Function ParseBlocks(md As String) As List(Of MdBlock)

            Dim blocks As New List(Of MdBlock)
            If String.IsNullOrEmpty(md) Then Return blocks

            Dim lines = md.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(ChrW(10))
            Dim cur As MdBlock = Nothing
            Dim listIndents As New List(Of Integer)
            Dim prevBlank = False
            Dim i = 0

            While i < lines.Length

                Dim raw = lines(i).Replace(vbTab, "    ")
                Dim t = raw.Trim()
                Dim indent = raw.Length - raw.TrimStart(" "c).Length

                If t.Length = 0 Then
                    cur = Nothing
                    prevBlank = True
                    i += 1
                    Continue While
                End If

                ' fenced code block
                Dim fm = FenceRx.Match(raw)
                If fm.Success Then
                    Dim fence = fm.Groups(2).Value
                    Dim baseIndent = fm.Groups(1).Value.Length
                    Dim b As New MdBlock With {.Kind = MdKind.Code}
                    i += 1
                    While i < lines.Length
                        Dim l = lines(i).Replace(vbTab, "    ")
                        Dim lt = l.Trim()
                        If lt.Length >= fence.Length AndAlso lt.Trim(fence(0)).Length = 0 Then
                            i += 1
                            Exit While
                        End If
                        Dim k = 0
                        While k < baseIndent AndAlso k < l.Length AndAlso l(k) = " "c
                            k += 1
                        End While
                        b.Lines.Add(l.Substring(k))
                        i += 1
                    End While
                    blocks.Add(b)
                    cur = Nothing
                    prevBlank = False
                    listIndents.Clear()
                    Continue While
                End If

                ' ATX heading
                Dim hm = HeadingRx.Match(raw)
                If hm.Success Then
                    Dim b As New MdBlock With {.Kind = MdKind.Heading, .Level = hm.Groups(1).Value.Length}
                    b.Text.Append(hm.Groups(2).Value)
                    blocks.Add(b)
                    cur = Nothing
                    prevBlank = False
                    listIndents.Clear()
                    i += 1
                    Continue While
                End If

                ' setext heading: a paragraph underlined with = or -
                If cur IsNot Nothing AndAlso cur.Kind = MdKind.Paragraph AndAlso Not prevBlank AndAlso SetextRx.IsMatch(raw) Then
                    cur.Kind = MdKind.Heading
                    cur.Level = If(t.StartsWith("="), 1, 2)
                    cur = Nothing
                    i += 1
                    Continue While
                End If

                ' horizontal rule
                If RuleRx.IsMatch(raw) Then
                    blocks.Add(New MdBlock With {.Kind = MdKind.Rule})
                    cur = Nothing
                    prevBlank = False
                    listIndents.Clear()
                    i += 1
                    Continue While
                End If

                ' table: a row with pipes over a separator row
                If t.Contains("|") AndAlso i + 1 < lines.Length AndAlso lines(i + 1).Contains("|") AndAlso SeparatorRx.IsMatch(lines(i + 1)) Then
                    Dim b As New MdBlock With {.Kind = MdKind.Table}
                    b.Header = SplitRow(raw)
                    Dim n = b.Header.Count
                    b.Aligns = New List(Of Integer)
                    For Each c In SplitRow(lines(i + 1))
                        Dim cs = c.Trim()
                        If cs.StartsWith(":") AndAlso cs.EndsWith(":") Then
                            b.Aligns.Add(1)
                        ElseIf cs.EndsWith(":") Then
                            b.Aligns.Add(2)
                        Else
                            b.Aligns.Add(0)
                        End If
                    Next
                    While b.Aligns.Count < n
                        b.Aligns.Add(0)
                    End While
                    i += 2
                    While i < lines.Length AndAlso lines(i).Trim().Length > 0 AndAlso lines(i).Contains("|")
                        Dim row = SplitRow(lines(i))
                        While row.Count < n
                            row.Add("")
                        End While
                        If row.Count > n Then row = row.GetRange(0, n)
                        b.Rows.Add(row)
                        i += 1
                    End While
                    blocks.Add(b)
                    cur = Nothing
                    prevBlank = False
                    listIndents.Clear()
                    Continue While
                End If

                ' block quote
                If t.StartsWith(">") AndAlso indent <= 3 Then
                    Dim s = t
                    Dim depth = 0
                    While s.StartsWith(">")
                        depth += 1
                        s = s.Substring(1).TrimStart()
                    End While
                    If s.Length = 0 Then
                        cur = Nothing
                    ElseIf cur IsNot Nothing AndAlso cur.Kind = MdKind.Quote AndAlso cur.QuoteDepth = depth AndAlso Not prevBlank Then
                        AppendLine(cur, s)
                    Else
                        cur = New MdBlock With {.Kind = MdKind.Quote, .QuoteDepth = depth}
                        cur.Text.Append(s)
                        blocks.Add(cur)
                    End If
                    prevBlank = False
                    listIndents.Clear()
                    i += 1
                    Continue While
                End If

                ' list item
                Dim lm = ListRx.Match(raw)
                If lm.Success AndAlso (lm.Groups(4).Success OrElse cur Is Nothing) Then
                    While listIndents.Count > 0 AndAlso indent < listIndents(listIndents.Count - 1) - 1
                        listIndents.RemoveAt(listIndents.Count - 1)
                    End While
                    If listIndents.Count = 0 OrElse indent > listIndents(listIndents.Count - 1) + 1 Then listIndents.Add(indent)
                    Dim b As New MdBlock With {.Kind = MdKind.ListItem, .Level = listIndents.Count - 1}
                    If lm.Groups(3).Success Then
                        b.Ordered = True
                        b.Number = Integer.Parse(lm.Groups(3).Value)
                    End If
                    Dim content = If(lm.Groups(4).Success, lm.Groups(4).Value, "")
                    Dim tm = TaskRx.Match(content)
                    If tm.Success Then
                        b.Task = If(tm.Groups(1).Value = " ", 0, 1)
                        content = tm.Groups(2).Value
                    End If
                    b.Text.Append(content)
                    blocks.Add(b)
                    cur = b
                    prevBlank = False
                    i += 1
                    Continue While
                End If

                ' text: continues the paragraph, list item or quote above it, or starts a paragraph
                If cur IsNot Nothing AndAlso Not prevBlank AndAlso (cur.Kind = MdKind.Paragraph OrElse cur.Kind = MdKind.ListItem OrElse cur.Kind = MdKind.Quote) Then
                    AppendLine(cur, raw.TrimStart())
                ElseIf prevBlank AndAlso indent >= 2 AndAlso blocks.Count > 0 AndAlso blocks(blocks.Count - 1).Kind = MdKind.ListItem Then
                    ' a later paragraph of a list item
                    cur = blocks(blocks.Count - 1)
                    cur.Text.Append(ChrW(10)).Append(raw.TrimStart())
                Else
                    cur = New MdBlock With {.Kind = MdKind.Paragraph}
                    cur.Text.Append(raw.TrimStart())
                    blocks.Add(cur)
                    listIndents.Clear()
                End If
                prevBlank = False
                i += 1

            End While

            Return blocks

        End Function

        ''' <summary>Joins a line to a block: a line break when the previous one ends with two spaces or a backslash, a space otherwise.</summary>
        Private Sub AppendLine(b As MdBlock, line As String)
            Dim s = b.Text
            If s.Length >= 1 AndAlso s(s.Length - 1) = "\"c AndAlso (s.Length < 2 OrElse s(s.Length - 2) <> "\"c) Then
                s.Length -= 1
                s.Append(ChrW(10))
            ElseIf s.Length >= 2 AndAlso s(s.Length - 1) = " "c AndAlso s(s.Length - 2) = " "c Then
                s.Append(ChrW(10))
            Else
                s.Append(" "c)
            End If
            s.Append(line)
        End Sub

        ''' <summary>The cells of a table row (leading and trailing pipes dropped, \| kept as a pipe).</summary>
        Private Function SplitRow(line As String) As List(Of String)
            Dim s = line.Trim()
            If s.StartsWith("|") Then s = s.Substring(1)
            If s.EndsWith("|") AndAlso Not s.EndsWith("\|") Then s = s.Substring(0, s.Length - 1)
            Dim cells As New List(Of String)
            Dim sb As New StringBuilder
            Dim inCode = False
            For k = 0 To s.Length - 1
                Dim c = s(k)
                If c = "\"c AndAlso k + 1 < s.Length AndAlso s(k + 1) = "|"c Then
                    sb.Append("|"c)
                    k += 1
                ElseIf c = "`"c Then
                    inCode = Not inCode
                    sb.Append(c)
                ElseIf c = "|"c AndAlso Not inCode Then
                    cells.Add(sb.ToString().Trim())
                    sb.Clear()
                Else
                    sb.Append(c)
                End If
            Next
            cells.Add(sb.ToString().Trim())
            Return cells
        End Function

#End Region

#Region "Inlines"

        Friend Class MdRun
            Public Text As String = ""
            Public Bold, Italic, Code, Strike, Break As Boolean
            Public Link As String
        End Class

        Private ReadOnly AutoLinkRx As New Regex("^<((?:https?|ftp|mailto):[^>\s]+)>")
        Private ReadOnly BreakTagRx As New Regex("^<br\s*/?>", RegexOptions.IgnoreCase)

        ''' <summary>The styled runs of a block's text.</summary>
        Friend Function ParseInlines(text As String, Optional bold0 As Boolean = False, Optional italic0 As Boolean = False,
                                     Optional strike0 As Boolean = False, Optional link As String = Nothing) As List(Of MdRun)

            Dim runs As New List(Of MdRun)
            If String.IsNullOrEmpty(text) Then Return runs

            Dim sb As New StringBuilder
            Dim bold = bold0, italic = italic0, strike = strike0
            Dim flush As Action = Sub()
                                      If sb.Length > 0 Then
                                          runs.Add(New MdRun With {.Text = sb.ToString(), .Bold = bold, .Italic = italic, .Strike = strike, .Link = link})
                                          sb.Clear()
                                      End If
                                  End Sub

            Dim len = text.Length
            Dim i = 0
            While i < len

                Dim c = text(i)

                ' escaped punctuation
                If c = "\"c AndAlso i + 1 < len AndAlso Char.IsPunctuation(text(i + 1)) OrElse
                   c = "\"c AndAlso i + 1 < len AndAlso Char.IsSymbol(text(i + 1)) Then
                    sb.Append(text(i + 1))
                    i += 2
                    Continue While
                End If

                ' line break
                If c = ChrW(10) Then
                    flush()
                    runs.Add(New MdRun With {.Break = True})
                    i += 1
                    Continue While
                End If

                ' inline code
                If c = "`"c Then
                    Dim n = 0
                    While i + n < len AndAlso text(i + n) = "`"c
                        n += 1
                    End While
                    Dim closer = text.IndexOf(New String("`"c, n), i + n, StringComparison.Ordinal)
                    If closer > 0 Then
                        flush()
                        Dim code = text.Substring(i + n, closer - i - n).Replace(ChrW(10), " "c)
                        If code.Length > 2 AndAlso code.StartsWith(" ") AndAlso code.EndsWith(" ") Then code = code.Substring(1, code.Length - 2)
                        runs.Add(New MdRun With {.Text = code, .Code = True, .Bold = bold, .Italic = italic, .Strike = strike, .Link = link})
                        i = closer + n
                    Else
                        sb.Append(New String("`"c, n))
                        i += n
                    End If
                    Continue While
                End If

                ' link or image: [text](url), ![alt](url)
                If c = "["c OrElse (c = "!"c AndAlso i + 1 < len AndAlso text(i + 1) = "["c) Then
                    Dim isImage = c = "!"c
                    Dim start = If(isImage, i + 1, i)
                    Dim close = MatchingBracket(text, start)
                    If close > 0 AndAlso close + 1 < len AndAlso text(close + 1) = "("c Then
                        Dim endParen = text.IndexOf(")"c, close + 2)
                        If endParen > 0 Then
                            flush()
                            Dim inner = text.Substring(start + 1, close - start - 1)
                            Dim url = text.Substring(close + 2, endParen - close - 2).Trim()
                            Dim sp = url.IndexOf(" "c)
                            If sp > 0 Then url = url.Substring(0, sp)
                            If isImage Then
                                runs.Add(New MdRun With {.Text = If(inner.Length > 0, inner, url), .Italic = True, .Bold = bold, .Strike = strike, .Link = link})
                            Else
                                runs.AddRange(ParseInlines(inner, bold, italic, strike, If(url.Length > 0, url, "#")))
                            End If
                            i = endParen + 1
                            Continue While
                        End If
                    End If
                    sb.Append(c)
                    i += 1
                    Continue While
                End If

                ' autolink <https://...> and <br>
                If c = "<"c Then
                    Dim rest = text.Substring(i)
                    Dim am = AutoLinkRx.Match(rest)
                    If am.Success Then
                        flush()
                        runs.Add(New MdRun With {.Text = am.Groups(1).Value, .Bold = bold, .Italic = italic, .Strike = strike, .Link = am.Groups(1).Value})
                        i += am.Length
                        Continue While
                    End If
                    Dim bm = BreakTagRx.Match(rest)
                    If bm.Success Then
                        flush()
                        runs.Add(New MdRun With {.Break = True})
                        i += bm.Length
                        Continue While
                    End If
                End If

                ' emphasis
                If c = "*"c OrElse c = "_"c Then
                    Dim n = 0
                    While i + n < len AndAlso text(i + n) = c
                        n += 1
                    End While
                    Dim prevCh = If(i > 0, text(i - 1), " "c)
                    Dim nextCh = If(i + n < len, text(i + n), " "c)
                    Dim canOpen = Not Char.IsWhiteSpace(nextCh)
                    Dim canClose = Not Char.IsWhiteSpace(prevCh)
                    If c = "_"c Then
                        ' snake_case words keep their underscores
                        canOpen = canOpen AndAlso Not Char.IsLetterOrDigit(prevCh)
                        canClose = canClose AndAlso Not Char.IsLetterOrDigit(nextCh)
                    End If
                    While n > 0
                        If n >= 2 AndAlso bold AndAlso canClose Then
                            flush() : bold = False : n -= 2 : i += 2
                        ElseIf n >= 2 AndAlso Not bold AndAlso canOpen AndAlso text.IndexOf(New String(c, 2), i + 2, StringComparison.Ordinal) > 0 Then
                            flush() : bold = True : n -= 2 : i += 2
                        ElseIf italic AndAlso canClose Then
                            flush() : italic = False : n -= 1 : i += 1
                        ElseIf Not italic AndAlso canOpen AndAlso HasSingleCloser(text, c, i + n) Then
                            flush() : italic = True : n -= 1 : i += 1
                        Else
                            sb.Append(c) : n -= 1 : i += 1
                        End If
                    End While
                    Continue While
                End If

                ' strikethrough
                If c = "~"c AndAlso i + 1 < len AndAlso text(i + 1) = "~"c Then
                    If strike Then
                        flush() : strike = False
                    ElseIf text.IndexOf("~~", i + 2, StringComparison.Ordinal) > 0 Then
                        flush() : strike = True
                    Else
                        sb.Append("~~")
                    End If
                    i += 2
                    Continue While
                End If

                sb.Append(c)
                i += 1

            End While

            flush()
            Return runs

        End Function

        ''' <summary>The closing bracket of the bracket at <paramref name="start"/>, or -1.</summary>
        Private Function MatchingBracket(text As String, start As Integer) As Integer
            Dim depth = 0
            For k = start To text.Length - 1
                Select Case text(k)
                    Case "\"c : k += 1
                    Case "["c : depth += 1
                    Case "]"c
                        depth -= 1
                        If depth = 0 Then Return k
                End Select
            Next
            Return -1
        End Function

        ''' <summary>True when a lone <paramref name="c"/> (not part of a pair) follows <paramref name="from"/>.</summary>
        Private Function HasSingleCloser(text As String, c As Char, from As Integer) As Boolean
            Dim k = from
            While k < text.Length
                If text(k) = c Then
                    Dim n = 0
                    While k + n < text.Length AndAlso text(k + n) = c
                        n += 1
                    End While
                    If n <> 2 AndAlso Not Char.IsWhiteSpace(text(k - 1)) Then Return True
                    k += n
                Else
                    k += 1
                End If
            End While
            Return False
        End Function

#End Region

#Region "Layout"

        Friend Enum OpKind
            Text
            FillRect
            RoundRect
            StrokeRect
            Line
            FillCircle
            StrokeCircle
            CheckMark
        End Enum

        Friend Class DrawOp
            Public Kind As OpKind
            Public Text As String
            Public X, Y, X2, Y2 As Single
            Public Rect As SKRect
            Public Color As SKColor
            Public Typeface As SKTypeface
            Public Size As Single
            Public StrokeWidth As Single = 1.0F
        End Class

        ''' <summary>A laid out Markdown text: drawing operations relative to its top left corner.</summary>
        Public Class Layout

            Friend ReadOnly Ops As New List(Of DrawOp)

            ''' <summary>The width the text was laid out to.</summary>
            Public Property Width As Single

            ''' <summary>The height of the laid out text.</summary>
            Public Property Height As Single

            ''' <summary>Draws the text with its top left corner at (<paramref name="x"/>, <paramref name="y"/>).</summary>
            Public Sub Draw(canvas As SKCanvas, x As Single, y As Single)
                Using p As New SKPaint With {.IsAntialias = True, .SubpixelText = True}
                    For Each op In Ops
                        p.Color = op.Color
                        p.Style = SKPaintStyle.Fill
                        p.StrokeWidth = op.StrokeWidth
                        Select Case op.Kind
                            Case OpKind.Text
                                p.Typeface = op.Typeface
                                p.TextSize = op.Size
                                canvas.DrawText(op.Text, x + op.X, y + op.Y, p)
                            Case OpKind.FillRect
                                canvas.DrawRect(Offset(op.Rect, x, y), p)
                            Case OpKind.RoundRect
                                canvas.DrawRoundRect(Offset(op.Rect, x, y), op.Size, op.Size, p)
                            Case OpKind.StrokeRect
                                p.Style = SKPaintStyle.Stroke
                                canvas.DrawRoundRect(Offset(op.Rect, x, y), op.Size, op.Size, p)
                            Case OpKind.Line
                                p.Style = SKPaintStyle.Stroke
                                canvas.DrawLine(x + op.X, y + op.Y, x + op.X2, y + op.Y2, p)
                            Case OpKind.FillCircle
                                canvas.DrawCircle(x + op.X, y + op.Y, op.Size, p)
                            Case OpKind.StrokeCircle
                                p.Style = SKPaintStyle.Stroke
                                canvas.DrawCircle(x + op.X, y + op.Y, op.Size, p)
                            Case OpKind.CheckMark
                                p.Style = SKPaintStyle.Stroke
                                p.StrokeCap = SKStrokeCap.Round
                                p.StrokeJoin = SKStrokeJoin.Round
                                Using path As New SKPath()
                                    Dim r = Offset(op.Rect, x, y)
                                    path.MoveTo(r.Left + r.Width * 0.22F, r.Top + r.Height * 0.52F)
                                    path.LineTo(r.Left + r.Width * 0.42F, r.Top + r.Height * 0.72F)
                                    path.LineTo(r.Left + r.Width * 0.78F, r.Top + r.Height * 0.3F)
                                    canvas.DrawPath(path, p)
                                End Using
                        End Select
                    Next
                End Using
            End Sub

            Private Shared Function Offset(r As SKRect, x As Single, y As Single) As SKRect
                Return New SKRect(r.Left + x, r.Top + y, r.Right + x, r.Bottom + y)
            End Function

        End Class

        Private Class Ctx
            Public Fonts As FontSet
            Public Pal As Palette
            Public Size As Single
            Public ReadOnly Paint As New SKPaint With {.IsAntialias = True, .SubpixelText = True}

            Public Function Measure(text As String, tf As SKTypeface, size As Single) As Single
                Paint.Typeface = tf
                Paint.TextSize = size
                Return Paint.MeasureText(text)
            End Function

            ''' <summary>Ascent and descent (both positive) of the regular font at a size.</summary>
            Public Function Metrics(size As Single) As LineMetrics
                Paint.Typeface = Fonts.Regular
                Paint.TextSize = size
                Dim fm = Paint.FontMetrics
                Return New LineMetrics With {.Asc = -fm.Ascent, .Desc = fm.Descent}
            End Function

            Private ReadOnly Fallbacks As New Dictionary(Of Long, SKTypeface)

            ''' <summary>
            ''' Splits text into runs drawn with one typeface each: the given one, or for a character it doesn't
            ''' have (arrows, Greek letters, math signs), a system font that has it in the same style.
            ''' </summary>
            Public Function Segments(text As String, tf As SKTypeface) As List(Of KeyValuePair(Of String, SKTypeface))
                Dim result As New List(Of KeyValuePair(Of String, SKTypeface))
                If String.IsNullOrEmpty(text) Then Return result
                Dim sb As New StringBuilder
                Dim current As SKTypeface = tf
                Dim i = 0
                While i < text.Length
                    Dim len = If(Char.IsHighSurrogate(text(i)) AndAlso i + 1 < text.Length AndAlso Char.IsLowSurrogate(text(i + 1)), 2, 1)
                    Dim cp = If(len = 2, Char.ConvertToUtf32(text(i), text(i + 1)), AscW(text(i)))
                    Dim use = tf
                    If cp > 127 AndAlso Not Char.IsWhiteSpace(text(i)) AndAlso tf.GetGlyph(cp) = 0 Then use = Fallback(tf, cp)
                    If use IsNot current AndAlso sb.Length > 0 Then
                        result.Add(New KeyValuePair(Of String, SKTypeface)(sb.ToString(), current))
                        sb.Clear()
                    End If
                    current = use
                    sb.Append(text, i, len)
                    i += len
                End While
                If sb.Length > 0 Then result.Add(New KeyValuePair(Of String, SKTypeface)(sb.ToString(), current))
                Return result
            End Function

            Private Function Fallback(tf As SKTypeface, cp As Integer) As SKTypeface
                Dim key = (CLng(tf.GetHashCode()) << 32) Or CLng(cp)
                Dim f As SKTypeface = Nothing
                If Fallbacks.TryGetValue(key, f) Then Return f
                Try
                    f = SKFontManager.Default.MatchCharacter(tf.FamilyName, tf.FontStyle, Nothing, cp)
                Catch
                    f = Nothing
                End Try
                If f Is Nothing Then f = tf
                Fallbacks(key) = f
                Return f
            End Function

            ''' <summary>The width of text drawn with its font fallbacks.</summary>
            Public Function MeasureMixed(text As String, tf As SKTypeface, size As Single) As Single
                Dim w As Single = 0
                For Each seg In Segments(text, tf)
                    w += Measure(seg.Key, seg.Value, size)
                Next
                Return w
            End Function

            Public Function LineHeight(size As Single) As Single
                Dim m = Metrics(size)
                Return (m.Asc + m.Desc) * 1.18F
            End Function

            Public Function Baseline(top As Single, size As Single) As Single
                Dim m = Metrics(size)
                Dim lh = (m.Asc + m.Desc) * 1.18F
                Return top + (lh - m.Asc - m.Desc) / 2 + m.Asc
            End Function
        End Class

        Private Structure LineMetrics
            Public Asc As Single
            Public Desc As Single
        End Structure

        Private Class Tok
            Public Text As String
            Public Run As MdRun
            Public W As Single
            Public X As Single
            Public IsSpace As Boolean
            Public IsBreak As Boolean
            Public TF As SKTypeface
            Public TS As Single
            ''' <summary>Continues the word of the token before it (a run of another font inside a word).</summary>
            Public Glue As Boolean
        End Class

        ''' <summary>Lays out Markdown to a width.</summary>
        Public Function Build(md As String, width As Single, size As Single, fonts As FontSet, pal As Palette) As Layout

            Dim lay As New Layout With {.Width = width}
            Dim cx As New Ctx With {.Fonts = fonts, .Pal = pal, .Size = size}
            Dim blocks = ParseBlocks(md)
            Dim y As Single = 0
            Dim prev As MdBlock = Nothing

            Try
                For Each b In blocks
                    If prev IsNot Nothing Then y += Gap(prev, b, size)
                    Select Case b.Kind
                        Case MdKind.Heading
                            Dim scales = {1.75F, 1.42F, 1.2F, 1.05F, 0.95F, 0.9F}
                            Dim hs = size * scales(Math.Max(1, Math.Min(6, b.Level)) - 1)
                            y += LayoutInlines(cx, ParseInlines(b.Text.ToString()), 0, width, y, hs,
                                               If(b.Level = 6, pal.Muted, pal.Text), lay.Ops, 0, True)
                            If b.Level <= 2 Then
                                y += size * 0.3F
                                lay.Ops.Add(New DrawOp With {.Kind = OpKind.Line, .X = 0, .Y = y, .X2 = width, .Y2 = y, .Color = pal.Border, .StrokeWidth = 1})
                                y += 1
                            End If
                        Case MdKind.Paragraph
                            y += LayoutInlines(cx, ParseInlines(b.Text.ToString()), 0, width, y, size, pal.Text, lay.Ops, 0, False)
                        Case MdKind.ListItem
                            y += LayoutListItem(cx, b, width, y, lay.Ops)
                        Case MdKind.Quote
                            Dim bar = size * 0.25F
                            Dim stepX = size * 0.9F
                            Dim x0 = b.QuoteDepth * stepX
                            Dim h = LayoutInlines(cx, ParseInlines(b.Text.ToString()), x0, width - x0, y, size, pal.Muted, lay.Ops, 0, False)
                            For d = 0 To b.QuoteDepth - 1
                                lay.Ops.Add(New DrawOp With {.Kind = OpKind.FillRect, .Rect = New SKRect(d * stepX, y, d * stepX + bar, y + h), .Color = pal.Border})
                            Next
                            y += h
                        Case MdKind.Code
                            y += LayoutCode(cx, b, width, y, lay.Ops)
                        Case MdKind.Rule
                            y += size * 0.35F
                            lay.Ops.Add(New DrawOp With {.Kind = OpKind.FillRect, .Rect = New SKRect(0, y, width, y + Math.Max(2.0F, size * 0.15F)), .Color = pal.Border})
                            y += Math.Max(2.0F, size * 0.15F) + size * 0.35F
                        Case MdKind.Table
                            y += LayoutTable(cx, b, width, y, lay.Ops)
                    End Select
                    prev = b
                Next
            Finally
                cx.Paint.Dispose()
            End Try

            lay.Height = y
            Return lay

        End Function

        Private Function Gap(a As MdBlock, b As MdBlock, size As Single) As Single
            If a.Kind = MdKind.ListItem AndAlso b.Kind = MdKind.ListItem Then Return size * 0.22F
            If b.Kind = MdKind.Heading Then Return size * 0.95F
            If a.Kind = MdKind.Heading Then Return size * 0.5F
            If a.Kind = MdKind.Quote AndAlso b.Kind = MdKind.Quote Then Return size * 0.45F
            Return size * 0.7F
        End Function

        ''' <summary>Lays out styled runs between x0 and x0 + maxW from y0, wrapping at spaces; returns the height used.</summary>
        Private Function LayoutInlines(cx As Ctx, runs As List(Of MdRun), x0 As Single, maxW As Single, y0 As Single, size As Single,
                                       color As SKColor, ops As List(Of DrawOp), align As Integer, forceBold As Boolean) As Single

            Dim lh = cx.LineHeight(size)
            If runs.Count = 0 Then Return lh
            maxW = Math.Max(maxW, size)

            ' tokens: words, spaces and line breaks
            Dim toks As New List(Of Tok)
            For Each r In runs
                If r.Break Then
                    toks.Add(New Tok With {.IsBreak = True, .Run = r})
                    Continue For
                End If
                Dim txt = If(r.Code, r.Text, Regex.Replace(r.Text, "\s+", " "))
                Dim tf = cx.Fonts.Pick(r.Bold OrElse forceBold, r.Italic, r.Code)
                Dim ts = If(r.Code, size * 0.88F, size)
                Dim first = True
                For Each piece In Regex.Split(txt, "( +)")
                    If piece.Length = 0 Then Continue For
                    If piece.Trim().Length = 0 Then
                        toks.Add(New Tok With {.Text = piece, .Run = r, .IsSpace = True, .TF = tf, .TS = ts, .W = cx.Measure(piece, tf, ts)})
                        first = True
                        Continue For
                    End If
                    ' a word continues the previous run's word when the run starts without a space
                    Dim glueToPrevious = first AndAlso toks.Count > 0 AndAlso Not toks(toks.Count - 1).IsSpace AndAlso Not toks(toks.Count - 1).IsBreak
                    Dim k = 0
                    For Each seg In cx.Segments(piece, tf)
                        toks.Add(New Tok With {.Text = seg.Key, .Run = r, .TF = seg.Value, .TS = ts, .W = cx.Measure(seg.Key, seg.Value, ts),
                                 .Glue = If(k = 0, glueToPrevious, True)})
                        k += 1
                    Next
                    first = False
                Next
            Next

            ' lines
            Dim lines As New List(Of List(Of Tok))
            Dim line As New List(Of Tok)
            Dim lw As Single = 0
            Dim ti = 0
            While ti < toks.Count
                Dim tk = toks(ti)
                If tk.IsBreak Then
                    lines.Add(line)
                    line = New List(Of Tok)
                    lw = 0
                    ti += 1
                    Continue While
                End If
                If tk.IsSpace Then
                    If line.Count > 0 Then
                        tk.X = lw
                        line.Add(tk)
                        lw += tk.W
                    End If
                    ti += 1
                    Continue While
                End If
                ' the word: this token and the tokens glued to it
                Dim word As New List(Of Tok) From {tk}
                Dim tj = ti + 1
                While tj < toks.Count AndAlso toks(tj).Glue AndAlso Not toks(tj).IsSpace AndAlso Not toks(tj).IsBreak
                    word.Add(toks(tj))
                    tj += 1
                End While
                Dim ww = word.Sum(Function(o) o.W)
                If lw + ww > maxW AndAlso line.Any(Function(o) Not o.IsSpace) Then
                    While line.Count > 0 AndAlso line(line.Count - 1).IsSpace
                        line.RemoveAt(line.Count - 1)
                    End While
                    lines.Add(line)
                    line = New List(Of Tok)
                    lw = 0
                End If
                For Each wt In word
                    If ww > maxW AndAlso lw + wt.W > maxW Then
                        ' a word wider than the line: broken by characters
                        Dim restText = wt.Text
                        While restText.Length > 0
                            Dim n = 1
                            While n < restText.Length AndAlso lw + cx.Measure(restText.Substring(0, n + 1), wt.TF, wt.TS) <= maxW
                                n += 1
                            End While
                            If lw > 0 AndAlso lw + cx.Measure(restText.Substring(0, n), wt.TF, wt.TS) > maxW Then
                                lines.Add(line)
                                line = New List(Of Tok)
                                lw = 0
                                Continue While
                            End If
                            Dim part = restText.Substring(0, n)
                            Dim pw = cx.Measure(part, wt.TF, wt.TS)
                            line.Add(New Tok With {.Text = part, .Run = wt.Run, .W = pw, .X = lw, .TF = wt.TF, .TS = wt.TS})
                            lw += pw
                            restText = restText.Substring(n)
                            If restText.Length > 0 Then
                                lines.Add(line)
                                line = New List(Of Tok)
                                lw = 0
                            End If
                        End While
                    Else
                        wt.X = lw
                        line.Add(wt)
                        lw += wt.W
                    End If
                Next
                ti = tj
            End While
            While line.Count > 0 AndAlso line(line.Count - 1).IsSpace
                line.RemoveAt(line.Count - 1)
            End While
            lines.Add(line)

            ' operations
            Dim m = cx.Metrics(size)
            Dim y = y0
            For Each ln In lines
                Dim baseline = cx.Baseline(y, size)
                Dim used = If(ln.Count > 0, ln(ln.Count - 1).X + ln(ln.Count - 1).W, 0)
                Dim dx = x0 + If(align = 1, (maxW - used) / 2, If(align = 2, maxW - used, 0))
                ' code backgrounds first, one per run of code tokens
                Dim k = 0
                While k < ln.Count
                    If ln(k).Run.Code Then
                        Dim j = k
                        While j + 1 < ln.Count AndAlso ln(j + 1).Run Is ln(k).Run
                            j += 1
                        End While
                        Dim pad = size * 0.18F
                        ops.Add(New DrawOp With {.Kind = OpKind.RoundRect, .Size = size * 0.18F, .Color = cx.Pal.CodeBack,
                                .Rect = New SKRect(dx + ln(k).X - pad, baseline - m.Asc * 0.92F, dx + ln(j).X + ln(j).W + pad, baseline + m.Desc * 0.9F)})
                        k = j + 1
                    Else
                        k += 1
                    End If
                End While
                For Each tk In ln
                    Dim r = tk.Run
                    Dim tf = tk.TF
                    Dim ts = tk.TS
                    Dim col = If(r.Link IsNot Nothing, cx.Pal.Link, color)
                    If Not tk.IsSpace Then
                        ops.Add(New DrawOp With {.Kind = OpKind.Text, .Text = tk.Text, .X = dx + tk.X, .Y = baseline, .Color = col, .Typeface = tf, .Size = ts})
                    End If
                    If r.Link IsNot Nothing Then
                        ops.Add(New DrawOp With {.Kind = OpKind.Line, .X = dx + tk.X, .Y = baseline + size * 0.12F, .X2 = dx + tk.X + tk.W, .Y2 = baseline + size * 0.12F,
                                .Color = col, .StrokeWidth = Math.Max(1.0F, size * 0.06F)})
                    End If
                    If r.Strike Then
                        ops.Add(New DrawOp With {.Kind = OpKind.Line, .X = dx + tk.X, .Y = baseline - m.Asc * 0.3F, .X2 = dx + tk.X + tk.W, .Y2 = baseline - m.Asc * 0.3F,
                                .Color = col, .StrokeWidth = Math.Max(1.0F, size * 0.07F)})
                    End If
                Next
                y += lh
            Next

            Return y - y0

        End Function

        ''' <summary>The width of runs on a single line (the widest line when they hold breaks).</summary>
        Private Function MeasureInlines(cx As Ctx, runs As List(Of MdRun), size As Single, forceBold As Boolean) As Single
            Dim best As Single = 0, w As Single = 0
            For Each r In runs
                If r.Break Then
                    best = Math.Max(best, w)
                    w = 0
                    Continue For
                End If
                Dim txt = If(r.Code, r.Text, Regex.Replace(r.Text, "\s+", " "))
                w += cx.MeasureMixed(txt, cx.Fonts.Pick(r.Bold OrElse forceBold, r.Italic, r.Code), If(r.Code, size * 0.88F, size))
            Next
            Return Math.Max(best, w)
        End Function

        Private Function LayoutListItem(cx As Ctx, b As MdBlock, width As Single, y As Single, ops As List(Of DrawOp)) As Single
            Dim size = cx.Size
            Dim stepX = size * 1.5F
            Dim gutter = size * 1.5F
            Dim xi = b.Level * stepX
            Dim xt = xi + gutter
            Dim lh = cx.LineHeight(size)
            Dim cyMid = y + lh / 2
            Dim m = cx.Metrics(size)
            Dim baseline = cx.Baseline(y, size)
            If b.Task >= 0 Then
                Dim s = size * 0.78F
                Dim r As New SKRect(xi + gutter - s - size * 0.4F, cyMid - s / 2, xi + gutter - size * 0.4F, cyMid + s / 2)
                If b.Task = 1 Then
                    ops.Add(New DrawOp With {.Kind = OpKind.RoundRect, .Rect = r, .Size = s * 0.2F, .Color = cx.Pal.Link})
                    ops.Add(New DrawOp With {.Kind = OpKind.CheckMark, .Rect = r, .Color = SKColors.White, .StrokeWidth = Math.Max(1.2F, s * 0.14F)})
                Else
                    ops.Add(New DrawOp With {.Kind = OpKind.StrokeRect, .Rect = r, .Size = s * 0.2F, .Color = cx.Pal.Muted, .StrokeWidth = Math.Max(1.0F, s * 0.09F)})
                End If
            ElseIf b.Ordered Then
                Dim label = b.Number.ToString() & "."
                Dim w = cx.Measure(label, cx.Fonts.Regular, size)
                ops.Add(New DrawOp With {.Kind = OpKind.Text, .Text = label, .X = xt - size * 0.35F - w, .Y = baseline, .Color = cx.Pal.Text, .Typeface = cx.Fonts.Regular, .Size = size})
            Else
                Dim bx = xi + gutter * 0.5F
                Dim br = size * 0.16F
                Dim cy = baseline - m.Asc * 0.36F
                Select Case b.Level Mod 3
                    Case 0
                        ops.Add(New DrawOp With {.Kind = OpKind.FillCircle, .X = bx, .Y = cy, .Size = br, .Color = cx.Pal.Text})
                    Case 1
                        ops.Add(New DrawOp With {.Kind = OpKind.StrokeCircle, .X = bx, .Y = cy, .Size = br, .Color = cx.Pal.Text, .StrokeWidth = Math.Max(1.0F, size * 0.07F)})
                    Case Else
                        ops.Add(New DrawOp With {.Kind = OpKind.FillRect, .Rect = New SKRect(bx - br, cy - br, bx + br, cy + br), .Color = cx.Pal.Text})
                End Select
            End If
            Return LayoutInlines(cx, ParseInlines(b.Text.ToString()), xt, width - xt, y, size, cx.Pal.Text, ops, 0, False)
        End Function

        Private Function LayoutCode(cx As Ctx, b As MdBlock, width As Single, y0 As Single, ops As List(Of DrawOp)) As Single
            Dim size = cx.Size
            Dim cs = size * 0.88F
            Dim pad = size * 0.65F
            Dim mono = cx.Fonts.Mono
            cx.Paint.Typeface = mono
            cx.Paint.TextSize = cs
            Dim fm = cx.Paint.FontMetrics
            Dim lh = (-fm.Ascent + fm.Descent) * 1.25F
            Dim maxW = Math.Max(size, width - 2 * pad)
            ' lines wider than the block are broken by characters
            Dim lines As New List(Of String)
            For Each l In If(b.Lines.Count > 0, b.Lines, New List(Of String) From {""})
                Dim rest = l.TrimEnd()
                If rest.Length = 0 Then
                    lines.Add("")
                    Continue For
                End If
                While rest.Length > 0
                    Dim n = CInt(cx.Paint.BreakText(rest, maxW))
                    If n <= 0 Then n = 1
                    lines.Add(rest.Substring(0, n))
                    rest = rest.Substring(n)
                End While
            Next
            Dim h = lines.Count * lh + 2 * pad
            ops.Add(New DrawOp With {.Kind = OpKind.RoundRect, .Rect = New SKRect(0, y0, width, y0 + h), .Size = size * 0.35F, .Color = cx.Pal.CodeBack})
            Dim y = y0 + pad
            For Each l In lines
                Dim lx = pad
                For Each seg In cx.Segments(l, mono)
                    ops.Add(New DrawOp With {.Kind = OpKind.Text, .Text = seg.Key, .X = lx, .Y = y + (lh - (-fm.Ascent + fm.Descent)) / 2 - fm.Ascent, .Color = cx.Pal.Text, .Typeface = seg.Value, .Size = cs})
                    lx += cx.Measure(seg.Key, seg.Value, cs)
                Next
                y += lh
            Next
            Return h
        End Function

        Private Function LayoutTable(cx As Ctx, b As MdBlock, width As Single, y0 As Single, ops As List(Of DrawOp)) As Single

            Dim size = cx.Size
            Dim n = b.Header.Count
            Dim padX = size * 0.55F
            Dim padY = size * 0.32F
            Dim header = b.Header.Select(Function(c) ParseInlines(c)).ToList()
            Dim rows = b.Rows.Select(Function(r) r.Select(Function(c) ParseInlines(c)).ToList()).ToList()

            ' natural widths; shrunk in proportion when the table is wider than the block
            Dim nat(n - 1) As Single
            For c = 0 To n - 1
                nat(c) = MeasureInlines(cx, header(c), size, True)
                For Each r In rows
                    nat(c) = Math.Max(nat(c), MeasureInlines(cx, r(c), size, False))
                Next
                nat(c) += 2 * padX
            Next
            Dim total = nat.Sum()
            Dim cw(n - 1) As Single
            For c = 0 To n - 1
                cw(c) = If(total <= width, nat(c), Math.Max(Math.Min(nat(c), size * 3), nat(c) * width / total))
            Next
            Dim xs(n) As Single
            For c = 0 To n - 1
                xs(c + 1) = xs(c) + cw(c)
            Next

            Dim y = y0
            Dim allRows As New List(Of List(Of List(Of MdRun))) From {header}
            allRows.AddRange(rows)
            For ri = 0 To allRows.Count - 1
                Dim row = allRows(ri)
                Dim isHeader = ri = 0
                Dim scratch As New List(Of DrawOp)
                Dim rh As Single = 0
                For c = 0 To n - 1
                    rh = Math.Max(rh, LayoutInlines(cx, row(c), 0, cw(c) - 2 * padX, 0, size, cx.Pal.Text, scratch, 0, isHeader))
                Next
                rh += 2 * padY
                If isHeader Then
                    ops.Add(New DrawOp With {.Kind = OpKind.FillRect, .Rect = New SKRect(0, y, xs(n), y + rh), .Color = cx.Pal.HeaderBack})
                ElseIf ri Mod 2 = 0 Then
                    ops.Add(New DrawOp With {.Kind = OpKind.FillRect, .Rect = New SKRect(0, y, xs(n), y + rh), .Color = cx.Pal.HeaderBack.WithAlpha(150)})
                End If
                For c = 0 To n - 1
                    LayoutInlines(cx, row(c), xs(c) + padX, cw(c) - 2 * padX, y + padY, size, cx.Pal.Text, ops, If(c < b.Aligns.Count, b.Aligns(c), 0), isHeader)
                Next
                ops.Add(New DrawOp With {.Kind = OpKind.Line, .X = 0, .Y = y, .X2 = xs(n), .Y2 = y, .Color = cx.Pal.Border})
                y += rh
            Next
            ops.Add(New DrawOp With {.Kind = OpKind.Line, .X = 0, .Y = y, .X2 = xs(n), .Y2 = y, .Color = cx.Pal.Border})
            For c = 0 To n
                ops.Add(New DrawOp With {.Kind = OpKind.Line, .X = xs(c), .Y = y0, .X2 = xs(c), .Y2 = y, .Color = cx.Pal.Border})
            Next
            Return y - y0

        End Function

#End Region

#Region "HTML"

        Private Class HtmlList
            Public Ordered As Boolean
            Public Count As Integer
        End Class

        ''' <summary>
        ''' Converts the HTML of a formatted text block to Markdown: paragraphs, headings, bold, italic,
        ''' strikethrough, code, links, lists, tables, rules and line breaks. Fonts, sizes and colors of
        ''' the HTML are dropped.
        ''' </summary>
        Public Function FromHtml(html As String) As String

            If String.IsNullOrWhiteSpace(html) Then Return ""

            Dim s = Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline)
            s = Regex.Replace(s, "<(head|style|script|title)\b.*?</\1\s*>", "", RegexOptions.Singleline Or RegexOptions.IgnoreCase)

            Dim sb As New StringBuilder
            Dim lists As New List(Of HtmlList)
            Dim hrefs As New Stack(Of String)
            Dim inPre = False
            Dim table As List(Of List(Of String)) = Nothing
            Dim row As List(Of String) = Nothing
            Dim cell As StringBuilder = Nothing
            Dim quote = False

            Dim outp As Func(Of StringBuilder) = Function() If(cell, sb)
            Dim newBlock As Action = Sub()
                                         If lists.Count > 0 Then
                                             Return
                                         End If
                                         TrimEndSpaces(sb)
                                         If sb.Length = 0 Then Return
                                         While Not EndsWith(sb, vbLf & vbLf)
                                             sb.Append(ChrW(10))
                                         End While
                                         If quote Then sb.Append("> ")
                                     End Sub

            For Each m As Match In Regex.Matches(s, "<(/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>|([^<]+)|(<)")

                If m.Groups(4).Success OrElse m.Groups(5).Success Then
                    Dim t = WebUtility.HtmlDecode(If(m.Groups(4).Success, m.Groups(4).Value, "<"))
                    If inPre Then
                        outp().Append(t)
                    Else
                        t = Regex.Replace(t, "\s+", " ")
                        t = Regex.Replace(t, "([\\`*_\[\]|~])", "\$1")
                        Dim o = outp()
                        If o.Length = 0 OrElse o(o.Length - 1) = ChrW(10) OrElse o(o.Length - 1) = " "c Then t = t.TrimStart()
                        o.Append(t)
                    End If
                    Continue For
                End If

                Dim closing = m.Groups(1).Value = "/"
                Dim tag = m.Groups(2).Value.ToLowerInvariant()
                Dim attrs = m.Groups(3).Value

                Select Case tag
                    Case "b", "strong"
                        outp().Append("**")
                    Case "i", "em"
                        outp().Append("*")
                    Case "s", "strike", "del"
                        outp().Append("~~")
                    Case "code", "tt"
                        If Not inPre Then outp().Append("`")
                    Case "br"
                        If cell IsNot Nothing Then
                            cell.Append(" "c)
                        Else
                            TrimEndSpaces(sb)
                            sb.Append("\" & vbLf)
                            If quote Then sb.Append("> ")
                        End If
                    Case "p", "div"
                        If cell Is Nothing Then newBlock()
                    Case "h1", "h2", "h3", "h4", "h5", "h6"
                        If cell Is Nothing Then
                            newBlock()
                            If Not closing Then sb.Append(New String("#"c, Integer.Parse(tag.Substring(1))) & " ")
                        End If
                    Case "blockquote"
                        quote = Not closing
                        newBlock()
                    Case "ul", "ol"
                        If closing Then
                            If lists.Count > 0 Then lists.RemoveAt(lists.Count - 1)
                            newBlock()
                        Else
                            If lists.Count = 0 Then newBlock()
                            lists.Add(New HtmlList With {.Ordered = tag = "ol"})
                        End If
                    Case "li"
                        If Not closing Then
                            TrimEndSpaces(sb)
                            If sb.Length > 0 AndAlso sb(sb.Length - 1) <> ChrW(10) Then sb.Append(ChrW(10))
                            Dim level = Math.Max(0, lists.Count - 1)
                            sb.Append(New String(" "c, 3 * level))
                            If lists.Count > 0 AndAlso lists(lists.Count - 1).Ordered Then
                                lists(lists.Count - 1).Count += 1
                                sb.Append(lists(lists.Count - 1).Count.ToString() & ". ")
                            Else
                                sb.Append("- ")
                            End If
                        End If
                    Case "hr"
                        newBlock()
                        sb.Append("---")
                        sb.Append(vbLf & vbLf)
                    Case "a"
                        If closing Then
                            Dim href = If(hrefs.Count > 0, hrefs.Pop(), "")
                            If href.Length > 0 Then outp().Append("](" & href & ")")
                        Else
                            Dim hm = Regex.Match(attrs, "href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)
                            Dim href = If(hm.Success, hm.Groups(1).Value, "")
                            hrefs.Push(href)
                            If href.Length > 0 Then outp().Append("[")
                        End If
                    Case "pre"
                        If closing Then
                            If Not EndsWith(sb, vbLf) Then sb.Append(ChrW(10))
                            sb.Append("```" & vbLf & vbLf)
                            inPre = False
                        Else
                            newBlock()
                            sb.Append("```" & vbLf)
                            inPre = True
                        End If
                    Case "table"
                        If closing Then
                            If table IsNot Nothing AndAlso table.Count > 0 Then
                                Dim nc = table.Max(Function(r) r.Count)
                                For ri = 0 To table.Count - 1
                                    Dim r = table(ri)
                                    While r.Count < nc
                                        r.Add("")
                                    End While
                                    sb.Append("| " & String.Join(" | ", r) & " |" & vbLf)
                                    If ri = 0 Then sb.Append("|" & String.Concat(Enumerable.Repeat(" --- |", nc)) & vbLf)
                                Next
                                sb.Append(ChrW(10))
                            End If
                            table = Nothing
                        Else
                            newBlock()
                            table = New List(Of List(Of String))
                        End If
                    Case "tr"
                        If closing Then
                            If table IsNot Nothing AndAlso row IsNot Nothing Then table.Add(row)
                            row = Nothing
                        Else
                            row = New List(Of String)
                        End If
                    Case "td", "th"
                        If closing Then
                            If row IsNot Nothing AndAlso cell IsNot Nothing Then row.Add(cell.ToString().Trim())
                            cell = Nothing
                        Else
                            If row Is Nothing Then row = New List(Of String)
                            cell = New StringBuilder
                        End If
                End Select

            Next

            Dim result = sb.ToString().Replace(vbCrLf, vbLf)
            result = Regex.Replace(result, "[ \t]+\n", vbLf)
            result = Regex.Replace(result, "\n{3,}", vbLf & vbLf)
            Return result.Trim()

        End Function

        Private Sub TrimEndSpaces(sb As StringBuilder)
            While sb.Length > 0 AndAlso sb(sb.Length - 1) = " "c
                sb.Length -= 1
            End While
        End Sub

        Private Function EndsWith(sb As StringBuilder, s As String) As Boolean
            If sb.Length < s.Length Then Return False
            For k = 0 To s.Length - 1
                If sb(sb.Length - s.Length + k) <> s(k) Then Return False
            Next
            Return True
        End Function

#End Region

    End Module

End Namespace
