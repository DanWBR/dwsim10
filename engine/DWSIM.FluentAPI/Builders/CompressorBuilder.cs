using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Compressor unit operation. Call <see cref="Flowsheet.AddCompressor"/> to obtain one.</summary>
    public sealed class CompressorBuilder : UnitOpBuilder<Compressor, CompressorBuilder>
    {
        internal CompressorBuilder(Flowsheet f, Compressor o) : base(f, o) { }

        /// <summary>Sets <c>Calc Mode</c> and returns this builder for chaining.</summary>
        public CompressorBuilder WithCalcMode(Compressor.CalculationMode mode) { Object.CalcMode = mode; return this; }
        /// <summary>Sets <c>Process Path</c> and returns this builder for chaining.</summary>
        public CompressorBuilder WithProcessPath(Compressor.ProcessPathType path) { Object.ProcessPath = path; return this; }
        /// <summary>Sets <c>Outlet Pressure</c> (SI) and returns this builder for chaining.</summary>
        public CompressorBuilder WithOutletPressure(Quantity p) { Object.POut = p.SI; Object.CalcMode = Compressor.CalculationMode.OutletPressure; return this; }
        /// <summary>Sets <c>Pressure Increase</c> (SI) and returns this builder for chaining.</summary>
        public CompressorBuilder WithPressureIncrease(Quantity dp) { Object.DeltaP = dp.SI; Object.CalcMode = Compressor.CalculationMode.Delta_P; return this; }
        /// <summary>Sets <c>Power</c> (SI) and returns this builder for chaining.</summary>
        public CompressorBuilder WithPower(Quantity power) { Object.DeltaQ = power.SI; Object.CalcMode = Compressor.CalculationMode.PowerRequired; return this; }
        /// <summary>Sets <c>Adiabatic Efficiency Percent</c> and returns this builder for chaining.</summary>
        public CompressorBuilder WithAdiabaticEfficiencyPercent(double pct) { Object.AdiabaticEfficiency = pct; return this; }
        /// <summary>Sets <c>Polytropic Efficiency Percent</c> and returns this builder for chaining.</summary>
        public CompressorBuilder WithPolytropicEfficiencyPercent(double pct) { Object.PolytropicEfficiency = pct; return this; }
    }
}
