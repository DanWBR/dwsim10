using System.Collections.Generic;

namespace DWSIM.PhaseEquilibriumData.Core
{
    public sealed record CompoundQuery(
        IReadOnlyList<string> CasNumbers,
        EquilibriumType? TypeFilter,
        (double Min, double Max)? TemperatureRangeK,
        (double Min, double Max)? PressureRangeKPa,
        int MaxResults);
}
