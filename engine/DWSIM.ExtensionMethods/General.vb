Imports System.Globalization
Imports DWSIM.Interfaces.Enums
Imports System.Linq
Imports System.IO
Imports System.IO.Compression
Imports System.Text

Public Module General

    <System.Runtime.CompilerServices.Extension()>
    Public Function GetValueOrDefault(Of TKey, TValue)(ByVal dict As IDictionary(Of TKey, TValue), ByVal key As TKey, defval As TValue) As TValue
        Dim val As TValue
        If dict.TryGetValue(key, val) Then
            Return val
        End If
        Return defval
    End Function

    ''' <summary>
    ''' Compresses the string: gzip, with the uncompressed byte count in the first four bytes,
    ''' base64 encoded.
    ''' </summary>
    <System.Runtime.CompilerServices.Extension()>
    Public Function Compress(ByVal text As String) As String

        Dim buffer0() As Byte = Encoding.UTF8.GetBytes(text)

        Using memoryStream As New MemoryStream()

            ' The compressed bytes are only all there once the deflate stream is closed: it
            ' buffers, and writes its last block and the gzip trailer on dispose. Reading the
            ' MemoryStream while the GZipStream was still open, which is what this used to do,
            ' read a length of zero and produced a payload holding nothing but the length prefix.
            Using gZipStream As New GZipStream(memoryStream, CompressionMode.Compress, leaveOpen:=True)
                gZipStream.Write(buffer0, 0, buffer0.Length)
            End Using

            Dim compressedData() As Byte = memoryStream.ToArray()
            Dim gZipBuffer(compressedData.Length + 4 - 1) As Byte

            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length)
            Buffer.BlockCopy(BitConverter.GetBytes(buffer0.Length), 0, gZipBuffer, 0, 4)

            Return Convert.ToBase64String(gZipBuffer)

        End Using

    End Function

    ''' <summary>
    ''' Decompresses a string produced by <see cref="Compress"/>.
    ''' </summary>
    ''' <param name="compressedText">The compressed text.</param>
    <System.Runtime.CompilerServices.Extension()>
    Public Function Decompress(ByVal compressedText As String) As String

        Dim gZipBuffer() As Byte = Convert.FromBase64String(compressedText)
        Dim dataLength As Integer = BitConverter.ToInt32(gZipBuffer, 0)

        If dataLength <= 0 Then Return ""

        Dim buffer(dataLength - 1) As Byte

        Using memoryStream As New MemoryStream(gZipBuffer, 4, gZipBuffer.Length - 4)
            Using gZipStream As New GZipStream(memoryStream, CompressionMode.Decompress)

                ' Read hands back what it has, not what it was asked for. A single call left the
                ' rest of the buffer as zeros, and a run of NUL is what XDocument.Parse reported
                ' as an invalid character at line 1, position 1.
                Dim read As Integer = 0

                While read < dataLength
                    Dim n As Integer = gZipStream.Read(buffer, read, dataLength - read)
                    If n <= 0 Then Exit While
                    read += n
                End While

                Return Encoding.UTF8.GetString(buffer, 0, read)

            End Using
        End Using

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Sub OpenURL(url As String)

        Select Case GlobalSettings.Settings.RunningPlatform
            Case GlobalSettings.Settings.Platform.Windows
                Process.Start(url)
            Case GlobalSettings.Settings.Platform.Linux
                Process.Start("xdg-open", url)
            Case GlobalSettings.Settings.Platform.Mac
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
    Public Function Chunk(Of T)(ByVal locations As List(Of T), ByVal Optional nSize As Integer = 30) As List(Of List(Of T))

        Dim list = New List(Of List(Of T))()
        Dim i As Integer = 0
        While i < locations.Count
            list.Add(locations.GetRange(i, Math.Min(nSize, locations.Count - i)))
            i += nSize
        End While

        Return list

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
    Public Function ToDoubleArray(al As String()) As Double()

        Dim list As New List(Of Double)
        For Each item In al
            list.Add(item.ToDoubleFromInvariant())
        Next
        Return list.ToArray()

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToSingleArray(al As String()) As Single()

        Dim list As New List(Of Single)
        For Each item In al
            list.Add(item.ToSingleFromInvariant())
        Next
        Return list.ToArray()

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToSingleArray(al As String(), size As Integer) As Single()

        Dim list As New List(Of Single)
        For Each item In al
            list.Add(item.ToSingleFromInvariant())
        Next
        For i = list.Count To size
            list.Add(0.0F)
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

        Return SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(units, d)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertToSI(d As Double, units As String) As Double

        Return SharedClasses.SystemsOfUnits.Converter.ConvertToSI(units, d)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertUnits(d As Double, fromunits As String, tounits As String) As Double

        Return SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(tounits, SharedClasses.SystemsOfUnits.Converter.ConvertToSI(fromunits, d))

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertUnits(vector As Double(), fromunits As String, tounits As String) As Double()

        Dim newvector As Double() = DirectCast(vector.Clone, Double())

        For i As Integer = 0 To vector.Length - 1
            newvector(i) = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(tounits, SharedClasses.SystemsOfUnits.Converter.ConvertToSI(fromunits, vector(i)))
        Next

        Return newvector

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertFromSI(vector As List(Of Double), units As String) As List(Of Double)

        Dim newvector As New List(Of Double)

        For i As Integer = 0 To vector.Count - 1
            newvector.Add(SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(units, vector(i)))
        Next

        Return newvector

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertToSI(vector As List(Of Double), units As String) As List(Of Double)

        Dim newvector As New List(Of Double)

        For i As Integer = 0 To vector.Count - 1
            newvector.Add(SharedClasses.SystemsOfUnits.Converter.ConvertToSI(units, vector(i)))
        Next

        Return newvector

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ConvertUnits(vector As List(Of Double), fromunits As String, tounits As String) As List(Of Double)

        Dim newvector As New List(Of Double)

        For i As Integer = 0 To vector.Count - 1
            newvector.Add(SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(tounits, SharedClasses.SystemsOfUnits.Converter.ConvertToSI(fromunits, vector(i))))
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
    Public Function IsValidDoubleExpression(str As String) As Boolean

        If Double.TryParse(str, New Double) Then
            Return True
        Else
            Try
                If Not SharedClasses.ExpressionParser.ParserInitialized Then SharedClasses.ExpressionParser.InitializeExpressionParser()
                Dim Expr = SharedClasses.ExpressionParser.ExpContext.CompileGeneric(Of Double)(str)
                Expr.Evaluate()
                Return True
            Catch ex As Exception
                Return False
            End Try
        End If

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ParseExpressionToDouble(str As String) As Double
        If Double.TryParse(str, New Double) Then
            Return Double.Parse(str)
        Else
            If Not SharedClasses.ExpressionParser.ParserInitialized Then SharedClasses.ExpressionParser.InitializeExpressionParser()
            Try
                Dim Expr = SharedClasses.ExpressionParser.ExpContext.CompileGeneric(Of Double)(str)
                Return Expr.Evaluate()
            Catch ex As Exception
                Throw New Exception("Error parsing the math expression '" & str & "'. Make sure to use the dot as the decimal separator for numbers in math expressions, and refrain from using thousands separators to avoid parsing errors.", ex)
            End Try
        End If
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToString(sourcearray As String(), ci As CultureInfo) As String

        Dim sb As String = ""

        If Not sourcearray Is Nothing Then
            If sourcearray.Length > 0 Then

                For Each obj As Object In sourcearray
                    If TypeOf obj Is Double Then
                        sb += Double.Parse(CStr(obj)).ToString(ci) + ","
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
            Return Double.Parse(nstring, CType(NumberStyles.Any - NumberStyles.AllowThousands, NumberStyles), Globalization.CultureInfo.InvariantCulture)
        Else
            Return 0.0#
        End If
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleFromInvariant(s As String) As Double

        Dim ci As CultureInfo = CultureInfo.InvariantCulture

        Return Double.Parse(s.Replace(",", "."), CType(NumberStyles.Any - NumberStyles.AllowThousands, NumberStyles), ci)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToSingleFromInvariant(s As String) As Single

        Dim ci As CultureInfo = CultureInfo.InvariantCulture

        Return Single.Parse(s.Replace(",", "."), CType(NumberStyles.Any - NumberStyles.AllowThousands, NumberStyles), ci)

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDoubleFromCurrent(s As String) As Double

        Dim ci As CultureInfo = CultureInfo.CurrentCulture

        If Double.TryParse(s, CType(NumberStyles.Any - NumberStyles.AllowThousands, NumberStyles), ci, New Double) Then
            Return Double.Parse(s, CType(NumberStyles.Any - NumberStyles.AllowThousands, NumberStyles), ci)
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
    Public Function ToSingle(vector As Double()) As Single()

        Return vector.Select(Function(d) Convert.ToSingle(d)).ToArray()

    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDouble(vector As Single()) As Double()

        Return vector.Select(Function(d) Convert.ToDouble(d)).ToArray()

    End Function
    <System.Runtime.CompilerServices.Extension()>
    Public Function ToString(vector As Double(), numberformat As String) As String()

        Return vector.Select(Function(d) d.ToString(numberformat, CultureInfo.InvariantCulture)).ToArray()

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

    <System.Runtime.CompilerServices.Extension> Function GetObjName(objtype As DWSIM.Interfaces.Enums.GraphicObjects.ObjectType) As String

        Select Case objtype
            Case GraphicObjects.ObjectType.AbsorptionColumn
                Return "Absorption Columns"
            Case GraphicObjects.ObjectType.CapeOpenUO
                Return "CAPE-OPEN Blocks"
            Case GraphicObjects.ObjectType.ComponentSeparator
                Return "Compound Separators"
            Case GraphicObjects.ObjectType.Compressor
                Return "Adiabatic Compressors"
            Case GraphicObjects.ObjectType.CompressorExpander
                Return "Adiabatic Compressors/Expanders"
            Case GraphicObjects.ObjectType.Cooler
                Return "Coolers"
            Case GraphicObjects.ObjectType.CustomUO
                Return "Python Script Blocks"
            Case GraphicObjects.ObjectType.DistillationColumn
                Return "Distillation Columns"
            Case GraphicObjects.ObjectType.EnergyStream
                Return "Energy Streams"
            Case GraphicObjects.ObjectType.ExcelUO
                Return "Spreadsheet Blocks"
            Case GraphicObjects.ObjectType.Expander
                Return "Adiabatic Expanders"
            Case GraphicObjects.ObjectType.Filter
                Return "Filters"
            Case GraphicObjects.ObjectType.FlowsheetUO
                Return "Sub-Flowsheet Blocks"
            Case GraphicObjects.ObjectType.Heater
                Return "Heaters"
            Case GraphicObjects.ObjectType.HeaterCooler
                Return "Heaters/Coolers"
            Case GraphicObjects.ObjectType.HeatExchanger
                Return "Heat Exchangers"
            Case GraphicObjects.ObjectType.MaterialStream
                Return "Material Streams"
            Case GraphicObjects.ObjectType.NodeEn
                Return "Energy Stream Mixers"
            Case GraphicObjects.ObjectType.NodeIn
                Return "Material Stream Mixers"
            Case GraphicObjects.ObjectType.NodeOut
                Return "Material Stream Splitters"
            Case GraphicObjects.ObjectType.OrificePlate
                Return "Orifice Plates"
            Case GraphicObjects.ObjectType.OT_Adjust
                Return "Adjust (Controller) Blocks"
            Case GraphicObjects.ObjectType.OT_EnergyRecycle
                Return "Energy Recycle Blocks"
            Case GraphicObjects.ObjectType.OT_Recycle
                Return "Recycle Blocks"
            Case GraphicObjects.ObjectType.OT_Spec
                Return "Secification Blocks"
            Case GraphicObjects.ObjectType.Pipe
                Return "Pipe Segment Blocks"
            Case GraphicObjects.ObjectType.Pump
                Return "Adiabatic Pumps"
            Case GraphicObjects.ObjectType.RCT_Conversion
                Return "Conversion Reactors"
            Case GraphicObjects.ObjectType.RCT_CSTR
                Return "CSTRs"
            Case GraphicObjects.ObjectType.RCT_Equilibrium
                Return "Equilibrium Reactors"
            Case GraphicObjects.ObjectType.RCT_Gibbs
                Return "Gibbs Reactors"
            Case GraphicObjects.ObjectType.RCT_PFR
                Return "PFRs"
            Case GraphicObjects.ObjectType.ReboiledAbsorber
                Return "Reboiled Absorbers"
            Case GraphicObjects.ObjectType.RefluxedAbsorber
                Return "Refluxed Absorbers"
            Case GraphicObjects.ObjectType.ShortcutColumn
                Return "Shortcut Columns"
            Case GraphicObjects.ObjectType.SolidSeparator
                Return "Solids Separators"
            Case GraphicObjects.ObjectType.Tank
                Return "Tanks"
            Case GraphicObjects.ObjectType.TPVessel
                Return "Three-Phase Gas-Liquid Separators"
            Case GraphicObjects.ObjectType.Valve
                Return "Valves"
            Case GraphicObjects.ObjectType.Vessel
                Return "Gas-Liquid Separators"
            Case Else
                Return "N/A"
        End Select

    End Function

    <System.Runtime.CompilerServices.Extension> Function Encrypt(text As String, passphrase As String) As String

        Return SharedClasses.EncryptString.StringCipher.Encrypt(text, passphrase)

    End Function

    <System.Runtime.CompilerServices.Extension> Function Decrypt(text As String, passphrase As String) As String

        Return SharedClasses.EncryptString.StringCipher.Decrypt(text, passphrase)

    End Function

    ''' <summary>
    ''' Every combination of k elements of the sequence, order not counting.
    ''' </summary>
    <System.Runtime.CompilerServices.Extension>
    Public Function DifferentCombinations(Of T)(elements As IEnumerable(Of T), k As Integer) As IEnumerable(Of IEnumerable(Of T))

        If k = 0 Then Return {New T() {}}

        Return elements.SelectMany(Function(e, i) elements.Skip(i + 1).DifferentCombinations(k - 1).
                                       Select(Function(c) New T() {e}.Concat(c)))

    End Function

End Module

