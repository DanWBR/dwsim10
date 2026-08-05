'    ADM1-S - Sulfate reduction extension: SRB kinetics, H2S inhibition, derivative overlay.
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Math

Namespace Reactors.ADM1

    ''' <summary>
    ''' Sulfate reduction layered on top of ADM1 (ADM1-S), after Fedorovich et al. 2003 and
    ''' Barrera et al. 2015. Standard ADM1 excludes it entirely, so everything here is additive:
    ''' four sulfate-reducing populations that take hydrogen, acetate, propionate and butyrate
    ''' away from the ADM1 groups, and the free H2S they produce, which inhibits both.
    ''' </summary>
    ''' <remarks>
    ''' The whole module is inert unless ADM1Parameters.Sulfate.Enabled is set. That is what lets
    ''' ADM1-Full stay the Batstone 2002 benchmark while ADM1-S is a separate selectable model
    ''' rather than a second copy of the code.
    '''
    ''' The four reactions, in the COD units the rest of the model uses:
    '''   4 H2      + SO4(2-)  -> HS- + 4 H2O            64 kg COD of H2 per kmol S
    '''   acetate   + SO4(2-)  -> 2 HCO3- + HS-          64 kg COD of acetate per kmol S
    '''   4 propionate + 3 SO4(2-) -> 4 acetate + 4 HCO3- + 3 HS-
    '''   2 butyrate   +   SO4(2-) -> 4 acetate + HS-
    ''' The last two are incomplete oxidisers: they stop at acetate and hand the rest of the
    ''' electrons on. Splitting each donor's COD between the sulfide and the acetate it becomes is
    ''' the only stoichiometry this needs, and both splits below are exact.
    ''' </remarks>
    Public Module ADM1Sulfate

        ''' <summary>Rate-array index of each of the four uptake processes.</summary>
        Public Const R_H2 As Integer = 0
        Public Const R_AC As Integer = 1
        Public Const R_PRO As Integer = 2
        Public Const R_BU As Integer = 3
        ''' <summary>Rate-array index of each of the four decays.</summary>
        Public Const R_DEC_H2 As Integer = 4
        Public Const R_DEC_AC As Integer = 5
        Public Const R_DEC_PRO As Integer = 6
        Public Const R_DEC_BU As Integer = 7
        Public Const NRates As Integer = 8

        ' Fraction of each donor's catabolised COD that ends up as acetate rather than sulfide.
        ' Propionate is 112 kg COD/kmol and yields 1 kmol acetate (64) per kmol, leaving 48 to the
        ' sulfate. Butyrate is 160 and yields 2 kmol acetate (128), leaving 32.
        Private Const f_ac_pro_srb As Double = 64.0 / 112.0
        Private Const f_ac_bu_srb As Double = 128.0 / 160.0

        ''' <summary>
        ''' Non-competitive inhibition by undissociated H2S. Only the free acid crosses the cell
        ''' membrane, so the inhibiting concentration is S_IS minus the HS- that SolvePH speciated
        ''' out of it - which makes the whole effect swing with pH.
        ''' </summary>
        Public Function I_h2s(s As ADM1State, K_I As Double) As Double
            If K_I <= 0.0 Then Return 1.0
            Dim free = Max(s.S_IS - s.S_hs_ion, 0.0)
            Return 1.0 / (1.0 + free / K_I)
        End Function

        ''' <summary>
        ''' Inhibition factor the ADM1 groups see. Returns 1.0 with sulfate reduction switched
        ''' off, which is what keeps ADM1-Full's rates untouched.
        ''' </summary>
        Public Function I_h2s_ADM1(s As ADM1State, p As ADM1Parameters) As Double
            If Not p.Sulfate.Enabled Then Return 1.0
            Return I_h2s(s, p.Sulfate.K_I_h2s)
        End Function

        ''' <summary>
        ''' The eight sulfate-reduction process rates (kg COD/m³/d) at the current state:
        ''' four uptakes then four decays. All zero when the extension is off.
        ''' </summary>
        ''' <remarks>
        ''' Each uptake is double-Monod - on the electron donor and on sulfate - times the same pH
        ''' and inorganic-nitrogen envelopes the ADM1 groups use, times the reducers' own (weaker)
        ''' H2S inhibition. Sulfate limitation is the term that hands the substrate back to the
        ''' methanogens once the sulfate runs out.
        ''' </remarks>
        Public Function ComputeRates(s As ADM1State, p As ADM1Parameters) As Double()
            Dim r(NRates - 1) As Double
            If Not p.Sulfate.Enabled Then Return r

            Dim sf = p.Sulfate
            Dim inh = p.Inhibition

            ' Acetogens and hydrogenotrophs sit under the aa/h2 pH envelopes respectively; the
            ' acetotrophic reducers share the acetoclastic one.
            Dim I_N = ADM1Equations.I_IN(s.S_IN, inh.K_S_IN)
            Dim I_pH_aa = ADM1Equations.I_pH(s.pH, inh.pH_UL_aa, inh.pH_LL_aa)
            Dim I_pH_ac = ADM1Equations.I_pH(s.pH, inh.pH_UL_ac, inh.pH_LL_ac)
            Dim I_pH_h2 = ADM1Equations.I_pH(s.pH, inh.pH_UL_h2, inh.pH_LL_h2)
            Dim I_S = I_h2s(s, sf.K_I_h2s_srb)

            Dim so4 = Max(s.S_so4, 0.0)

            r(R_H2) = sf.k_m_srb_h2 * ADM1Equations.I_sub(s.S_h2, sf.K_S_srb_h2) *
                      ADM1Equations.I_sub(so4, sf.K_S_so4_h2) * Max(s.X_srb_h2, 0.0) *
                      I_pH_h2 * I_N * I_S
            r(R_AC) = sf.k_m_srb_ac * ADM1Equations.I_sub(s.S_ac, sf.K_S_srb_ac) *
                      ADM1Equations.I_sub(so4, sf.K_S_so4_ac) * Max(s.X_srb_ac, 0.0) *
                      I_pH_ac * I_N * I_S
            r(R_PRO) = sf.k_m_srb_pro * ADM1Equations.I_sub(s.S_pro, sf.K_S_srb_pro) *
                       ADM1Equations.I_sub(so4, sf.K_S_so4_pro) * Max(s.X_srb_pro, 0.0) *
                       I_pH_aa * I_N * I_S
            r(R_BU) = sf.k_m_srb_bu * ADM1Equations.I_sub(s.S_bu, sf.K_S_srb_bu) *
                      ADM1Equations.I_sub(so4, sf.K_S_so4_bu) * Max(s.X_srb_bu, 0.0) *
                      I_pH_aa * I_N * I_S

            r(R_DEC_H2) = sf.k_dec_srb_h2 * Max(s.X_srb_h2, 0.0)
            r(R_DEC_AC) = sf.k_dec_srb_ac * Max(s.X_srb_ac, 0.0)
            r(R_DEC_PRO) = sf.k_dec_srb_pro * Max(s.X_srb_pro, 0.0)
            r(R_DEC_BU) = sf.k_dec_srb_bu * Max(s.X_srb_bu, 0.0)

            Return r
        End Function

        ''' <summary>
        ''' Sulfate reduced by each uptake process (kmol S/m³/d). The catabolised COD - what is
        ''' left of the donor after the yield - is split between acetate and sulfide, and only the
        ''' sulfide share draws on sulfate, at 64 kg COD per kmol S.
        ''' </summary>
        Public Function SulfateReduced(r As Double(), p As ADM1Parameters) As Double()
            Dim sf = p.Sulfate
            Dim q(3) As Double
            q(R_H2) = (1.0 - sf.Y_srb_h2) * r(R_H2) / ADM1State.COD_per_kmol_S
            q(R_AC) = (1.0 - sf.Y_srb_ac) * r(R_AC) / ADM1State.COD_per_kmol_S
            q(R_PRO) = (1.0 - sf.Y_srb_pro) * r(R_PRO) * (1.0 - f_ac_pro_srb) / ADM1State.COD_per_kmol_S
            q(R_BU) = (1.0 - sf.Y_srb_bu) * r(R_BU) * (1.0 - f_ac_bu_srb) / ADM1State.COD_per_kmol_S
            Return q
        End Function

        ''' <summary>
        ''' Extra hydrogen sink the sulfate reducers put into the dissolved-H2 balance, expressed
        ''' as the Monod pair (maximum uptake, half-saturation) the QSS solver needs.
        ''' </summary>
        ''' <remarks>
        ''' Has to go into the algebraic solve rather than be subtracted afterwards: S_h2 is not
        ''' integrated, it is the root of its own balance, and a sink left out of that balance is
        ''' a sink the model never applies.
        ''' </remarks>
        Public Sub H2UptakeTerm(s As ADM1State, p As ADM1Parameters,
                                ByRef upt As Double, ByRef KS As Double)
            upt = 0.0
            KS = 1.0
            If Not p.Sulfate.Enabled Then Return

            Dim sf = p.Sulfate
            Dim inh = p.Inhibition
            upt = sf.k_m_srb_h2 * Max(s.X_srb_h2, 0.0) *
                  ADM1Equations.I_sub(Max(s.S_so4, 0.0), sf.K_S_so4_h2) *
                  ADM1Equations.I_pH(s.pH, inh.pH_UL_h2, inh.pH_LL_h2) *
                  ADM1Equations.I_IN(s.S_IN, inh.K_S_IN) *
                  I_h2s(s, sf.K_I_h2s_srb)
            KS = Max(sf.K_S_srb_h2, 1.0E-30)
        End Sub

        ''' <summary>
        ''' Net carbon each uptake process takes out of the inorganic-carbon pool
        ''' (kmol C per kg COD of donor), in the same sign convention as ADM1Equations.CarbonBalance:
        ''' positive consumes S_IC.
        ''' </summary>
        ''' <remarks>
        ''' The hydrogenotrophic reducers come out positive - they have no organic carbon at all, so
        ''' every carbon in their biomass is fixed from CO2. The other three are negative: they
        ''' mineralise more carbon than they build.
        ''' </remarks>
        Public Function CarbonBalance(p As ADM1Parameters) As Double()
            Dim sf = p.Sulfate
            Dim st = p.Stoichiometry
            Dim c(3) As Double
            c(R_H2) = sf.Y_srb_h2 * st.C_bac
            c(R_AC) = -st.C_ac + sf.Y_srb_ac * st.C_bac
            c(R_PRO) = -st.C_pro + (1.0 - sf.Y_srb_pro) * f_ac_pro_srb * st.C_ac +
                       sf.Y_srb_pro * st.C_bac
            c(R_BU) = -st.C_bu + (1.0 - sf.Y_srb_bu) * f_ac_bu_srb * st.C_ac +
                      sf.Y_srb_bu * st.C_bac
            Return c
        End Function

        ''' <summary>
        ''' Add every sulfate-reduction contribution to a derivative vector the ADM1 core has
        ''' already filled in. Does nothing when the extension is off.
        ''' </summary>
        ''' <param name="d_">Derivative vector of length ADM1State.NDynamic, modified in place.</param>
        ''' <param name="algebraicH2">When set, d_(7) stays whatever the core left it at (zero):
        ''' the QSS solve already accounted for the reducers' hydrogen through H2UptakeTerm.</param>
        Public Sub ApplyDerivatives(d_ As Double(), s As ADM1State, p As ADM1Parameters,
                                    D As Double, Sin As Double(), algebraicH2 As Boolean)

            If Not p.Sulfate.Enabled Then Return

            Dim sf = p.Sulfate
            Dim st = p.Stoichiometry
            Dim r = ComputeRates(s, p)
            Dim qSO4 = SulfateReduced(r, p)
            Dim cS = CarbonBalance(p)

            Dim so4Total = qSO4(R_H2) + qSO4(R_AC) + qSO4(R_PRO) + qSO4(R_BU)
            Dim decayTotal = r(R_DEC_H2) + r(R_DEC_AC) + r(R_DEC_PRO) + r(R_DEC_BU)

            ' ---- Donors ----
            ' S_h2 (7): only when it is being integrated. In algebraic mode the sink is already
            ' inside the root SolveH2QSS found, and adding it again would double-count it.
            If Not algebraicH2 Then d_(7) -= r(R_H2)
            ' S_ac (6): consumed by the acetotrophic reducers, produced by the two incomplete
            ' oxidisers, which is exactly why sulfate can raise acetate instead of lowering it.
            d_(6) += (1.0 - sf.Y_srb_pro) * f_ac_pro_srb * r(R_PRO) +
                     (1.0 - sf.Y_srb_bu) * f_ac_bu_srb * r(R_BU) - r(R_AC)
            ' S_pro (5), S_bu (4)
            d_(5) -= r(R_PRO)
            d_(4) -= r(R_BU)

            ' ---- Inorganic carbon (9) ----
            ' Same sign convention as ADM1Equations.CarbonBalance and the core's d_(9): a positive
            ' coefficient takes carbon out of the inorganic pool, so it is subtracted.
            d_(9) -= cS(R_H2) * r(R_H2) + cS(R_AC) * r(R_AC) +
                     cS(R_PRO) * r(R_PRO) + cS(R_BU) * r(R_BU)
            ' Decay returns SRB biomass to composites on the ADM1 decay coefficient, s = C_xc -
            ' C_bac. Biomass is the richer of the two, so this releases carbon rather than fixing it.
            d_(9) -= (st.C_xc - st.C_bac) * decayTotal

            ' ---- Inorganic nitrogen (10) ----
            Dim Ynet = sf.Y_srb_h2 * r(R_H2) + sf.Y_srb_ac * r(R_AC) +
                       sf.Y_srb_pro * r(R_PRO) + sf.Y_srb_bu * r(R_BU)
            d_(10) += -st.N_bac * Ynet + (st.N_bac - st.N_xc) * decayTotal

            ' ---- Composites (12): SRB decay feeds the same pool as every other decay ----
            d_(12) += decayTotal

            ' ---- Anions (25) ----
            ' Sulfate carries two negative charges and sulfide, at digester pH, mostly one. Losing
            ' the difference is the alkalinity that sulfate reduction generates; S_IS picks the
            ' sulfide up below and SolvePH charges it as HS- from there.
            d_(25) -= 2.0 * so4Total

            ' ---- Sulfide (29) and sulfate (31) ----
            d_(29) += so4Total
            d_(31) = D * (Max(Sin(31), 0.0) - s.S_so4) - so4Total

            ' ---- SRB populations (32-35) ----
            d_(32) = D * (Sin(32) - s.X_srb_h2) + sf.Y_srb_h2 * r(R_H2) - r(R_DEC_H2)
            d_(33) = D * (Sin(33) - s.X_srb_ac) + sf.Y_srb_ac * r(R_AC) - r(R_DEC_AC)
            d_(34) = D * (Sin(34) - s.X_srb_pro) + sf.Y_srb_pro * r(R_PRO) - r(R_DEC_PRO)
            d_(35) = D * (Sin(35) - s.X_srb_bu) + sf.Y_srb_bu * r(R_BU) - r(R_DEC_BU)

        End Sub

    End Module

End Namespace
