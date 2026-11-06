# CAD Tools from Scripts

The `CadExport` class of the `DWSIM.Extensions.SolidCraft` assembly runs the same exports from a script, without the dialogs. It uses the options of the settings, which its `Options` property reads and writes, and finds SolidCraft and CADCraft as the dialogs do; a folder or file left out is the one the menu would use. From the Script Manager, with the flowsheet at hand as `Flowsheet`:

    import clr
    clr.AddReference("DWSIM.Extensions.SolidCraft")
    from DWSIM.Extensions.SolidCraft import CadExport

    o = CadExport.Options
    o.DrawingStandard = "ASME"
    o.SavePng = False
    o.ProjectNumber = "P-1234"
    CadExport.Options = o

    # two equipment drawings, then everything with the drawing set
    CadExport.ExportEquipmentDrawings(Flowsheet, r"C:\Projects\Plant\drawings", "V-101", "P-101")
    print(CadExport.ExportAllDrawings(Flowsheet, r"C:\Projects\Plant\drawings"))

From Python with the Automation API (pythonnet), pass the `Inner` flowsheet of the FluentAPI. The methods:

|  |  |
|:---|:---|
| **Method** | **Does** |
| **`ExportPlantModel(flowsheet, folder)`** | Builds the 3D plant model. |
| **`UpdatePlantModel(flowsheet, folder)`** | Sends the operating data to the 3D model. |
| **`ReadMovesFromPlantModel(flowsheet, folder)`** | Takes the places of the items moved in SolidCraft into the simulation. |
| **`ExportPFD`, `ExportPID`, `ExportPlotPlan` (flowsheet, dxf)** | The drawings of one sheet. |
| **`ExportEquipmentDrawings`, `ExportPipingIsometrics`, `ExportDataSheets` (flowsheet, folder, tags)** | The drawings of one sheet per item, line or loop; all of them when no tag is given. |
| **`ExportAllDrawings(flowsheet, folder)`** | Everything, and returns the drawing set file. |

