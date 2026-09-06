using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>VLLE regression - Peng-Robinson, water / n-pentane / n-octane at 370 K, 2 bar. The pentane is
    /// volatile enough to raise a vapour at this pressure, while water and the hydrocarbons stay nearly
    /// immiscible, so three phases coexist: a vapour, an aqueous liquid and an organic liquid. A second,
    /// three-component water + hydrocarbon check on the vapour-liquid-liquid flash at a different temperature
    /// and pressure than T22.</summary>
    internal static class T23_PR_WaterPentaneOctane_VLLE
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T23_PR_WPO_VLLE")
                .WithCompounds("Water", "N-pentane", "N-octane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var s = fs.AddMaterialStream("feed")
                .At(370.0.Kelvin(), 2e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Water", 0.50)
                .SetCompoundMolarFlow("N-pentane", 0.25)
                .SetCompoundMolarFlow("N-octane", 0.25);

            fs.Solve();

            var V = s.Object.Phases[2];
            var L1 = s.Object.Phases[3];
            var L2 = s.Object.Phases[4];
            double VF = V.Properties.molarfraction.GetValueOrDefault();
            double L1F = L1.Properties.molarfraction.GetValueOrDefault();
            double L2F = L2.Properties.molarfraction.GetValueOrDefault();

            double w1 = L1.Compounds["Water"].MoleFraction.GetValueOrDefault();
            double w2 = L2.Compounds["Water"].MoleFraction.GetValueOrDefault();
            double aqWater = Math.Max(w1, w2);
            double orgWater = Math.Min(w1, w2);

            new ResultTable("PR water/n-pentane/n-octane VLLE @ 370 K, 2 bar")
                .Row("Phase fractions sum to 1", 1.0, VF + L1F + L2F, 0.005, "-")
                .RowInRange("A vapour phase is present", 0.55, 0.63, VF, "-")
                .RowInRange("Two liquid phases are present (smaller > 1%)", 0.13, 0.20, Math.Min(L1F, L2F), "-")
                .RowInRange("The aqueous liquid is nearly pure water", 0.99, 1.0, aqWater, "-")
                .RowInRange("The organic liquid rejects water", 0.0, 0.02, orgWater, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
