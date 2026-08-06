using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Hydroelectric Turbine unit operation. Call <see cref="Flowsheet.AddHydroelectricTurbine"/> to obtain one.</summary>
    public sealed class HydroelectricTurbineBuilder : UnitOpBuilder<HydroelectricTurbine, HydroelectricTurbineBuilder>
    {
        internal HydroelectricTurbineBuilder(Flowsheet f, HydroelectricTurbine o) : base(f, o) { }

        /// <summary>Sets <c>Efficiency Percent</c> and returns this builder for chaining.</summary>
        public HydroelectricTurbineBuilder WithEfficiencyPercent(double pct) { Object.Efficiency = pct; return this; }
        /// <summary>Sets <c>Static Head M</c> and returns this builder for chaining.</summary>
        public HydroelectricTurbineBuilder WithStaticHeadM(double m) { Object.StaticHead = m; return this; }
        /// <summary>Sets <c>Velocity Head M</c> and returns this builder for chaining.</summary>
        public HydroelectricTurbineBuilder WithVelocityHeadM(double m) { Object.VelocityHead = m; return this; }
        /// <summary>Sets <c>Inlet Velocity MPer S</c> and returns this builder for chaining.</summary>
        public HydroelectricTurbineBuilder WithInletVelocityMPerS(double v) { Object.InletVelocity = v; return this; }
        /// <summary>Sets <c>Outlet Velocity MPer S</c> and returns this builder for chaining.</summary>
        public HydroelectricTurbineBuilder WithOutletVelocityMPerS(double v) { Object.OutletVelocity = v; return this; }

        /// <summary>Read-back of <c>Total Head M</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double TotalHeadM => Object.TotalHead;
        /// <summary>Read-back of <c>Generated Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double GeneratedPowerKW => Object.GeneratedPower;
    }
}
