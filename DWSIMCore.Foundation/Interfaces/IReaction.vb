'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


Public Interface IReaction

    ReadOnly Property Components() As Dictionary(Of String, IReactionStoichBase)

    Property Name() As String

    Property ID() As String

    Property Description() As String

    Property BaseReactant() As String

    Property ReactionPhase() As Enums.PhaseName

    Property ReactionType() As Enums.ReactionType

    Property ReactionBasis() As Enums.ReactionBasis

    Property StoichBalance() As Double

    Property ReactionHeatCO() As Double

    Property ReactionHeat() As Double

    Property Equation() As String
    'Equilibrium

    Property Kvalue() As Double

    Property ReactionGibbsEnergy() As Double

    Property ConstantKeqValue() As Double

    Property Tmax() As Double

    Property Tmin() As Double

    Property Approach() As Double

    Property Expression() As String

    Property KExprType() As Enums.KOpt

    'Kinetic

    Property Rate() As Double

    Property VelUnit() As String

    Property ConcUnit() As String


    Property A_Forward() As Double

    Property A_Reverse() As Double

    Property E_Forward() As Double

    Property E_Forward_Unit() As String

    Property E_Reverse() As Double

    Property E_Reverse_Unit() As String

    Property ReactionKinFwdType As Enums.ReactionKineticType

    Property ReactionKinRevType As Enums.ReactionKineticType

    Property ReactionKinFwdExpression As String

    Property ReactionKinRevExpression As String

    Property ReactionKinetics() As Enums.ReactionKinetics

    Property ScriptTitle As String

    'Heterogeneous
    Property RateEquationNumerator As String

    Property RateEquationDenominator As String
    Property EquilibriumReactionBasisUnits As String
    Function EvaluateK(T As Double, PP As IPropertyPackage) As Double
    Function GetPropertyList() As String()
    Function GetPropertyValue(prop As String) As Double
    Sub SetPropertyValue(prop As String, value As Double)
End Interface
