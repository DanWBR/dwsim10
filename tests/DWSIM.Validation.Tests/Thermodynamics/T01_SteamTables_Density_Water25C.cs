using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>IAPWS-IF97 - liquid water density at 25 °C, 1 atm.
    /// Reference: NIST webbook, ρ = 997.05 kg/m³.</summary>
    internal static class T01_SteamTables_Density_Water25C
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T01_SteamTables_DensityWater25C")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var s = fs.AddMaterialStream("water")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            fs.Solve();

            double rho = s.Object.Phases[0].Properties.density.GetValueOrDefault();

            new ResultTable("Steam Tables (IAPWS-IF97) - liquid H2O @ 25 °C, 1 atm")
                .Row("Density ρ (NIST 997.05)", 997.05, rho, 0.005, "kg/m³")
                .PrintAndThrowIfFailed();
        }
    }
}
