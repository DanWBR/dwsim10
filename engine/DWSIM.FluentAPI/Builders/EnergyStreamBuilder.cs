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

        // ------------------------------------------------------- Layout / orientation

        /// <summary>Mirrors the stream horizontally.</summary>
        public EnergyStreamBuilder FlipHorizontal(bool flipped = true) { Object.GraphicObject.FlippedH = flipped; return this; }

        /// <summary>Mirrors the stream vertically.</summary>
        public EnergyStreamBuilder FlipVertical(bool flipped = true) { Object.GraphicObject.FlippedV = flipped; return this; }

        /// <summary>Rotates the stream on the canvas; use 0, 90, 180 or 270 degrees.</summary>
        public EnergyStreamBuilder Rotate(int degrees) { Object.GraphicObject.Rotation = ((degrees % 360) + 360) % 360; return this; }

        /// <summary>Places the stream at (x, y) on the canvas.</summary>
        public EnergyStreamBuilder PositionAt(int x, int y) { Object.GraphicObject.X = x; Object.GraphicObject.Y = y; return this; }
    }
}
