using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Validation.Tests.Framework;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F15 - Methanol synthesis from syngas (single-pass, no recycle).
    /// Syngas (CO + CO2 + H2) → Compressor (50 bar) → Heater (523 K) →
    /// GibbsReactor (isothermal) → Cooler (313 K) → Separator → crude MeOH →
    /// DistillationColumn → purified methanol distillate + water bottoms.
    /// Validates: CO conversion, C atomic balance, MeOH purity, compressor work.</summary>
    internal static class F15_MethanolSynthesis
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F15_MeOH")
                .WithCompounds("Carbon monoxide", "Carbon dioxide", "Hydrogen", "Methanol", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // Syngas feed: CO 1, CO2 0.5, H2 4 mol/s
            var syngas = fs.AddMaterialStream("syngas")
                .At(300.0.Kelvin(), 10.0e5.Pascal())
                .WithMolarFlow(5.5.MolPerSecond())
                .SetCompoundMolarFlow("Carbon monoxide", 1.0)
                .SetCompoundMolarFlow("Carbon dioxide", 0.5)
                .SetCompoundMolarFlow("Hydrogen", 4.0)
                // A new stream starts with its compounds split evenly, so leaving the two products
                // unset fed 1.1 mol/s of methanol and of water in with the syngas. The carbon
                // entering the reactor was then 2.6 mol/s, not the 1.5 counted below.
                .SetCompoundMolarFlow("Methanol", 0.0)
                .SetCompoundMolarFlow("Water", 0.0);

            // Compressor to 50 bar
            var compOut = fs.AddMaterialStream("comp_out");
            var wComp = fs.AddEnergyStream("W_comp");
            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(50.0e5.Pascal())
                .WithAdiabaticEfficiencyPercent(80.0)
                .ConnectFeed(syngas, 0)
                .ConnectProduct(compOut, 0)
                .ConnectEnergyFeed(wComp, 1);

            // Heater to reactor inlet temperature
            var heated = fs.AddMaterialStream("heated");
            fs.AddHeater("H-1")
                .WithOutletTemperature(523.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(compOut, 0)
                .ConnectProduct(heated, 0);

            // Gibbs reactor - isothermal methanol synthesis
            var rxOut = fs.AddMaterialStream("rx_out");
            var rxLiq = fs.AddMaterialStream("rx_liq");
            var qRx = fs.AddEnergyStream("Q_rx");
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

            // Cooler to condense methanol
            var cooled = fs.AddMaterialStream("cooled");
            fs.AddCooler("CL-1")
                .WithOutletTemperature(313.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(rxOut, 0)
                .ConnectProduct(cooled, 0);

            // Flash separator
            var flashGas = fs.AddMaterialStream("flash_gas");
            var crudeMeOH = fs.AddMaterialStream("crude_meoh");
            fs.AddSeparator("V-1")
                .ConnectFeed(cooled, 0)
                .ConnectProduct(flashGas, 0)
                .ConnectProduct(crudeMeOH, 1);

            // Depressurize crude MeOH from reactor pressure to ~1 atm
            var depressurized = fs.AddMaterialStream("depressurized");
            fs.AddValve("VLV-1")
                .WithOutletPressure(101325.0.Pascal())
                .ConnectFeed(crudeMeOH, 0)
                .ConnectProduct(depressurized, 0);

            // Distillation to purify methanol
            var meohDist = fs.AddMaterialStream("meoh_distillate");
            var waterBot = fs.AddMaterialStream("water_bottoms");
            var condDuty = fs.AddEnergyStream("cond_duty");
            var rebDuty = fs.AddEnergyStream("reb_duty");

            fs.AddDistillationColumn("T-1")
                .WithNumberOfStages(20)
                .WithFeed(depressurized, 10)
                .WithDistillate(meohDist)
                .WithBottoms(waterBot)
                .WithCondenserDuty(condDuty)
                .WithReboilerDuty(rebDuty)
                .WithCondenserSpec("Reflux Ratio", 2.0, "")
                .WithReboilerSpec("Temperature", 373.15, "K")
                .WithTopPressure(101325.0.Pascal())
                .WithColumnPressureDrop(0.0.Pascal())
                .Configure(c =>
                {
                    c.MaxIterations = 300;
                    c.ExternalLoopTolerance = 1e-3;
                    c.InternalLoopTolerance = 1e-3;
                });

            fs.TrySolve();

            // Read reactor outlet flows
            double Sum(string c, MaterialStreamBuilder s) =>
                s.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();

            double nCO_in = 1.0;
            double nCO_out = Sum("Carbon monoxide", rxOut) + Sum("Carbon monoxide", rxLiq);
            double convCO = (nCO_in - nCO_out) / nCO_in;

            // C atomic balance across reactor
            double Cin = 1.0 + 0.5; // 1 CO + 0.5 CO2
            double Cout = nCO_out
                + Sum("Carbon dioxide", rxOut) + Sum("Carbon dioxide", rxLiq)
                + Sum("Methanol", rxOut) + Sum("Methanol", rxLiq);

            double mRxOut = rxOut.MassFlowKgPerSecond;
            double mGas = flashGas.MassFlowKgPerSecond;
            double mCrude = crudeMeOH.MassFlowKgPerSecond;

            var rt = new ResultTable("F15 - Methanol synthesis from syngas")
                .RowInRange("CO conversion 20-99%", 0.20, 0.99, convCO, "-")
                .Row("C atomic balance across reactor", Cin, Cout, 0.01, "mol/s")
                .Row("Separator balance", mRxOut, mGas + mCrude, 0.005, "kg/s")
                .RowInRange("Compressor work > 0", 0.001, 1e6, wComp.EnergyFlowKW, "kW");

            double distFlow = meohDist.MolarFlowMolPerSecond;
            if (distFlow > 1e-10)
            {
                double xMeOH_dist = meohDist.OverallMoleFraction("Methanol");
                rt.RowInRange("Distillate MeOH > 80%", 0.80, 1.0, xMeOH_dist, "-");
            }

            rt.PrintAndThrowIfFailed();
        }
    }
}
