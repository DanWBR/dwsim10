using System.Collections.Generic;

namespace DWSIM.PureCompoundData.Estimation
{
    /// Known compound constants (from sources or previously-run estimators) fed into
    /// each <see cref="IPropertyEstimator"/>. Missing values are left null.
    public sealed class CompoundInputs
    {
        public string? Smiles { get; set; }
        public double? MolecularWeight { get; set; }

        public double? Tc { get; set; }       // K
        public double? Pc { get; set; }       // Pa
        public double? Vc { get; set; }       // m3/mol
        public double? Zc { get; set; }       // -
        public double? Tb { get; set; }       // K
        public double? Tm { get; set; }       // K
        public double? Acentric { get; set; } // omega, -

        public double? HformIG { get; set; }  // J/mol @ 298.15 K, ideal gas
        public double? GformIG { get; set; }  // J/mol @ 298.15 K, ideal gas

        /// Joback group counts keyed by group symbol ("CH3", "CH2", "OH (alcohol)", ...).
        public Dictionary<string, int> JobackGroups { get; } = new Dictionary<string, int>();

        /// Optional (T, Psat) series in K, Pa - used by Antoine / DIPPR101 fitters.
        public List<(double T, double Psat)> PsatPoints { get; } = new List<(double, double)>();
    }
}
