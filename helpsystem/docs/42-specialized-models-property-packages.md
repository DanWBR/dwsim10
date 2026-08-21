# Specialized Models / Property Packages

#### IAPWS-IF97 Steam Tables

Water is used as cooling medium or heat transfer fluid and it plays an important role for air-condition. For conservation or for reaching desired properties, water must be removed from substances (drying). In other cases water must be added (humidification). Also, many chemical reactions take place in hydrous solutions. That’s why a good deal of work has been spent on the investigation and measurement of water properties over the years. Thermodynamic, transport and other properties of water are known better than of any other substance. Accurate data are especially needed for the design of equipment in steam power plants (boilers, turbines, condensers). In this field it’s also important that all parties involved, e.g., companies bidding for equipment in a new steam power plant, base their calculations on the same property data values because small differences may produce appreciable differences.

A standard for the thermodynamic properties of water over a wide range of temperature and pressure was developed in the 1960’s, the 1967 IFC Formulation for Industrial Use (IFC-67). Since 1967 IFC-67 has been used for "official" calculations such as performance guarantee calculations of power cycles.

In 1997, IFC-67 has been replaced by a new formulation, the IAPWS Industrial Formulation 1997 for the Thermodynamic Properties of Water and Steam or **IAPWS-IF97** for short. IAPWS-IF97 was developed in an international research project coordinated by the International Association for the Properties of Water and Steam (IAPWS). The formulation is described in a paper by W. Wagner et al., "The IAPWS Industrial Formulation 1997 for the Thermodynamic Properties of Water and Steam," ASME J. Eng. Gas Turbines and Power, Vol. 122 (2000), pp. 150-182 and several steam table books, among others ASME Steam Tables and Properties of Water and Steam by W. Wagner, Springer 1998.

The IAPWS-IF97 divides the thermodynamic surface into five regions:

- Region 1 for the liquid state from low to high pressures,

- Region 2 for the vapor and ideal gas state,

- Region 3 for the thermodynamic state around the critical point,

- Region 4 for the saturation curve (vapor-liquid equilibrium),

- Region 5 for high temperatures above 1073.15 K (800 °C) and pressures up to 10 MPa (100 bar).

For regions 1, 2, 3 and 5 the authors of IAPWS-IF97 have developed fundamental equations of very high accuracy. Regions 1, 2 and 5 are covered by fundamental equations for the Gibbs free energy g(T,p), region 3 by a fundamental equation for the Helmholtz free energy f(T,v). All thermodynamic properties can then be calculated from these fundamental equations by using the appropriate thermodynamic relations. For region 4 a saturation-pressure equation has been developed.

In chemical engineering applications mainly regions 1, 2, 4, and to some extent also region 3 are of interest. The range of validity of these regions, the equations for calculating the thermodynamic properties, and references are summarized in Attachment 1. The equations of the high-temperature region 5 should be looked up in the references. For regions 1 and 2 the thermodynamic properties are given as a function of temperature and pressure, for region 3 as a function of temperature and density. For other independent variables an iterative calculation is usually required. So-called backward equations are provided in IAPWS-IF97 which allow direct calculation of properties as a function of some other sets of variables (see references).

Accuracy of the equations and consistency along the region boundaries are more than sufficient for engineering applications.

More information about the IAPWS-IF97 Steam Tables formulation can be found at <http://www.thermo.ruhr-uni-bochum.de/en/prof-w-wagner/software/iapws-if97.html?id=172>.

#### IAPWS-08 Seawater

The IAPWS-08 Seawater Property Package is based on the **Seawater-Ice-Air (SIA)** library. The Seawater-Ice-Air (SIA) library contains the **TEOS-10** subroutines for evaluating a wide range of thermodynamic properties of pure water (using IAPWS-95), seawater (using IAPWS-08 for the saline part), ice Ih (using IAPWS-06) and for moist air (using Feistel et al. (2010a), IAPWS (2010)).

**TEOS-10** is based on a Gibbs function formulation from which all thermodynamic properties of seawater (density, enthalpy, entropy sound speed, etc.) can be derived in a thermodynamically consistent manner. TEOS-10 was adopted by the Intergovernmental Oceanographic Commission at its 25th Assembly in June 2009 to replace EOS-80 as the official description of seawater and ice properties in marine science.

A significant change compared with past practice is that TEOS-10 uses Absolute Salinity SA (mass fraction of salt in seawater) as opposed to Practical Salinity SP (which is essentially a measure of the conductivity of seawater) to describe the salt content of seawater. Ocean salinities now have units of g/kg.

Absolute Salinity (g/kg) is an SI unit of concentration. The thermodynamic properties of seawater, such as density and enthalpy, are now correctly expressed as functions of Absolute Salinity rather than being functions of the conductivity of seawater. Spatial variations of the composition of seawater mean that Absolute Salinity is not simply proportional to Practical Salinity; TEOS-10 contains procedures to correct for these effects.

More information about the SIA library can be found at <http://www.teos-10.org/software.htm>.

#### Black-Oil

When fluids flow from a petroleum reservoir to the surface, pressure and temperature decrease. This affects the gas/liquid equilibrium and the properties of the gas and liquid phases. The black-oil model enables estimation of these, from a minimum of input data.

The black-oil model employs 2 pseudo components:

1.  Oil which is usually defined as the produced oil, at stock tank conditions.

2.  Gas which then is defined as the produced gas at atmospheric standard conditions.

The basic modeling assumption is that the gas may dissolve in the liquid hydrocarbon phase, but no oil will dissolve in the gaseous phase. This implies that the composition of the gaseous phase is assumed the same at all pressure and temperatures.

The black-oil model assumption is reasonable for mixtures of heavy and light components, like many reservoir oils. The assumption gets worse for mixtures containing much of intermediate components (propane, butane), and is directly misleading for mixtures of light and intermediate components typically found in condensate reservoirs.

In DWSIM, a set of models calculates properties for a black oil fluid so it can be used in a process simulation. Black-oil fluids are defined in DWSIM through a minimum set of properties:

- Oil specific gravity (SGo) at standard conditions

- Gas specific gravity (SGg) at standard conditions

- Gas-to-oil ratio (GOR) at standard conditions

- Basic Sediments and Water (%)

Black oil fluids are defined and created through the **Compound Creator** tool. If multiple black-oil fluids are added to a simulation, a single fluid is calculated (based on averaged black-oil properties) and used to calculate stream equilibrium conditions and phase properties.

The Black-Oil Property Package is a simplified package for quick process calculations involving the black-oil fluids described above. All properties required by the unit operations are calculated based on the set of four basic properties (SGo, SGg, GOR and BSW), so the results of the calculations cannot be considered precise in any way. They can exhibit errors of several orders of magnitude when compared to real-world data.

For more accurate petroleum fluid simulations, use the petroleum characterization tools available in DWSIM together with an Equation of State model like Peng-Robinson or Soave-Redlich-Kwong.

#### CoolProp

CoolProp is a C++ library that implements pure and pseudo-pure fluid equations of state and transport properties for 114 components.

The CoolProp library currently provides thermophysical data for 114 pure and pseudo-pure working fluids. The literature sources for the thermodynamic and transport properties of each fluid are summarized in a table in the Supporting Information available in the above reference.

For the CoolProp Property Package, DWSIM implements simple mixing rules based on mass fraction averages in order to calculate mixture enthalpy, entropy, heat capacities, density (and compressibility factor as a consequence). For equilibrium calculations, DWSIM requires values of fugacity coefficients at system’s temperature and pressure. In the CoolProp Property Package, the vapor and liquid phases are considered to be ideal.

More information about CoolProp can be found at <http://www.coolprop.org>.

#### Electrolyte NRTL (eNRTL) {#sec:enrtl}

##### Overview {#overview-48}

The Electrolyte Non-Random Two-Liquid (eNRTL) model computes activity coefficients for aqueous electrolyte solutions by splitting the excess Gibbs energy into two additive contributions :


<a id="eq:enrtl_split"></a>

\[
\ln\gamma_{i} = \ln\gamma_{i}^{\mathrm{LR}} + \ln\gamma_{i}^{\mathrm{SR}}
\]


where the long-range (LR) term accounts for electrostatic ion–ion interactions via the Pitzer–Debye–Hückel equation, and the short-range (SR) term captures local-composition effects through the NRTL framework.

##### Long-Range Term — Pitzer–Debye–Hückel {#long-range-term-pitzerdebyehückel}

The ionic strength on a mole-fraction basis is


\[
I_{x} = \tfrac{1}{2}\sum_{i} x_{i} z_{i}^{2}
\]


where $x_{i}$ is the mole fraction and $z_{i}$ the charge number of species $i$.

For an *ion* $i$ the long-range contribution is 


<a id="eq:pdh_ion"></a>

\[
\ln\gamma_{i}^{\mathrm{PDH}} = -A_{\varphi}\, z_{i}^{2}
    \left[
      \frac{\sqrt{I_{x}}}{1+\rho\sqrt{I_{x}}}
      + \frac{2}{\rho}\ln\!\left(1+\rho\sqrt{I_{x}}\right)
    \right]
\]


where $\rho = 14.9$ Å is the closest-approach parameter (default).

For a *solvent* $s$:


\[
\ln\gamma_{s}^{\mathrm{PDH}} =
    \frac{2\,A_{\varphi}\,M_{s}}{\rho^{3}}
    \left[
      1 + \rho\sqrt{I_{x}}
      - \frac{1}{1+\rho\sqrt{I_{x}}}
      - 2\ln\!\left(1+\rho\sqrt{I_{x}}\right)
    \right]
\]


with $M_{s} = 0.018015$ kg/mol for water.

The Debye–Hückel parameter $A_{\varphi}$ (mol$^{1/2}$ kg$^{-1/2}$) depends on temperature and solvent properties:


\[
A_{\varphi} = \frac{1}{3}
    \left(\frac{2\pi N_{\mathrm{A}}\rho_{s}}{1000}\right)^{\!1/2}
    \left(\frac{e^{2}}{\varepsilon\, k_{\mathrm{B}}\, T}\right)^{\!3/2}
\]


where $N_{\mathrm{A}}$ is Avogadro’s number, $\rho_{s}$ the solvent mass density (kg/m$^{3}$), $e$ the elementary charge, $\varepsilon$ the dielectric constant, and $k_{\mathrm{B}}$ Boltzmann’s constant.

##### Short-Range Term — Local Composition NRTL {#short-range-term-local-composition-nrtl}

The short-range contribution follows the two-liquid theory of Renon & Prausnitz  extended to electrolytes by Chen & Evans . Two key assumptions are made: (1) local electroneutrality around every central species, and (2) like-ion repulsion (identically charged ions are not immediate neighbours).

The NRTL $G$-matrix element is


\[
G_{ij} = \exp\!\left(-\alpha\,\tau_{ij}\right)
\]


where $\alpha = 0.2$ (default for molecule–ion pairs) is the non-randomness parameter and $\tau_{ij}$ is the binary interaction energy parameter.

###### Water–electrolyte parameters

For a single salt dissolved in water, two asymmetric $\tau$ parameters are required: $\tau_{w,ca}$ (water around the cation–anion pair) and $\tau_{ca,w}$ (ion pair around water). Temperature dependence is expressed via the three-parameter Gibbs–Helmholtz form of Hossain, Bhattacharia and Chen :


<a id="eq:tau_T"></a>

\[
\tau_{ij}(T) = \Delta g_{ij}
                + \Delta h_{ij}\!\left(\frac{1}{T} - \frac{1}{T_\mathrm{ref}}\right)
                + \Delta c_{p,ij}\!\left(\frac{T_\mathrm{ref}-T}{T} + \ln\frac{T}{T_\mathrm{ref}}\right)
\]


with $T_\mathrm{ref} = 298.15$ K. $\tau_{ij}$, $\Delta g_{ij}$ and $\Delta c_{p,ij}$ are dimensionless; $\Delta h_{ij}$ has units of K. At $T = T_\mathrm{ref}$ the expression collapses to $\tau_{ij} = \Delta g_{ij}$, so the $\Delta g$ coefficients are numerically identical to the reference-temperature $\tau_{w,ca}$ and $\tau_{ca,w}$ values.

###### Activity coefficient for a solvent



\[
\ln\gamma_{m}^{\mathrm{SR}}
  = \sum_{c,a} \left(x_{c} + x_{a}\right)
    \left(
      \frac{x_{m}\,G_{m,c}\,\delta\tau_{m,c}}{D_{c}}
      +\frac{x_{m}\,G_{m,a}\,\delta\tau_{m,a}}{D_{a}}
    \right)
\]


where $\delta\tau_{m,c} = \tau_{m,c} - \sum_{j} x_{j} G_{j,c}\tau_{j,c} / D_{c}$, $D_{c} = x_{m} G_{m,c} + x_{a} G_{a,c}$, and analogously for the anion.

###### Activity coefficient for a cation



\[
\ln\gamma_{c}^{\mathrm{SR}}
  = \sum_{m,a} \frac{G_{m,c}}{D}
    \left(\tau_{m,c} - \frac{\sum_{j} x_{j} G_{j,c}\tau_{j,c}}{D}\right),
  \qquad
  D = \sum_{m} x_{m} G_{m,c} + \sum_{a'} x_{a'} G_{a',c}
\]


The expression for an anion is symmetric, with the roles of cations and anions exchanged.

##### Mean Ionic Activity Coefficient

The mean ionic activity coefficient for salt $\mathrm{C}_{\nu_{+}}\mathrm{A}_{\nu_{-}}$ is


\[
\ln\gamma_{\pm} =
    \frac{\nu_{+}\ln\gamma_{c} + \nu_{-}\ln\gamma_{a}}{\nu_{+} + \nu_{-}}
\]


##### Parameters

| Symbol              | Description                                   | Default  |
|:--------------------|:----------------------------------------------|:---------|
| $\alpha$          | Non-randomness parameter (molecule–ion)       | 0.2      |
| $\varepsilon$     | Solvent dielectric constant                   | 78.54    |
| $\rho_{s}$        | Solvent mass density (kg/m$^{3}$)           | 997.0    |
| $\rho$            | Pitzer–DH closest-approach parameter (Å)      | 14.9     |
| $\tau_{w,ca}$     | Water–electrolyte energy parameter            | fitted   |
| $\tau_{ca,w}$     | Electrolyte–water energy parameter            | fitted   |
| $\Delta g_{ij}$   | Gibbs-energy coefficient of $\tau_{ij}(T)$  | fitted   |
| $\Delta h_{ij}$   | Enthalpic coefficient of $\tau_{ij}(T)$ (K) | fitted   |
| $\Delta c_{p,ij}$ | Heat-capacity coefficient of $\tau_{ij}(T)$ | fitted   |
| $T_\mathrm{ref}$  | Reference temperature for $\tau_{ij}(T)$    | 298.15 K |

eNRTL model parameters

##### Validity Range and Validation {#sec:enrtl_validity}

| Property | Recommended range |
|:---|:---|
| Temperature | 273–473 K (0–200 $^{\circ}$C) for the base parameter set |
| Ionic strength | 0–6 mol/kg for fitted salts; up to saturation when validated |
| Pressure | 1–500 bar (no explicit pressure parameters; uses ideal-gas vapour) |
| Solvent | Water only (mixed-solvent extension not implemented) |
| Acid–base equilibria | Handled via flowsheet-defined equilibrium reactions |

eNRTL recommended operating envelope

###### Parameter database

The DWSIM eNRTL implementation ships with the JSON parameter file `enrtl_parameters.json` containing **137 water–electrolyte $\tau$ pairs**: 45 from the original Chen & Evans  fit (NaCl, KCl, LiCl, HCl, NaBr, KBr, NaI, NaOH, KOH, NaNO$_3$, KNO$_3$, NH$_4$Cl, Na$_2$SO$_4$, K$_2$SO$_4$, CaCl$_2$, MgCl$_2$, BaCl$_2$, MgSO$_4$, CaSO$_4$, H$_2$SO$_4$, plus 25 additional 1–1, 1–2, 2–1, 3–1 pairs) and 92 auto-fitted entries derived from Kim & Frederick  single-salt Pitzer parameters (transition-metal halides, perchlorates, nitrates, sulfates and selected 1–1 oxoanion salts). Temperature dependence is encoded with the three-parameter form of Eq. [\[eq:tau_T\]](#eq:tau_T) where measured calorimetric data are available; otherwise the $\Delta h$ and $\Delta c_{p}$ coefficients default to zero.

###### Application domains

- **Cooling-water and process-water chemistry:** dilute mixed electrolyte solutions for pH, conductivity and corrosion-precursor analysis.

- **Brine-handling and geothermal processes:** concentrated Na/K/Ca/Mg chloride and sulfate streams up to the $\sim$<!-- -->6 mol/kg validity ceiling.

- **Acid-gas treating with strong electrolytes:** sour-water and ammonia–carbonate systems where dissociation reactions are added to the flowsheet’s reaction set.

- **Salt crystallisation:** fractional crystallisation flowsheets coupled with the equilibrium solver, exposing $\Delta G^{\circ}$ precipitation reactions.

###### Validation against experimental $\gamma_{\pm}$. {#validation-against-experimental-gamma_pm.}

For the salts in the `water_electrolyte_pairs` set, the implementation reproduces the published Chen–Evans correlations to within numerical precision. For the auto-fitted Kim–Frederick subset, residuals against the underlying isopiestic / EMF data of Robinson & Stokes  are typically $\chi^{2} < 10^{-2}$ over $m = 0.05\,m_{\max}$ to $0.95\,m_{\max}$ ($m_{\max}$ being each salt’s documented saturation molality), with worst-case deviations confined to the high-$m$ tail of strongly ion-paired systems (notably FeCl$_3$, ZnBr$_2$, ZnI$_2$).

###### Pitzer fallback for unparameterised pairs

When a salt encountered at runtime has no $\tau$ entry in `enrtl_parameters.json` but a Pitzer single-salt parameter set is available in the companion file `pitzer_parameters.json` (see Section [6.15](#sec:pitzer_database)), the property package emits a warning to the flowsheet log identifying the pair and reporting the available Pitzer $\beta^{0}$, $\beta^{1}$, $C^{\varphi}$ values. The unknown pair otherwise contributes $\tau = 0$ to the short-range term (ideal residual contribution; only the Pitzer–Debye–Hückel long-range term is then active).

#### Extended UNIQUAC {#sec:exuniquac}

##### Overview {#overview-49}

The Extended UNIQUAC model of Thomsen et al.  computes activity coefficients for water and ionic species in aqueous electrolyte solutions. It adds an extended Debye–Hückel electrostatic term to the standard UNIQUAC expression :


<a id="eq:uniquac_split"></a>

\[
\ln\gamma_{i} =
    \ln\gamma_{i}^{\mathrm{comb}}
    + \ln\gamma_{i}^{\mathrm{res}}
    + \ln\gamma_{i}^{\mathrm{DH}}
\]


##### Combinatorial Term

The combinatorial (entropic) term has the standard UNIQUAC form:


\[
\ln\gamma_{i}^{\mathrm{comb}}
  = \ln\frac{\Phi_{i}}{x_{i}}
    + 1 - \frac{\Phi_{i}}{x_{i}}
    - \frac{Z}{2}\,q_{i}
      \left[\ln\frac{\Theta_{i}}{\Phi_{i}} + 1 - \frac{\Theta_{i}}{\Phi_{i}}\right]
\]


where $Z = 10$ is the lattice coordination number,


\[
\Phi_{i} = \frac{r_{i}\,x_{i}}{\displaystyle\sum_{j} r_{j}\,x_{j}},
  \qquad
  \Theta_{i} = \frac{q_{i}\,x_{i}}{\displaystyle\sum_{j} q_{j}\,x_{j}}
\]


and $r_{i}$, $q_{i}$ are the UNIQUAC volume and surface-area parameters, respectively.

###### Infinite-dilution correction for ions

Ions use the unsymmetric (McInnes) reference convention. The infinite-dilution combinatorial term is evaluated at $x_{\mathrm{w}} = 1$:


\[
\ln\gamma_{i}^{\mathrm{comb},\infty}
  = \ln\frac{r_{i}}{r_{\mathrm{w}}}
    + 1 - \frac{r_{i}}{r_{\mathrm{w}}}
    - \frac{Z}{2}\,q_{i}
      \left[
        \ln\frac{q_{i}/q_{\mathrm{w}}}{r_{i}/r_{\mathrm{w}}}
        + 1 - \frac{q_{i}/q_{\mathrm{w}}}{r_{i}/r_{\mathrm{w}}}
      \right]
\]


##### Residual Term

The residual (enthalpic) term is


\[
\ln\gamma_{i}^{\mathrm{res}} = q_{i}
    \left[
      1 - \ln\!\left(\sum_{j}\Theta_{j}\,\psi_{ji}\right)
        - \sum_{j}\frac{\Theta_{j}\,\psi_{ij}}{\sum_{k}\Theta_{k}\,\psi_{kj}}
    \right]
\]


where the UNIQUAC segment interaction parameter is


\[
\psi_{ji}(T) = \exp\!\left(-\frac{u_{ji}(T) - u_{ii}(T)}{T}\right)
\]


with a linear temperature dependence of the interaction energies:


<a id="eq:uT"></a>

\[
u_{ij}(T) = u_{ij}^{0} + u_{ij}^{T}\,(T - 298.15)
\]


The same infinite-dilution correction is applied to ions: $\ln\gamma_{i}^{\mathrm{res},*} =
 \ln\gamma_{i}^{\mathrm{res}} - \ln\gamma_{i}^{\mathrm{res},\infty}$.

##### Debye–Hückel Electrostatic Term

Following the extended Debye–Hückel formulation of Sander et al. , the electrostatic contribution is:

For an *ion* $i$:


<a id="eq:dh_ion"></a>

\[
\ln\gamma_{i}^{\mathrm{DH}}
  = -\frac{A(T)\,z_{i}^{2}\,\sqrt{I_{x}}}{1 + b\sqrt{I_{x}}}
\]


For *water*:


<a id="eq:dh_water"></a>

\[
\ln\gamma_{\mathrm{w}}^{\mathrm{DH}}
  = \frac{2\,A(T)\,M_{\mathrm{w}}}{b^{3}}
    \left[
      1 + b\sqrt{I_{x}}
      - \frac{1}{1+b\sqrt{I_{x}}}
      - 2\ln\!\left(1+b\sqrt{I_{x}}\right)
    \right]
\]


where $b = 1.5$ (kg/mol)$^{1/2}$ and $M_{\mathrm{w}} = 0.018015$ kg/mol.

The Debye–Hückel parameter $A(T)$ is given by the polynomial fit valid from 240 K to 540 K :


<a id="eq:A_T"></a>

\[
A(T) = 1.131 + 1.335\times10^{-3}(T-273.15) + 1.164\times10^{-5}(T-273.15)^{2}
\]


The mole-fraction ionic strength is


\[
I_{x} = \tfrac{1}{2}\sum_{i} x_{i}\,z_{i}^{2}
\]


##### Reference Convention for Ions

For ionic species the total activity coefficient uses the unsymmetric (infinite-dilution) normalisation:


\[
\ln\gamma_{i}^{*} =
    \bigl(\ln\gamma_{i}^{\mathrm{comb}} - \ln\gamma_{i}^{\mathrm{comb},\infty}\bigr)
    + \bigl(\ln\gamma_{i}^{\mathrm{res}} - \ln\gamma_{i}^{\mathrm{res},\infty}\bigr)
    + \ln\gamma_{i}^{\mathrm{DH}}
\]


Water uses the symmetric (Raoult) convention: $\ln\gamma_{\mathrm{w}} =
 \ln\gamma_{\mathrm{w}}^{\mathrm{comb}}
 + \ln\gamma_{\mathrm{w}}^{\mathrm{res}}
 + \ln\gamma_{\mathrm{w}}^{\mathrm{DH}}$.

##### Parameters

| Symbol | Description | Default / source |
|:---|:---|:---|
| $Z$ | Coordination number | 10 |
| $r_{i}$ | UNIQUAC volume parameter | Thomsen (1997) |
| $q_{i}$ | UNIQUAC surface-area parameter | Thomsen (1997) |
| $u_{ij}^{0}$ | Base interaction energy (K) | fitted |
| $u_{ij}^{T}$ | Temperature coefficient of $u_{ij}$ | fitted |
| $b$ | Debye–Hückel $b$ parameter (kg/mol)$^{1/2}$ | 1.5 |
| $A_{0},A_{1},A_{2}$ | Polynomial coefficients of $A(T)$ | Eq. [\[eq:A_T\]](#eq:A_T) |

Extended UNIQUAC model parameters

##### Validity Range and Validation {#sec:exuniquac_validity}

| Property | Recommended range |
|:---|:---|
| Temperature | 273–473 K (0–200 $^{\circ}$C) for the Thomsen 1997 base set |
|  | up to 523 K (250 $^{\circ}$C) for systems covered by García 2005–2006 |
| Ionic strength | 0–6 mol/kg for the base 12-ion set |
|  | 0–3 mol/kg for transition metals (when added from Hashemi 2017) |
| Pressure | 1–1000 bar (pressure-dependent $K_{sp}$ via García 2006 Table 5) |
| Solvents | water + methanol + ethanol + 1-/2-propanol + 1-/2-butanol |
|  | \+ i-/t-butanol + MEA + MDEA |

Extended UNIQUAC recommended operating envelope

###### Parameter database scope

The shipping JSON parameter file `ExtendedUNIQUAC_Parameters.json` contains:

- **31 species with full $r,q$ values:** the 12-ion Thomsen 1997 base set (H$_2$O, H$^{+}$, Na$^{+}$, K$^{+}$, NH$_4^{+}$, Cl$^{-}$, SO$_4^{2-}$, HSO$_4^{-}$, NO$_3^{-}$, OH$^{-}$, CO$_3^{2-}$, HCO$_3^{-}$, S$_2$O$_8^{2-}$); alkaline-earth extension Ca$^{2+}$, Ba$^{2+}$, Sr$^{2+}$, Mg$^{2+}$ (García 2005, 2006); Cs$^{+}$ (Pereda 2000); the seven alcohols methanol, ethanol, 1-/2-propanol, 1-/2-/i-/t-butanol (Thomsen 2004B ); CO$_2$(aq) and H$_2$NCOO$^{-}$ (Faramarzi 2009 ); and the five alkanolamine species MEA, MEAH$^{+}$, MEA-carbamate, MDEA, MDEAH$^{+}$.

- **$\sim$<!-- -->120 binary interaction parameters** ($u_{ij}^{0},
          u_{ij}^{T}$): cation–anion, cation–neutral, anion–neutral, ion–solvent and self-interaction terms covering the species combinations actually fit in the source publications.

- **Pressure-dependence parameters** ($\alpha$, $\beta$ in $\ln K_{sp}(P)/K_{sp}(P_{0}) = \alpha\,(P{-}P_{0}) +
          \beta\,(P{-}P_{0})^{2}$) for BaSO$_4$, SrSO$_4$, CaSO$_4$, CaSO$_4{\cdot}2$H$_2$O, NaCl and CaCO$_3$ from García 2006 Table 5.

- **Standard-state thermodynamic data** ($\Delta G_{f}^{\circ}$, $\Delta H_{f}^{\circ}$, $C_{p}^{\circ}(T)$) for each species, consistent with the original NBS tables of Wagman .

###### Application domains

- **Geothermal scaling prediction:** CaSO$_4$, BaSO$_4$, SrSO$_4$, CaCO$_3$, MgCO$_3$ saturation indices in produced waters at field $T,P$ (García 2005, 2006).

- **Industrial crystallisation:** fractional crystallisation of Na$_2$SO$_4{\cdot}10$H$_2$O, K$_2$SO$_4$, NaHCO$_3$ and other Thomsen 1997 hydrates with rigorous solid–liquid–vapour equilibrium.

- **Mixed-solvent salt processes:** methanol-, ethanol- and butanol-water-salt systems for solvent-displacement crystallisation (Iliuta 2000 , Thomsen 2004B ).

- **Post-combustion CO$_2$ capture:** aqueous MEA and MDEA absorber/stripper modelling with explicit MEA-carbamate speciation (Faramarzi 2009 ).

- **Acid-gas treating in mixed amines:** MEA–MDEA blends for selective H$_2$S/CO$_2$ removal.

###### Cross-validation against primary sources

The shipped parameter set has been line-by-line cross-checked against the original publications: $r,q$ values for the 12-ion base set match Thomsen 1997 thesis Table 1 verbatim (e.g. $r_{\mathrm{Na^{+}}} = 1.4034$, $q_{\mathrm{Cl^{-}}} = 10.197$); $u_{ij}^{0}$ values reproduce thesis Tables 2 and 3 to all decimal places shown (e.g. $u^{0}(\mathrm{H_2O,Na^{+}}) = 733.286$, $u^{0}(\mathrm{Na^{+},Cl^{-}}) = 1443.23$); the alcohol and alkanolamine extensions match Thomsen 2004B and Faramarzi 2009 within rounding.

###### Convention caveat: transition metals

For Fe$^{2+/3+}$, Cu$^{2+}$, Zn$^{2+}$, Ni$^{2+}$, Co$^{2+}$, Mn$^{2+}$, Cd$^{2+}$, Pb$^{2+}$, Ag$^{+}$, Al$^{3+}$, Cr$^{3+}$ and similar heavy-metal cations, the JSON does **not** contain Thomsen-convention parameters (the only published Extended UNIQUAC fits for these species are in Hashemi 2017 , which uses a non-standard H$^{+}$ convention that is incompatible with the rest of the parameter set). At runtime, missing $u_{ij}$ values default to zero, so transition metals contribute only via the Pitzer–Debye–Hückel long-range term and the combinatorial $r{=}q{=}1$ fallback. Use the eNRTL model for these ions instead, where the Pitzer-derived $\tau$ values are available.

#### Kent–Eisenberg {#sec:ke}

##### Overview {#overview-50}

The Kent–Eisenberg model  describes the vapour–liquid equilibrium of acid gases ( and/or ) absorbed in aqueous amine solutions (MEA, DEA, MDEA). It uses chemical equilibrium constants to describe the ionic speciation in the liquid phase, and Henry’s law to relate liquid-phase molecular concentrations to gas-phase partial pressures. Activity coefficients are absorbed into effective, regressed equilibrium constants, resulting in a simplified model well suited to process-simulation contexts .

##### Chemical Reactions

The following reactions are considered in the liquid phase:


<a id="rxn:K1"></a><a id="rxn:K2"></a><a id="rxn:Kw"></a><a id="rxn:KH2S"></a><a id="rxn:Kp"></a><a id="rxn:Kc"></a>

\[
\begin{alignat}
{3}
  \ce{CO2(aq) + H2O} &\;\ce{<=>}\; \ce{H+ + HCO3-}        &&\qquad K_{1}
    \\
  \ce{HCO3-}         &\;\ce{<=>}\; \ce{H+ + CO3^{2-}}      &&\qquad K_{2}
    \\
  \ce{H2O}           &\;\ce{<=>}\; \ce{H+ + OH-}           &&\qquad K_{w}
    \\
  \ce{H2S(aq)}       &\;\ce{<=>}\; \ce{H+ + HS-}           &&\qquad K_{\mathrm{H_{2}S}}
    \\
  \ce{RNH2 + H+}     &\;\ce{<=>}\; \ce{RNH3+}              &&\qquad K_{p}
    \\
  \ce{2\,RNH2 + CO2} &\;\ce{<=>}\; \ce{RNH3+ + RNHCOO-}   &&\qquad K_{c}
\end{alignat}
\]


Reaction [\[rxn:Kc\]](#rxn:Kc) (carbamate formation) applies to primary and secondary amines (MEA, DEA) only; MDEA does not form a stable carbamate .

##### Equilibrium Constants

All equilibrium constants follow the three-parameter correlation:


<a id="eq:lnK"></a>

\[
\ln K(T) = \frac{A}{T} + B + C\,T
\]


Table [20](#tab:ke_params) lists the fitted parameters calibrated over the range 298–403 K (25–130 °C).



<a id="tab:ke_params"></a>



| Constant                |  $A$ (K)   |    $B$    | $C$ (K$^{-1}$) |
|:------------------------|:------------:|:-----------:|:------------------:|
| $K_{1}$               | $-12092.1$ | $235.482$ |     $-0.398$     |
| $K_{2}$               | $-12431.7$ | $220.067$ |     $-0.350$     |
| $K_{w}$               | $-13445.9$ | $22.477$  |       $0$        |
| $K_{\mathrm{H_{2}S}}$ | $-11862.4$ | $21.000$  |       $0$        |
| $K_{p}$ (MEA)         | $-8190.0$  |  $26.5$   |       $0$        |
| $K_{p}$ (DEA)         | $-7986.0$  |  $23.9$   |       $0$        |
| $K_{p}$ (MDEA)        | $-9396.0$  |  $32.0$   |       $0$        |
| $K_{c}$ (MEA)         |  $2545.0$  |  $-3.05$  |       $0$        |
| $K_{c}$ (DEA)         |  $1350.0$  |  $-4.20$  |       $0$        |

Kent–Eisenberg equilibrium-constant parameters



##### Henry’s Law

The partial pressure of each dissolved gas is


\[
p_{i} = H_{i}(T)\,c_{i}
\]


where $c_{i}$ is the liquid-phase molar concentration of the molecular (dissolved) species (mol/L) and $H_{i}(T)$ is the Henry constant. Temperature dependence:


\[
H_{i}(T) = H_{i}^{25}\exp\!\left[
    -C_{H,i}\!\left(\frac{1}{T} - \frac{1}{298.15}\right)
  \right]
\]


with $H_{\mathrm{CO_{2}}}^{25} = 29.41$ bar$\cdot$L/mol, $C_{H,\mathrm{CO_{2}}} = 2400$ K; $H_{\mathrm{H_{2}S}}^{25} = 9.86$ bar$\cdot$L/mol, $C_{H,\mathrm{H_{2}S}} = 2100$ K.

##### Charge Balance and Solution Method

All ionic concentrations are expressed as explicit functions of $h = [\ce{H+}]$:


\[
[\ce{HCO3-}] = \frac{K_{1}\,c_{\ce{CO2}}}{h},
  \qquad
  [\ce{CO3^{2-}}] = \frac{K_{2}\,[\ce{HCO3-}]}{h},
  \qquad
  [\ce{HS-}] = \frac{K_{\mathrm{H_{2}S}}\,c_{\ce{H2S}}}{h},
  \qquad
  [\ce{OH-}] = \frac{K_{w}}{h}
\]


The free amine and protonated-amine concentrations are


\[
[\mathrm{RNH_{2}}] = \frac{C_{\mathrm{amine}}}
    {1 + h/K_{p} + K_{c}\,K_{p}\,c_{\ce{CO2}}/h},
  \qquad
  [\mathrm{RNH_{3}^{+}}] = \frac{[\mathrm{RNH_{2}}]\,h}{K_{p}}
\]


The electroneutrality condition


\[
[\ce{H+}] + [\mathrm{RNH_{3}^{+}}]
  = [\ce{OH-}] + [\ce{HCO3-}] + 2[\ce{CO3^{2-}}]
    + [\ce{HS-}] + [\mathrm{RNHCOO^{-}}]
\]


is solved for $h$ by Newton–Raphson iteration, starting from $h^{(0)} = 10^{-9}$ mol/L.

##### Acid-Gas Loading

The and loadings (mol acid gas / mol amine) are


\[
\alpha_{\ce{CO2}} =
    \frac{[\ce{HCO3-}] + [\ce{CO3^{2-}}] + [\mathrm{RNHCOO^{-}}]}
         {C_{\mathrm{amine}}},
  \qquad
  \alpha_{\ce{H2S}} = \frac{[\ce{HS-}]}{C_{\mathrm{amine}}}
\]


##### Validity Range and Application {#sec:ke_validity}

| Property | Recommended range |
|:---|:---|
| Temperature | 298–394 K (25–120 $^{\circ}$C) per Kent & Eisenberg 1976 fit |
| Amine | MEA, DEA, MDEA (single-amine, no blends) |
| Amine concentration | 10–40 wt % in water |
| CO$_2$ loading | 0–0.6 mol/mol amine for MEA, 0–0.7 for MDEA |
| H$_2$S loading | 0–0.7 mol/mol amine |
| Total pressure | 1–70 bar (vapour assumed ideal) |
| Acid-gas partial pressure | $10^{-2}$ to $10^{4}$ kPa |

Kent–Eisenberg recommended operating envelope

###### Application domains

- **Refinery acid-gas treating units:** sweetening of fuel-gas and refinery-gas streams via MEA or DEA absorbers.

- **Natural-gas sweetening:** CO$_2$/H$_2$S removal with MDEA for selective H$_2$S absorption.

- **Pre-screening / preliminary design:** fast equilibrium evaluation before committing to a more rigorous Extended UNIQUAC or rate-based amine model.

###### Limitations

The Kent–Eisenberg model bundles activity coefficients into effective equilibrium constants regressed against loading data. As a consequence:

- Predictions outside the original Kent–Eisenberg amine–$T$–loading grid carry larger uncertainty than rigorous activity-coefficient models.

- Mixed amines (MEA + MDEA, etc.) are *not* supported by the original parameterisation; use Faramarzi 2009  Extended UNIQUAC for blend systems.

- No explicit ionic strength dependence; the model is best for amine-only solutions without spectator electrolytes.

#### Sour Water (Edwards Model) {#sec:sourwater}

##### Overview {#overview-51}

The Sour Water model is based on the fugacity-based VLE framework of Edwards, Maurer, Newman and Prausnitz . It targets aqueous systems containing , , , and dissolved in water. Activity coefficients for the dissolved molecular species are calculated by a simplified Margules–Pitzer expression.

##### Vapour–Liquid Equilibrium

The VLE relationship for each volatile molecular species is


<a id="eq:sw_vle"></a>

\[
p_{i} = H_{i}(T)\,x_{i}\,\gamma_{i}
\]


where $H_{i}(T)$ is the Henry constant (Pa$\cdot$m$^{3}$/mol), $x_{i}$ the liquid mole fraction of the molecular species, and $\gamma_{i}$ the activity coefficient.

##### Activity Coefficients

Following Edwards et al. , the activity coefficient of each dissolved molecular species depends on the ionic strength through a simplified Pitzer expression:


<a id="eq:beta_I"></a>

\[
\ln\gamma_{i} = \beta_{i}\,I
\]


where $I$ (mol/kg) is the ionic strength and $\beta_{i}$ is an empirical molecule–ion interaction parameter. Values from Edwards et al.  are listed in Table [21](#tab:beta).



<a id="tab:beta"></a>



| Species | $\beta_{i}$ |
|:--------|:-------------:|
|         |  $-0.324$   |
|         |  $-0.190$   |
|         |  $-0.292$   |
|         |  $-0.160$   |

Molecule–ion interaction parameters $\beta_{i}$ (kg/mol)



##### Ionic Strength

The ionic strength in the liquid phase is


<a id="eq:ionic_strength"></a>

\[
I = \tfrac{1}{2}\sum_{i} c_{i}\,z_{i}^{2}
\]


where the sum runs over all ionic species. For the sour water system (, , , , , , , ):


\[
I = \tfrac{1}{2}\!\left(
      c_{\ce{HS-}}
    + 4\,c_{\ce{S^{2-}}}
    + c_{\ce{NH4+}}
    + c_{\ce{HCO3-}}
    + 4\,c_{\ce{CO3^{2-}}}
    + c_{\ce{CN-}}
    + c_{\ce{H+}}
    + c_{\ce{OH-}}
  \right)
\]


##### Speciation Model

Ionic concentrations are computed from total dissolved-gas concentrations and solution pH via dissociation fractions. With $h = 10^{-\mathrm{pH}}$ mol/L:

######  system: {#system}



\[
\alpha_{0}^{\ce{H2S}} = \frac{1}{D_{\ce{H2S}}},
  \quad
  \alpha_{1}^{\ce{H2S}} = \frac{K_{a1}/h}{D_{\ce{H2S}}},
  \quad
  \alpha_{2}^{\ce{H2S}} = \frac{K_{a1}\,K_{a2}/h^{2}}{D_{\ce{H2S}}}
\]


with $D_{\ce{H2S}} = 1 + K_{a1}/h + K_{a1}\,K_{a2}/h^{2}$. Analogous expressions apply to the and systems.

######  system: {#system-1}



\[
\alpha_{\ce{NH3}} = \frac{1}{1 + h/K_{a}^{\ce{NH4+}}},
  \qquad
  \alpha_{\ce{NH4+}} = \frac{h/K_{a}^{\ce{NH4+}}}{1 + h/K_{a}^{\ce{NH4+}}}
\]


##### Temperature Dependence of Equilibrium Constants

All equilibrium constants are corrected for temperature via the van’t Hoff equation:


\[
K(T) = K_{25}\exp\!\left[
    -\frac{\Delta H_{\mathrm{rxn}}}{R}
    \left(\frac{1}{T} - \frac{1}{298.15}\right)
  \right]
\]


Table [22](#tab:sw_keq) lists the reference constants and reaction enthalpies.



<a id="tab:sw_keq"></a>



| Reaction | $K_{25}$             | $\Delta H_{\mathrm{rxn}}$ (J/mol) | Source |
|:---------|:-----------------------|:------------------------------------|:-------|
|          | $1.02\times10^{-7}$  | $-22{,}000$                       |        |
|          | $1.30\times10^{-14}$ | $-16{,}000$                       |        |
|          | $1.80\times10^{-5}$  | $+35{,}000$                       |        |
|          | $4.30\times10^{-7}$  | $+4{,}100$                        |        |
|          | $4.70\times10^{-11}$ | $+14{,}900$                       |        |
|          | $6.20\times10^{-10}$ | $+12{,}100$                       |        |
|          | $1.01\times10^{-14}$ | $+55{,}800$                       |        |

Equilibrium constants and reaction enthalpies at 25 °C



Henry constants and temperature coefficients are given in Table [23](#tab:sw_henry).



<a id="tab:sw_henry"></a>



| Species | $H_{25}$ (Pa$\cdot$m$^{3}$/mol) | $C_{H}$ (K) | Source |
|:--------|:--------------------------------------|:--------------|:-------|
|         | $97{,}500$                          | 2100          |        |
|         | $58$                                | 4100          |        |
|         | $29{,}400$                          | 2400          |        |
|         | $115$                               | 3300          |        |

Henry constants at 25 °C for the sour water model



#### Vapour-Phase Fugacity Convention {#sec:vapor_fugacity_mode}

Every electrolyte property package inherits the `VaporPhaseFugacityCalculationMode` setting from the DWSIM core `PropertyPackage` base class. Two modes are available:

| Mode | $\varphi_{i}^{V}(T,P,\boldsymbol{y})$ | Recommended pressure range |
|:---|:---|:---|
| `Ideal` (default) | $1$ for non-ions, $10^{10}$ for ions/salts | $\lesssim 10$ bar |
| `PengRobinson` | evaluated via `ThermoPlugs.PR` EOS | up to $\sim$<!-- -->200 bar |

Vapour-phase fugacity coefficient choices

##### Mode behaviour by package

| Property package | Vapour fugacity behaviour |
|:---|:---|
| Electrolyte NRTL | Switches between Ideal and PR via the helper |
| Extended UNIQUAC | Switches between Ideal and PR via the helper |
| Sour Water (Edwards) | Switches between Ideal and PR via the helper |
| H$_2$O–HCl (Pitzer) | Switches between Ideal and PR via the helper |
|  | \+ Poynting correction on $p_{\mathrm{HCl}}$ regardless of mode |
| Glycol (TEG/MEG/DEG) | Inherits base `ActivityCoefficientPropertyPackage` dispatch (PR by default) |
| Kent–Eisenberg | SRK EOS (always real-gas, inherits `SRKPropertyPackage`) |

How each electrolyte PP honours the vapour-fugacity mode

###### When to switch

The default `Ideal` mode is appropriate for the vast majority of electrolyte applications, where the vapour phase contains water vapour and traces of dissolved acid gases (, , , ) at near-atmospheric total pressure. Switching to `PengRobinson` is recommended when:

- Total pressure exceeds $\sim$<!-- -->30 bar (sour-gas treating, geothermal wellhead chemistry, deep-water injection).

- The vapour contains a substantial mole fraction of CO$_2$, N$_2$, methane or other gases whose departure from ideality is non-negligible.

- Cross-validation against process measurements at high pressure shows the ideal-gas assumption underpredicts the gas-phase partition of acid gases by more than $\sim$<!-- -->5 %.

The setting is exposed on the standard PP-settings tab (cross-platform editor uses `EditPP.PopulateCrossPlatformEditor`); it is persisted in the flowsheet XML via the inherited `SaveData` hook under the element `<VaporPhaseFugacityCalculationMode>`.

###### Implementation note

The shared helper `DWSIM.Extensions.PropertyPackages.Electrolytes.PropertyPackages.VaporFugacityHelper.Calculate` encapsulates the dispatch: when the mode is `Ideal` it returns $\varphi = 1$; when the mode is `PengRobinson` it instantiates `DWSIM.Thermodynamics.PropertyPackages.ThermoPlugs.PR` and calls its `CalcLnFug` routine with the property package’s $T_{c}$, $P_{c}$, $\omega$ and $k_{ij}$ arrays. Ions and salts always receive a sentinel $10^{10}$ regardless of mode, which suffices to exclude them from the vapour phase in any standard flash algorithm.

#### Excess Enthalpy and Heat Capacity {#sec:gE_deriv}

All three activity-coefficient models (eNRTL, Extended UNIQUAC, and the Edwards model) implement a common interface and provide derived excess properties via numerical differentiation.

The molar excess enthalpy is obtained from the Gibbs–Helmholtz equation:


\[
H^{\mathrm{E}} = -RT^{2} \sum_{i} x_{i}\,\frac{\partial\ln\gamma_{i}}{\partial T}
  \approx -RT^{2} \sum_{i} x_{i}\,
    \frac{\gamma_{i}(T+\varepsilon) - \gamma_{i}(T)}{\varepsilon}
\]


and the molar excess heat capacity by a second numerical differentiation:


\[
C_{p}^{\mathrm{E}} = \frac{\partial H^{\mathrm{E}}}{\partial T}
  \approx \frac{H^{\mathrm{E}}(T+\varepsilon) - H^{\mathrm{E}}(T)}{\varepsilon}
\]


with $\varepsilon = 0.001$ K in both cases.

#### Aqueous-Phase Transport Properties {#sec:transport}

Dissolved ions modify the transport properties of the aqueous solvent. The following ion-additive correlations are applied as multiplicative correction factors to the pure-solvent viscosity and thermal conductivity computed by the underlying mixing rules (`AUX_LIQVISCm` and `AUX_CONDTL`).

##### Viscosity — Jones–Dole Equation {#sec:jones_dole}

The Jones–Dole equation , with the Kaminsky extension , relates the viscosity of an electrolyte solution to the pure-solvent value $\eta_{0}$:


<a id="eq:jones_dole"></a>

\[
\frac{\eta}{\eta_{0}}
  = 1
    + A\sqrt{I}
    + \sum_{i} B_{i}(T)\,c_{i}
    + D\left(\sum_{i} c_{i}\right)^{\!2}
\]


where $I$ (mol/L) is the ionic strength, $c_{i}$ (mol/L) the molar concentration of ion $i$, $A$ the Falkenhagen electrostatic coefficient, $B_{i}$ the ion-specific Jones–Dole *B-coefficient*, and $D$ the empirical Kaminsky quadratic coefficient.

###### Falkenhagen coefficient

An approximate temperature-dependent expression is used: $A \approx 0.005\sqrt{298.15/T}$. For typical 1:1 electrolytes at 25 °C the term $A\sqrt{I}$ contributes less than 0.5% and is only significant at very low concentrations .

###### Kaminsky coefficient

A global average $D = 0.007$ is used for all electrolytes. This quadratic term becomes relevant above $\sim\!1$ mol/L total ion concentration.

###### B-coefficients

The Jones–Dole $B$-coefficient is ion-specific and reflects whether the ion is a *structure maker* ($B > 0$, increases viscosity) or *structure breaker* ($B < 0$, decreases viscosity) with respect to the hydrogen-bond network of water. $B$ has a mild linear temperature dependence :


\[
B_{i}(T) = B_{i}^{25} + \frac{dB_{i}}{dT}\,(T - 298.15)
\]


Representative values from Marcus  and Jenkins & Marcus  are listed in Table [24](#tab:jones_dole_B).



<a id="tab:jones_dole_B"></a>



| Cation | $B^{25}$ | Anion | $B^{25}$ |
|:-------|-----------:|:------|-----------:|
|        |  $0.068$ |       | $-0.007$ |
|        |  $0.150$ |       |  $0.112$ |
|        |  $0.086$ |       |  $0.107$ |
|        | $-0.007$ |       | $-0.032$ |
|        | $-0.007$ |       | $-0.068$ |
|        |  $0.285$ |       |  $0.032$ |
|        |  $0.385$ |       |  $0.294$ |
|        |  $0.220$ |       |  $0.208$ |
|        |  $0.428$ |       |  $0.030$ |
|        |  $0.744$ |       | $-0.046$ |
|        |  $0.690$ |       |  $0.030$ |

Jones–Dole $B$-coefficients at 25 °C (L/mol)



###### Implementation

For the eNRTL and Extended UNIQUAC property packages, which carry explicit ionic species on the material stream, the correction factor is computed directly from the phase composition. For the Sour Water and Kent–Eisenberg packages, which embed speciation in modified $K$-values, the ionic concentrations are first obtained from the speciation model (Section [6.8](#sec:sourwater)) or the charge-balance solver (Section [6.7](#sec:ke)), and then fed to Eq. [\[eq:jones_dole\]](#eq:jones_dole).

The correction is clamped to the range $[0.5,\;5.0]$ to guard against extrapolation beyond the correlation’s valid concentration range.

##### Thermal Conductivity — Riedel Equation {#sec:riedel}

The Riedel equation  provides an analogous ion-additive correction for the thermal conductivity of aqueous electrolyte solutions:


<a id="eq:riedel"></a>

\[
\frac{\lambda}{\lambda_{0}}
  = 1 - \sum_{i} \alpha_{i}\,c_{i}
\]


where $\lambda_{0}$ is the pure-water thermal conductivity and $\alpha_{i}$ (L/mol) is the ion-specific thermal conductivity decrement coefficient.

Most ions *decrease* thermal conductivity ($\alpha > 0$) by disrupting the hydrogen-bond network that makes water an unusually efficient thermal conductor. Notable exceptions are and ($\alpha < 0$), which *increase* $\lambda$ via the Grotthuss proton-hopping mechanism .

Representative $\alpha$ values compiled from Horvath  are listed in Table [25](#tab:riedel_alpha).



<a id="tab:riedel_alpha"></a>



| Cation | $\alpha$ | Anion | $\alpha$ |
|:-------|-----------:|:------|-----------:|
|        | $-0.030$ |       | $0.0053$ |
|        |  $0.023$ |       | $-0.016$ |
|        | $0.0044$ |       | $-0.002$ |
|        | $-0.010$ |       |  $0.019$ |
|        | $-0.007$ |       |  $0.035$ |
|        | $0.0045$ |       |  $0.010$ |
|        |  $0.003$ |       |  $0.005$ |
|        |  $0.013$ |       | $-0.003$ |
|        |  $0.009$ |       |  $0.008$ |
|        |  $0.012$ |       |  $0.011$ |

Riedel $\alpha$-coefficients at 25 °C (L/mol)



The implementation follows the same two-path strategy as the viscosity correction. The result is clamped to $[0.5,\;1.5]$ (a narrower range than for viscosity, reflecting the typically smaller magnitude of the thermal conductivity effect).

#### Aqueous-Phase pH and Ionic Strength {#sec:ph_ionic}

All electrolyte property packages compute and store the following aqueous-phase properties on the material stream after each flash calculation:

##### pH

For the eNRTL and Extended UNIQUAC packages, the pH is obtained from the equilibrium solver’s speciation result, which accounts for all dissociation and association reactions and activity-coefficient corrections:


\[
\mathrm{pH} = -\log_{10}[\ce{H+}]
\]


For the Sour Water package, the pH is computed by the Edwards-model `PHSolver`, which solves the coupled charge-balance for the –––– system (Section [6.8](#sec:sourwater)).

For the Kent–Eisenberg package, the pH is obtained from the Newton–Raphson charge-balance solver described in Section [6.7](#sec:ke).

When no equilibrium solve has been performed (e.g. during initialisation), a fallback approximate pH is estimated from the composition using the auxiliary electrolyte calculator.

##### Ionic Strength

The ionic strength of the aqueous phase is computed as


\[
I = \tfrac{1}{2}\sum_{i} c_{i}\,z_{i}^{2}
\]


For the eNRTL and Extended UNIQUAC packages, the sum is taken over all ionic species returned by the equilibrium solver. For the Sour Water and Kent–Eisenberg packages, the ionic concentrations are obtained from the speciation model or charge-balance solver, yielding a rigorous ionic strength consistent with the computed pH.

#### H$_2$O–HCl (Pitzer) {#sec:hcl_pitzer}

##### Overview {#overview-52}

The H$_2$O–HCl property package implements the binary Pitzer ion-interaction model with the high-temperature parameter fit of Ruaya & Seward , validated experimentally to 350 $^{\circ}$C and 3 mol/kg HCl. HCl is treated as a fully dissociated 1–1 strong electrolyte (); the model returns mean ionic and individual-ion activity coefficients, water activity, osmotic coefficient, solution pH, HCl partial pressure, and excess enthalpy / heat capacity.

The package additionally provides:

- **Crystalline-hydrate phase tracking** for HCl$\cdot$H$_2$O, HCl$\cdot$<!-- -->2H$_2$O and HCl$\cdot$<!-- -->3H$_2$O at sub-zero temperatures.

- **Poynting correction** for the HCl partial pressure at high system pressure.

- **Harvie–Møller–Weare 1984 mixing rules**  for HCl in spectator-electrolyte brines (HCl + NaCl, KCl, Na$_2$SO$_4$).

##### Pitzer Activity Coefficient

For a 1–1 electrolyte with ionic strength $I = m_{\mathrm{HCl}}$ on the molality scale:


\[
\ln\gamma_{\pm} = f^{\gamma}(I,T) + m\,B^{\gamma}(I,T) + m^{2}\,C^{\gamma}(T)
\]


with the Pitzer–Debye–Hückel kernel


\[
f^{\gamma}(I,T) = -A_{\varphi}(T)\!\left[\frac{\sqrt{I}}{1+b\sqrt{I}}
                    + \frac{2}{b}\ln\!\left(1+b\sqrt{I}\right)\right]
\]


where $b = 1.2$ kg$^{1/2}$/mol$^{1/2}$ is the universal Pitzer constant and $A_{\varphi}(T)$ is the Debye–Hückel slope (Pitzer 1991 Table B.1 fit reproducing $A_{\varphi}(298.15) = 0.39150$).

The second virial coefficient follows the canonical Pitzer  Eq. 32 form:


\[
B^{\gamma}(I,T) = 2\beta^{0}(T)
    + \frac{2\beta^{1}(T)}{\alpha_{1}^{2}I}
      \left[1 - \left(1 + \alpha_{1}\sqrt{I} - \tfrac{1}{2}\alpha_{1}^{2}I\right)e^{-\alpha_{1}\sqrt{I}}\right]
\]


with $\alpha_{1} = 2.0$. The third virial coefficient is $C^{\gamma}(T) = \tfrac{3}{2}C^{\varphi}(T)$.

###### Ruaya–Seward 1987 temperature dependence

The temperature-dependent Pitzer parameters $\beta^{0}(T)$ and $\beta^{1}(T)$ follow Ruaya & Seward Eq. 20 (parameters anchored at $T_{\mathrm{ref}} =
298.15$ K):


\[
P(T) = q_{1} + \frac{q_{2}}{T - 1/T_{\mathrm{ref}}}
       + q_{3}\ln\!\left(\frac{T}{T_{\mathrm{ref}}}\right)
       + q_{4}\,(T - T_{\mathrm{ref}})
       + q_{5}\,(T^{2} - T_{\mathrm{ref}}^{2})
\]


with $q$ coefficients from Ruaya & Seward Table 2 (reproduced in Table [26](#tab:hcl_pitzer_params)). $C^{\varphi}$ is intentionally set to zero in this fit; for systems requiring third-virial accuracy at $m > 3$ and $T < 100~^{\circ}$C, the Pitzer & Mayorga 1973  value $C^{\varphi}(298) = 8.0\times10^{-4}$ is the standard reference.



<a id="tab:hcl_pitzer_params"></a>



| Parameter | $q_{1}$ | $q_{2}$ | $q_{3}$ | $q_{4}$ | $q_{5}$ |
|:---|---:|---:|---:|---:|---:|
| $\beta^{0}$ | 0.17416 | $-773.62$ | $-4.5174$ | $8.1556\!\times\!10^{-3}$ | $-2.8525\!\times\!10^{-6}$ |
| $\beta^{1}$ | 0.28799 | $-374.50$ | $-4.1319$ | $1.0855\!\times\!10^{-2}$ | $-9.2990\!\times\!10^{-7}$ |

Ruaya & Seward (1987) HCl Pitzer parameter coefficients



##### HMW Mixing Rules for Spectator Brines

When the stream contains additional electrolytes (NaCl, KCl, Na$_2$SO$_4$, …), the binary $\gamma_{\pm}$ is corrected via the Harvie–Møller–Weare 1984 mixing terms :


\[
\begin{align}
  \Delta\ln\gamma_{\mathrm{H^{+}}} &= \sum_{M} m_{M}\!\left[2\,\theta_{\mathrm{H,M}}
                                     + \sum_{X} m_{X}\,\psi_{\mathrm{H,M,X}}\right]  \\
                                  &\quad + \sum_{X<X'} m_{X}m_{X'}\,\psi_{X,X',\mathrm{H}} \\
  \Delta\ln\gamma_{\mathrm{Cl^{-}}} &= \sum_{X} m_{X}\!\left[2\,\theta_{\mathrm{Cl,X}}
                                      + \sum_{M} m_{M}\,\psi_{M,\mathrm{Cl},X}\right]  \\
                                  &\quad + \sum_{M<M'} m_{M}m_{M'}\,\psi_{M,M',\mathrm{Cl}} \\
  \Delta\ln\gamma_{\pm}(\mathrm{HCl}) &= \tfrac{1}{2}\!\left(\Delta\ln\gamma_{\mathrm{H^{+}}} + \Delta\ln\gamma_{\mathrm{Cl^{-}}}\right)
\end{align}
\]


The DWSIM implementation ships with $\theta$ and $\psi$ values curated from primary literature; pairs not in the database default to $\theta = \psi = 0$ and the implementation degrades gracefully to the binary-Pitzer prediction with screening at the total ionic strength.



<a id="tab:hmw_db"></a>



| Type | Pair / Triplet | Value | Source |
|:---|:---|:---|---:|
| $\theta$ (cation–cation) | H$^{+}$ $|$ Na$^{+}$ | $+0.03416$ | Pierrot 1997  |
| $\theta$ (cation–cation) | H$^{+}$ $|$ K$^{+}$ | $+0.005$ | Pitzer 1991  |
| $\theta$ (anion–anion) | HSO$_4^{-}$ $|$ SO$_4^{2-}$ | $+0.07$ | Clegg 1994  |
| $\psi$ (c–c–a) | H$^{+}$ $|$ Na$^{+}$ $|$ Cl$^{-}$ | $+0.0002$ | Pierrot 1997 |
| $\psi$ (c–c–a) | H$^{+}$ $|$ K$^{+}$ $|$ Cl$^{-}$ | $-0.011$ | Pitzer 1991 |
| $\psi$ (c–a–a) | H$^{+}$ $|$ Cl$^{-}$ $|$ SO$_4^{2-}$ | $-0.006$ | Harvie 1984  |
| $\psi$ (c–a–a) | Na$^{+}$ $|$ Cl$^{-}$ $|$ SO$_4^{2-}$ | $-0.009$ | Møller 1988  |

HMW mixing parameters in the H$_2$O–HCl property package



##### Crystalline Hydrate Phase Equilibrium

Three HCl$\cdot n$H$_2$O hydrates are tracked when the corresponding toggles are enabled in the property-package editor (default: *off*, since most industrial streams operate above 0 $^{\circ}$C):

| Hydrate | Peritectic ($^{\circ}$C) | $m_{\mathrm{sat}}$ at peritectic (mol/kg) | Stable below |
|:---|---:|---:|---:|
| HCl$\cdot$H$_2$O | $-15.4$ | $\sim$<!-- -->18.0 | 258 K |
| HCl$\cdot$<!-- -->2H$_2$O | $-17.7$ | $\sim$<!-- -->19.2 | 255 K |
| HCl$\cdot$<!-- -->3H$_2$O | $-24.4$ | $\sim$<!-- -->19.5 | 249 K |

HCl$\cdot n$H$_2$O hydrates (Linke & Seidell 1965 )

The solubility product is calibrated to the experimental phase diagram at the peritectic and extrapolated by van’t Hoff with literature $\Delta H_{\mathrm{diss}}$ values:


\[
\ln K_{sp}(T) = \ln K_{sp}(T_{\mathrm{anchor}})
    - \frac{\Delta H_{\mathrm{diss}}}{R}\!\left(\frac{1}{T} - \frac{1}{T_{\mathrm{anchor}}}\right)
\]


with $K_{sp} = (\gamma_{\pm}m)^{2}\,a_{w}^{n}$. The flash uses the solid-fugacity ratio $f_{\mathrm{solid}} = K_{sp}(T)/[(\gamma_{\pm}m)^{2}a_{w}^{n}]$ to drive precipitation when the solution is supersaturated. Above the peritectic, $K_{sp} \to \infty$ and the hydrate is automatically excluded from the flash.

##### Poynting Correction

At system pressures above water saturation, the HCl partial pressure includes a Poynting term:


\[
p_{\mathrm{HCl}}(T,P) = K_{H}(T)\,m^{2}\,\gamma_{\pm}^{2}
    \cdot \exp\!\left[\frac{\bar{V}_{\mathrm{HCl}}(P-P_{\mathrm{sat}})}{RT}\right]
\]


with the partial molar volume $\bar{V}_{\mathrm{HCl}} = 17.8\times10^{-6}$ m$^{3}$/mol (Söhnel & Novotný 1985 ). The correction is negligible at 1 atm ($<0.01\,\%$), reaches $\sim$<!-- -->7 % at 100 bar and $\sim$<!-- -->2$\times$ at 1000 bar.

##### Validity Range and Validation {#validity-range-and-validation}

| Property | Recommended range |
|:---|:---|
| Temperature | 273–623 K (0–350 $^{\circ}$C) for binary HCl–H$_2$O |
|  | 273–373 K (0–100 $^{\circ}$C) when $m > 3$ mol/kg |
| Molality | 0–6 mol/kg (well validated) |
|  | 6–16 mol/kg (validated to $\pm 5$ % via extrapolation; warning emitted above 16) |
| Pressure | 1–1000 bar with Poynting correction |
| Spectators | NaCl, KCl, Na$_2$SO$_4$ (HMW $\theta/\psi$ available); other ions degrade gracefully |
| Hydrates | HCl$\cdot$H$_2$O, HCl$\cdot$<!-- -->2H$_2$O, HCl$\cdot$<!-- -->3H$_2$O at $T <$ respective peritectic |

H$_2$O–HCl (Pitzer) recommended operating envelope

###### Validation against Robinson & Stokes 1959 at 25 $^{\circ}$C. {#validation-against-robinson-stokes-1959-at-25-circc.}

| $m$ (mol/kg) | $\gamma_{\pm}$ (model) | $\gamma_{\pm}$ (R&S ) | Deviation |
|---:|---:|---:|---:|
| 0.1 | 0.794 | 0.796 | $-0.3\,\%$ |
| 0.5 | 0.755 | 0.757 | $-0.3\,\%$ |
| 1.0 | 0.804 | 0.809 | $-0.6\,\%$ |
| 3.0 | 1.280 | 1.316 | $-2.7\,\%$ |
| 6.0 | 3.040 | 3.22 | $-5.6\,\%$ |
| 10.0 | 10.65 | 10.4 | $+2.4\,\%$ |

Mean ionic activity coefficient of HCl, model vs experiment

###### Application domains

- **Refinery overhead corrosion:** HCl partial pressure and condensate pH in crude-distillation overheads, where dew-point chloride condensation drives equipment corrosion.

- **HCl gas absorbers / strippers:** VLE-driven design of HCl recovery columns from acid-gas streams.

- **Cryogenic HCl storage:** hydrate phase-equilibrium for process safety analysis at sub-zero temperatures.

- **Hydrochloric acid concentration:** azeotropic distillation and pressure-swing concentration of aqueous HCl.

- **Chlor-alkali brine acidification:** HCl + NaCl + Na$_2$SO$_4$ mixed-electrolyte chemistry via HMW mixing rules.

#### Electrolyte Compound Database (`electrolyte.xml`) {#sec:electrolyte_db}

The DWSIM electrolyte property packages share a single XML compound database located at `Assets/Databases/electrolyte.xml`, containing 214 entries classified by the four boolean flags `<Ion>`, `<Salt>`, `<HydratedSalt>` and the integer `<HydrationNumber>`. Each compound carries the standard thermodynamic data ($\Delta G_{f}^{\circ}$, $\Delta H_{f}^{\circ}$, $C_{p}^{\circ}$, melting point $T_{f}$, enthalpy of fusion $\Delta H_{\mathrm{fus}}$, solid density $\rho_{S}$) plus the stoichiometric decomposition into positive and negative ions.

##### Compound Categories

| Category | Count | Examples |
|:---|---:|:---|
| Cations | 38 | Na$^{+}$, K$^{+}$, NH$_4^{+}$, Ca$^{2+}$, Fe$^{3+}$, Al$^{3+}$, Cu$^{2+}$, Pb$^{2+}$ |
| Anions | 41 | Cl$^{-}$, SO$_4^{2-}$, HCO$_3^{-}$, PO$_4^{3-}$, F$^{-}$, CN$^{-}$, MnO$_4^{-}$ |
| Anhydrous salts | 87 | NaCl, K$_2$SO$_4$, FeCl$_3$, ZnSO$_4$, BaSO$_4$, CaCO$_3$, AgNO$_3$ |
| Hydrated salts | 45 | Na$_2$SO$_4{\cdot}10$H$_2$O, MgCl$_2{\cdot}6$H$_2$O, K$_2$CO$_3{\cdot}1.5$H$_2$O |
| HCl crystalline hydrates | 3 | HCl$\cdot$H$_2$O, HCl$\cdot$<!-- -->2H$_2$O, HCl$\cdot$<!-- -->3H$_2$O |

Categories in `electrolyte.xml`

###### Recent extensions (2026 release cycle)

The database has been expanded with $\sim$<!-- -->110 new ionic species to enable hydrometallurgy and broader corrosion/scaling analysis:

- **Cations:** Ca$^{2+}$, Ba$^{2+}$, Sr$^{2+}$, Fe$^{2+}$, Fe$^{3+}$, Cu$^{+}$, Cu$^{2+}$, Zn$^{2+}$, Mn$^{2+}$, Ni$^{2+}$, Co$^{2+}$, Cd$^{2+}$, Hg$^{2+}$, Pb$^{2+}$, Ag$^{+}$, Al$^{3+}$, Cr$^{3+}$, Rb$^{+}$.

- **Anions:** F$^{-}$, PO$_4^{3-}$, HPO$_4^{2-}$, H$_2$PO$_4^{-}$, CN$^{-}$, SCN$^{-}$, ClO$_3^{-}$, BrO$_3^{-}$, IO$_3^{-}$, NO$_2^{-}$, SO$_3^{2-}$, HSO$_3^{-}$, S$_2$O$_3^{2-}$, MnO$_4^{-}$, CrO$_4^{2-}$, Cr$_2$O$_7^{2-}$, CH$_3$COO$^{-}$ (acetate), HCOO$^{-}$ (formate), C$_2$O$_4^{2-}$ (oxalate).

- **Salts:** $\sim$<!-- -->75 new combinations (Ca/Ba/Sr/Fe/Cu/Zn/Mn/Al/Ni/Pb/Ag chlorides, sulfates, nitrates, perchlorates, fluorides, phosphates, cyanides, acetates, chromates) plus filler entries Na$_2$CO$_3$, NaHCO$_3$, K$_2$CO$_3$ and the full Rubidium series.

##### XML Schema

    <compound>
      <Name>Hydrogen Chloride Monohydrate</Name>
      <Formula>HCl.H2O</Formula>
      <MW>54.479</MW>
      <Ion>False</Ion>
      <Salt>True</Salt>
      <HydratedSalt>True</HydratedSalt>
      <PositiveIon>H+</PositiveIon>
      <NegativeIon>Cl-</NegativeIon>
      <HydrationNumber>1</HydrationNumber>
      <PositiveIonStoichCoeff>1</PositiveIonStoichCoeff>
      <NegativeIonStoichCoeff>1</NegativeIonStoichCoeff>
      <StoichSum>2</StoichSum>
      <Charge>0</Charge>
      <DelGF_kJ_mol>-403.30</DelGF_kJ_mol>
      <DelHf_kJ_mol>-481.60</DelHf_kJ_mol>
      <Cp_J_mol_K>97.0</Cp_J_mol_K>
      <Tf_C>-15.4</Tf_C>
      <Hfus_at_Tf_kJ_mol>14.6</Hfus_at_Tf_kJ_mol>
      <DenS_T_C>-25</DenS_T_C>
      <DenS_g_mL>1.49</DenS_g_mL>
    </compound>

#### Pitzer Single-Salt Parameter Database (`pitzer_parameters.json`) {#sec:pitzer_database}

A companion JSON file ships alongside `enrtl_parameters.json` with **107 single-electrolyte Pitzer parameter sets** from Kim & Frederick  Tables I–VI, valid at 25 $^{\circ}$C. This database serves two purposes:

1.  **Direct $\gamma_{\pm}$ calculation** for any aqueous single-salt composition via the standard Pitzer equations (Pitzer & Mayorga 1973 , Harvie & Weare 1980  form).

2.  **Source for batch-fitted eNRTL $\tau$ parameters** via the included `PitzerToENRTLBatch` CLI tool, which generates $\gamma_{\pm}(m)$ from Pitzer at multiple molality points and runs the existing `FittingCoreENRTL` Nelder–Mead minimizer to produce a Chen–Evans-style $(\tau_{w,ca}, \tau_{ca,w})$ pair (107 $\to$ 92 successful fits merged into the eNRTL database).

##### Coverage

| Charge type | Count | Notable salts |
|:---|---:|:---|
| 1–1 | 23 | NaSCN, KSCN, NaH$_2$PO$_4$, KCH$_3$COO, AgNO$_3$, RbCl, LiNO$_2$ |
| 2–1 | 53 | MgBr$_2$, CaBr$_2$, SrCl$_2$, BaCl$_2$, FeCl$_2$, MnCl$_2$, NiCl$_2$, CoCl$_2$, |
|  |  | CuCl$_2$, ZnCl$_2$, CdCl$_2$, PbCl$_2$, UO$_2$Cl$_2$, Mg(NO$_3$)$_2$, etc. |
| 1–2 | 12 | Na$_2$CO$_3$, Na$_2$SO$_3$, K$_2$CrO$_4$, Cs$_2$SO$_4$, (NH$_4$)$_2$HPO$_4$ |
| 2–2 | 11 | ZnSO$_4$, CdSO$_4$, NiSO$_4$, MnSO$_4$, CuSO$_4$, MgSO$_4$, BeSO$_4$ |
| 3–1 | 8 | AlCl$_3$, ScCl$_3$, CrCl$_3$, FeCl$_3$, Na$_3$PO$_4$, K$_3$PO$_4$, Cr(NO$_3$)$_3$ |

Salt families in `pitzer_parameters.json`

##### JSON Schema

    {
      "salts_2_1": [
        {
          "id": "ZnCl2",
          "cation": "Zn2+",
          "anion": "Cl-",
          "beta0": 0.08887,
          "beta1": 2.94869,
          "Cphi": 0.00095,
          "m_max": 10.0,
          "table": "III"
        }, ...
      ]
    }

For 2–2 electrolytes a fourth coefficient $\beta^{2}$ is included with $\alpha_{1} = 1.4$ and $\alpha_{2} = 12.0$ per Pitzer & Mayorga 1974.

#### Carbon Capture (eNRTL) {#sec:ccus_capture}

##### Overview {#overview-53}

The Carbon Capture property package provides rigorous thermodynamic modelling of aqueous amine–CO$_2$ systems used in post-combustion carbon capture. It extends the eNRTL framework (Section [6.5](#sec:enrtl)) with amine-specific chemical equilibria, species registries, and CO$_2$ mass-transfer correlations.

Five amine types are supported: monoethanolamine (MEA), diethanolamine (DEA), methyldiethanolamine (MDEA), piperazine (PZ), and 2-amino-2-methyl-1-propanol (AMP). Mixed-amine blends are handled automatically when multiple amines are present in the material stream.

##### Chemical Equilibria

The aqueous speciation is determined by solving the following equilibrium reactions simultaneously via Newton–Raphson iteration with eNRTL activity coefficients:


<a id="rxn:cap_kw"></a><a id="rxn:cap_k1"></a><a id="rxn:cap_k2"></a><a id="rxn:cap_prot"></a><a id="rxn:cap_carb"></a>

\[
\begin{alignat}
{3}
  \ce{H2O}              &\;\ce{<=>}\; \ce{H+ + OH-}
    &&\qquad K_{w}(T)  \\
  \ce{CO2(aq) + H2O}    &\;\ce{<=>}\; \ce{H+ + HCO3-}
    &&\qquad K_{1}(T)  \\
  \ce{HCO3-}            &\;\ce{<=>}\; \ce{H+ + CO3^{2-}}
    &&\qquad K_{2}(T)  \\
  \ce{RNH3+}            &\;\ce{<=>}\; \ce{RNH2 + H+}
    &&\qquad K_{p}(T)  \\
  \ce{RNH2 + CO2}       &\;\ce{<=>}\; \ce{RNHCOO- + H+}
    &&\qquad K_{c}(T)
\end{alignat}
\]


Reaction [\[rxn:cap_carb\]](#rxn:cap_carb) (carbamate formation) applies to primary and secondary amines (MEA, DEA, PZ, AMP); MDEA does not form a stable carbamate . For piperazine, additional equilibria account for the diprotic nature of PZ and its dicarbamate .

##### Equilibrium Constants

All equilibrium constants follow the van ’t Hoff correlation


<a id="eq:cap_vanthoff"></a>

\[
K(T) = K_{25}\exp\!\left[
    -\frac{\Delta H_{\mathrm{rxn}}}{R}
    \left(\frac{1}{T} - \frac{1}{298.15}\right)
  \right]
\]


where $K_{25}$ and $\Delta H_{\mathrm{rxn}}$ are taken from Edwards et al.  for the universal reactions and from Austgen et al.  (MEA, DEA), Frailie  (PZ), and Li et al.  (AMP) for the amine-specific reactions.

Amine protonation constants are expressed via the three-parameter correlation


<a id="eq:cap_pka"></a>

\[
\ln K_{p}(T) = \frac{A}{T} + B\,\ln T + C
\]


Table [28](#tab:cap_pka) lists the reference p$K_{a}$ values at 25 $^{\circ}$C.



<a id="tab:cap_pka"></a>



| Amine           | p$K_a$ (25 $^{\circ}$C) | Source             |
|:----------------|----------------------------:|:-------------------|
| MEA             |                        9.50 | Austgen 1989       |
| DEA             |                        8.88 | Austgen 1989       |
| MDEA            |                        8.56 | Zhang & Chen 2011  |
| PZ ($K_{p1}$) |                        9.73 | Frailie 2014       |
| PZ ($K_{p2}$) |                        5.33 | Frailie 2014       |
| AMP             |                        9.35 | Li et al. 2014     |

Amine protonation p$K_a$ at 25 $^{\circ}$C



##### CO$_2$ Diffusivity — N$_2$O Analogy {#co_2-diffusivity-n_2o-analogy}

The diffusivity of CO$_2$ in aqueous amine solutions is estimated via the N$_2$O analogy :


<a id="eq:n2o_analogy"></a>

\[
D_{\ce{CO2}}^{\mathrm{amine}} =
    D_{\ce{N2O}}^{\mathrm{amine}}
    \cdot \frac{D_{\ce{CO2}}^{\mathrm{water}}}
               {D_{\ce{N2O}}^{\mathrm{water}}}
\]


with Arrhenius-form correlations for the water-phase diffusivities:


\[
\begin{align}
  D_{\ce{CO2}}^{\mathrm{water}} &= 2.35\times10^{-6}\exp(-2119/T) \quad \mathrm{m^{2}/s} \\
  D_{\ce{N2O}}^{\mathrm{water}} &= 5.07\times10^{-6}\exp(-2371/T) \quad \mathrm{m^{2}/s}
\end{align}
\]


##### Validity Range

| Property         | Recommended range                      |
|:-----------------|:---------------------------------------|
| Temperature      | 298–393 K (25–120 $^{\circ}$C)       |
| Pressure         | 1–5 bar (absorber/stripper conditions) |
| Amine conc.      | 10–50 wt% in water                     |
| CO$_2$ loading | 0–0.5 mol/mol amine (MEA), 0–1.0 (PZ)  |
| Amines           | MEA, DEA, MDEA, PZ, AMP, and blends    |

Carbon Capture PP recommended operating envelope

###### Application domains

- **Post-combustion CO$_2$ capture:** absorber and stripper column design with MEA, PZ, or blended amine solvents.

- **Solvent screening:** comparison of amine types (primary, secondary, tertiary, sterically hindered) for CO$_2$ absorption capacity and regeneration energy.

- **Rich/lean solvent property estimation:** density, viscosity, heat capacity and speciation of loaded amine solutions.

#### CO$_2$ Transport (Span-Wagner / PR) {#sec:ccus_transport}

##### Overview {#overview-54}

The CO$_2$ Transport property package provides high-accuracy thermodynamic and transport properties for CO$_2$-rich streams in pipeline and shipping applications. Two equation-of-state regimes are used depending on stream purity:

- **Pure CO$_2$** ($x_{\ce{CO2}} \geq 0.9999$): Span–Wagner multiparameter equation of state , a 42-term Helmholtz free energy formulation that is the IUPAC/NIST reference standard for CO$_2$.

- **CO$_2$ with impurities** ($x_{\ce{CO2}} < 0.9999$): Peng–Robinson equation of state  with transport-specific binary interaction parameters ($k_{ij}$).

##### Span–Wagner Equation of State

The dimensionless Helmholtz free energy is expressed as


<a id="eq:sw_helmholtz"></a>

\[
\frac{a(\rho,T)}{RT} = \alpha(\delta,\tau) = \alpha^{0}(\delta,\tau) + \alpha^{r}(\delta,\tau)
\]


where $\delta = \rho/\rho_{c}$ and $\tau = T_{c}/T$ are the reduced density and inverse reduced temperature, with critical parameters $T_{c} = 304.1282$ K, $P_{c} = 73.773$ bar, $\rho_{c} = 467.6$ kg/m$^{3}$.

The residual part $\alpha^{r}$ contains 42 terms : 7 polynomial, 27 exponential, and 8 Gaussian bell-shaped terms:


<a id="eq:sw_residual"></a>

\[
\begin{multline}
  \alpha^{r}(\delta,\tau) =
    \sum_{i=1}^{7} n_{i}\,\delta^{d_{i}}\,\tau^{t_{i}}
    + \sum_{i=8}^{34} n_{i}\,\delta^{d_{i}}\,\tau^{t_{i}}\,
      e^{-\delta^{c_{i}}} \\
    + \sum_{i=35}^{42} n_{i}\,\delta^{d_{i}}\,\tau^{t_{i}}\,
      e^{-\alpha_{i}(\delta-\varepsilon_{i})^{2} - \beta_{i}(\tau-\gamma_{i})^{2}}
\end{multline}
\]


All thermodynamic properties (pressure, enthalpy, entropy, heat capacity, speed of sound, fugacity coefficient) are obtained as analytical derivatives of $\alpha(\delta,\tau)$. Density is determined by iterative solution of $P = \rho RT(1 + \delta\,\partial\alpha^{r}/\partial\delta)$.

##### Transport Properties

###### Viscosity

The Fenghour–Vesovic–Wakeham correlation  decomposes viscosity into dilute-gas, initial-density and residual contributions:


<a id="eq:co2_visc"></a>

\[
\eta(\rho,T) = \eta_{0}(T) + \Delta\eta_{\mathrm{excess}}(\rho,T)
\]


Valid for 200–1500 K, 0–300 MPa, with uncertainty $< 5$% over most of the range.

###### Thermal conductivity

The Vesovic et al. correlation  follows an analogous decomposition:


<a id="eq:co2_cond"></a>

\[
\lambda(\rho,T) = \lambda_{0}(T) + \Delta\lambda_{\mathrm{excess}}(\rho,T) + \Delta\lambda_{\mathrm{crit}}(\rho,T)
\]


where the critical enhancement $\Delta\lambda_{\mathrm{crit}}$ is significant near $T_{c}$.

##### Mixture Mode — Peng–Robinson with Transport $k_{ij}$ {#mixture-mode-pengrobinson-with-transport-k_ij}

For impure CO$_2$ streams, the Peng–Robinson EOS  is used with binary interaction parameters fitted to pipeline-relevant mixtures. Table [29](#tab:transport_kij) lists the shipped $k_{ij}$ values.



<a id="tab:transport_kij"></a>



| Pair | $k_{ij}$ |
|:-----|-----------:|
| –    | $-0.012$ |
| –    | $+0.114$ |
| –    | $+0.130$ |
| –    | $+0.097$ |
| –    | $+0.120$ |
| –    | $+0.046$ |
| –    | $+0.092$ |
| –    | $+0.090$ |

CO$_2$ Transport binary interaction parameters



##### Validity Range and Validation {#validity-range-and-validation-1}

| Property | Recommended range |
|:---|:---|
| Temperature | 216–1100 K (Span–Wagner valid range) |
| Pressure | 0–800 MPa (Span–Wagner) |
| CO$_2$ purity | $> 90$ mol% for pipeline applications |
| Impurities | N$_2$, O$_2$, Ar, H$_2$S, H$_2$O, SO$_2$, CH$_4$, H$_2$ |

CO$_2$ Transport PP recommended operating envelope

###### Validation against NIST

Pure CO$_2$ density from Span–Wagner matches the NIST WebBook reference data to $< 0.01$% across the entire fluid range. Specific test points: 228.8 kg/m$^{3}$ at 350 K / 100 bar (supercritical, deviation $+0.003$%); 938.2 kg/m$^{3}$ at 280 K / 100 bar (liquid, deviation $+0.001$%).

###### Application domains

- **CO$_2$ pipeline design:** phase envelope calculation, dense-phase transport properties, two-phase detection for safety analysis.

- **CO$_2$ compression trains:** isentropic and polytropic compressor calculations with real-gas departure functions.

- **Ship transport:** liquefied CO$_2$ properties at low-temperature, medium-pressure conditions.

- **Impurity impact assessment:** effect of N$_2$, O$_2$, H$_2$S on phase behaviour and transport properties.

#### CO$_2$ Storage (eNRTL / Duan–Sun) {#sec:ccus_storage}

##### Overview {#overview-55}

The CO$_2$ Storage property package models CO$_2$ behaviour in geological storage contexts: saline aquifer injection, CO$_2$-enhanced oil recovery, and mineral trapping. It combines the eNRTL activity coefficient model (Section [6.5](#sec:enrtl)) for aqueous speciation with the Duan–Sun model  for CO$_2$ solubility in brine and Span–Wagner  for the CO$_2$ fugacity at reservoir pressures.

##### Duan–Sun CO$_2$ Solubility Model

The solubility of CO$_2$ in aqueous NaCl solutions is computed from the Duan–Sun equation :


<a id="eq:duansun"></a>

\[
\ln m_{\ce{CO2}} = \frac{\mu_{\ce{CO2}}^{l}(T,P)}{RT}
    - \ln\varphi_{\ce{CO2}}(T,P)
    + \ln P
    - 2\lambda_{\ce{CO2\text{-}Na}}(T,P)\,m_{\mathrm{Na}}
    - \zeta_{\ce{CO2\text{-}Na\text{-}Cl}}(T,P)\,m_{\mathrm{Na}}\,m_{\mathrm{Cl}}
\]


where $m_{\ce{CO2}}$ is the CO$_2$ molality (mol/kg H$_2$O), $\mu^{l}$ the chemical potential of CO$_2$ in the liquid phase, $\varphi_{\ce{CO2}}$ the fugacity coefficient (from Span–Wagner), $\lambda$ the CO$_2$–Na$^{+}$ interaction parameter, and $\zeta$ the CO$_2$–Na$^{+}$–Cl$^{-}$ ternary interaction parameter. Temperature-pressure dependence of $\mu^{l}/RT$, $\lambda$, and $\zeta$ is expressed via 11-coefficient polynomials in $T$ and $P$ fitted to experimental solubility data.

###### Validity range

273–533 K, 0–2000 bar, 0–4.5 mol/kg NaCl .

##### Aqueous Speciation

Once the total dissolved CO$_2$ is set by the Duan–Sun model, the acid–base speciation in the aqueous phase is solved via the eNRTL equilibrium solver:


\[
\begin{alignat}
{3}
  \ce{H2O}           &\;\ce{<=>}\; \ce{H+ + OH-}
    &&\qquad K_{w}(T)  \\
  \ce{CO2(aq) + H2O} &\;\ce{<=>}\; \ce{H+ + HCO3-}
    &&\qquad K_{1}(T)  \\
  \ce{HCO3-}         &\;\ce{<=>}\; \ce{H+ + CO3^{2-}}
    &&\qquad K_{2}(T)
\end{alignat}
\]


The VLE (gas–liquid partitioning of CO$_2$) is handled externally by the Duan–Sun model and the flash algorithm, not by the speciation solver. This separation ensures robust convergence of the Newton–Raphson iteration for the aqueous equilibria.

##### Mineral Trapping

Long-term CO$_2$ storage involves mineralisation reactions where dissolved CO$_2$ reacts with formation minerals. The property package includes saturation index calculations for four key carbonate minerals:


\[
\begin{alignat}
{2}
  \ce{CaCO3}                &\;\ce{<=>}\; \ce{Ca^{2+} + CO3^{2-}}
    &&\qquad \text{calcite}    \\
  \ce{MgCO3}                &\;\ce{<=>}\; \ce{Mg^{2+} + CO3^{2-}}
    &&\qquad \text{magnesite}  \\
  \ce{FeCO3}                &\;\ce{<=>}\; \ce{Fe^{2+} + CO3^{2-}}
    &&\qquad \text{siderite}   \\
  \ce{CaMg(CO3)2}           &\;\ce{<=>}\; \ce{Ca^{2+} + Mg^{2+} + 2\,CO3^{2-}}
    &&\qquad \text{dolomite}
\end{alignat}
\]


The saturation index $\mathrm{SI} = \log_{10}(Q/K_{sp})$ indicates supersaturation ($\mathrm{SI} > 0$, precipitation) or undersaturation ($\mathrm{SI} < 0$, dissolution).

##### High-Pressure Fugacity

At reservoir pressures (100–600 bar), the CO$_2$ fugacity cannot be approximated by Henry’s law (valid only below $\sim$<!-- -->50 bar). The Storage PP uses the Span–Wagner EOS (Section [6.17](#sec:ccus_transport)) to compute the fugacity coefficient $\varphi_{\ce{CO2}}(T,P)$ rigorously.

##### Validity Range

| Property         | Recommended range                            |
|:-----------------|:---------------------------------------------|
| Temperature      | 273–533 K (0–260 $^{\circ}$C) per Duan–Sun |
| Pressure         | 1–2000 bar                                   |
| Salinity         | 0–4.5 mol/kg NaCl                            |
| CO$_2$ content | trace to saturation                          |
| Minerals         | calcite, magnesite, siderite, dolomite       |

CO$_2$ Storage PP recommended operating envelope

###### Application domains

- **Saline aquifer injection:** CO$_2$ solubility and speciation at reservoir $T$, $P$, and salinity.

- **CO$_2$-EOR:** minimum miscibility pressure estimation and CO$_2$–brine phase behaviour.

- **Long-term storage security:** mineral trapping capacity and carbonate precipitation kinetics assessment.

- **Well integrity:** pH and carbonate chemistry in cement–brine interactions near the wellbore.

#### Patel–Teja Equation of State {#sec:pt}

##### Overview {#overview-56}

The Patel–Teja (PT) equation of state  is a three-parameter cubic EOS that generalises the Peng–Robinson and Soave–Redlich–Kwong forms by introducing an additional volume-translation parameter $c$:


<a id="eq:pt_eos"></a>

\[
P = \frac{RT}{v - b}
    - \frac{a(T)}{v(v+b) + c(v-b)}
\]


This extra degree of freedom substantially improves liquid-density predictions for polar and non-polar fluids without sacrificing vapour–liquid equilibrium accuracy .

##### Temperature Dependence of the Attractive Parameter

The temperature-dependent attractive parameter is


<a id="eq:pt_alpha"></a>

\[
a(T) = a(T_{\mathrm{c}})\,\alpha(T),
  \qquad
  \alpha(T) = \left[1 + F\!\left(1 - \sqrt{\frac{T}{T_{\mathrm{c}}}}\right)\right]^{2}
\]


where the substance-specific parameter $F$ is correlated with the acentric factor $\omega$:


<a id="eq:pt_F"></a>

\[
F = 0.452413 + 1.30982\,\omega - 0.295937\,\omega^{2}
\]


##### Critical Constraints

The three constants $a(T_{\mathrm{c}})$, $b$, and $c$ are obtained from the conditions $(\partial P/\partial v)_{T_{\mathrm{c}}} = 0$ and $(\partial^{2} P/\partial v^{2})_{T_{\mathrm{c}}} = 0$, yielding


<a id="eq:pt_abc"></a>

\[
a(T_{\mathrm{c}}) = \Omega_{a}\,\frac{R^{2}T_{\mathrm{c}}^{2}}{P_{\mathrm{c}}},
  \quad
  b = \Omega_{b}\,\frac{RT_{\mathrm{c}}}{P_{\mathrm{c}}},
  \quad
  c = \Omega_{c}\,\frac{RT_{\mathrm{c}}}{P_{\mathrm{c}}}
\]


The dimensionless parameters $\Omega_{b}$ and $\Omega_{a}$ satisfy:


<a id="eq:pt_omega"></a>

\[
\Omega_{b}^{3} - (2 - 3\zeta_{\mathrm{c}})\Omega_{b}^{2}
  + 3\zeta_{\mathrm{c}}^{2}\Omega_{b} - \zeta_{\mathrm{c}}^{3} = 0,
  \quad
  \Omega_{c} = 1 - 3\zeta_{\mathrm{c}},
  \quad
  \Omega_{a} = 3\zeta_{\mathrm{c}}^{2} + 3(1-2\zeta_{\mathrm{c}})\Omega_{b}
             + \Omega_{b}^{2} + \Omega_{c}
\]


where $\zeta_{\mathrm{c}} = P_{\mathrm{c}} v_{\mathrm{c}} / (RT_{\mathrm{c}})$ is the critical compressibility factor. If $\zeta_{\mathrm{c}}$ is not known it is estimated from the Patel–Teja generalised correlation:


\[
\zeta_{\mathrm{c}} = 0.329032 - 0.076799\,\omega + 0.0211947\,\omega^{2}
\]


##### Mixing Rules

For mixtures the van der Waals one-fluid mixing rules are applied:


\[
a = \sum_{i}\sum_{j} x_{i}\,x_{j}\,a_{ij},
  \quad a_{ij} = \sqrt{a_{i}\,a_{j}}\,(1 - k_{ij})
\]




\[
b = \sum_{i} x_{i}\,b_{i},
  \quad
  c = \sum_{i} x_{i}\,c_{i}
\]


where $k_{ij}$ is the binary interaction parameter.

##### Parameters

| Symbol | Description | Source |
|:---|:---|:---|
| $T_{\mathrm{c}},\,P_{\mathrm{c}}$ | Critical temperature and pressure | Database |
| $\omega$ | Acentric factor | Database |
| $\zeta_{\mathrm{c}}$ | Critical compressibility factor | Database or Eq. (4) |
| $k_{ij}$ | Binary interaction parameter | Fitted or 0 |

Patel–Teja EOS parameters

#### Schmidt–Wenzel Equation of State {#sec:sw}

##### Overview {#overview-57}

The Schmidt–Wenzel (SW) EOS  is a three-parameter cubic equation that incorporates the acentric factor $\omega$ directly into the repulsive/attractive volume term, giving a single, acentric-factor-dependent EOS form:


<a id="eq:sw_eos"></a>

\[
P = \frac{RT}{v - b}
    - \frac{a(T)}{v^{2} + (1 + 3\omega)\,b\,v - 3\omega\,b^{2}}
\]


When $\omega = 1/3$ the denominator reduces to $v(v+2b)$, recovering the Peng–Robinson form; for $\omega = 0$ it recovers the van der Waals denominator .

##### Temperature Dependence and Critical Parameters

The $\alpha$ function takes the Soave form:


\[
a(T) = a_{c}\,\alpha(T),
  \quad
  \alpha(T) = \left[1 + m\!\left(1 - \sqrt{\frac{T}{T_{\mathrm{c}}}}\right)\right]^{2}
\]


with


\[
m = 0.465 + 1.347\,\omega - 0.528\,\omega^{2}
\]


The critical constants are:


\[
a_{c} = \Omega_{a}\,\frac{R^{2}T_{\mathrm{c}}^{2}}{P_{\mathrm{c}}},
  \quad
  b = \Omega_{b}\,\frac{RT_{\mathrm{c}}}{P_{\mathrm{c}}}
\]


where $\Omega_{a}$ and $\Omega_{b}$ are roots of the criticality conditions that depend on $\omega$ .

##### Mixing Rules

The same van der Waals one-fluid mixing rules as in Eq. [\[eq:pt_abc\]](#eq:pt_abc)–[\[eq:pt_omega\]](#eq:pt_omega) are used, with $\omega$ evaluated at the mixture-average acentric factor $\bar\omega = \sum_{i}x_{i}\omega_{i}$.

#### Cubic-Plus-Association (CPA) Equation of State {#sec:cpa}

##### Overview {#overview-58}

The Cubic-Plus-Association (CPA) EOS, proposed by Kontogeorgis et al. , combines a standard cubic EOS with the associating term from Wertheim’s first-order perturbation theory . Two variants are available depending on the underlying cubic:

- **SRK-CPA**: Soave–Redlich–Kwong cubic 

- **PR-CPA**: Peng–Robinson cubic 

The pressure expression is


<a id="eq:cpa_pressure"></a>

\[
P = P_{\mathrm{cubic}}(T,v) + P_{\mathrm{assoc}}(T,v,\mathbf{x})
\]


where $P_{\mathrm{cubic}}$ is the SRK or PR equation and $P_{\mathrm{assoc}}$ is the association contribution.

##### Cubic Contributions

For SRK-CPA:


\[
P_{\mathrm{cubic}}^{\mathrm{SRK}} = \frac{RT}{v-b} - \frac{a(T)}{v(v+b)}
\]


For PR-CPA:


\[
P_{\mathrm{cubic}}^{\mathrm{PR}} = \frac{RT}{v-b} - \frac{a(T)}{v(v+b)+b(v-b)}
\]


The temperature-dependent attractive parameter uses the Soave $\alpha$ function with substance-specific parameters $a_{0}$ and $b_{1}$ (replacing the standard critical-point-derived values for associating compounds) .

##### Association Contribution

The residual Helmholtz energy from association is :


<a id="eq:cpa_assoc"></a>

\[
\frac{A^{\mathrm{assoc}}}{NkT}
  = \sum_{i} x_{i} \sum_{A_{i}}
    \left[\ln X^{A_{i}} - \frac{X^{A_{i}}}{2} + \frac{1}{2}\right]
\]


where $X^{A_{i}}$ is the monomer fraction at association site $A$ of species $i$ (fraction of molecules $i$ *not* bonded at site $A$), obtained by solving the mass-action equation:


<a id="eq:cpa_XA"></a>

\[
X^{A_{i}} = \frac{1}{1 + \rho\displaystyle\sum_{j}x_{j}
                        \sum_{B_{j}} X^{B_{j}}\,\Delta^{A_{i}B_{j}}}
\]


##### Association Strength

The association strength between sites $A_{i}$ and $B_{j}$ is


<a id="eq:cpa_delta"></a>

\[
\Delta^{A_{i}B_{j}}
  = g^{\mathrm{hs}}(\bar{\sigma})\,b_{ij}\,\beta^{A_{i}B_{j}}
    \left[\exp\!\left(\frac{\varepsilon^{A_{i}B_{j}}}{kT}\right) - 1\right]
\]


where $g^{\mathrm{hs}}$ is the radial distribution function at contact for hard spheres (simplified Carnahan–Starling expression ), $\varepsilon^{AB}$ is the association energy, and $\beta^{AB}$ is the association volume parameter.

##### Association Schemes

Common association schemes and their site types are listed in Table [30](#tab:cpa_schemes).



<a id="tab:cpa_schemes"></a>



| Scheme | Positive sites | Negative sites |
|:-------|:---------------|:---------------|
| 2B     | 1 (H-donor)    | 1 (H-acceptor) |
| 3B     | 1              | 2              |
| 4C     | 2              | 2              |
| 1A     | 1              | 0 (inert)      |

Standard association schemes used in CPA



##### Pure-Component Parameters

Each associating compound requires five CPA parameters: $a_{0}$ (J$\cdot$m$^{3}$/mol$^{2}$), $b$ (m$^{3}$/mol), $b_{1}$ (temperature coefficient of $\alpha$), $\varepsilon^{AB}/k$ (K), and $\beta^{AB}$ (dimensionless), fitted to saturation pressure and liquid density data.

#### Perturbed-Chain Statistical Associating Fluid Theory (PC-SAFT) {#sec:pcsaft}

##### Overview {#overview-59}

The Perturbed-Chain SAFT (PC-SAFT) EOS of Gross & Sadowski  models molecules as chains of hard-sphere segments with dispersive (van der Waals) and associative interactions. The total residual Helmholtz energy per mole is


<a id="eq:pcsaft_ares"></a>

\[
\tilde{a}^{\mathrm{res}} =
    \tilde{a}^{\mathrm{hc}} + \tilde{a}^{\mathrm{disp}} + \tilde{a}^{\mathrm{assoc}}
\]


The variant implemented in ThermoPack is PCP-SAFT , which adds a polar contribution for dipolar/quadrupolar molecules: $\tilde{a}^{\mathrm{res}} = \tilde{a}^{\mathrm{hc}} + \tilde{a}^{\mathrm{disp}}
+ \tilde{a}^{\mathrm{assoc}} + \tilde{a}^{\mathrm{polar}}$.

##### Hard-Chain Term

The hard-chain contribution is :


\[
\tilde{a}^{\mathrm{hc}} =
    \bar{m}\,\tilde{a}^{\mathrm{hs}} - \sum_{i} x_{i}(m_{i}-1)\ln g_{ii}^{\mathrm{hs}}
\]


where $\bar{m} = \sum_{i} x_{i}\,m_{i}$ is the mean segment number, $\tilde{a}^{\mathrm{hs}}$ is the Carnahan–Starling hard-sphere Helmholtz energy, and $g_{ii}^{\mathrm{hs}}$ is the hard-sphere radial distribution function at contact. The packing fraction is defined in terms of the temperature-dependent segment diameter $d_{i}(T)$:


\[
\eta = \frac{\pi}{6}\,\rho\sum_{i}x_{i}\,m_{i}\,d_{i}^{3}(T),
  \quad
  d_{i}(T) = \sigma_{i}\!\left[1 - 0.12\exp\!\left(-\frac{3\varepsilon_{i}}{kT}\right)\right]
\]


##### Dispersion Term

The Barker–Henderson second-order perturbation expansion gives :


\[
\tilde{a}^{\mathrm{disp}} =
    -2\pi\rho\,I_{1}(\eta,\bar{m})\,\overline{m^{2}\varepsilon\sigma^{3}}
    - \pi\rho\,\bar{m}\,C_{1}\,I_{2}(\eta,\bar{m})\,\overline{m^{2}\varepsilon^{2}\sigma^{3}}
\]


where $I_{1}$ and $I_{2}$ are power series in $\eta$ with $\bar{m}$-dependent coefficients, $C_{1}$ is a compressibility factor, and the mixture integrals are:


\[
\overline{m^{2}\varepsilon^{n}\sigma^{3}} =
    \sum_{i}\sum_{j} x_{i}\,x_{j}\,m_{i}\,m_{j}
      \!\left(\frac{\varepsilon_{ij}}{kT}\right)^{\!n} \sigma_{ij}^{3}
\]


with the Lorentz–Berthelot combining rules $\sigma_{ij} = (\sigma_{i}+\sigma_{j})/2$ and $\varepsilon_{ij} = \sqrt{\varepsilon_{i}\varepsilon_{j}}(1-k_{ij})$.

##### Association Term

The association term follows Eq. [\[eq:cpa_assoc\]](#eq:cpa_assoc)–[\[eq:cpa_delta\]](#eq:cpa_delta) with the hard-chain radial distribution function $g^{\mathrm{hc}}$ replacing $g^{\mathrm{hs}}$, and using SAFT-type combining rules for cross-associating pairs .

##### Pure-Component Parameters

Each non-associating molecule requires three pure-component parameters: $m$ (segment number), $\sigma$ (segment diameter, Å), and $\varepsilon/k$ (dispersion energy, K). Associating molecules additionally require $\varepsilon^{AB}/k$ (K) and $\kappa^{AB}$ (association volume, dimensionless).

#### Simplified Perturbed-Chain SAFT (SPC-SAFT) {#sec:spcsaft}

##### Overview {#overview-60}

SPC-SAFT  retains the PC-SAFT chain and association terms but replaces the full second-order perturbation dispersion with a simplified first-order expression based on a mean-field approximation. The residual Helmholtz energy is


\[
\tilde{a}^{\mathrm{res}} =
    \tilde{a}^{\mathrm{hc}} + \tilde{a}^{\mathrm{disp,simplified}} + \tilde{a}^{\mathrm{assoc}}
\]


##### Simplified Dispersion Term

The simplified dispersion term uses the mean-field integral $J(\eta,\bar{m})$ with a reduced parameter set :


\[
\tilde{a}^{\mathrm{disp,simplified}}
  = -2\pi\rho\,\bar{m}^{2}\,\varepsilon_{m}\sigma_{m}^{3}\,J(\eta)
\]


This formulation reduces computational cost while preserving accuracy for industrial-grade VLE calculations with smaller parameter sets compared to full PC-SAFT. The pure-component parameters ($m$, $\sigma$, $\varepsilon/k$ and optionally association parameters) are directly transferable from PC-SAFT .

#### SAFT-VR Mie Equation of State {#sec:saftvrmie}

##### Overview {#overview-61}

The SAFT-VR Mie EOS of Lafitte et al.  uses the generalised Mie pair potential instead of the hard-sphere/square-well potentials of earlier SAFT variants. This provides an additional degree of freedom in modelling the “softness” of the repulsive core and the range of the attractive well.

##### Mie Potential

The segment–segment interaction potential is


<a id="eq:mie_potential"></a>

\[
u(r) = C\,\varepsilon
  \left[
    \left(\frac{\sigma}{r}\right)^{\!\lambda_{r}}
    - \left(\frac{\sigma}{r}\right)^{\!\lambda_{a}}
  \right],
  \quad
  C = \frac{\lambda_{r}}{\lambda_{r}-\lambda_{a}}
      \left(\frac{\lambda_{r}}{\lambda_{a}}\right)^{\!\lambda_{a}/(\lambda_{r}-\lambda_{a})}
\]


where $\lambda_{r}$ and $\lambda_{a}$ are the repulsive and attractive exponents, respectively. The Lennard-Jones 12-6 potential is recovered for $\lambda_{r}=12$, $\lambda_{a}=6$.

##### Residual Helmholtz Energy

The total residual Helmholtz energy per mole is


<a id="eq:saftvrmie_ares"></a>

\[
\tilde{a}^{\mathrm{res}}
  = \tilde{a}^{\mathrm{mono}} + \tilde{a}^{\mathrm{chain}} + \tilde{a}^{\mathrm{assoc}}
\]


The monomer term is evaluated using a third-order Barker–Henderson perturbation expansion applied to Mie reference fluids :


\[
\tilde{a}^{\mathrm{mono}} = \tilde{a}^{\mathrm{hs}} + \tilde{a}_{1} + \tilde{a}_{2} + \tilde{a}_{3}
\]


The first-order perturbation term:


\[
\tilde{a}_{1} = 2\pi\rho\sum_{i}\sum_{j}x_{i}x_{j}m_{i}m_{j}
    \int_{0}^{\infty} u_{ij}(r)\,g_{ij}^{\mathrm{hs}}(r)\,r^{2}\,\mathrm{d}r
\]


Higher-order terms $\tilde{a}_{2}$ and $\tilde{a}_{3}$ capture local-density fluctuations and are evaluated analytically using the mean-value theorem and the local compressibility approximation .

The chain term:


\[
\tilde{a}^{\mathrm{chain}} = -\sum_{i} x_{i}(m_{i}-1)\ln\,y_{ii}^{\mathrm{Mie}}(\sigma_{ii})
\]


where $y_{ii}^{\mathrm{Mie}}$ is the cavity correlation function.

##### Pure-Component Parameters

| Symbol                 | Description                         | Unit |
|:-----------------------|:------------------------------------|:-----|
| $m$                  | Number of segments per chain        | –    |
| $\sigma$             | Segment diameter                    | Å    |
| $\varepsilon/k$      | Segment dispersion energy           | K    |
| $\lambda_{r}$        | Repulsive Mie exponent              | –    |
| $\lambda_{a}$        | Attractive Mie exponent (often 6)   | –    |
| $\varepsilon^{AB}/k$ | Association energy (if associating) | K    |
| $\kappa^{AB}$        | Association volume (if associating) | –    |

SAFT-VR Mie pure-component parameters

#### SAFT-VRQ Mie Equation of State {#sec:saftvrqmie}

##### Overview {#overview-62}

SAFT-VRQ Mie  extends SAFT-VR Mie to quantum-mechanical effects relevant for light molecules such as , , , and . Quantum corrections are incorporated via the Feynman–Hibbs (FH) perturbation approach .

##### Quantum-Corrected Pair Potential

The effective pair potential at first order in the de Broglie thermal wavelength is :


<a id="eq:fh1"></a>

\[
u^{\mathrm{FH1}}(r) = u^{\mathrm{Mie}}(r)
    + \frac{\hbar^{2}}{24\mu\,kT}\nabla^{2}u^{\mathrm{Mie}}(r)
\]


where $\mu = m_{1}m_{2}/(m_{1}+m_{2})$ is the reduced mass, and the Laplacian of the Mie potential is:


\[
\nabla^{2}u^{\mathrm{Mie}}(r) = C\,\varepsilon\,\sigma^{2}
    \left[
      \frac{\lambda_{r}(\lambda_{r}+1)}{\sigma^{2}}
      \!\left(\frac{\sigma}{r}\right)^{\!\lambda_{r}+2}
      - \frac{\lambda_{a}(\lambda_{a}+1)}{\sigma^{2}}
      \!\left(\frac{\sigma}{r}\right)^{\!\lambda_{a}+2}
    \right]
\]


A second-order correction $u^{\mathrm{FH2}}$ is available for the lightest species (, ) . The corrected potential is then used in place of $u^{\mathrm{Mie}}$ in all SAFT-VR Mie perturbation integrals.

##### Dimensionless Quantum Parameter

The strength of the quantum correction is characterised by the de Broglie parameter:


\[
Q^{2} = \frac{\hbar^{2}}{m\,\varepsilon\,\sigma^{2}\,k}
\]


Larger $Q$ (smaller mass, smaller potential well) indicates stronger quantum effects. For at 298 K the correction to the second virial coefficient exceeds 20% .

#### Modified Benedict–Webb–Rubin Equation (MBWR) {#sec:mbwr}

##### Overview {#overview-63}

The Modified Benedict–Webb–Rubin (MBWR) equation is a high-accuracy multiparameter EOS expressed as a power series in molar density $\rho$. Two variants are available in the ThermoPack library:

- **MBWR19** : 19-term form, widely used for cryogenic fluids (N$_2$, O$_2$, Ar, CH$_4$, etc.)

- **MBWR32** : 32-term extension providing higher accuracy over wide temperature and pressure ranges

##### MBWR19 Form

The pressure is :


<a id="eq:mbwr19"></a>

\[
P = \rho RT + \sum_{n=2}^{9} a_{n}(T)\,\rho^{n}
    + \exp\!\left(-\gamma\rho^{2}\right)
      \sum_{n=1}^{5} a_{n+9}(T)\,\rho^{2n-1}
\]


where $\gamma = 1/\rho_{\mathrm{c}}^{2}$ and the temperature-dependent coefficients have the general form:


\[
a_{n}(T) = \sum_{k=1}^{K_{n}}
    \frac{c_{nk}}{T^{k-1}}
\]


with empirical constants $c_{nk}$ fitted to PVT, saturation, and caloric data.

##### MBWR32 Form

The Younglove–Ely MBWR32 equation  uses 32 terms:


<a id="eq:mbwr32"></a>

\[
P = \sum_{n=1}^{9} a_{n}(T)\,\rho^{n}
    + \exp\!\left(-\gamma\rho^{2}\right)
      \sum_{n=10}^{15} a_{n}(T)\,\rho^{2n-21}
\]


Coefficients $a_{n}$ are polynomial functions of $1/T$ with up to five terms each, giving 32 temperature-dependent parameters in total. Both the MBWR19 and MBWR32 forms yield densities, enthalpies, entropies, and phase boundaries with near-experimental accuracy for pure components.

##### Derived Properties

All thermodynamic properties are derived analytically from Eq. [\[eq:mbwr19\]](#eq:mbwr19)–[\[eq:mbwr32\]](#eq:mbwr32) via standard thermodynamic identities. The residual Helmholtz energy is obtained by integration:


\[
\frac{A^{\mathrm{res}}}{RT}
  = \int_{\infty}^{\rho} \frac{P/(\rho RT) - 1}{\rho}\,\mathrm{d}\rho
\]


#### NIST Multiparameter Equation of State (NIST-MEOS) {#sec:nistmeos}

##### Overview {#overview-64}

The NIST multiparameter equations of state, developed predominantly by Span, Lemmon, Wagner, and co-workers , represent the state of the art in pure-fluid thermodynamic accuracy. They are formulated as explicit functions of the reduced Helmholtz energy $\alpha(\delta,\tau)$:


<a id="eq:meos_alpha"></a>

\[
\frac{A(\rho,T)}{RT} = \alpha(\delta,\tau)
    = \alpha^{\mathrm{o}}(\delta,\tau) + \alpha^{\mathrm{r}}(\delta,\tau)
\]


where $\delta = \rho/\rho_{\mathrm{c}}$ and $\tau = T_{\mathrm{c}}/T$ are the reduced density and inverse temperature.

##### Ideal-Gas Part

The ideal-gas Helmholtz contribution is :


<a id="eq:meos_ideal"></a>

\[
\alpha^{\mathrm{o}}(\delta,\tau)
  = \ln\delta + a_{1} + a_{2}\tau + a_{3}\ln\tau
  + \sum_{k=4}^{K} a_{k}\ln\!\left[1 - \exp(-\vartheta_{k}\tau)\right]
\]


The logarithmic terms represent quantum (Einstein) oscillators corresponding to the vibrational modes of the molecule.

##### Residual Part

The residual Helmholtz energy is a multi-term functional of the form:


<a id="eq:meos_residual"></a>

\[
\alpha^{\mathrm{r}}(\delta,\tau)
  = \sum_{k=1}^{K_{1}} n_{k}\,\delta^{d_{k}}\,\tau^{t_{k}}
  + \sum_{k=K_{1}+1}^{K_{2}} n_{k}\,\delta^{d_{k}}\,\tau^{t_{k}}
    \exp(-\delta^{c_{k}})
  + \sum_{k=K_{2}+1}^{K_{3}} n_{k}\,\delta^{d_{k}}\,\tau^{t_{k}}
    \exp\!\left[-\eta_{k}(\delta-\varepsilon_{k})^{2}-\beta_{k}(\tau-\gamma_{k})^{2}\right]
\]


The third group of Gaussian terms (“bank” terms) is used to represent the near-critical region .

##### Thermodynamic Properties from Helmholtz Derivatives

All equilibrium properties follow from partial derivatives of $\alpha$. A selection of key relations is:


\[
Z = \frac{Pv}{RT} = 1 + \delta\,\alpha^{\mathrm{r}}_{\delta}
\]




\[
\frac{H - H^{\mathrm{ig}}}{RT}
  = \tau\!\left(\alpha^{\mathrm{o}}_{\tau} + \alpha^{\mathrm{r}}_{\tau}\right)
    + \delta\,\alpha^{\mathrm{r}}_{\delta} + 1
    - \frac{H^{\mathrm{ig}}}{RT}
  \quad\text{(simplified form)}
\]




\[
\frac{S - S^{\mathrm{ig}}}{R}
  = \tau\!\left(\alpha^{\mathrm{o}}_{\tau} + \alpha^{\mathrm{r}}_{\tau}\right)
    - \alpha^{\mathrm{o}} - \alpha^{\mathrm{r}}
  \quad\text{(simplified form)}
\]


where subscripts denote partial derivatives: $\alpha^{\mathrm{r}}_{\delta} = (\partial\alpha^{\mathrm{r}}/\partial\delta)_{\tau}$, etc.

##### Accuracy and Component Coverage

NIST-MEOS equations are available for over 200 fluids in the ThermoPack database, including refrigerants, hydrocarbons, cryogenic fluids, and common gases. For reference fluids such as water (IAPWS-IF97 ), carbon dioxide , and nitrogen , the equations are valid over the full fluid range from the triple point to several times the critical temperature, with uncertainties in density typically below 0.1% and in sound speed below 0.02%.

#### ThermoPack Backend and Common Computational Features {#sec:thermopack_backend}

All property packages in this section use the open-source ThermoPack library  as the computational backend, accessed from DWSIM via the Python .NET interoperability layer. Common features include:

- **Fugacity and phase equilibrium.** Fugacity coefficients $\ln\hat\phi_{i}$ are obtained analytically from the respective Helmholtz-energy or pressure-explicit derivative.

- **Caloric properties.** Enthalpy and entropy departures from the ideal-gas reference are computed analytically using standard thermodynamic identities.

- **Poynting correction.** Partial molar volumes for condensed phases are used to apply a Poynting pressure correction to liquid fugacities.

- **Heat capacities.** $C_{p}$ and $C_{v}$ are evaluated via analytical residual derivatives supplemented by ideal-gas polynomial or NASA correlations.

- **Transport properties.** Viscosity, thermal conductivity, and surface tension are provided by the Lee–Kesler and other built-in correlations within DWSIM’s standard transport-property framework.

