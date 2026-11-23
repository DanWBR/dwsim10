Imports SkiaSharp

Namespace GraphicObjects

    ''' <summary>
    ''' The artwork of the controllers, logical blocks, gauges, inputs and switches: a flat icon for the
    ''' default drawing mode, a line drawing for the black and white mode and a photo for the color icons
    ''' mode (Icons/logic_&lt;name&gt;_{icon,bw,photo}.png). The gauges, displays and inputs keep drawing
    ''' their values over the art, in the region of the art that holds them (the dial, the screen, the
    ''' glass), given here relative to the drawn box.
    ''' </summary>
    Public Module LogicArt

        Private ReadOnly Images As New Dictionary(Of String, SKImage)
        Private ReadOnly ImagesLock As New Object

        ' value regions (left, top, right, bottom as fractions of the box), measured on the artwork
        Private ReadOnly Regions As New Dictionary(Of String, SKRect) From {
            {"analog_gauge_icon", New SKRect(0.207F, 0.1172F, 0.793F, 0.7344F)},
            {"analog_gauge_photo", New SKRect(0.1914F, 0.0703F, 0.8242F, 0.7266F)},
            {"digital_gauge_icon", New SKRect(0.1855F, 0.1484F, 0.8145F, 0.8516F)},
            {"digital_gauge_photo", New SKRect(0.2109F, 0.168F, 0.791F, 0.9023F)},
            {"input_icon", New SKRect(0.1094F, 0.1562F, 0.7344F, 0.8438F)},
            {"input_photo", New SKRect(0.2617F, 0.1797F, 0.6953F, 0.8281F)},
            {"level_gauge_icon", New SKRect(0.4314F, 0.1836F, 0.8529F, 0.7578F)},
            {"level_gauge_photo", New SKRect(0.49F, 0.1211F, 0.59F, 0.6719F)}
        }

        ''' <summary>The suffix of a drawing mode: 1 black and white, 2 color icons, the default otherwise.</summary>
        Private Function Suffix(drawMode As Integer) As String
            Select Case drawMode
                Case 1 : Return "bw"
                Case 2 : Return "photo"
                Case Else : Return "icon"
            End Select
        End Function

        ''' <summary>The artwork of <paramref name="name"/> in a drawing mode, or Nothing when there isn't any.</summary>
        Public Function GetImage(name As String, drawMode As Integer) As SKImage
            Dim key = String.Format("logic_{0}_{1}.png", name, Suffix(drawMode))
            SyncLock ImagesLock
                Dim img As SKImage = Nothing
                If Images.TryGetValue(key, img) Then Return img
                Using s = GetType(LogicArt).Assembly.GetManifestResourceStream("DWSIM.Drawing.SkiaSharp." & key)
                    If s IsNot Nothing Then
                        Using bmp = SKBitmap.Decode(s)
                            img = SKImage.FromBitmap(bmp)
                        End Using
                    End If
                End Using
                Images(key) = img
                Return img
            End SyncLock
        End Function

        ''' <summary>Draws the artwork in <paramref name="rect"/>, greyed when the object is inactive. False when there isn't any.</summary>
        Public Function Draw(canvas As SKCanvas, name As String, drawMode As Integer, rect As SKRect, active As Boolean) As Boolean
            Dim img = GetImage(name, drawMode)
            If img Is Nothing Then Return False
            Using p As New SKPaint With {.IsAntialias = True, .FilterQuality = SKFilterQuality.High}
                canvas.DrawImage(img, rect, p)
            End Using
            If Not active Then
                Using p As New SKPaint With {.IsAntialias = True, .FilterQuality = SKFilterQuality.High}
                    p.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.Gray.WithAlpha(150), SKBlendMode.SrcATop)
                    canvas.DrawImage(img, rect, p)
                End Using
            End If
            Return True
        End Function

        ''' <summary>
        ''' The region of the artwork that holds the value (dial, screen, glass) for the box
        ''' <paramref name="rect"/>; the black and white drawing uses the regions of the flat icon.
        ''' </summary>
        Public Function ValueRegion(name As String, drawMode As Integer, rect As SKRect) As SKRect
            Dim r As SKRect
            If Not Regions.TryGetValue(name & "_" & If(drawMode = 2, "photo", "icon"), r) Then Return rect
            Return New SKRect(rect.Left + r.Left * rect.Width, rect.Top + r.Top * rect.Height,
                              rect.Left + r.Right * rect.Width, rect.Top + r.Bottom * rect.Height)
        End Function

        ''' <summary>
        ''' The columns (fractions of the artwork's width) between which a display stretches when its box
        ''' is wider than the artwork: the ends keep their proportions, the middle of the screen stretches.
        ''' </summary>
        Private Function Slices(name As String) As (a As Single, b As Single)
            Select Case name
                Case "input" : Return (0.3F, 0.6F)
                Case Else : Return (0.3F, 0.7F)
            End Select
        End Function

        ''' <summary>Draws the artwork in a box of any width: the ends at the artwork's proportions and the middle stretched.</summary>
        Public Function DrawStretched(canvas As SKCanvas, name As String, drawMode As Integer, rect As SKRect, active As Boolean) As Boolean
            Dim img = GetImage(name, drawMode)
            If img Is Nothing Then Return False
            Dim wn = rect.Height * Aspect(name)
            Dim s = Slices(name)
            Dim mid = rect.Width - (s.a + 1 - s.b) * wn
            If Math.Abs(rect.Width - wn) < 1 OrElse mid <= 0 Then Return Draw(canvas, name, drawMode, rect, active)
            Dim iw = img.Width, ih = img.Height
            Dim src = {New SKRect(0, 0, s.a * iw, ih), New SKRect(s.a * iw, 0, s.b * iw, ih), New SKRect(s.b * iw, 0, iw, ih)}
            Dim x1 = rect.Left + s.a * wn, x2 = x1 + mid
            Dim dst = {New SKRect(rect.Left, rect.Top, x1, rect.Bottom), New SKRect(x1, rect.Top, x2, rect.Bottom), New SKRect(x2, rect.Top, rect.Right, rect.Bottom)}
            Using p As New SKPaint With {.IsAntialias = True, .FilterQuality = SKFilterQuality.High}
                If Not active Then p.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.Gray.WithAlpha(150), SKBlendMode.SrcATop)
                For i = 0 To 2
                    canvas.DrawImage(img, src(i), dst(i), p)
                Next
            End Using
            Return True
        End Function

        ''' <summary>The value region of a display drawn by <see cref="DrawStretched"/> in <paramref name="rect"/>.</summary>
        Public Function StretchedValueRegion(name As String, drawMode As Integer, rect As SKRect) As SKRect
            Dim r As SKRect
            If Not Regions.TryGetValue(name & "_" & If(drawMode = 2, "photo", "icon"), r) Then Return rect
            Dim wn = rect.Height * Aspect(name)
            Return New SKRect(rect.Left + r.Left * wn, rect.Top + r.Top * rect.Height,
                              rect.Right - (1 - r.Right) * wn, rect.Top + r.Bottom * rect.Height)
        End Function

        ''' <summary>
        ''' Draws a display (digital gauge, input): the artwork, as wide as the value needs, and the value
        ''' centred on its screen, in LCD green, or black on a white screen in the black and white mode.
        ''' Sets the width of <paramref name="go"/>. False when there isn't any artwork.
        ''' </summary>
        Public Function DrawDisplay(canvas As SKCanvas, go As GraphicObject, name As String, valtext As String, font As SKTypeface) As Boolean
            Dim mode = go.DrawMode
            Dim r As SKRect
            If GetImage(name, mode) Is Nothing OrElse Not Regions.TryGetValue(name & "_" & If(mode = 2, "photo", "icon"), r) Then Return False
            Dim h As Single = go.Height
            Dim wn = h * Aspect(name)
            Dim screenH = (r.Bottom - r.Top) * h
            Using paint As New SKPaint With {.TextSize = screenH * 0.62F, .IsAntialias = True, .Typeface = font}
                Dim tw = paint.MeasureText(valtext)
                Dim needed = (r.Left + 1 - r.Right) * wn + tw + 0.45F * screenH
                go.Width = CInt(Math.Ceiling(Math.Max(wn, needed)))
                Dim rect As New SKRect(go.X, go.Y, go.X + go.Width, go.Y + h)
                DrawStretched(canvas, name, mode, rect, go.Active)
                Dim screen = StretchedValueRegion(name, mode, rect)
                If mode = 1 Then
                    Using white As New SKPaint With {.Color = SKColors.White, .IsAntialias = True}
                        canvas.DrawRect(screen, white)
                    End Using
                    paint.Color = SKColors.Black
                Else
                    paint.Color = New SKColor(150, 240, 150)
                End If
                Dim bounds As New SKRect
                paint.MeasureText(valtext, bounds)
                canvas.DrawText(valtext, screen.MidX - tw / 2, screen.MidY - bounds.MidY, paint)
            End Using
            Return True
        End Function

        ''' <summary>The aspect (width / height) the artwork of <paramref name="name"/> was cut to.</summary>
        Public Function Aspect(name As String) As Single
            Select Case name
                Case "digital_gauge", "input" : Return 2.0F
                Case "switch", "switch_off" : Return 1.25F
                Case "level_gauge" : Return 0.4F
                Case Else : Return 1.0F
            End Select
        End Function

    End Module

End Namespace
