using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>IAPWS-IF97 - water saturation temperature at 101325 Pa.
    /// Reference: NIST webbook / IAPWS-IF97 → T_sat = 373.124 K (99.974 °C).</summary>
    internal static class T02_SteamTables_SaturationT_1atm
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T02_SteamTables_SatT")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            // PVF-flash: fixed P + vapor frac = 0.5 → flash returns T_sat
            var s = fs.AddMaterialStream("sat")
                .WithPressure(101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            // Define PVF spec (vapor fraction = 0.5) via escape hatch
            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.5;
            });

            fs.Solve();

            double Tsat = s.TemperatureK;

            new ResultTable("Steam Tables - water T_sat @ 1 atm")
                .Row("T_sat (NIST 373.124 K)", 373.124, Tsat, 0.005, "K")
                .PrintAndThrowIfFailed();
        }
    }
}
