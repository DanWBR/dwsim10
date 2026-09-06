using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>VLE regression - Peng-Robinson, a five-component natural gas at cold-separator conditions
    /// (240 K, 40 bar). The heavy ends condense; the vapour is enriched in methane and the liquid in the
    /// C4/C5 fraction. Pins the vapour fraction, the phase split (conservation) and the key K-value ordering
    /// so any change in the vapour-liquid flash or the PR fugacities is caught.</summary>
    internal static class T20_PR_NaturalGas_VLE5Comp
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T20_PR_NG5")
                .WithCompounds("Methane", "Ethane", "Propane", "N-butane", "N-pentane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var s = fs.AddMaterialStream("feed")
                .At(240.0.Kelvin(), 40e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 0.75)
                .SetCompoundMolarFlow("Ethane", 0.10)
                .SetCompoundMolarFlow("Propane", 0.07)
                .SetCompoundMolarFlow("N-butane", 0.05)
                .SetCompoundMolarFlow("N-pentane", 0.03);

            fs.Solve();

            var V = s.Object.Phases[2];
            var L = s.Object.Phases[3];
            double VF = V.Properties.molarfraction.GetValueOrDefault();
            double LF = L.Properties.molarfraction.GetValueOrDefault();
            double yC1 = V.Compounds["Methane"].MoleFraction.GetValueOrDefault();
            double xC5 = L.Compounds["N-pentane"].MoleFraction.GetValueOrDefault();
            double yC5 = V.Compounds["N-pentane"].MoleFraction.GetValueOrDefault();

            new ResultTable("PR natural gas 5-comp VLE @ 240 K, 40 bar")
                .Row("Phase fractions sum to 1", 1.0, VF + LF, 0.002, "-")
                .RowInRange("Vapour fraction", 0.71, 0.75, VF, "-")
                .RowInRange("Vapour is methane-rich (y_C1)", 0.89, 0.91, yC1, "-")
                .RowInRange("Liquid holds the C5 (x_C5)", 0.10, 0.12, xC5, "-")
                .RowInRange("C5 K-value well below 1 (heavy stays liquid)", 0.0, 0.05, yC5 / xC5, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
