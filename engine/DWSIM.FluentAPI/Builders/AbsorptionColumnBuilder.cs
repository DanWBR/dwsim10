using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Absorption Column unit operation. Call <see cref="Flowsheet.AddAbsorptionColumn"/> to obtain one.</summary>
    public sealed class AbsorptionColumnBuilder : UnitOpBuilder<AbsorptionColumn, AbsorptionColumnBuilder>
    {
        internal AbsorptionColumnBuilder(Flowsheet f, AbsorptionColumn o) : base(f, o) { }

        /// <summary>Sets <c>Number Of Stages</c> and returns this builder for chaining.</summary>
        public AbsorptionColumnBuilder WithNumberOfStages(int n) { Object.SetNumberOfStages(n); return this; }
        /// <summary>Sets <c>Top Pressure</c> (SI) and returns this builder for chaining.</summary>
        public AbsorptionColumnBuilder WithTopPressure(Quantity p) { Object.SetTopPressure(p.SI); return this; }
        /// <summary>Sets <c>Column Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public AbsorptionColumnBuilder WithColumnPressureDrop(Quantity dp) { Object.ColumnPressureDrop = dp.SI; return this; }

        /// <summary>Sets <c>Feed</c> and returns this builder for chaining.</summary>
        public AbsorptionColumnBuilder WithFeed(MaterialStreamBuilder feed, int stageNumber)
        { Object.ConnectFeed(feed.Object, stageNumber); return this; }

        /// <summary>Sets <c>Top Product</c> and returns this builder for chaining.</summary>
        public AbsorptionColumnBuilder WithTopProduct(MaterialStreamBuilder top)
        { Object.ConnectTopProduct(top.Object); return this; }

        /// <summary>Sets <c>Bottoms</c> and returns this builder for chaining.</summary>
        public AbsorptionColumnBuilder WithBottoms(MaterialStreamBuilder bottoms)
        { Object.ConnectBottoms(bottoms.Object); return this; }
    }
}
