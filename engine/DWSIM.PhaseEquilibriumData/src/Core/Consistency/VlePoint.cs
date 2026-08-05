namespace DWSIM.PhaseEquilibriumData.Core.Consistency
{
    /// <summary>
    /// A single binary VLE measurement (T, P, x1, y1). Compositions are mole fractions;
    /// component 2 values are implied (1 - x1, 1 - y1).
    /// </summary>
    public sealed record VlePoint(double TemperatureK, double PressureKPa, double X1, double Y1);
}
