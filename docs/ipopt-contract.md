# The IPOPT surface DWSIM calls

DWSIM talks to IPOPT through `Cureos.Numerics`, a .NET Framework wrapper around the native
`Ipopt39.dll`. A managed replacement drops into the engine unchanged as long as it keeps the
namespace, the type names and the signatures below: every call site was written against them
and none of them needs to move.

The engine reaches IPOPT from eight places. Two of them build the problem themselves; the
rest go through `MathEx.Optimization.IPOPTSolver`, a thin unconstrained wrapper.

| Caller | Constraints | What it solves |
|---|---|---|
| `DWSIM.Thermodynamics/FlashAlgorithms/GibbsMinimization3P.vb` | `m = n + 1`, dense Jacobian, analytic Hessian | Gibbs energy minimisation, three phases |
| `DWSIM.Thermodynamics/FlashAlgorithms/GibbsMinimizationMulti.vb` | none | Gibbs energy minimisation, many phases |
| `DWSIM.Thermodynamics/FlashAlgorithms/NestedLoops.vb`, `NestedLoops_v2.vb` | none, via `IPOPTSolver` | fallback when the nested loops stall |
| `DWSIM.Thermodynamics/PropertyPackages/{NRTL,UNIQUAC,WilsonPropertyPackage}.vb` | none, via `IPOPTSolver` | binary interaction parameter regression |
| `DWSIM.SharedClasses.DataRegression/Engine/RegressionEngine.vb` | none | pure compound property regression |
| `DWSIM/Forms/FlowsheetAnalysis/FormOptimization.vb` | none | the flowsheet optimiser |
| `DWSIM.Plugins.NaturalGas/DewPointFinder.vb` | none | water dew point |

`DWSIM.UnitOperations/Reactors/Gibbs.vb` reaches IPOPT through the flash algorithms, and gets
its initial estimate from `lpsolve55` instead, which is a separate native dependency with the
same portability problem.

## Namespace and types

```csharp
namespace Cureos.Numerics
```

### Ipopt

```csharp
public class Ipopt : IDisposable
{
    public const double PositiveInfinity = 2e19;
    public const double NegativeInfinity = -2e19;

    public Ipopt(int n, double[] x_L, double[] x_U,
                 int m, double[] g_L, double[] g_U,
                 int nele_jac, int nele_hess,
                 EvaluateObjectiveDelegate eval_f,
                 EvaluateConstraintsDelegate eval_g,
                 EvaluateObjectiveGradientDelegate eval_grad_f,
                 EvaluateJacobianDelegate eval_jac_g,
                 EvaluateHessianDelegate eval_h);

    public bool AddOption(string keyword, string val);
    public bool AddOption(string keyword, double val);
    public bool AddOption(string keyword, int val);

    public bool OpenOutputFile(string file_name, int print_level);
    public bool SetScaling(double obj_scaling, double[] x_scaling, double[] g_scaling);
    public bool SetIntermediateCallback(IntermediateDelegate intermediate);

    public IpoptReturnCode SolveProblem(double[] x, ref double obj_val, double[] g,
                                        double[] mult_g, double[] mult_x_L, double[] mult_x_U);

    public void Dispose();
}
```

Notes the call sites depend on:

- `m = 0` with `g_L`, `g_U` passed as `null` means an unconstrained problem, and `eval_g`,
  `eval_jac_g` and `eval_h` are then allowed to just return `false`.
- Every caller wraps the instance in `Using` / `using`, so `Dispose` must be safe to call once
  and must release whatever the solver holds.
- `SolveProblem` is called with `g`, `mult_g`, `mult_x_L` and `mult_x_U` as `null` everywhere
  except `GibbsMinimization3P`, which passes a `g` array and `null` for the multipliers.
- `x` is updated in place with the solution; `obj_val` comes back by reference.
- Calls are wrapped in a `SyncLock` in `IPOPTSolver` because the native library is not
  reentrant. A managed implementation that is reentrant lets that lock go, which matters:
  the flashes run under `Parallel.For`.

### Delegates

```csharp
public delegate bool EvaluateObjectiveDelegate(
    int n, double[] x, bool new_x, ref double obj_value);

public delegate bool EvaluateObjectiveGradientDelegate(
    int n, double[] x, bool new_x, ref double[] grad_f);

public delegate bool EvaluateConstraintsDelegate(
    int n, double[] x, bool new_x, int m, ref double[] g);

public delegate bool EvaluateJacobianDelegate(
    int n, double[] x, bool new_x, int m, int nele_jac,
    ref int[] iRow, ref int[] jCol, ref double[] values);

public delegate bool EvaluateHessianDelegate(
    int n, double[] x, bool new_x, double obj_factor, int m, double[] lambda,
    bool new_lambda, int nele_hess, ref int[] iRow, ref int[] jCol, ref double[] values);

public delegate bool IntermediateDelegate(
    IpoptAlgorithmMode alg_mod, int iter_count, double obj_value,
    double inf_pr, double inf_du, double mu, double d_norm,
    double regularization_size, double alpha_du, double alpha_pr, int ls_trials);
```

The `ref` on the array parameters is what the callers write to: `eval_grad_f` assigns a whole
new array to `grad_f` rather than filling the one it was given. `eval_jac_g` and `eval_h`
follow the IPOPT convention: when `values` is `null` they fill `iRow` and `jCol` with the
sparsity pattern, otherwise they fill `values`.

Returning `false` from `intermediate` stops the solve, and `IPOPTSolver` uses that to bail out
when the objective stops moving. The status that comes back is `User_Requested_Stop`, which
every caller treats as a success.

### Enums

```csharp
public enum IpoptAlgorithmMode
{
    RegularMode = 0,
    RestorationPhaseMode = 1,
}

public enum IpoptReturnCode
{
    Solve_Succeeded = 0,
    Solved_To_Acceptable_Level = 1,
    Infeasible_Problem_Detected = 2,
    Search_Direction_Becomes_Too_Small = 3,
    Diverging_Iterates = 4,
    User_Requested_Stop = 5,
    Feasible_Point_Found = 6,
    Maximum_Iterations_Exceeded = -1,
    Restoration_Failed = -2,
    Error_In_Step_Computation = -3,
    Maximum_CpuTime_Exceeded = -4,
    Not_Enough_Degrees_Of_Freedom = -10,
    Invalid_Problem_Definition = -11,
    Invalid_Option = -12,
    Invalid_Number_Detected = -13,
    Unrecoverable_Exception = -100,
    NonIpopt_Exception_Thrown = -101,
    Insufficient_Memory = -102,
    Internal_Error = -199,
}
```

The numbers are IPOPT's own `ApplicationReturnStatus`. Nothing in DWSIM stores them, so a
managed implementation is free to keep the names and ignore the values, but keeping both costs
nothing and makes the two interchangeable while both exist.

## Options the callers set

Only these six keywords are ever passed, so an implementation that understands them and
ignores the rest is enough:

| Keyword | Type | Values used |
|---|---|---|
| `tol` | double | `1e-14` to `1e-4`, usually from the caller's own tolerance |
| `max_iter` | int | 100, 1000, or the caller's iteration budget |
| `print_level` | int | 0 and 1 |
| `mu_strategy` | string | `adaptive` |
| `hessian_approximation` | string | `limited-memory` |
| `expect_infeasible_problem` | string | `yes` (commented out today, in `IPOPTSolver`) |

`hessian_approximation = limited-memory` is set by every unconstrained caller, which is why
their `eval_h` returns `false` and never fills anything. `GibbsMinimization3P` is the only one
that supplies a real Hessian.

## What DWSIM does with the result

`IPOPTSolver.Solve` keeps every point `eval_f` was called with, and by default returns the one
with the lowest objective value rather than the solver's final `x` (`ReturnLowestObjFuncValue`).
Callers treat `Solve_Succeeded`, `Solved_To_Acceptable_Level`, `Restoration_Failed`,
`Feasible_Point_Found`, `Search_Direction_Becomes_Too_Small`, `Infeasible_Problem_Detected`,
`Maximum_Iterations_Exceeded` and `User_Requested_Stop` as usable answers, and throw on the
rest. So an implementation that gives up may return `Maximum_Iterations_Exceeded` and the
engine will carry on with the best point it saw, which is the existing behaviour.

## Verification

The reference is the current answer, not the literature: run the flowsheet corpus with the
native IPOPT and with the managed one, and compare the converged streams. The cases that
exercise it hardest are the Gibbs reactor samples and the binary interaction parameter
regression, both of which are in `tests/flowsheets`.

## Status

The solver behind the façade now exists in this repository:

| Project | What it holds |
|---|---|
| `engine/DWSIM.Numerics.Ipopt.Sparse` | QDLDL, dense Bunch-Kaufman, dense Cholesky, and the adapter that presents them the way Ipopt's linear solver contract expects |
| `engine/DWSIM.Numerics.Ipopt.Core` | Primal-dual interior point for `m = 0`, limited-memory BFGS with history 6, adaptive mu through the LOQO oracle, and an Ipopt-format iteration log |
| `tools/IpoptKktReplay` | Replays KKT systems captured from a native run, and generates synthetic ones |

Both libraries target `netstandard2.0` as well as `net10.0`, so the .NET Framework build of the
Patreon edition can consume the same assemblies.

`engine/DWSIM.Numerics.Ipopt` now implements the façade over that solver. It maps the five
options the engine sets, forwards the intermediate callback (returning false stops the solve and
comes back as `User_Requested_Stop`, which is how `IPOPTSolver` abandons a stalled objective),
writes the answer into the array the caller passed, and falls back to central differences with
`eps = 0.001` when no gradient delegate is supplied. A caller's `eval_f` or `eval_grad_f`
returning false comes back as `Invalid_Number_Detected`.

Two things are still missing:

1. The constrained path, for `GibbsMinimization3P`: `m = n + 1`, dense Jacobian, analytic
   Hessian, and a real filter line search instead of the Armijo one, which is only equivalent
   while the infeasibility measure is identically zero. `SolveProblem` throws with the
   constraint count when it is handed one of these.
2. The comparison against the native library. It has to run on the .NET Framework build of the
   engine, which is the only one where `Ipopt39.dll` and the managed solver both exist.

## The comparison against the native library

Run in the .NET Framework build, where `Ipopt39.dll` and the managed solver both exist:

```
DWSIM.Automation.FluentAPI.Tests.exe ipoptab -- 5000
```

The harness hands the identical delegate to `MathEx.Optimization.IPOPTSolver`, which binds the
native library, and to `DwsimIpoptSolver`, and compares the objective reached. Both get the
tolerance and iteration cap the engine uses for a binary interaction parameter fit: `tol = 1e-4`,
`max_iter = 100`.

Over 5,004 problems, all bound-constrained, mostly randomised least squares of the size and
conditioning the engine's regressions have, a third of them with a bound the solution wants to
cross:

- **4,694 agree to a relative 1e-6** in the objective.
- 283 end with the native library lower, 27 with the managed one lower. The largest of those
  differences is a relative **9.1e-5**, which is below the 1e-4 both solvers were asked for.
- Nothing failed or threw.

On the named cases the managed solver does better rather than worse. Rosenbrock from
`(-1.2, 1)` reaches `4.3e-14` against the native `2.7e-11`, in 35 iterations against 41. A
two-parameter fit with the `+/-10000` bounds of a binary interaction regression reaches
`2.4e-6` against `6.1e-3`, in 4 iterations against 8. That last one is also where `|dx|` is
largest, `1.4e3`, because the objective is nearly flat in those variables: the two solvers stop
at very different points of the same valley, and the managed one stops lower.

What this does not cover: the constrained path, which does not exist, and therefore the Gibbs
three-phase flash.
