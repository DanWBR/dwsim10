using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Ethanol distillery: fermentation, degassing and distillation.
    /// Mash (10 % glucose) → heater (308 K) → conversion reactor (Gay-Lussac,
    /// C6H12O6 → 2 C2H5OH + 2 CO2, 95 %) → degasser heater (358 K) + drum (strips the
    /// dissolved CO2 so the column's total condenser never sees it) → distillation
    /// column (25 stages, NRTL) → hydrous ethanol distillate + stillage bottoms.
    /// Checks: fermentation balances, distillate below the 89.4 mol% azeotrope,
    /// stillage nearly ethanol-free.</summary>
    internal static class EthanolDistillerySample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("EthanolDistillery")
                .WithCompounds("Water", "Ethanol", "Glucose", "Carbon dioxide")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var rxn = fs.DefineConversionReaction("R_Ferm",
                new System.Collections.Generic.Dictionary<string, double>
                {
                    { "Glucose", -1 }, { "Ethanol", 2 }, { "Carbon dioxide", 2 }
                },
                "Glucose", "Mixture", "100");
            fs.ReactionSet("FermSet").Add(rxn);

            var mash = fs.AddMaterialStream("mash")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 9.0)
                .SetCompoundMassFlow("Glucose", 1.0)
                .SetCompoundMassFlow("Ethanol", 0.0)
                .SetCompoundMassFlow("Carbon dioxide", 0.0);

            var warmMash = fs.AddMaterialStream("warm mash");
            fs.AddHeater("H-1")
                .WithOutletTemperature(308.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(mash, 0)
                .ConnectProduct(warmMash, 0);

            var fermGas = fs.AddMaterialStream("fermenter gas");
            var wine = fs.AddMaterialStream("wine");
            var qFerm = fs.AddEnergyStream("Q fermenter");
            fs.AddConversionReactor("R-1")
                .Isothermal()
                .WithReactionSet("FermSet")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(warmMash, 0)
                .ConnectProduct(fermGas, 0)
                .ConnectProduct(wine, 1)
                .ConnectEnergyFeed(qFerm, 1);

            // Degasser: near-boiling flash so the CO2 dissolved in the wine leaves here
            // instead of accumulating in the column's total condenser.
            var hotWine = fs.AddMaterialStream("hot wine");
            fs.AddHeater("H-2")
                .WithOutletTemperature(358.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(wine, 0)
                .ConnectProduct(hotWine, 0);

            var degasVent = fs.AddMaterialStream("degasser vent");
            var degassedWine = fs.AddMaterialStream("degassed wine");
            fs.AddSeparator("V-1")
                .ConnectFeed(hotWine, 0)
                .ConnectProduct(degasVent, 0)
                .ConnectProduct(degassedWine, 1);

            // Preheat to just past the bubble point (~370.5 K) and flash once more: the
            // second drum vents the last of the CO2 (any non-condensable reaching the
            // total condenser breaks the column) and hands the column a feed at exact
            // saturation, which the bubble-point solver needs on a stream this dilute.
            var nearBoilWine = fs.AddMaterialStream("near-boiling wine");
            fs.AddHeater("H-3")
                .WithOutletTemperature(370.8.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(degassedWine, 0)
                .ConnectProduct(nearBoilWine, 0);

            var flashVapor = fs.AddMaterialStream("flash vapor");
            var columnFeed = fs.AddMaterialStream("column feed");
            fs.AddSeparator("V-2")
                .ConnectFeed(nearBoilWine, 0)
                .ConnectProduct(flashVapor, 0)
                .ConnectProduct(columnFeed, 1);

            // Solve the fermentation train, then size the column's bottoms spec from the
            // ethanol that actually reaches it: hydrous ethanol at ~70 mol% stays below
            // the 89.4 mol% azeotrope, which no simple column can cross.
            fs.Solve();

            double nEtOH = columnFeed.Object.Phases[0].Compounds["Ethanol"].MolarFlow.GetValueOrDefault();
            double F_col = columnFeed.MolarFlowMolPerSecond;
            double D_spec = nEtOH / 0.80;
            double B_spec = F_col - D_spec;

            var ethanol = fs.AddMaterialStream("hydrous ethanol");
            var stillage = fs.AddMaterialStream("stillage");
            var condDuty = fs.AddEnergyStream("cond duty");
            var rebDuty = fs.AddEnergyStream("reb duty");

            fs.AddDistillationColumn("T-1")
                .WithNumberOfStages(25)
                .WithFeed(columnFeed, 12)
                .WithDistillate(ethanol)
                .WithBottoms(stillage)
                .WithCondenserDuty(condDuty)
                .WithReboilerDuty(rebDuty)
                .WithCondenserSpec("Reflux Ratio", 12.0, "")
                .WithReboilerSpec("Product Molar Flow Rate", B_spec, "mol/s")
                .WithTopPressure(101325.0.Pascal())
                .WithColumnPressureDrop(0.0.Pascal())
                .Configure(c => c.MaxIterations = 500);

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            double xD = ethanol.OverallMoleFraction("Ethanol");
            double xB = stillage.OverallMoleFraction("Ethanol");

            new ResultTable("Ethanol distillery (fermentation + distillation, NRTL)")
                .Row("Fermenter balance F = gas + wine", mash.MassFlowKgPerSecond,
                     fermGas.MassFlowKgPerSecond + wine.MassFlowKgPerSecond, 0.005, "kg/s")
                .RowInRange("CO2 leaves with the fermenter gas (>50 %)", 0.5, 1.0,
                     fermGas.OverallMassFraction("Carbon dioxide"), "-")
                .RowInRange("Wine ethanol 3-8 wt%", 0.03, 0.08, wine.OverallMassFraction("Ethanol"), "-")
                .RowInRange("Residual glucose in wine < 2 wt%", 0.0, 0.02, wine.OverallMassFraction("Glucose"), "-")
                .Row("Column balance F = D + B", columnFeed.MolarFlowMolPerSecond,
                     ethanol.MolarFlowMolPerSecond + stillage.MolarFlowMolPerSecond, 0.005, "mol/s")
                .RowInRange("Distillate 60-89.4 mol% EtOH (below azeotrope)", 0.60, 0.894, xD, "-")
                .RowInRange("Stillage < 1 mol% EtOH", 0.0, 0.01, xB, "-")
                .RowInRange("Reboiler duty > 0", 0.0, 1e9, rebDuty.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "ethanol-distillery");
        }
    }
}
