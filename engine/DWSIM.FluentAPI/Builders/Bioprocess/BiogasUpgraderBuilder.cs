using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Biogas Upgrader unit operation. Call <see cref="Flowsheet.AddBiogasUpgrader"/> to obtain one.</summary>
    public sealed class BiogasUpgraderBuilder : UnitOpBuilder<UnitOp_BiogasUpgrader, BiogasUpgraderBuilder>
    {
        internal BiogasUpgraderBuilder(Flowsheet f, UnitOp_BiogasUpgrader o) : base(f, o) { }

        /// <summary>Sets <c>Technology</c> and returns this builder for chaining.</summary>
        public BiogasUpgraderBuilder WithTechnology(BiogasUpgraderTech tech) { Object.Technology = tech; return this; }
        /// <summary>Sets <c>H2SRemoval</c> and returns this builder for chaining. Has no effect unless
        /// <see cref="WithH2SCompound"/> assigns the compound to strip; the upgrader logs a warning if
        /// the feed carries H2S with no compound assigned.</summary>
        public BiogasUpgraderBuilder WithH2SRemoval(double frac) { Object.H2SRemovalEfficiency = frac; return this; }
        /// <summary>Names the compound treated as H2S, enabling <see cref="WithH2SRemoval"/>, and returns
        /// this builder for chaining. Unassigned by default (feed assumed already desulfurized).</summary>
        public BiogasUpgraderBuilder WithH2SCompound(string name) { Object.H2SCompound = name; return this; }
        /// <summary>Sets <c>CO2Removal</c> and returns this builder for chaining.</summary>
        public BiogasUpgraderBuilder WithCO2Removal(double frac) { Object.CO2RemovalEfficiency = frac; return this; }
        /// <summary>Sets <c>H2ORemoval</c> and returns this builder for chaining.</summary>
        public BiogasUpgraderBuilder WithH2ORemoval(double frac) { Object.H2ORemovalEfficiency = frac; return this; }
        /// <summary>Sets <c>CH4Loss Fraction</c> and returns this builder for chaining.</summary>
        public BiogasUpgraderBuilder WithCH4LossFraction(double frac) { Object.CH4LossFraction = frac; return this; }
        /// <summary>Sets <c>Target CH4Purity</c> and returns this builder for chaining.</summary>
        public BiogasUpgraderBuilder WithTargetCH4Purity(double frac) { Object.TargetCH4Purity = frac; return this; }
    }
}
