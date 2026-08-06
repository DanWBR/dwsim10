using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Sets overall compound molar flow (mol/s).</summary>
        public MaterialStreamBuilder SetCompoundMolarFlow(string compound, double molPerSecond)
        {
            Object.SetOverallCompoundMolarFlow(compound, molPerSecond);
            return this;
        }

        /// <summary>Sets overall compound mass flow (kg/s).</summary>
        public MaterialStreamBuilder SetCompoundMassFlow(string compound, double kgPerSecond)
        {
            Object.SetOverallCompoundMassFlow(compound, kgPerSecond);
            return this;
        }

        /// <summary>Configures composition fluently. Use <c>.Mole</c> / <c>.Mass</c> inside the builder.</summary>
        public MaterialStreamBuilder WithComposition(Action<CompositionBuilder> configure)
        {
            var c = new CompositionBuilder(Object);
            configure(c);
            c.Apply();
            return this;
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

        /// <summary>Escape hatch for any property not covered by a <c>WithX</c> helper. Mutates the underlying object via the supplied delegate.</summary>
        public MaterialStreamBuilder Configure(Action<MaterialStream> action)
        {
            action?.Invoke(Object);
            return this;
        }

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

        /// <summary>Adds a compound mole fraction. All entries are normalized when applied.</summary>
        public CompositionBuilder Mole(string compound, double fraction)
        { _mole[compound] = fraction; return this; }

        /// <summary>Adds a compound mass fraction. All entries are normalized when applied.</summary>
        public CompositionBuilder Mass(string compound, double fraction)
        { _mass[compound] = fraction; return this; }

        internal void Apply()
        {
            if (_mole.Count > 0)
            {
                double sum = 0; foreach (var v in _mole.Values) sum += v;
                if (sum <= 0) throw new InvalidOperationException("Mole fractions sum to zero.");
                var n = _stream.GetMolarFlow();
                if (n <= 0) n = 1.0; // default basis
                foreach (var kv in _mole)
                    _stream.SetOverallCompoundMolarFlow(kv.Key, kv.Value / sum * n);
            }
            else if (_mass.Count > 0)
            {
                double sum = 0; foreach (var v in _mass.Values) sum += v;
                if (sum <= 0) throw new InvalidOperationException("Mass fractions sum to zero.");
                var m = _stream.GetMassFlow();
                if (m <= 0) m = 1.0;
                foreach (var kv in _mass)
                    _stream.SetOverallCompoundMassFlow(kv.Key, kv.Value / sum * m);
            }
        }
    }
}
