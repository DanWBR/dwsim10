using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Biogas Upgrader unit operation. Call <see cref="Flowsheet.AddBiogasUpgrader"/> to obtain one.</summary>
    public sealed class BiogasUpgraderBuilder : UnitOpBuilder<UnitOp_BiogasUpgrader, BiogasUpgraderBuilder>
    {
        internal BiogasUpgraderBuilder(Flowsheet f, UnitOp_BiogasUpgrader o) : base(f, o) { }

        /// <summary>
        /// Sets <c>Technology</c> and loads its default CO2 removal, CH4 loss and N2 removal, as the editor does
        /// when the technology is picked. Set any of them afterwards to override the default.
        /// </summary>
        public BiogasUpgraderBuilder WithTechnology(BiogasUpgraderTech tech)
        {
            Object.Technology = tech;
            Object.ApplyTechnologyDefaults();
            return this;
        }
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
        /// <summary>Sets <c>TargetCH4Purity</c> (mole fraction) without changing <c>PurityMode</c>, and returns this builder
        /// for chaining. In Report mode the value is stored for reference only; use <see cref="WithTargetPurity"/> to make it a specification.</summary>
        public BiogasUpgraderBuilder WithTargetCH4Purity(double frac) { Object.TargetCH4Purity = frac; return this; }
        /// <summary>
        /// Makes the methane purity a specification: sets <c>PurityMode</c> to Target and <c>TargetCH4Purity</c> to
        /// <paramref name="moleFraction"/>, so the CO2 removal is solved for that methane mole fraction in the upgraded gas.
        /// Returns this builder for chaining.
        /// </summary>
        /// <param name="moleFraction">Target methane mole fraction in the upgraded gas (0-1], e.g. 0.97.</param>
        public BiogasUpgraderBuilder WithTargetPurity(double moleFraction)
        {
            Object.PurityMode = BiogasUpgraderPurityMode.Target;
            Object.TargetCH4Purity = moleFraction;
            return this;
        }
        /// <summary>Names the compound treated as N2, enabling <see cref="WithN2Removal"/>, and returns this builder for chaining.
        /// Unassigned by default (nitrogen goes entirely to the upgraded gas).</summary>
        public BiogasUpgraderBuilder WithN2Compound(string name) { Object.N2Compound = name; return this; }
        /// <summary>Sets <c>N2RemovalFraction</c>, the fraction of the N2 compound sent to the off-gas, and returns this builder
        /// for chaining. Has no effect unless <see cref="WithN2Compound"/> assigns the compound.</summary>
        public BiogasUpgraderBuilder WithN2Removal(double frac) { Object.N2RemovalFraction = frac; return this; }
    }
}
