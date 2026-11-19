'    DWSIM AI Assistant bridge
'    Copyright 2026 Daniel Wagner O. de Medeiros
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

Imports System.Collections
Imports System.Collections.Generic
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports DWSIM.Interfaces
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

''' <summary>
''' GET /api/ui-context: what the user is working on in DWSIM (the selected objects, the object
''' editor and its tab, the tool window), with every object it names described by tag, type and state,
''' and the objects of the whole flowsheet that ended the last calculation with an error. It also carries
''' a question the user asked from DWSIM itself ("ask") until the assistant's page picks it up.
''' The "sig" field changes only when the answer does, so the assistant can poll it cheaply.
''' </summary>
''' <remarks>The same file is in the private and in the open copy of the bridge; keep them equal.</remarks>
Public Module UIContextRoute

    Private Const MaxErrorLength As Integer = 300
    Private Const MaxListedErrors As Integer = 5

    ''' <summary>How long a question asked from DWSIM waits for the assistant's page to pick it up.</summary>
    Private Const AskLifetimeSeconds As Integer = 120

    Private ReadOnly AskLock As New Object
    Private PendingAsk As JObject
    Private PendingAskTime As DateTime

    ''' <summary>
    ''' Keeps a question the user asked from DWSIM (a log message, an object's error) until the
    ''' assistant's page reads it with the UI context. The page asks it once, by its id.
    ''' </summary>
    ''' <param name="objectName">The object the message is about, or "".</param>
    ''' <param name="message">The message, or "" to use the object's own error.</param>
    Public Sub Ask(fs As IFlowsheet, objectName As String, message As String)

        Dim ask As New JObject From {{"id", DateTime.UtcNow.Ticks.ToString()}}
        If fs IsNot Nothing AndAlso Not String.IsNullOrEmpty(objectName) Then
            Dim described = DescribeObject(fs, objectName)
            If described IsNot Nothing Then ask("object") = described
        End If
        If String.IsNullOrWhiteSpace(message) AndAlso ask("object") IsNot Nothing Then
            message = ask("object").Value(Of String)("error")
        End If
        message = If(message, "").Trim()
        If message.Length > MaxErrorLength * 2 Then message = message.Substring(0, MaxErrorLength * 2) & "..."
        ask("message") = message

        SyncLock AskLock
            PendingAsk = ask
            PendingAskTime = DateTime.UtcNow
        End SyncLock

    End Sub

    Private Sub AddAsk(result As JObject)
        SyncLock AskLock
            If PendingAsk Is Nothing Then Return
            If (DateTime.UtcNow - PendingAskTime).TotalSeconds > AskLifetimeSeconds Then
                PendingAsk = Nothing
                Return
            End If
            result("ask") = PendingAsk.DeepClone()
        End SyncLock
    End Sub

    Public Function Handle(fs As IFlowsheet) As String

        Dim result As New JObject
        AddAsk(result)

        Dim provider = TryCast(fs, IUIContextProvider)
        Dim ctx As Dictionary(Of String, Object) = Nothing
        If provider IsNot Nothing Then
            Try
                ctx = provider.GetUIContext()
            Catch
            End Try
        End If

        If ctx Is Nothing Then
            ' a host that can't tell (or an older one): the assistant falls back to the flowsheet alone
            result("available") = False
            Return Finish(result)
        End If

        result("available") = True
        result("host") = Text(ctx, "host")
        result("area") = Text(ctx, "area")

        Dim selected As New JArray
        Dim names = TryCast(Value(ctx, "selected"), IEnumerable)
        If names IsNot Nothing Then
            For Each name In names
                Dim described = DescribeObject(fs, Convert.ToString(name))
                If described IsNot Nothing Then selected.Add(described)
            Next
        End If
        result("selected") = selected

        Dim editor = TryCast(Value(ctx, "editor"), IDictionary(Of String, Object))
        If editor IsNot Nothing Then
            Dim described = DescribeObject(fs, Text(editor, "object"))
            If described IsNot Nothing Then
                described("tab") = Text(editor, "tab")
                result("editor") = described
            End If
        End If

        Dim window = TryCast(Value(ctx, "window"), IDictionary(Of String, Object))
        If window IsNot Nothing Then result("window") = ToJson(window)

        Dim flowsheet As New JObject
        Try
            flowsheet("dynamic_mode") = fs.DynamicMode
        Catch
        End Try
        Try
            flowsheet("name") = fs.FlowsheetOptions.SimulationName
        Catch
        End Try
        Dim solving As Boolean = False
        Try
            solving = GlobalSettings.Settings.CalculatorBusy
        Catch
        End Try
        flowsheet("solving") = solving
        ' while the solver runs the messages come and go, so the list waits for the end of the run
        If Not solving Then AddErrors(fs, flowsheet)
        result("flowsheet") = flowsheet

        Return Finish(result)

    End Function

    ''' <summary>Adds the signature of the answer, so a poller can tell a change from the same answer.</summary>
    Private Function Finish(result As JObject) As String
        Dim body = result.ToString(Formatting.None)
        Using sha = SHA1.Create()
            Dim hash = sha.ComputeHash(Encoding.UTF8.GetBytes(body))
            result("sig") = BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant()
        End Using
        Return result.ToString(Formatting.None)
    End Function

    ''' <summary>
    ''' Lists the objects of the whole flowsheet that ended the last calculation with an error, so the
    ''' assistant can offer to explain them even when the user hasn't selected them.
    ''' </summary>
    Private Sub AddErrors(fs As IFlowsheet, flowsheet As JObject)
        Dim errors As New JArray
        Dim count As Integer = 0
        Try
            For Each obj In fs.SimulationObjects.Values.ToList()
                Dim message As String = Nothing
                Try
                    message = obj?.ErrorMessage
                Catch
                End Try
                If String.IsNullOrWhiteSpace(message) Then Continue For
                count += 1
                If errors.Count < MaxListedErrors Then
                    Dim described = DescribeObject(fs, obj.Name)
                    If described IsNot Nothing Then errors.Add(described)
                End If
            Next
        Catch
            ' the collection changed under us: the next poll gets a clean answer
        End Try
        flowsheet("error_count") = count
        flowsheet("errors") = errors
    End Sub

    Private Function DescribeObject(fs As IFlowsheet, name As String) As JObject

        If String.IsNullOrEmpty(name) Then Return Nothing

        Dim obj As ISimulationObject = Nothing
        If Not fs.SimulationObjects.TryGetValue(name, obj) OrElse obj Is Nothing Then
            ' an annotation, a table or a picture: only the drawing knows it
            Dim graphic = fs.GraphicObjects.Values.FirstOrDefault(Function(g) g.Name = name)
            If graphic Is Nothing Then Return Nothing
            Return New JObject From {
                {"name", name},
                {"tag", If(graphic.Tag, name)},
                {"type", graphic.ObjectType.ToString()}
            }
        End If

        Dim described As New JObject From {{"name", name}}
        Try
            described("tag") = If(obj.GraphicObject?.Tag, name)
            described("type") = obj.GraphicObject.ObjectType.ToString()
        Catch
            described("tag") = name
        End Try
        Try
            described("kind") = obj.GetDisplayName()
        Catch
        End Try
        Try
            described("calculated") = obj.Calculated
        Catch
        End Try
        Try
            Dim message = obj.ErrorMessage
            If Not String.IsNullOrWhiteSpace(message) Then
                message = message.Trim()
                If message.Length > MaxErrorLength Then message = message.Substring(0, MaxErrorLength) & "..."
                described("error") = message
            End If
        Catch
        End Try
        Return described

    End Function

    Private Function ToJson(values As IDictionary(Of String, Object)) As JObject
        Dim json As New JObject
        For Each entry In values
            Try
                json(entry.Key) = If(entry.Value Is Nothing, JValue.CreateNull(), JToken.FromObject(entry.Value))
            Catch
                ' a value the serializer can't take is described by its text
                json(entry.Key) = Convert.ToString(entry.Value)
            End Try
        Next
        Return json
    End Function

    Private Function Value(values As IDictionary(Of String, Object), key As String) As Object
        Dim v As Object = Nothing
        If values IsNot Nothing AndAlso values.TryGetValue(key, v) Then Return v
        Return Nothing
    End Function

    Private Function Text(values As IDictionary(Of String, Object), key As String) As String
        Return If(Convert.ToString(Value(values, key)), "")
    End Function

End Module
