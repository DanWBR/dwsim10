using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.CLI.Output
{
    internal static class CsvFormatter
    {
        public static void WriteDatasets(TextWriter writer, IReadOnlyList<PhaseEquilibriumDataset> datasets)
        {
            writer.WriteLine("Id,Type,Method,Doi,PointCount,Cas1,Cas2");
            foreach (var d in datasets)
            {
                var cas1 = d.Compounds.Count > 0 ? d.Compounds[0].CasNumber : string.Empty;
                var cas2 = d.Compounds.Count > 1 ? d.Compounds[1].CasNumber : string.Empty;
                writer.WriteLine(string.Join(",", new[]
                {
                    Q(d.Id), Q(d.EquilibriumType.ToString()), Q(d.Method.ToString()),
                    Q(d.Citation.Doi ?? string.Empty),
                    d.Points.Count.ToString(CultureInfo.InvariantCulture),
                    Q(cas1), Q(cas2)
                }));
            }
        }

        public static void WriteDataset(TextWriter writer, PhaseEquilibriumDataset d)
        {
            var headers = new List<string>();
            foreach (var p in d.Points)
                foreach (var k in p.Values.Keys)
                    if (!headers.Contains(k)) headers.Add(k);
            writer.WriteLine(string.Join(",", headers.Select(Q)));
            foreach (var p in d.Points)
            {
                writer.WriteLine(string.Join(",", headers.Select(h =>
                    p.Values.TryGetValue(h, out var v)
                        ? v.ToString("G", CultureInfo.InvariantCulture)
                        : string.Empty)));
            }
        }

        private static string Q(string s)
        {
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
