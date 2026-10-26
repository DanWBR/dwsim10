'    Rigorous Columns: derivatives of a converged Naphtali-Sandholm solution with respect to the feeds
'    Copyright 2008-2026 Daniel Wagner O. de Medeiros
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

Imports DWSIM.MathOps.MathEx
Imports DWSIM.UnitOperations.UnitOperations.Column
Imports MathNet.Numerics.LinearAlgebra.Double

Namespace UnitOperations.Auxiliary.SepOps.SolvingMethods

    ''' <summary>
    ''' The derivatives of a converged column's results with respect to its feeds, from the implicit function theorem on
    ''' the MESH equations: at the solution x of F(x, u) = 0, dx/du = -(dF/dx)^-1 dF/du, with dF/dx the solver's own
    ''' Jacobian. The inputs are, for each feed stage in FeedStages, the component feed flows (mol/s) and then the feed
    ''' enthalpy flow (kW). The outputs are, for each stage, T (K), V, L, VSS, LSS (mol/s), x and y, then Q(0) and Q(ns)
    ''' (kW, as the solver returns them).
    ''' </summary>
    Public Class ColumnSensitivities

        Public NumberOfCompounds As Integer
        Public NumberOfStages As Integer
        Public FeedStages As Integer()
        Public Values As Double()
        Public Derivatives As Double(,)
        ''' <summary>The largest scaled MESH residual at the solution the derivatives were taken at.</summary>
        Public Residual As Double

        Public ReadOnly Property Block As Integer
            Get
                Return 5 + 2 * NumberOfCompounds
            End Get
        End Property

        Public Function TIndex(stage As Integer) As Integer
            Return stage * Block
        End Function

        Public Function VIndex(stage As Integer) As Integer
            Return stage * Block + 1
        End Function

        Public Function LIndex(stage As Integer) As Integer
            Return stage * Block + 2
        End Function

        Public Function VSSIndex(stage As Integer) As Integer
            Return stage * Block + 3
        End Function

        Public Function LSSIndex(stage As Integer) As Integer
            Return stage * Block + 4
        End Function

        Public Function XIndex(stage As Integer, compound As Integer) As Integer
            Return stage * Block + 5 + compound
        End Function

        Public Function YIndex(stage As Integer, compound As Integer) As Integer
            Return stage * Block + 5 + NumberOfCompounds + compound
        End Function

        Public Function QIndex(top As Boolean) As Integer
            Return (NumberOfStages + 1) * Block + If(top, 0, 1)
        End Function

        ''' <summary>The input column of a feed stage's compound flow, or of its enthalpy flow (compound = -1).</summary>
        Public Function InputIndex(feed As Integer, compound As Integer) As Integer
            Return feed * (NumberOfCompounds + 1) + If(compound < 0, NumberOfCompounds, compound)
        End Function

    End Class

    Partial Public Class NaphtaliSandholmMethod

        ''' <summary>
        ''' When set, Solve fills it with the derivatives of its converged solution before it returns. A caller sets it
        ''' around one solve on its own thread.
        ''' </summary>
        <ThreadStatic> Public Shared SensitivityCapture As ColumnSensitivities

        Private Sub CaptureSensitivities(xvar As Double(), sens As ColumnSensitivities)

            Dim nc = _nc, ns = _ns
            Dim n = xvar.Length

            sens.NumberOfCompounds = nc
            sens.NumberOfStages = ns
            Dim feeds = Enumerable.Range(0, ns + 1).Where(Function(i) _F(i) > 0.0).ToArray()
            sens.FeedStages = feeds

            Dim res0 = FunctionValue(xvar)
            sens.Residual = res0.Select(Function(v) Math.Abs(v)).Max()
            sens.Values = Outputs(xvar)

            Dim J = DenseMatrix.OfArray(FunctionGradient(_jacEpsilon, xvar))

            ' dF/du: the residuals are linear in the feed flows and in the feed enthalpy flow, so a central difference of
            ' the residual function at fixed x is exact to roundoff
            Dim m = feeds.Length * (nc + 1)
            Dim Ju As New DenseMatrix(n, m)
            Dim flow0 = DirectCast(_F.Clone(), Double())
            Dim fc0 = _fc.Select(Function(r) DirectCast(r.Clone(), Double())).ToArray()
            Dim HF0 = DirectCast(_HF.Clone(), Double())
            For k = 0 To feeds.Length - 1
                Dim s = feeds(k)
                Dim flows = fc0(s).NormalizeY().MultiplyConstY(flow0(s))
                Dim Hflow = HF0(s) * flow0(s)
                For p = 0 To nc
                    Dim h = 0.000001 * Math.Max(flow0(s), 0.001)
                    If p = nc Then h = 0.000001 * Math.Max(Math.Abs(Hflow), 0.001)
                    Dim up = Perturbed(xvar, s, flows, Hflow, p, h)
                    Dim dn = Perturbed(xvar, s, flows, Hflow, p, -h)
                    For r = 0 To n - 1
                        Ju(r, sens.InputIndex(k, If(p = nc, -1, p))) = (up(r) - dn(r)) / (2 * h)
                    Next
                    _F(s) = flow0(s)
                    _fc(s) = DirectCast(fc0(s).Clone(), Double())
                    _HF(s) = HF0(s)
                Next
            Next

            Dim dx = J.LU().Solve(-Ju)

            ' the outputs along each direction, by a central difference of the explicit map from (x, u) to the results: they
            ' depend on u directly as well (the duties close the energy balances with the feed enthalpies)
            Dim ny = sens.Values.Length
            sens.Derivatives = New Double(ny - 1, m - 1) {}
            For k = 0 To feeds.Length - 1
                Dim s = feeds(k)
                Dim flows = fc0(s).NormalizeY().MultiplyConstY(flow0(s))
                Dim Hflow = HF0(s) * flow0(s)
                For p = 0 To nc
                    Dim q = sens.InputIndex(k, If(p = nc, -1, p))
                    Dim col = dx.Column(q).ToArray()
                    Dim uscale = If(p = nc, Math.Max(Math.Abs(Hflow), 0.001), Math.Max(flow0(s), 0.001))
                    Dim t = 0.000001 * uscale
                    Dim xscale = col.Select(Function(v) Math.Abs(v)).Max() * t
                    If xscale > 0.000001 Then t *= 0.000001 / xscale
                    Dim xp(n - 1), xm(n - 1) As Double
                    For r = 0 To n - 1
                        xp(r) = xvar(r) + t * col(r)
                        xm(r) = xvar(r) - t * col(r)
                    Next
                    SetFeed(s, flows, Hflow, p, t)
                    Dim yp = Outputs(xp)
                    SetFeed(s, flows, Hflow, p, -t)
                    Dim ym = Outputs(xm)
                    _F(s) = flow0(s)
                    _fc(s) = DirectCast(fc0(s).Clone(), Double())
                    _HF(s) = HF0(s)
                    For r = 0 To ny - 1
                        sens.Derivatives(r, q) = (yp(r) - ym(r)) / (2 * t)
                    Next
                Next
            Next

            ' the solver's state back at the solution
            FunctionValue(xvar)

        End Sub

        ''' <summary>The residuals at x with feed stage s's compound p flow (p = nc: its enthalpy flow) moved by h.</summary>
        Private Function Perturbed(xvar As Double(), s As Integer, flows As Double(), Hflow As Double, p As Integer, h As Double) As Double()
            SetFeed(s, flows, Hflow, p, h)
            Return FunctionValue(xvar)
        End Function

        ''' <summary>Feed stage s with its compound p flow (p = nc: its enthalpy flow) moved by h.</summary>
        Private Sub SetFeed(s As Integer, flows As Double(), Hflow As Double, p As Integer, h As Double)
            Dim fl = DirectCast(flows.Clone(), Double())
            Dim Hf = Hflow
            If p < _nc Then fl(p) += h Else Hf += h
            Dim Ft = fl.Sum()
            _F(s) = Ft
            _fc(s) = fl.MultiplyConstY(1.0 / Ft)
            _HF(s) = Hf / Ft
        End Sub

        ''' <summary>The results at x as Solve hands them back (the same mapping as its last lines).</summary>
        Private Function Outputs(xvar As Double()) As Double()

            Dim nc = _nc, ns = _ns
            FunctionValue(xvar)

            Dim Tj(ns), Vj(ns), Lj(ns), VSSj(ns), LSSj(ns), Sl(ns), Sv(ns), sumvkj(ns), sumlkj(ns) As Double
            Dim xc(ns)(), yc(ns)() As Double
            For i = 0 To ns
                xc(i) = New Double(nc - 1) {}
                yc(i) = New Double(nc - 1) {}
                Tj(i) = xvar(i * (2 * nc + 1)) * _maxT
                For j = 0 To nc - 1
                    sumvkj(i) += xvar(i * (2 * nc + 1) + j + 1) * _maxvc
                    sumlkj(i) += xvar(i * (2 * nc + 1) + j + 1 + nc) * _maxlc
                Next
                Vj(i) = sumvkj(i)
                Lj(i) = sumlkj(i)
            Next
            For i = 0 To ns
                For j = 0 To nc - 1
                    If sumlkj(i) > 0 Then xc(i)(j) = xvar(i * (2 * nc + 1) + j + 1 + nc) * _maxlc / Lj(i)
                    If Vj(i) > 0 Then
                        yc(i)(j) = xvar(i * (2 * nc + 1) + j + 1) * _maxvc / Vj(i)
                    Else
                        yc(i)(j) = xc(i)(j) * _Kval(i)(j)
                    End If
                Next
            Next

            For i = 0 To ns
                VSSj(i) = _VSS(i)
                LSSj(i) = _LSS(i)
            Next
            Dim sumLSS = 0.0, sumVSS = 0.0
            For i = 0 To ns
                sumVSS += VSSj(i)
            Next
            For i = 1 To ns
                sumLSS += LSSj(i)
            Next
            If _coltype = ColType.DistillationColumn Then
                If _condtype = condtype.Full_Reflux Then
                    Vj(0) = _F.Sum - Lj(ns) - sumLSS - sumVSS
                    LSSj(0) = 0.0
                Else
                    LSSj(0) = _F.Sum - Lj(ns) - sumLSS - sumVSS - Vj(0)
                End If
            Else
                LSSj(0) = 0.0
            End If
            For i = 0 To ns
                If Vj(i) <> 0 Then Sv(i) = VSSj(i) / Vj(i) Else Sv(i) = 0
                If Lj(i) <> 0 Then Sl(i) = LSSj(i) / Lj(i) Else Sl(i) = 0
            Next
            For i = 0 To ns
                Lj(i) = sumlkj(i)
                If _coltype = ColType.DistillationColumn And i = 0 Then
                    If _condtype = condtype.Total_Condenser Then
                        LSSj(0) = Vj(0)
                        Vj(0) = 0.0
                    Else
                        LSSj(0) = Sl(0) * Lj(0)
                        Vj(i) = sumvkj(0)
                    End If
                Else
                    LSSj(i) = Sl(i) * Lj(i)
                    Vj(i) = sumvkj(i)
                End If
                VSSj(i) = Sv(i) * Vj(i)
            Next

            Dim Q = DirectCast(_Q.Clone(), Double())
            If _specs("C").SType = ColumnSpec.SpecType.Heat_Duty Then Q(0) = _spval1
            If _specs("R").SType = ColumnSpec.SpecType.Heat_Duty Then Q(ns) = -_spval2

            Dim block = 5 + 2 * nc
            Dim y((ns + 1) * block + 1) As Double
            For i = 0 To ns
                y(i * block) = Tj(i)
                y(i * block + 1) = Vj(i)
                y(i * block + 2) = Lj(i)
                y(i * block + 3) = VSSj(i)
                y(i * block + 4) = LSSj(i)
                For j = 0 To nc - 1
                    y(i * block + 5 + j) = xc(i)(j)
                    y(i * block + 5 + nc + j) = yc(i)(j)
                Next
            Next
            y((ns + 1) * block) = Q(0)
            y((ns + 1) * block + 1) = Q(ns)
            Return y

        End Function

    End Class

End Namespace
