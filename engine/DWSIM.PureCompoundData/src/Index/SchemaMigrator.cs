using LiteDB;

namespace DWSIM.PureCompoundData.Index
{
    public static class SchemaMigrator
    {
        public const string RecordsCollection = "records";
        public const string CompoundsCollection = "compounds";
        public const string FitsCollection = "fits";

        public static void EnsureSchema(LiteDatabase db)
        {
            var records = db.GetCollection<RecordDoc>(RecordsCollection);
            records.EnsureIndex(x => x.Cas);
            records.EnsureIndex(x => x.Category);
            records.EnsureIndex(x => x.Property);
            records.EnsureIndex(x => x.TMin);
            records.EnsureIndex(x => x.TMax);
            records.EnsureIndex(x => x.Doi);
            records.EnsureIndex(x => x.Provider);
            records.EnsureIndex(x => x.TextSearch);

            var compounds = db.GetCollection<CompoundDoc>(CompoundsCollection);
            compounds.EnsureIndex(x => x.Cas, unique: true);
            compounds.EnsureIndex(x => x.InChIKey);

            var fits = db.GetCollection<FitDoc>(FitsCollection);
            fits.EnsureIndex(x => x.RecordId);
            fits.EnsureIndex(x => x.Cas);
            fits.EnsureIndex(x => x.Category);
            fits.EnsureIndex(x => x.EquationName);
        }
    }
}
