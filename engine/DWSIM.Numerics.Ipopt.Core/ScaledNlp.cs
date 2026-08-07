// Gradient-based scaling, which is what Ipopt does by default (nlp_scaling_method=gradient-based)
// and which the constrained solver cannot do without.
//
// The objective is multiplied by min(1, g_max / ||grad f(x0)||_inf) and each constraint row by
// min(1, g_max / ||row of A(x0)||_inf), both computed once at the starting point. That leaves
// every gradient no larger than g_max, so the multipliers, the complementarity products the mu
// oracle averages, and the barrier parameter all live in the same range whatever units the caller
// wrote its problem in.
//
// The scaling never changes the solution: scaling the objective by a positive constant does not
// move its minimiser, and scaling a constraint row scales its bounds and its multiplier with it.
// The bounds are scaled here so the caller never sees any of it.

using System;

namespace DWSIM.Numerics.Ipopt.Core
{
    internal sealed class ScaledNlp : INlpConstrained, INlpHessian
    {
        private double[] _lambdaBuffer;

        private readonly INlpConstrained _inner;
        private readonly int _n;
        private readonly int _m;
        private readonly double _objScale;
        private readonly double[] _rowScale;
        private readonly double[] _gBuffer;
        private readonly double[] _jacBuffer;

        private ScaledNlp(INlpConstrained inner, int n, int m, double objScale, double[] rowScale)
        {
            _inner = inner;
            _n = n;
            _m = m;
            _objScale = objScale;
            _rowScale = rowScale;
            _gBuffer = new double[m];
            _jacBuffer = new double[m * n];
        }

        /// <summary>
        /// Measures the gradients at the starting point and returns the problem seen through the
        /// resulting scaling. When nothing needs scaling the factors are all one, and the wrapper
        /// is then a pass-through.
        /// </summary>
        public static ScaledNlp Wrap(INlpConstrained inner, int n, int m, double[] x0, double gMax)
        {
            var grad = new double[n];
            inner.EvalGradF(x0, grad);

            double gradMax = 0.0;
            for (int i = 0; i < n; i++) gradMax = Math.Max(gradMax, Math.Abs(grad[i]));

            double objScale = gradMax > gMax ? gMax / gradMax : 1.0;

            var jac = new double[m * n];
            inner.EvalJacG(x0, jac);

            var rowScale = new double[m];

            for (int r = 0; r < m; r++)
            {
                double rowMax = 0.0;
                for (int j = 0; j < n; j++) rowMax = Math.Max(rowMax, Math.Abs(jac[r * n + j]));

                rowScale[r] = rowMax > gMax ? gMax / rowMax : 1.0;
            }

            return new ScaledNlp(inner, n, m, objScale, rowScale);
        }

        /// <summary>The factor the objective was multiplied by, so a caller can undo it.</summary>
        public double ObjectiveScale => _objScale;

        /// <summary>The factor each constraint row was multiplied by.</summary>
        public double[] RowScale => _rowScale;

        public int N => _n;

        public int M => _m;

        public void GetBounds(double[] xl, double[] xu) => _inner.GetBounds(xl, xu);

        public void GetStartingPoint(double[] x) => _inner.GetStartingPoint(x);

        public void GetConstraintBounds(double[] cl, double[] cu)
        {
            _inner.GetConstraintBounds(cl, cu);

            for (int r = 0; r < _m; r++)
            {
                // An infinite bound stays infinite: scaling it would turn it into a real one.
                if (Math.Abs(cl[r]) < 1e19) cl[r] *= _rowScale[r];
                if (Math.Abs(cu[r]) < 1e19) cu[r] *= _rowScale[r];
            }
        }

        public double EvalF(double[] x) => _objScale * _inner.EvalF(x);

        public void EvalGradF(double[] x, double[] gradf)
        {
            _inner.EvalGradF(x, gradf);

            if (_objScale == 1.0) return;

            for (int i = 0; i < _n; i++) gradf[i] *= _objScale;
        }

        public void EvalG(double[] x, double[] g)
        {
            _inner.EvalG(x, _gBuffer);

            for (int r = 0; r < _m; r++) g[r] = _rowScale[r] * _gBuffer[r];
        }

        /// <summary>
        /// The scaled Lagrangian is sigma*f + sum_r (lambda_r * rho_r) * g_r, so the objective
        /// factor carries the objective scale and each multiplier carries its own row scale. That
        /// is the same identity that lets the scaling leave the solution alone.
        /// </summary>
        public bool TryEvalHessian(double[] x, double objFactor, double[] lambda, double[] w)
        {
            if (_inner is not INlpHessian inner) return false;

            if (_lambdaBuffer == null) _lambdaBuffer = new double[_m];

            for (int r = 0; r < _m; r++) _lambdaBuffer[r] = lambda[r] * _rowScale[r];

            return inner.TryEvalHessian(x, objFactor * _objScale, _lambdaBuffer, w);
        }

        public void EvalJacG(double[] x, double[] jac)
        {
            _inner.EvalJacG(x, _jacBuffer);

            for (int r = 0; r < _m; r++)
            {
                double sc = _rowScale[r];
                for (int j = 0; j < _n; j++) jac[r * _n + j] = sc * _jacBuffer[r * _n + j];
            }
        }
    }
}
