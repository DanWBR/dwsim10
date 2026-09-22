using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using LightEnds = DWSIM.SharedClasses.Utilities.PetroleumCharacterization.Assay.LightEnds;
using Assay = DWSIM.SharedClasses.Utilities.PetroleumCharacterization.Assay.Assay;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The light ends of a crude assay, put together with the pseudocomponents cut from its curve.
    /// </summary>
    /// <remarks>
    /// The arithmetic is worth a test of its own because the two halves of an assay are rarely
    /// reported in the same basis: the light ends come in moles and the curve in liquid volume, so
    /// five per cent of one is not five per cent of the other. Five weight per cent of propane in a
    /// crude of 200 kg/kmol average is nineteen mole per cent, which is the size of the mistake this
    /// guards against.
    /// </remarks>
    internal static class LightEndsTest
    {
        public static void Run()
        {
            Combining();
            TheShareComesBack();
            Refusals();
            WhatCanBeALightEnd();
            WhatCanSitBelowAPlusFraction();
            TheAssayCarriesThem();
            TheSimulationCarriesTheAssay();
        }

        /// <summary>
        /// The assay has to survive a save and a load of the simulation itself, which is where the
        /// light ends were being lost: the cross-platform save never wrote the assay list, so a crude
        /// characterized outside the Windows interface came back without its curve, its contaminants
        /// or its light ends.
        /// </summary>
        private static void TheSimulationCarriesTheAssay()
        {
            var fs = Flowsheet.Create("AssayRoundTrip")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var options = (DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables)fs.Inner.FlowsheetOptions;
            options.PetroleumAssays["Light crude"] = new Assay
            {
                Name = "Light crude",
                MW = 210.0,
                API = 34.0,
                BulkSulfurWtPct = 0.42,
                LightEndsCompounds = new List<string> { "Methane", "Propane" },
                LightEndsFractions = new List<double> { 0.005, 0.018 },
                LightEndsBasis = LightEnds.MoleBasis,
                LightEndsIncludedInCurve = true
            };

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "dwsim-assay-roundtrip-" + Guid.NewGuid().ToString("N") + ".dwxmz");

            try
            {
                fs.Save(path);
                var reloaded = Flowsheet.Load(path);
                var back = (DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables)reloaded.Inner.FlowsheetOptions;

                var assay = back.PetroleumAssays.Values.FirstOrDefault(a => a.Name == "Light crude");

                new ResultTable("An assay saved with the simulation")
                    .Row("the assay came back", 1.0, assay == null ? 0.0 : 1.0, 0.0)
                    .Row("with its bulk sulfur", 0.42, assay?.BulkSulfurWtPct ?? 0.0, 1e-12, "wt %")
                    .Row("and both light ends", 2, assay?.LightEndsCompounds?.Count ?? 0, 0.0)
                    .Row("with the propane fraction", 0.018,
                         assay?.LightEndsFractions != null && assay.LightEndsFractions.Count > 1
                             ? assay.LightEndsFractions[1] : 0.0, 1e-12)
                    .Row("the basis", 1.0, assay?.LightEndsBasis == LightEnds.MoleBasis ? 1.0 : 0.0, 0.0)
                    .Row("and the curve flag", 1.0, assay != null && assay.LightEndsIncludedInCurve ? 1.0 : 0.0, 0.0)
                    .PrintAndThrowIfFailed();
            }
            finally
            {
                try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch (Exception) { }
            }
        }

        /// <summary>
        /// A light end is a compound that boils below the front of the curve. Anything heavier named
        /// here is material the curve already carries, and counting it on both sides inflates the crude.
        /// </summary>
        private static void WhatCanBeALightEnd()
        {
            var names = new List<string> { "Methane", "Propane", "N-hexane", "N-decane", "PSEUDO-1", "Propane", "Exotic" };
            var nbp = new List<double> { 111.7, 231.1, 341.9, 447.3, 500.0, 231.1, 0.0 };
            var pseudo = new List<bool> { false, false, false, false, true, false, false };

            var issues = LightEnds.Validate(names, nbp, pseudo);

            var blocking = issues.Where(i => i.Blocking).Select(i => i.Compound).ToList();
            var warnings = issues.Where(i => !i.Blocking).Select(i => i.Compound).ToList();

            new ResultTable("What can be declared as a light end")
                .Row("methane and propane pass", 0.0,
                     issues.Count(i => i.Compound == "Methane") + issues.Count(i => i.Compound == "Propane" && !i.Blocking), 0.0)
                .Row("n-hexane is a warning, not a refusal", 1.0,
                     warnings.Count(c => c == "N-hexane"), 0.0)
                .Row("n-decane is refused", 1.0, blocking.Count(c => c == "N-decane"), 0.0)
                .Row("a pseudocomponent is refused", 1.0, blocking.Count(c => c == "PSEUDO-1"), 0.0)
                .Row("the repeated propane is refused", 1.0, blocking.Count(c => c == "Propane"), 0.0)
                .Row("a compound with no boiling point is a warning", 1.0, warnings.Count(c => c == "Exotic"), 0.0)
                .Row("the blocking message names them", 1.0,
                     LightEnds.BlockingMessage(issues).Contains("N-decane") ? 1.0 : 0.0, 0.0)
                .Row("a clean list has nothing to say", 0.0,
                     LightEnds.Validate(new List<string> { "Methane", "Ethane", "Isobutane" },
                                        new List<double> { 111.7, 184.6, 261.4 },
                                        new List<bool> { false, false, false }).Count, 0.0)
                .Row("and nothing blocking", 0.0,
                     LightEnds.BlockingMessage(LightEnds.Validate(new List<string> { "Methane" },
                                        new List<double> { 111.7 }, new List<bool> { false })).Length, 0.0)
                .PrintAndThrowIfFailed();
        }

        private static void Combining()
        {
            // one light end and one cut, so the numbers can be followed by hand
            var f = new[] { 0.05 };
            var lightMW = new[] { 44.1 };     // propane
            var lightSG = new[] { 0.507 };
            var z = new[] { 1.0 };
            var pseudoMW = new[] { 200.0 };
            var pseudoSG = new[] { 0.85 };

            double[] light = null, pseudo = null;

            LightEnds.Combine(f, lightMW, lightSG, LightEnds.MoleBasis, z, pseudoMW, pseudoSG, ref light, ref pseudo);
            var mole = new[] { light[0], pseudo[0] };

            LightEnds.Combine(f, lightMW, lightSG, LightEnds.MassBasis, z, pseudoMW, pseudoSG, ref light, ref pseudo);
            var mass = new[] { light[0], pseudo[0] };

            LightEnds.Combine(f, lightMW, lightSG, LightEnds.VolumeBasis, z, pseudoMW, pseudoSG, ref light, ref pseudo);
            var volume = new[] { light[0], pseudo[0] };

            // three cuts sharing what the light ends leave, in the proportions the curve gave them
            var manyZ = new[] { 0.5, 0.3, 0.2 };
            LightEnds.Combine(new[] { 0.02, 0.03 }, new[] { 16.04, 30.07 }, new[] { 0.3, 0.356 },
                LightEnds.MoleBasis, manyZ, new[] { 100.0, 200.0, 300.0 }, new[] { 0.7, 0.8, 0.9 },
                ref light, ref pseudo);

            new ResultTable("Light ends and the cuts they sit in front of")
                .Row("5 mol% stays 5 mol%", 0.05, mole[0], 1e-12)
                .Row("and the cut takes the rest", 0.95, mole[1], 1e-12)
                .Row("5 wt% of propane is 19.3 mol%", 0.1926967916, mass[0], 1e-9)
                .Row("with the cut at 80.7 mol%", 0.8073032084, mass[1], 1e-9)
                .Row("5 vol% of propane is 12.5 mol%", 0.1246289707, volume[0], 1e-9)
                .Row("with the cut at 87.5 mol%", 0.8753710293, volume[1], 1e-9)
                .Row("two light ends take 5 mol%", 0.05, light.Sum(), 1e-12)
                .Row("the first cut keeps its half of the rest", 0.5 * 0.95, pseudo[0], 1e-12)
                .Row("everything still sums to one", 1.0, light.Sum() + pseudo.Sum(), 1e-12)
                .PrintAndThrowIfFailed();
        }

        /// <summary>
        /// Combining and then measuring the share back in the same basis has to return what was
        /// declared. This is what places the first cut when the curve already covers the light ends.
        /// </summary>
        private static void TheShareComesBack()
        {
            var f = new[] { 0.015, 0.02, 0.01 };
            var lightMW = new[] { 16.04, 30.07, 44.10 };
            var lightSG = new[] { 0.300, 0.356, 0.507 };
            var z = new[] { 0.4, 0.35, 0.25 };
            var pseudoMW = new[] { 110.0, 220.0, 380.0 };
            var pseudoSG = new[] { 0.74, 0.83, 0.92 };

            var table = new ResultTable("The declared share survives the round trip");

            foreach (var basis in LightEnds.Bases)
            {
                double[] light = null, pseudo = null;
                LightEnds.Combine(f, lightMW, lightSG, basis, z, pseudoMW, pseudoSG, ref light, ref pseudo);
                var back = LightEnds.ShareInBasis(light, lightMW, lightSG, pseudo, pseudoMW, pseudoSG, basis);
                table.Row("declared 4.5 % on the " + basis.ToLower() + " basis", 0.045, back, 1e-12);
                table.Row("and the whole crude sums to one (" + basis.ToLower() + ")", 1.0,
                          light.Sum() + pseudo.Sum(), 1e-12);
            }

            table.PrintAndThrowIfFailed();
        }

        /// <summary>
        /// The same question for a reservoir fluid, where the line is not the boiling point of the
        /// pentanes but the molar weight the plus fraction starts at. A condensate analysis lists the
        /// heptanes and the octanes one by one in front of a C10+ fraction, and those are not light
        /// ends by any reading.
        /// </summary>
        private static void WhatCanSitBelowAPlusFraction()
        {
            var names = new List<string> { "Nitrogen", "Methane", "N-heptane", "N-decane", "PSEUDO-1", "Exotic" };
            var mw = new List<double> { 28.01, 16.04, 100.2, 142.3, 250.0, 0.0 };
            var pseudo = new List<bool> { false, false, false, false, true, false };

            // a C10+ fluid: the plus fraction starts at 134 kg/kmol, so n-heptane is a defined
            // compound and n-decane is inside the pseudocomponents
            var issues = LightEnds.ValidateAgainstPlusFraction(names, mw, pseudo, 134.0);

            var blocking = issues.Where(i => i.Blocking).Select(i => i.Compound).ToList();
            var warnings = issues.Where(i => !i.Blocking).Select(i => i.Compound).ToList();

            // the same n-heptane against a C7+ fluid, where the plus fraction starts at 90
            var c7 = LightEnds.ValidateAgainstPlusFraction(
                new List<string> { "N-heptane" }, new List<double> { 100.2 }, new List<bool> { false }, 90.0);

            new ResultTable("What can sit below a plus fraction")
                .Row("nitrogen and methane pass", 0.0,
                     issues.Count(i => i.Compound == "Nitrogen" || i.Compound == "Methane"), 0.0)
                .Row("n-heptane passes below a C10+ fraction", 0.0,
                     issues.Count(i => i.Compound == "N-heptane"), 0.0)
                .Row("and is refused below a C7+ one", 1.0, c7.Count(i => i.Blocking), 0.0)
                .Row("n-decane is refused", 1.0, blocking.Count(c => c == "N-decane"), 0.0)
                .Row("a pseudocomponent is refused", 1.0, blocking.Count(c => c == "PSEUDO-1"), 0.0)
                .Row("a compound with no molar weight is a warning", 1.0, warnings.Count(c => c == "Exotic"), 0.0)
                .Row("the message says where the fraction starts", 1.0,
                     LightEnds.BlockingMessage(issues).Contains("134.0") ? 1.0 : 0.0, 0.0)
                .PrintAndThrowIfFailed();
        }

        private static void Refusals()
        {
            var z = new[] { 1.0 };
            var mw = new[] { 200.0 };
            var sg = new[] { 0.85 };
            double[] light = null, pseudo = null;

            Refuses("light ends that leave no crude", () =>
                LightEnds.Combine(new[] { 0.6, 0.5 }, new[] { 16.0, 30.0 }, new[] { 0.3, 0.36 },
                    LightEnds.MoleBasis, z, mw, sg, ref light, ref pseudo));

            Refuses("a negative fraction", () =>
                LightEnds.Combine(new[] { -0.01 }, new[] { 16.0 }, new[] { 0.3 },
                    LightEnds.MoleBasis, z, mw, sg, ref light, ref pseudo));

            Refuses("a basis nobody knows", () =>
                LightEnds.Combine(new[] { 0.05 }, new[] { 16.0 }, new[] { 0.3 },
                    "Barrels", z, mw, sg, ref light, ref pseudo));

            Refuses("a missing molar weight on the mass basis", () =>
                LightEnds.Combine(new[] { 0.05 }, new[] { 0.0 }, new[] { 0.3 },
                    LightEnds.MassBasis, z, mw, sg, ref light, ref pseudo));

            Console.WriteLine("Light ends: the four bad inputs were all refused.");
        }

        private static void Refuses(string what, Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("Light ends: " + what + " was accepted, and it should not have been.");
        }

        /// <summary>The assay has to carry the light ends through a save and a load.</summary>
        private static void TheAssayCarriesThem()
        {
            var assay = new Assay
            {
                Name = "Light ends round trip",
                LightEndsCompounds = new List<string> { "Methane", "Ethane", "Propane" },
                LightEndsFractions = new List<double> { 0.015, 0.02, 0.01 },
                LightEndsBasis = LightEnds.MoleBasis,
                LightEndsIncludedInCurve = true
            };

            var copy = (Assay)assay.Clone();

            new ResultTable("An assay saved and read back")
                .Row("light ends listed", 3, copy.LightEndsCompounds.Count, 0.0)
                .Row("fractions listed", 3, copy.LightEndsFractions.Count, 0.0)
                .Row("the second fraction", 0.02, copy.LightEndsFractions[1], 1e-12)
                .Row("the basis survived", 1.0, copy.LightEndsBasis == LightEnds.MoleBasis ? 1.0 : 0.0, 0.0)
                .Row("the last compound survived", 1.0, copy.LightEndsCompounds[2] == "Propane" ? 1.0 : 0.0, 0.0)
                .Row("the curve flag survived", 1.0, copy.LightEndsIncludedInCurve ? 1.0 : 0.0, 0.0)
                .PrintAndThrowIfFailed();
        }
    }
}
