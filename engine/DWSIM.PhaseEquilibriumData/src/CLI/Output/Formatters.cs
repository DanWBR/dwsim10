using System;
using System.Collections.Generic;
using System.IO;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.CLI.Output
{
    internal enum OutputFormat { Table, Json, Csv }

    internal static class FormatterDispatch
    {
        public static OutputFormat Parse(string? format)
        {
            switch ((format ?? "table").ToLowerInvariant())
            {
                case "json": return OutputFormat.Json;
                case "csv":  return OutputFormat.Csv;
                case "table":
                case "":
                case null: return OutputFormat.Table;
                default: throw new ArgumentException($"Unknown format '{format}'.");
            }
        }

        public static void WriteDatasets(OutputFormat fmt, TextWriter w, IReadOnlyList<PhaseEquilibriumDataset> d)
        {
            switch (fmt)
            {
                case OutputFormat.Json: JsonFormatter.WriteDatasets(w, d); break;
                case OutputFormat.Csv:  CsvFormatter.WriteDatasets(w, d);  break;
                default:                TableFormatter.WriteDatasets(w, d); break;
            }
        }

        public static void WriteDataset(OutputFormat fmt, TextWriter w, PhaseEquilibriumDataset d)
        {
            switch (fmt)
            {
                case OutputFormat.Json: JsonFormatter.WriteDataset(w, d); break;
                case OutputFormat.Csv:  CsvFormatter.WriteDataset(w, d);  break;
                default:                TableFormatter.WriteDataset(w, d); break;
            }
        }
    }
}
