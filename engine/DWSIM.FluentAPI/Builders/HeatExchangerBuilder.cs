using System.Runtime.CompilerServices;
using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Heat Exchanger unit operation. Call <see cref="Flowsheet.AddHeatExchanger"/> to obtain one.</summary>
    public sealed class HeatExchangerBuilder : UnitOpBuilder<HeatExchanger, HeatExchangerBuilder>
    {
        internal HeatExchangerBuilder(Flowsheet f, HeatExchanger o) : base(f, o) { }

        // The exchanger has no UA input: its UA mode multiplies the overall coefficient by the area.
        // A UA given through the builder is remembered per exchanger, so an area set afterwards
        // keeps the product instead of scaling it.
        private static readonly ConditionalWeakTable<HeatExchanger, StrongBox<double>> _ua =
            new ConditionalWeakTable<HeatExchanger, StrongBox<double>>();

        /// <summary>Sets <c>Calculation Mode</c> and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithCalculationMode(HeatExchangerCalcMode mode)
        { Object.CalculationMode = mode; return this; }

        /// <summary>Sets <c>Hot Side Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithHotSidePressureDrop(Quantity dp) { Object.HotSidePressureDrop = dp.SI; return this; }
        /// <summary>Sets <c>Cold Side Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithColdSidePressureDrop(Quantity dp) { Object.ColdSidePressureDrop = dp.SI; return this; }

        /// <summary>
        /// Sets the global UA product, in W/K, and switches the exchanger to the UA mode
        /// (<see cref="HeatExchangerCalcMode.CalcBothTemp_UA"/>). The exchanger computes the duty from
        /// the overall coefficient times the area, so the builder stores U = UA / A for the current
        /// area; an area set later through <see cref="WithExchangeArea"/> keeps the product.
        /// </summary>
        public HeatExchangerBuilder WithGlobalUA(double ua)
        {
            _ua.Remove(Object);
            _ua.Add(Object, new StrongBox<double>(ua));
            Object.CalculationMode = HeatExchangerCalcMode.CalcBothTemp_UA;
            ApplyUA();
            return this;
        }

        /// <summary>
        /// Sets the overall heat transfer coefficient U, in W/(m2.K). It replaces a UA given earlier
        /// through <see cref="WithGlobalUA"/>.
        /// </summary>
        public HeatExchangerBuilder WithOverallCoefficient(double u)
        {
            _ua.Remove(Object);
            Object.OverallCoefficient = u;
            return this;
        }

        /// <summary>
        /// Sets <c>Exchange Area</c>, in m2, and returns this builder for chaining. After
        /// <see cref="WithGlobalUA"/> the overall coefficient is recomputed so that the UA is kept.
        /// </summary>
        public HeatExchangerBuilder WithExchangeArea(double m2)
        {
            Object.Area = m2;
            ApplyUA();
            return this;
        }

        /// <summary>The UA product the exchanger works with, the overall coefficient times the area, in W/K.</summary>
        public double GlobalUA => Object.OverallCoefficient.GetValueOrDefault() * Object.Area.GetValueOrDefault();

        private void ApplyUA()
        {
            if (!_ua.TryGetValue(Object, out var ua)) return;
            var area = Object.Area.GetValueOrDefault();
            if (!(area > 0)) { area = 1.0; Object.Area = area; }
            Object.OverallCoefficient = ua.Value / area;
        }
    }
}
