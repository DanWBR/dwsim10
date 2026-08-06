// A constrained NLP, the m > 0 case: the Gibbs three-phase flash is the one caller in the engine
// that poses one. It stays a separate interface from INlp so that nothing in the bound-constrained
// path has to know about constraints, and so that a caller with none pays nothing.

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>
    /// An NLP with general constraints: minimize f(x) subject to cl &lt;= g(x) &lt;= cu and
    /// xl &lt;= x &lt;= xu. A constraint whose lower and upper bound are equal is an equality.
    /// </summary>
    public interface INlpConstrained : INlp
    {
        /// <summary>Number of constraints.</summary>
        int M { get; }

        /// <summary>Fills cl/cu (length M) with the constraint bounds. Use +/-Inf (or |v| &gt;= 1e19) for none.</summary>
        void GetConstraintBounds(double[] cl, double[] cu);

        /// <summary>Fills g (length M) with the constraint values at x.</summary>
        void EvalG(double[] x, double[] g);

        /// <summary>
        /// Fills jac (M by N, row major: jac[i * N + j] is dg_i/dx_j) with the Jacobian at x.
        /// Dense because the one caller's Jacobian has two entries per row and the problem is
        /// small: sparsity here would buy nothing and cost a structure to keep in step.
        /// </summary>
        void EvalJacG(double[] x, double[] jac);
    }
}
