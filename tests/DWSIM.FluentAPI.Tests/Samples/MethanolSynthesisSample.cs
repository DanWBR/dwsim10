using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Methanol synthesis from syngas, single pass with product distillation.
    /// Syngas (CO/CO2/H2) → compressor (50 bar) → heater (523 K) → Gibbs reactor →
    /// cooler (313 K) → flash separator → valve (1 atm) → distillation column (20 stages) →
    /// methanol distillate + water bottoms.
    /// Checks: CO conversion, carbon atom balance, separator balance, MeOH purity.</summary>
    internal static class MethanolSynthesisSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("MethanolSynthesis")
                .WithCompounds("Carbon monoxide", "Carbon dioxide", "Hydrogen", "Methanol", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var syngas = fs.AddMaterialStream("syngas")
                .At(300.0.Kelvin(), 10.0e5.Pascal())
                .WithMolarFlow(55.0.MolPerSecond())
                .SetCompoundMolarFlow("Carbon monoxide", 10.0)
                .SetCompoundMolarFlow("Carbon dioxide", 5.0)
                .SetCompoundMolarFlow("Hydrogen", 40.0)
                .SetCompoundMolarFlow("Methanol", 0.0)
                .SetCompoundMolarFlow("Water", 0.0);

            var compOut = fs.AddMaterialStream("compressed syngas");
            var wComp = fs.AddEnergyStream("W comp");
            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(50.0e5.Pascal())
                .WithAdiabaticEfficiencyPercent(80.0)
                .ConnectFeed(syngas, 0)
                .ConnectProduct(compOut, 0)
                .ConnectEnergyFeed(wComp, 1);

            var heated = fs.AddMaterialStream("reactor feed");
            fs.AddHeater("H-1")
                .WithOutletTemperature(523.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(compOut, 0)
                .ConnectProduct(heated, 0);

            var rxOut = fs.AddMaterialStream("reactor out");
            var rxLiq = fs.AddMaterialStream("reactor liquid");
            var qRx = fs.AddEnergyStream("Q reactor");
            var gibbs = fs.AddGibbsReactor("R-1")
                .Isothermal()
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(heated, 0)
                .ConnectProduct(rxOut, 0)
                .ConnectProduct(rxLiq, 1)
                .ConnectEnergyFeed(qRx, 1);

            gibbs.Object.ComponentIDs = new List<string>
                { "Carbon monoxide", "Carbon dioxide", "Hydrogen", "Methanol", "Water" };
            gibbs.Object.CreateElementMatrix();
            gibbs.Object.InitializeFromPreviousSolution = false;

            var cooled = fs.AddMaterialStream("cooled effluent");
            fs.AddCooler("CL-1")
                .WithOutletTemperature(313.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(rxOut, 0)
                .ConnectProduct(cooled, 0);

            var flashGas = fs.AddMaterialStream("flash gas");
            var crudeMeOH = fs.AddMaterialStream("crude methanol");
            fs.AddSeparator("V-1")
                .ConnectFeed(cooled, 0)
                .ConnectProduct(flashGas, 0)
                .ConnectProduct(crudeMeOH, 1);

            var depressurized = fs.AddMaterialStream("depressurized crude");
            fs.AddValve("VLV-1")
                .WithOutletPressure(101325.0.Pascal())
                .ConnectFeed(crudeMeOH, 0)
                .ConnectProduct(depressurized, 0);

            // Degasser: warms the crude to 330 K so the CO2/H2 that stayed dissolved
            // at 50 bar leaves through the vent and the column's total condenser
            // sees no non-condensables.
            var warmCrude = fs.AddMaterialStream("warm crude");
            fs.AddHeater("H-2")
                .WithOutletTemperature(330.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(depressurized, 0)
                .ConnectProduct(warmCrude, 0);

            var ventGas = fs.AddMaterialStream("vent gas");
            var columnFeed = fs.AddMaterialStream("column feed");
            fs.AddSeparator("V-2")
                .ConnectFeed(warmCrude, 0)
                .ConnectProduct(ventGas, 0)
                .ConnectProduct(columnFeed, 1);

            // Solve the synthesis train first, then size the column's bottoms spec from
            // what actually reaches it: a flow spec converges where a temperature spec
            // hunts, and the value stays stored in the saved file as a plain constant.
            fs.Solve();

            // The crude is already >90 % MeOH; the column strips the water out the bottom.
            // Aim the bottoms at ~75 % water so the distillate lands at ~99 % MeOH.
            double nH2O_feed = columnFeed.Object.Phases[0].Compounds["Water"].MolarFlow.GetValueOrDefault();
            double B_spec = nH2O_feed / 0.75;

            var meohDist = fs.AddMaterialStream("methanol product");
            var waterBot = fs.AddMaterialStream("water bottoms");
            var condDuty = fs.AddEnergyStream("cond duty");
            var rebDuty = fs.AddEnergyStream("reb duty");

            fs.AddDistillationColumn("T-1")
                .WithNumberOfStages(20)
                .WithFeed(columnFeed, 10)
                .WithDistillate(meohDist)
                .WithBottoms(waterBot)
                .WithCondenserDuty(condDuty)
                .WithReboilerDuty(rebDuty)
                .WithCondenserSpec("Reflux Ratio", 2.0, "")
                .WithReboilerSpec("Product Molar Flow Rate", B_spec, "mol/s")
                .WithTopPressure(101325.0.Pascal())
                .WithColumnPressureDrop(0.0.Pascal());

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            double Sum(string c, MaterialStreamBuilder s) =>
                s.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();

            double nCO_out = Sum("Carbon monoxide", rxOut) + Sum("Carbon monoxide", rxLiq);
            double convCO = (10.0 - nCO_out) / 10.0;

            double Cin = 10.0 + 5.0;
            double Cout = nCO_out
                + Sum("Carbon dioxide", rxOut) + Sum("Carbon dioxide", rxLiq)
                + Sum("Methanol", rxOut) + Sum("Methanol", rxLiq);

            var rt = new ResultTable("Methanol synthesis from syngas (Gibbs, 50 bar / 523 K)")
                .RowInRange("CO conversion 20-99 %", 0.20, 0.99, convCO, "-")
                .Row("Carbon atom balance across the reactor", Cin, Cout, 0.01, "mol/s")
                .Row("Flash separator balance", cooled.MassFlowKgPerSecond,
                     flashGas.MassFlowKgPerSecond + crudeMeOH.MassFlowKgPerSecond, 0.005, "kg/s")
                .RowInRange("Compressor work > 0", 0.001, 1e6, wComp.EnergyFlowKW, "kW")
                .RowInRange("Distillate MeOH > 95 %", 0.95, 1.0, meohDist.OverallMoleFraction("Methanol"), "-")
                .RowInRange("Bottoms enriched in water", 0.5, 1.0, waterBot.OverallMoleFraction("Water"), "-");

            rt.PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "methanol-synthesis-syngas");
        }
    }
}
