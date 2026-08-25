using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>Simple refrigeration cycle - propane as refrigerant.
    /// 1) Adiabatic compressor (η=80 %): saturated vapor @ -20 °C, 1.4 bar → 10 bar.
    /// 2) Condenser: superheated @ 10 bar → saturated liquid @ 30 °C.
    /// 3) Isenthalpic valve: liquid @ 10 bar → 1.4 bar (JT flash, low T).
    /// 4) Evaporator: L+V mixture @ 1.4 bar → saturated vapor @ -20 °C.
    /// Verifies: energy balance on each UO + COP = Q_evap / W_comp within the expected range (3-7).</summary>
    internal static class F01_RefrigerationCycle
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F01_Refrigeration")
                .WithCompound("Propane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // State 1: saturated vapor @ T_evap ≈ -20 °C → P ≈ 2.45 bar from PR.
            // To start with a fixed pressure, we use saturated vapor at 2.45 bar (T ≈ 253 K).
            var s1 = fs.AddMaterialStream("1_evaporator_out")
                .WithPressure(2.45e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Propane", 1.0);
            s1.Configure(ms =>
            {
                ms.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                ms.Phases[2].Properties.molarfraction = 1.0;   // saturated vapor
            });

            var s2 = fs.AddMaterialStream("2_compressor_out");
            var w_comp = fs.AddEnergyStream("W_comp");

            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(12.5e5.Pascal())
                .WithAdiabaticEfficiencyPercent(80.0)
                .ConnectFeed(s1, 0)
                .ConnectProduct(s2, 0)
                .ConnectEnergyFeed(w_comp, 1);

            // Condenser: cools to 305 K (32 °C) - below T_sat @ 12.5 bar (~36 °C),
            // ensures complete condensation. No energy stream - duty read from the builder after Solve.
            var s3 = fs.AddMaterialStream("3_condenser_out");

            var cd = fs.AddCooler("CD-1")
                .WithOutletTemperature(305.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(s2, 0)
                .ConnectProduct(s3, 0);

            // Valve: 12.5 → 2.45 bar, isenthalpic (Joule-Thomson).
            var s4 = fs.AddMaterialStream("4_valve_out");
            fs.AddValve("V-1")
                .WithOutletPressure(2.45e5.Pascal())
                .ConnectFeed(s3, 0)
                .ConnectProduct(s4, 0);

            // Evaporator: evaporates to 253 K (T_sat @ 2.45 bar in PR). No energy stream.
            var s5 = fs.AddMaterialStream("5_evaporator_out");

            var ev = fs.AddHeater("EV-1")
                .WithOutletTemperature(253.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(s4, 0)
                .ConnectProduct(s5, 0);

            fs.Solve();

            // Energy balance: q_evap (in) + W_comp (in) = q_cond (out, positive sign).
            double Wc = w_comp.EnergyFlowKW;
            double Qcond = cd.HeatRemovedKW;     // positive magnitude (heat rejected)
            double Qevap = ev.HeatDutyKW;         // positive magnitude (heat absorbed)

            // Refrigeration COP = Q_evap / W_comp (classical relation for simple R-290 cycle ≈ 4-6)
            double COP = Qevap / System.Math.Max(Wc, 1e-9);

            // s5 should approximately close the cycle at s1 (saturated vapor, same T and P)
            double T1 = s1.TemperatureK;
            double T5 = s5.TemperatureK;

            new ResultTable("F01 - Refrigeration cycle (propane, R-290)")
                .RowInRange("Compressor work > 0", 0.001, 100.0, Wc, "kW")
                .RowInRange("Condenser heat > 0 (rejection)", 0.001, 100.0, Qcond, "kW")
                .RowInRange("Evaporator heat > 0 (absorption)", 0.001, 100.0, Qevap, "kW")
                .Row("Balance Q_cond ≈ Q_evap + W_comp", Qevap + Wc, Qcond, 0.005, "kW")
                .Row("Cycle closure: T_5 ≈ T_1", T1, T5, 0.005, "K")
                // Typical COP for R-290 with 80 % adiabatic efficiency and moderate ΔT: 2-5.
                .RowInRange("Refrigeration COP within 2-7", 2.0, 7.0, COP, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
