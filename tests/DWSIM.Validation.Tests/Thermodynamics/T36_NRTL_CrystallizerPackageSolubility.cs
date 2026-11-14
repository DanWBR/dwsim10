using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.Validation.Tests.Framework;
using CrystallizerMode = DWSIM.UnitOperations.UnitOperations.CrystallizerMode;
using TPP = DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Crystallizer with the solubility taken from the property package: naphthalene from toluene, cooled
    /// from 340 K to 280 K. The saturation satisfies the solid-liquid equilibrium expression, the crystals agree with
    /// the solid the flash with solids forms from the same feed, the duty is the enthalpy difference to that slurry
    /// (heat of crystallization included), the Crystals outlet is a solid phase, and the solubility at 298.15 K is
    /// close to the ideal one, as measured for this pair (mole fraction about 0.29).</summary>
    internal static class T36_NRTL_CrystallizerPackageSolubility
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T36_Crystallizer")
                .WithCompounds("Naphthalene", "Toluene")
                .WithPropertyPackage(PropertyPackages.NRTL,
                    p => p.WithFlashSetting(FlashSetting.HandleSolidsInDefaultEqCalcMode, true));

            const double P = 101325.0, R = 8.31446, Tfeed = 340.0, Top = 280.0;

            var feed = fs.AddMaterialStream("feed").At(Tfeed.Kelvin(), P.Pascal()).WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Naphthalene", 0.4).SetCompoundMolarFlow("Toluene", 0.6);
            // the same feed brought to the operating temperature by the flash with solids of the package
            var slurry = fs.AddMaterialStream("slurry").At(Top.Kelvin(), P.Pascal()).WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Naphthalene", 0.4).SetCompoundMolarFlow("Toluene", 0.6);
            var crystals = fs.AddMaterialStream("crystals");
            var liquor = fs.AddMaterialStream("liquor");

            var cr = fs.AddCrystallizer("CR-1")
                .WithMode(CrystallizerMode.Cooling)
                .WithSoluteCompound("Naphthalene")
                .WithSolubilityFromPropertyPackage()
                .WithOperatingTemperature(Top.Kelvin())
                .ConnectFeed(feed, 0).ConnectProduct(crystals, 0).ConnectProduct(liquor, 1);
            fs.Solve();

            var o = cr.Object;
            var cp = feed.Object.Phases[0].Compounds["Naphthalene"].ConstantProperties;
            Func<double, double> aSat = T => Math.Exp(-cp.EnthalpyOfFusionAtTf * 1000.0 / R / T * (1.0 - T / cp.TemperatureOfFusion));

            double xg = o.Result_SaturationMoleFraction * o.Result_ActivityCoefficient;
            double solidFlash = slurry.Object.Phases[7].Compounds["Naphthalene"].MolarFlow.GetValueOrDefault();
            double crystalMoles = crystals.Object.Phases[0].Compounds["Naphthalene"].MolarFlow.GetValueOrDefault();
            double dutyFlash = (slurry.Object.Phases[0].Properties.enthalpy.GetValueOrDefault() * slurry.MassFlowKgPerSecond
                              - feed.Object.Phases[0].Properties.enthalpy.GetValueOrDefault() * feed.MassFlowKgPerSecond);
            double yield280 = o.Result_Yield, sup280 = o.Result_Supersaturation, tsat = o.Result_SaturationT_K, duty280 = o.Result_Duty_kW;
            double solidFraction = crystals.Object.Phases[7].Properties.molarfraction.GetValueOrDefault();
            double massOut = crystals.MassFlowKgPerSecond + liquor.MassFlowKgPerSecond;

            // at the saturation temperature of the feed the crystallizer is at the edge of forming crystals
            o.OperatingT_K = tsat + 0.5;
            fs.Solve();
            double yieldAboveTsat = o.Result_Yield;
            o.OperatingT_K = tsat - 0.5;
            fs.Solve();
            double yieldBelowTsat = o.Result_Yield;

            // solubility at 25 C against the measurements of this pair
            o.OperatingT_K = 298.15;
            fs.Solve();
            double x298 = o.Result_SaturationMoleFraction;

            new ResultTable("Crystallizer with the solubility from the property package - naphthalene / toluene (NRTL)")
                .Row("x.gamma of the solute at 280 K equals the saturation activity", aSat(Top), xg, 0.001, "-")
                .Row("Crystals at 280 K against the flash with solids of the same feed", solidFlash, crystalMoles, 0.01, "mol/s")
                .Row("Duty at 280 K against the enthalpy change of the feed to that slurry", dutyFlash, duty280, 0.01, "kW")
                .Row("Crystals outlet is a solid phase", 1.0, solidFraction, 0.001, "-")
                .Row("Mass balance at 280 K", feed.MassFlowKgPerSecond, massOut, 0.0001, "kg/s")
                .RowInRange("Yield at 280 K", 0.5, 0.95, yield280, "-")
                .RowInRange("Supersaturation ratio of the feed at 280 K is above one", 1.0001, 100.0, sup280, "-")
                .RowInRange("Saturation temperature of the feed between 280 K and 340 K", Top, Tfeed, tsat, "K")
                .RowInRange("No crystals 0.5 K above the saturation temperature", -1e-9, 1e-9, yieldAboveTsat, "-")
                .RowInRange("Crystals 0.5 K below the saturation temperature", 1e-6, 0.1, yieldBelowTsat, "-")
                .RowInRange("Solubility at 298.15 K, mole fraction (measured about 0.29)", 0.26, 0.32, x298, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
