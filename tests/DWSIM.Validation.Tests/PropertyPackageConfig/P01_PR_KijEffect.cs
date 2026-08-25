using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.PropertyPackageConfig
{
    /// <summary>WithPropertyPackage(...).ConfigurePR(WithKij) - sets kij for CO2/N2 and verifies that
    /// the flash result changes relative to the default kij. Expected behavior: setting kij=0.0 vs 0.4
    /// alters the vapor-phase density of a supercritical mixture.</summary>
    internal static class P01_PR_KijEffect
    {
        private static double DensityWithKij(double kij)
        {
            var fs = Flowsheet.Create($"P01_kij_{kij}")
                .WithCompounds("Carbon dioxide", "Nitrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson, pp => pp
                    .ConfigurePR(pr => pr.WithKij("Carbon dioxide", "Nitrogen", kij)));

            var s = fs.AddMaterialStream("g")
                .At(300.0.Kelvin(), 100e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Carbon dioxide", 0.5)
                .SetCompoundMolarFlow("Nitrogen", 0.5);

            fs.Solve();
            return s.Object.Phases[0].Properties.density.GetValueOrDefault();
        }

        public static void Run()
        {
            double rho0 = DensityWithKij(0.0);
            double rhoHigh = DensityWithKij(0.4);

            new ResultTable("PR - kij effect on CO2/N2 (300 K, 100 bar)")
                .RowInRange("ρ with kij=0 finite", 1.0, 1000.0, rho0, "kg/m³")
                .RowInRange("ρ with kij=0.4 finite", 1.0, 1000.0, rhoHigh, "kg/m³")
                .RowInRange("Difference |ρ(kij=0.4) - ρ(kij=0)| > 0.5 kg/m³", 0.5, 1e6,
                    System.Math.Abs(rhoHigh - rho0), "kg/m³")
                .PrintAndThrowIfFailed();
        }
    }
}
