using DWSIM.ExtensionMethods;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces;
using ICSharpCode.SharpZipLib.Zip;
using iTextSharp.text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using unvell.ReoGrid;
using unvell.ReoGrid.DataFormat;
using unvell.ReoGrid.Formula;
using static DWSIM.Interfaces.Enums.Scripts;

namespace DWSIM.Automation
{
    [Guid("474a8e52-0b3e-48cb-a3bb-aae60f843578"), ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class Flowsheet2 : FlowsheetBase.FlowsheetBase
    {
        private Action<string, IFlowsheet.MessageType> listeningaction;

        private Action updateUIaction;

        private IWorkbook Spreadsheet;

        /// <inheritdoc/>
        public override bool SupressMessages { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of <see cref="Flowsheet2"/> with optional message and UI update callbacks.
        /// </summary>
        /// <param name="messageListener">An action invoked when the flowsheet emits a message, or <c>null</c> to suppress messages.</param>
        /// <param name="updateUIhandler">An action invoked when the flowsheet requests a UI refresh, or <c>null</c> to skip UI updates.</param>
        public Flowsheet2(Action<string, IFlowsheet.MessageType> messageListener, Action updateUIhandler)
        {

            GetSpreadsheetObjectFunc = () => Spreadsheet;

            LoadSpreadsheetData = new Action<XDocument>((xdoc) =>
            {
                if (xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet") != null)
                {
                    var rgfdataelement = xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("RGFData");
                    if (rgfdataelement != null)
                    {
                        string rgfdata = xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("RGFData").Value;
                        rgfdata = rgfdata.Replace("Calibri", "Arial").Replace("10.25", "10");
                        Dictionary<string, string> sdict = new Dictionary<string, string>();
                        sdict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(rgfdata);
                        Spreadsheet.RemoveWorksheet(0);
                        foreach (var item in sdict)
                        {
                            var tmpfile = SharedClasses.Utility.GetTempFileName();
                            var sheet = Spreadsheet.CreateWorksheet(item.Key);
                            Spreadsheet.Worksheets.Add(sheet);
                            var xmldoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(item.Value);
                            xmldoc.Save(tmpfile);
                            sheet.LoadRGF(tmpfile);
                            File.Delete(tmpfile);
                        }
                    }
                }
            });

            SaveSpreadsheetData = new Action<XDocument>((xdoc) =>
            {
                xdoc.Element("DWSIM_Simulation_Data").Add(new XElement("Spreadsheet"));
                xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Add(new XElement("RGFData"));
                var tmpfile = SharedClasses.Utility.GetTempFileName();
                Dictionary<string, string> sdict = new Dictionary<string, string>();
                foreach (var sheet in Spreadsheet.Worksheets)
                {
                    var tmpfile2 = SharedClasses.Utility.GetTempFileName();
                    sheet.SaveRGF(tmpfile2);
                    var xmldoc = new XmlDocument();
                    xmldoc.Load(tmpfile2);
                    sdict.Add(sheet.Name, Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmldoc));
                    File.Delete(tmpfile2);
                }
                xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("RGFData").Value = Newtonsoft.Json.JsonConvert.SerializeObject(sdict);
            });

            RetrieveSpreadsheetData = new Func<string, List<string[]>>((range) =>
            {
                return GetSpreadsheetDataFromRange(range);
            });

            RetrieveSpreadsheetFormat = new Func<string, List<string[]>>((range) =>
            {
                return GetSpreadsheetFormatFromRange(range);
            });

            DynamicsManager.RunSchedule = (schname) =>
            {
                DynamicsManager.CurrentSchedule = DynamicsManager.GetSchedule(schname).ID;
                return null;
            };

            listeningaction = messageListener;

            updateUIaction = updateUIhandler;

        }

        private List<string[]> GetSpreadsheetDataFromRange(string range)
        {

            var list = new List<string[]>();
            var slist = new List<string>();

            var rdata = Spreadsheet.Worksheets[0].GetRangeData(new RangePosition(range));

            for (var i = 0; i < rdata.GetLength(0); i++)
            {
                slist = new List<string>();
                for (var j = 0; j < rdata.GetLength(1); j++)
                {
                    slist.Add(rdata[i, j] != null ? rdata[i, j].ToString() : "");
                }
                list.Add(slist.ToArray());
            }

            return list;
        }

        private List<string[]> GetSpreadsheetFormatFromRange(string range)
        {

            var list = new List<string[]>();
            var slist = new List<string>();

            var rdata = Spreadsheet.Worksheets[0].GetRangeData(new RangePosition(range));

            for (var i = 0; i < rdata.GetLength(0); i++)
            {
                slist = new List<string>();
                for (var j = 0; j < rdata.GetLength(1); j++)
                {
                    var format = Spreadsheet.Worksheets[0].Cells[i, j].DataFormat;
                    if (format == CellDataFormatFlag.Number)
                    {
                        var args = (NumberDataFormatter.NumberFormatArgs)(Spreadsheet.Worksheets[0].Cells[i, j].DataFormatArgs);
                        slist.Add("N" + args.DecimalPlaces);
                    }
                    else
                    {
                        slist.Add("");
                    }
                }
                list.Add(slist.ToArray());
            }

            return list;
        }

        private void SetCustomSpreadsheetFunctions()
        {

            FormulaExtension.CustomFunctions["GETNAME"] = (cell, args) =>
            {
                try
                {
                    return SimulationObjects[args[0].ToString()].GraphicObject.Tag;
                }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            };

            FormulaExtension.CustomFunctions["GETPROPVAL"] = (cell, args) =>
            {
                if (args.Length == 2)
                {
                    try
                    {
                        return SimulationObjects[args[0].ToString()].GetPropertyValue(args[1].ToString());
                    }
                    catch (Exception ex)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else if (args.Length == 3)
                {
                    try
                    {
                        var obj = SimulationObjects[args[0].ToString()];
                        var val = obj.GetPropertyValue(args[1].ToString());
                        return General.ConvertUnits(double.Parse(val.ToString()), obj.GetPropertyUnit(args[1].ToString()), args[2].ToString());
                    }
                    catch (Exception ex)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else
                    return "INVALID ARGS";
            };

            FormulaExtension.CustomFunctions["SETPROPVAL"] = (cell, args) =>
            {
                if (args.Length == 3)
                {
                    try
                    {
                        var ws = cell.Worksheet;
                        var wcell = ws.Cells[ws.RowCount - 1, ws.ColumnCount - 1];
                        wcell.Data = null;
                        wcell.Formula = null;
                        wcell.Formula = args[2].ToString().Trim('"');
                        var val = wcell.Data;
                        if (wcell.Data == null)
                        {
                            val = wcell.Formula;
                        }
                        SimulationObjects[args[0].ToString()].SetPropertyValue(args[1].ToString(), val);
                        wcell.Formula = null;
                        wcell.Data = null;
                        return string.Format("EXPORT OK [{0}, {1} = {2}]", SimulationObjects[args[0].ToString()].GraphicObject.Tag, args[1].ToString(), val);
                    }
                    catch (Exception ex)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else if (args.Length == 4)
                {
                    try
                    {
                        var obj = SimulationObjects[args[0].ToString()];
                        var prop = args[1].ToString();
                        var ws = cell.Worksheet;
                        var wcell = ws.Cells[ws.RowCount - 1, ws.ColumnCount - 1];
                        wcell.Formula = null;
                        wcell.Formula = args[2].ToString().Trim('"');
                        var val = wcell.Data;
                        wcell.Formula = "";
                        wcell.Data = "";
                        var units = args[3].ToString();
                        var newval = General.ConvertUnits(double.Parse(val.ToString()), units, obj.GetPropertyUnit(prop));
                        obj.SetPropertyValue(prop, newval);
                        return string.Format("EXPORT OK [{0}, {1} = {2} {3}]", obj.GraphicObject.Tag, prop, val, units);
                    }
                    catch (Exception ex)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else
                    return "INVALID ARGS";
            };

            FormulaExtension.CustomFunctions["GETPROPUNITS"] = (cell, args) =>
            {
                if (args.Length == 2)
                {
                    try
                    {
                        return SimulationObjects[args[0].ToString()].GetPropertyUnit(args[1].ToString());
                    }
                    catch (Exception ex)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else
                    return "INVALID ARGS";
            };

            FormulaExtension.CustomFunctions["GETOBJID"] = (cell, args) =>
            {
                if (args.Length == 1)
                {
                    try
                    {
                        return GetFlowsheetSimulationObject(args[0].ToString()).Name;
                    }
                    catch (Exception ex)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else
                    return "INVALID ARGS";
            };

            FormulaExtension.CustomFunctions["GETOBJNAME"] = (cell, args) =>
            {
                if (args.Length == 1)
                {
                    try
                    {
                        return SimulationObjects[args[0].ToString()].GraphicObject.Tag;
                    }
                    catch (Exception ex)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else
                    return "INVALID ARGS";
            };
        }

        /// <summary>Initializes the flowsheet base, creates the in-memory spreadsheet, and registers custom spreadsheet functions.</summary>
        public void Init()
        {

            Initialize();

            Spreadsheet = unvell.ReoGrid.ReoGridControl.CreateMemoryWorkbook();

            SetCustomSpreadsheetFunctions();

        }

        /// <inheritdoc/>
        public override IFlowsheet GetNewInstance()
        {
            var fs = new Flowsheet2(null, null);
            return fs;
        }

        /// <inheritdoc/>
        public override void UpdateInformation()
        {
            UpdateInterface();
        }

        /// <inheritdoc/>
        public override void UpdateInterface()
        {
            updateUIaction?.Invoke();
        }

        /// <inheritdoc/>
        public override void ShowDebugInfo(string text, int level)
        {
            Console.WriteLine(text);
        }

        /// <inheritdoc/>
        public override void ShowMessage(string text, IFlowsheet.MessageType mtype, string exceptionid = "")
        {
            if (listeningaction != null) listeningaction(text, mtype);
        }

        /// <summary>Sends an informational message to the registered message listener.</summary>
        /// <param name="text">The message text to send.</param>
        public override void WriteMessage(string text)
        {
            listeningaction?.Invoke(text, IFlowsheet.MessageType.Information);
        }

        /// <inheritdoc/>
        public override void UpdateOpenEditForms()
        {

        }

        /// <inheritdoc/>
        public override object GetApplicationObject()
        {
            return null;
        }

        /// <summary>
        /// Solves the flowsheet synchronously, throwing an exception if no property package or compounds are selected.
        /// </summary>
        /// <exception cref="Exception">Thrown when no property package or no compound has been added to the flowsheet.</exception>
        public void SolveFlowsheet()
        {

            if (PropertyPackages.Count == 0)
            {
                throw new Exception("Please select a Property Package before solving the flowsheet.");
            }

            if (SelectedCompounds.Count == 0)
            {
                throw new Exception("Please select a Compound before solving the flowsheet.");
            }

            Settings.CalculatorActivated = true;
            Settings.SolverMode = 1;
            Settings.SolverBreakOnException = true;

            RequestCalculation();

        }

        /// <summary>
        /// Solves the flowsheet asynchronously and returns any exceptions produced by the solver.
        /// Shows a message instead of throwing when no property package or compounds are configured.
        /// </summary>
        /// <returns>A list of exceptions that occurred during solving, or an empty list on success.</returns>
        public List<Exception> SolveFlowsheet2()
        {
            if (PropertyPackages.Count == 0)
            {
                ShowMessage("Please select a Property Package before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return new List<Exception>();
            }

            if (SelectedCompounds.Count == 0)
            {
                ShowMessage("Please select a Compound before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return new List<Exception>();
            }

            Settings.CalculatorActivated = true;

            Task<List<Exception>> st = new Task<List<Exception>>(() =>
            {
                return FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(this, GlobalSettings.Settings.SolverMode);
            });

            st.ContinueWith((t) =>
            {
                Settings.CalculatorStopRequested = false;
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
            });

            try
            {
                st.Start(TaskScheduler.Default);
                st.Wait();
                return st.Result;
            }
            catch (AggregateException aex)
            {
                foreach (Exception ex2 in aex.InnerExceptions)
                {
                    ShowMessage(ex2.ToString(), IFlowsheet.MessageType.GeneralError);
                }
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
                return new List<Exception>(aex.InnerExceptions);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.ToString(), IFlowsheet.MessageType.GeneralError);
                Settings.CalculatorBusy = false;
                Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
                return new List<Exception> { ex };
            }

        }

        /// <inheritdoc/>
        public override void SetMessageListener(Action<string, IFlowsheet.MessageType> act)
        {
            listeningaction = act;
        }

        /// <summary>Generates a simulation results report for the specified objects and writes it to a stream.</summary>
        /// <param name="objects">The list of simulation objects to include in the report.</param>
        /// <param name="format">The output format: <c>"PDF"</c> or <c>"TXT"</c>.</param>
        /// <param name="ms">The destination stream to write the report to.</param>
        /// <exception cref="NotImplementedException">Thrown when an unsupported format is specified.</exception>
        public void GenerateReport(List<ISimulationObject> objects, string format, Stream ms)
        {

            string ptext = "";

            switch (format)
            {

                case "PDF":

                    iTextSharp.text.Document document = new iTextSharp.text.Document(PageSize.A4, 25, 25, 30, 30);
                    var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, ms);

                    var bf = iTextSharp.text.pdf.BaseFont.CreateFont(iTextSharp.text.pdf.BaseFont.COURIER, iTextSharp.text.pdf.BaseFont.CP1252, true);

                    var regfont = new Font(bf, 12, Font.NORMAL);
                    var boldfont = new Font(bf, 12, Font.BOLD);

                    document.Open();
                    document.Add(new Paragraph("DWSIM Simulation Results Report", boldfont));
                    document.Add(new Paragraph("Simulation Name: " + Options.SimulationName, boldfont));
                    document.Add(new Paragraph("Date created: " + System.DateTime.Now.ToString() + "\n\n", boldfont));

                    foreach (var obj in objects)
                    {
                        ptext = obj.GetDisplayName() + ": " + obj.GraphicObject.Tag + "\n\n";
                        document.Add(new Paragraph(ptext, boldfont));
                        ptext = obj.GetReport(Options.SelectedUnitSystem, System.Globalization.CultureInfo.CurrentCulture, Options.NumberFormat);
                        ptext += "\n";
                        document.Add(new Paragraph(ptext, regfont));
                    }

                    document.Close();

                    writer.Close();

                    break;

                case "TXT":

                    string report = "";

                    report += "DWSIM Simulation Results Report\nSimulation Name: " + Options.SimulationName + "\nDate created: " + System.DateTime.Now.ToString() + "\n\n";

                    foreach (var obj in objects)
                    {
                        ptext = "";
                        ptext += obj.GetDisplayName() + ": " + obj.GraphicObject.Tag + "\n\n";
                        ptext += obj.GetReport(Options.SelectedUnitSystem, System.Globalization.CultureInfo.CurrentCulture, Options.NumberFormat);
                        ptext += "\n";
                        report += ptext;
                    }


                    using (StreamWriter wr = new StreamWriter(ms))
                    {
                        wr.Write(report);
                    }
                    break;

                default:

                    throw new NotImplementedException("Sorry, this feature is not yet available.");
            }

        }

        /// <inheritdoc/>
        public override void RunCodeOnUIThread(Action act)
        {
            act.Invoke();
        }

        /// <inheritdoc/>
        public override void DisplayForm(object form)
        {
            throw new NotImplementedException();
        }

        /// <summary>Saves the flowsheet to the specified file path, choosing the format based on the file extension.</summary>
        /// <param name="path">The destination file path (.dwxmz for compressed, .dwxml or .xml for plain XML).</param>
        /// <param name="backup">Reserved for future use; currently has no effect.</param>
        public void SaveSimulation(string path, bool backup = false)
        {

            if (System.IO.Path.GetExtension(path).ToLower() == ".dwxmz")
            {

                path = Path.ChangeExtension(path, ".dwxmz");

                string xmlfile = Path.ChangeExtension(GetTempFileName(), ".xml");

                SaveToXML().Save(xmlfile);

                var i_Files = new List<string>();
                if (File.Exists(xmlfile))
                    i_Files.Add(xmlfile);

                var strmZipOutputStream = new ZipOutputStream(File.Create(path));

                strmZipOutputStream.SetLevel(9);

                if (Options.UsePassword)
                    strmZipOutputStream.Password = Options.Password;

                string strFile = "";

                foreach (string strFile_loopVariable in i_Files)
                {
                    strFile = strFile_loopVariable;
                    FileStream strmFile = File.OpenRead(strFile);
                    byte[] abyBuffer = new byte[strmFile.Length];

                    strmFile.Read(abyBuffer, 0, abyBuffer.Length);
                    ZipEntry objZipEntry = new ZipEntry(Path.GetFileName(strFile));

                    objZipEntry.DateTime = DateTime.Now;
                    objZipEntry.Size = strmFile.Length;
                    strmFile.Close();
                    strmZipOutputStream.PutNextEntry(objZipEntry);
                    strmZipOutputStream.Write(abyBuffer, 0, abyBuffer.Length);

                }

                strmZipOutputStream.Finish();
                strmZipOutputStream.Close();

                try
                {
                    File.Delete(xmlfile);
                }
                catch { }
                //try
                //{
                //    File.Delete(dbfile);
                //}
                //catch { }
            }
            else if (System.IO.Path.GetExtension(path).ToLower() == ".dwxml")
            {
                SaveToXML().Save(path);
            }
            else if (System.IO.Path.GetExtension(path).ToLower() == ".xml")
            {
                SaveToMXML().Save(path);
            }

            ProcessScripts(EventType.SimulationSaved, ObjectType.Simulation, "");

        }

        private string GetTempFileName()
        {
            return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
        }

        /// <inheritdoc/>
        public override void CloseOpenEditForms()
        {

        }

        /// <inheritdoc/>
        public override IFlowsheet Clone()
        {

            var fs = new Flowsheet2(null, null);
            fs.Initialize();
            var xdoc = SaveToXML();
            fs.LoadFromXML(xdoc);
            return fs;

        }

    }
}
