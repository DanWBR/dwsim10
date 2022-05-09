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

        Implements IReactionSet

        Protected m_reactionset As Dictionary(Of String, IReactionSetBase)

#Region "    DWSIM Specific"

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
