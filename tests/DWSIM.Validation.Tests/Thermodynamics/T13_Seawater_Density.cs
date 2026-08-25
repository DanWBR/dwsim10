using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Seawater (IAPWS-08) - seawater density @ 25 °C, 1 atm, salinity 35 g/kg (standard ocean).
    /// Reference (IAPWS-08): ρ ≈ 1023.4 kg/m³.</summary>
    internal static class T13_Seawater_Density
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T13_Seawater")
                .WithCompounds("Water", "Salt")
                .WithPropertyPackage(PropertyPackages.Seawater);

            // 35 g salt / kg solution → mass fraction 0.035
            var s = fs.AddMaterialStream("sw")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Salt", 0.035)
                .SetCompoundMassFlow("Water", 0.965);

            fs.Solve();

            double rho = s.Object.Phases[0].Properties.density.GetValueOrDefault();

            new ResultTable("Seawater (IAPWS-08) - S=35 g/kg, 25 °C, 1 atm")
                .Row("ρ (IAPWS-08 ≈ 1023.4)", 1023.4, rho, 0.01, "kg/m³")
                .PrintAndThrowIfFailed();
        }
    }
}
