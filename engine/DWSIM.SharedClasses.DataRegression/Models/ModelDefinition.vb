Namespace Global.DWSIM.SharedClasses.DataRegression.Models

    Public Class ParameterRow
        Public Property Label As String
        Public Property LowerBound As Double
        Public Property InitialValue As Double
        Public Property UpperBound As Double
        Public Property Fixed As Boolean

        Public Sub New(label As String, lower As Double, initial As Double, upper As Double, fixed As Boolean)
            Me.Label = label
            Me.LowerBound = lower
            Me.InitialValue = initial
            Me.UpperBound = upper
            Me.Fixed = fixed
        End Sub
    End Class

    Public Class ModelDefinition
        Public Property Name As String
        Public Property PropertyPackageName As String
        Public Property DefaultRows As ParameterRow()
        Public Property AllowEstimators As Boolean
        Public Property AllowIdealVaporOption As Boolean
        Public Property AllowTDepRegression As Boolean
        Public Property ResetTDepCheckedOnSelect As Boolean

        Public ReadOnly Property ParameterCount As Integer
            Get
                Return DefaultRows.Length
            End Get
        End Property
    End Class

End Namespace
