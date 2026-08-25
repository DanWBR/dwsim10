using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsAdvanced
{
    /// <summary>Adiabatic mixer - two water streams at different temperatures.
    /// 1st law (steady-state, no reaction): m1·h1 + m2·h2 = m3·h3.
    /// Case: 100 kg/s @ 300 K + 50 kg/s @ 348 K → m3 = 150 kg/s, weighted h3.</summary>
    internal static class A04_Mixer_EnergyBalance
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("A04_Mixer_Balance")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var inlet1 = fs.AddMaterialStream("inlet1")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(100.KgPerSecond())
                .SetCompoundMassFlow("Water", 100.0);

            var inlet2 = fs.AddMaterialStream("inlet2")
                .At(348.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(50.KgPerSecond())
                .SetCompoundMassFlow("Water", 50.0);

            var outlet = fs.AddMaterialStream("outlet");

            fs.AddMixer("MIX-1")
                .ConnectFeed(inlet1, 0)
                .ConnectFeed(inlet2, 1)
                .ConnectProduct(outlet, 0);

            fs.Solve();

            double m1 = inlet1.MassFlowKgPerSecond;
            double m2 = inlet2.MassFlowKgPerSecond;
            double m3 = outlet.MassFlowKgPerSecond;
            double h1 = inlet1.Object.GetMassEnthalpy();
            double h2 = inlet2.Object.GetMassEnthalpy();
            double h3 = outlet.Object.GetMassEnthalpy();

            double H_in = m1 * h1 + m2 * h2;
            double H_out = m3 * h3;

            new ResultTable("Adiabatic mixer - H2O 100 kg/s@300K + 50 kg/s@348K")
                .Row("Mass balance m1+m2=m3", m1 + m2, m3, 0.0001, "kg/s")
                .Row("Energy balance ΣmH_in=ΣmH_out", H_in, H_out, 0.001, "kW")
                .RowInRange("T_out within [300, 348] K", 300.0, 348.0, outlet.TemperatureK, "K")
                .PrintAndThrowIfFailed();
        }
    }
}
