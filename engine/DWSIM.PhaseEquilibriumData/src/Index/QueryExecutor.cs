using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Index
{
    public sealed class QueryExecutor
    {
        private readonly LiteDatabase _db;

        internal QueryExecutor(LiteDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public IReadOnlyList<PhaseEquilibriumDataset> SearchBinary(
            string cas1,
            string cas2,
            EquilibriumType? typeFilter,
            (double Min, double Max)? temperatureRangeK,
            (double Min, double Max)? pressureRangeKPa,
            int maxResults)
        {
            if (string.IsNullOrWhiteSpace(cas1)) throw new ArgumentException("cas1 required", nameof(cas1));
            if (string.IsNullOrWhiteSpace(cas2)) throw new ArgumentException("cas2 required", nameof(cas2));
            if (maxResults <= 0) maxResults = 50;

            var components = _db.GetCollection<DatasetComponentDoc>(SchemaMigrator.ComponentsCollection);
            var ids1 = new HashSet<string>(components.Find(x => x.Cas == cas1).Select(x => x.DatasetId), StringComparer.Ordinal);
            var ids2 = new HashSet<string>(components.Find(x => x.Cas == cas2).Select(x => x.DatasetId), StringComparer.Ordinal);
            ids1.IntersectWith(ids2);
            if (ids1.Count == 0) return Array.Empty<PhaseEquilibriumDataset>();

            var datasets = _db.GetCollection<DatasetDoc>(SchemaMigrator.DatasetsCollection);
            var typeStr = typeFilter?.ToString();

            // Rank by PointCount DESC (high-quality sets first), Id ASC as the deterministic tiebreaker (AC-7).
            var candidates = datasets
                .Find(x => ids1.Contains(x.Id))
                .Where(d => typeStr == null || d.EquilibriumType == typeStr)
                .Where(d => !temperatureRangeK.HasValue || (d.TMax >= temperatureRangeK.Value.Min && d.TMin <= temperatureRangeK.Value.Max))
                .Where(d => !pressureRangeKPa.HasValue || (d.PMax >= pressureRangeKPa.Value.Min && d.PMin <= pressureRangeKPa.Value.Max))
                .OrderByDescending(d => d.PointCount)
                .ThenBy(d => d.Id, StringComparer.Ordinal)
                .Take(maxResults)
                .ToList();

            return candidates.Select(Deserialize).ToList();
        }

        /// <summary>
        /// Name-based binary search. Matches each input against <c>CompoundDoc.CommonName</c>
        /// (case-insensitive exact, then case-insensitive contains). Returns the union of all
        /// name-pair combinations matching both inputs. Suitable when the caller has compound
        /// names (e.g., DWSIM combo-box selections) but no CAS / InChIKey.
        /// </summary>
        public IReadOnlyList<PhaseEquilibriumDataset> SearchBinaryByNames(
            string name1,
            string name2,
            EquilibriumType? typeFilter,
            (double Min, double Max)? temperatureRangeK,
            (double Min, double Max)? pressureRangeKPa,
            int maxResults)
        {
            if (string.IsNullOrWhiteSpace(name1)) throw new ArgumentException("name1 required", nameof(name1));
            if (string.IsNullOrWhiteSpace(name2)) throw new ArgumentException("name2 required", nameof(name2));

            var keys1 = ResolveNameToKeys(name1);
            var keys2 = ResolveNameToKeys(name2);
            if (keys1.Count == 0 || keys2.Count == 0) return Array.Empty<PhaseEquilibriumDataset>();

            var aggregate = new List<PhaseEquilibriumDataset>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k1 in keys1)
            {
                foreach (var k2 in keys2)
                {
                    if (string.Equals(k1, k2, StringComparison.Ordinal)) continue;
                    foreach (var ds in SearchBinary(k1, k2, typeFilter, temperatureRangeK, pressureRangeKPa, maxResults))
                    {
                        if (seen.Add(ds.Id)) aggregate.Add(ds);
                    }
                }
            }
            return aggregate
                .OrderByDescending(d => d.Points.Count)
                .ThenBy(d => d.Id, StringComparer.Ordinal)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// InChIKey-based binary search. Exact, case-insensitive match against
        /// <c>CompoundDoc.InChIKey</c>; falls back to the primary key slot (<c>Cas</c>) when the JSON
        /// archive stored the InChIKey there. Prefer this over name-based matching when the caller
        /// has InChIKeys from their own compound database (e.g., DWSIM's <c>ConstantProperties</c>).
        /// </summary>
        public IReadOnlyList<PhaseEquilibriumDataset> SearchBinaryByInChIKey(
            string inchiKey1,
            string inchiKey2,
            EquilibriumType? typeFilter,
            (double Min, double Max)? temperatureRangeK,
            (double Min, double Max)? pressureRangeKPa,
            int maxResults)
        {
            if (string.IsNullOrWhiteSpace(inchiKey1)) throw new ArgumentException("inchiKey1 required", nameof(inchiKey1));
            if (string.IsNullOrWhiteSpace(inchiKey2)) throw new ArgumentException("inchiKey2 required", nameof(inchiKey2));

            var keys1 = ResolveInChIKeyToStorageKeys(inchiKey1);
            var keys2 = ResolveInChIKeyToStorageKeys(inchiKey2);
            if (keys1.Count == 0 || keys2.Count == 0) return Array.Empty<PhaseEquilibriumDataset>();

            return SearchBinaryKeyUnion(keys1, keys2, typeFilter, temperatureRangeK, pressureRangeKPa, maxResults);
        }

        private IReadOnlyList<PhaseEquilibriumDataset> SearchBinaryKeyUnion(
            IReadOnlyList<string> keys1,
            IReadOnlyList<string> keys2,
            EquilibriumType? typeFilter,
            (double Min, double Max)? temperatureRangeK,
            (double Min, double Max)? pressureRangeKPa,
            int maxResults)
        {
            var aggregate = new List<PhaseEquilibriumDataset>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k1 in keys1)
            {
                foreach (var k2 in keys2)
                {
                    if (string.Equals(k1, k2, StringComparison.Ordinal)) continue;
                    foreach (var ds in SearchBinary(k1, k2, typeFilter, temperatureRangeK, pressureRangeKPa, maxResults))
                    {
                        if (seen.Add(ds.Id)) aggregate.Add(ds);
                    }
                }
            }
            return aggregate
                .OrderByDescending(d => d.Points.Count)
                .ThenBy(d => d.Id, StringComparer.Ordinal)
                .Take(maxResults)
                .ToList();
        }

        private IReadOnlyList<string> ResolveInChIKeyToStorageKeys(string inchiKey)
        {
            var compounds = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);
            var needle = inchiKey.Trim();
            if (needle.Length == 0) return Array.Empty<string>();
            var all = compounds.FindAll().ToList();
            // Match either InChIKey field or the storage key slot (JSON archive stored InChIKey in Cas).
            return all.Where(c =>
                        string.Equals(c.InChIKey ?? string.Empty, needle, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Cas ?? string.Empty, needle, StringComparison.OrdinalIgnoreCase))
                      .Select(c => c.Cas)
                      .Distinct(StringComparer.Ordinal)
                      .ToList();
        }

        private IReadOnlyList<string> ResolveNameToKeys(string name)
        {
            var compounds = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);
            var needle = name.Trim().ToLowerInvariant();
            var all = compounds.FindAll().ToList();
            var exact = all.Where(c => (c.CommonName ?? string.Empty).Trim().ToLowerInvariant() == needle).Select(c => c.Cas).ToList();
            if (exact.Count > 0) return exact;
            return all.Where(c => (c.CommonName ?? string.Empty).ToLowerInvariant().Contains(needle))
                      .Select(c => c.Cas).ToList();
        }

        public PhaseEquilibriumDataset? GetById(string id)
        {
            var datasets = _db.GetCollection<DatasetDoc>(SchemaMigrator.DatasetsCollection);
            var d = datasets.FindOne(x => x.Id == id);
            return d == null ? null : Deserialize(d);
        }

        private static PhaseEquilibriumDataset Deserialize(DatasetDoc d)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<PhaseEquilibriumDataset>(d.PayloadJson, CoreJson.Options)
                ?? throw new InvalidOperationException($"Failed to deserialize dataset {d.Id}.");
        }
    }
}
