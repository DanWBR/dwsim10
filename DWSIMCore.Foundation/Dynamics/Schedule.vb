Public Class Schedule

    Implements IDynamicsSchedule, ICustomXMLSerialization

    Public Property ID As String = "" Implements IDynamicsSchedule.ID

    Public Property Description As String = "" Implements IDynamicsSchedule.Description

    Public Property CurrentIntegrator As String = "" Implements IDynamicsSchedule.CurrentIntegrator

    Public Property UsesCauseAndEffectMatrix As Boolean Implements IDynamicsSchedule.UsesCauseAndEffectMatrix

    Public Property UsesEventList As Boolean Implements IDynamicsSchedule.UsesEventList

    Public Property CurrentCauseAndEffectMatrix As String = "" Implements IDynamicsSchedule.CurrentCauseAndEffectMatrix

    Public Property CurrentEventList As String = "" Implements IDynamicsSchedule.CurrentEventList

    Public Property InitialFlowsheetStateID As String = "" Implements IDynamicsSchedule.InitialFlowsheetStateID

    Public Property UseCurrentStateAsInitial As Boolean = True Implements IDynamicsSchedule.UseCurrentStateAsInitial

    Public Property ResetContentsOfAllObjects As Boolean = False Implements IDynamicsSchedule.ResetContentsOfAllObjects

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Return XMLSerializer.Serialize(Me)
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        XMLSerializer.Deserialize(Me, data)
        Return True
    End Function

End Class
