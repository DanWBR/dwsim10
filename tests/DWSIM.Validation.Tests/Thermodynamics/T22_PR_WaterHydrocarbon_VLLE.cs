using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>VLLE regression - Peng-Robinson, a water + hydrocarbon mixture (water/methane/n-hexane/
    /// n-octane) at 350 K, 3 bar splits into three phases: a vapour, an almost pure water (aqueous) liquid,
    /// and a hydrocarbon (organic) liquid, because water and the hydrocarbons are nearly immiscible. Pins the
    /// three-phase split and the near-immiscibility so any change in the vapour-liquid-liquid flash or its
    /// phase detection is caught.</summary>
    internal static class T22_PR_WaterHydrocarbon_VLLE
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T22_PR_W_HC_VLLE")
                .WithCompounds("Water", "Methane", "N-hexane", "N-octane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var s = fs.AddMaterialStream("feed")
                .At(350.0.Kelvin(), 3e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Water", 0.40)
                .SetCompoundMolarFlow("Methane", 0.20)
                .SetCompoundMolarFlow("N-hexane", 0.20)
                .SetCompoundMolarFlow("N-octane", 0.20);

            fs.Solve();

            var V = s.Object.Phases[2];
            var L1 = s.Object.Phases[3];
            var L2 = s.Object.Phases[4];
            double VF = V.Properties.molarfraction.GetValueOrDefault();
            double L1F = L1.Properties.molarfraction.GetValueOrDefault();
            double L2F = L2.Properties.molarfraction.GetValueOrDefault();

            // Identify which liquid is aqueous by its water content.
            double w1 = L1.Compounds["Water"].MoleFraction.GetValueOrDefault();
            double w2 = L2.Compounds["Water"].MoleFraction.GetValueOrDefault();
            double aqWater = Math.Max(w1, w2);
            double orgWater = Math.Min(w1, w2);

            new ResultTable("PR water/methane/n-hexane/n-octane VLLE @ 350 K, 3 bar")
                .Row("Phase fractions sum to 1", 1.0, VF + L1F + L2F, 0.005, "-")
                .RowInRange("A vapour phase is present", 0.28, 0.34, VF, "-")
                .RowInRange("Two liquid phases are present (smaller > 1%)", 0.28, 0.38, Math.Min(L1F, L2F), "-")
                .RowInRange("The aqueous liquid is nearly pure water", 0.99, 1.0, aqWater, "-")
                .RowInRange("The organic liquid rejects water", 0.0, 0.01, orgWater, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
