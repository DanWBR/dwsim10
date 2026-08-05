using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Index
{
    public sealed class Ingestor
    {
        private const int BatchSize = 10_000;

        private readonly LiteDatabase _db;
        private readonly Func<DateTime> _now;

        internal Ingestor(LiteDatabase db, Func<DateTime>? clock = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _now = clock ?? (() => DateTime.UtcNow);
        }

        public IngestResult Ingest(IEnumerable<PhaseEquilibriumDataset> datasets)
        {
            var datasetsCol = _db.GetCollection<DatasetDoc>(SchemaMigrator.DatasetsCollection);
            var componentsCol = _db.GetCollection<DatasetComponentDoc>(SchemaMigrator.ComponentsCollection);
            var compoundsCol = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);

            var result = new IngestResult();
            var dsBuffer = new List<DatasetDoc>(BatchSize);
            var compBuffer = new List<DatasetComponentDoc>(BatchSize);
            var cmpBuffer = new Dictionary<string, CompoundDoc>(StringComparer.Ordinal);

            foreach (var ds in datasets)
            {
                result.Seen++;
                if (datasetsCol.Exists(x => x.Id == ds.Id))
                {
                    result.Skipped++;
                    continue;
                }

                var doc = ToDoc(ds, _now());
                dsBuffer.Add(doc);

                for (int i = 0; i < ds.Compounds.Count; i++)
                {
                    var c = ds.Compounds[i];
                    compBuffer.Add(new DatasetComponentDoc
                    {
                        Key = ds.Id + "|" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        DatasetId = ds.Id,
                        Cas = c.CasNumber,
                        ComponentIndex = i
                    });
                    if (!cmpBuffer.ContainsKey(c.CasNumber))
                    {
                        cmpBuffer[c.CasNumber] = new CompoundDoc
                        {
                            Cas = c.CasNumber,
                            CommonName = c.CommonName,
                            IupacName = c.IupacName,
                            Smiles = c.Smiles,
                            InChIKey = c.InChIKey,
                            MolecularFormula = c.MolecularFormula,
                            MolecularWeight = c.MolecularWeight
                        };
                    }
                }

                if (dsBuffer.Count >= BatchSize)
                {
                    Flush(datasetsCol, componentsCol, compoundsCol, dsBuffer, compBuffer, cmpBuffer, result);
                }
            }

            Flush(datasetsCol, componentsCol, compoundsCol, dsBuffer, compBuffer, cmpBuffer, result);
            _db.Checkpoint();
            return result;
        }

        private static void Flush(
            ILiteCollection<DatasetDoc> datasets,
            ILiteCollection<DatasetComponentDoc> components,
            ILiteCollection<CompoundDoc> compounds,
            List<DatasetDoc> dsBuffer,
            List<DatasetComponentDoc> compBuffer,
            Dictionary<string, CompoundDoc> cmpBuffer,
            IngestResult result)
        {
            if (dsBuffer.Count == 0 && compBuffer.Count == 0 && cmpBuffer.Count == 0) return;
            if (dsBuffer.Count > 0)
            {
                datasets.Insert(dsBuffer);
                result.Inserted += dsBuffer.Count;
                dsBuffer.Clear();
            }
            if (compBuffer.Count > 0)
            {
                components.Insert(compBuffer);
                compBuffer.Clear();
            }
            if (cmpBuffer.Count > 0)
            {
                foreach (var c in cmpBuffer.Values) compounds.Upsert(c.Cas, c);
                cmpBuffer.Clear();
            }
        }

        private static DatasetDoc ToDoc(PhaseEquilibriumDataset ds, DateTime now)
        {
            var (tmin, tmax) = TempRange(ds);
            var (pmin, pmax) = PresRange(ds);

            var textTokens = new List<string>();
            if (ds.Citation.Doi != null) textTokens.Add(ds.Citation.Doi);
            if (ds.Citation.Title != null) textTokens.Add(ds.Citation.Title);
            textTokens.AddRange(ds.Citation.Authors);
            textTokens.AddRange(ds.Compounds.Select(c => c.CasNumber));
            textTokens.AddRange(ds.Compounds.Select(c => c.CommonName));

            return new DatasetDoc
            {
                Id = ds.Id,
                EquilibriumType = ds.EquilibriumType.ToString(),
                Doi = ds.Citation.Doi,
                Method = ds.Method.ToString(),
                Provider = ds.SourceProvider,
                PointCount = ds.Points.Count,
                TMin = tmin,
                TMax = tmax,
                PMin = pmin,
                PMax = pmax,
                PayloadJson = CoreJson.SerializeDeterministic(ds),
                TextSearch = string.Join(" ", textTokens).ToLowerInvariant(),
                IngestedUtc = now
            };
        }

        private static (double min, double max) TempRange(PhaseEquilibriumDataset ds)
            => Range(ds, ConstraintKind.Temperature, name => name.IndexOf("temp", StringComparison.OrdinalIgnoreCase) >= 0);

        private static (double min, double max) PresRange(PhaseEquilibriumDataset ds)
            => Range(ds, ConstraintKind.Pressure, name => name.IndexOf("pressure", StringComparison.OrdinalIgnoreCase) >= 0);

        private static (double min, double max) Range(PhaseEquilibriumDataset ds, ConstraintKind kind, Func<string, bool> nameMatches)
        {
            double min = double.NaN, max = double.NaN;
            foreach (var c in ds.Constraints.Where(c => c.Kind == kind))
                Absorb(c.Value);
            foreach (var p in ds.Points)
                foreach (var kv in p.Values)
                    if (nameMatches(kv.Key)) Absorb(kv.Value);

            if (double.IsNaN(min)) return (0, 0);
            return (min, max);

            void Absorb(double v)
            {
                if (double.IsNaN(min) || v < min) min = v;
                if (double.IsNaN(max) || v > max) max = v;
            }
        }
    }

    public sealed class IngestResult
    {
        public int Seen { get; set; }
        public int Inserted { get; set; }
        public int Skipped { get; set; }
    }
}
