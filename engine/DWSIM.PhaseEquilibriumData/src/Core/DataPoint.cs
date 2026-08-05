using System.Collections.Generic;

namespace DWSIM.PhaseEquilibriumData.Core
{
    public sealed record DataPoint(
        IReadOnlyDictionary<string, double> Values,
        IReadOnlyDictionary<string, double> Uncertainties);
}
