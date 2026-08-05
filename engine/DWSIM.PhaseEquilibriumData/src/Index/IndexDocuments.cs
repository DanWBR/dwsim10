using System;
using System.Collections.Generic;

namespace DWSIM.PhaseEquilibriumData.Index
{
    internal sealed class DatasetDoc
    {
        public string Id { get; set; } = string.Empty;
        public string EquilibriumType { get; set; } = string.Empty;
        public string? Doi { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public int PointCount { get; set; }
        public double TMin { get; set; }
        public double TMax { get; set; }
        public double PMin { get; set; }
        public double PMax { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public string TextSearch { get; set; } = string.Empty;
        public DateTime IngestedUtc { get; set; }
    }

    internal sealed class DatasetComponentDoc
    {
        public string Key { get; set; } = string.Empty;
        public string DatasetId { get; set; } = string.Empty;
        public string Cas { get; set; } = string.Empty;
        public int ComponentIndex { get; set; }
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
}
