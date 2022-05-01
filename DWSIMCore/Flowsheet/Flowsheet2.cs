using DWSIMCore.Foundation;

namespace DWSIMCore.Flowsheet
{
    public class Flowsheet2 : FlowsheetBase
    {
        private Action<string, IFlowsheet.MessageType> listeningaction;

        public override bool SupressMessages { get; set; } = false;

        public Flowsheet2()
        {

            Initialize();

            SaveSpreadsheetData = new Action<XDocument>((xdoc) =>
            {
                //xdoc.Element("DWSIM_Simulation_Data").Add(new XElement("Spreadsheet"));
                //xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Add(new XElement("RGFData"));
                //var tmpfile = System.IO.Path.GetTempFileName();

                //Dictionary<string, string> sdict = new Dictionary<string, string>();
                //foreach (var sheet in Spreadsheet.Worksheets)
                //{
                //    var tmpfile2 = System.IO.Path.GetTempFileName();
                //    sheet.SaveRGF(tmpfile2);
                //    var xmldoc = new XmlDocument();
                //    xmldoc.Load(tmpfile2);
                //    sdict.Add(sheet.Name, Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmldoc));
                //    File.Delete(tmpfile2);
                //}
                //xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("RGFData").Value = Newtonsoft.Json.JsonConvert.SerializeObject(sdict);

            });

            RetrieveSpreadsheetData = new Func<string, List<string[]>>((range) =>
            {
                return null;
                //return Spreadsheet.GetDataFromRange(range);
            });


            DynamicsManager.RunSchedule = (schname) =>
            {
                DynamicsManager.CurrentSchedule = DynamicsManager.GetSchedule(schname).ID;
                return DynamicsIntegratorControl.RunIntegrator(false, false, this, null);
            };

        }

        public override IFlowsheet GetNewInstance()
        {
            var fs = new Flowsheet2();
            return fs;
        }

        public override void UpdateInformation()
        {
            UpdateInterface();
        }

        public override void UpdateInterface()
        {
           
        }
        public override void ShowDebugInfo(string text, int level)
        {
            Console.WriteLine(text);
        }

        public override void ShowMessage(string text, IFlowsheet.MessageType mtype, string exceptionid = "")
        {
                if (listeningaction != null) listeningaction(text, mtype);
        }

        public void WriteMessage(string text)
        {
                listeningaction?.Invoke(text, IFlowsheet.MessageType.Information);
        }

        public override void UpdateOpenEditForms()
        {
           
        }

        public override object GetApplicationObject()
        {
            return null;
        }

        public void SolveFlowsheet(bool wait, ISimulationObject gobj = null, bool changecalcorder = false)
        {

            if (PropertyPackages.Count == 0)
            {
                ShowMessage("Please select a Property Package before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return;
            }

            if (SelectedCompounds.Count == 0)
            {
                ShowMessage("Please select a Compound before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return;
            }

            DWSIM.GlobalSettings.Settings.CalculatorActivated = true;
            DWSIM.GlobalSettings.Settings.SolverMode = 1;
            DWSIM.GlobalSettings.Settings.SolverBreakOnException = true;

            Task st = new Task(() =>
            {
                RequestCalculation(gobj, changecalcorder);
                Task.Delay(1000).Wait();
            });

            st.ContinueWith((t) =>
            {
                DWSIM.GlobalSettings.Settings.CalculatorStopRequested = false;
                DWSIM.GlobalSettings.Settings.CalculatorBusy = false;
                DWSIM.GlobalSettings.Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();

            });


            if (wait)
            {
                try
                {
                    st.Start(TaskScheduler.Default);
                    st.Wait();
                }
                catch (AggregateException aex)
                {
                    foreach (Exception ex2 in aex.InnerExceptions)
                    {
                            ShowMessage(ex2.ToString(), IFlowsheet.MessageType.GeneralError);
                    }
                    DWSIM.GlobalSettings.Settings.CalculatorBusy = false;
                    DWSIM.GlobalSettings.Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
                }
                catch (Exception ex)
                {
                        ShowMessage(ex.ToString(), IFlowsheet.MessageType.GeneralError);
                    DWSIM.GlobalSettings.Settings.CalculatorBusy = false;
                    DWSIM.GlobalSettings.Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
                }
            }
            else
            {
                st.Start(TaskScheduler.Default);
            }

        }

        public void SolveFlowsheet2()
        {

            var surface = ((DWSIM.Drawing.SkiaSharp.GraphicsSurface)this.GetSurface());

            if (PropertyPackages.Count == 0)
            {
                ShowMessage("Please select a Property Package before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return;
            }

            if (SelectedCompounds.Count == 0)
            {
                ShowMessage("Please select a Compound before solving the flowsheet.", IFlowsheet.MessageType.GeneralError);
                return;
            }

            DWSIM.GlobalSettings.Settings.CalculatorActivated = true;

            Task st = new Task(() =>
            {
                RequestCalculation();
            });

            st.ContinueWith((t) =>
            {
                DWSIM.GlobalSettings.Settings.CalculatorStopRequested = false;
                DWSIM.GlobalSettings.Settings.CalculatorBusy = false;
                DWSIM.GlobalSettings.Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
            });

            try
            {
                st.Start(TaskScheduler.Default);
                st.Wait();
            }
            catch (AggregateException aex)
            {
                foreach (Exception ex2 in aex.InnerExceptions)
                {
                        ShowMessage(ex2.ToString(), IFlowsheet.MessageType.GeneralError);
                }
                DWSIM.GlobalSettings.Settings.CalculatorBusy = false;
                DWSIM.GlobalSettings.Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
            }
            catch (Exception ex)
            {
                    ShowMessage(ex.ToString(), IFlowsheet.MessageType.GeneralError);
                DWSIM.GlobalSettings.Settings.CalculatorBusy = false;
                DWSIM.GlobalSettings.Settings.TaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
            }

        }

        public override void SetMessageListener(Action<string, IFlowsheet.MessageType> act)
        {
            listeningaction = act;
        }

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

        public override void RunCodeOnUIThread(Action act)
        {
            act.Invoke();
        }

        public override void DisplayForm(object form)
        {
            throw new NotImplementedException();
        }
        public void SaveSimulation(string path, bool backup = false)
        {

            if (System.IO.Path.GetExtension(path).ToLower() == ".dwxmz")
            {

                path = Path.ChangeExtension(path, ".dwxmz");

                string xmlfile = Path.ChangeExtension(GetTempFileName(), ".xml");

                SaveToXML().Save(xmlfile);

                var dbfile = Path.ChangeExtension(xmlfile, ".db");

                FileDatabaseProvider.ExportDatabase(dbfile);

                var i_Files = new List<string>();
                if (File.Exists(xmlfile))
                    i_Files.Add(xmlfile);
                if (File.Exists(dbfile))
                    i_Files.Add(dbfile);

                ZipOutputStream strmZipOutputStream = default(ZipOutputStream);

                strmZipOutputStream = new ZipOutputStream(File.Create(path));

                strmZipOutputStream.SetLevel(9);

                if (Options.UsePassword)
                    strmZipOutputStream.Password = Options.Password;

                string strFile = null;

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
                try
                {
                    File.Delete(dbfile);
                }
                catch { }
            }
            else if (System.IO.Path.GetExtension(path).ToLower() == ".dwxml")
            {
                SaveToXML().Save(path);
            }
            else if (System.IO.Path.GetExtension(path).ToLower() == ".xml")
            {
                SaveToMXML().Save(path);
            }

            ProcessScripts(DWSIM.Interfaces.Enums.Scripts.EventType.SimulationSaved, DWSIM.Interfaces.Enums.Scripts.ObjectType.Simulation, "");

        }

        private string GetTempFileName()
        {
            return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
        }

    }
}
