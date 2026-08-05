Imports DWSIM.SharedClasses.DataRegression.Models

Namespace Global.DWSIM.SharedClasses.DataRegression.Engine

    ''' <summary>
    ''' Indexed access to RegressionCase's per-parameter trio of fields
    ''' (iepar1/2/3, llim1/2/3, ulim1/2/3, fixed1/2/3) so callers can iterate by
    ''' parameter index instead of duplicating Select Case branches per model arity.
    ''' </summary>
    Public Module CaseAccessor

        Public Sub SetInitial(c As RegressionCase, index As Integer, value As Double)
            Select Case index
                Case 0 : c.iepar1 = value
                Case 1 : c.iepar2 = value
                Case 2 : c.iepar3 = value
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Sub

        Public Function GetInitial(c As RegressionCase, index As Integer) As Double
            Select Case index
                Case 0 : Return c.iepar1
                Case 1 : Return c.iepar2
                Case 2 : Return c.iepar3
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Function

        Public Sub SetLowerBound(c As RegressionCase, index As Integer, value As Double)
            Select Case index
                Case 0 : c.llim1 = value
                Case 1 : c.llim2 = value
                Case 2 : c.llim3 = value
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Sub

        Public Function GetLowerBound(c As RegressionCase, index As Integer) As Double
            Select Case index
                Case 0 : Return c.llim1
                Case 1 : Return c.llim2
                Case 2 : Return c.llim3
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Function

        Public Sub SetUpperBound(c As RegressionCase, index As Integer, value As Double)
            Select Case index
                Case 0 : c.ulim1 = value
                Case 1 : c.ulim2 = value
                Case 2 : c.ulim3 = value
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Sub

        Public Function GetUpperBound(c As RegressionCase, index As Integer) As Double
            Select Case index
                Case 0 : Return c.ulim1
                Case 1 : Return c.ulim2
                Case 2 : Return c.ulim3
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Function

        Public Sub SetFixed(c As RegressionCase, index As Integer, value As Boolean)
            Select Case index
                Case 0 : c.fixed1 = value
                Case 1 : c.fixed2 = value
                Case 2 : c.fixed3 = value
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Sub

        Public Function GetFixed(c As RegressionCase, index As Integer) As Boolean
            Select Case index
                Case 0 : Return c.fixed1
                Case 1 : Return c.fixed2
                Case 2 : Return c.fixed3
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
            End Select
        End Function

        Public Function GetInitialVector(c As RegressionCase, count As Integer) As Double()
            Dim r(count - 1) As Double
            For i = 0 To count - 1 : r(i) = GetInitial(c, i) : Next
            Return r
        End Function

        Public Function GetLowerBoundVector(c As RegressionCase, count As Integer) As Double()
            Dim r(count - 1) As Double
            For i = 0 To count - 1 : r(i) = GetLowerBound(c, i) : Next
            Return r
        End Function

        Public Function GetUpperBoundVector(c As RegressionCase, count As Integer) As Double()
            Dim r(count - 1) As Double
            For i = 0 To count - 1 : r(i) = GetUpperBound(c, i) : Next
            Return r
        End Function

        Public Function GetFixedVector(c As RegressionCase, count As Integer) As Boolean()
            Dim r(count - 1) As Boolean
            For i = 0 To count - 1 : r(i) = GetFixed(c, i) : Next
            Return r
        End Function

    End Module

End Namespace
