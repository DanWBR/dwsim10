using System;
using DWSIM.Automation.FluentAPI;
using Converter = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// What a dynamic event, a cause-and-effect effect or a ramp writes into a property.
    /// </summary>
    /// <remarks>
    /// A property write with no unit system falls back on the default SI unit set, and that set is not
    /// in SI units for every dimension: a diameter and a thickness are in millimetres there. The event
    /// runners used to convert the event's value to SI and write it straight, so the object converted it
    /// a second time and an event asking for a 1.25 m diameter applied 1.25 mm. Everything whose set
    /// unit is already the SI one, which is most of the catalogue, went through both conversions
    /// unchanged, which is why this went unnoticed.
    /// </remarks>
    internal static class EventUnitConversionTest
    {
        private const string CstrDiameter = "PROP_CS_21";
        private const string StreamPressure = "PROP_MS_1";

        public static void Run()
        {
            var fs = Flowsheet.Create("EventUnitConversion")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var reactor = fs.AddCSTR("R-01").Object;
            var stream = fs.AddMaterialStream("feed")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .Object;

            // an event carrying metres: the reactor stores metres, its property surface speaks
            // millimetres, and the value has to survive the trip
            Write(reactor, CstrDiameter, "m", 1.25);
            var diameterM = reactor.Diameter;
            var diameterProperty = Convert.ToDouble(reactor.GetPropertyValue(CstrDiameter));

            // the same event carrying millimetres
            Write(reactor, CstrDiameter, "mm", 900.0);
            var fromMillimetres = reactor.Diameter;

            // a dimension whose set unit is the SI one has to come through untouched, which is what
            // says the fix did not move everything else
            Write(stream, StreamPressure, "bar", 5.0);
            var pressurePa = Convert.ToDouble(stream.GetPropertyValue(StreamPressure));

            // no unit at all means the caller did not say, and the value is passed through as before
            var passedThrough = Converter.ConvertForPropertyWrite(reactor, CstrDiameter, "", 42.0);

            new ResultTable("What an event writes into a property")
                .Row("1.25 m of diameter is stored as 1.25 m", 1.25, diameterM, 1e-9, "m")
                .Row("and reads back as 1250 mm", 1250.0, diameterProperty, 1e-9, "mm")
                .Row("900 mm of diameter is stored as 0.9 m", 0.9, fromMillimetres, 1e-9, "m")
                .Row("5 bar of pressure is stored as 5 bar", 500000.0, pressurePa, 1e-9, "Pa")
                .Row("a value with no unit is passed through", 42.0, passedThrough, 1e-9)
                .PrintAndThrowIfFailed();
        }

        /// <summary>The two lines every event runner now runs.</summary>
        private static void Write(DWSIM.Interfaces.ISimulationObject obj, string property, string units, double value)
        {
            var converted = Converter.ConvertForPropertyWrite(obj, property, units, value);
            obj.SetPropertyValue(property, converted);
        }
    }
}
