using System.Collections.Generic;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Sources.ThermoML
{
    /// <summary>Lightweight AST produced by the parser and consumed by the classifier.</summary>
    public sealed class ThermoMLFile
    {
        public string? Doi { get; set; }
        public string? Title { get; set; }
        public string? Journal { get; set; }
        public int? Year { get; set; }
        public int? Volume { get; set; }
        public string? Pages { get; set; }
        public List<string> Authors { get; } = new List<string>();
        public Dictionary<int, Compound> Compounds { get; } = new Dictionary<int, Compound>();
        public List<ThermoMLPureOrMixture> PureOrMixtureData { get; } = new List<ThermoMLPureOrMixture>();
    }

    public sealed class ThermoMLPureOrMixture
    {
        public int Index { get; set; }
        public List<string> PhaseIds { get; } = new List<string>();
        public List<Constraint> Constraints { get; } = new List<Constraint>();
        public List<string> VariableNames { get; } = new List<string>();
        public List<string> PropertyNames { get; } = new List<string>();
        public List<DataPoint> Points { get; } = new List<DataPoint>();
        public List<int> ComponentOrder { get; } = new List<int>();
        public MeasurementMethod Method { get; set; } = MeasurementMethod.Unknown;
        public bool HasAzeotropMarker { get; set; }
        public bool CompositionIsMassFraction { get; set; }
    }
}
