''' <summary>
''' The equations a simulation object offers to an equation-oriented solver, which solves every stream's temperature,
''' pressure and compound molar flows (the variables, in SI) with the equations of all blocks at once. Each residual is
''' divided by its tolerance, so the system is converged when every residual is within 1. The base class implements it
''' by handing the object to the solver as a black box (its own Calculate); a unit operation with equations of its own
''' overrides the members and returns True from <see cref="EOHasEquations"/>.
''' </summary>
Public Interface IEquationOrientedModel

    ''' <summary>True when the object writes its own equations in its current mode; False makes the solver take it as a black box.</summary>
    ReadOnly Property EOHasEquations As Boolean

    ''' <summary>
    ''' The property codes the equations read and an Adjust or a design variable may move (a set temperature, a pressure
    ''' drop). Nothing for a black box, whose own calculation reads any property.
    ''' </summary>
    Function EOParameters() As String()

    ''' <summary>How many equations the object adds, for the streams connected to it.</summary>
    Function EOEquationCount(context As IEquationOrientedContext) As Integer

    ''' <summary>The indices of the variables the residuals read, the object's own parameters the solver moves included.</summary>
    Function EOVariables(context As IEquationOrientedContext) As Integer()

    ''' <summary>Writes the residuals, each divided by its tolerance, into r from position row.</summary>
    Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)

    ''' <summary>
    ''' The derivatives of the residuals through add(equation within the object, variable, value); entries for the same
    ''' pair add up. False when the object has none, and the solver takes finite differences.
    ''' </summary>
    Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean

    ''' <summary>What equation k says, for the solver's messages.</summary>
    Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String

End Interface

''' <summary>
''' What the equation-oriented solver gives an object writing its equations: where each stream's variables are, the
''' tolerances, the enthalpy of a stream at a state and its derivatives, the parameters the solver moves, and the black
''' box an object without equations of its own is routed to.
''' </summary>
Public Interface IEquationOrientedContext

    ''' <summary>The number of compounds: a stream's variables are T (K), P (Pa) and the molar flow (mol/s) of each compound, in that order.</summary>
    ReadOnly Property CompoundCount As Integer

    ''' <summary>The index of the first variable (the temperature) of the material stream with this name; -1 when it is not in the system.</summary>
    Function StreamOffset(streamName As String) As Integer

    ReadOnly Property TemperatureTolerance As Double

    ReadOnly Property PressureTolerance As Double

    ''' <summary>The tolerance of a compound flow balance written on the stream at this offset (mol/s).</summary>
    Function FlowTolerance(offset As Integer) As Double

    ''' <summary>The tolerance of an energy balance written on the stream at this offset (kW).</summary>
    Function EnergyTolerance(offset As Integer) As Double

    ''' <summary>The enthalpy flow (kW) of the stream at this offset, at the state x gives it (a PT flash of its package).</summary>
    Function Enthalpy(offset As Integer, x As Double()) As Double

    ''' <summary>The derivatives of <see cref="Enthalpy"/>: with respect to T, P and the molar flow of each compound, in that order.</summary>
    Function EnthalpyDerivatives(offset As Integer, x As Double()) As Double()

    ''' <summary>The variable the solver made of this property of the object (an Adjust or a design variable moves it); -1 when it stays at the object's value.</summary>
    Function ParameterIndex(obj As ISimulationObject, propertyCode As String) As Integer

    ''' <summary>The equations of the object as a black box: its outlets as its own calculation gives them from its inlets.</summary>
    Function BlackBoxEquationCount(obj As ISimulationObject) As Integer

    Function BlackBoxVariables(obj As ISimulationObject) As Integer()

    Sub BlackBoxResiduals(obj As ISimulationObject, x As Double(), r As Double(), row As Integer)

    Function BlackBoxEquationName(obj As ISimulationObject, k As Integer) As String

End Interface
