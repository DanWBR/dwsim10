// Drop-in managed replacement for DWSIM's IPOPTSolver.Solve, so a flowsheet
// optimization / Gibbs minimization / data regression can be pointed at the
// managed solver instead of the native Ipopt DLL with essentially no call-site
// change. Same delegate-based signature; defaults already mirror DWSIM's Ipopt
// options (mu_strategy=adaptive, hessian_approximation=limited-memory).

using System;
using System.IO;

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>
    /// Managed analogue of <c>DWSIM.Math.IPOPTSolver</c>: minimize a scalar objective
    /// (with constraints folded in as penalties) over box bounds, via the managed
    /// interior-point solver. Signature mirrors DWSIM's <c>Solve</c>.
    /// </summary>
    public sealed class DwsimIpoptSolver
    {
        /// <summary>Convergence tolerance (Ipopt "tol").</summary>
        public double Tolerance { get; set; } = 1e-8;

        /// <summary>Maximum iterations (Ipopt "max_iter").</summary>
        public int MaxIterations { get; set; } = 3000;

        /// <summary>Central-difference step used when no analytic gradient is supplied (DWSIM: 0.001).</summary>
        public double FiniteDifferenceStep { get; set; } = 1e-3;

        /// <summary>If set, the Ipopt-style iteration table is written here (for A/B against native).</summary>
        public TextWriter? LogWriter { get; set; }

        /// <summary>Diagnostics from the most recent <see cref="Solve"/> (status, iterations, log).</summary>
        public SolveResult? LastResult { get; private set; }

        /// <summary>
        /// Minimizes <paramref name="functionbody"/> starting from <paramref name="vars"/>,
        /// subject to the given bounds, and returns the solution vector.
        /// </summary>
        /// <param name="functionbody">Scalar objective f(x).</param>
        /// <param name="functiongradient">Optional analytic gradient; central differences if null.</param>
        /// <param name="vars">Initial variable values.</param>
        /// <param name="lbounds">Lower bounds, or null for none.</param>
        /// <param name="ubounds">Upper bounds, or null for none.</param>
        public double[] Solve(
            Func<double[], double> functionbody,
            Func<double[], double[]>? functiongradient,
            double[] vars,
            double[]? lbounds = null,
            double[]? ubounds = null)
        {
            var nlp = new DwsimFunctionNlp(functionbody, functiongradient, vars, lbounds, ubounds, FiniteDifferenceStep);

            var options = new SolverOptions
            {
                Tolerance = Tolerance,
                MaxIterations = MaxIterations,
                LogWriter = LogWriter,
                // Defaults already match DWSIM: MuStrategy.Adaptive, HessianApproximation.LimitedMemoryBfgs.
            };

            var result = new InteriorPointSolver(options).Solve(nlp);
            LastResult = result;
            return result.X;
        }
    }
}
