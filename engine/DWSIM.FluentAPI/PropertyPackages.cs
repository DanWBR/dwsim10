using System;
using System.Collections.Generic;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Canonical string identifiers for the property packages registered by
    /// <see cref="DWSIM.Automation.Automation3"/> on bootstrap, plus a nested
    /// <see cref="Plus"/> class for DWSIMPlus-only packages that require a patron key.
    /// Pass any of these to <see cref="Flowsheet.WithPropertyPackage"/>.
    /// </summary>
    public static class PropertyPackages
    {
        /// <summary>Peng-Robinson cubic EOS (1976). General-purpose for hydrocarbons / non-polar mixtures.</summary>
        public const string PengRobinson = "Peng-Robinson (PR)";
        /// <summary>Peng-Robinson with the 1978 alpha update - improves heavy-component vapour pressure.</summary>
        public const string PengRobinson1978 = "Peng-Robinson 1978 (PR78)";
        /// <summary>PR78 with advanced binary-interaction handling and temperature-dependent kij.</summary>
        public const string PengRobinson1978Advanced = "Peng-Robinson 1978 Advanced";
        /// <summary>Peng-Robinson-Stryjek-Vera 2 (matrix kij) - improves polar-system phase equilibrium.</summary>
        public const string PRSV2M = "Peng-Robinson-Stryjek-Vera 2 (PRSV2-M)";
        /// <summary>Peng-Robinson-Stryjek-Vera 2 (van Laar mixing rule).</summary>
        public const string PRSV2VL = "Peng-Robinson-Stryjek-Vera 2 (PRSV2-VL)";
        /// <summary>Soave-Redlich-Kwong cubic EOS - general-purpose for gases / hydrocarbon mixtures.</summary>
        public const string SoaveRedlichKwong = "Soave-Redlich-Kwong (SRK)";
        /// <summary>SRK with advanced binary-interaction handling and temperature-dependent kij.</summary>
        public const string SoaveRedlichKwongAdvanced = "Soave-Redlich-Kwong Advanced";
        /// <summary>Lee-Kesler-Plöcker - predictive method for non-polar mixtures, accurate enthalpy/entropy.</summary>
        public const string LeeKeslerPlocker = "Lee-Kesler-Plöcker";
        /// <summary>Chao-Seader - heavy-hydrocarbon vapour-liquid equilibrium correlation.</summary>
        public const string ChaoSeader = "Chao-Seader";
        /// <summary>Grayson-Streed - extension of Chao-Seader with hydrogen-rich mixtures support.</summary>
        public const string GraysonStreed = "Grayson-Streed";
        /// <summary>Raoult's law - ideal liquid + ideal gas, valid only at low pressure for non-polar mixtures.</summary>
        public const string Raoult = "Raoult's Law";
        /// <summary>Non-Random Two-Liquid activity-coefficient model (Renon 1968) - strongly non-ideal liquids.</summary>
        public const string NRTL = "NRTL";
        /// <summary>UNIQUAC activity-coefficient model - polar / hydrogen-bonding liquid mixtures.</summary>
        public const string UNIQUAC = "UNIQUAC";
        /// <summary>Wilson activity-coefficient model - completely miscible polar liquids.</summary>
        public const string Wilson = "Wilson";
        /// <summary>UNIFAC group-contribution method - predictive activity coefficients without binary data.</summary>
        public const string UNIFAC = "UNIFAC";
        /// <summary>UNIFAC for liquid-liquid equilibrium (separate parameter set).</summary>
        public const string UNIFAC_LL = "UNIFAC-LL";
        /// <summary>Modified UNIFAC (Dortmund) - improved temperature dependence and more groups.</summary>
        public const string ModifiedUNIFAC = "Modified UNIFAC (Dortmund)";
        /// <summary>Modified UNIFAC (NIST) - alternative parameter set from NIST.</summary>
        public const string ModifiedUNIFAC_NIST = "Modified UNIFAC (NIST)";
        /// <summary>IAPWS-IF97 steam tables - pure water across all phases.</summary>
        public const string SteamTables = "Steam Tables (IAPWS-IF97)";
        /// <summary>IAPWS-08 seawater - water with NaCl-based salinity model.</summary>
        public const string Seawater = "Seawater IAPWS-08";
        /// <summary>Black-oil correlation suite - single-component proxy for petroleum reservoirs.</summary>
        public const string BlackOil = "Black Oil";
        /// <summary>CoolProp Helmholtz-energy reference EOS for ~120 pure fluids.</summary>
        public const string CoolProp = "CoolProp";
        /// <summary>CoolProp incompressible-fluid correlations (pure thermal fluids).</summary>
        public const string CoolPropIncompressiblePure = "CoolProp (Incompressible Fluids)";
        /// <summary>CoolProp incompressible mixtures - brines, glycol/water, etc.</summary>
        public const string CoolPropIncompressibleMixture = "CoolProp (Incompressible Mixtures)";
        /// <summary>GERG-2008 wide-range reference EOS for natural-gas mixtures (21 components).</summary>
        public const string GERG2008 = "GERG-2008";
        /// <summary>PC-SAFT EOS - physically motivated for chain-like and associating fluids, including polymers.</summary>
        public const string PCSAFT = "PC-SAFT (with Association Support) (.NET Code)";
        /// <summary>Ideal electrolyte model - basic aqueous-ion behaviour without activity corrections.</summary>
        public const string IdealElectrolyte = "Ideal Electrolyte";
        /// <summary>CAPE-OPEN external property-package wrapper - registers any compliant 3rd-party PP.</summary>
        public const string CapeOpen = "CAPE-OPEN";

        /// <summary>
        /// Plus / DWSIMPlus property packages - auto-loaded from <c>ppacks\</c> when
        /// DWSIMPlus is installed. Their use requires an active patron key - call
        /// <see cref="License.Activate"/> first or <see cref="Flowsheet.WithPropertyPackage"/>
        /// will throw via <see cref="License.RequirePlus"/>.
        /// </summary>
        public static class Plus
        {
            /// <summary>Electrolyte NRTL - ion-aware extension of NRTL for aqueous electrolyte systems.</summary>
            public const string ElectrolyteNRTL = "Electrolyte NRTL (Aqueous Electrolytes)";
            /// <summary>Extended UNIQUAC for electrolytes - Thomsen-Rasmussen model with Debye-Hückel term.</summary>
            public const string ExtendedUNIQUAC = "Extended UNIQUAC (Aqueous Electrolytes)";
            /// <summary>Reaktoro-backed aqueous chemistry (SUPCRT-style database, full speciation equilibrium).</summary>
            public const string ReaktoroAqueous = "Reaktoro (Aqueous Electrolytes)";
            /// <summary>Glycol-water mixtures with NRTL parameters tuned for MEG/DEG/TEG systems.</summary>
            public const string Glycol = "Glycol (NRTL)";
            /// <summary>H2O-HCl Pitzer model for hydrogen-chloride aqueous systems.</summary>
            public const string HCl = "H2O-HCl (Pitzer)";
            /// <summary>Kent-Eisenberg model for amine-CO2-H2S equilibrium (gas-treating units).</summary>
            public const string KentEisenberg = "Kent-Eisenberg";
            /// <summary>Sour-water stripper model - H2O / NH3 / H2S / CO2 weak-electrolyte equilibrium.</summary>
            public const string SourWater = "Sour Water";

            /// <summary>ThermoPack MBWR19 - modified Benedict-Webb-Rubin (19-parameter) for cryogenic fluids.</summary>
            public const string MBWR19 = "MBWR19";
            /// <summary>ThermoPack MBWR32 - 32-parameter MBWR variant for high-accuracy cryogenic mixtures.</summary>
            public const string MBWR32 = "MBWR32";
            /// <summary>ThermoPack NIST-MEOS - multi-parameter equation of state with NIST coefficients.</summary>
            public const string NISTMEOS = "NIST-MEOS";
            /// <summary>ThermoPack Patel-Teja cubic EOS.</summary>
            public const string PatelTeja = "Patel-Teja";
            /// <summary>ThermoPack PCP-SAFT - perturbed-chain polar SAFT (dipolar/quadrupolar fluids).</summary>
            public const string PCPSAFT = "PCP-SAFT";
            /// <summary>ThermoPack PR-CPA - Peng-Robinson with cubic-plus-association term (water/alcohols).</summary>
            public const string PRCPA = "PR-CPA";
            /// <summary>ThermoPack SAFT-VR Mie - variable-range Mie potential SAFT (general).</summary>
            public const string SAFTVRMie = "SAFT-VR Mie";
            /// <summary>ThermoPack SAFT-VRQ Mie - quantum corrections (helium, hydrogen, neon).</summary>
            public const string SAFTVRQMie = "SAFT-VRQ Mie";
            /// <summary>ThermoPack Schmidt-Wensel cubic EOS (1980).</summary>
            public const string SchmidtWensel = "Schmidt-Wensel";
            /// <summary>ThermoPack simplified PC-SAFT.</summary>
            public const string SPCSAFT = "SPC-SAFT";
            /// <summary>ThermoPack SRK with cubic-plus-association term (water/alcohols).</summary>
            public const string SRKCPA = "SRK-CPA";

            /// <summary>CCUS Carbon Capture - eNRTL for CO2 capture with MEA, DEA, MDEA, PZ, AMP.</summary>
            public const string CarbonCapture = "CO2 Capture (eNRTL)";
            /// <summary>CCUS CO2 Transport - Span-Wagner EOS for pure CO2, PR for CO2-rich mixtures.</summary>
            public const string CO2Transport = "CO2 Transport (Span-Wagner/PR)";
            /// <summary>CCUS CO2 Storage - eNRTL/Duan-Sun for CO2 geological storage in saline aquifers.</summary>
            public const string CO2Storage = "CO2 Storage (eNRTL/Duan-Sun)";

            /// <summary>Every Plus property-package name as a flat list (electrolyte + ThermoPack suites).</summary>
            public static IReadOnlyList<string> All => new[]
            {
                ElectrolyteNRTL, ExtendedUNIQUAC, ReaktoroAqueous, Glycol, HCl,
                KentEisenberg, SourWater,
                MBWR19, MBWR32, NISTMEOS, PatelTeja, PCPSAFT, PRCPA,
                SAFTVRMie, SAFTVRQMie, SchmidtWensel, SPCSAFT, SRKCPA,
                CarbonCapture, CO2Transport, CO2Storage
            };
        }

        /// <summary>True when <paramref name="name"/> matches a Plus / DWSIMPlus property package
        /// and therefore requires <see cref="License.Activate"/> to have been called first.</summary>
        public static bool RequiresPlus(string name)
        {
            foreach (var n in Plus.All)
                if (string.Equals(n, name, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
