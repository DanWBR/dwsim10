# Dynamic Simulation for the AI Assistant

Two surfaces expose dynamic simulation to a language model, and they mirror each other one for
one: an **MCP server** (`tools/DWSIM.MCPServer`, headless) and the **assistant's HTTP API**
(`extensions/DWSIM.Extensions.AI.Assistant`, on the flowsheet the user has open).

Both are transport only. Every capability lives in `DWSIM.FluentAPI`'s dynamics layer, described
in [Dynamic Simulation](../api/dynamics.md), so the two cannot drift apart.

The assistant's own reasoning lives in a separate, proprietary program. This page is the contract
with it: what it can call, what comes back, and what it must not ask for.

## The workflow

```
inspect  →  properties  →  setup  →  monitor  →  event  →  check  →  run
                                                                      ↓
                                              status  →  series | analyze
                                                                      ↓
                                                        diagnose | tune_pid
```

Each step exists because the one before it produces something unguessable:

- **inspect** — what the flowsheet holds, which objects carry a dynamic model, how the
  pressure-flow network is specified, how the controllers are wired.
- **properties** — the property identifiers everything else addresses. `PROP_MS_2` is a mass
  flow; no model can know that without asking.
- **setup** — integrator step and duration, and a schedule to hold them.
- **monitor** — nothing is recorded unless it is a monitored variable. A run without them
  produces no series at all.
- **event** — the step changes and ramps that make a dynamic run worth running.
- **check** — blockers and warnings, each with a fix. Cheap, and it turns a failed run into a
  fixed one.
- **run** — returns a handle, never a series.
- **status** — progress while running; a per-variable summary once finished.
- **series** / **analyze** — a decimated preview, or the control metrics.
- **diagnose** / **tune_pid** — why it misbehaved, or better gains.

## Tools and routes

| MCP tool | HTTP route | What it does |
|---|---|---|
| `dwsim_dynamics_inspect` | `GET /api/dynamics/inspect` | Survey the flowsheet |
| `dwsim_dynamics_properties` | `GET /api/dynamics/properties` | Discover property ids |
| `dwsim_dynamics_check` | `GET /api/dynamics/check` | Readiness, with fixes |
| `dwsim_dynamics_setup` | `POST /api/dynamics/setup` | Integrator and schedule |
| `dwsim_dynamics_monitor` | `POST /api/dynamics/monitor` | Monitored variables |
| `dwsim_dynamics_event` | `POST /api/dynamics/event` | Step changes and ramps |
| `dwsim_dynamics_object` | `POST /api/dynamics/object` | Specs, dynamic properties, valve modes |
| `dwsim_dynamics_controller` | `POST /api/dynamics/controller` | Read or tune PID controllers |
| `dwsim_dynamics_state` | `POST /api/dynamics/state` | Stored initial states |
| `dwsim_dynamics_run` | `POST /api/dynamics/run` | Start a run, returns `run_id` |
| `dwsim_dynamics_status` | `GET /api/dynamics/status/{run_id}` | Progress, then summary |
| `dwsim_dynamics_abort` | `POST /api/dynamics/abort/{run_id}` | Stop at the next step |
| `dwsim_dynamics_series` | `GET /api/dynamics/series/{run_id}` | Decimated preview |
| `dwsim_dynamics_analyze` | `GET /api/dynamics/analyze/{run_id}` | Control metrics |
| `dwsim_dynamics_diagnose` | `GET /api/dynamics/diagnose/{run_id}` | Post-mortem |
| `dwsim_dynamics_export` | `POST /api/dynamics/export` | Full series to a CSV file |
| `dwsim_dynamics_tune_pid` | `POST /api/dynamics/tune-pid` | Search the gains |
| — | `POST /api/dynamics/chart` | Live plot on the flowsheet, plus a screenshot |
| — | `POST /api/dynamics/to-spreadsheet` | History into a worksheet |

The last two have no MCP equivalent: the MCP server is headless, with no canvas and no
spreadsheet to write to. There, `series` and `analyze` are the equivalent.

## Time series are large

A ten-minute run at a one-second step is 600 samples per variable. Four variables is 2,400
numbers — enough to crowd out everything else in a context window, for no benefit.

The rules are enforced by the tools, not left to the caller:

- `run` and `status` **never** return time-series points. `status` on a finished run returns a
  per-variable summary: first, last, minimum, maximum, whether it settled, whether it diverged.
- `series` is the only way to get points. Default 40, hard cap 400, six significant digits.
  Selection is largest-triangle-three-buckets with the minimum and maximum forced in, so the
  overshoot peak survives — a moving average would erase exactly what matters in a transient.
- Lists in `inspect` and `check` truncate at 25 items, with `truncated` and `total`.
- The complete series leaves only through `export`, which writes a file and returns its path.

A typical `series` response with 40 points across four variables is about 500 tokens.

## Visualisation

On the GUI surface, `POST /api/dynamics/chart` puts a live plot of the monitored variables on
the flowsheet and returns a screenshot of it in the same call. The chart is a normal flowsheet
graphic: it redraws itself from the integrator's history, keeps up during a run, and is saved
with the file. So the user sees the transient and the model sees the same picture.

`POST /api/dynamics/to-spreadsheet` writes the recorded history into an "Integrator Results"
worksheet, for whoever wants the numbers by hand.

## Concurrency

Integration drives process-wide solver state. Only one runs at a time, across every surface —
the integrator panel's Play button included.

A second request is refused rather than queued: the MCP tool reports `integrator_busy` and the
HTTP route answers `409`. Refusing is deliberate. Silently queueing behind a run the user started
by hand would corrupt the integrator's time and its recorded history.

## How the model learns all this

The assistant's prompt is not in this repository, so the surface has to describe itself:

- **MCP** — the `initialize` response carries the `instructions` field, which names both
  workflows and the series budget. Each `[McpTool]` description says what the tool does and what
  to call next.
- **HTTP** — `GET /api/fluent/catalog` carries a `dynamics` block: the workflow in order, every
  route, the integration methods, event transitions, specs, controller types, tuning objectives
  and valve calculation modes, the diagnostic codes, and the series budget.

Keep both in step with the tools when the surface changes. The catalog block is generated from
the same enumerations and constants the tools use, so most of it cannot drift.

## Diagnostic codes

Every finding — from `check` before a run and from `diagnose` after one — carries a code, a
severity, the object it concerns, a message and a fix. The fix names the call that resolves it,
so a model can act without knowing the process model.

| Code | Meaning |
|---|---|
| `NO_SCHEDULE` | The flowsheet has no dynamics schedule. |
| `NO_INTEGRATOR` | The schedule has no integrator assigned. |
| `NO_DYNAMIC_MODE` | Dynamic mode is off, so unit operations solve at steady state. |
| `NO_MONITORED_VARS` | The integrator records no variables, so the run produces no series. |
| `NOT_SOLVED_STEADY_STATE` | Some objects have never been solved; dynamics starts from an undefined state. |
| `NO_PROPERTY_PACKAGE` | The flowsheet has no property package, so nothing can be flashed. |
| `NO_COMPOUNDS` | The flowsheet has no compounds. |
| `MISSING_INITIAL_STATE` | The schedule starts from a stored state that does not exist. |
| `TOO_MANY_STEPS` | Duration divided by step gives an impractical number of steps. |
| `NO_PRESSURE_SPEC` | No stream is specified by pressure, leaving the pressure-flow network underdetermined. |
| `ALL_FLOW_SPECS` | Every stream is specified by flow, so pressure has nothing to resolve against. |
| `VALVE_NO_KV` | A valve has no flow coefficient, so it cannot pass a computed flow. |
| `VALVE_PRESSURE_DROP_MODE` | A valve is in a pressure-drop mode, so it cannot compute its own flow. |
| `VALVE_OPENING_IGNORED` | A valve passes its full Kv at any opening, so closing it does nothing. |
| `VESSEL_NO_VOLUME` | A vessel or tank has no volume, so it holds nothing up and adds no lag. |
| `PID_UNBOUND` | A controller is missing its process or manipulated variable. |
| `PID_LIMITS_INVALID` | A controller's output minimum is not below its maximum. |
| `PID_INACTIVE` | A controller is switched off or in manual, so the loop is open. |
| `UNSUPPORTED_OBJECT` | An object has no dynamic model and is solved at steady state every step. |
| `SOLVER_EXCEPTION` | The solver raised an exception and the run stopped early. |
| `NAN_IN_SERIES` | A recorded series contains NaN or infinity. |
| `DIVERGENT` | A recorded series grew without bound. |
| `SUSTAINED_OSCILLATION` | A series oscillates without decaying. |
| `MV_SATURATED` | A controller sat at its output limit for most of the run. |
| `STEP_TOO_LARGE_TRANSIENT` | A series jumps by more than half its range between adjacent steps. |
| `SLOW_STEP` | Each step took more than a second of wall time. |
| `PID_ACTION_INVERTED` | A controller consistently moved its output in the direction that increases the error. |
| `RUN_ABORTED` | The run stopped before reaching the configured duration. |
| `NOT_SETTLED` | A series had not settled by the end of the run. |

This table is generated from `DiagnosticCodes.All` in `DynamicsDiagnostics.cs`, which is the
same source the tools and the catalog read.
