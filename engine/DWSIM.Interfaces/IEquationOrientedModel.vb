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

    ''' <summary>False for an equation with no derivative at x (a minimum of tied pressures), which a check of the derivatives leaves out.</summary>
    Function EOIsSmooth(context As IEquationOrientedContext, x As Double(), k As Integer) As Boolean

    ''' <summary>
    ''' True when the equations hold next to a saturated stream (one whose extended vapor fraction is a variable, see
    ''' <see cref="IEquationOrientedContext.VaporFractionIndex"/>): they read the streams only through their flows,
    ''' pressures, enthalpies, vapor fractions and properties at the state the vapor fraction gives. False makes the
    ''' solver take the object as a black box next to such a stream.
    ''' </summary>
    ReadOnly Property EOTakesSaturatedStreams As Boolean

    ''' <summary>
    ''' True when the equations hold with a saturated stream at an inlet: they read the inlets only through their flows,
    ''' pressures, temperatures and enthalpies (no flash of an inlet at its temperature and pressure, where a saturated
    ''' stream's phases are not told). The outlets are judged by <see cref="EOTakesSaturatedStreams"/>.
    ''' </summary>
    ReadOnly Property EOTakesSaturatedInlets As Boolean

End Interface

''' <summary>
''' A PT flash of a mixture as the equation-oriented solver's submodel gives it: the moles in each phase and the enthalpy
''' flow, and, when asked, their derivatives with respect to T, P and the moles of the mixture (by the implicit function
''' theorem on the equality of fugacities).
''' </summary>
Public Interface IEquationOrientedFlash

    ''' <summary>Molar flow of each compound in the vapor (mol/s).</summary>
    ReadOnly Property V As Double()

    ''' <summary>Molar flow of each compound in the liquid (mol/s).</summary>
    ReadOnly Property L As Double()

    ''' <summary>Enthalpy flow of the mixture (kW).</summary>
    ReadOnly Property H As Double

    ReadOnly Property VdT As Double()

    ReadOnly Property VdP As Double()

    ''' <summary>d(vapor moles of compound i)/d(moles of compound j in the mixture), as (i, j).</summary>
    ReadOnly Property Vdn As Double(,)

    ReadOnly Property HdT As Double

    ReadOnly Property HdP As Double

    ReadOnly Property Hdn As Double()

    ''' <summary>Entropy flow of the mixture (kW/K), worked out when asked for.</summary>
    ReadOnly Property S As Double

    ReadOnly Property SdT As Double

    ReadOnly Property SdP As Double

    ReadOnly Property Sdn As Double()

End Interface

''' <summary>
''' What the equation-oriented solver gives an object writing its equations: where each stream's variables are, the
''' tolerances, the enthalpy of a stream at a state and its derivatives, the parameters the solver moves, and the black
''' box an object without equations of its own is routed to.
''' </summary>
Public Interface IEquationOrientedContext

    ''' <summary>The number of compounds: a stream's variables are T (K), P (Pa) and the molar flow (mol/s) of each compound, in that order.</summary>
    ReadOnly Property CompoundCount As Integer

    ''' <summary>The compounds' names, in the order of the flows.</summary>
    ReadOnly Property CompoundNames As String()

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

    ''' <summary>
    ''' A PT flash of the moles n at T and P with the package of the stream at this offset; with derivatives, the members
    ''' that hold them are filled too.
    ''' </summary>
    Function Flash(offset As Integer, T As Double, P As Double, n As Double(), derivatives As Boolean) As IEquationOrientedFlash

    ''' <summary>The molar weight of each compound (g/mol), in the order of the variables.</summary>
    ReadOnly Property MolecularWeights As Double()

    ''' <summary>
    ''' The variable of the extended vapor fraction of the stream at this offset, or -1. A stream of one compound, or one
    ''' at saturation whose bubble and dew points are close, has one: its temperature then follows from it (inside 0..1 the
    ''' saturation temperature, below 0 a subcooled liquid, above 1 a superheated vapor), and the vapor fraction is what
    ''' the equations set.
    ''' </summary>
    Function VaporFractionIndex(offset As Integer) As Integer

    ''' <summary>The tolerance of an equation written on a vapor fraction.</summary>
    ReadOnly Property VaporFractionTolerance As Double

    ''' <summary>The temperature (K) at which the stream at this offset, at the pressure and compound flows x gives it, has this vapor fraction.</summary>
    Function SaturationTemperature(offset As Integer, x As Double(), vaporFraction As Double) As Double

    ''' <summary>
    ''' A property of the stream at this offset, with the stream put at the state x gives it (flashed there, its properties
    ''' calculated) and read by the function; kept by the name and the state.
    ''' </summary>
    Function StreamProperty(offset As Integer, x As Double(), name As String, read As Func(Of IMaterialStream, Double)) As Double

    ''' <summary>The derivatives of <see cref="StreamProperty"/> with respect to the stream's T, P and compound flows, by central differences.</summary>
    Function StreamPropertyDerivatives(offset As Integer, x As Double(), name As String, read As Func(Of IMaterialStream, Double)) As Double()

    ''' <summary>Several properties of the stream at this offset at once, the stream put at the state x gives it; kept by the name and the state.</summary>
    Function StreamProperties(offset As Integer, x As Double(), name As String, read As Func(Of IMaterialStream, Double())) As Double()

    ''' <summary>The derivatives of <see cref="StreamProperties"/>: one row per property, one column per T, P and compound flow of the stream.</summary>
    Function StreamPropertiesDerivatives(offset As Integer, x As Double(), name As String, read As Func(Of IMaterialStream, Double())) As Double(,)

    ''' <summary>
    ''' The offsets of the material streams that enter the object, those whose pressure does not come back around from the
    ''' object's own outlets first: where two pressures tie, the first decides, and a recycle that returns at the pressure
    ''' of a feed leaves the feed in charge.
    ''' </summary>
    Function InletOffsets(obj As ISimulationObject) As Integer()

    ''' <summary>The variable the solver made of this property of the object (an Adjust or a design variable moves it); -1 when it stays at the object's value.</summary>
    Function ParameterIndex(obj As ISimulationObject, propertyCode As String) As Integer

    ''' <summary>The equations of the object as a black box: its outlets as its own calculation gives them from its inlets.</summary>
    Function BlackBoxEquationCount(obj As ISimulationObject) As Integer

    Function BlackBoxVariables(obj As ISimulationObject) As Integer()

    Sub BlackBoxResiduals(obj As ISimulationObject, x As Double(), r As Double(), row As Integer)

    Function BlackBoxEquationName(obj As ISimulationObject, k As Integer) As String

End Interface
