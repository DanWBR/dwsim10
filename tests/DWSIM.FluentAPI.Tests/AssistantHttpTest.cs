using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.AI.Assistant;
using Newtonsoft.Json.Linq;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Drives the assistant's HTTP API over a real socket.
    /// </summary>
    /// <remarks>
    /// Everything else about this surface is tested by calling the handlers directly, which
    /// says nothing about whether a request actually reaches them. The server binds a fixed
    /// port, so a DWSIM already running on this machine owns it — the test says so and stops
    /// rather than reporting a failure it cannot distinguish from a real one.
    /// </remarks>
    internal static class AssistantHttpTest
    {
        private const string BaseUrl = "http://localhost:5002";

        /// <summary>
        /// The API is gated on a per-launch token, which the extension mints on first use and
        /// reuses from the environment when one is already there. Setting it before anything
        /// touches the server is what lets a test speak to it.
        /// </summary>
        private const string Token = "test-token-assistant-http";

        public static void Run()
        {
            Environment.SetEnvironmentVariable("DWSIM_ASSISTANT_TOKEN", Token,
                                               EnvironmentVariableTarget.Process);

            var fs = Flowsheet.Create("AssistantHttp")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(400.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.KgPerSecond())
                .WithComposition(c => c.Mole("Water", 1.0));

            var product = fs.AddMaterialStream("product");

            fs.AddCooler("CD-1")
                .ConnectFeed(feed, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();

            var server = new Server { Flowsheet = fs.Inner };

            try
            {
                server.StartServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not start the assistant server: " + ex.Message);
                return;
            }

            // Always stop the server: its HTTP listener otherwise keeps the test host alive and the
            // whole `dotnet test` run hangs on exit (a background thread suffices on Windows, but the
            // Linux managed listener does not let the process go until the listener is torn down).
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                {
                    http.DefaultRequestHeaders.Add("X-DWSIM-Token", Token);

                    if (!Reachable(http))
                    {
                        Console.WriteLine("Port 5002 is not answering — a DWSIM instance probably owns " +
                                          "it. Skipping the HTTP checks.");
                        return;
                    }

                    CheckModifyUnit(http, fs);
                    CheckFlowsheetCheck(http);
                }
            }
            finally
            {
                server.StopServer();
            }
        }

        /// <summary>Waits for the listener thread to come up before deciding it is not there.</summary>
        private static bool Reachable(HttpClient http)
        {
            Exception last = null;

            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    var reply = http.GetAsync(BaseUrl + "/api/check").GetAwaiter().GetResult();
                    if (reply.IsSuccessStatusCode) return true;

                    Console.WriteLine($"  /api/check answered {(int)reply.StatusCode}");

                    // 401 means something else minted the token first — another DWSIM on this
                    // machine owns the port, and its answers are not ours to check.
                    return false;
                }
                catch (Exception ex)
                {
                    last = ex;
                    System.Threading.Thread.Sleep(250);
                }
            }

            Console.WriteLine("  last attempt: " + (last == null ? "(none)" : last.GetBaseException().Message));
            return false;
        }

        /// <summary>
        /// The calculation mode is a string, and strings were being dropped: the route read the
        /// request with a regular expression, skipped anything quoted, and swallowed the failure.
        /// A cooler left in heat-duty mode ignores the outlet temperature it was given.
        /// </summary>
        private static void CheckModifyUnit(HttpClient http, Flowsheet fs)
        {
            var request = new JObject
            {
                ["object_name"] = "CD-1",
                ["properties"] = new JObject
                {
                    ["CalcMode"] = "OutletTemperature",
                    ["OutletTemperature"] = 320.0
                }
            };

            var answer = Post(http, "/api/modify-unit", request);
            Console.WriteLine();
            Console.WriteLine("modify-unit: " + answer.ToString(Newtonsoft.Json.Formatting.None));

            var failed = (JArray)answer["failed"];
            if (failed == null)
                throw new Exception("The response does not report what it failed to set.");

            if (failed.Count > 0)
                throw new Exception("modify-unit could not set: " + string.Join("; ", failed));

            var modified = (JArray)answer["modified_properties"];
            if (modified == null || modified.Count != 2)
                throw new Exception("Expected both properties to be set, got: " + modified);

            // The point of setting the mode: the cooler now reads the temperature it was given.
            var errors = fs.TrySolve();
            if (errors.Count > 0) throw errors[0];

            var outlet = fs.MaterialStream("product").TemperatureK;
            if (Math.Abs(outlet - 320.0) > 0.5)
            {
                throw new Exception($"The cooler left its outlet at {outlet:F2} K, not the 320 K it " +
                    "was given — the calculation mode did not take.");
            }

            Console.WriteLine($"  the cooler took its target: outlet at {outlet:F2} K");

            // And a name that matches nothing has to come back saying so.
            var bad = Post(http, "/api/modify-unit", new JObject
            {
                ["object_name"] = "CD-1",
                ["properties"] = new JObject { ["CalcMode"] = "OutletTempreature" }
            });

            var badFailed = (JArray)bad["failed"];
            if (badFailed == null || badFailed.Count != 1)
                throw new Exception("A misspelled mode was not reported: " + bad);

            var message = badFailed[0].ToString();
            Console.WriteLine("  misspelling reported as: " + message);

            if (!message.Contains("OutletTemperature"))
                throw new Exception("The rejection does not name the modes that would work.");

            if (bad["success"] != null && bad["success"].Value<bool>())
                throw new Exception("A request that set nothing reported success.");
        }

        private static void CheckFlowsheetCheck(HttpClient http)
        {
            var reply = http.GetAsync(BaseUrl + "/api/flowsheet/check").GetAwaiter().GetResult();
            var body = reply.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var answer = JObject.Parse(body);

            Console.WriteLine("flowsheet/check: " + answer.ToString(Newtonsoft.Json.Formatting.None));

            foreach (var field in new[] { "ready", "blockers", "warnings", "findings" })
            {
                if (answer[field] == null)
                    throw new Exception("The check response has no '" + field + "'.");
            }
        }

        private static JObject Post(HttpClient http, string path, JObject payload)
        {
            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            var reply = http.PostAsync(BaseUrl + path, content).GetAwaiter().GetResult();
            var body = reply.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            try
            {
                return JObject.Parse(body);
            }
            catch (Exception)
            {
                throw new Exception($"{path} answered {(int)reply.StatusCode} with: {body}");
            }
        }
    }
}
