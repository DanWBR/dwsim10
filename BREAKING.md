# Breaking changes for extension authors

The engine no longer references `System.Windows.Forms` or `System.Drawing`. That is what makes it
run on Linux and macOS, and it is where every change in this list comes from: any place where an
interface named a WinForms or GDI+ type had to name something else.

The same changes are in the Patreon edition, so an extension only has to be updated once.

## Interfaces that changed shape

| Member | Was | Is |
|---|---|---|
| `IExtender.SetMainWindow` | `System.Windows.Forms.Form` | `Object` |
| `IExtender.DisplayImage` | `System.Drawing.Bitmap` | `Byte()`, the bytes of a PNG |
| `ISimulationObject.GetEditingForm` | `System.Windows.Forms.Form` | `Object` |
| `ISplashScreen.GetSplashScreen` | `System.Windows.Forms.Form` | `Object` |
| `IWelcomeScreen.GetWelcomeScreen` | `System.Windows.Forms.UserControl` | `Object` |
| `IWelcomeScreen.SetMainForm` | `System.Windows.Forms.Form` | `Object` |

An extension that returns a `Form` keeps working: the host casts it back. What breaks is the
declaration, which no longer compiles until the parameter or return type is widened.

`DisplayImage` is the one that needs a real edit. Instead of returning a `Bitmap`, return the
bytes of a PNG:

```vb
Public ReadOnly Property DisplayImage As Byte() Implements IExtender.DisplayImage
    Get
        Using stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MyExtension.icon.png")
            Using memory As New MemoryStream()
                stream.CopyTo(memory)
                Return memory.ToArray()
            End Using
        End Using
    End Get
End Property
```

## Members that are gone

| Removed | Use instead |
|---|---|
| `ISimulationObject.GetIconBitmap` | `GetIconBitmapBytes`, which returns the bytes of a PNG |
| `IFlowsheet.GetIconAsBitmap` | nothing; ask the object for its bytes |
| `IFileDatabaseProvider.GetFileAsImage` | `GetFileStream`, and decode the bytes in the host |
| `PropertyPackage.GetDisplayIcon` | `GetIconBitmapBytes` on the object that owns the package |
| `ObjectCopy` on `Reaction`, `ReactionSet`, `Assay`, `OptimizationCase`, `SensitivityAnalysisCase`, `SpreadsheetCellParameters`, `ConstantProperties`, `InteractionParameter` and the simulation object base class | `CloneXML`, or the type's own `Clone`. The old ones went through `BinaryFormatter`, which .NET no longer allows |
| `CAPEOPENManager.GetPropertyPackage(name)` and `GetPropertyPackageList()` | `PropertyPackages.PropertyPackageFactory.Create(name)` and `.Names()`. Building a property package no longer means instantiating a COM object |
| `MathEx.lpsolve55` | `MathEx.LinearProgramming.Simplex.Minimize`, which is managed and needs no native library |
| `PropertyPackages.STEAM67` | nothing; it had no callers and its native library was Windows-only |
| `MathEx.OptimizationL.*` (the LibOptimization wrappers) and `AutoDiff` | nothing; they had no callers in either edition |

## Members that stayed but do nothing on their own

`DisplayEditForm`, `UpdateEditForm` and `CloseEditForm` on the simulation object base class are
still there and are still `Overridable`, but their bodies in the engine are empty. The WinForms
editor of each unit operation now lives in a second half of the same class, under
`EditingForms/Partials/`, which only the Patreon edition compiles. An extension that overrides
them is unaffected.

## What the open edition does not carry

These exist in the Patreon edition and are absent here. Code that calls them compiles and throws
a `NotSupportedException` that says so.

- The refining, electrolyte and extension-pack unit operations, and their fluent builders
- `Flowsheet.RunLCA` and `Flowsheet.RunTEA`
- `License.Activate`; `License.RequirePlus` remains and refuses
- The GPU surface of the fluent API and the GPU path of the column solver
- The spreadsheet unit operation, which drives Excel. Reading a flowsheet that contains one
  throws instead of loading it silently wrong
- The AI assistant and the convergence enhancer panel. `IAIAssistedConvergenceManager` is still
  in the engine, so an extension that implements it still loads

## Numerical back-ends that need a native library

IPOPT and CoolProp are native, and this repository carries neither binary. Everything that
reaches them fails with a message naming the one that is missing: Gibbs energy minimisation and
binary interaction parameter regression for IPOPT, the CoolProp property packages for CoolProp.
Nothing else in the engine needs a native library any more.

## Framework

The engine targets .NET 10. An extension built for .NET Framework 4.7.2 will not load into the
open edition; rebuild it for `net10.0`, or for `netstandard2.0` if the same binary has to load
into both editions.
