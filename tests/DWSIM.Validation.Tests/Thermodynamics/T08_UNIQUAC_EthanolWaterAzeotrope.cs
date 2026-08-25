using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>UNIQUAC - ethanol/water azeotrope @ 1 atm.
    /// Reference (Gmehling/Onken): T ≈ 351.45 K, x_EtOH ≈ 0.894 mol.
    /// At the azeotrope: y_EtOH ≈ x_EtOH.</summary>
    internal static class T08_UNIQUAC_EthanolWaterAzeotrope
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T08_UNIQUAC_Az")
                .WithCompounds("Water", "Ethanol")
                .WithPropertyPackage(PropertyPackages.UNIQUAC);

            var s = fs.AddMaterialStream("feed")
                .WithPressure(101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Ethanol", 0.894)
                .SetCompoundMolarFlow("Water", 0.106);

            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.5;
            });

            fs.Solve();

            double T = s.TemperatureK;
            double yEtOH = s.Object.Phases[2].Compounds["Ethanol"].MoleFraction.GetValueOrDefault();
            double xEtOH = s.Object.Phases[1].Compounds["Ethanol"].MoleFraction.GetValueOrDefault();

            new ResultTable("UNIQUAC - EtOH/H2O azeotrope @ 1 atm")
                .Row("T_az (Gmehling 351.45 K)", 351.45, T, 0.02, "K")
                .Row("y_EtOH ≈ x_EtOH", xEtOH, yEtOH, 0.05, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
