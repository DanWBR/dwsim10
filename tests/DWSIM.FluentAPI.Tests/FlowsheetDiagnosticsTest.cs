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

            // A heater with a feed but no product, and an energy stream with one loose end.
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
            RequireCode(findings, FlowsheetCodes.EnergyStreamHalfConnected, "duty");

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
