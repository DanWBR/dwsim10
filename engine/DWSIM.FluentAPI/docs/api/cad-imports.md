# CAD Imports

The three imports of the CAD Tools menu are available to scripts through `CadImport`, in the
`DWSIM.Extensions.SolidCraft` extension. They are extension methods on the underlying `IFlowsheet`,
so from the FluentAPI they are called on `fs.Inner`:

```csharp
using DWSIM.Extensions.SolidCraft;

var fs = Flowsheet.Create("from drawings");

// a flowsheet from a PFD or P&ID: image, PDF, DXF or DWG
var drawing = fs.Inner.ImportFromDrawing("unit-100-pfd.pdf", solve: true);
Console.WriteLine(drawing.Summary());

// the route of a piping isometric into a pipe segment (a new one, named after the line, when no tag is given)
var iso = fs.Inner.ImportPipeFromIsometric("iso-2-P-1.pdf", pipeTag: "PIPE-1");

// equipment set up from its data sheet: the item tagged, else the one the sheet names, else a new one
var sheet = fs.Inner.SetUpFromDataSheet("P-101-datasheet.pdf", equipmentTag: "P-101");
```

From Python the same methods are called on the class:

```python
clr.AddReference("DWSIM.Extensions.SolidCraft")
from DWSIM.Extensions.SolidCraft import CadImport
session = CadImport.ImportFromDrawing(fs.Inner, "unit-100-pfd.pdf", True)
```

## The AI Assistant, or a saved reading

A document is read by the AI Assistant, which must be running with a model that reads images, as for
the wizards. Each method also takes a reading saved as `.json` (Save Reading in the wizards, or
`session.SaveReading(path)` after a call), which needs no assistant and gives the same result every
time. Saved readings are the way to run the imports in tests and batch jobs.

## Options

| Method | Options |
|---|---|
| `ImportFromDrawing` | `solve` (solve and compare with the stream table, default on), `propertyPackage` and `compounds` (instead of the ones the reading suggests), `fitToTable` (Patreon Edition: fit the equipment efficiencies and column reflux ratios to the table), `kind` (`pfd`, `pid` or `auto`) |
| `ImportPipeFromIsometric` | `pipeTag` (the pipe segment to fill; a new one when omitted) |
| `SetUpFromDataSheet` | `equipmentTag`, `operatingCase` (the sheet's case to use; the first one by default) |

All three take `progress`, a callback for the step being run, and `cancel`, a cancellation token.

## What comes back

Each method returns the wizard's session, with the reading and the results:

- `DrawingImportSession`: `Reading`, `Check()` (the checks run before building), `Built` (the items and
  streams made), `Solve` (the solver's messages and the comparison with the stream table), `Summary()`.
  Errors in the checks stop the build with an `InvalidOperationException` that lists them.
- `IsometricImportSession`: `Reading`, `Sections` (one per leg), `Issues` (the checks against the bill
  of materials and the elevations), `Applied` (the pipe segment).
- `DataSheetImportSession`: `Reading`, `Target` (the equipment), `Changes` (each value set, with the one
  it replaced), `Issues` (the sheet checked against itself and the operating point), `Results` (the
  changes made and the outlet compared with the sheet after solving).
