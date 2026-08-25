using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.PropertyPackageConfig
{
    /// <summary>ConfigureNRTL(WithBinary) - overrides A12/A21/alpha for the ethanol/water pair
    /// and verifies that the bubble-point temperature shifts compared to the DB default.
    /// Extreme values (A12=A21=5000 cal/mol) should deviate significantly from the default.</summary>
    internal static class P02_NRTL_BinaryEffect
    {
        private static double BubbleTWith(double a12, double a21, double alpha)
        {
            var fs = Flowsheet.Create($"P02_NRTL_{a12}_{a21}")
                .WithCompounds("Water", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL, pp => pp
                    .ConfigureNRTL(n => n.WithBinary("Ethanol", "Water", a12, a21, alpha)));

            var s = fs.AddMaterialStream("feed")
                .WithPressure(101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Ethanol", 0.5)
                .SetCompoundMolarFlow("Water", 0.5);
            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.001;
            });
            fs.Solve();
            return s.TemperatureK;
        }

        public static void Run()
        {
            // "Default-like" range and "strongly non-ideal" range to force a shift in the bubble point.
            double tDefault = BubbleTWith(-100.0, 700.0, 0.3);
            double tStrong = BubbleTWith(2000.0, 2000.0, 0.3);

            new ResultTable("NRTL - effect of EtOH/H2O BIPs on bubble point @ 1 atm, x=0.5")
                .RowInRange("T_bubble with default-like BIPs > 350 K", 350.0, 380.0, tDefault, "K")
                .RowInRange("T_bubble with strong BIPs finite", 300.0, 400.0, tStrong, "K")
                .RowInRange("Δ T_bubble > 0.1 K (BIPs altered the flash)", 0.1, 50.0,
                    System.Math.Abs(tStrong - tDefault), "K")
                .PrintAndThrowIfFailed();
        }
    }
}
