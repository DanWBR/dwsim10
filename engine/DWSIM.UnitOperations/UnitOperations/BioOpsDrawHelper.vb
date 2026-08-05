'    BioOpsDrawHelper - shared Draw-mode-2 (photorealistic image) fallback support
'    for the biorefinery unit-operation family (BioReactor, AnaerobicDigester, Pretreatment,
'    Centrifuge, CrossflowUF, BiogasUpgrader, Chromatography, CellLysis, Crystallizer).
'
'    Mirrors the DWSIMPlus ZeoliteAdsorber pattern:
'      - DrawMode = 0 → icon (caller's custom Skia drawing)
'      - DrawMode = 1 → silhouette (caller's custom Skia drawing, mode 0 by default)
'      - DrawMode = 2 → photorealistic image, by convention My.Resources.<resourceName>;
'                        falls back to DrawMode 0 if image is missing.

Imports SkiaSharp
Imports SkiaSharp.Views.Desktop
Imports System.IO
Imports System.Reflection

Namespace UnitOperations

    ''' <summary>Delegate for an icon-drawing routine that paints into a caller-supplied canvas at (gx, gy) with size (w, h).</summary>
    Public Delegate Sub IconDrawAction(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single)

    Public Module BioOpsDrawHelper

        ''' <summary>Renders an icon via the given draw action and returns its PNG-encoded bytes (w×h).</summary>
        Public Function RenderIconToPngBytes(w As Integer, h As Integer, drawAction As IconDrawAction) As Byte()
            Using skBmp As New SKBitmap(New SKImageInfo(w, h))
                Using canvas As New SKCanvas(skBmp)
                    canvas.Clear(SKColors.Transparent)
                    drawAction(canvas, 0.0F, 0.0F, CSng(w), CSng(h))
                End Using
                Using img = SKImage.FromBitmap(skBmp)
                    Using data = img.Encode(SKEncodedImageFormat.Png, 100)
                        Return data.ToArray()
                    End Using
                End Using
            End Using
        End Function


        ''' <summary>
        ''' Attempts to render a photorealistic image for DrawMode 2 using a named PNG resource
        ''' from the DWSIM.UnitOperations assembly's embedded "My.Resources.<resourceName>".
        ''' Returns True if the image was drawn; False if the resource was not found (caller
        ''' should then fall back to its icon/silhouette drawing).
        ''' </summary>
        Public Function TryDrawPhotorealistic(canvas As SKCanvas, x As Double, y As Double,
                                              w As Double, h As Double,
                                              resourceName As String,
                                              ByRef cachedImage As SKImage) As Boolean

            If cachedImage Is Nothing Then
                Try
                    ' the picture is embedded next to the icons; SkiaSharp decodes it directly,
                    ' where the resx entry would hand back a GDI+ bitmap
                    Dim asm = GetType(BioOpsDrawHelper).Assembly
                    Using stream = asm.GetManifestResourceStream("DWSIM.UnitOperations." & resourceName & ".png")
                        If stream IsNot Nothing Then
                            Using skBmp = SKBitmap.Decode(stream)
                                If skBmp IsNot Nothing Then cachedImage = SKImage.FromBitmap(skBmp)
                            End Using
                        End If
                    End Using
                Catch
                    ' resource lookup failed - caller falls back
                End Try
            End If

            If cachedImage Is Nothing Then Return False

            Using p As New SKPaint With {.IsAntialias = GlobalSettings.Settings.DrawingAntiAlias,
                                         .FilterQuality = SKFilterQuality.High}
                canvas.DrawImage(cachedImage,
                                 New SKRect(CSng(x), CSng(y), CSng(x + w), CSng(y + h)),
                                 p)
            End Using
            Return True

        End Function

#Region "Professional industrial-style drawing helpers (shared by bio ops)"

        ' ---------- color palette ----------
        Public Function ClrStroke(mono As Boolean) As SKColor
            Return If(mono, New SKColor(30, 30, 30), New SKColor(50, 65, 85))
        End Function

        Public Function ClrStrokeLight(mono As Boolean) As SKColor
            Return If(mono, New SKColor(100, 100, 100), New SKColor(110, 125, 150))
        End Function

        Public Function ClrMetalLight(mono As Boolean) As SKColor
            Return If(mono, New SKColor(245, 245, 245), New SKColor(240, 244, 250))
        End Function

        Public Function ClrMetalMid(mono As Boolean) As SKColor
            Return If(mono, New SKColor(190, 190, 190), New SKColor(175, 190, 210))
        End Function

        Public Function ClrMetalDark(mono As Boolean) As SKColor
            Return If(mono, New SKColor(140, 140, 140), New SKColor(120, 140, 165))
        End Function

        Public Function ClrMotor(mono As Boolean) As SKColor
            Return If(mono, New SKColor(60, 60, 60), New SKColor(55, 65, 80))
        End Function

        Public Function ClrAccent(mono As Boolean) As SKColor
            Return If(mono, New SKColor(90, 90, 90), New SKColor(85, 125, 95))
        End Function

        Public Function ClrSkid(mono As Boolean) As SKColor
            Return If(mono, New SKColor(95, 95, 95), New SKColor(90, 100, 115))
        End Function

        ''' <summary>Creates a vertical-cylinder metallic gradient (light→dark→light left-to-right) suggesting a cylinder surface.</summary>
        Public Function VerticalCylinderShader(rect As SKRect, mono As Boolean) As SKShader
            Dim colors = New SKColor() {ClrMetalLight(mono), ClrMetalMid(mono), ClrMetalDark(mono), ClrMetalMid(mono), ClrMetalLight(mono)}
            Dim positions = New Single() {0.0F, 0.25F, 0.55F, 0.8F, 1.0F}
            Return SKShader.CreateLinearGradient(New SKPoint(rect.Left, rect.Top), New SKPoint(rect.Right, rect.Top), colors, positions, SKShaderTileMode.Clamp)
        End Function

        ''' <summary>Horizontal-cylinder metallic gradient (light→dark→light top-to-bottom).</summary>
        Public Function HorizontalCylinderShader(rect As SKRect, mono As Boolean) As SKShader
            Dim colors = New SKColor() {ClrMetalLight(mono), ClrMetalMid(mono), ClrMetalDark(mono), ClrMetalMid(mono), ClrMetalLight(mono)}
            Dim positions = New Single() {0.0F, 0.25F, 0.55F, 0.8F, 1.0F}
            Return SKShader.CreateLinearGradient(New SKPoint(rect.Left, rect.Top), New SKPoint(rect.Left, rect.Bottom), colors, positions, SKShaderTileMode.Clamp)
        End Function

        ''' <summary>Draws a vertical cylindrical tank body (rectangle + elliptical top/bottom endcaps) with metallic shading.</summary>
        Public Sub DrawVerticalTank(canvas As SKCanvas, rect As SKRect, mono As Boolean, Optional strokeW As Single = 1.5F)
            Dim capH = Math.Min(rect.Width * 0.25F, rect.Height * 0.12F)
            Dim body As New SKRect(rect.Left, rect.Top + capH * 0.5F, rect.Right, rect.Bottom - capH * 0.5F)
            Dim topCap As New SKRect(rect.Left, rect.Top, rect.Right, rect.Top + capH)
            Dim botCap As New SKRect(rect.Left, rect.Bottom - capH, rect.Right, rect.Bottom)
            Using fill As New SKPaint With {.Shader = VerticalCylinderShader(body, mono), .IsAntialias = True}
                canvas.DrawRect(body, fill)
            End Using
            Using fill As New SKPaint With {.Color = ClrMetalMid(mono), .IsAntialias = True}
                canvas.DrawOval(botCap, fill)
            End Using
            Using fill As New SKPaint With {.Color = ClrMetalLight(mono), .IsAntialias = True}
                canvas.DrawOval(topCap, fill)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = strokeW, .IsAntialias = True}
                canvas.DrawLine(rect.Left, rect.Top + capH * 0.5F, rect.Left, rect.Bottom - capH * 0.5F, stroke)
                canvas.DrawLine(rect.Right, rect.Top + capH * 0.5F, rect.Right, rect.Bottom - capH * 0.5F, stroke)
                canvas.DrawOval(topCap, stroke)
                canvas.DrawArc(botCap, 0, 180, False, stroke)
            End Using
        End Sub

        ''' <summary>Draws a horizontal cylindrical tank body with left/right ellipse endcaps.</summary>
        Public Sub DrawHorizontalTank(canvas As SKCanvas, rect As SKRect, mono As Boolean, Optional strokeW As Single = 1.5F)
            Dim capW = Math.Min(rect.Height * 0.25F, rect.Width * 0.12F)
            Dim body As New SKRect(rect.Left + capW * 0.5F, rect.Top, rect.Right - capW * 0.5F, rect.Bottom)
            Dim leftCap As New SKRect(rect.Left, rect.Top, rect.Left + capW, rect.Bottom)
            Dim rightCap As New SKRect(rect.Right - capW, rect.Top, rect.Right, rect.Bottom)
            Using fill As New SKPaint With {.Shader = HorizontalCylinderShader(body, mono), .IsAntialias = True}
                canvas.DrawRect(body, fill)
            End Using
            Using fill As New SKPaint With {.Color = ClrMetalMid(mono), .IsAntialias = True}
                canvas.DrawOval(leftCap, fill)
            End Using
            Using fill As New SKPaint With {.Color = ClrMetalLight(mono), .IsAntialias = True}
                canvas.DrawOval(rightCap, fill)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = strokeW, .IsAntialias = True}
                canvas.DrawLine(rect.Left + capW * 0.5F, rect.Top, rect.Right - capW * 0.5F, rect.Top, stroke)
                canvas.DrawLine(rect.Left + capW * 0.5F, rect.Bottom, rect.Right - capW * 0.5F, rect.Bottom, stroke)
                canvas.DrawOval(rightCap, stroke)
                canvas.DrawArc(leftCap, 90, 180, False, stroke)
            End Using
        End Sub

        ''' <summary>Draws a domed-top vertical tank (ellipse dome, rectangular body, flat floor).</summary>
        Public Sub DrawDomedTank(canvas As SKCanvas, rect As SKRect, mono As Boolean, Optional strokeW As Single = 1.5F)
            Dim domeH = rect.Height * 0.3F
            Dim body As New SKRect(rect.Left, rect.Top + domeH * 0.5F, rect.Right, rect.Bottom)
            Dim dome As New SKRect(rect.Left, rect.Top, rect.Right, rect.Top + domeH)
            Using fill As New SKPaint With {.Shader = VerticalCylinderShader(body, mono), .IsAntialias = True}
                canvas.DrawRect(New SKRect(body.Left, body.Top + domeH * 0.5F, body.Right, body.Bottom), fill)
            End Using
            Using fill As New SKPaint With {.Color = ClrMetalLight(mono), .IsAntialias = True}
                canvas.DrawOval(dome, fill)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = strokeW, .IsAntialias = True}
                canvas.DrawArc(dome, 180, 180, False, stroke)
                canvas.DrawLine(rect.Left, rect.Top + domeH * 0.5F, rect.Left, rect.Bottom, stroke)
                canvas.DrawLine(rect.Right, rect.Top + domeH * 0.5F, rect.Right, rect.Bottom, stroke)
                canvas.DrawLine(rect.Left, rect.Bottom, rect.Right, rect.Bottom, stroke)
            End Using
        End Sub

        ''' <summary>Draws a vertical tank with conical bottom (top dome optional).</summary>
        Public Sub DrawConeBottomTank(canvas As SKCanvas, rect As SKRect, mono As Boolean, Optional strokeW As Single = 1.5F)
            Dim coneH = rect.Height * 0.22F
            Dim capH = rect.Width * 0.18F
            Dim body As New SKRect(rect.Left, rect.Top + capH * 0.5F, rect.Right, rect.Bottom - coneH)
            Using fill As New SKPaint With {.Shader = VerticalCylinderShader(body, mono), .IsAntialias = True}
                canvas.DrawRect(body, fill)
            End Using
            Dim topCap As New SKRect(rect.Left, rect.Top, rect.Right, rect.Top + capH)
            Using fill As New SKPaint With {.Color = ClrMetalLight(mono), .IsAntialias = True}
                canvas.DrawOval(topCap, fill)
            End Using
            ' cone
            Dim cx = (rect.Left + rect.Right) * 0.5F
            Dim cone As New SKPath()
            cone.MoveTo(rect.Left, rect.Bottom - coneH)
            cone.LineTo(cx, rect.Bottom)
            cone.LineTo(rect.Right, rect.Bottom - coneH)
            cone.Close()
            Using fill As New SKPaint With {.Color = ClrMetalMid(mono), .IsAntialias = True}
                canvas.DrawPath(cone, fill)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = strokeW, .IsAntialias = True}
                canvas.DrawLine(rect.Left, body.Top, rect.Left, body.Bottom, stroke)
                canvas.DrawLine(rect.Right, body.Top, rect.Right, body.Bottom, stroke)
                canvas.DrawOval(topCap, stroke)
                canvas.DrawPath(cone, stroke)
            End Using
        End Sub

        ''' <summary>Draws a flange (two concentric ellipses) with bolt detail at (cx, cy) with given diameter.</summary>
        Public Sub DrawFlange(canvas As SKCanvas, cx As Single, cy As Single, diameter As Single, mono As Boolean)
            Dim r = diameter * 0.5F
            Dim outer As New SKRect(cx - r, cy - r * 0.35F, cx + r, cy + r * 0.35F)
            Dim inner As New SKRect(cx - r * 0.55F, cy - r * 0.2F, cx + r * 0.55F, cy + r * 0.2F)
            Using fill As New SKPaint With {.Color = ClrMetalMid(mono), .IsAntialias = True}
                canvas.DrawOval(outer, fill)
            End Using
            Using fill As New SKPaint With {.Color = ClrMetalDark(mono), .IsAntialias = True}
                canvas.DrawOval(inner, fill)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawOval(outer, stroke)
                canvas.DrawOval(inner, stroke)
            End Using
            ' bolt studs around the outer flange ring (only if flange is large enough to render)
            If diameter > 8.0F Then
                Using bolt As New SKPaint With {.Color = ClrStroke(mono), .IsAntialias = True}
                    Dim bR = Math.Max(0.7F, r * 0.08F)
                    Dim ringRx = r * 0.78F
                    Dim ringRy = r * 0.26F
                    Dim nBolts = 6
                    For i = 0 To nBolts - 1
                        Dim ang = (i + 0.5F) * 2.0F * Math.PI / nBolts
                        Dim bx = cx + ringRx * CSng(Math.Cos(ang))
                        Dim by = cy + ringRy * CSng(Math.Sin(ang))
                        canvas.DrawCircle(bx, by, bR, bolt)
                    Next
                End Using
            End If
        End Sub

        ''' <summary>Draws a dark motor block with a fan-cap end and ventilation fins. Orientation auto-detected (wider = horizontal).</summary>
        Public Sub DrawMotor(canvas As SKCanvas, rect As SKRect, mono As Boolean)
            Dim horizontal = rect.Width >= rect.Height
            ' main body
            Using fill As New SKPaint With {.Color = ClrMotor(mono), .IsAntialias = True}
                canvas.DrawRoundRect(rect, 2.0F, 2.0F, fill)
            End Using
            ' ventilation fins on the body
            Using stroke As New SKPaint With {.Color = ClrStrokeLight(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.8F, .IsAntialias = True}
                Dim nV = 5
                If horizontal Then
                    For i = 1 To nV - 1
                        Dim xx = rect.Left + i * rect.Width / nV
                        canvas.DrawLine(xx, rect.Top + 1.5F, xx, rect.Bottom - 1.5F, stroke)
                    Next
                Else
                    For i = 1 To nV - 1
                        Dim yy = rect.Top + i * rect.Height / nV
                        canvas.DrawLine(rect.Left + 1.5F, yy, rect.Right - 1.5F, yy, stroke)
                    Next
                End If
            End Using
            ' fan cap - small dark cap at the rear end (left for horizontal, top for vertical)
            Dim capSize = 0.22F * Math.Min(rect.Width, rect.Height)
            Dim cap As SKRect
            If horizontal Then
                cap = New SKRect(rect.Left - capSize * 0.4F, rect.Top + rect.Height * 0.15F,
                                 rect.Left + capSize * 0.2F, rect.Bottom - rect.Height * 0.15F)
            Else
                cap = New SKRect(rect.Left + rect.Width * 0.15F, rect.Top - capSize * 0.4F,
                                 rect.Right - rect.Width * 0.15F, rect.Top + capSize * 0.2F)
            End If
            Using fill As New SKPaint With {.Color = If(mono, New SKColor(45, 45, 45), New SKColor(40, 48, 60)), .IsAntialias = True}
                canvas.DrawRoundRect(cap, 1.0F, 1.0F, fill)
            End Using
            ' outline
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.3F, .IsAntialias = True}
                canvas.DrawRoundRect(rect, 2.0F, 2.0F, stroke)
                canvas.DrawRoundRect(cap, 1.0F, 1.0F, stroke)
            End Using
            ' terminal box (small square on top/side)
            Dim tbSize = 0.18F * Math.Min(rect.Width, rect.Height)
            Dim tbox As SKRect
            If horizontal Then
                Dim tbCx = rect.Left + rect.Width * 0.55F
                tbox = New SKRect(tbCx - tbSize * 0.5F, rect.Top - tbSize * 0.5F, tbCx + tbSize * 0.5F, rect.Top + tbSize * 0.3F)
            Else
                Dim tbCy = rect.Top + rect.Height * 0.55F
                tbox = New SKRect(rect.Right - tbSize * 0.3F, tbCy - tbSize * 0.5F, rect.Right + tbSize * 0.5F, tbCy + tbSize * 0.5F)
            End If
            Using fill As New SKPaint With {.Color = ClrMotor(mono), .IsAntialias = True}
                canvas.DrawRect(tbox, fill)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawRect(tbox, stroke)
            End Using
        End Sub

        ''' <summary>Draws a skid / base frame (dark grey I-beam rectangle with legs and a top highlight).</summary>
        Public Sub DrawSkid(canvas As SKCanvas, rect As SKRect, mono As Boolean)
            Dim frameH = rect.Height * 0.35F
            Dim frame As New SKRect(rect.Left, rect.Top, rect.Right, rect.Top + frameH)
            ' shaded body
            Dim skidC = ClrSkid(mono)
            Dim skidDark = If(mono, New SKColor(65, 65, 65), New SKColor(60, 68, 80))
            Using fill As New SKPaint With {.IsAntialias = True,
                                            .Shader = SKShader.CreateLinearGradient(
                                                New SKPoint(frame.Left, frame.Top),
                                                New SKPoint(frame.Left, frame.Bottom),
                                                New SKColor() {skidC, skidDark},
                                                New Single() {0.0F, 1.0F},
                                                SKShaderTileMode.Clamp)}
                canvas.DrawRect(frame, fill)
            End Using
            ' top highlight stripe
            Using hl As New SKPaint With {.Color = If(mono, New SKColor(160, 160, 160), New SKColor(140, 155, 180)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(frame.Left, frame.Top, frame.Right, frame.Top + Math.Max(1.0F, frameH * 0.18F)), hl)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.2F, .IsAntialias = True}
                canvas.DrawRect(frame, stroke)
            End Using
            ' legs
            Dim legW = Math.Max(2.0F, rect.Width * 0.06F)
            Dim legPositions = New Single() {rect.Left + rect.Width * 0.1F, rect.Right - rect.Width * 0.1F - legW}
            Using fill As New SKPaint With {.Color = skidDark, .IsAntialias = True}
                For Each lx In legPositions
                    Dim leg As New SKRect(lx, rect.Top + frameH, lx + legW, rect.Bottom)
                    canvas.DrawRect(leg, fill)
                Next
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.9F, .IsAntialias = True}
                For Each lx In legPositions
                    Dim leg As New SKRect(lx, rect.Top + frameH, lx + legW, rect.Bottom)
                    canvas.DrawRect(leg, stroke)
                Next
            End Using
        End Sub

        ''' <summary>Draws a pipe segment as a rectangle with a slight shading.</summary>
        Public Sub DrawPipe(canvas As SKCanvas, p1 As SKPoint, p2 As SKPoint, thickness As Single, mono As Boolean)
            Dim rect As SKRect
            If Math.Abs(p2.X - p1.X) >= Math.Abs(p2.Y - p1.Y) Then
                rect = New SKRect(Math.Min(p1.X, p2.X), (p1.Y + p2.Y) * 0.5F - thickness * 0.5F, Math.Max(p1.X, p2.X), (p1.Y + p2.Y) * 0.5F + thickness * 0.5F)
                Using fill As New SKPaint With {.Shader = HorizontalCylinderShader(rect, mono), .IsAntialias = True}
                    canvas.DrawRect(rect, fill)
                End Using
            Else
                rect = New SKRect((p1.X + p2.X) * 0.5F - thickness * 0.5F, Math.Min(p1.Y, p2.Y), (p1.X + p2.X) * 0.5F + thickness * 0.5F, Math.Max(p1.Y, p2.Y))
                Using fill As New SKPaint With {.Shader = VerticalCylinderShader(rect, mono), .IsAntialias = True}
                    canvas.DrawRect(rect, fill)
                End Using
            End If
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawRect(rect, stroke)
            End Using
        End Sub

        ''' <summary>Draws an agitator shaft with two Rushton-style turbine discs inside a vertical tank.</summary>
        Public Sub DrawAgitator(canvas As SKCanvas, cx As Single, yTop As Single, yBot As Single, impellerW As Single, mono As Boolean)
            ' shaft
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 2.0F, .IsAntialias = True}
                canvas.DrawLine(cx, yTop, cx, yBot, stroke)
            End Using
            Dim n = 2
            Dim discH = impellerW * 0.14F
            For i = 1 To n
                Dim yy = yTop + (yBot - yTop) * i / (n + 1)
                Dim disc As New SKRect(cx - impellerW * 0.5F, yy - discH * 0.5F, cx + impellerW * 0.5F, yy + discH * 0.5F)
                ' disc body (dark metallic with highlight)
                Using fill As New SKPaint With {.Color = ClrMetalDark(mono), .IsAntialias = True}
                    canvas.DrawRoundRect(disc, 1.5F, 1.5F, fill)
                End Using
                ' vertical blade marks on the disc
                Using bladeStroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.9F, .IsAntialias = True}
                    Dim nBlades = 4
                    For b = 1 To nBlades - 1
                        Dim bx = disc.Left + b * disc.Width / nBlades
                        canvas.DrawLine(bx, disc.Top + 1, bx, disc.Bottom - 1, bladeStroke)
                    Next
                End Using
                ' outline
                Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.2F, .IsAntialias = True}
                    canvas.DrawRoundRect(disc, 1.5F, 1.5F, stroke)
                End Using
            Next
        End Sub

        ''' <summary>Draws a small pressure gauge (circle with needle).</summary>
        Public Sub DrawGauge(canvas As SKCanvas, cx As Single, cy As Single, r As Single, mono As Boolean)
            Using fill As New SKPaint With {.Color = ClrMetalLight(mono), .IsAntialias = True}
                canvas.DrawCircle(cx, cy, r, fill)
            End Using
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawCircle(cx, cy, r, stroke)
                canvas.DrawLine(cx, cy, cx + r * 0.6F, cy - r * 0.5F, stroke)
            End Using
        End Sub

        ''' <summary>Draws a ladder on the side of a vessel (two rails with rungs).</summary>
        Public Sub DrawLadder(canvas As SKCanvas, xLeft As Single, xRight As Single, yTop As Single, yBot As Single, mono As Boolean)
            Using stroke As New SKPaint With {.Color = ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawLine(xLeft, yTop, xLeft, yBot, stroke)
                canvas.DrawLine(xRight, yTop, xRight, yBot, stroke)
                Dim rungs = CInt(Math.Max(3, (yBot - yTop) / 6))
                For i = 1 To rungs - 1
                    Dim yy = yTop + (yBot - yTop) * i / rungs
                    canvas.DrawLine(xLeft, yy, xRight, yy, stroke)
                Next
            End Using
        End Sub

#End Region

    End Module

End Namespace
