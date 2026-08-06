using DWSIM.Interfaces;
using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the rigorous distillation column.</summary>
    public sealed class DistillationColumnBuilder : UnitOpBuilder<DistillationColumn, DistillationColumnBuilder>
    {
        internal DistillationColumnBuilder(Flowsheet f, DistillationColumn o) : base(f, o) { }

        /// <summary>Sets <c>Number Of Stages</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithNumberOfStages(int n) { Object.SetNumberOfStages(n); return this; }
        /// <summary>Sets <c>Top Pressure</c> (SI) and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithTopPressure(Quantity p) { Object.SetTopPressure(p.SI); return this; }
        /// <summary>Sets <c>Column Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithColumnPressureDrop(Quantity dp) { Object.ColumnPressureDrop = dp.SI; return this; }

        /// <summary>Sets <c>Feed</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithFeed(MaterialStreamBuilder feed, int stageNumber)
        { Object.ConnectFeed(feed.Object, stageNumber); return this; }

        /// <summary>Sets <c>Distillate</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithDistillate(MaterialStreamBuilder distillate)
        { Object.ConnectDistillate(distillate.Object); return this; }

        /// <summary>Sets <c>Bottoms</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithBottoms(MaterialStreamBuilder bottoms)
        { Object.ConnectBottoms(bottoms.Object); return this; }

        /// <summary>Sets <c>Vapor Product</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithVaporProduct(MaterialStreamBuilder vapor)
        { Object.ConnectVaporProduct(vapor.Object); return this; }

        /// <summary>Sets <c>Condenser Duty</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithCondenserDuty(EnergyStreamBuilder duty)
        { Object.ConnectCondenserDuty(duty.Object); return this; }

        /// <summary>Sets <c>Reboiler Duty</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithReboilerDuty(EnergyStreamBuilder duty)
        { Object.ConnectReboilerDuty(duty.Object); return this; }

        /// <summary>Sets the condenser specification (e.g. "Reflux Ratio", value, "" for unitless).</summary>
        public DistillationColumnBuilder WithCondenserSpec(string specType, double value, string units = "", string compound = "")
        { Object.SetCondenserSpec(specType, value, units, compound); return this; }

        /// <summary>Sets the reboiler specification (e.g. "Product Molar Flow Rate", 75, "mol/s").</summary>
        public DistillationColumnBuilder WithReboilerSpec(string specType, double value, string units = "", string compound = "")
        { Object.SetReboilerSpec(specType, value, units, compound); return this; }
    }
}
