Imports DWSIMCore.Foundation.Enums

Public Class CauseAndEffectItem

    Implements IDynamicsCauseAndEffectItem, ICustomXMLSerialization

    Public Property ID As String = "" Implements IDynamicsCauseAndEffectItem.ID

    Public Property Description As String = "" Implements IDynamicsCauseAndEffectItem.Description

    Public Property Enabled As Boolean Implements IDynamicsCauseAndEffectItem.Enabled

    Public Property AssociatedIndicator As String = "" Implements IDynamicsCauseAndEffectItem.AssociatedIndicator

    Public Property AssociatedIndicatorAlarm As Dynamics.DynamicsAlarmType Implements IDynamicsCauseAndEffectItem.AssociatedIndicatorAlarm

    Public Property SimulationObjectID As String = "" Implements IDynamicsCauseAndEffectItem.SimulationObjectID

    Public Property SimulationObjectProperty As String = "" Implements IDynamicsCauseAndEffectItem.SimulationObjectProperty

    Public Property SimulationObjectPropertyValue As String = "" Implements IDynamicsCauseAndEffectItem.SimulationObjectPropertyValue

    Public Property SimulationObjectPropertyUnits As String = "" Implements IDynamicsCauseAndEffectItem.SimulationObjectPropertyUnits

    Public Property ScriptID As String = "" Implements IDynamicsCauseAndEffectItem.ScriptID

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Return XMLSerializer.Serialize(Me)
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        XMLSerializer.Deserialize(Me, data)
        Return True
    End Function

End Class
