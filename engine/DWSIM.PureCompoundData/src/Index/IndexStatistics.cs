using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace DWSIM.PureCompoundData.Index
{
    public sealed class IndexStatistics
    {
        private readonly LiteDatabase _db;
        private readonly string? _dbPath;

        internal IndexStatistics(LiteDatabase db, string? dbPath)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _dbPath = dbPath;
        }

        public StatsReport Compute()
        {
            var records = _db.GetCollection<RecordDoc>(SchemaMigrator.RecordsCollection);
            var compounds = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);

            var breakdown = new Dictionary<string, int>(StringComparer.Ordinal);
            var providers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in records.FindAll())
            {
                breakdown.TryGetValue(r.Category, out var bc);
                breakdown[r.Category] = bc + 1;
                providers.TryGetValue(r.Provider, out var pc);
                providers[r.Provider] = pc + 1;
            }

            long size = 0;
            if (_dbPath != null && File.Exists(_dbPath)) size = new FileInfo(_dbPath).Length;

            return new StatsReport
            {
                TotalRecords = records.Count(),
                UniqueCompounds = compounds.Count(),
                CategoryBreakdown = breakdown,
                ProviderBreakdown = providers,
                DbFileSizeBytes = size
            };
        }
    }

    public sealed class StatsReport
    {
        public int TotalRecords { get; set; }
        public int UniqueCompounds { get; set; }
        public Dictionary<string, int> CategoryBreakdown { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ProviderBreakdown { get; set; } = new Dictionary<string, int>();
        public long DbFileSizeBytes { get; set; }
    }
}
