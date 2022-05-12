'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


Public Interface IReactionStoichBase

    Property CompName() As String

    Property StoichCoeff() As Double

    Property DirectOrder() As Double

    Property ReverseOrder() As Double

    Property IsBaseReactant() As Boolean

End Interface
