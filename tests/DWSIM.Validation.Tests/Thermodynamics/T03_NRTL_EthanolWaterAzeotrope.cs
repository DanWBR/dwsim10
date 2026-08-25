using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>NRTL - ethanol/water system at 1 atm, azeotropic point.
    /// Classical reference (Gmehling/Onken): azeotrope at x_EtOH ≈ 0.894 mol, T ≈ 351.45 K (78.30 °C).
    /// At the azeotrope, y_EtOH ≈ x_EtOH (vapor and liquid phase compositions coincide).</summary>
    internal static class T03_NRTL_EthanolWaterAzeotrope
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T03_NRTL_AzeotropeEtOHH2O")
                .WithCompounds("Water", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL);

            // PVF flash at 1 atm with VF=0.5 and overall composition at the azeotrope (z_EtOH = 0.894).
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
            // Phase compositions after flash (Phases[2] = Vapor, Phases[1] = LiquidMixture)
            double yEtOH = s.Object.Phases[2].Compounds["Ethanol"].MoleFraction.GetValueOrDefault();
            double xEtOH = s.Object.Phases[1].Compounds["Ethanol"].MoleFraction.GetValueOrDefault();

            new ResultTable("NRTL - ethanol/water azeotrope @ 1 atm, z_EtOH = 0.894")
                .Row("T_az (Gmehling 351.45 K)", 351.45, T, 0.01, "K")
                .Row("y_EtOH ≈ x_EtOH", xEtOH, yEtOH, 0.02, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
