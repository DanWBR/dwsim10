using System.Collections.Generic;
using System.IO;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.CLI.Output
{
    internal static class JsonFormatter
    {
        public static void WriteDatasets(TextWriter writer, IReadOnlyList<PhaseEquilibriumDataset> datasets)
            => writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(datasets, CoreJson.Options));

        public static void WriteDataset(TextWriter writer, PhaseEquilibriumDataset d)
            => writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(d, CoreJson.Options));
    }
}
