Imports System.Globalization
Imports DWSIM.Interfaces

Namespace CompoundEditing

    ''' <summary>One property of two compounds side by side.</summary>
    Public Class CompoundDiffEntry
        Public Property Key As String
        Public Property DisplayName As String
        Public Property Group As CompoundPropertyGroup
        ''' <summary>The block that owns the key, Nothing for plain scalars.</summary>
        Public Property Block As TemperatureDependentBlock
        Public Property ValueA As Object
        Public Property ValueB As Object
        Public Property Differs As Boolean
        ''' <summary>What changed, in words ("3 of 25 points differ", "Elements: C 14.08 -> 15.9").</summary>
        Public Property Detail As String = ""
        ''' <summary>True for the synthetic per-block curve comparison.</summary>
        Public ReadOnly Property IsCurve As Boolean
            Get
                Return Key IsNot Nothing AndAlso Key.EndsWith(".Curve")
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Compares two compounds property by property, tolerant to floating-point noise, so the editor can
    ''' show "what is in the simulation" next to "what is in the file" and highlight the differences.
    ''' </summary>
    Public Module CompoundDiff

        ''' <summary>Every property (scalars, block keys, one curve entry per block); the UI filters on Differs.</summary>
        Public Function Compare(a As ICompoundConstantProperties, b As ICompoundConstantProperties,
                                Optional relTol As Double = 0.000000001, Optional absTol As Double = 0.000000000001) As List(Of CompoundDiffEntry)

            Dim entries As New List(Of CompoundDiffEntry)

            For Each d In CompoundPropertyDescriptors.Scalars
                entries.Add(Entry(d, Nothing, a, b, relTol, absTol))
            Next

            For Each blk In CompoundPropertyDescriptors.Blocks
                For Each k In blk.Keys()
                    entries.Add(Entry(CompoundPropertyDescriptors.ByKey(k), blk, a, b, relTol, absTol))
                Next
                Dim dev = CompareCurve(a, b, blk)
                Dim e As New CompoundDiffEntry With {
                    .Key = blk.Key & ".Curve",
                    .DisplayName = blk.DisplayName & " (curve)",
                    .Group = CompoundPropertyGroup.Constants,
                    .Block = blk}
                If Double.IsNaN(dev) Then
                    e.Differs = False
                    e.Detail = "The curve could not be evaluated for both compounds."
                Else
                    e.Differs = dev > 0.000001
                    e.Detail = If(e.Differs,
                                  "The curves differ by up to " & (dev * 100.0).ToString("G3", CultureInfo.InvariantCulture) & " % over the common range.",
                                  "The curves agree within " & Math.Max(dev * 100.0, 0.0).ToString("G2", CultureInfo.InvariantCulture) & " %.")
                End If
                entries.Add(e)
            Next

            Return entries

        End Function

        Private Function Entry(d As CompoundPropertyDescriptor, blk As TemperatureDependentBlock,
                               a As ICompoundConstantProperties, b As ICompoundConstantProperties,
                               relTol As Double, absTol As Double) As CompoundDiffEntry
            Dim e As New CompoundDiffEntry With {.Key = d.Key, .DisplayName = d.DisplayName, .Group = d.Group, .Block = blk}
            Try
                e.ValueA = d.GetValue(a)
                e.ValueB = d.GetValue(b)
                Dim detail As String = ""
                e.Differs = Not AreEqual(d, blk, e.ValueA, e.ValueB, relTol, absTol, detail)
                e.Detail = detail
            Catch ex As Exception
                e.Differs = False
                e.Detail = "Could not compare: " & ex.Message
            End Try
            Return e
        End Function

        Private Function AreEqual(d As CompoundPropertyDescriptor, blk As TemperatureDependentBlock, va As Object, vb As Object,
                                  relTol As Double, absTol As Double, ByRef detail As String) As Boolean
            Select Case d.Kind
                Case CompoundPropertyKind.Double, CompoundPropertyKind.NullableDouble, CompoundPropertyKind.Integer
                    Return NumbersEqual(ToDouble(va), ToDouble(vb), relTol, absTol)
                Case CompoundPropertyKind.Boolean
                    Return CBool(If(va, False)) = CBool(If(vb, False))
                Case CompoundPropertyKind.Text
                    Dim sa = If(va Is Nothing, "", va.ToString().Trim())
                    Dim sb = If(vb Is Nothing, "", vb.ToString().Trim())
                    ' "" and "0" both mean "estimate with Lee-Kesler" for the vapor pressure
                    If blk IsNot Nothing AndAlso blk.Key = "VaporPressure" AndAlso d.Key = blk.EquationKey Then
                        If CompoundEquationCatalog.IsNotDefined(sa) AndAlso CompoundEquationCatalog.IsNotDefined(sb) Then Return True
                    End If
                    Return String.Equals(sa, sb, StringComparison.Ordinal)
                Case CompoundPropertyKind.Elements, CompoundPropertyKind.Groups
                    Return ListsEqual(TryCast(va, SortedList), TryCast(vb, SortedList), relTol, absTol, detail)
                Case CompoundPropertyKind.Tabular
                    Return TabularEqual(TryCast(va, ITabularData), TryCast(vb, ITabularData), relTol, absTol, detail)
                Case Else
                    Return Object.Equals(va, vb)
            End Select
        End Function

        Private Function NumbersEqual(x As Double, y As Double, relTol As Double, absTol As Double) As Boolean
            If Double.IsNaN(x) AndAlso Double.IsNaN(y) Then Return True
            If Double.IsNaN(x) OrElse Double.IsNaN(y) Then Return False
            Return Math.Abs(x - y) <= absTol + relTol * Math.Max(Math.Abs(x), Math.Abs(y))
        End Function

        ''' <summary>Nothing counts as 0 (the XML round-trip maps one onto the other); strings are parsed invariantly.</summary>
        Public Function ToDouble(v As Object) As Double
            If v Is Nothing Then Return 0.0
            If TypeOf v Is Double Then Return CDbl(v)
            If TypeOf v Is String Then
                Dim d As Double
                If Double.TryParse(CStr(v), NumberStyles.Float, CultureInfo.InvariantCulture, d) Then Return d
                If Double.TryParse(CStr(v), NumberStyles.Float, CultureInfo.CurrentCulture, d) Then Return d
                Return Double.NaN
            End If
            Try
                Return Convert.ToDouble(v, CultureInfo.InvariantCulture)
            Catch
                Return Double.NaN
            End Try
        End Function

        Private Function ListsEqual(la As SortedList, lb As SortedList, relTol As Double, absTol As Double, ByRef detail As String) As Boolean
            Dim da = ToDictionary(la), db = ToDictionary(lb)
            Dim changes As New List(Of String)
            For Each kv In da
                If Not db.ContainsKey(kv.Key) Then
                    changes.Add(kv.Key & " removed")
                ElseIf Not NumbersEqual(kv.Value, db(kv.Key), relTol, absTol) Then
                    changes.Add(kv.Key & " " & kv.Value.ToString("G5", CultureInfo.InvariantCulture) & " -> " & db(kv.Key).ToString("G5", CultureInfo.InvariantCulture))
                End If
            Next
            For Each kv In db
                If Not da.ContainsKey(kv.Key) Then changes.Add(kv.Key & " added")
            Next
            detail = String.Join(", ", changes)
            Return changes.Count = 0
        End Function

        Private Function ToDictionary(l As SortedList) As Dictionary(Of String, Double)
            Dim d As New Dictionary(Of String, Double)
            If l Is Nothing Then Return d
            For Each k In l.Keys
                d(k.ToString()) = ToDouble(l(k))
            Next
            Return d
        End Function

        Private Function TabularEqual(ta As ITabularData, tb As ITabularData, relTol As Double, absTol As Double, ByRef detail As String) As Boolean
            Dim na = If(ta Is Nothing OrElse ta.XData Is Nothing, 0, ta.XData.Count)
            Dim nb = If(tb Is Nothing OrElse tb.XData Is Nothing, 0, tb.XData.Count)
            If na = 0 AndAlso nb = 0 Then Return True
            If na <> nb Then
                detail = na & " points vs " & nb & " points"
                Return False
            End If
            For Each pair In {New With {.a = ta.XUnit, .b = tb.XUnit, .n = "x unit"}, New With {.a = ta.YUnit, .b = tb.YUnit, .n = "y unit"},
                              New With {.a = ta.XName, .b = tb.XName, .n = "x name"}, New With {.a = ta.YName, .b = tb.YName, .n = "y name"},
                              New With {.a = ta.Source, .b = tb.Source, .n = "source"}}
                If Not String.Equals(If(pair.a, "").Trim(), If(pair.b, "").Trim(), StringComparison.Ordinal) Then
                    detail = pair.n & " differs"
                    Return False
                End If
            Next
            Dim differing = 0
            For i = 0 To na - 1
                If Not NumbersEqual(ta.XData(i), tb.XData(i), relTol, absTol) OrElse Not NumbersEqual(ta.YData(i), tb.YData(i), relTol, absTol) Then differing += 1
            Next
            If differing > 0 Then
                detail = differing & " of " & na & " points differ"
                Return False
            End If
            Return True
        End Function

        ''' <summary>
        ''' Largest relative deviation between the two compounds' curves for a block, sampled over the
        ''' range both are valid on (the intersection, so neither is pushed past its own limits, where
        ''' the estimation correlations cut off). NaN when neither can be evaluated. Lets the UI say "the
        ''' coefficients differ but the curves agree", which is what matters to the simulation.
        ''' </summary>
        Public Function CompareCurve(a As ICompoundConstantProperties, b As ICompoundConstantProperties, blk As TemperatureDependentBlock, Optional n As Integer = 51) As Double
            Dim ra = blk.DefaultRange(a), rb = blk.DefaultRange(b)
            Dim lo = Math.Max(ra.Tmin, rb.Tmin), hi = Math.Min(ra.Tmax, rb.Tmax)
            If hi <= lo OrElse n < 2 Then Return Double.NaN
            Dim worst = Double.NaN
            For i = 0 To n - 1
                Dim t = lo + (hi - lo) * i / (n - 1)
                Dim ya, yb As Double
                Try
                    ya = blk.Evaluator(a, t)
                    yb = blk.Evaluator(b, t)
                Catch
                    Continue For
                End Try
                If Double.IsNaN(ya) OrElse Double.IsNaN(yb) OrElse Double.IsInfinity(ya) OrElse Double.IsInfinity(yb) Then Continue For
                Dim scale = Math.Max(Math.Max(Math.Abs(ya), Math.Abs(yb)), 0.000000000001)
                Dim dev = Math.Abs(ya - yb) / scale
                If Double.IsNaN(worst) OrElse dev > worst Then worst = dev
            Next
            Return worst
        End Function

        ''' <summary>Text for a diff cell in the display unit of the descriptor.</summary>
        Public Function FormatValue(entry As CompoundDiffEntry, su As IUnitsOfMeasure, Optional numberFormat As String = "G6") As String
            Return FormatValue(entry.Key, entry.ValueA, su, numberFormat)
        End Function

        Public Function FormatValue(key As String, value As Object, su As IUnitsOfMeasure, Optional numberFormat As String = "G6") As String
            If value Is Nothing Then Return ""
            Dim d As CompoundPropertyDescriptor = Nothing
            Try
                d = CompoundPropertyDescriptors.ByKey(key)
            Catch
            End Try
            If d Is Nothing Then Return value.ToString()
            Select Case d.Kind
                Case CompoundPropertyKind.Double, CompoundPropertyKind.NullableDouble
                    Dim x = ToDouble(value)
                    If d.UnitSelector IsNot Nothing AndAlso su IsNot Nothing Then
                        x = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(d.DisplayUnit(su), x)
                    End If
                    Return x.ToString(numberFormat, CultureInfo.InvariantCulture)
                Case CompoundPropertyKind.Elements, CompoundPropertyKind.Groups
                    Dim l = TryCast(value, SortedList)
                    If l Is Nothing OrElse l.Count = 0 Then Return "(none)"
                    Dim parts As New List(Of String)
                    For Each k In l.Keys
                        parts.Add(k.ToString() & " " & ToDouble(l(k)).ToString("G5", CultureInfo.InvariantCulture))
                    Next
                    Return String.Join(", ", parts)
                Case CompoundPropertyKind.Tabular
                    Dim t = TryCast(value, ITabularData)
                    If t Is Nothing OrElse t.XData Is Nothing OrElse t.XData.Count = 0 Then Return "(no data)"
                    Return t.XData.Count & " points"
                Case Else
                    Return value.ToString()
            End Select
        End Function

    End Module

End Namespace
