using System.Collections.Generic;

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>Termination status of the interior-point solve.</summary>
    public enum SolveStatus
    {
        /// <summary>Converged to the requested tolerance.</summary>
        Solved,
        /// <summary>Hit the iteration limit before converging.</summary>
        MaxIterations,
        /// <summary>The line search could not make progress.</summary>
        LineSearchFailure,
        /// <summary>The problem or options were invalid.</summary>
        InvalidInput,
        /// <summary>An iteration callback asked the solve to stop.</summary>
        UserRequested,
        /// <summary>The filter blocked every step and restoration could not reduce the violation.</summary>
        RestorationFailed
    }

    /// <summary>Barrier-parameter update strategy.</summary>
    public enum MuStrategy
    {
        /// <summary>Fiacco-McCormick monotone decrease (Ipopt mu_strategy=monotone).</summary>
        Monotone,
        /// <summary>Adaptive per-iteration mu from the LOQO/Mehrotra oracle (Ipopt mu_strategy=adaptive, mu_oracle=loqo). This is what DWSIM uses.</summary>
        Adaptive
    }

    /// <summary>Hessian approximation.</summary>
    public enum HessianApproximation
    {
        /// <summary>Full-memory damped BFGS.</summary>
        DenseBfgs,
        /// <summary>Limited-memory BFGS with a fixed history (Ipopt hessian_approximation=limited-memory). This is what DWSIM uses.</summary>
        LimitedMemoryBfgs
    }

    /// <summary>
    /// Options for <see cref="InteriorPointSolver"/>. Field defaults mirror Ipopt's own defaults
    /// so the managed solver tracks the native one as closely as this slice allows.
    /// </summary>
    public sealed class SolverOptions
    {
        /// <summary>Overall convergence tolerance on the scaled optimality error (Ipopt: tol=1e-8).</summary>
        public double Tolerance = 1e-8;

        /// <summary>Maximum iterations (Ipopt: max_iter=3000).</summary>
        public int MaxIterations = 3000;

        /// <summary>Initial barrier parameter (Ipopt: mu_init=0.1).</summary>
        public double MuInit = 0.1;

        /// <summary>Barrier strategy. Default <see cref="MuStrategy.Adaptive"/> to match DWSIM (mu_strategy=adaptive).</summary>
        public MuStrategy MuStrategy = MuStrategy.Adaptive;

        /// <summary>Hessian approximation. Default limited-memory BFGS to match DWSIM.</summary>
        public HessianApproximation HessianApproximation = HessianApproximation.LimitedMemoryBfgs;

        /// <summary>History length for limited-memory BFGS (Ipopt: limited_memory_max_history=6).</summary>
        public int LimitedMemoryMaxHistory = 6;

        /// <summary>Scaling cap s_max in the optimality error (Ipopt fixed value 100).</summary>
        public double SMax = 100.0;

        /// <summary>Subproblem tolerance factor kappa_eps (Ipopt: barrier_tol_factor=10).</summary>
        public double BarrierTolFactor = 10.0;

        /// <summary>Linear mu decrease kappa_mu (Ipopt: mu_linear_decrease_factor=0.2).</summary>
        public double MuLinearDecreaseFactor = 0.2;

        /// <summary>Superlinear mu decrease power theta_mu (Ipopt: mu_superlinear_decrease_power=1.5).</summary>
        public double MuSuperlinearDecreasePower = 1.5;

        /// <summary>Fraction-to-boundary floor tau_min (Ipopt: tau_min=0.99).</summary>
        public double TauMin = 0.99;

        /// <summary>Armijo constant eta_phi (Ipopt: eta_phi=1e-8 in the filter; 1e-4 is a common Armijo value).</summary>
        public double ArmijoEta = 1e-8;

        /// <summary>Absolute starting-point push (Ipopt: bound_push=0.01).</summary>
        public double BoundPush = 0.01;

        /// <summary>Relative starting-point push (Ipopt: bound_frac=0.01).</summary>
        public double BoundFrac = 0.01;

        /// <summary>Initial value for bound multipliers (Ipopt: bound_mult_init_val=1).</summary>
        public double BoundMultInit = 1.0;

        /// <summary>Bounds with magnitude at or above this are treated as infinite (Ipopt: nlp_*_bound_inf=1e19).</summary>
        public double BoundInf = 1e19;

        /// <summary>If set, an Ipopt-style iteration table is written here.</summary>
        public System.IO.TextWriter? LogWriter;

        /// <summary>If true, every iteration's <see cref="IterationInfo"/> is collected in the result.</summary>
        public bool CollectIterationLog = true;

        /// <summary>
        /// Called once per iteration. Returning false stops the solve with
        /// <see cref="SolveStatus.UserRequested"/>, which is what Ipopt's intermediate callback
        /// does and what DWSIM's wrapper uses to give up on a stalled objective.
        /// </summary>
        public System.Func<IterationInfo, bool>? IterationCallback;
    }

    /// <summary>Per-iteration diagnostics, mirroring the columns of Ipopt's iteration table.</summary>
    public readonly struct IterationInfo
    {
        public IterationInfo(int iter, double objective, double infPr, double infDu, double mu,
                             double dNorm, double regularization, double alphaDu, double alphaPr, int lsCount)
        {
            Iter = iter;
            Objective = objective;
            InfPr = infPr;
            InfDu = infDu;
            Mu = mu;
            DNorm = dNorm;
            Regularization = regularization;
            AlphaDu = alphaDu;
            AlphaPr = alphaPr;
            LsCount = lsCount;
        }

        public int Iter { get; }
        public double Objective { get; }
        public double InfPr { get; }          // primal infeasibility (theta); 0 when m = 0
        public double InfDu { get; }          // dual infeasibility (unscaled)
        public double Mu { get; }
        public double DNorm { get; }          // ||d_x||_inf search-direction norm
        public double Regularization { get; } // delta added to the diagonal, if any
        public double AlphaDu { get; }
        public double AlphaPr { get; }
        public int LsCount { get; }
    }

    /// <summary>Result of an interior-point solve.</summary>
    public sealed class SolveResult
    {
        public SolveStatus Status { get; init; }
        public double[] X { get; init; } = System.Array.Empty<double>();
        public double ObjValue { get; init; }
        public int Iterations { get; init; }
        public double OptimalityError { get; init; }
        public IReadOnlyList<IterationInfo> IterationLog { get; init; } = System.Array.Empty<IterationInfo>();
    }
}
