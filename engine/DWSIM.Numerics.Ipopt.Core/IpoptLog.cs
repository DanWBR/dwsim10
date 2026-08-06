// Formats the iteration diagnostics like Ipopt's own output table, so a managed
// run can be compared column-by-column against the native solver's log.

using System;
using System.Globalization;

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>Ipopt-style iteration-table formatting.</summary>
    public static class IpoptLog
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Header() =>
            "iter    objective    inf_pr   inf_du lg(mu)  ||d||  lg(rg) alpha_du alpha_pr  ls";

        public static string Row(in IterationInfo it)
        {
            string lgmu = Math.Log10(Math.Max(it.Mu, 1e-300)).ToString("0.0", Inv);
            string lgrg = it.Regularization > 0.0
                ? Math.Log10(it.Regularization).ToString("0.0", Inv)
                : "  -  ";

            return string.Format(Inv,
                "{0,4}  {1,13} {2,8} {3,8} {4,6} {5,8} {6,6} {7,8} {8,8} {9,3}",
                it.Iter,
                it.Objective.ToString("0.0000000e+00", Inv),
                it.InfPr.ToString("0.00e+00", Inv),
                it.InfDu.ToString("0.00e+00", Inv),
                lgmu,
                it.DNorm.ToString("0.00e+00", Inv),
                lgrg,
                it.AlphaDu.ToString("0.00e+00", Inv),
                it.AlphaPr.ToString("0.00e+00", Inv),
                it.LsCount);
        }
    }
}
