using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Modified UNIFAC (Dortmund) - equimolar acetone/water system @ 1 atm.
    /// Strongly non-ideal system. Expected: bubble point between acetone b.p. (329.2 K) and water (373.15 K),
    /// vapor enriched in acetone (more volatile).</summary>
    internal static class T11_ModUNIFAC_AcetoneWater
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T11_ModUNIFAC_Acet_H2O")
                .WithCompounds("Water", "Acetone")
                .WithPropertyPackage(PropertyPackages.ModifiedUNIFAC);

            var s = fs.AddMaterialStream("feed")
                .WithPressure(101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Acetone", 0.5)
                .SetCompoundMolarFlow("Water", 0.5);

            s.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 0.001;
            });

            fs.Solve();

            double Tbol = s.TemperatureK;
            double yAcetone = s.Object.Phases[2].Compounds["Acetone"].MoleFraction.GetValueOrDefault();

            new ResultTable("Mod UNIFAC (Dortmund) - equimolar Acetone/H2O @ 1 atm")
                .RowInRange("T_bubble between pure b.p.", 329.2, 373.15, Tbol, "K")
                .RowInRange("y_Acetone > x_Acetone", 0.50, 1.0, yAcetone, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
