'    Two-phase simplex for linear programs in standard form.
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

Option Strict Off
Option Explicit On

Namespace MathEx.LinearProgramming

    ''' <summary>
    ''' How the simplex ended.
    ''' </summary>
    Public Enum SimplexStatus

        ''' <summary>An optimal vertex was reached.</summary>
        Optimal = 0

        ''' <summary>The constraints have no solution with every variable non-negative.</summary>
        Infeasible = 1

        ''' <summary>The objective decreases without bound along a feasible ray.</summary>
        Unbounded = 2

    End Enum

    ''' <summary>
    ''' Solves
    ''' <code>
    '''     minimise    c . x
    '''     subject to  A x = b
    '''                 x  >= 0
    ''' </code>
    ''' by the two-phase simplex method, on a dense tableau.
    '''
    ''' The problems this is written for are small: the element balance of a Gibbs reactor is a
    ''' few dozen variables and a handful of rows, so a dense tableau costs nothing and a revised
    ''' simplex would only add moving parts. Both phases pick their entering and leaving columns
    ''' by Bland's rule, which is slower than steepest edge on large problems and, unlike it,
    ''' cannot cycle: on a degenerate vertex, which the element balance reaches often, that is
    ''' the property that matters.
    ''' </summary>
    Public Class Simplex

        ''' <summary>
        ''' Values smaller than this count as zero. The coefficients of the Gibbs problem are
        ''' energies in J/mol over RT and mole amounts, so they sit within a few orders of
        ''' magnitude of one.
        ''' </summary>
        Private Const Epsilon As Double = 0.00000001

        Private ReadOnly _rows As Integer
        Private ReadOnly _columns As Integer

        ''' <summary>The tableau: one row per constraint plus the objective row at the bottom.</summary>
        Private ReadOnly _tableau(,) As Double

        ''' <summary>Which variable is basic in each row.</summary>
        Private ReadOnly _basis() As Integer

        Private Sub New(rows As Integer, columns As Integer)

            _rows = rows
            _columns = columns

            ReDim _tableau(rows, columns)
            ReDim _basis(rows - 1)

        End Sub

        ''' <summary>
        ''' Minimises <paramref name="objective"/> over the solutions of
        ''' <paramref name="constraints"/> x = <paramref name="rightHandSide"/> with x non-negative.
        ''' </summary>
        ''' <param name="constraints">The coefficient matrix, one row per equality constraint.</param>
        ''' <param name="rightHandSide">The right-hand side, one entry per row. Rows with a
        ''' negative entry are negated on the way in, so the caller does not have to.</param>
        ''' <param name="objective">The objective coefficients, one per variable.</param>
        ''' <param name="solution">The optimal vertex, or all zeros when there is not one.</param>
        ''' <returns>How the solve ended.</returns>
        Public Shared Function Minimize(constraints(,) As Double,
                                        rightHandSide() As Double,
                                        objective() As Double,
                                        ByRef solution() As Double) As SimplexStatus

            Dim m As Integer = rightHandSide.Length
            Dim n As Integer = objective.Length

            If constraints.GetLength(0) <> m Then
                Throw New ArgumentException("The constraint matrix has " & constraints.GetLength(0) &
                                            " rows and the right-hand side has " & m & ".")
            End If

            If constraints.GetLength(1) <> n Then
                Throw New ArgumentException("The constraint matrix has " & constraints.GetLength(1) &
                                            " columns and the objective has " & n & ".")
            End If

            ReDim solution(n - 1)

            ' Phase one carries one artificial variable per row, so the tableau is n + m wide.

            Dim sx As New Simplex(m, n + m)

            sx.BuildPhaseOne(constraints, rightHandSide, n)

            sx.Pivot(n + m)

            If -sx._tableau(m, n + m) > Epsilon Then Return SimplexStatus.Infeasible

            ' An artificial that stayed basic at zero is swapped out for any structural variable
            ' with a non-zero coefficient in its row; a row where none exists is redundant and is
            ' left alone, its artificial pinned at zero by the phase-two objective.

            sx.DriveArtificialsOut(n)

            sx.BuildPhaseTwo(objective, n)

            Dim status = sx.Pivot(n)

            If status <> SimplexStatus.Optimal Then Return status

            sx.ReadSolution(solution, n)

            Return SimplexStatus.Optimal

        End Function

        ''' <summary>
        ''' Lays out the phase-one tableau: the constraints with their artificial columns, and an
        ''' objective row that is the sum of the artificials, priced out so the basis is dual
        ''' feasible from the start.
        ''' </summary>
        Private Sub BuildPhaseOne(constraints(,) As Double, rightHandSide() As Double, n As Integer)

            For i = 0 To _rows - 1

                Dim flip As Double = If(rightHandSide(i) < 0.0, -1.0, 1.0)

                For j = 0 To n - 1
                    _tableau(i, j) = flip * constraints(i, j)
                Next

                _tableau(i, n + i) = 1.0
                _tableau(i, _columns) = flip * rightHandSide(i)

                _basis(i) = n + i

            Next

            ' minimise the sum of the artificials, expressed in the non-basic variables

            For i = 0 To _rows - 1
                For j = 0 To _columns
                    _tableau(_rows, j) -= _tableau(i, j)
                Next
            Next

            For i = 0 To _rows - 1
                _tableau(_rows, n + i) = 0.0
            Next

        End Sub

        ''' <summary>
        ''' Replaces the phase-one objective with the real one, priced out against the basis the
        ''' first phase ended on.
        ''' </summary>
        Private Sub BuildPhaseTwo(objective() As Double, n As Integer)

            For j = 0 To _columns
                _tableau(_rows, j) = 0.0
            Next

            For j = 0 To n - 1
                _tableau(_rows, j) = objective(j)
            Next

            For i = 0 To _rows - 1

                Dim basic As Integer = _basis(i)

                If basic >= n Then Continue For

                Dim factor As Double = _tableau(_rows, basic)

                If Math.Abs(factor) <= Epsilon Then Continue For

                For j = 0 To _columns
                    _tableau(_rows, j) -= factor * _tableau(i, j)
                Next

            Next

        End Sub

        ''' <summary>
        ''' Runs simplex iterations until no column prices out negative. Only the first
        ''' <paramref name="width"/> columns are candidates to enter, which is what keeps the
        ''' artificials out of the basis during phase two.
        ''' </summary>
        Private Function Pivot(width As Integer) As SimplexStatus

            ' Bland's rule terminates, so the cap only guards against a coding error.

            Dim limit As Integer = 100 * (_rows + _columns) + 1000

            For iteration = 1 To limit

                ' entering column: the lowest-numbered one that prices out negative

                Dim entering As Integer = -1

                For j = 0 To width - 1
                    If _tableau(_rows, j) < -Epsilon Then
                        entering = j
                        Exit For
                    End If
                Next

                If entering < 0 Then Return SimplexStatus.Optimal

                ' leaving row: the tightest ratio, ties broken by the lowest basic variable

                Dim leaving As Integer = -1
                Dim best As Double = Double.MaxValue

                For i = 0 To _rows - 1

                    If _tableau(i, entering) <= Epsilon Then Continue For

                    Dim ratio As Double = _tableau(i, _columns) / _tableau(i, entering)

                    If ratio < best - Epsilon OrElse
                       (ratio < best + Epsilon AndAlso leaving >= 0 AndAlso _basis(i) < _basis(leaving)) Then
                        best = ratio
                        leaving = i
                    End If

                Next

                If leaving < 0 Then Return SimplexStatus.Unbounded

                Eliminate(leaving, entering)

                _basis(leaving) = entering

            Next

            Throw New InvalidOperationException(
                "The simplex did not terminate in " & limit & " iterations.")

        End Function

        ''' <summary>
        ''' Makes <paramref name="column"/> the basic variable of <paramref name="row"/>.
        ''' </summary>
        Private Sub Eliminate(row As Integer, column As Integer)

            Dim pivot As Double = _tableau(row, column)

            For j = 0 To _columns
                _tableau(row, j) /= pivot
            Next

            _tableau(row, column) = 1.0

            For i = 0 To _rows

                If i = row Then Continue For

                Dim factor As Double = _tableau(i, column)

                If Math.Abs(factor) <= Epsilon Then Continue For

                For j = 0 To _columns
                    _tableau(i, j) -= factor * _tableau(row, j)
                Next

                _tableau(i, column) = 0.0

            Next

        End Sub

        ''' <summary>
        ''' Pivots any artificial variable that stayed in the basis at value zero out of it.
        ''' </summary>
        Private Sub DriveArtificialsOut(n As Integer)

            For i = 0 To _rows - 1

                If _basis(i) < n Then Continue For

                For j = 0 To n - 1

                    If Math.Abs(_tableau(i, j)) <= Epsilon Then Continue For

                    Eliminate(i, j)
                    _basis(i) = j

                    Exit For

                Next

            Next

        End Sub

        ''' <summary>
        ''' Reads the values of the structural variables off the final tableau.
        ''' </summary>
        Private Sub ReadSolution(solution() As Double, n As Integer)

            For i = 0 To _rows - 1

                Dim basic As Integer = _basis(i)

                If basic >= n Then Continue For

                Dim value As Double = _tableau(i, _columns)

                solution(basic) = If(Math.Abs(value) <= Epsilon, 0.0, value)

            Next

        End Sub

    End Class

End Namespace
