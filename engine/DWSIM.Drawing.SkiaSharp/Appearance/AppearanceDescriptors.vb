'    Appearance properties of flowsheet graphic objects, shared by the classic and the cross-platform editors
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
Imports System.Globalization
Imports System.Linq
Imports System.Reflection
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums.GraphicObjects

Namespace Appearance

    ''' <summary>Section of the appearance editor a property is shown in, in display order.</summary>
    Public Enum AppearanceGroup
        Line = 0
        Fill = 1
        Text = 2
        ShapeAndSize = 3
        Orientation = 4
        Position = 5
    End Enum

    ''' <summary>How a property is stored and therefore which control edits it.</summary>
    Public Enum AppearancePropertyKind
        ''' <summary>SkiaSharp.SKColor.</summary>
        Color
        [Boolean]
        [Double]
        [Integer]
        ''' <summary>An enumeration; the choices are in <see cref="AppearancePropertyDescriptor.Options"/>.</summary>
        [Enum]
        Text
    End Enum

    ''' <summary>The kind of graphic object, which decides the properties the editor shows.</summary>
    Public Enum AppearanceTarget
        ''' <summary>Unit operations, streams, logical blocks, controllers and indicators.</summary>
        FlowsheetObject
        Text
        Rectangle
        Table
        Button
        ''' <summary>Pictures, charts, animations and formatted text: only size and position.</summary>
        Other
    End Enum

    ''' <summary>
    ''' One editable appearance property of a graphic object: the name of the .NET property, how it is
    ''' shown and explained, its limits, and the objects it applies to. Getting and setting go through
    ''' the descriptor so the side effects (a color that only shows with custom colors on, a locked
    ''' position) are handled the same way in both user interfaces.
    ''' </summary>
    Public NotInheritable Class AppearancePropertyDescriptor

        ''' <summary>Name of the .NET property on the graphic object.</summary>
        Public ReadOnly Property Key As String
        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property Group As AppearanceGroup
        Public ReadOnly Property Kind As AppearancePropertyKind
        ''' <summary>Plain-language explanation shown as tooltip and help text.</summary>
        Public ReadOnly Property Help As String
        Public ReadOnly Property Minimum As Double
        Public ReadOnly Property Maximum As Double
        ''' <summary>Decimal places shown for a Double.</summary>
        Public ReadOnly Property DecimalPlaces As Integer
        ''' <summary>Values offered as one-click choices (rotation: 0, 90, 180, 270).</summary>
        Public ReadOnly Property Presets As Double()
        ''' <summary>For an Enum: each value with the text shown for it.</summary>
        Public ReadOnly Property Options As List(Of KeyValuePair(Of Object, String))
        ''' <summary>True for X and Y, which cannot be changed while the position is locked.</summary>
        Public ReadOnly Property IsPosition As Boolean
        ''' <summary>True when "Reset to defaults" copies the value from a new object of the same type.</summary>
        Public ReadOnly Property IncludeInReset As Boolean
        ''' <summary>Text edited over several lines.</summary>
        Public ReadOnly Property Multiline As Boolean

        Private ReadOnly _targets As AppearanceTarget()

        Friend Sub New(key As String, displayName As String, group As AppearanceGroup, kind As AppearancePropertyKind,
                       help As String, targets As AppearanceTarget(),
                       Optional minimum As Double = 0.0, Optional maximum As Double = 0.0,
                       Optional decimalPlaces As Integer = 0, Optional presets As Double() = Nothing,
                       Optional options As List(Of KeyValuePair(Of Object, String)) = Nothing,
                       Optional isPosition As Boolean = False, Optional includeInReset As Boolean = True,
                       Optional multiline As Boolean = False)
            Me.Key = key
            Me.DisplayName = displayName
            Me.Group = group
            Me.Kind = kind
            Me.Help = If(help, "")
            _targets = targets
            Me.Minimum = minimum
            Me.Maximum = maximum
            Me.DecimalPlaces = decimalPlaces
            Me.Presets = If(presets, New Double() {})
            Me.Options = If(options, New List(Of KeyValuePair(Of Object, String)))
            Me.IsPosition = isPosition
            Me.IncludeInReset = includeInReset
            Me.Multiline = multiline
        End Sub

        Private Function GetInfo(gobj As IGraphicObject) As PropertyInfo
            If gobj Is Nothing Then Return Nothing
            Dim info = gobj.GetType().GetProperty(Key, BindingFlags.Public Or BindingFlags.Instance)
            If info Is Nothing OrElse Not info.CanRead OrElse Not info.CanWrite Then Return Nothing
            Return info
        End Function

        ''' <summary>True when the object is of a kind this property is shown for and has the property.</summary>
        Public Function AppliesTo(gobj As IGraphicObject) As Boolean
            If gobj Is Nothing OrElse gobj.IsConnector Then Return False
            If Not _targets.Contains(AppearanceDescriptors.GetTarget(gobj)) Then Return False
            Return GetInfo(gobj) IsNot Nothing
        End Function

        ''' <summary>The current value: SKColor, Boolean, Double, Integer, the enum value, or String.</summary>
        Public Function GetValue(gobj As IGraphicObject) As Object
            Dim info = GetInfo(gobj)
            If info Is Nothing Then Return Nothing
            Dim raw = info.GetValue(gobj)
            Select Case Kind
                Case AppearancePropertyKind.Double
                    Return Convert.ToDouble(raw, CultureInfo.InvariantCulture)
                Case AppearancePropertyKind.Integer
                    Return CInt(Math.Round(Convert.ToDouble(raw, CultureInfo.InvariantCulture)))
                Case AppearancePropertyKind.Text
                    Return If(raw, "").ToString()
                Case Else
                    Return raw
            End Select
        End Function

        ''' <summary>
        ''' Writes a value. Numbers are clamped to the limits and converted to the property's own type.
        ''' A line color turns custom colors on, since the calculation status colors overwrite it otherwise.
        ''' X and Y are left alone while the object's position is locked. Returns True when the object changed.
        ''' </summary>
        Public Function SetValue(gobj As IGraphicObject, value As Object) As Boolean

            Dim info = GetInfo(gobj)
            If info Is Nothing OrElse value Is Nothing Then Return False
            If IsPosition AndAlso gobj.PositionLocked Then Return False

            Dim converted As Object
            Select Case Kind
                Case AppearancePropertyKind.Color
                    If TypeOf value Is SKColor Then
                        converted = value
                    Else
                        Dim c As SKColor
                        If Not SKColor.TryParse(value.ToString(), c) Then Return False
                        converted = c
                    End If
                Case AppearancePropertyKind.Boolean
                    converted = Convert.ToBoolean(value, CultureInfo.InvariantCulture)
                Case AppearancePropertyKind.Double, AppearancePropertyKind.Integer
                    Dim d = AppearanceDescriptors.ParseNumber(value)
                    If Not d.HasValue Then Return False
                    Dim v = d.Value
                    If Maximum > Minimum Then v = Math.Max(Minimum, Math.Min(Maximum, v))
                    If Kind = AppearancePropertyKind.Integer Then v = Math.Round(v)
                    converted = Convert.ChangeType(v, info.PropertyType, CultureInfo.InvariantCulture)
                Case AppearancePropertyKind.Enum
                    If value.GetType() Is info.PropertyType Then
                        converted = value
                    ElseIf TypeOf value Is String Then
                        converted = [Enum].Parse(info.PropertyType, value.ToString())
                    Else
                        converted = [Enum].ToObject(info.PropertyType, value)
                    End If
                Case Else
                    converted = value.ToString()
            End Select

            info.SetValue(gobj, converted)

            If Key = "LineColor" AndAlso AppearanceDescriptors.GetTarget(gobj) = AppearanceTarget.FlowsheetObject Then
                Dim ov = gobj.GetType().GetProperty("OverrideColors")
                If ov IsNot Nothing AndAlso ov.CanWrite Then ov.SetValue(gobj, True)
            End If

            Return True

        End Function

        ''' <summary>
        ''' The value shared by every object of the list that has the property, or Nothing when they differ
        ''' (or none has it).
        ''' </summary>
        Public Function GetCommonValue(objects As IEnumerable(Of IGraphicObject)) As Object
            Dim first As Object = Nothing
            Dim found As Boolean = False
            For Each g In objects
                If Not AppliesTo(g) Then Continue For
                Dim v = GetValue(g)
                If Not found Then
                    first = v
                    found = True
                ElseIf Not Object.Equals(v, first) Then
                    Return Nothing
                End If
            Next
            Return first
        End Function

        Public Overrides Function ToString() As String
            Return DisplayName
        End Function

    End Class

    ''' <summary>
    ''' The appearance properties of the flowsheet graphic objects. This table is the single source of the
    ''' classic and the cross-platform appearance editors: a property is added in one place only.
    ''' </summary>
    Public NotInheritable Class AppearanceDescriptors

        Private Sub New()
        End Sub

        Private Shared ReadOnly _all As List(Of AppearancePropertyDescriptor) = Build()

        ''' <summary>Every descriptor, in display order.</summary>
        Public Shared ReadOnly Property All As IReadOnlyList(Of AppearancePropertyDescriptor)
            Get
                Return _all
            End Get
        End Property

        ''' <summary>The descriptors shown for an object, in display order.</summary>
        Public Shared Function ForObject(gobj As IGraphicObject) As List(Of AppearancePropertyDescriptor)
            Return _all.Where(Function(d) d.AppliesTo(gobj)).ToList()
        End Function

        Public Shared Function Find(key As String) As AppearancePropertyDescriptor
            Return _all.FirstOrDefault(Function(d) d.Key = key)
        End Function

        Public Shared Function GroupDisplayName(group As AppearanceGroup) As String
            Select Case group
                Case AppearanceGroup.Line : Return "Line"
                Case AppearanceGroup.Fill : Return "Fill"
                Case AppearanceGroup.Text : Return "Text"
                Case AppearanceGroup.ShapeAndSize : Return "Shape and size"
                Case AppearanceGroup.Orientation : Return "Orientation"
                Case Else : Return "Position"
            End Select
        End Function

        ''' <summary>The kind of graphic object, which decides the properties shown for it.</summary>
        Public Shared Function GetTarget(gobj As IGraphicObject) As AppearanceTarget
            Select Case gobj.ObjectType
                Case ObjectType.GO_Text
                    Return AppearanceTarget.Text
                Case ObjectType.GO_Rectangle
                    Return AppearanceTarget.Rectangle
                Case ObjectType.GO_Table, ObjectType.GO_MasterTable, ObjectType.GO_SpreadsheetTable
                    Return AppearanceTarget.Table
                Case ObjectType.GO_Button
                    Return AppearanceTarget.Button
                Case ObjectType.GO_Image, ObjectType.GO_Chart, ObjectType.GO_Animation, ObjectType.GO_HTMLText,
                     ObjectType.GO_FloatingTable, ObjectType.Nenhum
                    Return AppearanceTarget.Other
                Case Else
                    If TypeOf gobj Is ShapeGraphic Then Return AppearanceTarget.FlowsheetObject
                    Return AppearanceTarget.Other
            End Select
        End Function

        ''' <summary>
        ''' Copies the values of a new object of the same type into the object, for every property that
        ''' takes part in a reset. Size, position, the lock and the text itself are kept.
        ''' </summary>
        Public Shared Sub ResetToDefaults(gobj As IGraphicObject)
            If gobj Is Nothing Then Return
            Dim fresh As IGraphicObject
            Try
                fresh = DirectCast(Activator.CreateInstance(gobj.GetType()), IGraphicObject)
            Catch
                Return
            End Try
            ' the status colors only come back with custom colors off, so that goes last
            Dim descs = ForObject(gobj).Where(Function(d) d.IncludeInReset).ToList()
            For Each d In descs.Where(Function(x) x.Key <> "OverrideColors")
                Dim info = gobj.GetType().GetProperty(d.Key)
                Try
                    info.SetValue(gobj, info.GetValue(fresh))
                Catch
                End Try
            Next
            Dim ov = descs.FirstOrDefault(Function(x) x.Key = "OverrideColors")
            If ov IsNot Nothing Then
                Dim info = gobj.GetType().GetProperty(ov.Key)
                info.SetValue(gobj, info.GetValue(fresh))
            End If
            Try
                gobj.PositionConnectors()
            Catch
            End Try
        End Sub

        ''' <summary>Reads a number written with either decimal separator.</summary>
        Public Shared Function ParseNumber(value As Object) As Double?
            If value Is Nothing Then Return Nothing
            If TypeOf value Is Double Then Return DirectCast(value, Double)
            If TypeOf value Is Single OrElse TypeOf value Is Integer OrElse TypeOf value Is Decimal OrElse TypeOf value Is Long Then
                Return Convert.ToDouble(value, CultureInfo.InvariantCulture)
            End If
            Dim text = value.ToString().Trim()
            Dim d As Double
            If Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, d) Then Return d
            If Double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, d) Then Return d
            ' a decimal comma typed under a culture that uses the point
            Dim swapped = text.Replace(",", ".")
            If swapped.IndexOf("."c) = swapped.LastIndexOf("."c) AndAlso
               Double.TryParse(swapped, NumberStyles.Float, CultureInfo.InvariantCulture, d) Then Return d
            Return Nothing
        End Function

        ''' <summary>"#AARRGGBB", the form both editors show and parse.</summary>
        Public Shared Function ColorToHex(c As SKColor) As String
            Return String.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.Alpha, c.Red, c.Green, c.Blue)
        End Function

        Private Shared Function Build() As List(Of AppearancePropertyDescriptor)

            Dim fo = AppearanceTarget.FlowsheetObject
            Dim txt = AppearanceTarget.Text
            Dim rect = AppearanceTarget.Rectangle
            Dim tbl = AppearanceTarget.Table
            Dim btn = AppearanceTarget.Button
            Dim oth = AppearanceTarget.Other

            Dim fontStyles As New List(Of KeyValuePair(Of Object, String)) From {
                New KeyValuePair(Of Object, String)(FontStyle.Regular, "Regular"),
                New KeyValuePair(Of Object, String)(FontStyle.Bold, "Bold"),
                New KeyValuePair(Of Object, String)(FontStyle.Italic, "Italic"),
                New KeyValuePair(Of Object, String)(FontStyle.BoldItalic, "Bold italic")}

            Dim L = AppearanceGroup.Line, F = AppearanceGroup.Fill, T = AppearanceGroup.Text
            Dim S = AppearanceGroup.ShapeAndSize, O = AppearanceGroup.Orientation, P = AppearanceGroup.Position
            Dim kC = AppearancePropertyKind.Color, kB = AppearancePropertyKind.Boolean, kD = AppearancePropertyKind.Double
            Dim kI = AppearancePropertyKind.Integer, kE = AppearancePropertyKind.Enum, kT = AppearancePropertyKind.Text

            Dim list As New List(Of AppearancePropertyDescriptor)

            ' Line
            list.Add(New AppearancePropertyDescriptor("OverrideColors", "Use custom colors", L, kB,
                "Use custom colors instead of the calculation status colors. When this is off, the outline follows the status of the object: blue when calculated, red when not calculated or in error, gray when inactive.",
                {fo}))
            list.Add(New AppearancePropertyDescriptor("LineColor", "Line color", L, kC,
                "Color of the outline and of the name label. It shows only with custom colors on, so choosing a color turns them on.",
                {fo}))
            list.Add(New AppearancePropertyDescriptor("LineColorDark", "Label color (dark theme)", L, kC,
                "Color of the name label when the dark theme is active.",
                {fo}))
            list.Add(New AppearancePropertyDescriptor("BorderColor", "Border color", L, kC,
                "Color of the table borders in the light theme.",
                {tbl}))
            list.Add(New AppearancePropertyDescriptor("BorderColorDark", "Border color (dark theme)", L, kC,
                "Color of the table borders when the dark theme is active.",
                {tbl}))
            list.Add(New AppearancePropertyDescriptor("LineWidth", "Border width", L, kI,
                "Thickness of the border, in pixels.",
                {rect}, 1, 20))
            list.Add(New AppearancePropertyDescriptor("RoundEdges", "Rounded corners", L, kB,
                "Draw the rectangle with rounded corners.",
                {rect}))

            ' Fill
            list.Add(New AppearancePropertyDescriptor("GradientMode", "Fill with a gradient", F, kB,
                "Fill the rectangle with a vertical gradient between the two gradient colors. When this is off, the fill color is used.",
                {rect}))
            list.Add(New AppearancePropertyDescriptor("FillColor", "Fill color", F, kC,
                "Color of the fill when the gradient is off. It is drawn semi-transparent.",
                {rect}))
            list.Add(New AppearancePropertyDescriptor("GradientColor1", "Gradient top color", F, kC,
                "Color at the top of the gradient fill.",
                {rect}))
            list.Add(New AppearancePropertyDescriptor("GradientColor2", "Gradient bottom color", F, kC,
                "Color at the bottom of the gradient fill. The border is drawn in this color too.",
                {rect}))
            list.Add(New AppearancePropertyDescriptor("Opacity", "Opacity", F, kI,
                "Opacity of the gradient fill and of the border, from 0 (transparent) to 255 (opaque).",
                {rect}, 0, 255))

            ' Text
            list.Add(New AppearancePropertyDescriptor("Text", "Text", T, kT,
                "Text shown on the flowsheet.",
                {txt, rect, btn}, includeInReset:=False, multiline:=True))
            list.Add(New AppearancePropertyDescriptor("Color", "Text color", T, kC,
                "Color of the text. In the dark theme the text is drawn in a light color so it stays readable.",
                {txt}))
            list.Add(New AppearancePropertyDescriptor("FontColor", "Text color", T, kC,
                "Color of the text written inside the rectangle.",
                {rect}))
            list.Add(New AppearancePropertyDescriptor("TextColor", "Text color", T, kC,
                "Color of the table text in the light theme.",
                {tbl}))
            list.Add(New AppearancePropertyDescriptor("TextColorDark", "Text color (dark theme)", T, kC,
                "Color of the table text when the dark theme is active.",
                {tbl}))
            list.Add(New AppearancePropertyDescriptor("Size", "Font size", T, kD,
                "Size of the text, in points.",
                {txt}, 4, 200, 1))
            list.Add(New AppearancePropertyDescriptor("FontSize", "Font size", T, kD,
                "Size of the text, in points. For a flowsheet object this is the size of its name label.",
                {fo, rect, tbl, btn}, 4, 72, 1))
            list.Add(New AppearancePropertyDescriptor("FontStyle", "Font style", T, kE,
                "Regular, bold, italic or bold italic text.",
                {fo, txt, tbl}, options:=fontStyles))
            list.Add(New AppearancePropertyDescriptor("Padding", "Cell padding", T, kI,
                "Space between the text and the cell borders, in pixels.",
                {tbl}, 0, 50))
            list.Add(New AppearancePropertyDescriptor("DrawLabel", "Show the name label", T, kB,
                "Write the name of the object below it.",
                {fo}))

            ' Shape and size
            list.Add(New AppearancePropertyDescriptor("Width", "Width", S, kI,
                "Width of the object, in pixels.",
                {fo, rect, btn, oth}, 5, 5000, includeInReset:=False))
            list.Add(New AppearancePropertyDescriptor("Height", "Height", S, kI,
                "Height of the object, in pixels.",
                {fo, rect, btn, oth}, 5, 5000, includeInReset:=False))

            ' Orientation
            list.Add(New AppearancePropertyDescriptor("Rotation", "Rotation (degrees)", O, kI,
                "Clockwise rotation of the object around its center. Use 0, 90, 180 or 270 for objects connected to streams, so the ports stay on the sides.",
                {fo, oth}, 0, 359, presets:=New Double() {0, 90, 180, 270}))
            list.Add(New AppearancePropertyDescriptor("FlippedH", "Flip horizontally", O, kB,
                "Mirror the object left to right, so the inlets are on the right.",
                {fo}))
            list.Add(New AppearancePropertyDescriptor("FlippedV", "Flip vertically", O, kB,
                "Mirror the object top to bottom.",
                {fo}))

            ' Position
            list.Add(New AppearancePropertyDescriptor("X", "X", P, kD,
                "Horizontal position of the top left corner, in flowsheet pixels. Panning and zooming the view change it.",
                {fo, txt, rect, tbl, btn, oth}, -1000000, 1000000, isPosition:=True, includeInReset:=False))
            list.Add(New AppearancePropertyDescriptor("Y", "Y", P, kD,
                "Vertical position of the top left corner, in flowsheet pixels. Panning and zooming the view change it.",
                {fo, txt, rect, tbl, btn, oth}, -1000000, 1000000, isPosition:=True, includeInReset:=False))
            list.Add(New AppearancePropertyDescriptor("PositionLocked", "Lock position", P, kB,
                "Keep the object where it is. Dragging, aligning, snapping to the grid, the automatic layouts and the arrow keys leave it in place. Size, rotation and colors can still be changed.",
                {fo, txt, rect, tbl, btn, oth}, includeInReset:=False))

            Return list

        End Function

    End Class

End Namespace
