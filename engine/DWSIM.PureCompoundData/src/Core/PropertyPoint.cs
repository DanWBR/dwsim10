namespace DWSIM.PureCompoundData.Core
{
    /// A single (T, optional P) → value sample of a temperature-dependent property.
    /// Units are carried on the parent <see cref="PureCompoundRecord"/>.
    public sealed record PropertyPoint(
        double T,
        double? P,
        double Value,
        double? Uncertainty,
        string? Phase);
}
