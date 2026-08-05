namespace DWSIM.PureCompoundData.Fragmentation
{
    /// A single group definition sourced from the ugropy CSVs:
    /// <c>group|smarts|molecular_weight</c>.
    public sealed class GroupDefinition
    {
        public string Name { get; }
        public string Smarts { get; }
        public double MolecularWeight { get; }

        public GroupDefinition(string name, string smarts, double mw)
        {
            Name = name;
            Smarts = smarts;
            MolecularWeight = mw;
        }
    }
}
