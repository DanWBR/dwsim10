Public Class CauseAndEffectMatrix

    Implements IDynamicsCauseAndEffectMatrix, ICustomXMLSerialization

    Public Property ID As String = "" Implements IDynamicsCauseAndEffectMatrix.ID

    Public Property Description As String = "" Implements IDynamicsCauseAndEffectMatrix.Description

    Public Property Items As Dictionary(Of String, IDynamicsCauseAndEffectItem) = New Dictionary(Of String, IDynamicsCauseAndEffectItem) Implements IDynamicsCauseAndEffectMatrix.Items

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Dim data = XMLSerializer.Serialize(Me)
        Dim e1 = New XElement("Items")
        For Each kvp As KeyValuePair(Of String, IDynamicsCauseAndEffectItem) In Items
            e1.Add(New XElement("Item", DirectCast(kvp.Value, ICustomXMLSerialization).SaveData))
        Next
        data.Add(e1)
        Return data
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        XMLSerializer.Deserialize(Me, data)
        Dim elm As XElement = (From xel2 As XElement In data Select xel2 Where xel2.Name = "Items").LastOrDefault
        If Not elm Is Nothing Then
            Items = New Dictionary(Of String, IDynamicsCauseAndEffectItem)
            For Each xel2 As XElement In elm.Elements
                Dim cei = New CauseAndEffectItem
                DirectCast(cei, ICustomXMLSerialization).LoadData(xel2.Elements.ToList)
                Items.Add(cei.ID, cei)
            Next
        End If
        Return True
    End Function

End Class
