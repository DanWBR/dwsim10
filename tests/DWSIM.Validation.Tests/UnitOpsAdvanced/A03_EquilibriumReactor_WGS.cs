using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsAdvanced
{
    /// <summary>Equilibrium Reactor - Water-Gas Shift: CO + H2O ⇌ CO2 + H2.
    /// At 1100 K, ln(Keq) ≈ 0.0 (Keq ≈ 1.0) under a simplified correlation.
    /// Expected behavior: approach to equilibrium with CO_out·H2O_out ≈ CO2_out·H2_out (Keq=1).</summary>
    internal static class A03_EquilibriumReactor_WGS
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("A03_Eq_WGS")
                .WithCompounds("Carbon dioxide", "Carbon monoxide", "Water", "Hydrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // Keq(T) constant = 1.0 → ln(Keq) = 0 (simplified reference for 1100 K).
            var r = fs.DefineEquilibriumReaction("WGS",
                stoichiometry: new Dictionary<string, double> { { "Carbon monoxide", -1 }, { "Water", -1 }, { "Carbon dioxide", 1 }, { "Hydrogen", 1 } },
                baseCompound: "Carbon monoxide",
                phase: "Vapor",
                basis: "Activity",
                units: "",
                lnKeqExpression: "0.0");

            fs.ReactionSet("Set").Add(r);

            var feed = fs.AddMaterialStream("feed")
                .WithTemperature(1100.Kelvin())
                .WithPressure(101325.0.Pascal())
                .WithMolarFlow(2.0.MolPerSecond())
                .SetCompoundMolarFlow("Carbon monoxide", 1.0)
                .SetCompoundMolarFlow("Water", 1.0)
                .SetCompoundMolarFlow("Carbon dioxide", 0.0)
                .SetCompoundMolarFlow("Hydrogen", 0.0);

            var gasOut = fs.AddMaterialStream("gasOut");
            var liqOut = fs.AddMaterialStream("liqOut");
            var heat = fs.AddEnergyStream("heat");

            fs.AddEquilibriumReactor("R-EQ")
                .Isothermal()
                .WithReactionSet("Set")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(feed, 0)
                .ConnectProduct(gasOut, 0)
                .ConnectProduct(liqOut, 1)
                .ConnectEnergyFeed(heat, 1);

            fs.Solve();

            double Sum(string c) =>
                gasOut.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault()
                + liqOut.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();

            double nCO = Sum("Carbon monoxide");
            double nH2O = Sum("Water");
            double nCO2 = Sum("Carbon dioxide");
            double nH2 = Sum("Hydrogen");

            // Reaction quotient Q = (CO2·H2)/(CO·H2O) - flows cancel mole fraction (same phase)
            double Q = (nCO2 * nH2) / System.Math.Max(nCO * nH2O, 1e-30);

            new ResultTable("Eq Reactor - WGS @ 1100 K, Keq=1 (ln(K)=0)")
                .Row("Q ≈ Keq = 1.0", 1.0, Q, 0.05, "-")
                .Row("C balance (CO_in - CO_out = CO2_out)", 1.0 - nCO, nCO2, 0.005, "mol/s")
                .Row("H balance (H2O reacted = H2 formed)", 1.0 - nH2O, nH2, 0.005, "mol/s")
                .PrintAndThrowIfFailed();
        }
    }
}
