using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>UNIFAC (predictive) - EtOH/H2O azeotrope @ 1 atm.
    /// Group-contribution model: T_az ≈ 350-352 K, x_EtOH ≈ 0.88-0.91.
    /// Looser tolerance (3%) since it is predictive.</summary>
    internal static class T10_UNIFAC_EthanolWaterAzeotrope
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T10_UNIFAC_Az")
                .WithCompounds("Water", "Ethanol")
                .WithPropertyPackage(PropertyPackages.UNIFAC);

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

            new ResultTable("UNIFAC (predictive) - EtOH/H2O @ 1 atm")
                .Row("T_az (351.45 K, tol 3%)", 351.45, T, 0.03, "K")
                .Row("y_EtOH ≈ x_EtOH", xEtOH, yEtOH, 0.10, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
