''' <summary>
''' Keeps the Flee contexts and the compiled expressions of a single owner.
''' </summary>
''' <remarks>
''' Flee compiles an expression to IL through Reflection.Emit, which costs orders of magnitude more
''' than evaluating the result. Compiling inside an integration or convergence loop therefore
''' dominates the run time of everything around it. Hold one cache per owner, define the variables
''' the expression reads before asking for it the first time, and afterwards only update the
''' variable values.
'''
''' An instance is not thread-safe. Give each object its own, or use
''' <see cref="ExpressionParser.ThreadCache"/> from shared code.
''' </remarks>
Public Class ExpressionCache

    Private _contexts As Dictionary(Of String, Flee.PublicTypes.ExpressionContext)
    Private _compiled As Dictionary(Of String, Flee.PublicTypes.IGenericExpression(Of Double))

    ''' <summary>
    ''' Discards everything held. Call it when the expressions or the variables they read may have
    ''' changed, typically at the start of a calculation.
    ''' </summary>
    Public Sub Reset()

        _contexts = Nothing
        _compiled = Nothing

    End Sub

    ''' <summary>
    ''' Returns the context stored under <paramref name="key"/>, creating it on first use.
    ''' </summary>
    ''' <param name="key">
    ''' Identifies the set of variables the context holds. Expressions that read different variables
    ''' must use different keys.
    ''' </param>
    Public Function GetContext(key As String) As Flee.PublicTypes.ExpressionContext

        If _contexts Is Nothing Then _contexts = New Dictionary(Of String, Flee.PublicTypes.ExpressionContext)

        Dim context As Flee.PublicTypes.ExpressionContext = Nothing

        If Not _contexts.TryGetValue(key, context) Then
            context = New Flee.PublicTypes.ExpressionContext()
            context.Imports.AddType(GetType(System.Math))
            context.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture
            _contexts.Add(key, context)
        End If

        Return context

    End Function

    ''' <summary>
    ''' Sets a variable on a context, defining it if it is not there yet.
    ''' </summary>
    ''' <remarks>
    ''' Never clear the variable collection of a context that already has compiled expressions
    ''' attached to it: Flee resolves the variables when it compiles, and clearing them breaks that
    ''' binding.
    ''' </remarks>
    Public Shared Sub SetVariable(context As Flee.PublicTypes.ExpressionContext, name As String, value As Double)

        If context.Variables.ContainsKey(name) Then
            context.Variables(name) = value
        Else
            context.Variables.Add(name, value)
        End If

    End Sub

    ''' <summary>
    ''' Compiles <paramref name="expression"/> against the context stored under
    ''' <paramref name="key"/> and keeps the compiled form for later calls.
    ''' </summary>
    ''' <param name="key">The same key used with <see cref="GetContext"/>.</param>
    ''' <param name="expression">The expression text, as the user wrote it.</param>
    Public Function GetCompiled(key As String, expression As String) As Flee.PublicTypes.IGenericExpression(Of Double)

        If _compiled Is Nothing Then _compiled = New Dictionary(Of String, Flee.PublicTypes.IGenericExpression(Of Double))

        Dim cachekey As String = key & "|" & expression
        Dim compiled As Flee.PublicTypes.IGenericExpression(Of Double) = Nothing

        If Not _compiled.TryGetValue(cachekey, compiled) Then
            compiled = GetContext(key).CompileGeneric(Of Double)(expression)
            _compiled.Add(cachekey, compiled)
        End If

        Return compiled

    End Function

End Class

Public Class ExpressionParser

    Public Shared ExpContext As Flee.PublicTypes.ExpressionContext

    Public Shared Sub InitializeExpressionParser()

        ExpContext = New Flee.PublicTypes.ExpressionContext

        ExpContext.Imports.AddType(GetType(System.Math))
        ExpContext.Variables.Clear()
        ExpContext.Options.ParseCulture = Globalization.CultureInfo.InvariantCulture

        ParserInitialized = True

    End Sub

    Public Shared Property ParserInitialized As Boolean = False

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

End Class
