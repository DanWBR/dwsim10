using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Isentropic compressor - pure N2 1 mol/s, 300 K, 1 → 5 bar, η_iso = 100 %.
    /// Diatomic ideal-gas approximation (γ ≈ 1.4): T2/T1 = (P2/P1)^((γ-1)/γ) = 5^0.2857 ≈ 1.583
    /// → T_out ≈ 475 K. PR EOS should agree within ~ 3 % in this regime.</summary>
    internal static class U05_Compressor_Isentropic
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U05_Comp_Iso")
                .WithCompound("Nitrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("in")
                .At(300.0.Kelvin(), 1e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Nitrogen", 1.0);

            var outS = fs.AddMaterialStream("out");
            var work = fs.AddEnergyStream("W");

            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(5e5.Pascal())
                .WithAdiabaticEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(outS, 0)
                .ConnectEnergyFeed(work, 1);

            fs.Solve();

            double Tout = outS.TemperatureK;
            double Tideal = 300.0 * System.Math.Pow(5.0, 0.4 / 1.4);

            new ResultTable("Isentropic compressor - N2 1→5 bar, T_in=300 K")
                .Row("T_out (γ=1.4 ideal ≈ 475 K)", Tideal, Tout, 0.03, "K")
                .RowInRange("P_out", 4.99e5, 5.01e5, outS.PressurePa, "Pa")
                .RowInRange("Work > 0", 0.1, 1e6, work.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
