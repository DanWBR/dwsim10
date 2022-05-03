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
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(flowsheet)
            {
                var sim = autom.LoadFlowsheet(stream, false);
                sim.SetMessageListener((msg, type) => Console.WriteLine(msg));
                autom.CalculateFlowsheet2(sim);
            }
        }

        Console.ReadKey();
    }
}
