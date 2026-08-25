using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Compounds
{
    /// <summary>WithPseudoComponent - heavy petroleum cut (NBP=650 K, SG=0.85, MW=240).
    /// Validates: PseudoBuilder computed Tc, Pc, ω, K_Watson; PT flash @ 700 K, 1 bar runs.
    /// Expected behavior: Tc > NBP, ω > 0, K_w ≈ 12 (paraffinic-naphthenic intermediate).</summary>
    internal static class C01_PseudoComponent_HeavyCut
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("C01_Pseudo")
                .WithPseudoComponent("Cut_650K",
                    normalBoilingPoint: 650.0.Kelvin(),
                    specificGravity: 0.85,
                    molarWeight: 240.0)
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var s = fs.AddMaterialStream("feed")
                .At(700.0.Kelvin(), 1e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Cut_650K", 1.0);

            fs.Solve();

            // Read constants of the created compound
            var cprop = fs.Inner.SelectedCompounds["Cut_650K"];

            new ResultTable("Pseudocomponent - Cut_650K (NBP=650 K, SG=0.85, MW=240)")
                .Row("NBP preserved", 650.0, cprop.NBP.GetValueOrDefault(), 1e-6, "K")
                .Row("MW preserved", 240.0, cprop.Molar_Weight, 1e-6, "g/mol")
                .RowInRange("Tc > NBP (Riazi-Daubert)", 650.0, 1200.0, cprop.Critical_Temperature, "K")
                .RowInRange("Pc within 5-50 bar", 5e5, 50e5, cprop.Critical_Pressure, "Pa")
                .RowInRange("Acentric factor > 0", 0.05, 1.5, cprop.Acentric_Factor, "-")
                .RowInRange("Watson K within paraffinic range (10-13)", 10.0, 13.0,
                            cprop.PF_Watson_K.GetValueOrDefault(), "-")
                .RowInRange("Density computed @ 700 K", 1.0, 100.0,
                            s.Object.Phases[0].Properties.density.GetValueOrDefault(), "kg/m³")
                .PrintAndThrowIfFailed();
        }
    }
}
