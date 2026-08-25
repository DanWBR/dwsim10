using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Grayson-Streed - propane/n-butane mixture @ 300 K, 5 bar.
    /// Psat propane @ 300 K ≈ 9.9 bar; Psat n-butane ≈ 2.6 bar. Equimolar @ 5 bar should give 2 phases.</summary>
    internal static class T15_GraysonStreed_HydrocarbonMix
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T15_GS_C3C4")
                .WithCompounds("Propane", "N-butane")
                .WithPropertyPackage(PropertyPackages.GraysonStreed);

            var s = fs.AddMaterialStream("feed")
                .At(300.0.Kelvin(), 5e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Propane", 0.5)
                .SetCompoundMolarFlow("N-butane", 0.5);

            fs.Solve();

            double VF = s.Object.Phases[2].Properties.molarfraction.GetValueOrDefault();
            double xC4 = s.Object.Phases[1].Compounds["N-butane"].MoleFraction.GetValueOrDefault();
            double yC3 = s.Object.Phases[2].Compounds["Propane"].MoleFraction.GetValueOrDefault();

            new ResultTable("Grayson-Streed - C3/C4 50/50 @ 300 K, 5 bar")
                .RowInRange("Vapor frac in (0,1) - two phases", 0.01, 0.99, VF, "-")
                .RowInRange("x_C4 > 0.5 (heavy liquid)", 0.5, 1.0, xC4, "-")
                .RowInRange("y_C3 > 0.5 (light vapor)", 0.5, 1.0, yC3, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
