'    Anaerobic Digester Calculation Routines
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

Imports DWSIM.Thermodynamics.BaseClasses
Imports System.Math
Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports SkiaSharp
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics
Imports DWSIM.UnitOperations.Streams
Imports System.Collections.Generic
Imports DWSIM.UI.Shared.Avalonia

Namespace Reactors

    ''' <summary>Model fidelity selector for the Anaerobic Digester.</summary>
    Public Enum DigesterModel
        ''' <summary>Black-box Buswell + COD-removal efficiency (steady-state, single-population lumped).</summary>
        BlackBox = 0
        ''' <summary>ADM1-Lite: 4-population reduced ADM1 with VFA intermediates, H2 inhibition and dual
        ''' methanogenesis pathways (transient ODE, integrated over HRT / BatchDuration).</summary>
        ADM1Lite = 1
        ''' <summary>Full ADM1 (Batstone et al. 2002, IWA Task Group): 29 dynamic states, 19 biochemical
        ''' processes, algebraic acid-base equilibria and gas-liquid transfer. BSM2 benchmark defaults
        ''' (Rosen &amp; Jeppsson 2006). Parameters and trajectory available via dedicated dialogs.</summary>
        ADM1Full = 2
        ''' <summary>ADM1-S: full ADM1 plus kinetic sulfate reduction (Fedorovich et al. 2003,
        ''' Barrera et al. 2015). Four sulfate-reducing populations compete with the methanogens
        ''' and acetogens for hydrogen, acetate, propionate and butyrate, and the free H2S they
        ''' make inhibits both. Feed sulfate is respired here rather than debited from the COD up
        ''' front, so the methane loss follows the competition instead of being assumed.</summary>
        ADM1Sulfate = 3
    End Enum

    ''' <summary>
    ''' Anaerobic digester with three selectable model fidelities:
    '''   BlackBox  - steady-state Buswell / COD-removal (Tier A, default).
    '''   ADM1Lite  - reduced ADM1 ODE with hydrolysis â†’ acidogenesis â†’ acetogenesis (H2-inhibited)
    '''               â†’ acetoclastic + hydrogenotrophic methanogenesis.
    '''   ADM1Full  - the full Batstone 2002 / Rosen &amp; Jeppsson 2006 benchmark.
    '''   ADM1S     - ADM1Full plus kinetic sulfate reduction (Fedorovich 2003, Barrera 2015).
    ''' All modes share compound roles, ports, thermal-balance and energy-stream plumbing.
    '''
    ''' Sulfur is modelled on top of standard ADM1, which excludes sulfate reduction entirely.
    ''' Declare sulfate and organic sulfur separately: sulfate carries no COD, so reducing it to
    ''' sulfide costs 64 kg COD/kmol S out of the pool that would have made methane, while organic
    ''' sulfur arrives already reduced and is COD-neutral. Sulfide is partitioned between the biogas
    ''' (as H2S) and the effluent, and sulfide fed in joins the same pool.
    '''
    ''' The first three models do that stoichiometrically: the electron accounting is right, but
    ''' they assume every sulfate fed is reduced and carry no population dynamics. ADM1-S is the
    ''' one that solves it, growing four sulfate-reducing populations against the methanogens and
    ''' acetogens and letting free H2S inhibit both, so partial sulfate conversion, a sulfate-
    ''' limited digester and H2S toxicity are all states it can actually reach.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_AnaerobicDigester

        Inherits Reactor

        Implements IExternalUnitOperation
        Public ReadOnly Property IsBio As Boolean = True

        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Reactors
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ' ----------- INPUT PROPERTIES -----------

        ''' <summary>Working volume (m3).</summary>
        Public Property Volume As Double = 10.0

        ''' <summary>Hydraulic retention time HRT (s). Reported; not used in the steady-state conversion.</summary>
        Public Property HRT_s As Double = 86400.0 * 20.0 ' 20 days default

        ''' <summary>Name of the single Organic Substrate compound being digested.</summary>
        Public Property SubstrateCompound As String = ""

        ''' <summary>Methane compound name (typically "Methane").</summary>
        Public Property MethaneCompound As String = "Methane"

        ''' <summary>Carbon dioxide compound name.</summary>
        Public Property CO2Compound As String = "Carbon dioxide"

        ''' <summary>Water compound name.</summary>
        Public Property WaterCompound As String = "Water"

        ''' <summary>Ammonia compound name (nitrogen release from organic N). Optional.</summary>
        Public Property NH3Compound As String = "Ammonia"

        ''' <summary>Sludge biomass compound name. Optional - if empty, sludge is reported as zero.</summary>
        Public Property BiomassCompound As String = ""

        ''' <summary>Hydrogen sulfide compound name. Defaults to "Hydrogen sulfide" so the partitioned
        ''' H2S is written into the outlet streams whenever that compound is present; if the name is
        ''' cleared or the compound is absent, the sulfur balance still runs and is reported.</summary>
        Public Property H2SCompound As String = "Hydrogen sulfide"

        ''' <summary>
        ''' (ADM1-S) Compound carrying the sulfate the reducers did not respire, so it leaves in the
        ''' effluent instead of disappearing. Sulfuric acid or any sulfate salt will do: the sulfur
        ''' is converted through the compound's own formula. Only ADM1-S can leave sulfate
        ''' unreduced - the other models assume all of it is - so the other three ignore this.
        ''' </summary>
        Public Property SulfateCompound As String = ""

        ''' <summary>Fractional COD removal (0â€“1). Typical 0.65â€“0.90 for mesophilic AD.</summary>
        Public Property CODRemovalEfficiency As Double = 0.85

        ''' <summary>Biomass yield on COD removed (g VSS / g COD). Typical 0.04â€“0.10 for mesophilic AD.</summary>
        Public Property BiomassYield_gVSSpergCOD As Double = 0.08

        ''' <summary>User override for the methane fraction of the biogas (mol/mol). Set â‰¤0 to use the Buswell-predicted split.</summary>
        Public Property MethaneFractionOverride As Double = 0.0

        ''' <summary>Thermal mode (reuses the BioReactor enum for consistency).</summary>
        Public Property ThermalMode As BioReactorThermalMode = BioReactorThermalMode.Isothermal

        ''' <summary>Heat release per gram of COD removed (J/g COD, negative = exothermic).
        ''' Anaerobic digestion is only mildly exothermic, â‰ˆ âˆ’3500 J/g COD (â‰ˆ 8 % of aerobic).</summary>
        Public Property HeatPerGCODremoved_Jg As Double = -3500.0

        ''' <summary>Model fidelity selector (BlackBox Buswell or ADM1-Lite reduced dynamic model).</summary>
        Public Property Model As DigesterModel = DigesterModel.BlackBox

        ' ----------- SULFUR BALANCE -----------
        ' Standard ADM1 (Batstone et al. 2002) excludes sulfate reduction, so none of the three
        ' models tracked sulfur. This is a stoichiometric balance: the sulfur declared here is
        ' mineralised to sulfide and partitioned between biogas and effluent. It does NOT model the
        ' kinetic competition between sulfate reducers and methanogens for H2 and acetate.
        '
        ' Sulfate-S and organic-S are declared separately because they differ in electron
        ' bookkeeping: reducing sulfate to sulfide costs 64 kg COD/kmol S drawn from the same pool
        ' that would make methane (a real CH4 loss), whereas organic S arrives already reduced
        ' inside the substrate molecule and is COD-neutral for methane.

        ''' <summary>Sulfate sulfur in the feed liquid (mg S/L). Carries no COD of its own; reducing
        ''' it to sulfide debits 64 kg COD/kmol S from the feed, which is a genuine methane loss.</summary>
        Public Property InfluentSulfateS_mgL As Double = 0.0

        ''' <summary>Organic sulfur bound in the substrate (g S/kg substrate). Leave at -1 to read it
        ''' from the substrate compound's elemental formula, which keeps it consistent with the
        ''' theoretical COD. Set >= 0 to override when the compound has no S in its formula.</summary>
        Public Property SubstrateOrganicS_gPerKg As Double = -1.0

        ''' <summary>Assumed pH for the H2S/HS- speciation in the BlackBox and ADM1-Lite models,
        ''' which have no mechanistic pH. ADM1-Full ignores this and uses its charge-balance pH.</summary>
        Public Property AssumedPH_ForSulfide As Double = 7.2

        ''' <summary>(ADM1-Full) Feed alkalinity carried by strong cations - the net cation charge the
        ''' substrate brings that is not accounted for by the ammonia it releases (potassium, sodium,
        ''' calcium salts in the raw feed), in equivalents per litre (= kmol charge/m³). ADM1-Full solves
        ''' pH from the influent charge balance, and manure feeds are strongly buffered (typically
        ''' 0.05-0.15 eq/L, ~2.5-7.5 g CaCO3/L); leaving this at 0 lets the pH fall, which strips CO2 into
        ''' the biogas and understates the methane fraction. Only ADM1-Full and ADM1-S read it.</summary>
        Public Property InfluentAlkalinity_eqL As Double = 0.0

        ' ----------- ADM1-LITE INITIAL STATE (concentrations, g COD/L unless noted) -----------

        ''' <summary>(ADM1-Lite) Initial soluble-substrate COD (g COD/L) - lumped sugars/amino-acids/LCFA from hydrolysed particulates.</summary>
        Public Property ADM1_S_s0 As Double = 0.5
        ''' <summary>(ADM1-Lite) Initial VFA concentration (g COD/L) - lumped propionate + butyrate + valerate.</summary>
        Public Property ADM1_S_VFA0 As Double = 0.2
        ''' <summary>(ADM1-Lite) Initial acetate concentration (g COD/L).</summary>
        Public Property ADM1_S_Ac0 As Double = 0.1
        ''' <summary>(ADM1-Lite) Initial dissolved H2 concentration (g COD/L). Typical value ~1e-6.</summary>
        Public Property ADM1_S_H20 As Double = 0.0000001
        ''' <summary>(ADM1-Lite) Initial acidogen/hydrolyser biomass (g VSS/L).</summary>
        Public Property ADM1_X_hyd0 As Double = 0.2
        ''' <summary>(ADM1-Lite) Initial acetogen biomass (g VSS/L).</summary>
        Public Property ADM1_X_ace0 As Double = 0.1
        ''' <summary>(ADM1-Lite) Initial acetoclastic methanogen biomass (g VSS/L).</summary>
        Public Property ADM1_X_am0 As Double = 0.1
        ''' <summary>(ADM1-Lite) Initial hydrogenotrophic methanogen biomass (g VSS/L).</summary>
        Public Property ADM1_X_hm0 As Double = 0.05

        ' ----------- ADM1-LITE KINETIC PARAMETERS -----------

        ''' <summary>(ADM1-Lite) First-order hydrolysis rate of particulate substrate (1/d, Batstone default 10).</summary>
        Public Property ADM1_k_hyd_d As Double = 10.0
        ''' <summary>(ADM1-Lite) Maximum specific uptake rate of sugars/amino-acids by acidogens (1/d, ~30).</summary>
        Public Property ADM1_km_su_d As Double = 30.0
        ''' <summary>(ADM1-Lite) Half-saturation K for sugars/aa (g COD/L, ~0.5).</summary>
        Public Property ADM1_Ks_su As Double = 0.5
        ''' <summary>(ADM1-Lite) Biomass yield on soluble substrate (g VSS / g COD, ~0.1).</summary>
        Public Property ADM1_Y_su As Double = 0.1
        ''' <summary>(ADM1-Lite) Max specific uptake rate of VFAs by acetogens (1/d, ~20).</summary>
        Public Property ADM1_km_vfa_d As Double = 20.0
        ''' <summary>(ADM1-Lite) Half-saturation K for VFAs (g COD/L, ~0.3).</summary>
        Public Property ADM1_Ks_vfa As Double = 0.3
        ''' <summary>(ADM1-Lite) Acetogen yield (g VSS / g COD, ~0.05).</summary>
        Public Property ADM1_Y_ace As Double = 0.05
        ''' <summary>(ADM1-Lite) H2 inhibition constant on acetogens (g COD/L, Batstone KI_h2_pro ~ 3.5e-6).</summary>
        Public Property ADM1_KI_h2 As Double = 0.0000035
        ''' <summary>(ADM1-Lite) Max acetoclastic methanogen uptake rate (1/d, ~8).</summary>
        Public Property ADM1_km_ac_d As Double = 8.0
        ''' <summary>(ADM1-Lite) Half-saturation K for acetate (g COD/L, ~0.15).</summary>
        Public Property ADM1_Ks_ac As Double = 0.15
        ''' <summary>(ADM1-Lite) Acetoclastic methanogen yield (g VSS / g COD, ~0.05).</summary>
        Public Property ADM1_Y_am As Double = 0.05
        ''' <summary>(ADM1-Lite) Max hydrogenotrophic methanogen uptake rate (1/d, ~35).</summary>
        Public Property ADM1_km_h2_d As Double = 35.0
        ''' <summary>(ADM1-Lite) Half-saturation K for H2 (g COD/L, ~7e-6).</summary>
        Public Property ADM1_Ks_h2 As Double = 0.000007
        ''' <summary>(ADM1-Lite) Hydrogenotrophic methanogen yield (g VSS / g COD, ~0.06).</summary>
        Public Property ADM1_Y_hm As Double = 0.06
        ''' <summary>(ADM1-Lite) First-order decay rate for all populations (1/d, ~0.02).</summary>
        Public Property ADM1_k_dec_d As Double = 0.02

        ' ----------- ADM1-LITE RESULT STATE -----------

        Public Property ADM1_Result_S_s As Double = 0.0
        Public Property ADM1_Result_S_VFA As Double = 0.0
        Public Property ADM1_Result_S_Ac As Double = 0.0
        Public Property ADM1_Result_S_H2 As Double = 0.0
        Public Property ADM1_Result_X_hyd As Double = 0.0
        Public Property ADM1_Result_X_ace As Double = 0.0
        Public Property ADM1_Result_X_am As Double = 0.0
        Public Property ADM1_Result_X_hm As Double = 0.0
        Public Property ADM1_Result_pH As Double = 7.0

        ' ----------- FULL ADM1 (Batstone 2002) -----------

        ''' <summary>Full ADM1 parameter set (100+ values) - edited via the dedicated FormADM1Parameters dialog.
        ''' Persisted as a JSON blob on the unit op (see ADM1ParamsJSON).</summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property ADM1Params As ADM1.ADM1Parameters = New ADM1.ADM1Parameters()

        ''' <summary>Serialized ADM1 parameter set (XML/clone round-trip).</summary>
        Public Property ADM1ParamsJSON As String = ""

        ''' <summary>Last full-ADM1 simulation trajectory - populated by CalculateADM1Full.
        ''' Used by FormADM1Results for charting and export. Not persisted.</summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property ADM1LastTrajectory As ADM1.ADM1TrajectoryResult

        ''' <summary>Final state of the most recent full-ADM1 simulation.</summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore>
        Public Property ADM1LastState As ADM1.ADM1State

        ' ----------- RESULT PROPERTIES -----------

        ''' <summary>Feed COD load (kg COD/s).</summary>
        Public Property Result_CODin_kgs As Double = 0.0

        ''' <summary>COD removed (kg COD/s).</summary>
        Public Property Result_CODremoved_kgs As Double = 0.0

        ''' <summary>Mass flow of substrate consumed (kg/s).</summary>
        Public Property Result_SubstrateConsumed_kgs As Double = 0.0

        ''' <summary>Biogas molar flow (mol/s).</summary>
        Public Property Result_BiogasFlow_mols As Double = 0.0

        ''' <summary>Methane flow (kg/s).</summary>
        Public Property Result_CH4_kgs As Double = 0.0

        ''' <summary>Carbon dioxide flow (kg/s).</summary>
        Public Property Result_CO2_kgs As Double = 0.0

        ''' <summary>Methane mole fraction in biogas (â€“).</summary>
        Public Property Result_CH4MoleFraction As Double = 0.0

        ''' <summary>Specific methane yield (Nm3 CH4 / kg COD removed).</summary>
        Public Property Result_SpecificCH4Yield_Nm3kgCOD As Double = 0.0

        ''' <summary>Biomass (sludge) production (kg/s).</summary>
        Public Property Result_Sludge_kgs As Double = 0.0

        ''' <summary>Hydrogen sulfide leaving in the biogas (kg/s).</summary>
        Public Property Result_H2S_kgs As Double = 0.0

        ''' <summary>Hydrogen sulfide in the biogas (ppmv, dry basis) - the number that sizes the
        ''' primary desulfurisation stage ahead of upgrading.</summary>
        Public Property Result_H2S_ppmv As Double = 0.0

        ''' <summary>Total dissolved sulfide (H2S + HS-) left in the effluent, as kg S/m³.</summary>
        Public Property Result_DissolvedSulfide_kgSm3 As Double = 0.0

        ''' <summary>(ADM1-S) Sulfate left unreduced in the effluent, as kg S/m³. Zero in the other
        ''' models, which assume every sulfate fed is reduced.</summary>
        Public Property Result_ResidualSulfate_kgSm3 As Double = 0.0

        ''' <summary>(ADM1-S) Fraction of the influent sulfate the reducers actually respired.
        ''' Well short of 1 whenever sulfate outruns the electron donors or the H2S turns toxic.</summary>
        Public Property Result_SulfateReduction As Double = 0.0

        ''' <summary>(ADM1-S) Standing sulfate-reducing biomass, all four groups (kg COD/m³).</summary>
        Public Property Result_SRBBiomass_kgCODm3 As Double = 0.0

        ''' <summary>Metabolic heat release (kW, positive = exothermic).</summary>
        Public Property Result_Q_metabolic_kW As Double = 0.0

        ''' <summary>Net heat duty published to the energy stream (kW, + heating / âˆ’ cooling).</summary>
        Public Property Result_Q_duty_kW As Double = 0.0

        ''' <summary>Outlet temperature (K) resulting from the selected thermal mode.</summary>
        Public Property Result_OutletTemperature_K As Double = 0.0

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "CSTR Digester", "Plug-Flow Digester", "UASB", "Covered Lagoon"}
            End Get
        End Property

        Public Overrides Sub CreateDimensionsList()
            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Volume, .IsUserDefined = False})
        End Sub

        Public Overrides Sub UpdateDimensionsList()
            Dimensions(0).Value = Volume
        End Sub

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_AnaerobicDigester()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_AnaerobicDigester)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Returns the theoretical COD equivalent of a compound (g COD / g substrate) from its
        ''' elemental formula C_a H_b O_c N_d S_e, oxidising C to CO2, N to NH3 and S to sulfate:
        '''   COD = 32*(a + b/4 - c/2 - 3d/4 + 3e/2) / MW.
        ''' Reduces to well-known values (glucose 1.07 g/g, acetic acid 1.07 g/g, ethanol 2.09 g/g);
        ''' for H2S (a=0, b=2, e=1) it gives 2 mol O2/mol, i.e. H2S + 2 O2 -> H2SO4.
        ''' </summary>
        Private Shared Function TheoreticalCOD(cp As ConstantProperties) As Double
            If cp Is Nothing OrElse cp.Elements Is Nothing Then Return 0.0
            Dim a As Double = 0, b As Double = 0, c As Double = 0, d As Double = 0, e As Double = 0
            If cp.Elements.Contains("C") Then a = Convert.ToDouble(cp.Elements("C"))
            If cp.Elements.Contains("H") Then b = Convert.ToDouble(cp.Elements("H"))
            If cp.Elements.Contains("O") Then c = Convert.ToDouble(cp.Elements("O"))
            If cp.Elements.Contains("N") Then d = Convert.ToDouble(cp.Elements("N"))
            If cp.Elements.Contains("S") Then e = Convert.ToDouble(cp.Elements("S"))
            Dim O2mol = a + b / 4.0 - c / 2.0 - 0.75 * d + 1.5 * e
            If O2mol <= 0 Then Return 0.0
            If cp.Molar_Weight <= 0 Then Return 0.0
            Return 32.0 * O2mol / cp.Molar_Weight ' g O2 / g substrate
        End Function

        ''' <summary>Sulfur atoms per molecule from the elemental formula (0 if absent).</summary>
        Private Shared Function SulfurAtoms(cp As ConstantProperties) As Double
            If cp Is Nothing OrElse cp.Elements Is Nothing Then Return 0.0
            If Not cp.Elements.Contains("S") Then Return 0.0
            Return Convert.ToDouble(cp.Elements("S"))
        End Function

        ''' <summary>
        ''' Buswell stoichiometry per mol of substrate C_a H_b O_c N_d S_e:
        '''   CaHbOcNdSe + (a âˆ’ b/4 âˆ’ c/2 + 3d/4 + e/2) H2O
        '''     -> (a/2 âˆ’ b/8 + c/4 + 3d/8 + e/4) CO2 + (a/2 + b/8 âˆ’ c/4 âˆ’ 3d/8 âˆ’ e/4) CH4
        '''        + d NH3 + e H2S
        ''' </summary>
        Private Shared Sub BuswellCoefficients(a As Double, b As Double, c As Double, d As Double, e As Double,
                                               ByRef nCH4 As Double, ByRef nCO2 As Double,
                                               ByRef nH2O As Double, ByRef nNH3 As Double,
                                               ByRef nH2S As Double)
            nH2O = a - b / 4.0 - c / 2.0 + 3.0 * d / 4.0 + e / 2.0
            nCO2 = a / 2.0 - b / 8.0 + c / 4.0 + 3.0 * d / 8.0 + e / 4.0
            nCH4 = a / 2.0 + b / 8.0 - c / 4.0 - 3.0 * d / 8.0 - e / 4.0
            nNH3 = d
            nH2S = e
            If nCH4 < 0 Then nCH4 = 0
            If nCO2 < 0 Then nCO2 = 0
        End Sub

        ''' <summary>
        ''' Liquid volumetric flow of the feed (m³/s), falling back through the overall phase and
        ''' then mass/density when the property package resolves no liquid phase.
        ''' </summary>
        Private Shared Function LiquidVolumetricFlow(ims As MaterialStream) As Double
            ' An anaerobic digester feed is an aqueous slurry: the hydraulic loading is the whole feed,
            ' not just the flashed liquid phase. Deriving it from that phase is fragile - a pseudo-compound
            ' substrate can flash to a sliver of "liquid" or make the property package report an
            ' unphysical mixture density (e.g. 145 kg/m3), and either one throws the HRT off by orders of
            ' magnitude. Load from the total feed mass over a liquid density floored to an aqueous value,
            ' and fall back to the reported volumetric flow only if there is no mass.
            Dim m_feed = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim rho_L = ims.Phases(1).Properties.density.GetValueOrDefault
            If rho_L <= 250.0 Then rho_L = ims.Phases(0).Properties.density.GetValueOrDefault
            If rho_L <= 250.0 Then rho_L = 1000.0   ' aqueous-slurry floor: a real digester feed is mostly water
            Dim q = If(m_feed > 0.0, m_feed / rho_L, 0.0)
            If q <= 0.0 Then q = ims.Phases(1).Properties.volumetric_flow.GetValueOrDefault
            If q <= 0.0 Then q = ims.Phases(0).Properties.volumetric_flow.GetValueOrDefault
            If q <= 0.0 Then q = 0.001
            Return q
        End Function

        ''' <summary>Molar mass of elemental sulfur (kg/kmol). Note the BlackBox path has a local
        ''' MW_S meaning the molar mass of the substrate - these are different things.</summary>
        Private Const MW_Sulfur As Double = 32.06

        ''' <summary>Molar mass of H2S (kg/kmol).</summary>
        Private Const MW_H2S As Double = 34.08

        ''' <summary>
        ''' Starting biomass for each sulfate-reducing population in ADM1-S (kg COD/m³), used when
        ''' the user has not supplied one. Two orders of magnitude below the ADM1 groups: enough to
        ''' let the population establish where the sulfate supports it, small enough that it washes
        ''' straight back out where it does not.
        ''' </summary>
        Private Const SRBSeed_kgCODm3 As Double = 0.01

        ''' <summary>
        ''' Influent sulfur load (kmol S/s), split into its sulfate and organic contributions, plus
        ''' any COD that has to be credited to the substrate before the sulfide debit is applied.
        ''' </summary>
        ''' <remarks>
        ''' Organic sulfur is read from the substrate's elemental formula by default, which keeps it
        ''' consistent with TheoreticalCOD (which counts S). The SubstrateOrganicS_gPerKg override is
        ''' for compounds carrying no S in their formula: that sulfur is invisible to TheoreticalCOD,
        ''' so its COD is credited here (2 g COD/g S) and the debit downstream then cancels it out -
        ''' organic sulfur is COD-neutral for methane either way. Sulfate sulfur is not credited: it
        ''' has no COD, so its debit is a real methane loss, which is the whole point of the split.
        ''' </remarks>
        Private Sub SulfurLoad(cpSub As ConstantProperties, m_sub_kgs As Double, Q_liquid_m3s As Double,
                               ByRef nSO4_kmols As Double, ByRef nOrg_kmols As Double,
                               ByRef codFactorExtra As Double)

            nSO4_kmols = Max(InfluentSulfateS_mgL, 0.0) * Q_liquid_m3s / (1000.0 * MW_Sulfur)
            nOrg_kmols = 0.0
            codFactorExtra = 0.0

            Dim eSulfur = SulfurAtoms(cpSub)
            If eSulfur > 0.0 AndAlso cpSub IsNot Nothing AndAlso cpSub.Molar_Weight > 0.0 Then
                nOrg_kmols = eSulfur * m_sub_kgs / cpSub.Molar_Weight
                If SubstrateOrganicS_gPerKg >= 0.0 Then
                    FlowSheet?.ShowMessage(String.Format(
                        "{0}: substrate '{1}' already declares sulfur in its elemental formula, so the " &
                        "organic-sulfur override is being ignored. Clear the override (set it to -1) to " &
                        "silence this warning.", Me.GraphicObject.Tag, SubstrateCompound),
                        IFlowsheet.MessageType.Warning)
                End If
            ElseIf SubstrateOrganicS_gPerKg > 0.0 Then
                nOrg_kmols = SubstrateOrganicS_gPerKg * m_sub_kgs / (1000.0 * MW_Sulfur)
                codFactorExtra = 2.0 * SubstrateOrganicS_gPerKg / 1000.0   ' g COD / g substrate
            End If
        End Sub

        ''' <summary>
        ''' Characterise the ADM1 composite (X_c) from the substrate's elemental formula so the
        ''' influent fed as X_c carries the substrate's own carbon and nitrogen instead of the generic
        ''' Rosen and Jeppsson composite. Sets the composite carbon and nitrogen content (C_xc, N_xc,
        ''' both per unit COD) and moves the disintegration split so the protein fraction carries the
        ''' substrate nitrogen - the disintegration nitrogen balance stays closed (no artificial ammonia
        ''' source or sink) and the lipid and inert fractions (hence the biodegradable/inert split) are
        ''' kept. This is what lets a real substrate release ammonia (raising alkalinity, so CO2 stays in
        ''' the liquid as bicarbonate and the biogas is CH4-rich) and leave a non-degradable inert
        ''' residue, instead of the old behaviour that mapped every substrate to pure carbohydrate.
        ''' </summary>
        Private Sub CharacteriseCompositeFromSubstrate(cp As ConstantProperties)
            If cp Is Nothing OrElse cp.Elements Is Nothing Then Return
            Dim a As Double = 0, b As Double = 0, c As Double = 0, d As Double = 0, e As Double = 0
            If cp.Elements.Contains("C") Then a = Convert.ToDouble(cp.Elements("C"))
            If cp.Elements.Contains("H") Then b = Convert.ToDouble(cp.Elements("H"))
            If cp.Elements.Contains("O") Then c = Convert.ToDouble(cp.Elements("O"))
            If cp.Elements.Contains("N") Then d = Convert.ToDouble(cp.Elements("N"))
            If cp.Elements.Contains("S") Then e = Convert.ToDouble(cp.Elements("S"))
            Dim O2mol = a + b / 4.0 - c / 2.0 - 0.75 * d + 1.5 * e
            If O2mol <= 0.0 OrElse a <= 0.0 Then Return   ' cannot characterise; keep the default composite

            Dim st = ADM1Params.Stoichiometry
            ' Carbon and nitrogen per unit COD (kmol/kg COD): COD per mole = 32*O2mol g O2.
            st.C_xc = a / (32.0 * O2mol)
            st.N_xc = d / (32.0 * O2mol)

            ' All composite nitrogen not held by the soluble/particulate inerts goes into the protein
            ' fraction, so nDis = N_xc - (f_sI+f_xI)*N_I - f_pr*N_aa is zero and the disintegration adds
            ' no spurious ammonia term. The COD freed from the default protein fraction becomes
            ' carbohydrate; the lipid and inert fractions are left untouched.
            Dim inertN = (st.f_sI_xc + st.f_xI_xc) * st.N_I
            Dim fPr = (st.N_xc - inertN) / Max(st.N_aa, 1.0E-30)
            fPr = Max(0.0, Min(fPr, 1.0 - st.f_sI_xc - st.f_xI_xc - st.f_li_xc))
            st.f_pr_xc = fPr
            st.f_ch_xc = 1.0 - st.f_sI_xc - st.f_xI_xc - st.f_li_xc - fPr
        End Sub

        ''' <summary>
        ''' Equilibrium gas-liquid split of a sulfide load, in closed form, for the BlackBox and
        ''' ADM1-Lite paths (which have no headspace state of their own).
        ''' </summary>
        ''' <remarks>
        ''' Only undissociated H2S is volatile, so the split depends on Henry's law AND on the
        ''' H2S/HS- speciation. With f = [H+]/(Ka + [H+]) the undissociated fraction, equilibrium
        ''' c_H2S = K_H·y·P and c_total = c_H2S/f give nS = c_total·Q + y·n_gas, hence
        ''' y = nS / (K_H·P·Q/f + n_gas). pKa1(H2S) is about 7, right inside the operating range,
        ''' so this is genuinely pH-sensitive - see AssumedPH_ForSulfide.
        '''
        ''' K_a_h2s and K_H_h2s are borrowed from the ADM1 parameter set, which holds them at 25 °C,
        ''' so they have to be brought to T_K before use here exactly as ADM1-Full does. These two
        ''' models have no ADM1 state of their own, hence the temperature argument.
        ''' </remarks>
        Private Sub PartitionSulfide(nS_kmols As Double, Q_liquid_m3s As Double, nGasDry_kmols As Double,
                                     P_bar As Double, T_K As Double, ByRef nH2SGas_kmols As Double,
                                     ByRef cSulfideLiq_kmolm3 As Double)
            Dim phys = ADM1.ADM1Equations.TemperatureCorrect(ADM1Params.Physicochemical, T_K)
            Dim S_H = 10.0 ^ (-Max(AssumedPH_ForSulfide, 0.1))
            Dim f_H2S = S_H / (phys.K_a_h2s + S_H)
            PartitionVolatile(nS_kmols, Q_liquid_m3s, nGasDry_kmols, P_bar, phys.K_H_h2s, f_H2S,
                              nH2SGas_kmols, cSulfideLiq_kmolm3)
        End Sub

        ''' <summary>
        ''' Closed-form equilibrium gas-liquid split of a volatile weak-electrolyte load. Only the
        ''' undissociated (Henry-volatile) fraction f leaves for the gas: with c_volatile = K_H·y·P and
        ''' c_total = c_volatile/f, the balance nTotal = c_total·Q + y·n_gas gives
        ''' y = nTotal / (K_H·P·Q/f + n_gas). Shared by the H2S, CO2 and NH3 splits in the closed-form
        ''' BlackBox and ADM1-Lite paths, which carry no headspace state of their own.
        ''' </summary>
        Private Shared Sub PartitionVolatile(nTotal_kmols As Double, Q_liquid_m3s As Double,
                                             nGasDry_kmols As Double, P_bar As Double, K_H As Double,
                                             fVolatile As Double, ByRef nGas_kmols As Double,
                                             ByRef cLiq_kmolm3 As Double)
            nGas_kmols = 0.0
            cLiq_kmolm3 = 0.0
            If nTotal_kmols <= 0.0 Then Exit Sub
            If fVolatile <= 0.0 Then fVolatile = 1.0E-12
            Dim denom = K_H * P_bar * Q_liquid_m3s / fVolatile + nGasDry_kmols
            If denom <= 0.0 Then Exit Sub
            Dim y = nTotal_kmols / denom
            nGas_kmols = Max(Min(y * nGasDry_kmols, nTotal_kmols), 0.0)
            If Q_liquid_m3s > 0.0 Then cLiq_kmolm3 = (nTotal_kmols - nGas_kmols) / Q_liquid_m3s
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            ' Route to the reduced-ADM1 path if selected, otherwise fall through to the black-box model
            If Model = DigesterModel.ADM1Lite Then
                CalculateADM1Lite()
                Return
            End If

            If Model = DigesterModel.ADM1Full OrElse Model = DigesterModel.ADM1Sulfate Then
                CalculateADM1Full()
                Return
            End If

            If String.IsNullOrEmpty(SubstrateCompound) Then
                Throw New Exception("AnaerobicDigester: Organic Substrate compound not selected.")
            End If
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception("AnaerobicDigester: Feed stream not connected.")
            End If
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception("AnaerobicDigester: Effluent stream not connected.")
            End If
            If Volume <= 0.0 Then
                Throw New Exception("AnaerobicDigester: Working volume must be positive.")
            End If

            Dim ims As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.SetFlowsheet(Me.FlowSheet)
            ims.SetPropertyPackage(PropertyPackage)
            PropertyPackage.CurrentMaterialStream = ims
            ims.DefinedFlow = FlowSpec.Mass

            Dim T As Double = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P0 As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P As Double = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            Dim compounds = ims.Phases(0).Compounds

            If Not compounds.ContainsKey(SubstrateCompound) Then _
                Throw New Exception("AnaerobicDigester: Substrate '" & SubstrateCompound & "' not present in stream.")

            Dim sub_ = compounds(SubstrateCompound)
            Dim ch4 As Compound = Nothing
            Dim cod_ As Compound = Nothing ' alias for co2
            Dim co2 As Compound = Nothing
            Dim nh3 As Compound = Nothing
            Dim h2o As Compound = Nothing
            Dim biom As Compound = Nothing
            Dim h2s As Compound = Nothing

            If Not String.IsNullOrEmpty(MethaneCompound) AndAlso compounds.ContainsKey(MethaneCompound) Then ch4 = compounds(MethaneCompound)
            If Not String.IsNullOrEmpty(CO2Compound) AndAlso compounds.ContainsKey(CO2Compound) Then co2 = compounds(CO2Compound)
            If Not String.IsNullOrEmpty(NH3Compound) AndAlso compounds.ContainsKey(NH3Compound) Then nh3 = compounds(NH3Compound)
            If Not String.IsNullOrEmpty(WaterCompound) AndAlso compounds.ContainsKey(WaterCompound) Then h2o = compounds(WaterCompound)
            If Not String.IsNullOrEmpty(BiomassCompound) AndAlso compounds.ContainsKey(BiomassCompound) Then biom = compounds(BiomassCompound)
            If Not String.IsNullOrEmpty(H2SCompound) AndAlso compounds.ContainsKey(H2SCompound) Then h2s = compounds(H2SCompound)

            If ch4 Is Nothing Then _
                Throw New Exception("AnaerobicDigester: Methane compound '" & MethaneCompound & "' not present in stream.")
            If co2 Is Nothing Then _
                Throw New Exception("AnaerobicDigester: CO2 compound '" & CO2Compound & "' not present in stream.")

            ' Substrate formula
            Dim cpSub = sub_.ConstantProperties
            Dim a As Double = 0, b As Double = 0, c As Double = 0, d As Double = 0
            If cpSub IsNot Nothing AndAlso cpSub.Elements IsNot Nothing Then
                If cpSub.Elements.Contains("C") Then a = Convert.ToDouble(cpSub.Elements("C"))
                If cpSub.Elements.Contains("H") Then b = Convert.ToDouble(cpSub.Elements("H"))
                If cpSub.Elements.Contains("O") Then c = Convert.ToDouble(cpSub.Elements("O"))
                If cpSub.Elements.Contains("N") Then d = Convert.ToDouble(cpSub.Elements("N"))
            End If
            Dim eSulfur As Double = SulfurAtoms(cpSub)
            If a <= 0 Then Throw New Exception("AnaerobicDigester: Substrate compound has no elemental formula - cannot build Buswell stoichiometry.")
            Dim MW_S As Double = cpSub.Molar_Weight

            ' Theoretical COD of the substrate (g COD / g substrate)
            Dim codFactor = TheoreticalCOD(cpSub)
            If codFactor <= 0 Then Throw New Exception("AnaerobicDigester: Theoretical COD of substrate is non-positive.")

            ' Feed COD and COD removed
            Dim m_S_in = sub_.MassFlow.GetValueOrDefault ' kg/s
            Dim Q_liquid_m3s = LiquidVolumetricFlow(ims)
            Dim COD_in_kgs = m_S_in * codFactor
            Dim COD_removed_kgs = COD_in_kgs * Max(0.0, Min(1.0, CODRemovalEfficiency))

            ' Substrate consumed (kg/s)
            Dim dm_S_cons = COD_removed_kgs / codFactor

            ' Biomass (sludge) synthesis (kg/s)
            Dim dm_Biom = COD_removed_kgs * BiomassYield_gVSSpergCOD * 1000.0 / 1000.0 ' g COD Ã— (g VSS / g COD) - keep kg/s
            ' The above is identity because g/g * kg/s = kg/s. Kept explicit for readability.

            ' Mass of substrate actually sent to gas production = total consumed âˆ’ that going to biomass.
            ' Crudely we assume biomass COD equivalent = 1.42 g COD / g VSS, so biomass "consumes"
            ' dm_Biom * 1.42 of the removed COD and the remainder drives Buswell.
            Dim COD_to_gas_kgs = Max(COD_removed_kgs - 1.42 * dm_Biom, 0.0)
            Dim m_S_to_gas = COD_to_gas_kgs / codFactor

            ' Buswell mole coefficients (per mol substrate)
            Dim nCH4 As Double, nCO2 As Double, nH2O As Double, nNH3 As Double, nH2S As Double
            BuswellCoefficients(a, b, c, d, eSulfur, nCH4, nCO2, nH2O, nNH3, nH2S)

            ' Optional user override of methane mole fraction
            If MethaneFractionOverride > 0.0 AndAlso MethaneFractionOverride < 1.0 Then
                Dim tot = nCH4 + nCO2
                If tot > 0 Then
                    nCH4 = tot * MethaneFractionOverride
                    nCO2 = tot * (1.0 - MethaneFractionOverride)
                End If
            End If

            ' Molar flow of substrate to gas
            Dim n_S_gas_mols = m_S_to_gas / (MW_S / 1000.0)

            Dim n_CH4_mols = n_S_gas_mols * nCH4
            Dim n_CO2_mols = n_S_gas_mols * nCO2
            Dim n_H2O_mols = n_S_gas_mols * nH2O   ' consumed (negative mass delta for water)
            Dim n_NH3_mols = n_S_gas_mols * nNH3

            ' ---- Sulfur balance ----
            ' Organic sulfur bound in the substrate formula needs no special handling: Buswell
            ' already releases it as H2S and already charges its electrons against methane through
            ' the -e/4 term in nCH4. Only sulfate sulfur and the no-formula override are extra.
            Dim nSO4_kmols As Double, nOrgS_kmols As Double, codFactorExtra As Double
            SulfurLoad(cpSub, m_S_in, Q_liquid_m3s, nSO4_kmols, nOrgS_kmols, codFactorExtra)

            Dim n_H2S_mols = n_S_gas_mols * nH2S                     ' organic S, from Buswell
            If eSulfur <= 0.0 Then n_H2S_mols += nOrgS_kmols * 1000.0 ' organic S declared by override

            ' Sulfate reduction: one mol of sulfate-S takes 8 electrons, i.e. exactly the 64 g COD
            ' of one mol of CH4. So each mol reduced costs one mol of methane and its carbon leaves
            ' as CO2 instead. That single identity is the whole sulfate-to-methane penalty.
            Dim nSO4_mols = nSO4_kmols * 1000.0
            If nSO4_mols > 0.0 Then
                If nSO4_mols > n_CH4_mols Then
                    FlowSheet?.ShowMessage(String.Format(
                        "{0}: influent sulfate ({1:G4} mol S/s) exceeds the methane the substrate can " &
                        "produce ({2:G4} mol/s). Sulfate reduction is being capped at the available " &
                        "electron supply; this simplified balance cannot represent a sulfate-limited " &
                        "digester.", Me.GraphicObject.Tag, nSO4_mols, n_CH4_mols),
                        IFlowsheet.MessageType.Warning)
                    nSO4_mols = n_CH4_mols
                End If
                n_CH4_mols -= nSO4_mols
                n_CO2_mols += nSO4_mols
                n_H2S_mols += nSO4_mols
            End If

            ' Sulfide already dissolved in the feed joins the sulfide pool before the equilibrium split,
            ' so it is partitioned rather than stranded in the liquid (as it used to be). CO2 and NH3
            ' fed in are routed to the outlets further down, not folded into the produced pools, so the
            ' reported biogas composition stays a property of the digestion, not of the feed gas.
            If h2s IsNot Nothing Then n_H2S_mols += h2s.MassFlow.GetValueOrDefault / MW_H2S * 1000.0

            ' Gas-liquid equilibrium of the weak-electrolyte gases at the assumed digester pH. Only the
            ' volatile fraction of each obeys Henry's law, so most of the H2S and NH3 and part of the
            ' CO2 stay in the effluent as HS-/S(2-), NH4+ and HCO3-/CO3(2-). ADM1 constants, corrected to T.
            Dim phBB = ADM1.ADM1Equations.TemperatureCorrect(ADM1Params.Physicochemical, T)
            Dim S_H_bb = 10.0 ^ (-Max(AssumedPH_ForSulfide, 0.1))
            Dim Pbar = P / 100000.0
            Dim nGasRef = (n_CH4_mols + n_CO2_mols) / 1000.0                    ' dry-gas reference, kmol/s
            Dim f_h2s = S_H_bb / (phBB.K_a_h2s + S_H_bb)                        ' undissociated H2S
            Dim f_co2 = S_H_bb / (phBB.K_a_co2 + S_H_bb)                        ' dissolved CO2 (vs HCO3-)
            Dim f_nh3 = phBB.K_a_IN / (phBB.K_a_IN + S_H_bb)                    ' free NH3 (vs NH4+)

            Dim nH2SGas_kmols As Double, cSulfideLiq As Double
            PartitionVolatile(n_H2S_mols / 1000.0, Q_liquid_m3s, nGasRef, Pbar, phBB.K_H_h2s, f_h2s, nH2SGas_kmols, cSulfideLiq)
            Dim nCO2Gas_kmols As Double, cCO2Liq As Double
            PartitionVolatile(n_CO2_mols / 1000.0, Q_liquid_m3s, nGasRef, Pbar, phBB.K_H_co2, f_co2, nCO2Gas_kmols, cCO2Liq)
            Dim nNH3Gas_kmols As Double, cNH3Liq As Double
            PartitionVolatile(n_NH3_mols / 1000.0, Q_liquid_m3s, nGasRef, Pbar, phBB.K_H_nh3, f_nh3, nNH3Gas_kmols, cNH3Liq)

            Dim dm_H2S_gas = nH2SGas_kmols * MW_H2S                                     ' kg/s
            Dim dm_H2S_liq = Max(n_H2S_mols / 1000.0 - nH2SGas_kmols, 0.0) * MW_H2S
            Dim dm_CO2_gas = nCO2Gas_kmols * 1000.0 * 0.04401
            Dim dm_CO2_liq = Max(n_CO2_mols - nCO2Gas_kmols * 1000.0, 0.0) * 0.04401
            Dim dm_NH3_gas = nNH3Gas_kmols * 1000.0 * 0.01703
            Dim dm_NH3_liq = Max(n_NH3_mols - nNH3Gas_kmols * 1000.0, 0.0) * 0.01703

            ' Reported dry biogas (CH4 plus the volatile CO2, H2S and NH3 that made it to the gas).
            Dim nDryGas_mols = n_CH4_mols + nCO2Gas_kmols * 1000.0 + nH2SGas_kmols * 1000.0 + nNH3Gas_kmols * 1000.0

            Dim dm_CH4 = n_CH4_mols * 0.01604 ' kg/s
            Dim dm_H2O = -n_H2O_mols * 0.01802 ' water consumed by hydrolysis

            ' Results
            Result_CODin_kgs = COD_in_kgs
            Result_CODremoved_kgs = COD_removed_kgs
            Result_SubstrateConsumed_kgs = dm_S_cons
            Result_BiogasFlow_mols = nDryGas_mols
            Result_CH4_kgs = dm_CH4
            Result_CO2_kgs = dm_CO2_gas
            If Result_BiogasFlow_mols > 0 Then
                Result_CH4MoleFraction = n_CH4_mols / Result_BiogasFlow_mols
            Else
                Result_CH4MoleFraction = 0.0
            End If
            If COD_removed_kgs > 0 Then
                ' 22.414 L/mol at STP Ã— mol/s / kg_COD/s = L/kg Ã— (1 Nm3/1000 L) = Nm3/kg
                Result_SpecificCH4Yield_Nm3kgCOD = (n_CH4_mols * 0.022414) / COD_removed_kgs
            Else
                Result_SpecificCH4Yield_Nm3kgCOD = 0.0
            End If
            Result_Sludge_kgs = dm_Biom
            Result_H2S_kgs = dm_H2S_gas
            Result_H2S_ppmv = If(Result_BiogasFlow_mols > 0,
                                 nH2SGas_kmols * 1000.0 / Result_BiogasFlow_mols * 1000000.0, 0.0)
            Result_DissolvedSulfide_kgSm3 = cSulfideLiq * MW_Sulfur

            ' -------------------------------------------------------
            ' Build effluent (liquid) and biogas mass-flow dictionaries
            ' -------------------------------------------------------
            Dim effMass As New Dictionary(Of String, Double)
            Dim gasMass As New Dictionary(Of String, Double)
            For Each kvp In compounds
                effMass(kvp.Key) = kvp.Value.MassFlow.GetValueOrDefault
                gasMass(kvp.Key) = 0.0
            Next

            ' Consume substrate in liquid
            effMass(sub_.Name) = Max(effMass(sub_.Name) - dm_S_cons, 0.0)

            ' Water: consumed in hydrolysis, stays in the effluent
            If h2o IsNot Nothing Then effMass(h2o.Name) = Max(effMass(h2o.Name) + dm_H2O, 0.0)

            ' NH3: the produced NH3 that volatilizes leaves in the biogas; the rest stays dissolved,
            ' together with any ammonia that arrived in the feed.
            If nh3 IsNot Nothing Then
                gasMass(nh3.Name) = dm_NH3_gas
                effMass(nh3.Name) = Max(effMass(nh3.Name) + dm_NH3_liq, 0.0)
            End If

            ' Biomass (sludge): stays in effluent
            If biom IsNot Nothing Then effMass(biom.Name) = Max(effMass(biom.Name) + dm_Biom, 0.0)

            ' CH4 all to biogas; produced CO2 partitioned, any CO2 that came in with the feed degasses too
            gasMass(ch4.Name) = effMass(ch4.Name) + dm_CH4
            effMass(ch4.Name) = 0.0
            gasMass(co2.Name) = effMass(co2.Name) + dm_CO2_gas
            effMass(co2.Name) = dm_CO2_liq

            ' H2S: assigned outright on both sides rather than incremented, because the feed's own
            ' sulfide was folded into the pool before it was partitioned.
            If h2s IsNot Nothing Then
                gasMass(h2s.Name) = dm_H2S_gas
                effMass(h2s.Name) = dm_H2S_liq
            End If

            ' Close mass balance: Buswell is atom-balanced for the gas path, but the
            ' biomass synthesis path creates a small residual because substrate-to-biomass
            ' mass != biomass mass produced. Route the residual to a balancing species in
            ' the effluent (preferred order: water, biomass, largest non-gas component).
            Dim totalEffMass As Double = 0.0
            Dim totalGasMass As Double = 0.0
            For Each v In effMass.Values : totalEffMass += v : Next
            For Each v In gasMass.Values : totalGasMass += v : Next
            Dim feedMass As Double = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim massResidual = feedMass - totalEffMass - totalGasMass
            If Abs(massResidual) > 1.0E-12 Then
                Dim balKey As String = Nothing
                If h2o IsNot Nothing Then balKey = h2o.Name
                If balKey Is Nothing AndAlso biom IsNot Nothing Then balKey = biom.Name
                If balKey Is Nothing Then
                    Dim maxV As Double = -1.0
                    For Each kv In effMass
                        If kv.Key <> ch4.Name AndAlso kv.Key <> co2.Name AndAlso
                           (h2s Is Nothing OrElse kv.Key <> h2s.Name) AndAlso kv.Value > maxV Then
                            maxV = kv.Value : balKey = kv.Key
                        End If
                    Next
                End If
                If balKey IsNot Nothing Then
                    effMass(balKey) = Max(effMass(balKey) + massResidual, 0.0)
                    totalEffMass = 0.0
                    For Each v In effMass.Values : totalEffMass += v : Next
                End If
            End If

            ' -------------------------------------------------------
            ' THERMAL BALANCE
            ' -------------------------------------------------------
            Dim Q_met_W As Double = Abs(HeatPerGCODremoved_Jg) * (COD_removed_kgs * 1000.0) ' J/g Ã— g/s = W
            If HeatPerGCODremoved_Jg > 0.0 Then Q_met_W = -Q_met_W
            Result_Q_metabolic_kW = Q_met_W / 1000.0

            Dim cp_L_mass As Double = 0.0
            Try : cp_L_mass = ims.Phases(1).Properties.heatCapacityCp.GetValueOrDefault * 1000.0 : Catch : End Try
            If cp_L_mass <= 0.0 Then
                Try : cp_L_mass = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault * 1000.0 : Catch : End Try
            End If
            If cp_L_mass <= 0.0 Then cp_L_mass = 4180.0

            Dim rho_L = ims.Phases(1).Properties.density.GetValueOrDefault
            If rho_L <= 0.0 Then rho_L = 1000.0
            Dim m_dot = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim m_holdup = rho_L * Volume
            Dim tau = If(m_dot > 0.0, m_holdup / m_dot, HRT_s)

            Dim T_in_K = T
            Dim T_out_K = T_in_K
            Dim Q_duty_W = 0.0

            Select Case ThermalMode
                Case BioReactorThermalMode.Isothermal
                    T_out_K = T_in_K
                    Q_duty_W = -Q_met_W
                Case BioReactorThermalMode.Adiabatic
                    Q_duty_W = 0.0
                    If m_dot > 0.0 Then T_out_K = T_in_K + Q_met_W / (m_dot * cp_L_mass)
                Case BioReactorThermalMode.DefinedOutletTemperature
                    If OutletTemperature > 0.0 Then T_out_K = OutletTemperature Else T_out_K = T_in_K
                    Q_duty_W = m_dot * cp_L_mass * (T_out_K - T_in_K) - Q_met_W
            End Select

            Result_OutletTemperature_K = T_out_K
            Result_Q_duty_kW = Q_duty_W / 1000.0

            ' -------------------------------------------------------
            ' Push to outlet streams (Effluent index 0, Biogas index 1)
            ' -------------------------------------------------------
            Dim cpEff = Me.GraphicObject.OutputConnectors(0)
            If cpEff.IsAttached Then
                Dim msEff As MaterialStream = FlowSheet.SimulationObjects(cpEff.AttachedConnector.AttachedTo.Name)
                With msEff
                    .ClearAllProps()
                    .Phases(0).Properties.temperature = T_out_K
                    .Phases(0).Properties.pressure = P
                    If totalEffMass > 0 Then
                        For Each comp In .Phases(0).Compounds.Values
                            If effMass.ContainsKey(comp.Name) Then
                                comp.MassFraction = effMass(comp.Name) / totalEffMass
                            Else
                                comp.MassFraction = 0.0
                            End If
                        Next
                        Dim invMWsum As Double = 0.0
                        For Each comp In .Phases(0).Compounds.Values
                            invMWsum += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
                        Next
                        For Each comp In .Phases(0).Compounds.Values
                            comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsum
                        Next
                    End If
                    .Phases(0).Properties.massflow = totalEffMass
                    .DefinedFlow = FlowSpec.Mass
                    .SpecType = StreamSpec.Temperature_and_Pressure
                End With
            End If

            If Me.GraphicObject.OutputConnectors.Count > 1 Then
                Dim cpGas = Me.GraphicObject.OutputConnectors(1)
                If cpGas.IsAttached Then
                    Dim msGas As MaterialStream = FlowSheet.SimulationObjects(cpGas.AttachedConnector.AttachedTo.Name)
                    With msGas
                        .ClearAllProps()
                        .Phases(0).Properties.temperature = T_out_K
                        .Phases(0).Properties.pressure = P
                        If totalGasMass > 0 Then
                            For Each comp In .Phases(0).Compounds.Values
                                If gasMass.ContainsKey(comp.Name) Then
                                    comp.MassFraction = gasMass(comp.Name) / totalGasMass
                                Else
                                    comp.MassFraction = 0.0
                                End If
                            Next
                            Dim invMWsumG As Double = 0.0
                            For Each comp In .Phases(0).Compounds.Values
                                invMWsumG += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
                            Next
                            For Each comp In .Phases(0).Compounds.Values
                                comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsumG
                            Next
                        End If
                        .Phases(0).Properties.massflow = totalGasMass
                        .DefinedFlow = FlowSpec.Mass
                        .SpecType = StreamSpec.Temperature_and_Pressure
                    End With
                End If
            End If

            ' Energy stream
            DeltaQ = Result_Q_duty_kW
            Try
                Dim es = GetInletEnergyStream(1)
                If es IsNot Nothing Then
                    es.EnergyFlow = Result_Q_duty_kW
                    es.GraphicObject.Calculated = True
                End If
            Catch ex As ArgumentOutOfRangeException
            End Try

            OutletTemperature = T_out_K

        End Sub

        ''' <summary>
        ''' ADM1-Lite reduced model. Four populations and four soluble substrates (lumped):
        '''   Hydrolysis (1st order): Particulate substrate  â†’ S_s
        '''   Acidogenesis:            S_s     â†’ 0.60 S_VFA + 0.32 S_Ac + 0.08 S_H2 + CO2   [by X_hyd]
        '''   Acetogenesis (H2 inh.):  S_VFA   â†’ 0.75 S_Ac  + 0.25 S_H2                     [by X_ace]
        '''   Acetoclastic MG:         S_Ac    â†’ CH4 + CO2                                  [by X_am]
        '''   Hydrogenotrophic MG:     S_H2 + CO2 â†’ CH4                                     [by X_hm]
        ''' Monod kinetics with non-competitive H2 inhibition on acetogens
        '''   (I_h2 = 1 / (1 + S_H2/K_I_h2)). All populations decay 1st-order.
        ''' Integration: forward Euler over HRT (continuous) or BatchDuration (batch),
        ''' with step size tau/NSTEP and NSTEP = 20000 to remain stable in presence of
        ''' fast H2 dynamics. All state fluxes are COD-balanced (g COD/L).
        ''' </summary>
        Private Sub CalculateADM1Lite()

            If String.IsNullOrEmpty(SubstrateCompound) Then
                Throw New Exception("AnaerobicDigester (ADM1-Lite): Organic Substrate compound not selected.")
            End If
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception("AnaerobicDigester (ADM1-Lite): Feed stream not connected.")
            End If
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("AnaerobicDigester (ADM1-Lite): Both Effluent and Biogas outlets must be connected.")
            End If

            Dim ims As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.SetFlowsheet(Me.FlowSheet)
            ims.SetPropertyPackage(PropertyPackage)
            PropertyPackage.CurrentMaterialStream = ims
            ims.DefinedFlow = FlowSpec.Mass

            Dim T = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P0 = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            Try
                ims.Calculate(True, True)
            Catch ex As Exception
            End Try

            Dim compounds = ims.Phases(0).Compounds

            If Not compounds.ContainsKey(SubstrateCompound) Then _
                Throw New Exception("AnaerobicDigester (ADM1-Lite): Substrate '" & SubstrateCompound & "' not in stream.")

            Dim sub_ = compounds(SubstrateCompound)
            Dim cpSub = sub_.ConstantProperties
            If cpSub Is Nothing Then Throw New Exception("AnaerobicDigester (ADM1-Lite): Substrate compound has no ConstantProperties.")

            ' Substrate elemental formula (used for carbon balance on CO2 after COD allocation)
            Dim aSub As Double = 0, bSub As Double = 0, cSub As Double = 0, dSub As Double = 0
            If cpSub.Elements IsNot Nothing Then
                If cpSub.Elements.Contains("C") Then aSub = Convert.ToDouble(cpSub.Elements("C"))
                If cpSub.Elements.Contains("H") Then bSub = Convert.ToDouble(cpSub.Elements("H"))
                If cpSub.Elements.Contains("O") Then cSub = Convert.ToDouble(cpSub.Elements("O"))
                If cpSub.Elements.Contains("N") Then dSub = Convert.ToDouble(cpSub.Elements("N"))
            End If
            Dim MW_Sub As Double = cpSub.Molar_Weight ' g/mol
            Dim subCfrac As Double = If(MW_Sub > 0.0 AndAlso aSub > 0.0, aSub * 12.0 / MW_Sub, 0.0) ' g C / g substrate

            Dim m_sub_in = sub_.MassFlow.GetValueOrDefault ' kg/s

            ' Liquid volumetric flow (mÂ³/s)
            Dim Q_liquid = LiquidVolumetricFlow(ims)

            ' Sulfur load. Like ADM1-Full and unlike BlackBox, this path sees COD as a single lump
            ' rather than as Buswell stoichiometry, so the sulfide electrons have to be debited
            ' explicitly instead of falling out of the -e/4 methane term.
            Dim h2s As Compound = Nothing
            If Not String.IsNullOrEmpty(H2SCompound) AndAlso compounds.ContainsKey(H2SCompound) Then h2s = compounds(H2SCompound)
            Dim nSO4_kmols As Double, nOrgS_kmols As Double, codFactorExtra As Double
            SulfurLoad(cpSub, m_sub_in, Q_liquid, nSO4_kmols, nOrgS_kmols, codFactorExtra)
            Dim nS_total_kmols = nSO4_kmols + nOrgS_kmols
            If h2s IsNot Nothing Then nS_total_kmols += h2s.MassFlow.GetValueOrDefault / MW_H2S

            ' Feed COD flux (kg COD/s) using the shared theoretical-COD formula
            Dim codFactor = TheoreticalCOD(cpSub) + codFactorExtra
            If codFactor <= 0 Then Throw New Exception("AnaerobicDigester (ADM1-Lite): Theoretical COD of substrate is non-positive.")
            Dim CODin_kgs = m_sub_in * codFactor

            ' Inlet substrate COD concentration (g COD/L), net of the electrons the sulfide carries.
            Dim c_IS_in = nS_total_kmols / Q_liquid                            ' kmol S/mÂ³
            Dim codDebit = ADM1.ADM1State.COD_per_kmol_S * c_IS_in            ' kg COD/mÂ³
            Dim S_s_feed = CODin_kgs / Q_liquid ' kg COD / mÂ³ = g COD/L
            If codDebit > S_s_feed Then
                FlowSheet?.ShowMessage(String.Format(
                    "{0}: the sulfur load needs {1:G4} kg COD/mÂ³ of electrons but the feed only supplies " &
                    "{2:G4}. The debit is being capped; this simplified balance cannot represent a " &
                    "sulfate-limited digester.", Me.GraphicObject.Tag, codDebit, S_s_feed),
                    IFlowsheet.MessageType.Warning)
                codDebit = S_s_feed
            End If
            S_s_feed -= codDebit

            ' Retention time (s) - for CSTR consistency, tau must equal V/Q.
            ' If user specified both Volume and HRT, use V/Q and warn if they differ.
            Dim tau_s As Double
            If Volume > 0 AndAlso Q_liquid > 0 Then
                tau_s = Volume / Q_liquid
                If HRT_s > 0 AndAlso Abs(tau_s - HRT_s) / Max(tau_s, 1.0) > 0.05 Then
                    FlowSheet.ShowMessage(String.Format(
                        "AnaerobicDigester '{0}': HRT ({1:F1} d) differs from V/Q ({2:F1} d) by >{3}%. Using V/Q for CSTR consistency.",
                        Me.GraphicObject.Tag, HRT_s / 86400.0, tau_s / 86400.0, 5), IFlowsheet.MessageType.Warning)
                End If
            ElseIf HRT_s > 0 Then
                tau_s = HRT_s
            Else
                tau_s = 86400.0 * 20.0
            End If
            HRT_s = tau_s
            Dim tau_d = tau_s / 86400.0 ' days, since ADM1 kinetic constants are in 1/d

            ' Convert per-day kinetic constants to per-second
            Dim k_hyd = ADM1_k_hyd_d / 86400.0
            Dim km_su = ADM1_km_su_d / 86400.0
            Dim km_vfa = ADM1_km_vfa_d / 86400.0
            Dim km_ac = ADM1_km_ac_d / 86400.0
            Dim km_h2 = ADM1_km_h2_d / 86400.0
            Dim k_dec = ADM1_k_dec_d / 86400.0

            ' State
            Dim S_s = ADM1_S_s0, S_VFA = ADM1_S_VFA0, S_Ac = ADM1_S_Ac0, S_H2 = ADM1_S_H20
            Dim X_hyd = ADM1_X_hyd0, X_ace = ADM1_X_ace0, X_am = ADM1_X_am0, X_hm = ADM1_X_hm0

            ' Accumulators (in g COD / L over the integration, multiplied at the end by Q or V)
            Dim cum_CH4_Ac = 0.0 ' CH4 COD from acetoclastic MG (g COD/L consumed via S_Ac pathway)
            Dim cum_CH4_H2 = 0.0 ' CH4 COD from hydrogenotrophic MG
            Dim cum_CO2_mol = 0.0 ' cumulative mol CO2/L produced (stoich)
            Dim cum_NH3_mol = 0.0
            Dim cum_H2O_mol = 0.0
            Dim cum_X_decayed = 0.0 ' cumulative biomass decayed (g VSS/L), goes to inert sludge in effluent

            ' Dilution rate D = 1/tau (only applies to continuous feed; for batch D=0)
            Dim continuous = (OperatingMode_Digester() = "Continuous")
            Dim D_sec As Double = If(continuous AndAlso tau_s > 0, 1.0 / tau_s, 0.0)

            ' Integration time
            Dim t_end_s As Double = If(continuous, Max(tau_s * 10.0, 86400.0), tau_s) ' 10Ã— HRT to reach SS, or batch time
            Dim nsteps As Integer = 20000
            Dim dt As Double = t_end_s / nsteps

            ' Stoichiometric yields on COD basis (per g COD consumed from substrate of that step):
            ' Acidogenesis output fractions (must sum to ~1 minus biomass yield): VFA 0.60, Ac 0.32, H2 0.08
            Const f_vfa As Double = 0.6
            Const f_ac_acid As Double = 0.32
            Const f_h2_acid As Double = 0.08
            ' Acetogenesis: 0.75 Ac + 0.25 H2
            Const f_ac_ace As Double = 0.75
            Const f_h2_ace As Double = 0.25

            Dim maxTestUpt As Double = 0.0 ' guard rail
            For step_idx As Integer = 1 To nsteps

                ' Inhibition factor (non-competitive H2 inhibition on acetogens)
                Dim I_h2 = 1.0 / (1.0 + S_H2 / Max(ADM1_KI_h2, 0.0000000001))

                ' Specific uptake rates (g COD / g VSS / s)
                Dim rho_su = km_su * S_s / (ADM1_Ks_su + Max(S_s, 0.0)) * X_hyd
                Dim rho_vfa = km_vfa * S_VFA / (ADM1_Ks_vfa + Max(S_VFA, 0.0)) * X_ace * I_h2
                Dim rho_ac = km_ac * S_Ac / (ADM1_Ks_ac + Max(S_Ac, 0.0)) * X_am
                Dim rho_h2 = km_h2 * S_H2 / (ADM1_Ks_h2 + Max(S_H2, 0.0)) * X_hm

                Dim r_hyd As Double
                If continuous Then
                    r_hyd = D_sec * S_s_feed
                Else
                    r_hyd = k_hyd * Max(S_s_feed - S_s, 0.0)
                End If

                ' Patankar-linearized substrate update (positivity-preserving, unconditionally stable)
                Dim d_su = If(S_s > 1.0E-30, rho_su / S_s, 0.0)
                Dim d_vfa = If(S_VFA > 1.0E-30, rho_vfa / S_VFA, 0.0)
                Dim d_ac = If(S_Ac > 1.0E-30, rho_ac / S_Ac, 0.0)
                Dim d_h2 = If(S_H2 > 1.0E-30, rho_h2 / S_H2, 0.0)

                S_s = (S_s + r_hyd * dt) / (1.0 + (d_su + D_sec) * dt)
                S_VFA = (S_VFA + f_vfa * rho_su * dt) / (1.0 + (d_vfa + D_sec) * dt)
                S_Ac = (S_Ac + (f_ac_acid * rho_su + f_ac_ace * rho_vfa) * dt) / (1.0 + (d_ac + D_sec) * dt)
                S_H2 = (S_H2 + (f_h2_acid * rho_su + f_h2_ace * rho_vfa) * dt) / (1.0 + (d_h2 + D_sec) * dt)

                ' Recompute uptake rates with depleted substrates for biomass update
                Dim I_h2b = 1.0 / (1.0 + S_H2 / Max(ADM1_KI_h2, 0.0000000001))
                Dim rho_su2 = km_su * S_s / (ADM1_Ks_su + Max(S_s, 0.0)) * X_hyd
                Dim rho_vfa2 = km_vfa * S_VFA / (ADM1_Ks_vfa + Max(S_VFA, 0.0)) * X_ace * I_h2b
                Dim rho_ac2 = km_ac * S_Ac / (ADM1_Ks_ac + Max(S_Ac, 0.0)) * X_am
                Dim rho_h22 = km_h2 * S_H2 / (ADM1_Ks_h2 + Max(S_H2, 0.0)) * X_hm

                ' Biomass semi-implicit (growth with depleted-S rates, decay+washout implicit).
                ' Decayed biomass (k_dec * X) is treated as inert in this lumped model: in real
                ' ADM1 it would re-enter S_s via composite-particulate hydrolysis, but adding it
                ' directly causes a positive-feedback overcount of CH4 because X is in g VSS/L
                ' while S_s is in g COD/L (different units, ~1.42x conversion).
                X_hyd = Max((X_hyd + ADM1_Y_su * rho_su2 * dt) / (1.0 + (k_dec + D_sec) * dt), 0.0)
                X_ace = Max((X_ace + ADM1_Y_ace * rho_vfa2 * dt) / (1.0 + (k_dec + D_sec) * dt), 0.0)
                X_am = Max((X_am + ADM1_Y_am * rho_ac2 * dt) / (1.0 + (k_dec + D_sec) * dt), 0.0)
                X_hm = Max((X_hm + ADM1_Y_hm * rho_h22 * dt) / (1.0 + (k_dec + D_sec) * dt), 0.0)
                ' Track biomass lost to decay (k_dec * X) so it can be reported in the
                ' effluent as inert sludge; consistent with the implicit-Euler step above.
                cum_X_decayed += k_dec * (X_hyd + X_ace + X_am + X_hm) * dt

                ' Accumulate CH4 (COD basis) using average of old and new uptake rates
                Dim rho_ac_avg = 0.5 * (rho_ac + rho_ac2)
                Dim rho_h2_avg = 0.5 * (rho_h2 + rho_h22)
                cum_CH4_Ac += rho_ac_avg * dt
                cum_CH4_H2 += rho_h2_avg * dt
                maxTestUpt = Max(maxTestUpt, rho_ac_avg + rho_h2_avg)
            Next

            ' Convert CH4 COD to mass of CH4. COD of CH4: 64 g COD / 16 g CH4 = 4 g COD / g CH4.
            Const COD_per_CH4 As Double = 4.0
            Dim cum_CH4_kgL = (cum_CH4_Ac + cum_CH4_H2) / COD_per_CH4 / 1000.0 ' kg CH4/L (accumulated over t_end for batch; per-unit-volume-per-second â‰ˆ for continuous)

            Dim m_CH4_kgs As Double
            Dim m_CO2_kgs As Double
            Dim m_biomass_kgs As Double
            Dim m_decayed_kgs As Double = 0.0 ' decayed (lysed) biomass leaving as inert sludge

            If continuous Then
                ' Steady-state rates: CH4 volumetric production rate (kg/mÂ³/s) Ã— Q gives kg/s
                ' cum_CH4 integrated over 10Ã— HRT; divide by 10Ã— HRT to get rate per second
                Dim CH4_rate_gCODLs = (cum_CH4_Ac + cum_CH4_H2) / t_end_s
                Dim CH4_rate_kgCODs = CH4_rate_gCODLs / 1000.0 * (Q_liquid * tau_s) ' g COD/L/s Ã— L(=mÂ³Â·1000) Ã— tau = integral? simpler:
                ' CH4 rate at steady-state (kg CH4/s) = CH4_rate_gCODLs (g COD/L/s) Ã— V_broth (L) / COD_per_CH4
                Dim V_L = Volume * 1000.0
                Dim m_CH4_kgCOD_per_s = CH4_rate_gCODLs / 1000.0 * V_L ' kg COD/s
                m_CH4_kgs = m_CH4_kgCOD_per_s / COD_per_CH4

                ' CO2 from acetoclastic MG: 1 mol CH4 + 1 mol CO2 per mol acetate (44 g CO2 : 16 g CH4 molar),
                ' but on COD basis acetoclastic path gives 1 CH4 + 1 CO2 in moles. Approx mass ratio:
                ' CO2 produced from Ac pathway â‰ˆ 44/16 Ã— CH4 mass from that pathway
                Dim CH4_Ac_rate = cum_CH4_Ac / t_end_s / 1000.0 * V_L / COD_per_CH4 ' kg CH4/s from Ac
                Dim CH4_H2_rate = cum_CH4_H2 / t_end_s / 1000.0 * V_L / COD_per_CH4 ' kg CH4/s from H2
                ' Hydrogenotrophic consumes CO2 (1 mol CO2 per mol CH4) so net CO2 = Ac-path CO2 âˆ’ H2-path CO2
                m_CO2_kgs = CH4_Ac_rate * (44.0 / 16.0) - CH4_H2_rate * (44.0 / 16.0)
                If m_CO2_kgs < 0 Then m_CO2_kgs = 0.0

                ' Biomass production rate (kg/s): sum of growth minus decay of all populations Ã— Volume
                m_biomass_kgs = (ADM1_Y_su + ADM1_Y_ace + ADM1_Y_am + ADM1_Y_hm) * 0.25 *
                                CH4_rate_gCODLs / 1000.0 * V_L * 0.3 ' heuristic 30% yield Ã— weighted
                ' simpler: use endogenous balance XÂ·D at SS
                m_biomass_kgs = (X_hyd + X_ace + X_am + X_hm) / 1000.0 * V_L * D_sec
                ' Decayed biomass rate (kg VSS/s): cum_X_decayed in g VSS/L over t_end_s, Ã— V_L
                m_decayed_kgs = cum_X_decayed / t_end_s / 1000.0 * V_L

            Else
                ' Batch: total CH4 produced over BatchDuration (kg)
                Dim V_L = Volume * 1000.0
                Dim total_CH4_kgCOD = (cum_CH4_Ac + cum_CH4_H2) / 1000.0 * V_L ' kg COD
                Dim total_CH4_kg = total_CH4_kgCOD / COD_per_CH4
                m_CH4_kgs = total_CH4_kg / tau_s
                Dim total_CH4_Ac_kg = cum_CH4_Ac / 1000.0 * V_L / COD_per_CH4
                Dim total_CH4_H2_kg = cum_CH4_H2 / 1000.0 * V_L / COD_per_CH4
                m_CO2_kgs = (total_CH4_Ac_kg * (44.0 / 16.0) - total_CH4_H2_kg * (44.0 / 16.0)) / tau_s
                If m_CO2_kgs < 0 Then m_CO2_kgs = 0.0
                m_biomass_kgs = (X_hyd + X_ace + X_am + X_hm - ADM1_X_hyd0 - ADM1_X_ace0 - ADM1_X_am0 - ADM1_X_hm0) / 1000.0 * V_L / tau_s
                If m_biomass_kgs < 0 Then m_biomass_kgs = 0.0
                m_decayed_kgs = cum_X_decayed / 1000.0 * V_L / tau_s
            End If

            ' Save final state
            ADM1_Result_S_s = S_s
            ADM1_Result_S_VFA = S_VFA
            ADM1_Result_S_Ac = S_Ac
            ADM1_Result_S_H2 = S_H2
            ADM1_Result_X_hyd = X_hyd
            ADM1_Result_X_ace = X_ace
            ADM1_Result_X_am = X_am
            ADM1_Result_X_hm = X_hm
            ' Crude pH: pH = 7 âˆ’ log10(1 + S_VFAÂ·5) (lower pH when VFA accumulates; only for reporting)
            ADM1_Result_pH = 7.0 - Log10(1.0 + S_VFA * 5.0)

            ' COD removed = feed COD âˆ’ effluent soluble COD  (assumes particulate fully hydrolysed â‰ˆ feed)
            Dim CODeff_gL = S_s + S_VFA + S_Ac + S_H2
            Dim CODeff_kgs = CODeff_gL * Q_liquid ' g/L = kg/m3; kg/m3 * m3/s = kg/s
            Dim COD_removed_kgs = Max(CODin_kgs - CODeff_kgs, 0.0)

            ' Recompute CO2 from a carbon balance, replacing the simple 44/16 mass-ratio
            ' approximation.  After COD has been allocated to CH4 and to biomass (live + decayed),
            ' the remaining carbon from the substrate consumed must appear as CO2:
            '   C(substrate consumed) = C(CH4) + C(biomass + decayed) + C(CO2)
            ' Biomass carbon fraction defaults to C5H7NO2 (typical VSS) when no formula available.
            Dim subConsumed_kgs As Double = COD_removed_kgs / codFactor
            Dim C_consumed_kgs = subCfrac * subConsumed_kgs
            Dim biomCfrac As Double = 0.531 ' C5H7NO2: 5*12/113.12
            If Not String.IsNullOrEmpty(BiomassCompound) AndAlso compounds.ContainsKey(BiomassCompound) Then
                Dim cpB = compounds(BiomassCompound).ConstantProperties
                If cpB IsNot Nothing AndAlso cpB.Elements IsNot Nothing AndAlso cpB.Molar_Weight > 0.0 Then
                    Dim aB As Double = 0
                    If cpB.Elements.Contains("C") Then aB = Convert.ToDouble(cpB.Elements("C"))
                    If aB > 0 Then biomCfrac = aB * 12.0 / cpB.Molar_Weight
                End If
            End If
            Dim C_biomass_kgs = biomCfrac * (m_biomass_kgs + m_decayed_kgs)
            Dim C_CH4_kgs = (12.0 / 16.04) * m_CH4_kgs
            Dim C_CO2_kgs = Max(C_consumed_kgs - C_biomass_kgs - C_CH4_kgs, 0.0)
            If subCfrac > 0.0 Then m_CO2_kgs = (44.01 / 12.0) * C_CO2_kgs

            ' Split the sulfide between biogas and effluent at equilibrium. The COD debit above
            ' already removed these electrons from the methane pool, so nothing to subtract here.
            Dim nH2SGas_kmols As Double, cSulfideLiq As Double
            PartitionSulfide(nS_total_kmols, Q_liquid,
                             (m_CH4_kgs / 0.01604 + m_CO2_kgs / 0.04401) / 1000.0,
                             P / 100000.0, T, nH2SGas_kmols, cSulfideLiq)
            Dim m_H2S_gas_kgs = nH2SGas_kmols * MW_H2S
            Dim m_H2S_liq_kgs = Max(nS_total_kmols - nH2SGas_kmols, 0.0) * MW_H2S

            ' Populate the standard result vector so downstream reporting and the energy balance work unchanged
            Result_CODin_kgs = CODin_kgs
            Result_CODremoved_kgs = COD_removed_kgs
            Result_SubstrateConsumed_kgs = subConsumed_kgs
            Result_CH4_kgs = m_CH4_kgs
            Result_CO2_kgs = m_CO2_kgs
            Result_Sludge_kgs = m_biomass_kgs + m_decayed_kgs
            Result_BiogasFlow_mols = m_CH4_kgs / 0.01604 + m_CO2_kgs / 0.04401 + nH2SGas_kmols * 1000.0
            If Result_BiogasFlow_mols > 0 Then
                Result_CH4MoleFraction = (m_CH4_kgs / 0.01604) / Result_BiogasFlow_mols
            Else
                Result_CH4MoleFraction = 0.0
            End If
            If COD_removed_kgs > 0 Then
                Result_SpecificCH4Yield_Nm3kgCOD = ((m_CH4_kgs / 0.01604) * 0.022414) / COD_removed_kgs
            Else
                Result_SpecificCH4Yield_Nm3kgCOD = 0.0
            End If
            Result_H2S_kgs = m_H2S_gas_kgs
            Result_H2S_ppmv = If(Result_BiogasFlow_mols > 0,
                                 nH2SGas_kmols * 1000.0 / Result_BiogasFlow_mols * 1000000.0, 0.0)
            Result_DissolvedSulfide_kgSm3 = cSulfideLiq * MW_Sulfur

            ' Build effluent + biogas mass dictionaries (identical plumbing to BlackBox path)
            Dim ch4 As Compound = Nothing, co2 As Compound = Nothing, h2o As Compound = Nothing
            Dim biom As Compound = Nothing, nh3 As Compound = Nothing
            If Not String.IsNullOrEmpty(MethaneCompound) AndAlso compounds.ContainsKey(MethaneCompound) Then ch4 = compounds(MethaneCompound)
            If Not String.IsNullOrEmpty(CO2Compound) AndAlso compounds.ContainsKey(CO2Compound) Then co2 = compounds(CO2Compound)
            If Not String.IsNullOrEmpty(WaterCompound) AndAlso compounds.ContainsKey(WaterCompound) Then h2o = compounds(WaterCompound)
            If Not String.IsNullOrEmpty(BiomassCompound) AndAlso compounds.ContainsKey(BiomassCompound) Then biom = compounds(BiomassCompound)
            If Not String.IsNullOrEmpty(NH3Compound) AndAlso compounds.ContainsKey(NH3Compound) Then nh3 = compounds(NH3Compound)

            If ch4 Is Nothing Then Throw New Exception("AnaerobicDigester (ADM1-Lite): Methane compound not present in stream.")
            If co2 Is Nothing Then Throw New Exception("AnaerobicDigester (ADM1-Lite): CO2 compound not present in stream.")

            Dim effMass As New Dictionary(Of String, Double)
            Dim gasMass As New Dictionary(Of String, Double)
            For Each kvp In compounds
                effMass(kvp.Key) = kvp.Value.MassFlow.GetValueOrDefault
                gasMass(kvp.Key) = 0.0
            Next

            ' Substrate consumed moves into biomass + CH4 + CO2 (mass conservative at the COD level only)
            effMass(sub_.Name) = Max(effMass(sub_.Name) - Result_SubstrateConsumed_kgs, 0.0)
            ' Live biomass washout + decayed (lysed) biomass both leave with the effluent as
            ' BiomassCompound (treated as inert VSS sludge in this lumped model).
            If biom IsNot Nothing Then effMass(biom.Name) = Max(effMass(biom.Name) + m_biomass_kgs + m_decayed_kgs, 0.0)
            ' NH3 release: rough 12% of substrate N content, negligible for carbohydrates â†’ skip by default

            gasMass(ch4.Name) = effMass(ch4.Name) + m_CH4_kgs
            gasMass(co2.Name) = effMass(co2.Name) + m_CO2_kgs
            effMass(ch4.Name) = 0.0
            effMass(co2.Name) = 0.0

            ' H2S: assigned outright on both sides, since the feed's own sulfide was folded into the
            ' pool before it was partitioned.
            If h2s IsNot Nothing Then
                gasMass(h2s.Name) = m_H2S_gas_kgs
                effMass(h2s.Name) = m_H2S_liq_kgs
            End If

            ' Close mass balance via residual routing (same approach as BlackBox)
            Dim totalEffMass As Double = 0.0
            Dim totalGasMass As Double = 0.0
            For Each v In effMass.Values : totalEffMass += v : Next
            For Each v In gasMass.Values : totalGasMass += v : Next
            Dim feedMassADM = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim massResidualADM = feedMassADM - totalEffMass - totalGasMass
            If Abs(massResidualADM) > 1.0E-12 Then
                Dim balKeyADM As String = Nothing
                If h2o IsNot Nothing Then balKeyADM = h2o.Name
                If balKeyADM Is Nothing AndAlso biom IsNot Nothing Then balKeyADM = biom.Name
                If balKeyADM Is Nothing Then
                    Dim maxV As Double = -1.0
                    For Each kv In effMass
                        If kv.Key <> ch4.Name AndAlso kv.Key <> co2.Name AndAlso
                           (h2s Is Nothing OrElse kv.Key <> h2s.Name) AndAlso kv.Value > maxV Then
                            maxV = kv.Value : balKeyADM = kv.Key
                        End If
                    Next
                End If
                If balKeyADM IsNot Nothing Then
                    effMass(balKeyADM) = Max(effMass(balKeyADM) + massResidualADM, 0.0)
                    totalEffMass = 0.0
                    For Each v In effMass.Values : totalEffMass += v : Next
                End If
            End If

            ' Thermal balance (reuse the same correlation as BlackBox mode)
            Dim Q_met_W As Double = Abs(HeatPerGCODremoved_Jg) * (COD_removed_kgs * 1000.0)
            If HeatPerGCODremoved_Jg > 0.0 Then Q_met_W = -Q_met_W
            Result_Q_metabolic_kW = Q_met_W / 1000.0

            Dim cp_L_mass As Double = 0.0
            Try : cp_L_mass = ims.Phases(1).Properties.heatCapacityCp.GetValueOrDefault * 1000.0 : Catch : End Try
            If cp_L_mass <= 0.0 Then Try : cp_L_mass = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault * 1000.0 : Catch : End Try
            If cp_L_mass <= 0.0 Then cp_L_mass = 4180.0

            Dim m_dot = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim T_in_K = T
            Dim T_out_K = T_in_K
            Dim Q_duty_W = 0.0
            Select Case ThermalMode
                Case BioReactorThermalMode.Isothermal
                    T_out_K = T_in_K
                    Q_duty_W = -Q_met_W
                Case BioReactorThermalMode.Adiabatic
                    If m_dot > 0 Then T_out_K = T_in_K + Q_met_W / (m_dot * cp_L_mass)
                Case BioReactorThermalMode.DefinedOutletTemperature
                    If OutletTemperature > 0 Then T_out_K = OutletTemperature Else T_out_K = T_in_K
                    Q_duty_W = m_dot * cp_L_mass * (T_out_K - T_in_K) - Q_met_W
            End Select
            Result_OutletTemperature_K = T_out_K
            Result_Q_duty_kW = Q_duty_W / 1000.0

            ' Push to outlet streams (same plumbing as BlackBox)
            Dim cpEff = Me.GraphicObject.OutputConnectors(0)
            If cpEff.IsAttached Then
                Dim msEff As MaterialStream = FlowSheet.SimulationObjects(cpEff.AttachedConnector.AttachedTo.Name)
                WriteSplitStream(msEff, effMass, totalEffMass, T_out_K, P)
            End If
            If Me.GraphicObject.OutputConnectors.Count > 1 Then
                Dim cpGas = Me.GraphicObject.OutputConnectors(1)
                If cpGas.IsAttached Then
                    Dim msGas As MaterialStream = FlowSheet.SimulationObjects(cpGas.AttachedConnector.AttachedTo.Name)
                    WriteSplitStream(msGas, gasMass, totalGasMass, T_out_K, P)
                End If
            End If

            DeltaQ = Result_Q_duty_kW
            Try
                Dim es = GetInletEnergyStream(1)
                If es IsNot Nothing Then
                    es.EnergyFlow = Result_Q_duty_kW
                    es.GraphicObject.Calculated = True
                End If
            Catch ex As ArgumentOutOfRangeException
            End Try

            OutletTemperature = T_out_K

        End Sub

        ''' <summary>Helper: is the digester operated continuously or batch-wise?
        ''' Currently AD is modelled as a CSTR at steady state; returns "Continuous" unless
        ''' BatchDuration (HRT_s) is explicitly flagged via future extensions.</summary>
        Private Function OperatingMode_Digester() As String
            Return "Continuous"
        End Function

        ' ================================================================================
        ' FULL ADM1 (Batstone et al. 2002) - Calculation Path
        ' ================================================================================

        ''' <summary>
        ''' Full ADM1 dynamic simulation. Integrates the 29-variable ODE system with algebraic
        ''' pH over ADM1Params.Operating.SimulationTime_d (default 200 d) using Cash-Karp RK45.
        ''' Trajectory is stored in ADM1LastTrajectory; final state in ADM1LastState. Results
        ''' are mapped back to outlet liquid and biogas streams on a COD basis.
        ''' </summary>
        Public Sub CalculateADM1Full()

            ' Hydrate parameter object from JSON if we just came back from a save/load
            If ADM1Params Is Nothing Then ADM1Params = New ADM1.ADM1Parameters()
            If Not String.IsNullOrWhiteSpace(ADM1ParamsJSON) Then
                Try
                    ADM1Params = ADM1.ADM1Parameters.FromJSON(ADM1ParamsJSON)
                Catch
                End Try
            End If

            If String.IsNullOrEmpty(SubstrateCompound) Then
                Throw New Exception("AnaerobicDigester (ADM1-Full): Organic Substrate compound not selected.")
            End If
            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception("AnaerobicDigester (ADM1-Full): Feed stream not connected.")
            End If
            If Me.GraphicObject.OutputConnectors.Count < 2 OrElse
               Not Me.GraphicObject.OutputConnectors(0).IsAttached OrElse
               Not Me.GraphicObject.OutputConnectors(1).IsAttached Then
                Throw New Exception("AnaerobicDigester (ADM1-Full): Both Effluent and Biogas outlets must be connected.")
            End If

            Dim ims As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.SetFlowsheet(Me.FlowSheet)
            ims.SetPropertyPackage(PropertyPackage)
            PropertyPackage.CurrentMaterialStream = ims
            ims.DefinedFlow = FlowSpec.Mass

            Dim T_K = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P0 = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            Try
                ims.Calculate(True, True)
            Catch ex As Exception
            End Try

            Dim compounds = ims.Phases(0).Compounds

            If Not compounds.ContainsKey(SubstrateCompound) Then _
                Throw New Exception("AnaerobicDigester (ADM1-Full): Substrate '" & SubstrateCompound & "' not in stream.")

            Dim sub_ = compounds(SubstrateCompound)
            Dim cpSub = sub_.ConstantProperties
            Dim m_sub_in = sub_.MassFlow.GetValueOrDefault
            Dim Q_liquid_m3s = LiquidVolumetricFlow(ims)
            Dim Q_liquid_m3d = Q_liquid_m3s * 86400.0

            ' Sulfur load. codFactorExtra credits the COD of organic sulfur declared by override,
            ' which TheoreticalCOD cannot see; without it the debit below would destroy COD that was
            ' never counted in the first place.
            Dim nSO4_kmols As Double, nOrgS_kmols As Double, codFactorExtra As Double
            SulfurLoad(cpSub, m_sub_in, Q_liquid_m3s, nSO4_kmols, nOrgS_kmols, codFactorExtra)
            Dim nS_total_kmols = nSO4_kmols + nOrgS_kmols
            ' Sulfide already dissolved in the feed joins the pool instead of being stranded.
            If Not String.IsNullOrEmpty(H2SCompound) AndAlso compounds.ContainsKey(H2SCompound) Then
                nS_total_kmols += compounds(H2SCompound).MassFlow.GetValueOrDefault / MW_H2S
            End If

            Dim codFactor = TheoreticalCOD(cpSub) + codFactorExtra
            If codFactor <= 0 Then Throw New Exception("AnaerobicDigester (ADM1-Full): Theoretical COD of substrate is non-positive.")

            Dim CODin_kgs = m_sub_in * codFactor

            ' Build influent vector - feed the substrate as composite particulate X_c, characterised
            ' from its own elemental formula (see CharacteriseCompositeFromSubstrate), so disintegration
            ' splits it into carbohydrate/protein/lipid plus soluble and particulate inerts. The inerts
            ' leave undigested (COD conversion below 100 %) and the protein nitrogen becomes ammonia,
            ' which raises alkalinity and keeps CO2 in the liquid so the biogas is CH4-rich.
            ' Users wanting fine-grained influent composition edit ADM1Params.Operating.Sin_*/Xin_*.
            ' ADM1-S respires the feed sulfate inside the reactor instead of assuming it reduced.
            ' Switching the extension on here rather than leaving it to the parameter dialog is what
            ' makes the two models genuinely separate: pick ADM1-Full and the sulfate block is off
            ' whatever it holds, so the run is the Batstone 2002 benchmark.
            Dim srbKinetics = (Model = DigesterModel.ADM1Sulfate)
            ADM1Params.Sulfate.Enabled = srbKinetics

            Dim op = ADM1Params.Operating
            Dim useStream = op.UseInfluentFromFeedStream
            Dim Sin As Double()
            If useStream Then
                Sin = New Double(ADM1.ADM1State.NDynamic - 1) {}
                CharacteriseCompositeFromSubstrate(cpSub)
                ' Feed COD concentration (kg COD/mÂ³)
                Dim S_in_COD = CODin_kgs / Q_liquid_m3s
                Dim c_SO4_in = nSO4_kmols / Q_liquid_m3s            ' kmol S/m³ as sulfate
                Dim c_IS_in = (nS_total_kmols - nSO4_kmols) / Q_liquid_m3s ' organic S + fed sulfide

                ' Feed alkalinity: strong cations the substrate carries beyond the ammonia it releases,
                ' added to the influent cation charge so the charge-balance pH reflects a buffered feed.
                Dim alk = Max(InfluentAlkalinity_eqL, 0.0)
                Sin(10) = op.Sin_IN   ' inorganic N
                Sin(9) = op.Sin_IC    ' inorganic C
                Sin(24) = op.Sin_cat + alk
                Sin(25) = op.Sin_an

                If srbKinetics Then
                    ' Sulfate goes in as sulfate and the reducers take their electrons out of the
                    ' donor pool themselves, so there is nothing to debit: the methane loss is
                    ' whatever the competition produces, and a sulfate-limited digester is now a
                    ' state the model can actually reach.
                    Sin(12) = S_in_COD   ' feed COD as composite X_c
                    Sin(31) = c_SO4_in
                    Sin(29) = c_IS_in
                    ' Sulfate is divalent and S_an counts charge, not moles - and it arrives as a
                    ' salt, so the counter-cations come with it. Feeding the anion alone would be
                    ' feeding sulfuric acid and would acidify the reactor for no physical reason.
                    Sin(24) = op.Sin_cat + 2.0 * c_SO4_in + alk
                    Sin(25) = op.Sin_an + 2.0 * c_SO4_in
                Else
                    ' Sulfide arrives already mineralised, and its electrons are debited from the
                    ' feed COD here rather than modelled as an in-reactor sink: that keeps the
                    ' balance exact at steady state and out of reach of the integrator's
                    ' non-negativity clamp. Organic sulfur is COD-neutral for methane (its COD is
                    ' inside codFactor and comes straight back out here); sulfate sulfur carries no
                    ' COD, so its debit is a real loss.
                    Dim c_IS_all = nS_total_kmols / Q_liquid_m3s
                    Dim codDebit = ADM1.ADM1State.COD_per_kmol_S * c_IS_all
                    If codDebit > S_in_COD Then
                        FlowSheet?.ShowMessage(String.Format(
                            "{0}: the sulfur load needs {1:G4} kg COD/m³ of electrons but the feed only " &
                            "supplies {2:G4}. The debit is being capped; this simplified balance cannot " &
                            "represent a sulfate-limited digester. The ADM1-S model can - it solves the " &
                            "competition instead of assuming it.",
                            Me.GraphicObject.Tag, codDebit, S_in_COD), IFlowsheet.MessageType.Warning)
                        codDebit = S_in_COD
                    End If

                    Sin(12) = S_in_COD - codDebit   ' feed COD as composite X_c, net of the sulfide debit
                    Sin(29) = c_IS_all
                End If
                ' override operating Q
                op.Q_in = Q_liquid_m3d
            Else
                Sin = op.ToInfluentVector(ADM1Params.Sulfate)
            End If

            ' The reactor runs at the feed's temperature whichever way the influent was specified.
            ' These used to sit inside the useStream branch, so a manual influent silently pinned the
            ' whole model to the 308.15 K default no matter what the stream said. Physicochemical is
            ' the one the equations read; Operating.T_op_K is kept in step because it is what the
            ' parameter dialog shows.
            op.T_op_K = T_K
            ADM1Params.Physicochemical.T_op_K = T_K

            ' Use ADM1Params.Operating.V_liq as reactor volume if it matches DWSIM Volume; otherwise override
            If Volume > 0.0 Then op.V_liq = Volume

            ' Integrate
            Dim ic = ADM1Params.InitialConditions

            ' A sulfate reducer that starts at exactly zero stays at exactly zero: its growth term
            ' is proportional to its own biomass, so an unseeded population can never establish and
            ' the model would quietly behave as if sulfate reduction did not happen. Seed only when
            ' the user has left all four at zero, so a deliberate inoculum is never overwritten.
            If srbKinetics AndAlso
               ic.X_srb_h2 <= 0.0 AndAlso ic.X_srb_ac <= 0.0 AndAlso
               ic.X_srb_pro <= 0.0 AndAlso ic.X_srb_bu <= 0.0 Then
                ic = ic.Clone()
                ic.X_srb_h2 = SRBSeed_kgCODm3
                ic.X_srb_ac = SRBSeed_kgCODm3
                ic.X_srb_pro = SRBSeed_kgCODm3
                ic.X_srb_bu = SRBSeed_kgCODm3
            End If

            Dim tEnd = Max(op.SimulationTime_d, 1.0)
            Dim traj = ADM1.ADM1Integrator.Integrate(ic, ADM1Params, op.Q_in, Sin, 0.0, tEnd,
                                                     recordTrajectory:=True, sampleInterval:=-1.0)
            ADM1LastTrajectory = traj
            ADM1LastState = traj.FinalState

            ' An integration that stopped short leaves an early transient in FinalState, and every
            ' result below - pH, biogas, H2S, COD removal - would be read as a steady state. Refuse
            ' to publish it rather than let it out looking like a converged answer.
            If Not traj.Converged Then
                Throw New Exception("AnaerobicDigester (ADM1-Full): " & traj.StopReason)
            End If
            If Not String.IsNullOrEmpty(traj.StopReason) Then
                FlowSheet?.ShowMessage(Me.GraphicObject.Tag & ": " & traj.StopReason,
                                       IFlowsheet.MessageType.Warning)
            End If

            ' Map back to standard result fields
            Dim sFinal = traj.FinalState
            Dim Q_gas_m3d = ADM1.ADM1Equations.BiogasFlow_Nm3_d(sFinal, ADM1Params)
            Dim x_CH4 = ADM1.ADM1Equations.CH4MoleFraction(sFinal, ADM1Params)
            Dim x_CO2 = ADM1.ADM1Equations.CO2MoleFraction(sFinal, ADM1Params)
            Dim x_H2S = ADM1.ADM1Equations.H2SMoleFraction(sFinal, ADM1Params)

            ' Convert biogas Nm3/d to mol/s and species mass flows
            Dim molGas_per_s = (Q_gas_m3d / 86400.0) / 0.022414 ' mol/s at STP
            Dim mol_CH4_s = molGas_per_s * x_CH4
            Dim mol_CO2_s = molGas_per_s * x_CO2
            Dim mol_H2S_s = molGas_per_s * x_H2S
            Dim m_CH4_kgs = mol_CH4_s * 0.01604
            Dim m_CO2_kgs = mol_CO2_s * 0.04401
            Dim m_H2S_kgs = mol_H2S_s * (MW_H2S / 1000.0)

            Dim CODeff_kgm3 = sFinal.TotalCOD()
            Dim CODeff_kgs = CODeff_kgm3 * Q_liquid_m3s
            Dim COD_removed_kgs = Max(CODin_kgs - CODeff_kgs, 0.0)

            Result_CODin_kgs = CODin_kgs
            Result_CODremoved_kgs = COD_removed_kgs
            Result_SubstrateConsumed_kgs = COD_removed_kgs / codFactor
            Result_CH4_kgs = m_CH4_kgs
            Result_CO2_kgs = m_CO2_kgs
            Result_BiogasFlow_mols = molGas_per_s
            Result_CH4MoleFraction = x_CH4
            Result_SpecificCH4Yield_Nm3kgCOD = If(COD_removed_kgs > 0,
                                                  ((m_CH4_kgs / 0.01604) * 0.022414) / COD_removed_kgs, 0.0)
            Result_H2S_kgs = m_H2S_kgs
            Result_H2S_ppmv = x_H2S * 1000000.0
            Result_DissolvedSulfide_kgSm3 = sFinal.S_IS * MW_Sulfur

            Dim X_srb_total = sFinal.X_srb_h2 + sFinal.X_srb_ac + sFinal.X_srb_pro + sFinal.X_srb_bu
            Result_SRBBiomass_kgCODm3 = X_srb_total
            Result_ResidualSulfate_kgSm3 = sFinal.S_so4 * MW_Sulfur
            Result_SulfateReduction = If(Sin(31) > 0.0,
                                         Max(1.0 - sFinal.S_so4 / Sin(31), 0.0), 0.0)

            ' Biomass (sludge) COD â†’ mass via same factor as substrate (approximation)
            Dim X_bio_total = sFinal.X_su + sFinal.X_aa + sFinal.X_fa + sFinal.X_c4 + sFinal.X_pro + sFinal.X_ac + sFinal.X_h2 + X_srb_total
            Result_Sludge_kgs = X_bio_total * Q_liquid_m3s / Max(codFactor, 0.1) ' kg biomass/s

            ADM1_Result_pH = sFinal.pH

            ' Build outlet streams (same plumbing as ADM1-Lite)
            Dim ch4 As Compound = Nothing, co2 As Compound = Nothing, h2o As Compound = Nothing
            Dim biom As Compound = Nothing, nh3 As Compound = Nothing, h2s As Compound = Nothing
            If Not String.IsNullOrEmpty(MethaneCompound) AndAlso compounds.ContainsKey(MethaneCompound) Then ch4 = compounds(MethaneCompound)
            If Not String.IsNullOrEmpty(CO2Compound) AndAlso compounds.ContainsKey(CO2Compound) Then co2 = compounds(CO2Compound)
            If Not String.IsNullOrEmpty(WaterCompound) AndAlso compounds.ContainsKey(WaterCompound) Then h2o = compounds(WaterCompound)
            If Not String.IsNullOrEmpty(BiomassCompound) AndAlso compounds.ContainsKey(BiomassCompound) Then biom = compounds(BiomassCompound)
            If Not String.IsNullOrEmpty(NH3Compound) AndAlso compounds.ContainsKey(NH3Compound) Then nh3 = compounds(NH3Compound)
            If Not String.IsNullOrEmpty(H2SCompound) AndAlso compounds.ContainsKey(H2SCompound) Then h2s = compounds(H2SCompound)

            If ch4 Is Nothing Then Throw New Exception("AnaerobicDigester (ADM1-Full): Methane compound not present in stream.")
            If co2 Is Nothing Then Throw New Exception("AnaerobicDigester (ADM1-Full): CO2 compound not present in stream.")

            Dim effMass As New Dictionary(Of String, Double)
            Dim gasMass As New Dictionary(Of String, Double)
            For Each kvp In compounds
                effMass(kvp.Key) = kvp.Value.MassFlow.GetValueOrDefault
                gasMass(kvp.Key) = 0.0
            Next

            effMass(sub_.Name) = Max(effMass(sub_.Name) - Result_SubstrateConsumed_kgs, 0.0)
            If biom IsNot Nothing Then effMass(biom.Name) = Max(effMass(biom.Name) + Result_Sludge_kgs, 0.0)

            gasMass(ch4.Name) = effMass(ch4.Name) + m_CH4_kgs
            gasMass(co2.Name) = effMass(co2.Name) + m_CO2_kgs
            effMass(ch4.Name) = 0.0
            effMass(co2.Name) = 0.0

            ' H2S: the ODEs already split the sulfide between headspace and liquid, so both sides
            ' are assigned outright (the feed's own sulfide went into Sin, not into the outlet).
            If h2s IsNot Nothing Then
                gasMass(h2s.Name) = m_H2S_kgs
                effMass(h2s.Name) = sFinal.S_IS * Q_liquid_m3s * MW_H2S
            End If

            ' Sulfate the reducers did not get to. Only ADM1-S can leave any: the other models
            ' assume complete reduction, so S_so4 is zero there and this is a no-op.
            If srbKinetics Then
                Dim so4Out_kmols = sFinal.S_so4 * Q_liquid_m3s   ' kmol S/s
                If so4Out_kmols > 0.0 Then
                    Dim so4Comp As Compound = Nothing
                    If Not String.IsNullOrEmpty(SulfateCompound) AndAlso compounds.ContainsKey(SulfateCompound) Then _
                        so4Comp = compounds(SulfateCompound)
                    Dim sPerMol = If(so4Comp Is Nothing, 0.0, SulfurAtoms(so4Comp.ConstantProperties))
                    If so4Comp IsNot Nothing AndAlso sPerMol > 0.0 Then
                        effMass(so4Comp.Name) = so4Out_kmols / sPerMol * so4Comp.ConstantProperties.Molar_Weight
                    Else
                        FlowSheet?.ShowMessage(String.Format(
                            "{0}: {1:G4} kg S/s of sulfate was fed but not reduced, and no sulfate compound is " &
                            "selected to carry it out. Pick one under Compound Mapping or that sulfur leaves the " &
                            "flowsheet unaccounted.", Me.GraphicObject.Tag, so4Out_kmols * MW_Sulfur),
                            IFlowsheet.MessageType.Warning)
                    End If
                End If
            End If

            Dim totalEffMass As Double = 0.0
            Dim totalGasMass As Double = 0.0
            For Each v In effMass.Values : totalEffMass += v : Next
            For Each v In gasMass.Values : totalGasMass += v : Next

            ' Close the mass balance the same way BlackBox and ADM1-Lite already do. The ADM1 gas
            ' and sludge paths are COD-based, not atom-based, so a residual is expected; leaving it
            ' unclosed (as this path used to) just pushes the error into the stream compositions.
            Dim feedMass As Double = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim massResidual = feedMass - totalEffMass - totalGasMass
            If Abs(massResidual) > 1.0E-12 Then
                Dim balKey As String = Nothing
                If h2o IsNot Nothing Then balKey = h2o.Name
                If balKey Is Nothing AndAlso biom IsNot Nothing Then balKey = biom.Name
                If balKey Is Nothing Then
                    Dim maxV As Double = -1.0
                    For Each kv In effMass
                        If kv.Key <> ch4.Name AndAlso kv.Key <> co2.Name AndAlso
                           (h2s Is Nothing OrElse kv.Key <> h2s.Name) AndAlso
                           kv.Key <> SulfateCompound AndAlso kv.Value > maxV Then
                            maxV = kv.Value : balKey = kv.Key
                        End If
                    Next
                End If
                If balKey IsNot Nothing Then
                    effMass(balKey) = Max(effMass(balKey) + massResidual, 0.0)
                    totalEffMass = 0.0
                    For Each v In effMass.Values : totalEffMass += v : Next
                End If
            End If

            ' Thermal balance - reuse the same correlation
            Dim Q_met_W As Double = Abs(HeatPerGCODremoved_Jg) * (COD_removed_kgs * 1000.0)
            If HeatPerGCODremoved_Jg > 0.0 Then Q_met_W = -Q_met_W
            Result_Q_metabolic_kW = Q_met_W / 1000.0

            Dim cp_L_mass As Double = 0.0
            Try : cp_L_mass = ims.Phases(1).Properties.heatCapacityCp.GetValueOrDefault * 1000.0 : Catch : End Try
            If cp_L_mass <= 0.0 Then Try : cp_L_mass = ims.Phases(0).Properties.heatCapacityCp.GetValueOrDefault * 1000.0 : Catch : End Try
            If cp_L_mass <= 0.0 Then cp_L_mass = 4180.0

            Dim m_dot = ims.Phases(0).Properties.massflow.GetValueOrDefault
            Dim T_in_K = T_K
            Dim T_out_K = T_in_K
            Dim Q_duty_W = 0.0
            Select Case ThermalMode
                Case BioReactorThermalMode.Isothermal
                    T_out_K = T_in_K
                    Q_duty_W = -Q_met_W
                Case BioReactorThermalMode.Adiabatic
                    If m_dot > 0 Then T_out_K = T_in_K + Q_met_W / (m_dot * cp_L_mass)
                Case BioReactorThermalMode.DefinedOutletTemperature
                    If OutletTemperature > 0 Then T_out_K = OutletTemperature Else T_out_K = T_in_K
                    Q_duty_W = m_dot * cp_L_mass * (T_out_K - T_in_K) - Q_met_W
            End Select
            Result_OutletTemperature_K = T_out_K
            Result_Q_duty_kW = Q_duty_W / 1000.0

            Dim cpEff = Me.GraphicObject.OutputConnectors(0)
            If cpEff.IsAttached Then
                Dim msEff As MaterialStream = FlowSheet.SimulationObjects(cpEff.AttachedConnector.AttachedTo.Name)
                WriteSplitStream(msEff, effMass, totalEffMass, T_out_K, P)
            End If
            If Me.GraphicObject.OutputConnectors.Count > 1 Then
                Dim cpGas = Me.GraphicObject.OutputConnectors(1)
                If cpGas.IsAttached Then
                    Dim msGas As MaterialStream = FlowSheet.SimulationObjects(cpGas.AttachedConnector.AttachedTo.Name)
                    WriteSplitStream(msGas, gasMass, totalGasMass, T_out_K, P)
                End If
            End If

            DeltaQ = Result_Q_duty_kW
            Try
                Dim es = GetInletEnergyStream(1)
                If es IsNot Nothing Then
                    es.EnergyFlow = Result_Q_duty_kW
                    es.GraphicObject.Calculated = True
                End If
            Catch ex As ArgumentOutOfRangeException
            End Try

            OutletTemperature = T_out_K

            ' Sync JSON for persistence
            Try : ADM1ParamsJSON = ADM1Params.ToJSON() : Catch : End Try

        End Sub

        ''' <summary>
        ''' Pure-function ADM1 simulation API for regression/scripting. Runs the integrator
        ''' with the supplied parameters and initial conditions over the requested time span
        ''' (days) and returns the trajectory result. Does not modify the unit op.
        ''' </summary>
        ''' <remarks>
        ''' Returns the result rather than throwing on a bad run, so a parameter fit can score a
        ''' failed trial instead of dying on it. Check ADM1TrajectoryResult.Converged before reading
        ''' the states: when it is False the trajectory stops short of tEnd_d and holds a transient.
        ''' </remarks>
        ''' <param name="p">ADM1 parameter set.</param>
        ''' <param name="ic">Initial conditions.</param>
        ''' <param name="qInflow_m3d">Influent volumetric flow (mÂ³/d).</param>
        ''' <param name="Sin">Influent concentrations in state order (ADM1State.NDynamic entries).</param>
        ''' <param name="tEnd_d">Integration endpoint (days).</param>
        Public Function RunADM1Simulation(p As ADM1.ADM1Parameters,
                                          ic As ADM1.ADM1State,
                                          qInflow_m3d As Double,
                                          Sin As Double(),
                                          tEnd_d As Double) As ADM1.ADM1TrajectoryResult
            Return ADM1.ADM1Integrator.Integrate(ic, p, qInflow_m3d, Sin, 0.0, tEnd_d)
        End Function

        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)
            ' Make sure the JSON blob reflects the current in-memory ADM1Params before serialization
            Try
                If ADM1Params IsNot Nothing Then ADM1ParamsJSON = ADM1Params.ToJSON()
            Catch
            End Try
            Return MyBase.SaveData()
        End Function

        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean
            Dim ok = MyBase.LoadData(data)
            Try
                If Not String.IsNullOrWhiteSpace(ADM1ParamsJSON) Then
                    ADM1Params = ADM1.ADM1Parameters.FromJSON(ADM1ParamsJSON)
                ElseIf ADM1Params Is Nothing Then
                    ADM1Params = New ADM1.ADM1Parameters()
                End If
            Catch
                ADM1Params = New ADM1.ADM1Parameters()
            End Try
            Return ok
        End Function

        ''' <summary>Writes a compound-mass-dictionary to a MaterialStream at (T, P). Used by the ADM1-Lite path.</summary>
        Private Shared Sub WriteSplitStream(ms As MaterialStream, m As Dictionary(Of String, Double),
                                             total As Double, T As Double, P As Double)
            With ms
                .ClearAllProps()
                .Phases(0).Properties.temperature = T
                .Phases(0).Properties.pressure = P
                If total > 0 Then
                    For Each c In .Phases(0).Compounds.Values
                        c.MassFraction = If(m.ContainsKey(c.Name), m(c.Name), 0.0) / total
                    Next
                    Dim invMW As Double = 0.0
                    For Each c In .Phases(0).Compounds.Values
                        invMW += c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight
                    Next
                    If invMW > 0 Then
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = (c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight) / invMW
                        Next
                    End If
                End If
                .Phases(0).Properties.massflow = total
                .DefinedFlow = FlowSpec.Mass
                .SpecType = StreamSpec.Temperature_and_Pressure
            End With
        End Sub

        Public Overrides Sub DeCalculate()
            For Each cp In {Me.GraphicObject.OutputConnectors(0),
                            If(Me.GraphicObject.OutputConnectors.Count > 1, Me.GraphicObject.OutputConnectors(1), Nothing)}
                If cp IsNot Nothing AndAlso cp.IsAttached Then
                    Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                    With ms
                        .Phases(0).Properties.temperature = Nothing
                        .Phases(0).Properties.pressure = Nothing
                        .Phases(0).Properties.enthalpy = Nothing
                        For Each c In .Phases(0).Compounds.Values
                            c.MoleFraction = 0
                            c.MassFraction = 0
                        Next
                        .Phases(0).Properties.massflow = Nothing
                        .GraphicObject.Calculated = False
                    End With
                End If
            Next
        End Sub

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return UnitOperations.BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Anaerobic Digester (black-box Buswell + COD removal)"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Anaerobic Digester"
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder
            str.AppendLine("AnaerobicDigester:  " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Configuration")
            str.AppendLine("    Volume:              " & Volume.ToString(numberformat, ci) & " m3")
            str.AppendLine("    HRT:                 " & (HRT_s / 3600.0).ToString(numberformat, ci) & " h")
            str.AppendLine("    Substrate:           " & SubstrateCompound)
            str.AppendLine("    Methane compound:    " & MethaneCompound)
            str.AppendLine("    CO2 compound:        " & CO2Compound)
            If BiomassCompound <> "" Then str.AppendLine("    Biomass compound:    " & BiomassCompound)
            str.AppendLine()
            str.AppendLine("Parameters")
            str.AppendLine("    COD Removal:         " & (CODRemovalEfficiency * 100.0).ToString(numberformat, ci) & " %")
            str.AppendLine("    Biomass Yield:       " & BiomassYield_gVSSpergCOD.ToString(numberformat, ci) & " g VSS/g COD")
            str.AppendLine("    CH4 Fraction (ovr):  " & MethaneFractionOverride.ToString(numberformat, ci))
            str.AppendLine()
            str.AppendLine("Results")
            str.AppendLine("    Feed COD:            " & Result_CODin_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    COD Removed:         " & Result_CODremoved_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    Substrate Consumed:  " & Result_SubstrateConsumed_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    Biogas Flow:         " & Result_BiogasFlow_mols.ToString(numberformat, ci) & " mol/s")
            str.AppendLine("    CH4 Flow:            " & Result_CH4_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    CO2 Flow:            " & Result_CO2_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    CH4 Fraction:        " & (Result_CH4MoleFraction * 100.0).ToString(numberformat, ci) & " %")
            str.AppendLine("    Specific CH4 Yield:  " & Result_SpecificCH4Yield_Nm3kgCOD.ToString(numberformat, ci) & " Nm3/kg COD")
            str.AppendLine("    Sludge Production:   " & Result_Sludge_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine()
            str.AppendLine("Sulfur")
            str.AppendLine("    H2S in biogas:       " & Result_H2S_ppmv.ToString(numberformat, ci) & " ppmv")
            str.AppendLine("    H2S flow:            " & Result_H2S_kgs.ToString(numberformat, ci) & " kg/s")
            str.AppendLine("    Dissolved sulfide:   " & Result_DissolvedSulfide_kgSm3.ToString(numberformat, ci) & " kg S/m3")
            If Model = DigesterModel.ADM1Sulfate Then
                str.AppendLine("    Residual sulfate:    " & Result_ResidualSulfate_kgSm3.ToString(numberformat, ci) & " kg S/m3")
                str.AppendLine("    Sulfate reduced:     " & (Result_SulfateReduction * 100.0).ToString(numberformat, ci) & " %")
                str.AppendLine("    SRB biomass:         " & Result_SRBBiomass_kgCODm3.ToString(numberformat, ci) & " kg COD/m3")
            End If
            str.AppendLine()
            str.AppendLine("Thermal Balance")
            str.AppendLine("    Mode:               " & ThermalMode.ToString)
            str.AppendLine("    Metabolic heat:     " & Result_Q_metabolic_kW.ToString(numberformat, ci) & " kW")
            str.AppendLine("    Net heat duty:      " & Result_Q_duty_kW.ToString(numberformat, ci) & " kW  (+ heating / âˆ’ cooling)")
            str.AppendLine("    Outlet temperature: " & Result_OutletTemperature_K.ToString(numberformat, ci) & " K")
            Return str.ToString()

        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Volume",
            "HRT",
            "Substrate Compound",
            "Methane Compound",
            "CO2 Compound",
            "Water Compound",
            "NH3 Compound",
            "Biomass Compound",
            "H2S Compound",
            "Sulfate Compound",
            "COD Removal Efficiency",
            "Biomass Yield on COD",
            "Methane Fraction Override",
            "Thermal Mode",
            "Heat per g COD removed",
            "Digester Model",
            "Influent Sulfate Sulfur",
            "Substrate Organic Sulfur",
            "Assumed pH for Sulfide",
            "ADM1 k_hyd",
            "ADM1 km_su",
            "ADM1 Ks_su",
            "ADM1 Y_su",
            "ADM1 km_vfa",
            "ADM1 Ks_vfa",
            "ADM1 Y_ace",
            "ADM1 KI_h2",
            "ADM1 km_ac",
            "ADM1 Ks_ac",
            "ADM1 Y_am",
            "ADM1 km_h2",
            "ADM1 Ks_h2",
            "ADM1 Y_hm",
            "ADM1 k_dec"
        }

        Private Shared ReadOnly _outputProps As String() = {
            "Feed COD",
            "COD Removed",
            "Substrate Consumed",
            "Biogas Molar Flow",
            "Methane Mass Flow",
            "CO2 Mass Flow",
            "Methane Mole Fraction",
            "Specific CH4 Yield",
            "Sludge Production",
            "H2S Mass Flow",
            "H2S in Biogas",
            "Dissolved Sulfide",
            "Residual Sulfate",
            "Sulfate Reduction",
            "SRB Biomass",
            "Metabolic Heat Duty",
            "Net Heat Duty",
            "Outlet Temperature",
            "ADM1 S_s",
            "ADM1 S_VFA",
            "ADM1 S_Ac",
            "ADM1 S_H2",
            "ADM1 X_hyd",
            "ADM1 X_ace",
            "ADM1 X_am",
            "ADM1 X_hm",
            "ADM1 pH"
        }

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR : Return _inputProps
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Volume" : Return Volume
                Case "HRT" : Return HRT_s
                Case "Substrate Compound" : Return SubstrateCompound
                Case "Methane Compound" : Return MethaneCompound
                Case "CO2 Compound" : Return CO2Compound
                Case "Water Compound" : Return WaterCompound
                Case "NH3 Compound" : Return NH3Compound
                Case "Biomass Compound" : Return BiomassCompound
                Case "H2S Compound" : Return H2SCompound
                Case "Sulfate Compound" : Return SulfateCompound
                Case "COD Removal Efficiency" : Return CODRemovalEfficiency
                Case "Biomass Yield on COD" : Return BiomassYield_gVSSpergCOD
                Case "Methane Fraction Override" : Return MethaneFractionOverride
                Case "Thermal Mode" : Return ThermalMode.ToString()
                Case "Heat per g COD removed" : Return HeatPerGCODremoved_Jg
                Case "Digester Model" : Return Model.ToString()
                Case "Influent Sulfate Sulfur" : Return InfluentSulfateS_mgL
                Case "Substrate Organic Sulfur" : Return SubstrateOrganicS_gPerKg
                Case "Assumed pH for Sulfide" : Return AssumedPH_ForSulfide
                Case "ADM1 k_hyd" : Return ADM1_k_hyd_d
                Case "ADM1 km_su" : Return ADM1_km_su_d
                Case "ADM1 Ks_su" : Return ADM1_Ks_su
                Case "ADM1 Y_su" : Return ADM1_Y_su
                Case "ADM1 km_vfa" : Return ADM1_km_vfa_d
                Case "ADM1 Ks_vfa" : Return ADM1_Ks_vfa
                Case "ADM1 Y_ace" : Return ADM1_Y_ace
                Case "ADM1 KI_h2" : Return ADM1_KI_h2
                Case "ADM1 km_ac" : Return ADM1_km_ac_d
                Case "ADM1 Ks_ac" : Return ADM1_Ks_ac
                Case "ADM1 Y_am" : Return ADM1_Y_am
                Case "ADM1 km_h2" : Return ADM1_km_h2_d
                Case "ADM1 Ks_h2" : Return ADM1_Ks_h2
                Case "ADM1 Y_hm" : Return ADM1_Y_hm
                Case "ADM1 k_dec" : Return ADM1_k_dec_d
                Case "Feed COD" : Return Result_CODin_kgs
                Case "COD Removed" : Return Result_CODremoved_kgs
                Case "Substrate Consumed" : Return Result_SubstrateConsumed_kgs
                Case "Biogas Molar Flow" : Return Result_BiogasFlow_mols
                Case "Methane Mass Flow" : Return Result_CH4_kgs
                Case "CO2 Mass Flow" : Return Result_CO2_kgs
                Case "Methane Mole Fraction" : Return Result_CH4MoleFraction
                Case "Specific CH4 Yield" : Return Result_SpecificCH4Yield_Nm3kgCOD
                Case "Sludge Production" : Return Result_Sludge_kgs
                Case "H2S Mass Flow" : Return Result_H2S_kgs
                Case "H2S in Biogas" : Return Result_H2S_ppmv
                Case "Dissolved Sulfide" : Return Result_DissolvedSulfide_kgSm3
                Case "Residual Sulfate" : Return Result_ResidualSulfate_kgSm3
                Case "Sulfate Reduction" : Return Result_SulfateReduction
                Case "SRB Biomass" : Return Result_SRBBiomass_kgCODm3
                Case "Metabolic Heat Duty" : Return Result_Q_metabolic_kW
                Case "Net Heat Duty" : Return Result_Q_duty_kW
                Case "Outlet Temperature" : Return Result_OutletTemperature_K
                Case "ADM1 S_s" : Return ADM1_Result_S_s
                Case "ADM1 S_VFA" : Return ADM1_Result_S_VFA
                Case "ADM1 S_Ac" : Return ADM1_Result_S_Ac
                Case "ADM1 S_H2" : Return ADM1_Result_S_H2
                Case "ADM1 X_hyd" : Return ADM1_Result_X_hyd
                Case "ADM1 X_ace" : Return ADM1_Result_X_ace
                Case "ADM1 X_am" : Return ADM1_Result_X_am
                Case "ADM1 X_hm" : Return ADM1_Result_X_hm
                Case "ADM1 pH" : Return ADM1_Result_pH
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Volume" : Return "m3"
                Case "HRT" : Return "s"
                Case "COD Removal Efficiency",
                     "Methane Fraction Override",
                     "Methane Mole Fraction" : Return "-"
                Case "Biomass Yield on COD" : Return "g VSS/g COD"
                Case "Heat per g COD removed" : Return "J/g"
                Case "Feed COD",
                     "COD Removed",
                     "Substrate Consumed",
                     "Methane Mass Flow",
                     "CO2 Mass Flow",
                     "H2S Mass Flow",
                     "Sludge Production" : Return "kg/s"
                Case "Biogas Molar Flow" : Return "mol/s"
                Case "Specific CH4 Yield" : Return "Nm3/kg"
                Case "Metabolic Heat Duty",
                     "Net Heat Duty" : Return "kW"
                Case "Outlet Temperature" : Return "K"
                Case "Influent Sulfate Sulfur" : Return "mg S/L"
                Case "Substrate Organic Sulfur" : Return "g S/kg"
                Case "Assumed pH for Sulfide" : Return "-"
                Case "H2S in Biogas" : Return "ppmv"
                Case "Dissolved Sulfide" : Return "kg S/m3"
                Case "Residual Sulfate" : Return "kg S/m3"
                Case "Sulfate Reduction" : Return "-"
                Case "SRB Biomass" : Return "kg COD/m3"
                Case Else : Return ""
            End Select
        End Function

        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean
            Dim d As Double = 0.0
            If TypeOf propval Is Double Then
                d = CDbl(propval)
            ElseIf TypeOf propval Is String Then
                Double.TryParse(CStr(propval), Globalization.NumberStyles.Any, Globalization.CultureInfo.CurrentCulture, d)
            End If
            Select Case prop
                Case "Volume" : Volume = d : Return True
                Case "HRT" : HRT_s = d : Return True
                Case "Substrate Compound" : SubstrateCompound = propval?.ToString() : Return True
                Case "Methane Compound" : MethaneCompound = propval?.ToString() : Return True
                Case "CO2 Compound" : CO2Compound = propval?.ToString() : Return True
                Case "Water Compound" : WaterCompound = propval?.ToString() : Return True
                Case "NH3 Compound" : NH3Compound = propval?.ToString() : Return True
                Case "Biomass Compound" : BiomassCompound = propval?.ToString() : Return True
                Case "H2S Compound" : H2SCompound = propval?.ToString() : Return True
                Case "Sulfate Compound" : SulfateCompound = propval?.ToString() : Return True
                Case "Influent Sulfate Sulfur" : InfluentSulfateS_mgL = d : Return True
                Case "Substrate Organic Sulfur" : SubstrateOrganicS_gPerKg = d : Return True
                Case "Assumed pH for Sulfide" : AssumedPH_ForSulfide = d : Return True
                Case "COD Removal Efficiency" : CODRemovalEfficiency = d : Return True
                Case "Biomass Yield on COD" : BiomassYield_gVSSpergCOD = d : Return True
                Case "Methane Fraction Override" : MethaneFractionOverride = d : Return True
                Case "Thermal Mode"
                    Dim tm As BioReactorThermalMode
                    If [Enum].TryParse(Of BioReactorThermalMode)(propval?.ToString(), tm) Then ThermalMode = tm
                    Return True
                Case "Heat per g COD removed" : HeatPerGCODremoved_Jg = d : Return True
                Case "Digester Model"
                    Dim m As DigesterModel
                    If [Enum].TryParse(Of DigesterModel)(propval?.ToString(), m) Then Model = m
                    Return True
                Case "ADM1 k_hyd" : ADM1_k_hyd_d = d : Return True
                Case "ADM1 km_su" : ADM1_km_su_d = d : Return True
                Case "ADM1 Ks_su" : ADM1_Ks_su = d : Return True
                Case "ADM1 Y_su" : ADM1_Y_su = d : Return True
                Case "ADM1 km_vfa" : ADM1_km_vfa_d = d : Return True
                Case "ADM1 Ks_vfa" : ADM1_Ks_vfa = d : Return True
                Case "ADM1 Y_ace" : ADM1_Y_ace = d : Return True
                Case "ADM1 KI_h2" : ADM1_KI_h2 = d : Return True
                Case "ADM1 km_ac" : ADM1_km_ac_d = d : Return True
                Case "ADM1 Ks_ac" : ADM1_Ks_ac = d : Return True
                Case "ADM1 Y_am" : ADM1_Y_am = d : Return True
                Case "ADM1 km_h2" : ADM1_km_h2_d = d : Return True
                Case "ADM1 Ks_h2" : ADM1_Ks_h2 = d : Return True
                Case "ADM1 Y_hm" : ADM1_Y_hm = d : Return True
                Case "ADM1 k_dec" : ADM1_k_dec_d = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' ======================================================================
        ' IExternalUnitOperation implementation
        ' ======================================================================

        Private ReadOnly Property IEUO_Name As String Implements IExternalUnitOperation.Name
            Get
                Return GetDisplayName()
            End Get
        End Property

        Private ReadOnly Property IEUO_Description As String Implements IExternalUnitOperation.Description
            Get
                Return GetDisplayDescription()
            End Get
        End Property

        Public ReadOnly Property Prefix As String Implements IExternalUnitOperation.Prefix
            Get
                Return "AD-"
            End Get
        End Property

        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New Reactor_AnaerobicDigester()
        End Function

        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then
                PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel))
                Return
            End If
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim su = FlowSheet.FlowsheetOptions.SelectedUnitSystem
            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat

            container.CreateAndAddLabelRow("General")

            Dim modelNames As New List(Of String) From {"Black Box (Buswell + COD Removal)", "ADM1-Lite (4-population reduced)", "ADM1 Full (Batstone 2002 / BSM2)", "ADM1-S (Full + kinetic sulfate reduction)"}
            container.CreateAndAddDropDownRow("Model", modelNames, CInt(Model),
                Sub(dd, e)
                    Model = CType(dd.SelectedIndex, DigesterModel)
                    If GlobalSettings.Settings.CallSolverOnEditorPropertyChanged Then FlowSheet.RequestCalculation()
                End Sub)

            container.CreateAndAddTextBoxRow(nf, String.Format("Reactor Volume ({0})", su.volume), Volume.ConvertFromSI(su.volume),
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then
                        Volume = tb.Text.ParseExpressionToDouble().ConvertToSI(su.volume)
                        If GlobalSettings.Settings.CallSolverOnEditorPropertyChanged Then FlowSheet.RequestCalculation()
                    End If
                End Sub)

            container.CreateAndAddTextBoxRow(nf, "Hydraulic Residence Time (s)", HRT_s,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then
                        HRT_s = tb.Text.ParseExpressionToDouble()
                        If GlobalSettings.Settings.CallSolverOnEditorPropertyChanged Then FlowSheet.RequestCalculation()
                    End If
                End Sub)

            Dim thermalNames As New List(Of String) From {"Isothermal", "Adiabatic", "Heat Duty"}
            container.CreateAndAddDropDownRow("Thermal Mode", thermalNames, CInt(ThermalMode),
                Sub(dd, e)
                    ThermalMode = CType(dd.SelectedIndex, BioReactorThermalMode)
                    If GlobalSettings.Settings.CallSolverOnEditorPropertyChanged Then FlowSheet.RequestCalculation()
                End Sub)

            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()
            Dim dropdownItems As New List(Of String) From {"(none)"}
            dropdownItems.AddRange(compIds)

            Dim addCompoundDropdown = Sub(host As AvaloniaEditorPanel, label As String, currentValue As String, setter As Action(Of String))
                                          Dim idx = compIds.IndexOf(currentValue)
                                          host.CreateAndAddDropDownRow(label, dropdownItems, If(idx < 0, 0, idx + 1),
                                              Sub(dd, e)
                                                  setter(If(dd.SelectedIndex <= 0, "", compIds(dd.SelectedIndex - 1)))
                                                  If GlobalSettings.Settings.CallSolverOnEditorPropertyChanged Then FlowSheet.RequestCalculation()
                                              End Sub)
                                      End Sub

            container.CreateAndAddLabelRow("Compound Mapping")
            addCompoundDropdown(container, "Substrate", SubstrateCompound, Sub(v) SubstrateCompound = v)
            addCompoundDropdown(container, "Biomass", BiomassCompound, Sub(v) BiomassCompound = v)
            addCompoundDropdown(container, "Methane", MethaneCompound, Sub(v) MethaneCompound = v)
            addCompoundDropdown(container, "CO2", CO2Compound, Sub(v) CO2Compound = v)
            addCompoundDropdown(container, "Water", WaterCompound, Sub(v) WaterCompound = v)
            addCompoundDropdown(container, "Ammonia", NH3Compound, Sub(v) NH3Compound = v)
            addCompoundDropdown(container, "Hydrogen Sulfide (H2S)", H2SCompound, Sub(v) H2SCompound = v)
            addCompoundDropdown(container, "Sulfate carrier (ADM1-S)", SulfateCompound, Sub(v) SulfateCompound = v)

            container.CreateAndAddLabelRow("Sulfur Balance")
            container.CreateAndAddDescriptionRow(
                "Sulfate sulfur carries no COD, so reducing it to sulfide costs 64 kg COD/kmol S out of " &
                "the methane pool; organic sulfur arrives already reduced and makes H2S at no cost in " &
                "methane. Black Box and ADM1-Lite and ADM1 Full debit that COD up front and assume all " &
                "the sulfate is reduced. ADM1-S instead grows four sulfate-reducing populations that " &
                "compete with the methanogens for hydrogen, acetate, propionate and butyrate, so the " &
                "methane loss and the residual sulfate come out of the kinetics.")
            container.CreateAndAddTextBoxRow(nf, "Influent Sulfate Sulfur (mg S/L)", InfluentSulfateS_mgL,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then
                        InfluentSulfateS_mgL = tb.Text.ParseExpressionToDouble()
                        FlowSheet.RequestCalculation()
                    End If
                End Sub)
            container.CreateAndAddTextBoxRow(nf, "Substrate Organic Sulfur (g S/kg, -1 = from formula)", SubstrateOrganicS_gPerKg,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then
                        SubstrateOrganicS_gPerKg = tb.Text.ParseExpressionToDouble()
                        FlowSheet.RequestCalculation()
                    End If
                End Sub)
            container.CreateAndAddTextBoxRow(nf, "Assumed pH for Sulfide (BlackBox/Lite only)", AssumedPH_ForSulfide,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then
                        AssumedPH_ForSulfide = tb.Text.ParseExpressionToDouble()
                        FlowSheet.RequestCalculation()
                    End If
                End Sub)

            container.CreateAndAddLabelRow("Parameters")

            container.CreateAndAddButtonRow("Simplified Model Parameters...", Nothing,
                Sub(btn, e) ShowSimplifiedParamsFormAvalonia(nf))

            container.CreateAndAddButtonRow("ADM1-Lite Initial Conditions && Kinetics...", Nothing,
                Sub(btn, e) ShowADM1LiteParamsFormAvalonia(nf))

            container.CreateAndAddButtonRow("ADM1 Full Parameters (Batstone)...", Nothing,
                Sub(btn, e) ShowADM1FullParamsFormAvalonia(nf))

            container.CreateAndAddLabelRow("Results && Diagnostics")

            container.CreateAndAddButtonRow("ADM1 Trajectory Results...", Nothing,
                Sub(btn, e) ShowADM1ResultsFormAvalonia(nf))

            container.CreateAndAddButtonRow("View Help", Nothing,
                Sub(btn, e)
                    Dim url = "https://dwsim.org/wiki/index.php?title=Anaerobic_Digester"
                    url.OpenURL()
                End Sub)

        End Sub





        Private Sub ShowSimplifiedParamsFormAvalonia(nf As String)
            Dim cnt As AvaloniaEditorPanel = AvaloniaCommon.GetDefaultContainer()
            cnt.CreateAndAddDescriptionRow("Black-box Buswell / COD-removal parameters.")
            cnt.CreateAndAddTextBoxRow(nf, "COD Removal Efficiency (0-1)", CODRemovalEfficiency,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then
                        CODRemovalEfficiency = tb.Text.ParseExpressionToDouble()
                    End If
                End Sub)
            cnt.CreateAndAddTextBoxRow(nf, "Biomass Yield (g VSS / g COD)", BiomassYield_gVSSpergCOD,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then BiomassYield_gVSSpergCOD = tb.Text.ParseExpressionToDouble()
                End Sub)
            cnt.CreateAndAddTextBoxRow(nf, "Methane Fraction Override (0 = auto)", MethaneFractionOverride,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then MethaneFractionOverride = tb.Text.ParseExpressionToDouble()
                End Sub)
            cnt.CreateAndAddTextBoxRow(nf, "Heat per g COD removed (J/g)", HeatPerGCODremoved_Jg,
                Sub(tb, e)
                    If tb.Text.IsValidDoubleExpression() Then HeatPerGCODremoved_Jg = tb.Text.ParseExpressionToDouble()
                End Sub)
            Dim w = AvaloniaCommon.GetDefaultEditorForm("Simplified Model Parameters", 520, 380, cnt)
            w.Show()
        End Sub

        Private Sub ShowADM1LiteParamsFormAvalonia(nf As String)
            Dim tInit As AvaloniaEditorPanel = AvaloniaCommon.GetDefaultContainer()
            tInit.Tag = "Initial Conditions"
            tInit.CreateAndAddDescriptionRow("ADM1-Lite initial concentrations (g/L).")
            Dim addRow = Sub(host As AvaloniaEditorPanel, label As String, getter As Func(Of Double), setter As Action(Of Double))
                             host.CreateAndAddTextBoxRow(nf, label, getter(),
                                 Sub(tb, e)
                                     If tb.Text.IsValidDoubleExpression() Then setter(tb.Text.ParseExpressionToDouble())
                                 End Sub)
                         End Sub
            addRow(tInit, "S_s (soluble substrate)", Function() ADM1_S_s0, Sub(v) ADM1_S_s0 = v)
            addRow(tInit, "S_VFA (volatile fatty acids)", Function() ADM1_S_VFA0, Sub(v) ADM1_S_VFA0 = v)
            addRow(tInit, "S_Ac (acetate)", Function() ADM1_S_Ac0, Sub(v) ADM1_S_Ac0 = v)
            addRow(tInit, "S_H2 (hydrogen)", Function() ADM1_S_H20, Sub(v) ADM1_S_H20 = v)
            addRow(tInit, "X_hyd (hydrolytic biomass)", Function() ADM1_X_hyd0, Sub(v) ADM1_X_hyd0 = v)
            addRow(tInit, "X_ace (acetogenic biomass)", Function() ADM1_X_ace0, Sub(v) ADM1_X_ace0 = v)
            addRow(tInit, "X_am (acetoclastic methanogens)", Function() ADM1_X_am0, Sub(v) ADM1_X_am0 = v)
            addRow(tInit, "X_hm (hydrogenotrophic methanogens)", Function() ADM1_X_hm0, Sub(v) ADM1_X_hm0 = v)

            Dim tKin As AvaloniaEditorPanel = AvaloniaCommon.GetDefaultContainer()
            tKin.Tag = "Kinetics"
            addRow(tKin, "k_hyd (1/d)", Function() ADM1_k_hyd_d, Sub(v) ADM1_k_hyd_d = v)
            addRow(tKin, "km_su (1/d)", Function() ADM1_km_su_d, Sub(v) ADM1_km_su_d = v)
            addRow(tKin, "Ks_su (g/L)", Function() ADM1_Ks_su, Sub(v) ADM1_Ks_su = v)
            addRow(tKin, "Y_su", Function() ADM1_Y_su, Sub(v) ADM1_Y_su = v)
            addRow(tKin, "km_vfa (1/d)", Function() ADM1_km_vfa_d, Sub(v) ADM1_km_vfa_d = v)
            addRow(tKin, "Ks_vfa (g/L)", Function() ADM1_Ks_vfa, Sub(v) ADM1_Ks_vfa = v)
            addRow(tKin, "Y_ace", Function() ADM1_Y_ace, Sub(v) ADM1_Y_ace = v)
            addRow(tKin, "KI_h2 (g/L)", Function() ADM1_KI_h2, Sub(v) ADM1_KI_h2 = v)
            addRow(tKin, "km_ac (1/d)", Function() ADM1_km_ac_d, Sub(v) ADM1_km_ac_d = v)
            addRow(tKin, "Ks_ac (g/L)", Function() ADM1_Ks_ac, Sub(v) ADM1_Ks_ac = v)
            addRow(tKin, "Y_am", Function() ADM1_Y_am, Sub(v) ADM1_Y_am = v)
            addRow(tKin, "km_h2 (1/d)", Function() ADM1_km_h2_d, Sub(v) ADM1_km_h2_d = v)
            addRow(tKin, "Ks_h2 (g/L)", Function() ADM1_Ks_h2, Sub(v) ADM1_Ks_h2 = v)
            addRow(tKin, "Y_hm", Function() ADM1_Y_hm, Sub(v) ADM1_Y_hm = v)
            addRow(tKin, "k_dec (1/d)", Function() ADM1_k_dec_d, Sub(v) ADM1_k_dec_d = v)

            Dim w = AvaloniaCommon.GetDefaultTabbedForm("ADM1-Lite Parameters", 560, 520, New AvaloniaEditorPanel() {tInit, tKin})
            w.Show()
        End Sub

        Private Sub ShowADM1FullParamsFormAvalonia(nf As String)
            If ADM1Params Is Nothing Then ADM1Params = New ADM1.ADM1Parameters()

            Dim buildGroupTab = Function(grp As Object, label As String) As AvaloniaEditorPanel
                                    Dim layout As AvaloniaEditorPanel = AvaloniaCommon.GetDefaultContainer()
                                    layout.Tag = label
                                    If grp Is Nothing Then
                                        layout.CreateAndAddDescriptionRow("(not initialized)")
                                        Return layout
                                    End If
                                    layout.CreateAndAddLabelRow(label)
                                    Dim props = grp.GetType().GetProperties().
                                        Where(Function(p) p.CanRead AndAlso p.CanWrite AndAlso p.PropertyType = GetType(Double)).
                                        ToList()
                                    For Each p In props
                                        Dim propRef = p
                                        Dim val = CDbl(propRef.GetValue(grp, Nothing))
                                        layout.CreateAndAddTextBoxRow(nf, propRef.Name, val,
                                            Sub(tb, e)
                                                If tb.Text.IsValidDoubleExpression() Then
                                                    propRef.SetValue(grp, tb.Text.ParseExpressionToDouble(), Nothing)
                                                End If
                                            End Sub)
                                    Next
                                    Return layout
                                End Function

            Dim tabs As New List(Of AvaloniaEditorPanel)
            tabs.Add(buildGroupTab(ADM1Params.Stoichiometry, "Stoichiometry"))
            tabs.Add(buildGroupTab(ADM1Params.Kinetics, "Kinetics"))
            tabs.Add(buildGroupTab(ADM1Params.Inhibition, "Inhibition"))
            tabs.Add(buildGroupTab(ADM1Params.Physicochemical, "Physicochemical"))
            tabs.Add(buildGroupTab(ADM1Params.Operating, "Operating"))
            ' Only read when the model is ADM1-S; harmless to show and edit either way.
            tabs.Add(buildGroupTab(ADM1Params.Sulfate, "Sulfate Reduction"))

            Dim w = AvaloniaCommon.GetDefaultTabbedForm("ADM1 Full Parameters (Batstone 2002)", 640, 560, tabs.ToArray())
            w.Show()
        End Sub

        Private Sub ShowADM1ResultsFormAvalonia(nf As String)
            Dim cnt As AvaloniaEditorPanel = AvaloniaCommon.GetDefaultContainer()
            If ADM1LastTrajectory Is Nothing OrElse ADM1LastTrajectory.States Is Nothing OrElse ADM1LastTrajectory.States.Count = 0 Then
                cnt.CreateAndAddDescriptionRow("No ADM1 trajectory available. Run the digester in ADM1-Lite or ADM1 Full mode first.")
            Else
                cnt.CreateAndAddLabelRow("ADM1 Final-State Summary")
                cnt.CreateAndAddTwoLabelsRow("Trajectory points:", ADM1LastTrajectory.States.Count.ToString())
                cnt.CreateAndAddTwoLabelsRow("S_s  (g/L):", ADM1_Result_S_s.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("S_VFA (g/L):", ADM1_Result_S_VFA.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("S_Ac (g/L):", ADM1_Result_S_Ac.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("S_H2 (g/L):", ADM1_Result_S_H2.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("X_hyd (g/L):", ADM1_Result_X_hyd.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("X_ace (g/L):", ADM1_Result_X_ace.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("X_am  (g/L):", ADM1_Result_X_am.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("X_hm  (g/L):", ADM1_Result_X_hm.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("pH:", ADM1_Result_pH.ToString(nf))
                cnt.CreateAndAddLabelRow("Bulk Results")
                cnt.CreateAndAddTwoLabelsRow("Biogas flow (mol/s):", Result_BiogasFlow_mols.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("CH4 flow (kg/s):", Result_CH4_kgs.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("CO2 flow (kg/s):", Result_CO2_kgs.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("CH4 mole frac:", Result_CH4MoleFraction.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("Specific CH4 yield (Nm3/kg COD):", Result_SpecificCH4Yield_Nm3kgCOD.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("COD in  (kg/s):", Result_CODin_kgs.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("COD removed (kg/s):", Result_CODremoved_kgs.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("Sludge (kg/s):", Result_Sludge_kgs.ToString(nf))
                cnt.CreateAndAddTwoLabelsRow("Q metabolic (kW):", Result_Q_metabolic_kW.ToString(nf))
            End If
            Dim w = AvaloniaCommon.GetDefaultEditorForm("ADM1 Trajectory Results", 560, 520, cnt)
            w.Show()
        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors

            If GraphicObject Is Nothing Then Return

            Dim w = GraphicObject.Width
            Dim h = GraphicObject.Height
            Dim gx = GraphicObject.X
            Dim gy = GraphicObject.Y

            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 2 Then

                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.6 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Feed"

                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.8 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Effluent"

                GraphicObject.OutputConnectors(1).Position = New Point(gx + 0.5 * w, gy)
                GraphicObject.OutputConnectors(1).ConnectorName = "Biogas"
                GraphicObject.OutputConnectors(1).Direction = ConDir.Up

            Else

                GraphicObject.InputConnectors.Clear()
                GraphicObject.OutputConnectors.Clear()

                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.6 * h),
                    .Type = ConType.ConIn,
                    .Direction = ConDir.Right,
                    .ConnectorName = "Feed"
                })
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.8 * h),
                    .Type = ConType.ConOut,
                    .Direction = ConDir.Right,
                    .ConnectorName = "Effluent"
                })
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + 0.5 * w, gy),
                    .Type = ConType.ConOut,
                    .Direction = ConDir.Up,
                    .ConnectorName = "Biogas"
                })
            End If

            GraphicObject.EnergyConnector.Position = New Point(gx + 0.75 * w, gy + h)
            GraphicObject.EnergyConnector.Direction = ConDir.Up
            GraphicObject.EnergyConnector.Active = True
            GraphicObject.EnergyConnector.ConnectorName = "Heat Duty"

        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw

            If GraphicObject Is Nothing Then Return

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            If GraphicObject.DrawMode = 2 Then
                If UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "anaerobic_digester_photo", _photoImage) Then Return
            End If

            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)

        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Anaerobic digester: big cylindrical tank w/ gas dome top, side ladder, gas outlet pipe.
            Dim vessel As New SKRect(gx + 0.1F * w, gy + 0.1F * h, gx + 0.9F * w, gy + 0.95F * h)
            UnitOperations.BioOpsDrawHelper.DrawDomedTank(canvas, vessel, mono)
            ' liquid level (brown band near bottom 40%)
            Using liq As New SKPaint With {.Color = If(mono, New SKColor(170, 170, 170, 220), New SKColor(160, 130, 85, 220)), .IsAntialias = True}
                canvas.DrawRect(New SKRect(vessel.Left + 2, vessel.Top + vessel.Height * 0.6F, vessel.Right - 2, vessel.Bottom - 2), liq)
            End Using
            ' side ladder
            UnitOperations.BioOpsDrawHelper.DrawLadder(canvas, gx + 0.91F * w, gx + 0.95F * w, gy + 0.15F * h, gy + 0.9F * h, mono)
            ' gas outlet pipe on top-right of dome
            Dim cx = (vessel.Left + vessel.Right) * 0.5F
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(cx + 0.15F * w, gy + 0.02F * h), New SKPoint(cx + 0.15F * w, gy + 0.15F * h), 0.04F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, cx + 0.15F * w, gy + 0.03F * h, 0.1F * w, mono)
            ' feed inlet at bottom-left
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.02F * w, gy + 0.75F * h), New SKPoint(gx + 0.1F * w, gy + 0.75F * h), 0.04F * h, mono)
            ' label
            Using txt As New SKPaint With {.Color = If(mono, New SKColor(30, 30, 30), New SKColor(40, 70, 55)), .IsAntialias = True,
                                           .TextSize = 0.11F * h, .TextAlign = SKTextAlign.Center, .Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)}
                canvas.DrawText("CH" & ChrW(&H2084), cx - 0.05F * w, gy + 0.45F * h, txt)
            End Using
        End Sub

    End Class

End Namespace
