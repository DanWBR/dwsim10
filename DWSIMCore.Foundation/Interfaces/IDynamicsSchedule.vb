'    DWSIM Interface definitions
'    Copyright 2020 Daniel Wagner O. de Medeiros


Public Interface IDynamicsSchedule

    Property ID As String

    Property Description As String

    Property CurrentIntegrator As String

    Property UsesCauseAndEffectMatrix As Boolean

    Property UsesEventList As Boolean

    Property CurrentCauseAndEffectMatrix As String

    Property CurrentEventList As String

    Property InitialFlowsheetStateID As String

    Property UseCurrentStateAsInitial As Boolean

    Property ResetContentsOfAllObjects As Boolean

End Interface
