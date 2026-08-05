namespace DWSIM.PureCompoundData.Core
{
    /// A pre-fitted correlation (coefficients + T-range + error metric).
    /// <see cref="DwsimEquationNumber"/> maps to DWSIM's internal equation IDs
    /// used by e.g. VaporPressureEquation / IdealgasCpEquation.
    public sealed record PropertyFit(
        string EquationName,
        int DwsimEquationNumber,
        double[] Coefficients,
        double TMin,
        double TMax,
        double? AARD);
}
