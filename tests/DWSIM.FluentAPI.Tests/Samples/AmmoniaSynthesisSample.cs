using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Ammonia synthesis (Haber-Bosch), simplified single pass.
    /// Feed (3:1 H2:N2) → compressor (200 bar) → heater (700 K) → equilibrium reactor
    /// (ln Keq = 11000/T − 25, Gillespie/Beattie approximation) → cooler (250 K) →
    /// separator (liquid NH3 + recycle-grade gas).
    /// Checks: equilibrium-limited conversion, H and N atom balances, NH3 condenses.</summary>
    internal static class AmmoniaSynthesisSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("AmmoniaSynthesis")
                .WithCompounds("Hydrogen", "Nitrogen", "Ammonia")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var rxn = fs.DefineEquilibriumReaction("R_NH3",
                stoichiometry: new Dictionary<string, double>
                {
                    { "Nitrogen", -1 }, { "Hydrogen", -3 }, { "Ammonia", 2 }
                },
                baseCompound: "Ammonia",
                phase: "Vapor",
                basis: "Activity",
                units: "",
                lnKeqExpression: "11000/T - 25.0");
            fs.ReactionSet("NH3Set").Add(rxn);

            var feed = fs.AddMaterialStream("makeup gas")
                .At(300.0.Kelvin(), 30e5.Pascal())
                .WithMolarFlow(4.0.MolPerSecond())
                .SetCompoundMolarFlow("Hydrogen", 3.0)
                .SetCompoundMolarFlow("Nitrogen", 1.0)
                .SetCompoundMolarFlow("Ammonia", 0.0);

            var compOut = fs.AddMaterialStream("compressed");
            var wComp = fs.AddEnergyStream("W comp");
            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(200e5.Pascal())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(compOut, 0)
                .ConnectEnergyFeed(wComp, 1);

            var hot = fs.AddMaterialStream("converter feed");
            fs.AddHeater("H-1")
                .WithOutletTemperature(700.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(compOut, 0)
                .ConnectProduct(hot, 0);

            var rxOut = fs.AddMaterialStream("converter out");
            var rxLiq = fs.AddMaterialStream("converter liquid");
            var qRx = fs.AddEnergyStream("Q converter");
            fs.AddEquilibriumReactor("R-1")
                .Isothermal()
                .WithReactionSet("NH3Set")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(hot, 0)
                .ConnectProduct(rxOut, 0)
                .ConnectProduct(rxLiq, 1)
                .ConnectEnergyFeed(qRx, 1);

            var cold = fs.AddMaterialStream("chilled effluent");
            var qCool = fs.AddEnergyStream("Q chiller");
            fs.AddCooler("CL-1")
                .WithOutletTemperature(250.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(rxOut, 0)
                .ConnectProduct(cold, 0)
                .ConnectEnergyFeed(qCool, 1);

            var purge = fs.AddMaterialStream("recycle gas");
            var nh3Liq = fs.AddMaterialStream("liquid ammonia");
            fs.AddSeparator("V-1")
                .ConnectFeed(cold, 0)
                .ConnectProduct(purge, 0)
                .ConnectProduct(nh3Liq, 1);

            fs.Solve();

            double Sum(string c, MaterialStreamBuilder s) =>
                s.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();

            double n_NH3_rx = Sum("Ammonia", rxOut) + Sum("Ammonia", rxLiq);
            double n_H2_rx = Sum("Hydrogen", rxOut) + Sum("Hydrogen", rxLiq);
            double n_N2_rx = Sum("Nitrogen", rxOut) + Sum("Nitrogen", rxLiq);

            double convN2 = 1.0 - n_N2_rx;

            double Hin = 3.0 * 2;
            double Hout = n_H2_rx * 2 + n_NH3_rx * 3;
            double Nin = 1.0 * 2;
            double Nout = n_N2_rx * 2 + n_NH3_rx;

            new ResultTable("Ammonia synthesis (single pass, 200 bar / 700 K)")
                .RowInRange("N2 conversion within 5-99 %", 0.05, 0.99, convN2, "-")
                .Row("H atom balance across the converter", Hin, Hout, 0.005, "mol/s")
                .Row("N atom balance across the converter", Nin, Nout, 0.005, "mol/s")
                .RowInRange("NH3 produced > 0", 1e-6, 2.0, n_NH3_rx, "mol/s")
                .RowInRange("Separator liquid enriched in NH3 (>50 %)", 0.5, 1.0, nh3Liq.OverallMoleFraction("Ammonia"), "-")
                .RowInRange("Recycle gas keeps most of the H2/N2", 0.0, 0.5, purge.OverallMoleFraction("Ammonia"), "-")
                .RowInRange("Compressor work > 0", 0.001, 1e6, wComp.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "ammonia-synthesis-single-pass");
        }
    }
}
