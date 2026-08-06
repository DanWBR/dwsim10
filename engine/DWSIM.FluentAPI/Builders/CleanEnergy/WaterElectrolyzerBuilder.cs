using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Water Electrolyzer unit operation. Call <see cref="Flowsheet.AddWaterElectrolyzer"/> to obtain one.</summary>
    public sealed class WaterElectrolyzerBuilder : UnitOpBuilder<WaterElectrolyzer, WaterElectrolyzerBuilder>
    {
        internal WaterElectrolyzerBuilder(Flowsheet f, WaterElectrolyzer o) : base(f, o) { }

        /// <summary>Sets <c>Voltage</c> and returns this builder for chaining.</summary>
        public WaterElectrolyzerBuilder WithVoltage(double v) { Object.Voltage = v; return this; }
        /// <summary>Sets <c>Cell Voltage</c> and returns this builder for chaining.</summary>
        public WaterElectrolyzerBuilder WithCellVoltage(double v) { Object.CellVoltage = v; return this; }
        /// <summary>Sets <c>Cell Count</c> and returns this builder for chaining.</summary>
        public WaterElectrolyzerBuilder WithCellCount(int n) { Object.NumberOfCells = n; return this; }
        /// <summary>Sets <c>Electron Transfer</c> and returns this builder for chaining.</summary>
        public WaterElectrolyzerBuilder WithElectronTransfer(double n) { Object.ElectronTransfer = n; return this; }
        /// <summary>Sets <c>Efficiency Percent</c> and returns this builder for chaining.</summary>
        public WaterElectrolyzerBuilder WithEfficiencyPercent(double pct) { Object.Efficiency = pct; return this; }
    }
}
