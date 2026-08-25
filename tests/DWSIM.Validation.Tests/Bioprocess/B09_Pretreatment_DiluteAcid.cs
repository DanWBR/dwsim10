using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>Dilute-acid pretreatment - biomass surrogated by Glucose as substrate.
    /// Without cellulose/hemicellulose compounds in the standard DB, we validate: Solve completes +
    /// mass balance, severity R0 ≈ 3981 (Overend &amp; Chornet).</summary>
    internal static class B09_Pretreatment_DiluteAcid
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B09_Pre")
                .WithCompounds("Water", "Glucose")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.82)
                .SetCompoundMassFlow("Glucose", 0.18);

            var slurry = fs.AddMaterialStream("slurry");

            var pre = fs.AddPretreatmentReactor("PRE-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(PretreatmentType.DiluteAcid)
                .WithSeverityLogR0(3.6)
                .WithResidenceTime(15.0.Minutes())
                .WithSolidsLoading(0.18)
                .WithCelluloseConversion(0.10)
                .WithHemicelluloseConversion(0.92)
                .WithLigninSolubilization(0.18)
                .WithGlucoseToHMF(0.025)
                .WithXyloseToFurfural(0.06)
                .ConnectFeed(feed, 0)
                .ConnectProduct(slurry, 0);

            pre.Object.Calculate();

            double r0 = Math.Pow(10.0, pre.Object.SeverityLogR0);

            new ResultTable("Pretreatment DiluteAcid - log R0=3.6")
                .Row("Severity R0 = 10^logR0", 3981.07, r0, 0.001, "-")
                .Row("Mass balance F = slurry", 1.0, slurry.MassFlowKgPerSecond, 0.001, "kg/s")
                .Row("T_out preserved", 303.15, slurry.TemperatureK, 0.001, "K")
                .RowInRange("Glucose preserved (no cellulose role)", 0.179, 0.181,
                    slurry.OverallMassFraction("Glucose") * slurry.MassFlowKgPerSecond, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
