using DWSIM.Automation.FluentAPI;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - Water and 1-butanol at 25 C: the aqueous liquid holds about 1.9 mol % butanol and the organic liquid about 51 mol % water (IUPAC-NIST solubility data). With the NRTL parameters of the database the organic liquid holds about 60 mol % water; the ranges hold the model's split around the data.</summary>
    internal static class T27_NRTL_WaterButanol_LLE
    {
        public static void Run() => LiquidLiquidSplitCase.Run("NRTL water/1-butanol LLE @ 298 K, 2 bar", PropertyPackages.NRTL,
            new[] { "Water", "1-butanol" }, new[] { 0.75, 0.25 },
            298.15, 200000, 0.3, 0.975, 0.998, 0.45, 0.65);
    }
}
