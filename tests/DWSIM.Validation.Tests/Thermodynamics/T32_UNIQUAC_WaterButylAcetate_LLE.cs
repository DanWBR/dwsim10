using DWSIM.Automation.FluentAPI;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - Water and n-butyl acetate at 25 C: about 0.1 mol % ester in the aqueous liquid and 11 mol % water in the organic liquid.</summary>
    internal static class T32_UNIQUAC_WaterButylAcetate_LLE
    {
        public static void Run() => LiquidLiquidSplitCase.Run("UNIQUAC water/n-butyl acetate LLE @ 298 K, 2 bar", PropertyPackages.UNIQUAC,
            new[] { "Water", "N-butyl acetate" }, new[] { 0.5, 0.5 },
            298.15, 200000, 0.3, 0.995, 1.0, 0.03, 0.2);
    }
}
