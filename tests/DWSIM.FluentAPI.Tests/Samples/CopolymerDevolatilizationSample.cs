using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DWSIM.Automation.FluentAPI;
using DWSIM.Thermodynamics.AdvancedEOS;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Copolymer devolatilization: stripping the solvent from a poly(ethylene-co-propylene) solution.
    /// The copolymer is an ethylene/propylene chain (a random 50/50 EPR/EPM rubber, Mn = 50 000 g/mol) dissolved
    /// in n-pentane. PC-SAFT builds the copolymer from its two segment types (each segment reuses the parent
    /// homopolymer parameters, ethylene from polyethylene and propylene from polypropylene), so its solution
    /// thermodynamics sit between the two homopolymers. Like any polymer it is non-volatile: heated to 420 K
    /// under vacuum the n-pentane flashes off and the copolymer stays as a concentrated melt.
    /// Property package: PC-SAFT with the segment-based copolymer model (Gross et al., 2003).
    /// Checks: mass balance, the vapour is pure solvent (no polymer), and the melt is a concentrated copolymer.</summary>
    internal static class CopolymerDevolatilizationSample
    {
        private static string AddcompsDir([CallerFilePath] string sourceFile = "")
        {
            var dir = Path.GetDirectoryName(sourceFile);
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "content", "addcomps"));
        }

        public static void Run()
        {
            const string copolyCas = "EPR-5050";               // synthetic id for this copolymer grade
            const double Mn = 50000.0;

            // Build the copolymer compound from the polyethylene template (any polymer JSON works; the segment
            // parameters come from the copolymer definition below, not from this compound's own row).
            var cp = Newtonsoft.Json.JsonConvert.DeserializeObject<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>(
                File.ReadAllText(Path.Combine(AddcompsDir(), "Polyethylene_HDPE.json")));
            cp.CurrentDB = "User"; cp.OriginalDB = "User";
            cp.CAS_Number = copolyCas;
            cp.Name = "Poly(ethylene-co-propylene)";
            cp.Molar_Weight = Mn;

            var fs = Flowsheet.Create("CopolymerDevolatilization")
                .WithCompounds("N-pentane")
                .WithCompound(cp)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            // Register the copolymer's segment definition on the PC-SAFT package: 50 wt% ethylene segments
            // (from polyethylene, CAS 9002-88-4) and 50 wt% propylene segments (from polypropylene, 9003-07-0),
            // random sequence.
            foreach (var pp in fs.Inner.PropertyPackages.Values.OfType<PCSAFT2PropertyPackage>())
                pp.CompoundParameters[copolyCas] = new PCSParam
                {
                    casno = copolyCas,
                    compound = cp.Name,
                    mw = Mn,
                    m_over_M = 0.0247,                          // ethylene/propylene average, flags it a polymer
                    copolymer = "9002-88-4:0.5;9003-07-0:0.5",
                    coseq = "random"
                };

            var feed = fs.AddMaterialStream("copolymer solution")
                .At(320.0.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("N-pentane", 0.70)
                .SetCompoundMassFlow("Poly(ethylene-co-propylene)", 0.30);

            var hotFeed = fs.AddMaterialStream("heated feed");
            fs.AddHeater("H-1")
                .WithOutletTemperature(420.0.Kelvin())
                .WithPressureDrop((101325.0 - 15000.0).Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(hotFeed, 0);

            var solventVapor = fs.AddMaterialStream("solvent vapour");
            var copolymerMelt = fs.AddMaterialStream("copolymer melt");
            fs.AddSeparator("V-1")
                .ConnectFeed(hotFeed, 0)
                .ConnectProduct(solventVapor, 0)
                .ConnectProduct(copolymerMelt, 1);

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            const string cpName = "Poly(ethylene-co-propylene)";
            double meltCopoly = copolymerMelt.OverallMassFraction(cpName);
            double vaporCopoly = solventVapor.OverallMassFraction(cpName);
            double vaporSolvent = solventVapor.OverallMassFraction("N-pentane");

            new ResultTable("Poly(ethylene-co-propylene) devolatilization (PC-SAFT copolymer model)")
                .Row("Mass balance F = vapour + melt", feed.MassFlowKgPerSecond,
                     solventVapor.MassFlowKgPerSecond + copolymerMelt.MassFlowKgPerSecond, 0.005, "kg/s")
                .RowInRange("Vapour is pure n-pentane (>99.9 wt%)", 0.999, 1.0, vaporSolvent, "-")
                .RowInRange("No copolymer in the vapour (<1 ppm)", 0.0, 1e-6, vaporCopoly, "-")
                .RowInRange("Melt is a concentrated copolymer (>85 wt%)", 0.85, 1.0, meltCopoly, "-")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "copolymer-devolatilization");
        }
    }
}
