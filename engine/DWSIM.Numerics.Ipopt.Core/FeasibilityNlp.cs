// The problem the restoration phase solves: get back to feasibility, ignoring the objective.
//
// Ipopt writes this as an l1 problem in extra variables p and n. Here it is the least-squares
// form, min 1/2 ||g(x) - s||^2 over the same x and s and their same bounds, because that is a
// bound-constrained problem and the bound-constrained solver in this assembly already exists and
// is tested. The purpose is the same: find a point the filter has never seen, closer to the
// constraint manifold, and hand it back to the main iteration.
//
// The squared form is smooth where the l1 form is not, which suits a quasi-Newton method; what it
// gives up is the exactness of the l1 penalty, and with it the guarantee of finishing at a point
// that is exactly feasible. That does not matter here, because the caller only needs the
// violation reduced enough to escape the filter, not driven to zero.

using System;

namespace DWSIM.Numerics.Ipopt.Core
{
    internal sealed class FeasibilityNlp : INlp
    {
        private readonly INlpConstrained _inner;
        private readonly int _n;
        private readonly int _ns;
        private readonly int _m;
        private readonly int[] _slackOf;
        private readonly double[] _cl;
        private readonly double[] _xl;
        private readonly double[] _xu;
        private readonly double[] _sl;
        private readonly double[] _su;
        private readonly double[] _start;

        private readonly double[] _x;
        private readonly double[] _g;
        private readonly double[] _jac;

        public FeasibilityNlp(INlpConstrained inner, int n, int ns, int m, int[] slackOf,
                              double[] cl, double[] xl, double[] xu, double[] sl, double[] su,
                              double[] x, double[] s)
        {
            _inner = inner;
            _n = n;
            _ns = ns;
            _m = m;
            _slackOf = slackOf;
            _cl = cl;
            _xl = xl;
            _xu = xu;
            _sl = sl;
            _su = su;

            _start = new double[n + ns];
            Array.Copy(x, 0, _start, 0, n);
            Array.Copy(s, 0, _start, n, ns);

            _x = new double[n];
            _g = new double[m];
            _jac = new double[m * n];
        }

        public int N => _n + _ns;

        public void GetBounds(double[] xl, double[] xu)
        {
            Array.Copy(_xl, 0, xl, 0, _n);
            Array.Copy(_xu, 0, xu, 0, _n);
            Array.Copy(_sl, 0, xl, _n, _ns);
            Array.Copy(_su, 0, xu, _n, _ns);
        }

        public void GetStartingPoint(double[] x) => Array.Copy(_start, x, _n + _ns);

        public double EvalF(double[] v)
        {
            Residual(v, out double sum);
            return 0.5 * sum;
        }

        public void EvalGradF(double[] v, double[] gradf)
        {
            var residual = Residual(v, out _);

            Array.Clear(gradf, 0, gradf.Length);

            _inner.EvalJacG(_x, _jac);

            for (int r = 0; r < _m; r++)
            {
                double res = residual[r];
                if (res == 0.0) continue;

                for (int j = 0; j < _n; j++) gradf[j] += res * _jac[r * _n + j];

                int k = _slackOf[r];
                if (k >= 0) gradf[_n + k] -= res;
            }
        }

        private double[] Residual(double[] v, out double sumOfSquares)
        {
            Array.Copy(v, 0, _x, 0, _n);

            _inner.EvalG(_x, _g);

            var residual = new double[_m];
            sumOfSquares = 0.0;

            for (int r = 0; r < _m; r++)
            {
                int k = _slackOf[r];
                double target = k >= 0 ? v[_n + k] : _cl[r];

                residual[r] = _g[r] - target;
                sumOfSquares += residual[r] * residual[r];
            }

            return residual;
        }
    }
}
