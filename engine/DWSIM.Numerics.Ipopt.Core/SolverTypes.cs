using System.Collections.Generic;

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>Termination status of the interior-point solve.</summary>
    public enum SolveStatus
    {
        /// <summary>Converged to the requested tolerance.</summary>
        Solved,
        /// <summary>
        /// Stopped at a point that is good enough for long enough: the optimality error stayed
        /// below <see cref="SolverOptions.AcceptableTolerance"/> for
        /// <see cref="SolverOptions.AcceptableIterations"/> iterations in a row without reaching
        /// the tolerance asked for. Ipopt's Solved_To_Acceptable_Level, and callers treat it as an
        /// answer.
        /// </summary>
        SolvedToAcceptableLevel,
        /// <summary>Hit the iteration limit before converging.</summary>
        MaxIterations,
        /// <summary>The line search could not make progress.</summary>
        LineSearchFailure,
        /// <summary>The problem or options were invalid.</summary>
        InvalidInput,
        /// <summary>An iteration callback asked the solve to stop.</summary>
        UserRequested,
        /// <summary>The filter blocked every step and restoration could not reduce the violation.</summary>
        RestorationFailed,
        /// <summary>
        /// The objective or the point went to NaN or infinity, which means the problem was
        /// evaluated somewhere it is not defined. Ipopt's Invalid_Number_Detected: callers treat
        /// it as a failure and fall back, and they must, because there is no answer here.
        /// </summary>
        InvalidNumber
    }

    /// <summary>
    /// The check both solvers run before handing an answer back. Ipopt calls it
    /// Invalid_Number_Detected and it is not a formality: an objective built from a logarithm or a
    /// square root is undefined in part of the box a caller declares, and a solver that reports
    /// NaN under a success code hands the caller a wrong answer it has no way to spot.
    /// </summary>
    internal static class NumberCheck
    {
        public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        public static SolveStatus Verify(SolveStatus status, double[] x, double f)
        {
            if (!IsFinite(f)) return SolveStatus.InvalidNumber;

            for (int i = 0; i < x.Length; i++)
            {
                if (!IsFinite(x[i])) return SolveStatus.InvalidNumber;
            }

            return status;
        }
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
        LimitedMemoryBfgs,
        /// <summary>
        /// Second derivatives from the problem itself (Ipopt hessian_approximation=exact), which
        /// requires it to implement <see cref="INlpHessian"/>. Falls back to limited-memory BFGS
        /// on any iteration where the problem declines to supply one.
        /// </summary>
        Exact
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

        /// <summary>
        /// Optimality error that counts as good enough when the requested tolerance cannot be
        /// reached (Ipopt: acceptable_tol=1e-6).
        /// <para>
        /// Callers ask for tolerances they will never get - the Gibbs reactor asks for 1e-20 - and
        /// without this the solve runs to its iteration cap, spending hundreds of objective
        /// evaluations wandering the floor of a minimum it found long before. Measured on the
        /// Gibbs reactor of the sample flowsheet: the optimality error is 7.6e-9 and the solver
        /// still takes 500 iterations.
        /// </para>
        /// </summary>
        public double AcceptableTolerance = 1e-6;

        /// <summary>
        /// How many iterations in a row have to meet <see cref="AcceptableTolerance"/> before the
        /// solve stops there (Ipopt: acceptable_iter=15). Zero switches the test off.
        /// </summary>
        public int AcceptableIterations = 15;

        /// <summary>
        /// Constraint violation that counts as good enough alongside
        /// <see cref="AcceptableTolerance"/> (Ipopt: acceptable_constr_viol_tol=1e-2). Ignored
        /// when there are no constraints.
        /// </summary>
        public double AcceptableConstraintViolation = 1e-2;

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
        /// Called once per iteration that reached a new point. Returning false stops the solve with
        /// <see cref="SolveStatus.UserRequested"/>, which is what Ipopt's intermediate callback
        /// does and what DWSIM's wrapper uses to give up on a stalled objective.
        /// <para>
        /// Iterations flagged <see cref="IterationInfo.Restoration"/> are skipped, because they
        /// report the point the previous call already reported. A caller that watches the
        /// objective for a stall - which is what the Gibbs flash does, on a threshold of 1e-10 -
        /// would otherwise read a repeat as convergence and end the solve early. Ipopt has the
        /// same problem and answers it the same way, by telling the caller which mode the
        /// iteration was in; the log keeps every row either way.
        /// </para>
        /// </summary>
        public System.Func<IterationInfo, bool>? IterationCallback;
    }

    /// <summary>Per-iteration diagnostics, mirroring the columns of Ipopt's iteration table.</summary>
    public readonly struct IterationInfo
    {
        public IterationInfo(int iter, double objective, double infPr, double infDu, double mu,
                             double dNorm, double regularization, double alphaDu, double alphaPr, int lsCount,
                             bool restoration = false)
        {
            Restoration = restoration;
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

        /// <summary>
        /// True when the iteration took no step: the line search rejected every trial point and
        /// the iteration was spent recovering rather than moving. The point is the one the
        /// previous row already reported, which is why these are not handed to the caller's
        /// iteration callback (see the note on <see cref="SolverOptions.IterationCallback"/>).
        /// </summary>
        public bool Restoration { get; }
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
