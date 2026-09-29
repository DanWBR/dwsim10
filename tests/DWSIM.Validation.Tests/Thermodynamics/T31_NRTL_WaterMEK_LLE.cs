using DWSIM.Automation.FluentAPI;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - Water and methyl ethyl ketone at 25 C: about 8 mol % ketone in the aqueous liquid and 35 mol % water in the organic liquid.</summary>
    internal static class T31_NRTL_WaterMEK_LLE
    {
        public static void Run() => LiquidLiquidSplitCase.Run("NRTL water/methyl ethyl ketone LLE @ 298 K, 2 bar", PropertyPackages.NRTL,
            new[] { "Water", "Methyl ethyl ketone" }, new[] { 0.6, 0.4 },
            298.15, 200000, 0.2, 0.88, 0.95, 0.25, 0.45);
    }
}
