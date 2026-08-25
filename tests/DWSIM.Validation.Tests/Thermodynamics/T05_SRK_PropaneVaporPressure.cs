using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>SRK - propane vapor pressure at 298.15 K.
    /// Reference (NIST, Antoine): P_sat ≈ 9.49 bar (949 kPa).</summary>
    internal static class T05_SRK_PropaneVaporPressure
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T05_SRK_C3H8_Psat")
                .WithCompound("Propane")
                .WithPropertyPackage(PropertyPackages.SoaveRedlichKwong);

            var s = fs.AddMaterialStream("sat")
                .WithTemperature(298.15.Kelvin())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Propane", 1.0);

            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Temperature_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.5;
            });

            fs.Solve();

            double Psat = s.PressurePa;

            new ResultTable("SRK - propane P_sat @ 298.15 K (NIST 9.49 bar)")
                .Row("P_sat", 9.49e5, Psat, 0.05, "Pa")
                .PrintAndThrowIfFailed();
        }
    }
}
