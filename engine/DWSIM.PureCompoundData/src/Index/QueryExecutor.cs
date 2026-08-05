using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Index
{
    public sealed class QueryExecutor
    {
        private readonly LiteDatabase _db;

        internal QueryExecutor(LiteDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public IReadOnlyList<PureCompoundRecord> SearchByCas(
            string cas,
            PropertyCategory? category,
            (double Min, double Max)? temperatureRangeK,
            int maxResults)
        {
            if (string.IsNullOrWhiteSpace(cas)) throw new ArgumentException("cas required", nameof(cas));
            if (maxResults <= 0) maxResults = 100;

            var records = _db.GetCollection<RecordDoc>(SchemaMigrator.RecordsCollection);
            var catStr = category?.ToString();

            // Rank: fits first (pre-fitted > raw points), then PointCount DESC, then Id ASC.
            var candidates = records
                .Find(x => x.Cas == cas)
                .Where(d => catStr == null || d.Category == catStr)
                .Where(d => !temperatureRangeK.HasValue
                            || (d.TMax >= temperatureRangeK.Value.Min && d.TMin <= temperatureRangeK.Value.Max))
                .OrderByDescending(d => d.FitCount > 0 ? 1 : 0)
                .ThenByDescending(d => d.PointCount)
                .ThenBy(d => d.Id, StringComparer.Ordinal)
                .Take(maxResults)
                .ToList();

            return candidates.Select(Deserialize).ToList();
        }

        public IReadOnlyList<PureCompoundRecord> SearchByInChIKey(
            string inchiKey,
            PropertyCategory? category,
            (double Min, double Max)? temperatureRangeK,
            int maxResults)
        {
            if (string.IsNullOrWhiteSpace(inchiKey)) throw new ArgumentException("inchiKey required", nameof(inchiKey));
            var casList = ResolveInChIKeyToCas(inchiKey);
            return UnionByCas(casList, category, temperatureRangeK, maxResults);
        }

        public IReadOnlyList<PureCompoundRecord> SearchByName(
            string name,
            PropertyCategory? category,
            (double Min, double Max)? temperatureRangeK,
            int maxResults)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name required", nameof(name));
            var casList = ResolveNameToCas(name);
            return UnionByCas(casList, category, temperatureRangeK, maxResults);
        }

        public PureCompoundRecord? GetById(string id)
        {
            var records = _db.GetCollection<RecordDoc>(SchemaMigrator.RecordsCollection);
            var d = records.FindOne(x => x.Id == id);
            return d == null ? null : Deserialize(d);
        }

        public IReadOnlyList<PureCompoundRecord> GetAllForCompound(string cas)
            => SearchByCas(cas, category: null, temperatureRangeK: null, maxResults: int.MaxValue);

        private IReadOnlyList<PureCompoundRecord> UnionByCas(
            IReadOnlyList<string> casList,
            PropertyCategory? category,
            (double Min, double Max)? temperatureRangeK,
            int maxResults)
        {
            if (casList.Count == 0) return Array.Empty<PureCompoundRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var aggregate = new List<PureCompoundRecord>();
            foreach (var cas in casList)
            {
                foreach (var rec in SearchByCas(cas, category, temperatureRangeK, maxResults))
                {
                    if (seen.Add(rec.Id)) aggregate.Add(rec);
                }
            }
            return aggregate
                .OrderByDescending(r => (r.Fits?.Count ?? 0) > 0 ? 1 : 0)
                .ThenByDescending(r => r.Points?.Count ?? 0)
                .ThenBy(r => r.Id, StringComparer.Ordinal)
                .Take(maxResults)
                .ToList();
        }

        private IReadOnlyList<string> ResolveInChIKeyToCas(string inchiKey)
        {
            var compounds = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);
            var needle = inchiKey.Trim();
            return compounds.FindAll()
                .Where(c => string.Equals(c.InChIKey ?? string.Empty, needle, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(c.Cas ?? string.Empty, needle, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Cas)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private IReadOnlyList<string> ResolveNameToCas(string name)
        {
            var compounds = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);
            var needle = name.Trim().ToLowerInvariant();
            var all = compounds.FindAll().ToList();
            var exact = all
                .Where(c => (c.CommonName ?? string.Empty).Trim().ToLowerInvariant() == needle
                         || (c.IupacName ?? string.Empty).Trim().ToLowerInvariant() == needle)
                .Select(c => c.Cas)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (exact.Count > 0) return exact;
            return all
                .Where(c => (c.CommonName ?? string.Empty).ToLowerInvariant().Contains(needle)
                         || (c.IupacName ?? string.Empty).ToLowerInvariant().Contains(needle))
                .Select(c => c.Cas)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static PureCompoundRecord Deserialize(RecordDoc d)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<PureCompoundRecord>(d.PayloadJson, CoreJson.Options)
                ?? throw new InvalidOperationException($"Failed to deserialize record {d.Id}.");
        }
    }
}
