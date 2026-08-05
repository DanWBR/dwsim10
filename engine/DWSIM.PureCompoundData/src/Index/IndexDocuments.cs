using System;

namespace DWSIM.PureCompoundData.Index
{
    internal sealed class RecordDoc
    {
        public string Id { get; set; } = string.Empty;
        public string Cas { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public string? Doi { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public int PointCount { get; set; }
        public int FitCount { get; set; }
        public double TMin { get; set; }
        public double TMax { get; set; }
        public double? ScalarValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string TextSearch { get; set; } = string.Empty;
        public DateTime IngestedUtc { get; set; }
    }

    internal sealed class CompoundDoc
    {
        public string Cas { get; set; } = string.Empty;
        public string? CommonName { get; set; }
        public string? IupacName { get; set; }
        public string? Smiles { get; set; }
        public string? InChIKey { get; set; }
        public string? MolecularFormula { get; set; }
        public double? MolecularWeight { get; set; }
    }

    internal sealed class FitDoc
    {
        public string Key { get; set; } = string.Empty;          // RecordId|FitIndex
        public string RecordId { get; set; } = string.Empty;
        public string Cas { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string EquationName { get; set; } = string.Empty;
        public int DwsimEquationNumber { get; set; }
        public double TMin { get; set; }
        public double TMax { get; set; }
        public double? Aard { get; set; }
    }
}
