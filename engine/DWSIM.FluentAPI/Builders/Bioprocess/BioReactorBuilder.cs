using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Bio Reactor unit operation. Call <see cref="Flowsheet.AddBioReactor"/> to obtain one.</summary>
    public sealed class BioReactorBuilder : UnitOpBuilder<Reactor_BioReactor, BioReactorBuilder>
    {
        internal BioReactorBuilder(Flowsheet f, Reactor_BioReactor o) : base(f, o) { }

        /// <summary>Sets <c>Volume</c> (SI) and returns this builder for chaining.</summary>
        public BioReactorBuilder WithVolume(Quantity v) { Object.Volume = v.SI; return this; }
        /// <summary>Sets <c>Batch Duration</c> (SI) and returns this builder for chaining.</summary>
        public BioReactorBuilder WithBatchDuration(Quantity t) { Object.BatchDuration = t.SI; return this; }
        /// <summary>Sets <c>Kinetic Model</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithKineticModel(BioKineticModel m) { Object.KineticModel = m; return this; }
        /// <summary>Sets <c>Operating Mode</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithOperatingMode(BioReactorMode m) { Object.OperatingMode = m; return this; }
        /// <summary>Sets <c>Thermal Mode</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithThermalMode(BioReactorThermalMode m) { Object.ThermalMode = m; return this; }
        /// <summary>Sets <c>Aerobic</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithAerobic(bool aerobic) { Object.IsAerobic = aerobic; return this; }
        /// <summary>Sets <c>Max Specific Growth Per Hour</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithMaxSpecificGrowthPerHour(double muMax) { Object.MuMax_h = muMax; return this; }
        /// <summary>Sets <c>Monod Ks GPer L</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithMonodKsGPerL(double ks) { Object.Ks_gL = ks; return this; }
        /// <summary>Sets <c>Biomass Yield</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithBiomassYield(double yxs) { Object.YieldXS = yxs; return this; }
        /// <summary>Sets <c>KLa Per Hour</c> and returns this builder for chaining.</summary>
        public BioReactorBuilder WithKLaPerHour(double kla) { Object.KLa_h = kla; return this; }

        // ---- Property profile access (populated after Calculate) ----

        /// <summary>Dynamic trajectory from the last Calculate call (biomass, substrate, product vs time). Null if not yet calculated.</summary>
        public BioReactorTrajectoryResult Trajectory => Object.LastTrajectory;

        /// <summary>Names of all available profile series (e.g. "X", "S", "P", "Mu").</summary>
        public string[] ProfileSeriesNames => Object.LastTrajectory?.AvailableSeries();

        /// <summary>Returns a named profile series as an array of doubles.</summary>
        public double[] GetProfileSeries(string name) => Object.LastTrajectory?.GetSeries(name);

        /// <summary>Exports the full trajectory to CSV text.</summary>
        public string ProfileToCSV() => Object.LastTrajectory?.ToCSV();

        /// <summary>Exports the full trajectory to a DataTable for charting or tabular display.</summary>
        public System.Data.DataTable ProfileToDataTable() => Object.LastTrajectory?.ToDataTable();
    }
}
