# ExternalCatalog

`DWSIM.Automation.FluentAPI.ExternalCatalog`

Canonical display-name constants for every `DWSIM.Interfaces.IExternalUnitOperation` (bioprocess, refining, electrolyte and other Plus components) registered through `IFlowsheet.AvailableSimulationObjects`. Names match each UO's `GetDisplayName()` exactly and round-trip with [`AvailableExternalUnitOperationNames`](dwsim-automation-fluentapi-flowsheet.md).

## Remarks

Use these constants with [`AddExternalUnitOperation`](dwsim-automation-fluentapi-flowsheet.md) or with the typed `AddX` methods on [`Flowsheet`](dwsim-automation-fluentapi-flowsheet.md). [`RequiresPlus`](dwsim-automation-fluentapi-externalcatalog.md) answers whether a name needs [`Activate`](dwsim-automation-fluentapi-license.md).

## Methods

### `RequiresPlus(string)`

True when `displayName` matches a Plus / DWSIMPlus component (refining, electrolyte ops, advanced HX, fired heater, ExtensionPack, etc.) and therefore requires an active patron key. Used by [`AddExternalUnitOperation`](dwsim-automation-fluentapi-flowsheet.md) and every typed Plus `AddX` method to decide whether to call [`RequirePlus`](dwsim-automation-fluentapi-license.md).
