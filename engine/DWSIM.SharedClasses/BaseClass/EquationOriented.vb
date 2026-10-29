'    Flowsheet Object Base Classes - the equation-oriented model of a simulation object
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports DWSIM.Interfaces

Namespace UnitOperations

    Partial Public MustInherit Class BaseClass

        Implements IEquationOrientedModel

        ''' <summary>False: the equation-oriented solver takes the object as a black box. A unit with equations of its own returns True in the modes they cover.</summary>
        Public Overridable ReadOnly Property EOHasEquations As Boolean Implements IEquationOrientedModel.EOHasEquations
            Get
                Return False
            End Get
        End Property

        ''' <summary>Nothing: as a black box, the object's own calculation reads any of its properties.</summary>
        Public Overridable Function EOParameters() As String() Implements IEquationOrientedModel.EOParameters
            Return Nothing
        End Function

        Public Overridable Function EOEquationCount(context As IEquationOrientedContext) As Integer Implements IEquationOrientedModel.EOEquationCount
            Return context.BlackBoxEquationCount(Me)
        End Function

        Public Overridable Function EOVariables(context As IEquationOrientedContext) As Integer() Implements IEquationOrientedModel.EOVariables
            Return context.BlackBoxVariables(Me)
        End Function

        Public Overridable Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer) Implements IEquationOrientedModel.EOResiduals
            context.BlackBoxResiduals(Me, x, r, row)
        End Sub

        ''' <summary>False: a black box is differenced by the solver.</summary>
        Public Overridable Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean Implements IEquationOrientedModel.EOJacobian
            Return False
        End Function

        Public Overridable Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String Implements IEquationOrientedModel.EOEquationName
            Return context.BlackBoxEquationName(Me, k)
        End Function

        Public Overridable Function EOIsSmooth(context As IEquationOrientedContext, x As Double(), k As Integer) As Boolean Implements IEquationOrientedModel.EOIsSmooth
            Return True
        End Function

        ''' <summary>False: equations that set a temperature or read a state by a flash at T and P do not hold next to a saturated stream.</summary>
        Public Overridable ReadOnly Property EOTakesSaturatedStreams As Boolean Implements IEquationOrientedModel.EOTakesSaturatedStreams
            Get
                Return False
            End Get
        End Property

        ''' <summary>As <see cref="EOTakesSaturatedStreams"/> unless the unit says its inlets are read only by flows, pressures, temperatures and enthalpies.</summary>
        Public Overridable ReadOnly Property EOTakesSaturatedInlets As Boolean Implements IEquationOrientedModel.EOTakesSaturatedInlets
            Get
                Return EOTakesSaturatedStreams
            End Get
        End Property

        ''' <summary>None: the object's equations tie its streams alone.</summary>
        Public Overridable Function EOInternalStateCount(context As IEquationOrientedContext) As Integer Implements IEquationOrientedModel.EOInternalStateCount
            Return 0
        End Function

        Public Overridable Function EOInternalStateGuess(context As IEquationOrientedContext, k As Integer) As Double() Implements IEquationOrientedModel.EOInternalStateGuess
            Return Nothing
        End Function

        Public Overridable Function EOInitialize(context As IEquationOrientedContext, x As Double()) As Boolean Implements IEquationOrientedModel.EOInitialize
            Return False
        End Function

        ''' <summary>
        ''' Solves n equations f(y) = 0 (each over its tolerance) for the variables at these indices of x, from where x holds
        ''' them: Newton on forward differences, the steps cut to keep the flows non-negative and the temperature within
        ''' 50 K a step. The indices are T, P and the compound flows of one state. True when every residual is within 0.1.
        ''' </summary>
        Protected Shared Function EOSolveState(x As Double(), offset As Integer, n As Integer, f As Func(Of Double(), Double()), Optional maxIterations As Integer = 30) As Boolean
            For it = 1 To maxIterations
                Dim r = f(x)
                If r.All(Function(v) Math.Abs(v) <= 0.1) Then Return True
                If r.Any(Function(v) Double.IsNaN(v) OrElse Double.IsInfinity(v)) Then Return False
                Dim jac(n - 1, n - 1) As Double
                For j = 0 To n - 1
                    Dim k = offset + j
                    Dim keep = x(k)
                    Dim h = If(j = 0, 0.01, If(j = 1, 1.0E-05 * Math.Max(keep, 10000.0), 1.0E-06 * Math.Max(Math.Abs(keep), 0.001)))
                    x(k) = keep + h
                    Dim rh = f(x)
                    x(k) = keep
                    For i = 0 To n - 1
                        jac(i, j) = (rh(i) - r(i)) / h
                    Next
                Next
                Dim dy = EOGauss(jac, r.Select(Function(v) -v).ToArray())
                If dy Is Nothing Then Return False
                Dim alpha = 1.0
                If Math.Abs(dy(0)) > 50.0 Then alpha = 50.0 / Math.Abs(dy(0))
                For j = 2 To n - 1
                    If x(offset + j) + alpha * dy(j) < 0.0 AndAlso dy(j) < 0.0 Then alpha = Math.Min(alpha, 0.9 * x(offset + j) / -dy(j))
                Next
                If x(offset + 1) + alpha * dy(1) <= 0.0 Then alpha = Math.Min(alpha, 0.5 * x(offset + 1) / -dy(1))
                ' halve the step until the residuals fall
                Dim merit = r.Sum(Function(v) v * v)
                Dim before = Enumerable.Range(0, n).Select(Function(j) x(offset + j)).ToArray()
                Dim taken = False
                For cut = 0 To 20
                    For j = 0 To n - 1
                        x(offset + j) = Math.Max(before(j) + alpha * dy(j), If(j >= 2, 0.0, before(j) * 0.1))
                    Next
                    Dim rt = f(x)
                    If rt.All(Function(v) Not Double.IsNaN(v)) AndAlso rt.Sum(Function(v) v * v) < merit Then
                        taken = True
                        Exit For
                    End If
                    alpha /= 2.0
                Next
                If Not taken Then
                    For j = 0 To n - 1
                        x(offset + j) = before(j)
                    Next
                    Return False
                End If
            Next
            Return f(x).All(Function(v) Math.Abs(v) <= 0.1)
        End Function

        ''' <summary>Gaussian elimination with partial pivoting; Nothing for a singular matrix.</summary>
        Private Shared Function EOGauss(A As Double(,), b As Double()) As Double()
            Dim n = b.Length
            Dim M = CType(A.Clone(), Double(,)), v = CType(b.Clone(), Double())
            For col = 0 To n - 1
                Dim piv = col
                For i = col + 1 To n - 1
                    If Math.Abs(M(i, col)) > Math.Abs(M(piv, col)) Then piv = i
                Next
                If Math.Abs(M(piv, col)) < 1.0E-300 Then Return Nothing
                If piv <> col Then
                    For j = 0 To n - 1
                        Dim t = M(col, j) : M(col, j) = M(piv, j) : M(piv, j) = t
                    Next
                    Dim tb = v(col) : v(col) = v(piv) : v(piv) = tb
                End If
                For i = col + 1 To n - 1
                    Dim fct = M(i, col) / M(col, col)
                    If fct = 0.0 Then Continue For
                    For j = col To n - 1
                        M(i, j) -= fct * M(col, j)
                    Next
                    v(i) -= fct * v(col)
                Next
            Next
            Dim xs(n - 1) As Double
            For i = n - 1 To 0 Step -1
                Dim s = v(i)
                For j = i + 1 To n - 1
                    s -= M(i, j) * xs(j)
                Next
                xs(i) = s / M(i, i)
            Next
            Return xs
        End Function

        ''' <summary>The variable offset of the material stream on input connector port, or -1.</summary>
        Protected Function EOInletOffset(context As IEquationOrientedContext, port As Integer) As Integer
            Dim c = GraphicObject.InputConnectors(port)
            Return If(c.IsAttached, context.StreamOffset(c.AttachedConnector.AttachedFrom.Name), -1)
        End Function

        ''' <summary>The variable offset of the material stream on output connector port, or -1.</summary>
        Protected Function EOOutletOffset(context As IEquationOrientedContext, port As Integer) As Integer
            Dim c = GraphicObject.OutputConnectors(port)
            Return If(c.IsAttached, context.StreamOffset(c.AttachedConnector.AttachedTo.Name), -1)
        End Function

        ''' <summary>The variables of the streams at these offsets (T, P and the compound flows of each), then the parameters the solver moves.</summary>
        Protected Function EOStreamVariables(context As IEquationOrientedContext, offsets As Integer(), parameters As String()) As Integer()
            Dim v As New List(Of Integer)
            For Each o In offsets
                For j = 0 To context.CompoundCount + 1
                    v.Add(o + j)
                Next
            Next
            If parameters IsNot Nothing Then
                For Each p In parameters
                    Dim k = context.ParameterIndex(Me, p)
                    If k >= 0 Then v.Add(k)
                Next
            End If
            Return v.Distinct().ToArray()
        End Function

        ''' <summary>A parameter's value: the solver's variable when it moves it, else the value the object holds.</summary>
        Protected Function EOParameterValue(context As IEquationOrientedContext, x As Double(), propertyCode As String, value As Double) As Double
            Dim k = context.ParameterIndex(Me, propertyCode)
            Return If(k >= 0, x(k), value)
        End Function

    End Class

End Namespace
