using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Modified UNIFAC (NIST) - pure toluene at 383.78 K (normal b.p.).
    /// Expected: P_sat ≈ 101325 Pa.</summary>
    internal static class T19_ModUNIFAC_NIST_Toluene
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T19_ModUNIFAC_NIST")
                .WithCompound("Toluene")
                .WithPropertyPackage(PropertyPackages.ModifiedUNIFAC_NIST);

            var s = fs.AddMaterialStream("sat")
                .WithTemperature(383.78.Kelvin())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Toluene", 1.0);

            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Temperature_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.5;
            });

            fs.Solve();

            double Psat = s.PressurePa;

            new ResultTable("Mod UNIFAC (NIST) - toluene P_sat @ 383.78 K (b.p. ≈ 1 atm)")
                .Row("P_sat", 101325.0, Psat, 0.05, "Pa")
                .PrintAndThrowIfFailed();
        }
    }
}
