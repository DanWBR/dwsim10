using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Lookup helper that resolves a logical identity (formula or common name)
    /// to the exact compound Name DWSIM uses in the flowsheet. Required because
    /// IReaction.Components is keyed by compound Name and suggesters express
    /// templates by formula.
    /// </summary>
    public class CompoundIndex
    {
        private readonly Dictionary<string, ICompoundConstantProperties> _byName
            = new Dictionary<string, ICompoundConstantProperties>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ICompoundConstantProperties> _byFormula
            = new Dictionary<string, ICompoundConstantProperties>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ICompoundConstantProperties> _byCasNumber
            = new Dictionary<string, ICompoundConstantProperties>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<ICompoundConstantProperties> All => _byName.Values;

        public CompoundIndex(IEnumerable<ICompoundConstantProperties> compounds)
        {
            foreach (var c in compounds)
            {
                if (c == null) continue;
                if (!string.IsNullOrEmpty(c.Name) && !_byName.ContainsKey(c.Name))
                    _byName.Add(c.Name, c);
                if (!string.IsNullOrEmpty(c.Formula) && !_byFormula.ContainsKey(c.Formula))
                    _byFormula.Add(c.Formula, c);
                if (!string.IsNullOrEmpty(c.CAS_Number) && !_byCasNumber.ContainsKey(c.CAS_Number))
                    _byCasNumber.Add(c.CAS_Number, c);
            }
        }

        public ICompoundConstantProperties FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _byName.TryGetValue(name, out var c);
            return c;
        }

        public ICompoundConstantProperties FindByFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula)) return null;
            return _byFormula.TryGetValue(formula, out var c) ? c : null;
        }

        public ICompoundConstantProperties FindByCas(string cas)
        {
            if (string.IsNullOrEmpty(cas)) return null;
            return _byCasNumber.TryGetValue(cas, out var c) ? c : null;
        }

        /// <summary>
        /// Returns the chemical formula for a given flowsheet compound Name,
        /// or the original name when no formula is available. Intended for
        /// building human-readable reaction equations.
        /// </summary>
        public string FormulaOf(string name)
        {
            var cp = FindByName(name);
            var f = cp?.Formula;
            return string.IsNullOrEmpty(f) ? name : f;
        }

        /// <summary>
        /// Try to resolve by any of formula, CAS, or name (in that order).
        /// </summary>
        public ICompoundConstantProperties Resolve(string identity)
        {
            return FindByFormula(identity) ?? FindByCas(identity) ?? FindByName(identity);
        }

        /// <summary>
        /// Returns the element-count dictionary for a compound, normalised to
        /// a plain Dictionary&lt;string,int&gt; keyed by element symbol.
        /// </summary>
        public static Dictionary<string, int> GetElements(ICompoundConstantProperties cp)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (cp?.Elements == null) return result;
            foreach (DictionaryEntry entry in cp.Elements)
            {
                if (entry.Key == null) continue;
                var key = entry.Key.ToString();
                int count = 0;
                if (entry.Value != null) int.TryParse(entry.Value.ToString(), out count);
                if (count > 0) result[key] = count;
            }
            return result;
        }
    }
}
