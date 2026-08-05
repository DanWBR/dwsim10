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

End Class
