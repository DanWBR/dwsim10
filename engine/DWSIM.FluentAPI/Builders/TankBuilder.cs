using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Tank unit operation. Call <see cref="Flowsheet.AddTank"/> to obtain one.</summary>
    public sealed class TankBuilder : UnitOpBuilder<Tank, TankBuilder>
    {
        internal TankBuilder(Flowsheet f, Tank o) : base(f, o) { }
    }
}
