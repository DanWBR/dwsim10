Imports System.Globalization

''' <summary>
''' Compiles and evaluates the arithmetic and logical expressions a flowsheet carries - reaction
''' conversions, valve opening curves, spec relationships, optimizer objectives, the compound
''' database's property equations - without emitting IL.
''' </summary>
''' <remarks>
''' This replaces Flee, which compiled through <c>Reflection.Emit</c>. That is unavailable under full
''' AOT with no JIT, which is how iOS runs, and it also costs far more than evaluating the result:
''' Flee's use here had to be wrapped in a cache precisely because compiling inside a convergence
''' loop dominated everything around it. A parsed tree walked over a variable table has no compile
''' step to amortise.
'''
''' The grammar and the semantics are not invented. They follow Flee's own
''' <c>src/Flee/Parsing/Expression.grammar</c> and were checked against Flee expression by
''' expression, so that files already saved keep evaluating to the same numbers. The corners that
''' are surprising, and are deliberate here:
'''
'''  - names match without regard to case, variables and functions alike;
'''  - <c>^</c> groups to the LEFT, so <c>2^3^2</c> is 64 and not 512;
'''  - unary minus binds TIGHTER than <c>^</c>, so <c>-2^2</c> is 4 and not -4;
'''  - there is no unary plus and no double negation: <c>+2</c> and <c>--x</c> are syntax errors;
'''  - <c>xor</c> binds looser than <c>or</c>, which binds looser than <c>and</c>;
'''  - arithmetic on two integer literals stays integral, so <c>7/2</c> is 3 and <c>3/4*8</c> is 0,
'''    and it wraps rather than throwing on overflow. A variable is always a double, so anything
'''    touching one is double throughout;
'''  - <c>Math</c>'s own members win over a variable of the same name: <c>pi</c> and <c>e</c> are the
'''    constants even where the flowsheet defines variables so called;
'''  - a real literal needs a decimal point, and an exponent needs an explicit sign and at most three
'''    digits: <c>1.5e+2</c> parses, <c>1.5e2</c> and <c>1e3</c> do not. (This is why the compound
'''    database's equation strings are rewritten to move <c>e</c> out of the way before they get here.)
'''  - <c>log</c> is the natural logarithm; <c>log(x, b)</c> is the logarithm to base b.
'''
''' Flee's remaining surface - strings, characters, dates, timespans, <c>cast</c>, <c>in</c>, member
''' access, indexing - is not implemented, because no expression DWSIM writes or accepts uses it.
''' Anything of that kind raises <see cref="ExpressionSyntaxException"/> when it is parsed, so it
''' fails where it can be seen rather than evaluating to something plausible and wrong.
''' </remarks>
Public NotInheritable Class ExpressionEvaluator

    Private Sub New()
    End Sub

    ''' <summary>Parses an expression, or throws <see cref="ExpressionSyntaxException"/>.</summary>
    Public Shared Function Compile(expression As String) As CompiledExpression

        Dim text = If(expression, "")
        Dim parser As New TextParser(text)

        Return New CompiledExpression(text, parser.Parse())

    End Function

#Region "Values"

    Public Enum ValueKind
        Int32Value
        RealValue
        BooleanValue
    End Enum

    ''' <summary>
    ''' A value moving through the tree. Flee gives every node a type and integer arithmetic differs
    ''' from double arithmetic, so the distinction has to survive evaluation instead of being
    ''' flattened to a double at the leaves.
    ''' </summary>
    Public Structure ExpressionValue

        Public ReadOnly Kind As ValueKind
        Public ReadOnly Real As Double
        Public ReadOnly Whole As Integer
        Public ReadOnly Flag As Boolean

        Private Sub New(k As ValueKind, r As Double, w As Integer, f As Boolean)
            Kind = k
            Real = r
            Whole = w
            Flag = f
        End Sub

        Public Shared Function FromInt(v As Integer) As ExpressionValue
            Return New ExpressionValue(ValueKind.Int32Value, v, v, False)
        End Function

        Public Shared Function FromReal(v As Double) As ExpressionValue
            Return New ExpressionValue(ValueKind.RealValue, v, 0, False)
        End Function

        Public Shared Function FromBool(v As Boolean) As ExpressionValue
            Return New ExpressionValue(ValueKind.BooleanValue, 0.0, 0, v)
        End Function

        Public ReadOnly Property IsNumber As Boolean
            Get
                Return Kind = ValueKind.Int32Value OrElse Kind = ValueKind.RealValue
            End Get
        End Property

        Public ReadOnly Property AsReal As Double
            Get
                Select Case Kind
                    Case ValueKind.Int32Value : Return Whole
                    Case ValueKind.RealValue : Return Real
                    Case Else : Throw New ExpressionEvaluationException("A logical value cannot be used as a number.")
                End Select
            End Get
        End Property

        Public ReadOnly Property AsBool As Boolean
            Get
                If Kind <> ValueKind.BooleanValue Then
                    Throw New ExpressionEvaluationException("A number cannot be used as a logical value.")
                End If
                Return Flag
            End Get
        End Property

    End Structure

#End Region

#Region "Variables"

    ''' <summary>
    ''' The variables an expression reads. Names match without regard to case, as Flee's did.
    ''' </summary>
    Public NotInheritable Class VariableTable

        Private ReadOnly _values As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase)

        Public Sub SetValue(name As String, value As Double)
            _values(name) = value
        End Sub

        Public Function ContainsName(name As String) As Boolean
            Return _values.ContainsKey(name)
        End Function

        Public Function TryGetValue(name As String, ByRef value As Double) As Boolean
            Return _values.TryGetValue(name, value)
        End Function

        Public Sub Clear()
            _values.Clear()
        End Sub

        Public ReadOnly Property Count As Integer
            Get
                Return _values.Count
            End Get
        End Property

        Public ReadOnly Property Names As IEnumerable(Of String)
            Get
                Return _values.Keys
            End Get
        End Property

    End Class

#End Region

#Region "Lexer"

    Private Enum TokKind
        WholeNumber
        RealNumber
        Name
        Symbol
        OpenParen
        CloseParen
        Separator
        EndOfText
    End Enum

    Private Structure Tok
        Public Kind As TokKind
        Public Text As String
        Public Pos As Integer
        Public Whole As Integer
        Public Real As Double
    End Structure

    ''' <summary>Turns the text into tokens, following Flee's lexical patterns.</summary>
    Private NotInheritable Class Lexer

        Private ReadOnly _s As String
        Private _i As Integer

        Public Sub New(s As String)
            _s = s
            _i = 0
        End Sub

        Private ReadOnly Property Ch As Char
            Get
                If _i >= _s.Length Then Return ChrW(0)
                Return _s(_i)
            End Get
        End Property

        Private Function Peek(offset As Integer) As Char
            Dim p = _i + offset
            If p >= _s.Length Then Return ChrW(0)
            Return _s(p)
        End Function

        Public Function Take() As Tok

            While _i < _s.Length AndAlso Char.IsWhiteSpace(_s(_i))
                _i += 1
            End While

            If _i >= _s.Length Then Return New Tok With {.Kind = TokKind.EndOfText, .Text = "", .Pos = _i}

            Dim start = _i
            Dim c = Ch

            If c = "0"c AndAlso (Peek(1) = "x"c OrElse Peek(1) = "X"c) Then Return ReadHex(start)
            If Char.IsDigit(c) OrElse (c = "."c AndAlso Char.IsDigit(Peek(1))) Then Return ReadNumber(start)
            If Char.IsLetter(c) OrElse c = "_"c Then Return ReadName(start)

            Select Case c
                Case "("c
                    _i += 1
                    Return New Tok With {.Kind = TokKind.OpenParen, .Text = "(", .Pos = start}
                Case ")"c
                    _i += 1
                    Return New Tok With {.Kind = TokKind.CloseParen, .Text = ")", .Pos = start}
                Case ","c
                    _i += 1
                    Return New Tok With {.Kind = TokKind.Separator, .Text = ",", .Pos = start}
                Case "+"c, "-"c, "*"c, "/"c, "%"c, "^"c, "="c
                    _i += 1
                    Return New Tok With {.Kind = TokKind.Symbol, .Text = c.ToString(), .Pos = start}
                Case "<"c
                    _i += 1
                    If Ch = "="c Then
                        _i += 1
                        Return New Tok With {.Kind = TokKind.Symbol, .Text = "<=", .Pos = start}
                    ElseIf Ch = ">"c Then
                        _i += 1
                        Return New Tok With {.Kind = TokKind.Symbol, .Text = "<>", .Pos = start}
                    ElseIf Ch = "<"c Then
                        _i += 1
                        Return New Tok With {.Kind = TokKind.Symbol, .Text = "<<", .Pos = start}
                    End If
                    Return New Tok With {.Kind = TokKind.Symbol, .Text = "<", .Pos = start}
                Case ">"c
                    _i += 1
                    If Ch = "="c Then
                        _i += 1
                        Return New Tok With {.Kind = TokKind.Symbol, .Text = ">=", .Pos = start}
                    ElseIf Ch = ">"c Then
                        _i += 1
                        Return New Tok With {.Kind = TokKind.Symbol, .Text = ">>", .Pos = start}
                    End If
                    Return New Tok With {.Kind = TokKind.Symbol, .Text = ">", .Pos = start}
            End Select

            Throw New ExpressionSyntaxException(
                String.Format(CultureInfo.InvariantCulture, "Unexpected character '{0}' at position {1}.", c, start + 1))

        End Function

        ''' <summary>Flee: <c>0x[0-9a-f]+(u|l|ul|lu)?</c>.</summary>
        Private Function ReadHex(start As Integer) As Tok

            _i += 2
            Dim digits = _i
            While _i < _s.Length AndAlso Uri.IsHexDigit(_s(_i))
                _i += 1
            End While

            If _i = digits Then
                Throw New ExpressionSyntaxException(
                    String.Format(CultureInfo.InvariantCulture, "Incomplete hexadecimal literal at position {0}.", start + 1))
            End If

            Dim body = _s.Substring(digits, _i - digits)
            TakeIntegerSuffix()

            Dim parsed As Long = Convert.ToInt64(body, 16)
            Return New Tok With {.Kind = TokKind.WholeNumber, .Text = _s.Substring(start, _i - start),
                                 .Pos = start, .Whole = WrapToInt32(parsed)}

        End Function

        ''' <summary>
        ''' Flee: <c>INTEGER = \d+(u|l|ul|lu)?</c> and
        ''' <c>REAL = \d*\.\d+([e][+-]\d{1,3})?f?</c>. The exponent needs an explicit sign and at most
        ''' three digits, which is why <c>1.5e2</c> is not a number to Flee and <c>1.5e+2</c> is.
        ''' </summary>
        Private Function ReadNumber(start As Integer) As Tok

            While _i < _s.Length AndAlso Char.IsDigit(_s(_i))
                _i += 1
            End While

            Dim isReal = False

            If Ch = "."c AndAlso Char.IsDigit(Peek(1)) Then
                isReal = True
                _i += 1
                While _i < _s.Length AndAlso Char.IsDigit(_s(_i))
                    _i += 1
                End While

                If Ch = "e"c OrElse Ch = "E"c Then
                    Dim save = _i
                    Dim p = _i + 1
                    If p < _s.Length AndAlso (_s(p) = "+"c OrElse _s(p) = "-"c) Then
                        Dim d = p + 1
                        Dim n = 0
                        While d < _s.Length AndAlso Char.IsDigit(_s(d)) AndAlso n < 3
                            d += 1
                            n += 1
                        End While
                        If n > 0 Then _i = d Else _i = save
                    End If
                End If

                If Ch = "f"c OrElse Ch = "F"c Then _i += 1
            End If

            Dim text = _s.Substring(start, _i - start)

            If isReal Then
                Dim body = text.TrimEnd("f"c, "F"c)
                Return New Tok With {.Kind = TokKind.RealNumber, .Text = text, .Pos = start,
                                     .Real = Double.Parse(body, NumberStyles.Float, CultureInfo.InvariantCulture)}
            End If

            TakeIntegerSuffix()
            text = _s.Substring(start, _i - start)

            Dim whole As Long
            If Long.TryParse(text.TrimEnd("u"c, "U"c, "l"c, "L"c), NumberStyles.Integer, CultureInfo.InvariantCulture, whole) Then
                Return New Tok With {.Kind = TokKind.WholeNumber, .Text = text, .Pos = start, .Whole = WrapToInt32(whole)}
            End If

            Return New Tok With {.Kind = TokKind.RealNumber, .Text = text, .Pos = start,
                                 .Real = Double.Parse(text.TrimEnd("u"c, "U"c, "l"c, "L"c), NumberStyles.Float, CultureInfo.InvariantCulture)}

        End Function

        Private Sub TakeIntegerSuffix()
            If Ch = "u"c OrElse Ch = "U"c Then
                _i += 1
                If Ch = "l"c OrElse Ch = "L"c Then _i += 1
            ElseIf Ch = "l"c OrElse Ch = "L"c Then
                _i += 1
                If Ch = "u"c OrElse Ch = "U"c Then _i += 1
            End If
        End Sub

        Private Function ReadName(start As Integer) As Tok

            While _i < _s.Length AndAlso (Char.IsLetterOrDigit(_s(_i)) OrElse _s(_i) = "_"c)
                _i += 1
            End While

            Dim text = _s.Substring(start, _i - start)

            Select Case text.ToLowerInvariant()
                Case "and", "or", "xor", "not"
                    Return New Tok With {.Kind = TokKind.Symbol, .Text = text.ToLowerInvariant(), .Pos = start}
            End Select

            Return New Tok With {.Kind = TokKind.Name, .Text = text, .Pos = start}

        End Function

    End Class

    ''' <summary>Integer arithmetic wraps in Flee rather than throwing, so 2147483647+1 is -2147483648.</summary>
    Friend Shared Function WrapToInt32(v As Long) As Integer
        Dim m = v And &HFFFFFFFFL
        If m > Integer.MaxValue Then m -= 4294967296L
        Return CInt(m)
    End Function

#End Region

#Region "Parser"

    ''' <summary>
    ''' Recursive descent over Flee's production rules, from the loosest binding to the tightest:
    ''' xor, or, and, not, comparison, shift, additive, multiplicative, power, negate, primary.
    ''' </summary>
    Private NotInheritable Class TextParser

        Private ReadOnly _lexer As Lexer
        Private _tok As Tok

        Public Sub New(text As String)
            _lexer = New Lexer(text)
            _tok = _lexer.Take()
        End Sub

        Private Sub Advance()
            _tok = _lexer.Take()
        End Sub

        Private Function IsSymbol(s As String) As Boolean
            Return _tok.Kind = TokKind.Symbol AndAlso String.Equals(_tok.Text, s, StringComparison.Ordinal)
        End Function

        Public Function Parse() As ExpressionNode

            Dim node = ParseXor()

            If _tok.Kind <> TokKind.EndOfText Then
                Throw New ExpressionSyntaxException(
                    String.Format(CultureInfo.InvariantCulture, "Unexpected '{0}' at position {1}.", _tok.Text, _tok.Pos + 1))
            End If

            Return node

        End Function

        Private Function ParseXor() As ExpressionNode
            Dim left = ParseOr()
            While IsSymbol("xor")
                Advance()
                left = New LogicalNode("xor", left, ParseOr())
            End While
            Return left
        End Function

        Private Function ParseOr() As ExpressionNode
            Dim left = ParseAnd()
            While IsSymbol("or")
                Advance()
                left = New LogicalNode("or", left, ParseAnd())
            End While
            Return left
        End Function

        Private Function ParseAnd() As ExpressionNode
            Dim left = ParseNot()
            While IsSymbol("and")
                Advance()
                left = New LogicalNode("and", left, ParseNot())
            End While
            Return left
        End Function

        Private Function ParseNot() As ExpressionNode
            If IsSymbol("not") Then
                Advance()
                Return New NotNode(ParseNot())
            End If
            Return ParseCompare()
        End Function

        Private Function ParseCompare() As ExpressionNode
            Dim left = ParseShift()
            While _tok.Kind = TokKind.Symbol AndAlso
                  (_tok.Text = "=" OrElse _tok.Text = "<" OrElse _tok.Text = ">" OrElse
                   _tok.Text = "<=" OrElse _tok.Text = ">=" OrElse _tok.Text = "<>")
                Dim op = _tok.Text
                Advance()
                left = New CompareNode(op, left, ParseShift())
            End While
            Return left
        End Function

        Private Function ParseShift() As ExpressionNode
            Dim left = ParseAdditive()
            While _tok.Kind = TokKind.Symbol AndAlso (_tok.Text = "<<" OrElse _tok.Text = ">>")
                Dim op = _tok.Text
                Advance()
                left = New ShiftNode(op, left, ParseAdditive())
            End While
            Return left
        End Function

        Private Function ParseAdditive() As ExpressionNode
            Dim left = ParseMultiplicative()
            While _tok.Kind = TokKind.Symbol AndAlso (_tok.Text = "+" OrElse _tok.Text = "-")
                Dim op = _tok.Text
                Advance()
                left = New ArithmeticNode(op, left, ParseMultiplicative())
            End While
            Return left
        End Function

        Private Function ParseMultiplicative() As ExpressionNode
            Dim left = ParsePower()
            While _tok.Kind = TokKind.Symbol AndAlso
                  (_tok.Text = "*" OrElse _tok.Text = "/" OrElse _tok.Text = "%")
                Dim op = _tok.Text
                Advance()
                left = New ArithmeticNode(op, left, ParsePower())
            End While
            Return left
        End Function

        ''' <summary>Left-associative, as Flee has it: <c>2^3^2</c> is 64.</summary>
        Private Function ParsePower() As ExpressionNode
            Dim left = ParseNegate()
            While IsSymbol("^")
                Advance()
                left = New ArithmeticNode("^", left, ParseNegate())
            End While
            Return left
        End Function

        ''' <summary>One optional minus, binding tighter than <c>^</c>: <c>-2^2</c> is 4.</summary>
        Private Function ParseNegate() As ExpressionNode
            If IsSymbol("-") Then
                Advance()
                Return New NegateNode(ParsePrimary())
            End If
            Return ParsePrimary()
        End Function

        Private Function ParsePrimary() As ExpressionNode

            Select Case _tok.Kind

                Case TokKind.WholeNumber
                    Dim v = ExpressionValue.FromInt(_tok.Whole)
                    Advance()
                    Return New ConstantNode(v)

                Case TokKind.RealNumber
                    Dim v = ExpressionValue.FromReal(_tok.Real)
                    Advance()
                    Return New ConstantNode(v)

                Case TokKind.OpenParen
                    Advance()
                    Dim inner = ParseXor()
                    Expect(TokKind.CloseParen, ")")
                    Return inner

                Case TokKind.Name
                    Dim name = _tok.Text
                    Dim pos = _tok.Pos
                    Advance()

                    If _tok.Kind = TokKind.OpenParen Then
                        Advance()
                        Dim args As New List(Of ExpressionNode)
                        If _tok.Kind <> TokKind.CloseParen Then
                            args.Add(ParseXor())
                            While _tok.Kind = TokKind.Separator
                                Advance()
                                args.Add(ParseXor())
                            End While
                        End If
                        Expect(TokKind.CloseParen, ")")

                        If String.Equals(name, "if", StringComparison.OrdinalIgnoreCase) Then
                            If args.Count <> 3 Then
                                Throw New ExpressionSyntaxException(
                                    String.Format(CultureInfo.InvariantCulture,
                                                  "'if' takes three arguments, {0} given, at position {1}.", args.Count, pos + 1))
                            End If
                            Return New ConditionalNode(args(0), args(1), args(2))
                        End If

                        Return New FunctionNode(name, args, pos)
                    End If

                    Select Case name.ToLowerInvariant()
                        Case "true" : Return New ConstantNode(ExpressionValue.FromBool(True))
                        Case "false" : Return New ConstantNode(ExpressionValue.FromBool(False))
                    End Select

                    Return New NameNode(name, pos)

            End Select

            If _tok.Kind = TokKind.EndOfText Then
                Throw New ExpressionSyntaxException("The expression ends before it is complete.")
            End If

            Throw New ExpressionSyntaxException(
                String.Format(CultureInfo.InvariantCulture, "Unexpected '{0}' at position {1}.", _tok.Text, _tok.Pos + 1))

        End Function

        Private Sub Expect(kind As TokKind, what As String)
            If _tok.Kind <> kind Then
                Throw New ExpressionSyntaxException(
                    String.Format(CultureInfo.InvariantCulture, "Expected '{0}' at position {1}.", what, _tok.Pos + 1))
            End If
            Advance()
        End Sub

    End Class

#End Region

End Class

''' <summary>Raised when an expression cannot be parsed.</summary>
Public Class ExpressionSyntaxException
    Inherits Exception

    Public Sub New(message As String)
        MyBase.New(message)
    End Sub

End Class

''' <summary>Raised when a parsed expression cannot be evaluated.</summary>
Public Class ExpressionEvaluationException
    Inherits Exception

    Public Sub New(message As String)
        MyBase.New(message)
    End Sub

End Class
