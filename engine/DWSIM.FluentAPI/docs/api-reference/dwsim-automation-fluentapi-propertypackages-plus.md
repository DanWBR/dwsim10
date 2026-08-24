# PropertyPackages.Plus

`DWSIM.Automation.FluentAPI.PropertyPackages.Plus`

Plus / DWSIMPlus property packages - auto-loaded from `ppacks\` when DWSIMPlus is installed. Their use requires an active patron key - call `Activate` first or [`WithPropertyPackage`](dwsim-automation-fluentapi-flowsheet.md) will throw via [`RequirePlus`](dwsim-automation-fluentapi-license.md).

## Properties

### `All`

Every Plus property-package name as a flat list (electrolyte + ThermoPack suites).

## Fields

### `CarbonCapture`

CCUS Carbon Capture - eNRTL for CO2 capture with MEA, DEA, MDEA, PZ, AMP.

### `CO2Storage`

CCUS CO2 Storage - eNRTL/Duan-Sun for CO2 geological storage in saline aquifers.

### `CO2Transport`

CCUS CO2 Transport - Span-Wagner EOS for pure CO2, PR for CO2-rich mixtures.

### `ElectrolyteNRTL`

Electrolyte NRTL - ion-aware extension of NRTL for aqueous electrolyte systems.

### `ExtendedUNIQUAC`

Extended UNIQUAC for electrolytes - Thomsen-Rasmussen model with Debye-Hückel term.

### `Glycol`

Glycol-water mixtures with NRTL parameters tuned for MEG/DEG/TEG systems.

### `HCl`

H2O-HCl Pitzer model for hydrogen-chloride aqueous systems.

### `KentEisenberg`

Kent-Eisenberg model for amine-CO2-H2S equilibrium (gas-treating units).

### `MBWR19`

ThermoPack MBWR19 - modified Benedict-Webb-Rubin (19-parameter) for cryogenic fluids.

### `MBWR32`

ThermoPack MBWR32 - 32-parameter MBWR variant for high-accuracy cryogenic mixtures.

### `NISTMEOS`

ThermoPack NIST-MEOS - multi-parameter equation of state with NIST coefficients.

### `PatelTeja`

ThermoPack Patel-Teja cubic EOS.

### `PCPSAFT`

ThermoPack PCP-SAFT - perturbed-chain polar SAFT (dipolar/quadrupolar fluids).

### `PRCPA`

ThermoPack PR-CPA - Peng-Robinson with cubic-plus-association term (water/alcohols).

### `ReaktoroAqueous`

Reaktoro-backed aqueous chemistry (SUPCRT-style database, full speciation equilibrium).

### `SAFTVRMie`

ThermoPack SAFT-VR Mie - variable-range Mie potential SAFT (general).

### `SAFTVRQMie`

ThermoPack SAFT-VRQ Mie - quantum corrections (helium, hydrogen, neon).

### `SchmidtWensel`

ThermoPack Schmidt-Wensel cubic EOS (1980).

### `SourWater`

Sour-water stripper model - H2O / NH3 / H2S / CO2 weak-electrolyte equilibrium.

### `SPCSAFT`

ThermoPack simplified PC-SAFT.

### `SRKCPA`

ThermoPack SRK with cubic-plus-association term (water/alcohols).
