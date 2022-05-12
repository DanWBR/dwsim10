'    DWSIM Interface definitions
'    Copyright 2020 Daniel Wagner O. de Medeiros


Public Interface IDynamicsEvent

    Property ID As String

    Property Description As String

    Property Enabled As Boolean

    Property TimeStamp As DateTime

    Property EventType As Enums.Dynamics.DynamicsEventType

    Property SimulationObjectID As String

    Property SimulationObjectProperty As String

    Property SimulationObjectPropertyValue As String

    Property SimulationObjectPropertyUnits As String

    Property ScriptID As String

End Interface
