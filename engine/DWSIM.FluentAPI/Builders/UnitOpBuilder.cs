using System;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.Streams;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Base class for all fluent unit-operation builders. Provides port-based
    /// connection helpers (feed/product material and energy streams) shared by
    /// every <see cref="ISimulationObject"/>.
    /// </summary>
    /// <typeparam name="TObject">Concrete DWSIM unit-operation class.</typeparam>
    /// <typeparam name="TSelf">CRTP self type so chained calls return the derived builder.</typeparam>
    public abstract class UnitOpBuilder<TObject, TSelf>
        where TObject : ISimulationObject
        where TSelf : UnitOpBuilder<TObject, TSelf>
    {
        /// <summary>The owning flowsheet.</summary>
        public Flowsheet Flowsheet { get; }
        /// <summary>The underlying DWSIM object.</summary>
        public TObject Object { get; }

        /// <summary>Initialises the builder with its owning flowsheet and the underlying DWSIM object.</summary>
        protected UnitOpBuilder(Flowsheet flowsheet, TObject obj)
        {
            Flowsheet = flowsheet;
            Object = obj;
        }

        /// <summary>Returns this cast to the derived builder type, for chaining.</summary>
        protected TSelf Self => (TSelf)this;

        // ----------------------------------------------------------- Connections

        /// <summary>Connects a material stream as a feed at the given port (default 0).</summary>
        public TSelf ConnectFeed(MaterialStreamBuilder stream, int port = 0)
        {
            Object.ConnectFeedMaterialStream(stream.Object, port);
            return Self;
        }

        /// <summary>Connects a material stream as a product at the given port (default 0).</summary>
        public TSelf ConnectProduct(MaterialStreamBuilder stream, int port = 0)
        {
            Object.ConnectProductMaterialStream(stream.Object, port);
            return Self;
        }

        /// <summary>Connects an energy stream as a feed at the given port.</summary>
        public TSelf ConnectEnergyFeed(EnergyStreamBuilder stream, int port = 0)
        {
            Object.ConnectFeedEnergyStream(stream.Object, port);
            return Self;
        }

        /// <summary>Connects an energy stream as a product at the given port.</summary>
        public TSelf ConnectEnergyProduct(EnergyStreamBuilder stream, int port = 0)
        {
            Object.ConnectProductEnergyStream(stream.Object, port);
            return Self;
        }

        /// <summary>
        /// Creates a new material stream with <paramref name="newTag"/> and connects it as a product
        /// at the given port. Returns the new stream's builder for further chaining.
        /// </summary>
        public MaterialStreamBuilder ConnectNewProduct(string newTag, int port = 0)
        {
            var s = Flowsheet.AddMaterialStream(newTag);
            Object.ConnectProductMaterialStream(s.Object, port);
            return s;
        }

        /// <summary>Escape hatch: applies an arbitrary mutation to the underlying DWSIM object.</summary>
        public TSelf Configure(Action<TObject> action)
        {
            action?.Invoke(Object);
            return Self;
        }

        // ----------------------------------------------------------- Dynamic mode

        /// <summary>
        /// Sets a dynamic-mode property by name, e.g. <c>"Liquid Level"</c> or <c>"Volume"</c>.
        /// The value is in SI units, matching what DWSIM stores internally.
        /// </summary>
        /// <exception cref="ArgumentException">The object has no dynamic property by that name.</exception>
        public TSelf WithDynamicProperty(string name, double value)
        {
            RequireDynamicProperty(name);
            Object.AddDynamicProperty(name, value);
            return Self;
        }

        /// <summary>Sets a dynamic-mode property from a unit-aware quantity.</summary>
        public TSelf WithDynamicProperty(string name, Quantity value)
        {
            return WithDynamicProperty(name, value.SI);
        }

        /// <summary>Sets a boolean dynamic-mode property, e.g. <c>"Reset Content"</c>.</summary>
        public TSelf WithDynamicProperty(string name, bool value)
        {
            RequireDynamicProperty(name);
            Object.AddDynamicProperty(name, value);
            return Self;
        }

        /// <summary>Reads a dynamic-mode property, or null when the object has none by that name.</summary>
        public object GetDynamicProperty(string name)
        {
            PropertyCatalog.EnsureDynamicProperties(Object);
            return Object.GetDynamicProperty(name);
        }

        /// <summary>Reads a numeric dynamic-mode property in SI units, or 0 when it is unset.</summary>
        public double GetDynamicValue(string name)
        {
            var value = GetDynamicProperty(name);
            return value == null ? 0.0 : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Every dynamic-mode property this object exposes, with descriptions and units.</summary>
        public System.Collections.Generic.IReadOnlyList<PropertyEntry> DynamicProperties =>
            PropertyCatalog.DynamicFor(Object, Flowsheet.Inner.FlowsheetOptions.SelectedUnitSystem);

        /// <summary>
        /// Declares whether the object is specified by pressure or by flow in the dynamic
        /// pressure-flow network. A network with no pressure specification anywhere is underdetermined.
        /// </summary>
        public TSelf WithDynamicsSpec(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType spec)
        {
            Object.DynamicsSpec = spec;
            return Self;
        }

        private void RequireDynamicProperty(string name)
        {
            PropertyCatalog.EnsureDynamicProperties(Object);
            if (Object.IsDynamicProperty(name)) return;

            var known = PropertyCatalog.DynamicFor(Object, Flowsheet.Inner.FlowsheetOptions.SelectedUnitSystem);
            var names = known.Count == 0
                ? "it has none"
                : string.Join(", ", System.Linq.Enumerable.Select(known, p => "'" + p.Id + "'"));
            throw new ArgumentException(
                "'" + (Object.GraphicObject?.Tag ?? Object.Name) + "' has no dynamic property '" + name +
                "'. Available: " + names + ".", nameof(name));
        }

        // ----------------------------------------------------------- Layout / orientation

        /// <summary>Mirrors the object horizontally (swaps its inlet and outlet sides), as one does on a recycle return.</summary>
        public TSelf FlipHorizontal(bool flipped = true) { Object.GraphicObject.FlippedH = flipped; return Self; }

        /// <summary>Mirrors the object vertically (swaps its top and bottom).</summary>
        public TSelf FlipVertical(bool flipped = true) { Object.GraphicObject.FlippedV = flipped; return Self; }

        /// <summary>Rotates the object on the canvas; use 0, 90, 180 or 270 degrees.</summary>
        public TSelf Rotate(int degrees) { Object.GraphicObject.Rotation = ((degrees % 360) + 360) % 360; return Self; }

        /// <summary>Places the object at (x, y) on the canvas.</summary>
        public TSelf PositionAt(int x, int y) { Object.GraphicObject.X = x; Object.GraphicObject.Y = y; return Self; }
    }
}
