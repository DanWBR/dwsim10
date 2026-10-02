using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Wind Turbine unit operation. Call <see cref="Flowsheet.AddWindTurbine"/> to obtain one.</summary>
    public sealed class WindTurbineBuilder : UnitOpBuilder<WindTurbine, WindTurbineBuilder>
    {
        internal WindTurbineBuilder(Flowsheet f, WindTurbine o) : base(f, o) { }

        /// <summary>
        /// Sets the rotor disk area, in m2. The turbine reads the area from the rotor diameter when
        /// one is set, so this also sets the diameter that gives the area.
        /// </summary>
        public WindTurbineBuilder WithDiskAreaM2(double m2)
        {
            Object.DiskArea = m2;
            Object.RotorDiameter = System.Math.Sqrt(m2 * 4.0 / System.Math.PI);
            return this;
        }

        /// <summary>Sets the rotor diameter, in m, and the disk area that goes with it.</summary>
        public WindTurbineBuilder WithRotorDiameterM(double m)
        {
            Object.RotorDiameter = m;
            Object.DiskArea = System.Math.PI * m * m / 4.0;
            return this;
        }

        /// <summary>Sets <c>Efficiency Percent</c> and returns this builder for chaining.</summary>
        public WindTurbineBuilder WithEfficiencyPercent(double pct) { Object.Efficiency = pct; return this; }
        /// <summary>Sets <c>Turbine Count</c> and returns this builder for chaining.</summary>
        public WindTurbineBuilder WithTurbineCount(int n) { Object.NumberOfTurbines = n; return this; }

        /// <summary>
        /// Sets the air density, in kg/m3, the turbine uses in place of the one it calculates from the
        /// air temperature, pressure and humidity. Zero goes back to calculating it.
        /// </summary>
        public WindTurbineBuilder WithAirDensityKgPerM3(double rho) { Object.UserDefinedAirDensity = rho; return this; }

        /// <summary>
        /// Runs the turbine on its own weather instead of the flowsheet's: the wind speed, in m/s, the
        /// air temperature and pressure, and the relative humidity, in percent.
        /// </summary>
        public WindTurbineBuilder WithUserDefinedWeather(double windSpeedMPerS, Quantity airTemperature,
            Quantity airPressure, double relativeHumidityPercent)
        {
            Object.UseUserDefinedWeather = true;
            Object.UserDefinedWindSpeed = windSpeedMPerS;
            Object.UserDefinedAirTemperature = airTemperature.SI;
            Object.UserDefinedAirPressure = airPressure.SI;
            Object.UserDefinedRelativeHumidity = relativeHumidityPercent;
            return this;
        }

        /// <summary>Read-back of <c>Generated Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double GeneratedPowerKW => Object.GeneratedPower;
        /// <summary>Read-back of <c>Max Theoretical Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double MaxTheoreticalPowerKW => Object.MaximumTheoreticalPower;
        /// <summary>Read-back of the air density the last calculation used, in kg/m3 (populated after <c>Solve</c>).</summary>
        public double AirDensityKgPerM3 => Object.AirDensity;
    }
}
