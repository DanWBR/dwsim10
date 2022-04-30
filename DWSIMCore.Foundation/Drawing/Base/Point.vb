Public Class Point

    Implements IPoint

    Public Property X As Double Implements IPoint.X

    Public Property Y As Double Implements IPoint.Y

    Sub New([x] As Single, [y] As Single)
        Me.X = [x]
        Me.Y = [y]
    End Sub

    Sub New()

    End Sub

    Public Overrides Function ToString() As String
        Return "{X = " + X.ToString() + ", Y = " + Y.ToString + "}"
    End Function

End Class
