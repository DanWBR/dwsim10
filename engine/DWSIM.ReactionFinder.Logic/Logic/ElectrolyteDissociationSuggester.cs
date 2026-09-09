using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DWSIM.Interfaces.Enums;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Emits electrolyte equilibrium reactions from the embedded
    /// <c>electrolyte_reactions.json</c> dataset (auto-generated from
    /// <c>electrolyte.xml</c> plus a curated list of common aqueous-process
    /// equilibria).
    ///
    /// <para>Every reaction is built as <c>Equilibrium</c> with K computed at
    /// runtime from compound Gibbs energies of formation
    /// (<see cref="DWSIM.Interfaces.Enums.KOpt.Gibbs"/>), so the equilibrium
    /// constant follows the standard ΔG_rxn = Σν·ΔGf identity automatically.</para>
    ///
    /// <para>Salt-dissociation reactions follow the convention
    /// <c>salt &lt;-&gt; ν+·cation + ν-·anion (+ n·H2O for hydrates)</c>; the
    /// stoichiometric coefficients come straight from the
    /// <c>PositiveIonStoichCoeff</c> / <c>NegativeIonStoichCoeff</c> /
    /// <c>HydrationNumber</c> fields of <c>electrolyte.xml</c>.</para>
    ///
    /// <para>A reaction is emitted only when every participating species
    /// resolves through <see cref="CompoundIndex"/> (formula → CAS → Name
    /// fallback). Reactions referencing species absent from the flowsheet
    /// are silently dropped, matching the behaviour of
    /// <see cref="LibrarySuggester"/> and <see cref="AcidBaseSuggester"/>.</para>
    /// </summary>
    public static class ElectrolyteDissociationSuggester
    {
        /// <summary>
        /// Manifest-resource name of the embedded JSON data file. The build
        /// configures this via <c>EmbeddedResource</c> in the .csproj; the
        /// resource ID is the project's <c>RootNamespace</c> ("ReactionFinder")
        /// plus the relative path with directory separators replaced by dots.
        /// </summary>
        private const string ResourceName = "ReactionFinder.Data.electrolyte_reactions.json";

        public static IEnumerable<SuggestedReaction> Suggest(CompoundIndex idx)
        {
            JArray entries;
            try { entries = LoadEntries(); }
            catch { yield break; }

            foreach (var token in entries)
            {
                if (!(token is JObject e)) continue;

                string id          = (string)e["id"];
                string name        = (string)e["name"];
                string description = (string)e["description"];
                string category    = (string)e["category"];
                string phaseStr    = (string)e["phase"];
                if (string.IsNullOrEmpty(name)) continue;

                if (!(e["stoich"] is JObject stoich)) continue;
                var resolved = new Dictionary<string, double>(StringComparer.Ordinal);
                bool ok = true;
                foreach (var kv in stoich)
                {
                    var cp = idx.Resolve(kv.Key);
                    if (cp == null) { ok = false; break; }
                    double coeff = ParseDouble(kv.Value);
                    if (Math.Abs(coeff) < 1e-12) continue;
                    resolved[cp.Name] = coeff;
                }
                if (!ok || resolved.Count < 2) continue;

                ReactionPhase phase = ParsePhase(phaseStr);
                var rxn = ReactionFactory.BuildEquilibrium(name, description, resolved, phase);
                rxn.Equation = ReactionFactory.BuildEquationString(rxn, idx.FormulaOf);

                // Annotate the description with ΔG_rxn or pKa info when present
                // so the user sees the source/quality at a glance in the picker.
                string suffix = "";
                if (e["delta_G_rxn_298_kJ_mol"] != null && e["delta_G_rxn_298_kJ_mol"].Type == JTokenType.Float)
                {
                    suffix = $" (ΔG_rxn ≈ {e["delta_G_rxn_298_kJ_mol"]:0.#} kJ/mol)";
                }
                else if (e["pKa_or_pKw_298"] != null)
                {
                    suffix = $" (pKa/pKw {e["pKa_or_pKw_298"]:0.##})";
                }

                yield return new SuggestedReaction
                {
                    Category = MapCategory(category),
                    DisplayName = name + suffix,
                    Equation = rxn.Equation,
                    Reaction = rxn,
                    Signature = "el:" + ReactionFactory.Signature(resolved)
                };
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static double ParseDouble(JToken t)
        {
            if (t == null) return 0;
            if (t.Type == JTokenType.Integer) return (long)t;
            if (t.Type == JTokenType.Float) return (double)t;
            if (t.Type == JTokenType.String && double.TryParse((string)t, out double v)) return v;
            return 0;
        }

        private static ReactionPhase ParsePhase(string s)
        {
            if (string.IsNullOrEmpty(s)) return ReactionPhase.Liquid;
            switch (s.Trim().ToLowerInvariant())
            {
                case "vapor":
                case "vapour":     return ReactionPhase.Vapor;
                case "liquid":
                case "aqueous":    return ReactionPhase.Liquid;
                case "solid":      return ReactionPhase.Solid;
                case "mixture":    return ReactionPhase.Mixture;
                default:           return ReactionPhase.Liquid;
            }
        }

        private static SuggestionCategory MapCategory(string s)
        {
            if (string.IsNullOrEmpty(s)) return SuggestionCategory.AcidBase;
            switch (s.Trim())
            {
                case "SaltDissociation":
                case "Precipitation":   return SuggestionCategory.Precipitation;
                case "AcidBase":
                case "WaterChemistry":  return SuggestionCategory.AcidBase;
                default:                return SuggestionCategory.AcidBase;
            }
        }

        private static JArray LoadEntries()
        {
            var asm = typeof(ElectrolyteDissociationSuggester).Assembly;
            using (var stream = asm.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException("Embedded resource not found: " + ResourceName);

                using (var reader = new StreamReader(stream))
                {
                    var doc = JObject.Parse(reader.ReadToEnd());
                    return (doc["reactions"] as JArray) ?? new JArray();
                }
            }
        }
    }
}
