using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Reaktoro-backed Gibbs reactor (electrolyte / aqueous chemistry).</summary>
    public sealed class ReaktoroGibbsBuilder : ReactorBuilder<Reactor_ReaktoroGibbs, ReaktoroGibbsBuilder>
    {
        internal ReaktoroGibbsBuilder(Flowsheet f, Reactor_ReaktoroGibbs o) : base(f, o) { }

        /// <summary>Sets <c>Database</c> and returns this builder for chaining.</summary>
        public ReaktoroGibbsBuilder WithDatabase(string supcrtFileName) { Object.DatabaseName = supcrtFileName; return this; }
        /// <summary>Sets <c>External Database</c> and returns this builder for chaining.</summary>
        public ReaktoroGibbsBuilder WithExternalDatabase(string filePath) { Object.UseExternalDatabase = true; Object.ExternalDatabaseFileName = filePath; return this; }
        /// <summary>Sets <c>Aqueous Phase</c> and returns this builder for chaining.</summary>
        public ReaktoroGibbsBuilder WithAqueousPhase(bool enabled) { Object.AqueousPhase = enabled; return this; }
    }
}
