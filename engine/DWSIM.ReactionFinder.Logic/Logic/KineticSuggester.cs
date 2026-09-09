using System.Collections.Generic;
using System.Linq;
using DWSIM.Extensions.ReactionFinder.Data;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Emits reactions from ReactionTables.Kinetic when every species is
    /// resolvable against the flowsheet compound list.
    /// </summary>
    public static class KineticSuggester
    {
        public static IEnumerable<SuggestedReaction> Suggest(CompoundIndex idx)
        {
            foreach (var tpl in ReactionTables.Kinetic)
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

                var rxn = ReactionFactory.BuildKinetic(tpl, resolved);
                rxn.Equation = ReactionFactory.BuildEquationString(rxn, idx.FormulaOf);

                // Build a display label carrying A/E so the user can inspect
                // parameters without opening the reaction editor.
                var label = string.Format("{0}  (A={1:0.##E+0}, E={2:0} kJ/mol)",
                    tpl.Name, tpl.A_Forward, tpl.E_Forward / 1000.0);

                var stoich = tpl.Species.ToDictionary(
                    s => resolved[s.Identity], s => s.StoichCoeff);
                yield return new SuggestedReaction
                {
                    Category = SuggestionCategory.Kinetic,
                    DisplayName = label,
                    Equation = rxn.Equation,
                    Reaction = rxn,
                    Signature = "kin:" + ReactionFactory.Signature(stoich)
                };
            }
        }
    }
}
