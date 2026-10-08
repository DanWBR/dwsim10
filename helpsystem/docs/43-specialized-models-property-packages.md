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

##### Overview {#overview-51}

The Electrolyte Non-Random Two-Liquid (eNRTL) model computes activity coefficients for aqueous electrolyte solutions by splitting the excess Gibbs energy into two additive contributions :


<a id="eq:enrtl_split"></a>

\[
\ln\gamma_{i} = \ln\gamma_{i}^{\mathrm{LR}} + \ln\gamma_{i}^{\mathrm{SR}}
\]


where the long-range (LR) term accounts for electrostatic ion–ion interactions via the Pitzer–Debye–Hückel equation, and the short-range (SR) term captures local-composition effects through the NRTL framework.

##### Long-Range Term: Pitzer–Debye–Hückel

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

##### Short-Range Term: Local Composition NRTL

The short-range contribution follows the two-liquid theory of Renon & Prausnitz  extended to electrolytes by Chen & Evans . Two key assumptions are made: (1) local electroneutrality around every central species, and (2) like-ion repulsion (identically charged ions are not immediate neighbours).

The NRTL $G$-matrix element is


\[
G_{ij} = \exp\!\left(-\alpha\,\tau_{ij}\right)
\]


where $\alpha = 0.2$ (default for molecule–ion pairs) is the non-randomness parameter and $\tau_{ij}$ is the binary interaction energy parameter.

###### Water–electrolyte parameters

For a single salt $\ce{C_{\nu+}A_{\nu-}}$ dissolved in water, two asymmetric $\tau$ parameters are required: $\tau_{w,ca}$ (water around the cation–anion pair) and $\tau_{ca,w}$ (ion pair around water). Temperature dependence is expressed via the three-parameter Gibbs–Helmholtz form of Hossain, Bhattacharia and Chen :


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

###### Validation against experimental $\gamma_{\pm}$

For the salts in the `water_electrolyte_pairs` set, the implementation reproduces the published Chen–Evans correlations to within numerical precision. For the auto-fitted Kim–Frederick subset, residuals against the underlying isopiestic / EMF data of Robinson & Stokes  are typically $\chi^{2} < 10^{-2}$ over $m = 0.05\,m_{\max}$ to $0.95\,m_{\max}$ ($m_{\max}$ being each salt’s documented saturation molality), with worst-case deviations confined to the high-$m$ tail of strongly ion-paired systems (notably FeCl$_3$, ZnBr$_2$, ZnI$_2$).

###### Pitzer fallback for unparameterised pairs

When a salt encountered at runtime has no $\tau$ entry in `enrtl_parameters.json` but a Pitzer single-salt parameter set is available in the companion file `pitzer_parameters.json` (see Section [6.14](#sec:pitzer_database)), the property package emits a warning to the flowsheet log identifying the pair and reporting the available Pitzer $\beta^{0}$, $\beta^{1}$, $C^{\varphi}$ values. The unknown pair otherwise contributes $\tau = 0$ to the short-range term (ideal residual contribution; only the Pitzer–Debye–Hückel long-range term is then active).

#### Extended UNIQUAC {#sec:exuniquac}

##### Overview {#overview-52}

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

#### Sour Water (Edwards Model) {#sec:sourwater}

##### Overview {#overview-53}

The Sour Water model is based on the fugacity-based VLE framework of Edwards, Maurer, Newman and Prausnitz . It targets aqueous systems containing $\ce{H2S}$, $\ce{NH3}$, $\ce{CO2}$, and $\ce{HCN}$ dissolved in water. Activity coefficients for the dissolved molecular species are calculated by a simplified Margules–Pitzer expression. The package finds the five compounds by name (`Water`, `Hydrogen sulfide`, `Ammonia`, `Carbon dioxide`, `Hydrogen cyanide`); a stream may hold any subset of them, in any order, together with other compounds.

##### Vapour–Liquid Equilibrium

The VLE relationship for each volatile molecular species is


<a id="eq:sw_vle"></a>

\[
p_{i} = H_{i}(T)\,c_{i}\,\gamma_{i}
\]


where $H_{i}(T)$ is the Henry constant (Pa$\cdot$m$^{3}$/mol), $c_{i}$ (mol/m$^{3}$) the concentration of the molecular (undissociated) form of the species, and $\gamma_{i}$ the activity coefficient. The total concentration is taken from the liquid mole fractions as $55\,500\,x_{i}/x_{w}$ mol/m$^{3}$ (dilute solution), and the molecular form is the fraction $\alpha_{0}$ of it given by the speciation model below. Water follows its vapour pressure.

##### Activity Coefficients

Following Edwards et al. , the activity coefficient of each dissolved molecular species depends on the ionic strength through a simplified Pitzer expression:


<a id="eq:beta_I"></a>

\[
\ln\gamma_{i} = \beta_{i}\,I
\]


where $I$ (mol/kg) is the ionic strength and $\beta_{i}$ is an empirical molecule–ion interaction parameter. Values from Edwards et al.  are listed in Table [27](#tab:beta).



<a id="tab:beta"></a>



| Species      | $\beta_{i}$ |
|:-------------|:-------------:|
| $\ce{H2S}$ |  $-0.324$   |
| $\ce{NH3}$ |  $-0.190$   |
| $\ce{CO2}$ |  $-0.292$   |
| $\ce{HCN}$ |  $-0.160$   |

Molecule–ion interaction parameters $\beta_{i}$ (kg/mol)



##### Ionic Strength

The ionic strength in the liquid phase is


<a id="eq:ionic_strength"></a>

\[
I = \tfrac{1}{2}\sum_{i} c_{i}\,z_{i}^{2}
\]


where the sum runs over all ionic species. For the sour water system ($\ce{HS-}$, $\ce{S^{2-}}$, $\ce{NH4+}$, $\ce{HCO3-}$, $\ce{CO3^{2-}}$, $\ce{CN-}$, $\ce{H+}$, $\ce{OH-}$):


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

###### $\ce{H2S}$ system:



\[
\alpha_{0}^{\ce{H2S}} = \frac{1}{D_{\ce{H2S}}},
  \quad
  \alpha_{1}^{\ce{H2S}} = \frac{K_{a1}/h}{D_{\ce{H2S}}},
  \quad
  \alpha_{2}^{\ce{H2S}} = \frac{K_{a1}\,K_{a2}/h^{2}}{D_{\ce{H2S}}}
\]


with $D_{\ce{H2S}} = 1 + K_{a1}/h + K_{a1}\,K_{a2}/h^{2}$. Analogous expressions apply to the $\ce{CO2}$ and $\ce{HCN}$ systems.

###### $\ce{NH3}$ system:



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


Table [28](#tab:sw_keq) lists the reference constants, from Edwards et al. , and the reaction enthalpies with their sources.



<a id="tab:sw_keq"></a>



| Reaction | $K_{25}$ | $\Delta H_{\mathrm{rxn}}$ (J/mol) | Source of $\Delta H_{\mathrm{rxn}}$ |
|:---|:---|:---|:---|
| $\ce{H2S <=> H+ + HS-}$ | $1.02\times10^{-7}$ | $+22{,}100$ |  |
| $\ce{HS- <=> H+ + S^{2-}}$ | $1.30\times10^{-14}$ | $+50{,}600$ |  |
| $\ce{NH3 + H2O <=> NH4+ + OH-}$ | $1.80\times10^{-5}$ | $+3{,}860$ |  |
| $\ce{CO2 + H2O <=> H+ + HCO3-}$ | $4.30\times10^{-7}$ | $+9{,}150$ |  |
| $\ce{HCO3- <=> H+ + CO3^{2-}}$ | $4.70\times10^{-11}$ | $+14{,}900$ |  |
| $\ce{HCN <=> H+ + CN-}$ | $6.20\times10^{-10}$ | $+43{,}500$ |  |
| $\ce{H2O <=> H+ + OH-}$ | $1.01\times10^{-14}$ | $+55{,}800$ |  |

Equilibrium constants and reaction enthalpies at 25 °C



The $\ce{NH3}$ enthalpy is $\Delta H(K_{w}) - \Delta H(\ce{NH4+ <=> NH3 + H+})
= 55.81 - 51.95$ kJ/mol: $K_{b}$ grows by a factor of about 1.2 between 25 and 100 °C.

The Henry constants follow $H_{i}(T) = H_{i,25}\exp\!\left[-C_{H,i}\left(1/T - 1/298.15\right)\right]$, with the values of Table [29](#tab:sw_henry). $H_{25}$ of $\ce{H2S}$ and $\ce{CO2}$ comes from their solubilities at 25 °C and 1 atm (0.10 and 0.034 mol/L).



<a id="tab:sw_henry"></a>



| Species | $H_{25}$ (Pa$\cdot$m$^{3}$/mol) | $C_{H}$ (K) | Source |
|:---|:---|:---|:---|
| $\ce{H2S}$ | $1013$ | 2100 | solubility; $C_{H}$ |
| $\ce{NH3}$ | $1.76$ | 4100 | ; $C_{H}$ |
| $\ce{CO2}$ | $2980$ | 2400 | solubility; $C_{H}$ |
| $\ce{HCN}$ | $8.4$ | 5000 | ; $C_{H}$ |

Henry constants at 25 °C for the sour water model



##### Flash Calculations

The $K$-values depend on the liquid composition through the speciation, so the flash routines of the package take that dependence into account:

- **PT flash:** successive substitution on the $K$-values with the Rachford–Rice equation. The single-phase tests use the liquid each phase would be in equilibrium with: the feed itself at the bubble point, the incipient liquid $z/K$ at the dew point.

- **Bubble and dew points:** roots of $\sum_{i} z_{i}K_{i}(\boldsymbol{z}) = 1$ and $\sum_{i} z_{i}/K_{i}(\boldsymbol{x}_{\mathrm{dew}}) = 1$, where the incipient liquid $\boldsymbol{x}_{\mathrm{dew}}$ is found by successive substitution. Both sums are monotonic in $T$ and in $\ln P$; each root is bracketed from the initial estimate and bisected.

- **PV and TV flashes:** for a vapour fraction between 0 and 1 the temperature (or pressure) is bisected between the bubble and dew points on the vapour fraction of PT flashes.

For a stripper feed with 0.6 mol/kg $\ce{NH3}$ and 0.45 mol/kg $\ce{H2S}$, the bubble pressure at 40 °C is 0.22 bar and the bubble temperature at 1 atm is 72 °C.

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
| CO$_2$ Capture | Peng–Robinson EOS (always real-gas) |
| CO$_2$ Storage | Peng–Robinson EOS with $k_{ij}(\ce{CO2}\text{--}\ce{H2O}) = 0.193$ (always real-gas) |

How each electrolyte PP honours the vapour-fugacity mode

###### When to switch

The default `Ideal` mode is appropriate for the vast majority of electrolyte applications, where the vapour phase contains water vapour and traces of dissolved acid gases ($\ce{CO2}$, $\ce{H2S}$, $\ce{HCl}$, $\ce{NH3}$) at near-atmospheric total pressure. Switching to `PengRobinson` is recommended when:

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

Dissolved ions modify the transport properties of the aqueous solvent. The following ion-additive correlations are applied as multiplicative correction factors to the viscosity and thermal conductivity of the ion-free solvent: the underlying mixing rules (`AUX_LIQVISCm` and `AUX_CONDTL`) evaluated over the compounds that are neither ions nor salts (water and the molecular solutes), with their fractions renormalised. A liquid without ions keeps the values of the mixing rules.

##### Viscosity: Jones–Dole Equation {#sec:jones_dole}

The Jones–Dole equation , with the Kaminsky extension , relates the viscosity of an electrolyte solution to the pure-solvent value $\eta_{0}$:


<a id="eq:jones_dole"></a>

\[
\frac{\eta}{\eta_{0}}
  = 1
    + A\sqrt{I}
    + \sum_{i} B_{i}(T)\,c_{i}
    + D\,c^{2}
\]


where $I$ (mol/L) is the ionic strength, $c_{i}$ (mol/L) the molar concentration of ion $i$, $c = \tfrac{1}{2}\sum_{i} c_{i}$ the electrolyte concentration (the salt concentration of a 1:1 electrolyte), $A$ the Falkenhagen electrostatic coefficient, $B_{i}$ the ion-specific Jones–Dole *B-coefficient*, and $D$ the empirical Kaminsky quadratic coefficient. For 1 mol/kg NaCl at 25 °C the package gives 0.979 mPa$\cdot$s, against 0.974 mPa$\cdot$s measured.

###### Falkenhagen coefficient

An approximate temperature-dependent expression is used: $A \approx 0.005\sqrt{298.15/T}$. For typical 1:1 electrolytes at 25 °C the term $A\sqrt{I}$ contributes less than 0.5% and is only significant at very low concentrations .

###### Kaminsky coefficient

A global average $D = 0.007$ L$^{2}$/mol$^{2}$ is used for all electrolytes. This quadratic term becomes relevant above $\sim\!1$ mol/L of electrolyte.

###### B-coefficients

The Jones–Dole $B$-coefficient is ion-specific and reflects whether the ion is a *structure maker* ($B > 0$, increases viscosity) or *structure breaker* ($B < 0$, decreases viscosity) with respect to the hydrogen-bond network of water. $B$ has a mild linear temperature dependence :


\[
B_{i}(T) = B_{i}^{25} + \frac{dB_{i}}{dT}\,(T - 298.15)
\]


Representative values from Marcus  and Jenkins & Marcus  are listed in Table [30](#tab:jones_dole_B).



<a id="tab:jones_dole_B"></a>



| Cation           | $B^{25}$ | Anion             | $B^{25}$ |
|:-----------------|-----------:|:------------------|-----------:|
| $\ce{H+}$      |  $0.068$ | $\ce{Cl-}$      | $-0.007$ |
| $\ce{Li+}$     |  $0.150$ | $\ce{OH-}$      |  $0.112$ |
| $\ce{Na+}$     |  $0.086$ | $\ce{F-}$       |  $0.107$ |
| $\ce{K+}$      | $-0.007$ | $\ce{Br-}$      | $-0.032$ |
| $\ce{NH4+}$    | $-0.007$ | $\ce{I-}$       | $-0.068$ |
| $\ce{Ca^{2+}}$ |  $0.285$ | $\ce{HCO3-}$    |  $0.032$ |
| $\ce{Mg^{2+}}$ |  $0.385$ | $\ce{CO3^{2-}}$ |  $0.294$ |
| $\ce{Ba^{2+}}$ |  $0.220$ | $\ce{SO4^{2-}}$ |  $0.208$ |
| $\ce{Fe^{2+}}$ |  $0.428$ | $\ce{HS-}$      |  $0.030$ |
| $\ce{Al^{3+}}$ |  $0.744$ | $\ce{NO3-}$     | $-0.046$ |
| $\ce{Fe^{3+}}$ |  $0.690$ | $\ce{CN-}$      |  $0.030$ |

Jones–Dole $B$-coefficients at 25 °C (L/mol)



###### Implementation

For the eNRTL and Extended UNIQUAC property packages, which carry explicit ionic species on the material stream, the correction factor is computed directly from the phase composition. For the Sour Water package, which embeds speciation in modified $K$-values, the ionic concentrations are first obtained from its speciation model (Section [6.7](#sec:sourwater)) and then fed to Eq. [\[eq:jones_dole\]](#eq:jones_dole).

The correction is clamped to the range $[0.5,\;5.0]$ to guard against extrapolation beyond the correlation’s valid concentration range.

##### Thermal Conductivity: Riedel Equation {#sec:riedel}

The Riedel equation  provides an analogous ion-additive correction for the thermal conductivity of aqueous electrolyte solutions:


<a id="eq:riedel"></a>

\[
\frac{\lambda}{\lambda_{0}}
  = 1 - \sum_{i} \alpha_{i}\,c_{i}
\]


where $\lambda_{0}$ is the thermal conductivity of the ion-free solvent and $\alpha_{i}$ (L/mol) is the ion-specific thermal conductivity decrement coefficient. For 1 mol/kg NaCl at 25 °C the package gives 0.604 W/(m$\cdot$K).

Most ions *decrease* thermal conductivity ($\alpha > 0$) by disrupting the hydrogen-bond network that makes water an unusually efficient thermal conductor. Notable exceptions are $\ce{H+}$ and $\ce{OH-}$ ($\alpha < 0$), which *increase* $\lambda$ via the Grotthuss proton-hopping mechanism .

Representative $\alpha$ values compiled from Horvath  are listed in Table [31](#tab:riedel_alpha).



<a id="tab:riedel_alpha"></a>



| Cation           | $\alpha$ | Anion             | $\alpha$ |
|:-----------------|-----------:|:------------------|-----------:|
| $\ce{H+}$      | $-0.030$ | $\ce{Cl-}$      | $0.0053$ |
| $\ce{Li+}$     |  $0.023$ | $\ce{OH-}$      | $-0.016$ |
| $\ce{Na+}$     | $0.0044$ | $\ce{F-}$       | $-0.002$ |
| $\ce{K+}$      | $-0.010$ | $\ce{Br-}$      |  $0.019$ |
| $\ce{NH4+}$    | $-0.007$ | $\ce{I-}$       |  $0.035$ |
| $\ce{Ca^{2+}}$ | $0.0045$ | $\ce{HCO3-}$    |  $0.010$ |
| $\ce{Mg^{2+}}$ |  $0.003$ | $\ce{CO3^{2-}}$ |  $0.005$ |
| $\ce{Ba^{2+}}$ |  $0.013$ | $\ce{SO4^{2-}}$ | $-0.003$ |
| $\ce{Fe^{2+}}$ |  $0.009$ | $\ce{HS-}$      |  $0.008$ |
| $\ce{Al^{3+}}$ |  $0.012$ | $\ce{NO3-}$     |  $0.011$ |

Riedel $\alpha$-coefficients at 25 °C (L/mol)



The implementation follows the same two-path strategy as the viscosity correction. The result is clamped to $[0.5,\;1.5]$ (a narrower range than for viscosity, reflecting the typically smaller magnitude of the thermal conductivity effect).

#### Aqueous-Phase pH and Ionic Strength {#sec:ph_ionic}

All electrolyte property packages compute and store the following aqueous-phase properties on the material stream after each flash calculation:

##### pH

For the eNRTL and Extended UNIQUAC packages, the pH is obtained from the equilibrium solver’s speciation result, which accounts for all dissociation and association reactions and activity-coefficient corrections:


\[
\mathrm{pH} = -\log_{10}[\ce{H+}]
\]


The hydrogen ion is the compound `Hydron` ($\ce{H+}$) or `Hydronium` ($\ce{H3O+}$).

For the Sour Water package, the pH is computed by the Edwards-model `PHSolver`, which solves the coupled charge-balance for the $\ce{H2S}$–$\ce{NH3}$–$\ce{CO2}$–$\ce{HCN}$–$\ce{H2O}$ system (Section [6.7](#sec:sourwater)).

For the CO$_2$ Capture package with an amine, the pH comes from the speciation of the amine model (Section [6.15](#sec:ccus_capture)); for the CO$_2$ Storage package, from its carbonate speciation (Section [6.17](#sec:ccus_storage)).

When no equilibrium solve has been performed (e.g. during initialisation), a fallback approximate pH is estimated from the composition using the auxiliary electrolyte calculator.

##### Osmotic Coefficient and Freezing Point

The osmotic coefficient and the freezing point come from the water activity of the package’s own model, $a_{w} = x_{w}\gamma_{w}$, with $\gamma_{w}$ obtained from the liquid fugacity coefficient of water. The freezing point depression is


\[
\Delta T_{f} = -\frac{R\,T_{f}^{2}}{\Delta H_{\mathrm{fus}}}\,\ln a_{w},
  \qquad T_{f}' = T_{f} - \Delta T_{f}
\]


For 1 mol/kg NaCl the Electrolyte NRTL package gives an osmotic coefficient of 0.933 (0.936 in Robinson and Stokes ) and a freezing point of $-3.48$ °C.

##### Ionic Strength

The ionic strength of the aqueous phase is computed as


\[
I = \tfrac{1}{2}\sum_{i} c_{i}\,z_{i}^{2}
\]


For the eNRTL and Extended UNIQUAC packages, the sum is taken over all ionic species returned by the equilibrium solver. For the Sour Water package, the ionic concentrations are obtained from its speciation model, which gives an ionic strength consistent with the computed pH.

#### H$_2$O–HCl (Pitzer) {#sec:hcl_pitzer}

##### Overview {#overview-54}

The H$_2$O–HCl property package implements the binary Pitzer ion-interaction model with the high-temperature parameter fit of Ruaya & Seward , validated experimentally to 350 $^{\circ}$C and 3 mol/kg HCl. HCl is treated as a fully dissociated 1–1 strong electrolyte ($\ce{HCl(aq) -> H+ + Cl-}$); the model returns mean ionic and individual-ion activity coefficients, water activity, osmotic coefficient, solution pH, HCl partial pressure, and the integral heat of solution and apparent molar heat capacity of HCl.

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
P(T) = q_{1} + q_{2}\left(\frac{1}{T} - \frac{1}{T_{\mathrm{ref}}}\right)
       + q_{3}\ln\!\left(\frac{T}{T_{\mathrm{ref}}}\right)
       + q_{4}\,(T - T_{\mathrm{ref}})
       + q_{5}\,(T^{2} - T_{\mathrm{ref}}^{2})
\]


with $q$ coefficients from Ruaya & Seward Table 2 (reproduced in Table [32](#tab:hcl_pitzer_params)). $C^{\varphi}$ is intentionally set to zero in this fit; for systems requiring third-virial accuracy at $m > 3$ and $T < 100~^{\circ}$C, the Pitzer & Mayorga 1973  value $C^{\varphi}(298) = 8.0\times10^{-4}$ is the standard reference.



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


with $K_{sp} = (\gamma_{\pm}m)^{2}\,a_{w}^{n}$. The flash of the package is a vapour–liquid flash: the hydrates do not form a solid phase in it, and their saturation is reported through the saturation ratio $(\gamma_{\pm}m)^{2}a_{w}^{n}/K_{sp}(T)$. Above the peritectic, $K_{sp} \to \infty$ and the hydrate is never saturated.

##### HCl Partial Pressure

The HCl partial pressure over the solution is $p_{\mathrm{HCl}} = K_{H}(T)\,m^{2}\gamma_{\pm}^{2}$, where $K_{H}$ is the inverse of the equilibrium constant of $\ce{HCl(g) <=> H+(aq) + Cl-(aq)}$ on the molal scale. It is integrated from 298.15 K with a constant reaction heat capacity:


\[
\ln\frac{1}{K_{H}} = \frac{\Delta_{r}G^{\circ}}{RT_{0}}
    + \frac{\Delta_{r}H^{\circ}}{R}\left(\frac{1}{T} - \frac{1}{T_{0}}\right)
    - \frac{\Delta_{r}C_{p}^{\circ}}{R}\left(\ln\frac{T}{T_{0}} + \frac{T_{0}}{T} - 1\right)
\]


with $\Delta_{r}G^{\circ} = -35.929$ kJ/mol, $\Delta_{r}H^{\circ} =
-74.852$ kJ/mol and $\Delta_{r}C_{p}^{\circ} = -165.52$ J/(mol$\cdot$K) from the NBS tables  ($K_{H}$ in bar$\cdot$kg$^{2}$/mol$^{2}$, multiplied by $10^{5}$ for Pa). $K_{H}(298.15~\mathrm{K}) =
0.0508$ Pa$\cdot$kg$^{2}$/mol$^{2}$. The heat capacity is taken as constant, which is not verified above about 110 °C.

##### Poynting Correction

At system pressures above water saturation, the HCl partial pressure includes a Poynting term:


\[
p_{\mathrm{HCl}}(T,P) = K_{H}(T)\,m^{2}\,\gamma_{\pm}^{2}
    \cdot \exp\!\left[\frac{\bar{V}_{\mathrm{HCl}}(P-P_{\mathrm{sat}})}{RT}\right]
\]


with the partial molar volume $\bar{V}_{\mathrm{HCl}} = 17.8\times10^{-6}$ m$^{3}$/mol (Söhnel & Novotný 1985 ). The correction is negligible at 1 atm ($<0.01\,\%$), reaches $\sim$<!-- -->7 % at 100 bar and $\sim$<!-- -->2$\times$ at 1000 bar.

##### Liquid Density

In the liquid density HCl takes its apparent molar volume in water, while the rest of the liquid keeps the volume of the electrolyte model:


\[
\phi_{V} = 17.8164 + 0.021512\,\Delta T - 4.9124\times10^{-4}\,\Delta T^{2}
    + \left(0.8850 + 0.005858\,\Delta T\right)\sqrt{m} - 0.00842\,m
\]


in cm$^{3}$/mol, with $\Delta T = T - 298.15$ K. The correlation is a least-squares fit to the densities of Perry’s Table 2-57  (1 to 30 wt%, 0 to 100 °C, 112 points, within 0.28 %).

##### Liquid Enthalpy

Dissolved HCl takes its apparent molar enthalpy on the ideal-gas reference of the vapour, so that the heat of absorption comes out of the enthalpy balance:


\[
H_{\mathrm{HCl}}^{\mathrm{app}}(m,T) = \Delta_{\mathrm{sol}}H^{\infty}
    + \phi_{L}(m) + \phi_{C_p}(m)\,(T - 298.15~\mathrm{K})
\]


with $\Delta_{\mathrm{sol}}H^{\infty} = \Delta_{f}H^{\circ}(\ce{Cl-},\mathrm{aq})
- \Delta_{f}H^{\circ}(\ce{HCl},\mathrm{g}) = -74.852$ kJ/mol and the relative apparent molar enthalpy $\phi_{L}$ from the NBS tables  ($\Delta_{f}H$ of HCl in $n$ $\ce{H2O}$, $n$ = 1 to 50 000) and the apparent molar heat capacity $\phi_{C_p}$ from Parker  at 25 °C, both interpolated in $\sqrt{m}$. Water and the other compounds keep the enthalpy of the electrolyte model; the liquid heat capacity adds $n_{\mathrm{HCl}}\,\phi_{C_p}(m)$. Above 55.5 mol/kg (one HCl per water) the term blends linearly to pure liquid HCl.



<a id="tab:hcl_enthalpy"></a>



| Case | Model | Reference |
|:---|---:|---:|
| Integral heat of solution, 5.55 mol/kg, 25 °C (kJ/mol) | $-69.011$ | $-69.011$ (NBS) |
| Integral heat of solution, 15 mol/kg, 25 °C (kJ/mol) | $-59.48$ | $-59.46$ (Parker) |
| HCl(g) + water to 1 mol/kg from 25 °C, outlet (°C) | 43.07 | 42.96 |
| HCl(g) + water to 3 mol/kg from 25 °C, outlet (°C) | 79.12 | 79.65 |
| Dilution from 15 to 4 mol/kg at 25 °C, outlet (°C) | 36.46 | 36.40 |
| Liquid $C_{p}$, 5.55 mol/kg, 25 °C (kJ/(kg$\cdot$K)) | 3.163 | 3.153 |

H$_2$O–HCl (Pitzer): heat of solution and adiabatic mixing at 1 atm



The reference temperatures of the mixers are hand calculations from the NBS and Parker tables.

##### Flash Calculations

The partial pressures of water and HCl depend on the HCl molality of the liquid only, so the $K$-values change steeply with the liquid composition ($K_{\mathrm{HCl}}$ goes from 0.57 to 0.98 between $x_{\mathrm{HCl}} = 0.100$ and 0.111 at the azeotrope). The package therefore has a flash of its own:

- **PT flash:** the unknown is the HCl fraction $s$ of the water plus HCl in the liquid. It fixes the molality, hence the vapour fractions $y_{w}(s)$ and $y_{\mathrm{HCl}}(s)$; the water and HCl balances give the vapour fraction in closed form, and every other compound splits by its own $K$-value. The residual $\sum y - 1$ is bracketed from the feed towards the far side of the azeotrope it sits on and solved by Brent’s method.

- **Bubble and dew points:** roots of the logarithms of $\sum z_{i}K_{i}(\boldsymbol{z})$ and of the dew sum, in $T$ or in $\ln P$; the dew liquid is the one whose vapour has the feed’s HCl to water ratio.

- **PV, TV, PH and PS flashes:** the specification picks the region (subcooled, two-phase or superheated) and the temperature or pressure is solved inside it.

- Streams without water or without HCl use the default flash.

Table [35](#tab:hcl_txy) gives the bubble and dew points at 1 atm. The tie lines of PT flashes land on these curves within $5\times10^{-10}$ K.



<a id="tab:hcl_txy"></a>



| wt% HCl | $T_{\mathrm{bubble}}$ model (°C) | $T_{\mathrm{dew}}$ model (°C) | $T_{\mathrm{bubble}}$ Perry (°C) |
|---:|---:|---:|---:|
| 6 | 101.87 | 106.62 | 101.65 |
| 10 | 103.69 | 107.31 | 103.12 |
| 14 | 105.91 | 107.64 | 104.98 |
| 18 | 107.67 | 107.75 | 107.50 |
| 20 | 107.51 | 107.74 | 108.19 |
| 22 | 105.72 | 107.71 | 108.10 |
| 26 | 94.13 | 107.54 | 103.34 |
| 30 | 71.73 | 107.28 | 90.33 |

H$_2$O–HCl (Pitzer): bubble and dew points at 1 atm against Perry’s Tables 2-9 and 2-10



An absorber taking 10 mol/s of HCl gas into 1 kg/s of water at 25 °C and 1 atm boils at 103.95 °C with a vapour fraction of 0.109, and its outlet enthalpy equals that of the feeds; cooling it back to 25 °C takes 646.07 kW (646.1 kW from the NBS tables).

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

###### Validation against Robinson & Stokes 1959 at 25 $^{\circ}$C {#validation-against-robinson-stokes-1959-at-25-circc}

| $m$ (mol/kg) | $\gamma_{\pm}$ (model) | $\gamma_{\pm}$ (R&S ) | Deviation |
|---:|---:|---:|---:|
| 0.1 | 0.794 | 0.796 | $-0.3\,\%$ |
| 0.5 | 0.755 | 0.757 | $-0.3\,\%$ |
| 1.0 | 0.804 | 0.809 | $-0.6\,\%$ |
| 3.0 | 1.280 | 1.316 | $-2.7\,\%$ |
| 6.0 | 3.040 | 3.22 | $-5.6\,\%$ |
| 10.0 | 10.65 | 10.4 | $+2.4\,\%$ |

Mean ionic activity coefficient of HCl, model vs experiment

###### Validation against Perry’s tables

Table [36](#tab:hcl_perry) compares the HCl partial pressure at 25 °C (Perry’s Table 2-10) and the density at 20 °C (Table 2-57) . At 1 atm the maximum-boiling azeotrope comes out at 18.9 wt% HCl and 107.75 °C, against 20.22 wt% and 108.58 °C measured.



<a id="tab:hcl_perry"></a>



| wt% HCl | $m$ (mol/kg) | $p_{\mathrm{HCl}}$ model (Pa) | $p_{\mathrm{HCl}}$ Perry (Pa) | $\rho$ model (kg/m$^{3}$) | $\rho$ Perry (kg/m$^{3}$) |
|---:|---:|---:|---:|---:|---:|
| 4 | 1.14 | 0.045 | 0.059 | 1017.5 | 1018.1 |
| 10 | 3.05 | 0.79 | 0.89 | 1047.4 | 1047.4 |
| 18 | 6.02 | 17.2 | 19.7 | 1087.6 | 1087.8 |
| 24 | 8.66 | 184 | 199 | 1118.0 | 1118.7 |
| 30 | 11.75 | 2472 | 2013 | 1148.6 | 1149.3 |

H$_2$O–HCl (Pitzer): HCl partial pressure at 25 °C and density at 20 °C, model vs Perry



###### Limitations

- The HCl partial pressure is about 22 % below the tabulated values at 6 wt% and less, and 23 % above them at 30 wt%.

- Above about 22 wt% the bubble points fall below the measured ones (26 wt%: 94.1 against 103.3 °C at 1 atm; Table [35](#tab:hcl_txy)).

- $\phi_{C_p}$ is held at its 25 °C value; heats of solution far from 25 °C, and above about 110 °C in particular, are extrapolations.

- Above 16 mol/kg the molality is clamped, and every more concentrated liquid behaves as 36.8 wt% acid.

- A spectator salt lowers the water activity through the ionic strength only. With salt in the acid, the vapour fraction jumps near the point where the liquid water boils away (20 wt% HCl with 1 mol/kg NaCl at 1 atm: from 0.234 to 0.969 within 0.006 K); a specification inside that jump stops with an error.

- A feed with a dissolved permanent gas has no bubble point; its bubble-point specifications use the default flash.

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

##### Overview {#overview-55}

The CO$_2$ Capture property package describes aqueous amine solvents loaded with $\ce{CO2}$, as found in the absorbers and strippers of post-combustion capture and gas treating units. It covers five solvents: monoethanolamine (MEA), diethanolamine (DEA), methyldiethanolamine (MDEA), piperazine (PZ), and the blend of MDEA with piperazine.

The flash works on the apparent compounds of the material stream: water, the amine, $\ce{CO2}$ and any other gases. Whenever the flash needs the fugacities of a liquid, the package splits that liquid into its true species (the molecules and ions in chemical equilibrium) and computes their activity coefficients with a generalized electrolyte NRTL model. At chemical equilibrium the chemical potential of an apparent compound equals that of its molecular form, so the fugacity of apparent $\ce{CO2}$ is the fugacity of $\ce{CO2(aq)}$ in the speciated liquid, and the same holds for water and the amine. Stream compositions stay on the apparent basis, and the phase equilibrium carries the chemistry. All parameters are compiled into the package; it reads no data files.

##### True Species

Table [37](#tab:cap_species) lists the true species of each solvent. MDEA is a tertiary amine and forms no carbamate. Piperazine is a diamine: besides the carbamate it forms the protonated carbamate $\mathrm{H^{+}PZCOO^{-}}$, a zwitterion that the model treats as a molecular solute, and the dicarbamate $\mathrm{PZ(COO^{-})_2}$. The diprotonated ion $\mathrm{PZH_2^{2+}}$ is left out, as in the models of Hilliard  and Frailie ; below 0.5 mol $\ce{CO2}$ per mol of alkalinity its amount is negligible. The MDEA + PZ blend carries the species and reactions of both amines in one liquid, with a mass balance for each amine.



<a id="tab:cap_species"></a>



| Solvent | True species |
|:---|:---|
| MEA | $\mathrm{H_2O}$, MEA, $\mathrm{CO_2(aq)}$, $\mathrm{MEAH^{+}}$, $\mathrm{MEACOO^{-}}$, $\mathrm{HCO_3^{-}}$, $\mathrm{CO_3^{2-}}$, $\mathrm{OH^{-}}$, $\mathrm{H^{+}}$ |
| DEA | $\mathrm{H_2O}$, DEA, $\mathrm{CO_2(aq)}$, $\mathrm{DEAH^{+}}$, $\mathrm{DEACOO^{-}}$, $\mathrm{HCO_3^{-}}$, $\mathrm{CO_3^{2-}}$, $\mathrm{OH^{-}}$, $\mathrm{H^{+}}$ |
| MDEA | $\mathrm{H_2O}$, MDEA, $\mathrm{CO_2(aq)}$, $\mathrm{MDEAH^{+}}$, $\mathrm{HCO_3^{-}}$, $\mathrm{CO_3^{2-}}$, $\mathrm{OH^{-}}$, $\mathrm{H^{+}}$ |
| PZ | $\mathrm{H_2O}$, PZ, $\mathrm{CO_2(aq)}$, $\mathrm{PZH^{+}}$, $\mathrm{PZCOO^{-}}$, $\mathrm{H^{+}PZCOO^{-}}$, $\mathrm{PZ(COO^{-})_2}$, $\mathrm{HCO_3^{-}}$, $\mathrm{CO_3^{2-}}$, $\mathrm{OH^{-}}$, $\mathrm{H^{+}}$ |
| MDEA + PZ | $\mathrm{H_2O}$, MDEA, PZ, $\mathrm{CO_2(aq)}$, $\mathrm{MDEAH^{+}}$, $\mathrm{PZH^{+}}$, $\mathrm{PZCOO^{-}}$, $\mathrm{H^{+}PZCOO^{-}}$, $\mathrm{PZ(COO^{-})_2}$, $\mathrm{HCO_3^{-}}$, $\mathrm{CO_3^{2-}}$, $\mathrm{OH^{-}}$, $\mathrm{H^{+}}$ |

True species of the CO$_2$ Capture package



##### Chemical Equilibria

The liquid holds the following reactions, where Am stands for the amine:


<a id="rxn:cap_r1"></a><a id="rxn:cap_r2"></a><a id="rxn:cap_r3"></a><a id="rxn:cap_r4"></a><a id="rxn:cap_r5"></a><a id="rxn:cap_r6"></a><a id="rxn:cap_r7"></a>

\[
\begin{alignat}
{2}
  \mathrm{H_2O}                     &\;\rightleftharpoons\; \mathrm{OH^{-} + H^{+}}
    &&\qquad  \\
  \mathrm{CO_2(aq) + H_2O}          &\;\rightleftharpoons\; \mathrm{HCO_3^{-} + H^{+}}
    &&\qquad  \\
  \mathrm{HCO_3^{-}}                &\;\rightleftharpoons\; \mathrm{CO_3^{2-} + H^{+}}
    &&\qquad  \\
  \mathrm{AmH^{+}}                  &\;\rightleftharpoons\; \mathrm{Am + H^{+}}
    &&\qquad \text{(each amine)}  \\
  \mathrm{AmCOO^{-} + H_2O}         &\;\rightleftharpoons\; \mathrm{Am + HCO_3^{-}}
    &&\qquad \text{(MEA, DEA, PZ)}  \\
  \mathrm{H^{+}PZCOO^{-}}           &\;\rightleftharpoons\; \mathrm{PZCOO^{-} + H^{+}}
    &&\qquad \text{(PZ)}  \\
  \mathrm{PZ(COO^{-})_2 + H_2O}     &\;\rightleftharpoons\; \mathrm{PZCOO^{-} + HCO_3^{-}}
    &&\qquad \text{(PZ)}
\end{alignat}
\]


The equilibrium constants are thermodynamic constants on the mole-fraction scale:


<a id="eq:cap_keq"></a>

\[
\ln K_{x,r}(T) = \sum_{i} \nu_{i,r}\,\ln\!\left(x_{i}\,\gamma_{i}\right)
\]


where $\nu_{i,r}$ is the stoichiometric coefficient of true species $i$ in reaction $r$. Water and the amine are referred to the pure liquid; $\ce{CO2(aq)}$, $\mathrm{H^{+}PZCOO^{-}}$ and the ions are referred to infinite dilution in water ($\gamma_{i}^{*} \to 1$ as $x_{\ce{H2O}} \to 1$).

Most published constants are on the molality scale with every solute at infinite dilution. The package converts them with


<a id="eq:cap_kx_km"></a>

\[
\ln K_{x} = \ln K_{m} + \Delta\nu_{s}\,\ln M_{w}
\]


where $\Delta\nu_{s}$ is the number of solute species formed less the number consumed (water excluded) and $M_{w} = 0.018015$ kg/mol. For the protonation constants of the amines, reaction [\[rxn:cap_r4\]](#rxn:cap_r4), the amine is then moved to its pure-liquid reference:


<a id="eq:cap_k4"></a>

\[
\ln K_{x,4} = \ln K_{m,4} + \ln M_{w} + \ln\gamma_{\mathrm{Am}}^{\infty}
\]


with $\gamma_{\mathrm{Am}}^{\infty}$ the infinite-dilution activity coefficient of the amine in water from the water–amine NRTL pair of the model itself, so that the constant and the binary parameters stay consistent. Table [38](#tab:cap_constants) lists the constants and their sources.

The three piperazine carbamate constants come from the $^{1}$H NMR measurements of Ermatchkov, Pérez-Salado Kamps and Maurer  (283 to 333 K), as tabulated by Cullinane  on the mole-fraction scale with every solute at infinite dilution in water. They are given at 313.15 K with a van ’t Hoff temperature dependence:


<a id="eq:cap_vanthoff"></a>

\[
\ln K(T) = \ln K(313.15\,\mathrm{K})
    - \frac{\Delta H}{R}\left(\frac{1}{T} - \frac{1}{313.15\,\mathrm{K}}\right)
\]


The values at 313.15 K are those of the source. The reaction enthalpies were shifted by small amounts in the fit of the pair parameters to the $\ce{CO2}$ partial pressures and heats up to 150 °C, since the NMR data stop at 333 K. Reactions [\[rxn:cap_r5\]](#rxn:cap_r5) and [\[rxn:cap_r7\]](#rxn:cap_r7) for PZ are obtained by combining these constants with reaction [\[rxn:cap_r2\]](#rxn:cap_r2), and PZ is moved to its pure-liquid reference with $\ln\gamma_{\mathrm{PZ}}^{\infty}$ as in Eq. [\[eq:cap_k4\]](#eq:cap_k4).



<a id="tab:cap_constants"></a>



| Reaction | Correlation | Source |
|:---|:---|:---|
| [\[rxn:cap_r1\]](#rxn:cap_r1) | $\ln K_{m} = 140.932 - 13445.9/T - 22.4773\ln T$ | Edwards et al.  |
| [\[rxn:cap_r2\]](#rxn:cap_r2) | $\ln K_{m} = 235.482 - 12092.1/T - 36.7816\ln T$ | Edwards et al.  |
| [\[rxn:cap_r3\]](#rxn:cap_r3) | $\ln K_{m} = 220.067 - 12431.7/T - 35.4819\ln T$ | Edwards et al.  |
| [\[rxn:cap_r4\]](#rxn:cap_r4), MEA | $\mathrm{p}K_{m} = 2677.91/T + 0.3869 + 0.0004277\,T$ | Bates and Pinching , Eq. 10 |
| [\[rxn:cap_r4\]](#rxn:cap_r4), DEA | $\ln K_{m} = -26.5 - 4035/T + 3.44\ln T$ | Posey , Table 5.8 |
| [\[rxn:cap_r4\]](#rxn:cap_r4), MDEA | $\ln K_{m} = -59.55 - 1709/T + 8.01\ln T$ | Posey , Table 5.8 |
| [\[rxn:cap_r4\]](#rxn:cap_r4), PZ | $\mathrm{p}K_{m} = 9.86$ at 20 °C, dissociation enthalpy 42.87 kJ/mol at 25 °C, $\Delta C_{p} = 75$ J/(mol K) | Hetzer, Robinson and Bates , as summarized by Cullinane , Table 5.9 |
| [\[rxn:cap_r5\]](#rxn:cap_r5), MEA | $\ln K_{x} = 2.8898 - 3635.09/T$ | Austgen , Table 4.1 (25 to 120 °C) |
| [\[rxn:cap_r5\]](#rxn:cap_r5), DEA | $\ln K_{x} = 4.5146 - 3417.34/T$ | Austgen , Table 4.1 (25 to 120 °C) |
| $\mathrm{PZ + CO_2(aq)} \rightleftharpoons \mathrm{PZCOO^{-} + H^{+}}$ | $K_{x}(313.15\,\mathrm{K}) = 9.24\times10^{-6}$; $\Delta H = -26.64$ kJ/mol (source $-25.3$) | Ermatchkov et al. , via Cullinane , Table 5.12 |
| [\[rxn:cap_r6\]](#rxn:cap_r6) | $K_{x}(313.15\,\mathrm{K}) = 1.13\times10^{-11}$; $\Delta H = +33.72$ kJ/mol (source $+29.0$) | same |
| $\mathrm{PZCOO^{-} + CO_2(aq)} \rightleftharpoons \mathrm{PZ(COO^{-})_2 + H^{+}}$ | $K_{x}(313.15\,\mathrm{K}) = 8.86\times10^{-7}$; $\Delta H = -7.12$ kJ/mol (source $-6.2$) | same |

Equilibrium constants of the CO$_2$ Capture package ($T$ in K). $K_{m}$: molality scale, converted with Eqs. [\[eq:cap_kx_km\]](#eq:cap_kx_km) and [\[eq:cap_k4\]](#eq:cap_k4); $K_{x}$: mole-fraction scale.



##### Activity Coefficients

The activity coefficients of the true species come from the electrolyte NRTL model in the generalized form of Chen and Song , written for a mixed solvent. The excess Gibbs energy is the sum of a long-range and a short-range term:


<a id="eq:cap_gex"></a>

\[
\frac{G^{\mathrm{ex}}}{RT} = \frac{G^{\mathrm{ex,PDH}}}{RT} + \frac{G^{\mathrm{ex,lc}}}{RT}
\]


###### Long-range term

The Pitzer–Debye–Hückel term is the one of the eNRTL package (Section [6.5](#sec:enrtl)), with water as the solvent:


<a id="eq:cap_pdh"></a>

\[
\frac{G^{\mathrm{ex,PDH}}}{RT} = -\Big(\sum_{k} n_{k}\Big)
    \left(\frac{1}{M_{w}}\right)^{1/2}
    \frac{4A_{\varphi}I_{x}}{\rho}\,\ln\!\left(1 + \rho I_{x}^{1/2}\right),
  \qquad
  I_{x} = \frac{1}{2}\sum_{i} x_{i} z_{i}^{2}
\]


with closest-approach parameter $\rho = 14.9$ and the Debye–Hückel parameter $A_{\varphi}$ computed from the dielectric constant and density of water at the system temperature. The Born term for the change of permittivity between water and the mixed solvent is not included.

###### Short-range term

The local-composition term has one cell for each molecular species $m$ (water, the amine, $\ce{CO2(aq)}$ and, for PZ, $\mathrm{H^{+}PZCOO^{-}}$) and one for each ion. With $X_{j} = x_{j}C_{j}$ ($C_{j} = |z_{j}|$ for ions and 1 for molecules), the molecular cells contribute


<a id="eq:cap_lc_mol"></a>

\[
\frac{G^{\mathrm{ex,lc}}_{\mathrm{mol}}}{RT} = \sum_{m} n_{m}\,
    \frac{\sum_{j} X_{j}G_{jm}\tau_{jm}}{\sum_{j} X_{j}G_{jm}}
\]


where $j$ runs over all species. For a molecule–molecule pair, $G_{jm} = \exp(-\alpha\tau_{jm})$. For an ion around a molecule, the parameters are averages over the ion pairs: $G_{cm} = \sum_{a} Y_{a}G_{ca,m}$ and $\tau_{cm} = -\ln(G_{cm})/\alpha$, with $Y_{a}$ the charge fraction of anion $a$ among the anions (and the same for anions with the cation fractions). The cation and anion cells follow Chen and Song ; for a cation $c$,


<a id="eq:cap_lc_cat"></a>

\[
\frac{G^{\mathrm{ex,lc}}_{c}}{RT} = z_{c}n_{c}\sum_{a'} Y_{a'}\,
    \frac{\sum_{m} X_{m}G_{m c,a'c}\tau_{m c,a'c}}
         {\sum_{m} X_{m}G_{m c,a'c} + \sum_{a} X_{a}},
  \qquad
  \tau_{m c,a'c} = \tau_{cm} - \tau_{ca',m} + \tau_{m,ca'}
\]


and the anion cells are written the same way. The non-randomness factor is $\alpha = 0.2$ for every pair. The ions, $\ce{CO2(aq)}$ and $\mathrm{H^{+}PZCOO^{-}}$ are referred to infinite dilution in water by subtracting from $G^{\mathrm{ex}}$ the part linear in their amounts at infinite dilution in pure water. The activity coefficients are the derivatives $\ln\gamma_{i} = \partial(G^{\mathrm{ex}}/RT)/\partial n_{i}$, taken exactly in one pass over the true amounts, so they satisfy the Gibbs–Duhem equation.

Every pair parameter has the form


<a id="eq:cap_tau"></a>

\[
\tau = a + b\left(\frac{1}{T} - \frac{1}{298.15\,\mathrm{K}}\right)
\]


except the water–PZ pair, which uses $\tau = a + d\,T$ as published by Hilliard . Table [39](#tab:cap_tau) lists the parameters and the data each set was fitted to. The water–amine pairs of MEA and MDEA were fitted to amine + water vapour–liquid equilibrium; the water–DEA and water–PZ pairs are published values used as they are. The molecule–ion-pair parameters were fitted to $\ce{CO2}$ partial pressures and calorimetric heats of absorption together. Every pair not listed takes the defaults of Chen and Evans : $\tau_{m,ca} = 8$ and $\tau_{ca,m} = -4$ for molecule–ion pairs, zero for molecule–molecule pairs (water–$\ce{CO2}$ and amine–$\ce{CO2}$ included).



<a id="tab:cap_tau"></a>



<table>
<caption>Electrolyte NRTL parameters of the CO<span class="math inline">\(_2\)</span> Capture package, <span class="math inline">\(\tau = a + b\,(1/T - 1/298.15\,\mathrm{K})\)</span>. “Water, (MEAH<span class="math inline">\(^{+}\)</span>, MEACOO<span class="math inline">\(^{-}\)</span>)” is <span class="math inline">\(\tau_{m,ca}\)</span> of water around the ion pair; the reverse order is <span class="math inline">\(\tau_{ca,m}\)</span>.</caption>
<thead>
<tr>
<th style="text-align: left;">Pair</th>
<th style="text-align: right;"><span class="math inline">\(a\)</span></th>
<th style="text-align: right;"><span class="math inline">\(b\)</span> (K)</th>
<th style="text-align: left;">Fitted to</th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">Pair</td>
<td style="text-align: right;"><span class="math inline">\(a\)</span></td>
<td style="text-align: right;"><span class="math inline">\(b\)</span> (K)</td>
<td style="text-align: left;">Fitted to</td>
</tr>
<tr>
<td style="text-align: left;"></td>
<td style="text-align: right;"></td>
<td style="text-align: right;"></td>
<td style="text-align: left;"></td>
</tr>
<tr>
<td style="text-align: left;">water, MEA (water around MEA)</td>
<td style="text-align: right;"><span class="math inline">\(0.8270\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1616.5\)</span></td>
<td style="text-align: left;">total pressure of MEA + water, 283–363 K <span class="citation" data-cites="Belabbaci2009"></span>; total pressure and MEA in the vapour, 313–373 K <span class="citation" data-cites="KimSvendsen2008"></span></td>
</tr>
<tr>
<td style="text-align: left;">MEA, water</td>
<td style="text-align: right;"><span class="math inline">\(-1.6974\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1422.8\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (MEAH<span class="math inline">\(^{+}\)</span>, MEACOO<span class="math inline">\(^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(8.6390\)</span></td>
<td style="text-align: right;"><span class="math inline">\(4330.6\)</span></td>
<td style="text-align: left;"><span class="math inline">\(\ce{CO2}\)</span> partial pressures <span class="citation" data-cites="Jou1995 Wagner2013"></span>; integral heats of solution <span class="citation" data-cites="Arcis2011"></span></td>
</tr>
<tr>
<td style="text-align: left;">(MEAH<span class="math inline">\(^{+}\)</span>, MEACOO<span class="math inline">\(^{-}\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-4.3474\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1619.4\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (MEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(6.6966\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-3457.6\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">(MEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-3.2317\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1691.2\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;"></td>
<td style="text-align: right;"></td>
<td style="text-align: right;"></td>
<td style="text-align: left;"></td>
</tr>
<tr>
<td style="text-align: left;">water, DEA (water around DEA)</td>
<td style="text-align: right;"><span class="math inline">\(4.7593\)</span></td>
<td style="text-align: right;"><span class="math inline">\(175.1\)</span></td>
<td style="text-align: left;">taken from Posey <span class="citation" data-cites="Posey1996"></span>, Table 3.8 (<span class="math inline">\(\tau = 4.172 + 175.1/T\)</span>)</td>
</tr>
<tr>
<td style="text-align: left;">DEA, water</td>
<td style="text-align: right;"><span class="math inline">\(-3.4116\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-546.4\)</span></td>
<td style="text-align: left;">taken from Posey <span class="citation" data-cites="Posey1996"></span>, Table 3.8 (<span class="math inline">\(\tau = -1.579 - 546.4/T\)</span>)</td>
</tr>
<tr>
<td style="text-align: left;">water, (DEAH<span class="math inline">\(^{+}\)</span>, DEACOO<span class="math inline">\(^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(11.5183\)</span></td>
<td style="text-align: right;"><span class="math inline">\(554.4\)</span></td>
<td style="text-align: left;"><span class="math inline">\(\ce{CO2}\)</span> partial pressures <span class="citation" data-cites="Ghalib2016 Suleman2016"></span>; integral heats of solution <span class="citation" data-cites="Arcis2012"></span></td>
</tr>
<tr>
<td style="text-align: left;">(DEAH<span class="math inline">\(^{+}\)</span>, DEACOO<span class="math inline">\(^{-}\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-5.4626\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-458.2\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (DEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(8.2991\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-4494.8\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">(DEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-4.0348\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1674.2\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;"></td>
<td style="text-align: right;"></td>
<td style="text-align: right;"></td>
<td style="text-align: left;"></td>
</tr>
<tr>
<td style="text-align: left;">water, MDEA (water around MDEA)</td>
<td style="text-align: right;"><span class="math inline">\(-2.8375\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-2679.1\)</span></td>
<td style="text-align: left;">total pressure, 313–373 K <span class="citation" data-cites="KimSvendsen2008"></span>; bubble temperature and MDEA in the vapour, 5 to 40 kPa <span class="citation" data-cites="Soames2018"></span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA, water</td>
<td style="text-align: right;"><span class="math inline">\(2.5972\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2415.3\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (MDEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(8.3416\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3958.8\)</span></td>
<td style="text-align: left;"><span class="math inline">\(\ce{CO2}\)</span> partial pressures <span class="citation" data-cites="Dey2018 Najafloo2015 Xiao2018 Shokouhi2015"></span>; integral heats of solution <span class="citation" data-cites="Arcis2008 Arcis2009"></span></td>
</tr>
<tr>
<td style="text-align: left;">(MDEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-4.0854\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1532.9\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">MDEA, (MDEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(14.6616\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">(MDEAH<span class="math inline">\(^{+}\)</span>, HCO<span class="math inline">\(_3^{-}\)</span>), MDEA</td>
<td style="text-align: right;"><span class="math inline">\(3.9382\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;"></td>
<td style="text-align: right;"></td>
<td style="text-align: right;"></td>
<td style="text-align: left;"></td>
</tr>
<tr>
<td style="text-align: left;">water, PZ (water around PZ)</td>
<td colspan="2" style="text-align: left;"><span class="math inline">\(-7.68 + 0.0107\,T\)</span></td>
<td style="text-align: left;">taken from Hilliard <span class="citation" data-cites="Hilliard2008"></span>, Table 9.3-4</td>
</tr>
<tr>
<td style="text-align: left;">PZ, water</td>
<td colspan="2" style="text-align: left;"><span class="math inline">\(-6.42 + 0.0249\,T\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (PZH<span class="math inline">\(^{+}\)</span>, PZCOO<span class="math inline">\(^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(6.7270\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;"><span class="math inline">\(\ce{CO2}\)</span> partial pressures <span class="citation" data-cites="Dugas2011 Ermatchkov2006 Xu2011"></span>; differential heats of absorption measured by Kim (2007) and tabulated by Hilliard <span class="citation" data-cites="Hilliard2008"></span>; fitted together with the three carbamate enthalpy shifts</td>
</tr>
<tr>
<td style="text-align: left;">(PZH<span class="math inline">\(^{+}\)</span>, PZCOO<span class="math inline">\(^{-}\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-3.8619\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (PZH<span class="math inline">\(^{+}\)</span>, PZ(COO<span class="math inline">\(^{-}\)</span>)<span class="math inline">\(_2\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(8.3952\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">(PZH<span class="math inline">\(^{+}\)</span>, PZ(COO<span class="math inline">\(^{-}\)</span>)<span class="math inline">\(_2\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-5.0428\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;"></td>
<td style="text-align: right;"></td>
<td style="text-align: right;"></td>
<td style="text-align: left;"></td>
</tr>
<tr>
<td style="text-align: left;">MDEA, PZ (MDEA around PZ)</td>
<td style="text-align: right;"><span class="math inline">\(-0.0629\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;"><span class="math inline">\(\ce{CO2}\)</span> partial pressures over 7 m MDEA/2 m PZ and 5 m MDEA/5 m PZ <span class="citation" data-cites="ChenX2011"></span>; PZ and MDEA partial pressures over the same blends <span class="citation" data-cites="Nguyen2013"></span></td>
</tr>
<tr>
<td style="text-align: left;">PZ, MDEA</td>
<td style="text-align: right;"><span class="math inline">\(-5.7042\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (MDEAH<span class="math inline">\(^{+}\)</span>, PZCOO<span class="math inline">\(^{-}\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(7.0549\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">(MDEAH<span class="math inline">\(^{+}\)</span>, PZCOO<span class="math inline">\(^{-}\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-4.6114\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">water, (MDEAH<span class="math inline">\(^{+}\)</span>, PZ(COO<span class="math inline">\(^{-}\)</span>)<span class="math inline">\(_2\)</span>)</td>
<td style="text-align: right;"><span class="math inline">\(10.3464\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
<tr>
<td style="text-align: left;">(MDEAH<span class="math inline">\(^{+}\)</span>, PZ(COO<span class="math inline">\(^{-}\)</span>)<span class="math inline">\(_2\)</span>), water</td>
<td style="text-align: right;"><span class="math inline">\(-5.3367\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0\)</span></td>
<td style="text-align: left;">same</td>
</tr>
</tbody>
</table>



###### Notes on the binary pairs

The water–PZ pair of Hilliard reproduces the PZ partial pressures over unloaded PZ measured by Hilliard  (0.9 to 5 mol/kg, 33 to 63 °C) within 24 % and those of Nguyen  within 12 and 21 %, none of which were fitted. No open DEA + water vapour–liquid data in the range of the model were at hand: Austgen  and Posey  fitted their water–DEA pairs mainly to unpublished total pressures. The pair of Posey reproduces the excess enthalpies of DEA + water measured by Li et al.  to 5.2 % on average and gives a better $\ce{CO2}$ fit than the pair of Austgen, so the package uses it.

##### Phase Equilibrium and Flash

The liquid fugacities of the apparent compounds are those of their molecular species in the speciated liquid:


<a id="eq:cap_fw"></a><a id="eq:cap_fa"></a><a id="eq:cap_fc"></a>

\[
\begin{align}
  f_{\ce{H2O}}^{L} &= x_{\ce{H2O}}\,\gamma_{\ce{H2O}}\,P_{\ce{H2O}}^{\mathrm{sat}}(T)
     \\
  f_{\mathrm{Am}}^{L} &= x_{\mathrm{Am}}\,\gamma_{\mathrm{Am}}\,P_{\mathrm{Am}}^{\mathrm{sat}}(T)
     \\
  f_{\ce{CO2}}^{L} &= x_{\ce{CO2(aq)}}\,\gamma_{\ce{CO2(aq)}}^{*}\,H_{\ce{CO2}}(T)\,
    \exp\!\left[\frac{\bar{v}_{\ce{CO2}}^{\infty}\,(P - P_{\ce{H2O}}^{\mathrm{sat}})}{RT}\right]
\end{align}
\]


where $x_{i}$ and $\gamma_{i}$ are the true mole fraction and activity coefficient. $P^{\mathrm{sat}}$ is the vapour pressure of the pure liquid; for the amines it is the liquid vapour pressure also below the melting point (piperazine melts at 106 °C, DEA at 28 °C). The Henry constant of $\ce{CO2}$ in water is that of Carroll, Slupsky and Mather ,


<a id="eq:cap_henry"></a>

\[
\ln\frac{H_{\ce{CO2}}}{\mathrm{MPa}} = -6.8346 + \frac{1.2817\times10^{4}}{T}
    - \frac{3.7668\times10^{6}}{T^{2}} + \frac{2.997\times10^{8}}{T^{3}}
\]


and $\bar{v}_{\ce{CO2}}^{\infty} = 34$ cm$^{3}$/mol is the partial molar volume of $\ce{CO2}$ at infinite dilution in water. The fugacity coefficient the flash uses is $\varphi_{i}^{L} = f_{i}^{L}/(x_{i}^{\mathrm{app}}P)$, with $x_{i}^{\mathrm{app}}$ the apparent mole fraction. The vapour phase follows the Peng–Robinson equation of state .

The speciation solves the equilibrium equations [\[eq:cap_keq\]](#eq:cap_keq) together with the balances of each amine, carbon and oxygen and electroneutrality, by Newton’s method on the logarithms of the true amounts (8 to 13 unknowns), with the derivatives of the activity coefficients in the Jacobian and a line search. Each speciation starts from the nearest of the recent ones. Gases other than $\ce{CO2}$ ($\ce{N2}$, $\ce{O2}$, $\ce{H2S}$ and so on) take part in no reaction: they dissolve physically (Henry’s law for $\ce{H2S}$ and the supercritical gases) and only dilute the true species. The package therefore does not describe $\ce{H2S}$ treating.

The PT flash solves $\ln K_{i} = \ln\varphi_{i}^{L}(\boldsymbol{x})
- \ln\varphi_{i}^{V}(\boldsymbol{y})$ with Newton’s method on $\ln K$ after a few damped substitution steps. Near a loading of 0.5 mol/mol the $\ce{CO2}$ partial pressure rises tenfold within a few hundredths of loading, and plain successive substitution oscillates there. The Rachford–Rice equation is solved as a negative flash, so the PV and TV flashes locate the bubble or dew point by bracketing on a continuous vapour fraction. The PH and PS flashes call the PT flash; for MDEA + PZ they bracket the temperature directly, because the steep boiling curve of the blend near its bubble point could stop the standard PH algorithm at a wrong temperature. A trial liquid in which $\ce{CO2}$ is more than half of water + amine + $\ce{CO2}$ lies outside the model and takes the molecular fugacities described below for streams without an amine.

##### Enthalpy and Entropy

The partial molar enthalpy of a compound relative to its ideal gas follows from the temperature dependence of its liquid fugacity:


<a id="eq:cap_hbar"></a>

\[
\bar{H}_{i} - H_{i}^{\mathrm{ig}} = -RT^{2}
    \left(\frac{\partial\ln f_{i}^{L}}{\partial T}\right)_{P,\boldsymbol{x}^{\mathrm{app}}}
\]


For $\ce{CO2}$, kept on its ideal-gas reference, this term holds the heat of physical solution, the enthalpies of the reactions (through the temperature slopes of their constants) and the excess enthalpy of the electrolyte NRTL model, so the heat of absorption comes out of the enthalpy balance of any unit operation. Water and the amine keep the heat of vaporization of the package for the $\partial\ln P^{\mathrm{sat}}/\partial T$ part and add $-RT^{2}\,\partial\ln(x\gamma)/\partial T$ of their true species. The derivatives are central differences on speciations at $T \pm 0.01$ K. The entropy is consistent with the enthalpy through $G^{R} = RT\sum_{i} x_{i}^{\mathrm{app}}\ln(f_{i}/f_{i}^{\mathrm{ref}})$ with the same reference fugacities.

A liquid without an amine takes the enthalpy of the electrolyte packages (ideal gas less the heat of vaporization), plus the excess enthalpy of the brine when that option is on (Section [6.17](#sec:ccus_storage)). The vapour enthalpy and entropy are the ideal-gas values plus the residual of the Peng–Robinson vapour root, with the same $k_{ij}$ as the vapour fugacities: for $\ce{CO2}$ at 40 °C and 30 bar the residual enthalpy is $-28.9$ kJ/kg (CoolProp $-28.1$), and a $\ce{CO2}$ compressor from 1.5 to 30 bar at 75 % efficiency takes 1425.4 kW (CoolProp 1423.8 kW).

##### Liquid Density

The liquid density comes from a molar volume on apparent mole fractions:


<a id="eq:cap_density"></a>

\[
V = x_{W}V_{W}(T) + x_{A}V_{A}(T) + x_{W}x_{A}(k_{0} + k_{1}\theta)
      + x_{C}(k_{2} + k_{3}\theta),
  \qquad \theta = T - 298.15\,\mathrm{K},
  \qquad \rho = M/V
\]


where W, A and C stand for water, the amine and $\ce{CO2}$, $V_{A} = A_{0} + A_{1}\theta$ and $V_{W}$ is a quadratic fit to the IAPWS-95 density of water at 0.1 MPa . The apparent molar volume of $\ce{CO2}$ is close to zero (about 0.4 cm$^{3}$/mol for MEA): the carbamate and bicarbonate it forms take almost no room, which is why a loaded solvent is denser than a lean one. The parameters were fitted to Amundsen, Øi and Eimer  for MEA, Han et al.  for DEA and MDEA, and Freeman and Rochelle  with Samanta and Bandyopadhyay  for PZ. For piperazine, $V_{A}$ and $k_{0}$ are fitted constants of the aqueous solution and do not represent the volume of liquid piperazine. The MDEA + PZ blend takes the terms of each amine, the $\ce{CO2}$ term weighted by the amine mole fractions, and one cross term $x_{\mathrm{MDEA}}x_{\mathrm{PZ}}k_{MP}$ fitted to lean blends . Any other dissolved compound adds its own liquid molar volume.

##### pH and Ionic Strength

The pH and the ionic strength of a liquid come from its speciation. The pH is on the molality scale,


<a id="eq:cap_ph"></a>

\[
\mathrm{pH} = -\log_{10}\!\left(\frac{x_{\ce{H+}}\,\gamma_{\ce{H+}}^{*}}{M_{w}}\right)
\]


with the activity coefficient of the model taken from the mole-fraction to the molality scale.

##### Scope and Limitations

The package detects the amine by its CAS number, then by its name (case, spaces and punctuation ignored). Depending on the compound list it takes one of three paths:

- Water with MEA, DEA, MDEA, PZ, or MDEA with PZ (no ions or salts): the electrolyte NRTL model with chemical equilibrium described above.

- Water and gases without an amine: a molecular flash, with the vapour pressure for water, Henry’s law for $\ce{CO2}$ and $\ce{H2S}$ (also below their critical temperature when water is present) and Peng–Robinson for the vapour.

- Water with ions or salts and no amine: the electrolyte speciation of the base electrolyte package.

The package refuses, with a message in the flash, AMP and every amine blend other than MDEA + PZ, and any amine together with ions or salts (heat-stable salts, $\ce{Na+}$, $\ce{Cl-}$ and so on). The model has no parameters for other ions, and the ion speciation of the base package carries no amine chemistry consistent with it.

The main limitations are:

- **Fitted ranges.** MEA: 15 and 30 mass % (2.8 to 7.3 mol/kg), 313 to 393 K. DEA: 2 and 4 M, 303 to 353 K. MDEA: 298 to 363 K, with the heats at 322.5 and 372.9 K; above 100 °C the solubility rests on the protonation constant of Posey (data to 150 °C) and on the heats. PZ: 1 to 12 mol/kg, 313 to 423 K. MDEA + PZ: the cross parameters rest on two compositions (7 m/2 m and 5 m/5 m) from 40 to 100 °C; above 100 °C the 7/2 blend comes out about a factor of 2 below Xu . $\ce{CO2}$ partial pressures up to 1.2 MPa (MEA up to 500 kPa). Above 120 °C the MEA and DEA carbamate constants of Austgen extrapolate.

- **Solid piperazine.** The solid PZ hexahydrate (and, in very rich solutions, the hydrate of $\mathrm{H^{+}PZCOO^{-}}$) is not modelled. Freeman  finds 8 to 10 mol/kg PZ insoluble below about 0.22 to 0.25 mol $\ce{CO2}$ per mol of alkalinity at 21 °C and below about 0.05 at 40 °C. A flash in those regions returns a liquid that would in fact precipitate.

- **Heat of absorption of PZ at 120 °C.** The 393 K differential heats of Kim (2007) lie 25 to 35 kJ/mol above the model, while his 313 and 353 K heats and the temperature slope of the solubility data put the heat near 70 to 82 kJ/mol. The model heat of 8 mol/kg PZ at 0.5 mol/mol is 80, 73, 67 and 63 kJ/mol at 40, 80, 120 and 150 °C.

- **MDEA kinetics.** The flash is an equilibrium flash. The slow reaction of $\ce{CO2}$ with MDEA in a real absorber is not modelled; a rate-based column model has to supply it.

- **Water–DEA pair.** The pair comes from Posey, who fitted it to unpublished total pressures; the only open check is the excess enthalpy of DEA + water. The total pressures of Shin and Kim  at 393 K imply a water activity coefficient of 2.5 at $x_{\ce{H2O}} = 0.23$, where both published water–DEA pairs give 0.48 to 0.5, and were not used.

- **MEA heats.** The differential heat at 313 K comes out up to 13 kJ/mol more exothermic than the calorimetry of Mondal et al.  between 0.2 and 0.45 mol/mol, while the integral heats of Arcis et al. at 322.5 K, which the fit follows, lie at $-88$ to $-97$ kJ/mol.

- **Amine volatility.** No measured MEA volatility over loaded solutions was at hand; MEA in the vapour follows from the water–MEA parameters and the speciation (about 1.9 Pa over 30 mass % MEA at 0.4 mol/mol and 40 °C). MDEA in the vapour over MDEA-rich mixtures comes out about 50 % above the data of Soames et al. (Table [48](#tab:capture_amine_volatility)).

- **Speed.** The PZ speciation, with eleven true species, takes about twice the time of MEA; the MDEA + PZ blend, with thirteen, about three times that of MDEA alone.

##### Validation

Tables [43](#tab:capture_co2_solubility) to [52](#tab:capture_density) compare the package with experimental data. They were produced by the script `validate.py` in the fitting folder of the package source (`Capture/Fitting`), which calls the shipped package over every data point: liquid fugacities, enthalpies and densities at the apparent composition of each point, without a flash. The same folder holds the data files (one per source, with the citation, DOI and the use of each row), the fit scripts and the point-by-point results.

The *Use* column tells how each data set entered the package: *fit* means the parameters were regressed to it, *check* means it was only compared. The measured $\ce{CO2}$ partial pressure is taken to a fugacity with the second virial coefficient of $\ce{CO2}$ and compared with the $\ce{CO2}$ fugacity of the liquid; for data sets that report the total pressure, the $\ce{CO2}$ partial pressure is the total pressure less the water partial pressure from Raoult’s law. The loading column gives the mean difference between the measured loading and the loading at which the package meets the measured partial pressure. The integral heat of solution is the enthalpy of the loaded liquid less that of the lean liquid and of the $\ce{CO2}$ gas at $T$ and $P$, per mol of $\ce{CO2}$; the differential heat is the same over a loading step of 0.02 mol/mol.

Some points were left out: the data of Wagner et al. above 500 kPa of $\ce{CO2}$, the other $\ce{CO2}$ data above 1.2 MPa, the PZ and MDEA + PZ data above 150 °C, and the heats of Arcis et al. past the solubility limit of each run (with the MEA runs near 5 MPa at 372.9 K).

Several data sets disagree with the others, and the fit follows the majority:

- MDEA: the sets of Dey et al., Zoghi et al. and Harris et al. lie a factor of 2 to 8 away from the others at the same concentration and temperature. At 313 K and about 0.47 mol/mol, Dey et al. give 8 kPa in 30 mass % MDEA where Leontiadis et al. give 20 kPa in 23.4 mass %.

- PZ: the sets of Bougie and Iliuta, Dash et al. and Suleman et al. differ from the University of Texas and Maurer data by one to three orders of magnitude in $\ce{CO2}$ partial pressure at the same loading (for 4.35 mol/kg PZ at 0.77 mol/mol and 308 K, 1048 kPa against about 3 kPa from the model, which follows Dugas and Rochelle there).

- DEA: Ghalib et al. and Suleman et al. disagree with each other between 0.5 and 0.8 mol/mol at 313 K; the model lies up to 70 % above the first and up to 30 % below the second. The points of Han and Wee, read as grams of $\ce{CO2}$ per kg of solvent, lie 0.1 to 0.15 mol/mol below the model above 20 mass % DEA.

- MEA: near 0.5 mol/mol, Jou et al. and Wagner et al. disagree by a factor of about 2 at 313 K; the model lies between them.

The short names in the tables refer to the following sources. $\ce{CO2}$ solubility: Jou 1995 , Tong 2012 , Wagner 2013 , Dugas 2011 , Li 2015 , Bernhardsen 2019 , Ghalib 2016 , Suleman 2016 , Dash 2011 (DEA) , Han 2017 , Dey 2018 , Najafloo 2015 , Xiao 2018 , Shokouhi 2015 , Leontiadis 2019 , Zoghi 2012 , Shirazizadeh 2019 , Harris 2009 , Sairi 2015 , Arcis 2008, 2009 , Ermatchkov 2006 , Xu 2011 , Hilliard 2008 , Bougie 2011 , Dash 2011 (PZ) , Chen 2011 . Heats: Arcis 2008 to 2012 , Mondal 2017 , Ojala 2014 , and Kim 2007, tabulated by Hilliard . Amine and water: Belabbaci 2009 , Kim 2008 , Soames 2018 , Barreau 2007 , Li 2015 , Shin 2023 , Hilliard 2008 , Nguyen 2013 . Density: Amundsen 2009 , Han 2012 , Jayarathna 2012 , Freeman 2011 , Samanta 2006 , Muhammad 2009 , Derks 2005 , Derks 2008 , Speyer 2010 , Paul 2006 , Böttger 2009 , Kessler 2019 , Frailie 2014 .



<a id="tab:capture_co2_solubility"></a>



<table>
<caption>CO<span class="math inline">\(_2\)</span> partial pressure over aqueous amines loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data. pCO<span class="math inline">\(_2\)</span>: 100 (calc/exp - 1) at the measured loading; <span class="math inline">\(|\ln|\)</span>: mean <span class="math inline">\(|\ln(\mathrm{calc}/\mathrm{exp})|\)</span>; loading: mean |calc - exp| of the loading at which the package meets the measured pCO<span class="math inline">\(_2\)</span>. Loadings in mol CO<span class="math inline">\(_2\)</span> per mol of amine (MDEA + PZ: per mol of MDEA + PZ).</caption>
<thead>
<tr>
<th style="text-align: left;">System</th>
<th style="text-align: left;">Source</th>
<th style="text-align: left;">Conditions</th>
<th style="text-align: center;">Use</th>
<th style="text-align: right;"><span class="math inline">\(n\)</span></th>
<th style="text-align: right;"><div id="tab:capture_co2_solubility">
<table>
<caption>CO<span class="math inline">\(_2\)</span> partial pressure over aqueous amines loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data. pCO<span class="math inline">\(_2\)</span>: 100 (calc/exp - 1) at the measured loading; <span class="math inline">\(|\ln|\)</span>: mean <span class="math inline">\(|\ln(\mathrm{calc}/\mathrm{exp})|\)</span>; loading: mean |calc - exp| of the loading at which the package meets the measured pCO<span class="math inline">\(_2\)</span>. Loadings in mol CO<span class="math inline">\(_2\)</span> per mol of amine (MDEA + PZ: per mol of MDEA + PZ).</caption>
<tbody>
<tr>
<td style="text-align: right;"><span class="math inline">\(p_{\mathrm{CO_2}}\)</span> AAD</td>
</tr>
<tr>
<td style="text-align: right;">(%)</td>
</tr>
</tbody>
</table>

</th>
<th style="text-align: right;">

<a id="tab:capture_co2_solubility"></a>


<table>
<caption>CO<span class="math inline">\(_2\)</span> partial pressure over aqueous amines loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data. pCO<span class="math inline">\(_2\)</span>: 100 (calc/exp - 1) at the measured loading; <span class="math inline">\(|\ln|\)</span>: mean <span class="math inline">\(|\ln(\mathrm{calc}/\mathrm{exp})|\)</span>; loading: mean |calc - exp| of the loading at which the package meets the measured pCO<span class="math inline">\(_2\)</span>. Loadings in mol CO<span class="math inline">\(_2\)</span> per mol of amine (MDEA + PZ: per mol of MDEA + PZ).</caption>
<tbody>
<tr>
<td style="text-align: right;">Bias</td>
</tr>
<tr>
<td style="text-align: right;">(%)</td>
</tr>
</tbody>
</table>

</th>
<th style="text-align: right;"><span class="math inline">\(|\ln|\)</span></th>
<th style="text-align: right;">

<a id="tab:capture_co2_solubility"></a>


<table>
<caption>CO<span class="math inline">\(_2\)</span> partial pressure over aqueous amines loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data. pCO<span class="math inline">\(_2\)</span>: 100 (calc/exp - 1) at the measured loading; <span class="math inline">\(|\ln|\)</span>: mean <span class="math inline">\(|\ln(\mathrm{calc}/\mathrm{exp})|\)</span>; loading: mean |calc - exp| of the loading at which the package meets the measured pCO<span class="math inline">\(_2\)</span>. Loadings in mol CO<span class="math inline">\(_2\)</span> per mol of amine (MDEA + PZ: per mol of MDEA + PZ).</caption>
<tbody>
<tr>
<td style="text-align: right;">Loading AAD</td>
</tr>
<tr>
<td style="text-align: right;">(mol/mol)</td>
</tr>
</tbody>
</table>

</th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Jou 1995</td>
<td style="text-align: left;">mol/kg, 313-393 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(17\)</span></td>
<td style="text-align: right;"><span class="math inline">\(41\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+34\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.31\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.013\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Tong 2012</td>
<td style="text-align: left;">mol/kg, 313-393 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(17\)</span></td>
<td style="text-align: right;"><span class="math inline">\(35\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.30\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.019\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Wagner 2013</td>
<td style="text-align: left;">-7.3 mol/kg, 313-393 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(59\)</span></td>
<td style="text-align: right;"><span class="math inline">\(14\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.15\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.008\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Dugas 2011</td>
<td style="text-align: left;">-13.0 mol/kg, 313-373 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(50\)</span></td>
<td style="text-align: right;"><span class="math inline">\(18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.19\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.009\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Li 2015</td>
<td style="text-align: left;">mol/kg, 313-393 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(31\)</span></td>
<td style="text-align: right;"><span class="math inline">\(18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-12\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.21\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.013\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Bernhardsen 2019</td>
<td style="text-align: left;">mol/kg, 313-393 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(50\)</span></td>
<td style="text-align: right;"><span class="math inline">\(20\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.009\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Ghalib 2016</td>
<td style="text-align: left;">mol/kg, 313-353 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(15\)</span></td>
<td style="text-align: right;"><span class="math inline">\(29\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.29\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.025\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Suleman 2016</td>
<td style="text-align: left;">-6.4 mol/kg, 303-343 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(15\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.15\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.016\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Dash 2011</td>
<td style="text-align: left;">mol/kg, 323 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(11\)</span></td>
<td style="text-align: right;"><span class="math inline">\(12\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.13\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.011\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Han 2017</td>
<td style="text-align: left;">-9.7 mol/kg, 298 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(5\)</span></td>
<td style="text-align: right;"><span class="math inline">\(65\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-64\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.69\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.111\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Dey 2018</td>
<td style="text-align: left;">mol/kg, 313 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(97\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+97\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.67\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.102\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Najafloo 2015</td>
<td style="text-align: left;">-5.4 mol/kg, 313-358 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(29\)</span></td>
<td style="text-align: right;"><span class="math inline">\(12\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.13\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.019\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Najafloo 2015, total pressure</td>
<td style="text-align: left;">mol/kg, 298-348 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(25\)</span></td>
<td style="text-align: right;"><span class="math inline">\(15\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-13\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.019\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Xiao 2018</td>
<td style="text-align: left;">mol/kg, 298-313 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(20\)</span></td>
<td style="text-align: right;"><span class="math inline">\(20\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-19\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.24\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.044\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Shokouhi 2015</td>
<td style="text-align: left;">mol/kg, 303-363 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(23\)</span></td>
<td style="text-align: right;"><span class="math inline">\(12\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.14\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.017\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Leontiadis 2019</td>
<td style="text-align: left;">-2.6 mol/kg, 298-333 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(69\)</span></td>
<td style="text-align: right;"><span class="math inline">\(14\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-13\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.15\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.019\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Suleman 2016</td>
<td style="text-align: left;">-7.1 mol/kg, 303-343 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(40\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-6\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.44\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.065\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Zoghi 2012</td>
<td style="text-align: left;">-8.1 mol/kg, 313-343 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(17\)</span></td>
<td style="text-align: right;"><span class="math inline">\(71\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-71\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.40\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.230\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Shirazizadeh 2019</td>
<td style="text-align: left;">mol/kg, 313 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(5\)</span></td>
<td style="text-align: right;"><span class="math inline">\(6\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-5\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.07\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.008\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Harris 2009</td>
<td style="text-align: left;">mol/kg, 313 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(83\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-83\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.99\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.285\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Sairi 2015</td>
<td style="text-align: left;">mol/kg, 303 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-11\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.20\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.038\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Arcis 2008, 2009, solubility limits</td>
<td style="text-align: left;">-3.6 mol/kg, 322-373 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(17\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.16\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.026\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Dugas 2011</td>
<td style="text-align: left;">-12.0 mol/kg, 313-373 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(43\)</span></td>
<td style="text-align: right;"><span class="math inline">\(32\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+26\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.26\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.028\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Ermatchkov 2006</td>
<td style="text-align: left;">-4.4 mol/kg, 313-393 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(52\)</span></td>
<td style="text-align: right;"><span class="math inline">\(16\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.20\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.015\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Xu 2011</td>
<td style="text-align: left;">-9.9 mol/kg, 354-423 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(123\)</span></td>
<td style="text-align: right;"><span class="math inline">\(28\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+6\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.27\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.034\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Hilliard 2008</td>
<td style="text-align: left;">-5.2 mol/kg, 313-333 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(62\)</span></td>
<td style="text-align: right;"><span class="math inline">\(28\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+20\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.25\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.024\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Bougie 2011</td>
<td style="text-align: left;">-2.0 mol/kg, 287-313 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(56\)</span></td>
<td style="text-align: right;"><span class="math inline">\(61\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-38\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.59\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.125\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Suleman 2016</td>
<td style="text-align: left;">-6.0 mol/kg, 303-343 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(15\)</span></td>
<td style="text-align: right;"><span class="math inline">\(60\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.78\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.056\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Dash 2011</td>
<td style="text-align: left;">-7.2 mol/kg, 298-328 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(293\)</span></td>
<td style="text-align: right;"><span class="math inline">\(55\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-52\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.42\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.113\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Chen 2011</td>
<td style="text-align: left;">m MDEA/2 m PZ, 313-373 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(13\)</span></td>
<td style="text-align: right;"><span class="math inline">\(19\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-16\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.24\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.011\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Chen 2011</td>
<td style="text-align: left;">m MDEA/5 m PZ, 313-373 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(13\)</span></td>
<td style="text-align: right;"><span class="math inline">\(20\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+17\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.18\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.017\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Xu 2011</td>
<td style="text-align: left;">m MDEA/2 m PZ, 373-423 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(14\)</span></td>
<td style="text-align: right;"><span class="math inline">\(51\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-51\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.74\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.111\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Xu 2011</td>
<td style="text-align: left;">m MDEA/5 m PZ, 373-423 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(10\)</span></td>
<td style="text-align: right;"><span class="math inline">\(23\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-23\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.28\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.055\)</span></td>
</tr>
</tbody>
</table>

</div>

fit: the parameters of the package were regressed to these data; check: compared only. pCO$_2$ of the total-pressure sets is the total pressure less the water partial pressure (Raoult).



<a id="tab:capture_heat"></a>



<table>
<caption>Heat of absorption of CO<span class="math inline">\(_2\)</span> in aqueous amines: the CO<span class="math inline">\(_2\)</span> Capture package (enthalpy of the loaded liquid less that of the lean liquid and of the CO<span class="math inline">\(_2\)</span> gas) against calorimetric data, calc - exp in kJ/mol CO<span class="math inline">\(_2\)</span>.</caption>
<thead>
<tr>
<th style="text-align: left;">System</th>
<th style="text-align: left;">Source</th>
<th style="text-align: left;">Heat</th>
<th style="text-align: left;">Conditions</th>
<th style="text-align: center;">Use</th>
<th style="text-align: right;"><span class="math inline">\(n\)</span></th>
<th style="text-align: right;"><div id="tab:capture_heat">
<table>
<caption>Heat of absorption of CO<span class="math inline">\(_2\)</span> in aqueous amines: the CO<span class="math inline">\(_2\)</span> Capture package (enthalpy of the loaded liquid less that of the lean liquid and of the CO<span class="math inline">\(_2\)</span> gas) against calorimetric data, calc - exp in kJ/mol CO<span class="math inline">\(_2\)</span>.</caption>
<tbody>
<tr>
<td style="text-align: right;">AAD</td>
</tr>
<tr>
<td style="text-align: right;">(kJ/mol)</td>
</tr>
</tbody>
</table>

</th>
<th style="text-align: right;">

<a id="tab:capture_heat"></a>


<table>
<caption>Heat of absorption of CO<span class="math inline">\(_2\)</span> in aqueous amines: the CO<span class="math inline">\(_2\)</span> Capture package (enthalpy of the loaded liquid less that of the lean liquid and of the CO<span class="math inline">\(_2\)</span> gas) against calorimetric data, calc - exp in kJ/mol CO<span class="math inline">\(_2\)</span>.</caption>
<tbody>
<tr>
<td style="text-align: right;">Bias</td>
</tr>
<tr>
<td style="text-align: right;">(kJ/mol)</td>
</tr>
</tbody>
</table>

</th>
<th style="text-align: right;">

<a id="tab:capture_heat"></a>


<table>
<caption>Heat of absorption of CO<span class="math inline">\(_2\)</span> in aqueous amines: the CO<span class="math inline">\(_2\)</span> Capture package (enthalpy of the loaded liquid less that of the lean liquid and of the CO<span class="math inline">\(_2\)</span> gas) against calorimetric data, calc - exp in kJ/mol CO<span class="math inline">\(_2\)</span>.</caption>
<tbody>
<tr>
<td style="text-align: right;">Max</td>
</tr>
<tr>
<td style="text-align: right;">(kJ/mol)</td>
</tr>
</tbody>
</table>

</th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Arcis 2011</td>
<td style="text-align: left;">integral</td>
<td style="text-align: left;">2.9-7.0 mol/kg, 322.5 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(46\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+0.5\)</span></td>
<td style="text-align: right;"><span class="math inline">\(11.2\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Arcis 2011</td>
<td style="text-align: left;">integral</td>
<td style="text-align: left;">2.9-7.0 mol/kg, 372.9 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(90\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1.3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(13.0\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Mondal 2017</td>
<td style="text-align: left;">differential</td>
<td style="text-align: left;">7.0 mol/kg, 313.1 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(10\)</span></td>
<td style="text-align: right;"><span class="math inline">\(7.9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-7.3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(12.4\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Ojala 2014</td>
<td style="text-align: left;">integral</td>
<td style="text-align: left;">1.8-4.1 mol/kg, 298.0 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+2.0\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.7\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Arcis 2012</td>
<td style="text-align: left;">integral</td>
<td style="text-align: left;">1.7-4.1 mol/kg, 322.5 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(75\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-0.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(8.4\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Arcis 2012</td>
<td style="text-align: left;">integral</td>
<td style="text-align: left;">1.7-4.1 mol/kg, 372.9 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(52\)</span></td>
<td style="text-align: right;"><span class="math inline">\(4.3\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1.2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(11.2\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Arcis 2008</td>
<td style="text-align: left;">integral</td>
<td style="text-align: left;">1.5-3.6 mol/kg, 322.5 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(61\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+2.8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(6.5\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Arcis 2009</td>
<td style="text-align: left;">integral</td>
<td style="text-align: left;">1.5-3.6 mol/kg, 372.9 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(70\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-3.6\)</span></td>
<td style="text-align: right;"><span class="math inline">\(13.4\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Kim 2007</td>
<td style="text-align: left;">differential</td>
<td style="text-align: left;">2.4 mol/kg, 313.1 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(17\)</span></td>
<td style="text-align: right;"><span class="math inline">\(6.0\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-5.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(9.5\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Kim 2007</td>
<td style="text-align: left;">differential</td>
<td style="text-align: left;">2.4 mol/kg, 353.1 K</td>
<td style="text-align: center;">fit</td>
<td style="text-align: right;"><span class="math inline">\(16\)</span></td>
<td style="text-align: right;"><span class="math inline">\(7.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+6.7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(13.2\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Kim 2007</td>
<td style="text-align: left;">differential</td>
<td style="text-align: left;">2.4 mol/kg, 393.1 K</td>
<td style="text-align: center;">check</td>
<td style="text-align: right;"><span class="math inline">\(14\)</span></td>
<td style="text-align: right;"><span class="math inline">\(32.8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+32.8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(45.8\)</span></td>
</tr>
</tbody>
</table>

</div>

Integral heat: from zero loading to the measured loading; differential heat: partial molar enthalpy of absorption at the measured loading.



<a id="tab:capture_amine_volatility"></a>



| System | Quantity | Source | Conditions | Use | $n$ | AAD | Bias | Max | Unit |
|:---|:---|:---|:---|:---|:--:|---:|---:|---:|:---|
| MEA + water | total pressure | Belabbaci 2009 | -363 K | fit | $72$ | $2.7$ | $-0.3$ | $6.4$ | % |
| MEA + water | total pressure | Kim 2008 | -373 K | fit | $86$ | $1.2$ | $-0.5$ | $5.3$ | % |
| MEA + water | MEA vapour fraction | Kim 2008 | -373 K | fit | $56$ | $13.7$ | $+1.4$ | $59.6$ | % |
| MDEA + water | total pressure | Kim 2008 | -373 K | fit | $58$ | $0.7$ | $+0.1$ | $3.0$ | % |
| MDEA + water | bubble temperature | Soames 2018 | -40 kPa | fit | $33$ | $0.8$ | $+0.2$ | $2.7$ | K |
| MDEA + water | MDEA vapour fraction | Soames 2018 | -40 kPa | fit | $25$ | $46.9$ | $+46.9$ | $105.1$ | % |
| MDEA + water | bubble temperature | Barreau 2007 | -90 kPa | check | $9$ | $1.6$ | $-0.3$ | $4.2$ | K |
| DEA + water | excess enthalpy | Li 2015 | K | check | $8$ | $5.2$ | $-4.5$ | $7.2$ | % |
| DEA + water | total pressure | Shin 2023 | K | not used | $18$ | $35.4$ | $-35.4$ | $80.9$ | % |
| PZ | PZ partial pressure | Hilliard 2008 | -5.0 mol/kg, 306-337 K | check | $46$ | $23.8$ | $-22.5$ | $47.6$ | % |
| PZ | PZ partial pressure | Nguyen 2013 | mol/kg, 313-343 K | check | $7$ | $12.2$ | $+2.3$ | $24.6$ | % |
| PZ | PZ partial pressure | Nguyen 2013, activity | -10.0 mol/kg, 333 K | check | $4$ | $21.4$ | $-8.0$ | $34.4$ | % |
| MDEA + PZ | MDEA partial pressure | Nguyen 2013 | m MDEA/2 m PZ, 5 m MDEA/5 m PZ, 313-343 K | fit | $28$ | $23.0$ | $+21.0$ | $55.5$ | % |
| MDEA + PZ | PZ partial pressure | Nguyen 2013 | m MDEA/2 m PZ, 5 m MDEA/5 m PZ, 313-343 K | fit | $28$ | $43.1$ | $-43.1$ | $57.9$ | % |
| MDEA + PZ | CO$_2$ partial pressure | Nguyen 2013 | m MDEA/2 m PZ, 5 m MDEA/5 m PZ, 313-343 K | check | $14$ | $10.4$ | $+3.9$ | $24.1$ | % |

Amine and water vapour-liquid equilibrium: the CO$_2$ Capture package against experimental data (amine + water without CO$_2$, and the amine partial pressures over unloaded and loaded solvents).



DEA and PZ: the water-amine pairs are taken as published (Posey 1996; Hilliard 2008). Shin 2023 is reported and not used (see the README of the fitting data).



<a id="tab:capture_density"></a>



<table>
<caption>Density of aqueous amines, lean and loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data, calc - exp in kg/m<span class="math inline">\(^3\)</span>.</caption>
<thead>
<tr>
<th style="text-align: left;">System</th>
<th style="text-align: left;">Source</th>
<th style="text-align: left;">Conditions</th>
<th style="text-align: left;">Use</th>
<th style="text-align: center;"><span class="math inline">\(n\)</span></th>
<th style="text-align: right;"><div id="tab:capture_density">
<table>
<caption>Density of aqueous amines, lean and loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data, calc - exp in kg/m<span class="math inline">\(^3\)</span>.</caption>
<tbody>
<tr>
<td style="text-align: right;">AAD</td>
</tr>
<tr>
<td style="text-align: right;">(kg/m<span class="math inline">\(^3\)</span>)</td>
</tr>
</tbody>
</table>

</th>
<th style="text-align: right;">

<a id="tab:capture_density"></a>


<table>
<caption>Density of aqueous amines, lean and loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data, calc - exp in kg/m<span class="math inline">\(^3\)</span>.</caption>
<tbody>
<tr>
<td style="text-align: right;">Bias</td>
</tr>
<tr>
<td style="text-align: right;">(kg/m<span class="math inline">\(^3\)</span>)</td>
</tr>
</tbody>
</table>

</th>
<th style="text-align: right;">

<a id="tab:capture_density"></a>


<table>
<caption>Density of aqueous amines, lean and loaded with CO<span class="math inline">\(_2\)</span>: the CO<span class="math inline">\(_2\)</span> Capture package against experimental data, calc - exp in kg/m<span class="math inline">\(^3\)</span>.</caption>
<tbody>
<tr>
<td style="text-align: right;">Max</td>
</tr>
<tr>
<td style="text-align: right;">(kg/m<span class="math inline">\(^3\)</span>)</td>
</tr>
</tbody>
</table>

</th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">MEA</td>
<td style="text-align: left;">Amundsen 2009</td>
<td style="text-align: left;">lean and loaded, 298-353 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(103\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.6\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+0.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(7.2\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Han 2012</td>
<td style="text-align: left;">lean and loaded, 298-393 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(278\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-0.2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(4.6\)</span></td>
</tr>
<tr>
<td style="text-align: left;">DEA</td>
<td style="text-align: left;">Jayarathna 2012</td>
<td style="text-align: left;">lean and loaded, 293-423 K</td>
<td style="text-align: left;">check</td>
<td style="text-align: center;"><span class="math inline">\(304\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+3.8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(10.8\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Han 2012</td>
<td style="text-align: left;">lean and loaded, 298-393 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(271\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.0\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-0.4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(8.2\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA</td>
<td style="text-align: left;">Jayarathna 2012</td>
<td style="text-align: left;">lean and loaded, 293-423 K</td>
<td style="text-align: left;">check</td>
<td style="text-align: center;"><span class="math inline">\(247\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.8\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+2.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(11.5\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Freeman 2011</td>
<td style="text-align: left;">lean and loaded, 293-333 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(147\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+0.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(17.4\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Samanta 2006</td>
<td style="text-align: left;">lean, 298-333 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(32\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-0.9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.5\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Muhammad 2009</td>
<td style="text-align: left;">lean, 298-338 K</td>
<td style="text-align: left;">check</td>
<td style="text-align: center;"><span class="math inline">\(27\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-0.4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.0\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Derks 2005</td>
<td style="text-align: left;">lean, 293-323 K</td>
<td style="text-align: left;">check</td>
<td style="text-align: center;"><span class="math inline">\(19\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-0.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.7\)</span></td>
</tr>
<tr>
<td style="text-align: left;">PZ</td>
<td style="text-align: left;">Speyer 2010</td>
<td style="text-align: left;">lean, 293 K</td>
<td style="text-align: left;">check</td>
<td style="text-align: center;"><span class="math inline">\(6\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+0.2\)</span></td>
<td style="text-align: right;"><span class="math inline">\(0.6\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Paul 2006</td>
<td style="text-align: left;">lean, 288-333 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(40\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1.4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.3\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Muhammad 2009</td>
<td style="text-align: left;">lean, 298-338 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(54\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(+1.7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(9.7\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Derks 2008</td>
<td style="text-align: left;">lean, 293-323 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(78\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1.4\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.9\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">B<span>ö</span>ttger 2009</td>
<td style="text-align: left;">lean, 289-303 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(24\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.0\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1.0\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.1\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Kessler 2019</td>
<td style="text-align: left;">lean, 293-353 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(7\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-3.9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(4.6\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Speyer 2010</td>
<td style="text-align: left;">lean, 293 K</td>
<td style="text-align: left;">fit</td>
<td style="text-align: center;"><span class="math inline">\(37\)</span></td>
<td style="text-align: right;"><span class="math inline">\(1.1\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-0.5\)</span></td>
<td style="text-align: right;"><span class="math inline">\(3.1\)</span></td>
</tr>
<tr>
<td style="text-align: left;">MDEA + PZ</td>
<td style="text-align: left;">Frailie 2014</td>
<td style="text-align: left;">lean and loaded, 293-333 K</td>
<td style="text-align: left;">check</td>
<td style="text-align: center;"><span class="math inline">\(16\)</span></td>
<td style="text-align: right;"><span class="math inline">\(2.0\)</span></td>
<td style="text-align: right;"><span class="math inline">\(-1.9\)</span></td>
<td style="text-align: right;"><span class="math inline">\(4.9\)</span></td>
</tr>
</tbody>
</table>

</div>

MDEA + PZ: the parameters of each amine plus one MDEA-PZ term fitted to the lean blends.

###### Application domains

- **Post-combustion CO$_2$ capture:** absorber and stripper design with MEA, PZ, or MDEA + PZ, including the heat duty of the reboiler through the heat of absorption.

- **CO$_2$ removal from natural gas and syngas:** MDEA and MDEA + PZ solvents (equilibrium only; $\ce{H2S}$ is not part of the chemistry).

- **Solvent comparison:** $\ce{CO2}$ capacity, partial pressure curves and regeneration heat of the five solvents.

#### CO$_2$ Transport (Span-Wagner / PR) {#sec:ccus_transport}

##### Overview {#overview-56}

The CO$_2$ Transport property package provides high-accuracy thermodynamic and transport properties for CO$_2$-rich streams in pipeline and shipping applications. Two equation-of-state regimes are used depending on stream purity:

- **Pure CO$_2$** ($x_{\ce{CO2}} > 0.9999$): Span–Wagner multiparameter equation of state , a 42-term Helmholtz free energy formulation that is the IUPAC/NIST reference standard for CO$_2$.

- **CO$_2$ with impurities** ($x_{\ce{CO2}} \leq 0.9999$): Peng–Robinson equation of state  with transport-specific binary interaction parameters ($k_{ij}$).

##### Span–Wagner Equation of State

The dimensionless Helmholtz free energy is expressed as


<a id="eq:sw_helmholtz"></a>

\[
\frac{a(\rho,T)}{RT} = \alpha(\delta,\tau) = \alpha^{0}(\delta,\tau) + \alpha^{r}(\delta,\tau)
\]


where $\delta = \rho/\rho_{c}$ and $\tau = T_{c}/T$ are the reduced density and inverse reduced temperature, with critical parameters $T_{c} = 304.1282$ K, $P_{c} = 73.773$ bar, $\rho_{c} = 467.6$ kg/m$^{3}$.

The residual part $\alpha^{r}$ contains 42 terms : 7 polynomial, 27 exponential, 5 Gaussian bell-shaped and 3 non-analytic terms:


<a id="eq:sw_residual"></a>

\[
\begin{multline}
  \alpha^{r}(\delta,\tau) =
    \sum_{i=1}^{7} n_{i}\,\delta^{d_{i}}\,\tau^{t_{i}}
    + \sum_{i=8}^{34} n_{i}\,\delta^{d_{i}}\,\tau^{t_{i}}\,
      e^{-\delta^{c_{i}}} \\
    + \sum_{i=35}^{39} n_{i}\,\delta^{d_{i}}\,\tau^{t_{i}}\,
      e^{-\alpha_{i}(\delta-\varepsilon_{i})^{2} - \beta_{i}(\tau-\gamma_{i})^{2}}
    + \sum_{i=40}^{42} n_{i}\,\Delta^{b_{i}}\,\delta\,\psi
\end{multline}
\]


with $\Delta = \theta^{2} + B_{i}[(\delta-1)^{2}]^{a_{i}}$, $\theta = (1-\tau) + A_{i}[(\delta-1)^{2}]^{1/(2\beta_{i})}$ and $\psi = e^{-C_{i}(\delta-1)^{2} - D_{i}(\tau-1)^{2}}$. All thermodynamic properties (pressure, enthalpy, entropy, heat capacity, speed of sound, fugacity coefficient) are obtained as analytical derivatives of $\alpha(\delta,\tau)$. Density is determined by iterative solution of $P = \rho RT(1 + \delta\,\partial\alpha^{r}/\partial\delta)$.

The package takes density, enthalpy, entropy, fugacity, heat capacities and speed of sound from Span–Wagner (pure $\ce{CO2}$) or from Peng–Robinson with the transport $k_{ij}$ (mixtures, exact $C_{p}$ and $C_{v}$ of the equation of state). For pure $\ce{CO2}$ the heat capacities and the speed of sound agree with the reference implementation (CoolProp) to 0.00 % at 12 points of the gas, liquid, dense and near-critical regions; at 25 °C and 110 bar the stream shows $C_{p} = 2.694$ kJ/(kg$\cdot$K), $C_{v} = 0.937$ kJ/(kg$\cdot$K) and $w = 456.4$ m/s, the NIST values. For 95 % $\ce{CO2}$ with 5 % $\ce{N2}$ the Peng–Robinson $C_{p}$ is within 1 to 9 %, $C_{v}$ 5 to 10 % low and the speed of sound within 4 % of a multiparameter mixture model.

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


where $\Delta\lambda_{\mathrm{crit}}$ is the critical enhancement. The package leaves this term out, so close to the critical point, where it is significant, the computed conductivity is low. The viscosity is likewise computed without a critical enhancement.

##### Mixture Mode: Peng–Robinson with Transport $k_{ij}$

For impure CO$_2$ streams, the Peng–Robinson EOS  is used with binary interaction parameters taken from the literature for the usual pipeline impurities; the package editor shows the source of each pair. Table [53](#tab:transport_kij) lists the shipped $k_{ij}$ values.



<a id="tab:transport_kij"></a>



| Pair                      |  $k_{ij}$ |
|:--------------------------|------------:|
| $\ce{CO2}$–$\ce{N2}$  |  $0.0070$ |
| $\ce{CO2}$–$\ce{O2}$  |  $0.1140$ |
| $\ce{CO2}$–$\ce{Ar}$  |  $0.1150$ |
| $\ce{CO2}$–$\ce{H2S}$ |  $0.0974$ |
| $\ce{CO2}$–$\ce{H2O}$ |  $0.1896$ |
| $\ce{CO2}$–$\ce{SO2}$ |  $0.0440$ |
| $\ce{CO2}$–$\ce{CH4}$ |  $0.0919$ |
| $\ce{CO2}$–$\ce{H2}$  |  $0.0920$ |
| $\ce{CO2}$–$\ce{CO}$  |  $0.0490$ |
| $\ce{CO2}$–$\ce{NO2}$ |  $0.0500$ |
| $\ce{N2}$–$\ce{O2}$   | $-0.0119$ |
| $\ce{N2}$–$\ce{Ar}$   | $-0.0060$ |
| $\ce{N2}$–$\ce{H2}$   | $-0.0200$ |

CO$_2$ Transport binary interaction parameters



##### Validity Range and Validation {#validity-range-and-validation-1}

| Property | Recommended range |
|:---|:---|
| Temperature | 216–1100 K (Span–Wagner valid range) |
| Pressure | 0–800 MPa (Span–Wagner) |
| CO$_2$ purity | $> 90$ mol% for pipeline applications |
| Impurities | N$_2$, O$_2$, Ar, H$_2$S, H$_2$O, SO$_2$, CH$_4$, H$_2$, CO, NO$_2$ |

CO$_2$ Transport PP recommended operating envelope

###### Validation against NIST

Pure CO$_2$ density from Span–Wagner matches the NIST WebBook reference data to $< 0.01$% across the entire fluid range. Specific test points: 228.8 kg/m$^{3}$ at 350 K / 100 bar (supercritical, deviation $+0.003$%); 938.2 kg/m$^{3}$ at 280 K / 100 bar (liquid, deviation $+0.001$%).

###### Application domains

- **CO$_2$ pipeline design:** phase envelope calculation, dense-phase transport properties, two-phase detection for safety analysis.

- **CO$_2$ compression trains:** isentropic and polytropic compressor calculations with real-gas departure functions.

- **Ship transport:** liquefied CO$_2$ properties at low-temperature, medium-pressure conditions.

- **Impurity impact assessment:** effect of N$_2$, O$_2$, H$_2$S on phase behaviour and transport properties.

#### CO$_2$ Storage (Duan–Sun) {#sec:ccus_storage}

##### Overview {#overview-57}

The CO$_2$ Storage property package describes $\ce{CO2}$ with water and chloride brines at the conditions of geological storage: saline aquifer injection, CO$_2$-enhanced oil recovery and the brine chemistry that leads to mineral trapping. The apparent compounds are water, $\ce{CO2}$, other gases, and the ions or salts of the brine. Ions and salts are non-volatile and stay in the liquid.

The phase equilibrium combines four parts:

- $\ce{CO2}$ in the aqueous phase from the model of Duan and Sun , referred to a water-saturated $\ce{CO2}$ phase computed with Peng–Robinson;

- water from IAPWS-IF97 , with the Poynting correction and Raoult’s law on the dissolved particles;

- the $\ce{CO2}$-rich phase from the Peng–Robinson equation of state  with a $\ce{CO2}$–water $k_{ij}$ fitted to the water content of the Spycher, Pruess and Ennis-King model ;

- $\ce{H2S}$ and the supercritical gases by Henry’s law.

The flash is the standard vapour–liquid algorithm of DWSIM for a molecular package, run on these fugacity coefficients. A PT result with less than $10^{-5}$ of vapour whose feed lies below its bubble point is returned as all liquid. All parameters are compiled into the package; it reads no data files.

##### CO$_2$ in the Aqueous Phase

Duan and Sun  write the solubility of $\ce{CO2}$ in a brine as


<a id="eq:duansun"></a>

\[
\ln m_{\ce{CO2}} = \ln\!\left(y_{\ce{CO2}}\,\varphi_{\ce{CO2}}\,P\right)
    - \frac{\mu_{\ce{CO2}}^{l(0)}}{RT} - \ln\gamma_{\ce{CO2}}
\]


where $m_{\ce{CO2}}$ is the molality of dissolved $\ce{CO2}$ (mol/kg $\ce{H2O}$), $P$ is in bar, $\mu^{l(0)}/RT$ is the standard chemical potential of dissolved $\ce{CO2}$ (an 11-term function of $T$ and $P$ fitted by Duan and Sun to solubility data), and $\gamma_{\ce{CO2}}$ is the activity coefficient of dissolved $\ce{CO2}$ on the molality scale, from their Eq. 9:


<a id="eq:duansun_gamma"></a>

\[
\ln\gamma_{\ce{CO2}} = 2\lambda\left(m_{\ce{Na+}} + m_{\ce{K+}}
      + 2m_{\ce{Ca^{2+}}} + 2m_{\ce{Mg^{2+}}}\right)
    + \zeta\,m_{\ce{Cl-}}\left(m_{\ce{Na+}} + m_{\ce{K+}}
      + m_{\ce{Ca^{2+}}} + m_{\ce{Mg^{2+}}}\right)
    - 0.07\,m_{\ce{SO4^{2-}}}
\]


with $\lambda$ and $\zeta$ the $\ce{CO2}$–$\ce{Na+}$ and $\ce{CO2}$–$\ce{Na+}$–$\ce{Cl-}$ interaction parameters, functions of $T$ and $P$ of the same form. Ions outside these classes are sorted by charge: a monovalent cation counts as $\ce{Na+}$, a divalent cation as $\ce{Ca^{2+}}$, a cation of charge $z > 2$ as $z$ $\ce{Na+}$, a divalent anion as $\ce{SO4^{2-}}$ and any other anion as $|z|$ $\ce{Cl-}$.

Duan and Sun fitted $\mu^{l(0)}/RT$ with the $\ce{CO2}$ phase taken as pure $\ce{CO2}$ diluted by water at its vapour pressure, $y_{\ce{CO2}} =
(P - P_{w}^{\mathrm{sat}})/P$, and with the fugacity coefficient of pure $\ce{CO2}$ from the equation of state of Duan, Møller and Weare . The real water content of a $\ce{CO2}$ phase at reservoir conditions is several times $P_{w}^{\mathrm{sat}}/P$, and Peng–Robinson gives $\ce{CO2}$ a fugacity coefficient of its own. The package therefore writes the liquid fugacity of $\ce{CO2}$ as


<a id="eq:storage_fco2"></a>

\[
f_{\ce{CO2}}^{L} = m_{\ce{CO2}}\,\gamma_{\ce{CO2}}\,
    \exp\!\left(\frac{\mu_{\ce{CO2}}^{l(0)}}{RT}\right) C(T,P),
  \qquad
  C(T,P) = \frac{f_{\ce{CO2}}^{\mathrm{PR}}(T,P)}
               {(P - P_{w}^{\mathrm{sat}})\,\varphi_{\ce{CO2}}^{\mathrm{Duan}}(T,P)}
\]


where $f_{\ce{CO2}}^{\mathrm{PR}}$ is the fugacity of $\ce{CO2}$ in a $\ce{CO2}$ phase saturated with water, computed with the package’s own Peng–Robinson phase over pure water. Under a $\ce{CO2}$ phase the flash then returns the Duan–Sun solubility (within 0.25 % in pure water), and $C$ tends to one in the ideal-gas limit. The fugacity coefficient the flash uses is $\varphi_{\ce{CO2}}^{L} = f_{\ce{CO2}}^{L}/(x_{\ce{CO2}}P)$, with $x_{\ce{CO2}}$ the apparent mole fraction. A liquid that holds no water (dense $\ce{CO2}$) takes the fugacity of pure $\ce{CO2}$ from the Span–Wagner equation of state .

##### Water

The fugacity of water in the liquid is


<a id="eq:storage_fw"></a>

\[
f_{\ce{H2O}}^{L} = a_{w}\,P_{w}^{\mathrm{sat}}\,\varphi_{w}^{\mathrm{sat}}
    \exp\!\left[\frac{V_{w}\,(P - P_{w}^{\mathrm{sat}})}{RT}\right],
  \qquad
  a_{w} = \frac{1}{1 + M_{w}\sum_{i} m_{i}}
\]


with $P_{w}^{\mathrm{sat}}$ and the liquid volume $V_{w}$ from IAPWS-IF97 , $\varphi_{w}^{\mathrm{sat}}$ from the vapour equation of state at saturation, and the sum over every dissolved particle (the ions of a salt counted apart, dissolved $\ce{CO2}$ and other solutes). The fugacity coefficient is $\varphi_{\ce{H2O}}^{L} =
f_{\ce{H2O}}^{L}/(x_{\ce{H2O}}P)$, so the dilution of water by the solutes is counted once. Raoult’s law carries no osmotic coefficient: the water vapour pressure over 3.6 mol/kg NaCl comes out 2.2 % high (Table [59](#tab:storage_brine_vapour_pressure)).

##### CO$_2$-Rich Phase

The vapour, or the dense $\ce{CO2}$-rich phase, follows Peng–Robinson on the molecular compounds. The $\ce{CO2}$–water binary parameter $k_{ij} = 0.193$ was fitted to the water content of the $\ce{CO2}$ phase given by Spycher, Pruess and Ennis-King  from 288 to 373 K and 75 to 500 bar (2.6 % average deviation); with $k_{ij} = 0$ the water content was three to four times too high below 310 K. The root with the lower Gibbs energy is taken, except for a water-rich phase, which stays on the vapour root: below the critical temperature of $\ce{CO2}$ and above its vapour pressure the $\ce{CO2}$-rich phase is a liquid, and its vapour root would be a metastable state with a fugacity about 50 % too high. The residual enthalpy and entropy of this phase come from the same Peng–Robinson fugacities.

##### Liquid Density

The density of an aqueous liquid is computed per kilogram of water:


<a id="eq:storage_density"></a>

\[
V = \frac{1}{\rho_{w}(T,P)} + \sum_{s} m_{s}\,V_{\varphi,s}(T,P,m^{*})
      + m_{\ce{CO2}}\,V_{\varphi,\ce{CO2}}(T,P),
  \qquad
  \rho = \frac{1 + \sum_{s} m_{s}M_{s} + m_{\ce{CO2}}M_{\ce{CO2}}}{V}
\]


where $\rho_{w}$ is the IAPWS-IF97 density of water. The brine is taken as the chlorides of its cations (Young’s rule), each apparent molar volume $V_{\varphi,s}$ evaluated at the ionic strength of the whole brine. The apparent molar volumes of NaCl, KCl, $\ce{CaCl2}$ and $\ce{MgCl2}$ are 13-term polynomials in $T$, $P$ and $\sqrt{m}$ fitted to the densities of Al Ghafri, Maitland and Trusler  (283 to 473 K, up to 68 MPa and 6 mol/kg). The apparent molar volume of dissolved $\ce{CO2}$ is fitted to McBride-Wright, Maitland and Trusler  (274 to 449 K, up to 70 MPa). Other ions are mapped by charge: a monovalent cation other than $\ce{Na+}$ and $\ce{K+}$ counts as $\ce{Na+}$, a divalent one as $\ce{Ca^{2+}}$, a trivalent one as three $\ce{Na+}$; an anion other than $\ce{Cl-}$ is taken as the chlorides of its charge with its own mass. Inside $V_{\varphi}$, temperature, pressure and molality are held to the fitted ranges. Other molecular solutes add their own liquid molar volume.

##### Enthalpy and Heat of Solution

Dissolved $\ce{CO2}$ sits on its ideal-gas reference, plus its partial molar enthalpy of solution from the temperature slope of the Duan–Sun chemical potential:


<a id="eq:storage_hsol"></a>

\[
\bar{H}_{\ce{CO2}} - H_{\ce{CO2}}^{\mathrm{ig}} = -RT^{2}
    \left[\frac{\partial}{\partial T}\left(\frac{\mu_{\ce{CO2}}^{l(0)}}{RT}
      + \ln\gamma_{\ce{CO2}}\right)\right]_{P,m}
\]


The rest of the liquid takes the enthalpy of the electrolyte packages (ideal gas less the heat of vaporization). The excess enthalpy of the brine is an option, off by default: when it is on, $H^{E} = -RT^{2}\sum_{i}x_{i}\,\partial\ln\gamma_{i}/\partial T$ from the electrolyte NRTL model over the true species (water, the ions of the salts and the dissolved gases), with every cation–anion pair on NaCl parameters whose enthalpy terms were fitted to the relative apparent molar enthalpy of NaCl solutions of Pitzer, Peiper and Busey  (rms 119 J/mol at 25 °C, 151 at 60 °C and 169 at 100 °C). Only NaCl is validated, and KCl is within 260 J/mol of Parker’s data  up to 2.5 mol/kg. With the option on, the brine heat capacity moves away from the measured one (5 mol/kg NaCl at 20 °C: 3.550 against 3.345 kJ/(kg$\cdot$K), 3.197 with the option off), which is why it stays off. The vapour is the ideal gas plus the residual enthalpy of its Peng–Robinson fugacities. A liquid without water (dense $\ce{CO2}$ that a flash labels liquid) takes the enthalpy of the vapour equation of state, so that its label does not change its enthalpy.

##### pH and Carbonate Speciation

The pH and the ionic strength of a liquid come from a carbonate speciation at its total dissolved carbon. The first and second dissociation constants of carbonic acid and the ion product of water are those of Edwards et al. ; the charge balance with the strong ions of the brine sets $m_{\ce{H+}}$. The ions take Davies activity coefficients ,


<a id="eq:storage_davies"></a>

\[
\log_{10}\gamma_{i} = -A\,z_{i}^{2}\left(\frac{\sqrt{I}}{1 + \sqrt{I}}
    - 0.3\,I\right),
  \qquad A = 0.51,
\]


dissolved $\ce{CO2}$ takes the Duan–Sun coefficient of Eq. [\[eq:duansun_gamma\]](#eq:duansun_gamma), and the water activity is that of Eq. [\[eq:storage_fw\]](#eq:storage_fw). The pH is $-\log_{10}(\gamma_{\ce{H+}}
m_{\ce{H+}})$. The Davies equation loses accuracy above an ionic strength of about 0.5 mol/kg, so the pH of concentrated brines is indicative.

##### Mineral Trapping

The package reports the saturation index of carbonate minerals in the brine,


<a id="eq:storage_si"></a>

\[
\mathrm{SI} = \log_{10}\frac{\mathrm{IAP}}{K_{sp}(T)}
\]


where IAP is the ion activity product from the speciation (Davies coefficients). $\mathrm{SI} > 0$ means the brine is supersaturated and the mineral can precipitate; $\mathrm{SI} < 0$ means it can dissolve. The solubility products are written on the $\ce{CO3^{2-}}$ basis:


\[
\begin{alignat}
{2}
  $\ce{CaCO3}$             &\;$\ce{<=>}$\; $\ce{Ca^{2+} + CO3^{2-}}$
    &&\qquad \text{calcite~[Plummer1982]} \\
  $\ce{MgCO3}$             &\;$\ce{<=>}$\; $\ce{Mg^{2+} + CO3^{2-}}$
    &&\qquad \text{magnesite~[Benezeth2011]} \\
  $\ce{FeCO3}$             &\;$\ce{<=>}$\; $\ce{Fe^{2+} + CO3^{2-}}$
    &&\qquad \text{siderite~[Benezeth2009]} \\
  $\ce{CaMg(CO3)2}$        &\;$\ce{<=>}$\; $\ce{Ca^{2+} + Mg^{2+} + 2 CO3^{2-}}$
    &&\qquad \text{dolomite~[Benezeth2018]} \\
  $\ce{NaAlCO3(OH)2}$      &\;$\ce{<=>}$\; $\ce{Na+ + Al^{3+} + CO3^{2-} + 2 OH-}$
    &&\qquad \text{dawsonite~[ParkhurstAppelo2013]}
\end{alignat}
\]


with $\log_{10}K_{sp} = a + bT + c/T + d\log_{10}T + eT^{2} + f/T^{2}$. Calcite follows Plummer and Busenberg  (0 to 90 °C), magnesite, siderite and dolomite the measurements of Benezeth et al. (50 to 200, 25 to 250 and 50 to 253 °C), and dawsonite the `llnl.dat` database of PHREEQC , moved to this basis with the second dissociation constant of carbonic acid of Plummer and Busenberg and the ion product of water of `llnl.dat`. Outside these ranges the correlations are extrapolated. The package carries no aluminium species, so dawsonite has a solubility product but no saturation index. Table [60](#tab:storage_ksp) compares the correlations with `llnl.dat`.

##### Validity Range and Limitations



<a id="tab:storage_envelope"></a>



| Property | Recommended range |
|:---|:---|
| Temperature | 273–533 K (Duan–Sun); densities fitted to 283–473 K |
| Pressure | up to 2000 bar (Duan–Sun); densities fitted up to 700 bar |
| Salinity | NaCl up to about 6 mol/kg; see the limitations for KCl, $\ce{CaCl2}$, $\ce{MgCl2}$ |
| CO$_2$ content | trace to saturation |
| Minerals | calcite, magnesite, siderite, dolomite (dawsonite: $K_{sp}$ only) |

CO$_2$ Storage PP recommended operating envelope



- Duan and Sun count $\ce{K+}$ as $\ce{Na+}$ and $\ce{Mg^{2+}}$ as $\ce{Ca^{2+}}$ (Eq. [\[eq:duansun_gamma\]](#eq:duansun_gamma)). In KCl, $\ce{CaCl2}$ and $\ce{MgCl2}$ brines the model salts out more $\ce{CO2}$ than measured, most at high molality and low temperature: on average 18 % low in KCl, 11 % in $\ce{CaCl2}$ and 10 % in $\ce{MgCl2}$ (Table [55](#tab:storage_co2_solubility)), and up to 24 % at 6 mol/kg $\ce{CaCl2}$ and 15 % at 5 mol/kg $\ce{MgCl2}$.

- The flash forms no solid salt phase. At 473 K and 1 bar a brine can dry out completely, and the package does not precipitate the salt.

- The water content of the $\ce{CO2}$ phase follows Spycher and Pruess and the dew points of Kim et al. ; the Raman data of Wang et al.  lie well above both (Table [58](#tab:storage_water_in_co2)).

- The pH and the saturation indices use Davies activity coefficients and are indicative above an ionic strength of about 0.5 mol/kg.

##### Validation

Tables [55](#tab:storage_co2_solubility) to [60](#tab:storage_ksp) compare the package with experimental data. They were produced by the script `validate.py` in the fitting folder of the package source (`Storage/Fitting`), which runs about 3000 flashes of the shipped package, one or more per data point; the same folder holds the data files with their citations and DOIs, the fit scripts and the point-by-point results. In the *Use* column, *fit* means the parameters were regressed to the data set and *check* means it was only compared. No parameter was fitted to the $\ce{CO2}$ solubility data: the Duan–Sun parameters are the published ones. The data sets of Song et al., Hebach et al., Kim et al. and Nasirzadeh et al. were not used in any fit.

The NaCl set of Mohammadian et al. lies far from the other NaCl sets (24 % above the model on average and up to 240 % on single points, against 1.5 to 5 % for the others) and is left out of the NaCl total. Also left out: the points of McBride-Wright et al. at 100 MPa (the fit stops at 70 MPa), the points of Kamps et al. without $\ce{CO2}$, the KI and $\ce{AlCl3}$ sets of Al Ghafri et al., and set 5 of Song et al., which repeats the $\ce{CO2}$-free points of sets 1 to 4 and is used once as the brine check.

The short names in the tables refer to the following sources. $\ce{CO2}$ solubility: Messabeb 2016 , Koschel 2006 , Qin 2008 , Lucile 2012 , Tong 2013 , Messabeb 2017 , Wang 2019 , Mohammadian 2015 , Carvalho 2015 , Guo 2016 , Kamps 2007 . Density: Al Ghafri 2012 , Song 2013 , McBride-Wright 2015 , Hebach 2004 . Heat of solution: Koschel 2006 . Water in the $\ce{CO2}$ phase: Kim 2012 , Wang 2018 . Brine vapour pressure: Nasirzadeh 2004 .



<a id="tab:storage_co2_solubility"></a>



| System | Source | Use | $n$ | AAD (%) | Bias (%) | Max (%) |
|:---|:---|:---|---:|---:|---:|---:|
| Water | Messabeb 2016 | check | $4$ | $1.6$ | $-1.6$ | $2.8$ |
| Water | Koschel 2006 | check | $8$ | $2.5$ | $+1.1$ | $5.4$ |
| Water | Qin 2008 | check | $7$ | $4.1$ | $-4.1$ | $8.4$ |
| Water | Lucile 2012 | check | $30$ | $3.0$ | $-0.8$ | $8.4$ |
| Water | Tong 2013 | check | $5$ | $0.1$ | $0.0$ | $0.2$ |
| Water | Messabeb 2017 | check | $8$ | $2.5$ | $-2.5$ | $3.8$ |
| Water | Wang 2019 | check | $198$ | $1.6$ | $-0.7$ | $5.4$ |
| Water | Mohammadian 2015 | check | $20$ | $1.0$ | $+0.7$ | $4.1$ |
| Water | Carvalho 2015 | check | $66$ | $6.3$ | $-2.5$ | $30.8$ |
| Water | All sets | check | $346$ | $2.6$ | $-1.0$ | $30.8$ |
| NaCl | Messabeb 2016 | check | $36$ | $3.7$ | $-3.2$ | $10.2$ |
| NaCl | Koschel 2006 | check | $14$ | $5.1$ | $-0.9$ | $13.7$ |
| NaCl | Wang 2019 | check | $306$ | $1.5$ | $-0.4$ | $7.7$ |
| NaCl | Mohammadian 2015 (not in the total) | check | $49$ | $24.2$ | $+24.2$ | $237.8$ |
| NaCl | Carvalho 2015 | check | $44$ | $5.2$ | $-3.3$ | $13.8$ |
| NaCl | Guo 2016 | check | $180$ | $3.3$ | $-1.4$ | $11.0$ |
| NaCl | All sets | check | $580$ | $2.6$ | $-1.1$ | $13.8$ |
| KCl | Kamps 2007 | check | $98$ | $17.6$ | $-17.6$ | $35.6$ |
| CaCl$_2$ | Tong 2013 | check | $36$ | $8.3$ | $-7.9$ | $24.8$ |
| CaCl$_2$ | Messabeb 2017 | check | $36$ | $14.0$ | $-13.9$ | $39.7$ |
| CaCl$_2$ | All sets | check | $72$ | $11.1$ | $-10.9$ | $39.7$ |
| MgCl$_2$ | Tong 2013 | check | $39$ | $10.2$ | $-9.3$ | $33.5$ |

CO$_2$ solubility in water and chloride brines: flash of the CO$_2$ Storage package against experimental data (deviation 100 (calc/exp - 1)). No parameter of the package was fitted to these data.



Mohammadian 2015 (NaCl) lies far from the other NaCl sets and is left out of the NaCl total. Duan and Sun count K+ as Na+ and Mg2+ as Ca2+ (their Eq. 9): the model salts out more CO$_2$ than measured in KCl, CaCl$_2$ and MgCl$_2$ brines, most at high molality and low temperature.



<a id="tab:storage_density"></a>



| Liquid | Source | Use | $n$ | AAD (kg/m$^3$) | Bias (kg/m$^3$) | Max (kg/m$^3$) |
|:---|:---|:---|---:|---:|---:|---:|
| NaCl brine | Al Ghafri 2012 | fit | $189$ | $0.39$ | $-0.01$ | $2.10$ |
| KCl brine | Al Ghafri 2012 | fit | $189$ | $0.37$ | $0.00$ | $1.84$ |
| CaCl$_2$ brine | Al Ghafri 2012 | fit | $197$ | $0.63$ | $-0.02$ | $3.10$ |
| MgCl$_2$ brine | Al Ghafri 2012 | fit | $204$ | $0.65$ | $0.00$ | $3.30$ |
| NaCl+KCl brine | Al Ghafri 2012 | check | $260$ | $0.45$ | $-0.33$ | $2.44$ |
| NaCl brine | Song 2013 | check | $100$ | $1.36$ | $-0.39$ | $3.83$ |
| CO$_2$ + water | McBride-Wright 2015 | fit | $74$ | $0.21$ | $-0.04$ | $0.66$ |
| CO$_2$ + NaCl brine | Song 2013 | check | $300$ | $1.59$ | $-1.46$ | $4.61$ |
| CO$_2$-saturated water | Hebach 2004 | check | $205$ | $1.01$ | $-0.63$ | $2.14$ |

Liquid density of brines and of CO$_2$ solutions: CO$_2$ Storage package against experimental data (deviation calc - exp).





<a id="tab:storage_heat_of_solution"></a>



| Solvent       | $T$   | Use   |  $n$ | AAD (kJ/mol) | Bias (kJ/mol) | Max (kJ/mol) |
|:--------------|:--------|:------|-------:|-------------:|--------------:|-------------:|
| Water         | 323.1 K | check |  $5$ |     $0.33$ |     $+0.05$ |     $0.54$ |
| NaCl 1 mol/kg | 323.1 K | check |  $4$ |     $0.41$ |     $+0.41$ |     $1.23$ |
| NaCl 3 mol/kg | 323.1 K | check |  $4$ |     $0.12$ |     $-0.10$ |     $0.22$ |
| Water         | 373.1 K | check |  $3$ |     $0.30$ |     $+0.30$ |     $0.49$ |
| NaCl 1 mol/kg | 373.1 K | check |  $3$ |     $0.40$ |     $+0.40$ |     $0.57$ |
| NaCl 3 mol/kg | 373.1 K | check |  $3$ |     $0.23$ |     $+0.23$ |     $0.33$ |
| All           |         | check | $22$ |     $0.30$ |     $+0.20$ |     $1.23$ |

Heat of solution of CO$_2$ in water and NaCl brine at infinite dilution: CO$_2$ Storage package (mixer and heater back to the feed temperature) against Koschel et al. (2006), taken as the average $H_{mix}/x$(CO$_2$) of the three most dilute points of each isotherm and isobar (deviation calc - exp).





<a id="tab:storage_water_in_co2"></a>



| Source    | Use   |  $n$ |  AAD (%) |  Bias (%) |  Max (%) |
|:----------|:------|-------:|---------:|----------:|---------:|
| Kim 2012  | check | $25$ |  $7.3$ |  $-7.3$ | $12.1$ |
| Wang 2018 | check | $45$ | $34.1$ | $-34.1$ | $48.0$ |

Water content of the CO$_2$-rich phase over water: flash of the CO$_2$ Storage package against experimental data (deviation 100 (calc/exp - 1)). The CO$_2$-water kij of 0.193 was fitted to the correlation of Spycher, Pruess and Ennis-King (2003), not to these data.



Kim 2012: dew temperatures at 8.1, 10.1 and 20.1 MPa, 283 to 312 K. Wang 2018: in-situ Raman, 313 to 473 K, 10 to 50 MPa.



<a id="tab:storage_brine_vapour_pressure"></a>



| Brine              | Use   |  $n$ | AAD (%) | Bias (%) | Max (%) |
|:-------------------|:------|-------:|--------:|---------:|--------:|
| NaCl 1.0684 mol/kg | check | $14$ | $0.1$ |  $0.0$ | $0.4$ |
| NaCl 2.195 mol/kg  | check | $14$ | $0.4$ | $+0.4$ | $0.6$ |
| NaCl 3.5714 mol/kg | check | $14$ | $2.2$ | $+2.2$ | $2.6$ |
| All                | check | $42$ | $0.9$ | $+0.9$ | $2.6$ |

Vapour pressure of water over NaCl brine, 298 to 363 K: bubble pressure of the brine with the CO$_2$ Storage package against Nasirzadeh et al. (2004) (deviation 100 (calc/exp - 1)).



The package takes Raoult’s law on the dissolved ions (no osmotic coefficient).



<a id="tab:storage_ksp"></a>



| Mineral | Source | Range ($^\circ$C) | 0 $^\circ$C | 25 $^\circ$C | 50 $^\circ$C | 100 $^\circ$C | 150 $^\circ$C |
|:---|:---|:---|---:|---:|---:|---:|---:|
| Calcite | Plummer and Busenberg 1982 | 0 to 90 | $-8.38$ | $-8.48$ | $-8.66$ | ($-9.27$) | ($-10.16$) |
|  | llnl.dat (PHREEQC) | 0 to 300 | $-8.37$ | $-8.53$ | $-8.73$ | $-9.28$ | $-10.06$ |
| Magnesite | Benezeth et al. 2011 | 50 to 200 | ($-7.40$) | ($-7.80$) | $-8.26$ | $-9.35$ | $-10.57$ |
|  | llnl.dat (PHREEQC) | 0 to 300 | $-7.62$ | $-8.08$ | $-8.53$ | $-9.46$ | $-10.51$ |
| Siderite | Benezeth et al. 2009 | 25 to 250 | ($-10.73$) | $-10.90$ | $-11.18$ | $-11.93$ | $-12.81$ |
|  | llnl.dat (PHREEQC) | 0 to 300 | $-10.30$ | $-10.57$ | $-10.86$ | $-11.55$ | $-12.43$ |
| Dolomite | Benezeth et al. 2018 | 50 to 253 | ($-16.77$) | ($-17.19$) | $-17.82$ | $-19.52$ | $-21.63$ |
|  | llnl.dat (PHREEQC) | 0 to 300 | $-17.78$ | $-18.23$ | $-18.74$ | $-20.01$ | $-21.66$ |
| Dawsonite | llnl.dat, K2 of Plummer and Busenberg | 0 to 200 | $-34.91$ | $-34.02$ | $-33.42$ | $-32.91$ | $-33.10$ |
|  | llnl.dat (PHREEQC) | 0 to 200 | $-34.89$ | $-34.04$ | $-33.44$ | $-32.83$ | $-32.89$ |

Solubility products of the carbonate minerals, $\log_{10} K_{sp}$ on the CO$_3^{2-}$ basis (MineralTrapping.cs), with the LLNL database of PHREEQC moved to the same basis for comparison. Values in parentheses lie outside the temperature range of the source.



The first row of each mineral is the package. llnl.dat values are moved to the CO$_3^{2-}$ basis with K2 and Kw of llnl.dat; the package dawsonite takes K2 of Plummer and Busenberg (1982). Dolomite against the 14 measured constants of Benezeth et al. (2018), Table 6: average deviation 0.35, bias -0.02, largest 1.03 log units.

###### Application domains

- **Saline aquifer injection:** $\ce{CO2}$ solubility, brine density and water content of the $\ce{CO2}$ phase at reservoir $T$, $P$ and salinity.

- **CO$_2$-EOR and injection wells:** $\ce{CO2}$–brine phase behaviour and the heat of solution in the energy balance.

- **Long-term storage:** saturation indices of carbonate minerals as a screen for mineral trapping (equilibrium only; no precipitation kinetics).

- **Well integrity:** pH and carbonate chemistry of the brine near the wellbore.

#### PC-SAFT for Polymers {#sec:pcsaft_polymers}

##### Overview {#overview-58}

The Perturbed-Chain SAFT equation of state  writes the residual Helmholtz energy of a mixture as the sum of a hard-chain reference, a dispersion contribution and, for hydrogen-bonding species, an association term :


<a id="eq:pcsaft_ares"></a>

\[
\tilde{a}^{\mathrm{res}} = \tilde{a}^{\mathrm{hc}} + \tilde{a}^{\mathrm{disp}}
    + \tilde{a}^{\mathrm{assoc}}
\]


where each molecule is a chain of $m$ tangent spheres of diameter $\sigma$ and dispersion energy $\varepsilon/k$. DWSIM extends the model to polymers by scaling the segment number with molar mass, by adding numerical safeguards that keep the solution stable at the very large segment numbers of a macromolecule, by seeding the liquid–liquid flash so that a polymer solution demixes without a manual estimate, and by supplying transport properties for polymer-containing phases. The polymer treatment follows Tumakaka et al.  and the parameter work of Tihic et al.  and Kontogeorgis and Folas .

##### Segment Number and Pure-Component Parameters

A polymer of number-average molar mass $M_n$ is a chain whose segment number grows linearly with the molar mass. DWSIM stores a molar-mass-specific ratio $(m/M)$ per repeat unit and computes


<a id="eq:pcsaft_msegment"></a>

\[
m = \left(\frac{m}{M}\right) M_n
\]


so a single parameter row covers any chain length; small molecules keep their tabulated absolute $m$. Only three pure-component parameters are needed per polymer: the ratio $(m/M)$ and the segment size $\sigma$ and energy $\varepsilon/k$, all referred to the repeat unit. Table [61](#tab:pcsaft_polymers) lists the built-in polymers. The critical constants of the injected pseudo-compound only seed the initial guesses; the PC-SAFT fugacity uses $(m/M)$, $\sigma$ and $\varepsilon/k$ exclusively.



<a id="tab:pcsaft_polymers"></a>



| Polymer | $m/M$ (mol/g) | $\sigma$ (Å) | $\varepsilon/k$ (K) | Association |
|:---|:--:|:--:|:--:|:---|
| Polyethylene (HDPE) | 0.0263 | 4.0217 | 252.0 | none |
| Polyethylene (LDPE) | 0.0263 | 4.0217 | 249.5 | none |
| Polypropylene | 0.02305 | 4.1000 | 217.0 | none |
| Polybutene | 0.0140 | 4.2000 | 230.0 | none |
| Polyisobutene | 0.02350 | 4.1000 | 265.5 | none |
| Polystyrene | 0.0190 | 4.1071 | 267.0 | none |
| Poly(vinyl acetate) | 0.03211 | 3.3972 | 204.65 | none |
| Polydimethylsiloxane | 0.0324 | 3.5310 | 204.9 | none |
| Poly(n-butyl methacrylate) | 0.0241 | 3.8840 | 264.7 | none |
| Polybutadiene | 0.0245 | 4.0970 | 288.84 | none |
| Poly($\alpha$-methylstyrene) | 0.0204 | 4.2040 | 354.05 | none |
| Poly(methyl methacrylate) | 0.0270 | 3.5530 | 264.60 | none |
| Poly(methyl acrylate) | 0.0292 | 3.5110 | 268.3 | none |
| Poly(ethylene glycol) | 0.0192 | 4.0890 | 322.2 | 4C/ether |

Built-in PC-SAFT polymer parameters (per repeat unit)



For most polymers $\sigma$ and $\varepsilon/k$ are taken as the high-molar-mass asymptotic values, which is accurate for the macromolecular range. For the glycols, $\sigma$ and $\varepsilon/k$ are genuinely molar-mass dependent, so the oligomer range requires molar-mass-specific parameters rather than the single shipped row.

##### Association with Site Multiplicity

For a hydrogen-bonding polymer the association term uses the site model of Chapman and Huang–Radosz . The association strength between site $A$ on molecule $i$ and site $B$ on molecule $j$ is


<a id="eq:pcsaft_delta"></a>

\[
\Delta^{A_i B_j} = d_{ij}^{3}\, g_{ij}^{\mathrm{hs}}\, \kappa^{A_i B_j}
    \left[ \exp\!\left(\frac{\varepsilon^{A_i B_j}}{kT}\right) - 1 \right]
\]


where $\kappa$ and $\varepsilon$ are the association volume and energy, and $g_{ij}^{\mathrm{hs}}$ the hard-sphere radial distribution function. Cross associations between unlike species use the standard combining rules, $\varepsilon^{A_i B_j} = \tfrac{1}{2}(\varepsilon^{A_i}+\varepsilon^{B_j})$ and a geometric-mean $\kappa$ scaled by the segment sizes.

The fraction $X^{A_i}$ of sites of type $A$ on molecule $i$ that are *not* bonded follows from the mass-action balance


<a id="eq:pcsaft_xa"></a>

\[
X^{A_i} = \left[ 1 + \sum_{j} \rho\, x_j \sum_{B_j}
    n_{B_j}\, X^{B_j}\, \Delta^{A_i B_j} \right]^{-1}
\]


where $\rho$ is the number density, $x_j$ the mole fraction, and $n_{B_j}$ the *multiplicity* of site type $B$, that is, how many identical sites of that type each molecule carries. The association contribution to the Helmholtz energy is then


<a id="eq:pcsaft_aassoc"></a>

\[
\tilde{a}^{\mathrm{assoc}} = \sum_i x_i \sum_{A_i} n_{A_i}
    \left[ \ln X^{A_i} - \frac{X^{A_i}}{2} + \frac{1}{2} \right] .
\]


The multiplicity $n$ lets a single donor and a single acceptor site type stand for many identical sites, which is what makes a long associating chain tractable: without it a molecule with hundreds of bonding sites would need an equally large site-fraction system. Three schemes are supported: *2B* (one donor and one acceptor), *4C* (two donors and two acceptors), and *4C/ether*, the poly(ethylene glycol) model of Kontogeorgis and Folas . In the 4C/ether scheme the two hydroxyl end groups give two donor and two acceptor sites, and each ether oxygen along the backbone adds one acceptor site, with the count growing with molar mass as


<a id="eq:peg_ether"></a>

\[
N_{\mathrm{ether}} = 0.022\, M_n - 1.409 .
\]


Poly(ethylene glycol) is therefore represented with two donor sites and $2 + N_{\mathrm{ether}}$ acceptor sites, all carrying the same association volume $\kappa = 0.0235$ and energy $\varepsilon/k = 2080$ K.

##### Numerical Treatment at High Segment Numbers

Three safeguards keep the calculation stable when $m$ is large.

###### Logarithmic fugacity

The fugacity coefficient of a macromolecule underflows to zero in double precision, because $\ln\varphi_i$ is of the order of the segment number and can reach several hundred to a few thousand in magnitude. DWSIM therefore carries the *logarithm* of the fugacity coefficient throughout, and the phase-split ratio is obtained as


<a id="eq:pcsaft_kvalue"></a>

\[
K_i = \exp\!\left( \ln\varphi_i^{\,L} - \ln\varphi_i^{\,V} \right)
\]


rather than as a ratio $\varphi_i^{L}/\varphi_i^{V}$ of two numbers that both round to zero.

###### Bracketed density root

The reduced density (packing fraction $\eta$) is found by bracketing the sign changes of $P - P_{\mathrm{calc}}(\eta)$ over the physical range $(0,\,0.7405)$ and selecting the liquid (highest-$\eta$) or vapour (lowest-$\eta$) root. This avoids the spurious low-density roots and the close-packing singularity that a squared-objective minimiser can fall into for a polymer-rich phase.

###### Bounded site-fraction solve

The site fractions of Equation [\[eq:pcsaft_xa\]](#eq:pcsaft_xa) are solved by damped successive substitution, which keeps every fraction in $(0,1]$ by construction. An unconstrained minimiser can return a negative fraction and turn the $\ln X^{A_i}$ terms into a non-number, especially for a high-segment associating chain.

##### Liquid–Liquid Equilibrium

A polymer solution demixes at extreme dilution on a mole basis (the polymer mole fraction at the phase boundary can be of order $10^{-5}$), which the ordinary stability search from pure-component estimates does not reach. DWSIM seeds the split from the equation-of-state *spinodal*: the limit of intrinsic stability is located from the analytical composition derivative of the logarithmic fugacity coefficient, and the two liquid estimates are placed just outside that window. The split is then converged by directly descending the two-phase Gibbs energy, which walks away from the trivial (single-phase) solution that a plain successive-substitution or residual-Newton step collapses onto. Phase identity is judged on a *mass* basis, since the two liquids of a polymer system are almost indistinguishable by mole fraction but well separated by weight fraction. With this seeding the miscibility gap is reached automatically from both the dedicated Simple LLE flash and the general Nested Loops (VLLE) flash used by a material stream, with no manual phase estimate.

##### Properties of Polymer-Containing Phases

The density of a polymer phase comes from the equation of state, $\rho = PM/(ZRT)$ with the compressibility $Z$ from PC-SAFT, rather than from a low-molar-mass correlation. Transport properties use the user-supplied data of each compound when present. When a polymer carries no data, a per-polymer estimate is used instead of a corresponding-states correlation, since the latter relies on the polymer critical constants, which are only placeholders: liquid thermal conductivity from a Van Krevelen reduced curve anchored at the value at 298 K, and surface tension from a reference value at 293 K with a linear temperature slope. Mixture transport properties are combined on a mass-fraction basis, logarithmically (Arrhenius) for viscosity, whose values span orders of magnitude, and linearly for thermal conductivity and surface tension. A mole-fraction average would let the trace polymer mole fraction erase the polymer contribution.

##### Polydispersity

A real polymer is a mixture of chain lengths, not a single molar mass. The Polymer Characterization tool (on the Tools menu) discretizes a molar-mass distribution into a small number of pseudo-components of the same chemistry and different molar mass, so a polydisperse sample can be modelled directly. Two distributions are provided, Schulz-Zimm (a Gamma distribution) and log-normal; both are entered through the number-average molar mass $M_n$ and the polydispersity index $M_w/M_n$. The cut molar masses and mole fractions are chosen so that the number-average and weight-average molar mass of the cuts equal the targets. The cuts share the base polymer’s parameters, differing only in molar mass (hence segment number), and the liquid–liquid flash resolves them into a dilute and a concentrated phase, fractionating the chain lengths between the two.

##### Copolymers

PC-SAFT also models random and alternating copolymers, following Gross, Spuhl, Tumakaka and Sadowski . A copolymer is treated at the level of its repeat-unit *segments*: the hard-chain and dispersion terms are summed over segment types rather than over whole molecules, so a copolymer of repeat units R and S reuses the pure-component parameters of the two homopolymers. It is defined by the two repeat units, the copolymer composition (the mass fraction of each repeat unit) and the number-average molar mass; the segment number of each type follows from $m_{iR} = w_{iR}\,M\,(m/M)_R$, and the fraction of bonds between like and unlike segments is fixed by the composition. The unlike segment–segment interactions use the same combining rules as the homopolymers, with the segment–segment binary interaction parameters, including an internal repeat-unit correction, read from the interaction-parameter table by the segment CAS numbers (for example the ethylene–propylene correction $k_{ij} = -0.009$ of poly(ethylene-co-propylene)). A copolymer is built from the Polymer Characterization tool by choosing the two repeat units, the mass fraction and the sequence, and then takes part in vapour–liquid and liquid–liquid equilibria like any other compound.

##### Validation

###### Non-associating polymer solutions (liquid–liquid)

Table [62](#tab:pcsaft_polymer_val) compares the model against literature cloud data. The polypropylene/$n$-pentane and high-density-polyethylene/ethylene cloud pressures reproduce the measurements of Tumakaka et al.  to within a few bar and a few tens of bar respectively, and the poly(methyl methacrylate)/1-chlorobutane upper critical solution temperature matches the dome of Kontogeorgis and Folas  to within about two kelvin.



<a id="tab:pcsaft_polymer_val"></a>



| System | Quantity | Experiment | Model |
|:---|:---|:---|:---|
| PP/$n$-pentane ($M_w$ 50.4 kg/mol) | cloud $P$, 5 wt%, 177/187/197 °C | 47/59/73 bar | 48/62/72 bar |
| HDPE/ethylene ($M_w$ 118 kg/mol) | cloud $P$, 5 wt%, 140/150/170 °C | 1850/1780/1650 bar | 1850/1750/1650 bar |
| PMMA/1-chlorobutane ($M_w$ 36.5 kg/mol) | UCST ($k_{ij} = -0.0032$) | $\approx 281$ K | $\approx 283$ K |

PC-SAFT polymer LLE validation



###### Associating systems (cross-association)

The association term drives every mixture in which two components hydrogen-bond, including the aqueous polymer solutions, and is validated separately. Unlike sites cross-associate between a donor and an acceptor only, and the chemical potential of association follows from the association Helmholtz energy whenever two or more compounds associate, so that the model satisfies the Gibbs-Duhem relation. Table [63](#tab:pcsaft_assoc_val) compares bubble points of water with methanol and ethanol, computed with the water–alcohol $k_{ij}$ shipped in the parameter table (methanol $-0.06$, ethanol $-0.035$, 1-propanol $-0.04$), with isobaric data of Yang et al.  and Kamihama et al. . Bubble temperatures agree within 0.7 K and vapour compositions within 0.03 in mole fraction. With these $k_{ij}$ the three alcohols stay miscible with water over the whole composition range at 298 K, as they are. The two-site water of the parameter table cannot reproduce at the same time the vapour-liquid equilibrium and the infinite-dilution activity coefficients of the alcohols in water; the $k_{ij}$ follow the vapour-liquid data. The same cross-association gives poly(ethylene glycol) in water the correct *sign* of the deviation from Raoult’s law: the water activity is suppressed below the ideal value at every composition and molar mass, the negative deviation that makes the polymer water-soluble.



<a id="tab:pcsaft_assoc_val"></a>



| System | $x_w$ | $T$ exp. (K) | $T$ model (K) | $y_w$ exp. | $y_w$ model |
|:---|---:|---:|---:|---:|---:|
| methanol + water, 37.5 kPa | 0.181 | 316.24 | 316.48 | 0.078 | 0.071 |
|  | 0.550 | 322.68 | 322.63 | 0.231 | 0.218 |
|  | 0.926 | 337.78 | 337.72 | 0.596 | 0.623 |
| ethanol + water, 101.3 kPa | 0.099 | 351.26 | 350.56 | 0.100 | 0.083 |
|  | 0.430 | 352.39 | 352.75 | 0.312 | 0.312 |
|  | 0.910 | 359.70 | 360.01 | 0.559 | 0.589 |

PC-SAFT cross-association validation: bubble points of water + alcohol (liquid water mole fraction $x_w$, vapour water mole fraction $y_w$)



###### Copolymers

The segment-level copolymer model reproduces the two homopolymer limits and interpolates between them. A poly(ethylene-co-propylene) solution in $n$-pentane demixes into a polymer-rich phase whose composition lies between those of the polyethylene and polypropylene solutions at the same temperature and pressure, using the shipped ethylene–propylene internal parameter and the homopolymer–solvent parameters.

##### Validity Range and Limitations

The polymer model is accurate for non-associating and weakly interacting polymer–solvent systems, where a single small binary interaction parameter $k_{ij}$ captures the mixture, and for the vapour–liquid equilibrium of such systems. A few limitations should be kept in mind. Strongly hydrogen-bonding aqueous systems are reproduced only semi-quantitatively. Cross-association is included and gives the correct sign of the deviation from ideality, but the arithmetic-mean combining rule sets the cross-association energy to the average of the two self-association energies, which is weaker than the water self-association; so the water-rich vapour–liquid branch of poly(ethylene glycol) in water measured by Herskowitz and Gottlieb  still needs a fitted $k_{ij}$ for quantitative agreement, the experimental closed-loop liquid–liquid behaviour is not reproduced, and water–alcohol vapour–liquid equilibrium needs the small $k_{ij}$ shipped for the common alcohols. Oligomers require molar-mass-specific $\sigma$ and $\varepsilon/k$ rather than the asymptotic shipped values. A polydisperse polymer is entered as several pseudo-components, which the Polymer Characterization tool generates from a Schulz-Zimm or log-normal distribution, and the copolymer treatment is limited to two repeat units.

#### Ionic Liquids (PC-SAFT) {#sec:ionic_liquids}

##### Overview {#overview-59}

The Ionic Liquids (PC-SAFT) property package (DWSIM Patreon) describes ionic liquids used as physical solvents for $\ce{CO2}$, $\ce{H2S}$, $\ce{CH4}$, $\ce{N2}$, $\ce{H2}$ and water: the removal of $\ce{CO2}$ from natural gas, syngas or biogas, where the gas dissolves in the liquid without reacting and is released again by lowering the pressure or by mild heating . It is the PC-SAFT package of Section [6.18](#sec:pcsaft_polymers) with two parameter tables of its own and a viscosity rule for phases that hold an ionic liquid. The equation of state, the flash and the caloric properties are those of the PC-SAFT package.

Each ionic liquid is a single neutral compound, the ion pair, with two association sites (scheme 2B: one donor and one acceptor). The package carries 29 ionic liquids:

- imidazolium tetrafluoroborates \[emim\], \[bmim\], \[hmim\] and \[omim\]\[BF$_4$\];

- hexafluorophosphates \[bmim\], \[hmim\] and \[omim\]\[PF$_6$\];

- bis(trifluoromethylsulfonyl)imides \[emim\], \[bmim\], \[hmim\], \[omim\] and \[dmim\]\[Tf$_2$N\], \[bmpyrr\]\[Tf$_2$N\], \[N4111\]\[Tf$_2$N\] and \[P66614\]\[Tf$_2$N\];

- trifluoromethanesulfonates \[emim\], \[bmim\] and \[hmim\]\[OTf\];

- acetates \[emim\] and \[bmim\]\[Ac\];

- dicyanamides, thiocyanates and tricyanomethanides of \[emim\] and \[bmim\] (\[DCA\], \[SCN\], \[TCM\]);

- , \[bmim\]\[MeSO$_4$\] and \[P66614\]\[Cl\].

The compounds are installed with the package (one compound file each, in the `addcomps` folder) and appear in the compound list like any other. The package also supplies PC-SAFT parameters for $\ce{H2}$, which the general PC-SAFT table lacks.

The acetates absorb $\ce{CO2}$ and $\ce{H2S}$ chemically, and the package does not represent that reaction: their $\ce{CO2}$ and $\ce{H2S}$ pairs carry no fitted parameter and the predicted solubilities of these two gases in the acetates are far below the measured ones. The acetates can be used with the other solutes.

##### Pure-Compound Parameters

The segment number $m$, segment diameter $\sigma$ and dispersion energy $\varepsilon/k$ of each ionic liquid were fitted to the liquid densities of the NIST ILThermo database  over temperature and pressure (up to 400 points per ionic liquid, 250–480 K and up to 300 MPa) and, with a small weight, to the enthalpies of vaporization. The association energy and volume are the same for every ionic liquid, $\varepsilon^{AB}/k = 3134$ K and $\kappa^{AB} = 0.026$, the medians of the parameters fitted by Bülow et al. to twelve \[C$_2$mim\] ionic liquids .

For the imidazolium families (\[BF$_4$\], \[PF$_6$\], \[Tf$_2$N\], \[OTf\], \[Ac\], \[DCA\], \[SCN\], \[TCM\]) the fit is joint: $m$, $m\sigma^3$ and $m\varepsilon/k$ are linear in the molar mass with the slopes per $\ce{CH2}$ group of the $n$-alkanes of Gross and Sadowski , and only the three intercepts of each family are regressed. The parameters then change smoothly along a homologous series. The other ionic liquids were fitted one by one; \[emim\]\[EtSO$_4$\] and \[bmim\]\[MeSO$_4$\] were held at $\varepsilon/k \le 400$ K, because a higher dispersion energy gives the equation of state a second, spurious dense liquid root. Table (tab.) lists the parameters and the density deviations.

Hydrogen is a single segment ($m = 1$) with $\sigma = 2.8183$ Å and $\varepsilon/k = 20.168$ K, fitted to the normal-hydrogen densities of the NIST Chemistry WebBook (equation of state of Leachman et al. ), 250–450 K and 1–30 MPa.

###### Compound files

The temperature correlations of the compound files (liquid density, viscosity in the Vogel-Fulcher-Tammann form, heat capacity, thermal conductivity and surface tension) were fitted to the ILThermo data at 0.1 MPa. In this package the liquid density comes from the equation of state; the density correlation serves other property packages. The critical constants, acentric factor, normal boiling point and vapour pressure in the files are those of the PC-SAFT model fluid: ionic liquids decompose long before they boil, and these values only feed the initial estimates of the flash. The model vapour pressure is higher than the measured one (by about two orders of magnitude for \[emim\]\[Tf$_2$N\] at 400 K ) and still negligible in a process: the solvent does not leave with the gases. The ideal-gas heat capacity is the measured liquid heat capacity less the residual heat capacity of PC-SAFT, so that the package reproduces the liquid heat capacity (Table (tab.)).

###### Liquid viscosity

A liquid phase that holds an ionic liquid takes a logarithmic blend of the compound viscosities weighted by mass fraction. A blend by mole fraction would let a few percent of dissolved gas, light and abundant on a molar basis, lower the viscosity of the solvent far more than measured. Phases without an ionic liquid use the rule of the PC-SAFT package.

##### Binary Interaction Parameters

The binary interaction parameter may depend on temperature,


<a id="eq:il_kij"></a>

\[
k_{ij}(T) = k_{ij} + k_{ij,T}\,(T - 298.15~\mathrm{K})
\]


and was fitted for each pair of an ionic liquid with $\ce{CO2}$, $\ce{CH4}$, $\ce{N2}$, $\ce{H2}$, $\ce{H2S}$ and water to the ILThermo gas solubilities (liquid mole fraction at $T$ and $P$), Henry constants and water vapour-liquid data. The slope $k_{ij,T}$ was fitted when a pair had data over at least 30 K and 15 points; otherwise $k_{ij}$ is constant. About a quarter of the data sets of each pair (one point in four for a pair with a single set) were held out of the fit and used only to check it. Sets that disagreed with the rest by more than a factor of 1.6 after a first fit were left out; for $\ce{H2}$ only pressures of 10 bar and above were used, because the low-pressure Henry constants of different laboratories differ by a factor of two. Water associates with the ionic liquid through the donor-acceptor cross-association rule of the PC-SAFT package .

A pair of $\ce{CH4}$, $\ce{N2}$, $\ce{H2}$ or $\ce{H2S}$ without data takes the median of the fitted pairs of the same gas (the generic parameter). $\ce{CO2}$ and water pairs without data are left at $k_{ij} = 0$. Table (tab.) summarizes the pairs; the parameter of every pair, with its deviations, is in the fitting folder of the package source.

##### Validation

Tables (tab.) to [64](#tab:il_co2_heat) compare the package with experimental data. They were produced by the script `validate.py` in the fitting folder of the package source (`Fitting`), which calls the shipped package over every data point. The same folder holds the data files (NIST ILThermo sets with their citation, DOI and the use of each row), the fit scripts, which reproduce the shipped parameters, and the point-by-point results.

###### Pure liquids

Over all the ionic liquids the liquid density is within 0.47 % of the data the parameters were fitted to and within 0.52 % of the data held out; the median deviation is 4.3 % for the viscosity, 1.1 % for the heat capacity, 1.7 % for the thermal conductivity and 1.5 % for the surface tension. The largest viscosity deviations, \[P66614\]\[Cl\] (21 %) and the acetates, follow the spread between laboratories and the water content of the samples. One \[bmim\]\[MeSO$_4$\] heat capacity set lies about 45 % below the others, and the correlation of that compound follows it partly (10.7 % deviation).

###### Gases and water

The median deviation of the $\ce{CO2}$ solubility is 9.5 % on the fitted data and 16 % on the data held out; part of the difference is disagreement between laboratories. $\ce{H2S}$ and $\ce{H2}$ are reproduced within a few percent, $\ce{CH4}$ and $\ce{N2}$ within about 6 to 14 %. For water the median deviation of the partial pressure is 9.0 % fitted and 12.9 % held out. The pooled water deviation of the held-out data (47.6 %) comes from three sets that disagree with the fitted ones (\[bmim\]\[BF$_4$\] up to 448 K, \[emim\]\[Ac\] and \[emim\]\[EtSO$_4$\]).

The heat of absorption of $\ce{CO2}$ follows from the temperature dependence of the Henry constant of the package (Table [64](#tab:il_co2_heat)) and agrees with the data within 1 to 2 kJ/mol for most ionic liquids. \[omim\]\[Tf$_2$N\] is the exception: its $k_{ij,T}$ was fitted to the solubility alone and gives too weak a temperature dependence.



<a id="tab:il_co2_heat"></a>



| Ionic liquid           | Package | Data: sets |    Data: range | Data: median |
|:-----------------------|--------:|-----------:|---------------:|-------------:|
| \[emim\]\[BF$_4$\]   |   -11.2 |          1 |          -13.3 |        -13.3 |
| \[bmim\]\[BF$_4$\]   |   -14.5 |          4 |  -14.9 to -9.7 |        -14.1 |
| \[omim\]\[BF$_4$\]   |   -10.2 |          2 | -13.8 to -10.9 |        -12.4 |
| \[bmim\]\[PF$_6$\]   |   -13.9 |          6 | -15.5 to -13.3 |        -14.2 |
| \[emim\]\[Tf$_2$N\]  |   -13.9 |          5 | -16.7 to -13.1 |        -14.1 |
| \[bmim\]\[Tf$_2$N\]  |   -13.0 |          4 | -14.5 to -11.3 |        -12.5 |
| \[hmim\]\[Tf$_2$N\]  |   -12.2 |          1 |          -12.0 |        -12.0 |
| \[omim\]\[Tf$_2$N\]  |    -5.5 |          1 |          -12.0 |        -12.0 |
| \[emim\]\[EtSO$_4$\] |   -13.6 |          1 |          -13.0 |        -13.0 |

Heat of absorption of CO$_2$ (kJ/mol) from the temperature dependence of the Henry constant: the package (293.15-323.15 K) against the NIST ILThermo Henry constant sets of the same ionic liquid (range over the sets).



##### Scope and Limitations

- Physical absorption only. The chemical absorption of $\ce{CO2}$ and $\ce{H2S}$ by the acetates, and by other basic anions, is not represented.

- Below the critical temperature of $\ce{CO2}$ and above its vapour pressure, the $\ce{CO2}$-rich phase is a dense liquid, and the vapour-liquid flash reports it as the vapour; the composition of the ionic-liquid-rich phase is not affected. Above about 190 bar the model may give a single phase, its own mixture critical region.

- Liquid-liquid equilibrium of water with the hydrophobic ionic liquids is not fitted. Near a water mole fraction of 0.4 at high temperature the model may predict two liquid phases where the data show one.

- The generic parameters of pairs without data are estimates. A $\ce{CO2}$ or water pair without data has $k_{ij} = 0$.

- The association energy and volume are generic, so the results for water rely on the fitted $k_{ij}$; outside the range of the water data the deviations grow.

- Ionic liquids decompose well above the temperatures of a physical absorption process, but their long-term stability lies 100 to 150 K below the decomposition onset measured by thermogravimetry . Keep regeneration below about 150 °C.

##### Adding an Ionic Liquid

An ionic liquid that the package does not carry can be added to a simulation. Its parameters are entered in the package editor and saved with the flowsheet; the parameter tables of the package itself are not edited.

1.  **Create the compound file.** The simplest start is a copy of one of the compound files of the package (folder `addcomps` of the DWSIM installation, files `IL_*.json`). Change the name and give the compound a CAS number of its own: the package finds every parameter by CAS number, so it must not repeat one already in use. Keep the tag `Ionic liquid`, which gives the liquid phases the mass-fraction viscosity rule. Enter the molar mass and, where data exist, the correlations (liquid density, viscosity, heat capacity, thermal conductivity, surface tension, ideal-gas heat capacity). The compound creator of the Classic interface can also export a new compound to a file of this kind. The PC-SAFT fields of the compound file are not read by the package.

2.  **Bring it into the simulation.** In the simulation settings, use the option to import a compound from a JSON file (Classic and cross-platform interfaces). Alternatively, place the file in the `addcomps` folder of the installation, or (Classic interface) add it to the list of compound files in the general options, and restart DWSIM; the compound then appears in the compound list.

3.  **Enter the PC-SAFT parameters.** Open the editor of the Ionic Liquids (PC-SAFT) package. The new compound has a row of its own with zeros. Enter $m$, $\sigma$ (Å) and $\varepsilon/k$ (K), and the association volume and energy; the values of the shipped ionic liquids, $\kappa^{AB} = 0.026$ and $\varepsilon^{AB}/k = 3134$ K, are a consistent choice. A compound left with zero parameters stops the calculation with a message about missing PC-SAFT parameters.

4.  **Enter the binary parameters.** In the same editor, enter $k_{ij}$ for each pair of the new ionic liquid with the other compounds, and $k_{ij,T}$ (1/K) where the parameter depends on temperature (Eq. [\[eq:il_kij\]](#eq:il_kij)). Pairs left blank have $k_{ij} = 0$. Without data, the generic parameters of the package are a reasonable start: $k_{ij} = -0.0615$ and $k_{ij,T} = 8.90\times10^{-4}$ K$^{-1}$ with $\ce{CH4}$, $-0.0275$ and 0 with $\ce{N2}$, $-0.380$ and $1.21\times10^{-3}$ K$^{-1}$ with $\ce{H2}$, $-0.0151$ and $5.67\times10^{-5}$ K$^{-1}$ with $\ce{H2S}$. For $\ce{CO2}$ and water the fitted values vary too much from one ionic liquid to another for a generic value; take those of the closest shipped ionic liquid.

5.  **Save the flowsheet.** The parameters live in the simulation file. To reuse them, keep a template flowsheet with the compound and its parameters.

Fitting $m$, $\sigma$ and $\varepsilon/k$ to liquid densities over temperature and pressure, as for the shipped compounds, gives the best results; the fit scripts in the `Fitting` folder of the package source can be used for that. With the association fixed at the values above, the three parameters of an ionic liquid of a shipped family can also be interpolated from the members of the family in Table (tab.), which vary linearly with the molar mass.

#### Patel–Teja Equation of State {#sec:pt}

##### Overview {#overview-60}

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

##### Overview {#overview-61}

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

##### Overview {#overview-62}

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

Common association schemes and their site types are listed in Table [65](#tab:cpa_schemes).



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

##### Overview {#overview-63}

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

##### Overview {#overview-64}

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

##### Overview {#overview-65}

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

##### Overview {#overview-66}

SAFT-VRQ Mie  extends SAFT-VR Mie to quantum-mechanical effects relevant for light molecules such as $\ce{H2}$, $\ce{D2}$, $\ce{He}$, and $\ce{Ne}$. Quantum corrections are incorporated via the Feynman–Hibbs (FH) perturbation approach .

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


A second-order correction $u^{\mathrm{FH2}}$ is available for the lightest species ($\ce{H2}$, $\ce{He}$) . The corrected potential is then used in place of $u^{\mathrm{Mie}}$ in all SAFT-VR Mie perturbation integrals.

##### Dimensionless Quantum Parameter

The strength of the quantum correction is characterised by the de Broglie parameter:


\[
Q^{2} = \frac{\hbar^{2}}{m\,\varepsilon\,\sigma^{2}\,k}
\]


Larger $Q$ (smaller mass, smaller potential well) indicates stronger quantum effects. For $\ce{H2}$ at 298 K the correction to the second virial coefficient exceeds 20% .

#### Modified Benedict–Webb–Rubin Equation (MBWR) {#sec:mbwr}

##### Overview {#overview-67}

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

##### Overview {#overview-68}

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

