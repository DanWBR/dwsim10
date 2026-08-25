using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using ExpMode = DWSIM.UnitOperations.UnitOperations.Expander;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Isentropic expander - air (N2) 1 mol/s, 500 K, 10 → 1 bar, η = 100 %.
    /// T2/T1 = (P2/P1)^((γ-1)/γ) = 0.1^0.2857 ≈ 0.518 → T_out ≈ 259 K.</summary>
    internal static class U06_Expander_Isentropic
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U06_Exp_Iso")
                .WithCompound("Nitrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("in")
                .At(500.0.Kelvin(), 10e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Nitrogen", 1.0);

            var outS = fs.AddMaterialStream("out");
            var work = fs.AddEnergyStream("W");

            fs.AddExpander("E-1")
                .WithProcessPath(ExpMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(1e5.Pascal())
                .WithAdiabaticEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(outS, 0)
                .ConnectEnergyProduct(work, 0);

            fs.Solve();

            double Tideal = 500.0 * System.Math.Pow(0.1, 0.4 / 1.4);

            new ResultTable("Isentropic expander - N2 10→1 bar, T_in=500 K")
                .Row("T_out (γ=1.4 ideal ≈ 259 K)", Tideal, outS.TemperatureK, 0.03, "K")
                .RowInRange("Generated work > 0", 0.1, 1e6, work.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
