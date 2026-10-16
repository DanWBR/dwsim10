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
        /// <summary>
        /// Chooses the column solver by name: <c>"Wang-Henke (Bubble Point)"</c> (the default),
        /// <c>"Naphtali-Sandholm"</c> (simultaneous correction, the robust choice for sharp
        /// separations), <c>"Modified Wang-Henke Solver"</c> or an external solver's name.
        /// </summary>
        public DistillationColumnBuilder WithSolvingMethod(string name) { Object.SolvingMethodName = name; return this; }
        /// <summary>Sets the iteration cap of the column solver (default 100) and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithMaxIterations(int n) { Object.MaxIterations = n; return this; }
        /// <summary>Sets <c>Top Pressure</c> (SI) and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithTopPressure(Quantity p) { Object.SetTopPressure(p.SI); return this; }
        /// <summary>Sets <c>Column Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithColumnPressureDrop(Quantity dp) { Object.ColumnPressureDrop = dp.SI; return this; }
        /// <summary>Sets <c>Temperature Step Fraction</c> of the bubble-point solvers (0 to 1, default 0.5) and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithTemperatureStepFraction(double fraction) { Object.TemperatureStepFraction = fraction; return this; }

        /// <summary>Connects a feed to a stage by its zero-based index and returns this builder for chaining.
        /// Index 0 is the top stage, which the editor calls Stage1, so the editor's StageN is index N - 1:
        /// <c>WithFeed(feed, 10)</c> feeds Stage11. <see cref="WithFeed(MaterialStreamBuilder, string)"/> takes the name.</summary>
        public DistillationColumnBuilder WithFeed(MaterialStreamBuilder feed, int stageNumber)
        { Object.ConnectFeed(feed.Object, stageNumber); return this; }

        /// <summary>Connects a feed to the stage named as the editor shows it (<c>"Stage10"</c>) and returns this
        /// builder for chaining.</summary>
        public DistillationColumnBuilder WithFeed(MaterialStreamBuilder feed, string stageName)
        { Object.ConnectFeed(feed.Object, stageName); return this; }

        /// <summary>Sets <c>Distillate</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithDistillate(MaterialStreamBuilder distillate)
        { Object.ConnectDistillate(distillate.Object); return this; }

        /// <summary>Sets <c>Bottoms</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithBottoms(MaterialStreamBuilder bottoms)
        { Object.ConnectBottoms(bottoms.Object); return this; }

        /// <summary>Sets <c>Vapor Product</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithVaporProduct(MaterialStreamBuilder vapor)
        { Object.ConnectVaporProduct(vapor.Object); return this; }

        /// <summary>Connects a side draw to a stage by its zero-based index (0 is the top stage), as a liquid
        /// (default) or vapour draw with a fixed molar flow, and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithSideDraw(MaterialStreamBuilder stream, int stageNumber, Quantity molarFlow, bool vapor = false)
        { Object.ConnectSideDraw(stream.Object, stageNumber, vapor, molarFlow.SI); return this; }

        /// <summary>
        /// Connects the free water decanted from the condenser (it needs a column solver that decants it, such as the
        /// Inside-Out solver of the Refining pack): the condenser liquid returns as reflux without its water.
        /// </summary>
        public DistillationColumnBuilder WithFreeWater(MaterialStreamBuilder stream)
        { Object.ConnectFreeWater(stream.Object); return this; }

        /// <summary>
        /// Adds a secondary specification: a property of a product (a distillation curve point such as
        /// <c>"D86 95%"</c>, a product property such as <c>"Flash Point (Closed Cup)"</c>, or a stream property
        /// ID) reached by moving the condenser or reboiler specification value, or the rate of a side draw.
        /// Target and tolerance are in SI units (K for temperatures); a zero tolerance takes 0.1 % of the target.
        /// </summary>
        public DistillationColumnBuilder WithSecondarySpec(MaterialStreamBuilder product, string property, double targetSI,
            DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec.ManipulatedVariableType variable,
            MaterialStreamBuilder sideDraw = null, double toleranceSI = 0.0, string name = "")
        {
            Object.SecondarySpecs.Add(new DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec
            {
                Name = name,
                ProductStreamID = product.Object.Name,
                PropertyName = property,
                TargetValue = targetSI,
                Tolerance = toleranceSI,
                ManipulatedVariable = variable,
                ManipulatedStreamID = sideDraw?.Object.Name ?? ""
            });
            return this;
        }

        /// <summary>Sets <c>Condenser Duty</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithCondenserDuty(EnergyStreamBuilder duty)
        { Object.ConnectCondenserDuty(duty.Object); return this; }

        /// <summary>Sets <c>Reboiler Duty</c> and returns this builder for chaining.</summary>
        public DistillationColumnBuilder WithReboilerDuty(EnergyStreamBuilder duty)
        { Object.ConnectReboilerDuty(duty.Object); return this; }

        /// <summary>Sets the condenser specification (e.g. "Reflux Ratio", value, "" for unitless).
        /// The unit can also travel inside the spec type: <c>"Product Flow Rate (mol/s)"</c>.</summary>
        public DistillationColumnBuilder WithCondenserSpec(string specType, double value, string units = "", string compound = "")
        { Object.SetCondenserSpec(specType, value, units, compound); return this; }

        /// <summary>Sets the condenser specification from a <see cref="Quantity"/>
        /// (e.g. <c>"Product Molar Flow Rate", 75.0.MolPerSecond()</c>).</summary>
        public DistillationColumnBuilder WithCondenserSpec(string specType, Quantity value, string compound = "")
        { Object.SetCondenserSpec(specType, value.SI, SpecUnitOf(value), compound); return this; }

        /// <summary>Sets the reboiler specification (e.g. "Product Molar Flow Rate", 75, "mol/s").
        /// The unit can also travel inside the spec type: <c>"Product Flow Rate (mol/s)"</c>.</summary>
        public DistillationColumnBuilder WithReboilerSpec(string specType, double value, string units = "", string compound = "")
        { Object.SetReboilerSpec(specType, value, units, compound); return this; }

        /// <summary>Sets the reboiler specification from a <see cref="Quantity"/>
        /// (e.g. <c>"Product Molar Flow Rate", 25.0.MolPerSecond()</c>).</summary>
        public DistillationColumnBuilder WithReboilerSpec(string specType, Quantity value, string compound = "")
        { Object.SetReboilerSpec(specType, value.SI, SpecUnitOf(value), compound); return this; }

        // A Quantity is already in SI, so the column only needs to be told which SI unit that is.
        private static string SpecUnitOf(Quantity q)
        {
            switch (q.Dimension)
            {
                case "T": return "K";
                case "P": return "Pa";
                case "Mflow": return "kg/s";
                case "Nflow": return "mol/s";
                case "Power": return "kW";
                default: return "";
            }
        }
    }
}
