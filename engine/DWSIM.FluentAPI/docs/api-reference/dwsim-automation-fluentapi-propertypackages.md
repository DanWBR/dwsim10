# PropertyPackages

`DWSIM.Automation.FluentAPI.PropertyPackages`

Canonical string identifiers for the property packages registered by `DWSIM.Automation.Automation3` on bootstrap, plus a nested [`PropertyPackages.Plus`](dwsim-automation-fluentapi-propertypackages-plus.md) class for DWSIMPlus-only packages that require a patron key. Pass any of these to [`WithPropertyPackage`](dwsim-automation-fluentapi-flowsheet.md).

## Methods

### `RequiresPlus(string)`

True when `name` matches a Plus / DWSIMPlus property package and therefore requires `Activate` to have been called first.

## Fields

### `BlackOil`

Black-oil correlation suite - single-component proxy for petroleum reservoirs.

### `CapeOpen`

CAPE-OPEN external property-package wrapper - registers any compliant 3rd-party PP.

### `ChaoSeader`

Chao-Seader - heavy-hydrocarbon vapour-liquid equilibrium correlation.

### `CoolProp`

CoolProp Helmholtz-energy reference EOS for ~120 pure fluids.

### `CoolPropIncompressibleMixture`

CoolProp incompressible mixtures - brines, glycol/water, etc.

### `CoolPropIncompressiblePure`

CoolProp incompressible-fluid correlations (pure thermal fluids).

### `GERG2008`

GERG-2008 wide-range reference EOS for natural-gas mixtures (21 components).

### `GraysonStreed`

Grayson-Streed - extension of Chao-Seader with hydrogen-rich mixtures support.

### `IdealElectrolyte`

Ideal electrolyte model - basic aqueous-ion behaviour without activity corrections.

### `LeeKeslerPlocker`

Lee-Kesler-Plöcker - predictive method for non-polar mixtures, accurate enthalpy/entropy.

### `ModifiedUNIFAC`

Modified UNIFAC (Dortmund) - improved temperature dependence and more groups.

### `ModifiedUNIFAC_NIST`

Modified UNIFAC (NIST) - alternative parameter set from NIST.

### `NRTL`

Non-Random Two-Liquid activity-coefficient model (Renon 1968) - strongly non-ideal liquids.

### `PCSAFT`

PC-SAFT EOS - physically motivated for chain-like and associating fluids.

### `PengRobinson`

Peng-Robinson cubic EOS (1976). General-purpose for hydrocarbons / non-polar mixtures.

### `PengRobinson1978`

Peng-Robinson with the 1978 alpha update - improves heavy-component vapour pressure.

### `PengRobinson1978Advanced`

PR78 with advanced binary-interaction handling and temperature-dependent kij.

### `PRSV2M`

Peng-Robinson-Stryjek-Vera 2 (matrix kij) - improves polar-system phase equilibrium.

### `PRSV2VL`

Peng-Robinson-Stryjek-Vera 2 (van Laar mixing rule).

### `Raoult`

Raoult's law - ideal liquid + ideal gas, valid only at low pressure for non-polar mixtures.

### `Seawater`

IAPWS-08 seawater - water with NaCl-based salinity model.

### `SoaveRedlichKwong`

Soave-Redlich-Kwong cubic EOS - general-purpose for gases / hydrocarbon mixtures.

### `SoaveRedlichKwongAdvanced`

SRK with advanced binary-interaction handling and temperature-dependent kij.

### `SteamTables`

IAPWS-IF97 steam tables - pure water across all phases.

### `UNIFAC`

UNIFAC group-contribution method - predictive activity coefficients without binary data.

### `UNIFAC_LL`

UNIFAC for liquid-liquid equilibrium (separate parameter set).

### `UNIQUAC`

UNIQUAC activity-coefficient model - polar / hydrogen-bonding liquid mixtures.

### `Wilson`

Wilson activity-coefficient model - completely miscible polar liquids.
