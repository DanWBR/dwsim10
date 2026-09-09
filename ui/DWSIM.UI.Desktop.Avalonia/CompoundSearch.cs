using System;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Shared ranking for the compound search box, used by the simulation settings and the wizard so
/// both order matches from most to least similar to the query. The rank takes the best match across
/// the name, formula and CAS number: an exact match first, then a prefix, then a contains, then a
/// match found only on the database. Within a tier the caller breaks ties by the shorter (closer)
/// name, so typing "Methane" puts Methane at the top and typing "CO2" puts Carbon dioxide first.
/// </summary>
internal static class CompoundSearch
{
    public static bool Matches(string? name, string? cas, string? formula, string? database, string q)
    {
        return Has(name, q) || Has(cas, q) || Has(formula, q) || Has(database, q);
    }

    /// <summary>
    /// 0 = exact, 1 = starts with, 2 = contains, 3 = matched only on the database. The best (lowest)
    /// tier across the name, formula and CAS number wins, so typing a formula like "CO2" ranks Carbon
    /// dioxide (an exact formula match) above a compound whose formula merely contains it.
    /// </summary>
    public static int Rank(string? name, string? cas, string? formula, string q)
        => Math.Min(FieldRank(name, q), Math.Min(FieldRank(formula, q), FieldRank(cas, q)));

    private static int FieldRank(string? s, string q)
    {
        var v = s ?? "";
        if (string.Equals(v, q, StringComparison.CurrentCultureIgnoreCase)) return 0;
        if (v.StartsWith(q, StringComparison.CurrentCultureIgnoreCase)) return 1;
        if (v.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0) return 2;
        return 3;
    }

    private static bool Has(string? s, string q)
        => (s ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
}
