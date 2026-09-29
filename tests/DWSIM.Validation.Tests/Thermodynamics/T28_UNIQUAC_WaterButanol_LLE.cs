using DWSIM.Automation.FluentAPI;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - The water/1-butanol split of T27 with UNIQUAC. With the UNIQUAC parameters of the database the organic liquid holds about 38 mol % water.</summary>
    internal static class T28_UNIQUAC_WaterButanol_LLE
    {
        public static void Run() => LiquidLiquidSplitCase.Run("UNIQUAC water/1-butanol LLE @ 298 K, 2 bar", PropertyPackages.UNIQUAC,
            new[] { "Water", "1-butanol" }, new[] { 0.75, 0.25 },
            298.15, 200000, 0.3, 0.975, 0.995, 0.33, 0.56);
    }
}
