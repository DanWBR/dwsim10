using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>VLE regression - Soave-Redlich-Kwong, a four-component acid gas / light hydrocarbon mixture
    /// (CO2 + methane + ethane + propane) at 230 K, 50 bar. Two phases form; CO2 and the heavier hydrocarbons
    /// concentrate in the liquid. Pins the vapour fraction and split so a change in the SRK flash is caught.</summary>
    internal static class T21_SRK_AcidGas_VLE4Comp
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T21_SRK_AcidGas")
                .WithCompounds("Carbon dioxide", "Methane", "Ethane", "Propane")
                .WithPropertyPackage(PropertyPackages.SoaveRedlichKwong);

            var s = fs.AddMaterialStream("feed")
                .At(250.0.Kelvin(), 40e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Carbon dioxide", 0.30)
                .SetCompoundMolarFlow("Methane", 0.40)
                .SetCompoundMolarFlow("Ethane", 0.20)
                .SetCompoundMolarFlow("Propane", 0.10);

            fs.Solve();

            var V = s.Object.Phases[2];
            var L = s.Object.Phases[3];
            double VF = V.Properties.molarfraction.GetValueOrDefault();
            double LF = L.Properties.molarfraction.GetValueOrDefault();
            double yC1 = V.Compounds["Methane"].MoleFraction.GetValueOrDefault();
            double xC3 = L.Compounds["Propane"].MoleFraction.GetValueOrDefault();

            new ResultTable("SRK CO2/C1/C2/C3 VLE @ 250 K, 40 bar")
                .Row("Phase fractions sum to 1", 1.0, VF + LF, 0.002, "-")
                .RowInRange("Vapour fraction", 0.40, 0.46, VF, "-")
                .RowInRange("Vapour is methane-rich (y_C1)", 0.60, 0.65, yC1, "-")
                .RowInRange("Propane concentrates in liquid (x_C3)", 0.14, 0.18, xC3, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
