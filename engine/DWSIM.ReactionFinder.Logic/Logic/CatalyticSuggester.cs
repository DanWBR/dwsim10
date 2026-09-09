using System.Collections.Generic;
using System.Linq;
using DWSIM.Extensions.ReactionFinder.Data;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Emits heterogeneous-catalytic reactions from ReactionTables.Catalytic when
    /// every species resolves against the flowsheet compound list.
    /// </summary>
    public static class CatalyticSuggester
    {
        public static IEnumerable<SuggestedReaction> Suggest(CompoundIndex idx)
        {
            foreach (var tpl in ReactionTables.Catalytic)
            {
                var resolved = new Dictionary<string, string>();
                bool ok = true;
                foreach (var s in tpl.Species)
                {
                    var cp = idx.Resolve(s.Identity);
                    if (cp == null) { ok = false; break; }
                    resolved[s.Identity] = cp.Name;
                }
                if (!ok) continue;

                var rxn = ReactionFactory.BuildCatalytic(tpl, resolved);
                rxn.Equation = ReactionFactory.BuildEquationString(rxn, idx.FormulaOf);

                var stoich = tpl.Species.ToDictionary(
                    s => resolved[s.Identity], s => s.StoichCoeff);
                yield return new SuggestedReaction
                {
                    Category = SuggestionCategory.Catalytic,
                    DisplayName = tpl.Name + "  (LHHW)",
                    Equation = rxn.Equation,
                    Reaction = rxn,
                    Signature = "cat:" + ReactionFactory.Signature(stoich)
                };
            }
        }
    }
}
