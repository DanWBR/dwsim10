using System;
using LiteDB;

namespace DWSIM.PhaseEquilibriumData.Index
{
    public sealed class ThermoMLIndex : IDisposable
    {
        private readonly LiteDatabase _db;
        public string? DbPath { get; }
        public Ingestor Ingestor { get; }
        public QueryExecutor Query { get; }
        public IndexStatistics Statistics { get; }

        public ThermoMLIndex(string dbPath, Func<DateTime>? clock = null)
        {
            if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentException("dbPath required", nameof(dbPath));
            DbPath = dbPath;
            _db = new LiteDatabase($"Filename={dbPath};Connection=shared");
            SchemaMigrator.EnsureSchema(_db);
            Ingestor = new Ingestor(_db, clock);
            Query = new QueryExecutor(_db);
            Statistics = new IndexStatistics(_db, dbPath);
        }

        internal ThermoMLIndex(LiteDatabase db, Func<DateTime>? clock = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            SchemaMigrator.EnsureSchema(_db);
            Ingestor = new Ingestor(_db, clock);
            Query = new QueryExecutor(_db);
            Statistics = new IndexStatistics(_db, null);
        }

        internal LiteDatabase Database => _db;

        /// <summary>Creates a fresh <see cref="PhaseEqQuery"/> builder bound to this index.</summary>
        public PhaseEqQuery CreateQuery() => new PhaseEqQuery(Query);

        public void Dispose() => _db.Dispose();
    }
}
