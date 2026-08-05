using System.Collections.Generic;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Estimation
{
    public interface IPropertyEstimator
    {
        string Name { get; }
        IReadOnlyList<PropertyCategory> Provides { get; }
        IReadOnlyList<PropertyCategory> Requires { get; }
        EstimationResult Estimate(CompoundInputs inputs);
    }

    public sealed class EstimationResult
    {
        /// Produced scalar values keyed by short property code ("Tc", "Pc", "omega", ...).
        public Dictionary<string, double> Values { get; } = new Dictionary<string, double>();

        /// Unit string per produced value, e.g. "K", "Pa", "-".
        public Dictionary<string, string> Units { get; } = new Dictionary<string, string>();

        /// Optional fit parameters per equation (e.g. ("Antoine", [A,B,C])).
        public Dictionary<string, double[]> Fits { get; } = new Dictionary<string, double[]>();

        public bool HasAny => Values.Count > 0 || Fits.Count > 0;
    }
}
