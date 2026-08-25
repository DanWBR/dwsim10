using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Wilson - methanol/water system @ 1 atm, equimolar.
    /// Expected behavior: bubble point ≈ 348-353 K (between water b.p. 373 K and methanol b.p. 337.7 K),
    /// vapor richer in methanol (more volatile).</summary>
    internal static class T09_Wilson_MethanolWater
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T09_Wilson_MeOH_H2O")
                .WithCompounds("Water", "Methanol")
                .WithPropertyPackage(PropertyPackages.Wilson);

            var s = fs.AddMaterialStream("feed")
                .WithPressure(101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Methanol", 0.5)
                .SetCompoundMolarFlow("Water", 0.5);

            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.001; // bubble point
            });

            fs.Solve();

            double Tbol = s.TemperatureK;
            double yMeOH = s.Object.Phases[2].Compounds["Methanol"].MoleFraction.GetValueOrDefault();

            new ResultTable("Wilson - equimolar MeOH/H2O @ 1 atm, bubble point")
                .RowInRange("T_bubble between pure b.p.", 337.7, 373.15, Tbol, "K")
                .RowInRange("y_MeOH > x_MeOH (more volatile)", 0.50, 1.0, yMeOH, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
