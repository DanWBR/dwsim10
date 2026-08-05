using System;
using System.Globalization;

namespace DWSIM.PhaseEquilibriumData.Sources.ThermoML
{
    public static class UnitConversions
    {
        public static double TemperatureToK(double value, string unit)
        {
            switch (NormalizeUnit(unit))
            {
                case "k": return value;
                case "c":
                case "°c":
                case "degc": return value + 273.15;
                case "f":
                case "°f":
                case "degf": return (value - 32.0) * 5.0 / 9.0 + 273.15;
                default: return value;
            }
        }

        public static double PressureToKPa(double value, string unit)
        {
            switch (NormalizeUnit(unit))
            {
                case "kpa": return value;
                case "pa": return value / 1000.0;
                case "mpa": return value * 1000.0;
                case "bar": return value * 100.0;
                case "mbar": return value * 0.1;
                case "atm": return value * 101.325;
                case "torr":
                case "mmhg": return value * 0.133322387415;
                case "psi": return value * 6.894757293168361;
                default: return value;
            }
        }

        public static bool TryParseInvariant(string? s, out double result)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        private static string NormalizeUnit(string unit)
            => (unit ?? string.Empty).Trim().ToLowerInvariant();
    }
}
