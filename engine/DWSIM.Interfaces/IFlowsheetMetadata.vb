Public Interface IFlowsheetMetadata

    Property ProcessType As Enums.ProcessType

    Property ProcessDescription As String

    Property KeyCompounds As List(Of String)

    Property KeyReactants As List(Of String)

    Property KeyProducts As List(Of String)

    Property Score As Double

    Property TotalEnergyConsumption As Double

    Property TotalEnergyGeneration As Double

    Property FlowsheetTopology As String

End Interface

