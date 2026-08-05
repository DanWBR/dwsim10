using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.PhaseEquilibriumData.Core;
using DWSIM.PhaseEquilibriumData.Sources.Internal;

namespace DWSIM.PhaseEquilibriumData.Sources.ThermoML
{
    public sealed class ThermoMLLocalSource : IDataSource
    {
        public const string ProviderName = "ThermoML";

        private readonly string? _archivePath;
        private readonly Action<string>? _warn;

        public ThermoMLLocalSource(string? archivePath = null, Action<string>? warn = null)
        {
            _archivePath = archivePath;
            _warn = warn;
        }

        public string Name => ProviderName;
        public bool IsOffline => true;

        public Task<IReadOnlyList<PhaseEquilibriumDataset>> SearchAsync(CompoundQuery query, CancellationToken ct)
            => throw new NotSupportedException(
                "ThermoMLLocalSource does not support direct SearchAsync in Phase 1. Use ThermoMLIndex after ingest.");

        public IEnumerable<PhaseEquilibriumDataset> EnumerateDatasets()
        {
            if (_archivePath == null) yield break;
            using var reader = TarGzReader.FromFile(_archivePath);
            foreach (var entry in reader.ReadEntries())
            {
                bool isJson = entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                IEnumerable<PhaseEquilibriumDataset> datasets;
                try
                {
                    datasets = ParseFile(entry.Content, isJson);
                }
                catch (Exception ex)
                {
                    _warn?.Invoke($"{entry.Name}: {ex.Message}");
                    continue;
                }
                foreach (var d in datasets) yield return d;
            }
        }

        public IEnumerable<PhaseEquilibriumDataset> ParseFile(Stream content, bool isJson = false)
        {
            ThermoMLFile? file = isJson
                ? new ThermoMLJsonParser(_warn).Parse(content)
                : new ThermoMLParser(_warn).Parse(content);
            if (file == null) yield break;

            foreach (var pmod in file.PureOrMixtureData)
            {
                var compounds = pmod.ComponentOrder
                    .Where(i => file.Compounds.ContainsKey(i))
                    .Select(i => file.Compounds[i])
                    .ToList();
                if (compounds.Count == 0)
                {
                    _warn?.Invoke($"pmod {pmod.Index}: no resolvable compounds; skipping.");
                    continue;
                }

                var firstAuthor = file.Authors.FirstOrDefault();
                var surname = firstAuthor != null
                    ? firstAuthor.Split(',')[0].Trim()
                    : null;

                var id = IdHasher.ComputeDatasetId(
                    file.Doi,
                    pmod.Index,
                    compounds.Select(c => c.CasNumber),
                    file.Journal,
                    file.Year,
                    surname);

                var type = ThermoMLClassifier.Classify(pmod);
                var citation = new Citation(file.Doi, file.Title, file.Authors, file.Journal, file.Year, file.Volume, file.Pages);

                yield return new PhaseEquilibriumDataset(
                    id,
                    type,
                    compounds,
                    pmod.Constraints,
                    pmod.VariableNames,
                    pmod.Points,
                    pmod.Method,
                    citation,
                    ProviderName,
                    Array.Empty<ConsistencyTestResult>());
            }
        }
    }
}
