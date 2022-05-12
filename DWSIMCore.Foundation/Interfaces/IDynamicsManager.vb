'    DWSIM Interface definitions
'    Copyright 2020 Daniel Wagner O. de Medeiros


Public Interface IDynamicsManager

    Property Description As String

    Property ScheduleList As Dictionary(Of String, IDynamicsSchedule)

    Property CauseAndEffectMatrixList As Dictionary(Of String, IDynamicsCauseAndEffectMatrix)

    Property EventSetList As Dictionary(Of String, IDynamicsEventSet)

    Property CurrentSchedule As String

    Property IntegratorList As Dictionary(Of String, IDynamicsIntegrator)

    Function GetChartModel(IntegratorID As String) As Object

    Property ToggleDynamicMode() As Action(Of Boolean)

    Property RunSchedule() As Func(Of String, Task)

    Function GetSchedule(name As String) As IDynamicsSchedule

    Function GetIntegrator(name As String) As IDynamicsIntegrator

    Function GetEventSet(name As String) As IDynamicsEventSet

    Function GetCauseAndEffectMatrix(name As String) As IDynamicsCauseAndEffectMatrix

End Interface
