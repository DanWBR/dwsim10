'    Full ADM1 (Batstone et al. 2002) - State Vector
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Collections.Generic

Namespace Reactors.ADM1

    ''' <summary>
    ''' Complete ADM1 dynamic state (Batstone et al. 2002 / Rosen &amp; Jeppsson 2006).
    ''' All soluble concentrations are in kg COD / m³ (equivalent to g COD / L) except:
    '''   S_IC, S_IN, S_cat, S_an, S_IS - in kmol / m³
    '''   S_h2_gas, S_ch4_gas, S_co2_gas - in kg COD / m³ (gas headspace, COD basis for gases, kmol/m³ for CO₂)
    '''   S_h2s_gas - in kmol / m³ (gas headspace)
    ''' Particulates X_* in kg COD / m³.
    ''' </summary>
    <Serializable()> Public Class ADM1State

        ' ---------- 12 soluble state variables ----------
        Public Property S_su As Double = 0.012   ' monosaccharides
        Public Property S_aa As Double = 0.0054  ' amino acids
        Public Property S_fa As Double = 0.107   ' long-chain fatty acids
        Public Property S_va As Double = 0.0123  ' total valerate
        Public Property S_bu As Double = 0.0140  ' total butyrate
        Public Property S_pro As Double = 0.0176 ' total propionate
        Public Property S_ac As Double = 0.0893  ' total acetate
        Public Property S_h2 As Double = 2.50E-07 ' dissolved hydrogen
        Public Property S_ch4 As Double = 0.0550 ' dissolved methane
        Public Property S_IC As Double = 0.1526  ' inorganic carbon (kmol/m³)
        Public Property S_IN As Double = 0.1302  ' inorganic nitrogen (kmol/m³)
        Public Property S_I As Double = 0.329    ' soluble inerts

        ' ---------- 12 particulate state variables ----------
        Public Property X_c As Double = 0.3087   ' composites
        Public Property X_ch As Double = 0.02795 ' carbohydrates
        Public Property X_pr As Double = 0.1024  ' proteins
        Public Property X_li As Double = 0.02948 ' lipids
        Public Property X_su As Double = 0.4202  ' sugar degraders
        Public Property X_aa As Double = 1.1792  ' amino-acid degraders
        Public Property X_fa As Double = 0.2430  ' LCFA degraders
        Public Property X_c4 As Double = 0.4319  ' valerate+butyrate degraders
        Public Property X_pro As Double = 0.1373 ' propionate degraders
        Public Property X_ac As Double = 0.7600  ' acetate degraders
        Public Property X_h2 As Double = 0.3170  ' hydrogen degraders
        Public Property X_I As Double = 25.6173  ' particulate inerts

        ' ---------- Ions ----------
        Public Property S_cat As Double = 0.04   ' cations (kmol/m³)
        Public Property S_an As Double = 0.02    ' anions (kmol/m³)

        ' ---------- Gas phase (Rosen & Jeppsson steady-state defaults) ----------
        Public Property S_h2_gas As Double = 1.102E-05   ' kg COD/m³
        Public Property S_ch4_gas As Double = 1.6216     ' kg COD/m³
        Public Property S_co2_gas As Double = 0.01441    ' kmol/m³

        ' ---------- Sulfur ----------
        ' Not part of standard ADM1 (Batstone et al. 2002 excludes sulfate reduction). Appended at
        ' the end of the state vector so indices 0-28 stay stable for saved influent vectors.
        ' Defaults of zero reproduce the sulfur-free model exactly.
        Public Property S_IS As Double = 0.0             ' total dissolved inorganic sulfide (kmol/m³)
        Public Property S_h2s_gas As Double = 0.0        ' headspace H2S (kmol/m³)

        ' ---------- Sulfate reduction (ADM1-S) ----------
        ' The four sulfate-reducing populations of Fedorovich et al. 2003 / Barrera et al. 2015 and
        ' the sulfate they respire, appended after the sulfide pair for the same reason. They only
        ' move when ADM1Parameters.Sulfate.Enabled is set; at zero the ADM1-Full model is untouched.
        Public Property S_so4 As Double = 0.0            ' dissolved sulfate (kmol S/m³)
        Public Property X_srb_h2 As Double = 0.0         ' hydrogenotrophic SRB (kg COD/m³)
        Public Property X_srb_ac As Double = 0.0         ' acetotrophic SRB
        Public Property X_srb_pro As Double = 0.0        ' propionate-oxidising SRB
        Public Property X_srb_bu As Double = 0.0         ' butyrate-oxidising SRB

        ' ---------- Algebraic / derived (refreshed by ADM1Equations.SolvePH) ----------
        Public Property S_H_ion As Double = 3.423E-08    ' kmol/m³ (pH 7.47)
        Public Property pH As Double = 7.4655
        Public Property S_va_ion As Double = 0.01156
        Public Property S_bu_ion As Double = 0.01322
        Public Property S_pro_ion As Double = 0.01574
        Public Property S_ac_ion As Double = 0.08957
        Public Property S_hco3_ion As Double = 0.14278
        Public Property S_nh3 As Double = 0.004           ' free ammonia (kmol/m³)
        Public Property S_nh4 As Double = 0.126           ' ammonium (kmol/m³)
        Public Property S_hs_ion As Double = 0.0          ' bisulfide HS- (kmol/m³)

        ' 26 core + gas(3) + sulfide(2) + sulfate cycle(5); derived fields are algebraic
        Public Const NDynamic As Integer = 36

        ''' <summary>Pack the 36 dynamic variables (those with ODEs) into a vector for the integrator.</summary>
        Public Function ToVector() As Double()
            Return New Double() {
                S_su, S_aa, S_fa, S_va, S_bu, S_pro, S_ac, S_h2, S_ch4, S_IC, S_IN, S_I,
                X_c, X_ch, X_pr, X_li, X_su, X_aa, X_fa, X_c4, X_pro, X_ac, X_h2, X_I,
                S_cat, S_an,
                S_h2_gas, S_ch4_gas, S_co2_gas,
                S_IS, S_h2s_gas,
                S_so4, X_srb_h2, X_srb_ac, X_srb_pro, X_srb_bu
            }
        End Function

        Public Sub FromVector(v As Double())
            If v Is Nothing OrElse v.Length <> NDynamic Then
                Throw New ArgumentException("ADM1State.FromVector expects a vector of length " &
                                            NDynamic & ", got " & If(v Is Nothing, "null", v.Length.ToString()) & ".")
            End If
            S_su = v(0) : S_aa = v(1) : S_fa = v(2) : S_va = v(3) : S_bu = v(4) : S_pro = v(5)
            S_ac = v(6) : S_h2 = v(7) : S_ch4 = v(8) : S_IC = v(9) : S_IN = v(10) : S_I = v(11)
            X_c = v(12) : X_ch = v(13) : X_pr = v(14) : X_li = v(15)
            X_su = v(16) : X_aa = v(17) : X_fa = v(18) : X_c4 = v(19) : X_pro = v(20)
            X_ac = v(21) : X_h2 = v(22) : X_I = v(23)
            S_cat = v(24) : S_an = v(25)
            S_h2_gas = v(26) : S_ch4_gas = v(27) : S_co2_gas = v(28)
            S_IS = v(29) : S_h2s_gas = v(30)
            S_so4 = v(31)
            X_srb_h2 = v(32) : X_srb_ac = v(33) : X_srb_pro = v(34) : X_srb_bu = v(35)
        End Sub

        Public Function Clone() As ADM1State
            Return DirectCast(Me.MemberwiseClone(), ADM1State)
        End Function

        ''' <summary>
        ''' COD equivalent of dissolved sulfide: S²⁻ → SO₄²⁻ is 8 electrons, i.e. 2 mol O₂ per mol S.
        ''' </summary>
        Public Const COD_per_kmol_S As Double = 64.0   ' kg COD / kmol S

        ''' <summary>Total liquid-phase COD (kg/m³) - sum of soluble + particulate COD state variables.</summary>
        Public Function TotalCOD() As Double
            ' Sulfide carries COD: its electrons were debited from the feed COD when the sulfide was
            ' formed, so sulfide left dissolved has to reappear here or COD vanishes from the balance.
            Return S_su + S_aa + S_fa + S_va + S_bu + S_pro + S_ac + S_h2 + S_ch4 + S_I +
                   X_c + X_ch + X_pr + X_li + X_su + X_aa + X_fa + X_c4 + X_pro + X_ac + X_h2 + X_I +
                   X_srb_h2 + X_srb_ac + X_srb_pro + X_srb_bu +
                   COD_per_kmol_S * S_IS
        End Function

        ''' <summary>Total sulfur held in the liquid phase (kmol S/m³): sulfate plus sulfide.</summary>
        Public Function TotalDissolvedS() As Double
            Return S_so4 + S_IS
        End Function

        Public Shared ReadOnly VarNames As String() = {
            "S_su", "S_aa", "S_fa", "S_va", "S_bu", "S_pro", "S_ac", "S_h2", "S_ch4", "S_IC", "S_IN", "S_I",
            "X_c", "X_ch", "X_pr", "X_li", "X_su", "X_aa", "X_fa", "X_c4", "X_pro", "X_ac", "X_h2", "X_I",
            "S_cat", "S_an",
            "S_h2_gas", "S_ch4_gas", "S_co2_gas",
            "S_IS", "S_h2s_gas",
            "S_so4", "X_srb_h2", "X_srb_ac", "X_srb_pro", "X_srb_bu"
        }

    End Class

End Namespace
