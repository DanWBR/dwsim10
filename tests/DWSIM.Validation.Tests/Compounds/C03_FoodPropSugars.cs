using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using TPP = DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Validation.Tests.Compounds
{
    /// <summary>The sugars of the FoodProp database - glucose, fructose, maltose and sucrose - against the
    /// thermochemistry of aqueous carbohydrates and the properties of their solutions. Pins the heats and
    /// equilibrium constants of glucose isomerisation (Tewari and Goldberg 1984) and of maltose and sucrose
    /// hydrolysis (Goldberg and Tewari 1994), on the basis of DWSIM's corn compounds (glucose -1271.1 kJ/mol,
    /// liquid water -285.83); the heat capacity of a 30 % glucose solution (International Critical Tables V,
    /// in the CRA Critical Data Tables) and its density (NBS Scientific Paper 293); and maltose staying
    /// dissolved when a forced solid switches on the solid-liquid equilibrium.</summary>
    internal static class C03_FoodPropSugars
    {
        public static void Run()
        {
            string[] comps = { "Water", "Glucose", "Fructose", "Maltose", "Sucrose" };
            // the solution properties on the package as DWSIM creates it, density from the compounds' own curves
            var fs = Flowsheet.Create("C03_FoodPropSugars")
                .WithCompounds(comps)
                .WithPropertyPackage(PropertyPackages.NRTL);
            var pp = (TPP.PropertyPackage)fs.Inner.PropertyPackages.Values.First();
            pp.LiquidDensityCalculationMode_Subcritical = TPP.PropertyPackage.LiquidDensityCalcMode.Rackett_and_ExpData;
            var glucose30 = fs.AddMaterialStream("glucose 30 %").At(20.0.Celsius(), 1.0.Atm())
                .SetCompoundMassFlow("Water", 0.7).SetCompoundMassFlow("Glucose", 0.3);
            fs.Solve();

            // a forced solid switches on the solid-liquid equilibrium of every compound
            var fsSolid = Flowsheet.Create("C03_FoodPropSugars_SLE")
                .WithCompounds(comps)
                .WithPropertyPackage(PropertyPackages.NRTL, p => p.WithForcedSolids("Sucrose"));
            var maltose10 = fsSolid.AddMaterialStream("maltose 10 %").At(60.0.Celsius(), 1.0.Atm())
                .SetCompoundMassFlow("Water", 0.9).SetCompoundMassFlow("Maltose", 0.1);
            fsSolid.Solve();

            const double R = 8.314462618e-3, T = 298.15, HwL = -285.83, GwL = -237.129;     // kJ/mol
            var sc = fs.Inner.SelectedCompounds;
            Func<string, double> Hf = c => sc[c].IG_Enthalpy_of_Formation_25C * sc[c].Molar_Weight / 1000.0;
            Func<string, double> Gf = c => sc[c].IG_Gibbs_Energy_of_Formation_25C * sc[c].Molar_Weight / 1000.0;

            double dHiso = Hf("Fructose") - Hf("Glucose");
            double Kiso = Math.Exp(-(Gf("Fructose") - Gf("Glucose")) / (R * T));
            double dHmal = 2.0 * Hf("Glucose") - Hf("Maltose") - HwL;
            double dHsuc = Hf("Glucose") + Hf("Fructose") - Hf("Sucrose") - HwL;
            double Ksuc = Math.Exp(-(Gf("Glucose") + Gf("Fructose") - Gf("Sucrose") - GwL) / (R * T));
            double rho = glucose30.Object.Phases[1].Properties.density.GetValueOrDefault();
            double cp = glucose30.Object.Phases[1].Properties.heatCapacityCp.GetValueOrDefault();
            double crystal = maltose10.Object.Phases[7].Properties.massfraction.GetValueOrDefault();

            new ResultTable("FoodProp sugars - thermochemistry and solution properties")
                .Row("Glucose(aq) -> fructose(aq): heat of reaction, +2.78 kJ/mol", 2.78, dHiso, 0.02, "kJ/mol")
                .Row("Glucose(aq) -> fructose(aq): K at 25 C, 0.869", 0.869, Kiso, 0.01, "-")
                .Row("Maltose + water -> 2 glucose: heat of reaction, -4.59 kJ/mol", -4.59, dHmal, 0.02, "kJ/mol")
                .Row("Sucrose + water -> glucose + fructose: heat of reaction, -15.00 kJ/mol", -15.00, dHsuc, 0.01, "kJ/mol")
                .Row("Sucrose + water -> glucose + fructose: K at 25 C, 4.44e4", 4.44e4, Ksuc, 0.02, "-")
                .Row("Glucose 30 % at 20 C: density, 1124.75 kg/m3", 1124.75, rho, 0.01, "kg/m3")
                .Row("Glucose 30 % at 20 C: heat capacity, 3.49 kJ/(kg.K)", 3.49, cp, 0.05, "kJ/(kg.K)")
                .RowInRange("Maltose 10 % at 60 C beside a forced solid: no crystal", 0.0, 0.001, crystal, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
