# Q

`DWSIM.Automation.FluentAPI.Q`

Extension methods producing [`Quantity`](dwsim-automation-fluentapi-quantity.md) values from numeric literals. Each method's name carries the source unit; the returned [`Quantity`](dwsim-automation-fluentapi-quantity.md) holds the value in SI.

## Remarks

pythonnet does not surface C# extension methods as instance methods, so from Python call them as static helpers: `Q.Kelvin(300.0)`, `Q.Bar(10.0)`.

## Methods

### `Atm(double)`

Pressure: standard atmosphere → Pa.

### `Atm(int)`

Pressure: atm → Pa (int overload).

### `Bar(double)`

Pressure: bar → Pa (1 bar = 100 000 Pa).

### `Bar(int)`

Pressure: bar → Pa (int overload).

### `Celsius(double)`

Temperature: degrees Celsius → K.

### `Celsius(int)`

Temperature: degrees Celsius → K (int overload).

### `Centimeters(double)`

Length: centimeter → m.

### `CubicMeters(double)`

Volume: cubic meter → m³.

### `CubicMetersPerHour(double)`

Volumetric flow: m³/h → m³/s.

### `CubicMetersPerSecond(double)`

Volumetric flow: m³/s → m³/s.

### `Days(double)`

Time: days → s.

### `Days(int)`

Time: days → s (int overload).

### `Fraction(double)`

Dimensionless fraction in [0, 1].

### `Hours(double)`

Time: hours → s.

### `Hours(int)`

Time: hours → s (int overload).

### `Inches(double)`

Length: inch → m.

### `Kelvin(double)`

Temperature: kelvin → K.

### `Kelvin(int)`

Temperature: kelvin → K (int overload).

### `KgPerHour(double)`

Mass flow: kg/h → kg/s.

### `KgPerHour(int)`

Mass flow: kg/h → kg/s (int overload).

### `KgPerSecond(double)`

Mass flow: kg/s → kg/s.

### `KgPerSecond(int)`

Mass flow: kg/s → kg/s (int overload).

### `KiloPascal(double)`

Pressure: kilopascal → Pa.

### `Kilowatts(double)`

Power: kilowatt → kW (DWSIM EnergyStream native unit).

### `Kilowatts(int)`

Power: kilowatt → kW (int overload).

### `KmolPerHour(double)`

Molar flow: kmol/h → mol/s.

### `KmolPerHour(int)`

Molar flow: kmol/h → mol/s (int overload).

### `KmolPerSecond(double)`

Molar flow: kmol/s → mol/s.

### `Liters(double)`

Volume: liter → m³.

### `Megawatts(double)`

Power: megawatt → kW.

### `Meters(double)`

Length: meter → m.

### `Millimeters(double)`

Length: millimeter → m.

### `Minutes(double)`

Time: minutes → s.

### `Minutes(int)`

Time: minutes → s (int overload).

### `MolPerSecond(double)`

Molar flow: mol/s → mol/s.

### `MolPerSecond(int)`

Molar flow: mol/s → mol/s (int overload).

### `Pascal(double)`

Pressure: pascal → Pa.

### `Pascal(int)`

Pressure: pascal → Pa (int overload).

### `Percent(double)`

Percent (0–100) → fraction (0–1).

### `Seconds(double)`

Time: seconds → s.

### `Seconds(int)`

Time: seconds → s (int overload).

### `Watts(double)`

Power: watt → kW.
