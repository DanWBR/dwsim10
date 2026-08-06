// INlp adapter for DWSIM's optimization pattern (see DWSIM.Math/IPOPTSolver.vb):
// the caller supplies a scalar objective delegate f(x) and, optionally, a gradient
// delegate; constraints are already folded into f as penalties (so m = 0). When no
// gradient delegate is given, the gradient is computed by central finite
// differences, matching DWSIM's own eval_grad_f (epsilon = 0.001).

using System;

namespace DWSIM.Numerics.Ipopt.Core
{
    /// <summary>
    /// Wraps DWSIM-style objective/gradient delegates as an <see cref="INlp"/>.
    /// Objective: <c>Func&lt;double[], double&gt;</c>; optional gradient:
    /// <c>Func&lt;double[], double[]&gt;</c>. Bounds default to +/- <see cref="BoundInf"/>.
    /// </summary>
    public sealed class DwsimFunctionNlp : INlp
    {
        private readonly Func<double[], double> _f;
        private readonly Func<double[], double[]>? _grad;
        private readonly double[] _x0;
        private readonly double[] _xl;
        private readonly double[] _xu;
        private readonly double _fdStep;
        private readonly double[] _work;

        /// <param name="objective">Scalar objective f(x).</param>
        /// <param name="gradient">Optional gradient; if null, central differences are used.</param>
        /// <param name="startingPoint">Initial variable values (defines N).</param>
        /// <param name="lowerBounds">Lower bounds, or null for none (-inf).</param>
        /// <param name="upperBounds">Upper bounds, or null for none (+inf).</param>
        /// <param name="finiteDifferenceStep">Central-difference step (DWSIM uses 0.001).</param>
        /// <param name="boundInf">Magnitude treated as infinite (DWSIM uses 1e19).</param>
        public DwsimFunctionNlp(
            Func<double[], double> objective,
            Func<double[], double[]>? gradient,
            double[] startingPoint,
            double[]? lowerBounds = null,
            double[]? upperBounds = null,
            double finiteDifferenceStep = 1e-3,
            double boundInf = 1e19)
        {
            _f = objective ?? throw new ArgumentNullException(nameof(objective));
            _grad = gradient;
            if (startingPoint is null) throw new ArgumentNullException(nameof(startingPoint));

            int n = startingPoint.Length;
            _x0 = (double[])startingPoint.Clone();
            _xl = new double[n];
            _xu = new double[n];
            for (int i = 0; i < n; i++)
            {
                _xl[i] = lowerBounds != null ? lowerBounds[i] : -boundInf;
                _xu[i] = upperBounds != null ? upperBounds[i] : boundInf;
            }
            _fdStep = finiteDifferenceStep;
            _work = new double[n];
        }

        public int N => _x0.Length;

        public void GetBounds(double[] xl, double[] xu)
        {
            Array.Copy(_xl, xl, N);
            Array.Copy(_xu, xu, N);
        }

        public void GetStartingPoint(double[] x) => Array.Copy(_x0, x, N);

        public double EvalF(double[] x) => _f(x);

        public void EvalGradF(double[] x, double[] gradf)
        {
            if (_grad != null)
            {
                double[] g = _grad(x);
                Array.Copy(g, gradf, N);
                return;
            }

            // Central finite differences (matches DWSIM's fallback, epsilon = 0.001).
            Array.Copy(x, _work, N);
            for (int j = 0; j < N; j++)
            {
                double xj = _work[j];
                double h = _fdStep;
                _work[j] = xj + h;
                double f2 = _f(_work);
                _work[j] = xj - h;
                double f1 = _f(_work);
                _work[j] = xj;
                gradf[j] = (f2 - f1) / (2.0 * h);
            }
        }
    }
}
