using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Crossflow UF unit operation. Call <see cref="Flowsheet.AddCrossflowUF"/> to obtain one.</summary>
    public sealed class CrossflowUFBuilder : UnitOpBuilder<UnitOp_CrossflowUF, CrossflowUFBuilder>
    {
        internal CrossflowUFBuilder(Flowsheet f, UnitOp_CrossflowUF o) : base(f, o) { }

        /// <summary>Sets <c>Operating Mode</c> and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithOperatingMode(CrossflowUFMode m) { Object.OperatingMode = m; return this; }
        /// <summary>Sets <c>VCF</c> and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithVCF(double vcf) { Object.VCF = vcf; return this; }
        /// <summary>Sets <c>Diavolumes</c> and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithDiavolumes(double n) { Object.Diavolumes = n; return this; }
        /// <summary>Sets <c>Default Sieving Coefficient</c> and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithDefaultSievingCoefficient(double s) { Object.DefaultSievingCoefficient = s; return this; }
        /// <summary>Sets <c>Sieving Coefficient</c> and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithSievingCoefficient(string compound, double s)
        {
            if (Object.SievingCoefficients == null)
                Object.SievingCoefficients = new System.Collections.Generic.Dictionary<string, double>();
            Object.SievingCoefficients[compound] = s;
            return this;
        }
        /// <summary>Sets <c>Membrane Flux Kg Per M2S</c> and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithMembraneFluxKgPerM2S(double j) { Object.MembraneFlux_kgm2s = j; return this; }
        /// <summary>Sets <c>Transmembrane Pressure</c> (SI) and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithTransmembranePressure(Quantity p) { Object.TMP_Pa = p.SI; return this; }
        /// <summary>Sets <c>Fouling Half Life</c> (SI) and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithFoulingHalfLife(Quantity t) { Object.FoulingHalfLife_s = t.SI; return this; }
        /// <summary>Sets <c>Membrane Area</c> (SI) and returns this builder for chaining.</summary>
        public CrossflowUFBuilder WithMembraneArea(Quantity a) { Object.MembraneArea_m2 = a.SI; return this; }
    }
}
