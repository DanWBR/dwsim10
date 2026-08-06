using System.Collections.Generic;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Common reactor configuration. Shared by all reactor builders via inheritance.</summary>
    public abstract class ReactorBuilder<TObject, TSelf> : UnitOpBuilder<TObject, TSelf>
        where TObject : Reactor
        where TSelf : ReactorBuilder<TObject, TSelf>
    {
        /// <summary>Initialises the reactor builder with its owning flowsheet and the underlying DWSIM reactor.</summary>
        protected ReactorBuilder(Flowsheet f, TObject o) : base(f, o) { }

        /// <summary>Sets the thermal operation mode (Isothermic, Adiabatic, OutletTemperature, NonIsothermalNonAdiabatic, HeatExchange).</summary>
        public TSelf WithOperationMode(OperationMode mode) { Object.ReactorOperationMode = mode; return Self; }
        /// <summary>Binds the reactor to the reaction set identified by <paramref name="id"/>.</summary>
        public TSelf WithReactionSet(string id) { Object.ReactionSetID = id; return Self; }
        /// <summary>Binds the reactor to the reaction set described by <paramref name="set"/>.</summary>
        public TSelf WithReactionSet(ReactionSetBuilder set) { Object.ReactionSetID = set.Id; return Self; }
        /// <summary>Sets the inlet-to-outlet pressure drop across the reactor.</summary>
        public TSelf WithPressureDrop(Quantity dp) { Object.DeltaP = dp.SI; return Self; }
        /// <summary>Shortcut for <see cref="WithOperationMode"/> with <see cref="OperationMode.Isothermic"/>.</summary>
        public TSelf Isothermal() { Object.ReactorOperationMode = OperationMode.Isothermic; return Self; }
        /// <summary>Shortcut for <see cref="WithOperationMode"/> with <see cref="OperationMode.Adiabatic"/>.</summary>
        public TSelf Adiabatic() { Object.ReactorOperationMode = OperationMode.Adiabatic; return Self; }

        /// <summary>Heat duty exchanged with the surroundings (kW). Available after Solve().</summary>
        public double HeatDutyKW => Object.DeltaQ.GetValueOrDefault();
    }

    /// <summary>Fluent builder for the Conversion Reactor unit operation. Call <see cref="Flowsheet.AddConversionReactor"/> to obtain one.</summary>
    public sealed class ConversionReactorBuilder : ReactorBuilder<Reactor_Conversion, ConversionReactorBuilder>
    {
        internal ConversionReactorBuilder(Flowsheet f, Reactor_Conversion o) : base(f, o) { }
    }

    /// <summary>Fluent builder for the Equilibrium Reactor unit operation. Call <see cref="Flowsheet.AddEquilibriumReactor"/> to obtain one.</summary>
    public sealed class EquilibriumReactorBuilder : ReactorBuilder<Reactor_Equilibrium, EquilibriumReactorBuilder>
    {
        internal EquilibriumReactorBuilder(Flowsheet f, Reactor_Equilibrium o) : base(f, o) { }
    }

    /// <summary>Fluent builder for the Gibbs Reactor unit operation. Call <see cref="Flowsheet.AddGibbsReactor"/> to obtain one.</summary>
    public sealed class GibbsReactorBuilder : ReactorBuilder<Reactor_Gibbs, GibbsReactorBuilder>
    {
        internal GibbsReactorBuilder(Flowsheet f, Reactor_Gibbs o) : base(f, o) { }
    }

    /// <summary>Fluent builder for the CSTR unit operation. Call <see cref="Flowsheet.AddCSTR"/> to obtain one.</summary>
    public sealed class CSTRBuilder : ReactorBuilder<Reactor_CSTR, CSTRBuilder>
    {
        internal CSTRBuilder(Flowsheet f, Reactor_CSTR o) : base(f, o) { }

        /// <summary>Sets <c>Volume</c> (SI) and returns this builder for chaining.</summary>
        public CSTRBuilder WithVolume(Quantity v) { Object.Volume = v.SI; return this; }
        /// <summary>Sets <c>Headspace Fraction</c> and returns this builder for chaining.</summary>
        public CSTRBuilder WithHeadspaceFraction(double f) { Object.Headspace = f; return this; }
        /// <summary>Sets <c>Isothermal Temperature</c> (SI) and returns this builder for chaining.</summary>
        public CSTRBuilder WithIsothermalTemperature(Quantity t) { Object.IsothermalTemperature = t.SI; return this; }
        /// <summary>Sets <c>Catalyst Amount Kg</c> and returns this builder for chaining.</summary>
        public CSTRBuilder WithCatalystAmountKg(double kg) { Object.CatalystAmount = kg; return this; }
    }

    /// <summary>Fluent builder for the PFR unit operation. Call <see cref="Flowsheet.AddPFR"/> to obtain one.</summary>
    public sealed class PFRBuilder : ReactorBuilder<Reactor_PFR, PFRBuilder>
    {
        internal PFRBuilder(Flowsheet f, Reactor_PFR o) : base(f, o) { }

        /// <summary>Sets <c>Volume</c> (SI) and returns this builder for chaining.</summary>
        public PFRBuilder WithVolume(Quantity v) { Object.Volume = v.SI; return this; }

        // ---- Property profile access (populated after Calculate) ----

        /// <summary>Composition/temperature/pressure profile along the reactor length.
        /// Each element is (Position_m, Temperature_K, Pressure_Pa, List&lt;ProfileItem&gt;).
        /// Null or empty if not yet calculated.</summary>
        public List<System.Tuple<double, double, double, List<ProfileItem>>> Profile => Object.Profile;

        /// <summary>Number of axial points in the profile (0 if not yet calculated).</summary>
        public int ProfilePointCount => Object.Profile?.Count ?? 0;
    }
}
