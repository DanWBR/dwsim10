# Automation

Automation enables external programs to control DWSIM programmatically—creating flowsheets, setting operating conditions, running the solver, and extracting results without manual interaction. Through DWSIM’s automation interface, you can:

- Create applications and programming tools that expose objects.

- Create and manipulate objects exposed in one application from another application.

- Create tools that access and manipulate objects.

On Windows, DWSIM’s automation layer is exposed through COM (Component Object Model) interfaces. Any COM-compatible client—such as Excel VBA, C#, VB.NET, Python (via win32com or pythonnet), or MATLAB—can instantiate DWSIM objects, call their methods, and read their properties. On .NET environments, the DLLs can be referenced directly without COM registration.

#### Automation support in DWSIM

Since version 4.2, DWSIM exposes its principal classes and interfaces for automation via COM and .NET. This enables users to build, modify, and solve flowsheets programmatically—for example, driving parametric sweeps or optimization studies from Microsoft Excel without opening the DWSIM GUI. Coupling DWSIM with Excel through VBA macros provides a powerful workflow for process design, optimization, and techno-economic evaluation.

Simulation results can be exported to Excel spreadsheets for developing Heat and Material Balance (H&MB) tables, enabling straightforward post-processing and reporting using standard engineering workflows.

#### Registering DLLs for COM Automation

You can register DWSIM DLLs for automation during the installation process. You can also run the **automation_reg.bat** batch file (located in DWSIM’s current installation directory) with admin privileges to register. To de-register, run **automation_unreg.bat** also as admin. When you uninstall DWSIM, the DLLs are automatically deregistered.

If your automation project is based on a .NET language, there’s no need to register the DLLs. You’ll only need to add a reference to them.

Automating DWSIM through COM is limited to Windows, though .NET is recommended as the default mechanism. On Linux and macOS the same .NET assemblies are used, running on the cross-platform .NET runtime.

Introduction to Interfaces Before proceeding, read this text to get used to Interfaces and their implementation in actual Classes: [Interfaces in Object-Oriented Programming](http://www.cs.utah.edu/~germain/PPS/Topics/interfaces.html)

#### API Reference Documentation

Automation Class: [http://dwsim.inforside.com.br/api_help60/html/N_DWSIM_Automation.htm ](http://dwsim.inforside.com.br/api_help60/html/N_DWSIM_Automation.htm ){.uri}

Interface Definitions: [http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_Interfaces.htm ](http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_Interfaces.htm ){.uri}

Unit Operations: [http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_UnitOperations.htm ](http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_UnitOperations.htm ){.uri}

Thermodynamics: [http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_Thermodynamics.htm ](http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_Thermodynamics.htm ){.uri}

Base Class Shared Library: [http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_SharedClasses.htm ](http://dwsim.inforside.com.br/api_help60/html/G_DWSIM_SharedClasses.htm ){.uri}

Flowsheet GUI and DWSIM main executable: [http://dwsim.inforside.com.br/api_help60/html/N_DWSIM.htm ](http://dwsim.inforside.com.br/api_help60/html/N_DWSIM.htm ){.uri}

CAPE-OPEN Reference: <http://www.colan.org/specifications/>

#### DWSIM Flowsheet Class Structure




![](images/screens64/Flowsheet_class_structure.png)



The Flowsheet class in DWSIM provides access to all objects in the simulation:

- **Thermodynamics Subsystem:** includes the Compounds, Property Packages and Reactions/Reaction Sets collections.

- **Simulation Objects Subsystem:** includes Material & Energy Streams and Unit Operation blocks.

- **Graphical User Interface:** provides access to the displayed objects in the flowsheet and the connections between them.

- **Accessories:** includes added utilities, sensitivity & optimization studies, system of units definitions and other simulation definitions.

The Flowsheet object in DWSIM implements various interfaces, including **IFlowsheet** and **IFlowsheetOptions**.

- **IFlowsheet:** this is the main interface implemented by the Flowsheet class. It provides direct access to the various flowsheet components and helper functions to manipulate objects. <http://dwsim.inforside.com.br/api_help60/html/T_DWSIM_Interfaces_IFlowsheet.htm>

- **IFlowsheetOptions:** this interface defines the flowsheet settings and other properties. <http://dwsim.inforside.com.br/api_help60/html/T_DWSIM_Interfaces_IFlowsheetOptions.htm>

When you use the Automation class to load a simulation, an IFlowsheet object is returned, which is actually an instance of the Flowsheet class. You can cast the returned object to any of the interfaces implemented by the Flowsheet class to access all available functions, properties and procedures.

#### Sample Automation

This sample automation code will run Cavett’s Problem (simulation file located in the samples folder) with four different feed mass flow values, check outlet mass flows and calculate the mass balance of the flowsheet, displaying the results to the user.

##### About Cavett’s Problem

A simulation problem proposed by Cavett (1963) has been used to test various chemical engineering simulation programs. It provides a useful benchmark to compare and contrast various tear stream locations and convergence algorithms. The process is equivalent to a four theoretical stage near isothermal distillation flash tanks.




![](images/screens64/Cavett.jpg)



- Feed Stream: 2

- Vapor Outlet Stream: 8

- Liquid Outlet Stream: 18

##### Excel VBA

To run this sample, create a new Excel VBA project and add a reference to **CAPE-OPEN 1.1 Type Library** (<http://www.colan.org/software-tools/cape-open-type-libraries-and-primary-interop-assemblies/>), **DWSIM Simulator Automation Interface** and **DWSIM Simulator Interface Definitions Library**.

    Public Sub Sub1()
          
        'create automation manager
        Dim interf As DWSIM_Automation.Automation
        Set interf = New DWSIM_Automation.Automation
                 
        'declare the flowsheet variable
        Dim sim As DWSIM_Interfaces.IFlowsheet
      
        'load Cavett's Problem simulation file
        Set sim = interf.LoadFlowsheet(Application.ActiveWorkbook.Path & "\Cavett's Problem.dwxml")
                         
        'use CAPE-OPEN interfaces to manipulate objects
        Dim feed As CAPEOPEN110.ICapeThermoMaterialObject
        Dim vap_out As CAPEOPEN110.ICapeThermoMaterialObject
        Dim liq_out As CAPEOPEN110.ICapeThermoMaterialObject
          
        Set feed = sim.GetFlowsheetSimulationObject("2")
        Set vap_out = sim.GetFlowsheetSimulationObject("8")
        Set liq_out = sim.GetFlowsheetSimulationObject("18")
          
        'mass flow rate values in kg/s
        Dim flows(4) As Variant
          
        flows(0) = 170#
        flows(1) = 180#
        flows(2) = 190#
        flows(3) = 200#
          
        'vapor and liquid flows
        Dim vflow, lflow As Double
          
        For i = 0 To 3
            'set feed mass flow
            Call feed.SetProp("totalflow", "overall", Nothing, "", "mass", Array(flows(i)))
            'calculate the flowsheet (run the simulation)
            MsgBox "Running simulation with F = " & flows(i) & " kg/s, please wait..."
            Call interf.CalculateFlowsheet(sim, Nothing)
            'check for errors during the last run
            If sim.Solved = False Then
                MsgBox "Error solving flowsheet: " & sim.ErrorMessage
            End If
            'get vapor outlet mass flow value
            vflow = vap_out.GetProp("totalflow", "overall", Nothing, "", "mass")(0)
            'get liquid outlet mass flow value
            lflow = liq_out.GetProp("totalflow", "overall", Nothing, "", "mass")(0)
            'display results
            MsgBox "Simulation run #" & (i + 1) & " results:" & vbCrLf & "Feed: " & flows(i) & ", Vapor: " & vflow & ", Liquid: " & lflow & " kg/s" & vbCrLf & "Mass balance error: " & (flows(i) - vflow - lflow) & " kg/s"
        Next
          
        MsgBox "Finished OK!"
                  
    End Sub

##### VB

To run this sample, create a new VB.NET Console Application project and add a reference to DWSIM.Automation.dll, DWSIM.Interfaces.dll and CapeOpen.dll.

    Module Module1
      
        Sub Main()
      
            System.IO.Directory.SetCurrentDirectory("C:/Program Files/DWSIM6") ' replace with DWSIM's installation directory on your computer
     
            'create automation manager
            Dim interf As New DWSIM.Automation.Automation
      
            Dim sim As Interfaces.IFlowsheet
      
            'load Cavett's Problem simulation file
            sim = interf.LoadFlowsheet("samples" & IO.Path.DirectorySeparatorChar & "Cavett's Problem.dwxml")
      
            '(optional) set a listener to catch solver messages
            sim.SetMessageListener(Sub(msg As String)
                                       Console.WriteLine(msg)
                                   End Sub)
      
            'use CAPE-OPEN interfaces to manipulate objects
            Dim feed, vap_out, liq_out As CapeOpen.ICapeThermoMaterialObject
      
            feed = sim.GetFlowsheetSimulationObject1("2")
            vap_out = sim.GetFlowsheetSimulationObject1("8")
            liq_out = sim.GetFlowsheetSimulationObject1("18")
      
            'mass flow rate values in kg/s
            Dim flows(3) As Double
      
            flows(0) = 170.0#
            flows(1) = 180.0#
            flows(2) = 190.0#
            flows(3) = 200.0#
      
            'vapor and liquid flows
            Dim vflow, lflow As Double
      
            For i = 0 To flows.Length - 1
                'set feed mass flow
                feed.SetProp("totalflow", "overall", Nothing, "", "mass", New Double() {flows(i)})
                'calculate the flowsheet (run the simulation)
                Console.WriteLine("Running simulation with F = " & flows(i) & " kg/s, please wait...")
                interf.CalculateFlowsheet(sim, Nothing)
                'check for errors during the last run
                If sim.Solved = False Then
                    Console.WriteLine("Error solving flowsheet: " & sim.ErrorMessage)
                End If
                'get vapor outlet mass flow value
                vflow = vap_out.GetProp("totalflow", "overall", Nothing, "", "mass")(0)
                'get liquid outlet mass flow value
                lflow = liq_out.GetProp("totalflow", "overall", Nothing, "", "mass")(0)
                'display results
                Console.WriteLine("Simulation run #" & (i + 1) & " results:" & vbCrLf & "Feed: " & flows(i) & ", Vapor: " & vflow & ", Liquid: " & lflow & " kg/s" & vbCrLf & "Mass balance error: " & (flows(i) - vflow - lflow) & " kg/s")
            Next
      
            Console.WriteLine("Finished OK! Press any key to close.")
            Console.ReadKey()
      
        End Sub
      
    End Module

##### C#

To run this sample, create a new C# Console Application project and add a reference to **DWSIM.Automation.dll**, **DWSIM.Interfaces.dll** and **CapeOpen.dll**.

    using System;
     
    static class Module1
    {
     
        public static void Main()
        {
     
            System.IO.Directory.SetCurrentDirectory("C:/Program Files/DWSIM6"); // replace with DWSIM's installation directory on your computer
     
            //create automation manager
            DWSIM.Automation.Automation interf = new DWSIM.Automation.Automation();
     
            DWSIM.Interfaces.IFlowsheet sim;
     
            //load Cavett's Problem simulation file
            sim = interf.LoadFlowsheet("samples" + System.IO.Path.DirectorySeparatorChar + "Cavett's Problem.dwxml");
     
            //use CAPE-OPEN interfaces to manipulate objects
            CapeOpen.ICapeThermoMaterialObject feed, vap_out, liq_out;
     
            feed = (CapeOpen.ICapeThermoMaterialObject)sim.GetFlowsheetSimulationObject("2");
            vap_out = (CapeOpen.ICapeThermoMaterialObject)sim.GetFlowsheetSimulationObject("8");
            liq_out = (CapeOpen.ICapeThermoMaterialObject)sim.GetFlowsheetSimulationObject("18");
     
            //mass flow rate values in kg/s
            double[] flows = new double[4];
     
            flows[0] = 170.0;
            flows[1] = 180.0;
            flows[2] = 190.0;
            flows[3] = 200.0;
     
            //vapor and liquid flows
            double vflow = 0;
            double lflow = 0;
     
            for (var i = 0; i <= flows.Length - 1; i++)
            {
                //set feed mass flow
                feed.SetProp("totalflow", "overall", null, "", "mass", new double[] { flows[i] });
                //calculate the flowsheet (run the simulation)
                Console.WriteLine("Running simulation with F = " + flows[i] + " kg/s, please wait...");
                interf.CalculateFlowsheet(sim, null);
                //check for errors during the last run
                if (sim.Solved == false)
                {
                    Console.WriteLine("Error solving flowsheet: " + sim.ErrorMessage);
                }
                //get vapor outlet mass flow value
                vflow = ((double[])vap_out.GetProp("totalflow", "overall", null, "", "mass"))[0];
                //get liquid outlet mass flow value
                lflow = ((double[])liq_out.GetProp("totalflow", "overall", null, "", "mass"))[0];
                //display results
                Console.WriteLine("Simulation run #" + (i + 1) + " results:\nFeed: " + flows[i] + ", Vapor: " + vflow + ", Liquid: " + lflow + " kg/s\nMass balance error: " + (flows[i] - vflow - lflow) + " kg/s");
            }
     
            Console.WriteLine("Finished OK! Press any key to close.");
            Console.ReadKey();
     
        }
     
    }

##### Python

    import pythoncom
    pythoncom.CoInitialize()

    import clr

    from System.IO import Directory, Path, File
    from System import String, Environment

    dwsimpath = "C:\\Program Files\\DWSIM6\\"

    clr.AddReference(dwsimpath + "CapeOpen.dll")
    clr.AddReference(dwsimpath + "DWSIM.Automation.dll")
    clr.AddReference(dwsimpath + "DWSIM.Interfaces.dll")
    clr.AddReference(dwsimpath + "DWSIM.GlobalSettings.dll")
    clr.AddReference(dwsimpath + "DWSIM.SharedClasses.dll")
    clr.AddReference(dwsimpath + "DWSIM.Thermodynamics.dll")
    clr.AddReference(dwsimpath + "DWSIM.UnitOperations.dll")

    clr.AddReference(dwsimpath + "DWSIM.Inspector.dll")
    clr.AddReference(dwsimpath + "DWSIM.MathOps.dll")
    clr.AddReference(dwsimpath + "TcpComm.dll")
    clr.AddReference(dwsimpath + "Microsoft.ServiceBus.dll")

    from DWSIM.Interfaces.Enums.GraphicObjects import ObjectType
    from DWSIM.Thermodynamics import Streams, PropertyPackages
    from DWSIM.UnitOperations import UnitOperations
    from DWSIM.Automation import Automation2
    from DWSIM.GlobalSettings import Settings

    Directory.SetCurrentDirectory(dwsimpath)

    # create automation manager

    interf = Automation2()

    sim = interf.CreateFlowsheet()

    # add water

    water = sim.AvailableCompounds["Water"]

    sim.SelectedCompounds.Add(water.Name, water)

    # create and connect objects

    m1 = sim.AddObject(ObjectType.MaterialStream, 50, 50, "inlet")
    m2 = sim.AddObject(ObjectType.MaterialStream, 150, 50, "outlet")
    e1 = sim.AddObject(ObjectType.EnergyStream, 100, 50, "power")
    h1 = sim.AddObject(ObjectType.Heater, 100, 50, "heater")

    sim.ConnectObjects(m1.GraphicObject, h1.GraphicObject, -1, -1)
    sim.ConnectObjects(h1.GraphicObject, m2.GraphicObject, -1, -1)
    sim.ConnectObjects(e1.GraphicObject, h1.GraphicObject, -1, -1)

    sim.AutoLayout()

    # steam tables property package

    stables = PropertyPackages.SteamTablesPropertyPackage()

    sim.AddPropertyPackage(stables)

    # set inlet stream temperature
    # default properties: T = 298.15 K, P = 101325 Pa, Mass Flow = 1 kg/s

    m1.SetTemperature(300) # K
    m1.SetMassFlow(100) # kg/s

    # set heater outlet temperature

    h1.CalcMode = UnitOperations.Heater.CalculationMode.OutletTemperature
    h1.OutletTemperature = 400 # K

    # request a calculation

    Settings.SolverMode = 0

    errors = interf.CalculateFlowsheet2(sim)

    print(String.Format("Heater Heat Load: {0} kW", h1.DeltaQ))

    # save file

    fileNameToSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "heatersample.dwxmz")

    interf.SaveFlowsheet(sim, fileNameToSave, True)

    # save the pfd to an image and display it

    clr.AddReference(dwsimpath + "SkiaSharp.dll")
    clr.AddReference("System.Drawing")

    from SkiaSharp import SKBitmap, SKImage, SKCanvas, SKEncodedImageFormat
    from System.IO import MemoryStream
    from System.Drawing import Image
    from System.Drawing.Imaging import ImageFormat

    PFDSurface = sim.GetSurface()

    bmp = SKBitmap(1024, 768)
    canvas = SKCanvas(bmp)
    canvas.Scale(1.0)
    PFDSurface.UpdateCanvas(canvas)
    d = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100)
    str = MemoryStream()
    d.SaveTo(str)
    image = Image.FromStream(str)
    imgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pfd.png")
    image.Save(imgPath, ImageFormat.Png)
    str.Dispose()
    canvas.Dispose()
    bmp.Dispose()

    from PIL import Image

    im = Image.open(imgPath)
    im.show()

