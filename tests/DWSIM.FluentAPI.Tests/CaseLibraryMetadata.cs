using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The flowsheet metadata (process type, description, key compounds, reactants and products) of
    /// each case saved for the dwsim-case-library, by case name. CaseLibraryOutput.Emit applies it.
    /// </summary>
    internal static class CaseLibraryMetadata
    {
        public static void Apply(Flowsheet fs, string caseName)
        {
            switch (caseName)
            {
                case "ammonia-synthesis-single-pass":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transformation,
                        "A simplified Haber-Bosch single pass: a 3:1 hydrogen and nitrogen makeup gas is compressed to 200 bar, brought to equilibrium in a converter, chilled and separated.",
                        new[] { "Nitrogen", "Hydrogen", "Ammonia" }, new[] { "Nitrogen", "Hydrogen" }, new[] { "Ammonia" });
                    break;
                case "benzene-toluene-distillation":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "An equimolar benzene and toluene feed is preheated against hot water and split in a 30-stage column into high purity benzene and toluene.",
                        new[] { "Benzene", "Toluene" }, null, null);
                    break;
                case "biogas-to-grid":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transformation,
                        "An organic effluent is digested anaerobically; the biogas is upgraded in an amine unit and the biomethane is compressed to 50 bar for grid injection.",
                        new[] { "Glucose", "Methane", "Carbon dioxide", "Hydrogen sulfide" }, new[] { "Glucose" }, new[] { "Methane" });
                    break;
                case "compressor-performance-map":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transportation,
                        "A fuel gas booster compressor described by the head and efficiency map measured by its maker at three speeds.",
                        new[] { "Methane", "Ethane" }, null, null);
                    break;
                case "copolymer-devolatilization":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "A poly(ethylene-co-propylene) solution in n-pentane is heated and flashed under vacuum to strip the solvent from the copolymer.",
                        new[] { "N-pentane", "Poly(ethylene-co-propylene)" }, null, null);
                    break;
                case "straight-run-distillate-curves":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "The distillate part of a medium crude is fractionated into naphtha, kerosene, diesel and a light gas oil, with the distillation curves of the products.",
                        null, null, null);
                    break;
                case "straight-run-distillate-secondary-specs":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "A straight-run distillate column converged on product quality: the D86 95 % points of the kerosene and the diesel are secondary specifications.",
                        null, null, null);
                    break;
                case "dosing-pump-flow-control-dynamics":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transportation,
                        "A positive displacement dosing pump delivers an additive into a process line, with a flow controller acting on its speed.",
                        new[] { "Water" }, null, null);
                    break;
                case "ethanol-distillery":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transformation,
                        "A glucose mash is fermented to wine, degassed and distilled to hydrous ethanol.",
                        new[] { "Glucose", "Ethanol", "Carbon dioxide", "Water" }, new[] { "Glucose" }, new[] { "Ethanol", "Carbon dioxide" });
                    break;
                case "turboexpander-performance-map":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Unspecified,
                        "A city-gate letdown turboexpander described by the power measured by its maker at three speeds, with the gas reheated by warm water.",
                        new[] { "Methane" }, null, null);
                    break;
                case "green-hydrogen-solar-electrolysis":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transformation,
                        "A solar panel array powers a water electrolyzer that splits water into hydrogen and oxygen.",
                        new[] { "Water", "Hydrogen", "Oxygen" }, new[] { "Water" }, new[] { "Hydrogen", "Oxygen" });
                    break;
                case "hydroelectric-heat-recovery":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Unspecified,
                        "A run-of-river hydroelectric turbine whose tailrace water then serves as the cold sink of a heat exchanger cooling a hot process stream.",
                        new[] { "Water" }, null, null);
                    break;
                case "methanol-synthesis-syngas":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transformation,
                        "Syngas is compressed and brought to equilibrium in a methanol converter; the crude methanol is flashed, degassed and distilled to a 99.8 mol% product.",
                        new[] { "Hydrogen", "Carbon monoxide", "Carbon dioxide", "Methanol" }, new[] { "Hydrogen", "Carbon monoxide", "Carbon dioxide" }, new[] { "Methanol", "Water" });
                    break;
                case "natural-gas-dew-point-control":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "A Joule-Thomson dew point control unit: the gas is pre-chilled against the cold sales gas, expanded across a JT valve, separated from its NGL and recompressed.",
                        new[] { "Methane", "Ethane", "Propane", "N-butane", "N-pentane" }, null, null);
                    break;
                case "peg-dewatering":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "A poly(ethylene glycol) solution in water is concentrated by flashing off water under a mild vacuum.",
                        new[] { "Water" }, null, null);
                    break;
                case "polymer-cloud-point":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "A polypropylene solution in n-pentane splits into a polymer-rich and an almost pure solvent liquid as it approaches the solvent's critical region.",
                        new[] { "N-pentane", "Polypropylene" }, null, null);
                    break;
                case "polymer-devolatilization":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "A polystyrene solution in ethylbenzene, the effluent of a solution polymerization reactor, is stripped of its solvent under vacuum.",
                        new[] { "Ethylbenzene", "Polystyrene" }, null, null);
                    break;
                case "propane-refrigeration-cycle":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.RefrigerationCycle,
                        "A closed propane (R-290) vapour-compression refrigeration cycle: compressor, condenser, expansion valve and evaporator, the loop closed by a recycle block.",
                        new[] { "Propane" }, null, null);
                    break;
                case "pump-variable-frequency-drive":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transportation,
                        "A cooling water transfer pump with the manufacturer's curves at two speeds, running at an intermediate speed on its variable frequency drive.",
                        new[] { "Water" }, null, null);
                    break;
                case "steam-methane-reforming-h2":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transformation,
                        "Natural gas and steam are reformed to equilibrium, shifted in a second reactor and cooled to a hydrogen-rich syngas.",
                        new[] { "Methane", "Water", "Hydrogen", "Carbon monoxide", "Carbon dioxide" }, new[] { "Methane", "Water" }, new[] { "Hydrogen", "Carbon dioxide" });
                    break;
                case "styrene-polymerization":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.Transformation,
                        "A free-radical polymerization reactor turns part of a styrene feed into polystyrene, and the unreacted monomer is recovered by devolatilization.",
                        new[] { "Ethylbenzene", "Polystyrene" }, null, null);
                    break;
                case "benzene-toluene-column-dynamics":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "A 16-stage benzene-toluene column in dynamic mode, with tray hydraulics, level controllers on the reflux drum and the sump and a pressure controller on the condenser.",
                        new[] { "Benzene", "Toluene" }, null, null);
                    break;
                case "benzene-toluene-column-shutdown":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "The benzene-toluene column in dynamic mode is shut down from its design steady state.",
                        new[] { "Benzene", "Toluene" }, null, null);
                    break;
                case "benzene-toluene-column-startup":
                    fs.WithMetadata(DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation,
                        "The benzene-toluene column in dynamic mode is started from empty, cold and at atmospheric pressure.",
                        new[] { "Benzene", "Toluene" }, null, null);
                    break;
            }
        }
    }
}
