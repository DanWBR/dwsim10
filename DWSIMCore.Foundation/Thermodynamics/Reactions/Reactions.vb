Imports System.Xml.Serialization
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.Runtime.Serialization
Imports System.IO
Imports System.Globalization
Imports DWSIMCore.Foundation.Enums
Imports Mages.Core

Namespace BaseClasses

    <System.Serializable()> <XmlRoot(ElementName:="Reaction")>
    Public Class Reaction

        Implements ICloneable, ICustomXMLSerialization

        Implements IReaction

        Public _Components As Dictionary(Of String, IReactionStoichBase)

        <XmlIgnore> <NonSerialized> Private MEngine As Mages.Core.Engine
        <XmlIgnore> <NonSerialized> Private KFunc As Mages.Core.Function
        <XmlIgnore> <NonSerialized> Private _ExpressionChanged As Boolean = True

#Region "    DWSIM Specific"

        Public Function EvaluateK(ByVal T As Double, ByVal pp As PropertyPackages.PropertyPackage) As Double

            'equilibrium constant calculation

            Select Case KExprType

                Case KOpt.Constant

                    Return ConstantKeqValue

                Case KOpt.Expression

                    If MEngine Is Nothing Then
                        MEngine = New Mages.Core.Engine()
                        KFunc = MEngine.Interpret("(T) => " + Expression)
                    End If
                    If _ExpressionChanged Then
                        _ExpressionChanged = False
                        KFunc = MEngine.Interpret("(T) => " + Expression)
                    End If

                    Return Math.Exp(KFunc.Call(Of Double)(T))

                Case KOpt.Gibbs

                    Dim id(Components.Count - 1) As String
                    Dim stcoef(Components.Count - 1) As Double
                    Dim bcidx As Integer = 0
                    Dim j As Integer = 0
                    For Each sb As ReactionStoichBase In Components.Values
                        id(j) = sb.CompName
                        stcoef(j) = sb.StoichCoeff
                        If sb.IsBaseReactant Then bcidx = j
                        j += 1
                    Next

                    Dim DelG_RT = pp.AUX_DELGig_RT(298.15, T, id, stcoef, bcidx)

                    Return Math.Exp(-DelG_RT)

            End Select

        End Function

        'Initializers

        Public Sub New()
            Me._Components = New Dictionary(Of String, IReactionStoichBase)
        End Sub

        Public Sub New(ByVal Name As String, ByVal Id As String)
            Me.New()
            Me.Name = Name
            Me.ID = Id
        End Sub

        Public Sub New(ByVal Name As String, ByVal Id As String, ByVal Description As String)
            Me.New(Name, Id)
            Me.Description = Description
        End Sub

        Public Function Clone() As Object Implements System.ICloneable.Clone

            Dim rxn As Reaction = ObjectCopy(Me)
            rxn.ID = Guid.NewGuid.ToString

            Return rxn

        End Function

        Function ObjectCopy(ByVal obj As Reaction) As Reaction

            Dim objMemStream As New IO.MemoryStream(100000)
            Dim objBinaryFormatter As New BinaryFormatter(Nothing, New StreamingContext(StreamingContextStates.Clone))

            objBinaryFormatter.Serialize(objMemStream, obj)

            objMemStream.Seek(0, SeekOrigin.Begin)

            ObjectCopy = objBinaryFormatter.Deserialize(objMemStream)

            objMemStream.Close()

        End Function

#End Region

        Public Overrides Function ToString() As String
            If Name <> "" Then
                Return Name
            Else
                Return MyBase.ToString()
            End If
        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            XMLSerializer.Deserialize(Me, data)

            Dim ci As CultureInfo = CultureInfo.InvariantCulture
            For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "Compounds").Elements
                Me._Components.Add(xel2.@Name, New ReactionStoichBase(xel2.@Name, Double.Parse(xel2.@StoichCoeff, ci), xel2.@IsBaseReactant, Double.Parse(xel2.@DirectOrder, ci), Double.Parse(xel2.@ReverseOrder, ci)))
            Next

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Dim elements As List(Of System.Xml.Linq.XElement) = XMLSerializer.Serialize(Me)
            Dim ci As CultureInfo = CultureInfo.InvariantCulture

            With elements

                .Add(New XElement("Compounds"))
                For Each rsb As ReactionStoichBase In Me.Components.Values
                    .Item(.Count - 1).Add(New XElement("Compound", New XAttribute("Name", rsb.CompName),
                                                New XAttribute("StoichCoeff", rsb.StoichCoeff.ToString(ci)),
                                                New XAttribute("DirectOrder", rsb.DirectOrder.ToString(ci)),
                                                New XAttribute("ReverseOrder", rsb.ReverseOrder.ToString(ci)),
                                                New XAttribute("IsBaseReactant", rsb.IsBaseReactant)))
                Next

            End With

            Return elements

        End Function

        Public Property BaseReactant As String = "" Implements IReaction.BaseReactant

        Public ReadOnly Property Components As Dictionary(Of String, IReactionStoichBase) Implements IReaction.Components
            Get
                'Return _Components.ToDictionary(Of String, IReactionStoichBase)(Function(k) k.Key, Function(k) k.Value)
                Return _Components
            End Get
        End Property

        Public Property Description As String = "" Implements IReaction.Description

        Public Property Equation As String = "" Implements IReaction.Equation

        Public Property ID As String = "" Implements IReaction.ID

        Public Property Name As String = "" Implements IReaction.Name

        Public Property ReactionBasis As Enums.ReactionBasis = ReactionBasis.Fugacity Implements IReaction.ReactionBasis

        Public Property ReactionHeat As Double Implements IReaction.ReactionHeat

        Public Property ReactionHeatCO As Double Implements IReaction.ReactionHeatCO

        Public Property ReactionPhase As Enums.PhaseName Implements IReaction.ReactionPhase

        Public Property ReactionType As Enums.ReactionType Implements IReaction.ReactionType

        Public Property StoichBalance As Double Implements IReaction.StoichBalance

        Public Property A_Forward As Double Implements IReaction.A_Forward

        Public Property A_Reverse As Double Implements IReaction.A_Reverse

        Public Property Approach As Double Implements IReaction.Approach

        Public Property ConcUnit As String = "" Implements IReaction.ConcUnit

        Public Property ConstantKeqValue As Double Implements IReaction.ConstantKeqValue

        Public Property E_Forward As Double Implements IReaction.E_Forward

        Public Property E_Reverse As Double Implements IReaction.E_Reverse

        Private _Expression As String = ""

        Public Property Expression As String Implements IReaction.Expression
            Get
                Return _Expression
            End Get
            Set(value As String)
                _Expression = value
                _ExpressionChanged = True
            End Set
        End Property
        Public Property KExprType As Enums.KOpt Implements IReaction.KExprType

        Public Property Kvalue As Double Implements IReaction.Kvalue

        Public Property Rate As Double Implements IReaction.Rate

        Public Property RateEquationDenominator As String = "" Implements IReaction.RateEquationDenominator

        Public Property RateEquationNumerator As String = "" Implements IReaction.RateEquationNumerator

        Public Property ReactionGibbsEnergy As Double Implements IReaction.ReactionGibbsEnergy

        Public Property Tmax As Double = 2000.0 Implements IReaction.Tmax

        Public Property Tmin As Double = 0.0 Implements IReaction.Tmin

        Public Property VelUnit As String = "" Implements IReaction.VelUnit

        Public Property ReactionKinFwdType As ReactionKineticType = ReactionKineticType.Arrhenius Implements IReaction.ReactionKinFwdType

        Public Property ReactionKinRevType As ReactionKineticType = ReactionKineticType.Arrhenius Implements IReaction.ReactionKinRevType

        Public Property ReactionKinFwdExpression As String = "" Implements IReaction.ReactionKinFwdExpression

        Public Property ReactionKinRevExpression As String = "" Implements IReaction.ReactionKinRevExpression

        Public Property E_Forward_Unit As String = "J/mol" Implements IReaction.E_Forward_Unit

        Public Property E_Reverse_Unit As String = "J/mol" Implements IReaction.E_Reverse_Unit

        Public Property ReactionKinetics As ReactionKinetics = ReactionKinetics.Expression Implements IReaction.ReactionKinetics

        Public Property ScriptTitle As String = "" Implements IReaction.ScriptTitle

        Public Property EquilibriumReactionBasisUnits As String = "Pa" Implements IReaction.EquilibriumReactionBasisUnits

        Public Function EvaluateK1(T As Double, PP As IPropertyPackage) As Double Implements IReaction.EvaluateK
            Return EvaluateK(T, PP)
        End Function

        Public Function GetPropertyList() As String() Implements IReaction.GetPropertyList

            Return New String() {"Kinetic_A_Forward", "Kinetic_A_Reverse", "Kinetic_E_Forward", "Kinetic_E_Reverse", "Tmin", "Tmax", "Equilibrium_ConstantKeqValue", "Conversion_Value"}

        End Function

        Public Function GetPropertyValue(prop As String) As Double Implements IReaction.GetPropertyValue

            Select Case prop
                Case "Kinetic_A_Forward"
                    Return A_Forward
                Case "Kinetic_A_Reverse"
                    Return A_Reverse
                Case "Kinetic_E_Forward"
                    Return E_Forward
                Case "Kinetic_E_Reverse"
                    Return E_Reverse
                Case "Tmin"
                    Return Tmin
                Case "Tmax"
                    Return Tmax
                Case "Equilibrium_ConstantKeqValue"
                    Return ConstantKeqValue
                Case "Conversion_Value"
                    Return Expression.ToDoubleFromInvariant
                Case Else
                    Return 0.0
            End Select

        End Function

        Public Sub SetPropertyValue(prop As String, value As Double) Implements IReaction.SetPropertyValue

            Select Case prop
                Case "Kinetic_A_Forward"
                    A_Forward = value
                Case "Kinetic_A_Reverse"
                    A_Reverse = value
                Case "Kinetic_E_Forward"
                    E_Forward = value
                Case "Kinetic_E_Reverse"
                    E_Reverse = value
                Case "Tmin"
                    Tmin = value
                Case "Tmax"
                    Tmax = value
                Case "Equilibrium_ConstantKeqValue"
                    ConstantKeqValue = value
                Case "Conversion_Value"
                    If value.IsValidDouble Then Expression = value.ToString(Globalization.CultureInfo.InvariantCulture)
            End Select

        End Sub

    End Class

    <System.Serializable()> Public Class ReactionSet

        Implements ICloneable, ICustomXMLSerialization

        'CAPE-OPEN Reaction Package Interfaces
        Implements CapeOpen.ICapeIdentification
        Implements CapeOpen.ICapeUtilities, CapeOpen.ICapeCollection, CapeOpen.ICapeReactionsRoutine, CapeOpen.ICapeReactionChemistry
        Implements CapeOpen.ICapeThermoContext, CapeOpen.ICapeKineticReactionContext, CapeOpen.ICapeReactionProperties
        Implements CapeOpen.ICapeThermoMaterialContext

        Implements IReactionSet

        Protected m_reactionset As Dictionary(Of String, IReactionSetBase)

#Region "    DWSIM Specific"

        Public Function GetIDbyName(ByVal reactname As String)
            Dim ID As String = ""
            For Each r As Reaction In Me.m_pme.Reactions.Values
                If r.Name = reactname Then
                    ID = r.ID
                    Exit For
                End If
            Next
            Return ID
        End Function

        Public ReadOnly Property Reactions() As Dictionary(Of String, IReactionSetBase) Implements IReactionSet.Reactions
            Get
                Return m_reactionset
            End Get
        End Property

        Public Property ID() As String = "" Implements IReactionSet.ID

        Public Property Name() As String = "" Implements IReactionSet.Name

        Public Property Description() As String = "" Implements IReactionSet.Description

        Sub New()
            MyBase.New()
            Me.m_reactionset = New Dictionary(Of String, IReactionSetBase)
        End Sub

        Sub New(ByVal id As String, ByVal name As String, ByVal description As String)
            Me.New()
            Me.ID = id
            Me.Name = name
            Me.Description = description
        End Sub

        Public Overrides Function ToString() As String
            If Name <> "" Then
                Return Name
            Else
                Return MyBase.ToString()
            End If
        End Function

        Public Function Clone() As Object Implements System.ICloneable.Clone

            Dim rxs As ReactionSet = ObjectCopy(Me)
            rxs.ID = Guid.NewGuid.ToString

            Return rxs

        End Function

        Function ObjectCopy(ByVal obj As ReactionSet) As ReactionSet

            Dim objMemStream As New IO.MemoryStream(500000)
            Dim objBinaryFormatter As New BinaryFormatter(Nothing, New StreamingContext(StreamingContextStates.Clone))

            objBinaryFormatter.Serialize(objMemStream, obj)

            objMemStream.Seek(0, SeekOrigin.Begin)

            ObjectCopy = objBinaryFormatter.Deserialize(objMemStream)

            objMemStream.Close()
        End Function

#End Region

#Region "    CAPE-OPEN Reaction Package Methods and Properties"

        Protected m_params As New CapeOpen.ParameterCollection
        Protected m_str As Streams.MaterialStream
        <System.NonSerialized()> Protected m_pme As IFlowsheet
        Protected m_kre As Reaction

        Public Function Count() As Integer Implements CapeOpen.ICapeCollection.Count
            Return m_params.Count
        End Function

        Public Function Item(ByVal index As Object) As Object Implements CapeOpen.ICapeCollection.Item
            Dim mypar As Object = Nothing
            If IsNumeric(index) Then
                mypar = m_params(index - 1)
                Return mypar
            Else
                For Each p As CapeOpen.ICapeIdentification In m_params
                    If p.ComponentName = index Then
                        mypar = p
                        Exit For
                    End If
                Next
                Return mypar
            End If
        End Function

        ''' <summary>
        ''' Returns the name of the base reactant for a particular reaction.
        ''' </summary>
        ''' <param name="reacId">The reaction identifier</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function GetBaseReactant(ByVal reacId As String) As String Implements CapeOpen.ICapeReactionChemistry.GetBaseReactant
            Return Me.m_pme.Reactions(GetIDbyName(reacId)).BaseReactant
        End Function

        ''' <summary>
        ''' Gets the number of compounds occurring in a particular reaction within a Reactions Package.
        ''' </summary>
        ''' <param name="reacID">The reaction identifier</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function GetNumberOfReactionCompounds(ByVal reacID As String) As Integer Implements CapeOpen.ICapeReactionChemistry.GetNumberOfReactionCompounds
            Return Me.m_pme.Reactions(GetIDbyName(reacID)).Components.Count
        End Function

        ''' <summary>
        ''' Gets the number of reactions contained in the Reactions Package.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function GetNumberOfReactions() As Integer Implements CapeOpen.ICapeReactionChemistry.GetNumberOfReactions
            Return Me.Reactions.Count
        End Function

        ''' <summary>
        ''' Returns the number and ids of the compounds in the specified phase.
        ''' </summary>
        ''' <param name="reacID">Label of the required phase</param>
        ''' <param name="compNo"></param>
        ''' <param name="compIds">The ids of the compounds present in the specified phase.</param>
        ''' <remarks></remarks>
        Public Sub GetPhaseCompounds(ByVal reacID As String, ByRef compNo As Integer, ByRef compIds As Object) Implements CapeOpen.ICapeReactionChemistry.GetPhaseCompounds
            Throw New CapeOpen.CapeNoImplException()
        End Sub

        ''' <summary>
        ''' Get the identifiers of the components participating in the specified reaction within the reaction set defined in the
        ''' Reactions Package.
        ''' </summary>
        ''' <param name="reacId">The reaction identifier</param>
        ''' <param name="compIds">List of compound IDs</param>
        ''' <param name="compCharge">The charge for each compound</param>
        ''' <param name="compCASNumber">The CAS Registry numbers for the compounds</param>
        ''' <remarks></remarks>
        Public Sub GetReactionCompoundIds(ByVal reacId As String, ByRef compIds As Object, ByRef compCharge As Object, ByRef compCASNumber As Object) Implements CapeOpen.ICapeReactionChemistry.GetReactionCompoundIds
            Dim i As Integer = 0
            Dim narr, carr, charr As New ArrayList
            Dim comps = m_pme.SelectedCompounds.Values.ToList()
            Dim n As Integer = comps.Count - 1
            For Each c As ReactionStoichBase In Me.m_pme.Reactions(GetIDbyName(reacId)).Components.Values
                With Me.m_pme.SelectedCompounds(c.CompName)
                    For i = 0 To n
                        If comps(i).CAS_Number = .CAS_Number Then
                            narr.Add(comps(i).Name)
                            carr.Add(comps(i).CAS_Number)
                            charr.Add(Convert.ToDouble(comps(i).Charge))
                            Exit For
                        End If
                    Next
                End With
            Next
            Dim names(narr.Count - 1), casids(narr.Count - 1) As String, charges(narr.Count - 1) As Double
            Array.Copy(narr.ToArray, names, narr.Count)
            Array.Copy(carr.ToArray, casids, narr.Count)
            Array.Copy(charr.ToArray, charges, narr.Count)
            compIds = names
            compCharge = charges
            compCASNumber = casids
        End Sub

        ''' <summary>
        ''' Gets the concentration basis required that will be used by a particular reaction in its rate equation.
        ''' Qualifiers defined in the THRM spec can be used here (i.e. “fugacity”, “moleFraction”, etc)
        ''' </summary>
        ''' <param name="reacId">The reaction identifier</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function GetReactionConcBasis(ByVal reacId As String) As String Implements CapeOpen.ICapeReactionChemistry.GetReactionConcBasis
            Select Case Me.m_pme.Reactions(GetIDbyName(reacId)).ReactionBasis
                Case ReactionBasis.Activity
                    Return "activity"
                Case ReactionBasis.Fugacity
                    Return "fugacity"
                Case ReactionBasis.MassConc
                    Return "concentration"
                Case ReactionBasis.MassFrac
                    Return "massfraction"
                Case ReactionBasis.MolarConc
                    Return "molarity"
                Case ReactionBasis.MolarFrac
                    Return "molefraction"
                Case ReactionBasis.PartialPress
                    Return "partialpressure"
                Case Else
                    Throw New CapeOpen.CapeNoImplException
            End Select
        End Function

        ''' <summary>
        ''' Returns a collection containing the rate expression parameters for a particular reaction.
        ''' </summary>
        ''' <param name="reacId">Identifier of a particular reaction</param>
        ''' <returns></returns>
        ''' <remarks>GetReactionParameters returns a collection of CAPE-OPEN parameters [6] that characterize the rate expression
        ''' used by the reaction model in a Reaction Package. For a PowerLaw model this collection would contain
        ''' parameters for activation energy, pre-exponential factor and compound exponents for example. It is up to the
        ''' Reactions Package implementor to decide whether a client can update the values of these parameters. If this
        ''' operation is allowed, then the implementor must also provide support for persistence [5] interfaces, so that the
        ''' updated values can be saved and restored. In this case the COSE is also responsible for calling the persistence
        ''' methods.
        ''' Deliberately, the standard does not define the names of the parameters that may appear in such a collection, even
        ''' for well-known reaction models, such as PowerLaw and Langmuir – Hinshelwood – Hougen – Watson
        ''' (LHHW). This is because the formulation of well-known models is not fixed, and because the standard needs to
        ''' support custom models as well as the well-known models.
        ''' This decision is not expected to be restrictive: in most cases the (software) client of a Reactions Package does
        ''' not need to know what model the package implements and what parameters it has. However, the parameters may
        ''' be of interest to an end-user who wants to adjust or estimate the parameter values. In these cases the COSE can
        ''' invoke the Reaction Package’s own GUI, or, if it doesn’t have one, present the parameters in a generic grid. It is
        ''' the Reaction Package implementor’s responsibility to provide documentation for the parameters so that an enduser
        ''' can understand how they are used.</remarks>
        Public Function GetReactionParameters(ByVal reacId As String) As Object Implements CapeOpen.ICapeReactionChemistry.GetReactionParameters
            Throw New CapeOpen.CapeNoImplException("GetReactionParameters not implemented.")
        End Function

        ''' <summary>
        ''' Gets the phase on which a particular reaction contained in the Reactions Package will take place.
        ''' </summary>
        ''' <param name="reacId">The reaction identifier</param>
        ''' <returns></returns>
        ''' <remarks>The string returned by this method must match one of the phase labels known to the Property Package.</remarks>
        Public Function GetReactionPhase(ByVal reacId As String) As String Implements CapeOpen.ICapeReactionChemistry.GetReactionPhase
            Select Case Me.m_pme.Reactions(GetIDbyName(reacId)).ReactionPhase
                Case PhaseName.Vapor
                    Return "Vapor"
                Case PhaseName.Liquid
                    Return "Liquid"
                Case Else
                    Return "Overall"
            End Select
        End Function

        ''' <summary>
        ''' Gets the phase on which the reactions contained in the package will take place. 
        ''' </summary>
        ''' <param name="reacId">The reaction identifier</param>
        ''' <returns></returns>
        ''' <remarks>The reaction rate basis (i.e.
        ''' “Homogeneous” or “Heterogeneous”) Homogeneous reactions will be provided in kgmole/h/m3 and
        ''' heterogeneous will be provided in kgmole/h/kg-cat.
        ''' CapeReactionRateBasis:
        ''' CAPE_HOMOGENEOUS = 0,
        ''' CAPE_HETEROGENEOUS = 1,</remarks>
        Public Function GetReactionRateBasis(ByVal reacId As String) As CapeOpen.CapeReactionRateBasis Implements CapeOpen.ICapeReactionChemistry.GetReactionRateBasis
            Return CapeOpen.CapeReactionRateBasis.CAPE_HOMOGENEOUS
        End Function

        ''' <summary>
        ''' Returns the identifiers of all the reactions contained within the Reactions Package.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function GetReactionsIds() As Object Implements CapeOpen.ICapeReactionChemistry.GetReactionsIds
            Dim narr As New ArrayList
            For Each r As ReactionSetBase In Me.Reactions.Values
                narr.Add(Me.m_pme.Reactions(r.ReactionID).Name)
            Next
            Dim names(narr.Count - 1) As String
            Array.Copy(narr.ToArray, names, narr.Count)
            Return names
        End Function

        ''' <summary>
        ''' Returns the type of a particular reaction contained in the Reactions Package.
        ''' </summary>
        ''' <param name="reacID">The reaction identifier</param>
        ''' <returns>Returns the type of a particular reaction contained in the Reactions Package. CapeReactionType constants for the
        ''' various reaction:
        ''' CAPE_EQUILIBRIUM = 0,
        ''' CAPE_KINETIC = 1,</returns>
        ''' <remarks></remarks>
        Public Function GetReactionType(ByVal reacID As String) As CapeOpen.CapeReactionType Implements CapeOpen.ICapeReactionChemistry.GetReactionType
            Select Case Me.m_pme.Reactions(GetIDbyName(reacID)).ReactionType
                Case ReactionType.Conversion
                    Return CapeOpen.CapeReactionType.CAPE_KINETIC
                Case ReactionType.Equilibrium
                    Return CapeOpen.CapeReactionType.CAPE_EQUILIBRIUM
                Case ReactionType.Heterogeneous_Catalytic
                    Return CapeOpen.CapeReactionType.CAPE_KINETIC
                Case ReactionType.Kinetic
                    Return CapeOpen.CapeReactionType.CAPE_KINETIC
            End Select
        End Function

        ''' <summary>
        ''' Returns the stoichiometric coefficients of the specified reaction (positive numbers indicate products, negative
        ''' numbers indicate reactants)
        ''' </summary>
        ''' <param name="reacId">The reaction identifier</param>
        ''' <returns></returns>
        ''' <remarks>The array of coefficients returned by this method is parallel to the array returned by calling
        ''' GetReactionCompuoundIds meaning that the first coefficient corresponds to the first compound and so on.</remarks>
        Public Function GetStoichiometricCoefficients(ByVal reacId As String) As Object Implements CapeOpen.ICapeReactionChemistry.GetStoichiometricCoefficients
            Dim narr As New ArrayList
            For Each c As ReactionStoichBase In Me.m_pme.Reactions(GetIDbyName(reacId)).Components.Values
                narr.Add(c.StoichCoeff)
            Next
            Dim sc(narr.Count - 1) As Double
            Array.Copy(narr.ToArray, sc, narr.Count)
            Return sc
        End Function

        Public Sub Edit() Implements CapeOpen.ICapeUtilities.Edit
            'Dim rm As New FormReacManager
            'rm.Show()
            Throw New NotImplementedException
        End Sub

        Public Sub Initialize() Implements CapeOpen.ICapeUtilities.Initialize
            If m_params Is Nothing Then
                m_params = New CapeOpen.ParameterCollection
                'm_params.Add(...)
            End If
        End Sub

        Public ReadOnly Property parameters() As Object Implements CapeOpen.ICapeUtilities.parameters
            Get
                Return m_params
            End Get
        End Property

        Public WriteOnly Property simulationContext() As Object Implements CapeOpen.ICapeUtilities.simulationContext
            Set(ByVal value As Object)
                m_pme = value
            End Set
        End Property

        Public Sub Terminate() Implements CapeOpen.ICapeUtilities.Terminate
            'do nothing
        End Sub

        Public Sub SetReactionObject(ByRef reactionsObject As Object) Implements CapeOpen.ICapeKineticReactionContext.SetReactionObject
            Me.m_kre = reactionsObject
        End Sub

        ''' <summary>
        ''' Gets the value of the specified reaction property within a reactions object.\</summary>
        ''' <param name="property">The Reaction Property to be got.</param>
        ''' <param name="phase">The qualified phase for the Reaction Property.</param>
        ''' <param name="reacIds">The qualified reactions for the Reaction Property. NULL to specify all reactions in the set.</param>
        ''' <param name="basis">Qualifies the basis of the Reaction Property (i.e., mass /mole). Default is mole. Use NULL only 
        ''' as a placeholder for property for which basis does not apply. This qualifier could be extended with values such as 
        ''' activity, fugacity, fractions, molality…This way when an equilibrium constant is requested its basis can be specified</param>
        ''' <returns></returns>
        ''' <remarks>The qualifiers passed in determine the reactions, phase and calculation basis for 
        ''' which the property will be got. The order of the array is the same as in the passed in reacIds 
        ''' array (i.e. property value for reaction reacIds[1] will be stored in property[1])</remarks>
        Public Function GetReactionProp(ByVal [property] As String, ByVal phase As String, ByVal reacIds As Object, ByVal basis As String) As Object Implements CapeOpen.ICapeReactionProperties.GetReactionProp
            Dim res As New ArrayList
            For Each rid As String In reacIds
                Dim ro As Reaction = Me.m_pme.Reactions(GetIDbyName(rid))
                With ro
                    Select Case [property].ToLower
                        Case "reactionrate"
                            res.Add(ro.Rate)
                        Case "chemicalequilibriumconstant"
                            res.Add(ro.Kvalue)
                        Case "enthalpyofreaction"
                            Select Case basis.ToLower
                                Case "mole"
                                    res.Add(ro.ReactionHeatCO)
                                Case "mass"
                                    res.Add(ro.ReactionHeatCO / Me.m_str.Phases(0).Properties.molecularWeight.GetValueOrDefault)
                            End Select
                        Case Else
                            Throw New CapeOpen.CapeNoImplException
                    End Select
                End With
            Next
            Dim propvals(res.Count - 1) As Double
            Array.Copy(res.ToArray, propvals, res.Count)
            Return propvals
        End Function

        ''' <summary>
        ''' Sets the values of the specified reaction property within a reactions object. The qualifiers passed in determine the
        ''' reactions, phase and calculation basis for which the property will be got
        ''' </summary>
        ''' <param name="property">The Reaction Property to be got.</param>
        ''' <param name="phase">The qualified phase for the Reaction Property.</param>
        ''' <param name="reacIds">The qualified reactions for the Reaction Property. NULL to specify all reactions in the set.</param>
        ''' <param name="basis">Qualifies the basis of the Reaction Property (i.e., mass /mole).
        ''' Default is mole. Use NULL only as a placeholder for property
        ''' for which basis does not apply.
        ''' This qualifier could be extended with values such as activity,
        ''' fugacity, fractions, molality…This way when an equilibrium
        ''' constant is requested its basis can be specified</param>
        ''' <param name="propVals">The values of the requested reaction property. The order of the
        ''' array is the same as in the passed in reacIds array (i.e. property
        ''' value for reaction reacIds[1] will be stored in property[1])</param>
        ''' <remarks></remarks>
        Public Sub SetReactionProp(ByVal [property] As String, ByVal phase As String, ByVal reacIds As Object, ByVal basis As String, ByVal propVals As Object) Implements CapeOpen.ICapeReactionProperties.SetReactionProp
            Dim i As Integer = 0
            For Each rid As String In reacIds
                Dim ro As Reaction = Me.m_pme.Reactions(GetIDbyName(rid))
                With ro
                    Select Case [property].ToLower
                        Case "reactionrate"
                            ro.Rate = propVals(i)
                        Case "chemicalequilibriumconstant"
                            ro.Kvalue = propVals(i)
                        Case "enthalpyofreaction"
                            ro.ReactionHeatCO = propVals(i)
                        Case Else
                            Throw New CapeOpen.CapeNoImplException
                    End Select
                End With
                i += 1
            Next
        End Sub

        ''' <summary>
        ''' The Reactions Package is passed a list of reaction properties to be calculated.
        ''' </summary>
        ''' <param name="props">The Reaction Properties to be calculated.</param>
        ''' <param name="phase">The qualified phase for the results.</param>
        ''' <param name="reacIds">The qualified reactions for the results. NULL to specify all 
        ''' reactions in the set.</param>
        ''' <param name="basis">Qualifies the basis of the result (i.e., mass /mole). Default is
        ''' mole. Use NULL only as a placeholder for properties for which
        ''' basis does not apply.</param>
        ''' <remarks>The Reactions Package is passed a list of reaction properties to be calculated, the reaction IDS for which the
        ''' properties are required, and the calculation basis for the reaction properties (i.e. mole or mass). A material object
        ''' containing the thermodynamic state variables that need to be used for calculating the reaction properties (e.g. T,
        ''' P and compositions) is passed separately via a call to the setMaterial method of the Reaction Package’s
        ''' ICapeThermoContext interface.
        ''' The results of the calculation will be written to the reaction object passed to the Reactions Package via either the
        ''' ICapeKineticReactionContext interface for a kinetic reaction package, or the ICapeElectrolyteReactionContext
        ''' interface for an Electrolyte Property Package.</remarks>
        Public Sub CalcReactionProp(ByVal props As Object, ByVal phase As String, ByVal reacIds As Object, ByVal basis As String) Implements CapeOpen.ICapeReactionsRoutine.CalcReactionProp

            For Each rid As String In reacIds

                Dim ro As Reaction = Me.m_pme.Reactions(GetIDbyName(rid))

                With ro
                    For Each p As String In props
                        Select Case p.ToLower
                            Case "reactionrate"

                                Dim ims As Streams.MaterialStream = Me.m_str
                                Dim co As New Dictionary(Of String, Double)

                                'initial mole flows

                                For Each sb As ReactionStoichBase In .Components.Values

                                    Select Case ro.ReactionPhase
                                        Case PhaseName.Liquid
                                            co.Add(sb.CompName, ims.Phases(1).Compounds(sb.CompName).MolarFlow.GetValueOrDefault / ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault)
                                        Case PhaseName.Vapor
                                            co.Add(sb.CompName, ims.Phases(2).Compounds(sb.CompName).MolarFlow.GetValueOrDefault / ims.Phases(2).Properties.volumetric_flow.GetValueOrDefault)
                                        Case PhaseName.Mixture
                                            co.Add(sb.CompName, ims.Phases(0).Compounds(sb.CompName).MolarFlow.GetValueOrDefault / ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault)
                                    End Select

                                Next

                                Dim T = ims.Phases(0).Properties.temperature.GetValueOrDefault

                                Dim kxf As Double = ro.A_Forward * Math.Exp(-ro.E_Forward / (8.314 * T))
                                Dim kxr As Double = ro.A_Reverse * Math.Exp(-ro.E_Reverse / (8.314 * T))

                                Dim rx As Double = 0
                                Dim rxf As Double = 1
                                Dim rxr As Double = 1

                                'kinetic expression

                                For Each sb As ReactionStoichBase In ro.Components.Values
                                    rxf *= co(sb.CompName) ^ sb.DirectOrder
                                    rxr *= co(sb.CompName) ^ sb.ReverseOrder
                                Next

                                rx = kxf * rxf - kxr * rxr

                                ro.Rate = rx

                            Case "chemicalequilibriumconstant"

                                Dim T = Me.m_str.Phases(0).Properties.temperature.GetValueOrDefault

                                'equilibrium constant calculation

                                Select Case .KExprType
                                    Case KOpt.Constant
                                        .Kvalue = .ConstantKeqValue
                                    Case KOpt.Expression
                                        'If .ExpContext Is Nothing Then
                                        '    .ExpContext = New Ciloci.Flee.ExpressionContext
                                        '    .ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture
                                        '    With .ExpContext
                                        '        .Imports.AddType(GetType(System.Math))
                                        '    End With
                                        'End If
                                        '.ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture
                                        '.ExpContext.Variables.Clear()
                                        '.ExpContext.Variables.Add("T", T)
                                        '.Expr = .ExpContext.CompileGeneric(Of Double)(.Expression)
                                        '.Kvalue = Math.Exp(.Expr.Evaluate)
                                    Case KOpt.Gibbs
                                        Dim id(.Components.Count - 1) As String
                                        Dim stcoef(.Components.Count - 1) As Double
                                        Dim bcidx As Integer = 0
                                        Dim j As Integer = 0
                                        For Each sb As ReactionStoichBase In .Components.Values
                                            id(j) = sb.CompName
                                            stcoef(j) = sb.StoichCoeff
                                            If sb.IsBaseReactant Then bcidx = j
                                            j += 1
                                        Next
                                        Dim DelG_RT = Me.m_str.PropertyPackage.AUX_DELGig_RT(298.15, T, id, stcoef, bcidx)
                                        .Kvalue = Math.Exp(-DelG_RT)
                                End Select

                            Case "enthalpyofreaction"

                                'Dim rh As Double = 0.0#

                                'Dim T = Me.m_str.Phases(0).Properties.temperature.GetValueOrDefault

                                'Dim id(.Components.Count - 1) As String
                                'Dim stcoef(.Components.Count - 1) As Double
                                'Dim bcidx As Integer = 0
                                'Dim j As Integer = 0
                                'For Each sb As ReactionStoichBase In .Components.Values
                                '    id(j) = sb.CompName
                                '    stcoef(j) = sb.StoichCoeff
                                '    If sb.IsBaseReactant Then bcidx = j
                                '    j += 1
                                'Next

                                'Me.m_str.PropertyPackage.CurrentMaterialStream = Me.m_str
                                'rh = Me.m_str.PropertyPackage.AUX_DELHig_RT(298.15, T, id, stcoef, bcidx) * 8.314 * T

                                .ReactionHeatCO = .ReactionHeat

                            Case Else

                                Throw New CapeOpen.CapeNoImplException

                        End Select
                    Next
                End With
            Next

        End Sub

        Public Sub SetMaterial(ByVal materialObject As Object) Implements CapeOpen.ICapeThermoContext.SetMaterial
            If Not System.Runtime.InteropServices.Marshal.IsComObject(materialObject) Then
                Me.m_str = materialObject
            Else
                'get ID
                Dim id As String = CType(materialObject, CapeOpen.ICapeIdentification).ComponentDescription
                Dim myms As IMaterialStream = Me.m_pme.SimulationObjects(id)
                'proceed with copy
                Me.m_str = myms
            End If
        End Sub

        Public Property ComponentDescription() As String = "" Implements CapeOpen.ICapeIdentification.ComponentDescription

        Public Property ComponentName() As String = "" Implements CapeOpen.ICapeIdentification.ComponentName

        Public Sub SetMaterial1(ByVal material As Object) Implements CapeOpen.ICapeThermoMaterialContext.SetMaterial
            If Not System.Runtime.InteropServices.Marshal.IsComObject(material) Then
                Me.m_str = material
            Else
                'get ID
                Dim id As String = CType(material, CapeOpen.ICapeIdentification).ComponentDescription
                Dim myms As IMaterialStream = Me.m_pme.SimulationObjects(id)
                'proceed with copy
                Me.m_str = myms
            End If

        End Sub

        Public Sub UnsetMaterial() Implements CapeOpen.ICapeThermoMaterialContext.UnsetMaterial
            Me.m_str = Nothing
        End Sub

#End Region

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            Me.ID = (From xel As XElement In data Select xel Where xel.Name = "ID").SingleOrDefault.Value
            Me.Name = (From xel As XElement In data Select xel Where xel.Name = "Name").SingleOrDefault.Value
            Me.Description = (From xel As XElement In data Select xel Where xel.Name = "Description").SingleOrDefault.Value

            For Each xel2 As XElement In (From xel As XElement In data Select xel Where xel.Name = "Reactions").Elements
                Me.m_reactionset.Add(xel2.@Key, New ReactionSetBase(xel2.@ReactionID, xel2.@Rank, xel2.@IsActive))
            Next

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Dim elements As New List(Of System.Xml.Linq.XElement)
            Dim ci As CultureInfo = CultureInfo.InvariantCulture

            With elements

                .Add(New XElement("ID", ID))
                .Add(New XElement("Name", Name))
                .Add(New XElement("Description", Description))

                .Add(New XElement("Reactions"))

                For Each kvp As KeyValuePair(Of String, IReactionSetBase) In Reactions
                    .Item(.Count - 1).Add(New XElement("Reaction", New XAttribute("Key", kvp.Key),
                                                                New XAttribute("ReactionID", kvp.Value.ReactionID),
                                                                New XAttribute("Rank", kvp.Value.Rank),
                                                                New XAttribute("IsActive", kvp.Value.IsActive)))
                Next

            End With

            Return elements

        End Function

    End Class

    <System.Serializable()> Public Class ReactionSetBase

        Implements IReactionSetBase

        Sub New()

        End Sub

        Sub New(ByVal id As String, ByVal rank As Integer, ByVal isactive As Boolean)
            Me.IsActive = isactive
            Me.Rank = rank
            Me.ReactionID = id
        End Sub

        Public Property IsActive As Boolean Implements IReactionSetBase.IsActive

        Public Property Rank As Integer Implements IReactionSetBase.Rank

        Public Property ReactionID As String = "" Implements IReactionSetBase.ReactionID

    End Class

    <System.Serializable()> Public Class ReactionStoichBase

        Implements IReactionStoichBase

        Public Sub New(ByVal name As String, ByVal stoichcoeff As Double, ByVal isbasereactant As Boolean, ByVal directorder As Double, ByVal reversorder As Double)
            Me.CompName = name
            Me.StoichCoeff = stoichcoeff
            Me.IsBaseReactant = isbasereactant
            Me.DirectOrder = directorder
            Me.ReverseOrder = reversorder
        End Sub

        Public Property CompName As String = "" Implements IReactionStoichBase.CompName

        Public Property DirectOrder As Double Implements IReactionStoichBase.DirectOrder

        Public Property IsBaseReactant As Boolean Implements IReactionStoichBase.IsBaseReactant

        Public Property ReverseOrder As Double Implements IReactionStoichBase.ReverseOrder

        Public Property StoichCoeff As Double Implements IReactionStoichBase.StoichCoeff

    End Class

    <System.Serializable()> Public Class ReactionsCollection
        Public Collection() As Reaction
        Sub New()

        End Sub
    End Class

End Namespace
