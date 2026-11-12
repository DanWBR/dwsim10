using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.Validation.Tests.Framework;
using TPP = DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Solid enthalpy and entropy below the melting point. The liquid minus solid enthalpy is the enthalpy
    /// of fusion corrected with the heat capacity difference, and the entropy follows separately, so the Gibbs energy
    /// of fusion reproduces the solid-liquid equilibrium expression (it used to come out as zero at every
    /// temperature, the entropy being the enthalpy over the temperature). Pins the sign of the enthalpy of fusion,
    /// the heat capacity of ice and solid p-xylene against their data, the Gibbs energy of fusion of naphthalene and
    /// p-xylene, and the pressure-enthalpy flash of a p-xylene / benzene slurry coming back to its temperature.</summary>
    internal static class T35_NRTL_FusionEnthalpyEntropy
    {
        public static void Run()
        {
            string[] comps = { "Water", "Naphthalene", "P-xylene", "Benzene" };
            var fs = Flowsheet.Create("T35_Fusion")
                .WithCompounds(comps)
                .WithPropertyPackage(PropertyPackages.NRTL,
                    p => p.WithFlashSetting(FlashSetting.HandleSolidsInDefaultEqCalcMode, true));

            var s = fs.AddMaterialStream("slurry")
                .At(260.0.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Water", 0.0)
                .SetCompoundMolarFlow("Naphthalene", 0.0)
                .SetCompoundMolarFlow("P-xylene", 0.7)
                .SetCompoundMolarFlow("Benzene", 0.3);
            fs.Solve();

            var pp = (TPP.PropertyPackage)fs.Inner.PropertyPackages.Values.First();
            pp.CurrentMaterialStream = s.Object;
            var names = pp.RET_VNAMES();
            const double P = 101325.0, R = 8.31446;

            Func<string, double, TPP.State, double> H = (c, T, st) =>
            {
                // a flowsheet solve leaves the package without a stream
                pp.CurrentMaterialStream = s.Object;
                var x = new double[names.Length]; x[Array.IndexOf(names, c)] = 1.0;
                return pp.DW_CalcEnthalpy(x, T, P, st);
            };
            Func<string, double, TPP.State, double> S = (c, T, st) =>
            {
                // a flowsheet solve leaves the package without a stream
                pp.CurrentMaterialStream = s.Object;
                var x = new double[names.Length]; x[Array.IndexOf(names, c)] = 1.0;
                return pp.DW_CalcEntropy(x, T, P, st);
            };
            // ln of the solid-liquid activity ratio from the Gibbs energy of fusion, and from the fusion data
            Func<string, double, double> lnaFromG = (c, T) =>
            {
                var cp = s.Object.Phases[0].Compounds[c].ConstantProperties;
                double dH = H(c, T, TPP.State.Liquid) - H(c, T, TPP.State.Solid);
                double dS = S(c, T, TPP.State.Liquid) - S(c, T, TPP.State.Solid);
                return -(dH - T * dS) * cp.Molar_Weight / (R * T);
            };
            Func<string, double, double> lnaSle = (c, T) =>
            {
                var cp = s.Object.Phases[0].Compounds[c].ConstantProperties;
                return -cp.EnthalpyOfFusionAtTf * 1000.0 / R / T * (1.0 - T / cp.TemperatureOfFusion);
            };

            double TfN = s.Object.Phases[0].Compounds["Naphthalene"].ConstantProperties.TemperatureOfFusion;
            double TfX = s.Object.Phases[0].Compounds["P-xylene"].ConstantProperties.TemperatureOfFusion;

            double dHnaph = H("Naphthalene", TfN - 70.0, TPP.State.Liquid) - H("Naphthalene", TfN - 70.0, TPP.State.Solid);
            double cpIce = (H("Water", 263.15, TPP.State.Solid) - H("Water", 253.15, TPP.State.Solid)) / 10.0;
            double cpXyl = (H("P-xylene", 250.15, TPP.State.Solid) - H("P-xylene", 240.15, TPP.State.Solid)) / 10.0;
            string note = "";
            double cpIceData = s.Object.Phases[0].Compounds["Water"].ConstantProperties.GetSolidHeatCapacity(258.15, ref note);
            double cpXylData = s.Object.Phases[0].Compounds["P-xylene"].ConstantProperties.GetSolidHeatCapacity(245.15, ref note);

            // pressure-enthalpy flash of the slurry back to its own temperature
            double T0 = s.TemperatureK;
            double solid0 = s.Object.Phases[7].Properties.molarfraction.GetValueOrDefault();
            double H0 = s.Object.Phases[0].Properties.enthalpy.GetValueOrDefault();
            s.Object.SpecType = StreamSpec.Pressure_and_Enthalpy;
            s.Object.Phases[0].Properties.enthalpy = H0;
            s.Object.SetTemperature(T0 + 30.0);
            fs.Solve();
            double Tback = s.TemperatureK;

            new ResultTable("Enthalpy and entropy of fusion below the melting point (NRTL)")
                .RowInRange("Naphthalene: enthalpy of fusion 70 K below Tf, 110-150 kJ/kg (148 at Tf)", 110.0, 150.0, dHnaph, "kJ/kg")
                .Row("Ice: heat capacity from the solid enthalpy, 253 to 263 K", cpIceData, cpIce, 0.05, "kJ/(kg.K)")
                .Row("Solid p-xylene: heat capacity from the solid enthalpy, 240 to 250 K", cpXylData, cpXyl, 0.10, "kJ/(kg.K)")
                .Row("Naphthalene: ln a from dG of fusion, 10 K below Tf", lnaSle("Naphthalene", TfN - 10.0), lnaFromG("Naphthalene", TfN - 10.0), 0.03, "-")
                .Row("Naphthalene: ln a from dG of fusion, 40 K below Tf", lnaSle("Naphthalene", TfN - 40.0), lnaFromG("Naphthalene", TfN - 40.0), 0.06, "-")
                .Row("P-xylene: ln a from dG of fusion, 40 K below Tf", lnaSle("P-xylene", TfX - 40.0), lnaFromG("P-xylene", TfX - 40.0), 0.06, "-")
                .RowInRange("Slurry at 260 K holds solid and liquid", 0.2, 0.8, solid0, "-")
                .Row("PH flash of the slurry returns its temperature", T0, Tback, 0.001, "K")
                .PrintAndThrowIfFailed();
        }
    }
}
