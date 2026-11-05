// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DWSIM.Interfaces;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Talks to the AI Assistant program (localhost:5834) for the drawing import: sends the
    /// drawing to be read by the vision model the user set up there, and the drawing's text for
    /// the process to be inferred. CAD drawings go as a PDF of their drawing layout, plotted by
    /// CADCraft, with the text read straight from the file.
    /// </summary>
    public static class DrawingImportClient
    {
        const int Port = 5834;

        /// <summary>The drawings the import opens.</summary>
        public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".webp", ".pdf", ".dxf", ".dwg" };

        public static bool IsCad(string file) => file.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase);

        static bool Listening()
        {
            try { using (var c = new TcpClient()) { c.Connect("localhost", Port); return true; } }
            catch (Exception) { return false; }
        }

        /// <summary>The AI Assistant extension's helper, when it is loaded (both interfaces have one of the same name).</summary>
        static Type Helper() => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => (a.GetName().Name ?? "").IndexOf("AI.Assistant", StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(a => { try { return a.GetTypes().FirstOrDefault(t => t.Name == "ReportExportHelper"); } catch (Exception) { return null; } })
            .FirstOrDefault(t => t != null);

        static string Token()
        {
            var h = Helper();
            // reading the property makes the extension set the token up when it has not yet
            try { h?.GetProperty("AssistantToken", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null); } catch (Exception) { }
            return Environment.GetEnvironmentVariable("DWSIM_ASSISTANT_TOKEN") ?? "";
        }

        /// <summary>A time the wizards show while the assistant works: "42 s", "2 min 05 s".</summary>
        public static string Elapsed(TimeSpan t) => t.TotalMinutes >= 1
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0} min {1:00} s", (int)t.TotalMinutes, t.Seconds)
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0} s", (int)t.TotalSeconds);

        /// <summary>The note the import wizards show: the reading needs a model that reads images.</summary>
        public const string VisionModelNote = "The AI Assistant reads the drawing with the model chosen in its settings: choose one that reads images (a vision model, such as Claude, GPT-4o or Gemini).";

        /// <summary>
        /// Opens the AI Assistant's settings: its own dialog in the Windows interface, its settings
        /// page in the web browser in the cross-platform one (the import wizards are modal there,
        /// and the page the assistant docks would open behind them). Returns why it could not.
        /// </summary>
        public static string OpenAssistantSettings(IFlowsheet fs, bool windows)
        {
            var h = Helper();
            if (h == null) return "The AI Assistant is not installed or not loaded.";
            try
            {
                if (windows)
                {
                    var type = h.Assembly.GetTypes().FirstOrDefault(t => t.Name == "AIAssistantSettingsExtender");
                    if (type == null) return "The AI Assistant has no settings window here.";
                    var ext = (IExtender)Activator.CreateInstance(type);
                    ext.SetFlowsheet(fs);
                    ext.Run();
                    return null;
                }
                if (!EnsureAssistant(out var why)) return why;
                var token = h.GetProperty("AssistantToken", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as string ?? Token();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:" + Port + "/settings?token=" + Uri.EscapeDataString(token ?? "")) { UseShellExecute = true });
                return null;
            }
            catch (Exception ex) { return "The AI Assistant's settings could not be opened: " + ex.GetBaseException().Message; }
        }

        /// <summary>Whether the assistant program is running, starting it through the AI Assistant extension when it is not.</summary>
        public static bool EnsureAssistant(out string why)
        {
            why = null;
            if (Listening()) return true;
            var h = Helper();
            if (h == null)
            {
                why = "The drawing import reads the drawing with the AI Assistant, which is not installed or not loaded.";
                return false;
            }
            try
            {
                var m = h.GetMethod("EnsurePythonServerRunning", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (m != null && (bool)m.Invoke(null, null)) return true;
            }
            catch (Exception ex) { why = ex.GetBaseException().Message; }
            why = why ?? "The AI Assistant program did not start. Open the AI Assistant once and check its model settings.";
            return false;
        }

        /// <summary>The model the assistant reads with: its backend ("claude", "local", ...) and the model's name.</summary>
        public sealed class AssistantModel
        {
            public string Backend = "", Model = "";

            /// <summary>A model that runs on this computer (Ollama, LM Studio, llama.cpp), the assistant's default until it loads the choice made in its settings.</summary>
            public bool Local => Backend == "local" || Backend == "lmstudio" || Backend == "llamacpp";

            public string Provider =>
                Backend == "local" ? "Ollama" : Backend == "lmstudio" ? "LM Studio" : Backend == "llamacpp" ? "llama.cpp" :
                Backend == "claude" ? "Anthropic" : Backend == "openai" ? "OpenAI" : Backend == "gemini" ? "Google" :
                Backend == "azure" || Backend == "cloud" ? "Azure" : Backend == "bedrock" ? "AWS Bedrock" : Backend == "custom" ? "custom endpoint" : Backend;

            public override string ToString() => Model.Length > 0 ? Model + " (" + Provider + ")" : Provider;
        }

        /// <summary>The model the assistant reads with now, starting the assistant when it is not running.</summary>
        public static AssistantModel Model(CancellationToken ct, Action<string> step = null)
        {
            if (!Listening()) step?.Invoke("Starting the AI Assistant");
            if (!EnsureAssistant(out var why)) throw new InvalidOperationException(why);
            var o = Get("/session", ct);
            return new AssistantModel { Backend = (string)o["backend"] ?? "", Model = (string)o["model"] ?? "" };
        }

        /// <summary>The line the wizards' first page shows: the model the assistant reads with now.</summary>
        public static string ModelLine(AssistantModel m) => "The AI Assistant reads with " + m + "." +
            (m.Local ? " A model that runs on this computer seldom reads a document well; a vision model such as Claude, GPT or Gemini does it better." : "");

        /// <summary>What the wizards say before reading with a local model.</summary>
        public static string LocalModelNote(AssistantModel m, string what) =>
            "The AI Assistant is set to " + m + ", a model that runs on this computer and seldom reads " + what + " well. " +
            "If you chose another model in the AI Assistant's settings, open the AI Assistant once so it takes that choice up, then read again; " +
            "or click Read with the local model to read with " + (m.Model.Length > 0 ? m.Model : "it") + " anyway.";

        static JObject Get(string path, CancellationToken ct)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                http.DefaultRequestHeaders.Add("X-DWSIM-Token", Token());
                var resp = http.GetAsync("http://localhost:" + Port + path, ct).GetAwaiter().GetResult();
                var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) throw new InvalidOperationException("The AI Assistant answered " + (int)resp.StatusCode + ": " + text.Substring(0, Math.Min(300, text.Length)));
                return JObject.Parse(text);
            }
        }

        static JObject Post(string path, JObject body, CancellationToken ct)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) })
            using (var content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"))
            {
                http.DefaultRequestHeaders.Add("X-DWSIM-Token", Token());
                var resp = http.PostAsync("http://localhost:" + Port + path, content, ct).GetAwaiter().GetResult();
                var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) throw new InvalidOperationException("The AI Assistant answered " + (int)resp.StatusCode + ": " + text.Substring(0, Math.Min(300, text.Length)));
                var o = JObject.Parse(text);
                if (o["error"] != null) throw new InvalidOperationException(o["error"].ToString());
                return o;
            }
        }

        /// <summary>
        /// Reads a drawing: "pfd", "pid" or "auto" for <paramref name="kind"/>. The model's
        /// reading, with the CAD file's own text when the drawing is a DXF or DWG.
        /// </summary>
        public static DrawingReading Read(string file, string kind, CancellationToken ct, Action<string> step = null)
        {
            var o = ReadRaw(file, kind, ct, step, out var overview, out var cadTexts);
            var reading = DrawingReading.FromJson(o.ToString(), kind == "pfd" || kind == "pid" ? kind : null);
            reading.Source = file;
            reading.Overview = overview;
            reading.CadTexts.AddRange(cadTexts);
            return reading;
        }

        /// <summary>The model's reading of a drawing as it comes (pfd, pid or iso), and the sheet it saw.</summary>
        public static JObject ReadRaw(string file, string kind, CancellationToken ct, Action<string> step, out byte[] overview) =>
            ReadRaw(file, kind, ct, step, out overview, out _);

        static JObject ReadRaw(string file, string kind, CancellationToken ct, Action<string> step, out byte[] overview, out List<string> cadTexts)
        {
            var model = Model(ct, step);
            ct.ThrowIfCancellationRequested();
            overview = null;
            var send = file;
            cadTexts = new List<string>();
            string temp = null;
            try
            {
                if (IsCad(file))
                {
                    step?.Invoke("CADCraft: plotting the drawing for the model");
                    temp = Path.Combine(Path.GetTempPath(), "dwsim-import-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(temp);
                    send = PlotCad(file, temp, out cadTexts);
                }
                var what = kind == "iso" ? "the isometric" : kind == "datasheet" ? "the data sheet" : "the drawing";
                step?.Invoke("AI Assistant: " + model + " is reading " + what + "; it usually takes one to three minutes");
                var o = Post("/api/drawing/read", new JObject
                {
                    ["file"] = Convert.ToBase64String(File.ReadAllBytes(send)),
                    ["filename"] = Path.GetFileName(send),
                    ["kind"] = kind ?? "auto",
                }, ct);
                if (o["overview"] != null) try { overview = Convert.FromBase64String(o["overview"].ToString()); } catch (FormatException) { }
                return (JObject)o["reading"];
            }
            finally
            {
                if (temp != null) try { Directory.Delete(temp, true); } catch (Exception) { }
            }
        }

        /// <summary>The kind of process, its compounds and units, from the drawing's text.</summary>
        public static void Infer(DrawingReading r, CancellationToken ct)
        {
            if (!EnsureAssistant(out var why)) throw new InvalidOperationException(why);
            var texts = r.Texts.Concat(r.CadTexts).Concat(r.Lines.Select(l => l.Name)).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            var o = Post("/api/drawing/infer", new JObject
            {
                ["texts"] = new JArray(texts),
                ["items"] = new JArray(r.Items.Select(i => new JObject { ["tag"] = i.Tag, ["category"] = i.Category })),
                ["classes"] = new JArray(DrawingImport.ProcessClasses.Select(c => new JObject { ["class"] = c.Class, ["description"] = c.Description })),
                ["units"] = new JObject { ["temperature"] = r.Units.Temperature, ["pressure"] = r.Units.Pressure, ["massflow"] = r.Units.MassFlow, ["energy"] = r.Units.Energy },
            }, ct);
            var p = o["process"] as JObject;
            if (p == null) return;
            var guess = DrawingReading.FromJson(new JObject { ["process"] = p, ["units_system"] = p["units_system"] }.ToString());
            r.Process.Description = guess.Process.Description;
            r.Process.Class = guess.Process.Class;
            r.Process.Confidence = guess.Process.Confidence;
            r.Process.Evidence = guess.Process.Evidence;
            r.Process.Type = guess.Process.Type;
            r.Process.KeyReactants = guess.Process.KeyReactants;
            r.Process.KeyProducts = guess.Process.KeyProducts;
            if (guess.Process.Compounds.Count > 0) { r.Process.Compounds.Clear(); r.Process.Compounds.AddRange(guess.Process.Compounds); }
            // the units the drawing shows stay; the guess fills in the rest
            if (r.Kind == "pid" && p["units_system"] is JObject)
            {
                var u = guess.Units;
                r.Units.Temperature = u.Temperature; r.Units.Pressure = u.Pressure; r.Units.MassFlow = u.MassFlow; r.Units.Energy = u.Energy;
            }
            var packages = DrawingImport.PackagesFor(r.Process.Class);
            if (packages != null) r.Process.PropertyPackage = packages[0];
        }

        #region CAD drawings

        static string Run(string exe, string args, int seconds)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                var output = p.StandardOutput.ReadToEndAsync();
                var error = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(seconds * 1000)) { try { p.Kill(); } catch (Exception) { } throw new TimeoutException("CADCraft did not finish"); }
                if (p.ExitCode != 0) throw new InvalidOperationException("CADCraft: " + (error.Result + output.Result).Trim());
                return output.Result;
            }
        }

        static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

        /// <summary>
        /// A PDF of the drawing's sheet (its first named layout that is not a legend, else model
        /// space) and the text the file holds, for a DXF or DWG.
        /// </summary>
        static string PlotCad(string file, string temp, out List<string> texts)
        {
            var folder = CADCraftSession.FindInstallFolder();
            if (folder == null) throw new InvalidOperationException("Reading a DXF or DWG drawing needs CADCraft; install it, or export the drawing as PDF or PNG.");
            var exe = Path.Combine(folder, CADCraftSession.Exe("cadcraft-cli"));
            var dxf = file;
            if (file.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                dxf = Path.Combine(temp, "drawing.dxf");
                Run(exe, "convert " + Quote(file) + " " + Quote(dxf), 300);
            }
            texts = DxfTexts(dxf);

            var info = JObject.Parse(Run(exe, "info --compact " + Quote(dxf), 120));
            var layouts = ((info["layouts"] as JArray) ?? new JArray()).Select(t => t.ToString())
                .Where(l => !Regex.IsMatch(l, "^(Layout\\d+|Model)$", RegexOptions.IgnoreCase))
                .OrderBy(l => l.IndexOf("legend", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var pdf = Path.Combine(temp, "drawing.pdf");
            if (layouts.Count > 0)
            {
                var cmd = new JObject { ["path"] = pdf.Replace('\\', '/'), ["layout"] = layouts.First(), ["device"] = "CADCraft PDF" };
                Run(exe, "run " + Quote(dxf) + " --cmd " + Quote("plot " + cmd.ToString(Newtonsoft.Json.Formatting.None)), 300);
            }
            if (!File.Exists(pdf)) Run(exe, "convert " + Quote(dxf) + " " + Quote(pdf), 300);
            return pdf;
        }

        static readonly Regex Format = new Regex(@"\\[A-Za-z][^;]*;|\\P|[{}]");

        /// <summary>The text strings of a DXF (text, multiline text, block attributes), each once.</summary>
        public static List<string> DxfTexts(string dxf)
        {
            var list = new List<string>();
            var seen = new HashSet<string>();
            string entity = null;
            using (var reader = new StreamReader(dxf, Encoding.UTF8, true))
            {
                while (true)
                {
                    var code = reader.ReadLine();
                    var value = reader.ReadLine();
                    if (code == null || value == null) break;
                    code = code.Trim();
                    if (code == "0") entity = value.Trim();
                    else if ((code == "1" || code == "3") && (entity == "TEXT" || entity == "MTEXT" || entity == "ATTRIB"))
                    {
                        var t = Format.Replace(value, " ").Trim();
                        if (t.Length > 0 && seen.Add(t)) list.Add(t);
                    }
                }
            }
            return list;
        }

        #endregion
    }
}
