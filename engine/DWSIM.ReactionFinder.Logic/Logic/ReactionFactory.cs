using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DWSIM.Extensions.ReactionFinder.Data;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Builds concrete DWSIM.Thermodynamics.BaseClasses.Reaction objects from
    /// stoichiometric templates. Negative coefficients = reactants, positive =
    /// products (matching DWSIM's own convention in the Reactions Manager).
    /// </summary>
    public static class ReactionFactory
    {
        /// <summary>
        /// Builds an Equilibrium reaction. The base reactant is the first
        /// compound with a negative coefficient.
        /// </summary>
        public static Reaction BuildEquilibrium(string name, string description,
            IEnumerable<KeyValuePair<string, double>> stoich,
            ReactionPhase phase = ReactionPhase.Liquid)
        {
            var rxn = new Reaction
            {
                Name = name,
                ID = Guid.NewGuid().ToString(),
                Description = description ?? string.Empty,
                ReactionType = ReactionType.Equilibrium,
                ReactionBasis = ReactionBasis.Activity,
                ReactionPhase = phase,
                KExprType = KOpt.Gibbs,
                Tmin = 0,
                Tmax = 2000,
                Approach = 1.0
            };
            PopulateStoich(rxn, stoich);
            rxn.Equation = BuildEquationString(rxn);
            return rxn;
        }

        /// <summary>
        /// Determines a ReactionPhase by inspecting each compound's physical
        /// state at normal conditions (T = 298.15 K):
        ///   Tfus &gt; 298.15  ⇒ solid
        ///   else Tb &gt; 298.15 ⇒ liquid
        ///   else             ⇒ vapor
        /// </summary>
        public static ReactionPhase InferPhase(
            IEnumerable<ICompoundConstantProperties> compounds)
        {
            const double Tref = 298.15;
            bool hasGas = false, hasLiq = false, hasSol = false;
            foreach (var cp in compounds)
            {
                if (cp == null) continue;
                var tfus = cp.TemperatureOfFusion;
                var tb = cp.Normal_Boiling_Point;
                if (tfus > 0 && tfus > Tref) hasSol = true;
                else if (tb > 0 && tb > Tref) hasLiq = true;
                else hasGas = true;
            }
            if (hasGas && !hasLiq && !hasSol) return ReactionPhase.Vapor;
            if (hasLiq && !hasGas && !hasSol) return ReactionPhase.Liquid;
            if (hasSol && !hasGas && !hasLiq) return ReactionPhase.Solid;
            if (hasLiq && hasSol && !hasGas) return ReactionPhase.Liquid_Solid;
            if (hasGas && hasSol && !hasLiq) return ReactionPhase.Vapor_Solid;
            return ReactionPhase.Mixture;
        }

        /// <summary>
        /// Builds a Conversion reaction with the first reactant as base and a
        /// default 100% conversion expression.
        /// </summary>
        public static Reaction BuildConversion(string name, string description,
            IEnumerable<KeyValuePair<string, double>> stoich)
        {
            var rxn = new Reaction
            {
                Name = name,
                ID = Guid.NewGuid().ToString(),
                Description = description ?? string.Empty,
                ReactionType = ReactionType.Conversion,
                ReactionBasis = ReactionBasis.Activity,
                ReactionPhase = ReactionPhase.Vapor,
                Expression = "100",
                Tmin = 0,
                Tmax = 2000
            };
            PopulateStoich(rxn, stoich);
            rxn.Equation = BuildEquationString(rxn);
            return rxn;
        }

        /// <summary>
        /// Builds a Kinetic reaction with Arrhenius parameters and per-species
        /// forward/reverse reaction orders. <paramref name="resolvedNames"/> maps
        /// the template's identity strings to the actual flowsheet compound Names.
        /// </summary>
        public static Reaction BuildKinetic(KineticTemplate tpl,
            Dictionary<string, string> resolvedNames)
        {
            var rxn = new Reaction
            {
                Name = tpl.Name,
                ID = Guid.NewGuid().ToString(),
                Description = tpl.Description ?? string.Empty,
                ReactionType = ReactionType.Kinetic,
                ReactionBasis = ReactionBasis.MolarConc,
                ReactionPhase = tpl.Phase,
                ReactionKinetics = ReactionKinetics.Expression,
                ReactionKinFwdType = ReactionKineticType.Arrhenius,
                ReactionKinRevType = ReactionKineticType.Arrhenius,
                A_Forward = tpl.A_Forward,
                E_Forward = tpl.E_Forward,
                E_Forward_Unit = "J/mol",
                A_Reverse = tpl.A_Reverse,
                E_Reverse = tpl.E_Reverse,
                E_Reverse_Unit = "J/mol",
                ConcUnit = tpl.ConcUnit,
                VelUnit = tpl.VelUnit,
                Tmin = tpl.Tmin,
                Tmax = tpl.Tmax
            };

            string firstReactant = null;
            foreach (var s in tpl.Species)
                if (s.StoichCoeff < 0 && firstReactant == null)
                    firstReactant = resolvedNames[s.Identity];

            foreach (var s in tpl.Species)
            {
                var name = resolvedNames[s.Identity];
                var isBase = string.Equals(name, firstReactant, StringComparison.Ordinal);
                rxn._Components[name] = new ReactionStoichBase(
                    name, s.StoichCoeff, isBase, s.ForwardOrder, s.ReverseOrder);
                if (isBase) rxn.BaseReactant = name;
            }

            rxn.Equation = BuildEquationString(rxn);
            return rxn;
        }

        /// <summary>
        /// Builds a Heterogeneous Catalytic reaction using a Numerator/Denominator
        /// rate expression in DWSIM's LHHW-style form. Variables used in the
        /// expressions: T (temperature, K), R1..Rn (reactants, in the order they
        /// appear in the template), P1..Pm (products), optionally N1..Nk.
        /// Template species order (reactants first, then products) MUST match the
        /// R/P numbering used in the rate strings.
        /// </summary>
        public static Reaction BuildCatalytic(CatalyticTemplate tpl,
            Dictionary<string, string> resolvedNames)
        {
            var rxn = new Reaction
            {
                Name = tpl.Name,
                ID = Guid.NewGuid().ToString(),
                Description = tpl.Description ?? string.Empty,
                ReactionType = ReactionType.Heterogeneous_Catalytic,
                ReactionBasis = ReactionBasis.MolarConc,
                ReactionPhase = tpl.Phase,
                RateEquationNumerator = tpl.RateNumerator,
                RateEquationDenominator = tpl.RateDenominator,
                ConcUnit = tpl.ConcUnit,
                VelUnit = tpl.VelUnit,
                Tmin = tpl.Tmin,
                Tmax = tpl.Tmax
            };

            string firstReactant = null;
            foreach (var s in tpl.Species)
                if (s.StoichCoeff < 0 && firstReactant == null)
                    firstReactant = resolvedNames[s.Identity];

            // Insertion order matters: add reactants first (in template order),
            // then products, so DWSIM's R1..Rn / P1..Pm numbering matches the
            // rate expression strings.
            foreach (var s in tpl.Species.Where(x => x.StoichCoeff < 0))
            {
                var name = resolvedNames[s.Identity];
                var isBase = string.Equals(name, firstReactant, StringComparison.Ordinal);
                rxn._Components[name] = new ReactionStoichBase(
                    name, s.StoichCoeff, isBase, s.ForwardOrder, s.ReverseOrder);
                if (isBase) rxn.BaseReactant = name;
            }
            foreach (var s in tpl.Species.Where(x => x.StoichCoeff > 0))
            {
                var name = resolvedNames[s.Identity];
                rxn._Components[name] = new ReactionStoichBase(
                    name, s.StoichCoeff, false, s.ForwardOrder, s.ReverseOrder);
            }

            rxn.Equation = BuildEquationString(rxn);
            return rxn;
        }

        private static void PopulateStoich(Reaction rxn,
            IEnumerable<KeyValuePair<string, double>> stoich)
        {
            string firstReactant = null;
            foreach (var kv in stoich)
            {
                if (kv.Value < 0 && firstReactant == null) firstReactant = kv.Key;
            }
            foreach (var kv in stoich)
            {
                var isBase = string.Equals(kv.Key, firstReactant, StringComparison.Ordinal);
                rxn._Components[kv.Key] = new ReactionStoichBase(
                    kv.Key, kv.Value, isBase,
                    Math.Abs(kv.Value),   // DirectOrder default = |stoich|
                    0.0);
                if (isBase) rxn.BaseReactant = kv.Key;
            }
        }

        /// <summary>
        /// Produces "A + 2 B &lt;--&gt; C + D" style string using compound Name keys.
        /// </summary>
        public static string BuildEquationString(IReaction rxn)
        {
            return BuildEquationString(rxn, null);
        }

        /// <summary>
        /// Produces "A + 2 B &lt;--&gt; C + D" style string where each species token
        /// is emitted using <paramref name="formulaOf"/>(compoundName) instead of
        /// the flowsheet Name. When the resolver is null or returns null/empty,
        /// falls back to the compound Name.
        /// </summary>
        public static string BuildEquationString(IReaction rxn, Func<string, string> formulaOf)
        {
            var reactants = new List<string>();
            var products = new List<string>();
            foreach (var kv in rxn.Components)
            {
                var coeff = kv.Value.StoichCoeff;
                var symbol = formulaOf != null ? formulaOf(kv.Key) : null;
                if (string.IsNullOrEmpty(symbol)) symbol = kv.Key;
                var token = FormatCoeff(Math.Abs(coeff)) + symbol;
                if (coeff < 0) reactants.Add(token);
                else if (coeff > 0) products.Add(token);
            }
            var sb = new StringBuilder();
            sb.Append(string.Join(" + ", reactants));
            sb.Append(rxn.ReactionType == ReactionType.Equilibrium ? " <--> " : " --> ");
            sb.Append(string.Join(" + ", products));
            return sb.ToString();
        }

        private static string FormatCoeff(double v)
        {
            if (Math.Abs(v - 1.0) < 1e-9) return string.Empty;
            // Prefer integer display when close to integer.
            if (Math.Abs(v - Math.Round(v)) < 1e-6)
                return ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Produces a canonical signature like "a=1|b=2|c=-1" used to
        /// de-duplicate suggestions across different suggesters.
        /// </summary>
        public static string Signature(IEnumerable<KeyValuePair<string, double>> stoich)
        {
            return string.Join("|",
                stoich.Where(kv => Math.Abs(kv.Value) > 1e-9)
                      .Select(kv => kv.Key + "=" + kv.Value.ToString("0.###", CultureInfo.InvariantCulture))
                      .OrderBy(s => s, StringComparer.Ordinal));
        }
    }
}
