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
    }
}
