using System.Collections.Generic;

namespace DWSIM.PhaseEquilibriumData.Core
{
    public sealed record Citation(
        string? Doi,
        string? Title,
        IReadOnlyList<string> Authors,
        string? Journal,
        int? Year,
        int? Volume,
        string? Pages);
}
