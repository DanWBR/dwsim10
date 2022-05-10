using DWSIMCore.Automation;
using DWSIMCore.Foundation;
using DWSIMCore.Foundation.Enums.GraphicObjects;
using DWSIMCore.Foundation.PropertyPackages;
using DWSIMCore.Foundation.Streams;
using DWSIMCore.Foundation.UnitOperations;

class Test2
{
    [STAThread]
    static async void Main()
    {
        //create automation manager

        var interf = new  Automation();

        var sim = await interf.CreateFlowsheet();

        // add water

        var water = sim.AvailableCompounds["Water"];

        sim.SelectedCompounds.Add(water.Name, water);

        var m1 = (MaterialStream)sim.AddObject(ObjectType.MaterialStream, 50, 50, "inlet");
        var m2 = (MaterialStream)sim.AddObject(ObjectType.MaterialStream, 150, 50, "outlet");
        var e1 = sim.AddObject(ObjectType.EnergyStream, 100, 50, "power");
        var h1 = (Heater)sim.AddObject(ObjectType.Heater, 100, 50, "heater");

        sim.ConnectObjects(m1.GraphicObject, h1.GraphicObject, -1, -1);
        sim.ConnectObjects(h1.GraphicObject, m2.GraphicObject, -1, -1);
        sim.ConnectObjects(e1.GraphicObject, h1.GraphicObject, -1, -1);

        sim.AutoLayout();

        // steam table sproperty package

        var stables = new  SteamTablesPropertyPackage();

        sim.AddPropertyPackage(stables);

        // set inlet stream temperature
        // default properties: T = 298.15 K, P = 101325 Pa, Mass Flow = 1 kg/s

        m1.SetTemperature(300); // K
        m1.SetMassFlow(100); // kg/s

        // set heater outlet temperature

        h1.CalcMode = Heater.CalculationMode.OutletTemperature;
        h1.OutletTemperature = 400; // K

        // request a calculation

        interf.CalculateFlowsheet2(sim);

        Console.WriteLine(String.Format("Heater Heat Load: {0} kW", h1.DeltaQ.GetValueOrDefault()));

        // save file

        string fileNameToSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "heatersample.dwxml");
        interf.SaveFlowsheet(sim, fileNameToSave, false); //use true for dwxmz

        Console.WriteLine("Done! press any key to close.");
        Console.ReadKey();

    }
}