Imports DWSIMCore.Foundation.Enums

Public Class DynamicEvent

    Implements IDynamicsEvent, ICustomXMLSerialization

    Public Property ID As String = "" Implements IDynamicsEvent.ID

    Public Property Description As String = "" Implements IDynamicsEvent.Description

    Public Property TimeStamp As Date = DateTime.MinValue Implements IDynamicsEvent.TimeStamp

    Public Property EventType As Dynamics.DynamicsEventType = Dynamics.DynamicsEventType.ChangeProperty Implements IDynamicsEvent.EventType

    Public Property SimulationObjectID As String = "" Implements IDynamicsEvent.SimulationObjectID

    Public Property SimulationObjectProperty As String = "" Implements IDynamicsEvent.SimulationObjectProperty

    Public Property SimulationObjectPropertyValue As String = "" Implements IDynamicsEvent.SimulationObjectPropertyValue

    Public Property SimulationObjectPropertyUnits As String = "" Implements IDynamicsEvent.SimulationObjectPropertyUnits

    Public Property ScriptID As String = "" Implements IDynamicsEvent.ScriptID

    Public Property Enabled As Boolean = True Implements IDynamicsEvent.Enabled

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Return XMLSerializer.Serialize(Me)
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        XMLSerializer.Deserialize(Me, data)
        Return True
    End Function

End Class
