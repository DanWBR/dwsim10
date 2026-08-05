'    Copyright 2020 Daniel Wagner O. de Medeiros
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

Namespace MathEx.Optimization

    Public Class NewtonSolver

        Public Property Tolerance As Double = 0.0001

        Public Property MaxIterations As Integer = 100

        Public Property EnableDamping As Boolean = True

        Public Property UseBroydenApproximation As Boolean = False

        Public Property ExpandFactor As Double = 1.5

        Public Property MaximumDelta As Double = 0.5

        Public Property Epsilon As Double = Double.NaN

        Private _Iterations As Integer = 0

        Private fxb As Func(Of Double(), Double())

        Private broydengrad As Double(,)

        Private brentsolver As New BrentOpt.BrentMinimize

        Private tmpx As Double(), tmpdx As Double()

        Private _jacobian As Boolean

        Private dfdx As Func(Of Double(), Double(,))

        Private _error As Double

        Private _jac As Double(,)

        ''' <summary>
        ''' Best point reached by a run that stalled - the step vanished while the residual was
        ''' still above tolerance - together with its residual. Kept so that a ladder in which
        ''' every damping factor stalls can still hand back the least bad of them, which is what
        ''' this solver returned before a stall was allowed to fail its combination.
        ''' </summary>
        Private _stalledX As Double() = Nothing
        Private _stalledResidual As Double = Double.MaxValue

        Public ReadOnly Property Jacobian As Double(,)
            Get
                Return _jac
            End Get
        End Property

        Public ReadOnly Property BuildingJacobian As Boolean
            Get
                Return _jacobian
            End Get
        End Property

        Public ReadOnly Property Iterations As Integer
            Get
                Return _Iterations
            End Get
        End Property

        Sub New()

            brentsolver.DefineFuncDelegate(AddressOf minimizeerror)

        End Sub

        Public Sub Reset()

            _Iterations = 0
            _error = 0.0

        End Sub

        Public Shared Function FindRoots(functionbody As Func(Of Double(), Double()), vars As Double(),
                                         maxits As Integer, tol As Double) As Double()

            Dim newton As New NewtonSolver
            newton.Tolerance = tol
            newton.MaxIterations = maxits

            Return newton.Solve(functionbody, vars)

        End Function

        ''' <summary>
        ''' Solves a system of non-linear equations [f(x) = 0] using newton's method.
        ''' </summary>
        ''' <param name="functionbody">f(x) where x is a vector of double, returns the error values for each x</param>
        ''' <param name="vars">initial values for x</param>
        ''' <returns>vector of variables which solve the equations according to the minimum allowable error value (tolerance).</returns>
        Function Solve(functionbody As Func(Of Double(), Double()), vars As Double()) As Double()

            Dim dfacs As Double() = New Double() {0.1, 0.2, 0.4, 0.6, 0.8, 1.0}
            Dim epsilons As Double() = New Double() {0.000000000001, 0.00000001, 0.0001, 0.001, 0.01, 0.1}

            Dim leave As Boolean = False
            Dim finalx As Double() = vars
            _stalledX = Nothing
            _stalledResidual = Double.MaxValue


            dfdx = Nothing

            If Not Double.IsNaN(Epsilon) Then epsilons = New Double() {Epsilon}

            If EnableDamping Then
                For Each d In dfacs
                    If leave Then Exit For
                    For Each eps In epsilons
                        If leave Then Exit For
                        Try
                            finalx = solve_internal(d, eps, functionbody, vars)
                            leave = True
                        Catch ex As ArgumentException
                            'try next parameters
                        End Try
                    Next
                Next
            Else
                For Each eps In epsilons
                    If leave Then Exit For
                    Try
                        finalx = solve_internal(1.0, eps, functionbody, vars)
                        leave = True
                    Catch ex As ArgumentException
                        'try next parameters
                    End Try
                Next
            End If

            If Not leave Then
                ' Every combination stalled or blew up. A stalled point is the answer this
                ' solver handed back before a stall was allowed to fail its combination, so
                ' prefer the best one seen to failing outright; a run that never stalled has
                ' nothing to offer and still throws.
                If _stalledX IsNot Nothing Then Return _stalledX
                Throw New Exception("Newton Convergence Error")
            End If

            Return finalx

        End Function

        ''' <summary>
        ''' Solves a system of non-linear equations [f(x) = 0] using newton's method.
        ''' </summary>
        ''' <param name="functionbody">f(x) where x is a vector of double, returns the error values for each x</param>
        ''' <param name="vars">initial values for x</param>
        ''' <returns>vector of variables which solve the equations according to the minimum allowable error value (tolerance).</returns>
        Function Solve(functionbody As Func(Of Double(), Double()), functiongradient As Func(Of Double(), Double(,)), vars As Double()) As Double()

            Dim dfacs As Double() = New Double() {0.1, 0.2, 0.4, 0.6, 0.8, 1.0}
            Dim epsilons As Double() = New Double() {0.000000000001, 0.00000001, 0.0001, 0.001, 0.01, 0.1}

            Dim leave As Boolean = False
            Dim finalx As Double() = vars
            _stalledX = Nothing
            _stalledResidual = Double.MaxValue


            dfdx = functiongradient

            If EnableDamping Then
                For Each d In dfacs
                    If leave Then Exit For
                    For Each eps In epsilons
                        If leave Then Exit For
                        Try
                            finalx = solve_internal(d, eps, functionbody, vars)
                            leave = True
                        Catch ex As ArgumentException
                            'try next parameters
                        End Try
                    Next
                Next
            Else
                For Each eps In epsilons
                    If leave Then Exit For
                    Try
                        finalx = solve_internal(1.0, eps, functionbody, vars)
                        leave = True
                    Catch ex As ArgumentException
                        'try next parameters
                    End Try
                Next
            End If

            If Not leave Then
                ' Every combination stalled or blew up. A stalled point is the answer this
                ' solver handed back before a stall was allowed to fail its combination, so
                ' prefer the best one seen to failing outright; a run that never stalled has
                ' nothing to offer and still throws.
                If _stalledX IsNot Nothing Then Return _stalledX
                Throw New Exception("Newton Convergence Error")
            End If

            Return finalx

        End Function


        ''' <summary>
        ''' Set DWSIM_NEWTON_DAMP_TRACE=1 to watch the line search: the step factor
        ''' it settled on, how many times it had to halve, and whether it ended up
        ''' accepting a step that made the residual worse - which it is allowed to
        ''' do once the factor falls to 1e-4.
        ''' </summary>
        Private Shared ReadOnly _dampTrace As Boolean =
            Environment.GetEnvironmentVariable("DWSIM_NEWTON_DAMP_TRACE") = "1"

        ''' <summary>Armijo sufficient-decrease coefficient for the line search.</summary>
        Private Const ArmijoC1 As Double = 0.0001

        ''' <summary>
        ''' Step factor below which backtracking gives up. Well under the point where the
        ''' residual stops responding, so a direction that is going to work has had its chance.
        ''' </summary>
        Private Const ArmijoMinStep As Double = 0.000001

        Private Function solve_internal(mindamp As Double, epsilon As Double, functionbody As Func(Of Double(), Double()), vars As Double()) As Double()

            fxb = functionbody

            Dim fx(), x(), dx(), dfdx(,), df, fxsum, fxsum0 As Double
            Dim success As Boolean = False

            x = vars.Clone

            dx = x.Clone

            _Iterations = 0

            Do

                If _Iterations = 0 Then
                    fxsum0 = 1.0E+20
                Else
                    fxsum0 = MathEx.Common.SumSqr(fx)
                End If

                _jacobian = False

                fx = fxb.Invoke(x)

                _error = MathEx.Common.SumSqr(fx)
                fxsum = _error

                If fxsum < Tolerance Then
                    Exit Do
                End If

                _jacobian = True

                dfdx = gradient(epsilon, x, fx)

                ' Two-sided equilibration (row + column scaling) before the linear solve. The raw MESH
                ' Jacobian is severely ill-conditioned (cond ~1e8) because equations and variables span
                ' many orders of magnitude (e.g. material-balance rows carry a 1e6 weight, solvent flows
                ' dwarf trace-solute flows). Scaling each row and column to ~unit norm is mathematically
                ' neutral (dx is unchanged) but lets the LU solve recover an accurate Newton direction.
                Dim nrc As Integer = fx.Length
                Dim rscale(nrc - 1), cscale(nrc - 1) As Double
                For r As Integer = 0 To nrc - 1
                    Dim mrow As Double = 0.0
                    For c As Integer = 0 To nrc - 1
                        Dim av = Math.Abs(dfdx(r, c))
                        If av > mrow Then mrow = av
                    Next
                    rscale(r) = If(mrow > 0.0, 1.0 / mrow, 1.0)
                Next
                For c As Integer = 0 To nrc - 1
                    Dim mcol As Double = 0.0
                    For r As Integer = 0 To nrc - 1
                        Dim av = Math.Abs(rscale(r) * dfdx(r, c))
                        If av > mcol Then mcol = av
                    Next
                    cscale(c) = If(mcol > 0.0, 1.0 / mcol, 1.0)
                Next
                Dim ase(nrc - 1, nrc - 1), bse(nrc - 1) As Double
                For r As Integer = 0 To nrc - 1
                    bse(r) = rscale(r) * fx(r)
                    For c As Integer = 0 To nrc - 1
                        ase(r, c) = rscale(r) * dfdx(r, c) * cscale(c)
                    Next
                Next

                Dim A = MathNet.Numerics.LinearAlgebra.Matrix(Of Double).Build.DenseOfArray(ase)
                Dim B = MathNet.Numerics.LinearAlgebra.Vector(Of Double).Build.DenseOfArray(bse)

                ' solve the equilibrated system for z, then recover dx = C·z
                Dim zsol = A.Solve(B).ToArray()
                For c As Integer = 0 To nrc - 1
                    dx(c) = cscale(c) * zsol(c)
                Next

                'SysLin.rsolve.rmatrixsolve(dfdx, fx, x.Length, dx)

                'If success Then

                If Common.SumSqr(dx) < Tolerance And _Iterations > MaxIterations / 2 Then
                    ' A step this small with the residual still above tolerance is a stall, not a
                    ' solution: the convergence test at the top of the loop already established
                    ' fxsum >= Tolerance, so this branch is only ever reached short of an answer.
                    ' Leaving through Exit Do reported it as success, and the damping ladder in
                    ' Solve() took that at face value and stopped - it never got past its first
                    ' factor, so the larger steps that would have moved the residual were never
                    ' tried. Record the point and fail this combination so the ladder carries on;
                    ' if they all stall, Solve() hands back the best of them rather than throwing.
                    If fxsum < _stalledResidual Then
                        _stalledResidual = fxsum
                        _stalledX = x.Clone()
                    End If
                    Throw New ArgumentException("step vanished with the residual above tolerance")
                End If

                If EnableDamping Then
                    If _Iterations > 5 Then
                        df = df * ExpandFactor
                        If df > 1.0 Then df = 1.0
                    Else
                        df = mindamp
                    End If
                Else
                    df = 1.0#
                End If

                ' Cap the raw Newton step per variable (trust-region-like bound)
                For i = 0 To x.Length - 1
                    If Math.Abs(x(i)) >= 1.0E-20 AndAlso Math.Abs(dx(i) / x(i)) > MaximumDelta Then
                        dx(i) = Math.Sign(dx(i)) * Math.Abs(x(i)) * MaximumDelta
                    End If
                Next

                If EnableDamping Then
                    ' Backtracking line search with an Armijo sufficient-decrease test. The step
                    ' factor t starts at the scheduled damping and is halved until the trial point
                    ' buys a reduction in the residual sum-of-squares proportional to how far it
                    ' moved, which is what makes the damped Newton globally convergent on stiff
                    ' systems such as wide-boiling sour-water columns.
                    '
                    ' The test used to be "no worse than before", and that is not the same thing:
                    ' once t has been halved to around 1e-4 the step is too small to move the
                    ' residual at all, so equality satisfied it and a step that achieved nothing
                    ' was accepted. The collapsed t then carried into the next iteration as the
                    ' damping, and since recovery is only a factor of ExpandFactor per iteration
                    ' while any attempt to grow tripped the same escape, the solver sat at 1e-4
                    ' making no progress until it ran out of iterations. Requiring a real decrease
                    ' means a search that cannot find one fails its damping factor instead, and
                    ' the ladder in Solve() moves on to a larger step or a different Jacobian
                    ' perturbation.
                    '
                    ' For the Newton direction, the directional derivative of the merit function
                    ' m = |F|^2/2 is -|F|^2, so the Armijo condition on the sum-of-squares S is
                    ' S(t) <= S * (1 - 2*c1*t). c1 = 1e-4 is the textbook value.
                    Dim xold = x.Clone()
                    Dim t As Double = df
                    Dim tries As Integer = 0
                    Dim accepted As Boolean = False
                    Dim ftrialsum As Double = 0.0
                    _jacobian = False
                    Do
                        For i = 0 To x.Length - 1
                            x(i) = xold(i) - dx(i) * t
                        Next
                        Dim ftrial = fxb.Invoke(x)
                        ftrialsum = MathEx.Common.SumSqr(ftrial)
                        If ftrialsum <= fxsum * (1.0 - 2.0 * ArmijoC1 * t) Then
                            df = t
                            accepted = True
                            Exit Do
                        End If
                        t *= 0.5
                        tries += 1
                    Loop While tries < 25 AndAlso t > ArmijoMinStep

                    If _dampTrace Then
                        Console.WriteLine("[Damp] iter " & _Iterations.ToString().PadLeft(4) &
                            "  mindamp=" & mindamp.ToString("F2") &
                            "  t=" & t.ToString("E2") & "  backtracks=" & tries &
                            "  |F| " & fxsum.ToString("E3") & " -> " & ftrialsum.ToString("E3") &
                            If(accepted, "", "   NO DECREASE, combination abandoned"))
                    End If

                    If Not accepted Then
                        ' No step along this direction reduces the residual, so the direction
                        ' itself is no good - an ill-conditioned or badly perturbed Jacobian, or a
                        ' point the per-variable step cap has distorted. Stay where the iteration
                        ' was, record it, and fail this combination so the ladder can try a larger
                        ' damping or a different finite-difference perturbation. If every one of
                        ' them fails, Solve() returns the best point recorded rather than throwing.
                        For i = 0 To x.Length - 1
                            x(i) = xold(i)
                        Next
                        If fxsum < _stalledResidual Then
                            _stalledResidual = fxsum
                            _stalledX = x.Clone()
                        End If
                        Throw New ArgumentException("line search cannot find a decrease")
                    End If
                Else
                    For i = 0 To x.Length - 1
                        x(i) -= dx(i) * df
                    Next
                End If

                'Else

                '    For i = 0 To x.Length - 1
                '        x(i) *= 0.999
                '    Next

                'End If

                _Iterations += 1

                If _Iterations > 50 And fxsum > fxsum0 Then
                    Throw New ArgumentException("not converging")
                End If

                If Double.IsNaN(fxsum) Then
                    Throw New ArgumentException("not converging")
                End If

            Loop Until _Iterations > MaxIterations

            If _Iterations > MaxIterations Then
                Throw New ArgumentException("not converged")
            End If

            If dfdx Is Nothing Then dfdx = gradient(epsilon, x, fx)

            _jac = dfdx

            Return x

        End Function

        Private Function gradient(epsilon As Double, ByVal x() As Double, fx() As Double) As Double(,)

            Dim f1(), f2() As Double
            Dim g(x.Length - 1, x.Length - 1), x1(x.Length - 1), x2(x.Length - 1), dx(x.Length - 1), xbr(x.Length - 1), fbr(x.Length - 1) As Double
            Dim i, j, k, n As Integer

            n = x.Length - 1

            If UseBroydenApproximation Then

                If broydengrad Is Nothing Then broydengrad = g.Clone()

                If _Iterations = 0 Then
                    For i = 0 To n
                        For j = 0 To n
                            If i = j Then broydengrad(i, j) = 1.0 Else broydengrad(i, j) = 0.0
                        Next
                    Next
                    Broyden.broydn(n, x, fx, dx, xbr, fbr, broydengrad, 0)
                Else
                    Broyden.broydn(n, x, fx, dx, xbr, fbr, broydengrad, 1)
                End If

                Return broydengrad

            Else

                If dfdx IsNot Nothing Then

                    g = dfdx.Invoke(x)

                Else

                    For i = 0 To x.Length - 1
                        For j = 0 To x.Length - 1
                            If i <> j Then
                                x1(j) = x(j)
                                x2(j) = x(j)
                            Else
                                If x(j) = 0.0# Then
                                    x1(j) = epsilon
                                    x2(j) = 2 * epsilon
                                Else
                                    x1(j) = x(j) * (1 - epsilon)
                                    x2(j) = x(j) * (1 + epsilon)
                                End If
                            End If
                        Next
                        f1 = fxb.Invoke(x1)
                        f2 = fxb.Invoke(x2)
                        For k = 0 To x.Length - 1
                            g(k, i) = (f2(k) - f1(k)) / (x2(i) - x1(i))
                        Next
                    Next

                End If

            End If

            Return g

        End Function

        Public Function minimizeerror(ByVal t As Double) As Double

            Dim tmpx0 As Double() = tmpx.Clone

            For i = 0 To tmpx.Length - 1
                tmpx0(i) -= tmpdx(i) * t
            Next

            Dim abssum0 = MathEx.Common.SumSqr(fxb.Invoke(tmpx0))
            If Double.IsNaN(abssum0) Then abssum0 = 1.0E+20
            Return abssum0

        End Function

    End Class

    Public Class NewtonSolver_Old

        Public Property Tolerance As Double = 0.0001

        Public Property MaxIterations As Integer = 1000

        Public Property EnableDamping As Boolean = True

        Private _Iterations As Integer = 0

        Private fxb As Func(Of Double(), Double())

        Private brentsolver As New BrentOpt.BrentMinimize

        Private tmpx As Double(), tmpdx As Double()

        Private _error As Double

        Public ReadOnly Property Iterations
            Get
                Return _Iterations
            End Get
        End Property

        Sub New()

            brentsolver.DefineFuncDelegate(AddressOf minimizeerror)

        End Sub

        ''' <summary>
        ''' Solves a system of non-linear equations [f(x) = 0] using newton's method.
        ''' </summary>
        ''' <param name="functionbody">f(x) where x is a vector of double, returns the error values for each x</param>
        ''' <param name="vars">initial values for x</param>
        ''' <returns>vector of variables which solve the equations according to the minimum allowable error value (tolerance).</returns>
        Function Solve(functionbody As Func(Of Double(), Double()), vars As Double()) As Double()

            Dim minimaldampings As Double() = New Double() {1.0E-20, 0.000000000000001, 0.0000000001, 0.00001, 0.0001, 0.001, 0.01, 0.1}
            Dim epsilons As Double() = New Double() {0.0000000001, 0.000000001, 0.00000001, 0.0000001, 0.000001, 0.00001, 0.0001, 0.001, 0.01, 0.1}

            Dim leave As Boolean = False
            Dim finalx As Double() = vars

            If EnableDamping Then
                For Each mindamp In minimaldampings
                    If leave Then Exit For
                    For Each eps In epsilons
                        If leave Then Exit For
                        Try
                            finalx = solve_internal(mindamp, eps, functionbody, vars)
                            leave = True
                        Catch ex As ArgumentException
                            'try next parameters
                        End Try
                    Next
                Next
            Else
                For Each eps In epsilons
                    If leave Then Exit For
                    Try
                        finalx = solve_internal(1.0, eps, functionbody, vars)
                        leave = True
                    Catch ex As ArgumentException
                        'try next parameters
                    End Try
                Next
            End If

            If Not leave Then Throw New Exception("newton convergence error")

            Return finalx

        End Function

        Private Function solve_internal(mindamp As Double, epsilon As Double, functionbody As Func(Of Double(), Double()), vars As Double()) As Double()

            fxb = functionbody

            Dim fx(), x(), dx(), dfdx(,), df, fxsum, fxsum0 As Double
            Dim success As Boolean = False

            x = vars.Clone

            dx = x.Clone

            _Iterations = 0

            Do

                If _Iterations = 0 Then
                    fxsum0 = 1.0E+20
                Else
                    fxsum0 = MathEx.Common.SumSqr(fx)
                End If

                fx = fxb.Invoke(x)

                _error = MathEx.Common.SumSqr(fx)
                fxsum = _error

                If Common.SumSqr(fx) < Tolerance Then Exit Do

                dfdx = gradient(epsilon, x)

                success = SysLin.rsolve.rmatrixsolve(dfdx, fx, x.Length, dx)

                If success Then

                    'this call to the brent solver calculates the damping factor which minimizes the error (fval).

                    If EnableDamping Then

                        tmpx = x.Clone
                        tmpdx = dx.Clone
                        brentsolver.brentoptimize(mindamp, 1.0, mindamp / 10.0#, df)

                    Else

                        df = 1.0#

                    End If

                    For i = 0 To x.Length - 1
                        x(i) -= dx(i) * df
                    Next

                Else

                    For i = 0 To x.Length - 1
                        x(i) *= 0.999
                    Next

                End If

                _Iterations += 1

                If _Iterations > 50 And fxsum > fxsum0 Then
                    Throw New ArgumentException("not converging")
                End If

                If Double.IsNaN(fxsum) Then
                    Throw New ArgumentException("not converging")
                End If

            Loop Until _Iterations > MaxIterations

            If _Iterations > MaxIterations Then
                Throw New ArgumentException("not converged")
            End If

            Return x

        End Function

        Private Function gradient(epsilon As Double, ByVal x() As Double) As Double(,)

            Dim f1(), f2() As Double
            Dim g(x.Length - 1, x.Length - 1), x2(x.Length - 1) As Double
            Dim i, j, k As Integer

            f1 = fxb.Invoke(x)
            For i = 0 To x.Length - 1
                For j = 0 To x.Length - 1
                    If i <> j Then
                        x2(j) = x(j)
                    Else
                        If x(j) = 0.0# Then
                            x2(j) = epsilon
                        Else
                            x2(j) = x(j) * (1 + epsilon)
                        End If
                    End If
                Next
                f2 = fxb.Invoke(x2)
                For k = 0 To x.Length - 1
                    g(k, i) = (f2(k) - f1(k)) / (x2(i) - x(i))
                Next
            Next

            Return g

        End Function

        Public Function minimizeerror(ByVal t As Double) As Double

            Dim tmpx0 As Double() = tmpx.Clone

            For i = 0 To tmpx.Length - 1
                tmpx0(i) -= tmpdx(i) * t
            Next

            Dim abssum0 = MathEx.Common.SumSqr(fxb.Invoke(tmpx0))
            If Double.IsNaN(abssum0) Then abssum0 = 1.0E+20
            Return abssum0

        End Function

    End Class


End Namespace