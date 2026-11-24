# The window

The inputs are on the left and the results on the right. **New study**, **Load study** and **Save study** are on top. A study holds everything the window shows (composition, characterization, experiments with their measured tables, weights and the fitted fluid) in a file with the extension `.dwpvt`. A study with the name of the flowsheet, saved next to it, is loaded when the window opens.

Temperatures, pressures and densities are shown in the system of units of the flowsheet. The other quantities have the unit written in their label.

#### Fluid and characterization

Equation of state:  
Peng-Robinson, Peng-Robinson 1978 or Soave-Redlich-Kwong, always with volume translation.

Reservoir temperature:  
the temperature of the experiments that do not give their own.

Heavy end:  
the name, amount, molar mass and specific gravity of the last fraction of the report. The amount is in the basis of the composition.

Distribution shape and lowest molar mass:  
the two parameters of the gamma distribution of molar mass that splits the heavy end. A shape of 1 is the exponential distribution, the usual choice when the report does not have an extended analysis. The default lowest molar mass is 90 g/mol for a C7+.

Last carbon number of the split:  
the fractions go from the first carbon number of the heavy end to this one, and the last fraction holds what is left.

Pseudocomponents of the heavy end:  
the number of groups the fractions are lumped in. Zero keeps every fraction. Five is a common choice for process and reservoir work.

Critical properties of the fractions:  
Kesler-Lee, Twu or Riazi-Daubert.

Methane interaction constant:  
the constant of the rule that gives the interaction parameter of methane with each heavy fraction. The recommended value is 0.18, and zero leaves these pairs without interaction parameters.

Viscosity model:  
Lohrenz-Bray-Clark, the correlation of compositional reservoir simulators, or the corresponding states model of Pedersen.

#### Composition

One row per component, with its amount in any basis (mole percent or mole fraction). The names of the usual components of a PVT report are recognized: N2, CO2, H2S, C1, C2, C3, i-C4, n-C4, i-C5, n-C5 and C6. A row with molar mass and specific gravity is a petroleum fraction that the report gives by itself (a C7, C8 or C9 of an extended analysis, for example). Rows can be pasted from a spreadsheet.

#### Experiments and measured data

The list on the left of the group selects an experiment, **Remove** deletes it, and **Add** creates one of the kind chosen in the second list. Each experiment has a name, a temperature (empty for the reservoir temperature), its single measured values and a table with one row per pressure. Empty cells are values that were not measured. Rows can be pasted from a spreadsheet, with Ctrl+V in the Classic interface and with the **Paste** button in the Cross-Platform one.

In the experiments that start at the saturation point, the first row of the table is that point: its pressure is the measured saturation pressure.

Saturation pressure:  
the bubble point or the dew point at the temperature of the experiment, and the density of the fluid there.

Constant composition expansion:  
the fluid is expanded in a closed cell. Columns: relative volume (total volume over the volume at saturation), the Y function of an oil below its bubble point, the density, the Z factor and the compressibility of the single phase above saturation, and the liquid volume as a fraction of the volume at saturation for a gas condensate.

Differential liberation:  
the oil is depleted in stages at reservoir temperature and the gas of each stage is removed. Columns: the oil volume and the cumulative gas removed, both referred to the oil volume at the bubble point, the oil density, the Z factor, the volume factor and the specific gravity of the gas, the usual Rs, Bo and Bt, which are referred to the residual oil at standard conditions, and the viscosities of the oil and of the gas. Pressures above the bubble point give rows of undersaturated oil, which is how the oil viscosity of a report is usually tabulated. The rows go in descending order of pressure.

Constant volume depletion:  
the cell is depleted in stages and gas is removed until the volume returns to the volume at the saturation pressure. Columns: the liquid volume as a fraction of the cell, the cumulative moles produced as a fraction of the initial moles, the Z factor of the gas, the two-phase Z factor and the composition of the produced gas.

Separator test:  
one stage or more in series ending in the stock tank. The table has one row per stage, with its pressure and temperature. Values: the gas-oil ratio of each stage and the total one, the formation volume factor at the bubble point, the stock tank oil density and the gas specific gravity.

Swelling test:  
an injection gas, written as `component=amount, ...`, is added to the oil in steps. Columns: the mole fraction of gas in the mixture, the saturation pressure, the swelling factor (saturated volume over the saturated volume of the original oil) and the density at saturation.

Minimum miscibility pressure:  
the lowest pressure at which the injection gas, written as in the swelling test, becomes miscible with the oil after repeated contacts. The experiment takes the first pressure of the search, which has to be below the answer, and a measured value when there is one (from a slim tube test). The result is on the Summary tab, with the kind of drive, and the Experiments tab shows the shortest tie line against pressure. The search takes from a few seconds to about half a minute, and a fit does not repeat it.

The constant composition expansion also has a viscosity column for the single phase, and the constant volume depletion one for the gas.

#### Fit to the measured data

The first list has the variables a fit can move, with a check box and the limits of each one. The second list has the weight of each measured quantity. **Suggested steps** runs the fit in a staged order, and **Starting points** repeats it from several starting values and keeps the best result.

#### Actions and results

**Run** characterizes the fluid and simulates every experiment. **Fit** runs the regression and keeps the fitted fluid in the study; **Discard fit** returns to the characterization. **Send to flowsheet** and **Export** are described below.

Summary:  
one row per measured quantity of each experiment, with the number of points, the average absolute deviation and the largest one, and the notes of the fit.

Fluid:  
the components of the model with their mole fractions, molar masses, critical properties, acentric factors and volume translations.

Experiments:  
the simulated table of the selected experiment next to the measured values, and a plot of the selected quantity against pressure.

Log:  
the messages of the session.

