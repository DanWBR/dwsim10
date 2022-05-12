'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


Public Interface IPhase

    Property ComponentDescription As String
    Property ComponentName As String
    Property Name As String
    Property Compounds As Dictionary(Of String, ICompound)
    ReadOnly Property Properties As IPhaseProperties

End Interface
