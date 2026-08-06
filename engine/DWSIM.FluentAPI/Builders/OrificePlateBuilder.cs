using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Orifice Plate unit operation. Call <see cref="Flowsheet.AddOrificePlate"/> to obtain one.</summary>
    public sealed class OrificePlateBuilder : UnitOpBuilder<OrificePlate, OrificePlateBuilder>
    {
        internal OrificePlateBuilder(Flowsheet f, OrificePlate o) : base(f, o) { }
    }
}
