using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DigesterModel = DWSIM.UnitOperations.Reactors.DigesterModel;
using BioThermal = DWSIM.UnitOperations.Reactors.BioReactorThermalMode;
using BiogasUpgraderTech = DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Biogas-to-grid plant: anaerobic digestion + upgrading.
    /// Organic effluent (glucose as COD surrogate) → anaerobic digester (Buswell
    /// black box, sulfate reduced to H2S) → cooler (drops water) → amine upgrader
    /// (removes CO2 + H2S) → compressor (50 bar, gas-grid delivery).
    /// Checks: biogas composition, biomethane purity, the H2S path end to end,
    /// delivery pressure.</summary>
    internal static class BiogasToGridSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("BiogasToGrid")
                .WithCompounds("Water", "Glucose", "Methane", "Carbon dioxide",
                               "Hydrogen sulfide", "Ammonia")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("organic effluent")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(2.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.90)
                .SetCompoundMassFlow("Glucose", 0.10)
                .SetCompoundMassFlow("Hydrogen sulfide", 0.0)
                .SetCompoundMassFlow("Methane", 0.0)
                .SetCompoundMassFlow("Carbon dioxide", 0.0)
                .SetCompoundMassFlow("Ammonia", 0.0);

            var effluent = fs.AddMaterialStream("treated effluent");
            var biogasRaw = fs.AddMaterialStream("raw biogas");
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
                    o.MethaneCompound = "Methane";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.NH3Compound = "Ammonia";
                    o.H2SCompound = "Hydrogen sulfide";
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(effluent, 0)
                .ConnectProduct(biogasRaw, 1);

            var biogasDry = fs.AddMaterialStream("dry biogas");
            fs.AddCooler("CL-1")
                .WithOutletTemperature(283.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(biogasRaw, 0)
                .ConnectProduct(biogasDry, 0);

            var biomethane = fs.AddMaterialStream("biomethane");
            var offgas = fs.AddMaterialStream("upgrader offgas");
            fs.AddBiogasUpgrader("BU-1")
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

            var grid = fs.AddMaterialStream("grid gas 50 bar");
            var wComp = fs.AddEnergyStream("W comp");
            fs.AddCompressor("C-1")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(50e5.Pascal())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(biomethane, 0)
                .ConnectProduct(grid, 0)
                .ConnectEnergyFeed(wComp, 1);

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            double mBiogasRaw = biogasRaw.MassFlowKgPerSecond;
            double mBiomethane = biomethane.MassFlowKgPerSecond;

            // The sulfur chain end to end: the digester has to put H2S into the raw
            // biogas, and the upgrader has to take it back out. Judged on removed mass,
            // not residual fraction, because stripping CO2 concentrates what is left.
            double xH2S_raw = biogasRaw.OverallMassFraction("Hydrogen sulfide");
            double mH2S_in = biogasDry.MassFlowKgPerSecond * biogasDry.OverallMassFraction("Hydrogen sulfide");
            double mH2S_out = mBiomethane * biomethane.OverallMassFraction("Hydrogen sulfide");
            double h2sRemoved = mH2S_in > 1e-12 ? 1.0 - mH2S_out / mH2S_in : 0.0;

            new ResultTable("Biogas-to-grid (digester + amine upgrader)")
                .RowInRange("Digester produces biogas (> 0)", 1e-6, 1.0, mBiogasRaw, "kg/s")
                .RowInRange("Raw biogas CH4 40-75 mol%", 0.40, 0.75, biogasRaw.OverallMoleFraction("Methane"), "-")
                .RowInRange("Biomethane CH4 > 85 wt%", 0.85, 1.0, grid.OverallMassFraction("Methane"), "-")
                .RowInRange("Residual CO2 in biomethane < 5 wt%", 0.0, 0.05, grid.OverallMassFraction("Carbon dioxide"), "-")
                .RowInRange("Digester sours the biogas with H2S", 1e-9, 0.1, xH2S_raw, "-")
                .Row("Upgrader removes 99.5 % of the H2S", 0.995, h2sRemoved, 0.001, "-")
                .RowInRange("Grid delivery at 50 bar", 49.9e5, 50.1e5, grid.PressurePa, "Pa")
                .RowInRange("Compressor work > 0", 0.001, 1e6, wComp.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "biogas-to-grid");
        }
    }
}
