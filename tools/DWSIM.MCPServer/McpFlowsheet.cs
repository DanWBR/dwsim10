using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using DWSIM.Interfaces;
using DWSIM.GlobalSettings;
using ICSharpCode.SharpZipLib.Zip;

namespace DWSIM.MCPServer
{
    /// <summary>
    /// Headless IFlowsheet implementation for the MCP server.
    /// Based on Flowsheet3 from DWSIM.AI.ConvergenceAssistant.
    /// Uses FlowsheetSolver2 which does not depend on global settings,
    /// allowing parallel solving of multiple flowsheets.
    /// </summary>
    public class McpFlowsheet : FlowsheetBase.FlowsheetBase
    {
        public override bool SupressMessages { get; set; } = false;

        public McpFlowsheet()
        {
            // Spreadsheet delegates — no-op for headless MCP server
            GetSpreadsheetObjectFunc = () => null;
            LoadSpreadsheetData = new Action<XDocument>((_) => { });
            SaveSpreadsheetData = new Action<XDocument>((_) => { });
            RetrieveSpreadsheetData = new Func<string, List<string[]>>((_) => new List<string[]>());
            RetrieveSpreadsheetFormat = new Func<string, List<string[]>>((_) => new List<string[]>());

            DynamicsManager.RunSchedule = (schname) =>
            {
                var schedule = DWSIM.Automation.DynamicRunner.IntegratorRunner.ResolveSchedule(this, schname);
                DynamicsManager.CurrentSchedule = schedule.ID;
                return new DWSIM.Automation.DynamicRunner.IntegratorRunner(this).RunAsync(
                    new DWSIM.Automation.DynamicRunner.IntegratorRunOptions { Schedule = schedule.ID });
            };
        }

        public void Init()
        {
            Initialize();
            // Set the Flowsheet property on the GraphicsSurface so rendering works headlessly
            var surface = (DWSIM.Drawing.SkiaSharp.GraphicsSurface)GetSurface();
            surface.Flowsheet = this;
        }

        /// <summary>
        /// Solve the flowsheet using FlowsheetSolver2 (thread-safe, no global settings dependency).
        /// </summary>
        public List<Exception> SolveFlowsheet(CancellationToken token, int timeoutSeconds = 300)
        {
            if (PropertyPackages.Count == 0)
                throw new InvalidOperationException("No property package set. Call dwsim_thermo_set_property_package first.");

            if (SelectedCompounds.Count == 0)
                throw new InvalidOperationException("No compounds added. Call dwsim_thermo_add_compounds first.");

            Task<List<Exception>> solveTask = new Task<List<Exception>>(() =>
            {
                var solver = new FlowsheetSolver.FlowsheetSolver2
                {
                    ThisCancellationToken = token,
                    SolverTimeoutSeconds = timeoutSeconds
                };
                return solver.SolveFlowsheet(this);
            });

            try
            {
                solveTask.Start(TaskScheduler.Default);
                solveTask.Wait();
                return solveTask.Result;
            }
            catch (AggregateException aex)
            {
                foreach (Exception ex in aex.InnerExceptions)
                {
                    ShowMessage(ex.ToString(), IFlowsheet.MessageType.GeneralError);
                }
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new CancellationTokenSource();
                return new List<Exception>(aex.InnerExceptions);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.ToString(), IFlowsheet.MessageType.GeneralError);
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new CancellationTokenSource();
                return new List<Exception> { ex };
            }
        }

        public void SaveSimulation(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".dwxmz")
            {
                string xmlfile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
                SaveToXML().Save(xmlfile);

                using (var strmZipOutputStream = new ZipOutputStream(File.Create(path)))
                {
                    strmZipOutputStream.SetLevel(9);

                    if (Options.UsePassword)
                        strmZipOutputStream.Password = Options.Password;

                    using (FileStream strmFile = File.OpenRead(xmlfile))
                    {
                        byte[] buffer = new byte[strmFile.Length];
                        strmFile.Read(buffer, 0, buffer.Length);

                        var entry = new ZipEntry(Path.GetFileName(xmlfile))
                        {
                            DateTime = DateTime.Now,
                            Size = strmFile.Length
                        };
                        strmZipOutputStream.PutNextEntry(entry);
                        strmZipOutputStream.Write(buffer, 0, buffer.Length);
                    }

                    strmZipOutputStream.Finish();
                }

                try { File.Delete(xmlfile); } catch { }
            }
            else
            {
                SaveToXML().Save(path);
            }
        }

        // --- FlowsheetBase abstract overrides ---

        public override IFlowsheet GetNewInstance()
        {
            var fs = new McpFlowsheet();
            return fs;
        }

        public override void UpdateInformation() { UpdateInterface(); }

        public override void UpdateInterface() { }

        public override void ShowDebugInfo(string text, int level)
        {
            // Write to stderr, never stdout (would corrupt stdio JSON-RPC)
            Console.Error.WriteLine($"[dwsim-engine] {text}");
        }

        public override void ShowMessage(string text, IFlowsheet.MessageType mtype, string exceptionid = "")
        {
            Console.Error.WriteLine($"[dwsim-engine] [{mtype}] {text}");
        }

        public override void UpdateOpenEditForms() { }

        public override object GetApplicationObject() { return null; }

        public override void SetMessageListener(Action<string, IFlowsheet.MessageType> act) { }

        public override void CloseOpenEditForms() { }

        public override IFlowsheet Clone()
        {
            var fs = new McpFlowsheet();

            // Same setup SessionManager gives a fresh flowsheet: without the catalogues and the
            // resource managers, LoadFromXML has nothing to resolve the saved objects against.
            fs.SupressDataLoading = true;
            fs.AvailableCompounds = AvailableCompounds;
            fs.AvailablePropertyPackages = AvailablePropertyPackages;
            fs.SetResourcesManager(GetResourcesManager());
            fs.SetPropertyResourcesManager(GetPropertyResourcesManager());
            fs.Init();

            var xdoc = SaveToXML();
            fs.LoadFromXML(xdoc);
            return fs;
        }

        public override void DisplayForm(object form)
        {
            // Headless server: there is no window to show. The engine may still ask for one
            // (e.g. the column convergence inspector), so ignore the request instead of throwing.
        }

        public override void RunCodeOnUIThread(Action act)
        {
            act?.Invoke();
        }
    }
}
