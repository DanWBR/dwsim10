using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using LysisTechnology = DWSIM.UnitOperations.UnitOperations.LysisTechnology;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>HPH Cell Lysis - E. coli at 80 MPa, 2 passes. Hetherington-style cell disruption.
    /// Expected: > 80 % release of intracellular content to the lysate.</summary>
    internal static class B05_CellLysis_HighPressure
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B05_Lysis")
                .WithCompounds("Water", "Biomass_Ecoli", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.85)
                .SetCompoundMassFlow("Biomass_Ecoli", 0.10)
                .SetCompoundMassFlow("Ethanol", 0.05);

            var lysate = fs.AddMaterialStream("lysate");
            var debris = fs.AddMaterialStream("debris");

            var cl = fs.AddCellLysis("CL-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(LysisTechnology.HighPressureHomogenizer)
                .WithPasses(2)
                .WithPressureMPa(80.0)
                .WithBiomassCompound("Biomass_Ecoli")
                .WithDefaultReleaseFraction(0.50)
                .WithReleaseFraction("Ethanol", 0.95)
                .ConnectFeed(feed, 0)
                .ConnectProduct(lysate, 0)
                .ConnectProduct(debris, 1);

            cl.Object.Calculate();

            new ResultTable("Cell Lysis HPH - E. coli, 80 MPa × 2 passes")
                .Row("Feed mass", 1.0, cl.Object.Result_FeedMass_kgs, 0.001, "kg/s")
                .Row("Balance F = L + D", 1.0, cl.Object.Result_LysateMass_kgs + cl.Object.Result_DebrisMass_kgs, 0.001, "kg/s")
                .RowInRange("Lysate > debris (mass)", 1.0, 1e9,
                    cl.Object.Result_LysateMass_kgs / System.Math.Max(cl.Object.Result_DebrisMass_kgs, 1e-9), "-")
                .RowInRange("Ethanol majority in lysate", 0.04, 0.05001,
                    lysate.OverallMassFraction("Ethanol") * lysate.MassFlowKgPerSecond, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
