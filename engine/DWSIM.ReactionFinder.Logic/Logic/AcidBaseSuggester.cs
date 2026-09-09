using System.Collections.Generic;
using System.Linq;
using DWSIM.Extensions.ReactionFinder.Data;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Emits acid-base equilibria from the curated table in ReactionTables.AcidBase
    /// whenever every template compound is resolvable in the flowsheet.
    /// </summary>
    public static class AcidBaseSuggester
    {
        public static IEnumerable<SuggestedReaction> Suggest(CompoundIndex idx)
        {
            foreach (var tpl in ReactionTables.AcidBase)
            {
                // Resolve all template identities to flowsheet compound Names.
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
                    Category = SuggestionCategory.AcidBase,
                    DisplayName = tpl.Name + "  (pKa " + tpl.PKa.ToString("0.##") + ")",
                    Equation = rxn.Equation,
                    Reaction = rxn,
                    Signature = "ab:" + ReactionFactory.Signature(resolved)
                };
            }
        }
    }
}
