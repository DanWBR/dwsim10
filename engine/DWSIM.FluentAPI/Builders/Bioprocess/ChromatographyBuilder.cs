using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Chromatography unit operation. Call <see cref="Flowsheet.AddChromatography"/> to obtain one.</summary>
    public sealed class ChromatographyBuilder : UnitOpBuilder<UnitOp_Chromatography, ChromatographyBuilder>
    {
        internal ChromatographyBuilder(Flowsheet f, UnitOp_Chromatography o) : base(f, o) { }

        /// <summary>Sets <c>Mode</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithMode(ChromatographyMode m) { Object.Mode = m; return this; }
        /// <summary>Sets <c>Chemistry</c>, applies its typical platform values (<see cref="UnitOp_Chromatography.ApplyChemistryDefaults"/>:
        /// dynamic binding capacity, Thomas rate constant and per-compound recoveries) and returns this builder for chaining.
        /// Call <see cref="WithTargetCompound"/> first so the target gets the chemistry yield; the With calls after this one override the defaults.</summary>
        public ChromatographyBuilder WithChemistry(ChromatographyChemistry c) => WithChemistry(c, true);
        /// <summary>Sets <c>Chemistry</c> and returns this builder for chaining. <paramref name="applyDefaults"/> = <c>false</c> keeps the current
        /// binding capacity, rate constant and recoveries; <c>true</c> applies the typical values of the chemistry.</summary>
        public ChromatographyBuilder WithChemistry(ChromatographyChemistry c, bool applyDefaults)
        {
            Object.Chemistry = c;
            if (applyDefaults) Object.ApplyChemistryDefaults();
            return this;
        }
        /// <summary>Sets <c>TargetCompound</c> (the product the column captures) and returns this builder for chaining. Empty: every compound above 5000 g/mol is a target.</summary>
        public ChromatographyBuilder WithTargetCompound(string compound) { Object.TargetCompound = compound ?? ""; return this; }
        /// <summary>Sets <c>CycleLoadTime_s</c>, the load step duration of one cycle (SI, s), and returns this builder for chaining. The load ratio is the target fed in this time over the binding capacity.</summary>
        public ChromatographyBuilder WithCycleLoadTime(Quantity t) { Object.CycleLoadTime_s = t.SI; return this; }
        /// <summary>Sets the size exclusion calibration (exclusion and total permeation limits, g/mol) that <c>ApplyChemistryDefaults</c> uses for Kav, and returns this builder for chaining.
        /// Call it before <see cref="WithChemistry(ChromatographyChemistry)"/>.</summary>
        public ChromatographyBuilder WithSizeExclusionRange(double exclusionLimitGPerMol, double permeationLimitGPerMol)
        {
            Object.SEC_ExclusionLimit_gmol = exclusionLimitGPerMol;
            Object.SEC_PermeationLimit_gmol = permeationLimitGPerMol;
            return this;
        }
        /// <summary>Sets <c>Column Volume Liters</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithColumnVolumeLiters(double l) { Object.ColumnVolume_L = l; return this; }
        /// <summary>Sets <c>Dynamic Binding Capacity GPer L</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithDynamicBindingCapacityGPerL(double gl) { Object.DynamicBindingCapacity_gL = gl; return this; }
        /// <summary>Sets <c>Default Recovery To Product</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithDefaultRecoveryToProduct(double frac) { Object.DefaultRecoveryToProduct = frac; return this; }
        /// <summary>Sets the recovery to product of one compound as a user value (<see cref="UnitOp_Chromatography.SetRecoveryToProduct"/>) and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithRecoveryToProduct(string compound, double frac)
        {
            Object.SetRecoveryToProduct(compound, frac);
            return this;
        }
        /// <summary>Sets <c>Thomas Rate Constant LPer GS</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithThomasRateConstantLPerGS(double k) { Object.ThomasRateConstant_Lgs = k; return this; }
        /// <summary>Sets <c>Loading Time</c> (SI), the horizon of the Thomas breakthrough curve in <c>BindElute_Dynamic</c> (0 = auto to 99 % saturation), and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithLoadingTime(Quantity t) { Object.LoadingTime_s = t.SI; return this; }
        /// <summary>Sets <c>Resin Density GPer L</c> and returns this builder for chaining. The calculation does not use it (the binding capacity is per litre of column); kept for older scripts.</summary>
        public ChromatographyBuilder WithResinDensityGPerL(double rho) { Object.ResinDensity_gL = rho; return this; }
    }
}
