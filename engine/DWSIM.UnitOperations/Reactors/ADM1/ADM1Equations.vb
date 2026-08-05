'    Full ADM1 (Batstone et al. 2002) - Biochemical rates, pH, derivatives, gas transfer.
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Math

Namespace Reactors.ADM1

    ''' <summary>
    ''' ADM1 biochemical rates + stoichiometric derivatives + algebraic pH solver + gas transfer.
    ''' All rates are in kg COD / m³ / d (or kmol / m³ / d for inorganic-C/N). All kinetic constants
    ''' expected in per-day units. The integrator operates in day time units.
    ''' </summary>
    Public Module ADM1Equations

        ' ---- Fixed product yields of the LCFA and VFA uptake processes ----
        ' Rosen & Jeppsson 2006 Table A.1. These are the fixed stoichiometry of four processes, not
        ' tunable parameters, and each set sums to 1. They are needed in three places that must agree:
        ' ComputeDerivatives, SolveH2QSS (which has to stay the exact reduction of d_(7)) and the
        ' carbon balance. Naming follows the f_<product>_<substrate> convention of StoichiometryParams.
        Private Const f_ac_fa As Double = 0.7    ' rho_7:  LCFA       -> acetate
        Private Const f_h2_fa As Double = 0.3    ' rho_7:  LCFA       -> H2
        Private Const f_pro_va As Double = 0.54  ' rho_8:  valerate   -> propionate
        Private Const f_ac_va As Double = 0.31   ' rho_8:  valerate   -> acetate
        Private Const f_h2_va As Double = 0.15   ' rho_8:  valerate   -> H2
        Private Const f_ac_bu As Double = 0.8    ' rho_9:  butyrate   -> acetate
        Private Const f_h2_bu As Double = 0.2    ' rho_9:  butyrate   -> H2
        Private Const f_ac_pro As Double = 0.57  ' rho_10: propionate -> acetate
        Private Const f_h2_pro As Double = 0.43  ' rho_10: propionate -> H2

        ' ---- Van't Hoff enthalpies (J/mol), Rosen & Jeppsson 2006 ----
        ' Physical properties of the species, not tunable parameters, so they live here rather than
        ' in the editable parameter set. P_gas_h2o's 5290 is an exponent coefficient in kelvin, not
        ' an enthalpy: it is used without dividing by R.
        Private Const dH_K_w As Double = 55900.0
        Private Const dH_K_a_co2 As Double = 7646.0
        Private Const dH_K_a_IN As Double = 51965.0
        Private Const dH_K_a_h2s As Double = 21900.0
        Private Const dH_K_H_co2 As Double = -19410.0
        Private Const dH_K_H_ch4 As Double = -14240.0
        Private Const dH_K_H_h2 As Double = -4180.0
        Private Const dH_K_H_h2s As Double = -19200.0
        Private Const C_P_gas_h2o As Double = 5290.0
        Private Const R_J As Double = 8.3145            ' J/(mol·K) = 100 · R[bar·m³/(kmol·K)]

        ''' <summary>Van't Hoff: value at T_op from the value at T_base.</summary>
        Private Function VantHoff(kBase As Double, dH As Double, T_base As Double, T_op As Double) As Double
            Return kBase * Exp(dH / R_J * (1.0 / T_base - 1.0 / T_op))
        End Function

        ''' <summary>
        ''' The acid-base and Henry constants at the operating temperature, derived from the 25 °C
        ''' values held in <paramref name="phys"/>.
        ''' </summary>
        ''' <remarks>
        ''' Returns a copy rather than mutating: the parameter object is shared across every RK
        ''' stage and, in regression, across every trial, so correcting it in place would compound
        ''' the exponential on each pass. The VFA constants and k_La are carried through untouched.
        ''' At T_op = 35 °C this reproduces the constants the model used to hard-code, to the last
        ''' digit - which is what keeps the BSM2 benchmark undisturbed.
        ''' </remarks>
        ''' <param name="T_op">Operating temperature (K). Defaults to phys.T_op_K; pass it explicitly
        ''' from callers that carry the temperature themselves rather than in the parameter set.</param>
        Public Function TemperatureCorrect(phys As PhysicochemicalParams,
                                           Optional T_op As Double = -1.0) As PhysicochemicalParams
            Dim Tb = phys.T_base_K
            Dim Top = If(T_op > 0.0, T_op, phys.T_op_K)
            If Top <= 0.0 OrElse Tb <= 0.0 OrElse Abs(Top - Tb) < 1.0E-09 Then Return phys

            Dim c As New PhysicochemicalParams() With {
                .T_base_K = Tb, .T_op_K = Top, .R = phys.R,
                .K_a_va = phys.K_a_va, .K_a_bu = phys.K_a_bu,
                .K_a_pro = phys.K_a_pro, .K_a_ac = phys.K_a_ac,
                .k_AB_va = phys.k_AB_va, .k_AB_bu = phys.k_AB_bu, .k_AB_pro = phys.k_AB_pro,
                .k_AB_ac = phys.k_AB_ac, .k_AB_co2 = phys.k_AB_co2, .k_AB_IN = phys.k_AB_IN,
                .k_La = phys.k_La, .P_atm = phys.P_atm, .k_P = phys.k_P,
                .K_w = VantHoff(phys.K_w, dH_K_w, Tb, Top),
                .K_a_co2 = VantHoff(phys.K_a_co2, dH_K_a_co2, Tb, Top),
                .K_a_IN = VantHoff(phys.K_a_IN, dH_K_a_IN, Tb, Top),
                .K_a_h2s = VantHoff(phys.K_a_h2s, dH_K_a_h2s, Tb, Top),
                .K_H_co2 = VantHoff(phys.K_H_co2, dH_K_H_co2, Tb, Top),
                .K_H_ch4 = VantHoff(phys.K_H_ch4, dH_K_H_ch4, Tb, Top),
                .K_H_h2 = VantHoff(phys.K_H_h2, dH_K_H_h2, Tb, Top),
                .K_H_h2s = VantHoff(phys.K_H_h2s, dH_K_H_h2s, Tb, Top),
                .P_gas_h2o = phys.P_gas_h2o * Exp(C_P_gas_h2o * (1.0 / Tb - 1.0 / Top))
            }
            Return c
        End Function

        ''' <summary>
        ''' Net carbon transferred out of the inorganic-carbon pool by each of the 19 processes
        ''' (kmol C per kg COD), returned as the 13 distinct coefficients s_1..s_13 of Rosen &amp;
        ''' Jeppsson 2006, indexed 0..12.
        ''' </summary>
        ''' <remarks>
        ''' Each s_j is just the carbon leaving minus the carbon entering for process j, read off the
        ''' Petersen matrix. A positive s_j consumes S_IC, a negative one produces it. The mapping to
        ''' the rate array is one-to-one for s_1..s_12 (r(0)..r(11)); s_13 covers all seven decays,
        ''' which convert biomass back into composites.
        '''
        ''' s_12 comes out positive: hydrogenotrophic methanogens build CH4 out of CO2 and consume
        ''' inorganic carbon. Without this balance the model made that methane out of nothing.
        '''
        ''' Depends only on parameters, so it is safe to hoist out of the integrator's inner loop.
        ''' </remarks>
        Public Function CarbonBalance(stoi As StoichiometryParams) As Double()
            Dim s(12) As Double

            ' Disintegration: composites -> soluble inerts, carbohydrate, protein, lipid, particulate inerts
            s(0) = -stoi.C_xc + stoi.f_sI_xc * stoi.C_sI + stoi.f_ch_xc * stoi.C_ch +
                   stoi.f_pr_xc * stoi.C_pr + stoi.f_li_xc * stoi.C_li + stoi.f_xI_xc * stoi.C_xI
            ' Hydrolysis
            s(1) = -stoi.C_ch + stoi.C_su
            s(2) = -stoi.C_pr + stoi.C_aa
            s(3) = -stoi.C_li + (1.0 - stoi.f_fa_li) * stoi.C_su + stoi.f_fa_li * stoi.C_fa
            ' Sugar and amino-acid fermentation
            s(4) = -stoi.C_su + (1.0 - stoi.Y_su) * (stoi.f_bu_su * stoi.C_bu + stoi.f_pro_su * stoi.C_pro +
                                                     stoi.f_ac_su * stoi.C_ac) + stoi.Y_su * stoi.C_bac
            s(5) = -stoi.C_aa + (1.0 - stoi.Y_aa) * (stoi.f_va_aa * stoi.C_va + stoi.f_bu_aa * stoi.C_bu +
                                                     stoi.f_pro_aa * stoi.C_pro + stoi.f_ac_aa * stoi.C_ac) +
                   stoi.Y_aa * stoi.C_bac
            ' LCFA and VFA uptake
            s(6) = -stoi.C_fa + (1.0 - stoi.Y_fa) * f_ac_fa * stoi.C_ac + stoi.Y_fa * stoi.C_bac
            s(7) = -stoi.C_va + (1.0 - stoi.Y_c4) * (f_pro_va * stoi.C_pro + f_ac_va * stoi.C_ac) +
                   stoi.Y_c4 * stoi.C_bac
            s(8) = -stoi.C_bu + (1.0 - stoi.Y_c4) * f_ac_bu * stoi.C_ac + stoi.Y_c4 * stoi.C_bac
            s(9) = -stoi.C_pro + (1.0 - stoi.Y_pro) * f_ac_pro * stoi.C_ac + stoi.Y_pro * stoi.C_bac
            ' Methanogenesis
            s(10) = -stoi.C_ac + (1.0 - stoi.Y_ac) * stoi.C_ch4 + stoi.Y_ac * stoi.C_bac
            s(11) = (1.0 - stoi.Y_h2) * stoi.C_ch4 + stoi.Y_h2 * stoi.C_bac
            ' Decay: biomass -> composites
            s(12) = -stoi.C_bac + stoi.C_xc

            Return s
        End Function

        ''' <summary>
        ''' pH inhibition envelope (Rosen &amp; Jeppsson 2006, eq. A.5): a one-sided Hill in [H+] that
        ''' falls to zero below pH_LL and saturates at 1 above pH_UL.
        ''' </summary>
        ''' <remarks>
        ''' The two-sided form of Batstone 2002 also exists, but its optimum sits at the midpoint of
        ''' pH_LL..pH_UL and it decays on BOTH sides - it reads pH_LL/pH_UL as the edges of a
        ''' tolerance band, not as the foot and shoulder of a curve. The BSM2 parameters this model
        ''' ships (pH_UL_ac = 7, pH_LL_ac = 6) are calibrated for the one-sided form below. Nothing
        ''' inhibits here at high pH, which is correct: that end is free ammonia, and I_nh3 has it.
        ''' </remarks>
        Public Function I_pH(pH As Double, pH_UL As Double, pH_LL As Double) As Double
            Dim K_pH = Pow(10.0, -0.5 * (pH_LL + pH_UL))
            Dim n = 3.0 / Max(pH_UL - pH_LL, 1.0E-09)
            Dim S_H = Pow(10.0, -pH)
            Dim kn = Pow(K_pH, n)
            Return kn / (Pow(S_H, n) + kn)
        End Function

        ''' <summary>Monod substrate limitation with floor on zero.</summary>
        Public Function I_sub(S As Double, KS As Double) As Double
            Dim ss = Max(S, 0.0)
            Return ss / (KS + ss)
        End Function

        ''' <summary>Non-competitive inhibition by high H2.</summary>
        Public Function I_h2(S_h2 As Double, K_I As Double) As Double
            Return 1.0 / (1.0 + Max(S_h2, 0.0) / Max(K_I, 1.0E-30))
        End Function

        ''' <summary>Non-competitive inhibition by free NH3.</summary>
        Public Function I_nh3(S_nh3 As Double, K_I As Double) As Double
            Return 1.0 / (1.0 + Max(S_nh3, 0.0) / Max(K_I, 1.0E-30))
        End Function

        ''' <summary>IN-limitation on all biomass uptake (prevents growth when inorganic N is depleted).</summary>
        Public Function I_IN(S_IN As Double, K_S_IN As Double) As Double
            Return 1.0 / (1.0 + Max(K_S_IN, 0.0) / Max(S_IN, 1.0E-30))
        End Function

        ''' <summary>
        ''' Algebraic charge-balance pH solver. Newton-Raphson on [H+] with bisection fallback.
        ''' Updates state.S_H_ion, state.pH and the dissociated species S_va_ion, S_bu_ion, etc.
        ''' </summary>
        Public Sub SolvePH(s As ADM1State, p As ADM1Parameters)
            Dim phys = TemperatureCorrect(p.Physicochemical)
            Dim K_w = phys.K_w

            ' Convert VFA COD concentrations (kg COD/m³) to kmol/m³ for charge balance
            ' 64 g COD / mol HAc; 112 g COD / mol HPr; 160 g COD / mol HBu; 208 g COD / mol HVa
            Dim c_va_tot = s.S_va / 208.0
            Dim c_bu_tot = s.S_bu / 160.0
            Dim c_pro_tot = s.S_pro / 112.0
            Dim c_ac_tot = s.S_ac / 64.0
            Dim c_IC = Max(s.S_IC, 0.0)
            Dim c_IN = Max(s.S_IN, 0.0)
            Dim c_IS = Max(s.S_IS, 0.0)

            Dim S_H As Double = Max(s.S_H_ion, 1.0E-12)
            Dim success As Boolean = False

            ' Newton-Raphson on charge balance f(H+) = Σcations − Σanions = 0
            For iter = 1 To 60
                Dim OH = K_w / S_H
                Dim a_va = phys.K_a_va / (phys.K_a_va + S_H)
                Dim a_bu = phys.K_a_bu / (phys.K_a_bu + S_H)
                Dim a_pro = phys.K_a_pro / (phys.K_a_pro + S_H)
                Dim a_ac = phys.K_a_ac / (phys.K_a_ac + S_H)
                Dim a_co2 = phys.K_a_co2 / (phys.K_a_co2 + S_H)
                Dim a_IN = phys.K_a_IN / (phys.K_a_IN + S_H)
                Dim a_h2s = phys.K_a_h2s / (phys.K_a_h2s + S_H)

                Dim va_i = c_va_tot * a_va
                Dim bu_i = c_bu_tot * a_bu
                Dim pro_i = c_pro_tot * a_pro
                Dim ac_i = c_ac_tot * a_ac
                Dim hco3 = c_IC * a_co2
                Dim nh4 = c_IN * (1.0 - a_IN)  ' NH4+ = total × (1 − α_NH3)
                Dim hs_i = c_IS * a_h2s

                Dim f = S_H + s.S_cat + nh4 - hco3 - ac_i - pro_i - bu_i - va_i - hs_i - OH - s.S_an

                ' Derivatives
                Dim d_a_va = -phys.K_a_va / ((phys.K_a_va + S_H) ^ 2)
                Dim d_a_bu = -phys.K_a_bu / ((phys.K_a_bu + S_H) ^ 2)
                Dim d_a_pro = -phys.K_a_pro / ((phys.K_a_pro + S_H) ^ 2)
                Dim d_a_ac = -phys.K_a_ac / ((phys.K_a_ac + S_H) ^ 2)
                Dim d_a_co2 = -phys.K_a_co2 / ((phys.K_a_co2 + S_H) ^ 2)
                Dim d_a_IN = -phys.K_a_IN / ((phys.K_a_IN + S_H) ^ 2)
                Dim d_a_h2s = -phys.K_a_h2s / ((phys.K_a_h2s + S_H) ^ 2)

                ' d(nh4)/dS_H = −c_IN·d_a_IN, since nh4 = c_IN·(1 − a_IN).
                Dim df = 1.0 + c_IN * (-d_a_IN) - c_IC * d_a_co2 - c_ac_tot * d_a_ac -
                         c_pro_tot * d_a_pro - c_bu_tot * d_a_bu - c_va_tot * d_a_va -
                         c_IS * d_a_h2s + K_w / (S_H * S_H)

                If Abs(df) < 1.0E-30 Then Exit For
                Dim dS = f / df
                Dim S_new = S_H - dS
                If S_new <= 0.0 Then S_new = S_H * 0.5
                If Abs(dS) < 1.0E-14 OrElse Abs(dS / Max(S_H, 1.0E-18)) < 1.0E-10 Then
                    S_H = S_new
                    success = True
                    Exit For
                End If
                S_H = S_new
            Next

            ' Bisection fallback if Newton didn't converge
            If Not success Then
                Dim lo = 1.0E-14
                Dim hi = 1.0E-1
                For k = 1 To 200
                    Dim mid = 0.5 * (lo + hi)
                    Dim fm = ChargeBalance(mid, s, phys)
                    Dim flo = ChargeBalance(lo, s, phys)
                    If fm = 0.0 OrElse (hi - lo) < 1.0E-15 Then
                        S_H = mid
                        Exit For
                    End If
                    If (flo < 0 AndAlso fm < 0) OrElse (flo > 0 AndAlso fm > 0) Then
                        lo = mid
                    Else
                        hi = mid
                    End If
                Next
            End If

            s.S_H_ion = Max(S_H, 1.0E-14)
            s.pH = -Log10(s.S_H_ion)

            ' Refresh dissociated-species concentrations (as VFA COD basis in state, but track kmol fields)
            Dim aa_va = phys.K_a_va / (phys.K_a_va + s.S_H_ion)
            Dim aa_bu = phys.K_a_bu / (phys.K_a_bu + s.S_H_ion)
            Dim aa_pro = phys.K_a_pro / (phys.K_a_pro + s.S_H_ion)
            Dim aa_ac = phys.K_a_ac / (phys.K_a_ac + s.S_H_ion)
            Dim aa_co2 = phys.K_a_co2 / (phys.K_a_co2 + s.S_H_ion)
            Dim aa_IN = phys.K_a_IN / (phys.K_a_IN + s.S_H_ion)
            Dim aa_h2s = phys.K_a_h2s / (phys.K_a_h2s + s.S_H_ion)

            s.S_va_ion = c_va_tot * aa_va * 208.0
            s.S_bu_ion = c_bu_tot * aa_bu * 160.0
            s.S_pro_ion = c_pro_tot * aa_pro * 112.0
            s.S_ac_ion = c_ac_tot * aa_ac * 64.0
            s.S_hco3_ion = c_IC * aa_co2
            s.S_nh3 = c_IN * aa_IN
            s.S_nh4 = c_IN - s.S_nh3
            s.S_hs_ion = c_IS * aa_h2s
        End Sub

        ''' <summary>
        ''' Charge-balance residual at a trial [H+]. Must stay identical to the residual f() inside
        ''' SolvePH's Newton loop - the bisection fallback converges on this one instead.
        ''' </summary>
        Private Function ChargeBalance(SH As Double, s As ADM1State, phys As PhysicochemicalParams) As Double
            Dim c_va = s.S_va / 208.0
            Dim c_bu = s.S_bu / 160.0
            Dim c_pro = s.S_pro / 112.0
            Dim c_ac = s.S_ac / 64.0
            Dim c_IC = Max(s.S_IC, 0.0)
            Dim c_IN = Max(s.S_IN, 0.0)
            Dim c_IS = Max(s.S_IS, 0.0)
            Dim OH = phys.K_w / SH
            Dim hco3 = c_IC * phys.K_a_co2 / (phys.K_a_co2 + SH)
            Dim nh4 = c_IN * SH / (phys.K_a_IN + SH)
            Dim va_i = c_va * phys.K_a_va / (phys.K_a_va + SH)
            Dim bu_i = c_bu * phys.K_a_bu / (phys.K_a_bu + SH)
            Dim pro_i = c_pro * phys.K_a_pro / (phys.K_a_pro + SH)
            Dim ac_i = c_ac * phys.K_a_ac / (phys.K_a_ac + SH)
            Dim hs_i = c_IS * phys.K_a_h2s / (phys.K_a_h2s + SH)
            Return SH + s.S_cat + nh4 - hco3 - ac_i - pro_i - bu_i - va_i - hs_i - OH - s.S_an
        End Function

        ''' <summary>
        ''' The dissolved-H2 mass balance reduced to a scalar function of S_h2, with every term that
        ''' does not depend on S_h2 folded into a constant.
        ''' </summary>
        ''' <remarks>
        ''' Strictly decreasing in S_h2 - every producer is non-competitively inhibited by H2, and
        ''' every sink (uptake, stripping, washout) grows with it - so the root is unique and any
        ''' bracketed method reaches it. That monotonicity is what makes the algebraic solve safe.
        ''' </remarks>
        Private NotInheritable Class H2Balance
            Public Const0 As Double     ' influent + strip-back from the headspace + uninhibited production
            Public CFa As Double, CC4 As Double, CPro As Double   ' H2-inhibited producers
            Public KIfa As Double, KIc4 As Double, KIpro As Double
            Public Upt As Double, KS As Double                    ' hydrogenotrophic uptake
            ' Hydrogenotrophic sulfate reducers, when ADM1-S is on. A second Monod sink rather than
            ' a term folded into the first: it has its own half-saturation and its own biomass, and
            ' the two only coincide by accident.
            Public UptSRB As Double, KS_SRB As Double
            Public Decay As Double                                ' dilution + stripping, both linear in S_h2

            Public Function Residual(S As Double) As Double
                Return Const0 +
                       CFa * KIfa / (KIfa + S) +
                       CC4 * KIc4 / (KIc4 + S) +
                       CPro * KIpro / (KIpro + S) -
                       Upt * S / (KS + S) -
                       UptSRB * S / (KS_SRB + S) -
                       Decay * S
            End Function

            Public Function Derivative(S As Double) As Double
                Return -CFa * KIfa / ((KIfa + S) * (KIfa + S)) -
                       CC4 * KIc4 / ((KIc4 + S) * (KIc4 + S)) -
                       CPro * KIpro / ((KIpro + S) * (KIpro + S)) -
                       Upt * KS / ((KS + S) * (KS + S)) -
                       UptSRB * KS_SRB / ((KS_SRB + S) * (KS_SRB + S)) -
                       Decay
            End Function
        End Class

        ''' <summary>
        ''' Solve the dissolved-H2 balance for the value at which it closes, i.e. the quasi-steady
        ''' state (Rosen &amp; Jeppsson 2006). See NumericsParams.AlgebraicH2 for why S_h2 is treated
        ''' this way rather than integrated.
        ''' </summary>
        ''' <remarks>
        ''' Requires a fresh pH: the inhibition envelopes are built from it. pH does not depend on
        ''' S_h2 in return (H2 carries no charge and is absent from the charge balance), so the two
        ''' algebraic solves are a sequence and not a loop.
        ''' </remarks>
        Public Function SolveH2QSS(s As ADM1State, p As ADM1Parameters, q As Double, Sin As Double()) As Double
            Dim stoi = p.Stoichiometry
            Dim k = p.Kinetics
            Dim inh = p.Inhibition
            Dim phys = TemperatureCorrect(p.Physicochemical)
            Dim D = q / Max(p.Operating.V_liq, 1.0E-09)

            Dim I_pH_aa = I_pH(s.pH, inh.pH_UL_aa, inh.pH_LL_aa)
            Dim I_pH_h2 = I_pH(s.pH, inh.pH_UL_h2, inh.pH_LL_h2)
            Dim I_N = I_IN(s.S_IN, inh.K_S_IN)
            Dim I_S = ADM1Sulfate.I_h2s_ADM1(s, p)
            Dim base_ = I_pH_aa * I_N
            Dim sum_vabu = s.S_va + s.S_bu + 1.0E-20

            Dim bal As New H2Balance()

            ' Sugar and amino-acid fermentation produce H2 but are not inhibited by it: constant here.
            Dim prod0 = stoi.f_h2_su * (1.0 - stoi.Y_su) * (k.k_m_su * I_sub(s.S_su, k.K_S_su) * s.X_su * base_) +
                        stoi.f_h2_aa * (1.0 - stoi.Y_aa) * (k.k_m_aa * I_sub(s.S_aa, k.K_S_aa) * s.X_aa * base_)

            ' Only undissolved H2 strips; the headspace partial pressure is frozen over this solve.
            Dim sEq = 16.0 * phys.K_H_h2 * (s.S_h2_gas * phys.R * phys.T_op_K / 16.0)

            bal.Const0 = D * Max(Sin(7), 0.0) + phys.k_La * sEq + prod0

            ' The acetogens carry the H2S inhibition here exactly as they do in ComputeRates: the
            ' two have to stay the same expression or the root solved for is not the root of the
            ' balance the rest of the model uses.
            bal.CFa = f_h2_fa * (1.0 - stoi.Y_fa) * (k.k_m_fa * I_sub(s.S_fa, k.K_S_fa) * s.X_fa * base_ * I_S)
            ' Valerate and butyrate degraders share one inhibition constant, so they share one term.
            bal.CC4 = f_h2_va * (1.0 - stoi.Y_c4) * (k.k_m_c4 * I_sub(s.S_va, k.K_S_c4) * s.X_c4 * (s.S_va / sum_vabu) * base_ * I_S) +
                      f_h2_bu * (1.0 - stoi.Y_c4) * (k.k_m_c4 * I_sub(s.S_bu, k.K_S_c4) * s.X_c4 * (s.S_bu / sum_vabu) * base_ * I_S)
            bal.CPro = f_h2_pro * (1.0 - stoi.Y_pro) * (k.k_m_pro * I_sub(s.S_pro, k.K_S_pro) * s.X_pro * base_ * I_S)

            bal.KIfa = Max(inh.K_I_h2_fa, 1.0E-30)
            bal.KIc4 = Max(inh.K_I_h2_c4, 1.0E-30)
            bal.KIpro = Max(inh.K_I_h2_pro, 1.0E-30)

            bal.Upt = k.k_m_h2 * Max(s.X_h2, 0.0) * I_pH_h2 * I_N * I_S
            bal.KS = Max(k.K_S_h2, 1.0E-30)
            ' Hydrogenotrophic sulfate reducers draw on the same dissolved H2. Zero unless ADM1-S
            ' is on, and then this is the term that lets them outcompete the methanogens for it.
            ADM1Sulfate.H2UptakeTerm(s, p, bal.UptSRB, bal.KS_SRB)
            bal.Decay = D + phys.k_La

            ' f(0) ≥ 0 (production and influent are non-negative, sinks vanish at S = 0), and f is
            ' driven negative by the linear sinks, so a bracket always exists with lo = 0.
            If bal.Residual(0.0) <= 0.0 Then Return 0.0

            Dim lo = 0.0
            Dim hi = Max(s.S_h2 * 4.0, 1.0E-06)
            For i = 1 To 60
                If bal.Residual(hi) <= 0.0 Then Exit For
                lo = hi
                hi *= 4.0
            Next

            ' Newton, with the bracket as a safety net: fall back to bisection on any step that
            ' leaves it or that a vanishing derivative makes meaningless.
            Dim sH2 = Min(Max(s.S_h2, lo), hi)
            If sH2 <= lo OrElse sH2 >= hi Then sH2 = 0.5 * (lo + hi)
            For i = 1 To 100
                Dim f = bal.Residual(sH2)
                If f > 0.0 Then lo = sH2 Else hi = sH2
                Dim df = bal.Derivative(sH2)
                Dim sNext As Double
                If df < 0.0 Then sNext = sH2 - f / df Else sNext = 0.5 * (lo + hi)
                If sNext <= lo OrElse sNext >= hi Then sNext = 0.5 * (lo + hi)
                Dim dS = Abs(sNext - sH2)
                sH2 = sNext
                If dS <= 1.0E-12 * Max(sH2, 1.0E-14) OrElse (hi - lo) <= 1.0E-12 * Max(sH2, 1.0E-14) Then Exit For
            Next

            Return Max(sH2, 0.0)
        End Function

        ''' <summary>
        ''' Refresh the algebraic (non-integrated) variables of a state: acid-base speciation and pH
        ''' always, plus the quasi-steady dissolved H2 when that mode is on.
        ''' </summary>
        ''' <remarks>
        ''' Every state handed back to a caller - trajectory sample, final state - has to come
        ''' through here. In algebraic-H2 mode nothing else recomputes S_h2, so a state that skips
        ''' this carries whatever S_h2 it was constructed with.
        ''' </remarks>
        Public Sub RefreshAlgebraicStates(s As ADM1State, p As ADM1Parameters, q As Double, Sin As Double())
            SolvePH(s, p)
            If p.Numerics.AlgebraicH2 Then s.S_h2 = SolveH2QSS(s, p, q, Sin)
        End Sub

        ''' <summary>
        ''' Compute the 19 ADM1 biochemical process rates at the current state.
        ''' Returns array of length 19 in standard ADM1 order:
        '''   1: disintegration, 2-4: hydrolysis (ch,pr,li), 5-11: uptake of su/aa/fa/va/bu/pro/ac/h2,
        '''   12-18: decay of X_su..X_h2, 19: kLa H2 (handled separately in GasTransfer).
        ''' </summary>
        Public Function ComputeRates(s As ADM1State, p As ADM1Parameters) As Double()
            Dim k = p.Kinetics
            Dim i = p.Inhibition
            Dim r(18) As Double

            ' pH inhibitions
            Dim I_pH_aa = I_pH(s.pH, i.pH_UL_aa, i.pH_LL_aa)
            Dim I_pH_ac = I_pH(s.pH, i.pH_UL_ac, i.pH_LL_ac)
            Dim I_pH_h2 = I_pH(s.pH, i.pH_UL_h2, i.pH_LL_h2)

            Dim I_N = I_IN(s.S_IN, i.K_S_IN)

            ' Free H2S poisons the acetogens and both methanogen groups. Returns 1.0 unless the
            ' ADM1-S sulfate extension is on, so the Batstone 2002 rates are unchanged without it.
            Dim I_S = ADM1Sulfate.I_h2s_ADM1(s, p)

            ' Common inhibition bundles
            Dim I5 = I_pH_aa * I_N
            Dim I6 = I_pH_aa * I_N * I_h2(s.S_h2, i.K_I_h2_fa) * I_S
            Dim I7 = I_pH_aa * I_N * I_h2(s.S_h2, i.K_I_h2_c4) * I_S
            Dim I8 = I_pH_aa * I_N * I_h2(s.S_h2, i.K_I_h2_c4) * I_S
            Dim I9 = I_pH_aa * I_N * I_h2(s.S_h2, i.K_I_h2_pro) * I_S
            Dim I10 = I_pH_ac * I_N * I_nh3(s.S_nh3, i.K_I_nh3) * I_S
            Dim I11 = I_pH_h2 * I_N * I_S

            ' 1: disintegration of composites
            r(0) = k.k_dis * s.X_c
            ' 2,3,4: hydrolysis
            r(1) = k.k_hyd_ch * s.X_ch
            r(2) = k.k_hyd_pr * s.X_pr
            r(3) = k.k_hyd_li * s.X_li
            ' 5: sugar uptake
            r(4) = k.k_m_su * I_sub(s.S_su, k.K_S_su) * s.X_su * I5
            ' 6: aa uptake
            r(5) = k.k_m_aa * I_sub(s.S_aa, k.K_S_aa) * s.X_aa * I5
            ' 7: fa uptake
            r(6) = k.k_m_fa * I_sub(s.S_fa, k.K_S_fa) * s.X_fa * I6
            ' 8: va uptake (butyrate-valerate degraders X_c4)
            Dim sum_vabu = s.S_va + s.S_bu + 1.0E-20
            r(7) = k.k_m_c4 * I_sub(s.S_va, k.K_S_c4) * s.X_c4 * (s.S_va / sum_vabu) * I7
            ' 9: bu uptake
            r(8) = k.k_m_c4 * I_sub(s.S_bu, k.K_S_c4) * s.X_c4 * (s.S_bu / sum_vabu) * I8
            ' 10: pro uptake
            r(9) = k.k_m_pro * I_sub(s.S_pro, k.K_S_pro) * s.X_pro * I9
            ' 11: ac uptake
            r(10) = k.k_m_ac * I_sub(s.S_ac, k.K_S_ac) * s.X_ac * I10
            ' 12: h2 uptake
            r(11) = k.k_m_h2 * I_sub(s.S_h2, k.K_S_h2) * s.X_h2 * I11
            ' 13-19: decay
            r(12) = k.k_dec_X_su * s.X_su
            r(13) = k.k_dec_X_aa * s.X_aa
            r(14) = k.k_dec_X_fa * s.X_fa
            r(15) = k.k_dec_X_c4 * s.X_c4
            r(16) = k.k_dec_X_pro * s.X_pro
            r(17) = k.k_dec_X_ac * s.X_ac
            r(18) = k.k_dec_X_h2 * s.X_h2

            Return r
        End Function

        ''' <summary>
        ''' Headspace partial pressures (bar) from the gas-phase state. Single source of truth for
        ''' every caller that needs them - GasTransfer, GasOutflow and the mole fractions.
        ''' </summary>
        ''' <remarks>
        ''' S_h2_gas and S_ch4_gas are in kg COD/m³ and convert to kmol/m³ by /16 and /64
        ''' (16 g COD/mol H2, 64 g COD/mol CH4). S_co2_gas and S_h2s_gas are already in kmol/m³.
        ''' </remarks>
        Private Sub PartialPressures(s As ADM1State, p As ADM1Parameters,
                                     ByRef p_h2 As Double, ByRef p_ch4 As Double,
                                     ByRef p_co2 As Double, ByRef p_h2s As Double)
            Dim RT = p.Physicochemical.R * p.Physicochemical.T_op_K
            p_h2 = s.S_h2_gas * RT / 16.0
            p_ch4 = s.S_ch4_gas * RT / 64.0
            p_co2 = s.S_co2_gas * RT
            p_h2s = s.S_h2s_gas * RT
        End Sub

        ''' <summary>Gas-liquid transfer rates (kLa × (S_liq − K_H·p_gas)).</summary>
        Public Sub GasTransfer(s As ADM1State, p As ADM1Parameters, ByRef rT_h2 As Double, ByRef rT_ch4 As Double,
                               ByRef rT_co2 As Double, ByRef rT_h2s As Double)
            Dim phys = TemperatureCorrect(p.Physicochemical)
            Dim p_h2, p_ch4, p_co2, p_h2s As Double
            PartialPressures(s, p, p_h2, p_ch4, p_co2, p_h2s)

            rT_h2 = phys.k_La * (s.S_h2 - 16.0 * phys.K_H_h2 * p_h2)
            rT_ch4 = phys.k_La * (s.S_ch4 - 64.0 * phys.K_H_ch4 * p_ch4)
            rT_co2 = phys.k_La * ((s.S_IC - s.S_hco3_ion) - phys.K_H_co2 * p_co2)
            ' Only undissociated H2S strips, exactly as only free CO2 does above.
            rT_h2s = phys.k_La * ((s.S_IS - s.S_hs_ion) - phys.K_H_h2s * p_h2s)
        End Sub

        ''' <summary>
        ''' Compute derivative vector (31 entries) for ODE integration.
        ''' Influent concentrations Sin are provided in the same 31-vector order as state.ToVector().
        ''' q is influent volumetric flow (m³/d); V_liq, V_gas in m³.
        ''' </summary>
        Public Function ComputeDerivatives(s As ADM1State, p As ADM1Parameters, q As Double, Sin As Double()) As Double()
            Dim stoi = p.Stoichiometry
            Dim V_liq = p.Operating.V_liq
            Dim V_gas = p.Operating.V_gas
            Dim D = q / Max(V_liq, 1.0E-09)

            ' Make sure pH + dissociated species are fresh, and - in algebraic-H2 mode - that S_h2
            ' holds its quasi-steady value before any rate that depends on it is evaluated.
            Dim algebraicH2 = p.Numerics.AlgebraicH2
            SolvePH(s, p)
            If algebraicH2 Then s.S_h2 = SolveH2QSS(s, p, q, Sin)

            Dim r = ComputeRates(s, p)
            Dim rT_h2 = 0.0, rT_ch4 = 0.0, rT_co2 = 0.0, rT_h2s = 0.0
            GasTransfer(s, p, rT_h2, rT_ch4, rT_co2, rT_h2s)

            ' The seven decays share one coefficient in both the carbon and the nitrogen balance.
            Dim decay_total = r(12) + r(13) + r(14) + r(15) + r(16) + r(17) + r(18)

            Dim d_(ADM1State.NDynamic - 1) As Double

            ' ---- Soluble species ----
            ' S_su: from carbohydrate hydrolysis (r2) plus the glycerol backbone of lipid hydrolysis
            ' (the 1 - f_fa_li that does not leave as LCFA), consumed by r5. Dropping that second
            ' term is what used to make 5% of every hydrolysed lipid's COD disappear.
            d_(0) = D * (Sin(0) - s.S_su) + r(1) + (1.0 - stoi.f_fa_li) * r(3) - r(4)
            ' S_aa: from hydrolysis of proteins, consumed by r6
            d_(1) = D * (Sin(1) - s.S_aa) + r(2) - r(5)
            ' S_fa: fraction f_fa_li of lipid hydrolysis, consumed by r7
            d_(2) = D * (Sin(2) - s.S_fa) + stoi.f_fa_li * r(3) - r(6)
            ' S_va: from aa fermentation × f_va_aa, consumed by r8
            d_(3) = D * (Sin(3) - s.S_va) + stoi.f_va_aa * (1.0 - stoi.Y_aa) * r(5) - r(7)
            ' S_bu: from sugar r5*f_bu_su*(1-Y_su) + aa r6*f_bu_aa*(1-Y_aa), consumed by r9
            d_(4) = D * (Sin(4) - s.S_bu) + stoi.f_bu_su * (1.0 - stoi.Y_su) * r(4) + stoi.f_bu_aa * (1.0 - stoi.Y_aa) * r(5) - r(8)
            ' S_pro: r5 + r6 + from valerate degradation (0.54 stoich of r8), consumed by r10
            d_(5) = D * (Sin(5) - s.S_pro) + stoi.f_pro_su * (1.0 - stoi.Y_su) * r(4) + stoi.f_pro_aa * (1.0 - stoi.Y_aa) * r(5) + f_pro_va * (1.0 - stoi.Y_c4) * r(7) - r(9)
            ' S_ac: from sugar, aa, LCFA, valerate, butyrate and propionate uptake; consumed by r11
            d_(6) = D * (Sin(6) - s.S_ac) +
                    stoi.f_ac_su * (1.0 - stoi.Y_su) * r(4) +
                    stoi.f_ac_aa * (1.0 - stoi.Y_aa) * r(5) +
                    f_ac_fa * (1.0 - stoi.Y_fa) * r(6) +
                    f_ac_va * (1.0 - stoi.Y_c4) * r(7) +
                    f_ac_bu * (1.0 - stoi.Y_c4) * r(8) +
                    f_ac_pro * (1.0 - stoi.Y_pro) * r(9) - r(10)
            ' S_h2: produced by sugar/aa/fa/va/bu/pro; consumed by r12; kLa loss
            d_(7) = D * (Sin(7) - s.S_h2) +
                    stoi.f_h2_su * (1.0 - stoi.Y_su) * r(4) +
                    stoi.f_h2_aa * (1.0 - stoi.Y_aa) * r(5) +
                    f_h2_fa * (1.0 - stoi.Y_fa) * r(6) +
                    f_h2_va * (1.0 - stoi.Y_c4) * r(7) +
                    f_h2_bu * (1.0 - stoi.Y_c4) * r(8) +
                    f_h2_pro * (1.0 - stoi.Y_pro) * r(9) - r(11) - rT_h2
            ' SolveH2QSS closed that balance already, so S_h2 must not be advanced by the RK step on
            ' top of it. Zeroing rather than leaving the (residual-sized) value keeps the solver's
            ' error norm from chasing the H2 root-finding tolerance.
            If algebraicH2 Then d_(7) = 0.0
            ' S_ch4: produced by r11 (ac→ch4) and r12 (h2→ch4); kLa loss
            d_(8) = D * (Sin(8) - s.S_ch4) + (1.0 - stoi.Y_ac) * r(10) + (1.0 - stoi.Y_h2) * r(11) - rT_ch4
            ' S_IC: carbon balance over all 19 processes plus stripping. Each s_j is the net carbon
            ' the process takes out of the inorganic pool, so the sum is subtracted.
            Dim sC = CarbonBalance(stoi)
            Dim cNet = sC(0) * r(0) + sC(1) * r(1) + sC(2) * r(2) + sC(3) * r(3) +
                       sC(4) * r(4) + sC(5) * r(5) + sC(6) * r(6) + sC(7) * r(7) +
                       sC(8) * r(8) + sC(9) * r(9) + sC(10) * r(10) + sC(11) * r(11) +
                       sC(12) * decay_total
            d_(9) = D * (Sin(9) - s.S_IC) - cNet - rT_co2
            ' S_IN: nitrogen balance. Disintegration redistributes the composites' N between inerts
            ' and amino acids; uptake builds it into biomass; decay hands the surplus back, because
            ' biomass is richer in N than composites are - that last term is what makes ammonia climb
            ' in a lightly loaded digester.
            Dim Ynet = stoi.Y_su * r(4) + stoi.Y_aa * r(5) + stoi.Y_fa * r(6) + stoi.Y_c4 * (r(7) + r(8)) + stoi.Y_pro * r(9) + stoi.Y_ac * r(10) + stoi.Y_h2 * r(11)
            Dim nDis = stoi.N_xc - stoi.f_xI_xc * stoi.N_I - stoi.f_sI_xc * stoi.N_I - stoi.f_pr_xc * stoi.N_aa
            d_(10) = D * (Sin(10) - s.S_IN) + nDis * r(0) + stoi.N_aa * r(5) - stoi.N_bac * Ynet +
                     (stoi.N_bac - stoi.N_xc) * decay_total
            ' S_I: produced by disintegration
            d_(11) = D * (Sin(11) - s.S_I) + stoi.f_sI_xc * r(0)

            ' ---- Particulates ----
            ' X_c: consumed by disintegration, gains from all decays
            d_(12) = D * (Sin(12) - s.X_c) - r(0) + decay_total
            ' X_ch: produced by disintegration × f_ch_xc, consumed by hydrolysis r2
            d_(13) = D * (Sin(13) - s.X_ch) + stoi.f_ch_xc * r(0) - r(1)
            ' X_pr
            d_(14) = D * (Sin(14) - s.X_pr) + stoi.f_pr_xc * r(0) - r(2)
            ' X_li
            d_(15) = D * (Sin(15) - s.X_li) + stoi.f_li_xc * r(0) - r(3)
            ' Biomass growth and decay
            d_(16) = D * (Sin(16) - s.X_su) + stoi.Y_su * r(4) - r(12)
            d_(17) = D * (Sin(17) - s.X_aa) + stoi.Y_aa * r(5) - r(13)
            d_(18) = D * (Sin(18) - s.X_fa) + stoi.Y_fa * r(6) - r(14)
            d_(19) = D * (Sin(19) - s.X_c4) + stoi.Y_c4 * (r(7) + r(8)) - r(15)
            d_(20) = D * (Sin(20) - s.X_pro) + stoi.Y_pro * r(9) - r(16)
            d_(21) = D * (Sin(21) - s.X_ac) + stoi.Y_ac * r(10) - r(17)
            d_(22) = D * (Sin(22) - s.X_h2) + stoi.Y_h2 * r(11) - r(18)
            ' X_I: produced by disintegration × f_xI_xc
            d_(23) = D * (Sin(23) - s.X_I) + stoi.f_xI_xc * r(0)

            ' Cations/anions just flow through
            d_(24) = D * (Sin(24) - s.S_cat)
            d_(25) = D * (Sin(25) - s.S_an)

            ' S_IS: sulfide enters already mineralised with the influent (the sulfate-reduction COD
            ' debit is applied upstream, when Sin is built) and leaves by washout or stripping.
            ' There is no in-reactor source in ADM1 proper; ADM1-S adds one below.
            d_(29) = D * (Sin(29) - s.S_IS) - rT_h2s

            ' ---- Sulfate reduction (ADM1-S) ----
            ' Layered on afterwards so the block above stays the Batstone 2002 model: with the
            ' extension off this call returns without touching a single derivative.
            ADM1Sulfate.ApplyDerivatives(d_, s, p, D, Sin, algebraicH2)

            ' ---- Gas phase ----
            Dim q_gas = GasOutflow(s, p)
            ' V_liq*rT − q_gas*S_gas
            d_(26) = -s.S_h2_gas * q_gas / V_gas + rT_h2 * V_liq / V_gas
            d_(27) = -s.S_ch4_gas * q_gas / V_gas + rT_ch4 * V_liq / V_gas
            d_(28) = -s.S_co2_gas * q_gas / V_gas + rT_co2 * V_liq / V_gas
            d_(30) = -s.S_h2s_gas * q_gas / V_gas + rT_h2s * V_liq / V_gas

            Return d_
        End Function

        ''' <summary>Biogas volumetric outflow from pressure balance (m³/d).</summary>
        Public Function GasOutflow(s As ADM1State, p As ADM1Parameters) As Double
            Dim phys = TemperatureCorrect(p.Physicochemical)
            Dim p_h2, p_ch4, p_co2, p_h2s As Double
            PartialPressures(s, p, p_h2, p_ch4, p_co2, p_h2s)
            Dim P_total = p_h2 + p_ch4 + p_co2 + p_h2s + phys.P_gas_h2o
            Return Max(phys.k_P * (P_total - phys.P_atm), 0.0) * P_total / phys.P_atm
        End Function

        ''' <summary>Biogas flow rate in Nm³/d (at standard conditions 273.15 K, 1.013 bar).</summary>
        Public Function BiogasFlow_Nm3_d(s As ADM1State, p As ADM1Parameters) As Double
            Dim q = GasOutflow(s, p)
            Dim phys = TemperatureCorrect(p.Physicochemical)
            Return q * (273.15 / phys.T_op_K)
        End Function

        ''' <summary>
        ''' Sum of the biogas partial pressures on a dry basis (water vapour excluded) and, via the
        ''' ByRef arguments, the individual pressures. Shared by every mole fraction so they cannot
        ''' disagree on the denominator - the four of them must always sum to 1.
        ''' </summary>
        Private Function DryGasTotal(s As ADM1State, p As ADM1Parameters,
                                     ByRef p_h2 As Double, ByRef p_ch4 As Double,
                                     ByRef p_co2 As Double, ByRef p_h2s As Double) As Double
            PartialPressures(s, p, p_h2, p_ch4, p_co2, p_h2s)
            Return p_h2 + p_ch4 + p_co2 + p_h2s
        End Function

        ''' <summary>CH4 mole fraction in biogas (dry basis).</summary>
        Public Function CH4MoleFraction(s As ADM1State, p As ADM1Parameters) As Double
            Dim p_h2, p_ch4, p_co2, p_h2s As Double
            Dim tot = DryGasTotal(s, p, p_h2, p_ch4, p_co2, p_h2s)
            If tot <= 0.0 Then Return 0.0
            Return p_ch4 / tot
        End Function

        ''' <summary>CO2 mole fraction in biogas (dry basis).</summary>
        Public Function CO2MoleFraction(s As ADM1State, p As ADM1Parameters) As Double
            Dim p_h2, p_ch4, p_co2, p_h2s As Double
            Dim tot = DryGasTotal(s, p, p_h2, p_ch4, p_co2, p_h2s)
            If tot <= 0.0 Then Return 0.0
            Return p_co2 / tot
        End Function

        ''' <summary>H2 mole fraction in biogas (dry basis).</summary>
        Public Function H2MoleFraction(s As ADM1State, p As ADM1Parameters) As Double
            Dim p_h2, p_ch4, p_co2, p_h2s As Double
            Dim tot = DryGasTotal(s, p, p_h2, p_ch4, p_co2, p_h2s)
            If tot <= 0.0 Then Return 0.0
            Return p_h2 / tot
        End Function

        ''' <summary>H2S mole fraction in biogas (dry basis).</summary>
        Public Function H2SMoleFraction(s As ADM1State, p As ADM1Parameters) As Double
            Dim p_h2, p_ch4, p_co2, p_h2s As Double
            Dim tot = DryGasTotal(s, p, p_h2, p_ch4, p_co2, p_h2s)
            If tot <= 0.0 Then Return 0.0
            Return p_h2s / tot
        End Function

    End Module

End Namespace
