# The user interface

Three projects, all net10.0:

| Project | What it is |
|---|---|
| `DWSIM.UI.Shared.Avalonia` | The pieces the editors and the shell both use: the flowsheet canvas, the localization table, the common controls. It does not reference the engine. |
| `DWSIM.UI.Desktop.Editors.Avalonia` | The editing panel of every unit operation, of the material stream, and the compound tools. The assembly is named `DWSIM.UI.Desktop.Editors`, which is the name the flowsheet asks the editor factory for. |
| `DWSIM.UI.Desktop.Avalonia` | The application: shell, docking, menus, the utility and analysis windows, the spreadsheet. |

The spreadsheet is [ReoGrid](https://github.com/DanWBR/ReoGrid), built for Avalonia, in
`external/ReoGrid` as a submodule. Clone with `--recursive`, or run
`git submodule update --init` in a tree that is already there.

## Running it

```
dotnet run --project ui/DWSIM.UI.Desktop.Avalonia
```

A path passed on the command line opens as the first document.

## What this build does not have

The AI assistant and the convergence enhancer panel belong to the Patreon edition and are not
here. `MainWindow.InitializeSupport` stays: it looks for `DWSIM.Support.dll`
next to the executable through reflection and does nothing when it is absent, so the same source
builds both editions.

There is no embedded browser. The Inspector reports and the Markdown reports open in the system
browser or show as text, which is the one HTML renderer every platform is guaranteed to have.
