# PropertyPackageConfigExtensions

`DWSIM.Automation.FluentAPI.PropertyPackageConfigExtensions`

Extension methods that wire the configurator into [`WithPropertyPackage`](dwsim-automation-fluentapi-flowsheet.md).

## Methods

### `ConfigurePropertyPackage(Flowsheet, Action{PropertyPackageBuilder})`

Configures the most-recently-added property package without changing it. Intended to follow a parameterless [`WithPropertyPackage`](dwsim-automation-fluentapi-flowsheet.md) call.

### `WithPropertyPackage(Flowsheet, string, Action{PropertyPackageBuilder})`

Adds a property package and configures it via a typed builder. Equivalent to calling [`WithPropertyPackage`](dwsim-automation-fluentapi-flowsheet.md) followed by mutating the most-recently-added property package; failing to find it is treated as a programmer error.
