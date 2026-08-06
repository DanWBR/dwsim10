using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Amphlett-model PEM Fuel Cell.</summary>
    public sealed class PEMFuelCellBuilder : UnitOpBuilder<PEMFC_Amphlett, PEMFuelCellBuilder>
    {
        internal PEMFuelCellBuilder(Flowsheet f, PEMFC_Amphlett o) : base(f, o) { }
    }
}
