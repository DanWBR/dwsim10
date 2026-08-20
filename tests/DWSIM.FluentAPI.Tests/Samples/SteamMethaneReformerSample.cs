using System;
using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Steam methane reforming hydrogen plant (single train, Gibbs reactors).
    /// CH4 + steam (S/C = 3) → mixer → furnace preheat (1123 K) → Gibbs reformer (15 bar) →
    /// cooler (623 K) → Gibbs shift (methane excluded, so it is inert) → cooler (313 K) →
    /// knockout drum → H2-rich syngas.
    /// Checks: CH4 conversion, CO drops across the shift, carbon atom balance,
    /// dry-basis H2 fraction, endothermic reformer duty.</summary>
    internal static class SteamMethaneReformerSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("SteamMethaneReformer")
                .WithCompounds("Methane", "Water", "Carbon monoxide", "Carbon dioxide", "Hydrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var ng = fs.AddMaterialStream("natural gas")
                .At(298.15.Kelvin(), 15e5.Pascal())
                .WithMolarFlow(25.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 25.0)
                .SetCompoundMolarFlow("Water", 0.0)
                .SetCompoundMolarFlow("Carbon monoxide", 0.0)
                .SetCompoundMolarFlow("Carbon dioxide", 0.0)
                .SetCompoundMolarFlow("Hydrogen", 0.0);

            var steam = fs.AddMaterialStream("process steam")
                .At(453.15.Kelvin(), 15e5.Pascal())
                .WithMolarFlow(75.0.MolPerSecond())
                .SetCompoundMolarFlow("Water", 75.0)
                .SetCompoundMolarFlow("Methane", 0.0)
                .SetCompoundMolarFlow("Carbon monoxide", 0.0)
                .SetCompoundMolarFlow("Carbon dioxide", 0.0)
                .SetCompoundMolarFlow("Hydrogen", 0.0);

            var mixed = fs.AddMaterialStream("mixed feed");
            fs.AddMixer("MIX-1")
                .ConnectFeed(ng, 0)
                .ConnectFeed(steam, 1)
                .ConnectProduct(mixed, 0);

            // Furnace preheat to the reformer temperature.
            var hotFeed = fs.AddMaterialStream("reformer feed");
            fs.AddHeater("FUR-1")
                .WithOutletTemperature(1123.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(mixed, 0)
                .ConnectProduct(hotFeed, 0);

            // Gibbs reformer: CH4 + H2O ⇌ CO + 3H2, CO + H2O ⇌ CO2 + H2 at equilibrium.
            var refGas = fs.AddMaterialStream("reformed gas");
            var refLiq = fs.AddMaterialStream("reformer liquid");
            var qRef = fs.AddEnergyStream("Q reformer");
            var reformer = fs.AddGibbsReactor("R-REF")
                .Isothermal()
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(hotFeed, 0)
                .ConnectProduct(refGas, 0)
                .ConnectProduct(refLiq, 1)
                .ConnectEnergyFeed(qRef, 1);
            // CreateElementMatrix reads the connected inlet, so it runs after ConnectFeed.
            reformer.Object.ComponentIDs = new List<string>
                { "Methane", "Water", "Carbon monoxide", "Carbon dioxide", "Hydrogen" };
            reformer.Object.CreateElementMatrix();
            reformer.Object.InitializeFromPreviousSolution = false;

            // Cool to high-temperature-shift inlet.
            var shiftFeed = fs.AddMaterialStream("shift feed");
            fs.AddCooler("CL-1")
                .WithOutletTemperature(623.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(refGas, 0)
                .ConnectProduct(shiftFeed, 0);

            // Shift converter: methane stays out of ComponentIDs, so the Gibbs solver
            // treats it as inert and no re-methanation happens at 623 K.
            var shiftGas = fs.AddMaterialStream("shifted gas");
            var shiftLiq = fs.AddMaterialStream("shift liquid");
            var qShift = fs.AddEnergyStream("Q shift");
            var shift = fs.AddGibbsReactor("R-SHIFT")
                .Isothermal()
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(shiftFeed, 0)
                .ConnectProduct(shiftGas, 0)
                .ConnectProduct(shiftLiq, 1)
                .ConnectEnergyFeed(qShift, 1);
            shift.Object.ComponentIDs = new List<string>
                { "Water", "Carbon monoxide", "Carbon dioxide", "Hydrogen" };
            shift.Object.CreateElementMatrix();
            shift.Object.InitializeFromPreviousSolution = false;

            // Cool and knock out the process condensate.
            var coldGas = fs.AddMaterialStream("cooled syngas");
            fs.AddCooler("CL-2")
                .WithOutletTemperature(313.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(shiftGas, 0)
                .ConnectProduct(coldGas, 0);

            var syngas = fs.AddMaterialStream("H2-rich syngas");
            var condensate = fs.AddMaterialStream("process condensate");
            fs.AddSeparator("KO-1")
                .ConnectFeed(coldGas, 0)
                .ConnectProduct(syngas, 0)
                .ConnectProduct(condensate, 1);

            fs.Solve();

            double Sum(string c, MaterialStreamBuilder a, MaterialStreamBuilder b) =>
                a.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault()
                + b.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();

            double nCH4_out = Sum("Methane", refGas, refLiq);
            double convCH4 = (25.0 - nCH4_out) / 25.0;

            double nCO_ref = Sum("Carbon monoxide", refGas, refLiq);
            double nCO_shift = Sum("Carbon monoxide", shiftGas, shiftLiq);

            // Carbon atoms across the whole train: all 25 mol/s of feed carbon must
            // leave the shift as CH4 + CO + CO2.
            double Cout = Sum("Methane", shiftGas, shiftLiq)
                + nCO_shift
                + Sum("Carbon dioxide", shiftGas, shiftLiq);

            double yH2_dry = syngas.OverallMoleFraction("Hydrogen")
                / Math.Max(1.0 - syngas.OverallMoleFraction("Water"), 1e-9);

            new ResultTable("Steam methane reforming (Gibbs reformer + shift)")
                .RowInRange("CH4 conversion > 60 %", 0.60, 1.0, convCH4, "-")
                .RowInRange("Shift consumes CO", 0.0, 0.999, nCO_shift / Math.Max(nCO_ref, 1e-9), "-")
                .Row("Carbon atom balance across the train", 25.0, Cout, 0.005, "mol/s")
                .RowInRange("Reformer duty endothermic (> 0)", 0.001, 1e6, qRef.EnergyFlowKW, "kW")
                .RowInRange("Dry-basis H2 in syngas > 60 %", 0.60, 1.0, yH2_dry, "-")
                .RowInRange("Condensate mostly water", 0.95, 1.0, condensate.OverallMoleFraction("Water"), "-")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "steam-methane-reforming-h2");
        }
    }
}
