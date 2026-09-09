using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Generates complete-combustion reactions for any compound made of (C,H,O,N,S)
    /// when O2, CO2 and H2O are present in the flowsheet.
    ///     CxHyNzSwOv + (x + y/4 + w - v/2) O2 -&gt; x CO2 + (y/2) H2O + (z/2) N2 + w SO2
    /// Stoichiometry is doubled if any coefficient is non-integer (i.e. odd y/z).
    /// </summary>
    public static class CombustionSuggester
    {
        private static readonly HashSet<string> AllowedElements
            = new HashSet<string>(new[] { "C", "H", "O", "N", "S" });

        public static IEnumerable<SuggestedReaction> Suggest(CompoundIndex idx)
        {
            var o2 = idx.FindByFormula("O2") ?? idx.Resolve("7782-44-7");
            var co2 = idx.FindByFormula("CO2") ?? idx.Resolve("124-38-9");
            var h2o = idx.FindByFormula("H2O") ?? idx.Resolve("7732-18-5");
            if (o2 == null || co2 == null || h2o == null) yield break;

            var n2 = idx.FindByFormula("N2") ?? idx.Resolve("7727-37-9");
            var so2 = idx.FindByFormula("SO2") ?? idx.Resolve("7446-09-5");

            foreach (var fuel in idx.All)
            {
                // Skip the oxidiser / products themselves.
                if (fuel == o2 || fuel == co2 || fuel == h2o || fuel == n2 || fuel == so2) continue;

                var elems = CompoundIndex.GetElements(fuel);
                if (elems.Count == 0) continue;
                if (elems.Keys.Any(k => !AllowedElements.Contains(k))) continue;
                if (!elems.TryGetValue("C", out int nC)) nC = 0;
                if (!elems.TryGetValue("H", out int nH)) nH = 0;
                if (!elems.TryGetValue("O", out int nO)) nO = 0;
                if (!elems.TryGetValue("N", out int nN)) nN = 0;
                if (!elems.TryGetValue("S", out int nS)) nS = 0;

                // Must actually produce at least one of CO2 or H2O to be meaningful.
                if (nC == 0 && nH == 0) continue;
                // Need N2 compound if fuel contains nitrogen, otherwise skip.
                if (nN > 0 && n2 == null) continue;
                if (nS > 0 && so2 == null) continue;

                // Work in half-units (multiply all by 2) to keep integers.
                int fuelCoef = 2;
                int o2Coef = 2 * nC + (nH / 2) + 2 * nS - nO;
                int co2Coef = 2 * nC;
                int h2oCoef = nH;
                int n2Coef = nN;
                int so2Coef = 2 * nS;
                if (o2Coef <= 0) continue;

                int g = Gcd(fuelCoef, Gcd(o2Coef, Gcd(co2Coef, Gcd(h2oCoef, Gcd(n2Coef, so2Coef)))));
                if (g > 1)
                {
                    fuelCoef /= g; o2Coef /= g; co2Coef /= g;
                    h2oCoef /= g; n2Coef /= g; so2Coef /= g;
                }

                var stoich = new Dictionary<string, double>
                {
                    { fuel.Name, -fuelCoef },
                    { o2.Name, -o2Coef }
                };
                if (co2Coef > 0) stoich[co2.Name] = co2Coef;
                if (h2oCoef > 0) stoich[h2o.Name] = h2oCoef;
                if (n2Coef > 0) stoich[n2.Name] = n2Coef;
                if (so2Coef > 0) stoich[so2.Name] = so2Coef;

                var rxn = ReactionFactory.BuildConversion(
                    "Combustion of " + fuel.Name,
                    "Complete combustion of " + fuel.Name + " with oxygen",
                    stoich);
                rxn.Equation = ReactionFactory.BuildEquationString(rxn, idx.FormulaOf);

                yield return new SuggestedReaction
                {
                    Category = SuggestionCategory.Combustion,
                    DisplayName = rxn.Name,
                    Equation = rxn.Equation,
                    Reaction = rxn,
                    Signature = "comb:" + ReactionFactory.Signature(stoich)
                };
            }
        }

        private static int Gcd(int a, int b)
        {
            a = System.Math.Abs(a); b = System.Math.Abs(b);
            while (b != 0) { var t = b; b = a % b; a = t; }
            return a == 0 ? 1 : a;
        }
    }
}
