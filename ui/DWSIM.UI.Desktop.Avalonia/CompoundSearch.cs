using System;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Shared ranking for the compound search box, used by the simulation settings and the wizard so
/// both order matches from most to least similar to the query: an exact name first, then a
/// name-prefix, then a name-contains, then a match found only on the CAS number, formula or
/// database. Within a tier the caller breaks ties by the shorter (closer) name, so typing
/// "Methane" puts Methane at the very top.
/// </summary>
internal static class CompoundSearch
{
    public static bool Matches(string? name, string? cas, string? formula, string? database, string q)
    {
        return Has(name, q) || Has(cas, q) || Has(formula, q) || Has(database, q);
    }

    /// <summary>0 = exact name, 1 = name starts with, 2 = name contains, 3 = matched elsewhere.</summary>
    public static int Rank(string? name, string q)
    {
        var n = name ?? "";
        if (string.Equals(n, q, StringComparison.CurrentCultureIgnoreCase)) return 0;
        if (n.StartsWith(q, StringComparison.CurrentCultureIgnoreCase)) return 1;
        if (n.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0) return 2;
        return 3;
    }

    private static bool Has(string? s, string q)
        => (s ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
}
