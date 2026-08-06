using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Builder for unit operations that don't have a dedicated typed builder.
    /// Returned by <see cref="Flowsheet.AddUnitOperation"/>.
    /// </summary>
    public sealed class GenericUnitOpBuilder : UnitOpBuilder<ISimulationObject, GenericUnitOpBuilder>
    {
        internal GenericUnitOpBuilder(Flowsheet flowsheet, ISimulationObject obj)
            : base(flowsheet, obj) { }
    }
}
