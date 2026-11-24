# The window

The inputs are on the left and the results on the right.

Property package:  
the package of the flowsheet used in the study.

Solute:  
the solid compound.

Solid form of the solute:  
name of one of the solid forms stored in the solute (see below). Empty uses the fusion data of the compound.

Solvents:  
the compounds to be ranked. **Select all** and **Clear** act on the whole list.

Pressure:  
used by the property package; it has little effect on the solubility of a solid in a liquid.

Hot temperature and cold temperature:  
the dissolution and the isolation temperatures of a cooling crystallization. The ranking is calculated at both and the curves between them.

Curve points:  
number of temperatures of each solubility curve.

Solvents kept (ICH Q3C):  
leaves solvents out of the study according to their class in the ICH guideline for residual solvents (see below).

Antisolvent study:  
when enabled, the solubility in mixtures of a solvent and an antisolvent at one temperature.

Measured solubilities:  
solubilities from the laboratory or from the literature, to compare with the model (see below).

Results basis:  
how the solubility is shown: mass of solute per 100 g of solvent, weight percent, grams per litre of solution or mole fraction. Changing it redraws the results without calculating again.

**Fit NRTL to measured points** and **Store in package** are described below. **Run** calculates the study, **Stop** interrupts it and **Export** writes all the tables to a text file. **Load case** and **Save case** read and write the inputs as a `.dwsol` file. A case file saved next to the flowsheet with the same name is loaded when the window opens.

