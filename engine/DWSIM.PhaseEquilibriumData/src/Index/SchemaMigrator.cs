using LiteDB;

namespace DWSIM.PhaseEquilibriumData.Index
{
    public static class SchemaMigrator
    {
        public const string DatasetsCollection = "datasets";
        public const string ComponentsCollection = "dataset_components";
        public const string CompoundsCollection = "compounds";

        public static void EnsureSchema(LiteDatabase db)
        {
            var datasets = db.GetCollection<DatasetDoc>(DatasetsCollection);
            datasets.EnsureIndex(x => x.EquilibriumType);
            datasets.EnsureIndex(x => x.TMin);
            datasets.EnsureIndex(x => x.TMax);
            datasets.EnsureIndex(x => x.PMin);
            datasets.EnsureIndex(x => x.PMax);
            datasets.EnsureIndex(x => x.Doi);
            datasets.EnsureIndex(x => x.TextSearch);

            var components = db.GetCollection<DatasetComponentDoc>(ComponentsCollection);
            components.EnsureIndex(x => x.Cas);
            components.EnsureIndex(x => x.DatasetId);

            var compounds = db.GetCollection<CompoundDoc>(CompoundsCollection);
            compounds.EnsureIndex(x => x.Cas, unique: true);
        }
    }
}
