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

`hessian_approximation = limited-memory` is set by every caller. Every one of them also passes
`nele_hess = 0`, so the native library never calls the `eval_h` they hand it, whatever that
callback does. `exact` is understood as well, and routes to `INlpHessian`; no caller asks for it.

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

Both are done: the constrained path is described below, and so is the comparison against the
native library.

**The Patreon edition links this facade too**, since `DWSIM_Private` commit `704a818c`: the seven
projects that referenced `DWSIM\References\Cureos.Numerics.dll` reference the project instead, and
`Cureos.Numerics.dll`, `IpOpt39.dll` and `IpOptFSS.dll` are gone from that tree along with the
`IPOPTLoader` that used to preload them. The namespace is the same, so not one call site moved.

### Invalid numbers, and a step that was never accepted

Switching the Patreon edition over turned up a defect this repository's own tests had not: on the
validation suite the NRTL interaction-parameter estimation produced `NaN` two hundred and
fifty-seven times, where the native library had produced none.

The bound-constrained line search would reject every trial point and then **take the last one
anyway**: `x[i] = xTrial[i]` ran whether or not anything had been accepted. Usually that only
wastes an iteration. When the search direction is `NaN`, which is what a central difference of a
function that is undefined a short way from the iterate gives you, it makes the iterate `NaN`, and
from there every evaluation is of a `NaN`. The solve then reported `Solve_Succeeded` over an
answer made of `NaN`.

That estimation is a two-variable problem with bounds of plus and minus ten thousand and no
analytic gradient, over an objective built from activity coefficients that is not defined across
that whole box. The native library answers `Invalid_Number_Detected`; `IPOPTSolver.Solve` turns
anything outside its accepted list into an exception, and `NRTL.vb` catches it and falls back to a
near-zero parameter set. That is why the native run printed nothing at all.

Two changes:

- a rejected step leaves `x` alone, resets the curvature and retries, with the same budget the
  constrained solver gives restoration;
- `SolveStatus.InvalidNumber`, checked on the way out of both solvers and mapped to
  `Invalid_Number_Detected`.

Afterwards the validation suite reports no estimate at all, which is what the native run did, and
all ninety-seven of its groups pass.

Worth stating plainly: **no synthetic benchmark found this.** Five thousand problems shaped like
the engine's regressions did not, because every one of them was defined everywhere it was
evaluated. Running the real edition against the real suite did.

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

### The Gibbs reactor

The synthetic battery says the two solvers behave the same on problems shaped like the engine's.
The Gibbs and Equilibrium Reactors sample says it on a real one: its reactor runs in direct
minimisation mode with `UseIPOPTSolver` on, so it minimises the Gibbs energy through
`MathEx.Optimization.IPOPTSolver` with the element balance folded into the objective as a
penalty. A steam reformer at 1000 K, methane and water in, synthesis gas out.

`GibbsReactorTests` in the smoke suite pins the managed answer against the native one, which was
produced by `DWSIM.Automation.FluentAPI.Tests.exe gibbsdump` in the DWSIM_Private tree:

| | native | managed |
|---|---|---|
| initial Gibbs energy | -808.69816311351428 | identical |
| final Gibbs energy | -984.06203055084325 | -984.06176599410151 |
| CO2 | 0.036870752 | 0.036866522 |
| CO | 0.174218073 | 0.174201965 |
| H2 | 0.670135986 | 0.670070741 |
| CH4 | 0.020079581 | 0.020076672 |
| H2O | 0.098695608 | 0.098784100 |

The objective agrees to 2.7e-7 relative and the largest composition difference is 8.9e-5. Those
two numbers are consistent rather than independent: at a minimum the energy is stationary, so an
error `eps` in composition costs `eps^2` in energy, and `sqrt(2.7e-7)` is 5e-4, the order of the
relative composition spread. Both solvers found the same minimum and stopped at different points
of its floor. The reactor asks for `tol = 1e-20`, which neither can reach, so where each stops is
its own business.

## The constrained path

`engine/DWSIM.Numerics.Ipopt.Core/ConstrainedInteriorPointSolver.cs` implements the `m > 0` case:
every constraint gets a slack, `g(x) - s = 0` with `cl <= s <= cu`, so inequalities and equalities
are one shape; the step comes from the augmented system solved by the symmetric indefinite
factorization in `DWSIM.Numerics.Ipopt.Sparse`, whose inertia drives the regularization the way
Ipopt's Algorithm IC does; and acceptance is the filter on (constraint violation, barrier
objective) rather than a merit function. `Cureos.Numerics.Ipopt` routes `m > 0` there, mapping the
triplet Jacobian onto a dense one.

It is right on problems with an independent answer. **Hock-Schittkowski 71**, the problem Ipopt
ships as its own tutorial, converges in 13 iterations to `f = 17.0140173` at
`(1, 4.74299963, 3.82114998, 1.37940829)` with an optimality error of 1.1e-9, both directly and
through the façade with the triplet Jacobian; a sum of squares on a line, an inactive inequality
and the linear shape the Gibbs flash poses all reach their analytic answers.

**It is right on the Gibbs three-phase flash too, since the iteration report was fixed.** On
ethanol and water at 355 K it converges in 26 iterations to a vapour fraction of 0.42217598
against the native library's 0.42217598, with the compositions matching to the same digits.
`tests/DWSIM.Engine.SmokeTests/GibbsThreePhaseFlashTests.cs` pins it.

What was wrong was not the arithmetic. **The solver reported an iteration in which it took no
step as though it were an ordinary one.** When the filter rejects every trial point at a feasible
point, the iteration is spent rebuilding the quasi-Newton matrix and the point does not move; the
next row of the log then carries the same objective to the last bit. This flash watches the
objective for a stall on a threshold of 1e-10 and read that repeat as convergence, ending the
solve at iteration 13 of the 26 it needed. Ipopt has the same situation and answers it by telling
the caller which mode the iteration was in, so a caller can discount it. `IterationInfo` carries
a `Restoration` flag now, and those rows go to the log but not to the caller's callback.

Three things were built chasing this, and all three are worth having:

- **The restoration phase.** When the filter blocks every trial point, `FeasibilityNlp` poses
  `min 1/2 ||g(x) - s||^2` over the same variables and their same bounds, the bound-constrained
  solver minimises it, and the iteration resumes from the point it finds with the filter, the
  multipliers and the curvature history all reset. Ipopt writes this as an l1 problem in extra
  variables; the least-squares form is smooth, which suits a quasi-Newton method, and the caller
  only needs the violation reduced enough to escape the filter rather than driven to zero. It
  never fires on this flash, whose `theta` is zero from the first iteration.
- **Gradient-based scaling**, `ScaledNlp`, which is Ipopt's `nlp_scaling_method` default and was
  the real omission. The objective and each constraint row are scaled so no gradient exceeds 100.
  Without it the Gibbs energy of a flash, in the thousands, sat next to an element balance in the
  ones, and the complementarity products the mu oracle averages carried that ratio: mu swung
  between 1e-5 and 5e3 from one iteration to the next. With it, the dual error on this problem
  fell from 3.1e1 to 1.1e-1 and the vapour fraction moved from 0.21 to 0.31.
- **The exact Hessian**, `INlpHessian` and `HessianApproximation.Exact`, which is Ipopt's
  `hessian_approximation=exact`. It is not what fixed this flash, and on this flash it is worse
  than the quasi-Newton matrix: see below.

### The exact Hessian

`INlpHessian.TryEvalHessian` hands over the Hessian of the Lagrangian, dense and row major.
A problem may decline at any point and the solver falls back to the quasi-Newton matrix it keeps
up to date regardless. `ScaledNlp` forwards it with the objective factor carrying the objective
scale and each multiplier its own row scale. The matrix is expected to be indefinite: that is
what the inertia correction is for, and a caller must not symmetrise curvature away to make it
look positive definite.

The façade maps `hessian_approximation=exact` onto it and accepts two shapes of `eval_h`. The
documented one is Ipopt's, `nele_hess` triplets holding one triangle. The other is a callback
that ignores the structure it declared and replaces the value array with a full `n` by `n` block,
which is what **every** `eval_h` in this engine does; it is recognised by the length of what
comes back. Because every one of them is handed to a constructor with `nele_hess = 0`, the native
library never called any of them, and two bugs had sat unnoticed in `FunctionHessian` since it
was written: the difference was taken against `f3(k)` with `k` never leaving zero, so only the
first column of each row was a derivative at all, and the step was purely relative, so a mole
number sitting at zero was never perturbed and its row came out empty. Both are fixed, in
`GibbsMinimization3P` and in `GibbsMinimizationMulti`.

Measured on the ethanol-water flash with the exact Hessian switched on: the run reaches -730.8
at iteration 4, then wanders, with search directions of norm 1e5, and ends at a vapour fraction
of 0.053. The finite-difference Hessian of a gradient built from fugacity coefficients is too
noisy to steer with. The flash stays on `limited-memory`, which is what the native run uses.

On Hock-Schittkowski 71, whose Hessian is analytic, exact and quasi-Newton both converge in
thirteen iterations to the same answer; `ConstrainedTests` pins that, and `CureosFacadeTests`
pins the dense return shape through the façade.

Two things measured along the way that are worth not repeating:

- Making the adaptive mu non-increasing sounds right and makes this problem worse, not better:
  the vapour fraction goes from 0.31 to 0.
- The iteration log and the intermediate callback must carry the caller's objective, not the
  scaled one. The flash's stall detector compares consecutive objective values against a fixed
  1e-10, and under a scale factor that threshold means something entirely different.
- The exact Hessian makes this flash worse, not better, for the reason given above. It was tried
  twice, once before the reporting bug was found and once after.

Two things follow from that, and both are in the tree:

- `SolveStatus.LineSearchFailure` maps to `Error_In_Step_Computation`, not to
  `Search_Direction_Becomes_Too_Small`. Every caller in the engine treats the latter as a usable
  answer, so a stalled solve was being consumed as a converged one, which is how a flash comes to
  report a phase split it never computed.
- `GibbsThreePhaseFlashTests.TheGibbsFlashMatchesTheNativeSolver` carries the native numbers and
  is marked `Ignore` with the reason. Remove the `Ignore` when restoration lands.

Two earlier defects in this file are worth remembering, because both produced plausible answers
rather than obvious failures: an equality's residual is `g(x) - c`, not `g(x) - 0`; and both ends
of the quasi-Newton curvature pair have to use the multipliers the step produced, since the matrix
approximates the Hessian of one Lagrangian and mixing an old and a new `y` measures nothing.

The Gibbs reactor of the sample above does not go through any of this: it poses no constraints.
