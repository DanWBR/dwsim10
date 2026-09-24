using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Extensions.AI.Assistant;
using Newtonsoft.Json.Linq;
using DWSIM.Interfaces;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Checks that the flowsheet diagnostics name the faults a half-built flowsheet actually has,
    /// and stay quiet about a good one.
    /// </summary>
    /// <remarks>
    /// The second half matters more than the first. A rule that fires on a working flowsheet sends
    /// whoever reads it chasing a problem that is not there, and one false blocker costs more trust
    /// than ten missed warnings.
    /// </remarks>
    internal static class FlowsheetDiagnosticsTest
    {
        public static void Run()
        {
            AGoodFlowsheetIsQuiet();
            AnEmptyFlowsheetIsBlocked();
            FaultsAreNamed();
            FailuresAreExplained();
            TheJsonContractHolds();
            EveryCodeIsExplained();
            DegreesOfFreedomAreCounted();
            ImplausibleResultsAreFlagged();
            APumpRefusesVapourAndStaleResultsAreFlagged();
        }

        /// <summary>
        /// A code without a written explanation shows a student a bare identifier. The catalogue
        /// and the explanations are kept in step here, so adding a code means writing it up.
        /// </summary>
        private static void EveryCodeIsExplained()
        {
            foreach (var code in FlowsheetCodes.All.Keys)
            {
                FindingExplanation explanation;
                if (!FindingExplanations.All.TryGetValue(code, out explanation))
                    throw new Exception("No explanation is written for " + code + ".");

                foreach (var part in new[] { explanation.Title, explanation.Meaning, explanation.Why, explanation.HowToFix })
                {
                    if (string.IsNullOrWhiteSpace(part))
                        throw new Exception("The explanation of " + code + " has an empty part.");
                }

                var url = FindingExplanations.LearnMoreUrl(code);
                if (!url.StartsWith(FindingExplanations.TutorialsRoot + "en/"))
                    throw new Exception("The learn-more link of " + code + " is not on the tutorials site: " + url);

                var ptbr = FindingExplanations.LearnMoreUrl(code, "pt-BR");
                if (!ptbr.Contains("/pt-BR/"))
                    throw new Exception("The Portuguese learn-more link of " + code + " is not localised: " + ptbr);
            }

            // A code nobody wrote up still explains itself from the one-liner.
            var unknown = FindingExplanations.For("NO_SUCH_CODE");
            if (unknown == null || string.IsNullOrEmpty(unknown.Meaning))
                throw new Exception("An unknown code produced no explanation.");

            Console.WriteLine();
            Console.WriteLine("Every one of the " + FlowsheetCodes.All.Count + " codes is explained.");
        }

        /// <summary>
        /// The degrees of freedom of a feed, a mixer and two heaters: one told what to do and one
        /// left in a mode whose value was never given.
        /// </summary>
        private static void DegreesOfFreedomAreCounted()
        {
            var fs = Flowsheet.Create("DiagnosticsDof")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(300.Kelvin(), 200000.0.Pascal())
                .WithMassFlow(10.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));

            var warm = fs.AddMaterialStream("warm");
            var cold = fs.AddMaterialStream("cold");

            fs.AddHeater("H-set")
                .WithOutletTemperature(350.Kelvin())
                .ConnectFeed(feed, 0)
                .ConnectProduct(warm, 0);

            // Born in Heat Removed mode with no duty: one degree of freedom left open.
            fs.AddCooler("C-empty")
                .ConnectFeed(warm, 0)
                .ConnectProduct(cold, 0);

            fs.AutoLayout();

            var dof = DegreesOfFreedomAnalysis.Analyze(fs.Inner);
            Console.WriteLine();
            Console.WriteLine("Degrees of freedom: " + dof.Remaining + " remaining, " + dof.Unsupported + " unsupported");
            foreach (var o in dof.Objects)
            {
                Console.WriteLine("  " + o.ObjectTag + " (" + o.ObjectType + ", " + o.Mode + "): " +
                    o.Specified + "/" + o.Required + " set");
                foreach (var slot in o.Slots) Console.WriteLine("      " + slot);
            }

            var feedDof = dof.Objects.First(o => o.ObjectTag == "feed");
            if (feedDof.Required != 4 || feedDof.Remaining != 0)
                throw new Exception("A fully specified feed should need 4 values and lack none; got " +
                    feedDof.Specified + "/" + feedDof.Required + ".");

            var warmDof = dof.Objects.First(o => o.ObjectTag == "warm");
            if (warmDof.Required != 0)
                throw new Exception("A computed stream has no specifications to give.");

            var heaterDof = dof.Objects.First(o => o.ObjectTag == "H-set");
            if (heaterDof.Mode != "OutletTemperature" || heaterDof.Remaining != 0)
                throw new Exception("A heater given its outlet temperature is fully specified; got " + heaterDof.Remaining + " remaining.");
            if (!heaterDof.Slots.Any(s => s.Name == "Outlet temperature" && s.IsSet && Math.Abs(s.Value.GetValueOrDefault() - 350.0) < 1e-6))
                throw new Exception("The outlet temperature slot of the heater does not carry 350 K.");

            var coolerDof = dof.Objects.First(o => o.ObjectTag == "C-empty");
            if (coolerDof.Remaining != 1 || !coolerDof.Missing.Any(s => s.Property == "DeltaQ"))
                throw new Exception("A cooler in duty mode with no duty has one degree of freedom open, on the duty.");

            if (dof.Objects[0].ObjectTag != "C-empty")
                throw new Exception("Objects with open degrees of freedom come first.");

            // The same hole is a finding, so a student who only reads the check list still sees it.
            var findings = FlowsheetDiagnostics.Check(fs.Inner);
            Report("A flowsheet with an unspecified cooler", findings);
            RequireCode(findings, FlowsheetCodes.SpecMissing, "C-empty");
            if (findings.Any(f => f.ObjectTag == "H-set"))
                throw new Exception("The fully specified heater was reported: " + findings.First(f => f.ObjectTag == "H-set"));
        }

        /// <summary>
        /// Results the arithmetic accepts and a process would not: a heater with a negative duty,
        /// a pump that lowers the pressure, an efficiency entered as a fraction.
        /// </summary>
        private static void ImplausibleResultsAreFlagged()
        {
            var fs = Flowsheet.Create("DiagnosticsPlausibility")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(350.Kelvin(), 500000.0.Pascal())
                .WithMassFlow(10.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));

            var cooled = fs.AddMaterialStream("cooled");
            var lowP = fs.AddMaterialStream("lowP");
            var warmed = fs.AddMaterialStream("warmed");

            // A heater told to remove heat.
            fs.AddHeater("H-negative")
                .WithHeatAdded((-100.0).Kilowatts())
                .ConnectFeed(feed, 0)
                .ConnectProduct(cooled, 0);

            // A pump told to lower the pressure, with an efficiency typed as a fraction.
            fs.AddPump("P-backwards")
                .WithOutletPressure(300000.0.Pascal())
                .WithEfficiencyPercent(0.75)
                .ConnectFeed(cooled, 0)
                .ConnectProduct(lowP, 0);

            // A heater doing its job, which must stay out of the report.
            fs.AddHeater("H-fine")
                .WithHeatAdded(100.0.Kilowatts())
                .ConnectFeed(lowP, 0)
                .ConnectProduct(warmed, 0);

            fs.AutoLayout();

            var before = FlowsheetDiagnostics.Check(fs.Inner);
            Report("Before solving", before);
            RequireCode(before, FlowsheetCodes.EfficiencyOutOfRange, "P-backwards");

            var errors = fs.TrySolve();
            var after = FlowsheetDiagnostics.Diagnose(fs.Inner, errors);
            Report("After solving", after);

            RequireCode(after, FlowsheetCodes.HeaterCooled, "H-negative");
            RequireCode(after, FlowsheetCodes.PressureWrongDirection, "P-backwards");

            if (after.Any(f => f.ObjectTag == "H-fine"))
                throw new Exception("A heater that heated was reported: " + after.First(f => f.ObjectTag == "H-fine"));
        }

        /// <summary>
        /// The shape the assistant and the MCP tools hand a language model. It is a contract: a
        /// model that learned to read <c>fix</c> has to keep finding it there.
        /// </summary>
        private static void TheJsonContractHolds()
        {
            var fs = Flowsheet.Create("DiagnosticsJson")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            fs.AddMaterialStream("orphan");
            fs.AutoLayout();

            var json = FlowsheetChecks.Check(fs.Inner);
            Console.WriteLine();
            Console.WriteLine("The check response:");
            Console.WriteLine(json.ToString());

            foreach (var field in new[] { "ready", "blockers", "warnings", "findings", "object_count" })
            {
                if (json[field] == null) throw new Exception("The check response has no '" + field + "'.");
            }

            if (json["ready"].ToObject<bool>())
                throw new Exception("A flowsheet with a dangling stream reported itself ready.");

            var findings = (JArray)json["findings"];
            if (findings.Count == 0) throw new Exception("The check response carries no findings.");

            foreach (var finding in findings)
            {
                foreach (var field in new[] { "code", "severity", "object", "message", "fix" })
                {
                    if (finding[field] == null)
                        throw new Exception("A finding has no '" + field + "': " + finding);
                }

                if (string.IsNullOrEmpty(finding["fix"].ToString()))
                    throw new Exception("A finding carries an empty fix: " + finding);
            }
        }

        /// <summary>The one that must not cry wolf: a solved flowsheet has nothing to report.</summary>
        private static void AGoodFlowsheetIsQuiet()
        {
            var fs = Flowsheet.Create("DiagnosticsClean")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var inlet1 = fs.AddMaterialStream("inlet1")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(100.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));

            var inlet2 = fs.AddMaterialStream("inlet2")
                .At(350.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(50.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));

            var outlet = fs.AddMaterialStream("outlet");

            fs.AddMixer("MIX-1")
                .ConnectFeed(inlet1, 0)
                .ConnectFeed(inlet2, 1)
                .ConnectProduct(outlet, 0);

            fs.AutoLayout();

            var before = FlowsheetDiagnostics.Check(fs.Inner);
            Report("A clean flowsheet, before solving", before);

            if (before.Count > 0)
            {
                throw new Exception("A well-formed flowsheet produced " + before.Count +
                    " finding(s); it should produce none. First: " + before[0]);
            }

            fs.Solve();

            var after = FlowsheetDiagnostics.Diagnose(fs.Inner, new Exception[0]);
            Report("A clean flowsheet, after solving", after);

            if (after.Count > 0)
            {
                throw new Exception("A solved flowsheet produced " + after.Count +
                    " finding(s); it should produce none. First: " + after[0]);
            }
        }

        private static void AnEmptyFlowsheetIsBlocked()
        {
            var fs = Flowsheet.Create("DiagnosticsEmpty");

            var findings = FlowsheetDiagnostics.Check(fs.Inner);
            Report("An empty flowsheet", findings);

            RequireCode(findings, FlowsheetCodes.EmptyFlowsheet);

            // Nothing else is worth saying about a flowsheet with nothing in it.
            if (findings.Count != 1)
            {
                throw new Exception("An empty flowsheet produced " + findings.Count +
                    " findings; the one about being empty is enough.");
            }
        }

        /// <summary>Every fault below is deliberate, and each one has to be named.</summary>
        private static void FaultsAreNamed()
        {
            var fs = Flowsheet.Create("DiagnosticsFaulty")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            // A stream attached to nothing.
            fs.AddMaterialStream("orphan");

            // A feed whose flow was zeroed. A new stream is born at 298.15 K, 1 atm and 1 kg/s,
            // so an untouched one has nothing wrong with it to find.
            var bare = fs.AddMaterialStream("bare-feed").WithMassFlow(0.0.KgPerSecond());
            var bareProduct = fs.AddMaterialStream("bare-product");
            fs.AddMixer("MIX-bare")
                .ConnectFeed(bare, 0)
                .ConnectProduct(bareProduct, 0);

            // A heater with a feed but no product.
            var heaterFeed = fs.AddMaterialStream("heater-feed")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));

            var duty = fs.AddEnergyStream("duty");

            fs.AddHeater("H-1")
                .ConnectFeed(heaterFeed, 0)
                .ConnectEnergyFeed(duty, 1);

            // A mixer with nothing on it at all.
            fs.AddMixer("MIX-lonely");

            fs.AutoLayout();

            var findings = FlowsheetDiagnostics.Check(fs.Inner);
            Report("A faulty flowsheet", findings);

            RequireCode(findings, FlowsheetCodes.StreamDangling, "orphan");
            RequireCode(findings, FlowsheetCodes.UnitNoProduct, "H-1");
            RequireCode(findings, FlowsheetCodes.UnitUnconnected, "MIX-lonely");
            RequireCode(findings, FlowsheetCodes.FeedNoFlow, "bare-feed");

            // Blockers first: a caller acting on the list top-down fixes what matters soonest.
            var severities = findings.Select(f => (int)f.Severity).ToList();
            for (var i = 1; i < severities.Count; i++)
            {
                if (severities[i] > severities[i - 1])
                    throw new Exception("Findings are not ordered worst-first.");
            }

            // The good feed is fully specified, so nothing may be said about it.
            if (findings.Any(f => f.ObjectTag == "heater-feed"))
            {
                throw new Exception("A fully specified feed was reported: " +
                    findings.First(f => f.ObjectTag == "heater-feed"));
            }
        }

        /// <summary>A failed solve is explained by the object that failed, not by a stack trace.</summary>
        private static void FailuresAreExplained()
        {
            var fs = Flowsheet.Create("DiagnosticsFailure")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));

            var product = fs.AddMaterialStream("product");

            // A pump with no outlet pressure and no energy stream cannot compute anything.
            fs.AddPump("P-1")
                .ConnectFeed(feed, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();

            var errors = fs.TrySolve();

            var findings = FlowsheetDiagnostics.Diagnose(fs.Inner, errors);
            Report("A flowsheet that failed to solve", findings);

            if (errors.Count == 0)
            {
                // The pump found a specification it could work with; nothing to explain, and the
                // diagnosis has to stay quiet rather than invent a fault.
                if (findings.Any(f => f.Severity == DiagnosticSeverity.Blocker))
                {
                    throw new Exception("A flowsheet that solved was reported as blocked: " +
                        findings.First(f => f.Severity == DiagnosticSeverity.Blocker));
                }
                return;
            }

            var blockers = findings.Where(f => f.Severity == DiagnosticSeverity.Blocker).ToList();
            if (blockers.Count == 0)
                throw new Exception("The solve failed but the diagnosis found nothing to report.");

            // Whatever the code, a caller has to be told where to look and what to do.
            foreach (var blocker in blockers)
            {
                if (string.IsNullOrEmpty(blocker.Fix))
                    throw new Exception("A blocker carries no fix: " + blocker);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// A pump fed with steam stops with a code of its own, a pump fed with a wet stream is warned
        /// about, and an object handed another property package without a new solve is flagged
        /// before anyone reads its numbers.
        /// </summary>
        private static void APumpRefusesVapourAndStaleResultsAreFlagged()
        {
            // The solver stops at the first unit that throws, so each pump gets a flowsheet of its own.
            var dry = Flowsheet.Create("DiagnosticsPumpSteam")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);
            var steam = dry.AddMaterialStream("steam")
                .At(400.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));
            var pumped = dry.AddMaterialStream("pumped");
            dry.AddPump("P-steam")
                .WithOutletPressure(300000.0.Pascal())
                .WithEfficiencyPercent(75)
                .ConnectFeed(steam, 0)
                .ConnectProduct(pumped, 0);
            dry.AutoLayout();

            var dryFindings = FlowsheetDiagnostics.Diagnose(dry.Inner, dry.TrySolve());
            Report("A pump fed with steam", dryFindings);
            RequireCode(dryFindings, FlowsheetCodes.PumpVaporInlet, "P-steam");
            if (dryFindings.First(f => f.Code == FlowsheetCodes.PumpVaporInlet && f.ObjectTag == "P-steam").Severity != DiagnosticSeverity.Blocker)
                throw new Exception("A pump with no liquid to move was not reported as a blocker.");

            var fs = Flowsheet.Create("DiagnosticsPumpWet")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables)
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var wet = fs.AddMaterialStream("wet")
                .At(373.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));
            wet.Object.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
            wet.Object.Phases[2].Properties.molarfraction = 0.5;
            var wetPumped = fs.AddMaterialStream("wetPumped");
            fs.AddPump("P-wet")
                .WithOutletPressure(300000.0.Pascal())
                .WithEfficiencyPercent(75)
                .ConnectFeed(wet, 0)
                .ConnectProduct(wetPumped, 0);
            fs.AutoLayout();

            var after = FlowsheetDiagnostics.Diagnose(fs.Inner, fs.TrySolve());
            Report("A pump fed with a wet stream", after);
            RequireCode(after, FlowsheetCodes.PumpVaporInlet, "P-wet");
            if (after.First(f => f.Code == FlowsheetCodes.PumpVaporInlet && f.ObjectTag == "P-wet").Severity != DiagnosticSeverity.Warning)
                throw new Exception("A pump with a wet feed was not reported as a warning.");

            // The wet feed solved with the steam tables; hand it the other package and solve nothing.
            var solvedWith = wet.Object.PropertyPackage.UniqueID;
            wet.Object.PropertyPackage = (DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)fs.Inner.PropertyPackages.Values.First(p => p.UniqueID != solvedWith);
            var stale = FlowsheetDiagnostics.Check(fs.Inner);
            Report("After swapping the package of the wet feed", stale);
            RequireCode(stale, FlowsheetCodes.PropertyPackageChanged, "wet");
            if (stale.Any(f => f.Code == FlowsheetCodes.PropertyPackageChanged && f.ObjectTag == "wetPumped"))
                throw new Exception("An object that kept its package was reported as stale.");
        }

        private static void RequireCode(IReadOnlyList<Finding> findings, string code, string tag = null)
        {
            var match = findings.FirstOrDefault(f =>
                f.Code == code && (tag == null || f.ObjectTag == tag));

            if (match != null) return;

            var where = tag == null ? "" : " on '" + tag + "'";
            throw new Exception("Expected " + code + where + ", but the findings were:" +
                Environment.NewLine + string.Join(Environment.NewLine,
                    findings.Select(f => "  " + f)));
        }

        private static void Report(string title, IReadOnlyList<Finding> findings)
        {
            Console.WriteLine();
            Console.WriteLine(title + ": " + findings.Count + " finding(s)");
            foreach (var finding in findings) Console.WriteLine("  " + finding);
        }
    }
}
