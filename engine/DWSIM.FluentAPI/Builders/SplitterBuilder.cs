using System;
using System.Linq;
using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Splitter unit operation. Call <see cref="Flowsheet.AddSplitter"/> to obtain one.</summary>
    public sealed class SplitterBuilder : UnitOpBuilder<Splitter, SplitterBuilder>
    {
        internal SplitterBuilder(Flowsheet f, Splitter o) : base(f, o) { }

        /// <summary>
        /// Sets the split ratios of the outlets, in port order, and puts the splitter in
        /// <c>SplitRatios</c> mode. The splitter always keeps three slots; the ones not given
        /// are zero, and when only the first outlets are given the last one given is completed
        /// so the ratios sum to 1.
        /// </summary>
        /// <param name="ratios">One fraction per outlet, between 0 and 1.</param>
        public SplitterBuilder WithSplitRatios(params double[] ratios)
        {
            if (ratios == null || ratios.Length == 0) throw new ArgumentException("At least one split ratio is required.", nameof(ratios));
            if (ratios.Length > 3) throw new ArgumentException("A splitter has at most three outlets.", nameof(ratios));
            if (ratios.Any(r => r < 0.0 || r > 1.0)) throw new ArgumentException("Split ratios are fractions between 0 and 1.", nameof(ratios));

            var slots = new double[3];
            for (var i = 0; i < ratios.Length; i++) slots[i] = ratios[i];

            // One ratio given: the second outlet takes the rest, so a two-way split is one number.
            if (ratios.Length == 1) slots[1] = 1.0 - ratios[0];

            var sum = slots.Sum();
            if (Math.Abs(sum - 1.0) > 1e-6) throw new ArgumentException("Split ratios must sum to 1; they sum to " + sum + ".", nameof(ratios));

            Object.Ratios.Clear();
            foreach (var slot in slots) Object.Ratios.Add(slot);
            Object.OperationMode = Splitter.OpMode.SplitRatios;
            return this;
        }

        /// <summary>
        /// Specifies the mass flow of the first outlet (and of the second, when three outlets are
        /// connected); the last outlet takes what is left. Puts the splitter in <c>StreamMassFlowSpec</c> mode.
        /// </summary>
        public SplitterBuilder WithMassFlowSpecs(Quantity first, Quantity? second = null)
        {
            Object.StreamFlowSpec = first.SI;
            if (second.HasValue) Object.Stream2FlowSpec = second.Value.SI;
            Object.OperationMode = Splitter.OpMode.StreamMassFlowSpec;
            return this;
        }

        /// <summary>
        /// Specifies the molar flow of the first outlet (and of the second, when three outlets are
        /// connected); the last outlet takes what is left. Puts the splitter in <c>StreamMoleFlowSpec</c> mode.
        /// </summary>
        public SplitterBuilder WithMolarFlowSpecs(Quantity first, Quantity? second = null)
        {
            Object.StreamFlowSpec = first.SI;
            if (second.HasValue) Object.Stream2FlowSpec = second.Value.SI;
            Object.OperationMode = Splitter.OpMode.StreamMoleFlowSpec;
            return this;
        }

        /// <summary>Same as <see cref="WithMassFlowSpecs"/>, kept under the name the documentation used.</summary>
        public SplitterBuilder WithFlowSpecs(params double[] massFlowsKgPerS)
        {
            if (massFlowsKgPerS == null || massFlowsKgPerS.Length == 0) throw new ArgumentException("At least one flow is required.", nameof(massFlowsKgPerS));
            Object.StreamFlowSpec = massFlowsKgPerS[0];
            if (massFlowsKgPerS.Length > 1) Object.Stream2FlowSpec = massFlowsKgPerS[1];
            Object.OperationMode = Splitter.OpMode.StreamMassFlowSpec;
            return this;
        }
    }
}
