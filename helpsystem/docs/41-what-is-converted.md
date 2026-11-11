# What Is Converted

#### Unit operations

The tables list the units of each simulator that have an equivalent set. Anything else becomes a placeholder.

###### Aspen Plus







| **Aspen Plus block** | **DWSIM object** |
|:---|:---|
| Mixer, FSplit | Mixer, Splitter |
| Heater | Heater or Cooler. Two heaters joined by a heat stream become one Heat Exchanger. |
| Flash2, Flash3 | Separator Vessel |
| Valve, Pump, Compr | Valve, Pump, Compressor or Expander |
| HeatX | Heat Exchanger |
| RadFrac | Distillation Column or Absorption Column |
| RStoic | Conversion Reactor |
| RYield | Conversion Reactor, with the reactions to be written by hand: the block gives yields and no stoichiometry |
| REquil | Equilibrium Reactor |
| RGibbs | Gibbs Reactor |
| RPlug, RCSTR | PFR, CSTR, with power-law kinetics |
| Sep | Compound Separator |



###### Aspen HYSYS







| **HYSYS operation** | **DWSIM object** |
|:---|:---|
| Mixer, Tee | Mixer, Splitter |
| Valve, Pump, Compressor, Expander | the same object |
| Cooler, Heater, Heat Exchanger | Cooler, Heater, Heat Exchanger |
| Separator, 3-Phase Separator, Tank | Separator Vessel |
| Conversion, Equilibrium and Gibbs Reactor | the same reactor, with the reactions of its set |
| Plug Flow Reactor, CSTR | PFR, CSTR, with their size |
| Component Splitter | Compound Separator |
| Distillation, Absorber, Reboiled and Refluxed Absorber | Distillation Column or Absorption Column |
| Recycle | a Recycle block of DWSIM, at the same stream |
| Sub-flowsheet (template) | its units, brought up to the main flowsheet |



###### AVEVA Process Simulation







| **AVEVA model** | **DWSIM object** |
|:---|:---|
| Source, Sink | the feed and product streams themselves |
| Mix, Split, Header | Mixer, Splitter |
| Valve, Pump, Compressor, Expander, Turbine, Pipe | Valve, Pump, Compressor, Expander |
| HX, HXU, HXTR | Heater, Cooler or Heat Exchanger |
| Drum, Separator | Separator Vessel |
| CompSplit | Compound Separator |
| Column | Distillation Column or Absorption Column |
| CNVR, EQR, GMR | Conversion, Equilibrium and Gibbs Reactor |
| PFR, CSTR | PFR, CSTR, with their size |



###### COCO







| **COCO unit** | **DWSIM object** |
|:---|:---|
| Mixer, Splitter, Heater/Cooler, Flash, Valve, Pump, Compressor, Expander, Heat Exchanger, Compound Splitter | the same object |
| Fixed Conversion Reactor, Gibbs Reactor, PFR, CSTR | the same reactor |
| ChemSep column | Distillation Column or Absorption Column |
| Flowsheet unit operation | its units, brought up to the main flowsheet |



###### PRO/II







| **PRO/II unit** | **DWSIM object** |
|:---|:---|
| Mixer, Splitter | Mixer, Splitter |
| Flash | Separator Vessel |
| Valve, Pump, Compressor, Expander | the same object |
| HX | Heater, Cooler or Heat Exchanger |
| Column | Distillation Column or Absorption Column |



#### Property packages

The property method of the source is translated to the DWSIM package that stands for it. Peng-Robinson, Soave-Redlich-Kwong, NRTL, UNIQUAC, Wilson, UNIFAC, Chao-Seader, Grayson-Streed and the steam tables go to the package of the same name. A variant of a method (a modified alpha function, a mixing rule of its own, a different vapour phase model) goes to the standard package and is marked as approximate. Amine and acid gas packages go to the CO2 Capture package, sour water methods to Sour Water, glycol packages to Glycol (NRTL) and electrolyte methods to Electrolyte NRTL. These packages need the membership level that includes them.

Interaction parameters are left out: DWSIM uses the values of its own databases, and the Build page says how many parameters the source file had. Check the binary parameters of the pairs that matter to the process after the import.

#### Logical operations

Design specifications, calculators, spreadsheets, set and adjust operations and controllers are listed in the Units page as not converted, with what each one does in the source. The value that such an operation found is usually in the file already, as the specification of the unit it acts on, so the imported flowsheet solves at the same point. To make DWSIM follow a change the way the source does, add an Adjust, a Specification block or a script after the import.

