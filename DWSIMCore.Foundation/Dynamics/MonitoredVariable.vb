Public Class MonitoredVariable

    Implements IDynamicsMonitoredVariable, ICustomXMLSerialization, ICloneable

    Public Property ID As String = "" Implements IDynamicsMonitoredVariable.ID

    Public Property Description As String = "" Implements IDynamicsMonitoredVariable.Description

    Public Property TimeStamp As Date = New Date Implements IDynamicsMonitoredVariable.TimeStamp

    Public Property ObjectID As String = "" Implements IDynamicsMonitoredVariable.ObjectID

    Public Property PropertyID As String = "" Implements IDynamicsMonitoredVariable.PropertyID

    Public Property PropertyValue As String = "" Implements IDynamicsMonitoredVariable.PropertyValue

    Public Property PropertyUnits As String = "" Implements IDynamicsMonitoredVariable.PropertyUnits

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Return XMLSerializer.Serialize(Me)
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        Return XMLSerializer.Deserialize(Me, data)
    End Function

    Public Function Clone() As Object Implements ICloneable.Clone
        Dim mv As New MonitoredVariable()
        mv.LoadData(Me.SaveData)
        Return mv
    End Function
End Class
