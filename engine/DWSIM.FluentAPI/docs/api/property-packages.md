# Property Packages

`PropertyPackages` is a static class with **string constants** for every
package registered by `DWSIM.Automation.Automation3` at bootstrap. Pass any
of these to `Flowsheet.WithPropertyPackage`.

```csharp
fs.WithPropertyPackage(PropertyPackages.PengRobinson);
fs.WithPropertyPackage(PropertyPackages.NRTL);
fs.WithPropertyPackage(PropertyPackages.Plus.ElectrolyteNRTL); // requires patron key
```

## Free packages

| Constant | Display name | Notes |
|---|---|---|
| `PengRobinson` | Peng-Robinson (PR) | General-purpose cubic EOS for hydrocarbons. |
| `PengRobinson1978` | Peng-Robinson 1978 (PR78) | 1978 alpha update; better heavies. |
| `PengRobinson1978Advanced` | Peng-Robinson 1978 Advanced | Temperature-dependent kij. |
| `PRSV2M` | Peng-Robinson-Stryjek-Vera 2 (matrix) | Polar mixture phase equilibrium. |
| `PRSV2VL` | Peng-Robinson-Stryjek-Vera 2 (van Laar) | van Laar mixing rule. |
| `SoaveRedlichKwong` | Soave-Redlich-Kwong (SRK) | General-purpose cubic EOS. |
| `SoaveRedlichKwongAdvanced` | SRK Advanced | T-dependent kij. |
| `LeeKeslerPlocker` | Lee-Kesler-Plöcker | Predictive, accurate enthalpy/entropy. |
| `ChaoSeader` | Chao-Seader | Heavy-HC vapour-liquid equilibrium. |
| `GraysonStreed` | Grayson-Streed | Chao-Seader extension with H₂. |
| `Raoult` | Raoult's Law | Ideal liquid/gas, low pressure only. |
| `NRTL` | NRTL | Strongly non-ideal liquids. |
| `UNIQUAC` | UNIQUAC | Polar / hydrogen-bonding liquids. |
| `Wilson` | Wilson | Miscible polar liquids. |
| `UNIFAC` | UNIFAC | Predictive activity coefficients. |
| `UNIFAC_LL` | UNIFAC-LL | Liquid-liquid equilibrium. |
| `ModifiedUNIFAC` | Modified UNIFAC (Dortmund) | Improved T dependence. |
| `ModifiedUNIFAC_NIST` | Modified UNIFAC (NIST) | NIST parameter set. |
| `SteamTables` | Steam Tables (IAPWS-IF97) | Pure water. |
| `Seawater` | Seawater IAPWS-08 | Water with salinity. |
| `BlackOil` | Black Oil | Petroleum reservoir proxy. |
| `CoolProp` | CoolProp | Reference Helmholtz EOS, ~120 fluids. |
| `CoolPropIncompressiblePure` | CoolProp (Incompressible Fluids) | Thermal fluids. |
| `CoolPropIncompressibleMixture` | CoolProp (Incompressible Mixtures) | Brines, glycol/water. |
| `GERG2008` | GERG-2008 | Natural-gas reference EOS. |
| `PCSAFT` | PC-SAFT | Chain / associating fluids. |
| `IdealElectrolyte` | Ideal Electrolyte | Basic aqueous-ion model. |
| `CapeOpen` | CAPE-OPEN | Wraps any compliant 3rd-party PP. |

## Plus packages — `PropertyPackages.Plus`

Require an active patron key
([`License.CheckLicense`](license.md)). `Flowsheet.WithPropertyPackage`
calls `License.RequirePlus()` when handed a Plus name.

| Constant | Display name |
|---|---|
| `Plus.ElectrolyteNRTL` | Electrolyte NRTL (Aqueous Electrolytes) |
| `Plus.ExtendedUNIQUAC` | Extended UNIQUAC (Aqueous Electrolytes) |
| `Plus.ReaktoroAqueous` | Reaktoro (Aqueous Electrolytes) |
| `Plus.Glycol` | Glycol (NRTL) |
| `Plus.HCl` | H2O-HCl (Pitzer) |
| `Plus.KentEisenberg` | Kent-Eisenberg |
| `Plus.SourWater` | Sour Water |
| `Plus.MBWR19` | MBWR19 (ThermoPack) |
| `Plus.MBWR32` | MBWR32 |
| `Plus.NISTMEOS` | NIST-MEOS |
| `Plus.PatelTeja` | Patel-Teja |
| `Plus.PCPSAFT` | PCP-SAFT |
| `Plus.PRCPA` | PR-CPA |
| `Plus.SAFTVRMie` | SAFT-VR Mie |
| `Plus.SAFTVRQMie` | SAFT-VRQ Mie |
| `Plus.SchmidtWensel` | Schmidt-Wensel |
| `Plus.SPCSAFT` | SPC-SAFT |
| `Plus.SRKCPA` | SRK-CPA |

`PropertyPackages.Plus.All` returns the flat list;
`PropertyPackages.RequiresPlus(name)` answers whether a name is gated.

## Configuring after instantiation

The package created by `WithPropertyPackage(name)` can be tuned through the
`PropertyPackageBuilder` via the overload that takes a configurator:

```csharp
fs.WithPropertyPackage(PropertyPackages.NRTL, pp => pp
    .WithFlashApproach(FlashCalculationApproachType.NestedLoops)
    .WithFlashSetting(FlashSetting.UsePhaseStability, true)
    .ConfigureNRTL(n => n
        .WithBinary("Water", "Ethanol", a12: 6.49, a21: -1.41, alpha: 0.30)));
```

Typed sub-builders cover the most common interaction parameters:

| Builder method | Sub-builder | Purpose |
|---|---|---|
| `ConfigurePR(Action<PRConfig>)` | `PRConfig.WithKij(c1, c2, kij)` | PR / PR78 / PRSV2 binary kij. |
| `ConfigureSRK(Action<SRKConfig>)` | `SRKConfig.WithKij(...)` | SRK kij. |
| `ConfigureNRTL(Action<NRTLConfig>)` | `WithBinary(c1, c2, a12, a21, alpha, b12, b21)` | NRTL binary parameters. |
| `ConfigureUNIQUAC(Action<UNIQUACConfig>)` | `WithBinary(c1, c2, a12, a21, b12, b21)` | UNIQUAC binary parameters. |
| `ConfigureWilson(Action<WilsonConfig>)` | `WithBinary(cas1, cas2, a12, a21)` | Wilson binary parameters (keyed by CAS). |

For settings outside the typed surface, use the escape hatch:

```csharp
fs.WithPropertyPackage(PropertyPackages.PengRobinson, pp =>
    pp.Configure(inner => { /* mutate inner directly */ }));
```

## Listing what's loaded

```csharp
foreach (var name in fs.AvailablePropertyPackages)
    Console.WriteLine(name);
```

Plus packages only appear in this list when their DLLs in `ppacks/` have
loaded successfully (i.e., on a DWSIM build that ships them).
