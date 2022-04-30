Imports System.Globalization

Public Module General

    <System.Runtime.CompilerServices.Extension()>
    Public Sub OpenURL(url As String)

        Select Case Settings.RunningPlatform
            Case Settings.Platform.Windows
                Process.Start(url)
            Case Settings.Platform.Linux
                Process.Start("xdg-open", url)
            Case Settings.Platform.Mac
                Process.Start("open", url)
        End Select

    End Sub

    <System.Runtime.CompilerServices.Extension()>
    Public Function GetEnumNames(obj As Object) As List(Of String)

        If obj.GetType.BaseType Is GetType([Enum]) Then
            Return [Enum].GetNames(obj.GetType).ToList()
        Else
            Return New List(Of String)
        End If

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToEnum(Of T)(obj As Integer) As T

        Dim names = [Enum].GetNames(GetType(T))
        Dim values = New List(Of Integer)([Enum].GetValues(GetType(T)))
        Return [Enum].Parse(GetType(T), names(values.IndexOf(obj)))

    End Function


    <System.Runtime.CompilerServices.Extension()>
    Public Sub RemoveVariable(exobj As System.Dynamic.ExpandoObject, varname As String)
        Dim collection = DirectCast(exobj, IDictionary(Of String, Object))
        If collection.ContainsKey(varname) Then collection.Remove(varname)
    End Sub


    <System.Runtime.CompilerServices.Extension()>
    Public Function ReturnValidSets(data As Tuple(Of Double(), Double())) As Tuple(Of Double(), Double())

        Dim v1, v2 As New List(Of Double)
        For i As Integer = 0 To data.Item1.Count - 1
            If Not Double.IsNaN(data.Item1(i)) And Not Double.IsNaN(data.Item2(i)) Then
                v1.Add(data.Item1(i))
                v2.Add(data.Item2(i))
            End If
        Next

        Return New Tuple(Of Double(), Double())(v1.ToArray, v2.ToArray)

    End Function


    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleArray(al As ArrayList) As Double()

        Dim list As New List(Of Double)
        For Each item In al
            list.Add(Convert.ToDouble(item))
        Next
        Return list.ToArray()

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleList(al As ArrayList) As List(Of Double)

        Dim list As New List(Of Double)
        For Each item In al
            list.Add(Convert.ToDouble(item))
        Next
        Return list

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertFromSI(d As Double, units As String) As Double

        Return SystemsOfUnits.Converter.ConvertFromSI(units, d)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertToSI(d As Double, units As String) As Double

        Return SystemsOfUnits.Converter.ConvertToSI(units, d)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertUnits(d As Double, fromunits As String, tounits As String) As Double

        Return SystemsOfUnits.Converter.ConvertFromSI(tounits, SystemsOfUnits.Converter.ConvertToSI(fromunits, d))

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertUnits(vector As Double(), fromunits As String, tounits As String) As Double()

        Dim newvector As Double() = DirectCast(vector.Clone, Double())

        For i As Integer = 0 To vector.Length - 1
            newvector(i) = SystemsOfUnits.Converter.ConvertFromSI(tounits, SystemsOfUnits.Converter.ConvertToSI(fromunits, vector(i)))
        Next

        Return newvector

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertFromSI(vector As List(Of Double), units As String) As List(Of Double)

        Dim newvector As New List(Of Double)

        For i As Integer = 0 To vector.Count - 1
            newvector.Add(SystemsOfUnits.Converter.ConvertFromSI(units, vector(i)))
        Next

        Return newvector

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertToSI(vector As List(Of Double), units As String) As List(Of Double)

        Dim newvector As New List(Of Double)

        For i As Integer = 0 To vector.Count - 1
            newvector.Add(SystemsOfUnits.Converter.ConvertToSI(units, vector(i)))
        Next

        Return newvector

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertUnits(vector As List(Of Double), fromunits As String, tounits As String) As List(Of Double)

        Dim newvector As New List(Of Double)

        For i As Integer = 0 To vector.Count - 1
            newvector.Add(SystemsOfUnits.Converter.ConvertFromSI(tounits, SystemsOfUnits.Converter.ConvertToSI(fromunits, vector(i))))
        Next

        Return newvector

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsValidDouble(obj As Object) As Boolean

        Return Double.TryParse(obj.ToString, New Double)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsValidDouble(str As String) As Boolean

        Return Double.TryParse(str, New Double)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToString(sourcearray As String(), ci As CultureInfo) As String

        Dim sb As String = ""

        If Not sourcearray Is Nothing Then
            If sourcearray.Length > 0 Then

                For Each obj As Object In sourcearray
                    If TypeOf obj Is Double Then
                        sb += Double.Parse(obj.ToString()).ToString(ci) + ","
                    Else
                        sb += obj.ToString + ","
                    End If
                Next

                sb = sb.Remove(sb.Length - 1)

            End If
        End If

        Return sb

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToArray(ByVal text As String, ci As CultureInfo, arraytype As Type) As Array

#If WINE32 Then
        If Not text Is Nothing Then
            Dim values() As String = text.Split(",")
            If arraytype Is GetType(Double) Then
                Dim myarr As New List(Of Double)
                For Each s As String In values
                    If Double.TryParse(s, New Double) Then
                        myarr.Add(Double.Parse(s, ci))
                    Else
                        myarr.Add(0.0)
                    End If
                Next
                Return myarr.ToArray()
            ElseIf arraytype Is GetType(Integer) Then
                Dim myarr As New List(Of Integer)
                For Each s As String In values
                    If Integer.TryParse(s, New Integer) Then
                        myarr.Add(Integer.Parse(s, ci))
                    Else
                        myarr.Add(0)
                    End If
                Next
                Return myarr.ToArray()
            ElseIf arraytype Is GetType(String) Then
                Dim myarr As New List(Of String)
                For Each s As String In values
                    myarr.Add(s)
                Next
                Return myarr.ToArray()
            Else
                Return New ArrayList().ToArray(arraytype)
            End If
        Else
            Return New ArrayList().ToArray(arraytype)
        End If
#Else
        If Not text Is Nothing Then
            Dim values() As String = text.Split(",")
            Dim myarr As New ArrayList
            For Each s As String In values
                If Double.TryParse(s, New Double) Then
                    myarr.Add(Double.Parse(s, ci))
                Else
                    myarr.Add(s)
                End If
            Next
            Return myarr.ToArray(arraytype)
        Else
            Return New ArrayList().ToArray(arraytype)
        End If
#End If

    End Function

    '<System.Runtime.CompilerServices.Extension()> _
    'Public Function ToDTPoint(pt As System.Drawing.Point) As DrawingTools.Point
    '    Return New DrawingTools.Point(pt.X, pt.Y)
    'End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToArrayString(vector As Double()) As String

        Dim retstr As String = "{ "
        If vector IsNot Nothing Then
            For Each d In vector
                retstr += d.ToString + ", "
            Next
        End If
        retstr.TrimEnd(",")
        retstr += "}"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToArrayString(vector As Double(), ids As String(), nf As String) As String

        Dim retstr As String = ""
        If vector IsNot Nothing Then
            Dim i As Integer = 0
            For Each d In vector
                retstr += ids(i) + ": " + d.ToString(nf) + ", "
                i += 1
            Next
        End If
        retstr.TrimEnd(",")

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToMathArrayString(vector As Double()) As String

        Dim retstr As String = "<math_inline>\left[{\begin{array}{}"
        If vector IsNot Nothing Then
            For Each d In vector
                retstr += d.ToString + " & "
            Next
        End If
        retstr.TrimEnd(" ")
        retstr.TrimEnd("&")
        retstr += "\end{array}}\right]</math_inline>"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToMathArrayString(vector As String()) As String

        Dim retstr As String = "<math_inline>\left[{\begin{array}{}"
        For Each s In vector
            retstr += s + " & "
        Next
        retstr.TrimEnd(" ")
        retstr.TrimEnd("&")
        retstr += "\end{array}}\right]</math_inline>"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToMathArrayString(vector As Double(,)) As String

        Dim i, j, n, m As Integer
        n = vector.GetUpperBound(0)
        m = vector.GetUpperBound(1)
        Dim retstr As String = "<math_inline>\left[{\begin{array}{}"

        For i = 0 To n
            For j = 0 To m
                retstr += vector(i, j).ToString + " & "
            Next
            retstr.TrimEnd(" ")
            retstr.TrimEnd("&")
            retstr += "\\"
        Next
        retstr.TrimEnd("\")
        retstr += "\end{array}}\right]</math_inline>"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToMathArrayString(vector As Double()()) As String

        Dim i, j, n, m As Integer
        n = vector.GetUpperBound(0)
        m = vector(0).GetUpperBound(0)

        Dim retstr As String = "<math_inline>\left[{\begin{array}{}"

        For i = 0 To n
            For j = 0 To m
                retstr += vector(i)(j).ToString + " & "
            Next
            retstr.TrimEnd(" ")
            retstr.TrimEnd("&")
            retstr += "\\"
        Next
        retstr.TrimEnd("\")
        retstr += "\end{array}}\right]</math_inline>"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleWithSeparator(s As String, sep As String) As Double
        Dim nstring As String = s.Replace(sep, ".")
        If Double.TryParse(nstring, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, New Double) Then
            Return Double.Parse(nstring, NumberStyles.Any, Globalization.CultureInfo.InvariantCulture)
        Else
            Return 0.0#
        End If
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleFromInvariant(s As String) As Double

        Dim ci As CultureInfo = CultureInfo.InvariantCulture

        Return Double.Parse(s.Replace(",", "."), NumberStyles.Any, ci)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleFromCurrent(s As String) As Double

        Dim ci As CultureInfo = CultureInfo.CurrentCulture

        If Double.TryParse(s, NumberStyles.Any, ci, New Double) Then
            Return Double.Parse(s, NumberStyles.Any, ci)
        Else
            Return 0.0
        End If

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToArrayString(vector As Double(), ByVal ci As System.Globalization.CultureInfo, ByVal nf As String) As String

        If vector.Length > 1 Then

            Dim retstr As String = "{"
            For Each d As Double In vector
                retstr += d.ToString(nf, ci) + "; "
            Next
            retstr = retstr.TrimEnd(New Char() {";"c, " "c})
            retstr += "}"

            Return retstr

        ElseIf vector.Length > 0 Then

            Return vector(0).ToString(nf, ci)

        Else

            Return ""

        End If

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleArray(text As String, ByVal ci As System.Globalization.CultureInfo) As Double()

        Dim numbers As String() = text.Trim(New Char() {"{"c, "}"c}).Split(";"c)

        Dim doubles As New List(Of Double)

        For Each n As String In numbers
            If n <> "" Then doubles.Add(Convert.ToDouble(n, ci))
        Next

        Return doubles.ToArray

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToArrayString(vector As String()) As String

        Dim retstr As String = "{ "
        For Each s In vector
            retstr += s + ", "
        Next
        retstr.TrimEnd(",")
        retstr += "}"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToArrayString(vector As Object()) As String

        Dim retstr As String = "{ "
        For Each d In vector
            If Not d Is Nothing Then retstr += d.ToString + ", "
        Next
        retstr.TrimEnd(",")
        retstr += "}"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToArrayString(vector As Array) As String

        Dim retstr As String = "{ "
        For Each d In vector
            If Not d Is Nothing Then retstr += d.ToString + ", "
        Next
        retstr.TrimEnd(",")
        retstr += "}"

        Return retstr

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsValid(d As Double) As Boolean
        If Double.IsNaN(d) Or Double.IsInfinity(d) Then Return False Else Return True
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsValid(d As Nullable(Of Double)) As Boolean
        If Double.IsNaN(d.GetValueOrDefault) Or Double.IsInfinity(d.GetValueOrDefault) Then Return False Else Return True
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsValid(d As Double()) As Boolean

        Return d.Where(Function(d0) Double.IsNaN(d0) Or Double.IsInfinity(d0)).Count = 0

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsPositive(d As Double) As Boolean
        If d.IsValid() Then
            If d > 0.0# Then Return True Else Return False
        Else
            Throw New ArgumentException("invalid double")
        End If
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsPositive(d As Nullable(Of Double)) As Boolean
        If d.GetValueOrDefault.IsValid() Then
            If d.GetValueOrDefault > 0.0# Then Return True Else Return False
        Else
            Throw New ArgumentException("invalid double")
        End If
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsNegative(d As Double) As Boolean
        If d.IsValid() Then
            If d < 0.0# Then Return True Else Return False
        Else
            Throw New ArgumentException("invalid double")
        End If
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function IsNegative(d As Nullable(Of Double)) As Boolean
        If d.GetValueOrDefault.IsValid() Then
            If d.GetValueOrDefault < 0.0# Then Return True Else Return False
        Else
            Throw New ArgumentException("invalid double")
        End If
    End Function

    ''' <summary>
    ''' Alternative implementation for the Exponential (Exp) function.
    ''' </summary>
    ''' <param name="val"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    <System.Runtime.CompilerServices.Extension()> Public Function ExpY(val As Double) As Double
        Dim tmp As Long = CLng(1512775 * val + 1072632447)
        Return BitConverter.Int64BitsToDouble(tmp << 32)
    End Function


    ''' <summary>
    ''' Converts a two-dimensional array to a jagged array.
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="twoDimensionalArray"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    <System.Runtime.CompilerServices.Extension> Public Function ToJaggedArray(Of T)(twoDimensionalArray As T(,)) As T()()

        Dim rowsFirstIndex As Integer = twoDimensionalArray.GetLowerBound(0)
        Dim rowsLastIndex As Integer = twoDimensionalArray.GetUpperBound(0)
        Dim numberOfRows As Integer = rowsLastIndex + 1

        Dim columnsFirstIndex As Integer = twoDimensionalArray.GetLowerBound(1)
        Dim columnsLastIndex As Integer = twoDimensionalArray.GetUpperBound(1)
        Dim numberOfColumns As Integer = columnsLastIndex + 1

        Dim jaggedArray As T()() = New T(numberOfRows - 1)() {}
        For i As Integer = rowsFirstIndex To rowsLastIndex
            jaggedArray(i) = New T(numberOfColumns - 1) {}

            For j As Integer = columnsFirstIndex To columnsLastIndex
                jaggedArray(i)(j) = twoDimensionalArray(i, j)
            Next
        Next
        Return jaggedArray

    End Function

    ''' <summary>
    ''' Converts a jagged array to a two-dimensional array.
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="jaggedArray"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    <System.Runtime.CompilerServices.Extension> Public Function FromJaggedArray(Of T)(jaggedArray As T()()) As T(,)

        Dim rowsFirstIndex As Integer = jaggedArray.GetLowerBound(0)
        Dim rowsLastIndex As Integer = jaggedArray.GetUpperBound(0)
        Dim numberOfRows As Integer = rowsLastIndex + 1

        Dim columnsFirstIndex As Integer = jaggedArray(0).GetLowerBound(0)
        Dim columnsLastIndex As Integer = jaggedArray(0).GetUpperBound(0)
        Dim numberOfColumns As Integer = columnsLastIndex + 1

        Dim twoDimensionalArray As T(,) = New T(numberOfRows - 1, numberOfColumns - 1) {}
        For i As Integer = rowsFirstIndex To rowsLastIndex
            For j As Integer = columnsFirstIndex To columnsLastIndex
                twoDimensionalArray(i, j) = jaggedArray(i)(j)
            Next
        Next
        Return twoDimensionalArray

    End Function

    <System.Runtime.CompilerServices.Extension> Function Encrypt(text As String, passphrase As String) As String

        Return EncryptString.StringCipher.Encrypt(text, passphrase)

    End Function

    <System.Runtime.CompilerServices.Extension> Function Decrypt(text As String, passphrase As String) As String

        Return EncryptString.StringCipher.Decrypt(text, passphrase)

    End Function

End Module

