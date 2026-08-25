using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using CrystallizerMode = DWSIM.UnitOperations.UnitOperations.CrystallizerMode;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>Cooling crystallizer - supersaturated glucose solution @ 80 °C cooled to 5 °C.
    /// C_sat(T) = a + b·(T-273.15). At 5 °C: C_sat ≈ 0.40 + 0.005·5 = 0.425 g/g.
    /// Feed = 1 kg/s containing 0.6 kg/s glucose + 0.4 kg/s water → fraction exceeding C_sat crystallizes.</summary>
    internal static class B07_Crystallizer_Cooling
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B07_Cryst")
                .WithCompounds("Water", "Glucose")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(353.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.4)
                .SetCompoundMassFlow("Glucose", 0.6);

            var crystals = fs.AddMaterialStream("crystals");
            var liquor = fs.AddMaterialStream("liquor");

            var cr = fs.AddCrystallizer("CR-1")
                .Configure(o => o.CreateConnectors())
                .WithMode(CrystallizerMode.Cooling)
                .WithSoluteCompound("Glucose")
                .WithSolventCompound("Water")
                .WithOperatingTemperature(278.15.Kelvin())
                .WithSolubilityCoefficients(0.40, 0.005, 0.0)
                .WithEvaporationFraction(0.0)
                .WithMeanCrystalSizeMicrons(250.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(crystals, 0)
                .ConnectProduct(liquor, 1);

            cr.Object.Calculate();

            new ResultTable("Cooling Crystallizer - Glucose/H2O 0.6/0.4 kg/s, 80→5 °C")
                .Row("Solute in feed", 0.6, cr.Object.Result_SoluteInFeed_kgs, 0.001, "kg/s")
                .RowInRange("Csat(5 °C) between 0.2 and 0.5 g/g", 0.2, 0.5, cr.Object.Result_Csat_gg, "g/g")
                .RowInRange("Yield > 50 % (wide cooling)", 0.5, 1.0, cr.Object.Result_Yield, "-")
                .RowInRange("Crystals > 0", 1e-6, 1.0, cr.Object.Result_Cryst_kgs, "kg/s")
                .Row("Mass balance", 1.0, crystals.MassFlowKgPerSecond + liquor.MassFlowKgPerSecond, 0.001, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
