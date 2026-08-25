using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>UNIFAC-LL - package activated on immiscible n-hexane/water system at 298 K, 1 atm.
    /// The default flash (PT) does not trigger LLE automatically; this test simply verifies
    /// that the package runs and that the mixture enthalpy is finite (sanity check, regression).</summary>
    internal static class T16_UNIFAC_LL_NHexaneWater
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T16_UNIFAC_LL")
                .WithCompounds("Water", "N-hexane")
                .WithPropertyPackage(PropertyPackages.UNIFAC_LL);

            var s = fs.AddMaterialStream("feed")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Water", 0.5)
                .SetCompoundMolarFlow("N-hexane", 0.5);

            fs.Solve();

            double h = s.Object.GetMassEnthalpy();
            double T = s.TemperatureK;

            new ResultTable("UNIFAC-LL - H2O/n-hexane 50/50 @ 25 °C (smoke)")
                .Row("T preserved", 298.15, T, 0.001, "K")
                .RowInRange("Mass enthalpy finite", -1e6, 1e6, h, "kJ/kg")
                .PrintAndThrowIfFailed();
        }
    }
}
