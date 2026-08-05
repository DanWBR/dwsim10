namespace DWSIM.PhaseEquilibriumData.Core
{
    public sealed record Constraint(
        ConstraintKind Kind,
        double Value,
        string Unit,
        int? ComponentIndex);
}
