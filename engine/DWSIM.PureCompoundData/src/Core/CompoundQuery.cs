using System.Collections.Generic;

namespace DWSIM.PureCompoundData.Core
{
    /// Inputs for an online search. At least one identifier should be set.
    public sealed record CompoundQuery(
        string? CasNumber = null,
        string? InChIKey = null,
        string? Name = null,
        IReadOnlyList<PropertyCategory>? Categories = null,
        int Take = 100);
}
