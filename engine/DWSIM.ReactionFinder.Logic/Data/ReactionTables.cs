using System.Collections.Generic;
using DWSIM.Interfaces.Enums;

namespace DWSIM.Extensions.ReactionFinder.Data
{
    /// <summary>
    /// A single species in a kinetic-reaction template: its stoichiometric
    /// coefficient (signed: negative = reactant) and, independently, the order
    /// in the forward rate expression. Reaction order is NOT generally equal
    /// to |stoich|, so the two are tracked separately.
    /// </summary>
    public struct KineticSpecies
    {
        public string Identity;   // formula / CAS / name resolvable via CompoundIndex
        public double StoichCoeff;
        public double ForwardOrder;
        public double ReverseOrder;

        public KineticSpecies(string identity, double stoichCoeff,
            double forwardOrder = 0, double reverseOrder = 0)
        {
            Identity = identity;
            StoichCoeff = stoichCoeff;
            ForwardOrder = forwardOrder;
            ReverseOrder = reverseOrder;
        }
    }

    /// <summary>
    /// Kinetic reaction template. Arrhenius A and E (activation energy, J/mol) are
    /// the canonical textbook values; units of A depend on the overall reaction
    /// order and are stated in <see cref="VelUnit"/> / <see cref="ConcUnit"/>.
    /// </summary>
    public class KineticTemplate
    {
        public string Name;
        public string Description;   // includes reference citation
        public KineticSpecies[] Species;
        public double A_Forward;
        public double E_Forward;     // J/mol
        public double A_Reverse;
        public double E_Reverse;     // J/mol (0 if irreversible)
        public double Tmin;
        public double Tmax;
        public ReactionPhase Phase;
        public string ConcUnit;      // e.g. "mol/L", "mol/m3"
        public string VelUnit;       // e.g. "mol/[L.s]", "mol/[m3.s]"
    }

    /// <summary>
    /// Template for an acid-base equilibrium. Identities are formula strings that
    /// CompoundIndex resolves to actual compound names in the flowsheet.
    /// </summary>
    public class AcidBaseTemplate
    {
        public string Name;
        public string Description;
        /// <summary>
        /// Map: compound formula -&gt; stoichiometric coefficient (negative = reactant).
        /// All formulas must resolve to compounds present in the flowsheet for this
        /// template to be emitted.
        /// </summary>
        public Dictionary<string, double> Stoich;
        /// <summary>Informational pKa at 25 C. Not used in the reaction object yet.</summary>
        public double PKa;
    }

    public class PrecipitationTemplate
    {
        public string Name;
        public string Description;
        public Dictionary<string, double> Stoich;
        public double PKsp;
    }

    /// <summary>
    /// Heterogeneous catalytic template. Rate law is expressed by string
    /// formulas that DWSIM evaluates at runtime, with variables T (Kelvin),
    /// R1..Rn (reactant concentrations, ordered as in <see cref="Species"/>),
    /// and P1..Pm (product concentrations, ordered as in <see cref="Species"/>).
    /// Total rate = Numerator / Denominator in <see cref="VelUnit"/>.
    /// </summary>
    public class CatalyticTemplate
    {
        public string Name;
        public string Description;
        public KineticSpecies[] Species;   // Reactants first (order determines R1..Rn), then products (P1..Pm)
        public string RateNumerator;
        public string RateDenominator;
        public double Tmin;
        public double Tmax;
        public ReactionPhase Phase;
        public string ConcUnit;
        public string VelUnit;
    }

    public static class ReactionTables
    {
        /// <summary>
        /// Curated textbook kinetic reactions with Arrhenius parameters.
        /// Conventions:
        ///   - ConcUnit = "mol/L", VelUnit = "mol/[L.s]" throughout; A values are
        ///     converted to (mol/L)^(1-n)/s where n is the overall forward order.
        ///   - E in J/mol.
        ///   - Reverse kinetics only populated where a reliable textbook value exists.
        /// References are cited in the Description.
        /// </summary>
        public static readonly KineticTemplate[] Kinetic = new[]
        {
            // 1. N2O5 decomposition (first-order, gas) -- Daniels & Johnston (1921)
            new KineticTemplate
            {
                Name = "N2O5 decomposition",
                Description = "2 N2O5 -> 4 NO2 + O2. First-order. A=4.3e13 1/s, E=103.4 kJ/mol. " +
                              "Ref: Daniels & Johnston, J. Am. Chem. Soc. 43 (1921) 53.",
                Species = new[]
                {
                    new KineticSpecies("N2O5", -2, 1),
                    new KineticSpecies("NO2", +4),
                    new KineticSpecies("O2",  +1),
                },
                A_Forward = 4.3e13, E_Forward = 103400,
                Tmin = 273, Tmax = 400, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 2. HI decomposition (second-order, gas) -- Bodenstein (1897)
            new KineticTemplate
            {
                Name = "HI decomposition",
                Description = "2 HI -> H2 + I2. Second-order in HI. A=9.2e10 L/(mol.s), E=186 kJ/mol. " +
                              "Ref: Bodenstein, Z. Phys. Chem. 29 (1899) 295.",
                Species = new[]
                {
                    new KineticSpecies("HI", -2, 2),
                    new KineticSpecies("H2", +1),
                    new KineticSpecies("I2", +1),
                },
                A_Forward = 9.2e10, E_Forward = 186000,
                Tmin = 550, Tmax = 800, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 3. H2 + I2 -> 2 HI (second-order, gas) -- Kistiakowsky (1928)
            new KineticTemplate
            {
                Name = "H2 + I2 reaction",
                Description = "H2 + I2 -> 2 HI. Second-order. A=1.6e11 L/(mol.s), E=165.6 kJ/mol. " +
                              "Ref: Kistiakowsky, J. Am. Chem. Soc. 50 (1928) 2315.",
                Species = new[]
                {
                    new KineticSpecies("H2", -1, 1),
                    new KineticSpecies("I2", -1, 1),
                    new KineticSpecies("HI", +2),
                },
                A_Forward = 1.6e11, E_Forward = 165600,
                Tmin = 550, Tmax = 800, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 4. NO2 decomposition (second-order, gas) -- Bodenstein
            new KineticTemplate
            {
                Name = "NO2 decomposition",
                Description = "2 NO2 -> 2 NO + O2. Second-order in NO2. A=2.0e9 L/(mol.s), E=113.9 kJ/mol. " +
                              "Ref: Ashmore, Phys. Chem. Gases, 1963.",
                Species = new[]
                {
                    new KineticSpecies("NO2", -2, 2),
                    new KineticSpecies("NO",  +2),
                    new KineticSpecies("O2",  +1),
                },
                A_Forward = 2.0e9, E_Forward = 113900,
                Tmin = 550, Tmax = 800, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 5. Ethane thermal cracking (first-order, gas) -- Froment & Bischoff
            new KineticTemplate
            {
                Name = "Ethane thermal cracking",
                Description = "C2H6 -> C2H4 + H2. First-order. A=4.65e13 1/s, E=273 kJ/mol. " +
                              "Ref: Froment & Bischoff, Chemical Reactor Analysis and Design, 1990.",
                Species = new[]
                {
                    new KineticSpecies("C2H6", -1, 1),
                    new KineticSpecies("C2H4", +1),
                    new KineticSpecies("H2",   +1),
                },
                A_Forward = 4.65e13, E_Forward = 273000,
                Tmin = 900, Tmax = 1200, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 6. Propane thermal cracking (first-order, gas) -- simplified
            new KineticTemplate
            {
                Name = "Propane thermal cracking",
                Description = "C3H8 -> C3H6 + H2. First-order. A=4.6e13 1/s, E=211 kJ/mol. " +
                              "Ref: Sundaram & Froment, Chem. Eng. Sci. 32 (1977) 601 (simplified).",
                Species = new[]
                {
                    new KineticSpecies("C3H8", -1, 1),
                    new KineticSpecies("C3H6", +1),
                    new KineticSpecies("H2",   +1),
                },
                A_Forward = 4.6e13, E_Forward = 211000,
                Tmin = 900, Tmax = 1200, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 7. Acetaldehyde decomposition (second-order, gas) -- Hinshelwood & Hutchison
            new KineticTemplate
            {
                Name = "Acetaldehyde decomposition",
                Description = "CH3CHO -> CH4 + CO. Order 3/2 (approximated as 2). " +
                              "A=1.5e8 L/(mol.s), E=190.4 kJ/mol. " +
                              "Ref: Hinshelwood & Hutchison, Proc. Roy. Soc. A 111 (1926) 380.",
                Species = new[]
                {
                    new KineticSpecies("C2H4O", -1, 2),   // acetaldehyde
                    new KineticSpecies("CH4",   +1),
                    new KineticSpecies("CO",    +1),
                },
                A_Forward = 1.5e8, E_Forward = 190400,
                Tmin = 700, Tmax = 1000, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 8. Ethyl acetate saponification (liquid, 2nd order) -- Levenspiel
            new KineticTemplate
            {
                Name = "Ethyl acetate saponification (NaOH)",
                Description = "CH3COOC2H5 + NaOH -> CH3COONa + C2H5OH. Second-order. " +
                              "A=3.78e7 L/(mol.s), E=48.3 kJ/mol. " +
                              "Ref: Levenspiel, Chemical Reaction Engineering, 3rd ed., p.76.",
                Species = new[]
                {
                    new KineticSpecies("C4H8O2", -1, 1),   // ethyl acetate
                    new KineticSpecies("NaOH",   -1, 1),
                    new KineticSpecies("C2H3NaO2", +1),    // sodium acetate
                    new KineticSpecies("C2H6O",  +1),      // ethanol
                },
                A_Forward = 3.78e7, E_Forward = 48325,
                Tmin = 273, Tmax = 373, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 9. Acetic anhydride hydrolysis (liquid, pseudo-first-order in excess water)
            new KineticTemplate
            {
                Name = "Acetic anhydride hydrolysis",
                Description = "(CH3CO)2O + H2O -> 2 CH3COOH. Pseudo-first-order (excess water). " +
                              "A=2.14e7 1/s, E=44.35 kJ/mol. " +
                              "Ref: Shatyski & Hanesian, Ind. Eng. Chem. Res. 32 (1993) 594.",
                Species = new[]
                {
                    new KineticSpecies("C4H6O3", -1, 1),   // acetic anhydride
                    new KineticSpecies("H2O",    -1, 0),
                    new KineticSpecies("C2H4O2", +2),      // acetic acid
                },
                A_Forward = 2.14e7, E_Forward = 44350,
                Tmin = 273, Tmax = 373, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 10. Ethyl acetate esterification (Fischer, H2SO4 catalysed, liquid)
            new KineticTemplate
            {
                Name = "Ethyl acetate esterification",
                Description = "C2H5OH + CH3COOH <-> CH3COOC2H5 + H2O. Reversible, second-order each way. " +
                              "Af=1.69e9 L/(mol.s), Ef=68.4 kJ/mol; Ar=9.2e7 L/(mol.s), Er=68.0 kJ/mol. " +
                              "Ref: Smith, Chemical Engineering Kinetics, 3rd ed., p.107.",
                Species = new[]
                {
                    new KineticSpecies("C2H6O",  -1, 1, 0),   // ethanol
                    new KineticSpecies("C2H4O2", -1, 1, 0),   // acetic acid
                    new KineticSpecies("C4H8O2", +1, 0, 1),   // ethyl acetate
                    new KineticSpecies("H2O",    +1, 0, 1),
                },
                A_Forward = 1.69e9, E_Forward = 68400,
                A_Reverse = 9.2e7,  E_Reverse = 68000,
                Tmin = 273, Tmax = 373, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 11. Methyl acetate esterification (Fischer, liquid)
            new KineticTemplate
            {
                Name = "Methyl acetate esterification",
                Description = "CH3OH + CH3COOH <-> CH3COOCH3 + H2O. Reversible, second-order each way. " +
                              "Af=1.36e3 L/(mol.s), Ef=52 kJ/mol (representative). " +
                              "Ref: Popken et al., Ind. Eng. Chem. Res. 39 (2000) 2601.",
                Species = new[]
                {
                    new KineticSpecies("CH4O",   -1, 1, 0),   // methanol
                    new KineticSpecies("C2H4O2", -1, 1, 0),   // acetic acid
                    new KineticSpecies("C3H6O2", +1, 0, 1),   // methyl acetate
                    new KineticSpecies("H2O",    +1, 0, 1),
                },
                A_Forward = 1.36e3, E_Forward = 52000,
                A_Reverse = 7.9e2,  E_Reverse = 52000,
                Tmin = 273, Tmax = 373, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 12. Ozone decomposition (gas, second-order simplified)
            new KineticTemplate
            {
                Name = "Ozone decomposition",
                Description = "2 O3 -> 3 O2. Simplified second-order. A=4.61e11 L/(mol.s), E=107.3 kJ/mol. " +
                              "Ref: Benson & Axworthy, J. Chem. Phys. 26 (1957) 1718 (simplified).",
                Species = new[]
                {
                    new KineticSpecies("O3", -2, 2),
                    new KineticSpecies("O2", +3),
                },
                A_Forward = 4.61e11, E_Forward = 107300,
                Tmin = 300, Tmax = 800, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 13. Cyclopentadiene Diels-Alder dimerization (liquid, 2nd order)
            new KineticTemplate
            {
                Name = "Cyclopentadiene dimerization",
                Description = "2 C5H6 -> C10H12 (dicyclopentadiene). Second-order. " +
                              "A=1.3e6 L/(mol.s), E=69.9 kJ/mol. " +
                              "Ref: Wassermann, Trans. Faraday Soc. 34 (1938) 128.",
                Species = new[]
                {
                    new KineticSpecies("Cyclopentadiene", -2, 2),
                    new KineticSpecies("Dicyclopentadiene", +1),
                },
                A_Forward = 1.3e6, E_Forward = 69900,
                Tmin = 273, Tmax = 450, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 14. 1,3-Butadiene dimerization (gas, 2nd order)
            new KineticTemplate
            {
                Name = "1,3-Butadiene dimerization",
                Description = "2 C4H6 -> C8H12 (4-vinylcyclohexene). Second-order. " +
                              "A=9.2e9 L/(mol.s), E=100 kJ/mol. " +
                              "Ref: Vaughan, J. Am. Chem. Soc. 54 (1932) 3863.",
                Species = new[]
                {
                    new KineticSpecies("1,3-Butadiene", -2, 2),
                    new KineticSpecies("4-Vinyl-1-cyclohexene", +1),
                },
                A_Forward = 9.2e9, E_Forward = 100000,
                Tmin = 500, Tmax = 900, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 15. Butadiene + Ethylene Diels-Alder -> cyclohexene (gas, 2nd order)
            new KineticTemplate
            {
                Name = "Butadiene + Ethylene Diels-Alder",
                Description = "C4H6 + C2H4 -> C6H10 (cyclohexene). Second-order. " +
                              "A=1.03e7 L/(mol.s), E=115 kJ/mol. " +
                              "Ref: Rowley & Steiner, Discuss. Faraday Soc. 10 (1951) 198.",
                Species = new[]
                {
                    new KineticSpecies("1,3-Butadiene", -1, 1),
                    new KineticSpecies("Ethylene",      -1, 1),
                    new KineticSpecies("Cyclohexene",   +1),
                },
                A_Forward = 1.03e7, E_Forward = 115000,
                Tmin = 500, Tmax = 900, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 16. Cyclobutane thermal decomposition (gas, 1st order)
            new KineticTemplate
            {
                Name = "Cyclobutane decomposition",
                Description = "C4H8 -> 2 C2H4 (cyclobutane to ethylene). First-order. " +
                              "A=4.0e15 1/s, E=261 kJ/mol. " +
                              "Ref: Genaux, Kern & Walters, J. Am. Chem. Soc. 75 (1953) 6196.",
                Species = new[]
                {
                    new KineticSpecies("Cyclobutane", -1, 1),
                    new KineticSpecies("Ethylene",    +2),
                },
                A_Forward = 4.0e15, E_Forward = 261000,
                Tmin = 700, Tmax = 1100, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 17. Acetone pyrolysis (gas, 1st order)
            new KineticTemplate
            {
                Name = "Acetone pyrolysis",
                Description = "(CH3)2CO -> CH2CO + CH4 (acetone -> ketene + methane). First-order. " +
                              "A=8.7e15 1/s, E=286.6 kJ/mol. " +
                              "Ref: Rice & Herzfeld, J. Am. Chem. Soc. 56 (1934) 284.",
                Species = new[]
                {
                    new KineticSpecies("Acetone", -1, 1),
                    new KineticSpecies("Ketene",  +1),
                    new KineticSpecies("CH4",     +1),
                },
                A_Forward = 8.7e15, E_Forward = 286600,
                Tmin = 800, Tmax = 1200, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 18. Formic acid thermal decomposition (gas, 1st order, dehydration channel)
            new KineticTemplate
            {
                Name = "Formic acid decomposition",
                Description = "HCOOH -> CO + H2O (dehydration channel). First-order. " +
                              "A=2.1e13 1/s, E=289 kJ/mol. " +
                              "Ref: Saito et al., Int. J. Chem. Kinet. 16 (1984) 1017.",
                Species = new[]
                {
                    new KineticSpecies("CH2O2", -1, 1),   // formic acid
                    new KineticSpecies("CO",    +1),
                    new KineticSpecies("H2O",   +1),
                },
                A_Forward = 2.1e13, E_Forward = 289000,
                Tmin = 700, Tmax = 1200, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 19. Urea hydrolysis (aqueous, pseudo 1st order)
            new KineticTemplate
            {
                Name = "Urea hydrolysis",
                Description = "CO(NH2)2 + H2O -> 2 NH3 + CO2. Pseudo-first-order in urea (excess water). " +
                              "A=4.04e7 1/s, E=86.8 kJ/mol. " +
                              "Ref: Sahu et al., Ind. Eng. Chem. Res. 41 (2002) 4940.",
                Species = new[]
                {
                    new KineticSpecies("Urea", -1, 1),
                    new KineticSpecies("H2O",  -1, 0),
                    new KineticSpecies("NH3",  +2),
                    new KineticSpecies("CO2",  +1),
                },
                A_Forward = 4.04e7, E_Forward = 86800,
                Tmin = 333, Tmax = 473, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 20. Hydrogen peroxide thermal decomposition (aqueous, 1st order)
            new KineticTemplate
            {
                Name = "H2O2 thermal decomposition",
                Description = "2 H2O2 -> 2 H2O + O2. Pseudo-first-order (uncatalysed aqueous). " +
                              "A=7.94e10 1/s, E=75.3 kJ/mol. " +
                              "Ref: Shtamm, Purmal & Skurlatov, Int. J. Chem. Kinet. 11 (1979) 461.",
                Species = new[]
                {
                    new KineticSpecies("H2O2", -2, 1),
                    new KineticSpecies("H2O",  +2),
                    new KineticSpecies("O2",   +1),
                },
                A_Forward = 7.94e10, E_Forward = 75300,
                Tmin = 273, Tmax = 373, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 21. NO2 + CO -> NO + CO2 (gas, 2nd order)
            new KineticTemplate
            {
                Name = "NO2 + CO reaction",
                Description = "NO2 + CO -> NO + CO2. Second-order. A=1.2e10 L/(mol.s), E=132 kJ/mol. " +
                              "Ref: Johnston, J. Am. Chem. Soc. 78 (1956) 4542.",
                Species = new[]
                {
                    new KineticSpecies("NO2", -1, 1),
                    new KineticSpecies("CO",  -1, 1),
                    new KineticSpecies("NO",  +1),
                    new KineticSpecies("CO2", +1),
                },
                A_Forward = 1.2e10, E_Forward = 132000,
                Tmin = 500, Tmax = 900, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 22. Diethyl ether thermal decomposition (gas, 1st order)
            new KineticTemplate
            {
                Name = "Diethyl ether decomposition",
                Description = "(C2H5)2O -> CH3CHO + C2H6. First-order. A=2.5e11 1/s, E=223 kJ/mol. " +
                              "Ref: Hinshelwood & Askey, Proc. Roy. Soc. A 115 (1927) 215.",
                Species = new[]
                {
                    new KineticSpecies("Diethyl ether", -1, 1),
                    new KineticSpecies("C2H4O",         +1),  // acetaldehyde
                    new KineticSpecies("C2H6",          +1),
                },
                A_Forward = 2.5e11, E_Forward = 223000,
                Tmin = 700, Tmax = 1000, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 23. Methyl acetate hydrolysis (H+ catalysed, pseudo 2nd order)
            new KineticTemplate
            {
                Name = "Methyl acetate hydrolysis",
                Description = "CH3COOCH3 + H2O -> CH3COOH + CH3OH (acid catalysed). Second-order. " +
                              "A=3.0e7 L/(mol.s), E=48.0 kJ/mol. " +
                              "Ref: Laidler, Chemical Kinetics, 3rd ed. (representative).",
                Species = new[]
                {
                    new KineticSpecies("C3H6O2", -1, 1),   // methyl acetate
                    new KineticSpecies("H2O",    -1, 1),
                    new KineticSpecies("C2H4O2", +1),      // acetic acid
                    new KineticSpecies("CH4O",   +1),      // methanol
                },
                A_Forward = 3.0e7, E_Forward = 48000,
                Tmin = 273, Tmax = 373, Phase = ReactionPhase.Liquid,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 24. Trioxane thermal decomposition (gas, 1st order)
            new KineticTemplate
            {
                Name = "1,3,5-Trioxane decomposition",
                Description = "(CH2O)3 -> 3 CH2O (trioxane -> formaldehyde). First-order. " +
                              "A=4.0e13 1/s, E=190 kJ/mol. " +
                              "Ref: Burnett & Bell, Trans. Faraday Soc. 34 (1938) 420.",
                Species = new[]
                {
                    new KineticSpecies("1,3,5-Trioxane", -1, 1),
                    new KineticSpecies("Formaldehyde",   +3),
                },
                A_Forward = 4.0e13, E_Forward = 190000,
                Tmin = 500, Tmax = 800, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
        };

        /// <summary>
        /// Heterogeneous catalytic (LHHW / power-law) reactions. Rate laws use
        /// DWSIM's variable convention: R1..Rn for reactant concentrations (in
        /// order of appearance in Species[]), P1..Pm for products, T in Kelvin.
        /// Arrhenius factor Exp(-Ea/(R*T)) is embedded in the numerator string.
        /// All entries use ConcUnit = "mol/L" and VelUnit = "mol/[L.s]".
        /// Only emitted when every species resolves to a flowsheet compound.
        /// </summary>
        public static readonly CatalyticTemplate[] Catalytic = new[]
        {
            // 1. Water-Gas Shift (Fe-Cr, HT shift) - simplified power law, Moe (1962)
            new CatalyticTemplate
            {
                Name = "Water-Gas Shift (HT, Fe-Cr)",
                Description = "CO + H2O <-> CO2 + H2 over Fe-Cr. Power-law with reverse term: " +
                              "r = k_f*R1*R2 - k_r*P1*P2. Ref: Moe, Chem. Eng. Prog. 58 (1962) 33.",
                Species = new[]
                {
                    new KineticSpecies("CO",  -1),
                    new KineticSpecies("H2O", -1),
                    new KineticSpecies("CO2", +1),
                    new KineticSpecies("H2",  +1),
                },
                RateNumerator = "1.02e2 * Exp(-47400/(8.314*T)) * R1 * R2 - 1.25e2 * Exp(-67100/(8.314*T)) * P1 * P2",
                RateDenominator = "1",
                Tmin = 573, Tmax = 773, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 2. SO2 oxidation to SO3 over V2O5 (simplified)
            new CatalyticTemplate
            {
                Name = "SO2 oxidation (V2O5)",
                Description = "2 SO2 + O2 -> 2 SO3 over V2O5. Simplified rate r = k * C_SO2 * C_O2^0.5. " +
                              "Ref: Eklund, Doctoral Thesis, Stockholm, 1956 (simplified form).",
                Species = new[]
                {
                    new KineticSpecies("SO2", -2),
                    new KineticSpecies("O2",  -1),
                    new KineticSpecies("SO3", +2),
                },
                RateNumerator = "8.3e6 * Exp(-66000/(8.314*T)) * R1 * R2^0.5",
                RateDenominator = "1",
                Tmin = 673, Tmax = 923, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 3. Ethylbenzene dehydrogenation to styrene (Fogler Example 8-10, simplified)
            new CatalyticTemplate
            {
                Name = "Ethylbenzene dehydrogenation",
                Description = "C8H10 <-> C8H8 + H2 over Fe2O3/K2O. Reversible power-law: " +
                              "r = k*(C_EB - C_ST*C_H2/Keq). Ref: Sheel & Crowe, Can. J. Chem. Eng. 47 (1969) 183.",
                Species = new[]
                {
                    new KineticSpecies("Ethylbenzene", -1),
                    new KineticSpecies("Styrene",      +1),
                    new KineticSpecies("H2",           +1),
                },
                RateNumerator = "3.46e4 * Exp(-90800/(8.314*T)) * (R1 - P1*P2/(1.1e5*Exp(-120000/(8.314*T))))",
                RateDenominator = "1",
                Tmin = 773, Tmax = 923, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 4. Methanol to DME (gamma-Al2O3) - Bercic & Levec simplified LHHW
            new CatalyticTemplate
            {
                Name = "Methanol dehydration to DME",
                Description = "2 CH3OH -> CH3OCH3 + H2O over gamma-Al2O3. LHHW with water inhibition. " +
                              "Ref: Bercic & Levec, Ind. Eng. Chem. Res. 31 (1992) 1035 (simplified form).",
                Species = new[]
                {
                    new KineticSpecies("CH4O",      -2),  // methanol
                    new KineticSpecies("Dimethyl ether", +1),
                    new KineticSpecies("H2O",       +1),
                },
                RateNumerator = "1.21e6 * Exp(-80500/(8.314*T)) * R1^2",
                RateDenominator = "(1 + 5.4 * P2)^2",
                Tmin = 473, Tmax = 673, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 5. Toluene hydrodealkylation (Fogler Ex. 10-3)
            new CatalyticTemplate
            {
                Name = "Toluene hydrodealkylation",
                Description = "C7H8 + H2 -> C6H6 + CH4 over Cr2O3/Al2O3. Power law r = k*C_T*C_H2^0.5. " +
                              "Ref: Fogler, Elements of Chem. React. Eng., 4th ed., Ex. 10-3.",
                Species = new[]
                {
                    new KineticSpecies("Toluene", -1),
                    new KineticSpecies("H2",      -1),
                    new KineticSpecies("Benzene", +1),
                    new KineticSpecies("CH4",     +1),
                },
                RateNumerator = "8.7e9 * Exp(-217600/(8.314*T)) * R1 * R2^0.5",
                RateDenominator = "1",
                Tmin = 773, Tmax = 973, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 6. Cumene synthesis (Fogler P4-23, simplified)
            new CatalyticTemplate
            {
                Name = "Cumene synthesis",
                Description = "C6H6 + C3H6 -> C9H12 over solid acid. LHHW with cumene inhibition. " +
                              "r = k*C_B*C_P / (1 + K_C*C_C). Ref: Fogler P4-23 / Corma et al.",
                Species = new[]
                {
                    new KineticSpecies("Benzene",  -1),
                    new KineticSpecies("Propylene", -1),
                    new KineticSpecies("Cumene",   +1),
                },
                RateNumerator = "1.3e3 * Exp(-37000/(8.314*T)) * R1 * R2",
                RateDenominator = "1 + 0.5 * P1",
                Tmin = 523, Tmax = 723, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 7. CO methanation (Ni catalyst) - Vannice 1975 (simplified)
            new CatalyticTemplate
            {
                Name = "CO methanation (Ni)",
                Description = "CO + 3 H2 -> CH4 + H2O over Ni. Simplified LHHW with CO inhibition. " +
                              "Ref: Vannice, J. Catal. 37 (1975) 449.",
                Species = new[]
                {
                    new KineticSpecies("CO",  -1),
                    new KineticSpecies("H2",  -3),
                    new KineticSpecies("CH4", +1),
                    new KineticSpecies("H2O", +1),
                },
                RateNumerator = "2.7e3 * Exp(-103000/(8.314*T)) * R1 * R2",
                RateDenominator = "(1 + 0.7 * R1)^2",
                Tmin = 473, Tmax = 723, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 8. Ammonia synthesis (Temkin-Pyzhev, Fe-based, simplified forward branch)
            new CatalyticTemplate
            {
                Name = "Ammonia synthesis (Temkin-Pyzhev)",
                Description = "N2 + 3 H2 -> 2 NH3 over Fe. Temkin-Pyzhev simplified forward branch. " +
                              "Ref: Temkin & Pyzhev, Acta Physicochim. URSS 12 (1940) 327.",
                Species = new[]
                {
                    new KineticSpecies("N2",  -1),
                    new KineticSpecies("H2",  -3),
                    new KineticSpecies("NH3", +2),
                },
                RateNumerator = "1.79e4 * Exp(-87000/(8.314*T)) * R1 * (R2^1.5 / (P1 + 1e-20))",
                RateDenominator = "1",
                Tmin = 623, Tmax = 823, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 9. Methanol synthesis from CO + H2 (Cu/ZnO/Al2O3, simplified Graaf)
            new CatalyticTemplate
            {
                Name = "Methanol synthesis (Cu/ZnO)",
                Description = "CO + 2 H2 <-> CH3OH over Cu/ZnO/Al2O3. Simplified reversible power law. " +
                              "Ref: Graaf et al., Chem. Eng. Sci. 43 (1988) 3185 (simplified).",
                Species = new[]
                {
                    new KineticSpecies("CO",   -1),
                    new KineticSpecies("H2",   -2),
                    new KineticSpecies("CH4O", +1),   // methanol
                },
                RateNumerator = "4.06e-6 * Exp(-11000/(8.314*T)) * R1 * R2^2 - 1.55e10 * Exp(-124000/(8.314*T)) * P1",
                RateDenominator = "1",
                Tmin = 473, Tmax = 573, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 10. Methane steam reforming (Ni/Al2O3) - simplified Xu & Froment rxn I
            new CatalyticTemplate
            {
                Name = "Methane steam reforming",
                Description = "CH4 + H2O <-> CO + 3 H2 over Ni. Simplified power-law form of Xu-Froment rxn I. " +
                              "Ref: Xu & Froment, AIChE J. 35 (1989) 88.",
                Species = new[]
                {
                    new KineticSpecies("CH4", -1),
                    new KineticSpecies("H2O", -1),
                    new KineticSpecies("CO",  +1),
                    new KineticSpecies("H2",  +3),
                },
                RateNumerator = "4.225e15 * Exp(-240100/(8.314*T)) * R1 * R2 / (R2^2.5 + 1e-12)",
                RateDenominator = "(1 + 0.4 * R1 + 0.08 * R2 + 0.6 * P1 * P2^0.5)^2",
                Tmin = 773, Tmax = 1173, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 11. Propylene partial oxidation to acrolein (Bi-Mo oxide)
            new CatalyticTemplate
            {
                Name = "Propylene oxidation to acrolein",
                Description = "C3H6 + O2 -> C3H4O + H2O over Bi-Mo oxide. Power law r = k*C_P*C_O2^0.5. " +
                              "Ref: Callahan et al., Ind. Eng. Chem. Prod. Res. Dev. 9 (1970) 134.",
                Species = new[]
                {
                    new KineticSpecies("Propylene", -1),
                    new KineticSpecies("O2",        -1),
                    new KineticSpecies("Acrolein",  +1),
                    new KineticSpecies("H2O",       +1),
                },
                RateNumerator = "1.1e5 * Exp(-92000/(8.314*T)) * R1 * R2^0.5",
                RateDenominator = "1",
                Tmin = 573, Tmax = 773, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
            // 12. Ethylene hydrogenation over Pt (liquid/gas, simplified LHHW)
            new CatalyticTemplate
            {
                Name = "Ethylene hydrogenation (Pt)",
                Description = "C2H4 + H2 -> C2H6 over Pt. LHHW with H2 adsorption. " +
                              "Ref: Horiuti & Polanyi / Bond, Catalysis by Metals, 1962 (simplified).",
                Species = new[]
                {
                    new KineticSpecies("Ethylene", -1),
                    new KineticSpecies("H2",       -1),
                    new KineticSpecies("C2H6",     +1),
                },
                RateNumerator = "2.4e4 * Exp(-45000/(8.314*T)) * R1 * R2",
                RateDenominator = "(1 + 3.4 * R2)^2",
                Tmin = 273, Tmax = 523, Phase = ReactionPhase.Vapor,
                ConcUnit = "mol/L", VelUnit = "mol/[L.s]"
            },
        };

        /// <summary>
        /// Common aqueous acid-base equilibria. Each entry is only emitted if every
        /// referenced species exists in the flowsheet. Formulas match DWSIM's
        /// compound database conventions where possible; the CompoundIndex also
        /// tries CAS/name fallback.
        /// </summary>
        public static readonly AcidBaseTemplate[] AcidBase = new[]
        {
            // Water autoionization
            new AcidBaseTemplate
            {
                Name = "Water autoionization",
                Description = "H2O <-> H+ + OH-",
                PKa = 14.0,
                Stoich = new Dictionary<string, double>
                {
                    { "H2O", -1 }, { "H+", +1 }, { "OH-", +1 }
                }
            },
            // Carbonic acid first dissociation
            new AcidBaseTemplate
            {
                Name = "CO2 ionization (1st)",
                Description = "CO2 + H2O <-> H+ + HCO3-",
                PKa = 6.35,
                Stoich = new Dictionary<string, double>
                {
                    { "CO2", -1 }, { "H2O", -1 }, { "H+", +1 }, { "HCO3-", +1 }
                }
            },
            // Bicarbonate second dissociation
            new AcidBaseTemplate
            {
                Name = "Bicarbonate dissociation (2nd)",
                Description = "HCO3- <-> H+ + CO3-2",
                PKa = 10.33,
                Stoich = new Dictionary<string, double>
                {
                    { "HCO3-", -1 }, { "H+", +1 }, { "CO3-2", +1 }
                }
            },
            // Ammonia / ammonium
            new AcidBaseTemplate
            {
                Name = "Ammonia protonation",
                Description = "NH3 + H2O <-> NH4+ + OH-",
                PKa = 9.25,
                Stoich = new Dictionary<string, double>
                {
                    { "NH3", -1 }, { "H2O", -1 }, { "NH4+", +1 }, { "OH-", +1 }
                }
            },
            // H2S dissociations
            new AcidBaseTemplate
            {
                Name = "H2S ionization (1st)",
                Description = "H2S <-> H+ + HS-",
                PKa = 7.05,
                Stoich = new Dictionary<string, double>
                {
                    { "H2S", -1 }, { "H+", +1 }, { "HS-", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "Bisulfide dissociation (2nd)",
                Description = "HS- <-> H+ + S-2",
                PKa = 19.0,
                Stoich = new Dictionary<string, double>
                {
                    { "HS-", -1 }, { "H+", +1 }, { "S-2", +1 }
                }
            },
            // Strong acid (full dissociation, represented as equilibrium)
            new AcidBaseTemplate
            {
                Name = "HCl dissociation",
                Description = "HCl <-> H+ + Cl-",
                PKa = -6.3,
                Stoich = new Dictionary<string, double>
                {
                    { "HCl", -1 }, { "H+", +1 }, { "Cl-", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "HNO3 dissociation",
                Description = "HNO3 <-> H+ + NO3-",
                PKa = -1.3,
                Stoich = new Dictionary<string, double>
                {
                    { "HNO3", -1 }, { "H+", +1 }, { "NO3-", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "H2SO4 dissociation (1st)",
                Description = "H2SO4 <-> H+ + HSO4-",
                PKa = -3.0,
                Stoich = new Dictionary<string, double>
                {
                    { "H2SO4", -1 }, { "H+", +1 }, { "HSO4-", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "Bisulfate dissociation (2nd)",
                Description = "HSO4- <-> H+ + SO4-2",
                PKa = 1.99,
                Stoich = new Dictionary<string, double>
                {
                    { "HSO4-", -1 }, { "H+", +1 }, { "SO4-2", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "Acetic acid dissociation",
                Description = "CH3COOH <-> H+ + CH3COO-",
                PKa = 4.76,
                Stoich = new Dictionary<string, double>
                {
                    { "C2H4O2", -1 }, { "H+", +1 }, { "C2H3O2-", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "H3PO4 dissociation (1st)",
                Description = "H3PO4 <-> H+ + H2PO4-",
                PKa = 2.15,
                Stoich = new Dictionary<string, double>
                {
                    { "H3PO4", -1 }, { "H+", +1 }, { "H2PO4-", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "H3PO4 dissociation (2nd)",
                Description = "H2PO4- <-> H+ + HPO4-2",
                PKa = 7.20,
                Stoich = new Dictionary<string, double>
                {
                    { "H2PO4-", -1 }, { "H+", +1 }, { "HPO4-2", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "H3PO4 dissociation (3rd)",
                Description = "HPO4-2 <-> H+ + PO4-3",
                PKa = 12.35,
                Stoich = new Dictionary<string, double>
                {
                    { "HPO4-2", -1 }, { "H+", +1 }, { "PO4-3", +1 }
                }
            },
            new AcidBaseTemplate
            {
                Name = "HF dissociation",
                Description = "HF <-> H+ + F-",
                PKa = 3.17,
                Stoich = new Dictionary<string, double>
                {
                    { "HF", -1 }, { "H+", +1 }, { "F-", +1 }
                }
            }
        };

        /// <summary>
        /// Common salt precipitation/dissolution equilibria. Coefficient sign
        /// follows "solid on reactant side, ions on product side":
        /// CaCO3(s) &lt;-&gt; Ca+2 + CO3-2
        /// </summary>
        public static readonly PrecipitationTemplate[] Precipitation = new[]
        {
            new PrecipitationTemplate
            {
                Name = "CaCO3 solubility",
                Description = "CaCO3 <-> Ca+2 + CO3-2",
                PKsp = 8.48,
                Stoich = new Dictionary<string, double>
                {
                    { "CaCO3", -1 }, { "Ca+2", +1 }, { "CO3-2", +1 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "CaSO4 solubility",
                Description = "CaSO4 <-> Ca+2 + SO4-2",
                PKsp = 4.31,
                Stoich = new Dictionary<string, double>
                {
                    { "CaSO4", -1 }, { "Ca+2", +1 }, { "SO4-2", +1 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "BaSO4 solubility",
                Description = "BaSO4 <-> Ba+2 + SO4-2",
                PKsp = 9.96,
                Stoich = new Dictionary<string, double>
                {
                    { "BaSO4", -1 }, { "Ba+2", +1 }, { "SO4-2", +1 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "SrSO4 solubility",
                Description = "SrSO4 <-> Sr+2 + SO4-2",
                PKsp = 6.50,
                Stoich = new Dictionary<string, double>
                {
                    { "SrSO4", -1 }, { "Sr+2", +1 }, { "SO4-2", +1 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "Mg(OH)2 solubility",
                Description = "Mg(OH)2 <-> Mg+2 + 2 OH-",
                PKsp = 10.74,
                Stoich = new Dictionary<string, double>
                {
                    { "Mg(OH)2", -1 }, { "Mg+2", +1 }, { "OH-", +2 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "Ca(OH)2 solubility",
                Description = "Ca(OH)2 <-> Ca+2 + 2 OH-",
                PKsp = 5.19,
                Stoich = new Dictionary<string, double>
                {
                    { "Ca(OH)2", -1 }, { "Ca+2", +1 }, { "OH-", +2 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "Fe(OH)3 solubility",
                Description = "Fe(OH)3 <-> Fe+3 + 3 OH-",
                PKsp = 38.8,
                Stoich = new Dictionary<string, double>
                {
                    { "Fe(OH)3", -1 }, { "Fe+3", +1 }, { "OH-", +3 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "FeS solubility",
                Description = "FeS <-> Fe+2 + S-2",
                PKsp = 17.2,
                Stoich = new Dictionary<string, double>
                {
                    { "FeS", -1 }, { "Fe+2", +1 }, { "S-2", +1 }
                }
            },
            new PrecipitationTemplate
            {
                Name = "NaCl solubility",
                Description = "NaCl <-> Na+ + Cl-",
                PKsp = -1.58,
                Stoich = new Dictionary<string, double>
                {
                    { "NaCl", -1 }, { "Na+", +1 }, { "Cl-", +1 }
                }
            }
        };
    }
}
