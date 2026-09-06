using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>LLE regression - UNIQUAC, the water / toluene / acetone extraction ternary at 298 K, 2 bar
    /// (pressurized to keep it all liquid). Water and toluene are essentially immiscible, so the mixture
    /// splits into an aqueous and an organic liquid, and acetone partitions between them. Pins the two-liquid
    /// split (no vapour) and the near-immiscibility of the water/toluene pair so a change in the UNIQUAC
    /// liquid-liquid flash is caught.</summary>
    internal static class T25_UNIQUAC_WaterTolueneAcetone_LLE
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T25_UNIQUAC_WTA_LLE")
                .WithCompounds("Water", "Toluene", "Acetone")
                .WithPropertyPackage(PropertyPackages.UNIQUAC);

            var s = fs.AddMaterialStream("feed")
                .At(298.15.Kelvin(), 2e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Water", 0.50)
                .SetCompoundMolarFlow("Toluene", 0.40)
                .SetCompoundMolarFlow("Acetone", 0.10);

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

            new ResultTable("UNIQUAC water/toluene/acetone LLE @ 298 K, 2 bar")
                .Row("No vapour phase", 0.0, VF, 0.001, "-")
                .Row("Two liquid phases sum to 1", 1.0, L1F + L2F, 0.005, "-")
                .RowInRange("Both liquid phases present", 0.42, 0.55, Math.Min(L1F, L2F), "-")
                .RowInRange("Aqueous phase is water-rich", 0.94, 1.0, aqWater, "-")
                .RowInRange("Organic phase rejects water", 0.0, 0.03, orgWater, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
