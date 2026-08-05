Namespace Global.DWSIM.SharedClasses.DataRegression.Models

    Public Class RegressionProblem

        Inherits SwarmOps.Problem

        Public _Dim As Integer, _LB(), _UB(), _INIT() As Double, _Name As String

        Private ReadOnly _objective As Func(Of Double(), Double)
        Private ReadOnly _gradient As Func(Of Double(), Double())

        Public Sub New(objective As Func(Of Double(), Double),
                       gradient As Func(Of Double(), Double()))
            _objective = objective
            _gradient = gradient
        End Sub

        Public Overrides ReadOnly Property Dimensionality As Integer
            Get
                Return _Dim
            End Get
        End Property

        Public Overrides ReadOnly Property LowerBound As Double()
            Get
                Return _LB
            End Get
        End Property

        Public Overrides ReadOnly Property LowerInit As Double()
            Get
                Return _INIT
            End Get
        End Property

        Public Overrides ReadOnly Property UpperInit As Double()
            Get
                Return _INIT
            End Get
        End Property

        Public Overrides ReadOnly Property MinFitness As Double
            Get
                Return Double.MinValue
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return _Name
            End Get
        End Property

        Public Overrides ReadOnly Property UpperBound As Double()
            Get
                Return _UB
            End Get
        End Property

        Public Overrides ReadOnly Property HasGradient As Boolean
            Get
                Return _gradient IsNot Nothing
            End Get
        End Property

        Public Overrides Function Gradient(x() As Double, ByRef v() As Double) As Integer
            v = _gradient(x)
            Return 0
        End Function

        Public Overrides Function Fitness(parameters() As Double) As Double
            Return _objective(parameters)
        End Function

    End Class

End Namespace
