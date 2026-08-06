using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Crystallizer unit operation. Call <see cref="Flowsheet.AddCrystallizer"/> to obtain one.</summary>
    public sealed class CrystallizerBuilder : UnitOpBuilder<UnitOp_Crystallizer, CrystallizerBuilder>
    {
        internal CrystallizerBuilder(Flowsheet f, UnitOp_Crystallizer o) : base(f, o) { }

        /// <summary>Sets <c>Mode</c> and returns this builder for chaining.</summary>
        public CrystallizerBuilder WithMode(CrystallizerMode m) { Object.Mode = m; return this; }
        /// <summary>Sets <c>Solute Compound</c> and returns this builder for chaining.</summary>
        public CrystallizerBuilder WithSoluteCompound(string name) { Object.SoluteCompound = name; return this; }
        /// <summary>Sets <c>Solvent Compound</c> and returns this builder for chaining.</summary>
        public CrystallizerBuilder WithSolventCompound(string name) { Object.SolventCompound = name; return this; }
        /// <summary>Sets <c>Operating Temperature</c> (SI) and returns this builder for chaining.</summary>
        public CrystallizerBuilder WithOperatingTemperature(Quantity t) { Object.OperatingT_K = t.SI; return this; }

        /// <summary>Solubility C(T) [g solute / g solvent] = A + B*(T-273.15) + C*(T-273.15)^2.</summary>
        public CrystallizerBuilder WithSolubilityCoefficients(double a, double b, double c)
        { Object.Sol_A = a; Object.Sol_B = b; Object.Sol_C = c; return this; }

        /// <summary>Sets <c>Evaporation Fraction</c> and returns this builder for chaining.</summary>
        public CrystallizerBuilder WithEvaporationFraction(double frac) { Object.EvaporationFraction = frac; return this; }
        /// <summary>Sets <c>Solubility Reduction By Antisolvent</c> and returns this builder for chaining.</summary>
        public CrystallizerBuilder WithSolubilityReductionByAntisolvent(double factor) { Object.SolubilityReductionByAntisolvent = factor; return this; }
        /// <summary>Sets <c>Mean Crystal Size Microns</c> and returns this builder for chaining.</summary>
        public CrystallizerBuilder WithMeanCrystalSizeMicrons(double um) { Object.MeanCrystalSize_um = um; return this; }
    }
}
