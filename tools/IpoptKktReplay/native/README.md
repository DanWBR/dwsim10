# Native KKT capture (Phase 0 instrumentation)

This folder holds a C++ pass-through linear-solver interface,
`KktDumpSolverInterface`, that wraps a real Ipopt solver (MA57, MA27, MUMPS, …)
and streams every factorized KKT system to a binary `IKKT` file. You then replay
those real systems through the managed C# solver (`Ipopt.Sparse`) to:

1. **validate inertia** — does the managed LDL report the same number of negative
   eigenvalues as MA57 on the *actual* matrices your flowsheets produce?
2. **measure the size distribution** — is `N_KKT` small enough (≲ 2000) that the
   dense Bunch-Kaufman path is enough, or is the sparse path required?

No native↔managed interop is involved: the C++ side only writes a file, the C#
side only reads it.

## 1. Add the files to the native build

Copy both files into the Ipopt source tree:

```bash
cp dotnet/native/IpKktDumpSolverInterface.hpp src/Algorithm/LinearSolvers/
cp dotnet/native/IpKktDumpSolverInterface.cpp src/Algorithm/LinearSolvers/
```

In `src/Makefile.am`, add the source next to the other linear-solver interfaces
(unconditionally — it only depends on the base interface):

```make
libipopt_la_SOURCES += \
	Algorithm/LinearSolvers/IpKktDumpSolverInterface.cpp
```

## 2. Wire it into the solver factory

In `src/Algorithm/IpAlgBuilder.cpp`, include the header near the other
`LinearSolvers` includes:

```cpp
#include "IpKktDumpSolverInterface.hpp"
#include <cstdlib>
```

Then, in `AlgorithmBuilder::SymLinearSolverFactory`, wrap the created
`SolverInterface` immediately before it is handed to `TSymLinearSolver`
(right before the line `SmartPtr<SymLinearSolver> ScaledSolver = new TSymLinearSolver(...)`,
currently `src/Algorithm/IpAlgBuilder.cpp:550`):

```cpp
   const char* kkt_dump = std::getenv("IPOPT_KKT_DUMP");
   if( kkt_dump != NULL && IsValid(SolverInterface) )
   {
      SolverInterface = new KktDumpSolverInterface(SolverInterface, kkt_dump);
   }

   SmartPtr<SymLinearSolver> ScaledSolver = new TSymLinearSolver(SolverInterface, ScalingMethod);
```

Rebuild Ipopt as usual (MSYS2/MinGW autotools build).

## 3. Capture real systems

Run any Ipopt-driven problem (a DWSIM flowsheet optimization, or an example)
with MA57 as the solver and the dump path set:

```bash
export IPOPT_KKT_DUMP=/path/to/flowsheet.ikkt
# ...run the problem; e.g. linear_solver=ma57 in the options...
```

On the first factorization the library prints, once, to stderr:

```
[KktDump] writing '.../flowsheet.ikkt'; MatrixFormat=0 (0=Triplet). ...
```

`MatrixFormat=0` (Triplet) with a threshold-pivoting solver such as MA57/MA27 is
the expected case and is what the managed replay understands. (For a CSR-format
solver the managed reader would need the offset handled; stick to MA27/MA57 for
capture.)

## 4. Replay through the managed solver

```bash
cd dotnet
dotnet run --project Ipopt.Sparse.Replay -- replay /path/to/flowsheet.ikkt --kind auto
```

Example report:

```
== KKT replay report (Auto, dense-threshold=1000) ==
records                : 1843
N range                : [42, 137]
total nnz              : 5123900
inertia comparable     : 1843
inertia agreements     : 1843  (100.00%)
managed singular       : 0
managed wrong inertia  : 0
max solve residual     : 7.4e-12
within dense threshold : 1843  (100.00%)
size buckets N<= [100,500,1000,2000,5000,inf] : [1203,640,0,0,0,0]
```

Read it as:

- **inertia agreements** near 100% ⇒ the managed solver would have driven the
  same inertia decisions MA57 did (first-order evidence the port would converge).
  Any `managed wrong inertia` are the dangerous cases — inspect them.
- **managed singular** counts where the managed solver bailed (zero pivot or the
  element-growth guard). A handful is fine (Ipopt perturbs and retries); a large
  fraction means the sparse static LDL needs `perturb_always_cd` / more
  regularization, or the dense path.
- **size buckets / N range** decide dense-vs-sparse for the real port.

Compare `--kind sparse` vs `--kind dense` to see how much robustness the dense
Bunch-Kaufman buys on your matrices.

## Caveats

- Capture is on **new factorizations only** (one record per distinct matrix), so
  repeated back-solves are not duplicated.
- The dump can get large (every factorization × all nonzeros). Capture a handful
  of representative flowsheets, not an overnight batch.
- Indices are assumed 0-based lower-triangle triplets, which is what the
  `SparseSymLinearSolverInterface` boundary provides for MA27/MA57. If the first
  replay shows ~0% agreement and everything singular, the index base is off —
  check the stderr `MatrixFormat` note.
