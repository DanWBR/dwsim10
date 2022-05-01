using System.Xml.Linq;
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

        public IFlowsheet LoadFlowsheet(string filepath)
        {
            Settings.AutomationMode = true;
            Settings.CultureInfo = "en";
            Console.WriteLine("Initializing the Flowsheet, please wait...");
            var fsheet = new Flowsheet();
            Console.WriteLine("Loading Flowsheet data, please wait...");
            if (System.IO.Path.GetExtension(filepath).ToLower().EndsWith("z"))
            {
                fsheet.LoadZippedXML(filepath);
            }
            else
            {
                fsheet.LoadFromXML(XDocument.Load(filepath));
            }
            return fsheet;
        }

        public void ReleaseResources()
        {

        }

        public void SaveFlowsheet(IFlowsheet flowsheet, string filepath, bool compressed)
        {
            Console.WriteLine("Saving the Flowsheet, please wait...");
            ((Flowsheet)flowsheet).SaveSimulation(filepath);
        }

        public void CalculateFlowsheet(IFlowsheet flowsheet, ISimulationObject sender)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            Settings.SolverMode = 1;
            Settings.EnableGPUProcessing = false;
            Settings.EnableParallelProcessing = true;
            Console.WriteLine("Solving Flowsheet, please wait...");
            ((Flowsheet)flowsheet).SolveFlowsheet2();
        }

        public List<Exception> CalculateFlowsheet2(IFlowsheet flowsheet)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            Settings.SolverMode = 1;
            Settings.SolverTimeoutSeconds = 120;
            Settings.EnableGPUProcessing = false;
            Settings.EnableParallelProcessing = true;
            return FlowsheetSolver.SolveFlowsheet(flowsheet, Settings.SolverMode);
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

        public IFlowsheet CreateFlowsheet()
        {
            Settings.AutomationMode = true;
            Console.WriteLine("Initializing the Flowsheet, please wait...");
            return new Flowsheet();
        }

        public object GetMainWindow()
        {
            throw new NotImplementedException();
        }

    }

}
