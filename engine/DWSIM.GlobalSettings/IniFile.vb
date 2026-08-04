'    Reading and writing of the dwsim.ini settings file.
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

Imports System.Globalization
Imports System.IO
Imports System.Text

''' <summary>
''' One section of an INI file: an ordered list of key/value pairs under a [name] header.
''' </summary>
Public Class IniConfig

    Private ReadOnly _keys As New List(Of String)
    Private ReadOnly _values As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public Sub New(name As String)
        Me.Name = name
    End Sub

    Public ReadOnly Property Name As String

    ''' <summary>The keys of this section, in the order they were read or written.</summary>
    Public ReadOnly Property Keys As IEnumerable(Of String)
        Get
            Return _keys
        End Get
    End Property

    ''' <summary>Every value in this section, in key order. Sections used as lists are read this way.</summary>
    Public Function GetValues() As String()

        Return _keys.Select(Function(k) _values(k)).ToArray()

    End Function

    Public Function Contains(key As String) As Boolean

        Return _values.ContainsKey(key)

    End Function

    Public Function [Get](key As String) As String

        Dim value As String = Nothing
        If _values.TryGetValue(key, value) Then Return value

        Return Nothing

    End Function

    Public Function [Get](key As String, defaultValue As String) As String

        Dim value = [Get](key)
        If value Is Nothing Then Return defaultValue

        Return value

    End Function

    Public Function GetString(key As String) As String

        Return [Get](key)

    End Function

    Public Function GetString(key As String, defaultValue As String) As String

        Return [Get](key, defaultValue)

    End Function

    Public Function GetBoolean(key As String, defaultValue As Boolean) As Boolean

        Dim value = [Get](key)
        If String.IsNullOrWhiteSpace(value) Then Return defaultValue

        Select Case value.Trim().ToLowerInvariant()
            Case "true", "on", "yes", "1"
                Return True
            Case "false", "off", "no", "0"
                Return False
            Case Else
                Return defaultValue
        End Select

    End Function

    Public Function GetInt(key As String, defaultValue As Integer) As Integer

        Dim parsed As Integer
        If Integer.TryParse(Trimmed(key), NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) Then Return parsed

        Return defaultValue

    End Function

    Public Function GetFloat(key As String, defaultValue As Double) As Double

        Return GetDouble(key, defaultValue)

    End Function

    ''' <summary>
    ''' Reads a number. Files written by earlier versions carry the decimal separator of the
    ''' machine that wrote them, so the current culture is tried when the invariant parse fails.
    ''' </summary>
    Public Function GetDouble(key As String, defaultValue As Double) As Double

        Dim text = Trimmed(key)
        If text Is Nothing Then Return defaultValue

        Dim parsed As Double
        If Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then Return parsed
        If Double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, parsed) Then Return parsed

        Return defaultValue

    End Function

    Public Sub [Set](key As String, value As Object)

        Dim text As String

        If value Is Nothing Then
            text = ""
        ElseIf TypeOf value Is Double Then
            text = DirectCast(value, Double).ToString(CultureInfo.InvariantCulture)
        ElseIf TypeOf value Is Single Then
            text = DirectCast(value, Single).ToString(CultureInfo.InvariantCulture)
        ElseIf TypeOf value Is Decimal Then
            text = DirectCast(value, Decimal).ToString(CultureInfo.InvariantCulture)
        Else
            text = Convert.ToString(value, CultureInfo.InvariantCulture)
        End If

        If Not _values.ContainsKey(key) Then _keys.Add(key)
        _values(key) = text

    End Sub

    Public Sub Remove(key As String)

        If _values.Remove(key) Then
            _keys.RemoveAll(Function(k) String.Equals(k, key, StringComparison.OrdinalIgnoreCase))
        End If

    End Sub

    Private Function Trimmed(key As String) As String

        Dim value = [Get](key)
        If value Is Nothing Then Return Nothing

        Return value.Trim()

    End Function

End Class

''' <summary>
''' The sections of an INI file, addressed by name. An unknown name gives Nothing, so callers
''' can tell a missing section from an empty one.
''' </summary>
Public Class IniConfigCollection

    Private ReadOnly _order As New List(Of String)
    Private ReadOnly _sections As New Dictionary(Of String, IniConfig)(StringComparer.OrdinalIgnoreCase)

    Default Public ReadOnly Property Item(name As String) As IniConfig
        Get
            Dim section As IniConfig = Nothing
            If _sections.TryGetValue(name, section) Then Return section

            Return Nothing
        End Get
    End Property

    Public ReadOnly Property Count As Integer
        Get
            Return _order.Count
        End Get
    End Property

    Public Function All() As IEnumerable(Of IniConfig)

        Return _order.Select(Function(n) _sections(n)).ToList()

    End Function

    Friend Function Add(name As String) As IniConfig

        Dim section = Item(name)
        If section IsNot Nothing Then Return section

        section = New IniConfig(name)
        _order.Add(name)
        _sections.Add(name, section)

        Return section

    End Function

End Class

''' <summary>
''' An INI file held in memory. Reads on construction and writes back on <see cref="Save"/>.
''' Comments and blank lines are not preserved: the file belongs to the application.
''' </summary>
Public Class IniConfigSource

    Private _path As String

    Public Sub New()
        Configs = New IniConfigCollection()
    End Sub

    Public Sub New(path As String)

        Me.New()

        _path = path

        If File.Exists(path) Then Parse(File.ReadAllLines(path))

    End Sub

    Public ReadOnly Property Configs As IniConfigCollection

    Public Function AddConfig(name As String) As IniConfig

        Return Configs.Add(name)

    End Function

    Public Sub Save()

        Save(_path)

    End Sub

    Public Sub Save(path As String)

        If String.IsNullOrEmpty(path) Then Throw New InvalidOperationException("This settings file has no path to save to.")

        Dim text As New StringBuilder()

        For Each section In Configs.All()
            text.Append("["c).Append(section.Name).AppendLine("]")
            For Each key In section.Keys
                text.Append(key).Append(" = ").AppendLine(section.Get(key))
            Next
            text.AppendLine()
        Next

        File.WriteAllText(path, text.ToString())

        _path = path

    End Sub

    Private Sub Parse(lines As String())

        Dim current As IniConfig = Nothing

        For Each line In lines

            Dim trimmed = line.Trim()

            If trimmed.Length = 0 Then Continue For
            If trimmed.StartsWith(";") OrElse trimmed.StartsWith("#") Then Continue For

            If trimmed.StartsWith("[") AndAlso trimmed.EndsWith("]") Then
                current = Configs.Add(trimmed.Substring(1, trimmed.Length - 2).Trim())
                Continue For
            End If

            Dim separator = trimmed.IndexOf("="c)
            If separator < 0 Then Continue For

            ' a value before any section header belongs to an unnamed one, as in Nini
            If current Is Nothing Then current = Configs.Add("")

            current.Set(trimmed.Substring(0, separator).Trim(),
                        trimmed.Substring(separator + 1).Trim())

        Next

    End Sub

End Class
