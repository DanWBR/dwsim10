using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Water Electrolyzer unit operation. Call <see cref="Flowsheet.AddWaterElectrolyzer"/> to obtain one.</summary>
    public sealed class WaterElectrolyzerBuilder : UnitOpBuilder<WaterElectrolyzer, WaterElectrolyzerBuilder>
    {
        internal WaterElectrolyzerBuilder(Flowsheet f, WaterElectrolyzer o) : base(f, o) { }

        /// <summary>
        /// Sets the total stack voltage, in V. With <see cref="WithCellCount"/> above zero the
        /// electrolyzer runs from the voltage and the cell count, and an efficiency is not used.
        /// </summary>
        public WaterElectrolyzerBuilder WithVoltage(double v) { Object.Voltage = v; return this; }
        /// <summary>Sets <c>Cell Voltage</c> and returns this builder for chaining. The calculation overwrites it.</summary>
        public WaterElectrolyzerBuilder WithCellVoltage(double v) { Object.CellVoltage = v; return this; }
        /// <summary>Sets the number of cells in the stack, read together with <see cref="WithVoltage"/>.</summary>
        public WaterElectrolyzerBuilder WithCellCount(int n) { Object.NumberOfCells = n; return this; }
        /// <summary>Sets <c>Electron Transfer</c> and returns this builder for chaining. The calculation overwrites it.</summary>
        public WaterElectrolyzerBuilder WithElectronTransfer(double n) { Object.ElectronTransfer = n; return this; }

        /// <summary>
        /// Sets the efficiency the electrolyzer runs at, in percent (0 to 100, thermoneutral basis):
        /// that share of the power splits water and the rest leaves as waste heat. The electrolyzer
        /// only reads an efficiency when the stack voltage is zero, so this also clears the voltage.
        /// </summary>
        public WaterElectrolyzerBuilder WithEfficiencyPercent(double pct)
        {
            Object.InputEfficiency = pct / 100.0;
            Object.Voltage = 0.0;
            return this;
        }

        /// <summary>Read-back of the calculated efficiency, as a fraction of the power (populated after <c>Solve</c>).</summary>
        public double Efficiency => Object.Efficiency;
        /// <summary>Read-back of the waste heat, in kW (populated after <c>Solve</c>).</summary>
        public double WasteHeatKW => Object.WasteHeat;
        /// <summary>Read-back of the cell voltage, in V (populated after <c>Solve</c>).</summary>
        public double CellVoltage => Object.CellVoltage;
    }
}
