// Managed implementation of Ipopt's SparseSymLinearSolverInterface contract.
//
// It accepts the symmetric matrix in TRIPLET form (lower triangle, like MA27):
// InitializeStructure sets the sparsity pattern once; the caller then repeatedly
// writes values into the buffer from GetValuesArray and calls MultiSolve. The
// value ordering matches the triplet order given to InitializeStructure, and
// duplicate (row,col) triplets are summed -- exactly as Ipopt requires.
//
// Internally the triplets are converted to upper-triangular CSC and factorized
// either by the sparse static LDL (QDLDL) or the dense Bunch-Kaufman, dispatched
// by problem size. The status mapping (Success / Singular / WrongInertia) and the
// element-growth guard are what let Ipopt's perturbation handler converge.

using System;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>Mirrors Ipopt's ESymSolverStatus for the values the managed solver returns.</summary>
    public enum SymSolverStatus
    {
        Success,
        Singular,
        WrongInertia,
        FatalError
    }

    /// <summary>Which factorization the managed solver uses.</summary>
    public enum LinearSolverKind
    {
        /// <summary>Dense Bunch-Kaufman for small N, sparse QDLDL otherwise.</summary>
        Auto,
        /// <summary>Always sparse static LDL (QDLDL).</summary>
        Sparse,
        /// <summary>Always dense Bunch-Kaufman.</summary>
        Dense
    }

    /// <summary>
    /// Managed symmetric-indefinite linear solver honoring the Ipopt linear-solver
    /// contract. Provides inertia, so it can drive the primal-dual regularization.
    /// </summary>
    public sealed class IpoptLdlSolver
    {
        private int _n;
        private int _nnz; // number of triplets

        // Upper-triangular CSC structure (unique entries).
        private int[] _ap = Array.Empty<int>();
        private int[] _ai = Array.Empty<int>();
        private int _nUnique;

        // Triplet index k -> position in _ax (duplicates share a position).
        private int[] _map = Array.Empty<int>();

        private double[] _values = Array.Empty<double>(); // triplet-ordered value buffer (caller-filled)
        private double[] _ax = Array.Empty<double>();      // summed CSC values

        private readonly QdldlSolver _sparse = new QdldlSolver();
        private BunchKaufman? _dense;
        private double[,] _denseA = new double[0, 0];
        private bool _useDense;
        private bool _structureReady;

        /// <summary>Factorization strategy. Default is <see cref="LinearSolverKind.Auto"/>.</summary>
        public LinearSolverKind Kind { get; set; } = LinearSolverKind.Auto;

        /// <summary>Below this dimension, Auto uses the dense (robust) Bunch-Kaufman. Default 1000.</summary>
        public int DenseThreshold { get; set; } = 1000;

        /// <summary>Sparse path only: reject a factorization whose max |L| exceeds this (element growth). Default 1e10.</summary>
        public double GrowthLimit { get; set; } = 1e10;

        /// <summary>Sparse path only: reject if min |D| falls below this fraction of max |A|. Default 1e-13.</summary>
        public double MinPivotRatio { get; set; } = 1e-13;

        /// <summary>Inertia of the most recent factorization.</summary>
        public Inertia Inertia { get; private set; }

        /// <summary>Whether the last factorization used the dense path.</summary>
        public bool UsedDense => _useDense;

        /// <summary>
        /// Number of negative eigenvalues as Ipopt expects it (following MA27, zero
        /// eigenvalues are folded in as negative so the inertia check is conservative).
        /// </summary>
        public int NumberOfNegEVals => Inertia.Negative + Inertia.Zero;

        /// <summary>True: this solver computes inertia (always).</summary>
        public bool ProvidesInertia => true;

        /// <summary>No pivot tolerance to raise in a static LDL, so quality cannot be increased.</summary>
        public bool IncreaseQuality() => false;

        /// <summary>
        /// Sets the sparsity pattern from lower-triangular triplets (0-based). Call once.
        /// </summary>
        /// <param name="n">Matrix dimension.</param>
        /// <param name="nnz">Number of triplets.</param>
        /// <param name="irn">Row indices, length nnz.</param>
        /// <param name="jcn">Column indices, length nnz.</param>
        public SymSolverStatus InitializeStructure(int n, int nnz, int[] irn, int[] jcn)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            if (irn is null) throw new ArgumentNullException(nameof(irn));
            if (jcn is null) throw new ArgumentNullException(nameof(jcn));
            if (irn.Length < nnz || jcn.Length < nnz) throw new ArgumentException("irn/jcn shorter than nnz.");

            _n = n;
            _nnz = nnz;
            _structureReady = false;

            // Normalize each triplet to the upper triangle: col = max(r,c), row = min(r,c).
            var col = new int[nnz];
            var row = new int[nnz];
            for (int k = 0; k < nnz; k++)
            {
                int r = irn[k];
                int c = jcn[k];
                if ((uint)r >= (uint)n || (uint)c >= (uint)n)
                    throw new ArgumentException($"Triplet {k} index out of range: ({r},{c}) for n={n}.");
                if (r >= c) { col[k] = r; row[k] = c; }
                else { col[k] = c; row[k] = r; }
            }

            // Sort triplet indices by (col, row) to group duplicates.
            var order = new int[nnz];
            for (int k = 0; k < nnz; k++) order[k] = k;
            Array.Sort(order, (x, y) =>
            {
                int dc = col[x].CompareTo(col[y]);
                return dc != 0 ? dc : row[x].CompareTo(row[y]);
            });

            if (_map.Length < nnz) _map = new int[nnz];

            // Assign a unique CSC position to each distinct (col,row); sum duplicates there.
            var uCol = new System.Collections.Generic.List<int>(nnz);
            var uRow = new System.Collections.Generic.List<int>(nnz);
            int pos = -1;
            int prevCol = -1, prevRow = -1;
            for (int idx = 0; idx < nnz; idx++)
            {
                int k = order[idx];
                if (pos < 0 || col[k] != prevCol || row[k] != prevRow)
                {
                    pos++;
                    uCol.Add(col[k]);
                    uRow.Add(row[k]);
                    prevCol = col[k];
                    prevRow = row[k];
                }
                _map[k] = pos;
            }
            _nUnique = pos + 1;

            // Build column pointers and row indices (rows already ascending within a column).
            if (_ap.Length < n + 1) _ap = new int[n + 1];
            Array.Clear(_ap, 0, n + 1);
            for (int u = 0; u < _nUnique; u++) _ap[uCol[u] + 1]++;
            for (int j = 0; j < n; j++) _ap[j + 1] += _ap[j];

            if (_ai.Length < _nUnique) _ai = new int[_nUnique];
            for (int u = 0; u < _nUnique; u++) _ai[u] = uRow[u];

            if (_ax.Length < _nUnique) _ax = new double[_nUnique];
            if (_values.Length < nnz) _values = new double[nnz];

            // Every column must carry its diagonal for QDLDL. Verify up front.
            for (int j = 0; j < n; j++)
            {
                bool hasDiag = false;
                for (int p = _ap[j]; p < _ap[j + 1]; p++)
                {
                    if (_ai[p] == j) { hasDiag = true; break; }
                }
                if (!hasDiag)
                    return SymSolverStatus.FatalError; // structurally missing diagonal
            }

            _useDense = ChooseDense(n);
            if (_useDense)
            {
                _dense ??= new BunchKaufman();
                if (_denseA.GetLength(0) < n) _denseA = new double[n, n];
            }
            else
            {
                if (_sparse.AnalyzeStructure(n, _ap, _ai) != FactorStatus.Success)
                    return SymSolverStatus.FatalError;
            }

            _structureReady = true;
            return SymSolverStatus.Success;
        }

        /// <summary>
        /// The buffer the caller fills with matrix values in triplet order (length nnz)
        /// before a MultiSolve with newMatrix = true.
        /// </summary>
        public double[] GetValuesArray()
        {
            if (!_structureReady) throw new InvalidOperationException("Call InitializeStructure first.");
            return _values;
        }

        /// <summary>
        /// Factorizes (if <paramref name="newMatrix"/>) and solves for one or more right-hand sides.
        /// </summary>
        /// <param name="newMatrix">Values changed since the last solve; refactorize.</param>
        /// <param name="nrhs">Number of right-hand sides.</param>
        /// <param name="rhs">Right-hand sides, one after another (length nrhs*N); overwritten with the solutions.</param>
        /// <param name="checkNegEVals">Whether to verify the negative-eigenvalue count.</param>
        /// <param name="numberOfNegEVals">Expected number of negative eigenvalues.</param>
        public SymSolverStatus MultiSolve(bool newMatrix, int nrhs, double[] rhs, bool checkNegEVals, int numberOfNegEVals)
        {
            if (!_structureReady) throw new InvalidOperationException("Call InitializeStructure first.");
            if (rhs is null) throw new ArgumentNullException(nameof(rhs));
            if (rhs.Length < nrhs * _n) throw new ArgumentException("rhs shorter than nrhs*N.", nameof(rhs));

            if (newMatrix)
            {
                SymSolverStatus fs = Factorize();
                if (fs != SymSolverStatus.Success) return fs;

                if (checkNegEVals && NumberOfNegEVals != numberOfNegEVals)
                    return SymSolverStatus.WrongInertia;
            }

            for (int r = 0; r < nrhs; r++)
            {
                var single = new double[_n];
                Array.Copy(rhs, r * _n, single, 0, _n);
                if (_useDense) _dense!.Solve(single);
                else _sparse.Solve(single);
                Array.Copy(single, 0, rhs, r * _n, _n);
            }

            return SymSolverStatus.Success;
        }

        private SymSolverStatus Factorize()
        {
            // Scatter triplet values into the summed CSC / dense representation.
            double maxAbsA = 0.0;
            if (_useDense)
            {
                Array.Clear(_denseA, 0, _denseA.Length);
                // Rebuild dense symmetric matrix from summed CSC entries.
                Array.Clear(_ax, 0, _nUnique);
                for (int k = 0; k < _nnz; k++) _ax[_map[k]] += _values[k];
                for (int j = 0; j < _n; j++)
                {
                    for (int p = _ap[j]; p < _ap[j + 1]; p++)
                    {
                        int i = _ai[p]; // i <= j (upper); place in lower triangle for BK
                        double v = _ax[p];
                        _denseA[j, i] = v;
                        _denseA[i, j] = v;
                        double av = Math.Abs(v);
                        if (av > maxAbsA) maxAbsA = av;
                    }
                }
                FactorStatus st = _dense!.Factorize(_denseA);
                Inertia = _dense.Inertia;
                return st == FactorStatus.Success ? SymSolverStatus.Success : SymSolverStatus.Singular;
            }

            Array.Clear(_ax, 0, _nUnique);
            for (int k = 0; k < _nnz; k++) _ax[_map[k]] += _values[k];
            for (int u = 0; u < _nUnique; u++)
            {
                double av = Math.Abs(_ax[u]);
                if (av > maxAbsA) maxAbsA = av;
            }

            FactorStatus fst = _sparse.Factorize(_ax);
            if (fst != FactorStatus.Success)
            {
                Inertia = default;
                return SymSolverStatus.Singular;
            }

            // Element-growth guard: a static LDL can report inertia read from numerical
            // noise when pivots are tiny. Treat such factorizations as singular so the
            // perturbation handler increases delta_x instead of trusting a bad inertia.
            if (_sparse.MaxAbsL > GrowthLimit ||
                _sparse.MinAbsDiag < MinPivotRatio * Math.Max(1.0, maxAbsA))
            {
                Inertia = _sparse.Inertia;
                return SymSolverStatus.Singular;
            }

            Inertia = _sparse.Inertia;
            return SymSolverStatus.Success;
        }

        private bool ChooseDense(int n) => Kind switch
        {
            LinearSolverKind.Dense => true,
            LinearSolverKind.Sparse => false,
            _ => n <= DenseThreshold
        };
    }
}
