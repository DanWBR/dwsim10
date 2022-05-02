using DWSIMCore.Automation;
using DWSIMCore.Foundation;
using DWSIMCore.Foundation.Enums.GraphicObjects;
using DWSIMCore.Foundation.PropertyPackages;

namespace AddObjectsToDWSIM
{
    class Test1
    {
        static void Main()
        {
            //create automation manager
            var interf = new Automation();

            IFlowsheet sim;

            //load *.dwxmz empty simulation file
            string fileName = "simulation_template.dwxmz";

            sim = interf.LoadFlowsheet(fileName);

            var c1 = sim.AddObject(ObjectType.Cooler, 100, 100, "COOLER-001");
            var e1 = sim.AddObject(ObjectType.EnergyStream, 130, 150, "HEAT_OUT");
            var m1 = sim.AddObject(ObjectType.MaterialStream, 50, 100, "INLET");
            var m2 = sim.AddObject(ObjectType.MaterialStream, 150, 100, "OUTLET");

            // create the graphic object connectors manually as they are not being drawn on screen. 

            ((dynamic)c1.GraphicObject).PositionConnectors();
            ((dynamic)m1.GraphicObject).PositionConnectors();
            ((dynamic)m2.GraphicObject).PositionConnectors();
            ((dynamic)e1.GraphicObject).PositionConnectors();

            // connect the graphic objects.

            sim.ConnectObjects(m1.GraphicObject, c1.GraphicObject, 0, 0);
            sim.ConnectObjects(c1.GraphicObject, m2.GraphicObject, 0, 0);
            sim.ConnectObjects(c1.GraphicObject, e1.GraphicObject, 0, 0);

            // create and add an instance of PR Property Package

            var pr = new PengRobinsonPropertyPackage();
            pr.ComponentName = "Peng-Robinson (PR)";
            pr.ComponentDescription = "Any Description"; // <-- important to set any text as description.

            sim.AddPropertyPackage(pr);

            m1.PropertyPackage = sim.PropertyPackages.Values.First();
            m2.PropertyPackage = sim.PropertyPackages.Values.First();
            c1.PropertyPackage = sim.PropertyPackages.Values.First();

            // request a calculation

            sim.RequestCalculation();

            // save file as dwxmz (compressed XML)

            string fileNameToSave = "created_file.dwxmz";
            interf.SaveFlowsheet(sim, fileNameToSave, true);

        }
    }
}