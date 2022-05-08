using System.Xml.Linq;
using DWSIMCore.Flowsheet;
using DWSIMCore.Foundation;

namespace DWSIMCore.Automation
{

    public class Automation
    {

        public Automation()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            Console.WriteLine("Initializing DWSIM Automation Interface...");
            FlowsheetBase.AddPropPacks();
            Console.WriteLine("DWSIM Automation Interface initialized successfully.");
        }

        public async Task<IFlowsheet> LoadFlowsheet(Stream filestream, bool isXMZ, 
            Action<string, IFlowsheet.MessageType> msgListener, 
            Action<int, string>? ProgressCallback = null)
        {
            Settings.AutomationMode = true;
            Settings.CultureInfo = "en";
            Console.WriteLine("Initializing the Flowsheet, please wait...");
            var fsheet = new Flowsheet2(msgListener);
            await fsheet.Init(ProgressCallback);
            Console.WriteLine("Loading Flowsheet data, please wait...");
            if (isXMZ)
            {
                await fsheet.LoadZippedXML(filestream, ProgressCallback);
            }
            else
            {
                await fsheet.LoadFromXML2(XDocument.Load(filestream), ProgressCallback);
            }
            return fsheet;
        }

        public async Task<IFlowsheet> LoadFlowsheet(string filepath)
        {
            Settings.AutomationMode = true;
            Settings.CultureInfo = "en";
            Console.WriteLine("Initializing the Flowsheet, please wait...");
            var fsheet = new Flowsheet2(null);
            await fsheet.Init();
            Console.WriteLine("Loading Flowsheet data, please wait...");
            if (System.IO.Path.GetExtension(filepath).ToLower().EndsWith("z"))
            {
                await fsheet.LoadZippedXML(filepath);
            }
            else
            {
                await fsheet.LoadFromXML2(XDocument.Load(filepath));
            }
            return fsheet;
        }

        public void ReleaseResources()
        {

        }

        public void SaveFlowsheet(IFlowsheet flowsheet, string filepath, bool compressed)
        {
            Console.WriteLine("Saving the Flowsheet, please wait...");
            ((Flowsheet2)flowsheet).SaveSimulation(filepath);
        }

        public void CalculateFlowsheet(IFlowsheet flowsheet, ISimulationObject sender, Action<string>? ProgressCallback = null)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            Settings.SolverMode = 1;
            Settings.EnableGPUProcessing = false;
            Settings.EnableParallelProcessing = true;
            Console.WriteLine("Solving Flowsheet, please wait...");
            ((Flowsheet2)flowsheet).SolveFlowsheet2(ProgressCallback);
        }

        public async Task<List<Exception>> CalculateFlowsheet2(IFlowsheet flowsheet, Action<string>? ProgressCallback = null)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            Settings.SolverMode = 1;
            Settings.SolverTimeoutSeconds = 10000000;
            Settings.EnableGPUProcessing = false;
            Settings.EnableParallelProcessing = false;
            return await ((Flowsheet2)flowsheet).SolveFlowsheet(ProgressCallback);
        }

        public List<Exception> CalculateFlowsheet3(IFlowsheet flowsheet, int timeout_seconds)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            Settings.SolverMode = 1;
            Settings.SolverTimeoutSeconds = timeout_seconds;
            Settings.EnableGPUProcessing = false;
            Settings.EnableParallelProcessing = true;
            return FlowsheetSolver.SolveFlowsheet(flowsheet, Settings.SolverMode);
        }

        public void SaveFlowsheet2(IFlowsheet flowsheet, string filepath)
        {
            SaveFlowsheet(flowsheet, filepath, true);
        }

        public async Task<IFlowsheet> CreateFlowsheet()
        {
            Settings.AutomationMode = true;
            Console.WriteLine("Initializing the Flowsheet, please wait...");
            var f = new Flowsheet2(null);
            await f.Init();
            return f;
        }

        public object GetMainWindow()
        {
            throw new NotImplementedException();
        }

    }

}
