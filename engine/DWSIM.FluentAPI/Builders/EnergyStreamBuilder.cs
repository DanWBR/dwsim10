using System;
using DWSIM.UnitOperations.Streams;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent wrapper for an <see cref="EnergyStream"/>. Energy in DWSIM is in kW.</summary>
    public sealed class EnergyStreamBuilder
    {
        /// <summary>The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.</summary>
        public Flowsheet Flowsheet { get; }
        /// <summary>The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.</summary>
        public EnergyStream Object { get; }

        internal EnergyStreamBuilder(Flowsheet flowsheet, EnergyStream obj)
        {
            Flowsheet = flowsheet;
            Object = obj;
        }

        /// <summary>Sets the energy flow (kW). Pass via <c>10.Kilowatts()</c>.</summary>
        public EnergyStreamBuilder WithEnergyFlow(Quantity power)
        {
            Object.EnergyFlow = power.SI; // SI here = kW (DWSIM convention for EnergyFlow)
            return this;
        }

        /// <summary>Read-back of <c>Energy Flow KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double EnergyFlowKW => Object.EnergyFlow.GetValueOrDefault();

        /// <summary>Escape hatch for any property not covered by a <c>WithX</c> helper. Mutates the underlying object via the supplied delegate.</summary>
        public EnergyStreamBuilder Configure(Action<EnergyStream> action)
        {
            action?.Invoke(Object);
            return this;
        }
    }
}
