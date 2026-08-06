using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Solids Separator unit operation. Call <see cref="Flowsheet.AddSolidsSeparator"/> to obtain one.</summary>
    public sealed class SolidsSeparatorBuilder : UnitOpBuilder<SolidsSeparator, SolidsSeparatorBuilder>
    {
        internal SolidsSeparatorBuilder(Flowsheet f, SolidsSeparator o) : base(f, o) { }
    }
}
