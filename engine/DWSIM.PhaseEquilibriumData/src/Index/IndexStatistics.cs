using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace DWSIM.PhaseEquilibriumData.Index
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
            var datasets = _db.GetCollection<DatasetDoc>(SchemaMigrator.DatasetsCollection);
            var compounds = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);

            var breakdown = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var d in datasets.FindAll())
            {
                if (!breakdown.ContainsKey(d.EquilibriumType)) breakdown[d.EquilibriumType] = 0;
                breakdown[d.EquilibriumType]++;
            }

            long size = 0;
            if (_dbPath != null && File.Exists(_dbPath)) size = new FileInfo(_dbPath).Length;

            return new StatsReport
            {
                TotalDatasets = datasets.Count(),
                UniqueCompounds = compounds.Count(),
                Breakdown = breakdown,
                DbFileSizeBytes = size
            };
        }
    }

    public sealed class StatsReport
    {
        public int TotalDatasets { get; set; }
        public int UniqueCompounds { get; set; }
        public Dictionary<string, int> Breakdown { get; set; } = new Dictionary<string, int>();
        public long DbFileSizeBytes { get; set; }
    }
}
