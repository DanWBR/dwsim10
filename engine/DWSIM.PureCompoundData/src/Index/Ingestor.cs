using System;
using System.Collections.Generic;
using System.Globalization;
using LiteDB;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Index
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

        public IngestResult Ingest(IEnumerable<PureCompoundRecord> records)
        {
            var recordsCol = _db.GetCollection<RecordDoc>(SchemaMigrator.RecordsCollection);
            var compoundsCol = _db.GetCollection<CompoundDoc>(SchemaMigrator.CompoundsCollection);
            var fitsCol = _db.GetCollection<FitDoc>(SchemaMigrator.FitsCollection);

            var result = new IngestResult();
            var recBuf = new List<RecordDoc>(BatchSize);
            var fitBuf = new List<FitDoc>(BatchSize);
            var cmpBuf = new Dictionary<string, CompoundDoc>(StringComparer.Ordinal);

            foreach (var r in records)
            {
                result.Seen++;
                if (recordsCol.Exists(x => x.Id == r.Id))
                {
                    result.Skipped++;
                    continue;
                }

                recBuf.Add(ToDoc(r, _now()));

                if (r.Fits != null)
                {
                    for (int i = 0; i < r.Fits.Count; i++)
                    {
                        var f = r.Fits[i];
                        fitBuf.Add(new FitDoc
                        {
                            Key = r.Id + "|" + i.ToString(CultureInfo.InvariantCulture),
                            RecordId = r.Id,
                            Cas = r.Compound.CasNumber,
                            Category = r.Category.ToString(),
                            EquationName = f.EquationName,
                            DwsimEquationNumber = f.DwsimEquationNumber,
                            TMin = f.TMin,
                            TMax = f.TMax,
                            Aard = f.AARD
                        });
                    }
                }

                if (!cmpBuf.ContainsKey(r.Compound.CasNumber))
                {
                    cmpBuf[r.Compound.CasNumber] = new CompoundDoc
                    {
                        Cas = r.Compound.CasNumber,
                        CommonName = r.Compound.CommonName,
                        IupacName = r.Compound.IupacName,
                        Smiles = r.Compound.Smiles,
                        InChIKey = r.Compound.InChIKey,
                        MolecularFormula = r.Compound.MolecularFormula,
                        MolecularWeight = r.Compound.MolecularWeight
                    };
                }

                if (recBuf.Count >= BatchSize)
                    Flush(recordsCol, compoundsCol, fitsCol, recBuf, fitBuf, cmpBuf, result);
            }

            Flush(recordsCol, compoundsCol, fitsCol, recBuf, fitBuf, cmpBuf, result);
            _db.Checkpoint();
            return result;
        }

        private static void Flush(
            ILiteCollection<RecordDoc> records,
            ILiteCollection<CompoundDoc> compounds,
            ILiteCollection<FitDoc> fits,
            List<RecordDoc> recBuf,
            List<FitDoc> fitBuf,
            Dictionary<string, CompoundDoc> cmpBuf,
            IngestResult result)
        {
            if (recBuf.Count > 0)
            {
                records.Insert(recBuf);
                result.Inserted += recBuf.Count;
                recBuf.Clear();
            }
            if (fitBuf.Count > 0)
            {
                fits.Insert(fitBuf);
                fitBuf.Clear();
            }
            if (cmpBuf.Count > 0)
            {
                foreach (var c in cmpBuf.Values) compounds.Upsert(c.Cas, c);
                cmpBuf.Clear();
            }
        }

        private static RecordDoc ToDoc(PureCompoundRecord r, DateTime now)
        {
            var tokens = new List<string>
            {
                r.Compound.CasNumber,
                r.Compound.CommonName,
                r.Property,
                r.SourceProvider
            };
            if (r.Citation.Doi != null) tokens.Add(r.Citation.Doi);
            if (r.Citation.Title != null) tokens.Add(r.Citation.Title);
            if (r.Compound.InChIKey != null) tokens.Add(r.Compound.InChIKey);
            tokens.AddRange(r.Citation.Authors);

            return new RecordDoc
            {
                Id = r.Id,
                Cas = r.Compound.CasNumber,
                Category = r.Category.ToString(),
                Property = r.Property,
                Doi = r.Citation.Doi,
                Method = r.Method.ToString(),
                Provider = r.SourceProvider,
                PointCount = r.Points?.Count ?? 0,
                FitCount = r.Fits?.Count ?? 0,
                TMin = r.TMin ?? 0,
                TMax = r.TMax ?? 0,
                ScalarValue = r.ScalarValue,
                Unit = r.Unit,
                PayloadJson = CoreJson.SerializeDeterministic(r),
                TextSearch = string.Join(" ", tokens).ToLowerInvariant(),
                IngestedUtc = now
            };
        }
    }

    public sealed class IngestResult
    {
        public int Seen { get; set; }
        public int Inserted { get; set; }
        public int Skipped { get; set; }
    }
}
