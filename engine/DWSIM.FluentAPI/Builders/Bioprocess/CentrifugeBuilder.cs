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

        /// <summary>Sets how the recovery to the Heavy outlet is found: <c>UserFractions</c> (default) or <c>SigmaTheory</c>, and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithRecoveryModel(CentrifugeRecoveryModel model) { Object.RecoveryModel = model; return this; }
        /// <summary>Sets the bowl speed, in rpm, at which the Sigma factor was given (0 = the bowl speed), and returns this builder for chaining.
        /// With the Sigma theory the Sigma at the bowl speed is SigmaFactor (N / N_ref)^2.</summary>
        public CentrifugeBuilder WithReferenceBowlSpeedRpm(double rpm) { Object.ReferenceBowlSpeed_rpm = rpm; return this; }
        /// <summary>Sets the Sigma efficiency eta (0 to 1; 0 = the value for the technology) and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithSigmaEfficiency(double eta) { Object.SigmaEfficiency = eta; return this; }
        /// <summary>Sets the particle size distribution of a compound for the Sigma theory and returns this builder for chaining.</summary>
        /// <param name="compound">The compound name.</param>
        /// <param name="medianDiameterMicrons">The mass median diameter d50, in micrometres (0 marks the compound as not particulate).</param>
        /// <param name="geometricStdDev">The geometric standard deviation sigma_g of the log-normal distribution (1 = monodisperse); 0 keeps the default for the compound.</param>
        public CentrifugeBuilder WithParticleSize(string compound, double medianDiameterMicrons, double geometricStdDev = 0.0)
        {
            Object.ParticleMedianDiameter_um[compound] = medianDiameterMicrons;
            if (geometricStdDev > 0.0) Object.ParticleSizeSpread[compound] = geometricStdDev;
            return this;
        }
        /// <summary>Sets the particle density of a compound, in kg/m3, for the Sigma theory and returns this builder for chaining.</summary>
        public CentrifugeBuilder WithParticleDensity(string compound, double densityKgPerM3)
        {
            Object.ParticleDensity_kgm3[compound] = densityKgPerM3;
            return this;
        }
        /// <summary>Gives the disk-stack geometry, so that the Sigma theory computes Sigma from it at the bowl speed (Ambler 1952), and returns this builder for chaining.</summary>
        /// <param name="discs">The number of discs.</param>
        /// <param name="outerRadiusM">The disc outer radius, in m.</param>
        /// <param name="innerRadiusM">The disc inner radius, in m.</param>
        /// <param name="discAngleDeg">The disc half-cone angle from the axis of rotation, in degrees.</param>
        public CentrifugeBuilder WithDiscStackGeometry(int discs, double outerRadiusM, double innerRadiusM, double discAngleDeg = 40.0)
        {
            Object.NumberOfDiscs = discs;
            Object.OuterRadius_m = outerRadiusM;
            Object.InnerRadius_m = innerRadiusM;
            Object.DiscAngle_deg = discAngleDeg;
            return this;
        }
        /// <summary>Gives the geometry of a tubular bowl (or of the cylindrical section of a decanter), so that the Sigma theory computes Sigma from it
        /// at the bowl speed, and returns this builder for chaining.</summary>
        /// <param name="lengthM">The bowl length, in m.</param>
        /// <param name="outerRadiusM">The bowl wall radius, in m.</param>
        /// <param name="innerRadiusM">The liquid surface (weir) radius, in m.</param>
        public CentrifugeBuilder WithBowlGeometry(double lengthM, double outerRadiusM, double innerRadiusM)
        {
            Object.BowlLength_m = lengthM;
            Object.OuterRadius_m = outerRadiusM;
            Object.InnerRadius_m = innerRadiusM;
            return this;
        }
    }
}
