using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Ratio-based splitter - splits 100 kg/s of water into 70 / 30.
    /// Expected behavior: T, P and composition preserved; m1 = 70 kg/s, m2 = 30 kg/s.</summary>
    internal static class U08_Splitter_Ratios
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U08_Splitter")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("in")
                .At(350.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(100.KgPerSecond())
                .SetCompoundMassFlow("Water", 100.0);

            var out1 = fs.AddMaterialStream("o1");
            var out2 = fs.AddMaterialStream("o2");

            fs.AddSplitter("SP-1")
                .Configure(sp =>
                {
                    sp.Ratios[0] = 0.70;
                    sp.Ratios[1] = 0.30;
                    sp.Ratios[2] = 0.0;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(out1, 0)
                .ConnectProduct(out2, 1);

            fs.Solve();

            new ResultTable("Splitter - H2O 100 kg/s, ratio 70/30")
                .Row("m_out1", 70.0, out1.MassFlowKgPerSecond, 0.001, "kg/s")
                .Row("m_out2", 30.0, out2.MassFlowKgPerSecond, 0.001, "kg/s")
                .Row("T_out1 = T_in", 350.0, out1.TemperatureK, 0.001, "K")
                .Row("T_out2 = T_in", 350.0, out2.TemperatureK, 0.001, "K")
                .PrintAndThrowIfFailed();
        }
    }
}
