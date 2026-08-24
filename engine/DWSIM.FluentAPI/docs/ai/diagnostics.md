# Diagnostics for the AI Assistant

A language model assembling a flowsheet is in the same position as a person doing it for the
first time, minus the ability to look at the screen and notice that a stream goes nowhere. What
it has instead is whatever the tools tell it.

Telling it `Object reference not set to an instance of an object` teaches it nothing. Telling it
this does:

```json
{
  "code": "UNIT_NO_PRODUCT",
  "severity": "blocker",
  "object": "H-1",
  "message": "This unit operation has no product, so its result has nowhere to go.",
  "fix": "Connect a material stream to one of its outlet ports."
}
```

Same fault, same cost to detect. The difference is that the second one names the next action.

## The two calls

| MCP tool | HTTP route | When |
|---|---|---|
| `dwsim_flowsheet_check` | `GET /api/flowsheet/check` | Before solving. Cheap. |
| `dwsim_solve_diagnostics` | (in the `/api/solve` response) | After a solve failed. |

`dwsim_solve_run` and `POST /api/solve` carry `findings` alongside the raw errors, so a model
that only ever calls solve still gets told what to do. Checking first is better: it costs
microseconds against seconds, and catches the same faults.

## The response

```json
{
  "ready": false,
  "blockers": 2,
  "warnings": 1,
  "findings": [ ... ],
  "object_count": 7,
  "compound_count": 2
}
```

`ready` is the one field worth branching on: false means at least one blocker, and solving is a
waste of time until it is fixed. Findings come worst first, so acting on them top-down fixes what
matters soonest. Past 25 findings the list is capped and `truncated` with `total` says so.

## Where this fits

```
create → compounds → property package → streams → unit operations → connect
                                                                       ↓
                                                                     check
                                                                       ↓
                                                    (fix what it names, check again)
                                                                       ↓
                                                                     solve
```

The loop between check and fix is the point. A model that checks, reads a fix, applies it and
checks again converges on a working flowsheet without ever solving a broken one.

## What it will not catch

Worth telling a model plainly, so it does not read an empty list as a guarantee:

- An empty finding list means nothing *known* to be wrong. Convergence is not promised.
- A new stream comes with a default temperature, pressure and flow, so a feed nobody configured
  looks like one deliberately set. Set feed conditions explicitly; do not rely on the check to
  notice you forgot.
- Whether a unit operation has enough specifications, and whether the property package suits the
  compounds, are judgements no static rule makes.

## Codes

Codes are stable; messages are not. A model should branch on `code` and read `message` and `fix`
as prose.

| Code | Meaning |
|---|---|
| `EMPTY_FLOWSHEET` | The flowsheet has no objects. |
| `NO_COMPOUNDS` | The flowsheet has no compounds, so no stream can carry anything. |
| `NO_PROPERTY_PACKAGE` | The flowsheet has no property package, so nothing can be flashed. |
| `DUPLICATE_TAG` | Two or more objects share a tag, so addressing one by tag is ambiguous. |
| `STREAM_DANGLING` | A stream is connected to nothing at either end. |
| `ENERGY_STREAM_HALF_CONNECTED` | An energy stream is attached at one end only. |
| `UNIT_UNCONNECTED` | A unit operation has nothing connected to it. |
| `UNIT_NO_FEED` | A unit operation has no feed, so it has nothing to process. |
| `UNIT_NO_PRODUCT` | A unit operation has no product, so its result has nowhere to go. |
| `FEED_NO_PRESSURE` | A boundary feed has no pressure. |
| `FEED_NO_TEMPERATURE` | A boundary feed has no temperature. |
| `FEED_NO_FLOW` | A boundary feed carries no flow. |
| `FEED_NO_COMPOSITION` | Every compound in a boundary feed is at zero. |
| `FEED_COMPOSITION_NOT_NORMALISED` | The mole fractions of a boundary feed do not sum to 1. |
| `RECYCLE_NO_ESTIMATE` | A recycle starts from a zero estimate. |
| `LOGICAL_TARGET_MISSING` | An adjust or specification does not name both the object it reads and the one it writes. |
| `SOLVER_EXCEPTION` | The solver raised an exception. |
| `INFINITE_LOOP` | The solver found a cycle with no recycle to tear it. |
| `NOT_CONVERGED` | A unit operation did not solve. |
| `STREAM_NOT_FINITE` | A stream carries a flow that is not a finite number. |
| `NEGATIVE_FLOW` | A stream carries a negative flow. |
## Dynamic simulation codes

`dwsim_dynamics_check` and `dwsim_dynamics_diagnose` return findings in the same shape, from
their own set of codes:

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
Both tables are generated from the constants the tools read, and are served live at
`GET /api/fluent/catalog` under `diagnostics.codes` and `dynamics.diagnostic_codes`.

See also [Dynamic Simulation for the AI Assistant](dynamics.md).
