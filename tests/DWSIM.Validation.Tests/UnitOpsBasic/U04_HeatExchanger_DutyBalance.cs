using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using HXMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Counter-current HeatExchanger - verifies energy balance: q_hot = q_cold = reported Q.
    /// Hot: 1 kg/s water @ 100 °C, 1 atm. Cold: 1 kg/s water @ 25 °C, 1 atm. UA = 5000 W/K.
    /// Expected behavior: |Δh_hot|·m = |Δh_cold|·m = Q (1st law).</summary>
    internal static class U04_HeatExchanger_DutyBalance
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U04_HX_Balance")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var hotIn = fs.AddMaterialStream("hotIn")
                .At(373.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            var coldIn = fs.AddMaterialStream("coldIn")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            var hotOut = fs.AddMaterialStream("hotOut");
            var coldOut = fs.AddMaterialStream("coldOut");

            var hx = fs.AddHeatExchanger("HX-1")
                .WithCalculationMode(HXMode.CalcBothTemp_UA)
                .WithGlobalUA(5000.0)
                .WithHotSidePressureDrop(0.0.Pascal())
                .WithColdSidePressureDrop(0.0.Pascal())
                .ConnectFeed(hotIn, 0)        // hot inlet
                .ConnectFeed(coldIn, 1)       // cold inlet
                .ConnectProduct(hotOut, 0)
                .ConnectProduct(coldOut, 1);

            fs.Solve();

            double Q = hx.Object.Q.GetValueOrDefault();   // kW
            double mHot = hotIn.MassFlowKgPerSecond;
            double mCold = coldIn.MassFlowKgPerSecond;
            double dhHot = hotIn.Object.GetMassEnthalpy() - hotOut.Object.GetMassEnthalpy();   // kJ/kg
            double dhCold = coldOut.Object.GetMassEnthalpy() - coldIn.Object.GetMassEnthalpy();
            double qHot = mHot * dhHot;     // kW
            double qCold = mCold * dhCold;  // kW

            new ResultTable("Counter-current HX - H2O 1 kg/s @ 100→? vs 25→?")
                .Row("q_hot vs Q", Q, qHot, 0.01, "kW")
                .Row("q_cold vs Q", Q, qCold, 0.01, "kW")
                .Row("Balance (q_hot - q_cold)/Q", 0.0, (qHot - qCold) / System.Math.Max(System.Math.Abs(Q), 1e-9), 0.01, "-")
                .RowInRange("Q > 0 (transfer occurred)", 1.0, 1e6, Q, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
