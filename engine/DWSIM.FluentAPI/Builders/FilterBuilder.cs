using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Filter unit operation. Call <see cref="Flowsheet.AddFilter"/> to obtain one.</summary>
    public sealed class FilterBuilder : UnitOpBuilder<Filter, FilterBuilder>
    {
        internal FilterBuilder(Flowsheet f, Filter o) : base(f, o) { }
    }
}
