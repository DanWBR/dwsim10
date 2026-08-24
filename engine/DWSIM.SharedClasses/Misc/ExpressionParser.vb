''' <summary>
''' A compiled expression together with the variables it reads, so the callers that hold one can go
''' on writing <c>GetCompiled(...).Evaluate()</c>.
''' </summary>
Public NotInheritable Class BoundExpression

    Private ReadOnly _expression As CompiledExpression
    Private ReadOnly _variables As ExpressionEvaluator.VariableTable

    Friend Sub New(expression As CompiledExpression, variables As ExpressionEvaluator.VariableTable)
        _expression = expression
        _variables = variables
    End Sub

    ''' <summary>The expression as it was written.</summary>
    Public ReadOnly Property Text As String
        Get
            Return _expression.Text
        End Get
    End Property

    Public Function Evaluate() As Double
        Return _expression.Evaluate(_variables)
    End Function

    Public Function EvaluateBoolean() As Boolean
        Return _expression.EvaluateBoolean(_variables)
    End Function

End Class

''' <summary>
''' Keeps the variable tables and the parsed expressions of a single owner.
''' </summary>
''' <remarks>
''' Hold one cache per owner, set the variables the expression reads, and ask for the expression by
''' the same key each time. The reactors reset theirs at the start of every calculation, so parsing
''' is on the hot path and not only on load - which is one of the reasons the expressions no longer
''' go through Flee, whose compile step cost some three hundred times more than parsing does.
'''
''' An instance is not thread-safe. Give each object its own, or use
''' <see cref="ExpressionParser.ThreadCache"/> from shared code.
''' </remarks>
Public Class ExpressionCache

    Private _tables As Dictionary(Of String, ExpressionEvaluator.VariableTable)
    Private _compiled As Dictionary(Of String, BoundExpression)

    ''' <summary>
    ''' Discards everything held. Call it when the expressions or the variables they read may have
    ''' changed, typically at the start of a calculation.
    ''' </summary>
    Public Sub Reset()

        _tables = Nothing
        _compiled = Nothing

    End Sub

    ''' <summary>
    ''' Returns the variable table stored under <paramref name="key"/>, creating it on first use.
    ''' </summary>
    ''' <param name="key">
    ''' Identifies the set of variables the table holds. Expressions that read different variables
    ''' must use different keys.
    ''' </param>
    Public Function GetContext(key As String) As ExpressionEvaluator.VariableTable

        If _tables Is Nothing Then _tables = New Dictionary(Of String, ExpressionEvaluator.VariableTable)

        Dim table As ExpressionEvaluator.VariableTable = Nothing

        If Not _tables.TryGetValue(key, table) Then
            table = New ExpressionEvaluator.VariableTable()
            _tables.Add(key, table)
        End If

        Return table

    End Function

    ''' <summary>Sets a variable on a table, defining it if it is not there yet.</summary>
    Public Shared Sub SetVariable(table As ExpressionEvaluator.VariableTable, name As String, value As Double)

        If table Is Nothing Then Exit Sub
        table.SetValue(name, value)

    End Sub

    ''' <summary>
    ''' Parses <paramref name="expression"/> against the table stored under <paramref name="key"/>
    ''' and keeps the result for later calls.
    ''' </summary>
    ''' <param name="key">The same key used with <see cref="GetContext"/>.</param>
    ''' <param name="expression">The expression text, as the user wrote it.</param>
    Public Function GetCompiled(key As String, expression As String) As BoundExpression

        If _compiled Is Nothing Then _compiled = New Dictionary(Of String, BoundExpression)

        Dim cachekey As String = key & "|" & expression
        Dim bound As BoundExpression = Nothing

        If Not _compiled.TryGetValue(cachekey, bound) Then
            bound = New BoundExpression(ExpressionEvaluator.Compile(expression), GetContext(key))
            _compiled.Add(cachekey, bound)
        End If

        Return bound

    End Function

End Class

Public Class ExpressionParser

    <ThreadStatic> Private Shared _threadCache As ExpressionCache

    ''' <summary>
    ''' An expression cache private to the calling thread, for code that has nowhere to keep one of
    ''' its own. The solver calculates unit operations in parallel, so a cache reachable from shared
    ''' code must not be shared between threads.
    ''' </summary>
    Public Shared ReadOnly Property ThreadCache As ExpressionCache
        Get
            If _threadCache Is Nothing Then _threadCache = New ExpressionCache()
            Return _threadCache
        End Get
    End Property

    ''' <summary>
    ''' Kept because callers outside this assembly still set it. Nothing needs initialising now: an
    ''' expression is parsed where it is used and reads its variables from the table it is given.
    ''' </summary>
    Public Shared Property ParserInitialized As Boolean = True

    Public Shared Sub InitializeExpressionParser()
        ParserInitialized = True
    End Sub

End Class
