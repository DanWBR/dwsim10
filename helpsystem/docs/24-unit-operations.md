# Unit Operations

#### **Mixer**

##### Overview

The Mixer is a unit operation that combines two or more material streams into a single outlet stream. It performs rigorous mass and energy balances across all connected feeds, computing the outlet composition, temperature, pressure, and enthalpy from first principles. The Mixer is a passive unit — it adds no shaft work, heat, or pressure — and it models an ideal adiabatic mixing point.

##### Connections

The Mixer accepts up to six material stream inlets and produces exactly one material stream outlet. At least one inlet and the outlet must be connected for the calculation to proceed.

| **Port**  | **Direction** | **Type** | **Description**        |
|:----------|:--------------|:---------|:-----------------------|
| Inlet 1–6 | Inlet         | Material | Up to six feed streams |
| Outlet    | Outlet        | Material | Combined mixed stream  |

Mixer Connections

##### Calculation

The Mixer solves the following balances across all $N$ connected inlet streams, indexed by $k$ .

###### Total mass flow: {#total-mass-flow .unnumbered}



\[
\dot{m}_{\text{out}}=\sum_{k=1}^{N}\dot{m}_{k}
\]


###### Component mass fractions (mixing rule by mass): {#component-mass-fractions-mixing-rule-by-mass .unnumbered}



\[
w_{i,\text{out}}=\frac{{\displaystyle \sum_{k=1}^{N}\dot{m}_{k}\,w_{i,k}}}{\dot{m}_{\text{out}}}
\]


where $w_{i,k}$ is the mass fraction of component $i$ in stream $k$ .

###### Specific enthalpy (energy balance, adiabatic): {#specific-enthalpy-energy-balance-adiabatic .unnumbered}



\[
h_{\text{out}}=\frac{{\displaystyle \sum_{k=1}^{N}\dot{m}_{k}\,h_{k}}}{\dot{m}_{\text{out}}}
\]


##### Outlet pressure (user-configurable)

The outlet temperature and phase state are not set directly — the outlet stream is sent to a flash calculation at the computed $(P_{\text{out}},h_{\text{out}})$ , and the thermodynamic property package resolves the temperature and phase fractions.

When only a single inlet stream carries a non-zero flow, the outlet is assigned directly from that stream without a mixing flash, improving computational efficiency.

##### Pressure Calculation Mode

Because inlet streams may arrive at different pressures, the user must choose how the outlet pressure is determined:

| **Mode** | **Description** |
|:---|:---|
| Minimum (default) | The outlet pressure equals the lowest pressure among all connected inlet streams. This is the physically conservative choice, as it represents the pressure level at which all streams can coexist without back-flow. |
| Maximum | The outlet pressure equals the highest pressure among all inlet streams. Useful when modeling a mixing point downstream of a check valve where the highest-pressure stream sets the system pressure. |
| Average | The outlet pressure is the arithmetic mean of all inlet pressures. Suitable as an approximation when inlet pressures are close to each other. |

Mixer Pressure Calculation Modes

##### Dynamic Mode

The Mixer supports dynamic simulation. In dynamic mode, the outlet pressure is propagated back to all inlet streams (rather than being derived from them), so the upstream equipment drives the pressure. The mass and energy balances remain identical to steady-state.

#### **Splitter**

##### Overview

The Splitter divides a single material stream into two or three outlet streams. All outlets share the same temperature, pressure, composition, and specific enthalpy as the inlet — the Splitter performs no phase separation, heat exchange, or composition change. It is a purely mechanical flow distribution device, analogous to a pipe tee or manifold.

##### Connections

The Splitter has one material stream inlet and up to three material stream outlets. Outlet ports must be connected sequentially — port 2 cannot be used unless port 1 is already connected, and port 3 cannot be used unless port 2 is already connected.

| **Port** | **Direction** | **Type** | **Description**                  |
|:---------|:--------------|:---------|:---------------------------------|
| Inlet    | Inlet         | Material | Single feed stream               |
| Outlet 1 | Outlet        | Material | First product stream             |
| Outlet 2 | Outlet        | Material | Second product stream (optional) |
| Outlet 3 | Outlet        | Material | Third product stream (optional)  |

Splitter Connections

##### Operating Modes

The Splitter supports three operating modes that control how the outlet flows are specified:

| **Mode** | **Description** |
|:---|:---|
| Split Ratios (default) | The user specifies dimensionless split fractions $\alpha_1, \alpha_2, \ldots$ for each outlet. The fractions must sum to 1. The last fraction is always calculated automatically as the complement of the others. |
| Stream Mass Flow Specification | The user specifies the mass flow rate (kg/s) for outlets 1 and (optionally) 2. The remaining outlet receives the balance of the inlet mass flow. The specified flows must not exceed the total inlet mass flow. |
| Stream Mole Flow Specification | The user specifies the molar flow rate (kmol/s) for outlets 1 and (optionally) 2. The remaining outlet receives the balance of the inlet molar flow. The specified flows must not exceed the total inlet molar flow. |

Splitter Operating Modes

##### Calculation

###### Split Ratios mode — outlet mass flows are: {#split-ratios-mode-outlet-mass-flows-are .unnumbered}



\[
\dot{m}_{i}=\alpha_{i}\,\dot{m}_{\text{in}},\qquad\sum_{i=1}^{n}\alpha_{i}=1
\]


The last split fraction is always computed as the complement:



\[
\alpha_{n}=1-\sum_{i=1}^{n-1}\alpha_{i}
\]


###### Stream Mass Flow Specification mode: {#stream-mass-flow-specification-mode .unnumbered}



\[
\begin{align}
\dot{m}_{1} & =\dot{m}_{\text{spec},1}\\
\dot{m}_{2} & =\dot{m}_{\text{spec},2}\quad\text{(if three outlets)}\\
\dot{m}_{n} & =\dot{m}_{\text{in}}-\sum_{i=1}^{n-1}\dot{m}_{\text{spec},i}
\end{align}
\]


with the constraint $\dot{m}{\text{spec},1}+\dot{m}{\text{spec},2}\leq\dot{m}_{\text{in}}$ .

###### Stream Mole Flow Specification mode {#stream-mole-flow-specification-mode .unnumbered}

Identical in structure to mass flow specification, applied to molar flows $\dot{n}_{i}$ .

###### Property propagation {#property-propagation .unnumbered}

For every outlet $i$ , regardless of mode:



\[
T_{i}=T_{\text{in}},\quad P_{i}=P_{\text{in}},\quad h_{i}=h_{\text{in}},\quad x_{c,i}=x_{c,\text{in}}\;\forall c
\]


No flash calculation is performed on the outlet streams — they inherit the inlet phase state and composition identically.

##### Split Fraction Specifications

| **Property** | **Access** | **Description** |
|:---|:---|:---|
| $\alpha_1$ (SR1) | Read/Write | Split fraction for Outlet 1 |
| $\alpha_2$ (SR2) | Read/Write | Split fraction for Outlet 2 (three-outlet mode only) |
| $\alpha_3$ (SR3) | Read only | Auto-calculated complement: $1 - \alpha_1 - \alpha_2$ |

Split Fraction Properties

##### Dynamic Mode

The Splitter supports dynamic simulation. In dynamic mode, the inlet flow is back-calculated as the sum of all outlet flows, allowing downstream pressure specifications to drive the split. Composition and thermal properties continue to propagate from inlet to all outlets unchanged.

##### Constraints and Validation

Outlet ports must be connected sequentially.

In Split Ratios mode, all fractions must lie in $[0,1]$ and sum to 1.

In mass or mole flow specification modes, the sum of specified outlet flows must not exceed the total inlet flow; the simulation raises an error if this constraint is violated.

#### **Separator Vessel**

##### Overview

The Separator Vessel, also known as a Flash Drum, is a unit operation that separates a mixed feed stream into its vapor and liquid phases based on thermodynamic equilibrium. It is one of the most commonly used pieces of equipment in chemical process simulation, representing vessels where pressure reduction or heat addition/removal causes a feed to partially vaporize or condense, allowing the resulting phases to be collected separately.

In DWSIM, the Separator Vessel accepts up to six material inlet streams and one optional energy (heat) stream. It produces up to four material outlet streams — one for the vapor phase, one or two for liquid phases, and one auxiliary outlet — plus an optional energy outlet stream.

##### Operating Modes

The Separator Vessel supports two phase-separation modes:

- **Two-Phase Separation**: The vessel separates the feed into a vapor phase and a single liquid phase. This is the standard mode for most flash drum applications.

- **Three-Phase Separation:** The vessel separates the feed into a vapor phase, a light liquid phase, and a heavy liquid phase. This mode is used when two immiscible liquid phases form (e.g., an aqueous phase and an organic phase). The lighter liquid (lower density) is directed to the first liquid outlet, and the heavier liquid (higher density) is directed to the second liquid outlet. If solid-phase material is present, it is distributed proportionally between the liquid outlets based on their respective mass flow ratios.

##### Calculation Modes

Four calculation modes are available, each defining how the flash equilibrium conditions are determined:

- **Adiabatic:** The vessel operates with no heat exchange with the surroundings. When multiple inlet streams are present, they are mixed and a pressure-enthalpy (PH) flash is performed at the mixed conditions. For a single inlet stream, the outlet conditions match the inlet without additional equilibrium calculation.

- **Legacy:** The separation is performed at a specified temperature and pressure. The user may override the inlet temperature, the inlet pressure, or both. If an energy stream is connected, its heat duty is added to the system before the flash calculation.

- **Isothermic (Heating/Cooling)**: A specified amount of heat is added to or removed from the system while maintaining constant temperature. The vessel adjusts its operating pressure to satisfy the energy balance at the given temperature.

- **Isobaric (Heating/Cooling):** A specified amount of heat is added to or removed from the system while maintaining constant pressure. The vessel adjusts its operating temperature to satisfy the energy balance at the given pressure.

##### Inlet Pressure Handling

When multiple inlet streams are connected, the vessel must determine a single operating pressure for the flash calculation. Three options are available:

- **Minimum (default)**: Uses the lowest pressure among all connected inlet streams.

- **Maximum:** Uses the highest pressure among all connected inlet streams.

- **Average:** Uses the arithmetic average of all inlet stream pressures.

##### Heat Transfer and Energy Streams

An optional energy inlet stream can be connected to supply or remove heat from the vessel. Additionally, a rigorous heat balance option is available, which accounts for heat transfer through the vessel walls, including wall material thermal conductivity, insulation layers, ambient temperature, and optionally solar radiation. When enabled, the rigorous heat balance calculates internal and external heat transfer coefficients and adjusts the vessel’s energy balance accordingly.

##### Vessel Sizing {#vessel-sizing}

The Separator Vessel includes built-in sizing calculations for both vertical and horizontal orientations. The sizing routine estimates the vessel diameter and length (or height) based on the vapor disengaging velocity, which is a function of the liquid-to-vapor density difference. Key sizing parameters include:

- **Dimension Ratio:** The length-to-diameter ratio (default: 3).

- **Surge Factor:** An oversizing multiplier applied to the calculated volume (default: 1.2).

- **Residence Time:** The required liquid residence time in seconds (default: 5 s).

- **Wall Thickness:** The vessel shell thickness in meters (default: 0.01 m).

- **Wall Material:** Options include Steel, Carbon Steel, Cast Iron, Stainless Steel, and Commercial Copper.

- **Head Type:** The vessel end-cap geometry, such as Ellipsoidal (2:1), Hemispherical, or Torispherical (ASME F&D).

Nozzle diameters for the inlet, vapor outlet, and liquid outlet are also estimated based on velocity constraints.

##### Dynamic Mode

The Separator Vessel supports dynamic (time-dependent) simulation. In dynamic mode, the vessel tracks the holdup of liquid and vapor inside the vessel over time, and the liquid level changes as inlet and outlet flow rates vary. Key dynamic parameters include vessel volume, liquid level, operating pressure, and a minimum pressure constraint. The vessel contents can be initialized from an inlet stream or reset to empty at the start of a dynamic run.

#### Safety Valve (Pressure Safety Valve / Relief Valve) {#safety-valve-pressure-safety-valve-relief-valve}

##### Overview

The Safety Valve, also referred to as a Pressure Safety Valve (PSV) or Relief Valve, is a unit operation designed for dynamic simulations. It models a spring-loaded pressure relief device intended to protect pressurized equipment — such as separator vessels, pipes, tanks, and reactors — from exceeding safe operating pressure limits. When the inlet pressure rises above a user-defined set point, the valve opens progressively, allowing fluid to discharge until the pressure is relieved. When the pressure falls back below the set point, the valve closes.

In DWSIM, the Safety Valve is classified as a pressure changer and uses the naming prefix "PSV-". It connects to the pressurized equipment on its inlet side and to a downstream relief header, blowdown drum, or atmosphere on its outlet side. Because it is purpose-built for dynamic simulation, its steady-state Calculate method performs no calculation — the valve only activates during dynamic integrator runs.

##### Connections

The Safety Valve has two connection ports:

| **Port** | **Direction** | **Description** |
|:---|:---|:---|
| Inlet | In | Connected to the pressurized equipment (vessel, pipe, etc.) |
| Outlet | Out | Connected to the relief destination (blowdown system, flare, atmosphere) |

Safety Valve Connection Ports

Both the inlet and outlet streams must have their dynamic specification type set to Pressure for the valve to operate correctly during dynamic simulation.

##### Pressure Set Points

Two pressure thresholds govern the valve’s behavior:

- **Set Point Pressure:** The pressure at which the valve begins to open. Below this pressure, the valve remains fully closed and no flow passes through it.

- **Fully Opened Pressure:** The pressure at which the valve reaches its maximum opening. Between the set point and this value, the valve opening varies continuously.

The valve opening percentage is calculated as:



\[
\text{Opening} (\%) = \frac{P_{\text{inlet}} - P_{\text{set point}}}{P_{\text{fully opened}} - P_{\text{set point}}}
\]


This value is clamped between 0% (fully closed) and 100% (fully open), providing a modulating action rather than a simple on/off behavior.

| **Letter** | **Area (in<sup>2</sup>)** | **Area (cm<sup>2</sup>)** |
|:-----------|--------------------------:|--------------------------:|
| D          |                      0.11 |                      0.71 |
| E          |                      0.20 |                      1.26 |
| F          |                      0.31 |                      1.98 |
| G          |                      0.50 |                      3.24 |
| H          |                      0.79 |                      5.06 |
| J          |                      1.29 |                      8.30 |
| K          |                      1.84 |                     11.85 |
| L          |                      2.85 |                     18.40 |
| M          |                      3.60 |                     23.23 |
| N          |                      4.34 |                     28.00 |
| P          |                      6.38 |                     41.16 |
| Q          |                     11.05 |                     71.29 |
| R          |                     16.00 |                    103.22 |
| T          |                     26.00 |                    167.74 |

API 526 Standard Orifice Sizes

##### Correction Coefficients

Three dimensionless coefficients allow the user to account for real-world deviations from ideal nozzle flow:

- **Discharge Coefficient ( $K_{d}$ ):** Accounts for losses due to the actual valve geometry compared to an ideal nozzle. Default: 1.0.

- **Back Pressure Coefficient ( $K_{b}$ ):** Corrects for the effect of downstream (back) pressure on the valve’s relieving capacity, particularly important for balanced-bellows or pilot-operated valves. Default: 1.0.

- **Viscosity Coefficient ( $K_{v}$ ):** Corrects for the effect of fluid viscosity on flow through the orifice, relevant for highly viscous liquids. Default: 1.0.

##### Valve Characteristic Curves

The relationship between the valve’s opening percentage and its effective flow coefficient ( $K_{v}/K_{v,\text{max}}$ ) can be configured using one of five characteristic types:

- **Linear:** The flow coefficient is directly proportional to the opening percentage. This is the default.

- **Equal Percentage:** The flow coefficient follows an exponential curve, producing small flow changes at low openings and large changes near full opening. A characteristic parameter controls the curve shape.

- **Quick Opening:** The flow coefficient follows a square-root curve, producing a large initial flow increase at low openings that tapers off as the valve opens further.

- **User-Defined Expression:** A mathematical expression is evaluated at runtime. The variable $OP$ represents the opening percentage (0–100), and standard math functions are available.

- **Data Table:** The user provides a table of opening percentage versus $K_{v}/K_{v,\text{max}}$ percentage pairs, and the valve interpolates between them during simulation.

The corresponding equations are:



\[
K_{vc} = \frac{OP}{100} \quad \text{(Linear)}
\]




\[
K_{vc} = R^{\left(\frac{OP}{100} - 1\right)} \quad \text{(Equal Percentage, where } R \text{ is the characteristic parameter)}
\]




\[
K_{vc} = \sqrt{\frac{OP}{100}} \quad \text{(Quick Opening)}
\]


##### Flow Calculation

During each dynamic time step, the valve determines the mass flow rate through the orifice based on the phase of the inlet fluid.

- **Vapor (gas) flow** — The valve first checks whether the flow is choked (sonic) by comparing the outlet-to-inlet pressure ratio against the critical pressure ratio:



\[
r_c = \left(\frac{2}{\gamma + 1}\right)^{\frac{\gamma}{\gamma - 1}}
\]


where $\gamma=C_{p}/C_{v}$ is the heat capacity ratio of the gas.

- **Choked flow** (when $P_{2}/P_{1}\geq r_{c}$ ):



\[
\dot{m} = A \cdot K_{vc} \cdot K_d \cdot K_b \cdot
\sqrt{\frac{P_1 \cdot \gamma}{v_1} \cdot
\left(\frac{2}{\gamma + 1}\right)^{\frac{\gamma - 1}{\gamma + 1}}}
\]


- **Non-choked flow** (when $P_{2}/P_{1}<r_{c}$ ):



\[
\dot{m} = A \cdot K_{vc} \cdot K_d \cdot
\sqrt{\frac{P_1}{v_1} \cdot \frac{2\gamma}{\gamma + 1} \cdot
\left[\left(\frac{P_2}{P_1}\right)^{\frac{2}{\gamma}} -
\left(\frac{P_2}{P_1}\right)^{\frac{\gamma + 1}{\gamma}}\right]}
\]


- **Liquid flow:**



\[
\dot{m} = A \cdot K_{vc} \cdot K_d \cdot K_v \cdot
\sqrt{2 \cdot (P_1 - P_2) \cdot \rho}
\]


where $A$ is the orifice area, $P_{1}$ and $P_{2}$ are the inlet and outlet pressures, $v_{1}$ is the inlet specific volume, and \$\rho\$ is the inlet liquid density.

The expansion across the valve is treated as isenthalpic — the outlet stream enthalpy equals the inlet stream enthalpy, and the fluid composition is preserved unchanged.

**Note:** Two-phase (mixed vapor-liquid) flow through the valve is not currently supported. If the inlet vapor fraction falls between 0.01 and 0.99, the calculation will raise an error.

##### Key Specifications Summary

| **Property** | **Description** | **Default** |
|:---|:---|:--:|
| Set Point Pressure | Pressure at which the valve begins to open | 0 Pa |
| Fully Opened Pressure | Pressure at which the valve is 100% open | 0 Pa |
| Orifice Area | Effective flow area of the valve nozzle | 0.71 cm<sup>2</sup> (letter D) |
| Discharge Coefficient ($K_d$) | Geometric flow loss correction | 1.0 |
| Back Pressure Coefficient ($K_b$) | Downstream pressure correction | 1.0 |
| Viscosity Coefficient ($K_v$) | Fluid viscosity correction | 1.0 |
| Characteristic Type | Opening-to-$K_v$ relationship curve | Linear |

Safety Valve Key Specifications

##### Usage Notes

- The Safety Valve is only active during dynamic simulation runs. In steady-state mode, it performs no calculation.

- Connect the inlet port to a pressurized unit operation (separator vessel, pipe segment, tank) and the outlet to the relief destination.

- Both connected streams must use the **Pressure** dynamic specification type.

- Set the set point pressure to the equipment’s maximum allowable working pressure (MAWP) or the desired relief threshold, and the fully opened pressure to the accumulation pressure (typically 10–21% above set point, per applicable codes).

- Select the appropriate API 526 orifice size or use the built-in PSV sizing utility to determine the correct orifice area for your scenario.

- Adjust the correction coefficients based on the valve manufacturer’s data or applicable standard (API 520/521).

##### Orifice Area

The orifice area defines the effective flow area of the valve when fully open. The user can enter a custom value or select from a set of standard API 526 orifice letter designations:

#### **Tank**

In DWSIM, the Tank model applies a user-specified pressure drop to the process fluid at constant temperature (adiabatic operation).

###### *Input Parameters* {#input-parameters-1 .unnumbered}

- Pressure drop: pressure difference between the outlet and inlet streams.

###### *Calculation Method* {#calculation-method-1 .unnumbered}

The outlet pressure is computed as the inlet pressure minus the specified pressure drop. The temperature is assumed constant (no heat exchange). A TP flash is then performed at the outlet conditions to determine the phase distribution and thermodynamic properties of the outlet stream.

###### *Output Parameters* {#output-parameters-2 .unnumbered}

There are no output parameters for this object.

#### **Pipe Segment**

The Pipe Segment unit operation models single- and two-phase fluid flow through piping systems, computing pressure drop and heat transfer along the pipe length. Several widely used pressure-drop correlations are available (see below). The thermal profile can be calculated rigorously by specifying heat-transfer coefficients and ambient conditions. By combining multiple Pipe Segments with the Recycle logical operation, complex piping networks (e.g., water distribution systems) can be modeled.

The pipe is subdivided into sections, each of which can represent a straight tube, a fitting (valve, elbow, tee, etc.), or a change in elevation. Each section is further discretized into a user-specified number of increments for the numerical integration of the pressure and temperature profiles.

###### *Input Parameters* {#input-parameters-2 .unnumbered}

- Hydraulic profile: the pipe hydraulic profile editor allows the user to define each section’s type (straight tube, fitting, etc.), number of computational increments, pipe material, length, elevation change, and internal and external diameters.

- Pressure drop correlation: select the model to be used for the pressure drop calculation in the pipe segment.

- Thermal profile: In the thermal profile editor it is possible to define how the temperature profile in the pipe should be calculated. The configurations in this window are valid for the **entire** pipe segment. Changes are saved automatically.

###### *Calculation Method* {#calculation-method-2 .unnumbered}

The pipe segment is solved by marching incrementally along the pipe length, performing coupled mass and energy balances at each increment. The algorithm uses three nested iteration loops: the outer loop advances through successive increments, the middle loop converges the temperature, and the inner loop converges the pressure. The procedure at each increment is as follows:

1.  The inlet temperature and pressure are used to estimate the increment outlet pressure and temperature.

2.  Fluid properties are evaluated at the arithmetic mean of the inlet and estimated outlet conditions.

3.  The calculated properties and the inlet pressure are used to calculate the pressure drop. With it, the outlet pressure is calculated.

4.  The calculated outlet pressure is compared with the initial estimate; if the difference exceeds the convergence tolerance, the estimate is updated and steps 2–3 are repeated.

5.  Once the internal loop has converged, the outlet temperature is calculated. If the global heat transfer coefficient (U) was given, the outlet temperature is calculated from the following equation:\


\[
Q=UA\Delta T_{ml}
\]


    \
    where: $Q$ = heat transferred, $A$ = heat transfer area (external surface) and $\Delta T_{ml}$ = logarithmic mean temperature difference.

6.  The calculated temperature is compared to the estimated one, and if their difference exceeds the specified tolerance, a new temperature is estimated and new properties are calculated (return to step 2).

7.  Once both the pressure and temperature have converged within their respective tolerances, the computed outlet conditions become the inlet conditions for the next increment, and the procedure repeats.

###### *Output Parameters* {#output-parameters-3 .unnumbered}

- Delta-T: temperature variation in the pipe segment.

- Delta-P: pressure variation in the pipe segment.

- Heat exchanged: amount of heat exchanged with the environment, or lost by friction in the pipe walls.

- Results (table): results are show section by section in a table.

- Results (graph): a graph shows the temperature, pressure, liquid holdup, velocity and heat exchanged profiles.

##### Description of calculation methods

###### Mass and Heat balance

For each section on each segment, do:







Step 1 Read properties from the fluid entering the section

Step 2 For emulsion viscosity calculation:





\[
\phi=\frac{Q_{l1}}{Q_{l1}+Q_{l2}}
\]




\[
\eta_{lh}=\eta_{l1}\exp\left[3.6\left(1-\phi\right)\right]
\]




\[
\eta_{ll}=\eta_{l2}\left(1+2.5\phi\frac{\left(\eta_{l1}+0.4\eta_{l2}\right)}{\left(\eta_{l1}+\eta_{l2}\right)}\right)
\]




\[
\begin{align}
\phi & >0.5:\eta_{liq}=\eta_{lh}\\
\phi & <0.33:\eta_{liq}=\eta_{ll}\\
0.33<\phi & <0.5:\eta_{liq}=\frac{\phi-0.33}{0.17}\eta_{lh}+\left(1-\frac{\phi-0.33}{0.17}\right)\eta_{ll}
\end{align}
\]








Step 3 For slurry viscosity calculation:





\[
\phi_{s}=\frac{Q_{s}}{Q_{l}+Q_{s}}
\]




\[
\eta_{r}=1+3\frac{\phi_{s}}{1-\frac{\phi_{s}}{0.52}}
\]




\[
\eta_{slurry}=\eta_{liq}\times\eta_{r}
\]








Step 4 Start heat balance for the section, with initial estimate for $T_{2}$ :





\[
T_{ext}>T_{1}:T_{2}=1.005T_{1}
\]




\[
T_{ext}<T_{1}:T_{2}=\nicefrac{T_{1}}{1.005}
\]








Step 5 Temperature convergence loop begins

Step 6 Calculate pressure drop for section using Beggs-Brill (), Lockhart-Martinelli () or Petalas-Aziz () models. Calculation defaults to Darcy-Weisbach correlation for single phase fluids:





\[
\Delta P_{f}=f\frac{Lv^{2}}{2gD}
\]




\[
\begin{align}
Re & <2100:f=\frac{64}{Re}\\
Re & >4000:f=\log\left[\frac{\left(\frac{k}{D}\right)}{2.8257}^{1.1096}+\left(\frac{5.8506}{Re}\right)^{0.8961}\right]\\
2100\leq Re & \leq4000:f=8\left(\frac{\left(\frac{8}{Re}\right)^{12}}{\left(\left(2.457\log\left(\frac{1}{\left(\frac{7}{Re}\right)^{0.9}+0.27\frac{\varepsilon}{D}}\right)^{16}\right)+\left(\frac{37530}{Re}\right)^{16}\right)^{1.5}}\right)^{\frac{1}{12}}
\end{align}
\]




\[
\Delta P_{h}=9.8\rho\sin\left(\arcsin\left(\frac{\Delta h}{L}\right)\right)L
\]




\[
\Delta P_{t}=\Delta P_{h}+\Delta P_{f}
\]








Step 7 Calculate outlet pressure $P_{2}$ and compare with previous calculation. After 3 pressure iterations, accelerate convergence with a secant procedure:





\[
P_{2}=P_{1}-\Delta P_{t}
\]




\[
n_{iterations,P}>3:P_{2}=P_{2}-f_{P}\frac{P_{2}-P_{2,i-1}}{f_{P}-f_{P,i-1}}
\]


where



\[
f_{P}=P_{2}-P_{2,i-1}
\]




\[
f_{P,i-1}=P_{2,i-1}-P_{2,i-2}
\]








Step 8 Calculate Overall Heat Transfer Coefficient ( $U$ )



If $U$ was defined by the user, read value directly from input. Otherwise, calculate it with



\[
\frac{1}{U}=\frac{1}{h_{i}}+\frac{D_{i}}{\log\left(\frac{D_{e}}{D_{i}}\right)k_{wall}}+\frac{D_{e}}{\ln\left(\frac{D_{e}+2L_{ins}}{D_{e}}\right)k_{ins}}+\frac{D_{i}}{\left(D_{e}+2L_{ins}\right)h_{e}}
\]




\[
h_{i}=\frac{k_{fluid}}{D_{i}}\frac{f}{8}\left(Re-1000\right)\frac{Pr}{1+12.7\sqrt{\frac{f}{8}}\left(Pr^{2/3}-1\right)}
\]




\[
h_{e}=0.25\frac{k_{fluid,e}}{D_{e}}Re^{0.6}Pr^{0.38}
\]








Step 9 Calculate Heat Exchanged ( $Q$ )





\[
\Delta Q=\frac{\left(T_{2}-T_{1}\right)}{\ln\left(\frac{T_{ext}-T_{1}}{T_{ext}-T_{2}}\right)}UA
\]




\[
A=\pi D_{e}L
\]


If Solar Irradiation is included, then



\[
\Delta Q=\Delta Q+Q_{solar}
\]




\[
Q_{solar}=\frac{S_{r}}{t_{flux}}A
\]




\[
t_{flux}=\pi\frac{D_{e}^{2}}{4}L\left(Q_{v}+Q_{l}+Q_{s}\right)
\]


$Q_{v}$ , $Q_{l}$ and $Q_{s}$ are the volumetric flow rates of vapor, liquid and solid, respectively.







Step 8 Calculate outlet temperature $T_{2}$ and compare with previous calculation:





\[
f_{T}=T_{2}-T_{2,i-1}
\]


If $f_{T}$ is less than the convergence tolerance, the temperature has converged and the algorithm proceeds to the next section. Otherwise, calculate the new average fluid properties with $P_{avg}=\frac{\left(P_{1}-P_{2}\right)}{2}$ and $T_{avg}=\frac{\left(T_{1}-T_{2}\right)}{2}$ and return to Step 2.

#### **Valve**

The Valve models an isenthalpic throttling process: the fluid undergoes a pressure reduction at constant enthalpy, and the outlet temperature and phase state are determined by a Pressure–Enthalpy (PH) flash at the reduced pressure.

###### *Input Parameters* {#input-parameters-3 .unnumbered}

- Pressure drop: pressure difference between the outlet and inlet streams.

###### *Calculation Method* {#calculation-method-3 .unnumbered}

The outlet pressure is computed as the inlet pressure minus the specified pressure drop. A Pressure–Enthalpy (PH) flash at the outlet pressure and the inlet enthalpy then determines the outlet temperature and phase state. Because the process is isenthalpic, the outlet temperature is typically equal to or lower than the inlet temperature (Joule–Thomson cooling), except for fluids whose Joule–Thomson coefficient is negative at the operating conditions.

###### *Output Parameters* {#output-parameters-4 .unnumbered}

- Delta-T: temperature drop observed in the valve expansion process.

##### Opening (OP)/Flow Coefficient (Kv\[Cv\]) Relationship Types

- **Linear**



\[
K_{V}=K_{Vmax}\times OP
\]


- **Quick Opening**



\[
K_{V}=K_{Vmax}\times\sqrt{OP}
\]


- **Equal Percentage**



\[
K_{V}=K_{Vmax}\times R^{OP-1}
\]


where $R$ is the characteristic parameter (range: 20 - 50)

- **User-Defined Expression**



\[
K_{V}=K_{Vmax}\times f(OP)
\]


- **Data Table**

Actual flow coefficient value is determined by interpolating data from a user-defined table.

#### **Pump**

The Pump increases the pressure of a liquid stream by converting shaft work into hydraulic energy. The ideal (isentropic) work is corrected by a user-specified adiabatic efficiency to account for irreversibilities (friction, recirculation losses, etc.).

###### *Input Parameters* {#input-parameters-4 .unnumbered}

- Delta-P: pressure rise in the pump.

- Efficiency: pump adiabatic efficiency;

- Ignore vapor in the inlet stream: defines if the calculator should ignore any vapor in the inlet stream;

- Use the provided Delta-P: defines if the pressure of the outlet stream will be calculated by the user-defined Delta-P or the energy stream connected to the pump.

###### *Calculation Method* {#calculation-method-4 .unnumbered}

Two operating modes are available. In the first mode, the pressure rise is specified and the required power is calculated:

- Outlet stream enthalpy:



\[
H_{2}=H_{1}+\frac{\Delta P}{\rho},
\]


- Pump discharge pressure:



\[
P_{2}=P_{1}+\Delta P
\]


- Pump required power:



\[
Pot=\frac{W\left(H_{2}-H_{1}\right)}{\eta},
\]


where:







$Pot$ pump power

$W$ mass flow

$H_{2}$ outlet stream specific enthalpy

$H_{1}$ inlet stream specific enthalpy

$\eta$ pump efficiency



- Outlet temperature: PH Flash (with P2 and H2).

In the second mode, the available shaft power is specified (via an energy stream) and the achievable pressure rise is calculated:

- Outlet stream enthalpy:



\[
H_{2}=H_{1}+\frac{Pot\,\eta}{W},
\]


- $\Delta P$ :



\[
\Delta P=\rho(H_{2}-H_{1}),
\]


- Discharge pressure:



\[
P_{2}=P_{1}+\Delta P
\]


- Outlet temperature: PH Flash.

###### *Outlet Parameters* {#outlet-parameters .unnumbered}

- Delta-T: temperature variation in the pumping process.

- Power required: power required by the pump.

#### **Compressor/Expander**

The Compressor/Expander models the compression or expansion of a vapor-phase stream. The ideal reference process is isentropic (constant entropy); irreversibilities are accounted for through a user-specified efficiency. The user selects between an adiabatic (isentropic) or polytropic thermodynamic path, depending on the available performance data.

###### *Input Parameters* {#input-parameters-5 .unnumbered}

- Delta-P: pressure change in the equipment.

- Efficiency: adiabatic/polytropic efficiency;

- Ignore liquid in the inlet stream: defines if the calculator should ignore any liquid in the inlet stream;

- Thermodynamic path: select the thermodynamic path according to the experimental/field data available.

###### *Calculation Method* {#calculation-method-5 .unnumbered}

Isentropic (Adiabatic) or Polytropic power is calculated from:



\[
P=\frac{H_{2s}-H_{1}}{\eta}W
\]


for compressor, and



\[
P=\left(H_{2s}-H_{1}\right)\times W\times\eta
\]


for expander, where:







$H_{2s}$ Outlet Enthalpy for Isentropic Process

$H_{1}$ Inlet Enthalpy

$W$ Mass Flow

$\eta$ Adiabatic or Polytropic Efficiency



Isentropic (Adiabatic) and Polytropic Coefficients are calculated from:



\[
n_{i}=\frac{\ln\left(P_{2}/P_{1}\right)}{\ln\left(\rho_{2i}/\rho_{1}\right)}
\]




\[
n_{p}=\frac{\ln\left(P_{2}/P_{1}\right)}{\ln\left(\rho_{2}/\rho_{1}\right)}
\]


where:







$\rho_{2i}$ Outlet Gas Density calculated with Inlet Gas Entropy



Adiabatic and Polytropic Heads are calculated from:



\[
H=\frac{P}{W\times g}
\]








where:

$H$ Adiabatic or Polytropic Head

$P$ Adiabatic or Polytropic Power

$W$ Mass Flow

$\eta$ Adiabatic or Polytropic Efficiency

$g$ Gravitational Constant (9.8 m/s2)



#### **Heater/Cooler**

The Heater and Cooler are single-sided heat exchange models: they add or remove thermal energy from a process stream without explicitly modeling the utility-side fluid. They are used to represent furnaces, electric heaters, cooling-water exchangers, or any other device whose duty or outlet condition is known but whose utility stream need not be simulated.

###### *Calculation Modes* {#calculation-modes-1 .unnumbered}

- **Energy Stream:** the energy flow from a connected stream is used to heat or cool the inlet stream.

- **Define Outlet Temperature:** the outlet temperature is defined and the amount of heat added or removed is calculated and written to the energy stream.

- **Define Outlet Vapor Fraction:** the quality of the outlet fluid is defined and the required amount of heat to add or remove is written to the energy stream.

- **Temperature Change:** the temperature difference is defined and the required amount of heat to add or remove is written to the energy stream.

- **Heat Added/Removed:** the heat added or removed is defined directly from the unit operation.

###### *Input/Output Parameters* {#inputoutput-parameters .unnumbered}

- **Pressure Drop (input only):** defines the pressure drop in the heater/cooler.

- **Heating/Cooling:** used as input in *Heat Added/Removed* calculation mode, otherwise it is a calculated value.

- **Efficiency (input only):** heating/cooling efficiency.

- **Outlet Vapor Fraction:** used as input in *Define Outlet Vapor Fraction* mode, otherwise it is a calculated value.

- **Outlet Temperature:** used as input in *Define Outlet Temperature* mode, otherwise it is a calculated value.

- **Temperature Change:** used as input in *Temperature Change* mode, otherwise it is a calculated value.

#### Shortcut Column

##### Overview

The Shortcut Column is a unit operation that performs approximate distillation design calculations using classical shortcut methods. Rather than solving the full stage-by-stage MESH equations of a rigorous column, it applies the Fenske–Underwood–Gilliland (FUG) method to rapidly estimate the minimum number of stages, minimum reflux ratio, actual number of ideal stages, optimal feed stage location, internal flow rates, and condenser and reboiler duties. It is well suited for early-stage process design, equipment sizing surveys, and generating initial estimates for rigorous column simulations.

The Shortcut Column accepts a single multicomponent feed stream and produces a distillate and a bottoms product. The separation is characterized by specifying a **light key** and a **heavy key** component, whose recoveries in the top and bottom products define the desired split. The column uses the connected thermodynamic property package to compute K-values, relative volatilities, and product enthalpies at the operating conditions.

##### Connections

| **Port**       | **Direction** | **Type** | **Description**                      |
|:---------------|:--------------|:---------|:-------------------------------------|
| Feed           | Inlet         | Material | Single feed stream                   |
| Distillate     | Outlet        | Material | Overhead product                     |
| Bottoms        | Outlet        | Material | Bottom product                       |
| Condenser Duty | Outlet        | Energy   | Heat removed by condenser (optional) |
| Reboiler Duty  | Inlet         | Energy   | Heat added at reboiler (optional)    |

Shortcut Column Connections

##### Condenser Types

| **Type** | **Description** |
|:---|:---|
| Total Condenser | All overhead vapor is condensed. The distillate is withdrawn as a saturated or subcooled liquid. The reflux is returned as liquid. The distillate temperature is calculated at the bubble point. |
| Partial Condenser | The overhead vapor is only partially condensed. The distillate is withdrawn as a saturated vapor in equilibrium with the reflux liquid. The distillate temperature is calculated at the dew point. |

Condenser Types

##### Key Component Specification and Recovery

The user designates one component as the **light key (LK)** — the most volatile component that should primarily report to the bottoms — and one as the **heavy key (HK)** — the least volatile component that should primarily report to the distillate. All components lighter than the LK are assumed to report entirely to the distillate (light non-keys); all components heavier than the HK are assumed to report entirely to the bottoms (heavy non-keys).

Recovery is controlled indirectly through two purity specifications:

- **Heavy key mole fraction in distillate** ( $x_{D,\text{HK}}$ ): controls how much HK is allowed to appear in the overhead product (default: 0.01).

- **Light key mole fraction in bottoms** ( $x_{B,\text{LK}}$ ): controls how much LK is allowed to appear in the bottoms product (default: 0.01).

The distribution of all remaining (distributed) non-key components between the two products is then solved iteratively.

##### Calculation Sequence

###### Step 1 — Feed Characterization {#step-1-feed-characterization .unnumbered}

The feed thermal condition parameter $q$ is computed from the feed enthalpy relative to its bubble-point and dew-point enthalpies:



\[
q=1+\frac{h_{\text{bub}}-h_{F}}{h_{\text{dew}}-h_{\text{bub}}}
\]


where $h_{F}$ is the molar enthalpy of the feed, $h_{\text{bub}}$ is the enthalpy at the bubble point, and $h_{\text{dew}}$ is the enthalpy at the dew point at the feed pressure.

###### Step 2 — Relative Volatilities {#step-2-relative-volatilities .unnumbered}

K-values for all components are evaluated at the feed temperature and pressure using the selected property package. The relative volatility of each component is defined with respect to the heavy key:



\[
\alpha_{i}=\frac{K_{i}}{K_{\text{HK}}}
\]


Components are then classified as light non-keys ( $\alpha_{i}>\alpha_{\text{LK}}$ ), distributed non-keys ( $\alpha_{\text{HK}}<\alpha_{i}<\alpha_{\text{LK}}$ ), or heavy non-keys ( $\alpha_{i}<\alpha_{\text{HK}}$ ).

###### Step 3 — Minimum Number of Stages (Fenske Equation) {#step-3-minimum-number-of-stages-fenske-equation .unnumbered}

The minimum number of ideal stages at total reflux is:



\[
N_{\min}=\frac{\ln\!\left(\dfrac{x_{D,\text{LK}}}{x_{D,\text{HK}}}\cdot\dfrac{x_{B,\text{HK}}}{x_{B,\text{LK}}}\right)}{\ln\!\left(\dfrac{\alpha_{\text{LK}}}{\alpha_{\text{HK}}}\right)}
\]


The Fenske equation is also used iteratively to distribute each non-key component between the two products, consistent with the specified key recoveries.

###### Step 4 — Minimum Reflux Ratio (Underwood’s Method) {#step-4-minimum-reflux-ratio-underwoods-method .unnumbered}

The Underwood equation finds the root \$\theta\$ (in the interval $\alpha_{\text{HK}}<\theta<\alpha_{\text{LK}}$ ) of:



\[
\sum_{i=1}^{C}\frac{\alpha_{i}\,z_{i}}{\alpha_{i}-\theta}=1-q
\]


The minimum reflux ratio is then:



\[
R_{\min}=\sum_{i=1}^{C}\frac{\alpha_{i}\,x_{D,i}}{\alpha_{i}-\theta}-1
\]


When distributed non-keys are present, multiple $\theta$ roots exist (one between each pair of adjacent component $\alpha$ values) and the resulting system of equations is solved by matrix inversion. If the specified reflux ratio is lower than $R_{\min}$ , the calculation raises an error.

###### Step 5 — Actual Number of Stages (Gilliland Correlation) {#step-5-actual-number-of-stages-gilliland-correlation .unnumbered}

The actual number of ideal stages is obtained from the Gilliland correlation:



\[
X=\frac{R-R_{\min}}{R+1}
\]




\[
Y=0.75\left(1-X^{0.5668}\right)
\]




\[
N=\frac{Y+N_{\min}}{1-Y}
\]


where $R$ is the specified operating reflux ratio.

###### Step 6 — Internal Flow Rates {#step-6-internal-flow-rates .unnumbered}

The molar flow rates in each column section are:



\[
\begin{align}
L &= R \cdot D & &\text{(rectifying liquid)} \\
V &= D + L & &\text{(rectifying vapor)} \\
L' &= L + q \cdot F & &\text{(stripping liquid)} \\
V' &= L' - B & &\text{(stripping vapor)}
\end{align}
\]


where $D$ is the distillate molar flow, $B$ is the bottoms molar flow, and $F$ is the feed molar flow.

###### Step 7 — Optimal Feed Stage (Kirkbride/Fenske) {#step-7-optimal-feed-stage-kirkbridefenske .unnumbered}

The optimal feed stage location from the top is estimated as:



\[
N_{F}=\frac{N_{\min,S}}{N_{\min}}\cdot N
\]


where $N_{\min,S}$ is the Fenske minimum stages for the stripping section alone, calculated using the stripping-section relative volatility and the bottoms key component compositions.

###### Step 8 — Product Temperatures and Enthalpies {#step-8-product-temperatures-and-enthalpies .unnumbered}

The distillate temperature is computed by flashing the distillate composition at the condenser pressure to a vapor fraction of 0 (total condenser) or 1 (partial condenser).

The bottoms temperature is computed by flashing the bottoms composition at the reboiler pressure to a vapor fraction of 0 (bubble point).

###### Step 9 — Heat Duties {#step-9-heat-duties .unnumbered}

**Condenser duty** (negative, heat removal):

For a total condenser:



\[
Q_{C}=-\left(h_{L}-h_{V}^{\text{sat}}\right)\left(L+D\right)
\]


For a partial condenser:



\[
Q_{C}=-\left(h_{L}-h_{D}\right)L
\]


where $h_{L}$ is the saturated liquid enthalpy at the condenser pressure and $h_{V}^{\text{sat}}$ (or $h_{D}$ ) is the saturated vapor enthalpy.

**Reboiler duty** (positive, heat input) — from the overall column energy balance:



\[
Q_{R}=D\,h_{D}+B\,h_{B}-F\,h_{F}-Q_{C}
\]


All enthalpies are on a molar basis.

#### Rigorous Distillation Column

##### Overview

The Rigorous Distillation Column is a unit operation that models the separation of a multicomponent mixture into two or more product streams by exploiting differences in component volatilities across a series of equilibrium stages. Unlike the Shortcut Column, which relies on approximate correlations (Fenske, Underwood, Gilliland), the rigorous model solves the full set of MESH equations — Material balances, Equilibrium relations, Summation constraints, and Heat (enthalpy) balances — simultaneously for every stage.

In DWSIM, the Distillation Column supports multiple feed streams at arbitrary stages, liquid and vapor side draws, inter-stage heat exchangers, and pump-arounds. The column is bounded by a condenser at the top and a reboiler at the bottom. It can also be configured as a Reboiled Absorber (no condenser) or a Refluxed Absorber (no reboiler) to model stripping and enriching sections independently.

##### Column Structure

A rigorous distillation column consists of $N$ equilibrium stages numbered from top to bottom. Stage 1 is the condenser and stage $N$ is the reboiler. Each stage $j$ receives liquid from the stage above ( $L_{j-1}$ ), vapor from the stage below ( $V_{j+1}$ ), and optionally a feed stream ( $F_{j}$ ). Liquid and vapor side draws ( $U_{j}$ and $W_{j}$ , respectively) and a stage heat duty ( $Q_{j}$ ) can also be specified.

##### MESH Equations

For each stage $j$ and each component $i$ , the rigorous solver satisfies the following system of equations:

- **Material balance (M):**



\[
M_{i,j}=L_{j-1}\,x_{i,j-1}+V_{j+1}\,y_{i,j+1}+F_{j}\,z_{i,j}-(L_{j}+U_{j})\,x_{i,j}-(V_{j}+W_{j})\,y_{i,j}=0
\]


where $x_{i,j}$ and $y_{i,j}$ are the liquid and vapor mole fractions of component $i$ on stage \$j\$, $z_{i,j}$ is the feed composition, $U_{j}$ is the liquid side-draw flow, and $W_{j}$ is the vapor side-draw flow.

- **Equilibrium relation (E):**



\[
E_{i,j}=y_{i,j}-\eta_{j}\,K_{i,j}\,x_{i,j}-(1-\eta_{j})\,y_{i,j}^{*}=0
\]


where $K_{i,j}$ is the vapor-liquid equilibrium ratio evaluated by the selected thermodynamic property package and $\eta_{j}$ is the Murphree stage efficiency (default: 1.0 for an ideal stage).

- **Summation constraints (S):**



\[
S_{j}^{L}=\sum_{i=1}^{C}x_{i,j}-1=0\qquad\qquad S_{j}^{V}=\sum_{i=1}^{C}y_{i,j}-1=0
\]


- **Energy balance (H):**



\[
H_{j}=L_{j-1}\,h_{j-1}^{L}+V_{j+1}\,h_{j+1}^{V}+F_{j}\,h_{j}^{F}-(L_{j}+U_{j})\,h_{j}^{L}-(V_{j}+W_{j})\,h_{j}^{V}-Q_{j}=0
\]


where $h^{L}$ , $h^{V}$ , and $h^{F}$ are the liquid, vapor, and feed molar enthalpies.

##### Condenser Types

The column condenser can be configured in one of three modes:

| **Type** | **Description** |
|:---|:---|
| Total Condenser | All overhead vapor is condensed to liquid. The distillate |
|  | is withdrawn as a subcooled or saturated liquid. An optional |
|  | subcooling $\Delta T$ can be specified. |
| Partial Condenser | Only part of the overhead vapor is condensed. The distillate |
|  | is withdrawn as a vapor in equilibrium with the reflux liquid. |
| Full Reflux | All condensed liquid is returned to the column as reflux. |
|  | No distillate product is withdrawn. |

Condenser Types

Two independent specifications are required to fully define the column — one associated with the condenser and one with the reboiler. The available specification types are:

| **Specification Type** | **Description** |
|:---|:---|
| Heat Duty | Condenser or reboiler duty ($Q$, in W) |
| Product Molar Flow Rate | Total distillate or bottoms molar flow (mol/s) |
| Product Mass Flow Rate | Total distillate or bottoms mass flow (kg/s) |
| Component Molar Flow Rate | Molar flow of a specific component in the product (mol/s) |
| Component Mass Flow Rate | Mass flow of a specific component in the product (kg/s) |
| Component Fraction | Mole or mass fraction of a component in the product |
| Component Recovery | Fraction of a feed component recovered in the product (%) |
| Stream Ratio | Reflux ratio ($L/D$) or boilup ratio ($V/B$) |
| Temperature | Stage temperature (K) |
| Feed Recovery | Overall recovery of feed in the product (%) |

Column Specification Types

##### Solving Methods

DWSIM provides four built-in solving methods for the rigorous column, each with different strengths:

| **Method** | **Description** |
|:---|:---|
| Wang-Henke (Bubble Point) | Classic tri-diagonal matrix method. Uses stage temperatures $T_j$ and vapor flows $V_j$ as tear variables. Solves the linearized material balance with the Thomas algorithm and updates temperatures via bubble-point calculations. Best suited for narrow-boiling systems where temperature is a strong function of composition. |
| Modified Wang-Henke | Enhanced variant of the bubble-point method with improved convergence behavior for difficult or wide-boiling systems. |
| Naphtali-Sandholm (Simultaneous Correction) | Solves all MESH equations simultaneously using a Newton-Raphson method with a full Jacobian matrix. More robust for highly non-ideal systems, columns with multiple feeds and side draws, and problems where the bubble-point method fails to converge. |
| Burningham-Otto (Sum-Rates) | Updates liquid flows from the summation of component material balances rather than from energy balances. Designed for absorbers and strippers where the temperature profile is relatively flat and the bubble-point method is ill-conditioned. |

Available Solving Methods

External (third-party) solvers can also be registered through the plug-in interface.

##### Initialization Strategies

Good initial estimates are critical for convergence of rigorous column calculations. DWSIM offers four initialization schemes:

- **Direct:** Uses the raw problem specification without modification.

- **Ideal K-values:** Initializes equilibrium ratios from Raoult’s law ( $K_{i}=P_{i}^{\text{sat}}/P$ ), providing a simple starting point for the composition profile.

- **Ideal Enthalpies:** Initializes stage enthalpies from ideal mixing rules.

- **Ideal K-values and Enthalpies:** Combines both idealizations for the broadest initial simplification.

The user can also supply manual initial estimates for stage temperatures, vapor and liquid flows, and compositions. If the option **Auto-Update Initial Estimates** is enabled, the converged solution from the previous run is stored and reused as the starting point for subsequent calculations.

##### Convergence Parameters

| **Parameter** | **Description** | **Default** |
|:---|:---|:--:|
| Maximum Iterations | Upper limit on solver iterations | 100 |
| Internal Loop Tolerance | Convergence criterion for inner loop | $1 \times 10^{-5}$ |
| External Loop Tolerance | Convergence criterion for outer loop | $1 \times 10^{-5}$ |
| Broyden Acceleration | Use Broyden’s method to accelerate | Enabled |
|  | successive substitution updates |  |

Convergence Settings

A detailed convergence report can optionally be generated, recording the error norm at each iteration.

##### Stage Properties

Each equilibrium stage has the following configurable properties:

| **Property**               | **Description**                    | **Default** |
|:---------------------------|:-----------------------------------|:-----------:|
| Pressure ($P_j$)         | Stage operating pressure (Pa)      |      —      |
| Efficiency ($\eta_j$)    | Murphree tray efficiency (0–1)     |     1.0     |
| Heat Duty ($Q_j$)        | External heat added or removed (W) |      0      |
| Liquid Side Draw ($U_j$) | Liquid withdrawal rate (mol/s)     |      0      |
| Vapor Side Draw ($W_j$)  | Vapor withdrawal rate (mol/s)      |      0      |

Stage Properties

Additional tray hydraulic parameters are available for detailed design: dry tray pressure drop coefficient, total hole area, downcomer length, downcomer height, and liquid flow equation coefficients.

##### Connections

| **Port** | **Direction** | **Type** | **Description** |
|:---|:---|:---|:---|
| Feed(s) | Inlet | Material | One or more feeds at any stage |
| Distillate | Outlet | Material | Top liquid product (total condenser) |
|  |  |  | or top vapor product (partial condenser) |
| Bottoms | Outlet | Material | Bottom liquid product |
| Overhead Vapor | Outlet | Material | Vapor from partial condenser (if applicable) |
| Liquid Side Draw(s) | Outlet | Material | Liquid withdrawn from intermediate stages |
| Vapor Side Draw(s) | Outlet | Material | Vapor withdrawn from intermediate stages |
| Condenser Duty | Outlet | Energy | Heat removed by the condenser |
| Reboiler Duty | Inlet | Energy | Heat supplied to the reboiler |
| Inter-Exchangers | In/Out | Energy | Stage-level heating or cooling duties |

Distillation Column Connections

##### Physical Dimensions

The column estimates its physical dimensions from the converged solution:

- **Diameter:** Estimated from the maximum vapor and liquid traffic using flooding correlations.

- **Height:** Calculated as $H=N_{s}\cdot\Delta h+h_{\text{top}}+h_{\text{bottom}}$ , where $\Delta h$ is the tray spacing (default 0.5 m), $h_{\text{top}}$ is the top clearance (default 0.1 m), and $h_{\text{bottom}}$ is the bottom sump height (default 0.5 m).

##### Dynamic Mode

The Distillation Column supports dynamic (time-dependent) simulation. In dynamic mode, the solver tracks the liquid and vapor holdup on each stage over time using per-stage accumulation streams. Three parameters control the maximum allowable change per time step:

| **Parameter**     | **Description**                     | **Default** |
|:------------------|:------------------------------------|:-----------:|
| Max. P change (%) | Maximum pressure change per step    |     10%     |
| Max. L change (%) | Maximum liquid flow change per step |     10%     |
| Max. V change (%) | Maximum vapor flow change per step  |     10%     |

Dynamic Mode Parameters

The column can be initialized from its steady-state solution before starting a dynamic run.

#### Rigorous Absorption Column

##### Overview

The Absorption Column models gas-liquid absorption and liquid-liquid extraction operations using a rigorous stage-by-stage approach. It shares the same mathematical framework and base class as the Distillation Column but operates without a condenser or reboiler by default. The overhead product exits as a vapor (in absorption mode) and the bottoms product exits as a liquid, with no reflux or boilup generated internally — separation is driven entirely by the contact between the feed gas and the solvent.

##### Operating Modes

The Absorption Column supports two operating modes:

| **Mode** | **Description** |
|:---|:---|
| Absorber | Gas-liquid absorption. A gas feed enters near the bottom and a lean solvent enters near the top. Soluble components transfer from the gas phase into the liquid phase as the two streams flow countercurrently through the stages. The overhead product is the scrubbed gas (vapor) and the bottoms product is the rich solvent (liquid). |
| Extractor | Liquid-liquid extraction. Two immiscible liquid feeds contact each other countercurrently. Components transfer between the two liquid phases based on their partition coefficients. The solver uses liquid-liquid equilibrium (LLE) calculations and requires trial compositions for both liquid phases to initialize. |

Absorption Column Operating Modes

##### Differences from the Distillation Column

| **Feature** | **Distillation Column** | **Absorption Column** |
|:---|:---|:---|
| Condenser | Yes (Total, Partial, or Full Reflux) | None |
| Reboiler | Yes | None |
| Default solver | Wang-Henke (Bubble Point) | Burningham-Otto (Sum-Rates) |
| Top product | Distillate (liquid or vapor) | Overhead vapor (or liquid |
|  |  | in extractor mode) |
| LLE extraction | Not supported | Supported |
| Side draws | Supported | Supported |
| Dynamic mode | Supported | Supported |

Comparison: Distillation Column vs. Absorption Column

Two variant configurations bridge the gap between the two column types:

- **Reboiled Absorber:** An absorption column with a reboiler at the bottom stage, used for stripping operations where additional vapor generation is needed to strip dissolved components from the rich solvent.

- **Refluxed Absorber:** An absorption column with a condenser at the top stage, used when partial condensation of the overhead vapor is needed to improve separation.

##### Solving Methods

The Absorption Column uses the following solving methods:

- **Burningham-Otto (Sum-Rates)** — the default method. It updates liquid flows from the summation of component material balances, which is well-suited for absorbers and strippers where the temperature profile is nearly flat and the energy balance has a weak influence on the solution. If convergence difficulties arise, the solver automatically retries with relaxation of temperature and composition updates.

- **Naphtali-Sandholm (Simultaneous Correction)** — available as an alternative for more strongly coupled systems.

For the Extractor mode, the solver requires multiple sets of trial compositions for the two liquid phases. It iterates through each set of trial estimates until one leads to convergence; if all trials fail, the calculation raises an error.

##### Connections

| **Port** | **Direction** | **Type** | **Description** |
|:---|:---|:---|:---|
| Feed(s) | Inlet | Material | Gas and solvent feeds at any stage |
| Overhead Product | Outlet | Material | Scrubbed gas (vapor) or extract (liquid) |
| Bottoms Product | Outlet | Material | Rich solvent (liquid) or raffinate |
| Liquid Side Draw(s) | Outlet | Material | Liquid from intermediate stages |
| Vapor Side Draw(s) | Outlet | Material | Vapor from intermediate stages |

Absorption Column Connections

##### Convergence Parameters

The Absorption Column uses the same convergence settings as the Distillation Column:

| **Parameter** | **Description** | **Default** |
|:---|:---|:--:|
| Maximum Iterations | Upper limit on solver iterations | 100 |
| Convergence Tolerance | Error norm threshold for convergence | $1 \times 10^{-5}$ |
| Broyden Acceleration | Accelerate successive substitution | Enabled |
| Generate Report | Output detailed convergence report | Disabled |

Absorption Column Convergence Settings

##### Stage Properties

Each stage in the Absorption Column has the same configurable properties as in the Distillation Column — pressure, Murphree efficiency, heat duty, and liquid/vapor side-draw rates. Tray hydraulic parameters (hole area, downcomer geometry, dry tray pressure drop coefficient) are also available for detailed design.

##### Dynamic Mode

The Absorption Column supports dynamic simulation with the same per-stage accumulation tracking and maximum change constraints (pressure, liquid flow, vapor flow) as the Distillation Column. A bottoms accumulation stream tracks the liquid holdup at the column sump. The column can be initialized from its steady-state solution before starting a transient run.

##### Usage Notes

For gas absorption, connect the gas feed to a stage near the bottom and the lean solvent to a stage near the top so that the two phases flow countercurrently.

- For liquid-liquid extraction, provide trial compositions for both liquid phases in the initial estimates to help the LLE solver converge.

- If the Burningham-Otto solver fails to converge, try the Naphtali-Sandholm method or adjust the stage pressures and initial temperature profile.

- For stripping applications requiring a heat source at the bottom, use the **Reboiled Absorber** variant. For applications requiring partial condensation at the top, use the **Refluxed Absorber** variant.

- The stage pressure profile should be specified consistently — DWSIM does not compute hydraulic pressure drops between stages automatically; the user must set the pressure on each stage or specify an overall column pressure drop.

#### Heat Exchanger

The Heat Exchanger models a two-stream, countercurrent heat exchange device. It supports phase change (boiling, condensation) and multiphase flow on either side, evaluating enthalpy changes from the selected property package to correctly account for latent heat effects.

###### *Input Parameters* {#input-parameters-6 .unnumbered}

The heat exchanger in DWSIM has seven calculation modes:

1.  Calculate hot fluid outlet temperature: you must provide the cold fluid outlet temperature and the exchange area to calculate the hot fluid temperature.

2.  Calculate cold fluid outlet temperature: in this mode, DWSIM needs the hot fluid outlet temperature and the exchange area to calculate the cold fluid temperature.

3.  Calculate both temperatures: in this mode, DWSIM needs the exchange area and the heat exchanged to calculate both temperatures.

4.  Calculate area: in this mode you must provide the HTC and both temperatures to calculate the exchange area.

5.  Rate a Shell and Tube exchanger: in this mode you must provide the exchanger geometry and DWSIM will calculate output temperatures, pressure drop on the shell and tubes, overall HTC, LMTD, and exchange area.

6.  Pinch-Point (minimum temperature difference between outlet streams)

7.  Specify Outlet Vapor Fraction (Stream 1 or Stream 2)

###### *Calculation Mode* {#calculation-mode .unnumbered}

All calculation modes are based on the fundamental heat-transfer rate equation:



\[
Q=UA\Delta T_{ml},
\]


\
where: $Q$ = heat transferred, $A$ = heat transfer area (external surface) and $\Delta T_{ml}$ = Logarithmic Mean Temperature Difference (LMTD). The energy balance for each stream is:



\[
Q=\dot{m}\,\Delta H,
\]


\
where $\dot{m}$ is the mass flow rate and $\Delta H$ is the specific enthalpy change between outlet and inlet.

Depending on the selected mode, the unknowns are resolved as follows:

1.  Calculate hot fluid outlet temperature: HTC (Heat Transfer Coefficient), hot fluid outlet temperature, heat load and LMTD.

2.  Calculate cold fluid outlet temperature: HTC, cold fluid outlet temperature, heat load and LMTD.

3.  Calculate both temperatures: HTC, cold and hot fluid outlet temperatures and LMTD.

4.  Calculate area: exchange area and LMTD.

5.  Rate Shell and Tube exchanger: exchanger geometry information.

###### ***Results*** {#results-1 .unnumbered}

The output quantities computed by the heat exchanger depend on the selected mode:

1.  Calculate hot fluid outlet temperature: overall HTC, hot fluid outlet temperature, heat load and LMTD.

2.  Calculate cold fluid outlet temperature: overall HTC, cold fluid outlet temperature, heat load and LMTD.

3.  Calculate both temperatures: overall HTC, cold and hot fluid outlet temperatures and LMTD.

4.  Calculate area: exchange area and LMTD.

5.  Rate Shell and Tube exchanger: area, LMTD, LMTD correction factor (F), overall HTC, hot fluid outlet temperature, cold fluid outlet temperature, hot fluid pressure drop (shell/tubes only), cold fluid pressure drop (shell/tubes only).

##### Description of calculation methods

###### Shell and Tube

The calculation method for Shell and Tube Design and Rating is based on the method of Tinker ().

####### Fundamental Equations

The heat transfer $Q$ between the hot and cold fluids in a shell and tube heat exchanger can be written:



\[
Q=m_{t}C_{pt}(T_{t1}-T_{t2})
\]




\[
Q=m_{s}C_{ps}(T_{s1}-T_{s2})
\]




\[
Q=h_{i}A_{ti}(T_{t}-T_{ti})
\]




\[
Q=\frac{2k_{t}\pi nL}{\ln\frac{d_{e}}{d_{i}}}(T_{ti}-T_{te})
\]




\[
Q=h_{e}A_{te}(T_{te}-T_{s})
\]




\[
Q=UA_{te}\Delta T_{m}
\]


Heat losses for the environment are not considered.

Symbols:







$m_{t}$ tube-side flow rate

$C_{pt}$ tube-side fluid average heat capacity

$T_{t1}$ tube-side inlet fluid temperature

$T_{t2}$ tube-side outlet fluid temperature

$m_{s}$ shell-side flow rate

$C_{ps}$ shell-side fluid average heat capacity

$T_{s1}$ shell-side inlet fluid temperature

$T_{s2}$ shell-side outlet fluid temperature

$h_{i}$ average film coefficient at the tube inner wall

$A_{ti}$ internal tube surface heat exchange area

$T_{t}$ average tube fluid temperature

$T_{ti}$ average internal tube surface temperature

$k_{t}$ tube thermal conductivity

$L$ tube total length

$n$ total number of tubes in the exchanger

$d_{i}$ tube internal diameter

$d_{e}$ tube external diameter

$T_{te}$ average external tube surface temperature

$h_{e}$ average film coefficient at the tube outer wall

$A_{te}$ external tube surface heat exchange area

$T_{s}$ shell-side fluid average temperature

$U$ overall heat exchange coefficient

$\triangle T_{m}$ mean temperature difference



The mass flux $G_{t}$ for the tube-side flow can be written:



\[
G_{t}=\rho_{t}V_{t}=\frac{m_{t}}{\frac{n}{N_{t}}S_{ti}}
\]


where:







$\rho_{t}$ tube-side fluid density

$V_{t}$ tube-side flow velocity

$N_{t}$ number of tube passes

$S_{ti}$ tube internal cross-flow section area:


\[
S_{ti}=\frac{\pi d_{i}^{2}}{4}
\]



The mass flux $G_{s}$ for the shell-side flow can be written:



\[
G_{s}=\frac{m_{b}}{S_{s}}
\]


where:







$m_{b}$ fraction of total flow that crosses the tube bundle

$S_{s}$ cross-flow section area through the tube bundle:





\[
S_{s}=C_{a}lD_{f}
\]


where:







$l$ distance between two adjacent baffles

$D_{f}$ bundle diameter





\[
C_{a}=C_{b}\frac{s-d_{e}}{s}
\]


where $s$ is the tube pass and







$C_{b}=0.97$ for ▷ and □

$C_{b}=1.37$ for ◇



To determine the film coefficient for the shell-side flow, we have:



\[
G_{sh}=\frac{m_{s}}{S_{sh}}
\]




\[
S_{sh}=\frac{S_{s}M}{F_{h}}
\]




\[
F_{h}=\frac{1}{1+N_{h}\sqrt{\frac{D_{i}}{s}}}
\]








$S_{sh}$ effective cross-section area for heat exchange

$F_{h}$ fraction of total flow that crosses $S_{s}$



$M$ and $N_{h}$ are correction factors obtained from the tables in Figures [47](#fig:Shell-side-heat-transfer), [48](#fig:Shell-side-pressure-drop), [49](#fig:Shell-side-heat-transfer-1), [50](#fig:Shell-side-pressure-drop-1), [51](#fig:Shell-side-heat-transfer-2) and [52](#fig:Shell-side-pressure-drop-2).



<a id="fig:Shell-side-heat-transfer"></a>
![<span id="fig:Shell-side-heat-transfer" data-label="fig:Shell-side-heat-transfer"></span>Shell-side heat transfer characteristic for triangle layout.](images/screens90/tinker1.png)

*<span id="fig:Shell-side-heat-transfer" data-label="fig:Shell-side-heat-transfer"></span>Shell-side heat transfer characteristic for triangle layout.*





<a id="fig:Shell-side-pressure-drop"></a>
![<span id="fig:Shell-side-pressure-drop" data-label="fig:Shell-side-pressure-drop"></span>Shell-side pressure drop characteristic for triangle layout.](images/screens90/tinker2.png)

*<span id="fig:Shell-side-pressure-drop" data-label="fig:Shell-side-pressure-drop"></span>Shell-side pressure drop characteristic for triangle layout.*





<a id="fig:Shell-side-heat-transfer-1"></a>
![<span id="fig:Shell-side-heat-transfer-1" data-label="fig:Shell-side-heat-transfer-1"></span>Shell-side heat transfer characteristic for square layout.](images/screens90/tinker3.png)

*<span id="fig:Shell-side-heat-transfer-1" data-label="fig:Shell-side-heat-transfer-1"></span>Shell-side heat transfer characteristic for square layout.*





<a id="fig:Shell-side-pressure-drop-1"></a>
![<span id="fig:Shell-side-pressure-drop-1" data-label="fig:Shell-side-pressure-drop-1"></span>Shell-side pressure drop characteristic for square layout.](images/screens90/tinker4.png)

*<span id="fig:Shell-side-pressure-drop-1" data-label="fig:Shell-side-pressure-drop-1"></span>Shell-side pressure drop characteristic for square layout.*





<a id="fig:Shell-side-heat-transfer-2"></a>
![<span id="fig:Shell-side-heat-transfer-2" data-label="fig:Shell-side-heat-transfer-2"></span>Shell-side heat transfer characteristic for rotated square layout.](images/screens90/tinker5.png)

*<span id="fig:Shell-side-heat-transfer-2" data-label="fig:Shell-side-heat-transfer-2"></span>Shell-side heat transfer characteristic for rotated square layout.*





<a id="fig:Shell-side-pressure-drop-2"></a>
![<span id="fig:Shell-side-pressure-drop-2" data-label="fig:Shell-side-pressure-drop-2"></span>Shell-side pressure drop characteristic for rotated square.](images/screens90/tinker6.png)

*<span id="fig:Shell-side-pressure-drop-2" data-label="fig:Shell-side-pressure-drop-2"></span>Shell-side pressure drop characteristic for rotated square.*



####### Overall Heat Transfer Coefficient

The Overall HTC $U$ is given by the expression



\[
U=\frac{1}{\frac{d_{e}}{h_{i}d_{i}}+\frac{R_{di}d_{e}}{d_{i}}+\frac{d_{e}}{2k_{t}}\ln\frac{d_{e}}{d_{i}}+R_{de}+\frac{1}{h_{e}}}
\]


where







$R_{di}$ resistance due to deposits in the internal tube surface

$R_{de}$ resistance due to deposits in the external tube surface



####### Mean Temperature Difference

The basic equation for heat transfer to be used in heat exchanger design is



\[
U=\intop U\Delta TdA
\]


Fluid temperatures are not constant, changing at each point as heat is transferred from the hot fluid to the cold fluid, resulting in a variation in the temperature differences between the fluids through the exchanger.

Associated to the temperature variations are the fluid and materials’ thermal properties, which implies in variations in the thermal resistances, and thus in the overall coefficient $U$ .

When designing an exchanger, though, we usually calculate an average value for $U$ , and the properties of each fluid are evaluated at the arithmetic mean of the end temperatures, and the result value is assumed to be constant. Thus, we can write:



\[
Q=UA\Delta T_{m}
\]


where



\[
\Delta T_{m}=\frac{1}{A}\intop_{0}^{A}\Delta TdA
\]


By knowing how $\Delta T$ changes inside the exchanger, the above expression can be integrated, resulting in something like



\[
\Delta T_{m}=F\times LMTD
\]


where $LMTD$ is the log mean temperature difference for the exchanger conditions, calculated as if the exchanger is countercurrent, with only one shell pass and one tube pass.

$F$ is a correction factor which is given in formulas and charts such as in Figure [53](#fig:Shell-side-heat-transfer-3), where P and R are given by



\[
P=\frac{T_{t2}-T_{t1}}{T_{s1}-T_{t1}}
\]




\[
R=\frac{T_{s1}-T_{s2}}{T_{t2}-T_{t1}}
\]




<a id="fig:Shell-side-heat-transfer-3"></a>
![<span id="fig:Shell-side-heat-transfer-3" data-label="fig:Shell-side-heat-transfer-3"></span>Correction factor for LMTD.](images/screens90/tinker7.png)

*<span id="fig:Shell-side-heat-transfer-3" data-label="fig:Shell-side-heat-transfer-3"></span>Correction factor for LMTD.*



####### Film Coefficient

The film coefficient $h_{e}$ is obtained from the charts in Figures [47](#fig:Shell-side-heat-transfer), [49](#fig:Shell-side-heat-transfer-1) and [51](#fig:Shell-side-heat-transfer-2) as a function of the Reynolds number $Re_{h}$ and the $\nicefrac{s}{d_{e}}$ :



\[
Re_{h}=\frac{G_{sh}d_{e}}{\mu_{s}}
\]


####### Tube-side Pressure Drop



\[
P_{1}-P_{2}=f_{D}\frac{L}{d_{i}}\frac{\rho_{t}V_{t}^{2}}{2}
\]




\[
\frac{1}{\sqrt{f_{d}}}=-2\log\left(\frac{\varepsilon/d_{i}}{3.7}+\frac{2.51}{Re\sqrt{f_{D}}}\right)
\]


Initial value for $f_{D}$ :



\[
f_{D0}=0.25\left[\log\left(\frac{\varepsilon/d_{i}}{3.7}+\frac{5.74}{\sqrt{Re}}\right)\right]^{-2}
\]


Viscosity correction:



\[
f_{Dc}=f_{D}\left(\frac{\mu_{ti}}{\mu_{t}}\right)^{0.14}
\]


####### Shell-side Pressure Drop



\[
\Delta P_{s}=4f_{s}\frac{G_{sf}^{2}}{2\rho_{c}}C_{x}\left(1-\frac{H}{D_{i}}\right)\frac{D_{i}}{s}N'_{B}\left(1+\frac{Ys}{D_{i}}\right)\left(\frac{\mu_{te}}{\mu_{s}}\right)^{0.14}
\]








$C_{x}=1.154$ for ▷ arrangement

$C_{x}=1.0$ for □ arrangement

$C_{x}=1.414$ for ◇ arrangement



$\left(1+\frac{Ys}{D_{i}}\right)$ is obtained from the charts in Figures [47](#fig:Shell-side-heat-transfer), [49](#fig:Shell-side-heat-transfer-1) and [51](#fig:Shell-side-heat-transfer-2). $N'_{B}$ is the number of spaces between baffles and is given by $N'_{B}=N_{B}+1$ where $N_{B}$ is the number of baffles.

#### Air Cooler




![Air Cooler model](images/screens80/aircooler.png)

*Air Cooler model*



The Air Cooler models a fin-fan heat exchanger in which the process fluid flows through tube bundles and ambient air is the cooling medium on the shell (external) side. The calculation is based on a simplified variant of the shell-and-tube heat-transfer model.

###### *Input Parameters* {#input-parameters-7 .unnumbered}

The Air Cooler model in DWSIM has three calculation modes:

- Specify Outlet Temperature: you must provide the fluid outlet temperature and DWSIM will calculate Overall UA and Heat Exchanged.

- Specify Tube Geometry: in this mode you must provide the tube geometry and DWSIM will calculate output temperatures, pressure drop at the tubes, overall HTC (U), LMTD, and exchange area. This calculation mode uses a simplified version of Tinker’s method for Shell and Tube exchanger calculations, with a modification for the outside heat transfer coefficient (convection).

- Specify Overall UA: in this mode you must provide the Overall UA and DWSIM will calculate the Heat Exchanged and Outlet Temperature.

You can provide the pressure drop for the hot fluid in the exchanger for modes 1 and 3 only.

###### *Calculation Mode* {#calculation-mode-1 .unnumbered}

The Air Cooler is calculated using the fundamental heat-transfer rate equation:



\[
Q=UA\Delta T_{ml},
\]


\
where: $Q$ = heat transferred, $A$ = heat transfer area (external surface) and $\Delta T_{ml}$ = Logarithmic Mean Temperature Difference (LMTD). The energy balance for each stream is:



\[
Q=\dot{m}\,\Delta H,
\]


\
where $\dot{m}$ is the mass flow rate and $\Delta H$ is the specific enthalpy change between outlet and inlet.

#### Component Separator

The Component Separator is an idealized mass-balance unit operation that partitions the feed components between two product streams according to user-specified split fractions or absolute flow rates. No thermodynamic equilibrium is solved for the separation itself; after the component split is applied, the energy balance and phase states of the outlet streams are computed via flash calculations.

###### *Input Parameters* {#input-parameters-8 .unnumbered}

- Specified stream: sets the stream to which the separation specifications will be applied. ”0” corresponds to the Outlet stream 1 (overhead) and ”1” corresponds to the Outlet stream 2 (bottoms).

###### ***Results*** {#results-2 .unnumbered}

- Energy imbalance: Difference between enthalpy of outlet and inlet streams. in some cases it can be interpreted as the energy necessary to do the separation.

#### Orifice Plate

This model implements the ISO 5167 standard for thin-plate orifice flow meters. It computes the differential pressure across the orifice as well as the permanent pressure loss. When used in conjunction with the Adjust logical operation, it can be employed to back-calculate stream flow rates from a measured differential pressure.

###### *Input Parameters* {#input-parameters-9 .unnumbered}

- Pressure tappings: select the option which corresponds to the arrangement of the tappings for pressure reading.

- Orifice diameter: inner diameter of the plate.

- Beta (d/D): ratio between plate’s inner and outer diameters.

- Correction factor: multiplier for the mass flow rate used in the calculation of the pressure drop across the orifice. Default is 1.

###### ***Results*** {#results-3 .unnumbered}

- Orifice pressure drop: Pressure drop across the orifice. This is the value that is read through the tappings.

- Overall pressure drop: permanent pressure loss after downstream pressure recovery. This value is always less than or equal to the orifice differential pressure.

- Delta T: temperature drop across the orifice, considering that the process is an adiabatic expansion.

#### Custom Unit Operation

The Custom Unit Operation allows the user to define the calculation logic through a script that is executed inside the Calculate() method called by the flowsheet solver. Up to six material streams are available: three inlets and three outlets.

Supported Languages are IronPython, IronRuby, VBScript and JScript. You can use some predefined reference variables inside your script, defined as shortcuts to the most common objects:







**ims1** Input Material Stream in slot 1 (MaterialStream class instance)

**ims2** Input Material Stream in slot 2 (MaterialStream class instance)

**ims3** Input Material Stream in slot 3 (MaterialStream class instance)

**oms1** Output Material Stream in slot 1 (MaterialStream class instance)

**oms2** Output Material Stream in slot 2 (MaterialStream class instance)

**oms3** Output Material Stream in slot 3 (MaterialStream class instance)

**Me** Reference variable to the Custom UO object instance (CustomUO UnitOperation class)

**Flowsheet** Reference variable to the active flowsheet object (FormChild class)

**Solver** Flowsheet solver class instance, used to send commands to the calculator (COMSolver class)

**Spreadsheet** Reference variable to the Spreadsheet object.



#### Solids Separator

The Solids Separator splits a multiphase feed stream into a liquid product and a solids product, based on user-specified separation efficiencies for each phase.

###### *Input Parameters* {#input-parameters-10 .unnumbered}

- Solids Separation Efficiency: defines the amount of solids in the liquid stream. 100% efficiency means no solids in the liquid stream.

- Liquids Separation Efficiency: defines the amount of liquid in the solids stream. 100% efficiency means no liquid in the solids stream.

###### *Calculation Method* {#calculation-method-6 .unnumbered}

The solids separator performs a component mass balance, distributing the solid and liquid phases of the inlet stream into two distinct product streams according to the specified efficiencies.

#### Continuous Cake Filter

In a continuous filter, the feed, filtrate and cake move at steady constant rates. It is evident that the process consists of several steps in series - cake formation, washing, drying and discharging - and that each step involves progressive and continual change in conditions. The pressure drop across the filter during cake formation is, however, held constant.

###### *Calculation Method* {#calculation-method-7 .unnumbered}

For a continuous cake filter, the equation that relates the filter characteristics with the rate of solids production is



\[
\frac{\dot{m_{c}}}{A_{T}}=\frac{\left[2c\alpha\triangle Pfn/\mu+\left(nR_{m}\right)^{2}\right]^{0,5}-nR_{m}}{\alpha},
\]








where:

$\dot{m_{c}}$ rate of solids production, $kg/s$

$A_{T}$ total filter area, $m^{2}$

$\Delta P$ total pressure drop, $Pa$

$f$ fraction of filter area available for cake formation

$c$ solids concentration in the solids stream

$\alpha$ specific cake resistance, $m/kg$

$R_{m}$ filter medium resistance, $m^{-1}$

$n$ drum speed, $s^{-1}$



###### *Input and Output Parameters* {#input-and-output-parameters .unnumbered}

- Filter Medium Resistance: filter medium flow resistance;

- Specific Cake Resistance: specific cake flow resistance;

- Cycle Time: filter cycle time.

- Cake Relative Humidity: filter cake moisture in % wet basis;

- Filter Calculation Mode: Design or Simulation. If ***Design*** is selected, DWSIM will calculate the filter area given the total pressure drop. If ***Simulation*** is selected, it will do the opposite;

- Total Filter Area: filter area measured perpendicularly to the direction of flow;

- Pressure Drop: total pressure drop across the filter (cake + medium).

#### Excel Unit Operation

The Excel (Spreadsheet) Unit Operation allows user-defined calculation models to be implemented in an external Microsoft Excel workbook.

Each instance of this unit operation is associated with a separate Excel file that follows a predefined template. At each solver iteration, DWSIM writes the inlet stream data and user-defined parameters to the workbook, triggers the spreadsheet calculation, and reads the results back to populate the outlet streams. User-defined parameters can be transferred in both directions. Up to four material streams may be connected to both the inlet and outlet sides. An energy stream receives the calculated energy from the overall enthalpy balance.

###### Calculation Parameters

Calculation parameters are defined inside the Excel definition file as input parameters.

**Editor:** displays the Excel file associated with this unit operation. Click the file name to open the unit editor. After the spreadsheet model is evaluated, DWSIM computes the overall enthalpy balance between all inlet and outlet streams to determine the net heat added or removed. Additional output parameters defined in the workbook are also read back into DWSIM.

**Search Button:** to search Excel file to be associated with Unit Operation

**Create New Button:** creates a new definition file from the built-in template. The file can then be opened and edited directly in Excel; changes are saved from within Excel.

**Excel definition file:** the Excel definition file has a fixed structure that must not be altered. Modifying the layout will prevent DWSIM from correctly reading and writing data.

###### Input Tab

DWSIM writes the properties of all connected inlet streams into the blue area. From line 12 downward all components and their molar flows are listed. The red area contains the parameters which are required for calculation. This parameters are displayed in DWSIM as "Calculation Parameters" inside the property tab of ExcelUO. You may list as many properties as you want here. DWSIM starts to read the list below the heading downwards until it finds an empty cell. Each parameter features a name, a value, a unit and an annotation.

###### Output Tab

The output tab has the same structure as the input tab. Molare flows of each component of every output stream are to be written into their fields by the user defined calculation procedures. You also have to calculate temperature and pressure of all streams leaving the unit. The enthalpy of each stream is calculated by DWSIM automatically after finishing Excel calculations.

###### Results

DWSIM is calculating the enthalpy balance of the unit from enthalpy of outlet streams minus enthalpy of inlet streams. The result of this calculation is written to the energy stream. After finishing the calculations in Excel, DWSIM checks the mass balance of that unit. If mass balance is not ok DWSIM will issue an error message.

#### Flowsheet Unit Operation

The Flowsheet Unit Operation allows you to run a XML simulation file as a block inside another flowsheet. This can be useful if you have a large simulation and want to split it in several, smaller blocks which can be run as independent simulations, making it easier to mantain, make modifications and fix errors in the smaller blocks.

Mass transfer between flowsheets is done in a per-compound basis, that is, any compound that doesn’t match in both simulations will have its flow data erased in the inner flowsheet and will be ignored in the outer one. The mass is transferred to and from the inner flowsheet by matching inlet and outlet

Material Streams connected to the Flowsheet UO with streams in the inner flowsheet. Besides mass/mole flow information, temperature, pressure and enthalpy are also written and read to and from the inner flowsheet.

DWSIM uses the Property Package models defined in the inner flowsheet to do the mass and energy balances. Only settings like Parallel CPU and Parallel GPU calculations affect the way that DWSIM does its calculations inside the block, since it will use the parameters defined when the inner flowsheet was last saved.

You can select Parameters/Properties from objects in the inner flowsheet to expose them to the outer flowsheet, allowing usage of these parameters in Optimization and Sensitivity Analysis cases, Script blocks and for displaying in the outer flowsheet as well.

###### Connections

Ten inlet and ten outlet Material Stream ports are available for connecting with Material Streams from the inner flowsheet.

After connecting streams to the ports, you must open the Control Panel to map the connected streams to streams in the inner flowsheet.

###### Calculation Parameters

**Simulation file:** selects the XML simulation file to use as the inner flowsheet.

**Control Panel:** opens the Control Panel to initialize the flowsheet, define stream mapping, expose input and output parameters from the inner flowsheet and define the mass transfer mode.

**Initialize on load:** if true, initializes the inner flowsheet during the opening of the main flowsheet.

**Update process data on saving:** when saving the main simulation file, DWSIM will update the process data from the inner flowsheet in the selected XML file. Only object process data calculated by the solver will be updated. Other settings will remain unchanged.

**Redirect calculator messages:** show calculation details from the inner flowsheet in the main flowsheet’s log window.

**View flowsheet:** shows the inner flowsheet PFD.

###### Linked Input Parameters

This section will display the parameters that you’ve selected in the control panel to use as input parameters in the outer flowsheet. They can be changed anytime and will trigger the calculation of the flowsheet block.

###### Linked Output Parameters

This section will display the parameters that you’ve selected in the control panel to use as output parameters in the outer flowsheet. They are read-only and will be updated only after the flowsheet block is calculated successfully.

###### Results

**Mass balance error:** shows the mass balance error in %. It can be useful to detect orphan streams in the inner flowsheet, that is, streams that work as inlet or outlet streams in the inner flowsheet but aren’t connected to any stream in the main flowsheet, as this may lead to large mass balance errors.

**Control Panel:** Use the Control Panel to initialize the flowsheet. Only after initialization you’ll be able to make the connection and expose parameters from the inner flowsheet. You can also use the "Initialize/Reload" button to reload the simulation file after you’ve done changes in the simulation by opening it in another DWSIM window.

###### Viewing the inner flowsheet

By clicking on the "View Flowsheet" button in the property grid you’ll be able to view the inner flowsheet, check object properties and the overall layout. You can’t change anything here, so any attempt to do so will result in an error.

#### PEM Fuel Cell




![PEM Fuel Cell model](images/screens80/pemfc.png)

*PEM Fuel Cell model*



**Proton-exchange membrane fuel cells (PEMFC)**, also known as polymer electrolyte membrane (PEM) fuel cells, are a type of fuel cell being developed mainly for transport applications, as well as for stationary fuel-cell applications and portable fuel-cell applications. Their distinguishing features include lower temperature/pressure ranges (50 to 100 °C) and a special proton-conducting polymer electrolyte membrane. PEMFCs generate electricity and operate on the opposite principle to PEM electrolysis, which consumes electricity.

The PEM Fuel Cell model in DWSIM is an interface for the **Amphlett Static Model** from the **OPEM Python Library** (<https://www.ecsim.ir/opem/>).

The Amphlett static model has been used to predict the performance of proton exchange membrane fuel cell. Key concepts in Amphlett static model are Nernst voltage, activation polarization loss, ohmic polarization loss and concentration polarization loss. Amphlett static model has a mechanistic and empirical approach to describe the performance of proton exchange membrane fuel cell. The ideal standard potential of an H2/O2 fuel cell is 1.229 V with liquid water product. The actual cell potential is decreased from its reference potential because of irreversible losses.

For more information about the model inputs and outputs, please visit [https://www.ecsim.ir/opem/doc/Static/Amphlett.html](this link).

#### Water Electrolyzer




![Water Electrolyzer model](images/screens80/electrolyzer.png)

*Water Electrolyzer model*



Electrolysis of water, also known as electrochemical water splitting, is the process of using electricity to decompose water into oxygen and hydrogen gas by a process called electrolysis. Hydrogen gas released in this way can be used as hydrogen fuel, or remixed with the oxygen to create oxyhydrogen gas, which is used in welding and other applications.

Electrolysis of water requires a minimum potential difference of 1.23 volts, though at that voltage external heat is required from the environment.

####### Setup and Calculation Guide {#setup-and-calculation-guide .unnumbered}

- The Water Electrolyzer model requires Water, Hydrogen and Oxygen added to the simulation with Liquid Water present in the inlet stream.

- Input Parameters: Total Voltage and Number of Cells.

- Output Parameters: Cell Voltage, Current, Electron Transfer and Waste Heat.

- After the calculation, the generated power is directed to the energy stream, while the waste heat is added to the outlet material stream, increasing its temperature.

#### Hydroelectric Turbine




![Hydroelectric Turbine model](images/screens80/hydroelectricturbine.png)

*Hydroelectric Turbine model*



A Hydroelectric Turbine is a rotary machine that converts kinetic energy and potential energy of water into mechanical work.

Water turbines were developed in the 19th century and were widely used for industrial power prior to electrical grids. Now, they are mostly used for electric power generation. Water turbines are mostly found in dams to generate electric power from water potential energy.

###### Setup and Calculation Guide {#setup-and-calculation-guide-1 .unnumbered}

- The Hydroelectric Turbine model converts head and velocity from the inlet stream into usable energy for the process.

- Input parameters: Static Head, Inlet Velocity, Outlet Velocity and Efficiency.

- Output parameters: Velocity Head, Total Head, Generated Power.

- The generated power is calculated from



\[
P=\eta\,\rho\,g\,h\,q
\]


where:







$P$ generated power (W)

$\eta$ efficiency (0.00-1.00)

$\rho$ fluid density (kg/m3)

$g$ acceleration of gravity (9.81 m/s2)

$h$ total head (m)



- Total head is calculated from



\[
h=h_{s}+h_{v}
\]


where







$h_{s}$ static head (m)

$h_{v}$ velocity head (m), calculated from





\[
h_{v}=\frac{v_{in}^{2}-v_{out}^{2}}{2g}
\]


#### Wind Turbine




![Wind Turbine model](images/screens80/windturbine.png)

*Wind Turbine model*



A wind turbine is a device that converts the kinetic energy of wind into electrical energy. Hundreds of thousands of large turbines, in installations known as wind farms. They are an increasingly important source of intermittent renewable energy, and are used in many countries to lower energy costs and reduce reliance on fossil fuels.

###### Setup and Calculation Guide {#setup-and-calculation-guide-2 .unnumbered}

- The Wind Turbine model converts energy from air (wind) into usable energy for the process.

- Input parameters: Wind Speed, Atmospheric Temperature and Pressure, Relative Humidity, Rotor Diameter, Efficiency and Number of Units.

- Output parameters: Generated Power, Maximum Theoretical Power and Calculated Air Density.

- Atmospheric conditions are used to calculate the density of air.

- Conservation of mass requires that the amount of air entering and exiting a turbine must be equal. Accordingly, Betz’s law gives the maximal achievable extraction of wind power by a wind turbine as 16⁄27 (59.3%) of the rate at which the kinetic energy of the air arrives at the turbine (ref. [T̈he Physics of Wind Turbines Kira Grogg Carleton College, 2005, p. 8̈](http://apps.carleton.edu/campus/library/digitalcommons/assets/pacp_7.pdf)).

- The maximum theoretical power output of a wind machine is thus 16/27 times the rate at which kinetic energy of the air arrives at the effective disk area of the machine. If the effective area of the disk is $A$ , and the wind velocity $v$ , the maximum theoretical power output $P_{max}$ is:



\[
{\displaystyle P_{max}=\frac{16}{27}\frac{1}{2}\rho v^{3}A=\frac{8}{27}\rho v^{3}A},
\]


where $ρ$ is the air density. The actual generated power is given by



\[
{\displaystyle P=}n\,\eta\,P_{max}
\]


#### Solar Panel




![Solar Panel model](images/screens80/solarpanel.png)

*Solar Panel model*



A solar cell panel, solar electric panel, photo-voltaic (PV) module or solar panel is an assembly of photo-voltaic cells mounted in a framework for installation. Solar panels use sunlight as a source of energy to generate direct current electricity. A collection of PV modules is called a PV panel, and a system of PV panels is called an array. Arrays of a photovoltaic system supply solar electricity to electrical equipment.

###### Setup and Calculation Guide {#setup-and-calculation-guide-3 .unnumbered}

- The Solar Panel converts energy from solar irradiation into usable energy for the process.

- Input parameters: Solar Irradiation (kW/m2), Panel Area, Panel Efficiency and Number of Panels.

- Output parameters: Generated Power.

- Generated Power is calculated from



\[
{\displaystyle P=}\eta\,n\,S\,A\,
\]


where







$P$ generated power (kW)

$\eta$ panel efficiency

$n$ number of panels

$S$ solar irradiation (kW/m2)

$A$ panel area (m2)



#### Fired Heater

##### Overview

The Fired Heater (also referred to as a process furnace or direct-fired heater) is a unit operation used extensively in petroleum refining and petrochemical processes. It transfers heat released by the combustion of a fuel — typically refinery fuel gas or natural gas — to a process fluid flowing through tubes inside the furnace.

This DWSIM implementation models a two-zone fired heater consisting of a radiant section, an optional shield (shock) section, and a convection section, topped by a stack (chimney). The model accepts two material streams as input: the process fluid to be heated and the fuel gas stream. It produces two output streams: the heated process fluid and the flue gas exhaust.

##### Supported furnace configurations

The model supports three furnace geometries: vertical cylindrical upfired (cylindrical shell with a vertical tube coil and floor-fired burners), box horizontal (rectangular cabin with horizontal tube passes), and box vertical (rectangular cabin with vertical tube passes along the side walls and floor-fired burners, the most common configuration in petroleum refining).

##### Operating modes

The furnace can be specified in three modes. In outlet temperature mode, the user specifies the desired process outlet temperature and the model calculates the required fuel consumption. In duty mode, the user specifies the total absorbed duty and the model calculates the fuel consumption and process outlet temperature. In fuel flow rate mode, the user specifies the fuel stream flow rate as a material stream and the model calculates the absorbed duty and process outlet temperature.

##### Connections

The unit operation has four stream connections. The process inlet is a material stream carrying the cold process fluid entering the convection section, requiring temperature, pressure, flow rate, and composition. The fuel inlet is a material stream carrying the fuel gas or liquid fuel entering the burners, whose composition is used for stoichiometric combustion calculations. The process outlet is a material stream carrying the hot process fluid exiting the radiant section. The flue gas outlet is a material stream carrying the combustion products exiting the convection section, with composition including $CO_{2}$ , $H_{2}O$ , $N_{2}$ , $O_{2}$ , and $SO_{2}$ .

##### Combustion Model

The combustion model calculates the stoichiometric combustion of the fuel stream based on its molar composition. Each combustible component undergoes complete oxidation.

###### Combustion reactions

For a general hydrocarbon $\text{C}_{n}\text{H}_{m}$ :



\[
\text{C}_{n}\text{H}_{m}+\left(n+\frac{m}{4}\right)\text{O}_{2}\rightarrow n\,\text{CO}_{2}+\frac{m}{2}\,\text{H}_{2}\text{O}
\]


For hydrogen:



\[
\text{H}_{2}+\frac{1}{2}\,\text{O}_{2}\rightarrow\text{H}_{2}\text{O}
\]


For carbon monoxide:



\[
\text{CO}+\frac{1}{2}\,\text{O}_{2}\rightarrow\text{CO}_{2}
\]


For hydrogen sulfide:



\[
\text{H}_{2}\text{S}+\frac{3}{2}\,\text{O}_{2}\rightarrow\text{SO}_{2}+\text{H}_{2}\text{O}
\]


###### Supported fuel components

| **Component** | **Formula** | $\nu_{\text{O}_{2}}$ | $\nu_{\text{CO}_{2}}$ | $\nu_{\text{H}_{2}\text{O}}$ | **LHV \[kJ/mol\]** |
|:---|:--:|:--:|:--:|:--:|:--:|
| Methane | CH$_{4}$ | 2.0 | 1 | 2 | 802.3 |
| Ethane | C$_{2}$H$_{6}$ | 3.5 | 2 | 3 | 1427.8 |
| Propane | C$_{3}$H$_{8}$ | 5.0 | 3 | 4 | 2043.9 |
| n-Butane | C$_{4}$H$_{10}$ | 6.5 | 4 | 5 | 2657.3 |
| n-Pentane | C$_{5}$H$_{12}$ | 8.0 | 5 | 6 | 3272.1 |
| Hydrogen | H$_{2}$ | 0.5 | 0 | 1 | 241.8 |
| Carbon monoxide | CO | 0.5 | 1 | 0 | 283.0 |
| Hydrogen sulfide | H$_{2}$S | 1.5 | 0 | 1 | 518.0 |
| Ethylene | C$_{2}$H$_{4}$ | 3.0 | 2 | 2 | 1323.1 |
| Propylene | C$_{3}$H$_{6}$ | 4.5 | 3 | 3 | 1926.4 |

Stoichiometric coefficients and lower heating values for supported fuel components.

###### Air requirement

The total stoichiometric oxygen requirement is:



\[
\dot{n}_{\text{O}_{2},\text{stoich}}=\sum_{i}\dot{n}_{f,i}\;\nu_{\text{O}_{2},i}
\]


where $\dot{n}_{f,i}$ is the molar flow of fuel component $i$ , $\nu_{\text{O}_{2},i}$ is the stoichiometric coefficient of $\text{O}_{2}$ for that component, and the summation runs over all combustible species in the fuel stream.

The actual air molar flow, accounting for fractional excess air $e_{a}$ , is:



\[
\dot{n}_{\text{air}}=\frac{\dot{n}_{\text{O}_{2},\text{stoich}}}{y_{\text{O}_{2},\text{air}}}\left(1+e_{a}\right)
\]


where $y_{\text{O}_{2},\text{air}}=0.2095$ is the mole fraction of $\text{O}_{2}$ in dry air and $e_{a}$ is the fractional excess air (e.g. $e_{a}=0.15$ for $15\%$ excess). The air composition is assumed as $20.95\%$ $\text{O}_{2}$ , $78.08\%$ $\text{N}_{2}$ , and $0.93\%$ $\text{Ar}$ on a molar basis, with approximately $1\%$ moisture.

###### Heat released

The total heat released by combustion on a lower heating value basis is:



\[
Q_{\text{rel}}=\sum_{i}\dot{n}_{f,i}\cdot\text{LHV}_{i}
\]


where $\text{LHV}_{i}$ is the lower heating value of fuel component $i$ in $\text{J}/\text{mol}$ .

###### Adiabatic flame temperature

The adiabatic flame temperature $T_{\text{flame}}$ is obtained by solving the enthalpy balance iteratively:



\[
Q_{\text{rel}}+\dot{n}_{\text{air}}\int_{T_{\text{ref}}}^{T_{\text{air}}}C_{p,\text{air}}(T)\,dT=\sum_{j}\dot{n}_{fg,j}\int_{T_{\text{ref}}}^{T_{\text{flame}}}C_{p,j}(T)\,dT
\]


where $T_{\text{ref}}=298.15\;\text{K}$ is the reference temperature, $T_{\text{air}}$ is the combustion air inlet temperature, $C_{p,j}(T)$ are polynomial heat capacity functions for each flue gas species $j$ , and $\dot{n}_{fg,j}$ is the molar flow of flue gas species $j$ . The model uses Newton–Raphson iteration to solve for $T_{\text{flame}}$ .

###### Heat capacity correlations

The molar heat capacities $C_{p}$ \[ $\text{J}/(\text{mol}\cdot\text{K})$ \] are expressed as third-order polynomials in temperature $T$ \[K\]:



\[
C_{p}(T)=a+bT+cT^{2}+dT^{3}
\]


where $a$ , $b$ , $c$ , and $d$ are species-specific coefficients listed in Table $~§$ .



<a id="tab:Cp_coefficients"></a>



| **Species** | $a$ | $b$ | $c$ | $d$ |
|:---|:--:|:--:|:--:|:--:|
| $\text{CO}_{2}$ | 22.26 | $5.981\times10^{-2}$ | $-3.501\times10^{-5}$ | $7.469\times10^{-9}$ |
| $\text{H}_{2}\text{O}$ | 32.24 | $1.924\times10^{-3}$ | $1.055\times10^{-5}$ | $-3.596\times10^{-9}$ |
| $\text{N}_{2}$ | 28.90 | $-1.571\times10^{-3}$ | $8.081\times10^{-6}$ | $-2.873\times10^{-9}$ |
| $\text{O}_{2}$ | 25.48 | $1.520\times10^{-2}$ | $-7.156\times10^{-6}$ | $1.312\times10^{-9}$ |
| $\text{SO}_{2}$ | 25.78 | $5.795\times10^{-2}$ | $-3.812\times10^{-5}$ | $8.612\times10^{-9}$ |
| $\text{Ar}$ | 20.786 | 0 | 0 | 0 |

Polynomial coefficients for molar heat capacity $C_{p}$ \[$\text{J}/(\text{mol}\cdot\text{K})$\].



##### Radiant Section Model

The radiant section is the primary heat transfer zone where the process fluid absorbs heat predominantly by thermal radiation from the hot combustion gases and refractory walls. The model uses the Lobo–Evans method (Lobo and Evans, 1939).

###### Mean beam length

The mean beam length $L_{b}$ for the combustion gas volume is calculated as:



\[
L_{b}=0.9\times\frac{3.6\,V_{\text{chamber}}}{A_{\text{chamber}}}
\]


where $V_{\text{chamber}}$ is the chamber volume \[ $\text{m}^{3}$ \] and $A_{\text{chamber}}$ is the total internal surface area \[ $\text{m}^{2}$ \]. The factor $0.9$ is the correction from the optically thin limit to the actual mean beam length.

###### Gas emissivity (Hottel method)

The total emissivity of the combustion gases $\varepsilon_{g}$ is calculated as the sum of contributions from $\text{CO}_{2}$ and $\text{H}_{2}\text{O}$ with an overlap correction $\Delta\varepsilon$ :



\[
\varepsilon_{g}=\varepsilon_{\text{CO}_{2}}+\varepsilon_{\text{H}_{2}\text{O}}-\Delta\varepsilon
\]


The individual emissivities $\varepsilon_{\text{CO}_{2}}$ and $\varepsilon_{\text{H}_{2}\text{O}}$ are functions of the partial-pressure–path-length product $pL$ \[ $\text{atm}\cdot\text{m}$ \] and the gas temperature, evaluated from simplified Hottel correlations. The overlap correction $\Delta\varepsilon$ accounts for spectral band interference between $\text{CO}_{2}$ and $\text{H}_{2}\text{O}$ :



\[
\Delta\varepsilon=0.02\;\frac{p_{\text{H}_{2}\text{O}}L}{(p_{\text{CO}_{2}}+p_{\text{H}_{2}\text{O}})L}\;\left(1-\frac{p_{\text{H}_{2}\text{O}}L}{(p_{\text{CO}_{2}}+p_{\text{H}_{2}\text{O}})L}\right)\;(p_{\text{CO}_{2}}+p_{\text{H}_{2}\text{O}})L
\]


where $p_{\text{CO}_{2}}$ and $p_{\text{H}_{2}\text{O}}$ are the partial pressures of $\text{CO}_{2}$ and $\text{H}_{2}\text{O}$ in the flue gas \[atm\] and $L$ is the mean beam length $L_{b}$ \[m\].

###### Exchange factor

The total exchange factor $\mathcal{F}$ accounts for the geometric arrangement of tubes and refractory walls using the Lobo–Evans formulation:



\[
\mathcal{F}=\frac{1}{\dfrac{1}{\varepsilon_{g}}+\dfrac{A_{cp}}{A_{cp}+A_{r}}\left(\dfrac{1}{\varepsilon_{t}}-1\right)}
\]


where $\varepsilon_{g}$ is the gas emissivity, $\varepsilon_{t}$ is the tube surface emissivity (typically $0.9$ for oxidized steel), $A_{cp}$ is the cold-plane area (the projected area of the tube bank as seen from the flame) \[ $\text{m}^{2}$ \], and $A_{r}$ is the exposed refractory area \[ $\text{m}^{2}$ \].

###### Cold-plane area factor (alpha)

The fraction of the tube plane that intercepts radiation is characterized by the alpha factor $\alpha$ , which depends on the tube outer diameter $D_{o}$ and the centre-to-centre tube pitch $S$ :



\[
\alpha=1-\left(1-\frac{D_{o}}{S}\right)\sqrt{1-\left(\frac{D_{o}}{S}\right)^{2}}-\frac{1}{\pi}\arcsin\left(\frac{D_{o}}{S}\right)
\]


The effective cold-plane area is then $A_{cp,\text{eff}}=\alpha\;A_{cp}$ , where $A_{cp}=N_{t}\;D_{o}\;L_{t}$ is the total projected plane area, $N_{t}$ is the number of tubes, and $L_{t}$ is the effective tube length.

###### Radiant heat transfer

The heat absorbed in the radiant section $Q_{\text{rad}}$ comprises a dominant radiative component and a minor convective component:



\[
Q_{\text{rad}}=\mathcal{F}\;\sigma\;A_{cp,\text{eff}}\left(T_{g,\text{mean}}^{4}-T_{t}^{4}\right)+h_{c,\text{rad}}\;A_{t}\left(T_{g,\text{mean}}-T_{t}\right)
\]


where $\sigma=5.67\times10^{-8}$ $\text{W}/(\text{m}^{2}\cdot\text{K}^{4})$ is the Stefan–Boltzmann constant, $T_{g,\text{mean}}=(T_{\text{flame}}+T_{bw})/2$ is the mean gas temperature in the radiant section, $T_{t}$ is the average tube surface temperature \[K\], $A_{t}$ is the total tube external surface area $A_{t}=N_{t}\;\pi\;D_{o}\;L_{t}$ \[ $\text{m}^{2}$ \], and $h_{c,\text{rad}}\approx11.4$ $\text{W}/(\text{m}^{2}\cdot\text{K})$ is the natural convection coefficient inside the radiant chamber.

###### Bridgewall temperature

The bridgewall temperature $T_{bw}$ (gas temperature at the exit of the radiant section, entering the convection section) is found by iterative solution of the energy balance:



\[
Q_{\text{rad}}+\dot{n}_{fg}\;C_{p,fg}(T_{bw})\;\left(T_{bw}-T_{\text{ref}}\right)=Q_{\text{rel}}\left(1-f_{\text{wall}}\right)
\]


where $\dot{n}_{fg}$ is the total flue gas molar flow \[ $\text{mol}/\text{s}$ \], $C_{p,fg}(T_{bw})$ is the mean molar heat capacity of the flue gas mixture evaluated at $T_{bw}$ \[ $\text{J}/(\text{mol}\cdot\text{K})$ \], $T_{\text{ref}}=298.15\;\text{K}$ is the reference temperature, $Q_{\text{rel}}$ is the total heat released by combustion \[W\], and $f_{\text{wall}}$ is the fractional heat loss through the furnace walls (typically $0.02$ ). The iteration is performed using the Newton–Raphson method.

###### Tube skin temperature

The average tube metal temperature (skin temperature) $T_{\text{skin}}$ is critical for metallurgical design and is calculated from the inside-out thermal resistance model:



\[
T_{\text{skin}}=T_{\text{process}}+\dot{q}_{\text{avg}}\left(\frac{D_{o}}{D_{i}\;h_{\text{int}}}+\frac{D_{o}}{2k_{w}}\ln\frac{D_{o}}{D_{i}}\right)
\]


where $T_{\text{process}}$ is the bulk process fluid temperature \[K\], $\dot{q}_{\text{avg}}=Q_{\text{rad}}/A_{t}$ is the average heat flux \[ $\text{W}/\text{m}^{2}$ \], $D_{o}$ is the tube outer diameter \[m\], $D_{i}$ is the tube inner diameter \[m\], $h_{\text{int}}$ is the internal film coefficient of the process fluid \[ $\text{W}/(\text{m}^{2}\cdot\text{K})$ \], and $k_{w}$ is the thermal conductivity of the tube wall material \[ $\text{W}/(\text{m}\cdot\text{K})$ \].

The maximum skin temperature $T_{\text{skin,max}}$ accounts for the circumferential heat flux variation with a factor $f_{\text{circ}}\approx1.5$ for single-row tubes backed by refractory:



\[
T_{\text{skin,max}}=T_{\text{process}}+f_{\text{circ}}\;\dot{q}_{\text{avg}}\left(\frac{D_{o}}{D_{i}\;h_{\text{int}}}+\frac{D_{o}}{2k_{w}}\ln\frac{D_{o}}{D_{i}}\right)
\]


##### Shield Section Model

The shield section consists of one or two rows of bare (unfinned) tubes located between the radiant and convection sections. These tubes receive both direct radiation from the radiant chamber and convective heat transfer from the flue gases crossing them. The total duty absorbed by the shield $Q_{\text{shield}}$ is:



\[
Q_{\text{shield}}=f_{r}\;\varepsilon_{g}\;\sigma\;A_{\text{shield}}\left(T_{bw}^{4}-T_{t,s}^{4}\right)+h_{c,s}\;A_{\text{shield}}\left(T_{bw}-T_{t,s}\right)
\]


where $f_{r}$ is the fraction of radiant heat reaching the shield (typically $0.08$ - $0.12$ ), $A_{\text{shield}}$ is the total tube surface area in the shield section \[ $\text{m}^{2}$ \], $T_{t,s}$ is the tube surface temperature in the shield \[K\], and $h_{c,s}$ is the convective coefficient for flue gas crossing the shield tubes \[ $\text{W}/(\text{m}^{2}\cdot\text{K})$ \].

The gas temperature exiting the shield $T_{g,\text{out,shield}}$ is obtained from the energy balance on the gas side:



\[
T_{g,\text{out,shield}}=T_{bw}-\frac{Q_{\text{shield}}}{\dot{n}_{fg}\;C_{p,fg}(T_{bw})}
\]


##### Convection Section Model

The convection section recovers additional heat from the flue gases as they flow across a bank of tubes (often finned) before exiting through the stack. The model uses the Zukauskas correlation for crossflow over tube banks.

###### External heat transfer coefficient

The Nusselt number $\text{Nu}$ for crossflow over a bank of tubes is:



\[
\text{Nu}=C_{1}\;C_{2}\;\text{Re}^{m}\;\text{Pr}^{0.36}
\]


where $\text{Re}=\rho_{g}\;V_{\max}\;D_{o}/\mu_{g}$ is the Reynolds number based on the maximum velocity $V_{\max}$ in the minimum flow area, $\text{Pr}=\mu_{g}\;C_{p,g}/(k_{g}\;M_{fg})$ is the Prandtl number of the flue gas, $\rho_{g}$ is the gas density \[ $\text{kg}/\text{m}^{3}$ \], $\mu_{g}$ is the dynamic viscosity \[ $\text{Pa}\cdot\text{s}$ \], $k_{g}$ is the thermal conductivity \[ $\text{W}/(\text{m}\cdot\text{K})$ \], and the constants $C_{1}$ and $m$ depend on the Reynolds range and tube arrangement as shown in Table $~§$ .



<a id="tab:zukauskas"></a>



<table>
<caption>Zukauskas correlation constants for crossflow over tube banks.</caption>
<thead>
<tr>
<th style="text-align: left;"><strong>Re range</strong></th>
<th colspan="2" style="text-align: center;"><strong>Inline</strong></th>
<th style="text-align: center;"></th>
<th colspan="2" style="text-align: center;"><strong>Staggered</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;"><span>2-3</span></td>
<td style="text-align: center;"><span class="math inline">\(C_{1}\)</span></td>
<td style="text-align: center;"><span class="math inline">\(m\)</span></td>
<td style="text-align: center;"></td>
<td style="text-align: center;"><span class="math inline">\(C_{1}\)</span></td>
<td style="text-align: center;"><span class="math inline">\(m\)</span></td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(\text{Re}&lt;500\)</span></td>
<td style="text-align: center;">0.9</td>
<td style="text-align: center;">0.4</td>
<td style="text-align: center;"></td>
<td style="text-align: center;">1.04</td>
<td style="text-align: center;">0.4</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(500\leq\text{Re}&lt;10^{3}\)</span></td>
<td style="text-align: center;">0.52</td>
<td style="text-align: center;">0.5</td>
<td style="text-align: center;"></td>
<td style="text-align: center;">0.71</td>
<td style="text-align: center;">0.5</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(10^{3}\leq\text{Re}&lt;2\times10^{5}\)</span></td>
<td style="text-align: center;">0.27</td>
<td style="text-align: center;">0.63</td>
<td style="text-align: center;"></td>
<td style="text-align: center;">0.35</td>
<td style="text-align: center;">0.63</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(\text{Re}\geq2\times10^{5}\)</span></td>
<td style="text-align: center;">0.033</td>
<td style="text-align: center;">0.8</td>
<td style="text-align: center;"></td>
<td style="text-align: center;">0.031</td>
<td style="text-align: center;">0.8</td>
</tr>
</tbody>
</table>



The row correction factor $C_{2}$ accounts for the number of tube rows $N_{r}$ :



\[
C_{2}\approx0.70+0.30\;\frac{N_{r}}{20}\quad\text{for }N_{r}<20;\quad C_{2}=1\quad\text{for }N_{r}\geq20
\]


The external heat transfer coefficient $h_{o}$ \[ $\text{W}/(\text{m}^{2}\cdot\text{K})$ \] is then:



\[
h_{o}=\frac{\text{Nu}\;k_{g}}{D_{o}}
\]


where $k_{g}$ is the thermal conductivity of the flue gas \[ $\text{W}/(\text{m}\cdot\text{K})$ \] and $D_{o}$ is the tube outer diameter \[m\].

###### Fin efficiency

For finned tubes, the annular fin efficiency $\eta_{f}$ is calculated using the Harper-Brown solution:



\[
\eta_{f}=\frac{\tanh(m\,r_{1}\,\phi)}{m\,r_{1}\,\phi}
\]


where the parameter $m$ and the geometric correction $\phi$ are defined as:



\[
\begin{align}
m & =\sqrt{\frac{2\,h_{o}}{k_{f}\,t_{f}}}\\
\phi & =\left(\frac{r_{2}}{r_{1}}-1\right)\left(1+0.35\ln\frac{r_{2}}{r_{1}}\right)
\end{align}
\]


with $r_{1}=D_{o}/2$ being the tube outer radius \[m\], $r_{2}=r_{1}+H_{f}$ the fin tip radius \[m\], $H_{f}$ the fin height \[m\], $k_{f}$ the fin material thermal conductivity \[ $\text{W}/(\text{m}\cdot\text{K})$ \], and $t_{f}$ the fin thickness \[m\].

###### Overall heat transfer coefficient

The overall heat transfer coefficient $U$ \[ $\text{W}/(\text{m}^{2}\cdot\text{K})$ \] based on the external surface area is:



\[
U=\frac{1}{\dfrac{1}{h_{o}\,\eta_{f}}+R_{\text{foul}}+\dfrac{D_{o}}{2k_{w}}\ln\dfrac{D_{o}}{D_{i}}+\dfrac{D_{o}}{D_{i}\,h_{\text{int}}}}
\]


where $h_{o}$ is the external (gas-side) heat transfer coefficient, $\eta_{f}$ is the fin efficiency (equal to $1.0$ for bare tubes), $R_{\text{foul}}$ is the fouling resistance \[ $\text{m}^{2}\cdot\text{K}/\text{W}$ \] (typically $0.0002$ for clean flue gas service), $k_{w}$ is the tube wall thermal conductivity \[ $\text{W}/(\text{m}\cdot\text{K})$ \], $D_{o}$ and $D_{i}$ are the tube outer and inner diameters \[m\], and $h_{\text{int}}$ is the internal (process-side) film coefficient \[ $\text{W}/(\text{m}^{2}\cdot\text{K})$ \].

###### Duty calculation

The heat transferred in the convection section $Q_{\text{conv}}$ \[W\] is:



\[
Q_{\text{conv}}=U\;A_{\text{eff}}\;\Delta T_{\text{lm}}
\]


where $A_{\text{eff}}$ is the effective (fin-weighted) surface area \[ $\text{m}^{2}$ \] and $\Delta T_{\text{lm}}$ is the log-mean temperature difference for counter-current flow \[K\]:



\[
\Delta T_{\text{lm}}=\frac{\left(T_{g,\text{in}}-T_{p,\text{out}}\right)-\left(T_{g,\text{out}}-T_{p,\text{in}}\right)}{\ln\dfrac{T_{g,\text{in}}-T_{p,\text{out}}}{T_{g,\text{out}}-T_{p,\text{in}}}}
\]


Here $T_{g,\text{in}}$ is the gas inlet temperature to the convection bank (equal to $T_{g,\text{out,shield}}$ , the outlet of the shield section) \[K\], $T_{g,\text{out}}$ is the gas outlet temperature \[K\], $T_{p,\text{in}}$ is the process fluid inlet temperature to the convection section \[K\], and $T_{p,\text{out}}$ is the process fluid temperature exiting the convection section and entering the radiant section \[K\].

##### Draft Model

###### Natural draft

The available draft $\Delta P_{\text{draft}}$ \[Pa\] from the stack is generated by the density difference between the ambient air and the hot flue gases:



\[
\Delta P_{\text{draft}}=g\;H_{\text{stack}}\left(\rho_{\text{amb}}-\rho_{fg}\right)
\]


where $g=9.81$ $\text{m}/\text{s}^{2}$ is the gravitational acceleration, $H_{\text{stack}}$ is the stack height \[m\], $\rho_{\text{amb}}$ is the ambient air density \[ $\text{kg}/\text{m}^{3}$ \], and $\rho_{fg}$ is the flue gas density at stack temperature \[ $\text{kg}/\text{m}^{3}$ \], calculated from the ideal gas law:



\[
\rho_{fg}=\frac{P\;M_{fg}}{R\;T_{\text{stack}}}
\]


where $P$ is the absolute pressure \[Pa\], $M_{fg}$ is the mean molar mass of the flue gas \[ $\text{kg}/\text{mol}$ \], $R=8.314$ $\text{J}/(\text{mol}\cdot\text{K})$ is the universal gas constant, and $T_{\text{stack}}$ is the flue gas temperature at the stack \[K\]. The ideal gas law is appropriate for the flue gas and ambient air density calculations because these gases are at near-atmospheric pressure and elevated temperature, where the compressibility factor $Z\approx1.000$ . The process-side density, in contrast, is obtained from the property package (see Process-Side Pressure Drop below).

###### Pressure drops

The convection section pressure drop $\Delta P_{\text{conv}}$ \[Pa\] is estimated using an Euler number approach:



\[
\Delta P_{\text{conv}}=\text{Eu}\;N_{r}\;\frac{\rho_{g}\;V_{\max}^{2}}{2}
\]


where $\text{Eu}$ is the Euler number (approximately $1.0$ for staggered and $0.7$ for inline arrangements), $N_{r}$ is the number of tube rows, $\rho_{g}$ is the gas density at the mean convection temperature \[ $\text{kg}/\text{m}^{3}$ \], and $V_{\max}$ is the maximum gas velocity in the minimum flow cross-section \[ $\text{m}/\text{s}$ \].

The stack friction loss $\Delta P_{\text{stack}}$ \[Pa\] is estimated by the Darcy–Weisbach equation:



\[
\Delta P_{\text{stack}}=f\;\frac{H_{\text{stack}}}{D_{\text{stack}}}\;\frac{\rho_{fg}\;V_{\text{stack}}^{2}}{2}
\]


where $f$ is the Darcy friction factor (typically $0.02$ ), $D_{\text{stack}}$ is the stack inner diameter \[m\], $\rho_{fg}$ is the flue gas density at stack conditions \[ $\text{kg}/\text{m}^{3}$ \], and $V_{\text{stack}}$ is the flue gas velocity inside the stack \[ $\text{m}/\text{s}$ \].

The net draft $\Delta P_{\text{net}}$ \[Pa\] is:



\[
\Delta P_{\text{net}}=\Delta P_{\text{draft}}-\Delta P_{\text{conv}}-\Delta P_{\text{rad}}-\Delta P_{\text{stack}}
\]


where $\Delta P_{\text{rad}}$ is the pressure drop across the radiant chamber (typically small, around $5\;\text{Pa}$ for an open firebox). A positive value of $\Delta P_{\text{net}}$ indicates the system is self-drafting; a negative value means forced or induced draft is required.

##### Thermal Efficiency

The overall thermal efficiency $\eta$ is:



\[
\eta=1-\frac{Q_{\text{stack}}}{Q_{\text{rel}}}-f_{\text{wall}}
\]


where $Q_{\text{rel}}$ is the total heat released by combustion \[W\], $f_{\text{wall}}$ is the fractional wall loss, and the stack loss $Q_{\text{stack}}$ \[W\] is:



\[
Q_{\text{stack}}=\dot{n}_{fg}\;C_{p,fg}(T_{\text{stack}})\;\left(T_{\text{stack}}-T_{\text{amb}}\right)
\]


where $\dot{n}_{fg}$ is the total flue gas molar flow \[ $\text{mol}/\text{s}$ \], $C_{p,fg}(T_{\text{stack}})$ is the mean molar heat capacity of the flue gas evaluated at the stack temperature \[ $\text{J}/(\text{mol}\cdot\text{K})$ \], $T_{\text{stack}}$ is the flue gas exit temperature \[K\], and $T_{\text{amb}}$ is the ambient temperature \[K\]. Typical thermal efficiencies for well-designed process furnaces range from $80\%$ to $92\%$ , depending on stack temperature and excess air.

##### Emissions

###### $CO_{2}$ and $SO_{2}$

These are calculated directly from the combustion stoichiometry:



\[
\begin{align}
\dot{m}_{\text{CO}_{2}} & =\dot{n}_{\text{CO}_{2}}\;M_{\text{CO}_{2}}\\
\dot{m}_{\text{SO}_{2}} & =\dot{n}_{\text{SO}_{2}}\;M_{\text{SO}_{2}}
\end{align}
\]


where $\dot{n}_{\text{CO}_{2}}$ and $\dot{n}_{\text{SO}_{2}}$ are the molar flows from the combustion model \[ $\text{mol}/\text{s}$ \], $M_{\text{CO}_{2}}=0.04401$ $\text{kg}/\text{mol}$ is the molar mass of carbon dioxide, and $M_{\text{SO}_{2}}=0.06406$ $\text{kg}/\text{mol}$ is the molar mass of sulfur dioxide.

###### $NO_{x}$

$\text{NO}_{x}$ formation is estimated using an empirical correlation that accounts for the peak flame temperature $T_{\text{flame}}$ and excess oxygen concentration $y_{\text{O}_{2}}$ :



\[
C_{\text{NO}_{x}}\;\text{[mg/Nm}^{3}\text{]}=4.0\times10^{-8}\;\exp\left(0.01\;T_{\text{flame}}\right)\;\sqrt{y_{\text{O}_{2}}\times100}
\]


where $T_{\text{flame}}$ is the adiabatic flame temperature \[K\] and $y_{\text{O}_{2}}$ is the mole fraction of excess $\text{O}_{2}$ in the flue gas. This is a simplified estimate suitable for conventional gas-fired burners. For low- $\text{NO}_{x}$ burner performance, the user should consult manufacturer data.

###### Process-Side Pressure Drop

The pressure drop through the process tubes $\Delta P_{\text{process}}$ \[Pa\] is calculated using the Darcy–Weisbach equation with corrections for return bends:



\[
\Delta P_{\text{process}}=f\;\frac{L_{\text{total}}+L_{\text{eq,bends}}}{D_{i}}\;\frac{\rho_{p}\;V_{p}^{2}}{2}
\]


where $f$ is the Darcy friction factor, $L_{\text{total}}=L_{\text{tube}}\times N_{\text{passes}}$ is the total tube length \[m\], $L_{\text{eq,bends}}=(N_{\text{passes}}-1)\times30\;D_{i}$ is the equivalent length for return bends \[m\], $D_{i}$ is the tube inner diameter \[m\], $\rho_{p}$ is the process fluid density \[ $\text{kg}/\text{m}^{3}$ \] obtained from the property package (overall phase density of the process inlet stream), $V_{p}$ is the process fluid velocity in the tubes \[ $\text{m}/\text{s}$ \], $L_{\text{tube}}$ is the effective tube length per pass \[m\], and $N_{\text{passes}}$ is the number of process fluid passes through the furnace. The process fluid velocity is calculated as:



\[
V_{p}=\frac{\dot{m}_{p}}{\rho_{p}\;A_{\text{flow}}}
\]


where $\dot{m}_{p}$ is the total process mass flow rate \[ $\text{kg}/\text{s}$ \] and $A_{\text{flow}}=N_{\text{tubes/pass}}\;\pi\;D_{i}^{2}/4$ is the total flow cross-sectional area per pass \[ $\text{m}^{2}$ \]. Using the property-package density ensures correct pressure-drop estimation for both liquid and gas-phase process fluids, including high-pressure or supercritical services where the ideal gas law would be inaccurate.

##### Solution Algorithm

The overall calculation procedure follows these steps:

1.  Read process inlet and fuel inlet stream properties (temperature, pressure, flow, composition).

2.  Based on the operating mode, estimate the fuel flow rate or outlet temperature.

3.  Calculate the combustion stoichiometry, heat released $Q_{\text{rel}}$ , and adiabatic flame temperature $T_{\text{flame}}$ .

4.  Assume an initial radiant/convective duty split ( $70/30$ ).

5.  Iterate the radiant section model to find the bridgewall temperature $T_{bw}$ .

6.  Calculate the shield section duty $Q_{\text{shield}}$ and gas outlet temperature $T_{g,\text{out,shield}}$ .

7.  Calculate the convection section duty $Q_{\text{conv}}$ , gas outlet temperature $T_{g,\text{out}}$ , and overall coefficient $U$ .

8.  Update the duty split and intermediate process temperature; repeat from step 5 until convergence (temperature change $<0.5\;\text{K}$ between iterations).

9.  Calculate draft $\Delta P_{\text{net}}$ , emissions ( $\dot{m}_{\text{CO}_{2}}$ , $\dot{m}_{\text{SO}_{2}}$ , $C_{\text{NO}_{x}}$ ), and thermal efficiency $\eta$ .

10. Update all output streams.

Convergence is typically achieved in $10$ – $30$ outer iterations with a sub-relaxation factor of $0.4$ for stability.

##### Input Parameters



<a id="tab:radiant_params"></a>



| **Parameter** | **Description** | **Unit** |
|:---|:---|:--:|
| Length | Internal chamber length | m |
| Width | Internal chamber width | m |
| Height | Internal chamber height | m |
| TubeOuterDiameter | Tube outer diameter ($D_{o}$) | m |
| TubeWallThickness | Tube wall thickness | m |
| TubeEffectiveLength | Heated tube length ($L_{t}$) | m |
| NumberOfTubes | Total tubes in radiant section ($N_{t}$) | – |
| NumberOfPasses | Process fluid passes ($N_{\text{passes}}$) | – |
| TubePitch | Centre-to-centre spacing ($S$) | m |
| TubeToWallDistance | Centre of tube to refractory wall | m |
| TubeThermalConductivity | Tube material conductivity ($k_{w}$) | $\text{W}/(\text{m}\cdot\text{K})$ |
| TubeEmissivity | Oxidised tube surface emissivity ($\varepsilon_{t}$) | – |
| RefractoryEmissivity | Refractory wall emissivity | – |
| RefractoryThickness | Refractory lining thickness | m |

Radiant section geometry parameters.





<a id="tab:convection_params"></a>



| **Parameter** | **Description** | **Unit** |
|:---|:---|:--:|
| TubeOuterDiameter | Tube outer diameter ($D_{o}$) | m |
| TubeWallThickness | Tube wall thickness | m |
| TubeEffectiveLength | Tube length | m |
| NumberOfTubes | Total tubes | – |
| NumberOfRows | Tube rows perpendicular to gas flow ($N_{r}$) | – |
| NumberOfPasses | Process fluid passes | – |
| TransversePitch | Pitch perpendicular to gas flow ($S_{T}$) | m |
| LongitudinalPitch | Pitch along gas flow ($S_{L}$) | m |
| Arrangement | Inline or staggered | – |
| FinType | Bare, solid fin, or serrated fin | – |
| FinHeight | Fin height from tube surface ($H_{f}$) | m |
| FinThickness | Fin thickness ($t_{f}$) | m |
| FinDensity | Fins per unit length | $1/\text{m}$ |
| FinThermalConductivity | Fin material conductivity ($k_{f}$) | $\text{W}/(\text{m}\cdot\text{K})$ |

Convection section geometry parameters.





<a id="tab:stack_params"></a>



| **Parameter** | **Description** | **Unit** |
|:---|:---|:--:|
| StackHeight | Chimney height ($H_{\text{stack}}$) | m |
| StackInnerDiameter | Chimney inner diameter ($D_{\text{stack}}$) | m |
| AmbientTemperature | Surrounding air temperature ($T_{\text{amb}}$) | K |
| AmbientPressure | Atmospheric pressure ($P$) | Pa |
| ExcessAir | Fractional excess air ($e_{a}$, e.g. $0.15=15\%$) | – |
| WallHeatLossFraction | Heat loss as fraction of $Q_{\text{rel}}$ ($f_{\text{wall}}$) | – |
| DraftType | Natural, forced, or induced | – |

Stack and general operating parameters.



##### Output Variables

The model reports the following results: total duty absorbed $Q_{\text{total}}$ \[W\], thermal efficiency $\eta$ \[ $\%$ \], fuel consumption \[ $\text{kg}/\text{s}$ \], bridgewall temperature $T_{bw}$ \[K\], average and maximum tube skin temperatures $T_{\text{skin}}$ and $T_{\text{skin,max}}$ \[K\], average and maximum heat flux $\dot{q}_{\text{avg}}$ and $\dot{q}_{\max}$ \[ $\text{W}/\text{m}^{2}$ \], flue gas outlet temperature $T_{g,\text{out}}$ \[K\], available and net draft $\Delta P_{\text{draft}}$ and $\Delta P_{\text{net}}$ \[Pa\], stack gas velocity $V_{\text{stack}}$ \[ $\text{m}/\text{s}$ \], $\text{CO}_{2}$ emission rate $\dot{m}_{\text{CO}_{2}}$ \[ $\text{kg}/\text{h}$ \], $\text{SO}_{2}$ emission rate $\dot{m}_{\text{SO}_{2}}$ \[ $\text{g}/\text{h}$ \], $\text{NO}_{x}$ concentration $C_{\text{NO}_{x}}$ \[ $\text{mg}/\text{Nm}^{3}$ \], and the complete temperature profile across all sections.

#### Zeolite Adsorber

##### Overview

The **Zeolite Adsorber** is a general-purpose gas-phase adsorption unit operation that models the separation of multicomponent gas mixtures on zeolite (or other microporous) adsorbents. The model supports two operating modes:

- **Equilibrium mode** – a steady-state shortcut calculation based on working capacity at specified adsorption and desorption conditions.

- **PSA Cycle mode** – a simplified four-step Skarstrom pressure-swing (or temperature-swing) adsorption cycle that yields cycle-averaged raffinate and desorbate flows.

Three isotherm families are available: single-site Langmuir, dual-site Langmuir (DSL), and Freundlich. Multicomponent competition is handled through the extended (competitive) Langmuir and DSL mixing rules.

##### Stream Topology

The unit operation has the following connection ports:







| **Port**      | **Direction**     | **Description**                     |
|:--------------|:------------------|:------------------------------------|
| Feed Gas In   | Inlet (material)  | Mixed-gas feed stream               |
| Raffinate Out | Outlet (material) | Less- or non-adsorbed product       |
| Desorbate Out | Outlet (material) | Adsorbed product (regeneration gas) |



##### Isotherm Models {#sec:isotherms}

All isotherm calculations use partial pressure $p_i = y_i P$ as the independent variable, where $y_i$ is the mole fraction of component $i$ and $P$ is the total pressure. Loadings are expressed in mol kg$^{-1}$ (mol of adsorbate per kg of dry adsorbent).

###### Temperature Dependence of Affinity Constants

For the Langmuir and DSL models, the affinity constant follows a van’t Hoff relationship:


<a id="eq:vantHoff"></a>

\[
b_i(T) = b_{0,i} \exp\!\left(\frac{\Delta H_i}{R T}\right)
\]


where $b_{0,i}$ is the pre-exponential factor (Pa$^{-1}$), $\Delta H_i > 0$ is the isosteric heat of adsorption (J mol$^{-1}$, sign convention: positive for exothermic adsorption), $R = 8.314$ J mol$^{-1}$ K$^{-1}$ is the universal gas constant, and $T$ is the absolute temperature (K). This convention ensures that $b_i$ increases as temperature decreases, which is physically correct for physisorption.

###### Single-Site Langmuir (SSL)

The pure-component single-site Langmuir isotherm is


<a id="eq:langmuir_pure"></a>

\[
q_i = \frac{q_{\mathrm{sat},i}\, b_i(T)\, p_i}
               {1 + b_i(T)\, p_i}
\]


where $q_{\mathrm{sat},i}$ (mol kg$^{-1}$) is the saturation capacity.

For a multicomponent mixture the **extended Langmuir** mixing rule is applied:


<a id="eq:langmuir_multi"></a>

\[
q_i = \frac{q_{\mathrm{sat},i}\, b_i(T)\, p_i}
               {1 + \displaystyle\sum_{j=1}^{N} b_j(T)\, p_j}
\]


This expression is thermodynamically consistent (satisfies Gibbs–Duhem) only when all saturation capacities are equal; it is used as a practical approximation for unequal $q_{\mathrm{sat}}$ values.

###### Dual-Site Langmuir (DSL)

Zeolites often present two structurally distinct adsorption sites (e.g. cation sites and window sites in zeolite 5A, or cage and window sites in zeolite 13X). The pure-component DSL isotherm is


<a id="eq:DSL_pure"></a>

\[
q_i = \frac{q_{\mathrm{sat}1,i}\, b_{1,i}(T)\, p_i}
               {1 + b_{1,i}(T)\, p_i}
        + \frac{q_{\mathrm{sat}2,i}\, b_{2,i}(T)\, p_i}
               {1 + b_{2,i}(T)\, p_i}
\]


For a mixture, each site type is treated as independent and the competitive Langmuir mixing rule is applied site-by-site:


<a id="eq:DSL_multi"></a>

\[
q_i = \frac{q_{\mathrm{sat}1,i}\, b_{1,i}(T)\, p_i}
               {1 + \displaystyle\sum_j b_{1,j}(T)\, p_j}
        + \frac{q_{\mathrm{sat}2,i}\, b_{2,i}(T)\, p_i}
               {1 + \displaystyle\sum_j b_{2,j}(T)\, p_j}
\]


###### Freundlich

The Freundlich isotherm is an empirical power-law expression:


<a id="eq:freundlich"></a>

\[
q_i = K_i\, p_i^{1/n_i}
\]


where $K_i$ (mol kg$^{-1}$ Pa$^{-1/n}$) is the Freundlich pre-factor and $n_i > 0$ is the heterogeneity index. For $n_i > 1$ the isotherm is sub-linear (concave), which is typical for heterogeneous surfaces. Because no rigorous IAST extension is available for the Freundlich isotherm, an additive (independent) approximation is used for mixtures; this is valid only when inter-component competition is weak.

###### Note on temperature dependence

The current implementation treats $K_i$ as temperature-independent. Users who need to account for temperature effects should supply $K_i$ values measured at the desired operating temperature.

##### Working Capacity {#sec:working_capacity}

The *working* (or *delta*) capacity is the difference in equilibrium loading between adsorption and desorption conditions:


<a id="eq:working_capacity"></a>

\[
\Delta q_i = q_i(T_{\mathrm{ads}}, p_i^{\mathrm{ads}})
               - q_i(T_{\mathrm{des}}, p_i^{\mathrm{des}})
\]


where the desorption partial pressures are approximated as


<a id="eq:des_partial"></a>

\[
p_i^{\mathrm{des}} = y_i^{\mathrm{feed}}\, P_{\mathrm{des}}
\]


This assumes the feed mole fractions remain unchanged during regeneration, which is an approximation that is most accurate for dilute systems and for PSA processes where blowdown is fast.

##### Equilibrium (Shortcut) Mode {#sec:equilibrium_mode}

In Equilibrium mode the model converts the cycle working capacity into an equivalent steady-state molar flow of adsorbed species using the cycle time $t_{\mathrm{cyc}}$ (s) as a time basis:


<a id="eq:ads_flow"></a>

\[
\dot{n}_{i}^{\mathrm{ads}} = \frac{\Delta q_i\, M_{\mathrm{ads}}}
                                       {t_{\mathrm{cyc}}}
\]


where $M_{\mathrm{ads}}$ (kg) is the total adsorbent mass.

The raffinate flow before purge correction is


<a id="eq:raff_gross"></a>

\[
\dot{n}_{i}^{\mathrm{raff,0}} = \dot{n}_{i}^{\mathrm{feed}}
                                  - \dot{n}_{i}^{\mathrm{ads}}
\]


A fraction $\phi_{\mathrm{purge}}$ of the net raffinate is recycled as purge gas to assist regeneration. The final raffinate and desorbate flows are


<a id="eq:raff_net"></a><a id="eq:des_flow"></a>

\[
\begin{align}
    \dot{n}_{i}^{\mathrm{raff}} &= \dot{n}_{i}^{\mathrm{raff,0}}
                                 \left(1 - \phi_{\mathrm{purge}}\right)
    \\[4pt]
    \dot{n}_{i}^{\mathrm{des}}  &= \dot{n}_{i}^{\mathrm{ads}}
                                 + \phi_{\mathrm{purge}}\,
                                   \dot{n}_{i}^{\mathrm{raff,0}}
\end{align}
\]


A physical constraint is applied so that the adsorbed flow cannot exceed the feed flow ($\dot{n}_{i}^{\mathrm{ads}} \le \dot{n}_{i}^{\mathrm{feed}}$).

##### PSA Cycle Mode {#sec:psa_mode}

The PSA Cycle mode simulates a simplified four-step **Skarstrom cycle** operating at local equilibrium (sharp-front approximation). The four steps and their mass balances are described below. All mole quantities refer to a single bed; cycle-averaged flows are obtained by dividing by $t_{\mathrm{cyc}}$.

The cycle time is partitioned as follows. Let $f_{\mathrm{press}}$ be the fraction of the cycle allocated to pressurisation; the remaining time is split equally between adsorption/feed and blowdown+purge:


\[
\begin{align*}
    t_{\mathrm{press}} &= f_{\mathrm{press}}\, t_{\mathrm{cyc}} \\
    t_{\mathrm{feed}}  &= \tfrac{1}{2}(1 - f_{\mathrm{press}})\, t_{\mathrm{cyc}} \\
    t_{\mathrm{blow}}  &= t_{\mathrm{purge}}
                        = \tfrac{1}{4}(1 - f_{\mathrm{press}})\, t_{\mathrm{cyc}}
\end{align*}
\]


###### Step 1 – Pressurisation {#step-1-pressurisation}

The bed is pressurised from $P_{\mathrm{des}}$ to $P_{\mathrm{ads}}$ using raffinate (product-end) gas. The total moles consumed are


<a id="eq:pressurisation"></a>

\[
\Delta n_{\mathrm{press}}
        = \underbrace{\frac{V_{\mathrm{void}}(P_{\mathrm{ads}}-P_{\mathrm{des}})}{Z\,R\,T_{\mathrm{ads}}}}_{\text{void filling}}
        + \underbrace{f_{\mathrm{press}} \sum_i \Delta q_i\, M_{\mathrm{ads}}}_{\text{adsorbent loading}}
\]


where $Z$ is the gas compressibility factor at adsorption conditions, derived from the property-package molar density: $Z = P_{\mathrm{ads}} / (\rho_{\mathrm{mol}}\, R\, T_{\mathrm{ads}})$. where $V_{\mathrm{void}} = V_{\mathrm{bed}}\,\varepsilon$ is the void volume of the bed (m$^3$) and $\varepsilon$ is the void fraction. The bed volume is


<a id="eq:bed_volume"></a>

\[
V_{\mathrm{bed}} = \frac{M_{\mathrm{ads}}}{\rho_{\mathrm{bulk}}(1-\varepsilon)}
\]


No product is withdrawn during this step.

###### Step 2 – Feed / Adsorption {#step-2-feed-adsorption}

Feed gas flows into the bed at $P_{\mathrm{ads}}$. The heavy (more-adsorbed) component loads the adsorbent and the light product exits as raffinate. Moles adsorbed per component during this step:


<a id="eq:adsorption_step"></a>

\[
\Delta n_{i}^{\mathrm{feed}} = \frac{1-f_{\mathrm{press}}}{2}
                                   \Delta q_i\, M_{\mathrm{ads}}
\]


The per-component raffinate from this step is


<a id="eq:raff_step"></a>

\[
\Delta n_{i}^{\mathrm{raff}} = \max\!\left(0,\;
        \dot{n}_{i}^{\mathrm{feed}}\, t_{\mathrm{feed}}
        - \Delta n_{i}^{\mathrm{feed}}\right)
\]


###### Step 3 – Blowdown {#step-3-blowdown}

Pressure drops from $P_{\mathrm{ads}}$ to $P_{\mathrm{des}}$ co-currently. Gas released comprises void-space gas and desorbed adsorbate. Half of the working capacity is attributed to this step:


<a id="eq:blowdown_void"></a><a id="eq:blowdown_des"></a>

\[
\begin{align}
    \Delta n_{\mathrm{void}}^{\mathrm{blow}}
        &= \frac{V_{\mathrm{void}}(P_{\mathrm{ads}}-P_{\mathrm{des}})}{Z\,R\,T_{\mathrm{des}}}
    \\[4pt]
    \Delta n_{i}^{\mathrm{des,blow}}
        &= \tfrac{1}{2}\,\Delta q_i\, M_{\mathrm{ads}}
\end{align}
\]


The blowdown effluent is added to the desorbate stream.

###### Step 4 – Purge {#step-4-purge}

A fraction $\phi_{\mathrm{purge}}$ of the net raffinate flow is routed counter-currently through the bed at $P_{\mathrm{des}}$ to strip residual adsorbate. The remaining half of the working capacity is desorbed during this step. The purge exhaust (purge gas in $+$ desorbed gas) forms part of the desorbate stream.

###### Cycle-Averaged Mass Balance

The cycle-averaged molar flows (mol s$^{-1}$) returned to the DWSIM streams are:


<a id="eq:des_avg"></a><a id="eq:raff_avg"></a>

\[
\begin{align}
    \dot{n}_{i}^{\mathrm{des}} &=
        \frac{\Delta n_{i}^{\mathrm{blow}} + \Delta n_{i}^{\mathrm{purge,out}}}
             {t_{\mathrm{cyc}}}
    \\[4pt]
    \dot{n}_{i}^{\mathrm{raff}} &=
        \max\!\left(0,\;\frac{\Delta n_{i}^{\mathrm{raff}}}{t_{\mathrm{cyc}}}
        - \frac{\Delta n_{\mathrm{press}}}{t_{\mathrm{cyc}}}\,y_i
        - \phi_{\mathrm{purge}}\,\dot{n}_{i}^{\mathrm{raff}}\right)
\end{align}
\]


where the pressurisation penalty is distributed to the raffinate stream in proportion to feed mole fractions.

##### Separation Performance Indicators

The model reports the following key performance indicators for the most-adsorbed (*key*) component $k$:



<a id="eq:recovery"></a><a id="eq:purity_des"></a><a id="eq:purity_raff"></a>

\[
\begin{align}
    \text{Recovery} &= \frac{\dot{n}_k^{\mathrm{des}}}
                            {\dot{n}_k^{\mathrm{feed}}}
    \\[6pt]
    \text{Purity (desorbate)} &= \frac{\dot{n}_k^{\mathrm{des}}}
                                      {\displaystyle\sum_i \dot{n}_i^{\mathrm{des}}}
    \\[6pt]
    \text{Purity (raffinate)} &= \frac{\dot{n}_k^{\mathrm{raff}}}
                                      {\displaystyle\sum_i \dot{n}_i^{\mathrm{raff}}}
\end{align}
\]


##### Pressure Drop (Ergun Equation) {#sec:pressure_drop}

When the bed geometry parameters (vessel diameter, bed length, particle diameter, particle sphericity) are specified, the model computes the pressure drop across the packed bed using the **Ergun equation** :


<a id="eq:ergun"></a>

\[
\frac{\Delta P}{L}
    = \frac{150\,\mu\,u\,(1-\varepsilon)^2}
           {\varphi^2\,d_p^2\,\varepsilon^3}
    + \frac{1.75\,\rho\,u^2\,(1-\varepsilon)}
           {\varphi\,d_p\,\varepsilon^3}
\]


where $L$ (m) is the bed length, $\mu$ (Pa s) is the gas dynamic viscosity, $u$ (m s$^{-1}$) is the superficial gas velocity, $\varepsilon$ is the bed void fraction, $\varphi$ is the particle sphericity, $d_p$ (m) is the mean particle diameter, and $\rho$ (kg m$^{-3}$) is the gas density.

The first term represents viscous (Blake–Kozeny) losses and the second term represents inertial (Burke–Plummer) losses. The total pressure drop is


<a id="eq:dp_total"></a>

\[
\Delta P = \frac{\Delta P}{L}\, L
\]


The superficial velocity is computed *per bed* from the total feed flow divided by the number of parallel beds $N_{\mathrm{beds}}$:


<a id="eq:superficial_vel"></a>

\[
u = \frac{Q_{\mathrm{vol}}}{A_{\mathrm{cross}}}
    \;,\qquad
    Q_{\mathrm{vol}} = \frac{\dot{m}_{\mathrm{total}}}
                            {\rho\, N_{\mathrm{beds}}}
    \;,\qquad
    A_{\mathrm{cross}} = \frac{\pi}{4}\,D^2
\]


where $D$ (m) is the vessel diameter, $\dot{m}_{\mathrm{total}}$ (kg s$^{-1}$) is the total mass flow rate, and $\rho$ (kg m$^{-3}$) is the gas density obtained from the property package (vapor phase).

The outlet (raffinate) pressure is set to $P_{\mathrm{raff}} = P_{\mathrm{ads}} - \Delta P$, subject to a minimum floor of 1 kPa. If any geometry parameter is zero or unset, the pressure drop is taken as zero and the outlet pressure equals the inlet pressure.

###### Bed Geometry Parameters







| **Parameter**       | **Symbol**  | **SI Unit** | **Default** |
|:--------------------|:------------|:------------|:------------|
| Vessel diameter     | $D$       | m           | 1.0         |
| Bed length          | $L$       | m           | 3.0         |
| Particle diameter   | $d_p$     | m           | 0.002       |
| Particle sphericity | $\varphi$ | –           | 1.0         |



##### Heat of Adsorption Estimate

An approximate heat duty associated with adsorption is computed from the isosteric heats and the cycle-averaged adsorbed flows:


<a id="eq:heat_ads"></a>

\[
\dot{Q}_{\mathrm{ads}} = \sum_i \dot{n}_i^{\mathrm{ads}}\,\Delta H_i
\]


This estimate does not account for the sensible heat required to heat the bed during TSA regeneration. For rigorous energy balances, a detailed dynamic model is recommended.

##### Model Parameters {#sec:parameters}

###### Bed Parameters







| **Parameter**  | **Symbol**               | **SI Unit**   | **Default** |
|:---------------|:-------------------------|:--------------|:------------|
| Adsorbent mass | $M_{\mathrm{ads}}$     | kg            | 1000        |
| Void fraction  | $\varepsilon$          | –             | 0.40        |
| Bulk density   | $\rho_{\mathrm{bulk}}$ | kg m$^{-3}$ | 700         |
| Number of beds | $N_{\mathrm{beds}}$    | –             | 2           |



###### Cycle Parameters







| **Parameter** | **Symbol** | **Unit** | **Default** |
|:---|:---|:---|:---|
| Cycle time | $t_{\mathrm{cyc}}$ | s | 600 |
| Purge fraction | $\phi_{\mathrm{purge}}$ | – | 0.10 |
| Pressurisation time fraction | $f_{\mathrm{press}}$ | – | 0.10 |
| Adsorption pressure | $P_{\mathrm{ads}}$ | Pa | (from feed stream) |
| Desorption pressure | $P_{\mathrm{des}}$ | Pa | 20 000 |
| Adsorption temperature | $T_{\mathrm{ads}}$ | K | (from feed stream) |
| Desorption temperature | $T_{\mathrm{des}}$ | K | 423.15 |



###### Isotherm Parameters (per component)







| **Parameter** | **Symbol** | **Applicable Model** | **Unit** |  |
|:---|:---|:---|:---|:---|
| Saturation capacity (site 1) | $q_{\mathrm{sat}1}$ | SSL, DSL | mol kg$^{-1}$ |  |
| Pre-exp. affinity (site 1) | $b_{0,1}$ | SSL, DSL | Pa$^{-1}$ |  |
| Isosteric heat (site 1) | $\Delta H_1$ | SSL, DSL | J mol$^{-1}$ |  |
| Saturation capacity (site 2) | $q_{\mathrm{sat}2}$ | DSL only | mol kg$^{-1}$ |  |
| Pre-exp. affinity (site 2) | $b_{0,2}$ | DSL only | Pa$^{-1}$ |  |
| Isosteric heat (site 2) | $\Delta H_2$ | DSL only | J mol$^{-1}$ |  |
| Freundlich pre-factor | $K$ | Freundlich | mol kg$^{-1}$ Pa$^{-1/n}$ |  |
| Freundlich exponent | $n$ | Freundlich | – |  |



##### Built-In Zeolite Presets

Indicative Langmuir and DSL parameters for common zeolite–gas systems are provided as starting-point presets. These values are drawn from published literature and should be replaced by experimental data before engineering calculations are performed.







| **Preset**  | **Pore size**    | **Target separation**                    |
|:------------|:-----------------|:-----------------------------------------|
| Zeolite 3A  | $\approx 3$ Å  | Water removal (unsaturated hydrocarbons) |
| Zeolite 4A  | $\approx 4$ Å  | General drying, CO$_2$ removal         |
| Zeolite 5A  | $\approx 5$ Å  | O$_2$/N$_2$ air separation           |
| Zeolite 13X | $\approx 10$ Å | CO$_2$/CH$_4$ biogas upgrading       |



##### Assumptions and Limitations

1.  **Local equilibrium** – the model assumes instantaneous equilibrium between the gas phase and the adsorbed phase (infinite mass-transfer rate). Real columns exhibit dispersive mass-transfer zones; the shortcut result represents the best achievable performance for a given set of equilibrium data.

2.  **Steady-state cycle average** – the PSA Cycle mode converts a cyclic process to steady-state equivalent flows. Instantaneous concentration profiles within a cycle are not resolved.

3.  **Real gas in void space** – the void-space inventory uses the compressibility factor $Z$ derived from the property-package molar density at adsorption conditions. The same $Z$ is applied at desorption conditions as an approximation.

4.  **Simplified desorption composition** – the desorption partial pressures are approximated using feed mole fractions ([\[eq:des_partial\]](#eq:des_partial)). In reality the desorbate is enriched in the heavy component; a more rigorous treatment requires solving the column material balance iteratively.

5.  **Isothermal operation** – for PSA calculations the bed temperature is held constant. Heat effects due to adsorption and desorption are not fed back into the energy balance; they are reported separately as $\dot{Q}_{\mathrm{ads}}$ ([\[eq:heat_ads\]](#eq:heat_ads)).

6.  **Ergun pressure drop** – the pressure drop is estimated from the Ergun equation ([\[eq:ergun\]](#eq:ergun)) using the gas density and viscosity from the property package. The calculation assumes uniform, isothermal, single-phase gas flow through a homogeneous packed bed.

##### Numerical Solution Procedure

1.  Resolve feed stream conditions: $T$, $P$, $\{y_i\}$, $\dot{n}_{\mathrm{total}}$.

2.  Synchronise the component isotherm data list with the compound list from the feed stream.

3.  Compute partial pressures $p_i = y_i P_{\mathrm{ads}}$.

4.  Evaluate equilibrium loadings at adsorption conditions $q_i^{\mathrm{ads}}$ using the selected isotherm ([\[eq:langmuir_multi\]](#eq:langmuir_multi), [\[eq:DSL_multi\]](#eq:DSL_multi), or [\[eq:freundlich\]](#eq:freundlich)).

5.  Evaluate equilibrium loadings at desorption conditions $q_i^{\mathrm{des}}$.

6.  Compute working capacities $\Delta q_i$ ([\[eq:working_capacity\]](#eq:working_capacity)).

7.  Obtain the gas molar density $\rho_{\mathrm{mol}}$ from the property-package vapor-phase density and the feed mass and molar flows. Derive $Z = P/({\rho_{\mathrm{mol}}\,R\,T})$ for void-space calculations.

8.  *Equilibrium mode:* apply [\[eq:ads_flow\]](#eq:ads_flow)–[\[eq:des_flow\]](#eq:des_flow).\
    *PSA Cycle mode:* execute the four Skarstrom steps and compute cycle-averaged flows ([\[eq:pressurisation\]](#eq:pressurisation)–[\[eq:raff_avg\]](#eq:raff_avg)).

9.  Compute bed pressure drop via the Ergun equation ([\[eq:ergun\]](#eq:ergun)) if geometry parameters are specified.

10. Set outlet stream temperatures, pressures and molar flows. The raffinate pressure is reduced by $\Delta P$.

11. Compute separation performance indicators ([\[eq:recovery\]](#eq:recovery)–[\[eq:purity_raff\]](#eq:purity_raff)) and heat of adsorption estimate ([\[eq:heat_ads\]](#eq:heat_ads)).

No iteration is required for either mode; the calculation is explicit given the isotherm parameters and operating conditions.

##### Typical Usage Workflow

1.  Add the *Zeolite Adsorber* block to the flowsheet and connect the feed, raffinate, and desorbate material streams.

2.  Specify the operating mode (*Equilibrium* or *PSA Cycle*) and isotherm model on the **Parameters** tab.

3.  Select a zeolite preset or enter custom isotherm parameters on the **Isotherm Parameters** tab. If a feed stream is connected, the component table is populated automatically.

4.  Set the adsorption pressure (taken from the feed stream) and the desorption conditions ($P_{\mathrm{des}}$, $T_{\mathrm{des}}$).

5.  For PSA mode, adjust the cycle time, purge fraction, and pressurisation time fraction.

6.  Run the simulation and review the **Results** tab for stream summaries, per-component loadings, and separation performance indicators.

#### Copper Bed Mercury Adsorber

##### Overview

The **Copper Bed Mercury Adsorber** models a fixed-bed guard vessel used to remove elemental mercury () from natural gas, NGL, and LNG process streams. The unit represents a once-through, non-regenerable sorbent bed based on copper sulphide (CuS/Al$_2$O$_3$), metallic copper on activated carbon (Cu/C), or sulphur-impregnated activated carbon (SIAC).

Mercury occurs in natural gas at trace concentrations (typically 0.001–10,000 μg/Nm$^3$) and must be removed to protect aluminium heat exchangers, catalyst beds, and downstream equipment, as well as to comply with product-quality specifications . The primary removal mechanism is irreversible chemisorption:


<a id="eq:hg_cus"></a><a id="eq:hg_amal"></a>

\[
\begin{align}
    \ce{Hg^0 + CuS &-> HgS + Cu}      \\
    \ce{Hg^0 + Cu  &-> Cu\text{--}Hg} \quad \text{(amalgam)}
\end{align}
\]


Because the reaction is essentially irreversible, regeneration is not practised; the bed is replaced when the mercury capacity is exhausted.

Two calculation modes are available:

- **Capacity-Based mode** – a simplified sizing model that uses the vendor-rated maximum mercury capacity $q_{\max}$ to compute the bed lifetime at a given inlet concentration and gas flow rate. Full removal (outlet concentration equal to the breakthrough specification) is assumed until the capacity is exhausted.

- **Wheeler-Jonas mode** – a rigorous breakthrough model based on the Wheeler-Jonas equation , combined with a Langmuir or Freundlich adsorption isotherm. The model predicts the time-varying outlet Hg concentration as a function of bed age, inlet conditions, and mass-transfer kinetics.

##### Stream Topology

The unit operation has the following connection ports:







| **Port**        | **Direction**     | **Description**                   |
|:----------------|:------------------|:----------------------------------|
| Feed Gas In     | Inlet (material)  | Raw gas containing trace mercury  |
| Treated Gas Out | Outlet (material) | Cleaned gas at reduced Hg content |



No second outlet is provided because the sorbent is a consumable; the spent bed is removed from service rather than regenerated.

Mercury is identified in the feed stream by the configurable compound name (default: `Mercury`). If that compound is absent from the component list, the user may instead specify the inlet Hg concentration directly in μg/Nm$^3$.

##### Mercury Concentrations and Unit Conversions {#sec:hg_units}

Natural gas mercury concentrations are most commonly reported at *normal conditions* (0 °C, 101 325 Pa) in units of μg/Nm$^3$. The model uses *actual* conditions (operating $T$, $P$) internally and converts for display.

###### Mole fraction to actual concentration

The mass concentration of mercury at operating conditions is


<a id="eq:c_act"></a>

\[
C_{\mathrm{act}} \; [\mu\text{g/m}^3]
    = y_{\mathrm{Hg}}\, \rho_{\mathrm{mol}}\, M_{\mathrm{Hg}} \times 10^6
\]


where $y_{\mathrm{Hg}}$ is the mercury mole fraction, $\rho_{\mathrm{mol}}$ (mol m$^{-3}$) is the gas molar density from the property package, and $M_{\mathrm{Hg}} = 200.59$ g mol$^{-1}$. The molar density is computed as $\rho_{\mathrm{mol}} = \dot{n}_{\mathrm{total}} / Q$, where $Q$ is the actual volumetric flow obtained from the mass flow and the property-package density.

###### Actual to normal conditions



<a id="eq:c_norm"></a>

\[
C_{\mathrm{Nm}^3} \; [\mu\text{g/Nm}^3]
    = C_{\mathrm{act}} \cdot \frac{\rho_{\mathrm{mol}}^{\mathrm{NTP}}}
                                   {\rho_{\mathrm{mol}}}
\]


where $\rho_{\mathrm{mol}}^{\mathrm{NTP}} = P_{\mathrm{NTP}}/(R\,T_{\mathrm{NTP}})
\approx 44.6$ mol m$^{-3}$ is the ideal-gas molar density at normal conditions ($T_{\mathrm{NTP}} = 273.15$ K, $P_{\mathrm{NTP}} = 101{,}325$ Pa). The inverse conversion is used to bring user-specified inlet concentrations (given at normal conditions) to actual conditions for the mass balance.

##### Isotherm Models {#sec:hg_isotherms}

Both isotherm models use the Hg partial pressure $p_{\mathrm{Hg}} = y_{\mathrm{Hg}}\,P$ as the independent variable. Loadings are expressed in mol kg$^{-1}$ and converted to mg Hg g$^{-1}$ for display via


<a id="eq:loading_conv"></a>

\[
q \; [\text{mg Hg/g}] = q \; [\text{mol/kg}] \times M_{\mathrm{Hg}} \; [\text{g/mol}]
\]


###### Langmuir Isotherm

The single-site Langmuir model with temperature-dependent affinity constant is


<a id="eq:langmuir_hg"></a>

\[
q = \frac{q_{\mathrm{sat}}\, b(T)\, p_{\mathrm{Hg}}}
             {1 + b(T)\, p_{\mathrm{Hg}}}
\]


where $q_{\mathrm{sat}}$ (mol kg$^{-1}$) is the saturation capacity and the temperature-dependent affinity constant follows a van’t Hoff relationship:


<a id="eq:vanthoff_hg"></a>

\[
b(T) = b_0 \exp\!\left(\frac{\Delta H}{R T}\right)
\]


Here $b_0$ (Pa$^{-1}$) is the pre-exponential factor and $\Delta H > 0$ (J mol$^{-1}$) is the isosteric heat of chemisorption (sign convention: positive for exothermic). For copper-sulphide chemisorption, $\Delta H$ is typically in the range 50,000–80,000 J mol$^{-1}$, reflecting a very strong Hg–sulphur bond.

At the trace concentrations found in natural gas ($p_{\mathrm{Hg}} \ll
1/b(T)$), the Langmuir isotherm approaches Henry’s-law behaviour:


<a id="eq:henry_limit"></a>

\[
q \approx q_{\mathrm{sat}}\, b(T)\, p_{\mathrm{Hg}}
    \qquad (b\,p_{\mathrm{Hg}} \ll 1)
\]


Conversely, at elevated concentrations or with strongly chemisorptive sorbents ($b\,p_{\mathrm{Hg}} \gg 1$), the loading approaches $q_{\mathrm{sat}}$, indicating the sorbent is operating at or near saturation.

###### Freundlich Isotherm

The empirical Freundlich model is


<a id="eq:freundlich_hg"></a>

\[
q = K_F\, p_{\mathrm{Hg}}^{1/n_F}
\]


where $K_F$ (mol kg$^{-1}$ Pa$^{-1/n_F}$) is the pre-factor and $n_F > 0$ is the heterogeneity index. For chemisorption at trace concentrations, $n_F > 1$ gives a favourable (concave) isotherm. The current implementation treats $K_F$ and $n_F$ as temperature-independent; users should supply values measured at the design operating temperature.

##### Capacity-Based Mode {#sec:capacity_mode}

In this simplified mode the sorbent is characterised solely by its *maximum working capacity* $q_{\max}$ (mg Hg g$^{-1}$), which is typically obtained from the sorbent vendor’s datasheet or from accelerated laboratory tests.

The total mercury storage capacity of the bed is


<a id="eq:total_cap"></a>

\[
\hat{N} = q_{\max} \; [\text{mg/g}] \times W_s \; [\text{kg}] \times 10^3
    \; [\text{g/kg}]  \quad [\text{mg Hg}]
\]


The actual volumetric gas flow at operating conditions is


<a id="eq:vol_flow"></a>

\[
Q = \frac{\dot{m}_{\mathrm{total}}}{\rho}
\]


where $\dot{m}_{\mathrm{total}}$ (kg s$^{-1}$) is the total mass feed flow and $\rho$ (kg m$^{-3}$) is the gas density from the property package.

The bed lifetime (time from start-up to breakthrough) is


<a id="eq:lifetime_cap"></a>

\[
t_b = \frac{\hat{N} \; [\text{mg Hg}] \times 10^3 \; [\mu\text{g/mg}]}
               {C_{\mathrm{in}} \; [\mu\text{g/m}^3] \times Q \; [\text{m}^3/\text{s}]}
         = \frac{q_{\max}\, W_s \times 10^6}
               {C_{\mathrm{in}}\, Q}
\]


The outlet concentration is set equal to the breakthrough specification $C_b$ (corresponding to a fresh-to-breakthrough average), which represents the design limit for a guard bed in service.

##### Wheeler-Jonas Breakthrough Model {#sec:wheeler_jonas}

The Wheeler-Jonas equation provides a closed-form estimate of the breakthrough time for a fixed-bed adsorber with first-order mass-transfer kinetics and a favourable (concave) isotherm. It is widely used for sizing gas-phase sorbent systems including mercury guard beds in natural gas service .

###### Stoichiometric breakthrough time

The stoichiometric breakthrough time corresponds to the ideal (infinitely sharp) mass-transfer front and equals the ratio of total bed capacity to the Hg mass feed rate:


<a id="eq:t_stoich"></a>

\[
t_s = \frac{q_e \; [\text{mg/g}] \times W_s \; [\text{kg}] \times 10^6}
               {C_{\mathrm{in}} \; [\mu\text{g/m}^3] \times Q \; [\text{m}^3/\text{s}]}
\]


where $q_e$ is the equilibrium loading at the inlet concentration, obtained from Eq. [\[eq:langmuir_hg\]](#eq:langmuir_hg) or Eq. [\[eq:freundlich_hg\]](#eq:freundlich_hg) and converted by Eq. [\[eq:loading_conv\]](#eq:loading_conv). The factor $10^6$ converts kg mg/g $\to$ μg consistently with $C_{\mathrm{in}}$ expressed in μg/m$^3$.

###### Wheeler-Jonas breakthrough time

The mass-transfer zone (MTZ) shifts the breakthrough curve relative to the stoichiometric front. The Wheeler-Jonas corrected breakthrough time is


<a id="eq:wj_time"></a>

\[
t_b = t_s - \frac{1}{k_v} \ln\!\left(\frac{C_{\mathrm{in}}}{C_b} - 1\right)
\]


where $k_v$ (s$^{-1}$) is the overall first-order volumetric mass-transfer coefficient and $C_b$ (μg/m$^3$) is the breakthrough concentration (outlet specification at actual conditions).

The second term in Eq. [\[eq:wj_time\]](#eq:wj_time) is the mass-transfer correction $\delta t = k_v^{-1} \ln(C_{\mathrm{in}}/C_b - 1)$, which is negative when $C_b < C_{\mathrm{in}}/2$ (typical of stringent specifications), meaning that real beds break through *earlier* than the stoichiometric prediction. Equations [\[eq:t_stoich\]](#eq:t_stoich) and [\[eq:wj_time\]](#eq:wj_time) are valid provided $C_b < C_{\mathrm{in}}$.

###### Breakthrough concentration profile

At any elapsed service time $t$ (s) the outlet concentration is given by the logistic (S-shaped) breakthrough curve


<a id="eq:wj_profile"></a>

\[
C_{\mathrm{out}}(t) = \frac{C_{\mathrm{in}}}
                              {1 + \exp\!\left[k_v \left(t_b - t\right)\right]}
\]


This expression reproduces the expected behaviour: $C_{\mathrm{out}} \to 0$ for $t \ll t_b$ (fresh bed) and $C_{\mathrm{out}} \to C_{\mathrm{in}}$ for $t \gg t_b$ (exhausted bed). The steepness of the S-curve is controlled by $k_v$; as $k_v \to \infty$ the profile approaches the sharp-front limit.

###### Bed saturation fraction

The fraction of the bed capacity consumed at age $t$ is approximated as


<a id="eq:saturation"></a>

\[
f_{\mathrm{sat}} = \min\!\left(1,\; \frac{t}{t_b}\right)
\]


which is reported for monitoring purposes and is used to set the *Bed Saturation* output property.

##### Mercury Mass Balance {#sec:hg_balance}

The molar flow of mercury removed is computed from the actual concentration difference and the volumetric gas flow:


<a id="eq:hg_removed_mass"></a><a id="eq:hg_removed_mol"></a>

\[
\begin{align}
    \dot{m}_{\mathrm{Hg}}^{\mathrm{removed}} \; [\mu\text{g/s}]
        &= \left(C_{\mathrm{in}} - C_{\mathrm{out}}\right) \times Q
    \\[6pt]
    \dot{n}_{\mathrm{Hg}}^{\mathrm{removed}} \; [\text{mol/s}]
        &= \frac{\dot{m}_{\mathrm{Hg}}^{\mathrm{removed}}}{M_{\mathrm{Hg}} \times 10^6}
\end{align}
\]


where the factor $10^6$ converts μg mol$^{-1}$ to g mol$^{-1}$. The outlet Mercury molar flow is


<a id="eq:hg_out"></a>

\[
\dot{n}_{\mathrm{Hg}}^{\mathrm{out}} =
        \max\!\left(0,\; \dot{n}_{\mathrm{Hg}}^{\mathrm{feed}}
        - \dot{n}_{\mathrm{Hg}}^{\mathrm{removed}}\right)
\]


All other components pass through the bed unchanged.

###### Removal efficiency



<a id="eq:removal_eff"></a>

\[
\eta = \frac{C_{\mathrm{in}} - C_{\mathrm{out}}}{C_{\mathrm{in}}}
\]


##### Pressure Drop (Ergun Equation) {#sec:hg_pressure_drop}

When bed geometry parameters (vessel diameter, bed length, particle diameter, particle sphericity) are specified, the pressure drop across the packed bed is computed using the **Ergun equation** :


<a id="eq:ergun_hg"></a>

\[
\frac{\Delta P}{L}
    = \frac{150\,\mu\,u\,(1-\varepsilon)^2}
           {\varphi^2\,d_p^2\,\varepsilon^3}
    + \frac{1.75\,\rho\,u^2\,(1-\varepsilon)}
           {\varphi\,d_p\,\varepsilon^3}
\]


where $L$ (m) is the bed length, $\mu$ (Pa s) is the gas dynamic viscosity, $u$ (m s$^{-1}$) is the superficial gas velocity, $\varepsilon$ is the bed void fraction, $\varphi$ is the particle sphericity, $d_p$ (m) is the mean particle diameter, and $\rho$ (kg m$^{-3}$) is the gas density.

The first term accounts for viscous (Blake–Kozeny) losses and the second term for inertial (Burke–Plummer) losses. The total pressure drop is $\Delta P = (\Delta P / L)\, L$.

The superficial velocity is computed from the mass flow and the gas density obtained from the property package:


<a id="eq:vel_density_hg"></a>

\[
u = \frac{Q}{A}
    \;,\qquad
    Q = \frac{\dot{m}_{\mathrm{total}}}{\rho}
    \;,\qquad
    A = \frac{\pi}{4}\,D^2
\]


where $D$ (m) is the vessel diameter, $\dot{m}_{\mathrm{total}}$ (kg s$^{-1}$) is the total mass flow, and $\rho$ (kg m$^{-3}$) is the gas density from the property package (vapor phase).

The outlet pressure is set to $P_{\mathrm{out}} = P_{\mathrm{in}} - \Delta P$, subject to a minimum floor of 1 kPa. If any geometry parameter is zero or unset, the pressure drop defaults to zero.

###### Bed Geometry Parameters







| **Parameter**       | **Symbol**  | **SI Unit** | **Default** |
|:--------------------|:------------|:------------|:------------|
| Vessel diameter     | $D$       | m           | 1.0         |
| Bed length          | $L$       | m           | 3.0         |
| Particle diameter   | $d_p$     | m           | 0.002       |
| Particle sphericity | $\varphi$ | –           | 1.0         |



##### Sorbent Presets {#sec:hg_presets}

Representative parameters for three common sorbent types are provided as starting-point presets. Values are drawn from published literature and from commercially available guard-bed datasheets. **These parameters should always be replaced with site-specific experimental data before engineering calculations are performed.**







| **Preset** | **Mechanism** | $q_{\max}$ \[mg/g\] | $k_v$ \[s$^{-1}$\] |
|:---|:---|:---|:---|
| CuS / Al$_2$O$_3$ | Hg + CuS $\to$ HgS + Cu | 100 | 0.002 |
| Cu / Activated Carbon | Amalgam + sulphide | 200 | 0.003 |
| Sulphur-Impregnated Carbon | Hg + S $\to$ HgS | 150 | 0.003 |



The Langmuir isotherm parameters for each preset correspond to near-saturation behaviour at typical natural gas conditions, reflecting the essentially irreversible chemisorption mechanism: $\Delta H \approx 55\text{--}60$ kJ/mol with very large $b(T)$ values (bed operates in the plateau region of the isotherm).

##### Model Parameters {#sec:hg_parameters}

###### Bed Parameters







| **Parameter** | **Symbol**      | **SI Unit**   | **Default** |
|:--------------|:----------------|:--------------|:------------|
| Sorbent mass  | $W_s$         | kg            | 1000        |
| Bulk density  | $\rho_b$      | kg m$^{-3}$ | 700         |
| Void fraction | $\varepsilon$ | –             | 0.40        |



###### Capacity-Based Parameters







| **Parameter**    | **Symbol**   | **Unit** | **Default** |
|:-----------------|:-------------|:---------|:------------|
| Max. Hg capacity | $q_{\max}$ | mg Hg/g  | 100         |



###### Isotherm Parameters (Wheeler-Jonas mode)







| **Parameter** | **Symbol** | **Model** | **Unit** | **Default** |
|:---|:---|:---|:---|:---|
| Saturation capacity | $q_{\mathrm{sat}}$ | Langmuir | mol kg$^{-1}$ | 0.5 |
| Pre-exp. affinity constant | $b_0$ | Langmuir | Pa$^{-1}$ | $2\times 10^{-6}$ |
| Isosteric heat | $\Delta H$ | Langmuir | J mol$^{-1}$ | 60,000 |
| Freundlich pre-factor | $K_F$ | Freundlich | mol kg$^{-1}$ Pa$^{-1/n_F}$ | 10 |
| Freundlich exponent | $n_F$ | Freundlich | – | 3 |



###### Wheeler-Jonas and Operating Parameters







| **Parameter** | **Symbol** | **Unit** | **Default** |
|:---|:---|:---|:---|
| Mass-transfer coefficient | $k_v$ | s$^{-1}$ | 0.002 |
| Bed age | $t$ | h | 0 |
| Breakthrough spec. | $C_b$ | μg/Nm$^3$ | 1.0 |
| Inlet Hg concentration$^\dagger$ | $C_{\mathrm{in}}$ | μg/Nm$^3$ | 100 |
| Operating temperature | $T$ | K | (from feed stream) |



$^\dagger$Used only when the Mercury compound is absent from the feed stream; otherwise the concentration is derived from the mole fraction via Eq. [\[eq:c_act\]](#eq:c_act).

##### Assumptions and Limitations

1.  **Irreversible chemisorption** – the sorbent is modelled as non-regenerable. The capacity-based mode assumes that the sorbent is fully effective (100 % removal) until breakthrough; deactivation kinetics or competing reactions are not modelled.

2.  **Elemental mercury only** – only elemental Hg$^0$ is considered. Organomercury compounds (e.g. dimethylmercury) and ionic species (Hg$^{2+}$) have different adsorption behaviour and require separate treatment.

3.  **Local equilibrium in Wheeler-Jonas mode** – the model assumes that the axial dispersion and external film resistance are lumped into the single parameter $k_v$. Rigorous mass-transfer analysis (e.g. linear driving force or pore-diffusion models) is beyond the scope of this unit operation.

4.  **Ergun pressure drop** – the pressure drop is estimated from the Ergun equation ([\[eq:ergun_hg\]](#eq:ergun_hg)) using the gas density and viscosity from the property package. The calculation assumes uniform, isothermal, single-phase gas flow through a homogeneous packed bed.

5.  **Isothermal operation** – the outlet gas temperature is set equal to the inlet value. The heat released by chemisorption ($\Delta H \approx 50\text{--}80$ kJ/mol) is not fed back into the energy balance; it is reported implicitly through the removed molar flow and the isosteric heat parameter.

6.  **Real-gas properties** – all volumetric flows, gas densities, and mercury concentration conversions use the gas molar density from the property package, which accounts for real-gas compressibility effects. Normal-condition quantities (Nm$^3$) use the ideal-gas molar density at NTP as per the standard definition.

7.  **No competitive adsorption** – the isotherm parameters describe the Hg–sorbent interaction only. Competitive adsorption by H$_2$S, COS, or heavy hydrocarbons (which can reduce the effective Hg capacity) is not modelled.

8.  **Uniform concentration profile** – the Wheeler-Jonas equation assumes an axially uniform initial Hg loading. It does not resolve the spatial concentration profile within the bed.

##### Numerical Solution Procedure

1.  Resolve feed stream conditions: $T$, $P$, $\{y_i\}$, $\dot{n}_{\mathrm{total}}$.

2.  Determine the inlet Hg concentration $C_{\mathrm{in,act}}$ (μg/m$^3$) from the stream mole fraction (Eq. [\[eq:c_act\]](#eq:c_act)) or from the user-specified value converted by the inverse of Eq. [\[eq:c_norm\]](#eq:c_norm).

3.  Compute the actual volumetric flow $Q$ from the mass flow and property-package density (Eq. [\[eq:vol_flow\]](#eq:vol_flow)), and derive the gas molar density $\rho_{\mathrm{mol}} = \dot{n}_{\mathrm{total}} / Q$.

4.  Convert the breakthrough specification $C_b$ from μg/Nm$^3$ to actual conditions using Eq. [\[eq:c_norm\]](#eq:c_norm).

5.  *Capacity-Based mode:*

    1.  Compute total bed capacity $\hat{N}$ (Eq. [\[eq:total_cap\]](#eq:total_cap)).

    2.  Compute bed lifetime $t_b$ (Eq. [\[eq:lifetime_cap\]](#eq:lifetime_cap)).

    3.  Set $C_{\mathrm{out}} = C_b$ (design assumption).

6.  *Wheeler-Jonas mode:*

    1.  Compute $p_{\mathrm{Hg}} = y_{\mathrm{Hg}} P$ and evaluate the equilibrium loading $q_e$ from the selected isotherm (Eqs. [\[eq:langmuir_hg\]](#eq:langmuir_hg) or [\[eq:freundlich_hg\]](#eq:freundlich_hg)).

    2.  Convert $q_e$ to mg/g (Eq. [\[eq:loading_conv\]](#eq:loading_conv)).

    3.  Compute the stoichiometric breakthrough time $t_s$ (Eq. [\[eq:t_stoich\]](#eq:t_stoich)) and the Wheeler-Jonas breakthrough time $t_b$ (Eq. [\[eq:wj_time\]](#eq:wj_time)).

    4.  Compute the outlet concentration at the specified bed age (Eq. [\[eq:wj_profile\]](#eq:wj_profile)).

    5.  Compute the bed saturation fraction (Eq. [\[eq:saturation\]](#eq:saturation)).

7.  Compute Hg removal efficiency $\eta$ (Eq. [\[eq:removal_eff\]](#eq:removal_eff)) and the removed molar flow (Eqs. [\[eq:hg_removed_mass\]](#eq:hg_removed_mass) and [\[eq:hg_removed_mol\]](#eq:hg_removed_mol)).

8.  Compute bed pressure drop via the Ergun equation ([\[eq:ergun_hg\]](#eq:ergun_hg)) if geometry parameters are specified.

9.  Set the outlet stream: all non-Hg components unchanged; Mercury molar flow set to $\dot{n}_{\mathrm{Hg}}^{\mathrm{out}}$ (Eq. [\[eq:hg_out\]](#eq:hg_out)); $T$ equal to inlet value; $P_{\mathrm{out}} = P_{\mathrm{in}} - \Delta P$.

The calculation is explicit (no iteration required) in both modes.

##### Typical Usage Workflow

1.  Add the *Copper Bed Hg Adsorber* block to the flowsheet and connect the feed and treated-gas material streams.

2.  On the **Parameters** tab, select the operating mode (*CapacityBased* or *WheelerJonas*).

3.  Choose a sorbent preset or enter custom parameters. If the gas stream includes a Mercury compound, ensure the **Hg Compound Name** field matches the DWSIM compound name and set **Hg Concentration Source** to *From Stream*. Otherwise select *Specified* and enter the inlet concentration in μg/Nm$^3$.

4.  Enter the bed geometry (mass, bulk density, void fraction) and the breakthrough specification $C_b$.

5.  For *WheelerJonas* mode, enter the isotherm parameters (use a preset as a starting point) and the mass-transfer coefficient $k_v$. To obtain the outlet concentration at a given bed age, set the **Bed Age** field to the elapsed service hours.

6.  Run the simulation and review the **Results** tab for inlet and outlet Hg concentrations, removal efficiency, equilibrium capacity, total bed capacity, and bed lifetime.

7.  For design studies, use the DWSIM *Sensitivity* or *Optimizer* tool to assess how bed lifetime varies with sorbent mass, inlet concentration, or breakthrough specification.

##### Worked Example

A natural gas stream at 50 bar and 40 °C with an inlet mercury concentration of 100 μg/Nm$^3$ flows at 500 Nm$^3$/h through a CuS guard bed loaded with 500 kg of sorbent ($q_{\max} = 100$ mg/g).

The actual volumetric flow (assuming $Z \approx 0.9$ from the property package at 50 bar, 313 K) is


\[
Q = \frac{500\,\text{Nm}^3/\text{h} \times \tfrac{1}{3600}\,\text{h/s}
              \times 313.15\,\text{K}}
             {273.15\,\text{K}} \times \frac{101\,325\,\text{Pa}}{50 \times 10^5\,\text{Pa}}
             \times Z
      \approx 2.91 \times 10^{-3}\,\text{m}^3/\text{s}
\]


(In practice, the model obtains $Q$ directly from the stream mass flow and property-package density; the calculation above is for illustration.)

The actual inlet concentration uses the property-package molar density $\rho_{\mathrm{mol}} = P/(ZRT) \approx 2186$ mol/m$^3$:


\[
C_{\mathrm{in,act}} = 100 \times \frac{\rho_{\mathrm{mol}}}{\rho_{\mathrm{mol}}^{\mathrm{NTP}}}
      \approx 100 \times \frac{2186}{44.6}
      \approx 4899\,\mu\text{g/m}^3
\]


From Eq. [\[eq:lifetime_cap\]](#eq:lifetime_cap) the bed lifetime in Capacity-Based mode is


\[
t_b = \frac{100 \times 500 \times 10^6}{4899 \times 2.91 \times 10^{-3}}
        \approx 3.51 \times 10^9\,\text{s}
        \approx 40{,}600\,\text{days}
\]


This unrealistically long lifetime reveals that for this low flow rate the bottleneck is not volumetric capacity but rather the mass of mercury accumulated. Reducing the bed mass to 10 kg gives a more typical result of $\approx 828$ days.

#### Pipe Network

##### Overview

The **Pipe Network** unit operation performs a rigorous steady-state simulation of fluid flow, pressure distribution, and heat transfer in arbitrarily connected piping systems. The model resolves the simultaneous mass, momentum, and energy balances for all segments and junction nodes in the network, supporting single-phase and two-phase (gas–liquid) flows with full thermodynamic property integration.

The network is built by placing and connecting a set of *network objects*—pipes, nodes, pumps, compressors, valves, separators, sources, and sinks—on a graphical canvas. A nonlinear equation solver then determines the mass flow rates, pressures, and temperatures throughout the network that satisfy all governing balances simultaneously.

##### Network Objects {#sec:network_objects}







| **Object** | **Inlets / Outlets** | **Role** |
|:---|:---|:---|
| Source | 0 in / 1 out | Boundary-condition inlet |
| Sink | 1 in / 0 out | Boundary-condition outlet |
| Node | $\le10$ in / $\le10$ out | Flow-splitting/mixing junction |
| Pipe | 1 in / 1 out | Pressure-drop and heat-transfer segment |
| Pump | 1 in / 1 out | Liquid pressure booster |
| Compressor | 1 in / 1 out | Gas pressure booster |
| Valve | 1 in / 1 out | Throttling or control element |
| Separator | 1 in / 2 out | Gas–liquid flash separator |
| Bridge | 1 in / 1 out | Non-mixing bypass connection |



###### Source and Sink

Sources and sinks impose boundary conditions on the network. Each can be specified independently in seven modes:







| **Specification mode** | **Fixed quantities** |
|:-----------------------|:---------------------|
| Pressure only          | $P$                |
| Mass flow only         | $\dot{m}$          |
| Molar flow only        | $\dot{n}$          |
| Volumetric flow only   | $\dot{V}$          |
| Pressure & mass flow   | $P$, $\dot{m}$   |
| Pressure & molar flow  | $P$, $\dot{n}$   |
| Pressure & vol. flow   | $P$, $\dot{V}$   |
| None (fully free)      | —                    |



###### Node

Junction nodes mix or split streams. The model enforces per-node overall mass, pressure, and energy balances (see [2.31.3](#sec:node_balances)). An optional *rigorous heat balance* mode uses the full stream enthalpy from the thermodynamic property package rather than an ideal mixing approximation.

###### Pipe

The pipe segment calculates the pressure drop and thermal profile for a single pipe section given its geometry, orientation, and selected two-phase flow correlation (see [2.31.4](#sec:pressure_drop)). A per-segment equilibrium flash can be performed at configurable intervals to update stream thermodynamic properties along the pipe length.

###### Pump, Compressor, and Valve

These objects wrap the corresponding DWSIM base unit operations in $\Delta P$ calculation mode, allowing them to be embedded directly in the network without separate flowsheet connections.

###### Separator

Performs an adiabatic flash split. The vapour outlet supplies the gas phase stream and the liquid outlet supplies the liquid phase stream to the downstream network.

##### Node Balance Equations {#sec:node_balances}

For a node $k$ with $n_{\mathrm{in}}$ inlets and $n_{\mathrm{out}}$ outlets, three dimensionless residuals are formed and minimised by the network solver.

###### Mass Balance



<a id="eq:mass_balance"></a>

\[
r_{m,k} = \frac{\displaystyle\sum_{i \in \mathrm{in}} \dot{m}_i
                  - \displaystyle\sum_{j \in \mathrm{out}} \dot{m}_j}
                   {\dot{m}_{\mathrm{total}}}
\]


###### Pressure Balance

All streams leaving a node share the same node pressure $P_k$. Streams arriving at the node are assumed to match $P_k$ after pressure-drop elements upstream. The residual is defined as



<a id="eq:pressure_balance"></a>

\[
r_{P,k} = \frac{1}{n_{\mathrm{in}}}
               \sum_{i \in \mathrm{in}} \left(\frac{P_i}{P_k}\right)^{\!2}
             - \frac{1}{n_{\mathrm{out}}}
               \sum_{j \in \mathrm{out}} \left(\frac{P_j}{P_k}\right)^{\!2}
\]


###### Energy Balance



<a id="eq:energy_balance"></a>

\[
r_{E,k} = \frac{\displaystyle\sum_{i \in \mathrm{in}} \dot{m}_i h_i
                  - \displaystyle\sum_{j \in \mathrm{out}} \dot{m}_j h_j}
                   {\dot{H}_{\mathrm{total}}}
\]


where $h_i$ is the specific enthalpy of stream $i$ and $\dot{H}_{\mathrm{total}}$ is a reference enthalpy scale for normalisation.

##### Pipe Pressure-Drop Models {#sec:pressure_drop}

The user selects a pressure-drop correlation independently for each pipe segment. Three two-phase correlations are available, plus the underlying single-phase Darcy–Weisbach equation.

###### Single-Phase Flow (Darcy–Weisbach)

For a pipe of length $L$, internal diameter $D$, and friction factor $f$ carrying a fluid of density $\rho$ at mean velocity $u$:



<a id="eq:dw"></a>

\[
\Delta P = f \frac{L}{D} \frac{\rho u^2}{2} + \rho g L \sin\theta
\]


where $\theta$ is the pipe inclination angle from the horizontal and $g$ is the gravitational acceleration. The Fanning friction factor $f$ is evaluated from the **Colebrook–White** implicit equation :



<a id="eq:colebrook"></a>

\[
\frac{1}{\sqrt{f}} = -2\log_{10}\!\left(
        \frac{\varepsilon}{3.7\,D} + \frac{2.51}{Re\sqrt{f}}
    \right)
\]


where $\varepsilon$ is the pipe roughness and $Re = \rho u D / \mu$ is the Reynolds number. Equation [\[eq:colebrook\]](#eq:colebrook) is solved iteratively (or via the explicit Swamee–Jain approximation for initialisation).

###### Beggs and Brill (1973)

The Beggs–Brill correlation is the default method for two-phase gas–liquid flow. It predicts the in-situ liquid holdup $H_L$ and a two-phase friction multiplier $\phi_{tp}$ from the mixture Froude number $Fr_m$, input liquid volume fraction $\lambda_L$, and velocity numbers $N_{vL}$, $N_{vG}$.

The total pressure gradient is decomposed as



<a id="eq:bb_gradient"></a>

\[
\left.\frac{dP}{dz}\right|_{\mathrm{total}} =
        \left.\frac{dP}{dz}\right|_{\mathrm{fric}}
      + \left.\frac{dP}{dz}\right|_{\mathrm{el}}
      + \left.\frac{dP}{dz}\right|_{\mathrm{acc}}
\]


The friction term uses the mixture density $\rho_m$ and the two-phase friction factor $f_{tp}$:


<a id="eq:bb_fric"></a>

\[
\left.\frac{dP}{dz}\right|_{\mathrm{fric}}
        = f_{tp}\,\frac{\rho_m u_m^2}{2D}
\]


The elevation (hydrostatic) term uses the in-situ average density:


<a id="eq:bb_el"></a>

\[
\left.\frac{dP}{dz}\right|_{\mathrm{el}}
        = \bar{\rho}\,g\sin\theta,
    \qquad
    \bar{\rho} = \rho_L H_L + \rho_G (1 - H_L)
\]


The flow-pattern map identifies four regimes—segregated, intermittent, distributed, and transition—and the holdup correlation is applied per regime with an inclination correction factor $\psi(\theta, H_L)$. The friction factor is corrected by an empirical multiplier $e^S$ that depends on $\lambda_L / H_L^2$:


<a id="eq:bb_ftwo"></a>

\[
f_{tp} = f_{ns}\,e^S
\]


where $f_{ns}$ is the no-slip friction factor evaluated at the mixture Reynolds number.

###### Lockhart and Martinelli (1949)

The Lockhart–Martinelli correlation relates the two-phase pressure gradient to the single-phase liquid gradient via the two-phase multiplier $\phi_L^2$:



<a id="eq:lm_multiplier"></a>

\[
\left.\frac{dP}{dz}\right|_{tp}
        = \phi_L^2 \left.\frac{dP}{dz}\right|_{L}
\]


The multiplier is correlated against the Martinelli parameter $X$:


<a id="eq:martinelli_X"></a>

\[
X = \sqrt{\frac{(dP/dz)_L}{(dP/dz)_G}}
\]


The Chisholm  parameterisation is used for $\phi_L^2$:


<a id="eq:chisholm"></a>

\[
\phi_L^2 = 1 + \frac{C}{X} + \frac{1}{X^2}
\]


where the constant $C$ depends on whether each phase is in laminar ($C$ = 5 or 10) or turbulent ($C$ = 12 or 20) flow.

###### Petalas and Aziz (2000)

The Petalas–Aziz model is a mechanistic unified approach that uses a comprehensive flow-pattern classification and separate closure relationships for each regime, including stratified, annular-mist, slug, and dispersed bubble flow. It is recommended for high-pressure and high-GOR applications where empirical correlations may be less reliable.

##### Thermal Model {#sec:thermal}

The temperature profile along each pipe segment is determined from the steady-state energy balance:



<a id="eq:energy_pipe"></a>

\[
\dot{m}\,\frac{dh}{dz} = q(z) - \dot{m}\,g\sin\theta
\]


where $q(z) = U_o \pi D_o [T_{\mathrm{amb}}(z) - T(z)]$ is the heat flux per unit length, $U_o$ is the overall heat-transfer coefficient based on the outer diameter $D_o$, and $T_{\mathrm{amb}}(z)$ is the ambient temperature.

An optional **Joule-Thomson correction** accounts for the isenthalpic temperature change that accompanies pressure drop in compressible fluids. The Joule-Thomson coefficient is evaluated from the equation of state via



<a id="eq:jt"></a>

\[
\mu_{JT} = \left(\frac{\partial T}{\partial P}\right)_h
             = -\frac{1}{\dot{m}\,c_P}\left(\frac{\partial H}{\partial P}\right)_T
\]


For emulsified oil-water flows, an optional **emulsion viscosity correction** (Yoshida et al.) adjusts the mixture viscosity to account for the droplet-induced viscosity enhancement, which can significantly affect the friction pressure drop in oil-continuous emulsions.

##### Network Solver {#sec:solver}

###### Problem Formulation

The network is cast as a nonlinear optimisation problem. The decision variables $\mathbf{x}$ are the unknown source pressures and/or mass flow rates. To ensure physically meaningful (strictly positive) values, variables are log-transformed:



<a id="eq:log_transform"></a>

\[
x_i = \ln\!\left(\frac{v_i}{s_i}\right),
    \qquad v_i = s_i\,e^{x_i}
\]


where $v_i$ is the physical quantity (Pa or kg s$^{-1}$) and $s_i$ is a scale factor ($P_{\max}$ or $\dot{m}_{\max}$).

The objective function to be minimised is the sum of squared node residuals:



<a id="eq:objective"></a>

\[
F(\mathbf{x}) = \sum_k \left(r_{m,k}^2 + r_{P,k}^2 + r_{E,k}^2\right)
\]


For each evaluation of $F$, all network blocks are calculated sequentially— sources first, then pipes, pumps, compressors, valves, and separators—and the node residuals are assembled from the resulting stream conditions.

###### Solver Options

Two numerical methods are available:

- **Simplex** (default) – derivative-free Nelder–Mead simplex method . Robust for moderate-size networks and does not require gradient information.

- **IPOPT** – interior-point optimisation  using numerical gradients computed by finite differences. May converge faster for large or stiff networks.

###### Degrees of Freedom

Before solving, the model checks that the network is properly specified. For each source the number of fixed quantities (pressure, mass flow, or both) determines the degrees of freedom contributed to the system. A network with unconnected sources or insufficient boundary conditions will not converge.

##### Model Parameters {#sec:pn_parameters}

###### Solver Settings







| **Parameter**         | **Symbol**   | **Unit** | **Default** |
|:----------------------|:-------------|:---------|:------------|
| Solver method         | —            | —        | Simplex     |
| Maximum iterations    | $N_{\max}$ | —        | 1000        |
| Convergence tolerance | $\epsilon$ | —        | $10^{-4}$ |



###### Pipe Segment Parameters







| **Parameter** | **Symbol** | **Unit** | **Description** |
|:---|:---|:---|:---|
| Internal diameter | $D$ | m | Pipe bore |
| Length | $L$ | m | Segment length |
| Wall roughness | $\varepsilon$ | m | Absolute roughness |
| Inclination angle | $\theta$ | $^\circ$ | Angle from horizontal |
| Ambient temperature | $T_{\mathrm{amb}}$ | K | Surrounding temperature |
| Overall HTC | $U_o$ | W m$^{-2}$ K$^{-1}$ | Based on outer diameter |
| Pressure-drop model | — | — | BB / LM / PA |
| Joule-Thomson correction | — | — | On / Off |
| Emulsion correction | — | — | On / Off |
| Max. segment iterations | — | — | Per-pipe convergence limit |



###### Abbreviations:

BB = Beggs & Brill; LM = Lockhart & Martinelli; PA = Petalas & Aziz; HTC = heat-transfer coefficient.

##### Results Reported {#sec:pn_results}

For each network object the solver reports:







| **Quantity**             | **Symbol**   | **Unit**           |
|:-------------------------|:-------------|:-------------------|
| Mass flow rate           | $\dot{m}$  | kg s$^{-1}$      |
| Molar flow rate          | $\dot{n}$  | mol s$^{-1}$     |
| Volumetric flow rate     | $\dot{V}$  | m$^3$ s$^{-1}$ |
| Pressure (in/out/avg)    | $P$        | Pa                 |
| Temperature (in/out/avg) | $T$        | K                  |
| Pressure drop            | $\Delta P$ | Pa                 |
| Temperature change       | $\Delta T$ | K                  |



For each node, the solver also reports the dimensionless mass, pressure, and energy balance residuals ($r_m$, $r_P$, $r_E$) as convergence indicators. A converged network solution has all three residuals below the specified tolerance $\epsilon$.

##### Assumptions and Limitations

1.  **Steady state** – the model does not resolve transient behaviour such as surge, water hammer, or slug initiation. All flows and pressures represent time-averaged steady-state conditions.

2.  **One-dimensional flow** – each pipe segment is treated as a 1-D plug-flow element. Radial temperature and concentration gradients within the pipe cross-section are neglected.

3.  **Homogeneous mixture in pipes** – unless a rigorous two-phase correlation is selected, the two phases are treated as a homogeneous mixture for property evaluation. Slip between phases is captured by the holdup correlations in the Beggs–Brill and Lockhart–Martinelli methods.

4.  **No condensation or vaporisation along pipes by default** – phase change within a pipe is accounted for only when the per-segment equilibrium flash option is enabled. Without it, the overall stream composition entering each segment is assumed constant.

5.  **Instantaneous mixing at nodes** – streams mixing at a junction node are assumed to reach thermodynamic equilibrium instantaneously. Phase separation at nodes is not modelled; use a *Separator* object for this purpose.

6.  **Adiabatic pump/compressor/valve by default** – thermal effects in pump, compressor, and valve elements follow the standard DWSIM base unit operation assumptions.

7.  **Single composition throughout** – the network does not currently support reactions. Composition changes arise only from phase equilibrium at separator or equilibrium-flash-enabled pipe objects.

8.  **Pressure-drop correlation range** – the empirical correlations (Beggs–Brill, Lockhart–Martinelli) were developed from data sets at specific pressure, velocity, and fluid-property ranges. Extrapolation beyond these ranges may reduce accuracy. The Petalas–Aziz mechanistic model generally has wider applicability.

##### Numerical Solution Procedure

1.  Parse the network topology: identify all objects, connections, sources, sinks, and nodes.

2.  Check degrees of freedom: count independent boundary conditions against unknown pressures and flow rates.

3.  Initialise all internal stream conditions by propagating boundary values from sources through the network.

4.  Formulate the decision variable vector $\mathbf{x}$ using log-transformed source pressures and/or flow rates ([\[eq:log_transform\]](#eq:log_transform)).

5.  At each solver iteration:

    1.  Recover physical values $v_i = s_i\,e^{x_i}$.

    2.  Assign recovered values to the corresponding source streams.

    3.  Calculate all network blocks sequentially: pipes, pumps, compressors, valves, separators.

    4.  Assemble node residuals $r_{m,k}$, $r_{P,k}$, $r_{E,k}$ ([\[eq:mass_balance\]](#eq:mass_balance)–[\[eq:energy_balance\]](#eq:energy_balance)).

    5.  Evaluate the objective function $F(\mathbf{x})$ ([\[eq:objective\]](#eq:objective)).

6.  Continue until $F < \epsilon^2$ or the maximum iteration count $N_{\max}$ is reached.

7.  Update all network object results and report convergence status.

##### Typical Usage Workflow

1.  Open a new or existing DWSIM flowsheet and add the *Pipe Network* block from the unit operations palette.

2.  Double-click the block to open the network canvas editor.

3.  Drag *Source* and *Sink* objects onto the canvas and specify their boundary conditions (pressure, flow rate, and composition). Stream composition is inherited from connected DWSIM material streams.

4.  Add *Pipe*, *Node*, *Pump*, and other objects as needed to build the network topology. Connect objects by drawing links between inlet and outlet ports.

5.  For each pipe segment, specify the geometry (diameter, length, roughness, angle), thermal conditions (ambient temperature, overall HTC), and preferred pressure-drop correlation.

6.  Select the solver method and convergence settings on the **Solver** tab.

7.  Return to the main flowsheet and run the simulation. The solver iterates until the objective function falls below the convergence tolerance or the iteration limit is reached.

8.  Inspect results by opening the network editor: each object displays its pressure, temperature, and flow results, and nodes show their balance residuals as convergence indicators.

#### Restriction Orifice {#sec:restriction_orifice}

##### Overview

The **Restriction Orifice** unit operation models a concentric sharp-edged orifice plate installed in a gas pipeline. The block serves two purposes simultaneously: it acts as a *flow-restriction element* by imposing a user-specified permanent pressure drop on the portion of the stream that passes through the orifice, and as a *flow splitter* by routing the remaining fraction of the inlet flow to a bypass outlet at the original inlet pressure.

The discharge coefficient follows the **ISO 5167-2** standard for concentric orifice plates with corner taps . A compressibility expansion factor $Y$ accounts for gas density changes through the orifice. Two operating modes are available: *sizing mode* (calculate the orifice diameter for a target flow rate) and *operation mode* (calculate the actual flow rate through a known orifice).

##### Stream Topology







| **Port** | **Direction** | **Description** |
|:---|:---|:---|
| Inlet Port 1 | Inlet (material) | Upstream gas stream at $P_1$, $T_1$ |
| Outlet Port 1 | Outlet (material) | Through-orifice stream at $P_2 = P_1 - \Delta P_{\mathrm{perm}}$ |
| Outlet Port 2 | Outlet (material) | Bypass stream at $P_1$ |



The inlet stream must be 100 % vapour phase. Both outlet streams inherit the inlet composition and temperature; only the mass flow and pressure of Outlet 1 are modified.

##### Calculation Modes

1.  **Sizing mode** (`SizingMode = True`) – the user specifies the pipe internal diameter $D$, the permanent pressure drop $\Delta P_{\mathrm{perm}}$, and the *target* mass flow rate $\dot{m}_{\mathrm{spec}}$; the model iterates to find the required orifice diameter $d_o$.

2.  **Operation mode** (`SizingMode = False`) – the user specifies $D$, $\Delta P_{\mathrm{perm}}$, and the orifice diameter $d_o$; the model iterates to find the actual volumetric flow rate $Q$ (and hence the mass flow $\dot{m}$) through the orifice.

##### Orifice Geometry

The diameter ratio (beta ratio) is


<a id="eq:ro_beta"></a>

\[
\beta = \frac{d_o}{D}
\]


and the orifice cross-sectional area is


<a id="eq:ro_area"></a>

\[
A_o = \frac{\pi d_o^2}{4}
\]


##### Pressure Relations

The user specifies the *permanent* (irrecoverable) pressure drop $\Delta P_{\mathrm{perm}}$. The differential pressure across the orifice taps, $\Delta P_o$, is related to the permanent loss by


<a id="eq:ro_dp"></a>

\[
\Delta P_o = \frac{\Delta P_{\mathrm{perm}}}{1 - \beta^2}
\]


The downstream pressure at Outlet 1 is


<a id="eq:ro_P2"></a>

\[
P_2 = P_1 - \Delta P_{\mathrm{perm}}
\]


and the pressure ratio used in the expansion factor calculation is


<a id="eq:ro_Pi"></a>

\[
\Pi = \frac{P_2}{P_1}
\]


##### Orifice Reynolds Number

The Reynolds number is based on the orifice diameter and the local flow conditions:


<a id="eq:ro_Re"></a>

\[
Re = \frac{\dot{m}\,d_o}{A_o\,\mu}
\]


where $\mu$ (Pa s) is the dynamic viscosity of the gas phase evaluated at the inlet conditions.

##### Discharge Coefficient (ISO 5167-2, Corner Taps)

The discharge coefficient $C$ is computed from the ISO 5167-2 correlation for **corner taps** ($L_1 = L_2 = 0$) . The auxiliary roughness parameter is first evaluated:


<a id="eq:ro_A1"></a>

\[
A_1 = \left(\frac{19000\,\beta}{Re}\right)^{0.8}
\]


The three additive components of $C$ are:

###### Base term $c_1$



<a id="eq:ro_c1"></a>

\[
c_1 = 0.5961
        + 0.0261\beta^2
        - 0.216\beta^8
        + 0.000521\!\left(\frac{10^6\beta}{Re}\right)^{\!0.7}
        + \left(0.0188 + 0.0063 A_1\right)\beta^{3.5}
          \left(\frac{10^6}{Re}\right)^{\!0.3}
\]


###### Corner-tap pressure-recovery term $c_2$

For corner taps the tap-position coefficients in ISO 5167-2 evaluate to zero, giving


<a id="eq:ro_c2"></a>

\[
c_2 = (0.043 + 0.080 - 0.123)(1 - 0.11 A_1)
          \frac{\beta^4}{1-\beta^4} = 0
\]


###### Small-orifice correction $c_3$



<a id="eq:ro_c3"></a>

\[
c_3 = \begin{cases}
        0.011(0.75 - \beta)
        \!\left(2.8 - \dfrac{d_o\,[\text{mm}]}{25.4}\right) &
        d_o < 71.12\ \text{mm} \\[8pt]
        0 & d_o \geq 71.12\ \text{mm}
    \end{cases}
\]


###### Combined discharge coefficient



<a id="eq:ro_C"></a>

\[
C = c_1 + c_2 + c_3 = c_1 + c_3
\]


Typical values lie in the range $0.60 < C < 0.75$ for $0.10 < \beta < 0.75$ and $Re > 5\times10^3$.

##### Expansion Factor (ISO 5167-2)

The expansion factor $Y$ corrects for the reduction in gas density as it accelerates through the orifice . Two sub-correlations are used depending on the pressure ratio $\Pi$:

###### Subsonic flow ($\Pi \geq 0.63$) {#subsonic-flow-pi-geq-0.63}



<a id="eq:ro_Y_sub"></a>

\[
Y = 1 - \frac{1-\Pi}{\kappa}\left(0.41 + 0.35\beta^4\right)
\]


###### High pressure drop ($\Pi < 0.63$) {#high-pressure-drop-pi-0.63}



<a id="eq:ro_Y_hi"></a>

\[
Y = 1 - \frac{0.4604}{\kappa}
          - \frac{0.413}{\kappa}\beta^4
          + \left(0.49 + 0.45\beta^4\right)\frac{\Pi}{\kappa}
\]


In both equations $\kappa = C_p/C_v$ is the isentropic exponent of the gas evaluated at the inlet conditions. For an incompressible fluid $Y \to 1$.

##### Fundamental Flow Equation

Combining all factors, the volumetric flow rate through the orifice is


<a id="eq:ro_Q"></a>

\[
Q = A_o\,C\,Y\,\sqrt{\frac{2\,\Delta P_o}{\rho_1}}
\]


and the corresponding mass flow rate is


<a id="eq:ro_mdot"></a>

\[
\dot{m} = \rho_1\,Q
\]


where $\rho_1$ (kg m$^{-3}$) is the gas-phase density at the inlet.

##### Iterative Solution Procedure

Because $C$ depends on $Re$ and $Re$ depends on $\dot{m}$ (or $d_o$), the system is solved by fixed-point iteration.

###### Sizing Mode

Rearranging [\[eq:ro_Q\]](#eq:ro_Q) for $d_o$ yields the iteration update:


<a id="eq:ro_iter_sizing"></a>

\[
d_o^{(k+1)} = \left[
        \frac{4Q_{\mathrm{spec}}}{\pi\,C^{(k)}\,Y^{(k)}}
        \sqrt{\frac{\rho_1}{2\,\Delta P_o^{(k)}}}
    \right]^{1/2}
\]


where $Q_{\mathrm{spec}} = \dot{m}_{\mathrm{spec}}/\rho_1$.

###### Operation Mode

Substituting the fixed $d_o$ directly into [\[eq:ro_Q\]](#eq:ro_Q) gives the update:


<a id="eq:ro_iter_op"></a>

\[
Q^{(k+1)} = A_o\,C^{(k)}\,Y^{(k)}
                \sqrt{\frac{2\,\Delta P_o}{\rho_1}}
\]


###### Convergence

Both modes iterate until the absolute change in the primary variable falls below $10^{-8}$ (m for $d_o$; m$^3$ s$^{-1}$ for $Q$):


<a id="eq:ro_conv"></a>

\[
\left|x^{(k+1)} - x^{(k)}\right| < 10^{-8}
\]


The iteration is capped at 100 steps; convergence is typically achieved in 5–15 iterations.

##### Outlet Stream Assignment

After convergence, the two outlet streams are set as follows:







| **Property** | **Outlet 1 (through orifice)** | **Outlet 2 (bypass)** |
|:---|:---|:---|
| Mass flow | $\dot{m}$ | $\dot{m}_{\mathrm{in}} - \dot{m}$ |
| Pressure | $P_2 = P_1 - \Delta P_{\mathrm{perm}}$ | $P_1$ |
| Temperature | $T_1$ (adiabatic) | $T_1$ |
| Composition | Same as inlet | Same as inlet |
| Flash spec | Pressure & enthalpy | — |



A physical constraint enforces $\dot{m} \le \dot{m}_{\mathrm{in}}$; an exception is raised if the calculated or specified flow rate exceeds the inlet.

##### Model Parameters {#model-parameters}







| **Parameter** | **Symbol** | **Unit** | **Default** |
|:---|:---|:---|:---|
| Pipe internal diameter | $D$ | m | 0.0254 |
| Permanent pressure drop | $\Delta P_{\mathrm{perm}}$ | Pa | 0 |
| Orifice diameter | $d_o$ | m | 0.005 |
| Target mass flow (sizing) | $\dot{m}_{\mathrm{spec}}$ | kg s$^{-1}$ | 0 |
| Sizing mode | — | — | True |



##### Results Reported {#results-reported}







| **Quantity**               | **Symbol**        | **Unit**      |
|:---------------------------|:------------------|:--------------|
| Beta ratio                 | $\beta$         | —             |
| Orifice area               | $A_o$           | m$^2$       |
| Reynolds number            | $Re$            | —             |
| Discharge coefficient      | $C$             | —             |
| Expansion factor           | $Y$             | —             |
| Orifice pressure drop      | $\Delta P_o$    | Pa            |
| Pressure ratio             | $\Pi = P_2/P_1$ | —             |
| Orifice diameter (sizing)  | $d_o$           | m             |
| Mass flow rate (operation) | $\dot{m}$       | kg s$^{-1}$ |



##### Assumptions and Limitations

1.  **Gas phase only** – the inlet stream must be 100 % vapour. Liquid or two-phase streams are rejected with an error message.

2.  **Corner taps** – the discharge coefficient is evaluated for corner-tap geometry ($L_1 = L_2 = 0$). The pressure-recovery term $c_2$ vanishes identically for this configuration ([\[eq:ro_c2\]](#eq:ro_c2)).

3.  **ISO 5167 applicability range** – the correlation is validated for $0.10 < \beta < 0.75$ and $Re > 4000$ (turbulent flow). Results outside these ranges are unreliable.

4.  **Subsonic flow** – no choked-flow (sonic) limit is modelled. The expansion factor correlations assume $\Pi > 0$; for very high pressure drops ($\Pi \to 0$) the model may not converge or may produce non-physical results.

5.  **Adiabatic, no Joule-Thomson correction** – the outlet temperature equals the inlet temperature. The isentropic temperature drop through the orifice is not computed.

6.  **Constant inlet properties** – density $\rho_1$, viscosity $\mu$, and the isentropic exponent $\kappa$ are evaluated once at the inlet conditions and held fixed during the iteration.

7.  **No fin or discharge length effects** – the model is strictly applicable to a thin, sharp-edged plate. Nozzles, venturi elements, or long-bore orifices require different coefficients.

8.  **Horizontal pipe assumed** – no gravitational head correction is applied.

9.  **Premium requirement** – the calculation routine requires an active DWSIM Premium Supporter subscription.

##### Typical Usage Workflow

1.  Connect a gas material stream to **Inlet Port 1** and two material streams to **Outlet Port 1** (restricted flow) and **Outlet Port 2** (bypass).

2.  On the **Parameters** tab, enter the pipe internal diameter and the permanent pressure drop.

3.  *Sizing mode*: enable *Sizing Mode*, enter the desired mass flow rate, and run the simulation. Read the calculated orifice diameter from the results.

4.  *Operation mode*: disable *Sizing Mode*, enter the orifice diameter from the design step, and run the simulation. Read the actual flow through the orifice and the bypass flow from the outlet streams.

5.  Verify that $\beta$ lies within 0.10–0.75 and $Re > 4000$ in the results panel; adjust geometry if necessary.

#### Advanced Heat Exchanger {#sec:ahx}

##### Overview

The **Advanced Heat Exchanger** is a rigorous shell-and-tube heat exchanger model that performs incremental (zone-by-zone) integration of heat transfer and pressure drop along the exchanger length. Unlike the standard DWSIM heat exchanger, this unit operation evaluates local thermophysical properties at each integration increment, handles phase changes (condensation and vaporisation) on both the shell and tube sides, and uses the full Bell–Delaware method  for shell-side thermal–hydraulic calculations.

Four calculation modes are available:

- **Rating** — Compute the heat duty, outlet temperatures, and pressure drops from a fully specified geometry.

- **Design** — Size the exchanger (number of tubes and shell diameter) to meet a specified outlet temperature.

- **Simulation** — Use a user-supplied overall coefficient $U$ to compute the duty and outlet temperatures.

- **Fouling Factor** — Back-calculate the overall fouling resistance from known inlet and outlet temperatures.

##### Stream Topology







| **Port**      | **Direction**     | **Description**         |
|:--------------|:------------------|:------------------------|
| Hot Side In   | Inlet (material)  | Hot-side inlet stream   |
| Cold Side In  | Inlet (material)  | Cold-side inlet stream  |
| Hot Side Out  | Outlet (material) | Hot-side outlet stream  |
| Cold Side Out | Outlet (material) | Cold-side outlet stream |



Either fluid may be assigned to the shell side or the tube side via the `Shell_Fluid` and `Tube_Fluid` settings.

##### Geometry {#sec:ahx:geometry}

The exchanger geometry is defined by the `STHXPropertiesAdvanced` data class. The principal user inputs are:

###### Shell parameters

Shell inside diameter $D_s$ (mm), number of shell passes, number of shells in series, TEMA shell type designation, baffle type (single-segmental, double-segmental, or no-tubes-in-window), baffle cut $B_c$ (%), central baffle spacing $L_b$ (m), inlet and outlet baffle spacings $L_{bi}$ and $L_{bo}$ (m), shell-to-baffle diametral clearance $D_{sb}$ (mm), number of sealing strip pairs $N_{ss}$, and shell-side fouling factor $R_{f,s}$ (m$^2$ K/W).

###### Tube parameters

Tube outside diameter $d_o$ (mm), inside diameter $d_i$ (mm), tube length $L_t$ (m), number of tubes $N_t$, tube passes $N_{tp}$, tube pitch $P_t$ (mm), tube layout pattern (30° triangular, 90° square, 60° rotated triangular, or 45° rotated square), tube wall thermal conductivity $k_w$ (W/m K), inside roughness $\varepsilon$ (mm), tube-to-baffle clearance $d_{tb}$ (mm), and tube-side fouling factor $R_{f,t}$ (m$^2$ K/W).

###### Nozzle parameters

Inside diameters of the shell-side inlet/outlet and tube-side inlet/outlet nozzles (mm).

###### Derived quantities

The following intermediate quantities are computed once from the user inputs :


<a id="eq:ahx:Dotl"></a><a id="eq:ahx:Dctl"></a><a id="eq:ahx:Nb"></a><a id="eq:ahx:Sm"></a><a id="eq:ahx:Asb"></a><a id="eq:ahx:Atb"></a>

\[
\begin{align}
  D_\mathit{otl}  &= D_s - D_{sb},                               \\
  D_\mathit{ctl}  &= D_\mathit{otl} - d_o,                       \\
  N_b              &= \left\lfloor\frac{L_t - L_{bi} - L_{bo}}{L_b}\right\rfloor + 1,   \\
  S_m              &= L_b\left(D_s - D_\mathit{otl} + D_\mathit{ctl}\,\frac{P_t' - d_o}{P_t'}\right),   \\
  A_\mathit{sb}    &= \pi\,D_s\,\frac{D_{sb}}{2}\,(1 - B_c),    \\
  A_\mathit{tb}    &= \frac{\pi}{4}\bigl[(d_o + d_{tb})^2 - d_o^2\bigr]\,N_t\,(1-F_w),
\end{align}
\]


where $P_t'$ is the effective pitch (adjusted for rotated layouts), $F_w$ is the fraction of tubes in the window zone, and $S_m$ is the cross-flow area at the bundle centreline.

The total outside heat-transfer area is:


<a id="eq:ahx:area"></a>

\[
A = N_t\,\pi\,d_o\,L_t.
\]


##### Incremental Integration (Rating Mode) {#sec:ahx:rating}

The exchanger is divided into $N$ equal-area increments. At each increment $k$, local thermophysical properties (density, viscosity, thermal conductivity, heat capacity, vapor fraction) are evaluated by performing a pressure–temperature flash at the local conditions. The local heat duty is:


<a id="eq:ahx:dQ"></a>

\[
\delta Q_k = U_k\,\delta A\,\Delta T_k,
\]


where $U_k$ is the local overall coefficient, $\delta A = A/N$ is the incremental area, and $\Delta T_k = T_{h,k} - T_{c,k}$ is the local temperature difference.

After each increment, the new enthalpies are:


<a id="eq:ahx:Hh"></a><a id="eq:ahx:Hc"></a>

\[
\begin{align}
  H_{h,k+1} &= H_{h,k} - \frac{\delta Q_k}{\dot{m}_h},  \\
  H_{c,k+1} &= H_{c,k} + \frac{\delta Q_k}{\dot{m}_c},
\end{align}
\]


and the local temperatures are recovered by a pressure–enthalpy flash. The iteration is repeated until the total duty $Q = \sum_k \delta Q_k$ converges.

##### Shell-Side Heat Transfer (Bell–Delaware Method) {#sec:ahx:belldelaware}

###### Ideal cross-flow coefficient

The ideal (un-corrected) shell-side heat-transfer coefficient is computed from the Taborek $j$-factor correlation :


<a id="eq:ahx:hid"></a>

\[
h_\mathit{id} = j_i\,\frac{c_p\,G_s}{\Pr^{2/3}}
                  \left(\frac{\mu}{\mu_w}\right)^{0.14},
\]


where $G_s = \dot{m}_s / S_m$ is the shell-side mass velocity, $\Pr$ is the Prandtl number, $\mu/\mu_w$ is the viscosity correction, and the $j$-factor is:


<a id="eq:ahx:ji"></a>

\[
j_i = a_1\left(\frac{1.33}{P_t/d_o}\right)^{a}\,\Re_s^{a_2},
  \qquad
  a = \frac{a_3}{1 + 0.14\,\Re_s^{a_4}},
\]


with constants $a_1$–$a_4$ tabulated by Taborek for each tube layout and Reynolds number range ($\Re_s = d_o\,G_s/\mu$).

###### Correction factors

The actual shell-side coefficient is:


<a id="eq:ahx:hs"></a>

\[
h_s = h_\mathit{id}\,J_c\,J_l\,J_b\,J_s\,J_r,
\]


where the five Bell–Delaware correction factors account for the following effects:







| **Factor** | **Description** |
|:---|:---|
| $J_c = 0.55 + 0.72\,F_c$ | Baffle-cut correction; $F_c$ is the fraction of tubes in the cross-flow zone. |
| $J_l$ | Baffle-leakage correction for shell–baffle ($A_{sb}$) and tube–baffle ($A_{tb}$) clearance streams. |
| $J_b = \exp\!\bigl[-C_{bh}\,F_{sbp}\,(1 - (2N_{ss}/N_{cl})^{1/3})\bigr]$ | Bundle-bypass correction; $C_{bh} = 1.35$ (laminar) or $1.25$ (turbulent); $F_{sbp}$ is the bypass fraction; $N_{ss}$ is the number of sealing strip pairs. |
| $J_s$ | Unequal baffle-spacing correction for inlet/outlet vs. central spacings. |
| $J_r$ | Laminar-flow correction ($\Re_s < 100$) for adverse temperature gradients. |



##### Tube-Side Heat Transfer {#sec:ahx:tubeside}

The single-phase tube-side coefficient is computed from the Gnielinski correlation  in the turbulent regime ($\Re \geq 10\,000$), the Sieder–Tate correlation  in the laminar regime ($\Re \leq 2300$), and a linear interpolation in the transition zone:

###### Gnielinski (turbulent)



<a id="eq:ahx:gnielinski"></a>

\[
\Nu = \frac{(f/8)\,(\Re - 1000)\,\Pr}
             {1 + 12.7\,\sqrt{f/8}\,(\Pr^{2/3} - 1)}
       \left(\frac{\mu}{\mu_w}\right)^{0.14},
\]


where $f = (0.79\ln\Re - 1.64)^{-2}$ is the Petukhov friction factor.

###### Sieder–Tate (laminar)



<a id="eq:ahx:sieder"></a>

\[
\Nu = \max\!\left(3.66,\; 1.86\,\Gz^{1/3}\left(\frac{\mu}{\mu_w}\right)^{0.14}\right),
\]


where $\Gz = \Re\,\Pr\,(d_i/L_t)$ is the Graetz number.

##### Two-Phase Correlations {#sec:ahx:twophase}

###### Flow regime detection

At each integration increment the local vapor fractions on both sides are compared with the previous increment to classify the flow regime as single-phase liquid, single-phase vapor, condensing, vaporising, or two-phase (quality unchanged). The appropriate two-phase correlation is then selected.

###### Lockhart–Martinelli parameter

The turbulent–turbulent Martinelli parameter is used by several correlations:


<a id="eq:ahx:Xtt"></a>

\[
X_\mathit{tt} = \left(\frac{1-x}{x}\right)^{0.9}
                  \left(\frac{\rho_V}{\rho_L}\right)^{0.5}
                  \left(\frac{\mu_L}{\mu_V}\right)^{0.1},
\]


where $x$ is the local vapor quality.

###### Shell-side condensation

The shell-side condensation coefficient is taken as the maximum of a gravity-controlled (Nusselt film) contribution and a shear-controlled (McNaught/Boyko–Kruzhilin) contribution :


<a id="eq:ahx:nusselt_cond"></a><a id="eq:ahx:mcnaught"></a>

\[
\begin{align}
  h_\mathrm{grav} &= 0.725\left[\frac{\rho_L(\rho_L - \rho_V)\,g\,h_{fg}\,k_L^3}
                     {N_r\,\mu_L\,d_o\,\Delta T_\mathrm{film}}\right]^{0.25},
                      \\
  h_\mathrm{shear} &= h_{L}\sqrt{\frac{\rho_L}{\rho_\mathit{TP}}},
\end{align}
\]


where $N_r$ is the number of tube rows, $h_{fg}$ is the latent heat, and $\rho_\mathit{TP}$ is the two-phase mixture density.

###### Tube-side condensation (Shah)

The Shah  condensation correlation modifies the liquid-only coefficient:


<a id="eq:ahx:shah_cond"></a>

\[
h_\mathrm{cond} = h_L\left(0.55 + \frac{2.09}{X_\mathit{tt}^{0.38}}\right).
\]


###### Shell-side vaporisation (Chen)

Shell-side boiling uses the Chen  superposition of nucleate pool boiling (Mostinski correlation ) and forced convective enhancement:


<a id="eq:ahx:chen_shell"></a>

\[
h_\mathrm{boil} = S\,h_\mathit{nb} + F\,h_L,
\]


where $h_\mathit{nb}$ is the Mostinski nucleate boiling coefficient:


<a id="eq:ahx:mostinski"></a>

\[
h_\mathit{nb} = 0.00417\,P_c^{0.69}\,q^{0.7}\,F_P,
  \qquad
  F_P = 1.8\,P_r^{0.17} + 4\,P_r^{1.2} + 10\,P_r^{10},
\]


and the Chen enhancement factor $F$ and suppression factor $S$ are:


<a id="eq:ahx:chen_F"></a><a id="eq:ahx:chen_S"></a>

\[
\begin{align}
  F &= \max\!\left(1,\; 2.35\,(1/X_\mathit{tt} + 0.213)^{0.736}\right),
       \\
  S &= \frac{1}{1 + 2.53 \times 10^{-6}\,\Re_\mathit{TP}^{1.17}},
      \quad \Re_\mathit{TP} = \Re_L\,F^{1.25}.
\end{align}
\]


###### Tube-side vaporisation (Chen/Forster–Zuber)

Tube-side flow boiling uses the full Chen  correlation with the Forster–Zuber nucleate boiling coefficient:


<a id="eq:ahx:forster_zuber"></a>

\[
h_\mathit{FZ} = 0.00122\,
      \frac{k_L^{0.79}\,c_{p,L}^{0.45}\,\rho_L^{0.49}}
           {\sigma^{0.5}\,\mu_L^{0.29}\,h_{fg}^{0.24}\,\rho_V^{0.24}}
      \,\Delta T_\mathrm{sat}^{0.24}\,\Delta P_\mathrm{sat}^{0.75},
\]


and $h_\mathrm{boil} = S\,h_\mathit{FZ} + F\,h_L$ with the same $F$ and $S$ factors as above.

###### Critical heat flux

The maximum (critical) heat flux is checked using the Zuber  correlation:


<a id="eq:ahx:zuber"></a>

\[
q_\mathrm{max} = 0.149\,h_{fg}\,\rho_V
      \left[\frac{\sigma\,g\,(\rho_L - \rho_V)}{\rho_V^2}\right]^{0.25}
      \!\cdot f_b,
\]


where $f_b \approx 0.5$ is a bundle correction factor.

##### Overall Heat-Transfer Coefficient {#sec:ahx:overallU}

The local overall coefficient based on the outside tube area is:


<a id="eq:ahx:overallU"></a>

\[
\frac{1}{U} = \frac{1}{h_s} + R_{f,s}
                + \frac{d_o\,\ln(d_o/d_i)}{2\,k_w}
                + \frac{d_o}{d_i}\,R_{f,t}
                + \frac{d_o}{d_i}\,\frac{1}{h_t},
\]


where $h_s$ and $h_t$ are the shell-side and tube-side coefficients, $k_w$ is the tube wall thermal conductivity, and $R_{f,s}$, $R_{f,t}$ are the shell-side and tube-side fouling resistances.

##### LMTD and F-Correction Factor {#sec:ahx:lmtd}

The log-mean temperature difference for counter-current flow is:


<a id="eq:ahx:lmtd"></a>

\[
\Delta T_\mathrm{lm} = \frac{(T_{h,\mathrm{in}} - T_{c,\mathrm{out}})
                               - (T_{h,\mathrm{out}} - T_{c,\mathrm{in}})}
                              {\ln\!\dfrac{T_{h,\mathrm{in}} - T_{c,\mathrm{out}}}
                                          {T_{h,\mathrm{out}} - T_{c,\mathrm{in}}}}.
\]


For multi-pass configurations the LMTD correction factor $F$ is computed using the $P$–$R$ method for a 1–$N$ shell-and-tube exchanger:


<a id="eq:ahx:PR"></a>

\[
P = \frac{T_{c,\mathrm{out}} - T_{c,\mathrm{in}}}
           {T_{h,\mathrm{in}} - T_{c,\mathrm{in}}},
  \qquad
  R = \frac{T_{h,\mathrm{in}} - T_{h,\mathrm{out}}}
           {T_{c,\mathrm{out}} - T_{c,\mathrm{in}}},
\]


with the analytical formula for $F$ as a function of $P$, $R$, and the number of shell passes.

##### Pressure Drop {#sec:ahx:pressuredrop}

###### Shell-side pressure drop (Bell–Delaware)

The total shell-side pressure drop comprises four contributions:


<a id="eq:ahx:dPs"></a>

\[
\Delta P_s = \Delta P_\mathrm{cross} + \Delta P_\mathrm{window}
             + \Delta P_\mathrm{end} + \Delta P_\mathrm{nozzle}.
\]


The ideal cross-flow drop per baffle space is:


<a id="eq:ahx:dPid"></a>

\[
\Delta P_\mathrm{id} = 2\,f_i\,N_{cl}\,\frac{G_s^2}{\rho_s},
\]


where $f_i$ is the ideal friction factor (tabulated by layout and $\Re_s$) and $N_{cl}$ is the number of tube rows crossed. The cross-flow, window, and end-zone contributions are then :


<a id="eq:ahx:dPcross"></a><a id="eq:ahx:dPwin"></a><a id="eq:ahx:dPend"></a>

\[
\begin{align}
  \Delta P_\mathrm{cross}  &= (N_b - 1)\,\Delta P_\mathrm{id}\,R_l\,R_b,
       \\
  \Delta P_\mathrm{window} &= N_b\,(2 + 0.6\,N_{tw})\,\frac{G_w^2}{2\,\rho_s}\,R_l,
       \\
  \Delta P_\mathrm{end}    &= 2\,\Delta P_\mathrm{id}
      \left(1 + \frac{N_{cw}}{N_{cl}}\right) R_b,
\end{align}
\]


where $G_w = \dot{m}_s / \sqrt{S_m\,A_w}$ is the geometric mean mass velocity in the window zone, $N_{tw}$ is the number of tube rows in the window, $N_{cw}$ is the number of cross-flow rows in the end zone, and $R_l$, $R_b$ are the leakage and bypass correction factors (numerically equal to $J_l$ and $J_b$). Nozzle losses are computed as $\frac{1}{2}\rho v_n^2$ for each nozzle.

###### Tube-side pressure drop

The tube-side pressure drop is computed using the Darcy–Weisbach equation with the Churchill  friction factor (valid for all flow regimes):


<a id="eq:ahx:dPt"></a>

\[
\Delta P_t = N_{tp}\,f\,\frac{L_t}{d_i}\,\frac{\rho\,v^2}{2}
             + N_{tp}\,4\,\frac{\rho\,v^2}{2}
             + \Delta P_\mathrm{nozzle},
\]


where the first term is the friction loss, the second the return-bend loss ($4\times$ velocity head per pass), and nozzle losses are again $\frac{1}{2}\rho v_n^2$ for each nozzle.

###### Two-phase pressure drop

When a two-phase flow is detected, single-phase pressure drops are multiplied by the Lockhart–Martinelli  two-phase multiplier:


<a id="eq:ahx:LM_mult"></a>

\[
\phi_L^2 = 1 + \frac{C}{X_\mathit{tt}} + \frac{1}{X_\mathit{tt}^2},
\]


with $C = 20$ (turbulent–turbulent). On the shell side, the Grant correlation provides an alternative multiplier:


<a id="eq:ahx:grant"></a>

\[
\phi_\mathrm{Grant}^2 = 1 + x\left(\frac{\rho_L}{\rho_V} - 1\right).
\]


##### Vibration Analysis {#sec:ahx:vibration}

A tube vibration check is performed at the end of every rating calculation. Three mechanisms are evaluated:

1.  **Natural frequency.** The first-mode natural frequency of a fixed–fixed tube span between baffles is:


<a id="eq:ahx:fn"></a>

\[
f_n = \frac{22.4}{2\pi}\sqrt{\frac{EI}{m_\mathrm{total}\,L_b^4}},
\]


    where $E$ is the tube Young’s modulus (default 200 GPa, carbon steel), $I$ is the second moment of area, and $m_\mathrm{total}$ is the mass per unit length (tube wall + internal fluid + external added mass).

2.  **Vortex shedding.** The vortex shedding frequency $f_{vs} = 0.2\,v_\mathrm{cross}/d_o$ is compared to $f_n$. A warning is issued when $0.8 < f_{vs}/f_n < 1.2$.

3.  **Fluid-elastic instability (Connors criterion).** The critical cross-flow velocity is:


<a id="eq:ahx:vcrit"></a>

\[
v_\mathrm{crit} = 3.3\,f_n\,d_o
              \sqrt{\frac{2\pi\,\zeta\,m_\mathrm{total}}{\rho_s\,d_o^2}},
\]


    where $\zeta = 0.01$ is the assumed damping ratio. A warning is issued when $v_\mathrm{cross}/v_\mathrm{crit} > 0.7$.

##### Design Mode {#sec:ahx:design}

In Design mode the user specifies one outlet temperature. The model first estimates the required area from a shortcut LMTD calculation with an assumed $U = 500$ W/m$^2$ K, computes the initial tube count as $N_t = A_\mathrm{req}/(\pi\,d_o\,L_t)$, and derives the shell diameter from tube-count correlations. It then iterates (up to 20 times) by running a full Rating calculation and adjusting $N_t$ by the ratio $Q_\mathrm{req}/Q_\mathrm{calc}$ until convergence within 2%. If a maximum shell-side pressure drop constraint is active, the baffle spacing is increased by 20% in each iteration where the constraint is violated.

##### Simulation Mode {#sec:ahx:simulation}

In Simulation mode the user provides a constant overall coefficient $U$. A full Rating calculation is performed to obtain the geometry-dependent LMTD and $F$-factor, then the duty is recomputed as $Q = U\,A\,F\,\Delta T_\mathrm{lm}$, and the outlet temperatures are recovered by pressure–enthalpy flashes.

##### Fouling Factor Mode {#sec:ahx:fouling}

In Fouling Factor mode both outlet temperatures are specified. The model computes $Q$ from the hot-side energy balance, then the “dirty” overall coefficient $U_\mathrm{dirty} = Q/(A\,F\,\Delta T_\mathrm{lm})$. A Rating calculation is repeated with zero fouling to obtain $U_\mathrm{clean}$. The overall fouling resistance is:


<a id="eq:ahx:Rf"></a>

\[
R_f = \frac{1}{U_\mathrm{dirty}} - \frac{1}{U_\mathrm{clean}}.
\]


##### Parameters Summary

Table [7](#tab:ahx:params) lists the principal user-configurable parameters.



<a id="tab:ahx:params"></a>



<table>
<caption>Principal parameters of the Advanced Heat Exchanger.</caption>
<thead>
<tr>
<th style="text-align: left;">Parameter</th>
<th style="text-align: left;">Units</th>
<th style="text-align: left;">Default</th>
<th style="text-align: left;">Description</th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">Parameter</td>
<td style="text-align: left;">Units</td>
<td style="text-align: left;">Default</td>
<td style="text-align: left;">Description</td>
</tr>
<tr>
<td colspan="4" style="text-align: right;"><em>Continued on next page</em></td>
</tr>
<tr>
<td style="text-align: left;"></td>
<td style="text-align: left;"></td>
<td style="text-align: left;"></td>
<td style="text-align: left;"></td>
</tr>
<tr>
<td style="text-align: left;">Calculation Mode</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Rating</td>
<td style="text-align: left;">Rating, Design, Simulation, or FoulingFactor</td>
</tr>
<tr>
<td style="text-align: left;">Flow Direction</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Counter-current</td>
<td style="text-align: left;">Counter-current or Co-current</td>
</tr>
<tr>
<td style="text-align: left;">Number of Increments</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">20</td>
<td style="text-align: left;">Integration resolution</td>
</tr>
<tr>
<td style="text-align: left;">Max Iterations</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">100</td>
<td style="text-align: left;">Outer convergence loop limit</td>
</tr>
<tr>
<td style="text-align: left;">Tolerance</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;"><span class="math inline">\(10^{-6}\)</span></td>
<td style="text-align: left;">Relative duty convergence tolerance</td>
</tr>
<tr>
<td style="text-align: left;">Overall Coefficient</td>
<td style="text-align: left;">W/m<span class="math inline">\(^2\)</span>K</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">User-specified <span class="math inline">\(U\)</span> (Simulation mode)</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Shell</em></td>
</tr>
<tr>
<td style="text-align: left;">Shell <span class="math inline">\(D_s\)</span></td>
<td style="text-align: left;">mm</td>
<td style="text-align: left;">500</td>
<td style="text-align: left;">Shell inside diameter</td>
</tr>
<tr>
<td style="text-align: left;">Shell passes</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">1</td>
<td style="text-align: left;">Number of shell passes</td>
</tr>
<tr>
<td style="text-align: left;">Shells in series</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">1</td>
<td style="text-align: left;">Number of shells in series</td>
</tr>
<tr>
<td style="text-align: left;">TEMA type</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">AEL</td>
<td style="text-align: left;">TEMA designation</td>
</tr>
<tr>
<td style="text-align: left;">Baffle type</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Single-seg.</td>
<td style="text-align: left;">Single-segmental, double-segmental, NTIW</td>
</tr>
<tr>
<td style="text-align: left;">Baffle cut <span class="math inline">\(B_c\)</span></td>
<td style="text-align: left;">%</td>
<td style="text-align: left;">25</td>
<td style="text-align: left;">Baffle cut percentage</td>
</tr>
<tr>
<td style="text-align: left;">Baffle spacing <span class="math inline">\(L_b\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">0.25</td>
<td style="text-align: left;">Central baffle spacing</td>
</tr>
<tr>
<td style="text-align: left;">Shell fouling</td>
<td style="text-align: left;">m<span class="math inline">\(^2\)</span>K/W</td>
<td style="text-align: left;">0.0002</td>
<td style="text-align: left;">Shell-side fouling resistance</td>
</tr>
<tr>
<td style="text-align: left;">Sealing strips</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">1</td>
<td style="text-align: left;">Pairs of sealing strips</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Tubes</em></td>
</tr>
<tr>
<td style="text-align: left;">Tube <span class="math inline">\(d_o\)</span></td>
<td style="text-align: left;">mm</td>
<td style="text-align: left;">25.4</td>
<td style="text-align: left;">Tube outside diameter</td>
</tr>
<tr>
<td style="text-align: left;">Tube <span class="math inline">\(d_i\)</span></td>
<td style="text-align: left;">mm</td>
<td style="text-align: left;">21.2</td>
<td style="text-align: left;">Tube inside diameter</td>
</tr>
<tr>
<td style="text-align: left;">Tube length <span class="math inline">\(L_t\)</span></td>
<td style="text-align: left;">m</td>
<td style="text-align: left;">4.88</td>
<td style="text-align: left;">Tube length</td>
</tr>
<tr>
<td style="text-align: left;">Number of tubes <span class="math inline">\(N_t\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">100</td>
<td style="text-align: left;">Tubes per shell</td>
</tr>
<tr>
<td style="text-align: left;">Tube passes <span class="math inline">\(N_{tp}\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">2</td>
<td style="text-align: left;">Tube passes per shell</td>
</tr>
<tr>
<td style="text-align: left;">Tube pitch <span class="math inline">\(P_t\)</span></td>
<td style="text-align: left;">mm</td>
<td style="text-align: left;">31.75</td>
<td style="text-align: left;">Centre-to-centre pitch</td>
</tr>
<tr>
<td style="text-align: left;">Tube layout</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">30° tri. </td>
<td style="text-align: left;">Layout pattern</td>
</tr>
<tr>
<td style="text-align: left;">Tube <span class="math inline">\(k_w\)</span></td>
<td style="text-align: left;">W/m K</td>
<td style="text-align: left;">50</td>
<td style="text-align: left;">Tube wall thermal conductivity</td>
</tr>
<tr>
<td style="text-align: left;">Tube fouling</td>
<td style="text-align: left;">m<span class="math inline">\(^2\)</span>K/W</td>
<td style="text-align: left;">0.0002</td>
<td style="text-align: left;">Tube-side fouling resistance</td>
</tr>
</tbody>
</table>



##### Results

After a successful calculation the following results are available:

- Heat duty $Q$, hot-side and cold-side outlet temperatures.

- Shell-side and tube-side heat-transfer coefficients (local and average).

- Overall coefficient $U$ (calculated or specified).

- LMTD and $F$-correction factor.

- Thermal efficiency $\varepsilon$.

- Shell-side and tube-side pressure drops.

- Overall fouling factor (Fouling Factor mode).

- Temperature, duty, and vapor-fraction profiles along the exchanger.

- Vibration warnings (if any).

#### Vapor Compression Chiller {#sec:vcc}

##### Overview

The **Vapor Compression Chiller** is a custom unit operation that simulates a complete multi-stage mechanical refrigeration cycle. It is designed for refinery and petrochemical applications where process streams must be cooled below the temperature achievable by cooling water or air alone, as in LPG recovery, gas dewpoint control, alkylation feed chilling, and amine-unit intercooling.

The model covers the full thermodynamic cycle—evaporation, compression, condensation, and expansion—together with preliminary sizing of the major equipment items: compressors and heat exchangers. All thermodynamic calculations are performed through the flowsheet property package, so any equation of state or activity-coefficient model available in DWSIM can be used for the refrigerant side.

##### Stream Topology

The unit operation has the following connection ports:







| **Port** | **Direction** | **Type** | **Description** |
|:---|:---|:---|:---|
| Process In | Inlet | Material | Process fluid to be cooled (evaporator shell side) |
| Cooling Fluid In | Inlet | Material | Condenser cooling medium (water, glycol, air, etc.) |
| Shaft Power In | Inlet | Energy | Electrical/mechanical power driving the compressors (optional) |
| Process Out | Outlet | Material | Cooled process fluid leaving the evaporator |
| Cooling Fluid Out | Outlet | Material | Heated cooling medium leaving the condenser |



##### Configuration

###### Compression stages

Between one and three compression stages may be specified. Interstage pressures are distributed geometrically (equal pressure ratio per stage):


<a id="eq:vcc:interstage"></a>

\[
r = \left(\frac{P_{\mathrm{cond}}}{P_{\mathrm{evap}}}\right)^{1/N},
  \qquad
  P_i = P_{\mathrm{evap}}\,r^{i}, \quad i = 0, 1, \ldots, N,
\]


where $N$ is the number of stages, $P_{\mathrm{evap}}$ is the evaporator pressure, and $P_{\mathrm{cond}}$ is the condenser pressure.

###### Flash economizers

An open-cycle flash economizer may optionally be installed between any two adjacent stages. The liquid refrigerant leaving the upstream condenser or interstage liquid header is flashed to the interstage pressure. The resulting vapor is injected into the suction of the next stage, reducing the refrigerant circulation rate at the evaporator level and improving the cycle coefficient of performance. The vapor fraction at the flash condition is obtained from an isenthalpic flash:


<a id="eq:vcc:econ_flash"></a>

\[
H_{\mathrm{in}} = H_{\mathrm{liq}}\,(1-\psi) + H_{\mathrm{vap}}\,\psi,
\]


where $\psi$ is the molar vapor fraction and $H_{\mathrm{in}}$ is the enthalpy of the liquid entering the economizer at constant enthalpy (isenthalpic valve upstream).

###### Refrigerant

The refrigerant composition is defined by selecting compounds already present in the flowsheet and specifying their mole fractions. Any property package registered in the flowsheet can be assigned to the refrigerant side independently. Peng–Robinson EOS is recommended for hydrocarbon and common HFC refrigerants.

##### Thermodynamic Model

###### State points

The refrigerant cycle is described by four canonical state points per stage:

- **Point 1** — Evaporator exit: saturated (or slightly superheated) vapor at $P_{\mathrm{evap}}$, obtained from a dew-point flash.

- **Point 2s** — Isentropic compressor discharge: entropy equals Point 1, pressure equals stage discharge pressure. Obtained from a $P$–$S$ flash.

- **Point 2** — Actual compressor discharge: enthalpy corrected for isentropic efficiency.

- **Point 3** — Condenser exit: saturated liquid at $P_{\mathrm{cond}}$, obtained from a bubble-point flash.

- **Point 4** — Expansion valve exit: isenthalpic flash to $P_{\mathrm{evap}}$.

###### Compressor

The actual specific work and discharge state for each stage are:


<a id="eq:vcc:w_is"></a><a id="eq:vcc:w_act"></a><a id="eq:vcc:h2"></a><a id="eq:vcc:shaft"></a>

\[
\begin{align}
  w_{\mathrm{is}} &= H_{2\mathrm{s}} - H_1,  \\
  w_{\mathrm{act}} &= \frac{w_{\mathrm{is}}}{\eta_{\mathrm{is}}},  \\
  H_2 &= H_1 + w_{\mathrm{act}},  \\
  \dot{W}_{\mathrm{shaft},i} &= \dot{n}_{\mathrm{ref},i}\,\frac{w_{\mathrm{act}}}{\eta_{\mathrm{mech}}},
\end{align}
\]


where $\eta_{\mathrm{is}}$ and $\eta_{\mathrm{mech}}$ are the isentropic and mechanical efficiencies, respectively, and $\dot{n}_{\mathrm{ref},i}$ is the molar flow rate of refrigerant through stage $i$.

###### Refrigerant circulation rate

The molar flow rate at the evaporator level is determined from the process-stream heat balance:


<a id="eq:vcc:refflow"></a>

\[
\dot{n}_{\mathrm{ref}} = \frac{\dot{Q}_{\mathrm{evap}}}{H_1 - H_4},
\]


where $\dot{Q}_{\mathrm{evap}} = \dot{n}_{\mathrm{proc}}\,(H_{\mathrm{proc,in}} - H_{\mathrm{proc,out}})$ is the process-side heat load. When economizers are present, the flow through each subsequent stage is augmented by the injected flash vapor.

###### Energy balance

The overall energy balance is:


<a id="eq:vcc:energy"></a>

\[
\dot{Q}_{\mathrm{cond}} = \dot{Q}_{\mathrm{evap}} + \sum_{i=1}^{N} \dot{W}_{\mathrm{shaft},i}.
\]


###### Coefficient of performance



<a id="eq:vcc:cop"></a>

\[
\mathrm{COP} = \frac{\dot{Q}_{\mathrm{evap}}}{\displaystyle\sum_{i=1}^{N} \dot{W}_{\mathrm{shaft},i}}.
\]


##### Evaporator and Condenser Specifications

###### Evaporator

The user may specify either the evaporator temperature $T_{\mathrm{evap}}$ (from which $P_{\mathrm{evap}}$ is obtained by a dew-point calculation) or the evaporator pressure $P_{\mathrm{evap}}$ directly.

###### Condenser

Three specification modes are available:

1.  Fixed condenser temperature $T_{\mathrm{cond}}$.

2.  Fixed condenser pressure $P_{\mathrm{cond}}$.

3.  Approach temperature difference: $T_{\mathrm{cond}} = T_{\mathrm{cool,in}} + \Delta T_{\mathrm{app}}$, where $T_{\mathrm{cool,in}}$ is the inlet temperature of the cooling stream.

##### Equipment Sizing

###### Compressor sizing

For each stage the actual volumetric flow at suction conditions is:


<a id="eq:vcc:vdot"></a>

\[
\dot{V}_{\mathrm{suc}} = \dot{n}_{\mathrm{ref},i}\,\hat{V}_{m,\mathrm{suc}},
\]


where $\hat{V}_{m,\mathrm{suc}}$ is the molar volume at suction conditions from the property package.

The polytropic index $n$ is estimated from the isentropic temperature ratio and the actual temperature ratio:


<a id="eq:vcc:polytropic"></a>

\[
\gamma \approx 1 + \frac{\ln r}{\ln\!\left(T_{2\mathrm{s}}/T_1\right)},
  \qquad
  \eta_p = \frac{\gamma - 1}{\gamma}\,\frac{\ln r}{\ln\!\left(T_2/T_1\right)},
  \qquad
  \frac{n}{n-1} = \eta_p\,\frac{\gamma}{\gamma - 1},
\]


where $r = P_{\mathrm{dis}}/P_{\mathrm{suc}}$ is the stage pressure ratio.

The polytropic head (Schultz method, simplified) is:


<a id="eq:vcc:polhead"></a>

\[
H_p = \frac{R\,T_{\mathrm{suc}}}{M_w}\,\frac{n}{n-1}
        \left[\left(\frac{P_{\mathrm{dis}}}{P_{\mathrm{suc}}}\right)^{(n-1)/n} - 1\right],
\]


in J/kg, where $M_w$ is the refrigerant molar mass.

For *centrifugal* compressors, the dimensionless specific speed is:


<a id="eq:vcc:ns"></a>

\[
N_s = \frac{n_{\mathrm{rev}}\,\sqrt{\dot{V}_{\mathrm{suc}}}}{H_p^{3/4}},
\]


with $n_{\mathrm{rev}}$ in rev/s, $\dot{V}$ in m$^3$/s, and $H_p$ in m.

For *reciprocating* compressors, the volumetric efficiency is:


<a id="eq:vcc:etavol"></a>

\[
\eta_{\mathrm{vol}} = 1 + c - c\,r^{1/n},
\]


where $c$ is the clearance ratio (user input). The required piston displacement rate is:


<a id="eq:vcc:disp"></a>

\[
\dot{V}_{\mathrm{disp}} = \frac{\dot{V}_{\mathrm{suc}}}{\eta_{\mathrm{vol}}}.
\]


###### Heat exchanger sizing

Both the evaporator and condenser are sized by the LMTD method. For counter-current flow:


<a id="eq:vcc:lmtd"></a>

\[
\Delta T_{\mathrm{lm}} = \frac{\Delta T_1 - \Delta T_2}{\ln(\Delta T_1/\Delta T_2)},
\]


where $\Delta T_1$ and $\Delta T_2$ are the terminal temperature differences at each end of the exchanger.

The required heat transfer area is:


<a id="eq:vcc:area"></a>

\[
A = \frac{\dot{Q}}{U\,\Delta T_{\mathrm{lm}}},
\]


with the overall heat transfer coefficient:


<a id="eq:vcc:U"></a>

\[
\frac{1}{U} = \frac{1}{h_{\mathrm{ref}}} + \frac{1}{h_{\mathrm{fluid}}} + R_f,
\]


where $R_f$ is the combined fouling resistance (m$^2$ K/W).

####### Evaporator—refrigerant-side boiling. {#evaporatorrefrigerant-side-boiling.}

The boiling heat transfer coefficient is estimated by the Cooper reduced-pressure pool boiling correlation :


<a id="eq:vcc:cooper"></a>

\[
h_{\mathrm{boil}} = 55\,P_r^{0.12}\,\bigl(-\log_{10} P_r\bigr)^{-0.55}
                      M_w^{-0.5}\,q^{0.67},
\]


in W/m$^2$ K, where $P_r = P/P_c$ is the reduced pressure, $M_w$ is the molar mass in g/mol, and $q$ is the heat flux in W/m$^2$. Since $h = q/\Delta T_{\mathrm{lm}}$, substituting $q = h\,\Delta T_{\mathrm{lm}}$ and solving for $h$ yields:


<a id="eq:vcc:cooper_solved"></a>

\[
h_{\mathrm{boil}}
    = \left[55\,P_r^{0.12}\,\bigl(-\log_{10} P_r\bigr)^{-0.55} M_w^{-0.5}
      \,\Delta T_{\mathrm{lm}}^{0.67}\right]^{1/0.33}.
\]


The critical pressure of a refrigerant mixture is estimated by Kay’s mixing rule: $P_{c,\mathrm{mix}} = \sum_i z_i\,P_{c,i}$.

####### Condenser—refrigerant-side condensation. {#condenserrefrigerant-side-condensation.}

The condensing heat transfer coefficient is estimated by the Shah correlation  evaluated at a mean vapor quality of $x = 0.5$:


<a id="eq:vcc:shah"></a>

\[
h_{\mathrm{cond}} = h_L\left(0.55 + \frac{2.09}{P_r^{0.38}}\right),
\]


where $h_L$ is the liquid-phase single-flow heat transfer coefficient, approximated as 800 W/m$^2$ K for typical refrigerants at condensing conditions. This correlation is adequate for preliminary area estimation; rigorous sizing requires fluid-specific transport properties.

####### Process and cooling fluid sides. {#process-and-cooling-fluid-sides.}

The heat transfer coefficient on the process (evaporator) or cooling (condenser) fluid side may be supplied directly by the user or left at a conservative default value. When default values are used, the model adopts 2000 W/m$^2$ K for the process side (liquid) and selects between 80 W/m$^2$ K (gas/air stream, identified by low density) and 4000 W/m$^2$ K (liquid stream) for the cooling side.

##### Parameters Summary

Table [8](#tab:vcc:params) lists all user-configurable parameters.



<a id="tab:vcc:params"></a>



<table>
<caption>Parameters of the Vapor Compression Chiller.</caption>
<thead>
<tr>
<th style="text-align: left;">Parameter</th>
<th style="text-align: left;">Units</th>
<th style="text-align: left;">Default</th>
<th style="text-align: left;">Description</th>
</tr>
</thead>
<tbody>
<tr>
<td style="text-align: left;">Parameter</td>
<td style="text-align: left;">Units</td>
<td style="text-align: left;">Default</td>
<td style="text-align: left;">Description</td>
</tr>
<tr>
<td colspan="4" style="text-align: right;"><em>Continued on next page</em></td>
</tr>
<tr>
<td style="text-align: left;"></td>
<td style="text-align: left;"></td>
<td style="text-align: left;"></td>
<td style="text-align: left;"></td>
</tr>
<tr>
<td style="text-align: left;">Number of stages</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">1</td>
<td style="text-align: left;">Integer 1–3</td>
</tr>
<tr>
<td style="text-align: left;">Economizer mask</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">0</td>
<td style="text-align: left;">Bitmask: bit 0 = after stage 1, bit 1 = after stage 2</td>
</tr>
<tr>
<td style="text-align: left;">Refrigerant PP</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Property package name for refrigerant</td>
</tr>
<tr>
<td style="text-align: left;">Refrigerant comp.</td>
<td style="text-align: left;">mol/mol</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Mole fractions of flowsheet compounds</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Evaporator</em></td>
</tr>
<tr>
<td style="text-align: left;">Evaporator spec.</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Temperature</td>
<td style="text-align: left;">Temperature or Pressure</td>
</tr>
<tr>
<td style="text-align: left;">Evaporator <span class="math inline">\(T\)</span></td>
<td style="text-align: left;">K</td>
<td style="text-align: left;">258.15</td>
<td style="text-align: left;">Evaporator temperature (if spec = T)</td>
</tr>
<tr>
<td style="text-align: left;">Evaporator <span class="math inline">\(P\)</span></td>
<td style="text-align: left;">Pa</td>
<td style="text-align: left;">2  10<span class="math inline">\(^5\)</span></td>
<td style="text-align: left;">Evaporator pressure (if spec = P)</td>
</tr>
<tr>
<td style="text-align: left;">HX type (evap.)</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Shell&amp;Tube</td>
<td style="text-align: left;">Shell-and-tube or Plate</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(U\)</span> override (evap.)</td>
<td style="text-align: left;">W/m<span class="math inline">\(^2\)</span>K</td>
<td style="text-align: left;">0</td>
<td style="text-align: left;">0 = use correlation</td>
</tr>
<tr>
<td style="text-align: left;">Fouling (evap.)</td>
<td style="text-align: left;">m<span class="math inline">\(^2\)</span>K/W</td>
<td style="text-align: left;">2  10<span class="math inline">\(^{-4}\)</span></td>
<td style="text-align: left;">Combined fouling resistance</td>
</tr>
<tr>
<td style="text-align: left;">Process-side <span class="math inline">\(h\)</span></td>
<td style="text-align: left;">W/m<span class="math inline">\(^2\)</span>K</td>
<td style="text-align: left;">0</td>
<td style="text-align: left;">0 = use default (2000)</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Condenser</em></td>
</tr>
<tr>
<td style="text-align: left;">Condenser spec.</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Fixed <span class="math inline">\(T\)</span></td>
<td style="text-align: left;">Fixed T, fixed P, or approach <span class="math inline">\(\Delta T\)</span></td>
</tr>
<tr>
<td style="text-align: left;">Condenser <span class="math inline">\(T\)</span></td>
<td style="text-align: left;">K</td>
<td style="text-align: left;">318.15</td>
<td style="text-align: left;">(if spec = Fixed T)</td>
</tr>
<tr>
<td style="text-align: left;">Condenser <span class="math inline">\(P\)</span></td>
<td style="text-align: left;">Pa</td>
<td style="text-align: left;">12  10<span class="math inline">\(^5\)</span></td>
<td style="text-align: left;">(if spec = Fixed P)</td>
</tr>
<tr>
<td style="text-align: left;">Approach <span class="math inline">\(\Delta T\)</span></td>
<td style="text-align: left;">K</td>
<td style="text-align: left;">5.0</td>
<td style="text-align: left;">(if spec = Approach <span class="math inline">\(\Delta T\)</span>)</td>
</tr>
<tr>
<td style="text-align: left;">HX type (cond.)</td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Shell&amp;Tube</td>
<td style="text-align: left;">Shell-and-tube or Plate</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(U\)</span> override (cond.)</td>
<td style="text-align: left;">W/m<span class="math inline">\(^2\)</span>K</td>
<td style="text-align: left;">0</td>
<td style="text-align: left;">0 = use correlation</td>
</tr>
<tr>
<td style="text-align: left;">Fouling (cond.)</td>
<td style="text-align: left;">m<span class="math inline">\(^2\)</span>K/W</td>
<td style="text-align: left;">2  10<span class="math inline">\(^{-4}\)</span></td>
<td style="text-align: left;">Combined fouling resistance</td>
</tr>
<tr>
<td style="text-align: left;">Cooling-side <span class="math inline">\(h\)</span></td>
<td style="text-align: left;">W/m<span class="math inline">\(^2\)</span>K</td>
<td style="text-align: left;">0</td>
<td style="text-align: left;">0 = auto-detect</td>
</tr>
<tr>
<td colspan="4" style="text-align: left;"><em>Compressors (per stage, <span class="math inline">\(i\)</span> = 1, 2, 3)</em></td>
</tr>
<tr>
<td style="text-align: left;">Type<span class="math inline">\(_i\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">Centrifugal</td>
<td style="text-align: left;">Centrifugal or Reciprocating</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(\eta_{\mathrm{is},i}\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">0.75</td>
<td style="text-align: left;">Isentropic efficiency</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(\eta_{\mathrm{mech},i}\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">0.95</td>
<td style="text-align: left;">Mechanical efficiency</td>
</tr>
<tr>
<td style="text-align: left;">Speed<span class="math inline">\(_i\)</span></td>
<td style="text-align: left;">rpm</td>
<td style="text-align: left;">3000</td>
<td style="text-align: left;">Rotational speed (centrifugal)</td>
</tr>
<tr>
<td style="text-align: left;">Clearance<span class="math inline">\(_i\)</span></td>
<td style="text-align: left;">—</td>
<td style="text-align: left;">0.05</td>
<td style="text-align: left;">Clearance ratio (reciprocating)</td>
</tr>
</tbody>
</table>



##### Results

After a successful calculation the following results are available in the unit operation report and in the Results tab of the editor:

- Cycle: COP, evaporator duty, condenser duty, total shaft power, refrigerant molar flow rate, interstage pressures.

- Per stage: suction and discharge temperature, pressure, enthalpy, entropy; pressure ratio; shaft power; polytropic index and head; polytropic efficiency; volumetric suction flow; compressor-type-specific sizing result (specific speed or volumetric efficiency and displacement).

- Per economizer: flash vapor fraction, vapor injected, liquid carryover.

- Evaporator and condenser: LMTD, refrigerant-side $h$, fluid-side $h$, overall $U$, fouling resistance, required heat transfer area, terminal temperatures.

#### Air Cooler 2 {#sec:aircooler2}

##### Overview. {#overview.}

The **Air Cooler 2** models a forced-draft air-cooled heat exchanger in which a process fluid (hot side) flows through horizontal tubes and ambient air (cold side) is forced across the outside of the tube bundle by a fan. The model is a simplification of the TEMA shell-and-tube calculation method adapted for air cooling.

##### Stream Topology. {#stream-topology.}







| **Port**     | **Direction**     | **Description**            |
|:-------------|:------------------|:---------------------------|
| Fluid Inlet  | Inlet (material)  | Hot process fluid          |
| Power Inlet  | Inlet (energy)    | Fan shaft power (optional) |
| Fluid Outlet | Outlet (material) | Cooled process fluid       |



The air-side streams are generated internally by the model from the ambient conditions specified by the user and are not connected directly to the flowsheet.

##### Fan Air-Flow Model. {#fan-air-flow-model.}

The actual volumetric air flow is proportional to the fan speed relative to a reference condition:


<a id="eq:ac_fan"></a>

\[
\dot{V}_{\mathrm{air}} = \dot{V}_{\mathrm{ref}}
        \frac{N_{\mathrm{actual}}}{N_{\mathrm{ref}}}
\]


where $\dot{V}_{\mathrm{ref}}$ (m$^3$ s$^{-1}$) and $N_{\mathrm{ref}}$ (rpm) are user-specified reference values.

##### Calculation Modes. {#calculation-modes.}

Three calculation modes are available:

1.  **Specify Outlet Temperature** – the user fixes the hot-fluid outlet temperature $T_{h,\mathrm{out}}$; the model computes the heat load $Q$, the air outlet temperature $T_{c,\mathrm{out}}$, and the product $UA$.

2.  **Specify Geometry** – the user provides the tube-bundle geometry; the model iterates to find $T_{h,\mathrm{out}}$ and $T_{c,\mathrm{out}}$ using the simplified Tinker method  for the shell-and-tube calculation.

3.  **Specify Overall UA** – the user provides the product $UA$ (W K$^{-1}$); the model applies the $\varepsilon$-NTU method to find the outlet temperatures and $Q$.

##### Overall Heat Balance. {#overall-heat-balance.}

All modes use the fundamental heat-exchanger equations:


<a id="eq:ac_UAFLMTD"></a>

\[
Q = U A F \,\Delta T_{\mathrm{lm}}
\]




<a id="eq:ac_enthalpy"></a>

\[
Q = -\dot{m}_h \bigl(h_{h,\mathrm{out}} - h_{h,\mathrm{in}}\bigr)
      =  \dot{m}_c \bigl(h_{c,\mathrm{out}} - h_{c,\mathrm{in}}\bigr)
\]


where $U$ (W m$^{-2}$ K$^{-1}$) is the overall heat-transfer coefficient, $A$ (m$^2$) is the external tube surface area, and $F$ is the log-mean temperature-difference correction factor.

##### LMTD and Correction Factor. {#lmtd-and-correction-factor.}

The log-mean temperature difference for counter-current flow is


<a id="eq:ac_lmtd"></a>

\[
\Delta T_{\mathrm{lm}} =
        \frac{(T_{h,\mathrm{in}}-T_{c,\mathrm{out}})
            - (T_{h,\mathrm{out}}-T_{c,\mathrm{in}})}
             {\ln\!\dfrac{T_{h,\mathrm{in}}-T_{c,\mathrm{out}}}
                         {T_{h,\mathrm{out}}-T_{c,\mathrm{in}}}}
\]


The correction factor $F$ accounts for the multi-pass (1 shell–2 tube pass) geometry . Defining


<a id="eq:ac_RP"></a>

\[
R = \frac{T_{h,\mathrm{in}}-T_{h,\mathrm{out}}}
             {T_{c,\mathrm{out}}-T_{c,\mathrm{in}}}, \qquad
    P = \frac{T_{c,\mathrm{out}}-T_{c,\mathrm{in}}}
             {T_{h,\mathrm{in}}-T_{c,\mathrm{in}}}
\]


and the auxiliary variable $S = \dfrac{(1-RP)/(1-P)-1}{(1-RP)/(1-P)-R}$, the correction factor for $R \neq 1$ is


<a id="eq:ac_F"></a>

\[
F = \frac{\sqrt{R^2+1}\ln\!\dfrac{1-S}{1-RS}}
             {(R-1)\ln\!\dfrac{2-S\!\left(R+1-\sqrt{R^2+1}\right)}
                              {2-S\!\left(R+1+\sqrt{R^2+1}\right)}}
\]


When $R = 1$, the limiting form $F = \dfrac{S\sqrt{2}}{(1-S)\ln\!
\dfrac{2(1-S)+S\sqrt{2}}{2(1-S)-S\sqrt{2}}}$ is applied.

##### Tube-Side Heat Transfer (Gnielinski / Petukhov). {#tube-side-heat-transfer-gnielinski-petukhov.}

The internal heat-transfer coefficient $h_i$ (W m$^{-2}$ K$^{-1}$) is evaluated from the Gnielinski–Petukhov correlation :


<a id="eq:ac_hi"></a>

\[
h_i = \frac{k_h}{D_i}\,
          \frac{(f/8)(Re_t - 1000)\,Pr_t}
               {1 + 12.7\sqrt{f/8}\,(Pr_t^{2/3}-1)}
\]


The tube-side Darcy friction factor $f$ is computed using an explicit approximation ; for laminar flow ($Re \le 3250$), $f = 64/Re$.

##### Tube-Side Pressure Drop. {#tube-side-pressure-drop.}



<a id="eq:ac_dptube"></a>

\[
\Delta P_{\mathrm{tube}} = f\,\frac{L\,n_{\mathrm{pass}}}{D_i}
        \,\frac{\rho_h u_t^2}{2}
\]


where $u_t = \dot{m}_h / (\rho_h\,n_t\,\pi D_i^2/4)$ is the tube velocity and $n_t = N_{\mathrm{tubes}} / n_{\mathrm{pass}}$ is the number of tubes per pass.

##### Air-Side Heat Transfer (Holman). {#air-side-heat-transfer-holman.}

The external (air-side) heat-transfer coefficient $h_e$ (W m$^{-2}$ K$^{-1}$) over the tube bundle is evaluated from the Holman correlation :


<a id="eq:ac_he"></a>

\[
\frac{h_e D_e}{k_c} = 0.287\,Re_e^{0.61}\,Pr_c^{1/3}
\]


where $Re_e = G_s D_e / \mu_c$ is the shell-side Reynolds number and $G_s = \dot{m}_c / S_{\mathrm{flow}}$ is the mass flux through the minimum free-flow area $S_{\mathrm{flow}}$.

##### Overall Heat-Transfer Coefficient. {#overall-heat-transfer-coefficient.}

The overall heat-transfer coefficient based on the external area is


<a id="eq:ac_U"></a>

\[
\frac{1}{U} = \frac{D_e}{h_i D_i}
                + r_f \frac{D_e}{D_i}
                + \frac{D_e}{2k_t}\ln\frac{D_e}{D_i}
                + \frac{1}{h_e}
\]


where $r_f$ (m$^2$ K W$^{-1}$) is the tube-side fouling resistance and $k_t$ (W m$^{-1}$ K$^{-1}$) is the tube-wall thermal conductivity.

##### External Surface Area. {#external-surface-area.}



<a id="eq:ac_area"></a>

\[
A = N_{\mathrm{tubes}}\,\pi D_e (L - 2D_e)
\]


##### $\varepsilon$-NTU Method (Mode 3). {#varepsilon-ntu-method-mode-3.}

When $UA$ is specified, the outlet temperatures are found from the number of transfer units $NTU = UA / (W \cdot c_p)$ and the dimensionless temperature-change parameter $P$:


<a id="eq:ac_ntu"></a>

\[
P = \frac{1 - \exp[(R-1)\,NTU]}{1 - R\exp[(R-1)\,NTU]}
\]


where $R = W_c c_{p,c}/(W_h c_{p,h})$ is the heat-capacity-rate ratio. The outlet temperatures are updated iteratively until the heat load $Q$ converges.

##### Model Parameters. {#model-parameters.}







| **Parameter** | **Symbol** | **Unit** | **Default** |
|:---|:---|:---|:---|
| Tube internal diameter | $D_i$ | mm | 50 |
| Tube external diameter | $D_e$ | mm | 60 |
| Tube length | $L$ | m | 5 |
| Tube pitch | $p_t$ | mm | 80 |
| Number of tubes per shell | $N_{\mathrm{tubes}}$ | — | 160 |
| Passes per shell | $n_{\mathrm{pass}}$ | — | 1 |
| Tube fouling resistance | $r_f$ | m$^2$ K W$^{-1}$ | 0 |
| Tube wall roughness | $\varepsilon$ | mm | 0.045 |
| Tube thermal conductivity | $k_t$ | W m$^{-1}$ K$^{-1}$ | 70 |
| Air inlet temperature | $T_{c,\mathrm{in}}$ | K | 298.15 |
| Air pressure | $P_c$ | Pa | 101 325 |
| Reference air flow | $\dot{V}_{\mathrm{ref}}$ | m$^3$ s$^{-1}$ | 1 |
| Reference fan speed | $N_{\mathrm{ref}}$ | rpm | 100 |
| Actual fan speed | $N_{\mathrm{actual}}$ | rpm | 100 |
| Hot-side pressure drop | $\Delta P_h$ | Pa | 0 |



##### Results Reported. {#results-reported.}







| **Quantity**           | **Symbol**                 | **Unit**     |
|:-----------------------|:---------------------------|:-------------|
| Heat load              | $Q$                      | kW           |
| Overall UA             | $UA$                     | W K$^{-1}$ |
| Exchange area          | $A$                      | m$^2$      |
| LMTD                   | $\Delta T_{\mathrm{lm}}$ | K            |
| LMTD correction factor | $F$                      | —            |
| Air outlet temperature | $T_{c,\mathrm{out}}$     | K            |
| Maximum heat exchange  | $Q_{\max}$               | kW           |
| Exchanger efficiency   | $Q/Q_{\max}$             | —            |



#### Falling Film Evaporator {#sec:fffe}

##### Overview. {#overview.-1}

The **Falling Film Evaporator** (FFE) models a vertical shell-and-tube evaporator in which the feed liquid is distributed at the top of the heating tubes and descends as a thin film under gravity and co-current vapour flow. Heat applied to the tube exterior partially evaporates the film; the resulting vapour–liquid mixture is separated at the bottom of the calandria. A stepwise enthalpy-integration procedure is used, making the model independent of a specific heat-transfer correlation and relying instead on the thermodynamic property package for phase equilibrium and enthalpy calculations.

##### Stream Topology. {#stream-topology.-1}







| **Port**            | **Direction**     | **Description**                 |
|:--------------------|:------------------|:--------------------------------|
| Material Inlet      | Inlet (material)  | Liquid (or partial vapour) feed |
| Energy Inlet        | Inlet (energy)    | Heating duty $Q$ (kW)         |
| Vapour Outlet       | Outlet (material) | Evaporated vapour product       |
| Concentrated Liquid | Outlet (material) | Residual liquid concentrate     |



##### Calculation Modes. {#calculation-modes.-1}

1.  **Outlet Temperature** – the user specifies the exit temperature $T_{\mathrm{out}}$; the model integrates the enthalpy in $N_{\mathrm{steps}}$ equal temperature increments and reports the total heat duty.

2.  **Outlet Vapour Fraction** – the user specifies the exit vapour mole fraction $\psi_{\mathrm{out}}$; the model integrates in $N_{\mathrm{steps}}$ equal vapour-fraction increments.

3.  **Energy Stream** – the heat duty is read directly from the connected inlet energy stream; the model advances in enthalpy increments until the specified duty is consumed.

##### Stepwise Integration. {#stepwise-integration.}

The evaporation path is divided into $N_{\mathrm{steps}}$ intervals. At each step $i$, the stream is flashed at the local conditions $(T_i, P_i)$ or $(P_i, H_i)$ using the flowsheet property package. The heat added at step $i$ is


<a id="eq:ffe_dQ"></a>

\[
\delta Q_i = \dot{m}\,(h_i - h_{i-1})
\]


and the total heat duty is


<a id="eq:ffe_Q"></a>

\[
Q = \sum_{i=1}^{N_{\mathrm{steps}}} \delta Q_i
      = \dot{m}\,(h_{\mathrm{out}} - h_{\mathrm{in}})
\]


The pressure decreases linearly along the tube length:


<a id="eq:ffe_P"></a>

\[
P_i = P_{\mathrm{in}} - \frac{\Delta P_{\mathrm{tube}}}{N_{\mathrm{steps}}}\,i
\]


##### Phase Separation. {#phase-separation.}

After integration, the outlet stream is split at the exit vapour mass fraction $\psi_v$:


<a id="eq:ffe_vapour"></a><a id="eq:ffe_liquid"></a>

\[
\begin{align}
    \dot{m}_{\mathrm{vapour}} &= \dot{m}\,\psi_v
    \\
    \dot{m}_{\mathrm{liquid}} &= \dot{m}\,(1 - \psi_v)
\end{align}
\]


##### Evaporation Profile. {#evaporation-profile.}

At each integration step the model records a profile item containing: temperature, pressure, cumulative heat added, heat of vaporisation ($\Delta h_{\mathrm{vap}} = h_{\mathrm{vapour}} - h_{\mathrm{liquid}}$), and vapour/liquid phase fractions, densities, enthalpies, heat capacities, and thermal conductivities. This profile can be exported or used for detailed exchanger sizing calculations.

##### Model Parameters. {#model-parameters.-1}







| **Parameter** | **Symbol** | **Unit** | **Default** |
|:---|:---|:---|:---|
| Calculation mode | — | — | Outlet Vapour Fraction |
| Outlet temperature | $T_{\mathrm{out}}$ | K | 300 |
| Outlet vapour fraction | $\psi_{\mathrm{out}}$ | — | 0.3 |
| Number of integration steps | $N_{\mathrm{steps}}$ | — | 10 |
| Tube pressure drop | $\Delta P_{\mathrm{tube}}$ | Pa | 0 |



#### Energy Mixer {#sec:emixer}

##### Overview. {#overview.-2}

The **Energy Mixer** sums up to six inlet energy streams into a single outlet energy stream. The calculation is a simple energy balance with no user-adjustable parameters.

##### Stream Topology. {#stream-topology.-2}

Up to 6 energy inlets; 1 energy outlet.

##### Governing Equation. {#governing-equation.}



<a id="eq:emix"></a>

\[
\dot{E}_{\mathrm{out}} = \sum_{i=1}^{N} \dot{E}_i
\]


where $N \le 6$ is the number of connected inlet streams and $\dot{E}_i$ (kW) is the energy flow of each stream.

#### Energy Splitter {#sec:esplitter}

##### Overview. {#overview.-3}

The **Energy Splitter** divides one inlet energy stream into up to three outlet energy streams.

##### Stream Topology. {#stream-topology.-3}

1 energy inlet; 1–3 energy outlets.

##### Calculation Modes. {#calculation-modes.-2}

1.  **Split Ratios** – the user specifies the fractions $r_1, r_2, r_3$ with the constraint $\sum r_i = 1$. Each outlet receives


<a id="eq:esplit_ratio"></a>

\[
\dot{E}_i = r_i\,\dot{E}_{\mathrm{in}}
\]


2.  **Energy Flow Specification** – the user specifies the energy flows of the first (and optionally the second) outlet stream; the remaining stream is determined by the energy balance:


<a id="eq:esplit_spec"></a>

\[
\begin{align}
                  \dot{E}_1 &= \dot{E}_{1,\mathrm{spec}} \\
                  \dot{E}_2 &= \dot{E}_{2,\mathrm{spec}} \\
                  \dot{E}_3 &= \dot{E}_{\mathrm{in}}
                               - \dot{E}_{1,\mathrm{spec}}
                               - \dot{E}_{2,\mathrm{spec}}
    \end{align}
\]


    The specified values must not exceed the inlet energy flow.

#### Energy Stream Switch {#sec:esswitch}

##### Overview. {#overview.-4}

The **Energy Stream Switch** routes an inlet energy stream to one of two outlets depending on the result of a user-defined Boolean expression.

##### Stream Topology. {#stream-topology.-4}

1 energy inlet; 2 energy outlets.

##### Routing Logic. {#routing-logic.}

The user supplies an expression string that is evaluated at run time. The available variable is:







| **Variable** | **Meaning**       | **Unit** |
|:-------------|:------------------|:---------|
| `HF`         | Inlet energy flow | kW       |



The routing rule is:


<a id="eq:esswitch"></a>

\[
\dot{E}_{\mathrm{out\,1}} = \begin{cases}
        \dot{E}_{\mathrm{in}} & \text{if expression is TRUE} \\
        0 & \text{otherwise}
    \end{cases}
    \quad
    \dot{E}_{\mathrm{out\,2}} = \dot{E}_{\mathrm{in}} - \dot{E}_{\mathrm{out\,1}}
\]


Standard arithmetic and comparison operators (`+`, `-`, `*`, `/`, `>`, `<`, `=`, `AND`, `OR`, `NOT`) and all functions in `System.Math` are supported in the expression.

#### Material Stream Switch {#sec:msswitch}

##### Overview. {#overview.-5}

The **Material Stream Switch** routes an inlet material stream to one of two outlet material streams based on a user-defined Boolean expression evaluated against the inlet stream properties.

##### Stream Topology. {#stream-topology.-5}

1 material inlet; 2 material outlets.

##### Available Variables. {#available-variables.}







| **Variable** | **Meaning**                | **SI Unit**        |
|:-------------|:---------------------------|:-------------------|
| `T`          | Temperature                | K                  |
| `P`          | Pressure                   | Pa                 |
| `W`          | Mass flow rate             | kg s$^{-1}$      |
| `M`          | Molar flow rate            | mol s$^{-1}$     |
| `Q`          | Volumetric flow rate       | m$^3$ s$^{-1}$ |
| `VF`         | Vapour phase mole fraction | —                  |
| `LF`         | Liquid phase mole fraction | —                  |
| `SF`         | Solid phase mole fraction  | —                  |



##### Routing Logic. {#routing-logic.-1}



<a id="eq:msswitch"></a>

\[
\text{Outlet 1} \leftarrow \begin{cases}
        \text{inlet stream} & \text{if expression is TRUE} \\
        \text{zero flow}    & \text{otherwise}
    \end{cases}
    \quad
    \text{Outlet 2} \leftarrow \begin{cases}
        \text{zero flow}    & \text{if expression is TRUE} \\
        \text{inlet stream} & \text{otherwise}
    \end{cases}
\]


All stream properties (composition, temperature, pressure, enthalpy) are copied to the active outlet. The inactive outlet is assigned zero mass flow with the same composition and conditions as the inlet.

##### Example Expressions. {#example-expressions.}

- `T > 373.15` — route to Outlet 1 if temperature exceeds 100 $^\circ$C.

- `VF > 0.5 AND P < 500000` — route to Outlet 1 if the stream is predominantly vapour at sub-5 bar pressure.

- `W > 1.0` — route to Outlet 1 if mass flow exceeds 1 kg s$^{-1}$.

#### Material Stream Mapper {#sec:msmapper}

##### Overview. {#overview.-6}

The **Material Stream Mapper** copies an inlet material stream to an outlet stream and optionally overrides selected properties and per-compound flow rates. It is particularly useful for:

- connecting streams that use different compound lists (compound mapping);

- forcing a specific temperature, pressure, flow rate, or flash specification downstream without changing the upstream stream;

- scaling individual compound amounts by a fixed fraction or absolute value.

##### Stream Topology. {#stream-topology.-6}

1 material inlet (Source Stream); 1 material outlet (Target Stream).

##### Calculation Procedure. {#calculation-procedure.}

1.  Copy all properties from the inlet to the outlet: $\text{outlet} \leftarrow \text{inlet}$.

2.  Apply optional property overrides in the following order:

    1.  Flash specification (T-P, P-H, etc.)

    2.  Temperature override: $T_{\mathrm{out}} = T_{\mathrm{spec}}$

    3.  Pressure override: $P_{\mathrm{out}} = P_{\mathrm{spec}}$

    4.  Total flow override (mass, molar, or volumetric)

3.  Apply per-compound amount overrides for each compound $c$:


<a id="eq:msmap_comp"></a>

\[
\dot{m}_c^{\mathrm{out}} = \begin{cases}
                      \dot{m}_{c,\mathrm{spec}}  & \text{(Mass Flow mode)} \\
                      \dot{n}_{c,\mathrm{spec}} M_c & \text{(Molar Flow mode)} \\
                      \dot{m}_c^{\mathrm{in}} \times v_c/100 &
                                  \text{(Percentage of Source mode)}
                  \end{cases}
\]


    where $v_c$ is the user-specified percentage and $M_c$ is the molar mass of compound $c$.

4.  Apply compound mappings: transfer the mass flow of compound $c_1$ to compound $c_2$ and zero out $c_1$:


<a id="eq:msmap_remap"></a>

\[
\dot{m}_{c_2}^{\mathrm{out}} \mathrel{+}=
                      \dot{m}_{c_1}^{\mathrm{in}},
                  \qquad
                  \dot{m}_{c_1}^{\mathrm{out}} = 0
\]


After all overrides are applied, the outlet stream is flagged for recalculation so that the thermodynamic property package updates the equilibrium state consistently.

##### Override Options. {#override-options.}







| **Override** | **Description** |
|:---|:---|
| Flash specification | Change the flash type (e.g. T-P, P-H, P-VF) |
| Temperature | Fix outlet temperature to a specified value |
| Pressure | Fix outlet pressure to a specified value |
| Flow rate | Fix total mass, molar, or volumetric flow rate |
| Compound amount | Fix per-compound mass flow, molar flow, or percentage of inlet flow |
| Compound map | Rename/reassign a compound by mass-flow transfer |



#### Thermo Property Editor {#sec:thermopropedit}

##### Overview. {#overview.-7}

The **Thermo Property Editor** is a pass-through block that provides a graphical interface to view and modify the thermodynamic property package parameters (binary interaction coefficients, equation-of-state parameters, activity-model parameters, etc.) associated with a material stream in the flowsheet.

##### Stream Topology. {#stream-topology.-7}

1 material inlet; 1 material outlet.

##### Calculation. {#calculation.}

The outlet stream is a direct copy of the inlet stream ($\text{outlet} \leftarrow \text{inlet}$); no material transformation is performed. The block’s sole purpose is to expose the property package settings for interactive editing without requiring the user to navigate to the global property package editor.

##### Typical Use Cases. {#typical-use-cases.}

- Adjusting binary interaction parameters ($k_{ij}$) for a specific part of the flowsheet without altering the global property package.

- Inspecting pure-component parameters (critical properties, acentric factor, etc.) for components in a specific stream.

- Comparing property-package predictions with experimental data at a convenient location in the flowsheet.

#### Assumptions and Limitations (Additional Unit Operations)

1.  **Air Cooler 2 – pure-air assumption**: the cold-side fluid is treated as 100 % air using the Raoult property package. Humid air or alternative cooling media are not supported.

2.  **Air Cooler 2 – no fin model**: the current model computes the bare-tube external surface area (Eq. [\[eq:ac_area\]](#eq:ac_area)) and does not account for extended surfaces (fins). Users with finned tubes should apply an equivalent fin-efficiency correction to $A$ externally.

3.  **Air Cooler 2 – cross-flow correction**: the correction factor $F$ is derived for a one-shell, two-tube-pass arrangement. Other configurations require manual adjustment of $F$.

4.  **Falling Film Evaporator – no wall-temperature model**: the heat flux is distributed uniformly over the tube length (linear pressure drop); local dry-out or nucleation effects are not captured.

5.  **Falling Film Evaporator – equilibrium flash at each step**: the model assumes thermodynamic equilibrium at every integration step, which is equivalent to assuming an infinitely long residence time. Mass-transfer limitations are not modelled.

6.  **Stream switches – instantaneous evaluation**: the Boolean expression is evaluated once per solver iteration using the current stream properties. No hysteresis or deadband logic is built in; users requiring hysteresis must implement it via a custom Python script.

7.  **Material Stream Mapper – no energy balance**: property overrides (temperature, pressure, flow) are applied directly without checking an overall energy or mass balance around the block. It is the user’s responsibility to ensure that overridden values are physically consistent.

8.  **Premium requirement**: all additional unit operations require an active DWSIM Premium Supporter subscription.

