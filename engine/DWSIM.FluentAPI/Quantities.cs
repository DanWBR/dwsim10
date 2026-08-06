using System;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Lightweight unit-aware scalar. The numeric value is stored in SI units; conversion
    /// happens at construction via the extension methods on <see cref="Q"/>. A
    /// <see cref="Quantity"/> is consumed by builder <c>WithX</c> setters which call
    /// <see cref="SI"/> directly, so DWSIM always sees SI internally.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddMaterialStream("feed").At(300.Kelvin(), 10.Bar()).WithMassFlow(100.KgPerSecond());
    /// </code>
    /// </example>
    public readonly struct Quantity
    {
        /// <summary>Numeric value in the canonical SI unit for this dimension (K, Pa, kg/s, mol/s, m, m³, m³/s, kW).</summary>
        public double SI { get; }

        /// <summary>Short tag identifying the physical dimension (e.g. <c>"T"</c>, <c>"P"</c>, <c>"Mflow"</c>). Informational.</summary>
        public string Dimension { get; }

        /// <summary>Constructs a <see cref="Quantity"/> from an SI value and a dimension tag.</summary>
        public Quantity(double si, string dimension)
        {
            SI = si;
            Dimension = dimension;
        }

        /// <summary>Returns <c>"&lt;value&gt; (&lt;dimension&gt;, SI)"</c>.</summary>
        public override string ToString() => $"{SI} ({Dimension}, SI)";
    }

    /// <summary>
    /// Extension methods producing <see cref="Quantity"/> values from numeric literals.
    /// Each method's name carries the source unit; the returned <see cref="Quantity"/>
    /// holds the value in SI.
    /// </summary>
    /// <remarks>
    /// pythonnet does not surface C# extension methods as instance methods, so from Python
    /// call them as static helpers: <c>Q.Kelvin(300.0)</c>, <c>Q.Bar(10.0)</c>.
    /// </remarks>
    public static class Q
    {
        /// <summary>Temperature: kelvin → K.</summary>
        public static Quantity Kelvin(this double v) => new Quantity(v, "T");
        /// <summary>Temperature: degrees Celsius → K.</summary>
        public static Quantity Celsius(this double v) => new Quantity(v + 273.15, "T");
        /// <summary>Temperature: kelvin → K (int overload).</summary>
        public static Quantity Kelvin(this int v) => ((double)v).Kelvin();
        /// <summary>Temperature: degrees Celsius → K (int overload).</summary>
        public static Quantity Celsius(this int v) => ((double)v).Celsius();

        /// <summary>Pressure: pascal → Pa.</summary>
        public static Quantity Pascal(this double v) => new Quantity(v, "P");
        /// <summary>Pressure: kilopascal → Pa.</summary>
        public static Quantity KiloPascal(this double v) => new Quantity(v * 1e3, "P");
        /// <summary>Pressure: bar → Pa (1 bar = 100 000 Pa).</summary>
        public static Quantity Bar(this double v) => new Quantity(v * 1e5, "P");
        /// <summary>Pressure: standard atmosphere → Pa.</summary>
        public static Quantity Atm(this double v) => new Quantity(v * 101325.0, "P");
        /// <summary>Pressure: pascal → Pa (int overload).</summary>
        public static Quantity Pascal(this int v) => ((double)v).Pascal();
        /// <summary>Pressure: bar → Pa (int overload).</summary>
        public static Quantity Bar(this int v) => ((double)v).Bar();
        /// <summary>Pressure: atm → Pa (int overload).</summary>
        public static Quantity Atm(this int v) => ((double)v).Atm();

        /// <summary>Mass flow: kg/s → kg/s.</summary>
        public static Quantity KgPerSecond(this double v) => new Quantity(v, "Mflow");
        /// <summary>Mass flow: kg/h → kg/s.</summary>
        public static Quantity KgPerHour(this double v) => new Quantity(v / 3600.0, "Mflow");
        /// <summary>Mass flow: kg/s → kg/s (int overload).</summary>
        public static Quantity KgPerSecond(this int v) => ((double)v).KgPerSecond();
        /// <summary>Mass flow: kg/h → kg/s (int overload).</summary>
        public static Quantity KgPerHour(this int v) => ((double)v).KgPerHour();

        /// <summary>Molar flow: mol/s → mol/s.</summary>
        public static Quantity MolPerSecond(this double v) => new Quantity(v, "Nflow");
        /// <summary>Molar flow: kmol/s → mol/s.</summary>
        public static Quantity KmolPerSecond(this double v) => new Quantity(v * 1000.0, "Nflow");
        /// <summary>Molar flow: kmol/h → mol/s.</summary>
        public static Quantity KmolPerHour(this double v) => new Quantity(v * 1000.0 / 3600.0, "Nflow");
        /// <summary>Molar flow: mol/s → mol/s (int overload).</summary>
        public static Quantity MolPerSecond(this int v) => ((double)v).MolPerSecond();
        /// <summary>Molar flow: kmol/h → mol/s (int overload).</summary>
        public static Quantity KmolPerHour(this int v) => ((double)v).KmolPerHour();

        /// <summary>Volumetric flow: m³/s → m³/s.</summary>
        public static Quantity CubicMetersPerSecond(this double v) => new Quantity(v, "Qflow");
        /// <summary>Volumetric flow: m³/h → m³/s.</summary>
        public static Quantity CubicMetersPerHour(this double v) => new Quantity(v / 3600.0, "Qflow");

        /// <summary>Power: kilowatt → kW (DWSIM EnergyStream native unit).</summary>
        public static Quantity Kilowatts(this double v) => new Quantity(v, "Power");
        /// <summary>Power: watt → kW.</summary>
        public static Quantity Watts(this double v) => new Quantity(v / 1000.0, "Power");
        /// <summary>Power: megawatt → kW.</summary>
        public static Quantity Megawatts(this double v) => new Quantity(v * 1000.0, "Power");
        /// <summary>Power: kilowatt → kW (int overload).</summary>
        public static Quantity Kilowatts(this int v) => ((double)v).Kilowatts();

        /// <summary>Length: meter → m.</summary>
        public static Quantity Meters(this double v) => new Quantity(v, "L");
        /// <summary>Length: centimeter → m.</summary>
        public static Quantity Centimeters(this double v) => new Quantity(v / 100.0, "L");
        /// <summary>Length: millimeter → m.</summary>
        public static Quantity Millimeters(this double v) => new Quantity(v / 1000.0, "L");
        /// <summary>Length: inch → m.</summary>
        public static Quantity Inches(this double v) => new Quantity(v * 0.0254, "L");

        /// <summary>Volume: cubic meter → m³.</summary>
        public static Quantity CubicMeters(this double v) => new Quantity(v, "V");
        /// <summary>Volume: liter → m³.</summary>
        public static Quantity Liters(this double v) => new Quantity(v / 1000.0, "V");

        /// <summary>Time: seconds → s.</summary>
        public static Quantity Seconds(this double v) => new Quantity(v, "t");
        /// <summary>Time: minutes → s.</summary>
        public static Quantity Minutes(this double v) => new Quantity(v * 60.0, "t");
        /// <summary>Time: hours → s.</summary>
        public static Quantity Hours(this double v) => new Quantity(v * 3600.0, "t");
        /// <summary>Time: days → s.</summary>
        public static Quantity Days(this double v) => new Quantity(v * 86400.0, "t");
        /// <summary>Time: seconds → s (int overload).</summary>
        public static Quantity Seconds(this int v) => ((double)v).Seconds();
        /// <summary>Time: minutes → s (int overload).</summary>
        public static Quantity Minutes(this int v) => ((double)v).Minutes();
        /// <summary>Time: hours → s (int overload).</summary>
        public static Quantity Hours(this int v) => ((double)v).Hours();
        /// <summary>Time: days → s (int overload).</summary>
        public static Quantity Days(this int v) => ((double)v).Days();

        /// <summary>Dimensionless fraction in [0, 1].</summary>
        public static Quantity Fraction(this double v) => new Quantity(v, "frac");
        /// <summary>Percent (0–100) → fraction (0–1).</summary>
        public static Quantity Percent(this double v) => new Quantity(v / 100.0, "frac");
    }
}
