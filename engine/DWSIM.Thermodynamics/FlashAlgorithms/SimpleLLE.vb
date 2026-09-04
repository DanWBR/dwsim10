'    Simplified LLE Flash Algorithm
'    Copyright 2013-2026 Daniel Wagner O. de Medeiros
'    Copyright 2021 Gregor Reichert
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

Imports System.Math
Imports DWSIM.MathOps.MathEx
Imports DWSIM.SharedClasses

Namespace PropertyPackages.Auxiliary.FlashAlgorithms

    <System.Serializable()> Public Class SimpleLLE

        Inherits FlashAlgorithm

        Dim etol As Double = 0.000001
        Dim itol As Double = 0.000001
        Dim maxit_i As Integer = 100
        Dim maxit_e As Integer = 100
        Dim Hv0, Hvid, Hlid, Hf, Hv, Hl As Double
        Dim Sv0, Svid, Slid, Sf, Sv, Sl As Double

        Public Property InitialEstimatesForPhase1 As Double()
        Public Property InitialEstimateForPhase1Amount As Double?
        Public Property UseInitialEstimatesForPhase1 As Boolean = False

        Public Property InitialEstimatesForPhase2 As Double()
        Public Property UseInitialEstimatesForPhase2 As Boolean = False

        Sub New()
            MyBase.New()
            Order = 6
        End Sub

        Public Overrides ReadOnly Property InternalUseOnly As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property AlgoType As Interfaces.Enums.FlashMethod
            Get
                Return Interfaces.Enums.FlashMethod.Simple_LLE
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                If GlobalSettings.Settings.CurrentCulture = "pt-BR" Then
                    Return "Algoritmo Flash para c�lculo de equil�brio entre duas fases l�quidas"
                Else
                    Return "Flash Algorithm for simple Liquid-Liquid equilibrium calculations"
                End If
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "Simple LLE"
            End Get
        End Property

#Region "   Spinodal estimates and analytical Newton step"

        ' Refines a bracketed root of D2 (the spinodal) by bisection.
        Private Function BisectD2(ByVal d2 As Func(Of Double, Double, Double), ByVal T As Double,
                                  ByVal a As Double, ByVal b As Double) As Double
            Dim fa As Double = d2(T, a)
            For it As Integer = 1 To 80
                Dim m As Double = 0.5 * (a + b)
                Dim fm As Double = d2(T, m)
                If Double.IsNaN(fm) Then Exit For
                If fa * fm <= 0.0 Then
                    b = m
                Else
                    a = m : fa = fm
                End If
                If b - a < 0.0001 Then Exit For
            Next
            Return 0.5 * (a + b)
        End Function

        ''' <summary>
        ''' Outcome of the spinodal analysis of a binary described by an activity model.
        ''' </summary>
        Private Enum SpinodalState
            ''' <summary>Not a binary, or not an activity model, or D2 could not be evaluated.</summary>
            NotApplicable
            ''' <summary>D2 > 0 over the whole composition range, so g_mix is strictly convex.</summary>
            Convex
            ''' <summary>D2 < 0 somewhere: an unstable window exists and initial estimates were produced.</summary>
            WindowFound
        End Enum

        ''' <summary>
        ''' Minimises D2 = d2(g_mix/RT)/dx1^2 over x1 in (0,1). The ideal term 1/(x1 x2) drives D2 to +inf at
        ''' both ends, so D2 is a well and its interior minimum decides everything: if it is positive, g_mix
        ''' is strictly convex; if negative, exactly one spinodal root lies on each side of the minimiser.
        ''' A coarse grid brackets the well, then golden section refines it. This is a bracketed minimisation
        ''' rather than a scan for sign changes because near the consolute point the window narrows to
        ''' nothing, and any grid coarse enough to be affordable steps straight over it.
        ''' </summary>
        Private Function MinimiseD2(ByVal d2 As Func(Of Double, Double, Double), ByVal T As Double,
                                    ByRef xmin As Double) As Double

            Const m As Integer = 15
            Dim best As Double = Double.MaxValue
            Dim kbest As Integer = -1
            For k As Integer = 1 To m
                Dim v As Double = d2(T, k / (m + 1.0))
                If Double.IsNaN(v) Then Return Double.NaN
                If v < best Then best = v : kbest = k
            Next
            If kbest < 0 Then Return Double.NaN

            Dim a As Double = Math.Max((kbest - 1) / (m + 1.0), 0.000001)
            Dim b As Double = Math.Min((kbest + 1) / (m + 1.0), 0.999999)

            Dim gr As Double = (Math.Sqrt(5.0) - 1.0) / 2.0
            Dim c As Double = b - gr * (b - a), dd As Double = a + gr * (b - a)
            Dim fc As Double = d2(T, c), fd As Double = d2(T, dd)
            If Double.IsNaN(fc) OrElse Double.IsNaN(fd) Then Return Double.NaN
            For it As Integer = 1 To 60
                If b - a < 0.0001 Then Exit For
                If fc < fd Then
                    b = dd : dd = c : fd = fc
                    c = b - gr * (b - a)
                    fc = d2(T, c)
                    If Double.IsNaN(fc) Then Return Double.NaN
                Else
                    a = c : c = dd : fc = fd
                    dd = a + gr * (b - a)
                    fd = d2(T, dd)
                    If Double.IsNaN(fd) Then Return Double.NaN
                End If
            Next

            xmin = 0.5 * (a + b)
            Return d2(T, xmin)

        End Function

        ''' <summary>
        ''' Spinodal analysis of a BINARY described by an activity model, used both to decide whether a split
        ''' is possible at all and, when it is, to seed one.
        ''' A negative minimum of D2 means an unstable window exists. The binodal always lies OUTSIDE the two
        ''' spinodal roots, so seeding the phases beyond those roots is physically consistent and, unlike a
        ''' fixed perturbation, structurally cannot collapse onto the trivial solution x1 = x2 = z. This
        ''' seeds a split whether the feed sits inside the window or is merely metastable (outside the
        ''' spinodal but inside the binodal), and the metastable case is exactly the one the stability test
        ''' misses near the consolute point.
        ''' A positive minimum means g_mix is strictly convex over the whole range: no common tangent can
        ''' exist, so no split is possible and the mixture is provably a single phase, with nothing to solve.
        ''' Returns {L1, Vx1, Vx2} through est when the state is WindowFound.
        ''' </summary>
        Private Function AnalyseSpinodal(ByVal T As Double, ByVal Vz As Double(),
                                         ByVal PP As PropertyPackages.PropertyPackage,
                                         ByRef est As Object) As SpinodalState

            est = Nothing
            If Vz.Length <> 2 Then Return SpinodalState.NotApplicable
            Dim apk = TryCast(PP, ActivityCoefficientPropertyPackage)
            If apk Is Nothing Then Return SpinodalState.NotApplicable

            ' Resolve the model arguments once for the whole sweep: they do not vary with T or composition,
            ' and rebuilding them per point costs more than the derivative itself for group-contribution
            ' models, which have to re-read each compound's groups and allocate a dictionary per compound.
            Dim d2 = apk.GetGibbsMixingD2Evaluator()

            ' A single evaluation at the feed often settles it: D2(z) < 0 puts the feed between the two
            ' spinodal roots by definition, so the window is known to exist and z is known to lie in it,
            ' and z serves as the interior point the roots are bracketed from. Only when D2(z) >= 0 is the
            ' minimisation needed to tell a window elsewhere apart from strict convexity.
            Dim xmin As Double = Vz(0)
            If d2(T, xmin) >= 0.0 Then
                Dim d2min As Double = MinimiseD2(d2, T, xmin)
                If Double.IsNaN(d2min) Then Return SpinodalState.NotApplicable
                If d2min > 0.0 Then Return SpinodalState.Convex
            End If

            ' D2 is negative at the interior point and positive towards both ends, so each side brackets a root.
            Dim xs1 As Double = BisectD2(d2, T, 0.000001, xmin)
            Dim xs2 As Double = BisectD2(d2, T, xmin, 0.999999)
            If xs2 <= xs1 Then Return SpinodalState.NotApplicable

            ' Seed strictly outside each spinodal root, offset by a fraction of the window width. Mean-field
            ' scaling puts the binodal at about sqrt(3) times the spinodal half-width from the critical
            ' composition, i.e. roughly 0.37 of the window beyond each root, so this lands the seed near the
            ' answer at any window width. It matters most near the consolute point, where the window is
            ' narrow: a seed at a fixed fraction of the axis instead starts the phases far outside a binodal
            ' that is only hundredths wide, and the iteration collapses onto the trivial solution before it
            ' finds it. The second form keeps the seed inside (0,1) when the window is wide.
            Dim wid As Double = xs2 - xs1
            Dim x1a As Double = Math.Max(xs1 - 0.4 * wid, 0.5 * xs1)
            Dim x2a As Double = Math.Min(xs2 + 0.4 * wid, xs2 + 0.5 * (1.0 - xs2))
            ' material balance z = L1 x1 + (1-L1) x2
            Dim L1 As Double = (Vz(0) - x2a) / (x1a - x2a)
            If L1 <= 0.001 OrElse L1 >= 0.999 Then Return SpinodalState.NotApplicable

            est = New Object() {L1, New Double() {x1a, 1.0 - x1a}, New Double() {x2a, 1.0 - x2a}}
            Return SpinodalState.WindowFound

        End Function

        ' Isoactivity residual norm sum|F_i|, F_i = ln(x1_i phi1_i) - ln(x2_i phi2_i). NaN if infeasible.
        Private Function LLEResidualNorm(ByVal Vz As Double(), ByVal Vn1 As Double(), ByVal T As Double,
                                         ByVal P As Double, ByVal PP As PropertyPackages.PropertyPackage) As Double
            Dim n As Integer = Vz.Length - 1
            Dim Vn2(n), Vx1(n), Vx2(n) As Double
            Dim L1 As Double = 0.0, L2 As Double = 0.0
            For i As Integer = 0 To n
                Vn2(i) = Vz(i) - Vn1(i)
                L1 += Vn1(i) : L2 += Vn2(i)
            Next
            If L1 <= 0.0 OrElse L2 <= 0.0 Then Return Double.NaN
            For i As Integer = 0 To n
                Vx1(i) = Vn1(i) / L1 : Vx2(i) = Vn2(i) / L2
            Next
            ' Log fugacity coefficients, so a high segment-number polymer (phi underflows to zero) keeps a
            ' finite residual: F_i = ln(x1_i) + lnphi1_i - ln(x2_i) - lnphi2_i.
            Dim lnf1 = PP.DW_CalcLnFugCoeff(Vx1, T, P, State.Liquid)
            Dim lnf2 = PP.DW_CalcLnFugCoeff(Vx2, T, P, State.Liquid)
            Dim s As Double = 0.0
            For i As Integer = 0 To n
                If Vz(i) <= 0.0 Then Continue For
                If Vx1(i) <= 0.0 OrElse Vx2(i) <= 0.0 Then Return Double.NaN
                s += Math.Abs((Math.Log(Vx1(i)) + lnf1(i)) - (Math.Log(Vx2(i)) + lnf2(i)))
            Next
            Return s
        End Function

        ''' <summary>
        ''' Composition derivative D(i,j) = d(ln phi_i)/dn_j at total moles = 1, by finite difference: bump
        ''' n_j (= x_j at unit total moles), renormalise, and difference the log fugacity coefficients. Used
        ''' for the Newton step when the property package does not supply analytical derivatives.
        ''' </summary>
        Private Function DLnFugCoeffdnNumerical(ByVal Vx As Double(), ByVal T As Double, ByVal P As Double,
                                                ByVal PP As PropertyPackages.PropertyPackage) As Double(,)
            Dim n As Integer = Vx.Length - 1
            Dim D(n, n) As Double
            Dim delta As Double = 0.000001
            For j As Integer = 0 To n
                If Vx(j) <= 0.0 Then Continue For
                ' Central difference on the mole numbers (n_j = x_j at unit total moles), renormalising the
                ' bumped composition each side.
                Dim npp(n), npm(n) As Double
                For k As Integer = 0 To n
                    npp(k) = Vx(k) : npm(k) = Vx(k)
                Next
                npp(j) += delta : npm(j) -= delta
                Dim totp As Double = 1.0 + delta, totm As Double = 1.0 - delta
                For k As Integer = 0 To n
                    npp(k) /= totp : npm(k) /= totm
                Next
                Dim lnfp = PP.DW_CalcLnFugCoeff(npp, T, P, State.Liquid)
                Dim lnfm = PP.DW_CalcLnFugCoeff(npm, T, P, State.Liquid)
                For i As Integer = 0 To n
                    D(i, j) = (lnfp(i) - lnfm(i)) / (2.0 * delta)
                Next
            Next
            Return D
        End Function

        ''' <summary>
        ''' One Newton step on the isoactivity condition, with the phase-1 mole numbers as unknowns (phase 2
        ''' follows from the material balance n2 = z - n1). The residual is
        ''' F_i = ln(x1_i phi1_i) - ln(x2_i phi2_i), equivalent to the activity form x1 gamma1 = x2 gamma2
        ''' because the Psat/P factor is common to both liquid phases. Differentiating, and using that
        ''' ln(phi) is intensive so d(ln phi_i)/dn_j at total moles L equals D(i,j)/L with D the
        ''' total-moles = 1 derivative returned by the property package,
        '''   J_ij = delta_ij (1/n1_i + 1/n2_i) - (1/L1 + 1/L2) + D1(i,j)/L1 + D2(i,j)/L2.
        ''' Returns the step, or Nothing if it is not computable, and hands back the residual norm at the
        ''' current point in Fnorm (formed here anyway, so the caller need not pay to recompute it). This is
        ''' worthwhile only when the package supplies analytical composition derivatives, which the caller
        ''' checks.
        ''' </summary>
        Private Function NewtonStepLLE(ByVal Vz As Double(), ByVal Vn1 As Double(), ByVal T As Double,
                                       ByVal P As Double, ByVal PP As PropertyPackages.PropertyPackage,
                                       ByRef Fnorm As Double) As Double()

            Dim n As Integer = Vz.Length - 1
            Dim Vn2(n), Vx1(n), Vx2(n) As Double
            Dim L1 As Double = 0.0, L2 As Double = 0.0
            For i As Integer = 0 To n
                Vn2(i) = Vz(i) - Vn1(i)
                L1 += Vn1(i) : L2 += Vn2(i)
            Next
            If L1 <= 0.0 OrElse L2 <= 0.0 Then Return Nothing
            For i As Integer = 0 To n
                Vx1(i) = Vn1(i) / L1 : Vx2(i) = Vn2(i) / L2
            Next

            ' Residual from log fugacity coefficients (finite for a polymer whose phi underflows):
            ' F_i = ln(x1_i) + lnphi1_i - ln(x2_i) - lnphi2_i.
            Dim lnf1 = PP.DW_CalcLnFugCoeff(Vx1, T, P, State.Liquid)
            Dim lnf2 = PP.DW_CalcLnFugCoeff(Vx2, T, P, State.Liquid)

            ' Composition derivatives: analytical when the package supplies them, otherwise finite-difference
            ' (PC-SAFT does not implement analytical d(lnphi)/dn).
            Dim D1 As Double(,), D2m As Double(,)
            If PP.ImplementsAnalyticalDerivatives Then
                D1 = PP.DW_CalcdLnFugCoeffdn(Vx1, T, P, State.Liquid)
                D2m = PP.DW_CalcdLnFugCoeffdn(Vx2, T, P, State.Liquid)
            Else
                D1 = DLnFugCoeffdnNumerical(Vx1, T, P, PP)
                D2m = DLnFugCoeffdnNumerical(Vx2, T, P, PP)
            End If

            Dim F(n) As Double
            Fnorm = 0.0
            For i As Integer = 0 To n
                If Vz(i) <= 0.0 Then
                    F(i) = 0.0
                Else
                    If Vx1(i) <= 0.0 OrElse Vx2(i) <= 0.0 Then Return Nothing
                    F(i) = (Math.Log(Vx1(i)) + lnf1(i)) - (Math.Log(Vx2(i)) + lnf2(i))
                    Fnorm += Math.Abs(F(i))
                End If
                If Double.IsNaN(F(i)) OrElse Double.IsInfinity(F(i)) Then Return Nothing
            Next

            ' NOTE: VB is case-insensitive, so the matrix must not be called J while j indexes the loop.
            Dim Jac As Mapack.Matrix = New Mapack.Matrix(n + 1, n + 1)
            For i As Integer = 0 To n
                For j As Integer = 0 To n
                    If Vz(i) <= 0.0 Then
                        ' inert row for absent components: keep n1_i pinned
                        Jac(i, j) = If(i = j, 1.0, 0.0)
                    Else
                        Dim v As Double = -(1.0 / L1 + 1.0 / L2) + D1(i, j) / L1 + D2m(i, j) / L2
                        If i = j Then v += 1.0 / Vn1(i) + 1.0 / Vn2(i)
                        Jac(i, j) = v
                    End If
                Next
            Next

            Dim rhs As Mapack.Matrix = New Mapack.Matrix(n + 1, 1)
            For i As Integer = 0 To n : rhs(i, 0) = -F(i) : Next

            Dim dn(n) As Double
            Try
                Dim lu As New Mapack.LuDecomposition(Jac)
                Dim s = lu.Solve(rhs)
                For i As Integer = 0 To n
                    dn(i) = s(i, 0)
                    If Double.IsNaN(dn(i)) OrElse Double.IsInfinity(dn(i)) Then Return Nothing
                Next
            Catch ex As Exception
                Return Nothing
            End Try

            Return dn

        End Function

#End Region

        ''' <summary>
        ''' Number of successive-substitution iterations to run before handing over to the analytical
        ''' Newton step. Substitution is cheaper per iteration, so it is left to do the easy work; Newton
        ''' is there for the cases it cannot finish. An oscillation triggers the handover immediately,
        ''' regardless of this count.
        ''' </summary>
        Public Property NewtonFallbackIterations As Integer = 15

        ''' <summary>
        ''' Largest mole-fraction difference, in any single component, at which the two liquid phases are
        ''' taken to be the same phase and merged. The trivial solution x1 = x2 = z satisfies the isoactivity
        ''' condition exactly, so a converged flash on a miscible feed lands on it and must be recognised
        ''' here rather than reported as a split between two identical phases.
        ''' This is deliberately a per-component maximum and not a sum over components: a sum grows with the
        ''' number of components, which would make the same physical closeness pass or fail depending on how
        ''' many compounds the mixture happens to contain.
        ''' A genuine split narrower than this only exists within a whisker of the consolute point, where the
        ''' two phases really are becoming identical, so merging it is the physically right answer anyway.
        ''' </summary>
        Public Property PhaseIdentityTolerance As Double = 0.001

        Public Overrides Function Flash_PT(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PT", Name & " (PT Flash)", "Pressure-Temperature Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Components: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Use estimates for Liquid Phase 1: {0}", UseInitialEstimatesForPhase1))
            If UseInitialEstimatesForPhase1 Then IObj?.Paragraphs.Add(String.Format("Initial estimates for Liquid Phase 1: {0}", InitialEstimatesForPhase1.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Use estimates for Liquid Phase 2: {0}", UseInitialEstimatesForPhase2))
            If UseInitialEstimatesForPhase2 Then IObj?.Paragraphs.Add(String.Format("Initial estimates for Liquid Phase 2: {0}", InitialEstimatesForPhase2.ToMathArrayString))

            Dim i, j, n, ecount As Integer
            Dim result As Object

            n = Vz.Length - 1

            Dim Vx1(n), Vx2(n), Vy(n), Vn1(n), Vn2(n), Ki(n), fi1(n), fi2(n), gamma1(n), gamma2(n), Vp(n) As Double
            Dim Vx1_ant(n), Vx2_ant(n), Vn1_ant(n), Vn2_ant(n), L1_ant, L2_ant As Double
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L1, L2, V, S As Double
            Dim e1, e2 As Double
            Dim dampFactor As Double = 1.0
            Dim dL1_prev As Double = 0.0
            ' True when the initial estimate is a genuine two-phase seed (user estimates, the TPD test, or
            ' the spinodal roots) rather than the blind perturbation used to probe an apparently stable feed.
            Dim seeded As Boolean = False
            ' True when the spinodal analysis has PROVED no split can exist, so the loop is skipped entirely.
            Dim provablySinglePhase As Boolean = False
            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            If UseInitialEstimatesForPhase1 And UseInitialEstimatesForPhase2 Then
                seeded = True
                If InitialEstimateForPhase1Amount.HasValue Then
                    L1 = InitialEstimateForPhase1Amount.Value
                    L2 = 1 - L1
                Else
                    ' Lever rule, averaged over the compounds that actually tell the two phases apart:
                    ' z_i = L1 x1_i + (1 - L1) x2_i for each of them. A compound estimated the same in both
                    ' phases says nothing about L1 and is skipped rather than averaged in as half.
                    ' The sign of the difference must not be filtered on. In a binary exactly one compound
                    ' has a positive one - if a compound is richer in phase 1 the other is richer in phase 2
                    ' - so requiring diff > 0 collected a single contribution and then divided it by the
                    ' number of compounds, halving L1. The seed that came out did not even satisfy the
                    ' material balance: with the true binodal of UNIFAC Methanol/Cyclohexane at 430.5 K fed
                    ' in as the estimate, the lever rule gives L1 = 0.731 and this gave 0.366, whose phases
                    ' add up to a feed of (0.566, 0.434) rather than the (0.5, 0.5) asked for. From there the
                    ' iteration walked off to the trivial solution and reported two identical phases.
                    L1 = 0
                    L2 = 0
                    j = 0
                    For i = 0 To n
                        If Vz(i) > 0 Then
                            Dim diff As Double = (InitialEstimatesForPhase1(i) - InitialEstimatesForPhase2(i))
                            If Abs(diff) > 0.000001 Then
                                L1 += (Vz(i) - InitialEstimatesForPhase2(i)) / diff
                                j += 1
                            End If
                        End If
                    Next
                    If j > 0 Then L1 = L1 / j Else L1 = 0.5
                    If L1 > 0.99 Then L1 = 0.99
                    If L1 < 0.01 Then L1 = 0.01
                    L2 = 1 - L1
                End If
            Else
                ' Spinodal analysis of the binary decides, before any iteration, whether a split is even
                ' possible - and if it is, seeds it. It settles two cases the stability test alone gets
                ' wrong near the consolute point: a metastable feed (outside the spinodal but inside the
                ' binodal), where the test reports no instability although a split exists; and a mixture
                ' above its consolute temperature, where no split exists and the search for one used to
                ' oscillate until it ran out of iterations.
                Dim sp As Object = Nothing
                Select Case AnalyseSpinodal(T, Vz, PP, sp)

                    Case SpinodalState.Convex
                        ' D2 > 0 for every composition, so g_mix is strictly convex, no common tangent can
                        ' exist and the mixture cannot split. This is a proof, not a heuristic, so the
                        ' answer is settled here and there is nothing to iterate.
                        IObj?.Paragraphs.Add("D2 = d2(g_mix/RT)/dx1^2 is positive over the whole composition range, so g_mix is strictly convex and no liquid-liquid split is possible. Reporting a single phase without iterating.")
                        provablySinglePhase = True

                    Case SpinodalState.WindowFound
                        IObj?.Paragraphs.Add("An unstable window exists (D2 < 0), so a split is possible: skipping the stability test and seeding the phases outside the two spinodal roots.")
                        L1 = CDbl(DirectCast(sp, Object())(0))
                        L2 = 1.0 - L1
                        Dim sx1 = CType(DirectCast(sp, Object())(1), Double())
                        Dim sx2 = CType(DirectCast(sp, Object())(2), Double())
                        For i = 0 To n
                            Vn1(i) = L1 * sx1(i)
                            Vn2(i) = L2 * sx2(i)
                        Next
                        seeded = True

                End Select

                If Not seeded AndAlso Not provablySinglePhase Then
                    ' Use the Michelsen tangent-plane-distance (TPD) stability test to generate
                    ' rigorous initial estimates.  GetPhaseSplitEstimates runs StabTest2 and, if
                    ' a second liquid phase is detected, returns compositions that straddle the
                    ' miscibility gap - far superior to any fixed heuristic split.
                    IObj?.Paragraphs.Add("Running Michelsen stability test to generate initial phase-split estimates.")
                    Dim stabEst As Object() = GetPhaseSplitEstimates(T, P, 1.0, Vz, PP)
                    Dim L1_stab As Double = CDbl(stabEst(0))
                    Dim Vx1_stab As Double() = CType(stabEst(1), Double())
                    Dim L2_stab As Double = CDbl(stabEst(2))
                    Dim Vx2_stab As Double() = CType(stabEst(3), Double())

                    If L2_stab > 0.0001 Then
                        ' Stability test found a second liquid phase; seed the iteration directly
                        ' from the TPD-optimised trial compositions.
                        L1 = L1_stab
                        L2 = L2_stab
                        For i = 0 To n
                            Vn1(i) = L1 * Vx1_stab(i)
                            Vn2(i) = L2 * Vx2_stab(i)
                        Next
                        IObj?.Paragraphs.Add(String.Format("Stability test detected instability. L1={0:F4}, L2={1:F4}", L1, L2))
                        IObj?.Paragraphs.Add(String.Format("TPD phase 1 estimate: {0}", Vx1_stab.ToMathArrayString))
                        IObj?.Paragraphs.Add(String.Format("TPD phase 2 estimate: {0}", Vx2_stab.ToMathArrayString))
                        seeded = True
                    End If
                End If

                If Not seeded AndAlso Not provablySinglePhase Then
                    ' Feed appears stable per TPD; fall back to the composition-deviation
                    ' heuristic as a perturbation to probe for any edge cases.
                    IObj?.Paragraphs.Add("Stability test found no instability - using heuristic perturbation as initial estimate.")
                    Dim meanZ As Double = Vz.Sum() / (n + 1)
                    Dim maxDev As Double = -1.0
                    Dim splitComp As Integer = 0
                    For i = 0 To n
                        Dim dev As Double = Math.Abs(Vz(i) - meanZ)
                        If dev > maxDev Then
                            maxDev = dev
                            splitComp = i
                        End If
                    Next
                    For i = 0 To n
                        If i = splitComp Then
                            Vn1(i) = Vz(i) * 0.05
                            Vn2(i) = Vz(i) * 0.95
                        Else
                            Vn1(i) = Vz(i) * 0.95
                            Vn2(i) = Vz(i) * 0.05
                        End If
                    Next
                    L1 = Vn1.Sum
                    L2 = Vn2.Sum
                End If
            End If

            If UseInitialEstimatesForPhase1 Then
                For i = 0 To n
                    If Vz(i) > 0 Then Vn1(i) = L1 * InitialEstimatesForPhase1(i)
                Next
            End If

            If UseInitialEstimatesForPhase2 Then
                For i = 0 To n
                    If Vz(i) > 0 Then Vn2(i) = L2 * InitialEstimatesForPhase2(i)
                Next
            End If

            'renormalise Vn's
            S = Vn1.Sum() + Vn2.Sum()
            For i = 0 To n
                Vn1(i) /= S
                Vn2(i) /= S
            Next

            'calculate vapor pressures
            IObj?.SetCurrent
            For i = 0 To n
                Vp(i) = PP.AUX_PVAPi(i, T)
            Next
            IObj?.Paragraphs.Add(String.Format("Vapor pressures: {0} Pa", Vp.ToMathArrayString))


            Dim err As Double

            ecount = 0

            If provablySinglePhase Then
                Vx1 = Vz.Clone : Vx2 = Vz.Clone
                L1 = 1.0 : L2 = 0.0 : V = 0.0 : S = 0.0
                IObj?.SetCurrent()
                fi1 = PP.DW_CalcFugCoeff(Vx1, T, P, State.Liquid)
                fi2 = DirectCast(fi1.Clone, Double())
                For i = 0 To n
                    If Vp(i) > 0.001 Then gamma1(i) = P / Vp(i) * fi1(i) Else gamma1(i) = fi1(i)
                    gamma2(i) = gamma1(i)
                Next
                GoTo out
            End If

            IObj?.Paragraphs.Add("<h2>Starting iteration loop</h2>")
            Do
                IObj?.SetCurrent()
                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()
                Inspector.Host.CheckAndAdd(IObj2, "", "Flash_PT", "LLE-Flash Newton Iteration #" & ecount + 1, "Constant-Temperature LLE Flash Algorithm Convergence Iteration Step")

                IObj2?.Paragraphs.Add(String.Format("<b>Iteration:</b> {0}", ecount + 1))
                Vx1_ant = Vx1.Clone
                Vx2_ant = Vx2.Clone

                Vn1_ant = Vn1.Clone
                Vn2_ant = Vn2.Clone

                Vx1 = Vn1.MultiplyConstY(1 / L1).NormalizeY
                Vx2 = Vn2.MultiplyConstY(1 / L2).NormalizeY
                IObj2?.Paragraphs.Add(String.Format("Components: {0}", PP.RET_VNAMES.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("Composition phase 1: {0}", Vx1.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("Composition phase 2: {0}", Vx2.ToMathArrayString))

                IObj2?.SetCurrent
                IObj2?.Paragraphs.Add(String.Format("Calculating fugacity coefficients of liquid phases:", ecount))
                fi1 = PP.DW_CalcFugCoeff(Vx1, T, P, State.Liquid)
                fi2 = PP.DW_CalcFugCoeff(Vx2, T, P, State.Liquid)
                Dim lnfi1 = PP.DW_CalcLnFugCoeff(Vx1, T, P, State.Liquid)
                Dim lnfi2 = PP.DW_CalcLnFugCoeff(Vx2, T, P, State.Liquid)
                IObj2?.SetCurrent
                IObj2?.Paragraphs.Add(String.Format("Fugacity coefficients phase 1: {0}", fi1.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("Fugacity coefficients phase 2: {0}", fi2.ToMathArrayString))

                For i = 0 To n
                    If fi1(i) > 10000000000.0 Then fi1(i) = Vp(i) * 100
                    If fi2(i) > 10000000000.0 Then fi2(i) = Vp(i) * 100
                    If fi1(i) <= 0.0 OrElse fi2(i) <= 0.0 Then
                        ' The fugacity coefficient underflowed to zero (a high segment-number polymer,
                        ' whose ln is on the order of -1e3), which would make γ1/γ2 = φ1/φ2 = 0/0 = NaN.
                        ' That ratio is what the isoactivity update and residual actually use, so build it
                        ' from the log fugacity with a symmetric shift that keeps both values finite and
                        ' preserves the ratio exp(lnφ1 - lnφ2). Checked before the Pvap branch because a
                        ' non-volatile polymer can still report a small non-zero extrapolated Pvap.
                        Dim half As Double = 0.5 * (lnfi1(i) - lnfi2(i))
                        gamma1(i) = Math.Exp(half)
                        gamma2(i) = Math.Exp(-half)
                    ElseIf Vp(i) > 0.001 Then
                        ' Normal case: convert fugacity coefficients to activity coefficients
                        ' via the Raoult reference state (γ = P/Pvap · φ_liquid).
                        gamma1(i) = P / Vp(i) * fi1(i)
                        gamma2(i) = P / Vp(i) * fi2(i)
                    Else
                        ' Supercritical or highly non-volatile component (Pvap ≈ 0).
                        ' Dividing by Pvap would overflow; use the fugacity coefficients
                        ' directly instead. The equilibrium condition x1·fi1 = x2·fi2
                        ' is equivalent to x1·γ1 = x2·γ2 with γ ≡ fi, so the update
                        ' formula and convergence criterion are unchanged.
                        gamma1(i) = fi1(i)
                        gamma2(i) = fi2(i)
                    End If
                Next

                err = Vx1.MultiplyY(gamma1).SubtractY(Vx2.MultiplyY(gamma2)).AbsSumY()
                e1 = Vx1_ant.SubtractY(Vx1).AbsSumY
                e2 = Vx2_ant.SubtractY(Vx2).AbsSumY
                S = Vx1.SubtractY(Vx2).AbsY.MaxY

                IObj2?.SetCurrent
                IObj2?.Paragraphs.Add(String.Format("<hr><b>Actual Errors:</b><br>
                                                     Total Activity Difference between Phases: {0} (< 1e-6)<br><br>
                                                     Total Composition Changes since last Iteration:<br>
                                                     Phase 1: {1}<br>
                                                     Phase 2: {2}<br><br>",
                                                     err, e1, e2))

                IObj2?.Paragraphs.Add(String.Format("<b>Check Phases and Compositions:</b><br>
                                                     Components: {0} <br>
                                                     Component Differences between phases: {1}<br>
                                                     Largest Single-Component Composition Difference: {2} (> {6})<br>
                                                     Phase 1 Fraction {3}: (> 1e-4)<br>
                                                     Phase 2 Fraction {4}: (> 1e-4)<br><br>
                                                     Change of Phase Fractions since last Iteration: {5} (< 1e-7)",
                                                     PP.RET_VNAMES.ToMathArrayString, Vx1.SubtractY(Vx2).ToMathArrayString, S, L1, L2, Abs(L1_ant - L1) + Abs(L2_ant - L2), PhaseIdentityTolerance))

                If Double.IsNaN(err) Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashError"))

                If ecount > 0 And (err < 0.000001 Or L1 < 0.0001 Or L2 < 0.0001 Or S < PhaseIdentityTolerance) Then
                    IObj2?.Close()
                    Exit Do
                End If
                If Abs(L1_ant - L1) + Abs(L2_ant - L2) < 0.0000001 Then
                    IObj2?.Close()
                    Exit Do
                End If

                L1_ant = L1
                L2_ant = L2

                ' Successive substitution converges only linearly, and its rate degrades towards 1 near the
                ' consolute point - which is what the damping below is fighting, and where it runs out of
                ' iterations entirely. A Newton step on the isoactivity condition, using the analytical
                ' composition derivatives, converges quadratically and rescues exactly those cases.
                '
                ' It is a fallback rather than the default because a Newton iteration costs far more than a
                ' substitution one (two derivative matrices plus the line search), enough that it loses on
                ' wall-clock time wherever substitution already converges, despite needing an order of
                ' magnitude fewer iterations. So substitution runs while it is making progress, and Newton
                ' takes over only once it stalls: oscillation (the damping factor was cut) or a stubborn
                ' iteration count. That keeps the easy cases at their original cost and still converges the
                ' hard ones.
                '
                ' Newton is used ONLY from a genuine two-phase seed: the trivial solution x1 = x2 = z
                ' satisfies the isoactivity equations exactly, and Newton, having no way to know it must
                ' avoid that root, converges straight onto it from the blind perturbation seed. Successive
                ' substitution drifts away from it slowly enough to act as a crude safeguard there.
                Dim ssStalled As Boolean = (dampFactor < 1.0 OrElse ecount >= NewtonFallbackIterations)
                Dim newtonOK As Boolean = False
                If seeded AndAlso ssStalled Then
                    Dim f0 As Double = 0.0
                    Dim dnstep = NewtonStepLLE(Vz, Vn1, T, P, PP, f0)
                    If dnstep IsNot Nothing Then
                        If Not Double.IsNaN(f0) Then
                            Dim lam As Double = 1.0
                            For ls As Integer = 1 To 8
                                Dim trial(n) As Double
                                Dim feasible As Boolean = True
                                For i = 0 To n
                                    If Vz(i) > 0.0 Then
                                        trial(i) = Vn1(i) + lam * dnstep(i)
                                        If trial(i) <= 0.000000001 * Vz(i) OrElse trial(i) >= 0.999999999 * Vz(i) Then feasible = False
                                    Else
                                        trial(i) = 0.0
                                    End If
                                Next
                                If feasible Then
                                    Dim ft As Double = LLEResidualNorm(Vz, trial, T, P, PP)
                                    If Not Double.IsNaN(ft) AndAlso ft < f0 Then
                                        Vn1 = trial
                                        newtonOK = True
                                        IObj2?.SetCurrent()
                                        IObj2?.Paragraphs.Add(String.Format("Analytical Newton step accepted (lambda = {0:F4}, residual {1:E3} -> {2:E3}).", lam, f0, ft))
                                        Exit For
                                    End If
                                End If
                                lam *= 0.5
                            Next
                        End If
                    End If
                End If

                If Not newtonOK Then
                    Dim Vn1_new As Double() = Vz.DivideY(gamma1.MultiplyConstY(L2).DivideY(gamma2.MultiplyConstY(L1)).AddConstY(1))

                    Dim L1_new As Double = Vn1_new.Sum()
                    Dim dL1 As Double = L1_new - L1

                    ' Adaptive damping: detect oscillation by monitoring sign reversals in the
                    ' L1 update direction. Each sign reversal halves the step (minimum 5%).
                    ' When updates are consistent, the step size recovers toward 1.
                    If ecount > 0 AndAlso dL1_prev * dL1 < 0.0 Then
                        dampFactor = Math.Max(dampFactor * 0.5, 0.05)
                        IObj2?.SetCurrent()
                        IObj2?.Paragraphs.Add(String.Format("<b>Oscillation detected (iter {0})! Damping factor reduced to {1:F4}.</b>", ecount, dampFactor))
                    Else
                        dampFactor = Math.Min(dampFactor * 1.2, 1.0)
                    End If
                    dL1_prev = dL1

                    ' Apply damped update: blend new and previous molar amounts
                    Vn1 = Vn1_new.MultiplyConstY(dampFactor).AddY(Vn1_ant.MultiplyConstY(1.0 - dampFactor))
                End If

                Vn2 = Vz.SubtractY(Vn1)

                L1 = Vn1.Sum
                L2 = 1 - L1

                ecount += 1

                If ecount >= maxit_e Then
                    If seeded Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashMaxIt"))
                    ' Nothing here is evidence that a split exists: the stability test found this feed
                    ' stable, the spinodal analysis offered no seed, and the heuristic perturbation - which
                    ' only probes for edge cases the test might have missed - has not converged on one
                    ' either. So the stability verdict stands and the single phase is the answer, rather
                    ' than failing a flash whose result is not actually in doubt. A seeded run is different:
                    ' there a split is known to exist, so not converging is a real failure and still throws.
                    IObj2?.SetCurrent()
                    IObj2?.Paragraphs.Add("The heuristic perturbation did not converge on a split, and the stability test had already found the feed stable: reporting a single phase.")
                    Vx1 = DirectCast(Vz.Clone, Double())
                    Vx2 = DirectCast(Vz.Clone, Double())
                    L1 = 1.0 : L2 = 0.0 : S = 0.0
                    IObj2?.Close()
                    GoTo out
                End If

                IObj2?.Close()
            Loop

out:        d2 = Date.Now
            dt = d2 - d1

            IObj?.SetCurrent
            If L1 < 0.0001 Or L2 < 0.0001 Or S < PhaseIdentityTolerance Then
                'merge phases - both phases are identical
                IObj?.Paragraphs.Add(String.Format("<hr><b>Phase merge necessary!</b><br>"))
                IObj?.Paragraphs.Add(String.Format("Both Liquid phases either are identical or one phase has vanished!"))
                If L1 < 0.0001 Then IObj?.Paragraphs.Add(String.Format("Liquid phase 1 Molar Fraction: {0} < 0.0001", L1))
                If L2 < 0.0001 Then IObj?.Paragraphs.Add(String.Format("Liquid phase 2 Molar Fraction: {0} < 0.0001", L2))
                If S < PhaseIdentityTolerance Then
                    IObj?.Paragraphs.Add(String.Format("Components: {0}", PP.RET_VNAMES.ToMathArrayString))
                    IObj?.Paragraphs.Add(String.Format("Liquid phases compositions are identical (largest single-component difference {0} < {1})! <br>Fraction differences: {2}", S, PhaseIdentityTolerance, Vx1.SubtractY(Vx2).ToMathArrayString))
                End If

                result = {1, V, Vz, PP.RET_NullVector, ecount, 0, Vx2, 0.0#, PP.RET_NullVector, gamma1, gamma2}
            Else

                'order liquid phases by gibbs energy
                Dim gl1 = PP.DW_CalcGibbsEnergy(Vx1, T, P, "L")
                Dim gl2 = PP.DW_CalcGibbsEnergy(Vx2, T, P, "L")
                If gl1 < gl2 Then
                    result = {L2, V, Vx2, PP.RET_NullVector, ecount, L1, Vx1, 0.0#, PP.RET_NullVector, gamma2, gamma1}
                Else
                    result = {L1, V, Vx1, PP.RET_NullVector, ecount, L2, Vx2, 0.0#, PP.RET_NullVector, gamma1, gamma2}
                End If
            End If

            IObj?.Paragraphs.Add(String.Format("<hr><h2>Results:</h2>
                                                Liquid Phase 1 Fraction: {0}<br> 
                                                Liquid Phase 2 Fraction: {1}<br><br>
                                                Compounds: {2}<br>
                                                Liquid Phase 1 Composition: {3}<br>
                                                Liquid Phase 2 Composition: {4}", L1, L2, PP.RET_VNAMES.ToMathArrayString, Vx1.ToMathArrayString, Vx2.ToMathArrayString))

            WriteDebugInfo("PT Flash [SimpleLLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms. Error function value: " & err)

            IObj?.Paragraphs.Add("PT Flash [SimpleLLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms. Error function value: " & err)

            IObj?.Close()

            Return result

        End Function

        Public Overrides Function Flash_PH(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim doparallel As Boolean = Settings.EnableParallelProcessing

            Dim Vn(1) As String, Vx(1), Vy(1), Vx_ant(1), Vy_ant(1), Vp(1), Ki(1), Ki_ant(1), fi(1) As Double
            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, V, T, Pf As Double

            d1 = Date.Now

            n = Vz.Length - 1

            PP = PP
            Hf = H
            Pf = P

            ReDim Vn(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n)

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim maxitINT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_Internal_Iterations)
            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolINT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tsup, Tinf ', Hsup, Hinf

            If Tref <> 0 Then
                Tinf = Tref - 250
                Tsup = Tref + 250
            Else
                Tinf = 100
                Tsup = 2000
            End If
            If Tinf < 100 Then Tinf = 100

            Dim bo As New BrentOpt.Brent
            bo.DefineFuncDelegate(AddressOf Herror)
            WriteDebugInfo("PH Flash: Starting calculation for " & Tinf & " <= T <= " & Tsup)

            Dim fx, fx2, dfdx, x1 As Double

            Dim cnt As Integer = 0

            If Tref = 0 Then Tref = 298.15
            x1 = Tref
            Do
                If Settings.EnableParallelProcessing Then

                    Dim task1 As Task = TaskHelper.Run(Sub()
                                                           fx = Herror(x1, {P, Vz, PP})
                                                       End Sub)
                    Dim task2 As Task = TaskHelper.Run(Sub()
                                                           fx2 = Herror(x1 + 1, {P, Vz, PP})
                                                       End Sub)
                    Task.WaitAll(task1, task2)
                Else
                    fx = Herror(x1, {P, Vz, PP})
                    fx2 = Herror(x1 + 1, {P, Vz, PP})
                End If
                If Abs(fx) < etol Then Exit Do
                dfdx = (fx2 - fx)
                x1 = x1 - fx / dfdx
                If x1 < 0 Then GoTo alt
                cnt += 1
            Loop Until cnt > 20 Or Double.IsNaN(x1)
            If Double.IsNaN(x1) Then
alt:            T = bo.BrentOpt(Tinf, Tsup, 10, tolEXT, maxitEXT, {P, Vz, PP})
            Else
                T = x1
            End If

            'End If

            Dim tmp As Object = Flash_PT(Vz, P, T, PP)

            L = tmp(0)
            V = tmp(1)
            Vx = tmp(2)
            Vy = tmp(3)
            ecount = tmp(4)

            For i = 0 To n
                Ki(i) = Vy(i) / Vx(i)
            Next

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PH Flash [SimpleLLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides Function Flash_PS(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim doparallel As Boolean = Settings.EnableParallelProcessing

            Dim Vn(1) As String, Vx(1), Vy(1), Vx_ant(1), Vy_ant(1), Vp(1), Ki(1), Ki_ant(1), fi(1) As Double
            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, V, T, Pf As Double

            d1 = Date.Now

            n = Vz.Length - 1

            PP = PP
            Sf = S
            Pf = P

            ReDim Vn(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n)

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim maxitINT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_Internal_Iterations)
            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolINT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tsup, Tinf ', Ssup, Sinf

            If Tref <> 0 Then
                Tinf = Tref - 200
                Tsup = Tref + 200
            Else
                Tinf = 100
                Tsup = 2000
            End If
            If Tinf < 100 Then Tinf = 100
            Dim bo As New BrentOpt.Brent
            bo.DefineFuncDelegate(AddressOf Serror)
            WriteDebugInfo("PS Flash: Starting calculation for " & Tinf & " <= T <= " & Tsup)

            Dim fx, fx2, dfdx, x1 As Double

            Dim cnt As Integer = 0

            If Tref = 0 Then Tref = 298.15
            x1 = Tref
            Do
                If Settings.EnableParallelProcessing Then

                    Dim task1 As Task = TaskHelper.Run(Sub()
                                                           fx = Serror(x1, {P, Vz, PP})
                                                       End Sub)
                    Dim task2 As Task = TaskHelper.Run(Sub()
                                                           fx2 = Serror(x1 + 1, {P, Vz, PP})
                                                       End Sub)
                    Task.WaitAll(task1, task2)

                Else
                    fx = Serror(x1, {P, Vz, PP})
                    fx2 = Serror(x1 + 1, {P, Vz, PP})
                End If
                If Abs(fx) < etol Then Exit Do
                dfdx = (fx2 - fx)
                x1 = x1 - fx / dfdx
                If x1 < 0 Then GoTo alt
                cnt += 1
            Loop Until cnt > 50 Or Double.IsNaN(x1)
            If Double.IsNaN(x1) Then
alt:            T = bo.BrentOpt(Tinf, Tsup, 10, tolEXT, maxitEXT, {P, Vz, PP})
            Else
                T = x1
            End If

            Dim tmp As Object = Flash_PT(Vz, P, T, PP)

            L = tmp(0)
            V = tmp(1)
            Vx = tmp(2)
            Vy = tmp(3)
            ecount = tmp(4)

            For i = 0 To n
                Ki(i) = Vy(i) / Vx(i)
            Next

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PS Flash [SimpleLLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides Function Flash_TV(ByVal Vz As Double(), ByVal T As Double, ByVal V As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim Vn(1) As String, Vx(1), Vy(1), Vx_ant(1), Vy_ant(1), Vp(1), Ki(1), Ki_ant(1), fi(1) As Double
            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim Pmin, Pmax, soma_x, soma_y As Double
            Dim L, Lf, Vf, P, Pf As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            PP = PP
            Vf = V
            L = 1 - V
            Lf = 1 - Vf
            Pf = P

            ReDim Vn(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n)
            Dim dFdP As Double

            Dim VTc = PP.RET_VTC()

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            If Pref = 0 Then

                i = 0
                Do
                    Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                    i += 1
                Loop Until i = n + 1

                Pmin = Vp.Min
                Pmax = Vp.Max

                Pref = Pmin + (1 - V) * (Pmax - Pmin)

            Else

                Pmin = Pref * 0.8
                Pmax = Pref * 1.2

            End If

            P = Pref

            'Calculate Ki`s

            If Not ReuseKI Then
                i = 0
                Do
                    Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                    Ki(i) = Vp(i) / P
                    i += 1
                Loop Until i = n + 1
            Else
                If Not PP.AUX_CheckTrivial(PrevKi) Then
                    For i = 0 To n
                        Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                        Ki(i) = PrevKi(i)
                    Next
                Else
                    i = 0
                    Do
                        Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                        Ki(i) = Vp(i) / P
                        i += 1
                    Loop Until i = n + 1
                End If
            End If

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                    Vx(i) = Vy(i) / Ki(i)
                Else
                    Vy(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            i = 0
            soma_x = 0
            soma_y = 0
            Do
                soma_x = soma_x + Vx(i)
                soma_y = soma_y + Vy(i)
                i = i + 1
            Loop Until i = n + 1
            i = 0
            Do
                Vx(i) = Vx(i) / soma_x
                Vy(i) = Vy(i) / soma_y
                i = i + 1
            Loop Until i = n + 1

            Dim marcador3, marcador2, marcador As Integer
            Dim stmp4_ant, stmp4, Pant, fval As Double
            Dim chk As Boolean = False

            If V = 1.0# Or V = 0.0# Then

                ecount = 0
                Do

                    marcador3 = 0

                    Dim cont_int = 0
                    Do


                        Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                        marcador = 0
                        If stmp4_ant <> 0 Then
                            marcador = 1
                        End If
                        stmp4_ant = stmp4

                        If V = 0 Then
                            i = 0
                            stmp4 = 0
                            Do
                                stmp4 = stmp4 + Ki(i) * Vx(i)
                                i = i + 1
                            Loop Until i = n + 1
                        Else
                            i = 0
                            stmp4 = 0
                            Do
                                stmp4 = stmp4 + Vy(i) / Ki(i)
                                i = i + 1
                            Loop Until i = n + 1
                        End If

                        If V = 0 Then
                            i = 0
                            Do
                                Vy_ant(i) = Vy(i)
                                Vy(i) = Ki(i) * Vx(i) / stmp4
                                i = i + 1
                            Loop Until i = n + 1
                        Else
                            i = 0
                            Do
                                Vx_ant(i) = Vx(i)
                                Vx(i) = (Vy(i) / Ki(i)) / stmp4
                                i = i + 1
                            Loop Until i = n + 1
                        End If

                        marcador2 = 0
                        If marcador = 1 Then
                            If V = 0 Then
                                If Math.Abs(Vy(0) - Vy_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            Else
                                If Math.Abs(Vx(0) - Vx_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            End If
                        End If

                        cont_int = cont_int + 1

                    Loop Until marcador2 = 1 Or Double.IsNaN(stmp4) Or cont_int > maxit_i

                    Dim K1(n), K2(n), dKdP(n) As Double

                    K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                    K2 = PP.DW_CalcKvalue(Vx, Vy, T, P * 1.001)

                    For i = 0 To n
                        dKdP(i) = (K2(i) - K1(i)) / (0.001 * P)
                    Next

                    fval = stmp4 - 1

                    ecount += 1

                    i = 0
                    dFdP = 0
                    Do
                        If V = 0 Then
                            dFdP = dFdP + Vx(i) * dKdP(i)
                        Else
                            dFdP = dFdP - Vy(i) / (Ki(i) ^ 2) * dKdP(i)
                        End If
                        i = i + 1
                    Loop Until i = n + 1

                    If (P - fval / dFdP) < 0 Then
                        P = (P + Pant) / 2
                    Else
                        Pant = P
                        P = P - fval / dFdP
                    End If

                    WriteDebugInfo("TV Flash [SimpleLLE]: Iteration #" & ecount & ", P = " & P & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                Loop Until Math.Abs(P - Pant) < 1 Or Double.IsNaN(P) = True Or ecount > maxit_e Or Double.IsNaN(P) Or Double.IsInfinity(P)

            Else

                ecount = 0

                Do

                    Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                    i = 0
                    Do
                        If Vz(i) <> 0 Then
                            Vy_ant(i) = Vy(i)
                            Vx_ant(i) = Vx(i)
                            Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                            Vx(i) = Vy(i) / Ki(i)
                        Else
                            Vy(i) = 0
                            Vx(i) = 0
                        End If
                        i += 1
                    Loop Until i = n + 1
                    i = 0
                    soma_x = 0
                    soma_y = 0
                    Do
                        soma_x = soma_x + Vx(i)
                        soma_y = soma_y + Vy(i)
                        i = i + 1
                    Loop Until i = n + 1
                    i = 0
                    Do
                        Vx(i) = Vx(i) / soma_x
                        Vy(i) = Vy(i) / soma_y
                        i = i + 1
                    Loop Until i = n + 1

                    If V <= 0.5 Then

                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Ki(i) * Vx(i)
                            i = i + 1
                        Loop Until i = n + 1

                        Dim K1(n), K2(n), dKdP(n) As Double

                        K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                        K2 = PP.DW_CalcKvalue(Vx, Vy, T, P * 1.001)

                        For i = 0 To n
                            dKdP(i) = (K2(i) - K1(i)) / (0.001 * P)
                        Next

                        i = 0
                        dFdP = 0
                        Do
                            dFdP = dFdP + Vx(i) * dKdP(i)
                            i = i + 1
                        Loop Until i = n + 1

                    Else

                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Vy(i) / Ki(i)
                            i = i + 1
                        Loop Until i = n + 1

                        Dim K1(n), K2(n), dKdP(n) As Double

                        K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                        K2 = PP.DW_CalcKvalue(Vx, Vy, T, P * 1.001)

                        For i = 0 To n
                            dKdP(i) = (K2(i) - K1(i)) / (0.001 * P)
                        Next

                        i = 0
                        dFdP = 0
                        Do
                            dFdP = dFdP - Vy(i) / (Ki(i) ^ 2) * dKdP(i)
                            i = i + 1
                        Loop Until i = n + 1
                    End If

                    ecount += 1

                    fval = stmp4 - 1

                    If (P - fval / dFdP) < 0 Then
                        P = (P + Pant) / 2
                    Else
                        Pant = P
                        P = P - fval / dFdP
                    End If

                    WriteDebugInfo("TV Flash [SimpleLLE]: Iteration #" & ecount & ", P = " & P & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                Loop Until Math.Abs(fval) < etol Or Double.IsNaN(P) = True Or ecount > maxit_e

            End If

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("TV Flash [SimpleLLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {L, V, Vx, Vy, P, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides Function Flash_PV(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim Vn(1) As String, Vx(1), Vy(1), Vx_ant(1), Vy_ant(1), Vp(1), Ki(1), Ki_ant(1), fi(1) As Double
            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim soma_x, soma_y As Double
            Dim L, Lf, Vf, T, Tf As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            PP = PP
            Vf = V
            L = 1 - V
            Lf = 1 - Vf
            Tf = T

            ReDim Vn(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n)
            Dim Vt(n), VTc(n), Tmin, Tmax, dFdT As Double

            Vn = PP.RET_VNAMES()
            VTc = PP.RET_VTC()
            fi = Vz.Clone

            If Tref = 0.0# Then

                i = 0
                Tref = 0
                Do
                    Tref += 0.7 * Vz(i) * VTc(i)
                    Tmin += 0.1 * Vz(i) * VTc(i)
                    Tmax += 2.0 * Vz(i) * VTc(i)
                    i += 1
                Loop Until i = n + 1

            Else

                Tmin = Tref - 50
                Tmax = Tref + 50

            End If

            T = Tref

            'Calculate Ki`s

            If Not ReuseKI Then
                i = 0
                Do
                    Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                    Ki(i) = Vp(i) / P
                    i += 1
                Loop Until i = n + 1
            Else
                If Not PP.AUX_CheckTrivial(PrevKi) And Not Double.IsNaN(PrevKi(0)) Then
                    For i = 0 To n
                        Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                        Ki(i) = PrevKi(i)
                    Next
                Else
                    i = 0
                    Do
                        Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                        Ki(i) = Vp(i) / P
                        i += 1
                    Loop Until i = n + 1
                End If
            End If

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                    Vx(i) = Vy(i) / Ki(i)
                Else
                    Vy(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            i = 0
            soma_x = 0
            soma_y = 0
            Do
                soma_x = soma_x + Vx(i)
                soma_y = soma_y + Vy(i)
                i = i + 1
            Loop Until i = n + 1
            i = 0
            Do
                Vx(i) = Vx(i) / soma_x
                Vy(i) = Vy(i) / soma_y
                i = i + 1
            Loop Until i = n + 1

            Dim marcador3, marcador2, marcador As Integer
            Dim stmp4_ant, stmp4, Tant, fval As Double
            Dim chk As Boolean = False

            If V = 1.0# Or V = 0.0# Then

                ecount = 0
                Do

                    marcador3 = 0

                    Dim cont_int = 0
                    Do


                        Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                        marcador = 0
                        If stmp4_ant <> 0 Then
                            marcador = 1
                        End If
                        stmp4_ant = stmp4

                        If V = 0 Then
                            i = 0
                            stmp4 = 0
                            Do
                                stmp4 = stmp4 + Ki(i) * Vx(i)
                                i = i + 1
                            Loop Until i = n + 1
                        Else
                            i = 0
                            stmp4 = 0
                            Do
                                stmp4 = stmp4 + Vy(i) / Ki(i)
                                i = i + 1
                            Loop Until i = n + 1
                        End If

                        If V = 0 Then
                            i = 0
                            Do
                                Vy_ant(i) = Vy(i)
                                Vy(i) = Ki(i) * Vx(i) / stmp4
                                i = i + 1
                            Loop Until i = n + 1
                        Else
                            i = 0
                            Do
                                Vx_ant(i) = Vx(i)
                                Vx(i) = (Vy(i) / Ki(i)) / stmp4
                                i = i + 1
                            Loop Until i = n + 1
                        End If

                        marcador2 = 0
                        If marcador = 1 Then
                            If V = 0 Then
                                If Math.Abs(Vy(0) - Vy_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            Else
                                If Math.Abs(Vx(0) - Vx_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            End If
                        End If

                        cont_int = cont_int + 1

                    Loop Until marcador2 = 1 Or Double.IsNaN(stmp4) Or cont_int > maxit_i

                    Dim K1(n), K2(n), dKdT(n) As Double

                    K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                    K2 = PP.DW_CalcKvalue(Vx, Vy, T + 0.1, P)

                    For i = 0 To n
                        dKdT(i) = (K2(i) - K1(i)) / (0.1)
                    Next

                    fval = stmp4 - 1

                    ecount += 1

                    i = 0
                    dFdT = 0
                    Do
                        If V = 0 Then
                            dFdT = dFdT + Vx(i) * dKdT(i)
                        Else
                            dFdT = dFdT - Vy(i) / (Ki(i) ^ 2) * dKdT(i)
                        End If
                        i = i + 1
                    Loop Until i = n + 1

                    Tant = T
                    T = T - fval / dFdT
                    If T < Tmin Then T = Tmin
                    If T > Tmax Then T = Tmax

                    WriteDebugInfo("PV Flash [SimpleLLE]: Iteration #" & ecount & ", T = " & T & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                Loop Until Math.Abs(T - Tant) < 0.1 Or Double.IsNaN(T) = True Or ecount > maxit_e Or Double.IsNaN(T) Or Double.IsInfinity(T)

            Else

                ecount = 0

                Do

                    Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                    i = 0
                    Do
                        If Vz(i) <> 0 Then
                            Vy_ant(i) = Vy(i)
                            Vx_ant(i) = Vx(i)
                            Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                            Vx(i) = Vy(i) / Ki(i)
                        Else
                            Vy(i) = 0
                            Vx(i) = 0
                        End If
                        i += 1
                    Loop Until i = n + 1
                    i = 0
                    soma_x = 0
                    soma_y = 0
                    Do
                        soma_x = soma_x + Vx(i)
                        soma_y = soma_y + Vy(i)
                        i = i + 1
                    Loop Until i = n + 1
                    i = 0
                    Do
                        Vx(i) = Vx(i) / soma_x
                        Vy(i) = Vy(i) / soma_y
                        i = i + 1
                    Loop Until i = n + 1

                    If V <= 0.5 Then

                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Ki(i) * Vx(i)
                            i = i + 1
                        Loop Until i = n + 1

                        Dim K1(n), K2(n), dKdT(n) As Double

                        K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                        K2 = PP.DW_CalcKvalue(Vx, Vy, T + 0.1, P)

                        For i = 0 To n
                            dKdT(i) = (K2(i) - K1(i)) / (0.1)
                        Next

                        i = 0
                        dFdT = 0
                        Do
                            dFdT = dFdT + Vx(i) * dKdT(i)
                            i = i + 1
                        Loop Until i = n + 1

                    Else

                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Vy(i) / Ki(i)
                            i = i + 1
                        Loop Until i = n + 1

                        Dim K1(n), K2(n), dKdT(n) As Double

                        K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                        K2 = PP.DW_CalcKvalue(Vx, Vy, T + 0.1, P)

                        For i = 0 To n
                            dKdT(i) = (K2(i) - K1(i)) / (0.1)
                        Next

                        i = 0
                        dFdT = 0
                        Do
                            dFdT = dFdT - Vy(i) / (Ki(i) ^ 2) * dKdT(i)
                            i = i + 1
                        Loop Until i = n + 1
                    End If

                    ecount += 1

                    fval = stmp4 - 1

                    Tant = T
                    T = T - fval / dFdT
                    If T < Tmin Then T = Tmin
                    If T > Tmax Then T = Tmax

                    WriteDebugInfo("PV Flash [SimpleLLE]: Iteration #" & ecount & ", T = " & T & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                Loop Until Math.Abs(fval) < etol Or Double.IsNaN(T) = True Or ecount > maxit_e

            End If

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PV Flash [SimpleLLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Function OBJ_FUNC_PH_FLASH(ByVal T As Double, ByVal H As Double, ByVal P As Double, ByVal Vz As Object, ByVal pp As PropertyPackage) As Object

            Dim tmp As Object
            tmp = Me.Flash_PT(Vz, P, T, pp)
            Dim L, V, Vx(), Vy(), _Hv, _Hl As Double

            Dim n = Vz.Length - 1

            L = tmp(0)
            V = tmp(1)
            Vx = tmp(2)
            Vy = tmp(3)

            _Hv = 0
            _Hl = 0

            Dim mmg, mml As Double
            If V > 0 Then _Hv = pp.DW_CalcEnthalpy(Vy, T, P, State.Vapor)
            If L > 0 Then _Hl = pp.DW_CalcEnthalpy(Vx, T, P, State.Liquid)
            mmg = pp.AUX_MMM(Vy)
            mml = pp.AUX_MMM(Vx)

            Dim herr As Double = Hf - (mmg * V / (mmg * V + mml * L)) * _Hv - (mml * L / (mmg * V + mml * L)) * _Hl
            OBJ_FUNC_PH_FLASH = herr

            WriteDebugInfo("PH Flash [SimpleLLE]: Current T = " & T & ", Current H Error = " & herr)

        End Function

        Function OBJ_FUNC_PS_FLASH(ByVal T As Double, ByVal S As Double, ByVal P As Double, ByVal Vz As Object, ByVal pp As PropertyPackage) As Object

            Dim tmp = Me.Flash_PT(Vz, P, T, pp)
            Dim L, V, Vx(), Vy(), _Sv, _Sl As Double

            Dim n = Vz.Length - 1

            L = tmp(0)
            V = tmp(1)
            Vx = tmp(2)
            Vy = tmp(3)

            _Sv = 0
            _Sl = 0
            Dim mmg, mml As Double

            If V > 0 Then _Sv = pp.DW_CalcEntropy(Vy, T, P, State.Vapor)
            If L > 0 Then _Sl = pp.DW_CalcEntropy(Vx, T, P, State.Liquid)
            mmg = pp.AUX_MMM(Vy)
            mml = pp.AUX_MMM(Vx)

            Dim serr As Double = Sf - (mmg * V / (mmg * V + mml * L)) * _Sv - (mml * L / (mmg * V + mml * L)) * _Sl
            OBJ_FUNC_PS_FLASH = serr

            WriteDebugInfo("PS Flash [SimpleLLE]: Current T = " & T & ", Current S Error = " & serr)

        End Function

        Function Herror(ByVal Tt As Double, ByVal otherargs As Object) As Double
            Return OBJ_FUNC_PH_FLASH(Tt, Sf, otherargs(0), otherargs(1), otherargs(2))
        End Function

        Function Serror(ByVal Tt As Double, ByVal otherargs As Object) As Double
            Return OBJ_FUNC_PS_FLASH(Tt, Sf, otherargs(0), otherargs(1), otherargs(2))
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace

