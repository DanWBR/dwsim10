Imports System.Net.Http
Imports System.Net
Imports System.Text
Imports Newtonsoft.Json.Linq
Imports Globalization = System.Globalization

Public Class KDBParser

    Private Shared ReadOnly BaseUrl As String = "https://api.mdlkdb.com"

    Private Shared Function CreateHttpClient() As HttpClient

        Dim siteUri As New Uri(BaseUrl)
        Dim proxyUri As Uri = Net.WebRequest.GetSystemWebProxy.GetProxy(siteUri)

        Dim handler As New HttpClientHandler()

        If Not siteUri.AbsolutePath = proxyUri.AbsolutePath Then
            Dim proxyObj As New WebProxy(proxyUri)
            proxyObj.Credentials = CredentialCache.DefaultCredentials
            handler.Proxy = proxyObj
        End If

        Return New HttpClient(handler)

    End Function

    Private Shared Function FetchJson(url As String) As JObject

        Dim http = CreateHttpClient()

        Dim response = http.GetByteArrayAsync(url)
        response.Wait()

        Dim source As String = Encoding.GetEncoding("utf-8").GetString(response.Result, 0, response.Result.Length)

        Return JObject.Parse(source)

    End Function

    Shared Function GetBinaryVLESetIDs(compound1 As String, compound2 As String, Optional page As Integer = 1) As KDBVLESearchResult

        Dim url As String = BaseUrl + "/binary-vle/search?compound1=" + WebUtility.UrlEncode(compound1) +
                            "&compound2=" + WebUtility.UrlEncode(compound2) +
                            "&page=" + page.ToString

        Dim json = FetchJson(url)

        Dim result As New KDBVLESearchResult()
        result.TotalCount = CInt(json("num_data"))

        Dim compounds = json("compounds")
        result.Compound1Name = compounds("compound1")("name").ToString()
        result.Compound1ID = CInt(compounds("compound1")("compound_id"))
        result.Compound2Name = compounds("compound2")("name").ToString()
        result.Compound2ID = CInt(compounds("compound2")("compound_id"))

        For Each item In json("data")
            Dim entry As New KDBVLESetInfo()
            entry.SetID = CInt(item("vle_set_id"))
            entry.TypeID = CInt(item("vle_type_id"))
            entry.Title = item("title").ToString()
            entry.NumberOfDataPoints = CInt(item("number_of_data"))
            entry.TMin = CDbl(item("TMin"))
            entry.TMax = CDbl(item("TMax"))
            entry.PMin = CDbl(item("PMin"))
            entry.PMax = CDbl(item("PMax"))
            entry.XMin = CDbl(item("XMin"))
            entry.XMax = CDbl(item("XMax"))
            entry.YMin = CDbl(item("YMin"))
            entry.YMax = CDbl(item("YMax"))
            entry.Reference = item("reference").ToString()
            entry.Note = If(item("note")?.Type = JTokenType.Null, "", item("note").ToString())
            result.Sets.Add(entry)
        Next

        Return result

    End Function

    Shared Function GetVLEData(vleSetId As Integer) As KDBVLEDataSet

        Dim ci As New Globalization.CultureInfo("en-US")

        Dim url As String = BaseUrl + "/binary-vle/search/" + vleSetId.ToString

        Dim json = FetchJson(url)

        Dim result As New KDBVLEDataSet()

        Dim vleSet = json("VLE_set")
        result.SetID = CInt(vleSet("vle_set_id"))
        result.TypeID = CInt(vleSet("vle_type_id"))
        result.Title = vleSet("title").ToString()
        result.Reference = vleSet("reference").ToString()
        result.Note = If(vleSet("note")?.Type = JTokenType.Null, "", vleSet("note").ToString())

        Dim compounds = json("compounds")
        result.Compound1Name = compounds("compound1")("name").ToString()
        result.Compound1ID = CInt(compounds("compound1")("compound_id"))
        result.Compound2Name = compounds("compound2")("name").ToString()
        result.Compound2ID = CInt(compounds("compound2")("compound_id"))

        For Each item In json("VLE_data")
            Dim dp As New KDBVLEDataPoint()
            Double.TryParse(item("x1_value").ToString(), Globalization.NumberStyles.Any, ci, dp.X)
            Double.TryParse(item("y1_value").ToString(), Globalization.NumberStyles.Any, ci, dp.Y)
            Double.TryParse(item("temperature_value").ToString(), Globalization.NumberStyles.Any, ci, dp.T)
            Double.TryParse(item("pressure_value").ToString(), Globalization.NumberStyles.Any, ci, dp.P)
            result.Data.Add(dp)
        Next

        Return result

    End Function

End Class

Public Class KDBVLESearchResult

    Public TotalCount As Integer
    Public Sets As New List(Of KDBVLESetInfo)

    Public Compound1Name As String = ""
    Public Compound1ID As Integer
    Public Compound2Name As String = ""
    Public Compound2ID As Integer

End Class

Public Class KDBVLESetInfo

    Public SetID As Integer
    Public TypeID As Integer
    Public Title As String = ""
    Public NumberOfDataPoints As Integer
    Public TMin As Double
    Public TMax As Double
    Public PMin As Double
    Public PMax As Double
    Public XMin As Double
    Public XMax As Double
    Public YMin As Double
    Public YMax As Double
    Public Reference As String = ""
    Public Note As String = ""

End Class

Public Class KDBVLEDataSet

    Public SetID As Integer
    Public TypeID As Integer
    Public Title As String = ""
    Public Reference As String = ""
    Public Note As String = ""

    Public Compound1Name As String = ""
    Public Compound1ID As Integer
    Public Compound2Name As String = ""
    Public Compound2ID As Integer

    Public Tunits As String = "K"
    Public Punits As String = "kPa"

    Public Data As New List(Of KDBVLEDataPoint)

End Class

Public Class KDBVLEDataPoint

    Public X, Y, T, P As Double

End Class
