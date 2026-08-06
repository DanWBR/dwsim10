// Managed NLP callback interface -- the DWSIM-scoped analogue of Ipopt's TNLP,
// for the bound-constrained (m = 0) case with a quasi-Newton Hessian (no eval_h).
// This mirrors how DWSIM already drives Ipopt (see DWSIM.Math/IPOPTSolver.vb):
// a callback for f and grad f, box bounds, and a starting point.

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>A bound-constrained NLP: minimize f(x) subject to xL &lt;= x &lt;= xU.</summary>
    public interface INlp
    {
        /// <summary>Number of variables.</summary>
        int N { get; }

        /// <summary>Fills xl/xu (length N) with the lower/upper bounds. Use +/-Inf (or |v| &gt;= 1e19) for none.</summary>
        void GetBounds(double[] xl, double[] xu);

        /// <summary>Fills x (length N) with the starting point.</summary>
        void GetStartingPoint(double[] x);

        /// <summary>Returns the objective value at x.</summary>
        double EvalF(double[] x);

        /// <summary>Fills gradf (length N) with the gradient of the objective at x.</summary>
        void EvalGradF(double[] x, double[] gradf);
    }
}
