'    Background color of the flowsheet drawing, shared by the classic and the cross-platform editors
'    Copyright 2026 Daniel Wagner Oliveira de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports System.Collections.Generic
Imports SkiaSharp
Imports DWSIM.Interfaces

Namespace Appearance

    ''' <summary>
    ''' The background color of the flowsheet drawing, kept in the flowsheet options as #RRGGBB (empty follows
    ''' the interface theme). Only the clear at the start of a frame changes, so any color costs the same to
    ''' draw; whether it is dark decides the line and text colors of the objects (GraphicsSurface.DarkCanvas).
    ''' </summary>
    Public NotInheritable Class FlowsheetBackground

        Private Sub New()
        End Sub

        ''' <summary>Colors offered as one-click choices, with their names.</summary>
        Public Shared ReadOnly Property Presets As IReadOnlyList(Of KeyValuePair(Of String, SKColor)) =
            New List(Of KeyValuePair(Of String, SKColor)) From {
                New KeyValuePair(Of String, SKColor)("White", SKColors.White),
                New KeyValuePair(Of String, SKColor)("Light grey", New SKColor(&HF0, &HF0, &HF0)),
                New KeyValuePair(Of String, SKColor)("Paper", New SKColor(&HFA, &HF6, &HEC)),
                New KeyValuePair(Of String, SKColor)("Light blue", New SKColor(&HEC, &HF2, &HF8)),
                New KeyValuePair(Of String, SKColor)("Dark grey", New SKColor(&H1E, &H1E, &H1E)),
                New KeyValuePair(Of String, SKColor)("Navy", New SKColor(&H16, &H22, &H3A))}

        ''' <summary>The flowsheet's own background color; False when it follows the interface theme.</summary>
        Public Shared Function TryGetColor(options As IFlowsheetOptions, ByRef color As SKColor) As Boolean
            Dim text = options?.FlowsheetBackgroundColor
            If String.IsNullOrWhiteSpace(text) Then Return False
            Dim parsed As SKColor
            If Not SKColor.TryParse(text.Trim(), parsed) Then Return False
            color = parsed.WithAlpha(255)
            Return True
        End Function

        ''' <summary>Stores a background color; Nothing goes back to the interface theme (and drops a gradient).</summary>
        Public Shared Sub SetColor(options As IFlowsheetOptions, color As SKColor?)
            If options Is Nothing Then Return
            options.FlowsheetBackgroundColor = If(color.HasValue, ToText(color.Value), "")
            If Not color.HasValue Then options.FlowsheetBackgroundColor2 = ""
        End Sub

        ''' <summary>
        ''' The color at the bottom of a vertical gradient that starts from the background color at the top; False for a
        ''' plain color or the theme's background.
        ''' </summary>
        Public Shared Function TryGetGradientColor(options As IFlowsheetOptions, ByRef bottom As SKColor) As Boolean
            Dim top As SKColor
            If Not TryGetColor(options, top) Then Return False
            Dim text = options.FlowsheetBackgroundColor2
            If String.IsNullOrWhiteSpace(text) Then Return False
            Dim parsed As SKColor
            If Not SKColor.TryParse(text.Trim(), parsed) Then Return False
            bottom = parsed.WithAlpha(255)
            Return True
        End Function

        ''' <summary>Stores the bottom color of the gradient; Nothing makes the background a plain color.</summary>
        Public Shared Sub SetGradientColor(options As IFlowsheetOptions, bottom As SKColor?)
            If options Is Nothing Then Return
            options.FlowsheetBackgroundColor2 = If(bottom.HasValue, ToText(bottom.Value), "")
        End Sub

        ''' <summary>
        ''' True when light lines and text read better on a gradient than dark ones, judged on the mean luminance of its
        ''' two ends.
        ''' </summary>
        Public Shared Function IsDark(top As SKColor, bottom As SKColor) As Boolean
            Return (RelativeLuminance(top) + RelativeLuminance(bottom)) / 2.0 < 0.179
        End Function

        ''' <summary>
        ''' True when one end of a gradient is dark and the other light, so the lines and text cannot read well on both.
        ''' </summary>
        Public Shared Function ContrastDiffers(top As SKColor, bottom As SKColor) As Boolean
            Return IsDark(top) <> IsDark(bottom)
        End Function

        ''' <summary>The color halfway between the two ends, used where one color has to stand for the gradient.</summary>
        Public Shared Function Middle(top As SKColor, bottom As SKColor) As SKColor
            Return New SKColor(CByte((CInt(top.Red) + bottom.Red) \ 2), CByte((CInt(top.Green) + bottom.Green) \ 2),
                               CByte((CInt(top.Blue) + bottom.Blue) \ 2))
        End Function

        ''' <summary>#RRGGBB.</summary>
        Public Shared Function ToText(color As SKColor) As String
            Return String.Format("#{0:X2}{1:X2}{2:X2}", color.Red, color.Green, color.Blue)
        End Function

        ''' <summary>
        ''' True when light lines and text read better on the color than dark ones: its relative luminance is
        ''' below 0.179, where the contrast against black and against white is the same.
        ''' </summary>
        Public Shared Function IsDark(color As SKColor) As Boolean
            Return RelativeLuminance(color) < 0.179
        End Function

        Private Shared Function RelativeLuminance(color As SKColor) As Double
            Dim lin = Function(c As Byte) As Double
                          Dim v = c / 255.0
                          Return If(v <= 0.04045, v / 12.92, Math.Pow((v + 0.055) / 1.055, 2.4))
                      End Function
            Return 0.2126 * lin(color.Red) + 0.7152 * lin(color.Green) + 0.0722 * lin(color.Blue)
        End Function

    End Class

End Namespace
