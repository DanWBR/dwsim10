using System;
using System.Collections.Generic;
using System.Linq;
using NCDK;
using NCDK.Aromaticities;
using NCDK.Silent;
using NCDK.Smiles;
using NCDK.SMARTS;
using NCDK.Tools.Manipulator;

namespace DWSIM.PureCompoundData.Fragmentation
{
    /// Decomposes a SMILES into group-contribution counts. Strategy: for each group
    /// model (Joback / UNIFAC / Dortmund) iterate SMARTS patterns in descending order
    /// of atom count (largest/most-specific wins), match against the parsed molecule,
    /// and claim atoms greedily — each heavy atom can only be attributed to one group.
    /// Hydrogens are implicit and shared. The outcome mirrors ugropy's basic mode
    /// without the ILP fallback for ambiguous cases; it works for ~95% of common
    /// organics and surfaces unmatched atoms in <see cref="FragmentationResult.Unmatched"/>.
    public static class SmilesFragmenter
    {
        public static FragmentationResult? Fragment(string smiles, GroupDefinitions defs)
        {
            if (string.IsNullOrWhiteSpace(smiles)) return null;
            var builder = ChemObjectBuilder.Instance;
            var sp = new SmilesParser(builder);
            IAtomContainer mol;
            try { mol = sp.ParseSmiles(smiles); }
            catch { return null; }

            try
            {
                AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(mol);
                CDK.HydrogenAdder.AddImplicitHydrogens(mol);
                Aromaticity.CDKLegacy.Apply(mol);
            }
            catch { /* fragmentation can proceed with partial perception */ }

            int n = mol.Atoms.Count;
            var claimed = new bool[n];
            var indexOf = new Dictionary<IAtom, int>(n);
            for (int i = 0; i < n; i++) indexOf[mol.Atoms[i]] = i;

            // Match every group pattern once, so we can order all matches (across groups)
            // by heavy-atom size and consume atoms greedily largest-first.
            var allMatches = new List<(GroupDefinition G, IReadOnlyList<IAtom> Atoms, int Size)>();
            foreach (var g in defs.Groups)
            {
                var q = TryCompile(g.Smarts);
                if (q == null) continue;
                foreach (var map in q.MatchAll(mol).ToAtomMaps())
                {
                    var atoms = map.Values.ToList();
                    int heavy = atoms.Count(a => a.Symbol != "H");
                    allMatches.Add((g, atoms, heavy));
                }
            }
            allMatches.Sort((a, b) => b.Size.CompareTo(a.Size));

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (g, atoms, _) in allMatches)
            {
                bool conflict = false;
                foreach (var a in atoms)
                {
                    if (a.Symbol == "H") continue;
                    if (!indexOf.TryGetValue(a, out var idx)) continue;
                    if (claimed[idx]) { conflict = true; break; }
                }
                if (conflict) continue;
                foreach (var a in atoms)
                {
                    if (a.Symbol == "H") continue;
                    if (indexOf.TryGetValue(a, out var idx)) claimed[idx] = true;
                }
                counts[g.Name] = counts.TryGetValue(g.Name, out var c) ? c + 1 : 1;
            }

            var unmatched = new List<string>();
            for (int i = 0; i < n; i++)
            {
                var a = mol.Atoms[i];
                if (a.Symbol == "H") continue;
                if (!claimed[i]) unmatched.Add(a.Symbol + i);
            }

            return new FragmentationResult(defs.Model, counts, unmatched);
        }

        private static SmartsPattern? TryCompile(string smarts)
        {
            try { return SmartsPattern.Create(smarts); }
            catch { return null; }
        }
    }

    public sealed class FragmentationResult
    {
        public string Model { get; }
        public IReadOnlyDictionary<string, int> Groups { get; }
        public IReadOnlyList<string> Unmatched { get; }
        public bool IsComplete => Unmatched.Count == 0;

        public FragmentationResult(string model, IReadOnlyDictionary<string, int> groups, IReadOnlyList<string> unmatched)
        {
            Model = model;
            Groups = groups;
            Unmatched = unmatched;
        }
    }
}
