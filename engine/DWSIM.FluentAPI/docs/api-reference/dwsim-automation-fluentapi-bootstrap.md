# Bootstrap

`DWSIM.Automation.FluentAPI.Bootstrap`

Process-wide singleton that hosts a single `DWSIM.Automation.Automation3` instance, used to bootstrap headless flowsheets with all property packages and compounds pre-loaded. Reused across all [`Flowsheet`](dwsim-automation-fluentapi-flowsheet.md) instances created via the Fluent API.

## Methods

### `RegisterAssemblyResolver`

Installs an `AssemblyResolve` handler that probes the `extenders`, `unitops` and `ppacks` sub-folders next to the running assembly. Required for the JIT to find Plus / DWSIMPlus assemblies (LCA, TEA, electrolyte / ThermoPack PPs, refining UOs) before any [`Flowsheet`](dwsim-automation-fluentapi-flowsheet.md) method that statically references them is called. Idempotent; safe to call multiple times.
