'    Rigorous Columns (Distillation and Absorption) Solvers
'    Copyright 2008-2022 Daniel Wagner O. de Medeiros
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

Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics
Imports DWSIM.UnitOperations.UnitOperations
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.SolvingMethods
Imports System.Math

Namespace UnitOperations.Auxiliary.SepOps.SolvingMethods

    ''' <summary>
    ''' Implements the Burningham–Otto Sum-Rates (SR) method for solving the MESH equations
    ''' of absorption and stripping columns. Unlike the Bubble-Point method, the SR method
    ''' tears on stage temperatures rather than compositions, making it more suitable for
    ''' wide-boiling-range systems.
    ''' </summary>
    <System.Serializable()> Public Class BurninghamOttoMethod

        Inherits ColumnSolver

        ''' <summary>Gets or sets whether liquid-composition updates are relaxed (damped) between iterations to aid convergence.</summary>
        Public Shared Property RelaxCompositionUpdates As Boolean = False

        ''' <summary>Gets or sets whether temperature updates are relaxed (damped) between iterations to aid convergence.</summary>
        Public Shared Property RelaxTemperatureUpdates As Boolean = False

        ''' <summary>
        ''' Set DWSIM_COLUMN_TRACE=1 to write the stage temperature, liquid and vapour
        ''' profiles to the console once per iteration. The converged profiles are the
        ''' only ones the column publishes, so without this there is no way to watch a
        ''' run that never converges.
        ''' </summary>
        Private Shared ReadOnly TraceProfiles As Boolean =
            Environment.GetEnvironmentVariable("DWSIM_COLUMN_TRACE") = "1"

        ''' <summary>Gets the solver display name (not implemented for this class).</summary>
        Public Overrides ReadOnly Property Name As String
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        ''' <summary>Gets the solver description (not implemented for this class).</summary>
        Public Overrides ReadOnly Property Description As String
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        ''' <summary>
        ''' Solves the absorption/stripping column using the Burningham–Otto Sum-Rates method.
        ''' </summary>
        ''' <param name="rc">The rigorous column object being solved.</param>
        ''' <param name="nc">Number of chemical components.</param>
        ''' <param name="ns">Number of equilibrium stages (0-based).</param>
        ''' <param name="maxits">Maximum number of outer iterations.</param>
        ''' <param name="tol">Convergence tolerance array.</param>
        ''' <param name="F">Feed molar flow rate for each stage (mol/s).</param>
        ''' <param name="V">Initial vapour molar flow rate profile (mol/s).</param>
        ''' <param name="Q">Stage heat duty profile (kW).</param>
        ''' <param name="L">Initial liquid molar flow rate profile (mol/s).</param>
        ''' <param name="VSS">Vapour side-stream draw-off rates (mol/s).</param>
        ''' <param name="LSS">Liquid side-stream draw-off rates (mol/s).</param>
        ''' <param name="Kval">Initial K-value matrix (stage × component).</param>
        ''' <param name="x">Initial liquid mole-fraction matrix (stage × component).</param>
        ''' <param name="y">Initial vapour mole-fraction matrix (stage × component).</param>
        ''' <param name="z">Feed composition matrix (stage × component).</param>
        ''' <param name="fc">Feed component molar flow matrix (stage × component).</param>
        ''' <param name="HF">Feed molar enthalpy for each stage (kJ/mol).</param>
        ''' <param name="T">Initial temperature profile (K).</param>
        ''' <param name="P">Pressure profile (Pa).</param>
        ''' <param name="stopatitnumber">Iteration number at which to pause (0 = run to completion).</param>
        ''' <param name="eff">Murphree tray efficiency for each stage.</param>
        ''' <param name="pp">Property package used for thermodynamic calculations.</param>
        ''' <param name="specs">Column specifications (condenser and reboiler specs).</param>
        ''' <param name="IdealK">If <c>True</c>, use ideal (Raoult) K-values.</param>
        ''' <param name="IdealH">If <c>True</c>, use ideal enthalpies.</param>
        ''' <param name="llextr">If <c>True</c>, the column operates in liquid–liquid extraction mode.</param>
        ''' <returns>An object array containing the converged column profiles.</returns>
        Public Shared Function Solve(rc As Column, ByVal nc As Integer, ByVal ns As Integer, ByVal maxits As Integer,
                                ByVal tol As Double(), ByVal F As Double(), ByVal V As Double(),
                                ByVal Q As Double(), ByVal L As Double(),
                                ByVal VSS As Double(), ByVal LSS As Double(), ByVal Kval()() As Double,
                                ByVal x()() As Double, ByVal y()() As Double, ByVal z()() As Double,
                                ByVal fc()() As Double,
                                ByVal HF As Double(), ByVal T As Double(), ByVal P As Double(),
                                ByVal stopatitnumber As Integer,
                                ByVal eff() As Double,
                                ByVal pp As PropertyPackages.PropertyPackage,
                                ByVal specs As Dictionary(Of String, SepOps.ColumnSpec),
                                ByVal IdealK As Boolean, ByVal IdealH As Boolean,
                                Optional ByVal llextr As Boolean = False) As Object

            rc.ColumnSolverConvergenceReport = ""

            Dim reporter As Text.StringBuilder = Nothing

            If rc.CreateSolverConvergengeReport Then reporter = New Text.StringBuilder()

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Solve", "Sum-Rates (SR) Method", "Burningham–Otto Sum-Rates (SR) Method for Absorption and Stripping", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("The species in most absorbers and strippers cover a wide range of volatility. Hence, the BP method of 
                                    solving the MESH equations fails because bubble-point temperature calculations are too sensitive to 
                                    liquid-phase composition, and the stage energy balance is much more sensitive to stage temperatures 
                                    than to interstage flow rates. In this case, Friday and Smith showed that an alternative procedure 
                                    devised by Sujata could be used. This sum-rates (SR) method was further developed in conjunction with 
                                    the tridiagonal-matrix formulation for the modified M equations by Burningham and Otto.")

            IObj?.Paragraphs.Add("Problem specifications consist of conditions and stage locations for feeds, stage pressure, sidestream 
                                    flow rates, stage heat-transfer rates, and number of stages. Tear variables Tj and Vj are assumed to 
                                    initiate the calculations. It is sufficient to assume a set of Vj values based on the assumption of 
                                    constant-molar flows, working up from the absorber bottom using specified vapor feeds and vapor sidestream 
                                    flows, if any. An initial set of Tj values can be obtained from assumed top-stage and bottom-stage values 
                                    and a linear variation with stages in between. Values of xi,j are established by solving the tridiagonal 
                                    matrix by the Thomas algorithm. These values are not normalized but utilized directly to produce new values 
                                    of Lj through the sum-rates equation:")

            IObj?.Paragraphs.Add("<math>L^{(k+1)}_{j}=L^{(k)}_j\sum\limits_{i=1}^{C}{x_{i,j}}</math>")

            IObj?.Paragraphs.Add("where <math_inline>L^{(k)}_j</math_inline> values are obtained from <math_inline>V^{(k)}_j</math_inline> 
                                    values by")

            IObj?.Paragraphs.Add("<math>L_{j}=V_{j+1}+\sum\limits_{m=1}^{j}{(F_{m}-U_{m}-W_{m})-V_{1}}</math>")

            IObj?.Paragraphs.Add("Corresponding values of <math_inline>V^{(k+1)}_j</math_inline> are obtained from a total material balance:")

            IObj?.Paragraphs.Add("<math>V_{j}=L_{j-1}-L_{N}+\sum\limits_{m=j}^{N}{(F_{m}-U_{m}-W_{m})}</math>")

            IObj?.Paragraphs.Add("Normalized <math_inline>x_{i,j}</math_inline> values are calculated and Corresponding values of 
                                    <math_inline>y_{i,j}</math_inline> are computed from")

            IObj?.Paragraphs.Add("<math>y_{i,j}=K_{i,j}x_{i,j}</math>")

            IObj?.Paragraphs.Add("A new set of Tj is obtained by solving the simultaneous energy-balance relations for the N stages. The 
                                    temperatures are embedded in the specific enthalpies for the unspecified vapor and liquid flow rates. 
                                    Typically, these enthalpies are nonlinear in temperature. Therefore, an iterative procedure such as the 
                                    Newton–Raphson method is required.")

            IObj?.Paragraphs.Add("To obtain a new set of Tj from the energy equation, the Newton–Raphson recursion equation is")

            IObj?.Paragraphs.Add("<math>\left(\frac{\partial H_j}{\partial T_{j-1}} \right)^{(r)}\Delta T^{(r)}_{j-1} +\left(\frac{\partial H_j}{\partial T_{j}} \right)^{(r)}\Delta T^{(r)}_{j}+ \left(\frac{\partial H_j}{\partial T_{j+1}} \right)^{(r)}\Delta T^{(r)}_{j+1}=-H^{(r)}_j</math>")

            IObj?.Paragraphs.Add("where")

            IObj?.Paragraphs.Add("<math>\Delta T^{(r)}_{j}=T^{(r+1)}_j-T^{(r)}_j</math>")

            IObj?.Paragraphs.Add("<math>\frac{\partial H_j}{\partial T_{j-1}}=L_{j-1}\frac{\partial h_{L_{j-1}}}{\partial T_{j-1}}</math>")

            IObj?.Paragraphs.Add("<math>\frac{\partial H_j}{\partial T_{j}}=-(L_{j}+U_{j})\frac{\partial h_{L_{j}}}{\partial T_{j}}-(V_j+W_j)\frac{\partial h_{V_{j}}}{\partial T_{j}}</math>")

            IObj?.Paragraphs.Add("<math>\frac{\partial H_j}{\partial T_{j+1}}=V_{j+1}\frac{\partial h_{V_{j+1}}}{\partial T_{j+1}}</math>")

            IObj?.Paragraphs.Add("The partial derivatives are calculated numerically using the values provided by the Property Package.")

            IObj?.Paragraphs.Add("The N relations form a tridiagonal matrix equation that is linear in <math_inline>\Delta T^{(r)}_{j}</math_inline>.")

            IObj?.Paragraphs.Add("The matrix of partial derivatives is called the Jacobian correction matrix. The Thomas algorithm can be employed 
                                    to solve for the set of corrections <math_inline>\Delta T^{(r)}_{j}</math_inline>. New guesses of Tj are then determined from")

            IObj?.Paragraphs.Add("<math>T_j^{(r+1)}=T_j^{(r)}+\Delta T_j^{(r)}</math>")

            IObj?.Paragraphs.Add("When corrections <math_inline>\Delta T^{(r)}_{j}</math_inline> approach zero, the resulting values of Tj are 
                                    used with criteria to determine if convergence has been achieved. If not, before beginning a new k iteration, 
                                    values of Vj and Tj are adjusted. Convergence is rapid for the sum-rates method.")

            Dim ppr As PropertyPackages.RaoultPropertyPackage = Nothing

            If IdealK Or IdealH Then
                ppr = New PropertyPackages.RaoultPropertyPackage
                ppr.Flowsheet = pp.Flowsheet
                ppr.CurrentMaterialStream = pp.CurrentMaterialStream
            End If

            Dim doparallel As Boolean = Settings.EnableParallelProcessing
            Dim poptions As New ParallelOptions() With {.MaxDegreeOfParallelism = Settings.MaxDegreeOfParallelism}

            Dim ic As Integer
            Dim t_error, comperror As Double
            Dim Tj(ns), Tj_ant(ns) As Double
            Dim Fj(ns), Lj(ns), Vj(ns), Vj_ant(ns), xc(ns)(), fcj(ns)(), yc(ns)(), yc_ant(ns)(), lc(ns)(), vc(ns)(), zc(ns)(), K(ns)() As Double
            Dim Hfj(ns), Hv(ns), Hl(ns) As Double
            Dim VSSj(ns), LSSj(ns) As Double
            Dim sum1(ns), sum2(ns), sum3(ns) As Double

            'step1

            'step2

            Dim i, j As Integer

            For i = 0 To ns
                Array.Resize(fcj(i), nc)
                Array.Resize(xc(i), nc)
                Array.Resize(yc(i), nc)
                Array.Resize(yc_ant(i), nc)
                Array.Resize(lc(i), nc)
                Array.Resize(vc(i), nc)
                Array.Resize(zc(i), nc)
                Array.Resize(zc(i), nc)
                Array.Resize(K(i), nc)
            Next

            For i = 0 To ns
                If eff(i) = 0.0 Then eff(i) = 1.0
                VSSj(i) = VSS(i)
                LSSj(i) = LSS(i)
                Lj(i) = L(i)
                Vj(i) = V(i)
                Tj(i) = T(i)
                Fj(i) = F(i)
                Hfj(i) = HF(i) / 1000
                fcj(i) = fc(i)
            Next

            ' Physical temperature envelope for the relaxed retry pass. The initial Tj profile is a linear
            ' interpolation between the feed/product temperatures, so its range brackets the physical stage
            ' temperatures; a converged column stays within a modest margin of it. Clamping to this envelope
            ' on the relaxed pass stops the temperature<->K<->flow feedback of stiff wide-boiling systems from
            ' running away (e.g. a stage drifting to 650 K, which sends the K-values and internal flows to
            ' non-physical values from which the sum-rates iteration cannot recover).
            Dim Tmin_env As Double = T.Min - 8.0
            Dim Tmax_env As Double = T.Max + 8.0
            If Tmin_env < 100.0 Then Tmin_env = 100.0

            ' A second, generous envelope enforced on EVERY pass, not just the relaxed retry.
            ' Leaving the default pass unbounded is what lets a stripper's top stages fall to
            ' 219 K and its bottom climb past 490 K on a column whose real profile spans some
            ' 15 K: once the temperatures leave the physical range the K-values follow, the
            ' component balance answers with enormous flows, and the run is unrecoverable.
            ' Scaled to the initial profile so it only ever catches a runaway - a column whose
            ' true profile reaches beyond its linear starting ramp is left alone.
            Dim Tmargin As Double = Math.Max(0.5 * (T.Max - T.Min), 25.0)
            Dim Tmin_hard As Double = T.Min - Tmargin
            Dim Tmax_hard As Double = T.Max + Tmargin
            If Tmin_hard < 100.0 Then Tmin_hard = 100.0

            ' Per-iteration step bound, scaled to the column rather than to the absolute
            ' temperature: 10 % of Tj is 37 K at 373 K, which on a narrow column is a jump
            ' across the whole profile in a single step.
            Dim Tstep_max As Double = Math.Max(0.5 * (T.Max - T.Min), 5.0)

            Dim sumFeed As Double = 0.0
            For i = 0 To ns
                sumFeed += Fj(i)
            Next
            If sumFeed <= 0.0 Then sumFeed = 1.0

            IObj?.Paragraphs.Add("<h2>Input Parameters / Initial Estimates</h2>")

            IObj?.Paragraphs.Add(String.Format("Stage Temperatures: {0}", T.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Stage Pressures: {0}", P.ToMathArrayString))

            IObj?.Paragraphs.Add(String.Format("Feeds: {0}", F.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Vapor Flows: {0}", V.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Liquid Flows: {0}", L.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Vapor Side Draws: {0}", VSS.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Liquid Side Draws: {0}", LSS.ToMathArrayString))

            IObj?.Paragraphs.Add(String.Format("Mixture Compositions: {0}", z.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Liquid Phase Compositions: {0}", x.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Vapor/Liquid2 Phase Compositions: {0}", y.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("K-values: {0}", K.ToMathArrayString))

            IObj?.Paragraphs.Add("<h2>Calculated Parameters</h2>")

            For i = 0 To ns
                For j = 0 To nc - 1
                    K(i)(j) = Kval(i)(j)
                    IObj?.SetCurrent()
                    If Double.IsNaN(K(i)(j)) Or Double.IsInfinity(K(i)(j)) Or K(i)(j) = 0# Then
                        If llextr Then
                            If i > 0 Then
                                If K(i - 1).Sum > 0.0 Then
                                    K(i) = K(i - 1).Clone
                                End If
                            End If
                        Else
                            K(i)(j) = pp.AUX_PVAPi(j, T(i)) / P(i)
                        End If
                    End If
                Next
            Next

            'step3

            Dim names = pp.RET_VNAMES().ToList()

            reporter?.AppendLine("========================================================")
            reporter?.AppendLine(String.Format("Initial Estimates"))
            reporter?.AppendLine("========================================================")
            reporter?.AppendLine()

            reporter?.AppendLine("Stage Conditions & Flows")
            reporter?.AppendLine(String.Format("{0,-16}{1,16}{2,16}{3,16}{4,16}{5,16}",
                                                   "Stage", "P (Pa)", "T (K)",
                                                   "L1 (mol/s)", "V/L2 (mol/s)", "LSS (mol/s)"))
            For i = 0 To ns
                reporter?.AppendLine(String.Format("{0,-16}{1,16:G6}{2,16:G6}{3,16:G6}{4,16:G6}{5,16:G6}",
                                                   i + 1, P(i), T(i), L(i), V(i), LSS(i)))
            Next

            reporter?.AppendLine()
            reporter?.AppendLine("Stage Molar Fractions - Liquid 1")
            reporter?.Append("Stage".PadRight(20))
            For j = 0 To nc - 1
                reporter?.Append(names(j).PadLeft(20))
            Next
            reporter?.Append(vbCrLf)
            For i = 0 To ns
                reporter?.Append((i + 1).ToString().PadRight(20))
                For j = 0 To nc - 1
                    reporter?.Append(x(i)(j).ToString("G6").PadLeft(20))
                Next
                reporter?.Append(vbCrLf)
            Next

            reporter?.AppendLine()
            reporter?.AppendLine("Stage Molar Fractions - Vapor/Liquid 2")
            reporter?.Append("Stage".PadRight(20))
            For j = 0 To nc - 1
                reporter?.Append(names(j).PadLeft(20))
            Next
            reporter?.Append(vbCrLf)
            For i = 0 To ns
                reporter?.Append((i + 1).ToString().PadRight(20))
                For j = 0 To nc - 1
                    reporter?.Append(y(i)(j).ToString("G6").PadLeft(20))
                Next
                reporter?.Append(vbCrLf)
            Next

            reporter?.AppendLine()
            reporter?.AppendLine()

            Dim t_error_hist As New List(Of Double)
            Dim comp_error_hist As New List(Of Double)

            'internal loop
            ic = 0
            Do

                IObj?.SetCurrent()

                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj2, "", "Solve", "Sum-Rates (SR) Internal Loop #" & ic, "Burningham–Otto Sum-Rates (SR) Method for Absorption and Stripping", True)

                Dim il_err As Double = 0.0#
                Dim il_err_ant As Double = 0.0#
                Dim num, denom, x0, fx0 As New ArrayList

                'step4

                'find component liquid flows by the tridiagonal matrix method

                IObj2?.Paragraphs.Add(String.Format("Find component liquid flows by the tridiagonal matrix method"))
                IObj2?.Paragraphs.Add(String.Format("Calculating TDM A, B, C, D"))

                Dim at(nc - 1)(), bt(nc - 1)(), ct(nc - 1)(), dt(nc - 1)(), xt(nc - 1)() As Double

                For i = 0 To nc - 1
                    Array.Resize(at(i), ns + 1)
                    Array.Resize(bt(i), ns + 1)
                    Array.Resize(ct(i), ns + 1)
                    Array.Resize(dt(i), ns + 1)
                    Array.Resize(xt(i), ns + 1)
                Next

                For i = 0 To ns
                    sum1(i) = 0
                    sum2(i) = 0
                    For j = 0 To i
                        sum1(i) += Fj(j) - LSSj(j) - VSSj(j)
                    Next
                    If i > 0 Then
                        For j = 0 To i - 1
                            sum2(i) += Fj(j) - LSSj(j) - VSSj(j)
                        Next
                    End If
                Next

                For i = 0 To nc - 1
                    For j = 0 To ns
                        dt(i)(j) = -Fj(j) * fcj(j)(i)
                        If j < ns Then
                            bt(i)(j) = -(Vj(j + 1) + sum1(j) - Vj(0) + LSSj(j) + (Vj(j) + VSSj(j)) * K(j)(i))
                        Else
                            bt(i)(j) = -(sum1(j) - Vj(0) + LSSj(j) + (Vj(j) + VSSj(j)) * K(j)(i))
                        End If
                        'tdma solve
                        If j < ns Then ct(i)(j) = Vj(j + 1) * K(j + 1)(i)
                        If j > 0 Then at(i)(j) = Vj(j) + sum2(j) - Vj(0)
                    Next
                Next

                'solve matrices

                IObj2?.Paragraphs.Add(String.Format("Calling TDM Solver to calculate liquid phase compositions"))

                IObj2?.SetCurrent()

                IObj2?.Paragraphs.Add(String.Format("A: {0}", at.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("B: {0}", bt.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("C: {0}", ct.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("D: {0}", dt.ToMathArrayString))

                'tomich

                For i = 0 To nc - 1
                    IObj2?.SetCurrent()
                    IObj2?.Paragraphs.Add(String.Format("Calling TDM Solver for Stage #{0}...", i + 1))
                    xt(i) = Tomich.TDMASolve(at(i), bt(i), ct(i), dt(i))
                Next

                IObj2?.SetCurrent()

                IObj2?.Paragraphs.Add(String.Format("TDM solved successfully."))

                Dim sumx(ns), sumy(ns), sumz(ns) As Double

                For i = 0 To ns
                    sumx(i) = 0
                    For j = 0 To nc - 1
                        lc(i)(j) = xt(j)(i)
                        If lc(i)(j) < 0 Then lc(i)(j) = 0.0000001
                        sumx(i) += lc(i)(j)
                    Next
                    If Double.IsNaN(sumx(i)) Then
                        Throw New Exception(String.Format("Failed to update liquid phase composition on stage {0}", i))
                    End If
                Next

                'Ljs
                For i = 0 To ns
                    Dim ljfactor As Double = sumx(i)
                    If RelaxCompositionUpdates Then
                        ' Damp the sum-rates liquid-flow correction. On stiff, wide-boiling systems the raw
                        ' multiplicative update Lj = Lj*sumx can blow the internal flows up by orders of
                        ' magnitude in a single iteration; relaxing the factor toward 1 (opening up as the
                        ' iterations proceed) keeps the flows bounded while preserving the fixed point (sumx->1).
                        ' Persistent (non-vanishing) damping: limit the liquid flow to change by at most a
                        ' modest fraction per iteration. A ramp that opens to 1.0 lets the raw update take over
                        ' and the flows oscillate on stiff systems, so keep the damping fixed and the factor in
                        ' a tight band around 1.
                        ljfactor = 1.0 + 0.15 * (sumx(i) - 1.0)
                        If ljfactor < 0.6 Then ljfactor = 0.6
                        If ljfactor > 1.4 Then ljfactor = 1.4
                    End If
                    Lj(i) = Lj(i) * ljfactor
                    ' Physical envelope on the internal liquid flow (relaxed pass): in a stripper/absorber the
                    ' interstage liquid stays O(total feed), so bound it both below and above. This keeps the
                    ' flow profile physical and stops stages drifting to zero or to a blow-up, which otherwise
                    ' sends compositions/K-values non-physical and prevents the iteration from settling.
                    If RelaxCompositionUpdates Then
                        Dim Lmax As Double = 10.0 * sumFeed
                        Dim Lmin As Double = 0.02 * sumFeed
                        If Lj(i) > Lmax Then Lj(i) = Lmax
                        If Lj(i) < Lmin Then Lj(i) = Lmin
                    End If
                Next

                IObj2?.Paragraphs.Add(String.Format("l: {0}", lc.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("L: {0}", Lj.ToMathArrayString))

                For i = 0 To ns
                    For j = 0 To nc - 1
                        xc(i)(j) = lc(i)(j) / sumx(i)
                        yc(i)(j) = xc(i)(j) * K(i)(j)
                        sumy(i) += yc(i)(j)
                    Next
                Next

                IObj2?.Paragraphs.Add(String.Format("x: {0}", xc.ToMathArrayString))

                For i = 0 To ns
                    For j = 0 To nc - 1
                        yc(i)(j) = yc(i)(j) / sumy(i)
                    Next
                Next

                IObj2?.Paragraphs.Add(String.Format("y: {0}", yc.ToMathArrayString))

                For i = 0 To ns
                    sum3(i) = 0
                    For j = i To ns
                        sum3(i) += Fj(j) - LSSj(j) - VSSj(j)
                    Next
                Next

                'solve matrices

                For i = 0 To ns
                    Vj_ant(i) = Vj(i)
                Next

                ' A negative vapour flow out of the overall balance means the current
                ' liquid profile is inconsistent with the feeds, not that the vapour
                ' runs backwards. Mirroring the sign turns -5 mol/s into +5 mol/s, a
                ' value as wrong as the one it replaced and on the opposite side of the
                ' answer, so the next sweep is pushed back the other way: it is a ready
                ' source of the oscillation the sum-rates loop can settle into. Hold the
                ' stage at a small positive flow instead and let the balance correct it.
                Dim Vmin As Double = Math.Max(0.000001 * sumFeed, 0.0000000001)

                For i = 0 To ns
                    If i > 0 Then
                        Vj(i) = Lj(i - 1) - Lj(ns) + sum3(i)
                    Else
                        Vj(i) = -Lj(ns) + sum3(i)
                    End If
                    If Vj(i) < Vmin Then Vj(i) = Vmin
                Next

                For i = 0 To ns
                    sumz(i) = 0
                    For j = 0 To nc - 1
                        vc(i)(j) = xc(i)(j) * Vj(i) * K(i)(j)
                        zc(i)(j) = (lc(i)(j) + vc(i)(j)) / (Lj(i) + Vj(i))
                        sumz(i) += zc(i)(j)
                    Next
                Next

                IObj2?.Paragraphs.Add(String.Format("v: {0}", vc.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("V: {0}", Vj.ToMathArrayString))

                For i = 0 To ns
                    For j = 0 To nc - 1
                        zc(i)(j) = zc(i)(j) / sumz(i)
                    Next
                Next

                'Dim tmp As Object

                'calculate new temperatures

                IObj2?.Paragraphs.Add("Calculating new temperatures...")

                Dim H(ns), dHldT(ns), dHvdT(ns), dHdTa(ns), dHdTb(ns), dHdTc(ns), dHl(ns), dHv(ns), dHl2(ns), dHv2(ns) As Double

                Dim Hr(ns), Hr1(ns), Hr2(ns), dHr(ns) As Double

                Dim epsilon As Double = 0.01

                If doparallel Then

                    Dim task1 As Task = TaskHelper.Run(Sub() Parallel.For(0, ns + 1,
                                                             Sub(ipar)
                                                                 If IdealH Then
                                                                     Hl(ipar) = ppr.DW_CalcEnthalpy(xc(ipar), Tj(ipar), P(ipar), PropertyPackages.State.Liquid) * ppr.AUX_MMM(xc(ipar)) / 1000
                                                                     dHl(ipar) = ppr.DW_CalcEnthalpy(xc(ipar), Tj(ipar) - epsilon, P(ipar), PropertyPackages.State.Liquid) * ppr.AUX_MMM(xc(ipar)) / 1000
                                                                     dHl2(ipar) = ppr.DW_CalcEnthalpy(xc(ipar), Tj(ipar) + epsilon, P(ipar), PropertyPackages.State.Liquid) * ppr.AUX_MMM(xc(ipar)) / 1000
                                                                     If llextr Then
                                                                         Hv(ipar) = ppr.DW_CalcEnthalpy(yc(ipar), Tj(ipar), P(ipar), PropertyPackages.State.Liquid) * ppr.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv(ipar) = ppr.DW_CalcEnthalpy(yc(ipar), Tj(ipar) - epsilon, P(ipar), PropertyPackages.State.Liquid) * ppr.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv2(ipar) = ppr.DW_CalcEnthalpy(yc(ipar), Tj(ipar) + epsilon, P(ipar), PropertyPackages.State.Liquid) * ppr.AUX_MMM(yc(ipar)) / 1000
                                                                     Else
                                                                         Hv(ipar) = ppr.DW_CalcEnthalpy(yc(ipar), Tj(ipar), P(ipar), PropertyPackages.State.Vapor) * ppr.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv(ipar) = ppr.DW_CalcEnthalpy(yc(ipar), Tj(ipar) - epsilon, P(ipar), PropertyPackages.State.Vapor) * ppr.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv2(ipar) = ppr.DW_CalcEnthalpy(yc(ipar), Tj(ipar) + epsilon, P(ipar), PropertyPackages.State.Vapor) * ppr.AUX_MMM(yc(ipar)) / 1000
                                                                     End If
                                                                 Else
                                                                     Hl(ipar) = pp.DW_CalcEnthalpy(xc(ipar), Tj(ipar), P(ipar), PropertyPackages.State.Liquid) * pp.AUX_MMM(xc(ipar)) / 1000
                                                                     dHl(ipar) = pp.DW_CalcEnthalpy(xc(ipar), Tj(ipar) - epsilon, P(ipar), PropertyPackages.State.Liquid) * pp.AUX_MMM(xc(ipar)) / 1000
                                                                     dHl2(ipar) = pp.DW_CalcEnthalpy(xc(ipar), Tj(ipar) + epsilon, P(ipar), PropertyPackages.State.Liquid) * pp.AUX_MMM(xc(ipar)) / 1000
                                                                     If llextr Then
                                                                         Hv(ipar) = pp.DW_CalcEnthalpy(yc(ipar), Tj(ipar), P(ipar), PropertyPackages.State.Liquid) * pp.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv(ipar) = pp.DW_CalcEnthalpy(yc(ipar), Tj(ipar) - epsilon, P(ipar), PropertyPackages.State.Liquid) * pp.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv2(ipar) = pp.DW_CalcEnthalpy(yc(ipar), Tj(ipar) + epsilon, P(ipar), PropertyPackages.State.Liquid) * pp.AUX_MMM(yc(ipar)) / 1000
                                                                     Else
                                                                         Hv(ipar) = pp.DW_CalcEnthalpy(yc(ipar), Tj(ipar), P(ipar), PropertyPackages.State.Vapor) * pp.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv(ipar) = pp.DW_CalcEnthalpy(yc(ipar), Tj(ipar) - epsilon, P(ipar), PropertyPackages.State.Vapor) * pp.AUX_MMM(yc(ipar)) / 1000
                                                                         dHv2(ipar) = pp.DW_CalcEnthalpy(yc(ipar), Tj(ipar) + epsilon, P(ipar), PropertyPackages.State.Vapor) * pp.AUX_MMM(yc(ipar)) / 1000
                                                                     End If
                                                                     If pp.HasReactivePhase Then
                                                                         Hr(ipar) = pp.DW_CalcEnthalpyOfReaction(xc(ipar).MultiplyConstY(Lj(ipar)), Tj(ipar), P(ipar))
                                                                         Hr1(ipar) = pp.DW_CalcEnthalpyOfReaction(xc(ipar).MultiplyConstY(Lj(ipar)), Tj(ipar) - epsilon, P(ipar))
                                                                         Hr2(ipar) = pp.DW_CalcEnthalpyOfReaction(xc(ipar).MultiplyConstY(Lj(ipar)), Tj(ipar) + epsilon, P(ipar))
                                                                     End If
                                                                 End If
                                                             End Sub),
                                                      Settings.TaskCancellationTokenSource.Token)

                    task1.Wait(30000)
                Else
                    If IdealH Then
                        For i = 0 To ns
                            IObj2?.SetCurrent()
                            Hl(i) = ppr.DW_CalcEnthalpy(xc(i), Tj(i), P(i), PropertyPackages.State.Liquid) * ppr.AUX_MMM(xc(i)) / 1000
                            IObj2?.SetCurrent()
                            dHl(i) = ppr.DW_CalcEnthalpy(xc(i), Tj(i) - epsilon, P(i), PropertyPackages.State.Liquid) * ppr.AUX_MMM(xc(i)) / 1000
                            dHl2(i) = ppr.DW_CalcEnthalpy(xc(i), Tj(i) + epsilon, P(i), PropertyPackages.State.Liquid) * ppr.AUX_MMM(xc(i)) / 1000
                            IObj2?.SetCurrent()
                            If llextr Then
                                Hv(i) = ppr.DW_CalcEnthalpy(yc(i), Tj(i), P(i), PropertyPackages.State.Liquid) * ppr.AUX_MMM(yc(i)) / 1000
                                IObj2?.SetCurrent()
                                dHv(i) = ppr.DW_CalcEnthalpy(yc(i), Tj(i) - epsilon, P(i), PropertyPackages.State.Liquid) * ppr.AUX_MMM(yc(i)) / 1000
                                dHv2(i) = ppr.DW_CalcEnthalpy(yc(i), Tj(i) + epsilon, P(i), PropertyPackages.State.Liquid) * ppr.AUX_MMM(yc(i)) / 1000
                            Else
                                Hv(i) = ppr.DW_CalcEnthalpy(yc(i), Tj(i), P(i), PropertyPackages.State.Vapor) * ppr.AUX_MMM(yc(i)) / 1000
                                IObj2?.SetCurrent()
                                dHv(i) = ppr.DW_CalcEnthalpy(yc(i), Tj(i) - epsilon, P(i), PropertyPackages.State.Vapor) * ppr.AUX_MMM(yc(i)) / 1000
                                dHv2(i) = ppr.DW_CalcEnthalpy(yc(i), Tj(i) + epsilon, P(i), PropertyPackages.State.Vapor) * ppr.AUX_MMM(yc(i)) / 1000
                            End If
                            ppr.CurrentMaterialStream.Flowsheet.CheckStatus()
                        Next
                    Else
                        For i = 0 To ns
                            IObj2?.SetCurrent()
                            Hl(i) = pp.DW_CalcEnthalpy(xc(i), Tj(i), P(i), PropertyPackages.State.Liquid) * pp.AUX_MMM(xc(i)) / 1000
                            IObj2?.SetCurrent()
                            dHl(i) = pp.DW_CalcEnthalpy(xc(i), Tj(i) - epsilon, P(i), PropertyPackages.State.Liquid) * pp.AUX_MMM(xc(i)) / 1000
                            dHl2(i) = pp.DW_CalcEnthalpy(xc(i), Tj(i) + epsilon, P(i), PropertyPackages.State.Liquid) * pp.AUX_MMM(xc(i)) / 1000
                            IObj2?.SetCurrent()
                            If llextr Then
                                Hv(i) = pp.DW_CalcEnthalpy(yc(i), Tj(i), P(i), PropertyPackages.State.Liquid) * pp.AUX_MMM(yc(i)) / 1000
                                IObj2?.SetCurrent()
                                dHv(i) = pp.DW_CalcEnthalpy(yc(i), Tj(i) - epsilon, P(i), PropertyPackages.State.Liquid) * pp.AUX_MMM(yc(i)) / 1000
                                dHv2(i) = pp.DW_CalcEnthalpy(yc(i), Tj(i) + epsilon, P(i), PropertyPackages.State.Liquid) * pp.AUX_MMM(yc(i)) / 1000
                            Else
                                Hv(i) = pp.DW_CalcEnthalpy(yc(i), Tj(i), P(i), PropertyPackages.State.Vapor) * pp.AUX_MMM(yc(i)) / 1000
                                IObj2?.SetCurrent()
                                dHv(i) = pp.DW_CalcEnthalpy(yc(i), Tj(i) - epsilon, P(i), PropertyPackages.State.Vapor) * pp.AUX_MMM(yc(i)) / 1000
                                dHv2(i) = pp.DW_CalcEnthalpy(yc(i), Tj(i) + epsilon, P(i), PropertyPackages.State.Vapor) * pp.AUX_MMM(yc(i)) / 1000
                            End If
                            If pp.HasReactivePhase Then
                                Hr(i) = pp.DW_CalcEnthalpyOfReaction(xc(i).MultiplyConstY(Lj(i)), Tj(i), P(i))
                                Hr1(i) = pp.DW_CalcEnthalpyOfReaction(xc(i).MultiplyConstY(Lj(i)), Tj(i) - epsilon, P(i))
                                Hr2(i) = pp.DW_CalcEnthalpyOfReaction(xc(i).MultiplyConstY(Lj(i)), Tj(i) + epsilon, P(i))
                            End If
                            pp.CurrentMaterialStream.Flowsheet.CheckStatus()
                        Next
                    End If
                End If

                IObj2?.Paragraphs.Add(String.Format("HL: {0}", Hl.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("HV: {0}", Hv.ToMathArrayString))

                For i = 0 To ns
                    If i = 0 Then
                        H(i) = Hr(i) + Vj(i + 1) * Hv(i + 1) + Fj(i) * Hfj(i) - (Lj(i) + LSSj(i)) * Hl(i) - (Vj(i) + VSSj(i)) * Hv(i) - Q(i)
                    ElseIf i = ns Then
                        H(i) = Hr(i) + Lj(i - 1) * Hl(i - 1) + Fj(i) * Hfj(i) - (Lj(i) + LSSj(i)) * Hl(i) - (Vj(i) + VSSj(i)) * Hv(i) - Q(i)
                    Else
                        H(i) = Hr(i) + Lj(i - 1) * Hl(i - 1) + Vj(i + 1) * Hv(i + 1) + Fj(i) * Hfj(i) - (Lj(i) + LSSj(i)) * Hl(i) - (Vj(i) + VSSj(i)) * Hv(i) - Q(i)
                    End If
                    dHr(i) = (Hr2(i) - Hr1(i)) / (2 * epsilon)
                    dHldT(i) = (dHl2(i) - dHl(i)) / (2 * epsilon)
                    dHvdT(i) = (dHv2(i) - dHv(i)) / (2 * epsilon)
                Next

                IObj2?.Paragraphs.Add(("<mi>\frac{\partial h_L}{\partial T}</mi>: " & dHldT.ToMathArrayString))
                IObj2?.Paragraphs.Add(("<mi>\frac{\partial h_V}{\partial T}</mi>: " & dHvdT.ToMathArrayString))

                For i = 0 To ns
                    If i > 0 Then dHdTa(i) = Lj(i - 1) * dHldT(i - 1)
                    dHdTb(i) = -(Lj(i) + LSSj(i)) * dHldT(i) - (Vj(i) + VSSj(i)) * dHvdT(i)
                    If i < ns Then dHdTc(i) = Vj(i + 1) * dHvdT(i + 1)
                Next

                IObj2?.Paragraphs.Add(String.Format("H: {0}", H.ToMathArrayString))

                Dim ath(ns), bth(ns), cth(ns), dth(ns), xth(ns) As Double

                For i = 0 To ns
                    dth(i) = -H(i)
                    bth(i) = dHdTb(i)
                    If i < ns Then cth(i) = dHdTc(i)
                    If i > 0 Then ath(i) = dHdTa(i)
                Next

                'solve matrices
                'tomich

                IObj2?.Paragraphs.Add("Calling TDM Solver to solve for enthalpies/temperatures")

                IObj2?.SetCurrent()

                xth = Tomich.TDMASolve(ath, bth, cth, dth)

                Dim tmp As Object

                IObj2?.Paragraphs.Add(String.Format("Calculated Temperature perturbations: {0}", xth.ToMathArrayString))

                Dim deltat As Double() = xth

                Dim dft = MathOps.MathEx.Interpolation.Interpolation.GetDampingFactor(ic, 50, 0.25, 1.0)

                t_error = 0.0#
                comperror = 0.0#
                For i = 0 To ns
                    Tj_ant(i) = Tj(i)
                    ' Persistent (non-vanishing) temperature damping on the relaxed pass: the default schedule
                    ' opens to 1.0 and overshoots near the solution, so keep a fixed moderate factor.
                    Dim tstep As Double = If(RelaxTemperatureUpdates, 0.15, 0.7) * deltat(i)
                    ' Bound the per-iteration temperature change. The energy-balance Jacobian is only a local
                    ' linearization; from a poor initial estimate a full Newton step can send Tj below zero and
                    ' abort the solve. On the relaxed retry pass bound tightly (a few percent of Tj) so the
                    ' temperature<->K<->flow feedback of stiff systems cannot run away.
                    If Double.IsNaN(tstep) Or Double.IsInfinity(tstep) Then
                        Throw New Exception(String.Format(pp.CurrentMaterialStream.Flowsheet.GetTranslatedString("Converged to an invalid temperature at stage {0} (Tcalc = {1} K)."), i, Tj(i) + tstep))
                    End If
                    Dim maxtstep As Double = If(RelaxTemperatureUpdates, 0.02 * Tj(i), Tstep_max)
                    If Abs(tstep) > maxtstep Then tstep = Sign(tstep) * maxtstep
                    Tj(i) = Tj(i) + tstep
                    If RelaxTemperatureUpdates Then
                        If Tj(i) < Tmin_env Then Tj(i) = Tmin_env
                        If Tj(i) > Tmax_env Then Tj(i) = Tmax_env
                    Else
                        If Tj(i) < Tmin_hard Then Tj(i) = Tmin_hard
                        If Tj(i) > Tmax_hard Then Tj(i) = Tmax_hard
                    End If
                    If Tj(i) < 0.0 Or Double.IsNaN(Tj(i)) Or Double.IsInfinity(Tj(i)) Then
                        Throw New Exception(String.Format(pp.CurrentMaterialStream.Flowsheet.GetTranslatedString("Converged to an invalid temperature at stage {0} (Tcalc = {1} K)."), i, Tj(i)))
                    End If
                    If IdealK Then
                        IObj2?.SetCurrent()
                        If llextr Then
                            tmp = ppr.DW_CalcKvalue(xc(i), yc(i), Tj(i), P(i), "LL")
                        Else
                            If ppr.ShouldUseKvalueMethod3 Then
                                tmp = ppr.DW_CalcKvalue3(xc(i).MultiplyConstY(Lj(i)), yc(i).MultiplyConstY(Vj(i)), Tj(i), P(i))
                            ElseIf ppr.ShouldUseKvalueMethod2 Then
                                tmp = ppr.DW_CalcKvalue(xc(i).MultiplyConstY(Lj(i)).AddY(yc(i).MultiplyConstY(Vj(i))).MultiplyConstY(1 / (Lj(i) + Vj(i))), Tj(i), P(i))
                            Else
                                tmp = ppr.DW_CalcKvalue(xc(i), yc(i), Tj(i), P(i))
                            End If
                        End If
                    Else
                        IObj2?.SetCurrent()
                        If llextr Then
                            tmp = pp.DW_CalcKvalue(xc(i), yc(i), Tj(i), P(i), "LL")
                        Else
                            If pp.ShouldUseKvalueMethod3 Then
                                tmp = pp.DW_CalcKvalue3(xc(i).MultiplyConstY(Lj(i)), yc(i).MultiplyConstY(Vj(i)), Tj(i), P(i))
                            ElseIf pp.ShouldUseKvalueMethod2 Then
                                tmp = pp.DW_CalcKvalue(xc(i).MultiplyConstY(Lj(i)).AddY(yc(i).MultiplyConstY(Vj(i))).MultiplyConstY(1 / (Lj(i) + Vj(i))), Tj(i), P(i))
                            Else
                                tmp = pp.DW_CalcKvalue(xc(i), yc(i), Tj(i), P(i))
                            End If
                        End If
                    End If
                    sumy(i) = 0
                    For j = 0 To nc - 1
                        K(i)(j) = tmp(j)
                        If Double.IsNaN(K(i)(j)) Or Double.IsInfinity(K(i)(j)) Then
                            If llextr Then
                                IObj2?.SetCurrent()
                                K(i)(j) = pp.AUX_PVAPi(j, Tj(i)) / P(i)
                            Else
                                IObj2?.SetCurrent()
                                K(i)(j) = pp.AUX_PVAPi(j, Tj(i)) / P(i)
                            End If
                        End If
                        yc_ant(i)(j) = yc(i)(j)
                        yc(i)(j) = K(i)(j) * xc(i)(j)
                        If RelaxCompositionUpdates Then
                            ' Persistently damp the vapor-composition update too, so the K<->y feedback that
                            ' drives the composition error cannot sustain a limit cycle on stiff systems.
                            yc(i)(j) = 0.15 * yc(i)(j) + 0.85 * yc_ant(i)(j)
                        End If
                        sumy(i) += yc(i)(j)
                        comperror += Abs(yc(i)(j) - yc_ant(i)(j)) ^ 2
                    Next
                    t_error += Abs(Tj(i) - Tj_ant(i)) ^ 2
                    pp.CurrentMaterialStream.Flowsheet.CheckStatus()
                Next

                t_error_hist.Add(t_error)
                comp_error_hist.Add(comperror)

                If TraceProfiles Then
                    Console.WriteLine(String.Format("[col] it {0,4}  tErr {1,12:G5}  cErr {2,12:G5}", ic, t_error, comperror))
                    Console.WriteLine("      T " + String.Join(" ", Tj.Select(Function(tv) tv.ToString("F1"))))
                    Console.WriteLine("      L " + String.Join(" ", Lj.Select(Function(lv) lv.ToString("G4"))))
                    Console.WriteLine("      V " + String.Join(" ", Vj.Select(Function(vv) vv.ToString("G4"))))
                End If

                IObj2?.Paragraphs.Add(String.Format("Updated Temperatures: {0}", Tj.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("Updated K-values: {0}", K.ToMathArrayString))

                For i = 0 To ns
                    For j = 0 To nc - 1
                        yc(i)(j) = yc(i)(j) / sumy(i)
                    Next
                Next

                IObj2?.Paragraphs.Add(String.Format("Updated y: {0}", yc.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("Temperature error: {0}", t_error))

                IObj2?.Paragraphs.Add(String.Format("Composition error: {0}", comperror))

                IObj2?.Paragraphs.Add(String.Format("Combined Temperature/Composition error: {0}", t_error + comperror))

                ic = ic + 1

                Dim exc As Exception = Nothing

                If Not IdealH And Not IdealK Then
                    If ic >= maxits Then
                        exc = New Exception(pp.CurrentMaterialStream.Flowsheet.GetTranslatedString("DCMaxIterationsReached"))
                    End If
                End If

                If Double.IsNaN(t_error) Then
                    exc = New Exception(pp.CurrentMaterialStream.Flowsheet.GetTranslatedString("DCGeneralError"))
                End If
                If Double.IsNaN(comperror) Then
                    exc = New Exception(pp.CurrentMaterialStream.Flowsheet.GetTranslatedString("DCGeneralError"))
                End If

                If Not exc Is Nothing Then


                    reporter?.AppendLine("========================================================")
                    reporter?.AppendLine("Error Function Progression")
                    reporter?.AppendLine("========================================================")
                    reporter?.AppendLine()

                    reporter?.AppendLine(String.Format("{0,-16}{1,26}{2,26}", "Iteration", "Temperature Error", "Composition Error"))
                    For i = 0 To t_error_hist.Count - 1
                        reporter?.AppendLine(String.Format("{0,-16}{1,26:G6}{2,26:G6}", i + 1, t_error_hist(i), comp_error_hist(i)))
                    Next

                    reporter?.AppendLine("========================================================")
                    reporter?.AppendLine("Convergence Error!")
                    reporter?.AppendLine("========================================================")
                    reporter?.AppendLine()

                    reporter?.AppendLine(pp.CurrentMaterialStream.Flowsheet.GetTranslatedString("DCGeneralError"))

                    If rc.CreateSolverConvergengeReport Then rc.ColumnSolverConvergenceReport = reporter.ToString()

                    Throw exc

                End If

                pp.CurrentMaterialStream.Flowsheet.CheckStatus()

                IObj2?.Close()

                reporter?.AppendLine("========================================================")
                reporter?.AppendLine(String.Format("Updated variables after iteration {0}", ic))
                reporter?.AppendLine("========================================================")
                reporter?.AppendLine()

                reporter?.AppendLine("Stage Conditions & Flows")
                reporter?.AppendLine(String.Format("{0,-16}{1,16}{2,16}{3,16}{4,16}{5,16}",
                                                   "Stage", "P (Pa)", "T (K)", "L1 (mol/s)",
                                                   "V/L2 (mol/s)", "LSS (mol/s)"))
                For i = 0 To ns
                    reporter?.AppendLine(String.Format("{0,-16}{1,16:G6}{2,16:G6}{3,16:G6}{4,16:G6}{5,16:G6}",
                                                   i + 1, P(i), Tj(i), Lj(i), Vj(i), LSSj(i)))
                Next

                reporter?.AppendLine()
                reporter?.AppendLine("Stage Molar Fractions - Liquid")
                reporter?.Append("Stage".PadRight(20))
                For j = 0 To nc - 1
                    reporter?.Append(names(j).PadLeft(20))
                Next
                reporter?.Append(vbCrLf)
                For i = 0 To ns
                    reporter?.Append((i + 1).ToString().PadRight(20))
                    For j = 0 To nc - 1
                        reporter?.Append(xc(i)(j).ToString("G6").PadLeft(20))
                    Next
                    reporter?.Append(vbCrLf)
                Next

                reporter?.AppendLine()
                reporter?.AppendLine("Stage Molar Fractions - Vapor/Liquid2")
                reporter?.Append("Stage".PadRight(20))
                For j = 0 To nc - 1
                    reporter?.Append(names(j).PadLeft(20))
                Next
                reporter?.Append(vbCrLf)
                For i = 0 To ns
                    reporter?.Append((i + 1).ToString().PadRight(20))
                    For j = 0 To nc - 1
                        reporter?.Append(yc(i)(j).ToString("G6").PadLeft(20))
                    Next
                    reporter?.Append(vbCrLf)
                Next

                reporter?.AppendLine()
                reporter?.AppendLine()
                reporter?.AppendLine(String.Format("Temperature Error: {0} [tol: {1}]", t_error, tol(1)))
                reporter?.AppendLine(String.Format("Composition Error: {0} [tol: {1}]", comperror, tol(1)))
                reporter?.AppendLine()
                reporter?.AppendLine()

            Loop Until t_error <= tol(1) And comperror <= tol(1) And ic > 10

            IObj?.Paragraphs.Add("The algorithm converged in " & ic & " iterations.")

            IObj?.Paragraphs.Add("<h2>Results</h2>")

            IObj?.Paragraphs.Add(String.Format("Final converged values for T: {0}", Tj.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for V: {0}", Vj.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for L: {0}", Lj.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for VSS: {0}", VSSj.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for LSS: {0}", LSSj.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for y: {0}", yc.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for x: {0}", xc.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for K: {0}", K.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final converged values for Q: {0}", Q.ToMathArrayString))

            IObj?.Close()

            For Each Ki In K
                If pp.AUX_CheckTrivial(Ki) Then
                    IObj?.Paragraphs.Add("Invalid result - converged to the trivial solution.")
                    Throw New Exception("Invalid result - converged to the trivial solution.")
                End If
            Next


            reporter?.AppendLine("========================================================")
            reporter?.AppendLine("Error Function Progression")
            reporter?.AppendLine("========================================================")
            reporter?.AppendLine()

            reporter?.AppendLine(String.Format("{0,-16}{1,26}{2,26}", "Iteration", "Temperature Error", "Composition Error"))
            For i = 0 To t_error_hist.Count - 1
                reporter?.AppendLine(String.Format("{0,-16}{1,26:G6}{2,26:G6}", i + 1, t_error_hist(i), comp_error_hist(i)))
            Next

            reporter?.AppendLine()
            reporter?.AppendLine("Last Updated on " + Date.Now.ToString())

            If rc.CreateSolverConvergengeReport Then rc.ColumnSolverConvergenceReport = reporter.ToString()

            ' finished, return arrays

            Return New Object() {Tj, Vj, Lj, VSSj, LSSj, yc, xc, K, Q, ic, t_error}

        End Function

        ''' <summary>
        ''' Solves the column from a <see cref="ColumnSolverInputData"/> object using the Sum-Rates method.
        ''' </summary>
        ''' <param name="input">Structured input data containing initial estimates and column configuration.</param>
        ''' <returns>A <see cref="ColumnSolverOutputData"/> containing converged results.</returns>
        Public Overrides Function SolveColumn(input As ColumnSolverInputData) As ColumnSolverOutputData

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Dim nc = input.NumberOfCompounds
            Dim ns = input.NumberOfStages
            Dim maxits = input.MaximumIterations
            Dim tol = input.Tolerances.ToArray()
            Dim F = input.FeedFlows.ToArray()
            Dim V = input.VaporFlows.ToArray()
            Dim L = input.LiquidFlows.ToArray()
            Dim VSS = input.VaporSideDraws.ToArray()
            Dim LSS = input.LiquidSideDraws.ToArray()
            Dim Kval = input.Kvalues.ToArray()
            Dim Q = input.StageHeats.ToArray()
            Dim x = input.LiquidCompositions.ToArray()
            Dim y = input.VaporCompositions.ToArray()
            Dim z = input.OverallCompositions.ToArray()
            Dim fc = input.FeedCompositions.ToArray()
            Dim HF = input.FeedEnthalpies.ToArray()
            Dim T = input.StageTemperatures.ToArray()
            Dim P = input.StagePressures.ToArray()
            Dim eff = input.StageEfficiencies.ToArray()

            Dim col = input.ColumnObject

            Dim llextractor As Boolean = False
            Dim myabs As AbsorptionColumn = TryCast(col, AbsorptionColumn)
            If myabs IsNot Nothing Then
                If CType(col, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Absorber Then
                    llextractor = False
                Else
                    llextractor = True
                End If
            End If

            Dim result As Object() = Solve(col, nc, ns, maxits, tol, F, V, Q, L, VSS, LSS, Kval, x, y, z, fc, HF, T, P,
                               input.CondenserType, eff, col.PropertyPackage, col.Specs, False, False, llextractor)

            Dim output As New ColumnSolverOutputData

            'Return New Object() {Tj, Vj, Lj, VSSj, LSSj, yc, xc, K, Q, ic, t_error}

            With output
                .FinalError = result(10)
                .IterationsTaken = result(9)
                .StageTemperatures = DirectCast(result(0), Double()).ToList()
                .VaporFlows = DirectCast(result(1), Double()).ToList()
                .LiquidFlows = DirectCast(result(2), Double()).ToList()
                .VaporSideDraws = DirectCast(result(3), Double()).ToList()
                .LiquidSideDraws = DirectCast(result(4), Double()).ToList()
                .VaporCompositions = DirectCast(result(5), Double()()).ToList()
                .LiquidCompositions = DirectCast(result(6), Double()()).ToList()
                .Kvalues = DirectCast(result(7), Double()()).ToList()
                .StageHeats = DirectCast(result(8), Double()).ToList()
            End With

            Return output

        End Function

    End Class

End Namespace
