using System.Collections.Generic;

namespace DWSIM.PureCompoundData.Builder
{
    /// POCO that mirrors the shape of DWSIM's <c>BaseClasses.ConstantProperties</c>
    /// without taking a dependency on DWSIM.Thermodynamics. The DWSIM UI layer maps
    /// this onto a real <c>ConstantProperties</c> before inserting into a simulation.
    public sealed class BuiltConstantProperties
    {
        // Identification
        public string CasNumber { get; set; } = "";
        public string Name { get; set; } = "";
        public string? IupacName { get; set; }
        public string? Smiles { get; set; }
        public string? InChIKey { get; set; }
        public string? Formula { get; set; }
        public double? MolecularWeight { get; set; }

        // Critical constants (SI)
        public double? CriticalTemperature { get; set; }  // K
        public double? CriticalPressure { get; set; }     // Pa
        public double? CriticalVolume { get; set; }       // m3/mol
        public double? CriticalCompressibility { get; set; }
        public double? AcentricFactor { get; set; }

        // Other point constants
        public double? NormalBoilingPoint { get; set; }         // K
        public double? NormalMeltingPoint { get; set; }         // K
        public double? IgEnthalpyOfFormation25C { get; set; }   // J/mol
        public double? IgGibbsEnergyOfFormation25C { get; set; }// J/mol
        public double? EnthalpyOfFusion { get; set; }           // J/mol at Tm

        /// DWSIM vapor-pressure equation number (see PropertyMethodsFunctions);
        /// 101 = DIPPR 101, 0 = Antoine in mmHg/°C, etc.
        public int? VaporPressureEquation { get; set; }
        public double[]? VaporPressureCoefficients { get; set; }  // [A, B, C, D, E]
        public double? VaporPressureTMin { get; set; }
        public double? VaporPressureTMax { get; set; }

        public int? IdealGasCpEquation { get; set; }
        public double[]? IdealGasCpCoefficients { get; set; }

        public int? LiquidDensityEquation { get; set; }
        public double[]? LiquidDensityCoefficients { get; set; }

        public int? HeatOfVaporizationEquation { get; set; }
        public double[]? HeatOfVaporizationCoefficients { get; set; }

        /// Group counts from SMILES fragmentation, keyed by ugropy group name.
        /// Consumers map these names to DWSIM subgroup IDs via the DWSIM asset tables.
        public Dictionary<string, int> UnifacGroups { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> DortmundGroups { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> JobackGroups { get; } = new Dictionary<string, int>();

        /// Per-field provenance: field name → {source, estimator, citation}.
        public Dictionary<string, FieldProvenance> Provenance { get; } =
            new Dictionary<string, FieldProvenance>();
    }

    public sealed class FieldProvenance
    {
        public string Kind { get; set; } = "";   // "source" | "estimator" | "fit"
        public string Label { get; set; } = "";  // source provider, estimator name, fit equation
        public string? Doi { get; set; }
        public string? Method { get; set; }
    }
}
