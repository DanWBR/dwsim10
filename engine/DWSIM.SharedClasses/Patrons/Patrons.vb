Imports System.Linq
Imports System.Net

Public Class Patrons

    Private Shared ReadOnly PatronsApiUrl As String = "https://dsw-license-manager.azurewebsites.net/api/patrons"
    Private Shared ReadOnly PatronsApiKey As String = "3B7EB1A1-2000-42A1-A760-BB0A7545400E"

    Shared Sub New()
        ' Azure endpoints require TLS 1.2; .NET Framework 4.x default may exclude it.
        Try
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or
                                                   SecurityProtocolType.Tls12
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Returns the active patrons list. Tries the licensing server first;
    ''' falls back to the embedded activepatrons.txt resource on failure.
    ''' </summary>
    Public Shared Function GetList() As String

        Return JoinLines(GetLines())

    End Function

    ''' <summary>
    ''' The active patrons list with each name shortened to the first name plus initials
    ''' ("Alexander Semenyak" becomes "Alexander S."), for the splash screens.
    ''' </summary>
    Public Shared Function GetShortList() As String

        Return JoinLines(GetLines().Select(AddressOf ShortenName))

    End Function

    Private Shared Function JoinLines(lines As IEnumerable(Of String)) As String

        Return String.Join(", ", lines)

    End Function

    Private Shared Function ShortenName(line As String) As String

        Dim tokens = line.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
        ' the tier badge comes before the name
        Dim first = Array.FindIndex(tokens, Function(tk) tk.Any(AddressOf Char.IsLetter))
        If first < 0 Then Return line

        Dim parts As New List(Of String)(tokens.Take(first + 1))
        For Each tk In tokens.Skip(first + 1)
            If tk.EndsWith(".") Then
                parts.Add(tk)
            ElseIf Not tk.Any(AddressOf Char.IsUpper) AndAlso tk.All(AddressOf Char.IsLetter) AndAlso tk.Length <= 3 Then
                ' particles (de, da, dos, van...) are left out
            Else
                parts.Add(Globalization.StringInfo.GetNextTextElement(tk) & ".")
            End If
        Next
        Return String.Join(" ", parts)

    End Function

    Private Shared Function GetLines() As IEnumerable(Of String)

        Dim result As String = Nothing

        Try
            Dim request = DirectCast(WebRequest.Create(PatronsApiUrl), HttpWebRequest)
            request.Method = "GET"
            request.Timeout = 5000
            request.ReadWriteTimeout = 5000
            request.Headers.Add("X-Api-Key", PatronsApiKey)

            Using response = DirectCast(request.GetResponse(), HttpWebResponse)
                If response.StatusCode = HttpStatusCode.OK Then
                    Using stream = response.GetResponseStream()
                        Using reader As New IO.StreamReader(stream)
                            result = reader.ReadToEnd()
                        End Using
                    End Using
                End If
            End Using
        Catch ex As Exception
            ' Network error, timeout, etc. - fall through to embedded file
        End Try

        If String.IsNullOrWhiteSpace(result) Then
            Using filestr As IO.Stream = System.Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream("DWSIM.SharedClasses.activepatrons.txt")
                If filestr IsNot Nothing Then
                    Using t As New IO.StreamReader(filestr)
                        result = t.ReadToEnd()
                    End Using
                End If
            End Using
        End If

        If String.IsNullOrEmpty(result) Then Return New String() {}

        Return result.Split(New Char() {ChrW(13), ChrW(10)}, StringSplitOptions.RemoveEmptyEntries).Select(Function(l) l.Trim()).Where(Function(l) l <> "").ToArray()

    End Function

End Class
