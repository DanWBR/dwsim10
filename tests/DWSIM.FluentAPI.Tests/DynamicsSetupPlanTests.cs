using System;
using System.Linq;
using DWSIM.Automation.DynamicRunner.Setup;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Drives the conversion the Dynamics Wizard drives: builds the tank-filling flowsheet as a
    /// user would leave it after a steady-state run — no volumes, no flow coefficient, no specs,
    /// no integrator — and checks that applying the plan makes it ready to integrate.
    /// </summary>
    /// <remarks>
    /// The wizards in both user interfaces are only a face over these calls, so proving the plan
    /// here proves the part of them that can be wrong about chemistry rather than about buttons.
    /// </remarks>
    [TestFixture]
    public class DynamicsSetupPlanTests
    {
        /// <summary>A flowsheet as the steady state leaves it: solved, but with nothing dynamics needs.</summary>
        private static Flowsheet BuildSteadyStateFlowsheet()
        {
            var fs = Flowsheet.Create("DynamicsWizardConversion")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(25.Celsius(), 3.Atm())
                .WithMassFlow(1.0.KgPerSecond());

            var tankOutlet = fs.AddMaterialStream("tank-outlet")
                .At(25.Celsius(), 2.Atm());

            var product = fs.AddMaterialStream("product")
                .At(25.Celsius(), 1.Atm());

            fs.AddTank("TK-01")
                .ConnectFeed(feed, 0)
                .ConnectProduct(tankOutlet, 0);

            fs.AddValve("V-01")
                .WithCalcMode(DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode.OutletPressure)
                .ConnectFeed(tankOutlet, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();
            fs.Solve();

            return fs;
        }

        /// <summary>
        /// The point of the wizard: a flowsheet that is not ready becomes one that is, without the
        /// user knowing which of the dozen settings were missing.
        /// </summary>
        [Test]
        public void ApplyingThePlanClearsTheBlockers()
        {
            var fs = BuildSteadyStateFlowsheet();
            var inner = fs.Inner;

            var before = DynamicsSetupPlan.Propose(inner);

            Assert.That(before.Any(i => i.Code == "NO_SCHEDULE"), Is.True,
                "a flowsheet with no dynamics set up should report a missing schedule");

            // Apply everything the plan is confident about, in the order the wizard's steps run.
            foreach (var category in new[]
            {
                DynamicsIssueCategory.Integrator,
                DynamicsIssueCategory.Holdup,
                DynamicsIssueCategory.Hydraulics,
                DynamicsIssueCategory.BoundarySpecs,
                DynamicsIssueCategory.Control
            })
            {
                // Rescanned each time: applying one fix changes what the next step is looking at.
                var issues = DynamicsSetupPlan.Propose(inner);
                foreach (var issue in issues.Where(i => i.Category == category && i.CanAutoFix).ToList())
                {
                    DynamicsSetupPlan.Apply(issue);
                }
            }

            var after = DynamicsSetupPlan.Propose(inner);
            var blockers = after.Where(i => i.Severity == DynamicsIssueSeverity.Blocker).ToList();

            Assert.That(blockers, Is.Empty,
                "after the plan is applied nothing should block a run; still blocking: " +
                string.Join(" | ", blockers.Select(b => b.ToString())));
        }

        /// <summary>
        /// The conversion has to be safe to repeat: the wizard rescans after every apply, and a
        /// user may well walk it twice.
        /// </summary>
        [Test]
        public void ApplyingThePlanTwiceChangesNothingTheSecondTime()
        {
            var fs = BuildSteadyStateFlowsheet();
            var inner = fs.Inner;

            ApplyEverything(inner);

            var settled = DynamicsSetupPlan.Propose(inner);
            var fixableAfterFirstPass = settled.Count(i => i.CanAutoFix);

            ApplyEverything(inner);

            var again = DynamicsSetupPlan.Propose(inner);

            Assert.That(again.Count(i => i.CanAutoFix), Is.EqualTo(fixableAfterFirstPass),
                "a second pass should find the same work outstanding as the first one left");
            Assert.That(again.Count(i => i.Severity == DynamicsIssueSeverity.Blocker), Is.Zero);
        }

        private static void ApplyEverything(DWSIM.Interfaces.IFlowsheet inner)
        {
            for (var pass = 0; pass < 4; pass++)
            {
                foreach (var issue in DynamicsSetupPlan.Propose(inner).Where(i => i.CanAutoFix).ToList())
                {
                    DynamicsSetupPlan.Apply(issue);
                }
            }
        }

        /// <summary>
        /// A valve in a pressure-drop mode cannot resolve its own flow, and a valve that ignores
        /// its opening cannot be controlled. The plan has to settle both, and give it a real Kv.
        /// </summary>
        [Test]
        public void TheValveEndsUpAbleToResolveItsOwnFlow()
        {
            var fs = BuildSteadyStateFlowsheet();
            var inner = fs.Inner;

            ApplyEverything(inner);

            var valve = inner.SimulationObjects.Values
                .OfType<DWSIM.UnitOperations.UnitOperations.Valve>()
                .Single();

            Assert.That(valve.Kv, Is.GreaterThan(0.0), "the valve should have been sized");
            Assert.That(valve.EnableOpeningKvRelationship, Is.True,
                "without the opening-Kv characteristic a controller moving the opening does nothing");
            Assert.That(valve.CalcMode,
                Is.Not.EqualTo(DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode.OutletPressure)
                  .And.Not.EqualTo(DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode.DeltaP),
                "a pressure-drop mode cannot compute a flow");
        }

        /// <summary>
        /// The feed holds its flow and the product holds its pressure; that is what pins the
        /// pressure-flow network down at its edges.
        /// </summary>
        [Test]
        public void TheBoundaryStreamsAreSpecifiedFromTheirTopology()
        {
            var fs = BuildSteadyStateFlowsheet();
            var inner = fs.Inner;

            ApplyEverything(inner);

            var feed = inner.SimulationObjects.Values.First(o => o.GraphicObject.Tag == "feed");
            var product = inner.SimulationObjects.Values.First(o => o.GraphicObject.Tag == "product");

            Assert.That(feed.DynamicsSpec,
                Is.EqualTo(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType.Flow),
                "a stream with nothing upstream is a feed and should hold its flow");
            Assert.That(product.DynamicsSpec,
                Is.EqualTo(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType.Pressure),
                "a stream with nothing downstream is a product and should hold its pressure");
        }

        /// <summary>
        /// The Fluent API's own diagnostics are a view over the same rules now, so the codes it
        /// reports must still line up with the catalogue it publishes.
        /// </summary>
        [Test]
        public void TheFluentDiagnosticsStillSpeakTheDocumentedCodes()
        {
            var fs = BuildSteadyStateFlowsheet();

            var findings = DynamicsDiagnostics.CheckReady(fs.Inner);

            Assert.That(findings, Is.Not.Empty);

            var undocumented = findings
                .Select(f => f.Code)
                .Where(code => !DiagnosticCodes.All.ContainsKey(code))
                .Distinct()
                .ToList();

            Assert.That(undocumented, Is.Empty,
                "every code reported has to be in the published catalogue; missing: " +
                string.Join(", ", undocumented));
        }
    }
}
