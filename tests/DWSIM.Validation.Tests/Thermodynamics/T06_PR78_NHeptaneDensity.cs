using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>PR78 - n-heptane liquid density at 298.15 K, 1 atm.
    /// Reference (NIST): ρ_liq ≈ 679.5 kg/m³. PR78 adjusts α for heavy components.</summary>
    internal static class T06_PR78_NHeptaneDensity
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T06_PR78_NC7_rho")
                .WithCompound("N-heptane")
                .WithPropertyPackage(PropertyPackages.PengRobinson1978);

            var s = fs.AddMaterialStream("liq")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("N-heptane", 1.0);

            fs.Solve();

            double rho = s.Object.Phases[0].Properties.density.GetValueOrDefault();

            new ResultTable("PR78 - liquid n-heptane @ 25 °C, 1 atm")
                .Row("ρ_liq (NIST 679.5)", 679.5, rho, 0.05, "kg/m³")
                .PrintAndThrowIfFailed();
        }
    }
}
