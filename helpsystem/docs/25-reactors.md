# Reactors

DWSIM’s reactor models solve systems of chemical reactions coupled with material and energy balances. Six reactor types are available, each suited to different classes of reactions and modeling needs:

- Continuous Stirred Tank Reactor (CSTR)

- Plug Flow Reactor (PFR)

- Gibbs Minimization Reactor

- Equilibrium Reactor

- Conversion Reactor

- Bioreactor

To run a simulation of a reactor, the user needs to define the chemical reactions which will take place in the reactor. This is done through the **Reactions Manager**, accessible through the **Simulation Settings** panel (Classic UI) or from **Tools \> Reaction Manager** (Cross-Platform UI), which is also the **Reactions** tab of the Simulation Settings window.

Reactions can be of Equilibrium, Conversion, Kinetic, or Heterogeneous Catalytic type. One or more reactions are grouped into a Reaction Set, which is then assigned to a reactor block. A reactor solves only the reactions contained in its assigned reaction set.

Equilibrium reactions are characterized by an equilibrium constant $K$ . The equilibrium constant can be obtained from the standard Gibbs energy of reaction, from a user-defined expression (e.g., a polynomial in temperature), or as a fixed numerical value. Equilibrium reactions are used in the Equilibrium and Gibbs reactors.

Conversion reactions are defined by the fractional conversion of a designated base reactant. The conversion can be a fixed value or a function of the system temperature. Conversion reactions are used exclusively in the Conversion reactor.

Kinetic reactions are defined by rate expressions (typically power-law or Arrhenius forms). These reactions are supported by the CSTR and PFR reactors.

Heterogeneous Catalytic reactions in DWSIM must obey the Langmuir–Hinshelwood mechanism, where compounds react over a solid catalyst surface. In this model, reaction rates are a function of catalyst amount (i.e. mol/kg cat.s). These reactions are supported by the PFR and CSTR reactors.

#### Kinetic Reactors

DWSIM provides two kinetic reactor models for simulating systems governed by reaction rate expressions: the Continuous Stirred-Tank Reactor (CSTR) and the Plug Flow Reactor (PFR). Both support kinetic and heterogeneous catalytic reactions and can operate in multiple thermal modes.

##### Overview {#overview-19}

###### Supported Reaction Types {#supported-reaction-types .unnumbered}

Both the CSTR and PFR support the following reaction types:

- Kinetic reactions — defined by forward and reverse rate constants and power-law concentration dependences.

- Heterogeneous catalytic reactions — defined by a Langmuir–Hinshelwood rate expression (numerator/denominator form) with rates proportional to catalyst mass.

- Script-based kinetics — user-defined IronPython scripts that return the reaction rate given concentrations and temperature.

Equilibrium and conversion reactions are not supported by these two reactor models; they are handled by the Equilibrium, Gibbs, and Conversion reactor unit operations.

###### Reaction Rate Expressions {#reaction-rate-expressions .unnumbered}

####### Kinetic Reactions {#kinetic-reactions .unnumbered}

For a kinetic reaction with base component B, the net volumetric rate of reaction is



\[
r=k_{f}\prod_{i}\bigl(C_{i}\,\alpha_{i}\bigr)^{n_{f,i}}-k_{r}\prod_{i}\bigl(C_{i}\,\alpha_{i}\bigr)^{n_{r,i}}
\]


where $C_{i}$ is the molar concentration of component $i$ in the reaction phase (mol/m³), $alpha_{i}$ is a unit conversion factor (see Concentration Basis and Conversion Factors), $n_{f,i}$ and $n_{r,i}$ are the forward and reverse kinetic orders, and $k_{f}$ , $k_{r}$ are the forward and reverse rate constants.







Arrhenius form When the Arrhenius option is selected, the rate constant is computed as





\[
k=A\exp\!\left(-\frac{E_{a}}{R\,T}\right)
\]


where $A$ is the pre-exponential factor, $E_{a}$ the activation energy (J/mol), $R$ = 8.314 J/(mol·K) the universal gas constant, and $T$ the reaction temperature (K).







User-defined expression Alternatively, $k_{f}$ or $k_{r}$ can be given as an arbitrary analytical expression of temperature $T$ , compiled and evaluated at run time.

Temperature limits If $T<T_{min}$ or $T>T_{max}$ (user-specified bounds), both rate constants are set to zero, effectively disabling the reaction outside the valid range.



####### Heterogeneous Catalytic Reactions {#heterogeneous-catalytic-reactions .unnumbered}

For reactions following the Langmuir–Hinshelwood mechanism, the rate is expressed as



\[
r=\frac{f_{\text{num}}(R_{1},R_{2},\ldots,P_{1},P_{2},\ldots,T)}{f_{\text{den}}(R_{1},R_{2},\ldots,P_{1},P_{2},\ldots,T)}
\]


where $R_{j}$ and $P_{j}$ denote the concentrations of the j-th reactant and product, respectively, and $T$ is the temperature. Both the numerator and denominator are user-supplied analytical expressions. The variables $R_{1}$ , $R_{2}$ , ... and $P_{1}$ , $P_{2}$ , ... are assigned in the order the components appear in the reaction definition.

###### Component Production Rates {#component-production-rates .unnumbered}

Once the base reaction rate r is determined and converted to SI units, the molar production (or consumption) rate of each component i participating in reaction j is



\[
R_{i,j}=\begin{cases}
\dfrac{\nu_{i}}{|\nu_{B}|}\;r_{j}\;V_{r} & \text{(kinetic reaction)}\\[10pt]
\dfrac{\nu_{i}}{|\nu_{B}|}\;r_{j}\;W_{\text{cat}} & \text{(heterogeneous catalytic)}
\end{cases}
\]


where $\nu_{i}$ is the stoichiometric coefficient of component $i$ (negative for reactants, positive for products), $\nu_{B}$ is the stoichiometric coefficient of the base component, $V_{r}$ is the reaction phase volume (m³), and $W_{cat}$ is the catalyst mass (kg).

###### Reaction Heat {#reaction-heat .unnumbered}

The heat released or absorbed by each reaction is calculated as



\[
\dot{Q}_{\text{rxn},j}=\Delta H_{\text{rxn},j}\;\dot{\xi}_{j}\times10^{-3}
\]


where $\Delta H_{rxn,j}$ is the user-supplied heat of reaction (kJ/kmol) and $\xi_{j}$ is the molar extent of reaction j (mol/s). The factor $10^{-3}$ converts from kmol to mol basis.

###### Concentration Basis and Conversion Factors {#concentration-basis-and-conversion-factors .unnumbered}

Internally, concentrations are always stored as molar concentrations (mol/m³). The user may, however, define reaction rate expressions in a different basis. The conversion factor $\alpha_{i}$ applied to component $i$ transforms the internal molar concentration to the user-selected basis according to the following table.



<a id="tab:convfactors"></a>



| **Basis** | **Vapor** | **Liquid** | **Mixture** |
|:---|:---|:---|:---|
| Molar concentration | $1$ | $1$ | $1$ |
| Molar fraction | $\dfrac{ZRT}{P}$ | $\dfrac{\bar{M}}{1000\,\rho}$ | $\dfrac{\bar{M}}{1000\,\rho}$ |
| Mass concentration | $\dfrac{1000}{\bar{M}}$ | $\dfrac{1000}{\bar{M}}$ | $\dfrac{1000}{\bar{M}}$ |
| Mass fraction | $\dfrac{ZRT}{P}\,\dfrac{1000}{\bar{M}}$ | $\dfrac{1000\,\rho}{\bar{M}}$ | $\dfrac{1000\,\rho}{\bar{M}}$ |
| Activity | — | $\dfrac{\gamma_i \bar{M}}{1000\,\rho}$ | — |
| Fugacity | $\gamma_i Z R T$ | $\dfrac{\gamma_i \bar{M}\,P}{1000\,\rho}$ | $\dfrac{\gamma_i \bar{M}\,P}{1000\,\rho}$ |
| Partial pressure | $ZRT$ | — | — |

Concentration conversion factors $\alpha_i$ for different reaction bases. $Z$: compressibility factor; $\bar{M}$: phase average molecular weight (kg kmol$^{-1}$); $\rho$: phase density (kg m$^{-3}$); $\gamma_i$: activity/fugacity coefficient of component $i$; $P$: pressure (Pa).



###### Reaction Phases and Reaction Volume {#reaction-phases-and-reaction-volume .unnumbered}

Each reaction is assigned a reaction phase, which determines the volume in which the reaction takes place and the source of component concentrations.



<a id="tab:rxn_phases"></a>



| **Phase** | **Reaction Volume $V_r$** | **Concentration Source** |
|:---|:---|:---|
| Liquid | $\dfrac{Q_L}{Q_L + Q_S}\,V$ $\dfrac{Q_L}{Q}\,V$ | Liquid-phase molarity |
| Vapour | $V_h$ $\dfrac{Q_V}{Q}\,V$ | Vapour-phase molarity |
| Mixture | $V + V_h$ | $C_i = \dot{n}_i / Q$ |
| Solid | $\dfrac{Q_S}{Q_L + Q_S}\,V$ $\dfrac{Q_S}{Q}\,V$ | $C_i = \dot{n}_{i,S} / Q_S$ |
| Vapour–Solid | $\dfrac{Q_S}{Q_L + Q_S}\,V$ $\dfrac{Q_S}{Q}\,V$ | $C_i = (\dot{n}_{i,V} + \dot{n}_{i,S}) / (Q_V + Q_S)$ |
| Liquid–Solid | $V$ | $C_i = (\dot{n}_{i,L} + \dot{n}_{i,S}) / (Q_L + Q_S)$ |

Reaction volume $V_r$ and concentration source for each reaction phase. $V$: reactor liquid volume; $V_h$: headspace volume; $Q_L, Q_V, Q_S, Q$: volumetric flow rates of liquid, vapour, solid, and total mixture.



In single-outlet mode, the reaction volume is the fractional share of the total reactor volume $V$ (using total flow $Q$ as denominator). In two-outlet mode, the liquid volume $V$ is partitioned between liquid and solid using their flow fractions, and the vapour reaction takes place in the headspace $V_{h}$ .

##### Continuous Stirred-Tank Reactor (CSTR)

The CSTR models an ideally mixed reactor where the composition and temperature inside the vessel are uniform and equal to the outlet conditions. In DWSIM, the steady-state solution is obtained by a pseudo-transient algorithm that integrates the unsteady molar balance forward in time until steady state is reached.

###### Input Parameters {#input-parameters-12 .unnumbered}



<a id="tab:cstr_params"></a>



<table>
<caption>CSTR input parameters.</caption>
<thead>
<tr>
<th style="text-align: left;"><strong>Parameter</strong></th>
<th style="text-align: left;"><strong>Symbol</strong></th>
<th style="text-align: left;"><strong>Unit</strong></th>
<th style="text-align: left;"><strong>Description</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">Reactor volume</td>
<td style="text-align: left;"><span class="math inline">\(V\)</span></td>
<td style="text-align: left;">m<span class="math inline">\(^3\)</span></td>
<td style="text-align: left;">Active liquid/solid reaction volume</td>
</tr>
<tr>
<td style="text-align: left;">Headspace</td>
<td style="text-align: left;"><span class="math inline">\(V_h\)</span></td>
<td style="text-align: left;">m<span class="math inline">\(^3\)</span></td>
<td style="text-align: left;">Vapour-phase volume (above liquid level)</td>
</tr>
<tr>
<td style="text-align: left;">Catalyst amount</td>
<td style="text-align: left;"><span class="math inline">\(W_{\text{cat}}\)</span></td>
<td style="text-align: left;">kg</td>
<td style="text-align: left;">Catalyst mass for heterogeneous catalytic reactions</td>
</tr>
<tr>
<td style="text-align: left;">Pressure drop</td>
<td style="text-align: left;"><span class="math inline">\(\Delta P\)</span></td>
<td style="text-align: left;">Pa</td>
<td style="text-align: left;">Specified pressure drop across the reactor</td>
</tr>
<tr>
<td style="text-align: left;">Outlet temperature</td>
<td style="text-align: left;"><span class="math inline">\(T_{\text{out}}\)</span></td>
<td style="text-align: left;">K</td>
<td style="text-align: left;">Specified outlet temperature (outlet-temperature mode only)</td>
</tr>
<tr>
<td style="text-align: left;">Convergence tolerance</td>
<td style="text-align: left;"><span class="math inline">\(\varepsilon\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Composition convergence criterion (default <span class="math inline">\(10^{-5}\)</span>)</td>
</tr>
<tr>
<td style="text-align: left;">Max. iterations</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Maximum number of convergence iterations (default 1000)</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Heat Exchange mode only (see Section <a href="#eq:cstr_hx" data-reference-type="ref" data-reference="eq:cstr_hx">[eq:cstr_hx]</a>)</em></td>
</tr>
<tr>
<td style="text-align: left;">Overall HTC</td>
<td style="text-align: left;"><span class="math inline">\(U\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-2}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Overall heat transfer coefficient (default 500)</td>
</tr>
<tr>
<td style="text-align: left;">Heat exchange area</td>
<td style="text-align: left;"><span class="math inline">\(A\)</span></td>
<td style="text-align: left;">m<span class="math inline">\(^2\)</span></td>
<td style="text-align: left;">User-specified heat exchange area</td>
</tr>
<tr>
<td style="text-align: left;">Coolant inlet temperature</td>
<td style="text-align: left;"><span class="math inline">\(T_{c,\text{in}}\)</span></td>
<td style="text-align: left;">K</td>
<td style="text-align: left;">Coolant inlet temperature</td>
</tr>
<tr>
<td style="text-align: left;">Coolant mass flow rate</td>
<td style="text-align: left;"><span class="math inline">\(\dot{m}_c\)</span></td>
<td style="text-align: left;">kg s<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant mass flow rate (variable-<span class="math inline">\(T_c\)</span> modes)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant specific heat</td>
<td style="text-align: left;"><span class="math inline">\(c_{p,c}\)</span></td>
<td style="text-align: left;">J kg<span class="math inline">\(^{-1}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant specific heat (default 4180, water)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant flow direction</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Constant temperature, co-current, or counter-current</td>
</tr>
<tr>
<td style="text-align: left;">Area calculation mode</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Auto from geometry (<span class="math inline">\(A=4V/D\)</span>) or user-specified</td>
</tr>
<tr>
<td style="text-align: left;">Use utility stream</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Read coolant properties from connected utility material stream</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Wall thermal resistance (optional)</em></td>
</tr>
<tr>
<td style="text-align: left;">Use wall properties</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Enable wall resistance calculation for <span class="math inline">\(U\)</span></td>
</tr>
<tr>
<td style="text-align: left;">Wall material</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Steel, Carbon Steel, Cast Iron, Stainless Steel, PVC, or Copper</td>
</tr>
<tr>
<td style="text-align: left;">Wall thickness</td>
<td style="text-align: left;"><span class="math inline">\(\delta_w\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Reactor wall thickness (default 0.005)</td>
</tr>
<tr>
<td style="text-align: left;">Internal HTC</td>
<td style="text-align: left;"><span class="math inline">\(h_i\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-2}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Process-side heat transfer coefficient (user or auto)</td>
</tr>
<tr>
<td style="text-align: left;">External HTC</td>
<td style="text-align: left;"><span class="math inline">\(h_o\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-2}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant-side heat transfer coefficient (user or auto)</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Auto HTC calculation (optional, requires wall properties)</em></td>
</tr>
<tr>
<td style="text-align: left;">Impeller diameter</td>
<td style="text-align: left;"><span class="math inline">\(d_{imp}\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Impeller diameter for stirred-vessel <span class="math inline">\(h_i\)</span> (default 0.05)</td>
</tr>
<tr>
<td style="text-align: left;">Impeller speed</td>
<td style="text-align: left;"><span class="math inline">\(N\)</span></td>
<td style="text-align: left;">RPM</td>
<td style="text-align: left;">Impeller rotational speed (default 100)</td>
</tr>
<tr>
<td style="text-align: left;">Impeller type</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Impeller type selection (Flat Blade, Rushton, Pitched Blade, Propeller, Anchor, Helical Ribbon); determines Nusselt constant <span class="math inline">\(C\)</span> automatically</td>
</tr>
<tr>
<td style="text-align: left;">Jacket diameter</td>
<td style="text-align: left;"><span class="math inline">\(D_j\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Outer diameter of annular jacket (default 0.15)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant density</td>
<td style="text-align: left;"><span class="math inline">\(\rho_c\)</span></td>
<td style="text-align: left;">kg m<span class="math inline">\(^{-3}\)</span></td>
<td style="text-align: left;">Coolant density (default 998, or from utility stream)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant viscosity</td>
<td style="text-align: left;"><span class="math inline">\(\mu_c\)</span></td>
<td style="text-align: left;">Pa s</td>
<td style="text-align: left;">Coolant dynamic viscosity (default 0.001, or from utility stream)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant thermal cond.</td>
<td style="text-align: left;"><span class="math inline">\(k_c\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-1}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant thermal conductivity (default 0.6, or from utility stream)</td>
</tr>
</tbody>
</table>



###### Governing Equations {#governing-equations .unnumbered}

At steady state, the molar balance for each component i in the reactor is



<a id="eq:cstr_balance"></a>

\[
\dot{n}_{i,\text{out}}=\dot{n}_{i,\text{in}}+\sum_{j}R_{i,j}
\]


where $n_{i,in}$ and $n_{i,out}$ are the inlet and outlet molar flow rates and $R_{i,j}$ is the production rate of component $i$ in reaction $j$ (Eq. $§$ ).

###### Residence Time {#residence-time .unnumbered}

The liquid and vapor residence times are defined as



<a id="eq:cstr_restime"></a>

\[
\tau_{L}=\frac{V}{Q_{L}+Q_{S}},\qquad\tau_{V}=\frac{V_{h}}{Q_{V}}
\]


In single-outlet mode (no separate vapor exit), $tau_{L}=tau_{V}=V/Q$ .

###### Operating Modes and Energy Balance {#operating-modes-and-energy-balance .unnumbered}

####### Isothermal Mode {#isothermal-mode .unnumbered}

The reactor temperature is held constant at the inlet temperature $T_{0}$ . The required heat duty is



<a id="eq:cstr_iso"></a>

\[
\dot{Q}=\sum_{i}\Delta H_{f,i}^{\circ}\,M_{i}\,\frac{\dot{n}_{i,\text{out}}-\dot{n}_{i,\text{in}}}{1000}+\dot{H}_{\text{out}}-\dot{H}_{\text{in}}
\]


where <span class="roman">$\Delta H_{f,i}^{\circ}$</span> is the standard enthalpy of formation at 25 °C (kJ/kmol), $M_{i}$ the molar mass (kg/kmol), and <span class="roman">$\mathring{H}$</span> denotes the enthalpy flow rate (kW).

####### Adiabatic Mode {#adiabatic-mode .unnumbered}

No heat is exchanged with the surroundings (Q = 0). The outlet temperature is determined by an enthalpy–pressure flash. At each iteration of the convergence loop, the product enthalpy is computed from



<a id="eq:cstr_adiabatic"></a>

\[
\dot{H}_{\text{out}}=\dot{H}_{\text{in}}-\dot{Q}_{\text{rxn}}
\]


where $\dot{Q_{r}}xn$ = sum of $Q_{rxn}$ , $j$ is the total reaction heat release (Eq. $§$ ). A pressure–enthalpy flash is then performed to obtain the new temperature.

####### Outlet Temperature Mode {#outlet-temperature-mode .unnumbered}

The outlet temperature $T_{out}$ is specified by the user. The heat duty is calculated from Eq. $[§](#eq:cstr_iso)$ with the outlet enthalpy evaluated at $T_{out}$ .

####### Heat Exchange Mode {#heat-exchange-mode .unnumbered}

In this mode, the reactor exchanges heat with a utility fluid (jacket or external heat exchanger). The heat transfer rate is calculated from Newton’s law of cooling:



<a id="eq:cstr_hx"></a>

\[
\dot{Q}=U\,A\,(T_{c}-T)
\]


where $U$ is the overall heat transfer coefficient (W/m $^{2}$ /K), $A$ is the heat exchange area (m $^{2}$ ), $T_{c}$ is the effective coolant temperature, and $T$ is the reactor temperature (equal to the outlet temperature, since the CSTR is well-mixed). The product enthalpy at each iteration becomes



<a id="eq:cstr_hx_balance"></a>

\[
\dot{H}_{\text{out}}=\dot{H}_{\text{in}}-\dot{Q}_{\text{rxn}}+\frac{\dot{Q}}{1000}
\]


Three coolant flow configurations are supported:

- **Constant Temperature** — the coolant temperature is fixed at $T_{c,in}$ (e.g., boiling/condensing utility or very large coolant flow rate).

- **Co-current** or **Counter-current** — the coolant temperature changes as it absorbs or releases heat. Since the CSTR is perfectly mixed (uniform temperature on the process side), the co-current and counter-current configurations yield the same result. The effective coolant temperature is the arithmetic mean:



<a id="eq:cstr_Tc_eff"></a>

\[
T_{c,\text{eff}}=\frac{T_{c,\text{in}}+T_{c,\text{out}}}{2},\qquad T_{c,\text{out}}=T_{c,\text{in}}+\frac{\dot{Q}}{\dot{m}_{c}\,c_{p,c}}
\]


where $\dot{m}_{c}$ is the coolant mass flow rate (kg/s) and $c_{p,c}$ is the coolant specific heat (J/kgbreakableslashK). The coolant outlet temperature $T_{c,out}$ is computed iteratively together with the reactor temperature.

The coolant properties can be specified as simple parameters (inlet temperature, mass flow rate, specific heat) or read from a connected utility material stream. When a utility stream is used, the stream’s phase properties determine $c_{p,c}$ and $\dot{m}_{c}$ , and the outlet utility stream is updated with the calculated coolant outlet temperature.

When the area calculation mode is set to**Auto from Geometry**, the heat exchange area is computed from the cylindrical vessel lateral surface:



<a id="eq:cstr_hx_area"></a>

\[
A=\pi\,D\,H=\frac{4\,V}{D}
\]


where $D$ is the reactor diameter and $V$ is the reactor volume. Alternatively, the area can be specified directly by the user.

####### Wall Thermal Resistance {#wall-thermal-resistance .unnumbered}

When **Use Wall Properties** is enabled, the overall heat transfer coefficient $U$ is computed from the individual resistances of the internal film, the reactor wall, and the external film:



<a id="eq:cstr_wall_resistance"></a>

\[
\frac{1}{U}=\frac{1}{h_{i}}+\frac{\delta_{w}}{k_{w}}+\frac{1}{h_{o}}
\]


where $h_{i}$ is the process-side (internal) heat transfer coefficient, $h_{o}$ is the coolant-side (external) heat transfer coefficient, $\delta_{w}$ is the wall thickness, and $k_{w}$ is the wall thermal conductivity. The wall thermal conductivity is temperature-dependent and is determined from the selected wall material (using the same correlations as the Pipe unit operation). The supported materials are Steel, Carbon Steel, Cast Iron, Stainless Steel, PVC, and Commercial Copper.

####### Automatic HTC Calculation {#automatic-htc-calculation .unnumbered}

When wall properties are enabled, DWSIM can optionally auto-calculate the internal and external heat transfer coefficients from process and coolant fluid properties.

**Internal HTC (process side)** — A stirred-vessel Nusselt correlation is used:



<a id="eq:cstr_stirred_htc"></a>

\[
\text{Nu}=C\,\text{Re}_{imp}^{2/3}\,\text{Pr}^{1/3},\qquad h_{i}=\frac{\text{Nu}\,k}{D}
\]


where the impeller Reynolds number is $\text{Re}_{imp}=\rho\,N\,d_{imp}^{2}/\mu$ , $N$ is the impeller speed (rev/s), $d_{imp}$ is the impeller diameter, $C$ is the impeller-dependent Nusselt constant (determined automatically from the selected impeller type), and $\rho$ , $\mu$ , $k$ , $D$ are the process fluid density, viscosity, thermal conductivity, and reactor diameter, respectively.

**External HTC (coolant side)** — An annular-jacket Dittus–Boelter correlation is used:



<a id="eq:cstr_jacket_htc"></a>

\[
\text{Nu}=0.023\,\text{Re}^{0.8}\,\text{Pr}^{0.4},\qquad h_{o}=\frac{\text{Nu}\,k_{c}}{D_{h}}
\]


where $D_{h}=D_{j}-D_{o}$ is the hydraulic diameter of the annular jacket, $D_{j}$ is the jacket diameter, $D_{o}=D+2\delta_{w}$ is the outer wall diameter, and the Reynolds number is computed from the coolant flow rate, density, and viscosity in the annular cross section $A_{ann}=\tfrac{\pi}{4}(D_{j}^{2}-D_{o}^{2})$ . The coolant properties (density, viscosity, thermal conductivity, specific heat) can be specified manually or read from a connected utility stream.

####### Solution Algorithm {#solution-algorithm-1 .unnumbered}

Because the outlet composition depends on the reaction rates, which themselves depend on the outlet composition (through the well-mixed assumption), the steady-state balance (Eq. $[§](#eq:cstr_balance)$ ) is solved iteratively using a pseudo-transient approach. The algorithm tracks the molar inventory $N_{i}$ (mol) of each component inside the reactor and marches forward in discrete time steps until the composition reaches steady state.







Step 1 — Initialization The reactor inventory is initialized from the inlet stream and the residence time:





<a id="eq:cstr_init"></a>

\[
N_{i}^{(0)}=\dot{n}_{i,\text{in}}\;\tau
\]


The initial time step is $\Delta t=0.2\,\tau_{L}$ , with a fallback of 1 s when $\tau_{L}=0$ .







Step 2 — Molar balance At each iteration $k$ , the reaction rates $R_{i}$ are evaluated at the current composition and temperature. The molar balance residual (mol/s) is





<a id="eq:cstr_residual"></a>

\[
b_{i}=\dot{n}_{i,\text{in}}+R_{i}-\dot{n}_{i,\text{out}}
\]








Step 3 — Adaptive time step. The time step is reset to $\Delta t=0.2\,\tau_{L}$ at the beginning of each iteration, then limited to prevent any component from being consumed by more than 80%:





<a id="eq:cstr_dt_limit"></a>

\[
\Delta t\leq\min_{i}\left\{ \frac{0.8\,N_{i}^{(k)}}{|b_{i}|}\;\bigg|\;b_{i}<0,\;N_{i}^{(k)}>0\right\}
\]


Resetting the time step each iteration (rather than only shrinking) allows large steps when reactions are slow and small steps when they are fast.







Step 4 — Inventory update The inventory is updated and clamped to non-negative values:





<a id="eq:cstr_update"></a>

\[
N_{i}^{(k+1)}=\max\!\left(0,\;N_{i}^{(k)}+b_{i}\,\Delta t\right)
\]


The updated mole fractions $y_{i}=N_{i}^{(k+1)}/\sum N_{j}^{(k+1)}$ are converted to mass fractions, and the outlet molar flows are recomputed.







Step 5 — Oscillation damping If the composition error $E^{(k)}$ increases compared with the previous iteration $E^{(k-1)}$ , the solution is damped by blending with the previous composition:





<a id="eq:cstr_damping"></a>

\[
y_{i}^{(k+1)}\leftarrow(1-\lambda)\,y_{i}^{(k+1)}+\lambda\,y_{i}^{(k)},\qquad\lambda=\max\!\left(0.3,\;0.5^{n_{\text{osc}}}\right)
\]


where $n_{osc}$ is the number of consecutive oscillating iterations. This prevents the algorithm from bouncing indefinitely between two states.







Step 6 — Flash calculation The stream properties are updated. For isothermal and outlet-temperature modes, a temperature–pressure flash is performed. For adiabatic and heat exchange modes, a pressure–enthalpy flash determines the new temperature. To reduce computation time, flash calculations are skipped on some iterations when far from convergence in non-adiabatic modes (the flash is always performed in adiabatic and heat exchange modes because temperature is a coupled variable).

Step 7 — Convergence check The composition error is





<a id="eq:cstr_comperr"></a>

\[
E^{(k)}=\sum_{i}\left|y_{i}^{(k+1)}-y_{i}^{(k)}\right|
\]


The loop terminates when $E^{(k)}<\epsilon$ . In adiabatic and heat exchange modes, an additional check is applied on the relative temperature change:



<a id="eq:cstr_temperr"></a>

\[
\frac{\left|T^{(k+1)}-T^{(k)}\right|}{T^{(k)}}<\varepsilon
\]


Both conditions must be satisfied simultaneously for the algorithm to converge in adiabatic and heat exchange modes.

###### Component Conversions {#component-conversions .unnumbered}

After convergence, the conversion of each component is computed as



<a id="eq:cstr_conversion"></a>

\[
X_{i}=\frac{|\dot{n}_{i,\text{in}}-\dot{n}_{i,\text{out}}|}{\dot{n}_{i,\text{in}}},\qquad\dot{n}_{i,\text{in}}>0
\]


###### Outlet Modes {#outlet-modes .unnumbered}

- Single outlet — all phases exit through a single stream; the outlet composition equals the overall mixture composition in the reactor.

- Two outlets — vapor exits through outlet 2; liquid and solid exit through outlet 1. Vapor-phase reactions occur in the headspace volume V_h; liquid/solid reactions occur in the reactor volume V.

###### Dynamic Mode {#dynamic-mode-5 .unnumbered}

When running in dynamic mode, the CSTR performs a single iteration per integration step (the time step is set by the flowsheet integrator). The accumulation stream tracks the reactor contents over time, and temperature and pressure are updated via flash calculations at each step.

##### Plug Flow Reactor (PFR)

The PFR models a tubular reactor in which the fluid flows through the tube with no axial mixing. Composition and temperature vary along the reactor length. The governing equations are a system of ordinary differential equations (ODEs) integrated over the reactor volume.

###### Input Parameters {#input-parameters-13 .unnumbered}



<a id="tab:pfr_params"></a>



<table>
<caption>PFR input parameters.</caption>
<thead>
<tr>
<th style="text-align: left;"><strong>Parameter</strong></th>
<th style="text-align: left;"><strong>Symbol</strong></th>
<th style="text-align: left;"><strong>Unit</strong></th>
<th style="text-align: left;"><strong>Description</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">Reactor volume</td>
<td style="text-align: left;"><span class="math inline">\(V\)</span></td>
<td style="text-align: left;">m<span class="math inline">\(^3\)</span></td>
<td style="text-align: left;">Total active volume of all tubes</td>
</tr>
<tr>
<td style="text-align: left;">Length</td>
<td style="text-align: left;"><span class="math inline">\(L\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Tube length</td>
</tr>
<tr>
<td style="text-align: left;">Diameter</td>
<td style="text-align: left;"><span class="math inline">\(D\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Tube internal diameter</td>
</tr>
<tr>
<td style="text-align: left;">Number of tubes</td>
<td style="text-align: left;"><span class="math inline">\(N_t\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Number of parallel tubes</td>
</tr>
<tr>
<td style="text-align: left;">Volume step</td>
<td style="text-align: left;"><span class="math inline">\(\delta V\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Fractional volume step for integration (default 0.01)</td>
</tr>
<tr>
<td style="text-align: left;">Catalyst loading</td>
<td style="text-align: left;"><span class="math inline">\(w_{\text{cat}}\)</span></td>
<td style="text-align: left;">kg m<span class="math inline">\(^{-3}\)</span></td>
<td style="text-align: left;">Catalyst mass per unit reactor volume</td>
</tr>
<tr>
<td style="text-align: left;">Catalyst void fraction</td>
<td style="text-align: left;"><span class="math inline">\(\varepsilon_b\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Bed void fraction</td>
</tr>
<tr>
<td style="text-align: left;">Catalyst particle diameter</td>
<td style="text-align: left;"><span class="math inline">\(d_p\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Catalyst particle diameter (for pressure drop)</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Heat Exchange mode only (see Section <a href="#eq:pfr_hx" data-reference-type="ref" data-reference="eq:pfr_hx">[eq:pfr_hx]</a>)</em></td>
</tr>
<tr>
<td style="text-align: left;">Overall HTC</td>
<td style="text-align: left;"><span class="math inline">\(U\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-2}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Overall heat transfer coefficient (default 500)</td>
</tr>
<tr>
<td style="text-align: left;">Heat exchange area</td>
<td style="text-align: left;"><span class="math inline">\(A\)</span></td>
<td style="text-align: left;">m<span class="math inline">\(^2\)</span></td>
<td style="text-align: left;">User-specified heat exchange area (or auto from geometry)</td>
</tr>
<tr>
<td style="text-align: left;">Area calculation mode</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Auto from tube geometry or user-specified</td>
</tr>
<tr>
<td style="text-align: left;">Coolant inlet temperature</td>
<td style="text-align: left;"><span class="math inline">\(T_{c,\text{in}}\)</span></td>
<td style="text-align: left;">K</td>
<td style="text-align: left;">Coolant inlet temperature</td>
</tr>
<tr>
<td style="text-align: left;">Coolant mass flow rate</td>
<td style="text-align: left;"><span class="math inline">\(\dot{m}_c\)</span></td>
<td style="text-align: left;">kg s<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant mass flow rate (variable-<span class="math inline">\(T_c\)</span> modes)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant specific heat</td>
<td style="text-align: left;"><span class="math inline">\(c_{p,c}\)</span></td>
<td style="text-align: left;">J kg<span class="math inline">\(^{-1}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant specific heat (default 4180, water)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant flow direction</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Constant temperature, co-current, or counter-current</td>
</tr>
<tr>
<td style="text-align: left;">Use utility stream</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Read coolant properties from connected utility material stream</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Wall thermal resistance (optional)</em></td>
</tr>
<tr>
<td style="text-align: left;">Use wall properties</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Enable wall resistance calculation for <span class="math inline">\(U\)</span></td>
</tr>
<tr>
<td style="text-align: left;">Wall material</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Steel, Carbon Steel, Cast Iron, Stainless Steel, PVC, or Copper</td>
</tr>
<tr>
<td style="text-align: left;">Wall thickness</td>
<td style="text-align: left;"><span class="math inline">\(\delta_w\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Tube wall thickness (default 0.005)</td>
</tr>
<tr>
<td style="text-align: left;">Internal HTC</td>
<td style="text-align: left;"><span class="math inline">\(h_i\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-2}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Process-side heat transfer coefficient (user or auto)</td>
</tr>
<tr>
<td style="text-align: left;">External HTC</td>
<td style="text-align: left;"><span class="math inline">\(h_o\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-2}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant-side heat transfer coefficient (user or auto)</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Auto HTC calculation (optional, requires wall properties)</em></td>
</tr>
<tr>
<td style="text-align: left;">Jacket diameter</td>
<td style="text-align: left;"><span class="math inline">\(D_j\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">Outer diameter of annular jacket (default 0.15)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant density</td>
<td style="text-align: left;"><span class="math inline">\(\rho_c\)</span></td>
<td style="text-align: left;">kg m<span class="math inline">\(^{-3}\)</span></td>
<td style="text-align: left;">Coolant density (default 998, or from utility stream)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant viscosity</td>
<td style="text-align: left;"><span class="math inline">\(\mu_c\)</span></td>
<td style="text-align: left;">Pa s</td>
<td style="text-align: left;">Coolant dynamic viscosity (default 0.001, or from utility stream)</td>
</tr>
<tr>
<td style="text-align: left;">Coolant thermal cond.</td>
<td style="text-align: left;"><span class="math inline">\(k_c\)</span></td>
<td style="text-align: left;">W m<span class="math inline">\(^{-1}\)</span> K<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: left;">Coolant thermal conductivity (default 0.6, or from utility stream)</td>
</tr>
</tbody>
</table>



The reactor geometry satisfies



<a id="eq:pfr_geometry"></a>

\[
V=N_{t}\cdot\frac{\pi D^{2}}{4}\cdot L
\]


and either $L$ or $D$ can be calculated from the other two quantities.

###### Governing Equations {#governing-equations-1 .unnumbered}

The molar flow of each component i varies along the reactor volume according to



<a id="eq:pfr_ode"></a>

\[
\frac{d\dot{n}_{i}}{dV}=-R_{i}
\]


where $R_{i}$ is the net rate of consumption of component $i$ (mol/(m³·s)), evaluated from the reaction rate expressions. The system is integrated from $V=0$ (inlet) to $V=V_{total}$ (outlet).

###### Residence Time {#residence-time-1 .unnumbered}



<a id="eq:pfr_restime"></a>

\[
\tau=\frac{V}{Q}
\]


where $Q$ is the total volumetric flow rate at the inlet.

###### Operating Modes and Energy Balance {#operating-modes-and-energy-balance-1 .unnumbered}

####### Isothermal Mode {#isothermal-mode-1 .unnumbered}

The temperature is constant along the reactor: $T(V)=T_{0}$ . The heat duty required to maintain this condition is



<a id="eq:pfr_iso"></a>

\[
\dot{Q}=\sum_{i}\Delta H_{f,i}^{\circ}\,M_{i}\,\frac{\dot{n}_{i,\text{out}}-\dot{n}_{i,\text{in}}}{1000}+\dot{H}_{\text{out}}-\dot{H}_{\text{in}}
\]


####### Adiabatic Mode {#adiabatic-mode-1 .unnumbered}

No heat is exchanged ( $Q=0$ ). At each volume step, the temperature is updated from the enthalpy balance:



<a id="eq:pfr_adiabatic"></a>

\[
H_{\text{out}}(V+\Delta V)=H_{\text{in}}(V)-\Delta H_{\text{rxn}}(V)
\]


A pressure–enthalpy flash at each step yields the new temperature T(V + Delta V).

####### Non-isothermal, Non-adiabatic Mode {#non-isothermal-non-adiabatic-mode .unnumbered}

An external energy stream provides a heat duty $Q_{ext}$ . At each volume step, the enthalpy increment includes the external heat proportional to the volume fraction:



<a id="eq:pfr_noniso"></a>

\[
H_{\text{out}}=H_{\text{in}}-\Delta H_{\text{rxn}}+\dot{Q}_{\text{ext}}\cdot\delta V
\]


####### Outlet Temperature Mode {#outlet-temperature-mode-1 .unnumbered}

The temperature is linearly interpolated from $T_{0}$ at the inlet to $T_{out}$ at the outlet:



<a id="eq:pfr_outletT"></a>

\[
T(V)=T_{0}+(T_{\text{out}}-T_{0})\cdot\frac{V}{V_{\text{total}}}
\]


The required heat duty is computed after integration from the overall enthalpy balance.

####### Heat Exchange Mode {#heat-exchange-mode-1 .unnumbered}

In this mode, the reactor exchanges heat with a utility fluid flowing through a jacket or shell side. At each volume step $\delta V$ , the local heat transfer rate is



<a id="eq:pfr_hx"></a>

\[
\delta\dot{Q}=U\,\delta A\,(T_{c}-T)
\]


where $U$ is the overall heat transfer coefficient (W/m $^{2}$ /K), $T_{c}$ is the local coolant temperature, and $T$ is the local reactor temperature. The local heat exchange area $\delta A$ can be computed automatically from the tube geometry:



<a id="eq:pfr_dA"></a>

\[
\delta A=\pi\,D\,L\,\delta V\,N_{t}
\]


or as a user-specified fraction of the total area: $\delta A=A_{\text{user}}\cdot\delta V$ . The enthalpy balance at each volume step becomes



<a id="eq:pfr_hx_balance"></a>

\[
\dot{H}_{\text{out}}=\dot{H}_{\text{in}}-\Delta H_{\text{rxn}}+\frac{\delta\dot{Q}}{1000}
\]


Three coolant flow configurations are supported:

- **Constant Temperature** — the coolant temperature $T_{c}$ is fixed along the entire reactor length.

- **Co-current** — the coolant flows in the same direction as the process fluid. The coolant temperature is updated at each volume step:



<a id="eq:pfr_cocurrent"></a>

\[
T_{c}(V+\delta V)=T_{c}(V)-\frac{\delta\dot{Q}}{\dot{m}_{c}\,c_{p,c}}
\]


- **Counter-current** — the coolant flows in the opposite direction. This requires an iterative shooting method: the coolant outlet temperature (at the reactor inlet end) is guessed, the reactor is integrated forward tracking both process and coolant temperatures, and the coolant inlet temperature at the reactor outlet is compared with the specified value. The guess is updated with relaxation until convergence (tolerance 0.1 K, relaxation factor 0.5, up to 50 iterations).

The coolant properties can be specified as simple parameters (inlet temperature, mass flow rate, specific heat) or read from a connected utility material stream. When a utility stream is connected, its phase properties are used for $c_{p,c}$ and $\dot{m}_{c}$ , and the outlet utility stream is updated with the calculated coolant outlet conditions.

####### Wall Thermal Resistance {#wall-thermal-resistance-1 .unnumbered}

When **Use Wall Properties** is enabled, the overall heat transfer coefficient $U$ is computed from the individual resistances at each volume step:



<a id="eq:pfr_wall_resistance"></a>

\[
\frac{1}{U}=\frac{1}{h_{i}}+\frac{\delta_{w}}{k_{w}}+\frac{1}{h_{o}}
\]


where $h_{i}$ is the tube-side (internal) heat transfer coefficient, $h_{o}$ is the shell/jacket-side (external) heat transfer coefficient, $\delta_{w}$ is the tube wall thickness, and $k_{w}$ is the wall thermal conductivity. The wall thermal conductivity is temperature-dependent and is determined from the selected wall material (using the same correlations as the Pipe unit operation). Supported materials are Steel, Carbon Steel, Cast Iron, Stainless Steel, PVC, and Commercial Copper. Since $U$ depends on temperature through $k_{w}(T)$ , it is recomputed at each volume step.

####### Automatic HTC Calculation {#automatic-htc-calculation-1 .unnumbered}

When wall properties are enabled, DWSIM can optionally auto-calculate the internal and external heat transfer coefficients at each volume step from the local process and coolant fluid properties.

**Internal HTC (tube side)** — The Dittus–Boelter correlation is used:



<a id="eq:pfr_tube_htc"></a>

\[
\text{Nu}=0.023\,\text{Re}^{0.8}\,\text{Pr}^{n},\qquad h_{i}=\frac{\text{Nu}\,k}{D}
\]


where $n=0.4$ for heating ( $T_{c}>T$ ) and $n=0.3$ for cooling, the Reynolds number is $\text{Re}=\rho\,v\,D/\mu$ with velocity $v=\dot{m}/(\rho\,N_{t}\,\pi D^{2}/4)$ per tube, and a minimum Nusselt number of 3.66 is enforced for laminar flow. The process fluid properties (density $\rho$ , viscosity $\mu$ , heat capacity $c_{p}$ , thermal conductivity $k$ ) are taken from the process stream at each step.

**External HTC (jacket/shell side)** — An annular-jacket Dittus–Boelter correlation is used:



<a id="eq:pfr_jacket_htc"></a>

\[
\text{Nu}=0.023\,\text{Re}^{0.8}\,\text{Pr}^{0.4},\qquad h_{o}=\frac{\text{Nu}\,k_{c}}{D_{h}}
\]


where $D_{h}=D_{j}-D_{o}$ is the hydraulic diameter of the annular jacket, $D_{j}$ is the jacket diameter, $D_{o}=D+2\delta_{w}$ is the tube outer diameter, and the Reynolds number is computed from the coolant flow in the annular cross section $A_{ann}=\tfrac{\pi}{4}(D_{j}^{2}-D_{o}^{2})$ . The coolant properties (density, viscosity, thermal conductivity) can be specified manually or read from a connected utility stream.

###### ODE Integration Methods {#ode-integration-methods .unnumbered}

DWSIM provides five numerical integrators for the PFR, selectable via the solver option:



<a id="tab:pfr_solvers"></a>



| **Index** | **Method** | **Notes** |
|:--:|:---|:---|
| 0 | Implicit Runge–Kutta (5th order) | Recommended for stiff systems |
| 1 | Explicit Runge–Kutta 4(5) | For non-stiff problems |
| 2 | Adams–Moulton | Multi-step predictor–corrector |
| 3 | Gear’s BDF | For very stiff systems |
| 4 | OSLO RK4(5) | Adaptive Runge–Kutta with built-in step control |

Available ODE solvers for the PFR.



###### Adaptive Step Size Control {#adaptive-step-size-control .unnumbered}

The PFR uses a two-level adaptive strategy to ensure physically valid solutions (non-negative molar flows).







Outer level If negative molar flows are detected after integrating with the default volume step delta V, the entire calculation is retried with a smaller fraction:





<a id="eq:pfr_outer_adapt"></a>

\[
\delta V\;\longrightarrow\;0.1\,\delta V\;\longrightarrow\;0.05\,\delta V
\]








Inner level At each volume step, if negative concentrations are found, the step is repeatedly halved:





<a id="eq:pfr_inner_adapt"></a>

\[
\delta V\;\leftarrow\;\frac{\delta V}{2}
\]


up to 30 times. Concentrations that are negative but below the threshold $|C_{i}|<10^{-6}$ mol/m³ are clamped to zero rather than triggering a step reduction.

###### Pressure Drop Calculation {#pressure-drop-calculation .unnumbered}

The PFR supports two pressure drop correlations, selected automatically based on whether a catalyst bed is present.

####### Packed Bed — Ergun Equation {#packed-bed-ergun-equation .unnumbered}

For reactors containing a catalyst bed, the pressure drop per unit length is



<a id="eq:ergun"></a>

\[
-\frac{dP}{dL}=\frac{150\,\mu\,(1-\varepsilon_{b})^{2}}{\varepsilon_{b}^{3}\,d_{p}^{2}}\,u+\frac{1.75\,\rho\,(1-\varepsilon_{b})}{\varepsilon_{b}^{3}\,d_{p}}\,u^{2}
\]


where $\mu$ is the fluid viscosity (Pa·s), $\epsilon_{b}$ the bed void fraction, $d_{p}$ the particle diameter (m), $\rho$ the fluid density (kg/m³), and $u$ the superficial velocity (m/s).

####### Empty Tube — Beggs & Brill Correlation {#empty-tube-beggs-brill-correlation .unnumbered}

For empty tubes (no catalyst), the Beggs & Brill multiphase flow correlation is used, which accounts for liquid holdup, flow pattern, and friction for gas–liquid systems.

###### Component Conversions {#component-conversions-1 .unnumbered}



<a id="eq:pfr_conversion"></a>

\[
X_{i}=\frac{|\dot{n}_{i,\text{in}}-\dot{n}_{i,\text{out}}|}{\dot{n}_{i,\text{in}}}
\]


###### Output Profiles {#output-profiles .unnumbered}

The PFR records concentration, temperature, and pressure at each volume step, producing axial profiles that can be visualized in the DWSIM graphical interface.

##### Comparison of CSTR and PFR



<a id="tab:comparison"></a>



| **Feature** | **CSTR** | **PFR** |
|:---|:---|:---|
| Mixing assumption | Perfectly mixed | No axial mixing |
| Solution method | Pseudo-transient iteration | ODE integration over volume |
| Spatial variation | None (uniform) | Along reactor length |
| ODE solver options | N/A | 5 solvers available |
| Two-outlet (V/L split) | Yes | No |
| Headspace volume | Yes | No |
| Dynamic mode | Yes | No |
| Pressure drop | User-specified | Ergun / Beggs & Brill |
| Catalyst specification | Total mass (kg) | Loading (kg m$^{-3}$) |
| Axial profiles | N/A | T, P, composition vs. volume |
| Heat exchange | $U\!\cdot\!A\!\cdot\!(T_c-T)$ | $U\!\cdot\!\delta A\!\cdot\!(T_c-T)$ per step |
| Coolant flow configs | Const. $T_c$, co/counter-current | Const. $T_c$, co/counter-current |
| Utility stream | Optional | Optional |
| Wall thermal resistance | $1/U = 1/h_i +\delta_w/k_w + 1/h_o$ | Same |
| Auto internal HTC | Stirred vessel (Nu $= C\,\text{Re}_{imp}^{2/3}\,\text{Pr}^{1/3}$) | Dittus–Boelter (tube side) |
| Auto external HTC | Annular jacket (Dittus–Boelter) | Annular jacket (Dittus–Boelter) |

Summary comparison of the CSTR and PFR models in DWSIM.



#### General Reactors

##### Conversion Reactor

###### Overview {#overview-20 .unnumbered}

The Conversion Reactor is the simplest reaction model available in DWSIM. The user specifies directly the fractional conversion of one or more reactions, and the model calculates the resulting outlet composition and energy balance. It does not require kinetic expressions or equilibrium data, making it ideal for preliminary studies, mass balance verification, or situations where only the overall conversion is known.

###### Operating Principle {#operating-principle .unnumbered}

For each reaction assigned to the reactor, the user defines a conversion value between 0 and 1 (or a temperature-dependent expression). The model consumes the base component according to that conversion and adjusts all other species through the reaction stoichiometry.

When multiple reactions are present, they are grouped by rank (priority). All reactions of the same rank are solved simultaneously using a Simplex optimization algorithm that finds the set of reaction extents satisfying the specified conversions. Reactions of different ranks are solved sequentially, from lowest to highest rank, so that the products of one group become the reactants of the next.

###### Conversion Definition {#conversion-definition .unnumbered}

The conversion of the base component of reaction \$j\$ is defined as:



<a id="eq:conv_def"></a>

\[
X_{j}=\frac{n_{0,j}-n_{f,j}}{n_{0,j}}
\]


where $n_{0,j}$ is the initial molar flow of the base component and $n_{f,j}$ is its final molar flow. The conversion can be a fixed value or a function of temperature $T$ (in Kelvin), entered as a mathematical expression in the reaction settings.

###### Calculation of Molar Flows {#calculation-of-molar-flows .unnumbered}

For a reaction with stoichiometric coefficients $\nu_{i}$ (negative for reactants, positive for products) and base component $b$ , the molar flow of each species at the outlet is:



<a id="eq:conv_moles"></a>

\[
n_{i,\text{out}}=n_{i,\text{in}}+\frac{\nu_{i}}{\left|\nu_{b}\right|}\cdot X_{j}\cdot n_{b,\text{in}}
\]


###### Parallel Reactions and Simplex Optimization {#parallel-reactions-and-simplex-optimization .unnumbered}

When two or more reactions of the same rank share common reactants, their extents are determined simultaneously. The model builds an objective function that minimizes the sum of squared differences between the calculated conversions and the target conversions:



<a id="eq:conv_simplex"></a>

\[
\min_{\xi_{1},\dots,\xi_{m}}\sum_{j=1}^{m}\left(X_{j}^{\text{calc}}-X_{j}^{\text{target}}\right)^{2}
\]


This problem is solved using the Nelder-Mead Simplex method.

###### Energy Balance {#energy-balance-2 .unnumbered}

After the outlet composition is determined, a thermodynamic flash calculation (T-P or P-H, depending on the operating mode) provides the final phase distribution and properties. The heat of reaction $\Delta H_{r}$ for each reaction is computed from the component enthalpies.

In isothermal mode, the reactor calculates the heat duty $Q$ required to maintain the specified temperature:



<a id="eq:conv_energy"></a>

\[
Q=H_{\text{out}}-H_{\text{in}}
\]


In adiabatic mode ( $Q=0$ ), the outlet temperature is found iteratively using a P-H flash.

###### Supported Operating Modes {#supported-operating-modes .unnumbered}



<a id="tab:conv_modes"></a>



| **Mode**                     | **Specified**             | **Calculated**     |
|:-----------------------------|:--------------------------|:-------------------|
| Isothermal                   | $T_{\text{out}}$, $P$ | $Q$              |
| Adiabatic                    | $P$ ($Q=0$)           | $T_{\text{out}}$ |
| Non-isothermal/non-adiabatic | $Q$, $P$              | $T_{\text{out}}$ |

Conversion Reactor operating modes



###### Practical Tips {#practical-tips .unnumbered}

- Make sure the specified conversion does not exceed the limiting-reagent constraint; otherwise the model may produce negative molar flows.

- For temperature-dependent conversions, the expression must use $T$ as the variable (temperature in Kelvin). Example: `0.5 + 0.001*(T - 300)` .

- Use reaction ranks to model sequential reaction steps (e.g., A → B → C), assigning lower rank to the first step.

##### Equilibrium Reactor

###### Overview {#overview-21 .unnumbered}

The Equilibrium Reactor calculates the outlet composition based on chemical equilibrium. Instead of specifying how fast or how far a reaction proceeds, the user defines the equilibrium relationship, and the model finds the set of reaction extents that satisfy those equilibrium conditions simultaneously.

This model is appropriate when the reactions are fast enough that the outlet stream approaches thermodynamic equilibrium, which is common in high-temperature processes such as reforming, water-gas shift, and combustion.

###### Operating Principle {#operating-principle-1 .unnumbered}

For each equilibrium reaction, the model needs the equilibrium constant \$K\_{eq}\$ as a function of temperature. Based on the equilibrium expressions and the feed composition, the model solves a system of nonlinear equations to find the reaction extents \$\xi_j\$ that satisfy all equilibrium conditions simultaneously.

###### Equilibrium Constant {#equilibrium-constant .unnumbered}

The equilibrium constant can be provided in several ways:



<a id="tab:eq_Ke_bases"></a>



| **Basis**           | **Expression**                      |
|:--------------------|:------------------------------------|
| Activity            | $K_a = \prod_i a_i^{\nu_i}$       |
| Fugacity            | $K_f = \prod_i \hat{f}_i^{\nu_i}$ |
| Molar concentration | $K_c = \prod_i C_i^{\nu_i}$       |
| Mole fraction       | $K_x = \prod_i x_i^{\nu_i}$       |
| Partial pressure    | $K_p = \prod_i p_i^{\nu_i}$       |

Available bases for the equilibrium constant



The temperature dependence of $K_{eq}$ can be supplied in three forms:

1.  Constant value: a single number.

2.  Expression: a mathematical formula using \$T\$ (in Kelvin).

3.  From Gibbs energy: calculated automatically from the standard Gibbs energy of formation of the species:



<a id="eq:eq_Ke_gibbs"></a>

\[
\ln K_{eq}(T)=-\frac{\Delta G_{r}^{\circ}(T)}{RT}
\]


where $\Delta G_{r}^{\circ}=\sum_{i}\nu_{i},G_{f,i}^{\circ}$ is the standard Gibbs energy of reaction.

###### Equilibrium Equation {#equilibrium-equation .unnumbered}

For each reaction $j$ , the equilibrium condition written in logarithmic form is:



<a id="eq:eq_condition"></a>

\[
f_{j}(\boldsymbol{\xi})=\sum_{i}\nu_{i,j}\ln\!\left(\frac{n_{i}(\boldsymbol{\xi})}{n_{\text{total}}(\boldsymbol{\xi})}\right)-\ln K_{eq,j}(T)=0
\]


where $n_{i}(\boldsymbol{\xi})$ are the molar flows as functions of all reaction extents, and $n_{\text{total}}$ is the total molar flow. The exact form depends on the chosen basis (activity, fugacity, etc.).

###### Solution Method {#solution-method .unnumbered}

The system of \$m\$ nonlinear equations (one per reaction) in $m$ unknowns (the reaction extents $\xi_{1},\dots,\xi_{m}$ ) is solved using a Newton-Raphson method. The Jacobian is estimated numerically, and the extents are updated iteratively:



<a id="eq:eq_newton"></a>

\[
\boldsymbol{\xi}^{(k+1)}=\boldsymbol{\xi}^{(k)}-\mathbf{J}^{-1}\cdot\mathbf{f}\!\left(\boldsymbol{\xi}^{(k)}\right)
\]


The iteration continues until the residuals are below the specified tolerance (default: $10^{-6}$ ). To improve robustness, the model limits the step size of each extent update to avoid overshooting into nonphysical regions (negative molar flows).

###### Energy Balance {#energy-balance-3 .unnumbered}

The energy balance works identically to the Conversion Reactor:

- **Isothermal mode:** the model calculates the heat duty $Q$ needed to maintain the specified temperature.

- **Adiabatic mode:** the outlet temperature is found iteratively (the equilibrium must be re-solved at each candidate temperature until $Q=0$ ).

###### Practical Tips {#practical-tips-1 .unnumbered}

- When using the "From Gibbs energy" option for $K_{eq}$ , the accuracy depends on the quality of the Gibbs energy of formation data in the compound database.

- For reactions with very large or very small equilibrium constants, the logarithmic formulation helps avoid numerical overflow.

- The Newton-Raphson solver may require good initial estimates; for difficult cases, try providing a closer initial temperature or a simpler reaction set first.

##### Gibbs Reactor

###### Overview {#overview-22 .unnumbered}

The Gibbs Reactor finds the outlet composition that minimizes the total Gibbs energy of the system, subject to element balance constraints. Unlike the Equilibrium Reactor, it does not require the user to specify which reactions occur — the model determines the equilibrium composition automatically based on thermodynamic principles.

This is the most general equilibrium model available. It is especially useful when the reaction network is complex or unknown, since only the list of possible product species needs to be defined.

###### Thermodynamic Foundation {#thermodynamic-foundation .unnumbered}

At constant temperature and pressure, a closed system reaches equilibrium when its total Gibbs energy is at a minimum. The total Gibbs energy of the mixture is:



<a id="eq:gibbs_total"></a>

\[
G_{\text{total}}=\sum_{i=1}^{N}n_{i}\,\mu_{i}=\sum_{i=1}^{N}n_{i}\left[G_{f,i}^{\circ}+RT\ln\!\left(\frac{n_{i}}{\sum_{k}n_{k}}\right)\right]
\]


where $n_{i}$ is the molar amount of species \$i\$, \$\mu_i\$ is its chemical potential, $G_{f,i}^{\circ}$ is the standard Gibbs energy of formation, $R$ is the gas constant, and $T$ is the absolute temperature.

###### Optimization Problem {#optimization-problem .unnumbered}

The model solves the following constrained minimization:



<a id="eq:gibbs_min"></a>

\[
\min_{\mathbf{n}}\;G_{\text{total}}(\mathbf{n})
\]


subject to element balance constraints:



<a id="eq:gibbs_elem"></a>

\[
\sum_{i=1}^{N}a_{e,i}\,n_{i}=b_{e},\quad e=1,\dots,E
\]


and non-negativity:



<a id="eq:gibbs_nonneg"></a>

\[
n_{i}\geq0,\quad i=1,\dots,N
\]


where $a_{e,i}$ is the number of atoms of element $e$ in species $i$ , $b_{e}$ is the total amount of element $e$ in the feed (conserved), and $E$ is the number of distinct elements.

###### Solution Method {#solution-method-1 .unnumbered}

DWSIM uses the IPOPT (Interior Point OPTimizer) solver, a state-of-the-art nonlinear programming algorithm. The main features of the implementation are:

- **Element matrix construction:** the model builds the matrix $\mathbf{A}=[a_{e,i}]$ automatically from the molecular formulas of all species present.

- **Initial estimate by linear programming:** before calling IPOPT, the model solves a simplified LP problem to obtain a feasible starting point, which greatly improves convergence reliability.

- **Multiphase support:** the Gibbs energy includes contributions from vapor, liquid, and solid phases, with appropriate fugacity models for each.

- **AI-assisted convergence:** for difficult cases, the model can use artificial intelligence techniques to generate better initial estimates.

###### Operating Modes {#operating-modes-4 .unnumbered}

The Gibbs Reactor supports two distinct calculation approaches:



<a id="tab:gibbs_modes"></a>



| **Mode** | **Description** |
|:---|:---|
| Direct minimization | Minimizes total Gibbs energy with no reaction definitions needed. The user only selects which species may be present. |
| Reaction extents | Uses specified reactions and minimizes $G$ with respect to the reaction extents $\xi_j$, similar to the Equilibrium Reactor but solved via optimization rather than root-finding. |

Gibbs Reactor calculation modes



For thermal modes, the same options apply as the other reactors (isothermal, adiabatic, and specified heat duty).

###### Practical Tips {#practical-tips-2 .unnumbered}

- In direct minimization mode, the set of species you include determines the solution. If a possible product species is absent from the list, it cannot appear in the outlet. Conversely, including too many species that are thermodynamically negligible usually does not cause problems — their equilibrium amounts will simply be very small.

- The Gibbs Reactor tends to be more computationally expensive than the Equilibrium Reactor because it solves a full nonlinear optimization problem. For simple, well-defined reaction systems, the Equilibrium Reactor may be more efficient.

- Adiabatic mode requires an outer iteration loop on temperature, which increases computation time. Providing a reasonable initial temperature estimate helps convergence.

- If the solver reports infeasibility, check that the element balances are consistent (i.e., all elements present in the feed can be accounted for in the selected product species).

##### Gibbs Reactor (Reaktoro)




![Gibbs Reactor (Reaktoro) model](images/screens80/reaktorogibbs.png)

*Gibbs Reactor (Reaktoro) model*



The Reaktoro version of the Gibbs Reactor is a general-purpose isothermal reactor where the product species are determined on-the-fly. The user defines the reactive compounds, active elements and other parameters. The products formed will be a function of the global system Gibbs Free Energy according to the selected reactants and elements.

###### Setup and Calculation Guide {#setup-and-calculation-guide-4 .unnumbered}

1.  Setup the reactor connections.

2.  Phases: select the active phases (Aqueous, Gaseous, Liquid and Mineral) in the reactor output. You need to select at least one phase.

3.  Pressure Drop: set the pressure drop across the reactor.

4.  Component Database: select the component database to be used.

5.  Property Package: select the Property Package to be used.

6.  Compounds: select the compounds that will be fed to the reactor.

7.  Elements: select the active elements in the reactor.

8.  Compound Names: you can change the compound name mappings to match specific compounds in the currently selected database.

9.  Run the calculation once to get a list of product species.

10. Go back to the editor and map the product species back to DWSIM compounds. If more than one species is mapped to the same compound, the outlet flow of the compound will be the sum of the flows of the mapped species.

11. Run the calculation again to obtain the correct product flows.

