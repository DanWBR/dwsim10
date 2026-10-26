# Simultaneous Flowsheet Solver

#### Overview {#overview-51}

The sequential-modular solver converges a flowsheet one block at a time: each Recycle iterates on its own torn stream (by successive substitution, Wegstein or Broyden acceleration), and each Adjust runs its own loop, nested inside the recycle loops that surround it. On flowsheets where several recycles share compounds, or where an adjust acts on a variable inside a recycle, the nested loops interact and can take many passes or fail to converge.

The **Simultaneous Flowsheet Solver** (Patreon Edition) converges every Recycle, Energy Recycle and Adjust of the flowsheet at once. The unit operations are calculated exactly as before, by their own models; only the outer iteration changes.

#### Selecting the solver strategy

The strategy is a property of the flowsheet and is saved with it:







| **Interface** | **Where** |
|:---|:---|
| Classic (Windows) | `Solver:` button on the flowsheet toolbar, next to the simultaneous adjust button |
| Cross-platform | `Solver > Solver Strategy` menu |



###### Sequential-Modular

The default, and the strategy every file saved with an earlier version opens with. Recycles and adjusts are converged by their own blocks.

###### Simultaneous

Every steady-state solve of the flowsheet is handed to the simultaneous solver.

###### Auto

The simultaneous solver takes the flowsheet when it has already solved it once (it keeps the solution of the previous solve, see below) or when an Adjust acts inside a recycle, where the sequential solver nests its loops; otherwise the sequential solver runs.

Without a Patreon membership the log says so and the sequential solver runs. Dynamic simulations are always solved sequentially, one pass per time step.

#### Method

##### Unknowns and equations

The unknowns $\mathbf{x}$ are the state of every torn stream (temperature, pressure and the mass flow of each compound), every recycled energy flow and every manipulated variable of an Adjust. A torn stream of a single compound takes its mass enthalpy in place of the temperature, since DWSIM calculates such a stream from its pressure and enthalpy, and in two phases the temperature does not fix its state. The function $\mathbf{g}(\mathbf{x})$ is one sequential pass of the flowsheet with the recycle and adjust blocks taken out: the solver writes $\mathbf{x}$ into the torn streams and the manipulated variables, the unit operations are calculated in the usual order, and the pass returns the recalculated torn streams and the controlled variables. The residuals are


<a id="eq:simultaneous_residuals"></a>

\[
\mathbf{r}(\mathbf{x}) =
\begin{bmatrix}
\mathbf{g}_{\mathrm{tear}}(\mathbf{x}) - \mathbf{x}_{\mathrm{tear}} \\
y_{\mathrm{adjust}}(\mathbf{x}) - y_{\mathrm{target}}
\end{bmatrix} = \mathbf{0}\tag{1}
\]


and a recycle or adjust counts as converged with the tolerances set in its own block, by the same rules the block applies.

##### Newton iteration

The system is solved by a damped Newton method,


<a id="eq:simultaneous_newton"></a>

\[
\mathbf{J}\,\Delta\mathbf{x} = -\mathbf{r}(\mathbf{x}_k), \qquad
\mathbf{x}_{k+1} = \mathbf{x}_k + \lambda\,\Delta\mathbf{x}\tag{2}
\]


with a backtracking line search on $\lambda$, limits on the change of each variable in one step, and flows kept non-negative. The first matrix is minus the identity for the torn variables, so the first step is a plain successive-substitution step, plus one finite-difference column for each adjust. Broyden updates then shape the matrix from the passes the iteration makes anyway. A full finite-difference Jacobian is built only when the line search fails with the updated matrix; its columns are evaluated in parallel on copies of the flowsheet held in memory. When the Jacobian has to be built again in the same solve, the columns that move disjoint sets of residuals in the first one are perturbed together, one pass per group.

##### Warm start

The solution, and the Jacobian when one was built, are saved with the flowsheet. The next solve, in the same session or after the file is opened again, starts from them, and a small change of a feed usually converges in one or two iterations. If the current point cannot be evaluated (a unit operation fails), the solver tries the saved solution, then points halfway and a quarter of the way toward it.

##### Confirmation

When the solve converges, the solver reads the torn streams back from the flowsheet and runs one more pass. An answer that does not close on that pass (a unit operation that carries a state from one pass to the next) is solved again from there, and handed to the sequential solver if it still does not close.

##### When the solve does not converge

###### Fallback

A solve that does not converge is handed to the sequential solver, and the log says so.

###### Noise floor

Columns and other unit operations that iterate internally return results with a small noise. When the line search stops making progress, after a finite-difference Jacobian has been built, with every error within ten times its tolerance, the solver stops at the best point, without handing the flowsheet to the sequential solver, and the log says how many times the tolerance was left; loosen the recycle or adjust tolerances, or tighten the convergence tolerances of the columns. A matrix shaped by Broyden updates from the substitution start that stops making progress is replaced by a finite-difference Jacobian first.

###### Specifications without a steady state

When the finite-difference Jacobian is close to singular (condition number above $10^{8}$), the specifications may fix no steady state, or many (a closed loop whose inventory nothing sets). The log says so and names the recycle or adjust variables along the singular direction, which are the ones to check.

#### Messages

A converged solve writes a line to the log with the number of Newton iterations, the passes on the flowsheet, the Jacobian columns built (if any), whether it started warm, and the time taken. Unconverged solves give a warning with the reason and what to change.

#### Performance

The number of passes depends on how strongly the recycles interact. With tight tolerances (0.01 K, 0.1 Pa and $10^{-4}$ kg/s on every recycle):







| **Case** | **Sequential** | **Simultaneous** |
|:---|---:|---:|
| Cavett problem, torn flows halved and 10 K hotter | 25 passes | 11 passes |
| Biodiesel production (adjust inside a recycle), feeds $\times$ 1.2 | not converged | 8 passes |



On 59 flowsheets from the samples and the case library, solved as saved and again after a change of the feeds, both solvers closed every recycle and adjust and agreed within the tolerances; the one exception was a flowsheet close to having no steady state, where both stopped at different points inside the tolerance. On flowsheets with a single, loosely coupled recycle the sequential solver is as fast.

#### Limitations

- Spec blocks set to run after the flowsheet are not repeated inside each pass; Spec blocks attached to an object are.

- Recycles and adjusts marked as dynamic-only stay out of the steady-state system, as in the sequential solver.

- An adjust whose manipulated variable does not affect its controlled variable at steady state makes the system singular, and neither solver closes it.

- A tolerance below the noise of the unit operations that iterate internally is not reached by either solver.

