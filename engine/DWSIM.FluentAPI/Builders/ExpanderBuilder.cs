using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Expander unit operation. Call <see cref="Flowsheet.AddExpander"/> to obtain one.</summary>
    public sealed class ExpanderBuilder : UnitOpBuilder<Expander, ExpanderBuilder>
    {
        internal ExpanderBuilder(Flowsheet f, Expander o) : base(f, o) { }

        /// <summary>Sets <c>Calc Mode</c> and returns this builder for chaining.</summary>
        public ExpanderBuilder WithCalcMode(Expander.CalculationMode mode) { Object.CalcMode = mode; return this; }
        /// <summary>Sets <c>Process Path</c> and returns this builder for chaining.</summary>
        public ExpanderBuilder WithProcessPath(Expander.ProcessPathType path) { Object.ProcessPath = path; return this; }
        /// <summary>Sets <c>Outlet Pressure</c> (SI) and returns this builder for chaining.</summary>
        public ExpanderBuilder WithOutletPressure(Quantity p) { Object.POut = p.SI; Object.CalcMode = Expander.CalculationMode.OutletPressure; return this; }
        /// <summary>Sets <c>Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public ExpanderBuilder WithPressureDrop(Quantity dp) { Object.DeltaP = dp.SI; Object.CalcMode = Expander.CalculationMode.Delta_P; return this; }
        /// <summary>Sets <c>Power Generated</c> (SI) and returns this builder for chaining.</summary>
        public ExpanderBuilder WithPowerGenerated(Quantity power) { Object.DeltaQ = power.SI; Object.CalcMode = Expander.CalculationMode.PowerGenerated; return this; }
        /// <summary>Sets <c>Adiabatic Efficiency Percent</c> and returns this builder for chaining.</summary>
        public ExpanderBuilder WithAdiabaticEfficiencyPercent(double pct) { Object.AdiabaticEfficiency = pct; return this; }
        /// <summary>Sets <c>Polytropic Efficiency Percent</c> and returns this builder for chaining.</summary>
        public ExpanderBuilder WithPolytropicEfficiencyPercent(double pct) { Object.PolytropicEfficiency = pct; return this; }
    }
}
