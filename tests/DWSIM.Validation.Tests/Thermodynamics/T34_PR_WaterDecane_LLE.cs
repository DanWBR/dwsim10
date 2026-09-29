using DWSIM.Automation.FluentAPI;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - Water and n-decane with Peng-Robinson at 100 C and 10 bar (all liquid): two nearly pure liquids, the equation-of-state path of the liquid-liquid flash.</summary>
    internal static class T34_PR_WaterDecane_LLE
    {
        public static void Run() => LiquidLiquidSplitCase.Run("PR water/n-decane LLE @ 373 K, 10 bar", PropertyPackages.PengRobinson,
            new[] { "Water", "N-decane" }, new[] { 0.5, 0.5 },
            373.15, 1000000, 0.4, 0.99, 1.0, 0.0, 0.05);
    }
}
