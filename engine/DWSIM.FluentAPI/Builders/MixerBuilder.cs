using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Mixer unit operation. Call <see cref="Flowsheet.AddMixer"/> to obtain one.</summary>
    public sealed class MixerBuilder : UnitOpBuilder<Mixer, MixerBuilder>
    {
        internal MixerBuilder(Flowsheet f, Mixer o) : base(f, o) { }

        /// <summary>Sets the outlet pressure assignment behavior.</summary>
        public MixerBuilder WithPressureBehavior(Mixer.PressureBehavior behavior)
        {
            Object.PressureCalculation = behavior;
            return this;
        }
    }
}
