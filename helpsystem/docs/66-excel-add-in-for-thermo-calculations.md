# Excel Add-In for Thermo Calculations

#### Introduction

The DWSIM Excel Add-In exposes some of the internal Thermodynamic Property Calculation Routines to Microsoft Excel, including:

- Single Compound Properties (i.e. Boiling Point, Heat Capacity, Viscosity...)

- Single Phase Mixture Properties (i.e. Enthalpy, Entropy, Molar Weight, Thermal Conductivity, Viscosity...)

- Pressure-Temperature, Pressure-Enthalpy, Pressure-Entropy, Pressure-VaporFraction and Temperature-VaporFraction Flash Calculators, using an algorithm of your choice

- Other auxiliary functions

Property and Equilibrium calculation functionality is now available to Excel just as any other add-in function.

#### Installation

The Excel Add-In is part of DWSIM Simulator for Windows Desktop - you must install it first.

Remember the location where DWSIM was installed, as you’ll use this location to find the Add-In XLL file.

After installing DWSIM, open Excel and go to File \> Options \> Add-Ins \> Manage (Excel Add-Ins) \> Go \> Browse. More information: Add or remove add-ins in Excel

Look for **DWSIM.xll** in DWSIM’s installation directory if you’re running the 32-bit Excel version, otherwise look for for **DWSIM_64.xll** if you’re running the 64-bit version.

#### Usage

Functions exposed by this add-in will be grouped in a category named DWSIM:




![](images/screens64/Excel03.png)



Property and Equilibrium calculation functions require parameters that must be one or more values returned by **GetPropPackList**, **GetCompoundList**, **GetPropList**, **GetCompoundPropList** and **GetPhaseList**. They are self-explanatory, and will return values listed in a single column, so you probably will have to select some cells in a single column and call the functions using Ctrl+Shift+Enter:




![](images/screens64/Excel04.png)



For example, the **PTFlash** function requires the name of the Property Package to use, the compound names and mole fractions, temperature in K, pressure in Pa and you may optionally provide new interaction parameters that will override the ones used internally by DWSIM. The calculation results will be returned as a (n+2) x (4) matrix, where n is the number of compounds. First row will contain the phase names (Vapor, Liquid, Liquid2 and Solid, in this order), the second will contain the phase mole fractions and the other lines will contain the compound mole fractions in the corresponding phases:




![](images/screens64/Excel05.png)



For PH, PS, TVF and PVF flash calculation functions, and additional line is returned that will contain the temperature in K or pressure in Pa in the first column.

#### Overriding Interaction Parameters

You can directly override the interaction parameters used by Property Packages when calling calculations from Excel by providing n x n matrices containing the values, where n is the number of compounds. This feature is optional and should be used only when you know exactly what you are doing.

The following table shows the user-definable interaction parameters for each Property Package:




![](images/screens64/excel1.png)



