using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>PRSV2 (kij matrix) - methanol vapor pressure at 337.7 K (normal b.p.).
    /// Expected: P_sat ≈ 1.0 atm (101325 Pa) - Stryjek-Vera improves prediction for polars.</summary>
    internal static class T14_PRSV2_MethanolVaporPressure
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T14_PRSV2_MeOH")
                .WithCompound("Methanol")
                .WithPropertyPackage(PropertyPackages.PRSV2M);

            var s = fs.AddMaterialStream("sat")
                .WithTemperature(337.7.Kelvin())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Methanol", 1.0);

            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Temperature_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.5;
            });

            fs.Solve();

            double Psat = s.PressurePa;

            new ResultTable("PRSV2-M - methanol P_sat @ 337.7 K (b.p. ≈ 1 atm)")
                .Row("P_sat", 101325.0, Psat, 0.05, "Pa")
                .PrintAndThrowIfFailed();
        }
    }
}
