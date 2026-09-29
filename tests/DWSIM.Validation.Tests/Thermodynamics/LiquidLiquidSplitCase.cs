using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>
    /// Shared body of the liquid-liquid split cases: a water + organic feed at a temperature and pressure
    /// where it is all liquid, solved through the flowsheet, checked for no vapour, two liquids that both
    /// hold a sizeable share of the feed, and the water content of the aqueous and the organic liquid
    /// against the mutual solubility ranges of the literature. Water is the first compound of the feed.
    /// </summary>
    internal static class LiquidLiquidSplitCase
    {
        public static void Run(string title, string package, string[] compounds, double[] molarFlows,
            double temperatureK, double pressurePa, double minPhase,
            double aqueousWaterLow, double aqueousWaterHigh, double organicWaterLow, double organicWaterHigh)
        {
            var fs = Flowsheet.Create(title.Replace(' ', '_'))
                .WithCompounds(compounds)
                .WithPropertyPackage(package);

            var s = fs.AddMaterialStream("feed")
                .At(temperatureK.Kelvin(), pressurePa.Pascal())
                .WithMolarFlow(molarFlows.Sum().MolPerSecond());
            for (int i = 0; i < compounds.Length; i++) s = s.SetCompoundMolarFlow(compounds[i], molarFlows[i]);

            fs.Solve();

            var V = s.Object.Phases[2];
            var L1 = s.Object.Phases[3];
            var L2 = s.Object.Phases[4];
            double VF = V.Properties.molarfraction.GetValueOrDefault();
            double L1F = L1.Properties.molarfraction.GetValueOrDefault();
            double L2F = L2.Properties.molarfraction.GetValueOrDefault();

            double w1 = L1.Compounds[compounds[0]].MoleFraction.GetValueOrDefault();
            double w2 = L2.Compounds[compounds[0]].MoleFraction.GetValueOrDefault();

            new ResultTable(title)
                .Row("No vapour phase", 0.0, VF, 0.001, "-")
                .Row("Two liquid phases sum to 1", 1.0, L1F + L2F, 0.005, "-")
                .RowInRange("Both liquid phases present", minPhase, 1.0 - minPhase, Math.Min(L1F, L2F), "-")
                .RowInRange("Water in the aqueous liquid", aqueousWaterLow, aqueousWaterHigh, Math.Max(w1, w2), "-")
                .RowInRange("Water in the organic liquid", organicWaterLow, organicWaterHigh, Math.Min(w1, w2), "-")
                .PrintAndThrowIfFailed();
        }
    }
}
