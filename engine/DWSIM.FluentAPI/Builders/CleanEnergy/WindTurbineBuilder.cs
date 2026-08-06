using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Wind Turbine unit operation. Call <see cref="Flowsheet.AddWindTurbine"/> to obtain one.</summary>
    public sealed class WindTurbineBuilder : UnitOpBuilder<WindTurbine, WindTurbineBuilder>
    {
        internal WindTurbineBuilder(Flowsheet f, WindTurbine o) : base(f, o) { }

        /// <summary>Sets <c>Disk Area M2</c> and returns this builder for chaining.</summary>
        public WindTurbineBuilder WithDiskAreaM2(double m2) { Object.DiskArea = m2; return this; }
        /// <summary>Sets <c>Rotor Diameter M</c> and returns this builder for chaining.</summary>
        public WindTurbineBuilder WithRotorDiameterM(double m) { Object.RotorDiameter = m; return this; }
        /// <summary>Sets <c>Efficiency Percent</c> and returns this builder for chaining.</summary>
        public WindTurbineBuilder WithEfficiencyPercent(double pct) { Object.Efficiency = pct; return this; }
        /// <summary>Sets <c>Turbine Count</c> and returns this builder for chaining.</summary>
        public WindTurbineBuilder WithTurbineCount(int n) { Object.NumberOfTurbines = n; return this; }
        /// <summary>Sets <c>Air Density Kg Per M3</c> and returns this builder for chaining.</summary>
        public WindTurbineBuilder WithAirDensityKgPerM3(double rho) { Object.AirDensity = rho; return this; }

        /// <summary>Read-back of <c>Generated Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double GeneratedPowerKW => Object.GeneratedPower;
        /// <summary>Read-back of <c>Max Theoretical Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double MaxTheoreticalPowerKW => Object.MaximumTheoreticalPower;
    }
}
