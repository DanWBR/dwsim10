# Overview

The Solubility and Solvent Screening tool calculates how much of a solid compound dissolves in the other compounds of a flowsheet, with the property package of the simulation. It answers the questions that come up when a crystallization is being designed: which solvent dissolves the product best when hot and worst when cold, how the solubility changes with temperature, and how much product an antisolvent brings out of solution.

The tool does not need streams or unit operations. It works on the compounds and on one property package of the open flowsheet, and it does not change the simulation.

The tool is part of the Patreon edition, for Premium+ members. It is the item **Solubility and Solvent Screening** of the **Utilities** menu of the flowsheet, in the Classic and in the Cross-Platform interfaces.

#### What is needed

A solute with fusion data:  
the compound has to carry a melting point and an enthalpy of fusion. The list of solutes shows only the compounds that have both, with the highest melting point first.

The solvents as compounds of the simulation:  
the tool screens the compounds that are already in the flowsheet. Add the candidate solvents to the simulation before opening it.

A property package that describes the liquid:  
an activity coefficient model (NRTL, UNIQUAC, UNIFAC and the like) or an equation of state. With NRTL and UNIQUAC the result depends on the interaction parameters of each solute and solvent pair.

