'    LogicalOpSphereHelper - renders logical-op icons as 3D glossy spheres.
'
'    Used by Adjust, Spec, Recycle, EnergyRecycle, InformationCarrier to give
'    the default/DrawMode 0,2 rendering a consistent, polished "marble/ball"
'    look with a letter or short caption in the center.

Imports SkiaSharp

Namespace GraphicObjects.Shapes

    Public Module LogicalOpSphereHelper

        ''' <summary>
        ''' Draws a 3D-shaded glossy sphere inside (x, y, w, h) with a centered caption.
        ''' The sphere is coloured using the supplied base/edge colors. The top-left
        ''' highlight gives it a convex "ball" feel; a separate specular arc adds gloss.
        ''' </summary>
        ''' <param name="canvas">target canvas</param>
        ''' <param name="x">top-left X</param>
        ''' <param name="y">top-left Y</param>
        ''' <param name="w">width</param>
        ''' <param name="h">height</param>
        ''' <param name="centerColor">bright color at the top-left highlight</param>
        ''' <param name="edgeColor">darker shadow color at the bottom-right edge</param>
        ''' <param name="outlineColor">outline stroke color</param>
        ''' <param name="textColor">caption text color</param>
        ''' <param name="caption">short caption (1-2 chars)</param>
        ''' <param name="textSizeFactor">text size as fraction of the smaller dimension (default 0.5)</param>
        ''' <param name="antiAlias">antialias flag (usually GlobalSettings.Settings.DrawingAntiAlias)</param>
        ''' <param name="typeFace">typeface to use for the caption</param>
        Public Sub DrawSphere(canvas As SKCanvas,
                              x As Single, y As Single, w As Single, h As Single,
                              centerColor As SKColor, edgeColor As SKColor,
                              outlineColor As SKColor, textColor As SKColor,
                              caption As String,
                              Optional textSizeFactor As Single = 0.5F,
                              Optional antiAlias As Boolean = True,
                              Optional typeFace As SKTypeface = Nothing)

            Dim rect As New SKRect(x, y, x + w, y + h)
            Dim cx = x + w * 0.5F
            Dim cy = y + h * 0.5F
            Dim radius = Math.Max(w, h) * 0.75F

            ' 1. Radial gradient body (highlight at top-left → edge color at bottom-right)
            Using bodyFill As New SKPaint With {.IsAntialias = antiAlias,
                                                .Shader = SKShader.CreateRadialGradient(
                                                    New SKPoint(x + w * 0.32F, y + h * 0.28F),
                                                    radius,
                                                    New SKColor() {centerColor, edgeColor},
                                                    New Single() {0.0F, 1.0F},
                                                    SKShaderTileMode.Clamp)}
                canvas.DrawOval(rect, bodyFill)
            End Using

            ' 2. Soft inner shadow near bottom-right (deepens the 3D feel)
            Using shadow As New SKPaint With {.IsAntialias = antiAlias,
                                              .Shader = SKShader.CreateRadialGradient(
                                                  New SKPoint(x + w * 0.72F, y + h * 0.75F),
                                                  radius * 0.9F,
                                                  New SKColor() {New SKColor(0, 0, 0, 80), New SKColor(0, 0, 0, 0)},
                                                  New Single() {0.0F, 0.7F},
                                                  SKShaderTileMode.Clamp)}
                canvas.DrawOval(rect, shadow)
            End Using

            ' 3. Specular highlight arc (small bright ellipse near top-left)
            Dim hiW = w * 0.55F
            Dim hiH = h * 0.28F
            Dim hiRect As New SKRect(x + w * 0.18F, y + h * 0.1F, x + w * 0.18F + hiW, y + h * 0.1F + hiH)
            Using highlight As New SKPaint With {.IsAntialias = antiAlias,
                                                 .Shader = SKShader.CreateLinearGradient(
                                                     New SKPoint(hiRect.Left, hiRect.Top),
                                                     New SKPoint(hiRect.Left, hiRect.Bottom),
                                                     New SKColor() {New SKColor(255, 255, 255, 190), New SKColor(255, 255, 255, 0)},
                                                     New Single() {0.0F, 1.0F},
                                                     SKShaderTileMode.Clamp)}
                canvas.DrawOval(hiRect, highlight)
            End Using

            ' 4. Outline stroke
            Using stroke As New SKPaint With {.IsAntialias = antiAlias,
                                              .Color = outlineColor,
                                              .Style = SKPaintStyle.Stroke,
                                              .StrokeWidth = 1.3F}
                canvas.DrawOval(rect, stroke)
            End Using

            ' 5. Caption
            If Not String.IsNullOrEmpty(caption) Then
                Using tpaint As New SKPaint With {.IsAntialias = antiAlias,
                                                  .Color = textColor,
                                                  .IsStroke = False,
                                                  .TextSize = Math.Min(w, h) * textSizeFactor,
                                                  .Typeface = typeFace}
                    Dim bounds As New SKRect()
                    tpaint.MeasureText(caption, bounds)
                    Dim tx = cx - bounds.MidX
                    Dim ty = cy - bounds.MidY
                    canvas.DrawText(caption, tx, ty, tpaint)
                End Using
            End If

        End Sub

        ''' <summary>Draws a dashed b/w sphere outline with centered caption (used for DrawMode = 1).</summary>
        Public Sub DrawSphereMono(canvas As SKCanvas,
                                  x As Single, y As Single, w As Single, h As Single,
                                  caption As String,
                                  Optional textSizeFactor As Single = 0.5F,
                                  Optional antiAlias As Boolean = True,
                                  Optional typeFace As SKTypeface = Nothing)

            Dim rect As New SKRect(x, y, x + w, y + h)
            Using outline As New SKPaint With {.IsAntialias = antiAlias,
                                               .Color = SKColors.Black,
                                               .Style = SKPaintStyle.Stroke,
                                               .StrokeWidth = 1.2F,
                                               .PathEffect = SKPathEffect.CreateDash(New Single() {2.0F, 2.0F}, 4.0F)}
                canvas.DrawOval(rect, outline)
            End Using
            If Not String.IsNullOrEmpty(caption) Then
                Using tpaint As New SKPaint With {.IsAntialias = antiAlias,
                                                  .Color = SKColors.Black,
                                                  .IsStroke = False,
                                                  .TextSize = Math.Min(w, h) * textSizeFactor,
                                                  .Typeface = typeFace}
                    Dim bounds As New SKRect()
                    tpaint.MeasureText(caption, bounds)
                    Dim tx = x + w * 0.5F - bounds.MidX
                    Dim ty = y + h * 0.5F - bounds.MidY
                    canvas.DrawText(caption, tx, ty, tpaint)
                End Using
            End If
        End Sub

    End Module

End Namespace
