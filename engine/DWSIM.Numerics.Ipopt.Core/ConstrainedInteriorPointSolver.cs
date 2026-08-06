// Primal-dual interior point for the constrained case, following Waechter and Biegler 2006 (the
// Ipopt paper) closely enough that the two agree on an answer, and using the same defaults DWSIM
// passes to the native library: adaptive mu and a limited-memory BFGS Hessian.
//
// Every constraint gets a slack, g(x) - s = 0 with cl <= s <= cu, which turns inequalities and
// equalities into one shape; an equality is a slack whose bounds coincide, and it is simply left
// out of the slack vector. The step comes from the augmented system
//
//     [ W + Sx      0      A^T ] [dx]     [ grad f + A^T y - zL + zU ]
//     [   0        Ss     -P^T ] [ds] = - [ -yI - vL + vU            ]
//     [   A        -P       0  ] [dy]     [ g(x) - s                 ]
//
// where P selects the inequality rows, solved by the symmetric indefinite factorization in
// DWSIM.Numerics.Ipopt.Sparse, which reports inertia so the regularization can be driven the way
// Ipopt's Algorithm IC does. Acceptance is the filter on (constraint violation, barrier
// objective), not a merit function.

using System;
using System.Collections.Generic;
using DWSIM.Numerics.Ipopt.Sparse;

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>Interior-point solver for problems with general constraints.</summary>
    public sealed class ConstrainedInteriorPointSolver
    {
        private readonly SolverOptions _opt;

        public ConstrainedInteriorPointSolver(SolverOptions? options = null)
        {
            _opt = options ?? new SolverOptions();
        }

        // Filter constants, Ipopt's own defaults.
        private const double GammaTheta = 1e-5;
        private const double GammaPhi = 1e-5;
        private const double DeltaSwitch = 1.0;
        private const double STheta = 1.1;
        private const double SPhi = 2.3;
        private const double Eta = 1e-4;

        // Inertia correction, Ipopt's Algorithm IC.
        private const double DeltaMin = 1e-20;
        private const double Delta0 = 1e-4;
        private const double KappaMinus = 1.0 / 3.0;
        private const double KappaPlusBar = 100.0;
        private const double KappaPlus = 8.0;
        private const double DeltaCBar = 1e-8;

        /// <summary>Floor on a bound gap, so a point that reaches its bound cannot make a NaN.</summary>
        private const double Tiny = 1e-300;

        /// <summary>Restoration has to cut the violation by at least this factor to count.</summary>
        private const double KappaRestoration = 0.9;

        /// <summary>A solve that keeps needing restoration is not going to finish.</summary>
        private const int MaxRestorations = 12;

        /// <summary>Target infinity norm for a scaled gradient (Ipopt: nlp_scaling_max_gradient).</summary>
        private const double GMax = 100.0;

        public SolveResult Solve(INlpConstrained nlp)
        {
            int n = nlp.N;
            int m = nlp.M;

            if (n <= 0) return Fail(SolveStatus.InvalidInput);
            if (m <= 0) return Fail(SolveStatus.InvalidInput);

            var xl = new double[n];
            var xu = new double[n];
            nlp.GetBounds(xl, xu);

            var cl = new double[m];
            var cu = new double[m];
            nlp.GetConstraintBounds(cl, cu);

            var hasXl = new bool[n];
            var hasXu = new bool[n];
            for (int i = 0; i < n; i++)
            {
                hasXl[i] = xl[i] > -_opt.BoundInf;
                hasXu[i] = xu[i] < _opt.BoundInf;
                if (hasXl[i] && hasXu[i] && xl[i] > xu[i]) return Fail(SolveStatus.InvalidInput);
            }

            // An equality is a constraint whose bounds coincide: it keeps no slack, and its row
            // of the augmented system has a zero in the slack column.
            var isEquality = new bool[m];
            var slackOf = new int[m];
            int ns = 0;

            for (int i = 0; i < m; i++)
            {
                isEquality[i] = cu[i] - cl[i] <= 0.0;
                slackOf[i] = isEquality[i] ? -1 : ns++;
            }

            var hasSl = new bool[ns];
            var hasSu = new bool[ns];
            var sl = new double[ns];
            var su = new double[ns];

            for (int i = 0; i < m; i++)
            {
                int k = slackOf[i];
                if (k < 0) continue;
                sl[k] = cl[i];
                su[k] = cu[i];
                hasSl[k] = cl[i] > -_opt.BoundInf;
                hasSu[k] = cu[i] < _opt.BoundInf;
            }

            var x = new double[n];
            nlp.GetStartingPoint(x);
            PushInside(n, x, xl, xu, hasXl, hasXu);

            // Gradient-based scaling, Ipopt's nlp_scaling_method default. Without it a problem
            // whose objective gradient is orders of magnitude larger than its constraint rows
            // drives the barrier parameter through the roof: the complementarity products the mu
            // oracle averages are in the units of the multipliers, and the multipliers are in the
            // units of the gradient. The Gibbs energy of a flash is thousands while its element
            // balance is ones, and that alone was enough to make mu swing from 1e-5 to 1e4.
            var scaled = ScaledNlp.Wrap(nlp, n, m, x, GMax);

            var g = new double[m];
            var jac = new double[m * n];
            var grad = new double[n];

            scaled.EvalG(x, g);
            scaled.EvalJacG(x, jac);
            scaled.EvalGradF(x, grad);

            // Slacks start on the constraint value, pushed inside their own bounds, so the first
            // point is as feasible as the starting x allows.
            var s = new double[ns];
            for (int i = 0; i < m; i++)
            {
                int k = slackOf[i];
                if (k >= 0) s[k] = g[i];
            }
            PushInside(ns, s, sl, su, hasSl, hasSu);

            var zL = new double[n];
            var zU = new double[n];
            var vL = new double[ns];
            var vU = new double[ns];
            var y = new double[m];

            for (int i = 0; i < n; i++)
            {
                zL[i] = hasXl[i] ? _opt.BoundMultInit : 0.0;
                zU[i] = hasXu[i] ? _opt.BoundMultInit : 0.0;
            }

            for (int k = 0; k < ns; k++)
            {
                vL[k] = hasSl[k] ? _opt.BoundMultInit : 0.0;
                vU[k] = hasSu[k] ? _opt.BoundMultInit : 0.0;
            }

            double mu = _opt.MuInit;
            double tauMin = _opt.TauMin;

            var filter = new List<(double Theta, double Phi)>();
            var log = _opt.CollectIterationLog ? new List<IterationInfo>() : null;

            int dim = n + ns + m;
            var kkt = new KktSystem(dim);
            IHessian hess = _opt.HessianApproximation == HessianApproximation.LimitedMemoryBfgs
                ? new LbfgsHessian(_opt.LimitedMemoryMaxHistory)
                : (IHessian)new DenseBfgsHessian();
            hess.Reset(n);

            var w = new double[n, n];
            var gradLagOld = new double[n];
            var step = new double[n];

            double theta0 = Theta(m, g, s, slackOf, cl);
            double thetaMax = Math.Max(1e4, 1e4 * theta0);
            double thetaMin = Math.Min(1e-4, 1e-4 * theta0);

            int iter = 0;
            int restorations = 0;

            while (true)
            {
                double err = OptimalityError(n, ns, m, x, s, g, grad, jac, y, zL, zU, vL, vU,
                                             xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu,
                                             slackOf, cl, 0.0);

                double theta = Theta(m, g, s, slackOf, cl);
                double phi = Barrier(n, ns, scaled.EvalF(x), x, s, xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu, mu);

                if (err <= _opt.Tolerance)
                {
                    if (!Record(log, new IterationInfo(iter, scaled.EvalF(x), theta, err, mu, 0.0, 0.0, 0.0, 0.0, 0)))
                        return Finish(SolveStatus.UserRequested, x, nlp.EvalF(x), iter, err, log);

                    return Finish(SolveStatus.Solved, x, nlp.EvalF(x), iter, err, log);
                }

                if (iter >= _opt.MaxIterations)
                {
                    Record(log, new IterationInfo(iter, scaled.EvalF(x), theta, err, mu, 0.0, 0.0, 0.0, 0.0, 0));
                    return Finish(SolveStatus.MaxIterations, x, nlp.EvalF(x), iter, err, log);
                }

                // Barrier parameter. The subproblem is solved when its own error falls below
                // kappa_eps * mu; then mu drops, monotonically or by the LOQO oracle.
                double subError = OptimalityError(n, ns, m, x, s, g, grad, jac, y, zL, zU, vL, vU,
                                                  xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu,
                                                  slackOf, cl, mu);

                if (subError <= _opt.BarrierTolFactor * mu)
                {
                    mu = _opt.MuStrategy == MuStrategy.Adaptive
                        ? AdaptiveMu(n, ns, x, s, zL, zU, vL, vU, xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu)
                        : Math.Max(_opt.Tolerance / 10.0,
                                   Math.Min(_opt.MuLinearDecreaseFactor * mu,
                                            Math.Pow(mu, _opt.MuSuperlinearDecreasePower)));
                    filter.Clear();
                }
                else if (_opt.MuStrategy == MuStrategy.Adaptive)
                {
                    double next = AdaptiveMu(n, ns, x, s, zL, zU, vL, vU, xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu);

                    // The filter compares barrier objectives, and mu is in that objective, so an
                    // entry recorded under one mu says nothing about a trial point under another.
                    // Adaptive mu moves every iteration, so the filter has to be emptied with it.
                    if (next != mu)
                    {
                        mu = next;
                        filter.Clear();
                    }
                }

                double tau = Math.Max(tauMin, 1.0 - mu);

                hess.GetDense(w);

                double delta = SolveStep(kkt, n, ns, m, w, jac, x, s, zL, zU, vL, vU, y, g, grad,
                                         xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu, slackOf, cl, mu,
                                         out var dx, out var ds, out var dy);

                if (double.IsNaN(delta))
                {
                    Record(log, new IterationInfo(iter, scaled.EvalF(x), theta, err, mu, 0.0, 0.0, 0.0, 0.0, 0));
                    return Finish(SolveStatus.LineSearchFailure, x, nlp.EvalF(x), iter, err, log);
                }

                var dzL = new double[n];
                var dzU = new double[n];
                var dvL = new double[ns];
                var dvU = new double[ns];

                BoundMultiplierStep(n, x, dx, xl, xu, hasXl, hasXu, zL, zU, mu, dzL, dzU);
                BoundMultiplierStep(ns, s, ds, sl, su, hasSl, hasSu, vL, vU, mu, dvL, dvU);

                double alphaMax = FractionToBoundary(n, x, dx, xl, xu, hasXl, hasXu, tau);
                alphaMax = Math.Min(alphaMax, FractionToBoundary(ns, s, ds, sl, su, hasSl, hasSu, tau));

                double alphaZ = FractionToBoundaryDual(n, zL, dzL, tau);
                alphaZ = Math.Min(alphaZ, FractionToBoundaryDual(n, zU, dzU, tau));
                alphaZ = Math.Min(alphaZ, FractionToBoundaryDual(ns, vL, dvL, tau));
                alphaZ = Math.Min(alphaZ, FractionToBoundaryDual(ns, vU, dvU, tau));

                double dPhi = DirectionalDerivative(n, ns, grad, dx, ds, x, s,
                                                    xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu, mu);

                var xTrial = new double[n];
                var sTrial = new double[ns];
                var gTrial = new double[m];

                double alpha = alphaMax;
                bool accepted = false;
                int ls = 0;

                while (alpha > 1e-14)
                {
                    ls++;

                    for (int i = 0; i < n; i++) xTrial[i] = x[i] + alpha * dx[i];
                    for (int k = 0; k < ns; k++) sTrial[k] = s[k] + alpha * ds[k];

                    scaled.EvalG(xTrial, gTrial);

                    double thetaTrial = Theta(m, gTrial, sTrial, slackOf, cl);
                    double phiTrial = Barrier(n, ns, scaled.EvalF(xTrial), xTrial, sTrial,
                                              xl, xu, hasXl, hasXu, sl, su, hasSl, hasSu, mu);

                    if (thetaTrial > thetaMax)
                    {
                        alpha *= 0.5;
                        continue;
                    }

                    if (Acceptable(filter, theta, phi, thetaTrial, phiTrial, dPhi, alpha, thetaMin))
                    {
                        // The filter only grows when the step was not an f-type step, which is
                        // what keeps it from cutting off the neighbourhood of the solution.
                        bool switching = dPhi < 0.0 &&
                                         alpha * Math.Pow(-dPhi, SPhi) > DeltaSwitch * Math.Pow(theta, STheta);

                        if (!(switching && phiTrial <= phi + Eta * alpha * dPhi))
                        {
                            filter.Add(((1.0 - GammaTheta) * theta, phi - GammaPhi * theta));
                        }

                        accepted = true;
                        break;
                    }

                    alpha *= 0.5;
                }

                double dNorm = 0.0;
                for (int i = 0; i < n; i++) dNorm = Math.Max(dNorm, Math.Abs(dx[i]));

                if (!Record(log, new IterationInfo(iter, scaled.EvalF(x), theta, err, mu, dNorm, delta, alphaZ, alpha, ls)))
                    return Finish(SolveStatus.UserRequested, x, nlp.EvalF(x), iter, err, log);

                if (!accepted)
                {
                    // The filter blocks every trial point, so no step of this iteration can be
                    // taken. There are two reasons that happens and they need opposite answers.
                    if (restorations >= MaxRestorations)
                    {
                        return Finish(SolveStatus.RestorationFailed, x, nlp.EvalF(x), iter, err, log);
                    }

                    if (theta > thetaMin)
                    {
                        // Infeasible: go find a point closer to the constraints and resume there.
                        if (!Restore(scaled, n, ns, m, slackOf, cl, xl, xu, sl, su, x, s, theta))
                        {
                            return Finish(SolveStatus.RestorationFailed, x, nlp.EvalF(x), iter, err, log);
                        }
                    }
                    else
                    {
                        // Feasible already, so restoration has nothing to restore: the direction
                        // is bad, not the point. That is the quasi-Newton matrix having drifted,
                        // and the cure is to forget it and rebuild from the current curvature.
                        // Ipopt resets the approximation on entering restoration for the same
                        // reason; here it is the whole of the remedy.
                        hess.Reset(n);
                        filter.Clear();

                        restorations++;
                        iter++;
                        continue;
                    }

                    restorations++;

                    // The point that could not be left is now forbidden, so the iteration cannot
                    // walk back into it.
                    filter.Add(((1.0 - GammaTheta) * theta, phi - GammaPhi * theta));

                    scaled.EvalG(x, g);
                    scaled.EvalJacG(x, jac);
                    scaled.EvalGradF(x, grad);

                    // The multipliers and the curvature history belonged to the point that was
                    // abandoned, so neither means anything here.
                    Array.Clear(y, 0, m);

                    for (int i = 0; i < n; i++)
                    {
                        zL[i] = hasXl[i] ? _opt.BoundMultInit : 0.0;
                        zU[i] = hasXu[i] ? _opt.BoundMultInit : 0.0;
                    }

                    for (int k = 0; k < ns; k++)
                    {
                        vL[k] = hasSl[k] ? _opt.BoundMultInit : 0.0;
                        vU[k] = hasSu[k] ? _opt.BoundMultInit : 0.0;
                    }

                    hess.Reset(n);

                    iter++;
                    continue;
                }

                var gradOld = (double[])grad.Clone();
                var jacOld = (double[])jac.Clone();

                for (int i = 0; i < n; i++)
                {
                    step[i] = alpha * dx[i];
                    x[i] = xTrial[i];
                }

                for (int k = 0; k < ns; k++) s[k] = sTrial[k];
                for (int i = 0; i < m; i++) y[i] += alpha * dy[i];

                // Both ends of the curvature pair use the multipliers the step produced: the
                // quasi-Newton matrix approximates the Hessian of one Lagrangian, and mixing the
                // old y into one end and the new y into the other measures nothing.
                LagrangianGradient(n, m, gradOld, jacOld, y, gradLagOld);

                UpdateBoundMultipliers(n, zL, dzL, zU, dzU, alphaZ, x, xl, xu, hasXl, hasXu, mu);
                UpdateBoundMultipliers(ns, vL, dvL, vU, dvU, alphaZ, s, sl, su, hasSl, hasSu, mu);

                scaled.EvalG(x, g);
                scaled.EvalJacG(x, jac);
                scaled.EvalGradF(x, grad);

                var gradLagNew = new double[n];
                LagrangianGradient(n, m, grad, jac, y, gradLagNew);

                var yDiff = new double[n];
                for (int i = 0; i < n; i++) yDiff[i] = gradLagNew[i] - gradLagOld[i];

                hess.Update(step, yDiff);

                iter++;
            }
        }

        /// <summary>
        /// Builds and factorizes the augmented system, raising the regularization until the
        /// inertia is (n + ns, m, 0), which is what makes the step a descent direction for the
        /// barrier objective on the constraint manifold. Returns the delta it settled on, or NaN
        /// if no amount of regularization produced the right inertia.
        /// </summary>
        private double SolveStep(KktSystem kkt, int n, int ns, int m, double[,] w, double[] jac,
                                 double[] x, double[] s, double[] zL, double[] zU,
                                 double[] vL, double[] vU, double[] y, double[] g, double[] grad,
                                 double[] xl, double[] xu, bool[] hasXl, bool[] hasXu,
                                 double[] sl, double[] su, bool[] hasSl, bool[] hasSu,
                                 int[] slackOf, double[] cl, double mu,
                                 out double[] dx, out double[] ds, out double[] dy)
        {
            int dim = n + ns + m;

            var sigmaX = new double[n];
            for (int i = 0; i < n; i++)
            {
                double v = 0.0;
                if (hasXl[i]) v += zL[i] / Math.Max(x[i] - xl[i], Tiny);
                if (hasXu[i]) v += zU[i] / Math.Max(xu[i] - x[i], Tiny);
                sigmaX[i] = v;
            }

            var sigmaS = new double[ns];
            for (int k = 0; k < ns; k++)
            {
                double v = 0.0;
                if (hasSl[k]) v += vL[k] / Math.Max(s[k] - sl[k], Tiny);
                if (hasSu[k]) v += vU[k] / Math.Max(su[k] - s[k], Tiny);
                sigmaS[k] = v;
            }

            // Right-hand side, negated.
            var rhs = new double[dim];

            for (int i = 0; i < n; i++)
            {
                double v = grad[i];
                for (int r = 0; r < m; r++) v += jac[r * n + i] * y[r];
                if (hasXl[i]) v -= mu / (x[i] - xl[i]);
                if (hasXu[i]) v += mu / (xu[i] - x[i]);
                rhs[i] = -v;
            }

            for (int r = 0; r < m; r++)
            {
                int k = slackOf[r];
                if (k < 0) continue;
                double v = -y[r];
                if (hasSl[k]) v -= mu / (s[k] - sl[k]);
                if (hasSu[k]) v += mu / (su[k] - s[k]);
                rhs[n + k] = -v;
            }

            for (int r = 0; r < m; r++)
            {
                int k = slackOf[r];
                double v = g[r] - (k >= 0 ? s[k] : cl[r]);
                rhs[n + ns + r] = -v;
            }

            double delta = 0.0;
            double deltaC = 0.0;
            double deltaLast = 0.0;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                kkt.Fill(n, ns, m, w, jac, sigmaX, sigmaS, slackOf, delta, deltaC);

                var status = kkt.Factorize(out int negative, out int zero);

                if (status == SymSolverStatus.Success && negative == m && zero == 0)
                {
                    var sol = kkt.Solve(rhs);

                    dx = new double[n];
                    ds = new double[ns];
                    dy = new double[m];

                    Array.Copy(sol, 0, dx, 0, n);
                    Array.Copy(sol, n, ds, 0, ns);
                    Array.Copy(sol, n + ns, dy, 0, m);

                    return delta;
                }

                // Too many zero eigenvalues means the constraint block is rank deficient, which
                // is what delta_c is for; otherwise raise delta on the primal block.
                if (zero > 0 || status != SymSolverStatus.Success)
                {
                    deltaC = DeltaCBar * Math.Pow(mu, 0.25);
                }

                if (delta == 0.0)
                {
                    delta = deltaLast == 0.0 ? Delta0 : Math.Max(DeltaMin, KappaMinus * deltaLast);
                }
                else
                {
                    delta *= deltaLast == 0.0 ? KappaPlusBar : KappaPlus;
                }

                if (delta > 1e40) break;
            }

            dx = Array.Empty<double>();
            ds = Array.Empty<double>();
            dy = Array.Empty<double>();

            return double.NaN;
        }


        /// <summary>
        /// The restoration phase. Minimises the constraint violation on its own, over the same
        /// variables and their same bounds, and overwrites x and s when it finds a point whose
        /// violation is materially smaller than the one that could not be left. Returns false
        /// when it cannot, which is the honest end of the solve.
        /// </summary>
        private bool Restore(INlpConstrained nlp, int n, int ns, int m, int[] slackOf, double[] cl,
                             double[] xl, double[] xu, double[] sl, double[] su,
                             double[] x, double[] s, double theta)
        {
            var feasibility = new FeasibilityNlp(nlp, n, ns, m, slackOf, cl, xl, xu, sl, su, x, s);

            var options = new SolverOptions
            {
                // Feasibility only has to improve, not converge: the main iteration takes over
                // again as soon as there is a point the filter accepts.
                Tolerance = Math.Max(_opt.Tolerance, 1e-10),
                MaxIterations = Math.Max(50, _opt.MaxIterations / 10),
                MuStrategy = _opt.MuStrategy,
                HessianApproximation = _opt.HessianApproximation,
                LimitedMemoryMaxHistory = _opt.LimitedMemoryMaxHistory,
                BoundInf = _opt.BoundInf,
                CollectIterationLog = false
            };

            var result = new InteriorPointSolver(options).Solve(feasibility);

            if (result.X.Length != n + ns) return false;

            // 2 * f is the sum of squares, and theta is the 1-norm; comparing the 2-norms of the
            // two residuals is the like-for-like test.
            double before = Math.Sqrt(2.0 * feasibility.EvalF(Concatenate(n, ns, x, s)));
            double after = Math.Sqrt(2.0 * result.ObjValue);

            if (!(after < KappaRestoration * Math.Max(before, 1e-300))) return false;

            Array.Copy(result.X, 0, x, 0, n);
            Array.Copy(result.X, n, s, 0, ns);

            return true;
        }

        private static double[] Concatenate(int n, int ns, double[] x, double[] s)
        {
            var v = new double[n + ns];
            Array.Copy(x, 0, v, 0, n);
            Array.Copy(s, 0, v, n, ns);
            return v;
        }

        private static void LagrangianGradient(int n, int m, double[] grad, double[] jac, double[] y, double[] result)
        {
            for (int i = 0; i < n; i++)
            {
                double v = grad[i];
                for (int r = 0; r < m; r++) v += jac[r * n + i] * y[r];
                result[i] = v;
            }
        }

        private static double Theta(int m, double[] g, double[] s, int[] slackOf, double[] cl)
        {
            double t = 0.0;

            for (int r = 0; r < m; r++)
            {
                int k = slackOf[r];
                t += Math.Abs(g[r] - (k >= 0 ? s[k] : cl[r]));
            }

            return t;
        }

        private static double Barrier(int n, int ns, double f, double[] x, double[] s,
                                      double[] xl, double[] xu, bool[] hasXl, bool[] hasXu,
                                      double[] sl, double[] su, bool[] hasSl, bool[] hasSu, double mu)
        {
            double phi = f;

            for (int i = 0; i < n; i++)
            {
                if (hasXl[i]) phi -= mu * Math.Log(Math.Max(x[i] - xl[i], 1e-300));
                if (hasXu[i]) phi -= mu * Math.Log(Math.Max(xu[i] - x[i], 1e-300));
            }

            for (int k = 0; k < ns; k++)
            {
                if (hasSl[k]) phi -= mu * Math.Log(Math.Max(s[k] - sl[k], 1e-300));
                if (hasSu[k]) phi -= mu * Math.Log(Math.Max(su[k] - s[k], 1e-300));
            }

            return phi;
        }

        private static double DirectionalDerivative(int n, int ns, double[] grad, double[] dx, double[] ds,
                                                    double[] x, double[] s,
                                                    double[] xl, double[] xu, bool[] hasXl, bool[] hasXu,
                                                    double[] sl, double[] su, bool[] hasSl, bool[] hasSu,
                                                    double mu)
        {
            double d = 0.0;

            for (int i = 0; i < n; i++)
            {
                double v = grad[i];
                if (hasXl[i]) v -= mu / (x[i] - xl[i]);
                if (hasXu[i]) v += mu / (xu[i] - x[i]);
                d += v * dx[i];
            }

            for (int k = 0; k < ns; k++)
            {
                double v = 0.0;
                if (hasSl[k]) v -= mu / (s[k] - sl[k]);
                if (hasSu[k]) v += mu / (su[k] - s[k]);
                d += v * ds[k];
            }

            return d;
        }

        /// <summary>The filter test of Waechter and Biegler, section 2.3.</summary>
        private static bool Acceptable(List<(double Theta, double Phi)> filter,
                                       double theta, double phi, double thetaTrial, double phiTrial,
                                       double dPhi, double alpha, double thetaMin)
        {
            foreach (var entry in filter)
            {
                if (thetaTrial >= entry.Theta && phiTrial >= entry.Phi) return false;
            }

            bool switching = dPhi < 0.0 &&
                             alpha * Math.Pow(-dPhi, SPhi) > DeltaSwitch * Math.Pow(theta, STheta);

            if (theta <= thetaMin && switching)
            {
                // In the neighbourhood of feasibility a step has to buy objective, by Armijo.
                return phiTrial <= phi + Eta * alpha * dPhi;
            }

            return thetaTrial <= (1.0 - GammaTheta) * theta ||
                   phiTrial <= phi - GammaPhi * theta;
        }

        private static void PushInside(int n, double[] v, double[] lo, double[] hi, bool[] hasLo, bool[] hasHi)
        {
            const double push = 0.01;

            for (int i = 0; i < n; i++)
            {
                if (hasLo[i] && hasHi[i])
                {
                    double span = hi[i] - lo[i];
                    double p = Math.Min(push, push * span);
                    v[i] = Math.Min(Math.Max(v[i], lo[i] + p), hi[i] - p);
                }
                else if (hasLo[i])
                {
                    v[i] = Math.Max(v[i], lo[i] + push * Math.Max(1.0, Math.Abs(lo[i])));
                }
                else if (hasHi[i])
                {
                    v[i] = Math.Min(v[i], hi[i] - push * Math.Max(1.0, Math.Abs(hi[i])));
                }
            }
        }

        private static double FractionToBoundary(int n, double[] v, double[] d,
                                                 double[] lo, double[] hi, bool[] hasLo, bool[] hasHi, double tau)
        {
            double alpha = 1.0;

            for (int i = 0; i < n; i++)
            {
                if (hasLo[i] && d[i] < 0.0)
                {
                    alpha = Math.Min(alpha, -tau * (v[i] - lo[i]) / d[i]);
                }

                if (hasHi[i] && d[i] > 0.0)
                {
                    alpha = Math.Min(alpha, tau * (hi[i] - v[i]) / d[i]);
                }
            }

            return alpha;
        }

        private static double FractionToBoundaryDual(int n, double[] z, double[] dz, double tau)
        {
            double alpha = 1.0;

            for (int i = 0; i < n; i++)
            {
                if (dz[i] < 0.0 && z[i] > 0.0) alpha = Math.Min(alpha, -tau * z[i] / dz[i]);
            }

            return alpha;
        }

        private static void BoundMultiplierStep(int n, double[] v, double[] d,
                                                double[] lo, double[] hi, bool[] hasLo, bool[] hasHi,
                                                double[] zLo, double[] zHi, double mu,
                                                double[] dzLo, double[] dzHi)
        {
            for (int i = 0; i < n; i++)
            {
                if (hasLo[i])
                {
                    double gap = Math.Max(v[i] - lo[i], Tiny);
                    dzLo[i] = (mu - zLo[i] * gap - zLo[i] * d[i]) / gap;
                }

                if (hasHi[i])
                {
                    double gap = Math.Max(hi[i] - v[i], Tiny);
                    dzHi[i] = (mu - zHi[i] * gap + zHi[i] * d[i]) / gap;
                }
            }
        }

        private static void UpdateBoundMultipliers(int n, double[] zLo, double[] dzLo, double[] zHi, double[] dzHi,
                                                   double alphaZ, double[] v, double[] lo, double[] hi,
                                                   bool[] hasLo, bool[] hasHi, double mu)
        {
            for (int i = 0; i < n; i++)
            {
                if (hasLo[i])
                {
                    zLo[i] += alphaZ * dzLo[i];
                    double gap = Math.Max(v[i] - lo[i], Tiny);
                    zLo[i] = Math.Min(Math.Max(zLo[i], mu / (gap * 1e10)), 1e10 * mu / gap);
                }

                if (hasHi[i])
                {
                    zHi[i] += alphaZ * dzHi[i];
                    double gap = Math.Max(hi[i] - v[i], Tiny);
                    zHi[i] = Math.Min(Math.Max(zHi[i], mu / (gap * 1e10)), 1e10 * mu / gap);
                }
            }
        }

        /// <summary>
        /// The LOQO oracle, over the complementarity of the bounds on x and on the slacks
        /// together, which is what makes mu adaptive in the constrained case too.
        /// </summary>
        private double AdaptiveMu(int n, int ns, double[] x, double[] s,
                                  double[] zL, double[] zU, double[] vL, double[] vU,
                                  double[] xl, double[] xu, bool[] hasXl, bool[] hasXu,
                                  double[] sl, double[] su, bool[] hasSl, bool[] hasSu)
        {
            double sum = 0.0, min = double.MaxValue;
            int count = 0;

            void Take(double c)
            {
                sum += c;
                if (c < min) min = c;
                count++;
            }

            for (int i = 0; i < n; i++)
            {
                if (hasXl[i]) Take(zL[i] * (x[i] - xl[i]));
                if (hasXu[i]) Take(zU[i] * (xu[i] - x[i]));
            }

            for (int k = 0; k < ns; k++)
            {
                if (hasSl[k]) Take(vL[k] * (s[k] - sl[k]));
                if (hasSu[k]) Take(vU[k] * (su[k] - s[k]));
            }

            if (count == 0) return Math.Max(_opt.Tolerance / 10.0, _opt.MuInit);

            double avg = sum / count;
            double xi = avg > 0.0 ? min / avg : 1.0;
            double sigma = 0.1 * Math.Pow(Math.Min(0.05 * (1.0 - xi) / Math.Max(xi, 1e-12), 2.0), 3);

            return Math.Max(_opt.Tolerance / 10.0, sigma * avg);
        }

        /// <summary>Scaled optimality error, Waechter and Biegler equations 5 and 6.</summary>
        private double OptimalityError(int n, int ns, int m, double[] x, double[] s, double[] g,
                                       double[] grad, double[] jac, double[] y,
                                       double[] zL, double[] zU, double[] vL, double[] vU,
                                       double[] xl, double[] xu, bool[] hasXl, bool[] hasXu,
                                       double[] sl, double[] su, bool[] hasSl, bool[] hasSu,
                                       int[] slackOf, double[] cl, double mu)
        {
            double multSum = 0.0;
            int multCount = 0;

            for (int i = 0; i < n; i++)
            {
                if (hasXl[i]) { multSum += Math.Abs(zL[i]); multCount++; }
                if (hasXu[i]) { multSum += Math.Abs(zU[i]); multCount++; }
            }

            for (int k = 0; k < ns; k++)
            {
                if (hasSl[k]) { multSum += Math.Abs(vL[k]); multCount++; }
                if (hasSu[k]) { multSum += Math.Abs(vU[k]); multCount++; }
            }

            for (int r = 0; r < m; r++) { multSum += Math.Abs(y[r]); multCount++; }

            double sMax = _opt.SMax;
            double sD = multCount == 0 ? 1.0 : Math.Max(sMax, multSum / multCount) / sMax;

            double boundSum = 0.0;
            int boundCount = 0;

            for (int i = 0; i < n; i++)
            {
                if (hasXl[i]) { boundSum += Math.Abs(zL[i]); boundCount++; }
                if (hasXu[i]) { boundSum += Math.Abs(zU[i]); boundCount++; }
            }

            for (int k = 0; k < ns; k++)
            {
                if (hasSl[k]) { boundSum += Math.Abs(vL[k]); boundCount++; }
                if (hasSu[k]) { boundSum += Math.Abs(vU[k]); boundCount++; }
            }

            double sC = boundCount == 0 ? 1.0 : Math.Max(sMax, boundSum / boundCount) / sMax;

            double dual = 0.0;

            for (int i = 0; i < n; i++)
            {
                double v = grad[i];
                for (int r = 0; r < m; r++) v += jac[r * n + i] * y[r];
                if (hasXl[i]) v -= zL[i];
                if (hasXu[i]) v += zU[i];
                dual = Math.Max(dual, Math.Abs(v));
            }

            for (int r = 0; r < m; r++)
            {
                int k = slackOf[r];
                if (k < 0) continue;
                double v = -y[r];
                if (hasSl[k]) v -= vL[k];
                if (hasSu[k]) v += vU[k];
                dual = Math.Max(dual, Math.Abs(v));
            }

            double primal = 0.0;

            for (int r = 0; r < m; r++)
            {
                int k = slackOf[r];
                primal = Math.Max(primal, Math.Abs(g[r] - (k >= 0 ? s[k] : cl[r])));
            }

            double compl = 0.0;

            for (int i = 0; i < n; i++)
            {
                if (hasXl[i]) compl = Math.Max(compl, Math.Abs(zL[i] * (x[i] - xl[i]) - mu));
                if (hasXu[i]) compl = Math.Max(compl, Math.Abs(zU[i] * (xu[i] - x[i]) - mu));
            }

            for (int k = 0; k < ns; k++)
            {
                if (hasSl[k]) compl = Math.Max(compl, Math.Abs(vL[k] * (s[k] - sl[k]) - mu));
                if (hasSu[k]) compl = Math.Max(compl, Math.Abs(vU[k] * (su[k] - s[k]) - mu));
            }

            return Math.Max(dual / sD, Math.Max(primal, compl / sC));
        }

        private bool Record(List<IterationInfo>? log, in IterationInfo info)
        {
            log?.Add(info);
            if (_opt.LogWriter != null) _opt.LogWriter.WriteLine(IpoptLog.Row(info));
            return _opt.IterationCallback == null || _opt.IterationCallback(info);
        }

        private static SolveResult Fail(SolveStatus status)
        {
            return new SolveResult { Status = status };
        }

        private static SolveResult Finish(SolveStatus status, double[] x, double f, int iter, double err,
                                          List<IterationInfo>? log)
        {
            return new SolveResult
            {
                Status = status,
                X = (double[])x.Clone(),
                ObjValue = f,
                Iterations = iter,
                OptimalityError = err,
                IterationLog = (IReadOnlyList<IterationInfo>)log ?? Array.Empty<IterationInfo>()
            };
        }
    }
}
