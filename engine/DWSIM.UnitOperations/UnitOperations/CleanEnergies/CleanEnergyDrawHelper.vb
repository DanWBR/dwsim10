'    CleanEnergyDrawHelper - shared schematic (DrawMode 0/1) drawing for
'    clean-energy unit operations (SolarPanel, WindTurbine, HydroelectricTurbine,
'    WaterElectrolyzer, PEMFC_*). Photorealistic DrawMode 2 reuses
'    BioOpsDrawHelper.TryDrawPhotorealistic, with new PNGs embedded as
'    My.Resources.solarpanel_photo / windturbine_photo / hydroturbine_photo /
'    electrolyzer_photo / fuelcell_photo.

Imports SkiaSharp
Imports System.Math

Namespace UnitOperations

    Public Module CleanEnergyDrawHelper

        Private Function Stroke(mono As Boolean, Optional wide As Boolean = False) As SKPaint
            Return New SKPaint With {
                .Color = If(mono, New SKColor(30, 30, 30), New SKColor(40, 55, 80)),
                .Style = SKPaintStyle.Stroke,
                .StrokeWidth = If(wide, 1.8F, 1.1F),
                .IsAntialias = True
            }
        End Function

        Private Function Fill(c As SKColor) As SKPaint
            Return New SKPaint With {.Color = c, .Style = SKPaintStyle.Fill, .IsAntialias = True}
        End Function

        Private Function LinGrad(x0 As Single, y0 As Single, x1 As Single, y1 As Single, c0 As SKColor, c1 As SKColor) As SKShader
            Return SKShader.CreateLinearGradient(New SKPoint(x0, y0), New SKPoint(x1, y1),
                                                 New SKColor() {c0, c1}, Nothing, SKShaderTileMode.Clamp)
        End Function

        ' --------- SOLAR PANEL ---------
        Public Sub DrawSolarPanel(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, mono As Boolean)
            ' Tilted rectangular PV panel on a steel mast
            Dim cx = gx + 0.5F * w, cy = gy + 0.5F * h
            ' ground line
            Using gp As New SKPaint With {.Color = If(mono, New SKColor(120, 120, 120), New SKColor(170, 170, 170)), .StrokeWidth = 1.4F, .Style = SKPaintStyle.Stroke, .IsAntialias = True}
                canvas.DrawLine(gx + 0.05F * w, gy + 0.95F * h, gx + 0.95F * w, gy + 0.95F * h, gp)
            End Using
            ' mast
            Using m As New SKPaint With {.Color = If(mono, New SKColor(80, 80, 80), New SKColor(90, 100, 115)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(cx - 0.03F * w, gy + 0.55F * h, cx + 0.03F * w, gy + 0.95F * h), m)
            End Using
            Using s = Stroke(mono) : canvas.DrawRect(New SKRect(cx - 0.03F * w, gy + 0.55F * h, cx + 0.03F * w, gy + 0.95F * h), s) : End Using
            ' Panel quad (tilted): corners
            Dim p1 As New SKPoint(gx + 0.12F * w, gy + 0.55F * h) ' top-left (high)
            Dim p2 As New SKPoint(gx + 0.88F * w, gy + 0.30F * h) ' top-right (highest)
            Dim p3 As New SKPoint(gx + 0.92F * w, gy + 0.55F * h) ' bottom-right
            Dim p4 As New SKPoint(gx + 0.16F * w, gy + 0.80F * h) ' bottom-left (lowest)
            Dim path As New SKPath()
            path.MoveTo(p1) : path.LineTo(p2) : path.LineTo(p3) : path.LineTo(p4) : path.Close()
            ' glass fill
            Using gFill As New SKPaint With {.IsAntialias = True}
                If mono Then
                    gFill.Color = New SKColor(200, 200, 200)
                Else
                    gFill.Shader = LinGrad(p1.X, p1.Y, p3.X, p3.Y, New SKColor(50, 80, 160), New SKColor(20, 40, 95))
                End If
                canvas.DrawPath(path, gFill)
            End Using
            ' cell grid (6x4)
            Dim nCols = 6, nRows = 4
            Using cp As New SKPaint With {.Color = If(mono, New SKColor(80, 80, 80), New SKColor(140, 170, 220)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.7F, .IsAntialias = True}
                ' vertical lines (interpolate along top and bottom edges)
                For i = 1 To nCols - 1
                    Dim t = i / CSng(nCols)
                    Dim a As New SKPoint(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y))
                    Dim b As New SKPoint(p4.X + t * (p3.X - p4.X), p4.Y + t * (p3.Y - p4.Y))
                    canvas.DrawLine(a, b, cp)
                Next
                For i = 1 To nRows - 1
                    Dim t = i / CSng(nRows)
                    Dim a As New SKPoint(p1.X + t * (p4.X - p1.X), p1.Y + t * (p4.Y - p1.Y))
                    Dim b As New SKPoint(p2.X + t * (p3.X - p2.X), p2.Y + t * (p3.Y - p2.Y))
                    canvas.DrawLine(a, b, cp)
                Next
            End Using
            ' specular highlight (diagonal band)
            If Not mono Then
                Using hi As New SKPaint With {.IsAntialias = True}
                    hi.Shader = LinGrad(p1.X, p1.Y, p2.X, p2.Y, New SKColor(255, 255, 255, 90), New SKColor(255, 255, 255, 0))
                    Dim hpath As New SKPath()
                    hpath.MoveTo(p1.X, p1.Y)
                    hpath.LineTo(p1.X + 0.25F * (p2.X - p1.X), p1.Y + 0.25F * (p2.Y - p1.Y))
                    hpath.LineTo(p4.X + 0.25F * (p3.X - p4.X), p4.Y + 0.25F * (p3.Y - p4.Y))
                    hpath.LineTo(p4.X, p4.Y) : hpath.Close()
                    canvas.DrawPath(hpath, hi)
                End Using
            End If
            ' outline
            Using s = Stroke(mono, True) : canvas.DrawPath(path, s) : End Using
            ' mast brace
            Using br As New SKPaint With {.Color = If(mono, New SKColor(90, 90, 90), New SKColor(100, 115, 135)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 2.0F, .IsAntialias = True}
                canvas.DrawLine(cx, gy + 0.62F * h, cx - 0.03F * w, gy + 0.75F * h, br)
                canvas.DrawLine(cx, gy + 0.62F * h, cx + 0.03F * w, gy + 0.75F * h, br)
            End Using
        End Sub

        ' --------- WIND TURBINE ---------
        Public Sub DrawWindTurbine(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, mono As Boolean)
            Dim cx = gx + 0.5F * w
            Dim hubX = cx, hubY = gy + 0.35F * h
            Dim hubR = 0.06F * Min(w, h)
            ' ground
            Using gp As New SKPaint With {.Color = If(mono, New SKColor(120, 120, 120), New SKColor(160, 170, 180)), .StrokeWidth = 1.4F, .Style = SKPaintStyle.Stroke, .IsAntialias = True}
                canvas.DrawLine(gx + 0.05F * w, gy + 0.97F * h, gx + 0.95F * w, gy + 0.97F * h, gp)
            End Using
            ' tower (tapered) via path
            Dim towerTopW = 0.04F * w, towerBotW = 0.1F * w
            Dim tower As New SKPath()
            tower.MoveTo(cx - towerTopW / 2, hubY + hubR * 0.3F)
            tower.LineTo(cx + towerTopW / 2, hubY + hubR * 0.3F)
            tower.LineTo(cx + towerBotW / 2, gy + 0.97F * h)
            tower.LineTo(cx - towerBotW / 2, gy + 0.97F * h)
            tower.Close()
            Using tp As New SKPaint With {.IsAntialias = True}
                If mono Then
                    tp.Color = New SKColor(230, 230, 230)
                Else
                    tp.Shader = LinGrad(cx - towerBotW, gy, cx + towerBotW, gy, New SKColor(235, 240, 248), New SKColor(190, 200, 215))
                End If
                canvas.DrawPath(tower, tp)
            End Using
            Using s = Stroke(mono) : canvas.DrawPath(tower, s) : End Using
            ' nacelle
            Dim nac As New SKRect(cx - 0.09F * w, hubY - 0.035F * h, cx + 0.04F * w, hubY + 0.035F * h)
            Using np As New SKPaint With {.Color = If(mono, New SKColor(220, 220, 220), New SKColor(230, 235, 245)), .IsAntialias = True}
                canvas.DrawRoundRect(nac, 3, 3, np)
            End Using
            Using s = Stroke(mono) : canvas.DrawRoundRect(nac, 3, 3, s) : End Using
            ' 3 blades at 0, 120, 240 deg
            Dim bladeLen = 0.34F * Min(w, h)
            Dim bladeW = 0.04F * Min(w, h)
            For i = 0 To 2
                Dim ang = -PI / 2 + i * 2 * PI / 3  ' start pointing up
                canvas.Save()
                canvas.Translate(hubX, hubY)
                canvas.RotateRadians(CSng(ang))
                Dim blade As New SKPath()
                blade.MoveTo(0, 0)
                blade.LineTo(-bladeW * 0.5F, -bladeLen * 0.3F)
                blade.LineTo(-bladeW * 0.15F, -bladeLen)
                blade.LineTo(bladeW * 0.15F, -bladeLen)
                blade.LineTo(bladeW * 0.5F, -bladeLen * 0.3F)
                blade.Close()
                Using bp As New SKPaint With {.IsAntialias = True}
                    If mono Then
                        bp.Color = New SKColor(245, 245, 245)
                    Else
                        bp.Shader = SKShader.CreateLinearGradient(New SKPoint(-bladeW, 0), New SKPoint(bladeW, 0),
                            New SKColor() {New SKColor(255, 255, 255), New SKColor(200, 210, 225)}, Nothing, SKShaderTileMode.Clamp)
                    End If
                    canvas.DrawPath(blade, bp)
                End Using
                Using s = Stroke(mono) : canvas.DrawPath(blade, s) : End Using
                canvas.Restore()
            Next
            ' hub
            Using hp As New SKPaint With {.Color = If(mono, New SKColor(200, 200, 200), New SKColor(200, 210, 225)), .IsAntialias = True}
                canvas.DrawCircle(hubX, hubY, hubR, hp)
            End Using
            Using s = Stroke(mono) : canvas.DrawCircle(hubX, hubY, hubR, s) : End Using
        End Sub

        ' --------- HYDROELECTRIC TURBINE ---------
        Public Sub DrawHydroTurbine(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, mono As Boolean)
            Dim cx = gx + 0.5F * w, cy = gy + 0.55F * h
            ' scroll casing (spiral volute) - approximate as big circle + decreasing spiral hint
            Dim scrollR = 0.32F * Min(w, h)
            Using sp As New SKPaint With {.IsAntialias = True}
                If mono Then
                    sp.Color = New SKColor(210, 210, 210)
                Else
                    sp.Shader = SKShader.CreateRadialGradient(New SKPoint(cx - scrollR * 0.3F, cy - scrollR * 0.3F), scrollR * 1.4F,
                        New SKColor() {New SKColor(130, 175, 220), New SKColor(40, 85, 150)}, Nothing, SKShaderTileMode.Clamp)
                End If
                canvas.DrawCircle(cx, cy, scrollR, sp)
            End Using
            Using s = Stroke(mono, True) : canvas.DrawCircle(cx, cy, scrollR, s) : End Using
            ' inlet penstock from left
            Dim penY = cy
            Using pp As New SKPaint With {.Color = If(mono, New SKColor(180, 180, 180), New SKColor(100, 130, 170)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(gx, penY - 0.06F * h, cx - scrollR * 0.85F, penY + 0.06F * h), pp)
            End Using
            Using s = Stroke(mono) : canvas.DrawRect(New SKRect(gx, penY - 0.06F * h, cx - scrollR * 0.85F, penY + 0.06F * h), s) : End Using
            ' generator box on top
            Dim gen As New SKRect(cx - 0.13F * w, gy + 0.05F * h, cx + 0.13F * w, cy - scrollR * 0.7F)
            Using gp As New SKPaint With {.IsAntialias = True}
                If mono Then
                    gp.Color = New SKColor(230, 230, 230)
                Else
                    gp.Shader = LinGrad(gen.Left, gen.Top, gen.Right, gen.Bottom, New SKColor(245, 210, 120), New SKColor(180, 140, 60))
                End If
                canvas.DrawRoundRect(gen, 4, 4, gp)
            End Using
            Using s = Stroke(mono, True) : canvas.DrawRoundRect(gen, 4, 4, s) : End Using
            ' fins on generator
            Using fp As New SKPaint With {.Color = If(mono, New SKColor(80, 80, 80), New SKColor(120, 85, 30)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.8F, .IsAntialias = True}
                For i = 1 To 6
                    Dim fy = gen.Top + i * (gen.Bottom - gen.Top) / 7
                    canvas.DrawLine(gen.Left + 2, fy, gen.Right - 2, fy, fp)
                Next
            End Using
            ' runner (turbine rotor) hint - inner circle with blades
            Using rp As New SKPaint With {.Color = If(mono, New SKColor(100, 100, 100), New SKColor(70, 95, 140)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.3F, .IsAntialias = True}
                canvas.DrawCircle(cx, cy, scrollR * 0.45F, rp)
                Dim nB = 8
                For i = 0 To nB - 1
                    Dim ang = i * 2 * PI / nB
                    Dim x1 = cx + CSng(Cos(ang)) * scrollR * 0.18F
                    Dim y1 = cy + CSng(Sin(ang)) * scrollR * 0.18F
                    Dim x2 = cx + CSng(Cos(ang)) * scrollR * 0.42F
                    Dim y2 = cy + CSng(Sin(ang)) * scrollR * 0.42F
                    canvas.DrawLine(x1, y1, x2, y2, rp)
                Next
                canvas.DrawCircle(cx, cy, scrollR * 0.15F, rp)
            End Using
            ' draft tube (outlet at bottom)
            Using dp As New SKPaint With {.Color = If(mono, New SKColor(180, 180, 180), New SKColor(100, 130, 170)), .IsAntialias = True}
                Dim tube As New SKPath()
                tube.MoveTo(cx - 0.08F * w, cy + scrollR * 0.85F)
                tube.LineTo(cx + 0.08F * w, cy + scrollR * 0.85F)
                tube.LineTo(cx + 0.14F * w, gy + 0.97F * h)
                tube.LineTo(cx - 0.14F * w, gy + 0.97F * h)
                tube.Close()
                canvas.DrawPath(tube, dp)
                Using s = Stroke(mono) : canvas.DrawPath(tube, s) : End Using
            End Using
        End Sub

        ' --------- ELECTROLYZER / FUEL CELL STACK (shared geometry) ---------
        Private Sub DrawStackInternal(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single,
                                      mono As Boolean, leftLabel As String, rightLabel As String,
                                      bodyColor As SKColor)
            ' End plates + many bipolar plates stacked horizontally between them
            Dim stackRect As New SKRect(gx + 0.18F * w, gy + 0.22F * h, gx + 0.82F * w, gy + 0.78F * h)
            Dim epW = 0.06F * w
            Dim leftEP As New SKRect(stackRect.Left - epW, stackRect.Top - 0.03F * h, stackRect.Left, stackRect.Bottom + 0.03F * h)
            Dim rightEP As New SKRect(stackRect.Right, stackRect.Top - 0.03F * h, stackRect.Right + epW, stackRect.Bottom + 0.03F * h)
            ' main body gradient
            Using bp As New SKPaint With {.IsAntialias = True}
                If mono Then
                    bp.Color = New SKColor(215, 215, 215)
                Else
                    bp.Shader = LinGrad(stackRect.Left, stackRect.Top, stackRect.Left, stackRect.Bottom,
                                        New SKColor(CByte(Min(255, CInt(bodyColor.Red) + 40)),
                                                    CByte(Min(255, CInt(bodyColor.Green) + 40)),
                                                    CByte(Min(255, CInt(bodyColor.Blue) + 40))),
                                        bodyColor)
                End If
                canvas.DrawRect(stackRect, bp)
            End Using
            ' bipolar plate stripes
            Using sp As New SKPaint With {.Color = If(mono, New SKColor(80, 80, 80), New SKColor(45, 60, 85)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.6F, .IsAntialias = True}
                Dim nP = 22
                For i = 1 To nP - 1
                    Dim xp = stackRect.Left + i * (stackRect.Right - stackRect.Left) / nP
                    canvas.DrawLine(xp, stackRect.Top + 2, xp, stackRect.Bottom - 2, sp)
                Next
            End Using
            Using s = Stroke(mono, True) : canvas.DrawRect(stackRect, s) : End Using
            ' end plates
            Using ep As New SKPaint With {.Color = If(mono, New SKColor(160, 160, 160), New SKColor(120, 130, 150)), .IsAntialias = True}
                canvas.DrawRect(leftEP, ep) : canvas.DrawRect(rightEP, ep)
            End Using
            Using s = Stroke(mono, True)
                canvas.DrawRect(leftEP, s) : canvas.DrawRect(rightEP, s)
            End Using
            ' tie rods (4 circles on each end plate)
            Using tp As New SKPaint With {.Color = If(mono, New SKColor(50, 50, 50), New SKColor(70, 80, 95)), .IsAntialias = True}
                Dim rr = 0.012F * w
                For Each ep In New SKRect() {leftEP, rightEP}
                    Dim ecx = (ep.Left + ep.Right) * 0.5F
                    canvas.DrawCircle(ecx, ep.Top + 0.05F * h, rr, tp)
                    canvas.DrawCircle(ecx, ep.Bottom - 0.05F * h, rr, tp)
                Next
            End Using
            ' inlet/outlet ports
            Using pp As New SKPaint With {.Color = If(mono, New SKColor(140, 140, 140), New SKColor(90, 110, 140)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(gx, gy + 0.45F * h, leftEP.Left, gy + 0.55F * h), pp)
                canvas.DrawRect(New SKRect(rightEP.Right, gy + 0.45F * h, gx + w, gy + 0.55F * h), pp)
            End Using
            Using s = Stroke(mono)
                canvas.DrawRect(New SKRect(gx, gy + 0.45F * h, leftEP.Left, gy + 0.55F * h), s)
                canvas.DrawRect(New SKRect(rightEP.Right, gy + 0.45F * h, gx + w, gy + 0.55F * h), s)
            End Using
            ' labels
            Using tf As New SKPaint With {.Color = If(mono, SKColors.Black, New SKColor(30, 45, 75)), .IsAntialias = True, .TextSize = 0.09F * h, .TextAlign = SKTextAlign.Center, .FakeBoldText = True}
                canvas.DrawText(leftLabel, (gx + leftEP.Left) * 0.5F, gy + 0.42F * h, tf)
                canvas.DrawText(rightLabel, (rightEP.Right + gx + w) * 0.5F, gy + 0.42F * h, tf)
            End Using
        End Sub

        Public Sub DrawElectrolyzer(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, mono As Boolean)
            DrawStackInternal(canvas, gx, gy, w, h, mono, "H2O", "H2/O2",
                              If(mono, New SKColor(200, 200, 200), New SKColor(70, 115, 175)))
        End Sub

        Public Sub DrawFuelCell(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, mono As Boolean)
            DrawStackInternal(canvas, gx, gy, w, h, mono, "H2/Air", "Power",
                              If(mono, New SKColor(200, 200, 200), New SKColor(90, 140, 90)))
        End Sub

    End Module

End Namespace
