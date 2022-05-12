'    DWSIM Interface definitions
'    Copyright 2020 Daniel Wagner O. de Medeiros


''' <summary>
''' This interface defines the basic properties of the Dynamic Mode Integrator.
''' </summary>
Public Interface IDynamicsIntegrator

    Property ID As String

    Property Description As String

    Property ShouldCalculateEquilibrium As Boolean

    Property ShouldCalculatePressureFlow As Boolean

    Property ShouldCalculateControl As Boolean

    Property IntegrationStep As TimeSpan

    Property Duration As TimeSpan

    Property RealTimeStepMs As Integer

    Property CurrentTime As DateTime

    Property CalculationRateEquilibrium As Integer

    Property CalculationRatePressureFlow As Integer

    Property CalculationRateControl As Integer

    Property RealTime As Boolean

    Property MonitoredVariableValues As Dictionary(Of Long, List(Of IDynamicsMonitoredVariable))

    Property MonitoredVariables As List(Of IDynamicsMonitoredVariable)

End Interface
