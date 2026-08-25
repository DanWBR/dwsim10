using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Cross-check PR vs SRK - typical natural gas mixture @ 250 K, 50 bar.
    /// Molar composition: 90 % CH4 + 7 % C2H6 + 3 % C3H8.
    /// Expected: both cubic EoS give densities agreeing within 5 % in this regime.</summary>
    internal static class T17_PR_vs_SRK_NaturalGas
    {
        private static double DensityWith(string pp)
        {
            var fs = Flowsheet.Create("X_" + pp.GetHashCode())
                .WithCompounds("Methane", "Ethane", "Propane")
                .WithPropertyPackage(pp);
            var s = fs.AddMaterialStream("g")
                .At(250.0.Kelvin(), 50e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 0.90)
                .SetCompoundMolarFlow("Ethane", 0.07)
                .SetCompoundMolarFlow("Propane", 0.03);
            fs.Solve();
            return s.Object.Phases[0].Properties.density.GetValueOrDefault();
        }

        public static void Run()
        {
            double rhoPR = DensityWith(PropertyPackages.PengRobinson);
            double rhoSRK = DensityWith(PropertyPackages.SoaveRedlichKwong);

            new ResultTable("PR vs SRK - natural gas (90/7/3) @ 250 K, 50 bar")
                .Row("ρ_SRK ≈ ρ_PR (± 5 %)", rhoPR, rhoSRK, 0.05, "kg/m³")
                .RowInRange("ρ_PR > 0", 1.0, 1000.0, rhoPR, "kg/m³")
                .PrintAndThrowIfFailed();
        }
    }
}
