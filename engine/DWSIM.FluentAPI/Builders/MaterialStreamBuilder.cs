using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.Streams;
using PP = DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent wrapper for a <see cref="MaterialStream"/>.</summary>
    public sealed class MaterialStreamBuilder
    {
        /// <summary>The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.</summary>
        public Flowsheet Flowsheet { get; }
        /// <summary>The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.</summary>
        public MaterialStream Object { get; }

        internal MaterialStreamBuilder(Flowsheet flowsheet, MaterialStream obj)
        {
            Flowsheet = flowsheet;
            Object = obj;
        }

        /// <summary>Sets temperature and pressure.</summary>
        public MaterialStreamBuilder At(Quantity temperature, Quantity pressure)
        {
            Object.SetTemperature(temperature.SI);
            Object.SetPressure(pressure.SI);
            return this;
        }

        /// <summary>Sets <c>Temperature</c> (SI) and returns this builder for chaining.</summary>
        public MaterialStreamBuilder WithTemperature(Quantity t) { Object.SetTemperature(t.SI); return this; }
        /// <summary>Sets <c>Pressure</c> (SI) and returns this builder for chaining.</summary>
        public MaterialStreamBuilder WithPressure(Quantity p) { Object.SetPressure(p.SI); return this; }
        /// <summary>Sets <c>Mass Flow</c> (SI) and returns this builder for chaining.</summary>
        public MaterialStreamBuilder WithMassFlow(Quantity m) { Object.SetMassFlow(m.SI); return this; }
        /// <summary>Sets <c>Molar Flow</c> (SI) and returns this builder for chaining.</summary>
        public MaterialStreamBuilder WithMolarFlow(Quantity n) { Object.SetMolarFlow(n.SI); return this; }
        /// <summary>Sets <c>Volumetric Flow</c> (SI) and returns this builder for chaining.</summary>
        public MaterialStreamBuilder WithVolumetricFlow(Quantity q) { Object.SetVolumetricFlow(q.SI); return this; }
        /// <summary>Sets <c>Vapor Fraction</c> and returns this builder for chaining.</summary>
        public MaterialStreamBuilder WithVaporFraction(double frac)
        {
            // Use SetMolarVaporFraction when available; PhasesEnum is also acceptable.
            Object.GetType().GetMethod("SetMolarFraction")?.Invoke(Object, new object[] { frac });
            return this;
        }

        /// <summary>
        /// Sets the overall molar flow (mol/s) of one compound. On a stream created by
        /// <see cref="Flowsheet.AddMaterialStream"/> the compounds never named through the builder
        /// are set to zero, so a feed defined one compound at a time carries only the compounds
        /// named; on any other stream the other compounds keep their flows.
        /// </summary>
        public MaterialStreamBuilder SetCompoundMolarFlow(string compound, double molPerSecond)
        {
            Object.SetOverallCompoundMolarFlow(compound, molPerSecond);
            ClearUnnamed(new[] { compound }, molar: true);
            return this;
        }

        /// <summary>
        /// Sets the overall mass flow (kg/s) of one compound. On a stream created by
        /// <see cref="Flowsheet.AddMaterialStream"/> the compounds never named through the builder
        /// are set to zero, so a feed defined one compound at a time carries only the compounds
        /// named; on any other stream the other compounds keep their flows.
        /// </summary>
        public MaterialStreamBuilder SetCompoundMassFlow(string compound, double kgPerSecond)
        {
            Object.SetOverallCompoundMassFlow(compound, kgPerSecond);
            ClearUnnamed(new[] { compound }, molar: false);
            return this;
        }

        /// <summary>
        /// Configures the whole composition fluently. Use <c>.Mole</c> / <c>.Mass</c> inside the
        /// builder; every compound not named is set to zero.
        /// </summary>
        public MaterialStreamBuilder WithComposition(Action<CompositionBuilder> configure)
        {
            var c = new CompositionBuilder(Object);
            configure(c);
            c.Apply();
            if (_named.TryGetValue(Object, out var named))
                foreach (var name in c.Named) named.Add(name);
            return this;
        }

        // ------------------------------------------------------- Composition tracking

        // A new stream starts with every compound at the flowsheet's default share, so setting one
        // compound's flow would leave that share on every compound the caller never mentioned.
        // Streams the builder creates are tracked here with the compounds named so far; the
        // rest are zeroed on each per-compound call. A stream loaded from a file or created
        // elsewhere is not tracked and keeps the per-compound override semantics.
        private static readonly ConditionalWeakTable<MaterialStream, HashSet<string>> _named =
            new ConditionalWeakTable<MaterialStream, HashSet<string>>();

        /// <summary>Starts composition tracking on a stream the builder just created.</summary>
        internal static void TrackNew(MaterialStream stream)
        {
            _named.GetOrCreateValue(stream);
        }

        /// <summary>
        /// Records <paramref name="compounds"/> as named and zeroes every other compound of a tracked
        /// stream, once the named compounds carry a positive flow to normalise against.
        /// </summary>
        private void ClearUnnamed(IEnumerable<string> compounds, bool molar)
        {
            if (!_named.TryGetValue(Object, out var named)) return;
            foreach (var c in compounds) named.Add(c);

            var all = Object.Phases[0].Compounds.Values.ToList();
            double Flow(DWSIM.Interfaces.ICompound c) =>
                molar ? c.MolarFlow.GetValueOrDefault() : c.MassFlow.GetValueOrDefault();

            double kept = all.Where(c => named.Contains(c.Name)).Sum(Flow);
            if (!(kept > 0)) return;

            foreach (var c in all)
            {
                if (named.Contains(c.Name) || Flow(c) == 0) continue;
                if (molar) Object.SetOverallCompoundMolarFlow(c.Name, 0.0);
                else Object.SetOverallCompoundMassFlow(c.Name, 0.0);
            }
        }

        // ------------------------------------------------------- Read accessors

        /// <summary>Read-back of <c>Temperature K</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double TemperatureK => Object.GetTemperature();
        /// <summary>Read-back of <c>Pressure Pa</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double PressurePa => Object.GetPressure();
        /// <summary>Read-back of <c>Mass Flow Kg Per Second</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double MassFlowKgPerSecond => Object.GetMassFlow();
        /// <summary>Read-back of <c>Molar Flow Mol Per Second</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double MolarFlowMolPerSecond => Object.GetMolarFlow();
        /// <summary>Read-back of <c>Volumetric Flow M3Per Second</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double VolumetricFlowM3PerSecond => Object.GetVolumetricFlow();

        /// <summary>Mole fraction of <paramref name="compound"/> in the overall (mixture) phase.</summary>
        public double OverallMoleFraction(string compound)
            => Object.Phases[0].Compounds[compound].MoleFraction.GetValueOrDefault();

        /// <summary>Mass fraction of <paramml name="compound"/> in the overall (mixture) phase.</summary>
        public double OverallMassFraction(string compound)
            => Object.Phases[0].Compounds[compound].MassFraction.GetValueOrDefault();

        // ----------------------------------------------------------- Dynamic mode

        /// <summary>
        /// Declares whether this stream is specified by pressure or by flow in the dynamic
        /// pressure-flow network.
        /// </summary>
        public MaterialStreamBuilder WithDynamicsSpec(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType spec)
        {
            Object.DynamicsSpec = spec;
            return this;
        }

        /// <summary>
        /// Specifies this stream by flow: its mass flow is held and its pressure is whatever the
        /// network resolves to. This is the usual choice for a feed.
        /// </summary>
        public MaterialStreamBuilder AsFlowSpec() =>
            WithDynamicsSpec(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType.Flow);

        /// <summary>
        /// Specifies this stream by pressure: its pressure is held and its flow is whatever the
        /// network resolves to. A network needs at least one of these or it is underdetermined.
        /// </summary>
        public MaterialStreamBuilder AsPressureSpec() =>
            WithDynamicsSpec(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType.Pressure);

        /// <summary>The stream's current pressure-flow specification.</summary>
        public DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType DynamicsSpec => Object.DynamicsSpec;

        /// <summary>Escape hatch: applies an arbitrary mutation to the underlying stream.</summary>
        public MaterialStreamBuilder Configure(Action<MaterialStream> action)
        {
            action?.Invoke(Object);
            return this;
        }

        // ------------------------------------------------------- Layout / orientation

        /// <summary>Mirrors the stream horizontally (points its arrow the other way), as one does on a recycle return.</summary>
        public MaterialStreamBuilder FlipHorizontal(bool flipped = true) { Object.GraphicObject.FlippedH = flipped; return this; }

        /// <summary>Mirrors the stream vertically.</summary>
        public MaterialStreamBuilder FlipVertical(bool flipped = true) { Object.GraphicObject.FlippedV = flipped; return this; }

        /// <summary>Rotates the stream on the canvas; use 0, 90, 180 or 270 degrees.</summary>
        public MaterialStreamBuilder Rotate(int degrees) { Object.GraphicObject.Rotation = ((degrees % 360) + 360) % 360; return this; }

        /// <summary>Places the stream at (x, y) on the canvas.</summary>
        public MaterialStreamBuilder PositionAt(int x, int y) { Object.GraphicObject.X = x; Object.GraphicObject.Y = y; return this; }

        // ------------------------------------------------------- Thermodynamic analysis

        /// <summary>
        /// Computes the phase envelope (bubble/dew curves, critical point, optional quality line,
        /// LLE, SLE, Widom line) for the current stream composition.
        /// The stream must have a property package assigned and a valid composition.
        /// </summary>
        /// <param name="configure">Optional callback to customise <see cref="PP.PhaseEnvelopeOptions"/>
        /// (quality line, hydrate, stability curve, SLE, custom initial conditions, etc.).</param>
        public PhaseEnvelopeResult CalculatePhaseEnvelope(
            Action<PP.PhaseEnvelopeOptions> configure = null)
        {
            var pp = GetPropertyPackage();
            var opts = new PP.PhaseEnvelopeOptions();
            configure?.Invoke(opts);
            var raw = (object[])pp.DW_ReturnPhaseEnvelope(opts, null);
            return new PhaseEnvelopeResult(raw);
        }

        /// <summary>
        /// Computes a T-x-y binary phase diagram at the given pressure.
        /// The stream must contain exactly two compounds.
        /// </summary>
        /// <param name="pressurePa">System pressure in Pa.</param>
        /// <param name="includeVLE">Calculate vapor-liquid equilibrium curves (default true).</param>
        /// <param name="includeLLE">Calculate liquid-liquid equilibrium curves (default false).</param>
        /// <param name="includeSLE">Calculate solid-liquid equilibrium curves (default false).</param>
        /// <param name="includeCritical">Calculate critical locus (default false).</param>
        /// <param name="steps">Number of composition steps (default 40).</param>
        public BinaryEnvelopeResult CalculateBinaryDiagram_Txy(
            double pressurePa,
            bool includeVLE = true,
            bool includeLLE = false,
            bool includeSLE = false,
            bool includeCritical = false,
            int steps = 40)
        {
            return CalculateBinaryDiagramCore("T-x-y",
                pressurePa, 0.0, includeVLE, includeLLE, includeSLE,
                includeCritical, false, steps, 0.0, 1.0);
        }

        /// <summary>
        /// Computes a P-x-y binary phase diagram at the given temperature.
        /// The stream must contain exactly two compounds.
        /// </summary>
        /// <param name="temperatureK">System temperature in K.</param>
        /// <param name="includeVLE">Calculate VLE curves (default true).</param>
        /// <param name="includeLLE">Calculate LLE curves (default false).</param>
        /// <param name="steps">Number of composition steps (default 40).</param>
        public BinaryEnvelopeResult CalculateBinaryDiagram_Pxy(
            double temperatureK,
            bool includeVLE = true,
            bool includeLLE = false,
            int steps = 40)
        {
            return CalculateBinaryDiagramCore("P-x-y",
                0.0, temperatureK, includeVLE, includeLLE, false,
                false, false, steps, 0.0, 1.0);
        }

        /// <summary>
        /// Calculates the mixture critical point(s) for the current stream composition.
        /// Returns an empty list for pure components (use compound data instead).
        /// </summary>
        public IReadOnlyList<CriticalPointResult> CalculateCriticalPoints()
        {
            var pp = GetPropertyPackage();
            var raw = pp.DW_CalculateCriticalPoints();
            var result = new List<CriticalPointResult>(raw.Count);
            foreach (var pt in raw)
                result.Add(new CriticalPointResult(pt[0], pt[1], pt[2]));
            return result;
        }

        private BinaryEnvelopeResult CalculateBinaryDiagramCore(
            string type, double p, double t,
            bool vle, bool lle, bool sle, bool critical, bool solidSolution,
            int steps, double minX, double maxX)
        {
            var pp = GetPropertyPackage();
            var parameters = new object[13];
            parameters[0] = type;
            parameters[1] = p;
            parameters[2] = t;
            parameters[3] = vle;
            parameters[4] = lle;
            parameters[5] = sle;
            parameters[6] = critical;
            parameters[7] = solidSolution;
            parameters[10] = steps;
            parameters[11] = minX;
            parameters[12] = maxX;
            var raw = (object[])pp.DW_ReturnBinaryEnvelope(parameters, null);
            return new BinaryEnvelopeResult(type, raw);
        }

        private PP.PropertyPackage GetPropertyPackage()
        {
            var pp = Object.PropertyPackage as PP.PropertyPackage;
            if (pp == null)
                throw new InvalidOperationException(
                    "No property package assigned to this material stream. " +
                    "Add a property package to the flowsheet first.");
            pp.CurrentMaterialStream = Object;
            return pp;
        }
    }

    /// <summary>Helper used by <see cref="MaterialStreamBuilder.WithComposition"/>.</summary>
    public sealed class CompositionBuilder
    {
        private readonly MaterialStream _stream;
        private readonly Dictionary<string, double> _mole = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _mass = new Dictionary<string, double>();

        internal CompositionBuilder(MaterialStream stream) { _stream = stream; }

        /// <summary>The compounds named through <see cref="Mole"/> or <see cref="Mass"/>.</summary>
        internal IEnumerable<string> Named => _mole.Count > 0 ? _mole.Keys : _mass.Keys;

        /// <summary>Adds a compound mole fraction. All entries are normalized when applied.</summary>
        public CompositionBuilder Mole(string compound, double fraction)
        { _mole[compound] = fraction; return this; }

        /// <summary>Adds a compound mass fraction. All entries are normalized when applied.</summary>
        public CompositionBuilder Mass(string compound, double fraction)
        { _mass[compound] = fraction; return this; }

        internal void Apply()
        {
            // The composition is applied through per-compound flows, which moves the stream's
            // flow basis to whatever basis the fractions were given on. A flow the caller set
            // beforehand is restored on its own basis afterwards, so 10 kg/s stays 10 kg/s.
            var basis = _stream.DefinedFlow;
            var mass = Usable(_stream.GetMassFlow());
            var mole = Usable(_stream.GetMolarFlow());
            var volume = Usable(_stream.GetVolumetricFlow());

            if (_mole.Count > 0)
            {
                double sum = 0; foreach (var v in _mole.Values) sum += v;
                if (sum <= 0) throw new InvalidOperationException("Mole fractions sum to zero.");
                var n = mole > 0 ? mole : 1.0; // default basis
                foreach (var kv in _mole)
                    _stream.SetOverallCompoundMolarFlow(kv.Key, kv.Value / sum * n);
                foreach (var c in _stream.Phases[0].Compounds.Values.ToList())
                    if (!_mole.ContainsKey(c.Name) && c.MolarFlow.GetValueOrDefault() != 0)
                        _stream.SetOverallCompoundMolarFlow(c.Name, 0.0);
            }
            else if (_mass.Count > 0)
            {
                double sum = 0; foreach (var v in _mass.Values) sum += v;
                if (sum <= 0) throw new InvalidOperationException("Mass fractions sum to zero.");
                var m = mass > 0 ? mass : 1.0;
                foreach (var kv in _mass)
                    _stream.SetOverallCompoundMassFlow(kv.Key, kv.Value / sum * m);
                foreach (var c in _stream.Phases[0].Compounds.Values.ToList())
                    if (!_mass.ContainsKey(c.Name) && c.MassFlow.GetValueOrDefault() != 0)
                        _stream.SetOverallCompoundMassFlow(c.Name, 0.0);
            }

            switch (basis)
            {
                case DWSIM.Interfaces.Enums.FlowSpec.Mass: if (mass > 0) _stream.SetMassFlow(mass); break;
                case DWSIM.Interfaces.Enums.FlowSpec.Mole: if (mole > 0) _stream.SetMolarFlow(mole); break;
                case DWSIM.Interfaces.Enums.FlowSpec.Volumetric: if (volume > 0) _stream.SetVolumetricFlow(volume); break;
            }
        }

        /// <summary>A flow that can be restored: finite and positive, else zero.</summary>
        private static double Usable(double flow)
        {
            return double.IsNaN(flow) || double.IsInfinity(flow) || flow <= 0 ? 0.0 : flow;
        }
    }
}
