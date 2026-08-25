using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DigesterModel = DWSIM.UnitOperations.Reactors.DigesterModel;
using BioThermal = DWSIM.UnitOperations.Reactors.BioReactorThermalMode;
using BiogasUpgraderTech = DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F08 - Wastewater treatment plant with biogas valorization.
    /// Organic effluent (glucose as COD surrogate) → AnaerobicDigester (Buswell black-box) →
    /// raw biogas → Cooler (drops H2O via condensation) → BiogasUpgrader (MEA Amine) →
    /// biomethane → Compressor (delivery to natural-gas grid, 50 bar).
    ///
    /// The sulfur path is the point of the H2S assertions here: the feed carries sulfate, the
    /// digester reduces it to sulfide and strips part of it into the raw biogas, and the amine
    /// upgrader takes it back out. Both ends need their H2SCompound role assigned or the H2S
    /// silently never enters the streams at all, which is what this flowsheet used to do.</summary>
    internal static class F08_BiogasUpgradingPlant
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F08_Biogas")
                .WithCompounds("Water", "Glucose", "Methane", "Carbon dioxide",
                               "Hydrogen sulfide", "Ammonia", "Biomass_ActivatedSludge")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("effluent_in")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(2.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.88)
                .SetCompoundMassFlow("Glucose", 0.10)        // ~5 % COD
                .SetCompoundMassFlow("Biomass_ActivatedSludge", 0.02)
                // Zero the rest explicitly: unset compounds do not default to zero, and stray feed
                // H2S would be indistinguishable from the H2S the digester is supposed to make.
                .SetCompoundMassFlow("Hydrogen sulfide", 0.0)
                .SetCompoundMassFlow("Methane", 0.0)
                .SetCompoundMassFlow("Carbon dioxide", 0.0)
                .SetCompoundMassFlow("Ammonia", 0.0);

            // 1) Anaerobic digester - produces biogas (CH4 + CO2 + H2S from the feed sulfate)
            var effluent = fs.AddMaterialStream("treated_effluent");
            var biogasRaw = fs.AddMaterialStream("raw_biogas");
            var ad = fs.AddAnaerobicDigester("AD-1")
                .Configure(o => o.CreateConnectors())
                .WithVolume(1500.0.CubicMeters())
                .WithHydraulicRetentionTime(20.0.Days())
                .WithCODRemoval(0.80)
                .WithBiomassYieldGVssPerGCOD(0.08)
                .WithMethaneFractionOverride(0.65)
                .WithThermalMode(BioThermal.Isothermal)
                .WithModel(DigesterModel.BlackBox)
                .WithInfluentSulfateSulfurMgPerL(600.0)
                .Configure(o =>
                {
                    o.SubstrateCompound = "Glucose";
                    o.BiomassCompound = "Biomass_ActivatedSludge";
                    o.MethaneCompound = "Methane";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.NH3Compound = "Ammonia";
                    o.H2SCompound = "Hydrogen sulfide";
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(effluent, 0)
                .ConnectProduct(biogasRaw, 1);

            // 2) Biogas cooling/drying (condenses part of the water)
            var biogasDry = fs.AddMaterialStream("dry_biogas");
            var clBiogas = fs.AddCooler("CL-BIO")
                .WithOutletTemperature(283.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(biogasRaw, 0)
                .ConnectProduct(biogasDry, 0);

            // 3) Amine upgrader - removes CO2 + H2S, produces biomethane
            var biomethane = fs.AddMaterialStream("biomethane");
            var offgas = fs.AddMaterialStream("amine_offgas");
            var bu = fs.AddBiogasUpgrader("BU-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(BiogasUpgraderTech.Amine)
                .WithCO2Removal(0.99)
                .WithH2SCompound("Hydrogen sulfide")
                .WithH2SRemoval(0.995)
                .WithH2ORemoval(0.90)
                .WithCH4LossFraction(0.001)
                .WithTargetCH4Purity(0.97)
                .ConnectFeed(biogasDry, 0)
                .ConnectProduct(biomethane, 0)
                .ConnectProduct(offgas, 1);

            // 4) Biomethane compression to grid (50 bar)
            var grid = fs.AddMaterialStream("biomethane_50bar");
            var wComp = fs.AddEnergyStream("W_comp");
            var comp = fs.AddCompressor("C-EXP")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(50e5.Pascal())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(biomethane, 0)
                .ConnectProduct(grid, 0)
                .ConnectEnergyFeed(wComp, 1);

            // Calculate UOs in topological order.
            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);

            ad.Object.SetPropertyPackageInstance(pp);
            try { ad.Object.Calculate(); }
            catch (System.ArgumentOutOfRangeException) { /* known InputConnectors[1] bug */ }

            // Regular cooler: use fs.Solve for queueable UOs
            biogasRaw.Object.SetPropertyPackageInstance(pp);
            biogasRaw.Object.Calculate(true, true);
            clBiogas.Object.PropertyPackage = pp;
            clBiogas.Object.Calculate();
            biogasDry.Object.SetPropertyPackageInstance(pp);
            biogasDry.Object.Calculate(true, true);

            bu.Object.Calculate();

            biomethane.Object.SetPropertyPackageInstance(pp);
            biomethane.Object.Calculate(true, true);
            comp.Object.PropertyPackage = pp;
            comp.Object.Calculate();

            double mFeed = feed.MassFlowKgPerSecond;
            double mEff = effluent.MassFlowKgPerSecond;
            double mBiogasRaw = biogasRaw.MassFlowKgPerSecond;
            double mBiomethane = biomethane.MassFlowKgPerSecond;

            double xCH4_grid = grid.OverallMassFraction("Methane");
            double xCO2_grid = grid.OverallMassFraction("Carbon dioxide");

            // The sulfur chain: the digester has to put H2S into the raw biogas, and the upgrader
            // has to take it back out. Asserting only the second would pass on a gas that never
            // carried any H2S to begin with.
            double xH2S_raw = biogasRaw.OverallMassFraction("Hydrogen sulfide");
            double h2sPpmv = ad.Object.Result_H2S_ppmv;

            // Judge the upgrader on the H2S it removes, not on the fraction left behind: stripping
            // CO2 and water shrinks the gas ~2.5x, which concentrates whatever H2S survives and
            // would make a fraction-based threshold look like a leak at the specified 99.5%.
            double mH2S_in = biogasDry.MassFlowKgPerSecond * biogasDry.OverallMassFraction("Hydrogen sulfide");
            double mH2S_out = mBiomethane * biomethane.OverallMassFraction("Hydrogen sulfide");
            double h2sRemoved = mH2S_in > 1e-12 ? 1.0 - mH2S_out / mH2S_in : 0.0;

            new ResultTable("F08 - WWTP biogas → grid biomethane")
                .RowInRange("AD produces biogas (>0)", 1e-6, 1.0, mBiogasRaw, "kg/s")
                .RowInRange("Biomethane generated (>0)", 1e-6, 1.0, mBiomethane, "kg/s")
                .RowInRange("Biomethane rich in CH4 (>85 %)", 0.85, 1.0, xCH4_grid, "-")
                .RowInRange("Residual CO2 in biomethane < 5 %", 0.0, 0.05, xCO2_grid, "-")
                .RowInRange("AD sours the raw biogas with H2S", 1e-9, 0.1, xH2S_raw, "-")
                .RowInRange("AD reports H2S in ppmv", 1.0, 200000.0, h2sPpmv, "ppmv")
                .Row("Upgrader removes 99.5 % of the H2S", 0.995, h2sRemoved, 0.001, "-")
                .RowInRange("Compressor delivers 50 bar", 49.9e5, 50.1e5, grid.PressurePa, "Pa")
                .RowInRange("Compressor work > 0", 0.001, 1e6, wComp.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
