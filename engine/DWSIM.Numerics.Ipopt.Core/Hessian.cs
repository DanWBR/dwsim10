// Quasi-Newton Hessian approximations for the interior-point solver. Two flavors:
//   - DenseBfgsHessian: full-memory damped BFGS (updated in place).
//   - LbfgsHessian: limited-memory BFGS with a fixed history (Ipopt default: 6),
//     rebuilt densely from the stored (s,y) pairs each time it is queried. For the
//     small n of the DWSIM problems, forming B densely from the history is cheap
//     and reproduces exactly the limited-memory Hessian Ipopt uses.
//
// Both keep B positive definite via Powell damping so the reduced system stays SPD.

using System;

namespace DWSIM.Numerics.Ipopt.Core
{
    internal interface IHessian
    {
        void Reset(int n);
        void Update(double[] s, double[] y);
        void GetDense(double[,] b);
    }

    /// <summary>Full-memory damped BFGS, stored explicitly and updated in place.</summary>
    internal sealed class DenseBfgsHessian : IHessian
    {
        private int _n;
        private double[,] _b = new double[0, 0];

        public void Reset(int n)
        {
            _n = n;
            if (_b.GetLength(0) < n) _b = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    _b[i, j] = (i == j) ? 1.0 : 0.0;
        }

        public void Update(double[] s, double[] y) => BfgsMath.DampedUpdate(_n, _b, s, y);

        public void GetDense(double[,] b)
        {
            for (int i = 0; i < _n; i++)
                for (int j = 0; j < _n; j++)
                    b[i, j] = _b[i, j];
        }
    }

    /// <summary>Limited-memory BFGS: keeps the last <c>maxHistory</c> (s,y) pairs and
    /// rebuilds B = B0 + damped-BFGS updates on demand. B0 uses Ipopt's scalar1 scaling.</summary>
    internal sealed class LbfgsHessian : IHessian
    {
        private readonly int _maxHistory;
        private int _n;
        private readonly System.Collections.Generic.List<double[]> _s = new();
        private readonly System.Collections.Generic.List<double[]> _y = new();
        private double _sigma = 1.0;

        public LbfgsHessian(int maxHistory)
        {
            _maxHistory = Math.Max(1, maxHistory);
        }

        public void Reset(int n)
        {
            _n = n;
            _s.Clear();
            _y.Clear();
            _sigma = 1.0;
        }

        public void Update(double[] s, double[] y)
        {
            double sy = 0.0, ss = 0.0;
            for (int i = 0; i < _n; i++) { sy += s[i] * y[i]; ss += s[i] * s[i]; }
            if (ss <= 0.0) return;

            // Scalar1 initialization sigma = (s^T y)/(s^T s), guarded to stay positive.
            if (sy > 0.0) _sigma = sy / ss;

            var sc = (double[])s.Clone();
            var yc = (double[])y.Clone();
            _s.Add(sc);
            _y.Add(yc);
            if (_s.Count > _maxHistory) { _s.RemoveAt(0); _y.RemoveAt(0); }
        }

        public void GetDense(double[,] b)
        {
            int n = _n;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    b[i, j] = (i == j) ? _sigma : 0.0;

            for (int k = 0; k < _s.Count; k++)
                BfgsMath.DampedUpdate(n, b, _s[k], _y[k]);
        }
    }

    /// <summary>Shared damped-BFGS rank-two update B := B - (Bss^TB)/(s^TBs) + (yy^T)/(s^Ty), Powell-damped.</summary>
    internal static class BfgsMath
    {
        public static void DampedUpdate(int n, double[,] b, double[] s, double[] y)
        {
            var bs = new double[n];
            double sBs = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = 0.0;
                for (int j = 0; j < n; j++) t += b[i, j] * s[j];
                bs[i] = t;
                sBs += s[i] * t;
            }
            if (sBs <= 0.0) return;

            double sy = 0.0;
            for (int i = 0; i < n; i++) sy += s[i] * y[i];

            var yb = new double[n];
            double theta = 1.0;
            if (sy < 0.2 * sBs) theta = 0.8 * sBs / (sBs - sy);
            for (int i = 0; i < n; i++) yb[i] = theta * y[i] + (1.0 - theta) * bs[i];

            double syb = 0.0;
            for (int i = 0; i < n; i++) syb += s[i] * yb[i];
            if (syb <= 0.0) return;

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    b[i, j] += yb[i] * yb[j] / syb - bs[i] * bs[j] / sBs;
        }
    }
}
