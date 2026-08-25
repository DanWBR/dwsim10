using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>Mini ethanol plant - simplified fermentation with pre- and post-heating.
    /// Train: feed (10 % glucose mash) → Heater (35 °C) → ConvReactor (Gay-Lussac, 95 %) →
    ///        Cooler (25 °C). Distillation column omitted (dissolved non-condensable CO2 breaks the
    ///        balance in the DistillationColumn shortcut) - we validate the fermentative part of the train.
    /// Reaction: C6H12O6 → 2 C2H6O + 2 CO2 with 95 % stoichiometric conversion.
    /// Validates: global mass balance, CO2 leaves through the gas phase, ethanol appears in the liquid phase.</summary>
    internal static class F02_EthanolPlant
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F02_Ethanol")
                .WithCompounds("Water", "Ethanol", "Glucose", "Carbon dioxide")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var rxn = fs.DefineConversionReaction("R_Ferm",
                new System.Collections.Generic.Dictionary<string, double>
                {
                    { "Glucose", -1 }, { "Ethanol", 2 }, { "Carbon dioxide", 2 }
                },
                "Glucose", "Mixture", "95");
            fs.ReactionSet("FermSet").Add(rxn);

            var feed = fs.AddMaterialStream("mash")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 9.0)
                .SetCompoundMassFlow("Glucose", 1.0)
                .SetCompoundMassFlow("Ethanol", 0.0)
                .SetCompoundMassFlow("Carbon dioxide", 0.0);

            var heated = fs.AddMaterialStream("heated");
            fs.AddHeater("H-1")
                .WithOutletTemperature(308.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(heated, 0);

            var brothLiq = fs.AddMaterialStream("broth_liq");
            var brothGas = fs.AddMaterialStream("broth_gas");
            var rxn_heat = fs.AddEnergyStream("Q_rxn");
            fs.AddConversionReactor("R-1")
                .Isothermal()
                .WithReactionSet("FermSet")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(heated, 0)
                .ConnectProduct(brothGas, 0)   // port 0 = vapor (ConversionReactor convention)
                .ConnectProduct(brothLiq, 1)   // port 1 = liquid
                .ConnectEnergyFeed(rxn_heat, 1);

            var brothCold = fs.AddMaterialStream("broth_cold");
            var qCool = fs.AddEnergyStream("Q_cool");
            fs.AddCooler("C-1")
                .WithOutletTemperature(298.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(brothLiq, 0)
                .ConnectProduct(brothCold, 0)
                .ConnectEnergyFeed(qCool, 1);

            fs.Solve();

            double mFeed = feed.MassFlowKgPerSecond;
            double mLiq = brothLiq.MassFlowKgPerSecond;
            double mGas = brothGas.MassFlowKgPerSecond;
            double mCold = brothCold.MassFlowKgPerSecond;

            double yEtOH = brothCold.OverallMassFraction("Ethanol");
            double yGlu = brothCold.OverallMassFraction("Glucose");
            double yCO2_gas = brothGas.OverallMassFraction("Carbon dioxide");

            new ResultTable("F02 - Mini ethanol plant (Gay-Lussac fermentation 95 %)")
                .Row("Reactor balance F = liq + gas", mFeed, mLiq + mGas, 0.005, "kg/s")
                .Row("Post-cooler preserves mass", mLiq, mCold, 0.001, "kg/s")
                .RowInRange("CO2 leaves with gas phase (>50 %)", 0.5, 1.0, yCO2_gas, "-")
                .RowInRange("Ethanol in final broth (3-7 %)", 0.03, 0.08, yEtOH, "-")
                .RowInRange("Low residual glucose (<2 %)", 0.0, 0.02, yGlu, "-")
                .Row("Post-cooler T_out = 25 °C", 298.15, brothCold.TemperatureK, 0.001, "K")
                .PrintAndThrowIfFailed();
        }
    }
}
