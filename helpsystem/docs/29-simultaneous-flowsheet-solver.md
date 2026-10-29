# Simultaneous Flowsheet Solver

#### Overview {#overview-51}

The sequential-modular solver converges a flowsheet one block at a time: each Recycle iterates on its own torn stream (by successive substitution, Wegstein or Broyden acceleration), and each Adjust runs its own loop, nested inside the recycle loops that surround it. On flowsheets where several recycles share compounds, or where an adjust acts on a variable inside a recycle, the nested loops interact and can take many passes or fail to converge.

The **Simultaneous Flowsheet Solver** (Patreon Edition) converges every Recycle, Energy Recycle and Adjust of the flowsheet at once. The unit operations are calculated exactly as before, by their own models; only the outer iteration changes.

The Patreon Edition also has an **equation-oriented solver** ([Equation-Oriented Solver](#sec:equation_oriented)), which solves the equations of the unit operations themselves together with the recycles and adjusts, as one system. [Choosing a solver](#sec:choosing_solver) compares the three.

#### Selecting the solver strategy

The strategy is a property of the flowsheet and is saved with it:







| **Interface** | **Where** |
|:---|:---|
| Classic (Windows) | `Solver:` button on the flowsheet toolbar, next to the simultaneous adjust button |
| Cross-platform | `Solver > Solver Strategy` menu |



###### Sequential-Modular

The default, and the strategy every file saved with an earlier version opens with. Recycles and adjusts are converged by their own blocks.

###### Simultaneous

Every steady-state solve of the flowsheet is handed to the simultaneous solver.

###### Auto

The simultaneous solver takes the flowsheet when it has already solved it once (it keeps the solution of the previous solve, see below) or when an Adjust acts inside a recycle, where the sequential solver nests its loops; otherwise the sequential solver runs.

Without a Patreon membership the log says so and the sequential solver runs. Dynamic simulations are always solved sequentially, one pass per time step.

#### Method

##### Unknowns and equations

The unknowns $\mathbf{x}$ are the state of every torn stream (temperature, pressure and the mass flow of each compound), every recycled energy flow and every manipulated variable of an Adjust. A torn stream that DWSIM calculates from its pressure and enthalpy takes its mass enthalpy in place of the temperature: a temperature written into it would be ignored, and in two phases it does not fix the state. These are the streams of a single compound, and every stream of a property package that treats its mixtures as one fluid (the CoolProp incompressible mixtures, a lithium bromide solution among them). The function $\mathbf{g}(\mathbf{x})$ is one sequential pass of the flowsheet with the recycle and adjust blocks taken out: the solver writes $\mathbf{x}$ into the torn streams and the manipulated variables, the unit operations are calculated in the usual order, and the pass returns the recalculated torn streams and the controlled variables. The residuals are


<a id="eq:simultaneous_residuals"></a>

\[
\mathbf{r}(\mathbf{x}) =
\begin{bmatrix}
\mathbf{g}_{\mathrm{tear}}(\mathbf{x}) - \mathbf{x}_{\mathrm{tear}} \\
y_{\mathrm{adjust}}(\mathbf{x}) - y_{\mathrm{target}}
\end{bmatrix} = \mathbf{0}\tag{1}
\]


and a recycle or adjust counts as converged with the tolerances set in its own block, by the same rules the block applies.

##### Newton iteration

The system is solved by a damped Newton method,


<a id="eq:simultaneous_newton"></a>

\[
\mathbf{J}\,\Delta\mathbf{x} = -\mathbf{r}(\mathbf{x}_k), \qquad
\mathbf{x}_{k+1} = \mathbf{x}_k + \lambda\,\Delta\mathbf{x}\tag{2}
\]


with a backtracking line search on $\lambda$, limits on the change of each variable in one step, and flows kept non-negative. The first matrix is minus the identity for the torn variables, so the first step is a plain successive-substitution step, plus one finite-difference column for each adjust. Broyden updates then shape the matrix from the passes the iteration makes anyway. A full finite-difference Jacobian is built only when the line search fails with the updated matrix, or when the matrix is singular, which happens when the controlled variable of an adjust lies upstream of its manipulated variable across a torn stream (the minus-identity columns carry no derivative of it). Its columns are evaluated in parallel on copies of the flowsheet held in memory; with a CAPE-OPEN unit operation in the loop they are evaluated one after the other on the flowsheet itself, since the component answers one call at a time and a copy of it fails its first solve. When the Jacobian has to be built again in the same solve, the columns that move disjoint sets of residuals in the first one are perturbed together, one pass per group.

##### Warm start

The solution, and the Jacobian when one was built, are saved with the flowsheet. The next solve, in the same session or after the file is opened again, starts from them, and a small change of a feed usually converges in one or two iterations. If the current point cannot be evaluated (a unit operation fails), the solver tries the saved solution, then points halfway and a quarter of the way toward it.

##### Confirmation

When the solve converges, the solver reads the torn streams back from the flowsheet and runs one more pass. An answer that does not close on that pass (a unit operation that carries a state from one pass to the next) is solved again from there, and handed to the sequential solver if it still does not close.

##### When the solve does not converge

###### Restart

A solve that started from the substitution matrix and stops at the noise floor (below) starts again from the same point with a finite-difference Jacobian. The two starts take different paths, and the noise of a column can stop one of them short of the tolerances and not the other. When the restart does not close either, the flowsheet goes back to the first answer.

###### Fallback

A solve that does not converge is handed to the sequential solver, and the log says so.

###### Noise floor

Columns and other unit operations that iterate internally return results with a small noise. When the line search stops making progress, after a finite-difference Jacobian has been built, with every error within ten times its tolerance, the solver stops at the best point, without handing the flowsheet to the sequential solver, and the log says how many times the tolerance was left; loosen the recycle or adjust tolerances, or tighten the convergence tolerances of the columns. A matrix shaped by Broyden updates from the substitution start that stops making progress is replaced by a finite-difference Jacobian first.

###### Specifications without a steady state

When the finite-difference Jacobian is close to singular (condition number above $10^{8}$), the specifications may fix no steady state, or many (a closed loop whose inventory nothing sets). The log says so and names the recycle or adjust variables along the singular direction, which are the ones to check.

#### Messages

A converged solve writes a line to the log with the number of Newton iterations, the passes on the flowsheet, the Jacobian columns built (if any), whether it started warm, and the time taken. Unconverged solves give a warning with the reason and what to change.

#### Performance

The number of passes depends on how strongly the recycles interact. With tight tolerances (0.01 K, 0.1 Pa and $10^{-4}$ kg/s on every recycle):







| **Case** | **Sequential** | **Simultaneous** |
|:---|---:|---:|
| Cavett problem, torn flows halved and 10 K hotter | 25 passes | 11 passes |
| Biodiesel production (adjust inside a recycle), feeds $\times$ 1.2 | not converged | 8 passes |



On 59 flowsheets from the samples and the case library, solved as saved and again after a change of the feeds, both solvers closed every recycle and adjust and agreed within the tolerances; the one exception was a flowsheet close to having no steady state, where both stopped at different points inside the tolerance. On flowsheets with a single, loosely coupled recycle the sequential solver is as fast.

#### Limitations

- Spec blocks set to run after the flowsheet are not repeated inside each pass; Spec blocks attached to an object are.

- Recycles and adjusts marked as dynamic-only stay out of the steady-state system, as in the sequential solver.

- An adjust whose manipulated variable does not affect its controlled variable at steady state makes the system singular, and neither solver closes it.

- A tolerance below the noise of the unit operations that iterate internally is not reached by either solver.

- With a CAPE-OPEN unit operation (a ChemSep column) in a loop, the Jacobian is built one column at a time, and a solve can stop a few times above the tolerances, at the resolution of the column's own convergence.

#### Design Specifications, Optimization and Data Reconciliation

With the simultaneous solver, the converged flowsheet is a model that can be solved for targets, optimized and fitted to plant data (Patreon Edition). The three problems are set up in one window, **Design Specifications, Optimization and Reconciliation**, opened from the `Flowsheet Analysis` menu in both interfaces. Each row of its tables picks an object and one of its properties from lists; the numbers are typed and shown in the units of the flowsheet. The problem, with the results of its last run, is saved with the file.

The figures below come from a flash plant: two hydrocarbon feeds mixed, cooled and separated into vapor and liquid.

##### Design specifications



<a id="fig:simultaneous_specifications"></a>
![Design specifications: the methane mole fraction of the vapor brought to its target by the outlet temperature of the cooler.](images/screens_simultaneous/editor_specifications.png)

*Figure 1: Design specifications: the methane mole fraction of the vapor brought to its target by the outlet temperature of the cooler.*



A design specification asks for a property to reach a target; the solver reaches it by moving the properties listed under **Manipulated variables**, within their bounds, as many of them as there are specifications (Figure [1](#fig:simultaneous_specifications)). It does the work of an Adjust block without the block: every specification adds the equation


<a id="eq:simultaneous_specification"></a>

\[
y_{\mathrm{spec}}(\mathbf{x}) - y_{\mathrm{target}} = 0\tag{3}
\]


to the residuals of Equation [1](#eq:simultaneous_residuals), and every manipulated variable an unknown, so the specifications are met together, and together with the recycles, instead of one loop inside another. They are paired with the variables as a set; a specification is not tied to one variable.

###### Target and tolerance

The target is in the units shown. An empty tolerance stands for $10^{-6}$ of the target, or of 1 when the target is smaller.

###### When they are met

On every steady-state solve with the solver strategy set to Simultaneous or Auto (Auto takes the simultaneous solver whenever there are active specifications), while **Meet the specifications on every solve of the simultaneous solver** is checked. The line under the check box says whether they are met, and why not when they are not. A solve with specifications that does not converge is reported as failed: the sequential solver knows nothing of them, so the flowsheet is not handed to it. The usual reason is a target out of reach within the bounds of the variables, and the message names the variable held at its bound. With **Solve the flowsheet as one system of equations (equation-oriented)** checked, the specifications are equations of the equation-oriented solver (Section [Equation-Oriented Solver](#sec:equation_oriented)).

##### Optimization



<a id="fig:simultaneous_optimization"></a>
![Optimization: the liquid product, less half the heavy feed that makes it, maximized by the heavy feed flow, with an upper bound on the liquid.](images/screens_simultaneous/editor_optimization.png)

*Figure 2: Optimization: the liquid product, less half the heavy feed that makes it, maximized by the heavy feed flow, with an upper bound on the liquid.*



The optimization moves feed and unit properties, the **Variables**, within their bounds, to maximize or minimize an objective of the converged flowsheet, with bounds on other properties as **Constraints**:


<a id="eq:simultaneous_optimization"></a>

\[
\max_{\mathbf{u}}\ \mathrm{or}\ \min_{\mathbf{u}}\ f\left(\mathbf{y}(\mathbf{u})\right)
\quad \mathrm{subject\ to} \quad
\mathbf{u}_{L} \le \mathbf{u} \le \mathbf{u}_{U}, \qquad
\mathbf{c}_{L} \le \mathbf{c}(\mathbf{u}) \le \mathbf{c}_{U}\tag{4}
\]


where $\mathbf{y}$ and $\mathbf{c}$ are properties of the flowsheet solved at $\mathbf{u}$, with its recycles closed and its design specifications met. Equal bounds on a constraint make an equality.

###### Objective

Each row of **Objective terms** is a property. With the expression box empty, the objective is the sum of the terms times their weights, a weight being per unit shown. With an expression, each term enters by its **Name**, in the units of its row (the units in which the property was shown when it was picked), and the weights are not used: `50.0*L - 0.02*Q`, `(x - 0.95)^2`, `max(a, b)`. The expression is written in the language of the Spec block. Two of its rules change a number without an error, and the line under the box points them out: a leading minus binds tighter than the power, so `-x^2` is $(-x)^2$ (write `0.0 - x^2`), and two whole numbers divide as whole numbers, so `1/2` is 0 (write `1.0/2.0`). Figure [2](#fig:simultaneous_optimization) maximizes `L - 0.5*F`, the liquid product less half the heavy feed, both in kg/h.

###### Method

Sequential quadratic programming with a trust region. At each point the gradient of the objective and the linearized constraints come from the sensitivities of the unit operations where they allow it, and from finite differences elsewhere; a quadratic model, its curvature built up by BFGS updates, is solved by the interior point method without solving the flowsheet, and the flowsheet is solved once at the step it proposes. A step that does not improve the objective and the constraints together enough is refused and the trust region shrinks.

###### Settings and results

**Maximum iterations**; **Tolerance**, the relative change of the objective below which the optimization stops; **Constraint tolerance**, the relative violation allowed; and whether the derivatives come from the sensitivities. **Run** starts the optimization and **Stop** ends it before its next iteration. The line under the buttons gives the iterations, the flowsheet solves, the time, the objective at the end and at the start, and the largest violation of the constraints; the log lists every term, variable and constraint in the units of the flowsheet. The flowsheet is left at the best point found. A squared objective, such as a deviation from a target, meets the tolerance with only its square root on the deviation itself: tighten the tolerance (to $10^{-10}$, say) to bring the deviation down to $10^{-5}$.

##### Data reconciliation



<a id="fig:simultaneous_reconciliation"></a>
![Data reconciliation: five mass flows measured with a standard deviation of 1 %, the liquid one 12 standard deviations off, reconciled by moving the two feed flows.](images/screens_simultaneous/editor_reconciliation.png)

*Figure 3: Data reconciliation: five mass flows measured with a standard deviation of 1 %, the liquid one 12 standard deviations off, reconciled by moving the two feed flows.*



The reconciliation brings the flowsheet as close to plant measurements as their uncertainties allow. It moves the **Adjusted inputs**, the feeds and unit parameters that are not known exactly, within their bounds, to minimize


<a id="eq:simultaneous_reconciliation"></a>

\[
\sum_{i} \left( \frac{m_i - y_i(\mathbf{u})}{\sigma_i} \right)^2\tag{5}
\]


where $m_i$ is a measured value, $\sigma_i$ the standard deviation of its instrument and $y_i$ the same property of the converged flowsheet. Since the flowsheet is the model, its mass and energy balances hold at every point visited. **Constraints** bound other properties at the reconciled point.

###### Measurements

Picking a property fills the measured value with the one the flowsheet has, to be replaced by the plant's; the standard deviation, in the units shown, has to be given. After a run, the table shows the reconciled value and the normalized residual of each measurement, and the table of the inputs their reconciled values and standard deviations.

###### Method

**Auto** uses Levenberg-Marquardt without constraints and Gauss-Newton sequential quadratic programming with them; the interior point method can be chosen too. The derivatives come from the sensitivities of the unit operations where they allow it, and from finite differences elsewhere.

###### Tests

The redundancy is the number of measurements less the number of inputs moved (an input that ends on a bound counts as fixed). With redundancy, the global test compares the weighted sum of squares with the chi-square quantile at the confidence level: above it, the measurements do not agree with the model within their standard deviations. The normalized residual of a measurement is its adjustment over its own standard deviation; the largest one above the normal quantile marks the suspect measurement, highlighted in the table. In Figure [3](#fig:simultaneous_reconciliation) the sum of squares is 101.6 against a critical value of 7.81, and the liquid flow is pointed out with a normalized residual of 10.07 against 1.96. With fewer measurements a gross error can be impossible to place: measuring the two feeds, the vapor and the liquid only, the liquid and the heavy feed that makes most of it come out with nearly the same residual.

###### Results

The line under the buttons gives the iterations, the flowsheet solves, the method, the sum of squares, the redundancy, the global test and the suspect measurement; the log lists every measurement and input in the units of the flowsheet. The flowsheet is left at the reconciled inputs.

#### Equation-Oriented Solver {#sec:equation_oriented}

The equation-oriented solver (Patreon Edition) solves the whole flowsheet as one system of equations. Its unknowns are the temperature, the pressure and the molar flow of each compound of every material stream; each unit operation writes the equations that tie its outlets to its inlets, and a damped Newton method solves all of them at once, with no pass of the unit operations inside the iteration. It is turned on with **Solve the flowsheet as one system of equations (equation-oriented)** in the **Design Specifications** tab of the window above (Figure [1](#fig:simultaneous_specifications)), and the choice is saved with the file. From then on every steady-state solve with the solver strategy set to Simultaneous or Auto goes to it first; Auto takes it whenever the box is checked.

##### Equations

Every residual is divided by the tolerance of its kind: $10^{-3}$ K for a temperature, 1 Pa for a pressure, $10^{-7}$ of the stream's molar flow for the flow of a compound and $10^{-4}$ kW per mol/s for an energy balance. The system is converged when


<a id="eq:eo_converged"></a>

\[
\max_i \frac{\left|r_i(\mathbf{x})\right|}{\varepsilon_i} \le 1\tag{6}
\]


The phase equilibrium is a submodel: the PT flash of the property package, with the enthalpy and entropy of each phase. The derivatives of the phase split, the enthalpy and the entropy with respect to temperature, pressure and the moles come from the implicit function theorem on the equality of fugacities, so the unit operations below give their Jacobian analytically:

| **Unit operation** | **Modes with equations of their own** |
|:---|:---|
| Mixer | without an energy inlet: flows, pressure by its rule, energy balance |
| Splitter | split ratios, or the molar or mass flow of the first outlets with the last taking the rest |
| Heater, Cooler | outlet temperature, temperature change, or the duty (scaled by the efficiency, as the unit does) |
| Valve | pressure drop or outlet pressure (isenthalpic) |
| Pump | pressure increase or outlet pressure: the liquid takes $\Delta P/(\rho_L\,\eta)$ times its mass flow |
| Compressor | adiabatic path, with pressure increase, outlet pressure or pressure ratio: the isentropic enthalpy rise over the efficiency |
| Expander | adiabatic path, with pressure drop, outlet pressure or pressure ratio: the efficiency times the isentropic enthalpy drop, the isentropic outlet by the package's PS flash |
| Component Separator | every compound with a specification: both outlets at the inlet's temperature and pressure, the specified outlet with each compound's set flow or share |
| Gas-Liquid Separator | two outlets; adiabatic, at a set temperature, pressure or both (with only the pressure set, the inlets' temperatures averaged by mass, as the vessel takes it), with the heat of its energy stream, of its heating or cooling amount at constant pressure or constant temperature (the pressure then moves), or the heat its thermal profile gives |
| Heat Exchanger | outlet temperature of one side, heat load, $U$ and $A$ with $Q = U A F\,\Delta T_{\mathrm{lm}}$, or the vapor fraction of one outlet: its temperature where its compounds, at its pressure, flash to that fraction (the bubble point at 0, the dew point at 1); the area for a set outlet temperature (the area a result); a thermal efficiency of the largest heat: counter-current, the lesser of the cold side's at the hot inlet's temperature and the hot side's at the cold inlet's; co-current, the heat at which the two outlets meet at one temperature, at the inlet pressures; a minimum internal temperature approach, the least difference of the two sides' profiles of 26 points by PH flashes, as the exchanger makes them, differenced; shell and tube rating: each side's outlet pressure less the drop of the side it flows in and the heat load $U A F\,\Delta T_{\mathrm{lm}}$ (at most the largest heat), $U$ and the drops from the exchanger's correlations at each side's properties at its mean temperature, differenced; the fouling factor mode at its two outlet temperatures, its pressures as the rating's |
| Conversion Reactor | reactions in the mixture; adiabatic with one group of reactions, isothermal or at an outlet temperature |
| Equilibrium Reactor | reactions in the vapor or the liquid, with activity, fugacity, mole fraction or partial pressure as the basis |
| Gibbs Reactor | reacting in the vapor, in the liquid, or in the phases the flash gives |
| CSTR | one outlet; isothermal, adiabatic or at an outlet temperature; kinetic and catalytic reactions with expression rates |
| PFR | isothermal, adiabatic, at an outlet temperature or with the energy stream's duty; kinetic and catalytic reactions with expression rates, in one phase; the profile along the length as unknowns (below) |
| Pipe Segment | length or outlet temperature given; heat given, an overall coefficient against the surroundings (given or estimated from the fluid's properties at each cell's inlet), solar radiation on the projected area, or no heat balance; the profile along the length as unknowns (below) |
| Distillation Column, Absorber | the MESH equations of the Naphtali-Sandholm solver, each stage's temperature and flows unknowns of the system (below) |

Feeds keep their state, and a Recycle block makes its outlet equal to its inlet. Controllers, switches and inputs act in dynamic simulation only and are left out.

###### Equilibrium and Gibbs reactors

Both write the same equations. The overall moles $\mathbf{z}$ of the two outlets differ from the inlet's by a combination of the columns of a stoichiometric matrix $\mathbf{S}$, and every column is at equilibrium:


<a id="eq:eo_chemical_equilibrium"></a>

\[
\sum_i S_{ir} \ln b_i = \ln K_r\tag{7}
\]


For the Equilibrium Reactor the columns are its reactions, $K_r$ is the reaction's constant at the temperature plus its approach, and the bases $b_i$ are the reactor's own, taken at the overall composition. For the Gibbs Reactor the columns are a basis of the reactions the elements of the reacting compounds allow, $\ln K_r = -\sum_i S_{ir}\,\Delta G_{f,i}/RT$, and $b_i$ is the fugacity over 1 atm in the reacting phase, so the equations hold at the minimum of the Gibbs energy. The outlets split by the flash of $\mathbf{z}$, and an adiabatic reactor carries the heat of reaction in its energy balance.

###### CSTR

The outlet's moles are the inlet's plus what the reactions make in the reactor at the outlet's state, the steady state the reactor's own time stepping approaches; the concentrations, the reacting volume and the conversion factors are worked out from the outlet stream as the reactor works them out from its contents.

###### PFR and Pipe Segment

Both are cut along their length into cells, and the states between the cells (temperature, pressure and the flow of each compound) are unknowns of the system next to the streams', the last one the outlet. A PFR has as many cells as its own sections, at most 50; a pipe one per increment of its hydraulic profile, at most 200. Each cell writes the balance of each compound, the pressure drop and the energy balance:

- **Compounds:** a PFR's change of moles over the cell is the cell's volume times the mean of the reaction rates per volume at its two ends (the trapezoidal rule), with the rates, concentrations and conversion factors of the reactor's own integration; a pipe carries the compounds through.
- **Pressure:** the mean of the drop the unit's own correlation gives at the two ends (the PFR's Ergun or Beggs and Brill, or its drop as set; the pipe's flow package, fittings included). A gas pipeline equation (Weymouth, Panhandle) already gives the drop over the increment from its inlet, and is taken there.
- **Energy:** the enthalpy at the cell's end less the inlet's, with the enthalpies of formation of what reacted, equals the heat up to that cell: the PFR's duty spread along its length, the pipe's heat spread evenly or U A times the logarithmic mean of the differences to the surroundings. Isothermal and outlet-temperature reactors set the temperature instead.

The energy balances are written from the inlet so that the noise of the flashes does not add up along the length, and are held to ten times the energy tolerance, since a flash's enthalpy moves by about one tolerance under steps of 1e-5 K. The rates and the drops are worked out on the state put on a stream, and differenced. Before the first iteration, the cells are solved one after the other from the inlet, as an implicit integration along the length, so that the solve starts at the unit's own profile for its inlet.

The cells are of the second order in their length, and the units' own integrations of the first: the PFR freezes the state over each section and the pipe takes the drop and the heat at the inlet of each increment. With their default steps the cells are the closer to the limit of ever finer steps: in a test, an isothermal PFR at its default 1 % sections kept 0.21 mol/s more methanol than at 0.1 %, and 0.02 mol/s more than the limit, which the 50 cells reached within 0.001 mol/s; a pipe losing heat to its surroundings came out 1.03 K off the limit by its own increments and 0.002 K by the cells. The pass of the unit operations after the solve writes the units' own results.

###### Columns

A distillation column or an absorber (without rate-based efficiencies, secondary specifications, a free water product or an external solver) writes its MESH equations into the system: each stage's temperature and its vapor and liquid compound flows are unknowns, two internal states a stage at the stage's temperature and pressure, and its equations are the Naphtali-Sandholm solver's own, the material and energy balances, the equilibrium relations with the stage efficiencies, and the two specifications in place of the condenser's and the reboiler's energy balances. The feeds are the system's streams and enter the balances with their flows and enthalpies; each product is the flow of its stage times the composition there (a side draw its set flow), at the stage's temperature and pressure. A partial condenser's liquid distillate is the distillate stream's flow, an unknown of the system, with the reflux's composition, and the vapor product leaves at the rate the column sets. The derivatives of the stages' equations are the solver's own Jacobian, analytic where the property package gives them; a recovery specification's are differenced on the feeds, and a specification value an Adjust moves is a variable of the equations. The equations start from the column's last solution, so a column solved once by its own solver joins the system already converged.

Where the solver's equations do not cover the column (a subcooled distillate of one compound or of an azeotrope; a column with no solution of its own yet), the column gives its products from its feeds with its own solver, warm from the last solution, and its derivatives come from the Naphtali-Sandholm solver started at that solution, by the implicit function theorem on the MESH equations; the log says which and why.

###### Spec blocks

An active Spec block is an equation: its target property becomes an unknown, equal to the block's expression of the source (X) and of the target itself (Y) in the units of the flowsheet, held between the block's limits when it has both. An active Information Carrier writes one such equation for each of its targets: the target property, an unknown, equal to the source property in SI.

###### Black boxes

A unit operation in another mode, a Tank, an Orifice Plate, a Filter, a Solids Separator, a Shortcut Column, a Polymerization Reactor, a Python Script, Spreadsheet or Flowsheet unit operation, a column the derivatives above do not cover (secondary specifications, rate-based efficiencies, an external solver) or a CAPE-OPEN unit operation (one call at a time, as a ChemSep column needs) enters through its own calculation: its inlets are put at their variables and flashed there, the unit is calculated, and its residuals are


<a id="eq:eo_blackbox"></a>

\[
\mathbf{r} = \mathbf{x}_{\mathrm{out}} - \mathbf{f}_{\mathrm{unit}}(\mathbf{x}_{\mathrm{in}})\tag{8}
\]


with derivatives by central differences of 0.1 %, one-sided where the unit's own solver fails on the other side (and none where it fails on both, as a ChemSep column does for a compound absent from a feed). The unit's own convergence noise makes these slower than the units with equations. A CAPE-OPEN unit whose solver stops on a message box is calculated once more; a second failure is an error. The black boxes' blocks of the Jacobian are differenced on the first iteration only and updated by Broyden's formula from each step after it; they are differenced again when the line search fails or cuts a step taken with them, or the iterations stall. On the first iteration, the block of a black box that separates without reacting (a column), when its differences would take longer than two seconds and its feeds carry six compounds or more, is estimated instead: each outlet keeps its split of each compound, the feeds' temperatures and the unit's parameters are differenced, and the block is corrected along the flow of each feed. That is four solves of the unit per feed.

###### Energy recycles

The power of the energy stream an energy recycle writes becomes an unknown, which the unit downstream (a compressor driven by an expander) reads through its own calculation, and the recycle's equation sets it equal to the power the unit upstream writes, through its own calculation, into the stream the recycle reads; an expander with equations of its own writes the enthalpy its fluid gives up.

###### Adjusts and design specifications

Each active Adjust and each active design specification adds an unknown, the property it manipulates, and an equation, the controlled property at its target over the tolerance (the Adjust's own tolerance). The manipulated property is a number the unit's equations read (an outlet temperature, a pressure drop, a split ratio, a feed's flow) or, for a black box, a property written into the unit before it is calculated.

###### Saturated streams

A stream of one compound, or of an azeotrope, in two phases or at its bubble or dew point has its saturation temperature whatever its enthalpy, so its temperature, pressure and flows do not tell its state. Such a stream takes its vapor fraction $\beta$ as one more unknown: every stream of one compound below 95 % of its critical pressure, in any phase, since the solution may take it into two phases, and a mixture whose bubble and dew points are within 0.5 K of each other, with its temperature between them. $\beta$ is an unknown, and its temperature becomes an equation of it: the temperature at which its compounds, at its pressure, flash to $\beta$ (the compound's saturation temperature; for a mixture, the package's PVF flash). Its enthalpy comes from the phases of that state. A saturated feed holds its vapor fraction in place of its temperature, a recycle sets the two vapor fractions equal, a black box's saturated outlet is matched by the vapor fraction the unit calculates, and a unit with equations of its own next to a saturated stream is taken as a black box. $\beta$ is an extended vapor fraction: below 0 the stream is a liquid 100 K per unit below its bubble point, above 1 a vapor 100 K per unit above its dew point, with the enthalpy of that one phase. The temperature equation is the same in the three regions and continuous across 0 and 1, so a saturated stream that the solution takes out of the two phases, or back into them, needs no change of formulation.

###### The simultaneous solver takes over

A flowsheet with a stream of a package that takes every stream for one fluid (Steam Tables, CoolProp, seawater), a unit operation outside the table and the black-box list (a Python script unit, for one), two liquid phases in a flash, or a Newton iteration that does not converge (or stalls: five iterations that do not halve the sum of the squared residuals) is handed to the simultaneous solver, with a message that gives the reason. A loop with no make-up of what it loses (a glycol loop whose only losses go to the products) has no well-defined inventory at steady state, and the Newton steps stall on it.

###### After the solve

The solution is written into the streams and one pass of the unit operations runs on it, so every unit holds its results. The log lists the largest residual of each iteration with its equation, and the last line gives the iterations, the equations, the flashes, the calculations of black boxes and the time.

###### Performance

On the Cavett problem (four flash drums at set temperatures, three compressors, a mixer and three recycles) the solver closes 360 equations in 3 Newton iterations and 1.1 s, with the recycles closed to $3\times10^{-10}$ kg/s; the simultaneous solver over passes takes 14 s on the same file. On flowsheets with ChemSep columns (CAPE-OPEN), each solved as saved:

| Flowsheet | Equations | Sequential | Equation-oriented |
|---|---:|---:|---:|
| Methanol synthesis (reactor loop, one column) | 253 | 119 s | 4.9 s |
| Stacked columns (three columns) | 61 | 19.5 s | 13.2 s |
| Methanol and chloroform, pressure swing | 26 | 2.8 s | 6.6 s |
| Natural gas processing with turbo-expansion (three columns) | 1576 | 5.8 s | 38 s |

The equation-oriented solver wins where the sequential solver needs many passes around interacting loops, and loses where it spends its time on the columns' own solvers inside the Newton iterations and the sequential solver closes in one or two passes.

###### Limitations

- A conversion the reactants cannot sustain is not limited the way the Conversion Reactor's own solver limits it.
- The final pass recalculates the units, whose pressure-enthalpy flashes close to their own tolerance: an outlet temperature can differ from the equation-oriented solution by a few thousandths of a kelvin.
- The Heat Exchanger's own iteration on $U$ and $A$ stops when the load moves less than 0.1 %; the equation-oriented solver closes $Q = U A F\,\Delta T_{\mathrm{lm}}$, so the two differ by that much.
- The Equilibrium Reactor's own solver stops when the squares of $\ln(\prod b_i^{\nu_i}/K)$ sum to $10^{-3}$, and the Gibbs Reactor's minimization within a similar margin; Equation [7](#eq:eo_chemical_equilibrium) is closed to $10^{-6}$, so their results differ by that.
- A loop whose inventory the specifications leave free stalls the Newton steps: a glycol loop with no make-up, or two columns around a recycle that both set their bottoms flow, so that the feed alone closes the overall balance.
- Units with equations of their own next to a stream of one compound are taken as black boxes, so flowsheets of a pure fluid solve more slowly.
- A PFR whose reaction runs away within one cell (a front of hundreds of kelvin over a few percent of its length) does not solve cell by cell, and the Newton steps from there stall; so can a pipe whose flow package changes its flow pattern, and its drop, by a jump between two ends of a cell. The other two solvers take them.
- A PFR's or a pipe's outlet is the cells' in the solution, and the unit's own integration after the pass: the two differ by the error of the unit's own steps.

#### Choosing a solver {#sec:choosing_solver}

| | Sequential-modular | Simultaneous | Equation-oriented |
|---|---|---|---|
| Unknowns | each recycle's torn stream, each adjust's variable, one at a time | every torn stream and adjust variable at once | every stream's T, P and flows, and the adjust variables |
| Unit operations | their own models, pass after pass | their own models, one pass per evaluation | their equations, with analytic derivatives; the others through their own models |
| Iteration | substitution, Wegstein or Broyden per recycle; nested adjust loops | damped Newton over passes | damped Newton over all the equations |
| Derivatives | none | finite differences of passes, or the units' sensitivities | analytic (flash by the implicit function theorem), differences for black boxes |
| Recycles closed to | the recycle tolerances | the recycle tolerances | $10^{-7}$ of each stream's flow |
| Best on | loosely coupled flowsheets, one recycle | interacting recycles, adjusts inside recycles, repeated solves | tightly coupled flowsheets of units with equations; design specifications |
| Weak on | interacting loops, adjusts inside recycles | flowsheets whose passes are slow (columns), since a Jacobian takes passes | flowsheets dominated by black boxes (CAPE-OPEN columns); loops of free inventory |

All three give the same answer within their tolerances. Each falls back to the one before it: the equation-oriented solver to the simultaneous solver, and the simultaneous solver to the sequential one.
