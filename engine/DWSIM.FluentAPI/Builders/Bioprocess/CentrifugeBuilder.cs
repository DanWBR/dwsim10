using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Centrifuge unit operation. Call <see cref="Flowsheet.AddCentrifuge"/> to obtain one.</summary>
    public sealed class CentrifugeBuilder : UnitOpBuilder<UnitOp_Centrifuge, CentrifugeBuilder>
    {
        internal CentrifugeBuilder(Flowsheet f, UnitOp_Centrifuge o) : base(f, o) { }

        /// <summary>Sets <c>Technology</c> and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithTechnology(CentrifugeType t) { Object.Technology = t; return this; }
        /// <summary>Sets <c>Bowl Speed Rpm</c> and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithBowlSpeedRpm(double rpm) { Object.BowlSpeed_rpm = rpm; return this; }
        /// <summary>Sets <c>Sigma Factor M2</c> and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithSigmaFactorM2(double sigma) { Object.SigmaFactor_m2 = sigma; return this; }
        /// <summary>Sets <c>Default Recovery To Heavy</c> and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithDefaultRecoveryToHeavy(double frac) { Object.DefaultRecoveryToHeavy = frac; return this; }
        /// <summary>Sets <c>Recovery To Heavy</c> and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithRecoveryToHeavy(string compound, double frac)
        {
            if (Object.RecoveryToHeavy == null)
                Object.RecoveryToHeavy = new System.Collections.Generic.Dictionary<string, double>();
            Object.RecoveryToHeavy[compound] = frac;
            return this;
        }
    }
}
