//    The IPOPT surface the engine calls, over the managed solver.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Globalization;
using System.IO;
using Core = DWSIM.Numerics.Ipopt.Core;

namespace Cureos.Numerics
{
    /// <summary>
    /// The interior point solver the flash algorithms, the binary interaction parameter
    /// regression and the flowsheet optimiser reach for. On .NET Framework this was
    /// Cureos.Numerics, a wrapper over the native IPOPT, which has no arm64 build; the shape is
    /// kept exactly so no caller has to change. See docs/ipopt-contract.md.
    ///
    /// Bound-constrained problems are solved here, by DWSIM.Numerics.Ipopt.Core. Problems with
    /// constraints are not: the only caller that poses any is the Gibbs three-phase flash, and
    /// it says so rather than answering wrongly.
    /// </summary>
    public class Ipopt : IDisposable
    {
        public const double PositiveInfinity = 2e19;
        public const double NegativeInfinity = -2e19;

        private readonly int _n;
        private readonly int _m;
        private readonly double[] _xL;
        private readonly double[] _xU;
        private readonly double[] _cL;
        private readonly double[] _cU;
        private readonly EvaluateObjectiveDelegate _evalF;
        private readonly EvaluateObjectiveGradientDelegate _evalGradF;
        private readonly EvaluateConstraintsDelegate _evalG;
        private readonly EvaluateJacobianDelegate _evalJacG;
        private readonly int _neleJac;
        private readonly Core.SolverOptions _options = new Core.SolverOptions { LogWriter = null };

        private IntermediateDelegate _intermediate;
        private StreamWriter _outputFile;
        private double _objScaling = 1.0;

        public Ipopt(int n, double[] x_L, double[] x_U,
                     int m, double[] g_L, double[] g_U,
                     int nele_jac, int nele_hess,
                     EvaluateObjectiveDelegate eval_f,
                     EvaluateConstraintsDelegate eval_g,
                     EvaluateObjectiveGradientDelegate eval_grad_f,
                     EvaluateJacobianDelegate eval_jac_g,
                     EvaluateHessianDelegate eval_h)
        {
            _n = n;
            _m = m;
            _xL = x_L;
            _xU = x_U;
            _cL = g_L;
            _cU = g_U;
            _neleJac = nele_jac;
            _evalF = eval_f;
            _evalGradF = eval_grad_f;
            _evalG = eval_g;
            _evalJacG = eval_jac_g;
        }

        /// <summary>
        /// Accepts the options the engine sets. Anything else is accepted and ignored, which is
        /// what the native wrapper did with an option the linked build did not know.
        /// </summary>
        public bool AddOption(string keyword, string val)
        {
            if (keyword == null || val == null) return false;

            switch (keyword)
            {
                case "mu_strategy":
                    _options.MuStrategy = val == "adaptive"
                        ? Core.MuStrategy.Adaptive
                        : Core.MuStrategy.Monotone;
                    return true;

                case "hessian_approximation":
                    _options.HessianApproximation = val == "limited-memory"
                        ? Core.HessianApproximation.LimitedMemoryBfgs
                        : Core.HessianApproximation.DenseBfgs;
                    return true;

                default:
                    return true;
            }
        }

        public bool AddOption(string keyword, double val)
        {
            if (keyword == null) return false;

            switch (keyword)
            {
                case "tol":
                    _options.Tolerance = val;
                    return true;
                case "mu_init":
                    _options.MuInit = val;
                    return true;
                case "bound_push":
                    _options.BoundPush = val;
                    return true;
                case "bound_frac":
                    _options.BoundFrac = val;
                    return true;
                default:
                    return true;
            }
        }

        public bool AddOption(string keyword, int val)
        {
            if (keyword == null) return false;

            switch (keyword)
            {
                case "max_iter":
                    _options.MaxIterations = val;
                    return true;
                case "limited_memory_max_history":
                    _options.LimitedMemoryMaxHistory = val;
                    return true;
                case "print_level":
                    // Zero means silent, which is what every caller in the engine asks for. A
                    // higher level only matters once an output file has been opened.
                    if (val <= 0) _options.LogWriter = null;
                    return true;
                default:
                    return true;
            }
        }

        public bool OpenOutputFile(string file_name, int print_level)
        {
            if (string.IsNullOrEmpty(file_name)) return false;

            try
            {
                _outputFile = new StreamWriter(file_name, append: false) { AutoFlush = true };
                _options.LogWriter = print_level > 0 ? _outputFile : null;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Objective scaling is honoured; variable and constraint scaling are not, and no caller
        /// in the engine passes them.
        /// </summary>
        public bool SetScaling(double obj_scaling, double[] x_scaling, double[] g_scaling)
        {
            if (obj_scaling == 0.0) return false;
            _objScaling = obj_scaling;
            return x_scaling == null && g_scaling == null;
        }

        public bool SetIntermediateCallback(IntermediateDelegate intermediate)
        {
            _intermediate = intermediate;
            return true;
        }

        public IpoptReturnCode SolveProblem(double[] x, ref double obj_val, double[] g,
                                            double[] mult_g, double[] mult_x_L, double[] mult_x_U)
        {
            if (x == null || x.Length < _n) return IpoptReturnCode.Invalid_Problem_Definition;
            if (_evalF == null) return IpoptReturnCode.Invalid_Problem_Definition;

            if (_m > 0 && (_evalG == null || _evalJacG == null))
            {
                return IpoptReturnCode.Invalid_Problem_Definition;
            }

            if (_intermediate != null)
            {
                var callback = _intermediate;
                _options.IterationCallback = info => callback(
                    IpoptAlgorithmMode.RegularMode, info.Iter, info.Objective,
                    info.InfPr, info.InfDu, info.Mu, info.DNorm,
                    info.Regularization, info.AlphaDu, info.AlphaPr, info.LsCount);
            }

            Core.SolveResult result;

            try
            {
                if (_m > 0)
                {
                    var constrained = new FacadeConstrainedNlp(
                        _n, _m, _neleJac, _xL, _xU, _cL, _cU, x,
                        _evalF, _evalGradF, _evalG, _evalJacG, _objScaling);

                    result = new Core.ConstrainedInteriorPointSolver(_options).Solve(constrained);
                }
                else
                {
                    var nlp = new FacadeNlp(_n, _xL, _xU, x, _evalF, _evalGradF, _objScaling);

                    result = new Core.InteriorPointSolver(_options).Solve(nlp);
                }
            }
            catch (EvaluationFailedException)
            {
                return IpoptReturnCode.Invalid_Number_Detected;
            }

            // The caller reads its answer out of the array it passed in, the way the native
            // wrapper filled it.
            Array.Copy(result.X, x, _n);
            obj_val = result.ObjValue / _objScaling;

            // The constraint values at the answer, when the caller wanted them. The multipliers
            // are not part of this slice; every caller in the engine passes null for all three.
            if (g != null)
            {
                Array.Clear(g, 0, g.Length);

                if (_m > 0 && g.Length >= _m)
                {
                    var values = new double[_m];
                    if (_evalG(_n, x, true, _m, ref values)) Array.Copy(values, g, _m);
                }
            }

            if (mult_g != null) Array.Clear(mult_g, 0, mult_g.Length);
            if (mult_x_L != null) Array.Clear(mult_x_L, 0, mult_x_L.Length);
            if (mult_x_U != null) Array.Clear(mult_x_U, 0, mult_x_U.Length);

            switch (result.Status)
            {
                case Core.SolveStatus.Solved:
                    return IpoptReturnCode.Solve_Succeeded;
                case Core.SolveStatus.MaxIterations:
                    return IpoptReturnCode.Maximum_Iterations_Exceeded;
                case Core.SolveStatus.LineSearchFailure:
                    // Not Search_Direction_Becomes_Too_Small, which every caller in the engine
                    // treats as a usable answer: a line search that gave up is a point that was
                    // never converged, and handing it back as an answer is how a flash ends up
                    // reporting a phase split it did not compute.
                    return IpoptReturnCode.Error_In_Step_Computation;
                case Core.SolveStatus.UserRequested:
                    return IpoptReturnCode.User_Requested_Stop;
                default:
                    return IpoptReturnCode.Invalid_Problem_Definition;
            }
        }

        public void Dispose()
        {
            if (_outputFile != null)
            {
                _outputFile.Dispose();
                _outputFile = null;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The constrained shape, over the same delegates. The Jacobian arrives in Ipopt's
        /// triplet form: one call with a null value array asks for the structure, and every call
        /// after that fills the values in that order. The solver wants it dense, so the triplets
        /// are scattered into a row-major block, which for the one caller that poses constraints
        /// is a few dozen entries.
        /// </summary>
        private sealed class FacadeConstrainedNlp : FacadeNlp, Core.INlpConstrained
        {
            private readonly int _m;
            private readonly int _neleJac;
            private readonly double[] _cL;
            private readonly double[] _cU;
            private readonly EvaluateConstraintsDelegate _evalG;
            private readonly EvaluateJacobianDelegate _evalJacG;
            private readonly int[] _iRow;
            private readonly int[] _jCol;

            public FacadeConstrainedNlp(int n, int m, int neleJac,
                                        double[] xL, double[] xU, double[] cL, double[] cU, double[] x0,
                                        EvaluateObjectiveDelegate evalF,
                                        EvaluateObjectiveGradientDelegate evalGradF,
                                        EvaluateConstraintsDelegate evalG,
                                        EvaluateJacobianDelegate evalJacG,
                                        double objScaling)
                : base(n, xL, xU, x0, evalF, evalGradF, objScaling)
            {
                _m = m;
                _neleJac = neleJac;
                _cL = cL;
                _cU = cU;
                _evalG = evalG;
                _evalJacG = evalJacG;

                _iRow = new int[neleJac];
                _jCol = new int[neleJac];

                int[] rows = _iRow;
                int[] cols = _jCol;
                double[] none = null!;

                if (!_evalJacG(n, x0, true, m, neleJac, ref rows, ref cols, ref none))
                {
                    throw new EvaluationFailedException();
                }

                Array.Copy(rows, _iRow, neleJac);
                Array.Copy(cols, _jCol, neleJac);
            }

            public int M => _m;

            public void GetConstraintBounds(double[] cl, double[] cu)
            {
                for (int i = 0; i < _m; i++)
                {
                    cl[i] = _cL != null && i < _cL.Length ? _cL[i] : NegativeInfinity;
                    cu[i] = _cU != null && i < _cU.Length ? _cU[i] : PositiveInfinity;
                }
            }

            public void EvalG(double[] x, double[] g)
            {
                var buffer = g;

                if (!_evalG(N, x, true, _m, ref buffer))
                {
                    throw new EvaluationFailedException();
                }

                if (!ReferenceEquals(buffer, g)) Array.Copy(buffer, g, _m);
            }

            public void EvalJacG(double[] x, double[] jac)
            {
                var rows = _iRow;
                var cols = _jCol;
                var values = new double[_neleJac];

                if (!_evalJacG(N, x, true, _m, _neleJac, ref rows, ref cols, ref values))
                {
                    throw new EvaluationFailedException();
                }

                Array.Clear(jac, 0, jac.Length);

                for (int k = 0; k < _neleJac; k++)
                {
                    // Duplicate triplets add up, the way every sparse format defines them.
                    jac[rows[k] * N + cols[k]] += values[k];
                }
            }
        }

        /// <summary>Raised when a caller's eval_f or eval_grad_f reports failure.</summary>
        private sealed class EvaluationFailedException : Exception
        {
        }

        /// <summary>
        /// Presents the engine's delegates to the managed solver. The delegates return false to
        /// mean "could not evaluate here", which the interior point solver has no notion of, so
        /// it travels as an exception and comes back as Invalid_Number_Detected.
        /// </summary>
        private class FacadeNlp : Core.INlp
        {
            private readonly int _n;
            private readonly double[] _xL;
            private readonly double[] _xU;
            private readonly double[] _x0;
            private readonly EvaluateObjectiveDelegate _evalF;
            private readonly EvaluateObjectiveGradientDelegate _evalGradF;
            private readonly double _objScaling;
            private readonly double[] _last;
            private bool _seen;

            public FacadeNlp(int n, double[] xL, double[] xU, double[] x0,
                             EvaluateObjectiveDelegate evalF,
                             EvaluateObjectiveGradientDelegate evalGradF,
                             double objScaling)
            {
                _n = n;
                _xL = xL;
                _xU = xU;
                _x0 = (double[])x0.Clone();
                _evalF = evalF;
                _evalGradF = evalGradF;
                _objScaling = objScaling;
                _last = new double[n];
            }

            public int N => _n;

            protected int Count => _n;

            public void GetBounds(double[] xl, double[] xu)
            {
                for (int i = 0; i < _n; i++)
                {
                    xl[i] = _xL != null && i < _xL.Length ? _xL[i] : NegativeInfinity;
                    xu[i] = _xU != null && i < _xU.Length ? _xU[i] : PositiveInfinity;
                }
            }

            public void GetStartingPoint(double[] x)
            {
                Array.Copy(_x0, x, _n);
            }

            public double EvalF(double[] x)
            {
                double value = 0.0;

                if (!_evalF(_n, x, IsNew(x), ref value))
                {
                    throw new EvaluationFailedException();
                }

                return _objScaling * value;
            }

            public void EvalGradF(double[] x, double[] gradf)
            {
                if (_evalGradF == null)
                {
                    CentralDifferences(x, gradf);
                    return;
                }

                var buffer = gradf;

                if (!_evalGradF(_n, x, IsNew(x), ref buffer))
                {
                    throw new EvaluationFailedException();
                }

                // The delegate takes its buffer by reference and may hand back a different array.
                if (!ReferenceEquals(buffer, gradf)) Array.Copy(buffer, gradf, _n);

                if (_objScaling != 1.0)
                {
                    for (int i = 0; i < _n; i++) gradf[i] *= _objScaling;
                }
            }

            /// <summary>
            /// The fallback DWSIM's own wrapper uses when no gradient is supplied: central
            /// differences with a relative step of 0.001, absolute where the variable is zero.
            /// </summary>
            private void CentralDifferences(double[] x, double[] gradf)
            {
                const double eps = 0.001;

                var x1 = new double[_n];
                var x2 = new double[_n];

                for (int j = 0; j < _n; j++)
                {
                    Array.Copy(x, x1, _n);
                    Array.Copy(x, x2, _n);

                    if (x[j] != 0.0)
                    {
                        x1[j] = x[j] * (1.0 + eps);
                        x2[j] = x[j] * (1.0 - eps);
                    }
                    else
                    {
                        x1[j] = x[j] + eps;
                        x2[j] = x[j] - eps;
                    }

                    gradf[j] = (EvalF(x2) - EvalF(x1)) / (x2[j] - x1[j]);
                }
            }

            /// <summary>
            /// Ipopt tells the callback whether x moved since the last call so it can cache. The
            /// engine's callbacks ignore the flag, but reporting it correctly costs one compare.
            /// </summary>
            private bool IsNew(double[] x)
            {
                bool changed = !_seen;

                for (int i = 0; i < _n && !changed; i++)
                {
                    if (_last[i] != x[i]) changed = true;
                }

                if (changed)
                {
                    Array.Copy(x, _last, _n);
                    _seen = true;
                }

                return changed;
            }
        }
    }

    public delegate bool EvaluateObjectiveDelegate(
        int n, double[] x, bool new_x, ref double obj_value);

    public delegate bool EvaluateObjectiveGradientDelegate(
        int n, double[] x, bool new_x, ref double[] grad_f);

    public delegate bool EvaluateConstraintsDelegate(
        int n, double[] x, bool new_x, int m, ref double[] g);

    public delegate bool EvaluateJacobianDelegate(
        int n, double[] x, bool new_x, int m, int nele_jac,
        ref int[] iRow, ref int[] jCol, ref double[] values);

    public delegate bool EvaluateHessianDelegate(
        int n, double[] x, bool new_x, double obj_factor, int m, double[] lambda,
        bool new_lambda, int nele_hess, ref int[] iRow, ref int[] jCol, ref double[] values);

    public delegate bool IntermediateDelegate(
        IpoptAlgorithmMode alg_mod, int iter_count, double obj_value,
        double inf_pr, double inf_du, double mu, double d_norm,
        double regularization_size, double alpha_du, double alpha_pr, int ls_trials);

    public enum IpoptAlgorithmMode
    {
        RegularMode = 0,
        RestorationPhaseMode = 1,
    }

    /// <summary>IPOPT's own ApplicationReturnStatus.</summary>
    public enum IpoptReturnCode
    {
        Solve_Succeeded = 0,
        Solved_To_Acceptable_Level = 1,
        Infeasible_Problem_Detected = 2,
        Search_Direction_Becomes_Too_Small = 3,
        Diverging_Iterates = 4,
        User_Requested_Stop = 5,
        Feasible_Point_Found = 6,
        Maximum_Iterations_Exceeded = -1,
        Restoration_Failed = -2,
        Error_In_Step_Computation = -3,
        Maximum_CpuTime_Exceeded = -4,
        Not_Enough_Degrees_Of_Freedom = -10,
        Invalid_Problem_Definition = -11,
        Invalid_Option = -12,
        Invalid_Number_Detected = -13,
        Unrecoverable_Exception = -100,
        NonIpopt_Exception_Thrown = -101,
        Insufficient_Memory = -102,
        Internal_Error = -199,
    }
}
