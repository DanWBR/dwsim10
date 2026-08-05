using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DWSIM.PureCompoundData.Fragmentation
{
    /// Loads group SMARTS tables from embedded ugropy CSVs (format:
    /// <c>group|smarts|molecular_weight</c>, '?' comments). One instance per model.
    public sealed class GroupDefinitions
    {
        public string Model { get; }
        public IReadOnlyList<GroupDefinition> Groups { get; }

        private GroupDefinitions(string model, IReadOnlyList<GroupDefinition> groups)
        {
            Model = model;
            Groups = groups;
        }

        public static GroupDefinitions Unifac => _unifac.Value;
        public static GroupDefinitions Dortmund => _dortmund.Value;
        public static GroupDefinitions Joback => _joback.Value;

        private static readonly Lazy<GroupDefinitions> _unifac =
            new Lazy<GroupDefinitions>(() => Load("UNIFAC",
                "DWSIM.PureCompoundData.Fragmentation.Assets.ugropy.unifac.unifac_subgroups.csv"));

        private static readonly Lazy<GroupDefinitions> _dortmund =
            new Lazy<GroupDefinitions>(() => Load("Dortmund",
                "DWSIM.PureCompoundData.Fragmentation.Assets.ugropy.dortmund.dortmund_subgroups.csv"));

        private static readonly Lazy<GroupDefinitions> _joback =
            new Lazy<GroupDefinitions>(() => Load("Joback",
                "DWSIM.PureCompoundData.Fragmentation.Assets.ugropy.joback.joback_subgroups.csv"));

        private static GroupDefinitions Load(string model, string resourceName)
        {
            var asm = typeof(GroupDefinitions).Assembly;
            using var s = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("Missing embedded resource: " + resourceName);
            using var rd = new StreamReader(s);
            var list = new List<GroupDefinition>();
            string? line;
            bool first = true;
            while ((line = rd.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '?') continue;
                if (first) { first = false; continue; } // header
                var parts = line.Split('|');
                if (parts.Length < 3) continue;
                double mw = 0;
                double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out mw);
                list.Add(new GroupDefinition(parts[0].Trim(), parts[1].Trim(), mw));
            }
            return new GroupDefinitions(model, list);
        }
    }
}
