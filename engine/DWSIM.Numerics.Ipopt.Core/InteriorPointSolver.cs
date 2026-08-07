// A primal-dual interior-point method for bound-constrained NLP
//     min f(x)   s.t.  xL <= x <= xU
// with a (dense, damped-BFGS) quasi-Newton Hessian. This is the DWSIM-scoped
// m = 0 case of Ipopt: no constraints (they enter DWSIM as objective penalties),
// limited-memory-style Hessian (here full dense BFGS; identical for small n).
//
// The math and the option defaults follow Ipopt (Waechter & Biegler 2006): the
// scaled optimality error E_mu (eq. 5-6), monotone mu update, fraction-to-
// boundary step lengths, and an Armijo/filter line search on the barrier
// function (the filter degenerates to Armijo because the constraint
// infeasibility theta is identically zero when m = 0). The reduced Newton system
// (B + Sigma) dx = -grad_phi is solved with the dense Cholesky from DWSIM.Numerics.Ipopt.Sparse,
// with diagonal regularization as an inertia guard. Per-iteration diagnostics are
// recorded in Ipopt's log format for side-by-side comparison with the native run.

using System;
using System.Collections.Generic;
using DWSIM.Numerics.Ipopt.Sparse;

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>Interior-point solver for bound-constrained problems (Ipopt m = 0 case).</summary>
    public sealed class InteriorPointSolver
    {
        private readonly SolverOptions _opt;

        /// <summary>
        /// How many times a rejected step may be answered by rebuilding the curvature before the
        /// solve is called off. The same budget the constrained solver gives restoration.
        /// </summary>
        private const int MaxRecoveries = 12;

        public InteriorPointSolver(SolverOptions? options = null)
        {
            _opt = options ?? new SolverOptions();
        }

        public SolveResult Solve(INlp nlp)
        {
            if (nlp is null) throw new ArgumentNullException(nameof(nlp));
            int n = nlp.N;
            if (n <= 0) return new SolveResult { Status = SolveStatus.InvalidInput };

            double inf = _opt.BoundInf;

            var xl = new double[n];
            var xu = new double[n];
            nlp.GetBounds(xl, xu);

            var hasL = new bool[n];
            var hasU = new bool[n];
            int nbounds = 0;
            for (int i = 0; i < n; i++)
            {
                hasL[i] = xl[i] > -inf;
                hasU[i] = xu[i] < inf;
                if (hasL[i]) nbounds++;
                if (hasU[i]) nbounds++;
                if (hasL[i] && hasU[i] && xl[i] > xu[i])
                    return new SolveResult { Status = SolveStatus.InvalidInput };
            }

            var x = new double[n];
            nlp.GetStartingPoint(x);
            PushInterior(x, xl, xu, hasL, hasU);

            var zL = new double[n];
            var zU = new double[n];
            for (int i = 0; i < n; i++)
            {
                zL[i] = hasL[i] ? _opt.BoundMultInit : 0.0;
                zU[i] = hasU[i] ? _opt.BoundMultInit : 0.0;
            }

            var grad = new double[n];
            var gradOld = new double[n];
            nlp.EvalGradF(x, grad);

            IHessian hess = _opt.HessianApproximation == HessianApproximation.LimitedMemoryBfgs
                ? new LbfgsHessian(_opt.LimitedMemoryMaxHistory)
                : new DenseBfgsHessian();
            hess.Reset(n);
            var B = new double[n, n];
            int recoveries = 0;

            double mu = _opt.MuInit;
            double tau = Math.Max(_opt.TauMin, 1.0 - mu);

            var sL = new double[n];
            var sU = new double[n];
            var gradPhi = new double[n];
            var sigma = new double[n];
            var dx = new double[n];
            var dzL = new double[n];
            var dzU = new double[n];
            var s = new double[n];
            var y = new double[n];
            var xTrial = new double[n];

            var chol = new DenseCholesky();
            var bk = new BunchKaufman();
            var sysA = new double[n, n];

            var log = _opt.CollectIterationLog ? new List<IterationInfo>() : null;
            _opt.LogWriter?.WriteLine(IpoptLog.Header());

            int iter = 0;

            while (true)
            {
                UpdateSlacks(x, xl, xu, hasL, hasU, sL, sU);

                double eMu = OptimalityError(n, grad, zL, zU, sL, sU, hasL, hasU, mu, nbounds, out _);
                double e0 = OptimalityError(n, grad, zL, zU, sL, sU, hasL, hasU, 0.0, nbounds, out double dualInf);

                if (e0 <= _opt.Tolerance)
                {
                    Record(log, new IterationInfo(iter, nlp.EvalF(x), 0.0, dualInf, mu, 0.0, 0.0, 0.0, 0.0, 0));
                    return Finish(SolveStatus.Solved, x, nlp.EvalF(x), iter, e0, log);
                }

                if (iter >= _opt.MaxIterations)
                {
                    Record(log, new IterationInfo(iter, nlp.EvalF(x), 0.0, dualInf, mu, 0.0, 0.0, 0.0, 0.0, 0));
                    return Finish(SolveStatus.MaxIterations, x, nlp.EvalF(x), iter, e0, log);
                }

                if (_opt.MuStrategy == MuStrategy.Adaptive)
                {
                    // Recompute mu each iteration from the current complementarity (LOQO oracle).
                    mu = AdaptiveMu(n, sL, sU, zL, zU, hasL, hasU);
                    tau = Math.Max(_opt.TauMin, 1.0 - mu);
                }
                else
                {
                    // Monotone: decrease mu once the barrier subproblem is solved accurately
                    // enough. Only loop back if mu actually shrinks; at the floor, keep stepping.
                    if (eMu <= _opt.BarrierTolFactor * mu)
                    {
                        double muNew = Math.Max(_opt.Tolerance / 10.0,
                            Math.Min(_opt.MuLinearDecreaseFactor * mu, Math.Pow(mu, _opt.MuSuperlinearDecreasePower)));
                        if (muNew < mu)
                        {
                            mu = muNew;
                            tau = Math.Max(_opt.TauMin, 1.0 - mu);
                            continue;
                        }
                    }
                }

                hess.GetDense(B);

                for (int i = 0; i < n; i++)
                {
                    double g = grad[i];
                    double sig = 0.0;
                    if (hasL[i]) { g -= mu / sL[i]; sig += zL[i] / sL[i]; }
                    if (hasU[i]) { g += mu / sU[i]; sig += zU[i] / sU[i]; }
                    gradPhi[i] = g;
                    sigma[i] = sig;
                }

                double delta = SolveReducedSystem(n, B, sigma, gradPhi, dx, sysA, chol, bk);

                for (int i = 0; i < n; i++)
                {
                    dzL[i] = hasL[i] ? (mu / sL[i] - zL[i] - (zL[i] / sL[i]) * dx[i]) : 0.0;
                    dzU[i] = hasU[i] ? (mu / sU[i] - zU[i] + (zU[i] / sU[i]) * dx[i]) : 0.0;
                }

                double alphaX = FractionToBoundaryPrimal(n, sL, sU, dx, hasL, hasU, tau);
                double alphaZ = FractionToBoundaryDual(n, zL, zU, dzL, dzU, hasL, hasU, tau);

                double phi0 = BarrierValue(nlp, x, xl, xu, hasL, hasU, mu);
                double dphi = 0.0;
                for (int i = 0; i < n; i++) dphi += gradPhi[i] * dx[i];

                double alpha = alphaX;
                bool accepted = false;
                int ls = 0;
                for (; ls < 40; ls++)
                {
                    for (int i = 0; i < n; i++) xTrial[i] = x[i] + alpha * dx[i];
                    if (StrictlyInterior(xTrial, xl, xu, hasL, hasU))
                    {
                        double phiT = BarrierValue(nlp, xTrial, xl, xu, hasL, hasU, mu);
                        if (phiT <= phi0 + _opt.ArmijoEta * alpha * dphi)
                        {
                            accepted = true;
                            break;
                        }
                    }
                    alpha *= 0.5;
                }

                double dNorm = 0.0;
                for (int i = 0; i < n; i++) dNorm = Math.Max(dNorm, Math.Abs(dx[i]));

                if (!Record(log, new IterationInfo(iter, nlp.EvalF(x), 0.0, dualInf, mu, dNorm, delta,
                                                   alphaZ, alpha, ls, restoration: !accepted)))
                {
                    return Finish(SolveStatus.UserRequested, x, nlp.EvalF(x), iter, e0, log);
                }

                if (!accepted)
                {
                    // The trial point was rejected, so it is not somewhere to go. Moving there
                    // anyway is how an objective that is undefined in part of the box ends up
                    // being reported as the answer: every trial is rejected, the last one is
                    // taken regardless, and from then on every evaluation is of a NaN.
                    if (recoveries >= MaxRecoveries)
                    {
                        return Finish(SolveStatus.LineSearchFailure, x, nlp.EvalF(x), iter, e0, log);
                    }

                    // A direction the line search could not use is a direction built from a
                    // curvature estimate that no longer describes the function here.
                    recoveries++;
                    hess.Reset(n);
                    iter++;
                    continue;
                }

                Array.Copy(grad, gradOld, n);
                for (int i = 0; i < n; i++)
                {
                    s[i] = alpha * dx[i];
                    x[i] = xTrial[i];
                }
                nlp.EvalGradF(x, grad);
                for (int i = 0; i < n; i++) y[i] = grad[i] - gradOld[i];

                for (int i = 0; i < n; i++)
                {
                    if (hasL[i])
                    {
                        zL[i] += alphaZ * dzL[i];
                        double sLi = x[i] - xl[i];
                        zL[i] = Math.Min(Math.Max(zL[i], mu / (sLi * 1e10)), 1e10 * mu / sLi);
                    }
                    if (hasU[i])
                    {
                        zU[i] += alphaZ * dzU[i];
                        double sUi = xu[i] - x[i];
                        zU[i] = Math.Min(Math.Max(zU[i], mu / (sUi * 1e10)), 1e10 * mu / sUi);
                    }
                }

                hess.Update(s, y);
                iter++;
            }
        }

        /// <summary>Reports one iteration. Returns false when the callback asks to stop.</summary>
        private bool Record(List<IterationInfo>? log, in IterationInfo info)
        {
            log?.Add(info);
            if (_opt.LogWriter != null) _opt.LogWriter.WriteLine(IpoptLog.Row(info));
            return _opt.IterationCallback == null || _opt.IterationCallback(info);
        }

        private static SolveResult Finish(SolveStatus status, double[] x, double f, int iter, double err, List<IterationInfo>? log)
        {
            return new SolveResult
            {
                Status = NumberCheck.Verify(status, x, f),
                X = x,
                ObjValue = f,
                Iterations = iter,
                OptimalityError = err,
                IterationLog = (IReadOnlyList<IterationInfo>?)log ?? Array.Empty<IterationInfo>()
            };
        }

        // (B + Sigma) dx = -gradPhi, SPD via Cholesky; if not positive definite, inflate the
        // diagonal (a simple inertia correction) until it is. Returns the regularization used.
        private static double SolveReducedSystem(
            int n, double[,] B, double[] sigma, double[] gradPhi, double[] dx,
            double[,] sysA, DenseCholesky chol, BunchKaufman bk)
        {
            double delta = 0.0;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++) sysA[i, j] = B[i, j];
                    sysA[i, i] += sigma[i] + delta;
                }

                if (chol.Factorize(sysA) == FactorStatus.Success)
                {
                    for (int i = 0; i < n; i++) dx[i] = -gradPhi[i];
                    chol.Solve(dx);
                    return delta;
                }
                delta = delta == 0.0 ? 1e-8 : delta * 10.0;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) sysA[i, j] = B[i, j];
                sysA[i, i] += sigma[i] + delta;
            }
            bk.Factorize(sysA);
            for (int i = 0; i < n; i++) dx[i] = -gradPhi[i];
            bk.Solve(dx);
            return delta;
        }

        // LOQO/Mehrotra adaptive barrier oracle (Ipopt mu_oracle=loqo): from the current
        // complementarity distribution, mu = sigma * avg_compl with a centrality-based sigma.
        private double AdaptiveMu(int n, double[] sL, double[] sU, double[] zL, double[] zU, bool[] hasL, bool[] hasU)
        {
            double sum = 0.0;
            double min = double.PositiveInfinity;
            int p = 0;
            for (int i = 0; i < n; i++)
            {
                if (hasL[i]) { double c = sL[i] * zL[i]; sum += c; if (c < min) min = c; p++; }
                if (hasU[i]) { double c = sU[i] * zU[i]; sum += c; if (c < min) min = c; p++; }
            }
            if (p == 0) return Math.Max(_opt.Tolerance / 10.0, _opt.MuInit);

            double avg = sum / p;
            if (avg <= 0.0) return _opt.Tolerance / 10.0;

            double xi = min / avg;                        // centrality measure in (0,1]
            double t = Math.Min(0.05 * (1.0 - xi) / Math.Max(xi, 1e-12), 2.0);
            double sigma = 0.1 * t * t * t;
            double mu = sigma * avg;

            // Keep mu strictly positive; let it fall with the complementarity toward the tolerance.
            return Math.Max(mu, _opt.Tolerance / 100.0);
        }

        private void PushInterior(double[] x, double[] xl, double[] xu, bool[] hasL, bool[] hasU)
        {
            double k1 = _opt.BoundPush;
            double k2 = _opt.BoundFrac;
            for (int i = 0; i < x.Length; i++)
            {
                if (hasL[i] && hasU[i])
                {
                    double pl = Math.Min(k1 * Math.Max(1.0, Math.Abs(xl[i])), k2 * (xu[i] - xl[i]));
                    double pu = Math.Min(k1 * Math.Max(1.0, Math.Abs(xu[i])), k2 * (xu[i] - xl[i]));
                    x[i] = Math.Min(Math.Max(x[i], xl[i] + pl), xu[i] - pu);
                }
                else if (hasL[i])
                {
                    double pl = k1 * Math.Max(1.0, Math.Abs(xl[i]));
                    if (x[i] < xl[i] + pl) x[i] = xl[i] + pl;
                }
                else if (hasU[i])
                {
                    double pu = k1 * Math.Max(1.0, Math.Abs(xu[i]));
                    if (x[i] > xu[i] - pu) x[i] = xu[i] - pu;
                }
            }
        }

        private static void UpdateSlacks(double[] x, double[] xl, double[] xu, bool[] hasL, bool[] hasU, double[] sL, double[] sU)
        {
            for (int i = 0; i < x.Length; i++)
            {
                sL[i] = hasL[i] ? x[i] - xl[i] : 1.0;
                sU[i] = hasU[i] ? xu[i] - x[i] : 1.0;
            }
        }

        // Scaled optimality error E_mu (Ipopt eq. 5-6). For m = 0, s_d = s_c and the
        // primal-feasibility term is zero. Also returns the unscaled dual infeasibility.
        private double OptimalityError(
            int n, double[] grad, double[] zL, double[] zU, double[] sL, double[] sU,
            bool[] hasL, bool[] hasU, double mu, int nbounds, out double dualInfUnscaled)
        {
            double sumZ = 0.0;
            for (int i = 0; i < n; i++)
            {
                if (hasL[i]) sumZ += Math.Abs(zL[i]);
                if (hasU[i]) sumZ += Math.Abs(zU[i]);
            }
            double sMax = _opt.SMax;
            double sd = Math.Max(sMax, sumZ / Math.Max(1, nbounds)) / sMax;
            double sc = sd;

            double dualInf = 0.0;
            double compl = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = grad[i];
                if (hasL[i]) d -= zL[i];
                if (hasU[i]) d += zU[i];
                dualInf = Math.Max(dualInf, Math.Abs(d));

                if (hasL[i]) compl = Math.Max(compl, Math.Abs(sL[i] * zL[i] - mu));
                if (hasU[i]) compl = Math.Max(compl, Math.Abs(sU[i] * zU[i] - mu));
            }
            dualInfUnscaled = dualInf;
            return Math.Max(dualInf / sd, compl / sc);
        }

        private static double FractionToBoundaryPrimal(int n, double[] sL, double[] sU, double[] dx, bool[] hasL, bool[] hasU, double tau)
        {
            double a = 1.0;
            for (int i = 0; i < n; i++)
            {
                if (hasL[i] && dx[i] < 0.0) a = Math.Min(a, -tau * sL[i] / dx[i]);
                if (hasU[i] && dx[i] > 0.0) a = Math.Min(a, tau * sU[i] / dx[i]);
            }
            return a;
        }

        private static double FractionToBoundaryDual(int n, double[] zL, double[] zU, double[] dzL, double[] dzU, bool[] hasL, bool[] hasU, double tau)
        {
            double a = 1.0;
            for (int i = 0; i < n; i++)
            {
                if (hasL[i] && dzL[i] < 0.0) a = Math.Min(a, -tau * zL[i] / dzL[i]);
                if (hasU[i] && dzU[i] < 0.0) a = Math.Min(a, -tau * zU[i] / dzU[i]);
            }
            return a;
        }

        private static bool StrictlyInterior(double[] x, double[] xl, double[] xu, bool[] hasL, bool[] hasU)
        {
            for (int i = 0; i < x.Length; i++)
            {
                if (hasL[i] && x[i] <= xl[i]) return false;
                if (hasU[i] && x[i] >= xu[i]) return false;
            }
            return true;
        }

        private static double BarrierValue(INlp nlp, double[] x, double[] xl, double[] xu, bool[] hasL, bool[] hasU, double mu)
        {
            double phi = nlp.EvalF(x);
            for (int i = 0; i < x.Length; i++)
            {
                if (hasL[i]) phi -= mu * Math.Log(x[i] - xl[i]);
                if (hasU[i]) phi -= mu * Math.Log(xu[i] - x[i]);
            }
            return phi;
        }
    }
}
