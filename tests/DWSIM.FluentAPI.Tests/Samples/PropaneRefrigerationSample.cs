using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums.GraphicObjects;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;
using RecycleOp = DWSIM.UnitOperations.SpecialOps.Recycle;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Closed propane (R-290) vapor-compression refrigeration cycle.
    /// Suction (sat. vapor, 2.45 bar / 253 K) → compressor (12.5 bar, η=75 %) →
    /// condenser (305 K) → JT valve (2.45 bar) → evaporator (253 K) → Recycle → suction.
    /// Checks: recycle converges, first-law closure Q_cond = Q_evap + W_comp, COP in range,
    /// flash gas at the valve outlet, and the saved .dwxmz re-solves after reload.</summary>
    internal static class PropaneRefrigerationSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("PropaneRefrigerationCycle")
                .WithCompound("Propane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // Tear stream: the recycle overwrites this each iteration; the values here
            // are the initial guess and must be a complete state or the loop starts empty.
            var suction = fs.AddMaterialStream("1 suction")
                .At(253.0.Kelvin(), 2.45e5.Pascal())
                .WithMassFlow(2.0.KgPerSecond())
                .SetCompoundMassFlow("Propane", 2.0);

            var discharge = fs.AddMaterialStream("2 discharge");
            var wComp = fs.AddEnergyStream("W comp");
            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(12.5e5.Pascal())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(suction, 0)
                .ConnectProduct(discharge, 0)
                .ConnectEnergyFeed(wComp, 1);

            // Condenser: 305 K is below T_sat at 12.5 bar (~309 K), so the propane
            // leaves fully condensed and slightly subcooled.
            var liquid = fs.AddMaterialStream("3 liquid");
            var cd = fs.AddCooler("CD-1")
                .WithOutletTemperature(305.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(discharge, 0)
                .ConnectProduct(liquid, 0);

            var flashed = fs.AddMaterialStream("4 flashed");
            fs.AddValve("V-1")
                .WithOutletPressure(2.45e5.Pascal())
                .ConnectFeed(liquid, 0)
                .ConnectProduct(flashed, 0);

            var evapOut = fs.AddMaterialStream("5 evaporator out");
            var ev = fs.AddHeater("EV-1")
                .WithOutletTemperature(253.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(flashed, 0)
                .ConnectProduct(evapOut, 0);

            var rec = fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-1")
                .ConnectFeed(evapOut, 0)
                .ConnectProduct(suction, 0);

            fs.Solve();

            var recObj = (RecycleOp)rec.Object;
            double Wc = wComp.EnergyFlowKW;
            double Qcond = cd.HeatRemovedKW;
            double Qevap = ev.HeatDutyKW;
            double cop = Qevap / Math.Max(Wc, 1e-9);
            double vfFlashed = flashed.Object.Phases[2].Properties.molarfraction.GetValueOrDefault();

            new ResultTable("Propane refrigeration cycle (closed loop)")
                .RowInRange("Recycle converged", 1.0, 1.0, recObj.Converged ? 1.0 : 0.0, "-")
                .RowInRange("Compressor work > 0", 0.001, 1e4, Wc, "kW")
                .Row("First law: Q_cond = Q_evap + W_comp", Qevap + Wc, Qcond, 0.01, "kW")
                .Row("Cycle closure: T_suction = T_evap_out", evapOut.TemperatureK, suction.TemperatureK, 0.005, "K")
                .Row("Mass around the loop is preserved", suction.MassFlowKgPerSecond, evapOut.MassFlowKgPerSecond, 0.001, "kg/s")
                .RowInRange("Flash gas at valve outlet (VF 0.1-0.6)", 0.1, 0.6, vfFlashed, "-")
                .RowInRange("Refrigeration COP within 2-7", 2.0, 7.0, cop, "-")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "propane-refrigeration-cycle");
        }
    }
}
