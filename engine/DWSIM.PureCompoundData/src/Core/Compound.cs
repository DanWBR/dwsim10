namespace DWSIM.PureCompoundData.Core
{
    public sealed record Compound(
        string CasNumber,
        string CommonName,
        string? IupacName,
        string? Smiles,
        string? InChIKey,
        string? MolecularFormula,
        double? MolecularWeight);
}
