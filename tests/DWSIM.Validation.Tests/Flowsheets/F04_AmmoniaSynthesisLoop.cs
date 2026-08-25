using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Validation.Tests.Framework;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>Ammonia synthesis (Haber-Bosch) - simplified single-pass loop (NO recycle).
    /// Train: feed (3:1 H2:N2) → Compressor (200 bar) → Heater (700 K) → Equilibrium reactor
    ///        (Keq approximation) → Cooler (250 K) → Separator (NH3 liquid + gas).
    /// Validates: equilibrium-limited conversion; NH3 condenses in the separator;
    /// H/N atomic balance preserved.</summary>
    internal static class F04_AmmoniaSynthesisLoop
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F04_Ammonia")
                .WithCompounds("Hydrogen", "Nitrogen", "Ammonia")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // Reaction: N2 + 3 H2 ⇌ 2 NH3 ; approximate ln Keq (NH3 as base, vapor)
            var rxn = fs.DefineEquilibriumReaction("R_NH3",
                stoichiometry: new Dictionary<string, double>
                {
                    { "Nitrogen", -1 }, { "Hydrogen", -3 }, { "Ammonia", 2 }
                },
                baseCompound: "Ammonia",
                phase: "Vapor",
                basis: "Activity",
                units: "",
                // ln Keq Gillespie/Beattie approximation (NH3 from N2 + 3 H2):
                // K decreases with T (low-T favored). The expression yields ~30-50 % conversion
                // at 700 K depending on activities.
                lnKeqExpression: "11000/T - 25.0");
            fs.ReactionSet("NH3Set").Add(rxn);

            // Stoichiometric feed: 3 H2 + 1 N2 (mol/s)
            var feed = fs.AddMaterialStream("feed")
                .At(300.0.Kelvin(), 30e5.Pascal())
                .WithMolarFlow(4.0.MolPerSecond())
                .SetCompoundMolarFlow("Hydrogen", 3.0)
                .SetCompoundMolarFlow("Nitrogen", 1.0)
                .SetCompoundMolarFlow("Ammonia", 0.0);

            // Compression to 200 bar
            var compOut = fs.AddMaterialStream("comp_out");
            var wComp = fs.AddEnergyStream("W_comp");
            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(200e5.Pascal())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(compOut, 0)
                .ConnectEnergyFeed(wComp, 1);

            // Heating to 700 K
            var hot = fs.AddMaterialStream("hot");
            fs.AddHeater("H-1")
                .WithOutletTemperature(700.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(compOut, 0)
                .ConnectProduct(hot, 0);

            // Equilibrium reactor
            var rxOut = fs.AddMaterialStream("rx_out");
            var rxLiq = fs.AddMaterialStream("rx_liq");
            var qRx = fs.AddEnergyStream("Q_rx");
            fs.AddEquilibriumReactor("R-1")
                .Isothermal()
                .WithReactionSet("NH3Set")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(hot, 0)
                .ConnectProduct(rxOut, 0)
                .ConnectProduct(rxLiq, 1)
                .ConnectEnergyFeed(qRx, 1);

            // Cooling to 250 K to condense NH3
            var cold = fs.AddMaterialStream("cold");
            var qCool = fs.AddEnergyStream("Q_cool");
            fs.AddCooler("CL-1")
                .WithOutletTemperature(250.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(rxOut, 0)
                .ConnectProduct(cold, 0)
                .ConnectEnergyFeed(qCool, 1);

            // Two-phase separator: recyclable gas + liquid NH3
            var purge = fs.AddMaterialStream("purge_gas");
            var nh3Liq = fs.AddMaterialStream("NH3_liq");
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

            double convN2 = (1.0 - n_N2_rx) / 1.0;

            // Atomic balance (in the reactor): H_in = H_out, N_in = N_out
            double Hin = 3.0 * 2 + 0;          // 3 H2 → 6 H atoms
            double Hout = n_H2_rx * 2 + n_NH3_rx * 3;
            double Nin = 1.0 * 2;              // 1 N2 → 2 N atoms
            double Nout = n_N2_rx * 2 + n_NH3_rx;

            double yNH3_liq = nh3Liq.OverallMoleFraction("Ammonia");
            double yNH3_purge = purge.OverallMoleFraction("Ammonia");

            new ResultTable("F04 - Ammonia synthesis (single-pass)")
                .RowInRange("N2 conversion within 5-99 %", 0.05, 0.99, convN2, "-")
                .Row("H atomic balance", Hin, Hout, 0.005, "mol/s")
                .Row("N atomic balance", Nin, Nout, 0.005, "mol/s")
                .RowInRange("NH3 produced > 0", 1e-6, 2.0, n_NH3_rx, "mol/s")
                .RowInRange("Liquid enriched in NH3 (>50 %)", 0.5, 1.0, yNH3_liq, "-")
                .RowInRange("Purge gas predominantly H2/N2", 0.0, 0.5, yNH3_purge, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
