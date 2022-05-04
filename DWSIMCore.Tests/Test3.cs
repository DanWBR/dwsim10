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
        //create automation manager

        var autom = new Automation();

        var flowsheets = Assembly.GetExecutingAssembly().GetManifestResourceNames();

        foreach (var flowsheet in flowsheets)
        {
            var extension = Path.GetExtension(flowsheet);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(flowsheet))
            {
                var sim = autom.LoadFlowsheet(stream, extension.EndsWith("xmz") ? true : false);
                sim.SetMessageListener((msg, type) => Console.WriteLine(String.Format("[{0}] {1}", flowsheet, msg)));
                autom.CalculateFlowsheet2(sim);
            }
        }

        Console.ReadKey();
    }
}
