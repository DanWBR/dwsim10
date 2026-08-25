using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Chao-Seader - propane/n-butane flash @ 320 K, 8 bar.
    /// Psat C3@320K ≈ 16 bar, Psat n-C4@320K ≈ 4.4 bar - equimolar @ 8 bar should give 2 phases.</summary>
    internal static class T18_ChaoSeader_HydrocarbonFlash
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T18_CS_C3C4")
                .WithCompounds("Propane", "N-butane")
                .WithPropertyPackage(PropertyPackages.ChaoSeader);

            var s = fs.AddMaterialStream("feed")
                .At(320.0.Kelvin(), 8e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Propane", 0.5)
                .SetCompoundMolarFlow("N-butane", 0.5);

            fs.Solve();

            double VF = s.Object.Phases[2].Properties.molarfraction.GetValueOrDefault();
            double yC3 = s.Object.Phases[2].Compounds["Propane"].MoleFraction.GetValueOrDefault();

            new ResultTable("Chao-Seader - C3/n-C4 50/50 @ 320 K, 8 bar")
                .RowInRange("Vapor frac in (0,1)", 0.01, 0.99, VF, "-")
                .RowInRange("y_C3 > 0.5 (more volatile)", 0.5, 1.0, yC3, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
