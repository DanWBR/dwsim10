using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Vessel unit operation. Call <see cref="Flowsheet.AddVessel"/> to obtain one.</summary>
    public sealed class VesselBuilder : UnitOpBuilder<Vessel, VesselBuilder>
    {
        internal VesselBuilder(Flowsheet f, Vessel o) : base(f, o) { }
    }
}
