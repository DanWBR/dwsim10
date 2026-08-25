using System;
using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>BioReactor - aerobic E. coli growth on glucose with sulfuric acid as the sulfur
    /// carrier. Checks that every element the growth stoichiometry touches (C, H, O, N and S)
    /// leaves the reactor in the same amount it entered, and that sulfur is actually being
    /// assimilated rather than passing through untouched.</summary>
    internal static class B13_BioReactor_SulfurBalance
    {
        private static readonly string[] Elements = { "C", "H", "O", "N", "S" };

        public static void Run()
        {
            var fs = Flowsheet.Create("B13_BioReactorSulfur")
                .WithCompounds("Water", "Glucose", "Ammonia", "Oxygen", "Carbon dioxide",
                               "Sulfuric acid", "Biomass_Ecoli")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(310.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.809)
                .SetCompoundMassFlow("Glucose", 0.100)
                .SetCompoundMassFlow("Ammonia", 0.020)
                .SetCompoundMassFlow("Oxygen", 0.060)
                .SetCompoundMassFlow("Sulfuric acid", 0.010)
                .SetCompoundMassFlow("Biomass_Ecoli", 0.001);

            var liqOut = fs.AddMaterialStream("liqOut");
            var gasOut = fs.AddMaterialStream("gasOut");

            var br = fs.AddBioReactor("BR-S")
                .Configure(o => o.CreateConnectors())
                .WithVolume(20.0.CubicMeters())
                .WithKineticModel(BioKineticModel.Monod)
                .WithOperatingMode(BioReactorMode.Continuous)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithAerobic(true)
                .WithMaxSpecificGrowthPerHour(0.9)
                .WithMonodKsGPerL(0.05)
                .WithBiomassYield(0.48)
                .Configure(o =>
                {
                    o.BiomassCompound = "Biomass_Ecoli";
                    o.SubstrateCompound = "Glucose";
                    o.OxygenCompound = "Oxygen";
                    o.CO2Compound = "Carbon dioxide";
                    o.NitrogenSourceCompound = "Ammonia";
                    o.SulfurSourceCompound = "Sulfuric acid";
                    o.WaterCompound = "Water";
                    o.YieldPS = 0.0;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(liqOut, 0)
                .ConnectProduct(gasOut, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            br.Object.SetPropertyPackageInstance(pp);
            br.Object.Calculate();

            var inAtoms = AtomFlows(feed);
            var outAtoms = AtomFlows(liqOut);
            foreach (var kv in AtomFlows(gasOut)) outAtoms[kv.Key] += kv.Value;

            // Sulfur consumed out of the acid: the difference tells us the balance is doing work
            // rather than leaving the carrier untouched.
            double h2so4In = feed.OverallMassFraction("Sulfuric acid") * feed.MassFlowKgPerSecond;
            double h2so4Out = liqOut.OverallMassFraction("Sulfuric acid") * liqOut.MassFlowKgPerSecond
                            + gasOut.OverallMassFraction("Sulfuric acid") * gasOut.MassFlowKgPerSecond;

            var table = new ResultTable("BioReactor - elemental balance with a sulfur carrier");
            foreach (var el in Elements)
                table.Row(el + " atoms out = in", inAtoms[el], outAtoms[el], 1e-6, "kmol/s");

            table.RowInRange("S atoms entering (>0)", 1e-9, 1.0, inAtoms["S"], "kmol/s")
                 .RowInRange("H2SO4 consumed (>0)", 1e-9, h2so4In, h2so4In - h2so4Out, "kg/s")
                 .PrintAndThrowIfFailed();
        }

        /// <summary>Atom flows (kmol of element / s) carried by every compound in a stream.</summary>
        private static Dictionary<string, double> AtomFlows(MaterialStreamBuilder ms)
        {
            var totals = new Dictionary<string, double>();
            foreach (var el in Elements) totals[el] = 0.0;

            double massflow = ms.MassFlowKgPerSecond;
            foreach (var kv in ms.Object.Phases[0].Compounds)
            {
                var cp = kv.Value.ConstantProperties;
                if (cp == null || cp.Elements == null || cp.Molar_Weight <= 0.0) continue;
                double kmols = kv.Value.MassFraction.GetValueOrDefault() * massflow / cp.Molar_Weight;
                foreach (var el in Elements)
                    if (cp.Elements.Contains(el))
                        totals[el] += kmols * Convert.ToDouble(cp.Elements[el]);
            }
            return totals;
        }
    }
}
