Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.ExtensionMethods

''' <summary>
''' How material streams are coloured on the flowsheet. Status is the default: blue when solved,
''' salmon when not. The others paint every solved stream, and the lines attached to it, on a
''' colour scale of one property, so a student sees at a glance where the process is hot, where
''' the pressure drops, and which streams are vapour.
''' </summary>
Public Enum StreamColorMode
    [Status] = 0
    Temperature = 1
    Pressure = 2
    VaporFraction = 3
    Phase = 4
    MassFlow = 5
End Enum

''' <summary>
''' Colours for the stream colour modes, and the legend that explains them.
''' </summary>
''' <remarks>
''' The scale of a property runs from the smallest to the largest value among the solved streams
''' of the flowsheet, so the colours always spread over the whole range in play. That range is
''' read once per frame and cached for a fifth of a second, because every stream and every
''' connector asks for it while a frame is drawn.
''' </remarks>
Public Class StreamColoring

    Private Class RangeCache
        Public Ticks As Long
        Public Mode As StreamColorMode
        Public Min As Double
        Public Max As Double
    End Class

    Private Shared ReadOnly Cache As New Dictionary(Of Integer, RangeCache)
    Private Shared ReadOnly CacheLock As New Object

    ''' <summary>The colour mode the flowsheet is set to, Status when it has no options.</summary>
    Public Shared Function ModeOf(flowsheet As IFlowsheet) As StreamColorMode
        If flowsheet Is Nothing OrElse flowsheet.FlowsheetOptions Is Nothing Then Return StreamColorMode.Status
        Dim mode = flowsheet.FlowsheetOptions.StreamColorMode
        If mode < 0 OrElse mode > 5 Then Return StreamColorMode.Status
        Return CType(mode, StreamColorMode)
    End Function

    ''' <summary>
    ''' The colour of a solved material stream in the flowsheet's mode, or Nothing when the mode
    ''' is Status, the stream is not solved, or it has no usable value.
    ''' </summary>
    Public Shared Function ColorOf(stream As IMaterialStream) As SKColor?

        If stream Is Nothing Then Return Nothing
        Dim flowsheet = stream.Flowsheet
        Dim mode = ModeOf(flowsheet)
        If mode = StreamColorMode.Status Then Return Nothing

        Dim obj = TryCast(stream, ISimulationObject)
        If obj Is Nothing OrElse Not obj.Calculated Then Return Nothing
        If obj.GraphicObject IsNot Nothing AndAlso Not obj.GraphicObject.Active Then Return Nothing

        If mode = StreamColorMode.Phase Then Return PhaseColor(stream)

        Dim value = ValueOf(stream, mode)
        If Double.IsNaN(value) Then Return Nothing

        Dim min, max As Double
        GetRange(flowsheet, mode, min, max)

        Return ScaleColor(mode, Fraction(mode, value, min, max))

    End Function

    ''' <summary>The property the mode paints, in SI, or NaN when the stream cannot give it.</summary>
    Public Shared Function ValueOf(stream As IMaterialStream, mode As StreamColorMode) As Double
        Try
            Select Case mode
                Case StreamColorMode.Temperature
                    Return stream.GetTemperature()
                Case StreamColorMode.Pressure
                    Return stream.GetPressure()
                Case StreamColorMode.VaporFraction
                    Return stream.Phases(2).Properties.molarfraction.GetValueOrDefault()
                Case StreamColorMode.MassFlow
                    Return stream.GetMassFlow()
                Case Else
                    Return Double.NaN
            End Select
        Catch ex As Exception
            Return Double.NaN
        End Try
    End Function

    ''' <summary>
    ''' The smallest and largest value of the mode's property among the solved streams. A
    ''' flowsheet with one value, or none, gets a range of width one around it so nothing
    ''' divides by zero.
    ''' </summary>
    Public Shared Sub GetRange(flowsheet As IFlowsheet, mode As StreamColorMode, ByRef min As Double, ByRef max As Double)

        min = 0.0 : max = 1.0
        If flowsheet Is Nothing Then Return

        Dim key = flowsheet.GetHashCode()
        Dim now = Date.Now.Ticks

        SyncLock CacheLock
            Dim cached As RangeCache = Nothing
            If Cache.TryGetValue(key, cached) AndAlso cached.Mode = mode AndAlso now - cached.Ticks < TimeSpan.TicksPerMillisecond * 200 Then
                min = cached.Min : max = cached.Max
                Return
            End If
        End SyncLock

        Dim lo = Double.PositiveInfinity, hi = Double.NegativeInfinity
        For Each obj In flowsheet.SimulationObjects.Values
            If obj.GraphicObject Is Nothing OrElse obj.GraphicObject.ObjectType <> ObjectType.MaterialStream Then Continue For
            If Not obj.Calculated OrElse Not obj.GraphicObject.Active Then Continue For
            Dim stream = TryCast(obj, IMaterialStream)
            If stream Is Nothing Then Continue For
            Dim v = ValueOf(stream, mode)
            If Double.IsNaN(v) OrElse Double.IsInfinity(v) Then Continue For
            If mode = StreamColorMode.MassFlow AndAlso v <= 0.0 Then Continue For
            If v < lo Then lo = v
            If v > hi Then hi = v
        Next

        If Double.IsInfinity(lo) Then
            lo = 0.0 : hi = 1.0
        ElseIf hi - lo < 1.0E-12 Then
            lo -= 0.5 : hi += 0.5
        End If

        If mode = StreamColorMode.VaporFraction Then
            lo = 0.0 : hi = 1.0
        End If

        SyncLock CacheLock
            Cache(key) = New RangeCache With {.Ticks = now, .Mode = mode, .Min = lo, .Max = hi}
        End SyncLock

        min = lo : max = hi

    End Sub

    ''' <summary>Whether the mass flow scale is logarithmic: it is when the flows span more than two decades.</summary>
    Public Shared Function IsLogScale(mode As StreamColorMode, min As Double, max As Double) As Boolean
        Return mode = StreamColorMode.MassFlow AndAlso min > 0.0 AndAlso max / min > 100.0
    End Function

    ''' <summary>Where a value sits on the scale, 0 at the minimum and 1 at the maximum.</summary>
    Public Shared Function Fraction(mode As StreamColorMode, value As Double, min As Double, max As Double) As Double
        Dim f As Double
        If IsLogScale(mode, min, max) Then
            If value <= 0.0 Then Return 0.0
            f = (Math.Log10(value) - Math.Log10(min)) / (Math.Log10(max) - Math.Log10(min))
        Else
            f = (value - min) / (max - min)
        End If
        If Double.IsNaN(f) Then Return 0.0
        Return Math.Max(0.0, Math.Min(1.0, f))
    End Function

    ' ── Scales ───────────────────────────────────────────────────────────

    ''' <summary>Blue through grey to red: cold to hot.</summary>
    Private Shared ReadOnly TemperatureStops As SKColor() = {
        New SKColor(59, 76, 192), New SKColor(140, 176, 240), New SKColor(221, 221, 221),
        New SKColor(244, 154, 123), New SKColor(180, 4, 38)}

    ''' <summary>Pale yellow to deep blue: low to high pressure.</summary>
    Private Shared ReadOnly PressureStops As SKColor() = {
        New SKColor(255, 255, 204), New SKColor(161, 218, 180), New SKColor(65, 182, 196),
        New SKColor(34, 94, 168), New SKColor(12, 44, 132)}

    ''' <summary>Blue liquid through purple to orange vapour.</summary>
    Private Shared ReadOnly VaporFractionStops As SKColor() = {
        New SKColor(31, 119, 180), New SKColor(148, 103, 189), New SKColor(255, 127, 14)}

    ''' <summary>Pale to dark green: small to large flow.</summary>
    Private Shared ReadOnly MassFlowStops As SKColor() = {
        New SKColor(229, 245, 224), New SKColor(116, 196, 118), New SKColor(0, 90, 50)}

    Public Shared ReadOnly LiquidColor As New SKColor(31, 119, 180)
    Public Shared ReadOnly VaporColor As New SKColor(255, 127, 14)
    Public Shared ReadOnly TwoPhaseColor As New SKColor(148, 103, 189)
    Public Shared ReadOnly SolidColor As New SKColor(140, 86, 75)

    ''' <summary>The colour at a fraction of the mode's scale.</summary>
    Public Shared Function ScaleColor(mode As StreamColorMode, fraction As Double) As SKColor
        Dim stops As SKColor()
        Select Case mode
            Case StreamColorMode.Temperature : stops = TemperatureStops
            Case StreamColorMode.Pressure : stops = PressureStops
            Case StreamColorMode.VaporFraction : stops = VaporFractionStops
            Case StreamColorMode.MassFlow : stops = MassFlowStops
            Case Else : Return SKColors.SteelBlue
        End Select
        Return Interpolate(stops, fraction)
    End Function

    Private Shared Function Interpolate(stops As SKColor(), fraction As Double) As SKColor
        If fraction <= 0.0 Then Return stops(0)
        If fraction >= 1.0 Then Return stops(stops.Length - 1)
        Dim scaled = fraction * (stops.Length - 1)
        Dim i = CInt(Math.Floor(scaled))
        Dim t = scaled - i
        Dim a = stops(i), b = stops(Math.Min(i + 1, stops.Length - 1))
        Return New SKColor(Channel(a.Red, b.Red, t), Channel(a.Green, b.Green, t), Channel(a.Blue, b.Blue, t))
    End Function

    Private Shared Function Channel(a As Byte, b As Byte, t As Double) As Byte
        Dim v = CDbl(a) + (CDbl(b) - CDbl(a)) * t
        Return CByte(Math.Max(0.0, Math.Min(255.0, Math.Round(v))))
    End Function

    ''' <summary>The colour of the phase state: liquid, vapour, two-phase or solid.</summary>
    Public Shared Function PhaseColor(stream As IMaterialStream) As SKColor?
        Dim vapor, solid As Double
        Try
            vapor = stream.Phases(2).Properties.molarfraction.GetValueOrDefault()
        Catch ex As Exception
            Return Nothing
        End Try
        Try
            solid = stream.Phases(7).Properties.molarfraction.GetValueOrDefault()
        Catch ex As Exception
            solid = 0.0
        End Try
        If solid > 0.5 Then Return SolidColor
        If vapor > 0.999 Then Return VaporColor
        If vapor < 0.001 Then Return LiquidColor
        Return TwoPhaseColor
    End Function

    ' ── Legend ───────────────────────────────────────────────────────────

    ''' <summary>What the legend calls the mode.</summary>
    Public Shared Function Title(mode As StreamColorMode, su As IUnitsOfMeasure) As String
        Select Case mode
            Case StreamColorMode.Temperature : Return "Temperature (" & su.temperature & ")"
            Case StreamColorMode.Pressure : Return "Pressure (" & su.pressure & ")"
            Case StreamColorMode.VaporFraction : Return "Vapour fraction (molar)"
            Case StreamColorMode.MassFlow : Return "Mass flow (" & su.massflow & ")"
            Case StreamColorMode.Phase : Return "Phase"
            Case Else : Return ""
        End Select
    End Function

    ''' <summary>A scale value in the flowsheet's units, formatted for the legend.</summary>
    Private Shared Function Label(mode As StreamColorMode, value As Double, su As IUnitsOfMeasure) As String
        Dim units As String = ""
        Select Case mode
            Case StreamColorMode.Temperature : units = su.temperature
            Case StreamColorMode.Pressure : units = su.pressure
            Case StreamColorMode.MassFlow : units = su.massflow
        End Select
        Dim shown = value
        If units <> "" Then
            Try
                shown = value.ConvertFromSI(units)
            Catch ex As Exception
                shown = value
            End Try
        End If
        Return shown.ToString("G4", Globalization.CultureInfo.InvariantCulture)
    End Function

    ''' <summary>
    ''' Draws the legend of the flowsheet's colour mode in the bottom-left corner of the view, in
    ''' screen pixels. Draws nothing in Status mode.
    ''' </summary>
    Public Shared Sub DrawLegend(canvas As SKCanvas, flowsheet As IFlowsheet, viewWidth As Single, viewHeight As Single,
                                 darkMode As Boolean, typeface As SKTypeface)

        Dim mode = ModeOf(flowsheet)
        If mode = StreamColorMode.Status Then Return

        Dim su = flowsheet.FlowsheetOptions.SelectedUnitSystem
        Dim min, max As Double
        GetRange(flowsheet, mode, min, max)

        Const barWidth As Single = 180
        Const barHeight As Single = 12
        Const margin As Single = 14
        Const pad As Single = 8

        Dim textColor = If(darkMode, SKColors.WhiteSmoke, SKColors.Black)
        Dim boxColor = If(darkMode, New SKColor(30, 30, 30, 220), New SKColor(255, 255, 255, 220))

        Using textPaint As New SKPaint With {.Color = textColor, .IsAntialias = True, .TextSize = 11, .Typeface = typeface}
        Using boxPaint As New SKPaint With {.Color = boxColor, .IsAntialias = True, .IsStroke = False}
        Using borderPaint As New SKPaint With {.Color = If(darkMode, SKColors.DimGray, SKColors.LightGray), .IsAntialias = True, .IsStroke = True, .StrokeWidth = 1}

            Dim heading = Title(mode, su)
            If IsLogScale(mode, min, max) Then heading &= ", log scale"

            Dim rows As Integer = If(mode = StreamColorMode.Phase, 4, 1)
            Dim boxHeight As Single = pad + 14 + pad + If(mode = StreamColorMode.Phase, rows * 16, barHeight + 14) + pad
            Dim boxWidth As Single = Math.Max(barWidth, textPaint.MeasureText(heading)) + 2 * pad
            Dim left = margin
            Dim top = viewHeight - margin - boxHeight

            canvas.Save()
            canvas.ResetMatrix()

            Dim box = New SKRect(left, top, left + boxWidth, top + boxHeight)
            canvas.DrawRoundRect(box, 4, 4, boxPaint)
            canvas.DrawRoundRect(box, 4, 4, borderPaint)

            canvas.DrawText(heading, left + pad, top + pad + 11, textPaint)

            If mode = StreamColorMode.Phase Then
                Dim entries = {Tuple.Create(LiquidColor, "Liquid"), Tuple.Create(VaporColor, "Vapour"),
                               Tuple.Create(TwoPhaseColor, "Two-phase"), Tuple.Create(SolidColor, "Solid")}
                Dim y = top + pad + 14 + pad
                For Each entry In entries
                    Using swatch As New SKPaint With {.Color = entry.Item1, .IsAntialias = True, .IsStroke = False}
                        canvas.DrawRoundRect(New SKRect(left + pad, y + 2, left + pad + 14, y + 12), 2, 2, swatch)
                    End Using
                    canvas.DrawText(entry.Item2, left + pad + 20, y + 11, textPaint)
                    y += 16
                Next
            Else
                Dim barTop = top + pad + 14 + pad
                Dim barLeft = left + pad
                Const steps As Integer = 40
                For i = 0 To steps - 1
                    Using segment As New SKPaint With {.Color = ScaleColor(mode, i / (steps - 1.0)), .IsStroke = False}
                        canvas.DrawRect(New SKRect(barLeft + i * barWidth / steps, barTop, barLeft + (i + 1) * barWidth / steps + 0.5F, barTop + barHeight), segment)
                    End Using
                Next
                canvas.DrawRect(New SKRect(barLeft, barTop, barLeft + barWidth, barTop + barHeight), borderPaint)

                Dim lo = Label(mode, min, su)
                Dim hi = Label(mode, max, su)
                canvas.DrawText(lo, barLeft, barTop + barHeight + 12, textPaint)
                canvas.DrawText(hi, barLeft + barWidth - textPaint.MeasureText(hi), barTop + barHeight + 12, textPaint)
            End If

            canvas.Restore()

        End Using
        End Using
        End Using

    End Sub

End Class
