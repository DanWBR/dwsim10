using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Chromatography unit operation. Call <see cref="Flowsheet.AddChromatography"/> to obtain one.</summary>
    public sealed class ChromatographyBuilder : UnitOpBuilder<UnitOp_Chromatography, ChromatographyBuilder>
    {
        internal ChromatographyBuilder(Flowsheet f, UnitOp_Chromatography o) : base(f, o) { }

        /// <summary>Sets <c>Mode</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithMode(ChromatographyMode m) { Object.Mode = m; return this; }
        /// <summary>Sets <c>Chemistry</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithChemistry(ChromatographyChemistry c) { Object.Chemistry = c; return this; }
        /// <summary>Sets <c>Column Volume Liters</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithColumnVolumeLiters(double l) { Object.ColumnVolume_L = l; return this; }
        /// <summary>Sets <c>Dynamic Binding Capacity GPer L</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithDynamicBindingCapacityGPerL(double gl) { Object.DynamicBindingCapacity_gL = gl; return this; }
        /// <summary>Sets <c>Default Recovery To Product</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithDefaultRecoveryToProduct(double frac) { Object.DefaultRecoveryToProduct = frac; return this; }
        /// <summary>Sets <c>Recovery To Product</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithRecoveryToProduct(string compound, double frac)
        {
            if (Object.RecoveryToProduct == null)
                Object.RecoveryToProduct = new System.Collections.Generic.Dictionary<string, double>();
            Object.RecoveryToProduct[compound] = frac;
            return this;
        }
        /// <summary>Sets <c>Thomas Rate Constant LPer GS</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithThomasRateConstantLPerGS(double k) { Object.ThomasRateConstant_Lgs = k; return this; }
        /// <summary>Sets <c>Loading Time</c> (SI) and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithLoadingTime(Quantity t) { Object.LoadingTime_s = t.SI; return this; }
        /// <summary>Sets <c>Resin Density GPer L</c> and returns this builder for chaining.</summary>
        public ChromatographyBuilder WithResinDensityGPerL(double rho) { Object.ResinDensity_gL = rho; return this; }
    }
}
