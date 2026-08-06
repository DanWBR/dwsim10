using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Component Separator unit operation. Call <see cref="Flowsheet.AddComponentSeparator"/> to obtain one.</summary>
    public sealed class ComponentSeparatorBuilder : UnitOpBuilder<ComponentSeparator, ComponentSeparatorBuilder>
    {
        internal ComponentSeparatorBuilder(Flowsheet f, ComponentSeparator o) : base(f, o) { }
    }
}
