using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Lee-Kesler-Plöcker - gaseous methane density @ 300 K, 1 bar (near-ideal regime).
    /// Expected: ρ ≈ PM/RT = 1e5·0.01604/(8.314·300) = 0.643 kg/m³, Z ≈ 1.</summary>
    internal static class T07_LKP_MethaneCompressibility
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T07_LKP_CH4")
                .WithCompound("Methane")
                .WithPropertyPackage(PropertyPackages.LeeKeslerPlocker);

            var s = fs.AddMaterialStream("gas")
                .At(300.0.Kelvin(), 1e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 1.0);

            fs.Solve();

            double rho = s.Object.Phases[0].Properties.density.GetValueOrDefault();
            double rhoIdeal = 1e5 * 0.01604 / (8.314 * 300.0);

            new ResultTable("LKP - CH4 gas @ 300 K, 1 bar (Z≈1)")
                .Row("ρ vs ideal gas", rhoIdeal, rho, 0.01, "kg/m³")
                .PrintAndThrowIfFailed();
        }
    }
}
