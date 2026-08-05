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

        If String.IsNullOrEmpty(result) Then Return String.Empty

        Return result.Replace(vbCrLf, ", ").Replace(vbCr, ", ").Replace(vbLf, ", ").TrimEnd(" "c).TrimEnd(","c)

    End Function

End Class
