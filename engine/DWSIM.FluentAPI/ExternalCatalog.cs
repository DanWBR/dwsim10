using System;
using System.Collections.Generic;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Canonical display-name constants for every <see cref="DWSIM.Interfaces.IExternalUnitOperation"/>
    /// (bioprocess, refining, electrolyte and other Plus components) registered through
    /// <c>IFlowsheet.AvailableSimulationObjects</c>. Names match each UO's
    /// <c>GetDisplayName()</c> exactly and round-trip with
    /// <see cref="Flowsheet.AvailableExternalUnitOperationNames"/>.
    /// </summary>
    /// <remarks>
    /// Use these constants with <see cref="Flowsheet.AddExternalUnitOperation"/> or with
    /// the typed <c>AddX</c> methods on <see cref="Flowsheet"/>.
    /// <see cref="RequiresPlus"/> answers whether a name needs <see cref="License.Activate"/>.
    /// </remarks>
    public static class ExternalCatalog
    {
        /// <summary>Bioprocess unit operations (free, in <c>DWSIM.UnitOperations.dll</c>).</summary>
        public static class Bioprocess
        {
            /// <summary>Anaerobic digester (BlackBox / ADM1-Lite / ADM1 full).</summary>
            public const string AnaerobicDigester = "Anaerobic Digester";
            /// <summary>BioReactor (Monod / Moser / Teissier kinetics; batch / fed-batch / continuous).</summary>
            public const string BioReactor = "BioReactor";
            /// <summary>Circulating fluidized bed fast-pyrolysis reactor (1-D PFR with sand circulation).</summary>
            public const string CFBFastPyrolysis = "CFB Fast Pyrolysis";
            /// <summary>Pretreatment reactor (dilute acid / steam-explosion / alkaline / organosolv).</summary>
            public const string PretreatmentReactor = "Pretreatment Reactor";
            /// <summary>Biogas upgrader (water-scrubbing / amine / PSA / membrane).</summary>
            public const string BiogasUpgrader = "Biogas Upgrader";
            /// <summary>Cell-lysis unit (homogenizer / bead mill / chemical / enzymatic / osmotic / ultrasonic).</summary>
            public const string CellLysis = "Cell Lysis";
            /// <summary>Liquid-solid centrifuge (disk-stack / decanter / tubular).</summary>
            public const string Centrifuge = "Centrifuge";
            /// <summary>Bind-elute / flow-through / dynamic Thomas-model chromatography column.</summary>
            public const string ChromatographyColumn = "Chromatography Column";
            /// <summary>Crossflow ultrafiltration / diafiltration with optional Hermia fouling.</summary>
            public const string CrossflowUFDF = "Crossflow UF/DF";
            /// <summary>Cooling / evaporative / antisolvent crystallizer with polynomial solubility.</summary>
            public const string Crystallizer = "Crystallizer";

            /// <summary>Every bioprocess display name as a flat list.</summary>
            public static IReadOnlyList<string> All => new[]
            {
                AnaerobicDigester, BioReactor, CFBFastPyrolysis, PretreatmentReactor,
                BiogasUpgrader, CellLysis, Centrifuge, ChromatographyColumn,
                CrossflowUFDF, Crystallizer
            };
        }

        /// <summary>Refining shortcut models (Plus, in <c>DistPackages\Windows_Plus\unitops</c>).</summary>
        public static class Refining
        {
            /// <summary>HF / sulphuric-acid alkylation shortcut (i-C4 + olefin → alkylate).</summary>
            public const string Alkylation = "Shortcut Alkylation";
            /// <summary>Amine treater shortcut for H2S / CO2 absorption with HC slip.</summary>
            public const string AmineTreater = "Shortcut Amine Treater";
            /// <summary>Stream blender (2-4 inlets, min/avg pressure rule).</summary>
            public const string Blender = "Shortcut Blender";
            /// <summary>Claus sulphur recovery unit (3-stage thermal + catalytic).</summary>
            public const string ClausSRU = "Shortcut Claus SRU";
            /// <summary>Delayed coker shortcut with CCR-based coke yield and PNA-aware product slate.</summary>
            public const string Coker = "Shortcut Coker";
            /// <summary>Fluid catalytic cracker - slate or Weekman-kinetic mode.</summary>
            public const string FCC = "Shortcut FCC";
            /// <summary>Hydrocracker (HCR) with conversion + S/N removal targets.</summary>
            public const string Hydrocracker = "Shortcut Hydrocracker";
            /// <summary>Hydrodesulfurization with HDN + mercaptan removal options.</summary>
            public const string HDS = "Shortcut HDS";
            /// <summary>C5/C6 isomerization shortcut producing isomerate of given RON.</summary>
            public const string Isomerization = "Shortcut Isomerization";
            /// <summary>Catalytic reformer with severity-RON sensitivity and PNA adjustment.</summary>
            public const string Reformer = "Shortcut Reformer";
            /// <summary>Crude distillation unit shortcut - TBP-cut splitter with logistic overlap.</summary>
            public const string CDU = "Shortcut CDU";

            /// <summary>Every refining display name as a flat list.</summary>
            public static IReadOnlyList<string> All => new[]
            {
                Alkylation, AmineTreater, Blender, ClausSRU, Coker, FCC,
                Hydrocracker, HDS, Isomerization, Reformer, CDU
            };
        }

        /// <summary>Electrolyte / aqueous-chemistry unit operations (Plus).</summary>
        public static class Electrolyte
        {
            /// <summary>Ion-exchange unit (cation / anion, equilibrium or staged-countercurrent).</summary>
            public const string IonExchangeUnit = "Ion Exchange Unit";
            /// <summary>Multi-inlet neutralization reactor (electrolyte mixing + thermal balance).</summary>
            public const string NeutralizationReactor = "Neutralization Reactor";
            /// <summary>Precipitation reactor - solid formation by Ksp with Davies / Debye-Hückel activity.</summary>
            public const string PrecipitationReactor = "Precipitation Reactor";
            /// <summary>Reverse-osmosis / nanofiltration / ultrafiltration membrane unit.</summary>
            public const string ReverseOsmosisUnit = "Reverse Osmosis Unit";

            /// <summary>Every electrolyte display name as a flat list.</summary>
            public static IReadOnlyList<string> All => new[]
            {
                IonExchangeUnit, NeutralizationReactor, PrecipitationReactor, ReverseOsmosisUnit
            };
        }

        /// <summary>Other Plus components (advanced HX, fired heater, networking, energy-stream ops, etc.).</summary>
        public static class Plus
        {
            /// <summary>Shell-and-tube heat exchanger with rating / design / simulation modes (Bell-Delaware).</summary>
            public const string AdvancedHeatExchanger = "Advanced Heat Exchanger";
            /// <summary>Fired-heater (radiant + convection sections, draft + emissions models).</summary>
            public const string FiredHeater = "Fired Heater";
            /// <summary>Pipe-network solver (Simplex / Nelder-Mead) over connected Pipe / Pump / Valve / Node blocks.</summary>
            public const string PipeNetwork = "Pipe Network Unit Operation";
            /// <summary>Multi-stage vapor-compression chiller (1-3 stages, economizers, equipment sizing).</summary>
            public const string VaporCompressionChiller = "Vapor Compression Chiller";
            /// <summary>Zeolite molecular-sieve adsorber (PSA or equilibrium).</summary>
            public const string ZeoliteAdsorber = "Zeolite Adsorber";
            /// <summary>Copper-bed mercury removal (capacity-based or Wheeler-Jonas).</summary>
            public const string CopperBedHgAdsorber = "Copper Bed Hg Adsorber";
            /// <summary>Detailed air-cooler with fan curves and global weather hookup.</summary>
            public const string AirCooler2 = "Air Cooler 2";
            /// <summary>Energy-stream mixer (sum or selectable inputs).</summary>
            public const string EnergyMixer = "Energy Mixer";
            /// <summary>Energy-stream splitter (split-ratio or fixed flow per output).</summary>
            public const string EnergySplitter = "Energy Splitter";
            /// <summary>Energy-stream switch - routes by an evaluated boolean expression.</summary>
            public const string EnergyStreamSwitch = "Energy Stream Switch";
            /// <summary>Material-stream switch - routes by an evaluated boolean expression.</summary>
            public const string MaterialStreamSwitch = "Material Stream Switch";
            /// <summary>Material-stream mapper / overrider (compounds, T, P, flow with custom units).</summary>
            public const string MaterialStreamMapper = "Material Stream Mapper";
            /// <summary>Falling-film evaporator with stage-wise vapour-fraction profile.</summary>
            public const string FallingFilmEvaporator = "Falling Film Evaporator";
            /// <summary>Thermodynamic property editor - overrides PP interaction parameters within a simulation.</summary>
            public const string ThermoPropertyEditor = "Thermo Property Editor";

            /// <summary>Every advanced-Plus display name as a flat list.</summary>
            public static IReadOnlyList<string> All => new[]
            {
                AdvancedHeatExchanger, FiredHeater, PipeNetwork,
                VaporCompressionChiller, ZeoliteAdsorber, CopperBedHgAdsorber,
                AirCooler2, EnergyMixer, EnergySplitter, EnergyStreamSwitch,
                MaterialStreamSwitch, MaterialStreamMapper, FallingFilmEvaporator,
                ThermoPropertyEditor
            };
        }

        /// <summary>Free miscellaneous UOs (in <c>DWSIM.UnitOperations.dll</c>).</summary>
        public static class Misc
        {
            /// <summary>Pressure-relief valve sizing / rating.</summary>
            public const string ReliefValve = "Relief Valve";

            /// <summary>Every misc free display name as a flat list.</summary>
            public static IReadOnlyList<string> All => new[] { ReliefValve };
        }

        /// <summary>
        /// True when <paramref name="displayName"/> matches a Plus / DWSIMPlus component
        /// (refining, electrolyte ops, advanced HX, fired heater, ExtensionPack, etc.)
        /// and therefore requires an active patron key. Used by
        /// <see cref="Flowsheet.AddExternalUnitOperation"/> and every typed Plus
        /// <c>AddX</c> method to decide whether to call <see cref="License.RequirePlus"/>.
        /// </summary>
        public static bool RequiresPlus(string displayName)
        {
            foreach (var n in Refining.All)
                if (string.Equals(n, displayName, StringComparison.Ordinal)) return true;
            foreach (var n in Electrolyte.All)
                if (string.Equals(n, displayName, StringComparison.Ordinal)) return true;
            foreach (var n in Plus.All)
                if (string.Equals(n, displayName, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
