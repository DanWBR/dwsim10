//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Sends commands to the SolidCraft application that is open on this computer, through its MCP
    /// bridge (solidcraft-cli mcp --connect): the model the user is looking at changes without being
    /// built again. The running application writes its control port and token under its local
    /// data folder (SolidCraft/control/current.port and &lt;port&gt;.token).
    /// </summary>
    public static class LiveLink
    {
        /// <summary>The control port and token of the running application, or the default port without a token.</summary>
        static void Endpoint(out string address, out string token)
        {
            address = "127.0.0.1:7878";
            token = null;
            try
            {
                var control = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SolidCraft", "control");
                var portFile = Path.Combine(control, "current.port");
                if (!File.Exists(portFile)) return;
                var port = File.ReadAllText(portFile).Trim();
                if (port.Length == 0) return;
                address = "127.0.0.1:" + port;
                var tokenFile = Path.Combine(control, port + ".token");
                if (File.Exists(tokenFile)) token = File.ReadAllText(tokenFile).Trim();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Runs each command in the open application, one by one, so a component that is not there
        /// does not stop the rest. Returns false (with the reason) when the application cannot be
        /// reached or does not hold the design of this flowsheet; otherwise the commands that failed.
        /// </summary>
        public static bool TryRun(string folder, string modelName, List<SolidCraftScript.Command> cmds, out int failed, out string error)
        {
            failed = 0;
            error = null;
            var cli = Path.Combine(folder, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "solidcraft-cli.exe" : "solidcraft-cli");
            Endpoint(out var address, out var token);
            var psi = new ProcessStartInfo(cli, "mcp --connect " + address + (string.IsNullOrEmpty(token) ? "" : " --token " + token))
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            Process p;
            try { p = Process.Start(psi); }
            catch (Exception ex) { error = ex.Message; return false; }

            try
            {
                p.ErrorDataReceived += (s, e) => { };
                p.BeginErrorReadLine();
                var id = 0;
                JObject Call(string method, JObject prms)
                {
                    id++;
                    var msg = new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = prms };
                    p.StandardInput.WriteLine(msg.ToString(Newtonsoft.Json.Formatting.None));
                    p.StandardInput.Flush();
                    var read = Task.Run(() => p.StandardOutput.ReadLine());
                    if (!read.Wait(30000) || read.Result == null) throw new TimeoutException("SolidCraft did not answer");
                    return JObject.Parse(read.Result);
                }

                Call("initialize", new JObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = "DWSIM", ["version"] = "1" }
                });
                p.StandardInput.WriteLine(new JObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }.ToString(Newtonsoft.Json.Formatting.None));

                // the open design must be this flowsheet's model
                var md = Call("tools/call", new JObject { ["name"] = "execute", ["arguments"] = new JObject { ["command"] = "document.metadata", ["params"] = new JObject() } });
                var text = md.ToString();
                if (md["error"] != null || md["result"]?["isError"]?.Value<bool>() == true)
                {
                    error = "the SolidCraft application is not open on this computer (" + address + ")";
                    return false;
                }
                if (!text.Contains("org.dwsim.flowsheet") || !text.Contains(modelName))
                {
                    error = "the design open in SolidCraft is not this flowsheet's 3D model";
                    return false;
                }

                foreach (var c in cmds)
                {
                    var r = Call("tools/call", new JObject { ["name"] = "execute", ["arguments"] = new JObject { ["command"] = c.Name, ["params"] = c.Params } });
                    if (r["error"] != null || r["result"]?["isError"]?.Value<bool>() == true) failed++;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "the SolidCraft application could not be reached (" + ex.Message + ")";
                return false;
            }
            finally
            {
                try { p.StandardInput.Close(); } catch { }
                try { if (!p.WaitForExit(3000)) p.Kill(); } catch { }
                p.Dispose();
            }
        }
    }
}
