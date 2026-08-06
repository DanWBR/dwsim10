# First Flowsheet — C#

We'll mix two water streams of different temperatures using IAPWS-IF97 steam
tables and read back the outlet temperature. The complete file:

```csharp
using System;
using DWSIM.Automation.FluentAPI;

class Program
{
    static void Main()
    {
        var fs = Flowsheet.Create("CSharpMixer")
            .WithCompound("Water")
            .WithPropertyPackage(PropertyPackages.SteamTables);

        var inlet1 = fs.AddMaterialStream("inlet1")
            .At(300.Kelvin(), 1.Atm())
            .WithMassFlow(100.KgPerSecond());

        var inlet2 = fs.AddMaterialStream("inlet2")
            .At(348.Kelvin(), 1.Atm())
            .WithMassFlow(50.KgPerSecond());

        var outlet = fs.AddMaterialStream("outlet");

        fs.AddMixer("MIX-1")
          .ConnectFeed(inlet1, 0)
          .ConnectFeed(inlet2, 1)
          .ConnectProduct(outlet, 0);

        fs.AutoLayout();
        fs.Solve();

        Console.WriteLine($"Outlet T  = {outlet.TemperatureK:F4} K");
        Console.WriteLine($"Mass flow = {outlet.MassFlowKgPerSecond:F4} kg/s");
    }
}
```

## Walk-through

### 1. Create the flowsheet

```csharp
var fs = Flowsheet.Create("CSharpMixer")
    .WithCompound("Water")
    .WithPropertyPackage(PropertyPackages.SteamTables);
```

`Flowsheet.Create` returns a fluent wrapper around a fresh in-memory
`IFlowsheet`. The first call also installs the assembly resolver so Plus
DLLs in `extenders/`, `unitops/` and `ppacks/` can be JITted on demand.

`WithCompound` adds a compound by its DWSIM database name. Multiple
compounds in a single call use `WithCompounds(params string[])`.

`WithPropertyPackage` accepts any constant from
[`PropertyPackages`](../api/property-packages.md). Plus packages
(`PropertyPackages.Plus.*`) require an active patron key first.

### 2. Build the streams

```csharp
var inlet1 = fs.AddMaterialStream("inlet1")
    .At(300.Kelvin(), 1.Atm())
    .WithMassFlow(100.KgPerSecond());
```

`At(T, P)` is a shorthand for `WithTemperature(T).WithPressure(P)`.
`300.Kelvin()`, `1.Atm()` and `100.KgPerSecond()` are
[`Quantity`](../api/quantities.md) values; the conversion to SI
(K, Pa, kg/s) happens at the call site.

### 3. Connect the unit operation

```csharp
fs.AddMixer("MIX-1")
  .ConnectFeed(inlet1, 0)
  .ConnectFeed(inlet2, 1)
  .ConnectProduct(outlet, 0);
```

`ConnectFeed` and `ConnectProduct` are inherited from `UnitOpBuilder<,>` and
available on every typed UO builder. The integer is the port index.

### 4. Solve and read results

```csharp
fs.Solve();
Console.WriteLine($"Outlet T  = {outlet.TemperatureK:F4} K");
```

`Solve` throws `FlowsheetSolveException` (an `AggregateException`) if any
unit fails. Use `TrySolve` to receive the list of solver errors instead of
throwing.

`MaterialStreamBuilder` exposes read-back properties (`TemperatureK`,
`PressurePa`, `MassFlowKgPerSecond`, `MolarFlowMolPerSecond`,
`VolumetricFlowM3PerSecond`, plus per-compound overall fractions) populated
after `Solve`.

### Next steps

- The same flowsheet, port-for-port, in [Python](first-flowsheet-python.md).
- More elaborate cases under [Examples](../examples/index.md).
- Full [`Flowsheet`](../api/flowsheet.md) reference.
