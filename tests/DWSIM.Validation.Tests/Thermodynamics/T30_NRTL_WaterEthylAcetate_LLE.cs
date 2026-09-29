using DWSIM.Automation.FluentAPI;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - Water and ethyl acetate at 25 C: about 1.7 mol % ester in the aqueous liquid and 14 mol % water in the organic liquid.</summary>
    internal static class T30_NRTL_WaterEthylAcetate_LLE
    {
        public static void Run() => LiquidLiquidSplitCase.Run("NRTL water/ethyl acetate LLE @ 298 K, 2 bar", PropertyPackages.NRTL,
            new[] { "Water", "Ethyl acetate" }, new[] { 0.5, 0.5 },
            298.15, 200000, 0.3, 0.975, 0.995, 0.08, 0.22);
    }
}
