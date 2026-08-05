namespace DWSIM.PhaseEquilibriumData.Core.Consistency
{
    /// <summary>
    /// Supplies saturation pressure (in kPa) for a single compound at a given temperature.
    /// Consumers inject one provider per binary component.
    /// </summary>
    public interface ISaturationPressureProvider
    {
        double GetPsatKPa(double temperatureK);
    }
}
