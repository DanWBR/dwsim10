using System;
using System.Collections.Generic;
using System.IO;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Maps the group names the fragmenter produces (DWSIM.PureCompoundData.Fragmentation) to
    /// DWSIM's numeric subgroup IDs, as defined in unifac.txt, modfac.txt and
    /// NIST-MODFAC_RiQi.txt. Those three files are embedded in DWSIM.Thermodynamics; each is
    /// parsed once and the name to subgroup ID map kept for the rest of the session.
    /// </summary>
    public static class GroupIdMap
    {

        private static readonly Lazy<Dictionary<string, string>> _unifac =
            new Lazy<Dictionary<string, string>>(LoadUnifac);
        private static readonly Lazy<Dictionary<string, string>> _modfac =
            new Lazy<Dictionary<string, string>>(LoadModfac);
        private static readonly Lazy<Dictionary<string, string>> _nistmfac =
            new Lazy<Dictionary<string, string>>(LoadNistMfac);

        public static Dictionary<string, string> Unifac() { return _unifac.Value; }

        public static Dictionary<string, string> Dortmund() { return _modfac.Value; }

        public static Dictionary<string, string> NistMfac() { return _nistmfac.Value; }

        private static Stream ThermoStream(string name)
        {
            var assembly = typeof(DWSIM.Thermodynamics.BaseClasses.ConstantProperties).Assembly;
            return assembly.GetManifestResourceStream(name);
        }

        private static Dictionary<string, string> LoadUnifac()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var stream = ThermoStream("DWSIM.Thermodynamics.unifac.txt"))
            {
                if (stream == null) return map;

                using (var reader = new StreamReader(stream))
                {
                    reader.ReadLine(); // column headers

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line.StartsWith("'")) continue;

                        var parts = line.Split(',');
                        if (parts.Length < 4) continue;

                        var id = parts[1].Trim();
                        var name = parts[3].Trim();
                        if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name)) map[name] = id;
                    }
                }
            }

            return map;
        }

        private static Dictionary<string, string> LoadModfac()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var stream = ThermoStream("DWSIM.Thermodynamics.modfac.txt"))
            {
                if (stream == null) return map;

                using (var reader = new StreamReader(stream))
                {
                    reader.ReadLine(); // header

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0) continue;

                        var parts = line.Split(';');
                        if (parts.Length < 4) continue;

                        var subgroup = parts[2].Trim();
                        var id = parts[3].Trim();
                        if (!string.IsNullOrEmpty(subgroup) && !map.ContainsKey(subgroup)) map[subgroup] = id;
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// NIST-MODFAC_RiQi.txt is "(n) Main Name" headers followed by tab separated rows of
        /// number, subgroup name, Ri, Qi, example and sample groups.
        /// </summary>
        private static Dictionary<string, string> LoadNistMfac()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var stream = ThermoStream("DWSIM.Thermodynamics.NIST-MODFAC_RiQi.txt"))
            {
                if (stream == null) return map;

                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0) continue;
                        if (line.TrimStart().StartsWith("(")) continue;

                        var parts = line.Split('\t');
                        if (parts.Length < 2) continue;

                        var id = parts[0].Trim();
                        var name = parts[1].Trim();

                        int number;
                        if (!int.TryParse(id, out number)) continue;

                        if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name)) map[name] = id;
                    }
                }
            }

            return map;
        }

    }

}
