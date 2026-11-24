# Measured solubilities

Measured solubilities are typed one per line, as the solvent, the temperature in the unit of the flowsheet and the mole fraction of the solute in the saturated solution, separated by semicolons:

    Ethanol; 25; 0.024
    Ethanol; 40; 0.050
    Toluene; 25; 0.29

The decimal separator is the one of the operating system. The solvent has to be a compound of the simulation, with its name as the flowsheet shows it. The points appear as markers on the chart of the solubility curves, in the color of the curve of their solvent, and in the Measured tab.

The activity coefficient of a measured point is the one the fusion data imply for it, the right-hand side of equation [1 (How the solubility is calculated)](62-how-the-solubility-is-calculated.md#eq:solscreen_sle) divided by the measured mole fraction. Comparing it with the one of the model shows whether a deviation comes from the interaction parameters. When the two agree and the solubilities still differ from other sources, the fusion data are the place to look.

