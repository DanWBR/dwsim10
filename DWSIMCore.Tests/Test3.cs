using DWSIMCore.Automation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

class Test3
{
    [STAThread]
    static void Main()
    {
        DoJob().GetAwaiter().GetResult();
    }

    static async Task DoJob()
    {
        //create automation manager

        var autom = new Automation();

        var flowsheets = Assembly.GetExecutingAssembly().GetManifestResourceNames();

        foreach (var flowsheet in flowsheets)
        {
            var extension = Path.GetExtension(flowsheet);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(flowsheet))
            {
                var sim = await autom.LoadFlowsheet(stream, extension.EndsWith("xmz") ? true : false, 
                    (msg, type) => Console.WriteLine(String.Format("[{0}] {1}", flowsheet, msg)));
                //var comps = Newtonsoft.Json.JsonConvert.SerializeObject(sim.AvailableCompounds.Values, Newtonsoft.Json.Formatting.Indented);
                //File.WriteAllText("C:\\Users\\Daniel\\allcomps.json", comps);
                await autom.CalculateFlowsheet2(sim);
                //autom.SaveFlowsheet2(sim, "C:\\Users\\Daniel\\out.dwxml");
            }
        }

        Console.ReadKey();
    }
}
