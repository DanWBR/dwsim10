using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Solids Separator unit operation. Call <see cref="Flowsheet.AddSolidsSeparator"/> to obtain one.
    /// Outlet 0 takes the fluid, outlet 1 the solids. The separator reads the solids from the feed's
    /// solid phase, so the solid compounds must flash as solids (see <see cref="PropertyPackageBuilder.WithForcedSolids"/>).
    /// </summary>
    public sealed class SolidsSeparatorBuilder : UnitOpBuilder<SolidsSeparator, SolidsSeparatorBuilder>
    {
        internal SolidsSeparatorBuilder(Flowsheet f, SolidsSeparator o) : base(f, o) { }

        /// <summary>Sets the percentage of the feed solids sent to the solids outlet (port 1).</summary>
        public SolidsSeparatorBuilder WithSolidsSeparationEfficiency(double percent) { Object.SeparationEfficiency = percent; return this; }

        /// <summary>Sets the percentage of the feed liquid sent to the fluid outlet (port 0).</summary>
        public SolidsSeparatorBuilder WithLiquidSeparationEfficiency(double percent) { Object.LiquidSeparationEfficiency = percent; return this; }
    }
}
