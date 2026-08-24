Imports System.Globalization
Imports DWSIM.SharedClasses.ExpressionEvaluator

''' <summary>A node of a parsed expression.</summary>
Public MustInherit Class ExpressionNode

    Public MustOverride Function Evaluate(vars As VariableTable) As ExpressionValue

    ''' <summary>
    ''' The type this node produces, worked out without evaluating it.
    ''' </summary>
    ''' <remarks>
    ''' Flee types the whole tree when it compiles, and the type of a node can depend on a branch
    ''' that never runs: the arms of an <c>if</c> are brought to a common type, so
    ''' <c>1/if(true,3,2.5)</c> is 0.333 and not the 0 that integer division of the taken arm alone
    ''' would give. Evaluating lazily cannot see that, so the type is inferred separately.
    ''' </remarks>
    Public MustOverride Function InferKind() As ValueKind

End Class

''' <summary>A literal.</summary>
Public NotInheritable Class ConstantNode
    Inherits ExpressionNode

    Private ReadOnly _value As ExpressionValue

    Public Sub New(value As ExpressionValue)
        _value = value
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue
        Return _value
    End Function

    Public Overrides Function InferKind() As ValueKind
        Return _value.Kind
    End Function

End Class

''' <summary>
''' A bare name: one of <c>Math</c>'s constants, or a variable.
''' </summary>
''' <remarks>
''' The order matters and follows Flee: an imported type's members are resolved before the variable
''' table, so a flowsheet that happens to define a variable called <c>e</c> or <c>pi</c> still reads
''' the constant. Keeping that is what makes existing compound equations - which use <c>pi</c> freely -
''' evaluate as they did.
''' </remarks>
Public NotInheritable Class NameNode
    Inherits ExpressionNode

    Private ReadOnly _name As String
    Private ReadOnly _pos As Integer

    Public Sub New(name As String, pos As Integer)
        _name = name
        _pos = pos
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Select Case _name.ToLowerInvariant()
            Case "pi" : Return ExpressionValue.FromReal(Math.PI)
            Case "e" : Return ExpressionValue.FromReal(Math.E)
        End Select

        Dim v As Double
        If vars IsNot Nothing AndAlso vars.TryGetValue(_name, v) Then Return ExpressionValue.FromReal(v)

        Throw New ExpressionEvaluationException(
            String.Format(CultureInfo.InvariantCulture,
                          "There is no variable or constant named '{0}' (position {1}).", _name, _pos + 1))

    End Function

    ''' <summary>A variable is always a double, and so are pi and e.</summary>
    Public Overrides Function InferKind() As ValueKind
        Return ValueKind.RealValue
    End Function

End Class

''' <summary>Unary minus. Keeps the operand's type, so <c>-2</c> stays an integer.</summary>
Public NotInheritable Class NegateNode
    Inherits ExpressionNode

    Private ReadOnly _operand As ExpressionNode

    Public Sub New(operand As ExpressionNode)
        _operand = operand
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Dim v = _operand.Evaluate(vars)

        Select Case v.Kind
            Case ValueKind.Int32Value
                Return ExpressionValue.FromInt(ExpressionEvaluator.WrapToInt32(-CLng(v.Whole)))
            Case ValueKind.RealValue
                Return ExpressionValue.FromReal(-v.Real)
            Case Else
                Throw New ExpressionEvaluationException("A logical value cannot be negated with '-'.")
        End Select

    End Function

    Public Overrides Function InferKind() As ValueKind
        Return _operand.InferKind()
    End Function

End Class

''' <summary>Logical negation.</summary>
Public NotInheritable Class NotNode
    Inherits ExpressionNode

    Private ReadOnly _operand As ExpressionNode

    Public Sub New(operand As ExpressionNode)
        _operand = operand
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue
        Return ExpressionValue.FromBool(Not _operand.Evaluate(vars).AsBool)
    End Function

    Public Overrides Function InferKind() As ValueKind
        Return ValueKind.BooleanValue
    End Function

End Class

''' <summary>
''' Arithmetic. Two integers give an integer - integer division, integer modulo, and wrapping on
''' overflow; anything touching a double gives a double.
''' </summary>
Public NotInheritable Class ArithmeticNode
    Inherits ExpressionNode

    Private ReadOnly _op As String
    Private ReadOnly _left As ExpressionNode
    Private ReadOnly _right As ExpressionNode

    Public Sub New(op As String, left As ExpressionNode, right As ExpressionNode)
        _op = op
        _left = left
        _right = right
    End Sub

    ''' <summary>Repeated squaring, wrapping at each step so the result matches 32-bit arithmetic.</summary>
    Private Shared Function WholePower(base As Integer, exponent As Integer) As Integer

        Dim result As Long = 1
        Dim factor As Long = base
        Dim e As Integer = exponent

        While e > 0
            If (e And 1) = 1 Then result = ExpressionEvaluator.WrapToInt32(result * factor)
            factor = ExpressionEvaluator.WrapToInt32(factor * factor)
            e >>= 1
        End While

        Return ExpressionEvaluator.WrapToInt32(result)

    End Function

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Dim a = _left.Evaluate(vars)
        Dim b = _right.Evaluate(vars)

        If Not a.IsNumber OrElse Not b.IsNumber Then
            Throw New ExpressionEvaluationException(
                String.Format(CultureInfo.InvariantCulture, "'{0}' needs two numbers.", _op))
        End If

        Dim bothWhole = a.Kind = ValueKind.Int32Value AndAlso b.Kind = ValueKind.Int32Value

        If _op = "^" Then
            ' Two whole numbers raised to a whole power stay whole, so 9^1/2 is 4 and not 4.5. A
            ' negative exponent has no whole answer and falls to a double: 2^-2 is 0.25.
            '
            ' Once the answer no longer fits in 32 bits this parts company with Flee, deliberately.
            ' Flee is not consistent with itself there: it folds a literal 2^32 to 0, having wrapped
            ' it, yet computes 2^(6^2) as 2^36 exactly, because the folding does not reach a computed
            ' exponent. No single behaviour matches both, so the arithmetic one is taken - an
            ' overflowing power widens to a double instead of wrapping to a number that means nothing.
            If bothWhole AndAlso b.Whole >= 0 Then
                Dim whole As Double = Math.Pow(a.AsReal, b.AsReal)
                If whole >= Integer.MinValue AndAlso whole <= Integer.MaxValue Then
                    Return ExpressionValue.FromInt(WholePower(a.Whole, b.Whole))
                End If
                Return ExpressionValue.FromReal(whole)
            End If
            Return ExpressionValue.FromReal(Math.Pow(a.AsReal, b.AsReal))
        End If

        If bothWhole Then
            Dim x = CLng(a.Whole), y = CLng(b.Whole)
            Select Case _op
                Case "+" : Return ExpressionValue.FromInt(ExpressionEvaluator.WrapToInt32(x + y))
                Case "-" : Return ExpressionValue.FromInt(ExpressionEvaluator.WrapToInt32(x - y))
                Case "*" : Return ExpressionValue.FromInt(ExpressionEvaluator.WrapToInt32(x * y))
                Case "/"
                    If y = 0 Then Throw New DivideByZeroException("Attempted to divide by zero.")
                    Return ExpressionValue.FromInt(ExpressionEvaluator.WrapToInt32(x \ y))
                Case "%"
                    If y = 0 Then Throw New DivideByZeroException("Attempted to divide by zero.")
                    Return ExpressionValue.FromInt(ExpressionEvaluator.WrapToInt32(x Mod y))
            End Select
        End If

        Dim l = a.AsReal, r = b.AsReal
        Select Case _op
            Case "+" : Return ExpressionValue.FromReal(l + r)
            Case "-" : Return ExpressionValue.FromReal(l - r)
            Case "*" : Return ExpressionValue.FromReal(l * r)
            Case "/" : Return ExpressionValue.FromReal(l / r)
            Case "%" : Return ExpressionValue.FromReal(l Mod r)
        End Select

        Throw New ExpressionEvaluationException(
            String.Format(CultureInfo.InvariantCulture, "Unknown operator '{0}'.", _op))

    End Function

    Public Overrides Function InferKind() As ValueKind
        If _left.InferKind() = ValueKind.Int32Value AndAlso _right.InferKind() = ValueKind.Int32Value Then
            Return ValueKind.Int32Value
        End If
        Return ValueKind.RealValue
    End Function

End Class

''' <summary>Bit shifts, on integers.</summary>
Public NotInheritable Class ShiftNode
    Inherits ExpressionNode

    Private ReadOnly _op As String
    Private ReadOnly _left As ExpressionNode
    Private ReadOnly _right As ExpressionNode

    Public Sub New(op As String, left As ExpressionNode, right As ExpressionNode)
        _op = op
        _left = left
        _right = right
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Dim a = _left.Evaluate(vars)
        Dim b = _right.Evaluate(vars)

        If a.Kind <> ValueKind.Int32Value OrElse b.Kind <> ValueKind.Int32Value Then
            Throw New ExpressionEvaluationException(
                String.Format(CultureInfo.InvariantCulture, "'{0}' needs two integers.", _op))
        End If

        If _op = "<<" Then Return ExpressionValue.FromInt(a.Whole << b.Whole)
        Return ExpressionValue.FromInt(a.Whole >> b.Whole)

    End Function

    Public Overrides Function InferKind() As ValueKind
        Return ValueKind.Int32Value
    End Function

End Class

''' <summary>Comparison. Mixed types compare as numbers.</summary>
Public NotInheritable Class CompareNode
    Inherits ExpressionNode

    Private ReadOnly _op As String
    Private ReadOnly _left As ExpressionNode
    Private ReadOnly _right As ExpressionNode

    Public Sub New(op As String, left As ExpressionNode, right As ExpressionNode)
        _op = op
        _left = left
        _right = right
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Dim a = _left.Evaluate(vars)
        Dim b = _right.Evaluate(vars)

        If a.Kind = ValueKind.BooleanValue OrElse b.Kind = ValueKind.BooleanValue Then
            Dim x = a.AsBool, y = b.AsBool
            Select Case _op
                Case "=" : Return ExpressionValue.FromBool(x = y)
                Case "<>" : Return ExpressionValue.FromBool(x <> y)
                Case Else
                    Throw New ExpressionEvaluationException(
                        String.Format(CultureInfo.InvariantCulture, "'{0}' cannot compare logical values.", _op))
            End Select
        End If

        Dim l = a.AsReal, r = b.AsReal
        Select Case _op
            Case "=" : Return ExpressionValue.FromBool(l = r)
            Case "<>" : Return ExpressionValue.FromBool(l <> r)
            Case "<" : Return ExpressionValue.FromBool(l < r)
            Case ">" : Return ExpressionValue.FromBool(l > r)
            Case "<=" : Return ExpressionValue.FromBool(l <= r)
            Case ">=" : Return ExpressionValue.FromBool(l >= r)
        End Select

        Throw New ExpressionEvaluationException(
            String.Format(CultureInfo.InvariantCulture, "Unknown comparison '{0}'.", _op))

    End Function

    Public Overrides Function InferKind() As ValueKind
        Return ValueKind.BooleanValue
    End Function

End Class

''' <summary>and / or / xor.</summary>
Public NotInheritable Class LogicalNode
    Inherits ExpressionNode

    Private ReadOnly _op As String
    Private ReadOnly _left As ExpressionNode
    Private ReadOnly _right As ExpressionNode

    Public Sub New(op As String, left As ExpressionNode, right As ExpressionNode)
        _op = op
        _left = left
        _right = right
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Dim a = _left.Evaluate(vars).AsBool

        Select Case _op
            Case "and"
                If Not a Then Return ExpressionValue.FromBool(False)
                Return ExpressionValue.FromBool(_right.Evaluate(vars).AsBool)
            Case "or"
                If a Then Return ExpressionValue.FromBool(True)
                Return ExpressionValue.FromBool(_right.Evaluate(vars).AsBool)
            Case "xor"
                Return ExpressionValue.FromBool(a Xor _right.Evaluate(vars).AsBool)
        End Select

        Throw New ExpressionEvaluationException(
            String.Format(CultureInfo.InvariantCulture, "Unknown logical operator '{0}'.", _op))

    End Function

    Public Overrides Function InferKind() As ValueKind
        Return ValueKind.BooleanValue
    End Function

End Class

''' <summary><c>if(condition, whenTrue, whenFalse)</c>.</summary>
Public NotInheritable Class ConditionalNode
    Inherits ExpressionNode

    Private ReadOnly _condition As ExpressionNode
    Private ReadOnly _whenTrue As ExpressionNode
    Private ReadOnly _whenFalse As ExpressionNode

    Public Sub New(condition As ExpressionNode, whenTrue As ExpressionNode, whenFalse As ExpressionNode)
        _condition = condition
        _whenTrue = whenTrue
        _whenFalse = whenFalse
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Dim taken = If(_condition.Evaluate(vars).AsBool, _whenTrue, _whenFalse)
        Dim result = taken.Evaluate(vars)

        ' Both arms are brought to one type, so a whole number in the arm that ran becomes a double
        ' when the other arm is one. Without this, 1/if(true,3,2.5) divides as integers and gives 0.
        If InferKind() = ValueKind.RealValue AndAlso result.Kind = ValueKind.Int32Value Then
            Return ExpressionValue.FromReal(result.Whole)
        End If

        Return result

    End Function

    Public Overrides Function InferKind() As ValueKind
        Dim a = _whenTrue.InferKind()
        Dim b = _whenFalse.InferKind()
        If a = b Then Return a
        If a = ValueKind.BooleanValue OrElse b = ValueKind.BooleanValue Then Return ValueKind.BooleanValue
        Return ValueKind.RealValue
    End Function

End Class

''' <summary>
''' A call to one of <c>System.Math</c>'s methods, by the name the user wrote, without regard to case.
''' </summary>
''' <remarks>
''' The overload matters: <c>Math.Abs</c>, <c>Math.Sign</c>, <c>Math.Min</c> and <c>Math.Max</c> have
''' integer forms, so <c>abs(-9)/2</c> is 4 while <c>sqrt(81)/2</c> - <c>Math.Sqrt</c> taking and
''' returning a double - is 4.5. Everything else takes doubles.
''' </remarks>
Public NotInheritable Class FunctionNode
    Inherits ExpressionNode

    Private ReadOnly _name As String
    Private ReadOnly _args As List(Of ExpressionNode)
    Private ReadOnly _pos As Integer

    Public Sub New(name As String, args As List(Of ExpressionNode), pos As Integer)
        _name = name
        _args = args
        _pos = pos
    End Sub

    Public Overrides Function Evaluate(vars As VariableTable) As ExpressionValue

        Dim a(_args.Count - 1) As ExpressionValue
        For i = 0 To _args.Count - 1
            a(i) = _args(i).Evaluate(vars)
        Next

        Dim lower = _name.ToLowerInvariant()
        Dim allWhole = True
        For i = 0 To a.Length - 1
            If a(i).Kind <> ValueKind.Int32Value Then allWhole = False
        Next

        Select Case lower

            Case "abs"
                Expect(a, 1)
                If allWhole Then Return ExpressionValue.FromInt(Math.Abs(a(0).Whole))
                Return ExpressionValue.FromReal(Math.Abs(a(0).AsReal))

            Case "sign"
                Expect(a, 1)
                If allWhole Then Return ExpressionValue.FromInt(Math.Sign(a(0).Whole))
                Return ExpressionValue.FromInt(Math.Sign(a(0).AsReal))

            Case "min"
                Expect(a, 2)
                If allWhole Then Return ExpressionValue.FromInt(Math.Min(a(0).Whole, a(1).Whole))
                Return ExpressionValue.FromReal(Math.Min(a(0).AsReal, a(1).AsReal))

            Case "max"
                Expect(a, 2)
                If allWhole Then Return ExpressionValue.FromInt(Math.Max(a(0).Whole, a(1).Whole))
                Return ExpressionValue.FromReal(Math.Max(a(0).AsReal, a(1).AsReal))

            Case "sin" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Sin(a(0).AsReal))
            Case "cos" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Cos(a(0).AsReal))
            Case "tan" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Tan(a(0).AsReal))
            Case "asin" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Asin(a(0).AsReal))
            Case "acos" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Acos(a(0).AsReal))
            Case "atan" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Atan(a(0).AsReal))
            Case "sinh" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Sinh(a(0).AsReal))
            Case "cosh" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Cosh(a(0).AsReal))
            Case "tanh" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Tanh(a(0).AsReal))
            Case "exp" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Exp(a(0).AsReal))
            Case "log10" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Log10(a(0).AsReal))
            Case "sqrt" : Expect(a, 1) : Return ExpressionValue.FromReal(Math.Sqrt(a(0).AsReal))
            Case "ceiling" : Expect(a, 1) : RefuseWholeArgument(a, 0) : Return ExpressionValue.FromReal(Math.Ceiling(a(0).AsReal))
            Case "floor" : Expect(a, 1) : RefuseWholeArgument(a, 0) : Return ExpressionValue.FromReal(Math.Floor(a(0).AsReal))
            Case "truncate" : Expect(a, 1) : RefuseWholeArgument(a, 0) : Return ExpressionValue.FromReal(Math.Truncate(a(0).AsReal))
            Case "atan2" : Expect(a, 2) : Return ExpressionValue.FromReal(Math.Atan2(a(0).AsReal, a(1).AsReal))
            Case "pow" : Expect(a, 2) : Return ExpressionValue.FromReal(Math.Pow(a(0).AsReal, a(1).AsReal))
            Case "ieeeremainder" : Expect(a, 2) : Return ExpressionValue.FromReal(Math.IEEERemainder(a(0).AsReal, a(1).AsReal))

            Case "log"
                If a.Length = 1 Then Return ExpressionValue.FromReal(Math.Log(a(0).AsReal))
                If a.Length = 2 Then Return ExpressionValue.FromReal(Math.Log(a(0).AsReal, a(1).AsReal))
                Throw Wrong(a.Length, "one or two")

            Case "round"
                RefuseWholeArgument(a, 0)
                If a.Length = 1 Then Return ExpressionValue.FromReal(Math.Round(a(0).AsReal))
                If a.Length = 2 Then Return ExpressionValue.FromReal(Math.Round(a(0).AsReal, a(1).Whole))
                Throw Wrong(a.Length, "one or two")

        End Select

        Throw New ExpressionEvaluationException(
            String.Format(CultureInfo.InvariantCulture,
                          "There is no function named '{0}' (position {1}).", _name, _pos + 1))

    End Function

    ''' <summary>
    ''' Refuses an integer where the method only offers a double and a decimal to convert it to.
    ''' Flee resolves overloads the way the CLR does and reports the call as ambiguous, so
    ''' floor(2) is an error where floor(2.0) is not; keeping that keeps the two in step.
    ''' </summary>
    Private Sub RefuseWholeArgument(a As ExpressionValue(), index As Integer)
        If index < a.Length AndAlso a(index).Kind = ValueKind.Int32Value Then
            Throw New ExpressionSyntaxException(
                String.Format(CultureInfo.InvariantCulture,
                              "'{0}' cannot be called with a whole number: write it with a decimal point (position {1}).",
                              _name, _pos + 1))
        End If
    End Sub

    ''' <summary>
    ''' Only the four with an integer overload keep an integer: Math.Abs, Math.Sign, Math.Min and
    ''' Math.Max. Math.Sign returns one whatever it is given. Everything else takes and returns
    ''' doubles.
    ''' </summary>
    Public Overrides Function InferKind() As ValueKind

        Select Case _name.ToLowerInvariant()
            Case "sign"
                Return ValueKind.Int32Value
            Case "abs", "min", "max"
                For Each arg In _args
                    If arg.InferKind() <> ValueKind.Int32Value Then Return ValueKind.RealValue
                Next
                Return ValueKind.Int32Value
        End Select

        Return ValueKind.RealValue

    End Function

    Private Sub Expect(a As ExpressionValue(), n As Integer)
        If a.Length <> n Then Throw Wrong(a.Length, n.ToString(CultureInfo.InvariantCulture))
    End Sub

    Private Function Wrong(given As Integer, expected As String) As Exception
        Return New ExpressionEvaluationException(
            String.Format(CultureInfo.InvariantCulture,
                          "'{0}' takes {1} argument(s), {2} given (position {3}).",
                          _name, expected, given, _pos + 1))
    End Function

End Class

''' <summary>
''' A parsed expression, ready to be evaluated against a variable table as often as needed.
''' </summary>
Public NotInheritable Class CompiledExpression

    Private ReadOnly _text As String
    Private ReadOnly _root As ExpressionNode

    Friend Sub New(text As String, root As ExpressionNode)
        _text = text
        _root = root
    End Sub

    ''' <summary>The expression as it was written.</summary>
    Public ReadOnly Property Text As String
        Get
            Return _text
        End Get
    End Property

    ''' <summary>Evaluates to a number.</summary>
    Public Function Evaluate(vars As VariableTable) As Double
        Return _root.Evaluate(vars).AsReal
    End Function

    ''' <summary>Evaluates to a logical value.</summary>
    Public Function EvaluateBoolean(vars As VariableTable) As Boolean
        Return _root.Evaluate(vars).AsBool
    End Function

End Class
