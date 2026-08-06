// High-level wrapper around the QDLDL core (Qdldl.cs). It owns the working
// memory, runs the symbolic analysis once for a fixed sparsity pattern, and
// then factorizes/solves repeatedly for changing values -- exactly the access
// pattern Ipopt's SparseSymLinearSolverInterface expects (InitializeStructure
// once, MultiSolve many). Inertia is read from the signs of D (Sylvester).

using System;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>Outcome of a numeric factorization.</summary>
    public enum FactorStatus
    {
        /// <summary>Factorization completed; <see cref="QdldlSolver.Inertia"/> is valid.</summary>
        Success,

        /// <summary>A diagonal pivot hit exactly zero; the matrix is (numerically) singular.</summary>
        ZeroPivot,

        /// <summary>The supplied structure is not valid upper-triangular CSC with a full diagonal.</summary>
        InvalidStructure,

        /// <summary>A Cholesky factorization found the matrix not positive definite.</summary>
        NotPositiveDefinite
    }

    /// <summary>Number of positive/negative/zero eigenvalues of the factorized matrix.</summary>
    public readonly struct Inertia
    {
        public Inertia(int positive, int negative, int zero)
        {
            Positive = positive;
            Negative = negative;
            Zero = zero;
        }

        public int Positive { get; }
        public int Negative { get; }
        public int Zero { get; }

        public override string ToString() => $"(+{Positive}, -{Negative}, 0:{Zero})";
    }

    /// <summary>
    /// Managed LDL^T solver for symmetric quasi-definite systems, backed by QDLDL.
    /// Supply the <b>upper triangle</b> (with a full diagonal) in CSC form.
    /// </summary>
    public sealed class QdldlSolver
    {
        private int _n;
        private int[] _ap = Array.Empty<int>();
        private int[] _ai = Array.Empty<int>();

        // Symbolic products.
        private int[] _lnz = Array.Empty<int>();
        private int[] _etree = Array.Empty<int>();
        private int _sumLnz;

        // Numeric products.
        private int[] _lp = Array.Empty<int>();
        private int[] _li = Array.Empty<int>();
        private double[] _lx = Array.Empty<double>();
        private double[] _d = Array.Empty<double>();
        private double[] _dinv = Array.Empty<double>();

        // Scratch.
        private int[] _work = Array.Empty<int>();
        private bool[] _bwork = Array.Empty<bool>();
        private int[] _iwork = Array.Empty<int>();
        private double[] _fwork = Array.Empty<double>();

        private bool _symbolicDone;

        /// <summary>Matrix dimension.</summary>
        public int N => _n;

        /// <summary>Inertia from the most recent successful <see cref="Factorize"/>.</summary>
        public Inertia Inertia { get; private set; }

        /// <summary>Diagonal D from the most recent successful factorization (length N). Do not mutate.</summary>
        public double[] D => _d;

        /// <summary>Largest magnitude among the L factor entries in the last factorization (element-growth signal).</summary>
        public double MaxAbsL { get; private set; }

        /// <summary>Smallest magnitude among the D pivots in the last factorization.</summary>
        public double MinAbsDiag { get; private set; }

        /// <summary>
        /// Runs the symbolic analysis for a fixed sparsity pattern. Call once per
        /// structure; the arrays are retained by reference for later factorizations,
        /// so their contents must stay valid (values in <c>ax</c> may change).
        /// </summary>
        public FactorStatus AnalyzeStructure(int n, int[] ap, int[] ai)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            if (ap is null) throw new ArgumentNullException(nameof(ap));
            if (ai is null) throw new ArgumentNullException(nameof(ai));
            if (ap.Length < n + 1) throw new ArgumentException("ap must have length n+1.", nameof(ap));

            _n = n;
            _ap = ap;
            _ai = ai;

            EnsureLength(ref _lnz, n);
            EnsureLength(ref _etree, n);
            EnsureLength(ref _work, n);

            _sumLnz = Qdldl.Etree(n, ap, ai, _work, _lnz, _etree);
            if (_sumLnz < 0)
            {
                _symbolicDone = false;
                return FactorStatus.InvalidStructure;
            }

            EnsureLength(ref _lp, n + 1);
            EnsureLength(ref _li, _sumLnz);
            EnsureLength(ref _lx, _sumLnz);
            EnsureLength(ref _d, n);
            EnsureLength(ref _dinv, n);

            EnsureLength(ref _bwork, n);
            EnsureLength(ref _iwork, 3 * n);
            EnsureLength(ref _fwork, n);

            _symbolicDone = true;
            return FactorStatus.Success;
        }

        /// <summary>
        /// Numerically factorizes the matrix whose values are <paramref name="ax"/>
        /// (matching the row indices given to <see cref="AnalyzeStructure"/>).
        /// On success, <see cref="Inertia"/> is updated.
        /// </summary>
        public FactorStatus Factorize(double[] ax)
        {
            if (!_symbolicDone) throw new InvalidOperationException("Call AnalyzeStructure first.");
            if (ax is null) throw new ArgumentNullException(nameof(ax));
            if (ax.Length < _ap[_n]) throw new ArgumentException("ax is shorter than the number of nonzeros.", nameof(ax));

            int positive = Qdldl.Factor(
                _n, _ap, _ai, ax,
                _lp, _li, _lx, _d, _dinv,
                _lnz, _etree,
                _bwork, _iwork, _fwork);

            if (positive < 0)
            {
                Inertia = default;
                MaxAbsL = double.PositiveInfinity;
                MinAbsDiag = 0.0;
                return FactorStatus.ZeroPivot;
            }

            // QDLDL aborts on an exact zero pivot, so a success means no zero eigenvalues
            // were seen: negatives are simply the complement of the positives.
            Inertia = new Inertia(positive, _n - positive, 0);

            // Factorization-quality metrics for the element-growth guard.
            double maxL = 0.0;
            int nnzL = _lp[_n];
            for (int i = 0; i < nnzL; i++)
            {
                double v = Math.Abs(_lx[i]);
                if (v > maxL) maxL = v;
            }
            MaxAbsL = maxL;

            double minD = double.PositiveInfinity;
            for (int i = 0; i < _n; i++)
            {
                double v = Math.Abs(_d[i]);
                if (v < minD) minD = v;
            }
            MinAbsDiag = _n > 0 ? minD : 0.0;

            return FactorStatus.Success;
        }

        /// <summary>
        /// Solves A x = b in place using the most recent factorization.
        /// <paramref name="rhs"/> holds b on entry and x on return (length N).
        /// </summary>
        public void Solve(double[] rhs)
        {
            if (rhs is null) throw new ArgumentNullException(nameof(rhs));
            if (rhs.Length < _n) throw new ArgumentException("rhs is shorter than N.", nameof(rhs));
            Qdldl.Solve(_n, _lp, _li, _lx, _dinv, rhs);
        }

        private static void EnsureLength(ref int[] a, int len)
        {
            if (a.Length < len) a = new int[len];
        }

        private static void EnsureLength(ref double[] a, int len)
        {
            if (a.Length < len) a = new double[len];
        }

        private static void EnsureLength(ref bool[] a, int len)
        {
            if (a.Length < len) a = new bool[len];
        }
    }
}
