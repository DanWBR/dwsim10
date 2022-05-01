Public Class EventSet

    Implements IDynamicsEventSet, ICustomXMLSerialization

    Public Property ID As String = "" Implements IDynamicsEventSet.ID

    Public Property Description As String = "" Implements IDynamicsEventSet.Description

    Public Property Events As Dictionary(Of String, IDynamicsEvent) = New Dictionary(Of String, IDynamicsEvent) Implements IDynamicsEventSet.Events

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Dim data = XMLSerializer.Serialize(Me)
        Dim e1 = New XElement("Events")
        For Each kvp As KeyValuePair(Of String, IDynamicsEvent) In Events
            e1.Add(New XElement("Event", DirectCast(kvp.Value, ICustomXMLSerialization).SaveData))
        Next
        data.Add(e1)
        Return data
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        XMLSerializer.Deserialize(Me, data)
        Dim elm As XElement = (From xel2 As XElement In data Select xel2 Where xel2.Name = "Events").LastOrDefault
        If Not elm Is Nothing Then
            Events = New Dictionary(Of String, IDynamicsEvent)
            For Each xel2 As XElement In elm.Elements
                Dim ev = New DynamicEvent
                DirectCast(ev, ICustomXMLSerialization).LoadData(xel2.Elements.ToList)
                Events.Add(ev.ID, ev)
            Next
        End If
        Return True
    End Function


End Class
