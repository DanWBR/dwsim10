# Bioprocess Operations

This section groups the bioprocess-oriented unit operations added to DWSIM in support of biorefinery, fermentation, and biologics workflows. Together they enable end-to-end modelling of three representative process trains: (i) second-generation ethanol / sustainable aviation fuel (Pretreatment → Enzymatic Hydrolysis → Fermentation → Centrifuge → UF/DF → Distillation); (ii) recombinant-protein and enzyme manufacturing (Fermentation → Centrifuge → Lysis → UF/DF → Chromatography → Crystallization); and (iii) renewable natural gas (Anaerobic Digester → Biogas Upgrader). The reactive units share a common Thermal Balance model (Isothermal / Adiabatic / Defined-Outlet-Temperature) and publish the net heat duty to the attached Heat Duty energy stream.

#### Biomass Pretreatment

###### Overview {#overview-23 .unnumbered}

The Pretreatment block converts a lignocellulosic slurry (cellulose + hemicellulose + lignin) into a pretreated slurry suitable for downstream enzymatic hydrolysis or direct fermentation. Four technologies are selectable via the **Technology** parameter: Dilute Acid, Steam Explosion, Alkaline, and Organosolv. Selecting one preloads a typical set of conversion fractions that the user may override.

###### Reactions and Stoichiometry {#reactions-and-stoichiometry .unnumbered}

- **Cellulose → Glucose** (hydrolysis): $(C_{6}H_{10}O_{5})_{n}+n\,H_{2}O\rightarrow n\,C_{6}H_{12}O_{6}$ — 1.111 g glucose per g cellulose consumed.

- **Glucose → HMF** (dehydration): C $_{6}$ H $_{12}$ O $_{6}\rightarrow$ C $_{6}$ H $_{6}$ O $_{3}+3\,H_{2}O$ — furanic inhibitor.

- **Hemicellulose → Xylose**: $(C_{5}H_{8}O_{4})_{n}+n\,H_{2}O\rightarrow n\,C_{5}H_{10}O_{5}$ — 1.136 g xylose per g xylan.

- **Xylose → Furfural**: C $_{5}$ H $_{10}$ O $_{5}\rightarrow$ C $_{5}$ H $_{4}$ O $_{2}+3\,H_{2}O$ .

- **Acetic acid release** — proportional to hemicellulose consumed, via the Acetic Acid Yield on Hemi parameter (default 0.12 g/g).

- **Lignin solubilization** — a user-set fraction of the lignin is converted to a Soluble Lignin pseudo-compound.

###### Ports {#ports .unnumbered}

- **Biomass Slurry (inlet)** — lignocellulose suspended in water.

- **Pretreated Slurry (outlet)** — sugars, inhibitors, residual solids and soluble lignin.

###### Input Parameters {#input-parameters-14 .unnumbered}

- **General**: Technology, Severity log R $_{0}$ , Residence Time, Solids Loading, optional Outlet Temperature.

- **Compound Roles**: Cellulose, Hemicellulose, Lignin, Glucose, Xylose, HMF, Furfural, Acetic Acid, Water, Soluble Lignin.

- **Conversion Fractions** (0–1): Cellulose → Glucose, Glucose → HMF, Hemicellulose → Xylose, Xylose → Furfural, Lignin Solubilization, Acetic Acid Yield on Hemi.

###### Results {#results-8 .unnumbered}

- Mass flows (kg/s) of cellulose and hemicellulose consumed, lignin solubilized, and glucose, xylose, HMF, furfural and acetic acid produced.

###### Practical Notes {#practical-notes .unnumbered}

- Pretreatment conversion fractions are keyed to the selected Technology; the Alkaline and Organosolv presets favour lignin removal over sugar release, while Dilute Acid maximises xylose recovery (at the cost of higher furfural).

- A low HMF/furfural yield is desirable because these inhibitors suppress microbial growth downstream; they are reported explicitly so that the fermentor model can account for them (e.g. via a UserScript kinetic expression in the Bioreactor).

#### Bioreactor

###### Overview {#overview-24 .unnumbered}

The Bioreactor models a microbial culture system in which biomass (cells) grows on a limiting substrate and, optionally, produces one or more metabolic products. Unlike the chemical reactors described above, the Bioreactor does not rely on the reactions defined in the flowsheet Reactions Manager. Instead, it uses Monod-family kinetic expressions together with user-selected compound roles (biomass, substrate, product, O $_{2}$ , CO $_{2}$ , N-source, water) to compute cell growth, substrate consumption, product formation and — for aerobic cultures — oxygen uptake and carbon dioxide evolution.

The Bioreactor supports three operating modes:

- **Continuous** — steady-state chemostat with constant feed and broth outlet flows.

- **Batch** — represents a discontinuous batch reactor as a cycle-averaged steady-state for the surrounding flowsheet. The microbial balances are integrated from the inlet substrate/biomass concentrations over the user-specified*Batch Duration* $t_{b}$ ; the final concentrations $S(t_{b}),\,X(t_{b}),\,P(t_{b})$ are written to the broth outlet, and the equivalent volumetric flow seen by the flowsheet is $Q_{eff}=V/t_{b}$ (one V-volume charge processed every $t_{b}$ seconds). The inlet stream’s mass flows are scaled by $Q_{eff}/Q_{in}$ so the cycle-averaged inlet matches $Q_{eff}$ ; a warning is emitted when $Q_{in}$ from the upstream stream differs from $V/t_{b}$ by more than 5%.

- **Fed-Batch** — same cycle-averaged steady-state representation as Batch in this simplified model: the reactor receives substrate continuously, fills to working volume $V$ over $t_{b}$ , and is discharged at the end. The equivalent flow seen by the flowsheet is again $Q_{eff}=V/t_{b}$ . A rigorous volume-ramping treatment (with diluting concentrations) is not yet implemented.

###### Kinetic Models {#kinetic-models .unnumbered}

The specific growth rate $\mu$ (1/h) can be described by one of the following expressions:

- **Monod**: $\mu=\mu_{max}\,S/(K_{s}+S)$ .

- **Contois**: $\mu=\mu_{max}\,S/(K_{s}\,X+S)$ — saturation constant scales with biomass concentration; useful for dense cultures.

- **Moser**: $\mu=\mu_{max}\,S^{n}/(K_{s}+S^{n})$ — generalization of Monod with exponent $n$ .

- **Haldane**: $\mu=\mu_{max}\,S/(K_{s}+S+S^{2}/K_{i})$ — includes substrate inhibition through $K_{i}$ .

- **User Script** — evaluate an IronPython script at every integration step; the script receives the current $X$ , $S$ , $P$ , $T$ , $p$ and must return the specific growth rate $\mu$ (1/s). This enables arbitrary models (Luedeking–Piret, Andrews, multi-substrate limitation, etc.). The following Python variables are available inside the script scope:

- `S` — substrate concentration (g/L)

- `X` — biomass concentration (g/L)

- `Px` — product concentration (g/L)

- `T` — temperature (K)

- `P` — pressure (Pa)

- `mu_max` — maximum specific growth rate (1/s)

- `Ks` — saturation constant (g/L)

- `Ki` — inhibition constant (g/L)

- `reactor` — reference to the Bioreactor object

- `Flowsheet` — reference to the flowsheet object

The script must set the variable`mu` to the computed specific growth rate in 1/s units.

###### Mass Balances {#mass-balances .unnumbered}

The unstructured model integrates the following ODE system:



\[
\begin{align*}
\frac{dX}{dt} & =\mu X-k_{d}X-\frac{F_{out}}{V}X\\
\frac{dS}{dt} & =-\frac{\mu X}{Y_{x/s}}-m_{s}X+\frac{F_{in}}{V}\bigl(S_{in}-S\bigr)\\
\frac{dP}{dt} & =Y_{p/s}\frac{\mu X}{Y_{x/s}}-\frac{F_{out}}{V}P
\end{align*}
\]


where $X$ , $S$ and $P$ are biomass, substrate and product concentrations (g/L), $Y_{x/s}$ and $Y_{p/s}$ are yield coefficients (g/g), $m_{s}$ is the maintenance coefficient (g/g/h), $k_{d}$ is the endogenous death rate (1/h) and $V$ is the working volume (m $^{3}$ ).

###### Aerobic Operation and Gas Exchange {#aerobic-operation-and-gas-exchange .unnumbered}

When the **Aerobic** flag is enabled, the Bioreactor automatically closes the elemental balance (C/H/O/N) of the growth stoichiometry using the assigned biomass, substrate, oxygen, carbon-dioxide, nitrogen-source and water compound formulas. The growth reaction is written in the generalised form



\[
\mathrm{Substrate}+a\,\mathrm{O_{2}}+b\,\mathrm{NH_{3}}\;\longrightarrow\;Y_{x/s}\,\mathrm{Biomass}+Y_{p/s}\,\mathrm{Product}+c\,\mathrm{CO_{2}}+d\,\mathrm{H_{2}O}
\]


and the coefficients $a$ , $b$ , $c$ , $d$ are solved from the four elemental balances. From these, the model reports:

- **Oxygen Uptake Rate (OUR)**, g O $_{2}$ /L/h

- **Carbon Dioxide Evolution Rate (CER)**, g CO $_{2}$ /L/h

- **Respiratory Quotient** $RQ=CER/OUR$ (mol/mol)

The volumetric oxygen transfer coefficient $k_{L}a$ (1/h) is used to check that the specified OUR is sustainable at the assumed dissolved-oxygen saturation; if not, the growth rate is capped by the maximum oxygen transfer.

###### Anaerobic Operation {#anaerobic-operation .unnumbered}

When the **Aerobic** flag is disabled, oxygen consumption is forced to zero and the elemental balance is solved in fermentative form: with $a=0$ , the oxygen balance closes via the water coefficient $d$ , which may turn negative when the substrate is more oxidised than is needed to deliver the assumed CO $_{2}$ , biomass and product slate (i.e. water is net-consumed, Buswell-style). The hydrogen balance is no longer enforced strictly, since hydrogen leaves the reactor implicitly as un-modelled reduced products (H $_{2}$ , volatile fatty acids, alcohols); to keep the balance closed, the user should configure the **Product** compound and the **Y $_{p/s}$** yield consistently with the intended fermentation (e.g. ethanol with Y $_{p/s}\approx0.51$ g/g for glucose). In anaerobic mode the metabolic heat term is set to zero, since most of the combustion enthalpy is carried out by the reduced products.

###### Outlet Streams {#outlet-streams .unnumbered}

The Bioreactor exposes two material outlets:

- **Broth Outlet** (port 0, lateral) — carries the bulk liquid phase: residual substrate, biomass, product, water and dissolved nitrogen source.

- **Offgas Outlet** (port 1, top) — carries the volatile species: CO $_{2}$ produced metabolically and, in aerobic mode, any oxygen that was not consumed. The off-gas port is optional; if no stream is connected, these components remain in the broth dictionary but are not written downstream.

###### Thermal Balance {#thermal-balance .unnumbered}

Fermentations are exothermic: in aerobic cultures the metabolic heat release is closely tied to the oxygen uptake rate (OUR). The Bioreactor estimates the metabolic heat from the Cooney-Wang-Mateles correlation,



\[
\dot{Q}_{met}=\Delta H_{O_{2}}\cdot\dot{n}_{O_{2},\,consumed}\quad\mathrm{with}\quad\Delta H_{O_{2}}\approx460\;\mathrm{kJ/mol\,O_{2}}
\]


where $\dot{n}_{O_{2}}$ is the molar oxygen uptake rate obtained from the elemental-balance step. The specific heat per mole of O $_{2}$ is a user input (default 460 kJ/mol). For anaerobic cultures the metabolic heat term is set to zero, because most of the combustion enthalpy leaves the reactor as reduced products (ethanol, lactate, etc.).

Three thermal modes are available via the **Thermal Mode** parameter:

- **Isothermal** — the broth temperature is held at the inlet temperature and the required net duty is reported as cooling (negative $\dot{Q}_{duty}$ ). This is the default and the most common industrial operation mode.

- **Adiabatic** — no external duty is applied; the metabolic heat raises the outlet temperature according to $\Delta T=\dot{Q}_{met}/(\dot{m}\,c_{p})$ for continuous operation, or $\Delta T=\dot{Q}_{met}\tau/(m_{holdup}\,c_{p})$ for batch / fed-batch.

- **Defined Outlet Temperature** — the user prescribes the outlet temperature and the Bioreactor back-computes the net heat duty from the enthalpy balance $\dot{m}\,c_{p}(T_{out}-T_{in})=\dot{Q}_{met}+\dot{Q}_{duty}$ .

In all modes the computed $\dot{Q}_{duty}$ (kW, positive = heating, negative = cooling) is published through the attached **Heat Duty** energy stream.

###### Compounds and the Biomass Compound Creator {#compounds-and-the-biomass-compound-creator .unnumbered}

Biomass is treated as a regular DWSIM compound with a user-defined elemental formula (typically C $_{a}$ H $_{b}$ O $_{c}$ N $_{d}$ , e.g. CH $_{1.8}$ O $_{0.5}$ N $_{0.2}$ for a representative dry cell). A helper form,*Biomass Compound Creator*, is provided under the compound database tools: the user enters the formula, C-molecular weight and a few kinetic defaults, and the tool generates a JSON compound definition (including a Heijnen-style estimate of the heat of formation).

A small database of common micro-organisms ships with DWSIM and can be added to a simulation like any regular compound:*Escherichia coli*, *Saccharomyces cerevisiae* (baker’s yeast), *Pichia pastoris* (Komagataella phaffii), *CHO* (mammalian cells), generic photosynthetic *Microalgae*, mixed-culture *Activated Sludge*, and a generic *Biomass* placeholder (CH $_{1.8}$ O $_{0.5}$ N $_{0.2}$ ).

###### Ports {#ports-1 .unnumbered}

The Bioreactor exposes two inlet and two outlet streams:

- **Inlet (Feed)** — liquid feed (substrate, inoculum, nutrients, water).

- **Sparger Gas Inlet (Optional)** — gas feed (typically air) for aerobic cultures; supplies O $_{2}$ and sweeps CO $_{2}$ .

- **Broth Outlet** — liquid outlet containing residual substrate, biomass and products.

- **Offgas Outlet** — gas outlet carrying CO $_{2}$ , unreacted O $_{2}$ and water vapour.

- **Heat Duty** (energy stream) — publishes the net thermal duty $\dot{Q}_{duty}$ (kW, positive = heating, negative = cooling) required to satisfy the selected thermal mode.

###### Input Parameters {#input-parameters-15 .unnumbered}

The editor is organized in four tabs (Connections, Parameters, Results, Annotations). Input parameters are:

- **General**: Property Package, Operating Mode, Kinetic Model, Aerobic flag, Working Volume, Batch Duration.

- **Compound Roles**: Biomass, Substrate, Product, Oxygen, CO $_{2}$ , N-Source and Water compounds.

- **Kinetics**: $\mu_{max}$ (1/h), $K_{s}$ (g/L), $K_{i}$ (g/L, Haldane), Moser exponent $n$ .

- **Stoichiometry / Yields**: $Y_{x/s}$ (g/g), $Y_{p/s}$ (g/g), maintenance $m_{s}$ (g/g/h), death rate $k_{d}$ (1/h).

- **Oxygen Transfer**: $k_{L}a$ (1/h).

- **Thermal Balance**: Thermal Mode (Isothermal / Adiabatic / DefinedOutletTemperature), Heat per mol O $_{2}$ (J/mol, Cooney default 460 000), Outlet Temperature Setpoint (K, used only when Thermal Mode = DefinedOutletTemperature).

- **User Kinetics Script**: flowsheet script to evaluate when Kinetic Model is set to UserScript.

###### Results {#results-9 .unnumbered}

The Results tab reports, after calculation:

- Outlet biomass, substrate and product concentrations (g/L).

- Average specific growth rate $\mu$ (1/h).

- Oxygen uptake and CO $_{2}$ evolution rates (g/L/h) and respiratory quotient.

- Metabolic heat release $\dot{Q}_{met}$ (kW) estimated from the Cooney correlation.

- Net heat duty $\dot{Q}_{duty}$ (kW, positive = heating, negative = cooling) published to the Heat Duty energy stream.

- Outlet temperature (K) — equal to the inlet temperature in Isothermal mode, solved from the enthalpy balance in Adiabatic mode, or equal to the user setpoint in Defined Outlet Temperature mode.

###### Practical Notes {#practical-notes-1 .unnumbered}

- Supply a consistent set of compound roles: at minimum the Biomass and Substrate compounds must be set; Oxygen, CO $_{2}$ , N-source and Water are only required when **Aerobic** is enabled.

- For Batch and Fed-Batch modes, the Batch Duration $t_{b}$ defines both the integration interval and the cycle period of the equivalent steady-state representation: the equivalent volumetric flow exchanged with the flowsheet is $Q_{eff}=V/t_{b}$ . To keep the flowsheet mass balance closed, set the upstream stream’s volumetric flow so that $Q_{in}\approx V/t_{b}$ (a warning is emitted otherwise). Shorter durations with tighter tolerances improve accuracy near the exponential-growth phase.

- When using a UserScript kinetic model, keep the script short and side-effect-free: it is called at every integration step.

- If the elemental balance cannot be closed (e.g. a required compound role is missing or its formula is incomplete), the Bioreactor falls back to a purely kinetic balance without reporting OUR, CER and RQ.

###### Dynamic Trajectories and Charts {#dynamic-trajectories-and-charts .unnumbered}

Every Bioreactor calculation now records the full internal trajectory of the forward-Euler integrator in a transient`LastTrajectory` object. For the growth-kinetics modes (Monod, Contois, Moser, Haldane) the stored series are X(t), S(t), P(t), specific growth rate μ(t), substrate and product specific rates q $_{S}$ / q $_{P}$ , and the metabolic fluxes OUR, CER and RQ. For the Enzymatic Hydrolysis mode the series instead follow Cellulose, Hemicellulose, Glucose and Xylose concentrations over time. Two new buttons at the bottom of the Parameters tab open the results: **Results & Charts…** launches a modal OxyPlot dialog with tabs for Growth, Specific Rates, Metabolic Fluxes (or Hydrolysis), a Custom pick-and-plot tab, and a Data Table grid; its toolbar exports CSV, PNG (1600×900) of the current chart, and Copy-CSV-to-Clipboard. **Export Time Series…** is a one-click CSV shortcut. The trajectory is capped at 2000 samples with geometric interval doubling and is marked` XmlIgnore` /`JsonIgnore` so it is re-populated on every Calculate and never persisted to the flowsheet file.

#### CFB Fast Pyrolysis Reactor

###### Overview {#overview-25 .unnumbered}

The Circulating Fluidized Bed (CFB) Fast Pyrolysis Reactor models a dilute-riser reactor for the rapid thermochemical conversion of dry lignocellulosic biomass into bio-oil, non-condensable gas, and char. Hot circulating sand (typically silica or olivine) supplies the endothermic pyrolysis heat; the sand is either returned at a user-fixed temperature (external mode) or re-heated by combusting the produced char in a coupled regenerator (internal char combustor mode). The block is intended as a drop-in replacement for Aspen/CHEMCAD-style equilibrium black boxes that cannot resolve axial temperature, residence-time, or yield profiles.

###### Kinetic scheme {#kinetic-scheme .unnumbered}

The reactor uses a reduced version of the Ranzi et al. multi-step lignocellulose pyrolysis scheme. Cellulose, hemicellulose and lignin each first activate to an intermediate solid (CELLA, HCEA, LIGA) and then decompose along parallel branches to primary vapors (bio-oil lump), non-condensable gas, and char, with a secondary vapor-cracking step BIO_OIL $\rightarrow$ GAS that becomes significant for vapor residence times above 2 s. Each elementary step is first-order in the corresponding solid or vapor mass fraction and follows an Arrhenius form $k=A\exp(-E_{a}/RT)$ . The default pre-exponential factors and activation energies reproduce the 65–75 wt % bio-oil yield envelope reported for pine and hardwood at 773–823 K and vapor residence times below 2 s, and degrade gracefully back to the classical Shafizadeh three-lump behaviour when the intermediate-species activation rates are large. Char properties use the correlations of Debiagi et al..

###### Hydrodynamics and energy balance {#hydrodynamics-and-energy-balance .unnumbered}

The riser is discretised into*NumAxialCells* (default 50) one-dimensional PFR cells of length $\Delta z=H/N$ . Each cell is solved with an RK2 sub-stepper for the coupled species mass balances and a cell-by-cell energy balance between the solids (biomass + intermediates + char) and the lifted carrier gas plus sand. Solids hold-up is taken as a user-set dilute-riser value (default 0.05, Geldart-A sand). The hot-sand stream enters at*SandInletTemperature_K* with a sand-to-biomass mass ratio*SandToBiomassRatio* (default 15 kg/kg) and is cooled adiabatically (minus a*HeatLossFraction*) as it supplies the $\Delta H_{pyr}\approx250$ kJ/kg endotherm. The per-cell gas velocity is inflated with the cumulative vapor generated upstream, so the effective vapor residence time reported at the outlet self-corrects for the mass inflation from primary devolatilisation.

###### Optional internal char combustor {#optional-internal-char-combustor .unnumbered}

When*SandMode = InternalCharCombustor* the reactor iterates the sand-to-biomass ratio (starting from the user-provided initial guess) until the char combustor duty matches the net riser endotherm within 0.5 %. The combustor is sized stoichiometrically on the produced char (assumed pure carbon), with a user-set*CharCombustorExcessAir* (default 20 %) and a small heat-loss fraction; the adiabatic flue temperature and air mass flow are reported on the Results tab and exported to the flue outlet connector when present. In this mode the energy connector carries zero duty (the system is autothermal); the external mode reports the net pyrolysis duty on the energy connector as in a classical heated-bed configuration.

###### Axial trajectory, charts and export {#axial-trajectory-charts-and-export .unnumbered}

Every Calculate records the full axial profile — temperature $T(z)$ , cumulative vapor residence time $\tau(z)$ , solid and gas velocities, solids hold-up, and the 9-species mass-fraction tracks (CELL, CELLA, HCE, HCEA, LIG, LIGA, CHAR, BIO_OIL, GAS) — in a transient`LastTrajectory` property of type`CFBPyrolysisTrajectoryResult`. The Parameters tab of the editor exposes two buttons:*Results“ Charts…* opens an OxyPlot-based multi-tab dialog (Temperature, Species, Hydrodynamics, Vapor Residence, Summary, Custom, Data Table) with PNG export at 1600×900 and CSV export of the full profile plus the outlet summary;*Export Time Series…* is a one-click CSV shortcut. The trajectory is marked`XmlIgnore` /`JsonIgnore` and is re-populated on every Calculate (not persisted to the flowsheet file).

#### Anaerobic Digester

###### Overview {#overview-26 .unnumbered}

The Anaerobic Digester converts a single user-selected Organic Substrate compound into biogas (CH $_{4}$ + CO $_{2}$ ) plus sludge biomass, using Buswell-type stoichiometry driven by the substrate’s elemental formula and scaled by a user-specified COD-removal efficiency. The model is a black-box Tier-A representation suitable for flowsheet-level mass and energy balances and biogas yield estimation.

###### Stoichiometry and COD Balance {#stoichiometry-and-cod-balance .unnumbered}

For a substrate C $_{a}$ H $_{b}$ O $_{c}$ N $_{d}$ , the Buswell equation gives



\[
C_{a}H_{b}O_{c}N_{d}S_{e}+\bigl(a-\tfrac{b}{4}-\tfrac{c}{2}+\tfrac{3d}{4}+\tfrac{e}{2}\bigr)\,H_{2}O\;\longrightarrow\;\bigl(\tfrac{a}{2}-\tfrac{b}{8}+\tfrac{c}{4}+\tfrac{3d}{8}+\tfrac{e}{4}\bigr)\,CO_{2}+\bigl(\tfrac{a}{2}+\tfrac{b}{8}-\tfrac{c}{4}-\tfrac{3d}{8}-\tfrac{e}{4}\bigr)\,CH_{4}+d\,NH_{3}+e\,H_{2}S
\]


The theoretical COD of the substrate is computed from its formula and used to scale the reaction extent: actual COD removed = feed COD × **CODRemovalEfficiency**. A fraction of the removed COD is partitioned to biomass synthesis via the **Biomass Yield on COD** parameter (≈ 1.42 g COD per g VSS); the remainder drives biogas production. The user may override the CH $_{4}$ mole fraction in the biogas (otherwise it follows the Buswell split).

Sulfur bound in the substrate formula needs no separate treatment here: Buswell releases it as H $_{2}$ S and the $-e/4$ term in the CH $_{4}$ coefficient is the methane it costs. Sulfate, which is not part of the molecule, is handled by the sulfur balance below.

###### Ports {#ports-2 .unnumbered}

- **Feed (inlet)** — organic wastewater, sludge or agricultural residue slurry.

- **Effluent (outlet)** — stabilised liquid with residual COD, NH $_{3}$ and biomass (sludge).

- **Biogas (outlet)** — CH $_{4}$ / CO $_{2}$ mixture, plus H $_{2}$ S if the feed carries sulfur (see Sulfur Balance below), plus any dissolved gas carried in from the feed.

- **Heat Duty (energy stream)** — net thermal duty required to hold the selected Thermal Mode.

###### Input Parameters {#input-parameters-16 .unnumbered}

- **General**: Working Volume, HRT.

- **Compound Roles**: Organic Substrate, Methane, CO $_{2}$ , Water, NH $_{3}$ , Biomass (sludge), H $_{2}$ S. The last one is optional: without it the sulfur balance still runs and reports, but the H $_{2}$ S is not written into the outlet streams. ADM1-S adds one further optional role, the sulfate carrier, which takes the sulfate the reducers did not respire out with the effluent; sulfuric acid or any sulfate salt will do, since the sulfur is converted through the compound’s own elemental formula.

- **Digester Parameters**: COD Removal Efficiency (0–1), Biomass Yield on COD (g VSS / g COD), Methane Mole Fraction Override.

- **Thermal Balance**: Thermal Mode, Heat per g COD Removed (J/g).

###### Sulfur Balance {#sulfur-balance .unnumbered}

Standard ADM1 (Batstone et al. 2002) leaves sulfate reduction out of scope, so none of the four models tracks sulfur on its own. DWSIM adds a sulfur balance on top, available in all four: you declare the sulfur entering the digester, and the model mineralises it to sulfide and splits it between the biogas (as H $_{2}$ S) and the effluent. This matters for biogas because H $_{2}$ S is what sizes the primary desulfurisation stage ahead of any upgrading, and substrates such as pig slurry carry a lot of it.

Sulfate and organic sulfur are declared separately because they behave differently, and the distinction is the whole point:

- **Sulfate sulfur** carries no COD of its own. Reducing it to sulfide takes eight electrons per sulfur — 64 kg COD per kmol S, or 2 g COD per g S — drawn from the very pool that would otherwise have made methane. Expect a real loss of CH $_{4}$ .

- **Organic sulfur** arrives already reduced inside the substrate molecule. Mineralising it is not a redox step, so it makes H $_{2}$ S at no cost in methane.

- **Inputs**: Influent Sulfate Sulfur (mg S/L, as S rather than as SO $_{4}$ ), Substrate Organic Sulfur (g S/kg substrate), Assumed pH for Sulfide Speciation, and the H $_{2}$ S compound role.

Leave Substrate Organic Sulfur at $-1$ to read it from the substrate compound’s elemental formula, which keeps it consistent with the theoretical COD; set it to a value only to declare sulfur the formula omits. Only undissociated H $_{2}$ S is volatile, and its pK $_{a1}$ of about 7 sits right in the operating range, so the gas/liquid split is strongly pH-dependent: the Assumed pH is used by BlackBox and ADM1-Lite, which have no mechanistic pH, while ADM1-Full and ADM1-S ignore it and use their own charge-balance pH.

Sulfide already dissolved in the feed joins the same pool rather than passing through untouched. In BlackBox, ADM1-Lite and ADM1-Full this balance is stoichiometric: the electron accounting is right, but every sulfate fed is taken to be reduced and there are no population dynamics behind it. The kinetic competition between sulfate-reducing bacteria and methanogens for hydrogen and acetate is the subject of the fourth model, ADM1-S, described at the end of this section.

###### Results {#results-10 .unnumbered}

- Feed COD, COD removed, substrate consumed, biogas molar flow, CH $_{4}$ and CO $_{2}$ mass flows, CH $_{4}$ mole fraction, specific CH $_{4}$ yield (Nm³/kg COD), sludge production, metabolic heat, net heat duty, outlet temperature.

- Sulfur: H $_{2}$ S in Biogas (ppmv, dry basis — the number that sizes the desulfurisation stage), H $_{2}$ S mass flow, and Dissolved Sulfide in the effluent (kg S/m³).

###### Practical Notes {#practical-notes-2 .unnumbered}

- Typical mesophilic AD runs at 35 °C with CODRemovalEfficiency 0.75–0.90 and BiomassYield_gVSS 0.04–0.10. Thermophilic operation (55 °C) gives higher removal but requires significant Heat Duty.

###### ADM1-Lite Model (reduced ADM1) {#adm1-lite-model-reduced-adm1 .unnumbered}

A second fidelity mode (**Digester Model = ADM1Lite**) replaces the Buswell black-box stoichiometry with a reduced version of the IWA Anaerobic Digestion Model No. 1. The reduced model tracks four lumped soluble substrates — sugars S $_{s}$ , volatile fatty acids S $_{VFA}$ , acetate S $_{Ac}$ and hydrogen S $_{H_{2}}$ — and four microbial populations (hydrolysers/acidogens X $_{hyd}$ , acetogens X $_{ace}$ , acetoclastic methanogens X $_{am}$ , hydrogenotrophic methanogens X $_{hm}$ ). Monod kinetics drive each uptake step, with non-competitive H $_{2}$ inhibition on acetogenesis:



\[
\rho_{j}=k_{m,j}\,\frac{S_{j}}{K_{S,j}+S_{j}}\,X_{j}\,I_{H_{2}},\qquad I_{H_{2}}=\frac{1}{1+S_{H_{2}}/K_{I,H_{2}}}
\]


Biomass grows with yields Y $_{j}$ on each substrate and decays at first order (k $_{dec}$ ). The system is integrated over 10× HRT (continuous) or BatchDuration (batch) via a forward-Euler stepper (20 000 steps) until a quasi-steady outlet composition is obtained; CH $_{4}$ and CO $_{2}$ generation are then computed from the COD balance (4 g COD / g CH $_{4}$ ). A crude pH estimate is exposed, based on accumulated VFA concentration.

###### ADM1-Lite Inputs {#adm1-lite-inputs .unnumbered}

- **Initial state (8)**: S $_{s,0}$ , S $_{VFA,0}$ , S $_{Ac,0}$ , S $_{H_{2},0}$ , X $_{hyd,0}$ , X $_{ace,0}$ , X $_{am,0}$ , X $_{hm,0}$ (all in g COD / L or g VSS / L).

- **Kinetics (15)**: k $_{hyd}$ , k $_{m,su}$ / K $_{S,su}$ / Y $_{su}$ , k $_{m,vfa}$ / K $_{S,vfa}$ / Y $_{ace}$ , K $_{I,H_{2}}$ , k $_{m,ac}$ / K $_{S,ac}$ / Y $_{am}$ , k $_{m,h_{2}}$ / K $_{S,h_{2}}$ / Y $_{hm}$ , k $_{dec}$ . Defaults follow Batstone mesophilic parameter values.

###### ADM1-Lite Results {#adm1-lite-results .unnumbered}

- Final S $_{s}$ , S $_{VFA}$ , S $_{Ac}$ , S $_{H_{2}}$ , X $_{hyd}$ , X $_{ace}$ , X $_{am}$ , X $_{hm}$ ; crude pH estimate. Biogas mass flows, COD balance and thermal plumbing are shared with the BlackBox path.

###### ADM1-Full Model (Batstone 2002 / BSM2) {#adm1-full-model-batstone-2002-bsm2 .unnumbered}

A third fidelity mode (**Digester Model = ADM1Full**) activates the complete IWA Anaerobic Digestion Model No. 1 as specified by Batstone et al. and implemented per Rosen & Jeppsson for the IWA BSM2 benchmark. The ADM1-Full path integrates 31 dynamic state variables — 12 soluble species (monosaccharides, amino acids, LCFA, valerate, butyrate, propionate, acetate, dissolved H $_{2}$ , dissolved CH $_{4}$ , inorganic C, inorganic N, soluble inerts), 12 particulates (composites, carbohydrates, proteins, lipids, seven biomass populations, particulate inerts), 2 ion surrogates (S $_{cat}$ , S $_{an}$ ), 4 gas-phase species (H $_{2}$ , CH $_{4}$ , CO $_{2}$ , H $_{2}$ S) and dissolved inorganic sulfide — the last two being the sulfur extension, which reduces to standard ADM1 exactly when no sulfur is declared; the state vector carries five more (the dissolved sulfate and four sulfate-reducing populations) that only ADM1-S moves — under 19 biochemical processes, Hill pH envelopes on the three acidogen/acetoclast/hydrogenotroph groups, free-NH $_{3}$ and H $_{2}$ inhibition, and gas-liquid transfer with per-species Henry constants. Inorganic carbon and nitrogen are closed over all 19 processes from the carbon and nitrogen content of every component, so the CO $_{2}$ in the biogas is the carbon the reactions actually release and the ammonia climbs as cell lysis returns it. An algebraic charge-balance pH is solved by Newton-Raphson (with bisection fallback) at every ODE stage, and the acid-base and Henry constants are corrected from their 25 °C reference to the reactor temperature by van’t Hoff, so setting the operating temperature moves the whole chemistry with it.

The system is advanced with a Cash-Karp embedded RK45 adaptive integrator. Dissolved H $_{2}$ is not integrated but solved from its own mass balance at each stage (the DAE form of Rosen & Jeppsson): it sits near $2.5\times10^{-7}$ kg COD/m³ against a half-saturation of $7\times10^{-6}$ , which gives it a time constant of a fraction of a second in a model whose retention time is weeks, and an explicit method has to resolve the fastest mode it is given. Integrating it directly pins the step at about $2\times10^{-6}$ d and the run never finishes.

The trajectory result carries whether the run converged, the time it actually reached, the step count and, if it stopped early, why. A run that fails to reach its horizon raises an error rather than reporting a half-finished transient as an answer. Sampling is decoupled from the adaptive step — accepted steps are interpolated onto a fixed sample grid (default 500 points, capped at 2000) for reporting and charting.

Validation: the model reproduces the published BSM2 steady state (Rosen & Jeppsson 2006; 3400 m³ liquid, 178.47 m³/d, 35 °C) to within 0.1 % on pH, 0.8 % on biogas flow and 1.3 % on CH $_{4}$ fraction, and converges back to it from a perturbed start.

###### What the Feed Stream Must Supply {#what-the-feed-stream-must-supply .unnumbered}

- A single connected MaterialStream on Input 0 with fully specified T, P and mass flow.

- A non-zero liquid volumetric flow (taken from the liquid phase, with a fallback to overall volumetric flow). This becomes Q $_{in}$ for the CSTR mass balance.

- The Organic Substrate compound must be present in the stream with a positive mass flow and a computable theoretical COD (i.e. C/H/O/N atoms defined in its ConstantProperties; S is read too when present, and contributes to the COD). Feed COD is obtained as m $_{sub}\times\text{TheoreticalCOD}$ .

- Methane and CO $_{2}$ compounds must be mapped (used to write the biogas outlet). Water and NH $_{3}$ mappings are optional but recommended for realistic liquid-effluent composition; the Biomass (sludge) compound is optional and, when set, receives the total biomass mass flow (ΣX $_{*}$ ) in the liquid outlet.

- Two product MaterialStreams must be connected to the digester outputs: Output 0 = liquid effluent (digestate), Output 1 = biogas. An Energy Stream may be optionally attached to pick up the thermal duty.

The operating flag **UseInfluentFromFeedStream** (in the ADM1 Parameters dialog, Operating tab) selects how the influent vector S $_{in}$ is built: when **true** the entire feed COD is routed to the hydrolysable-carbohydrate slot X $_{ch}$ (pragmatic default for any organic substrate), while inorganic C/N, cations, anions and inerts come from the JSON defaults. When **false**, the full fine-grained influent (Sin\_ $\ast$ , Xin\_ $\ast$ ) and the flow Q $_{in}$ are both taken from the parameter set, not from the stream — this is the mode used for BSM2-style runs, where the influent is the benchmark’s rather than the flowsheet’s. The operating temperature still follows the feed stream either way.

Note that in this mode there is no influent sulfate slot: the parameter set carries Sin_IS, which is sulfide, already reduced, and the COD debit does not apply to it because you are stating the influent state directly. To model sulfate reduction and the methane it costs, use the feed-stream mode and the Influent Sulfate Sulfur input.

###### ADM1-Full Dialogs and Output {#adm1-full-dialogs-and-output .unnumbered}

When **Digester Model = ADM1Full** is selected, three buttons appear at the bottom of the Parameters tab in the editing form:

- **ADM1 Parameters…** — opens a modal dialog with seven tabs (Stoichiometry, Kinetics, Inhibition & pH, Physicochemical, Initial Conditions, Operating, Numerics) exposing every one of the ~100 parameters. Note that the Physicochemical constants are quoted at 25 °C and corrected to the operating temperature by the model, so editing them means editing the chemistry at 25 °C. Footer actions: Reset to Benchmark, Load JSON, Save JSON, OK, Cancel. Parameter sets round-trip via a JSON string persisted on the unit op and therefore travel with the flowsheet file.

- **ADM1 Results & Charts…** — opens a modal dialog containing OxyPlot time-series charts on tabs for Biogas (Q $_{gas}$ , x $_{CH_{4}}$ , x $_{CO_{2}}$ , x $_{H_{2}}$ ), VFAs & Acids, pH & Inorganic, Biomass, Substrates, Dissolved gases, plus a Custom tab (pick-and-plot any series), a Data Table tab (full trajectory as a grid) and a toolbar for Export CSV, Export PNG (current chart), and Copy CSV to Clipboard.

- **Export Time Series…** — shortcut that writes the full 29-state trajectory (plus derived pH and biogas composition) to a CSV file.

The Results and Export buttons are disabled until a successful calculation has populated a trajectory.

- **ADM1 Regression…** — opens a modal parameter-fitting dialog. The user loads (or pastes) a CSV dataset of measured time-series observations — any combination of pH, biogas flow Q_gas, CH4/CO2/H2 fractions, individual ADM1 states (e.g. S_ac, S_pro, X_ac), or the derived Total_VFA — then selects ADM1 parameters to fit by their dotted reflection path (e.g.`Kinetics.k_m_ac`,`Kinetics.K_S_ac`,`Inhibition.K_I_nh3`,`GasTransfer.k_La`) with lower/upper bounds and an optional log-scale flag. A Nelder-Mead simplex (DotNumerics) minimises the range-normalised weighted sum of squared residuals between the simulated trajectory (linearly interpolated at measurement times) and the observations. Optimisation runs in a background worker with a live logarithmic SSR convergence chart and per-iteration log. When it finishes, the Results tab shows an Initial / Fitted / Ratio table and lets the user overlay measured points against the fitted simulated curve for any observable. Apply writes the fitted values back into the digester’s`ADM1Params` (and its JSON snapshot); Export Report writes a multi-section CSV with the dataset, parameter table, per-series RMSE, full iteration history and fitted trajectory.

###### Sample Parameter Sets {#sample-parameter-sets .unnumbered}

Three ready-to-load JSON files are installed alongside the engine, under` Reactors\ADM1\Samples`, and can be loaded from the **ADM1 Parameters…** dialog via **Load JSON**:

- `ADM1_BSM2_Mesophilic.json` — the Rosen & Jeppsson BSM2 benchmark at 35 °C (V $_{liq}$ = 3400 m $^{3}$ , V $_{gas}$ = 300 m $^{3}$ , Q $_{in}$ = 178.47 m $^{3}$ /d). Expected steady state: pH ≈ 7.47, Q $_{gas}$ ≈ 2955 Nm $^{3}$ /d, x $_{CH_{4}}$ ≈ 0.650.

- `ADM1_Thermophilic_55C.json` — same reactor geometry, thermophilic operation at 55 °C. Temperature-corrected K $_{w}$ , pK $_{a}$ (CO $_{2}$ /NH $_{3}$ ), Henry constants and P $_{gas,H_{2}O}$ ; kinetic rates ~1.75× mesophilic; lower K $_{I,NH_{3}}$ to reveal ammonia-inhibition effects on acetoclasts.

- `ADM1_SwineManure_Mesophilic.json` — farm-scale high-strength mesophilic digester (V $_{liq}$ = 1000 m $^{3}$ , V $_{gas}$ = 100 m $^{3}$ , Q $_{in}$ = 50 m $^{3}$ /d, HRT ≈ 20 d) with a protein-rich, high-N influent to exercise NH $_{3}$ inhibition of the acetate-degrader population.

###### ADM1-Full Results {#adm1-full-results .unnumbered}

- Final 29-state vector (last row of the trajectory) plus derived pH, free NH $_{3}$ / NH $_{4}^{+}$ split, dissociated VFA species, HCO $_{3}^{-}$ , and biogas composition (x $_{CH_{4}}$ , x $_{CO_{2}}$ , x $_{H_{2}}$ , Q $_{gas}$ in Nm $^{3}$ /d).

- Full time-series available in the Results & Charts dialog and as CSV export (~500 rows by default, configurable via the Operating.SimulationTime_d parameter and internal sampleInterval). COD balance (Feed COD, liquid effluent COD, biogas COD) and thermal plumbing reuse the same machinery as the BlackBox and ADM1-Lite paths.

###### ADM1-S Model (kinetic sulfate reduction) {#adm1-s-model-kinetic-sulfate-reduction .unnumbered}

A fourth fidelity mode (**Digester Model = ADM1Sulfate**) runs the full ADM1 of the previous section with sulfate reduction added as a kinetic process, in place of the stoichiometric assumption the other three models make. Reach for it when the feed carries sulfate rather than only organic sulfur: cane-molasses vinasse and distillery stillage, paper-mill and tannery effluent, scrubber blowdown, or any stream in which sulfate and methanogenesis compete for the same electrons.

###### Populations and reactions {#populations-and-reactions .unnumbered}

Four sulfate-reducing populations join the seven ADM1 groups, each living on one of the electron donors the standard groups use:



\[
\begin{aligned}4\,H_{2}+SO_{4}^{2-} & \longrightarrow HS^{-}+4\,H_{2}O\\
CH_{3}COO^{-}+SO_{4}^{2-} & \longrightarrow 2\,HCO_{3}^{-}+HS^{-}\\
4\,CH_{3}CH_{2}COO^{-}+3\,SO_{4}^{2-} & \longrightarrow 4\,CH_{3}COO^{-}+4\,HCO_{3}^{-}+3\,HS^{-}\\
2\,CH_{3}(CH_{2})_{2}COO^{-}+SO_{4}^{2-} & \longrightarrow 4\,CH_{3}COO^{-}+HS^{-}
\end{aligned}
\]


The propionate and butyrate reducers are incomplete oxidisers: they stop at acetate and pass the remaining electrons on, which is why sulfate can raise the acetate concentration instead of lowering it. Splitting each donor’s COD between the sulfide it becomes and the acetate it leaves behind is the whole of the stoichiometry, and both splits are exact. Of the 112 kg COD per kmol of propionate, 64 leaves as acetate and 48 goes to the sulfate; of the 160 per kmol of butyrate, 128 leaves as acetate and 32 goes to the sulfate. Hydrogen and acetate are oxidised completely, at the 64 kg COD per kmol S that one sulfate accepts.

###### Rates and inhibition {#rates-and-inhibition .unnumbered}

Each uptake is a double Monod, on the electron donor and on sulfate, under the same pH and inorganic-nitrogen envelopes the ADM1 groups carry and under the reducers’ own H $_{2}$ S inhibition. The sulfate term is what hands the substrate back to the methanogens once the sulfate runs out, so a digester that starts sulfate-rich and ends sulfate-limited passes through that changeover on its own.

Free H $_{2}$ S, not total sulfide, is what poisons the cells: only the undissociated acid crosses the membrane. The model therefore inhibits on the undissociated fraction that the charge balance leaves after speciation, which makes the whole effect swing with pH. It applies to the acetogens, to both methanogen groups and to the reducers themselves, the last with a constant about three times larger. Defaults are 0.003 kmol/m $^{3}$ for the ADM1 groups, about 96 mg S/L and the middle of the reported IC $_{50}$ band for acetoclastic methanogens, and 0.009 kmol/m $^{3}$ for the reducers.

###### What changes against the other three models {#what-changes-against-the-other-three-models .unnumbered}

BlackBox, ADM1-Lite and ADM1-Full debit the sulfate’s electrons from the feed COD before the run starts and take every sulfate fed to be reduced; when the debit does not fit they cap it and warn. ADM1-S feeds sulfate in as sulfate and lets the reducers draw their electrons out of the donor pool themselves. Three things follow: the methane loss is whatever the competition produces rather than an assumption, sulfate the reducers could not reach survives to the effluent and is reported, and a sulfate-limited digester becomes a state the model can actually reach.

Feed sulfate arrives with its counter-cations, so the influent is charge-neutral and the alkalinity rise comes from the reduction itself, where a divalent sulfate becomes a monovalent bisulfide. Loading the anion alone would be feeding sulfuric acid.

A sulfate reducer whose population starts at exactly zero stays there, because its growth is proportional to its own biomass. The digester therefore seeds each of the four groups at 0.01 kg COD/m $^{3}$ when all four are left at zero in the initial conditions, and never overwrites a deliberate inoculum. The seed does not decide the answer: at steady state the standing population is set by the sulfate load, the yield and the sum of the dilution and decay rates.

###### ADM1-S Parameters and Results {#adm1-s-parameters-and-results .unnumbered}

- **Parameters**: a Sulfate Reduction tab in the ADM1 Parameters dialog carries the maximum uptake rates and donor half-saturations of the four groups, their sulfate half-saturations, yields and decay rates, the two H $_{2}$ S inhibition constants and the influent sulfate. Kinetic defaults are the mesophilic values of Fedorovich et al.

- **Results**: alongside the H $_{2}$ S outputs shared with the other models, ADM1-S reports Residual Sulfate (kg S/m $^{3}$ ), Sulfate Reduction (the fraction of the influent sulfate actually respired) and SRB Biomass (kg COD/m $^{3}$ ).

- **Effluent sulfate**: unreduced sulfate leaves through the sulfate carrier compound role. With none selected the digester warns that the sulfur is leaving the flowsheet unaccounted, which only ADM1-S can do, since the other three assume complete reduction.

#### Centrifuge

###### Overview {#overview-27 .unnumbered}

The Centrifuge is a solids/liquid separator typically used as the first downstream step after a bioreactor (cell harvest), or for sludge dewatering downstream of an anaerobic digester. Three technology presets are available: Disk-Stack (continuous clarification), Decanter (high-solids dewatering), and Tubular (high-G small-scale).

###### Model {#model .unnumbered}

Each compound is split between a Heavy (concentrate) and Light (clarified) outlet via a per-compound **RecoveryToHeavy** fraction $r_{i}\in[0,1]$ : $\dot{m}_{i,heavy}=r_{i}\,\dot{m}_{i,feed}$ . The recovery defaults are suggested from molecular weight: MW \> 10 kDa → 0.95 (macromolecules/cells to heavy); 1–10 kDa → 0.60 (colloids); smaller → user-default (0.05 by default, i.e. solutes stay in the light phase). Users can override each recovery explicitly per compound. Sigma factor (Σ, equivalent settling area) and bowl speed are reported but not used in the split itself; they serve as sizing/scale-up inputs.

###### Ports {#ports-3 .unnumbered}

- **Feed (inlet)** — cell broth or slurry.

- **Heavy / Concentrate (outlet)** — cells, solids, cake.

- **Light / Clarified (outlet)** — supernatant / centrate.

###### Input Parameters {#input-parameters-17 .unnumbered}

- Technology, Bowl Speed (rpm), Sigma Factor Σ (m²), Default Recovery to Heavy, per-compound Recovery table.

###### Results {#results-11 .unnumbered}

- Feed, Heavy and Light mass flows; Solids Recovery (fraction of compounds with MW \> 10 kDa captured in the heavy phase).

#### Cell Lysis / High-Pressure Homogenizer {#cell-lysis-high-pressure-homogenizer}

###### Overview {#overview-28 .unnumbered}

The Cell Lysis unit models the release of intracellular products (recombinant proteins, PHB, pigments, DNA) by mechanical, chemical or enzymatic disruption of cells. The Hetherington first-order release model is used to compute release as a function of the number of passes and operating pressure:



\[
R=1-\exp\!\bigl(-k\,N\,P^{\alpha}\bigr)
\]


where $N$ is the number of passes and $P$ the operating pressure. The rate constant $k$ and the pressure exponent $\alpha$ are user-tuneable (defaults target commercial yeast/E. coli homogenization at 60–100 MPa).

###### Stream Partitioning {#stream-partitioning .unnumbered}

Each compound is routed between a Lysate (cell-free supernatant) and a Debris (unbroken/partial cells + cell walls) outlet. Defaults are suggested by MW: macromolecules (MW \> 5 kDa) follow the Hetherington release curve; small solutes diffuse freely and go fully to the lysate; the compound flagged as **Biomass** is routed entirely to the debris stream. Users can override each fraction explicitly.

###### Ports {#ports-4 .unnumbered}

- **Cell Broth (inlet)** — cell suspension from the centrifuge or bioreactor.

- **Lysate (outlet)** — cell-free fraction with released intracellular content.

- **Debris (outlet)** — whole/partial cells plus unreleased material.

###### Input Parameters {#input-parameters-18 .unnumbered}

- Technology (HighPressureHomogenizer / BeadMill / Chemical / Enzymatic / Osmotic / Ultrasound), Number of Passes, Operating Pressure, Hetherington k and α, Biomass Compound, Default Release Fraction, per-compound Release table.

- **Ultrasound-specific** (when Technology = Ultrasound): Acoustic Power Density P $_{a}$ (W/mL), Sonication Time t (s), rate constant k $_{u}$ , power exponent β.

###### Ultrasound Mode {#ultrasound-mode .unnumbered}

When Technology is set to **Ultrasound**, the release curve switches from the Hetherington pressure–pass correlation to an acoustic-cavitation kinetic law driven by sonication time and acoustic power density:



\[
R_{u}=1-\exp\!\bigl(-k_{u}\,P_{a}^{\beta}\,t\bigr)
\]


where $P_{a}$ is the acoustic power density (W/mL), t the total sonication time (s), and k $_{u}$ , β are empirical fitting parameters (defaults 0.008 and 1.2 target bench-scale probe sonication of moderately tough microbial cells). All other plumbing — stream partitioning, MW-based default release, per-compound overrides and biomass routing — is identical to the mechanical modes, so the same Lysate/Debris outlets and the same release table apply.

###### Results {#results-12 .unnumbered}

- Feed, Lysate and Debris mass flows; intrinsic release R (%) — labeled *Hetherington R* for mechanical modes and *Ultrasound R* for sonication; Overall macromolecule release (%).

#### Crossflow Ultrafiltration / Diafiltration (UF/DF) {#crossflow-ultrafiltration-diafiltration-ufdf}

###### Overview {#overview-29 .unnumbered}

The Crossflow UF/DF unit concentrates or buffer-exchanges a liquid stream using a membrane described by per-compound sieving coefficients $\sigma_{i}\in[0,1]$ (0 = fully retained, 1 = freely permeable). Two operating modes are supported.

- **Concentration** — retentate volume is reduced by a user-set Volume Concentration Factor VCF (feed volume / retentate volume). Per compound, $\dot{m}_{ret,i}/\dot{m}_{feed,i}=VCF^{-\sigma_{i}}$ .

- **Constant-Volume Diafiltration** — N diavolumes of buffer are exchanged at constant retentate volume. $\dot{m}_{ret,i}/\dot{m}_{in,i}=\exp\!\bigl(-N\cdot(1-\sigma_{i})\bigr)$ .

###### Sieving-coefficient Defaults {#sieving-coefficient-defaults .unnumbered}

Suggested σ values are keyed to molecular weight: MW \< 200 Da → σ = 1 (freely permeable); 200–1000 Da → σ = 0.5 (mid-MW); \> 1000 Da → σ = 0 (retained). Each value can be overridden per compound.

###### Ports {#ports-5 .unnumbered}

- **Feed (inlet)**.

- **Diafiltration Buffer (inlet, optional)** — only meaningful in DF mode; its composition is added to the retentate pool before the sieving law is applied.

- **Retentate (outlet)** — concentrated product.

- **Permeate (outlet)** — membrane-permeated liquid + removed solutes.

###### Input Parameters {#input-parameters-19 .unnumbered}

- Operating Mode, VCF, Diavolumes, Default σ, Permeate Flux (kg/m²/s), Transmembrane Pressure; per-compound σ table.

###### Results {#results-13 .unnumbered}

- Feed, Buffer, Retentate and Permeate mass flows; Effective VCF; Required Membrane Area (computed from the flux).

###### Dynamic Concentration and Diafiltration Modes {#dynamic-concentration-and-diafiltration-modes .unnumbered}

Two additional operating modes, **ConcentrationDynamic** and **DiafiltrationDynamic**, integrate the batch mass balance in the time domain with a Hermia cake-filtration flux decline J(t) = J $_{0}$ / (1 + t/τ), where the single fouling parameter` FoulingHalfLife_s` sets τ (leave at 0 to disable fouling and recover a constant-flux integration). A user-set` MembraneArea_m2` closes the dV/dt = −J·A/ρ balance; per-compound retentate mass follows dm $_{i}$ /dt = −σ $_{i}$ ·J·A·c $_{i}$ , recovering the classical VCF $^{1-\sigma_{i}}$ analytical limit at t → ∞. In dynamic diafiltration V $_{ret}$ is held constant while fresh buffer replaces the permeate, sweeping to the target` Diavolumes`. When either dynamic mode is active the Parameters tab exposes **Results & Charts…** and **Export Time Series…** buttons that open an OxyPlot dialog with tabs for Flux & Volume, VCF & Diavolumes, per-compound Concentrations, a Custom pick-and-plot tab and a Data Table grid, plus CSV / PNG export. The legacy **Concentration** and **DiafiltrationConstantVolume** modes are preserved unchanged and produce identical outlet streams to earlier releases.

#### Chromatography

###### Overview {#overview-30 .unnumbered}

The Chromatography unit models a packed-bed column with Langmuir-style binding. Two operating modes and five chemistry presets are available.

- **Bind-Elute** — targets bind to the resin during load and are recovered in the Product during elution.

- **Flow-Through** — contaminants bind; the product flows through unretained.

- **Chemistry presets**: IonExchange, Affinity, HIC (Hydrophobic Interaction), SizeExclusion, MixedMode.

###### Model {#model-1 .unnumbered}

Each compound is assigned a **RecoveryToProduct** fraction. MW-based defaults flip with the operating mode: in Bind-Elute, macromolecules bind and elute to product; in Flow-Through, macromolecules bind and stay on the column while the product flows through. A **Dynamic Binding Capacity** (g/L resin) combined with the Column Volume gives the total bindable mass per cycle. The load-ratio = feed macromolecule mass ÷ (DBC × CV) is reported, and the unit flags **Saturated = YES** when this exceeds 1.0, alerting the user that the column capacity has been exceeded and yield calculations will be optimistic.

###### Ports {#ports-6 .unnumbered}

- **Feed (inlet)** — load stream.

- **Product (outlet)** — eluate (BindElute) or pass-through (FlowThrough).

- **Waste (outlet)** — flow-through, strip and regeneration fractions lumped.

###### Input Parameters {#input-parameters-20 .unnumbered}

- Operating Mode, Chemistry, Column Volume, Dynamic Binding Capacity, Default Recovery to Product, per-compound Recovery table.

###### Results {#results-14 .unnumbered}

- Feed, Product and Waste mass flows; Target Recovery (%), Load Ratio, Saturated flag.

###### Thomas Breakthrough Curve (Dynamic Mode) {#thomas-breakthrough-curve-dynamic-mode .unnumbered}

A third operating mode, **BindElute_Dynamic**, replaces the equilibrium load-ratio check with an analytical Thomas breakthrough curve for the loading step:


\[
\frac{C}{C_{0}}=\frac{1}{1+\exp\left(\frac{k_{Th}}{Q}\left(q_{max}\,m_{resin}-C_{0}\,Q\,t\right)\right)}
\]


where k $_{Th}$ is the Thomas rate constant (`ThomasRateConstant_Lgs`, L/(g·s)), q $_{max}$ reuses the` DynamicBindingCapacity_gL` entry, m $_{resin}$ = V $_{col}$ ·ρ $_{resin}$ (with` ResinDensity_gL`), and Q, C $_{0}$ are derived from the feed of the target compound (MW \> 5 kDa).` LoadingTime_s` = 0 auto-selects a duration that reaches ≈ 99 % saturation. The routine generates 500 samples of t, bed volumes Q·t/V $_{col}$ , C/C $_{0}$ , cumulative q $_{loaded}$ (t) and breakthrough fraction. The Parameters tab exposes **Results & Charts…** and **Export Time Series…** buttons that open an OxyPlot dialog with a Breakthrough Curve tab (C/C $_{0}$ vs bed volumes), a Cumulative Load tab, Custom and Data Table tabs, and CSV / PNG export. The legacy **BindElute** and **FlowThrough** modes remain unchanged.

#### Crystallizer

###### Overview {#overview-31 .unnumbered}

The Crystallizer splits a liquid stream between a Crystals outlet and a Mother-Liquor outlet based on the solubility of a selected solute in a selected solvent. Three operating modes are available.

- **Cooling crystallization** — outlet temperature is user-specified (typically well below the feed T).

- **Evaporative crystallization** — a user-set Evaporation Fraction of the solvent is removed before the solubility check is applied.

- **Antisolvent crystallization** — a second inlet (Antisolvent) is mixed with the feed; effective solubility is reduced by the user-set Solubility Reduction factor.

###### Solubility and Yield {#solubility-and-yield .unnumbered}

The solute solubility in the solvent at temperature $T$ (in K) follows the modified Van’t-Hoff/Apelblat form



\[
C_{sat}(T)=A+B\,(T-298)+C\,(T-298)^{2}\quad[\mathrm{g\,solute/g\,solvent}]
\]


The crystallized mass is $\max(0,\,\dot{m}_{solute,in}-C_{sat}\cdot\dot{m}_{solvent,eff})$ where $\dot{m}_{solvent,eff}$ reflects evaporation if applicable. Mean crystal size is reported as an input for downstream equipment sizing but is not used in the yield calculation.

###### Ports {#ports-7 .unnumbered}

- **Feed (inlet)** — supersaturated or saturable solution.

- **Antisolvent (inlet, optional)** — only used in Antisolvent mode.

- **Crystals (outlet)** — crystalline solid product.

- **Mother Liquor (outlet)** — saturated remainder containing residual solute, solvent and impurities.

###### Input Parameters {#input-parameters-21 .unnumbered}

- Mode, Solute Compound, Solvent Compound, Operating Temperature, Apelblat constants A/B/C, Evaporation Fraction, Antisolvent Solubility Reduction, Mean Crystal Size.

###### Results {#results-15 .unnumbered}

- C $_{sat}$ at operating T, Solute in Feed, Crystallized mass, Mother Liquor mass, Crystallization Yield (%).

#### Biogas Upgrader

###### Overview {#overview-32 .unnumbered}

The Biogas Upgrader processes a raw biogas stream (typically 50–65 % CH $_{4}$ , 35–50 % CO $_{2}$ , traces of H $_{2}$ S and water) into pipeline-specification Renewable Natural Gas (RNG). Four upgrading technologies are selectable, each preloading typical removal efficiencies and CH $_{4}$ slippage losses: WaterScrubbing, Amine absorption, Pressure-Swing Adsorption (PSA), and MembraneSeparation.

###### Model {#model-2 .unnumbered}

A two-stage algebraic model splits each compound between an Upgraded-Gas (RNG) and an Off-gas outlet:

- **H $_{2}$ S polishing** — ZnO bed or caustic wash; default 99 % removal.

- **CO $_{2}$ bulk removal** — technology-specific efficiency; associated CH $_{4}$ loss is routed to off-gas.

- **H $_{2}$ O polishing** — optional drying step; default 98 % removal.

- Inerts (N $_{2}$ , O $_{2}$ ) are carried through to the upgraded stream by default.

###### Ports {#ports-8 .unnumbered}

- **Biogas (inlet)** — raw biogas, typically from the Anaerobic Digester.

- **Upgraded Gas / RNG (outlet)** — pipeline-spec methane (typically \> 95 % CH $_{4}$ ).

- **Off-gas (outlet)** — CO $_{2}$ , H $_{2}$ S, water and lost CH $_{4}$ .

###### Input Parameters {#input-parameters-22 .unnumbered}

- Technology, H $_{2}$ S Removal, CO $_{2}$ Removal, CH $_{4}$ Loss, H $_{2}$ O Removal, Target CH $_{4}$ Purity (reporting only), compound roles for CH $_{4}$ , CO $_{2}$ , H $_{2}$ S, H $_{2}$ O, N $_{2}$ .

###### Results {#results-16 .unnumbered}

- Feed, Upgraded and Off-gas mass flows; Upgraded CH $_{4}$ mass fraction; CH $_{4}$ recovery (%).

###### Practical Notes {#practical-notes-3 .unnumbered}

- Amine absorption offers the highest CO $_{2}$ removal (\> 99 %) with minimal CH $_{4}$ slip (\< 0.2 %) but has the highest thermal regeneration cost. PSA and membrane systems are simpler and cheaper to operate but slip 2–3 % CH $_{4}$ to the off-gas; this is often combusted or recycled to a downstream oxidizer.

