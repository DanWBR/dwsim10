Imports DWSIM.Interfaces.Enums

Public Class FlowsheetMetadata

    Implements IFlowsheetMetadata, ICustomXMLSerialization

    Public Property ProcessType As ProcessType = ProcessType.Unspecified Implements IFlowsheetMetadata.ProcessType

    Public Property ProcessDescription As String = "" Implements IFlowsheetMetadata.ProcessDescription

    Public Property KeyCompounds As New List(Of String) Implements IFlowsheetMetadata.KeyCompounds

    Public Property KeyReactants As New List(Of String) Implements IFlowsheetMetadata.KeyReactants

    Public Property KeyProducts As New List(Of String) Implements IFlowsheetMetadata.KeyProducts

    Public Property Score As Double Implements IFlowsheetMetadata.Score

    Public Property TotalEnergyConsumption As Double Implements IFlowsheetMetadata.TotalEnergyConsumption

    Public Property TotalEnergyGeneration As Double Implements IFlowsheetMetadata.TotalEnergyGeneration

    Public Property FlowsheetTopology As String = "" Implements IFlowsheetMetadata.FlowsheetTopology

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Return XMLSerializer.XMLSerializer.Serialize(Me)
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        XMLSerializer.XMLSerializer.Deserialize(Me, data)
        Return True
    End Function

End Class
