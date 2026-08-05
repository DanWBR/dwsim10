namespace DWSIM.PureCompoundData.Core
{
    /// Coarse grouping used for querying and for the UI matrix. Each record's
    /// <see cref="PureCompoundRecord.Property"/> holds the finer-grained name.
    public enum PropertyCategory
    {
        Unknown,
        Identification,          // names, formulae, InChI/SMILES, MW
        Critical,                // Tc, Pc, Vc, Zc
        Acentric,                // omega
        NormalBoilingPoint,      // Tb
        MeltingPoint,            // Tm
        FormationEnergetics,     // HfIG, SfIG, GfIG at 298.15 K
        EnthalpyOfFusion,        // Hfus at Tm
        VaporPressure,           // Psat vs T (curve) or Antoine/Riedel coeffs
        LiquidDensity,           // rhoL vs T
        VaporDensity,
        SolidDensity,
        LiquidViscosity,         // muL vs T
        VaporViscosity,          // muV vs T
        LiquidThermalConductivity,
        VaporThermalConductivity,
        IdealGasCp,              // Cp_ig vs T (DIPPR 107 typically)
        LiquidCp,
        SolidCp,
        SurfaceTension,          // sigma vs T
        HeatOfVaporization,      // HVap vs T (DIPPR 106)
        UnifacGroups,            // group assignments (original + modified + NIST-modified)
        DipoleMoment,
        SolubilityParameter,
        UniquacParameters,       // r, q
        Other
    }
}
