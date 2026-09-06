using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>SLE regression - solid-liquid equilibrium in a xylene mixture, the basis of para-xylene
    /// recovery by crystallization. p-Xylene has by far the highest melting point of the three isomers
    /// (286 K, against 225 K and 248 K), so cooling a p-xylene / m-xylene / o-xylene mixture to 255 K freezes
    /// out solid p-xylene over a liquid. Solids handling is turned on in the flash. Pins that a solid phase
    /// forms, that it is essentially pure p-xylene, and that the liquid is depleted in it, so a change in the
    /// solid-liquid flash is caught.</summary>
    internal static class T26_NRTL_XyleneCrystallization_SLE
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T26_SLE_Xylene")
                .WithCompounds("P-xylene", "M-xylene", "O-xylene")
                .WithPropertyPackage(PropertyPackages.NRTL,
                    pp => pp.WithFlashSetting(FlashSetting.HandleSolidsInDefaultEqCalcMode, true));

            var s = fs.AddMaterialStream("feed")
                .At(255.0.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("P-xylene", 0.50)
                .SetCompoundMolarFlow("M-xylene", 0.30)
                .SetCompoundMolarFlow("O-xylene", 0.20);

            fs.Solve();

            var L = s.Object.Phases[3];
            var S = s.Object.Phases[7];
            double LF = L.Properties.molarfraction.GetValueOrDefault();
            double SF = S.Properties.molarfraction.GetValueOrDefault();
            double xP_liq = L.Compounds["P-xylene"].MoleFraction.GetValueOrDefault();
            double xP_sol = S.Compounds["P-xylene"].MoleFraction.GetValueOrDefault();

            new ResultTable("NRTL p/m/o-xylene SLE @ 255 K, 1 atm")
                .Row("Liquid + solid fractions sum to 1", 1.0, LF + SF, 0.005, "-")
                .RowInRange("A solid phase forms", 0.12, 0.17, SF, "-")
                .RowInRange("The solid is essentially pure p-xylene", 0.98, 1.0, xP_sol, "-")
                .RowInRange("The liquid is depleted in p-xylene (< feed 0.5)", 0.39, 0.44, xP_liq, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
