using System.Collections.Generic;

namespace DWSIM.PureCompoundData.Core
{
    /// One observation of one property of one compound, from one source.
    /// Either <see cref="ScalarValue"/> is set (for point constants like Tc, Pc, omega)
    /// or <see cref="Points"/> is non-empty (for T-dependent curves), possibly both
    /// with <see cref="Fits"/> (source-provided DIPPR / Antoine coefficients).
    public sealed record PureCompoundRecord(
        string Id,
        Compound Compound,
        PropertyCategory Category,
        string Property,
        double? ScalarValue,
        IReadOnlyList<PropertyPoint>? Points,
        string Unit,
        double? TMin,
        double? TMax,
        IReadOnlyList<PropertyFit>? Fits,
        MeasurementMethod Method,
        Citation Citation,
        string SourceProvider);
}
