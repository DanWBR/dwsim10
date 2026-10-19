using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.MCPServer.Sessions;
using DWSIM.UnitOperations.Reactors;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.MCPServer.Tools.Reactions
{
    public class ReactionTools
    {
        private readonly SessionManager _sessions;

        private static readonly string[] Phases = { "Mixture", "Vapor", "Liquid", "Solid" };

        private static readonly string[] Bases =
        {
            "activity", "fugacity", "partial pressure", "molar concentration",
            "mass concentration", "molar fraction", "mass fraction"
        };

        public ReactionTools(SessionManager sessions) { _sessions = sessions; }

        [McpTool("dwsim_reaction_add_conversion",
            "Define a conversion reaction and put it in a reaction set (DefaultSet unless named). Stoichiometry is " +
            "{compound: coefficient}, negative for reactants. The conversion of the base compound is in percent, a " +
            "number or an expression in T (K). Add the compounds first: the heat of reaction is computed from their " +
            "ideal-gas enthalpies of formation when the reaction is created. Every reactor whose ReactionSetID names " +
            "the set uses it; a new reactor points at DefaultSet.")]
        public JObject AddConversion(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Reaction name, unique in the flowsheet")] string name,
            [McpParam("Stoichiometry as {compound: coefficient}; reactants negative", JsonType = "object")] JObject stoichiometry,
            [McpParam("Base compound (a reactant); the conversion refers to it")] string base_compound,
            [McpParam("Conversion of the base compound in %, number or expression in T (default 100)", Required = false)] string conversion = "100",
            [McpParam("Reacting phase: Mixture, Vapor, Liquid or Solid (default Vapor)", Required = false)] string phase = "Vapor",
            [McpParam("Reaction set id (default DefaultSet; created if missing)", Required = false)] string reaction_set = "DefaultSet",
            [McpParam("Order in the set: same rank runs in parallel, higher rank after (default 0)", Required = false)] int rank = 0,
            [McpParam("Description", Required = false)] string description = "")
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var stoich = Validate(fs, name, stoichiometry, base_compound, phase);

            var r = fs.DefineConversionReaction(name, stoich, base_compound, Canonical(phase),
                                                string.IsNullOrWhiteSpace(conversion) ? "100" : conversion, description ?? "");
            return AddToSet(fs, r, reaction_set, rank);
        }

        [McpTool("dwsim_reaction_add_equilibrium",
            "Define an equilibrium reaction and put it in a reaction set (DefaultSet unless named). Without " +
            "lnkeq_expression the constant comes from the Gibbs energies of formation; with it, ln(Keq) is an " +
            "expression in T (K). Basis: activity, fugacity, partial pressure, molar concentration, mass " +
            "concentration, molar fraction or mass fraction. An equilibrium reactor takes its compounds from " +
            "the reactions of its set.")]
        public JObject AddEquilibrium(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Reaction name, unique in the flowsheet")] string name,
            [McpParam("Stoichiometry as {compound: coefficient}; reactants negative", JsonType = "object")] JObject stoichiometry,
            [McpParam("Base compound (a reactant)")] string base_compound,
            [McpParam("Reacting phase: Mixture, Vapor, Liquid or Solid (default Vapor)", Required = false)] string phase = "Vapor",
            [McpParam("Equilibrium basis (default fugacity)", Required = false)] string basis = "fugacity",
            [McpParam("Units of the basis, e.g. Pa for partial pressure (default empty)", Required = false)] string units = "",
            [McpParam("ln(Keq) as an expression in T (K); empty = from Gibbs energies of formation", Required = false)] string lnkeq_expression = "",
            [McpParam("Temperature approach in K (default 0)", Required = false)] double approach_K = 0,
            [McpParam("Reaction set id (default DefaultSet; created if missing)", Required = false)] string reaction_set = "DefaultSet",
            [McpParam("Order in the set (default 0)", Required = false)] int rank = 0,
            [McpParam("Description", Required = false)] string description = "")
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var stoich = Validate(fs, name, stoichiometry, base_compound, phase);

            var b = string.IsNullOrWhiteSpace(basis) ? "fugacity" : basis.Trim().ToLowerInvariant();
            if (!Bases.Contains(b))
                throw new ArgumentException($"'{basis}' is not an equilibrium basis. Use one of: {string.Join(", ", Bases)}.");

            var r = fs.DefineEquilibriumReaction(name, stoich, base_compound, Canonical(phase), b,
                                                 units ?? "", lnkeq_expression ?? "", approach_K, description ?? "");
            return AddToSet(fs, r, reaction_set, rank);
        }

        [McpTool("dwsim_reactionset_create",
            "Create an empty reaction set. A flowsheet starts with DefaultSet, which every new reactor uses; " +
            "another set is needed only when two reactors must run different reactions. Point a reactor at it " +
            "with dwsim_unitop_set {\"ReactionSetID\": id}.")]
        public JObject CreateSet(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Reaction set id")] string name,
            [McpParam("Description", Required = false)] string description = "")
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A reaction set needs a name.");
            if (fs.Inner.ReactionSets.ContainsKey(name))
                throw new ArgumentException($"Reaction set '{name}' already exists. Use dwsim_reaction_list to see it.");

            fs.ReactionSet(name, description ?? "");
            return new JObject { ["reaction_set"] = name };
        }

        [McpTool("dwsim_reaction_list",
            "List the reactions (type, equation, stoichiometry, base compound, phase, conversion or ln(Keq) " +
            "expression, heat of reaction in kJ/kmol of base compound, mass balance) and the reaction sets with " +
            "their members. Works for loaded flowsheets too.")]
        public JObject List(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var reactions = new JArray(fs.Inner.Reactions.Values.Select(Describe));
            var sets = new JArray(fs.Inner.ReactionSets.Values.Select(s => new JObject
            {
                ["id"] = s.ID,
                ["name"] = s.Name,
                ["reactions"] = new JArray(s.Reactions.Values.Select(m => new JObject
                {
                    ["reaction"] = m.ReactionID,
                    ["rank"] = m.Rank,
                    ["active"] = m.IsActive
                }))
            }));
            return new JObject { ["reactions"] = reactions, ["reaction_sets"] = sets };
        }

        [McpTool("dwsim_reactor_set_compounds",
            "Set the compounds a Gibbs reactor minimises over and build its element matrix. Call it once the feed " +
            "is connected and specified: the matrix is built from the feed, and every element of the listed " +
            "compounds must enter with it. Without it the reactor fails with 'The Element Matrix is not defined'.")]
        public JObject SetGibbsCompounds(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Gibbs reactor tag")] string name,
            [McpParam("Compounds taking part in the equilibrium", JsonType = "array")] string[] compounds)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            var obj = fs.Inner.SimulationObjects.Values.FirstOrDefault(
                o => o.GraphicObject != null && (o.GraphicObject.Tag == name || o.Name == name));

            if (obj == null)
                throw new ArgumentException($"No unit operation with tag or id '{name}'.");
            if (!(obj is Reactor_Gibbs gibbs))
                throw new ArgumentException($"'{name}' is a {obj.GraphicObject.ObjectType}, not a Gibbs reactor.");
            if (!gibbs.GraphicObject.InputConnectors[0].IsAttached)
                throw new ArgumentException($"Connect the feed of '{name}' first: the element matrix is built from it.");
            if (compounds == null || compounds.Length == 0)
                throw new ArgumentException("List the compounds taking part in the equilibrium.");

            var missing = compounds.Where(c => !fs.Inner.SelectedCompounds.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Not in the flowsheet: {string.Join(", ", missing)}. Add them with dwsim_thermo_add_compounds.");

            gibbs.ComponentIDs = compounds.Distinct().ToList();
            // A previous solution is reused when it has one value per compound, so one sized for
            // another list must not seed the minimisation.
            gibbs.InitialEstimates = new List<double>();
            gibbs.PreviousSolution = new List<double>();
            gibbs.CreateElementMatrix();

            return new JObject
            {
                ["reactor"] = name,
                ["compounds"] = new JArray(gibbs.ComponentIDs),
                ["elements"] = new JArray(gibbs.Elements)
            };
        }

        /// <summary>The stoichiometry as the engine takes it, after the checks the engine does not make.</summary>
        private static Dictionary<string, double> Validate(FluentFlowsheet fs, string name, JObject stoichiometry,
                                                           string baseCompound, string phase)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A reaction needs a name.");
            if (fs.Inner.Reactions.ContainsKey(name))
                throw new ArgumentException($"Reaction '{name}' already exists. Use dwsim_reaction_list to see it.");
            if (stoichiometry == null || stoichiometry.Count < 2)
                throw new ArgumentException("Stoichiometry needs at least two compounds.");

            var stoich = new Dictionary<string, double>();
            foreach (var p in stoichiometry.Properties())
            {
                if (p.Value.Type != JTokenType.Float && p.Value.Type != JTokenType.Integer)
                    throw new ArgumentException($"The coefficient of '{p.Name}' must be a number.");
                var coeff = p.Value.Value<double>();
                if (coeff == 0)
                    throw new ArgumentException($"The coefficient of '{p.Name}' is zero; leave the compound out.");
                stoich[p.Name] = coeff;
            }

            // The engine skips a compound it does not know when it sums the enthalpies of formation, so the
            // heat of reaction would come out wrong without any error.
            var missing = stoich.Keys.Where(c => !fs.Inner.SelectedCompounds.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Not in the flowsheet: {string.Join(", ", missing)}. Add them with dwsim_thermo_add_compounds first.");

            if (!stoich.TryGetValue(baseCompound ?? "", out var baseCoeff))
                throw new ArgumentException($"Base compound '{baseCompound}' is not in the stoichiometry.");
            if (baseCoeff > 0)
                throw new ArgumentException($"Base compound '{baseCompound}' must be a reactant (negative coefficient).");

            if (Canonical(phase) == null)
                throw new ArgumentException($"'{phase}' is not a reaction phase. Use one of: {string.Join(", ", Phases)}.");

            var imbalance = ElementImbalance(fs, stoich);
            if (imbalance.Count > 0)
                throw new ArgumentException("The reaction does not balance: " +
                    string.Join(", ", imbalance.Select(e => $"{e.Key} {e.Value:+0.####;-0.####}")) + " (products minus reactants).");

            return stoich;
        }

        /// <summary>The engine's name of the phase; an unknown name would silently leave the default.</summary>
        private static string Canonical(string phase) =>
            Phases.FirstOrDefault(p => string.Equals(p, string.IsNullOrWhiteSpace(phase) ? "Vapor" : phase.Trim(),
                                                     StringComparison.OrdinalIgnoreCase));

        /// <summary>Atoms of each element created by the reaction, where it is not zero.</summary>
        private static Dictionary<string, double> ElementImbalance(FluentFlowsheet fs, Dictionary<string, double> stoich)
        {
            var sum = new Dictionary<string, double>();
            foreach (var entry in stoich)
            {
                var elements = fs.Inner.SelectedCompounds[entry.Key].Elements;
                // a pseudo-compound has no formula to check against
                if (elements == null || elements.Count == 0) return new Dictionary<string, double>();
                foreach (DictionaryEntry el in elements)
                {
                    var key = (string)el.Key;
                    sum[key] = (sum.TryGetValue(key, out var v) ? v : 0) + entry.Value * Convert.ToDouble(el.Value);
                }
            }
            return sum.Where(e => Math.Abs(e.Value) > 1e-9).ToDictionary(e => e.Key, e => e.Value);
        }

        private static JObject AddToSet(FluentFlowsheet fs, IReaction r, string set, int rank)
        {
            var id = string.IsNullOrWhiteSpace(set) ? "DefaultSet" : set;
            fs.ReactionSet(id).Add(r, rank);
            var o = Describe(r);
            o["reaction_set"] = id;
            return o;
        }

        private static JObject Describe(IReaction r) => new JObject
        {
            ["name"] = r.Name,
            ["type"] = r.ReactionType.ToString(),
            ["equation"] = r.Equation,
            ["stoichiometry"] = new JObject(r.Components.Values.Select(c => new JProperty(c.CompName, c.StoichCoeff))),
            ["base_compound"] = r.BaseReactant,
            ["phase"] = r.ReactionPhase.ToString(),
            ["expression"] = r.Expression,
            ["keq_from"] = r.ReactionType == ReactionType.Equilibrium ? r.KExprType.ToString() : null,
            ["heat_of_reaction_kJ_kmol"] = r.ReactionHeat,
            ["mass_balance_kg_kmol"] = r.StoichBalance
        };
    }
}
