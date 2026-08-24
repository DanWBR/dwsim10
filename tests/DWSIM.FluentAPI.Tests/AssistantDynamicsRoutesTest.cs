using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.AI.Assistant;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Drives the assistant's dynamics endpoints against a headless flowsheet.
    /// </summary>
    /// <remarks>
    /// The routes are the assistant's whole view of dynamic simulation, and the assistant's own
    /// brain lives outside this repository — so the shape of these responses is a contract with
    /// code nobody here can test against. This walks the documented workflow end to end and checks
    /// the payloads a caller is told to expect.
    /// </remarks>
    [TestFixture]
    public class AssistantDynamicsRoutesTest
    {
        private const string MassFlow = "PROP_MS_2";

        private static Flowsheet NewFlowsheet()
        {
            var fs = Flowsheet.Create("AssistantDynamicsRoutes")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            fs.AddMaterialStream("feed")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.KgPerSecond())
                .AsFlowSpec();

            fs.Solve();
            return fs;
        }

        private static JObject Call(Flowsheet fs, string method, string path, object payload = null,
            int expectedStatus = 200)
        {
            var body = payload == null ? "" : JObject.FromObject(payload).ToString();
            var result = DynamicsRoutes.Handle(fs.Inner, method, path, body);

            Assert.That(result.StatusCode, Is.EqualTo(expectedStatus),
                "unexpected status for " + path + ": " + result.Body);

            return JObject.Parse(result.Body);
        }

        [Test]
        public void TheDocumentedWorkflowRunsEndToEnd()
        {
            var fs = NewFlowsheet();

            var inspect = Call(fs, "GET", "/api/dynamics/inspect");
            Assert.That(inspect["success"].Value<bool>(), Is.True);
            Assert.That(inspect["object_count"].Value<int>(), Is.EqualTo(1));

            // Property ids are not guessable, which is the reason this route exists.
            var properties = Call(fs, "GET", "/api/dynamics/properties", new { tag = "feed", filter = MassFlow });
            Assert.That(properties["properties"].Any(), Is.True,
                "the feed's mass-flow property was not discoverable");

            var setup = Call(fs, "POST", "/api/dynamics/setup",
                new { schedule = "Routes", step_s = 1.0, duration_s = 30.0 });
            Assert.That(setup["estimated_steps"].Value<int>(), Is.EqualTo(30));

            var monitor = Call(fs, "POST", "/api/dynamics/monitor",
                new { action = "set", variables = new[] { "feed." + MassFlow } });
            Assert.That(monitor["monitored_variables"].Count(), Is.EqualTo(1));

            var ev = Call(fs, "POST", "/api/dynamics/event",
                new
                {
                    action = "add",
                    tag = "feed",
                    property = MassFlow,
                    value = 25.0,
                    units = "kg/s",
                    at_s = 15.0,
                    transition = "step",
                    description = "step the feed"
                });
            Assert.That(ev["events"].Count(), Is.EqualTo(1));

            var check = Call(fs, "GET", "/api/dynamics/check");
            Assert.That(check["ready"].Value<bool>(), Is.True,
                "the flowsheet should be ready: " + check["blockers"]);

            var run = Call(fs, "POST", "/api/dynamics/run", new { wait = true, max_wall_time_s = 60 });
            var runId = run["run_id"].Value<string>();
            Assert.That(runId, Does.StartWith("dyn_"));

            var status = Call(fs, "GET", "/api/dynamics/status/" + runId);
            Assert.That(status["state"].Value<string>(), Is.EqualTo("completed"),
                "the run did not complete: " + status);
            Assert.That(status["steps"].Value<int>(), Is.GreaterThan(0));

            // A status response must never carry the time series itself.
            Assert.That(status["summary"], Is.Not.Null);
            Assert.That(status.ToString().Length, Is.LessThan(4096),
                "the status response is too large to be worth putting in a model's context");

            var series = Call(fs, "GET", "/api/dynamics/series/" + runId, new { max_points = 8 });
            Assert.That(series["points"].Value<int>(), Is.LessThanOrEqualTo(10));
            Assert.That(series["decimated_from"].Value<int>(), Is.EqualTo(31));

            // Series come back in the flowsheet's display units, which for mass flow is kg/h here,
            // so compare the shape of the step rather than the raw numbers.
            var recorded = series["series"].First.First;
            var values = recorded["values"].Select(v => double.Parse(v.Value<string>(),
                System.Globalization.CultureInfo.InvariantCulture)).ToList();

            Assert.That(recorded["units"].Value<string>(), Is.Not.Empty, "the series carries no units");
            Assert.That(values.Last() / values.First(), Is.EqualTo(2.5).Within(0.01),
                "the feed should have stepped from 10 to 25 kg/s, whatever the display units");

            var analysis = Call(fs, "GET", "/api/dynamics/analyze/" + runId);
            Assert.That(analysis["analysis"].Count(), Is.EqualTo(1));
            Assert.That(analysis["analysis"][0]["verdict"].Value<string>(), Is.Not.Empty);

            var diagnose = Call(fs, "GET", "/api/dynamics/diagnose/" + runId);
            Assert.That(diagnose["findings"], Is.Not.Null);
        }

        [Test]
        public void AnUnknownRouteIsRejected()
        {
            var fs = NewFlowsheet();
            var result = Call(fs, "GET", "/api/dynamics/nonsense", expectedStatus: 404);
            Assert.That(result["error"].Value<string>(), Is.EqualTo("unknown_route"));
        }

        [Test]
        public void RunningAnUnpreparedFlowsheetReportsItsBlockers()
        {
            var fs = NewFlowsheet();

            var result = Call(fs, "POST", "/api/dynamics/run", new { }, expectedStatus: 400);

            Assert.That(result["error"].Value<string>(), Is.EqualTo("not_ready"));
            Assert.That(result["blockers"].Count(), Is.GreaterThan(0));
            Assert.That(result["blockers"][0]["code"].Value<string>(), Is.EqualTo("NO_SCHEDULE"));
            Assert.That(result["blockers"][0]["fix"].Value<string>(), Is.Not.Empty,
                "a blocker without a fix leaves the caller stuck");
        }

        [Test]
        public void MalformedJsonIsRejectedWithoutThrowing()
        {
            var fs = NewFlowsheet();
            var result = DynamicsRoutes.Handle(fs.Inner, "POST", "/api/dynamics/setup", "{not json");

            Assert.That(result.StatusCode, Is.EqualTo(400));
            Assert.That(JObject.Parse(result.Body)["error"].Value<string>(), Is.EqualTo("invalid_json"));
        }

        [Test]
        public void AskingForResultsBeforeTheyExistSaysSo()
        {
            var fs = NewFlowsheet();
            var result = Call(fs, "GET", "/api/dynamics/series/dyn_nosuchrun", expectedStatus: 404);
            Assert.That(result["error"].Value<string>(), Is.EqualTo("unknown_run"));
        }

        /// <summary>
        /// The catalogue is how the assistant's brain — which lives outside this repository —
        /// discovers that dynamics exists at all. If it stops describing the routes, the capability
        /// is invisible however well it works.
        /// </summary>
        [Test]
        public void TheCatalogueAdvertisesTheDynamicsSurface()
        {
            var fs = NewFlowsheet();
            var catalog = JObject.Parse(FluentSweep.CatalogJson(fs.Inner));

            var dynamics = catalog["dynamics"];
            Assert.That(dynamics, Is.Not.Null, "the catalogue does not mention dynamics");
            Assert.That(dynamics["supported"].Value<bool>(), Is.True);
            Assert.That(dynamics["workflow"].Count(), Is.GreaterThan(5));
            Assert.That(dynamics["endpoints"]["run"].Value<string>(), Is.EqualTo("POST /api/dynamics/run"));
            Assert.That(dynamics["diagnostic_codes"]["VALVE_NO_KV"], Is.Not.Null);
            Assert.That(dynamics["series_budget"]["hard_cap"].Value<int>(), Is.EqualTo(400));

            // Every route the catalogue advertises has to be one the module actually handles.
            // The call is expected to fail on its arguments — what must not happen is the router
            // saying it has never heard of the route.
            foreach (var endpoint in (JObject)dynamics["endpoints"])
            {
                var route = endpoint.Value.Value<string>();
                var path = route.Split(' ')[1].Replace("{run_id}", "dyn_probe");
                var reply = DynamicsRoutes.Handle(fs.Inner, "GET", path, "");
                var error = JObject.Parse(reply.Body)["error"];

                Assert.That(error == null || error.Value<string>() != "unknown_route", Is.True,
                    "the catalogue advertises " + route + ", which no route handles");
            }
        }
    }
}
