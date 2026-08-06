using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Splitter unit operation. Call <see cref="Flowsheet.AddSplitter"/> to obtain one.</summary>
    public sealed class SplitterBuilder : UnitOpBuilder<Splitter, SplitterBuilder>
    {
        internal SplitterBuilder(Flowsheet f, Splitter o) : base(f, o) { }
    }
}
