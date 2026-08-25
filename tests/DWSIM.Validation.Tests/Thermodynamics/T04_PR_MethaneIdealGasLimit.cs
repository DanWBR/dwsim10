using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Peng-Robinson - ideal gas limit: methane at 500 K and 1 bar.
    /// Expected behavior: single-phase gas, ρ ≈ P·M/(R·T) = 1e5·0.01604/(8.314·500) = 0.3858 kg/m³.
    /// PR EOS should agree with ideal gas within &lt; 0.5 % in this regime (P·v_R/RT ≈ 1).</summary>
    internal static class T04_PR_MethaneIdealGasLimit
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T04_PR_CH4_IdealLimit")
                .WithCompound("Methane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var s = fs.AddMaterialStream("feed")
                .At(500.0.Kelvin(), 1e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 1.0);

            fs.Solve();

            double rho = s.Object.Phases[0].Properties.density.GetValueOrDefault();
            double rhoIdeal = 1e5 * 0.01604 / (8.314 * 500.0);
            double vaporFrac = s.Object.Phases[2].Properties.molarfraction.GetValueOrDefault();

            new ResultTable("PR EOS - CH4 @ 500 K, 1 bar (ideal gas limit)")
                .Row("ρ vs ideal gas (PM/RT)", rhoIdeal, rho, 0.005, "kg/m³")
                .Row("Vapor fraction (expected =1)", 1.0, vaporFrac, 0.001, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
