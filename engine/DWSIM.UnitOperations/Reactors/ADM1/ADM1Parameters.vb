'    Full ADM1 (Batstone et al. 2002) - Parameters
'    Defaults per Rosen & Jeppsson 2006, BSM2 benchmark (Table A.1)
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Collections.Generic
Imports System.Math

Namespace Reactors.ADM1

    ''' <summary>
    ''' Full parameter set for Batstone-2002 ADM1. Grouped into nested classes for editor tabs.
    ''' Defaults are the BSM2 benchmark values (Rosen &amp; Jeppsson 2006).
    ''' </summary>
    <Serializable()> Public Class ADM1Parameters

        Public Property Stoichiometry As New StoichiometryParams()
        Public Property Kinetics As New KineticsParams()
        Public Property Inhibition As New InhibitionParams()
        Public Property Physicochemical As New PhysicochemicalParams()
        Public Property InitialConditions As New ADM1State()
        Public Property Operating As New OperatingParams()

        Private _numerics As NumericsParams = Nothing
        Private _sulfate As SulfateParams = Nothing

        ''' <summary>
        ''' Solver settings. Lazily created so parameter sets deserialised from a JSON blob written
        ''' before this block existed still get the defaults instead of a null reference.
        ''' </summary>
        Public Property Numerics As NumericsParams
            Get
                If _numerics Is Nothing Then _numerics = New NumericsParams()
                Return _numerics
            End Get
            Set(value As NumericsParams)
                _numerics = value
            End Set
        End Property

        ''' <summary>
        ''' Sulfate-reduction extension (ADM1-S). Lazily created for the same reason as Numerics,
        ''' and disabled by default: an ADM1-Full parameter set that never touches this block runs
        ''' the Batstone 2002 model unchanged.
        ''' </summary>
        Public Property Sulfate As SulfateParams
            Get
                If _sulfate Is Nothing Then _sulfate = New SulfateParams()
                Return _sulfate
            End Get
            Set(value As SulfateParams)
                _sulfate = value
            End Set
        End Property

        Public Sub ResetToBenchmark()
            Stoichiometry = New StoichiometryParams()
            Kinetics = New KineticsParams()
            Inhibition = New InhibitionParams()
            Physicochemical = New PhysicochemicalParams()
            InitialConditions = New ADM1State()
            Operating = New OperatingParams()
            Numerics = New NumericsParams()
            Sulfate = New SulfateParams()
        End Sub

        Public Function ToJSON() As String
            Return Newtonsoft.Json.JsonConvert.SerializeObject(Me, Newtonsoft.Json.Formatting.Indented)
        End Function

        Public Shared Function FromJSON(s As String) As ADM1Parameters
            If String.IsNullOrWhiteSpace(s) Then Return New ADM1Parameters()
            Try
                Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of ADM1Parameters)(s)
            Catch
                Return New ADM1Parameters()
            End Try
        End Function

    End Class

    ''' <summary>Numerical settings for the ODE integration.</summary>
    <Serializable()> Public Class NumericsParams

        ''' <summary>
        ''' Solve dissolved H2 from its own mass balance at every step instead of integrating it
        ''' (the DAE form of Rosen &amp; Jeppsson 2006).
        ''' </summary>
        ''' <remarks>
        ''' S_h2 sits at ~2.5e-7 kg COD/m³ against a half-saturation constant of 7e-6, which puts
        ''' the slope of its uptake term near 1.5e6 /d - a 0.06-second time constant inside a model
        ''' whose next-fastest mode is minutes and whose retention time is weeks. An explicit RK45
        ''' is stable only for |h·λ| ≲ 3, so integrating S_h2 pins the step at h ~ 2e-6 d whatever
        ''' the error tolerance, and the run never gets near the horizon. Solving it algebraically
        ''' deletes that eigenvalue and costs nothing in accuracy: the mode is so fast it is at its
        ''' quasi-steady value between any two reportable instants anyway.
        ''' Set False only to reproduce the fully-explicit ODE form.
        ''' </remarks>
        Public Property AlgebraicH2 As Boolean = True

        ''' <summary>Step budget (accepted + rejected) before the integrator gives up and says so.</summary>
        Public Property MaxSteps As Integer = 2000000

    End Class

    ''' <summary>
    ''' Sulfate-reduction extension of ADM1 (ADM1-S), after Fedorovich et al. 2003 and Barrera et
    ''' al. 2015. Four sulfate-reducing populations compete with the standard ADM1 groups for
    ''' hydrogen, acetate, propionate and butyrate, and the free H2S they make inhibits both them
    ''' and the methanogens.
    ''' </summary>
    ''' <remarks>
    ''' Enabled is false by default and nothing here is read while it is: ADM1-Full stays the
    ''' Batstone 2002 model to the last digit. Kinetic defaults are the mesophilic values of
    ''' Fedorovich 2003, which are the ones the BSM2-style parameter set can sit alongside.
    '''
    ''' Every rate is 1/d and every COD concentration kg COD/m³, matching the rest of the model.
    ''' Sulfate and sulfide are in kmol S/m³, and 64 kg COD reduces one kmol of sulfate: that
    ''' single identity is what makes sulfate reduction cost methane.
    ''' </remarks>
    <Serializable()> Public Class SulfateParams

        ''' <summary>Run the four sulfate reducers and the H2S inhibition. Off = plain ADM1.</summary>
        Public Property Enabled As Boolean = False

        ' ---- Maximum uptake rates (1/d) and donor half-saturations (kg COD/m³) ----
        Public Property k_m_srb_h2 As Double = 41.125
        Public Property K_S_srb_h2 As Double = 5.0E-06
        Public Property k_m_srb_ac As Double = 9.286
        Public Property K_S_srb_ac As Double = 0.024
        Public Property k_m_srb_pro As Double = 12.5
        Public Property K_S_srb_pro As Double = 0.295
        Public Property k_m_srb_bu As Double = 15.6
        Public Property K_S_srb_bu As Double = 0.176

        ' ---- Sulfate half-saturations (kmol S/m³) ----
        ' Sulfate limitation is what hands the substrate back to the methanogens once the sulfate
        ' is gone, so these decide where the changeover sits rather than merely how fast it is.
        Public Property K_S_so4_h2 As Double = 2.0E-04
        Public Property K_S_so4_ac As Double = 2.0E-04
        Public Property K_S_so4_pro As Double = 3.0E-04
        Public Property K_S_so4_bu As Double = 3.0E-04

        ' ---- Yields (kg COD biomass / kg COD donor) ----
        Public Property Y_srb_h2 As Double = 0.05
        Public Property Y_srb_ac As Double = 0.05
        Public Property Y_srb_pro As Double = 0.04
        Public Property Y_srb_bu As Double = 0.04

        ' ---- Decay (1/d) ----
        Public Property k_dec_srb_h2 As Double = 0.02
        Public Property k_dec_srb_ac As Double = 0.02
        Public Property k_dec_srb_pro As Double = 0.02
        Public Property k_dec_srb_bu As Double = 0.02

        ''' <summary>
        ''' Non-competitive inhibition constant on undissociated H2S for the ADM1 groups
        ''' (kmol S/m³). 0.003 is about 96 mg S/L, the middle of the reported IC50 band for
        ''' acetoclastic methanogens. Only free H2S is toxic, not HS-, which is why the model
        ''' inhibits on S_IS - S_hs_ion and why the effect is so strongly pH-dependent.
        ''' </summary>
        Public Property K_I_h2s As Double = 0.003

        ''' <summary>Same, for the sulfate reducers themselves - they tolerate roughly 3x more.</summary>
        Public Property K_I_h2s_srb As Double = 0.009

        ''' <summary>Influent sulfate (kmol S/m³). 1 kmol S/m³ = 32.06 kg S/m³.</summary>
        Public Property Sin_so4 As Double = 0.0

    End Class

    ''' <summary>Stoichiometric fractions and yields (Rosen & Jeppsson 2006 Table A.1).</summary>
    <Serializable()> Public Class StoichiometryParams
        ' Disintegration fractions of composites X_c
        Public Property f_sI_xc As Double = 0.1
        Public Property f_xI_xc As Double = 0.2
        Public Property f_ch_xc As Double = 0.2
        Public Property f_pr_xc As Double = 0.2
        Public Property f_li_xc As Double = 0.3
        ' Nitrogen content
        Public Property N_xc As Double = 0.0376 / 14.0  ' kmol N / kg COD
        ''' <summary>The Batstone 2002 value, kept nameable because characterising the composite
        ''' from a substrate has to recompute the cap on it from a fixed starting point.</summary>
        Public Const N_I_Default As Double = 0.06 / 14.0
        Public Property N_I As Double = N_I_Default
        Public Property N_aa As Double = 0.007
        Public Property N_bac As Double = 0.08 / 14.0
        ' Carbon content
        Public Property C_xc As Double = 0.02786
        Public Property C_sI As Double = 0.03
        Public Property C_ch As Double = 0.0313
        Public Property C_pr As Double = 0.03
        Public Property C_li As Double = 0.022
        Public Property C_xI As Double = 0.03
        Public Property C_su As Double = 0.0313
        Public Property C_aa As Double = 0.03
        Public Property C_fa As Double = 0.0217
        Public Property C_bu As Double = 0.025
        Public Property C_pro As Double = 0.0268
        Public Property C_ac As Double = 0.0313
        Public Property C_bac As Double = 0.0313
        Public Property C_va As Double = 0.024
        Public Property C_ch4 As Double = 0.0156
        ' Lipids hydrolysis split
        Public Property f_fa_li As Double = 0.95
        ' Sugar fermentation split (f_h2_su + f_bu_su + f_pro_su + f_ac_su ≈ 1)
        Public Property f_h2_su As Double = 0.19
        Public Property f_bu_su As Double = 0.13
        Public Property f_pro_su As Double = 0.27
        Public Property f_ac_su As Double = 0.41
        ' Amino-acid fermentation split
        Public Property f_h2_aa As Double = 0.06
        Public Property f_va_aa As Double = 0.23
        Public Property f_bu_aa As Double = 0.26
        Public Property f_pro_aa As Double = 0.05
        Public Property f_ac_aa As Double = 0.40
        ' Biomass yields (kg COD biomass / kg COD substrate)
        Public Property Y_su As Double = 0.10
        Public Property Y_aa As Double = 0.08
        Public Property Y_fa As Double = 0.06
        Public Property Y_c4 As Double = 0.06
        Public Property Y_pro As Double = 0.04
        Public Property Y_ac As Double = 0.05
        Public Property Y_h2 As Double = 0.06
    End Class

    ''' <summary>Biochemical kinetic parameters (all rates in 1/d).</summary>
    <Serializable()> Public Class KineticsParams
        Public Property k_dis As Double = 0.5       ' disintegration 1/d
        Public Property k_hyd_ch As Double = 10.0
        Public Property k_hyd_pr As Double = 10.0
        Public Property k_hyd_li As Double = 10.0
        Public Property k_m_su As Double = 30.0
        Public Property K_S_su As Double = 0.5      ' kg COD/m³
        Public Property k_m_aa As Double = 50.0
        Public Property K_S_aa As Double = 0.3
        Public Property k_m_fa As Double = 6.0
        Public Property K_S_fa As Double = 0.4
        Public Property k_m_c4 As Double = 20.0
        Public Property K_S_c4 As Double = 0.2
        Public Property k_m_pro As Double = 13.0
        Public Property K_S_pro As Double = 0.1
        Public Property k_m_ac As Double = 8.0
        Public Property K_S_ac As Double = 0.15
        Public Property k_m_h2 As Double = 35.0
        Public Property K_S_h2 As Double = 7.0E-06
        ' Biomass decay rates (1/d)
        Public Property k_dec_X_su As Double = 0.02
        Public Property k_dec_X_aa As Double = 0.02
        Public Property k_dec_X_fa As Double = 0.02
        Public Property k_dec_X_c4 As Double = 0.02
        Public Property k_dec_X_pro As Double = 0.02
        Public Property k_dec_X_ac As Double = 0.02
        Public Property k_dec_X_h2 As Double = 0.02
    End Class

    ''' <summary>Inhibition + pH envelope parameters.</summary>
    <Serializable()> Public Class InhibitionParams
        Public Property K_S_IN As Double = 1.0E-04    ' kmol/m³ IN-limit for all uptakes
        Public Property K_I_h2_fa As Double = 5.0E-06 ' kg COD/m³
        Public Property K_I_h2_c4 As Double = 1.0E-05
        Public Property K_I_h2_pro As Double = 3.5E-06
        Public Property K_I_nh3 As Double = 0.0018    ' kmol/m³, inhibits X_ac
        ' pH envelopes (Rosen & Jeppsson): lower/upper pH for each group
        Public Property pH_UL_aa As Double = 5.5
        Public Property pH_LL_aa As Double = 4.0
        Public Property pH_UL_ac As Double = 7.0
        Public Property pH_LL_ac As Double = 6.0
        Public Property pH_UL_h2 As Double = 6.0
        Public Property pH_LL_h2 As Double = 5.0
    End Class

    ''' <summary>
    ''' Physicochemical (acid-base, gas-liquid) parameters, quoted at the reference temperature
    ''' T_base_K and corrected to T_op_K by van't Hoff.
    ''' </summary>
    ''' <remarks>
    ''' These are values at 25 °C, NOT at the operating temperature. ADM1Equations.TemperatureCorrect
    ''' derives the values actually used at T_op_K, so setting T_op_K is all it takes to move the
    ''' whole acid-base and Henry chemistry with it. Editing these fields means editing the
    ''' chemistry at 25 °C.
    '''
    ''' They used to hold values already evaluated at 35 °C, with T_base_K declared and never read,
    ''' so a reactor run at any other temperature silently kept 35 °C chemistry: at 55 °C K_H_co2 was
    ''' 59% high, P_gas_h2o 65% low - the water simply vanished from the headspace - and K_w 62% low,
    ''' which moves pH.
    ''' </remarks>
    <Serializable()> Public Class PhysicochemicalParams
        Public Property T_base_K As Double = 298.15
        Public Property T_op_K As Double = 308.15
        Public Property R As Double = 0.08314          ' bar·m³/(K·kmol)
        Public Property K_w As Double = 1.0E-14        ' at 25°C (kmol/m³)²
        ' The VFA dissociation constants carry no van't Hoff term: their ΔH is near zero, and
        ' Rosen & Jeppsson leave them as constants.
        Public Property K_a_va As Double = 1.38E-05
        Public Property K_a_bu As Double = 1.5E-05
        Public Property K_a_pro As Double = 1.32E-05
        Public Property K_a_ac As Double = 1.74E-05
        Public Property K_a_co2 As Double = 4.4668E-07 ' 10^-6.35 at 25°C
        Public Property K_a_IN As Double = 5.6234E-10  ' 10^-9.25 at 25°C, NH4+/NH3
        ' H2S/HS- first dissociation. pKa1 ~ 7.0 sits inside the operating pH range, unlike the VFAs
        ' (pKa ~4.8, always dissociated), so the H2S/HS- split and pH are strongly coupled both ways.
        ' The second dissociation (HS-/S2-, pKa2 ~ 19) is negligible at pH 7-8 and is not modelled.
        Public Property K_a_h2s As Double = 8.9125E-08 ' 10^-7.05 at 25°C
        ' Acid-base kinetic constants of the ODE formulation of ADM1. This model uses the DAE
        ' formulation instead - SolvePH closes the charge balance algebraically - so nothing reads
        ' these. Kept for parameter-set compatibility only.
        Public Property k_AB_va As Double = 10000000000.0
        Public Property k_AB_bu As Double = 10000000000.0
        Public Property k_AB_pro As Double = 10000000000.0
        Public Property k_AB_ac As Double = 10000000000.0
        Public Property k_AB_co2 As Double = 10000000000.0
        Public Property k_AB_IN As Double = 10000000000.0
        ' Gas-liquid transfer
        Public Property k_La As Double = 200.0         ' 1/d
        Public Property K_H_co2 As Double = 0.035      ' kmol/(m³·bar) at 25°C
        Public Property K_H_ch4 As Double = 0.0014     ' at 25°C
        Public Property K_H_h2 As Double = 7.8E-04     ' at 25°C
        Public Property K_H_h2s As Double = 0.1        ' at 25°C; H2S is ~3x more soluble than CO2
        Public Property K_H_nh3 As Double = 59.0       ' at 25°C; NH3 is highly soluble (Sander)
        Public Property P_atm As Double = 1.013        ' bar
        Public Property P_gas_h2o As Double = 0.0313   ' bar at 25°C
        ''' <summary>Friction-like constant of the gas outlet (m³/d/bar). BSM2 value.</summary>
        Public Property k_P As Double = 50000.0
    End Class

    ''' <summary>Operating conditions (reactor volumes and influent specification).</summary>
    <Serializable()> Public Class OperatingParams
        Public Property V_liq As Double = 3400.0      ' m³ (BSM2 digester)
        Public Property V_gas As Double = 300.0       ' m³ headspace
        Public Property Q_in As Double = 178.4674     ' m³/d (BSM2 feed)
        Public Property T_op_K As Double = 308.15
        Public Property SimulationTime_d As Double = 200.0  ' integration horizon
        Public Property UseInfluentFromFeedStream As Boolean = True
        ' Influent defaults (BSM2 steady-state digester feed) - used when not taken from stream
        Public Property Sin_su As Double = 0.01
        Public Property Sin_aa As Double = 0.001
        Public Property Sin_fa As Double = 0.001
        Public Property Sin_va As Double = 0.001
        Public Property Sin_bu As Double = 0.001
        Public Property Sin_pro As Double = 0.001
        Public Property Sin_ac As Double = 0.001
        Public Property Sin_h2 As Double = 1.0E-08
        Public Property Sin_ch4 As Double = 1.0E-05
        Public Property Sin_IC As Double = 0.04
        Public Property Sin_IN As Double = 0.01
        Public Property Sin_I As Double = 0.02
        Public Property Xin_c As Double = 2.0
        Public Property Xin_ch As Double = 5.0
        Public Property Xin_pr As Double = 20.0
        Public Property Xin_li As Double = 5.0
        Public Property Xin_I As Double = 25.0
        Public Property Sin_cat As Double = 0.04
        Public Property Sin_an As Double = 0.02
        Public Property Sin_IS As Double = 0.0        ' influent dissolved sulfide (kmol/m³)

        ''' <summary>
        ''' Pack the influent specification into a state-ordered vector for the integrator.
        ''' Biomass (16-22, 32-35) and gas-phase (26-28, 30) entries are always zero: neither
        ''' enters the feed.
        ''' </summary>
        ''' <param name="sulfate">Sulfate-reduction parameters. Pass them to feed sulfate in;
        ''' omit them and the vector is the plain ADM1 influent it always was.</param>
        Public Function ToInfluentVector(Optional sulfate As SulfateParams = Nothing) As Double()
            Dim v(ADM1State.NDynamic - 1) As Double
            v(0) = Sin_su : v(1) = Sin_aa : v(2) = Sin_fa : v(3) = Sin_va
            v(4) = Sin_bu : v(5) = Sin_pro : v(6) = Sin_ac : v(7) = Sin_h2
            v(8) = Sin_ch4 : v(9) = Sin_IC : v(10) = Sin_IN : v(11) = Sin_I
            v(12) = Xin_c : v(13) = Xin_ch : v(14) = Xin_pr : v(15) = Xin_li
            v(23) = Xin_I : v(24) = Sin_cat : v(25) = Sin_an
            v(29) = Sin_IS
            If sulfate IsNot Nothing AndAlso sulfate.Enabled Then
                Dim so4 = Max(sulfate.Sin_so4, 0.0)
                v(31) = so4
                ' Sulfate is divalent and S_an counts charge, not moles - but it arrives as a salt,
                ' so its counter-cations come with it. Loading the anion alone would feed 2*Sin_so4
                ' of free acid: at 0.5 kmol S/m³ that titrates the reactor dead before a single
                ' reducer can grow, and the model would report zero sulfate conversion for a purely
                ' artificial reason. Charge-neutral in, and the alkalinity swing then comes from the
                ' reduction itself - S_an loses two charges per sulfate while S_cat keeps its two,
                ' which is exactly why sulfate reduction raises pH.
                v(24) = Sin_cat + 2.0 * so4
                v(25) = Sin_an + 2.0 * so4
            End If
            Return v
        End Function
    End Class

End Namespace
