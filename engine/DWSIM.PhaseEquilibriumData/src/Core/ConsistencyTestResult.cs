namespace DWSIM.PhaseEquilibriumData.Core
{
    public sealed record ConsistencyTestResult(
        string TestName,
        bool Passed,
        double Statistic,
        double? Threshold,
        string? Comment);
}
