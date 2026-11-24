# The window

The inputs are on the left and the results on the right. **Load case** and **Save case** read and write everything the window holds to a file with the extension `.dwrsc`. A case file with the name of the flowsheet, saved next to it, is loaded when the window opens.

#### Reservoir simulator

**Check** looks for the Linux shell, Python and the OPM packages in the Python environment whose path is given, and says what is missing. **Install** creates the environment and downloads OPM Flow. The path is inside the Linux environment (`~/dwsim-opm/venv` by default) and takes letters, digits and the characters `/ . _ - ~`.

#### Reservoir model

The reservoir model is given by its Windows path, or by a path inside WSL when it starts with `/` or `~`. The output folder of the reservoir simulator is `rc_out` next to the reservoir model unless another one is given. **Read the reservoir model** starts the simulator, lists the units of the reservoir model, its report steps, wells and groups, and stops it.

The tool runs a copy of the reservoir model (with `_RC` in its name) that asks for the summary vectors the coupling reads. The original file is left as it is.

#### Flowsheet

Pipe Network of the wells:  
the network whose Source blocks stand for the producers.

Stock tank oil composition from, Separator gas composition from:  
two material streams of the flowsheet. Only their compositions are used. The oil and gas rates of the reservoir are surface volumes; they become moles of these two mixtures, and water when the well produces it.

Water compound:  
the compound that receives the produced water.

Stock tank oil density:  
converts the oil volume to mass. It has to be the density the reservoir model uses (`DENSITY` keyword). Zero takes it from the property package of the flowsheet.

#### Wells

Each producer of the reservoir model is mapped to one Source block of the network. **Map the wells to the sources by name** fills the table for the sources whose tag carries the name of the well. During the run the source is set to the mass flow of the well, and the pressure the network finds at it is what goes back to the reservoir.

#### Coupling

Relaxation of the THP limit:  
the fraction of the distance between the current limit and the pressure of the network that the limit moves each step. A value of 0.5 is a good start; 1 takes the pressure of the network as it is.

Largest THP limit change per step:  
in the pressure unit of the reservoir model. A limit that jumps above what a well can deliver makes the reservoir simulator shut the well, and the well stays shut when the limit falls again. A bound of a few bar per step lets the rate follow the limit. Zero does not bound it.

Reuse the flowsheet solution:  
skips the solution of the flowsheet in a step where the rates of every well changed less than this fraction since the last solution.

Time limit of a flowsheet solve:  
a step whose flowsheet does not solve in this time counts as failed.

Report steps to run:  
zero runs the reservoir model to its end.

Run the reservoir alone:  
keeps the THP limits of the reservoir model and leaves the flowsheet out. It is the reference to compare a coupled run with.

#### Returns from the flowsheet to the reservoir

A return reads a value from the flowsheet after each step and sets it in the reservoir for the next one.







| **Return** | **What it sets** |
|:---|:---|
| Gas injection, Water injection | The injection rate of a group (`GCONINJE`; the injectors of the group pass to group control) or of one injector (`WCONINJE`). |
| Oil, Gas, Water or Liquid production limit | The limit of a group (`GCONPROD`). The reservoir simulator shares the cut among the wells by the group rules of the reservoir model. |
| Lift gas supply | The lift gas rate of each producer of a group (`WELTARG ... LIFT`), split evenly or by the weights given. The lift tables of the wells need the lift gas axis. |



The value comes from a material stream read as gas, as water or as stock tank oil (standard volume), from a property of any object, or from a constant. It is multiplied by the factor, kept between the minimum and the maximum, and can be under-relaxed. Constants and bounds are in the units of the reservoir model; a maximum of zero means unbounded. The target is a group of the reservoir model, or an injector for the injection returns.

#### Tables for the reservoir model

Two tables can be generated from the fluid of the flowsheet, so that the reservoir and the surface work with the same fluid.

Black-oil PVT:  
the keywords `DENSITY`, `PVTO`, `PVDG` and `PVTW`, by recombination of the stock tank oil with the separator gas at each gas-oil ratio, taken to the bubble point at the reservoir temperature with the property package of the flowsheet. The stock tank oil density of the table is copied to the window.

Lift table:  
a `VFPPROD` table for a tubing given by its length, vertical rise and diameter, computed with the Pipe unit operation over the oil rates, tubing head pressures, water cuts, gas-oil ratios and lift gas rates listed. Points the well cannot flow are extrapolated and counted in the status line.

Each table is saved as an include file. The PVT table goes in the `PROPS` section of the reservoir model, in the place of the keywords it replaces; the lift table goes in the `SCHEDULE` section, before the well controls that use it.

#### Running and results

**Run** saves a private copy of the flowsheet and works on it, so the open flowsheet stays as it is and remains usable during the run. **Stop** ends the run after the current step and keeps what was computed. The results follow the run:

Summary:  
cumulative oil, final rate and pressure, flowsheet solutions that succeeded and failed, and the notes of the run.

Field:  
oil, gas and water of the coupled wells against time, with the injection and the average reservoir pressure.

Wells:  
oil rate and THP limit of each well against time, and a table with the gas-oil ratio, water cut, bottom-hole pressure, THP limit and network pressure per step.

Returns:  
the value each return had in force and the value read from the flowsheet, step by step.

Log:  
the messages of the run.

**Export CSV** writes the three tables to a file.

#### Notes of a run

The notes on the Summary tab point to what deserves a look:

The flowsheet did not solve in some steps:  
in those steps the THP limits and the returns kept the values of the last good step. The flowsheet copy is loaded again before the next step. A Pipe Network solves poorly at very low rates; wells that the reservoir model cuts to a small rate can lose production while the limit stays at the value of the last good step.

The reservoir injected less than the target:  
the injector is shut or at its pressure limit.

The limit never acted:  
the group stayed below the limit during the whole run.

Wells stopped producing:  
with the step in which each one stopped.

