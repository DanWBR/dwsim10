using System.Collections.Generic;

namespace DWSIM.PhaseEquilibriumData.Core
{
    public sealed record PhaseEquilibriumDataset(
        string Id,
        EquilibriumType EquilibriumType,
        IReadOnlyList<Compound> Compounds,
        IReadOnlyList<Constraint> Constraints,
        IReadOnlyList<string> VariableNames,
        IReadOnlyList<DataPoint> Points,
        MeasurementMethod Method,
        Citation Citation,
        string SourceProvider,
        IReadOnlyList<ConsistencyTestResult> ConsistencyTests);
}
