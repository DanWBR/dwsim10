using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.PureCompoundData.Core;
using DWSIM.PureCompoundData.Sources.ThermoML;
using PeqTml = DWSIM.PhaseEquilibriumData.Sources.ThermoML;

namespace DWSIM.PureCompoundData.Sources
{
    /// Reads the NIST ThermoML archive (tar.gz of XML / JSON) and emits
    /// <see cref="PureCompoundRecord"/>s for PureOrMixtureData entries with a single
    /// component. Reuses the phaseq project's tar reader and XML/JSON parsers so
    /// pure-compound and mixture ingest pipelines share the same archive.
    public sealed class ThermoMLPureSource : IDataSource
    {
        public const string ProviderName = ThermoMLPureAdapter.ProviderName;

        private readonly string? _archivePath;
        private readonly Action<string>? _warn;

        public ThermoMLPureSource(string? archivePath = null, Action<string>? warn = null)
        {
            _archivePath = archivePath;
            _warn = warn;
        }

        public string Name => ProviderName;
        public bool IsOffline => true;

        public Task<IReadOnlyList<PureCompoundRecord>> SearchAsync(CompoundQuery query, CancellationToken ct)
            => throw new NotSupportedException(
                "ThermoMLPureSource does not support direct SearchAsync. Use Ingestor + PureCompoundIndex after EnumerateRecords.");

        /// Streams every pure-compound record in the archive.
        public IEnumerable<PureCompoundRecord> EnumerateRecords()
        {
            if (_archivePath == null) yield break;
            using var reader = PeqTml.TarGzReader.FromFile(_archivePath);
            foreach (var entry in reader.ReadEntries())
            {
                bool isJson = entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                IEnumerable<PureCompoundRecord> records;
                try
                {
                    records = ParseFile(entry.Content, isJson);
                }
                catch (Exception ex)
                {
                    _warn?.Invoke($"{entry.Name}: {ex.Message}");
                    continue;
                }
                foreach (var r in records) yield return r;
            }
        }

        public IEnumerable<PureCompoundRecord> ParseFile(Stream content, bool isJson = false)
        {
            var file = isJson
                ? new PeqTml.ThermoMLJsonParser(_warn).Parse(content)
                : new PeqTml.ThermoMLParser(_warn).Parse(content);
            if (file == null) return Array.Empty<PureCompoundRecord>();
            return ThermoMLPureAdapter.Adapt(file);
        }
    }
}
