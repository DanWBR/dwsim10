using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Shortcut Column unit operation. Call <see cref="Flowsheet.AddShortcutColumn"/> to obtain one.</summary>
    public sealed class ShortcutColumnBuilder : UnitOpBuilder<ShortcutColumn, ShortcutColumnBuilder>
    {
        internal ShortcutColumnBuilder(Flowsheet f, ShortcutColumn o) : base(f, o) { }
    }
}
