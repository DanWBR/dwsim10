// The augmented system of the constrained solver, held in the lower-triangular triplet form the
// linear solver wants. The structure is fixed once, because it never changes between iterations:
// only the values do, and the Jacobian is taken as dense, which for the sizes involved here
// (a few dozen variables) costs nothing and removes a structure to keep in step.

using System;
using DWSIM.Numerics.Ipopt.Sparse;

namespace DWSIM.Numerics.Ipopt.Core
{
    internal sealed class KktSystem
    {
        private readonly int _dim;
        private readonly IpoptLdlSolver _solver = new IpoptLdlSolver();

        private int[] _row = Array.Empty<int>();
        private int[] _col = Array.Empty<int>();
        private double[] _values = Array.Empty<double>();
        private bool _ready;

        public KktSystem(int dim)
        {
            _dim = dim;
        }

        /// <summary>
        /// Fills the matrix. Lower triangle only, in the order the structure was declared:
        /// the diagonal of the primal block, the diagonal of the slack block, the Jacobian, the
        /// slack coupling and the diagonal of the constraint block.
        /// </summary>
        public void Fill(int n, int ns, int m, double[,] w, double[] jac,
                         double[] sigmaX, double[] sigmaS, int[] slackOf,
                         double delta, double deltaC)
        {
            if (!_ready) BuildStructure(n, ns, m, slackOf);

            int k = 0;

            // (1,1): W + Sigma_x + delta, lower triangle.
            for (int j = 0; j < n; j++)
            {
                for (int i = j; i < n; i++)
                {
                    double v = w[i, j];
                    if (i == j) v += sigmaX[i] + delta;
                    _values[k++] = v;
                }
            }

            // (2,2): Sigma_s + delta.
            for (int i = 0; i < ns; i++) _values[k++] = sigmaS[i] + delta;

            // (3,1): the Jacobian.
            for (int r = 0; r < m; r++)
            {
                for (int j = 0; j < n; j++) _values[k++] = jac[r * n + j];
            }

            // (3,2): minus the slack selection.
            for (int r = 0; r < m; r++)
            {
                if (slackOf[r] >= 0) _values[k++] = -1.0;
            }

            // (3,3): minus delta_c, the regularization of the constraint block.
            for (int r = 0; r < m; r++) _values[k++] = -deltaC;

            Array.Copy(_values, _solver.GetValuesArray(), _values.Length);
        }

        private void BuildStructure(int n, int ns, int m, int[] slackOf)
        {
            int count = n * (n + 1) / 2 + ns + m * n + CountSlacks(m, slackOf) + m;

            _row = new int[count];
            _col = new int[count];
            _values = new double[count];

            int k = 0;

            for (int j = 0; j < n; j++)
            {
                for (int i = j; i < n; i++)
                {
                    _row[k] = i;
                    _col[k] = j;
                    k++;
                }
            }

            for (int i = 0; i < ns; i++)
            {
                _row[k] = n + i;
                _col[k] = n + i;
                k++;
            }

            for (int r = 0; r < m; r++)
            {
                for (int j = 0; j < n; j++)
                {
                    _row[k] = n + ns + r;
                    _col[k] = j;
                    k++;
                }
            }

            for (int r = 0; r < m; r++)
            {
                int slack = slackOf[r];
                if (slack < 0) continue;
                _row[k] = n + ns + r;
                _col[k] = n + slack;
                k++;
            }

            for (int r = 0; r < m; r++)
            {
                _row[k] = n + ns + r;
                _col[k] = n + ns + r;
                k++;
            }

            var status = _solver.InitializeStructure(_dim, count, _row, _col);

            if (status != SymSolverStatus.Success)
            {
                throw new InvalidOperationException("the augmented system's structure was rejected: " + status);
            }

            _ready = true;
        }

        private static int CountSlacks(int m, int[] slackOf)
        {
            int c = 0;
            for (int r = 0; r < m; r++) if (slackOf[r] >= 0) c++;
            return c;
        }

        /// <summary>Factorizes and reports the inertia, without asking the solver to judge it.</summary>
        public SymSolverStatus Factorize(out int negative, out int zero)
        {
            var scratch = new double[_dim];
            var status = _solver.MultiSolve(true, 1, scratch, false, 0);

            negative = _solver.Inertia.Negative;
            zero = _solver.Inertia.Zero;

            return status;
        }

        /// <summary>Solves with the factorization already in hand.</summary>
        public double[] Solve(double[] rhs)
        {
            var b = (double[])rhs.Clone();
            _solver.MultiSolve(false, 1, b, false, 0);
            return b;
        }
    }
}
