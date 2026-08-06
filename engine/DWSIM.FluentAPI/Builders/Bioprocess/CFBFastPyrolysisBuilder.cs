using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the CFBFast Pyrolysis unit operation. Call <see cref="Flowsheet.AddCFBFastPyrolysis"/> to obtain one.</summary>
    public sealed class CFBFastPyrolysisBuilder : UnitOpBuilder<Reactor_CFBFastPyrolysis, CFBFastPyrolysisBuilder>
    {
        internal CFBFastPyrolysisBuilder(Flowsheet f, Reactor_CFBFastPyrolysis o) : base(f, o) { }

        /// <summary>Sets <c>Riser Height</c> (SI) and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithRiserHeight(Quantity h) { Object.RiserHeight_m = h.SI; return this; }
        /// <summary>Sets <c>Riser Diameter</c> (SI) and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithRiserDiameter(Quantity d) { Object.RiserDiameter_m = d.SI; return this; }
        /// <summary>Sets <c>Axial Cells</c> and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithAxialCells(int n) { Object.NumAxialCells = n; return this; }
        /// <summary>Sets <c>Carrier Gas Velocity MPer S</c> and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithCarrierGasVelocityMPerS(double v) { Object.CarrierGasVelocity_ms = v; return this; }
        /// <summary>Sets <c>Solids Holdup</c> and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithSolidsHoldup(double frac) { Object.SolidsHoldup = frac; return this; }
        /// <summary>Sets <c>Sand Mode</c> and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithSandMode(CFBSandMode mode) { Object.SandMode = mode; return this; }
        /// <summary>Sets <c>Sand Inlet Temperature</c> (SI) and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithSandInletTemperature(Quantity t) { Object.SandInletTemperature_K = t.SI; return this; }
        /// <summary>Sets <c>Sand To Biomass Ratio</c> and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithSandToBiomassRatio(double r) { Object.SandToBiomassRatio = r; return this; }
        /// <summary>Sets <c>Heat Loss Fraction</c> and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithHeatLossFraction(double frac) { Object.HeatLossFraction = frac; return this; }

        /// <summary>Sets <c>Biomass Composition</c> and returns this builder for chaining.</summary>
        public CFBFastPyrolysisBuilder WithBiomassComposition(double celluloseFrac, double hemicelluloseFrac, double ligninFrac)
        {
            Object.CelluloseMassFrac = celluloseFrac;
            Object.HemicelluloseMassFrac = hemicelluloseFrac;
            Object.LigninMassFrac = ligninFrac;
            return this;
        }

        // ---- Property profile access (populated after Calculate) ----

        /// <summary>Axial trajectory from the last Calculate call (temperature, yields, species vs riser height). Null if not yet calculated.</summary>
        public CFBPyrolysisTrajectoryResult Trajectory => Object.LastTrajectory;

        /// <summary>Names of all available axial profile series (e.g. "T_K", "SolidVelocity_ms").</summary>
        public string[] ProfileSeriesNames => Object.LastTrajectory?.AvailableSeries();

        /// <summary>Returns a named axial profile series as an array of doubles.</summary>
        public double[] GetProfileSeries(string name) => Object.LastTrajectory?.GetSeries(name);

        /// <summary>Exports the full axial trajectory to CSV text.</summary>
        public string ProfileToCSV() => Object.LastTrajectory?.ToCSV();

        /// <summary>Exports the full axial trajectory to a DataTable for charting or tabular display.</summary>
        public System.Data.DataTable ProfileToDataTable() => Object.LastTrajectory?.ToDataTable();
    }
}
