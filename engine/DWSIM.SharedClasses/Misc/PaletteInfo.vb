Imports System.Globalization
Imports System.Reflection

''' <summary>
''' The industry groups that the object palette offers as filters. A unit operation names the ones
''' it belongs to through a <c>PaletteIndustries</c> property (a String array); one that doesn't
''' name any is a general unit operation, and the palette shows it under every filter.
''' </summary>
Public NotInheritable Class PaletteIndustry

    Public Const OilGasRefining As String = "Oil, Gas and Refining"
    Public Const ChemicalsPolymers As String = "Chemicals and Polymers"
    Public Const PharmaFood As String = "Pharma and Food"
    Public Const SugarEthanol As String = "Sugar and Ethanol"
    Public Const PulpPaper As String = "Pulp and Paper"
    Public Const AluminaMinerals As String = "Alumina and Minerals"
    Public Const WaterElectrolytes As String = "Water and Electrolytes"
    Public Const Bioprocess As String = "Bioprocess"

    ''' <summary>The order of the filter buttons. Industries that aren't listed here come after these, in alphabetical order.</summary>
    Public Shared ReadOnly Order As String() = {OilGasRefining, ChemicalsPolymers, PharmaFood, SugarEthanol, PulpPaper, AluminaMinerals, WaterElectrolytes, Bioprocess}

    Private Sub New()
    End Sub

    ''' <summary>Sorts a set of industry names in the order of the filter buttons.</summary>
    Public Shared Function Sort(names As IEnumerable(Of String)) As List(Of String)
        Return names.Distinct(StringComparer.OrdinalIgnoreCase) _
                    .OrderBy(Function(n) If(Array.IndexOf(Order, n) >= 0, Array.IndexOf(Order, n), Order.Length)) _
                    .ThenBy(Function(n) n, StringComparer.CurrentCultureIgnoreCase).ToList()
    End Function

End Class

Partial Public Class Utility

    ''' <summary>
    ''' Returns the Patreon access level of the current user. Assigned by the Support layer; left
    ''' Nothing in the open edition, where the palette doesn't draw any level badge.
    ''' </summary>
    Public Shared UserAccessLevel As Func(Of Integer) = Nothing

    ''' <summary>
    ''' Raised when the Support layer has validated the licence again, so that the access level may
    ''' have changed (the licence check runs in the background after the palette is built). It may
    ''' be raised on any thread.
    ''' </summary>
    Public Shared Event UserAccessLevelChanged As EventHandler

    ''' <summary>Raises <see cref="UserAccessLevelChanged"/>. Called by the Support layer.</summary>
    Public Shared Sub NotifyUserAccessLevelChanged()
        Try
            RaiseEvent UserAccessLevelChanged(Nothing, EventArgs.Empty)
        Catch
        End Try
    End Sub

    ''' <summary>The Patreon access level of the current user, or 0 when the Support layer isn't present.</summary>
    Public Shared Function GetUserAccessLevel() As Integer
        Try
            If UserAccessLevel IsNot Nothing Then Return Math.Max(0, UserAccessLevel.Invoke())
        Catch
        End Try
        Return 0
    End Function

    ''' <summary>
    ''' The Patreon access level that a unit operation needs (1, 2 or 3), read from its
    ''' <c>RequiredAccessLevel</c> property or constant. Returns 0 for a unit operation that anyone can use.
    ''' </summary>
    Public Shared Function GetRequiredAccessLevel(uo As Object) As Integer
        If uo Is Nothing Then Return 0
        Try
            Dim t = uo.GetType()
            Dim flags = BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static
            Dim p = t.GetProperty("RequiredAccessLevel", flags)
            If p IsNot Nothing AndAlso p.GetIndexParameters().Length = 0 Then
                Return Math.Max(0, Convert.ToInt32(p.GetValue(If(p.GetGetMethod().IsStatic, Nothing, uo)), CultureInfo.InvariantCulture))
            End If
            Dim f = t.GetField("RequiredAccessLevel", flags)
            If f IsNot Nothing Then Return Math.Max(0, Convert.ToInt32(f.GetValue(If(f.IsStatic, Nothing, uo)), CultureInfo.InvariantCulture))
            'a unit operation whose level depends on its contents (the pipe network) has a method instead
            Dim m = t.GetMethod("RequiredAccessLevel", flags, Nothing, Type.EmptyTypes, Nothing)
            If m IsNot Nothing AndAlso m.ReturnType Is GetType(Integer) Then
                Return Math.Max(0, CInt(m.Invoke(If(m.IsStatic, Nothing, uo), Nothing)))
            End If
        Catch
        End Try
        Return 0
    End Function

    ''' <summary>
    ''' The industries a unit operation belongs to, read from its <c>PaletteIndustries</c> property.
    ''' A refining unit operation (IsRefining) that doesn't declare any belongs to oil, gas and refining,
    ''' and a biochemical one (IsBio) to bioprocess. An empty array means a general unit operation.
    ''' </summary>
    Public Shared Function GetPaletteIndustries(uo As Object) As String()
        If uo Is Nothing Then Return Array.Empty(Of String)()
        Try
            Dim p = uo.GetType().GetProperty("PaletteIndustries", BindingFlags.Public Or BindingFlags.Instance)
            If p IsNot Nothing Then
                Dim v = TryCast(p.GetValue(uo), IEnumerable(Of String))
                If v IsNot Nothing Then
                    Dim list = v.Where(Function(s) Not String.IsNullOrWhiteSpace(s)).Select(Function(s) s.Trim()).ToArray()
                    If list.Length > 0 Then Return list
                End If
            End If
        Catch
        End Try
        If FlagIsTrue(uo, "IsRefining") Then Return {PaletteIndustry.OilGasRefining}
        If FlagIsTrue(uo, "IsBio") Then Return {PaletteIndustry.Bioprocess}
        Return Array.Empty(Of String)()
    End Function

    ''' <summary>
    ''' Tells whether a palette item matches a search text. Every word of the text has to appear
    ''' in at least one of the fields (the name, the description, the group, the industries),
    ''' ignoring case and accents. An empty text matches every item.
    ''' </summary>
    Public Shared Function PaletteSearchMatches(query As String, ParamArray fields As String()) As Boolean
        If String.IsNullOrWhiteSpace(query) Then Return True
        Dim hay = FoldForSearch(String.Join(" ", fields.Where(Function(s) s IsNot Nothing)))
        For Each word In FoldForSearch(query).Split({" "c, ","c, ";"c}, StringSplitOptions.RemoveEmptyEntries)
            If hay.IndexOf(word, StringComparison.Ordinal) < 0 Then Return False
        Next
        Return True
    End Function

    Private Shared Function FoldForSearch(s As String) As String
        Dim d = s.Normalize(Text.NormalizationForm.FormD)
        Dim sb As New Text.StringBuilder(d.Length)
        For Each c In d
            If CharUnicodeInfo.GetUnicodeCategory(c) <> UnicodeCategory.NonSpacingMark Then sb.Append(Char.ToLowerInvariant(c))
        Next
        Return sb.ToString()
    End Function

End Class
