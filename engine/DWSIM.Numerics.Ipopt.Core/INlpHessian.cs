// An NLP that can hand over the second derivatives of its Lagrangian, which is Ipopt's
// hessian_approximation=exact. Optional, and separate from INlp/INlpConstrained, so that a caller
// with nothing but f and grad f keeps the interface it already implements.
//
// Nothing in the engine declared a usable Hessian when this was written: every eval_h in the tree
// is handed to a constructor with nele_hess = 0, so the native library never called it. The
// capability exists here because the quasi-Newton matrix is an approximation and a caller that has
// the real thing should be able to say so.

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>Second derivatives of the Lagrangian, for <see cref="HessianApproximation.Exact"/>.</summary>
    public interface INlpHessian
    {
        /// <summary>
        /// Fills w (N by N, row major: w[i * N + j] is d2L/dx_i dx_j) with
        /// <c>objFactor * grad^2 f(x) + sum_r lambda[r] * grad^2 g_r(x)</c>.
        /// <para>
        /// Returns false when the Hessian is not available at this point; the solver then uses its
        /// quasi-Newton matrix for that iteration, so a caller may refuse whenever it likes.
        /// </para>
        /// <para>
        /// The matrix may be indefinite. That is expected, and is what the inertia correction of
        /// the augmented system is for: a caller must not symmetrise away curvature to make it
        /// look positive definite.
        /// </para>
        /// </summary>
        bool TryEvalHessian(double[] x, double objFactor, double[] lambda, double[] w);
    }
}
