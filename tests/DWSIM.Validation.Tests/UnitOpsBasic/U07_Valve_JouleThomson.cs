using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Adiabatic (isenthalpic) valve - Joule-Thomson effect on high-pressure CO2.
    /// Feed: CO2 @ 300 K, 100 bar → 10 bar. Expected: μ_JT > 0 for CO2 in this regime → T_out &lt; T_in.
    /// Enthalpy balance: h_in ≈ h_out (isenthalpic valve).</summary>
    internal static class U07_Valve_JouleThomson
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U07_Valve_JT")
                .WithCompound("Carbon dioxide")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("in")
                .At(300.0.Kelvin(), 100e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Carbon dioxide", 1.0);

            var outS = fs.AddMaterialStream("out");

            fs.AddValve("V-1")
                .WithOutletPressure(10e5.Pascal())
                .ConnectFeed(feed, 0)
                .ConnectProduct(outS, 0);

            fs.Solve();

            double hIn = feed.Object.GetMassEnthalpy();
            double hOut = outS.Object.GetMassEnthalpy();

            new ResultTable("Isenthalpic valve - CO2 100→10 bar, T_in=300 K (JT effect)")
                .Row("h_in ≈ h_out", hIn, hOut, 0.001, "kJ/kg")
                .RowInRange("T_out < T_in (μ_JT > 0)", 200.0, 299.99, outS.TemperatureK, "K")
                .RowInRange("P_out", 9.99e5, 10.01e5, outS.PressurePa, "Pa")
                .PrintAndThrowIfFailed();
        }
    }
}
