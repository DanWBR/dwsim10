using System.Collections.Generic;
using DWSIM.Extensions.ReactionFinder.Data;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Emits salt solubility equilibria from ReactionTables.Precipitation when
    /// the salt and both ions are present in the flowsheet.
    /// </summary>
    public static class PrecipitationSuggester
    {
        public static IEnumerable<SuggestedReaction> Suggest(CompoundIndex idx)
        {
            foreach (var tpl in ReactionTables.Precipitation)
            {
                var resolved = new Dictionary<string, double>();
                bool ok = true;
                foreach (var kv in tpl.Stoich)
                {
                    var cp = idx.Resolve(kv.Key);
                    if (cp == null) { ok = false; break; }
                    resolved[cp.Name] = kv.Value;
                }
                if (!ok || resolved.Count < 2) continue;

                var rxn = ReactionFactory.BuildEquilibrium(tpl.Name, tpl.Description, resolved);
                rxn.Equation = ReactionFactory.BuildEquationString(rxn, idx.FormulaOf);
                yield return new SuggestedReaction
                {
                    Category = SuggestionCategory.Precipitation,
                    DisplayName = tpl.Name + "  (pKsp " + tpl.PKsp.ToString("0.##") + ")",
                    Equation = rxn.Equation,
                    Reaction = rxn,
                    Signature = "pp:" + ReactionFactory.Signature(resolved)
                };
            }
        }
    }
}
