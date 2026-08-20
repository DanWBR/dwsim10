using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums.GraphicObjects;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;
using HXMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode;
using RecycleOp = DWSIM.UnitOperations.SpecialOps.Recycle;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Natural gas hydrocarbon dew-point control unit (JT self-refrigeration).
    /// Dehydrated gas (60 bar) → inlet KO drum → gas-gas exchanger (hot side) →
    /// JT valve (60 → 30 bar) → cold separator → cold gas back through the exchanger's
    /// cold side (loop closed with a Recycle) → sales gas → export compressor.
    /// Checks: recycle converges, JT cooling happens, sales gas is leaner in C5+ than
    /// the feed, NGL is recovered, global mass balance closes.</summary>
    internal static class NaturalGasProcessingSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("NaturalGasDewPoint")
                .WithCompounds("Methane", "Ethane", "Propane", "N-butane", "N-pentane", "Nitrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // Gas leaving an upstream dehydration unit: water-free, still rich in C3+.
            var feed = fs.AddMaterialStream("feed gas")
                .At(303.15.Kelvin(), 60e5.Pascal())
                .WithMolarFlow(100.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 88.0)
                .SetCompoundMolarFlow("Ethane", 6.5)
                .SetCompoundMolarFlow("Propane", 2.5)
                .SetCompoundMolarFlow("N-butane", 1.0)
                .SetCompoundMolarFlow("N-pentane", 0.5)
                .SetCompoundMolarFlow("Nitrogen", 1.5);

            // Inlet knockout drum: dry in the design case, catches slugs in operation.
            var hpGas = fs.AddMaterialStream("HP gas");
            var hpCondensate = fs.AddMaterialStream("HP condensate");
            fs.AddSeparator("V-1")
                .ConnectFeed(feed, 0)
                .ConnectProduct(hpGas, 0)
                .ConnectProduct(hpCondensate, 1);

            // Tear stream for the gas-gas exchanger's cold side: the recycle overwrites
            // it, but it needs a complete initial state close to the expected answer.
            var coldGasTear = fs.AddMaterialStream("cold gas")
                .At(253.15.Kelvin(), 30e5.Pascal())
                .WithMolarFlow(97.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 87.5)
                .SetCompoundMolarFlow("Ethane", 6.3)
                .SetCompoundMolarFlow("Propane", 2.2)
                .SetCompoundMolarFlow("N-butane", 0.6)
                .SetCompoundMolarFlow("N-pentane", 0.1)
                .SetCompoundMolarFlow("Nitrogen", 1.5);

            // Gas-gas exchanger: the cold sales gas pre-chills the HP gas before the
            // JT valve. Hot outlet temperature specified; cold outlet computed.
            var precooled = fs.AddMaterialStream("precooled gas");
            var salesGasLP = fs.AddMaterialStream("sales gas LP");
            fs.AddHeatExchanger("HX-1")
                .WithCalculationMode(HXMode.CalcTempColdOut)
                .WithHotSidePressureDrop(0.0.Pascal())
                .WithColdSidePressureDrop(0.0.Pascal())
                .Configure(o => o.HotSideOutletTemperature = 268.15)
                .ConnectFeed(hpGas, 0)
                .ConnectFeed(coldGasTear, 1)
                .ConnectProduct(precooled, 0)
                .ConnectProduct(salesGasLP, 1);

            // JT valve: isenthalpic expansion 60 → 30 bar does the final chilling.
            var chilled = fs.AddMaterialStream("chilled gas");
            fs.AddValve("JT-1")
                .WithOutletPressure(30e5.Pascal())
                .ConnectFeed(precooled, 0)
                .ConnectProduct(chilled, 0);

            // Cold separator: the C3+ condensed by the JT drop leaves here as NGL.
            var ltGas = fs.AddMaterialStream("LT gas");
            var ngl = fs.AddMaterialStream("NGL");
            fs.AddSeparator("V-2")
                .ConnectFeed(chilled, 0)
                .ConnectProduct(ltGas, 0)
                .ConnectProduct(ngl, 1);

            var rec = fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-1")
                .ConnectFeed(ltGas, 0)
                .ConnectProduct(coldGasTear, 0);

            // Export compression back to pipeline pressure.
            var export = fs.AddMaterialStream("export gas");
            var wExp = fs.AddEnergyStream("W export");
            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(60e5.Pascal())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(salesGasLP, 0)
                .ConnectProduct(export, 0)
                .ConnectEnergyFeed(wExp, 1);

            fs.Solve();

            var recObj = (RecycleOp)rec.Object;
            double mIn = feed.MassFlowKgPerSecond;
            double mOut = export.MassFlowKgPerSecond + hpCondensate.MassFlowKgPerSecond
                + ngl.MassFlowKgPerSecond;

            double dT_JT = precooled.TemperatureK - chilled.TemperatureK;
            double xC5_feed = feed.OverallMoleFraction("N-pentane");
            double xC5_export = export.OverallMoleFraction("N-pentane");
            double xC4_feed = feed.OverallMoleFraction("N-butane");
            double xC4_export = export.OverallMoleFraction("N-butane");

            new ResultTable("Natural gas HC dew-point control (JT self-refrigeration)")
                .RowInRange("Recycle converged", 1.0, 1.0, recObj.Converged ? 1.0 : 0.0, "-")
                .Row("Global mass balance", mIn, mOut, 0.005, "kg/s")
                .RowInRange("JT valve chills the gas (dT > 3 K)", 3.0, 60.0, dT_JT, "K")
                .RowInRange("Cold separator runs below 260 K", 200.0, 260.0, chilled.TemperatureK, "K")
                .RowInRange("Sales gas leaner in C5 than the feed", 0.0, xC5_feed * 0.9, xC5_export, "-")
                .RowInRange("Sales gas leaner in C4 than the feed", 0.0, xC4_feed * 0.98, xC4_export, "-")
                .RowInRange("NGL recovered (> 0)", 1e-6, 10.0, ngl.MassFlowKgPerSecond, "kg/s")
                .RowInRange("Export at 60 bar", 59.9e5, 60.1e5, export.PressurePa, "Pa")
                .RowInRange("Export compressor work > 0", 0.001, 1e6, wExp.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "natural-gas-dew-point-control");
        }
    }
}
