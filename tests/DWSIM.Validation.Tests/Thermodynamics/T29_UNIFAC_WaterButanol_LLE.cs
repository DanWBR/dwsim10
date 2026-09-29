using DWSIM.Automation.FluentAPI;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - The water/1-butanol split of T27 predicted by UNIFAC; wider ranges for a predictive model.</summary>
    internal static class T29_UNIFAC_WaterButanol_LLE
    {
        public static void Run() => LiquidLiquidSplitCase.Run("UNIFAC water/1-butanol LLE @ 298 K, 2 bar", PropertyPackages.UNIFAC,
            new[] { "Water", "1-butanol" }, new[] { 0.75, 0.25 },
            298.15, 200000, 0.2, 0.96, 0.995, 0.35, 0.65);
    }
}
