# Overview

The PVT Analysis tool takes a reservoir fluid from its laboratory PVT report to an equation of state model that the flowsheet and a reservoir simulator can use. It does four things:

1.  It characterizes the fluid: the heavy end of the reported composition (C7+ or a later fraction) is split in single carbon number fractions, each one receives critical properties, and the fractions are lumped in a few pseudocomponents.

2.  It simulates the experiments of the PVT report with a cubic equation of state: saturation pressure, constant composition expansion, differential liberation, constant volume depletion, separator test and swelling test, with the viscosities of the oil and of the gas.

3.  It fits properties of the heavy fractions to the measured data by regression, and reports how well the data determine each fitted variable.

4.  It calculates the minimum miscibility pressure of the fluid with an injection gas.

5.  It sends the fluid to the flowsheet (compounds, property package and a stream) and writes it for a reservoir simulator, as a compositional model or as black-oil tables.

The tool is part of the Patreon edition, for Premium and Premium+ members. It is the item **PVT Analysis** of the **Tools** menu of the flowsheet, in the Classic and in the Cross-Platform interfaces. The window opens for anyone; **Run**, **Fit**, **Send to flowsheet** and **Export** ask for the membership.

The tool has its own phase equilibrium calculations. They do not depend on the flash algorithm selected in the flowsheet, and the tool does not change it.

