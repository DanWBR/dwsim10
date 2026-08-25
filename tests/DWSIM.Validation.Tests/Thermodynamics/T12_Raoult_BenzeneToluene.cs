using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Raoult's Law - equimolar benzene/toluene (~ideal system) @ 1 atm.
    /// Reference (Smith Van Ness, 8e, ex. 10.x): T_bubble ≈ 365 K, y_benzene ≈ 0.71.</summary>
    internal static class T12_Raoult_BenzeneToluene
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T12_Raoult_BzT")
                .WithCompounds("Benzene", "Toluene")
                .WithPropertyPackage(PropertyPackages.Raoult);

            var s = fs.AddMaterialStream("feed")
                .WithPressure(101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Benzene", 0.5)
                .SetCompoundMolarFlow("Toluene", 0.5);

            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.001;
            });

            fs.Solve();

            double Tbol = s.TemperatureK;
            double yBz = s.Object.Phases[2].Compounds["Benzene"].MoleFraction.GetValueOrDefault();

            new ResultTable("Raoult - equimolar Benzene/Toluene @ 1 atm")
                .Row("T_bubble (Smith VN ≈ 365 K)", 365.0, Tbol, 0.02, "K")
                .Row("y_Benzene (≈ 0.71)", 0.71, yBz, 0.05, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
